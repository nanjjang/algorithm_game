namespace Wizard.AssetCheck.Check
{
    // 반입 검사의 진단 하나. File 은 저장소 기준 경로(art/production/…), Slot 은 대조 보고서의 자리 id(없으면 null).
    // 문구는 개발 로그용 영어다(화면에 보이지 않는다)
    public sealed class Finding
    {
        public Finding(FindingSeverity severity, string check, string file, string? slot, string message)
        {
            Severity = severity;
            Check = check;
            File = file;
            Slot = slot;
            Message = message;
        }

        public FindingSeverity Severity { get; }

        // 검사 이름(CheckNames)
        public string Check { get; }

        public string File { get; }

        public string? Slot { get; }

        public string Message { get; }

        public bool IsError => Severity == FindingSeverity.Error;

        public override string ToString() =>
            (IsError ? "FAIL  " : "warn  ") + "[" + Check + "] " + File + (Slot != null ? " (" + Slot + ")" : "") + ": " + Message;
    }
}
