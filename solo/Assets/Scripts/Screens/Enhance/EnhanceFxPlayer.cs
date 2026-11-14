using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 화면의 일회 효과 그림 재생기(ritual 효과 묶음 — 09 요약서 5.3절). uGUI Image 하나에 SpriteAnim 프레임을 돌린다.
    /// 그림이 없으면 아무것도 그리지 않는다(소리 사건과 결과 카드가 같은 뜻을 전한다 — 소리로만 전달되는 정보 없음).
    /// 시계는 화면이 넘기는 초이고, 그림 정지(hitstop) 동안 화면이 dt = 0을 넘긴다.
    /// </summary>
    public sealed class EnhanceFxPlayer
    {
        sealed class Active
        {
            public UImage Image;
            public SpriteAnim Anim;
            public float Time;
            public float Duration;
            public bool Loop;
            public string Tag;
        }

        readonly RectTransform _root;
        readonly List<Active> _active = new List<Active>();

        public EnhanceFxPlayer(RectTransform root) { _root = root; }

        public int Count => _active.Count;

        /// <summary>효과 하나를 (x, y, w, h)에 띄운다. duration ≤ 0이면 애니 한 번 길이. loop면 Stop(tag)까지.</summary>
        public void Play(SpriteAnim anim, float x, float y, float w, float h, Color color, float duration = 0f, bool loop = false, string tag = null)
        {
            if (anim == null || anim.Frames == null || anim.Frames.Length == 0 || _root == null) return;
            var img = Wizard.Solo.UI.UiKit.Image(_root, anim.Frames[0], x, y, w, h, color, true);
            img.name = "Fx." + (anim.Key ?? tag ?? "anim");
            float dur = duration > 0f ? duration : (anim.Duration > 0f ? anim.Duration : 0.5f);
            _active.Add(new Active { Image = img, Anim = anim, Duration = dur, Loop = loop, Tag = tag });
        }

        public void Stop(string tag)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Tag != tag) continue;
                EnhanceUi.Destroy(_active[i].Image != null ? _active[i].Image.gameObject : null);
                _active.RemoveAt(i);
            }
        }

        public bool IsPlaying(string tag)
        {
            for (int i = 0; i < _active.Count; i++) if (_active[i].Tag == tag) return true;
            return false;
        }

        public void StopAll()
        {
            for (int i = 0; i < _active.Count; i++) EnhanceUi.Destroy(_active[i].Image != null ? _active[i].Image.gameObject : null);
            _active.Clear();
        }

        public void Update(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                a.Time += dt;
                if (!a.Loop && a.Time >= a.Duration)
                {
                    EnhanceUi.Destroy(a.Image != null ? a.Image.gameObject : null);
                    _active.RemoveAt(i);
                    continue;
                }
                if (a.Image != null) a.Image.sprite = a.Anim.At(a.Time);
            }
        }
    }
}
