namespace Wizard.Netcode.Duel
{
    // CONTRACTS 6.4, 7.3. 재접속: 마지막 확정 틱, 다음 순번, 서버가 아는 키 상태
    public sealed class DuelResume
    {
        public uint ConfirmedTick;
        public uint NextSeq;
        public ushort KeyState;               // KeyMask 비트
    }
}
