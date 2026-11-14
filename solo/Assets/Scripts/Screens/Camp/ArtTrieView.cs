using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 비기 트라이 그림 + 설명 판 + 해금 고르기 판(08 요약서 8.4절). ArtTrie 화면과 야영 비기 탭이 같이 쓴다(트라이 그림 = 커맨드 목록 = 해금 화면).
    /// 영역 912×760: 그림 912×600, 설명 판 912×144. 해금 차례면 720×400 판 위에 후보 카드 320×320 한두 장.
    /// </summary>
    public sealed class ArtTrieView
    {
        /// <summary>판 배치(08 8.4절). 화면 배치 상수.</summary>
        public const float W = 912f, GraphH = 600f, DescH = 144f, PickW = 720f, PickH = 400f, CardSize = 320f;

        readonly GameContext _ctx;
        readonly RectTransform _root, _graph, _desc, _pick;
        readonly UiFocusGroup _focus;
        readonly Dictionary<UiButton, TrieNodeItem> _nodes = new Dictionary<UiButton, TrieNodeItem>();
        readonly List<UiButton> _buttons = new List<UiButton>();
        readonly List<UiButton> _pickButtons = new List<UiButton>();
        ArtTrieModel _model;
        TrieNodeItem _selected;

        /// <summary>해금 차례에서 비기를 골랐다(화면이 ArtPick 명령을 보낸다).</summary>
        public Action<string> Picked;

        public ArtTrieView(GameContext ctx, RectTransform parent, float x, float y, UiFocusGroup focus)
        {
            _ctx = ctx;
            _focus = focus;
            _root = UiKit.Node(parent, "ArtTrie", x, y, W, GraphH + DescH + 16f);
            UiKit.Panel(ctx, _root, UiPanelKind.Recess, 0f, 0f, W, GraphH);
            _graph = UiKit.Node(_root, "Graph", 16f, 16f, W - 32f, GraphH - 32f);
            UiKit.Panel(ctx, _root, UiPanelKind.Board, 0f, GraphH + 16f, W, DescH);
            _desc = UiKit.Node(_root, "Desc", 16f, GraphH + 24f, W - 32f, DescH - 16f);
            _pick = UiKit.Node(_root, "Pick", (W - PickW) * 0.5f, (GraphH - PickH) * 0.5f, PickW, PickH);
            _pick.gameObject.SetActive(false);
        }

        public RectTransform Root => _root;
        public ArtTrieModel Model => _model;
        public bool PickShown => _pick.gameObject.activeSelf;

        string T(string k) => _ctx != null && _ctx.Names != null ? _ctx.Names.Get(k) : k;
        string F(string k, params object[] a) => _ctx != null && _ctx.Names != null ? _ctx.Names.Format(k, a) : k;

        public void SetActive(bool on) => _root.gameObject.SetActive(on);

        public void Rebuild(ArtTrieModel m, bool pickTurn)
        {
            _model = m;
            foreach (var b in _buttons) if (b != null) _focus?.Remove(b);
            foreach (var b in _pickButtons) if (b != null) _focus?.Remove(b);
            _buttons.Clear();
            _pickButtons.Clear();
            _nodes.Clear();
            CampViews.Clear(_graph);
            CampViews.Clear(_pick);
            if (m == null) { ShowDescription(null); return; }

            // 가지(부모 → 자식 가로선 + 입력 문양).
            var byId = new Dictionary<int, TrieNodeItem>();
            foreach (var n in m.Nodes) byId[n.View.NodeId] = n;
            foreach (var n in m.Nodes)
            {
                TrieNodeItem parent;
                if (n.View.ParentId < 0 || !byId.TryGetValue(n.View.ParentId, out parent)) continue;
                float px = parent.X + ArtTrieModel.TechSize, py = parent.Y + ArtTrieModel.TechSize * 0.5f;
                float cx = n.X, cy = n.Y + ArtTrieModel.TechSize * 0.5f;
                UiKit.Rect(_graph, px, Mathf.Min(py, cy), 2f, Mathf.Abs(cy - py) + 2f, UiTheme.SlateBlue);
                UiKit.Rect(_graph, px, cy, Mathf.Max(2f, cx - px), 2f, UiTheme.SlateBlue);
                if (n.EdgeGlyphKey != null)
                    UiKit.LabelKey(_ctx, _graph, n.EdgeGlyphKey, UiFontId.F12, px + 8f, cy - 18f, 48f, 16f, UiTheme.Chalk);
                if (n.GapText != null)
                    UiKit.Label(_ctx, _graph, F("str.art_trie.interval", "sec", n.GapText), UiFontId.F12, px + 8f, cy + 4f, 72f, 16f, UiTheme.Amber);
            }

            foreach (var n in m.Nodes)
            {
                float size = n.IsTechnique ? ArtTrieModel.TechSize : ArtTrieModel.DotSize;
                float off = n.IsTechnique ? 0f : (ArtTrieModel.TechSize - ArtTrieModel.DotSize) * 0.5f;
                var item = n;
                var b = UiKit.Button(_ctx, _graph, null, n.X + off, n.Y + off, size, size, () => Select(item), UiButtonStyle.Plain, _focus, "");
                b.name = "TrieNode" + n.View.NodeId;
                bool locked = n.State == ArtNodeState.Locked;
                Color tone = locked ? UiTheme.SlateBlue : n.State == ArtNodeState.Candidate ? UiTheme.SelectYellow : UiTheme.Chalk;
                UiKit.Rect(b.ContentRoot, 0f, 0f, size, size, UiTheme.WithAlpha(UiTheme.WoodDark, 0.9f));
                UiFrame.Create(b.ContentRoot, tone, n.State == ArtNodeState.Current ? 3 : 1);
                if (n.IsTechnique)
                {
                    if (locked)
                    {
                        // 미해금 = 회청 단색 실루엣 + 자물쇠(U-R13).
                        var img = UiKit.Icon(_ctx, b.ContentRoot, n.View.ArtId, 48, 8f, 8f);
                        if (img != null) img.color = UiTheme.SlateBlue;
                        UiKit.LabelKey(_ctx, b.ContentRoot, "str.art_trie.lock_mark", UiFontId.F12, size - 16f, size - 16f, 16f, 16f, UiTheme.SlateBlue);
                    }
                    else UiKit.Icon(_ctx, b.ContentRoot, n.View.ArtId, 48, 8f, 8f);
                    if (n.IsUnique) UiKit.LabelKey(_ctx, b.ContentRoot, "str.art_trie.unique_mark", UiFontId.F16, 0f, 0f, 16f, 16f, UiTheme.SelectYellow);
                    UiKit.Label(_ctx, _graph, T(n.View.ArtId), UiFontId.F12, n.X - 24f, n.Y + size + 2f, size + 48f, 16f, tone, TextAnchor.UpperCenter);
                    if (n.View.JustArtId != null)
                    {
                        // 섬 기술은 K 기술과 따로 잠긴다(뇌광창) — 표식 색으로 그 상태를 보인다.
                        var js = n.View.JustState;
                        Color jt = js == ArtNodeState.Locked ? UiTheme.SlateBlue : js == ArtNodeState.Candidate ? UiTheme.SelectYellow : UiTheme.Chalk;
                        UiKit.LabelKey(_ctx, _graph, ArtTrieModel.JustGlyphKey, UiFontId.F12, n.X + size + 2f, n.Y, 32f, 16f, jt);
                    }
                }
                UiKit.AttachTooltip(b.gameObject, () => T(item.StateKey));
                _nodes[b] = n;
                _buttons.Add(b);
            }

            if (_selected != null && _selected.View != null)
            {
                var again = m.FindByArt(_selected.View.ArtId);
                _selected = again;
            }
            ShowDescription(_selected ?? FirstTechnique(m));

            bool showPick = pickTurn && m.Candidates.Count > 0;
            _pick.gameObject.SetActive(showPick);
            if (showPick) BuildPick(m);
        }

        static TrieNodeItem FirstTechnique(ArtTrieModel m)
        {
            foreach (var n in m.Nodes) if (n.IsTechnique) return n;
            return null;
        }

        void BuildPick(ArtTrieModel m)
        {
            UiKit.Panel(_ctx, _pick, UiPanelKind.Paper, 0f, 0f, PickW, PickH);
            UiKit.LabelKey(_ctx, _pick, "str.art_trie.unlock_pick", UiFontId.F24, 24f, 16f, PickW - 48f, 32f, UiTheme.PaperText);
            int n = m.PickCardCount;
            float total = n * CardSize + (n - 1) * 24f;
            float x0 = (PickW - total) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                string art = m.Candidates[i];
                var b = UiKit.Button(_ctx, _pick, null, x0 + i * (CardSize + 24f), 64f, CardSize, CardSize - 16f, () => Picked?.Invoke(art),
                    UiButtonStyle.Plain, _focus, "");
                b.name = "PickCard" + i;
                UiKit.Panel(_ctx, b.ContentRoot, UiPanelKind.Board, 0f, 0f, CardSize, CardSize - 16f);
                UiKit.Icon(_ctx, b.ContentRoot, art, 96, (CardSize - 96f) * 0.5f, 32f);
                UiKit.Label(_ctx, b.ContentRoot, T(art), UiFontId.F24, 8f, 144f, CardSize - 16f, 32f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                var node = m.FindByArt(art);
                var d = ArtTrieModel.Describe(_ctx != null ? _ctx.Content : null, node, node != null && node.View.JustArtId == art);
                UiKit.Label(_ctx, b.ContentRoot, CommandText(d), UiFontId.F16, 8f, 184f, CardSize - 16f, 24f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                UiKit.Label(_ctx, b.ContentRoot, CostText(d), UiFontId.F16, 8f, 216f, CardSize - 16f, 24f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                _pickButtons.Add(b);
            }
            if (_pickButtons.Count > 0) _focus?.SetCursor(_pickButtons[0], false);
        }

        public void Select(TrieNodeItem n)
        {
            _selected = n;
            ShowDescription(n);
        }

        string CommandText(TrieDescription d)
        {
            var s = string.Empty;
            for (int i = 0; i < d.CommandGlyphKeys.Count; i++) s += T(d.CommandGlyphKeys[i]);
            return s;
        }

        string CostText(TrieDescription d) =>
            d.CostAll ? T("str.art_trie.crystal_cost_all") : F("str.art_trie.crystal_value", "n", d.Cost);

        void ShowDescription(TrieNodeItem n)
        {
            CampViews.Clear(_desc);
            if (n == null || !n.IsTechnique) return;
            var db = _ctx != null ? _ctx.Content : null;
            var d = ArtTrieModel.Describe(db, n);
            float x = 0f;
            UiKit.Label(_ctx, _desc, T(d.ArtId), UiFontId.F24, x, 0f, 280f, 32f);
            UiKit.Label(_ctx, _desc, T(n.StateKey), UiFontId.F16, x, 36f, 200f, 24f, UiTheme.SlateBlue);
            float c2 = 300f;
            UiKit.Label(_ctx, _desc, T("str.art_trie.command") + " " + CommandText(d), UiFontId.F16, c2, 0f, 280f, 24f);
            string dots = string.Empty;
            for (int i = 0; i < ArtDifficultyMax; i++) dots += T(i < d.Difficulty ? "str.art_trie.dot_on" : "str.art_trie.dot_off");
            UiKit.Label(_ctx, _desc, T("str.art_trie.difficulty") + " " + dots, UiFontId.F16, c2, 28f, 280f, 24f);
            UiKit.Label(_ctx, _desc, T("str.art_trie.crystal_cost") + " " + CostText(d), UiFontId.F16, c2, 56f, 280f, 24f);
            float c3 = 600f;
            UiKit.LabelKey(_ctx, _desc, "str.art_trie.effect", UiFontId.F16, c3, 0f, 260f, 24f, UiTheme.SlateBlue);
            for (int i = 0; i < d.EffectLines.Count; i++)
                UiKit.Label(_ctx, _desc, F(d.EffectLines[i].Key, "n", d.EffectLines[i].Value), UiFontId.F16, c3, 28f + i * 28f, 260f, 24f);
            if (d.FallbackArtId != null)
            {
                string fb = T("str.art_trie.fallback") + " " + T(d.FallbackArtId);
                if (d.ParentReplace) fb += " " + T("str.art_trie.parent_replace");
                UiKit.Label(_ctx, _desc, fb, UiFontId.F16, c2, 88f, 560f, 24f, UiTheme.SlateBlue);
            }
            if (d.JustArtId != null)
                UiKit.Label(_ctx, _desc, T(ArtTrieModel.JustGlyphKey) + " " + T(d.JustArtId), UiFontId.F16, x, 64f, 280f, 24f, UiTheme.Chalk);
        }

        /// <summary>난이도 점 수(08 8.4절 "난이도 점(1~5)"). 화면 배치 상수.</summary>
        const int ArtDifficultyMax = 5;

        /// <summary>그 버튼이 트라이 마디면 그 마디.</summary>
        public TrieNodeItem NodeOf(IUiFocusable f)
        {
            var b = f as UiButton;
            TrieNodeItem n;
            return b != null && _nodes.TryGetValue(b, out n) ? n : null;
        }
    }
}
