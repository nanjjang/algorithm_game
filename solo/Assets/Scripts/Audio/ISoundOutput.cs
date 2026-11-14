using UnityEngine;

namespace Wizard.Solo.Audio
{
    /// <summary>
    /// 실제로 소리를 내는 곳(AudioSourcePool). 재생기 논리(ClipSoundPlayer)와 나눠 둔 것은 EditMode 테스트가 가짜 출력으로
    /// 연타 제한·반복음 정지를 시험하게 하려고다. 손잡이(handle)는 0 이상, 실패면 −1.
    /// </summary>
    public interface ISoundOutput
    {
        int Start(AudioClip clip, float volume, float pitch, bool loop);
        /// <summary>fadeSeconds 동안 줄이다가 멈춘다(0 = 바로).</summary>
        void Stop(int handle, float fadeSeconds);
        void SetVolume(int handle, float volume);
        void SetPaused(int handle, bool paused);
        /// <summary>아직 소리를 내는 중이거나 멈춰 둔 상태(일시정지)면 참. 끝났거나 멈췄으면 거짓.</summary>
        bool IsAlive(int handle);
        void StopAll();
    }
}
