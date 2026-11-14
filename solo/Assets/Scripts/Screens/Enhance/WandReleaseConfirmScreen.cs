using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Inventory;
using Wizard.Solo.UI;
using UInputField = UnityEngine.UI.InputField;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 떠나보내기 확인 창(08 10.2절, 07 3.5절) 640×400. 인자 WandIds(List&lt;long&gt;).
    /// 한 자루: 돌려받는 골드, 돌아오는 것, 되돌릴 수 없음, +7 이상은 지팡이 이름을 그대로 입력해야 [떠나보내기](조심 버튼)가 켜진다(TV-149).
    /// 여러 자루: "n자루 · 합계 g 골드". 확인은 한 번. 결과는 Pop(true/false)로 고르기 화면에 돌아간다.
    /// </summary>
    public sealed class WandReleaseConfirmScreen : ScreenBase
    {
        public const float Width = 640f, Height = 400f;
        EnhanceScreenTuning _t;
        UiLabel _title, _refund, _returns, _irrev, _typeName;
        UInputField _input;
        UiButton _confirm, _cancel;
        readonly List<WandData> _wands = new List<WandData>();

        public override ScreenId Id => ScreenId.WandReleaseConfirm;
        public override ScreenLayer Layer => ScreenLayer.Modal;
        public override bool BlocksInput => true;

        protected override void BuildContent(RectTransform content)
        {
            _t = EnhanceScreenTuning.From(Ctx != null ? Ctx.Content : null);
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Paper, 0f, 0f, Width, Height);
            var p = panel.rectTransform;
            UiKit.Center(p, Width, Height);
            panel.name = "WandReleaseConfirm";
            const float Pad = 32f;
            float w = Width - 2 * Pad;
            _title = UiKit.LabelKey(Ctx, p, EnhanceNameKeys.ReleaseTitle, UiFontId.F24, Pad, Pad, w, 0f, UiTheme.PaperText);
            _refund = UiKit.Label(Ctx, p, string.Empty, UiFontId.F16, Pad, 88f, w, 0f, UiTheme.PaperText);
            _returns = UiKit.LabelKey(Ctx, p, EnhanceNameKeys.ReleaseReturns, UiFontId.F16, Pad, 120f, w, 48f, UiTheme.PaperText, TextAnchor.UpperLeft, true);
            _irrev = UiKit.LabelKey(Ctx, p, EnhanceNameKeys.ReleaseIrreversible, UiFontId.F16, Pad, 172f, w, 0f, UiTheme.PaperText);
            _typeName = UiKit.LabelKey(Ctx, p, EnhanceNameKeys.ReleaseTypeName, UiFontId.F16, Pad, 208f, w, 0f, UiTheme.PaperText);
            _input = BuildInput(p, Pad, 236f, w, 40f);
            const float BtnW = 240f, BtnH = 64f;
            float by = Height - Pad - BtnH;
            _confirm = UiKit.Button(Ctx, p, T(EnhanceNameKeys.ReleaseConfirm), Pad, by, BtnW, BtnH, DoRelease, UiButtonStyle.Caution, Focus,
                EnhanceSoundIds.Release);
            _cancel = UiKit.Button(Ctx, p, T(EnhanceNameKeys.ReleaseCancel), Width - Pad - BtnW, by, BtnW, BtnH, () => Close(false), UiButtonStyle.Large, Focus);
        }

        UInputField BuildInput(RectTransform parent, float x, float y, float w, float h)
        {
            var bg = UiKit.NineSlice(Ctx, parent, "K-33", "input", null, x, y, w, h, UiTheme.Chalk);
            bg.raycastTarget = true;
            var go = bg.gameObject;
            var field = go.AddComponent<UInputField>();
            var text = UiKit.Label(Ctx, bg.rectTransform, string.Empty, UiFontId.F16, 8f, 0f, w - 16f, h, UiTheme.PaperText);
            var ut = text.GetComponent<UText>();
            ut.supportRichText = false;
            field.textComponent = ut;
            field.lineType = UInputField.LineType.SingleLine;
            field.onValueChanged.AddListener(_ => RefreshConfirm());
            return field;
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _wands.Clear();
            var ids = Args.Get<List<long>>(ScreenArgKeys.WandIds);
            var p = Ctx != null ? Ctx.Profile : null;
            if (ids != null && p != null)
                foreach (long id in ids) { var w = p.Wand(id); if (w != null) _wands.Add(w); }
            var econ = Ctx != null && Ctx.Content != null ? Ctx.Content.Economy : null;
            long refund = 0;
            foreach (var w in _wands) refund += WandPickerModel.RefundOf(econ, w);
            bool multi = _wands.Count > 1;
            _refund.SetText(multi
                ? F(EnhanceNameKeys.ReleaseMultiSummary, "n", _wands.Count, "gold", N(refund))
                : F(EnhanceNameKeys.ReleaseRefund, "gold", N(refund)));
            bool needName = WandReleaseRules.NeedsName(_t, _wands);
            _typeName.gameObject.SetActive(needName);
            _input.gameObject.SetActive(needName);
            _input.text = string.Empty;
            RefreshConfirm();
            Focus.SetCursor(_cancel, false);
            // 키보드만으로도 되게 이름 칸에 바로 들어간다(Enter·Esc로 칸을 떠나면 메뉴 키가 돌아온다).
            if (needName) _input.ActivateInputField();
        }

        void RefreshConfirm()
        {
            bool ok = WandReleaseRules.CanConfirm(_t, _wands, _input != null ? _input.text : null, Ctx != null ? Ctx.Names : null);
            if (ok) _confirm.SetEnabled(); else _confirm.SetDisabled(EnhanceNameKeys.ErrorKey(ErrorCode.NameConfirmRequired));
        }

        public override void Tick(float dt)
        {
            // 이름 입력 칸에 글을 치는 동안 J·K·방향이 메뉴로 새지 않게 메뉴 키를 잡아 둔다(Esc 포함 — 칸을 떠나면 풀린다).
            if (Screens != null && _input != null && _input.gameObject.activeInHierarchy)
                Screens.CaptureKeys = _input.isFocused;
        }

        void DoRelease()
        {
            if (Ctx == null || Ctx.Wands == null || _wands.Count == 0) return;
            var ids = new List<long>();
            foreach (var w in _wands) ids.Add(w.Id);
            string typed = _input != null ? _input.text : null;
            // 계약의 IWandService.Release(ids)는 이름을 받지 않아 +7 이상은 NameConfirmRequired다 — 이름을 받는 확장(G13)을 쓴다.
            Result<long> r = Ctx.Wands.Release(ids, typed);
            if (Screens != null) Screens.CaptureKeys = false;
            if (!r.Ok)
            {
                Screens?.Toast(T(EnhanceNameKeys.ErrorKey(r.Error)));
                return;
            }
            Close(true);
        }

        public override void OnExit()
        {
            if (Screens != null) Screens.CaptureKeys = false;
            base.OnExit();
        }

        public override bool OnBack() { Close(false); return true; }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(false); return true; }
            base.OnMenuKey(key);
            return true;
        }
    }
}
