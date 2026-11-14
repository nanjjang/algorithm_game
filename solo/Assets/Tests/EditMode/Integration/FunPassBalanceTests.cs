using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Tests;

namespace Wizard.Solo.Tests.Integration
{
    /// <summary>
    /// 재미 조정(Docs/11-fun-pass.md)의 측정 테스트. 명세 값(손잡이 꺼짐, TestContent.Db)과 조정 값(FunPass.json 켬, FunPassKit.TunedDb)을
    /// 같은 봇으로 재서 표를 남기고(테스트 출력), 조정이 노린 관계를 단언한다. 봇은 결정론적이라 같은 씨앗이면 같은 표가 나온다.
    /// </summary>
    [Category("Balance")]
    public sealed class FunPassBalanceTests
    {
        [Test]
        public void Fun_Load__AllPatchesApplyWithoutErrors()
        {
            var db = (ContentDb)FunPassKit.TunedDb;
            // 보충 조각 손잡이는 그 조각을 처음 읽을 때 적용된다 → 다 읽혀 둔다.
            db.SoloExtra("G01"); db.SoloExtra("G09"); db.SoloExtra("G10");
            Assert.IsTrue(db.TuningEnabled, "FunPass.json enabled");
            Assert.IsEmpty(db.LoadErrors, string.Join(" | ", db.LoadErrors));
            var file = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(
                System.IO.Path.Combine(TestContent.SoloExtraDir, TuningPatches.ExtraName + ".json")));
            var patches = TuningPatches.EnabledPatches(file);
            Assert.IsNotNull(patches);
            for (int i = 0; i < patches.Count; i++)
                CollectionAssert.Contains(db.AppliedTuning, (string)patches[i]["id"]);
            // 명세 쪽은 손잡이가 꺼져 있다.
            Assert.IsFalse(((ContentDb)TestContent.Db).TuningEnabled);
            Assert.AreEqual(0.2f, TestContent.Db.BaselineCharacter.CoolDelaySeconds, 1e-6f);
            Assert.AreEqual(0.08f, db.BaselineCharacter.CoolDelaySeconds, 1e-6f);
            Assert.AreEqual(2, TestContent.Db.Const.GrazeComboGain);
            Assert.AreEqual(5, db.Const.GrazeComboGain);
            Assert.IsNull(TestContent.Db.SoloExtra("G01")["graze_heat_relief"], "명세 쪽 G01에는 조정 열쇠가 없다");
        }

        // ------------------------------------------------------------ A. 과열 리듬

        [Test]
        public void Fun_A__TapRhythmBeatsHoldBy25To45Percent_HoldUnchanged()
        {
            for (int c = 1; c <= 5; c++)
            {
                var specHold = FunPassKit.MeasurePolicy(TestContent.Db, c, FunPassKit.HeatPolicy.Hold);
                var specTap = FunPassKit.MeasurePolicy(TestContent.Db, c, FunPassKit.HeatPolicy.Tap);
                var specBurst = FunPassKit.MeasurePolicy(TestContent.Db, c, FunPassKit.HeatPolicy.Burst);
                var hold = FunPassKit.MeasurePolicy(FunPassKit.TunedDb, c, FunPassKit.HeatPolicy.Hold);
                var tap = FunPassKit.MeasurePolicy(FunPassKit.TunedDb, c, FunPassKit.HeatPolicy.Tap);
                var burst = FunPassKit.MeasurePolicy(FunPassKit.TunedDb, c, FunPassKit.HeatPolicy.Burst);
                FunPassKit.Log(string.Format(
                    "char.{0}: spec hold {1:0.0} / tap {2:0.0} ({3:+0%;-0%}) / burst {4:0.0} ({5:+0%;-0%})  →  tuned hold {6:0.0} / tap {7:0.0} ({8:+0%;-0%}) / burst {9:0.0} ({10:+0%;-0%})",
                    c, specHold.Dps, specTap.Dps, specTap.Dps / specHold.Dps - 1f, specBurst.Dps, specBurst.Dps / specHold.Dps - 1f,
                    hold.Dps, tap.Dps, tap.Dps / hold.Dps - 1f, burst.Dps, burst.Dps / hold.Dps - 1f));

                // 초보(꾹 누르기)는 그대로 진행할 수 있다: 조정 전후가 같다.
                Assert.AreEqual(specHold.Dps, hold.Dps, specHold.Dps * 0.01f, "char." + c + " 꾹 누르기 DPS는 조정으로 바뀌지 않는다");
                // 명세에선 한 발씩 끊어 쏘기가 꾹 누르기와 비슷했다(±10%) — 조정이 고치려던 것.
                Assert.Less(specTap.Dps / specHold.Dps, 1.1f, "char." + c + " 명세의 끊어 쏘기");
                // 조정 뒤: 끊어 쏘기 +25~45%, 몰아 쏘기는 그보다 위.
                Assert.That(tap.Dps / hold.Dps, Is.InRange(1.25f, 1.45f), "char." + c + " 끊어 쏘기 ÷ 꾹 누르기");
                Assert.Greater(burst.Dps, tap.Dps, "char." + c + " 몰아 쏘기 > 끊어 쏘기");
                Assert.Less(tap.Locks, hold.Locks, "끊어 쏘기는 잠기지 않는다");
            }
        }

        [Test]
        public void Fun_A__PolicyMeasureIsDeterministic()
        {
            var a = FunPassKit.MeasurePolicy(FunPassKit.TunedDb, 1, FunPassKit.HeatPolicy.Burst, 1800);
            var b = FunPassKit.MeasurePolicy(FunPassKit.TunedDb, 1, FunPassKit.HeatPolicy.Burst, 1800);
            Assert.AreEqual(a.Dps, b.Dps);
            Assert.AreEqual(a.Shots, b.Shots);
            Assert.AreEqual(a.Crits, b.Crits);
        }

        // ------------------------------------------------------------ B·C·F·G. 판 한 벌

        static float AvgClear(List<FunPassKit.WaveRow> rows, int from, int to)
        {
            float sum = 0f; int n = 0;
            for (int i = 0; i < rows.Count; i++)
                if (rows[i].Wave >= from && rows[i].Wave <= to && !rows[i].Boss && rows[i].ClearTicks > 0) { sum += rows[i].ClearTicks / 60f; n++; }
            return n > 0 ? sum / n : 0f;
        }

        static FunPassKit.WaveRow Row(List<FunPassKit.WaveRow> rows, int wave)
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i].Wave == wave) return rows[i];
            return null;
        }

        [Test]
        public void Fun_Run__CurveReport_And_EarlyWavesHaveGrazeAndK_LaterWavesLonger()
        {
            var spec = FunPassKit.PlayRun(TestContent.Db, 1, 20261115UL, 30);
            FunPassKit.Log("== spec(명세 수치 + G09 떼 출현, FunPass 꺼짐) char.1 꾹 누르기\n" + FunPassKit.Table(spec));
            var tuned = FunPassKit.PlayRun(FunPassKit.TunedDb, 1, 20261115UL, 30);
            FunPassKit.Log("== tuned char.1 꾹 누르기\n" + FunPassKit.Table(tuned));
            var tap = FunPassKit.PlayRun(FunPassKit.TunedDb, 1, 20261115UL, 30, FunPassKit.HeatPolicy.Tap);
            FunPassKit.Log("== tuned char.1 끊어 쏘기\n" + FunPassKit.Table(tap));
            FunPassKit.Log(string.Format("평균 클리어(초) 1~9 / 11~19 / 21~29: spec {0:0.0} / {1:0.0} / {2:0.0}, tuned {3:0.0} / {4:0.0} / {5:0.0}, tuned tap {6:0.0} / {7:0.0} / {8:0.0}",
                AvgClear(spec, 1, 9), AvgClear(spec, 11, 19), AvgClear(spec, 21, 29),
                AvgClear(tuned, 1, 9), AvgClear(tuned, 11, 19), AvgClear(tuned, 21, 29),
                AvgClear(tap, 1, 9), AvgClear(tap, 11, 19), AvgClear(tap, 21, 29)));

            Assert.IsNotNull(Row(tuned, 30), "봇(체력만 채움)이 웨이브 30까지 간다");
            Assert.Greater(Row(tuned, 30).ClearTicks, 0, "웨이브 30 클리어");

            // B: 웨이브 1·2에 탄을 쏘는 적이 있어 스침이 나고, 웨이브 1부터 K를 쓴다(시작 결정).
            var w1 = Row(tuned, 1);
            StringAssert.Contains("shaman_cap", w1.Species, "웨이브 1에 갓버섯 술사");
            Assert.Greater(Row(tuned, 1).ArtsFired + Row(tuned, 2).ArtsFired, 0, "웨이브 1~2에 비기를 쓴다");
            Assert.LessOrEqual(w1.ClearTicks / 60f, 40f, "웨이브 1은 짧다(봇 기준 40초 이하)");
            // B: 웨이브 길이 곡선 — 뒤로 갈수록 길다.
            Assert.Greater(AvgClear(tuned, 21, 29), AvgClear(tuned, 1, 9), "후반 웨이브가 초반보다 길다");
            Assert.Greater(AvgClear(tuned, 11, 19), AvgClear(tuned, 1, 9), "둘째 날 웨이브가 첫날보다 길다");

            // C: 떼 출현의 늘린 적도 보상을 나눠 받는다 → 처치 수 = 나온 수(엘리트·졸개 제외 오차만), 웨이브 3 늑대 급등 완화.
            for (int i = 0; i < tuned.Count; i++)
                if (!tuned[i].Boss && tuned[i].Spawned > 0)
                    Assert.GreaterOrEqual(tuned[i].Kills, tuned[i].Spawned * 9 / 10, "웨이브 " + tuned[i].Wave + " 보상 받는 처치");
            Assert.Less(CountOf(Row(tuned, 3).Species, "wolf_bramble"), CountOf(Row(spec, 3).Species, "wolf_bramble"), "웨이브 3 늑대 수");

            // G: 보스 직전 웨이브(9)에 다음 보스(첫 만남 허상) 시연 종이 섞인다.
            Assert.GreaterOrEqual(CountOf(Row(tuned, 9).Species, "shaman_cap"), 2, "웨이브 9 = 허상 시연(갓버섯 술사 2)");
        }

        static int CountOf(string species, string id)
        {
            if (species == null) return 0;
            foreach (var part in species.Split(' '))
            {
                int x = part.IndexOf('×');
                if (x > 0 && part.Substring(0, x) == id) return int.Parse(part.Substring(x + 1));
            }
            return 0;
        }
    }
}
