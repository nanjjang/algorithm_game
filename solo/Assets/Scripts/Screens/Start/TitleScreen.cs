using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 타이틀(ScreenId.Title, 08 3.2절, UA 4.2, 09 7.6절). 배경(S-0201 = backgrounds/title_1920x1080.png), 제목(S-0202, 없으면 글자),
    /// 지금 프로필 표지(항상), [프로필 바꾸기], 시작 안내(2초 주기로 밝기만), 판 번호·만든 이(오른쪽 아래 12px).
    /// 규칙은 TitleModel. J = 시작, 방향 아래·왼쪽 = [프로필 바꾸기]에 커서, 위·오른쪽 = 커서 풀기.
    /// </summary>
    public sealed class TitleScreen : ScreenBase
    {
        const float TitleX = 480f, TitleY = 120f, TitleW = 960f, TitleH = 320f;
        const float BadgeX = 48f, BadgeY = 968f, BadgeW = 200f, BadgeH = 40f;
        const float ChangeX = BadgeX + BadgeW + 16f, ChangeY = 968f, ChangeW = 192f, ChangeH = 40f;
        const float HintX = 760f, HintY = 840f, HintW = 400f, HintH = 48f;
        const float CornerW = 480f, CornerPad = 16f;

        TitleModel _model;
        UiLabel _hint;
        UiLabel _profileLabel;
        UiLabel _version;
        UiButton _change;
        bool _changeArmed;

        public override ScreenId Id => ScreenId.Title;
        protected override bool HasFrame => false;

        protected override void BuildContent(RectTransform content)
        {
            _model = new TitleModel(StartTuning.Load(Ctx != null ? Ctx.Content : null));
            // 바탕은 창 끝까지(UiFullBleed): 단색은 늘이고 그림은 비율대로 덮는다. 제목·단추는 1920×1080 안전 영역 그대로.
            UiFullBleed.Fill(UiKit.Rect(content, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, UiTheme.WoodDark, true, "Background").rectTransform);
            var art = Ctx != null ? Ctx.Art : null;
            if (art != null)
            {
                UiFullBleed.Cover(UiKit.Image(content, art.Background("title"), 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, null, false));
                var titleArt = art.Ui("S-0202", "title_korean");
                if (titleArt.IsValid) UiKit.Image(content, titleArt.Sprite, TitleX, TitleY, TitleW, TitleH);
                else TitleText(content);
            }
            else TitleText(content);

            var badge = art != null ? art.Ui("I-F12", TitleModel.ProfileBadgeVariant(Ctx.Kind)) : default(UiSprite);
            if (badge.IsValid) UiKit.Image(content, badge.Sprite, BadgeX, BadgeY, BadgeW, BadgeH, null, false);
            else UiKit.Panel(Ctx, content, UiPanelKind.Heading, BadgeX, BadgeY, BadgeW, BadgeH);
            // 표지 그림(I-F12)은 왼쪽에 문서 아이콘, 오른쪽이 글자 자리인 종이다 → 글자는 아이콘 오른쪽에 종이 글자색으로.
            _profileLabel = UiKit.Label(Ctx, content, T(TitleModel.ProfileKey(Ctx != null ? Ctx.Kind : ProfileKind.Offline)),
                UiFontId.F16, BadgeX + TitleModel.BadgeTextInset, BadgeY, BadgeW - TitleModel.BadgeTextInset - TitleModel.BadgeTextRight, BadgeH,
                badge.IsValid ? UiTheme.PaperText : UiTheme.Chalk, TextAnchor.MiddleCenter, false, !badge.IsValid);

            _change = UiKit.Button(Ctx, content, T(StartNameKeys.TitleChangeProfile), ChangeX, ChangeY, ChangeW, ChangeH,
                OpenProfileSelect, UiButtonStyle.Small, null, null, UiFontId.F16);

            _hint = UiKit.Label(Ctx, content, T(StartNameKeys.TitleStartHint), UiFontId.F24, HintX, HintY, HintW, HintH,
                UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);

            float lh = UiTheme.LineHeight(UiFontId.F12);
            float bottom = UiScale.RefHeight - CornerPad;
            _version = UiKit.Label(Ctx, content, TitleModel.BuildVersionText(Ctx != null ? Ctx.Names : null, Ctx != null ? Ctx.BuildVersion : null),
                UiFontId.F12, UiScale.RefWidth - CornerPad - CornerW, bottom - 2f * lh, CornerW, lh, UiTheme.SlateBlue, TextAnchor.MiddleRight, false, true);
            UiKit.Label(Ctx, content, T(StartNameKeys.TitleCredits), UiFontId.F12, UiScale.RefWidth - CornerPad - CornerW, bottom - lh,
                CornerW, lh, UiTheme.SlateBlue, TextAnchor.MiddleRight, false, true);
        }

        void TitleText(RectTransform content)
        {
            UiKit.Label(Ctx, content, T(StartNameKeys.TitleTitle), UiFontId.F64, TitleX, TitleY, TitleW, TitleH,
                UiTheme.Chalk, TextAnchor.MiddleCenter, true, true);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            SetArmed(false);
            if (_version != null && Ctx != null) _version.SetText(TitleModel.BuildVersionText(Ctx.Names, Ctx.BuildVersion));
            if (_profileLabel != null && Ctx != null) _profileLabel.SetText(T(TitleModel.ProfileKey(Ctx.Kind)));
            // ProfileRestored가 참이면 저장 되돌림 창을 한 번(인자에서 지운다). PendingSettled는 남겨 MainMenu로 넘긴다.
            if (_model.OnEnter(Args)) Screens?.Push(ScreenId.SaveRestored, ScreenArgs.From(ScreenId.Title));
        }

        public override void Tick(float dt)
        {
            if (_model == null) return;
            if (_model.Advance(dt, out float b)) PlayUi(StartSounds.TitleStartPromptPulse);
            if (_hint != null) _hint.Color = new Color(UiTheme.Chalk.r * b, UiTheme.Chalk.g * b, UiTheme.Chalk.b * b, 1f);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            switch (key)
            {
                case MenuKey.Confirm:
                    if (_changeArmed) { PlayUi(UiSounds.Press); OpenProfileSelect(); }
                    else StartGame();
                    return true;
                case MenuKey.Down:
                case MenuKey.Left:
                    if (!_changeArmed) { SetArmed(true); PlayUi(UiSounds.CursorMove); }
                    return true;
                case MenuKey.Up:
                case MenuKey.Right:
                    if (_changeArmed) { SetArmed(false); PlayUi(UiSounds.CursorMove); }
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>타이틀은 맨 아래 화면이라 Esc로 닫지 않는다.</summary>
        public override bool OnBack() => true;

        void SetArmed(bool on)
        {
            _changeArmed = on;
            if (_change != null) _change.Cursor = on;
        }

        void StartGame()
        {
            var nav = _model.Start(Ctx != null ? Ctx.Profile : null, Args);
            if (nav.Kind == StartNavKind.None) return;
            PlayUi(StartSounds.TitleStart);
            nav.Apply(Screens, this);
            // OfflineGuide를 위에 쌓았으면 타이틀은 남는다 — Esc로 돌아오면 다시 J를 받는다.
            if (nav.Kind == StartNavKind.Push) _model.Rearm();
        }

        void OpenProfileSelect()
        {
            SetArmed(false);
            _model.ChangeProfile(Args).Apply(Screens, this);
        }
    }
}
