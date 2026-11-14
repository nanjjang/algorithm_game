using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Prep
{
    /// <summary>
    /// 원정 준비(08 요약서 5절, 01 요약서 2.3절). 캐릭터 5명 고르기 → 그 캐릭터의 지팡이 → 출발 조립 확인([조립 바꾸기]) →
    /// 시작 웨이브 → 출발 J. 규칙과 상태는 ExpeditionPrepModel에 있고 이 화면은 그리기와 입력 전달만 한다.
    /// 좌표는 08 5.2절의 화면 좌표(1920×1080)이고 내용 칸(위 띠 80px 아래)에 놓을 때 y에서 80을 뺀다.
    /// </summary>
    public sealed class ExpeditionPrepScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.ExpeditionPrep;
        protected override string TitleKey => PrepKeys.Title;

        const float Top = TopBarHeight;
        // 08 5.2절 배치(화면 좌표)
        const float CardX = 80f, CardY = 104f, CardW = 336f, CardH = 376f, CardGap = 20f;
        const float StatX = 80f, StatY = 504f, StatW = 640f, StatH = 480f, StatRowH = 32f;
        const float WandX = 744f, WandY = 504f, WandW = 480f, WandH = 480f, WandColW = 104f, WandCell = 80f;
        const float LoadX = 1248f, LoadY = 504f, LoadW = 592f, LoadH = 352f;
        const float TrackerH = 24f;
        const float SkipX = 1248f, SkipY = 872f, SkipW = 592f, SkipH = 48f;
        const float DepartX = 1248f, DepartY = 936f, DepartW = 592f, DepartH = 64f;
        const int SelectionPx = 192, EmblemPx = 64, InspectPx = 192;

        ExpeditionPrepModel _model;

        // 캐릭터 카드
        readonly UiButton[] _cards = new UiButton[Ids.Char.Count];
        readonly UImage[] _cardBg = new UImage[Ids.Char.Count];
        readonly UImage[] _cardStrip = new UImage[Ids.Char.Count];
        readonly UiLabel[] _cardTitle = new UiLabel[Ids.Char.Count];

        // 능력치 판
        RectTransform _statRoot;
        readonly List<UiLabel> _statValues = new List<UiLabel>();
        readonly List<UiProgressBar> _statBars = new List<UiProgressBar>();
        readonly List<UImage> _statIcons = new List<UImage>();
        UiLabel _passive, _wandTrait, _uniqueArt;

        // 지팡이 판
        UiLabel _wandHeader;
        UiButton _sortButton;
        UiScrollList _wandList;
        readonly List<UiButton> _wandButtons = new List<UiButton>();
        readonly List<long> _wandButtonIds = new List<long>();
        UImage _inspect;
        UiLabel _wandName, _wandNick, _wandStage, _wandMut, _wandExt, _wandScar, _wandHeldTag;
        UImage _scarIcon;
        RectTransform _mutIcons;
        UiButton _setHeldButton;

        // 출발 조립 판
        UImage _loadPanel;
        RectTransform _slotRoot;
        UiTriangle _coreWarn;
        UiLabel _coreWarnLabel;
        UiButton _changeLoadout;

        // 추적기, 시작 웨이브, 출발
        UiLabel _tracker;
        RectTransform _skipRow;
        UiButton _skipPrev, _skipNext;
        UiLabel _skipValue, _skipNote;
        UiButton _depart;

        bool _awaitLoadoutReturn;

        // ================================================================== 만들기

        protected override void BuildContent(RectTransform content)
        {
            _model = new ExpeditionPrepModel(Ctx);
            BuildCards(content);
            BuildStats(content);
            BuildWandPanel(content);
            BuildLoadout(content);
            BuildStartWave(content);
            _depart = UiKit.Button(Ctx, content, T(PrepKeys.Depart), DepartX, DepartY - Top, DepartW, DepartH,
                OnDepartPressed, UiButtonStyle.Primary, Focus, "");
        }

        void BuildCards(RectTransform content)
        {
            for (int i = 0; i < Ids.Char.Count; i++)
            {
                int number = i + 1;
                string charId = Ids.Char.Of(number);
                float x = CardX + i * (CardW + CardGap);
                var b = UiKit.Button(Ctx, content, null, x, CardY - Top, CardW, CardH, () => OnCharacterPicked(number),
                    UiButtonStyle.Plain, Focus, "");
                b.name = "Card" + number;
                var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                _cardBg[i] = UiKit.Image(root, KitSprite("S-0601", "character_card", "normal"), 0f, 0f, CardW, CardH, null, false);
                var def = Ctx != null && Ctx.Content != null ? Ctx.Content.Character(charId) : null;
                string element = def != null ? def.Element : null;
                // 뒤에 속성 문양(64, 20% 투명) — 08 5.2절
                if (element != null)
                    UiKit.Image(root, UiKit.IconSprite(Ctx, element, EmblemPx), CardW - EmblemPx - 16f, 16f, EmblemPx, EmblemPx,
                        UiTheme.WithAlpha(Color.white, 0.2f));
                var sel = Ctx != null && Ctx.Art != null ? Ctx.Art.CharacterSelection(charId) : null;
                UiKit.Image(root, sel, (CardW - SelectionPx) * 0.5f, 24f, SelectionPx, SelectionPx);
                UiKit.Label(Ctx, root, T(charId), UiFontId.F24, 16f, 232f, CardW - 32f, 0f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                _cardTitle[i] = UiKit.Label(Ctx, root, string.Empty, UiFontId.F16, 16f, 272f, CardW - 32f, 0f, UiTheme.SlateBlue, TextAnchor.MiddleCenter);
                if (element != null)
                    UiKit.Label(Ctx, root, T(element), UiFontId.F16, 16f, 304f, CardW - 32f, 0f, UiTheme.ElementColor(element), TextAnchor.MiddleCenter);
                // 고른 캐릭터 = 확정 체크 + 아랫변 속성색 8px 줄
                _cardStrip[i] = UiKit.Rect(root, 0f, CardH - 8f, CardW, 8f, UiTheme.ElementColor(element));
                _cards[i] = b;
            }
        }

        void BuildStats(RectTransform content)
        {
            UiKit.OrnatePanel(Ctx, content, StatX, StatY - Top, StatW, StatH, 1);
            _statRoot = UiKit.Node(content, "Stats", StatX + 16f, StatY - Top + 16f, StatW - 32f, StatH - 32f);
            float w = StatW - 32f;
            for (int i = 0; i < PrepStats.RowCount; i++)
            {
                float y = i * StatRowH;
                _statIcons.Add(UiKit.Image(_statRoot, null, 0f, y + 4f, 24f, 24f));
                // 이름은 Refresh에서 채운다(열쇠는 모델이 준다).
                _statValues.Add(UiKit.Label(Ctx, _statRoot, string.Empty, UiFontId.F16, 32f, y, w - 32f, StatRowH));
                _statBars.Add(UiKit.ProgressBar(Ctx, _statRoot, w - 200f, y + 12f, 200f, 8f));
            }
            float y0 = PrepStats.RowCount * StatRowH;
            _passive = UiKit.Label(Ctx, _statRoot, string.Empty, UiFontId.F16, 0f, y0, w, StatRowH);
            _wandTrait = UiKit.Label(Ctx, _statRoot, string.Empty, UiFontId.F16, 0f, y0 + StatRowH, w, StatRowH);
            _uniqueArt = UiKit.Label(Ctx, _statRoot, string.Empty, UiFontId.F16, 0f, y0 + 2f * StatRowH, w, StatRowH);
        }

        void BuildWandPanel(RectTransform content)
        {
            float py = WandY - Top;
            UiKit.Plate(Ctx, content, UiPanelKind.Board, WandX, py, WandW, WandH);
            _wandHeader = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, WandX + 16f, py + 12f, 200f, 24f);
            _sortButton = UiKit.Button(Ctx, content, string.Empty, WandX + WandW - 232f, py + 8f, 216f, 32f, OnSortPressed,
                UiButtonStyle.Small, Focus, null, UiFontId.F16);
            // 왼쪽 104px: 보관 지팡이 칸(80×80) 세로 줄, 한 화면 5칸, 세로 스크롤
            _wandList = UiKit.ScrollList(Ctx, content, WandX + 8f, py + 48f, WandColW, 5f * (WandCell + 4f), Focus, 4f);
            float rx = WandX + WandColW + 16f, rw = WandW - WandColW - 32f;
            UiKit.Image(content, KitSprite("S-0602", "wand_pedestal", null), rx + (rw - 224f) * 0.5f, py + 48f + InspectPx - 24f, 224f, 48f);
            _inspect = UiKit.Image(content, null, rx + (rw - InspectPx) * 0.5f, py + 48f, InspectPx, InspectPx);
            float ty = py + 48f + InspectPx + 32f;
            _wandName = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, rx, ty, rw, 0f);
            _wandNick = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx, ty + 32f, rw, 0f, UiTheme.SlateBlue);
            _wandStage = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx, ty + 56f, rw * 0.5f, 0f);
            _wandHeldTag = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx + rw * 0.5f, ty + 56f, rw * 0.5f, 0f, UiTheme.SelectYellow, TextAnchor.MiddleRight);
            _wandMut = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx, ty + 80f, 120f, 0f);
            _mutIcons = UiKit.Node(content, "MutationIcons", rx + 124f, ty + 80f, rw - 124f, 24f);
            _wandExt = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx, ty + 104f, rw * 0.6f, 0f);
            _scarIcon = UiKit.Image(content, UiKit.IconSprite(Ctx, "ui:I-G2/scar", 24), rx + rw * 0.6f, ty + 104f, 24f, 24f);
            _wandScar = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx + rw * 0.6f + 28f, ty + 104f, rw * 0.4f - 28f, 0f);
            _setHeldButton = UiKit.Button(Ctx, content, T(PrepKeys.SetHeld), rx, py + WandH - 48f, rw, 36f, OnSetHeldPressed,
                UiButtonStyle.Small, Focus, "", UiFontId.F16);
        }

        void BuildLoadout(RectTransform content)
        {
            float py = LoadY - Top;
            _loadPanel = UiKit.Plate(Ctx, content, UiPanelKind.Board, LoadX, py, LoadW, LoadH);
            UiKit.Label(Ctx, content, T(PrepKeys.Loadout), UiFontId.F24, LoadX + 16f, py + 12f, LoadW - 32f, 0f);
            _slotRoot = UiKit.Node(content, "Slots", LoadX + 16f, py + 56f, LoadW - 32f, 160f);
            _coreWarn = UiKit.WarningTriangle(content, LoadX + 16f, py + 228f, 24f);
            _coreWarnLabel = UiKit.Label(Ctx, content, T(PrepKeys.WarnCoreEmpty), UiFontId.F16, LoadX + 48f, py + 228f, LoadW - 64f, 24f, UiTheme.Amber);
            _changeLoadout = UiKit.Button(Ctx, content, T(PrepKeys.ChangeLoadout), LoadX + 16f, py + LoadH - 80f, LoadW - 32f, 56f,
                OnChangeLoadoutPressed, UiButtonStyle.Medium, Focus);
            _tracker = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, LoadX, SkipY - Top - TrackerH - 4f, LoadW, TrackerH, UiTheme.SlateBlue);
        }

        void BuildStartWave(RectTransform content)
        {
            _skipRow = UiKit.Node(content, "StartWave", SkipX, SkipY - Top, SkipW, SkipH);
            UiKit.Label(Ctx, _skipRow, T(PrepKeys.StartWaveLabel), UiFontId.F16, 0f, 0f, 176f, SkipH);
            _skipPrev = UiKit.Button(Ctx, _skipRow, null, 184f, 4f, 40f, 40f, () => OnSkipStep(-1), UiButtonStyle.Small, Focus, null);
            _skipValue = UiKit.Label(Ctx, _skipRow, string.Empty, UiFontId.F24, 232f, 0f, 176f, SkipH, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _skipNext = UiKit.Button(Ctx, _skipRow, null, 416f, 4f, 40f, 40f, () => OnSkipStep(1), UiButtonStyle.Small, Focus, null);
            UiKit.Triangle(_skipPrev.Rect, UiTriangleDir.Left, 12f, 12f, 16f, 16f, UiTheme.Chalk);
            UiKit.Triangle(_skipNext.Rect, UiTriangleDir.Right, 12f, 12f, 16f, 16f, UiTheme.Chalk);
            _skipNote = UiKit.Label(Ctx, content, string.Empty, UiFontId.F12, SkipX, SkipY - Top + SkipH, SkipW, 16f, UiTheme.SlateBlue);
        }

        Sprite KitSprite(string id, string variant, string state) => UiKit.KitSprite(Ctx, id, variant, state).Sprite;

        // ================================================================== 수명

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _awaitLoadoutReturn = false;
            _model.Enter();
            RefreshAll();
            Focus.SetCursor(_cards[_model.Character - 1], false);
            FocusLoadoutIfBlocked();
        }

        public override void Tick(float dt)
        {
            // 조립 바꾸기(Camp)에서 돌아오면 목록을 다시 읽는다(덮였다 드러날 때는 OnEnter가 오지 않는다).
            if (_awaitLoadoutReturn && Screens != null && Screens.Top == ScreenId.ExpeditionPrep) OnLoadoutReturned();
        }

        public override bool OnMenuKey(MenuKey key)
        {
            switch (key)
            {
                case MenuKey.PrevTab: OnCharacterPicked(_model.Character > 1 ? _model.Character - 1 : Ids.Char.Count); return true;
                case MenuKey.NextTab: OnCharacterPicked(_model.Character < Ids.Char.Count ? _model.Character + 1 : 1); return true;
                case MenuKey.Cancel: Close(); return true;
                case MenuKey.Confirm:
                    if (Focus.Current == (IUiFocusable)_depart && !_depart.Enabled) { OnDepartBlocked(); return Focus.HandleKey(key); }
                    break;
            }
            return base.OnMenuKey(key);
        }

        // ================================================================== 입력

        void OnCharacterPicked(int number)
        {
            bool changed = number != _model.Character;
            if (!_model.SelectCharacter(number)) return;
            PlayUi(PrepKeys.SndCharacterConfirm);
            RefreshAll();
            if (changed) Focus.SetCursor(_cards[number - 1], false);
            FocusLoadoutIfBlocked();
        }

        void OnWandPicked(long wandId)
        {
            if (!_model.SelectWand(wandId)) return;
            PlayUi(PrepKeys.SndWandSelect);
            RefreshWandDetail();
            RefreshWandRowsState();
            RefreshLoadout();
            RefreshDepart();
            FocusLoadoutIfBlocked();
        }

        void OnSortPressed()
        {
            _model.NextSort();
            RefreshWandList();
        }

        void OnSetHeldPressed()
        {
            var err = _model.SetHeld();
            if (err != ErrorCode.None) { ShowError(PrepErrorKeys.Of(err)); return; }
            PlayUi(PrepKeys.SndHeldWandSet);
            RefreshAll();
        }

        void OnChangeLoadoutPressed()
        {
            var args = _model.ChangeLoadoutArgs();
            if (args == null || Screens == null) return;
            args.OnResult = _ => OnLoadoutReturned();
            _awaitLoadoutReturn = true;
            Screens.Push(ScreenId.Camp, args);
        }

        void OnLoadoutReturned()
        {
            if (!_awaitLoadoutReturn) return;
            _awaitLoadoutReturn = false;
            _model.Reload();
            RefreshAll();
            FocusLoadoutIfBlocked();
        }

        void OnSkipStep(int dir)
        {
            if (_model.StepSkip(dir)) RefreshStartWave();
        }

        void OnDepartPressed()
        {
            var r = _model.Depart(SeedFromClock());
            if (!r.Ok)
            {
                OnDepartBlocked();
                ShowError(r.ErrorKey);
                return;
            }
            PlayUi(PrepKeys.SndDepart);
            Screens?.Replace(r.Next, r.NextArgs);
        }

        void OnDepartBlocked()
        {
            PlayUi(PrepKeys.SndDepartBlocked);
            FocusLoadoutIfBlocked();
        }

        void ShowError(string key)
        {
            if (key != null && Screens != null) Screens.Toast(T(key));
        }

        /// <summary>판 씨앗 = 기기 시계(00 문서: 판 씨앗은 출발 때 정한다).</summary>
        ulong SeedFromClock()
        {
            var now = Ctx != null && Ctx.Clock != null ? Ctx.Clock.UtcNow : DateTime.UtcNow;
            return unchecked((ulong)now.Ticks);
        }

        void FocusLoadoutIfBlocked()
        {
            if (_model.CanDepart().CoreEmpty && _changeLoadout != null) Focus.SetCursor(_changeLoadout, false);
        }

        // ================================================================== 그리기

        void RefreshAll()
        {
            RefreshCards();
            RefreshStats();
            RefreshWandList();
            RefreshLoadout();
            RefreshStartWave();
            RefreshDepart();
        }

        void RefreshCards()
        {
            var p = Ctx != null ? Ctx.Profile : null;
            for (int i = 0; i < _cards.Length; i++)
            {
                int number = i + 1;
                bool picked = number == _model.Character;
                _cards[i].Picked = picked;
                _cardBg[i].sprite = KitSprite("S-0601", "character_card", picked ? "selected" : "normal");
                _cardStrip[i].enabled = picked;
                // 칭호 = 그 캐릭터 든 지팡이의 단계 칭호
                int stage = 0;
                var rec = p != null ? p.Character(number) : null;
                var held = rec != null && p != null ? p.Wand(rec.EquippedWandId) : null;
                if (held != null) stage = held.Stage;
                _cardTitle[i].SetText(T(ExpeditionPrepModel.StageTitleKey(Ctx != null ? Ctx.Content : null, number, stage)));
            }
        }

        void RefreshStats()
        {
            var db = Ctx != null ? Ctx.Content : null;
            var rows = PrepStats.Build(db, _model.CharId);
            for (int i = 0; i < _statValues.Count; i++)
            {
                bool has = i < rows.Count;
                _statValues[i].gameObject.SetActive(has);
                _statBars[i].gameObject.SetActive(has);
                _statIcons[i].gameObject.SetActive(has);
                if (!has) continue;
                var r = rows[i];
                string num = PrepStats.FormatNumber(r.Value, r.Unit);
                if (r.Unit == PrepStatUnit.Seconds) num = F(PrepKeys.FormatSeconds, "n", num);
                _statValues[i].SetText(T(r.LabelKey) + "  " + num);
                _statBars[i].Value01 = r.Bar01;
                var sp = UiKit.IconSprite(Ctx, "ui:I-S1/" + r.IconVariant, 24);
                _statIcons[i].sprite = sp;
                _statIcons[i].color = sp != null ? Color.white : UiTheme.WoodLight;
            }
            var def = db != null ? db.Character(_model.CharId) : null;
            _passive.SetText(def != null ? T(PrepKeys.StatPassive) + "  " + T(def.Passive) : string.Empty);
            _wandTrait.SetText(def != null ? T(PrepKeys.StatWandTrait) + "  " + T(def.WandTrait) : string.Empty);
            _uniqueArt.SetText(def != null ? T(PrepKeys.StatUniqueArt) + "  " + T(def.UniqueArt) : string.Empty);
        }

        void RefreshWandList()
        {
            for (int i = 0; i < _wandButtons.Count; i++)
            {
                Focus.Remove(_wandButtons[i]);
            }
            _wandButtons.Clear();
            _wandButtonIds.Clear();
            _wandList.Clear();
            var rows = _model.Rows;
            _wandHeader.SetText(T(PrepKeys.Wands) + "  " + F(PrepKeys.WandCount, "n", rows.Count));
            _sortButton.SetText(T(PrepKeys.SortKey(_model.Sort)));
            for (int i = 0; i < rows.Count; i++)
            {
                var e = rows[i];
                long id = e.Wand.Id;
                var row = _wandList.AddRow(WandCell, "Wand" + id);
                var b = UiKit.Button(Ctx, row, null, 0f, 0f, WandCell, WandCell, () => OnWandPicked(id), UiButtonStyle.Plain, Focus, "");
                var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                string state = e.Locked ? "expedition_locked" : "filled";
                UiKit.Image(root, KitSprite("K-62", "wand_slot_" + state, null), 0f, 0f, WandCell, WandCell, null, false);
                var icon = Ctx != null && Ctx.Art != null ? Ctx.Art.WandIcon(_model.CharId, e.Wand.Stage) : null;
                UiKit.Image(root, icon, 8f, 8f, 64f, 64f);
                UiKit.Label(Ctx, root, F(PrepKeys.FormatWandStage, "n", e.Wand.Stage), UiFontId.F12, 4f, WandCell - 18f, WandCell - 8f, 16f,
                    UiTheme.Chalk, TextAnchor.MiddleRight, false, true);
                // 든 지팡이 표시(작은 꼬리표 점) · 변이 표식 · 흔적
                if (e.Equipped) UiKit.Rect(root, 4f, 4f, 12f, 12f, UiTheme.SelectYellow);
                if (e.Wand.Mutations != null && e.Wand.Mutations.Count > 0) UiKit.Rect(root, WandCell - 16f, 4f, 12f, 12f, UiTheme.Mutation);
                if (e.Wand.DropCount > 0) UiKit.Image(root, UiKit.IconSprite(Ctx, "ui:I-G2/scar", 16), 4f, WandCell - 20f, 16f, 16f);
                _wandButtons.Add(b);
                _wandButtonIds.Add(id);
            }
            RefreshWandRowsState();
            RefreshWandDetail();
        }

        void RefreshWandRowsState()
        {
            for (int i = 0; i < _wandButtons.Count; i++) _wandButtons[i].Picked = _wandButtonIds[i] == _model.WandId;
        }

        void RefreshWandDetail()
        {
            var e = _model.SelectedEntry;
            bool has = e != null;
            int number = _model.Character;
            var db = Ctx != null ? Ctx.Content : null;
            _inspect.enabled = has;
            ClearChildren(_mutIcons);
            if (!has)
            {
                _wandName.SetText(T(PrepKeys.CommonNone));
                _wandNick.SetText(string.Empty);
                _wandStage.SetText(string.Empty);
                _wandHeldTag.SetText(string.Empty);
                _wandMut.SetText(string.Empty);
                _wandExt.SetText(string.Empty);
                _wandScar.SetText(string.Empty);
                _scarIcon.enabled = false;
                _setHeldButton.SetDisabled(PrepKeys.ErrorNotFound);
                return;
            }
            var w = e.Wand;
            _inspect.sprite = Ctx != null && Ctx.Art != null ? Ctx.Art.WandInspection(_model.CharId, w.Stage) : null;
            _inspect.color = Color.white;
            _inspect.preserveAspect = true;
            _inspect.enabled = _inspect.sprite != null;
            _wandName.SetText(T(Ids.Char.WandKindOf(number)));
            _wandNick.SetText(T(ExpeditionPrepModel.NicknameKey(number, w.Stage)));
            _wandStage.SetText(F(PrepKeys.FormatWandStage, "n", w.Stage) + "  " + T(ExpeditionPrepModel.StageTitleKey(db, number, w.Stage)));
            _wandHeldTag.SetText(e.Equipped ? T(PrepKeys.HeldTag) : string.Empty);
            var def = db != null ? db.Character(_model.CharId) : null;
            int mutMax = def != null ? def.MaxMutations : (db != null && db.Enhance != null ? db.Enhance.MutationCapFor(_model.CharId) : 0);
            int mutCount = w.Mutations != null ? w.Mutations.Count : 0;
            _wandMut.SetText(F(PrepKeys.Mutations, "n", mutCount, "max", mutMax));
            for (int i = 0; i < mutCount; i++)
                UiKit.Image(_mutIcons, UiKit.IconSprite(Ctx, w.Mutations[i].Id, 24), i * 28f, 0f, 24f, 24f);
            int extMax = db != null && db.Enhance != null ? db.Enhance.SlotExtraMax : w.SlotsExtra;
            _wandExt.SetText(F(PrepKeys.EnhanceExtSlots, "n", w.SlotsExtra, "max", extMax));
            _scarIcon.enabled = w.DropCount > 0;
            _wandScar.SetText(w.DropCount > 0 ? F(PrepKeys.Scars, "n", w.DropCount) : string.Empty);
            if (_model.CanSetHeld) _setHeldButton.SetEnabled();
            else _setHeldButton.SetDisabled(e.Equipped ? PrepKeys.HeldTag : PrepKeys.ErrorNotFound);
        }

        void RefreshLoadout()
        {
            ClearChildren(_slotRoot);
            var e = _model.SelectedEntry;
            var check = _model.CanDepart();
            bool tracker = _model.ShowTrackerLine;
            // 추적기 한 줄(592×24)은 시작 웨이브 줄 위에 놓으므로 그만큼 조립 판을 줄인다.
            var prt = _loadPanel.rectTransform;
            prt.sizeDelta = new Vector2(LoadW, tracker ? LoadH - TrackerH - 4f : LoadH);
            _tracker.gameObject.SetActive(tracker);
            if (tracker) _tracker.SetText(F(PrepKeys.RevengeShort, "n", _model.Tracker.Progress));

            _coreWarn.gameObject.SetActive(check.CoreEmpty);
            _coreWarnLabel.gameObject.SetActive(check.CoreEmpty);
            if (e == null) { _changeLoadout.SetDisabled(PrepKeys.ErrorNotFound); return; }
            _changeLoadout.SetEnabled();

            var lo = e.Wand.Loadout ?? new LoadoutData();
            var db = Ctx != null ? Ctx.Content : null;
            var def = db != null ? db.Character(_model.CharId) : null;
            int baseLinks = def != null ? def.BaseLinkSlots : lo.Links.Count;
            // 첫 줄: 코어 72 + 연결 칸 64 × base(4, 제자 3은 5) / 둘째 줄: 확장 칸 64 × slots_extra (03 요약서 3절 SL-3)
            string core = lo.Core;
            UiKit.Image(_slotRoot, KitSprite("K-63", string.IsNullOrEmpty(core) ? "core_slot_empty" : "core_slot_filled", null), 0f, 0f, 72f, 72f, null, false);
            if (!string.IsNullOrEmpty(core)) UiKit.Image(_slotRoot, UiKit.IconSprite(Ctx, core, 48), 12f, 12f, 48f, 48f);
            for (int i = 0; i < baseLinks; i++)
            {
                string id = i < lo.Links.Count ? lo.Links[i] : null;
                float x = 80f + i * 68f;
                UiKit.Image(_slotRoot, KitSprite("K-60", string.IsNullOrEmpty(id) ? "item_slot_empty" : "item_slot_filled", null), x, 4f, 64f, 64f, null, false);
                if (!string.IsNullOrEmpty(id)) UiKit.Image(_slotRoot, UiKit.IconSprite(Ctx, id, 48), x + 8f, 12f, 48f, 48f);
            }
            for (int i = 0; i < e.Wand.SlotsExtra; i++)
            {
                string id = i < lo.Extra.Count ? lo.Extra[i] : null;
                float x = 80f + i * 68f;
                UiKit.Image(_slotRoot, KitSprite("K-64", "extension_slot_open", null), x, 84f, 64f, 64f, null, false);
                if (!string.IsNullOrEmpty(id)) UiKit.Image(_slotRoot, UiKit.IconSprite(Ctx, id, 48), x + 8f, 92f, 48f, 48f);
            }
        }

        void RefreshStartWave()
        {
            bool show = _model.ShowStartWaveRow;
            // 고를 수 없으면 줄 자체가 없다(08 5.5절): 커서도 가지 않게 묶음에서 뺀다.
            _skipRow.gameObject.SetActive(show);
            _skipNote.gameObject.SetActive(show);
            if (!show) return;
            _skipValue.SetText(F(PrepKeys.StartWaveOption, "wave", _model.StartWave));
            if (_model.SkipIndex > 0) _skipPrev.SetEnabled(); else _skipPrev.SetDisabled(PrepKeys.StartWaveLabel);
            if (_model.SkipIndex < _model.SkipOptions.Count - 1) _skipNext.SetEnabled(); else _skipNext.SetDisabled(PrepKeys.StartWaveLabel);
            int skip = _model.Skip;
            _skipNote.SetText(skip > 0
                ? F(PrepKeys.FormatStartWave, "start", _model.StartWave) + "  " + F(PrepKeys.StartWaveScoreNote, "score", N(_model.SkippedScore(skip)))
                : string.Empty);
        }

        void RefreshDepart()
        {
            var check = _model.CanDepart();
            if (check.Ok) _depart.SetEnabled();
            else _depart.SetDisabled(check.ReasonKey);
        }

        static void ClearChildren(RectTransform rt)
        {
            if (rt == null) return;
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var go = rt.GetChild(i).gameObject;
                go.SetActive(false);
                UnityEngine.Object.Destroy(go);
            }
        }
    }
}
