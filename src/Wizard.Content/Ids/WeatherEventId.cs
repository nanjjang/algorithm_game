namespace Wizard.Content
{
    // CONTRACTS 2.1. spec 14 3.9 "날씨와 돌발 사건". 결투에는 넣지 않는다
    public enum WeatherEventId : byte
    {
        None = 0,
        Shower = 1,
        Fog = 2,
        StarCrystal = 3,
        MasterAdvice = 4,
        DiscardedItem = 5,
    }
}
