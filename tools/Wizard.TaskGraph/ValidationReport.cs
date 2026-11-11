using System.Collections.Generic;
using System.IO;

namespace Wizard.TaskGraph
{
    public sealed class ValidationReport
    {
        public ValidationReport(IReadOnlyList<string> errors, IReadOnlyList<string> summary)
        {
            Errors = errors;
            Summary = summary;
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Summary { get; }
        public bool Ok => Errors.Count == 0;

        public void Print(TextWriter o)
        {
            foreach (string e in Errors)
            {
                o.WriteLine("FAIL  " + e);
            }
            o.WriteLine();
            foreach (string s in Summary)
            {
                o.WriteLine(s);
            }
            o.WriteLine(Ok ? "\n통과" : $"\n실패 {Errors.Count}건");
        }
    }
}
