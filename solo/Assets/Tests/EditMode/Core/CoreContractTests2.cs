using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using Wizard.Solo.View;

namespace Wizard.Solo.Tests.Core
{
    /// <summary>공용 계약에 든 순수 함수(복수 추적기, 설정 옵션 표, 화면 배율)와 기본 모듈의 약속을 확인한다.</summary>
    public class CoreContractTests2
    {
        static ProfileData Profile(bool met, string doneAt, int bestWave, int tries, int dent)
        {
            var p = new ProfileData();
            p.Flags.PhantomMet = met;
            p.Flags.RevengeDoneAt = doneAt;
            p.Characters.Add(new CharacterRecord { Id = 1, BestWave = bestWave });
            p.Characters.Add(new CharacterRecord { Id = 2, BestWave = 3 });
            p.Revenge.PhantomTries = tries;
            p.Revenge.PhantomBestDent = dent;
            return p;
        }

        // 08 요약서 6.1절 TV-043: 복수 중 웨이브 14 → 6
        [Test]
        public void S13_TV_043__HudWavesLeftAtWave14()
        {
            var v = RevengeTracker.Build(Profile(true, null, 14, 0, 0), 50, 14);
            Assert.AreEqual(6, v.HudWavesLeft);
        }

        // TV-044: 10 / 19 / 20 → 없음 / 1 / 없음
        [TestCase(10, 0)]
        [TestCase(19, 1)]
        [TestCase(20, 0)]
        public void S13_TV_044__HudWavesLeftEdges(int wave, int expected)
        {
            var v = RevengeTracker.Build(Profile(true, null, 14, 0, 0), 50, wave);
            Assert.AreEqual(expected, v.HudWavesLeft);
        }

        // TV-045: 만나기 전 → 없음(어디에도 보이지 않는다)
        [Test]
        public void S13_TV_045__NotMetHasNoTracker()
        {
            var v = RevengeTracker.Build(Profile(false, null, 14, 0, 0), 50, 14);
            Assert.AreEqual(RevengeState.BeforeMeeting, v.State);
            Assert.IsFalse(v.Visible);
            Assert.AreEqual(0, v.HudWavesLeft);
        }

        // 05 요약서 10.4절: 진행 n = min(19, 전 캐릭터 최고 웨이브), 한 번도 못 닿았으면 흠집 줄 없음, 50% 이상이면 금 간 거울
        [Test]
        public void S06_RevengeTracker__ProgressCapDentAndMirror()
        {
            var v = RevengeTracker.Build(Profile(true, null, 34, 0, 0), 50);
            Assert.IsTrue(v.Visible);
            Assert.AreEqual(19, v.Progress);
            Assert.AreEqual(19, v.ProgressMax);
            Assert.IsFalse(v.HasDent);
            Assert.AreEqual(MirrorMark.Intact, v.Mirror);

            v = RevengeTracker.Build(Profile(true, null, 14, 2, 62), 50);
            Assert.AreEqual(14, v.Progress);
            Assert.IsTrue(v.HasDent);
            Assert.AreEqual(62, v.DentPercent);
            Assert.AreEqual(MirrorMark.Cracked, v.Mirror);

            v = RevengeTracker.Build(Profile(true, null, 14, 1, 49), 50);
            Assert.AreEqual(MirrorMark.Intact, v.Mirror);
        }

        // 05 요약서 10.3절: 판 안에서 첫 만남을 치르면 그 뒤로 복수 중. 완료는 정산에서 한 번만 보인다.
        [Test]
        public void S06_RevengeTracker__MetThisRunAndCompletedOnce()
        {
            var v = RevengeTracker.Build(Profile(false, null, 10, 0, 0), 50, 14, metThisRun: true);
            Assert.AreEqual(RevengeState.Pursuing, v.State);
            Assert.IsTrue(v.Visible);
            Assert.AreEqual(6, v.HudWavesLeft);

            var done = Profile(true, "2026-11-14", 20, 3, 100);
            done.Revenge.RunsSinceFirstMeet = 7;
            v = RevengeTracker.Build(done, 50);
            Assert.AreEqual(RevengeState.Done, v.State);
            Assert.IsFalse(v.Visible);
            Assert.AreEqual(MirrorMark.Broken, v.Mirror);
            Assert.AreEqual(7, v.RunsToComplete);
            v = RevengeTracker.Build(done, 50, completedThisRun: true);
            Assert.IsTrue(v.Visible);
            Assert.IsTrue(v.JustCompleted);
        }

        // 08 요약서 12.2절 TV-170: 내 탄 투명도 29 → 30으로 고정. TV-168: 기본값 모음.
        [Test]
        public void S13_TV_170__MyBulletOpacityClampsTo30()
        {
            var s = new SettingsData();
            SettingsOptions.Set(s, OptionKeys.MyBulletOpacity, 29);
            Assert.AreEqual(30, SettingsOptions.Int(s, OptionKeys.MyBulletOpacity));
            SettingsOptions.Set(s, OptionKeys.WandFxOpacity, 19);
            Assert.AreEqual(20, SettingsOptions.Int(s, OptionKeys.WandFxOpacity));
            SettingsOptions.Set(s, OptionKeys.TelegraphFill, 84);
            Assert.AreEqual(80, SettingsOptions.Int(s, OptionKeys.TelegraphFill));
            s.Options[OptionKeys.CloneZoneOpacity] = new JValue(5);
            Assert.AreEqual(30, SettingsOptions.Int(s, OptionKeys.CloneZoneOpacity));
        }

        [Test]
        public void S13_TV_168__OptionDefaults()
        {
            var s = new SettingsData();
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.MyBulletOpacity));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.DamageNumberOpacity));
            Assert.AreEqual(OptionValues.All, SettingsOptions.Str(s, OptionKeys.DamageNumberMode));
            Assert.AreEqual(OptionValues.Full, SettingsOptions.Str(s, OptionKeys.MotionPreset));
            Assert.AreEqual(0, SettingsOptions.EffectsLevel(s));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.ShakeScale));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.FlashScale));
            Assert.IsTrue(SettingsOptions.Bool(s, OptionKeys.HitstopEnabled));
            Assert.AreEqual(OptionValues.All, SettingsOptions.Str(s, OptionKeys.WandFxLevel));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.WandFxOpacity));
            Assert.AreEqual(OptionValues.On, SettingsOptions.Str(s, OptionKeys.BulletTrail));
            Assert.IsTrue(SettingsOptions.Bool(s, OptionKeys.GroundMarks));
            Assert.AreEqual(OptionValues.On, SettingsOptions.Str(s, OptionKeys.KillShards));
            Assert.AreEqual(OptionValues.On, SettingsOptions.Str(s, OptionKeys.ArtNamePopup));
            Assert.AreEqual(OptionValues.NearCharacter, SettingsOptions.Str(s, OptionKeys.ComboPosition));
            Assert.AreEqual(100, SettingsOptions.Int(s, OptionKeys.CloneZoneOpacity));
            Assert.IsTrue(SettingsOptions.Bool(s, OptionKeys.WeatherParticles));
            Assert.AreEqual(70, SettingsOptions.Int(s, OptionKeys.TelegraphFill));
            Assert.IsFalse(SettingsOptions.Bool(s, OptionKeys.ColorblindAssist));
            Assert.AreEqual(OptionValues.Full, SettingsOptions.Str(s, OptionKeys.EnhanceRitual));
            Assert.AreEqual(1f, SettingsOptions.Volume(s, SoundBus.Master), 0.0001f);

            var vo = new ViewOptions();
            vo.ReadFrom(s);
            Assert.AreEqual(70, vo.TelegraphFillPct);
            Assert.AreEqual(1f, vo.MyBulletOpacity, 0.0001f);
            Assert.AreEqual(OptionValues.NearCharacter, vo.ComboPosition);
        }

        [Test]
        public void SettingsOptions_RejectsUnknownChoiceAndResetsCategory()
        {
            var s = new SettingsData();
            SettingsOptions.Set(s, OptionKeys.BulletTrail, "sparkly");
            Assert.AreEqual(OptionValues.On, SettingsOptions.Str(s, OptionKeys.BulletTrail));
            SettingsOptions.Set(s, OptionKeys.BulletTrail, OptionValues.Short);
            SettingsOptions.SetEffectsLevel(s, 2);
            Assert.AreEqual(OptionValues.Short, SettingsOptions.Str(s, OptionKeys.BulletTrail));
            Assert.AreEqual(2, SettingsOptions.EffectsLevel(s));
            SettingsOptions.ResetCategory(s, SettingsOptions.CatCombatView);
            Assert.AreEqual(OptionValues.On, SettingsOptions.Str(s, OptionKeys.BulletTrail));
            Assert.AreEqual(2, SettingsOptions.EffectsLevel(s));
        }

        // UI 배율: 1920×1080 안전 영역이 창에 꽉 들어가는 배율(정수로 내리지 않는다 — 큰 창에서 UI가 작게 뜨지 않게).
        [Test]
        public void UiScale_FitsSafeAreaToWindow()
        {
            Assert.AreEqual(1f, UiScale.Compute(1920, 1080));
            Assert.AreEqual(2560f / 1920f, UiScale.Compute(2560, 1440), 0.0001f);
            Assert.AreEqual(2f, UiScale.Compute(3840, 2160));
            Assert.AreEqual(3024f / 1920f, UiScale.Compute(3024, 1964), 0.0001f); // 맥 레티나(세로가 남는 창)는 너비에 맞춘다
            Assert.AreEqual(1f, UiScale.Compute(2560, 1080));                     // 21:9는 높이에 맞추고 좌우는 바탕이 채운다
            Assert.AreEqual(1280f / 1920f, UiScale.Compute(1280, 720), 0.0001f);
            Assert.AreEqual(8.4375f, UiScale.OrthographicSize(1080, 1f), 0.0001f);
            Assert.AreEqual(8.4375f, UiScale.OrthographicSize(2160, 2f), 0.0001f);
            Assert.AreEqual(100, UiSortOrders.Screen(ScreenLayer.Fullscreen));
            Assert.Less(UiSortOrders.Hud, UiSortOrders.Screen(ScreenLayer.Fullscreen));
        }

        [Test]
        public void BaseWaveModule_BeginWaveFillsStatsFromPlan()
        {
            var w = new SimWorld(null, SimModuleSet.Stubs(), 3);
            var plan = new WavePlan { Wave = 20, BossId = Ids.Boss.MirrorPhantom };
            plan.Whims.Add(Ids.Whim.Crowding);
            w.Mods.Waves.BeginWave(w, plan);
            Assert.AreEqual(20, w.Wave.Stats.Wave);
            Assert.IsTrue(w.Wave.Stats.IsBoss);
            Assert.AreEqual(1, w.Wave.Stats.Whims.Count);
        }

        [Test]
        public void SimWorld_AttackAllowedAndCampControl()
        {
            var w = new SimWorld(null, SimModuleSet.Stubs(), 3);
            var p = w.Players[0];
            p.Active = true;
            w.Run.Phase = RunPhase.WaveCombat;
            Assert.IsTrue(w.AttackAllowed(p));
            p.AttackLocked = true;
            Assert.IsFalse(w.AttackAllowed(p));
            p.AttackLocked = false;
            w.Run.Phase = RunPhase.StartCamp;
            Assert.IsFalse(w.AttackAllowed(p));
            Assert.IsTrue(w.InCamp);
            Assert.IsTrue(w.PlayerControlActive);
            w.Run.CampSub = CampSubState.CampScreen;
            Assert.IsFalse(w.PlayerControlActive);
        }

        [Test]
        public void BaseBossModule_DismissClearsBoss()
        {
            var w = new SimWorld(null, SimModuleSet.Stubs(), 3);
            w.Boss = new BossState();
            w.Mods.Bosses.Dismiss(w);
            Assert.IsNull(w.Boss);
        }

        [Test]
        public void EnemyState_PendingDeathIsNotAliveNorTargetable()
        {
            var e = new EnemyState();
            Assert.IsTrue(e.Alive);
            Assert.IsTrue(e.Targetable);
            e.Burrowed = true;
            Assert.IsTrue(e.Alive);
            Assert.IsFalse(e.Targetable);
            e.Burrowed = false;
            e.PendingDeath = true;
            Assert.IsFalse(e.Alive);
            Assert.IsFalse(e.Targetable);
        }

        [Test]
        public void ScreenArgs_ViaLoadingCarriesNextScreen()
        {
            var cfg = new RunConfig { CharId = Ids.Char.C1 };
            var next = ScreenArgs.From(ScreenId.ExpeditionPrep).With(ScreenArgKeys.RunConfig, cfg);
            var args = ScreenArgs.ViaLoading(ScreenId.ExpeditionPrep, ScreenId.Battle, next);
            Assert.AreEqual(ScreenId.Battle, args.Get(ScreenArgKeys.NextScreen, ScreenId.None));
            Assert.AreSame(cfg, args.Get<ScreenArgs>(ScreenArgKeys.NextArgs).Get<RunConfig>(ScreenArgKeys.RunConfig));
            Assert.AreEqual("enemy.slime_moss|weak", AffinityKey.Of(Ids.Enemy.SlimeMoss, Affinity.Weak));
        }
    }
}
