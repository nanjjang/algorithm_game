namespace Wizard.Content
{
    // CONTRACTS 2.1·2.3. 정수 값은 추가만 하고 바꾸지 않는다
    public enum WandLockState : byte
    {
        None = 0,
        Expedition = 1,
        Duel = 2,
        Market = 3,
        Mail = 4,
        Trade = 5,
    }
}
