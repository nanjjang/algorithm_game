using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 보스 한 마리의 싸움 문맥이자 뼈대의 런타임 상태. BossState.Brain에 들어 있다(BossSystem.Context(w)로 꺼낸다).
    /// 두뇌는 이 객체로 세계·몸통·데이터를 읽고, 아래 도우미로 탄·졸개·이동을 처리한다.
    /// "뼈대 전용"이라고 적힌 필드는 두뇌가 읽기만 한다.
    /// </summary>
    public sealed class BossContext
    {
        public SimWorld W;
        public BossState Boss;
        /// <summary>보스 몸통(IsBoss인 EnemyState). 주인은 Bosses 모듈이라 두뇌가 Pos·Facing·Tags·Burrowed·AnimClip을 바꿔도 된다.</summary>
        public EnemyState Body;
        public BossCommonDef Common;
        public BossTuning Tuning;
        public IBossBrain Brain;
        /// <summary>뼈대(돌진 시작·장애물 세우기 같은 공용 동작을 두뇌가 부를 때 쓴다).</summary>
        public BossSystem Sys;
        /// <summary>지금 단계·회차의 덮어쓰기를 반영한 패턴 표(단계가 바뀌면 뼈대가 다시 만든다).</summary>
        public Dictionary<string, BossPattern> Patterns = new Dictionary<string, BossPattern>();

        // ---- 뼈대 전용 런타임
        /// <summary>등장이 끝난 틱(격노 시계의 0).</summary>
        public int FightStartTick;
        /// <summary>Waiting의 남은 틱(동작 멈춤 중에는 줄지 않는다).</summary>
        public int WaitLeft;
        public int RecoveryLeft;
        /// <summary>빈틈 중 받는 피해 배율(가마등 기절 1.5). 빈틈이 아니면 1.</summary>
        public float RecoveryDamageRatio = 1f;
        public BossPatternRun Run;
        /// <summary>이번 고르기에서 R5로 막혀 뺀 패턴(다시 Waiting으로 돌아가면 비운다).</summary>
        public readonly List<string> Excluded = new List<string>();
        public bool FightEnded;
        public int ReflectionBloomIndex;
        public bool ReflectionDownDone;
        public bool MasterLineDone;
        public bool DeathHandled;
        /// <summary>이 모듈이 InputLocked를 켜 둔 상태인가.</summary>
        public bool InputLockApplied;
        /// <summary>BossPatternStarted 이벤트의 A·B에 실을 값. 두뇌가 OnPatternStart에서 채운다(허상: 따라 쓰는 비기 번호, 예고 틱).</summary>
        public int PatternEventA, PatternEventB;

        // ---- 임시 목록(틱 안에서만 쓴다. 세계마다 문맥이 따로라 서로 닿지 않는다)
        public readonly List<string> CandidateBuf = new List<string>();
        public readonly List<ZoneEntity> ZoneEntityBuf = new List<ZoneEntity>();
        public readonly List<Vector2> PointBuf = new List<Vector2>();

        public BossDef Def => Boss.Def;
        public Pcg32 Rng => W.Rng(RngStream.Boss);
        public bool Halted => W.Tick < Boss.ActionHaltUntilTick;
        public BossPhaseDef PhaseDef =>
            Def.Phases != null && Boss.Phase >= 0 && Boss.Phase < Def.Phases.Count ? Def.Phases[Boss.Phase] : null;
        /// <summary>순환 2회차 이상인가(second_cycle·변이 복사).</summary>
        public bool SecondCycle => Boss.Cycle >= 2;

        public BossPattern Pattern(string key)
        {
            BossPattern p;
            return key != null && Patterns.TryGetValue(key, out p) ? p : null;
        }

        public float HitRadius(PlayerState p)
        {
            return p != null && p.Def != null ? p.Def.HitRadius : BossRules.BaselineHitRadius(W.Content);
        }

        /// <summary>몸통에서 pos 쪽 단위 벡터. 겹쳐 있으면 지금 시선.</summary>
        public Vector2 DirTo(Vector2 pos)
        {
            Vector2 d = pos - Body.Pos;
            return d.sqrMagnitude > 0f ? d.normalized : Body.Facing;
        }

        public void SetAnim(string clip)
        {
            if (Body == null || clip == null || Body.AnimClip == clip) return;
            Body.AnimClip = clip;
            Body.AnimStartTick = W.Tick;
        }

        // ------------------------------------------------------------ 이동

        /// <summary>to 쪽으로 한 틱 걷는다(감속 반영, 벽·장애물에서 멈춤). 움직였으면 true.</summary>
        public bool Chase(Vector2 to, float speed)
        {
            Vector2 d = to - Body.Pos;
            float dist = d.magnitude;
            if (dist <= Tuning.ChaseStopDistance) return false;
            float step = speed * (1f - Body.SlowStrength(W.Tick)) * SimConst.TickSeconds;
            if (step <= 0f) return false;
            if (step > dist) step = dist;
            return MoveBody(d / dist * step);
        }

        /// <summary>몸통을 delta만큼 옮긴다(벽은 몸 반지름으로 자르고, 장애물에 막히면 움직이지 않는다).</summary>
        public bool MoveBody(Vector2 delta)
        {
            if (delta.sqrMagnitude <= 0f) return false;
            Vector2 from = Body.Pos;
            Vector2 to = W.ClampToArena(from + delta, Body.BodyRadius);
            if ((to - from).sqrMagnitude <= 0f) return false;
            ObstacleEntity hit;
            if (W.Mods.Threats.SegmentHitsObstacle(W, from, to, false, out hit)) return false;
            Body.Pos = to;
            Body.Facing = delta.normalized;
            return true;
        }

        // ------------------------------------------------------------ 간격

        /// <summary>반복 간격(틱). 연사형(rapid_fire)이면 격노로 줄이고 min_interval_ticks를 하한으로 둔다.</summary>
        public int IntervalTicks(BossStep step)
        {
            int baseTicks = SimConst.Ticks(step.RepeatIntervalSeconds);
            if (!step.Def.RapidFire) return baseTicks;
            return BossRules.RapidIntervalTicks(Common, baseTicks, Boss.RageStacks, step.Def.MinIntervalTicks);
        }

        // ------------------------------------------------------------ 탄

        /// <summary>적 탄 하나(Threats.SpawnShot). 화면 상한이면 null.</summary>
        public EnemyShotEntity Fire(Vector2 pos, Vector2 dir, float speed, float radius, float range, int baseDamage, bool grazeable, int castId)
        {
            var spec = new EnemyShotSpec
            {
                OwnerEnemyId = Body.Id, CastId = castId, Pos = pos, Dir = dir, Speed = speed, Radius = radius,
                Range = range, BaseDamage = baseDamage, Grazeable = grazeable
            };
            return W.Mods.Threats.SpawnShot(W, spec);
        }

        /// <summary>고리 탄: first 방향부터 spacingDegrees 간격으로 count발(각도 순서). 만든 수를 돌려준다.</summary>
        public int FireRing(Vector2 pos, Vector2 first, int count, float spacingDegrees, float speed, float radius, float range,
            int baseDamage, bool grazeable, int castId)
        {
            int made = 0;
            for (int i = 0; i < count; i++)
                if (Fire(pos, BossRules.Rotate(first, spacingDegrees * i), speed, radius, range, baseDamage, grazeable, castId) != null) made++;
            return made;
        }

        /// <summary>부채 탄: center를 가운데로 spacingDegrees 간격 count발.</summary>
        public int FireSpread(Vector2 pos, Vector2 center, int count, float spacingDegrees, float speed, float radius, float range,
            int baseDamage, bool grazeable, int castId)
        {
            int made = 0;
            float start = -spacingDegrees * (count - 1) / 2f;
            for (int i = 0; i < count; i++)
                if (Fire(pos, BossRules.Rotate(center, start + spacingDegrees * i), speed, radius, range, baseDamage, grazeable, castId) != null) made++;
            return made;
        }

        /// <summary>공격 정의의 proj대로 쏜다(ring_shot = 고리, spread = 부채, 그 밖 = aim 방향 한 줄).</summary>
        public int FireAttack(AttackDef attack, Vector2 pos, Vector2 aim, float extraDegrees, int castId)
        {
            var proj = attack != null ? attack.Proj : null;
            if (proj == null) return 0;
            float range = proj.Range.HasValue ? proj.Range.Value : -1f;
            Vector2 dir = BossRules.Rotate(aim, extraDegrees);
            if (attack.Kind == BossRules.KindRingShot)
            {
                float spacing = proj.SpreadDegrees > 0f ? proj.SpreadDegrees : FullTurnDegrees / Math.Max(1, proj.Count);
                return FireRing(pos, dir, proj.Count, spacing, proj.Speed, proj.HitRadius, range, attack.Damage, attack.Grazeable, castId);
            }
            return FireSpread(pos, dir, proj.Count, proj.SpreadDegrees, proj.Speed, proj.HitRadius, range, attack.Damage, attack.Grazeable, castId);
        }

        public const float FullTurnDegrees = 360f;
        public const float HalfTurnDegrees = 180f;

        // ------------------------------------------------------------ 졸개

        /// <summary>이 보스가 불러낸 살아 있는 개체 수(SummonerId 기준). defId가 null이면 종을 가리지 않는다.</summary>
        public int CountSummoned(string defId)
        {
            int n = 0;
            var list = W.Enemies;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (!e.Alive || e.SummonerId != Body.Id) continue;
                if (defId != null && e.DefId != defId) continue;
                n++;
            }
            return n;
        }

        /// <summary>
        /// 졸개를 부른다(NoReward, SummonerId = 몸통 Id). field_cap이 있으면 SummonerId로 센 수가 상한을 넘지 않게 줄인다.
        /// positions가 null이면 몸통 둘레에 고르게 둔다. 부른 수를 돌려준다.
        /// </summary>
        public int Summon(BossSummonDef summon, IReadOnlyList<Vector2> positions)
        {
            if (summon == null || string.IsNullOrEmpty(summon.Enemy)) return 0;
            int count = summon.Count;
            if (summon.FieldCap > 0) count = Math.Min(count, summon.FieldCap - CountSummoned(summon.Enemy));
            int made = 0;
            for (int i = 0; i < count; i++)
            {
                Vector2 pos;
                if (positions != null && i < positions.Count) pos = positions[i];
                else
                {
                    Vector2 offset = BossRules.Rotate(Body.Facing, FullTurnDegrees * i / Math.Max(1, summon.Count));
                    pos = Body.Pos + offset * (Body.BodyRadius + Tuning.SummonRingRadius);
                }
                var def = W.Content != null ? W.Content.Enemy(summon.Enemy) : null;
                pos = W.ClampToArena(pos, def != null ? def.BodyRadius : 0f);
                var spec = new EnemySpawnSpec { DefId = summon.Enemy, Pos = pos, NoReward = true, SummonerId = Body.Id };
                var e = W.Mods.Enemies.Spawn(W, spec);
                if (e == null) continue;
                made++;
                W.Emit(SimEventType.BossMinionSpawned, entityId: e.Id, pos: pos, id: summon.Enemy);
            }
            return made;
        }

        // ------------------------------------------------------------ 위협 개체 조작은 주인 모듈에 위임

        /// <summary>남는 구역을 끈다(불씨 소화).</summary>
        public void ExpireHazard(HazardEntity h)
        {
            W.Mods.Threats.RemoveHazard(W, h);
        }

        /// <summary>장애물을 부순다(돌진이 지나간 바위·기둥).</summary>
        public void BreakObstacle(ObstacleEntity o)
        {
            W.Mods.Threats.DestroyObstacle(W, o, broken: true);
        }

        // ------------------------------------------------------------ 예고 구역의 기본 배치

        /// <summary>
        /// 공격 정의에서 예고 구역을 만든다. 원·고리는 anchor(self = 몸통, player_pos_at_start = 플레이어)나 placement대로,
        /// 띠·조준선·부채는 몸통에서 플레이어 쪽(+ 반복 회전). 돌진 띠는 피해 0·Dash 분류(판정은 몸 충돌이 한다).
        /// 두뇌가 BuildZones 안에서 불러 놓고 고쳐 써도 된다.
        /// </summary>
        public void DefaultZones(BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones)
        {
            var a = step.Attack;
            if (a == null || target == null) return;
            var shape = BossRules.ParseShape(a.Shape);
            bool lockLast = a.Lock == BossRules.LockLastTicks;
            var spec = new ZoneSpec
            {
                Shape = shape, Anchor = ZoneAnchor.Fixed, Radius = a.Radius, InnerRadius = a.InnerRadius, Width = a.Width,
                Length = a.Length.HasValue ? a.Length.Value : 0f, FanDegrees = a.FanDegrees,
                TelegraphTicks = SimConst.Ticks(a.TelegraphSeconds), LockLastTicks = lockLast ? SimConst.Ticks(a.LockSeconds) : 0,
                BaseDamage = a.Damage, DamageSource = BossRules.ParseDamageSource(a.DamageSource), Grazeable = a.Grazeable,
                LeavesHazard = a.LeavesHazard, TargetSlot = target.Slot
            };
            if (step.IsDash)
            {
                // 돌진의 피해는 몸 충돌(Player.QueueHit, Dash)로 준다. 띠는 길을 보여 줄 뿐이고, 지나간 길의 남는 구역은 두뇌가 깐다.
                spec.BaseDamage = 0;
                spec.DamageSource = PlayerDamageSource.Dash;
                spec.Grazeable = false;
                spec.LeavesHazard = null;
            }
            int count = Math.Max(1, step.Count);
            Vector2 aim = BossRules.Rotate(DirTo(target.Pos), step.RepeatAngleDegrees * inst.RepeatIndex);
            if (shape == ZoneShape.Circle || shape == ZoneShape.Ring)
            {
                PointBuf.Clear();
                if (step.Def.Placement != null) Place(step.Def.Placement, count, target.Pos, PointBuf);
                else
                {
                    Vector2 at = a.Anchor == BossRules.AnchorPlayerPos ? target.Pos : Body.Pos;
                    for (int i = 0; i < count; i++) PointBuf.Add(at);
                }
                for (int i = 0; i < PointBuf.Count; i++)
                {
                    spec.Pos = PointBuf[i];
                    spec.Dir = aim;
                    zones.Add(spec);
                }
                return;
            }
            // 띠 여러 개는 반 바퀴를, 부채 여러 개는 한 바퀴를 고르게 나눈다(둘이면 십자).
            float turn = (shape == ZoneShape.Fan ? FullTurnDegrees : HalfTurnDegrees) / count;
            for (int i = 0; i < count; i++)
            {
                spec.Pos = Body.Pos;
                spec.Dir = BossRules.Rotate(aim, turn * i);
                spec.AimAtPlayer = lockLast && i == 0;
                zones.Add(spec);
            }
        }

        /// <summary>
        /// placement 규칙으로 원 중심을 고른다: at_player개는 플레이어 위치, 나머지는 거부 표본(max_tries, 실패하면 그 원은 생략).
        /// 조건: 플레이어와 min_player_distance 이상(max가 있으면 이하), 무작위 원끼리 min_spacing 이상, 벽에서 wall_margin 이상.
        /// </summary>
        public void Place(JObject placement, int count, Vector2 playerPos, List<Vector2> into)
        {
            int atPlayer = Math.Min(count, BossTuning.Int(placement, "at_player", 0));
            float minPlayer = BossTuning.Float(placement, "min_player_distance", 0f);
            float maxPlayer = BossTuning.Float(placement, "max_player_distance", 0f);
            float spacing = BossTuning.Float(placement, "min_spacing", 0f);
            float wall = BossTuning.Float(placement, "wall_margin", 0f);
            int tries = Math.Max(1, BossTuning.Int(placement, "max_tries", 1));
            for (int i = 0; i < atPlayer; i++) into.Add(playerPos);
            int firstRandom = into.Count;
            var rng = Rng;
            for (int i = atPlayer; i < count; i++)
            {
                for (int t = 0; t < tries; t++)
                {
                    Vector2 pos;
                    if (maxPlayer > 0f)
                    {
                        Vector2 dir = BossRules.Rotate(new Vector2(1f, 0f), rng.Range(0f, FullTurnDegrees));
                        pos = playerPos + dir * rng.Range(minPlayer, maxPlayer);
                    }
                    else pos = new Vector2(rng.Range(wall, W.ArenaWidth - wall), rng.Range(wall, W.ArenaHeight - wall));
                    if (!W.InArena(pos, wall)) continue;
                    float dp = (pos - playerPos).magnitude;
                    if (dp < minPlayer) continue;
                    if (maxPlayer > 0f && dp > maxPlayer) continue;
                    bool ok = true;
                    for (int k = firstRandom; k < into.Count && ok; k++)
                        if ((into[k] - pos).magnitude < spacing) ok = false;
                    if (!ok) continue;
                    into.Add(pos);
                    break;
                }
            }
        }
    }

    /// <summary>step의 반복 하나(예고 하나 또는 타격 하나)의 진행.</summary>
    public enum BossStepPhase { Pending, AwaitGrant, Telegraph, Warn, Act, Dash, Busy, Done }

    public sealed class BossStepInstance
    {
        public int RepeatIndex;
        public BossStepPhase Phase;
        /// <summary>스침 묶음 번호(이 반복의 구역·탄·돌진이 함께 쓴다).</summary>
        public int CastId;
        public int Ticket;
        public int LaunchTick;
        public int BlockedSinceTick;
        public int RequestTick;
        /// <summary>예고 시작 틱(허가된 틱)과 판정 틱(Threats.TryGetTiming).</summary>
        public int StartTick;
        public int DetonateTick;
        public int WarnUntilTick;
        /// <summary>요청한 구역(판정 뒤 장애물 자리 등에 쓴다).</summary>
        public readonly List<ZoneSpec> Zones = new List<ZoneSpec>();
        /// <summary>고정된 방향(첫 구역의 방향. 돌진·조준 탄이 쓴다).</summary>
        public Vector2 Dir;
        public bool DirSet;
        public bool Acted;
        public int LastTickProcessed = -1;
        // 돌진
        public int DashTicksLeft;
        public int DashHitMask;
        public float DashTravelled;
        /// <summary>두뇌 전용 값(불씨 띠의 조각 시작점 등).</summary>
        public Vector2 ScratchPos;
        public float ScratchFloat;
        public object Scratch;
    }

    /// <summary>진행 중인 패턴 하나.</summary>
    public sealed class BossPatternRun
    {
        public BossPattern Pattern;
        public int StartTick;
        public int StepIndex;
        /// <summary>지금 step에서 띄운 반복 수.</summary>
        public int Launched;
        public int StepNotBeforeTick;
        public bool GateOpen = true;
        public int NextLaunchTick;
        /// <summary>패턴의 첫 예고가 허가됐다(그 전의 R5 막힘은 다른 패턴을 다시 고른다).</summary>
        public bool FirstGranted;
        /// <summary>지금 step에서 고정된 방향(예고 없는 뒤 반복이 물려받는다).</summary>
        public Vector2 StepDir;
        public bool StepDirSet;
        public bool Started;
        public readonly List<BossStepInstance> Active = new List<BossStepInstance>();
        public readonly List<ZoneSpec> ZoneBuf = new List<ZoneSpec>();
    }
}
