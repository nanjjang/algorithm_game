namespace Wizard.Content
{
    // CONTRACTS 2.1. spec 14 3.10. 문자열 id가 정본(item_stack.item_code). 블록은 BlockId 를 쓴다. HealthShard 는 판 안에서만
    public enum ItemCode : ushort
    {
        None = 0,
        StoneLow = 1,
        StoneMid = 2,
        StoneHigh = 3,
        CharmSafeguard = 4,
        MaterialBundle = 10,
        CrowBundle = 11,
        SeasonFabric = 12,
        WandBox = 13,
        LibraryExpansion = 14,
        Rename = 15,
        HealthShard = 20,
    }
}
