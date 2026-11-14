using System;
using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Audio;
using Wizard.Solo.Meta;
using Wizard.Solo.Save;

namespace Wizard.Solo.Tests.Save
{
    /// <summary>섞는 규칙(10 요약서 5.2절 = E 19.5)과 무음 재생기의 음량 따라가기.</summary>
    public class SoundMixTests
    {
        const float Eps = 1e-4f;

        static SoundMixPolicy Policy() => new SoundMixPolicy(SoundMixConfig.Spec());

        [Test]
        public void E19_5__SpecValues()
        {
            var c = SoundMixConfig.Spec();
            Assert.AreEqual(32, c.MaxVoices);
            Assert.AreEqual(8, c.HitPerSecond);
            Assert.AreEqual(6, c.KillPerSecond);
            Assert.AreEqual(2, c.TelegraphConcurrent);
            Assert.AreEqual(-6f, c.DuckDb);
            Assert.AreEqual(0.4, c.DuckSeconds);
            Assert.AreEqual(6f, c.MaxBoostDb);
        }

        // 동시 재생 전체 32개: 33번째(같은 우선순위)는 거절
        [Test]
        public void E19_5__ThirtyThirdVoiceRejected()
        {
            var p = Policy();
            for (int i = 0; i < 32; i++)
                Assert.IsTrue(p.Request("tod.transition", 1f, 0.001 * i).Accepted, "voice " + i);
            var d = p.Request("tod.transition", 1f, 0.05);
            Assert.IsFalse(d.Accepted);
            Assert.AreEqual(SoundRejectReason.VoicesFull, d.Reason);
            Assert.AreEqual(32, p.ActiveVoices(0.05));
        }

        // 적중 1초 8번: 9번째 거절, 창이 지나면 다시 받는다
        [Test]
        public void E19_5__NinthHitWithinOneSecondRejected()
        {
            var p = Policy();
            for (int i = 0; i < 8; i++)
                Assert.IsTrue(p.Request("hit.enemy", 1f, 0.1 * i).Accepted, "hit " + i);
            var d = p.Request("hit.enemy", 1f, 0.9);
            Assert.IsFalse(d.Accepted);
            Assert.AreEqual(SoundRejectReason.RateLimit, d.Reason);
            Assert.AreEqual(SoundCategory.Hit, d.Category);
            // 첫 적중(0.0초)이 창에서 빠지는 1.0초에 다시 된다
            Assert.IsTrue(p.Request("hit.enemy", 1f, 1.0).Accepted);
        }

        // 처치 1초 6번
        [Test]
        public void E19_5__SeventhKillWithinOneSecondRejected()
        {
            var p = Policy();
            for (int i = 0; i < 6; i++) Assert.IsTrue(p.Request("enemy.death", 1f, 0.1 * i).Accepted);
            Assert.AreEqual(SoundRejectReason.RateLimit, p.Request("enemy.death", 1f, 0.6).Reason);
            // 적중 횟수와 따로 센다
            Assert.IsTrue(p.Request("hit.enemy", 1f, 0.6).Accepted);
        }

        // 예고 음 동시 2개
        [Test]
        public void E19_5__TelegraphConcurrentTwo()
        {
            var p = Policy();
            Assert.IsTrue(p.Request("telegraph.start", 1f, 0.0).Accepted);
            Assert.IsTrue(p.Request("boss.telegraph_begin", 1f, 0.1).Accepted);
            var d = p.Request("telegraph.start", 1f, 0.2);
            Assert.IsFalse(d.Accepted);
            Assert.AreEqual(SoundRejectReason.TelegraphLimit, d.Reason);
            // 첫 예고 음이 끝나면(기본 길이 1초) 다시 된다
            Assert.IsTrue(p.Request("telegraph.start", 1f, 1.0).Accepted);
        }

        // 우선순위로 밀어내기: 가득 찬 32개 중 가장 낮고 오래된 것을 민다
        [Test]
        public void E19_5__HigherPriorityStealsLowestOldest()
        {
            var p = Policy();
            p.Request("enemy.death", 1f, 0.00);           // 처치(우선순위 1)
            for (int i = 1; i < 31; i++) p.Request("tod.transition", 1f, 0.001 * i);   // 환경(0) 30개
            p.Request("art.burst.fire", 1f, 0.04);        // 비기(3)
            Assert.AreEqual(32, p.ActiveVoices(0.05));

            var hit = p.Request("player.hit", 1f, 0.05);  // 내가 맞음(6)
            Assert.IsTrue(hit.Accepted);
            Assert.AreEqual("tod.transition", hit.StolenEventId);
            Assert.AreEqual(32, p.ActiveVoices(0.05));

            // 환경을 다 밀고 나면 처치가 밀린다. 비기는 적중(2)에게 밀리지 않는다.
            for (int i = 0; i < 29; i++) Assert.IsTrue(p.Request("graze.awarded", 1f, 0.06).Accepted);
            var h = p.Request("hit.enemy", 1f, 0.07);
            Assert.IsTrue(h.Accepted);
            Assert.AreEqual("enemy.death", h.StolenEventId);
            var h2 = p.Request("hit.enemy", 1f, 0.08);
            Assert.IsFalse(h2.Accepted);
            Assert.AreEqual(SoundRejectReason.VoicesFull, h2.Reason);
        }

        [Test]
        public void E19_5__PriorityOrder()
        {
            Assert.Greater(SoundMixPolicy.Priority(SoundCategory.PlayerHit), SoundMixPolicy.Priority(SoundCategory.Telegraph));
            Assert.Greater(SoundMixPolicy.Priority(SoundCategory.Telegraph), SoundMixPolicy.Priority(SoundCategory.GrazeHeat));
            Assert.Greater(SoundMixPolicy.Priority(SoundCategory.GrazeHeat), SoundMixPolicy.Priority(SoundCategory.Art));
            Assert.Greater(SoundMixPolicy.Priority(SoundCategory.Art), SoundMixPolicy.Priority(SoundCategory.Hit));
            Assert.Greater(SoundMixPolicy.Priority(SoundCategory.Hit), SoundMixPolicy.Priority(SoundCategory.Kill));
            Assert.Greater(SoundMixPolicy.Priority(SoundCategory.Kill), SoundMixPolicy.Priority(SoundCategory.Ambient));
            Assert.AreEqual(SoundCategory.PlayerHit, SoundEventClassifier.Classify("player.hit"));
            Assert.AreEqual(SoundCategory.Telegraph, SoundEventClassifier.Classify("telegraph.fill"));
            Assert.AreEqual(SoundCategory.GrazeHeat, SoundEventClassifier.Classify("heat.lock_entered"));
            Assert.AreEqual(SoundCategory.GrazeHeat, SoundEventClassifier.Classify("graze.awarded"));
            Assert.AreEqual(SoundCategory.Art, SoundEventClassifier.Classify("art.bolt_dash.fire"));
            Assert.AreEqual(SoundCategory.Hit, SoundEventClassifier.Classify("hit.weakness"));
            Assert.AreEqual(SoundCategory.Kill, SoundEventClassifier.Classify("enemy.death"));
            Assert.AreEqual(SoundCategory.Ambient, SoundEventClassifier.Classify("tod.transition"));
            Assert.AreEqual(SoundCategory.Interface, SoundEventClassifier.Classify("ui.common.cursor_move"));
            Assert.AreEqual(SoundCategory.Ambient, SoundEventClassifier.Classify(null));
        }

        // 누르기: 피격 뒤 0.4초 동안 다른 소리 −6dB, 피격 소리 자신은 누르지 않는다
        [Test]
        public void E19_5__DuckSixDbForPointFourSecondsAfterHit()
        {
            var p = Policy();
            float duck = (float)Math.Pow(10, -6.0 / 20.0);
            Assert.AreEqual(1f, p.Request("hit.enemy", 1f, 0.0).Gain, Eps);
            var me = p.Request("player.hit", 1f, 1.0);
            Assert.AreEqual(1f, me.Gain, Eps);
            Assert.IsFalse(me.Ducked);
            var during = p.Request("hit.enemy", 1f, 1.2);
            Assert.IsTrue(during.Ducked);
            Assert.AreEqual(duck, during.Gain, Eps);
            Assert.AreEqual(duck, p.DuckGain(1.39), Eps);
            var after = p.Request("hit.enemy", 1f, 1.4);
            Assert.IsFalse(after.Ducked);
            Assert.AreEqual(1f, after.Gain, Eps);
        }

        // 음량 한도: 평소보다 +6dB 넘게 크지 않다
        [Test]
        public void E19_5__VolumeCapPlusSixDb()
        {
            var p = Policy();
            float cap = (float)Math.Pow(10, 6.0 / 20.0);
            Assert.AreEqual(cap, p.Request("enhance.plus13.reached", 5f, 0.0).Gain, Eps);
            Assert.AreEqual(1.5f, p.Request("ui.common.confirm_check", 1.5f, 0.0).Gain, Eps);
            Assert.AreEqual(0f, p.Request("ui.common.confirm_check", -1f, 0.0).Gain, Eps);
        }

        [Test]
        public void E19_5__CutAllSilencesEveryVoice()
        {
            var p = Policy();
            for (int i = 0; i < 5; i++) p.Request("tod.transition", 1f, 0.0);
            p.CutAll();
            Assert.AreEqual(0, p.ActiveVoices(0.0));
        }

        [Test]
        public void Config__ReadsSoloExtraOverrides()
        {
            var c = SoundMixConfig.FromJson(JObject.Parse("{\"sound_mix\": {\"max_voices\": 4, \"duck_seconds\": 0.25}}"));
            Assert.AreEqual(4, c.MaxVoices);
            Assert.AreEqual(0.25, c.DuckSeconds, 1e-9);
            Assert.AreEqual(8, c.HitPerSecond);
            Assert.AreEqual(32, SoundMixConfig.FromJson(null).MaxVoices);
        }

        // ---------------------------------------------------------------- 무음 재생기

        sealed class ManualTime { public double T; }

        [Test]
        public void SilentPlayer__RemembersDecisionsAndMusic()
        {
            var t = new ManualTime();
            var player = new SilentSoundPlayer(null, () => t.T, SoundMixConfig.Spec(), 4);
            for (int i = 0; i < 9; i++) { t.T = 0.05 * i; player.Play("hit.enemy", SoundBus.Sfx); }
            var recent = player.Recent();
            Assert.AreEqual(4, recent.Count);   // 마지막 4건만
            Assert.IsTrue(recent[2].Accepted);
            Assert.IsFalse(recent[3].Accepted);  // 9번째
            Assert.AreEqual(SoundRejectReason.RateLimit, recent[3].Reason);
            Assert.AreEqual(0f, recent[3].FinalGain);
            Assert.AreEqual("hit.enemy", player.Last.Value.EventId);

            player.SetMusic("bgm.camp");
            Assert.AreEqual("bgm.camp", player.CurrentMusic);
            player.SetMusic(null);
            Assert.IsNull(player.CurrentMusic);
        }

        [Test]
        public void SilentPlayer__VolumeFollowsSettingsChanges()
        {
            string root = Path.Combine(Path.GetTempPath(), "wizard-solo-sound-" + Guid.NewGuid().ToString("N"));
            try
            {
                var settings = new SettingsStore(SavePaths.SettingsFile(root));
                settings.Load();
                var t = new ManualTime();
                var player = new SilentSoundPlayer(settings, () => t.T);
                Assert.AreEqual(1f, player.GetVolume(SoundBus.Sfx), Eps);

                SettingsOptions.Set(settings.Current, OptionKeys.VolSfx, 50);
                SettingsOptions.Set(settings.Current, OptionKeys.VolMaster, 80);
                SettingsOptions.Set(settings.Current, OptionKeys.VolEnemy, 0);
                Assert.AreEqual(1f, player.GetVolume(SoundBus.Sfx), Eps, "저장(Changed) 전에는 그대로");
                settings.Save();
                Assert.AreEqual(0.5f, player.GetVolume(SoundBus.Sfx), Eps);
                Assert.AreEqual(0.8f, player.GetVolume(SoundBus.Master), Eps);
                Assert.AreEqual(0f, player.GetVolume(SoundBus.Enemy), Eps);
                Assert.AreEqual(1f, player.GetVolume(SoundBus.Bgm), Eps);

                player.Play("hit.enemy", SoundBus.Sfx, 1f);
                Assert.AreEqual(0.4f, player.Last.Value.FinalGain, Eps);   // 1 × 0.5 × 0.8

                // 설정을 처음부터 읽은 새 재생기도 같은 음량
                var fresh = new SettingsStore(SavePaths.SettingsFile(root));
                fresh.Load();
                var p2 = new SilentSoundPlayer(fresh, () => 0.0);
                Assert.AreEqual(0.5f, p2.GetVolume(SoundBus.Sfx), Eps);

                player.Dispose();
                SettingsOptions.Set(settings.Current, OptionKeys.VolSfx, 10);
                settings.Save();
                Assert.AreEqual(0.5f, player.GetVolume(SoundBus.Sfx), Eps, "Dispose 뒤에는 따라가지 않는다");
            }
            finally
            {
                try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
            }
        }

        [Test]
        public void SilentPlayer__SetVolumeClamps()
        {
            var player = new SilentSoundPlayer(null, () => 0.0);
            player.SetVolume(SoundBus.Ui, 2f);
            Assert.AreEqual(1f, player.GetVolume(SoundBus.Ui));
            player.SetVolume(SoundBus.Ui, -1f);
            Assert.AreEqual(0f, player.GetVolume(SoundBus.Ui));
        }
    }
}
