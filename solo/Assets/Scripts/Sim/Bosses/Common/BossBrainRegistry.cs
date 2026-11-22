using System;
using System.Collections.Generic;

namespace Wizard.Solo.Sim.Bosses
{
    /// <summary>보스 id → 두뇌 만드는 함수. BossSystem 하나가 표 하나를 가진다(정적 상태 없음).</summary>
    public sealed class BossBrainTable
    {
        readonly Dictionary<string, Func<IBossBrain>> _factories = new Dictionary<string, Func<IBossBrain>>();

        /// <summary>같은 id를 다시 등록하면 뒤의 것이 이긴다(테스트가 가짜 두뇌를 끼울 때 쓴다).</summary>
        public void Add(string bossId, Func<IBossBrain> factory)
        {
            if (bossId == null || factory == null) return;
            _factories[bossId] = factory;
        }

        public bool Has(string bossId) => bossId != null && _factories.ContainsKey(bossId);

        /// <summary>등록이 없으면 DefaultBossBrain.</summary>
        public IBossBrain Create(string bossId)
        {
            Func<IBossBrain> f;
            if (bossId != null && _factories.TryGetValue(bossId, out f))
            {
                var brain = f();
                if (brain != null) return brain;
            }
            return new DefaultBossBrain();
        }
    }

    public static class BossBrainRegistry
    {
        public static BossBrainTable Build()
        {
            var table = new BossBrainTable();
            table.Add(Ids.Boss.KilnBoar, () => new KilnBoarBrain());
            table.Add(Ids.Boss.MirrorPhantom, () => new MirrorPhantomBrain());
            table.Add(Ids.Boss.SporeQueen, () => new SporeQueenBrain());
            table.Add(Ids.Boss.StormOwl, () => new StormOwlBrain());
            table.Add(Ids.Boss.FrostGoat, () => new FrostGoatBrain());
            table.Add(Ids.Boss.NightSerpent, () => new NightSerpentBrain());
            return table;
        }
    }
}
