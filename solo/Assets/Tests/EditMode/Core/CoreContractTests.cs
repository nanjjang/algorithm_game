using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Core
{
    /// <summary>공용 계약(Core)의 순수 함수와 빈 모듈 세계가 도는지 확인한다. 본보기 테스트이기도 하다.</summary>
    public class CoreContractTests
    {
        // 08 요약서 6.1절 TV-042: 23→3, 10→1, 11→2, 1→1, 20→2
        [TestCase(23, 3)]
        [TestCase(10, 1)]
        [TestCase(11, 2)]
        [TestCase(1, 1)]
        [TestCase(20, 2)]
        public void S13_TV_042__DayOfWave(int wave, int day)
        {
            Assert.AreEqual(day, DayCycle.Day(wave));
        }

        [Test]
        public void DayCycle_SlotTodAndBossRotation()
        {
            Assert.AreEqual(1, DayCycle.Slot(11));
            Assert.AreEqual(10, DayCycle.Slot(20));
            Assert.AreEqual(Ids.Tod.Day, DayCycle.Tod(4));
            Assert.AreEqual(Ids.Tod.Dusk, DayCycle.Tod(5));
            Assert.AreEqual(Ids.Tod.Night, DayCycle.Tod(19));
            Assert.AreEqual(Ids.Tod.Boss, DayCycle.Tod(30));
            Assert.IsTrue(DayCycle.IsBossWave(10));
            Assert.IsFalse(DayCycle.IsBossWave(0));
            Assert.AreEqual(0, DayCycle.BossRotationIndex(10, 6));
            Assert.AreEqual(5, DayCycle.BossRotationIndex(60, 6));
            Assert.AreEqual(0, DayCycle.BossRotationIndex(70, 6));
            Assert.AreEqual(2, DayCycle.BossCycle(70, 6));
        }

        // 04 요약서 0.3절: 0.07초 = 1,075u, 0.18초 = 2,765u, 0.3초 = 4,608u
        [Test]
        public void S03_ParserUnits__RoundToNearest()
        {
            Assert.AreEqual(1075, SimConst.Units(0.07));
            Assert.AreEqual(2765, SimConst.Units(0.18));
            Assert.AreEqual(3072, SimConst.Units(0.2));
            Assert.AreEqual(4608, SimConst.Units(0.3));
            Assert.AreEqual(9, SimConst.Ticks(0.15f));
            Assert.AreEqual(480, SimConst.HeatToHu(4f));
        }

        // 02 요약서 4.4절 상대 방향 표
        [Test]
        public void S01_RelativeDirTable()
        {
            Assert.AreEqual(CmdDir.Forward, Dir8Util.Relative(Dir8.N, Dir8.N));
            Assert.AreEqual(CmdDir.Left, Dir8Util.Relative(Dir8.W, Dir8.N));
            Assert.AreEqual(CmdDir.Back, Dir8Util.Relative(Dir8.S, Dir8.N));
            Assert.AreEqual(CmdDir.Right, Dir8Util.Relative(Dir8.E, Dir8.N));
            Assert.AreEqual(CmdDir.Right, Dir8Util.Relative(Dir8.N, Dir8.W));
            Assert.AreEqual(CmdDir.Left, Dir8Util.Relative(Dir8.N, Dir8.E));
            Assert.AreEqual(CmdDir.Back, Dir8Util.Relative(Dir8.N, Dir8.S));
        }

        [Test]
        public void Dir8_SimVectorIsYDown()
        {
            Assert.AreEqual(new Vector2(0, -1), Dir8Util.ToSimVector(Dir8.N));
            Assert.AreEqual(Dir8.S, Dir8Util.Flip(Dir8.N));
            Assert.AreEqual(90f, Dir8Util.SimVectorToDegrees(new Vector2(0, -1)), 0.001f);
        }

        [Test]
        public void Pcg32_SameSeedSameSequence_StreamsDiffer()
        {
            var a = Pcg32.Derive(42, RngStream.Loot);
            var b = Pcg32.Derive(42, RngStream.Loot);
            var c = Pcg32.Derive(42, RngStream.Spawn);
            bool differs = false;
            for (int i = 0; i < 32; i++)
            {
                uint x = a.NextU32();
                Assert.AreEqual(x, b.NextU32());
                if (x != c.NextU32()) differs = true;
            }
            Assert.IsTrue(differs);
            for (int i = 0; i < 1000; i++)
            {
                int r = a.Range(3, 7);
                Assert.IsTrue(r >= 3 && r < 7);
            }
        }

        [Test]
        public void SimWorld_StepsWithStubModules()
        {
            var w = new SimWorld(null, SimModuleSet.Stubs(), 7);
            for (int i = 0; i < 120; i++) w.Step();
            Assert.AreEqual(120, w.Tick);
            Assert.AreEqual(SimConst.MaxPlayers, w.Players.Length);
            Assert.AreEqual(new Vector2(0.3f, 13.7f), w.ClampToArena(new Vector2(-5f, 99f), 0.3f));
        }

        [Test]
        public void SimModuleSet_CreateDefault_NeverNull()
        {
            var s = SimModuleSet.CreateDefault();
            Assert.NotNull(s.Player);
            Assert.NotNull(s.Spells);
            Assert.NotNull(s.Arts);
            Assert.NotNull(s.Traits);
            Assert.NotNull(s.Threats);
            Assert.NotNull(s.Enemies);
            Assert.NotNull(s.Bosses);
            Assert.NotNull(s.Waves);
            Assert.NotNull(s.Run);
            Assert.NotNull(s.Score);
        }
    }
}
