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
    public sealed partial class SettlementScreen : ScreenBase
    {
        // 자리: UiLayout 12열 격자(Content 좌표 = 화면 좌표 − 위 띠 80). 왼쪽 0~5열 = 돌아보기·가방 후보, 오른쪽 6~11열 = 점수·가방.
        //   주 버튼(다음·다시 원정)은 UiLayout.PrimaryRect, 보조 버튼은 그 왼쪽 같은 줄.
        const float Top = TopBarHeight;
        static readonly float LX = UiLayout.ColX(0), HalfW = UiLayout.ColW(6), RX = UiLayout.ColX(6);
        /// <summary>머리(큰 제목 F48 + 시작 웨이브 F24) 밑에서 판들이 시작한다.</summary>
        const float RecordY = 24f, RecordH = 56f, BodyY = 128f;
        static readonly float RecordX = LX, RecordW = UiLayout.InnerW;
        static readonly float ArtX = LX, ArtY = BodyY;
        const float ArtSize = 384f, WandSize = 192f;
        static readonly float ScoreX = RX, ScoreW = HalfW;
        const float ScoreY = BodyY, ScoreH = 480f;
        static readonly float TrackerX = RX, TrackerW = HalfW;
        const float TrackerH = 96f;
        static readonly float WildX = LX, WildW = HalfW, BagX = RX, BagW = HalfW, LeftX = RX, LeftW = HalfW;
        const float WildY = 24f, WildH = 640f, BagY = 24f, BagH = 360f, LeftH = 176f;
        static readonly float TakeX = UiLayout.ColX(2), TakeW = UiLayout.ColW(8);
        const float TakeY = 24f, TakeH = 680f;
        static readonly float ButtonsY = UiLayout.SecondaryYBesidePrimary(Top);
        const float ButtonH = UiLayout.SecondaryH, SecondaryW = 320f;
        const float Pad = UiLayout.SurfacePad, RowGap = 8f;
        const float CellW = 200f, CellH = 128f, CellGap = 8f, SlotSize = 80f, SlotGap = 8f;
        const int WildColumns = 4;
        const float PageLabelW = 200f;
        /// <summary>점수 줄이 차례로 나오는 간격(연출, 초).</summary>
        const float RowRevealSeconds = 0.12f;

        public override ScreenId Id => ScreenId.Settlement;
        protected override string TitleKey => SettlementNameKeys.Title;

        SettlementModel _model;

        readonly RectTransform[] _pages = new RectTransform[3];
        readonly UiLabel[] _pageLabels = new UiLabel[3];
        UiButton _recordDetails;
        bool _showRecordDetails;

        readonly List<UiWidget>[] _pageWidgets = { new List<UiWidget>(), new List<UiWidget>(), new List<UiWidget>() };

        // 1쪽
        UiLabel _recordLine, _startLine, _nameLine;
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
            BuildFxRoot(content);
        }

        void BuildRecord(RectTransform page)
        {
            // 머리: 큰 제목(F48) + 시작 웨이브 한 줄(F24 회청). 왼쪽 끝 정렬(격자 0열).
            _recordLine = UiKit.Label(Ctx, page, string.Empty, UiFontId.F48, RecordX, RecordY, RecordW, RecordH, UiTheme.Chalk, TextAnchor.MiddleLeft);
            UiKit.DropShadow(_recordLine, 3f);
            float slh = UiTheme.LineHeight(UiFontId.F24);
            _startLine = UiKit.Label(Ctx, page, string.Empty, UiFontId.F24, RecordX, RecordY + RecordH, RecordW, slh, UiTheme.SlateBlue,
                TextAnchor.MiddleLeft);

            // 쓰러진 캐릭터 받침 + 캐릭터 큰 그림 + 지팡이(판 끝 요약이 있으면 그 칸이 대신 선다 — SettlementScreen.Recap.cs).
            _artObjects.Add(UiLayout.Surface(Ctx, page, ArtX, ArtY, ArtSize, ArtSize, 0).gameObject);
            _charImage = UiKit.Image(page, null, ArtX, ArtY, ArtSize, ArtSize, Color.white);
            _artObjects.Add(_charImage.gameObject);
            float wy = ArtY + ArtSize + RowGap;
            _artObjects.Add(UiLayout.Surface(Ctx, page, ArtX + (ArtSize - WandSize) * 0.5f, wy, WandSize, WandSize, 0).gameObject);
            _wandImage = UiKit.Image(page, null, ArtX + (ArtSize - WandSize) * 0.5f, wy, WandSize, WandSize, Color.white);
            _artObjects.Add(_wandImage.gameObject);
            _nameLine = UiKit.Label(Ctx, page, string.Empty, UiFontId.F24, ArtX, wy + WandSize + RowGap, ArtSize, slh, UiTheme.Chalk,
                TextAnchor.MiddleCenter);
            _artObjects.Add(_nameLine.gameObject);
            BuildRecapBox(page);

            // 점수: 구역 제목 + 이름표 ― 값 줄(내용 높이에 맞춘 면). FillRecord가 줄을 채우고 높이를 잰다.
            _scoreBox = UiKit.Node(page, "ScoreBreakdown", ScoreX, ScoreY, ScoreW, ScoreH);

            _trackerBox = UiKit.Node(page, "RevengeTracker", TrackerX, 0f, TrackerW, TrackerH);

            // 주 행동 하나: 늘 같은 자리(PrimaryRect).
            _recordNext = UiLayout.PrimaryButton(Ctx, page, T(SettlementNameKeys.CommonNext), () => { if (_model.Next()) ShowPage(); }, null, Top);
            _pageWidgets[0].Add(_recordNext);
            _recordDetails = UiLayout.SecondaryButton(Ctx, page, T("str.ui.details"), LX, ButtonsY, 184f, () =>
            {
                _showRecordDetails = !_showRecordDetails;
                _recordDetails.SetText(T(_showRecordDetails ? "str.ui.hide_details" : "str.ui.details"));
                FillRecord();
            });
        }

        void BuildBag(RectTransform page)
        {
            // 왼쪽 0~5열: 야생 블록(구역 제목 + 칸 목록). 면은 BuildWildCells가 칸 수에 맞춰 깐다.
            _wildBox = UiKit.Node(page, "WildBlocks", WildX, WildY, WildW, WildH);
            var wild = _wildBox;
            float tlh = UiTheme.LineHeight(UiFontId.F24);
            float listY = UiLayout.Section(Ctx, wild, Pad, Pad, WildW - Pad * 2f, T(SettlementNameKeys.BagWildTitle));
            _wildList = UiKit.ScrollList(Ctx, wild, Pad, listY, WildW - Pad, WildH - listY - Pad, Focus, CellGap);
            _wildEmpty = UiKit.Label(Ctx, wild, T(SettlementNameKeys.BagEmpty), UiFontId.F24, Pad, listY, WildW - Pad * 2f, tlh, UiTheme.SlateBlue);

            // 오른쪽 6~11열: 가방(제목 + 칸 수 + 영웅 한도, 칸 줄, 안내), 그 밑 담지 못한 것.
            _bagBox = UiKit.Node(page, "ReturnBag", BagX, BagY, BagW, BagH);
            float by = UiLayout.Section(Ctx, _bagBox, Pad, Pad, BagW - Pad * 2f, T(SettlementNameKeys.BagTitle));
            _bagCounter = UiKit.Label(Ctx, _bagBox, string.Empty, UiFontId.F24, Pad, by, BagW * 0.5f - Pad, tlh, UiTheme.Chalk);
            _heroCounter = UiKit.Label(Ctx, _bagBox, string.Empty, UiFontId.F24, BagW * 0.5f, by, BagW * 0.5f - Pad, tlh, UiTheme.Gold,
                TextAnchor.MiddleRight);
            float nlh = UiTheme.LineHeight(UiFontId.F16);
            _bagSlotsY = by + tlh + RowGap * 2f;
            _noSlots = UiKit.Label(Ctx, _bagBox, T(SettlementNameKeys.BagNoSlots), UiFontId.F24, Pad, _bagSlotsY, BagW - Pad * 2f, tlh,
                UiTheme.SlateBlue);
            _bagNote = UiKit.Label(Ctx, _bagBox, string.Empty, UiFontId.F16, Pad, BagH - Pad - nlh, BagW - Pad * 2f, nlh, UiTheme.Amber);

            _leftBox = UiKit.Node(page, "Leftover", LeftX, 0f, LeftW, LeftH);
            var left = _leftBox;
            float y = UiLayout.Section(Ctx, left, Pad, Pad, LeftW - Pad * 2f, T(SettlementNameKeys.BagLeftoverTitle));
            _leftGold = UiKit.Label(Ctx, left, string.Empty, UiFontId.F24, Pad, y, LeftW - Pad * 2f, tlh, UiTheme.Gold);
            y += tlh + RowGap;
            _leftLibrary = UiKit.Label(Ctx, left, T(SettlementNameKeys.BagLibraryFull), UiFontId.F16, Pad, y, LeftW - Pad * 2f, nlh, UiTheme.Amber);
            y += nlh + RowGap;
            _leftBorrowed = UiKit.Label(Ctx, left, T(SettlementNameKeys.BagBorrowedNote), UiFontId.F16, Pad, y, LeftW - Pad * 2f, nlh,
                UiTheme.SlateBlue);

            _bagNext = UiLayout.PrimaryButton(Ctx, page, T(SettlementNameKeys.CommonNext), () => { if (_model.Next()) ShowPage(); }, null, Top);
            _bagAuto = Btn(page, T(SettlementNameKeys.BagAuto), 0, () => { _model.AutoPick(); RefreshBag(); });
            _bagPrev = Btn(page, T(SettlementNameKeys.CommonPrev), 1, () => { if (_model.Prev()) ShowPage(); });
        }

        RectTransform _wildBox, _leftBox, _wildBg, _bagBg, _leftBg, _scoreBg, _trackerBg;
        float _bagSlotsY;

        void BuildTakeaways(RectTransform page)
        {
            _takeList = UiKit.ScrollList(Ctx, page, TakeX, TakeY, TakeW + Pad, TakeH, Focus, RowGap);
            float hlh = UiTheme.LineHeight(UiFontId.F16);
            // 주 행동 = 다시 원정(PrimaryRect). 강화·메인 메뉴는 그 왼쪽 보조 버튼. 강화 안내는 강화 버튼 바로 위.
            _againButton = UiLayout.PrimaryButton(Ctx, page, T(SettlementNameKeys.Again), () => Go(_model.Again()), null, Top);
            _enhanceButton = Btn(page, T(SettlementNameKeys.ToEnhance), 0, () => Go(_model.ToEnhance()));
            _menuButton = Btn(page, T(SettlementNameKeys.ToMenu), 1, () => Go(_model.ToMenu()));
            _enhanceHint = UiKit.Label(Ctx, page, T(SettlementNameKeys.EnhanceHint), UiFontId.F16, UiLayout.ColX(0), ButtonsY - RowGap - hlh,
                UiLayout.PrimaryRect.x + UiLayout.PrimaryW - UiLayout.ColX(0), hlh, UiTheme.SelectYellow, TextAnchor.MiddleRight);
        }

        /// <summary>주 버튼 왼쪽 같은 줄의 index번째 보조 버튼(0이 가장 가깝다).</summary>
        UiButton Btn(RectTransform page, string text, int index, System.Action onClick)
        {
            return UiLayout.SecondaryButton(Ctx, page, text, UiLayout.SecondaryXBesidePrimary(SecondaryW, index), ButtonsY, SecondaryW, onClick);
        }

        /// <summary>면을 bg 노드 안에 다시 깐다(내용 높이에 맞출 때). 반환 = 면.</summary>
        RectTransform Backing(ref RectTransform bg, RectTransform owner, float w, float h, int level)
        {
            if (bg == null)
            {
                bg = UiKit.Node(owner, "Bg", 0f, 0f, w, h);
                bg.SetAsFirstSibling();
            }
            for (int i = bg.childCount - 1; i >= 0; i--) Object.Destroy(bg.GetChild(i).gameObject);
            return UiLayout.Surface(Ctx, bg, 0f, 0f, w, h, level);
        }

        // ================================================================ 열기

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            RunSummary summary = Args.Get<RunSummary>(ScreenArgKeys.RunSummary);
            if (summary == null && Ctx != null && Ctx.Session != null) summary = Ctx.Session.LastSummary;
            _model = new SettlementModel(Ctx != null ? Ctx.Settlement : null, Ctx != null ? Ctx.Content : null,
                () => Ctx != null ? Ctx.Profile : null, Ctx != null ? Ctx.Sound : null, EndRun);
            _showRecordDetails = false;
            _recordDetails.SetText(T("str.ui.details"));
            _model.Begin(summary);
            PrepareRecap(summary);
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
            ScheduleCelebration();
        }

        List<UiWidget> PageWidgets(SettlementPage p)
        {
            var list = new List<UiWidget>();
            switch (p)
            {
                case SettlementPage.Record:
                    list.Add(_recordNext);
                    list.Add(_recordDetails);
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
            float lh = UiTheme.LineHeight(UiFontId.F32);
            float rowH = lh + 4f;
            float rowW = ScoreW - Pad * 2f;
            var section = UiKit.Node(_scoreBox, "ScoreSection", Pad, Pad, rowW, UiLayout.SectionH);
            _scoreRowObjects.Add(section.gameObject);
            UiLayout.Section(Ctx, section, 0f, 0f, rowW, T(_showRecordDetails ? SettlementNameKeys.ScoreSection : "str.battle_hud.score"), UiFontId.F24);
            float y = Pad + UiLayout.SectionH;
            var scoreRows = _model.ScoreRows.FindAll(ShowScoreRow);
            for (int i = 0; i < scoreRows.Count; i++)
            {
                ScoreRow r = scoreRows[i];
                if (r.Total && i > 0)
                {
                    var d = UiKit.Rect(_scoreBox, Pad, y + RowGap, rowW, 1f, UiTheme.WithAlpha(UiTheme.Brass, 0.5f), false, "TotalRule");
                    _scoreRowObjects.Add(d.gameObject);
                    y += RowGap * 2f;
                }
                float rh = r.Total ? UiTheme.LineHeight(UiFontId.F48) : lh;
                UiFontId lf = r.Total ? UiFontId.F32 : UiFontId.F24;
                var l = UiKit.Label(Ctx, _scoreBox, T(r.Key), lf, Pad, y, rowW * 0.5f, rh, r.Total ? UiTheme.Chalk : UiTheme.Paper, TextAnchor.MiddleLeft);
                var v = UiKit.Label(Ctx, _scoreBox, N(r.Value), r.Total ? UiFontId.F48 : UiFontId.F32, Pad + rowW * 0.5f, y, rowW * 0.5f, rh,
                    r.Total ? UiTheme.Gold : UiTheme.Chalk, TextAnchor.MiddleRight);
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
                    // 최고 기록 알약(UiLayout.Chip, 금빛 글): 합계 이름 오른쪽.
                    var badge = UiLayout.Chip(Ctx, _scoreBox, Pad + 96f, y + (rh - UiLayout.ChipH) * 0.5f, T(SettlementNameKeys.BestRecord), null, UiTheme.Gold);
                    _scoreRowObjects.Add(badge.gameObject);
                    UiMotion.Entrance(badge, 0.15f + i * RowRevealSeconds + UiTheme.CountUpSeconds * 1.4f, 0.3f, Vector2.zero, 1.5f);
                }
                y += r.Total ? rh + RowGap : rowH;
            }
            float boxH = UiLayout.Snap(y + Pad - RowGap);
            UiKit.Place(_scoreBox, ScoreX, ScoreY, ScoreW, boxH);
            Backing(ref _scoreBg, _scoreBox, ScoreW, boxH, 1);

            FillTracker(_trackerBox, TrackerW, TrackerH, _trackerObjects);
            float below = ScoreY + boxH + 24f;
            UiKit.Place(_trackerBox, TrackerX, below, TrackerW, TrackerH);
            if (_trackerBox.gameObject.activeSelf) Backing(ref _trackerBg, _trackerBox, TrackerW, TrackerH, 0);
            FillRecap();
        }

        /// <summary>복수 추적기 큰 꼴(08 요약서 13.2절). 값은 모델의 RevengeTrackerView(= Meta.RevengeTracker.Build)만.</summary>
        bool ShowScoreRow(ScoreRow row) => row.Total || (_showRecordDetails && row.Value != 0);

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
                UiKit.Icon(Ctx, root, e.Block.BlockId, IconPx, (CellW - IconPx) * 0.5f, 8f);
                float lh = UiTheme.LineHeight(UiFontId.F16);
                UiKit.Label(Ctx, root, T(e.Block.BlockId), UiFontId.F16, 4f, 8f + IconPx + 4f, CellW - 8f, lh, UiTheme.Chalk,
                    TextAnchor.MiddleCenter);
                string tag = e.Borrowed ? T(SettlementNameKeys.BlockBorrowed) : T(SettlementNameKeys.BlockGradePrefix + e.Rarity);
                UiKit.Label(Ctx, root, tag, UiFontId.F16, 4f, 8f + IconPx + 4f + lh, CellW - 8f, lh, RarityColor(e),
                    TextAnchor.MiddleCenter);
                if (e.Borrowed)
                {
                    b.SetDisabled(SettlementNameKeys.BagBorrowedNote);
                    Wizard.Solo.Screens.Camp.CampViews.BorrowedTag(Ctx, root, Pad * 0.5f, Pad * 0.5f, 1);
                }
                _cellButtons[uid] = b;
            }
            // 면은 칸 줄 수에 맞춘다(빈 아랫부분을 남기지 않는다). 최대 WildH.
            int rowsN = Mathf.Max(1, (n + WildColumns - 1) / WildColumns);
            float listY = Pad + UiLayout.SectionH;
            float h = Mathf.Min(WildH, UiLayout.Snap(listY + (n == 0 ? UiTheme.LineHeight(UiFontId.F24) : rowsN * (CellH + CellGap) - CellGap) + Pad));
            UiKit.Place(_wildBox, WildX, WildY, WildW, h);
            Backing(ref _wildBg, _wildBox, WildW, h, 1);
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
            float y0 = _bagSlotsY;
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

            // 가방 면과 담지 못한 것 면을 내용 높이에 맞춘다.
            float nlh = UiTheme.LineHeight(UiFontId.F16);
            float slotsH = lim.Slots <= 0 ? UiTheme.LineHeight(UiFontId.F24) : rows.Count * (SlotSize + SlotGap) - SlotGap;
            string note = BagNoteText();
            float bagH = UiLayout.Snap(y0 + slotsH + (string.IsNullOrEmpty(note) ? 0f : RowGap * 2f + nlh) + Pad);
            UiKit.Place(_bagBox, BagX, BagY, BagW, bagH);
            UiKit.Place(_bagNote.Rect, Pad, bagH - Pad - nlh, BagW - Pad * 2f, nlh);
            Backing(ref _bagBg, _bagBox, BagW, bagH, 1);
            float leftY = BagY + bagH + 24f;
            int extra = (_model.LibraryOverflow || _model.LibraryFullOnCommit ? 1 : 0) + (_model.HasBorrowed ? 1 : 0);
            float leftH = UiLayout.Snap(Pad + UiLayout.SectionH + UiTheme.LineHeight(UiFontId.F24) + extra * (nlh + RowGap) + Pad);
            UiKit.Place(_leftBox, LeftX, leftY, LeftW, leftH);
            Backing(ref _leftBg, _leftBox, LeftW, leftH, 0);
            _bagNote.SetText(note);
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
                UiLayout.Surface(Ctx, row, 0f, 0f, TakeW, lh24 + Pad * 2f, 0);
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
                ChallengeRows();
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

        /// <summary>가져가는 것 한 줄: 왼쪽 이름표(F16 회청) ― 오른쪽 값(F24). 들어간 면, 안쪽 여백 Pad.</summary>
        void SimpleRow(string label, string value, Color valueColor)
        {
            float lh = UiTheme.LineHeight(UiFontId.F24);
            float h = lh + Pad * 2f;
            RectTransform row = _takeList.AddRow(h);
            UiLayout.Surface(Ctx, row, 0f, 0f, TakeW, h, 0);
            UiKit.Label(Ctx, row, label, UiFontId.F16, Pad, Pad, LabelColW, lh, UiTheme.SlateBlue);
            UiKit.Label(Ctx, row, value, UiFontId.F24, Pad + LabelColW, Pad, TakeW - LabelColW - Pad * 2f, lh, valueColor);
        }

        static float LabelColW => UiLayout.ColW(2);

        void ListRow(string label, List<string> items, Color? color)
        {
            float lh = UiTheme.LineHeight(UiFontId.F24);
            int n = items.Count;
            float h = Mathf.Max(1, n) * lh + Mathf.Max(0, n - 1) * 4f + Pad * 2f;
            RectTransform row = _takeList.AddRow(h);
            UiLayout.Surface(Ctx, row, 0f, 0f, TakeW, h, 0);
            UiKit.Label(Ctx, row, label, UiFontId.F16, Pad, Pad, LabelColW, lh, UiTheme.SlateBlue);
            for (int i = 0; i < n; i++)
                UiKit.Label(Ctx, row, items[i], UiFontId.F24, Pad + LabelColW, Pad + i * (lh + 4f), TakeW - LabelColW - Pad * 2f, lh,
                    color ?? UiTheme.Chalk);
        }

        void BundleRow(float lh24)
        {
            float ilh = lh24;
            int n = _model.BundleOpened ? _model.BundleMaterials.Count : 0;
            float h = Mathf.Max(UiTheme.LineHeight(UiFontId.F16) + 56f, Mathf.Max(ButtonH, n * (ilh + 4f))) + Pad * 2f;
            RectTransform row = _takeList.AddRow(h);
            UiLayout.Surface(Ctx, row, 0f, 0f, TakeW, h, 0);
            UiKit.Label(Ctx, row, T(SettlementNameKeys.TakeBundle), UiFontId.F16, Pad, Pad, LabelColW, UiTheme.LineHeight(UiFontId.F16), UiTheme.SlateBlue);
            lh24 = UiTheme.LineHeight(UiFontId.F16) + 4f;
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
                _bundleButton = UiLayout.SecondaryButton(Ctx, row, T(SettlementNameKeys.TakeBundleOpen), Pad + LabelColW, Pad, SecondaryW, OnOpenBundle);
                return;
            }
            List<string> lines = ItemLines(_model.BundleMaterials);
            for (int i = 0; i < lines.Count; i++)
                UiKit.Label(Ctx, row, lines[i], UiFontId.F24, Pad + LabelColW, Pad + i * (ilh + 4f), TakeW - LabelColW - Pad * 2f, ilh, UiTheme.Chalk);
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
            TickCelebration(dt);
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
            UiLayout.Surface(Ctx, row, 0f, 0f, TakeW, h, 1);
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
