namespace Wizard.Sim
{
    // CONTRACTS 3.2 야영 하위 상태(spec 07 11.4). 정본 상태이고 해시에 들어간다
    public enum CampSub : byte
    {
        None = 0,
        LootPick = 1,
        FireIdle = 2,
        CampScreen = 3,
        Practice = 4,
        DepartHold = 5,
    }
}
