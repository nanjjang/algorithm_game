using System;
using Wizard.Core;

namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4, 10.1절, DECISIONS-TECH T-62·T-64. 경기의 두 사람 키(슬롯 순). DuelHostCore 의 생성자가 받는다.
    // 두 키는 다르고 token_id 도 다르다(token_id 가 패킷의 보낸 사람을 가린다). default(DuelHostKeys)는 두 칸이 다 빈 값이고
    // DuelHostCore 의 생성자가 ArgumentException 으로 거절한다.
    public readonly struct DuelHostKeys
    {
        public readonly DuelPeerKey Slot0;
        public readonly DuelPeerKey Slot1;

        // 둘 다 IsValid 이고 token_id 가 서로 달라야 한다(아니면 ArgumentException)
        public DuelHostKeys(DuelPeerKey slot0, DuelPeerKey slot1)
        {
            if (!slot0.IsValid || !slot1.IsValid)
            {
                throw new ArgumentException("both duel keys must be set");
            }
            if (slot0.TokenId == slot1.TokenId)
            {
                throw new ArgumentException("the two players must have different token_id");
            }
            Slot0 = slot0;
            Slot1 = slot1;
        }

        // 결투의 슬롯은 0 과 1 이다(그 밖이면 ArgumentOutOfRangeException)
        public DuelPeerKey For(PlayerSlot slot)
        {
            if (slot.Value == 0)
            {
                return Slot0;
            }
            if (slot.Value == 1)
            {
                return Slot1;
            }
            throw new ArgumentOutOfRangeException(nameof(slot), "duel slots are 0 and 1");
        }
    }
}
