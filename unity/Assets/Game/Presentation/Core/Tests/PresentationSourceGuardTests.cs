#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Wizard.Unity.Presentation.Core.Tests
{
    // 수락 조건 1·3의 정적 확인(Presentation 아래 이 작업의 폴더와 UnityConvert):
    //   - Fix64 → float 를 하지 않는다(Raw·Fix64 실수 변환이 없다. FixConvert 만 한다). System.Numerics.Vector2 는 UnityConvert 로만 옮긴다
    //   - 세계(SimWorld)를 쥐지 않는다. 읽는 것은 ISimView·표시 이벤트뿐이다. 시제품의 읽기 문도 쓰지 않는다(이름조차 없다)
    //   - Time.timeScale·프레임 시간·FixedUpdate·물리·키 폴링을 쓰지 않는다. 프리팹·Resources 를 읽지 않는다(T-32)
    public sealed class PresentationSourceGuardTests
    {
        private static readonly string[] Folders = { "Game/Presentation/Core", "Game/Presentation/World/Basic" };
        private static readonly string[] Files = { "Game/Presentation/UnityConvert.cs" };

        private static readonly Regex[] Forbidden =
        {
            new Regex(@"\.Raw\b"),
            new Regex(@"\bFix64\b"),
            new Regex(@"\bFixVec2\b"),
            new Regex(@"\bSimWorld\b"),
            new Regex(@"\bWizard\.Sim\.Proto\b|\bProto[A-Z]\w*"),
            new Regex(@"\b(Step|SaveSnapshot|LoadSnapshot|ReadState|WriteState)\s*\("),
            new Regex(@"\btimeScale\b"),
            new Regex(@"\bTime\s*\.\s*(deltaTime|fixedDeltaTime|time|timeAsDouble|unscaledTime|unscaledDeltaTime|frameCount)\b"),
            new Regex(@"\bvoid\s+(FixedUpdate|Update|LateUpdate)\s*\("),
            new Regex(@"\b(Physics2D|Physics|Rigidbody2D|Rigidbody|Collider2D)\b"),
            new Regex(@"\bGetKey(Down|Up)?\s*\("),
            new Regex(@"\bUnityEngine\.Input\s*\."),
            new Regex(@"\bResources\s*\.\s*Load"),
            new Regex(@"\bInstantiate\s*\("),
            new Regex(@"\bnew\s+System\.Random\b|\bUnityEngine\.Random\b"),
        };

        [Test]
        public void PresentationCoreAndBasicWorld_OnlyReadTheViewAndConvertNothingThemselves()
        {
            var problems = new List<string>();
            int files = 0;
            var paths = new List<string>();
            foreach (string folder in Folders)
            {
                string root = Path.Combine(Application.dataPath, folder);
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (!file.Replace('\\', '/').Contains("/Tests/"))
                    {
                        paths.Add(file);
                    }
                }
            }
            foreach (string f in Files)
            {
                paths.Add(Path.Combine(Application.dataPath, f));
            }
            foreach (string file in paths)
            {
                files++;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    string code = StripComment(lines[i]);
                    foreach (Regex r in Forbidden)
                    {
                        if (r.IsMatch(code))
                        {
                            problems.Add(Path.GetFileName(file) + ":" + (i + 1) + ": " + lines[i].Trim());
                        }
                    }
                }
            }
            Assert.That(files, Is.GreaterThan(Files.Length));
            Assert.That(problems, Is.Empty);
        }

        private static string StripComment(string line)
        {
            int i = line.IndexOf("//", System.StringComparison.Ordinal);
            return i < 0 ? line : line.Substring(0, i);
        }
    }
}
