using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using Wizard.TestKit.Shared.Golden;

namespace Wizard.TestKit.Shared.Arith
{
    // 산술 경계 벡터 묶음: tests/golden/arith/<묶음 이름>.json (형식은 tests/golden/arith/README.md).
    // 기대값은 계약(CONTRACTS 1.1 의 나눗셈·넘침 규칙)에서 손으로 적는다. 구현 출력에서 베끼지 않는다.
    public sealed class ArithBundle
    {
        public const int FormatVersion = 1;

        private ArithBundle(string name, IReadOnlyList<ArithCase> cases)
        {
            Name = name;
            Cases = cases;
        }

        // 파일 이름에서 .json 을 뺀 것
        public string Name { get; }

        public IReadOnlyList<ArithCase> Cases { get; }

        public static ArithBundle Load(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            return Parse(name, File.ReadAllText(path, Encoding.UTF8));
        }

        public static ArithBundle Parse(string name, string json)
        {
            var problems = new List<string>();
            var j = new StrictJson("arith/" + name + ".json", problems);
            var cases = new List<ArithCase>();
            JObject? root = GoldenEntry.ParseRoot(j, json);
            if (root != null)
            {
                j.OnlyFields(root, "format", "bundle", "cases");
                j.Format(root, FormatVersion);
                string? declared = j.String(root, "bundle", true);
                if (declared != null && !string.Equals(declared, name, StringComparison.Ordinal))
                {
                    j.Problem("bundle '" + declared + "' does not match the file name '" + name + "'");
                }
                JArray? arr = j.Array(root, "cases", true);
                if (arr != null && arr.Count == 0)
                {
                    j.Problem("'cases' is empty");
                }
                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; arr != null && i < arr.Count; i++)
                {
                    var c = new StrictJson(j.Where + " cases[" + i + "]", problems);
                    if (arr[i].Type != JTokenType.Object)
                    {
                        c.Problem("must be an object");
                        continue;
                    }
                    int before = problems.Count;
                    var o = (JObject)arr[i];
                    c.OnlyFields(o, "id", "op", "args", "expect", "fault", "notes");
                    string? id = c.String(o, "id", true);
                    string? op = c.String(o, "op", true);
                    c.String(o, "notes", false);
                    if (id != null && !ids.Add(id))
                    {
                        c.Problem("duplicate case id '" + id + "'");
                    }
                    List<ArithValue> args = Values(c, c.Array(o, "args", true), "args");
                    JArray? expectArr = c.Array(o, "expect", false);
                    string? fault = c.String(o, "fault", false);
                    List<ArithValue>? expect = null;
                    if ((expectArr == null) == (fault == null))
                    {
                        c.Problem("exactly one of 'expect' and 'fault' is required");
                    }
                    if (expectArr != null)
                    {
                        expect = Values(c, expectArr, "expect");
                        if (expectArr.Count == 0)
                        {
                            c.Problem("'expect' is empty");
                        }
                    }
                    if (fault != null && !ArithFaults.IsExpectable(fault))
                    {
                        c.Problem("fault '" + fault + "' is not an expectable class (only '" + ArithFaults.SimInvariant + "')");
                    }
                    if (problems.Count == before && id != null && op != null)
                    {
                        cases.Add(new ArithCase(id, op, args, expect, fault));
                    }
                }
            }
            GoldenFormatException.ThrowIfAny(problems);
            return new ArithBundle(name, cases);
        }

        private static List<ArithValue> Values(StrictJson c, JArray? arr, string field)
        {
            var list = new List<ArithValue>();
            for (int i = 0; arr != null && i < arr.Count; i++)
            {
                JToken t = arr[i];
                if (t.Type != JTokenType.String)
                {
                    c.Problem(field + "[" + i + "] must be a string (decimal or 0x hex); JSON numbers lose 64-bit values");
                    continue;
                }
                if (!ArithValue.TryParse((string?)t, out ArithValue v))
                {
                    c.Problem(field + "[" + i + "] '" + (string?)t + "' is not a decimal 64-bit integer or 0x hex");
                    continue;
                }
                list.Add(v);
            }
            return list;
        }
    }
}
