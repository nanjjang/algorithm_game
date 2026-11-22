using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Wizard.TestKit;
using Xunit;

namespace Wizard.ViewLogic.Tests.Convert
{
    // M1-UE-02 수락 조건 1의 검색 검사(CONTRACTS 0절·13절, T-27): 코어 세 어셈블리(Core, Content, Sim) 밖에서 고정소수점의 Raw 를
    // 실수로 바꾸는(나누는) 곳은 Wizard.ViewLogic.FixConvert 하나뿐이다. Unity 쪽은 Raw 를 아예 만지지 않는다.
    // 저장소의 C# 전부(src/, server/, unity/Assets/, tools/, tests/)를 본다. 빌드 출력과 이 파일은 뺀다.
    // 이진 실수(float, double)로 바꾸는 곳은 어디서나 잡는다. 십진 실수(decimal)는 도구와 시험(tools/, tests/)에서만 넘긴다:
    // 데이터의 십진 값을 Raw 로 바꾸는 DataBuild 의 변환기와, 명세 표의 십진 값을 Raw 와 대조하는 시험(예: Wizard.Content.Tests 의
    // 강화 배율 표)은 값을 정확히 다루는 검산이고 표시 경로가 아니다(CLAUDE.md 5절: 데이터는 십진 그대로, DataBuild 가 정수로 바꾼다).
    // 출시 코드(src/, server/, unity/Assets/)에서는 decimal 도 잡는다.
    public sealed class FixConvertSearchTests
    {
        private const string FixConvertPath = "src/Wizard.ViewLogic/FixConvert.cs";
        private const string ThisFile = "tests/Wizard.ViewLogic.Tests/Convert/FixConvertSearchTests.cs";

        private static readonly string[] Roots = { "src", "server", "unity/Assets", "tools", "tests" };

        private static readonly string[] CorePrefixes = { "src/Wizard.Core/", "src/Wizard.Content/", "src/Wizard.Sim/" };

        private static readonly string[] ExactDecimalPrefixes = { "tools/", "tests/" };

        // 같은 줄에 Raw 와 실수형(형 이름, 실수 리터럴, 실수 접미사)이 같이 있다
        private static readonly Regex RawWord = new Regex(@"\.Raw\b");
        private static readonly Regex StringLiteral = new Regex(@"@""(?:[^""]|"""")*""|""(?:[^""\\]|\\.)*""");
        private static readonly Regex RealWord = new Regex(
            @"\b(float|double|decimal|Single|Double|Decimal)\b|\b\d+\.\d+|\b\d+(\.\d+)?[fFdDmM]\b");

        // decimal 을 넘기는 곳의 실수형: 이진 실수의 형 이름과 리터럴만(접미사 m 의 리터럴은 빼고)
        private static readonly Regex BinaryRealWord = new Regex(
            @"\b(float|double|Single|Double)\b|\b\d+\.\d+(?:[eE][+-]?\d+)?(?![\deEmM_])|\b\d+(\.\d+)?[fFdD]\b");

        // 2^32 로 나누는 실수 식, 2^-32 를 실수로 만드는 식, Raw 를 실수 단위로 바꾸는 이름
        private static readonly Regex[] RealScale =
        {
            new Regex(@"/\s*\(?\s*(\((float|double|decimal)\)\s*)?4_?294_?967_?296(\.\d*)?[fFdDmM]?\b"),
            new Regex(@"\bMath[F]?\s*\.\s*Pow\s*\(\s*2(\.0*)?[fFdD]?\s*,\s*-?\s*32(\.0*)?[fFdD]?\s*\)"),
            new Regex(@"\bRawTo(Unit|Float|Double|Real)\b"),
            new Regex(@"/\s*\(\s*(float|double|decimal)\s*\)\s*\(?\s*1L?\s*<<\s*32"),
        };

        [Fact]
        public void OnlyFixConvertTurnsRawIntoReal_OutsideTheCoreAssemblies()
        {
            string root = TestRepo.Root;
            var hits = new List<string>();
            int files = 0;
            foreach (string r in Roots)
            {
                string dir = Path.Combine(root, r);
                if (!Directory.Exists(dir))
                {
                    continue;
                }
                foreach (string file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    string rel = Relative(root, file);
                    if (IsBuildOutput(rel) || CorePrefixes.Any(p => rel.StartsWith(p, StringComparison.Ordinal)) || rel == ThisFile)
                    {
                        continue;
                    }
                    files++;
                    bool allowExactDecimal = ExactDecimalPrefixes.Any(p => rel.StartsWith(p, StringComparison.Ordinal));
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (IsRawToReal(lines[i], allowExactDecimal))
                        {
                            hits.Add(rel + ":" + (i + 1) + ": " + lines[i].Trim());
                        }
                    }
                }
            }
            Assert.True(files > 0);
            Assert.All(hits, h => Assert.StartsWith(FixConvertPath + ":", h, StringComparison.Ordinal));
            Assert.Contains(hits, h => h.StartsWith(FixConvertPath + ":", StringComparison.Ordinal));   // 검사가 FixConvert 를 실제로 찾는다
        }

        [Fact]
        public void UnityAssembliesDoNotTouchRawAtAll()
        {
            string root = TestRepo.Root;
            string dir = Path.Combine(root, "unity", "Assets");
            if (!Directory.Exists(dir))
            {
                return;   // unity/ 프로젝트를 저장소에서 뺐다. 없으면 검사할 것도 없다.
            }
            var hits = new List<string>();
            foreach (string file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (RawWord.IsMatch(StripComment(lines[i])))
                    {
                        hits.Add(Relative(root, file) + ":" + (i + 1));
                    }
                }
            }
            Assert.Empty(hits);
        }

        // 검사기 자체: 잡아야 할 줄과 지나가야 할 줄
        [Theory]
        [InlineData("float x = (float)(v.Raw * RawToUnit);", true)]
        [InlineData("double d = p.X.Raw / 4294967296.0;", true)]
        [InlineData("var f = v.Raw / (double)(1L << 32);", true)]
        [InlineData("float g = v.Raw * 2.3283064365386963E-10f;", true)]
        [InlineData("double s = Math.Pow(2, -32);", true)]
        [InlineData("decimal m = raw / 4294967296m;", true)]
        [InlineData("long r = a.Raw + b.Raw;", false)]
        [InlineData("long one = 4294967296L;", false)]
        [InlineData("double expected = Math.Sin(x) * 4294967296.0;", false)]
        [InlineData("float y = FixConvert.ToFloat(v);   // v.Raw 를 float 로", false)]
        [InlineData("var r = new Regex(@\"\\.Raw\\b\"); float f = 0f;", false)]
        [InlineData("string s = \"a.Raw / 2.0\";", false)]
        public void Scanner_FlagsRawToRealButNotIntegerOrRealToRaw(string line, bool flagged)
        {
            Assert.Equal(flagged, IsRawToReal(line, false));
        }

        // 도구와 시험(tools/, tests/)의 규칙: 이진 실수는 그대로 잡고 decimal 만 넘긴다
        [Theory]
        [InlineData("Assert.Equal(6.152788m, decimal.Round((decimal)v.Raw / OneRaw, 6));", false)]
        [InlineData("if (FixRaw((decimal)e.HopPermille / Units.PermillePerUnit) != FixMath.PowTable(PowBase.Chain07, 1).Raw)", false)]
        [InlineData("decimal m = raw / 4294967296m;", false)]
        [InlineData("double d = p.X.Raw / 4294967296.0;", true)]
        [InlineData("float x = (float)(v.Raw * RawToUnit);", true)]
        [InlineData("var g = v.Raw * 2.3283064365386963E-10;", true)]
        [InlineData("var h = v.Raw * 0.5;", true)]
        [InlineData("double s = Math.Pow(2, -32);", true)]
        [InlineData("long r = a.Raw + b.Raw;", false)]
        public void Scanner_InToolsAndTests_AllowsExactDecimalOnly(string line, bool flagged)
        {
            Assert.Equal(flagged, IsRawToReal(line, true));
        }

        private static bool IsRawToReal(string line, bool allowExactDecimal)
        {
            string code = StripComment(line);
            Regex real = allowExactDecimal ? BinaryRealWord : RealWord;
            if (RawWord.IsMatch(code) && real.IsMatch(code))
            {
                return true;
            }
            return RealScale.Any(r => IsRealScaleHit(r, code, allowExactDecimal));
        }

        private static bool IsRealScaleHit(Regex r, string code, bool allowExactDecimal)
        {
            foreach (Match m in r.Matches(code))
            {
                bool exactDecimal = m.Value.EndsWith("m", StringComparison.OrdinalIgnoreCase) || m.Value.Contains("decimal", StringComparison.Ordinal);
                if (!(allowExactDecimal && exactDecimal))
                {
                    return true;
                }
            }
            return false;
        }

        // 문자열 리터럴(검사 규칙을 담은 정규식 문자열 등)을 비운 뒤 줄 끝 주석을 뗀다
        private static string StripComment(string line)
        {
            string code = StringLiteral.Replace(line, "\"\"");
            int i = code.IndexOf("//", StringComparison.Ordinal);
            return i < 0 ? code : code.Substring(0, i);
        }

        private static bool IsBuildOutput(string rel) =>
            rel.Contains("/obj/", StringComparison.Ordinal) || rel.Contains("/bin/", StringComparison.Ordinal)
            || rel.StartsWith("unity/Library/", StringComparison.Ordinal);

        private static string Relative(string root, string file) => Path.GetRelativePath(root, file).Replace('\\', '/');
    }
}
