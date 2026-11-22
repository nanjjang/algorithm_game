using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Tests.Save
{
    /// <summary>
    /// 저장 테스트용 최소 콘텐츠: 캐릭터 다섯 줄만(data/sim/characters.json의 unique_art·base_link_slots 값 그대로).
    /// 저장 모듈이 F1(ContentDb)의 진행에 묶이지 않게 테스트 폴더 안에 둔다.
    /// </summary>
    sealed class FakeContent : IContentDb
    {
        readonly List<CharacterDef> _chars = new List<CharacterDef>
        {
            new CharacterDef { Id = "char.1", UniqueArt = "art.kiln", BaseLinkSlots = 4 },
            new CharacterDef { Id = "char.2", UniqueArt = "art.bolt_dash", BaseLinkSlots = 4 },
            new CharacterDef { Id = "char.3", UniqueArt = "art.copy_circle", BaseLinkSlots = 5 },
            new CharacterDef { Id = "char.4", UniqueArt = "art.ice_screen", BaseLinkSlots = 4 },
            new CharacterDef { Id = "char.5", UniqueArt = "art.shadow_hand", BaseLinkSlots = 4 },
        };

        public string Version = "test-data-1";
        public JObject Extra;

        public string DataVersion => Version;
        public IReadOnlyList<BlockDef> Blocks => new List<BlockDef>();
        public BlockDef Block(string blockId) => null;
        public IReadOnlyList<string> BlocksOfRarity(string rarity) => new List<string>();
        public ElementsDef Elements => null;
        public IReadOnlyList<CharacterDef> Characters => _chars;
        public CharacterDef Character(string charId) => _chars.Find(c => c.Id == charId);
        public CharacterDef BaselineCharacter => null;
        public IReadOnlyList<ArtDef> Arts => new List<ArtDef>();
        public ArtDef Art(string artId) => null;
        public TriesDef Tries => null;
        public IReadOnlyList<string> TrieArts(string charId) => new List<string>();
        public IReadOnlyList<EnemyDef> Enemies => new List<EnemyDef>();
        public EnemyDef Enemy(string enemyId) => null;
        public EliteDef Elite(string eliteOrEnemyId) => null;
        public IReadOnlyList<EliteDef> Elites => new List<EliteDef>();
        public BossCommonDef BossCommon => null;
        public IReadOnlyList<BossDef> Bosses => new List<BossDef>();
        public BossDef Boss(string bossId) => null;
        public WavesDef Waves => null;
        public IReadOnlyList<FrameDef> Frames => new List<FrameDef>();
        public FrameDef Frame(string frameId) => null;
        public IReadOnlyList<ModifierDef> Modifiers => new List<ModifierDef>();
        public ModifierDef Modifier(string whimId) => null;
        public IReadOnlyList<TodDef> Tods => new List<TodDef>();
        public TodDef Tod(string todId) => null;
        public ScoringDef Scoring => null;
        public StarfallDef Starfall => null;
        public IReadOnlyList<WeatherEventDef> WeatherEvents => new List<WeatherEventDef>();
        public ArenaDef Field => null;
        public EnhanceConfig Enhance => null;
        public EconomyConfig Economy => null;
        public MutationsConfig Mutations => null;
        public DropsConfig Drops => null;
        public CampConfig Camp => null;
        public SoloConstDef Const => null;
        public PracticeConfig Practice => null;
        public JObject SoloExtra(string name) => name == Wizard.Solo.Audio.SoundMixConfig.ExtraName ? Extra : null;
    }

    sealed class FixedClock : IClock
    {
        public System.DateTime Value;
        public FixedClock(System.DateTime value) { Value = value; }
        public System.DateTime Now => Value;
        public System.DateTime UtcNow => Value.ToUniversalTime();
    }
}
