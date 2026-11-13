namespace Wizard.Netcode.Duel
{
    // CONTRACTS 10.1, T-62. 한 키의 보내는 방향 packet_number 를 세는 단 하나의 자리. 1부터 1씩이고 한 번 내준 번호는 다시 내주지 않는다
    // ("같은 키에서 packet_number 는 절대 되돌아가지 않는다"). 받는 쪽의 재전송 창은 빈 번호를 탓하지 않으므로, 받아 놓고 쓰지 못한
    // 번호는 버려도 된다. 스레드 하나에서만 쓴다(넷코드에는 스레드가 없다).
    public sealed class DuelSendCounter
    {
        private ulong _taken;

        // 지금까지 내준 마지막 번호. 아직 없으면 0
        public ulong Taken => _taken;

        public ulong Take()
        {
            _taken++;
            return _taken;
        }
    }
}
