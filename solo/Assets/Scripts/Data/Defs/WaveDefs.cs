using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Data
{
    /// <summary>"from..to → value" 계단 표(to가 null이면 끝없이).</summary>
    public sealed class RangeValueDef
    {
        public int From;
        public int? To;
        public int Value;

        /// <summary>계단 표에서 w에 해당하는 값. 없으면 fallback.</summary>
        public static int Lookup(List<RangeValueDef> table, int w, int fallback = 0)
        {
            if (table == null) return fallback;
            for (int i = 0; i < table.Count; i++)
            {
                var r = table[i];
                if (w >= r.From && (!r.To.HasValue || w <= r.To.Value)) return r.Value;
            }
            return fallback;
        }
    }

    public sealed class SpeciesCountDef
    {
        public string Species;
        public int Min;
        public int Max;
    }

    public sealed class SpeciesWeightDef
    {
        public string Species;
        public float Weight;
    }

    public sealed class SpeciesCapDef
    {
        public string Species;
        public int Cap;
    }

    /// <summary>waves.json. 06 요약서 1.1절.</summary>
    public sealed class WavesDef
    {
        public int DayLength;
        public List<TodRangeDef> TodRanges = new List<TodRangeDef>();
        public int HpLinearUntil;
        public float HpLinearStep;
        public float HpExpBase;
        public float HpExpRate;
        public float EliteHpMul;
        public int BossHpRefWave;
        public BudgetLinearDef BudgetLinear = new BudgetLinearDef();
        public BudgetLateDef BudgetLate = new BudgetLateDef();
        public BundleRuleDef BundleRule = new BundleRuleDef();
        public float BundleIntervalSeconds;
        public int BundleEarlyReleaseAlive;
        public int MaxAlive;
        public List<RangeValueDef> TelegraphCap = new List<RangeValueDef>();
        public List<RangeValueDef> EliteCount = new List<RangeValueDef>();
        public int EliteCostMul;
        public List<SpeciesCapDef> SpeciesCap = new List<SpeciesCapDef>();
        public float StallSeconds;
        public float StallKillScore;
        public List<RangeValueDef> ModifierCount = new List<RangeValueDef>();
        public WaveDamageDef Damage = new WaveDamageDef();
        public string FillSpecies;
        public List<WaveScriptDef> Script = new List<WaveScriptDef>();
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class TodRangeDef { public int From; public int To; public string Tod; }
    public sealed class BudgetLinearDef { public int Base; public int PerWave; public int Until; }
    public sealed class BudgetLateDef { public int Base; public int PerWave; public int Cap; }
    public sealed class BundleRuleDef { public int Base; public int GrowAfter; public int GrowEvery; public int Max; }

    public sealed class WaveDamageDef
    {
        public int Contact;
        public int TelegraphPlus;
        public int EliteBossPlusFrom;
        public int AllPlusFrom;
        public int AttackCap;
    }

    /// <summary>고정 각본(웨이브 1~12, 13, 15). 06 요약서 3.3절.</summary>
    public sealed class WaveScriptDef
    {
        public int Wave;
        public string Frame;
        public List<SpeciesCountDef> Fixed = new List<SpeciesCountDef>();
        public List<string> Group = new List<string>();
        public int GroupShare;
        public string Rest;
    }

    /// <summary>frames.json 한 줄. 06 요약서 1.3절.</summary>
    public sealed class FrameDef
    {
        public string Id;
        public List<SpeciesCountDef> Fixed = new List<SpeciesCountDef>();
        public List<string> Group = new List<string>();
        public int GroupShare;
    }

    /// <summary>modifiers.json 한 줄(변덕). 06 요약서 1.4절. 없는 절은 null.</summary>
    public sealed class ModifierDef
    {
        public string Id;
        public float ScoreMultiplier;
        public float? BundleIntervalSeconds;
        public float? EnemyHpMultiplier;
        public ExtraBudgetDef ExtraBudget;
        public int ExtraElites;
        public DeathCircleDef DeathCircle;
        public EnemyShotsDef EnemyShots;
        public string OnlyTod;
        public List<SpeciesWeightDef> Weights;
        public List<SpeciesCapDef> SpeciesCaps;
        public ThornBorderDef ThornBorder;
        [JsonExtensionData] public IDictionary<string, JToken> Extra;
    }

    public sealed class ExtraBudgetDef { public int Percent; public int CostMax; }
    public sealed class DeathCircleDef { public float Radius; public float TelegraphSeconds; public int Damage; public float StaggerSeconds; }
    public sealed class EnemyShotsDef { public float CountMultiplier; public float SpeedMultiplier; }
    public sealed class ThornBorderDef { public float Width; public float TelegraphSeconds; public float ContactIntervalSeconds; public int Damage; }

    /// <summary>tod.json 한 줄. 01 요약서 7.1절.</summary>
    public sealed class TodDef
    {
        public string Id;
        public bool NormalEnemies;
        public List<SpeciesWeightDef> Weights = new List<SpeciesWeightDef>();
        public int RimStrengthPercent;
    }

    /// <summary>scoring.json. 01 요약서 6.1절.</summary>
    public sealed class ScoringDef
    {
        public WaveCoefDef WaveCoef = new WaveCoefDef();
        public int KillPerCost;
        public int BossKill;
        public int WaveClear;
        public FastClearDef FastClear = new FastClearDef();
        public int NoHit;
        public GrazeScoreDef Graze = new GrazeScoreDef();
        public int PhantomFirst;
    }

    public sealed class WaveCoefDef
    {
        /// <summary>JSON 열쇠 "at_wave_1": 스네이크 변환이 숫자 앞에 밑줄을 넣지 않아 이름을 직접 준다.</summary>
        [JsonProperty("at_wave_1")] public float AtWave1;
        public float PerWave;
    }
    public sealed class FastClearDef { public int PerSecond; public int TargetSeconds; public int BossTargetSeconds; }
    public sealed class GrazeScoreDef { public int PerGraze; public int Cap; public int BossCap; }

    /// <summary>starfall.json. 01 요약서 7.3절.</summary>
    public sealed class StarfallDef
    {
        public string OnlyTod;
        public int ChancePercent;
        public FromToDef DropWindowSeconds = new FromToDef();
        public float TelegraphSeconds;
        public float LandedSeconds;
        public float BlinkSeconds;
        public SizeDef Area = new SizeDef();
        public float GridStep;
        public float HazardClearance;
        public float PlayerClearance;
        public float RetrySeconds;
        public float PickRadius;
        public int Crystals;
    }

    public sealed class FromToDef { public float From; public float To; }
    public sealed class SizeDef { public float Width; public float Height; }

    /// <summary>weather_events.json 한 줄. 01 요약서 7.2절.</summary>
    public sealed class WeatherEventDef
    {
        public string Id;
        public bool AffectsRules;
        /// <summary>wave_start / day_change.</summary>
        public string When;
        public List<string> Tods = new List<string>();
        public int ChancePercent;
        public int MaxPerDay;
        /// <summary>timed / wave / instant.</summary>
        public string Span;
        public float DurationSeconds;
        public float EdgeWidth;
        public int Variants;
    }
}
