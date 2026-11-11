namespace Wizard.Content
{
    // CONTRACTS 2.1
    [System.Flags]
    public enum EnemyTag : ushort
    {
        None = 0,
        Elite = 1,
        Heavy = 2,
        Boss = 4,
        Flying = 8,
        Burrowed = 16,
        Rooted = 32,
    }
}
