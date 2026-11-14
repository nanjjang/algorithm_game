using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Start
{
    /// <summary>
    /// 메인 메뉴(ScreenId.MainMenu, 08 4절, UA 4.5). 위 띠(프로필 표지, 칭호, 골드), 원정 카드(48,128 400×520),
    /// 서버 전용 묶음 상자(결투·광장 카드 자리, S-0401), 메뉴 목록(1344,128 528×760, 줄 48 간격 16), [나가기](1680,968 192×48).
    /// 배경 S-0501 + 모닥불, 가운데 지금 고른 캐릭터(×4)와 든 지팡이, 기기 시각의 낮/해질녘/밤 색.
    /// 규칙은 MainMenuModel. 들어올 때 PendingSettled가 있으면 자동 정산 알림 창을 한 번 띄운다(D-68, PendingSettledPanel).
    /// </summary>
    public sealed class MainMenuScreen : ScreenBase
    {
        // 08 4.1절 위치(화면 기준). 내용 칸은 y 80부터라 TopBarHeight를 뺀다.
        const float CardX = 48f, CardY = 128f, CardW = 400f, CardH = 520f, CardGap = 24f;
        const float CardArtW = 368f, CardArtH = 240f, CardPad = 16f;
        const float ListX = 1344f, ListY = 128f, ListW = 528f, RowH = 48f, RowGap = 16f;
        const float ExitX = 1680f, ExitY = 968f, ExitW = 192f, ExitH = 48f;
        const float BadgeW = 200f, BadgeH = 40f;
        const int CharScale = 4, CharCell = 64, WandCellW = 32, WandCellH = 64;
        const float PedestalW = 320f, PedestalH = 96f, CampfireSize = 128f;

        MainMenuModel _model;
        StartTuning _tuning;
        readonly Dictionary<MainMenuAction, UiButton> _buttons = new Dictionary<MainMenuAction, UiButton>();
        UiButton _expedition;
        UiLabel _bestWave, _tracker, _gold, _equippedTitle, _profileLabel;
        UImage _bgImage, _charImage, _wandImage, _campfire;
        SpriteAnim _charAnim, _fireAnim;
        float _animClock, _clockPoll;
        MenuTimeOfDay _tod;
        bool _todKnown;
        PendingSettledPanel _pending;
        bool _subscribed;
        int _shownChar = -1, _shownStage = -1;

        public override ScreenId Id => ScreenId.MainMenu;

        protected override void BuildContent(RectTransform content)
        {
            _tuning = StartTuning.Load(Ctx != null ? Ctx.Content : null);
            _model = MainMenuModel.Build(Ctx != null ? Ctx.Profile : null, Ctx != null ? Ctx.Content : null, RunActive());
            BuildBackdrop();
            BuildTopBar();

            float top = TopBarHeight;
            // 원정 카드
            _expedition = UiKit.Button(Ctx, content, null, CardX, CardY - top, CardW, CardH, () => Open(_model.Expedition),
                UiButtonStyle.Plain, Focus);
            var er = _expedition.ContentRoot != null ? _expedition.ContentRoot : _expedition.Rect;
            UiKit.OrnatePanel(Ctx, er, 0f, 0f, CardW, CardH, 1).raycastTarget = false;
            CardArt(er, "mode_expedition", CardPad, CardPad);
            UiKit.AccentRule(er, CardPad, CardPad + CardArtH - 3f, CardArtW);
            float y = CardPad + CardArtH + CardPad;
            float h32 = UiTheme.LineHeight(UiFontId.F32), h16 = UiTheme.LineHeight(UiFontId.F16);
            UiKit.DropShadow(UiKit.Label(Ctx, er, T(StartNameKeys.MainMenuExpedition), UiFontId.F32, CardPad, y, CardArtW, h32), 2f);
            y += h32 + 8f;
            UiKit.Label(Ctx, er, T(StartNameKeys.MainMenuExpeditionSub), UiFontId.F16, CardPad, y, CardArtW, h16 * 2f,
                UiTheme.SlateBlue, TextAnchor.UpperLeft, wrap: true);
            y += h16 * 2f + 16f;
            _bestWave = UiKit.Label(Ctx, er, string.Empty, UiFontId.F16, CardPad, y, CardArtW, h16);
            y += h16 + 8f;
            _tracker = UiKit.Label(Ctx, er, string.Empty, UiFontId.F16, CardPad, y, CardArtW, h16);
            _buttons[MainMenuAction.Expedition] = _expedition;

            // 서버 전용 묶음 상자(결투·광장 카드 둘의 자리). 흐리게 그리지 않는다(TV-026).
            BuildServerGroup(content, CardX + CardW + CardGap, CardY - top, CardW * 2f + CardGap, CardH);

            // 메뉴 목록
            for (int i = 0; i < _model.Items.Count; i++)
            {
                var e = _model.Items[i];
                var b = UiKit.Button(Ctx, content, T(e.NameKey), ListX, ListY - top + i * (RowH + RowGap), ListW, RowH, () => Open(e),
                    UiButtonStyle.Medium, Focus);
                _buttons[e.Action] = b;
                // 연출: 메뉴 줄이 오른쪽에서 차례로 들어온다.
                UiMotion.Entrance(b, 0.08f + i * UiTheme.StaggerSeconds, UiTheme.PanelPopSeconds, new Vector2(40f, 0f), 1f);
            }
            UiMotion.Entrance(_expedition, 0.04f, UiTheme.PanelPopSeconds + 0.06f, new Vector2(0f, -20f), 1f);
            var exitEntry = _model.Exit;
            _buttons[MainMenuAction.Exit] = UiKit.Button(Ctx, content, T(exitEntry.NameKey), ExitX, ExitY - top, ExitW, ExitH,
                () => Open(exitEntry), UiButtonStyle.Medium, Focus);

            Focus.CursorChanged = prev =>
            {
                if (_expedition != null && ReferenceEquals(Focus.Current, _expedition)) PlayUi(StartSounds.MainMenuModeCardFocus);
            };

            _pending = new PendingSettledPanel(Ctx, Root, new UiFocusGroup(Manager != null ? Manager.InputMode : null, Ctx != null ? Ctx.Sound : null));
        }

        void CardArt(RectTransform parent, string variant, float x, float y)
        {
            var sp = Ctx != null && Ctx.Art != null ? Ctx.Art.Ui("S-0502", variant) : default(UiSprite);
            if (sp.IsValid) UiKit.Image(parent, sp.Sprite, x, y, CardArtW, CardArtH, null, false);
            else UiKit.Panel(Ctx, parent, UiPanelKind.Recess, x, y, CardArtW, CardArtH);
        }

        void BuildServerGroup(RectTransform content, float x, float y, float w, float h)
        {
            var e = _model.ServerOnly;
            var b = UiKit.Button(Ctx, content, null, x, y, w, h, () => Open(e), UiButtonStyle.Plain, Focus, StartSounds.OfflineGroupBoxOpen);
            var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
            UiKit.NineSlice(Ctx, r, "S-0401", "server_features_group", null, 0f, 0f, w, h, UiTheme.Wood);
            const float Pad = 24f;
            // 묶음 상자 그림(S-0401)은 종이색이다 → 글자는 종이 글자색. 흰 글자면 흐려 보여 "흐리게 두지 않는다"(TV-026)를 어긴다.
            Color ink = UiTheme.PaperText;
            float colW = (w - 3f * Pad) * 0.5f;
            float artW = Mathf.Min(CardArtW, colW);
            CardArtSized(r, "mode_duel", Pad + (colW - artW) * 0.5f, Pad, artW);
            CardArtSized(r, "mode_square", 2f * Pad + colW + (colW - artW) * 0.5f, Pad, artW);
            float ny = Pad + CardArtH + 8f;
            float h24 = UiTheme.LineHeight(UiFontId.F24), h16 = UiTheme.LineHeight(UiFontId.F16);
            UiKit.Label(Ctx, r, T(MainMenuModel.ServerOnlyKeys[0]), UiFontId.F24, Pad, ny, colW, h24, ink, TextAnchor.MiddleCenter);
            UiKit.Label(Ctx, r, T(MainMenuModel.ServerOnlyKeys[1]), UiFontId.F24, 2f * Pad + colW, ny, colW, h24, ink, TextAnchor.MiddleCenter);
            ny += h24 + 24f;
            int rest = MainMenuModel.ServerOnlyKeys.Length - 2;
            float cellW = (w - 2f * Pad) / rest;
            for (int i = 0; i < rest; i++)
                UiKit.Label(Ctx, r, T(MainMenuModel.ServerOnlyKeys[2 + i]), UiFontId.F16, Pad + i * cellW, ny, cellW, h16, ink,
                    TextAnchor.MiddleCenter);
            ny += h16 + 32f;
            UiKit.Label(Ctx, r, T(StartNameKeys.MainMenuServerOnly), UiFontId.F24, Pad, ny, w - 2f * Pad, h24, ink, TextAnchor.MiddleCenter);
            ny += h24 + 8f;
            UiKit.Label(Ctx, r, T(StartNameKeys.MainMenuOfflineGuide), UiFontId.F16, Pad, ny, w - 2f * Pad, h16, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            _buttons[MainMenuAction.ServerOnly] = b;
        }

        void CardArtSized(RectTransform parent, string variant, float x, float y, float w)
        {
            var sp = Ctx != null && Ctx.Art != null ? Ctx.Art.Ui("S-0502", variant) : default(UiSprite);
            if (sp.IsValid) UiKit.Image(parent, sp.Sprite, x, y, w, CardArtH, null, true);
            else UiKit.Panel(Ctx, parent, UiPanelKind.Recess, x, y, w, CardArtH);
        }

        void BuildBackdrop()
        {
            var back = UiKit.Node(Root, "MenuBackdrop", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            back.SetSiblingIndex(1); // 바탕 단색 바로 위, 위 띠·내용 아래
            var art = Ctx != null ? Ctx.Art : null;
            if (art == null) return;
            var bg = art.Ui("S-0501", "camp_background");
            _bgImage = UiKit.Image(back, bg.IsValid ? bg.Sprite : art.Background("S-0501"), 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, null, false);
            // 모닥불: manifest composite_anchor (1236,780), sprite_origin (64,100).
            var fireInfo = art.UiInfo("S-0501", "campfire");
            _fireAnim = art.UiAnim("S-0501", "campfire");
            if (_fireAnim != null && _fireAnim.Frames != null && _fireAnim.Frames.Length > 0)
            {
                Vector2 tl = FireTopLeft(fireInfo);
                _campfire = UiKit.Image(back, _fireAnim.Frames[0], tl.x, tl.y, CampfireSize, CampfireSize, null, false);
            }
            float cs = CharCell * CharScale;
            float cx = (UiScale.RefWidth - cs) * 0.5f;
            float cy = UiScale.RefHeight - BottomBarHeight - cs - PedestalH * 0.5f;
            var ped = art.Ui("S-0503", "dirt_pedestal");
            if (ped.IsValid)
                UiKit.Image(back, ped.Sprite, (UiScale.RefWidth - PedestalW) * 0.5f, cy + cs - PedestalH * 0.5f, PedestalW, PedestalH, null, false);
            _charImage = UiKit.Image(back, null, cx, cy, cs, cs, Color.white, false);
            _charImage.enabled = false;
            _wandImage = UiKit.Image(back, null, cx, cy, WandCellW * CharScale, WandCellH * CharScale, Color.white, false);
            _wandImage.enabled = false;
        }

        /// <summary>
        /// 모닥불 자리: ui manifest의 S-0501 campfire 항목 composite_anchor (1236,780) − sprite_origin.
        /// UiEntry가 composite_anchor를 싣지 않아(계약 추가 요청으로 올림) 기준점만 여기 적고 sprite_origin은 manifest에서 읽는다.
        /// </summary>
        const int FireAnchorX = 1236, FireAnchorY = 780;

        static Vector2 FireTopLeft(UiEntry info)
        {
            int ox = (int)(CampfireSize * 0.5f), oy = (int)CampfireSize;
            if (info != null && info.HasOrigin) { ox = info.SpriteOrigin.X; oy = info.SpriteOrigin.Y; }
            return new Vector2(FireAnchorX - ox, FireAnchorY - oy);
        }

        void BuildTopBar()
        {
            if (TopBar == null) return;
            var art = Ctx != null ? Ctx.Art : null;
            float by = (TopBarHeight - BadgeH) * 0.5f;
            var badge = art != null ? art.Ui("I-F12", TitleModel.ProfileBadgeVariant(Ctx.Kind)) : default(UiSprite);
            if (badge.IsValid) UiKit.Image(TopBar, badge.Sprite, 48f, by, BadgeW, BadgeH, null, false);
            else UiKit.Panel(Ctx, TopBar, UiPanelKind.Heading, 48f, by, BadgeW, BadgeH);
            // 표지 그림(I-F12)은 왼쪽에 문서 아이콘, 오른쪽이 글자 자리인 종이다 → 글자는 아이콘 오른쪽에 종이 글자색으로.
            _profileLabel = UiKit.Label(Ctx, TopBar, T(TitleModel.ProfileKey(Ctx != null ? Ctx.Kind : ProfileKind.Offline)), UiFontId.F16,
                48f + TitleModel.BadgeTextInset, by, BadgeW - TitleModel.BadgeTextInset - TitleModel.BadgeTextRight, BadgeH,
                badge.IsValid ? UiTheme.PaperText : UiTheme.Chalk, TextAnchor.MiddleCenter, false, !badge.IsValid);
            _equippedTitle = UiKit.Label(Ctx, TopBar, string.Empty, UiFontId.F16, 48f + BadgeW + 24f, by, 480f, BadgeH, UiTheme.Chalk);
            if (TitleLabel != null)
            {
                TitleLabel.SetText(T(StartNameKeys.MainMenuTitle));
                TitleLabel.Text.alignment = TextAnchor.MiddleCenter;
            }
            const float GoldW = 320f, IconS = 24f;
            float gx = UiScale.RefWidth - 48f - GoldW;
            if (art != null) UiKit.Image(TopBar, art.Icon("ui:I-C2/gold", 24), gx, (TopBarHeight - IconS) * 0.5f, IconS, IconS, null, false);
            _gold = UiKit.Label(Ctx, TopBar, string.Empty, UiFontId.F24, gx + IconS + 8f, by, GoldW - IconS - 8f, BadgeH, UiTheme.Gold,
                TextAnchor.MiddleRight);
        }

        bool RunActive() => Ctx != null && Ctx.Session != null && Ctx.Session.HasRun;
        /// <summary>마지막 Refresh 때 판이 열려 있었나.</summary>
        bool _refreshedWithRun;

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            if (!_subscribed && Ctx != null && Ctx.Profiles != null) { Ctx.Profiles.Saved += Refresh; _subscribed = true; }
            Refresh();
            _todKnown = false;
            UpdateTimeOfDay();
            Focus.SetCursor(_expedition, false);
            // 자동 정산 알림: 인자는 한 번 쓰고 버린다(D-68).
            var pending = StartFlow.TakePending(Args);
            if (pending != null)
            {
                _pending.Show(pending);
                PlayUi(UiSounds.ScreenOpen);
            }
        }

        public override void OnExit()
        {
            if (_subscribed && Ctx != null && Ctx.Profiles != null) Ctx.Profiles.Saved -= Refresh;
            _subscribed = false;
            _pending?.Hide();
            base.OnExit();
        }

        /// <summary>프로필 → 글자·항목. 저장될 때마다 다시 그린다(아래 화면에서 돌아와도 OnEnter가 다시 오지 않으므로).</summary>
        void Refresh()
        {
            if (_model == null) return;
            var p = Ctx != null ? Ctx.Profile : null;
            _refreshedWithRun = RunActive();
            _model = MainMenuModel.Build(p, Ctx != null ? Ctx.Content : null, _refreshedWithRun);
            if (_bestWave != null) _bestWave.SetText(_model.BestWaveLine(Ctx != null ? Ctx.Names : null));
            if (_tracker != null)
            {
                string line = _model.TrackerLine(Ctx != null ? Ctx.Names : null);
                _tracker.SetText(line ?? string.Empty);
                _tracker.gameObject.SetActive(line != null);
            }
            if (_gold != null) _gold.SetText(N(_model.Gold) + " " + T(StartNameKeys.CurrencyGold));
            if (_equippedTitle != null) _equippedTitle.SetText(string.IsNullOrEmpty(_model.EquippedTitle) ? string.Empty : T(_model.EquippedTitle));
            // 판 중에는 강화 항목이 없다(TV-133). 솔로 흐름에서는 판 중에 메인 메뉴가 열리지 않지만 목록은 모델을 따른다.
            bool hasEnhance = false;
            for (int i = 0; i < _model.Items.Count; i++) if (_model.Items[i].Action == MainMenuAction.Enhance) hasEnhance = true;
            if (_buttons.TryGetValue(MainMenuAction.Enhance, out var eb) && eb != null) eb.gameObject.SetActive(hasEnhance);
            RefreshCharacter(p);
        }

        void RefreshCharacter(ProfileData p)
        {
            var art = Ctx != null ? Ctx.Art : null;
            if (art == null || _charImage == null) return;
            int ch = _model.Character;
            int stage = 0;
            var rec = p != null ? p.Character(ch) : null;
            var wand = rec != null && p != null ? p.Wand(rec.EquippedWandId) : null;
            if (wand != null) stage = wand.Stage;
            if (ch == _shownChar && stage == _shownStage) return;
            _shownChar = ch;
            _shownStage = stage;
            string charId = Ids.Char.Of(ch);
            _charAnim = art.CharacterClip(charId, "idle", Facing3.Down);
            _charImage.sprite = _charAnim != null ? _charAnim.At(0f) : null;
            _charImage.enabled = _charImage.sprite != null;
            _wandImage.sprite = art.WandHand(charId, stage, Facing3.Down, false);
            PlaceWand(0f);
        }

        void PlaceWand(float t)
        {
            var art = Ctx != null ? Ctx.Art : null;
            if (art == null || _wandImage == null || _charImage == null) return;
            var info = art.CharacterClipInfo(Ids.Char.Of(_model.Character), "idle", Facing3.Down);
            if (info == null || info.Frames == null || info.Frames.Count == 0) { _wandImage.enabled = false; return; }
            int fi = 0;
            if (info.Fps > 0f) fi = (int)(t * info.Fps) % info.Frames.Count;
            var tl = WandSheet.AttachTopLeft(info.Frames[fi].Hand, art.WandGrip);
            var crt = _charImage.rectTransform;
            float cx = crt.anchoredPosition.x, cy = -crt.anchoredPosition.y;
            UiKit.Place(_wandImage.rectTransform, cx + tl.X * CharScale, cy + tl.Y * CharScale, WandCellW * CharScale, WandCellH * CharScale);
            _wandImage.enabled = _wandImage.sprite != null;
        }

        void UpdateTimeOfDay()
        {
            if (Ctx == null || Ctx.Clock == null) return;
            var tod = MainMenuModel.TimeOfDay(Ctx.Clock.Now.Hour, _tuning);
            if (_todKnown && tod != _tod) PlayUi(StartSounds.MainMenuBgTimeChange);
            _tod = tod;
            _todKnown = true;
            if (_bgImage != null) _bgImage.color = TodTint(tod);
        }

        static Color TodTint(MenuTimeOfDay tod)
        {
            switch (tod)
            {
                case MenuTimeOfDay.Dusk: return UiTheme.PaperShade;
                case MenuTimeOfDay.Night: return UiTheme.SlateBlue;
                default: return Color.white;
            }
        }

        public override void Tick(float dt)
        {
            // 판이 열리고 닫힌 것이 바뀌면 항목(강화, TV-133)을 다시 정한다. 정산은 판이 열린 채 저장하고(그때 Refresh가 강화를 숨긴다)
            // 그 뒤 EndRun을 하므로, 저장 신호만 믿으면 메뉴로 돌아왔을 때 강화가 숨은 채 남는다.
            if (_model != null && RunActive() != _refreshedWithRun) Refresh();
            _animClock += dt;
            if (_charAnim != null && _charImage != null) { _charImage.sprite = _charAnim.At(_animClock); PlaceWand(_animClock); }
            if (_fireAnim != null && _campfire != null) _campfire.sprite = _fireAnim.At(_animClock);
            _clockPoll += dt;
            if (_tuning != null && _clockPoll >= _tuning.ClockPollSeconds) { _clockPoll = 0f; UpdateTimeOfDay(); }
        }

        void Open(MainMenuEntry e)
        {
            if (e == null || (_pending != null && _pending.IsOpen)) return;
            if (e.Action == MainMenuAction.Exit)
            {
                // D-36 [임시] 게임 종료, N-02 [임시] 확인 창 한 번.
                Screens?.Confirm(StartNameKeys.ConfirmTitle, StartNameKeys.MainMenuExitConfirm, false, ok => { if (ok) Application.Quit(); });
                return;
            }
            _model.Open(e).Apply(Screens, this);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (_pending != null && _pending.IsOpen) return _pending.HandleKey(key);
            return base.OnMenuKey(key);
        }

        /// <summary>메인 메뉴는 맨 아래 화면이다. Esc는 알림 창이 떠 있으면 그것을 닫고, 아니면 아무 일도 하지 않는다.</summary>
        public override bool OnBack()
        {
            if (_pending != null && _pending.IsOpen) { _pending.Hide(); return true; }
            return true;
        }
    }
}
