using System.Collections.Generic;
using System.Linq;
using Wizard.TestKit.Shared.Vectors;
using Xunit;

namespace Wizard.TestKit.SelfTests
{
    // 저장소 설정 파일의 규칙을 작은 가짜 명세에 적용해 본다(무엇을 잡고 무엇을 문제로 보는가).
    public sealed class SpecVectorScannerTests
    {
        private static SpecVectorScan ScanOne(string fileName, string text)
        {
            var files = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(fileName, text) };
            return SpecVectorScanner.Scan(files, TestRepo.LoadVectorConfig());
        }

        private static string[] Ids(SpecVectorScan s) => s.Vectors.Select(v => v.Id).ToArray();

        private static IEnumerable<string> OnlyFileProblems(SpecVectorScan s) =>
            s.Problems.Where(p => !p.StartsWith("spec ", System.StringComparison.Ordinal));   // 다른 명세 파일이 없다는 문제는 뺀다

        [Fact]
        public void TableRows_InsideVectorChapterSubsections_AreVectors()
        {
            string md = string.Join("\n",
                "## 14. 테스트 벡터",
                "| T-H98 | 절 밖 |",
                "### 14.1 열",
                "| ID | given | then |",
                "|---|---|---|",
                "| T-H01 | a | b |",
                "| `T-H02` | a | b |",
                "### 14.2 잠금",
                "| T-H03 | a | b |");
            SpecVectorScan s = ScanOne("01-x.md", md);
            Assert.Equal(new[] { "T-H01", "T-H02", "T-H03" }, Ids(s));
            Assert.Equal(new[] { "14.1", "14.1", "14.2" }, s.Vectors.Select(v => v.Section).ToArray());
            Assert.All(s.Vectors, v => Assert.True(v.InVectorChapter));
            Assert.Equal(6, s.Vectors[0].Line);
            // 절 밖의 T-H98 하나만 문제다(절 밖이라는 것과, 그래서 정의되지 않은 id 라는 것 두 줄로 보고된다)
            Assert.All(OnlyFileProblems(s), p => Assert.StartsWith("01-x.md:2: ", p));
            Assert.Contains(OnlyFileProblems(s), p => p.Contains("outside a ### subsection"));
        }

        // spec 11 16절·17.5절 꼴: 벡터 장 밖의 굵은 항목·표 행이 이 명세의 id 로 시작하면 벡터다.
        // ### 소절이 있으면 그 절, 없으면 장 번호가 절이다
        [Fact]
        public void IdShapedBoldItemsAndTableRows_OutsideTheVectorChapter_AreVectorsOfTheirSection()
        {
            string md = string.Join("\n",
                "## 13. 규칙",
                "| T-H99 | 장 밖의 표 행도 id 로 시작하면 정의다 |",
                "| C-01 | 꼴이 다른 표 행은 보통 표다 (T-H99) |",
                "- **굵은 글씨** 꼴이 다른 굵은 항목은 보통 글이다",
                "## 14. 테스트 벡터",
                "### 14.1 열",
                "| T-H01 | a | b |",
                "## 16. 오프라인",
                "- **T-H05** Given 소절 없는 장",
                "## 17. 연결점",
                "### 17.5 입자",
                "- **T-H06** Given 소절 안",
                "| T-H07~T-H08 | 범위 |",
                "1. 번호 항목은 장 밖에서는 벡터가 아니다");
            SpecVectorScan s = ScanOne("01-x.md", md);
            Assert.Equal(new[] { "T-H99", "T-H01", "T-H05", "T-H06", "T-H07", "T-H08" }, Ids(s));
            Assert.Equal(new[] { "13", "14.1", "16", "17.5", "17.5", "17.5" }, s.Vectors.Select(v => v.Section).ToArray());
            Assert.Equal(new[] { false, true, false, false, false, false }, s.Vectors.Select(v => v.InVectorChapter).ToArray());
            Assert.Equal(new[] { 2, 7, 9, 12, 13, 13 }, s.Vectors.Select(v => v.Line).ToArray());
            Assert.Empty(OnlyFileProblems(s));
        }

        [Fact]
        public void VectorOutsideTheVectorChapter_ThatDuplicatesOne_IsAProblem()
        {
            SpecVectorScan s = ScanOne("11-x.md", "## 15. 테스트 벡터\n### 15.1 가\n- **TV-11-001** Given\n## 16. 오프라인\n- **TV-11-001** Given 또\n");
            Assert.Single(s.Vectors);
            Assert.Contains(OnlyFileProblems(s), p => p.StartsWith("11-x.md:5: ", System.StringComparison.Ordinal) && p.Contains("appears twice"));
        }

        [Fact]
        public void VectorBeforeAnyNumberedChapter_IsAProblem()
        {
            SpecVectorScan s = ScanOne("11-x.md", "# 제목\n- **TV-11-009** Given 장 앞\n## 15. 테스트 벡터\n### 15.1 가\n- **TV-11-001** Given\n");
            Assert.Contains(OnlyFileProblems(s), p => p.Contains("'TV-11-009' is outside a numbered chapter"));
        }

        [Fact]
        public void StarAndRange_AreUnderstood()
        {
            string md = string.Join("\n",
                "## 18. 테스트 벡터",
                "### 18.1 가",
                "| T001 | a |",
                "| T002* | 임시 |",
                "| T003~T005 | 범위 |");
            SpecVectorScan s = ScanOne("06-x.md", md);
            Assert.Equal(new[] { "T001", "T002", "T003", "T004", "T005" }, Ids(s));
            Assert.Equal(new[] { false, true, false, false, false }, s.Vectors.Select(v => v.Provisional).ToArray());
            Assert.Empty(OnlyFileProblems(s));
        }

        [Fact]
        public void BoldAndNumberedItems_AreVectors()
        {
            SpecVectorScan s11 = ScanOne("11-x.md", "## 15. 테스트 벡터\n### 15.1 가\n- **TV-11-001** Given …\n- **TV-11-002** Given …\n");
            Assert.Equal(new[] { "TV-11-001", "TV-11-002" }, Ids(s11));
            SpecVectorScan s04 = ScanOne("04-x.md", "## 16. 테스트 벡터\n### 16.1 가\n1. given a\n2. given b\n   이어지는 줄\n");
            Assert.Equal(new[] { "N1", "N2" }, Ids(s04));
        }

        // spec 08 12.8절 꼴: 본문 줄의 괄호 속 id 와 끝 숫자만 적은 범위
        [Fact]
        public void ProseParentheses_DefineVectors_AndKnownIdsAreReferences()
        {
            string md = string.Join("\n",
                "## 12. 테스트 벡터",
                "### 12.8 가",
                "| TV-E-01 | a |",
                "빠른 쪽: 44 / 40 / 75 (TV-E-09~12).",
                "분포 (TV-E-15): 3칸 79%. 부적: 48% (TV-E-16). 비교는 TV-E-01 과 같다 (TV-E-01).",
                "민감도 (TV-E-60, sim/x.py): 그대로 (O2.6). (k=1.74) 다시 (TV-E-15).",
                "| TV-E-20 | 표는 본문 뒤에 와도 정의다 (TV-E-20) |");
            SpecVectorScan s = ScanOne("08-x.md", md);
            Assert.Equal(new[] { "TV-E-01", "TV-E-09", "TV-E-10", "TV-E-11", "TV-E-12", "TV-E-15", "TV-E-16", "TV-E-60", "TV-E-20" }, Ids(s));
            Assert.Equal(new[] { 3, 4, 4, 4, 4, 5, 5, 6, 7 }, s.Vectors.Select(v => v.Line).ToArray());
            Assert.All(s.Vectors, v => Assert.Equal("12.8", v.Section));
            Assert.Empty(OnlyFileProblems(s));
        }

        // 어느 규칙도 정의로 잡지 않은 id 꼴 낱말은 문제다(본문, 표의 다른 칸, 장 머리말 어디든)
        [Theory]
        [InlineData("본문에 괄호 없이 TV-E-15 를 적었다", "TV-E-15")]
        [InlineData("| TV-E-02 | 없는 벡터 TV-E-77 을 가리킨다 |", "TV-E-77")]
        [InlineData("- **TV-E-02** 굵은 항목의 몸에 적힌 (TV-E-31) 은 본문 규칙이 아니다", "TV-E-31")]
        public void IdTokens_NotDefinedByAnyRule_AreProblems(string line, string id)
        {
            SpecVectorScan s = ScanOne("08-x.md", "## 12. 테스트 벡터\n### 12.1 가\n| TV-E-01 | a |\n" + line + "\n");
            Assert.Contains(OnlyFileProblems(s), p => p.Contains("'" + id + "' appears in the vector chapter but no rule defines it"));
        }

        // 벡터 장 밖의 본문 괄호는 정의가 아니다. 정의된 벡터를 가리키면 참조로 넘어가고
        [Fact]
        public void IdTokens_OutsideTheVectorChapter_ThatReferToDefinedVectors_AreFine()
        {
            SpecVectorScan s = ScanOne("08-x.md", "## 11. 규칙\n본문 TV-E-01 (TV-E-01)\n## 12. 테스트 벡터\n### 12.1 가\n| TV-E-01 | a |\n");
            Assert.Equal(new[] { "TV-E-01" }, Ids(s));
            Assert.Empty(OnlyFileProblems(s));
        }

        // 정의되지 않은 벡터를 가리키면 문제다(번호 항목·본문 꼴로 장 밖에 적힌 정의를 조용히 놓치지 않는다)
        [Fact]
        public void IdTokens_OutsideTheVectorChapter_NotDefinedAnywhere_AreProblems()
        {
            SpecVectorScan s = ScanOne("08-x.md", "## 11. 규칙\n본문 TV-E-99 (TV-E-98)\n1. TV-E-97 Given 번호 항목\n## 12. 테스트 벡터\n### 12.1 가\n| TV-E-01 | a |\n");
            Assert.Equal(new[] { "TV-E-01" }, Ids(s));
            foreach (string id in new[] { "TV-E-99", "TV-E-98", "TV-E-97" })
            {
                Assert.Contains(OnlyFileProblems(s), p => p.Contains("'" + id + "' appears outside the vector chapter but no rule defines it"));
            }
        }

        [Fact]
        public void ProseVector_BeforeAnySubsection_IsAProblem()
        {
            SpecVectorScan s = ScanOne("08-x.md", "## 12. 테스트 벡터\n머리말 (TV-E-05)\n### 12.1 가\n| TV-E-01 | a |\n");
            Assert.Contains(OnlyFileProblems(s), p => p.Contains("'TV-E-05' is outside a ### subsection"));
        }

        [Fact]
        public void ProseRange_ThatCannotBeExpanded_IsAProblem()
        {
            SpecVectorScan s = ScanOne("08-x.md", "## 12. 테스트 벡터\n### 12.1 가\n| TV-E-01 | a |\n거꾸로 (TV-E-14~09)\n");
            Assert.Contains(OnlyFileProblems(s), p => p.Contains("cannot be expanded"));
        }

        [Theory]
        [InlineData("| T-H1 | 자릿수 틀림 |", "does not match")]
        [InlineData("| given | 머리 줄이 아닌 낯선 칸 |", "does not match")]
        [InlineData("| T-H01 / T-H02 | 두 id |", "unrecognized table row")]
        [InlineData("| T-H05~T-H03 | 거꾸로 된 범위 |", "cannot be expanded")]
        [InlineData("| T-H01~T-X03 | 앞머리가 다른 범위 |", "cannot be expanded")]
        [InlineData("1. 번호 항목은 spec 04 꼴이다", "does not match")]
        public void Unrecognized_Or_Malformed_VectorLines_AreProblems(string line, string expected)
        {
            SpecVectorScan s = ScanOne("01-x.md", "## 14. 테스트 벡터\n### 14.1 가\n| T-H09 | 정상 |\n" + line + "\n");
            Assert.Contains(OnlyFileProblems(s), p => p.Contains(expected));
        }

        [Fact]
        public void DuplicateIds_AreProblems()
        {
            SpecVectorScan s = ScanOne("01-x.md", "## 14. 테스트 벡터\n### 14.1 가\n| T-H01 | a |\n### 14.2 나\n| T-H01 | b |\n");
            Assert.Single(s.Vectors);
            Assert.Contains(OnlyFileProblems(s), p => p.Contains("appears twice"));
        }

        [Fact]
        public void SpecWithoutVectors_OrWithoutPattern_IsAProblem()
        {
            SpecVectorScan empty = ScanOne("01-x.md", "## 14. 테스트 벡터\n### 14.1 가\n본문만 있다\n");
            Assert.Contains(empty.Problems, p => p.Contains("no test vectors found"));
            SpecVectorScan unknown = ScanOne("15-new.md", "## 1. 테스트 벡터\n### 1.1 가\n| X01 | a |\n");
            Assert.Contains(unknown.Problems, p => p.Contains("no id pattern"));
        }

        [Fact]
        public void ConfiguredSpecs_WithoutFiles_AreProblems()
        {
            SpecVectorScan s = ScanOne("01-x.md", "## 14. 테스트 벡터\n### 14.1 가\n| T-H01 | a |\n");
            Assert.Equal(13, s.Problems.Count(p => p.Contains("no spec file was found")));
        }
    }
}
