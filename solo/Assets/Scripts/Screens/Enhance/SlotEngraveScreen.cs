using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 슬롯 새기기(08 10.10절, 07 4.8절) 800×560. 인자 WandId(long).
    /// 대상 +7 이상 && 확장 3칸 미만(TV-141), 비용 상급 강화석 2 + 20,000 × (확장 칸 + 1) 골드(TV-139),
    /// 확률 min(100, 40 + 20 × 실패 수)(TV-140)을 화면에 보이고, 실패마다 눈금 한 칸. 판정은 IEnhanceService.CarveSlot.
    /// 결과를 보인 채 창이 남아 다시 새길 수 있다.
    /// </summary>
    public sealed class SlotEngraveScreen : ScreenBase
    {
        public const float Width = 800f, Height = 560f;
        RectTransform _panel;
        UiLabel _condition, _slots, _chance, _cost, _marks, _result;
        UiButton _engrave, _close;
        long _wandId;
        RectTransform _markRow;

        public override ScreenId Id => ScreenId.SlotEngrave;
        public override ScreenLayer Layer => ScreenLayer.Modal;
        public override bool BlocksInput => true;

        protected override void BuildContent(RectTransform content)
        {
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Paper, 0f, 0f, Width, Height);
            _panel = panel.rectTransform;
            UiKit.Center(_panel, Width, Height);
            panel.name = "SlotEngrave";
            const float Pad = 32f;
            float w = Width - 2 * Pad;
            UiKit.LabelKey(Ctx, _panel, EnhanceNameKeys.EngraveTitle, UiFontId.F24, Pad, Pad, w, 0f, UiTheme.PaperText);
            _condition = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, Pad, 88f, w, 0f, UiTheme.PaperText);
            _slots = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, Pad, 120f, w, 0f, UiTheme.PaperText);
            _chance = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F32, Pad, 160f, w, 0f, UiTheme.PaperText);
            _cost = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, Pad, 216f, w, 48f, UiTheme.PaperText, TextAnchor.UpperLeft, true);
            _marks = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, Pad, 272f, 240f, 0f, UiTheme.PaperText);
            _markRow = UiKit.Node(_panel, "Marks", Pad + 248f, 268f, 200f, 24f);
            _result = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F24, Pad, 320f, w, 0f, UiTheme.PaperText);
            const float BtnW = 240f, BtnH = 64f;
            float by = Height - Pad - BtnH;
            _engrave = UiKit.Button(Ctx, _panel, T(EnhanceNameKeys.Engrave), Pad, by, BtnW, BtnH, DoEngrave, UiButtonStyle.Primary, Focus, string.Empty);
            _close = UiKit.Button(Ctx, _panel, T(EnhanceNameKeys.CommonClose), Width - Pad - BtnW, by, BtnW, BtnH, () => Close(), UiButtonStyle.Large, Focus);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _wandId = Args.Get(ScreenArgKeys.WandId, 0L);
            _result.SetText(string.Empty);
            Refresh();
            Focus.SetCursor(_engrave, false);
        }

        void Refresh()
        {
            var cfg = Ctx != null && Ctx.Content != null ? Ctx.Content.Enhance : null;
            var w = Ctx != null && Ctx.Profile != null ? Ctx.Profile.Wand(_wandId) : null;
            if (cfg == null || w == null || Ctx.Enhance == null) { _engrave.SetDisabled(EnhanceNameKeys.ErrorKey(ErrorCode.NotFound)); return; }
            ProbBreakdown b = Ctx.Enhance.PreviewCarve(_wandId);
            _condition.SetText(F(EnhanceNameKeys.EngraveCondition, "stage", cfg.Carve.MinStage, "max", cfg.SlotExtraMax));
            _slots.SetText(F(EnhanceNameKeys.ExtSlots, "n", w.SlotsExtra, "max", cfg.SlotExtraMax));
            _chance.SetText(F(EnhanceNameKeys.EngraveChance, "n", b.SuccessPct));
            _cost.SetText(F(EnhanceNameKeys.EngraveCost, "gold", N(b.CostGold), "stone", T(b.StoneId ?? EnhanceNameKeys.StoneHigh), "n", b.StoneCount));
            _marks.SetText(F(EnhanceNameKeys.EngraveFailMarks, "n", w.CarveBonus));
            EnhanceUi.Clear(_markRow);
            // 실패 눈금(S-1203 empty/filled): 확률이 100%에 닿기까지의 칸 수만큼.
            int marks = cfg.Carve.StepPct > 0 ? (100 - cfg.Carve.BasePct + cfg.Carve.StepPct - 1) / cfg.Carve.StepPct : 0;
            for (int i = 0; i < marks; i++)
            {
                var sp = UiKit.KitSprite(Ctx, "S-1203", i < w.CarveBonus ? "filled" : "empty");
                if (sp.IsValid) UiKit.Image(_markRow, sp.Sprite, i * 24f, 0f, 16f, 16f);
                else UiKit.Rect(_markRow, i * 24f, 0f, 16f, 16f, i < w.CarveBonus ? UiTheme.PaperText : UiTheme.PaperShade);
            }
            if (b.Blocked == ErrorCode.None) _engrave.SetEnabled();
            else _engrave.SetDisabled(EnhanceNameKeys.ErrorKey(b.Blocked));
        }

        void DoEngrave()
        {
            if (Ctx == null || Ctx.Enhance == null) return;
            Ctx.Sound?.Play(EnhanceSoundIds.EngraveTry, SoundBus.Ui);
            var r = Ctx.Enhance.CarveSlot(_wandId);
            if (!r.Ok) { Screens?.Toast(T(EnhanceNameKeys.ErrorKey(r.Error))); Refresh(); return; }
            Ctx.Sound?.Play(r.Value.Success ? EnhanceSoundIds.EngraveOk : EnhanceSoundIds.EngraveFail, SoundBus.Ui);
            _result.SetText(T(r.Value.Success ? EnhanceNameKeys.EngraveResultOk : EnhanceNameKeys.EngraveResultFail));
            Refresh();
        }

        public override bool OnBack() { Close(); return true; }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            base.OnMenuKey(key);
            return true;
        }
    }
}
