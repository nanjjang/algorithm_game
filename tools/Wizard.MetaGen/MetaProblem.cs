namespace Wizard.MetaGen
{
    public readonly struct MetaProblem
    {
        public MetaProblem(MetaProblemKind kind, string path, string detail)
        {
            Kind = kind;
            Path = path;
            Detail = detail;
        }

        public MetaProblemKind Kind { get; }

        // 문제가 있는 파일의 저장소 기준 상대 경로(빠진 경우는 대상, 그 밖은 .meta).
        public string Path { get; }

        public string Detail { get; }

        public string Code
        {
            get
            {
                switch (Kind)
                {
                    case MetaProblemKind.Missing: return "missing-meta";
                    case MetaProblemKind.Orphan: return "orphan-meta";
                    case MetaProblemKind.DuplicateGuid: return "duplicate-guid";
                    case MetaProblemKind.GuidMismatch: return "guid-mismatch";
                    default: return "bad-meta";
                }
            }
        }

        public override string ToString()
        {
            return Detail.Length == 0 ? $"{Code}: {Path}" : $"{Code}: {Path} ({Detail})";
        }
    }
}
