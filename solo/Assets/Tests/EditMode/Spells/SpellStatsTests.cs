using NUnit.Framework;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Spells;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>조립 수치(03 요약서 13.3절)와 등록 자리. 기대값: 02 요약서 6.6·6.7절(T-H01·H02·H05~08), S02 TV-D17.</summary>
    public class SpellStatsTests
    {
        SpellWorld _k;

        [SetUp]
        public void SetUp() { _k = new SpellWorld(); }

        PlayerDerived DerivedOf(CharacterDef ch)
        {
            // 기본 TraitModule의 ComputeDerived = 캐릭터 정의의 임계 시작·잠금 틱 그대로(강화 +0, 변이 없음).
            return new TraitModule().ComputeDerived(_k.Db, ch, 0, new string[0]);
        }

        CompiledSpell Bolt() => _k.Compile(B.Mb, B.None, B.None, B.None, B.None);

        [Test]
        public void S02_13_3__HeatPerShotAndProjectileCount()
        {
            var s = _k.Compile(B.Mb, B.S3, B.Trig, B.Fb, B.None);
            var st = _k.Sys.ComputeStats(_k.Db, s, _k.Db.BaselineCharacter, DerivedOf(_k.Db.BaselineCharacter));
            // TV-J02: "발사 1회 = 투사체 6개 · 열 26.8"
            Assert.AreEqual(26.8f, st.HeatPerShot, 1e-4f);
            Assert.AreEqual(6, st.ProjectilesPerShot);
            Assert.AreEqual(30f, st.StandardT, 1e-4f);
        }

        // 임계 사격 가능 횟수: 마력탄 임계 85 → 4, 80 → 6, 90 → 3
        [TestCase(85f, 4)]
        [TestCase(80f, 6)]
        [TestCase(90f, 3)]
        [TestCase(70f, 8)]
        public void S01_7_7__CritShotCount(float critStart, int expected)
        {
            var d = DerivedOf(_k.Db.BaselineCharacter);
            d.CritStartHu = SimConst.HeatToHu(critStart);
            Assert.AreEqual(expected, _k.Sys.ComputeStats(_k.Db, Bolt(), _k.Db.BaselineCharacter, d).CritShots);
        }

        [Test]
        public void S01_7_7__CritShotsFromCharacterData()
        {
            // char.1 임계 80 → 6, char.4 임계 90 → 3, 그 밖 85 → 4
            int[] expected = { 6, 4, 4, 3, 4 };
            for (int n = 1; n <= Ids.Char.Count; n++)
            {
                var ch = _k.Db.Character(Ids.Char.Of(n));
                Assert.AreEqual(expected[n - 1], _k.Sys.ComputeStats(_k.Db, Bolt(), ch, DerivedOf(ch)).CritShots, "char." + n);
            }
        }

        [Test]
        public void S01_7_7__CritShotsZeroWhenOneShotExceedsMaxAndInfiniteWhenNoHeat()
        {
            // 발사당 열 > 100 → 0 (경고)
            var hot = _k.CompileWith(false, 0f, B.Fb, B.Fb, B.Fb, B.S3, B.Trig, B.Fb, B.Fb, B.S3, B.Fb);
            Assert.Greater(hot.HeatPerShotHu, SimConst.HeatToHu(100f));
            Assert.AreEqual(0, _k.Sys.ComputeStats(_k.Db, hot, _k.Db.BaselineCharacter, DerivedOf(_k.Db.BaselineCharacter)).CritShots);

            // 열 0이면 무한(-1): 열이 0인 투사체 블록은 없으므로 컴파일 결과의 열만 0으로 둔다.
            var cold = Bolt();
            cold.HeatPerShotHu = 0;
            Assert.AreEqual(-1, _k.Sys.ComputeStats(_k.Db, cold, _k.Db.BaselineCharacter, DerivedOf(_k.Db.BaselineCharacter)).CritShots);
        }

        // TV-D17 / T-H01·H02: 기준값, 마력탄 +0, J를 누르고만 있음 → 26발(일반 21 + 임계 5) + 잠금 3초 = 6.9초, 310 피해, 44.93 DPS
        [Test]
        public void S02_TV_D17__HoldFireCycleDps44_93()
        {
            var st = _k.Sys.ComputeStats(_k.Db, Bolt(), _k.Db.BaselineCharacter, DerivedOf(_k.Db.BaselineCharacter));
            Assert.AreEqual(310f / 6.9f, st.DummyDps, 1e-2f);
            Assert.AreEqual(44.93f, st.DummyDps, 1e-2f);
        }

        // T-H05~08: 꾹 누르기 1주기 — char.1 47.83 / char.2 48.44 / char.3 41.89 / char.4 43.48, char.5는 기준과 같은 44.93
        [TestCase(1, 47.83f)]
        [TestCase(2, 48.44f)]
        [TestCase(3, 41.89f)]
        [TestCase(4, 43.48f)]
        [TestCase(5, 44.93f)]
        public void S01_T_H05__HoldFireCycleDpsPerCharacter(int number, float expected)
        {
            var ch = _k.Db.Character(Ids.Char.Of(number));
            // 마력탄의 조립 수치는 연결 칸 수와 무관하다.
            var st = _k.Sys.ComputeStats(_k.Db, Bolt(), ch, DerivedOf(ch));
            Assert.AreEqual(expected, st.DummyDps, 1e-2f);
        }

        [Test]
        public void S02_13_3__MirrorImageUsesActualHeatAndCount()
        {
            var d = DerivedOf(_k.Db.BaselineCharacter);
            d.MirrorImage = true;
            var st = _k.Sys.ComputeStats(_k.Db, Bolt(), _k.Db.BaselineCharacter, d);
            Assert.AreEqual(6f, st.HeatPerShot, 1e-4f);
            Assert.AreEqual(2, st.ProjectilesPerShot);
            // (100 − 85) ÷ 6 = 2 → 3발
            Assert.AreEqual(3, st.CritShots);
        }

        [Test]
        public void S02_13_3__InvalidSpellGivesZeroStats()
        {
            var st = _k.Sys.ComputeStats(_k.Db, _k.Compile(null, B.None, B.None, B.None, B.None), _k.Db.BaselineCharacter, DerivedOf(_k.Db.BaselineCharacter));
            Assert.AreEqual(0f, st.HeatPerShot);
            Assert.AreEqual(0f, st.DummyDps);
            Assert.AreEqual(0, st.ProjectilesPerShot);
        }

        // 00 문서 2.1절: 등록 자리로 주문 모듈이 끼워진다
        [Test]
        public void S00_2_1__FactoryRegistersSpellSystem()
        {
            var set = SimModuleSet.CreateDefault();
            Assert.IsInstanceOf<SpellSystem>(set.Spells);
        }

        // 수치는 JSON에서 온다: 조각 G02와 const·블록 params
        [Test]
        public void S00_9__TuningReadsDataFragments()
        {
            var t = SpellTuning.From(_k.Db);
            Assert.AreEqual(2f, t.CritMultiplier);
            Assert.AreEqual(3f, t.TripleHeatCritMultiplier);
            Assert.AreEqual(SimConst.HeatToHu(100f), t.HeatMaxHu);
            Assert.AreEqual(64, t.TreeShotCap);
            Assert.AreEqual(300, t.PlayerShotCap);
            Assert.AreEqual(3, t.TriggerMaxDepth);
            Assert.AreEqual(1.25f, t.WeakMul);
            Assert.AreEqual(0.8f, t.ResistMul);
            Assert.AreEqual(1.15f, t.CorrosionMul, 1e-5f);
            Assert.AreEqual(180, t.CorrosionTicks);
            Assert.AreEqual(3f, t.HomingTurnDegreesPerTick, 1e-5f);
            Assert.AreEqual(1.5f, t.MirrorHeatMul);
        }
    }
}
