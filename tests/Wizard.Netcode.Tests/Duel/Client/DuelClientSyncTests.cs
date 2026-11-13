using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Client
{
    // M2-NET-05 수락 조건: 세계 둘, 시계 둘, 멈춤과 따라잡기, NeedsFullState 의 두 조건, 패킷 검증, 결정론
    public sealed class DuelClientSyncTests
    {
        private static KeyMask Bit(LogicalKey k) => (KeyMask)(1 << (int)k);

        // 멈춘 동안에도 입력 시계는 간다: 키 이벤트는 입력 시계의 틱으로 찍혀 곧바로 나가고, 틱마다 패킷이 나간다.
        // 멈춤이 풀리면 한 번의 Advance 에서 CatchupTicksPerAdvance 까지 여러 틱을 돌린다
        [Fact]
        public void InputClockKeepsGoingWhileStalled_ThenCatchesUpManyTicksInOneAdvance()
        {
            NetRules net = ClientRig.Net;
            var rig = new ClientRig(me: 0, inputDelay: 2);
            DuelAdvance a = default;
            for (uint n = 0; n <= 20; n++)
            {
                a = rig.Client.Advance(n);
                Assert.NotNull(rig.Outgoing());                      // 매 틱 1개(멈춰도)
            }
            Assert.True(a.Stalled);
            Assert.Equal((uint)net.RollbackLimit, a.PredictedTick);  // 확정 없이 갈 수 있는 끝(틱 0..7)
            Assert.False(rig.Client.NeedsFullState);

            rig.Client.OnLocalInput(new[] { ClientRig.Q(22, LogicalKey.Stance, true) }, Bit(LogicalKey.Stance));
            ClientInputPacket? p = rig.Outgoing();
            Assert.Equal(22u, p!.Events[0].Tick);
            Assert.Equal(21u, p.SealedThroughTick);

            // 예측과 같은 확정 0..5 → 예측 세계(틱 8)를 그대로 두고 한도(확정 6 + 8)까지 한 번에 여러 틱을 돌린다
            rig.ConfirmEmptyThrough(5);
            DuelAdvance b = rig.Client.Advance(21);
            Assert.Equal(0, b.RollbackDepth);
            Assert.Equal(6, b.TicksSimulated);
            Assert.True(b.TicksSimulated <= net.Catchup);
            Assert.Equal(14u, b.PredictedTick);
            Assert.True(b.Stalled);                                  // 입력 시계(21)에 아직 못 닿았고 한도에 걸렸다

            // 확정이 예측 세계를 앞질렀다(16 > 14) → 확정 세계에서 다시 출발해 입력 시계까지
            rig.ConfirmEmptyThrough(15);
            DuelAdvance c = rig.Client.Advance(21);
            Assert.False(c.Stalled);
            Assert.Equal(6, c.TicksSimulated);
            Assert.Equal(22u, c.PredictedTick);
            Assert.Equal(16u, c.ConfirmedTick);
        }

        // 따라잡기 상한(CatchupTicksPerAdvance)이 되감기 한도보다 작아도 한 번의 Advance 는 상한까지만 돈다.
        // 지금 데이터는 둘 다 8이라 위 시험에서는 한도가 먼저 걸린다. 상한만 따로 보려고 상한에 닿는 Advance 를 만든다
        [Fact]
        public void CatchupCap_BoundsTicksPerAdvance()
        {
            NetRules net = ClientRig.Net;
            var rig = new ClientRig(me: 0);
            DuelAdvance a = rig.Client.Advance((uint)(net.Catchup * 3));
            Assert.Equal(Math.Min(net.Catchup, net.RollbackLimit), a.TicksSimulated);
        }

        // NeedsFullState 의 둘째 조건: 확정의 첫 틱이 내 확정 틱 + 1 보다 커서 생긴 공백이 FullStateGapTicks 를 넘을 때만
        [Fact]
        public void ConfirmGap_RequestsFullStateOnlyBeyondTheLimit()
        {
            NetRules net = ClientRig.Net;

            var within = new ClientRig();
            within.Client.Advance(0);
            within.ConfirmEmptyThrough(4);                            // 내 확정 틱 = 4
            uint from = 5U + (uint)net.GapLimit;                      // 공백 = GapLimit
            within.Deliver(within.ServerTick(new[] { Tick(from) }, fromTick: from));
            Assert.False(within.Client.NeedsFullState);
            Assert.Equal(5u, within.Client.Advance(1).ConfirmedTick);   // 메워질 때까지 기다린다

            var beyond = new ClientRig();
            beyond.Client.Advance(0);
            beyond.ConfirmEmptyThrough(4);
            uint far = 6U + (uint)net.GapLimit;                       // 공백 = GapLimit + 1
            beyond.Deliver(beyond.ServerTick(new[] { Tick(far) }, fromTick: far));
            Assert.True(beyond.Client.NeedsFullState);
            Assert.Equal(0, beyond.Client.HashMismatchCount);         // 해시 불일치 통계에는 세지 않는다
        }

        // 공백 뒤 전체 상태로 이어 간다. 깨진 상태는 받지 않는다
        [Fact]
        public void FullState_AfterGap_ContinuesWithHostHash_AndBrokenStateIsIgnored()
        {
            NetRules net = ClientRig.Net;
            var rig = new ClientRig();
            rig.Client.Advance(0);
            rig.ConfirmEmptyThrough(2);
            var skipped = new List<ConfirmedTick>();
            for (int i = 0; i < net.GapLimit + 5; i++)
            {
                skipped.Add(rig.StepServer());
            }
            ConfirmedTick far = rig.StepServer();
            rig.Deliver(rig.ServerTick(new[] { far }));
            Assert.True(rig.Client.NeedsFullState);

            rig.Client.OnFullState(rig.Server.Tick, new byte[] { 1, 2, 3 });
            Assert.True(rig.Client.NeedsFullState);
            var w = new ByteWriter();
            rig.Server.WriteState(w);
            rig.Client.OnFullState(rig.Server.Tick + 1, w.Written);   // 틱이 상태와 다르다
            Assert.True(rig.Client.NeedsFullState);
            rig.Client.OnFullState(rig.Server.Tick, w.Written);
            Assert.False(rig.Client.NeedsFullState);
            Assert.Equal(far.Hash, rig.Client.ConfirmedHash(far.Tick));

            ConfirmedTick next = rig.StepServer();
            rig.Deliver(rig.ServerTick(new[] { next }, hashOf: next));
            Assert.False(rig.Client.NeedsFullState);
            DuelAdvance a = rig.Client.Advance(next.Tick + 1);
            Assert.Equal(next.Tick + 1, a.ConfirmedTick);
            Assert.Equal(next.Hash, rig.Client.ConfirmedHash(next.Tick));
        }

        // 확정 세계에서 해시를 계산한다: 예측 세계가 틀린 입력으로 앞서 있어도 ConfirmedHash 는 서버 해시와 같다
        [Fact]
        public void ConfirmedHash_ComesFromConfirmedWorld_NotThePrediction()
        {
            var rig = new ClientRig(me: 0);
            for (uint n = 0; n <= 6; n++)
            {
                rig.Client.Advance(n);
            }
            var ticks = new List<ConfirmedTick>
            {
                rig.StepServer(),
                rig.StepServer(slot1: new[] { ClientRig.Ev(1, LogicalKey.Stance, true, 1) }),
                rig.StepServer(),
            };
            rig.Deliver(rig.ServerTick(ticks, hashOf: ticks[2]));
            Assert.False(rig.Client.NeedsFullState);
            Assert.Equal(ticks[2].Hash, rig.Client.ConfirmedHash(2));
            Assert.Equal(ticks[0].Hash, rig.Client.ConfirmedHash(0));
            Assert.Equal(0UL, rig.Client.ConfirmedHash(3));          // 아직 확정되지 않은 틱
        }

        // 서버 해시가 그 틱보다 먼저 오면 그 틱을 지날 때 견준다
        [Fact]
        public void ServerHashAheadOfConfirm_IsComparedWhenThatTickIsReached()
        {
            var rig = new ClientRig(me: 0);
            rig.Client.Advance(0);
            var ticks = new List<ConfirmedTick> { rig.StepServer(), rig.StepServer(), rig.StepServer() };
            rig.Deliver(rig.ServerTick(ticks.GetRange(0, 1), hashOf: ticks[2], hashValue: ticks[2].Hash ^ 0xFFUL));
            Assert.False(rig.Client.NeedsFullState);
            rig.Deliver(rig.ServerTick(ticks.GetRange(1, 2)));
            Assert.True(rig.Client.NeedsFullState);
            Assert.Equal(1, rig.Client.HashMismatchCount);
        }

        // 태그가 틀린 패킷, 다시 보낸 같은 번호, 다른 종류는 열지 않고 버린다(상태가 바뀌지 않는다)
        [Fact]
        public void ForgedReplayedOrForeignPackets_AreIgnored()
        {
            var rig = new ClientRig(me: 0);
            rig.Client.Advance(0);
            ConfirmedTick t0 = rig.StepServer();
            byte[] good = rig.ServerTick(new[] { t0 });

            byte[] forged = (byte[])good.Clone();
            forged[DuelWire.HeaderBytes + 2] ^= 0x01;
            rig.Deliver(forged);
            Assert.Equal(0u, rig.Client.Advance(0).ConfirmedTick);

            var otherKind = new ServerTickPacket { ConfirmedFromTick = 0 };
            otherKind.TryAddConfirmedTick(t0.Slot0, t0.Slot1);
            rig.Deliver(rig.Seal(otherKind, DuelPacketKind.SyncReply));
            Assert.Equal(0u, rig.Client.Advance(0).ConfirmedTick);

            rig.Deliver(good);
            Assert.Equal(1u, rig.Client.Advance(0).ConfirmedTick);

            // 같은 번호의 다시 보냄: 확정 틱 1 을 실었어도 받지 않는다
            ConfirmedTick t1 = rig.StepServer();
            byte[] next = rig.ServerTick(new[] { t1 });
            byte[] replay = (byte[])next.Clone();
            rig.Deliver(next);
            Assert.Equal(2u, rig.Client.Advance(0).ConfirmedTick);
            int failures = rig.Cipher.OpenFailures;
            rig.Deliver(replay);
            Assert.Equal(failures, rig.Cipher.OpenFailures);          // 재전송 창에서 걸러 열어 보지도 않는다
            Assert.Equal(2u, rig.Client.Advance(0).ConfirmedTick);

            // 깨진 길이의 패킷은 예외 없이 버린다
            rig.Client.OnPacket(new byte[] { 0x12, 1, 2 }, 0L);
            rig.Client.OnPacket(ReadOnlySpan<byte>.Empty, 0L);
        }

        // 마감 여유(HUD)와 에코: 받은 ServerTick 의 값을 그대로 낸다. 붙든 시간과 보낸 시각은 호출자가 준 두 시각에서 나온다(T-62)
        [Fact]
        public void MarginAndEchoComeFromTheLastServerTick()
        {
            var rig = new ClientRig(me: 0);
            rig.NowUs = 2_000_000L;
            rig.Client.Advance(5);
            ClientInputPacket? first = rig.Outgoing();
            Assert.Equal(0U, first!.EchoServerTimeMs);              // 받은 서버 패킷이 없으면 0(10.2)
            Assert.Equal(0, first.EchoHoldMs);
            Assert.Equal(2000U, first.ClientSendTimeMs);

            rig.NowUs = 2_040_100L;
            rig.Deliver(rig.ServerTick(new List<ConfirmedTick>(), serverTimeMs: 12345U, marginMs: 17));
            Assert.Equal(17, rig.Client.MarginMsEstimate);
            rig.NowUs = 2_048_000L;                                  // 받은 뒤 7.9ms 에 보낸다
            rig.Client.Advance(8);
            ClientInputPacket? p = rig.Outgoing();
            Assert.Equal(12345U, p!.EchoServerTimeMs);
            Assert.Equal(7, p.EchoHoldMs);                           // 내림. 크게 적으면 서버의 왕복 표본이 작아진다(10.5)
            Assert.Equal(2048U, p.ClientSendTimeMs);
        }

        // 붙든 시간은 입력 시계의 틱 수로 어림하지 않는다. 틱이 넘어가도 실제로 붙든 시간이 0.3ms 면 0 이다.
        // 틱으로 세면 16ms 가 되고, 서버는 왕복을 그만큼 작게 재서 D 를 한 단계 낮게 고른다(늦은 입력이 버려진다)
        [Fact]
        public void EchoHold_IsTheRealTimeBetweenReceiveAndSend_NotInputClockTicks()
        {
            var rig = new ClientRig(me: 0);
            rig.NowUs = 500_000L;
            rig.Client.Advance(30);
            rig.Outgoing();
            rig.NowUs = 516_400L;
            rig.Deliver(rig.ServerTick(new List<ConfirmedTick>(), serverTimeMs: 700U));
            rig.NowUs = 516_700L;
            rig.Client.Advance(31);                                  // 입력 시계는 한 틱 갔다
            Assert.Equal(0, rig.Outgoing()!.EchoHoldMs);

            // 같은 틱 안에서 오래 붙들면 그 시간이 그대로 실린다
            rig.NowUs = 516_700L + 12_999L;
            rig.Client.OnLocalInput(new[] { ClientRig.Q(40, LogicalKey.Stance, true) }, Bit(LogicalKey.Stance));
            Assert.Equal(13, rig.Outgoing()!.EchoHoldMs);

            // 받은 시각보다 이른 보낸 시각(호출자의 잘못)은 0, u16 을 넘으면 65,535
            rig.NowUs = 100L;
            rig.Client.Advance(32);
            Assert.Equal(0, rig.Outgoing()!.EchoHoldMs);
            rig.NowUs = 516_400L + 70_000_000L;
            rig.Client.Advance(33);
            Assert.Equal(ushort.MaxValue, rig.Outgoing()!.EchoHoldMs);
        }

        // 같은 키의 패킷 번호는 DuelClientKeys.SendCounter 한 곳에서만 나온다(T-62, CONTRACTS 10.1):
        // 연결 절차가 Hello·SyncRequest 에 쓴 번호를 코어가 다시 쓰지 않고, 경기 중에 끼어든 번호도 건너뛴다
        [Fact]
        public void PacketNumbers_ComeFromTheSharedSendCounter_NeverReusedAcrossSenders()
        {
            var rig = new ClientRig(me: 0);
            var used = new HashSet<ulong>();
            for (int i = 0; i < 17; i++)
            {
                Assert.True(used.Add(rig.Keys.SendCounter.Take()));  // SyncRequest 16번 + Hello
            }
            byte[] buffer = new byte[DuelWire.MaxPacketBytes];
            for (uint n = 0; n < 10; n++)
            {
                rig.Client.Advance(n);
                int len = rig.Client.PollOutgoing(buffer, rig.NowUs);
                Assert.True(len > 0);
                rig.Open(buffer.AsSpan(0, len).ToArray(), out DuelPacketHeader h);
                Assert.True(used.Add(h.PacketNumber), "packet number reused: " + h.PacketNumber);
                Assert.Equal(rig.Keys.SendCounter.Taken, h.PacketNumber);
                if (n == 4)
                {
                    Assert.True(used.Add(rig.Keys.SendCounter.Take()));   // 경기 중에 다른 보내는 쪽이 하나 썼다
                }
            }
            Assert.Equal(28UL, rig.Keys.SendCounter.Taken);          // 17 + 10 + 1. 빈 번호도 겹친 번호도 없다
            Assert.Equal(28, used.Count);
        }

        // 같은 호출 열이면 같은 출력 열이다(소켓·시계 없음, T-19)
        [Fact]
        public void SameCallSequence_SameOutputs()
        {
            (List<byte[]> packets, List<PresentationEvent> events, List<ulong> hashes) Run()
            {
                var rig = new ClientRig(me: 1, inputDelay: 3);
                var packets = new List<byte[]>();
                var hashes = new List<ulong>();
                byte[] buffer = new byte[DuelWire.MaxPacketBytes];
                var pending = new List<ConfirmedTick>();
                uint seq0 = 0;
                for (uint n = 0; n < 90; n++)
                {
                    if (n % 7 == 3)
                    {
                        rig.Client.OnLocalInput(new[] { ClientRig.Q(n + 3, LogicalKey.Stance, (n & 8) == 0, (byte)(n * 5)) },
                                                (n & 8) == 0 ? Bit(LogicalKey.Stance) : KeyMask.None);
                    }
                    rig.Client.Advance(n);
                    int len;
                    while ((len = rig.Client.PollOutgoing(buffer, rig.NowUs)) > 0)
                    {
                        packets.Add(buffer.AsSpan(0, len).ToArray());
                    }
                    if (n >= 4)
                    {
                        uint t = rig.Server.Tick;
                        InputEvent[]? s0 = t % 11 == 5 ? new[] { ClientRig.Ev(t, LogicalKey.Up, (t & 16) == 0, ++seq0) } : null;
                        pending.Add(rig.StepServer(slot0: s0));
                        if (n % 3 == 0)
                        {
                            rig.Deliver(rig.ServerTick(pending, hashOf: pending[pending.Count - 1]));
                            pending.Clear();
                        }
                    }
                    hashes.Add(rig.Client.ConfirmedHash(rig.Server.Tick - 1));
                }
                rig.Drain();
                return (packets, rig.Events, hashes);
            }

            var a = Run();
            var b = Run();
            Assert.Equal(a.packets.Count, b.packets.Count);
            for (int i = 0; i < a.packets.Count; i++)
            {
                Assert.Equal(a.packets[i], b.packets[i]);
            }
            Assert.Equal(a.hashes, b.hashes);
            Assert.Equal(a.events.Select(e => (e.Event.Type, e.Event.Tick, e.Event.Slot.Value, e.Phase, e.Ordinal)),
                         b.events.Select(e => (e.Event.Type, e.Event.Tick, e.Event.Slot.Value, e.Phase, e.Ordinal)));
            Assert.Contains(a.events, e => e.Phase == EventPhase.Confirmed);
        }

        // DuelClientKeys(T-62): token_id, epoch, 32바이트 키(복사해 둔다), 보내는 번호의 자리 하나
        [Fact]
        public void DuelClientKeys_HoldTokenEpochKeyCopyAndOneSendCounter()
        {
            byte[] source = new byte[DuelWire.KeyBytes];
            for (int i = 0; i < source.Length; i++)
            {
                source[i] = (byte)(i + 1);
            }
            var keys = new DuelClientKeys(0x11223344U, 7U, source);
            Assert.True(keys.IsValid);
            Assert.Equal(0x11223344U, keys.TokenId);
            Assert.Equal(7U, keys.Epoch);
            source[0] = 0xFF;                                        // 넘겨준 배열을 고쳐도 키는 그대로다
            Assert.Equal(1, keys.Key[0]);
            Assert.Equal(DuelWire.KeyBytes, keys.Key.Length);

            Assert.Equal(0UL, keys.SendCounter.Taken);
            DuelClientKeys copy = keys;                              // 값을 복사해도 번호의 자리는 하나다
            Assert.Equal(1UL, keys.SendCounter.Take());
            Assert.Equal(2UL, copy.SendCounter.Take());
            Assert.Equal(2UL, keys.SendCounter.Taken);
            Assert.Same(keys.SendCounter, copy.SendCounter);

            Assert.False(default(DuelClientKeys).IsValid);
            Assert.Throws<ArgumentException>(() => new DuelClientKeys(1U, 0U, new byte[16]));
            Assert.Throws<ArgumentException>(() => new DuelClientKeys(1U, 0U, new byte[DuelWire.KeyBytes + 1]));
        }

        [Fact]
        public void ArgumentChecks()
        {
            var keys = new DuelClientKeys(1U, 0U, new byte[DuelWire.KeyBytes]);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new DuelClientCore(ClientRig.Db, ClientRig.Setup(1), new PlayerSlot(2), keys, new FakeAeadCipher(), null));
            // 채우지 않은 키로 조용히 돌지 않는다
            Assert.Throws<ArgumentException>(() => new DuelClientCore(ClientRig.Db, ClientRig.Setup(1), new PlayerSlot(0),
                default(DuelClientKeys), new FakeAeadCipher()));
            Assert.Throws<ArgumentNullException>(() => new DuelClientCore(ClientRig.Db, ClientRig.Setup(1), new PlayerSlot(0), keys, null!));
            var rig = new ClientRig();
            Assert.Throws<ArgumentOutOfRangeException>(() => rig.Client.SetInputDelay(-1));
        }

        private static ConfirmedTick Tick(uint tick) => new ConfirmedTick(tick, Array.Empty<InputEvent>(), Array.Empty<InputEvent>(), 0UL);
    }
}
