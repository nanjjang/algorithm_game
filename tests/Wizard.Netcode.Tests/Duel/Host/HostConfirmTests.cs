using System.Collections.Generic;
using System.Linq;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;
using static Wizard.Netcode.Tests.Duel.Host.HostKit;
using static Wizard.Netcode.Tests.Duel.Host.HostRig;

namespace Wizard.Netcode.Tests.Duel.Host
{
    // 확정 열, 30틱 해시 방송, WZDR, 같은 입력 열 → 같은 출력 열, 겹쳐 보내기·연속 유실, 수신 시각의 계약
    // (spec 10 14.6·14.8·20.14, CONTRACTS 6.4·10.3·10.6·11.1)
    public sealed class HostConfirmTests
    {
        private static HostMatch Play(int seed, uint ticks, System.Action<HostMatch>? setup = null)
        {
            var m = new HostMatch(seed);
            setup?.Invoke(m);
            m.Start();
            m.Run(ticks);
            return m;
        }

        // ── 같은 입력 열이면 같은 출력 열 (소켓과 시계를 모른다)

        [Fact]
        public void SamePacketAndTimeSequenceGivesSameOutputBytesAndRecord()
        {
            void Lossy(HostMatch m)
            {
                m.OneWayUs[1] = 21000;
                m.DropPacket = (slot, n) => (slot == 0 && n % 17 == 5) || (slot == 1 && n % 23 == 3);
            }
            HostMatch a = Play(7, 240, Lossy);
            HostMatch b = Play(7, 240, Lossy);

            Assert.True(a.Outgoing.Count > 400);
            Assert.Equal(a.Outgoing.Count, b.Outgoing.Count);
            for (int i = 0; i < a.Outgoing.Count; i++)
            {
                Assert.Equal(a.Outgoing[i], b.Outgoing[i]);
            }
            Assert.Equal(a.Host.ConfirmedInputRecord().ToArray(), b.Host.ConfirmedInputRecord().ToArray());
            Assert.Equal(a.Host.StatsFor(S0), b.Host.StatsFor(S0));
            Assert.Equal(a.Host.StatsFor(S1), b.Host.StatsFor(S1));

            // 다른 씨앗(다른 입력 열)이면 다르다: 시험이 실제로 입력을 싣는다
            HostMatch c = Play(8, 240, Lossy);
            Assert.NotEqual(a.Host.ConfirmedInputRecord().ToArray(), c.Host.ConfirmedInputRecord().ToArray());
        }

        // ── WZDR

        [Fact]
        public void S10_TV_N24__SameMatchRecordReplayedTwiceGivesSameHashes()
        {
            HostMatch m = Play(11, 200);
            Wzdr.File file = Wzdr.Read(m.Host.ConfirmedInputRecord());
            var (first, finalA) = Wzdr.Replay(file);
            var (second, finalB) = Wzdr.Replay(file);
            Assert.Equal(first, second);
            Assert.Equal(finalA, finalB);
            Assert.True(file.Inputs.Count > 50, "the match must confirm inputs");
        }

        [Fact]
        public void ConfirmedInputRecordReplaysToTheHostFinalHash()
        {
            HostMatch m = Play(13, 300);
            byte[] bytes = m.Host.ConfirmedInputRecord().ToArray();
            Wzdr.File file = Wzdr.Read(bytes);

            // 머리말 = 호스트의 DuelSetup, input_delay = SetInputDelay 값, 꼬리말 = 확정한 틱 수와 그때의 해시
            Assert.Equal(m.Host.Status.NextConfirmTick, file.Body.EndTick);
            Assert.Equal(m.Delay[0], file.Body.InputDelayTicks0);
            Assert.Equal(m.Delay[1], file.Body.InputDelayTicks1);
            Assert.Equal(Setup().MatchId, file.Setup.MatchId);

            // 다시 돌린 최종 해시 = 꼬리말의 해시(호스트 세계의 해시) = 호스트가 방송한 해시
            var (afterStep, final) = Wzdr.Replay(file);
            Assert.Equal(file.Body.FinalStateHash, final);
            ServerView lastHash = m.Views[0].Last(v => v.HasHash);
            Assert.Equal(lastHash.Hash, afterStep[(int)lastHash.HashTick]);

            // 확정 열 = 두 사람이 낸 이벤트 가운데 받은 것 전부(이 경기는 유실이 없다)
            for (int s = 0; s < 2; s++)
            {
                var sent = m.Sent[s].Where(e => e.Tick < file.Body.EndTick).ToList();
                var confirmed = file.Inputs.Where(i => i.Slot.Value == s).Select(i => i.Event).OrderBy(e => e.Seq).ToList();
                Assert.Equal(sent, confirmed);
            }

            // 공개 문 ReplayRunner.RunDuel: 경기 끝 규칙의 모듈(M14-DUL-01)이 아직 없어 세계가 끝나지 않으므로 end_tick 에서 EndedLate 다.
            // 머리말을 읽어 두 사람 세계를 만들고 확정 입력을 end_tick 까지 넣었다는 뜻이다(RecordCorrupt 가 아니다)
            DuelReplayResult r = ReplayRunner.RunDuel(Db, bytes, Wzdr.Generous);
            Assert.Equal(ReplayStatus.EndedLate, r.Status);
            Assert.Equal(file.Body.EndTick, r.TicksSimulated);

            // 경기 중에 다시 불러도 확정이 늘지 않았으면 같은 바이트다
            Assert.Equal(bytes, m.Host.ConfirmedInputRecord().ToArray());
        }

        // ── 해시 방송

        [Fact]
        public void S10_TV_N25__HashIsBroadcastAtTicks30_60_90AndNotAt31()
        {
            HostMatch m = Play(17, 100);
            Wzdr.File file = Wzdr.Read(m.Host.ConfirmedInputRecord());
            var (afterStep, _) = Wzdr.Replay(file);

            for (int s = 0; s < 2; s++)
            {
                var hashed = m.Views[s].Where(v => v.HasHash).ToList();
                Assert.Equal(new uint[] { 0, 30, 60, 90 }, hashed.Select(v => v.HashTick).ToArray());
                Assert.DoesNotContain(m.Views[s], v => v.HasHash && v.HashTick == 31U);
                foreach (ServerView v in hashed)
                {
                    Assert.Equal(afterStep[(int)v.HashTick], v.Hash);      // 해시의 틱 = 처리한 틱(그 틱의 Step 뒤)
                    Assert.True(v.HasTick(v.HashTick) || v.ConfirmedFrom > v.HashTick);   // 그 틱의 확정과 같이, 또는 그 뒤에 실린다
                }
            }
            Assert.Equal(m.Views[0].Where(v => v.HasHash).Select(v => v.Hash), m.Views[1].Where(v => v.HasHash).Select(v => v.Hash));
        }

        // ── 겹쳐 보내기와 유실 (spec 10 14.3, CONTRACTS 10.6)

        [Fact]
        public void S10_TV_N29__OneLostPacketIsCoveredByTheEightTickOverlap()
        {
            HostMatch m = Play(19, 120, x =>
            {
                x.DropPacket = (slot, n) => slot == 0 && n == 50;
                x.Keys = (slot, n) => slot == 0 && n == 50
                    ? new List<(byte, LogicalKey)> { (40, LogicalKey.Fire), (90, LogicalKey.Right) }
                    : null;
            });
            List<InputEvent> record = Wzdr.Read(m.Host.ConfirmedInputRecord()).Inputs.Where(i => i.Slot.Value == 0).Select(i => i.Event).ToList();

            InputEvent[] lostPacketEvents = m.Sent[0].Where(e => e.Tick == 50U + (uint)m.Delay[0]).ToArray();
            Assert.Equal(2, lostPacketEvents.Length);
            Assert.All(lostPacketEvents, e => Assert.Contains(e, record));       // 다음 패킷이 메웠다
            Assert.Equal(m.Sent[0].Where(e => e.Tick < m.Host.Status.NextConfirmTick), record.OrderBy(e => e.Seq));
            Assert.All(m.Views[0], v => Assert.Empty(v.Dropped));               // 버려진 입력 없음
            DuelNetStats s = m.Host.StatsFor(S0);
            Assert.Equal(0, s.SeqsLost);
            Assert.Equal(0, s.EventsLate);
            Assert.Equal(0, s.OrderViolations);
        }

        [Fact]
        public void TwoHundredMsBurstLossWhilePressingAndReleasing_NoForfeitLostNoticeThenNormal()
        {
            // 입력 시계 40~51(12틱 = 200ms)의 슬롯 0 패킷이 다 사라진다. 그 사이 42 에 Fire 를 누르고 45 에 뗀다(D 4: 유효 틱 46, 49).
            // 52 의 패킷은 유효 틱 48 이상만 싣는다(겹쳐 보내는 창 밖으로 누름이 밀려났다): 누름은 base_seq 로 유실(reason 5),
            // 뗌은 이미 봉인된 49 라 늦음(reason 1). 몰수 계수는 0 이고 그 뒤의 입력은 다 받는다
            uint press = 42, release = 45, lossFrom = 40, lossTo = 52;
            HostMatch m = Play(23, 140, x =>
            {
                x.DropPacket = (slot, n) => slot == 0 && n >= lossFrom && n < lossTo;
                x.Keys = (slot, n) =>
                {
                    if (slot != 0 || n < lossFrom - 2 || n >= lossTo + 4)
                    {
                        return null;
                    }
                    var keys = new List<(byte, LogicalKey)>();
                    if (n == press || n == release)
                    {
                        keys.Add((100, LogicalKey.Fire));
                    }
                    return keys;
                };
            });
            InputEvent pressEv = m.Sent[0].Single(e => e.Tick == press + 4U);
            InputEvent releaseEv = m.Sent[0].Single(e => e.Tick == release + 4U);
            Assert.True(pressEv.Down && !releaseEv.Down);

            var drops = m.Views[0].SelectMany(v => v.Dropped)
                .SelectMany(d => Enumerable.Range(0, d.Run + 1).Select(i => (seq: d.Seq + (uint)i, d.Reason))).ToList();
            Assert.Contains((pressEv.Seq, DuelDropReason.Lost), drops);
            Assert.Contains((releaseEv.Seq, DuelDropReason.Late), drops);
            Assert.DoesNotContain(drops, d => d.Reason == DuelDropReason.Order);

            DuelNetStats s = m.Host.StatsFor(S0);
            Assert.Equal(0, s.OrderViolations);                                  // 몰수 0
            Assert.Equal(0, m.Host.StatsFor(S1).OrderViolations);

            var record = Wzdr.Read(m.Host.ConfirmedInputRecord()).Inputs.Where(i => i.Slot.Value == 0).Select(i => i.Event).ToList();
            Assert.DoesNotContain(pressEv, record);
            Assert.DoesNotContain(releaseEv, record);
            var after = m.Sent[0].Where(e => e.Tick >= lossTo + 4U && e.Tick < m.Host.Status.NextConfirmTick).ToList();
            Assert.True(after.Count > 10, "random input after the burst");
            Assert.All(after, e => Assert.Contains(e, record));                // 그 뒤 입력은 정상으로 받는다
            Assert.Equal(m.Sent[1].Where(e => e.Tick < m.Host.Status.NextConfirmTick),
                         Wzdr.Read(m.Host.ConfirmedInputRecord()).Inputs.Where(i => i.Slot.Value == 1).Select(i => i.Event).OrderBy(e => e.Seq));
        }

        // ── 수신 시각의 계약 (CONTRACTS 6.4 OnPacket·Advance)

        [Fact]
        public void ReceiveTimeIsTheArgumentAndCallerDrainsBeforeAdvance()
        {
            // 패킷이 기기에 닿은 시각은 T + 0.9틱인데 서버 루프가 늦어 T + 1.2틱에 처리한다. 수신 시각을 인자로 받으므로 늦음이 아니다
            const uint t = 10;
            InputEvent fire = Ev(t, 0, LogicalKey.Fire, true, 1);
            var drained = new HostRig();
            drained.Send(1, T(2), 30);
            byte[] packet = drained.Clients[0].Build(t - 1, new[] { fire });
            drained.Host.OnPacket(packet, At(t, 9, 10), drained.Clients[0].Address);   // 버퍼를 다 비운 뒤에
            drained.Advance(At(t + 1, 1, 5));                                          // Advance
            Assert.Equal(new[] { fire }, drained.Record(0));
            Assert.Empty(drained.Dropped(0));

            // 호출 순서를 어기면(먼저 Advance) 그 사이 T 가 닫혀 같은 패킷이 늦음이 된다. 그래서 계약이 "다 비운 뒤에"다
            var early = new HostRig();
            early.Send(1, T(2), 30);
            byte[] same = early.Clients[0].Build(t - 1, new[] { fire });
            early.Advance(At(t + 1, 1, 5));
            early.Host.OnPacket(same, At(t, 9, 10), early.Clients[0].Address);
            early.Advance(At(t + 1, 1, 5));
            Assert.Empty(early.Record(0));
            Assert.Equal(new[] { (1U, DuelDropReason.Late) }, early.DroppedSeqs(0));

            // 큐에 밀린 패킷 여럿: 닿은 시각이 다 마감 안이면 처리 시각과 상관없이 받는다
            var queued = new HostRig();
            queued.Send(1, T(2), 30);
            var packets = new List<(byte[] bytes, long at)>();
            for (uint n = 0; n < 5; n++)
            {
                packets.Add((queued.Clients[0].Build(t + n - 1, new[] { Ev(t + n, 0, LogicalKey.Up, n % 2 == 0, n + 1) }), At(t + n, 1, 2)));
            }
            foreach (var p in packets)
            {
                queued.Host.OnPacket(p.bytes, p.at, queued.Clients[0].Address);
            }
            queued.Advance(T(t + 9));
            Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, Seqs(queued.Record(0)));
        }

        // ── 무작위 패킷에 예외가 없다(받기·진행·보내기)

        [Fact]
        public void RandomAuthenticPacketsNeverThrowAndNeverExceedEightPerTick()
        {
            var rng = new System.Random(29);
            var r = new HostRig(3, 2);
            long now = 0;
            for (int i = 0; i < 600; i++)
            {
                int slot = rng.Next(0, 2);
                int count = rng.Next(0, 12);
                var events = new InputEvent[count];
                uint seq = (uint)rng.Next(0, 40);
                uint tick = (uint)rng.Next(0, 80);
                for (int k = 0; k < count; k++)
                {
                    seq += (uint)rng.Next(0, 3);
                    tick += (uint)rng.Next(0, 2);
                    events[k] = Ev(tick, (byte)rng.Next(0, 256), (LogicalKey)rng.Next(0, 8), rng.Next(0, 2) == 0, seq);
                }
                now += rng.Next(0, 9000);
                uint sealedThrough = (uint)rng.Next(0, 90);
                r.Clients[slot].Send(r.Host, now - rng.Next(0, 30000), sealedThrough, events, (uint)rng.Next(0, 40));
                if (rng.Next(0, 3) == 0)
                {
                    r.Advance(now);
                }
            }
            r.Advance(now + 2_000_000);
            Assert.True(r.Host.Status.NextConfirmTick > 100U);
            foreach (ServerView v in r.Views[0].Concat(r.Views[1]))
            {
                foreach (InputEvent[][] tickEvents in v.Ticks)
                {
                    Assert.True(tickEvents[0].Length <= 8 && tickEvents[1].Length <= 8);
                }
            }
        }
    }
}
