using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Arts;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // 수락 조건 3: 판정기 상태(커서, 방향 시각 8개, 자세, 대기 끝, 보관된 K)가 스냅숏·직렬화·해시에 들어가고,
    // 되감은 뒤 같은 판정이 나온다. 두 번 실행, 저장 → 진행 → 복원 → 다시 진행, 바이트 왕복, 할당 0
    public sealed class ParserDeterminismTests
    {
        private const long T0 = ParserRig.T0;

        private enum Op { Dir, K, StanceOn, StanceOff, Reverse, Pause, Take }

        private readonly struct Act
        {
            public readonly Op Op;
            public readonly long Time;
            public readonly CmdDir Dir;

            public Act(Op op, long time, CmdDir dir = CmdDir.Forward)
            {
                Op = op;
                Time = time;
                Dir = dir;
            }
        }

        // 자세, 커맨드, 보관, 0.3초 초기화, 반전, 외상이 섞인 입력 열(char.5: 외상이 있다)
        private static Act[] Script()
        {
            var a = new List<Act>();
            long t = T0;
            a.Add(new Act(Op.StanceOn, t));
            a.Add(new Act(Op.Dir, t += 500, CmdDir.Left));
            a.Add(new Act(Op.Dir, t += 2304, CmdDir.Right));
            a.Add(new Act(Op.K, t += 2304));                                   // 그림자 손
            a.Add(new Act(Op.Dir, t += 1000, CmdDir.Forward));
            a.Add(new Act(Op.K, t += 4000));                                   // 대기 중 보관(창 안)
            a.Add(new Act(Op.Dir, t += 300, CmdDir.Forward));
            a.Add(new Act(Op.Take, t += 1200));
            a.Add(new Act(Op.Dir, t += 500, CmdDir.Forward));
            a.Add(new Act(Op.Reverse, t += 100));
            a.Add(new Act(Op.Dir, t += 600, CmdDir.Back));
            a.Add(new Act(Op.Dir, t += 5000, CmdDir.Forward));                // 0.3초 초기화 뒤
            a.Add(new Act(Op.Dir, t += 2000, CmdDir.Back));
            a.Add(new Act(Op.Dir, t += 2000, CmdDir.Back));
            a.Add(new Act(Op.Dir, t += 2000, CmdDir.Forward));
            a.Add(new Act(Op.Take, t += 10));
            a.Add(new Act(Op.K, t += 2000));                                   // 만개(결정 모자라면 불발)
            a.Add(new Act(Op.Dir, t += 7000, CmdDir.Left));
            a.Add(new Act(Op.Dir, t += 1000, CmdDir.Left));
            a.Add(new Act(Op.K, t += 100));                                    // 잔상 걸음(외상)
            a.Add(new Act(Op.Pause, t += 200));
            a.Add(new Act(Op.StanceOff, t += 100));
            a.Add(new Act(Op.K, t += 9000));                                   // 자세 밖 마력 방출
            a.Add(new Act(Op.Take, t += 9000));
            return a.ToArray();
        }

        private static void Apply(CommandParser p, TestWallet w, in UnlockSet u, in Act act, List<ArtDecision> outList)
        {
            switch (act.Op)
            {
                case Op.Dir: p.OnDirection(act.Dir, act.Time); break;
                case Op.StanceOn: p.OnStanceChanged(true, act.Time); break;
                case Op.StanceOff: p.OnStanceChanged(false, act.Time); break;
                case Op.Reverse: p.Reset(CommandResetReason.Reverse); break;
                case Op.Pause: p.Reset(CommandResetReason.Pause); break;
                case Op.K:
                    if (p.OnSkillPress(act.Time, w, in u, out ArtDecision v))
                    {
                        outList.Add(v);
                    }
                    break;
                case Op.Take:
                    if (p.TryTakeBuffered(act.Time, out ArtDecision b))
                    {
                        outList.Add(b);
                    }
                    break;
            }
        }

        private static ulong Hash(CommandParser p)
        {
            StateWriter w = StateWriter.ForHash();
            p.WriteState(ref w);
            return w.Hash;
        }

        private static byte[] Bytes(CommandParser p)
        {
            var bw = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bw);
            p.WriteState(ref w);
            return bw.ToArray();
        }

        private static (List<ArtDecision> verdicts, List<ulong> hashes, TestWallet wallet) RunFrom(
            CommandParser p, TestWallet w, Act[] script, int from, int to)
        {
            UnlockSet u = Unlocks.All.For(CharacterId.Char5);
            var verdicts = new List<ArtDecision>();
            var hashes = new List<ulong>();
            for (int i = from; i < to; i++)
            {
                Apply(p, w, in u, in script[i], verdicts);
                hashes.Add(Hash(p));
            }
            return (verdicts, hashes, w);
        }

        private static void AssertSame(List<ArtDecision> a, List<ArtDecision> b)
        {
            Assert.Equal(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(Encode(a[i]), Encode(b[i]));
            }
        }

        private static string Encode(in ArtDecision v)
        {
            var bw = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bw);
            v.Write(ref w);
            return Convert.ToHexString(bw.ToArray());
        }

        [Fact]
        public void ScriptProducesFiresBufferedAndFizzles()
        {
            Act[] s = Script();
            var p = new CommandParser(SpecArts.Trie(CharacterId.Char5));
            var r = RunFrom(p, SpecArts.Wallet(CharacterId.Char5, 3, 0), s, 0, s.Length);
            Assert.True(r.verdicts.Count >= 4);
            Assert.Contains(r.verdicts, v => v.Art == ArtId.ShadowHand && v.IsFire);
            Assert.Contains(r.verdicts, v => v.DebtAdded > 0);
        }

        [Fact]
        public void TwoRuns_SameVerdictsAndHashes()
        {
            Act[] s = Script();
            var a = RunFrom(new CommandParser(SpecArts.Trie(CharacterId.Char5)), SpecArts.Wallet(CharacterId.Char5, 3, 0), s, 0, s.Length);
            var b = RunFrom(new CommandParser(SpecArts.Trie(CharacterId.Char5)), SpecArts.Wallet(CharacterId.Char5, 3, 0), s, 0, s.Length);
            AssertSame(a.verdicts, b.verdicts);
            Assert.Equal(a.hashes, b.hashes);
            Assert.Equal(a.wallet.Crystals, b.wallet.Crystals);
            Assert.Equal(a.wallet.Debt, b.wallet.Debt);
        }

        // 모든 지점에서: 저장 → 끝까지 진행 → 복원 → 다시 끝까지. 두 번째 진행의 판정과 해시가 첫 번째와 같고,
        // 처음부터 끊김 없이 돌린 것과도 같다
        [Fact]
        public void SnapshotRestore_AtEveryStep_ReplaysIdentically()
        {
            Act[] s = Script();
            CommandTrie trie = SpecArts.Trie(CharacterId.Char5);
            var whole = RunFrom(new CommandParser(trie), SpecArts.Wallet(CharacterId.Char5, 3, 0), s, 0, s.Length);
            for (int cut = 0; cut <= s.Length; cut++)
            {
                var p = new CommandParser(trie);
                TestWallet w = SpecArts.Wallet(CharacterId.Char5, 3, 0);
                var head = RunFrom(p, w, s, 0, cut);

                var snap = new CommandParser(trie);
                snap.CopyFrom(p);
                TestWallet wSnap = SpecArts.Wallet(CharacterId.Char5, 0, 0);
                wSnap.CopyFrom(w);
                Assert.Equal(Hash(p), Hash(snap));

                var first = RunFrom(p, w, s, cut, s.Length);
                p.CopyFrom(snap);
                w.CopyFrom(wSnap);
                Assert.Equal(Hash(snap), Hash(p));
                var second = RunFrom(p, w, s, cut, s.Length);

                AssertSame(first.verdicts, second.verdicts);
                Assert.Equal(first.hashes, second.hashes);
                var joined = new List<ArtDecision>(head.verdicts);
                joined.AddRange(second.verdicts);
                AssertSame(whole.verdicts, joined);
            }
        }

        // WriteState → ReadState 왕복: 바이트가 같고, 이어서 돌린 결과도 같다
        [Fact]
        public void SerializeRoundTrip_AtEveryStep()
        {
            Act[] s = Script();
            CommandTrie trie = SpecArts.Trie(CharacterId.Char5);
            for (int cut = 0; cut <= s.Length; cut++)
            {
                var p = new CommandParser(trie);
                TestWallet w = SpecArts.Wallet(CharacterId.Char5, 3, 0);
                RunFrom(p, w, s, 0, cut);
                byte[] bytes = Bytes(p);

                var q = new CommandParser(trie);
                var r = new StateReader(bytes);
                q.ReadState(ref r);
                Assert.Equal(0, r.Remaining);
                Assert.Equal(bytes, Bytes(q));
                Assert.Equal(Hash(p), Hash(q));

                TestWallet w2 = SpecArts.Wallet(CharacterId.Char5, 0, 0);
                w2.CopyFrom(w);
                var a = RunFrom(p, w, s, cut, s.Length);
                var b = RunFrom(q, w2, s, cut, s.Length);
                AssertSame(a.verdicts, b.verdicts);
                Assert.Equal(a.hashes, b.hashes);
            }
        }

        // 상태의 각 칸이 해시에 들어간다
        [Fact]
        public void Hash_CoversEveryStateField()
        {
            CommandTrie trie = SpecArts.Trie(CharacterId.Char1);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            var hashes = new HashSet<ulong>();

            var p = new CommandParser(trie);
            Assert.True(hashes.Add(Hash(p)));                                  // 처음
            p.OnStanceChanged(true, T0);
            Assert.True(hashes.Add(Hash(p)));                                  // 자세
            p.OnDirection(CmdDir.Forward, T0 + 10);
            Assert.True(hashes.Add(Hash(p)));                                  // 커서·시각

            var q = new CommandParser(trie);
            q.OnStanceChanged(true, T0);
            q.OnDirection(CmdDir.Forward, T0 + 11);                            // 시각만 다르다
            Assert.True(hashes.Add(Hash(q)));

            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            Assert.True(p.OnSkillPress(T0 + 20, w, in u, out _));
            Assert.True(hashes.Add(Hash(p)));                                  // 대기 끝
            Assert.False(p.OnSkillPress(T0 + 20 + SpecArts.ArtCooldown - 10, w, in u, out _));
            Assert.True(p.HasBuffered);
            Assert.True(hashes.Add(Hash(p)));                                  // 보관된 K
        }

        [Fact]
        public void ReadState_RejectsBrokenValues()
        {
            CommandTrie trie = SpecArts.Trie(CharacterId.Char1);
            var p = new CommandParser(trie);
            byte[] bytes = Bytes(p);
            byte[] badCursor = (byte[])bytes.Clone();
            badCursor[0] = 0xFF;                                               // cursor i32(LE) 의 첫 바이트
            Assert.Throws<SimInvariantException>(() =>
            {
                var r = new StateReader(badCursor);
                new CommandParser(trie).ReadState(ref r);
            });
            byte[] badCount = (byte[])bytes.Clone();
            badCount[5] = 9;                                                   // dir_count = 9 > 8
            Assert.Throws<SimInvariantException>(() =>
            {
                var r = new StateReader(badCount);
                new CommandParser(trie).ReadState(ref r);
            });
        }

        // 스냅숏의 그림자 세계와 결투의 예측 세계는 같은 ContentDb 로 따로 만든 트라이를 가진다(CONTRACTS 3.2).
        // 그래서 같은 모양(같은 캐릭터)의 다른 인스턴스끼리는 복사가 되고, 복사한 쪽이 같은 판정을 이어 간다
        [Fact]
        public void CopyFrom_SameShapeOtherInstance_CopiesState()
        {
            Act[] s = Script();
            for (int cut = 0; cut <= s.Length; cut++)
            {
                var a = new CommandParser(SpecArts.Trie(CharacterId.Char5));
                var b = new CommandParser(SpecArts.Trie(CharacterId.Char5));
                Assert.NotSame(a.Trie, b.Trie);
                TestWallet wa = SpecArts.Wallet(CharacterId.Char5, 3, 0);
                RunFrom(a, wa, s, 0, cut);
                b.CopyFrom(a);
                Assert.Equal(Bytes(a), Bytes(b));
                TestWallet wb = SpecArts.Wallet(CharacterId.Char5, 0, 0);
                wb.CopyFrom(wa);
                var ra = RunFrom(a, wa, s, cut, s.Length);
                var rb = RunFrom(b, wb, s, cut, s.Length);
                AssertSame(ra.verdicts, rb.verdicts);
                Assert.Equal(ra.hashes, rb.hashes);
            }
        }

        [Fact]
        public void CopyFrom_OtherCharacterTrie_IsInvariantViolation()
        {
            var a = new CommandParser(SpecArts.Trie(CharacterId.Char1));
            var b = new CommandParser(SpecArts.Trie(CharacterId.Char2));
            Assert.Throws<SimInvariantException>(() => a.CopyFrom(b));
        }

        // 방향 입력, K(발동·보관·무시·불발), 보관 꺼내기, 초기화, 미리보기, 해시 쓰기에서 할당하지 않는다
        [Fact]
        public void HotPaths_DoNotAllocate()
        {
            CommandTrie trie = SpecArts.Trie(CharacterId.Char5);
            var p = new CommandParser(trie);
            TestWallet w = SpecArts.Wallet(CharacterId.Char5, 7, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char5);
            void Round(long t)
            {
                p.OnStanceChanged(true, t);
                p.OnDirection(CmdDir.Forward, t + 1);
                p.OnDirection(CmdDir.Back, t + 2000);
                p.OnDirection(CmdDir.Back, t + 4000);
                p.OnDirection(CmdDir.Forward, t + 6000);
                p.Preview(t + 6100, w, in u);
                p.OnDirection(CmdDir.Forward, t + 6150);                       // 커서 FF: 두 줄 미리보기
                p.Preview(t + 6160, w, in u);
                p.OnSkillPress(t + 6200, w, in u, out _);
                p.OnSkillPress(t + 6300, w, in u, out _);
                p.OnSkillPress(t + 6200 + SpecArts.ArtCooldown - 100, w, in u, out _);
                p.TryTakeBuffered(t + 6200 + SpecArts.ArtCooldown, out _);
                for (int i = 0; i < 10; i++)
                {
                    p.OnDirection(i % 2 == 0 ? CmdDir.Left : CmdDir.Right, t + 20000 + i * 100);
                }
                p.OnSkillPress(t + 30000, w, in u, out _);
                StateWriter sw = StateWriter.ForHash();
                p.WriteState(ref sw);
                p.Reset(CommandResetReason.WaveStart);
                p.OnStanceChanged(false, t + 40000);
                w.Crystals = 7;
                w.Debt = 0;
            }
            Round(T0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 200; i++)
            {
                Round(T0 + i * 100_000L);
            }
            Assert.Equal(0L, GC.GetAllocatedBytesForCurrentThread() - before);
        }
    }
}
