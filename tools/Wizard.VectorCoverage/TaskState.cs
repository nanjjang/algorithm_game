using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Wizard.VectorCoverage
{
    // 작업의 진행 상태(.claude/taskstate.json. git 이 추적하지 않는다. DECISIONS-TECH T-40)에서 done 인 작업을 읽는다.
    // 상태 파일은 메인 체크아웃에 있다: git 공통 폴더(--git-common-dir)의 부모/.claude/taskstate.json.
    // 쓰는 것은 tools/Wizard.TaskGraph 뿐이다. 여기서는 공유 잠금(FileShare.Read)으로 읽기만 한다.
    public static class TaskState
    {
        public static string DefaultPath(string root)
        {
            var psi = new ProcessStartInfo("git")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string a in new[] { "-C", root, "rev-parse", "--path-format=absolute", "--git-common-dir" })
            {
                psi.ArgumentList.Add(a);
            }
            psi.Environment["GIT_TERMINAL_PROMPT"] = "0";
            string stdout;
            string stderr;
            int code;
            try
            {
                using Process p = Process.Start(psi) ?? throw new CoverageException("git 을 띄우지 못했다", CoverageException.UsageExitCode);
                var err = p.StandardError.ReadToEndAsync();
                stdout = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                stderr = err.GetAwaiter().GetResult();
                code = p.ExitCode;
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                throw new CoverageException("git 을 띄우지 못했다: " + e.Message + " (--state <경로>로 상태 파일을 알려 줄 수 있다)", CoverageException.UsageExitCode);
            }
            if (code != 0)
            {
                throw new CoverageException(root + " 은 git 저장소가 아니다: " + stderr.Trim() + " (--state <경로>로 상태 파일을 알려 줄 수 있다)",
                    CoverageException.UsageExitCode);
            }
            string common = Path.GetFullPath(stdout.Trim()).TrimEnd('/');
            string main = Path.GetDirectoryName(common) ?? root;
            return Path.Combine(main, ".claude", "taskstate.json");
        }

        // 파일이 없으면 빈 집합(아직 끝난 작업이 없다)
        public static SortedSet<string> ReadDone(string path)
        {
            var done = new SortedSet<string>(StringComparer.Ordinal);
            if (!File.Exists(path))
            {
                return done;
            }
            string text;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(fs, new UTF8Encoding(false)))
            {
                text = reader.ReadToEnd();
            }
            return ParseDone(text, path);
        }

        public static SortedSet<string> ParseDone(string json, string source)
        {
            var done = new SortedSet<string>(StringComparer.Ordinal);
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("status", out JsonElement status) || status.ValueKind != JsonValueKind.Object)
                {
                    throw new CoverageException(source + ": 'status' 객체가 없다", CoverageException.UsageExitCode);
                }
                foreach (JsonProperty p in status.EnumerateObject())
                {
                    if (p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "done")
                    {
                        done.Add(p.Name);
                    }
                }
            }
            catch (JsonException e)
            {
                throw new CoverageException(source + ": JSON 이 아니다 — " + e.Message, CoverageException.UsageExitCode);
            }
            return done;
        }
    }
}
