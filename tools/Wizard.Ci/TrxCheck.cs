using System;
using System.Globalization;
using System.IO;

namespace Wizard.Ci
{
    // dotnet test 의 결과를 TRX 로 판정한다(ci trx-check. tools/ci/test.sh, test-release.sh, pg-test.sh 가 부른다).
    // 콘솔 요약 줄('Skipped: 1', 한국어 판의 '건너뜀: 1')은 dotnet 의 표시 언어를 따르므로 세지 않는다: 이 PC 의 dotnet 은 요약을 한국어로 내서
    // 영어 줄을 grep 하던 건너뜀 검사가 조용히 꺼져 있었다(FIX-9 검토). TRX 의 Counters 는 언어와 상관없다.
    // 실패: TRX 가 없다, 결과 파일 수가 기대와 다르다(덮어써졌거나 돌지 않은 프로젝트), 전체 0, 실패, 건너뜀(건너뛴 테스트도 실패다. CLAUDE.md 2절)
    public static class TrxCheck
    {
        // 판정. null 이면 통과. expectedFiles: 테스트 프로젝트마다 TRX 하나(없으면 수를 보지 않는다)
        public static string? Problem(TrxCounts c, int? expectedFiles)
        {
            if (c.Files == 0)
            {
                return "TRX 결과가 없다(dotnet test 를 --logger trx --results-directory 로 돌렸는가)";
            }
            if (expectedFiles is int n && c.Files != n)
            {
                return "TRX 결과 파일이 " + N(c.Files) + "개다. 테스트 프로젝트 " + N(n) + "개의 결과가 하나씩 있어야 한다";
            }
            if (c.Total == 0)
            {
                return "테스트가 하나도 돌지 않았다(전체 0)";
            }
            if (c.Failed > 0)
            {
                return "실패한 테스트가 " + N(c.Failed) + "개다";
            }
            if (c.NotExecuted > 0)
            {
                return "건너뛴 테스트가 " + N(c.NotExecuted) + "개다(건너뛴 테스트도 실패다)";
            }
            return null;
        }

        public static string Summary(TrxCounts c) =>
            "결과 파일 " + N(c.Files) + "개, 전체 " + N(c.Total) + ", 통과 " + N(c.Passed) + ", 실패 " + N(c.Failed) + ", 건너뜀 " + N(c.NotExecuted);

        public static int Run(string dir, int? expectedFiles, TextWriter stdout, TextWriter stderr)
        {
            TrxCounts c = TrxCounts.ReadDir(dir);
            string? problem = Problem(c, expectedFiles);
            if (problem != null)
            {
                stderr.WriteLine("FAIL  trx-check: " + problem + " — " + Summary(c) + " (" + dir + ")");
                return 1;
            }
            stdout.WriteLine("tests: " + N(c.Passed) + " passed (" + Summary(c) + ")");
            return 0;
        }

        private static string N(int n) => n.ToString(CultureInfo.InvariantCulture);
    }
}
