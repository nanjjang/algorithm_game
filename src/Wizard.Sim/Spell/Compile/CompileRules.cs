using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Spell.Compile
{
    // 컴파일러가 읽는 기획 수치 셋(DECISIONS-TECH T-57): spec 02 6.2 SF-2(트리 실행 1회의 투사체 상한 64),
    // 7.1 HT-5(갈피 밑줄이 깎는 양의 상한 5 = 600 HU), HT-7(거울상의 발사당 열 배수 1.5 = 1500).
    // 값은 팩의 blocks 절 꼬리(ContentDb.SpellRules, 원본 data/sim/blocks.json 의 spell_rules)에서 읽는다. 코드에 수를 적지 않는다.
    internal readonly struct CompileRules
    {
        public readonly int TreeProjectileCap;       // 트리 실행 1회의 투사체 상한(SF-2, 8.1)
        public readonly int UnderlineMaxCutHu;       // 밑줄이 블록 하나에서 깎는 열의 상한(HU. HT-5)
        public readonly int MirrorHeatMulPermille;   // 거울상의 발사당 열 배수(천분율. HT-7)

        public CompileRules(int treeProjectileCap, int underlineMaxCutHu, int mirrorHeatMulPermille)
        {
            if (treeProjectileCap < 1 || underlineMaxCutHu < 0 || mirrorHeatMulPermille < 0)
            {
                throw new SimInvariantException("CompileRules: cap must be >= 1, underline cut and mirror multiplier >= 0");
            }
            TreeProjectileCap = treeProjectileCap;
            UnderlineMaxCutHu = underlineMaxCutHu;
            MirrorHeatMulPermille = mirrorHeatMulPermille;
        }

        // 팩에 주문 규칙이 없으면(blocks 절의 꼬리가 없는 시험용 팩) ContentException
        public static CompileRules FromContent(ContentDb db)
        {
            SpellRulesDef r = db.SpellRules;
            return new CompileRules(r.TreeProjectileCap, r.UnderlineMaxCutHu, r.MirrorHeatMulPermille);
        }
    }
}
