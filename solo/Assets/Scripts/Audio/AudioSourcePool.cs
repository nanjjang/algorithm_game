using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wizard.Solo.Audio
{
    /// <summary>
    /// AudioSource 풀(ISoundOutput). 2D 소리(spatialBlend 0). 프레임마다 멈춤 페이드를 진행하고 재생기의 Tick을 부른다.
    /// 시간은 실제 경과 시간(Time.unscaledDeltaTime) — 일시정지·timeScale과 무관하다.
    /// </summary>
    public sealed class AudioSourcePool : MonoBehaviour, ISoundOutput
    {
        AudioSource[] _src = new AudioSource[0];
        int[] _handle = new int[0];
        bool[] _busy = new bool[0];
        bool[] _paused = new bool[0];
        float[] _fadeLeft = new float[0];
        float[] _fadeTotal = new float[0];
        float[] _fadeFrom = new float[0];
        int _nextHandle = 1;

        /// <summary>프레임마다 부를 것(재생기의 Tick).</summary>
        public Action OnTick;

        public int Size => _src.Length;

        public void Init(int size)
        {
            size = Math.Max(1, size);
            _src = new AudioSource[size];
            _handle = new int[size];
            _busy = new bool[size];
            _paused = new bool[size];
            _fadeLeft = new float[size];
            _fadeTotal = new float[size];
            _fadeFrom = new float[size];
            for (int i = 0; i < size; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                s.loop = false;
                _src[i] = s;
            }
        }

        int IndexOf(int handle)
        {
            if (handle <= 0) return -1;
            for (int i = 0; i < _handle.Length; i++) if (_busy[i] && _handle[i] == handle) return i;
            return -1;
        }

        bool Done(int i) => !_busy[i] || (!_paused[i] && _src[i] != null && !_src[i].isPlaying);

        public int StartVoice(AudioClip clip, float volume, float pitch, bool loop)
        {
            if (clip == null) return -1;
            int slot = -1;
            for (int i = 0; i < _src.Length; i++) if (Done(i)) { slot = i; break; }
            if (slot < 0) return -1;
            var s = _src[slot];
            if (s == null) return -1;
            s.Stop();
            s.clip = clip;
            s.loop = loop;
            s.volume = Mathf.Clamp01(volume);
            s.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
            s.Play();
            _busy[slot] = true;
            _paused[slot] = false;
            _fadeLeft[slot] = 0f;
            _handle[slot] = _nextHandle++;
            if (_nextHandle == int.MaxValue) _nextHandle = 1;
            return _handle[slot];
        }

        public void Stop(int handle, float fadeSeconds)
        {
            int i = IndexOf(handle);
            if (i < 0) return;
            if (fadeSeconds <= 0f || _paused[i]) { Kill(i); return; }
            if (_fadeLeft[i] > 0f) return;
            _fadeTotal[i] = _fadeLeft[i] = fadeSeconds;
            _fadeFrom[i] = _src[i].volume;
        }

        void Kill(int i)
        {
            if (_src[i] != null) { _src[i].Stop(); _src[i].clip = null; }
            _busy[i] = false;
            _paused[i] = false;
            _fadeLeft[i] = 0f;
            _handle[i] = 0;
        }

        public void SetVolume(int handle, float volume)
        {
            int i = IndexOf(handle);
            if (i < 0 || _fadeLeft[i] > 0f) return;
            _src[i].volume = Mathf.Clamp01(volume);
        }

        public void SetPaused(int handle, bool paused)
        {
            int i = IndexOf(handle);
            if (i < 0 || _paused[i] == paused) return;
            _paused[i] = paused;
            if (paused) _src[i].Pause(); else _src[i].UnPause();
        }

        public bool IsAlive(int handle)
        {
            int i = IndexOf(handle);
            return i >= 0 && !Done(i) && _fadeLeft[i] <= 0f;
        }

        public void StopAll()
        {
            for (int i = 0; i < _src.Length; i++) if (_busy[i]) Kill(i);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _src.Length; i++)
            {
                if (!_busy[i] || _fadeLeft[i] <= 0f) continue;
                _fadeLeft[i] -= dt;
                if (_fadeLeft[i] <= 0f) { Kill(i); continue; }
                _src[i].volume = _fadeFrom[i] * (_fadeLeft[i] / _fadeTotal[i]);
            }
            OnTick?.Invoke();
        }
    }
}
