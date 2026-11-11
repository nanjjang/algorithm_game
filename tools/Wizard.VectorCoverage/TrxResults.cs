using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Wizard.TestKit.Shared.Vectors;

namespace Wizard.VectorCoverage
{
    // dotnet test 의 TRX 결과(--logger trx)에서 벡터 테스트의 결과를 읽는다(--trx 를 준 때만).
    // 벡터 하나는 그 벡터의 테스트가 한 번 이상 Passed 이고 Passed 가 아닌 결과가 하나도 없을 때 통과다.
    public sealed class TrxResults
    {
        private readonly Dictionary<string, int> _passed = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _bad = new Dictionary<string, string>(StringComparer.Ordinal);

        public int FileCount { get; private set; }

        public static TrxResults Load(IReadOnlyList<string> paths, System.Text.RegularExpressions.Regex testName)
        {
            var r = new TrxResults();
            var files = new List<string>();
            foreach (string p in paths)
            {
                if (Directory.Exists(p))
                {
                    files.AddRange(Directory.GetFiles(p, "*.trx", SearchOption.AllDirectories));
                }
                else if (File.Exists(p))
                {
                    files.Add(p);
                }
                else
                {
                    throw new CoverageException("--trx: 파일이나 폴더가 없다: " + p, CoverageException.UsageExitCode);
                }
            }
            files.Sort(StringComparer.Ordinal);
            if (files.Count == 0)
            {
                throw new CoverageException("--trx: .trx 파일이 하나도 없다: " + string.Join(", ", paths), CoverageException.UsageExitCode);
            }
            foreach (string f in files)
            {
                r.Read(f, testName);
            }
            r.FileCount = files.Count;
            return r;
        }

        public static TrxResults FromXml(string xml, System.Text.RegularExpressions.Regex testName)
        {
            var r = new TrxResults();
            r.ReadXml(XDocument.Parse(xml), "<xml>", testName);
            r.FileCount = 1;
            return r;
        }

        // 통과면 null, 아니면 이유
        public string? Verdict(SpecVector v)
        {
            if (_bad.TryGetValue(v.Key, out string? why))
            {
                return why;
            }
            return _passed.ContainsKey(v.Key) ? null : "TRX 결과에 이 벡터의 테스트 실행이 없다";
        }

        private void Read(string path, System.Text.RegularExpressions.Regex testName)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(path);
            }
            catch (XmlException e)
            {
                throw new CoverageException("--trx: XML 이 아니다: " + path + " — " + e.Message, CoverageException.UsageExitCode);
            }
            ReadXml(doc, path, testName);
        }

        private void ReadXml(XDocument doc, string path, System.Text.RegularExpressions.Regex testName)
        {
            foreach (XElement e in doc.Descendants())
            {
                if (e.Name.LocalName != "UnitTestResult")
                {
                    continue;
                }
                string name = (string?)e.Attribute("testName") ?? "";
                string outcome = (string?)e.Attribute("outcome") ?? "";
                if (!VectorNames.TryParseTestName(testName, name, out string spec, out string id))
                {
                    continue;
                }
                string key = VectorNames.Key(spec, id);
                if (outcome == "Passed")
                {
                    _passed[key] = _passed.TryGetValue(key, out int n) ? n + 1 : 1;
                }
                else if (!_bad.ContainsKey(key))
                {
                    _bad.Add(key, name + " 의 결과가 " + (outcome.Length == 0 ? "(없음)" : outcome) + " (" + Path.GetFileName(path) + ")");
                }
            }
        }
    }
}
