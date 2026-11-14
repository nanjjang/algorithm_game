using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 지팡이 고르기(08 10.2절, 07 3.4·3.5절). 인자: Character(int, 없으면 profile.last_character), ReturnTo(ScreenId).
    /// 가운데 1200×720: 왼쪽 캐릭터 세로 탭 5개(탭마다 "n자루", Q/E로도 바꾼다), 위 줄 정렬·거르기 넷, 줄 높이 88 세로 목록
    /// (아이콘·이름·단계·변이·슬롯·기운·변이 결정 대기, [든 지팡이로]/"든 지팡이", [떠나보내기]), 아래 [여러 자루 고르기].
    /// 줄을 고르면 Push(Enhance, WandId + ReturnTo). Esc → ReturnTo가 있으면 PopTo(그 화면), 없으면 Pop.
    /// 들어올 때 강화 첫 안내 조건이면 안내를 먼저 연다(D-67).
    /// </summary>
    public sealed class WandPickerScreen : ScreenBase
    {
        const float SortW = 196f, HeldShare = 0.4f;
        public const float PanelW = 1200f, PanelH = 720f, TabW = 200f, TabH = 56f, RowH = 88f, TopRowH = 48f;
        /// <summary>지팡이 카드 격자(3열). 카드 하나 = 그림·이름·큰 단계·정보 + 아래 단추 줄.</summary>
        public const int GridCols = 3;
        public const float CardH = 220f, CardGap = 16f;
        static readonly WandFilter[] FilterOrder = { WandFilter.HasMutation, WandFilter.HasExtSlot, WandFilter.TradeLocked, WandFilter.PlusZeroOnly };
        static readonly string[] FilterKeys =
        {
            EnhanceNameKeys.FilterHasMutation, EnhanceNameKeys.FilterHasExtSlot, EnhanceNameKeys.FilterTradeLocked, EnhanceNameKeys.FilterPlusZero
        };

        EnhanceScreenTuning _t;
        WandPickerModel _model;
        RectTransform _panel, _viewport, _rowsRoot;
        readonly List<UiLabel> _tabCounts = new List<UiLabel>();
        UiButton _sortButton, _multiButton, _multiRelease;
        readonly List<UiToggle> _filterToggles = new List<UiToggle>();
        UiLabel _summary;
        readonly List<UiWidget> _rowWidgets = new List<UiWidget>();
        readonly List<RectTransform> _rowRects = new List<RectTransform>();
        float _scrollY;

        public override ScreenId Id => ScreenId.WandPicker;
        protected override string TitleKey => EnhanceNameKeys.PickerTitle;
        public WandPickerModel Model => _model;

        protected override void BuildContent(RectTransform content)
        {
            _t = EnhanceScreenTuning.From(Ctx != null ? Ctx.Content : null);
            _model = new WandPickerModel(_t);
            float px = (UiScale.RefWidth - PanelW) * 0.5f, py = (ContentHeight - PanelH) * 0.5f;
            var bg = UiKit.Panel(Ctx, content, UiPanelKind.Board, px, py, PanelW, PanelH);
            _panel = bg.rectTransform;
            var picker = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupEnhance, "wand_picker");
            if (picker != null) UiKit.Image(_panel, picker, 0f, 0f, PanelW, PanelH, Color.white, false);

            var names = new List<string>();
            for (int c = 1; c <= Ids.Char.Count; c++) names.Add(T(Ids.Char.Of(c)));
            Tabs = UiKit.Tabs(Ctx, _panel, names, 16f, 16f, TabW, TabH, i => SelectCharacter(i + 1), 0, true, 8f);
            for (int c = 0; c < Ids.Char.Count; c++)
                _tabCounts.Add(UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F12, 16f, 16f + c * (TabH + 8f) + TabH - 18f, TabW - 12f, 16f,
                    UiTheme.SlateBlue, TextAnchor.MiddleRight));

            float lx = 16f + TabW + 16f, lw = PanelW - lx - 16f;
            _sortButton = UiKit.Button(Ctx, _panel, string.Empty, lx, 16f, SortW, TopRowH, CycleSort, UiButtonStyle.Small, Focus, null, UiFontId.F16);
            float fx = lx + SortW + 12f;
            // 거르기 토글은 남은 폭을 똑같이 나눈다(글 칸이 "확장 슬롯 있음"을 자르지 않게).
            float slot = (PanelW - 16f - fx) / FilterOrder.Length;
            for (int i = 0; i < FilterOrder.Length; i++)
            {
                var f = FilterOrder[i];
                var tg = UiKit.Toggle(Ctx, _panel, T(FilterKeys[i]), fx + i * slot, 16f, slot - 4f, TopRowH, false, v => SetFilter(f, v), Focus);
                _filterToggles.Add(tg);
            }

            _viewport = UiKit.Node(_panel, "Viewport", lx, 16f + TopRowH + 12f, lw, PanelH - (16f + TopRowH + 12f) - 96f);
            _viewport.gameObject.AddComponent<RectMask2D>();
            _rowsRoot = UiKit.Node(_viewport, "Rows", 0f, 0f, lw, 0f);

            float by = PanelH - 80f;
            _multiButton = UiKit.Button(Ctx, _panel, T(EnhanceNameKeys.PickerReleaseMulti), lx, by, 280f, 64f, ToggleMulti, UiButtonStyle.Medium, Focus);
            _summary = UiKit.Label(Ctx, _panel, string.Empty, UiFontId.F16, lx + 296f, by + 20f, lw - 296f - 256f, 0f);
            _multiRelease = UiKit.Button(Ctx, _panel, T(EnhanceNameKeys.PickerRelease), PanelW - 16f - 240f, by, 240f, 64f, ReleaseChecked,
                UiButtonStyle.Caution, Focus);
            Focus.CursorChanged = _ => FollowCursor();
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            int fallback = Ctx != null && Ctx.Profile != null ? Ctx.Profile.LastCharacter : 1;
            int c = Args.Get(ScreenArgKeys.Character, fallback);
            if (c < 1 || c > Ids.Char.Count) c = 1;
            _model.Character = c;
            if (Ctx != null && Ctx.Settings != null && Ctx.Settings.Current != null)
                _model.Sort = SettingsOptions.WandListSort(Ctx.Settings.Current);
            _model.MultiMode = false;
            _model.Checked.Clear();
            Tabs.Select(c - 1, false);
            Refresh();
            EnhanceTutorialGate.TryOpen(Ctx, Id);
        }

        void SelectCharacter(int c)
        {
            _model.Character = c;
            _model.Checked.Clear();
            Refresh();
        }

        void CycleSort()
        {
            int i = System.Array.IndexOf(WandPickerModel.SortOrder, _model.Sort);
            _model.Sort = WandPickerModel.SortOrder[(i + 1) % WandPickerModel.SortOrder.Length];
            if (Ctx != null && Ctx.Settings != null && Ctx.Settings.Current != null)
            {
                SettingsOptions.SetWandListSort(Ctx.Settings.Current, _model.Sort);
                Ctx.Settings.Save();
            }
            Refresh();
        }

        void SetFilter(WandFilter f, bool on)
        {
            _model.Filters = on ? _model.Filters | f : _model.Filters & ~f;
            Refresh();
        }

        void ToggleMulti()
        {
            _model.MultiMode = !_model.MultiMode;
            _model.Checked.Clear();
            Refresh();
        }

        /// <summary>목록을 다시 그린다.</summary>
        public void Refresh()
        {
            var wands = Ctx != null ? Ctx.Wands : null;
            _model.Refresh(wands);
            for (int c = 0; c < _tabCounts.Count; c++)
                _tabCounts[c].SetText(F(EnhanceNameKeys.ListCount, "n", WandPickerModel.CountOf(wands, c + 1)));
            _sortButton.SetText(T(WandPickerModel.SortKey(_model.Sort)));
            _multiButton.On = _model.MultiMode;

            foreach (var w in _rowWidgets) Focus.Remove(w);
            _rowWidgets.Clear();
            _rowRects.Clear();
            EnhanceUi.Clear(_rowsRoot);
            int hand = WandPickerModel.CountOf(wands, _model.Character);
            float lw = _viewport.rect.width > 0 ? _viewport.rect.width : PanelW - TabW - 64f;
            for (int i = 0; i < _model.Rows.Count; i++) BuildRow(_model.Rows[i], i, lw, hand);
            int gridRows = (_model.Rows.Count + GridCols - 1) / GridCols;
            UiKit.Place(_rowsRoot, 0f, 0f, lw, gridRows * (CardH + CardGap));
            _scrollY = 0f;
            ApplyScroll();

            var econ = Ctx != null && Ctx.Content != null ? Ctx.Content.Economy : null;
            _summary.gameObject.SetActive(_model.MultiMode);
            _multiRelease.gameObject.SetActive(_model.MultiMode);
            _summary.SetText(F(EnhanceNameKeys.PickerReleaseSummary, "n", _model.Checked.Count, "gold", N(_model.CheckedRefund(econ))));
            if (_model.Checked.Count > 0) _multiRelease.SetEnabled(); else _multiRelease.SetDisabled(null);
            if (_rowWidgets.Count > 0) Focus.SetCursor(_rowWidgets[0], false);
        }

        /// <summary>속성색(캐릭터의 속성). 위험색이면 Safe로 뺀다.</summary>
        Color ElementOf(int character)
        {
            var def = Ctx != null && Ctx.Content != null ? Ctx.Content.Character(Ids.Char.Of(character)) : null;
            return EnhanceFxStyle.Safe(UiTheme.ElementColor(def != null ? def.Element : null));
        }

        void BuildRow(WandListEntry e, int index, float width, int handCount)
        {
            var w = e.Wand;
            float cw = (width - (GridCols - 1) * CardGap) / GridCols;
            float x = (index % GridCols) * (cw + CardGap), y = (index / GridCols) * (CardH + CardGap);
            var row = UiKit.Node(_rowsRoot, "Card" + w.Id, x, y, cw, CardH);
            _rowRects.Add(row);
            var names = Ctx != null ? Ctx.Names : null;
            var cfg = Ctx != null && Ctx.Content != null ? Ctx.Content.Enhance : null;
            int max = cfg != null ? cfg.MaxStage : int.MaxValue;
            Color el = ElementOf(w.Character);
            int tier = EnhanceFxStyle.Tier(w.Stage);
            Color stageCol = w.Stage >= max ? UiTheme.Gold : tier >= 2 ? EnhanceFxStyle.TierColor(tier, el) : UiTheme.Chalk;
            Color edge = e.Equipped ? UiTheme.Good : Color.Lerp(el, stageCol, tier >= 2 ? 0.7f : 0f);

            // 카드 판: 그림자 · 짙은 유리 · 윤기 · 테(든 지팡이는 초록, +7 이상은 등급 색).
            EnhanceGfx.Panel(row, 4f, 6f, cw, CardH, EnhanceFxStyle.WithA(Color.black, 0.35f), false, "Shadow");
            EnhanceGfx.Panel(row, 0f, 0f, cw, CardH, EnhanceFxStyle.WithA(UiTheme.Ink, 0.9f), false, "Fill");
            UiKit.Image(row, EnhanceGfx.Sheen, 6f, 4f, cw - 12f, 70f, EnhanceFxStyle.WithA(Color.white, 0.06f), false);
            EnhanceGfx.Panel(row, 0f, 0f, cw, CardH, EnhanceFxStyle.WithA(edge, 0.7f), true, "Edge");

            var pick = UiKit.Button(Ctx, row, null, 0f, 0f, cw, 148f, () => OpenEnhance(w.Id), UiButtonStyle.Plain, Focus, null);
            _rowWidgets.Add(pick);
            var cr = pick.ContentRoot;
            var glow = EnhanceGfx.Img(cr, EnhanceGfx.Glow, 64f, 78f, 200f, 200f, EnhanceFxStyle.WithA(el, 0.35f + 0.08f * tier), "Glow");
            EnhanceGfx.Additive(Ctx, glow);
            string auraKey = EnhanceCommonArt.AuraFor(w.Stage);
            var aura = auraKey != null ? EnhanceCommonArt.First(EnhanceCommonArt.Enh(Ctx != null ? Ctx.Art : null, auraKey)) : null;
            if (aura != null) EnhanceGfx.Img(cr, aura, 64f, 78f, 128f, 128f, Color.white, "Aura");
            Sprite icon = null;
            if (Ctx != null && Ctx.Art != null) { try { icon = Ctx.Art.WandIcon(Ids.Char.Of(w.Character), w.Stage); } catch (System.Exception) { icon = null; } }
            UiKit.Image(cr, icon, 16f, 30f, 96f, 96f);
            float tx = 128f, tw = cw - tx - 12f;
            UiKit.Label(Ctx, cr, EnhanceNameKeys.WandName(names, w.Character), UiFontId.F24, tx, 14f, tw, 0f, UiTheme.Chalk, TextAnchor.MiddleLeft, false, true);
            UiKit.Label(Ctx, cr, EnhanceNameKeys.Stage(names, w.Stage), UiFontId.F48, tx, 42f, tw, 60f, stageCol, TextAnchor.MiddleLeft, false, true);
            string info = T(EnhanceNameKeys.Mutations) + " " + F(EnhanceNameKeys.FormatRatio, "a", w.Mutations.Count, "b",
                    cfg != null ? cfg.MutationCapFor(Ids.Char.Of(w.Character)) : 0)
                + T(UiNameKeys.HintSep) + F(EnhanceNameKeys.ExtSlots, "n", w.SlotsExtra, "max", cfg != null ? cfg.SlotExtraMax : 0)
                + T(UiNameKeys.HintSep) + T(EnhanceNameKeys.ProbAura) + " " + F(EnhanceNameKeys.FormatRatio, "a", w.Aura, "b", cfg != null ? cfg.AuraMax : 0);
            UiKit.Label(Ctx, cr, info, UiFontId.F12, tx, 104f, tw, 36f, UiTheme.SlateBlue, TextAnchor.UpperLeft, true);
            if (w.PendingOffer != null)
                UiKit.Label(Ctx, cr, T(EnhanceNameKeys.MutationPending), UiFontId.F12, 12f, 128f, cw - 24f, 0f, UiTheme.Mutation);

            UiKit.Rect(row, 12f, 152f, cw - 24f, 1f, EnhanceFxStyle.WithA(UiTheme.SlateBlue, 0.3f), false, "Sep");
            // [떠나보내기]는 경고 삼각 그림이 붙어 글 칸이 좁다 → 든 지팡이 칸보다 넓게 나눈다(글이 "…"로 잘리지 않게).
            float bw = (cw - 36f) * HeldShare, rw = cw - 36f - bw, by = 162f;
            if (_model.MultiMode)
            {
                bool checkable = _model.Checkable(e, handCount);
                var tg = UiKit.Toggle(Ctx, row, null, 12f, by + 4f, 120f, 44f, _model.Checked.Contains(w.Id), _ => { _model.ToggleCheck(e, handCount); Refresh(); }, Focus);
                if (!checkable && !_model.Checked.Contains(w.Id)) tg.SetDisabled(WandPickerModel.ReleaseReasonKey(WandPickerModel.CanRelease(e, handCount)) ?? EnhanceNameKeys.ErrorNotEligible);
                _rowWidgets.Add(tg);
            }
            else if (e.Equipped)
            {
                EnhanceGfx.Panel(row, 12f, by, bw, 48f, EnhanceFxStyle.WithA(UiTheme.Good, 0.15f), false, "HeldTag");
                UiKit.Label(Ctx, row, T(EnhanceNameKeys.ListHeldTag), UiFontId.F16, 12f, by, bw, 48f, UiTheme.Good, TextAnchor.MiddleCenter);
            }
            else
            {
                var held = UiKit.Button(Ctx, row, T(EnhanceNameKeys.ListSetHeld), 12f, by, bw, 48f, () => Equip(w), UiButtonStyle.Medium, Focus, null, UiFontId.F16);
                _rowWidgets.Add(held);
            }
            if (!_model.MultiMode)
            {
                var rel = UiKit.Button(Ctx, row, T(EnhanceNameKeys.PickerRelease), 24f + bw, by, rw, 48f, () => ReleaseOne(w.Id), UiButtonStyle.Caution, Focus, null, UiFontId.F16);
                var block = WandPickerModel.CanRelease(e, handCount);
                if (block != ReleaseBlock.None) rel.SetDisabled(WandPickerModel.ReleaseReasonKey(block));
                _rowWidgets.Add(rel);
            }
        }

        void OpenEnhance(long wandId) => Screens?.Push(ScreenId.Enhance, EnhanceNav.EnhanceArgs(wandId, Args));

        void Equip(WandData w)
        {
            if (Ctx == null || Ctx.Wands == null) return;
            var r = Ctx.Wands.Equip(w.Character, w.Id);
            if (!r.Ok) Screens?.Toast(T(EnhanceNameKeys.ErrorKey(r.Error)));
            Refresh();
        }

        void ReleaseOne(long id) => OpenRelease(new[] { id });

        void ReleaseChecked()
        {
            if (_model.Checked.Count == 0) return;
            OpenRelease(_model.Checked);
        }

        void OpenRelease(IEnumerable<long> ids)
        {
            var a = EnhanceNav.ReleaseArgs(ids);
            a.OnResult = _ => { _model.Checked.Clear(); _model.MultiMode = false; Refresh(); };
            Screens?.Push(ScreenId.WandReleaseConfirm, a);
        }

        void FollowCursor()
        {
            var cur = Focus.Current as UiWidget;
            if (cur == null || _viewport == null) return;
            for (int i = 0; i < _rowRects.Count; i++)
            {
                if (_rowRects[i] == null || !cur.transform.IsChildOf(_rowRects[i])) continue;
                float top = (i / GridCols) * (CardH + CardGap), bottom = top + CardH, viewH = _viewport.rect.height;
                if (top < _scrollY) _scrollY = top;
                else if (bottom > _scrollY + viewH) _scrollY = bottom - viewH;
                ApplyScroll();
                return;
            }
        }

        void ApplyScroll()
        {
            if (_rowsRoot == null) return;
            var p = _rowsRoot.anchoredPosition;
            _rowsRoot.anchoredPosition = new Vector2(p.x, _scrollY);
        }

        public override bool OnBack() => EnhanceNav.Back(Screens, Args);

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) return EnhanceNav.Back(Screens, Args);
            return base.OnMenuKey(key);
        }
    }
}
