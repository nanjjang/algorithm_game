#nullable enable

namespace Wizard.Unity.App.Headless
{
    // 골든 항목 하나를 돌릴 준비: 펼친 입력 기록과 세계의 종류, 해시 간격. GoldenReplay.Prepare 가 만들고 확인한다.
    // 같은 기록을 여러 번(끊지 않고, 되감기·상태 옮기기를 끼워) 돌릴 때 기록을 다시 읽지 않는다.
    public sealed class GoldenPlan
    {
        internal GoldenPlan(string id, string kind, GoldenRecord record, uint hashEvery, int players)
        {
            Id = id;
            Kind = kind;
            Record = record;
            HashEvery = hashEvery;
            Players = players;
        }

        public string Id { get; }

        // practice | endless | duel (tests/golden/README.md 의 kind)
        public string Kind { get; }

        public GoldenRecord Record { get; }

        public uint HashEvery { get; }

        public int Players { get; }
    }
}
