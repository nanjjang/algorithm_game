using UnityEngine;
using Wizard.Solo.Art;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 예/아니오 확인 창(ScreenId.ConfirmDialog, 560×320, 08 1.6절). IScreenManager.Confirm이 연다.
    /// 인자: TitleKey(물음 한 줄, 없으면 str.confirm_dialog.title), BodyKey(설명 두 줄) 또는 Text(이미 만든 설명 글), Caution(bool).
    /// 조심할 일(caution)은 [예]가 조심 버튼(호박색 테 + 경고 삼각)이고 처음 커서는 [아니오]에 둔다[임시].
    /// [예] = Pop(true), [아니오]·Esc·K = Pop(false). 결과는 args.OnResult로 간다(Confirm의 onResult(bool)).
    /// </summary>
    public sealed class UiConfirmDialog : ScreenBase
    {
        public const float Width = 560f, Height = 320f;
        UiLabel _title, _body;
        UiButton _yes, _yesCaution, _no;

        public override ScreenId Id => ScreenId.ConfirmDialog;
        public override ScreenLayer Layer => ScreenLayer.Modal;
        public override bool BlocksInput => true;

        public bool Caution { get; private set; }
        public string TitleText => _title != null ? _title.FullText : null;
        public string BodyText => _body != null ? _body.FullText : null;

        protected override void BuildContent(RectTransform content)
        {
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Paper, 0f, 0f, Width, Height);
            var prt = panel.rectTransform;
            UiKit.Center(prt, Width, Height);
            panel.name = "ConfirmDialog";
            const float Pad = 32f;
            float th = UiTheme.LineHeight(UiFontId.F24);
            _title = UiKit.Label(Ctx, prt, string.Empty, UiFontId.F24, Pad, Pad, Width - 2f * Pad, th, UiTheme.PaperText, TextAnchor.MiddleLeft);
            float bh = UiTheme.LineHeight(UiFontId.F16) * 2f;
            _body = UiKit.Label(Ctx, prt, string.Empty, UiFontId.F16, Pad, Pad + th + 24f, Width - 2f * Pad, bh, UiTheme.PaperText,
                TextAnchor.UpperLeft, wrap: true);
            const float BtnW = 200f, BtnH = 64f, Gap = 32f;
            float bx = (Width - (2f * BtnW + Gap)) * 0.5f, by = Height - Pad - BtnH;
            _yes = UiKit.Button(Ctx, prt, T(UiNameKeys.CommonYes), bx, by, BtnW, BtnH, () => Answer(true), UiButtonStyle.Large);
            _yesCaution = UiKit.Button(Ctx, prt, T(UiNameKeys.CommonYes), bx, by, BtnW, BtnH, () => Answer(true), UiButtonStyle.Caution);
            _no = UiKit.Button(Ctx, prt, T(UiNameKeys.CommonNo), bx + BtnW + Gap, by, BtnW, BtnH, () => Answer(false), UiButtonStyle.Large);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Caution = Args.Get(ScreenArgKeys.Caution, false);
            string titleKey = Args.Get<string>(ScreenArgKeys.TitleKey);
            _title.SetText(T(string.IsNullOrEmpty(titleKey) ? UiNameKeys.ConfirmTitle : titleKey));
            string text = Args.Get<string>(ScreenArgKeys.Text);
            string bodyKey = Args.Get<string>(ScreenArgKeys.BodyKey);
            _body.SetText(!string.IsNullOrEmpty(text) ? text : string.IsNullOrEmpty(bodyKey) ? string.Empty : T(bodyKey));
            _yes.gameObject.SetActive(!Caution);
            _yesCaution.gameObject.SetActive(Caution);
            Focus.Clear();
            var yes = Caution ? _yesCaution : _yes;
            Focus.Add(yes);
            Focus.Add(_no);
            Focus.Wrap = true;
            Focus.SetCursor(Caution ? _no : yes, false);
        }

        void Answer(bool yes) => Close(yes);

        public override bool OnBack()
        {
            Answer(false);
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Answer(false); return true; }
            base.OnMenuKey(key);
            return true; // 창은 키를 아래로 흘리지 않는다
        }
    }
}
