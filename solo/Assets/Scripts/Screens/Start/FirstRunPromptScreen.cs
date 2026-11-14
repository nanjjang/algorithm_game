using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 첫 실행 물음(ScreenId.FirstRunPrompt, 08 3.1절, UA 4.1). 바탕 나무 어두움 단색, 가운데 판 880×480, 물음 한 줄,
    /// 선택지 셋(각 256×160, 간격 24). 규칙은 FirstRunPromptModel. 번쩍임·흔들림 연출을 하나도 두지 않는다(TV-002).
    /// </summary>
    public sealed class FirstRunPromptScreen : ScreenBase
    {
        const float BoardW = 880f, BoardH = 480f, OptW = 256f, OptH = 160f, OptGap = 24f, Pad = 40f;

        readonly FirstRunPromptModel _model = new FirstRunPromptModel();
        readonly UiButton[] _options = new UiButton[3];

        public override ScreenId Id => ScreenId.FirstRunPrompt;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background");
            var board = UiKit.Panel(Ctx, content, UiPanelKind.Board, 0f, 0f, BoardW, BoardH).rectTransform;
            UiKit.Center(board, BoardW, BoardH);
            board.name = "FirstRunBoard";
            float th = UiTheme.LineHeight(UiFontId.F32);
            UiKit.Label(Ctx, board, T(StartNameKeys.FirstRunTitle), UiFontId.F32, Pad, Pad + 24f, BoardW - 2f * Pad, th,
                UiTheme.Chalk, TextAnchor.MiddleCenter);

            float total = OptW * _options.Length + OptGap * (_options.Length - 1);
            float x0 = (BoardW - total) * 0.5f;
            float y = BoardH - Pad - OptH - 48f;
            for (int i = 0; i < _options.Length; i++)
            {
                int level = i;
                var b = UiKit.Button(Ctx, board, null, x0 + i * (OptW + OptGap), y, OptW, OptH, () => Pick(level),
                    UiButtonStyle.Plain, Focus, "");
                var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                UiKit.Panel(Ctx, root, UiPanelKind.Recess, 0f, 0f, OptW, OptH);
                float nh = UiTheme.LineHeight(UiFontId.F24);
                UiKit.Label(Ctx, root, T(FirstRunPromptModel.OptionKeys[i]), UiFontId.F24, 16f, 20f, OptW - 32f, nh,
                    UiTheme.Chalk, TextAnchor.MiddleCenter);
                UiKit.Label(Ctx, root, T(FirstRunPromptModel.OptionDescKeys[i]), UiFontId.F16, 16f, 20f + nh + 12f, OptW - 32f,
                    UiTheme.LineHeight(UiFontId.F16) * 3f, UiTheme.SlateBlue, TextAnchor.UpperCenter, wrap: true);
                _options[i] = b;
            }
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _model.Reset();
            Focus.SetCursor(FirstRunPromptModel.DefaultCursor, false);
        }

        void Pick(int level)
        {
            var nav = _model.Choose(Ctx != null ? Ctx.Settings : null, level, Args);
            if (nav.Kind == StartNavKind.None) return;
            PlayUi(StartSounds.FirstRunChoice);
            nav.Apply(Screens, this);
        }

        /// <summary>Esc: 저장하지 않는다 → 다음 실행에 다시 묻는다 [임시]. 이번에는 타이틀로 넘어간다.</summary>
        public override bool OnBack()
        {
            _model.Skip(Args).Apply(Screens, this);
            return true;
        }
    }
}
