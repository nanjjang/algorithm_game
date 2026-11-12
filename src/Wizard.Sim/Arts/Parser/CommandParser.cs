using System;
using Wizard.Content;
using Wizard.Core;

namespace Wizard.Sim.Arts
{
    // CONTRACTS 3.5, spec 03 6~7절(상태 기계, 방향 입력, K 판정, 결제)과 10절(재사용 대기, K 보관).
    // 플레이어 한 명의 판정기. 시각은 전부 입력 시각(틱 × 256 + 오프셋)이고 간격은 정수로 비교한다(경과 ≤ 한도면 통과).
    // 상태(커서, 방향 시각 8개, 자세, 대기 끝, 보관된 K)는 WriteState/ReadState/CopyFrom 으로 스냅숏·직렬화·해시에 들어간다.
    // 순번(Seq)은 받지도 넣지도 않는다. 트라이는 판 동안 바뀌지 않는 참조라 상태가 아니다.
    // 방향 입력·K·초기화·미리보기 경로에서 할당하지 않는다. 결정과 빚은 지갑(ICrystalWallet)이 갖는다(spec 03 6.1 [임시]).
    public sealed class CommandParser : ICommandParser
    {
        private const long NoCooldown = 0;                  // spec 03 6.1: cooldownEnd 0 = 대기 없음

        private readonly CommandTrie _trie;
        private readonly long[] _dirTimes = new long[CommandTrie.DirHistorySize];   // 오래된 것 → 최근, 앞쪽 _dirCount 개만 뜻이 있다

        // 상태
        private int _cursor;
        private int _dirCount;
        private bool _inStance;
        private long _cooldownEnd;
        private bool _hasBuffered;
        private ArtDecision _buffered;

        public CommandParser(CommandTrie trie)
        {
            _trie = trie ?? throw new ArgumentNullException(nameof(trie));
        }

        public CommandTrie Trie => _trie;

        public int Cursor => _cursor;

        public bool InStance => _inStance;

        public long CooldownEnd => _cooldownEnd;

        public bool HasBuffered => _hasBuffered;

        public int DirCount => _dirCount;

        // 최근 방향 입력 시각. 0 = 가장 최근
        public long RecentDirTime(int back)
        {
            if (back < 0 || back >= _dirCount)
            {
                throw new SimInvariantException("arts: no such direction input in the history");
            }
            return _dirTimes[_dirCount - 1 - back];
        }

        private bool HasDir => _dirCount > 0;

        private long LastDirTime => _dirTimes[_dirCount - 1];

        private ref readonly ParserTimings T => ref _trie.Source.Timings;

        // ── 상태 기계(spec 03 6.2)

        // 커서를 루트로, 입력 기록을 비운다. 판·웨이브·결투 라운드 시작이면 재사용 대기도 해제한다(spec 03 6.2, U-35 [임시]).
        // 대기 중에 보관된 K 는 버리지 않는다(결제가 끝난 결과다): 대기가 풀렸으므로 다음 TryTakeBuffered 에서 나간다 [임시, Q-11]
        public void Reset(CommandResetReason reason)
        {
            _cursor = CommandTrie.Root;
            _dirCount = 0;
            if (reason == CommandResetReason.WaveStart || reason == CommandResetReason.RoundStart)
            {
                _cooldownEnd = NoCooldown;
            }
        }

        // 자세 진입·해제(inStance 값이 바뀌는 순간)는 커맨드를 처음으로 돌린다(R-11). 반전은 부르는 쪽이 Reset(Reverse)로 알린다.
        // 시각은 판정에 쓰지 않는다(초기화는 시각과 무관하다)
        public void OnStanceChanged(bool inStance, long time)
        {
            if (inStance == _inStance)
            {
                return;
            }
            _inStance = inStance;
            Reset(inStance ? CommandResetReason.StanceEnter : CommandResetReason.StanceExit);
        }

        // spec 03 7.1. 자세 밖이면 무시(R-04, U-10)
        public void OnDirection(CmdDir dir, long time)
        {
            if (dir > CmdDir.Right)
            {
                throw new SimInvariantException("arts: unknown command direction");
            }
            if (!_inStance)
            {
                return;
            }
            if (HasDir && unchecked(time - LastDirTime) > T.GapDefaultUnits)
            {
                Reset(CommandResetReason.Timeout);                     // R-10
            }
            _cursor = _trie.Next(_cursor, dir);
            PushDir(time);
        }

        // 방향 입력이 이어지는 중인가(표시 이벤트 cmd.dir_input 의 chained, spec 03 7.1): 커서 깊이 ≥ 2 이고 마지막 간선이 통과
        public bool LastDirChained
        {
            get
            {
                int depth = _trie.Depth(_cursor);
                if (depth < 2 || _dirCount < 2)
                {
                    return false;
                }
                long gap = unchecked(_dirTimes[_dirCount - 1] - _dirTimes[_dirCount - 2]);
                return gap <= _trie.PathGap(_cursor, depth - 1);
            }
        }

        // K 누름(spec 03 6.2, 10절). 참이면 decision 이 지금 적용할 결과(발동 또는 불발)다.
        // 거짓: 재사용 대기 중이라 무시했거나(커서·기록 그대로, U-02·U-04), 보관 창 안이라 결과를 보관했다(TryTakeBuffered 가 낸다)
        public bool OnSkillPress(long time, ICrystalWallet wallet, in UnlockSet unlocks, out ArtDecision decision)
        {
            if (wallet == null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }
            decision = default;
            if (_hasBuffered)
            {
                // 보관 중의 K 는 무시한다(U-04). 보관된 K 는 5단계(TryTakeBuffered)에서 나가므로, 대기 끝과 같은 틱의 2단계에
                // 대기 끝 뒤의 시각으로 들어온 K 도 아직 보관 중이다: 여기서 판정하면 뒤에 누른 K 가 먼저 나가고 보관된 K 가 한 대기만큼 밀린다
                return false;
            }
            if (time < _cooldownEnd)
            {
                if (time < unchecked(_cooldownEnd - T.KBufferWindowUnits))
                {
                    return false;
                }
                // 보관 창 안: 누른 시각으로 선택과 결제를 끝내고 결과를 보관한다(U-03). 대기는 보관을 낼 때 다시 시작한다
                ArtDecision held = Evaluate(time, wallet, in unlocks, true);
                _buffered = held;
                _hasBuffered = true;
                Reset(held.IsFire ? CommandResetReason.Fired : CommandResetReason.Fizzled);
                return false;
            }
            decision = Evaluate(time, wallet, in unlocks, true);
            Reset(decision.IsFire ? CommandResetReason.Fired : CommandResetReason.Fizzled);
            if (decision.IsFire)
            {
                _cooldownEnd = unchecked(decision.JudgeTime + T.ArtCooldownUnits);   // 대기 시작 = 판정 시각(U-06). 불발은 대기 없음(U-05)
            }
            return true;
        }

        // 재사용 대기 끝에 나가는 보관된 K(spec 03 6.2 마지막 줄, 10절). tickStartTime ≥ 대기 끝이면 낸다.
        // 틱 T 에서 tickStartTime = T × 256 으로 부르면 발동 틱이 ceil(대기 끝 ÷ 256)이 된다 [임시, U-03].
        // 발동이면 새 대기는 실제로 나간 시각(tickStartTime)부터다: 연속 발동 사이가 대기(0.4초)보다 짧아지지 않게 한다 [임시, Q-10]
        public bool TryTakeBuffered(long tickStartTime, out ArtDecision decision)
        {
            decision = default;
            if (!_hasBuffered || tickStartTime < _cooldownEnd)
            {
                return false;
            }
            decision = _buffered;
            _buffered = default;
            _hasBuffered = false;
            _cooldownEnd = decision.IsFire ? unchecked(tickStartTime + T.ArtCooldownUnits) : NoCooldown;
            return true;
        }

        // HUD "지금 K =" 의 판정(spec 03 11절): 같은 판정 함수를 지갑·상태를 바꾸지 않고 부른다. 재사용 대기는 보지 않는다.
        // 고른 노드에 해금된 섬 기술이 있으면 항상 두 줄이다(U-29): 윗줄은 마지막 방향 시각의 판정,
        // 아랫줄은 섬 창을 넘긴 시각의 판정. 그 밖은 now 의 판정 한 줄(만개 간격 초과의 불발 포함, U-30)
        public CommandPreview Preview(long now, ICrystalWallet wallet, in UnlockSet unlocks)
        {
            if (wallet == null)
            {
                throw new ArgumentNullException(nameof(wallet));
            }
            ArtDecision atNow = Evaluate(now, wallet, in unlocks, false);
            if (atNow.Reason == FizzleReason.FullBloomLate || !HasDir)
            {
                return new CommandPreview(in atNow, false, default);
            }
            ArtId justArt = _trie.JustArt(atNow.NodeId);
            if (justArt == ArtId.None || !IsUnlocked(in unlocks, justArt))
            {
                return new CommandPreview(in atNow, false, default);
            }
            long last = LastDirTime;
            ArtDecision just = Evaluate(last, wallet, in unlocks, false);
            long pastWindow = unchecked(last + T.JustWindowUnits + 1);
            ArtDecision normal = now >= pastWindow ? atNow : Evaluate(pastWindow, wallet, in unlocks, false);
            return new CommandPreview(in normal, true, in just);
        }

        // ── 판정(spec 03 7.2~7.3)

        private ArtDecision Evaluate(long t, ICrystalWallet w, in UnlockSet u, bool commit)
        {
            int node;
            if (!_inStance)
            {
                node = CommandTrie.Root;                               // R-06: 자세 밖 K 는 커서와 무관하게 루트
            }
            else if (HasDir && unchecked(t - LastDirTime) > T.GapDefaultUnits)
            {
                node = CommandTrie.Root;                               // R-10. 커서는 판정 뒤 Reset 으로 비워진다
            }
            else
            {
                node = _cursor;
            }
            // 루트로 간 경우(자세 밖, 0.3초 초기화)는 루트에 섬 기술이 없으므로 아래 섬 검사에 걸리지 않는다
            bool hasDir = HasDir;

            // 실패 링크를 따라 "기술이 있고, 해금됐고, 간격을 통과한" 첫 노드. 루트의 기술은 언제나 쓸 수 있다(spec 03 7.2, R-06)
            while (node != CommandTrie.Root)
            {
                ArtId k = _trie.KArt(node);
                if (k != ArtId.None && IsUnlocked(in u, k))
                {
                    if (_trie.IsFullBloom(node))
                    {
                        if (!CheckFullBloom(node, t, out byte lateIndex, out long lateBy))
                        {
                            // 실패 링크로 가지 않고 불발(결정 소모 없음, spec 03 7.2)
                            return new ArtDecision(ArtOutcome.Fizzle, k, k, false, false, 0, 0, ClampByte(w.Crystals), 0,
                                                  FizzleReason.FullBloomLate, lateIndex, lateBy, t, node, 0);
                        }
                        break;
                    }
                    if (GapsOk(node))
                    {
                        break;
                    }
                }
                node = _trie.Fail(node);
            }
            ArtId art = _trie.KArt(node);

            // 섬: 고른 노드에 해금된 섬 기술이 있고 마지막 방향 입력에서 0.07초 안(경계 포함, spec 03 7.2)
            bool just = false;
            long justLateBy = 0;
            ArtId justArt = _trie.JustArt(node);
            if (justArt != ArtId.None && IsUnlocked(in u, justArt) && hasDir)
            {
                long since = unchecked(t - LastDirTime);
                if (since >= 0 && since <= T.JustWindowUnits)
                {
                    art = justArt;
                    just = true;
                }
                else if (since > T.JustWindowUnits)
                {
                    justLateBy = unchecked(since - T.JustWindowUnits);
                }
            }
            return Pay(art, node, just, justLateBy, t, w, in u, commit);
        }

        // spec 03 7.3. 외상 먼저, 그다음 고유 비기만 부모 기술로 한 번, 그 밖은 불발
        private ArtDecision Pay(ArtId art, int node, bool just, long justLateBy, long t, ICrystalWallet w, in UnlockSet u, bool commit)
        {
            int crystals = w.Crystals;
            int debt = w.Debt;
            bool credit = w.HasCredit;
            if (crystals < 0 || debt < 0)
            {
                throw new SimInvariantException("arts: wallet holds a negative count");
            }
            if (TryPay(art, crystals, debt, credit, out int c1, out int d1, out int repeats))
            {
                return Fire(art, art, false, just, crystals, debt, c1, d1, repeats, t, node, justLateBy, w, commit);
            }
            if (_trie.UniqueFallback(node))
            {
                ArtId parentArt = _trie.KArt(_trie.Parent(node));
                // 부모가 해금되지 않았으면 불발(spec 03 7.3, 19절 C-02 [임시])
                if (parentArt != ArtId.None && IsUnlocked(in u, parentArt)
                    && TryPay(parentArt, crystals, debt, credit, out int c2, out int d2, out int r2))
                {
                    return Fire(parentArt, art, true, false, crystals, debt, c2, d2, r2, t, node, 0, w, commit);
                }
            }
            return new ArtDecision(ArtOutcome.Fizzle, art, art, false, just, 0, 0, ClampByte(crystals), 0,
                                  FizzleReason.NoCrystal, 0, 0, t, node, justLateBy);
        }

        private ArtDecision Fire(ArtId art, ArtId selected, bool via, bool just, int crystalsBefore, int debtBefore,
                                int crystalsAfter, int debtAfter, int repeats, long t, int node, long justLateBy,
                                ICrystalWallet w, bool commit)
        {
            if (commit)
            {
                if (crystalsAfter != crystalsBefore)
                {
                    w.Crystals = crystalsAfter;
                }
                if (debtAfter != debtBefore)
                {
                    w.Debt = debtAfter;
                }
            }
            return new ArtDecision(ArtOutcome.Fire, art, selected, via, just,
                                  ClampByte(crystalsBefore - crystalsAfter), ClampByte(debtAfter - debtBefore),
                                  ClampByte(crystalsAfter), ClampByte(repeats), FizzleReason.None, 0, 0, t, node, justLateBy);
        }

        // spec 03 7.3 TryPay(외상 포함). 지갑은 바꾸지 않고 바뀔 값만 계산한다
        private bool TryPay(ArtId id, int crystals, int debt, bool credit, out int crystalsAfter, out int debtAfter, out int repeats)
        {
            ParserArt art = _trie.Source.Get(id);
            crystalsAfter = crystals;
            debtAfter = debt;
            repeats = 0;
            if (art.TakesAll)
            {
                if (crystals >= art.MinCost)
                {
                    repeats = crystals;                                 // 반복 수 = 소모 직전 결정 수
                    crystalsAfter = 0;
                    return true;
                }
                return false;                                           // 외상 불가
            }
            int cost = art.Cost;
            if (crystals >= cost)
            {
                crystalsAfter = crystals - cost;
                return true;
            }
            if (credit && art.CreditAllowed)
            {
                int need = cost - crystals;
                if (debt + need <= T.MaxDebt)
                {
                    crystalsAfter = 0;
                    debtAfter = debt + need;
                    return true;
                }
            }
            return false;
        }

        // spec 03 7.2 GapsOk: 노드 깊이만큼의 최근 방향 입력을 경로 위 노드들의 저장 간격으로 본다.
        // 마지막 방향 → K 간격은 0.3초 초기화가 이미 보장하므로 보지 않는다
        private bool GapsOk(int node)
        {
            int k = _trie.Depth(node);
            if (k <= 1)
            {
                return true;
            }
            int first = RequireHistory(k);
            for (int i = 1; i < k; i++)
            {
                long gap = unchecked(_dirTimes[first + i] - _dirTimes[first + i - 1]);
                if (gap > _trie.PathGap(node, i))
                {
                    return false;
                }
            }
            return true;
        }

        // spec 03 7.2 CheckFullBloom: 경로의 간격 전부와 마지막 방향 → K 를 만개 간격으로 다시 본다.
        // 떨어지면 lateIndex = "몇 번째 입력"(2부터), lateBy = 초과량
        private bool CheckFullBloom(int node, long tK, out byte lateIndex, out long lateBy)
        {
            int k = _trie.Depth(node);
            int first = RequireHistory(k);
            long limit = T.GapFullBloomUnits;
            for (int i = 1; i <= k; i++)
            {
                long cur = i < k ? _dirTimes[first + i] : tK;
                long gap = unchecked(cur - _dirTimes[first + i - 1]);
                if (gap > limit)
                {
                    lateIndex = (byte)(i + 1);
                    lateBy = unchecked(gap - limit);
                    return false;
                }
            }
            lateIndex = 0;
            lateBy = 0;
            return true;
        }

        // 깊이 k 노드를 검사할 만큼 기록이 있는가. 커서 깊이는 초기화 뒤의 방향 입력 수를 넘지 않으므로 늘 있다
        private int RequireHistory(int k)
        {
            if (k > _dirCount)
            {
                throw new SimInvariantException("arts: trie node deeper than the direction history");
            }
            return _dirCount - k;
        }

        private void PushDir(long time)
        {
            if (_dirCount == CommandTrie.DirHistorySize)
            {
                for (int i = 1; i < CommandTrie.DirHistorySize; i++)
                {
                    _dirTimes[i - 1] = _dirTimes[i];                    // 용량 8, 오래된 것부터 버림
                }
                _dirTimes[CommandTrie.DirHistorySize - 1] = time;
                return;
            }
            _dirTimes[_dirCount++] = time;
        }

        // UnlockSet 의 비트 번호 = (int)ArtId(CONTRACTS 2.1)
        public static bool IsUnlocked(in UnlockSet unlocks, ArtId art) => (((int)unlocks.Arts >> (int)art) & 1) != 0;

        private static byte ClampByte(int v)
        {
            if (v < 0 || v > byte.MaxValue)
            {
                throw new SimInvariantException("arts: crystal count out of byte range");
            }
            return (byte)v;
        }

        // ── 상태(CONTRACTS 3.5: 커서, 방향 시각 8개, 대기 끝, 보관 결과)

        // 바이트: cursor i32, in_stance bool, dir_count i32, dir_time i64 × 8(쓰지 않는 칸 0), cooldown_end i64,
        //         has_buffered bool, buffered(ArtDecision.Write. 없으면 빈 값)
        public void WriteState(ref StateWriter w)
        {
            w.I32(_cursor);
            w.Bool(_inStance);
            w.Count(_dirCount);
            for (int i = 0; i < CommandTrie.DirHistorySize; i++)
            {
                w.I64(i < _dirCount ? _dirTimes[i] : 0);
            }
            w.I64(_cooldownEnd);
            w.Bool(_hasBuffered);
            ArtDecision b = _hasBuffered ? _buffered : default;
            b.Write(ref w);
        }

        // WriteState 의 역. 깨진 값은 SimInvariantException
        public void ReadState(ref StateReader r)
        {
            int cursor = r.I32();
            bool inStance = r.Bool();
            int count = r.Count();
            if (!_trie.IsValidNode(cursor) || count < 0 || count > CommandTrie.DirHistorySize)
            {
                throw new SimInvariantException("arts: stored parser state out of range");
            }
            for (int i = 0; i < CommandTrie.DirHistorySize; i++)
            {
                long v = r.I64();
                _dirTimes[i] = i < count ? v : 0;
            }
            long cooldownEnd = r.I64();
            bool hasBuffered = r.Bool();
            ArtDecision b = ArtDecision.Read(ref r);
            _cursor = cursor;
            _inStance = inStance;
            _dirCount = count;
            _cooldownEnd = cooldownEnd;
            _hasBuffered = hasBuffered;
            _buffered = hasBuffered ? b : default;
        }

        // 스냅숏. 같은 모양의 트라이(같은 캐릭터, 같은 ContentDb)를 쓰는 판정기끼리만. 그림자 세계의 트라이는 다른 인스턴스다
        public void CopyFrom(ICommandParser other)
        {
            if (!(other is CommandParser o) || !o._trie.SameShapeAs(_trie))
            {
                throw new SimInvariantException("arts: snapshot copy between parsers of different tries");
            }
            _cursor = o._cursor;
            _dirCount = o._dirCount;
            for (int i = 0; i < CommandTrie.DirHistorySize; i++)
            {
                _dirTimes[i] = o._dirTimes[i];
            }
            _inStance = o._inStance;
            _cooldownEnd = o._cooldownEnd;
            _hasBuffered = o._hasBuffered;
            _buffered = o._buffered;
        }
    }
}
