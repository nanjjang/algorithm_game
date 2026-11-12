namespace Wizard.Content
{
    // CONTRACTS 11.2 표의 절 이름(snake_case)과 구간. 오류 문구와 DataBuild 가 쓴다. 표는 switch 로 둔다(코어에 가변 정적 배열을 두지 않는다).
    public static class ContentSections
    {
        public const ushort SimFirst = 1;
        public const ushort SimLast = 19;
        public const ushort RulesFirst = 20;
        public const ushort RulesLast = 39;
        public const ushort SimExtFirst = 40;

        // 알려진 절이면 이름, 아니면 null
        public static string? Name(ContentSection s)
        {
            switch (s)
            {
                case ContentSection.Characters: return "characters";
                case ContentSection.Blocks: return "blocks";
                case ContentSection.Arts: return "arts";
                case ContentSection.Mutations: return "mutations";
                case ContentSection.Enemies: return "enemies";
                case ContentSection.Bosses: return "bosses";
                case ContentSection.Waves: return "waves";
                case ContentSection.Frames: return "frames";
                case ContentSection.Modifiers: return "modifiers";
                case ContentSection.Tod: return "tod";
                case ContentSection.Scoring: return "scoring";
                case ContentSection.Arena: return "arena";
                case ContentSection.DuelRules: return "duel_rules";
                case ContentSection.Tries: return "tries";
                case ContentSection.Starfall: return "starfall";
                case ContentSection.WeatherEvents: return "weather_events";
                case ContentSection.BossRotation: return "boss_rotation";
                case ContentSection.Elites: return "elites";
                case ContentSection.Elements: return "elements";
                case ContentSection.Enhance: return "enhance";
                case ContentSection.Economy: return "economy";
                case ContentSection.Drops: return "drops";
                case ContentSection.Leagues: return "leagues";
                case ContentSection.Season: return "season";
                case ContentSection.Titles: return "titles";
                case ContentSection.Cosmetics: return "cosmetics";
                case ContentSection.Plaza: return "plaza";
                case ContentSection.Shop: return "shop";
                case ContentSection.Items: return "items";
                case ContentSection.Bag: return "bag";
                case ContentSection.RunLimits: return "run_limits";
                case ContentSection.Il: return "il";
                case ContentSection.Fees: return "fees";
                case ContentSection.Net: return "net";
                case ContentSection.ChatFilter: return "chat_filter";
                case ContentSection.SimLimits: return "sim_limits";
                default: return null;
            }
        }

        public static bool IsKnown(ContentSection s) => Name(s) != null;

        // 규칙 쪽(20~39, 코어 버전 해시 밖)
        public static bool IsRules(ContentSection s) => (ushort)s >= RulesFirst && (ushort)s <= RulesLast;
    }
}
