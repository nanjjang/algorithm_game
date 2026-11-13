using System.Collections.Generic;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.AssetCheck.Report
{
    // 대조 보고서의 자리 하나
    public sealed class SlotReport
    {
        public SlotReport(string slotId, AssetKey key, SlotStatus status, string? file, string? sha256, PixelSize? frameSize,
            int frames, bool approvalStale, IReadOnlyList<string> problems, string? reason)
        {
            SlotId = slotId;
            Key = key;
            Status = status;
            File = file;
            Sha256 = sha256;
            FrameSize = frameSize;
            Frames = frames;
            ApprovalStale = approvalStale;
            Problems = problems;
            Reason = reason;
        }

        public string SlotId { get; }

        public AssetKey Key { get; }

        public SlotStatus Status { get; }

        // art/production 기준. 없는 자리는 null
        public string? File { get; }

        public string? Sha256 { get; }

        public PixelSize? FrameSize { get; }

        // 있는 자리는 프레임 수, 없는 자리는 asset_slots.json 의 장수(모르면 0)
        public int Frames { get; }

        // 승인 장부에 있지만 파일이 바뀌어 풀린 자리
        public bool ApprovalStale { get; }

        public IReadOnlyList<string> Problems { get; }

        // 없는 자리의 까닭
        public string? Reason { get; }
    }
}
