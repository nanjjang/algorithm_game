using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Audio
{
    /// <summary>
    /// 실제 클립 재생기(10 요약서 5절, D-72·D-76). 사건 id → event_bindings의 sfx 묶음 → SoundBank(Audio.json)로 클립을 고르고
    /// 섞는 규칙(SoundMixPolicy: 동시 32, 초당 횟수, 우선순위 밀어내기, 누르기, +6dB 한도)을 거쳐 출력(ISoundOutput)에 낸다.
    /// - 같은 클립 연타: SoundRepeatGate(최소 간격 안이면 버림, 짧은 창 안의 되풀이는 음높이를 조금 흩음 — 화면 소리는 흩지 않음).
    /// - 반복음: StartLoop/StopLoop(열쇠별 하나). 섞는 규칙의 보이스 수에는 넣지 않는다(풀에 여유 보이스를 둔다).
    /// - 일시정지(판): 내 소리·지팡이·적 묶음의 소리를 멈춰 두고 풀릴 때 잇는다. 화면·배경음은 그대로.
    /// - E-10 숨 멈춤(Audio.json cut_sfx): 배경음 말고 모든 소리를 cut_fade 안에 끊는다(반복음 포함).
    /// - 클립 파일이 없는 사건은 조용히 넘어간다. 표가 가리키는데 파일이 없으면 경로마다 경고 한 번, 표에 없는 사건은 처음 한 번만 알린다.
    /// </summary>
    public sealed class ClipSoundPlayer : ISoundPlayer, ISoundControl, IDisposable
    {
        static readonly SoundBus[] AllBuses =
            { SoundBus.Master, SoundBus.Bgm, SoundBus.Sfx, SoundBus.Wand, SoundBus.Ui, SoundBus.Enemy };

        sealed class Voice
        {
            public int Handle;
            public SoundBus Bus;
            public SoundCategory Category;
            public float Base;
            public string Path;
            public string LoopKey;
            public bool Music;
            public bool Paused;
        }

        readonly ISettingsStore _settings;
        readonly Func<double> _now;
        readonly SoundMixPolicy _policy;
        readonly SoundBank _bank;
        readonly Func<string, IReadOnlyList<string>> _sfxOf;
        readonly Func<string, AudioClip> _load;
        readonly ISoundOutput _out;
        readonly Func<int, int> _rndInt;
        readonly Func<float, float, float> _rndRange;
        readonly SoundRepeatGate _gate;
        readonly float[] _volumes = new float[AllBuses.Length];
        readonly List<Voice> _voices = new List<Voice>();
        readonly int[] _policyHandle;
        readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();
        readonly HashSet<string> _warnedMissing = new HashSet<string>();
        readonly HashSet<string> _unmapped = new HashSet<string>();
        readonly List<ResolvedClip> _clips = new List<ResolvedClip>();
        bool _gamePaused;
        string _character;
        int _stage;
        string _music;
        Voice _musicVoice;

        /// <summary>테스트·디버그: 표에 없어서(또는 파일이 없어서) 소리가 나지 않은 사건 id.</summary>
        public IEnumerable<string> UnmappedEvents => _unmapped;
        public SoundMixPolicy Policy => _policy;
        public SoundBank Bank => _bank;
        public string Character => _character;
        public int Stage => _stage;
        public string CurrentMusic => _music;
        public bool GamePaused => _gamePaused;
        public int ActiveVoiceCount => _voices.Count;

        /// <param name="sfxOf">사건 id → event_bindings.json의 sfx 묶음(없으면 null).</param>
        /// <param name="load">Resources 경로 → 클립(없으면 null). 이 객체가 결과를 캐시한다.</param>
        /// <param name="rndInt">0~n−1 표시용 난수(null이면 UnityEngine.Random).</param>
        /// <param name="rndRange">(최소, 최대) 표시용 난수(null이면 UnityEngine.Random.Range).</param>
        public ClipSoundPlayer(ISettingsStore settings, Func<double> now, SoundMixConfig mix, SoundBank bank,
            Func<string, IReadOnlyList<string>> sfxOf, Func<string, AudioClip> load, ISoundOutput output,
            Func<int, int> rndInt = null, Func<float, float, float> rndRange = null)
        {
            _settings = settings;
            _now = now ?? (() => 0.0);
            _policy = new SoundMixPolicy(mix);
            _bank = bank ?? new SoundBank();
            _sfxOf = sfxOf ?? (_ => null);
            _load = load ?? (_ => null);
            _out = output;
            _rndInt = rndInt ?? (n => UnityEngine.Random.Range(0, n));
            _rndRange = rndRange ?? ((a, b) => UnityEngine.Random.Range(a, b));
            _gate = new SoundRepeatGate(_bank.RepeatMinIntervalSeconds, _bank.RepeatWindowSeconds);
            _policyHandle = new int[Math.Max(1, _policy.Config.MaxVoices)];
            for (int i = 0; i < _policyHandle.Length; i++) _policyHandle[i] = -1;
            for (int i = 0; i < _volumes.Length; i++) _volumes[i] = 1f;
            ApplySettings();
            if (_settings != null) _settings.Changed += ApplySettings;
        }

        public void Dispose()
        {
            if (_settings != null) _settings.Changed -= ApplySettings;
            if (_out != null) _out.StopAll();
            _voices.Clear();
            _musicVoice = null;
        }

        public void ApplySettings()
        {
            var s = _settings != null ? _settings.Current : null;
            for (int i = 0; i < AllBuses.Length; i++) SetVolume(AllBuses[i], SettingsOptions.Volume(s, AllBuses[i]));
        }

        // ---- ISoundControl ----

        public void SetContext(string characterId, int wandStage)
        {
            _character = characterId;
            _stage = wandStage;
        }

        public void StartLoop(string loopKey, string eventId, SoundBus bus, float volume = 1f)
        {
            if (string.IsNullOrEmpty(loopKey)) return;
            var existing = FindLoop(loopKey);
            _clips.Clear();
            _bank.Resolve(eventId, _sfxOf(eventId), _character, _stage, 0f, _rndInt, _clips);
            ResolvedClip pick = default;
            bool found = false;
            for (int i = 0; i < _clips.Count; i++) if (_clips[i].Loop) { pick = _clips[i]; found = true; break; }
            AudioClip clip = found ? Load(pick.Path) : null;
            float cap = SoundMixPolicy.DbToGain(_policy.Config.MaxBoostDb);
            float baseGain = Mathf.Clamp(volume * SoundMixPolicy.DbToGain(found ? pick.GainDb : 0f), 0f, cap);
            if (existing != null)
            {
                if (clip != null && existing.Path == pick.Path)
                {
                    existing.Base = baseGain;
                    _out.SetVolume(existing.Handle, VolumeOf(existing, _now()));
                    return;
                }
                StopVoice(existing, _bank.StopFadeSeconds);
            }
            if (clip == null || _out == null) return;
            var v = new Voice { Bus = bus, Category = SoundCategory.Ambient, Base = baseGain, Path = pick.Path, LoopKey = loopKey };
            v.Handle = _out.StartVoice(clip, VolumeOf(v, _now()), pick.PitchMul, true);
            if (v.Handle < 0) return;
            _voices.Add(v);
            if (_gamePaused && IsGameBus(bus)) { _out.SetPaused(v.Handle, true); v.Paused = true; }
        }

        public void StopLoop(string loopKey)
        {
            var v = FindLoop(loopKey);
            if (v != null) StopVoice(v, _bank.StopFadeSeconds);
        }

        public void StopAllLoops()
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (_voices[i].LoopKey != null) StopVoice(_voices[i], _bank.StopFadeSeconds);
        }

        /// <summary>그 열쇠의 반복음이 돌고 있나(테스트).</summary>
        public bool IsLoopPlaying(string loopKey) => FindLoop(loopKey) != null;

        Voice FindLoop(string key)
        {
            for (int i = 0; i < _voices.Count; i++) if (_voices[i].LoopKey == key) return _voices[i];
            return null;
        }

        // ---- ISoundPlayer ----

        public void Play(string eventId, SoundBus bus = SoundBus.Sfx, float volume = 1f, float pitch = 1f)
        {
            if (string.IsNullOrEmpty(eventId) || _out == null) return;
            var sfx = _sfxOf(eventId);
            if (_bank.IsCut(sfx)) { CutAll(); return; }
            double now = _now();
            float semis = pitch > 0f ? 12f * Mathf.Log(pitch, 2f) : 0f;
            _clips.Clear();
            _bank.Resolve(eventId, sfx, _character, _stage, semis, _rndInt, _clips);
            if (_clips.Count == 0) { NoteUnmapped(eventId); return; }
            SoundCategory cat = SoundEventClassifier.Classify(eventId);
            float cap = SoundMixPolicy.DbToGain(_policy.Config.MaxBoostDb);
            for (int i = 0; i < _clips.Count; i++)
            {
                var c = _clips[i];
                var clip = Load(c.Path);
                if (clip == null) continue;
                bool repeated;
                if (!_gate.TryPass(c.Path, now, out repeated)) continue;
                float p = c.PitchConsumed ? c.PitchMul : pitch * c.PitchMul;
                float j = _bank.RepeatPitchJitterSemitones;
                if (repeated && j > 0f && bus != SoundBus.Ui && !c.PitchConsumed)
                    p *= Mathf.Pow(2f, _rndRange(-j, j) / 12f);
                if (p <= 0.01f) p = 0.01f;
                float req = Mathf.Clamp(volume * SoundMixPolicy.DbToGain(c.GainDb), 0f, cap);
                var d = _policy.Request(eventId, cat, req, now, clip.length / p);
                if (!d.Accepted) continue;
                int slot = d.VoiceIndex;
                if (slot >= 0 && slot < _policyHandle.Length && _policyHandle[slot] >= 0)
                {
                    var old = FindHandle(_policyHandle[slot]);
                    if (old != null) StopVoice(old, 0f);
                }
                float baseGain = d.Ducked ? d.Gain / SoundMixPolicy.DbToGain(_policy.Config.DuckDb) : d.Gain;
                var v = new Voice { Bus = bus, Category = cat, Base = baseGain, Path = c.Path };
                v.Handle = _out.StartVoice(clip, VolumeOf(v, now), p, false); // 반복 클립도 Play로는 한 번만(반복은 StartLoop)
                if (v.Handle < 0) continue;
                _voices.Add(v);
                if (slot >= 0 && slot < _policyHandle.Length) _policyHandle[slot] = v.Handle;
            }
        }

        public void SetMusic(string trackId)
        {
            if (trackId == _music) return;
            _music = trackId;
            if (_musicVoice != null) { StopVoice(_musicVoice, _bank.MusicFadeSeconds); _musicVoice = null; }
            if (string.IsNullOrEmpty(trackId) || _out == null) return;
            // 곡 목록은 미정(10 요약서 5.1절) — 파일이 생기면 Resources/<music_root>/<id>로 넣는다. 없으면 경고 없이 조용히.
            var clip = LoadQuiet(_bank.MusicRoot + "/" + trackId);
            if (clip == null) return;
            var v = new Voice { Bus = SoundBus.Bgm, Category = SoundCategory.Ambient, Base = 1f, Path = trackId, Music = true };
            v.Handle = _out.StartVoice(clip, VolumeOf(v, _now()), 1f, true);
            if (v.Handle < 0) return;
            _voices.Add(v);
            _musicVoice = v;
        }

        public void SetVolume(SoundBus bus, float volume01)
        {
            int i = (int)bus;
            if (i < 0 || i >= _volumes.Length) return;
            if (float.IsNaN(volume01)) volume01 = 0f;
            _volumes[i] = Mathf.Clamp01(volume01);
            RefreshVolumes();
        }

        public float GetVolume(SoundBus bus)
        {
            int i = (int)bus;
            return i >= 0 && i < _volumes.Length ? _volumes[i] : 0f;
        }

        // ---- 프레임 ----

        /// <summary>프레임마다(AudioSourcePool.Update): 판 일시정지 따라가기, 끝난 소리 치우기, 누르기 음량 갱신.</summary>
        public void Tick(bool gamePaused)
        {
            if (gamePaused != _gamePaused) SetGamePaused(gamePaused);
            Prune();
            RefreshVolumes();
        }

        public void SetGamePaused(bool paused)
        {
            _gamePaused = paused;
            if (_out == null) return;
            for (int i = 0; i < _voices.Count; i++)
            {
                var v = _voices[i];
                if (!IsGameBus(v.Bus) || v.Paused == paused) continue;
                v.Paused = paused;
                _out.SetPaused(v.Handle, paused);
            }
        }

        /// <summary>E-10 숨 멈춤·+13 침묵(10 요약서 5.2절): 배경음 말고 전부 끊는다.</summary>
        public void CutAll()
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (!_voices[i].Music) StopVoice(_voices[i], _bank.CutFadeSeconds);
            _policy.CutAll();
            for (int i = 0; i < _policyHandle.Length; i++) _policyHandle[i] = -1;
        }

        static bool IsGameBus(SoundBus b) => b == SoundBus.Sfx || b == SoundBus.Wand || b == SoundBus.Enemy;

        float VolumeOf(Voice v, double now)
        {
            float g = v.Base * GetVolume(v.Bus);
            if (v.Bus != SoundBus.Master) g *= GetVolume(SoundBus.Master);
            if (!v.Music && v.Category != SoundCategory.PlayerHit) g *= _policy.DuckGain(now);
            return Mathf.Clamp01(g);
        }

        void RefreshVolumes()
        {
            if (_out == null) return;
            double now = _now();
            for (int i = 0; i < _voices.Count; i++) _out.SetVolume(_voices[i].Handle, VolumeOf(_voices[i], now));
        }

        void Prune()
        {
            if (_out == null) return;
            for (int i = _voices.Count - 1; i >= 0; i--)
                if (!_out.IsAlive(_voices[i].Handle))
                {
                    if (_voices[i] == _musicVoice) _musicVoice = null;
                    _voices.RemoveAt(i);
                }
        }

        Voice FindHandle(int h)
        {
            for (int i = 0; i < _voices.Count; i++) if (_voices[i].Handle == h) return _voices[i];
            return null;
        }

        void StopVoice(Voice v, float fade)
        {
            if (_out != null) _out.Stop(v.Handle, fade);
            _voices.Remove(v);
            if (v == _musicVoice) _musicVoice = null;
        }

        AudioClip Load(string path)
        {
            AudioClip c;
            if (_cache.TryGetValue(path, out c)) return c;
            c = _load(path);
            _cache[path] = c;
            if (c == null && _warnedMissing.Add(path))
                Debug.LogWarning("[Sound] clip listed in DataSolo/Audio.json is missing from Resources: " + path);
            return c;
        }

        AudioClip LoadQuiet(string path)
        {
            AudioClip c;
            if (_cache.TryGetValue(path, out c)) return c;
            c = _load(path);
            _cache[path] = c;
            return c;
        }

        void NoteUnmapped(string eventId)
        {
            if (!_unmapped.Add(eventId)) return;
            if (_unmapped.Count == 1)
                Debug.Log("[Sound] events without a clip are skipped silently (first: " + eventId + "). See ClipSoundPlayer.UnmappedEvents.");
        }
    }
}
