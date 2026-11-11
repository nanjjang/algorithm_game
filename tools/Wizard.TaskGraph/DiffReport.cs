using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Wizard.TaskGraph
{
    public sealed class DiffReport
    {
        private readonly List<(bool Ok, string Path, string Note)> _lines = new List<(bool, string, string)>();

        public DiffReport(string id, string baseRef, string headRef)
        {
            Id = id;
            BaseRef = baseRef;
            HeadRef = headRef;
        }

        public string Id { get; }
        public string BaseRef { get; }
        public string HeadRef { get; }
        public int ChangedCount { get; set; }

        public IReadOnlyList<(bool Ok, string Path, string Note)> Lines => _lines;
        public IEnumerable<(bool Ok, string Path, string Note)> Failures => _lines.Where(l => !l.Ok);
        public bool Passed => _lines.All(l => l.Ok);

        public void Ok(string path, string note) => _lines.Add((true, path, note));

        public void Fail(string path, string note) => _lines.Add((false, path, note));

        public void Print(TextWriter o)
        {
            foreach ((bool ok, string path, string note) in _lines.OrderBy(l => l.Path, System.StringComparer.Ordinal))
            {
                o.WriteLine($"{(ok ? "ok  " : "FAIL")}  {path} — {note}");
            }
            int fails = _lines.Count(l => !l.Ok);
            o.WriteLine($"check-diff {Id}: {BaseRef}...{HeadRef} 변경 {ChangedCount}개, 위반 {fails}건");
            o.WriteLine(fails == 0 ? "통과" : "실패");
        }
    }
}
