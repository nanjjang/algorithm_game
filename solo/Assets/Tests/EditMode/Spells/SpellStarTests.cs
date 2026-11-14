using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>
    /// 판 한정 블록 성장(★, 전리품 겹치기)이 컴파일 수치에 닿는 몫. 값은 DataSolo/Loot.json star 절:
    /// 투사체 피해 +20%/★, 투사체·변형 열 −10%/★, 변형 스코프 피해 +8%/★, 분열 열 배수 덧분 −20%/★, 트리거 하위 스코프 피해 +12%/★, 상한 3.
    /// 별이 없으면 03 요약서 벡터 그대로(SpellCompilerTests가 지킨다).
    /// </summary>
    public class SpellStarTests
    {
        SpellWorld _k;

        [SetUp]
        public void SetUp() { _k = new SpellWorld(); }

        static int Hu(float heat) => SimConst.HeatToHu(heat);

        CompiledSpell Compile(int[] stars, string core, params string[] links)
        {
            var ctx = new SpellCompileContext { LinkSlotCount = links.Length, DepthDecay = _k.Db.Const.DepthDecayDefault, SlotStars = stars };
            return _k.Sys.Compile(_k.Db, core, links, ctx);
        }

        [Test]
        public void Star__NoStars_SameAsBefore()
        {
            var a = _k.Compile(B.Mb, B.Pierce, B.S2, B.None, B.None);
            var b = Compile(new[] { 0, 0, 0, 0, 0 }, B.Mb, B.Pierce, B.S2, B.None, B.None);
            Assert.AreEqual(a.HeatPerShotHu, b.HeatPerShotHu);
            Assert.AreEqual(a.StandardTBase, b.StandardTBase, 1e-5f);
            Assert.AreEqual(a.Scopes[0].Mods.DamageBonusSum, b.Scopes[0].Mods.DamageBonusSum, 1e-6f);
            Assert.AreEqual(1f, b.Scopes[0].Projectiles[0].DamageScale);
        }

        [Test]
        public void Star__ProjectileCore_DamageUp_HeatDown()
        {
            var s1 = Compile(new[] { 1, 0, 0, 0, 0 }, B.Mb, B.None, B.None, B.None, B.None);
            Assert.AreEqual(432, s1.HeatPerShotHu, "열 4(480 HU) × 0.9 = 3.6(432 HU)");
            Assert.AreEqual(1.2f, s1.Scopes[0].Projectiles[0].DamageScale, 1e-5f);
            Assert.AreEqual(12f, s1.StandardTBase, 1e-4f, "표준 T 바탕 10 × 1.2");
            Assert.AreEqual(1, s1.SlotStars[0]);

            var s3 = Compile(new[] { 3, 0, 0, 0, 0 }, B.Mb, B.None, B.None, B.None, B.None);
            Assert.AreEqual(336, s3.HeatPerShotHu, "★3: 열 × 0.7 = 2.8(336 HU)");
            Assert.AreEqual(16f, s3.StandardTBase, 1e-4f, "★3: 피해 × 1.6");

            var over = Compile(new[] { 9, 0, 0, 0, 0 }, B.Mb, B.None, B.None, B.None, B.None);
            Assert.AreEqual(3, over.SlotStars[0], "상한 3으로 자른다");
            Assert.AreEqual(s3.HeatPerShotHu, over.HeatPerShotHu);
        }

        [Test]
        public void Star__Modifier_ScopeBonusAndHeat()
        {
            // 관통 ★2: 피해 보너스 0.1 + 0.08 × 2 = 0.26, 열 3 × 0.8 = 2.4 → 발사 열 4 + 2.4 = 6.4
            var s = Compile(new[] { 0, 2, 0, 0, 0 }, B.Mb, B.Pierce, B.None, B.None, B.None);
            Assert.AreEqual(0.26f, s.Scopes[0].Mods.DamageBonusSum, 1e-5f);
            Assert.AreEqual(480 + 288, s.HeatPerShotHu, "4 + 3 × 0.8 = 6.4(768 HU)");
            Assert.AreEqual(2, s.Scopes[0].Mods.PierceCount, "관통 수는 블록 수치 그대로");
        }

        [Test]
        public void Star__Split_HeatMultiplierCut()
        {
            // 2갈래 ★1: 열 배수 1 + 0.6 × 0.8 = 1.48 → 마력탄 열 4 × 1.48 = 5.92
            var s = Compile(new[] { 0, 1, 0, 0, 0 }, B.Mb, B.S2, B.None, B.None, B.None);
            Assert.AreEqual(1.48f, s.Scopes[0].HeatMul, 1e-5f);
            Assert.AreEqual(710, s.HeatPerShotHu, "480 HU × 1.48 = 710.4 → 710");
            Assert.AreEqual(2, s.Scopes[0].SplitCount);
        }

        [Test]
        public void Star__Trigger_ChildScopeBonus()
        {
            var s = Compile(new[] { 0, 2, 0, 0, 0 }, B.Mb, B.Trig, B.Fb, B.None, B.None);
            Assert.AreEqual(2, s.Scopes.Count);
            Assert.AreEqual(0.24f, s.Scopes[1].Mods.DamageBonusSum, 1e-5f, "트리거 ★2: 하위 스코프 +24%");
            Assert.AreEqual(0f, s.Scopes[0].Mods.DamageBonusSum, 1e-6f, "부모 스코프는 그대로");
            Assert.AreEqual(480 + 2160, s.HeatPerShotHu, "트리거 열은 0 그대로(4 + 18)");
        }

        [Test]
        public void Star__Underline_CutsAfterStar()
        {
            // 밑줄(맨 끝 연결 칸) + ★1 화염구: 18 × 0.9 = 16.2 → 밑줄 min(5, ceil(16.2/2)=9) = 5 → 11.2
            var ctx = new SpellCompileContext { LinkSlotCount = 5, Underline = true, DepthDecay = 0.92f, SlotStars = new[] { 0, 0, 0, 0, 0, 1 } };
            var s = _k.Sys.Compile(_k.Db, B.Mb, new[] { B.None, B.None, B.None, B.None, B.Fb }, ctx);
            Assert.AreEqual(480 + 1944 - 600, s.HeatPerShotHu, "4 + 16.2 − 5 = 15.2 (HU)");
        }

        [Test]
        public void Star__StatsAndShots_ReflectStars()
        {
            var derived = _k.P.Derived;
            derived.CritStartHu = Hu(80f);
            derived.LockTicks = 120;
            var ch = _k.Db.Character(Ids.Char.C1);
            var plain = _k.Sys.ComputeStats(_k.Db, _k.Compile(B.Mb, B.None, B.None, B.None, B.None), ch, derived);
            var star = _k.Sys.ComputeStats(_k.Db, Compile(new[] { 2, 0, 0, 0, 0 }, B.Mb, B.None, B.None, B.None, B.None), ch, derived);
            Assert.Greater(star.DummyDps, plain.DummyDps * 1.4f - 1e-3f, "피해 ×1.4 + 열이 줄어 더 오래 쏜다");
            Assert.Less(star.HeatPerShot, plain.HeatPerShot);
            Assert.AreEqual(plain.ProjectilesPerShot, star.ProjectilesPerShot);
        }

        [Test]
        public void Star__ExecutedProjectileCarriesStarDamage()
        {
            var plain = _k.Compile(B.Mb, B.None, B.None, B.None, B.None);
            var star = Compile(new[] { 1, 0, 0, 0, 0 }, B.Mb, B.None, B.None, B.None, B.None);
            _k.Fire(plain);
            float m0 = _k.W.Projectiles[_k.W.Projectiles.Count - 1].DamageMul;
            _k.Fire(star);
            float m1 = _k.W.Projectiles[_k.W.Projectiles.Count - 1].DamageMul;
            Assert.AreEqual(m0 * 1.2f, m1, 1e-4f);
        }
    }
}
