using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Waves;

namespace Wizard.Solo.Tests.Waves
{
    /// <summary>날씨·돌발 사건·별똥 결정. 01 요약서 4.1절·7.2절·7.3절, 10 요약서 8절 Q-19·Q-20, 00 문서 D-44.</summary>
    public class WaveWeatherStarfallTests
    {
        static IContentDb Db => TestContent.Db;

        static int CountEvents(SimWorld w, SimEventType type, string id = null)
        {
            int n = 0;
            for (int i = 0; i < w.Events.Count; i++)
                if (w.Events[i].Type == type && (id == null || w.Events[i].Id == id)) n++;
            return n;
        }

        static SimEvent LastEvent(SimWorld w, SimEventType type)
        {
            for (int i = w.Events.Count - 1; i >= 0; i--) if (w.Events[i].Type == type) return w.Events[i];
            Assert.Fail("이벤트 없음: " + type);
            return default(SimEvent);
        }

        /// <summary>같은 세계에서 그 웨이브를 trials번 시작·종료하며 사건이 켜진 횟수를 센다.</summary>
        static int CountWeatherStarts(WaveRig rig, int wave, string eventId, int trials)
        {
            int hits = 0;
            for (int i = 0; i < trials; i++)
            {
                rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(wave, 1));
                if (rig.World.Weather.Active.Contains(eventId)) hits++;
                rig.Director.EndWave(rig.World);
            }
            return hits;
        }

        // ------------------------------------------------------------ 7.2절 날씨

        // 소나기는 낮·해질녘만(10%). 웨이브 1·11은 하루의 첫 웨이브라 하루 상한이 매번 풀린다
        [Test]
        public void S01_Weather__ShowerOnlyDayAndDusk()
        {
            var rig = WaveRig.Create(11);
            Assert.AreEqual(0, CountWeatherStarts(rig, 8, Ids.Event.Shower, 400), "밤에는 소나기가 없다");
            Assert.AreEqual(0, CountWeatherStarts(rig, 9, Ids.Event.Shower, 400));
            Assert.AreEqual(0, CountWeatherStarts(rig, 10, Ids.Event.Shower, 400), "보스 웨이브에도 없다");
            int day = CountWeatherStarts(rig, 1, Ids.Event.Shower, 600);
            Assert.That(day, Is.InRange(25, 110), "낮 10% 안팎이어야 한다: " + day);
        }

        [Test]
        public void S01_Weather__ShowerAtMostOncePerDay()
        {
            int daysWithShower = 0;
            for (ulong seed = 1; seed <= 80; seed++)
            {
                var rig = WaveRig.Create(seed);
                int showers = 0;
                for (int wave = 1; wave <= 7; wave++)       // 낮·해질녘 일곱 웨이브
                {
                    rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(wave, 1));
                    if (rig.World.Weather.Active.Contains(Ids.Event.Shower)) showers++;
                    rig.Director.EndWave(rig.World);
                }
                Assert.LessOrEqual(showers, 1, "seed " + seed);
                Assert.AreEqual(showers, rig.World.Weather.ShowersToday);
                Assert.AreEqual(showers, CountEvents(rig.World, SimEventType.WeatherStarted, Ids.Event.Shower));
                if (showers == 1) daysWithShower++;
            }
            Assert.Greater(daysWithShower, 0);
            Assert.Less(daysWithShower, 80);
        }

        [Test]
        public void S01_Weather__NoSecondShowerOnceDayCapIsReachedAndResetsNextDay()
        {
            var rig = WaveRig.Create(3);
            rig.World.Weather.ShowersToday = 1;
            Assert.AreEqual(0, CountWeatherStarts(rig, 3, Ids.Event.Shower, 400));
            Assert.AreEqual(1, rig.World.Weather.ShowersToday);
            // 날이 바뀌면(하루의 첫 웨이브) 다시 올 수 있다
            Assert.Greater(CountWeatherStarts(rig, 11, Ids.Event.Shower, 400), 0);
        }

        static WaveRig RigWithWeather(int wave, string eventId)
        {
            for (ulong seed = 1; seed <= 400; seed++)
            {
                var rig = WaveRig.Create(seed);
                rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(wave, 1));
                if (rig.World.Weather.Active.Contains(eventId)) return rig;
            }
            Assert.Fail("사건이 켜지는 씨앗을 찾지 못했다: " + eventId);
            return null;
        }

        // 소나기 60초(3,600틱)
        [Test]
        public void S01_Weather__ShowerLasts3600Ticks()
        {
            var rig = RigWithWeather(1, Ids.Event.Shower);
            Assert.AreEqual(3600, rig.World.Weather.ShowerUntilTick - rig.World.Tick);
            Assert.AreEqual(1, CountEvents(rig.World, SimEventType.WeatherStarted, Ids.Event.Shower));
            rig.Steps(3600);                    // 틱 0~3,599
            CollectionAssert.Contains(rig.World.Weather.Active, Ids.Event.Shower);
            Assert.AreEqual(0, CountEvents(rig.World, SimEventType.WeatherEnded, Ids.Event.Shower));
            rig.Steps(1);                       // 틱 3,600
            CollectionAssert.DoesNotContain(rig.World.Weather.Active, Ids.Event.Shower);
            Assert.AreEqual(1, CountEvents(rig.World, SimEventType.WeatherEnded, Ids.Event.Shower));
        }

        // Q-20: 소나기는 60초지만 웨이브가 끝나면 그친다
        [Test]
        public void S01_Weather__ShowerStopsWhenWaveEnds()
        {
            var rig = RigWithWeather(2, Ids.Event.Shower);
            rig.Steps(100);
            rig.Director.EndWave(rig.World);
            Assert.AreEqual(0, rig.World.Weather.Active.Count);
            Assert.AreEqual(1, CountEvents(rig.World, SimEventType.WeatherEnded, Ids.Event.Shower));
        }

        // 안개: 해질녘만(20%), 그 웨이브 끝까지
        [Test]
        public void S01_Weather__FogOnlyAtDuskAndLastsWholeWave()
        {
            var rig = WaveRig.Create(21);
            Assert.AreEqual(0, CountWeatherStarts(rig, 2, Ids.Event.Fog, 300));
            Assert.AreEqual(0, CountWeatherStarts(rig, 8, Ids.Event.Fog, 300));
            Assert.AreEqual(0, CountWeatherStarts(rig, 10, Ids.Event.Fog, 300));
            int dusk = CountWeatherStarts(rig, 6, Ids.Event.Fog, 600);
            Assert.That(dusk, Is.InRange(70, 180), "해질녘 20% 안팎이어야 한다: " + dusk);

            var foggy = RigWithWeather(5, Ids.Event.Fog);
            foggy.Steps(4000);
            CollectionAssert.Contains(foggy.World.Weather.Active, Ids.Event.Fog);
            foggy.Director.EndWave(foggy.World);
            CollectionAssert.DoesNotContain(foggy.World.Weather.Active, Ids.Event.Fog);
            Assert.AreEqual(1, CountEvents(foggy.World, SimEventType.WeatherEnded, Ids.Event.Fog));
        }

        // 느루의 훈수: 모든 시간대 5%, 문구 번호 1~2
        [Test]
        public void S01_Weather__MasterAdviceAnyTodWithLineNumber()
        {
            var rig = WaveRig.Create(31);
            bool one = false, two = false;
            int[] waves = { 1, 6, 8, 10 };
            foreach (int wave in waves)
            {
                rig.World.Events.Clear();
                for (int i = 0; i < 1000; i++)
                {
                    rig.Director.BeginWave(rig.World, wave == 10 ? WaveRig.Build(1, 10) : WaveRig.SlimePlan(wave, 1));
                    rig.Director.EndWave(rig.World);
                }
                int n = CountEvents(rig.World, SimEventType.MasterAdvice);
                Assert.That(n, Is.InRange(20, 95), "웨이브 " + wave + " 5% 안팎이어야 한다: " + n);
                for (int i = 0; i < rig.World.Events.Count; i++)
                {
                    var e = rig.World.Events[i];
                    if (e.Type != SimEventType.MasterAdvice) continue;
                    Assert.IsTrue(e.A == 1 || e.A == 2, "문구 번호 " + e.A);
                    Assert.IsTrue(TestContent.Names.Has("event.master_advice.line." + e.A));
                    one |= e.A == 1;
                    two |= e.A == 2;
                }
            }
            Assert.IsTrue(one && two);
            // 훈수·별똥은 "켜진 날씨" 목록에 들지 않는다
            CollectionAssert.DoesNotContain(rig.World.Weather.Active, Ids.Event.MasterAdvice);
            CollectionAssert.DoesNotContain(rig.World.Weather.Active, Ids.Event.StarCrystal);
        }

        // 4.1절: 날이 바뀌면 풀밭 무늬·소품 배치가 6벌 중 다음 것, 버린 물건(100%)
        [Test]
        public void S01_Weather__DayChangeAdvancesPropsAndDropsDiscardedItem()
        {
            var rig = WaveRig.Create(41);
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(1, 1));
            Assert.AreEqual(0, CountEvents(rig.World, SimEventType.DayChanged), "웨이브 1은 날이 바뀐 것이 아니다");
            Assert.AreEqual(0, rig.World.Weather.PropVariant);
            Assert.AreEqual(-1, rig.World.Weather.DiscardedItemVariant);
            rig.Director.EndWave(rig.World);

            rig.World.Weather.ShowersToday = 1;
            rig.World.Events.Clear();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(11, 1));
            var changed = LastEvent(rig.World, SimEventType.DayChanged);
            Assert.AreEqual(2, changed.A);
            Assert.AreEqual(1, changed.B);
            Assert.AreEqual(-1, changed.Slot);
            Assert.AreEqual(1, rig.World.Weather.PropVariant);
            Assert.That(rig.World.Weather.DiscardedItemVariant, Is.InRange(0, 5));
            Assert.AreEqual(1, CountEvents(rig.World, SimEventType.WeatherStarted, Ids.Event.DiscardedItem));
            Assert.LessOrEqual(rig.World.Weather.ShowersToday, 1);
            rig.Director.EndWave(rig.World);

            rig.World.Events.Clear();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(12, 1));
            Assert.AreEqual(0, CountEvents(rig.World, SimEventType.DayChanged));
            Assert.AreEqual(1, rig.World.Weather.PropVariant);
            rig.Director.EndWave(rig.World);

            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(21, 1));
            Assert.AreEqual(2, rig.World.Weather.PropVariant);
            rig.Director.EndWave(rig.World);
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(61, 1));
            Assert.AreEqual(0, rig.World.Weather.PropVariant, "7일째는 다시 첫 벌");
        }

        [Test]
        public void S01_Tod__TodChangedOnlyWhenTodDiffersFromPreviousWave()
        {
            var rig = WaveRig.Create(51);
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(1, 1));
            Assert.AreEqual(Ids.Tod.Day, LastEvent(rig.World, SimEventType.TodChanged).Id);
            rig.Director.EndWave(rig.World);
            rig.World.Events.Clear();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(2, 1));
            Assert.AreEqual(0, CountEvents(rig.World, SimEventType.TodChanged));
            rig.Director.EndWave(rig.World);
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(4, 1));
            rig.Director.EndWave(rig.World);
            rig.World.Events.Clear();
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(5, 1));
            Assert.AreEqual(Ids.Tod.Dusk, LastEvent(rig.World, SimEventType.TodChanged).Id);
            Assert.AreEqual(1, CountEvents(rig.World, SimEventType.TodChanged));
        }

        // ------------------------------------------------------------ 7.3절 별똥 결정

        // 밤만, 50%, 웨이브 시작 뒤 600~2,400틱(균등)
        [Test]
        public void S01_Starfall__OnlyAtNightHalfTheTimeWithin600To2400()
        {
            var rig = WaveRig.Create(61);
            int[] notNight = { 1, 4, 5, 7, 10 };
            foreach (int wave in notNight)
                for (int i = 0; i < 200; i++)
                {
                    rig.Director.BeginWave(rig.World, wave == 10 ? WaveRig.Build(1, 10) : WaveRig.SlimePlan(wave, 1));
                    Assert.AreEqual(StarfallPhase.None, rig.World.Starfall.Phase, "웨이브 " + wave);
                    rig.Director.EndWave(rig.World);
                }

            int scheduled = 0, early = 0, late = 0;
            for (int i = 0; i < 600; i++)
            {
                rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(i % 2 == 0 ? 8 : 9, 1));
                var star = rig.World.Starfall;
                if (star.Phase == StarfallPhase.Scheduled)
                {
                    scheduled++;
                    Assert.That(star.ScheduledTick, Is.InRange(600, 2400));
                    if (star.ScheduledTick < 1500) early++; else late++;
                }
                else Assert.AreEqual(StarfallPhase.None, star.Phase);
                rig.Director.EndWave(rig.World);
            }
            Assert.That(scheduled, Is.InRange(240, 360), "50% 안팎이어야 한다: " + scheduled);
            Assert.Greater(early, 60);
            Assert.Greater(late, 60);
        }

        /// <summary>밤 웨이브를 시작하고 별똥을 웨이브 시계 scheduledTick에 떨어지게 맞춘 세계.</summary>
        static WaveRig NightWithStar(int scheduledTick, ulong seed = 71)
        {
            var rig = WaveRig.Create(seed);
            rig.World.Players[0].Pos = new Vector2(12f, 7f);
            rig.Director.BeginWave(rig.World, WaveRig.SlimePlan(8, 1));
            var star = rig.World.Starfall;
            star.Phase = StarfallPhase.Scheduled;
            star.ScheduledTick = scheduledTick;
            star.NextRetryTick = 0;
            rig.World.Events.Clear();
            return rig;
        }

        [Test]
        public void S01_Starfall__WaitsForScheduledWaveTick()
        {
            var rig = NightWithStar(600);
            rig.Steps(599);
            Assert.AreEqual(599, rig.World.Wave.WaveTick);
            Assert.AreEqual(StarfallPhase.Scheduled, rig.World.Starfall.Phase);
            rig.Steps(1);
            Assert.AreEqual(StarfallPhase.Telegraph, rig.World.Starfall.Phase);
        }

        // Scheduled → Telegraph 90틱 → Landed 480틱 → Expired
        [Test]
        public void S01_Starfall__Telegraph90ThenLanded480ThenExpired()
        {
            var rig = NightWithStar(0);
            rig.Steps(1);                       // 틱 0
            var star = rig.World.Starfall;
            Assert.AreEqual(StarfallPhase.Telegraph, star.Phase);
            var tele = LastEvent(rig.World, SimEventType.StarfallTelegraph);
            Assert.AreEqual(90, tele.A);
            Assert.AreEqual(star.Pos, tele.Pos);
            Assert.AreEqual(-1, tele.Slot);

            rig.Steps(89);                      // 틱 1~89
            Assert.AreEqual(StarfallPhase.Telegraph, star.Phase);
            rig.Steps(1);                       // 틱 90
            Assert.AreEqual(StarfallPhase.Landed, star.Phase);
            Assert.AreEqual(star.Pos, LastEvent(rig.World, SimEventType.StarfallLanded).Pos);

            rig.Steps(479);                     // 착지 뒤 479틱
            Assert.AreEqual(StarfallPhase.Landed, star.Phase);
            rig.Steps(1);                       // 480틱
            Assert.AreEqual(StarfallPhase.Expired, star.Phase);
            Assert.AreEqual(1, CountEvents(rig.World, SimEventType.StarfallExpired));
            Assert.AreEqual(0, CountEvents(rig.World, SimEventType.StarfallPicked));
            Assert.AreEqual(0, rig.Player.GainCalls, "밟지 않으면 결정을 주지 않는다");
            Assert.AreEqual(0, rig.Threats.Requests.Count, "별똥은 피해 없는 표시라 예고 구역을 청하지 않는다");
        }

        // 자리: 가운데 20×10칸, 격자 0.5칸, 플레이어에게서 3칸 이상, 위협에서 2칸 이상(Threats.IsClear)
        [Test]
        public void S01_Starfall__SpotInsideCenterAreaOnGridAwayFromPlayerAndThreats()
        {
            for (ulong seed = 1; seed <= 60; seed++)
            {
                var rig = NightWithStar(0, seed);
                var player = new Vector2(7f + seed % 9, 4f + seed % 5);
                rig.World.Players[0].Pos = player;
                float asked = -1f;
                // 오른쪽 절반은 위협이 있다고 답한다
                rig.Threats.Clear = (pos, clearance) => { asked = clearance; return pos.x < 12f; };
                rig.Steps(1);
                var star = rig.World.Starfall;
                Assert.AreEqual(StarfallPhase.Telegraph, star.Phase, "seed " + seed);
                Assert.AreEqual(2f, asked, 0.0001f);
                Assert.That(star.Pos.x, Is.InRange(2f, 22f));
                Assert.That(star.Pos.y, Is.InRange(2f, 12f));
                Assert.Less(star.Pos.x, 12f);
                Assert.AreEqual(0f, Mathf.Abs(star.Pos.x * 2f - Mathf.Round(star.Pos.x * 2f)), 0.0001f, "0.5칸 격자");
                Assert.AreEqual(0f, Mathf.Abs(star.Pos.y * 2f - Mathf.Round(star.Pos.y * 2f)), 0.0001f, "0.5칸 격자");
                Assert.GreaterOrEqual((star.Pos - player).magnitude, 3f);
            }
        }

        // 자리가 없으면 60틱 뒤 다시 찾는다
        [Test]
        public void S01_Starfall__RetriesAfter60TicksWhenNoSpot()
        {
            var rig = NightWithStar(0);
            rig.Threats.Clear = (pos, clearance) => false;
            rig.Steps(1);                       // 틱 0: 자리 없음
            var star = rig.World.Starfall;
            Assert.AreEqual(StarfallPhase.Scheduled, star.Phase);
            Assert.AreEqual(60, star.NextRetryTick);

            rig.Threats.Clear = null;           // 이제 비었지만 다시 찾는 것은 60틱 뒤
            rig.Steps(59);                      // 틱 1~59
            Assert.AreEqual(StarfallPhase.Scheduled, star.Phase);
            Assert.AreEqual(0, CountEvents(rig.World, SimEventType.StarfallTelegraph));
            rig.Steps(1);                       // 틱 60
            Assert.AreEqual(StarfallPhase.Telegraph, star.Phase);
        }

        static WaveRig LandedStar()
        {
            var rig = NightWithStar(0);
            rig.Steps(91);                      // 틱 0 예고, 틱 90 착지
            Assert.AreEqual(StarfallPhase.Landed, rig.World.Starfall.Phase);
            return rig;
        }

        // 줍기: 판정 중심과 별똥 중심의 거리 ≤ 0.5칸 → 결정 +1
        [Test]
        public void S01_Starfall__PickedWithinHalfCell()
        {
            var rig = LandedStar();
            var star = rig.World.Starfall;
            rig.World.Players[0].Pos = star.Pos + new Vector2(0.6f, 0f);
            rig.Steps(5);
            Assert.AreEqual(StarfallPhase.Landed, star.Phase, "0.6칸은 닿지 않는다");
            Assert.AreEqual(0, rig.Player.GainCalls);

            rig.World.Players[0].Pos = star.Pos + new Vector2(0.3f, 0.3f);
            rig.Steps(1);
            Assert.AreEqual(StarfallPhase.Picked, star.Phase);
            Assert.AreEqual(1, rig.Player.Gained);
            var picked = LastEvent(rig.World, SimEventType.StarfallPicked);
            Assert.AreEqual(0, picked.Slot);
            Assert.AreEqual(star.Pos, picked.Pos);
            rig.Steps(600);
            Assert.AreEqual(1, rig.Player.Gained, "한 번만 준다");
            Assert.AreEqual(0, CountEvents(rig.World, SimEventType.StarfallExpired));
        }

        // 결정 상한이 차 있으면 밟아도 사라지지 않고 남는다. 칸이 비면 그때 줍는다
        [Test]
        public void S01_Starfall__StaysWhenCrystalsAreFullThenPickedOnceRoomOpens()
        {
            var rig = LandedStar();
            var star = rig.World.Starfall;
            rig.Player.GainResult = false;
            rig.World.Players[0].Pos = star.Pos;
            rig.Steps(30);
            Assert.AreEqual(StarfallPhase.Landed, star.Phase);
            Assert.Greater(rig.Player.GainCalls, 0);
            Assert.AreEqual(0, CountEvents(rig.World, SimEventType.StarfallPicked));

            rig.Player.GainResult = true;
            rig.Steps(1);
            Assert.AreEqual(StarfallPhase.Picked, star.Phase);
            Assert.AreEqual(1, rig.Player.Gained);
        }

        // 가득 찬 채로 8초가 지나면 사라진다
        [Test]
        public void S01_Starfall__ExpiresEvenWhileStoodOnIfNeverPickable()
        {
            var rig = LandedStar();
            rig.Player.GainResult = false;
            rig.World.Players[0].Pos = rig.World.Starfall.Pos;
            rig.Steps(480);
            Assert.AreEqual(StarfallPhase.Expired, rig.World.Starfall.Phase);
            Assert.AreEqual(0, rig.Player.Gained);
        }

        [Test]
        public void S01_Starfall__DownedPlayerDoesNotPick()
        {
            var rig = LandedStar();
            rig.World.Players[0].Pos = rig.World.Starfall.Pos;
            rig.World.Players[0].Downed = true;
            rig.Steps(10);
            Assert.AreEqual(StarfallPhase.Landed, rig.World.Starfall.Phase);
            Assert.AreEqual(0, rig.Player.GainCalls);
        }

        // 웨이브가 끝날 때 남은 별똥은 사라진다. 떨어지기 전에 끝나면 안 나온다
        [Test]
        public void S01_Starfall__RemovedAtWaveEnd()
        {
            var landed = LandedStar();
            landed.World.Events.Clear();
            landed.Director.EndWave(landed.World);
            Assert.AreEqual(StarfallPhase.None, landed.World.Starfall.Phase);
            Assert.AreEqual(1, CountEvents(landed.World, SimEventType.StarfallExpired));

            var waiting = NightWithStar(2000);
            waiting.Steps(100);
            waiting.Director.EndWave(waiting.World);
            Assert.AreEqual(StarfallPhase.None, waiting.World.Starfall.Phase);
            Assert.AreEqual(0, CountEvents(waiting.World, SimEventType.StarfallTelegraph));
            Assert.AreEqual(0, CountEvents(waiting.World, SimEventType.StarfallExpired));
            waiting.Steps(3000);
            Assert.AreEqual(StarfallPhase.None, waiting.World.Starfall.Phase);
        }

        // 별똥 시계는 전투(WaveCombat) 동안만 간다
        [Test]
        public void S01_Starfall__DoesNotAdvanceOutsideCombat()
        {
            var rig = NightWithStar(0);
            rig.World.Run.Phase = RunPhase.WavePrelude;
            rig.Steps(200);
            Assert.AreEqual(StarfallPhase.Scheduled, rig.World.Starfall.Phase);
            rig.World.Run.Phase = RunPhase.WaveCombat;
            rig.Steps(1);
            Assert.AreEqual(StarfallPhase.Telegraph, rig.World.Starfall.Phase);
        }

        // 데이터 값 그대로(D-44)
        [Test]
        public void S01_Starfall__DataValuesAsSpecified()
        {
            var sf = Db.Starfall;
            Assert.AreEqual(Ids.Tod.Night, sf.OnlyTod);
            Assert.AreEqual(50, sf.ChancePercent);
            Assert.AreEqual(600, SimConst.Ticks(sf.DropWindowSeconds.From));
            Assert.AreEqual(2400, SimConst.Ticks(sf.DropWindowSeconds.To));
            Assert.AreEqual(90, SimConst.Ticks(sf.TelegraphSeconds));
            Assert.AreEqual(480, SimConst.Ticks(sf.LandedSeconds));
            Assert.AreEqual(120, SimConst.Ticks(sf.BlinkSeconds));
            Assert.AreEqual(60, SimConst.Ticks(sf.RetrySeconds));
            Assert.AreEqual(1, sf.Crystals);
        }
    }
}
