using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo;
using Wizard.Solo.Audio;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.Tests.Audio
{
    /// <summary>실제 클립 재생기: 표(DataSolo/Audio.json)가 가리키는 파일, 캐릭터·단계 고르기, 연타 제한, 반복음 정지, 일시정지, 음량.</summary>
    public class AudioPlayerTests
    {
        sealed class FakeOutput : ISoundOutput
        {
            public sealed class Rec
            {
                public int Handle; public string Clip; public float Volume; public float Pitch; public bool Loop;
                public bool Stopped; public bool Paused; public float StopFade;
            }
            public readonly List<Rec> Started = new List<Rec>();
            int _next = 1;

            public Rec Of(int h) => Started.Find(r => r.Handle == h);
            public int StartVoice(AudioClip clip, float volume, float pitch, bool loop)
            {
                var r = new Rec { Handle = _next++, Clip = clip.name, Volume = volume, Pitch = pitch, Loop = loop };
                Started.Add(r);
                return r.Handle;
            }
            public void Stop(int handle, float fade) { var r = Of(handle); if (r != null) { r.Stopped = true; r.StopFade = fade; } }
            public void SetVolume(int handle, float v) { var r = Of(handle); if (r != null) r.Volume = v; }
            public void SetPaused(int handle, bool p) { var r = Of(handle); if (r != null) r.Paused = p; }
            public bool IsAlive(int handle) { var r = Of(handle); return r != null && !r.Stopped; }
            public void StopAll() { foreach (var r in Started) r.Stopped = true; }
        }

        sealed class Clock { public double T; }

        const string TestBank = @"{
  ""clip_root"": ""Audio"",
  ""mix"": { ""repeat_min_interval_seconds"": 0.05, ""repeat_window_seconds"": 0.3, ""repeat_pitch_jitter_semitones"": 0.5,
            ""cut_fade_seconds"": 0.02, ""stop_fade_seconds"": 0.08 },
  ""cut_sfx"": [""sfx_e10""],
  ""common"": { ""folder"": ""common"", ""events"": {}, ""sfx"": { ""sfx_h05"": ""kill"" } },
  ""characters"": {
    ""char.2"": { ""folder"": ""char.2"",
      ""events"": { ""ui.enhance.wand_idle"": { ""loop"": true, ""stages"": [[0, 3, ""idle0""], [4, 13, ""idle1""]] } },
      ""sfx"": { ""sfx_c02"": { ""stages"": [[0, 3, ""fire0""], [13, 13, ""fire4""]] }, ""sfx_h01"": ""hit"",
                 ""sfx_c13"": { ""scale"": [[""n0"", 0], [""n2"", 2], [""n4"", 4], [""n7"", 7], [""n9"", 9]] } } },
    ""char.3"": { ""folder"": ""char.3"", ""events"": {}, ""sfx"": { ""sfx_h01"": ""hit3"" } }
  }
}";

        static readonly Dictionary<string, string[]> Sfx = new Dictionary<string, string[]>
        {
            { "heat.shot_fired", new[] { "sfx_c01", "sfx_c02" } },
            { "hit.enemy", new[] { "sfx_h01" } },
            { "enemy.death", new[] { "sfx_h05" } },
            { "crystal.gained", new[] { "sfx_c13" } },
            { "ui.enhance.hold", new[] { "sfx_e10" } },
        };

        readonly List<AudioClip> _made = new List<AudioClip>();
        Clock _clock;
        FakeOutput _out;
        float _jitter;

        AudioClip Make(string path)
        {
            // 0.1초짜리 빈 클립(이름 = 경로의 마지막 조각).
            var c = AudioClip.Create(path.Substring(path.LastIndexOf('/') + 1), 4410, 1, 44100, false);
            _made.Add(c);
            return c;
        }

        ClipSoundPlayer NewPlayer(string bankJson = TestBank)
        {
            _clock = new Clock();
            _out = new FakeOutput();
            _jitter = 0f;
            return new ClipSoundPlayer(null, () => _clock.T, SoundMixConfig.Spec(), SoundBank.Parse(bankJson),
                id => { string[] s; return Sfx.TryGetValue(id, out s) ? s : null; }, Make, _out,
                n => 0, (a, b) => _jitter);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var c in _made) if (c != null) Object.DestroyImmediate(c);
            _made.Clear();
        }

        // ---- 데이터 ----

        [Test]
        public void AudioJson__EveryMappedClipExistsInResources()
        {
            var ta = Resources.Load<TextAsset>(SoundBank.Resource);
            Assert.IsNotNull(ta, "Resources/" + SoundBank.Resource + ".json");
            var bank = SoundBank.Parse(ta.text);
            var paths = bank.AllClipPaths();
            Assert.Greater(paths.Count, 50);
            var missing = new List<string>();
            foreach (var p in paths)
            {
                var clip = Resources.Load<AudioClip>(p);
                if (clip == null) missing.Add(p);
                else Resources.UnloadAsset(clip);
            }
            Assert.IsEmpty(missing, "missing clips: " + string.Join(", ", missing));
        }

        [Test]
        public void AudioJson__SfxKeysAreInBindingsCatalog()
        {
            var bank = Resources.Load<TextAsset>(SoundBank.Resource);
            var bind = Resources.Load<TextAsset>(PresentationData.BindingsResource);
            Assert.IsNotNull(bank);
            Assert.IsNotNull(bind);
            var b = EventBindings.Parse(bind.text);
            var root = JObject.Parse(bank.text);
            var bad = new List<string>();
            void Check(JObject table, string who)
            {
                var sfx = table["sfx"] as JObject;
                if (sfx != null) foreach (var p in sfx.Properties()) if (!b.InSfxCatalog(p.Name)) bad.Add(who + ":" + p.Name);
            }
            Check((JObject)root["common"], "common");
            foreach (var p in ((JObject)root["characters"]).Properties()) Check((JObject)p.Value, p.Name);
            Assert.IsEmpty(bad, string.Join(", ", bad));
        }

        [Test]
        public void AudioJson__CharacterFolderLoopFlagsAgree()
        {
            // char.2 audio.json의 loop 표시가 있는 클립 = Audio.json에서 loop로 쓰는 클립(반복음은 이것뿐이다).
            var ta = Resources.Load<TextAsset>("Audio/char.2/audio");
            Assert.IsNotNull(ta);
            var assets = (JObject)JObject.Parse(ta.text)["assets"];
            var bank = SoundBank.Parse(Resources.Load<TextAsset>(SoundBank.Resource).text);
            var into = new List<ResolvedClip>();
            for (int stage = 0; stage <= 13; stage++)
            {
                into.Clear();
                bank.Resolve("ui.enhance.wand_idle", null, "char.2", stage, 0f, n => 0, into);
                Assert.AreEqual(1, into.Count, "stage " + stage);
                Assert.IsTrue(into[0].Loop);
                string name = into[0].Path.Substring(into[0].Path.LastIndexOf('/') + 1);
                Assert.IsTrue((bool)assets[name]["loop"], name);
            }
        }

        [Test]
        public void RealBank__CharacterSoundsOnlyForThatCharacter()
        {
            var bank = SoundBank.Parse(Resources.Load<TextAsset>(SoundBank.Resource).text);
            var sfx = new[] { "sfx_c01", "sfx_c02" };
            var into = new List<ResolvedClip>();
            bank.Resolve("heat.shot_fired", sfx, "char.2", 13, 0f, n => 0, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("Audio/char.2/fire_band_4", into[0].Path);
            into.Clear();
            bank.Resolve("heat.shot_fired", sfx, "char.5", 0, 0f, n => 0, into);
            Assert.AreEqual("Audio/char.5/fire_knot", into[0].Path);
            // 제자 1 소리는 Tools/sfx/synth.py가 합성해 넣었다(예전엔 파일이 없어 무음이라고 단언했다). 이제 C-02 단계 덧소리가 나온다.
            into.Clear();
            bank.Resolve("heat.shot_fired", sfx, "char.1", 0, 0f, n => 0, into);
            Assert.AreEqual(1, into.Count);
            Assert.AreEqual("Audio/char.1/fire_clank", into[0].Path);
            into.Clear();
            bank.Resolve("heat.shot_fired", sfx, "char.1", 13, 0f, n => 0, into);
            Assert.AreEqual("Audio/char.1/fire_boom_bell", into[0].Path);
            into.Clear();
            bank.Resolve("art.bolt_dash.fire", new[] { "sfx_t12" }, "char.3", 0, 0f, n => 0, into);
            Assert.AreEqual(0, into.Count, "char.2's bolt dash sound must not play for char.3");
        }

        [Test]
        public void RealBank__CommonClipsAndSoloEvents()
        {
            var bank = SoundBank.Parse(Resources.Load<TextAsset>(SoundBank.Resource).text);
            var into = new List<ResolvedClip>();
            // 공용 화면 소리는 사건 id로(bindings에 sfx가 비어 있다)
            bank.Resolve("ui.common.press", null, "char.3", 0, 0f, n => 0, into);
            Assert.AreEqual("Audio/common/ui_press", into[0].Path);
            // 발사 블록음(C-01)은 캐릭터와 상관없이 공용
            into.Clear();
            bank.Resolve("heat.shot_block.fireball", null, "char.1", 0, 0f, n => 1, into);
            Assert.AreEqual("Audio/common/fire_block_fireball_2", into[0].Path);
            // 캐릭터 sfx가 없으면 공용 sfx로 떨어진다(char.2에는 처치음이 없다)
            into.Clear();
            bank.Resolve("enemy.death", new[] { "sfx_h05" }, "char.2", 0, 0f, n => 0, into);
            Assert.AreEqual("Audio/common/kill_1", into[0].Path);
            // 캐릭터 표가 공용 폴더의 클립을 가리킨다(+13 속성 선율 = 제자 번호)
            into.Clear();
            bank.Resolve("ui.enhance.plus13_theme", new[] { "sfx_e30" }, "char.4", 13, 0f, n => 0, into);
            Assert.AreEqual("Audio/common/enh_plus13_theme_4", into[0].Path);
            // 불가마는 제자 1만
            into.Clear();
            bank.Resolve("art.kiln.place", new[] { "sfx_t11" }, "char.1", 0, 0f, n => 0, into);
            CollectionAssert.AreEqual(new[] { "Audio/char.1/kiln_place", "Audio/char.1/kiln_hum" }, into.ConvertAll(c => c.Path));
            into.Clear();
            bank.Resolve("art.kiln.place", new[] { "sfx_t11" }, "char.2", 0, 0f, n => 0, into);
            Assert.AreEqual(0, into.Count);
        }

        [Test]
        public void Bank__SlashNameIsUnderClipRootAndCycleRotates()
        {
            var bank = SoundBank.Parse(@"{ ""clip_root"": ""Audio"",
  ""common"": { ""folder"": ""common"", ""events"": {}, ""sfx"": { ""sfx_e18"": { ""cycle"": [""a"", ""b"", ""c""] } } },
  ""characters"": { ""char.2"": { ""folder"": ""char.2"", ""events"": {}, ""sfx"": { ""sfx_e36"": ""common/mark"" } } } }");
            var into = new List<ResolvedClip>();
            bank.Resolve("x", new[] { "sfx_e36" }, "char.2", 0, 0f, n => 0, into);
            Assert.AreEqual("Audio/common/mark", into[0].Path);
            var got = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                into.Clear();
                bank.Resolve("x", new[] { "sfx_e18" }, "char.2", 0, 0f, n => 0, into);
                got.Add(into[0].Path);
            }
            CollectionAssert.AreEqual(new[] { "Audio/common/a", "Audio/common/b", "Audio/common/c", "Audio/common/a" }, got);
            CollectionAssert.Contains(bank.AllClipPaths(), "Audio/common/mark");
        }

        [Test]
        public void SoundEventMap__SoloEventsWaveDropBlockKillGroup()
        {
            // 원문 bindings에서 sfx가 빈 사건도 G16 sound.solo_events면 낸다(웨이브 시작·클리어, 줍기, 발사 블록, 처치 음색 묶음).
            var data = Wizard.Solo.Tests.ViewEffects.ViewEffectsTestData.Shared;
            var m = new SoundEventMap(data.Bindings, data.Tuning, (a, b) => 0f);
            var l = new List<SoundCue>();
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.WaveStarted, A = 3 }, l);
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.WaveCleared, A = 3 }, l);
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.DropPicked, A = (int)DropKind.GoldCoin }, l);
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.DropPicked, A = (int)DropKind.HealthShard }, l);
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.DropPicked, A = (int)DropKind.MaterialBundle }, l);
            CollectionAssert.AreEqual(new[] { "wave.started", "wave.cleared", "drop.gold_collected", "drop.hp_shard_picked", "drop.material_bundle_picked" },
                l.ConvertAll(c => c.Id));

            l.Clear();
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.ProjectileSpawned, Slot = 0, Tick = 50, B = 0, Id = Ids.Block.Fireball }, l);
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.ShotFired, Slot = 0, Tick = 50 }, l);
            CollectionAssert.AreEqual(new[] { "heat.shot_fired", "heat.shot_block.fireball" }, l.ConvertAll(c => c.Id));
            Assert.AreEqual(SoundBus.Wand, l[1].Bus);
            l.Clear();
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.ShotFired, Slot = 0, Tick = 51 }, l);
            Assert.AreEqual(1, l.Count, "no spawn this tick = no block sound");

            l.Clear();
            m.Map(null, new Wizard.Solo.Sim.SimEvent { Type = Wizard.Solo.Sim.SimEventType.EnemyKilled, Tick = 10, B = 0, Id = Ids.Enemy.GolemMenhir }, l);
            Assert.AreEqual("enemy.death.stone", l[0].Id);
            Assert.AreEqual(SoundBus.Enemy, l[0].Bus);
            Assert.AreEqual(SoundCategory.Kill, SoundEventClassifier.Classify(l[0].Id));
        }

        // ---- 고르기 ----

        [Test]
        public void Resolve__StageAndScale()
        {
            var p = NewPlayer();
            p.SetContext("char.2", 2);
            p.Play("heat.shot_fired", SoundBus.Wand);
            Assert.AreEqual("fire0", _out.Started[0].Clip);
            p.SetContext("char.2", 7); // 표에 없는 단계 = 소리 없음
            p.Play("heat.shot_fired", SoundBus.Wand);
            Assert.AreEqual(1, _out.Started.Count);
            // 5음 음계 7반음 위 = n7 그대로(음높이 1), 8반음 = n7 + 1반음
            p.Play("crystal.gained", SoundBus.Sfx, 1f, Mathf.Pow(2f, 7f / 12f));
            Assert.AreEqual("n7", _out.Started[1].Clip);
            Assert.AreEqual(1f, _out.Started[1].Pitch, 1e-3f);
            _clock.T = 1;
            p.Play("crystal.gained", SoundBus.Sfx, 1f, Mathf.Pow(2f, 8f / 12f));
            Assert.AreEqual("n7", _out.Started[2].Clip);
            Assert.AreEqual(Mathf.Pow(2f, 1f / 12f), _out.Started[2].Pitch, 1e-3f);
            // 한 옥타브 위 2반음 = n2 × 2
            _clock.T = 2;
            p.Play("crystal.gained", SoundBus.Sfx, 1f, Mathf.Pow(2f, 14f / 12f));
            Assert.AreEqual("n2", _out.Started[3].Clip);
            Assert.AreEqual(2f, _out.Started[3].Pitch, 1e-3f);
        }

        [Test]
        public void Resolve__CommonFallbackAndUnmappedIsSilent()
        {
            var p = NewPlayer();
            p.SetContext("char.3", 0);
            p.Play("enemy.death");
            Assert.AreEqual("kill", _out.Started[0].Clip);
            p.Play("no.such.event");
            p.Play("hit.enemy");
            Assert.AreEqual("hit3", _out.Started[1].Clip);
            p.SetContext("char.1", 0);
            _clock.T = 1;
            p.Play("hit.enemy");
            Assert.AreEqual(2, _out.Started.Count);
            CollectionAssert.Contains(new List<string>(p.UnmappedEvents), "no.such.event");
        }

        // ---- 연타 제한 ----

        [Test]
        public void Repeat__SameClipWithinMinIntervalIsDropped()
        {
            var p = NewPlayer();
            p.SetContext("char.2", 0);
            p.Play("hit.enemy");
            _clock.T = 0.03; // 0.05초 안
            p.Play("hit.enemy");
            Assert.AreEqual(1, _out.Started.Count);
            _clock.T = 0.051;
            p.Play("hit.enemy");
            Assert.AreEqual(2, _out.Started.Count);
        }

        [Test]
        public void Repeat__RepeatsInWindowGetPitchJitterButFirstAndUiDoNot()
        {
            var p = NewPlayer();
            p.SetContext("char.2", 0);
            p.Play("hit.enemy");
            Assert.AreEqual(1f, _out.Started[0].Pitch, 1e-4f, "first play is not jittered");
            _jitter = 0.5f;
            _clock.T = 0.1;
            p.Play("hit.enemy");
            Assert.AreEqual(Mathf.Pow(2f, 0.5f / 12f), _out.Started[1].Pitch, 1e-4f);
            _clock.T = 1.0; // 창(0.3초) 밖
            p.Play("hit.enemy");
            Assert.AreEqual(1f, _out.Started[2].Pitch, 1e-4f);
            _clock.T = 1.1;
            p.Play("hit.enemy", SoundBus.Ui);
            Assert.AreEqual(1f, _out.Started[3].Pitch, 1e-4f, "UI sounds are not jittered");
        }

        [Test]
        public void Repeat__MixPolicyRateLimitStillApplies()
        {
            // 적중 1초 8번(10 요약서 5.2절): 연타 간격을 넘겨도 9번째는 섞는 규칙이 버린다.
            var p = NewPlayer();
            p.SetContext("char.2", 0);
            for (int i = 0; i < 9; i++) { _clock.T = 0.06 * i; p.Play("hit.enemy"); }
            Assert.AreEqual(8, _out.Started.Count);
        }

        // ---- 반복음 ----

        [Test]
        public void Loop__StartStopAndSameClipNotRestarted()
        {
            var p = NewPlayer();
            p.SetContext("char.2", 0);
            p.StartLoop("idle", "ui.enhance.wand_idle", SoundBus.Wand);
            Assert.AreEqual(1, _out.Started.Count);
            Assert.IsTrue(_out.Started[0].Loop);
            Assert.AreEqual("idle0", _out.Started[0].Clip);
            p.StartLoop("idle", "ui.enhance.wand_idle", SoundBus.Wand);
            Assert.AreEqual(1, _out.Started.Count, "same clip keeps playing");
            p.SetContext("char.2", 5);
            p.StartLoop("idle", "ui.enhance.wand_idle", SoundBus.Wand);
            Assert.AreEqual(2, _out.Started.Count);
            Assert.IsTrue(_out.Started[0].Stopped, "old band loop stopped");
            Assert.AreEqual("idle1", _out.Started[1].Clip);
            p.StopLoop("idle");
            Assert.IsTrue(_out.Started[1].Stopped);
            Assert.IsFalse(p.IsLoopPlaying("idle"));
        }

        [Test]
        public void Loop__NoLoopClipForCharacterStopsLoop()
        {
            var p = NewPlayer();
            p.SetContext("char.2", 0);
            p.StartLoop("idle", "ui.enhance.wand_idle", SoundBus.Wand);
            p.SetContext("char.3", 0);
            p.StartLoop("idle", "ui.enhance.wand_idle", SoundBus.Wand);
            Assert.IsTrue(_out.Started[0].Stopped);
            Assert.IsFalse(p.IsLoopPlaying("idle"));
        }

        [Test]
        public void Loop__BreathHoldCutStopsEverythingQuickly()
        {
            // E-10 숨 멈춤 = 모든 소리를 0.02초에 끊는다(반복음 포함).
            var p = NewPlayer();
            p.SetContext("char.2", 0);
            p.StartLoop("idle", "ui.enhance.wand_idle", SoundBus.Wand);
            p.Play("hit.enemy");
            p.Play("ui.enhance.hold", SoundBus.Ui);
            Assert.IsTrue(_out.Started[0].Stopped);
            Assert.IsTrue(_out.Started[1].Stopped);
            Assert.AreEqual(0.02f, _out.Started[0].StopFade, 1e-6f);
            Assert.AreEqual(2, _out.Started.Count, "the hold event itself plays nothing");
            Assert.IsFalse(p.IsLoopPlaying("idle"));
        }

        [Test]
        public void Pause__GameBusesPauseAndResumeUiKeepsPlaying()
        {
            var p = NewPlayer();
            p.SetContext("char.2", 0);
            p.StartLoop("idle", "ui.enhance.wand_idle", SoundBus.Wand);
            p.Play("enemy.death", SoundBus.Ui);
            p.Tick(true);
            Assert.IsTrue(_out.Started[0].Paused);
            Assert.IsFalse(_out.Started[1].Paused);
            p.Tick(false);
            Assert.IsFalse(_out.Started[0].Paused);
            Assert.IsFalse(_out.Started[0].Stopped);
        }

        // ---- 음량 ----

        [Test]
        public void Volume__BusAndMasterMultiplyAndFollowChanges()
        {
            var p = NewPlayer();
            p.SetContext("char.2", 0);
            p.SetVolume(SoundBus.Wand, 0.5f);
            p.SetVolume(SoundBus.Master, 0.5f);
            p.Play("heat.shot_fired", SoundBus.Wand);
            Assert.AreEqual(0.25f, _out.Started[0].Volume, 1e-4f);
            p.SetVolume(SoundBus.Wand, 1f);
            Assert.AreEqual(0.5f, _out.Started[0].Volume, 1e-4f, "playing sounds follow the setting");
            p.SetVolume(SoundBus.Master, 0f);
            Assert.AreEqual(0f, _out.Started[0].Volume, 1e-4f);
        }
    }
}
