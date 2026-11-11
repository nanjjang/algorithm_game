using Wizard.TestKit.Shared.Golden;
using Xunit;

namespace Wizard.TestKit
{
    // [MemberData] 로 골든 항목마다 테스트 하나를 만든다. 예:
    //   public static TheoryData<string> M1 => GoldenData.Ids("m1");
    //   [Theory, MemberData(nameof(M1))] public void Golden(string id) { GoldenEntry e = TestRepo.LoadGolden().Get(id); … }
    // 항목이 하나도 없으면 xUnit 이 "데이터 없음"으로 실패한다(골든이 조용히 빠지지 않는다).
    public static class GoldenData
    {
        public static TheoryData<string> Ids(string milestone) => Ids(TestRepo.LoadGolden(), milestone);

        public static TheoryData<string> Ids(GoldenCatalog catalog, string milestone)
        {
            var data = new TheoryData<string>();
            foreach (GoldenEntry e in catalog.InMilestone(milestone))
            {
                data.Add(e.Id);
            }
            return data;
        }
    }
}
