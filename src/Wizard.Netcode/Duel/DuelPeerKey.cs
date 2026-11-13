using System;
using Wizard.Protocol.Duel;

namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4, 9절 duel.setup, 10.1절, DECISIONS-TECH T-62·T-64. 서버가 만든 한 사람의 키 = (경기, 사람, epoch):
    // token_id, epoch, 32바이트 키. duel.setup 으로 그 사람에게 내려 준 것과 같은 값이다. 재접속 때는 새 token_id, 새 키, epoch + 1 로
    // 새로 만든다(DuelHostCore.Rekey).
    //
    // 클라이언트 쪽의 DuelClientKeys 와 달리 보내는 번호를 세는 자리가 없다: 서버 → 그 사람 방향의 packet_number 는 DuelHostCore 가
    // 사람마다 들고 있다(10.1절). default(DuelPeerKey)는 키가 없고 IsValid 가 거짓이다.
    public readonly struct DuelPeerKey
    {
        public readonly uint TokenId;
        public readonly uint Epoch;
        private readonly byte[]? _key;

        // key 는 복사해 둔다(호출자가 버퍼를 다시 써도 이 값의 키는 바뀌지 않는다). 32바이트가 아니면 ArgumentException
        public DuelPeerKey(uint tokenId, uint epoch, ReadOnlySpan<byte> key)
        {
            if (key.Length != DuelWire.KeyBytes)
            {
                throw new ArgumentException("duel key must be 32 bytes", nameof(key));
            }
            TokenId = tokenId;
            Epoch = epoch;
            _key = key.ToArray();
        }

        // default(DuelPeerKey)는 빈 열이다. IsValid 를 먼저 본다
        public ReadOnlySpan<byte> Key => _key;

        // default(DuelPeerKey)는 키가 없다. 빈 키로 조용히 돌지 않게 받는 쪽이 본다
        public bool IsValid => _key != null;
    }
}
