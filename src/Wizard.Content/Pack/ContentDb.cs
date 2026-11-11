using System;
using System.Collections.Generic;

namespace Wizard.Content
{
    // CONTRACTS 2.3. 불변이다. 판과 규칙 함수에 주입한다(전역 싱글턴이 아니다. 서버는 여러 판을 동시에 돌린다)
    public sealed partial class ContentDb
    {
        private readonly ContentPack _pack;

        private ContentDb(ContentPack pack)
        {
            _pack = pack;
        }

        public static ContentDb Build(ContentPack pack) => new ContentDb(pack ?? throw new ArgumentNullException(nameof(pack)));

        public int DataVersion => _pack.DataVersion;

        public ContentPack Pack => _pack;

        // 전투 쪽(코어 버전 해시 안). 절이 팩에 없거나 아직 읽는 코드가 없으면 ContentException.
        // 절마다 ContentDb.<절>.cs 하나가 이 몸통을 바꾼다(T-33, CONTRACTS 2.3).
        public CharacterDef Character(CharacterId id) => throw Missing("characters");
        public BlockDef Block(BlockId id) => throw Missing("blocks");
        public ArtDef Art(ArtId id) => throw Missing("arts");
        public MutationDef Mutation(MutationId id) => throw Missing("mutations");
        public EnemyDef Enemy(EnemyTypeId id) => throw Missing("enemies");
        public BossDef Boss(BossId id) => throw Missing("bosses");
        public WaveCurveDef WaveCurve => throw Missing("waves");
        public IReadOnlyList<FrameDef> Frames => throw Missing("frames");
        public IReadOnlyList<ModifierDef> Modifiers => throw Missing("modifiers");
        public ScoringDef Scoring => throw Missing("scoring");
        public ArenaDef Arena(SimMode mode) => throw Missing("arena");
        public DuelRuleSet DuelRules => throw Missing("duel_rules");
        public TrieDef Tries(CharacterId id) => throw Missing("tries");
        public EliteDef Elite(EnemyTypeId baseType) => throw Missing("elites");
        public StarfallDef Starfall => throw Missing("starfall");
        public IReadOnlyList<WeatherEventDef> WeatherEvents => throw Missing("weather_events");
        public BossRotationDef BossRotation => throw Missing("boss_rotation");
        public TimeOfDayDef TimeOfDay(TimeOfDay tod) => throw Missing("tod");
        public ElementDef ElementRules => throw Missing("elements");
        public SimLimitsDef SimLimits => throw Missing("sim_limits");

        private static ContentException Missing(string section) => new ContentException("content section '" + section + "' is not loaded");
    }
}
