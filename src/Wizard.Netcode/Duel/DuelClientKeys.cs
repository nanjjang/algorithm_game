using System;
using Wizard.Protocol.Duel;

namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4, 9절 duel.setup, 10.1절, T-62. 내 키 하나 = (경기, 나, epoch): duel.setup 이 준 token_id, epoch, 32바이트 키와
    // 그 키로 보내는 방향의 packet_number 를 세는 단 하나의 자리(SendCounter).
    //
    // 같은 키로 봉인하는 코드는 둘이다: 연결 절차(SyncRequest, Hello, Bye. Wizard.Client 의 DuelSession 쪽)와 DuelClientCore(ClientInput).
    // 둘이 번호를 따로 세면 같은 (키, nonce)가 두 번 쓰여 키스트림이 드러난다(10.1절). 그래서 번호는 여기 든 SendCounter 에서만 받는다.
    // 값을 복사해도 같은 SendCounter 를 가리킨다. duel.setup 한 번에 이 값을 한 번만 만든다(같은 키로 두 번 만들면 번호가 1부터 겹친다).
    // 프로세스를 다시 켜 번호를 잃었으면 같은 키로 다시 만들지 않고 duel.rejoin 으로 새 키(epoch + 1, 새 token_id)를 받는다.
    public readonly struct DuelClientKeys
    {
        public readonly uint TokenId;
        public readonly uint Epoch;
        private readonly byte[] _key;
        private readonly DuelSendCounter _sendCounter;

        public DuelClientKeys(uint tokenId, uint epoch, ReadOnlySpan<byte> key)
        {
            if (key.Length != DuelWire.KeyBytes)
            {
                throw new ArgumentException("duel key must be 32 bytes", nameof(key));
            }
            TokenId = tokenId;
            Epoch = epoch;
            _key = key.ToArray();
            _sendCounter = new DuelSendCounter();
        }

        public ReadOnlySpan<byte> Key => _key;

        // default(DuelClientKeys)는 null 이다. IsValid 를 먼저 본다
        public DuelSendCounter SendCounter => _sendCounter;

        // default(DuelClientKeys)는 키가 없다. 빈 키로 조용히 돌지 않게 받는 쪽이 본다
        public bool IsValid => _key != null;
    }
}
