using Wizard.ViewLogic.Common;
using Wizard.ViewLogic.Presentation;

namespace Wizard.AssetCheck.Slots
{
    // asset_slots.json 의 한 줄: 열쇠, 프레임 하나의 크기, 장수, 출처 절
    public sealed class AssetSlotEntry
    {
        public AssetSlotEntry(AssetKey key, PixelSize frameSize, int count, string spec)
        {
            Key = key;
            FrameSize = frameSize;
            Count = count;
            Spec = spec;
        }

        public AssetKey Key { get; }

        public PixelSize FrameSize { get; }

        public int Count { get; }

        public string Spec { get; }
    }
}
