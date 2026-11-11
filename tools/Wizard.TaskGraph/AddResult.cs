namespace Wizard.TaskGraph
{
    public sealed class AddResult
    {
        public AddResult(string id, string newText, ValidationReport report)
        {
            Id = id;
            NewText = newText;
            Report = report;
        }

        public string Id { get; }
        public string NewText { get; }
        public ValidationReport Report { get; }
    }
}
