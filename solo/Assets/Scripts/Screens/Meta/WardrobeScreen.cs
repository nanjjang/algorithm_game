using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Meta
{
    /// <summary>
    /// 옷장(ScreenId.Wardrobe, 08 11.2절, UA 4.24, D-73). 오프라인에서도 된다.
    /// 미리보기 (48,104) 560×880: 위에 캐릭터 초상 5개, 캐릭터 256×256(지팡이는 지금 든 지팡이 단계 그대로), 방향 돌리기·걷기·앉기·감정표현
    /// (그 클립 그림이 없으면 서 있는 그림), 아래에 고른 칸의 정보(이름, 얻은 곳, 계정에 묶임).
    /// 분류 (632,104) 280×880: 12분류(Q/E로도). "옷" 분류 맨 위에 야생 물 단계 고르기.
    /// 격자 (936,104) 936×880, 칸 96×96 8열: 얻은 것만(TV-197). 첫 칸 "원래 모습". 입는 중 = 확정 체크. 입히면 저장.
    /// 규칙은 WardrobeModel. 한정판 겉모습은 색·장식만 바꾸고 단계 실루엣을 가리지 않는다(그림 쪽 약속 — 여기서는 미리보기만).
    /// </summary>
    public sealed class WardrobeScreen : ScreenBase
    {
        const float Top = TopBarHeight;
        const float PrevX = 48f, PrevY = 104f - Top, PrevW = 560f, PrevH = 880f;
        const float CatX = 632f, CatY = 104f - Top, CatW = 280f, CatH = 880f, CatGap = 4f;
        const float GridX = 936f, GridY = 104f - Top, GridW = 936f, GridH = 880f, Cell = 96f;
        const int GridColumns = 8;
        const float Portrait = 88f, FigureSize = 256f, Pad = 24f, BtnH = 56f;
        /// <summary>몸 그림 한 칸(px, players manifest canvas)과 미리보기 확대 배율 = 256 / 64.</summary>
        const float FramePx = 64f;

        int _character = 1;
        int _category;
        Facing3 _facing = Facing3.Down;
        WardrobePose _pose = WardrobePose.Idle;
        float _animTime;
        string _selected;
        bool _selectedOriginal = true;

        RectTransform _figureRoot, _gridArea, _info;
        UImage _body, _wand;
        readonly UiButton[] _portraits = new UiButton[Ids.Char.Count];
        UiButton _turn, _walk, _sit, _emote;
        readonly List<UiButton> _cells = new List<UiButton>();
        readonly List<string> _cellIds = new List<string>();
        readonly List<UiButton> _stainButtons = new List<UiButton>();

        public override ScreenId Id => ScreenId.Wardrobe;
        protected override string TitleKey => MetaKeys.WardrobeTitle;

        protected override void BuildContent(RectTransform content)
        {
            BuildPreview(content);
            var labels = new List<string>(WardrobeModel.Categories.Length);
            for (int i = 0; i < WardrobeModel.Categories.Length; i++) labels.Add(T(WardrobeModel.CategoryNameKey(WardrobeModel.Categories[i])));
            UiKit.Panel(Ctx, content, UiPanelKind.Recess, CatX, CatY, CatW, CatH);
            float tabH = (CatH - CatGap * (labels.Count - 1)) / labels.Count;
            Tabs = UiKit.Tabs(Ctx, content, labels, CatX, CatY, CatW, tabH, SelectCategory, 0, true, CatGap);
            UiKit.Panel(Ctx, content, UiPanelKind.Board, GridX, GridY, GridW, GridH);
            _gridArea = UiKit.Node(content, "WardrobeGrid", GridX + Pad, GridY + Pad, GridW - 2f * Pad, GridH - 2f * Pad);
        }

        void BuildPreview(RectTransform content)
        {
            var panel = UiKit.Panel(Ctx, content, UiPanelKind.Board, PrevX, PrevY, PrevW, PrevH).rectTransform;
            panel.name = "WardrobePreview";
            float gap = (PrevW - 2f * Pad - Portrait * Ids.Char.Count) / (Ids.Char.Count - 1);
            for (int i = 0; i < Ids.Char.Count; i++)
            {
                int ch = i + 1;
                var b = UiKit.Button(Ctx, panel, null, Pad + i * (Portrait + gap), Pad, Portrait, Portrait, () => SelectCharacter(ch),
                    UiButtonStyle.Plain, Focus);
                var sp = Ctx != null && Ctx.Art != null ? Ctx.Art.CharacterPortrait(Ids.Char.Of(ch), 0) : null;
                var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                if (sp != null) UiKit.Image(root, sp, 12f, 12f, Portrait - 24f, Portrait - 24f, null, true);
                else UiKit.Label(Ctx, root, T(Ids.Char.Of(ch)), UiFontId.F12, 0f, 0f, Portrait, Portrait, UiTheme.Chalk, TextAnchor.MiddleCenter);
                _portraits[i] = b;
            }

            float fy = Pad + Portrait + 24f;
            UiKit.Panel(Ctx, panel, UiPanelKind.Recess, (PrevW - FigureSize) * 0.5f - 8f, fy - 8f, FigureSize + 16f, FigureSize + 16f);
            _figureRoot = UiKit.Node(panel, "Figure", (PrevW - FigureSize) * 0.5f, fy, FigureSize, FigureSize);
            _body = UiKit.Image(_figureRoot, null, 0f, 0f, FigureSize, FigureSize, null, false);
            _wand = UiKit.Image(_figureRoot, null, 0f, 0f, FigureSize * 0.5f, FigureSize, null, false);

            float by = fy + FigureSize + 24f;
            float bw = (PrevW - 2f * Pad - 16f) * 0.5f;
            _turn = UiKit.Button(Ctx, panel, T(MetaKeys.WardrobeTurn), Pad, by, bw, BtnH, Turn, UiButtonStyle.Medium, Focus);
            _walk = UiKit.Button(Ctx, panel, T(MetaKeys.WardrobeWalk), Pad + bw + 16f, by, bw, BtnH, () => SetPose(WardrobePose.Walk),
                UiButtonStyle.Medium, Focus);
            _sit = UiKit.Button(Ctx, panel, T(MetaKeys.WardrobeSit), Pad, by + BtnH + 12f, bw, BtnH, () => SetPose(WardrobePose.Sit),
                UiButtonStyle.Medium, Focus);
            _emote = UiKit.Button(Ctx, panel, T(MetaKeys.WardrobeEmote), Pad + bw + 16f, by + BtnH + 12f, bw, BtnH,
                () => SetPose(WardrobePose.Emote), UiButtonStyle.Medium, Focus);

            float iy = by + 2f * BtnH + 36f;
            _info = UiKit.Panel(Ctx, panel, UiPanelKind.Recess, Pad, iy, PrevW - 2f * Pad, PrevH - iy - Pad).rectTransform;
            _info.name = "WardrobeInfo";
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            var p = Ctx != null ? Ctx.Profile : null;
            _character = p != null && p.LastCharacter >= 1 && p.LastCharacter <= Ids.Char.Count ? p.LastCharacter : 1;
            _pose = WardrobePose.Idle;
            _facing = Facing3.Down;
            _category = Tabs != null ? Tabs.Index : 0;
            RefreshAll();
            Focus.SetCursor(0, false);
        }

        // ---------------------------------------------------------------- 고르기

        void SelectCharacter(int ch)
        {
            _character = ch;
            RefreshAll();
        }

        void SelectCategory(int i)
        {
            _category = i;
            _selected = null;
            _selectedOriginal = true;
            RebuildGrid();
            RefreshInfo();
        }

        void Turn()
        {
            _facing = WardrobeModel.NextFacing(_facing);
            _animTime = 0f;
            RefreshFigure();
        }

        void SetPose(WardrobePose pose)
        {
            _pose = WardrobeModel.Toggle(_pose, pose);
            _animTime = 0f;
            RefreshFigure();
        }

        void RefreshAll()
        {
            for (int i = 0; i < _portraits.Length; i++) if (_portraits[i] != null) _portraits[i].Picked = i + 1 == _character;
            RebuildGrid();
            RefreshFigure();
            RefreshInfo();
        }

        string Category => WardrobeModel.Categories[Mathf.Clamp(_category, 0, WardrobeModel.Categories.Length - 1)];

        void RebuildGrid()
        {
            // 커서 묶음에서 앞의 칸을 빼고 다시 넣는다(미리보기 단추·초상은 그대로).
            for (int i = 0; i < _cells.Count; i++) Focus.Remove(_cells[i]);
            for (int i = 0; i < _stainButtons.Count; i++) Focus.Remove(_stainButtons[i]);
            _cells.Clear();
            _cellIds.Clear();
            _stainButtons.Clear();
            MetaUi.ClearChildren(_gridArea);

            var p = Ctx != null ? Ctx.Profile : null;
            string cat = Category;
            float y = 0f;
            if (cat == WardrobeModel.ClothesCategory) y = BuildStainPicker(p);

            var cells = WardrobeModel.Cells(p, cat);
            float gap = (GridW - 2f * Pad - GridColumns * Cell) / (GridColumns - 1);
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                var pos = UiKit.GridCell(i, GridColumns, Cell, Cell, gap, gap);
                string id = c.Id;
                var b = UiKit.Button(Ctx, _gridArea, null, pos.x, y + pos.y, Cell, Cell, () => Wear(id), UiButtonStyle.Plain, Focus);
                b.name = c.IsOriginal ? "Original" : id;
                var root = b.ContentRoot != null ? b.ContentRoot : b.Rect;
                UiKit.NineSlice(Ctx, root, "K-60", "item_slot_filled", null, 0f, 0f, Cell, Cell, UiTheme.WoodLight);
                if (c.IsOriginal)
                    UiKit.Label(Ctx, root, T(MetaKeys.WardrobeOriginal), UiFontId.F12, 4f, 0f, Cell - 8f, Cell, UiTheme.Chalk,
                        TextAnchor.MiddleCenter, true);
                else UiKit.Icon(Ctx, root, id, 64, (Cell - 64f) * 0.5f, (Cell - 64f) * 0.5f);
                b.Picked = c.Worn;
                UiKit.AttachTooltip(b.gameObject, () => id == null ? T(MetaKeys.WardrobeOriginal) : T(id));
                _cells.Add(b);
                _cellIds.Add(id);
            }
        }

        /// <summary>"옷" 분류 맨 위: 야생 물 단계 고르기(1 = 원래 모습 ~ 그 캐릭터의 최고 단계). 높이를 돌려준다.</summary>
        float BuildStainPicker(ProfileData p)
        {
            var rec = p != null ? p.Character(_character) : null;
            var th = WardrobeModel.StainThresholds(Ctx != null ? Ctx.Content : null);
            int max = WardrobeModel.StainMax(rec, th);
            int worn = WardrobeModel.StainWorn(rec, th);
            float lh = UiTheme.LineHeight(UiFontId.F16);
            UiKit.Label(Ctx, _gridArea, T(MetaKeys.WardrobeStainPick), UiFontId.F16, 0f, 0f, GridW - 2f * Pad, lh, UiTheme.SlateBlue);
            float bw = 200f, gap = 16f, y = lh + 8f;
            for (int s = 1; s <= max; s++)
            {
                int stage = s;
                var b = UiKit.Button(Ctx, _gridArea, T(WardrobeModel.WildStainNameKey(stage)), (s - 1) * (bw + gap), y, bw, BtnH,
                    () => PickStain(stage), UiButtonStyle.Medium, Focus, null, UiFontId.F16);
                b.Picked = stage == worn;
                _stainButtons.Add(b);
            }
            float h = y + BtnH + 16f;
            UiKit.Divider(Ctx, _gridArea, 0f, h - 8f, GridW - 2f * Pad);
            return h + 8f;
        }

        void Wear(string id)
        {
            _selected = id;
            _selectedOriginal = id == null;
            WardrobeModel.Wear(Ctx != null ? Ctx.Profiles : null, Category, id);
            var p = Ctx != null ? Ctx.Profile : null;
            string worn = WardrobeModel.Worn(p, Category);
            for (int i = 0; i < _cells.Count; i++) _cells[i].Picked = _cellIds[i] == worn;
            RefreshInfo();
            RefreshFigure();
        }

        void PickStain(int stage)
        {
            var th = WardrobeModel.StainThresholds(Ctx != null ? Ctx.Content : null);
            if (!WardrobeModel.PickStain(Ctx != null ? Ctx.Profiles : null, _character, stage, th)) return;
            for (int i = 0; i < _stainButtons.Count; i++) _stainButtons[i].Picked = i + 1 == stage;
            RefreshFigure();
        }

        // ---------------------------------------------------------------- 그리기

        void RefreshInfo()
        {
            MetaUi.ClearChildren(_info);
            float lh = UiTheme.LineHeight(UiFontId.F24), sh = UiTheme.LineHeight(UiFontId.F16);
            float w = PrevW - 2f * Pad - 32f;
            string id = _selectedOriginal ? null : _selected;
            UiKit.Label(Ctx, _info, id == null ? T(MetaKeys.WardrobeOriginal) : T(id), UiFontId.F24, 16f, 12f, w, lh, UiTheme.Chalk);
            float y = 12f + lh + 8f;
            if (id == null) return;
            string src = WardrobeModel.SourceKey(id);
            if (src != null)
            {
                UiKit.Label(Ctx, _info, T(MetaKeys.WardrobeAcquiredFrom) + " " + T(src), UiFontId.F16, 16f, y, w, sh, UiTheme.SlateBlue);
                y += sh + 4f;
            }
            if (WardrobeModel.IsBound(id)) UiKit.Label(Ctx, _info, T(MetaKeys.WardrobeBound), UiFontId.F16, 16f, y, w, sh, UiTheme.SlateBlue);
        }

        void RefreshFigure()
        {
            if (_body == null) return;
            var art = Ctx != null ? Ctx.Art : null;
            string charId = Ids.Char.Of(_character);
            if (art == null)
            {
                _body.sprite = null;
                _body.color = UiTheme.WoodLight;
                _wand.enabled = false;
                return;
            }
            string clip = WardrobeModel.ResolveClip(_pose, _facing, (c, f) => art.CharacterClipInfo(charId, c, f) != null);
            var anim = art.CharacterClip(charId, clip, _facing);
            var info = art.CharacterClipInfo(charId, clip, _facing);
            var sp = anim != null ? anim.At(_animTime) : null;
            _body.sprite = sp;
            _body.color = Color.white;

            // 지팡이: 지금 든 지팡이 단계 그대로(손 점 − 손잡이 점 = 지팡이 칸 왼쪽 위).
            int stage = TitleModel.HeldStage(Ctx.Profile, _character);
            var wandSp = art.WandHand(charId, stage, _facing, false);
            int frame = FrameIndex(anim, _animTime);
            if (wandSp != null && info != null && info.Frames.Count > 0)
            {
                var f = info.Frames[Mathf.Clamp(frame, 0, info.Frames.Count - 1)];
                var tl = WandSheet.AttachTopLeft(f.Hand, art.WandGrip);
                float k = FigureSize / FramePx;
                UiKit.Place(_wand.rectTransform, tl.X * k, tl.Y * k, wandSp.rect.width * k, wandSp.rect.height * k);
                _wand.sprite = wandSp;
                _wand.enabled = true;
            }
            else _wand.enabled = false;
        }

        static int FrameIndex(SpriteAnim a, float t)
        {
            if (a == null || a.Frames == null || a.Frames.Length == 0 || a.Fps <= 0f) return 0;
            int i = (int)(t * a.Fps);
            return a.Loop ? i % a.Frames.Length : Mathf.Min(i, a.Frames.Length - 1);
        }

        public override void Tick(float dt)
        {
            _animTime += dt;
            RefreshFigure();
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { Close(); return true; }
            return base.OnMenuKey(key);
        }
    }
}
