namespace Wizard.BalanceLab.Feel
{
    internal sealed class SelfCheckResult
    {
        public SelfCheckResult(string name, bool passed, string detail)
        {
            Name = name;
            Passed = passed;
            Detail = detail;
        }

        public string Name { get; }

        public bool Passed { get; }

        public string Detail { get; }
    }
}
