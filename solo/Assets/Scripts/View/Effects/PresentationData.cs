using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// G16이 읽는 데이터 묶음: presentation/event_bindings.json·particle_budget.json·asset_aliases.json(사본 그대로, 09 9절 표의
    /// "표시 작업이 필요한 만큼 읽는다")과 DataSolo/G16.json(ctx.Content.SoloExtra("G16")).
    /// </summary>
    public sealed class PresentationData
    {
        public const string BindingsResource = "Data/presentation/event_bindings";
        public const string BudgetResource = "Data/presentation/particle_budget";
        public const string AliasesResource = "Data/presentation/asset_aliases";
        public const string SoloExtraName = "G16";

        public EffectsTuning Tuning;
        public EventBindings Bindings;
        public ParticleBudgetConfig Budget;
        public ShotSpriteTable Shots;

        public static PresentationData FromTexts(string bindingsJson, string budgetJson, string aliasesJson, string tuningJson)
        {
            var t = EffectsTuning.Parse(tuningJson);
            return new PresentationData
            {
                Tuning = t,
                Bindings = EventBindings.Parse(bindingsJson),
                Budget = ParticleBudgetConfig.Parse(budgetJson),
                Shots = ShotSpriteTable.Parse(aliasesJson, t)
            };
        }

        /// <summary>앱에서: Resources의 TextAsset과 콘텐츠의 보충 조각. 없는 파일은 빈 값(표시만 비고 죽지 않는다).</summary>
        public static PresentationData Load(IContentDb content)
        {
            var extra = content != null ? content.SoloExtra(SoloExtraName) : null;
            var t = new EffectsTuning(extra);
            return new PresentationData
            {
                Tuning = t,
                Bindings = EventBindings.Parse(Text(BindingsResource)),
                Budget = ParticleBudgetConfig.Parse(Text(BudgetResource)),
                Shots = ShotSpriteTable.Parse(Text(AliasesResource), t)
            };
        }

        static string Text(string resource)
        {
            var ta = Resources.Load<TextAsset>(resource);
            return ta != null ? ta.text : null;
        }
    }
}
