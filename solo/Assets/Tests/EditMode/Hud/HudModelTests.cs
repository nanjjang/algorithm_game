using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using Wizard.Solo.View;
using Wizard.Solo.View.Hud;

namespace Wizard.Solo.Tests.Hud
{
    /// <summary>
    /// 전투 HUD 표시 값 계산(HudModel)의 벡터. 기대값은 요약서에서 가져온다:
    /// 08 요약서 6.1절(TV-042~049), 02 요약서 6.6절(열 링 경계), 04 요약서 6.1절(늦음 올림), 10 요약서 1.3절(콤보 막대·결정 직전).
    /// 명세 번호: 08 = S13, 02 = S01, 04 = S03.
    /// </summary>
    public class HudModelTests
    {
        static readonly HudFakeNames Names = new HudFakeNames();

        // ---------------------------------------------------------------- 날·웨이브

        // TV-042: 23→3, 10→1, 11→2, 1→1, 20→2 (날 계산은 DayCycle을 쓴다)
        [TestCase(23, 3)]
        [TestCase(10, 1)]
        [TestCase(11, 2)]
        [TestCase(1, 1)]
        [TestCase(20, 2)]
        public void S13_TV_042__DayUsesDayCycle(int wave, int day)
        {
            Assert.AreEqual(day, HudModel.Day(wave));
            Assert.AreEqual(DayCycle.Day(wave), HudModel.Day(wave));
            Assert.AreEqual("str.battle_hud.day_wave{day=" + day + ",wave=" + wave + "}", HudModel.DayWaveText(Names, wave));
        }

        [Test]
        public void S13_TV_042__FrameDayFromRunWave()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 23);
            var f = HudModel.Compute(w, null, null, new ViewOptions());
            Assert.AreEqual(3, f.Day);
            Assert.AreEqual(23, f.Wave);
        }

        // ---------------------------------------------------------------- 허상까지 n웨이브 (RevengeTracker를 거쳐)

        static int WavesLeft(bool phantomMet, bool revengeActive, int wave)
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, wave);
            w.Run.RevengeActive = revengeActive;
            var f = HudModel.Compute(w, HudTestWorlds.Profile(phantomMet), null, new ViewOptions());
            Assert.AreEqual(HudModel.RevengeWavesLeft(HudTestWorlds.Profile(phantomMet), null, w.Run), f.RevengeWavesLeft);
            return f.RevengeWavesLeft;
        }

        [Test]
        public void S13_TV_043__RevengeWave14Shows6()
        {
            Assert.AreEqual(6, WavesLeft(true, true, 14));
            Assert.AreEqual("revenge.tracker.hud{n=6}", HudModel.RevengeText(Names, 6));
        }

        [TestCase(10, 0)]
        [TestCase(19, 1)]
        [TestCase(20, 0)]
        public void S13_TV_044__RevengeWave10_19_20(int wave, int expected)
        {
            Assert.AreEqual(expected, WavesLeft(true, true, wave));
        }

        [Test]
        public void S13_TV_045__RevengeBeforeMeetingNone()
        {
            Assert.AreEqual(0, WavesLeft(false, false, 14));
            Assert.AreEqual(string.Empty, HudModel.RevengeText(Names, 0));
        }

        [Test]
        public void S13_Contract__RevengeMetThisRunWave14Shows6()
        {
            // 프로필은 아직 만나기 전(phantom_met은 정산 때 적힌다)인데 이 판에서 첫 만남을 치렀다(w.Run.RevengeActive).
            Assert.AreEqual(6, WavesLeft(false, true, 14));
        }

        [Test]
        public void S13_Contract__RevengeDoneNone()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 14);
            w.Run.RevengeActive = false;
            var pr = HudTestWorlds.Profile(true);
            pr.Flags.RevengeDoneAt = "2026-11-01";
            Assert.AreEqual(0, HudModel.Compute(w, pr, null, new ViewOptions()).RevengeWavesLeft);
        }

        // ---------------------------------------------------------------- 보스 웨이브·보스 표시

        [Test]
        public void S13_TV_046__BossWaveLayout()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 20);
            var f = HudModel.Compute(w, null, null, new ViewOptions());
            Assert.IsTrue(f.BossWave);
            Assert.IsFalse(f.ShowWaveProgress, "보스 웨이브에 진행 바 숨김");
            Assert.IsTrue(f.DayWaveBossLayout);
            var r = HudModel.DayWaveRect(true);
            Assert.AreEqual(760f, r.X); Assert.AreEqual(76f, r.Y); Assert.AreEqual(400f, r.W); Assert.AreEqual(24f, r.H); Assert.AreEqual(16, r.FontPx);
            var n = HudModel.DayWaveRect(false);
            Assert.AreEqual(760f, n.X); Assert.AreEqual(36f, n.Y); Assert.AreEqual(400f, n.W); Assert.AreEqual(40f, n.H); Assert.AreEqual(32, n.FontPx);

            var w2 = HudTestWorlds.World(RunPhase.WaveCombat, 19);
            var f2 = HudModel.Compute(w2, null, null, new ViewOptions());
            Assert.IsTrue(f2.ShowWaveProgress);
            Assert.IsFalse(f2.DayWaveBossLayout);
        }

        [Test]
        public void S13_TV_047__FirstEncounterBarNoTicks()
        {
            var def = new BossDef { Id = Ids.Boss.MirrorPhantom };
            def.Phases.Add(new BossPhaseDef { Index = 0, EnterAtHpPercent = 100 });
            def.Phases.Add(new BossPhaseDef { Index = 1, EnterAtHpPercent = 60 });
            def.Phases.Add(new BossPhaseDef { Index = 2, EnterAtHpPercent = 25 });
            CollectionAssert.IsEmpty(HudModel.BossTickPercents(def, firstEncounter: true));
            CollectionAssert.AreEqual(new List<int> { 60, 25 }, HudModel.BossTickPercents(def, firstEncounter: false));
        }

        [Test]
        public void S13_TV_047__RealBossDataTicks()
        {
            var db = TestContent.Db;
            var kiln = db.Boss(Ids.Boss.KilnBoar);
            Assert.IsNotNull(kiln);
            // 단계 눈금 = enter_at_hp_percent(100 제외)
            var expected = new List<int>();
            foreach (var ph in kiln.Phases) if (ph.EnterAtHpPercent < 100) expected.Add(ph.EnterAtHpPercent);
            CollectionAssert.AreEqual(expected, HudModel.BossTickPercents(kiln, false));
            CollectionAssert.IsEmpty(HudModel.BossTickPercents(db.Boss(Ids.Boss.MirrorPhantom), true));
            Assert.AreEqual(576f, HudModel.BossTickX(60, 960f));
        }

        [Test]
        public void S13_Contract__BossNullNoBossDisplay()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 10);
            Assert.IsNull(w.Boss);
            Assert.IsFalse(HudModel.Compute(w, null, null, new ViewOptions()).ShowBoss);
            w.Boss = new BossState { Def = new BossDef { Id = Ids.Boss.KilnBoar }, Wave = 10 };
            Assert.IsTrue(HudModel.Compute(w, null, null, new ViewOptions()).ShowBoss);
        }

        [Test]
        public void S13_Contract__RevengeTagFromState()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 20);
            w.Run.RevengeActive = true;
            w.Boss = new BossState { Def = new BossDef { Id = Ids.Boss.MirrorPhantom }, Wave = 20, FirstEncounter = false };
            Assert.IsTrue(HudModel.RevengeTagFromState(w));
            w.Run.RevengeActive = false;
            Assert.IsFalse(HudModel.RevengeTagFromState(w));
            w.Run.RevengeActive = true;
            w.Boss.FirstEncounter = true;
            Assert.IsFalse(HudModel.RevengeTagFromState(w));
        }

        [Test]
        public void S05_BossRageText()
        {
            var b = new BossState();
            Assert.AreEqual(string.Empty, HudModel.BossRageText(Names, b));
            b.RageStacks = 3;
            Assert.AreEqual("str.battle_hud.boss.rage{n=3}", HudModel.BossRageText(Names, b));
            b.FinalRage = true;
            Assert.AreEqual("str.battle_hud.boss.final_rage", HudModel.BossRageText(Names, b));
        }

        // ---------------------------------------------------------------- 폭

        [Test]
        public void S13_TV_048__HpEightCellsWidth284()
        {
            Assert.AreEqual(284, HudLayout.HpRowWidth(8));
            Assert.AreEqual(140, HudLayout.HpRowWidth(4));
        }

        [Test]
        public void S13_TV_049__CircuitNineCellsWidth392()
        {
            Assert.AreEqual(392, HudLayout.CircuitWidth(9));
            var p = new PlayerState();
            p.Loadout.Links = new SlotItem[12];
            Assert.AreEqual(9, HudModel.CircuitCells(p), "코어 1 + 연결 최대 8");
            p.Loadout.Links = new SlotItem[4];
            Assert.AreEqual(5, HudModel.CircuitCells(p));
            p.Loadout.Core = new SlotItem { BlockId = Ids.Block.MagicBolt };
            p.Loadout.Links[1] = new SlotItem { BlockId = Ids.Block.Pierce };
            Assert.AreEqual(Ids.Block.MagicBolt, HudModel.CircuitBlock(p, 0));
            Assert.IsNull(HudModel.CircuitBlock(p, 1), "빈 칸");
            Assert.AreEqual(Ids.Block.Pierce, HudModel.CircuitBlock(p, 2));
        }

        // ---------------------------------------------------------------- 열 링 (02 요약서 6.6절)

        const int Crit85 = 85 * SimConst.HuPerHeat;

        [TestCase(0, false, HeatRingState.Cold)]
        [TestCase(1, false, HeatRingState.Rising)]
        [TestCase(70 * 120 - 1, false, HeatRingState.Rising)]
        [TestCase(70 * 120, false, HeatRingState.Imminent)]      // 임계 시작 − 15
        [TestCase(85 * 120 - 1, false, HeatRingState.Imminent)]
        [TestCase(85 * 120, false, HeatRingState.Critical)]      // 임계 시작
        [TestCase(95 * 120 - 1, false, HeatRingState.Critical)]
        [TestCase(95 * 120, false, HeatRingState.Brink)]         // 95
        [TestCase(100 * 120, false, HeatRingState.Brink)]        // 정확히 100(잠금 아님 — T-H03)
        [TestCase(100 * 120, true, HeatRingState.Locked)]
        [TestCase(0, true, HeatRingState.Locked)]
        public void S01_HeatRing__StateBoundaries(int heatHu, bool locked, HeatRingState expected)
        {
            Assert.AreEqual(expected, HudModel.RingState(heatHu, Crit85, locked));
        }

        [Test]
        public void S01_HeatRing__CritStartFromCharacterWhenNoDerived()
        {
            var p = new PlayerState { Def = new CharacterDef { CritStart = 80f } };
            Assert.AreEqual(80 * 120, HudModel.CritStartHu(p));
            p.Derived.CritStartHu = 90 * 120;
            Assert.AreEqual(90 * 120, HudModel.CritStartHu(p));
            p.HeatHu = 75 * 120;
            Assert.AreEqual(HeatRingState.Imminent, HudModel.RingState(p));
            Assert.AreEqual(306f, HudModel.RingAngleDegrees(85 * 120), 1e-3f);
        }

        [Test]
        public void S01_HeatRing__ColorsNeverGoldOrDanger()
        {
            foreach (HeatRingState s in System.Enum.GetValues(typeof(HeatRingState)))
            {
                HudColors.Ring(s, out var c, out _);
                Assert.IsFalse(UiTheme.IsDangerColor(c), s + " ring is danger red");
                Assert.AreNotEqual(UiTheme.Gold, new Color(c.r, c.g, c.b, 1f), s + " ring is gold");
            }
            Assert.IsFalse(UiTheme.IsDangerColor(HudColors.RingAsh));
            Assert.AreEqual(new Color(0.56f, 0.70f, 1.0f, 1f), HudModel.ArrowColor(FacingArrowMode.Reverse, 0.5f));
            Assert.AreEqual(1f, HudModel.ArrowColor(FacingArrowMode.Stance, 0.5f).a, "자세 불투명");
            Assert.Less(HudModel.ArrowColor(FacingArrowMode.Normal, 0.5f).a, 1f, "보통 반투명");
        }

        [Test]
        public void S01_HeatLock__SecondsAndRatio()
        {
            // 잠금 남은 틱 → 초 올림("과열 잠금 {n}초")
            Assert.AreEqual(3, HudModel.HeatLockSecondsLeft(414, 414 - 180));
            Assert.AreEqual(3, HudModel.HeatLockSecondsLeft(414, 414 - 121));
            Assert.AreEqual(2, HudModel.HeatLockSecondsLeft(414, 414 - 120));
            Assert.AreEqual(0, HudModel.HeatLockSecondsLeft(414, 414));
            Assert.AreEqual(0.5f, HudModel.LockRemainRatio(0, 200, 100), 1e-5f);
        }

        // ---------------------------------------------------------------- 늦음 문구 (04 요약서 6.1절)

        [TestCase(153L, 1)]
        [TestCase(307L, 2)]
        [TestCase(1075L, 7)]
        [TestCase(1L, 1)]
        public void S03_LateBy__CeilToHundredths(long lateU, int centi)
        {
            Assert.AreEqual(centi, HudModel.LateCentiFromU(lateU));
            Assert.AreEqual(centi, HudModel.LateCentiFromSeconds(lateU / (float)SimConst.UnitsPerSecond));
        }

        [Test]
        public void S03_LateBy__FormatTwoDecimals()
        {
            Assert.AreEqual("0.01", HudModel.FormatCenti(1));
            Assert.AreEqual("0.07", HudModel.FormatCenti(7));
            Assert.AreEqual("1.20", HudModel.FormatCenti(120));
            Assert.AreEqual(1, HudModel.LateCentiFromSeconds(0.01f), "이미 올림한 값은 그대로");
            Assert.AreEqual("str.dummy_practice.fail.late_input{n=3,sec=0.04}", HudModel.LateInputText(Names, 3, 0.04f));
        }

        // ---------------------------------------------------------------- 지금 K (04 요약서 6절)

        [Test]
        public void S03_NowK__PlainAndDebt()
        {
            var pv = new ArtPreview { ArtId = Ids.Art.Spear, Cost = 2 };
            var t = HudModel.NowK(Names, pv);
            Assert.IsTrue(t.Visible);
            Assert.AreEqual("str.battle_hud.now_k{art=art.spear,n=2}", t.Main);
            Assert.IsNull(t.Just);
            pv.DebtUsed = 1;
            Assert.AreEqual("str.battle_hud.now_k_debt{line=str.battle_hud.now_k{art=art.spear,n=2},n=1}", HudModel.NowK(Names, pv).Main);
        }

        [Test]
        public void S03_NowK__FizzleAndJustLine()
        {
            var pv = new ArtPreview { ArtId = Ids.Art.Bloom, Cost = 3, Fizzle = true, Reason = ArtFizzleReason.NoCrystal };
            Assert.AreEqual("str.battle_hud.now_k{art=str.battle_hud.now_k_fizzle,n=3}", HudModel.NowK(Names, pv).Main);
            pv.Reason = ArtFizzleReason.FullBloomLate;
            pv.LateIndex = 3;
            pv.LateSeconds = 614 / (float)SimConst.UnitsPerSecond; // 614u → 0.04초(올림)
            Assert.AreEqual("str.battle_hud.now_k{art=str.battle_hud.now_k_fizzle_late{n=3,sec=0.04},n=3}", HudModel.NowK(Names, pv).Main);

            var just = new ArtPreview { ArtId = Ids.Art.Spear, Cost = 2, HasJustLine = true, JustArtId = Ids.Art.ThunderSpear, JustCost = 3 };
            var t = HudModel.NowK(Names, just);
            Assert.AreEqual("str.battle_hud.now_k_just str.battle_hud.now_k{art=art.thunder_spear,n=3}", t.Just);
            Assert.AreEqual("str.battle_hud.now_k{art=art.spear,n=2}", t.Main);

            Assert.IsFalse(HudModel.NowK(Names, default(ArtPreview)).Visible);
        }

        [Test]
        public void S03_NowK__CooldownBorderPerimeter()
        {
            HudModel.PerimeterFill(0f, 192, 16, out float t0, out float r0, out float b0, out float l0);
            Assert.AreEqual(0f, t0 + r0 + b0 + l0);
            HudModel.PerimeterFill(0.5f, 192, 16, out float t, out float r, out float b, out float l);
            Assert.AreEqual(192f, t); Assert.AreEqual(16f, r); Assert.AreEqual(0f, b); Assert.AreEqual(0f, l);
            HudModel.PerimeterFill(1f, 192, 16, out t, out r, out b, out l);
            Assert.AreEqual(192f, t); Assert.AreEqual(16f, r); Assert.AreEqual(192f, b); Assert.AreEqual(16f, l);
        }

        // ---------------------------------------------------------------- 콤보 (10 요약서 1.3절)

        [TestCase(0, 1f)]
        [TestCase(30, 0.75f)]
        [TestCase(60, 0.5f)]
        [TestCase(119, 1f / 120f)]
        [TestCase(120, 0f)]
        public void S01_Combo__BarRatio(int idle, float ratio)
        {
            Assert.AreEqual(ratio, HudModel.ComboBarRatio(idle), 1e-5f);
        }

        [TestCase(0, 15, false)]
        [TestCase(11, 15, false)]
        [TestCase(12, 15, true)]    // 남은 콤보 3
        [TestCase(14, 15, true)]
        [TestCase(15, 15, false)]   // 막 얻음 → 다음까지 15
        [TestCase(27, 15, true)]
        [TestCase(9, 12, true)]     // 제자 2: 12마다
        [TestCase(8, 12, false)]
        public void S01_Combo__NearCrystalWithin3(int combo, int per, bool near)
        {
            Assert.AreEqual(near, HudModel.ComboNearCrystal(combo, per));
        }

        [Test]
        public void S01_Combo__TierAndDigits()
        {
            Assert.AreEqual(0, HudModel.ComboTier(4));
            Assert.AreEqual(1, HudModel.ComboTier(5));
            Assert.AreEqual(1, HudModel.ComboTier(9));
            Assert.AreEqual(2, HudModel.ComboTier(10));
            var d = new int[4];
            Assert.AreEqual(3, HudModel.ComboDigits(127, d));
            Assert.AreEqual(new[] { 1, 2, 7 }, new[] { d[0], d[1], d[2] });
        }

        // ---------------------------------------------------------------- 결정 칸 (10 요약서 2.3절)

        [Test]
        public void S01_Crystal__SlotKindsAndDebt()
        {
            Assert.AreEqual(1, HudModel.CrystalSlotKind(0, 2, 0));
            Assert.AreEqual(1, HudModel.CrystalSlotKind(1, 2, 0));
            Assert.AreEqual(0, HudModel.CrystalSlotKind(2, 2, 0));
            // 빚 2(결정 0): 앞 두 칸이 보라 테 빈 칸
            Assert.AreEqual(2, HudModel.CrystalSlotKind(0, 0, 2));
            Assert.AreEqual(2, HudModel.CrystalSlotKind(1, 0, 2));
            Assert.AreEqual(0, HudModel.CrystalSlotKind(2, 0, 2));
            var p = new PlayerState { Def = new CharacterDef { CrystalCap = 7 } };
            Assert.AreEqual(7, HudModel.CrystalCells(p), "칸 수 = 상한");
        }

        // ---------------------------------------------------------------- 계약 벡터

        [Test]
        public void S13_Contract__ComboPositionTwoPlaces()
        {
            var o = new ViewOptions();
            Assert.AreEqual(OptionValues.NearCharacter, o.ComboPosition, "기본 = 캐릭터 옆");
            Assert.IsFalse(HudModel.ComboInHud(o));
            o.ComboPosition = OptionValues.Hud;
            Assert.IsTrue(HudModel.ComboInHud(o));
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 3);
            Assert.IsTrue(HudModel.Compute(w, null, null, o).ComboInHud);
            o.ComboPosition = OptionValues.NearCharacter;
            Assert.IsFalse(HudModel.Compute(w, null, null, o).ComboInHud);
            Assert.AreEqual(new Vector2(40, -72), HudLayout.ComboOffset);
        }

        static SimWorld CampWorld(Vector2 playerPos, CampSubState sub)
        {
            var w = HudTestWorlds.World(RunPhase.Camp, 4);
            w.Run.CampSub = sub;
            w.Run.CampFirePos = new Vector2(12, 7);
            w.Run.DepartMarkerPos = new Vector2(16, 7);
            w.Run.DepartMarkerRadius = 1f;
            w.P0.Pos = playerPos;
            return w;
        }

        [Test]
        public void S13_Contract__DepartHintOnlyInsideRadius()
        {
            Assert.IsTrue(HudModel.ShowDepartHint(CampWorld(new Vector2(16.5f, 7f), CampSubState.FireIdle)));
            Assert.IsTrue(HudModel.ShowDepartHint(CampWorld(new Vector2(16f, 8f), CampSubState.FireIdle)), "경계 포함");
            Assert.IsTrue(HudModel.ShowDepartHint(CampWorld(new Vector2(16f, 7f), CampSubState.DepartHold)));
            Assert.IsFalse(HudModel.ShowDepartHint(CampWorld(new Vector2(17.5f, 7f), CampSubState.FireIdle)), "반경 밖");
            Assert.IsFalse(HudModel.ShowDepartHint(CampWorld(new Vector2(12f, 7f), CampSubState.FireIdle)), "모닥불 옆");
            Assert.IsFalse(HudModel.ShowDepartHint(CampWorld(new Vector2(16f, 7f), CampSubState.CampScreen)), "야영 화면이 떠 있음");

            var inside = CampWorld(new Vector2(16.5f, 7f), CampSubState.FireIdle);
            Assert.AreEqual(HudStateNotice.DepartHint, HudModel.Compute(inside, null, null, new ViewOptions()).StateNotice);
            var outside = CampWorld(new Vector2(18f, 7f), CampSubState.FireIdle);
            Assert.AreEqual(HudStateNotice.None, HudModel.Compute(outside, null, null, new ViewOptions()).StateNotice);

            var combat = HudTestWorlds.World(RunPhase.WaveCombat, 4);
            combat.Run.DepartMarkerPos = combat.P0.Pos;
            combat.Run.DepartMarkerRadius = 1f;
            Assert.IsFalse(HudModel.ShowDepartHint(combat), "야영 밖");
        }

        [Test]
        public void S13_Contract__PracticeHidesWaveAndWallet()
        {
            var w = HudTestWorlds.World(RunPhase.Practice, 1);
            var f = HudModel.Compute(w, HudTestWorlds.Profile(true), null, new ViewOptions());
            Assert.IsTrue(f.InPractice);
            Assert.IsFalse(f.ShowWaveProgress);
            Assert.IsFalse(f.ShowDayWave);
            Assert.IsFalse(f.ShowElapsed);
            Assert.IsFalse(f.ShowWallet);
            Assert.AreEqual(0, f.RevengeWavesLeft);

            var c = HudTestWorlds.World(RunPhase.WaveCombat, 1);
            var fc = HudModel.Compute(c, null, null, new ViewOptions());
            Assert.IsTrue(fc.ShowWaveProgress);
            Assert.IsTrue(fc.ShowDayWave);
            Assert.IsTrue(fc.ShowWallet);
        }

        [Test]
        public void S13_HeatLockNoticeOutranksDepartHint()
        {
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 4);
            w.Tick = 300;
            w.P0.Locked = true;
            w.P0.LockStartTick = 200;
            w.P0.UnlockTick = 414;
            var f = HudModel.Compute(w, null, null, new ViewOptions());
            Assert.AreEqual(HudStateNotice.HeatLock, f.StateNotice);
            Assert.AreEqual(2, f.HeatLockSecondsLeft);
        }

        [Test]
        public void S13_WaveProgressPermilleAndElapsed()
        {
            Assert.AreEqual(1000, HudModel.WaveProgressPermille(1f));
            Assert.AreEqual(500, HudModel.WaveProgressPermille(0.5f));
            Assert.AreEqual(0, HudModel.WaveProgressPermille(-0.2f));
            Assert.AreEqual("1:05", HudModel.ElapsedText(65 * 60));
            var w = HudTestWorlds.World(RunPhase.WaveCombat, 3);
            w.Run.WaveResults.Add(new WaveResult { Wave = 1, Stats = new WaveStats { ElapsedTicks = 600 } });
            w.Run.WaveResults.Add(new WaveResult { Wave = 2, Stats = new WaveStats { ElapsedTicks = 900 } });
            w.Wave.WaveTick = 120;
            Assert.AreEqual(1620, HudModel.RunElapsedTicks(w));
            w.Run.WaveResults.Add(new WaveResult { Wave = 3, Stats = new WaveStats { ElapsedTicks = 120 } });
            Assert.AreEqual(1620, HudModel.RunElapsedTicks(w), "끝낸 웨이브의 시계를 두 번 세지 않는다");
        }

        [Test]
        public void S13_TuningReadsG17Fragment()
        {
            string path = Path.Combine(TestContent.SoloExtraDir, "G17.json");
            Assert.IsTrue(File.Exists(path), path);
            var t = HudTuning.FromJson(JObject.Parse(File.ReadAllText(path)));
            var d = HudTuning.Defaults();
            Assert.AreEqual(d.NoticeSeconds, t.NoticeSeconds);
            Assert.AreEqual(d.ComboHudPos, t.ComboHudPos);
            Assert.AreEqual(d.CrystalRowOffsetY, t.CrystalRowOffsetY);
            Assert.AreEqual(d.MasterAdvice.W, t.MasterAdvice.W);
            Assert.AreEqual(d.FacingArrowNormalAlpha, t.FacingArrowNormalAlpha);
        }

        [Test]
        public void S02_8_3__UnderlineSlotIsLastLinkSlot()
        {
            var p = new PlayerState();
            p.Loadout.Links = new SlotItem[5];
            Assert.AreEqual(-1, Wizard.Solo.View.Hud.HudCircuitPanel.UnderlineSlotOf(p), "밑줄 패시브가 없으면 없음");
            p.Derived.Underline = true;
            Assert.AreEqual(5, Wizard.Solo.View.Hud.HudCircuitPanel.UnderlineSlotOf(p), "칸 번호: 코어 0, 맨 끝 연결 칸 L");
            p.Loadout.Links = new SlotItem[6];
            Assert.AreEqual(6, Wizard.Solo.View.Hud.HudCircuitPanel.UnderlineSlotOf(p), "슬롯이 늘면 한 칸 뒤로");
        }
    }
}
