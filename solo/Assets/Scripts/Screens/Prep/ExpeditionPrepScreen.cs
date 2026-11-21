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
    /// 배치: 왼쪽 = 캐릭터(위 작은 초상 줄 5칸 → 고른 캐릭터 크게 + 특성 + 능력치 두 칸), 오른쪽 위 = 기본 공격 3장,
    /// 오른쪽 가운데 = 지팡이 · 출발 조립, 오른쪽 아래 = 시작 웨이브와 크고 밝은 [출발]. 판 테두리는 줄이고 옅은 바탕으로만 묶는다.
    /// 좌표는 화면 좌표(1920×1080)이고 내용 칸(위 띠 80px 아래)에 놓을 때 y에서 80을 뺀다.
    /// </summary>
    public sealed class ExpeditionPrepScreen : ScreenBase
    {
        public override ScreenId Id => ScreenId.ExpeditionPrep;
        protected override string TitleKey => PrepKeys.Title;

        const float Top = TopBarHeight;
        const float Pad = UiLayout.SurfacePad;
        // 왼쪽(격자 0~4열): 초상 줄 → 고른 캐릭터 면(초상 · 이름 · 특성 · 능력치 두 칸)
        static float LeftX => UiLayout.ColX(0);
        static float LeftW => UiLayout.ColW(5);
        const float CardY = 104f, CardH = 136f, CardGap = 16f, TilePx = 96f;
        const float HeroY = 256f, HeroPx = 256f;
        const float TraitRowH = 56f, StatRowH = 40f, StatColGap = 24f;
        const int StatRowsPerCol = 6;
        // 오른쪽(격자 5~11열): 기본 공격 3장 → 지팡이(5~8열) · 출발 조립(9~11열) → 주 버튼
        static float RightX => UiLayout.ColX(5);
        static float RightW => UiLayout.ColW(7);
        const float StyleY = 104f, StyleH = 264f, StyleGap = 24f, StyleIconPx = 40f;
        const float PanelY = 424f;
        static float WandX => UiLayout.ColX(5);
        static float WandW => UiLayout.ColW(4);
        const float WandColW = 104f, WandCell = 80f;
        static float LoadX => UiLayout.ColX(9);
        static float LoadW => UiLayout.ColW(3);
        const float LoadH = 360f, SlotCore = 64f, SlotPx = 56f, SlotStep = 60f;
        const float TrackerH = 24f, SkipH = 48f;
        const int EmblemPx = 48, InspectPx = 160;

        ExpeditionPrepModel _model;

        // 캐릭터 카드(작은 초상 칸)
        readonly UiButton[] _cards = new UiButton[Ids.Char.Count];
        readonly UImage[] _cardBg = new UImage[Ids.Char.Count];
        readonly UImage[] _cardStrip = new UImage[Ids.Char.Count];
        readonly UiLabel[] _cardTitle = new UiLabel[Ids.Char.Count];

        // 고른 캐릭터(크게)
        UImage _heroArt, _heroEmblem, _heroRule;
        UiLabel _heroName, _heroTitle, _heroElement;

        // 기본 공격 카드 3장
        readonly List<UiButton> _styleCards = new List<UiButton>();
        readonly List<UImage> _styleBg = new List<UImage>();
        readonly List<UImage> _styleIcon = new List<UImage>();
        readonly List<UImage> _styleStrip = new List<UImage>();
        readonly List<UImage> _styleShade = new List<UImage>();
        readonly List<UiLabel> _styleName = new List<UiLabel>();
        readonly List<UiLabel> _styleDesc = new List<UiLabel>();
        readonly List<string> _styleIds = new List<string>();
        readonly List<StylePreview> _stylePreview = new List<StylePreview>();
        const float StylePreviewW = 150f, StylePreviewH = 100f;

        // 능력치(두 칸 목록: 아이콘 · 이름 · 값)
        RectTransform _statRoot, _characterDetails;
        UiButton _details;
        readonly List<UiLabel> _statValues = new List<UiLabel>();
        readonly List<UiLabel> _statNumbers = new List<UiLabel>();
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
        RectTransform _loadPanel;
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

        static readonly Color CardIdle = UiTheme.WithAlpha(UiTheme.WoodDark, UiLayout.SurfaceAlpha);
        static readonly Color SoftPanel = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0.38f);

        // ================================================================== 만들기

        protected override void BuildContent(RectTransform content)
        {
            _model = new ExpeditionPrepModel(Ctx);
            BuildCards(content);
            _characterDetails = UiKit.Node(content, "CharacterDetails", 0f, 0f, 0f, 0f);
            BuildHero(content);
            BuildStyleCards(content);
            BuildStats();
            _characterDetails.SetAsLastSibling();
            BuildWandPanel(content);
            BuildLoadout(content);
            BuildStartWave(content);
            BuildDepart(content);
            _details = UiLayout.SecondaryButton(Ctx, content, T("str.ui.details"), LeftX + LeftW - 208f,
                HeroY - Top + HeroPx - 68f, 184f, () => ShowDetails(!_characterDetails.gameObject.activeSelf), Focus, 48f);
            ShowDetails(false);
        }

        static readonly Color CardPicked = UiTheme.WithAlpha(UiTheme.WoodLight, UiLayout.SurfaceAlpha);

        void BuildCards(RectTransform content)
        {
            float cw = (LeftW - (Ids.Char.Count - 1) * CardGap) / Ids.Char.Count;
            float h16 = UiTheme.LineHeight(UiFontId.F16);
            for (int i = 0; i < Ids.Char.Count; i++)
            {
                int number = i + 1;
                string charId = Ids.Char.Of(number);
                float x = LeftX + i * (cw + CardGap);
                var b = UiKit.Button(Ctx, content, null, x, CardY - Top, cw, CardH, () => OnCharacterPicked(number),
                    UiButtonStyle.Plain, Focus, "");
                b.name = "Card" + number;
                var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                _cardBg[i] = UiKit.Rect(root, 0f, 0f, cw, CardH, CardIdle);
                var def = Ctx != null && Ctx.Content != null ? Ctx.Content.Character(charId) : null;
                string element = def != null ? def.Element : null;
                var sel = Ctx != null && Ctx.Art != null ? Ctx.Art.CharacterSelection(charId) : null;
                UiKit.Image(root, sel, (cw - TilePx) * 0.5f, 4f, TilePx, TilePx);
                UiKit.Label(Ctx, root, T(charId), UiFontId.F16, 4f, TilePx + 8f, cw - 8f, h16, UiTheme.Chalk, TextAnchor.MiddleCenter);
                // 칭호는 큰 초상 쪽에 보인다. 칸에는 두지 않는다(글 칸만 남겨 Refresh가 그대로 쓴다).
                _cardTitle[i] = UiKit.Label(Ctx, root, string.Empty, UiFontId.F16, 0f, 0f, 1f, 1f, UiTheme.SlateBlue);
                _cardTitle[i].gameObject.SetActive(false);
                // 고른 캐릭터 = 아랫변 속성색 4px 줄
                _cardStrip[i] = UiKit.Rect(root, 0f, CardH - 4f, cw, 4f, UiTheme.ElementColor(element));
                _cards[i] = b;
            }
        }

        /// <summary>고른 캐릭터 면: 초상(256) + 이름 F48 · 칭호 F24 · 속성 F16, 아래 특성 세 줄(이름표 F24 + 설명 F24).</summary>
        void BuildHero(RectTransform content)
        {
            float y = HeroY - Top;
            UiLayout.Surface(Ctx, content, LeftX, y, LeftW, UiLayout.PrimaryTop + UiLayout.PrimaryMargin + UiLayout.PrimaryH - HeroY, 1);
            float ix = LeftX + Pad, iw = LeftW - 2f * Pad;
            _heroArt = UiKit.Image(content, null, ix - 8f, y + Pad - 8f, HeroPx, HeroPx);
            float tx = ix + HeroPx + Pad, tw = ix + iw - tx;
            float h48 = UiTheme.LineHeight(UiFontId.F48), h24 = UiTheme.LineHeight(UiFontId.F24), h16 = UiTheme.LineHeight(UiFontId.F16);
            float ty = y + Pad + UiLayout.V(3);
            _heroName = UiKit.Label(Ctx, content, string.Empty, UiFontId.F48, tx, ty, tw, h48, UiTheme.Chalk);
            UiKit.DropShadow(_heroName, 3f);
            ty += h48;
            _heroTitle = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, tx, ty, tw, h24, UiTheme.SlateBlue);
            ty += h24 + UiLayout.V(2);
            _heroEmblem = UiKit.Image(content, null, tx, ty, EmblemPx, EmblemPx, UiTheme.WithAlpha(Color.white, 0.9f));
            _heroElement = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, tx + EmblemPx + UiLayout.V(1), ty, tw - EmblemPx - 8f, EmblemPx, UiTheme.Chalk);
            ty += EmblemPx + UiLayout.V(2);
            _heroRule = UiKit.Rect(content, tx, ty, 96f, 4f, UiTheme.Chalk);

            // 특성 세 줄: 왼쪽 회청 이름표, 오른쪽 Chalk 설명(두 줄까지).
            float ry = y + Pad + HeroPx + UiLayout.V(1);
            const float LabelW = 152f;
            string[] keys = { PrepKeys.StatPassive, PrepKeys.StatWandTrait, PrepKeys.StatUniqueArt };
            var vals = new UiLabel[3];
            for (int i = 0; i < 3; i++)
            {
                float rowY = ry + i * TraitRowH;
                if (i > 0) UiKit.Rect(_characterDetails, ix, rowY - 4f, iw, 1f, UiTheme.WithAlpha(UiTheme.SlateBlue, 0.18f), false, "TraitLine");
                UiKit.Label(Ctx, _characterDetails, T(keys[i]), UiFontId.F24, ix, rowY, LabelW, h24, UiTheme.Paper);
                vals[i] = UiKit.Label(Ctx, _characterDetails, string.Empty, UiFontId.F24, ix + LabelW, rowY, iw - LabelW, TraitRowH - 8f,
                    i == 2 ? UiTheme.Gold : UiTheme.Chalk, TextAnchor.UpperLeft, true);
            }
            _passive = vals[0];
            _wandTrait = vals[1];
            _uniqueArt = vals[2];
        }

        /// <summary>기본 공격 카드 3장(미리보기 · 아이콘 · 이름 · 설명). 고른 캐릭터가 바뀌면 RefreshStyles가 글과 그림을 갈아 끼운다.</summary>
        void BuildStyleCards(RectTransform content)
        {
            float sy = UiLayout.Section(Ctx, content, RightX, StyleY - Top, RightW, T(PrepKeys.AttackStyle));
            const int count = 3;
            float sw = (RightW - (count - 1) * StyleGap) / count;
            float h24 = UiTheme.LineHeight(UiFontId.F24);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                float x = RightX + i * (sw + StyleGap);
                var b = UiKit.Button(Ctx, content, null, x, sy, sw, StyleH, () => OnStylePicked(index), UiButtonStyle.Plain, Focus, "");
                b.name = "AttackStyle" + (i + 1);
                var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                _styleBg.Add(UiKit.Rect(root, 0f, 0f, sw, StyleH, CardIdle));
                // 미리보기(위): 그 유형이 실제로 나가는 모양을 반복해서 보여 준다(DataSolo/StylePreview.json, 150×100 좌표).
                UiKit.Rect(root, 0f, 0f, sw, StylePreviewH + 8f, UiTheme.WithAlpha(UiTheme.Ink, 0.6f));
                _stylePreview.Add(new StylePreview(Ctx, root, (sw - StylePreviewW) * 0.5f, 4f, StylePreviewW, StylePreviewH));
                float ty = StylePreviewH + 8f + UiLayout.V(1);
                // 아이콘 = 그 유형의 실제 탄·효과 그림 한 장(ArtLibrary.Icon → effects/styles/reuse.json style_icons)
                _styleIcon.Add(UiKit.Image(root, null, Pad - 8f, ty + 8f, StyleIconPx, StyleIconPx));
                float tx = Pad - 8f + StyleIconPx + 12f, tw = sw - tx - 16f;
                _styleName.Add(UiKit.Label(Ctx, root, string.Empty, UiFontId.F24, tx, ty, tw, h24, UiTheme.Chalk));
                float dy = ty + h24 + 8f;
                _styleDesc.Add(UiKit.Label(Ctx, root, string.Empty, UiFontId.F24, 16f, dy, sw - 32f, StyleH - dy - 12f,
                    UiTheme.Chalk, TextAnchor.UpperLeft, true));
                // 잠김(해금 손잡이를 켰을 때)은 어두운 덮개, 고름은 아랫변 속성색 4px 줄.
                _styleShade.Add(UiKit.Rect(root, 0f, 0f, sw, StyleH, UiTheme.WithAlpha(UiTheme.Ink, 0.55f)));
                _styleStrip.Add(UiKit.Rect(root, 0f, StyleH - 4f, sw, 4f, Color.white));
                _styleCards.Add(b);
                _styleIds.Add(null);
            }
        }

        /// <summary>능력치: 구역 제목 + 두 칸(열마다 6줄), 아이콘 · 이름표 · 오른쪽 맞춤 값.</summary>
        void BuildStats()
        {
            float ix = LeftX + Pad, iw = LeftW - 2f * Pad;
            float sy = HeroY - Top + Pad + HeroPx + UiLayout.V(1) + 3f * TraitRowH;
            sy = UiLayout.Section(Ctx, _characterDetails, ix, sy, iw, T(PrepKeys.StatsTitle), UiFontId.F24);
            _statRoot = UiKit.Node(_characterDetails, "Stats", ix, sy, iw, StatRowsPerCol * StatRowH);
            float colW = (iw - StatColGap) * 0.5f;
            for (int i = 0; i < PrepStats.RowCount; i++)
            {
                int col = i / StatRowsPerCol, row = i % StatRowsPerCol;
                float x = col * (colW + StatColGap), y = row * StatRowH;
                if (row > 0) UiKit.Rect(_statRoot, x, y, colW, 1f, UiTheme.WithAlpha(UiTheme.SlateBlue, 0.18f), false, "StatLine");
                _statIcons.Add(UiKit.Image(_statRoot, null, x, y + (StatRowH - 24f) * 0.5f, 24f, 24f));
                // 이름·값은 Refresh에서 채운다(열쇠는 모델이 준다).
                _statValues.Add(UiKit.Label(Ctx, _statRoot, string.Empty, UiFontId.F24, x + 32f, y, colW - 32f - 72f, StatRowH, UiTheme.Paper));
                _statNumbers.Add(UiKit.Label(Ctx, _statRoot, string.Empty, UiFontId.F32, x + colW - 64f, y, 64f, StatRowH, UiTheme.Chalk, TextAnchor.MiddleRight));
                var bar = UiKit.ProgressBar(Ctx, _statRoot, x, y, 1f, 1f);
                bar.gameObject.SetActive(false);
                _statBars.Add(bar);
            }
        }

        /// <summary>구역 제목(UiLayout.Section과 같은 모양)인데 글을 나중에 바꿀 수 있게 글 칸을 돌려준다.</summary>
        UiLabel SectionLabel(RectTransform parent, float x, float y, float w)
        {
            UiLayout.Section(Ctx, parent, x, y, w, string.Empty);
            return UiKit.Label(Ctx, parent, string.Empty, UiFontId.F16, x, y + 4f, w, UiTheme.LineHeight(UiFontId.F16), UiTheme.Brass);
        }

        void BuildWandPanel(RectTransform content)
        {
            float py = PanelY - Top;
            float wh = UiLayout.PrimaryTop - PanelY + UiLayout.PrimaryMargin + UiLayout.PrimaryH;
            UiLayout.Surface(Ctx, content, WandX, py, WandW, wh, 1);
            float ix = WandX + Pad, iw = WandW - 2f * Pad;
            _wandHeader = SectionLabel(content, ix, py + Pad, iw);
            _sortButton = UiKit.Button(Ctx, content, string.Empty, ix + iw - 176f, py + Pad, 176f, 32f, OnSortPressed,
                UiButtonStyle.Small, Focus, null, UiFontId.F16);
            float ly = py + Pad + UiLayout.SectionH + UiLayout.V(1);
            float setY = py + wh - Pad - 48f;
            // 왼쪽 104px: 보관 지팡이 칸(80×80) 세로 줄, 세로 스크롤
            _wandList = UiKit.ScrollList(Ctx, content, ix, ly, WandColW, setY - UiLayout.V(2) - ly, Focus, 4f);
            float rx = ix + WandColW + Pad, rw = ix + iw - rx;
            float h24 = UiTheme.LineHeight(UiFontId.F24), h16 = UiTheme.LineHeight(UiFontId.F16);
            UiKit.Image(content, KitSprite("S-0602", "wand_pedestal", null), rx + (rw - 192f) * 0.5f, ly + InspectPx - 20f, 192f, 40f);
            _inspect = UiKit.Image(content, null, rx + (rw - InspectPx) * 0.5f, ly, InspectPx, InspectPx);
            float ty = ly + InspectPx + UiLayout.V(3);
            _wandName = UiKit.Label(Ctx, content, string.Empty, UiFontId.F24, rx, ty, rw, h24);
            ty += h24;
            _wandNick = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx, ty, rw, h16, UiTheme.SlateBlue);
            ty += h16 + UiLayout.V(1);
            _wandStage = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx, ty, rw * 0.65f, h16);
            _wandHeldTag = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx + rw * 0.65f, ty, rw * 0.35f, h16, UiTheme.SelectYellow, TextAnchor.MiddleRight);
            ty += h16;
            _wandMut = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx, ty, 120f, h16);
            _mutIcons = UiKit.Node(content, "MutationIcons", rx + 124f, ty, rw - 124f, 24f);
            ty += h16;
            _wandExt = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx, ty, rw * 0.6f, h16);
            _scarIcon = UiKit.Image(content, UiKit.IconSprite(Ctx, "ui:I-G2/scar", 24), rx + rw * 0.6f, ty, 24f, 24f);
            _wandScar = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, rx + rw * 0.6f + 28f, ty, rw * 0.4f - 28f, h16);
            _setHeldButton = UiLayout.SecondaryButton(Ctx, content, T(PrepKeys.SetHeld), ix, setY, iw, OnSetHeldPressed, Focus, 48f);
            _setHeldButton.PressSound = null;
        }

        void BuildLoadout(RectTransform content)
        {
            float py = PanelY - Top;
            _loadPanel = UiLayout.Surface(Ctx, content, LoadX, py, LoadW, LoadH, 1);
            float ix = LoadX + Pad, iw = LoadW - 2f * Pad;
            float y = UiLayout.Section(Ctx, content, ix, py + Pad, iw, T(PrepKeys.Loadout));
            _slotRoot = UiKit.Node(content, "Slots", ix, y, iw, 2f * SlotStep + 24f);
            y += SlotCore + SlotStep + UiLayout.V(4);
            _coreWarn = UiKit.WarningTriangle(content, ix, y, 24f);
            _coreWarnLabel = UiKit.Label(Ctx, content, T(PrepKeys.WarnCoreEmpty), UiFontId.F16, ix + 32f, y, iw - 32f, 24f, UiTheme.Amber);
            _changeLoadout = UiLayout.SecondaryButton(Ctx, content, T(PrepKeys.ChangeLoadout), ix, py + LoadH - Pad - UiLayout.SecondaryH, iw,
                OnChangeLoadoutPressed, Focus);
            _tracker = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, LoadX, py + LoadH + UiLayout.V(1), LoadW, TrackerH, UiTheme.SlateBlue);
        }

        void BuildStartWave(RectTransform content)
        {
            float y = PanelY - Top + LoadH + UiLayout.V(1) + TrackerH + UiLayout.V(1);
            _skipRow = UiKit.Node(content, "StartWave", LoadX, y, LoadW, SkipH);
            float w = LoadW;
            UiKit.Label(Ctx, _skipRow, T(PrepKeys.StartWaveLabel), UiFontId.F16, 0f, 0f, w - 248f, SkipH, UiTheme.SlateBlue);
            _skipPrev = UiKit.Button(Ctx, _skipRow, null, w - 240f, 4f, 40f, 40f, () => OnSkipStep(-1), UiButtonStyle.Small, Focus, null);
            _skipValue = UiKit.Label(Ctx, _skipRow, string.Empty, UiFontId.F24, w - 192f, 0f, 144f, SkipH, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _skipNext = UiKit.Button(Ctx, _skipRow, null, w - 40f, 4f, 40f, 40f, () => OnSkipStep(1), UiButtonStyle.Small, Focus, null);
            UiKit.Triangle(_skipPrev.Rect, UiTriangleDir.Left, 12f, 12f, 16f, 16f, UiTheme.Chalk);
            UiKit.Triangle(_skipNext.Rect, UiTriangleDir.Right, 12f, 12f, 16f, 16f, UiTheme.Chalk);
            _skipNote = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, LoadX, y + SkipH, LoadW, TrackerH, UiTheme.SlateBlue);
        }

        /// <summary>[출발]: 공용 주 버튼(오른쪽 아래 같은 자리, 초록 바탕 · 꺼지면 어둡게).</summary>
        void BuildDepart(RectTransform content)
        {
            _depart = UiLayout.PrimaryButton(Ctx, content, T(PrepKeys.Depart), OnDepartPressed, Focus);
            _depart.PressSound = null;
        }

        Sprite KitSprite(string id, string variant, string state) => UiKit.KitSprite(Ctx, id, variant, state).Sprite;

        // ================================================================== 수명

        void ShowDetails(bool show)
        {
            _characterDetails.gameObject.SetActive(show);
            _details.SetText(T(show ? "str.ui.hide_details" : "str.ui.details"));
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _awaitLoadoutReturn = false;
            ShowDetails(false);
            _model.Enter();
            RefreshAll();
            Focus.SetCursor(_cards[_model.Character - 1], false);
            FocusLoadoutIfBlocked();
        }

        public override void Tick(float dt)
        {
            // 조립 바꾸기(Camp)에서 돌아오면 목록을 다시 읽는다(덮였다 드러날 때는 OnEnter가 오지 않는다).
            if (_awaitLoadoutReturn && Screens != null && Screens.Top == ScreenId.ExpeditionPrep) OnLoadoutReturned();
            for (int i = 0; i < _stylePreview.Count; i++) _stylePreview[i].Tick(dt);
        }

        /// <summary>테스트·점검: i번째 기본 공격 카드의 미리보기(없으면 null).</summary>
        public StylePreview StylePreviewAt(int i) => i >= 0 && i < _stylePreview.Count ? _stylePreview[i] : null;

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

        void OnStylePicked(int index)
        {
            if (index < 0 || index >= _styleIds.Count || _styleIds[index] == null) return;
            if (!_model.SelectStyle(_styleIds[index]))
            {
                if (_model.SelectedStyle != _styleIds[index]) OnDepartBlocked();
                return;
            }
            PlayUi(PrepKeys.SndWandSelect);
            RefreshStyles();
            RefreshLoadout();
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
            RefreshStyles();
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
                _cardBg[i].color = picked ? CardPicked : CardIdle;
                _cardStrip[i].enabled = picked;
                // 칭호 = 그 캐릭터 든 지팡이의 단계 칭호
                int stage = 0;
                var rec = p != null ? p.Character(number) : null;
                var held = rec != null && p != null ? p.Wand(rec.EquippedWandId) : null;
                if (held != null) stage = held.Stage;
                _cardTitle[i].SetText(T(ExpeditionPrepModel.StageTitleKey(Ctx != null ? Ctx.Content : null, number, stage)));
                if (picked) RefreshHero(number, _cardTitle[i].FullText);
            }
        }

        /// <summary>고른 캐릭터 크게: 초상·이름·칭호·속성(속성색 줄).</summary>
        void RefreshHero(int number, string title)
        {
            string charId = Ids.Char.Of(number);
            var def = Ctx != null && Ctx.Content != null ? Ctx.Content.Character(charId) : null;
            string element = def != null ? def.Element : null;
            Color ec = UiTheme.ElementColor(element);
            if (_heroArt != null)
            {
                _heroArt.sprite = Ctx != null && Ctx.Art != null ? Ctx.Art.CharacterSelection(charId) : null;
                _heroArt.preserveAspect = true;
                _heroArt.color = Color.white;
                _heroArt.enabled = _heroArt.sprite != null;
            }
            if (_heroEmblem != null)
            {
                _heroEmblem.sprite = element != null ? UiKit.IconSprite(Ctx, element, EmblemPx) : null;
                _heroEmblem.preserveAspect = true;
                _heroEmblem.enabled = _heroEmblem.sprite != null;
            }
            _heroName.SetText(T(charId));
            _heroTitle.SetText(title ?? string.Empty);
            _heroElement.SetText(element != null ? T(element) : string.Empty);
            _heroElement.Color = ec;
            _heroRule.color = ec;
        }

        void RefreshStyles()
        {
            var cards = _model.StyleCards();
            for (int i = 0; i < _styleCards.Count; i++)
            {
                bool has = i < cards.Count;
                _styleCards[i].gameObject.SetActive(has);
                _styleIds[i] = has ? cards[i].Id : null;
                if (!has) continue;
                var c = cards[i];
                _styleCards[i].Picked = c.Selected;
                _styleBg[i].color = c.Selected ? CardPicked : CardIdle;
                var icon = UiKit.IconSprite(Ctx, c.Id, (int)StyleIconPx);
                _styleIcon[i].sprite = icon;
                _styleIcon[i].preserveAspect = true;
                if (i < _stylePreview.Count) _stylePreview[i].Show(c.Unlocked ? c.Id : null);
                _styleIcon[i].color = icon != null ? Color.white : UiTheme.ElementColor(c.Element);
                _styleName[i].SetText(T(c.Id));
                _styleDesc[i].SetText(c.Unlocked ? T(c.DescKey) : F(PrepKeys.AttackStyleLocked, "n", c.UnlockWave));
                _styleShade[i].enabled = !c.Unlocked;
                _styleStrip[i].enabled = c.Selected;
                _styleStrip[i].color = UiTheme.ElementColor(c.Element);
                if (c.Unlocked) _styleCards[i].SetEnabled(); else _styleCards[i].SetDisabled(PrepKeys.AttackStyle);
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
                _statNumbers[i].gameObject.SetActive(has);
                _statBars[i].gameObject.SetActive(false);
                _statIcons[i].gameObject.SetActive(has);
                if (!has) continue;
                var r = rows[i];
                string num = PrepStats.FormatNumber(r.Value, r.Unit);
                if (r.Unit == PrepStatUnit.Seconds) num = F(PrepKeys.FormatSeconds, "n", num);
                _statValues[i].SetText(T(r.LabelKey));
                _statNumbers[i].SetText(num);
                _statBars[i].Value01 = r.Bar01;
                var sp = UiKit.IconSprite(Ctx, "ui:I-S1/" + r.IconVariant, 24);
                _statIcons[i].sprite = sp;
                _statIcons[i].color = sp != null ? Color.white : UiTheme.WoodLight;
            }
            var def = db != null ? db.Character(_model.CharId) : null;
            _passive.SetText(def != null ? T(def.Passive) : string.Empty);
            _wandTrait.SetText(def != null ? T(def.WandTrait) : string.Empty);
            _uniqueArt.SetText(def != null ? T(def.UniqueArt) : string.Empty);
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
            // 코어는 판 시작 규칙대로 보인다: 마력탄 자리면 고른 기본 공격의 핵(Docs/12-attack-styles.md 1절).
            string core = _model.EffectiveCore(lo.Core);
            const float Icon = 40f, cIcon = 44f;
            float lx0 = SlotCore + 8f, io = (SlotPx - Icon) * 0.5f;
            UiKit.Image(_slotRoot, KitSprite("K-63", string.IsNullOrEmpty(core) ? "core_slot_empty" : "core_slot_filled", null), 0f, 0f, SlotCore, SlotCore, null, false);
            if (!string.IsNullOrEmpty(core)) UiKit.Image(_slotRoot, UiKit.IconSprite(Ctx, core, 48), (SlotCore - cIcon) * 0.5f, (SlotCore - cIcon) * 0.5f, cIcon, cIcon);
            if (!string.IsNullOrEmpty(core) && core != lo.Core)
                UiKit.Label(Ctx, _slotRoot, T(core) + T(PrepKeys.StyleTagSep) + T(PrepKeys.AttackStyle), UiFontId.F16, 0f, SlotCore + SlotPx + 8f,
                    LoadW - 2f * Pad, UiTheme.LineHeight(UiFontId.F16), UiTheme.SlateBlue);
            for (int i = 0; i < baseLinks; i++)
            {
                string id = i < lo.Links.Count ? lo.Links[i] : null;
                float x = lx0 + i * SlotStep;
                UiKit.Image(_slotRoot, KitSprite("K-60", string.IsNullOrEmpty(id) ? "item_slot_empty" : "item_slot_filled", null), x, 4f, SlotPx, SlotPx, null, false);
                if (!string.IsNullOrEmpty(id)) UiKit.Image(_slotRoot, UiKit.IconSprite(Ctx, id, 48), x + io, 4f + io, Icon, Icon);
            }
            for (int i = 0; i < e.Wand.SlotsExtra; i++)
            {
                string id = i < lo.Extra.Count ? lo.Extra[i] : null;
                float x = lx0 + i * SlotStep;
                UiKit.Image(_slotRoot, KitSprite("K-64", "extension_slot_open", null), x, SlotCore + 4f, SlotPx, SlotPx, null, false);
                if (!string.IsNullOrEmpty(id)) UiKit.Image(_slotRoot, UiKit.IconSprite(Ctx, id, 48), x + io, SlotCore + 4f + io, Icon, Icon);
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
