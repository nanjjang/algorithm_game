using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;
using static Wizard.Netcode.Tests.Duel.Host.HostKit;
using static Wizard.Netcode.Tests.Duel.Host.HostRig;

namespace Wizard.Netcode.Tests.Duel.Host
{
    // 계약 타입과 틱·시각의 뜻(CONTRACTS 6.4·10.1~10.3, DECISIONS-TECH T-62·T-64):
    // DuelPeerKey·DuelHostKeys, DuelHostStatus, TryWriteFullState 의 tick, ack 0, server_time_ms, 빈 패킷 번호,
    // 확정 열의 순번(한 사람 안에서 늘어나기만 하지 않는다), 아직 자리만 있는 문(Rekey, 관전)
    public sealed class HostContractTests
    {
        private const uint T10 = 10;

        // ── 키 (T-62 5번)

        [Fact]
        public void DuelPeerKeyHoldsTokenEpochAndACopyOfThe32ByteKey()
        {
            byte[] k = Key(0);
            var key = new DuelPeerKey(Token0, 3U, k);
            Assert.True(key.IsValid);
            Assert.Equal(Token0, key.TokenId);
            Assert.Equal(3U, key.Epoch);
            Assert.Equal(Key(0), key.Key.ToArray());

            Array.Clear(k, 0, k.Length);                                   // 호출자가 버퍼를 다시 써도
            Assert.Equal(Key(0), key.Key.ToArray());                       // 복사해 둔 키는 그대로다

            Assert.Throws<ArgumentException>(() => new DuelPeerKey(Token0, 1U, new byte[16]));
            Assert.Throws<ArgumentException>(() => new DuelPeerKey(Token0, 1U, new byte[33]));
            Assert.Throws<ArgumentException>(() => new DuelPeerKey(Token0, 1U, ReadOnlySpan<byte>.Empty));

            DuelPeerKey none = default;
            Assert.False(none.IsValid);
            Assert.Equal(0, none.Key.Length);
        }

        [Fact]
        public void DuelHostKeysNeedsTwoValidKeysWithDifferentTokens()
        {
            var keys = new DuelHostKeys(PeerKey(0), PeerKey(1));
            Assert.Equal(Token0, keys.Slot0.TokenId);
            Assert.Equal(Token1, keys.Slot1.TokenId);
            Assert.Equal(Token0, keys.For(S0).TokenId);
            Assert.Equal(Key(1), keys.For(S1).Key.ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => keys.For(new PlayerSlot(2)));

            Assert.Throws<ArgumentException>(() => new DuelHostKeys(PeerKey(0), default));
            Assert.Throws<ArgumentException>(() => new DuelHostKeys(default, PeerKey(1)));
            Assert.Throws<ArgumentException>(() => new DuelHostKeys(PeerKey(0), PeerKey(0)));                       // 같은 token_id
            Assert.Throws<ArgumentException>(() => new DuelHostKeys(PeerKey(0), new DuelPeerKey(Token0, 2U, Key(1))));
        }

        [Fact]
        public void ConstructorTakesDuelHostKeysAndRejectsUnfilledOnes()
        {
            Assert.Throws<ArgumentNullException>(() => new DuelHostCore(null!, Setup(), Keys(), new TestCipher()));
            Assert.Throws<ArgumentNullException>(() => new DuelHostCore(Db, null!, Keys(), new TestCipher()));
            Assert.Throws<ArgumentNullException>(() => new DuelHostCore(Db, Setup(), Keys(), null!));
            Assert.Throws<ArgumentException>(() => new DuelHostCore(Db, Setup(), default, new TestCipher()));       // 채우지 않은 키

            // 슬롯 0 의 키로 봉인한 패킷은 슬롯 0 의 것으로, 슬롯 1 의 키는 슬롯 1 의 것으로 열린다. 호스트는 키를 복사해 들고 있다
            byte[] k0 = Key(0);
            var host = new DuelHostCore(Db, Setup(), new DuelHostKeys(new DuelPeerKey(Token0, 1U, k0), PeerKey(1)), new TestCipher());
            Array.Clear(k0, 0, k0.Length);
            new FakeClient(0).Send(host, T(1), 0, new InputEvent[0]);
            new FakeClient(1).Send(host, T(1), 0, new InputEvent[0]);
            Assert.Equal(1, host.StatsFor(S0).PacketsVerified);
            Assert.Equal(1, host.StatsFor(S1).PacketsVerified);
            Assert.Throws<ArgumentOutOfRangeException>(() => host.StatsFor(new PlayerSlot(2)));
        }

        // ── 진행 상태 (T-64)

        [Fact]
        public void StatusGivesTheNextConfirmTickAndWhetherTheWorldIsOver()
        {
            var r = new HostRig();
            Assert.Equal(0U, r.Host.Status.NextConfirmTick);
            Assert.False(r.Host.Status.IsOver);

            r.Advance(T(T10) - 1);                                         // 틱 9 가 끝나기 1µs 전: 0..8 이 닫혔다
            Assert.Equal(T10 - 1, r.Host.Status.NextConfirmTick);
            r.Advance(T(T10));
            Assert.Equal(T10, r.Host.Status.NextConfirmTick);              // 확정한 틱의 수 = 다음에 확정할 틱
            Assert.False(r.Host.Status.IsOver);                            // 경기 끝 규칙의 모듈(M14-DUL-01) 전에는 세계가 끝나지 않는다

            // 마지막 확정 틱 = NextConfirmTick − 1 (ResumeInfo 의 ConfirmedTick)
            Assert.Equal(T10 - 1, r.Host.ResumeInfo(S0).ConfirmedTick);
        }

        // ── 틱의 뜻 (T-62 7번)

        [Fact]
        public void TryWriteFullStateTickIsTheWorldTickOfTheWrittenState()
        {
            var r = new HostRig();
            r.Send(1, T(8), T10 + 5);
            r.Send(0, T(8), T10 + 5, Ev(T10, 3, LogicalKey.Right, true, 1), Ev(T10 + 2, 9, LogicalKey.Fire, true, 2));
            r.Advance(T(T10 + 6));
            uint next = r.Host.Status.NextConfirmTick;
            Assert.True(next >= T10 + 6);

            var w = new ByteWriter();
            Assert.True(r.Host.TryWriteFullState(w, out uint tick));
            Assert.Equal(next, tick);                                      // 다음에 처리할 틱(처리한 틱의 수)

            // 그 상태를 같은 모양의 새 세계에 읽으면 그 틱에 서 있고 해시가 호스트의 것(WZDR 꼬리말)과 같다
            DuelSetup setup = Setup();
            SimWorld world = SimWorld.CreateDuel(Db, SimConfig.ForDuel(Db, setup.Options), setup);
            world.ReadState(w.Written);
            Assert.Equal(tick, world.Tick);
            Wzdr.File file = Wzdr.Read(r.Host.ConfirmedInputRecord());
            Assert.Equal(next, file.Body.EndTick);
            Assert.Equal(file.Body.FinalStateHash, world.ComputeStateHash());

            Assert.Throws<ArgumentNullException>(() => r.Host.TryWriteFullState(null!, out _));
        }

        [Fact]
        public void AckZeroMeansFromTickZeroAndAckKMeansFromKPlusOne()
        {
            var r = new HostRig();
            FakeClient c = r.Clients[0];

            // ack 0 = "아직 없다"와 "틱 0 까지 받았다"가 같은 값이다. 서버는 틱 0 부터 싣는다
            c.Send(r.Host, T(5), 0, new InputEvent[0], ack: 0);
            r.Advance(T(5));
            Assert.Equal(0U, r.Views[0].Last().ConfirmedFrom);
            Assert.Equal(5, r.Views[0].Last().Ticks.Count);                // 틱 0..4

            c.Send(r.Host, T(6), 0, new InputEvent[0], ack: 0);            // 다시 0: 틱 0 을 한 번 더 싣는다
            r.Advance(T(6));
            Assert.Equal(0U, r.Views[0].Last().ConfirmedFrom);

            c.Send(r.Host, T(7), 0, new InputEvent[0], ack: 3);            // 틱 3 까지 받았다 → 4 부터
            r.Advance(T(7));
            Assert.Equal(4U, r.Views[0].Last().ConfirmedFrom);
            Assert.Equal(3, r.Views[0].Last().Ticks.Count);                // 틱 4..6

            c.Send(r.Host, T(8), 0, new InputEvent[0], ack: 1);            // 더 작은 ack(순서가 바뀌어 온 옛 패킷)은 되돌리지 않는다
            r.Advance(T(8));
            Assert.Equal(4U, r.Views[0].Last().ConfirmedFrom);

            // 받지도 않은 틱을 받았다고 하면 그 패킷을 받은 때에 확정한 데(틱 0..7)까지만 받은 것으로 본다. 그 뒤의 확정은 빠짐없이 실린다
            Assert.Equal(8U, r.Host.Status.NextConfirmTick);
            c.Send(r.Host, T(9), 0, new InputEvent[0], ack: 5000);
            r.Advance(T(9));
            Assert.Equal(8U, r.Views[0].Last().ConfirmedFrom);
            Assert.Single(r.Views[0].Last().Ticks);                        // 틱 8
            r.Advance(T(T10));
            Assert.Equal(8U, r.Views[0].Last().ConfirmedFrom);
            Assert.Equal(2, r.Views[0].Last().Ticks.Count);                // 틱 8..9
        }

        [Fact]
        public void ServerTimeMsIsTheMatchClockInMilliseconds()
        {
            var r = new HostRig();
            r.Send(0, T(1), 0);
            r.Advance(123_456);                                            // 경기 시계 123.456ms
            Assert.Equal(123U, r.Views[0].Last().ServerTimeMs);            // 내림
            r.Advance(2_000_999);
            Assert.Equal(2000U, r.Views[0].Last().ServerTimeMs);
        }

        // ── 패킷 번호의 빈 자리 (T-62 3번)

        [Fact]
        public void GapsInClientPacketNumbersAreNotAFault()
        {
            var r = new HostRig();
            FakeClient c = r.Clients[0];
            c.Send(r.Host, T(1), 0, new InputEvent[0]);                    // 번호 1
            c.SkipPacketNumbers(4);
            c.Send(r.Host, T(2), 1, new InputEvent[0]);                    // 번호 6
            c.SkipPacketNumbers(200);
            c.Send(r.Host, T(3), 2, new[] { Ev(5, 0, LogicalKey.Fire, true, 1) });     // 번호 207(창 64 를 넘는 건너뜀)
            c.Send(r.Host, T(4), 3, new InputEvent[0], lastSeq: 1);        // 번호 208

            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(4, s.PacketsVerified);
            Assert.Equal(0, s.PacketsReplayed);
            Assert.Equal(0, s.PacketsUnauthentic);
            Assert.Equal(0, s.OrderViolations);
            Assert.Equal(1, s.EventsAccepted);
        }

        // ── 확정 열의 순번 (T-64)

        [Fact]
        public void TimeReversalAcrossPacketsIsNotAnOrderViolationAndTheRecordStillWrites()
        {
            // 조작한 클라이언트: 순번 1 을 틱 11 에, 다음 패킷에서 순번 2 를 틱 10 에 낸다. 10.6 (0)은 한 패킷 안만 보므로 둘 다 받는다
            // (둘 다 봉인 전이고 마감 안이다). 확정 열은 InputOrder 순이라 틱 10 의 순번 2 가 틱 11 의 순번 1 보다 먼저 실린다
            var r = new HostRig();
            r.Send(1, T(8), T10 + 2);
            InputEvent later = Ev(T10 + 1, 0, LogicalKey.Right, true, 1);
            InputEvent earlier = Ev(T10, 7, LogicalKey.Fire, true, 2);
            r.Send(0, T(8), T10 - 1, later);
            r.SendBase(0, T(8) + 500, T10 - 1, 1, earlier);
            r.Send(0, T(9), T10 + 2);
            r.Advance(T(9) + 1000);

            Assert.Equal(T10 + 3, r.Host.Status.NextConfirmTick);
            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(0, s.OrderViolations);                            // 몰수 계수에 들어가지 않는다
            Assert.Equal(2, s.EventsAccepted);
            Assert.Empty(r.Dropped(0));
            Assert.Equal(new[] { earlier }, r.Confirmed(1, T10, 0));
            Assert.Equal(new[] { later }, r.Confirmed(1, T10 + 1, 0));

            // 다시 보기 기록을 쓸 수 있고(예외 없음), 그 기록을 다시 돌리면 호스트의 해시가 나온다
            Assert.Equal(new[] { earlier, later }, r.Record(0));
            Wzdr.File file = Wzdr.Read(r.Host.ConfirmedInputRecord());
            Assert.Equal(file.Body.FinalStateHash, Wzdr.Replay(file).final);
            Assert.Equal(ReplayStatus.EndedLate, ReplayRunner.RunDuel(Db, r.Host.ConfirmedInputRecord().Span, Wzdr.Generous).Status);
        }

        [Fact]
        public void RandomAuthenticPacketsAlwaysLeaveAWritableRecordThatReplaysToTheHostHash()
        {
            // 한 패킷 안의 순서만 지키는 무작위 클라이언트 둘(패킷마다 시각을 아무 데서나 시작한다). 어느 열이 확정되든
            // ConfirmedInputRecord 는 던지지 않고, 그 기록은 읽히고, 다시 돌리면 호스트의 해시가 나온다
            bool sawDecreasingSeq = false;
            for (int seed = 1; seed <= 6; seed++)
            {
                var rng = new Random(seed * 131);
                var r = new HostRig(3, 2);
                var lastSeq = new uint[2];
                long now = 0;
                for (int i = 0; i < 500; i++)
                {
                    int slot = rng.Next(0, 2);
                    uint nowTick = (uint)(now * 60 / 1_000_000);
                    int count = rng.Next(0, 5);
                    var events = new InputEvent[count];
                    uint seq = lastSeq[slot];
                    uint baseSeq = seq;
                    uint tick = nowTick + (uint)rng.Next(0, 4);
                    byte offset = (byte)rng.Next(0, 256);
                    for (int k = 0; k < count; k++)
                    {
                        seq++;
                        if (rng.Next(0, 3) == 0)
                        {
                            tick += (uint)rng.Next(1, 3);
                            offset = (byte)rng.Next(0, 256);
                        }
                        else if (rng.Next(0, 2) == 0)
                        {
                            offset = (byte)Math.Min(255, offset + rng.Next(0, 40));
                        }
                        events[k] = Ev(tick, offset, (LogicalKey)rng.Next(0, 8), rng.Next(0, 2) == 0, seq);
                    }
                    lastSeq[slot] = seq;
                    r.Clients[slot].Send(r.Host, now, nowTick + (uint)rng.Next(0, 3), events, baseSeq);
                    now += rng.Next(0, 9000);
                    if (rng.Next(0, 3) == 0)
                    {
                        r.Advance(now);
                        _ = r.Host.ConfirmedInputRecord();                 // 경기 중에도 던지지 않는다
                    }
                }
                r.Advance(now + 2_000_000);

                Assert.Equal(0, r.Host.StatsFor(S0).OrderViolations);      // 한 패킷 안은 다 바른 순서다
                Assert.Equal(0, r.Host.StatsFor(S1).OrderViolations);
                Wzdr.File file = Wzdr.Read(r.Host.ConfirmedInputRecord());
                Assert.Equal(r.Host.Status.NextConfirmTick, file.Body.EndTick);
                Assert.True(file.Inputs.Count > 100, "the random clients must get inputs confirmed");
                Assert.Equal(file.Body.FinalStateHash, Wzdr.Replay(file).final);
                Assert.Equal(ReplayStatus.EndedLate, ReplayRunner.RunDuel(Db, r.Host.ConfirmedInputRecord().Span, Wzdr.Generous).Status);

                // 확정 열은 InputOrder 순이다. 한 사람의 순번은 그 열에서 한 번씩만 나온다(10.6: 순번은 한 번 소비된다)
                for (int i = 1; i < file.Inputs.Count; i++)
                {
                    Assert.True(InputOrder.Compare(file.Inputs[i - 1], file.Inputs[i]) < 0);
                }
                for (int slot = 0; slot < 2; slot++)
                {
                    List<uint> seqs = file.Inputs.Where(p => p.Slot.Value == slot).Select(p => p.Event.Seq).ToList();
                    Assert.Equal(seqs.Count, seqs.Distinct().Count());
                    sawDecreasingSeq |= seqs.Zip(seqs.Skip(1), (a, b) => b < a).Any(x => x);
                }
            }
            Assert.True(sawDecreasingSeq, "the scenario must produce a confirmed column whose seq decreases within a slot");
        }

        // ── 아직 자리만 있는 문 (M14-NET-01)

        [Fact]
        public void LifecycleAndSpectatorDoorsAreExplicitPlaceholders()
        {
            var r = new HostRig();
            r.Advance(T(200));

            // Rekey 를 조용히 넘기면 옛 키로 계속 보내게 된다. 채워지기 전에는 던진다
            Assert.Throws<NotSupportedException>(() => r.Host.Rekey(S0, new DuelPeerKey(0x77U, 2U, Key(0))));
            Assert.Throws<ArgumentOutOfRangeException>(() => r.Host.Rekey(new PlayerSlot(2), new DuelPeerKey(0x77U, 2U, Key(0))));

            var w = new ByteWriter();
            Assert.False(r.Host.TryWriteSpectatorStart(w, out uint tick));
            Assert.Equal(0U, tick);
            Assert.Equal(0, w.Written.Length);
            Assert.Equal(0, r.Host.PollSpectatorChunk(new byte[DuelWire.MaxPacketBytes]));
        }
    }
}
