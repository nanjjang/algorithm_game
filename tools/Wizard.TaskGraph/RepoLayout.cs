using System;
using System.IO;

namespace Wizard.TaskGraph
{
    // 경로 정하기. 현재 폴더에 기대지 않는다(PIPELINE 4.3).
    //   ToolRoot  : 이 도구가 놓인 저장소(작업 트리일 수 있다). 자기 위치에서 위로 올라가 .git 이 있는 폴더.
    //   MainRepo  : 메인 체크아웃. git 의 공통 폴더(--git-common-dir)의 부모. 상태 파일과 작업 트리는 여기에 있다.
    //   TasksPath : ToolRoot/docs/dev/tasks.json (그 체크아웃의 작업 그래프)
    //   StatePath : MainRepo/.claude/taskstate.json (git 이 추적하지 않는다. T-40)
    public sealed class RepoLayout
    {
        public RepoLayout(string toolRoot, string mainRepo, string tasksPath, string specsDir, string schemaPath, string statePath)
        {
            ToolRoot = toolRoot;
            MainRepo = mainRepo;
            TasksPath = tasksPath;
            SpecsDir = specsDir;
            SchemaPath = schemaPath;
            StatePath = statePath;
        }

        public string ToolRoot { get; }
        public string MainRepo { get; }
        public string TasksPath { get; }
        public string SpecsDir { get; }
        public string SchemaPath { get; }
        public string StatePath { get; }

        public string WorktreesDir => Path.Combine(MainRepo, ".claude", "worktrees");

        public string WorktreeOf(string id) => Path.Combine(WorktreesDir, id);

        public static string BranchOf(string id) => "task/" + id;

        public static RepoLayout Resolve(CliOptions o)
        {
            string toolRoot = o.Repo != null ? Normalize(o.Repo) : FindRootFrom(AppContext.BaseDirectory);
            string mainRepo = FindMainRepo(toolRoot);
            string tasks = o.Tasks != null ? Normalize(o.Tasks) : Path.Combine(toolRoot, "docs", "dev", "tasks.json");
            string specs = o.Specs != null ? Normalize(o.Specs) : Path.Combine(toolRoot, "docs", "spec");
            string schema = o.Schema != null ? Normalize(o.Schema) : Path.Combine(toolRoot, "docs", "dev", "tasks.schema.json");
            string state = o.State != null ? Normalize(o.State) : Path.Combine(mainRepo, ".claude", "taskstate.json");
            return new RepoLayout(toolRoot, mainRepo, tasks, specs, schema, state);
        }

        public static string FindRootFrom(string start)
        {
            string? dir = Normalize(start);
            while (dir != null)
            {
                string dotGit = Path.Combine(dir, ".git");
                if (Directory.Exists(dotGit) || File.Exists(dotGit))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            throw new TaskGraphException($"저장소 뿌리를 찾지 못했다({start} 위로 .git 이 없다). --repo <경로>로 알려 준다", TaskGraphException.UsageExitCode);
        }

        public static string FindMainRepo(string root)
        {
            ProcResult r = Proc.Run("git", new[] { "-C", root, "rev-parse", "--path-format=absolute", "--git-common-dir" });
            if (r.ExitCode != 0)
            {
                throw new TaskGraphException($"{root} 은 git 저장소가 아니다: {r.FirstErrorLine()}", TaskGraphException.UsageExitCode);
            }
            string common = Normalize(r.Stdout.Trim());
            string? parent = Path.GetDirectoryName(common);
            return parent ?? root;
        }

        public static string Normalize(string path)
        {
            string full = Path.GetFullPath(path);
            if (full.Length > 1 && (full.EndsWith('/') || full.EndsWith(Path.DirectorySeparatorChar)))
            {
                full = full.TrimEnd('/', Path.DirectorySeparatorChar);
            }
            return full;
        }
    }
}
