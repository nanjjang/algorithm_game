using System.Collections.Generic;
using System.Linq;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Sim.Events;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Netcode.Tests.Duel.Client
{
    // M2-NET-05 수락 조건: 이벤트 장부(CONTRACTS 6.4 "표시 이벤트의 단계"). 열쇠 (Tick, Type, Slot, Ordinal), EntityId 없음.
    public sealed class DuelClientLedgerTests
    {
        private static KeyMask Bit(LogicalKey k) => (KeyMask)(1 << (int)k);

        private static List<PresentationEvent> Of(ClientRig rig, SimEventType type, EventPhase phase) =>
            rig.Events.Where(e => e.Event.Type == type && e.Phase == phase).ToList();

        // 내 자세 누름(유효 틱 5): 예측 세계에서 Predicted, 확정되면 Confirmed. 번복 없음. 같은 확정이 다시 와도 Confirmed 는 한 번
        [Fact]
        public void PredictedThenConfirmed_ConfirmedOnlyOnce()
        {
            var rig = new ClientRig(me: 0, inputDelay: 2);
            rig.Client.Advance(3);
            rig.Client.OnLocalInput(new[] { ClientRig.Q(5, LogicalKey.Stance, true) }, Bit(LogicalKey.Stance));
            for (uint n = 4; n <= 7; n++)
            {
                rig.Client.Advance(n);
            }
            rig.Drain();
            PresentationEvent predicted = Assert.Single(Of(rig, SimEventType.StanceEntered, EventPhase.Predicted));
            Assert.Equal(5u, predicted.Event.Tick);
            Assert.Equal((byte)0, predicted.Event.Slot.Value);
            Assert.Equal((ushort)0, predicted.Ordinal);

            var ticks = new List<ConfirmedTick>();
            for (uint t = 0; t <= 6; t++)
            {
                ticks.Add(t == 5 ? rig.StepServer(slot0: new[] { ClientRig.Ev(5, LogicalKey.Stance, true, 1) }) : rig.StepServer());
            }
            rig.Deliver(rig.ServerTick(ticks));
            rig.Deliver(rig.ServerTick(ticks));                      // ack 가 닿기 전에 같은 확정이 다시 왔다(새 패킷 번호)
            rig.Client.Advance(8);
            rig.Drain();

            Assert.Single(Of(rig, SimEventType.StanceEntered, EventPhase.Confirmed));
            Assert.DoesNotContain(rig.Events, e => e.Phase == EventPhase.Retracted);
            Assert.Single(Of(rig, SimEventType.StanceEntered, EventPhase.Predicted));   // 다시 예측해도 같은 열쇠는 내지 않는다
        }

        // 내 이벤트가 서버에서 버려졌다: 그 틱의 확정 때 예측 이벤트가 Retracted 로 나온다
        [Fact]
        public void DroppedOwnEvent_RetractsItsPrediction()
        {
            var rig = new ClientRig(me: 0, inputDelay: 2);
            rig.Client.Advance(3);
            rig.Client.OnLocalInput(new[] { ClientRig.Q(5, LogicalKey.Stance, true) }, Bit(LogicalKey.Stance));
            for (uint n = 4; n <= 7; n++)
            {
                rig.Client.Advance(n);
            }
            var ticks = new List<ConfirmedTick>();
            for (uint t = 0; t <= 5; t++)
            {
                ticks.Add(rig.StepServer());
            }
            rig.Deliver(rig.ServerTick(ticks));
            rig.Drain();
            PresentationEvent r = Assert.Single(Of(rig, SimEventType.StanceEntered, EventPhase.Retracted));
            Assert.Equal(5u, r.Event.Tick);
            Assert.Empty(Of(rig, SimEventType.StanceEntered, EventPhase.Confirmed));
        }

        // 예측 하나(상대의 틱 3)가 틀려도 같은 틱 뒤의 무관한 이벤트(내 틱 5의 자세)는 번복되지 않는다
        [Fact]
        public void OneWrongPrediction_DoesNotRetractUnrelatedLaterEvents()
        {
            var rig = new ClientRig(me: 0, inputDelay: 2);
            rig.Client.Advance(3);
            rig.Client.OnLocalInput(new[] { ClientRig.Q(5, LogicalKey.Stance, true) }, Bit(LogicalKey.Stance));
            for (uint n = 4; n <= 7; n++)
            {
                rig.Client.Advance(n);
            }
            rig.Drain();
            int before = rig.Events.Count;

            var ticks = new List<ConfirmedTick>();
            for (uint t = 0; t <= 3; t++)
            {
                ticks.Add(t == 3 ? rig.StepServer(slot1: new[] { ClientRig.Ev(3, LogicalKey.Stance, true, 1) }) : rig.StepServer());
            }
            rig.Deliver(rig.ServerTick(ticks));
            DuelAdvance a = rig.Client.Advance(8);
            Assert.True(a.RollbackDepth > 0);                        // 틀린 예측이라 다시 돌렸다
            rig.Drain();

            Assert.DoesNotContain(rig.Events, e => e.Phase == EventPhase.Retracted);
            // 다시 돈 틱 4~7 에서는 새 Predicted 가 없다(내 틱 5 의 자세는 같은 열쇠라 다시 내지 않는다). 처음 돈 틱 8 의 것만 있을 수 있다
            Assert.All(rig.Events.Skip(before).Where(e => e.Phase == EventPhase.Predicted), e => Assert.Equal(8u, e.Event.Tick));
            Assert.Single(rig.Events, e => e.Event.Type == SimEventType.StanceEntered && e.Event.Slot.Value == 0);
            PresentationEvent opp = Assert.Single(rig.Events, e => e.Event.Type == SimEventType.StanceEntered && e.Event.Slot.Value == 1);
            Assert.Equal(EventPhase.Confirmed, opp.Phase);           // 예측한 적 없는 상대 이벤트는 확정으로만 나온다
        }

        // 장부 단위 시험: 열쇠에 EntityId 가 없다. 다시 돌린 틱에서 개체 번호만 바뀐 이벤트는 번복도 재예측도 없다
        [Fact]
        public void Ledger_KeyIgnoresEntityIds_AndCountsOrdinalsPerTypeAndSlot()
        {
            var ledger = new PresentationLedger(4);
            var first = new[]
            {
                Ev(SimEventType.ShotFired, 0, source: 1),
                Ev(SimEventType.ShotFired, 0, source: 2),
                Ev(SimEventType.ShotFired, 1, source: 3),
            };
            ledger.OnPredictedTick(10, first);
            PresentationEvent[] out1 = Drain(ledger);
            Assert.Equal(new ushort[] { 0, 1, 0 }, out1.Select(e => e.Ordinal).ToArray());
            Assert.All(out1, e => Assert.Equal(EventPhase.Predicted, e.Phase));

            // 다시 돌림: 개체 번호가 밀렸다(상대 탄이 먼저 생겼다) → 아무것도 내지 않는다
            var shifted = new[]
            {
                Ev(SimEventType.ShotFired, 0, source: 7),
                Ev(SimEventType.ShotFired, 0, source: 8),
                Ev(SimEventType.ShotFired, 1, source: 9),
            };
            ledger.OnPredictedTick(10, shifted);
            Assert.Empty(Drain(ledger));

            // 다시 돌림: 슬롯 0 의 두 번째 발사가 사라지고 슬롯 1 에 하나 늘었다
            var changed = new[]
            {
                Ev(SimEventType.ShotFired, 0, source: 7),
                Ev(SimEventType.ShotFired, 1, source: 9),
                Ev(SimEventType.ShotFired, 1, source: 10),
            };
            ledger.OnPredictedTick(10, changed);
            PresentationEvent[] out3 = Drain(ledger);
            Assert.Equal(2, out3.Length);
            Assert.Equal((EventPhase.Retracted, (byte)0, (ushort)1), (out3[0].Phase, out3[0].Event.Slot.Value, out3[0].Ordinal));
            Assert.Equal((EventPhase.Predicted, (byte)1, (ushort)1), (out3[1].Phase, out3[1].Event.Slot.Value, out3[1].Ordinal));

            // 확정: 확정 열에 없는 예측은 Retracted, 확정 열은 전부 Confirmed. 그 뒤로 그 틱의 기록은 없다
            var confirmed = new[] { Ev(SimEventType.ShotFired, 0, source: 3) };
            ledger.OnConfirmedTick(10, confirmed, emit: true);
            PresentationEvent[] out4 = Drain(ledger);
            Assert.Equal(new[] { EventPhase.Retracted, EventPhase.Retracted, EventPhase.Confirmed }, out4.Select(e => e.Phase).ToArray());
            Assert.All(out4.Take(2), e => Assert.Equal((byte)1, e.Event.Slot.Value));
            ledger.OnConfirmedTick(10, confirmed, emit: false);
            Assert.Empty(Drain(ledger));
        }

        [Fact]
        public void Ledger_DrainInPieces_KeepsOrder()
        {
            var ledger = new PresentationLedger(2);
            var many = new SimEvent[300];
            for (int i = 0; i < many.Length; i++)
            {
                many[i] = Ev(SimEventType.ShotFired, (byte)(i & 1), source: i);
            }
            ledger.OnPredictedTick(1, many);
            var all = new List<PresentationEvent>();
            var buffer = new PresentationEvent[7];
            int n;
            while ((n = ledger.Drain(buffer)) > 0)
            {
                all.AddRange(buffer.Take(n));
            }
            Assert.Equal(many.Length, all.Count);
            for (int i = 0; i < many.Length; i++)
            {
                Assert.Equal(i, all[i].Event.Source.Value);
                Assert.Equal((ushort)(i / 2), all[i].Ordinal);
            }
        }

        private static SimEvent Ev(SimEventType type, byte slot, int source) => new SimEvent
        {
            Type = type,
            Slot = new PlayerSlot(slot),
            Tick = 10,
            Source = new EntityId(source),
        };

        private static PresentationEvent[] Drain(PresentationLedger ledger)
        {
            var buffer = new PresentationEvent[64];
            int n = ledger.Drain(buffer);
            return buffer.Take(n).ToArray();
        }
    }
}
