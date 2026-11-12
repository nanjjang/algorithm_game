namespace Wizard.Perf
{
    // 항목 하나의 판정. Failures 가 비어 있으면 통과다
    internal sealed class MetricVerdict
    {
        public MetricVerdict(string id, MetricDef? def, double? measured, double? baseline, string[] failures)
        {
            Id = id;
            Def = def;
            Measured = measured;
            Baseline = baseline;
            Failures = failures;
        }

        public string Id { get; }

        public MetricDef? Def { get; }

        public double? Measured { get; }

        public double? Baseline { get; }

        public string[] Failures { get; }

        public bool Passed => Failures.Length == 0;
    }
}
