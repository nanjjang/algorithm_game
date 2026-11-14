using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 첫 안내(08 10.11절, 07 5.5절): 가운데 800×480, 4쪽. 본문 열쇠 str.enhance_tutorial.page.N.body — 값이 없으면 줄을 숨긴다(N-05).
    /// 닫을 때 flags.enhance_tutorial_shown = true + 저장(한 번만). 인자 NextScreen이 있으면 닫은 뒤 그 화면으로 Replace,
    /// 없으면 Pop(밑의 WandPicker·Enhance가 그대로 있다).
    /// </summary>
    public sealed class EnhanceTutorialScreen : ScreenBase
    {
        public const float Width = 800f, Height = 480f;
        EnhanceTutorialModel _model;
        UiLabel _page, _body;
        UiButton _prev, _next, _close;

        public override ScreenId Id => ScreenId.EnhanceTutorial;
        public override ScreenLayer Layer => ScreenLayer.Modal;
        public override bool BlocksInput => true;
        public EnhanceTutorialModel Model => _model;

        protected override void BuildContent(RectTransform content)
        {
            var tuning = EnhanceScreenTuning.From(Ctx != null ? Ctx.Content : null);
            _model = new EnhanceTutorialModel(tuning.TutorialPages);
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Paper, 0f, 0f, Width, Height);
            var p = panel.rectTransform;
            UiKit.Center(p, Width, Height);
            panel.name = "EnhanceTutorial";
            const float Pad = 32f;
            UiKit.LabelKey(Ctx, p, EnhanceNameKeys.TutorialTitle, UiFontId.F32, Pad, Pad, Width - 2 * Pad - 160f, UiTheme.LineHeight(UiFontId.F32), UiTheme.PaperText);
            _page = UiKit.Label(Ctx, p, string.Empty, UiFontId.F16, Width - Pad - 160f, Pad + 8f, 160f, 0f, UiTheme.PaperText, TextAnchor.MiddleRight);
            _body = UiKit.Label(Ctx, p, string.Empty, UiFontId.F16, Pad, 112f, Width - 2 * Pad, 240f, UiTheme.PaperText, TextAnchor.UpperLeft, wrap: true);
            const float BtnW = 200f, BtnH = 64f, Gap = 24f;
            float by = Height - Pad - BtnH;
            _prev = UiKit.Button(Ctx, p, T(EnhanceNameKeys.CommonPrev), Pad, by, BtnW, BtnH, () => Turn(false), UiButtonStyle.Large, Focus);
            _next = UiKit.Button(Ctx, p, T(EnhanceNameKeys.CommonNext), Width - Pad - 2 * BtnW - Gap, by, BtnW, BtnH, () => Turn(true), UiButtonStyle.Large, Focus);
            _close = UiKit.Button(Ctx, p, T(EnhanceNameKeys.CommonClose), Width - Pad - BtnW, by, BtnW, BtnH, CloseTutorial, UiButtonStyle.Primary, Focus);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _model.Reset();
            Refresh();
            Focus.SetCursor(_next, false);
        }

        void Turn(bool forward)
        {
            if (forward ? _model.Next() : _model.Prev()) Refresh();
        }

        void Refresh()
        {
            _page.SetText(_model.PageLabel(Ctx != null ? Ctx.Names : null));
            string body = _model.Body(Ctx != null ? Ctx.Names : null);
            _body.gameObject.SetActive(body != null);
            _body.SetText(body ?? string.Empty);
            if (_model.IsFirst) _prev.SetDisabled(null); else _prev.SetEnabled();
            if (_model.IsLast) _next.SetDisabled(null); else _next.SetEnabled();
        }

        void CloseTutorial()
        {
            if (Ctx == null) return;
            _model.Close(Ctx.Profile, Ctx.Profiles);
            var next = Args.Get(ScreenArgKeys.NextScreen, ScreenId.None);
            if (next != ScreenId.None) Screens?.Replace(next, Args.Get<ScreenArgs>(ScreenArgKeys.NextArgs));
            else Close();
        }

        public override bool OnBack()
        {
            CloseTutorial();
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { if (!_model.IsFirst) Turn(false); else CloseTutorial(); return true; }
            if (key == MenuKey.PrevTab || key == MenuKey.Left && Focus.Current == null) { Turn(false); return true; }
            if (key == MenuKey.NextTab) { Turn(true); return true; }
            base.OnMenuKey(key);
            return true;
        }
    }
}
