using System.IO;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.Tests.ViewEffects
{
    /// <summary>G16 테스트 공용: Resources의 실제 JSON(presentation 사본 + DataSolo/G16.json)을 파일로 읽는다.</summary>
    public static class ViewEffectsTestData
    {
        static PresentationData _data;

        public static string Resources => Path.Combine(TestContent.AssetsDir, "Resources");
        public static string Read(string relative) => File.ReadAllText(Path.Combine(Resources, relative), System.Text.Encoding.UTF8);

        public static string BindingsJson => Read("Data/presentation/event_bindings.json");
        public static string BudgetJson => Read("Data/presentation/particle_budget.json");
        public static string AliasesJson => Read("Data/presentation/asset_aliases.json");
        public static string TuningJson => Read("DataSolo/G16.json");

        /// <summary>새로 읽은 묶음(테스트마다 상태가 섞이지 않게 새 객체).</summary>
        public static PresentationData Fresh() => PresentationData.FromTexts(BindingsJson, BudgetJson, AliasesJson, TuningJson);

        /// <summary>읽기만 하는 테스트용(한 번 읽어 둔다).</summary>
        public static PresentationData Shared => _data ?? (_data = Fresh());
    }
}
