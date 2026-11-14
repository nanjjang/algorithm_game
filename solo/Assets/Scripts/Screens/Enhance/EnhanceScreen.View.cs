using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Enhance
{
    // 화면 조립(08 요약서 10.3절 배치표: 위 띠 (0,0) 1920×80 / 누계 띠 (480,88) 960×40 / 왼쪽 (0,80) 480×840 /
    // 가운데 (480,80) 960×840 / 오른쪽 (1440,80) 480×840 / 아래 (0,920) 1920×160. 무대 배치의 주인은 effects.md 1.2절).
    // 가운데 무대가 주인공: 여러 겹 마법진 + 큰 지팡이 + 빛 번짐(가산 합성 흉내). 왼쪽·오른쪽은 둥근 카드, 재화·비용은 아이콘 칩.
    // 겹 순서(뒤 → 앞): 바탕 · 바탕 어둠 · 위 띠 · 왼쪽 · 오른쪽 · 아래 · 집중 어둠 · 무대 · 결과 어둠 · 효과 층 · 카드 · 칭호 띠 · 번쩍 · 테두리.
    // 커서 순서(위젯을 만든 순서)는 예전과 같게 두고, 겹 순서만 SetSiblingIndex로 맞춘다.
    public sealed partial class EnhanceScreen
    {
        const float TopH = 80f, SideW = 480f, MidW = 960f, PanelTop = 80f, PanelH = 840f, BottomY = 920f, BottomH = 160f;
        const float WandPx = 384f, Cell = 64f, SlotPx = 56f;
        /// <summary>무대 안 마법진 가운데(무대 왼쪽 위 기준).</summary>
        const float RitualCx = 480f, RitualCy = 392f;
        const float CostX = 1176f, ButtonY = 76f, ButtonW = 320f, ButtonH = 64f;
        const float MeterX = 48f, MeterY = 214f, MeterW = 384f, MeterH = 22f;
        // 아래 비용 칩: 강화석 칩은 "상급 100"(F24)이 아이콘 옆에 잘리지 않고 들어가는 폭. 토글 줄은 그만큼 오른쪽으로.
        const float CostGoldX = CostX + 56f, CostGoldW = 140f, CostStoneX = CostX + 204f, CostStoneW = 156f, CostChipY = 10f, CostChipH = 40f;
        const float ToggleX = CostStoneX + CostStoneW + 16f, ToggleW = 1904f - ToggleX;
        /// <summary>충전 빛(충전 고리)과 결과 충격 빛(국소 섬광)의 오브젝트 이름 — 켜져 있는 동안만 활성(시험이 이름으로 찾는다).</summary>
        public const string ChargeLightName = "ChargeLight", ImpactLightName = "ImpactLight";

        /// <summary>화면 좌표(1920×1080)의 마법진 가운데.</summary>
        static Vector2 RitualScreen => new Vector2(SideW + RitualCx, PanelTop + RitualCy);

        // 바탕·덮개
        UImage _background, _dim, _darken, _flash, _border, _focusDim, _vignette;
        // 위 띠
        UiLabel _wandName, _stageTitle, _gold;
        readonly UiLabel[] _stoneLabels = new UiLabel[3];
        RectTransform _goldChip;
        UiButton _prevWand, _nextWand;
        UiLabel _tally;
        // 왼쪽
        RectTransform _left;
        UiLabel _slotsLabel, _extLabel, _mutLabel, _bestLabel, _floorLabel, _traceNote, _circuit;
        readonly List<UiButton> _mutationCells = new List<UiButton>();
        UiToggle _traceToggle;
        UImage _portrait, _portraitGlow;
        UiButton _engraveButton, _removeButton;
        // 가운데(무대)
        RectTransform _stage, _stageFx, _wandRoot, _ritual;
        UImage _sealBase, _sealElement, _outerGlyph, _wandImage, _master, _candle, _sigil;
        UImage _stageGlow, _beam, _beamCore, _rays, _raysBack, _chargeRing, _chargeRingBack, _wandGlow;
        readonly List<UImage> _sealLayers = new List<UImage>();
        readonly List<UImage> _spokes = new List<UImage>();
        RectTransform _spokeRoot;
        UImage _aura, _resultBanner;
        UiLabel _nickname, _stageDigits;
        UImage _digitsGlow;
        RectTransform _markers;
        readonly List<UiButton> _sacrificeSlots = new List<UiButton>();
        UiButton _charmSlot;
        UiLabel _charmNote;
        readonly List<UImage> _auraCells = new List<UImage>();
        // 효과 층(화면 좌표)
        RectTransform _fxLayer;
        EnhanceParticles _particles;
        UImage _coreFlash, _crack, _crackShadeImg;
        Material _additive;
        // 오른쪽
        UiLabel _bigProb, _capTag, _lineBase, _lineSac, _lineSame, _lineAura, _hint, _probTitle;
        UImage _meterFill, _meterGlow, _hintBox;
        readonly UImage[] _band = new UImage[4];
        UiLabel _expectTarget, _expectTries, _expectGold, _expectStone, _expectOwned;
        readonly UiLabel[] _expectStoneCells = new UiLabel[3];
        UiButton _targetPrev, _targetNext;
        // 아래
        RectTransform _candRow, _costGoldChip, _costStoneChip;
        readonly List<UiButton> _candidateCells = new List<UiButton>();
        UiButton _candPrev, _candNext;
        UiLabel _cost, _costGold, _costStone, _dropTag, _candTitle;
        UImage _costStoneIcon, _enhanceGlow, _enhanceHalo;
        RectTransform _costStoneIconRoot;
        UiButton _enhanceButton;
        UiToggle _fastToggle, _continueToggle, _stopOnDropToggle;
        // 카드
        RectTransform _resultCard, _resultLines, _mutationCard, _titleBandRoot;
        UImage _resultCardBg, _resultCardEdge, _resultCardGlow, _titleRibbon;
        UiButton _retryButton, _traceCardButton, _closeCardButton;
        UiLabel _mutName, _mutGain, _mutCost, _mutPush;
        UImage _mutIcon;
        UiButton _acceptButton, _rejectButton;
        UiLabel _titleBand;
        UiFocusGroup _cardFocus;

        protected override void BuildContent(RectTransform content)
        {
            InitState();
            float W = UiScale.RefWidth, H = UiScale.RefHeight;
            if (Ctx != null && Ctx.Art != null)
            {
                try { _additive = Ctx.Art.Material(SpriteMaterialKind.SpriteAdditive); } catch (System.Exception) { _additive = null; }
            }
            Sprite bg = null;
            if (Ctx != null && Ctx.Art != null) { try { bg = Ctx.Art.Background("enhance.workbench"); } catch (System.Exception) { bg = null; } }
            _background = UiKit.Image(content, bg, 0f, 0f, W, H, bg != null ? Color.white : UiTheme.WoodDark, false);
            _background.raycastTarget = true;
            _dim = UiKit.Rect(content, 0f, 0f, W, H, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0f), false, "Dim");
            _vignette = EnhanceGfx.Img(content, EnhanceGfx.Vignette, W * 0.5f, H * 0.5f, W * 1.15f, H * 1.6f, EnhanceFxStyle.WithA(UiTheme.Ink, 0.55f), "Vignette");

            BuildTop(content, W);
            BuildLeft(content);
            BuildStage(content);
            BuildRight(content);
            BuildRecordPanel(content);   // 기록 칸·경고 줄(EnhanceScreen.Records.cs)
            BuildBottom(content, W);
            BuildCards(content);

            _focusDim = UiKit.Rect(content, 0f, 0f, W, H, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0f), false, "FocusDim");
            _darken = UiKit.Rect(content, 0f, 0f, W, H, new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, 0f), false, "Darken");
            BuildFxLayer(content, W, H);
            _flash = UiKit.Rect(content, 0f, 0f, W, H, new Color(1f, 1f, 1f, 0f), false, "Flash");
            _border = UiKit.Rect(content, 0f, 0f, W, H, new Color(0f, 0f, 0f, 0f), false, "Border");
            var outline = _border.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color(UiTheme.Chalk.r, UiTheme.Chalk.g, UiTheme.Chalk.b, 0f);
            outline.effectDistance = new Vector2(_t.BorderHighlightPx, _t.BorderHighlightPx);
            _border.gameObject.SetActive(false);

            // 겹 순서: 집중 어둠은 무대 바로 밑(양옆 판만 어두워지고 마법진은 밝게 남는다), 결과 어둠·효과 층은 카드 밑.
            _focusDim.transform.SetSiblingIndex(_stage.GetSiblingIndex());
            _stage.SetAsLastSibling();
            _darken.transform.SetAsLastSibling();
            _fxLayer.SetAsLastSibling();
            _resultCard.SetAsLastSibling();
            _mutationCard.SetAsLastSibling();
            _titleBandRoot.SetAsLastSibling();
            _flash.transform.SetAsLastSibling();
            _border.transform.SetAsLastSibling();
            Focus.CursorChanged = _ => OnCursorChanged();
        }

        // ------------------------------------------------------------------ 판 도우미

        /// <summary>둥근 카드: 짙은 유리 바탕 + 위쪽 윤기 + 강조색 테.</summary>
        static void Card(RectTransform parent, float x, float y, float w, float h, Color accent, float fillAlpha = 0.86f)
        {
            EnhanceGfx.Panel(parent, x + 4f, y + 6f, w, h, EnhanceFxStyle.WithA(Color.black, 0.35f), false, "CardShadow");
            EnhanceGfx.Panel(parent, x, y, w, h, EnhanceFxStyle.WithA(UiTheme.Ink, fillAlpha), false, "CardFill");
            var sheen = UiKit.Image(parent, EnhanceGfx.Sheen, x + 6f, y + 4f, w - 12f, Mathf.Min(120f, h * 0.3f), EnhanceFxStyle.WithA(Color.white, 0.06f), false);
            sheen.name = "CardSheen";
            EnhanceGfx.Panel(parent, x, y, w, h, EnhanceFxStyle.WithA(accent, 0.55f), true, "CardEdge");
        }

        /// <summary>아이콘 + 숫자 칩. 돌려주는 것은 칩 뿌리(흔들림에 쓴다).</summary>
        RectTransform Chip(RectTransform parent, float x, float y, float w, float h, string iconId, Color accent, out UiLabel label, out UImage icon)
        {
            var root = UiKit.Node(parent, "Chip", x, y, w, h);
            EnhanceGfx.Panel(root, 0f, 0f, w, h, EnhanceFxStyle.WithA(UiTheme.Ink, 0.75f), false, "ChipFill");
            EnhanceGfx.Panel(root, 0f, 0f, w, h, EnhanceFxStyle.WithA(accent, 0.6f), true, "ChipEdge");
            float isz = h - 12f;
            icon = iconId != null ? UiKit.Icon(Ctx, root, iconId, 32, 8f, 6f) : null;
            if (icon != null) UiKit.Place(icon.rectTransform, 8f, 6f, isz, isz);
            label = UiKit.Label(Ctx, root, string.Empty, UiFontId.F24, 12f + isz, 0f, w - isz - 20f, h, UiTheme.Chalk, TextAnchor.MiddleRight, false, true);
            return root;
        }

        void BuildTop(RectTransform content, float W)
        {
            var top = UiKit.Node(content, "Top", 0f, 0f, W, TopH);
            UiKit.Rect(top, 0f, 0f, W, TopH, EnhanceFxStyle.WithA(UiTheme.Ink, 0.88f), false, "TopFill");
            UiKit.Image(top, EnhanceGfx.Sheen, 0f, 0f, W, TopH, EnhanceFxStyle.WithA(Color.white, 0.05f), false);
            UiKit.Rect(top, 0f, TopH - 2f, W, 2f, EnhanceFxStyle.WithA(UiTheme.Gold, 0.45f), false, "TopLine");
            _prevWand = UiKit.Button(Ctx, top, null, 24f, 16f, 48f, 48f, () => SwitchWand(-1), UiButtonStyle.Small, null, string.Empty);
            UiKit.Triangle(_prevWand.ContentRoot, UiTriangleDir.Left, 12f, 12f, 24f, 24f, UiTheme.Chalk);
            _wandName = UiKit.Label(Ctx, top, string.Empty, UiFontId.F32, 84f, 20f, 360f, 0f, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            _nextWand = UiKit.Button(Ctx, top, null, 456f, 16f, 48f, 48f, () => SwitchWand(1), UiButtonStyle.Small, null, string.Empty);
            UiKit.Triangle(_nextWand.ContentRoot, UiTriangleDir.Right, 12f, 12f, 24f, 24f, UiTheme.Chalk);
            _stageTitle = UiKit.Label(Ctx, top, string.Empty, UiFontId.F24, 528f, 24f, 540f, 0f, UiTheme.Chalk, TextAnchor.MiddleLeft, false, true);
            _goldChip = Chip(top, 1084f, 16f, 236f, 48f, Ids.Item.Gold, UiTheme.Gold, out _gold, out _);
            string[] stones = { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh };
            for (int i = 0; i < 3; i++)
                Chip(top, 1336f + i * 192f, 16f, 180f, 48f, stones[i], UiTheme.SlateBlue, out _stoneLabels[i], out _);
            _tally = UiKit.Label(Ctx, content, string.Empty, UiFontId.F16, 480f, 88f, 960f, 40f, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
        }

        void BuildLeft(RectTransform content)
        {
            _left = UiKit.Node(content, "Left", 0f, PanelTop, SideW, PanelH);
            Card(_left, 24f, 24f, 432f, 792f, UiTheme.SlateBlue);
            float x = 48f, w = 384f;
            _slotsLabel = UiKit.Label(Ctx, _left, string.Empty, UiFontId.F24, x, 44f, w, 0f, UiTheme.Chalk, TextAnchor.MiddleLeft, false, true);
            _circuit = UiKit.Label(Ctx, _left, string.Empty, UiFontId.F12, x, 80f, w, 48f, UiTheme.SlateBlue, TextAnchor.UpperLeft, true);
            UiKit.Rect(_left, x, 128f, w, 1f, EnhanceFxStyle.WithA(UiTheme.SlateBlue, 0.3f), false, "Sep");
            _extLabel = UiKit.Label(Ctx, _left, string.Empty, UiFontId.F16, x, 140f, w, 0f);
            _mutLabel = UiKit.Label(Ctx, _left, string.Empty, UiFontId.F16, x, 172f, w, 0f);
            for (int i = 0; i < MaxMutationCells; i++)
            {
                int idx = i;
                var b = UiKit.Button(Ctx, _left, null, x + i * 56f, 200f, 48f, 48f, () => SelectMutation(idx), UiButtonStyle.Plain, Focus, null);
                _mutationCells.Add(b);
            }
            UiKit.Rect(_left, x, 260f, w, 1f, EnhanceFxStyle.WithA(UiTheme.SlateBlue, 0.3f), false, "Sep");
            _bestLabel = UiKit.Label(Ctx, _left, string.Empty, UiFontId.F16, x, 268f, w, 0f, UiTheme.Gold);
            _floorLabel = UiKit.Label(Ctx, _left, string.Empty, UiFontId.F16, x, 294f, w, 0f, UiTheme.SlateBlue);
            _traceToggle = UiKit.Toggle(Ctx, _left, T(EnhanceNameKeys.TracePublic), x, 324f, w, 40f, false, SetTracePublic, Focus);
            _traceNote = UiKit.Label(Ctx, _left, T(EnhanceNameKeys.TraceHiddenNote), UiFontId.F12, x, 368f, w, 32f, UiTheme.SlateBlue, TextAnchor.UpperLeft, true);
            _engraveButton = UiKit.Button(Ctx, _left, T(EnhanceNameKeys.Engrave), x, 412f, 184f, 56f, OpenEngrave, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            _removeButton = UiKit.Button(Ctx, _left, T(EnhanceNameKeys.RemoveMutation), x + 200f, 412f, 184f, 56f, OpenRemoveMutation, UiButtonStyle.Medium, Focus, null, UiFontId.F16);
            // 캐릭터 초상(64×64를 3배) — 왼쪽 칸 아래(10 요약서 4.2절). 뒤에 속성색 은은한 빛.
            _portraitGlow = EnhanceGfx.Img(_left, EnhanceGfx.Glow, 240f, 668f, 320f, 320f, EnhanceFxStyle.WithA(ElementColor, 0.25f), "PortraitGlow");
            EnhanceGfx.Additive(Ctx, _portraitGlow);
            _portrait = UiKit.Image(_left, null, 144f, 572f, 192f, 192f, UiTheme.WoodLight);
        }

        void BuildStage(RectTransform content)
        {
            _stage = UiKit.Node(content, "Stage", SideW, PanelTop, MidW, PanelH);
            var sr = _stage;
            float cx = RitualCx, cy = RitualCy;
            var candle = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupEnhance, "candle");
            _candle = candle != null ? UiKit.Image(sr, candle, 40f, 40f, 80f, 46f) : null;
            var master = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupEnhance, "master_sleep");
            _master = master != null ? UiKit.Image(sr, master, MidW - 160f, 40f, 128f, 128f) : null;

            // 마법진 묶음(가운데 피벗 — 확대·떨림은 이 묶음만. 화면 전체는 떨지 않는다, 08 10.8절).
            _ritual = EnhanceGfx.Centered(UiKit.Node(sr, "Ritual"), cx, cy, 760f, 760f);
            float rc = 380f;   // 묶음 안 가운데
            _stageGlow = EnhanceGfx.Img(_ritual, EnhanceGfx.Glow, rc, rc, 820f, 820f, EnhanceFxStyle.WithA(ElementColor, 0.22f), "StageGlow");
            EnhanceGfx.Additive(Ctx, _stageGlow);
            _raysBack = EnhanceGfx.Img(_ritual, EnhanceGfx.Rays24, rc, rc, 900f, 900f, new Color(1f, 1f, 1f, 0f), "RaysBack");
            EnhanceGfx.Additive(Ctx, _raysBack);
            _beam = EnhanceGfx.Img(_ritual, EnhanceGfx.Beam, rc, rc - 120f, 240f, 1000f, new Color(1f, 1f, 1f, 0f), "Beam");
            EnhanceGfx.Additive(Ctx, _beam);
            _outerGlyph = EnhanceGfx.Img(_ritual, null, rc, rc, 640f, 640f, new Color(1f, 1f, 1f, 0f), "OuterGlyph");
            var layers = EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupEnhance, "seal_layers");
            for (int i = 0; i < EnhanceFxStyle.LayerDiameter.Length; i++)
            {
                Sprite sp = layers != null && layers.Frames.Length > 0 ? layers.Frames[Mathf.Min(i, layers.Frames.Length - 1)] : EnhanceGfx.RingThin;
                float d = EnhanceFxStyle.LayerDiameter[i];
                var img = EnhanceGfx.Img(_ritual, sp, rc, rc, d, d, new Color(1f, 1f, 1f, 0f), "SealLayer" + i);
                EnhanceGfx.Additive(Ctx, img);
                _sealLayers.Add(img);
            }
            _sigil = EnhanceGfx.Img(_ritual, null, rc, rc, 540f, 540f, new Color(1f, 1f, 1f, 0f), "Sigil");
            _sealBase = EnhanceGfx.Img(_ritual, null, rc, rc, 480f, 480f, new Color(1f, 1f, 1f, 0f), "SealBase");
            _sealElement = EnhanceGfx.Img(_ritual, null, rc, rc, 480f, 480f, new Color(1f, 1f, 1f, 0f), "SealElement");
            _chargeRingBack = EnhanceGfx.Img(_ritual, EnhanceGfx.Ring, rc, rc, 600f, 600f, new Color(1f, 1f, 1f, 0f), "ChargeRingBack");
            _chargeRing = EnhanceGfx.Img(_ritual, EnhanceGfx.Ring, rc, rc, 600f, 600f, new Color(1f, 1f, 1f, 0f), ChargeLightName);
            _chargeRing.type = UImage.Type.Filled;
            _chargeRing.fillMethod = UImage.FillMethod.Radial360;
            _chargeRing.fillOrigin = (int)UImage.Origin360.Top;
            _chargeRing.fillClockwise = true;
            _chargeRing.fillAmount = 0f;
            EnhanceGfx.Additive(Ctx, _chargeRing);
            _chargeRing.gameObject.SetActive(false);
            _rays = EnhanceGfx.Img(_ritual, EnhanceGfx.Rays12, rc, rc, 700f, 700f, new Color(1f, 1f, 1f, 0f), "Rays");
            EnhanceGfx.Additive(Ctx, _rays);
            // 빛살(공통 그림 light_ray, 아래 가운데 피벗): 가운데에서 방사형으로 돌려 꽂는다. 성공 때만 켜진다.
            _spokeRoot = EnhanceGfx.Centered(UiKit.Node(_ritual, "Spokes"), rc, rc, 0f, 0f);
            var ray = EnhanceCommonArt.First(EnhanceCommonArt.Enh(Ctx != null ? Ctx.Art : null, EnhanceCommonArt.LightRay));
            if (ray != null)
                for (int i = 0; i < EnhanceFxStyle.SpokeCount; i++)
                {
                    var sp = EnhanceGfx.Img(_spokeRoot, ray, 0f, 0f, 64f, 512f, new Color(1f, 1f, 1f, 0f), "Spoke" + i);
                    sp.rectTransform.pivot = new Vector2(0.5f, 0f);
                    sp.rectTransform.anchoredPosition = Vector2.zero;
                    sp.rectTransform.localEulerAngles = new Vector3(0f, 0f, i * 360f / EnhanceFxStyle.SpokeCount);
                    EnhanceGfx.Additive(Ctx, sp);
                    _spokes.Add(sp);
                }
            _wandGlow = EnhanceGfx.Img(_ritual, EnhanceGfx.Glow, rc, rc, 420f, 420f, EnhanceFxStyle.WithA(ElementColor, 0.35f), "WandGlow");
            EnhanceGfx.Additive(Ctx, _wandGlow);
            // 단계 오라(+7 파랑 / +10 보라 / +13 무지개 금, 공통 그림 128×128을 4배).
            _aura = EnhanceGfx.Img(_ritual, null, rc, rc, 512f, 512f, new Color(1f, 1f, 1f, 0f), "Aura");
            _beamCore = EnhanceGfx.Img(_ritual, EnhanceGfx.Beam, rc, rc - 120f, 90f, 1000f, new Color(1f, 1f, 1f, 0f), "BeamCore");
            EnhanceGfx.Additive(Ctx, _beamCore);
            _wandRoot = UiKit.Node(_ritual, "Wand", rc - WandPx * 0.5f, rc - WandPx * 0.5f, WandPx, WandPx);
            _wandImage = UiKit.Image(_wandRoot, null, 0f, 0f, WandPx, WandPx, UiTheme.WoodLight);
            _markers = UiKit.Node(_wandRoot, "Markers", 0f, 0f, WandPx, WandPx);

            // 단계 숫자: 마법진 위, 크게(F64 × 1.6). 뒤에 빛.
            _digitsGlow = EnhanceGfx.Img(sr, EnhanceGfx.Glow, cx, 56f, 360f, 200f, new Color(1f, 1f, 1f, 0.12f), "DigitsGlow");
            EnhanceGfx.Additive(Ctx, _digitsGlow);
            _stageDigits = UiKit.Label(Ctx, sr, string.Empty, UiFontId.F64, 0f, 0f, 400f, 96f, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            EnhanceGfx.Centered(_stageDigits.Rect, cx, 56f, 400f, 96f);
            var dOut = _stageDigits.gameObject.GetComponent<Outline>();
            if (dOut != null) dOut.effectDistance = new Vector2(3f, -3f);
            _stageDigits.Rect.localScale = Vector3.one * DigitsBaseScale;
            _nickname = UiKit.Label(Ctx, sr, string.Empty, UiFontId.F24, 0f, 112f, MidW, 0f, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);

            // 제물 칸 5개 + 부적 칸. 비었을 때도 은은한 테와 빛 점.
            float sy = 716f, total = MaxSacrifice * SlotPx + (MaxSacrifice - 1) * 16f + 16f + 48f;
            float sx = cx - total * 0.5f;
            EnhanceGfx.Panel(sr, sx - 20f, sy - 36f, total + 40f, SlotPx + 64f, EnhanceFxStyle.WithA(UiTheme.Ink, 0.7f), false, "OfferTray");
            Tint(EnhanceGfx.Panel(sr, sx - 20f, sy - 36f, total + 40f, SlotPx + 64f, EnhanceFxStyle.WithA(ElementColor, 0.35f), true, "OfferTrayEdge"), 0.35f);
            UiKit.LabelKey(Ctx, sr, EnhanceNameKeys.Sacrifice, UiFontId.F16, sx, sy - 30f, 200f, 0f);
            for (int i = 0; i < MaxSacrifice; i++)
            {
                int idx = i;
                var b = UiKit.Button(Ctx, sr, null, sx + i * (SlotPx + 16f), sy, SlotPx, SlotPx, () => RemoveSacrificeAt(idx), UiButtonStyle.Plain, Focus, string.Empty);
                EnhanceGfx.Panel(b.ContentRoot, 0f, 0f, SlotPx, SlotPx, EnhanceFxStyle.WithA(UiTheme.WoodDark, 0.9f), false, "SlotFill");
                var slotSprite = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "offering_slot", 0);
                if (slotSprite != null) UiKit.Image(b.ContentRoot, slotSprite, 0f, 0f, SlotPx, SlotPx);
                Tint(EnhanceGfx.Panel(b.ContentRoot, 0f, 0f, SlotPx, SlotPx, EnhanceFxStyle.WithA(ElementColor, 0.45f), true, "SlotEdge"), 0.45f);
                EnhanceGfx.Img(b.ContentRoot, EnhanceGfx.Sparkle, SlotPx * 0.5f, SlotPx * 0.5f, 20f, 20f, EnhanceFxStyle.WithA(ElementColor, 0.35f), "EmptyMark");
                _sacrificeSlots.Add(b);
            }
            float chx = sx + MaxSacrifice * (SlotPx + 16f) + 16f;
            UiKit.LabelKey(Ctx, sr, EnhanceNameKeys.Charm, UiFontId.F16, chx, sy - 30f, 120f, 0f);
            _charmSlot = UiKit.Button(Ctx, sr, null, chx, sy - 12f, 48f, 80f, ToggleCharm, UiButtonStyle.Plain, Focus, string.Empty);
            EnhanceGfx.Panel(_charmSlot.ContentRoot, 0f, 0f, 48f, 80f, EnhanceFxStyle.WithA(UiTheme.WoodDark, 0.9f), false, "CharmFill");
            var ward = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "ward_slot", 0);
            if (ward != null) UiKit.Image(_charmSlot.ContentRoot, ward, 0f, 0f, 48f, 80f);
            EnhanceGfx.Panel(_charmSlot.ContentRoot, 0f, 0f, 48f, 80f, EnhanceFxStyle.WithA(EnhanceFxStyle.WardTeal, 0.45f), true, "CharmEdge");
            // 부적 칸 아래 한 줄: 제물 쟁반 안쪽(오른쪽 끝 맞춤, 쟁반 아래 테 위)에 둔다 — 쟁반 밖으로 삐져나가지 않게.
            _charmNote = UiKit.Label(Ctx, sr, string.Empty, UiFontId.F12, chx + 52f - 200f, sy + 66f, 200f, 16f, UiTheme.SlateBlue, TextAnchor.MiddleRight);
            // 기운 칸 0..3
            for (int i = 0; i < AuraMax; i++)
            {
                var slot = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "artisan_slot", 0);
                var img = UiKit.Image(sr, slot, cx - AuraMax * 20f + i * 40f, sy + 92f, 28f, 28f, slot != null ? Color.white : UiTheme.Wood);
                _auraCells.Add(img);
            }
            _stageFx = UiKit.Node(sr, "Fx", 0f, 0f, MidW, PanelH);
            _fx = new EnhanceFxPlayer(_stageFx);
            // 제물 칸은 끌어다 놓기도 받는다.
            for (int i = 0; i < _sacrificeSlots.Count; i++)
                UiKit.MakeDropTarget(_sacrificeSlots[i].gameObject, p => p is int && Model.AcceptsSetup, p => PutCandidate((int)p));
        }

        void BuildFxLayer(RectTransform content, float W, float H)
        {
            _fxLayer = UiKit.Node(content, "FxLayer", 0f, 0f, W, H);
            var c = RitualScreen;
            _crackShadeImg = UiKit.Rect(_fxLayer, 0f, 0f, W, H, new Color(0f, 0f, 0f, 0f), false, "CrackShade");
            var crack = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "screen_crack", 0);
            _crack = EnhanceGfx.Img(_fxLayer, crack, c.x, c.y, W * 1.05f, H * 1.05f, new Color(1f, 1f, 1f, 0f), "Crack");
            var pr = UiKit.Node(_fxLayer, "Particles", 0f, 0f, W, H);
            _particles = new EnhanceParticles(pr, _additive, 360);
            _coreFlash = EnhanceGfx.Img(_fxLayer, EnhanceGfx.Glow, c.x, c.y, 900f, 900f, new Color(1f, 1f, 1f, 0f), ImpactLightName);
            EnhanceGfx.Additive(Ctx, _coreFlash);
            _coreFlash.gameObject.SetActive(false);
        }

        void BuildRight(RectTransform content)
        {
            var right = UiKit.Node(content, "Right", SideW + MidW, PanelTop, SideW, PanelH);
            Card(right, 24f, 24f, 432f, 792f, UiTheme.Gold, 0.88f);
            float x = 48f, w = 384f;
            _probTitle = UiKit.LabelKey(Ctx, right, "str.enhance.prob.total", UiFontId.F16, x, 40f, 260f, 0f, UiTheme.SlateBlue);
            _bigProb = UiKit.Label(Ctx, right, string.Empty, UiFontId.F64, x, 64f, 240f, 96f, UiTheme.Chalk, TextAnchor.MiddleLeft, false, true);
            _bigProb.Rect.localScale = new Vector3(1.5f, 1.5f, 1f);
            var pOut = _bigProb.gameObject.GetComponent<Outline>();
            if (pOut != null) pOut.effectDistance = new Vector2(2f, -2f);
            _capTag = UiKit.LabelKey(Ctx, right, EnhanceNameKeys.ProbCap, UiFontId.F16, x + 300f, 40f, 84f, 32f, UiTheme.Amber, TextAnchor.MiddleCenter);
            // 계기판: 둥근 홈 + 색 단계 채움 + 10% 눈금 + 끝 빛.
            EnhanceGfx.Panel(right, MeterX - 2f, MeterY - 2f, MeterW + 4f, MeterH + 4f, EnhanceFxStyle.WithA(Color.black, 0.6f), false, "MeterRail");
            _meterFill = EnhanceGfx.Panel(right, MeterX, MeterY, MeterW, MeterH, UiTheme.Good, false, "MeterFill");
            UiKit.Image(right, EnhanceGfx.Sheen, MeterX, MeterY, MeterW, MeterH * 0.5f, EnhanceFxStyle.WithA(Color.white, 0.18f), false);
            for (int i = 1; i < 10; i++) UiKit.Rect(right, MeterX + MeterW * i / 10f, MeterY + 3f, 1f, MeterH - 6f, EnhanceFxStyle.WithA(UiTheme.Ink, 0.55f), false, "Tick");
            _meterGlow = EnhanceGfx.Img(right, EnhanceGfx.SoftDot, MeterX, MeterY + MeterH * 0.5f, 64f, 64f, Color.white, "MeterGlow");
            EnhanceGfx.Additive(Ctx, _meterGlow);
            _lineBase = UiKit.Label(Ctx, right, string.Empty, UiFontId.F16, x, 252f, w, 0f);
            _lineSac = UiKit.Label(Ctx, right, string.Empty, UiFontId.F16, x, 278f, w, 0f);
            _lineSame = UiKit.Label(Ctx, right, string.Empty, UiFontId.F16, x, 304f, w, 0f);
            _lineAura = UiKit.Label(Ctx, right, string.Empty, UiFontId.F16, x, 330f, w, 0f);
            EnhanceGfx.Panel(right, 38f, 362f, 404f, 28f, EnhanceFxStyle.WithA(Color.black, 0.5f), false, "BandRail");
            var bandRoot = UiKit.Node(right, "Band", 40f, 364f, 400f, 24f);
            Color[] colors = { UiTheme.Gold, ElementColor, UiTheme.Mutation, UiTheme.CommonGray };
            for (int i = 0; i < 4; i++)
            {
                int k = i;
                _band[i] = UiKit.Rect(bandRoot, 0f, 0f, 0f, 24f, colors[i], true, "Band" + i);
                UiKit.AttachTooltip(_band[i].gameObject, () => BandTooltip(k));
            }
            _hintBox = EnhanceGfx.Panel(right, 40f, 404f, 400f, 64f, EnhanceFxStyle.WithA(UiTheme.SlateBlue, 0.12f), false, "HintBox");
            _hint = UiKit.Label(Ctx, right, string.Empty, UiFontId.F16, x + 8f, 412f, w - 16f, 48f, UiTheme.Chalk, TextAnchor.MiddleLeft, true);

            // 기댓값 표(432×160)
            var table = UiKit.Node(right, "Expected", 24f, 616f, 432f, 176f);
            EnhanceGfx.Panel(table, 12f, 0f, 408f, 168f, EnhanceFxStyle.WithA(Color.black, 0.35f), false, "TableFill");
            UiKit.Rect(table, 24f, 44f, 384f, 1f, EnhanceFxStyle.WithA(UiTheme.SlateBlue, 0.3f), false, "Sep");
            _targetPrev = UiKit.Button(Ctx, table, null, 20f, 6f, 32f, 32f, () => MoveTarget(-1), UiButtonStyle.Small, Focus, string.Empty);
            UiKit.Triangle(_targetPrev.ContentRoot, UiTriangleDir.Left, 8f, 8f, 16f, 16f, UiTheme.Chalk);
            _expectTarget = UiKit.Label(Ctx, table, string.Empty, UiFontId.F16, 60f, 10f, 312f, 0f, UiTheme.Gold, TextAnchor.MiddleCenter);
            _targetNext = UiKit.Button(Ctx, table, null, 380f, 6f, 32f, 32f, () => MoveTarget(1), UiButtonStyle.Small, Focus, string.Empty);
            UiKit.Triangle(_targetNext.ContentRoot, UiTriangleDir.Right, 8f, 8f, 16f, 16f, UiTheme.Chalk);
            // 평균 시도·골드는 한 줄에 둘, 평균 강화석은 제목 한 줄 + 짧은 이름(하급·중급·상급) 세 칸(긴 이름 한 줄은 잘렸다).
            _expectTries = UiKit.Label(Ctx, table, string.Empty, UiFontId.F16, 28f, 54f, 172f, 0f);
            _expectGold = UiKit.Label(Ctx, table, string.Empty, UiFontId.F16, 208f, 54f, 200f, 0f);
            _expectStone = UiKit.Label(Ctx, table, string.Empty, UiFontId.F16, 28f, 80f, 380f, 0f);
            for (int i = 0; i < _expectStoneCells.Length; i++)
                _expectStoneCells[i] = UiKit.Label(Ctx, table, string.Empty, UiFontId.F16, 28f + i * 128f, 106f, 124f, 0f);
            _expectOwned = UiKit.Label(Ctx, table, string.Empty, UiFontId.F16, 28f, 132f, 380f, 0f, UiTheme.SlateBlue);
        }

        void BuildBottom(RectTransform content, float W)
        {
            var bottom = UiKit.Node(content, "Bottom", 0f, BottomY, W, BottomH);
            UiKit.Rect(bottom, 0f, 0f, W, BottomH, EnhanceFxStyle.WithA(UiTheme.Ink, 0.9f), false, "BottomFill");
            UiKit.Rect(bottom, 0f, 0f, W, 2f, EnhanceFxStyle.WithA(UiTheme.Gold, 0.35f), false, "BottomLine");
            _candTitle = UiKit.LabelKey(Ctx, bottom, EnhanceNameKeys.Candidates, UiFontId.F16, 72f, 12f, 400f, 0f);
            _candPrev = UiKit.Button(Ctx, bottom, null, 24f, 56f, 32f, 64f, () => PageCandidates(-1), UiButtonStyle.Small, Focus, string.Empty);
            UiKit.Triangle(_candPrev.ContentRoot, UiTriangleDir.Left, 8f, 24f, 16f, 16f, UiTheme.Chalk);
            _candRow = UiKit.Node(bottom, "Candidates", 72f, 44f, CandidatesPerRow * (Cell + 8f), Cell + 24f);
            for (int i = 0; i < CandidatesPerRow; i++)
            {
                int idx = i;
                var b = UiKit.Button(Ctx, _candRow, null, i * (Cell + 8f), 0f, Cell, Cell, () => PutCandidate(idx), UiButtonStyle.Plain, Focus, string.Empty);
                UiKit.AttachTooltip(b.gameObject, () => CandidateTooltip(idx));
                UiKit.MakeDraggable(b.gameObject, idx, null, () => CandidateAt(idx) != null && Model.AcceptsSetup);
                _candidateCells.Add(b);
            }
            _candNext = UiKit.Button(Ctx, bottom, null, 72f + CandidatesPerRow * (Cell + 8f) + 8f, 56f, 32f, 64f, () => PageCandidates(1), UiButtonStyle.Small, Focus, string.Empty);
            UiKit.Triangle(_candNext.ContentRoot, UiTriangleDir.Right, 8f, 24f, 16f, 16f, UiTheme.Chalk);

            // 비용: "비용" + [골드 칩] [강화석 칩].
            _cost = UiKit.LabelKey(Ctx, bottom, EnhanceNameKeys.Cost, UiFontId.F16, CostX, 14f, 60f, 32f, UiTheme.SlateBlue);
            _costGoldChip = Chip(bottom, CostGoldX, CostChipY, CostGoldW, CostChipH, Ids.Item.Gold, UiTheme.Gold, out _costGold, out _);
            _costStoneChip = Chip(bottom, CostStoneX, CostChipY, CostStoneW, CostChipH, null, UiTheme.SlateBlue, out _costStone, out _);
            _costStoneIconRoot = UiKit.Node(_costStoneChip, "StoneIcon", 6f, 4f, 32f, 32f);
            _dropTag = UiKit.LabelKey(Ctx, bottom, EnhanceNameKeys.DropPossible, UiFontId.F12, CostX, 56f, ButtonW, 16f, UiTheme.Amber, TextAnchor.MiddleCenter);
            // 큰 주 버튼: 뒤에서 맥동하는 빛(누를 수 있을 때만). 속성색(금색 금지, 08 10.3절).
            _enhanceHalo = EnhanceGfx.Img(bottom, EnhanceGfx.Glow, CostX + ButtonW * 0.5f, ButtonY + ButtonH * 0.5f, ButtonW + 240f, ButtonH + 150f, EnhanceFxStyle.WithA(ElementColor, 0.3f), "EnhanceHalo");
            EnhanceGfx.Additive(Ctx, _enhanceHalo);
            _enhanceButton = UiKit.Button(Ctx, bottom, T(EnhanceNameKeys.Enhance), CostX, ButtonY, ButtonW, ButtonH, OnEnhancePressed, UiButtonStyle.Primary, Focus, string.Empty, UiFontId.F32);
            _enhanceButton.Tint = ElementColor;
            _enhanceGlow = EnhanceGfx.Panel(_enhanceButton.ContentRoot, -3f, -3f, ButtonW + 6f, ButtonH + 6f, EnhanceFxStyle.WithA(Color.white, 0.5f), true, "EnhanceEdge");
            _fastToggle = UiKit.Toggle(Ctx, bottom, T(EnhanceNameKeys.FastMode), ToggleX, 16f, ToggleW, 40f, false, SetFastMode, Focus);
            _continueToggle = UiKit.Toggle(Ctx, bottom, T(EnhanceNameKeys.AutoContinue), ToggleX, 60f, ToggleW, 40f, false, SetContinue, Focus);
            _stopOnDropToggle = UiKit.Toggle(Ctx, bottom, T(EnhanceNameKeys.StopOnDrop), ToggleX, 104f, ToggleW, 40f, _t.AutoStopOnDropDefault, v => _cont.StopOnDrop = v, Focus);
        }

        void BuildCards(RectTransform content)
        {
            _cardFocus = new UiFocusGroup(Manager != null ? Manager.InputMode : null, Ctx != null ? Ctx.Sound : null) { Tooltips = Manager };
            // 결과 카드(600×420): 짙은 유리 + 결과색 테 + 뒤 빛. 펼칠 때 튕기며 커진다.
            float cw = 720f, ch = 420f;   // 아래 줄 단추 셋([흔적 공개][닫기][다시 시도])이 잘리지 않는 폭
            var c = RitualScreen;
            _resultCard = EnhanceGfx.Centered(UiKit.Node(content, "ResultCard"), c.x, c.y + 10f, cw, ch);
            _resultCardGlow = EnhanceGfx.Img(_resultCard, EnhanceGfx.Glow, cw * 0.5f, ch * 0.5f, cw + 360f, ch + 360f, new Color(1f, 1f, 1f, 0.2f), "CardGlow");
            EnhanceGfx.Additive(Ctx, _resultCardGlow);
            _resultCardBg = EnhanceGfx.Panel(_resultCard, 0f, 0f, cw, ch, EnhanceFxStyle.WithA(UiTheme.Ink, 0.94f), false, "CardFill");
            UiKit.Image(_resultCard, EnhanceGfx.Sheen, 8f, 6f, cw - 16f, 140f, EnhanceFxStyle.WithA(Color.white, 0.07f), false);
            _resultCardEdge = EnhanceGfx.Panel(_resultCard, 0f, 0f, cw, ch, UiTheme.Chalk, true, "CardEdge");
            // 결과 띠(공통 그림 480×120, 글자 없음 — 제목 글자를 그 위에 얹는다).
            _resultBanner = EnhanceGfx.Img(_resultCard, null, cw * 0.5f, 72f, 480f, 120f, new Color(1f, 1f, 1f, 0f), "Banner");
            _resultLines = UiKit.Node(_resultCard, "Lines", 32f, 40f, cw - 64f, 300f);
            // 아래 줄: [흔적 공개] [닫기] [다시 시도] — 마우스만으로도 카드를 닫을 수 있게 닫기 단추를 둔다(K와 같은 동작).
            _traceCardButton = UiKit.Button(Ctx, _resultCard, string.Empty, 32f, ch - 72f, 180f, 48f, ToggleTraceFromCard, UiButtonStyle.Small, null, null, UiFontId.F16);
            _closeCardButton = UiKit.Button(Ctx, _resultCard, T(EnhanceNameKeys.CommonClose), 228f, ch - 72f, 140f, 48f, CloseResultCard, UiButtonStyle.Small, null, string.Empty, UiFontId.F16);
            _retryButton = UiKit.Button(Ctx, _resultCard, T(EnhanceNameKeys.Retry), cw - 32f - 304f, ch - 72f, 304f, 48f, Retry, UiButtonStyle.Small, null, string.Empty, UiFontId.F16);
            _resultCard.gameObject.SetActive(false);

            // 변이 카드(480×272 — ritual/mutation_card)
            _mutationCard = UiKit.Node(content, "MutationCard", SideW + MidW * 0.5f - 240f, PanelTop + 160f, 480f, 360f);
            var mglow = EnhanceGfx.Img(_mutationCard, EnhanceGfx.Glow, 240f, 180f, 820f, 700f, EnhanceFxStyle.WithA(UiTheme.Mutation, 0.18f), "MutGlow");
            EnhanceGfx.Additive(Ctx, mglow);
            var mc = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "mutation_card", 0);
            EnhanceGfx.Panel(_mutationCard, 0f, 0f, 480f, 360f, EnhanceFxStyle.WithA(UiTheme.Ink, 0.94f), false, "MutFill");
            if (mc != null) UiKit.Image(_mutationCard, mc, 0f, 0f, 480f, 272f, Color.white, false);
            EnhanceGfx.Panel(_mutationCard, 0f, 0f, 480f, 360f, EnhanceFxStyle.WithA(UiTheme.Mutation, 0.8f), true, "MutEdge");
            _mutIcon = UiKit.Image(_mutationCard, null, 24f, 24f, 64f, 64f, UiTheme.Mutation);
            _mutName = UiKit.Label(Ctx, _mutationCard, string.Empty, UiFontId.F24, 104f, 40f, 352f, 0f, UiTheme.Mutation, TextAnchor.MiddleLeft, false, true);
            _mutGain = UiKit.Label(Ctx, _mutationCard, string.Empty, UiFontId.F16, 24f, 104f, 432f, 48f, UiTheme.Chalk, TextAnchor.UpperLeft, true);
            _mutCost = UiKit.Label(Ctx, _mutationCard, string.Empty, UiFontId.F16, 24f, 156f, 432f, 48f, UiTheme.SlateBlue, TextAnchor.UpperLeft, true);
            _mutPush = UiKit.Label(Ctx, _mutationCard, string.Empty, UiFontId.F16, 24f, 208f, 432f, 0f, UiTheme.Amber);
            _acceptButton = UiKit.Button(Ctx, _mutationCard, T(EnhanceNameKeys.MutationAccept), 24f, 280f, 200f, 56f, () => ResolveMutation(true), UiButtonStyle.Large, _cardFocus, string.Empty);
            _rejectButton = UiKit.Button(Ctx, _mutationCard, T(EnhanceNameKeys.MutationReject), 256f, 280f, 200f, 56f, () => ResolveMutation(false), UiButtonStyle.Large, _cardFocus, string.Empty);
            _mutationCard.gameObject.SetActive(false);

            // 칭호 띠(ritual/title_ribbon_normal·gold 720×96).
            _titleBandRoot = EnhanceGfx.Centered(UiKit.Node(content, "TitleBand"), c.x, 470f, 960f, 120f);
            var bandGlow = EnhanceGfx.Img(_titleBandRoot, EnhanceGfx.Glow, 480f, 60f, 1100f, 320f, EnhanceFxStyle.WithA(UiTheme.Gold, 0.3f), "BandGlow");
            EnhanceGfx.Additive(Ctx, bandGlow);
            _titleRibbon = EnhanceGfx.Img(_titleBandRoot, null, 480f, 60f, 840f, 112f, Color.white, "Ribbon");
            _titleBand = UiKit.Label(Ctx, _titleBandRoot, string.Empty, UiFontId.F32, 0f, 24f, 960f, 72f, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            _titleBandRoot.gameObject.SetActive(false);
        }
    }
}
