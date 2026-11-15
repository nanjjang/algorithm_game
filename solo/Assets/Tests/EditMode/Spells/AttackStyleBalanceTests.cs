using System;
using NUnit.Framework;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>
    /// 기본 공격 유형의 균형(사용자 지시, Docs/12-attack-styles.md 5절). 허수아비 방(FunPass 켬 = 게임과 같은 값), 6,000틱:
    /// (1) 꾹 누르기·임계 끊어 쏘기 DPS가 같은 캐릭터 마력탄의 ±12%, (2) 임계 끊어 쏘기 ≥ 꾹 누르기 × 1.25(과열 리듬 규칙 유지),
    /// (3) 3마리 무리에서 마력탄 무리 DPS의 1.8배 이하(범위 유형이 과하지 않게). 표는 로그로 남긴다.
    /// </summary>
    [Category("Balance")]
    public sealed class AttackStyleBalanceTests
    {
        const int Ticks = 6000;
        const float Band = 0.12f;
        const float TapOverHold = 1.25f;
        const float ClusterCap = 1.8f;

        static void Log(string s)
        {
            try { TestContext.WriteLine(s); }
            catch (Exception) { Console.WriteLine(s); }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AS_Balance__HoldAndTapWithin12PercentOfMagicBolt_ClusterAtMost18x(int character)
        {
            var db = AttackStyleKit.Tuned;
            var bolt = AttackStyleKit.MeasureRow(db, character, null, Ticks);
            Log("char." + character + " magic_bolt hold " + bolt.Hold.ToString("0.0") + " tap " + bolt.Tap.ToString("0.0") + " cluster " + bolt.Cluster.ToString("0.0"));
            Assert.GreaterOrEqual(bolt.Tap, bolt.Hold * TapOverHold, "마력탄 자체의 끊어 쏘기 규칙(FunPass)");
            foreach (var id in AttackStyleKit.Styles[character - 1])
            {
                var r = AttackStyleKit.MeasureRow(db, character, id, Ticks);
                Log(id + " hold " + r.Hold.ToString("0.0") + " (" + (r.Hold / bolt.Hold).ToString("0.00") + ") tap " + r.Tap.ToString("0.0")
                    + " (" + (r.Tap / bolt.Tap).ToString("0.00") + ") cluster " + r.Cluster.ToString("0.0") + " (" + (r.Cluster / bolt.Cluster).ToString("0.00") + ")");
                Assert.AreEqual(1f, r.Hold / bolt.Hold, Band, id + " 꾹 누르기 DPS ±12%");
                Assert.AreEqual(1f, r.Tap / bolt.Tap, Band, id + " 임계 끊어 쏘기 DPS ±12%");
                Assert.GreaterOrEqual(r.Tap, r.Hold * TapOverHold, id + " 끊어 쏘기 ≥ 꾹 × 1.25");
                Assert.LessOrEqual(r.Cluster, bolt.Cluster * ClusterCap, id + " 3마리 무리 ≤ 마력탄 × 1.8");
                Assert.GreaterOrEqual(r.Cluster, r.Hold * 0.99f, id + " 무리에서 덜 세지는 않다");
            }
        }

        [Test]
        public void AS_Balance__MeasurementIsDeterministic()
        {
            var db = AttackStyleKit.Tuned;
            foreach (var id in new[] { "block.core.ember_mortar", "block.core.flame_jet", "block.core.rune_mine" })
            {
                int c = id == "block.core.rune_mine" ? 3 : 1;
                var a = AttackStyleKit.Measure(db, c, id, AttackStyleKit.Policy.Tap, AttackStyleKit.DistanceFor(id), true, 1200);
                var b = AttackStyleKit.Measure(db, c, id, AttackStyleKit.Policy.Tap, AttackStyleKit.DistanceFor(id), true, 1200);
                Assert.AreEqual(a.Damage, b.Damage, id);
                Assert.AreEqual(a.Shots, b.Shots, id);
            }
        }

        [Test]
        public void AS_Balance__CampStatsDummyDpsIsInTheSameBallpark()
        {
            // 야영 수치판의 허수아비 DPS 어림(ComputeStats, estimate_hits)이 실제 꾹 누르기 측정의 ±35% 안 — 조립을 고를 때 엉뚱한 값이 나오지 않게.
            var db = AttackStyleKit.Tuned;
            var mods = SimModuleSet.CreateDefault();
            for (int c = 1; c <= 5; c++)
                foreach (var id in AttackStyleKit.Styles[c - 1])
                {
                    var w = AttackStyleKit.Start(db, AttackStyleKit.PracticeConfig(db, c, id));
                    var p = w.P0;
                    var stats = mods.Spells.ComputeStats(db, p.Spell, p.Def, p.Derived);
                    float measured = AttackStyleKit.Measure(db, c, id, AttackStyleKit.Policy.Hold, AttackStyleKit.DistanceFor(id), false, Ticks).Dps;
                    Assert.AreEqual(1f, stats.DummyDps / measured, 0.35f, id + " 어림 " + stats.DummyDps + " / 측정 " + measured);
                }
        }
    }
}
