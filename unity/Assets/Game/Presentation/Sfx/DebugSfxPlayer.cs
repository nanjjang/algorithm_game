#nullable enable
using System;
using System.Globalization;
using Wizard.ViewLogic.Presentation;

namespace Wizard.Unity.Presentation.Sfx
{
    // 에셋이 없는 지금의 소리 재생기(CONTRACTS 13, ARCHITECTURE 9, T-29). 소리를 내지 않고 최근에 부른 식별자를 남긴다.
    // 디버그 겹침 화면(Bus/PresentationDebugOverlay)이 그 줄을 찍는다. 실제 재생기(V-14)는 ISfxPlayer 를 구현해
    // PresentationBusSetup.Sfx 에 꽂기만 하면 된다. 소리는 라우터가 Confirmed 에서만 부른다(CONTRACTS 6.4).
    public sealed class DebugSfxPlayer : ISfxPlayer
    {
        public const int Capacity = 32;                   // 자료구조 크기. 넘치면 가장 오래된 줄을 버린다
        public const int MarkLifetimeMs = 1000;           // [임시] 디버그 줄이 남는 길이(표시 전용)

        private readonly Func<long> _nowMs;
        private readonly DebugSfxMark[] _marks = new DebugSfxMark[Capacity];
        private readonly float[] _busVolume;
        private int _count;

        public DebugSfxPlayer(Func<long> nowMs)
        {
            _nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
            _busVolume = new float[Enum.GetValues(typeof(SfxBus)).Length];
            for (int i = 0; i < _busVolume.Length; i++)
            {
                _busVolume[i] = 1f;
            }
        }

        public int PlayCount { get; private set; }

        public int Version { get; private set; }

        public int Count => _count;

        public DebugSfxMark Mark(int index)
        {
            if ((uint)index >= (uint)_count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _marks[index];
        }

        public float BusVolume(SfxBus bus) => (uint)bus < (uint)_busVolume.Length ? _busVolume[(int)bus] : 0f;

        public void Play(string sfxId, int pitchStep = 0, float volumeDb = 0f)
        {
            PlayCount++;
            Version++;
            if (_count == Capacity)
            {
                Array.Copy(_marks, 1, _marks, 0, Capacity - 1);
                _count--;
            }
            _marks[_count++] = new DebugSfxMark(sfxId, pitchStep, volumeDb, LabelOf(sfxId, pitchStep), _nowMs());
        }

        public void SetBus(SfxBus bus, float volume)
        {
            if ((uint)bus < (uint)_busVolume.Length)
            {
                _busVolume[(int)bus] = Math.Max(0f, Math.Min(1f, volume));
            }
        }

        public void Clear()
        {
            if (_count > 0)
            {
                Array.Clear(_marks, 0, _count);
                _count = 0;
                Version++;
            }
        }

        public void Advance(long nowMs)
        {
            int w = 0;
            for (int r = 0; r < _count; r++)
            {
                if (nowMs - _marks[r].AtMs < MarkLifetimeMs)
                {
                    _marks[w++] = _marks[r];
                }
            }
            if (w != _count)
            {
                Array.Clear(_marks, w, _count - w);
                _count = w;
                Version++;
            }
        }

        // "sfx_c10 p3": 식별자와 연속 스침의 음높이 단(0이면 생략)
        public static string LabelOf(string sfxId, int pitchStep) =>
            pitchStep == 0 ? sfxId : sfxId + " p" + pitchStep.ToString(CultureInfo.InvariantCulture);
    }
}
