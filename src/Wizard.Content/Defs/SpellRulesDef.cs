namespace Wizard.Content
{
    // DECISIONS-TECH T-57. 주문 컴파일러가 읽는 규칙 값(spec 02 6.2 SF-2, 7.1 HT-5·HT-7). 팩에서 읽은 뒤 바뀌지 않는다.
    // 원본 data/sim/blocks.json 의 "spell_rules", 팩 절 2 blocks 의 꼬리(꼴은 Pack/Sections/ContentDb.Blocks.cs)
    public sealed class SpellRulesDef
    {
        public int TreeProjectileCap;         // 트리 실행 1회의 투사체 상한(SF-2. 64)
        public int UnderlineMaxCutHu;         // 밑줄이 블록 하나에서 깎는 열의 상한(HT-5. 5 → 600 HU)
        public int MirrorHeatMulPermille;     // 거울상의 발사당 열 배수(HT-7. 1.5 → 1500)
    }
}
