using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>
    /// 기록(ScreenId.Records, 08 11.1절, UA 4.18). 오프라인 프로필의 기기 안 기록만.
    /// 캐릭터 탭 6개 (48,104) 1824×48 / 요약 판 (48,168) 560×760 / 판 목록 (632,168) 1240×760(줄 높이 64).
    /// 결투 전적·시즌 결과 줄과 [다시 보기] 단추는 그리지 않는다(D-51, N-10, N-11). 규칙은 RecordsModel.
    /// Q/E = 탭, 위·아래 = 목록 굴리기, J = 얻은 칭호 줄(→ TitlePicker), Esc·K = 나가기.
    /// </summary>
    public sealed partial class RecordsScreen : ScreenBase
    {
        // 08 11.1절 자리(1920×1080 기준) → 내용 좌표(위 띠 80을 뺀다).
        const float Top = TopBarHeight;
        const float TabsX = 48f, TabsY = 104f - Top, TabsW = 1824f, TabsH = 48f, TabGap = 4f;
        const float SumX = 48f, SumY = 168f - Top, SumW = 560f, SumH = 760f;
        const float ListX = 632f, ListY = 168f - Top, ListW = 1240f, ListH = 760f, RowH = 64f, HeadH = 40f;
        const float TrackerW = 432f, TrackerH = 96f, Pad = 24f, SumRowH = 56f;
        const float LoadoutIcon = 24f, LoadoutGap = 4f;
        static readonly float[] ColumnW = { 200f, 150f, 220f, 160f, 160f, 110f, 200f };

        int _tab;
        RectTransform _summary, _revengeSlot;
        UiScrollList _list;
        readonly List<UiLabel> _sumValues = new List<UiLabel>();
        UiLabel _stainLabel, _emptyLabel;
        UiButton _titlesButton;

        public override ScreenId Id => ScreenId.Records;
        protected override string TitleKey => MetaKeys.RecordsTitle;

        protected override void BuildContent(RectTransform content)
        {
            int tabs = RecordsModel.TabCountWithChallenges;
            var labels = new List<string>(tabs);
            for (int i = 0; i < tabs; i++) labels.Add(T(RecordsModel.TabKey(i)));
            float tabW = (TabsW - TabGap * (tabs - 1)) / tabs;
            Tabs = UiKit.Tabs(Ctx, content, labels, TabsX, TabsY, tabW, TabsH, SelectTab, 0, false, TabGap);

            BuildSummary(content);
            BuildList(content);
            BuildChallengeBoard(content);
        }

        void BuildSummary(RectTransform content)
        {
            _summary = UiKit.OrnatePanel(Ctx, content, SumX, SumY, SumW, SumH, 1).rectTransform;
            _summary.name = "RecordsSummary";
            float y = Pad;
            float lh = UiTheme.LineHeight(UiFontId.F24);
            for (int i = 0; i < RecordsModel.SummaryRowKeys.Length; i++)
            {
                string key = RecordsModel.SummaryRowKeys[i];
                if (key == MetaKeys.RecordsTitles)
                {
                    // 얻은 칭호 줄 = 칭호 고르기로 가는 단추(08 11.1절).
                    _titlesButton = UiKit.Button(Ctx, _summary, null, Pad, y, SumW - 2f * Pad, SumRowH, OpenTitlePicker,
                        UiButtonStyle.Plain, Focus);
                    var root = _titlesButton.ContentRoot != null ? _titlesButton.ContentRoot : _titlesButton.Rect;
                    UiKit.Label(Ctx, root, T(key), UiFontId.F24, 8f, (SumRowH - lh) * 0.5f, 280f, lh, UiTheme.SlateBlue);
                    _sumValues.Add(UiKit.Label(Ctx, root, string.Empty, UiFontId.F24, 288f, (SumRowH - lh) * 0.5f, SumW - 2f * Pad - 296f, lh,
                        UiTheme.Chalk, TextAnchor.MiddleRight));
                }
                else
                {
                    UiKit.Label(Ctx, _summary, T(key), UiFontId.F24, Pad + 8f, y + (SumRowH - lh) * 0.5f, 280f, lh, UiTheme.SlateBlue);
                    _sumValues.Add(UiKit.Label(Ctx, _summary, string.Empty, UiFontId.F24, Pad + 288f, y + (SumRowH - lh) * 0.5f,
                        SumW - 2f * Pad - 296f, lh, UiTheme.Chalk, TextAnchor.MiddleRight));
                    if (key == MetaKeys.RecordsTotalWaves)
                    {
                        // 야생 물 단계(캐릭터 탭만): 누적 웨이브 줄 바로 아래 한 줄.
                        float sh = UiTheme.LineHeight(UiFontId.F16);
                        _stainLabel = UiKit.Label(Ctx, _summary, string.Empty, UiFontId.F16, Pad + 8f, y + SumRowH - 8f, SumW - 2f * Pad - 16f,
                            sh, UiTheme.SlateBlue, TextAnchor.MiddleRight);
                        y += sh;
                    }
                }
                y += SumRowH;
            }
            _revengeSlot = UiKit.Node(_summary, "RevengeRecord", (SumW - TrackerW) * 0.5f, SumH - Pad - TrackerH, TrackerW, TrackerH);
        }

        void BuildList(RectTransform content)
        {
            var board = UiKit.Plate(Ctx, content, UiPanelKind.Board, ListX, ListY, ListW, ListH).rectTransform;
            board.name = "RunList";
            _listBoard = board;
            float x = Pad;
            float lh = UiTheme.LineHeight(UiFontId.F16);
            for (int i = 0; i < RecordsModel.ColumnKeys.Length; i++)
            {
                UiKit.Label(Ctx, board, T(RecordsModel.ColumnKeys[i]), UiFontId.F16, x, (HeadH - lh) * 0.5f + 8f, ColumnW[i] - 8f, lh,
                    UiTheme.SlateBlue);
                x += ColumnW[i];
            }
            UiKit.Divider(Ctx, board, Pad, HeadH + 8f, ListW - 2f * Pad);
            _list = UiKit.ScrollList(Ctx, board, Pad, HeadH + 16f, ListW - 2f * Pad, ListH - HeadH - 16f - Pad, null, 0f);
            _emptyLabel = UiKit.Label(Ctx, board, T(MetaKeys.CommonEmpty), UiFontId.F24, Pad, HeadH + 64f, ListW - 2f * Pad,
                UiTheme.LineHeight(UiFontId.F24), UiTheme.SlateBlue, TextAnchor.MiddleCenter);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            Refresh();
            Focus.SetCursor(0, false);
        }

        void SelectTab(int i)
        {
            _tab = i;
            Refresh();
        }

        void Refresh()
        {
            var p = Ctx != null ? Ctx.Profile : null;
            bool challenges = _tab == RecordsModel.ChallengeTab;
            _summary.gameObject.SetActive(!challenges);
            if (_listBoard != null) _listBoard.gameObject.SetActive(!challenges);
            if (_challengeBoard != null) _challengeBoard.gameObject.SetActive(challenges);
            if (challenges) { FillChallenges(p); return; }
            var db = Ctx != null ? Ctx.Content : null;
            var thresholds = WardrobeModel.StainThresholds(db);
            var milestones = TitleModel.Milestones(db);
            var s = RecordsModel.Summary(p, _tab, thresholds, milestones);

            // 요약 판 값(줄 순서 = RecordsModel.SummaryRowKeys).
            for (int i = 0; i < RecordsModel.SummaryRowKeys.Length && i < _sumValues.Count; i++)
            {
                string key = RecordsModel.SummaryRowKeys[i];
                string v;
                if (key == MetaKeys.RecordsBestScore) v = N(s.BestScore);
                else if (key == MetaKeys.RecordsBestWave)
                    v = s.BestWave > 0 ? F(MetaKeys.FormatDayWave, "day", RecordsModel.DayOf(s.BestWave), "wave", s.BestWave) : N(0);
                else if (key == MetaKeys.RecordsTotalWaves) v = N(s.TotalWaves);
                else v = N(s.TitleCount);
                _sumValues[i].SetText(v);
            }
            if (_stainLabel != null)
                _stainLabel.SetText(s.WildStainStage > 0 ? T(WardrobeModel.WildStainNameKey(s.WildStainStage)) : string.Empty);

            BuildRevenge(p);
            BuildRows(p);
        }

        void BuildRevenge(ProfileData p)
        {
            MetaUi.ClearChildren(_revengeSlot);
            var v = RevengeTracker.Build(p, Ctx != null ? Ctx.Content : null);
            var kind = RecordsModel.RevengeKind(_tab, v);
            if (kind == RevengeRecordKind.None) { _revengeSlot.gameObject.SetActive(false); return; }
            _revengeSlot.gameObject.SetActive(true);
            UiKit.Panel(Ctx, _revengeSlot, UiPanelKind.Recess, 0f, 0f, TrackerW, TrackerH);
            float lh = UiTheme.LineHeight(UiFontId.F16);
            if (kind == RevengeRecordKind.Done)
            {
                DrawMirror(_revengeSlot, v.Mirror, 16f, (TrackerH - 32f) * 0.5f);
                UiKit.Label(Ctx, _revengeSlot, F(MetaKeys.RevengeDoneLine, "date", RecordsModel.DatePart(v.DoneDate), "n", v.RunsToComplete),
                    UiFontId.F16, 60f, (TrackerH - lh) * 0.5f, TrackerW - 76f, lh, UiTheme.Chalk);
                return;
            }
            // 큰 꼴(08 13.2절): 목표 / 진행 + 막대 / 흠집(한 번이라도 닿았을 때만).
            float x = 60f, w = TrackerW - 76f;
            float y = RecordsModel.TrackerLines(v) == 3 ? 6f : 18f;
            DrawMirror(_revengeSlot, v.Mirror, 16f, (TrackerH - 32f) * 0.5f);
            UiKit.Label(Ctx, _revengeSlot, T(MetaKeys.RevengeGoal), UiFontId.F16, x, y, w, lh, UiTheme.Chalk);
            y += lh + 4f;
            UiKit.Label(Ctx, _revengeSlot, F(MetaKeys.RevengeProgress, "n", v.Progress), UiFontId.F16, x, y, w * 0.6f, lh, UiTheme.Chalk);
            UiKit.ProgressBar(Ctx, _revengeSlot, x + w * 0.62f, y + (lh - 8f) * 0.5f, w * 0.38f, 8f, RecordsModel.TrackerProgress01(v));
            y += lh + 4f;
            if (v.HasDent)
                UiKit.Label(Ctx, _revengeSlot, F(MetaKeys.RevengeDent, "percent", v.DentPercent, "tries", v.Tries), UiFontId.F16, x, y, w, lh,
                    UiTheme.SlateBlue);
        }

        /// <summary>거울 표식: 고리 + 금(Cracked 한 줄, Broken 두 줄). 플레이어 쪽 표식이라 위험색을 쓰지 않는다.</summary>
        void DrawMirror(RectTransform parent, MirrorMark mark, float x, float y)
        {
            const float Size = 32f;
            var ring = Ctx != null && Ctx.Art != null ? Ctx.Art.Circle((int)(Size * 0.5f), true, 3) : null;
            if (ring != null) UiKit.Image(parent, ring, x, y, Size, Size, UiTheme.Chalk, false);
            else UiKit.Rect(parent, x, y, Size, Size, UiTheme.WoodLight);
            int cracks = mark == MirrorMark.Broken ? 2 : mark == MirrorMark.Cracked ? 1 : 0;
            for (int i = 0; i < cracks; i++)
            {
                var line = UiKit.Rect(parent, x + Size * 0.5f - 1f, y + 4f, 2f, Size - 8f, mark == MirrorMark.Broken ? UiTheme.Gold : UiTheme.SlateBlue);
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 30f : -30f);
            }
        }

        void BuildRows(ProfileData p)
        {
            MetaUi.ClearChildren(_list.Content);
            _list.Clear();
            var runs = RecordsModel.Runs(p, _tab);
            _emptyLabel.gameObject.SetActive(runs.Count == 0);
            float lh = UiTheme.LineHeight(UiFontId.F16);
            for (int i = 0; i < runs.Count; i++)
            {
                var r = runs[i];
                var row = _list.AddRow(RowH, "Run" + i);
                if (i % 2 == 1) UiKit.Rect(row, 0f, 0f, ListW - 2f * Pad, RowH, UiTheme.WithAlpha(UiTheme.WoodLight, 0.35f));
                float x = 0f, y = (RowH - lh) * 0.5f;
                string[] cells =
                {
                    RecordsModel.DateTimeText(r.Date),
                    T(MetaKeys.Character(r.Character)),
                    r.ReachedWave > 0 ? F(MetaKeys.FormatDayWave, "day", RecordsModel.DayOf(r.ReachedWave), "wave", r.ReachedWave) : string.Empty,
                    F(MetaKeys.FormatWave, "n", r.StartWave),
                    N(r.Score),
                    F(MetaKeys.FormatWandStage, "n", r.WandStage)
                };
                for (int c = 0; c < cells.Length; c++)
                {
                    UiKit.Label(Ctx, row, cells[c], UiFontId.F16, x, y, ColumnW[c] - 8f, lh, UiTheme.Chalk);
                    x += ColumnW[c];
                }
                // 조립: 24×24 블록 아이콘 줄(코어 먼저, 빈 칸은 테두리만).
                if (r.Loadout != null)
                    for (int b = 0; b < r.Loadout.Count; b++)
                    {
                        float ix = x + b * (LoadoutIcon + LoadoutGap);
                        if (ix + LoadoutIcon > ListW - 2f * Pad - 16f) break;
                        float iy = (RowH - LoadoutIcon) * 0.5f;
                        string id = r.Loadout[b];
                        if (string.IsNullOrEmpty(id)) UiKit.Rect(row, ix, iy, LoadoutIcon, LoadoutIcon, UiTheme.WoodLight);
                        else UiKit.Icon(Ctx, row, id, (int)LoadoutIcon, ix, iy);
                    }
            }
        }

        void OpenTitlePicker()
        {
            var a = ScreenArgs.From(ScreenId.Records);
            a.OnResult = _ => Refresh();
            Screens?.Push(ScreenId.TitlePicker, a);
        }

        void Scroll(int dir)
        {
            if (_list == null || _list.Scroll == null) return;
            float viewH = _list.Viewport != null ? _list.Viewport.rect.height : ListH;
            float range = Mathf.Max(1f, _list.ContentHeight - viewH);
            float step = RowH / range;
            _list.Scroll.verticalNormalizedPosition = Mathf.Clamp01(_list.Scroll.verticalNormalizedPosition - dir * step);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            if (key == MenuKey.Up) { Scroll(-1); return true; }
            if (key == MenuKey.Down) { Scroll(1); return true; }
            return base.OnMenuKey(key);
        }
    }
}
