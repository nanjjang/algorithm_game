using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Spells;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>감속·잠식을 상태에 그대로 적고(주인 = Enemies) 부른 인자를 기록하는 가짜.</summary>
    public sealed class FakeEnemies : EnemyModule
    {
        public struct SlowCall { public int EnemyId; public SlowSlot Slot; public float Strength; public int Ticks; public CcSource Source; }
        public struct CcCall { public int EnemyId; public CcKind Kind; public float Amount; public CcSource Source; public Vector2 Dir; }

        public readonly List<SlowCall> Slows = new List<SlowCall>();
        public readonly List<CcCall> Ccs = new List<CcCall>();
        public readonly List<int> Corrosions = new List<int>();
        public readonly List<int> DashCancels = new List<int>();
        /// <summary>ModifyIncomingDamage가 곱할 값(1 = 그대로).</summary>
        public float IncomingMul = 1f;
        /// <summary>TryCancelDash가 돌려줄 값(돌진 예고 중인 적이었나).</summary>
        public bool CancelDashResult;
        /// <summary>ModifyIncomingDamage가 받은 맞은 자리(차례대로).</summary>
        public readonly List<Vector2> HitPositions = new List<Vector2>();

        public override float ModifyIncomingDamage(SimWorld w, EnemyState e, float amount, bool isCrit, bool ignoresArmor, Vector2 fromPos, Vector2 hitPos)
        {
            HitPositions.Add(hitPos);
            return amount * IncomingMul;
        }

        public override void ApplySlow(SimWorld w, EnemyState e, SlowSlot slot, float strength, int ticks, CcSource source)
        {
            Slows.Add(new SlowCall { EnemyId = e.Id, Slot = slot, Strength = strength, Ticks = ticks, Source = source });
            e.Slows[(int)slot] = new SlowEntry { Strength = strength, UntilTick = w.Tick + ticks };
        }

        public override void ApplyCorrosion(SimWorld w, EnemyState e, int ticks)
        {
            Corrosions.Add(e.Id);
            e.CorrosionUntilTick = w.Tick + ticks;
        }

        public override float ApplyCc(SimWorld w, EnemyState e, CcKind kind, float amount, CcSource source, Vector2 dir)
        {
            Ccs.Add(new CcCall { EnemyId = e.Id, Kind = kind, Amount = amount, Source = source, Dir = dir });
            return amount;
        }

        public override bool TryCancelDash(SimWorld w, EnemyState e, int staggerTicks)
        {
            DashCancels.Add(staggerTicks);
            return CancelDashResult;
        }
    }

    public sealed class FakeTraits : TraitModule
    {
        public readonly Queue<float> Spreads = new Queue<float>();
        public int SpreadRolls;
        public float ExplosionMul = 1f;
        public readonly List<HitInfo> Hits = new List<HitInfo>();

        public override float RollSpreadDegrees(SimWorld w, PlayerState p)
        {
            SpreadRolls++;
            return Spreads.Count > 0 ? Spreads.Dequeue() : 0f;
        }
        public override float ExplosionRadiusMul(PlayerState p) => ExplosionMul;
        public override void OnEnemyHit(SimWorld w, in HitInfo hit) { Hits.Add(hit); }
    }

    public sealed class FakePlayer : PlayerModule
    {
        public readonly List<HitInfo> Hits = new List<HitInfo>();
        public override void OnPlayerHitEnemy(SimWorld w, in HitInfo hit) { Hits.Add(hit); }
    }

    /// <summary>주문 모듈만 진짜인 세계. 전투 중(WaveCombat)으로 세우고 플레이어 0을 (2, 7)에 동쪽을 보게 둔다.</summary>
    public sealed class SpellWorld
    {
        public readonly IContentDb Db = TestContent.Db;
        public readonly SpellSystem Sys = new SpellSystem();
        public readonly FakeEnemies Enemies = new FakeEnemies();
        public readonly FakeTraits Traits = new FakeTraits();
        public readonly FakePlayer Player = new FakePlayer();
        public readonly SimWorld W;
        public readonly PlayerState P;
        int _shotId = 1;

        public SpellWorld()
        {
            var mods = SimModuleSet.Stubs();
            mods.Spells = Sys;
            mods.Enemies = Enemies;
            mods.Traits = Traits;
            mods.Player = Player;
            W = new SimWorld(Db, mods, 1);
            W.Run.Phase = RunPhase.WaveCombat;
            P = W.Players[0];
            P.Active = true;
            P.CharId = Ids.Char.C5;
            P.Def = Db.Character(Ids.Char.C5);
            P.Pos = new Vector2(2f, 7f);
            P.Derived = new PlayerDerived { DepthDecay = Db.Const.DepthDecayDefault, EnhanceMul = 1f, MutationDamageMul = 1f };
        }

        public CompiledSpell Compile(string core, params string[] links) => CompileWith(false, 0f, core, links);

        /// <summary>decay 0 = 기본 감쇠(const.json).</summary>
        public CompiledSpell CompileWith(bool underline, float decay, string core, params string[] links)
        {
            var ctx = new SpellCompileContext
            {
                LinkSlotCount = links.Length,
                Underline = underline,
                DepthDecay = decay > 0f ? decay : Db.Const.DepthDecayDefault
            };
            return Sys.Compile(Db, core, links, ctx);
        }

        public ExecuteRequest Request(CompiledSpell tree, float degrees = 0f)
        {
            return new ExecuteRequest
            {
                OwnerSlot = 0,
                Tree = tree,
                Pos = P.Pos,
                DirDegrees = degrees,
                DamageMul = 1f,
                CharacterMul = 1f,
                Source = ShotSource.Fire,
                ShotId = _shotId++
            };
        }

        public ExecuteResult Fire(CompiledSpell tree, float degrees = 0f) => Sys.Execute(W, Request(tree, degrees));

        public EnemyState AddEnemy(float x, float y, float hp = 1000f, float radius = 0.3f, string defId = Ids.Enemy.SlimeMoss)
        {
            var e = new EnemyState
            {
                Id = W.NewEntityId(),
                DefId = defId,
                Pos = new Vector2(x, y),
                PrevPos = new Vector2(x, y),
                BodyRadius = radius,
                Hp = hp,
                MaxHp = hp,
                Ai = EnemyAiState.Act
            };
            W.Enemies.Add(e);
            return e;
        }

        public void Step(int ticks = 1)
        {
            for (int i = 0; i < ticks; i++) W.Step();
        }

        public int CountEvents(SimEventType type, int entityId = -1)
        {
            int n = 0;
            for (int i = 0; i < W.Events.Count; i++)
                if (W.Events[i].Type == type && (entityId < 0 || W.Events[i].EntityId == entityId)) n++;
            return n;
        }

        public List<SimEvent> EventsOf(SimEventType type, int entityId = -1)
        {
            var list = new List<SimEvent>();
            for (int i = 0; i < W.Events.Count; i++)
                if (W.Events[i].Type == type && (entityId < 0 || W.Events[i].EntityId == entityId)) list.Add(W.Events[i]);
            return list;
        }

        public int LiveProjectiles()
        {
            int n = 0;
            for (int i = 0; i < W.Projectiles.Count; i++) if (!W.Projectiles[i].Dead) n++;
            return n;
        }
    }

    /// <summary>블록 id 줄임(테스트 읽기 쉽게).</summary>
    public static class B
    {
        public const string Mb = Ids.Block.MagicBolt, Fb = Ids.Block.Fireball, Cl = Ids.Block.ChainLightning, Bm = Ids.Block.Boomerang,
            Ice = Ids.Block.IceSpike, Orb = Ids.Block.ShadowOrb, S2 = Ids.Block.SplitTwo, S3 = Ids.Block.SplitThree,
            Trig = Ids.Block.OnHitTrigger, Bounce = Ids.Block.WallBounce, Size = Ids.Block.DoubleSize, Pierce = Ids.Block.Pierce,
            Frost = Ids.Block.Frost, Homing = Ids.Block.Homing, Triple = Ids.Block.TripleHeat, Corr = Ids.Block.Corrosion;
        public const string None = null;
    }
}
