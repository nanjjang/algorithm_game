namespace Wizard.Client.Duel
{
    // DuelSession 의 단계
    public enum DuelSessionPhase : byte
    {
        Connecting = 0,   // 연결 절차(시계 맞추기, 인사). 키는 세계에 가지 않는다
        Running = 1,      // HelloAck 를 받았다. 입력 시계가 돈다(틱 0 전이면 카운트다운 전의 대기)
    }
}
