using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>
    /// 밤삼킴(boss.night_serpent, 05 요약서 9.6절). 어둠 침의 조준선·연사와 잠행 원의 예고·판정 틱은 뼈대가 데이터대로 실행하고, 여기서는
    ///  - 마디 몸: 머리가 지나간 길을 따라 마디 10개 + 꼬리가 간격 1.0으로 따라온다(BossState.Segments, 마지막이 꼬리).
    ///    접촉은 머리·마디·꼬리 어느 것이든 "같은 적" 하나(뼈대의 접촉 쿨다운, AttackId = 몸통 Id).
    ///    받는 피해: 머리 ×1.0, 마디·꼬리 ×0.5(맞은 자리로 가린다).
    ///  - 또아리: 고리 예고가 터지는 틱부터 active_ticks(240) 동안 몸으로 닫힌 고리를 만든다(고리 띠에 닿으면 접촉 피해 —
    ///    같은 적 쿨다운). 가둠이 끝난 틱에 무는 띠 예고: 머리 → 그 틱의 플레이어 위치 → 고리 반대편, 판정 틱에 16칸/초로 문다.
    ///  - 잠행: 패턴 동안 땅속(피해·효과·접촉 없음), 원이 솟는 틱마다 그 자리로 옮기고 세 번째에 땅 위로 나온다.
    ///  - 비늘 탄(2단계 상시): 움직이는 동안 1.5초마다 마디가 0.6초 깜박인 뒤 바깥쪽으로 1발씩.
    /// </summary>
    public sealed class NightSerpentBrain : BossBrainBase
    {
        public const string PatternCoil = "coil", PatternSpit = "dark_spit", PatternBurrow = "burrow", PatternVolley = "scale_volley";
        public const string ExtraName = "G08", ExtraSection = "night_serpent";
        // 그림 클립 이름(monsters manifest의 boss.night_serpent).
        const string ClipMove = "move", ClipBite = "bite", ClipSpit = "spit", ClipSink = "sink", ClipRise = "rise", ClipRest = "rest",
            ClipPhase = "phase";

        // 보충 수치(G08.json). 처음 값은 조각을 못 읽었을 때 쓰고 조각과 같다.
        public float TrailSampleDistance = 0.1f, PartHitSlack = 0.1f;

        SerpentBodyDef _body = new SerpentBodyDef();
        /// <summary>머리가 지나간 자리(가장 최근 것부터).</summary>
        readonly List<Vector2> _trail = new List<Vector2>();
        readonly List<Vector2> _parts = new List<Vector2>();
        Vector2 _lastHead;
        bool _moving;

        // 또아리
        bool _coilApproach, _coilActive;
        Vector2 _coilCenter;
        float _coilInner, _coilOuter, _coilAngle;
        int _coilUntilTick;
        float _biteLength;

        // 비늘 탄
        bool _volleyWarn, _volleyOwnsPatternName;
        int _volleyFireTick, _volleyNextTick;

        public SerpentBodyDef BodyDef => _body;
        public bool CoilActive => _coilActive;
        public Vector2 CoilCenter => _coilCenter;
        public float CoilMidRadius => (_coilInner + _coilOuter) / 2f;
        public int CoilUntilTick => _coilUntilTick;
        public bool VolleyWarning => _volleyWarn;

        public override void Init(BossContext c)
        {
            JObject extra = c.W.Content != null ? c.W.Content.SoloExtra(ExtraName) : null;
            JObject j = extra != null ? extra[ExtraSection] as JObject : null;
            TrailSampleDistance = Mathf.Max(BossTuning.Float(j, "trail_sample_distance", TrailSampleDistance), Mathf.Epsilon);
            PartHitSlack = BossTuning.Float(j, "part_hit_slack", PartHitSlack);
            _body = SerpentBodyDef.From(c.Def);
            LayStraight(c, -c.Body.Facing);
            _lastHead = c.Body.Pos;
            c.SetAnim(ClipMove);
        }

        // ------------------------------------------------------------ 마디 몸

        /// <summary>머리 뒤(back 방향)로 몸을 곧게 편다(전장 밖으로 나가는 부분은 벽 안에 모인다).</summary>
        void LayStraight(BossContext c, Vector2 back)
        {
            _trail.Clear();
            if (back.sqrMagnitude <= 0f) back = new Vector2(0f, -1f);
            back = back.normalized;
            float total = _body.PartCount * _body.SegmentSpacing;
            for (float d = TrailSampleDistance; d <= total + TrailSampleDistance; d += TrailSampleDistance)
                _trail.Add(c.W.ClampToArena(c.Body.Pos + back * d, _body.SegmentRadius));
            WriteParts(c);
        }

        /// <summary>몸 전체를 머리 자리에 모은다(땅속에서 솟을 때. 머리가 움직이면 다시 풀려 나온다).</summary>
        void Collapse(BossContext c)
        {
            _trail.Clear();
            WriteParts(c);
        }

        void WriteParts(BossContext c)
        {
            SerpentRules.Follow(c.Body.Pos, _trail, _body.SegmentSpacing, _body.PartCount, _parts);
            var seg = c.Boss.Segments;
            seg.Clear();
            seg.AddRange(_parts);
            // 플레이어 탄이 마디를 맞히는 판정 반지름(Spells가 읽는다). 꼬리 반지름이 다르면 큰 쪽으로 잡고 부위 배율은 PartAt이 가린다.
            c.Boss.SegmentRadius = Mathf.Max(_body.SegmentRadius, _body.TailRadius);
        }

        void UpdateTrail(BossContext c)
        {
            Vector2 head = c.Body.Pos;
            Vector2 last = _trail.Count > 0 ? _trail[0] : _lastHead;
            if (_trail.Count == 0 || (head - last).magnitude >= TrailSampleDistance) _trail.Insert(0, head);
            // 몸 길이보다 한참 뒤의 자리는 버린다.
            int keep = Mathf.CeilToInt(_body.PartCount * _body.SegmentSpacing / TrailSampleDistance) * 2 + 2;
            if (_trail.Count > keep) _trail.RemoveRange(keep, _trail.Count - keep);
            WriteParts(c);
        }

        public override bool TouchesPlayer(BossContext c, PlayerState p)
        {
            float hit = c.HitRadius(p);
            float r = _body.HeadRadius + hit;
            if ((p.Pos - c.Body.Pos).sqrMagnitude < r * r) return true;
            var seg = c.Boss.Segments;
            for (int i = 0; i < seg.Count; i++)
            {
                r = _body.RadiusOf(i) + hit;
                if ((p.Pos - seg[i]).sqrMagnitude < r * r) return true;
            }
            // 또아리의 닫힌 고리: 부위 사이의 틈도 몸으로 본다(R5로는 지나갈 수 없는 칸).
            return _coilActive && SerpentRules.TouchesRing(_coilCenter, _coilInner, _coilOuter, p.Pos, hit);
        }

        public override float ModifyIncomingDamage(BossContext c, float amount, bool isCrit, Vector2 fromPos, Vector2 hitPos)
        {
            // 탄 한 발당 마디 3번 상한(max_segment_hits): 몸통이 적 하나라 탄 한 발이 이 적을 한 번만 맞힌다(Spells의 HitSet) —
            // 상한을 넘는 일이 없다(SerpentRules.HitMul이 규칙의 정본). 부위는 맞은 자리(hitPos)로 가린다.
            int segHits = 0;
            var part = SerpentRules.PartAt(_body, c.Body.Pos, c.Boss.Segments, hitPos, PartHitSlack);
            return amount * SerpentRules.HitMul(_body, part, ref segHits);
        }

        // ------------------------------------------------------------ 이동·상시

        public override void Move(BossContext c, PlayerState target)
        {
            if (c.Body.Burrowed || _coilActive) return;
            var s = c.Boss.State;
            if (_coilApproach && s == BossFightState.Telegraph)
            {
                // 고리 예고 동안 머리가 고리 자리로 다가간다.
                Vector2 off = c.Body.Pos - _coilCenter;
                Vector2 dir = off.sqrMagnitude > 0f ? off.normalized : c.Body.Facing;
                c.Chase(_coilCenter + dir * CoilMidRadius, c.Def.MoveSpeed);
                return;
            }
            if (s != BossFightState.Waiting && s != BossFightState.R5Blocked) return;
            c.Chase(target.Pos, c.Def.MoveSpeed);
        }

        public override void Always(BossContext c, PlayerState target, bool halted)
        {
            if (_coilActive) TickCoil(c, halted);
            else if (c.Body.Burrowed) Collapse(c);
            else UpdateTrail(c);
            _moving = (c.Body.Pos - _lastHead).sqrMagnitude > 0f;
            _lastHead = c.Body.Pos;
            TickVolley(c, halted);
        }

        // ------------------------------------------------------------ 또아리

        public override void OnTelegraphStart(BossContext c, BossStep step, BossStepInstance inst)
        {
            if (c.Run == null || c.Run.Pattern.Key != PatternCoil || step.IsDash || inst.Zones.Count == 0) return;
            _coilCenter = inst.Zones[0].Pos;
            _coilOuter = step.Attack.Radius;
            _coilInner = step.Attack.InnerRadius;
            _coilApproach = true;
        }

        public override bool BuildZones(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones)
        {
            if (c.Run == null || c.Run.Pattern.Key != PatternCoil || !step.IsDash) return false;
            // 무는 띠: 머리 위치에서 그 틱의 플레이어 위치를 지나 고리 반대편까지.
            var a = step.Attack;
            Vector2 head = c.Body.Pos;
            Vector2 dir = c.DirTo(target.Pos);
            float toWall = BossRules.DistanceToWall(c.W, head, dir, 0f);
            float length = SerpentRules.ExitDistance(head, dir, _coilCenter, CoilMidRadius);
            if (length <= 0f || length > toWall) length = toWall;
            _biteLength = length;
            zones.Add(new ZoneSpec
            {
                Shape = ZoneShape.Band, Anchor = ZoneAnchor.Fixed, Pos = head, Dir = dir, Width = a.Width, Length = length,
                TelegraphTicks = SimConst.Ticks(a.TelegraphSeconds), BaseDamage = 0, DamageSource = PlayerDamageSource.Dash,
                Grazeable = false, TargetSlot = target.Slot
            });
            return true;
        }

        public override BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
        {
            string key = c.Run != null ? c.Run.Pattern.Key : null;
            if (key == PatternCoil) return step.IsDash ? Bite(c, step, inst) : Coil(c, step, inst);
            if (key == PatternBurrow && inst.Phase == BossStepPhase.Act) Rise(c, step, inst);
            else if (key == PatternSpit && inst.Phase == BossStepPhase.Act) c.SetAnim(ClipSpit);
            return BossActResult.Default;
        }

        BossActResult Coil(BossContext c, BossStep step, BossStepInstance inst)
        {
            if (inst.Phase == BossStepPhase.Act)
            {
                // 고리 예고의 판정 틱: 몸이 고리를 닫는다. 이 틱부터 active_ticks 동안 가둔다.
                if (inst.Zones.Count > 0) _coilCenter = inst.Zones[0].Pos;
                _coilOuter = step.Attack.Radius;
                _coilInner = step.Attack.InnerRadius;
                _coilApproach = false;
                _coilActive = true;
                _coilUntilTick = c.W.Tick + step.Attack.ActiveTicks;
                Vector2 off = c.Body.Pos - _coilCenter;
                _coilAngle = off.sqrMagnitude > 0f ? Mathf.Atan2(off.y, off.x) : 0f;
                PlaceOnRing(c);
            }
            if (c.W.Tick < _coilUntilTick) return BossActResult.Busy;
            // 가둠이 끝난 틱: 고리를 풀고(마디는 지금 자리에서 다시 머리를 따라간다) 다음 step이 무는 띠를 예고한다.
            EndCoil(c);
            return BossActResult.Done;
        }

        void TickCoil(BossContext c, bool halted)
        {
            if (!halted)
            {
                float mid = CoilMidRadius;
                if (mid > 0f) _coilAngle += c.Def.MoveSpeed * SimConst.TickSeconds / mid;
            }
            PlaceOnRing(c);
        }

        /// <summary>머리와 마디·꼬리를 고리 위에 고르게 놓는다(머리가 앞서 돌고 나머지가 같은 간격으로 따라온다).</summary>
        void PlaceOnRing(BossContext c)
        {
            float mid = CoilMidRadius;
            int total = _body.PartCount + 1;
            float gap = Mathf.PI * 2f / total;
            var body = c.Body;
            body.Pos = c.W.ClampToArena(_coilCenter + new Vector2(Mathf.Cos(_coilAngle), Mathf.Sin(_coilAngle)) * mid, 0f);
            body.Facing = new Vector2(-Mathf.Sin(_coilAngle), Mathf.Cos(_coilAngle));
            _parts.Clear();
            for (int i = 1; i < total; i++)
            {
                float ang = _coilAngle - gap * i;
                _parts.Add(c.W.ClampToArena(_coilCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * mid, 0f));
            }
            var seg = c.Boss.Segments;
            seg.Clear();
            seg.AddRange(_parts);
        }

        void EndCoil(BossContext c)
        {
            if (!_coilActive) return;
            _coilActive = false;
            _trail.Clear();
            _trail.AddRange(c.Boss.Segments);
        }

        BossActResult Bite(BossContext c, BossStep step, BossStepInstance inst)
        {
            if (inst.Phase != BossStepPhase.Act) return BossActResult.Default;
            // 판정 틱의 7단계에 문다(공통 규칙 4). 고리 반대편에서 멈추도록 길이를 이동 틱으로 바꾼다.
            float perTick = step.Attack.DashSpeed * SimConst.TickSeconds;
            int ticks = perTick > 0f ? Math.Max(1, Mathf.CeilToInt(_biteLength / perTick)) : 1;
            c.SetAnim(ClipBite);
            c.Sys.StartDash(c, inst, inst.DirSet ? inst.Dir : c.Body.Facing, ticks, step.Attack.Damage);
            return c.Sys.DashTick(c, step, inst) ? BossActResult.Done : BossActResult.Busy;
        }

        // ------------------------------------------------------------ 잠행

        public override void OnPatternStart(BossContext c, BossPattern pattern)
        {
            if (pattern.Key != PatternBurrow) return;
            // 첫 원의 예고는 잠행 시작 틱이다. 잠행 패턴 동안만 땅속(피해·효과·접촉 없음).
            SetBurrowed(c, true);
            c.SetAnim(ClipSink);
        }

        void Rise(BossContext c, BossStep step, BossStepInstance inst)
        {
            // 원이 솟는 틱: 머리를 그 자리로 옮긴다. 마지막(세 번째)에 땅 위로 나와 빈틈 동안 맞는다.
            if (inst.Zones.Count > 0) c.Body.Pos = c.W.ClampToArena(inst.Zones[0].Pos, c.Body.BodyRadius);
            Collapse(c);
            _lastHead = c.Body.Pos;
            if (inst.RepeatIndex < Math.Max(1, step.Repeat) - 1) return;
            SetBurrowed(c, false);
            c.SetAnim(ClipRise);
        }

        static void SetBurrowed(BossContext c, bool on)
        {
            c.Body.Burrowed = on;
            if (on) c.Body.Tags |= EnemyTags.Burrowed;
            else c.Body.Tags &= ~EnemyTags.Burrowed;
        }

        public override void OnPatternEnd(BossContext c, BossPattern pattern, bool aborted)
        {
            // 끊긴 패턴(단계 전환 등)이 땅속·고리를 남기지 않게 한다.
            if (c.Body != null && c.Body.Burrowed) SetBurrowed(c, false);
            _coilApproach = false;
            EndCoil(c);
        }

        // ------------------------------------------------------------ 비늘 탄 (2단계 상시)

        void TickVolley(BossContext c, bool halted)
        {
            var w = c.W;
            var b = c.Boss;
            var phase = c.PhaseDef;
            var pattern = c.Pattern(PatternVolley);
            if (phase == null || pattern == null || pattern.Steps.Count == 0 || !phase.Passives.Contains(PatternVolley))
            {
                EndVolleyWarn(c);
                return;
            }
            var step = pattern.Steps[0];
            if (halted)
            {
                // 동작 멈춤: 상시 행동의 시계도 한 틱씩 민다.
                _volleyNextTick++;
                if (_volleyWarn) _volleyFireTick++;
                return;
            }
            if (_volleyWarn)
            {
                c.Body.WeaponTint = true;
                if (w.Tick < _volleyFireTick) return;
                if (!c.Body.Burrowed) FireVolley(c, step);
                EndVolleyWarn(c);
                return;
            }
            // 보스가 움직이는 동안(또아리로 도는 것 포함)에만 시작한다.
            if (c.Body.Burrowed || w.Tick < _volleyNextTick || !(_moving || _coilActive)) return;
            _volleyWarn = true;
            _volleyFireTick = w.Tick + SimConst.Ticks(step.Attack != null ? step.Attack.CastSeconds : 0f);
            _volleyNextTick = w.Tick + c.IntervalTicks(step);
            c.Body.WeaponTint = true;
            // 표시 층은 "지금 패턴 == scale_volley + 무기 물듦"으로 마디 깜박임을 그린다. 다른 패턴이 없을 때만 이름을 빌린다.
            if (b.CurrentPattern == null)
            {
                b.CurrentPattern = PatternVolley;
                _volleyOwnsPatternName = true;
            }
            w.Emit(SimEventType.BossPatternStarted, entityId: b.EnemyId, b: _volleyFireTick - w.Tick, id: PatternVolley);
        }

        void EndVolleyWarn(BossContext c)
        {
            if (!_volleyWarn) return;
            _volleyWarn = false;
            var b = c.Boss;
            if (_volleyOwnsPatternName && b.CurrentPattern == PatternVolley) b.CurrentPattern = null;
            _volleyOwnsPatternName = false;
            c.Body.WeaponTint = b.State == BossFightState.Telegraph;
        }

        /// <summary>마디 count개가 바깥쪽으로 1발씩. 바깥 = 진행 방향 법선 중 전장 중심에서 먼 쪽, 또아리 중에는 고리 바깥.</summary>
        void FireVolley(BossContext c, BossStep step)
        {
            var a = step.Attack;
            if (a == null || a.Proj == null) return;
            var w = c.W;
            var seg = c.Boss.Segments;
            int count = Math.Min(Math.Max(1, step.Count), Math.Min(seg.Count, _body.SegmentCount));
            int castId = w.NewCastId();
            Vector2 center = new Vector2(w.ArenaWidth / 2f, w.ArenaHeight / 2f);
            float range = a.Proj.Range.HasValue ? a.Proj.Range.Value : -1f;
            for (int i = 0; i < count; i++)
            {
                Vector2 pos = seg[i];
                Vector2 dir;
                if (_coilActive) dir = SerpentRules.OutwardNormal(Vector2.zero, pos - _coilCenter);
                else
                {
                    Vector2 prev = i == 0 ? c.Body.Pos : seg[i - 1];
                    Vector2 next = i + 1 < seg.Count ? seg[i + 1] : pos;
                    Vector2 tangent = prev - next;
                    if (tangent.sqrMagnitude <= 0f) tangent = c.Body.Facing;
                    dir = SerpentRules.OutwardNormal(tangent, pos - center);
                }
                c.Fire(pos, dir, a.Proj.Speed, a.Proj.HitRadius, range, a.Damage, a.Grazeable, castId);
            }
        }

        // ------------------------------------------------------------ 상태

        public override void OnPhaseChanged(BossContext c, int oldPhase, int newPhase)
        {
            // 상시 행동의 첫 깜박임은 단계 전환이 끝난 뒤 한 주기 지나서.
            var pattern = c.Pattern(PatternVolley);
            if (pattern != null && pattern.Steps.Count > 0)
                _volleyNextTick = c.W.Tick + SimConst.Ticks(c.Common.PhaseTransitionSeconds) + c.IntervalTicks(pattern.Steps[0]);
        }

        public override void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState)
        {
            switch (newState)
            {
                case BossFightState.Waiting:
                case BossFightState.R5Blocked:
                    c.SetAnim(ClipMove);
                    break;
                case BossFightState.Telegraph:
                {
                    string p = c.Boss.CurrentPattern;
                    if (p == PatternSpit) c.SetAnim(ClipSpit);
                    else if (p == PatternBurrow) c.SetAnim(ClipSink);
                    else if (p == PatternCoil && !_coilActive && !_coilApproach) c.SetAnim(ClipMove);
                    break;
                }
                case BossFightState.Recovery:
                    c.SetAnim(ClipRest);
                    break;
                case BossFightState.PhaseTransition:
                    c.SetAnim(ClipPhase);
                    break;
            }
        }

        public override void OnFightEnd(BossContext c)
        {
            if (c.Body != null && c.Body.Burrowed) SetBurrowed(c, false);
            _coilApproach = false;
            _coilActive = false;
            EndVolleyWarn(c);
        }
    }
}
