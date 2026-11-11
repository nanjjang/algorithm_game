namespace Wizard.Content
{
    // CONTRACTS 2.1. 엘리트는 종이 아니라 EnemyState.IsElite (표시 id는 elite.<키>, 코드 200 + 종)
    public enum EnemyTypeId : ushort
    {
        None = 0,
        SlimeMoss = 1,
        WolfBramble = 2,
        GolemMenhir = 3,
        ShamanCap = 4,
        BugLantern = 5,
        HogNeedle = 6,
        WeaselPot = 7,
        BatDusk = 8,
        WispStray = 9,
        VineThorn = 10,
        BoarTusk = 11,
        ToadDrum = 12,
        SporeSeedling = 101,
    }
}
