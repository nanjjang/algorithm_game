using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Traits;

namespace Wizard.Solo.Tests.Traits
{
    /// <summary>표준 T를 고정 값으로 돌려주고 직접 피해 요청을 적어 두는 가짜 주문 모듈.</summary>
    public sealed class FakeSpells : SpellModule
    {
        public float T = 10f;
        public readonly List<DirectDamage> Direct = new List<DirectDamage>();
        /// <summary>이 id의 적은 직접 피해를 받으면 죽어 가는 상태가 된다.</summary>
        public readonly HashSet<int> KillOnDirect = new HashSet<int>();

        public override float StandardT(SimWorld w, PlayerState p) => T;

        public override DamageResult DealDirect(SimWorld w, in DirectDamage dmg)
        {
            Direct.Add(dmg);
            var e = w.FindEnemy(dmg.TargetEnemyId);
            bool killed = false;
            if (e != null && KillOnDirect.Contains(e.Id))
            {
                e.PendingDeath = true;
                killed = true;
            }
            return new DamageResult { Applied = dmg.Amount, Killed = killed };
        }
    }

    public struct CcCall
    {
        public int EnemyId;
        public CcKind Kind;
        public float Amount;
        public CcSource Source;
        public Vector2 Dir;
        public int Tick;
    }

    /// <summary>군중 제어 요청의 인자를 적어 두는 가짜 적 모듈(꼬리표별 변환은 진짜 Enemies의 몫이라 여기서는 받은 값을 그대로 돌려준다).</summary>
    public sealed class FakeEnemies : EnemyModule
    {
        public readonly List<CcCall> Cc = new List<CcCall>();

        public override float ApplyCc(SimWorld w, EnemyState e, CcKind kind, float amount, CcSource source, Vector2 dir)
        {
            Cc.Add(new CcCall { EnemyId = e.Id, Kind = kind, Amount = amount, Source = source, Dir = dir, Tick = w.Tick });
            return amount;
        }
    }

    /// <summary>열 깎기만 하는 가짜 플레이어 모듈(계약: 잠금 중이면 아무 일도 없다).</summary>
    public sealed class FakePlayer : PlayerModule
    {
        public readonly List<int> Reduced = new List<int>();

        public override void ReduceHeat(SimWorld w, PlayerState p, int hu)
        {
            if (p.Locked) return;
            Reduced.Add(hu);
            p.HeatHu = Mathf.Max(0, p.HeatHu - hu);
        }
    }

    /// <summary>Traits만 진짜인 세계를 세운다.</summary>
    public sealed class TraitRig
    {
        public readonly SimWorld W;
        public readonly TraitSystem Traits = new TraitSystem();
        public readonly FakeSpells Spells = new FakeSpells();
        public readonly FakeEnemies Enemies = new FakeEnemies();
        public readonly FakePlayer Player = new FakePlayer();
        public PlayerState P => W.Players[0];

        public TraitRig(string charId, int stage = 0, string core = Ids.Block.MagicBolt, ulong seed = 1, params string[] mutations)
        {
            var mods = SimModuleSet.Stubs();
            mods.Traits = Traits;
            mods.Spells = Spells;
            mods.Enemies = Enemies;
            mods.Player = Player;
            W = new SimWorld(TestContent.Db, mods, seed);
            W.Run.Phase = RunPhase.WaveCombat;

            var p = W.Players[0];
            p.Active = true;
            p.CharId = charId;
            p.Def = TestContent.Db.Character(charId);
            p.Pos = new Vector2(12f, 7f);
            p.Wand.Stage = stage;
            for (int i = 0; i < mutations.Length; i++) p.Wand.Mutations.Add(mutations[i]);
            p.Loadout.Core = new SlotItem { BlockId = core };
            Traits.RecomputeDerived(W, p);
        }

        public EnemyState AddEnemy(float x, float y, EnemyTags tags = EnemyTags.None)
        {
            var e = new EnemyState
            {
                Id = W.NewEntityId(), DefId = Ids.Enemy.SlimeMoss, Pos = new Vector2(x, y), Hp = 1000f, MaxHp = 1000f,
                Ai = EnemyAiState.Act, Tags = tags
            };
            W.Enemies.Add(e);
            return e;
        }

        public int CountEvents(SimEventType type)
        {
            int n = 0;
            for (int i = 0; i < W.Events.Count; i++) if (W.Events[i].Type == type) n++;
            return n;
        }

        public HitInfo Hit(EnemyState e, DamageOrigin origin = DamageOrigin.Projectile)
        {
            return new HitInfo { AttackerSlot = 0, EnemyId = e.Id, Origin = origin, Amount = 10f, Pos = e.Pos };
        }
    }
}
