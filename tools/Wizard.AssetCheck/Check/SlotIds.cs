using System.Globalization;
using Wizard.ViewLogic.Assets;

namespace Wizard.AssetCheck.Check
{
    // 대조 보고서의 자리 id. 열쇠 하나 = 자리 하나이고, 같은 열쇠를 크기마다 따로 그린 그림(T-58)은 "@<폭>x<높이>"를 붙여 가른다.
    // 예: "Ui:K-20/button_large#hover", "Ui:K-40/selection_corner#normal@24x24", "Character:char.5/idle_down"
    public static class SlotIds
    {
        public const char SizeSeparator = '@';

        public static string Of(AssetRecord rec, AssetRegistry reg)
        {
            string id = AssetKeyComparer.Format(rec.Key);
            if (reg.GetAll(rec.Key).Count > 1)
            {
                id += SizeSeparator + rec.FrameSize.Width.ToString(CultureInfo.InvariantCulture) + "x" + rec.FrameSize.Height.ToString(CultureInfo.InvariantCulture);
            }
            return id;
        }
    }
}
