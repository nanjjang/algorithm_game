using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>이 묶음 화면들의 그리기 도우미(UiKit 위에 조립만 한다. 규칙은 모델에).</summary>
    public static class CampViews
    {
        /// <summary>등급 테 색(08 1.5절: 일반 회색, 희귀 파랑, 영웅 금색).</summary>
        public static Color RarityColor(string rarity)
        {
            if (rarity == Ids.Rarity.Hero) return UiTheme.Gold;
            if (rarity == Ids.Rarity.Rare) return UiTheme.RareBlue;
            return UiTheme.CommonGray;
        }

        /// <summary>등급 테 모양(색으로만 구분하지 않는다, 08 1.5절): 테 두께 2 / 3 / 4px.</summary>
        public static int RarityFrameThickness(string rarity)
        {
            if (rarity == Ids.Rarity.Hero) return 2 + 2;
            if (rarity == Ids.Rarity.Rare) return 2 + 1;
            return 2;
        }

        public static string RarityOf(IContentDb db, string blockId)
        {
            var def = db != null && blockId != null ? db.Block(blockId) : null;
            return def != null ? def.Rarity : Ids.Rarity.Common;
        }

        /// <summary>자식을 모두 걷는다(플레이 중 Destroy, 에디터 테스트 DestroyImmediate).</summary>
        public static void Clear(RectTransform rt)
        {
            if (rt == null) return;
            for (int i = rt.childCount - 1; i >= 0; i--)
            {
                var go = rt.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(go); else UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>점선 테(빌린 블록, 생략 마디). 테 위에 짧은 막대를 번갈아 놓는다.</summary>
        public static void DashedFrame(RectTransform parent, float w, float h, Color color, int thickness, float dash, float gap)
        {
            var root = UiKit.Node(parent, "Dashed", 0f, 0f, w, h);
            float step = dash + gap;
            for (float x = 0f; x < w; x += step)
            {
                float len = Mathf.Min(dash, w - x);
                UiKit.Rect(root, x, 0f, len, thickness, color);
                UiKit.Rect(root, x, h - thickness, len, thickness, color);
            }
            for (float y = 0f; y < h; y += step)
            {
                float len = Mathf.Min(dash, h - y);
                UiKit.Rect(root, 0f, y, thickness, len, color);
                UiKit.Rect(root, w - thickness, y, thickness, len, color);
            }
        }

        /// <summary>
        /// 블록 칸 하나(Plain 버튼 + 아이콘 + 등급 테, 빌린 블록은 점선 테). 빈 칸은 어두운 바탕만.
        /// 끌어다 놓기는 slotRef를 실은 uGUI 끌기(UiKit.MakeDraggable/MakeDropTarget).
        /// </summary>
        public static UiButton BlockCell(GameContext ctx, RectTransform parent, float x, float y, float size, AssemblyItem item,
            Action onClick, UiFocusGroup group, bool locked = false)
        {
            var b = UiKit.Button(ctx, parent, null, x, y, size, size, onClick, UiButtonStyle.Plain, group, "");
            b.name = "Cell";
            UiKit.Rect(b.ContentRoot, 0f, 0f, size, size, UiTheme.WithAlpha(UiTheme.WoodDark, 0.9f));
            if (locked)
            {
                UiKit.Rect(b.ContentRoot, 4f, 4f, size - 8f, size - 8f, UiTheme.WithAlpha(UiTheme.SlateBlue, 0.25f));
                return b;
            }
            if (item.IsEmpty)
            {
                var f = UiFrame.Create(b.ContentRoot, UiTheme.WoodLight, 1);
                f.name = "EmptyFrame";
                return b;
            }
            string rarity = RarityOf(ctx != null ? ctx.Content : null, item.BlockId);
            float icon = size >= 64f ? 48f : size - 16f;
            UiKit.Icon(ctx, b.ContentRoot, item.BlockId, (int)icon, (size - icon) * 0.5f, (size - icon) * 0.5f);
            if (item.Borrowed)
            {
                DashedFrame(b.ContentRoot, size, size, RarityColor(rarity), 2, 6f, 4f);
                BorrowedTag(ctx, b.ContentRoot, 2f, 2f, 1);
            }
            else UiFrame.Create(b.ContentRoot, RarityColor(rarity), RarityFrameThickness(rarity));
            if (item.Stars > 0) StarBadge(ctx, b.ContentRoot, size - (size >= 64f ? 30f : 24f), size - (size >= 64f ? 18f : 15f), item.Stars, size >= 64f ? 14f : 12f);
            string name = ctx != null && ctx.Names != null ? ctx.Names.Get(item.BlockId) : item.BlockId;
            if (item.Stars > 0 && ctx != null && ctx.Names != null) name += " " + ctx.Names.Format("str.block.stars", "n", item.Stars);
            UiKit.AttachTooltip(b.gameObject, () => name);
            return b;
        }

        /// <summary>판 한정 블록 성장 표식 "★n"(칸 오른쪽 아래, 어두운 받침 위 금빛 글자). h = 글자 칸 높이(px).</summary>
        public static UiLabel StarBadge(GameContext ctx, RectTransform parent, float x, float y, int stars, float h)
        {
            string text = ctx != null && ctx.Names != null ? ctx.Names.Format("str.block.stars", "n", stars) : stars.ToString(System.Globalization.CultureInfo.InvariantCulture);
            float w = h * 2.2f;
            var bg = UiKit.Rect(parent, x - 2f, y - 1f, w, h + 2f, UiTheme.WithAlpha(UiTheme.Ink, 0.78f), false, "StarBadge");
            var font = h >= 18f ? UiFontId.F16 : UiFontId.F12;
            var l = UiKit.Label(ctx, bg.rectTransform, text, font, 0f, 0f, w, h + 2f, UiTheme.Gold, TextAnchor.MiddleCenter);
            l.name = "Stars";
            return l;
        }

        /// <summary>"빌림" 꼬리표(UI I-M10/borrowed_tag 32×16, 보충 그림)를 scale 정수배로 놓는다. 그림이 없으면 아무것도 놓지 않는다(점선 테와 글자가 남는다).</summary>
        public static UImage BorrowedTag(GameContext ctx, RectTransform parent, float x, float y, int scale)
        {
            UiSprite tag = UiKit.KitSprite(ctx, "I-M10", "borrowed_tag");
            if (!tag.IsValid) return null;
            int s = Mathf.Max(1, scale);
            float w = (tag.Width > 0 ? tag.Width : tag.Sprite.rect.width) * s, h = (tag.Height > 0 ? tag.Height : tag.Sprite.rect.height) * s;
            var img = UiKit.Image(parent, tag.Sprite, x, y, w, h, null, false);
            img.name = "BorrowedTag";
            img.raycastTarget = false;
            return img;
        }

        /// <summary>속성 문양(16×16 아이콘). 없는 그림은 대체 그림(UiKit.Icon이 맡는다).</summary>
        public static void ElementGlyph(GameContext ctx, RectTransform parent, string elementId, float x, float y, int size = 16)
        {
            if (string.IsNullOrEmpty(elementId)) return;
            UiKit.Icon(ctx, parent, elementId, size, x, y);
        }
    }

    /// <summary>
    /// 지팡이 회로(08 8.2절 왼쪽 (24,160) 432×168): 첫 줄 코어(72) + 연결 4칸(제자 3은 5칸), 둘째 줄 확장 3칸(열린 것만, 나머지 잠김).
    /// </summary>
    public sealed class CircuitView
    {
        /// <summary>회로 배치(08 8.2절: 코어 72, 칸 64). 확장 칸 최대 3(SL-2). 화면 배치 상수.</summary>
        public const float CoreSize = 72f, LinkSize = 64f, Gap = 4f, RowGap = 16f;
        public const int MaxExtraSlots = 3;

        readonly GameContext _ctx;
        readonly RectTransform _root;
        public readonly Dictionary<UiButton, int> Refs = new Dictionary<UiButton, int>();
        public readonly List<UiButton> Cells = new List<UiButton>();

        public CircuitView(GameContext ctx, RectTransform parent, float x, float y, float w, float h)
        {
            _ctx = ctx;
            _root = UiKit.Node(parent, "Circuit", x, y, w, h);
        }

        public RectTransform Root => _root;

        public void Rebuild(CampAssembly a, UiFocusGroup group, Action<int> onClick, Func<int, bool> canDragFrom,
            Func<int, int, bool> canMove, Action<int, int> onDrop, int heldRef, int highlightRef)
        {
            foreach (var c in Cells) if (c != null) group?.Remove(c);
            Cells.Clear();
            Refs.Clear();
            CampViews.Clear(_root);
            if (a == null) return;
            int baseLinks = a.BaseLinkCount;
            int total = a.LinkCount;
            AddCell(a, SlotRefs.Core, 0f, 0f, CoreSize, group, onClick, canDragFrom, canMove, onDrop, heldRef, highlightRef);
            for (int i = 0; i < baseLinks; i++)
                AddCell(a, SlotRefs.Link(i), CoreSize + Gap + i * (LinkSize + Gap), (CoreSize - LinkSize) * 0.5f, LinkSize, group, onClick, canDragFrom, canMove, onDrop, heldRef, highlightRef);
            float y2 = CoreSize + RowGap;
            for (int e = 0; e < MaxExtraSlots; e++)
            {
                int linkIndex = baseLinks + e;
                float x = CoreSize + Gap + e * (LinkSize + Gap);
                if (linkIndex < total)
                    AddCell(a, SlotRefs.Link(linkIndex), x, y2, LinkSize, group, onClick, canDragFrom, canMove, onDrop, heldRef, highlightRef);
                else
                    CampViews.BlockCell(_ctx, _root, x, y2, LinkSize, default, null, null, locked: true);
            }
        }

        void AddCell(CampAssembly a, int r, float x, float y, float size, UiFocusGroup group, Action<int> onClick,
            Func<int, bool> canDragFrom, Func<int, int, bool> canMove, Action<int, int> onDrop, int heldRef, int highlightRef)
        {
            var item = a.At(r);
            int myRef = r;
            var b = CampViews.BlockCell(_ctx, _root, x, y, size, item, () => onClick?.Invoke(myRef), group);
            b.Picked = heldRef == myRef;
            if (highlightRef == myRef) UiFrame.Create(b.ContentRoot, UiTheme.SelectYellow, 2).name = "TreeLink";
            UiKit.MakeDraggable(b.gameObject, myRef, null, () => canDragFrom == null || canDragFrom(myRef));
            UiKit.MakeDropTarget(b.gameObject, p => p is int src && canMove != null && canMove(src, myRef), p => { if (p is int src) onDrop?.Invoke(src, myRef); });
            Refs[b] = myRef;
            Cells.Add(b);
        }
    }

    /// <summary>
    /// 컴파일 트리 미리보기(08 8.3절, (24,344) 432×480): 스코프 사슬, 주인 굵은 테, 깊이 감쇠 꼬리표, 생략 점선, 경고 딱지, 맨 아래 줄.
    /// </summary>
    public sealed class TreePreviewView
    {
        /// <summary>마디 배치(08 8.3절: 32×32 틀 안 24×24 아이콘, 세로 간격 16, 가로 간격 8, 들여쓰기 40). 화면 배치 상수.</summary>
        public const float Frame = 32f, IconSize = 24f, VGap = 16f, HGap = 8f, Indent = 40f, FooterH = 24f;

        readonly GameContext _ctx;
        readonly RectTransform _root;
        readonly RectTransform _body;
        readonly UiLabel _footer;
        readonly float _w, _h;
        public readonly Dictionary<UiButton, int> NodeSlots = new Dictionary<UiButton, int>();
        public readonly List<UiButton> Nodes = new List<UiButton>();

        public TreePreviewView(GameContext ctx, RectTransform parent, float x, float y, float w, float h)
        {
            _ctx = ctx;
            _w = w; _h = h;
            _root = UiKit.Node(parent, "TreePreview", x, y, w, h);
            UiKit.Panel(ctx, _root, UiPanelKind.Recess, 0f, 0f, w, h);
            _body = UiKit.Node(_root, "Body", 8f, 8f, w - 16f, h - 16f - FooterH);
            _footer = UiKit.Label(ctx, _root, string.Empty, UiFontId.F16, 8f, h - FooterH - 4f, w - 16f, FooterH);
        }

        string T(string k) => _ctx != null && _ctx.Names != null ? _ctx.Names.Get(k) : k;
        string F(string k, params object[] a) => _ctx != null && _ctx.Names != null ? _ctx.Names.Format(k, a) : k;

        public void Rebuild(TreePreviewModel m, UiFocusGroup group, Action<int> onNodeCursor, int bracketSlot)
        {
            foreach (var n in Nodes) if (n != null) group?.Remove(n);
            Nodes.Clear();
            NodeSlots.Clear();
            CampViews.Clear(_body);
            if (m == null) { _footer.SetText(string.Empty); return; }
            float y = 0f;
            for (int s = 0; s < m.Scopes.Count; s++)
            {
                var sc = m.Scopes[s];
                float indent = sc.Depth * Indent;
                Color tone = UiTheme.WithAlpha(UiTheme.Chalk, sc.Brightness);
                if (sc.Depth > 0)
                {
                    // 왼쪽 세로 묶음 선 + 배율 꼬리표(오른쪽 끝) + 갈피 각주 점.
                    float lines = Mathf.Max(1, sc.Nodes.Count) * (Frame + VGap);
                    UiKit.Rect(_body, indent - HGap, y, 2f, lines, tone);
                    string tag = F("str.camp.tree.multiplier", "v", sc.MultiplierText);
                    for (int d = 0; d < sc.FootnoteDots; d++) tag += T("str.camp.tree.footnote_dot");
                    UiKit.Label(_ctx, _body, tag, UiFontId.F12, _w - 16f - 120f, y, 120f, 16f, tone, TextAnchor.UpperRight);
                }
                for (int i = 0; i < sc.Nodes.Count; i++)
                {
                    var node = sc.Nodes[i];
                    var b = UiKit.Button(_ctx, _body, null, indent, y, Frame, Frame, null, UiButtonStyle.Plain, group, "");
                    b.name = "Node" + node.Order;
                    int slot = node.Slot;
                    b.OnClick = () => onNodeCursor?.Invoke(slot);
                    if (node.Omitted) CampViews.DashedFrame(b.ContentRoot, Frame, Frame, UiTheme.SlateBlue, 1, 4f, 3f);
                    else UiFrame.Create(b.ContentRoot, tone, node.Shape == TreeNodeShape.ScopeOwner ? 3 : 1);
                    if (!node.Omitted) UiKit.Icon(_ctx, b.ContentRoot, node.BlockId, (int)IconSize, (Frame - IconSize) * 0.5f, (Frame - IconSize) * 0.5f);
                    UiKit.Label(_ctx, b.ContentRoot, node.Order.ToString(System.Globalization.CultureInfo.InvariantCulture), UiFontId.F12, -6f, -8f, 16f, 16f, tone);
                    if (node.HasWarning) UiKit.WarningTriangle(b.ContentRoot, Frame - 8f, -8f, 16f);
                    if (bracketSlot >= 0 && node.Slot == bracketSlot) b.Picked = true;
                    float tx = indent + Frame + HGap;
                    if (node.Omitted)
                        UiKit.LabelKey(_ctx, _body, "str.camp.tree.omitted", UiFontId.F12, tx, y + 8f, 120f, 16f, UiTheme.SlateBlue);
                    else
                    {
                        for (int k = 0; k < node.Modifiers.Count; k++)
                        {
                            UiKit.Icon(_ctx, _body, node.Modifiers[k], (int)IconSize, tx, y + (Frame - IconSize) * 0.5f);
                            tx += IconSize + 2f;
                        }
                        if (node.MoreModifiers > 0)
                        {
                            UiKit.Label(_ctx, _body, F("str.format.more_count", "n", node.MoreModifiers), UiFontId.F12, tx, y + 8f, 40f, 16f, tone);
                            tx += 40f;
                        }
                        if (node.ForkWays > 0)
                            UiKit.Label(_ctx, _body, F("str.camp.tree.fork", "n", node.ForkWays), UiFontId.F12, tx + HGap, y + 8f, 80f, 16f, tone);
                    }
                    NodeSlots[b] = node.Slot;
                    Nodes.Add(b);
                    y += Frame + VGap;
                }
                if (sc.Nodes.Count == 0) y += Frame + VGap;
            }
            _footer.SetText(F("str.camp.tree.footer", "n", m.ProjectilesPerShot, "heat", CampFormat.OneDecimal(m.HeatPerShot)));
        }
    }
}
