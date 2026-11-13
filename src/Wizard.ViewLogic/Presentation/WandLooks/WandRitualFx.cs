using System.Collections.Generic;

namespace Wizard.ViewLogic.Presentation.WandLooks
{
    // 강화 의식의 속성별 변주 그림(spec 14 5.6절 ritual/<이름>_<속성>, effects.md 5절). 구조와 시간은 다섯이 같고
    // 재질·무늬만 지팡이의 속성으로 갈린다. 금빛(대성공)과 연두(변이) 같은 공용 그림은 여기 없다
    public static class WandRitualFx
    {
        public const string Group = "ritual/";

        public static readonly IReadOnlyList<string> ElementParts = new[]
        {
            "seal", "outer_glyph", "charge", "success_column", "max_column", "success_mote", "break_pieces", "extinguish", "ward_ribbon", "stage_hold",
        };

        public static string FxId(WandLookSet set, string part) => Group + part + "_" + set.Element;

        public static AssetKey Key(WandLookSet set, string part) => new AssetKey(AssetPack.Fx, FxId(set, part));
    }
}
