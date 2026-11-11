namespace Wizard.Content
{
    // CONTRACTS 2.1·2.3. 정수 값은 추가만 하고 바꾸지 않는다
    public enum PresenceState : byte
    {
        Offline = 0,
        Online = 1,
        Expedition = 2,
        Duel = 3,
        Trade = 4,
        Idle = 5,
        DuelWait = 6,
        Spectate = 7,
    }
}
