using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Data
{
    /// <summary>공격 정의. enemies/elites/bosses 공통. 05 요약서 8.5절.</summary>
    public sealed class AttackDef
    {
        public string Key;
        /// <summary>contact / dash / zone / aimed_shot / spread / ring_shot / lob / self_destruct / tongue.</summary>
        public string Kind;
        /// <summary>circle / band / fan / aim_line / ring.</summary>
        public string Shape;
        public float Radius;
        public float InnerRadius;
        public float Width;
        /// <summary>null = 벽까지.</summary>
        public float? Length;
        public float FanDegrees;
        public float Range;
        public float TriggerMin;
        public float TriggerMax;
        public float TelegraphSeconds;
        public float CastSeconds;
        /// <summary>at_start / last_n_ticks.</summary>
        public string Lock;
        public float LockSeconds;
        /// <summary>self / player_pos_at_start.</summary>
        public string Anchor;
        public int ActiveTicks;
        public float DashSpeed;
        public float RecoverySeconds;
        public int Damage;
        /// <summary>contact / projectile / telegraph / lingering.</summary>
        public string DamageSource;
        public AttackProjDef Proj;
        public bool Grazeable;
        public bool IsDash;
        public bool Lob;
        public int EscapeOverrideMilli;
        public float CooldownSeconds;
        public HazardDef LeavesHazard;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class AttackProjDef
    {
        public float Speed;
        public int Count = 1;
        public float SpreadDegrees;
        /// <summary>null = 벽까지.</summary>
        public float? Range;
        public float HitRadius;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class HazardDef
    {
        /// <summary>thorn_field / ember_strip / spore_cloud / frost_field.</summary>
        public string Kind;
        public float Radius;
        public float DurationSeconds;
        public int MaxCount;
        public float ContactCooldownSeconds;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    /// <summary>enemies.json 한 줄. 05 요약서 6절.</summary>
    public sealed class EnemyDef
    {
        public string Id;
        public string Role;
        public float BaseHp;
        public float MoveSpeed;
        public int AttackDamage;
        public int ContactDamage;
        public int Cost;
        public int FirstWave;
        public List<string> Times = new List<string>();
        public string Weak;
        public string Resist;
        public List<string> Tags = new List<string>();
        public bool KnockbackImmune;
        public int? SpeciesCap;
        public float BodyRadius;
        public List<AttackDef> Attacks = new List<AttackDef>();
        /// <summary>종별 AI 값. 열쇠가 종마다 달라 원문 그대로 둔다(적 AI 작업이 읽는다).</summary>
        public JObject Ai;
        public List<int> SpawnPack;
        public bool Minion;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;

        [JsonIgnore] public EnemyTags TagFlags => EnemyTagsUtil.Parse(Tags);
    }

    /// <summary>elites.json 한 줄. 05 요약서 7절.</summary>
    public sealed class EliteDef
    {
        /// <summary>"elite." + 바탕 종 열쇠.</summary>
        public string Id;
        public string Ability;
        public JObject Params;
        public List<EliteAttackDef> Attacks = new List<EliteAttackDef>();
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class EliteAttackDef
    {
        /// <summary>바탕 공격 첨자 n 대신.</summary>
        public int? Replaces;
        /// <summary>바탕 공격 n의 판정 틱에 이어서.</summary>
        public int? Follows;
        public AttackDef Attack;
    }

    /// <summary>bosses.json. 05 요약서 8절.</summary>
    public sealed class BossesFile
    {
        public BossCommonDef Common = new BossCommonDef();
        public List<BossDef> Bosses = new List<BossDef>();
    }

    public sealed class BossCommonDef
    {
        public float IntroSeconds;
        public float PhaseTransitionSeconds;
        public float WaitSeconds;
        public float NoCandidateWaitSeconds;
        public float R5RetrySeconds;
        public float R5RepickSeconds;
        public float RageStackSeconds;
        public int RageMaxStacks;
        public float RageStepRatio;
        public float FinalRageSeconds;
        public int FinalRageDamagePlus;
        public float ActionHaltSeconds;
        public float ContactCooldownSeconds;
    }

    public sealed class BossDef
    {
        public string Id;
        public int RotationIndex;
        public float BaseHp;
        public int HpScaleAnchorWave;
        public float MoveSpeed;
        public float? MoveSpeedOnFrost;
        public int ContactDamage;
        public float BodyRadius;
        public string Element;
        public string Weak;
        public string Resist;
        public List<BossPhaseDef> Phases = new List<BossPhaseDef>();
        public List<BossPatternDef> Patterns = new List<BossPatternDef>();
        public List<BossOverrideDef> SecondCycle = new List<BossOverrideDef>();
        public JObject Movement;
        public JObject PhantomCopy;
        public PhantomFirstEncounterDef FirstEncounter;
        public JObject Body;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class BossPhaseDef
    {
        public int Index;
        public int EnterAtHpPercent;
        public List<string> Patterns = new List<string>();
        public List<string> Passives = new List<string>();
        public List<string> Tags = new List<string>();
        public float AnchorRadius;
        public List<BossOverrideDef> Overrides = new List<BossOverrideDef>();
    }

    public sealed class BossOverrideDef
    {
        public string Pattern;
        public int Step;
        public string Key;
        public JToken Value;
    }

    public sealed class BossPatternDef
    {
        public string Key;
        public bool Passive;
        /// <summary>문자열 하나 또는 배열(charge / bullet / zone).</summary>
        public JToken Graze;
        public float RecoverySeconds;
        public float RecoveryDamageRatio;
        public List<BossStepDef> Steps = new List<BossStepDef>();
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class BossStepDef
    {
        public AttackDef Attack;
        public int Repeat = 1;
        public float RepeatIntervalSeconds;
        public float RepeatAngleDegrees;
        public bool TelegraphEachRepeat;
        public int Count = 1;
        public float StartDelaySeconds;
        public JObject Placement;
        public JObject Obstacle;
        /// <summary>새 구역을 깔 때 남는 위험 구역과 띄울 거리(칸).</summary>
        public float ClearHazardMargin;
        /// <summary>연사형(격노로 간격이 줄어듦, 05 요약서 8.3절). 하한 = MinIntervalTicks.</summary>
        public bool RapidFire;
        public int MinIntervalTicks;
        public BossStreamDef Stream;
        /// <summary>서리밭 위 예고 길이(초, 흰뿔).</summary>
        public float FrostTelegraphSeconds;
        /// <summary>남는 구역 위 이동 속도 배율.</summary>
        public float HazardSpeedRatio;
        /// <summary>허상의 비기 따라 쓰기.</summary>
        public BossCopyDef Copy;
        /// <summary>졸개 불러내기.</summary>
        public BossSummonDef Summon;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    /// <summary>step.stream: 연속 사격. 05 요약서 8.4절.</summary>
    public sealed class BossStreamDef
    {
        public int ShotsPerSecond;
        public float SweepDegrees;
        public float SweepDegreesPerSecond;
    }

    /// <summary>step.copy: 허상이 따라 쓰는 비기 목록(해금된 것 중), 없으면 fallback, 예고 초.</summary>
    public sealed class BossCopyDef
    {
        public List<string> Arts = new List<string>();
        public string Fallback;
        public float WarnSeconds;
    }

    /// <summary>step.summon: 불러낼 적 id, 한 번에 수, 전장 상한(0 = 없음), 예고 초.</summary>
    public sealed class BossSummonDef
    {
        public string Enemy;
        public int Count;
        public int FieldCap;
        public float WarnSeconds;
    }

    /// <summary>허상 첫 만남 설정. 05 요약서 10.2절.</summary>
    public sealed class PhantomFirstEncounterDef
    {
        public int HpFloorPercent;
        public float MaxFightSeconds;
        public int RageStacks;
        public int StartPhase;
        public float FinisherSeconds;
        public int FinisherHpZeroTicks;
        public float CollapseSeconds;
    }
}
