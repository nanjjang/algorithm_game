using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Def 규칙(이 폴더 전체): 공개 필드는 PascalCase, JSON은 snake_case. 로더가 SnakeCaseNamingStrategy로 잇는다.
// 초(*Seconds), 칸, 도 단위를 JSON 값 그대로 담는다. 틱·HU 환산은 쓰는 쪽이 SimConst 함수로 한다.
// 이 폴더는 설계자가 껍데기를 만들었고 데이터 작업(F1)이 이어받아 채운다. 있는 필드·타입 이름은 바꾸지 않고 더하기만 한다.
namespace Wizard.Solo.Data
{
    /// <summary>blocks.json 한 줄. 03 요약서 2.1절.</summary>
    public sealed class BlockDef
    {
        public string Id;
        /// <summary>"projectile" / "control" / "modifier".</summary>
        public string Kind;
        /// <summary>"common" / "rare" / "hero".</summary>
        public string Rarity;
        public string Element;
        public float Heat;
        public float HeatMultiplier = 1f;
        public int SplitCount = 1;
        public float BaseDamage;
        public float IntervalSeconds;
        public float Speed;
        public bool Instant;
        public float Range;
        public float HitRadius;
        public float DamageBonus;
        public List<string> Flags = new List<string>();
        public string Effect = Ids.BlockEffect.None;
        public BlockParams Params = new BlockParams();
        /// <summary>기본 공격 유형(핵 블록 block.core.*)의 동작 수치(DataSolo/AttackStyles.json). blocks.json의 블록은 null.
        /// Docs/12-attack-styles.md 2절.</summary>
        public AttackStyleParams Style;

        /// <summary>기본 공격 유형의 핵 블록(또는 그 부속 탄)인가.</summary>
        [JsonIgnore] public bool IsStyle => Style != null;
        [JsonIgnore] public BlockKind KindEnum => Kind == "projectile" ? BlockKind.Projectile : Kind == "control" ? BlockKind.Control : BlockKind.Modifier;
        [JsonIgnore] public bool IsProjectile => Kind == "projectile";
        [JsonIgnore] public bool IsSplit => Kind == "control" && SplitCount > 1;
        [JsonIgnore] public bool IsTrigger => Id == Ids.Block.OnHitTrigger;
        [JsonIgnore] public int IntervalTicks => SimConst.Ticks(IntervalSeconds);
        [JsonIgnore] public int HeatHu => SimConst.HeatToHu(Heat);
        public bool HasFlag(string flag) => Flags != null && Flags.Contains(flag);
    }

    /// <summary>blocks.json params. 없는 값은 0.</summary>
    public sealed class BlockParams
    {
        public float ExplosionRadius;
        public int ChainHops;
        public float ChainRadius;
        public float ChainHopMultiplier;
        public float ReturnArriveRadius;
        public float ReturnMaxDistance;
        public float SlowPercent;
        public float SlowSeconds;
        public float FanHalfAngleDegrees;
        public int BounceCount;
        public float RadiusMultiplier;
        public float RadiusMultiplierCap;
        public int PierceCount;
        public float HomingTurnDegreesPerSecond;
        public float HomingConeHalfDegrees;
        public float HomingRange;
        public float CritMultiplier;
        public float DamageTakenBonusPercent;
        public float DamageTakenBonusSeconds;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    /// <summary>elements.json.</summary>
    public sealed class ElementsDef
    {
        public float WeakMultiplier;
        public float ResistMultiplier;
    }

    /// <summary>characters.json 한 줄(baseline도 같은 꼴, Id 없음). 02 요약서 8.2절.</summary>
    public sealed class CharacterDef
    {
        public string Id;
        public string Element;
        public string Passive;
        public string WandTrait;
        public string UniqueArt;
        public int MaxHpCells;
        public float WalkSpeed;
        public float StanceSpeed;
        public float HitRadius;
        public float BodyRadius;
        public float GrazeRadius;
        public float InvulnSeconds;
        public int CrystalCap;
        public int ComboPerCrystal;
        public int CrystalLossOnHit;
        public int BaseLinkSlots;
        public int MaxMutations;
        public float CoolPerSecond;
        public float CoolDelaySeconds;
        public float CritStart;
        public float LockSeconds;
        public float GrazeBuffSeconds;
        public float GrazeBuffSpeed;

        [JsonIgnore] public int Number => Ids.Char.NumberOf(Id);
    }

    public sealed class CharactersFile
    {
        public CharacterDef Baseline;
        public List<CharacterDef> Characters = new List<CharacterDef>();
    }

    /// <summary>arts.json 한 줄. 04 요약서 9.1절. duel 절은 솔로 제외라 읽지 않는다.</summary>
    public sealed class ArtDef
    {
        public string Id;
        /// <summary>"common" / "secret" / "unique".</summary>
        public string Kind;
        public string Owner;
        /// <summary>경로 배열. 글자 "F" "B" "L" "R". 좌우 대칭 경로는 둘 다 들어 있다.</summary>
        public List<List<string>> Paths = new List<List<string>>();
        public bool Just;
        public int Difficulty;
        /// <summary>정수 또는 "all"(만개).</summary>
        public JToken Cost;
        public int MinCost;
        public bool Credit = true;
        public string Fallback;
        public ArtUnlockDef Unlock = new ArtUnlockDef();
        public ArtEffectDef Effect = new ArtEffectDef();

        [JsonIgnore] public bool CostIsAll => Cost != null && Cost.Type == JTokenType.String;
        [JsonIgnore] public int CostInt => Cost != null && Cost.Type == JTokenType.Integer ? (int)Cost : 0;
        [JsonIgnore] public bool IsUnique => Kind == "unique";
    }

    public sealed class ArtUnlockDef
    {
        public bool AtStart;
        public string Prereq;
        public int MinWave;
    }

    /// <summary>arts.json effect. 없는 값은 0. 뜻은 04 요약서 9.1절 표.</summary>
    public sealed class ArtEffectDef
    {
        public float Damage;
        public float SideDamage;
        public int Branches;
        public float BranchAngleDegrees;
        public float Knockback;
        public float SelfMove;
        public float StaggerSeconds;
        public float InvulnSeconds;
        public float Width;
        public float Range;
        public bool IgnoresArmor;
        public float Radius;
        public float HeatPerT;
        public float ClearRadius;
        public float PrepSeconds;
        public float StunSeconds;
        public float BossHaltSeconds;
        public float RepeatIntervalSeconds;
        public float DurationSeconds;
        public float IntervalSeconds;
        public float SlowPercent;
        public float HeatAbsorb;
        public int MaxConcurrent;
        public int MaxCopies;
        public float Offset;
        public float Thickness;
        public float ShardLength;
        public float PullDistance;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    /// <summary>tries.json. 04 요약서 0.3절.</summary>
    public sealed class TriesDef
    {
        public ParserDef Parser = new ParserDef();
        public List<TrieCharacterDef> Characters = new List<TrieCharacterDef>();
    }

    public sealed class ParserDef
    {
        public float JustWindowSeconds;
        public float GapFullBloomSeconds;
        public float GapShortSeconds;
        public float GapSameSideSeconds;
        public float GapDefaultSeconds;
        public float ArtCooldownSeconds;
        public float KBufferWindowSeconds;
        public int MaxDebt;
    }

    public sealed class TrieCharacterDef
    {
        public string Character;
        public List<string> Arts = new List<string>();
    }

    /// <summary>arena.json 한 줄. 02 요약서 4.6절.</summary>
    public sealed class ArenaDef
    {
        public string Id;
        public List<string> Modes = new List<string>();
        public float Width;
        public float Height;
        public List<PointDef> Pillars = new List<PointDef>();
        public List<PointDef> SpawnPoints = new List<PointDef>();
    }

    public sealed class PointDef
    {
        public float X;
        public float Y;
    }
}
