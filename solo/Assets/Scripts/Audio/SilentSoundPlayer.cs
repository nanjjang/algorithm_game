using System;
using System.Collections.Generic;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Audio
{
    /// <summary>재생 요청 한 건의 기록(테스트·디버그용).</summary>
    public struct SoundRecord
    {
        public string EventId;
        public SoundBus Bus;
        public float Volume;
        public float Pitch;
        public double Time;
        public bool Accepted;
        public SoundRejectReason Reason;
        public SoundCategory Category;
        /// <summary>섞는 규칙의 배율 × 묶음 음량 × 전체 음량(받아들인 것만. 거절이면 0).</summary>
        public float FinalGain;
        public string StolenEventId;
    }

    /// <summary>
    /// 무음 소리 재생기(10 요약서 5절: 저장소에 게임 소리 파일이 없다). 소리를 내지 않고, 섞는 규칙(SoundMixPolicy)을
    /// 거친 결과를 마지막 n건만 기억한다. 음량은 생성 때와 설정이 바뀔 때(ISettingsStore.Changed)
    /// SettingsOptions.Volume으로 여섯 묶음 모두 다시 넣는다. 소리로만 전하는 정보가 없으니 무음이어도 게임은 완전하다.
    /// </summary>
    public sealed class SilentSoundPlayer : ISoundPlayer, IDisposable
    {
        public const int DefaultHistorySize = 64;
        static readonly SoundBus[] AllBuses =
            { SoundBus.Master, SoundBus.Bgm, SoundBus.Sfx, SoundBus.Wand, SoundBus.Ui, SoundBus.Enemy };

        readonly ISettingsStore _settings;
        readonly Func<double> _now;
        readonly SoundMixPolicy _policy;
        readonly float[] _volumes = new float[AllBuses.Length];
        readonly SoundRecord[] _history;
        int _historyStart, _historyCount;

        public string CurrentMusic { get; private set; }
        public SoundMixPolicy Policy => _policy;

        /// <param name="settings">음량을 읽을 설정(null이면 전부 1).</param>
        /// <param name="now">섞는 규칙의 시각(초). 앱은 실제 경과 시간, 테스트는 손으로 움직이는 시계.</param>
        public SilentSoundPlayer(ISettingsStore settings, Func<double> now, SoundMixConfig config = null, int historySize = DefaultHistorySize)
        {
            _settings = settings;
            _now = now ?? (() => 0.0);
            _policy = new SoundMixPolicy(config);
            _history = new SoundRecord[Math.Max(1, historySize)];
            for (int i = 0; i < _volumes.Length; i++) _volumes[i] = 1f;
            ApplySettings();
            if (_settings != null) _settings.Changed += ApplySettings;
        }

        public void Dispose()
        {
            if (_settings != null) _settings.Changed -= ApplySettings;
        }

        /// <summary>설정의 vol_*를 여섯 묶음에 다시 넣는다.</summary>
        public void ApplySettings()
        {
            var s = _settings != null ? _settings.Current : null;
            for (int i = 0; i < AllBuses.Length; i++) SetVolume(AllBuses[i], SettingsOptions.Volume(s, AllBuses[i]));
        }

        public void Play(string eventId, SoundBus bus = SoundBus.Sfx, float volume = 1f, float pitch = 1f)
        {
            double t = _now();
            var d = _policy.Request(eventId, volume, t);
            float final = 0f;
            if (d.Accepted)
            {
                final = d.Gain * GetVolume(bus);
                if (bus != SoundBus.Master) final *= GetVolume(SoundBus.Master);
            }
            Remember(new SoundRecord
            {
                EventId = eventId, Bus = bus, Volume = volume, Pitch = pitch, Time = t,
                Accepted = d.Accepted, Reason = d.Reason, Category = d.Category, FinalGain = final, StolenEventId = d.StolenEventId
            });
        }

        public void SetMusic(string trackId)
        {
            CurrentMusic = trackId;
        }

        public void SetVolume(SoundBus bus, float volume01)
        {
            int i = (int)bus;
            if (i < 0 || i >= _volumes.Length) return;
            if (float.IsNaN(volume01)) volume01 = 0f;
            _volumes[i] = volume01 < 0f ? 0f : volume01 > 1f ? 1f : volume01;
        }

        public float GetVolume(SoundBus bus)
        {
            int i = (int)bus;
            return i >= 0 && i < _volumes.Length ? _volumes[i] : 0f;
        }

        /// <summary>최근 기록(오래된 것 → 새것).</summary>
        public List<SoundRecord> Recent()
        {
            var list = new List<SoundRecord>(_historyCount);
            for (int i = 0; i < _historyCount; i++) list.Add(_history[(_historyStart + i) % _history.Length]);
            return list;
        }

        public SoundRecord? Last => _historyCount == 0 ? (SoundRecord?)null
            : _history[(_historyStart + _historyCount - 1) % _history.Length];

        public void ClearHistory()
        {
            _historyStart = 0;
            _historyCount = 0;
        }

        void Remember(SoundRecord r)
        {
            if (_historyCount < _history.Length)
            {
                _history[(_historyStart + _historyCount) % _history.Length] = r;
                _historyCount++;
            }
            else
            {
                _history[_historyStart] = r;
                _historyStart = (_historyStart + 1) % _history.Length;
            }
        }
    }
}
