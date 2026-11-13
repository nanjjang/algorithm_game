using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wizard.TaskGraph
{
    // DECISIONS-TECH.md 와 OPEN-QUESTIONS.md 의 줄 단위 3방향 병합(DECISIONS-TECH T-65 9번).
    // `git merge-file -p --diff3` 로 합치고(diff3 꼴은 충돌 덩어리를 쪼개지 않는다), 충돌 덩어리마다
    //   - 바탕 쪽이 비어 있으면(양쪽이 같은 자리에 줄을 더하기만 했다) main 쪽 묶음 전체 다음에 옮기는 쪽 묶음 전체를 둔다
    //   - 바탕 쪽에 줄이 있으면(있던 줄을 고쳤다) 풀지 않는다
    // git merge-file 은 파일만 읽는다(저장소·색인을 건드리지 않는다). 세 판은 workDir 아래 임시 파일로 쓴다.
    public static class TextMerge
    {
        public static TasksMerge.Outcome Merge(string label, string baseText, string mainText, string movingText, string workDir)
        {
            Directory.CreateDirectory(workDir);
            string stem = Path.Combine(workDir, Guid.NewGuid().ToString("N").Substring(0, 12));
            string mainFile = stem + ".main", baseFile = stem + ".base", movingFile = stem + ".moving";
            var utf8 = new UTF8Encoding(false);
            try
            {
                File.WriteAllText(mainFile, mainText, utf8);
                File.WriteAllText(baseFile, baseText, utf8);
                File.WriteAllText(movingFile, movingText, utf8);
                ProcResult r = Proc.Run("git", new[] { "merge-file", "-p", "--diff3", "-L", "main", "-L", "base", "-L", "moving", mainFile, baseFile, movingFile });
                if (r.ExitCode < 0 || r.ExitCode > 127)
                {
                    return new TasksMerge.Outcome(null, new[] { $"{label}: git merge-file 이 실패했다(종료 {r.ExitCode}): {r.FirstErrorLine()}" });
                }
                return Resolve(label, r.Stdout);
            }
            finally
            {
                foreach (string f in new[] { mainFile, baseFile, movingFile })
                {
                    try
                    {
                        File.Delete(f);
                    }
                    catch (IOException)
                    {
                    }
                }
            }
        }

        // diff3 꼴의 출력을 푼다. 표식 줄: "<<<<<<< main", "||||||| base", "=======", ">>>>>>> moving"
        public static TasksMerge.Outcome Resolve(string label, string diff3)
        {
            var conflicts = new List<string>();
            var output = new StringBuilder();
            string[] lines = diff3.Split('\n');
            int i = 0;
            int outLine = 0;
            void Emit(string l, bool newline)
            {
                output.Append(l);
                if (newline)
                {
                    output.Append('\n');
                }
                outLine++;
            }
            while (i < lines.Length)
            {
                string line = lines[i];
                if (!line.StartsWith("<<<<<<< main", StringComparison.Ordinal))
                {
                    Emit(line, i + 1 < lines.Length);
                    i++;
                    continue;
                }
                var ours = new List<string>();
                var bas = new List<string>();
                var theirs = new List<string>();
                List<string> cur = ours;
                i++;
                bool closed = false;
                while (i < lines.Length)
                {
                    string l = lines[i];
                    if (l.StartsWith("||||||| base", StringComparison.Ordinal) && cur == ours)
                    {
                        cur = bas;
                    }
                    else if (l == "=======" && cur == bas)
                    {
                        cur = theirs;
                    }
                    else if (l.StartsWith(">>>>>>> moving", StringComparison.Ordinal) && cur == theirs)
                    {
                        closed = true;
                        i++;
                        break;
                    }
                    else
                    {
                        cur.Add(l);
                    }
                    i++;
                }
                if (!closed)
                {
                    conflicts.Add($"{label}: 합친 결과의 {outLine + 1}줄에서 충돌 표식을 읽지 못했다");
                    break;
                }
                if (bas.Count > 0)
                {
                    string first = bas[0].Length > 60 ? bas[0].Substring(0, 60) + "…" : bas[0];
                    conflicts.Add($"{label}:{outLine + 1}: 있던 줄을 고친 덩어리가 충돌했다(바탕 쪽 {bas.Count}줄, 첫 줄 \"{first}\")");
                }
                foreach (string l in ours)
                {
                    Emit(l, true);
                }
                foreach (string l in theirs)
                {
                    Emit(l, true);
                }
            }
            return conflicts.Count > 0 ? new TasksMerge.Outcome(null, conflicts) : new TasksMerge.Outcome(output.ToString(), conflicts);
        }
    }
}
