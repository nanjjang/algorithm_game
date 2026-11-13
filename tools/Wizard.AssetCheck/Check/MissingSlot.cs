using Wizard.ViewLogic.Assets;
using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.AssetCheck.Check
{
    // 그림이 없는 자리(대조 보고서의 "없음"). 크기와 장수는 asset_slots.json 이 적은 자리만 있다
    public sealed class MissingSlot
    {
        public MissingSlot(AssetKey key, PixelSize? frameSize, int count, string reason)
        {
            Key = key;
            FrameSize = frameSize;
            Count = count;
            Reason = reason;
            SlotId = AssetKeyComparer.Format(key);
        }

        public AssetKey Key { get; }

        public string SlotId { get; }

        public PixelSize? FrameSize { get; }

        public int Count { get; }

        public string Reason { get; }
    }
}
