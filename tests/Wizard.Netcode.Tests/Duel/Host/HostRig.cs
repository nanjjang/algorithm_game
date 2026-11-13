using System.Collections.Generic;
using System.Linq;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;

namespace Wizard.Netcode.Tests.Duel.Host
{
    // 손으로 짠 패킷을 정한 시각에 넣는 시험대: 호스트 하나, 가짜 클라이언트 둘, 호스트가 낸 ServerTick 전부.
    // Advance 를 부를 때마다 두 사람에게 보낼 것을 다 꺼내 Views 에 쌓는다(dropped 알림은 한 번만 실리므로 꺼낸 것을 모은다)
    internal sealed class HostRig
    {
        public readonly TestCipher Cipher = new TestCipher();
        public readonly DuelHostCore Host;
        public readonly FakeClient[] Clients = { new FakeClient(0), new FakeClient(1) };
        public readonly List<ServerView>[] Views = { new List<ServerView>(), new List<ServerView>() };

        public HostRig(int delay0 = 2, int delay1 = 2)
        {
            Host = new DuelHostCore(HostKit.Db, HostKit.Setup(), HostKit.Keys(), Cipher);
            Host.SetInputDelay(HostKit.S0, delay0);
            Host.SetInputDelay(HostKit.S1, delay1);
        }

        // 틱 tick 의 시작(µs). tick + num/den 틱의 시각
        public static long T(long tick) => HostKit.TickStartUs(tick);

        public static long At(long tick, long num, long den) => HostKit.AtTicks(tick, num, den);

        // base_seq 는 10.2 의 규칙(첫 이벤트 바로 앞 순번. 이벤트가 없으면 lastSeq)
        public byte[] Send(int slot, long receiveUs, uint sealedThrough, params InputEvent[] events) =>
            Clients[slot].Send(Host, receiveUs, sealedThrough, events);

        public byte[] SendBase(int slot, long receiveUs, uint sealedThrough, uint baseSeq, params InputEvent[] events) =>
            Clients[slot].Send(Host, receiveUs, sealedThrough, events, baseSeq);

        public void Advance(long serverUs)
        {
            Host.Advance(serverUs);
            for (int s = 0; s < 2; s++)
            {
                Views[s].AddRange(ServerView.Drain(Host, s));
            }
        }

        public List<DroppedRun> Dropped(int slot) => Views[slot].SelectMany(v => v.Dropped).ToList();

        // 본인에게 알린 순번 하나하나와 그 이유
        public List<(uint seq, DuelDropReason reason)> DroppedSeqs(int slot)
        {
            var list = new List<(uint, DuelDropReason)>();
            foreach (DroppedRun r in Dropped(slot))
            {
                for (uint s = r.Seq; s <= r.LastSeq; s++)
                {
                    list.Add((s, r.Reason));
                }
            }
            return list;
        }

        // viewer 에게 실린 틱 tick 의 slot 입력(가장 늦게 실린 것). 실린 적이 없으면 null
        public InputEvent[]? Confirmed(int viewer, uint tick, int slot)
        {
            for (int i = Views[viewer].Count - 1; i >= 0; i--)
            {
                if (Views[viewer][i].HasTick(tick))
                {
                    return Views[viewer][i].Events(tick, slot);
                }
            }
            return null;
        }

        public bool EverSent(int viewer, uint tick) => Views[viewer].Any(v => v.HasTick(tick));

        // WZDR 로 본 확정 열(한 슬롯)
        public List<InputEvent> Record(int slot) =>
            Wzdr.Read(Host.ConfirmedInputRecord()).Inputs.Where(i => i.Slot.Value == slot).Select(i => i.Event).ToList();

        public static uint[] Seqs(IEnumerable<InputEvent> events) => events.Select(e => e.Seq).ToArray();
    }
}
