using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Wizard.Ci.Tests
{
    // 이 어셈블리의 시험 전체가 바깥 게이트의 환경에서도 통과하는가(FIX-4).
    // 이 시험만 빼고 나머지를, 바깥 게이트의 환경(임시 저장소에 없는 WIZARD_CI_BASE, WIZARD_CI_FORCE_ALL=1, 게이트가 검사 스크립트에 주는
    // 나머지 변수)을 준 자식 프로세스(dotnet test <이 어셈블리>)로 다시 돌린다. 실패, 건너뜀, 결과 없음은 전부 실패다.
    // M1-TE-01 에서 본 누출: WIZARD_CI_BASE=<계약 개정 커밋> 으로 돈 fast 게이트에서 ChangeScopeTests 셋이
    // "비교 기준 '<그 커밋>' 을 git 이 모른다"로 실패했다(ChangeScope.Detect 가 프로세스 환경을 읽었다).
    // 같은 산출물 폴더(artifacts/ci-tests-build 의 taskgraph 빌드)를 쓰는 다른 시험과 겹치지 않게 병렬을 끈 묶음에서 돈다.
    [Collection(SerialCollection.Name)]
    public sealed class WholeSuiteUnderGateEnvironmentTests
    {
        // 자식 실행의 표시. 자식이 이 시험을 다시 고르면(--filter 가 빼지 못하면) 끝없이 되부르지 않게 실패한다
        private const string ChildMarker = "WIZARD_CI_SELFTEST_CHILD";

        // M1-TE-01 의 누출에서 실패한 셋. 자식 실행의 결과에 이 이름들이 있어야 한다(걸러져 빠지지 않았다)
        private static readonly string[] IncidentTests =
        {
            "Wizard.Ci.Tests.ChangeScopeTests.TaskBranch_SeesCommittedStagedUnstagedAndUntrackedChanges_SinceTheMergeBase",
            "Wizard.Ci.Tests.ChangeScopeTests.DeletedFiles_CountAsChanged",
            "Wizard.Ci.Tests.ChangeScopeTests.OnMain_ForcedOrWithoutMain_EverythingCounts",
            "Wizard.Ci.Tests.GateEnvironmentTests.ScopeFunctions_IgnoreTheProcessEnvironment_WhenGivenNoBase",
            "Wizard.Ci.Tests.TaskBranchTests.Fast_WithTheBaseAtAContractAmendmentCommit_ChecksOnlyTheRest",
        };

        [Fact]
        public void WholeSuite_PassesUnderAnOuterGateEnvironment()
        {
            Assert.True(Environment.GetEnvironmentVariable(ChildMarker) == null,
                "자식 실행이 이 시험을 다시 골랐다(--filter 가 이 시험을 빼지 못했다): 끝없이 되부르지 않게 멈춘다");

            string self = typeof(WholeSuiteUnderGateEnvironmentTests).FullName + "." + nameof(WholeSuite_PassesUnderAnOuterGateEnvironment);
            string work = Path.Combine(TempGit.RepoRoot, "artifacts", "ci-tests", "gate-env-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            string trx = Path.Combine(work, "trx");
            Directory.CreateDirectory(work);
            try
            {
                // 바깥 저장소의 HEAD: 임시 저장소에는 없는 커밋이다
                string outer = TempGit.GitIn(TempGit.RepoRoot, "rev-parse", "HEAD").Trim();
                var req = new ProcRequest("dotnet", new[]
                {
                    "test", TempGit.RunnerDll, "--filter", "FullyQualifiedName!=" + self, "--logger", "trx", "--results-directory", trx,
                })
                {
                    WorkingDirectory = work,
                    Capture = true,
                    Timeout = TimeSpan.FromMinutes(15),
                };
                req.Environment[ChildMarker] = "1";
                req.Environment["WIZARD_CI_BASE"] = outer;
                req.Environment["WIZARD_CI_FORCE_ALL"] = "1";
                req.Environment["WIZARD_CI_STAGE"] = "fast";
                req.Environment["WIZARD_ROOT"] = TempGit.RepoRoot;
                req.Environment["WIZARD_CI_NOTE"] = Path.Combine(work, "outer.note");
                req.Environment["WIZARD_CI_DLL"] = TempGit.RunnerDll;
                req.Environment["WIZARD_CI_TRX_DIR"] = Path.Combine(work, "outer-trx");
                req.Environment["CI_STEP_TIMEOUT"] = "1";
                req.Environment["WIZARD_CI_LOCK_WAIT"] = "0";
                ProcResult r = ProcessRunner.Run(req);

                TrxCounts c = TrxCounts.ReadDir(trx);
                string detail = "자식 실행(WIZARD_CI_BASE=" + outer + ", WIZARD_CI_FORCE_ALL=1): 종료 " + r.ExitCode + (r.TimedOut ? "(시간 제한)" : "") +
                                ", 전체 " + c.Total + ", 실패 " + c.Failed + ", 건너뜀 " + c.NotExecuted + "\n" + Tail(r.Stdout + r.Stderr);
                Assert.True(r.ExitCode == 0, detail);
                Assert.True(c.Files == 1, detail);
                Assert.True(c.Total > 0 && c.Failed == 0 && c.NotExecuted == 0 && c.Passed == c.Total, detail);
                string text = File.ReadAllText(Directory.GetFiles(trx, "*.trx", SearchOption.AllDirectories)[0]);
                foreach (string name in IncidentTests)
                {
                    Assert.True(text.Contains("testName=\"" + name + "\"", StringComparison.Ordinal), name + " 이 자식 실행에 없다. " + detail);
                }
                Assert.False(text.Contains("testName=\"" + self + "\"", StringComparison.Ordinal), "자식 실행이 이 시험을 다시 골랐다");
            }
            finally
            {
                try
                {
                    Directory.Delete(work, true);
                }
                catch (IOException)
                {
                    // 결과에 영향이 없다. artifacts/ 는 git 이 무시한다
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static string Tail(string s)
        {
            const int Max = 6000;
            return s.Length <= Max ? s : s.Substring(s.Length - Max);
        }
    }
}
