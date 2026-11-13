using Wizard.Content.Rules;
using Wizard.Core;
using Wizard.Netcode.Clock;
using Wizard.Protocol.Duel;

namespace Wizard.Netcode.Duel
{
    // DuelHostCore 가 사람(슬롯)마다 가진 상태. 호스트 밖으로 내보내지 않는다.
    internal sealed class HostPeer
    {
        private const int MaxDropNotices = ServerTickPacket.MaxDropped;   // 자료구조 크기

        public readonly PlayerSlot Slot;
        public readonly uint TokenId;
        public readonly uint Epoch;
        public readonly byte[] Key;

        // 받는 쪽(CONTRACTS 10.1): 재전송 창(태그 검증 뒤에만 Mark), 응답 주소(태그 검증을 통과한 마지막 패킷의 출발 주소)
        public DuelReplayWindow Window;
        public bool HasAddress;
        public ulong Address;

        // 입력 검증(10.6)
        public uint LastSeq;                     // 소비한 마지막 순번(받음, 버림, 유실 모두)
        public long SealedThrough = -1;          // 이 틱까지 봉인. −1 = 아직 없음
        public ushort KeyDown;                   // 받아들인 이벤트로 본 키 상태(KeyMask 비트 = 1 << LogicalKey)
        public int InputDelayTicks;              // SetInputDelay(서버가 정한 D). 이른 시각 검사와 WZDR 의 input_delay 칸
        public readonly RttSampleWindow Rtt;     // 서버가 직접 잰 왕복(10.5)
        public readonly PendingInputRing Pending = new PendingInputRing();

        // 보내는 쪽(10.3)
        public ulong NextOutPacketNumber = 1;    // 서버 → 이 사람 방향의 번호. 같은 키에서 되돌리지 않는다
        public uint NextUnackedTick;             // ack 받지 못한 첫 확정 틱
        public uint EchoClientSendTimeMs;
        public bool SentServerTime;              // ServerTick 을 하나라도 보냈나(에코 검사)
        public uint LastSentServerTimeMs;        // 보낸 server_time_ms 의 최댓값(그보다 큰 에코는 지어낸 값이다)
        public short MarginMs;                   // 마감 여유 추정(HUD)
        public bool SendDue;
        public bool HashPending;
        public uint HashTick;
        public ulong HashValue;
        public readonly DropNoticeQueue Drops = new DropNoticeQueue(MaxDropNotices);

        public DuelNetStats Stats;

        public HostPeer(PlayerSlot slot, in DuelPeerKey key, NetRuleSet rules)
        {
            Slot = slot;
            TokenId = key.TokenId;
            Epoch = key.Epoch;
            Key = key.Key.ToArray();
            Rtt = new RttSampleWindow(rules);
        }

        public bool IsSealed(uint tick) => SealedThrough >= tick;
    }
}
