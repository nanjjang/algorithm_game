using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.TestKit.Shared.Vectors
{
    // tests/vectors.config.json: 명세에서 벡터 id 를 모으는 규칙과 테스트 이름 규칙(PIPELINE 7.1 G1, CLAUDE.md 8절).
    // 모으는 규칙(SpecVectorScanner):
    //   "## N. …테스트 벡터" 장 안의 "### N.M" 절에서, 표 줄의 첫 칸, "- **id**" 항목, "71. …" 번호 항목(→ N71)을 벡터로 본다.
    //   표의 첫 칸에 별표가 붙은 id(T023*)는 [임시] 규칙에 기대는 벡터이고 id 는 별표를 뺀 것이다(spec 06 14절).
    //   "S06~S10" 처럼 범위로 적힌 칸은 하나씩 펼친다(spec 03 18.3절).
    //   위 셋에 맞지 않는 본문 줄에서는 괄호 안에 적힌 id("(TV-E-15)", "(TV-E-09~14)", "(TV-E-60, …)")를 벡터로 본다(spec 08 12.8절).
    //   단, 같은 명세의 표·항목이 이미 정의한 id 는 참조로 본다. 본문에서 같은 id 가 다시 나오면 처음 것이 정의다.
    //   id 는 명세별 꼴(specs.<번호>.id)에 맞아야 한다. 맞지 않거나, 알아볼 수 없는 표 줄이 있으면 문제로 보고한다.
    //   벡터 장 밖(다른 "## N." 장)에서는 표 줄의 첫 칸이나 "- **id**" 항목이 그 명세의 id 꼴일 때만 벡터로 본다(spec 11 16절·17.5절의
    //   TV-11-162~166). 절은 그 자리의 "### N.M", 소절이 없는 장이면 장 번호 "N"(chapter 의 ch 묶음)이다. 번호 항목과 본문 괄호는
    //   장 밖에서는 벡터가 아니다. 이렇게 잡힌 벡터는 SpecVector.InVectorChapter 가 false 다.
    //   명세의 어느 줄에든(벡터 장 안이든 밖이든) 그 명세의 id 꼴인 낱말(idToken)이 있는데 정의로 잡히지 않았으면 문제로 보고한다
    //   (빠짐을 조용히 넘기지 않는다).
    public sealed class VectorConfig
    {
        public const int FormatVersion = 1;
        public const string DefaultPath = "tests/vectors.config.json";

        private VectorConfig(string specDir, Regex specFile, Regex chapter, Regex vectorChapter, Regex subsection, Regex tableRow,
                             Regex tableIgnore, Regex tableId, Regex boldItem, Regex numberedItem, string numberedPrefix, Regex proseItem,
                             Regex idToken, Regex testName, IReadOnlyList<string> specs, IReadOnlyList<Regex> specIds)
        {
            SpecDir = specDir;
            SpecFile = specFile;
            Chapter = chapter;
            VectorChapter = vectorChapter;
            Subsection = subsection;
            TableRow = tableRow;
            TableIgnore = tableIgnore;
            TableId = tableId;
            BoldItem = boldItem;
            NumberedItem = numberedItem;
            NumberedPrefix = numberedPrefix;
            ProseItem = proseItem;
            IdToken = idToken;
            TestName = testName;
            Specs = specs;
            SpecIds = specIds;
        }

        // 저장소 기준 상대 경로
        public string SpecDir { get; }
        public Regex SpecFile { get; }
        // "## " 장 머리. ch 묶음은 장 번호("## 16. …" → "16"), 번호가 없는 장이면 묶음이 비어 있다
        public Regex Chapter { get; }
        public Regex VectorChapter { get; }
        public Regex Subsection { get; }
        public Regex TableRow { get; }
        public Regex TableIgnore { get; }
        public Regex TableId { get; }
        public Regex BoldItem { get; }
        public Regex NumberedItem { get; }
        public string NumberedPrefix { get; }

        // 본문 줄의 괄호 속 벡터 정의(여러 번 맞을 수 있다). to 는 끝 id 나 끝 숫자("TV-E-09~14" 의 14)
        public Regex ProseItem { get; }

        // 정의되지 않은 id 를 찾을 때 쓰는 낱말 꼴(명세 파일 전체). 낱말이 명세의 id 꼴에 통째로 맞으면 id 로 본다
        public Regex IdToken { get; }
        public Regex TestName { get; }

        // 명세 번호("01" …) 오름차순
        public IReadOnlyList<string> Specs { get; }

        // Specs 와 같은 순서의 id 꼴
        public IReadOnlyList<Regex> SpecIds { get; }

        public Regex? IdPatternFor(string spec)
        {
            for (int i = 0; i < Specs.Count; i++)
            {
                if (string.Equals(Specs[i], spec, StringComparison.Ordinal))
                {
                    return SpecIds[i];
                }
            }
            return null;
        }

        public static VectorConfig LoadFromRepo(string repoRoot) => Load(RepoRoot.Resolve(repoRoot, DefaultPath));

        public static VectorConfig Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

        public static VectorConfig Parse(string json)
        {
            var problems = new List<string>();
            var j = new StrictJson("vectors.config.json", problems);
            JObject? root = GoldenEntry.ParseRoot(j, json);
            if (root == null)
            {
                GoldenFormatException.ThrowIfAny(problems);
                throw new InvalidOperationException("unreachable");
            }
            j.OnlyFields(root, "format", "description", "specDir", "specFile", "chapter", "vectorChapter", "subsection", "tableRow",
                "tableIgnore", "tableId", "boldItem", "numberedItem", "numberedPrefix", "proseItem", "idToken", "testName", "specs");
            j.Format(root, FormatVersion);
            j.String(root, "description", false);
            string specDir = j.String(root, "specDir", true) ?? "";
            Regex specFile = Rx(j, root, "specFile", "spec");
            Regex chapter = Rx(j, root, "chapter", "ch");
            Regex vectorChapter = Rx(j, root, "vectorChapter");
            Regex subsection = Rx(j, root, "subsection", "sec");
            Regex tableRow = Rx(j, root, "tableRow", "cell");
            Regex tableIgnore = Rx(j, root, "tableIgnore");
            Regex tableId = Rx(j, root, "tableId", "id", "star", "to");
            Regex boldItem = Rx(j, root, "boldItem", "id");
            Regex numberedItem = Rx(j, root, "numberedItem", "num");
            string numberedPrefix = j.String(root, "numberedPrefix", true) ?? "";
            Regex proseItem = Rx(j, root, "proseItem", "id", "to");
            Regex idToken = Rx(j, root, "idToken");
            Regex testName = Rx(j, root, "testName", "spec", "id");

            var specs = new List<string>();
            JObject? specObj = j.Object(root, "specs", true);
            if (specObj != null)
            {
                foreach (JProperty p in specObj.Properties())
                {
                    specs.Add(p.Name);
                }
            }
            specs.Sort(StringComparer.Ordinal);
            var ids = new List<Regex>();
            foreach (string s in specs)
            {
                var sj = new StrictJson("vectors.config.json specs." + s, problems);
                if (!Regex.IsMatch(s, "^[0-9]{2}$"))
                {
                    sj.Problem("spec key must be two digits");
                }
                JToken t = specObj![s]!;
                if (t.Type != JTokenType.Object)
                {
                    sj.Problem("must be an object");
                    ids.Add(new Regex("(?!)"));
                    continue;
                }
                var o = (JObject)t;
                sj.OnlyFields(o, "id", "example");
                Regex id = Rx(sj, o, "id");
                ids.Add(id);
                string? example = sj.String(o, "example", true);
                if (example != null && !id.IsMatch(example))
                {
                    sj.Problem("example '" + example + "' does not match the id pattern");
                }
            }
            GoldenFormatException.ThrowIfAny(problems);
            return new VectorConfig(specDir, specFile, chapter, vectorChapter, subsection, tableRow, tableIgnore, tableId, boldItem,
                numberedItem, numberedPrefix, proseItem, idToken, testName, specs, ids);
        }

        private static Regex Rx(StrictJson j, JObject obj, string name, params string[] groups)
        {
            string? pattern = j.String(obj, name, true);
            if (pattern == null)
            {
                return new Regex("(?!)");
            }
            Regex rx;
            try
            {
                rx = new Regex(pattern, RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                j.Problem("'" + name + "' is not a valid regular expression: " + ex.Message);
                return new Regex("(?!)");
            }
            foreach (string g in groups)
            {
                if (Array.IndexOf(rx.GetGroupNames(), g) < 0)
                {
                    j.Problem("'" + name + "' must have a named group (?<" + g + ">…)");
                }
            }
            return rx;
        }
    }
}
