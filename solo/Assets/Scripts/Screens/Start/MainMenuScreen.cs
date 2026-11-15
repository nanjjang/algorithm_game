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
        // 배치(화면 기준 px). 왼쪽 세로 메뉴(원정이 머리 항목), 오른쪽은 모닥불 장면이 숨 쉬게 비워 둔다.
        const float HeroY = 112f, HeroH = 88f, RowH = 56f, RowInset = 24f;
        const float ScrimW = 820f;
        const float BadgeW = 200f, BadgeH = 40f;
        const int CharScale = 4, CharCell = 64, WandCellW = 32, WandCellH = 64;
        const float PedestalW = 320f, PedestalH = 96f, CampfireSize = 128f;

        MainMenuModel _model;
        StartTuning _tuning;
        readonly Dictionary<MainMenuAction, UiButton> _buttons = new Dictionary<MainMenuAction, UiButton>();
        UiButton _expedition;
        UiLabel _tracker, _gold, _equippedTitle, _profileLabel;
        UiLabel _goalRevenge, _goalChallenge, _goalChallengeProgress;
        UiProgressBar _goalChallengeBar;
        UImage _bgImage, _charImage, _wandImage, _campfire;
        // 메뉴 줄의 고름 표식(마름모)과 줄 글자색: 커서가 온 줄만 밝게.
        readonly List<UiButton> _rows = new List<UiButton>();
        readonly List<GameObject> _rowMarks = new List<GameObject>();
        readonly List<Color> _rowColors = new List<Color>();
        UImage _heroMark;
        RectTransform _bestChipRoot;
        string _bestChipText;
        UiLabel _heroTitle;
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
            BuildScrim(content, top);

            // 왼쪽 단(격자 0~3열): 원정 머리글 → 기록 알약 → 다음 목표 → 메뉴 줄 → 나가기, 맨 아래 서버 한 줄.
            float lx = UiLayout.ColX(0), lw = UiLayout.ColW(4);
            float h64 = UiTheme.LineHeight(UiFontId.F64), h24 = UiTheme.LineHeight(UiFontId.F24), h16 = UiTheme.LineHeight(UiFontId.F16);

            // 머리 항목 = 원정: 커서 테가 제목 줄만 감싸게 버튼은 제목 높이만큼.
            float y = HeroY - top;
            _expedition = UiKit.Button(Ctx, content, null, lx, y, lw, HeroH, () => Open(_model.Expedition),
                UiButtonStyle.Plain, Focus, noBracket: true);
            var er = _expedition.ContentRoot != null ? _expedition.ContentRoot : _expedition.Rect;
            _heroMark = UiKit.Rect(er, 0f, (HeroH - h64) * 0.5f + 8f, 4f, h64 - 16f, UiTheme.Gold, false, "HeroMark");
            _heroTitle = UiKit.Label(Ctx, er, T(StartNameKeys.MainMenuExpedition), UiFontId.F64, RowInset, (HeroH - h64) * 0.5f,
                lw - RowInset, h64, UiTheme.Chalk);
            UiKit.DropShadow(_heroTitle, 4f);
            _buttons[MainMenuAction.Expedition] = _expedition;
            y += HeroH;
            UiKit.Label(Ctx, content, T(StartNameKeys.MainMenuExpeditionSub), UiFontId.F24, lx + RowInset, y, lw - RowInset, h24, UiTheme.SlateBlue);
            y += h24 + UiLayout.V(2);

            // 기록: 최고 웨이브 알약 + 추적 한 줄.
            float ix = lx + RowInset, iw = lw - RowInset;
            _bestChipRoot = UiKit.Node(content, "BestWave", ix, y, iw, UiLayout.ChipH);
            y += UiLayout.ChipH + UiLayout.V(1);
            _tracker = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, ix, y, iw, h16, UiTheme.Chalk);
            y += h16 + UiLayout.V(1);

            // 다음 목표: 구역 제목 + 두 줄 + 진행 막대.
            y = UiLayout.Section(Ctx, content, ix, y, iw, T(MainMenuModel.GoalTitleKey));
            _goalRevenge = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, ix, y, iw, h24, UiTheme.Chalk);
            y += h24 + UiLayout.V(1);
            _goalChallenge = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, ix, y, iw, h16, UiTheme.Chalk);
            y += h16 + UiLayout.V(1);
            const float ProgW = 120f;
            _goalChallengeBar = UiKit.ProgressBar(Ctx, content, ix, y + (h16 - 8f) * 0.5f, iw - ProgW - UiLayout.V(2), 8f, 0f);
            _goalChallengeProgress = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, ix + iw - ProgW, y, ProgW, h16, UiTheme.SlateBlue,
                TextAnchor.MiddleRight);
            y += h16 + UiLayout.V(2);

            // 나머지 항목: F32 줄, 고른 줄은 왼쪽 금 막대 + 옅은 면 + 금 글자.
            float ly = UiLayout.Snap(y);
            for (int i = 0; i < _model.Items.Count; i++)
            {
                var e = _model.Items[i];
                var b = MenuRow(content, T(e.NameKey), ly, RowH, UiFontId.F32, UiTheme.Chalk, () => Open(e), null);
                _buttons[e.Action] = b;
                ly += RowH;
            }
            // 나가기: 목록 끝의 조용한 줄.
            ly += UiLayout.V(2);
            var exitEntry = _model.Exit;
            _buttons[MainMenuAction.Exit] = MenuRow(content, T(exitEntry.NameKey), ly, 40f, UiFontId.F24, UiTheme.SlateBlue, () => Open(exitEntry), null);

            // 서버 전용 묶음: 왼쪽 아래 조용한 설명 한 줄(고르면 오프라인 안내).
            BuildServerLine(content, lx, UiScale.RefHeight - BottomBarHeight - UiLayout.V(2) - 40f - top, UiLayout.ColW(7), 40f);
            foreach (var mb in _buttons.Values)
            {
                if (mb == null) continue;
                if (mb.Background != null) mb.Background.enabled = false;
                if (mb.Glow != null) mb.Glow.enabled = false;
            }
            Focus.CursorChanged = prev =>
            {
                if (_expedition != null && ReferenceEquals(Focus.Current, _expedition)) PlayUi(StartSounds.MainMenuModeCardFocus);
            };

            _pending = new PendingSettledPanel(Ctx, Root, new UiFocusGroup(Manager != null ? Manager.InputMode : null, Ctx != null ? Ctx.Sound : null));
        }

        /// <summary>왼쪽 메뉴 뒤 어둠(판 대신): 진한 띠 + 옅은 띠로 오른쪽 장면 쪽으로 풀린다.</summary>
        void BuildScrim(RectTransform content, float top)
        {
            float h = UiScale.RefHeight - top - BottomBarHeight;
            // 진한 몸통 뒤로 얇은 띠 여러 장이 조금씩 옅어져 경계가 보이지 않게 장면 쪽으로 풀린다.
            const int Steps = 24;
            float solid = ScrimW * 0.55f, fade = ScrimW - solid;
            UiKit.Rect(content, 0f, 0f, solid, h, UiTheme.WithAlpha(UiTheme.Night, 0.62f), false, "Scrim");
            for (int i = 0; i < Steps; i++)
            {
                float t = (i + 0.5f) / Steps;
                float a = 0.62f * (1f - t) * (1f - t);
                UiKit.Rect(content, solid + fade * i / Steps, 0f, fade / Steps + 1f, h, UiTheme.WithAlpha(UiTheme.Night, a), false, "ScrimFade" + i);
            }
        }

        /// <summary>메뉴 한 줄: 판 없는 버튼(단 폭), 글자는 왼쪽 정렬. 고르면 옅은 면 + 왼쪽 금 막대(표식).</summary>
        UiButton MenuRow(RectTransform content, string text, float y, float h, UiFontId font, Color color, System.Action onClick, string sound)
        {
            float w = UiLayout.ColW(4);
            var b = UiKit.Button(Ctx, content, text, UiLayout.ColX(0), y, w, h, onClick, UiButtonStyle.Plain, Focus, sound, font, noBracket: true);
            var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
            var mark = UiKit.Node(r, "RowMark", 0f, 0f, w, h);
            mark.SetAsFirstSibling();
            UiKit.Rect(mark, 0f, 0f, w, h, UiTheme.WithAlpha(UiTheme.Wood, 0.6f), false, "RowFill");
            UiKit.Rect(mark, 0f, 8f, 4f, h - 16f, UiTheme.Gold, false, "RowBar");
            if (b.Label != null)
            {
                b.Label.Text.alignment = TextAnchor.MiddleLeft;
                var lrt = b.Label.Rect;
                lrt.offsetMin = new Vector2(RowInset, lrt.offsetMin.y);
                UiKit.DropShadow(b.Label, 2f);
            }
            b.LabelColor = color;
            b.Refresh();
            mark.gameObject.SetActive(false);
            _rows.Add(b);
            _rowMarks.Add(mark.gameObject);
            _rowColors.Add(color);
            return b;
        }

        /// <summary>커서가 온 줄: 표식을 켜고 글자를 금색으로. 원정 머리 항목도 같은 규칙.</summary>
        void UpdateRowMarks()
        {
            var cur = Focus != null ? Focus.Current : null;
            for (int i = 0; i < _rows.Count; i++)
            {
                var b = _rows[i];
                if (b == null) continue;
                bool on = ReferenceEquals(cur, b);
                if (_rowMarks[i].activeSelf == on) continue;
                _rowMarks[i].SetActive(on);
                b.LabelColor = on ? UiTheme.Gold : _rowColors[i];
                b.Refresh();
            }
            if (_heroMark != null)
            {
                bool heroOn = _expedition != null && ReferenceEquals(cur, _expedition);
                _heroMark.enabled = heroOn;
                if (_heroTitle != null) _heroTitle.Color = heroOn ? UiTheme.Gold : UiTheme.Chalk;
            }
        }

        /// <summary>서버 전용 묶음 한 줄: "결투 · 광장 · … — 서버에 접속하면 열립니다". 판 없는 작은 설명 글.</summary>
        void BuildServerLine(RectTransform content, float x, float y, float w, float h)
        {
            var e = _model.ServerOnly;
            var b = UiKit.Button(Ctx, content, null, x, y, w, h, () => Open(e), UiButtonStyle.Plain, Focus, StartSounds.OfflineGroupBoxOpen);
            var r = b.ContentRoot != null ? b.ContentRoot : b.Rect;
            var names = new List<string>();
            for (int i = 0; i < MainMenuModel.ServerOnlyKeys.Length; i++) names.Add(T(MainMenuModel.ServerOnlyKeys[i]));
            float h16 = UiTheme.LineHeight(UiFontId.F16);
            // 결투·광장·친구·장터·순위표는 [온라인]에 생겼다(11-online.md). 아직 없는 것만 "준비 중"으로 알린다.
            var l = UiKit.Label(Ctx, r, T(Wizard.Solo.Screens.Online.OnlineKeys.MenuServerLine), UiFontId.F16, RowInset, (h - h16) * 0.5f, w - RowInset, h16, UiTheme.SlateBlue);
            _buttons[MainMenuAction.ServerOnly] = b;
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
            // 캐릭터는 모닥불 왼쪽 곁(장면의 중심, 왼쪽 메뉴와 겹치지 않게).
            float cx = FireAnchorX - CampfireSize * 0.5f - cs - 24f;
            float cy = UiScale.RefHeight - BottomBarHeight - cs - PedestalH * 0.5f;
            var ped = art.Ui("S-0503", "dirt_pedestal");
            if (ped.IsValid)
                UiKit.Image(back, ped.Sprite, cx + (cs - PedestalW) * 0.5f, cy + cs - PedestalH * 0.5f, PedestalW, PedestalH, null, false);
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
            if (badge.IsValid) UiKit.Image(TopBar, badge.Sprite, UiLayout.Outer, by, BadgeW, BadgeH, null, false);
            else UiLayout.Surface(Ctx, TopBar, UiLayout.Outer, by, BadgeW, BadgeH);
            // 표지 그림(I-F12)은 왼쪽에 문서 아이콘, 오른쪽이 글자 자리인 종이다 → 글자는 아이콘 오른쪽에 종이 글자색으로.
            _profileLabel = UiKit.Label(Ctx, TopBar, T(TitleModel.ProfileKey(Ctx != null ? Ctx.Kind : ProfileKind.Offline)), UiFontId.F16,
                UiLayout.Outer + TitleModel.BadgeTextInset, by, BadgeW - TitleModel.BadgeTextInset - TitleModel.BadgeTextRight, BadgeH,
                badge.IsValid ? UiTheme.PaperText : UiTheme.Chalk, TextAnchor.MiddleCenter, false, !badge.IsValid);
            if (TitleLabel != null)
            {
                // 메인 메뉴는 제목 글을 두지 않는다(화면 자체가 메뉴다).
                TitleLabel.SetText(string.Empty);
            }
            // 골드 알약: 들어간 면 + 동전 + F24 금색 값(오른쪽 바깥 여백 64에 맞춘다).
            const float GoldW = 240f, IconS = 24f, ChipH = 48f;
            float gx = UiLayout.ScreenW - UiLayout.Outer - GoldW, gy = (TopBarHeight - ChipH) * 0.5f;
            var chip = UiLayout.Surface(Ctx, TopBar, gx, gy, GoldW, ChipH, 0);
            if (art != null) UiKit.Image(chip, art.Icon("ui:I-C2/gold", 24), 16f, (ChipH - IconS) * 0.5f, IconS, IconS, null, false);
            _gold = UiKit.Label(Ctx, chip, string.Empty, UiFontId.F24, 16f + IconS + 8f, 0f, GoldW - IconS - 40f, ChipH, UiTheme.Gold,
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
            RefreshBestChip(_model.BestWaveLine(Ctx != null ? Ctx.Names : null));
            if (_tracker != null)
            {
                string line = _model.TrackerLine(Ctx != null ? Ctx.Names : null);
                _tracker.SetText(line ?? string.Empty);
                _tracker.gameObject.SetActive(line != null);
            }
            if (_gold != null) _gold.SetText(N(_model.Gold) + " " + T(StartNameKeys.CurrencyGold));
            RefreshGoals();
            if (_equippedTitle != null) _equippedTitle.SetText(string.IsNullOrEmpty(_model.EquippedTitle) ? string.Empty : T(_model.EquippedTitle));
            // 판 중에는 강화 항목이 없다(TV-133). 솔로 흐름에서는 판 중에 메인 메뉴가 열리지 않지만 목록은 모델을 따른다.
            bool hasEnhance = false;
            for (int i = 0; i < _model.Items.Count; i++) if (_model.Items[i].Action == MainMenuAction.Enhance) hasEnhance = true;
            if (_buttons.TryGetValue(MainMenuAction.Enhance, out var eb) && eb != null) eb.gameObject.SetActive(hasEnhance);
            RefreshCharacter(p);
        }

        /// <summary>최고 웨이브 알약: 글이 바뀔 때만 다시 만든다(알약 폭이 글 길이를 따른다).</summary>
        void RefreshBestChip(string text)
        {
            if (_bestChipRoot == null || text == _bestChipText) return;
            _bestChipText = text;
            for (int i = _bestChipRoot.childCount - 1; i >= 0; i--) Object.Destroy(_bestChipRoot.GetChild(i).gameObject);
            UiLayout.Chip(Ctx, _bestChipRoot, 0f, 0f, text, null, UiTheme.Gold);
        }

        /// <summary>원정 카드의 다음 목표: 복수 중이면 허상까지 n웨이브, 그리고 지금 캐릭터의 다음 도전 과제(없으면 "모두" 과제).</summary>
        void RefreshGoals()
        {
            if (_goalRevenge == null) return;
            var names = Ctx != null ? Ctx.Names : null;
            string rev = _model.RevengeGoalLine(names);
            _goalRevenge.SetText(rev ?? T(MainMenuModel.GoalFreeKey));
            _goalRevenge.Color = rev != null ? UiTheme.Chalk : UiTheme.SlateBlue;
            bool has = _model.NextChallenge != null;
            _goalChallenge.SetText(has ? _model.ChallengeGoalLine(names) : T(MainMenuModel.GoalAllDoneKey));
            _goalChallengeBar.gameObject.SetActive(has);
            _goalChallengeProgress.gameObject.SetActive(has);
            if (has)
            {
                _goalChallengeBar.Value01 = _model.NextChallengeProgress.Ratio;
                _goalChallengeProgress.SetText(Wizard.Solo.Meta.Records.ChallengeText.ProgressText(names, _model.NextChallengeProgress));
            }
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
            UpdateRowMarks();
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
