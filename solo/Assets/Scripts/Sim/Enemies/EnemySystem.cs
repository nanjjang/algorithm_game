using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Enemies
{
    /// <summary>
    /// 일반 적 12종 + 졸개 + 엘리트 12종 + 허수아비의 AI, 이동, 접촉, 군중 제어, 사망. 05 요약서 3~7절.
    /// 틱 규칙은 Core/Sim/Modules.cs 머리의 공통 규칙(2 죽어 가는 적, 3 대상 없음, 4 예고 표의 틱, 6 피해 칸)을 따른다.
    /// 종별 두뇌는 EnemySystem.Species.cs, 9단계는 EnemySystem.Move.cs, 군중 제어·피해 조정·사망은 EnemySystem.Cc.cs.
    /// </summary>
    public sealed partial class EnemySystem : EnemyModule
    {
        sealed class PendingBlast
        {
            public int Tick;
            public Vector2 Pos;
            public float Radius;
            public float Damage;
        }

        /// <summary>세계 하나에 딸린 임시 상태(지연 유폭, 임시 목록). 모듈 객체가 여러 세계에 쓰여도 섞이지 않게 세계에 매단다.</summary>
        sealed class WorldScratch
        {
            public readonly List<PendingBlast> Blasts = new List<PendingBlast>();
            public readonly List<ZoneEntity> Zones = new List<ZoneEntity>();
            public readonly List<EnemyState> Tmp = new List<EnemyState>();
        }

        readonly ConditionalWeakTable<SimWorld, WorldScratch> _scratch = new ConditionalWeakTable<SimWorld, WorldScratch>();
        EnemyTuning _tuning;
        IContentDb _tuningDb;

        WorldScratch Scratch(SimWorld w) => _scratch.GetOrCreateValue(w);

        /// <summary>보충 수치(DataSolo/G06.json). 콘텐츠가 바뀌면 다시 읽는다.</summary>
        public EnemyTuning Tuning(SimWorld w)
        {
            if (_tuning == null || !ReferenceEquals(_tuningDb, w.Content))
            {
                _tuning = EnemyTuning.Load(w.Content);
                _tuningDb = w.Content;
            }
            return _tuning;
        }

        // ------------------------------------------------------------ 만들기

        public override EnemyState Spawn(SimWorld w, in EnemySpawnSpec spec)
        {
            var db = w.Content;
            if (db == null || string.IsNullOrEmpty(spec.DefId)) return null;
            EnemyDef def = db.Enemy(spec.DefId);
            if (def == null) return null;
            EliteDef elite = spec.Elite ? db.Elite(spec.DefId) : null;
            var tun = Tuning(w);
            var waves = db.Waves;
            bool dummy = def.Id == Ids.Enemy.Dummy;

            // 보스가 부른 졸개는 꼬리표·상성이 없다(05 요약서 6.13절).
            EnemyState summoner = spec.SummonerId != 0 ? w.FindEnemy(spec.SummonerId) : null;
            bool bossMinion = summoner != null && summoner.IsBoss;

            var e = new EnemyState
            {
                Id = w.NewEntityId(),
                DefId = def.Id,
                Def = def,
                Elite = elite,
                Tags = bossMinion ? EnemyTags.None : def.TagFlags,
                Weak = bossMinion ? Ids.Element.Neutral : def.Weak,
                Resist = bossMinion ? Ids.Element.Neutral : def.Resist,
                Cost = def.Cost,
                NoReward = spec.NoReward || def.Minion,
                SummonerId = spec.SummonerId,
                SpawnWave = w.Wave.Wave,
                BodyRadius = def.BodyRadius,
                MoveSpeed = def.MoveSpeed,
                AiStateStartTick = w.Tick,
                AnimStartTick = w.Tick
            };
            e.Pos = w.ClampToArena(spec.Pos, e.BodyRadius);
            e.PrevPos = e.Pos;
            if (elite != null)
            {
                e.Tags |= EnemyTags.Elite;
                if (waves != null) e.Cost = def.Cost * waves.EliteCostMul;
            }

            // 체력 = base × H(max(1, 웨이브)) × 엘리트 배율 × 변덕 배율(05 요약서 3.2절). 연습 방(웨이브 0)은 H(1).
            if (dummy)
            {
                // 허수아비는 죽지 않는다: 바닥을 두고 매 틱 가득 채운다(ThinkDummy).
                e.MaxHp = tun.DummyHp;
                e.HpFloor = 1f;
            }
            else if (spec.HpOverride > 0f)
            {
                e.MaxHp = spec.HpOverride;
            }
            else
            {
                float hp = def.BaseHp * HpMul(w);
                if (elite != null && waves != null) hp *= waves.EliteHpMul;
                hp *= w.Wave.EnemyHpMul;
                e.MaxHp = hp;
            }
            e.Hp = e.MaxHp;

            var b = BuildBrain(def, elite, dummy);
            e.Brain = b;
            b.SpawnTicks = spec.Instant ? 0
                : b.Ai.Has("emerge_seconds") ? b.Ai.Ticks("emerge_seconds")
                : db.Const != null ? SimConst.Ticks(db.Const.SpawnNoticeSeconds) : 0;
            b.ContactTicks = db.Const != null ? SimConst.Ticks(db.Const.ContactIntervalSeconds) : 0;
            for (int i = 0; i < b.Attacks.Length; i++)
                if (b.Attacks[i] != null && b.Attacks[i].Kind == "contact" && b.Attacks[i].CooldownSeconds > 0f)
                    b.ContactTicks = SimConst.Ticks(b.Attacks[i].CooldownSeconds);

            if (b.Species == EnemySpecies.Weasel)
            {
                // 둔덕으로 나온다(05 요약서 4절 U-25).
                e.Burrowed = true;
                e.Tags |= EnemyTags.Burrowed;
                e.AnimClip = "burrow";
            }
            if (b.Species == EnemySpecies.Golem) e.ArmorActive = true;

            w.Enemies.Add(e);
            if (b.SpawnTicks > 0)
            {
                e.Ai = EnemyAiState.Spawning;
                if (b.Species == EnemySpecies.Vine) e.AnimClip = "emerge";
                w.Emit(SimEventType.EnemySpawnNotice, pos: e.Pos, id: e.DefId, a: b.SpawnTicks);
            }
            else
            {
                e.Ai = EnemyAiState.Act;
                FinishSpawn(w, e, b);
            }
            return e;
        }

        float HpMul(SimWorld w) => w.Mods.Waves.HpMul(Mathf.Max(1, w.Wave.Wave));

        static EnemyBrain BuildBrain(EnemyDef def, EliteDef elite, bool dummy)
        {
            var b = new EnemyBrain
            {
                Species = dummy ? EnemySpecies.Dummy : SpeciesOf(def.Id),
                Ability = elite != null ? AbilityOf(elite.Ability) : EliteAbility.None,
                Ai = new JObjectView(def.Ai),
                Params = new JObjectView(elite != null ? elite.Params : null)
            };
            int n = def.Attacks != null ? def.Attacks.Count : 0;
            b.Attacks = new AttackDef[n];
            b.Follows = new AttackDef[n];
            for (int i = 0; i < n; i++) b.Attacks[i] = def.Attacks[i];
            if (elite != null && elite.Attacks != null)
            {
                for (int i = 0; i < elite.Attacks.Count; i++)
                {
                    var ea = elite.Attacks[i];
                    if (ea == null || ea.Attack == null) continue;
                    if (ea.Replaces.HasValue) { if (ea.Replaces.Value >= 0 && ea.Replaces.Value < n) b.Attacks[ea.Replaces.Value] = ea.Attack; }
                    else if (ea.Follows.HasValue) { if (ea.Follows.Value >= 0 && ea.Follows.Value < n) b.Follows[ea.Follows.Value] = ea.Attack; }
                    else b.Extra = ea.Attack;
                }
            }
            return b;
        }

        static EnemySpecies SpeciesOf(string defId)
        {
            switch (defId)
            {
                case Ids.Enemy.WolfBramble: return EnemySpecies.Wolf;
                case Ids.Enemy.GolemMenhir: return EnemySpecies.Golem;
                case Ids.Enemy.ShamanCap: return EnemySpecies.Shaman;
                case Ids.Enemy.BugLantern: return EnemySpecies.Bug;
                case Ids.Enemy.HogNeedle: return EnemySpecies.Hog;
                case Ids.Enemy.WeaselPot: return EnemySpecies.Weasel;
                case Ids.Enemy.BatDusk: return EnemySpecies.Bat;
                case Ids.Enemy.WispStray: return EnemySpecies.Wisp;
                case Ids.Enemy.VineThorn: return EnemySpecies.Vine;
                case Ids.Enemy.BoarTusk: return EnemySpecies.Boar;
                case Ids.Enemy.ToadDrum: return EnemySpecies.Toad;
                // 이끼 슬라임, 갓버섯 새싹(10 요약서 Q-26: 슬라임과 같은 두뇌)
                default: return EnemySpecies.Chaser;
            }
        }

        static EliteAbility AbilityOf(string ability)
        {
            switch (ability)
            {
                case "split": return EliteAbility.Split;
                case "howl": return EliteAbility.Howl;
                case "shards": return EliteAbility.Shards;
                case "wide_spread": return EliteAbility.WideSpread;
                case "big_blast": return EliteAbility.BigBlast;
                case "triple_aim": return EliteAbility.TripleAim;
                case "earth_bands": return EliteAbility.EarthBands;
                case "summon": return EliteAbility.Summon;
                case "twin_ring": return EliteAbility.TwinRing;
                case "bloom": return EliteAbility.Bloom;
                case "bounce": return EliteAbility.Bounce;
                case "big_drum": return EliteAbility.BigDrum;
                default: return EliteAbility.None;
            }
        }

        void FinishSpawn(SimWorld w, EnemyState e, EnemyBrain b)
        {
            SetAi(w, e, EnemyAiState.Act);
            b.BurrowStartTick = w.Tick;
            b.WavePoint = e.Pos;
            if (b.Species == EnemySpecies.Bat)
                b.WavePhase = w.Rng(RngStream.EnemyAi).Range(0, Mathf.Max(1, b.Ai.Ticks("wave_period_seconds")));
            if (b.Species == EnemySpecies.Wisp) b.NextBlinkTick = w.Tick + b.Ai.Ticks("blink_period_seconds");
            if (b.Species == EnemySpecies.Toad) b.NextCroakTick = w.Tick + b.Ai.Ticks("croak_period_seconds");
            SetClip(w, e, IdleClip(b));
            w.Emit(SimEventType.EnemySpawned, entityId: e.Id, id: e.DefId, a: e.IsElite ? 1 : 0, pos: e.Pos);
        }

        // ------------------------------------------------------------ 7단계

        public override void Stage7_Ai(SimWorld w)
        {
            ProcessBlasts(w);
            if (!w.CombatActive) return;
            // 이 틱에 불려 나온 개체(갈라짐, 소환)는 다음 틱부터 생각한다.
            int n = w.Enemies.Count;
            for (int i = 0; i < n; i++)
            {
                var e = w.Enemies[i];
                if (!e.Alive || e.IsBoss) continue;
                var b = e.Brain as EnemyBrain;
                if (b == null) continue;
                Think(w, e, b);
            }
        }

        void Think(SimWorld w, EnemyState e, EnemyBrain b)
        {
            b.Vel = Vector2.zero;
            b.VelIsWalk = true;
            // 대상이 없으면 그 틱은 가만히 있는다(공통 규칙 3).
            PlayerState p = w.NearestPlayer(e.Pos);
            if (b.Species == EnemySpecies.Dummy) { ThinkDummy(w, e, b, p); return; }
            if (p == null) return;

            if (e.Ai == EnemyAiState.Spawning)
            {
                if (w.Tick - e.AiStateStartTick < b.SpawnTicks) return;
                FinishSpawn(w, e, b);
            }
            if (e.DrumBuffed && w.Tick >= b.BuffUntilTick)
            {
                e.DrumBuffed = false;
                w.Emit(SimEventType.EnemyBuff, entityId: e.Id, id: EnemyTuning.DrumBuffId, a: 0);
            }
            if (b.Sub == BrainSub.WallStun && w.Tick >= e.StunUntilTick)
            {
                // 벽 충돌 기절이 끝났다(멧돼지).
                e.DamageTakenMul = 1f;
                b.Sub = BrainSub.None;
                SetAi(w, e, EnemyAiState.Act);
            }
            FlushShots(w, b);
            if (!e.CanAct(w.Tick)) return;
            if (e.Ai == EnemyAiState.Interrupted) SetAi(w, e, EnemyAiState.Act);

            switch (b.Species)
            {
                case EnemySpecies.Chaser: ThinkChaser(w, e, b, p); break;
                case EnemySpecies.Wolf: ThinkWolf(w, e, b, p); break;
                case EnemySpecies.Golem: ThinkGolem(w, e, b, p); break;
                case EnemySpecies.Shaman: ThinkShaman(w, e, b, p); break;
                case EnemySpecies.Bug: ThinkBug(w, e, b, p); break;
                case EnemySpecies.Hog: ThinkHog(w, e, b, p); break;
                case EnemySpecies.Weasel: ThinkWeasel(w, e, b, p); break;
                case EnemySpecies.Bat: ThinkBat(w, e, b, p); break;
                case EnemySpecies.Wisp: ThinkWisp(w, e, b, p); break;
                case EnemySpecies.Vine: ThinkVine(w, e, b, p); break;
                case EnemySpecies.Boar: ThinkBoar(w, e, b, p); break;
                case EnemySpecies.Toad: ThinkToad(w, e, b, p); break;
            }
        }

        // ------------------------------------------------------------ 공통 도우미

        void SetAi(SimWorld w, EnemyState e, EnemyAiState s)
        {
            if (e.Ai == s) return;
            e.Ai = s;
            e.AiStateStartTick = w.Tick;
            w.Emit(SimEventType.EnemyStateChanged, entityId: e.Id, a: (int)s);
        }

        static void SetClip(SimWorld w, EnemyState e, string clip)
        {
            if (e.AnimClip == clip) return;
            e.AnimClip = clip;
            e.AnimStartTick = w.Tick;
        }

        static string IdleClip(EnemyBrain b)
        {
            switch (b.Species)
            {
                case EnemySpecies.Weasel: return "burrow";
                case EnemySpecies.Bat: return "fly";
                case EnemySpecies.Wisp: return "hover";
                case EnemySpecies.Vine:
                case EnemySpecies.Dummy: return "idle";
                case EnemySpecies.Toad: return "hop";
                default: return "move";
            }
        }

        /// <summary>평소 이동 속도(칸/초). 북두꺼비 버프만 반영한다(감속은 9단계에서).</summary>
        static float WalkSpeed(EnemyState e, EnemyBrain b) =>
            e.DrumBuffed ? EnemyRules.BuffedSpeed(e.MoveSpeed, b.BuffSpeedRatio, b.BuffSpeedCap) : e.MoveSpeed;

        static float HitRadius(SimWorld w, PlayerState p)
        {
            if (p.Def != null) return p.Def.HitRadius;
            var bc = w.Content != null ? w.Content.BaselineCharacter : null;
            return bc != null ? bc.HitRadius : 0f;
        }

        static ZoneShape ShapeOf(string shape)
        {
            switch (shape)
            {
                case "band": return ZoneShape.Band;
                case "fan": return ZoneShape.Fan;
                case "aim_line": return ZoneShape.AimLine;
                case "ring": return ZoneShape.Ring;
                default: return ZoneShape.Circle;
            }
        }

        /// <summary>
        /// 공격 정의 → 예고 구역. bodyStrike = 판정을 몸(돌진)이나 탄(조준선)이 하는 공격의 예고:
        /// 구역 자체는 피해 0·스침 없음으로 두고(구역이 터질 때 한 번 더 때리지 않게), 피해는 몸 충돌(Dash)이나 탄이 낸다.
        /// </summary>
        public static ZoneSpec ZoneFrom(AttackDef a, EnemyState e, PlayerState p, bool bodyStrike)
        {
            var z = new ZoneSpec
            {
                Shape = ShapeOf(a.Shape),
                Radius = a.Radius,
                InnerRadius = a.InnerRadius,
                Width = a.Width,
                Length = a.Length ?? 0f,
                FanDegrees = a.FanDegrees,
                TelegraphTicks = SimConst.Ticks(a.TelegraphSeconds),
                LockLastTicks = a.Lock == "last_n_ticks" ? SimConst.Ticks(a.LockSeconds) : 0,
                BaseDamage = bodyStrike ? 0 : a.Damage,
                DamageSource = a.IsDash ? PlayerDamageSource.Dash : PlayerDamageSource.Telegraph,
                Grazeable = !bodyStrike,
                Harmless = false,
                LeavesHazard = a.LeavesHazard,
                TargetSlot = p.Slot,
                Dir = EnemyRules.Norm(p.Pos - e.Pos)
            };
            if (a.Anchor == "player_pos_at_start") z.Anchor = ZoneAnchor.TargetPlayer;
            else
            {
                z.Anchor = ZoneAnchor.Owner;
                z.AimAtPlayer = z.Shape == ZoneShape.Band || z.Shape == ZoneShape.Fan || z.Shape == ZoneShape.AimLine;
            }
            if (z.Dir == Vector2.zero) z.Dir = e.Facing;
            return z;
        }

        /// <summary>예고를 청한다(7단계). 허가는 같은 틱 8단계에 나고, 주인은 그동안 Waiting이다(공통 규칙 4).</summary>
        void Submit(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk, int index, TelegraphRequest req)
        {
            b.Ticket = w.Mods.Threats.RequestTelegraph(w, req);
            b.HasTicket = true;
            b.Granted = false;
            b.RequestTick = w.Tick;
            b.CastId = req.CastId;
            b.Pending = atk;
            b.PendingIndex = index;
            e.CurrentAttack = index;
            SetAi(w, e, EnemyAiState.Waiting);
        }

        void RequestAttack(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk, int index, PlayerState p, bool bodyStrike)
        {
            var req = new TelegraphRequest { OwnerEnemyId = e.Id, CastId = w.NewCastId() };
            req.Zones.Add(ZoneFrom(atk, e, p, bodyStrike));
            b.SelfTarget = false;
            b.LockedDir = Vector2.zero;
            Submit(w, e, b, atk, index, req);
        }

        /// <summary>표가 허가됐는지 본다. 허가됐으면 WindUp으로(시계는 허가된 틱 s부터). 7단계와 9단계 머리에서 부른다.</summary>
        bool PollGrant(SimWorld w, EnemyState e, EnemyBrain b)
        {
            if (!b.HasTicket || b.Granted) return b.Granted;
            int s, d;
            if (!w.Mods.Threats.TryGetTiming(w, b.Ticket, out s, out d)) return false;
            b.Granted = true;
            b.StartTick = s;
            b.DetTick = d;
            if (e.Ai != EnemyAiState.WindUp)
            {
                e.Ai = EnemyAiState.WindUp;
                w.Emit(SimEventType.EnemyStateChanged, entityId: e.Id, a: (int)EnemyAiState.WindUp);
            }
            e.AiStateStartTick = s;
            e.WeaponTint = true;
            OnGranted(w, e, b);
            return true;
        }

        void OnGranted(SimWorld w, EnemyState e, EnemyBrain b)
        {
            ZoneEntity zone = FirstZone(w, b.Ticket);
            PlayerState p = w.NearestPlayer(e.Pos);
            bool fixedDir = b.LockedDir != Vector2.zero;
            if (zone != null)
            {
                b.TargetPos = zone.Pos;
                if (!fixedDir) b.LockedDir = zone.Dir;
            }
            else
            {
                bool atPlayer = b.Pending != null && b.Pending.Anchor == "player_pos_at_start" && !b.SelfTarget && p != null;
                b.TargetPos = atPlayer ? p.Pos : e.Pos;
                if (!fixedDir) b.LockedDir = p != null ? EnemyRules.Norm(p.Pos - e.Pos) : e.Facing;
            }
            if (b.LockedDir == Vector2.zero) b.LockedDir = e.Facing;

            switch (b.Species)
            {
                case EnemySpecies.Weasel:
                    // 솟구칠 자리로 옮긴다(05 요약서 6.7절 U-46). 아직 땅속이다.
                    e.Pos = w.ClampToArena(b.TargetPos, e.BodyRadius);
                    e.PrevPos = e.Pos;
                    SetClip(w, e, "rise");
                    break;
                case EnemySpecies.Vine:
                    // 씨앗 간격은 허가 틱 기준(U-59).
                    b.NextAttackTick = b.StartTick + SimConst.Ticks(b.Pending.CooldownSeconds);
                    SetClip(w, e, "cast");
                    break;
                case EnemySpecies.Golem:
                    e.Facing = b.LockedDir;
                    SetClip(w, e, b.PendingIndex == 1 ? "sweep_warn" : "slam_warn");
                    break;
                case EnemySpecies.Wolf:
                case EnemySpecies.Boar:
                    e.Facing = b.LockedDir;
                    SetClip(w, e, "warn");
                    break;
                case EnemySpecies.Bug: SetClip(w, e, "ignite"); break;
                case EnemySpecies.Hog: SetClip(w, e, "aim"); break;
                case EnemySpecies.Toad: e.Facing = b.LockedDir; SetClip(w, e, "tongue_warn"); break;
            }
        }

        ZoneEntity FirstZone(SimWorld w, int ticket)
        {
            var sc = Scratch(w);
            sc.Zones.Clear();
            w.Mods.Threats.GetZones(w, ticket, sc.Zones);
            ZoneEntity z = sc.Zones.Count > 0 ? sc.Zones[0] : null;
            sc.Zones.Clear();
            return z;
        }

        /// <summary>표를 버리고 평소 행동으로 돌아간다(조건 소멸, 표가 밖에서 취소됨).</summary>
        void DropTicket(SimWorld w, EnemyState e, EnemyBrain b, bool cancel)
        {
            if (b.HasTicket && cancel) w.Mods.Threats.CancelTicket(w, b.Ticket);
            b.HasTicket = false;
            b.Granted = false;
            b.Pending = null;
            b.PendingIndex = -1;
            b.ForcedDash = false;
            e.CurrentAttack = -1;
            e.WeaponTint = false;
            SetAi(w, e, EnemyAiState.Act);
        }

        /// <summary>
        /// Waiting·WindUp 공통 처리. 돌려주는 값이 true면 이번 틱이 자체 타격을 시작할 틱이다(w.Tick ≥ 판정 틱. 공통 규칙 4 —
        /// Status == Detonated를 기다리지 않는다).
        /// </summary>
        bool StrikeDue(SimWorld w, EnemyState e, EnemyBrain b)
        {
            if (!b.HasTicket) { DropTicket(w, e, b, false); return false; }
            if (!b.Granted && !PollGrant(w, e, b))
            {
                TicketStatus st = w.Mods.Threats.Status(w, b.Ticket);
                if (st != TicketStatus.Waiting && st != TicketStatus.Active) DropTicket(w, e, b, false);
                return false;
            }
            if (w.Tick >= b.DetTick) return true;
            if (w.Mods.Threats.Status(w, b.Ticket) == TicketStatus.Cancelled) DropTicket(w, e, b, false);
            return false;
        }

        /// <summary>타격을 마쳤다: 표를 놓는다(표 자체는 Threats에 Detonated로 남는다).</summary>
        static void FinishStrike(EnemyState e, EnemyBrain b)
        {
            b.HasTicket = false;
            b.Granted = false;
            b.ForcedDash = false;
            e.WeaponTint = false;
        }

        /// <summary>빈틈에 들어간다. 북두꺼비 버프면 ceil(t × 0.7).</summary>
        void BeginRecovery(SimWorld w, EnemyState e, EnemyBrain b, int ticks)
        {
            if (e.DrumBuffed) ticks = EnemyRules.BuffedRecovery(ticks, b.BuffRecoveryRatio);
            if (ticks <= 0) { SetAi(w, e, EnemyAiState.Act); return; }
            b.RecoveryEndTick = w.Tick + ticks;
            SetAi(w, e, EnemyAiState.Recovery);
        }

        bool RecoveryDone(SimWorld w, EnemyState e, EnemyBrain b)
        {
            if (w.Tick < b.RecoveryEndTick) return false;
            SetAi(w, e, EnemyAiState.Act);
            return true;
        }

        void StartDash(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk)
        {
            e.DashActive = true;
            e.DashCastId = b.CastId;
            e.DashBaseDamage = atk.Damage;
            e.DashDir = b.LockedDir != Vector2.zero ? b.LockedDir : e.Facing;
            e.Facing = e.DashDir;
            b.DashAttack = atk;
            b.DashToWall = atk.ActiveTicks <= 0;
            b.DashEndTick = w.Tick + atk.ActiveTicks;
            b.DashHitMask = 0;
            b.PathHitIds.Clear();
            FinishStrike(e, b);
            SetAi(w, e, EnemyAiState.Strike);
        }

        // ------------------------------------------------------------ 탄

        /// <summary>
        /// 한 시전의 탄을 쏜다. 탄 수 = floor(n × w.Wave.EnemyShotCountMul). 부채·고리는 이웃 탄 사이 각을 그대로 두고 수만 늘린다:
        /// 부채는 바깥으로 넓어지고, 고리는 한 바퀴를 넘는 탄이 같은 방향으로 겹치므로 ceil(1.0칸 ÷ (속도 × 배율))틱 뒤에 이어 쏜다
        /// (한 방향으로 이어 쏘는 탄 사이 1.0칸 이상. 00 문서 D-64).
        /// </summary>
        void FireShots(SimWorld w, EnemyState e, EnemyBrain b, Vector2 origin, Vector2 firstDir, int count, float spreadDegrees, bool ring,
            float speed, float radius, float range, int damage, bool grazeable, int castId, float extraDegrees)
        {
            if (firstDir == Vector2.zero) firstDir = e.Facing;
            int n0 = Mathf.Max(1, count);
            int n = EnemyRules.BarrageCount(n0, w.Wave.EnemyShotCountMul);
            int gap = n > n0 ? EnemyRules.BarrageGapTicks(Tuning(w).BarrageMinGap, speed, w.Wave.EnemyShotSpeedMul) : 0;
            bool fan = !ring && spreadDegrees > 0f;
            for (int k = 0; k < n; k++)
            {
                float ang;
                int wave;
                if (ring) { ang = (k % n0) * spreadDegrees; wave = k / n0; }
                else if (fan) { ang = (k - (n - 1) / 2f) * spreadDegrees; wave = 0; }
                else { ang = 0f; wave = k; }
                var spec = new EnemyShotSpec
                {
                    OwnerEnemyId = e.Id,
                    CastId = castId,
                    Pos = origin,
                    Dir = EnemyRules.Rotate(firstDir, ang + extraDegrees),
                    Speed = speed,
                    Radius = radius,
                    Range = range,
                    BaseDamage = damage,
                    Grazeable = grazeable
                };
                if (wave == 0) w.Mods.Threats.SpawnShot(w, spec);
                else b.Shots.Add(new PendingShot { Tick = w.Tick + wave * gap, Spec = spec });
            }
        }

        void FireAttack(SimWorld w, EnemyState e, EnemyBrain b, AttackDef atk, Vector2 origin, Vector2 firstDir, int castId, float extraDegrees)
        {
            var pr = atk.Proj;
            if (pr == null) return;
            FireShots(w, e, b, origin, firstDir, pr.Count, pr.SpreadDegrees, atk.Kind == "ring_shot", pr.Speed, pr.HitRadius,
                pr.Range ?? -1f, atk.Damage, atk.Grazeable, castId, extraDegrees);
        }

        static void FlushShots(SimWorld w, EnemyBrain b)
        {
            if (b.Shots.Count == 0) return;
            for (int i = 0; i < b.Shots.Count;)
            {
                if (w.Tick >= b.Shots[i].Tick)
                {
                    w.Mods.Threats.SpawnShot(w, b.Shots[i].Spec);
                    b.Shots.RemoveAt(i);
                }
                else i++;
            }
        }

        // ------------------------------------------------------------ 지연 유폭(꽈리)

        void ProcessBlasts(SimWorld w)
        {
            var sc = Scratch(w);
            if (sc.Blasts.Count == 0) return;
            for (int i = 0; i < sc.Blasts.Count;)
            {
                var bl = sc.Blasts[i];
                if (w.Tick < bl.Tick) { i++; continue; }
                sc.Blasts.RemoveAt(i);
                w.Emit(SimEventType.Explosion, pos: bl.Pos, id: Ids.Element.Neutral, f: bl.Radius);
                // 적에게만(플레이어 피해 0). 무속성, 철갑 무시.
                int n = w.Enemies.Count;
                for (int k = 0; k < n; k++)
                {
                    var o = w.Enemies[k];
                    if (!o.Targetable || o.Ai == EnemyAiState.Spawning) continue;
                    if (!EnemyRules.Overlap(bl.Pos, bl.Radius, o.Pos, o.BodyRadius)) continue;
                    DealEnvironment(w, o, bl.Damage);
                }
            }
        }

        static void DealEnvironment(SimWorld w, EnemyState target, float amount)
        {
            w.Mods.Spells.DealDirect(w, new DirectDamage
            {
                AttackerSlot = -1,
                TargetEnemyId = target.Id,
                Amount = amount,
                Element = Ids.Element.Neutral,
                Origin = DamageOrigin.Environment,
                IgnoresArmor = true
            });
        }

        /// <summary>적 스스로 죽는다(꽈리 점화 폭발, 멧돼지 돌진에 걸린 꽈리). 사망 처리는 10단계가 한다.</summary>
        static void KillSelf(EnemyState e)
        {
            if (!e.Alive) return;
            e.Hp = 0f;
            e.PendingDeath = true;
            e.KillingHit = new HitInfo
            {
                AttackerSlot = -1, EnemyId = e.Id, Origin = DamageOrigin.Environment, Element = Ids.Element.Neutral, Killed = true, Pos = e.Pos
            };
        }

        /// <summary>그 적이 불러낸 살아 있는 개체 수(SummonerId로 센다).</summary>
        public int CountSummonedBy(SimWorld w, int summonerId)
        {
            int n = 0;
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var o = w.Enemies[i];
                if (o.Alive && o.SummonerId == summonerId) n++;
            }
            return n;
        }
    }
}
