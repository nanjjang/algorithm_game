using Wizard.Content;
using Wizard.Sim.World;
using Xunit;

namespace Wizard.Sim.Tests.World
{
    // 하루와 웨이브: DECISIONS 2.3, spec 01 5.7·14.11(T-D01~T-D07), spec 14 3.9·7.3(TV-047~TV-050).
    public sealed class DayCycleTests
    {
        // 보스 순번의 길이(monsters 3, spec 01 5.7: 가마등·허상·천 갓·밤눈·흰뿔·밤삼킴). 데이터의 BossRotation 절은 아직 팩에 없다
        private const int SpecBossRotationLength = 6;

        [Theory]
        [InlineData(1, TimeOfDay.Day)]
        [InlineData(4, TimeOfDay.Day)]
        [InlineData(5, TimeOfDay.Dusk)]
        [InlineData(7, TimeOfDay.Dusk)]
        [InlineData(8, TimeOfDay.Night)]
        [InlineData(9, TimeOfDay.Night)]
        [InlineData(10, TimeOfDay.BossWave)]
        public void S01_T_D01__PhaseOfFirstDay(int wave, TimeOfDay expected)
        {
            Assert.Equal(expected, DayCycle.TimeOfDay(wave));
        }

        [Fact]
        public void S01_T_D02__Wave23IsDay3Slot3Day()
        {
            Assert.Equal(3, DayCycle.DayNumber(23));
            Assert.Equal(3, DayCycle.SlotInDay(23));
            Assert.Equal(TimeOfDay.Day, DayCycle.TimeOfDay(23));
        }

        // T-D03의 표시 글자("3일째 · 웨이브 27 (해질녘)")는 이름 표와 화면 로직(Wizard.Text·ViewLogic)이 만든다.
        // 코어가 정하는 값은 그 글자의 재료인 N일째, 웨이브 번호, 시간대다
        [Fact]
        public void S01_T_D03__Wave27DisplayPartsAreDay3AndDusk()
        {
            Assert.Equal(3, DayCycle.DayNumber(27));
            Assert.Equal(7, DayCycle.SlotInDay(27));
            Assert.Equal(TimeOfDay.Dusk, DayCycle.TimeOfDay(27));
        }

        [Theory]
        [InlineData(10, 0)]
        [InlineData(20, 1)]
        [InlineData(30, 2)]
        [InlineData(60, 5)]
        [InlineData(70, 0)]
        public void S01_T_D04__BossOrderIndexCycles(int wave, int expected)
        {
            Assert.Equal(expected, DayCycle.BossOrderIndex(wave, SpecBossRotationLength));
        }

        [Fact]
        public void S01_T_D05__Wave11IsDay2()
        {
            Assert.Equal(2, DayCycle.DayNumber(11));
        }

        // T-D06·T-D07: 이 모듈이 정하는 것은 "이 웨이브가 쓰러져도 판이 이어지는 첫 만남인가"까지다.
        // 체력 0 → 일어남 → 웨이브 11, 판 끝의 흐름은 체력(spec 01 6.3)과 웨이브·결과 상태 기계가 생긴 뒤 SimWorld 로 다시 확인한다
        [Fact]
        public void S01_T_D06__PhantomFirstWave10IsTheDownNotDeathEncounter()
        {
            Assert.True(DayCycle.IsPhantomFirstEncounter(10, phantomFirst: true));
            Assert.Equal(TimeOfDay.BossWave, DayCycle.TimeOfDay(10));
            // 일어난 뒤의 웨이브 11은 2일째 낮이다
            Assert.Equal(2, DayCycle.DayNumber(11));
            Assert.Equal(TimeOfDay.Day, DayCycle.TimeOfDay(11));
            // 첫 만남 플래그는 웨이브 10에만(spec 14 TV-046)
            Assert.False(DayCycle.IsPhantomFirstEncounter(20, phantomFirst: true));
            Assert.False(DayCycle.IsPhantomFirstEncounter(70, phantomFirst: true));
            Assert.False(DayCycle.IsPhantomFirstEncounter(9, phantomFirst: true));
        }

        [Fact]
        public void S01_T_D07__WithoutPhantomFirstWave10IsANormalBoss()
        {
            Assert.False(DayCycle.IsPhantomFirstEncounter(10, phantomFirst: false));
            Assert.True(DayCycle.IsBossWave(10));
            Assert.Equal(0, DayCycle.BossOrderIndex(10, SpecBossRotationLength));
        }

        [Theory]
        [InlineData(1, TimeOfDay.Day)]
        [InlineData(4, TimeOfDay.Day)]
        [InlineData(5, TimeOfDay.Dusk)]
        [InlineData(7, TimeOfDay.Dusk)]
        [InlineData(8, TimeOfDay.Night)]
        [InlineData(9, TimeOfDay.Night)]
        [InlineData(10, TimeOfDay.BossWave)]
        public void S14_TV_047__TimeOfDayFirstDay(int wave, TimeOfDay expected)
        {
            Assert.Equal(expected, DayCycle.TimeOfDay(wave));
        }

        [Fact]
        public void S14_TV_048__TimeOfDay23IsDay()
        {
            Assert.Equal(TimeOfDay.Day, DayCycle.TimeOfDay(23));
        }

        [Fact]
        public void S14_TV_049__TimeOfDay27Dusk30Boss()
        {
            Assert.Equal(TimeOfDay.Dusk, DayCycle.TimeOfDay(27));
            Assert.Equal(TimeOfDay.BossWave, DayCycle.TimeOfDay(30));
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(10, 1)]
        [InlineData(11, 2)]
        [InlineData(23, 3)]
        public void S14_TV_050__DayNumber(int wave, int expected)
        {
            Assert.Equal(expected, DayCycle.DayNumber(wave));
        }

        [Fact]
        public void Codes_MatchSpec14Section3_9()
        {
            // spec 14 3.9: 1 tod.day, 2 tod.dusk, 3 tod.night, 4 tod.boss
            Assert.Equal(1, (int)DayCycle.TimeOfDay(1));
            Assert.Equal(2, (int)DayCycle.TimeOfDay(5));
            Assert.Equal(3, (int)DayCycle.TimeOfDay(8));
            Assert.Equal(4, (int)DayCycle.TimeOfDay(10));
        }

        [Fact]
        public void MatchesSpecFormulasOverManyDays()
        {
            // spec 14 3.9: r = ((w − 1) % 10) + 1; DayNumber = (w + 9) / 10
            for (int w = 1; w <= 1000; w++)
            {
                int r = (w - 1) % 10 + 1;
                TimeOfDay expected = r <= 4 ? TimeOfDay.Day : r <= 7 ? TimeOfDay.Dusk : r <= 9 ? TimeOfDay.Night : TimeOfDay.BossWave;
                Assert.Equal(expected, DayCycle.TimeOfDay(w));
                Assert.Equal((w + 9) / 10, DayCycle.DayNumber(w));
                Assert.Equal(r, DayCycle.SlotInDay(w));
                Assert.Equal(r == 10, DayCycle.IsBossWave(w));
                Assert.Equal(r == 10 ? (w / 10 - 1) % 6 : -1, DayCycle.BossOrderIndex(w, SpecBossRotationLength));
            }
            Assert.Equal(214_748_365, DayCycle.DayNumber(int.MaxValue));      // 넘치지 않는다
        }

        [Fact]
        public void NonWavesAreNone()
        {
            Assert.Equal(TimeOfDay.None, DayCycle.TimeOfDay(0));
            Assert.Equal(TimeOfDay.None, DayCycle.TimeOfDay(-3));
            Assert.Equal(0, DayCycle.DayNumber(0));
            Assert.Equal(0, DayCycle.SlotInDay(-1));
            Assert.Equal(-1, DayCycle.BossOrderIndex(15, SpecBossRotationLength));
            Assert.Equal(-1, DayCycle.BossOrderIndex(10, 0));
            Assert.False(DayCycle.IsBossWave(0));
        }
    }
}
