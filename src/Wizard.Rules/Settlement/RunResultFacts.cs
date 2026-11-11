namespace Wizard.Rules.Settlement
{
    // CONTRACTS 4.3, 3.7. 판의 결과를 코어 버전과 무관하게 실어 나르는 정규 바이트를 읽은 것. Sim 타입에 기대지 않는다(정수와 id만).
    // TryRead/BagSelectionValid 는 M6-SIM-01 이 더한다
    public sealed class RunResultFacts
    {
        public byte FactsFormat;                          // 나머지는 CONTRACTS 3.7절 바이트의 필드 그대로(M6-SIM-01)
    }
}
