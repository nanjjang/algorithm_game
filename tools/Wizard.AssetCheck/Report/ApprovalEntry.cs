namespace Wizard.AssetCheck.Report
{
    // 승인 한 줄: 자리 id, 그 자리의 파일(art/production 기준), 승인한 때의 SHA-256(소문자 16진)
    public sealed class ApprovalEntry
    {
        public ApprovalEntry(string slotId, string file, string sha256)
        {
            SlotId = slotId;
            File = file;
            Sha256 = sha256;
        }

        public string SlotId { get; }

        public string File { get; }

        public string Sha256 { get; }
    }
}
