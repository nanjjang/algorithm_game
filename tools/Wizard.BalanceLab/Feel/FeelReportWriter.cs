using System;
using System.Collections.Generic;
using System.Text.Json;
using Wizard.BalanceLab.Core;
using Wizard.Content;
using Wizard.Sim;

namespace Wizard.BalanceLab.Feel
{
    // 판정 근거 JSON(시도 수, 성공 수, 간격 분포). 모든 수는 정수이고 비율은 천분율이다.
    // 축마다 측정값과 명세의 목표값을 나란히 적는다(CLAUDE.md "계측 도구는 측정값과 목표값을 나란히").
    internal static class FeelReportWriter
    {
        private const int SchemaVersion = 1;
        private const string SourceGdd16 = "GDD 16절 1단계 통과 기준";

        public static Verdict OverallOf(FireRatioReport? fire, FeelResult? feel, out string overall)
        {
            var axes = new List<Verdict>();
            if (fire != null)
            {
                axes.Add(fire.Verdict);
            }
            if (feel != null)
            {
                axes.Add(FeelJudge.TwoPress(feel));
                axes.Add(FeelJudge.Just(feel));
                axes.Add(FeelJudge.Misfire(feel));
            }
            overall = VerdictNames.Overall(axes.ToArray());
            return overall == "pass" ? Verdict.Pass : overall == "fail" ? Verdict.Fail : Verdict.NoData;
        }

        public static string Build(FireRatioReport? fire, FeelRecordInfo? record, FeelResult? feel, FeelOptions options, bool detail)
        {
            return JsonOut.Build(w =>
            {
                OverallOf(fire, feel, out string overall);
                w.WriteStartObject();
                w.WriteString("tool", "balancelab feel");
                w.WriteNumber("schema", SchemaVersion);
                w.WriteString("verdict", overall);
                if (record != null)
                {
                    WriteRecord(w, record);
                }
                if (feel != null)
                {
                    w.WriteStartObject("options");
                    w.WriteNumber("practice_minutes", options.PracticeMinutes);
                    w.WriteNumber("attempt_window_ms", options.AttemptWindowMs);
                    w.WriteEndObject();
                }
                w.WriteStartObject("axes");
                if (fire != null)
                {
                    WriteFireRatio(w, fire);
                }
                if (feel != null)
                {
                    WriteTwoPress(w, feel, detail);
                    WriteJust(w, feel);
                    WriteMisfire(w, feel);
                }
                w.WriteEndObject();
                w.WriteEndObject();
            });
        }

        private static void WriteRecord(Utf8JsonWriter w, FeelRecordInfo r)
        {
            w.WriteStartObject("record");
            w.WriteString("file", r.FileName);
            w.WriteNumber("bytes", r.Bytes);
            w.WriteString("sha256", r.Sha256);
            w.WriteString("world", "practice");
            w.WriteNumber("ticks", r.EndTick);
            w.WriteNumber("input_count", r.InputCount);
            w.WriteNumber("command_count", r.CommandCount);
            w.WriteNumber("pause_count", r.PauseCount);
            w.WriteNumber("data_version", r.DataVersion);
            w.WriteString("core_version", r.CoreVersion);
            w.WriteBoolean("replay_hash_matches", r.HashMatches);
            w.WriteBoolean("replay_hash_check_skipped", r.HashCheckSkipped);
            w.WriteNumber("rejected_commands", r.RejectedCommands);
            w.WriteEndObject();
        }

        private static void WriteTarget(Utf8JsonWriter w, string kind, int permille)
        {
            w.WriteStartObject("target");
            w.WriteString("kind", kind);                    // min_permille: 이 값 이상, below_permille: 이 값 미만
            w.WriteNumber("permille", permille);
            w.WriteString("source", SourceGdd16);
            w.WriteEndObject();
        }

        private static void WriteBot(Utf8JsonWriter w, string name, BotRun b)
        {
            w.WriteStartObject(name);
            w.WriteNumber("ticks_run", b.Ticks);
            w.WriteNumber("cycles", b.Cycles);
            w.WriteNumber("window_start_tick", b.WindowStartTick);
            w.WriteNumber("window_ticks", b.WindowTicks);
            w.WriteNumber("window_shots", b.WindowShots);
            w.WriteNumber("window_crit_shots", b.WindowCritShots);
            w.WriteNumber("window_damage_milli", IntMath.RawToMilli(b.WindowDamageRaw));
            w.WriteNumber("dps_milli", IntMath.DpsMilli(b.WindowDamageRaw, b.WindowTicks));
            w.WriteNumber("total_shots", b.TotalShots);
            w.WriteNumber("graze_events", b.GrazeEvents);
            w.WriteEndObject();
        }

        private static void WriteFireRatio(Utf8JsonWriter w, FireRatioReport f)
        {
            w.WriteStartObject("fire_ratio");
            w.WriteString("verdict", VerdictNames.Of(f.Verdict));
            w.WriteString("core", f.Core == BlockId.MagicBolt ? "block.magic_bolt" : f.Core == BlockId.Fireball ? "block.fireball" : "other");
            w.WriteBoolean("split_three", f.SplitThree);
            w.WriteNumber("measured_ratio_permille", f.RatioPermille);
            WriteTarget(w, "min_permille", FeelTargets.FireRatioMinPermille);
            w.WriteStartObject("reference");                 // GDD 18절 계산값(마력탄·기준값 한정). 판정에 쓰지 않는다
            w.WriteNumber("hold_dps_milli", FeelTargets.GddHoldDpsMilli);
            w.WriteNumber("crit_dps_milli", FeelTargets.GddCritDpsMilli);
            w.WriteNumber("ratio_permille", FeelTargets.GddRatioPermille);
            w.WriteString("source", "GDD 18절 검증 요약(연속 시간 계산)");
            w.WriteEndObject();
            WriteBot(w, "hold_fire", f.Hold);
            WriteBot(w, "crit_zone", f.Crit);
            w.WriteEndObject();
        }

        private static void WriteTwoPress(Utf8JsonWriter w, FeelResult r, bool detail)
        {
            w.WriteStartObject("two_press_command");
            w.WriteString("verdict", VerdictNames.Of(FeelJudge.TwoPress(r)));
            w.WriteString("command", "앞, 앞, K (art.spear)");
            w.WriteNumber("attempts", r.TwoPressAttempts);
            w.WriteNumber("successes", r.TwoPressSuccesses);
            w.WriteNumber("measured_rate_permille", IntMath.PermilleFloor(r.TwoPressSuccesses, r.TwoPressAttempts));
            WriteTarget(w, "min_permille", FeelTargets.TwoPressMinPermille);
            WriteResultCounts(w, r.Attempts);
            WriteGapDistribution(w, "k_gap", r.Attempts);
            if (detail)
            {
                w.WriteStartArray("attempt_list");
                foreach (AttemptRecord a in r.Attempts)
                {
                    w.WriteStartObject();
                    w.WriteNumber("tick", a.Tick);
                    w.WriteNumber("k_gap_units", a.GapUnits);
                    w.WriteNumber("k_gap_ms", IntMath.UnitsToMs(a.GapUnits));
                    w.WriteNumber("first_gap_ticks", a.FirstGapTicks);
                    w.WriteString("result", a.ResultName);
                    w.WriteEndObject();
                }
                w.WriteEndArray();
            }
            w.WriteEndObject();
        }

        private static void WriteResultCounts(Utf8JsonWriter w, List<AttemptRecord> attempts)
        {
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (AttemptRecord a in attempts)
            {
                string key = a.ResultName;
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            w.WriteStartObject("results");
            foreach (KeyValuePair<string, int> kv in counts)
            {
                w.WriteNumber(kv.Key, kv.Value);
            }
            w.WriteEndObject();
        }

        private static void WriteJust(Utf8JsonWriter w, FeelResult r)
        {
            w.WriteStartObject("just");
            w.WriteString("verdict", VerdictNames.Of(FeelJudge.Just(r)));
            w.WriteNumber("practice_ticks", r.PracticeTicks);
            w.WriteNumber("window_units", FeelTargets.JustWindowUnits);
            w.WriteNumber("window_ms", IntMath.UnitsToMs(FeelTargets.JustWindowUnits));
            w.WriteNumber("attempts", r.JustAttempts);
            w.WriteNumber("successes", r.JustSuccesses);
            w.WriteNumber("measured_rate_permille", IntMath.PermilleFloor(r.JustSuccesses, r.JustAttempts));
            WriteTarget(w, "min_permille", FeelTargets.JustMinPermille);
            w.WriteNumber("attempts_before_practice_end", r.TwoPressAttempts - r.JustAttempts);   // 연습 시간 안의 시도(판정에 안 씀)
            var after = new List<AttemptRecord>();
            foreach (AttemptRecord a in r.Attempts)
            {
                if (a.Tick >= r.PracticeTicks)
                {
                    after.Add(a);
                }
            }
            WriteGapDistribution(w, "k_gap", after);
            w.WriteEndObject();
        }

        private static void WriteMisfire(Utf8JsonWriter w, FeelResult r)
        {
            w.WriteStartObject("stance_misfire");
            w.WriteString("verdict", VerdictNames.Of(FeelJudge.Misfire(r)));
            w.WriteNumber("k_presses_total", r.KPresses);
            w.WriteNumber("casts_moving_in_stance", r.MisfireCasts);       // 분모
            w.WriteNumber("misfires", r.Misfires);                         // 분자: 마력 방출이 아닌 기술이 나감
            w.WriteNumber("misfire_recoil", r.MisfireRecoil);
            w.WriteNumber("misfire_spear", r.MisfireSpear);
            w.WriteNumber("misfire_other", r.MisfireOther);
            w.WriteNumber("no_cast_moving_in_stance", r.IgnoredOrUnresolvedMoving);
            w.WriteNumber("measured_rate_permille", IntMath.PermilleFloor(r.Misfires, r.MisfireCasts));
            WriteTarget(w, "below_permille", FeelTargets.MisfireBelowPermille);
            w.WriteEndObject();
        }

        // 마지막 방향 → K 간격의 분포. 단위는 입력 시각 단위(정확)와 밀리초(반올림)
        private static void WriteGapDistribution(Utf8JsonWriter w, string name, List<AttemptRecord> attempts)
        {
            w.WriteStartObject(name + "_distribution");
            w.WriteNumber("count", attempts.Count);
            if (attempts.Count > 0)
            {
                var gaps = new long[attempts.Count];
                for (int i = 0; i < gaps.Length; i++)
                {
                    gaps[i] = attempts[i].GapUnits;
                }
                Array.Sort(gaps);
                WriteStat(w, "min", gaps[0]);
                WriteStat(w, "median", gaps[(gaps.Length - 1) / 2]);                 // 아래쪽 중앙값
                WriteStat(w, "p90", gaps[(int)(((long)gaps.Length * 9 + 9) / 10) - 1]);   // 최근접 순위 90%
                WriteStat(w, "max", gaps[gaps.Length - 1]);
            }
            int[] edges = FeelTargets.GapEdgesMs;
            var counts = new int[edges.Length + 1];
            foreach (AttemptRecord a in attempts)
            {
                int bucket = edges.Length;
                for (int i = 0; i < edges.Length; i++)
                {
                    if (a.GapUnits <= SimTime.InputUnitsFromDecimalSeconds(edges[i], 3))
                    {
                        bucket = i;
                        break;
                    }
                }
                counts[bucket]++;
            }
            w.WriteStartArray("histogram");
            for (int i = 0; i <= edges.Length; i++)
            {
                w.WriteStartObject();
                if (i < edges.Length)
                {
                    w.WriteNumber("max_ms", edges[i]);                              // 이 밀리초 이하
                }
                else
                {
                    w.WriteString("max_ms", "inf");
                }
                w.WriteNumber("count", counts[i]);
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }

        private static void WriteStat(Utf8JsonWriter w, string name, long units)
        {
            w.WriteStartObject(name);
            w.WriteNumber("units", units);
            w.WriteNumber("ms", IntMath.UnitsToMs(units));
            w.WriteEndObject();
        }
    }
}
