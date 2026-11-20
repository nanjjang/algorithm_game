using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Player
{
    /// <summary>
    /// 플레이어 모듈 구현: 입력 적용, 이동·시야·자세, 체력·피격·무적, 과열, 스침, 콤보, 결정.
    /// 02 요약서 2~7절, 10 요약서 1·2절, 00 문서 5.1·5.4절.
    /// 파일 나눔: 이 파일(판 시작·공용), .Controls(입력·시야·이동), .Heat(1·6·11단계·잠금),
    /// .Hit(피격·무적·체력), .Graze(9단계 스침), .Combo(콤보·결정).
    /// </summary>
    public sealed partial class PlayerSystem : PlayerModule
    {
        /// <summary>
        /// PlayerState.PlayerScratch가 소유하는 플레이어 모듈 전용 상태.
        /// CanAfford·RingState처럼 SimWorld를 받지 않는 계약 메서드가 연습 옵션·수치를 읽을 길도 이것 하나다.
        /// </summary>
        sealed class Extra
        {
            public SimWorld World;
            public readonly List<InputEvent> Events = new List<InputEvent>();
            public readonly List<GrazeCandidate> Graze = new List<GrazeCandidate>();
            /// <summary>스침을 인정한 시전이 마지막으로 살아 있던 틱(지울 때를 정한다).</summary>
            public readonly Dictionary<int, int> CastLastLiveTick = new Dictionary<int, int>();
            public readonly List<int> Scratch = new List<int>();
            public HeatRingState LastRing = HeatRingState.Cold;

            public void Reset()
            {
                Events.Clear();
                Graze.Clear();
                CastLastLiveTick.Clear();
                Scratch.Clear();
                LastRing = HeatRingState.Cold;
            }
        }

        PlayerTuning _tuning;
        IContentDb _tuningDb;

        PlayerTuning Tuning(IContentDb db)
        {
            if (_tuning == null || !ReferenceEquals(_tuningDb, db))
            {
                _tuning = PlayerTuning.From(db);
                _tuningDb = db;
            }
            return _tuning;
        }

        PlayerTuning Tuning(SimWorld w) => Tuning(w.Content);

        static Extra Bind(SimWorld w, PlayerState p)
        {
            var x = p.PlayerScratch as Extra;
            if (x == null) p.PlayerScratch = x = new Extra();
            x.World = w;
            return x;
        }

        static Extra Find(PlayerState p)
        {
            return p?.PlayerScratch as Extra;
        }

        static bool HeatFrozen(SimWorld w) => w.Run != null && w.Run.PracticeHeatFreeze;

        // ------------------------------------------------------------ 판 시작

        public override void SetupPlayer(SimWorld w, PlayerState p, RunConfig cfg)
        {
            Extra x = Bind(w, p);
            x.Reset();
            IContentDb db = w.Content;

            p.Active = true;
            p.CharId = cfg.CharId;
            CharacterDef def = db != null ? (db.Character(cfg.CharId) ?? db.BaselineCharacter) : null;
            p.Def = def;

            // 체력: 칸 × 100점(02 요약서 5.1절). 판 시작은 가득.
            p.MaxHp = def != null ? def.MaxHpCells * SimConst.HpPerCell : 0;
            p.Hp = p.MaxHp;
            p.InvulnUntilTick = 0;
            p.ArtGrazeWindowUntilTick = 0;
            p.ArtGrazeWindowUsed = false;
            p.HitThisTick = false;
            p.Downed = false;
            p.HasPendingHit = false;
            p.PendingHit = default;

            // 위치(arena.json spawn_points, 슬롯 순)와 시야 S(02 요약서 4.2절 [임시]).
            p.Pos = SpawnPoint(w, p.Slot);
            p.PrevPos = p.Pos;
            p.Facing = Dir8.S;
            p.LockedFacing = Dir8.S;
            p.LastPressedCardinal = Dir8.S;
            p.Stance = StanceState.None;
            p.Moving = false;
            p.MoveDir = Dir8.S;
            p.DiagPendingActive = false;
            p.EnvSpeedMul = 1f;
            for (int i = 0; i < p.Keys.Down.Length; i++) { p.Keys.Down[i] = false; p.Keys.LastPressU[i] = 0; }

            // 과열·스침·콤보·결정: 열 0, 잠금 없음, 콤보 0, 결정 0, 빚 0.
            p.HeatHu = 0;
            p.Locked = false;
            p.UnlockTick = 0;
            p.LockStartTick = 0;
            p.FireReadyTick = w.Tick;
            p.LastFireTick = int.MinValue / 2;
            p.LastFireIntervalTicks = 0;
            p.HeatExemptArmed = false;
            p.JPressedThisTick = false;
            p.NextShotId = 1;
            p.GrazeBuffUntilTick = 0;
            p.GrazedCasts.Clear();
            p.Combo = 0;
            p.ComboIdleTicks = 0;
            p.ShotComboGain.Clear();
            p.Crystals = 0;
            p.Debt = 0;
            p.Kills = 0;

            // 지팡이(판 중 단계는 안 변한다).
            p.Wand.WandId = cfg.WandId;
            p.Wand.Stage = cfg.WandStage;
            p.Wand.TempStageDrop = 0;
            p.Wand.GlassDroppedThisWave = false;
            p.Wand.LinkSlotCount = cfg.LinkSlotCount > 0 ? cfg.LinkSlotCount : (def != null ? def.BaseLinkSlots : 0);
            p.Wand.Mutations.Clear();
            if (cfg.Mutations != null) p.Wand.Mutations.AddRange(cfg.Mutations);

            // 조립: 출발 조립의 블록 id를 판 중 조립으로(서고 블록 = WildUid 0).
            p.Loadout = BuildLoadout(cfg.Loadout, p.Wand.LinkSlotCount);

            w.Mods.Traits.RecomputeDerived(w, p);
            RecompileSpell(w, p);
            w.Mods.Arts.SetupPlayer(w, p);
            x.LastRing = RingState(w, p);
        }

        /// <summary>협동 동료 자리(DataSolo/Coop.json, 11-online.md).</summary>
        public const string CoopExtraName = "Coop";

        static Vector2 SpawnPoint(SimWorld w, int slot)
        {
            if (slot > 0 && w.Content != null)
            {
                var pts = w.Content.SoloExtra(CoopExtraName)?["party_spawn_points"] as Newtonsoft.Json.Linq.JArray;
                if (pts != null && slot - 1 < pts.Count)
                    return new Vector2((float)pts[slot - 1]["x"], (float)pts[slot - 1]["y"]);
            }
            ArenaDef field = w.Content != null ? w.Content.Field : null;
            if (field != null && field.SpawnPoints != null && field.SpawnPoints.Count > 0)
            {
                PointDef pt = field.SpawnPoints[slot < field.SpawnPoints.Count ? slot : 0];
                return new Vector2(pt.X, pt.Y);
            }
            return new Vector2(w.ArenaWidth / 2f, w.ArenaHeight / 2f);
        }

        static RunLoadout BuildLoadout(LoadoutSpec spec, int linkSlotCount)
        {
            var lo = new RunLoadout();
            if (spec == null)
            {
                lo.Links = new SlotItem[linkSlotCount > 0 ? linkSlotCount : 0];
                return lo;
            }
            lo.Core = new SlotItem { BlockId = string.IsNullOrEmpty(spec.Core) ? null : spec.Core, Stars = StarOf(spec, 0) };
            int n = spec.Links != null ? spec.Links.Count : 0;
            lo.Links = new SlotItem[n];
            for (int i = 0; i < n; i++)
            {
                string id = spec.Links[i];
                lo.Links[i] = new SlotItem { BlockId = string.IsNullOrEmpty(id) ? null : id, Stars = string.IsNullOrEmpty(id) ? 0 : StarOf(spec, i + 1) };
            }
            return lo;
        }

        /// <summary>출발 조립의 칸 별(야영에서 연 연습이 지금 판의 ★을 가져온다). 없으면 0.</summary>
        static int StarOf(LoadoutSpec spec, int slot) => spec.Stars != null && slot < spec.Stars.Count ? spec.Stars[slot] : 0;

        /// <summary>조립이 바뀌었다: 다시 컴파일하고 LoadoutChanged를 낸다. 컴파일 문맥은 Traits.CompileContext 하나로만 만든다.</summary>
        public override void RecompileSpell(SimWorld w, PlayerState p)
        {
            Bind(w, p);
            RunLoadout lo = p.Loadout ?? (p.Loadout = new RunLoadout());
            SpellCompileContext ctx = w.Mods.Traits.CompileContext(p.Derived, p.Wand.LinkSlotCount);
            ctx.SlotStars = lo.SlotStars();   // 판 한정 블록 성장(★, 전리품 겹치기)
            string core = lo.Core.IsEmpty ? null : lo.Core.BlockId;
            SlotItem[] links = lo.Links ?? (lo.Links = new SlotItem[0]);
            var ids = new string[links.Length];
            for (int i = 0; i < links.Length; i++) ids[i] = links[i].IsEmpty ? null : links[i].BlockId;
            p.Spell = w.Mods.Spells.Compile(w.Content, core, ids, ctx);
            w.Emit(SimEventType.LoadoutChanged, p.Slot);
            // 패시브 밑줄의 칸(맨 끝 연결 칸, 02 요약서 8.3절). 표시는 이 이벤트나 상태(Derived.Underline, Links.Length)로 안다.
            if (p.Derived.Underline) w.Emit(SimEventType.UnderlineSlotChanged, p.Slot, a: links.Length > 0 ? links.Length : -1);
        }

        // ------------------------------------------------------------ 11단계

        public override void Stage11_TickEnd(SimWorld w)
        {
            PlayerTuning tune = Tuning(w);
            int t = w.Tick;
            for (int s = 0; s < w.Players.Length; s++)
            {
                PlayerState p = w.Players[s];
                if (!p.Active) continue;
                Extra x = Bind(w, p);

                Cool(w, p);
                TickComboIdle(w, p, tune);

                // 무적·가속이 이 틱으로 끝난다(다음 틱부터 t >= until).
                if (p.InvulnUntilTick == t + 1) w.Emit(SimEventType.InvulnEnded, p.Slot);
                if (p.GrazeBuffUntilTick == t + 1) w.Emit(SimEventType.GrazeBuffEnded, p.Slot);

                PruneShotComboGain(w, p, x);
                PruneGrazedCasts(w, p, x, tune);

                HeatRingState ring = Ring(p, tune);
                if (ring != x.LastRing)
                {
                    x.LastRing = ring;
                    w.Emit(SimEventType.HeatRingChanged, p.Slot, a: (int)ring);
                }
            }
        }
    }
}
