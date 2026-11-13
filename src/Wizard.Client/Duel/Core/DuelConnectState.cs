namespace Wizard.Client.Duel
{
    // DuelConnector 의 단계(CONTRACTS 10.4)
    public enum DuelConnectState : byte
    {
        Syncing = 0,     // SyncRequest/SyncReply 를 ClockSyncSamples 번 주고받는 중
        Greeting = 1,    // Hello 를 보내고 HelloAck 를 기다리는 중
        Ready = 2,       // HelloAck 를 받았다: D, 경기 시계의 기준점, 시계 오프셋이 정해졌다
    }
}
