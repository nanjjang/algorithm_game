using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Meta.Enhance
{
    /// <summary>
    /// 강화 보충 수치(Resources/DataSolo/G12.json). solo/enhance.json(EnhanceConfig)에 없는 값만 둔다.
    /// 값의 뜻과 출처는 그 파일의 _note. 파일이 없으면 전부 0/false(기록을 남기지 않고 흔적 조각 0)로 돈다.
    /// </summary>
    public sealed class EnhanceTuning
    {
        public const string ExtraName = "G12";

        /// <summary>흔적 조각 상한(07 요약서 4.9절).</summary>
        public int ScarMax;
        /// <summary>"보유 재화로 평균 N번" 계산의 상한(07 요약서 6절).</summary>
        public int OwnedForecastMaxAttempts;
        /// <summary>의식 중 J 재입력을 무시하는 틱 수(07 요약서 5.4절).</summary>
        public int SkipIgnoreTicks;
        public int ShortResultTicks;
        public int FastChargeTicks;
        public int FastAttemptTicks;
        /// <summary>빠른 강화를 켜도 전체 의식을 하는 현재 단계의 하한.</summary>
        public int FastFullRitualMinStage;
        /// <summary>+13 첫 도달 연출에서 건너뛸 수 없는 처음 구간.</summary>
        public int Plus13FirstLockTicks;
        public bool AutoStopOnDropDefault;
        /// <summary>enhance_log_tail에 남기는 줄 수. 0 이하면 남기지 않는다.</summary>
        public int LogTailMax;

        public static EnhanceTuning FromJson(JObject json)
        {
            if (json == null) return new EnhanceTuning();
            return new EnhanceTuning
            {
                ScarMax = Int(json, "scar_max"),
                OwnedForecastMaxAttempts = Int(json, "owned_forecast_max_attempts"),
                SkipIgnoreTicks = Int(json, "skip_ignore_ticks"),
                ShortResultTicks = Int(json, "short_result_ticks"),
                FastChargeTicks = Int(json, "fast_charge_ticks"),
                FastAttemptTicks = Int(json, "fast_attempt_ticks"),
                FastFullRitualMinStage = Int(json, "fast_full_ritual_min_stage"),
                Plus13FirstLockTicks = Int(json, "plus13_first_lock_ticks"),
                AutoStopOnDropDefault = Bool(json, "auto_stop_on_drop_default"),
                LogTailMax = Int(json, "log_tail_max"),
            };
        }

        public static EnhanceTuning From(IContentDb content) =>
            FromJson(content != null ? content.SoloExtra(ExtraName) : null);

        static int Int(JObject json, string key)
        {
            JToken t = json[key];
            return t != null && t.Type == JTokenType.Integer ? (int)t : 0;
        }

        static bool Bool(JObject json, string key)
        {
            JToken t = json[key];
            return t != null && t.Type == JTokenType.Boolean && (bool)t;
        }
    }
}
