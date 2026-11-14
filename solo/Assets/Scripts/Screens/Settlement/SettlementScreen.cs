using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Settlement
{
    /// <summary>
    /// 정산 화면(ScreenId.Settlement, 08 요약서 9절, S13 10절, UA 4.16). 판이 끝나면 차례로 넘어가는 세 쪽:
    /// 1쪽 기록(기록 줄, 캐릭터·지팡이, 점수 내역, 최고 기록 딱지, 복수 추적기 큰 꼴 1040×96)
    /// → 2쪽 귀환 가방(야생 블록, 가방 칸 한 줄 8칸, 영웅 한도, 담지 못한 것의 골드, [자동 담기])
    /// → (가방 확정 = Commit 한 번) 3쪽 가져가는 것(복수 보상 줄 맨 위, 골드, 담은 블록, 확정 재료, 까마귀 보따리, 재료 꾸러미, 새 비기, 새 칭호)
    /// + 끝 버튼 [다시 원정] [강화 화면으로] [메인 메뉴].
    /// 인자: ScreenArgKeys.RunSummary(RunSummary). 없으면 세션의 LastSummary.
    /// 규칙과 상태는 SettlementModel(엔진 없음)에 있고 이 화면은 그리기와 입력 전달만 한다.
    /// Esc로 정산을 건너뛸 수 없다(확정 전 Esc는 아무 일도 없다). K·Q = 앞 쪽, E = [다음].
    /// 솔로 제외 항목(검증 대기, 시즌 주화)은 그리지 않는다.
    /// </summary>
    public sealed class SettlementScreen : ScreenBase
    {
        // 자리(08 요약서 9.1~9.3절의 화면 좌표 − 위 띠 80px = Content 좌표)
        const float Top = TopBarHeight;
        const float RecordX = 360f, RecordY = 120f - Top, RecordW = 1200f, RecordH = 72f;
        const float ArtX = 160f, ArtY = 240f - Top, ArtSize = 384f, WandSize = 192f;
        const float ScoreX = 720f, ScoreY = 240f - Top, ScoreW = 1040f, ScoreH = 480f;
        const float TrackerX = 720f, TrackerY = 736f - Top, TrackerW = 1040f, TrackerH = 96f;
        const float WildX = 160f, WildY = 200f - Top, WildW = 720f, WildH = 560f;
        const float BagX = 1000f, BagY = 200f - Top, BagW = 760f, BagH = 360f;
        const float LeftX = 1000f, LeftY = 580f - Top, LeftW = 760f, LeftH = 180f;
        const float TakeX = 360f, TakeY = 120f - Top, TakeW = 1200f, TakeH = 680f;
        const float ButtonsX = 360f, ButtonsY = 936f - Top, ButtonsW = 1200f, ButtonH = 64f, ButtonGap = 24f;
        const float Pad = 16f, RowGap = 8f;
        const float CellW = 164f, CellH = 120f, CellGap = 8f, SlotSize = 80f, SlotGap = 8f;
        const int WildColumns = 4;
        const float PageLabelW = 200f;
        /// <summary>점수 줄이 차례로 나오는 간격(연출, 초).</summary>
        const float RowRevealSeconds = 0.12f;

        public override ScreenId Id => ScreenId.Settlement;
        protected override string TitleKey => SettlementNameKeys.Title;

        SettlementModel _model;

        readonly RectTransform[] _pages = new RectTransform[3];
        readonly UiLabel[] _pageLabels = new UiLabel[3];
        readonly List<UiWidget>[] _pageWidgets = { new List<UiWidget>(), new List<UiWidget>(), new List<UiWidget>() };

        // 1쪽
        UiLabel _recordLine, _startLine, _nameLine, _bestLine;
        UImage _charImage, _wandImage;
        RectTransform _scoreBox, _trackerBox;
        readonly List<GameObject> _scoreRowObjects = new List<GameObject>();
        readonly List<GameObject> _trackerObjects = new List<GameObject>();
        UiButton _recordNext;

        // 2쪽
        UiScrollList _wildList;
        UiLabel _wildEmpty, _bagCounter, _heroCounter, _bagNote, _noSlots;
        RectTransform _bagBox;
        readonly List<GameObject> _slotObjects = new List<GameObject>();
        readonly Dictionary<int, UiButton> _cellButtons = new Dictionary<int, UiButton>();
        UiLabel _leftGold, _leftLibrary, _leftBorrowed;
        UiButton _bagPrev, _bagAuto, _bagNext;

        // 3쪽
        UiScrollList _takeList;
        UiButton _againButton, _enhanceButton, _menuButton, _bundleButton;
        // 꾸러미 여는 연출(UI I-M11/bundle_open 64×64×5, 보충 그림): 연 순간부터 한 번 재생하고 마지막 장에 멈춘다.
        UImage _bundleImage;
        SpriteAnim _bundleAnim;
        float _bundleClock = float.MaxValue;
        UiLabel _enhanceHint;

        protected override UiKeyHint[] KeyHints => new[]
        {
            new UiKeyHint(Key.J, UiNameKeys.HintPick),
            new UiKeyHint(Key.K, SettlementNameKeys.CommonPrev),
        };

        // ================================================================ 만들기

        protected override void BuildContent(RectTransform content)
        {
            // 원정의 새벽 배경(S-1602)을 흐리게 + 비네트.
            AddBackdropArt("S-1602", "expedition_dawn", 0.35f);

            // 쪽 표시(위 띠 오른쪽).
            string[] pageKeys = { SettlementNameKeys.PageRecord, SettlementNameKeys.PageReturnBag, SettlementNameKeys.PageTakeaways };
            if (TopBar != null)
            {
                float lh = UiTheme.LineHeight(UiFontId.F24);
                float x0 = UiScale.RefWidth - 48f - PageLabelW * pageKeys.Length;
                for (int i = 0; i < pageKeys.Length; i++)
                    _pageLabels[i] = UiKit.Label(Ctx, TopBar, T(pageKeys[i]), UiFontId.F24, x0 + PageLabelW * i, (TopBarHeight - lh) * 0.5f,
                        PageLabelW, lh, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
            }

            for (int i = 0; i < _pages.Length; i++)
            {
                _pages[i] = UiKit.Node(content, "Page" + (i + 1), 0f, 0f, UiScale.RefWidth, ContentHeight);
            }
            BuildRecord(_pages[0]);
            BuildBag(_pages[1]);
            BuildTakeaways(_pages[2]);
        }

        void BuildRecord(RectTransform page)
        {
            UiBackdrop.Glow(page, RecordX + RecordW * 0.2f, RecordY - RecordH * 0.6f, RecordW * 0.6f, RecordH * 2.2f, UiTheme.WithAlpha(UiTheme.Brass, 0.18f));
            _recordLine = UiKit.Label(Ctx, page, string.Empty, UiFontId.F48, RecordX, RecordY, RecordW, RecordH, UiTheme.Chalk, TextAnchor.MiddleCenter);
            UiKit.DropShadow(_recordLine, 3f);
            UiKit.AccentRule(page, RecordX + RecordW * 0.3f, RecordY + RecordH - 4f, RecordW * 0.4f);
            float slh = UiTheme.LineHeight(UiFontId.F24);
            _startLine = UiKit.Label(Ctx, page, string.Empty, UiFontId.F24, RecordX, RecordY + RecordH, RecordW, slh, UiTheme.SlateBlue,
                TextAnchor.MiddleCenter);

            // 쓰러진 캐릭터 받침 + 캐릭터 큰 그림 + 지팡이.
            UiKit.Panel(Ctx, page, UiPanelKind.Recess, ArtX, ArtY, ArtSize, ArtSize);
            _charImage = UiKit.Image(page, null, ArtX, ArtY, ArtSize, ArtSize, Color.white);
            float wy = ArtY + ArtSize + RowGap;
            UiKit.Panel(Ctx, page, UiPanelKind.Recess, ArtX + (ArtSize - WandSize) * 0.5f, wy, WandSize, WandSize);
            _wandImage = UiKit.Image(page, null, ArtX + (ArtSize - WandSize) * 0.5f, wy, WandSize, WandSize, Color.white);
            _nameLine = UiKit.Label(Ctx, page, string.Empty, UiFontId.F24, ArtX, wy + WandSize + RowGap, ArtSize, slh, UiTheme.Chalk,
                TextAnchor.MiddleCenter);

            _scoreBox = UiKit.OrnatePanel(Ctx, page, ScoreX, ScoreY, ScoreW, ScoreH, 1).rectTransform;
            _scoreBox.name = "ScoreBreakdown";
            float blh = UiTheme.LineHeight(UiFontId.F16);
            _bestLine = UiKit.Label(Ctx, _scoreBox, string.Empty, UiFontId.F16, Pad * 2f, ScoreH - Pad - blh, ScoreW - Pad * 4f, blh, UiTheme.Gold);

            _trackerBox = UiKit.Panel(Ctx, page, UiPanelKind.Recess, TrackerX, TrackerY, TrackerW, TrackerH).rectTransform;
            _trackerBox.name = "RevengeTracker";

            _recordNext = Btn(page, T(SettlementNameKeys.CommonNext), 2, () => { if (_model.Next()) ShowPage(); }, UiButtonStyle.Primary);
            _pageWidgets[0].Add(_recordNext);
        }

        void BuildBag(RectTransform page)
        {
            var wild = UiKit.Panel(Ctx, page, UiPanelKind.Board, WildX, WildY, WildW, WildH).rectTransform;
            wild.name = "WildBlocks";
            float tlh = UiTheme.LineHeight(UiFontId.F24);
            UiKit.Label(Ctx, wild, T(SettlementNameKeys.BagWildTitle), UiFontId.F24, Pad, Pad * 0.5f, WildW - Pad * 2f, tlh);
            float listY = Pad * 0.5f + tlh + RowGap;
            _wildList = UiKit.ScrollList(Ctx, wild, Pad, listY, WildW - Pad * 2f, WildH - listY - Pad, Focus, CellGap);
            _wildEmpty = UiKit.Label(Ctx, wild, T(SettlementNameKeys.BagEmpty), UiFontId.F16, Pad, listY, WildW - Pad * 2f, 0f, UiTheme.SlateBlue);

            _bagBox = UiKit.Panel(Ctx, page, UiPanelKind.Board, BagX, BagY, BagW, BagH).rectTransform;
            _bagBox.name = "ReturnBag";
            UiKit.Label(Ctx, _bagBox, T(SettlementNameKeys.BagTitle), UiFontId.F24, Pad, Pad * 0.5f, BagW * 0.4f, tlh);
            _bagCounter = UiKit.Label(Ctx, _bagBox, string.Empty, UiFontId.F24, BagW * 0.4f, Pad * 0.5f, BagW * 0.3f - Pad, tlh, UiTheme.Chalk,
                TextAnchor.MiddleRight);
            _heroCounter = UiKit.Label(Ctx, _bagBox, string.Empty, UiFontId.F16, BagW * 0.7f, Pad * 0.5f, BagW * 0.3f - Pad, tlh, UiTheme.Gold,
                TextAnchor.MiddleRight);
            float nlh = UiTheme.LineHeight(UiFontId.F16);
            _noSlots = UiKit.Label(Ctx, _bagBox, T(SettlementNameKeys.BagNoSlots), UiFontId.F16, Pad, Pad + tlh + RowGap, BagW - Pad * 2f, 0f,
                UiTheme.SlateBlue);
            _bagNote = UiKit.Label(Ctx, _bagBox, string.Empty, UiFontId.F16, Pad, BagH - Pad - nlh, BagW - Pad * 2f, nlh, UiTheme.Amber);

            var left = UiKit.Panel(Ctx, page, UiPanelKind.Recess, LeftX, LeftY, LeftW, LeftH).rectTransform;
            left.name = "Leftover";
            UiKit.Label(Ctx, left, T(SettlementNameKeys.BagLeftoverTitle), UiFontId.F24, Pad, Pad * 0.5f, LeftW - Pad * 2f, tlh);
            float y = Pad * 0.5f + tlh + RowGap;
            _leftGold = UiKit.Label(Ctx, left, string.Empty, UiFontId.F16, Pad, y, LeftW - Pad * 2f, nlh, UiTheme.Gold);
            y += nlh + RowGap;
            _leftLibrary = UiKit.Label(Ctx, left, T(SettlementNameKeys.BagLibraryFull), UiFontId.F16, Pad, y, LeftW - Pad * 2f, nlh, UiTheme.Amber);
            y += nlh + RowGap;
            _leftBorrowed = UiKit.Label(Ctx, left, T(SettlementNameKeys.BagBorrowedNote), UiFontId.F16, Pad, y, LeftW - Pad * 2f, nlh,
                UiTheme.SlateBlue);

            _bagPrev = Btn(page, T(SettlementNameKeys.CommonPrev), 0, () => { if (_model.Prev()) ShowPage(); }, UiButtonStyle.Medium);
            _bagAuto = Btn(page, T(SettlementNameKeys.BagAuto), 1, () => { _model.AutoPick(); RefreshBag(); }, UiButtonStyle.Medium, string.Empty);
            _bagNext = Btn(page, T(SettlementNameKeys.CommonNext), 2, () => { if (_model.Next()) ShowPage(); }, UiButtonStyle.Primary);
        }

        void BuildTakeaways(RectTransform page)
        {
            _takeList = UiKit.ScrollList(Ctx, page, TakeX, TakeY, TakeW, TakeH, Focus, RowGap);
            float hlh = UiTheme.LineHeight(UiFontId.F16);
            _enhanceHint = UiKit.Label(Ctx, page, T(SettlementNameKeys.EnhanceHint), UiFontId.F16, ButtonsX, ButtonsY - RowGap - hlh, ButtonsW, hlh,
                UiTheme.SelectYellow, TextAnchor.MiddleCenter);
            _againButton = Btn(page, T(SettlementNameKeys.Again), 0, () => Go(_model.Again()), UiButtonStyle.Large);
            _enhanceButton = Btn(page, T(SettlementNameKeys.ToEnhance), 1, () => Go(_model.ToEnhance()), UiButtonStyle.Large);
            _menuButton = Btn(page, T(SettlementNameKeys.ToMenu), 2, () => Go(_model.ToMenu()), UiButtonStyle.Large);
        }

        /// <summary>아래 버튼 줄(1200×64)의 col번째 자리(셋으로 나눔).</summary>
        UiButton Btn(RectTransform page, string text, int col, System.Action onClick, UiButtonStyle style, string pressSound = null)
        {
            float w = (ButtonsW - ButtonGap * 2f) / 3f;
            return UiKit.Button(Ctx, page, text, ButtonsX + col * (w + ButtonGap), ButtonsY, w, ButtonH, onClick, style, null, pressSound);
        }

        // ================================================================ 열기

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            RunSummary summary = Args.Get<RunSummary>(ScreenArgKeys.RunSummary);
            if (summary == null && Ctx != null && Ctx.Session != null) summary = Ctx.Session.LastSummary;
            _model = new SettlementModel(Ctx != null ? Ctx.Settlement : null, Ctx != null ? Ctx.Content : null,
                () => Ctx != null ? Ctx.Profile : null, Ctx != null ? Ctx.Sound : null, EndRun);
            _model.Begin(summary);
            FillRecord();
            BuildWildCells();
            ShowPage();
        }

        void EndRun()
        {
            if (Ctx != null && Ctx.Session != null) Ctx.Session.EndRun();
        }

        void ShowPage()
        {
            int idx = (int)_model.Page - 1;
            for (int i = 0; i < _pages.Length; i++) _pages[i].gameObject.SetActive(i == idx);
            for (int i = 0; i < _pageLabels.Length; i++)
                if (_pageLabels[i] != null) _pageLabels[i].Color = i == idx ? UiTheme.SelectYellow : UiTheme.SlateBlue;

            switch (_model.Page)
            {
                case SettlementPage.Record: FillRecord(); break;
                case SettlementPage.ReturnBag: RefreshBag(); break;
                case SettlementPage.Takeaways: FillTakeaways(); break;
            }

            Focus.Clear();
            List<UiWidget> list = PageWidgets(_model.Page);
            for (int i = 0; i < list.Count; i++) Focus.Add(list[i]);
            UiWidget first = _model.Page == SettlementPage.Takeaways && _model.ShowEnhanceHint ? _enhanceButton
                : list.Count > 0 ? list[0] : null;
            if (first != null) Focus.SetCursor(first, false);

            SetHints(_model.Page == SettlementPage.Takeaways
                ? new[]
                {
                    new UiKeyHint(Key.J, UiNameKeys.HintPick),
                    new UiKeyHint(Key.K, SettlementNameKeys.CommonPrev),
                    new UiKeyHint(Key.Escape, SettlementNameKeys.ToMenu),
                }
                : _model.Page == SettlementPage.Record
                    ? new[] { new UiKeyHint(Key.J, SettlementNameKeys.CommonNext) }
                    : KeyHints);
            _model.OnPageShown();
        }

        List<UiWidget> PageWidgets(SettlementPage p)
        {
            var list = new List<UiWidget>();
            switch (p)
            {
                case SettlementPage.Record:
                    list.Add(_recordNext);
                    break;
                case SettlementPage.ReturnBag:
                    for (int i = 0; i < _model.Candidates.Count; i++)
                        if (_cellButtons.TryGetValue(_model.Candidates[i].Uid, out UiButton b) && b != null) list.Add(b);
                    list.Add(_bagPrev);
                    list.Add(_bagAuto);
                    list.Add(_bagNext);
                    break;
                case SettlementPage.Takeaways:
                    if (_bundleButton != null) list.Add(_bundleButton);
                    list.Add(_againButton);
                    list.Add(_enhanceButton);
                    list.Add(_menuButton);
                    break;
            }
            return list;
        }

        // ================================================================ 1쪽

        void FillRecord()
        {
            RunSummary s = _model.Summary;
            _recordLine.SetText(F(SettlementNameKeys.FormatDayWave, "day", _model.Day, "wave", _model.DisplayWave));
            _startLine.SetText(_model.ShowStartWave ? F(SettlementNameKeys.FormatStartWave, "start", _model.StartWave) : string.Empty);

            string charId = s != null ? s.CharId : null;
            Sprite cs = Ctx != null && Ctx.Art != null && !string.IsNullOrEmpty(charId) ? Ctx.Art.CharacterSelection(charId) : null;
            SetImage(_charImage, cs);
            Sprite ws = Ctx != null && Ctx.Art != null && !string.IsNullOrEmpty(charId) ? Ctx.Art.WandInspection(charId, s.WandStage) : null;
            SetImage(_wandImage, ws);
            string name = !string.IsNullOrEmpty(charId) ? T(charId) : string.Empty;
            if (s != null && !string.IsNullOrEmpty(charId))
                name = name + " " + F(SettlementNameKeys.FormatWandStage, "n", s.WandStage);
            _nameLine.SetText(name);

            Clear(_scoreRowObjects);
            float lh = UiTheme.LineHeight(UiFontId.F24);
            float rowH = lh + RowGap;
            float y = Pad;
            for (int i = 0; i < _model.ScoreRows.Count; i++)
            {
                ScoreRow r = _model.ScoreRows[i];
                if (r.Total)
                {
                    var d = UiKit.Divider(Ctx, _scoreBox, Pad * 2f, y + RowGap * 0.5f, ScoreW - Pad * 4f);
                    _scoreRowObjects.Add(d.gameObject);
                    y += RowGap;
                }
                Color c = r.Total ? UiTheme.Chalk : UiTheme.Paper;
                var l = UiKit.Label(Ctx, _scoreBox, T(r.Key), UiFontId.F24, Pad * 2f, y, ScoreW * 0.5f, lh, c);
                var v = UiKit.Label(Ctx, _scoreBox, N(r.Value), UiFontId.F24, ScoreW * 0.5f, y, ScoreW * 0.5f - Pad * 2f, lh, c, TextAnchor.MiddleRight);
                _scoreRowObjects.Add(l.gameObject);
                _scoreRowObjects.Add(v.gameObject);
                // 연출: 줄이 차례로 미끄러져 들어오고 숫자가 차르륵 올라간다(합계는 마지막에 툭 튄다). 글의 끝 값은 그대로다.
                float delay = 0.15f + i * RowRevealSeconds;
                UiMotion.Entrance(l, delay, UiTheme.PanelPopSeconds, new Vector2(-24f, 0f), 1f);
                UiMotion.Entrance(v, delay, UiTheme.PanelPopSeconds, new Vector2(24f, 0f), 1f);
                UiCountUp.Run(v, r.Value, N, delay, r.Total ? UiTheme.CountUpSeconds * 1.4f : UiTheme.CountUpSeconds * 0.6f, 0, r.Total);
                if (r.Total) UiKit.DropShadow(v, 2f);
                if (r.Total && _model.NewBest)
                {
                    float bw = ScoreW * 0.25f;
                    var badge = UiKit.Panel(Ctx, _scoreBox, UiPanelKind.Heading, ScoreW * 0.5f - bw - Pad, y, bw, lh).rectTransform;
                    UiKit.Label(Ctx, badge, T(SettlementNameKeys.BestRecord), UiFontId.F16, 0f, 0f, bw, lh, UiTheme.Gold, TextAnchor.MiddleCenter);
                    _scoreRowObjects.Add(badge.gameObject);
                    UiMotion.Entrance(badge, 0.15f + i * RowRevealSeconds + UiTheme.CountUpSeconds * 1.4f, 0.3f, Vector2.zero, 1.5f);
                }
                y += rowH;
            }
            _bestLine.SetText(_model.NewBest ? T(SettlementNameKeys.BestLine) : string.Empty);

            FillTracker(_trackerBox, TrackerW, TrackerH, _trackerObjects);
        }

        /// <summary>복수 추적기 큰 꼴(08 요약서 13.2절). 값은 모델의 RevengeTrackerView(= Meta.RevengeTracker.Build)만.</summary>
        void FillTracker(RectTransform box, float w, float h, List<GameObject> objects)
        {
            Clear(objects);
            RevengeTrackerView t = _model.Tracker;
            box.gameObject.SetActive(t.Visible);
            if (!t.Visible) return;

            const int MirrorPx = 32;
            UiSprite mirror = UiKit.KitSprite(Ctx, "S-2603", MirrorVariant(t.Mirror));
            float mx = Pad;
            if (mirror.IsValid)
            {
                objects.Add(UiKit.Image(box, mirror.Sprite, mx, (h - MirrorPx * 2) * 0.5f, MirrorPx * 2, MirrorPx * 2).gameObject);
            }
            float tx = mx + MirrorPx * 2 + Pad;
            float tw = w - tx - Pad;
            if (t.JustCompleted)
            {
                float dlh = UiTheme.LineHeight(UiFontId.F32);
                objects.Add(UiKit.Label(Ctx, box, T(SettlementNameKeys.TrackerDone), UiFontId.F32, tx, (h - dlh) * 0.5f, tw * 0.5f, dlh, UiTheme.Good)
                    .gameObject);
                UiSprite check = UiKit.KitSprite(Ctx, "K-41", "confirmation");
                float cs = check.IsValid && check.Height > 0 ? check.Height : 16f;
                if (check.IsValid)
                    objects.Add(UiKit.Image(box, check.Sprite, tx + tw * 0.5f, (h - cs) * 0.5f, check.Width > 0 ? check.Width : cs, cs).gameObject);
                else
                    objects.Add(UiKit.Rect(box, tx + tw * 0.5f, (h - cs) * 0.5f, cs, cs, UiTheme.Good).gameObject);
                return;
            }

            float lh = UiTheme.LineHeight(UiFontId.F16);
            int lines = t.HasDent ? 3 : 2;
            float y = (h - lh * lines - RowGap * (lines - 1)) * 0.5f;
            objects.Add(UiKit.Label(Ctx, box, T(SettlementNameKeys.TrackerGoal), UiFontId.F16, tx, y, tw, lh, UiTheme.Chalk).gameObject);
            y += lh + RowGap;
            float pw = tw * 0.5f;
            objects.Add(UiKit.Label(Ctx, box, F(SettlementNameKeys.TrackerProgress, "n", t.Progress), UiFontId.F16, tx, y, pw, lh, UiTheme.Chalk)
                .gameObject);
            var bar = UiKit.ProgressBar(Ctx, box, tx + pw + Pad, y + lh * 0.25f, tw - pw - Pad, lh * 0.5f,
                t.ProgressMax > 0 ? (float)t.Progress / t.ProgressMax : 0f);
            objects.Add(bar.gameObject);
            if (t.HasDent)
            {
                y += lh + RowGap;
                objects.Add(UiKit.Label(Ctx, box, F(SettlementNameKeys.TrackerDent, "percent", t.DentPercent, "tries", t.Tries), UiFontId.F16,
                    tx, y, tw, lh, UiTheme.SlateBlue).gameObject);
            }
        }

        static string MirrorVariant(MirrorMark m) =>
            m == MirrorMark.Broken ? "revenge_mirror_shattered" : m == MirrorMark.Cracked ? "revenge_mirror_cracked" : "revenge_mirror_whole";

        // ================================================================ 2쪽

        void BuildWildCells()
        {
            _wildList.Clear();
            _cellButtons.Clear();
            int n = _model.Candidates.Count;
            _wildEmpty.gameObject.SetActive(n == 0);
            RectTransform row = null;
            for (int i = 0; i < n; i++)
            {
                if (i % WildColumns == 0) row = _wildList.AddRow(CellH);
                BagEntry e = _model.Candidates[i];
                int uid = e.Uid;
                float x = (i % WildColumns) * (CellW + CellGap);
                var b = UiKit.Button(Ctx, row, null, x, 0f, CellW, CellH, () => OnCell(uid), UiButtonStyle.Plain, null, string.Empty);
                RectTransform root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                const int IconPx = 48;
                UiKit.Icon(Ctx, root, e.Block.BlockId, IconPx, (CellW - IconPx) * 0.5f, Pad * 0.5f);
                float lh = UiTheme.LineHeight(UiFontId.F12);
                UiKit.Label(Ctx, root, T(e.Block.BlockId), UiFontId.F12, Pad * 0.5f, Pad * 0.5f + IconPx + 4f, CellW - Pad, lh, UiTheme.Chalk,
                    TextAnchor.MiddleCenter);
                string tag = e.Borrowed ? T(SettlementNameKeys.BlockBorrowed) : T(SettlementNameKeys.BlockGradePrefix + e.Rarity);
                UiKit.Label(Ctx, root, tag, UiFontId.F12, Pad * 0.5f, Pad * 0.5f + IconPx + 4f + lh, CellW - Pad, lh, RarityColor(e),
                    TextAnchor.MiddleCenter);
                if (e.Borrowed)
                {
                    b.SetDisabled(SettlementNameKeys.BagBorrowedNote);
                    Wizard.Solo.Screens.Camp.CampViews.BorrowedTag(Ctx, root, Pad * 0.5f, Pad * 0.5f, 1);
                }
                _cellButtons[uid] = b;
            }
        }

        static Color RarityColor(BagEntry e) =>
            e.Borrowed ? UiTheme.SlateBlue : e.Rarity == Ids.Rarity.Hero ? UiTheme.Gold : e.Rarity == Ids.Rarity.Rare ? UiTheme.RareBlue : UiTheme.CommonGray;

        void OnCell(int uid)
        {
            _model.Toggle(uid);
            RefreshBag();
        }

        void RefreshBag()
        {
            BagLimits lim = _model.Limits;
            _bagCounter.SetText(F(SettlementNameKeys.BagSlots, "used", _model.Picked.Count, "max", lim.Slots));
            _heroCounter.SetText(lim.HeroCap > 0 ? F(SettlementNameKeys.BagHeroCount, "used", _model.HeroPicked, "max", lim.HeroCap) : string.Empty);
            _noSlots.gameObject.SetActive(lim.Slots <= 0);

            for (int i = 0; i < _model.Candidates.Count; i++)
            {
                BagEntry e = _model.Candidates[i];
                if (!_cellButtons.TryGetValue(e.Uid, out UiButton b) || b == null) continue;
                b.Picked = e.Picked;
                if (e.Borrowed) continue;
                if (_model.BagLocked) b.SetDisabled(SettlementNameKeys.BagLockedNote);
                else if (!e.Baggable) b.SetDisabled(SettlementNameKeys.BagNoSlots);
                else b.SetEnabled();
            }

            // 가방 칸: 한 줄 8칸(TV-156). 고른 순서대로 채운다.
            Clear(_slotObjects);
            List<int> rows = SettlementRules.BagRows(lim.Slots);
            float tlh = UiTheme.LineHeight(UiFontId.F24);
            float y0 = Pad * 0.5f + tlh + RowGap * 2f;
            int k = 0;
            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < rows[r]; c++, k++)
                {
                    float x = Pad + c * (SlotSize + SlotGap);
                    float y = y0 + r * (SlotSize + SlotGap);
                    bool filled = k < _model.Picked.Count;
                    BagEntry e = filled ? _model.Entry(_model.Picked[k]) : null;
                    // 귀환 가방 칸(보충 그림 I-M12/bag_slot_empty|filled|hero 32×32)을 2배로 칸 가운데에. 그림이 없으면 K-60 칸.
                    string bagVariant = !filled ? "bag_slot_empty" : e != null && e.Rarity == Ids.Rarity.Hero ? "bag_slot_hero" : "bag_slot_filled";
                    UiSprite bagSlot = UiKit.KitSprite(Ctx, "I-M12", bagVariant);
                    if (bagSlot.IsValid)
                    {
                        const float BagSlotPx = 64f;
                        float inset = (SlotSize - BagSlotPx) * 0.5f;
                        _slotObjects.Add(UiKit.Image(_bagBox, bagSlot.Sprite, x + inset, y + inset, BagSlotPx, BagSlotPx, null, false).gameObject);
                    }
                    else
                    {
                        var slot = UiKit.NineSlice(Ctx, _bagBox, "K-60", filled ? "item_slot_filled" : "item_slot_empty", null, x, y, SlotSize, SlotSize, UiTheme.WoodDark);
                        _slotObjects.Add(slot.gameObject);
                    }
                    if (!filled) continue;
                    if (e == null) continue;
                    const int IconPx = 48;
                    _slotObjects.Add(UiKit.Icon(Ctx, _bagBox, e.Block.BlockId, IconPx, x + (SlotSize - IconPx) * 0.5f, y + (SlotSize - IconPx) * 0.5f)
                        .gameObject);
                    // 서고 남은 자리를 넘는 칸은 골드로 바뀐다(흐리게).
                    if (k >= _model.LibraryRoom)
                        _slotObjects.Add(UiKit.Rect(_bagBox, x, y, SlotSize, SlotSize, UiTheme.ModalDim).gameObject);
                }

            _bagNote.SetText(BagNoteText());
            _leftGold.SetText(F(SettlementNameKeys.BagLeftoverGold, "gold", N(_model.LeftoverGold)));
            _leftLibrary.gameObject.SetActive(_model.LibraryOverflow || _model.LibraryFullOnCommit);
            _leftBorrowed.gameObject.SetActive(_model.HasBorrowed);

            if (_model.BagLocked) { _bagAuto.SetDisabled(SettlementNameKeys.BagLockedNote); }
            else if (lim.Slots <= 0) { _bagAuto.SetDisabled(SettlementNameKeys.BagNoSlots); }
            else _bagAuto.SetEnabled();
            if (Focus.Current == null && _model.Page == SettlementPage.ReturnBag) Focus.SetCursor(0, false);
        }

        string BagNoteText()
        {
            if (_model.BagLocked) return T(SettlementNameKeys.BagLockedNote);
            if (!_model.LastReject.HasValue) return string.Empty;
            switch (_model.LastReject.Value)
            {
                case BagToggleResult.Full: return T(SettlementNameKeys.BagFull);
                case BagToggleResult.HeroLimit: return T(SettlementNameKeys.BagHeroLimit);
                case BagToggleResult.Borrowed: return T(SettlementNameKeys.BagBorrowedNote);
                default: return string.Empty;
            }
        }

        // ================================================================ 3쪽

        void FillTakeaways()
        {
            _takeList.Clear();
            _bundleButton = null;
            SettlementResult r = _model.Result;
            float lh24 = UiTheme.LineHeight(UiFontId.F24);
            float lh16 = UiTheme.LineHeight(UiFontId.F16);

            if (r == null)
            {
                RectTransform row = _takeList.AddRow(lh24 + Pad * 2f);
                UiKit.Label(Ctx, row, T(SettlementNameKeys.TakeNothing), UiFontId.F24, Pad, Pad, TakeW - Pad * 2f, lh24, UiTheme.SlateBlue);
            }
            else
            {
                if (r.RevengeReward) RevengeRow(lh24, lh16);
                SimpleRow(T(SettlementNameKeys.TakeGold), F(SettlementNameKeys.FormatGoldAmount, "n", N(r.GoldGained)), UiTheme.Gold);
                if (r.Bagged != null && r.Bagged.Count > 0)
                {
                    var names = new List<string>();
                    for (int i = 0; i < r.Bagged.Count; i++) if (r.Bagged[i] != null) names.Add(T(r.Bagged[i].BlockId));
                    ListRow(T(SettlementNameKeys.TakeBagged), names, null);
                }
                if (_model.LibraryFullOnCommit) SimpleRow(string.Empty, T(SettlementNameKeys.BagLibraryFull), UiTheme.Amber);
                if (_model.ConfirmedMaterials.Count > 0) ListRow(T(SettlementNameKeys.TakeMaterials), ItemLines(_model.ConfirmedMaterials), null);
                if (_model.CrowMaterials.Count > 0) ListRow(T(SettlementNameKeys.ItemCrowBundle), ItemLines(_model.CrowMaterials), null);
                if (_model.BundleMaterials.Count > 0) BundleRow(lh24);
                if (r.NewArts != null && r.NewArts.Count > 0)
                {
                    var arts = new List<string>();
                    for (int i = 0; i < r.NewArts.Count; i++) arts.Add(T(r.NewArts[i]));
                    ListRow(T(SettlementNameKeys.TakeNewArt), arts, null);
                }
                if (_model.NewTitles.Count > 0)
                {
                    var titles = new List<string>();
                    for (int i = 0; i < _model.NewTitles.Count; i++) titles.Add(T(_model.NewTitles[i]));
                    ListRow(T(SettlementNameKeys.TakeNewTitle), titles, null);
                }
            }

            _enhanceHint.gameObject.SetActive(_model.ShowEnhanceHint);
            _enhanceButton.On = _model.ShowEnhanceHint;
        }

        List<string> ItemLines(List<MaterialGain> gains)
        {
            var lines = new List<string>();
            for (int i = 0; i < gains.Count; i++) lines.Add(F(SettlementNameKeys.TakeItemCount, "name", T(gains[i].ItemId), "n", gains[i].Qty));
            return lines;
        }

        void SimpleRow(string label, string value, Color valueColor)
        {
            float lh = UiTheme.LineHeight(UiFontId.F24);
            RectTransform row = _takeList.AddRow(lh + Pad);
            UiKit.Panel(Ctx, row, UiPanelKind.Recess, 0f, 0f, TakeW - Pad, lh + Pad);
            UiKit.Label(Ctx, row, label, UiFontId.F24, Pad, Pad * 0.5f, TakeW * 0.3f, lh, UiTheme.Chalk);
            UiKit.Label(Ctx, row, value, UiFontId.F24, TakeW * 0.3f, Pad * 0.5f, TakeW * 0.7f - Pad * 3f, lh, valueColor);
        }

        void ListRow(string label, List<string> items, Color? color)
        {
            float lh = UiTheme.LineHeight(UiFontId.F24);
            float ilh = UiTheme.LineHeight(UiFontId.F16);
            int n = items.Count;
            float h = Mathf.Max(lh, n * ilh + (n - 1) * 4f) + Pad;
            RectTransform row = _takeList.AddRow(h);
            UiKit.Panel(Ctx, row, UiPanelKind.Recess, 0f, 0f, TakeW - Pad, h);
            UiKit.Label(Ctx, row, label, UiFontId.F24, Pad, Pad * 0.5f, TakeW * 0.3f, lh, UiTheme.Chalk);
            for (int i = 0; i < n; i++)
                UiKit.Label(Ctx, row, items[i], UiFontId.F16, TakeW * 0.3f, Pad * 0.5f + i * (ilh + 4f), TakeW * 0.7f - Pad * 3f, ilh,
                    color ?? UiTheme.Paper);
        }

        void BundleRow(float lh24)
        {
            float ilh = UiTheme.LineHeight(UiFontId.F16);
            int n = _model.BundleOpened ? _model.BundleMaterials.Count : 0;
            float h = Mathf.Max(lh24 + ButtonH * 0.75f, n * (ilh + 4f)) + Pad;
            RectTransform row = _takeList.AddRow(h);
            UiKit.Panel(Ctx, row, UiPanelKind.Recess, 0f, 0f, TakeW - Pad, h);
            UiKit.Label(Ctx, row, T(SettlementNameKeys.TakeBundle), UiFontId.F24, Pad, Pad * 0.5f, TakeW * 0.3f, lh24, UiTheme.Chalk);
            const int IconPx = 48;
            _bundleAnim = _model.BundleOpened && Ctx != null && Ctx.Art != null ? Ctx.Art.UiAnim("I-M11", "bundle_open") : null;
            if (_bundleAnim != null && _bundleAnim.Frames != null && _bundleAnim.Frames.Length > 0)
            {
                const int OpenPx = 64;
                _bundleImage = UiKit.Image(row, BundleFrame(), Pad - (OpenPx - IconPx) * 0.5f, Pad * 0.5f + lh24 - (OpenPx - IconPx) * 0.5f, OpenPx, OpenPx, null, false);
                _bundleImage.name = "BundleOpen";
            }
            else
            {
                _bundleImage = null;
                UiKit.Icon(Ctx, row, Ids.Item.MaterialBundle, IconPx, Pad, Pad * 0.5f + lh24);
            }
            if (!_model.BundleOpened)
            {
                _bundleButton = UiKit.Button(Ctx, row, T(SettlementNameKeys.TakeBundleOpen), TakeW * 0.3f, Pad * 0.5f, TakeW * 0.25f, ButtonH * 0.75f,
                    OnOpenBundle, UiButtonStyle.Medium, null, string.Empty);
                return;
            }
            List<string> lines = ItemLines(_model.BundleMaterials);
            for (int i = 0; i < lines.Count; i++)
                UiKit.Label(Ctx, row, lines[i], UiFontId.F16, TakeW * 0.3f, Pad * 0.5f + i * (ilh + 4f), TakeW * 0.7f - Pad * 3f, ilh, UiTheme.Paper);
        }

        void OnOpenBundle()
        {
            if (!_model.OpenBundle()) return;
            _bundleClock = 0f;
            ShowPage();
        }

        /// <summary>꾸러미 연출의 지금 장(한 번 재생, 끝나면 마지막 장).</summary>
        Sprite BundleFrame()
        {
            var a = _bundleAnim;
            if (a == null || a.Frames == null || a.Frames.Length == 0) return null;
            int last = a.Frames.Length - 1;
            int i = a.Fps > 0f && _bundleClock < float.MaxValue ? Mathf.Min(last, (int)(_bundleClock * a.Fps)) : last;
            return a.Frames[i];
        }

        public override void Tick(float dt)
        {
            base.Tick(dt);
            if (_bundleImage == null || _bundleClock == float.MaxValue) return;
            _bundleClock += dt;
            _bundleImage.sprite = BundleFrame();
        }

        /// <summary>복수 보상 줄(맨 위, TV-163): 거울 표식(복수 완료 꼴), 칭호, 머리 장식, 귀속 재료 + 자물쇠 + "계정에 묶임", 스승의 한마디.</summary>
        void RevengeRow(float lh24, float lh16)
        {
            int lines = 2 + _model.RevengeMaterials.Count;
            float h = Pad + lh24 + RowGap + lines * (lh16 + 4f) + RowGap + lh16 + Pad;
            RectTransform row = _takeList.AddRow(h);
            UiKit.Panel(Ctx, row, UiPanelKind.Board, 0f, 0f, TakeW - Pad, h);
            const int MirrorPx = 64;
            UiSprite mirror = UiKit.KitSprite(Ctx, "S-2603", MirrorVariant(MirrorMark.Broken));
            if (mirror.IsValid) UiKit.Image(row, mirror.Sprite, Pad, Pad, MirrorPx, MirrorPx);
            float x = Pad * 2f + MirrorPx;
            float w = TakeW - x - Pad * 2f;
            UiKit.Label(Ctx, row, T(SettlementNameKeys.TakeRevengeReward), UiFontId.F24, x, Pad, w * 0.5f, lh24, UiTheme.Chalk);
            // 복수 완료 꼴(revenge.tracker.done + 확정 체크).
            UiKit.Label(Ctx, row, T(SettlementNameKeys.TrackerDone), UiFontId.F24, x + w * 0.5f, Pad, w * 0.4f, lh24, UiTheme.Good, TextAnchor.MiddleRight);
            UiSprite check = UiKit.KitSprite(Ctx, "K-41", "confirmation");
            float cs = check.IsValid && check.Height > 0 ? check.Height : 16f;
            if (check.IsValid) UiKit.Image(row, check.Sprite, x + w * 0.9f + 4f, Pad + (lh24 - cs) * 0.5f, check.Width > 0 ? check.Width : cs, cs);
            else UiKit.Rect(row, x + w * 0.9f + 4f, Pad + (lh24 - cs) * 0.5f, cs, cs, UiTheme.Good);

            float y = Pad + lh24 + RowGap;
            UiKit.Label(Ctx, row, F(SettlementNameKeys.TakeTitle, "name", T(Ids.Title.MirrorBreaker)), UiFontId.F16, x, y, w, lh16, UiTheme.Paper);
            y += lh16 + 4f;
            BoundLine(row, F(SettlementNameKeys.TakeHeadwear, "name", T(Ids.Cosmetic.CrackedMirrorShard)), x, y, w, lh16);
            y += lh16 + 4f;
            for (int i = 0; i < _model.RevengeMaterials.Count; i++)
            {
                MaterialGain g = _model.RevengeMaterials[i];
                BoundLine(row, F(SettlementNameKeys.TakeItemCount, "name", T(g.ItemId), "n", g.Qty), x, y, w, lh16);
                y += lh16 + 4f;
            }
            y += RowGap;
            UiKit.Label(Ctx, row, T(SettlementNameKeys.RevengeMasterLine), UiFontId.F16, x, y, w, lh16, UiTheme.SlateBlue);
        }

        /// <summary>계정에 묶인 것 한 줄: 이름 + 자물쇠 + "계정에 묶임".</summary>
        void BoundLine(RectTransform row, string text, float x, float y, float w, float lh)
        {
            UiKit.Label(Ctx, row, text, UiFontId.F16, x, y, w * 0.5f, lh, UiTheme.Paper);
            const int LockPx = 16;
            UiSprite lockIcon = UiKit.KitSprite(Ctx, "I-F9", "lock");
            float lx = x + w * 0.5f;
            if (lockIcon.IsValid) UiKit.Image(row, lockIcon.Sprite, lx, y + (lh - LockPx) * 0.5f, LockPx, LockPx);
            else UiKit.Rect(row, lx, y + (lh - LockPx) * 0.5f, LockPx, LockPx, UiTheme.SlateBlue);
            UiKit.Label(Ctx, row, T(SettlementNameKeys.TakeBound), UiFontId.F16, lx + LockPx + 8f, y, w * 0.5f - LockPx - 8f, lh, UiTheme.SlateBlue);
        }

        // ================================================================ 입력

        void Go(SettlementNav nav)
        {
            if (nav == null) return;
            nav.Apply(Screens);
        }

        /// <summary>Esc: 정산을 건너뛸 수 없다. 확정 뒤 3쪽이면 메인 메뉴로.</summary>
        public override bool OnBack()
        {
            if (_model == null) return true;
            Go(_model.Back());
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (_model == null) return base.OnMenuKey(key);
            if (key == MenuKey.Cancel || key == MenuKey.PrevTab)
            {
                if (_model.Prev()) ShowPage();
                return true;
            }
            if (key == MenuKey.NextTab)
            {
                if (_model.Next()) ShowPage();
                return true;
            }
            return base.OnMenuKey(key);
        }

        // ================================================================ 도우미

        static void SetImage(UImage img, Sprite s)
        {
            if (img == null) return;
            img.sprite = s;
            img.preserveAspect = s != null;
            img.enabled = s != null;
        }

        static void Clear(List<GameObject> objects)
        {
            for (int i = 0; i < objects.Count; i++) if (objects[i] != null) Object.Destroy(objects[i]);
            objects.Clear();
        }
    }
}
