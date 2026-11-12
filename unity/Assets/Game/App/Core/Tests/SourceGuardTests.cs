#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Wizard.Unity.App.Core.Tests
{
    // 수락 조건 1·3의 정적 확인: 앱 계층과 입력원은 Time.timeScale 을 쓰지 않고, 키를 프레임 단위로 읽지 않으며, FixedUpdate 로 틱을 돌리지 않는다
    public sealed class SourceGuardTests
    {
        private static readonly string[] Folders = { "Game/App/Core", "Game/Input" };

        private static readonly Regex[] Forbidden =
        {
            new Regex(@"\btimeScale\b"),                             // 읽기·쓰기 모두(테스트 폴더는 보지 않는다)
            new Regex(@"\bGetKey(Down|Up)?\s*\("),
            new Regex(@"\bUnityEngine\.Input\s*\."),
            new Regex(@"\bvoid\s+FixedUpdate\s*\("),
            new Regex(@"\bTime\s*\.\s*(deltaTime|fixedDeltaTime|time|timeAsDouble|unscaledTime|frameCount)\b"),
        };

        [Test]
        public void AppCoreAndInput_DoNotUseFrameTimeOrFramePolledKeys()
        {
            var problems = new List<string>();
            int files = 0;
            foreach (string folder in Folders)
            {
                string root = Path.Combine(Application.dataPath, folder);
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(root.Length).Replace('\\', '/');
                    if (relative.Contains("/Tests/"))
                    {
                        continue;
                    }
                    files++;
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string code = StripComment(lines[i]);
                        foreach (Regex r in Forbidden)
                        {
                            if (r.IsMatch(code))
                            {
                                problems.Add(folder + relative + ":" + (i + 1) + ": " + lines[i].Trim());
                            }
                        }
                    }
                }
            }
            Assert.That(files, Is.GreaterThan(0));
            Assert.That(problems, Is.Empty);
        }

        private static string StripComment(string line)
        {
            int i = line.IndexOf("//", System.StringComparison.Ordinal);
            return i < 0 ? line : line.Substring(0, i);
        }
    }
}
