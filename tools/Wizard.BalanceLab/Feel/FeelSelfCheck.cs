using System;
using System.Collections.Generic;
using System.Text.Json;
using Wizard.BalanceLab.Core;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim;
using Wizard.Sim.Arts;
using Wizard.Sim.Events;
using Wizard.Sim.Heat;
using Wizard.Sim.Input;

namespace Wizard.BalanceLab.Feel
{
    // 계측 도구 자신의 점검: `balancelab feel --selfcheck`. 기대값은 명세에서 가져온다(구현의 지금 출력을 베끼지 않는다).
    // 틀린 입력(일부러 넣은 결함)을 도구가 알아보는지도 본다.
    //   봇:    꾹 누르기 주기 = spec 01 7.5(26발째에 잠김, 21 일반 + 5 임계, 6.9초 = 414틱, 피해 21×10 + 5×20 = 310)
    //          임계 구간 주기 = GDD 18절 "임계 n발 → 0.2초 대기 → 4n 냉각"을 틱으로 푼 것(아래 CritCycle*)
    //   판정:  교차 곱 경계(정확히 1.4배, 정확히 90%, 정확히 10%), 자료 없음
    //   분석:  이벤트 열을 손으로 짠 사례(spec 03 17절: 간격이 한도와 같으면 통과 = 1,075단위는 섬, 1,076은 아니다)
    //   종단:  각본 → 연습 세계 → .wzrc 로 적기 → 읽기 → 재실행(해시 일치) → 분석 → JSON
    internal static class FeelSelfCheck
    {
        // spec 01 7.5: 꾹 누르기 한 주기
        private const int HoldCycleTicks = 414;                   // 26발 × 9틱(0.15초) 가 아니라 (25 × 9) + 9(간격) + 180(잠금 3초) = 414
        private const int HoldCycleShots = 26;
        private const int HoldCycleCritShots = 5;
        private const int HoldCycleDamage = 310;                  // 21 × 10 + 5 × 20

        // GDD 18절 임계 유지(n = 4)를 틱으로 풀면: 열이 임계 시작 근처에서 시작해 임계 4발(85, 89, 93, 97)을 쏘고(다음 발사는 101 > 100 이라 멈춘다),
        // 마지막 발사 뒤 간격 9 + 대기 12 = 21틱이 지나서부터 틱당 90 HU 씩 식고, 임계로 쏠 수 있는 마지막 틱에 다시 쏜다.
        // 열 단위(90 HU/틱, 발사당 480 HU)가 맞아떨어지지 않아 주기가 69틱과 70틱으로 번갈아 나오므로 틱 수는 CritZoneModel(명세 7.3·7.4의
        // 의사코드를 그대로 옮긴 독립 모형)과 대조한다. 명세만으로 정해지는 것: 주기당 4발이 전부 임계, 피해 4 × 20 = 80.
        private const int CritCycleShots = 4;
        private const int CritCycleDamage = 80;                   // 4 × (10 × 2)

        private const uint BotTicks = 7200;                       // 120초

        public static IReadOnlyList<SelfCheckResult> RunAll(ContentDb db)
        {
            var results = new List<SelfCheckResult>();
            Check(results, "bot.hold_fire_cycle__spec01_7_5", () => HoldCycle(db));
            Check(results, "bot.crit_zone_cycle__gdd18", () => CritCycle(db));
            Check(results, "bot.ratio_is_rule_ratio_and_passes", () => RatioMatchesRule(db));
            Check(results, "bot.other_loadouts_run", () => OtherLoadoutsRun(db));
            Check(results, "bot.defect__crit_multiplier_one_fails_target", () => DefectCritMultiplier(db));
            Check(results, "judge.fire_ratio_boundary", FireRatioBoundary);
            Check(results, "judge.axes_boundary_and_no_data", AxesBoundary);
            Check(results, "analyzer.two_press_counts", TwoPressCounts);
            Check(results, "analyzer.just_window_boundary__spec03_17", JustBoundary);
            Check(results, "analyzer.practice_cutoff", PracticeCutoff);
            Check(results, "analyzer.attempt_window_and_resets", AttemptWindowAndResets);
            Check(results, "analyzer.buffered_and_ignored_k", BufferedAndIgnored);
            Check(results, "analyzer.ignored_k_retry_is_same_attempt", IgnoredKRetry);
            Check(results, "analyzer.misfire_ratio_boundary", MisfireBoundary);
            Check(results, "e2e.scripted_play_through_wzrc", () => EndToEnd(db));
            Check(results, "e2e.ignored_k_retry_with_real_parser", () => IgnoredKRetryEndToEnd(db));
            Check(results, "e2e.tampered_record_is_rejected", () => TamperedRecord(db));
            Check(results, "e2e.record_from_another_world_is_rejected", () => OtherWorldRecord(db));
            return results;
        }

        // 각본으로 만든 시험용 연습 기록(.wzrc)의 바이트. 명령 사용 예를 보여 주려는 것이고 사람이 플레이한 기록이 아니다
        public static byte[] SampleRecord(ContentDb db)
        {
            KeyScript script = BuildScript(out uint endTick);
            return Record(db, script, endTick);
        }

        private static void Check(List<SelfCheckResult> results, string name, Func<string?> body)
        {
            try
            {
                string? failure = body();
                results.Add(new SelfCheckResult(name, failure == null, failure ?? string.Empty));
            }
            catch (Exception e)
            {
                results.Add(new SelfCheckResult(name, false, "threw " + e.GetType().Name + ": " + e.Message));
            }
        }

        // ── 봇

        private static string? HoldCycle(ContentDb db)
        {
            BotRun b = FireRatioMeter.Run(db, BotKind.HoldFire, BotTicks, BlockId.MagicBolt, false);
            return CycleShape(b, HoldCycleTicks, HoldCycleShots, HoldCycleCritShots, HoldCycleDamage);
        }

        private static string? CritCycle(ContentDb db)
        {
            BotRun b = FireRatioMeter.Run(db, BotKind.CritZone, BotTicks, BlockId.MagicBolt, false);
            if (b.GrazeEvents != 0)
            {
                return b.GrazeEvents + " graze rewards disturbed the heat cycle";
            }
            if (b.Cycles < 2)
            {
                return "fewer than two full cycles (cycles=" + b.Cycles + ")";
            }
            if (b.WindowShots != b.Cycles * CritCycleShots || b.WindowCritShots != b.WindowShots)
            {
                return "shots " + b.WindowShots + " crit " + b.WindowCritShots + " for " + b.Cycles + " cycles, want " + CritCycleShots + " crit shots per cycle";
            }
            if (b.WindowDamageRaw != (long)b.Cycles * CritCycleDamage * Fix64.OneRaw)
            {
                return "damage raw " + b.WindowDamageRaw + " for " + b.Cycles + " cycles, want " + CritCycleDamage + " per cycle";
            }
            CritZoneModel.Result m = Model(db);
            if (m.Cycles != b.Cycles || m.WindowTicks != b.WindowTicks)
            {
                return "world " + b.Cycles + " cycles / " + b.WindowTicks + " ticks, spec model " + m.Cycles + " cycles / " + m.WindowTicks + " ticks";
            }
            return null;
        }

        private static CritZoneModel.Result Model(ContentDb db)
        {
            CharacterDef c = db.Character(CharacterId.Baseline);
            BlockDef bolt = db.Block(BlockId.MagicBolt);
            return CritZoneModel.Run(BotTicks, FeelTargets.SettleTicks, bolt.HeatHu, bolt.IntervalTicks, c.CoolDelayTicks, c.CoolHuPerTick,
                                     c.CritStartHu, HeatRules.MaxHeatHu, FeelTargets.CritMulPermille);
        }

        private static string? CycleShape(BotRun b, int cycleTicks, int shots, int critShots, int damage)
        {
            if (b.GrazeEvents != 0)
            {
                return b.GrazeEvents + " graze rewards disturbed the heat cycle (the bot must stand outside the turret's range)";
            }
            if (b.Cycles < 2)
            {
                return "fewer than two full cycles in " + b.Ticks + " ticks (cycles=" + b.Cycles + ")";
            }
            long wantDamageRaw = (long)b.Cycles * damage * Fix64.OneRaw;
            if (b.WindowTicks != (long)b.Cycles * cycleTicks)
            {
                return "window " + b.WindowTicks + " ticks for " + b.Cycles + " cycles, want " + cycleTicks + " per cycle (shots " + b.WindowShots
                       + ", crit " + b.WindowCritShots + ", damage raw " + b.WindowDamageRaw + ")";
            }
            if (b.WindowShots != b.Cycles * shots || b.WindowCritShots != b.Cycles * critShots)
            {
                return "shots " + b.WindowShots + " crit " + b.WindowCritShots + " for " + b.Cycles + " cycles, want " + shots + " / " + critShots + " per cycle";
            }
            if (b.WindowDamageRaw != wantDamageRaw)
            {
                return "damage raw " + b.WindowDamageRaw + ", want " + wantDamageRaw;
            }
            return null;
        }

        // 비 = (임계 구간 피해 ÷ 틱) ÷ (꾹 누르기 310 ÷ 414). 임계 구간 쪽은 명세 모형의 값, 꾹 누르기 쪽은 spec 01 7.5의 손 계산이다
        private static string? RatioMatchesRule(ContentDb db)
        {
            FireRatioReport r = FireRatioMeter.Measure(db, BotTicks, BlockId.MagicBolt, false);
            if (!r.Measurable)
            {
                return "not measurable";
            }
            CritZoneModel.Result m = Model(db);
            // 임계 구간 피해 = 모형의 기본 피해 단위(1/1000) × 기본 피해. 꾹 누르기는 310 ÷ 414. 천분율은 분자의 1000 과 약분된다
            long baseDamage = db.Block(BlockId.MagicBolt).BaseDamage.RoundToInt();
            long want = m.WindowDamageMilli * baseDamage * HoldCycleTicks / (m.WindowTicks * HoldCycleDamage);
            if (r.RatioPermille != want)
            {
                return "ratio " + r.RatioPermille + "/1000, spec model " + want;
            }
            return r.Verdict == Verdict.Pass ? null : "verdict " + VerdictNames.Of(r.Verdict) + " for ratio " + r.RatioPermille;
        }

        private static string? OtherLoadoutsRun(ContentDb db)
        {
            foreach ((BlockId core, bool split) in new[] { (BlockId.Fireball, false), (BlockId.MagicBolt, true) })
            {
                FireRatioReport r = FireRatioMeter.Measure(db, BotTicks, core, split);
                if (r.Hold.TotalShots <= 0 || r.Crit.TotalShots <= 0)
                {
                    return core + (split ? "+split" : string.Empty) + ": a bot never fired";
                }
            }
            return null;
        }

        // 일부러 틀린 값: 임계 배율이 ×1이면 임계 구간 플레이의 이득이 사라져 1.4 목표를 못 넘어야 한다
        private static string? DefectCritMultiplier(ContentDb db)
        {
            BotRun hold = FireRatioMeter.Run(db, BotKind.HoldFire, BotTicks, BlockId.MagicBolt, false, 1000);
            BotRun crit = FireRatioMeter.Run(db, BotKind.CritZone, BotTicks, BlockId.MagicBolt, false, 1000);
            var r = new FireRatioReport(hold, crit, BlockId.MagicBolt, false);
            return r.Verdict == Verdict.Fail ? null : "a ×1 crit multiplier still passed (ratio " + r.RatioPermille + "/1000)";
        }

        // ── 판정

        private static string? FireRatioBoundary()
        {
            // 같은 창(1,000틱)에서 꾹 누르기 피해 1,000, 임계 구간 1,400 → 정확히 1.4배(통과). 1,399 는 실패
            if (RatioVerdict(1400) != Verdict.Pass)
            {
                return "exactly 1.4x must pass";
            }
            if (RatioVerdict(1399) != Verdict.Fail)
            {
                return "1.399x must fail";
            }
            var none = new FireRatioReport(new BotRun(), new BotRun(), BlockId.MagicBolt, false);
            return none.Verdict == Verdict.NoData ? null : "no cycles must be no_data";
        }

        private static Verdict RatioVerdict(long critDamage)
        {
            var hold = new BotRun { Cycles = 1, WindowStartTick = 0, WindowEndTick = 1000, WindowDamageRaw = 1000 };
            var crit = new BotRun { Cycles = 1, WindowStartTick = 0, WindowEndTick = 1000, WindowDamageRaw = critDamage };
            return new FireRatioReport(hold, crit, BlockId.MagicBolt, false).Verdict;
        }

        private static string? AxesBoundary()
        {
            // 두 번 누르는 커맨드: 9/10 통과, 8/10 실패(정확히 90%는 통과)
            if (Judge(TwoPressResult(9, 10), FeelJudge.TwoPress) != Verdict.Pass)
            {
                return "9/10 must pass two_press";
            }
            if (Judge(TwoPressResult(8, 10), FeelJudge.TwoPress) != Verdict.Fail)
            {
                return "8/10 must fail two_press";
            }
            if (Judge(new FeelResult(), FeelJudge.TwoPress) != Verdict.NoData)
            {
                return "no attempts must be no_data";
            }
            // 섬: 3/10 통과(정확히 30%), 2/10 실패
            if (Judge(JustResult(3, 10), FeelJudge.Just) != Verdict.Pass || Judge(JustResult(2, 10), FeelJudge.Just) != Verdict.Fail)
            {
                return "just boundary at exactly 30%";
            }
            if (Judge(new FeelResult(), FeelJudge.Just) != Verdict.NoData)
            {
                return "no just attempts must be no_data";
            }
            // 오발: 10% 미만이어야 통과 → 1/10 은 실패, 1/11 은 통과
            if (Judge(MisfireResult(1, 10), FeelJudge.Misfire) != Verdict.Fail || Judge(MisfireResult(1, 11), FeelJudge.Misfire) != Verdict.Pass)
            {
                return "misfire must be strictly below 10%";
            }
            if (Judge(new FeelResult(), FeelJudge.Misfire) != Verdict.NoData)
            {
                return "no moving casts must be no_data";
            }
            if (VerdictNames.Overall(Verdict.Pass, Verdict.NoData) != "inconclusive" || VerdictNames.Overall(Verdict.NoData, Verdict.Fail) != "fail"
                || VerdictNames.Overall(Verdict.Pass, Verdict.Pass) != "pass")
            {
                return "overall verdict rule";
            }
            return null;
        }

        private static Verdict Judge(FeelResult r, Func<FeelResult, Verdict> axis) => axis(r);

        private static FeelResult TwoPressResult(int successes, int attempts)
        {
            var r = new FeelResult();
            for (int i = 0; i < attempts; i++)
            {
                r.Attempts.Add(new AttemptRecord { Outcome = i < successes ? ArtId.Spear : ArtId.Burst });
            }
            return r;
        }

        private static FeelResult JustResult(int successes, int attempts) => new FeelResult { JustAttempts = attempts, JustSuccesses = successes };

        private static FeelResult MisfireResult(int misfires, int casts) => new FeelResult { MisfireCasts = casts, Misfires = misfires };

        // ── 분석기(손으로 짠 이벤트 열)

        private static SimEvent Ev(SimEventType type, uint tick, int a = 0, int b = 0, int c = 0, long v = 0) =>
            new SimEvent { Type = type, Tick = tick, A = a, B = b, C = c, V = v };

        // 한 번의 K: 앞, 앞(첫째는 tick, 둘째는 tick + 6) → tick + 10 에 K, 간격 gapUnits, 나간 기술 art
        private static void Attempt(List<SimEvent> e, uint tick, long gapUnits, ArtId art)
        {
            e.Add(Ev(SimEventType.CmdDirInput, tick, (int)CmdDir.Forward));
            e.Add(Ev(SimEventType.CmdDirInput, tick + 6, (int)CmdDir.Forward, 1));
            e.Add(Ev(SimEventType.PracticeGapReport, tick + 10, (int)gapUnits, (int)art, 1, gapUnits));
            e.Add(Ev(SimEventType.ArtCast, tick + 10, (int)art));
        }

        private static FeelResult Analyze(List<SimEvent> events, long practiceMinutes = 0)
        {
            var options = new FeelOptions { PracticeMinutes = practiceMinutes };
            return FeelAnalyzer.Analyze(events, new List<PlayerInput>(), 100000, options);
        }

        private static List<SimEvent> InStance() => new List<SimEvent> { Ev(SimEventType.StanceEntered, 1, 1) };

        private static string? TwoPressCounts()
        {
            List<SimEvent> e = InStance();
            for (uint i = 0; i < 10; i++)
            {
                Attempt(e, 10 + i * 100, 2000, i == 4 ? ArtId.Burst : ArtId.Spear);
            }
            FeelResult r = Analyze(e);
            if (r.TwoPressAttempts != 10 || r.TwoPressSuccesses != 9)
            {
                return "attempts " + r.TwoPressAttempts + " successes " + r.TwoPressSuccesses + ", want 10 / 9";
            }
            return FeelJudge.TwoPress(r) == Verdict.Pass ? null : "9/10 must pass";
        }

        private static string? JustBoundary()
        {
            // spec 03 17절 경계 1: 간격이 한도와 정확히 같으면 통과(0.07초 = 1,075단위는 섬). 1,076 은 아니다. 0(방향과 K 가 같은 오프셋)도 섬이다
            List<SimEvent> e = InStance();
            Attempt(e, 10, 1075, ArtId.Spear);
            Attempt(e, 110, 1076, ArtId.Spear);
            Attempt(e, 210, 0, ArtId.Spear);
            Attempt(e, 310, 500, ArtId.Burst);                   // 섬 간격이어도 꿰뚫는 창이 안 나갔으면 섬이 아니다
            FeelResult r = Analyze(e);
            if (FeelTargets.JustWindowUnits != 1075)
            {
                return "just window is " + FeelTargets.JustWindowUnits + " units, spec 03 1.2 says 1,075";
            }
            return r.JustAttempts == 4 && r.JustSuccesses == 2 ? null : "just " + r.JustSuccesses + "/" + r.JustAttempts + ", want 2/4";
        }

        private static string? PracticeCutoff()
        {
            List<SimEvent> e = InStance();
            Attempt(e, 100, 100, ArtId.Spear);                   // 연습 시간 안
            Attempt(e, 3600 + 100, 100, ArtId.Spear);            // 정확히 1분 뒤(틱 3,700 ≥ 3,600)
            FeelResult r = Analyze(e, 1);
            if (r.TwoPressAttempts != 2 || r.JustAttempts != 1 || r.JustSuccesses != 1)
            {
                return "attempts " + r.TwoPressAttempts + " just " + r.JustSuccesses + "/" + r.JustAttempts + ", want 2 and 1/1";
            }
            FeelResult none = Analyze(e, 2);
            return none.JustAttempts == 0 && FeelJudge.Just(none) == Verdict.NoData ? null : "a record shorter than the practice time must be no_data";
        }

        private static string? AttemptWindowAndResets()
        {
            // 첫째 앞이 K 보다 90틱 앞(창 60틱 밖) → 시도가 아니다
            var e = InStance();
            e.Add(Ev(SimEventType.CmdDirInput, 10, (int)CmdDir.Forward));
            e.Add(Ev(SimEventType.CmdDirInput, 100, (int)CmdDir.Forward));
            e.Add(Ev(SimEventType.PracticeGapReport, 101, 256, (int)ArtId.Burst, 1, 256));
            e.Add(Ev(SimEventType.ArtCast, 101, (int)ArtId.Burst));
            // 반전이 커맨드를 처음으로 돌린다: 앞, (반전), 앞, K 는 시도가 아니다
            e.Add(Ev(SimEventType.CmdDirInput, 200, (int)CmdDir.Forward));
            e.Add(Ev(SimEventType.ReverseFlipped, 202, 2));
            e.Add(Ev(SimEventType.CmdDirInput, 204, (int)CmdDir.Forward));
            e.Add(Ev(SimEventType.PracticeGapReport, 206, 512, (int)ArtId.Burst, 1, 512));
            e.Add(Ev(SimEventType.ArtCast, 206, (int)ArtId.Burst));
            // 자세 밖(StanceExited 뒤)의 K 는 어느 축에도 들어가지 않는다
            e.Add(Ev(SimEventType.StanceExited, 300));
            e.Add(Ev(SimEventType.PracticeGapReport, 310, -1, (int)ArtId.Burst, 1, -1));
            e.Add(Ev(SimEventType.ArtCast, 310, (int)ArtId.Burst));
            FeelResult r = Analyze(e);
            if (r.TwoPressAttempts != 0)
            {
                return "attempts " + r.TwoPressAttempts + ", want 0";
            }
            return r.KPresses == 3 && r.MisfireCasts == 2 ? null : "k presses " + r.KPresses + " moving casts " + r.MisfireCasts + ", want 3 and 2";
        }

        private static string? BufferedAndIgnored()
        {
            // 재사용 대기 끝에 나갈 K 를 보관했다가 나가는 경우: 시도의 결과는 보관된 K 의 발동이 정한다
            var e = InStance();
            e.Add(Ev(SimEventType.CmdDirInput, 10, (int)CmdDir.Forward));
            e.Add(Ev(SimEventType.CmdDirInput, 14, (int)CmdDir.Forward, 1));
            e.Add(Ev(SimEventType.ArtKBuffered, 16, 5000, v: 5000));
            e.Add(Ev(SimEventType.PracticeGapReport, 16, 600, 0, 0, 600));
            e.Add(Ev(SimEventType.ArtCast, 20, (int)ArtId.Spear));         // 보관된 K 의 발동(그 틱에 판정한 K 가 없다)
            // 재사용 대기 중이고 보관 창 밖이라 무시된 K
            e.Add(Ev(SimEventType.CmdDirInput, 100, (int)CmdDir.Forward));
            e.Add(Ev(SimEventType.CmdDirInput, 104, (int)CmdDir.Forward, 1));
            e.Add(Ev(SimEventType.PracticeGapReport, 106, 600, 0, 0, 600));
            FeelResult r = Analyze(e);
            if (r.TwoPressAttempts != 2 || r.TwoPressSuccesses != 1)
            {
                return "attempts " + r.TwoPressAttempts + " successes " + r.TwoPressSuccesses + ", want 2 / 1";
            }
            return r.Attempts[0].Success && r.Attempts[1].ResultName == "ignored_cooldown" ? null : "buffered/ignored attribution";
        }

        // 재사용 대기에 무시된 K 는 판정기의 커서와 방향 기록을 건드리지 않는다(spec 03 10절, CommandParser U-02·U-04).
        // 앞, 앞, K(무시), K(다시)이면 다시 누른 K 가 같은 앞, 앞으로 꿰뚫는 창을 낸다. 시도는 하나이고 오발이 아니다
        private static string? IgnoredKRetry()
        {
            // 틱 0 의 발동으로 대기는 틱 24 까지, 보관 창은 틱 18 부터다. 앞 5, 앞 10, K 14 는 보관 창 밖이라 무시된다. K 26 은 대기가 끝난 뒤다
            var e = InStance();
            e.Add(Ev(SimEventType.CmdDirInput, 5, (int)CmdDir.Forward));
            e.Add(Ev(SimEventType.CmdDirInput, 10, (int)CmdDir.Forward, 1));
            e.Add(Ev(SimEventType.PracticeGapReport, 14, 1024, 0, 0, 1024));          // 무시: 발동 없음, ArtKBuffered 없음
            e.Add(Ev(SimEventType.PracticeGapReport, 26, 4096, (int)ArtId.Spear, 1, 4096));
            e.Add(Ev(SimEventType.ArtCast, 26, (int)ArtId.Spear));
            FeelResult r = Analyze(e);
            if (r.KPresses != 2 || r.TwoPressAttempts != 1 || r.TwoPressSuccesses != 1)
            {
                return "retry: k " + r.KPresses + " attempts " + r.TwoPressAttempts + " successes " + r.TwoPressSuccesses + ", want 2 / 1 / 1";
            }
            if (r.MisfireCasts != 0 || r.Misfires != 0 || r.MisfireSpear != 0 || r.IgnoredOrUnresolvedMoving != 0)
            {
                return "the retried spear was counted as a misfire (casts " + r.MisfireCasts + ", misfires " + r.Misfires + ", spear " + r.MisfireSpear + ")";
            }

            // 다시 누르지 않으면 무시된 K 가 실패한 시도로 남는다
            var alone = InStance();
            alone.Add(Ev(SimEventType.CmdDirInput, 5, (int)CmdDir.Forward));
            alone.Add(Ev(SimEventType.CmdDirInput, 10, (int)CmdDir.Forward, 1));
            alone.Add(Ev(SimEventType.PracticeGapReport, 14, 1024, 0, 0, 1024));
            FeelResult a = Analyze(alone);
            if (a.TwoPressAttempts != 1 || a.TwoPressSuccesses != 0 || a.Attempts[0].ResultName != "ignored_cooldown")
            {
                return "no retry: attempts " + a.TwoPressAttempts + " successes " + a.TwoPressSuccesses + ", want 1 / 0 ignored_cooldown";
            }

            // 무시된 K 뒤에 앞을 더 누르면 앞, 앞의 첫째 앞이 바뀐 다른 시도다: 무시된 쪽은 실패한 시도로 남는다
            var other = InStance();
            other.Add(Ev(SimEventType.CmdDirInput, 5, (int)CmdDir.Forward));
            other.Add(Ev(SimEventType.CmdDirInput, 10, (int)CmdDir.Forward, 1));
            other.Add(Ev(SimEventType.PracticeGapReport, 14, 1024, 0, 0, 1024));
            other.Add(Ev(SimEventType.CmdDirInput, 18, (int)CmdDir.Forward, 1));
            other.Add(Ev(SimEventType.PracticeGapReport, 26, 2048, (int)ArtId.Spear, 1, 2048));
            other.Add(Ev(SimEventType.ArtCast, 26, (int)ArtId.Spear));
            FeelResult o = Analyze(other);
            if (o.TwoPressAttempts != 2 || o.TwoPressSuccesses != 1)
            {
                return "new directions after an ignored K: attempts " + o.TwoPressAttempts + " successes " + o.TwoPressSuccesses + ", want 2 / 1";
            }

            // 초기화(자세 반전 등)가 끼면 방향 기록이 비워진 것이라 다시 누른 K 는 같은 시도가 아니다
            var reset = InStance();
            reset.Add(Ev(SimEventType.CmdDirInput, 5, (int)CmdDir.Forward));
            reset.Add(Ev(SimEventType.CmdDirInput, 10, (int)CmdDir.Forward, 1));
            reset.Add(Ev(SimEventType.PracticeGapReport, 14, 1024, 0, 0, 1024));
            reset.Add(Ev(SimEventType.CommandReset, 15));
            reset.Add(Ev(SimEventType.PracticeGapReport, 26, 4096, (int)ArtId.Burst, 1, 4096));
            reset.Add(Ev(SimEventType.ArtCast, 26, (int)ArtId.Burst));
            FeelResult s = Analyze(reset);
            return s.TwoPressAttempts == 1 && s.TwoPressSuccesses == 0 && s.Attempts[0].ResultName == "ignored_cooldown"
                ? null
                : "reset after an ignored K: attempts " + s.TwoPressAttempts + ", want 1 (the ignored one)";
        }

        private static string? MisfireBoundary()
        {
            // 이동 중 자세 K 11번 가운데 반동 사격 1번(1/11 = 9.09% < 10%): 통과. 10번 가운데 1번(10%): 실패
            FeelResult pass = Analyze(MovingKs(11, 1));
            FeelResult fail = Analyze(MovingKs(10, 1));
            if (pass.MisfireCasts != 11 || pass.Misfires != 1 || pass.MisfireRecoil != 1)
            {
                return "11 casts: " + pass.Misfires + "/" + pass.MisfireCasts;
            }
            if (FeelJudge.Misfire(pass) != Verdict.Pass || FeelJudge.Misfire(fail) != Verdict.Fail)
            {
                return "1/11 must pass and 1/10 must fail";
            }
            return null;
        }

        // 자세 중 마지막 방향 0.1초 뒤에 누른 K 가 count 번. 그 가운데 recoil 번은 반동 사격(뒤, K)
        private static List<SimEvent> MovingKs(int count, int recoil)
        {
            List<SimEvent> e = InStance();
            for (int i = 0; i < count; i++)
            {
                uint t = (uint)(10 + i * 100);
                bool back = i < recoil;
                e.Add(Ev(SimEventType.CmdDirInput, t, (int)(back ? CmdDir.Back : CmdDir.Left)));
                ArtId art = back ? ArtId.Recoil : ArtId.Burst;
                e.Add(Ev(SimEventType.PracticeGapReport, t + 6, 1536, (int)art, 1, 1536));
                e.Add(Ev(SimEventType.ArtCast, t + 6, (int)art));
            }
            return e;
        }

        // ── 종단: 각본 → 연습 세계 → .wzrc → 읽기 → 재실행 → 분석 → JSON

        private const uint AttemptSpacingTicks = 70;                 // 재사용 대기 0.4초(24틱)보다 길다
        private const uint AttemptCount = 12;

        private static KeyScript BuildScript(out uint endTick)
        {
            var s = new KeyScript();
            s.Down(2, LogicalKey.Stance);

            // 첫째 앞과 둘째 앞의 간격(틱), 둘째 앞에서 K 까지 틱·오프셋. 자세 중 아래를 보므로 앞 = 아래 키, 뒤 = 위 키(spec 01 5.5)
            (uint firstGap, uint kTicks, byte kOffset)[] attempts =
            {
                (8, 0, 80), (8, 0, 80), (8, 0, 80),
                (8, 4, 51),        // 4 × 256 + 51 = 1,075 단위: 정확히 한도, 섬
                (8, 4, 52),        // 1,076 단위: 한도 밖, 꿰뚫는 창은 나가지만 섬이 아니다
                (8, 6, 0),         // 1,536 단위(0.1초)
                (8, 0, 80),
                (8, 24, 0),        // 마지막 앞에서 0.4초 뒤의 K: 0.3초를 넘어 커맨드가 끊기고 마력 방출이 나간다
                (8, 6, 0),
                (8, 0, 80),
                (30, 2, 0),        // 앞과 앞 사이가 0.5초: 첫째 앞이 잊히고 마력 방출이 나간다
                (8, 0, 80),
            };
            for (uint i = 0; i < AttemptCount; i++)
            {
                uint a = 10 + i * AttemptSpacingTicks;
                (uint firstGap, uint kTicks, byte kOffset) = attempts[i];
                s.Down(a, LogicalKey.Down);
                s.Up(a + 2, LogicalKey.Down);
                s.Down(a + firstGap, LogicalKey.Down);
                s.Up(a + firstGap + 2, LogicalKey.Down);
                s.Down(a + firstGap + kTicks, LogicalKey.Skill, kOffset);
                s.Up(a + firstGap + kTicks + 2, LogicalKey.Skill);
            }

            // 이동하다 누른 K: 옆으로 한 번 누르고 K 5번(마력 방출), 뒤로 한 번 누르고 K(반동 사격), 방향 없이 K(분모에 안 든다),
            // 왼쪽 키를 누른 채 0.5초 뒤의 K(방향 키가 눌려 있다)
            uint m = 10 + AttemptCount * AttemptSpacingTicks + 20;
            for (uint j = 0; j < 5; j++)
            {
                uint t = m + 40 * j;
                s.Down(t, LogicalKey.Left);
                s.Up(t + 2, LogicalKey.Left);
                s.Down(t + 4, LogicalKey.Skill);
                s.Up(t + 6, LogicalKey.Skill);
            }
            uint back = m + 40 * 5;
            s.Down(back, LogicalKey.Up);
            s.Up(back + 2, LogicalKey.Up);
            s.Down(back + 4, LogicalKey.Skill);
            s.Up(back + 6, LogicalKey.Skill);
            uint bare = m + 40 * 6;
            s.Down(bare, LogicalKey.Skill);
            s.Up(bare + 2, LogicalKey.Skill);
            uint held = m + 40 * 7;
            s.Down(held, LogicalKey.Left);
            s.Down(held + 30, LogicalKey.Skill);
            s.Up(held + 32, LogicalKey.Skill);
            s.Up(held + 40, LogicalKey.Left);
            endTick = m + 40 * 8 + 10;
            return s;
        }

        private static InputRecordHeader Header(ContentDb db) => new InputRecordHeader
        {
            FormatVersion = EndlessSetup.CanonicalFormatVersion,
            Setup = new EndlessSetup
            {
                Seed = 0,
                Kind = RunKind.Normal,
                StartWave = 1,
                Wand = new WandSnapshot { Character = CharacterId.Baseline },
                CoreVersion = SimVersion.CoreVersion,
                DataVersion = db.DataVersion,
            },
        };

        private static byte[] Record(ContentDb db, KeyScript script, uint endTick) =>
            Record(db, script, endTick, PracticeRig.NewWorld(db));

        // world 에 각본을 넣으며 기록한다(연습 세계가 아닌 세계로 기록해 재실행 해시 대조를 시험할 수도 있다)
        private static byte[] Record(ContentDb db, KeyScript script, uint endTick, SimWorld world)
        {
            var recorder = new InputRecorder(Header(db));
            var sink = new EventLog();
            var byTick = new Dictionary<uint, List<QuantizedKey>>();
            foreach (KeyValuePair<uint, List<QuantizedKey>> kv in script.Ticks)
            {
                byTick[kv.Key] = kv.Value;
            }
            var commandsOut = new RunCommand[1];
            var inputsOut = new PlayerInput[InputOrder.MaxEventsPerTickPerPlayer];
            for (uint t = 0; t < endTick; t++)
            {
                int inputCount = 0;
                if (byTick.TryGetValue(t, out List<QuantizedKey>? keys))
                {
                    recorder.Append(t, PracticeRig.Slot0, ReadOnlySpan<RunCommandDraft>.Empty, keys.ToArray(), commandsOut, out _, inputsOut, out inputCount);
                }
                world.Step(new ReadOnlySpan<PlayerInput>(inputsOut, 0, inputCount), ReadOnlySpan<RunCommand>.Empty, sink);
            }
            return recorder.Finish(endTick, world.ComputeStateHash()).ToArray();
        }

        private static string? EndToEnd(ContentDb db)
        {
            KeyScript script = BuildScript(out uint endTick);
            byte[] file = Record(db, script, endTick);
            var options = new FeelOptions { PracticeMinutes = 0 };
            if (!FeelRecordAnalysis.TryAnalyze(db, file, "selfcheck.wzrc", options, false, out FeelRecordInfo info, out FeelResult r, out string error))
            {
                return "analysis failed: " + error;
            }
            if (!info.HashMatches || info.RejectedCommands != 0 || info.EndTick != endTick)
            {
                return "replay: hash " + info.HashMatches + " rejected " + info.RejectedCommands + " end " + info.EndTick + " want " + endTick;
            }
            // 기대값은 각본과 명세에서 나온다(위 BuildScript 의 주석): 시도 12, 꿰뚫는 창 10(느린 둘은 마력 방출), 섬 7(1,075 포함, 1,076 제외)
            if (r.TwoPressAttempts != 12 || r.TwoPressSuccesses != 10)
            {
                return "two-press " + r.TwoPressSuccesses + "/" + r.TwoPressAttempts + ", want 10/12";
            }
            if (r.JustAttempts != 12 || r.JustSuccesses != 7)
            {
                return "just " + r.JustSuccesses + "/" + r.JustAttempts + ", want 7/12";
            }
            if (r.MisfireCasts != 7 || r.Misfires != 1 || r.MisfireRecoil != 1 || r.KPresses != 20)
            {
                return "misfire " + r.Misfires + "/" + r.MisfireCasts + " (recoil " + r.MisfireRecoil + ", k presses " + r.KPresses + "), want 1/7, 1, 20";
            }
            if (FeelJudge.TwoPress(r) != Verdict.Fail || FeelJudge.Just(r) != Verdict.Pass || FeelJudge.Misfire(r) != Verdict.Fail)
            {
                return "verdicts " + VerdictNames.Of(FeelJudge.TwoPress(r)) + "/" + VerdictNames.Of(FeelJudge.Just(r)) + "/" + VerdictNames.Of(FeelJudge.Misfire(r));
            }
            // 연습 시간이 기록보다 길면 섬은 자료 없음이다
            if (!FeelRecordAnalysis.TryAnalyze(db, file, "selfcheck.wzrc", new FeelOptions { PracticeMinutes = 10 }, false, out _, out FeelResult shortRun, out error)
                || FeelJudge.Just(shortRun) != Verdict.NoData)
            {
                return "a 10 minute practice time over a short record must give no_data for just " + error;
            }
            string json = FeelReportWriter.Build(null, info, r, options, true);
            string again = FeelReportWriter.Build(null, info, r, options, true);
            if (json != again)
            {
                return "the report is not deterministic";
            }
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            JsonElement two = root.GetProperty("axes").GetProperty("two_press_command");
            if (root.GetProperty("verdict").GetString() != "fail" || two.GetProperty("attempts").GetInt32() != 12
                || two.GetProperty("successes").GetInt32() != 10 || two.GetProperty("measured_rate_permille").GetInt64() != 833
                || two.GetProperty("target").GetProperty("permille").GetInt32() != FeelTargets.TwoPressMinPermille
                || two.GetProperty("attempt_list").GetArrayLength() != 12
                || root.GetProperty("axes").GetProperty("just").GetProperty("k_gap_distribution").GetProperty("count").GetInt32() != 12)
            {
                return "report fields do not match the counts";
            }
            return null;
        }

        // 실제 판정기로 보는 무시된 K 의 다시 누르기: 앞, 앞, K 로 발동한 직후 대기 안에서(보관 창 밖) 앞, 앞, K 를 누르면 무시되고,
        // 대기가 끝난 뒤 K 만 다시 누르면 같은 앞, 앞으로 꿰뚫는 창이 나간다(spec 03 10절). 시도는 둘(첫 발동, 다시 누른 K)이고 오발은 없다
        private static string? IgnoredKRetryEndToEnd(ContentDb db)
        {
            var s = new KeyScript();
            s.Down(2, LogicalKey.Stance);
            s.Down(10, LogicalKey.Down);
            s.Up(12, LogicalKey.Down);
            s.Down(18, LogicalKey.Down);
            s.Up(20, LogicalKey.Down);
            s.Down(18, LogicalKey.Skill, 80);                     // 첫 발동: 대기는 틱 42(오프셋 80)까지
            s.Up(20, LogicalKey.Skill);
            s.Down(26, LogicalKey.Down);                          // 대기 중의 앞, 앞, K: 보관 창(대기 끝 앞 1,536단위 안)보다 이르다
            s.Up(27, LogicalKey.Down);
            s.Down(28, LogicalKey.Down);
            s.Up(29, LogicalKey.Down);
            s.Down(31, LogicalKey.Skill);
            s.Up(32, LogicalKey.Skill);
            s.Down(43, LogicalKey.Skill);                         // 대기 뒤 다시 누름: 마지막 앞에서 15틱 = 3,840단위(0.3초 = 4,608 안)
            s.Up(44, LogicalKey.Skill);
            const uint endTick = 60;
            byte[] file = Record(db, s, endTick);
            if (!FeelRecordAnalysis.TryAnalyze(db, file, "retry.wzrc", new FeelOptions { PracticeMinutes = 0 }, false, out FeelRecordInfo info, out FeelResult r, out string error))
            {
                return "analysis failed: " + error;
            }
            if (!info.HashMatches)
            {
                return "replay hash mismatch";
            }
            if (r.KPresses != 3 || r.TwoPressAttempts != 2 || r.TwoPressSuccesses != 2)
            {
                return "k " + r.KPresses + " attempts " + r.TwoPressAttempts + " successes " + r.TwoPressSuccesses + ", want 3 / 2 / 2 (the ignored K belongs to the retried attempt)";
            }
            return r.MisfireCasts == 0 && r.Misfires == 0 ? null : "the retried spear was a misfire (" + r.Misfires + "/" + r.MisfireCasts + ")";
        }

        // 무한 웨이브 세계에서 적은 기록은 연습 세계에서 다시 돌려도 같은 해시가 나오지 않는다: 손맛 계측은 그것을 받지 않는다.
        // --skip-hash-check 를 주면 받는다(그때 JSON 의 replay_hash_matches 는 false 로 남는다)
        private static string? OtherWorldRecord(ContentDb db)
        {
            KeyScript script = BuildScript(out uint endTick);
            SimWorld endless = SimWorld.CreateEndless(db, SimConfig.ForEndless(db), Header(db).Setup);
            byte[] file = Record(db, script, endTick, endless);
            if (FeelRecordAnalysis.TryAnalyze(db, file, "endless.wzrc", new FeelOptions(), false, out _, out _, out string error))
            {
                return "a record from an endless world was accepted as a practice record";
            }
            if (!error.Contains("final_state_hash", StringComparison.Ordinal))
            {
                return "unexpected error text: " + error;
            }
            if (!FeelRecordAnalysis.TryAnalyze(db, file, "endless.wzrc", new FeelOptions(), true, out FeelRecordInfo info, out _, out error))
            {
                return "--skip-hash-check must analyze anyway: " + error;
            }
            return !info.HashMatches && info.HashCheckSkipped ? null : "skipped check must be visible in the record info";
        }

        private static string? TamperedRecord(ContentDb db)
        {
            KeyScript script = BuildScript(out uint endTick);
            byte[] file = Record(db, script, endTick);
            file[file.Length / 2] ^= 0x01;
            if (FeelRecordAnalysis.TryAnalyze(db, file, "tampered.wzrc", new FeelOptions(), false, out _, out _, out string error))
            {
                return "a record with a flipped bit was accepted";
            }
            return error.Length > 0 ? null : "no error text";
        }
    }
}
