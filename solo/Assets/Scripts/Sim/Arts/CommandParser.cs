using System;
using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Arts
{
    public enum ArtOutcome { None = 0, Fire = 1, Fizzle = 2 }

    /// <summary>K 판정 결과. 04 요약서 5.1절.</summary>
    public struct ArtDecision
    {
        public ArtOutcome Outcome;
        /// <summary>나간 기술(불발이면 고른 기술).</summary>
        public string Art;
        /// <summary>부모 대체 전에 고른 기술.</summary>
        public string SelectedArt;
        public bool ViaFallback;
        public bool IsJust;
        public int CrystalsPaid;
        public int DebtAdded;
        /// <summary>결제 뒤 남은 결정(그림자 계약 계산용).</summary>
        public int CrystalsAfter;
        /// <summary>만개 반복 수(그 밖은 0).</summary>
        public int Repeats;
        public ArtFizzleReason Reason;
        /// <summary>만개 간격 초과: 늦은 입력 번호(2~5).</summary>
        public int LateIndex;
        public long LateByU;
        public long JudgeTimeU;
        public int NodeId;
        /// <summary>섬 창을 넘긴 양(u). 섬 기술이 걸린 노드에서 일반 기술이 나갔을 때만 &gt; 0(연습장 "K가 x초 늦음").</summary>
        public long JustLateByU;

        public bool IsFire => Outcome == ArtOutcome.Fire;
        public bool IsFizzle => Outcome == ArtOutcome.Fizzle;
    }

    /// <summary>결정 지갑. 판정기는 결정·빚을 갖지 않는다(04 요약서 3.1절). 판에서는 Player 모듈로 이어진다.</summary>
    public interface IArtWallet
    {
        int Crystals { get; }
        int Debt { get; }
        /// <summary>외상을 쓸 수 있는 지갑인가(미리보기의 "외상 N" 계산용).</summary>
        bool HasCredit { get; }
        bool CanAfford(int cost, bool allowCredit);
        /// <summary>결정을 쓴다(CanAfford가 참일 때만). 돌려주는 값 = 새로 진 빚.</summary>
        int Spend(int cost, bool allowCredit);
        /// <summary>전부 쓴다(만개). 돌려주는 값 = 쓴 개수.</summary>
        int SpendAll();
    }

    public enum SkillPressResult { Ignored, Buffered, Decided }

    public struct ParserPreview
    {
        /// <summary>아랫줄(또는 한 줄): 지금 K를 누르면 나가는 것.</summary>
        public ArtDecision Main;
        /// <summary>고른 노드에 해금된 섬 기술이 있어 두 줄로 보여 준다.</summary>
        public bool HasJustLine;
        public ArtDecision Just;
    }

    /// <summary>늦음 문구의 초(소수 둘째 자리 올림). 04 요약서 6.1절.</summary>
    public static class LateText
    {
        const int Hundred = 100;
        /// <summary>ceil(lateBy × 100 / 15360). 153u → 1, 307u → 2, 1,075u → 7, 1u → 1.</summary>
        public static int Hundredths(long lateByU)
        {
            if (lateByU <= 0) return 0;
            return (int)((lateByU * Hundred + SimConst.UnitsPerSecond - 1) / SimConst.UnitsPerSecond);
        }
        public static float Seconds(long lateByU) => Hundredths(lateByU) / (float)Hundred;
    }

    /// <summary>
    /// 커맨드 판정기(순수 클래스, 시각은 long u). 04 요약서 3~7절.
    /// 비교는 "경과 &gt; 한도면 실패, 같으면 통과". 결정·빚은 IArtWallet에서 읽고 쓴다.
    /// </summary>
    public sealed class CommandParser
    {
        /// <summary>HUD에 보여 주는 최근 입력 수(04 요약서 6절).</summary>
        public const int RecentShown = 5;

        readonly CommandTrie _trie;
        readonly long[] _dirTimes = new long[CommandTrie.DirHistorySize];
        int _dirCount;
        int _cursor;
        bool _inStance;
        long _cooldownEndU;
        bool _hasBuffered;
        ArtDecision _buffered;
        bool _lastChained;

        readonly RecentInput[] _recent = new RecentInput[RecentShown];
        int _recentCount;
        /// <summary>앞 커맨드가 발동·불발로 끝났다 → 다음 방향 입력이 최근 입력 표시를 새로 시작한다.</summary>
        bool _recentFresh;

        public CommandParser(CommandTrie trie)
        {
            _trie = trie ?? throw new ArgumentNullException(nameof(trie));
        }

        public CommandTrie Trie => _trie;
        ParserTimings T => _trie.Timings;
        public int Cursor => _cursor;
        public TrieNode CursorNode => _trie.Node(_cursor);
        public bool InStance => _inStance;
        /// <summary>재사용 대기 끝 시각(u). 0 = 대기 없음.</summary>
        public long CooldownEndU => _cooldownEndU;
        public bool HasBuffered => _hasBuffered;
        public bool HasDir => _dirCount > 0;
        /// <summary>마지막 방향 입력 시각(u). 없으면 -1.</summary>
        public long LastDirTimeU => _dirCount > 0 ? _dirTimes[_dirCount - 1] : -1;
        public int DirCount => _dirCount;
        /// <summary>방금 넣은 방향 입력이 앞 입력과 이어졌는가(깊이 ≥ 2이고 마지막 간선 간격 통과).</summary>
        public bool LastDirChained => _lastChained;

        // ------------------------------------------------------------ 상태 기계 (3.2절)

        /// <summary>커서 루트 + 기록 비움. 웨이브(판) 시작이면 재사용 대기도 푼다(U-35). 보관된 K는 버리지 않는다(D-13).</summary>
        public void Reset(CommandResetReason reason)
        {
            _cursor = CommandTrie.Root;
            _dirCount = 0;
            _lastChained = false;
            if (reason == CommandResetReason.WaveStart) _cooldownEndU = 0;
            if (reason == CommandResetReason.Fired || reason == CommandResetReason.Fizzled) _recentFresh = true;
            else { _recentCount = 0; _recentFresh = false; }
        }

        /// <summary>자세 여부를 맞춘다. 값이 바뀌는 순간이면 초기화한다(R-11).</summary>
        public void SetStance(bool inStance)
        {
            if (inStance == _inStance) return;
            _inStance = inStance;
            Reset(inStance ? CommandResetReason.StanceEnter : CommandResetReason.StanceExit);
        }

        /// <summary>방향 누름. 자세 밖이면 무시한다(기록 안 함, U-10). 받아들였으면 true.</summary>
        public bool OnDirection(CmdDir dir, long timeU)
        {
            if (!_inStance) return false;
            long prevTime = _recentCount > 0 && !_recentFresh ? _recent[_recentCount - 1].TimeU : -1;
            if (_dirCount > 0 && timeU - _dirTimes[_dirCount - 1] > T.GapDefaultU)
            {
                // R-10: 0.3초 초과면 루트부터. 최근 입력 표시는 끊긴 채로 남긴다.
                _cursor = CommandTrie.Root;
                _dirCount = 0;
            }
            _cursor = _trie.Node(_cursor).Next[(int)dir];
            PushDir(timeU);

            var node = _trie.Node(_cursor);
            bool chained = false;
            if (node.Depth >= 2 && _dirCount >= 2)
                chained = _dirTimes[_dirCount - 1] - _dirTimes[_dirCount - 2] <= node.InEdgeGapU;
            _lastChained = chained;

            if (_recentFresh) { _recentCount = 0; _recentFresh = false; prevTime = -1; }
            var entry = new RecentInput { Dir = dir, Chained = chained, TimeU = timeU };
            if (prevTime >= 0)
            {
                entry.GapU = timeU - prevTime;
                entry.AllowedGapU = node.Depth >= 2 ? node.InEdgeGapU : T.GapDefaultU;
            }
            if (_recentCount == RecentShown)
            {
                for (int i = 1; i < RecentShown; i++) _recent[i - 1] = _recent[i];
                _recentCount--;
            }
            _recent[_recentCount++] = entry;
            return true;
        }

        /// <summary>
        /// K 누름. Decided면 decision이 지금 적용할 결과(발동 또는 불발)다.
        /// Ignored = 재사용 대기 중이라 아무 일도 없다(커서·기록 그대로). Buffered = 누른 시각으로 판정·결제를 끝내 보관했다(7절).
        /// </summary>
        public SkillPressResult OnSkillPress(long timeU, IArtWallet wallet, ICollection<string> unlocked, out ArtDecision decision)
        {
            decision = default;
            if (_hasBuffered) return SkillPressResult.Ignored;
            if (timeU < _cooldownEndU)
            {
                if (timeU < _cooldownEndU - T.KBufferWindowU) return SkillPressResult.Ignored;
                var held = Evaluate(timeU, wallet, unlocked, true);
                _buffered = held;
                _hasBuffered = true;
                Reset(held.IsFire ? CommandResetReason.Fired : CommandResetReason.Fizzled);
                return SkillPressResult.Buffered;
            }
            decision = Evaluate(timeU, wallet, unlocked, true);
            Reset(decision.IsFire ? CommandResetReason.Fired : CommandResetReason.Fizzled);
            // 대기 시작 = 판정 시각. 불발은 대기를 시작하지 않는다(U-05).
            if (decision.IsFire) _cooldownEndU = decision.JudgeTimeU + T.CooldownU;
            return SkillPressResult.Decided;
        }

        /// <summary>보관된 K를 낸다(tickStartU ≥ 대기 끝일 때). 틱 t에서 t × 256으로 부르면 발동 틱 = ceil(대기 끝 / 256).
        /// 발동이면 새 대기는 실제로 나간 시각부터다(D-13).</summary>
        public bool TryTakeBuffered(long tickStartU, out ArtDecision decision)
        {
            decision = default;
            if (!_hasBuffered || tickStartU < _cooldownEndU) return false;
            decision = _buffered;
            _buffered = default;
            _hasBuffered = false;
            _cooldownEndU = decision.IsFire ? tickStartU + T.CooldownU : 0;
            return true;
        }

        /// <summary>보관된 K를 버린다(공격이 허용되지 않는 단계). 버렸으면 true.</summary>
        public bool DropBuffered()
        {
            if (!_hasBuffered) return false;
            _hasBuffered = false;
            _buffered = default;
            return true;
        }

        /// <summary>최근 방향 입력(오래된 것부터, 최대 5개)을 into 뒤에 붙인다.</summary>
        public void GetRecentInputs(List<RecentInput> into)
        {
            for (int i = 0; i < _recentCount; i++) into.Add(_recent[i]);
        }

        /// <summary>재사용 대기 진행(0 = 막 씀, 1 = 준비됨).</summary>
        public float CooldownRatio(long nowU)
        {
            if (_cooldownEndU <= 0 || nowU >= _cooldownEndU || T.CooldownU <= 0) return 1f;
            float r = 1f - (_cooldownEndU - nowU) / (float)T.CooldownU;
            return r < 0f ? 0f : r;
        }

        // ------------------------------------------------------------ 미리보기 (6절)

        /// <summary>"지금 K": 같은 판정을 지갑·상태를 바꾸지 않고 한다. 재사용 대기는 보지 않는다.</summary>
        public ParserPreview Preview(long nowU, IArtWallet wallet, ICollection<string> unlocked)
        {
            var atNow = Evaluate(nowU, wallet, unlocked, false);
            var pv = new ParserPreview { Main = atNow };
            if (atNow.Reason == ArtFizzleReason.FullBloomLate || _dirCount == 0) return pv;
            var just = _trie.Node(atNow.NodeId).JustArt;
            if (just == null || !IsUnlocked(unlocked, just)) return pv;
            // 해금된 섬 기술이 있는 노드는 항상 두 줄(U-29): 윗줄 = 마지막 방향 시각의 판정, 아랫줄 = 섬 창을 넘긴 시각의 판정.
            long last = _dirTimes[_dirCount - 1];
            long pastWindow = last + T.JustWindowU + 1;
            pv.HasJustLine = true;
            pv.Just = Evaluate(last, wallet, unlocked, false);
            pv.Main = nowU >= pastWindow ? atNow : Evaluate(pastWindow, wallet, unlocked, false);
            return pv;
        }

        // ------------------------------------------------------------ 판정 (4절)

        public static bool IsUnlocked(ICollection<string> unlocked, ArtDef art)
        {
            if (art == null) return false;
            if (art.Unlock != null && art.Unlock.AtStart) return true;
            return unlocked != null && unlocked.Contains(art.Id);
        }

        ArtDecision Evaluate(long t, IArtWallet w, ICollection<string> unlocked, bool commit)
        {
            TrieNode node;
            if (!_inStance) node = _trie.Node(CommandTrie.Root);                                        // R-06
            else if (_dirCount > 0 && t - _dirTimes[_dirCount - 1] > T.GapDefaultU) node = _trie.Node(CommandTrie.Root);   // R-10
            else node = _trie.Node(_cursor);
            bool hasDir = _dirCount > 0;

            // 실패 링크를 따라 "기술이 있고, 해금됐고, 간격을 통과한" 첫 노드. 루트의 기술은 언제나 쓸 수 있다.
            while (node.Id != CommandTrie.Root)
            {
                if (node.KArt != null && IsUnlocked(unlocked, node.KArt))
                {
                    if (node.IsFullBloom)
                    {
                        int lateIndex;
                        long lateBy;
                        if (!CheckFullBloom(node, t, out lateIndex, out lateBy))
                        {
                            // 유일한 예외: 실패 링크로 가지 않고 불발(결정 소모 없음).
                            return new ArtDecision
                            {
                                Outcome = ArtOutcome.Fizzle, Art = node.KArt.Id, SelectedArt = node.KArt.Id,
                                CrystalsAfter = w.Crystals, Reason = ArtFizzleReason.FullBloomLate,
                                LateIndex = lateIndex, LateByU = lateBy, JudgeTimeU = t, NodeId = node.Id
                            };
                        }
                        break;
                    }
                    if (GapsOk(node)) break;
                }
                node = _trie.Node(node.Fail);
            }
            var art = node.KArt;

            bool just = false;
            long justLateBy = 0;
            if (node.JustArt != null && IsUnlocked(unlocked, node.JustArt) && hasDir)
            {
                long since = t - _dirTimes[_dirCount - 1];
                if (since >= 0 && since <= T.JustWindowU) { art = node.JustArt; just = true; }
                else if (since > T.JustWindowU) justLateBy = since - T.JustWindowU;
            }
            return Pay(art, node, just, justLateBy, t, w, unlocked, commit);
        }

        /// <summary>결제(5절): 외상 먼저 → 고유 비기만 부모 기술로 한 번 → 그 밖은 불발.</summary>
        ArtDecision Pay(ArtDef art, TrieNode node, bool just, long justLateBy, long t, IArtWallet w, ICollection<string> unlocked, bool commit)
        {
            var d = new ArtDecision
            {
                Art = art != null ? art.Id : null, SelectedArt = art != null ? art.Id : null,
                IsJust = just, JustLateByU = justLateBy, JudgeTimeU = t, NodeId = node.Id
            };
            if (art != null && TryPay(art, w, commit, ref d))
            {
                d.Outcome = ArtOutcome.Fire;
                return d;
            }
            if (node.UniqueFallback && node.Parent >= 0)
            {
                var parentArt = _trie.Node(node.Parent).KArt;
                // 부모가 해금되지 않았으면 불발(D-15 [임시]).
                if (parentArt != null && IsUnlocked(unlocked, parentArt) && TryPay(parentArt, w, commit, ref d))
                {
                    d.Outcome = ArtOutcome.Fire;
                    d.Art = parentArt.Id;
                    d.ViaFallback = true;
                    d.IsJust = false;
                    d.JustLateByU = 0;
                    return d;
                }
            }
            d.Outcome = ArtOutcome.Fizzle;
            d.Reason = ArtFizzleReason.NoCrystal;
            d.CrystalsPaid = 0;
            d.DebtAdded = 0;
            d.Repeats = 0;
            d.CrystalsAfter = w.Crystals;
            return d;
        }

        static bool TryPay(ArtDef art, IArtWallet w, bool commit, ref ArtDecision d)
        {
            int before = w.Crystals;
            if (art.CostIsAll)
            {
                // 만개: 결정 전부(최소 min_cost), 외상 불가. 반복 수 = 소모 직전 결정 수.
                if (!w.CanAfford(art.MinCost, false)) return false;
                int spent = commit ? w.SpendAll() : before;
                d.Repeats = spent < art.MinCost ? art.MinCost : spent;
                d.CrystalsPaid = spent;
                d.DebtAdded = 0;
                d.CrystalsAfter = commit ? w.Crystals : 0;
                return true;
            }
            int cost = art.CostInt;
            if (!w.CanAfford(cost, art.Credit)) return false;
            d.Repeats = 0;
            if (commit)
            {
                d.DebtAdded = w.Spend(cost, art.Credit);
                d.CrystalsAfter = w.Crystals;
                d.CrystalsPaid = before - d.CrystalsAfter;
            }
            else
            {
                int use = before < cost ? before : cost;
                d.CrystalsPaid = use;
                d.DebtAdded = w.HasCredit && art.Credit ? cost - use : 0;
                d.CrystalsAfter = before - use;
            }
            return true;
        }

        /// <summary>깊이 k 노드: 최근 k개 방향 시각을 경로 위 노드의 InEdgeGap으로 본다. 마지막 방향 → K는 0.3초 초기화가 이미 보장한다.</summary>
        bool GapsOk(TrieNode node)
        {
            int k = node.Depth;
            if (k <= 1) return true;
            if (k > _dirCount) return false;
            int first = _dirCount - k;
            for (int i = 1; i < k; i++)
                if (_dirTimes[first + i] - _dirTimes[first + i - 1] > _trie.PathGapU(node, i)) return false;
            return true;
        }

        /// <summary>만개 노드만: 경로의 간격 전부와 마지막 방향 → K를 만개 간격으로 다시 본다. 떨어지면 몇 번째 입력(2~5)이 얼마나 늦었나.</summary>
        bool CheckFullBloom(TrieNode node, long tK, out int lateIndex, out long lateBy)
        {
            lateIndex = 0;
            lateBy = 0;
            int k = node.Depth;
            if (k > _dirCount) return false;
            int first = _dirCount - k;
            long limit = T.GapFullBloomU;
            for (int i = 1; i <= k; i++)
            {
                long cur = i < k ? _dirTimes[first + i] : tK;
                long gap = cur - _dirTimes[first + i - 1];
                if (gap > limit)
                {
                    lateIndex = i + 1;
                    lateBy = gap - limit;
                    return false;
                }
            }
            return true;
        }

        void PushDir(long timeU)
        {
            if (_dirCount == CommandTrie.DirHistorySize)
            {
                for (int i = 1; i < CommandTrie.DirHistorySize; i++) _dirTimes[i - 1] = _dirTimes[i];
                _dirTimes[CommandTrie.DirHistorySize - 1] = timeU;
                return;
            }
            _dirTimes[_dirCount++] = timeU;
        }
    }
}
