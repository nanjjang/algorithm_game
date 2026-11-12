using System;
using System.IO;
using Wizard.TestKit;
using Wizard.ViewLogic.Presentation.Limits;
using Xunit;

namespace Wizard.ViewLogic.Tests.Presentation.Limits
{
    // 수락 조건: "표시용 난수는 코어 난수기를 쓰지 않는다"(CLAUDE.md 3절 끝, ARCHITECTURE 9절).
    // 연출 한도 폴더의 소스가 코어 난수기·System.Random 을 부르지 않는지, 표시 난수의 타입이 코어에 매이지 않았는지 본다.
    public sealed class DisplayRandomBoundaryTests
    {
        private static readonly string[] Forbidden = { "Pcg32", "RngStreams", "System.Random", "new Random(", "Guid.NewGuid", "SimWorld", "ISimView" };

        [Fact]
        public void LimitsSourcesDoNotTouchCoreRandomOrWorld()
        {
            string dir = TestRepo.Path("src/Wizard.ViewLogic/Presentation/Limits");
            string[] files = Directory.GetFiles(dir, "*.cs");
            Assert.NotEmpty(files);
            foreach (string f in files)
            {
                string text = WithoutLineComments(File.ReadAllText(f));   // 주석은 이유를 적느라 이름을 쓸 수 있다
                foreach (string bad in Forbidden)
                {
                    Assert.False(text.Contains(bad, StringComparison.Ordinal), Path.GetFileName(f) + " mentions " + bad);
                }
            }
        }

        private static string WithoutLineComments(string source)
        {
            var sb = new System.Text.StringBuilder();
            foreach (string line in source.Split('\n'))
            {
                int at = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(at < 0 ? line : line.Substring(0, at)).Append('\n');
            }
            return sb.ToString();
        }

        [Fact]
        public void LimitsTypesDoNotHoldCoreTypes()
        {
            foreach (Type t in typeof(ShakeAccumulator).Assembly.GetTypes())
            {
                if (t.Namespace != "Wizard.ViewLogic.Presentation.Limits")
                {
                    continue;
                }
                foreach (System.Reflection.FieldInfo fi in t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static
                                                                      | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                {
                    string ns = fi.FieldType.Namespace ?? "";
                    Assert.False(ns.StartsWith("Wizard.Sim", StringComparison.Ordinal) || ns.StartsWith("Wizard.Core", StringComparison.Ordinal),
                        t.Name + "." + fi.Name + " : " + fi.FieldType.FullName);
                }
            }
        }

        [Fact]
        public void DisplayRandomCoversTheUnitRangeAndSeedZeroIsNotStuck()
        {
            var r = new DisplayRandom(0u);
            uint first = r.NextUInt();
            Assert.NotEqual(0u, first);
            Assert.NotEqual(first, r.NextUInt());
            float min = 1f, max = 0f;
            for (int i = 0; i < 10000; i++)
            {
                float u = r.NextUnit();
                Assert.InRange(u, 0f, 0.99999994f);
                min = Math.Min(min, u);
                max = Math.Max(max, u);
                Assert.InRange(r.NextSigned(), -1f, 1f);
            }
            Assert.True(min < 0.01f && max > 0.99f);
        }
    }
}
