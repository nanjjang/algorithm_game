namespace Wizard.Perf
{
    // 판정하는 항목 하나. Limit 이 null 이면 한도 없이 기준선 대비 회귀만 본다
    internal sealed class MetricDef
    {
        public MetricDef(string id, string unit, Better better, double? limit, string source)
        {
            Id = id;
            Unit = unit;
            Better = better;
            Limit = limit;
            Source = source;
        }

        public string Id { get; }

        public string Unit { get; }

        public Better Better { get; }

        public double? Limit { get; }

        // 한도의 출처(명세 절, 또는 [총괄 제안]). 측정값과 나란히 낸다
        public string Source { get; }
    }
}
