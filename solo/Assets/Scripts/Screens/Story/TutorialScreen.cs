using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>
    /// 튜토리얼(첫 판 전 허수아비 방 5단계, 08 요약서 6.4절). 판을 연 화면이라 전투 입력의 문(InputScreen)을 자기 id로 연다.
    /// 전장이 보여야 하므로 뼈대 없는 Fullscreen 층(전투 화면과 같음)이고, 그리는 것은 풍선(560,200) 800×120 하나다(전장 위 예외 — 08 6절).
    /// 흐름은 TutorialController가 갖는다.
    /// </summary>
    public sealed class TutorialScreen : ScreenBase
    {
        public const string OpenLoadoutKey = "str.tutorial.open_loadout";
        public const string SkipActionKey = "str.tutorial.skip";
        public const string DoneKey = "str.common.done";

        public override ScreenId Id => ScreenId.Tutorial;
        // 전장이 보여야 하므로 뼈대 없이 풍선만 그린다. 층은 전투 화면(Battle)·연습 화면과 같이 Fullscreen이다 —
        // HudOverlay면 아래의 전체 화면(원정 준비가 바뀐 뒤 드러나는 메인 메뉴)이 꺼지지 않아 전장을 가린다(통합 단계 3에서 화면으로 확인).
        public override ScreenLayer Layer => ScreenLayer.Fullscreen;
        protected override bool HasFrame => false;
        /// <summary>메뉴 키가 아래 화면으로 새지 않게 막는다(아래 화면이 남아 있어도 조작되지 않는다).</summary>
        public override bool BlocksInput => true;

        TutorialController _ctl;
        StoryTuning _tuning;
        RectTransform _balloon;
        UiLabel _title, _body, _done, _hint;
        UiButton _open;
        int _shownStep = -1;
        bool _shownBody;

        public TutorialController Controller => _ctl;

        protected override void BuildContent(RectTransform content)
        {
            _tuning = StoryTuning.From(Ctx != null ? Ctx.Content : null);
            _ctl = new TutorialController(Ctx, _tuning);
            var r = _tuning.Balloon;
            _balloon = UiKit.Node(content, "TutorialBalloon", r.X, r.Y, r.W, r.H);
            UiKit.Panel(Ctx, _balloon, UiPanelKind.Board, 0f, 0f, r.W, r.H);
            const float pad = 16f;
            float lineTitle = UiTheme.LineHeight(UiFontId.F24);
            float lineSmall = UiTheme.LineHeight(UiFontId.F16);
            _title = UiKit.Label(Ctx, _balloon, string.Empty, UiFontId.F24, pad, pad, r.W - pad * 2f, lineTitle, UiTheme.Chalk);
            _done = UiKit.Label(Ctx, _balloon, T(DoneKey), UiFontId.F16, pad, pad, r.W - pad * 2f, lineTitle, UiTheme.Good, TextAnchor.MiddleRight);
            _body = UiKit.Label(Ctx, _balloon, string.Empty, UiFontId.F16, pad, pad + lineTitle + 4f, r.W - pad * 2f,
                r.H - lineTitle - lineSmall - pad * 2f - 8f, UiTheme.Chalk, TextAnchor.UpperLeft, true);
            _hint = UiKit.Label(Ctx, _balloon,
                UiKeyHint.Compose(Ctx != null ? Ctx.Names : null, new[] { new UiKeyHint(Key.Escape, SkipActionKey) }),
                UiFontId.F16, pad, r.H - pad - lineSmall, r.W * 0.5f, lineSmall, UiTheme.SlateBlue);
            float bw = 280f, bh = 40f;
            string openText = Ctx != null && Ctx.Names != null
                ? Ctx.Names.Format(OpenLoadoutKey, "key", UiKeyLabels.Of(Ctx.Names, Key.J))
                : OpenLoadoutKey;
            _open = UiKit.Button(Ctx, _balloon, openText, r.W - pad - bw, r.H - pad - bh, bw, bh, OnOpenLoadoutClicked,
                UiButtonStyle.Primary, null, null, UiFontId.F16);
            _balloon.gameObject.SetActive(false);
        }

        void OnOpenLoadoutClicked()
        {
            if (_ctl != null) _ctl.OpenLoadout();
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _shownStep = -1;
            if (_ctl == null) _ctl = new TutorialController(Ctx, _tuning);
            _ctl.Enter(Args);
            Refresh();
        }

        public override void OnExit()
        {
            if (_ctl != null) _ctl.Exit();
            base.OnExit();
        }

        /// <summary>Esc = 건너뛰기 확인 창(일시정지 화면을 쓰지 않는다 — 00 4.1절).</summary>
        public override bool OnBack()
        {
            if (_ctl != null) _ctl.RequestSkip();
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            // 메뉴 키(J·방향)는 같은 물리 키로 전투에도 들어간다. ⑤의 J만 [조립 열기]로 쓰고 나머지는 무시한다.
            if (key == MenuKey.Confirm && _ctl != null && _ctl.Model != null && _ctl.Model.CanOpenLoadout(_ctl.CurrentStep))
            {
                _ctl.OpenLoadout();
                return true;
            }
            return true;
        }

        public override void Tick(float dt)
        {
            if (_ctl == null) return;
            _ctl.Tick();
            Refresh();
        }

        void Refresh()
        {
            if (_balloon == null || _ctl == null) return;
            var b = _ctl.Running ? _ctl.Balloon() : new TutorialBalloon();
            if (_balloon.gameObject.activeSelf != b.Visible) _balloon.gameObject.SetActive(b.Visible);
            if (!b.Visible) { _shownStep = -1; return; }
            bool hasBody = b.BodyKey != null;
            if (b.Step != _shownStep || hasBody != _shownBody)
            {
                _shownStep = b.Step;
                _shownBody = hasBody;
                _title.SetText(T(b.TitleKey));
                _body.SetText(hasBody ? T(b.BodyKey) : string.Empty);
                _body.gameObject.SetActive(hasBody);
            }
            if (_done.gameObject.activeSelf != b.Done) _done.gameObject.SetActive(b.Done);
            if (_open.gameObject.activeSelf != b.ShowOpenLoadout) _open.gameObject.SetActive(b.ShowOpenLoadout);
            if (_hint != null && !_hint.gameObject.activeSelf) _hint.gameObject.SetActive(true);
        }
    }
}
