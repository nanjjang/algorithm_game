using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Wizard.TaskGraph
{
    // taskgraph check-diff <id>: 브랜치(기본 task/<id>)가 main 에서 갈라진 뒤 커밋한 변경이 규칙 안인가(PIPELINE 4.4절, T-41).
    // 파일마다 처음 맞는 규칙 하나로 판정한다:
    //   1. Unity 가 만드는 파일(unity/Library/, unity/*.csproj, *.sln)         → 늘 실패(커밋하지 않는다)
    //   2. owned_paths 안                                                       → 허용
    //   3. Unity 가 스스로 고쳐 쓰는 파일(packages-lock.json, ProjectVersion.txt, ProjectSettings/*.asset) → 실패
    //   4. 다시 만드는 파일(src/*/Generated/*.g.cs, tests/golden/**/expected*.json) → 도구의 출력과 바이트가 같으면 허용
    //      골든을 바꿨으면 tests/golden/CHANGELOG.md 에 이 작업 id 가 든 줄을 더해야 한다
    //   5. 공유 추가 전용 파일                                                   → 줄 추가만(꼴이 정해진 파일은 더한 줄의 꼴도 본다)
    //      .meta 는 새 파일만, 그리고 MetaGen --check 가 통과해야 한다. tests/fixtures/ 와 Plugins 의 DLL 은 새 파일만
    //   6. 그 밖                                                                  → 실패(소유 파일 밖)
    public sealed class DiffChecker
    {
        private static readonly string[] UnityGenerated = { "unity/Library/**", "unity/*.csproj", "unity/*.sln", "unity/*.slnx" };
        private static readonly string[] UnitySelfRewritten =
        {
            "unity/Packages/packages-lock.json", "unity/ProjectSettings/ProjectVersion.txt", "unity/ProjectSettings/*.asset",
        };
        private static readonly string[] GoldenGlobs = { "tests/golden/**/expected*.json" };
        private static readonly string[] MetaGlobs = { "src/**/*.meta", "unity/Assets/**/*.meta" };
        private const string GoldenChangelog = "tests/golden/CHANGELOG.md";

        private static readonly Regex PackageVersionLine = new Regex("^\\s*<PackageVersion Include=\"[^\"]+\" Version=\"[^\"]+\"\\s*/>\\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex PackageReferenceLine = new Regex("^\\s*<PackageReference Include=\"[^\"]+\"(\\s+[A-Za-z]+=\"[^\"]*\")*\\s*/>\\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex DllEntryLine = new Regex("^\\s*\"[^\"]+\\.dll\",?\\s*$", RegexOptions.CultureInvariant);

        private readonly Git _git;
        private readonly string _scratchDir;
        private readonly IReadOnlyList<Regenerator> _regenerators;

        public DiffChecker(Git git, string scratchDir, IReadOnlyList<Regenerator> regenerators)
        {
            _git = git;
            _scratchDir = scratchDir;
            _regenerators = regenerators;
        }

        public DiffReport Check(TaskDef task, string baseRef, string headRef)
        {
            if (!_git.RefExists(headRef))
            {
                throw new TaskGraphException($"{task.Id}: '{headRef}' 이 없다(작업 브랜치를 만들었는가)");
            }
            if (!_git.RefExists(baseRef))
            {
                throw new TaskGraphException($"{task.Id}: 기준 '{baseRef}' 이 없다");
            }
            string head = _git.RunChecked("rev-parse", headRef + "^{commit}").Trim();
            string mergeBase = _git.RunChecked("merge-base", baseRef, head).Trim();
            var report = new DiffReport(task.Id, baseRef, headRef);
            List<(char Status, string Path)> changes = NameStatus(mergeBase, head);

            var pending = new Dictionary<Regenerator, List<string>>();
            var goldenChanged = new List<string>();
            foreach ((char status, string path) in changes)
            {
                Judge(task, status, path, mergeBase, head, report, pending, goldenChanged);
            }
            if (goldenChanged.Count > 0)
            {
                bool logged = AddedLines(mergeBase, head, GoldenChangelog).Any(l => l.Contains(task.Id, StringComparison.Ordinal));
                if (!logged)
                {
                    report.Fail(GoldenChangelog, $"골든을 다시 만들었으면({string.Join(", ", goldenChanged)}) 이 파일에 '{task.Id}' 가 든 줄(작업 id, 골든, 이유)을 더해야 한다");
                }
            }
            if (pending.Count > 0)
            {
                RunRegenerators(task.Id, head, pending, report);
            }
            report.ChangedCount = changes.Count;
            return report;
        }

        private void Judge(TaskDef task, char status, string path, string mergeBase, string head, DiffReport report,
            Dictionary<Regenerator, List<string>> pending, List<string> goldenChanged)
        {
            if (UnityGenerated.Any(g => PathPattern.Glob(g, path)))
            {
                report.Fail(path, "Unity 가 만드는 파일이 커밋에 들어 있다. 커밋하지 않는다");
                return;
            }
            string? owner = task.OwnedPaths.FirstOrDefault(p => PathPattern.Owns(p, path));
            if (owner != null)
            {
                report.Ok(path, $"소유({owner})");
                return;
            }
            if (UnitySelfRewritten.Any(g => PathPattern.Glob(g, path)))
            {
                report.Fail(path, "Unity 가 스스로 고쳐 쓰는 파일이 커밋에 들어 있다(PIPELINE 4.3). 되돌린다: command git -C $WT checkout main -- " + path);
                return;
            }
            Regenerator? regen = _regenerators.FirstOrDefault(r => r.Covers(path));
            bool isMeta = MetaGlobs.Any(g => PathPattern.Glob(g, path));
            if (regen != null && !isMeta)
            {
                if (status == 'D')
                {
                    report.Fail(path, "다시 만드는 파일을 지웠다");
                    return;
                }
                Queue(pending, regen, path);
                if (GoldenGlobs.Any(g => PathPattern.Glob(g, path)))
                {
                    goldenChanged.Add(path);
                }
                return;
            }
            if (isMeta)
            {
                if (status != 'A')
                {
                    report.Fail(path, "공유 파일 .meta 는 MetaGen 이 만든 새 파일만 더할 수 있다(이미 있는 .meta 를 고치거나 지웠다)");
                    return;
                }
                if (regen == null)
                {
                    report.Fail(path, "새 .meta 를 대조할 MetaGen 대조기가 없다");
                    return;
                }
                Queue(pending, regen, path);
                return;
            }
            if (PathPattern.Glob("unity/Assets/Plugins/**/*.dll", path))
            {
                NewFileOnly(status, path, report, "새 DLL. 계획에 없던 의존이므로 검토 승인이 필요하다(PIPELINE 4.4)");
                return;
            }
            if (PathPattern.Glob("tests/fixtures/**", path))
            {
                NewFileOnly(status, path, report, "tests/fixtures/ 의 새 파일");
                return;
            }
            if (path == "data/names/names.ko.json" || path == GoldenChangelog)
            {
                AddOnly(status, path, mergeBase, head, null, "", report);
                return;
            }
            if (path == "Directory.Packages.props")
            {
                AddOnly(status, path, mergeBase, head, PackageVersionLine, "패키지 버전 줄. 새 외부 의존은 검토 승인이 필요하다", report);
                return;
            }
            if (new[] { "src/**/*.csproj", "server/**/*.csproj", "tests/**/*.csproj", "tools/**/*.csproj" }.Any(g => PathPattern.Glob(g, path)))
            {
                AddOnly(status, path, mergeBase, head, PackageReferenceLine, "PackageReference 줄. 검토 승인이 필요하다(PIPELINE 4.4)", report);
                return;
            }
            if (PathPattern.Glob("unity/Assets/**/*.asmdef", path))
            {
                AddOnly(status, path, mergeBase, head, DllEntryLine, "precompiledReferences 의 DLL 줄", report);
                return;
            }
            report.Fail(path, "소유 파일 밖이다(owned_paths 에도, 공유 추가 전용 파일과 다시 만드는 파일에도 들지 않는다)");
        }

        private static void Queue(Dictionary<Regenerator, List<string>> pending, Regenerator r, string path)
        {
            if (!pending.TryGetValue(r, out List<string>? list))
            {
                list = new List<string>();
                pending[r] = list;
            }
            list.Add(path);
        }

        private static void NewFileOnly(char status, string path, DiffReport report, string what)
        {
            if (status == 'A')
            {
                report.Ok(path, what);
            }
            else
            {
                report.Fail(path, "공유 파일: 새 파일을 더하기만 할 수 있다(이미 있는 파일을 고치거나 지웠다)");
            }
        }

        private void AddOnly(char status, string path, string mergeBase, string head, Regex? lineShape, string what, DiffReport report)
        {
            if (status != 'M' && status != 'A')
            {
                report.Fail(path, "공유 추가 전용 파일을 지우거나 종류를 바꿨다");
                return;
            }
            (List<string> added, List<string> removed, bool binary) = LineDiff(mergeBase, head, path);
            if (binary)
            {
                report.Fail(path, "공유 추가 전용 파일인데 이진 변경이다");
                return;
            }
            // 줄을 지우고 같은 줄(또는 끝에 쉼표만 붙인 줄)을 다시 더한 것은 바뀐 것이 아니다(배열 끝에 항목을 더할 때)
            foreach (string r in removed)
            {
                int at = added.FindIndex(a => a == r || a.TrimEnd() == r.TrimEnd() + ",");
                if (at < 0)
                {
                    report.Fail(path, $"공유 추가 전용 파일의 줄을 지우거나 고쳤다: \"{r.Trim()}\"");
                    return;
                }
                added.RemoveAt(at);
            }
            if (lineShape != null)
            {
                string? bad = added.FirstOrDefault(a => a.Trim().Length > 0 && !lineShape.IsMatch(a));
                if (bad != null)
                {
                    report.Fail(path, $"더한 줄의 꼴이 이 파일에 허용된 것이 아니다({what}): \"{bad.Trim()}\"");
                    return;
                }
            }
            report.Ok(path, $"공유 추가 전용: 줄 {added.Count}개 추가" + (what.Length > 0 ? " — " + what : ""));
        }

        private List<(char, string)> NameStatus(string mergeBase, string head)
        {
            string raw = _git.RunChecked("diff", "--no-renames", "--no-ext-diff", "--name-status", "-z", mergeBase, head);
            string[] parts = raw.Split('\0');
            var list = new List<(char, string)>();
            for (int i = 0; i + 1 < parts.Length; i += 2)
            {
                if (parts[i].Length == 0)
                {
                    break;
                }
                list.Add((parts[i][0], parts[i + 1]));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Item2, b.Item2));
            return list;
        }

        private (List<string> Added, List<string> Removed, bool Binary) LineDiff(string mergeBase, string head, string path)
        {
            string raw = _git.RunChecked("diff", "--no-renames", "--no-ext-diff", "--no-textconv", "-U0", mergeBase, head, "--", path);
            var added = new List<string>();
            var removed = new List<string>();
            bool inHunk = false;
            bool binary = false;
            foreach (string l in raw.Split('\n'))
            {
                string line = l.TrimEnd('\r');
                if (line.StartsWith("Binary files ", StringComparison.Ordinal) || line.StartsWith("GIT binary patch", StringComparison.Ordinal))
                {
                    binary = true;
                }
                if (line.StartsWith("@@", StringComparison.Ordinal))
                {
                    inHunk = true;
                    continue;
                }
                if (!inHunk)
                {
                    continue;
                }
                if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    inHunk = false;
                    continue;
                }
                if (line.StartsWith("+", StringComparison.Ordinal))
                {
                    added.Add(line.Substring(1));
                }
                else if (line.StartsWith("-", StringComparison.Ordinal))
                {
                    removed.Add(line.Substring(1));
                }
            }
            return (added, removed, binary);
        }

        private List<string> AddedLines(string mergeBase, string head, string path)
        {
            return LineDiff(mergeBase, head, path).Added;
        }

        private void RunRegenerators(string id, string head, Dictionary<Regenerator, List<string>> pending, DiffReport report)
        {
            Directory.CreateDirectory(_scratchDir);
            string tmp = Path.Combine(_scratchDir, $"check-{id}-{Environment.ProcessId}");
            if (Directory.Exists(tmp))
            {
                RemoveWorktree(tmp);
            }
            _git.RunChecked("worktree", "add", "--detach", tmp, head);
            try
            {
                foreach (Regenerator r in _regenerators)
                {
                    if (!pending.TryGetValue(r, out List<string>? files))
                    {
                        continue;
                    }
                    if (r.CompareByGitStatus)
                    {
                        // 도구가 만들지 않는 파일이 남아서 "같다"로 보이지 않게, 대조할 파일을 먼저 지우고 도구가 다시 쓰게 한다
                        foreach (string f in files)
                        {
                            File.Delete(Path.Combine(tmp, f));
                        }
                    }
                    string? failure = null;
                    foreach (string[] cmd in r.Commands)
                    {
                        string[] args = cmd.Skip(1).Select(a => a.Replace("{root}", tmp, StringComparison.Ordinal)).ToArray();
                        string file = cmd[0].Replace("{root}", tmp, StringComparison.Ordinal);
                        ProcResult pr = Proc.Run(file, args, tmp);
                        if (pr.ExitCode != 0)
                        {
                            failure = $"{r.Name}: `{string.Join(" ", cmd.Select(c => c.Replace("{root}", "$WT", StringComparison.Ordinal)))}` 이 {pr.ExitCode} 으로 끝났다 — {pr.FirstErrorLine()}";
                            break;
                        }
                    }
                    if (failure != null)
                    {
                        foreach (string f in files)
                        {
                            report.Fail(f, "도구의 출력과 같은지 확인하지 못했다: " + failure);
                        }
                        continue;
                    }
                    if (!r.CompareByGitStatus)
                    {
                        foreach (string f in files)
                        {
                            report.Ok(f, $"다시 만드는 파일: {r.Name} 의 출력과 같다");
                        }
                        continue;
                    }
                    var args2 = new List<string> { "-C", tmp, "status", "--porcelain", "-z", "--" };
                    args2.AddRange(files);
                    ProcResult st = Proc.Run("git", args2);
                    if (st.ExitCode != 0)
                    {
                        foreach (string f in files)
                        {
                            report.Fail(f, "git status 가 실패했다: " + st.FirstErrorLine());
                        }
                        continue;
                    }
                    var dirty = new HashSet<string>(
                        st.Stdout.Split('\0').Where(s => s.Length > 3).Select(s => s.Substring(3)), StringComparer.Ordinal);
                    foreach (string f in files)
                    {
                        if (dirty.Contains(f))
                        {
                            report.Fail(f, $"커밋된 바이트가 {r.Name} 의 출력과 다르다. 도구로 다시 만들어 커밋한다");
                        }
                        else
                        {
                            report.Ok(f, $"다시 만드는 파일: {r.Name} 의 출력과 같다");
                        }
                    }
                }
            }
            finally
            {
                RemoveWorktree(tmp);
            }
        }

        private void RemoveWorktree(string tmp)
        {
            // 이 검사가 만든 임시 작업 트리만 지운다(도구가 쓴 출력이 남아 있으므로 --force)
            ProcResult r = _git.Run("worktree", "remove", "--force", tmp);
            if (r.ExitCode != 0 && Directory.Exists(tmp))
            {
                Directory.Delete(tmp, true);
            }
            _git.Run("worktree", "prune");
        }
    }
}
