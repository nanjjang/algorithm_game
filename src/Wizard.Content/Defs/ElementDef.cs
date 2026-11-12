using Wizard.Core;

namespace Wizard.Content
{
    // CONTRACTS 2.2. 약점·저항 배율(DECISIONS 2.5: ×1.25, ×0.8, spec 02 3.3 AF-1). 원본 data/sim/elements.json,
    // 절 꼴은 Pack/Sections/ContentDb.Elements.cs. 속성별 약점·저항 배치(상성 표)는 적 정의의 몫이다.
    public sealed class ElementDef
    {
        public Fix64 WeakMultiplier;                        // 약점 속성 피해 배율. 1.25 = 5,368,709,120 (spec 02 2.2)
        public Fix64 ResistMultiplier;                      // 저항 속성 피해 배율. 0.8 = 3,435,973,837 (spec 02 2.2)

        // 속성 배율(spec 02 3.3 ElementMultiplier). 무속성 피해(Element.None)는 상성을 받지 않는다(AF-8).
        // 결투(AF-7)에서 1 로 두는 것은 부르는 쪽의 몫이다(모드는 이 정의가 모른다)
        public Fix64 Multiplier(Element damage, Element weak, Element resist)
        {
            if (damage == Element.None)
            {
                return Fix64.One;
            }
            if (damage == weak)
            {
                return WeakMultiplier;
            }
            if (damage == resist)
            {
                return ResistMultiplier;
            }
            return Fix64.One;
        }
    }
}
