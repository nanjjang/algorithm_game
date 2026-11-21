using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>
    /// 비기 트라이 그림 + 설명 판 + 해금 고르기 판(08 요약서 8.4절). ArtTrie 화면과 야영 비기 탭이 같이 쓴다(트라이 그림 = 커맨드 목록 = 해금 화면).
    /// 영역 912×760: 그림 912×560, 설명 판 912×184. 해금 차례면 720×400 판 위에 후보 카드 320×320 한두 장.
    /// 설명 판은 비기 풀이(ArtGuide): 내는 법(지금 키 배치), 효과 한 줄, 결정 비용·대기, 배우는 법, 다음 가지, 고유 비기 강화.
    /// 그림 높이: 가장 붐비는 깊이 2가 7줄(72 간격) + 마디 64 + 이름 16 = 512 ≤ 528(그림 안쪽).
    /// </summary>
    public sealed class ArtTrieView
    {
        /// <summary>판 배치(08 8.4절). 화면 배치 상수.</summary>
        const float PickBottomGap = 24f;
        public const float W = 912f, GraphH = 560f, DescH = 184f, PickW = 720f, PickH = 400f, CardSize = 320f;

        readonly GameContext _ctx;
        readonly RectTransform _root, _graph, _desc, _pick;
        readonly UiFocusGroup _focus;
        readonly Dictionary<UiButton, TrieNodeItem> _nodes = new Dictionary<UiButton, TrieNodeItem>();
        /// <summary>섬 표식 버튼(뇌광창처럼 같은 마디에 따로 잠긴 섬 비기를 골라 그 풀이를 본다).</summary>
        readonly Dictionary<UiButton, TrieNodeItem> _justNodes = new Dictionary<UiButton, TrieNodeItem>();
        bool _selectedJust;
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
            // 고르기 창은 판 아래쪽에 붙인다: 첫 줄 마디(시작 비기와 그 이름)가 창 위 끝에 잘리지 않게.
            _pick = UiKit.Node(_root, "Pick", (W - PickW) * 0.5f, GraphH - PickH - PickBottomGap, PickW, PickH);
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
            _justNodes.Clear();
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
                        // 표식 자체가 작은 버튼: 고르면 섬 비기의 풀이(내는 법·효과·배우는 법)를 설명 판에 보인다.
                        var jb = UiKit.Button(_ctx, _graph, null, n.X + size + 2f, n.Y, 32f, 20f, () => Select(item, true), UiButtonStyle.Plain, _focus, "");
                        jb.name = "TrieJust" + n.View.NodeId;
                        UiKit.LabelKey(_ctx, jb.ContentRoot, ArtTrieModel.JustGlyphKey, UiFontId.F12, 0f, 0f, 32f, 20f, jt);
                        UiKit.AttachTooltip(jb.gameObject, () => T(item.View.JustArtId));
                        _justNodes[jb] = n;
                        _buttons.Add(jb);
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
            if (_selected == null) _selectedJust = false;
            ShowDescription(_selected ?? FirstTechnique(m), _selectedJust);

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
                // 커맨드는 지금 키 배치의 키 이름으로("L + ↑ ↑ + K"). 풀이가 없으면 문양만.
                var g = Guide(art);
                UiKit.Label(_ctx, b.ContentRoot, g.Short ?? CommandText(d), UiFontId.F16, 8f, 184f, CardSize - 16f, 24f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                UiKit.Label(_ctx, b.ContentRoot, CostText(d), UiFontId.F16, 8f, 216f, CardSize - 16f, 24f, UiTheme.Chalk, TextAnchor.MiddleCenter);
                // 무엇을 하는 비기인지 한 줄(Data.ArtDescriber — 수치는 arts.json에서).
                string effect = global::Wizard.Solo.Data.ArtDescriber.OneLine(_ctx != null ? _ctx.Names : null, _ctx != null ? _ctx.Content : null, art);
                if (effect != null)
                    UiKit.Label(_ctx, b.ContentRoot, effect, UiFontId.F16, 12f, 244f, CardSize - 24f, 48f, UiTheme.SlateBlue, TextAnchor.UpperCenter, true);
                // 마우스를 올리면 내는 법·대기·다음 가지(카드에 다 들어가지 않는 줄).
                UiKit.AttachTooltip(b.gameObject, () => JoinLines(g.HowTo, g.Gap, g.Cost, g.Next));
                _pickButtons.Add(b);
            }
            if (_pickButtons.Count > 0) _focus?.SetCursor(_pickButtons[0], false);
        }

        /// <summary>마디를 골라 설명 판에 보인다. just = 그 마디의 섬 비기(섬 표식 버튼).</summary>
        public void Select(TrieNodeItem n, bool just = false)
        {
            _selected = n;
            _selectedJust = just && n != null && n.View != null && n.View.JustArtId != null;
            ShowDescription(n, _selectedJust);
        }

        /// <summary>커서가 마디나 섬 표식 위로 오면 그것을 고른다(화면의 OnMenuKey가 부른다). 마디가 아니면 아무것도 안 한다.</summary>
        public void SelectFocused(IUiFocusable f)
        {
            var b = f as UiButton;
            TrieNodeItem n;
            if (b == null) return;
            if (_nodes.TryGetValue(b, out n)) Select(n, false);
            else if (_justNodes.TryGetValue(b, out n)) Select(n, true);
        }

        /// <summary>지금 키 배치의 키 이름(설정의 기본 키, 없으면 기본 배치).</summary>
        string KeyName(LogicalKey k)
        {
            var st = _ctx != null && _ctx.Settings != null ? _ctx.Settings.Current : null;
            int bound = st != null ? KeyLayouts.PrimaryOf(st, k) : 0;
            if (bound <= 0) { var table = KeyLayouts.DefaultPrimary; bound = (int)k < table.Count ? table[(int)k] : 0; }
            return UiKeyLabels.Of(_ctx != null ? _ctx.Names : null, (UnityEngine.InputSystem.Key)bound);
        }

        ArtGuideInfo Guide(string artId) =>
            ArtGuide.Describe(_ctx != null ? _ctx.Names : null, _ctx != null ? _ctx.Content : null, _model != null ? _model.CharId : null, artId, KeyName);

        static string JoinLines(params string[] lines)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var l in lines)
            {
                if (string.IsNullOrEmpty(l)) continue;
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(l);
            }
            return sb.ToString();
        }

        string CommandText(TrieDescription d)
        {
            var s = string.Empty;
            for (int i = 0; i < d.CommandGlyphKeys.Count; i++) s += T(d.CommandGlyphKeys[i]);
            return s;
        }

        string CostText(TrieDescription d) =>
            d.CostAll ? T("str.art_trie.crystal_cost_all") : F("str.art_trie.crystal_value", "n", d.Cost);

        /// <summary>
        /// 설명 판(안쪽 880×168). 왼쪽 단(240): 이름 · 상태 · 난이도 · 결정 비용 · 섬 비기 표시.
        /// 오른쪽 단(624): 내는 법(키 이름) · 효과 한 줄(두 줄까지) · 비용과 대기 · 결정 부족 시 대신 나가는 비기 · 배우는 법 · 다음 가지 · 강화.
        /// </summary>
        void ShowDescription(TrieNodeItem n, bool just = false)
        {
            CampViews.Clear(_desc);
            if (n == null || !n.IsTechnique) return;
            var db = _ctx != null ? _ctx.Content : null;
            just = just && n.View.JustArtId != null;
            var d = ArtTrieModel.Describe(db, n, just);
            var g = Guide(d.ArtId);
            const float LeftW = 240f, RightX = 256f, RightW = 624f, Line = 24f;
            float x = 0f;
            UiKit.Label(_ctx, _desc, T(d.ArtId), UiFontId.F24, x, 0f, LeftW, 32f);
            var state = just ? n.View.JustState : n.State;
            UiKit.Label(_ctx, _desc, T(ArtTrieModel.StateKey(state)), UiFontId.F16, x, 36f, LeftW, Line, UiTheme.SlateBlue);
            string dots = string.Empty;
            for (int i = 0; i < ArtDifficultyMax; i++) dots += T(i < d.Difficulty ? "str.art_trie.dot_on" : "str.art_trie.dot_off");
            UiKit.Label(_ctx, _desc, T("str.art_trie.difficulty") + " " + dots, UiFontId.F16, x, 64f, LeftW, Line);
            UiKit.Label(_ctx, _desc, T("str.art_trie.crystal_cost") + " " + CostText(d), UiFontId.F16, x, 92f, LeftW, Line);
            if (d.JustArtId != null)
            {
                // 같은 마디의 다른 쪽(K 비기 ↔ 섬 비기)을 알려 준다 — 섬 표식을 고르면 그쪽 풀이.
                string other = just ? T(ArtTrieModel.KGlyphKey) + " " + T(n.View.ArtId) : T(ArtTrieModel.JustGlyphKey) + " " + T(d.JustArtId);
                UiKit.Label(_ctx, _desc, other, UiFontId.F16, x, 120f, LeftW, Line, UiTheme.Chalk);
            }

            float y = 0f;
            string howto = g.HowTo ?? (T("str.art_trie.command") + " " + CommandText(d));
            // 간격 줄은 내는 법 옆에 붙이되, 한 줄(오른쪽 단 폭)에 안 들어가면(섬 비기·긴 키 이름) 아래 줄들로 내린다 — 판 밖으로 넘치지 않게.
            string gapBelow = null;
            if (g.Gap != null)
            {
                string joined = howto + T("str.ui.hint.sep") + g.Gap;
                if (UiTextFit.Fits(joined, (int)UiFontId.F16, (int)RightW)) howto = joined;
                else gapBelow = g.Gap;
            }
            UiKit.Label(_ctx, _desc, howto, UiFontId.F16, RightX, y, RightW, Line, UiTheme.Chalk);
            y += Line + 4f;
            if (g.Effect != null)
            {
                UiKit.Label(_ctx, _desc, g.Effect, UiFontId.F16, RightX, y, RightW, 44f, UiTheme.Paper, TextAnchor.UpperLeft, true);
                y += 48f;
            }
            else
            {
                // 풀이 문구가 없는 비기: 예전처럼 효과 두 줄(arts.json 값).
                string lines = string.Empty;
                for (int i = 0; i < d.EffectLines.Count; i++)
                    lines += (i > 0 ? T("str.ui.hint.sep") : string.Empty) + F(d.EffectLines[i].Key, "n", d.EffectLines[i].Value);
                UiKit.Label(_ctx, _desc, T("str.art_trie.effect") + " " + lines, UiFontId.F16, RightX, y, RightW, Line);
                y += Line;
            }
            string rest = JoinLines(g.Cost, gapBelow, Fallback(d, g), g.Unlock, g.Next, g.Upgrade);
            if (rest.Length > 0)
            {
                float h = Mathf.Max(Line, DescH - 16f - y);
                UiKit.Label(_ctx, _desc, rest, UiFontId.F16, RightX, y, RightW, h, UiTheme.SlateBlue, TextAnchor.UpperLeft, true);
            }
        }

        /// <summary>결정이 모자랄 때의 줄: 풀이 문구 + (부모 대체). 풀이 문구가 없으면 예전 글.</summary>
        string Fallback(TrieDescription d, ArtGuideInfo g)
        {
            if (d.FallbackArtId == null) return null;
            string fb = g.Fallback ?? (T("str.art_trie.fallback") + " " + T(d.FallbackArtId));
            if (d.ParentReplace) fb += " " + T("str.art_trie.parent_replace");
            return fb;
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
