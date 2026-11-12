using System.Collections.Generic;
using System.IO;
using Wizard.DataBuild.Names;
using Wizard.TestKit;
using Xunit;

namespace Wizard.DataBuild.Tests.Names
{
    // data/names/names.ko.json 의 형식(T-35, CONTRACTS 5절): names --check 와 names --fix
    public sealed class NamesFileTests
    {
        private const string Skeleton = "{\n  \"~end\": \"\"\n}\n";

        private static string Lines(params string[] entries) => "{\n" + string.Join("\n", entries) + "\n}\n";

        [Fact]
        public void RepoFile_IsTheSkeletonOrLonger_AndPassesCheck()
        {
            string text = NamesFile.ReadUtf8(TestRepo.Path(NamesFile.RepoPath));
            Assert.Empty(NamesFile.Check(text));
            Assert.EndsWith("  \"~end\": \"\"\n}\n", text);
        }

        [Fact]
        public void Skeleton_PassesCheck_AndFixIsNoOp()
        {
            Assert.Empty(NamesFile.Check(Skeleton));
            var problems = new List<string>();
            Assert.Equal(Skeleton, NamesFile.Fix(Skeleton, problems));
            Assert.Empty(problems);
        }

        [Fact]
        public void Canonical_PassesCheck()
        {
            string text = Lines("  \"char.1\": \"제자 1\",", "  \"char.2\": \"제자 2\",", "  \"error.not_enough_gold\": \"골드가 모자라요\",", "  \"~end\": \"\"");
            Assert.Empty(NamesFile.Check(text));
            NamesFile f = NamesFile.FromText(text);
            Assert.Equal(3, f.Entries.Count);
            Assert.True(f.TryGet("char.1", out string v));
            Assert.Equal("제자 1", v);
            Assert.False(f.Contains("~end"));
        }

        [Theory]
        [InlineData("  \"char.2\": \"제자 2\",\n  \"char.1\": \"제자 1\",\n  \"~end\": \"\"", "서수 오름차순")]
        [InlineData("  \"char.1\": \"제자 1\",\n  \"char.1\": \"제자 1\",\n  \"~end\": \"\"", "두 번")]
        [InlineData("  \"char.1\": \"제자 1\",\n  \"char.2\": \"제자 2\"", "~end")]
        [InlineData("  \"~end\": \"\",\n  \"char.1\": \"제자 1\"", "~end")]
        [InlineData("  \"char.1\": \"제자 1\", \"char.2\": \"제자 2\",\n  \"~end\": \"\"", "한 줄에")]
        [InlineData("  \"char.1\": \"제자 1\"\n  \"~end\": \"\"", "쉼표")]
        [InlineData("  \"char.1\":\"제자 1\",\n  \"~end\": \"\"", "띄어쓰기")]
        [InlineData("    \"char.1\": \"제자 1\",\n  \"~end\": \"\"", "띄어쓰기")]
        [InlineData("  \"Char.1\": \"제자 1\",\n  \"~end\": \"\"", "id 문법")]
        [InlineData("  \"master\": \"느루\",\n  \"~end\": \"\"", "id 문법")]
        [InlineData("  \"~end\": \"x\"", "~end")]
        public void Check_FindsEachViolation(string body, string expected)
        {
            List<string> problems = NamesFile.Check("{\n" + body + "\n}\n");
            Assert.Contains(problems, p => p.Contains(expected));
        }

        [Fact]
        public void Check_RejectsWholeFileShapes()
        {
            Assert.NotEmpty(NamesFile.Check("{ \"~end\": \"\" }\n"));                 // 한 줄
            Assert.NotEmpty(NamesFile.Check("{\r\n  \"~end\": \"\"\r\n}\r\n"));       // CRLF
            Assert.NotEmpty(NamesFile.Check("{\n  \"~end\": \"\"\n}"));               // 끝 줄바꿈 없음
            Assert.NotEmpty(NamesFile.Check("{\n  \"~end\": \"\"\n}\n\n"));           // 끝 줄바꿈 둘
            Assert.NotEmpty(NamesFile.Check("\uFEFF{\n  \"~end\": \"\"\n}\n"));        // BOM
            Assert.NotEmpty(NamesFile.Check("[\n]\n"));                                // 객체가 아님
            Assert.NotEmpty(NamesFile.Check("{\n  \"a.b\": 1,\n  \"~end\": \"\"\n}\n")); // 값이 문자열이 아님
        }

        [Fact]
        public void Fix_SortsDedupesAndMovesTheMarker_AfterUnionMerge()
        {
            // union 병합이 양쪽 줄을 다 살린 꼴: 순서가 흐트러지고, 같은 줄이 두 번, 표지가 중간에, 쉼표·띄어쓰기가 틀림, CRLF
            string merged = "{\r\n  \"char.2\": \"제자 2\",\r\n  \"~end\": \"\",\r\n  \"block.fireball\":\"거대 화염구\"\r\n"
                + "    \"char.1\": \"제자 1\",\r\n  \"char.2\": \"제자 2\",\r\n\r\n  \"~end\": \"\"\r\n}";
            var problems = new List<string>();
            string? fixedText = NamesFile.Fix(merged, problems);
            Assert.Empty(problems);
            Assert.Equal(Lines("  \"block.fireball\": \"거대 화염구\",", "  \"char.1\": \"제자 1\",", "  \"char.2\": \"제자 2\",", "  \"~end\": \"\""), fixedText);
            Assert.Empty(NamesFile.Check(fixedText!));
            var again = new List<string>();
            Assert.Equal(fixedText, NamesFile.Fix(fixedText!, again));     // 고친 결과를 다시 고쳐도 같다
        }

        [Fact]
        public void Fix_KeepsEscapesAndKoreanAsWritten()
        {
            string text = Lines("  \"a.quote\": \"\\\"따옴표\\\" \\u0041\",", "  \"~end\": \"\"");
            var problems = new List<string>();
            Assert.Equal(text, NamesFile.Fix(text, problems));
            Assert.True(NamesFile.FromText(text).TryGet("a.quote", out string v));
            Assert.Equal("\"따옴표\" A", v);
        }

        [Theory]
        [InlineData("  \"char.1\": \"제자 1\",\n  \"char.1\": \"제자 일\",\n  \"~end\": \"\"", "값이 둘")]
        [InlineData("  \"char.1\": \"제자 1\", \"char.2\": \"제자 2\",\n  \"~end\": \"\"", "한 줄에")]
        [InlineData("  \"Char.1\": \"제자 1\",\n  \"~end\": \"\"", "id 문법")]
        [InlineData("  \"char.1\": \"\\q\",\n  \"~end\": \"\"", "이스케이프")]
        public void Fix_RefusesWhatOnlyAPersonCanDecide(string body, string expected)
        {
            var problems = new List<string>();
            Assert.Null(NamesFile.Fix("{\n" + body + "\n}\n", problems));
            Assert.Contains(problems, p => p.Contains(expected));
        }

        [Fact]
        public void Format_IsCanonical()
        {
            string text = NamesFile.Format(NamesFile.FromText(Lines("  \"b.x\": \"2\",", "  \"a.x\": \"1\",", "  \"~end\": \"\"")).Entries);
            Assert.Equal(Lines("  \"a.x\": \"1\",", "  \"b.x\": \"2\",", "  \"~end\": \"\""), text);
        }

        [Fact]
        public void Ordinal_NotCultureOrder()
        {
            // 서수 비교: '_'(0x5F) 는 소문자(0x61~)보다 앞, 숫자(0x30~)는 '_' 보다 앞
            string text = Lines("  \"a.b1\": \"x\",", "  \"a.b_c\": \"x\",", "  \"a.ba\": \"x\",", "  \"~end\": \"\"");
            Assert.Empty(NamesFile.Check(text));
        }

        [Fact]
        public void GitAttributes_UnionMergeForNamesFile()
        {
            string attrs = File.ReadAllText(TestRepo.Path(".gitattributes"));
            Assert.Contains("data/names/names.ko.json merge=union", attrs);
        }
    }
}
