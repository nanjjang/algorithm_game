using System;
using System.Linq;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;
using Xunit;
using static Wizard.Netcode.Tests.Duel.Host.HostKit;
using static Wizard.Netcode.Tests.Duel.Host.HostRig;

namespace Wizard.Netcode.Tests.Duel.Host
{
    // 받는 쪽의 겉 형식(CONTRACTS 10.1): token_id, 태그, 재전송 창, 응답 주소. 서버가 잰 왕복(10.5)과 입력 지연 D(SetInputDelay)
    public sealed class HostTransportTests
    {
        private const uint T10 = 10;

        // ── 인증 (spec 10 14.10, TV-N27)

        [Fact]
        public void S10_TV_N27__UnknownTokenOrBadTagIsDroppedUnopened()
        {
            var r = new HostRig();
            InputEvent fire = Ev(T10, 0, LogicalKey.Fire, true, 1);

            // 모르는 token_id: 열어 보지도 않는다(복호 0회), 답하지 않는다
            var stranger = new FakeClient(0, token: 0x0BADF00DU);
            stranger.Send(r.Host, T(8), T10, new[] { fire });
            Assert.Equal(0, r.Cipher.OpenCalls);

            // 아는 token_id 에 틀린 태그(마지막 바이트를 뒤집음), 다른 키로 봉인한 것: 열지 못하고 버린다. 아무 상태도 바꾸지 않는다
            byte[] good = r.Clients[0].Build(T10, new[] { fire });
            byte[] tampered = (byte[])good.Clone();
            tampered[tampered.Length - 1] ^= 0x01;
            r.Host.OnPacket(tampered, T(8), Addr0);
            var wrongKey = new FakeClient(0, key: Key(1));
            wrongKey.Send(r.Host, T(8), T10, new[] { fire });
            Assert.Equal(2, r.Cipher.OpenCalls);

            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(2, s.PacketsUnauthentic);
            Assert.Equal(0, s.PacketsVerified);
            Assert.Equal(0, s.EventsAccepted);
            r.Advance(T(9));
            Assert.Empty(r.Views[0]);                                     // 검증된 패킷이 없으니 보낼 주소도 없다
            Assert.Equal(9U, r.Host.Status.NextConfirmTick);                      // sealed T 를 받지 않았다(마감 봉인만)

            // 태그를 지나면 받는다(위조 패킷이 재전송 창을 더럽히지 않았다: 같은 번호의 진짜 패킷이 들어간다)
            r.Host.OnPacket(good, T(9), Addr0);
            Assert.Equal(1, r.Host.StatsFor(S0).PacketsVerified);
            Assert.Equal(1, r.Host.StatsFor(S0).EventsAccepted);
        }

        // ── 재전송 (TV-N28)

        [Fact]
        public void S10_TV_N28__RetransmittedPacketNumberIsDropped()
        {
            var r = new HostRig();
            byte[] p1 = r.Send(0, T(8), T10 - 1, Ev(T10, 0, LogicalKey.Fire, true, 1));
            r.Host.OnPacket(p1, T(8) + 10, 0x9999UL);                      // 같은 번호 다시
            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(1, s.PacketsReplayed);
            Assert.Equal(1, s.PacketsVerified);
            Assert.Equal(1, s.EventsAccepted);

            // 창(64)보다 오래된 번호도 버린다
            byte[] old = r.Clients[0].Build(T10 - 1, new InputEvent[0], lastSeq: 1);
            for (int i = 0; i < 70; i++)
            {
                r.Clients[0].Send(r.Host, T(8) + 20, T10 - 1, new InputEvent[0], lastSeq: 1);
            }
            r.Host.OnPacket(old, T(8) + 30, 0x8888UL);
            Assert.Equal(2, r.Host.StatsFor(S0).PacketsReplayed);

            r.Advance(T(9));
            Assert.Equal(Addr0, r.Views[0].Last().ToAddress);              // 버린 재전송은 응답 주소를 바꾸지 못했다
        }

        // ── 응답 주소 (10.1)

        [Fact]
        public void ReplyAddressIsTheSourceOfTheLastAuthenticatedPacket()
        {
            var r = new HostRig();
            r.Advance(T(1));
            Assert.Empty(r.Views[0]);                                     // 검증된 패킷 전에는 보내지 않는다

            r.Clients[0].Send(r.Host, T(1), 0, new InputEvent[0], fromAddress: 0xA1UL);
            r.Advance(T(2));
            Assert.Equal(0xA1UL, r.Views[0].Last().ToAddress);

            r.Clients[0].Send(r.Host, T(2), 1, new InputEvent[0], fromAddress: 0xB2UL);   // NAT 가 포트를 바꿨다
            r.Advance(T(3));
            Assert.Equal(0xB2UL, r.Views[0].Last().ToAddress);

            byte[] forged = r.Clients[0].Build(2, new InputEvent[0]);
            forged[forged.Length - 2] ^= 0x40;
            r.Host.OnPacket(forged, T(3), 0xC3UL);                           // 태그가 틀렸다
            byte[] replay = r.Clients[0].Build(2, new InputEvent[0]);
            r.Host.OnPacket(replay, T(3), 0xB2UL);
            r.Host.OnPacket(replay, T(3), 0xD4UL);                           // 재전송
            r.Advance(T(4));
            Assert.Equal(0xB2UL, r.Views[0].Last().ToAddress);              // 검증에 실패한 패킷은 주소를 바꾸지 못한다

            Assert.Empty(r.Views[1]);                                     // 다른 사람의 주소와 섞이지 않는다
            Assert.True(r.Views[0].Select(v => v.PacketNumber).SequenceEqual(Enumerable.Range(1, r.Views[0].Count).Select(i => (ulong)i)));
        }

        // ── 왕복 (10.5, T-60)

        [Fact]
        public void MeasuredRttIsTheServerSampleWindowMinimum()
        {
            var r = new HostRig();
            Assert.Equal(-1, r.Host.MeasuredRttMs(S0));                      // 표본 없음
            r.Send(0, T(1), 0);
            r.Advance(T(1));
            uint serverMs = r.Views[0].Last().ServerTimeMs;
            long sentUs = serverMs * 1000L;
            FakeClient c = r.Clients[0];

            // 에코가 없는 패킷(0)은 표본이 아니다
            c.Send(r.Host, sentUs + 40_000, 0, new InputEvent[0]);
            Assert.Equal(-1, r.Host.MeasuredRttMs(S0));

            c.Send(r.Host, sentUs + 70_000 + 3_000, 0, new InputEvent[0], echoMs: serverMs, holdMs: 3);   // 왕복 70
            c.Send(r.Host, sentUs + 65_500, 0, new InputEvent[0], echoMs: serverMs, holdMs: 0);           // 65.5 → 올림 66
            c.Send(r.Host, sentUs + 80_000, 0, new InputEvent[0], echoMs: serverMs, holdMs: 0);
            Assert.Equal(66, r.Host.MeasuredRttMs(S0));
            Assert.Equal(-1, r.Host.MeasuredRttMs(S1));                      // 사람마다 따로

            // hold 가 RttHoldCapMs 를 넘는 표본(붙들어 왕복을 작게 보이려는 값)과 서버가 보낸 적 없는 시각의 에코는 버린다
            int cap = Db.Net.RttHoldCapMs;
            c.Send(r.Host, sentUs + 70_000 + (cap + 1) * 1000L, 0, new InputEvent[0], echoMs: serverMs, holdMs: (ushort)(cap + 1));
            c.Send(r.Host, sentUs + 100_000, 0, new InputEvent[0], echoMs: serverMs + 90, holdMs: 0);
            Assert.Equal(66, r.Host.MeasuredRttMs(S0));

            // 창의 크기는 ClockSyncSamples: 그만큼 새 표본이 들어오면 옛 최솟값은 밀려난다
            for (int i = 0; i < Db.Net.ClockSyncSamples; i++)
            {
                c.Send(r.Host, sentUs + 90_000, 0, new InputEvent[0], echoMs: serverMs, holdMs: 0);
            }
            Assert.Equal(90, r.Host.MeasuredRttMs(S0));

            // 태그가 틀린 패킷의 에코는 표본이 아니다
            byte[] forged = c.Build(0, new InputEvent[0], echoMs: serverMs, holdMs: 0);
            forged[forged.Length - 1] ^= 0x01;
            r.Host.OnPacket(forged, sentUs + 1_000, Addr0);
            Assert.Equal(90, r.Host.MeasuredRttMs(S0));
        }

        // ── 입력 지연 D (CONTRACTS 6.4 SetInputDelay, 10.5)

        [Fact]
        public void SetInputDelayDrivesTheEarlyCheckAndTheRecordHeader()
        {
            // 같은 이벤트(유효 틱 14, 오프셋 0)가 서버 시각 10틱에 닿는다(편도 표본 없음 = 0). 주장한 누른 시각 = 14 − D
            static long Accepted(int delay)
            {
                var r = new HostRig(delay, 3);
                r.Send(0, T(10), 0, Ev(14, 0, LogicalKey.Fire, true, 1));
                return r.Host.StatsFor(S0).EventsAccepted;
            }
            Assert.Equal(1, Accepted(4));        // 10 ≤ 10 + 1
            Assert.Equal(1, Accepted(3));        // 11 ≤ 11
            Assert.Equal(0, Accepted(2));        // 12 > 11: 이른 시각

            var rec = new HostRig(4, 3);
            rec.Advance(T(5));
            Wzdr.File f = Wzdr.Read(rec.Host.ConfirmedInputRecord());
            Assert.Equal(4, f.Body.InputDelayTicks0);
            Assert.Equal(3, f.Body.InputDelayTicks1);

            Assert.Throws<ArgumentOutOfRangeException>(() => rec.Host.SetInputDelay(S0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => rec.Host.SetInputDelay(S0, 256));
            Assert.Throws<ArgumentOutOfRangeException>(() => rec.Host.SetInputDelay(new Wizard.Core.PlayerSlot(2), 2));
        }
    }
}
