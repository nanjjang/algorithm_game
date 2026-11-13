#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Wizard.Unity.Presentation.Bus.Tests
{
    // 수락 3의 정적 확인(Presentation/Bus·Fx·Sfx·Camera):
    //   - Time.timeScale 과 Unity 의 프레임 시간을 쓰지 않는다(표시 시계는 부르는 쪽이 준다). 그래서 그림 정지가 틱을 멈출 길이 없다
    //   - 세계(SimWorld)·세션·틱 진행을 쥐지 않는다. Fix64 를 만지지 않는다(FixConvert 는 라우터가 한다)
    //   - 코어 난수기·System.Random·UnityEngine.Random 을 쓰지 않는다(흔들림은 DisplayRandom). 프리팹·Resources 를 읽지 않는다(T-32)
    public sealed class PresentationBusSourceGuardTests
    {
        private static readonly string[] Folders =
        {
            "Game/Presentation/Bus", "Game/Presentation/Fx", "Game/Presentation/Sfx", "Game/Presentation/Camera",
        };

        private static readonly Regex[] Forbidden =
        {
            new Regex(@"\btimeScale\b"),
            new Regex(@"\bTime\s*\.\s*\w+"),
            new Regex(@"\bvoid\s+(FixedUpdate|Update|LateUpdate)\s*\("),
            new Regex(@"\bSimWorld\b|\bITickSession\b|\bFixedTickDriver\b"),
            new Regex(@"\b(Step|SaveSnapshot|LoadSnapshot|ReadState|WriteState)\s*\("),
            new Regex(@"\.Raw\b|\bFix64\b|\bFixVec2\b"),
            new Regex(@"\bPcg32\b|\bRngStreams\b|\bnew\s+System\.Random\b|\bUnityEngine\.Random\b"),
            new Regex(@"\bResources\s*\.\s*Load|\bInstantiate\s*\("),
            new Regex(@"\bGetKey(Down|Up)?\s*\(|\bUnityEngine\.Input\s*\."),
        };

        [Test]
        public void BusFxSfxCamera_NeverTouchGameTimeOrTheWorld()
        {
            var problems = new List<string>();
            int files = 0;
            foreach (string folder in Folders)
            {
                string root = Path.Combine(Application.dataPath, folder);
                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (file.Replace('\\', '/').Contains("/Tests/"))
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
                                problems.Add(Path.GetFileName(file) + ":" + (i + 1) + ": " + lines[i].Trim());
                            }
                        }
                    }
                }
            }
            Assert.That(files, Is.GreaterThanOrEqualTo(Folders.Length));
            Assert.That(problems, Is.Empty);
        }

        private static string StripComment(string line)
        {
            int i = line.IndexOf("//", System.StringComparison.Ordinal);
            return i < 0 ? line : line.Substring(0, i);
        }
    }
}
