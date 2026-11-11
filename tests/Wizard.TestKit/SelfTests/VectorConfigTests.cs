using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Wizard.TestKit.Shared.Vectors;
using Xunit;

namespace Wizard.TestKit.SelfTests
{
    // tests/vectors.config.json 과 SpecVectorScanner 가 명세 14개의 벡터 id 꼴을 모두 잡는지 본다(M0-TE-01 수락 조건).
    public sealed class VectorConfigTests
    {
        // 명세별 벡터 수. 구현 출력에서 베끼지 않았고, 스캐너처럼 "벡터 장"으로 자르지도 않았다. 출처:
        //   01: spec 01 14절 끝 "= 221개".  09: spec 09 "테스트 벡터 수: 162개".  13: DECISIONS-TECH T-31 "spec 13의 테스트 벡터 243개".
        //   나머지: 명세 파일 **전체**에서 정의 줄 머리 꼴을 grep 으로 센 값(장 구분 없음, 명세 5f68bff 판).
        //     표 꼴: grep -cE '^\| *`?<id 꼴>\*?`? *\|'  (01 221, 02 168, 03 169, 05 287, 06 257, 07 202, 08 204, 09 162, 10 218,
        //            12 257, 13 243, 14 177)
        //     굵은 항목 꼴: grep -cE '^- \*\*TV-11-[0-9]{3}\*\*'  → 11 은 166줄, 고유 id 166개(TV-11-001~166, 빈 번호 없음).
        //       그중 5개는 "## 15. 테스트 벡터" 장 밖에 있다: TV-11-165·166(16절, ### 소절 없음), TV-11-162~164(17.5절).
        //       벡터 장만 보던 이전 판은 161 로 세고 문제도 내지 않았다(검토 반려). 지금은 장 밖의 정의도 잡는다.
        //   표·굵은 항목 꼴로 셀 수 없는 것:
        //   03 은 표의 169줄 + 범위 줄 "S06~S10"(18.3절)의 5개 = 174.
        //   04 는 16절의 번호 항목 1~156(번호 항목은 장 밖에서는 벡터가 아니다).
        //   06 은 별표(*)가 붙은 [임시] 벡터 27개를 포함한 257(14절 머리말: "별표(*)는 [임시] 규칙에 기대는 벡터다").
        //   tools/check-tasks.mjs 의 추출기는 이 별표 27개와 범위 줄 5개를 놓친다(230, 169로 센다). 결과 보고에 올렸다.
        //   08 은 표의 204줄 + 12.8절 본문 괄호에만 적힌 10개(TV-E-09~14 의 6개, TV-E-15, TV-E-16, TV-E-50, TV-E-60) = 214.
        //   교차 확인: 벡터 장 밖에서 명세의 id 꼴이 나오는 줄을 node 로 따로 모아 보면(01·02·03·05·07·08·09 의 참조 줄과 11 의 5줄),
        //   정의 줄은 spec 11 의 5줄뿐이고 나머지는 모두 이미 정의된 id 를 가리키는 참조다. "Given" 이 든 장 밖 줄도 11 의 5줄뿐이다.
        public static readonly TheoryData<string, int> ExpectedCounts = new TheoryData<string, int>
        {
            { "01", 221 }, { "02", 168 }, { "03", 174 }, { "04", 156 }, { "05", 287 }, { "06", 257 }, { "07", 202 },
            { "08", 214 }, { "09", 162 }, { "10", 218 }, { "11", 166 }, { "12", 257 }, { "13", 243 }, { "14", 177 },
        };

        private static readonly Lazy<SpecVectorScan> Scan =
            new Lazy<SpecVectorScan>(() => SpecVectorScanner.ScanRepo(TestRepo.Root, TestRepo.LoadVectorConfig()));

        [Fact]
        public void Config_CoversExactlyTheFourteenSpecs()
        {
            VectorConfig config = TestRepo.LoadVectorConfig();
            string[] want = Enumerable.Range(1, 14).Select(i => i.ToString("D2", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(want, config.Specs.ToArray());
        }

        [Fact]
        public void Scan_OfRepositorySpecs_HasNoProblems()
        {
            Assert.True(Scan.Value.Problems.Count == 0, "problems:\n" + string.Join("\n", Scan.Value.Problems));
        }

        [Theory]
        [MemberData(nameof(ExpectedCounts))]
        public void Scan_CountsEveryVectorOfTheSpec(string spec, int expected)
        {
            Assert.Equal(expected, Scan.Value.InSpec(spec).Count);
        }

        [Fact]
        public void Scan_TotalIsTheSumOfTheSpecCounts()
        {
            int sum = 0;
            foreach (object[] row in ExpectedCounts)
            {
                sum += (int)row[1];
            }
            Assert.Equal(sum, Scan.Value.Vectors.Count);
        }

        [Fact]
        public void Scan_EveryIdMatchesItsSpecPattern_AndIsUniqueInItsSpec()
        {
            VectorConfig config = TestRepo.LoadVectorConfig();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (SpecVector v in Scan.Value.Vectors)
            {
                Assert.Matches(config.IdPatternFor(v.Spec)!, v.Id);
                Assert.True(seen.Add(v.Key), "duplicate " + v);
                // 절은 "N.M". 벡터 장 밖에서 ### 소절 없는 장 바로 아래에 정의된 벡터만 장 번호 "N"
                Assert.Matches(v.InVectorChapter ? "^[0-9]+\\.[0-9]+$" : "^[0-9]+(?:\\.[0-9]+)?$", v.Section);
            }
        }

        // spec 06 14절: 별표가 붙은 id 는 별표를 뺀 id 로 잡히고 [임시] 표시가 붙는다
        [Fact]
        public void Scan_StarredIds_AreCapturedAsProvisional()
        {
            SpecVector? v = Scan.Value.Find("06", "T023");
            Assert.NotNull(v);
            Assert.True(v!.Provisional);
            Assert.Equal(27, Scan.Value.InSpec("06").Count(x => x.Provisional));
            Assert.False(Scan.Value.Find("06", "T001")!.Provisional);
            Assert.DoesNotContain(Scan.Value.Vectors, x => x.Id.Contains('*'));
        }

        // spec 03 18.3절: "S06~S10" 은 다섯 벡터다
        [Fact]
        public void Scan_RangeRow_IsExpandedOneByOne()
        {
            string[] ids = Scan.Value.InSpec("03").Where(v => v.Id.StartsWith("S", StringComparison.Ordinal)).Select(v => v.Id).ToArray();
            Assert.Equal(new[] { "S01", "S02", "S03", "S04", "S05", "S06", "S07", "S08", "S09", "S10", "S11", "S12", "S13" }, ids);
            Assert.All(Scan.Value.InSpec("03").Where(v => v.Id.StartsWith("S", StringComparison.Ordinal)), v => Assert.Equal("18.3", v.Section));
        }

        // spec 11: 굵은 항목 166개(TV-11-001~166)가 빈 번호 없이 다 잡히고, 벡터 장 밖의 5개는 그 자리의 절로 잡힌다
        [Fact]
        public void Scan_Spec11_HasEveryBoldItemOfTheFile_IncludingThoseOutsideTheVectorChapter()
        {
            string[] want = Enumerable.Range(1, 166).Select(i => "TV-11-" + i.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.Equal(want, Scan.Value.InSpec("11").Select(v => v.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray());
            var outside = Scan.Value.InSpec("11").Where(v => !v.InVectorChapter).OrderBy(v => v.Id, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "TV-11-162", "TV-11-163", "TV-11-164", "TV-11-165", "TV-11-166" }, outside.Select(v => v.Id).ToArray());
            Assert.Equal(new[] { "17.5", "17.5", "17.5", "16", "16" }, outside.Select(v => v.Section).ToArray());
            Assert.All(outside, v => Assert.False(v.Provisional));
        }

        // 벡터 장 밖에 정의된 벡터는 spec 11 의 5개뿐이다. 다른 명세의 장 밖 id 는 모두 참조다
        [Fact]
        public void Scan_VectorsOutsideTheVectorChapter_AreOnlyTheFiveOfSpec11()
        {
            string[] keys = Scan.Value.Vectors.Where(v => !v.InVectorChapter).Select(v => v.Key).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            Assert.Equal(new[] { "S11:TV-11-162", "S11:TV-11-163", "S11:TV-11-164", "S11:TV-11-165", "S11:TV-11-166" }, keys);
        }

        // spec 08 12.8절: 본문 줄의 괄호에만 적힌 벡터("(TV-E-09~14)", "(TV-E-15)", "(TV-E-60, …)")도 벡터다
        [Fact]
        public void Scan_Spec08ProseVectors_AreCaptured()
        {
            string[] want = { "TV-E-09", "TV-E-10", "TV-E-11", "TV-E-12", "TV-E-13", "TV-E-14", "TV-E-15", "TV-E-16", "TV-E-50", "TV-E-60" };
            foreach (string id in want)
            {
                SpecVector? v = Scan.Value.Find("08", id);
                Assert.True(v != null, id + " is missing");
                Assert.Equal("12.8", v!.Section);
                Assert.False(v.Provisional);
            }
            string[] e = Scan.Value.InSpec("08").Where(v => v.Id.StartsWith("TV-E-", StringComparison.Ordinal)).Select(v => v.Id).ToArray();
            Assert.Equal(e.OrderBy(x => x, StringComparer.Ordinal).ToArray(), e);   // 줄 순서 = 번호 순서
            // 표: 01~08, 20~28, 30~32, 40~44 (8+9+3+5 = 25줄, `grep -c '^| TV-E-'`). 본문: 09~16, 50, 60 (10개)
            Assert.Equal(35, e.Length);
        }

        // 본문 규칙을 끄면 같은 10개가 "정의되지 않은 id" 문제로 드러나야 한다(규칙이 놓친 벡터를 조용히 넘기지 않는다)
        [Fact]
        public void Scan_WithoutProseRule_ReportsTheSpec08ProseIdsAsProblems()
        {
            string json = System.IO.File.ReadAllText(System.IO.Path.Combine(TestRepo.Root, VectorConfig.DefaultPath));
            JObject o = JObject.Parse(json);
            o["proseItem"] = "(?!)(?<id>x)(?<to>y)";   // 이름 있는 묶음은 두고 아무것에도 맞지 않게
            VectorConfig crippled = VectorConfig.Parse(o.ToString());
            SpecVectorScan s = SpecVectorScanner.ScanRepo(TestRepo.Root, crippled);
            Assert.Equal(204, s.InSpec("08").Count);
            foreach (string id in new[] { "TV-E-09", "TV-E-15", "TV-E-16", "TV-E-50", "TV-E-60" })
            {
                Assert.Contains(s.Problems, p => p.StartsWith("08-", StringComparison.Ordinal) && p.Contains("'" + id + "' appears in the vector chapter"));
            }
        }

        // 꼴마다 하나씩: 표의 첫 칸, 굵은 항목(spec 11), 번호 항목(spec 04 → N), 소문자 꼬리(spec 03 B09c)
        [Theory]
        [InlineData("01", "T-H04")]
        [InlineData("02", "TV-J06")]
        [InlineData("03", "TV-A117")]
        [InlineData("03", "B09c")]
        [InlineData("04", "N71")]
        [InlineData("04", "N156")]
        [InlineData("05", "TV-R1-01")]
        [InlineData("05", "TV-MISC-07")]
        [InlineData("08", "TV-MK-22")]
        [InlineData("10", "TV-N32")]
        [InlineData("11", "TV-11-017")]
        [InlineData("11", "TV-11-161")]
        [InlineData("11", "TV-11-166")]
        [InlineData("12", "T-AUTHZ-05")]
        [InlineData("13", "TV-243")]
        [InlineData("14", "TV-238")]
        public void Scan_FindsRepresentativeIdOfEachShape(string spec, string id)
        {
            Assert.NotNull(Scan.Value.Find(spec, id));
        }

        [Fact]
        public void Scan_Spec04NumberedItems_RunFromOneWithoutGaps()
        {
            string[] ids = Scan.Value.InSpec("04").Select(v => v.Id).ToArray();
            Assert.Equal(Enumerable.Range(1, 156).Select(i => "N" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray(), ids);
        }

        // 벡터 이름 도우미와 설정의 testName 정규식이 모든 벡터 id 에서 같은 답을 낸다
        [Fact]
        public void TestNames_RoundTripForEveryVector_WithHelperAndConfigRegex()
        {
            Regex configRx = TestRepo.LoadVectorConfig().TestName;
            foreach (SpecVector v in Scan.Value.Vectors)
            {
                string name = VectorNames.TestName(v.Spec, v.Id, "Description_1");
                Assert.True(VectorNames.TryParseTestName(name, out string spec, out string id), name);
                Assert.Equal(v.Spec, spec);
                Assert.Equal(v.Id, id);
                Assert.True(VectorNames.TryParseTestName(configRx, name, out string spec2, out string id2), name);
                Assert.Equal(v.Spec, spec2);
                Assert.Equal(v.Id, id2);
                Assert.True(VectorNames.TryParseTestName(VectorNames.TestPrefix(v.Spec, v.Id), out _, out string id3));
                Assert.Equal(v.Id, id3);
            }
        }

        [Fact]
        public void TestNamePattern_InConfig_IsTheHelpersPattern()
        {
            Assert.Equal(VectorNames.TestNamePattern, TestRepo.LoadVectorConfig().TestName.ToString());
        }
    }
}
