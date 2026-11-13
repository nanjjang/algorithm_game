using System.Collections.Generic;
using Wizard.Netcode.Duel;
using Wizard.Protocol.Duel;
using Wizard.Sim.Input;

namespace Wizard.Netcode.Tests.NetSim
{
    // 한 클라이언트에서 NetSim 이 본 것(시험이 읽는다)
    internal sealed class ClientTrace
    {
        public int Delay;
        public readonly List<ulong> ConfirmedHashes = new List<ulong>();          // 색인 = 확정 틱. 그 틱을 Step 한 뒤의 확정 세계 해시
        public readonly List<long> ConfirmedAtUs = new List<long>();              // 색인 = 확정 틱. 그 틱이 이 클라이언트에서 확정된 시각
        public readonly List<(uint inputClock, DuelAdvance advance)> Advances = new List<(uint, DuelAdvance)>();
        public readonly List<PresentationEvent> Events = new List<PresentationEvent>();
        public readonly SortedDictionary<uint, InputEvent> SentBySeq = new SortedDictionary<uint, InputEvent>();   // 내보낸 패킷을 열어 본 내 이벤트
        public readonly List<(long atUs, QuantizedKey key)> Pressed = new List<(long, QuantizedKey)>();          // 시험이 누른 키(유효 틱 = 누른 틱 + D)
        public readonly SortedDictionary<uint, DuelDropReason> DropNotices = new SortedDictionary<uint, DuelDropReason>();   // 호스트가 이 사람에게 알린 버린 순번(ServerTick 의 dropped)
        public readonly List<(long atUs, uint sealedThrough)> SealArrivals = new List<(long, uint)>();           // 내 패킷이 호스트에 닿은 시각과 그 패킷의 sealed_through_tick
        public bool EverNeedsFullState;
        public long PacketsSent;
        public KeyMask Physical;
    }
}
