using System.Linq;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;
using Xunit;
using static Wizard.Netcode.Tests.Duel.Host.HostKit;
using static Wizard.Netcode.Tests.Duel.Host.HostRig;

namespace Wizard.Netcode.Tests.Duel.Host
{
    // spec 10 20.14 TV-N10~TV-N20, spec 01 14.2 T-I06·T-I13. 서버 수신의 봉인·검증 순서(CONTRACTS 10.6 정본, spec 10 14.6·14.8).
    // 시각은 경기 시계 µs(틱 T 의 시작 = T × 10^6 / 60 µs 올림). 입력 지연 D 는 따로 적지 않으면 두 사람 다 2.
    public sealed class HostValidationVectorTests
    {
        private const uint T10 = 10;

        // ── 늦은 시각과 봉인 (14.6, 14.8 "늦은 시각")

        [Fact]
        public void S10_TV_N10__EventArrivingAtTPlusPoint9TickIsAccepted()
        {
            var r = new HostRig();
            r.Send(0, At(T10, 9, 10), T10 - 1, Ev(T10, 0, LogicalKey.Fire, true, 1));
            r.Advance(At(T10, 9, 10));
            Assert.Equal(T10, r.Host.Status.NextConfirmTick);                    // T 는 아직 열려 있다(T + 1틱 전)
            r.Advance(T(T10 + 1));
            Assert.Equal(T10 + 1, r.Host.Status.NextConfirmTick);

            Assert.Equal(new[] { Ev(T10, 0, LogicalKey.Fire, true, 1) }, r.Record(0));
            Assert.Equal(new[] { Ev(T10, 0, LogicalKey.Fire, true, 1) }, r.Confirmed(0, T10, 0));
            Assert.Empty(r.Dropped(0));
            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(1, s.EventsAccepted);
            Assert.Equal(0, s.EventsLate);

            // 마감 1µs 전도 받는다
            var edge = new HostRig();
            edge.Send(0, T(T10 + 1) - 1, T10 - 1, Ev(T10, 0, LogicalKey.Fire, true, 1));
            edge.Advance(T(T10 + 1));
            Assert.Single(edge.Record(0));
        }

        [Fact]
        public void S10_TV_N11__EventArrivingAtTPlus1TickIsDroppedNotMovedAndNotified()
        {
            var r = new HostRig();
            r.Send(0, T(T10 + 1), T10 - 1, Ev(T10, 0, LogicalKey.Fire, true, 1));
            r.Advance(T(T10 + 3));

            Assert.Empty(r.Record(0));                                     // 다른 틱으로 옮기지 않는다: 어느 틱에도 없다
            for (uint t = 0; t < r.Host.Status.NextConfirmTick; t++)
            {
                Assert.Empty(r.Confirmed(0, t, 0) ?? new InputEvent[0]);
            }
            Assert.Equal(new[] { (1U, DuelDropReason.Late) }, r.DroppedSeqs(0));   // 본인 알림(본인 화면의 되감기는 클라이언트 몫, M2-NET-05)
            Assert.Empty(r.Dropped(1));                                    // 상대에게는 알리지 않는다
            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(1, s.EventsLate);
            Assert.Equal(0, s.EventsAccepted);
            Assert.Equal(0, s.OrderViolations);
        }

        [Fact]
        public void S10_TV_N12__NoPacketUntilTPlus1TickSealsTAsNoChange()
        {
            var r = new HostRig();
            r.Advance(T(T10 + 1) - 1);
            Assert.Equal(T10, r.Host.Status.NextConfirmTick);                     // T + 1틱 1µs 전: T 는 열려 있다
            r.Advance(T(T10 + 1));
            Assert.Equal(T10 + 1, r.Host.Status.NextConfirmTick);                 // T + 1틱: "변화 없음"으로 닫히고 확정된다
            Assert.Empty(Wzdr.Read(r.Host.ConfirmedInputRecord()).Inputs);
            Assert.Equal(T10 + 1, r.Host.StatsFor(S0).TicksSealedByDeadline);
            Assert.Equal(T10 + 1, r.Host.StatsFor(S1).TicksSealedByDeadline);
        }

        [Fact]
        public void S10_TV_N13__PacketWithSealedThroughTSealsImmediately()
        {
            var r = new HostRig();
            r.Send(0, T(2), T10);
            r.Send(1, T(2), T10);
            r.Advance(T(2));                                               // 마감(T + 1틱)보다 한참 이르다
            Assert.Equal(T10 + 1, r.Host.Status.NextConfirmTick);
            Assert.Equal(0, r.Host.StatsFor(S0).TicksSealedByDeadline);
            Assert.True(r.EverSent(0, T10));
            Assert.True(r.EverSent(1, T10));
        }

        [Fact]
        public void S10_TV_N14__OpponentTickIsNotSentBeforeMyTickIsSealed()
        {
            var r = new HostRig();
            InputEvent bFire = Ev(T10, 5, LogicalKey.Fire, true, 1);
            r.Send(1, T(9), T10, bFire);                                   // B 는 T 를 봉인했다
            r.Send(0, T(9), T10 - 1);                                      // A 는 T − 1 까지
            r.Advance(T(9) + 1000);

            Assert.Equal(T10, r.Host.Status.NextConfirmTick);
            Assert.False(r.EverSent(0, T10));                              // A 에게 B 의 T 를 보내지 않는다
            Assert.False(r.EverSent(1, T10));                              // 반대도 같다(확정 전이다)
            Assert.True(r.EverSent(0, T10 - 1));

            r.Send(0, T(9) + 2000, T10);                                   // A 가 T 를 봉인했다
            r.Advance(T(9) + 2000);
            Assert.Equal(T10 + 1, r.Host.Status.NextConfirmTick);
            Assert.Equal(new[] { bFire }, r.Confirmed(0, T10, 1));
            Assert.Equal(new[] { bFire }, r.Confirmed(1, T10, 1));
        }

        [Fact]
        public void S10_TV_N15__EventOfSealedTickIsDroppedAloneOthersAndLaterPacketsAccepted()
        {
            var r = new HostRig();
            r.Send(1, T(9), T10 + 2);
            r.Send(0, T(9), T10);                                          // A 가 T 를 봉인
            r.Send(0, At(9, 1, 2), T10,
                Ev(T10, 0, LogicalKey.Fire, true, 1),                      // 봉인된 T: 늦음
                Ev(T10 + 1, 0, LogicalKey.Up, true, 2));                   // 같은 패킷의 다른 이벤트: 받는다
            r.Send(0, At(9, 3, 4), T10 + 1,
                Ev(T10 + 1, 0, LogicalKey.Up, true, 2),                    // 겹쳐 보낸 것
                Ev(T10 + 1, 100, LogicalKey.Left, true, 3));               // 뒤따르는 패킷: 받는다
            r.Advance(T(T10 + 2));

            Assert.Equal(new uint[] { 2, 3 }, Seqs(r.Record(0)));
            Assert.Equal(new[] { (1U, DuelDropReason.Late) }, r.DroppedSeqs(0));
            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(0, s.OrderViolations);                            // 패킷을 버리지 않았다
            Assert.Equal(1, s.EventsLate);
            Assert.Equal(2, s.EventsAccepted);
            Assert.Equal(0, s.SeqsLost);                                   // 늦은 순번은 소비됐다(유실이 아니다)
        }

        // ── 순번 (10.6 (0)·(1))

        [Fact]
        public void S10_TV_N16__SeqGapIsConsumedAsLostReverseInsidePacketRejectsPacket()
        {
            SeqGapAndReverse(out HostRig r);
            Assert.Equal(1, r.Host.StatsFor(S0).OrderViolations);
        }

        [Fact]
        public void S01_T_I13__SeqGapIsLostAndPacketKeepsGoingReverseRejectsOnlyThatPacket()
        {
            // spec 01 T-I13 은 TV-N16 과 같은 규칙이다(CONTRACTS 0.3, 10.6). 건너뛴 순번 뒤의 이벤트도 같은 패킷에서 받고,
            // 역행 패킷 하나를 버린 뒤에도 다음 패킷은 정상으로 받는다
            SeqGapAndReverse(out HostRig r);
            Assert.Contains(r.Record(0), e => e.Seq == 7U && e.Key == LogicalKey.Skill);
            Assert.Contains(r.Record(0), e => e.Seq == 8U);
            Assert.Equal(new[] { (6U, DuelDropReason.Lost) }, r.DroppedSeqs(0));   // 읽지 못한 패킷은 알릴 순번이 없다
            Assert.Equal(1, r.Host.StatsFor(S0).PacketsMalformed);
        }

        // ClientInput 본문을 형식 그대로 쓴다. 다음 이벤트의 Δtick·Δseq 는 32비트에서 돈 값(앞보다 작으면 큰 수가 된다)
        private static byte[] ReversedBody(uint sealedThrough, uint baseSeq, params InputEvent[] events)
        {
            var w = new Wizard.Core.ByteWriter();
            w.U32(sealedThrough);
            w.U32(0);
            w.U32(0);
            w.U32(0);
            w.U16(0);
            w.U32(baseSeq);
            w.U8((byte)events.Length);
            for (int i = 0; i < events.Length; i++)
            {
                InputEvent e = events[i];
                if (i == 0)
                {
                    w.U32(e.Tick);
                    w.U8(e.Offset);
                    w.U8(e.PackKey());
                    w.U32(e.Seq);
                    continue;
                }
                w.VarUInt(unchecked(e.Tick - events[i - 1].Tick));
                w.U8(e.Offset);
                w.U8(e.PackKey());
                w.VarUInt(unchecked(e.Seq - events[i - 1].Seq));
            }
            return w.ToArray();
        }

        // 10.6 (0)의 나머지 꼴(형식으로 적을 수 있는 것): 순번이 늘지 않음(Δseq 0), 같은 틱에서 오프셋 역행, base_seq ≥ 첫 순번.
        // 셋 다 그 패킷만 버리고(봉인·순번·키 상태를 바꾸지 않는다) 계수 +1, 알림은 reason 4
        [Fact]
        public void OrderViolationFormsRejectOnlyThatPacketAndCount()
        {
            var r = new HostRig();
            r.Send(1, T(8), 20);
            r.SendBase(0, T(8), 15, 0, Ev(T10, 0, LogicalKey.Up, true, 1), Ev(T10, 5, LogicalKey.Left, true, 1));      // 순번이 늘지 않는다
            r.SendBase(0, T(8), 15, 0, Ev(T10, 50, LogicalKey.Up, true, 1), Ev(T10, 10, LogicalKey.Left, true, 2));    // 오프셋 역행
            r.SendBase(0, T(8), 15, 1, Ev(T10, 0, LogicalKey.Up, true, 1));                                            // base_seq ≥ 첫 순번
            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(3, s.OrderViolations);
            Assert.Equal(0, s.EventsAccepted);
            Assert.Equal(0, s.PacketsMalformed);
            Assert.Equal(1U, r.Host.ResumeInfo(S0).NextSeq);                // 순번을 소비하지 않았다

            r.Send(0, T(8) + 100, T10, Ev(T10, 0, LogicalKey.Up, true, 1));  // 그 뒤의 정상 패킷은 받는다
            r.Advance(T(9));
            Assert.Equal(T10 + 1, r.Host.Status.NextConfirmTick);                  // 버린 패킷의 sealed 15 는 봉인을 올리지 않았다
            Assert.Equal(new uint[] { 1 }, Seqs(r.Record(0)));
            Assert.All(r.DroppedSeqs(0), d => Assert.Equal(DuelDropReason.Order, d.reason));
            Assert.Equal(3, r.DroppedSeqs(0).Count);
        }

        // 순번 1~4 → 5, 7(6 이 빠짐) → 역행 패킷 [7, 5, 8](sealed 20) → 정상 패킷 [8]
        private static void SeqGapAndReverse(out HostRig r)
        {
            r = new HostRig();
            r.Send(1, T(8), 20);
            r.Send(0, T(8), T10 - 1,
                Ev(T10, 0, LogicalKey.Up, true, 1), Ev(T10, 1, LogicalKey.Up, false, 2),
                Ev(T10, 2, LogicalKey.Left, true, 3), Ev(T10, 3, LogicalKey.Left, false, 4));
            r.SendBase(0, T(8) + 100, T10 - 1, 4,
                Ev(T10, 10, LogicalKey.Fire, true, 5),
                Ev(T10, 20, LogicalKey.Skill, true, 7));                   // 5 다음 7

            DuelNetStats afterGap = r.Host.StatsFor(S0);
            Assert.Equal(0, afterGap.OrderViolations);                     // 몰수 계수 0
            Assert.Equal(1, afterGap.SeqsLost);
            Assert.Equal(6, afterGap.EventsAccepted);

            // 한 패킷 안에서 7 다음 5: 역행. 10.2 형식은 Δseq 를 부호 없는 varuint 로 적으므로 이 열은 부호기로 만들 수 없다(Unrepresentable).
            // 조작한 클라이언트가 보낼 수 있는 바이트는 32비트를 넘어 도는 Δseq 뿐이라 그것을 손으로 짠다
            byte[] reversed = r.Clients[0].SealRaw(ReversedBody(20, 6,
                Ev(T10 + 1, 0, LogicalKey.Up, true, 7),
                Ev(T10 + 1, 1, LogicalKey.Left, true, 5),
                Ev(T10 + 1, 2, LogicalKey.Down, true, 8)));
            r.Host.OnPacket(reversed, T(8) + 200, r.Clients[0].Address);
            Assert.Equal(1, r.Host.StatsFor(S0).OrderViolations);
            Assert.Equal(6, r.Host.StatsFor(S0).EventsAccepted);           // 그 패킷의 어느 이벤트도 받지 않았다

            r.Advance(T(9));
            Assert.Equal(T10, r.Host.Status.NextConfirmTick);                     // 버린 패킷의 sealed 20 은 봉인을 올리지 않았다(A 는 9 까지)

            r.SendBase(0, T(9) + 100, T10 + 1, 7, Ev(T10 + 1, 2, LogicalKey.Down, true, 8));
            r.Advance(T(T10 + 2));
            Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 7, 8 }, Seqs(r.Record(0)));
            Assert.Equal(1, r.Host.StatsFor(S0).SeqsLost);                 // 버린 패킷은 순번 8 을 소비하지 않았다
        }

        // ── 수량 (spec 10 14.8 "양", spec 01 T-I06)

        [Fact]
        public void S10_TV_N17__NinthEventInOneTickIsDropped()
        {
            var r = new HostRig();
            var events = new InputEvent[9];
            for (int i = 0; i < 9; i++)
            {
                events[i] = Ev(T10, (byte)(i * 10), LogicalKey.Up, i % 2 == 0, (uint)(i + 1));   // 누름·뗌을 번갈아(키 상태 모순 없음)
            }
            r.Send(1, T(8), T10);
            r.Send(0, T(8), T10, events);
            r.Advance(T(9));

            Assert.Equal(Enumerable.Range(1, 8).Select(i => (uint)i).ToArray(), Seqs(r.Record(0)));
            Assert.Equal(8, r.Confirmed(0, T10, 0)!.Length);
            Assert.Equal(new[] { (9U, DuelDropReason.Quantity) }, r.DroppedSeqs(0));
            Assert.Equal(1, r.Host.StatsFor(S0).EventsQuantity);
        }

        [Fact]
        public void S01_T_I06__DuelServerDropsFromTheNinthEventOfATickByQuantity()
        {
            // 8개의 서로 다른 키 누름 → 9번째(Up 뗌)·10번째(Left 뗌)는 수량(reason 3)으로 버려져 확정 열과 WZDR 에 없다.
            // 버린 순번도 소비했으므로 다음 틱의 순번 11 은 유실 없이 받는다
            var r = new HostRig();
            LogicalKey[] keys =
            {
                LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right,
                LogicalKey.Fire, LogicalKey.Skill, LogicalKey.Stance, LogicalKey.Reverse,
            };
            var events = keys.Select((k, i) => Ev(T10, (byte)(i * 20), k, true, (uint)(i + 1))).ToList();
            events.Add(Ev(T10, 200, LogicalKey.Up, false, 9));
            events.Add(Ev(T10, 210, LogicalKey.Left, false, 10));
            r.Send(1, T(8), T10 + 1);
            r.Send(0, T(8), T10, events.ToArray());
            r.Send(0, T(8) + 100, T10 + 1, Ev(T10 + 1, 0, LogicalKey.Up, false, 11));
            r.Advance(T(9));

            InputEvent[] tick10 = r.Confirmed(0, T10, 0)!;
            Assert.Equal(8, tick10.Length);
            Assert.All(tick10, e => Assert.True(e.Down));
            Assert.DoesNotContain(r.Record(0), e => e.Seq == 9U || e.Seq == 10U);
            Assert.Equal(new uint[] { 1, 2, 3, 4, 5, 6, 7, 8, 11 }, Seqs(r.Record(0)).OrderBy(s => s).ToArray());
            Assert.Equal(new[] { (9U, DuelDropReason.Quantity), (10U, DuelDropReason.Quantity) }, r.DroppedSeqs(0));
            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(2, s.EventsQuantity);
            Assert.Equal(0, s.SeqsLost);
            Assert.Equal(0, s.OrderViolations);
        }

        // ── 키 상태 (14.8 "키 상태")

        [Fact]
        public void S10_TV_N18__PressOfHeldFireIsIgnoredSilently()
        {
            var r = new HostRig();
            r.Send(1, T(8), T10);
            r.Send(0, T(8), T10,
                Ev(T10, 0, LogicalKey.Fire, true, 1),
                Ev(T10, 10, LogicalKey.Fire, true, 2),                     // 이미 눌림: 무시
                Ev(T10, 20, LogicalKey.Fire, false, 3),
                Ev(T10, 30, LogicalKey.Up, false, 4));                     // 안 눌린 키의 뗌: 무시
            r.Advance(T(9));

            Assert.Equal(new uint[] { 1, 3 }, Seqs(r.Record(0)));
            Assert.Empty(r.Dropped(0));                                    // 알리지 않는다
            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(2, s.EventsKeyStateIgnored);
            Assert.Equal(0, s.SeqsLost);                                   // 순번은 소비됐다
            Assert.Equal(0, r.Host.ResumeInfo(S0).KeyState);               // Fire 는 뗀 상태
            Assert.Equal(5U, r.Host.ResumeInfo(S0).NextSeq);
        }

        // ── 이른 시각 (14.8 "이른 시각", future_claim_tolerance 1틱)

        [Theory]
        [InlineData(98, 128, true)]     // 98.5틱 ≤ 100 − 1.8 + 1 = 99.2 → 받음
        [InlineData(99, 77, false)]     // 99.3틱(99 + 77/256) > 99.2 → 버림
        [InlineData(99, 51, true)]      // 99 + 51/256 = 99.199… : 경계 안(입력 시각 25,395 ≤ 25,395)
        [InlineData(99, 52, false)]     // 입력 시각 25,396 > 25,395
        public void S10_TV_N19__FutureClaimBeyondArrivalMinusOneWayPlusOneTickIsDropped(int pressTick, int offset, bool accepted)
        {
            var r = new HostRig();
            GiveRtt(r, 0, 60);                                             // 편도 추정 30ms(1.8틱)
            Assert.Equal(60, r.Host.MeasuredRttMs(S0));

            uint eff = (uint)pressTick + 2;                                // 주장한 누른 틱 = 유효 틱 − D
            r.Send(0, T(100), 0, Ev(eff, (byte)offset, LogicalKey.Fire, true, 1));
            r.Advance(T(100));

            DuelNetStats s = r.Host.StatsFor(S0);
            Assert.Equal(accepted ? 1 : 0, s.EventsAccepted);
            Assert.Equal(accepted ? 0 : 1, s.EventsEarly);
            Assert.Equal(accepted ? new (uint, DuelDropReason)[0] : new[] { (1U, DuelDropReason.Early) }, r.DroppedSeqs(0));
        }

        // 슬롯 slot 의 왕복 표본 하나 = rttMs: 서버가 보낸 ServerTick 의 server_time_ms 를 hold 0 으로 되돌려 rttMs 뒤에 닿는다
        internal static void GiveRtt(HostRig r, int slot, int rttMs)
        {
            r.Send(slot, T(1), 0);
            r.Advance(T(1));
            uint serverMs = r.Views[slot].Last().ServerTimeMs;
            r.Clients[slot].Send(r.Host, serverMs * 1000L + rttMs * 1000L, 0, new InputEvent[0], echoMs: serverMs, holdMs: 0);
        }

        // ── 틱 안 순서 (14.2, 14.8 "틱 안 순서")

        [Fact]
        public void S10_TV_N20__SameTickAndOffsetServerSortsDirectionBeforeK()
        {
            var r = new HostRig();
            r.Send(1, T(8), T10);
            r.SendBase(0, T(8), T10, 9,
                Ev(T10, 50, LogicalKey.Skill, true, 10),                   // K(순번 10)
                Ev(T10, 50, LogicalKey.Up, true, 11));                     // 앞(순번 11), 같은 (틱, 오프셋)
            r.Advance(T(9));

            Assert.Equal(T10 + 1, r.Host.Status.NextConfirmTick);
            Assert.Equal(new[] { Ev(T10, 50, LogicalKey.Up, true, 11), Ev(T10, 50, LogicalKey.Skill, true, 10) },
                         r.Confirmed(0, T10, 0));                          // 확정 순서(= Step 에 넘긴 InputOrder 순): 앞 → K
            Assert.Equal(new[] { Ev(T10, 50, LogicalKey.Up, true, 11), Ev(T10, 50, LogicalKey.Skill, true, 10) },
                         r.Confirmed(1, T10, 0));
            Assert.Equal(0, r.Host.StatsFor(S0).OrderViolations);          // 한 패킷 안의 역행이 아니다(시각은 같다)
            Assert.All(r.DroppedSeqs(0), d => Assert.Equal(DuelDropReason.Lost, d.reason));   // base_seq 9: 순번 1~9 의 유실 소비뿐이다

            // WZDR 도 같은 열을 싣는다(DECISIONS-TECH T-64: 순번은 한 슬롯 안에서 늘어나기만 하지 않는다)
            Assert.Equal(new[] { Ev(T10, 50, LogicalKey.Up, true, 11), Ev(T10, 50, LogicalKey.Skill, true, 10) }, r.Record(0));
            Wzdr.File file = Wzdr.Read(r.Host.ConfirmedInputRecord());
            Assert.Equal(file.Body.FinalStateHash, Wzdr.Replay(file).final);
        }
    }
}
