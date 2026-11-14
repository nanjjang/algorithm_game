using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Bosses;

namespace Wizard.Solo.Tests.Bosses.Common
{
    /// <summary>허상의 복사 변환(05 요약서 9.2절, 03 요약서 12절 끝).</summary>
    public class PhantomCopyRulesTests
    {
        static IContentDb Db => TestContent.Db;
        static PhantomCopyParams Prm => PhantomCopyParams.From(Db.Boss(Ids.Boss.MirrorPhantom).PhantomCopy);

        // 05 요약서 9.2절 phantom_copy 표.
        [Test]
        public void S06_9_2__PhantomCopyParamsFromData()
        {
            var p = Prm;
            Assert.That(p.IntervalRatio, Is.EqualTo(2.5f));
            Assert.AreEqual(12, p.MaxProjectiles);
            Assert.That(p.Speed, Is.EqualTo(6f));
            Assert.AreEqual(1, p.Damage);
            Assert.AreEqual(1, p.BounceCap);
            Assert.That(p.ExplosionRadiusRatio, Is.EqualTo(0.6f));
            Assert.That(p.ExplosionRadiusMax, Is.EqualTo(1.5f));
            Assert.AreEqual(48, SimConst.Ticks(p.ExplosionTelegraphSeconds));
            Assert.AreEqual(1, p.ExplosionDamage);
            Assert.AreEqual(36, SimConst.Ticks(p.ChainLineTelegraphSeconds));
            Assert.That(p.ChainLineWidth, Is.EqualTo(0.5f));
            Assert.That(p.ChainLineLength, Is.EqualTo(7f));
            Assert.AreEqual(2, p.CopyMutationsFromCycle);
        }

        // 03 요약서 12절 끝(S02 TV-F18): 3갈래 ±15 → ±19.19도(3칸 지점 1.0칸). 2갈래 ±10(사이 20도)은 그대로.
        [Test]
        public void S02_TV_F18__SplitThreeWidensTo1919()
        {
            float minGap = PhantomCopyRules.MinSplitGapDegrees(Prm, BossTuning.Load(Db));
            Assert.That(minGap, Is.EqualTo(19.19f).Within(0.005f));
            Assert.GreaterOrEqual(minGap, Prm.SplitMinAngleDegrees, "데이터의 최소 각(19.1도)보다 좁지 않다");

            var offsets = new List<float>();
            var three = Db.Block(Ids.Block.SplitThree);
            PhantomCopyRules.FanOffsets(three.SplitCount, three.Params.FanHalfAngleDegrees, minGap, offsets);
            Assert.AreEqual(3, offsets.Count);
            Assert.That(offsets[0], Is.EqualTo(-19.19f).Within(0.005f));
            Assert.That(offsets[1], Is.EqualTo(0f).Within(0.0001f));
            Assert.That(offsets[2], Is.EqualTo(19.19f).Within(0.005f));
            // 3칸 지점에서 이웃 탄 사이 1.0칸(현 길이)
            float chord = 2f * 3f * Mathf.Sin(offsets[2] / 2f * Mathf.Deg2Rad);
            Assert.That(chord, Is.EqualTo(1.0f).Within(0.001f));

            var two = Db.Block(Ids.Block.SplitTwo);
            PhantomCopyRules.FanOffsets(two.SplitCount, two.Params.FanHalfAngleDegrees, minGap, offsets);
            Assert.AreEqual(2, offsets.Count);
            Assert.That(offsets[0], Is.EqualTo(-10f).Within(0.0001f));
            Assert.That(offsets[1], Is.EqualTo(10f).Within(0.0001f));

            PhantomCopyRules.FanOffsets(1, 0f, minGap, offsets);
            CollectionAssert.AreEqual(new[] { 0f }, offsets);
        }

        // 05 요약서 9.2절: 간격 = 플레이어 간격 × 2.5(올림), 격노 −10%/스택, 하한 10틱.
        [TestCase(9, 0, 23)]    // 마력탄 0.15초 = 9틱 → 22.5 → 23
        [TestCase(10, 0, 25)]
        [TestCase(36, 0, 90)]   // 화염구 0.6초
        [TestCase(10, 5, 12)]   // 25 × 5 / 10 = 12.5 → 12
        [TestCase(9, 5, 11)]    // 23 × 5 / 10 = 11.5 → 11
        [TestCase(6, 5, 10)]    // 15 × 5 / 10 = 7 → 하한 10
        [TestCase(3, 0, 10)]    // 7.5 → 8 → 하한 10
        public void S06_9_2__CopyIntervalCeilRageAndMin(int playerTicks, int rage, int expected)
        {
            var phantom = Db.Boss(Ids.Boss.MirrorPhantom);
            int min = phantom.Patterns.Find(p => p.Key == MirrorPhantomBrain.PatternCopyFire).Steps[0].MinIntervalTicks;
            Assert.AreEqual(10, min);
            Assert.AreEqual(expected, PhantomCopyRules.CopyIntervalTicks(playerTicks, Prm.IntervalRatio, Db.BossCommon, rage, min));
        }

        // 05 요약서 9.2절: 발사 1회 최대 12개.
        [Test]
        public void S06_9_2__TwelveProjectileCapPerShot()
        {
            var prm = Prm;
            float minGap = PhantomCopyRules.MinSplitGapDegrees(prm, BossTuning.Load(Db));
            var spell = BossWorld.Spell(Db, Ids.Block.MagicBolt, split: 3, fanHalf: 15f, copies: 5);   // 15발
            var shots = new List<PhantomShot>();
            PhantomCopyRules.ScopeShots(Db, spell, 0, prm, minGap, false, prm.MaxProjectiles, shots, new List<float>());
            Assert.AreEqual(12, shots.Count);

            var small = BossWorld.Spell(Db, Ids.Block.MagicBolt, split: 3, fanHalf: 15f);
            shots.Clear();
            PhantomCopyRules.ScopeShots(Db, small, 0, prm, minGap, false, prm.MaxProjectiles, shots, new List<float>());
            Assert.AreEqual(3, shots.Count);
            Assert.That(shots[0].OffsetDegrees, Is.EqualTo(-19.19f).Within(0.005f));
            Assert.That(shots[2].OffsetDegrees, Is.EqualTo(19.19f).Within(0.005f));

            // 거울상(2회차부터): 앞 묶음 다음 뒤 묶음(+180도), 상한은 합쳐서 12.
            shots.Clear();
            PhantomCopyRules.ScopeShots(Db, small, 0, prm, minGap, true, prm.MaxProjectiles, shots, new List<float>());
            Assert.AreEqual(6, shots.Count);
            Assert.That(shots[4].OffsetDegrees, Is.EqualTo(180f).Within(0.0001f));
        }

        // 05 요약서 9.2절: 유도·서리·잠식 제거, 튕김 1회까지, 폭발 반경 60%(최대 1.5), 연쇄 번개 → 예고선, 그림자 구체 반지름 0.25 그대로.
        [Test]
        public void S06_9_2__TransformPerBlock()
        {
            var prm = Prm;
            var buf = new List<float>();
            var shots = new List<PhantomShot>();

            var bolt = BossWorld.Spell(Db, Ids.Block.MagicBolt);
            bolt.Scopes[0].Mods = new ScopeModifiers { BounceCount = 2, Homing = true, Frost = true, Corrosion = true, PierceCount = 2 };
            PhantomCopyRules.ScopeShots(Db, bolt, 0, prm, 0f, false, prm.MaxProjectiles, shots, buf);
            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(1, shots[0].Bounces, "벽 튕김은 1회까지");
            Assert.IsFalse(shots[0].ChainLine);
            Assert.That(shots[0].ExplosionRadius, Is.EqualTo(0f));
            Assert.That(shots[0].Radius, Is.EqualTo(0.12f).Within(0.0001f));

            shots.Clear();
            PhantomCopyRules.ScopeShots(Db, BossWorld.Spell(Db, Ids.Block.Fireball), 0, prm, 0f, false, prm.MaxProjectiles, shots, buf);
            Assert.That(shots[0].ExplosionRadius, Is.EqualTo(1.2f).Within(0.0001f), "플레이어 반경 2의 60%");
            Assert.LessOrEqual(shots[0].ExplosionRadius, 1.5f);

            shots.Clear();
            PhantomCopyRules.ScopeShots(Db, BossWorld.Spell(Db, Ids.Block.ChainLightning), 0, prm, 0f, false, prm.MaxProjectiles, shots, buf);
            Assert.IsTrue(shots[0].ChainLine);

            shots.Clear();
            PhantomCopyRules.ScopeShots(Db, BossWorld.Spell(Db, Ids.Block.ShadowOrb), 0, prm, 0f, false, prm.MaxProjectiles, shots, buf);
            Assert.That(shots[0].Radius, Is.EqualTo(0.25f).Within(0.0001f));

            shots.Clear();
            PhantomCopyRules.ScopeShots(Db, BossWorld.Spell(Db, Ids.Block.Boomerang), 0, prm, 0f, false, prm.MaxProjectiles, shots, buf);
            Assert.IsTrue(shots[0].Returns, "부메랑은 돌아오는 길에도 판정");

            shots.Clear();
            PhantomCopyRules.ScopeShots(Db, new CompiledSpell(), 0, prm, 0f, false, prm.MaxProjectiles, shots, buf);
            Assert.AreEqual(0, shots.Count, "유효하지 않은 주문은 쏘지 않는다");
        }

        // 05 요약서 9.2절 mimic_art: 해금한 ③④⑧ 중 하나(같은 기술 연속 금지, 하나뿐이면 반복), 없으면 ①. 첫 만남은 해금 무관 셋 다.
        [Test]
        public void S06_9_2__MimicArtChoice()
        {
            var copy = Db.Boss(Ids.Boss.MirrorPhantom).Patterns.Find(p => p.Key == MirrorPhantomBrain.PatternMimicArt).Steps[0].Copy;
            CollectionAssert.AreEqual(new[] { Ids.Art.Fan, Ids.Art.Wings, Ids.Art.Octastar }, copy.Arts);
            Assert.AreEqual(Ids.Art.Burst, copy.Fallback);
            Assert.AreEqual(48, SimConst.Ticks(copy.WarnSeconds));
            var buf = new List<string>();

            var none = new HashSet<string> { Ids.Art.Burst, Ids.Art.Recoil };
            Assert.AreEqual(Ids.Art.Burst, PhantomCopyRules.PickMimicArt(copy.Arts, copy.Fallback, none, false, null, n => 0, buf));

            var two = new HashSet<string> { Ids.Art.Burst, Ids.Art.Fan, Ids.Art.Octastar };
            Assert.AreEqual(Ids.Art.Octastar, PhantomCopyRules.PickMimicArt(copy.Arts, copy.Fallback, two, false, Ids.Art.Fan, n => 0, buf));
            Assert.AreEqual(Ids.Art.Fan, PhantomCopyRules.PickMimicArt(copy.Arts, copy.Fallback, two, false, Ids.Art.Octastar, n => 0, buf));

            var one = new HashSet<string> { Ids.Art.Wings };
            Assert.AreEqual(Ids.Art.Wings, PhantomCopyRules.PickMimicArt(copy.Arts, copy.Fallback, one, false, Ids.Art.Wings, n => 0, buf),
                "하나뿐이면 반복");

            // 첫 만남: 해금과 무관하게 셋 다, 연속 금지.
            for (int roll = 0; roll < 2; roll++)
            {
                int r = roll;
                string art = PhantomCopyRules.PickMimicArt(copy.Arts, copy.Fallback, none, true, Ids.Art.Fan, n => r, buf);
                Assert.AreNotEqual(Ids.Art.Fan, art);
                CollectionAssert.Contains(copy.Arts, art);
            }
            Assert.AreEqual(Ids.Art.Fan, PhantomCopyRules.PickMimicArt(copy.Arts, copy.Fallback, none, true, null, n => 0, buf));

            var bloom = Db.Boss(Ids.Boss.MirrorPhantom).Patterns.Find(p => p.Key == MirrorPhantomBrain.PatternBloomMimic);
            Assert.AreEqual(72, SimConst.Ticks(bloom.Steps[0].Copy.WarnSeconds));
            Assert.AreEqual(3, bloom.Steps[0].Repeat);
            Assert.AreEqual(30, SimConst.Ticks(bloom.Steps[0].RepeatIntervalSeconds));
            Assert.AreEqual(120, SimConst.Ticks(bloom.RecoverySeconds));
        }

        // 따라 쓰는 비기의 갈래: ③ 세 갈래(R3 보정 ±19.19) / ④ 좌우 한 번씩 / ⑧ 8방향 / ① 한 번.
        [Test]
        public void S06_9_2__MimicArtBranchAngles()
        {
            float minGap = PhantomCopyRules.MinSplitGapDegrees(Prm, BossTuning.Load(Db));
            var buf = new List<float>();

            var fan = Db.Art(Ids.Art.Fan).Effect;
            PhantomCopyRules.ArtBranchOffsets(fan.Branches, fan.BranchAngleDegrees, minGap, buf);
            Assert.AreEqual(3, buf.Count);
            Assert.That(buf[0], Is.EqualTo(-19.19f).Within(0.005f));
            Assert.That(buf[1], Is.EqualTo(0f).Within(0.0001f));
            Assert.That(buf[2], Is.EqualTo(19.19f).Within(0.005f));

            var wings = Db.Art(Ids.Art.Wings).Effect;
            PhantomCopyRules.ArtBranchOffsets(wings.Branches, wings.BranchAngleDegrees, minGap, buf);
            CollectionAssert.AreEqual(new[] { -90f, 90f }, buf);

            var octa = Db.Art(Ids.Art.Octastar).Effect;
            PhantomCopyRules.ArtBranchOffsets(octa.Branches, octa.BranchAngleDegrees, minGap, buf);
            Assert.AreEqual(8, buf.Count);
            for (int i = 0; i < 8; i++) Assert.That(buf[i], Is.EqualTo(45f * i).Within(0.0001f));

            var burst = Db.Art(Ids.Art.Burst).Effect;
            PhantomCopyRules.ArtBranchOffsets(burst.Branches, burst.BranchAngleDegrees, minGap, buf);
            CollectionAssert.AreEqual(new[] { 0f }, buf);
        }

        // 05 요약서 9.2절: 단계 60% / 25%, 상시 행동은 모든 단계에서.
        [Test]
        public void S06_9_2__PhantomPhasesAndPassives()
        {
            var phantom = Db.Boss(Ids.Boss.MirrorPhantom);
            Assert.That(phantom.BaseHp, Is.EqualTo(4000f));
            Assert.That(phantom.MoveSpeed, Is.EqualTo(2.5f));
            Assert.AreEqual(1, phantom.ContactDamage);
            Assert.AreEqual(3, phantom.Phases.Count);
            Assert.AreEqual(60, phantom.Phases[1].EnterAtHpPercent);
            Assert.AreEqual(25, phantom.Phases[2].EnterAtHpPercent);
            Assert.AreEqual(0, phantom.Phases[0].Patterns.Count);
            CollectionAssert.AreEqual(new[] { "mimic_art" }, phantom.Phases[1].Patterns);
            CollectionAssert.AreEqual(new[] { "mimic_art", "bloom_mimic" }, phantom.Phases[2].Patterns);
            for (int i = 0; i < 3; i++) CollectionAssert.AreEqual(new[] { "copy_fire", "summon_slimes" }, phantom.Phases[i].Passives);
            var summon = phantom.Patterns.Find(p => p.Key == MirrorPhantomBrain.PatternSummon).Steps[0];
            Assert.AreEqual(Ids.Enemy.SlimeMoss, summon.Summon.Enemy);
            Assert.AreEqual(6, summon.Summon.Count);
            Assert.AreEqual(1800, SimConst.Ticks(summon.RepeatIntervalSeconds));
            // 10.2절 first_encounter
            var fe = phantom.FirstEncounter;
            Assert.AreEqual(50, fe.HpFloorPercent);
            Assert.AreEqual(2700, SimConst.Ticks(fe.MaxFightSeconds));
            Assert.AreEqual(5, fe.RageStacks);
            Assert.AreEqual(3, fe.StartPhase);
            Assert.AreEqual(180, SimConst.Ticks(fe.FinisherSeconds));
            Assert.AreEqual(114, fe.FinisherHpZeroTicks);
            Assert.AreEqual(240, SimConst.Ticks(fe.CollapseSeconds));
        }
    }
}
