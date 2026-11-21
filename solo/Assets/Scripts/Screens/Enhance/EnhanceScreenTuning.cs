using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>시간표 사건 하나(판정·구간 시작 기준 틱). 소리(EventId)와 화면 효과(Fx) 둘 중 하나 또는 둘 다.</summary>
    public sealed class EnhanceCueSpec
    {
        public double Seconds;
        public string EventId;
        /// <summary>"ui"(기본) / "wand".</summary>
        public string Bus;
        public string Fx;
        /// <summary>조건(scar_shed, slot_expanded, aura_gained). null이면 언제나.</summary>
        public string If;
        public double FxSeconds;
        public double Alpha;
        public string Color;
        public string Shake;
    }

    /// <summary>
    /// 강화 화면의 연출 시간표·표시 수치(Resources/DataSolo/G23.json). 값의 뜻과 출처는 그 파일의 _note.
    /// 초 단위로 읽고 틱(1초 = 60틱)으로 바꿔 쓴다. 엔진에 기대지 않는다(EditMode 테스트가 그대로 읽는다).
    /// </summary>
    public sealed class EnhanceScreenTuning
    {
        public const string ExtraName = "G23";
        public const int TicksPerSecond = SimConst.TicksPerSecond;

        public int[] IntensityMinTarget = new int[0];
        public double PaySeconds, OfferWithSacrificeSeconds, OfferWithoutSacrificeSeconds, OfferIntervalSeconds;
        public double[] ChargeSeconds = new double[0], HoldSeconds = new double[0];
        public int HeartBpmStart;
        public int[] HeartBpmEnd = new int[0];
        public int[] PitchSemitones = new int[0];
        public int[] OfferSemitones = new int[0];
        public double[] CameraZoom = new double[0];
        public int BackgroundDimIdlePct;
        public int[] BackgroundDimPct = new int[0];
        public int[] CircleLayers = new int[0];
        public int[] ChargeParticlesPerSecond = new int[0];
        public int[] BgmDuckDb = new int[0];
        public bool[] BgmOff = new bool[0];
        public int WandFloatMinIntensity, OuterGlyphMinIntensity, MasterPeekMinIntensity;
        public int WandFloatPx, ChargeTremblePx;
        public double GaugeKneeTime, GaugeKneeValue;
        public double FastChargeSeconds, FastAttemptSeconds;
        public int FastFullRitualMinTarget;
        public double SkipIgnoreSeconds, ShortResultSeconds, FakeFailSeconds, RejectReplayFromSeconds;
        public double FailSilenceAfterSeconds;
        public double[] FailSilenceSeconds = new double[0];
        public int ReferenceIntensity;
        public readonly Dictionary<string, double> ResultSeconds = new Dictionary<string, double>();
        public double MutationAcceptSeconds, MilestoneFirstSeconds, MilestoneAgainSeconds;
        public double Plus13FullSeconds, Plus13RepeatFromSeconds, Plus13RepeatToSeconds, Plus13LockSeconds;
        public double Plus13CutSuccess, Plus13CutGreatFrom12, Plus13CutGreatFrom11;
        public double GreatSecondSlamSeconds;
        public readonly Dictionary<string, List<EnhanceCueSpec>> ResultEvents = new Dictionary<string, List<EnhanceCueSpec>>();
        public int SuccessShakeMediumMinIntensity;
        public int DarkenBrightnessPct, DarkenBrightnessReducedPct, NoDropIntensity1BrightnessPct;
        public double DarkenTransitionSeconds;
        public double[] FlashCap = new double[0];
        public double FlashFastCap, FlashMinIntervalSeconds, FlashWindowSeconds, FlashRiseSeconds, FlashFallSeconds;
        public int FlashPerAttemptMax, FlashWindowMax;
        public int BorderHighlightPx;
        public double BorderHighlightSeconds;
        public readonly Dictionary<string, double> ShakeAdd = new Dictionary<string, double>();
        public double ShakeDecayPerSecond;
        public int ShakeMaxPx;
        public double[] ShakeLevelScale = new double[0], CameraLevelScale = new double[0], DimLevelScale = new double[0],
            HitstopLevelScale = new double[0];
        public double ProbRollSeconds;
        public int CostShakePx;
        public double CostShakeSeconds;
        public int RollDecimals;
        public int ScarMax;
        public int CandidatesPerRow;
        public int ExpectedTableRows;
        public bool AutoStopOnDropDefault;
        public int OwnedForecastRuns, OwnedForecastMaxAttempts;
        public long OwnedForecastSeed;
        public int ReleaseNameConfirmMinStage, ReleaseMultiMaxStage, ReleaseMultiMaxMutations, ReleaseMultiMaxSlotsExtra;
        public int TutorialPages;
        public double SlamScale, SlamSettleSeconds, SealSpinIdleSeconds, SealSpinChargedSeconds, WhiteGoldMix;
        /// <summary>캐릭터 교체 묶음의 강화 연출 표(G23 character_fx: char.N → 역할 → 묶음 열쇠). 없으면 null.</summary>
        public JObject CharacterFx;

        /// <summary>초 → 틱(반올림).</summary>
        public static int Ticks(double seconds) => (int)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero);

        public static EnhanceScreenTuning From(IContentDb content) =>
            FromJson(content != null ? content.SoloExtra(ExtraName) : null);

        public static EnhanceScreenTuning FromJson(JObject j)
        {
            var t = new EnhanceScreenTuning();
            if (j == null) return t;
            t.IntensityMinTarget = Ints(j, "intensity_min_target");
            t.CharacterFx = j["character_fx"] as JObject;
            t.PaySeconds = D(j, "pay_seconds");
            t.OfferWithSacrificeSeconds = D(j, "offer_with_sacrifice_seconds");
            t.OfferWithoutSacrificeSeconds = D(j, "offer_without_sacrifice_seconds");
            t.OfferIntervalSeconds = D(j, "offer_interval_seconds");
            t.ChargeSeconds = Ds(j, "charge_seconds");
            t.HoldSeconds = Ds(j, "hold_seconds");
            t.HeartBpmStart = I(j, "heart_bpm_start");
            t.HeartBpmEnd = Ints(j, "heart_bpm_end");
            t.PitchSemitones = Ints(j, "pitch_semitones");
            t.OfferSemitones = Ints(j, "offer_semitones");
            t.CameraZoom = Ds(j, "camera_zoom");
            t.BackgroundDimIdlePct = I(j, "background_dim_idle_pct");
            t.BackgroundDimPct = Ints(j, "background_dim_pct");
            t.CircleLayers = Ints(j, "circle_layers");
            t.ChargeParticlesPerSecond = Ints(j, "charge_particles_per_second");
            t.BgmDuckDb = Ints(j, "bgm_duck_db");
            t.BgmOff = Bools(j, "bgm_off");
            t.WandFloatMinIntensity = I(j, "wand_float_min_intensity");
            t.OuterGlyphMinIntensity = I(j, "outer_glyph_min_intensity");
            t.MasterPeekMinIntensity = I(j, "master_peek_min_intensity");
            t.WandFloatPx = I(j, "wand_float_px");
            t.ChargeTremblePx = I(j, "charge_tremble_px");
            t.GaugeKneeTime = D(j, "gauge_knee_time");
            t.GaugeKneeValue = D(j, "gauge_knee_value");
            t.FastChargeSeconds = D(j, "fast_charge_seconds");
            t.FastAttemptSeconds = D(j, "fast_attempt_seconds");
            t.FastFullRitualMinTarget = I(j, "fast_full_ritual_min_target");
            t.SkipIgnoreSeconds = D(j, "skip_ignore_seconds");
            t.ShortResultSeconds = D(j, "short_result_seconds");
            t.FakeFailSeconds = D(j, "fake_fail_seconds");
            t.RejectReplayFromSeconds = D(j, "reject_replay_from_seconds");
            t.FailSilenceAfterSeconds = D(j, "fail_silence_after_seconds");
            t.FailSilenceSeconds = Ds(j, "fail_silence_seconds");
            t.ReferenceIntensity = I(j, "reference_intensity");
            if (j["result_seconds"] is JObject rs)
                foreach (var p in rs.Properties()) t.ResultSeconds[p.Name] = ToD(p.Value);
            t.MutationAcceptSeconds = D(j, "mutation_accept_seconds");
            t.MilestoneFirstSeconds = D(j, "milestone_first_seconds");
            t.MilestoneAgainSeconds = D(j, "milestone_again_seconds");
            t.Plus13FullSeconds = D(j, "plus13_full_seconds");
            t.Plus13RepeatFromSeconds = D(j, "plus13_repeat_from_seconds");
            t.Plus13RepeatToSeconds = D(j, "plus13_repeat_to_seconds");
            t.Plus13LockSeconds = D(j, "plus13_lock_seconds");
            if (j["plus13_cut"] is JObject cut)
            {
                t.Plus13CutSuccess = D(cut, "success");
                t.Plus13CutGreatFrom12 = D(cut, "great_success_from_12");
                t.Plus13CutGreatFrom11 = D(cut, "great_success_from_11");
            }
            t.GreatSecondSlamSeconds = D(j, "great_second_slam_seconds");
            if (j["result_events"] is JObject re)
            {
                foreach (var p in re.Properties())
                {
                    var list = new List<EnhanceCueSpec>();
                    if (p.Value is JArray arr)
                        foreach (var item in arr)
                            if (item is JObject o) list.Add(Cue(o));
                    t.ResultEvents[p.Name] = list;
                }
            }
            t.SuccessShakeMediumMinIntensity = I(j, "success_shake_medium_min_intensity");
            t.DarkenBrightnessPct = I(j, "darken_brightness_pct");
            t.DarkenBrightnessReducedPct = I(j, "darken_brightness_reduced_pct");
            t.NoDropIntensity1BrightnessPct = I(j, "no_drop_intensity1_brightness_pct");
            t.DarkenTransitionSeconds = D(j, "darken_transition_seconds");
            t.FlashCap = Ds(j, "flash_cap");
            t.FlashFastCap = D(j, "flash_fast_cap");
            t.FlashMinIntervalSeconds = D(j, "flash_min_interval_seconds");
            t.FlashPerAttemptMax = I(j, "flash_per_attempt_max");
            t.FlashWindowSeconds = D(j, "flash_window_seconds");
            t.FlashWindowMax = I(j, "flash_window_max");
            t.FlashRiseSeconds = D(j, "flash_rise_seconds");
            t.FlashFallSeconds = D(j, "flash_fall_seconds");
            t.BorderHighlightPx = I(j, "border_highlight_px");
            t.BorderHighlightSeconds = D(j, "border_highlight_seconds");
            if (j["shake_add"] is JObject sa)
                foreach (var p in sa.Properties()) t.ShakeAdd[p.Name] = ToD(p.Value);
            t.ShakeDecayPerSecond = D(j, "shake_decay_per_second");
            t.ShakeMaxPx = I(j, "shake_max_px");
            t.ShakeLevelScale = Ds(j, "shake_level_scale");
            t.CameraLevelScale = Ds(j, "camera_level_scale");
            t.DimLevelScale = Ds(j, "dim_level_scale");
            t.HitstopLevelScale = Ds(j, "hitstop_level_scale");
            t.ProbRollSeconds = D(j, "prob_roll_seconds");
            t.CostShakePx = I(j, "cost_shake_px");
            t.CostShakeSeconds = D(j, "cost_shake_seconds");
            t.RollDecimals = I(j, "roll_decimals");
            t.ScarMax = I(j, "scar_max");
            t.CandidatesPerRow = I(j, "candidates_per_row");
            t.ExpectedTableRows = I(j, "expected_table_rows");
            t.AutoStopOnDropDefault = B(j, "auto_stop_on_drop_default");
            t.OwnedForecastRuns = I(j, "owned_forecast_runs");
            t.OwnedForecastMaxAttempts = I(j, "owned_forecast_max_attempts");
            t.OwnedForecastSeed = j["owned_forecast_seed"] != null ? (long)ToD(j["owned_forecast_seed"]) : 0;
            t.ReleaseNameConfirmMinStage = I(j, "release_name_confirm_min_stage");
            t.ReleaseMultiMaxStage = I(j, "release_multi_max_stage");
            t.ReleaseMultiMaxMutations = I(j, "release_multi_max_mutations");
            t.ReleaseMultiMaxSlotsExtra = I(j, "release_multi_max_slots_extra");
            t.TutorialPages = I(j, "tutorial_pages");
            t.SlamScale = D(j, "slam_scale");
            t.SlamSettleSeconds = D(j, "slam_settle_seconds");
            t.SealSpinIdleSeconds = D(j, "seal_spin_idle_seconds");
            t.SealSpinChargedSeconds = D(j, "seal_spin_charged_seconds");
            t.WhiteGoldMix = D(j, "white_gold_mix");
            return t;
        }

        // ------------------------------------------------------------------ 표 조회 도우미

        public static int At(int[] a, int i) => a == null || a.Length == 0 ? 0 : a[Clamp(i, a.Length)];
        public static double At(double[] a, int i) => a == null || a.Length == 0 ? 0 : a[Clamp(i, a.Length)];
        public static bool At(bool[] a, int i) => a != null && a.Length > 0 && a[Clamp(i, a.Length)];
        static int Clamp(int i, int n) => i < 0 ? 0 : (i >= n ? n - 1 : i);

        public double ResultLength(string kind) => ResultSeconds.TryGetValue(kind, out double s) ? s : 0;

        public List<EnhanceCueSpec> Events(string kind) =>
            ResultEvents.TryGetValue(kind, out var l) ? l : new List<EnhanceCueSpec>();

        public double ShakeAmount(string cls) => cls != null && ShakeAdd.TryGetValue(cls, out double v) ? v : 0;

        // ------------------------------------------------------------------ 읽기

        static EnhanceCueSpec Cue(JObject o) => new EnhanceCueSpec
        {
            Seconds = D(o, "t"),
            EventId = S(o, "e"),
            Bus = S(o, "bus"),
            Fx = S(o, "fx"),
            If = S(o, "if"),
            FxSeconds = D(o, "s"),
            Alpha = D(o, "alpha"),
            Color = S(o, "color"),
            Shake = S(o, "shake"),
        };

        static double ToD(JToken t) =>
            t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) ? (double)t : 0;
        static double D(JObject j, string k) => ToD(j[k]);
        static int I(JObject j, string k) { var t = j[k]; return t != null && (t.Type == JTokenType.Integer || t.Type == JTokenType.Float) ? (int)Math.Round((double)t) : 0; }
        static bool B(JObject j, string k) { var t = j[k]; return t != null && t.Type == JTokenType.Boolean && (bool)t; }
        static string S(JObject j, string k) { var t = j[k]; return t != null && t.Type == JTokenType.String ? (string)t : null; }

        static int[] Ints(JObject j, string k)
        {
            if (!(j[k] is JArray a)) return new int[0];
            var r = new int[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = (int)Math.Round(ToD(a[i]));
            return r;
        }

        static double[] Ds(JObject j, string k)
        {
            if (!(j[k] is JArray a)) return new double[0];
            var r = new double[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = ToD(a[i]);
            return r;
        }

        static bool[] Bools(JObject j, string k)
        {
            if (!(j[k] is JArray a)) return new bool[0];
            var r = new bool[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = a[i].Type == JTokenType.Boolean && (bool)a[i];
            return r;
        }
    }
}
