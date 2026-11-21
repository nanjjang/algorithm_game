using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Sim;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 기본 공격 유형(Docs/12-attack-styles.md) 표시의 공용 판단(순수). 내 탄 층·타격감 층·유형 층·소리 층이 같은 답을 쓴다.
    /// - 몸 없는 동작(G16 shots.style_body_hidden: 포탄·부채꼴·낙뢰·고드름·룬·사슬)은 내 탄 층·탄 빛이 그리지 않고 StyleFxLayer가 그린다.
    /// - 유형이 낸 폭발(Explosion B = 1)은 StyleBlast를 그리는 층이 맡는다 → 속성 폭발 그림·소리는 건너뛴다(두 번 그리지 않게).
    /// </summary>
    public static class StyleFxRules
    {
        static readonly ConditionalWeakTable<EffectsTuning, HashSet<string>> Hidden = new ConditionalWeakTable<EffectsTuning, HashSet<string>>();

        /// <summary>Explosion 이벤트의 B = 1: 기본 공격 유형이 낸 폭발(SpellSystem.Styles.StyleBlast).</summary>
        public static bool IsStyleExplosion(in SimEvent e) => e.Type == SimEventType.Explosion && e.B == 1;

        /// <summary>내 탄 층이 몸을 그리지 않는 유형 탄인가.</summary>
        public static bool BodyHidden(EffectsTuning g16, ProjectileEntity p)
        {
            if (p == null || p.Block == null || p.Block.Style == null || p.Block.Style.Behavior == null || g16 == null) return false;
            HashSet<string> set;
            if (!Hidden.TryGetValue(g16, out set))
            {
                set = new HashSet<string>();
                var arr = g16.Token("shots", "style_body_hidden") as JArray;
                if (arr != null)
                    foreach (var x in arr)
                        if (x.Type == JTokenType.String) set.Add((string)x);
                Hidden.Add(g16, set);
            }
            return set.Contains(p.Block.Style.Behavior);
        }
    }
}
