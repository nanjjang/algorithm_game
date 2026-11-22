using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Arts;

namespace Wizard.Solo.Tests.Arts
{
    /// <summary>결정 지갑만 진짜 규칙(10 요약서 2.2절 CanAfford / Spend / SpendAll)으로 흉내 내는 가짜 Player.</summary>
    public sealed class FakePlayer : PlayerModule
    {
        public struct Move { public Vector2 Delta; public bool StopAtEnemies, StopAtObstacles; }
        public struct Invuln { public int Ticks; public bool GrazeWindow; }
        public struct Graze { public int CastId; public GrazeKind Kind; }
        public readonly List<Move> Moves = new List<Move>();
        public readonly List<Invuln> Invulns = new List<Invuln>();
        public readonly List<Graze> Grazes = new List<Graze>();
        public int VentCalls;

        public override bool CanAfford(SimWorld w, PlayerState p, int cost, bool allowCredit)
        {
            if (p.Crystals >= cost) return true;
            return allowCredit && p.Derived.HasCredit && p.Debt + (cost - p.Crystals) <= p.Derived.MaxDebt;
        }

        public override int SpendCrystals(SimWorld w, PlayerState p, int cost, bool allowCredit)
        {
            int use = Math.Min(p.Crystals, cost);
            p.Crystals -= use;
            int shortBy = cost - use;
            if (shortBy > 0) p.Debt += shortBy;
            return shortBy;
        }

        public override int SpendAllCrystals(SimWorld w, PlayerState p)
        {
            int n = p.Crystals;
            p.Crystals = 0;
            return n;
        }

        public override Vector2 MoveByArt(SimWorld w, PlayerState p, Vector2 delta, bool stopAtEnemies, bool stopAtObstacles)
        {
            Moves.Add(new Move { Delta = delta, StopAtEnemies = stopAtEnemies, StopAtObstacles = stopAtObstacles });
            p.Pos = w.ClampToArena(p.Pos + delta, 0f);
            return p.Pos;
        }

        public override void GrantInvuln(SimWorld w, PlayerState p, int ticks, bool grazeWindow)
        {
            Invulns.Add(new Invuln { Ticks = ticks, GrazeWindow = grazeWindow });
        }

        public override void AwardGraze(SimWorld w, PlayerState p, int castId, GrazeKind kind, Vector2 threatPos)
        {
            Grazes.Add(new Graze { CastId = castId, Kind = kind });
        }

        public override float ConsumeHeatForVent(SimWorld w, PlayerState p)
        {
            VentCalls++;
            float heat = p.Locked ? 100f : p.HeatHu / (float)SimConst.HuPerHeat;
            p.HeatHu = 0;
            p.Locked = false;
            return heat;
        }
    }

    public sealed class FakeSpells : SpellModule
    {
        /// <summary>표준 T(마력탄 하나 낀 +0 지팡이 = 10. 04 요약서 A-12).</summary>
        public float T = 10f;
        public int SpawnedPerExecute = 1;
        public readonly List<ExecuteRequest> Executes = new List<ExecuteRequest>();
        public readonly List<DirectDamage> Directs = new List<DirectDamage>();
        public readonly List<int> ExecuteTicks = new List<int>();
        public readonly List<int> DirectTicks = new List<int>();

        public override float StandardT(SimWorld w, PlayerState p) => T;

        public override ExecuteResult Execute(SimWorld w, in ExecuteRequest req)
        {
            Executes.Add(req);
            ExecuteTicks.Add(w.Tick);
            int n = SpawnedPerExecute;
            if (req.ProjectileBudget > 0 && n > req.ProjectileBudget) n = req.ProjectileBudget;
            return new ExecuteResult { Spawned = n, Requested = SpawnedPerExecute };
        }

        public override DamageResult DealDirect(SimWorld w, in DirectDamage dmg)
        {
            Directs.Add(dmg);
            DirectTicks.Add(w.Tick);
            var e = w.FindEnemy(dmg.TargetEnemyId);
            if (e == null) return default;
            e.Hp -= dmg.Amount;
            if (e.Hp <= 0f) e.PendingDeath = true;
            return new DamageResult { Applied = dmg.Amount, Killed = e.PendingDeath };
        }

        public float TotalTo(int enemyId)
        {
            float sum = 0f;
            for (int i = 0; i < Directs.Count; i++) if (Directs[i].TargetEnemyId == enemyId) sum += Directs[i].Amount;
            return sum;
        }

        public int CountTo(int enemyId)
        {
            int n = 0;
            for (int i = 0; i < Directs.Count; i++) if (Directs[i].TargetEnemyId == enemyId) n++;
            return n;
        }
    }

    public sealed class FakeEnemies : EnemyModule
    {
        public struct Cc { public int EnemyId; public CcKind Kind; public float Amount; public CcSource Source; public Vector2 Dir; }
        public struct Slow { public int EnemyId; public SlowSlot Slot; public float Strength; public int Ticks; public int Tick; }
        public readonly List<Cc> Ccs = new List<Cc>();
        public readonly List<Slow> Slows = new List<Slow>();

        public override float ApplyCc(SimWorld w, EnemyState e, CcKind kind, float amount, CcSource source, Vector2 dir)
        {
            Ccs.Add(new Cc { EnemyId = e.Id, Kind = kind, Amount = amount, Source = source, Dir = dir });
            return amount;
        }

        public override void ApplySlow(SimWorld w, EnemyState e, SlowSlot slot, float strength, int ticks, CcSource source)
        {
            Slows.Add(new Slow { EnemyId = e.Id, Slot = slot, Strength = strength, Ticks = ticks, Tick = w.Tick });
        }
    }

    /// <summary>그림자 계약만: 제자 5는 가진 결정 1개당 +3%(02 요약서 8.3절).</summary>
    public sealed class FakeTraits : TraitModule
    {
        public readonly List<int> CrystalArgs = new List<int>();
        public override float CharacterMul(SimWorld w, PlayerState p, int crystalsHeld)
        {
            CrystalArgs.Add(crystalsHeld);
            return p.CharId == Ids.Char.C5 ? (100 + 3 * crystalsHeld) / 100f : 1f;
        }
    }

    public sealed class FakeThreats : ThreatModule
    {
        public struct Clear { public Vector2 Center; public float Radius; public int Tick; }
        public readonly List<Clear> Clears = new List<Clear>();
        public override int ClearShots(SimWorld w, Vector2 center, float radius)
        {
            Clears.Add(new Clear { Center = center, Radius = radius, Tick = w.Tick });
            return 0;
        }
    }

    public sealed class FakeRun : RunModule
    {
        public readonly List<string> ArtsFired = new List<string>();
        public override void OnArtFired(SimWorld w, PlayerState p, string artId) { ArtsFired.Add(artId); }
    }

    public sealed class FakeBosses : BossModule
    {
        public struct Halt { public int EnemyId; public int Ticks; }
        public readonly List<Halt> Halts = new List<Halt>();
        public override void ActionHalt(SimWorld w, EnemyState e, int ticks) { Halts.Add(new Halt { EnemyId = e.Id, Ticks = ticks }); }
    }

    /// <summary>
    /// 비기 테스트 세계: ArtSystem만 진짜이고 Player·Spells·Enemies·Traits·Threats·Bosses는 기본 클래스를 상속한 가짜다.
    /// 04 요약서 14절 하네스: 시작 시 자세 중, 대기 없음, 커서 루트.
    /// </summary>
    public sealed class ArtKit
    {
        public const string BloomInput = "F, B+2611, B+2611, F+2611, K+2611";
        /// <summary>입력열의 시작 시각(u). 틱 경계에 맞춘 아무 값.</summary>
        public const long T0 = 256L * 40;

        public readonly SimWorld W;
        public readonly PlayerState P;
        public readonly ArtSystem Arts = new ArtSystem();
        public readonly FakePlayer Player = new FakePlayer();
        public readonly FakeSpells Spells = new FakeSpells();
        public readonly FakeEnemies Enemies = new FakeEnemies();
        public readonly FakeTraits Traits = new FakeTraits();
        public readonly FakeThreats Threats = new FakeThreats();
        public readonly FakeBosses Bosses = new FakeBosses();
        public readonly FakeRun Run = new FakeRun();
        public long Now = T0;

        /// <param name="commons">해금한 공용 비기(시작 3종은 늘 있다). null = 전부.</param>
        public ArtKit(string charId, int crystals = 0, int debt = 0, string[] commons = null, bool inStance = true, ulong seed = 1)
        {
            var mods = SimModuleSet.Stubs();
            mods.Arts = Arts;
            mods.Player = Player;
            mods.Spells = Spells;
            mods.Enemies = Enemies;
            mods.Traits = Traits;
            mods.Threats = Threats;
            mods.Bosses = Bosses;
            mods.Run = Run;
            W = new SimWorld(TestContent.Db, mods, seed);
            W.Run.Phase = RunPhase.WaveCombat;
            P = W.P0;
            P.Active = true;
            P.CharId = charId;
            P.Def = TestContent.Db.Character(charId);
            P.Derived = new PlayerDerived
            {
                HasCredit = charId == Ids.Char.C5,
                MaxDebt = TestContent.Db.Tries.Parser.MaxDebt,
                DepthDecay = 1f, EnhanceMul = 1f, MutationDamageMul = 1f
            };
            P.Crystals = crystals;
            P.Debt = debt;
            P.Pos = new Vector2(12f, 7f);
            P.Facing = Dir8.E;
            P.LockedFacing = Dir8.E;
            P.Stance = inStance ? StanceState.Stance : StanceState.None;
            P.Spell = new CompiledSpell { IsValid = true, Core = TestContent.Db.Block(Ids.Block.MagicBolt) };
            if (commons == null)
            {
                var all = TestContent.Db.TrieArts(charId);
                for (int i = 0; i < all.Count; i++) P.UnlockedArts.Add(all[i]);
            }
            else
            {
                for (int i = 0; i < commons.Length; i++) P.UnlockedArts.Add(commons[i]);
            }
            Arts.SetupPlayer(W, P);
        }

        public CommandParser Parser => ArtSystem.StateOf(P).Parser;
        public CommandTrie Trie => ArtSystem.StateOf(P).Trie;

        public ArtDecision Last
        {
            get
            {
                ArtDecision d;
                if (!ArtSystem.TryGetLastDecision(P, out d)) throw new InvalidOperationException("no decision yet");
                return d;
            }
        }

        public bool HasDecision
        {
            get { ArtDecision d; return ArtSystem.TryGetLastDecision(P, out d); }
        }

        /// <summary>상대 방향 → 지금 시야 기준의 물리 방향(왼쪽 = 반시계 90도).</summary>
        public Dir8 Physical(CmdDir d)
        {
            switch (d)
            {
                case CmdDir.Forward: return P.Facing;
                case CmdDir.Back: return Dir8Util.Flip(P.Facing);
                case CmdDir.Left: return Dir8Util.Rotate(P.Facing, 2);
                default: return Dir8Util.Rotate(P.Facing, -2);
            }
        }

        public void Dir(CmdDir d, long gapU = 0)
        {
            Now += gapU;
            Arts.OnDirectionPress(W, P, d, Physical(d), Now);
        }

        public void K(long gapU = 0)
        {
            Now += gapU;
            Arts.OnSkillPress(W, P, Now);
        }

        /// <summary>"F, B+2304, K+2304" 꼴의 입력열을 넣는다(+n = 직전 입력에서 n u 뒤).</summary>
        public void Seq(string inputs)
        {
            var tokens = inputs.Split(',');
            for (int i = 0; i < tokens.Length; i++)
            {
                string t = tokens[i].Trim();
                if (t.Length == 0) continue;
                long gap = 0;
                int plus = t.IndexOf('+');
                if (plus >= 0) { gap = long.Parse(t.Substring(plus + 1)); t = t.Substring(0, plus); }
                switch (t)
                {
                    case "F": Dir(CmdDir.Forward, gap); break;
                    case "B": Dir(CmdDir.Back, gap); break;
                    case "L": Dir(CmdDir.Left, gap); break;
                    case "R": Dir(CmdDir.Right, gap); break;
                    case "K": K(gap); break;
                    default: throw new ArgumentException("unknown token " + t);
                }
            }
        }

        public void EnterStance()
        {
            P.Stance = StanceState.Stance;
            Arts.OnCommandReset(W, P, CommandResetReason.StanceEnter);
        }

        public void ExitStance()
        {
            P.Stance = StanceState.None;
            Arts.OnCommandReset(W, P, CommandResetReason.StanceExit);
        }

        public EnemyState AddEnemy(float x, float y, float hp = 100000f, EnemyTags tags = EnemyTags.None, bool boss = false)
        {
            var e = new EnemyState
            {
                Id = W.NewEntityId(), DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(x, y), BodyRadius = 0.4f,
                Hp = hp, MaxHp = hp, Tags = tags, IsBoss = boss, Ai = EnemyAiState.Act
            };
            W.Enemies.Add(e);
            return e;
        }

        /// <summary>5단계만 n틱 돌린다(다른 단계는 이 테스트에 필요 없다).</summary>
        public void Stage5(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++)
            {
                Arts.Stage5_Arts(W);
                W.PlayerZones.RemoveAll(z => z.Dead);
                W.Tick++;
            }
        }

        public int CountEvents(SimEventType type, string id = null)
        {
            int n = 0;
            for (int i = 0; i < W.Events.Count; i++)
                if (W.Events[i].Type == type && (id == null || W.Events[i].Id == id)) n++;
            return n;
        }

        public SimEvent LastEvent(SimEventType type)
        {
            for (int i = W.Events.Count - 1; i >= 0; i--)
                if (W.Events[i].Type == type) return W.Events[i];
            throw new InvalidOperationException("no event " + type);
        }

        public PlayerZoneEntity Zone(PlayerZoneKind kind)
        {
            for (int i = 0; i < W.PlayerZones.Count; i++)
                if (!W.PlayerZones[i].Dead && W.PlayerZones[i].Kind == kind) return W.PlayerZones[i];
            return null;
        }
    }
}
