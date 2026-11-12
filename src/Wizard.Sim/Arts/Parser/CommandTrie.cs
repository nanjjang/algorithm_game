using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5, spec 03 5절. 캐릭터별 커맨드 트라이(아호-코라식 실패 링크 + 실패 링크까지 펼친 DFA 전이 표). 불변이다.
    // 판을 시작할 때 캐릭터마다 한 번 만든다(할당은 여기서만). 노드 id 는 BFS 순서(0 = 루트, 자식은 F, B, L, R 순, spec 03 5.3 [임시]).
    //
    // 노드의 값은 노드별 병렬 배열에 두고(판정의 뜨거운 길이 읽는다), 같은 값을 TrieNode 로도 내준다(Node(id), CONTRACTS 3.5).
    // 배열 칸(자식, 펼친 전이, 경로)은 TrieNode 에 없고 Child·Next·Path 로 읽는다(T-52).
    // 해금 여부는 노드에 굽지 않고 판 동안의 UnlockSet 에서 읽는다(spec 03 5.3 [임시]).
    public sealed class CommandTrie
    {
        public const int Root = 0;
        public const int NoNode = -1;
        public const int DirCount = 4;                          // 알파벳 F, B, L, R(spec 03 2.3)
        public const int DirHistorySize = 8;                    // 판정기가 기억하는 최근 방향 입력 수(spec 03 1.2). 자료구조 크기

        private readonly CharacterId _character;
        private readonly ParserSource _source;
        private readonly int _count;
        private readonly int _maxDepth;
        private readonly int[] _child;                          // [node × 4 + dir], 없으면 -1
        private readonly int[] _next;                           // [node × 4 + dir], 항상 유효
        private readonly int[] _fail;
        private readonly int[] _parent;                         // 루트는 -1
        private readonly byte[] _depth;
        private readonly CmdDir[] _path;                        // [node × maxDepth + i], i < depth
        private readonly int[] _ancestor;                       // [node × (maxDepth + 1) + k] = 깊이 k 의 조상(k = depth 면 자기)
        private readonly ArtId[] _kArt;                         // 없으면 None
        private readonly ArtId[] _justArt;                      // 없으면 None
        private readonly long[] _inEdgeGap;                     // 들어오는 간선의 허용 간격. 깊이 0~1 은 -1(없음)
        private readonly bool[] _uniqueFallback;
        private readonly bool[] _isFullBloom;
        private readonly TrieNode[] _nodes;                     // Node(id)가 내주는 값. 위 배열들과 같은 내용이다

        private CommandTrie(CharacterId character, ParserSource source, int count, int maxDepth)
        {
            _character = character;
            _source = source;
            _count = count;
            _maxDepth = maxDepth;
            _child = new int[count * DirCount];
            _next = new int[count * DirCount];
            _fail = new int[count];
            _parent = new int[count];
            _depth = new byte[count];
            _path = new CmdDir[count * Math.Max(maxDepth, 1)];
            _ancestor = new int[count * (maxDepth + 1)];
            _kArt = new ArtId[count];
            _justArt = new ArtId[count];
            _inEdgeGap = new long[count];
            _uniqueFallback = new bool[count];
            _isFullBloom = new bool[count];
            _nodes = new TrieNode[count];
        }

        // CONTRACTS 3.5의 생성 함수. 팩의 arts·tries 절에서 판정기 데이터를 만들어(ParserSourceLoader) 아래 Build 로 넘긴다.
        // 그 절이 팩에 없으면 ContentException 이다(절의 필드와 읽기는 M5-DAT-01 이 채운다, T-52)
        public static CommandTrie Build(CharacterId character, ContentDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            return Build(character, ParserSourceLoader.FromContent(db));
        }

        // spec 03 5.4 BuildTrie. source 의 공용 비기 전부와 이 캐릭터의 고유 비기로 만든다(다른 캐릭터의 고유 노드는 없다)
        public static CommandTrie Build(CharacterId character, ParserSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            ReadOnlySpan<ParserArt> arts = source.All;

            // 노드 수의 상한과 최대 깊이
            int cap = 1;
            int maxDepth = 0;
            foreach (ParserArt a in arts)
            {
                if (!source.IsInTrieOf(a, character))
                {
                    continue;
                }
                for (int i = 0; i < a.PathCount; i++)
                {
                    int len = a.Path(i).Length;
                    cap += len;
                    if (len > maxDepth)
                    {
                        maxDepth = len;
                    }
                }
            }
            if (maxDepth > DirHistorySize)
            {
                throw new SimInvariantException("arts: a command path is longer than the direction history");
            }

            // 1) 만든 순서로 넣는다
            var child = new int[cap * DirCount];
            for (int i = 0; i < child.Length; i++)
            {
                child[i] = NoNode;
            }
            var parent = new int[cap];
            var viaDir = new CmdDir[cap];
            var depth = new byte[cap];
            var kArt = new ArtId[cap];
            var justArt = new ArtId[cap];
            var unique = new bool[cap];
            var bloom = new bool[cap];
            parent[Root] = NoNode;
            int made = 1;
            foreach (ParserArt a in arts)
            {
                if (!source.IsInTrieOf(a, character))
                {
                    continue;
                }
                for (int pi = 0; pi < a.PathCount; pi++)
                {
                    ReadOnlySpan<CmdDir> path = a.Path(pi);
                    int n = Root;
                    foreach (CmdDir d in path)
                    {
                        int slot = n * DirCount + (int)d;
                        if (child[slot] == NoNode)
                        {
                            child[slot] = made;
                            parent[made] = n;
                            viaDir[made] = d;
                            depth[made] = (byte)(depth[n] + 1);
                            made++;
                        }
                        n = child[slot];
                    }
                    if (a.IsJust)
                    {
                        if (justArt[n] != ArtId.None)
                        {
                            throw new SimInvariantException("arts: two arts on one just slot");
                        }
                        justArt[n] = a.Id;
                    }
                    else
                    {
                        if (kArt[n] != ArtId.None)
                        {
                            throw new SimInvariantException("arts: two arts on one K slot");
                        }
                        kArt[n] = a.Id;
                    }
                    if (a.IsUnique)
                    {
                        if (n == Root)
                        {
                            throw new SimInvariantException("arts: a unique art cannot sit on the root");
                        }
                        unique[n] = true;
                    }
                    if (a.Id == ArtId.Bloom)
                    {
                        bloom[n] = true;
                    }
                }
            }
            if (kArt[Root] == ArtId.None)
            {
                throw new SimInvariantException("arts: the root needs a K art (spec 03 R-06)");
            }

            // 2) BFS 순서로 번호를 다시 매긴다(자식은 F, B, L, R 순)
            var order = new int[made];
            var newId = new int[made];
            int head = 0, tail = 0;
            order[tail++] = Root;
            while (head < tail)
            {
                int u = order[head++];
                for (int d = 0; d < DirCount; d++)
                {
                    int v = child[u * DirCount + d];
                    if (v != NoNode)
                    {
                        order[tail++] = v;
                    }
                }
            }
            for (int i = 0; i < made; i++)
            {
                newId[order[i]] = i;
            }

            var t = new CommandTrie(character, source, made, maxDepth);
            for (int i = 0; i < made; i++)
            {
                int old = order[i];
                t._parent[i] = parent[old] == NoNode ? NoNode : newId[parent[old]];
                t._depth[i] = depth[old];
                t._kArt[i] = kArt[old];
                t._justArt[i] = justArt[old];
                t._uniqueFallback[i] = unique[old];
                t._isFullBloom[i] = bloom[old];
                for (int d = 0; d < DirCount; d++)
                {
                    int c = child[old * DirCount + d];
                    t._child[i * DirCount + d] = c == NoNode ? NoNode : newId[c];
                }
                // 경로: 부모의 경로 + 들어온 방향(부모는 BFS 에서 먼저다)
                int dep = t._depth[i];
                if (dep > 0)
                {
                    int p = t._parent[i];
                    for (int k = 0; k < dep - 1; k++)
                    {
                        t._path[i * maxDepth + k] = t._path[p * maxDepth + k];
                    }
                    t._path[i * maxDepth + dep - 1] = viaDir[old];
                }
                // 조상 표
                int w = maxDepth + 1;
                int a = i;
                for (int k = dep; k >= 0; k--)
                {
                    t._ancestor[i * w + k] = a;
                    a = a == Root ? Root : t._parent[a];
                }
                t._inEdgeGap[i] = dep >= 2 ? EdgeGap(t.PathOf(i), character, in source.Timings) : TrieNode.NoGap;
            }

            // 3) 실패 링크(spec 03 5.4: 가장 긴 진접미사 중 트라이에 있는 노드)
            t._fail[Root] = Root;
            for (int i = 0; i < made; i++)
            {
                for (int d = 0; d < DirCount; d++)
                {
                    int v = t._child[i * DirCount + d];
                    if (v == NoNode)
                    {
                        continue;
                    }
                    if (i == Root)
                    {
                        t._fail[v] = Root;
                        continue;
                    }
                    int f = t._fail[i];
                    while (f != Root && t._child[f * DirCount + d] == NoNode)
                    {
                        f = t._fail[f];
                    }
                    int fc = t._child[f * DirCount + d];
                    t._fail[v] = fc != NoNode && fc != v ? fc : Root;
                }
            }

            // 4) DFA 전이(spec 03 5.4). 실패 링크는 더 얕아 BFS 에서 먼저 채워져 있다
            for (int i = 0; i < made; i++)
            {
                for (int d = 0; d < DirCount; d++)
                {
                    int c = t._child[i * DirCount + d];
                    int nx;
                    if (c != NoNode)
                    {
                        nx = c;
                    }
                    else if (i == Root)
                    {
                        nx = Root;
                    }
                    else
                    {
                        nx = t._next[t._fail[i] * DirCount + d];
                    }
                    t._next[i * DirCount + d] = nx;
                }
            }

            // 5) 계약의 마디 값(실패 링크가 다 정해진 뒤)
            for (int i = 0; i < made; i++)
            {
                t._nodes[i] = new TrieNode(i, t._depth[i], t._parent[i], t._fail[i], t._kArt[i], t._justArt[i], t._inEdgeGap[i],
                                           t._uniqueFallback[i], t._isFullBloom[i]);
            }
            return t;
        }

        // spec 03 4.2 EdgeGap. 이 순서 그대로(PY edge_gap). 깊이 2 이상의 노드에만 부른다
        private static long EdgeGap(ReadOnlySpan<CmdDir> path, CharacterId character, in ParserTimings g)
        {
            int n = path.Length;
            CmdDir a = path[n - 2];
            CmdDir b = path[n - 1];
            if (IsFullBloomPath(path))
            {
                return g.GapFullBloomUnits;
            }
            if (IsSide(a) && b == a)
            {
                return g.GapSameSideUnits;
            }
            if (a == CmdDir.Forward && b == CmdDir.Back)
            {
                return g.GapShortUnits;
            }
            if (IsSide(a) && b == CmdDir.Forward)
            {
                return g.GapShortUnits;
            }
            if (a == CmdDir.Forward && IsSide(b))
            {
                return g.GapShortUnits;
            }
            if (IsSide(a) && IsSide(b) && a != b)
            {
                return g.GapShortUnits;
            }
            if (character == CharacterId.Char1 && n == 2 && a == CmdDir.Back && b == CmdDir.Back)
            {
                return g.GapShortUnits;
            }
            return g.GapDefaultUnits;
        }

        // [F,B,B] 또는 [F,B,B,F](spec 03 4.2)
        private static bool IsFullBloomPath(ReadOnlySpan<CmdDir> p)
        {
            if (p.Length != 3 && p.Length != 4)
            {
                return false;
            }
            if (p[0] != CmdDir.Forward || p[1] != CmdDir.Back || p[2] != CmdDir.Back)
            {
                return false;
            }
            return p.Length == 3 || p[3] == CmdDir.Forward;
        }

        private static bool IsSide(CmdDir d) => d == CmdDir.Left || d == CmdDir.Right;

        // ── 계약(CONTRACTS 3.5)

        public int NodeCount => _count;

        public ref readonly TrieNode Node(int id)
        {
            RequireNode(id);
            return ref _nodes[id];
        }

        // 입력당 O(1)
        public int Next(int node, CmdDir dir)
        {
            RequireNode(node);
            return _next[node * DirCount + RequireDir(dir)];
        }

        // ── 노드의 값(spec 03 5.3의 필드)

        public CharacterId Character => _character;

        public ParserSource Source => _source;

        public int MaxDepth => _maxDepth;

        public int Depth(int node)
        {
            RequireNode(node);
            return _depth[node];
        }

        public int Parent(int node)
        {
            RequireNode(node);
            return _parent[node];
        }

        public int Fail(int node)
        {
            RequireNode(node);
            return _fail[node];
        }

        public int Child(int node, CmdDir dir)
        {
            RequireNode(node);
            return _child[node * DirCount + RequireDir(dir)];
        }

        public ArtId KArt(int node)
        {
            RequireNode(node);
            return _kArt[node];
        }

        public ArtId JustArt(int node)
        {
            RequireNode(node);
            return _justArt[node];
        }

        // 깊이 0~1 은 거짓(직전 입력이 없다, spec 03 4.1)
        public bool TryGetInEdgeGap(int node, out long gapUnits)
        {
            RequireNode(node);
            gapUnits = _inEdgeGap[node];
            return gapUnits >= 0;
        }

        public bool UniqueFallback(int node)
        {
            RequireNode(node);
            return _uniqueFallback[node];
        }

        public bool IsFullBloom(int node)
        {
            RequireNode(node);
            return _isFullBloom[node];
        }

        public ReadOnlySpan<CmdDir> Path(int node)
        {
            RequireNode(node);
            return PathOf(node);
        }

        // 루트에서 path 를 따라간 노드. 없으면 -1
        public int Find(ReadOnlySpan<CmdDir> path)
        {
            int n = Root;
            foreach (CmdDir d in path)
            {
                n = _child[n * DirCount + RequireDir(d)];
                if (n == NoNode)
                {
                    return NoNode;
                }
            }
            return n;
        }

        // 노드 경로의 i 번째 간선(1 ≤ i < 깊이, path[i-1] → path[i])의 허용 간격 = 깊이 i+1 조상의 InEdgeGap(spec 03 7.2 GapsOk)
        internal long PathGap(int node, int i) => _inEdgeGap[_ancestor[node * (_maxDepth + 1) + i + 1]];

        internal bool IsValidNode(int node) => node >= 0 && node < _count;

        // 같은 모양의 트라이인가(같은 캐릭터, 같은 노드 수). 스냅숏의 그림자 세계와 예측 세계는 같은 ContentDb 로 만든
        // 다른 인스턴스의 트라이를 가지므로 참조가 아니라 모양으로 본다(CONTRACTS 3.2 "같은 모양의 다른 세계와도 쓸 수 있다")
        internal bool SameShapeAs(CommandTrie other) => other._character == _character && other._count == _count;

        private ReadOnlySpan<CmdDir> PathOf(int node) => new ReadOnlySpan<CmdDir>(_path, node * _maxDepth, _depth[node]);

        private void RequireNode(int node)
        {
            if (node < 0 || node >= _count)
            {
                throw new SimInvariantException("arts: trie node out of range");
            }
        }

        private static int RequireDir(CmdDir dir)
        {
            if (dir > CmdDir.Right)
            {
                throw new SimInvariantException("arts: unknown command direction");
            }
            return (int)dir;
        }
    }
}
