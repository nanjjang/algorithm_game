using System;
using System.Collections.Generic;

namespace Wizard.Content
{
    // CONTRACTS 2.3. 불변이다. 판과 규칙 함수에 주입한다(전역 싱글턴이 아니다. 서버는 여러 판을 동시에 돌린다)
    //
    // 뼈대(T-33, T-47): 이 파일과 Rules/ContentDb.Rules.cs, Pack/Core/ContentDb.Core.cs 가 접근자와 절 훅을 전부 선언한다.
    // 절을 읽는 코드는 절마다 ContentDb.<절>.cs 하나(전투 쪽 Pack/Sections/, 규칙 쪽 Rules/Sections/)이고,
    // 그 파일은 Load<절> 과 Find<접근자> 훅을 구현하기만 한다. 뼈대 파일은 고치지 않는다.
    public sealed partial class ContentDb
    {
        private readonly ContentPack _pack;

        private ContentDb(ContentPack pack)
        {
            _pack = pack;
            // 팩의 절을 id 오름차순으로 절 훅에 넘긴다. 절 파일의 필드는 여기서 한 번 채워지고 그 뒤로 바뀌지 않는다
            LoadSections();
        }

        // 형식이 깨진 절은 ContentException
        public static ContentDb Build(ContentPack pack) => new ContentDb(pack ?? throw new ArgumentNullException(nameof(pack)));

        public int DataVersion => _pack.DataVersion;

        public ContentPack Pack => _pack;

        // 전투 쪽(코어 버전 해시 안). 접근자는 조회 훅에 묻고, 훅이 답을 주지 않으면
        // (절을 읽는 코드가 아직 없다, 절이 팩에 없다, 그 id 가 없다) ContentException 을 던진다.
        public CharacterDef Character(CharacterId id)
        {
            CharacterDef? def = null;
            FindCharacter(id, ref def);
            return Require(def, ContentSection.Characters);
        }

        public BlockDef Block(BlockId id)
        {
            BlockDef? def = null;
            FindBlock(id, ref def);
            return Require(def, ContentSection.Blocks);
        }

        public ArtDef Art(ArtId id)
        {
            ArtDef? def = null;
            FindArt(id, ref def);
            return Require(def, ContentSection.Arts);
        }

        public MutationDef Mutation(MutationId id)
        {
            MutationDef? def = null;
            FindMutation(id, ref def);
            return Require(def, ContentSection.Mutations);
        }

        public EnemyDef Enemy(EnemyTypeId id)
        {
            EnemyDef? def = null;
            FindEnemy(id, ref def);
            return Require(def, ContentSection.Enemies);
        }

        public BossDef Boss(BossId id)
        {
            BossDef? def = null;
            FindBoss(id, ref def);
            return Require(def, ContentSection.Bosses);
        }

        public WaveCurveDef WaveCurve
        {
            get
            {
                WaveCurveDef? def = null;
                FindWaveCurve(ref def);
                return Require(def, ContentSection.Waves);
            }
        }

        public IReadOnlyList<FrameDef> Frames
        {
            get
            {
                IReadOnlyList<FrameDef>? defs = null;
                FindFrames(ref defs);
                return Require(defs, ContentSection.Frames);
            }
        }

        public IReadOnlyList<ModifierDef> Modifiers
        {
            get
            {
                IReadOnlyList<ModifierDef>? defs = null;
                FindModifiers(ref defs);
                return Require(defs, ContentSection.Modifiers);
            }
        }

        public ScoringDef Scoring
        {
            get
            {
                ScoringDef? def = null;
                FindScoring(ref def);
                return Require(def, ContentSection.Scoring);
            }
        }

        public ArenaDef Arena(SimMode mode)
        {
            ArenaDef? def = null;
            FindArena(mode, ref def);
            return Require(def, ContentSection.Arena);
        }

        public DuelRuleSet DuelRules
        {
            get
            {
                DuelRuleSet? def = null;
                FindDuelRules(ref def);
                return Require(def, ContentSection.DuelRules);
            }
        }

        public TrieDef Tries(CharacterId id)
        {
            TrieDef? def = null;
            FindTries(id, ref def);
            return Require(def, ContentSection.Tries);
        }

        public EliteDef Elite(EnemyTypeId baseType)
        {
            EliteDef? def = null;
            FindElite(baseType, ref def);
            return Require(def, ContentSection.Elites);
        }

        public StarfallDef Starfall
        {
            get
            {
                StarfallDef? def = null;
                FindStarfall(ref def);
                return Require(def, ContentSection.Starfall);
            }
        }

        public IReadOnlyList<WeatherEventDef> WeatherEvents
        {
            get
            {
                IReadOnlyList<WeatherEventDef>? defs = null;
                FindWeatherEvents(ref defs);
                return Require(defs, ContentSection.WeatherEvents);
            }
        }

        public BossRotationDef BossRotation
        {
            get
            {
                BossRotationDef? def = null;
                FindBossRotation(ref def);
                return Require(def, ContentSection.BossRotation);
            }
        }

        public TimeOfDayDef TimeOfDay(TimeOfDay tod)
        {
            TimeOfDayDef? def = null;
            FindTimeOfDay(tod, ref def);
            return Require(def, ContentSection.Tod);
        }

        public ElementDef ElementRules
        {
            get
            {
                ElementDef? def = null;
                FindElementRules(ref def);
                return Require(def, ContentSection.Elements);
            }
        }

        // 주문 컴파일 규칙(트리 실행 상한, 밑줄 상한, 거울상 열 배수). blocks 절의 꼬리에 있다(T-57).
        // 꼬리가 없는 팩(시험용)에서는 ContentException
        public SpellRulesDef SpellRules
        {
            get
            {
                SpellRulesDef? def = null;
                FindSpellRules(ref def);
                return Require(def, ContentSection.Blocks);
            }
        }

        public SimLimitsDef SimLimits
        {
            get
            {
                SimLimitsDef? def = null;
                FindSimLimits(ref def);
                return Require(def, ContentSection.SimLimits);
            }
        }
    }
}
