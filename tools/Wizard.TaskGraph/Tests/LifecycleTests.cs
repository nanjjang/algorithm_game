using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Xunit;

namespace Wizard.TaskGraph.Tests
{
    // 수락 2·3·4·6·9: ready, start, 상태 전이(잠금), reconcile, 작업 트리 위치.
    public sealed class LifecycleTests
    {
        private static JsonObject Graph()
        {
            return Fx.Doc(
                Fx.Task("M0-BT-01", group: "M0-W01", paths: new[] { "a/" }),
                Fx.Task("M0-BT-02", group: "M0-W02", deps: new[] { "M0-BT-01" }, paths: new[] { "b/" }),
                Fx.Task("M0-BT-03", group: "M0-W02", deps: new[] { "M0-BT-01" }, paths: new[] { "c/" }),
                Fx.Task("M0-BT-04", group: "M0-W03", deps: new[] { "M0-BT-02", "M0-BT-03" }, paths: new[] { "d/" }),
                Fx.Task("M0-UE-01", group: "M0-W02", deps: new[] { "M0-BT-01" }, paths: new[] { "unity/ProjectSettings/" }),
                Fx.Task("V-01", milestone: "V", group: "V-assets", status: "blocked-on-assets", paths: new[] { "unity/Assets/Art/" }));
        }

        private static (TempRepo Repo, string TasksPath, Lifecycle Life) Setup(bool git = true)
        {
            TempRepo repo = TempRepo.Create(git);
            string tasks = repo.PathOf("docs/dev/tasks.json");
            repo.Write("docs/dev/tasks.json", Fx.Text(Graph()));
            if (git)
            {
                repo.Commit("docs(M0-TL-01): graph");
            }
            var layout = Fx.LayoutIn(repo.Dir, tasks);
            return (repo, tasks, new Lifecycle(layout, TaskDoc.Load(tasks)));
        }

        private static string ReadyText(Lifecycle life)
        {
            return string.Join(" | ", life.Ready().Select(g => g.Group + ": " + string.Join(",", g.Tasks.Select(t => t.Id))));
        }

        [Fact]
        public void Ready_ListsTodoTasksWhoseDepsAreDone_ByGroup()
        {
            var (repo, _, life) = Setup(git: false);
            using (repo)
            {
                Assert.Equal("M0-W01: M0-BT-01", ReadyText(life));
                life.Store.Update(st => st["M0-BT-01"] = "done");
                Assert.Equal("M0-W02: M0-BT-02,M0-BT-03,M0-UE-01", ReadyText(life));
                life.Store.Update(st =>
                {
                    st["M0-BT-02"] = "done";
                    st["M0-BT-03"] = "review";   // review 는 done 이 아니다
                    return 0;
                });
                Assert.Equal("M0-W02: M0-UE-01", ReadyText(life));
                life.Store.Update(st => st["M0-BT-03"] = "done");
                Assert.Equal("M0-W02: M0-UE-01 | M0-W03: M0-BT-04", ReadyText(life));
                // blocked-on-assets 는 unblock 전에는 나오지 않는다
                Assert.DoesNotContain("V-01", ReadyText(life));
                life.Transition("V-01", "blocked-on-assets", "todo");
                Assert.Contains("V-assets: V-01", ReadyText(life));
            }
        }

        [Fact]
        public void Transitions_FollowTheLifecycle_AndNeverTouchTasksJson()
        {
            var (repo, tasks, life) = Setup(git: false);
            using (repo)
            {
                byte[] before = File.ReadAllBytes(tasks);
                life.Store.Update(st => st["M0-BT-01"] = "in_progress");
                Assert.Throws<TaskGraphException>(() => life.Transition("M0-BT-01", "review", "in_progress")); // reopen 은 review 에서만
                life.Transition("M0-BT-01", "in_progress", "review");
                Assert.Throws<TaskGraphException>(() => life.Transition("M0-BT-01", "in_progress", "review"));
                life.Transition("M0-BT-01", "review", "in_progress");
                life.Transition("M0-BT-01", "in_progress", "review");
                Assert.Throws<TaskGraphException>(() => life.Transition("M0-BT-02", "blocked-on-assets", "todo"));
                life.Transition("V-01", "blocked-on-assets", "todo");
                Assert.Throws<TaskGraphException>(() => life.Transition("M0-XX-99", "todo", "review"));

                Assert.Equal(before, File.ReadAllBytes(tasks));
                var st2 = life.Snapshot();
                Assert.Equal("review", st2["M0-BT-01"]);
                Assert.Equal("todo", st2["V-01"]);
                // 상태 파일은 결정적이다: 열쇠 오름차순, LF, 같은 상태면 같은 바이트
                Assert.Equal("{\n \"schema\": 1,\n \"status\": {\n  \"M0-BT-01\": \"review\",\n  \"V-01\": \"todo\"\n }\n}\n",
                    File.ReadAllText(repo.PathOf(".claude/taskstate.json")));
            }
        }

        [Fact]
        public void CorruptStateFile_FailsLoudly()
        {
            var (repo, _, life) = Setup(git: false);
            using (repo)
            {
                repo.Write(".claude/taskstate.json", "{\"schema\":1,\"status\":{\"M0-BT-01\":\"finished\"}}");
                var e = Assert.Throws<TaskGraphException>(() => life.Snapshot());
                Assert.Contains("모르는 상태", e.Message);
            }
        }

        [Fact]
        public async Task ConcurrentProcesses_DoNotLoseUpdates()
        {
            // 프로세스 여럿이 같은 상태 파일을 동시에 바꾼다. 잠금이 없으면 읽고-쓰기 사이에 남의 변경을 덮어 줄이 사라진다.
            using TempRepo repo = TempRepo.Create();
            const int count = 40;
            const int workers = 8;
            var ids = Enumerable.Range(1, count).Select(i => "V-" + i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)).ToList();
            repo.Write("docs/dev/tasks.json", Fx.Text(Fx.Doc(ids.Select(id =>
                Fx.Task(id, milestone: "V", group: "V-assets", status: "blocked-on-assets", paths: new[] { "art/" + id + "/" })).ToArray())));
            string tasks = repo.PathOf("docs/dev/tasks.json");
            string dll = typeof(Program).Assembly.Location;
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";

            var jobs = Enumerable.Range(0, workers).Select(w => Task.Run(() =>
            {
                var failures = new List<string>();
                for (int i = w; i < count; i += workers)
                {
                    ProcResult r = Proc.Run(dotnet, new[] { dll, "--repo", repo.Dir, "--tasks", tasks, "unblock", ids[i] });
                    if (r.ExitCode != 0)
                    {
                        failures.Add($"{ids[i]}: {r.ExitCode} {r.Stderr}");
                    }
                }
                return failures;
            })).ToArray();
            List<string>[] results = await Task.WhenAll(jobs);
            Assert.Empty(results.SelectMany(x => x));

            var state = new StateStore(repo.PathOf(".claude/taskstate.json")).Read();
            Assert.Equal(ids, state.Keys.ToList());
            Assert.All(state.Values, v => Assert.Equal("todo", v));
        }

        [Fact]
        public void DisabledFileLocking_IsRefused()
        {
            using TempRepo repo = TempRepo.Create();
            repo.Write("docs/dev/tasks.json", Fx.Text(Fx.Doc(Fx.Task("V-01", milestone: "V", group: "V-assets", status: "blocked-on-assets"))));
            string dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
            ProcResult r = Proc.Run("/usr/bin/env", new[]
            {
                "DOTNET_SYSTEM_IO_DISABLEFILELOCKING=1", dotnet, typeof(Program).Assembly.Location,
                "--repo", repo.Dir, "--tasks", repo.PathOf("docs/dev/tasks.json"), "unblock", "V-01",
            });
            Assert.Equal(1, r.ExitCode);
            Assert.Contains("파일 잠금이 걸리지 않는다", r.Stderr);
            Assert.False(File.Exists(repo.PathOf(".claude/taskstate.json")));
        }

        [Fact]
        public async Task ConcurrentThreads_DoNotLoseUpdates()
        {
            using TempRepo dir = TempRepo.Create(git: false);
            var store = new StateStore(dir.PathOf(".claude/taskstate.json"));
            var jobs = Enumerable.Range(0, 200).Select(i => Task.Run(() =>
                store.Update(st => st["T-" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)] = "done"))).ToArray();
            await Task.WhenAll(jobs);
            Assert.Equal(200, store.Read().Count);
        }

        [Fact]
        public void Start_CreatesWorktreeUnderMainRepo_AndCardCarriesAbsoluteWt()
        {
            var (repo, tasks, life) = Setup();
            using (repo)
            {
                life.Store.Update(st => st["M0-BT-01"] = "done");
                string msg = life.Start("M0-BT-02", "main");
                string wt = Path.Combine(repo.Dir, ".claude", "worktrees", "M0-BT-02");
                Assert.Contains("WT=" + wt, msg);
                Assert.True(File.Exists(Path.Combine(wt, "docs", "dev", "tasks.json")));
                Assert.Equal("task/M0-BT-02", TempRepo.GitIn(wt, "rev-parse", "--abbrev-ref", "HEAD").Trim());
                Assert.Equal(TempRepo.GitIn(repo.Dir, "rev-parse", "main").Trim(), TempRepo.GitIn(wt, "rev-parse", "HEAD").Trim());
                Assert.Equal("in_progress", life.Snapshot()["M0-BT-02"]);

                // 두 번 시작하지 않는다. deps 가 끝나지 않은 작업은 시작하지 않는다
                Assert.Throws<TaskGraphException>(() => life.Start("M0-BT-02", "main"));
                var e = Assert.Throws<TaskGraphException>(() => life.Start("M0-BT-04", "main"));
                Assert.Contains("M0-BT-02, M0-BT-03", e.Message);
                Assert.False(Directory.Exists(Path.Combine(repo.Dir, ".claude", "worktrees", "M0-BT-04")));

                // 작업 트리 안에서 부른 도구도 같은 메인 저장소의 상태 파일과 작업 트리 자리를 쓴다
                CliRun card = CliRun.Run("--repo", wt, "--tasks", tasks, "card", "M0-BT-02");
                Assert.True(card.Code == 0, card.ToString());
                Assert.Contains("WT=" + wt + "   브랜치: task/M0-BT-02", card.Stdout);
                Assert.Contains("- M0-BT-01 — 작업 M0-BT-01 (done). 남긴 곳: `a/`", card.Stdout);
            }
        }

        [Fact]
        public void Cli_Start_ThenReviewDone_RemovesWorktreeAndBranch()
        {
            var (repo, tasks, _) = Setup();
            using (repo)
            {
                CliRun r1 = CliRun.In(repo, tasks, "start", "M0-BT-01");
                Assert.True(r1.Code == 0, r1.ToString());
                string wt = Path.Combine(repo.Dir, ".claude", "worktrees", "M0-BT-01");
                repo.Write(".claude/worktrees/M0-BT-01/a/x.txt", "x\n");
                repo.Commit("feat(M0-BT-01): x", wt);

                CliRun early = CliRun.In(repo, tasks, "done", "M0-BT-01");
                Assert.Equal(1, early.Code); // review 가 아니다
                Assert.True(CliRun.In(repo, tasks, "review", "M0-BT-01").Code == 0);

                CliRun notMerged = CliRun.In(repo, tasks, "done", "M0-BT-01");
                Assert.Equal(1, notMerged.Code);
                Assert.Contains("main 에 '<type>(M0-BT-01): …' 커밋이 없다", notMerged.Stderr);

                // 통합 담당이 main 에 한 커밋으로 넣는다(squash)
                TempRepo.GitIn(repo.Dir, "merge", "--squash", "task/M0-BT-01");
                repo.Commit("feat(M0-BT-01): x");
                CliRun done = CliRun.In(repo, tasks, "done", "M0-BT-01");
                Assert.True(done.Code == 0, done.ToString());
                Assert.False(Directory.Exists(wt));
                Assert.Equal("", TempRepo.GitIn(repo.Dir, "branch", "--list", "task/M0-BT-01").Trim());
                Assert.Contains("M0-BT-01\tdone", CliRun.In(repo, tasks, "status", "M0-BT-01").Stdout);
                Assert.Contains("M0-W02", CliRun.In(repo, tasks, "ready").Stdout);
            }
        }

        [Fact]
        public void Done_RefusesWhenWorktreeHasUncommittedWork()
        {
            var (repo, _, life) = Setup();
            using (repo)
            {
                life.Start("M0-BT-01", "main");
                life.Transition("M0-BT-01", "in_progress", "review");
                repo.Commit("feat(M0-BT-01): merged");
                repo.Write(".claude/worktrees/M0-BT-01/a/dirty.txt", "not committed\n");
                Assert.Throws<TaskGraphException>(() => life.Done("M0-BT-01", "main"));
                Assert.Equal("review", life.Snapshot()["M0-BT-01"]);
                Assert.True(Directory.Exists(Path.Combine(repo.Dir, ".claude", "worktrees", "M0-BT-01")));
            }
        }

        [Fact]
        public void Reconcile_RebuildsDoneFromMainLog_AndInProgressFromWorktrees()
        {
            var (repo, tasks, life) = Setup();
            using (repo)
            {
                repo.Commit("feat(M0-BT-01): first");
                repo.Commit("fix(M0-BT-03): third");
                repo.Commit("chore(m0): not a task id");
                repo.Commit("M0-BT-04 without type");
                repo.Commit("wip(M0-UE-01): unknown type");
                life.Store.Update(st => st["M0-BT-01"] = "done");
                life.Start("M0-BT-02", "main");

                File.Delete(repo.PathOf(".claude/taskstate.json")); // 상태 파일을 잃었다
                Assert.Empty(life.Snapshot());
                CliRun r = CliRun.In(repo, tasks, "reconcile");
                Assert.True(r.Code == 0, r.ToString());
                var st2 = life.Snapshot();
                Assert.False(st2.ContainsKey("M0-TL-01")); // tasks.json 에 없는 id 는 무시한다
                Assert.Equal("done", st2["M0-BT-01"]);
                Assert.Equal("done", st2["M0-BT-03"]);
                Assert.Equal("in_progress", st2["M0-BT-02"]);
                Assert.False(st2.ContainsKey("M0-BT-04"));
                Assert.False(st2.ContainsKey("M0-UE-01"));

                // 다시 돌려도 같다
                string once = File.ReadAllText(repo.PathOf(".claude/taskstate.json"));
                Assert.True(CliRun.In(repo, tasks, "reconcile").Code == 0);
                Assert.Equal(once, File.ReadAllText(repo.PathOf(".claude/taskstate.json")));
            }
        }

        [Fact]
        public void Start_ForUnityTask_ClonesLibraryWhenPresent()
        {
            var (repo, _, life) = Setup();
            using (repo)
            {
                life.Store.Update(st => st["M0-BT-01"] = "done");
                repo.Write("unity/Library/cache.bin", "cache");
                string msg = life.Start("M0-UE-01", "main");
                Assert.Contains("unity/Library", msg);
                Assert.Equal("cache", File.ReadAllText(Path.Combine(repo.Dir, ".claude", "worktrees", "M0-UE-01", "unity", "Library", "cache.bin"), Encoding.UTF8));
            }
        }
    }
}
