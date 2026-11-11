using System;
using System.Text.RegularExpressions;

namespace Wizard.TestKit.Shared.Vectors
{
    // 벡터 테스트 이름 규칙(CLAUDE.md 8절): S<명세 번호>_<벡터 id>__<설명>
    //   벡터 id 의 '-' 는 '_' 로 바꾼다(식별자에 '-' 를 못 쓴다). 명세의 id 에는 '_' 가 없다(SpecVectorScanner 가 확인한다).
    //   예: spec 01 의 T-H04 → S01_T_H04__LockAtExactly100,  spec 11 의 TV-11-017 → S11_TV_11_017__…
    // 이름 규칙의 정규식은 tests/vectors.config.json 의 "testName" 과 같아야 한다(자체 테스트가 확인한다).
    public static class VectorNames
    {
        public const string TestNamePattern =
            "^S(?<spec>[0-9]{2})_(?<id>[A-Za-z0-9]+(?:_[A-Za-z0-9]+)*)(?:__(?<desc>[A-Za-z0-9][A-Za-z0-9_]*))?$";

        private static readonly Regex TestNameRx = new Regex(TestNamePattern, RegexOptions.CultureInvariant);
        private static readonly Regex DescriptionRx = new Regex("^[A-Za-z0-9][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        // tasks.json 선택자와 같은 꼴: "S01:T-H04"
        public static string Key(string spec, string id) => "S" + spec + ":" + id;

        // "S01_T_H04" (설명 없음)
        public static string TestPrefix(string spec, string id)
        {
            if (spec.Length != 2 || id.IndexOf('_') >= 0)
            {
                throw new ArgumentException("spec must be two digits and id must not contain '_': S" + spec + ":" + id);
            }
            return "S" + spec + "_" + id.Replace('-', '_');
        }

        // "S01_T_H04__LockAtExactly100"
        public static string TestName(string spec, string id, string description)
        {
            if (!DescriptionRx.IsMatch(description) || description.IndexOf("__", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException("description must be an identifier without '__': " + description);
            }
            return TestPrefix(spec, id) + "__" + description;
        }

        // 테스트 이름에서 (명세, 벡터 id)를 얻는다. 네임스페이스·형식 이름 앞머리("Wizard.Sim.Tests.HeatTests.")와
        // 이론 테스트의 인자 꼬리("(a: 1)")는 떼고 본다.
        public static bool TryParseTestName(string name, out string spec, out string id)
        {
            return TryParse(TestNameRx, name, out spec, out id);
        }

        // 설정 파일의 정규식으로 읽는다(VectorCoverage 가 쓴다)
        public static bool TryParseTestName(Regex testName, string name, out string spec, out string id)
        {
            return TryParse(testName, name, out spec, out id);
        }

        private static bool TryParse(Regex rx, string name, out string spec, out string id)
        {
            spec = "";
            id = "";
            string method = MethodPart(name);
            Match m = rx.Match(method);
            if (!m.Success)
            {
                return false;
            }
            spec = m.Groups["spec"].Value;
            id = m.Groups["id"].Value.Replace('_', '-');
            return true;
        }

        private static string MethodPart(string name)
        {
            string s = name;
            int paren = s.IndexOf('(');
            if (paren >= 0)
            {
                s = s.Substring(0, paren);
            }
            int dot = s.LastIndexOf('.');
            if (dot >= 0)
            {
                s = s.Substring(dot + 1);
            }
            return s.Trim();
        }
    }
}
