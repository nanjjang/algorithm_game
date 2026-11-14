using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.Audio
{
    /// <summary>
    /// 앱의 소리 재생기를 세운다(AppFactory.CreateSound가 부른다): "Sound" GameObject + AudioSourcePool + ClipSoundPlayer.
    /// 표: Resources/DataSolo/Audio.json(SoundBank), 사건 → sfx 묶음: Resources/Data/presentation/event_bindings.json.
    /// 클립은 Resources.Load&lt;AudioClip&gt;(재생기가 캐시). 다시 부팅하면 앞의 것을 걷는다.
    /// </summary>
    public static class UnitySoundPlayer
    {
        static GameObject _lastGo;
        static ClipSoundPlayer _last;

        public static ISoundPlayer Create(GameContext ctx)
        {
            Release();
            var bankText = Resources.Load<TextAsset>(SoundBank.Resource);
            var bank = SoundBank.Parse(bankText != null ? bankText.text : null);
            var bindText = Resources.Load<TextAsset>(PresentationData.BindingsResource);
            var bindings = EventBindings.Parse(bindText != null ? bindText.text : null);
            var mix = SoundMixConfig.FromJson(ctx != null && ctx.Content != null ? ctx.Content.SoloExtra(SoundMixConfig.ExtraName) : null);

            var go = new GameObject("Sound");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            go.AddComponent<AudioListenerGuard>();
            var pool = go.AddComponent<AudioSourcePool>();
            pool.Init(mix.MaxVoices + bank.ExtraVoices);

            var player = new ClipSoundPlayer(ctx != null ? ctx.Settings : null, () => Time.realtimeSinceStartupAsDouble, mix, bank,
                id => { var b = bindings.Get(id); return b != null ? (IReadOnlyList<string>)b.Sfx : null; },
                path => Resources.Load<AudioClip>(path), pool);
            pool.OnTick = () =>
            {
                var s = ctx != null ? ctx.Session : null;
                player.Tick(s != null && s.HasRun && s.Paused);
            };
            _lastGo = go;
            _last = player;
            return player;
        }

        /// <summary>앞서 세운 재생기를 걷는다.</summary>
        public static void Release()
        {
            if (_last != null) { _last.Dispose(); _last = null; }
            if (_lastGo != null)
            {
                if (Application.isPlaying) Object.Destroy(_lastGo); else Object.DestroyImmediate(_lastGo);
                _lastGo = null;
            }
        }
    }

    /// <summary>장면에 AudioListener가 하나도 없으면 하나 단다(카메라가 코드로 만들어져 빠질 수 있다). 둘이면 소리가 나도 경고가 뜨므로 있으면 달지 않는다.</summary>
    public sealed class AudioListenerGuard : MonoBehaviour
    {
        float _next;

        void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            var own = GetComponent<AudioListener>();
            int n = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            if (own == null && n == 0) gameObject.AddComponent<AudioListener>();
            else if (own != null && n > 1) Destroy(own);
        }
    }
}
