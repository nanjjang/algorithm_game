using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Netcode.Tests.Duel.Host;
using Wizard.Netcode.Tests.NetSim;
using Wizard.Sim;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Matrix
{
    // NetSim 경기 하나가 끝난 뒤의 공통 판정. 기대값은 명세의 성질(spec 10 14.6·14.7, CONTRACTS 6.4)에서 온다:
    //   확정 해시 열 = 호스트, 되감기 ≤ 한도, 해시 불일치·전체 상태 0, 몰수 계수 0,
    //   받아들여진 이벤트의 시각 = 누른 기기가 찍은 시각(다른 틱으로 옮기지 않는다).
    //   Retracted 뒤 같은 열쇠의 Confirmed 없음(RetractedNeverConfirmedLater)은 All 에 넣지 않고 따로 판정한다(NetcodeMatrixTests 의 별도 시험)
    internal static class MatrixChecks
    {
        public static void All(NetSimMatch m, int minConfirmedTicks)
        {
            ConfirmedHashesMatchHost(m, minConfirmedTicks);
            RollbackWithinLimit(m);
            NoForfeitCount(m);
            AcceptedEventsKeepPressedTime(m);
            EverySentEventIsAcceptedNotifiedOrIgnored(m);
            PredictedWorldMatchesHost(m);
        }

        public static void ConfirmedHashesMatchHost(NetSimMatch m, int minConfirmedTicks)
        {
            var (perTick, _, _) = m.HostReference();
            for (int s = 0; s < 2; s++)
            {
                ClientTrace t = m.Trace[s];
                Assert.True(t.ConfirmedHashes.Count >= minConfirmedTicks,
                            $"slot {s}: only {t.ConfirmedHashes.Count} confirmed ticks (want ≥ {minConfirmedTicks})");
                Assert.True(t.ConfirmedHashes.Count <= perTick.Count, $"slot {s}: client confirmed past the host");
                for (int i = 0; i < t.ConfirmedHashes.Count; i++)
                {
                    Assert.True(perTick[i] == t.ConfirmedHashes[i], $"slot {s}: confirmed hash differs from host at tick {i}");
                }
                Assert.Equal(0, m.Clients[s].HashMismatchCount);
                Assert.False(t.EverNeedsFullState, $"slot {s}: requested full state");
                Assert.False(m.Clients[s].NeedsFullState);
            }
        }

        // 예측 세계가 다시 돌린 결과 = 호스트. 끝(입력이 없는 꼬리)에서 각 클라이언트의 예측 세계(View)를, 호스트의 확정 열로 같은 틱까지
        // 돌린 세계와 견준다(확정 뒤의 틱은 입력이 없다). 되감기가 빠지거나 틀린 상태에서 다시 돌리면 여기서 어긋난다
        public static void PredictedWorldMatchesHost(NetSimMatch m)
        {
            Wzdr.File file = Wzdr.Read(m.Host.ConfirmedInputRecord());
            for (int s = 0; s < 2; s++)
            {
                uint predicted = m.Trace[s].Advances[m.Trace[s].Advances.Count - 1].advance.PredictedTick;
                SimWorld w = SimWorld.CreateDuel(HostKit.Db, SimConfig.ForDuel(HostKit.Db, file.Setup.Options), file.Setup);
                int at = 0;
                var buf = new PlayerInput[16];
                while (w.Tick < predicted)
                {
                    int n = 0;
                    while (at < file.Inputs.Count && file.Inputs[at].Event.Tick == w.Tick)
                    {
                        buf[n++] = file.Inputs[at++];
                    }
                    w.Step(buf.AsSpan(0, n), ReadOnlySpan<RunCommand>.Empty, Wizard.Netcode.Tests.Duel.Client.NullSink.Instance);
                }
                Assert.Equal(file.Inputs.Count, at);
                for (int k = 0; k < 2; k++)
                {
                    var slot = new PlayerSlot((byte)k);
                    Assert.Equal(Summ(w.View.Player(slot)), Summ(m.Clients[s].View.Player(slot)));
                }
            }
        }

        private static string Summ(Wizard.Sim.Events.PlayerView p) =>
            $"{p.Pos.X.Raw},{p.Pos.Y.Raw} f{p.Facing} st{p.Stance} hp{p.HpPoints} heat{p.HeatHu} lock{p.LockRemainTicks} k{(int)p.Keys} c{p.Combo} cr{p.Crystals}";

        public static int MaxRollback(NetSimMatch m, int slot) =>
            m.Trace[slot].Advances.Count == 0 ? 0 : m.Trace[slot].Advances.Max(a => a.advance.RollbackDepth);

        public static void RollbackWithinLimit(NetSimMatch m)
        {
            int limit = HostKit.Db.Net.RollbackLimitTicks;
            for (int s = 0; s < 2; s++)
            {
                foreach ((uint clock, DuelAdvance a) in m.Trace[s].Advances)
                {
                    Assert.True(a.RollbackDepth <= limit, $"slot {s}: rollback depth {a.RollbackDepth} at input clock {clock}");
                    Assert.True((long)a.PredictedTick - a.ConfirmedTick <= limit, $"slot {s}: predicted {a.PredictedTick} confirmed {a.ConfirmedTick}");
                }
            }
        }

        public static void NoForfeitCount(NetSimMatch m)
        {
            for (int s = 0; s < 2; s++)
            {
                DuelNetStats st = m.Host.StatsFor(new PlayerSlot((byte)s));
                Assert.Equal(0L, st.OrderViolations);
                Assert.Equal(0L, st.PacketsMalformed);
                Assert.Equal(0L, st.PacketsUnauthentic);
            }
        }

        // CONTRACTS 6.4: 열쇠 (Tick, Type, Slot, Ordinal). Retracted 가 난 열쇠는 그 뒤 Confirmed 로 나오지 않는다
        public static void RetractedNeverConfirmedLater(NetSimMatch m)
        {
            for (int s = 0; s < 2; s++)
            {
                var retracted = new HashSet<(uint, int, byte, ushort)>();
                var violations = new List<string>();
                foreach (PresentationEvent e in m.Trace[s].Events)
                {
                    var key = (e.Event.Tick, (int)e.Event.Type, e.Event.Slot.Value, e.Ordinal);
                    if (e.Phase == EventPhase.Retracted)
                    {
                        retracted.Add(key);
                    }
                    else if (e.Phase == EventPhase.Confirmed && retracted.Contains(key))
                    {
                        violations.Add(key.ToString());
                    }
                }
                Assert.True(violations.Count == 0,
                            $"slot {s}: {violations.Count} keys were Retracted and later Confirmed, first {string.Join(" ", violations.Take(3))}");
            }
        }

        // 확정 열의 이벤트는 그 사람이 낸 이벤트 그대로다(같은 순번, 같은 유효 틱·오프셋·키·누름). 누른 키는 모두 그 시각 그대로 나갔다
        public static void AcceptedEventsKeepPressedTime(NetSimMatch m)
        {
            Wzdr.File file = Wzdr.Read(m.Host.ConfirmedInputRecord());
            for (int s = 0; s < 2; s++)
            {
                ClientTrace t = m.Trace[s];
                foreach (PlayerInput i in file.Inputs.Where(x => x.Slot.Value == s))
                {
                    Assert.True(t.SentBySeq.TryGetValue(i.Event.Seq, out InputEvent sent), $"slot {s}: seq {i.Event.Seq} was never sent");
                    Assert.Equal(sent, i.Event);
                }
                var sentList = t.SentBySeq.Values.ToList();
                foreach ((long _, QuantizedKey q) in t.Pressed)
                {
                    Assert.Contains(sentList, e => e.Time == q.Time && e.Key == q.Key && e.Down == q.Down);
                }
            }
        }

        // 이 사람이 누른 키 가운데 호스트가 버렸다고 알린 것(늦음·이른 시각·수량·유실·순서, CONTRACTS 10.6)의 수. 보정 이벤트는 세지 않는다.
        // 키 상태 모순(눌린 키의 누름, 안 눌린 키의 뗌)은 버림이 아니라 무시다(알리지 않고, 서버의 키 상태가 이미 그 값이다, spec 10 14.8)
        public static int DroppedPresses(NetSimMatch m, int slot, long pressedFromUs = long.MinValue)
        {
            uint end = Wzdr.Read(m.Host.ConfirmedInputRecord()).Body.EndTick;
            ClientTrace t = m.Trace[slot];
            int dropped = 0;
            foreach ((long at, QuantizedKey q) in t.Pressed)
            {
                if (at < pressedFromUs || q.Tick >= end)
                {
                    continue;
                }
                InputEvent e = t.SentBySeq.Values.First(x => x.Time == q.Time && x.Key == q.Key && x.Down == q.Down);
                if (t.DropNotices.ContainsKey(e.Seq))
                {
                    dropped++;
                }
            }
            return dropped;
        }

        // 확정 열에 든 이벤트와 버림 알림을 합치면 그 사람이 낸 순번(end 앞의 틱) 가운데 키 상태 모순으로 무시된 것만 빠진다:
        // 알림 없이 사라진 이벤트가 없다
        public static void EverySentEventIsAcceptedNotifiedOrIgnored(NetSimMatch m)
        {
            Wzdr.File file = Wzdr.Read(m.Host.ConfirmedInputRecord());
            for (int s = 0; s < 2; s++)
            {
                var accepted = new HashSet<uint>(file.Inputs.Where(x => x.Slot.Value == s).Select(x => x.Event.Seq));
                ClientTrace t = m.Trace[s];
                long silent = t.SentBySeq.Values.Count(e => e.Tick < file.Body.EndTick && !accepted.Contains(e.Seq) && !t.DropNotices.ContainsKey(e.Seq));
                Assert.Equal(m.Host.StatsFor(new PlayerSlot((byte)s)).EventsKeyStateIgnored, silent);
            }
        }
    }
}
