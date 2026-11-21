using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>트라이 그림의 마디 하나(08 요약서 8.4절).</summary>
    public sealed class TrieNodeItem
    {
        public ArtTrieNodeView View;
        /// <summary>그림 영역 안 좌표(px, 왼쪽 위 원점). 기술 마디 64×64, 지나가는 점 마디 24×24.</summary>
        public float X, Y;
        public bool IsTechnique => View != null && View.ArtId != null;
        public bool IsUnique;
        /// <summary>이 마디로 들어오는 가지의 입력 문양 열쇠(뿌리는 null).</summary>
        public string EdgeGlyphKey;
        /// <summary>기본(0.3초)과 다른 간격이면 "0.18" 같은 글자. 같으면 null(TV-080).</summary>
        public string GapText;
        public string StateKey;
        public ArtNodeState State;
    }

    /// <summary>설명 판(912×144)의 값.</summary>
    public sealed class TrieDescription
    {
        public string ArtId;
        public string JustArtId;
        public readonly List<string> CommandGlyphKeys = new List<string>();
        public int Difficulty;
        /// <summary>결정 비용. CostAll이면 "전부"(만개).</summary>
        public int Cost;
        public bool CostAll;
        public string FallbackArtId;
        public bool ParentReplace;
        /// <summary>효과 두 줄(열쇠, 값).</summary>
        public readonly List<KeyValuePair<string, string>> EffectLines = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// 비기 트라이 화면의 규칙(08 요약서 8.4절, 04 요약서 2·11절). 트라이 그림 = 커맨드 목록 = 해금 화면.
    /// 마디 설명은 ctx.Rules.Arts.DescribeTrie가 주고, 모델은 배치·문구 열쇠·지금 위치·해금 고르기만 정한다.
    /// </summary>
    public sealed class ArtTrieModel
    {
        /// <summary>그림 배치(08 8.4절): 깊이 0~4 다섯 열, 열 간격 176, 기술 마디 64, 점 마디 24. 화면 배치 상수.</summary>
        public const float ColumnStep = 176f, TechSize = 64f, DotSize = 24f, RowStep = 72f;
        /// <summary>효과 줄 최대 수(08 8.4절 "효과 두 줄"). 자료구조 크기.</summary>
        public const int MaxEffectLines = 2;

        public readonly List<TrieNodeItem> Nodes = new List<TrieNodeItem>();
        public readonly List<string> Candidates = new List<string>();
        public string CharId;
        public int CurrentNodeId = -1;

        public static string StateKey(ArtNodeState s)
        {
            switch (s)
            {
                case ArtNodeState.Locked: return "str.art_trie.state.locked";
                case ArtNodeState.Candidate: return "str.art_trie.state.candidate";
                case ArtNodeState.Current: return "str.art_trie.state.current";
                default: return "str.art_trie.state.unlocked";
            }
        }

        /// <summary>입력 문양 열쇠(앞, 뒤, 옆, 반대쪽 옆). 섬 슬롯은 JustGlyphKey.</summary>
        public static string GlyphKey(CmdDir d)
        {
            switch (d)
            {
                case CmdDir.Forward: return "str.art_trie.dir.forward";
                case CmdDir.Back: return "str.art_trie.dir.back";
                case CmdDir.Left: return "str.art_trie.dir.left";
                default: return "str.art_trie.dir.right";
            }
        }
        public const string JustGlyphKey = "str.art_trie.dir.just";
        public const string KGlyphKey = "str.art_trie.dir.k";

        /// <summary>간격 글자: 기본과 같으면 null, 다르면 "0.18"(TV-080: 0.3 → 없음, 0.18 → "0.18초").</summary>
        public static string GapText(ArtTrieNodeView v) => v == null || v.GapIsDefault ? null : CampFormat.ShortSeconds(v.GapSeconds);

        public static ArtTrieModel Build(IContentDb db, ArtModule arts, string charId, ICollection<string> unlocked, ICollection<string> candidates)
        {
            var m = new ArtTrieModel { CharId = charId };
            if (candidates != null) m.Candidates.AddRange(candidates);
            if (arts == null) return m;
            var views = arts.DescribeTrie(db, charId, unlocked ?? new List<string>(), candidates ?? new List<string>());
            if (views == null) return m;

            // 열 = 깊이, 줄 = 같은 깊이 안의 차례(마디 id는 너비 우선 순).
            var rowOfDepth = new Dictionary<int, int>();
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                int row;
                rowOfDepth.TryGetValue(v.Depth, out row);
                rowOfDepth[v.Depth] = row + 1;
                var art = v.ArtId != null && db != null ? db.Art(v.ArtId) : null;
                var item = new TrieNodeItem
                {
                    View = v,
                    X = v.Depth * ColumnStep,
                    Y = row * RowStep,
                    IsUnique = art != null && art.IsUnique,
                    EdgeGlyphKey = v.Path != null && v.Path.Length > 0 ? GlyphKey(v.Path[v.Path.Length - 1]) : null,
                    GapText = v.Depth >= 2 ? GapText(v) : null,
                    State = v.State,
                    StateKey = StateKey(v.State)
                };
                m.Nodes.Add(item);
            }
            return m;
        }

        /// <summary>
        /// 연습 중 "지금 위치"(Current, 2프레임): 최근 입력 중 끝에서부터 이어진 입력열과 경로가 같은 가장 깊은 마디.
        /// 입력이 없으면 −1.
        /// </summary>
        public void MarkCurrent(IList<RecentInput> recent)
        {
            CurrentNodeId = -1;
            for (int i = 0; i < Nodes.Count; i++)
            {
                var n = Nodes[i];
                if (n.View.State == ArtNodeState.Current) { n.State = ArtNodeState.Unlocked; n.StateKey = StateKey(n.State); }
            }
            if (recent == null || recent.Count == 0) return;
            int start = recent.Count - 1;
            while (start > 0 && recent[start].Chained) start--;
            int best = -1, bestDepth = -1;
            for (int i = 0; i < Nodes.Count; i++)
            {
                var path = Nodes[i].View.Path;
                if (path == null || path.Length == 0 || path.Length > recent.Count - start) continue;
                int off = recent.Count - path.Length;
                if (off < start) continue;
                bool match = true;
                for (int k = 0; k < path.Length && match; k++) match = recent[off + k].Dir == path[k];
                if (match && path.Length > bestDepth) { best = i; bestDepth = path.Length; }
            }
            if (best < 0) return;
            CurrentNodeId = Nodes[best].View.NodeId;
            Nodes[best].State = ArtNodeState.Current;
            Nodes[best].StateKey = StateKey(ArtNodeState.Current);
        }

        public TrieNodeItem FindByArt(string artId)
        {
            for (int i = 0; i < Nodes.Count; i++) if (Nodes[i].View.ArtId == artId || Nodes[i].View.JustArtId == artId) return Nodes[i];
            return null;
        }

        /// <summary>설명 판: 이름, 커맨드, 난이도 점, 결정 비용, 효과 두 줄, 결정이 모자랄 때 나가는 기술(부모 대체).</summary>
        public static TrieDescription Describe(IContentDb db, TrieNodeItem node, bool just = false)
        {
            var d = new TrieDescription();
            if (node == null || node.View == null) return d;
            d.ArtId = just ? node.View.JustArtId : node.View.ArtId;
            d.JustArtId = node.View.JustArtId;
            if (node.View.Path != null) for (int i = 0; i < node.View.Path.Length; i++) d.CommandGlyphKeys.Add(GlyphKey(node.View.Path[i]));
            d.CommandGlyphKeys.Add(just ? JustGlyphKey : KGlyphKey);
            var art = d.ArtId != null && db != null ? db.Art(d.ArtId) : null;
            if (art == null) return d;
            d.Difficulty = art.Difficulty;
            d.CostAll = art.CostIsAll;
            d.Cost = art.CostInt;
            d.FallbackArtId = art.Fallback;
            d.ParentReplace = art.IsUnique;
            EffectLines(art.Effect, d.EffectLines);
            return d;
        }

        /// <summary>효과 줄: arts.json effect의 0이 아닌 값을 앞에서부터 두 줄까지(피해 nT, 갈래, 넉백, 이동, 경직, 무적).</summary>
        public static void EffectLines(ArtEffectDef e, List<KeyValuePair<string, string>> into)
        {
            into.Clear();
            if (e == null) return;
            Add(into, "str.art_trie.effect.damage", e.Damage);
            Add(into, "str.art_trie.effect.side_damage", e.SideDamage);
            if (e.Branches > 0) Add(into, "str.art_trie.effect.branches", e.Branches);
            Add(into, "str.art_trie.effect.knockback", e.Knockback);
            Add(into, "str.art_trie.effect.self_move", e.SelfMove);
            Add(into, "str.art_trie.effect.stagger", e.StaggerSeconds);
            Add(into, "str.art_trie.effect.invuln", e.InvulnSeconds);
        }

        static void Add(List<KeyValuePair<string, string>> into, string key, float v)
        {
            if (into.Count >= MaxEffectLines || v == 0f) return;
            into.Add(new KeyValuePair<string, string>(key, CampFormat.ShortSeconds(v)));
        }

        /// <summary>해금 차례의 카드 수: 후보 2개(1개뿐이면 1개, TV-079).</summary>
        public int PickCardCount => Candidates.Count;
    }
}
