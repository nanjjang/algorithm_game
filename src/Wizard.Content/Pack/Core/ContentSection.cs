namespace Wizard.Content
{
    // CONTRACTS 11.2. 팩 절 목차의 section_id. 번호는 고정이고 더하기만 한다(바꾸거나 다시 쓰지 않는다).
    //   1~19  전투(data/sim/, 코어 버전 해시 안)
    //   20~39 규칙(data/rules/, 해시 밖. 읽는 코드는 src/Wizard.Content/Rules/)
    //   40~   전투(이어서)
    // ARCHITECTURE 2절의 data/sim·data/rules 파일은 전부 여기에 절 하나씩 있다(rules/limits.json 은 RunLimits).
    public enum ContentSection : ushort
    {
        None = 0,

        Characters = 1,
        Blocks = 2,
        Arts = 3,
        Mutations = 4,
        Enemies = 5,
        Bosses = 6,
        Waves = 7,
        Frames = 8,
        Modifiers = 9,
        Tod = 10,
        Scoring = 11,
        Arena = 12,
        DuelRules = 13,
        Tries = 14,
        Starfall = 15,
        WeatherEvents = 16,
        BossRotation = 17,
        Elites = 18,
        Elements = 19,

        Enhance = 20,
        Economy = 21,
        Drops = 22,
        Leagues = 23,
        Season = 24,
        Titles = 25,
        Cosmetics = 26,
        Plaza = 27,
        Shop = 28,
        Items = 29,
        Bag = 30,
        RunLimits = 31,
        Il = 32,
        Fees = 33,
        Net = 34,
        ChatFilter = 35,

        SimLimits = 40,
    }
}
