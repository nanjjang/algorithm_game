namespace Wizard.Sim.Input
{
    // CONTRACTS 3.1, 0.3. 4비트. 키 코드는 spec 01 의 것 하나다(DuelKey 없음). K = Skill
    public enum LogicalKey : byte
    {
        Up = 0,
        Left = 1,
        Down = 2,
        Right = 3,
        Fire = 4,
        Skill = 5,
        Stance = 6,
        Reverse = 7,
        Pause = 8,
    }
}
