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

    /// <summary>
    /// 두뇌 등록 자리. 보스 작업은 자기 폴더에 `BossBrainRegistry.&lt;보스&gt;.cs`를 두고 해당 partial 메서드 하나만 구현한다:
    /// <code>static partial void RegisterSporeQueen(BossBrainTable t) { t.Add(Ids.Boss.SporeQueen, () => new SporeQueenBrain()); }</code>
    /// 구현이 없는 partial 메서드는 컴파일 때 사라진다(그 보스는 DefaultBossBrain으로 돈다).
    /// </summary>
    public static partial class BossBrainRegistry
    {
        static partial void RegisterKilnBoar(BossBrainTable t);
        static partial void RegisterMirrorPhantom(BossBrainTable t);
        static partial void RegisterSporeQueen(BossBrainTable t);
        static partial void RegisterStormOwl(BossBrainTable t);
        static partial void RegisterFrostGoat(BossBrainTable t);
        static partial void RegisterNightSerpent(BossBrainTable t);

        public static BossBrainTable Build()
        {
            var t = new BossBrainTable();
            RegisterKilnBoar(t);
            RegisterMirrorPhantom(t);
            RegisterSporeQueen(t);
            RegisterStormOwl(t);
            RegisterFrostGoat(t);
            RegisterNightSerpent(t);
            return t;
        }
    }
}
