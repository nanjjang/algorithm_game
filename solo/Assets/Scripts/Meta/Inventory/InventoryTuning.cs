using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Meta.Inventory
{
    /// <summary>
    /// Def에 없는 떠나보내기 수치(Resources/DataSolo/G13.json, 00 문서 9절 "수치 조각 규칙").
    /// 값은 07 요약서 3.5절 그대로: +7 이상 이름 입력, 여러 자루는 +3 이하·변이 0·확장 0.
    /// 조각이 없으면 가장 조심하는 쪽(0)이 된다: 모든 단계에 이름 입력, 여러 자루는 +0만.
    /// </summary>
    public sealed class InventoryTuning
    {
        public const string ExtraName = "G13";

        public int ReleaseNameConfirmMinStage;
        public int ReleaseMultiMaxStage;
        public int ReleaseMultiMaxMutations;
        public int ReleaseMultiMaxSlotsExtra;

        public static InventoryTuning From(IContentDb db)
        {
            var t = new InventoryTuning();
            JObject o = db != null ? db.SoloExtra(ExtraName) : null;
            if (o == null) return t;
            t.ReleaseNameConfirmMinStage = Int(o, "release_name_confirm_min_stage", t.ReleaseNameConfirmMinStage);
            t.ReleaseMultiMaxStage = Int(o, "release_multi_max_stage", t.ReleaseMultiMaxStage);
            t.ReleaseMultiMaxMutations = Int(o, "release_multi_max_mutations", t.ReleaseMultiMaxMutations);
            t.ReleaseMultiMaxSlotsExtra = Int(o, "release_multi_max_slots_extra", t.ReleaseMultiMaxSlotsExtra);
            return t;
        }

        static int Int(JObject o, string key, int fallback)
        {
            JToken v = o[key];
            return v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) ? v.Value<int>() : fallback;
        }
    }
}
