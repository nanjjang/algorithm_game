namespace Wizard.Ci
{
    // ci.sh full 의 Unity 단계(배치 컴파일, EditMode)를 건너뛸 수 있는가(PIPELINE 3.8 "경로 조건").
    // 바뀐 경로가 아래 어디에도 닿지 않을 때만 건너뛴다(서버, 서버 테스트, 문서, 그 밖의 도구만 바뀐 경우). 건너뛴 것은 결과에 적힌다.
    public static class UnityScope
    {
        public static readonly string[] Prefixes =
        {
            "src/", "unity/", "data/", "tests/golden/", "tests/Wizard.TestKit/Shared/", "tools/Wizard.MetaGen/",
        };

        // 필요하면 true. reason 은 결과에 적을 한 줄
        public static bool Needed(ChangeScope scope, out string reason)
        {
            if (scope.All)
            {
                reason = scope.Reason;
                return true;
            }
            if (scope.Touches(Prefixes, out string? first))
            {
                reason = first + " 이 바뀌었다(" + scope.Reason + ")";
                return true;
            }
            reason = "바뀐 경로 " + scope.Paths.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + "개가 " +
                     string.Join(", ", Prefixes) + " 어디에도 닿지 않는다(" + scope.Reason + ")";
            return false;
        }
    }
}
