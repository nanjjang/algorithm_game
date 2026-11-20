using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Arts
{
    /// <summary>판정기 상수(tries.json parser 절)를 입력 단위 u로 바꾼 것. 04 요약서 0.3절.</summary>
    public sealed class ParserTimings
    {
        public long JustWindowU;
        public long GapFullBloomU;
        public long GapShortU;
        public long GapSameSideU;
        public long GapDefaultU;
        public long CooldownU;
        public long KBufferWindowU;
        public float GapFullBloomSeconds, GapShortSeconds, GapSameSideSeconds, GapDefaultSeconds;
        public int MaxDebt;

        public static ParserTimings From(ParserDef d)
        {
            d = d ?? new ParserDef();
            return new ParserTimings
            {
                JustWindowU = SimConst.Units(d.JustWindowSeconds),
                GapFullBloomU = SimConst.Units(d.GapFullBloomSeconds),
                GapShortU = SimConst.Units(d.GapShortSeconds),
                GapSameSideU = SimConst.Units(d.GapSameSideSeconds),
                GapDefaultU = SimConst.Units(d.GapDefaultSeconds),
                CooldownU = SimConst.Units(d.ArtCooldownSeconds),
                KBufferWindowU = SimConst.Units(d.KBufferWindowSeconds),
                GapFullBloomSeconds = d.GapFullBloomSeconds,
                GapShortSeconds = d.GapShortSeconds,
                GapSameSideSeconds = d.GapSameSideSeconds,
                GapDefaultSeconds = d.GapDefaultSeconds,
                MaxDebt = d.MaxDebt
            };
        }
    }

    /// <summary>트라이 노드. 04 요약서 2.3절. 해금 여부는 굽지 않는다(판 중 해금이 바뀌어도 다시 만들지 않는다).</summary>
    public sealed class TrieNode
    {
        /// <summary>BFS 순 번호(0 = 루트, 자식은 F, B, L, R 순).</summary>
        public int Id;
        public int Depth;
        public int Parent = -1;
        public int Fail;
        public CmdDir[] Path = new CmdDir[0];
        /// <summary>자식 노드 번호(없으면 -1).</summary>
        public readonly int[] Child = { -1, -1, -1, -1 };
        /// <summary>실패 링크까지 펼친 전이(항상 유효).</summary>
        public readonly int[] Next = new int[CommandTrie.DirCount];
        /// <summary>깊이 d(0..Depth)의 조상 노드 번호(Depth면 자기).</summary>
        public int[] Ancestors = new int[0];
        public ArtDef KArt;
        public ArtDef JustArt;
        /// <summary>들어오는 간선의 허용 간격(u). 깊이 0~1은 -1(없음).</summary>
        public long InEdgeGapU = -1;
        public float InEdgeGapSeconds;
        public bool UniqueFallback;
        public bool IsFullBloom;

        public string KArtId => KArt != null ? KArt.Id : null;
        public string JustArtId => JustArt != null ? JustArt.Id : null;
    }

    /// <summary>
    /// 캐릭터별 커맨드 트라이(공용 10 + 고유 1). 판 시작 때 한 번 만든다. 04 요약서 2절.
    /// 생성은 아호-코라식(2.5절), 간선 간격은 2.4절의 순서 그대로.
    /// </summary>
    public sealed class CommandTrie
    {
        public const int DirCount = 4;
        public const int Root = 0;
        /// <summary>최근 방향 입력 기록 크기(자료구조 크기. 04 요약서 0.3절).</summary>
        public const int DirHistorySize = 8;

        public readonly string CharId;
        public readonly ParserTimings Timings;
        readonly TrieNode[] _nodes;

        CommandTrie(string charId, ParserTimings timings, TrieNode[] nodes)
        {
            CharId = charId;
            Timings = timings;
            _nodes = nodes;
        }

        public int Count => _nodes.Length;
        public TrieNode Node(int id) => _nodes[id];
        public IReadOnlyList<TrieNode> Nodes => _nodes;

        /// <summary>경로로 노드를 찾는다(자식 간선만 따라간다). 없으면 null.</summary>
        public TrieNode Find(params CmdDir[] path)
        {
            var n = _nodes[Root];
            if (path == null) return n;
            for (int i = 0; i < path.Length; i++)
            {
                int c = n.Child[(int)path[i]];
                if (c < 0) return null;
                n = _nodes[c];
            }
            return n;
        }

        /// <summary>노드 경로 위에서 i번째와 i+1번째 입력 사이(1 ≤ i &lt; Depth)에 허용된 간격(u) = 깊이 i+1 조상의 InEdgeGap.</summary>
        public long PathGapU(TrieNode node, int i) => _nodes[node.Ancestors[i + 1]].InEdgeGapU;

        public static bool TryParseDir(string s, out CmdDir dir)
        {
            dir = CmdDir.Forward;
            switch (s)
            {
                case "F": dir = CmdDir.Forward; return true;
                case "B": dir = CmdDir.Back; return true;
                case "L": dir = CmdDir.Left; return true;
                case "R": dir = CmdDir.Right; return true;
                default: return false;
            }
        }

        /// <summary>그 캐릭터의 트라이에 넣을 비기 정의(tries.json characters[].arts. 없으면 공용 전부 + 자기 고유).</summary>
        public static List<ArtDef> ArtsOf(IContentDb db, string charId)
        {
            var arts = new List<ArtDef>();
            if (db == null) return arts;
            var ids = db.TrieArts(charId);
            if (ids != null)
            {
                for (int i = 0; i < ids.Count; i++)
                {
                    var a = db.Art(ids[i]);
                    if (a != null) arts.Add(a);
                }
                return arts;
            }
            var all = db.Arts;
            if (all == null) return arts;
            for (int i = 0; i < all.Count; i++)
                if (!all[i].IsUnique || all[i].Owner == charId) arts.Add(all[i]);
            return arts;
        }

        public static CommandTrie Build(IContentDb db, string charId)
        {
            var timings = ParserTimings.From(db != null && db.Tries != null ? db.Tries.Parser : null);
            return Build(charId, ArtsOf(db, charId), timings);
        }

        public static CommandTrie Build(string charId, IReadOnlyList<ArtDef> arts, ParserTimings timings)
        {
            // 1. 경로를 끼워 넣는다(만든 순서의 임시 번호).
            var raw = new List<TrieNode> { new TrieNode() };
            for (int a = 0; a < arts.Count; a++)
            {
                var art = arts[a];
                if (art.Paths == null) continue;
                for (int pi = 0; pi < art.Paths.Count; pi++)
                {
                    var path = art.Paths[pi];
                    int n = 0;
                    bool ok = true;
                    for (int k = 0; path != null && k < path.Count; k++)
                    {
                        CmdDir d;
                        if (!TryParseDir(path[k], out d)) { ok = false; break; }
                        int c = raw[n].Child[(int)d];
                        if (c < 0)
                        {
                            var parent = raw[n];
                            var child = new TrieNode { Depth = parent.Depth + 1, Parent = n };
                            child.Path = new CmdDir[parent.Path.Length + 1];
                            parent.Path.CopyTo(child.Path, 0);
                            child.Path[parent.Path.Length] = d;
                            c = raw.Count;
                            raw.Add(child);
                            parent.Child[(int)d] = c;
                        }
                        n = c;
                    }
                    if (!ok) continue;
                    var node = raw[n];
                    if (art.Just) node.JustArt = art; else node.KArt = art;
                    if (art.IsUnique) node.UniqueFallback = true;
                    if (art.Id == Ids.Art.Bloom) node.IsFullBloom = true;
                }
            }

            // 2. BFS 순으로 번호를 다시 매긴다(자식은 F, B, L, R 순).
            var order = new List<int> { 0 };
            for (int head = 0; head < order.Count; head++)
            {
                var u = raw[order[head]];
                for (int d = 0; d < DirCount; d++)
                    if (u.Child[d] >= 0) order.Add(u.Child[d]);
            }
            var newId = new int[raw.Count];
            for (int i = 0; i < order.Count; i++) newId[order[i]] = i;
            var nodes = new TrieNode[order.Count];
            for (int i = 0; i < order.Count; i++)
            {
                var n = raw[order[i]];
                n.Id = i;
                n.Parent = n.Parent < 0 ? -1 : newId[n.Parent];
                for (int d = 0; d < DirCount; d++)
                    if (n.Child[d] >= 0) n.Child[d] = newId[n.Child[d]];
                nodes[i] = n;
            }
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                n.Ancestors = new int[n.Depth + 1];
                int cur = i;
                for (int d = n.Depth; d >= 0; d--) { n.Ancestors[d] = cur; cur = nodes[cur].Parent; }
                if (n.Depth >= 2)
                {
                    float seconds;
                    n.InEdgeGapU = EdgeGapU(n.Path, charId, timings, out seconds);
                    n.InEdgeGapSeconds = seconds;
                }
            }

            // 3. 실패 링크(BFS). 번호가 BFS 순이라 번호 오름차순으로 돌면 된다.
            var root = nodes[Root];
            root.Fail = Root;
            for (int i = 1; i < nodes.Length; i++)
            {
                var v = nodes[i];
                if (v.Depth == 1) { v.Fail = Root; continue; }
                int d = (int)v.Path[v.Path.Length - 1];
                int f = nodes[v.Parent].Fail;
                while (f != Root && nodes[f].Child[d] < 0) f = nodes[f].Fail;
                int cand = nodes[f].Child[d];
                v.Fail = cand >= 0 && cand != i ? cand : Root;
            }

            // 4. DFA 전이.
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                for (int d = 0; d < DirCount; d++)
                {
                    if (n.Child[d] >= 0) n.Next[d] = n.Child[d];
                    else if (i == Root) n.Next[d] = Root;
                    else n.Next[d] = nodes[n.Fail].Next[d];
                }
            }
            return new CommandTrie(charId, timings, nodes);
        }

        static bool IsSide(CmdDir d) => d == CmdDir.Left || d == CmdDir.Right;

        /// <summary>간선 간격 함수. 04 요약서 2.4절 — 이 순서 그대로.</summary>
        public static long EdgeGapU(CmdDir[] path, string charId, ParserTimings t, out float seconds)
        {
            int k = path.Length;
            CmdDir a = path[k - 2], b = path[k - 1];
            bool fbb = k >= 3 && path[0] == CmdDir.Forward && path[1] == CmdDir.Back && path[2] == CmdDir.Back;
            if ((k == 3 && fbb) || (k == 4 && fbb && path[3] == CmdDir.Forward)) { seconds = t.GapFullBloomSeconds; return t.GapFullBloomU; }
            if (IsSide(a) && b == a) { seconds = t.GapSameSideSeconds; return t.GapSameSideU; }
            if (a == CmdDir.Forward && b == CmdDir.Back) { seconds = t.GapShortSeconds; return t.GapShortU; }
            if (IsSide(a) && b == CmdDir.Forward) { seconds = t.GapShortSeconds; return t.GapShortU; }
            if (a == CmdDir.Forward && IsSide(b)) { seconds = t.GapShortSeconds; return t.GapShortU; }
            if (IsSide(a) && IsSide(b) && a != b) { seconds = t.GapShortSeconds; return t.GapShortU; }
            if (charId == Ids.Char.C1 && k == 2 && a == CmdDir.Back && b == CmdDir.Back) { seconds = t.GapShortSeconds; return t.GapShortU; }
            seconds = t.GapDefaultSeconds;
            return t.GapDefaultU;
        }
    }
}
