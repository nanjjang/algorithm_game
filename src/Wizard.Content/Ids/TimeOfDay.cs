namespace Wizard.Content
{
    // CONTRACTS 2.1. spec 14 3.9의 코드(tod.day … tod.boss)
    public enum TimeOfDay : byte
    {
        None = 0,
        Day = 1,
        Dusk = 2,
        Night = 3,
        BossWave = 4,
    }
}
