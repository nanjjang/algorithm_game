using System.Collections.Generic;
using Wizard.DataBuild.Names;
using Wizard.TestKit;
using Xunit;

namespace Wizard.DataBuild.Tests.Names
{
    // tools/ci/names-scan.sh 의 몸통(databuild names --scan): 제품 코드의 한글 리터럴, 작업용 이름의 로마자 표기
    public sealed class NamesScanTests
    {
        private static List<string> Hangul(string code)
        {
            var p = new List<string>();
            NamesScan.ScanHangulLiterals("src/X/A.cs", code, p);
            return p;
        }

        [Theory]
        [InlineData("var s = \"제자 1\";")]
        [InlineData("var s = @\"여러\n줄\";")]
        [InlineData("var s = $\"{n}개\";")]
        [InlineData("var s = $@\"{n}개\";")]
        [InlineData("var s = @$\"{n}개\";")]
        [InlineData("var s = \"\"\"\n원시 문자열\n\"\"\";")]
        [InlineData("var c = '가';")]
        [InlineData("var s = \"\\uAC00\";")]
        [InlineData("var s = \"\\xAC00\";")]
        [InlineData("var c = '\\uD55C';")]
        [InlineData("var s = $\"{(ok ? \"예\" : \"no\")}\";")]
        [InlineData("var s = \"ㄱ\";")]
        public void HangulLiteral_IsFound(string code)
        {
            List<string> p = Hangul(code);
            Assert.Single(p);
            Assert.StartsWith("src/X/A.cs:1: 이름 표 밖 한글 리터럴", p[0]);
        }

        [Theory]
        [InlineData("// 한글 주석 \"따옴표\"\nvar s = \"ok\";")]
        [InlineData("/* 한글\n \"주석\" */ var s = \"ok\";")]
        [InlineData("/// <summary>한글 문서 주석</summary>\nvoid F() { }")]
        [InlineData("#region 한글 구역\n#endregion")]
        [InlineData("var s = \"// \"; // 한글")]
        [InlineData("var s = @\"a\"\"b\"; // 한글")]
        [InlineData("var s = \"a\\\"b\"; // \"한글\"")]
        [InlineData("var s = $\"{{a}}\"; // 한글")]
        [InlineData("var x = '\"'; // \"한글\"")]
        [InlineData("var key = \"char.1\";")]
        public void NotALiteral_OrNotHangul_IsNotFound(string code)
        {
            Assert.Empty(Hangul(code));
        }

        [Fact]
        public void LineNumber_IsReported()
        {
            List<string> p = Hangul("namespace A\n{\n    // 주석\n    class B { string s = \"마력탄\"; }\n}\n");
            Assert.Single(p);
            Assert.StartsWith("src/X/A.cs:4:", p[0]);
        }

        [Theory]
        [InlineData("DalguHeat", "dalgu")]
        [InlineData("fac_nuru_chair", "nuru")]
        [InlineData("NuruChair", "nuru")]
        [InlineData("nuruchair", "nuru")]
        [InlineData("chorongLamp", "chorong")]
        [InlineData("wands/galpi.md", "galpi")]
        [InlineData("SEONGE_ICE", "seonge")]
        [InlineData("geumeum2", "geumeum")]
        [InlineData("place.handeot", "handeot")]
        [InlineData("dolmadangGate", "dolmadang")]
        [InlineData("cheotmodak_camp", "cheotmodak")]
        public void WorkingName_IsFound(string text, string name)
        {
            Assert.Equal(name, NamesScan.FindWorkingName(text));
        }

        [Theory]
        [InlineData("MenuRunner")]
        [InlineData("menu_runner")]
        [InlineData("char.1")]
        [InlineData("BigCampfire")]
        [InlineData("facility.master_chair")]
        public void InnocentWord_IsNotFound(string text)
        {
            Assert.Null(NamesScan.FindWorkingName(text));
        }

        [Fact]
        public void Romanized_InCodeIsFound_InCommentsAndArtPathsIsNot()
        {
            var p = new List<string>();
            NamesScan.ScanRomanizedContent("src/X/A.cs",
                "// 문서 wands/dalgu.md 참고\nvar a = \"art/production/players/dalgu/body.png\";\nint nuruChair = 1;\n", p);
            Assert.Single(p);
            Assert.StartsWith("src/X/A.cs:3:", p[0]);
        }

        [Fact]
        public void Romanized_InJsonKeyIsFound_ArtPathIsNot()
        {
            var p = new List<string>();
            NamesScan.ScanRomanizedContent("data/sim/x.json", "{\n  \"sprite\": \"art/production/players/chorong/body.png\",\n  \"fac_nuru_chair\": 1\n}\n", p);
            Assert.Single(p);
            Assert.StartsWith("data/sim/x.json:3:", p[0]);
        }

        [Fact]
        public void Romanized_InSqlCommentIsNotFound()
        {
            var p = new List<string>();
            NamesScan.ScanRomanizedContent("server/migrations/0010.sql", "-- nuru 의자\nCREATE TABLE t (galpi_x int);\n", p);
            Assert.Single(p);
            Assert.StartsWith("server/migrations/0010.sql:2:", p[0]);
        }

        [Fact]
        public void Run_OnTempRepo_ScopesAndExceptions()
        {
            using var repo = new TempRepo();
            repo.Write("src/Wizard.X/Bad.cs", "class Bad { string s = \"제자\"; }\n");
            repo.Write("server/Wizard.Y/Bad.cs", "class Bad { char c = '한'; }\n");
            repo.Write("unity/Assets/Game/UI/Bad.cs", "class Bad { string s = \"버튼\"; }\n");
            repo.Write("tests/Wizard.X.Tests/Ok.cs", "class Ok { string s = \"테스트는 보지 않는다\"; }\n");
            repo.Write("unity/Assets/Other/Ok.cs", "class Ok { string s = \"Game 밖은 보지 않는다\"; }\n");
            repo.Write("src/Wizard.X/DalguThing.cs", "class Ok { }\n");
            repo.Write("data/presentation/asset_aliases.json", "{ \"char.1\": \"dalgu\" }\n");
            repo.Write("data/presentation/other.json", "{ \"char.1\": \"dalgu\" }\n");
            repo.Write("unity/Assets/Game/UI/dalgu_view.cs", "class Ok { }\n");   // unity/ 는 로마자 검사 범위가 아니다
            List<string> p = NamesScan.Run(repo.Root);
            Assert.Equal(5, p.Count);
            Assert.StartsWith("data/presentation/other.json:1:", p[0]);
            Assert.StartsWith("server/Wizard.Y/Bad.cs:1: 이름 표 밖 한글", p[1]);
            Assert.StartsWith("src/Wizard.X/Bad.cs:1: 이름 표 밖 한글", p[2]);
            Assert.StartsWith("src/Wizard.X/DalguThing.cs: 파일·폴더 이름", p[3]);
            Assert.StartsWith("unity/Assets/Game/UI/Bad.cs:1: 이름 표 밖 한글", p[4]);
        }

        [Fact]
        public void Repo_HasNoFindings()
        {
            List<string> p = NamesScan.Run(TestRepo.Root);
            Assert.True(p.Count == 0, string.Join("\n", p));
        }
    }
}
