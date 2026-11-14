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
                doc = XDocument.Parse(xml.TrimStart('﻿'));
            }
            catch (XmlException e)
            {
                throw new CiException("TRX 를 읽지 못했다: " + e.Message);
            }
            // 건너뜀은 Counters 의 notExecuted 만으로 세지 않는다: xunit 의 [Fact(Skip=…)] 은 결과가 outcome="NotExecuted" 인데
            // Counters 는 notExecuted="0", executed 가 total 보다 작게만 나온다(이 PC 의 dotnet 10.0.201 + xunit.runner.visualstudio 3.1.4 로 확인. FIX-9).
            // 그래서 (Counters 의 건너뜀 칸 합, total − executed, 결과 줄의 Passed·실패 아닌 outcome 수) 가운데 큰 값을 쓴다. 실패도 같은 식으로 둘 중 큰 값.
            XElement? counters = null;
            int resultFailed = 0, resultOther = 0;
            foreach (XElement e in doc.Descendants())
            {
                if (e.Name.LocalName == "Counters" && counters == null)
                {
                    counters = e;
                }
                else if (e.Name.LocalName == "UnitTestResult")
                {
                    switch ((string?)e.Attribute("outcome"))
                    {
                        case "Passed":
                            break;
                        case "Failed":
                        case "Error":
                        case "Timeout":
                        case "Aborted":
                            resultFailed++;
                            break;
                        default:
                            resultOther++;
                            break;
                    }
                }
            }
            if (counters == null)
            {
                throw new CiException("TRX 에 Counters 가 없다");
            }
            int total = Attr(counters, "total");
            int failed = Math.Max(Attr(counters, "failed") + Attr(counters, "error") + Attr(counters, "timeout") + Attr(counters, "aborted"), resultFailed);
            int notExecuted = Attr(counters, "notExecuted") + Attr(counters, "inconclusive") + Attr(counters, "pending") + Attr(counters, "disconnected") +
                              Attr(counters, "warning") + Attr(counters, "notRunnable");
            if (counters.Attribute("executed") != null)
            {
                notExecuted = Math.Max(notExecuted, total - Attr(counters, "executed"));
            }
            notExecuted = Math.Max(notExecuted, resultOther);
            return new TrxCounts(1, total, Attr(counters, "passed"), failed, notExecuted);
        }

        private static int Attr(XElement e, string name)
        {
            string? v = (string?)e.Attribute(name);
            return v != null && int.TryParse(v, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : 0;
        }
    }
}
