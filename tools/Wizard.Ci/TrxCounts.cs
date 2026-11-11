using System;
using System.Globalization;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Wizard.Ci
{
    // TRX 결과의 집계(<ResultSummary><Counters total passed failed notExecuted …>)
    public readonly struct TrxCounts
    {
        public TrxCounts(int files, int total, int passed, int failed, int notExecuted)
        {
            Files = files;
            Total = total;
            Passed = passed;
            Failed = failed;
            NotExecuted = notExecuted;
        }

        public int Files { get; }
        public int Total { get; }
        public int Passed { get; }
        public int Failed { get; }
        public int NotExecuted { get; }

        public static TrxCounts ReadDir(string dir)
        {
            int files = 0, total = 0, passed = 0, failed = 0, notExecuted = 0;
            if (Directory.Exists(dir))
            {
                string[] list = Directory.GetFiles(dir, "*.trx", SearchOption.AllDirectories);
                Array.Sort(list, StringComparer.Ordinal);
                foreach (string f in list)
                {
                    TrxCounts c = Parse(File.ReadAllText(f));
                    files++;
                    total += c.Total;
                    passed += c.Passed;
                    failed += c.Failed;
                    notExecuted += c.NotExecuted;
                }
            }
            return new TrxCounts(files, total, passed, failed, notExecuted);
        }

        public static TrxCounts Parse(string xml)
        {
            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml);
            }
            catch (XmlException e)
            {
                throw new CiException("TRX 를 읽지 못했다: " + e.Message);
            }
            foreach (XElement e in doc.Descendants())
            {
                if (e.Name.LocalName == "Counters")
                {
                    return new TrxCounts(1, Attr(e, "total"), Attr(e, "passed"), Attr(e, "failed") + Attr(e, "error") + Attr(e, "timeout") + Attr(e, "aborted"),
                        Attr(e, "notExecuted") + Attr(e, "inconclusive") + Attr(e, "pending") + Attr(e, "disconnected") + Attr(e, "warning"));
                }
            }
            throw new CiException("TRX 에 Counters 가 없다");
        }

        private static int Attr(XElement e, string name)
        {
            string? v = (string?)e.Attribute(name);
            return v != null && int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }
    }
}
