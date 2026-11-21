using System.Collections.Generic;
using UnityEngine;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 화면의 UI 입자(그림 한 장짜리 Image를 미리 만들어 돌려 쓴다). 좌표는 뿌리의 왼쪽 위 원점 px(아래가 +y).
    /// 표시 전용이라 화면 난수(UnityEngine.Random)를 쓴다 — 결과는 이미 정해져 있고 입자는 결과를 바꾸지 않는다.
    /// </summary>
    public sealed class EnhanceParticles
    {
        public sealed class P
        {
            public UImage Img;
            public bool Alive;
            public Vector2 Pos, Vel, Target;
            public float Gravity, Drag, Life, Age, Size0, Size1, Rot, RotVel, Seek, Delay;
            public Color C0, C1;
            public bool Stretch;
        }

        readonly RectTransform _root;
        readonly List<P> _pool = new List<P>();
        readonly Material _additive;
        readonly int _max;

        public EnhanceParticles(RectTransform root, Material additive, int max)
        {
            _root = root;
            _additive = additive;
            _max = max;
        }

        public int AliveCount
        {
            get { int n = 0; foreach (var p in _pool) if (p.Alive) n++; return n; }
        }

        /// <summary>입자 하나를 빌린다(남는 칸이 없으면 null — 그냥 덜 그린다).</summary>
        public P Spawn(Sprite sprite, Vector2 pos, Vector2 vel, float life, float size0, float size1, Color c0, Color c1, bool additive = true)
        {
            if (_root == null) return null;
            P p = null;
            for (int i = 0; i < _pool.Count; i++) if (!_pool[i].Alive) { p = _pool[i]; break; }
            if (p == null)
            {
                if (_pool.Count >= _max) return null;
                var go = new GameObject("Particle", typeof(RectTransform), typeof(UImage));
                var rt = (RectTransform)go.transform;
                rt.SetParent(_root, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                var img = go.GetComponent<UImage>();
                img.raycastTarget = false;
                p = new P { Img = img };
                _pool.Add(p);
            }
            p.Alive = true;
            p.Pos = pos; p.Vel = vel; p.Target = pos;
            p.Gravity = 0f; p.Drag = 0f; p.Life = Mathf.Max(0.01f, life); p.Age = 0f;
            p.Size0 = size0; p.Size1 = size1; p.Rot = 0f; p.RotVel = 0f; p.Seek = 0f; p.Delay = 0f;
            p.C0 = c0; p.C1 = c1; p.Stretch = false;
            p.Img.sprite = sprite;
            p.Img.material = additive ? _additive : null;
            p.Img.gameObject.SetActive(true);
            Apply(p);
            return p;
        }

        /// <summary>한 점에서 사방으로 터진다.</summary>
        public void Burst(Sprite sprite, Vector2 at, int count, float speedMin, float speedMax, float life, float size, Color a, Color b,
            float gravity = 0f, float drag = 1.5f, bool stretch = false)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float sp = Random.Range(speedMin, speedMax);
                var c = Color.Lerp(a, b, Random.value);
                var p = Spawn(sprite, at, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * sp, life * Random.Range(0.6f, 1.2f),
                    size * Random.Range(0.6f, 1.3f), size * 0.15f, c, new Color(c.r, c.g, c.b, 0f));
                if (p == null) return;
                p.Gravity = gravity; p.Drag = drag; p.Stretch = stretch;
                p.RotVel = Random.Range(-360f, 360f);
            }
        }

        /// <summary>반지름 radius 고리 위에서 생겨 target으로 빨려 든다.</summary>
        public void Converge(Sprite sprite, Vector2 target, float radiusMin, float radiusMax, int count, float life, float size, Color a, Color b)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = Random.Range(0f, Mathf.PI * 2f);
                float r = Random.Range(radiusMin, radiusMax);
                var pos = target + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                var tangent = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * Random.Range(40f, 120f);
                var c = Color.Lerp(a, b, Random.value);
                var p = Spawn(sprite, pos, tangent, life, size * Random.Range(0.6f, 1.2f), size * 0.3f, new Color(c.r, c.g, c.b, 0f), c);
                if (p == null) return;
                p.Target = target;
                p.Seek = Random.Range(5f, 8f);
                p.Drag = 2f;
                p.Stretch = true;
            }
        }

        public void Clear()
        {
            foreach (var p in _pool) { p.Alive = false; if (p.Img != null) p.Img.gameObject.SetActive(false); }
        }

        public void Update(float dt)
        {
            if (dt <= 0f) return;
            for (int i = 0; i < _pool.Count; i++)
            {
                var p = _pool[i];
                if (!p.Alive) continue;
                if (p.Delay > 0f) { p.Delay -= dt; continue; }
                p.Age += dt;
                if (p.Age >= p.Life || p.Img == null)
                {
                    p.Alive = false;
                    if (p.Img != null) p.Img.gameObject.SetActive(false);
                    continue;
                }
                if (p.Seek > 0f)
                {
                    // 끝으로 갈수록 세게 당긴다(빨려 드는 느낌).
                    float k = p.Age / p.Life;
                    p.Vel += (p.Target - p.Pos) * (p.Seek * (0.4f + 2.6f * k * k)) * dt;
                }
                p.Vel.y += p.Gravity * dt;
                p.Vel *= Mathf.Max(0f, 1f - p.Drag * dt);
                p.Pos += p.Vel * dt;
                p.Rot += p.RotVel * dt;
                Apply(p);
            }
        }

        static void Apply(P p)
        {
            float k = Mathf.Clamp01(p.Age / p.Life);
            // 나타남은 빠르게, 사라짐은 천천히.
            float fadeIn = Mathf.Clamp01(k * 8f);
            var c = Color.Lerp(p.C0, p.C1, k);
            c.a *= fadeIn;
            p.Img.color = c;
            float s = Mathf.Lerp(p.Size0, p.Size1, k);
            var rt = p.Img.rectTransform;
            rt.anchoredPosition = new Vector2(p.Pos.x, -p.Pos.y);
            if (p.Stretch && p.Vel.sqrMagnitude > 1f)
            {
                float len = Mathf.Clamp(p.Vel.magnitude * 0.03f, 1f, 4f);
                rt.sizeDelta = new Vector2(s * len, s);
                rt.localEulerAngles = new Vector3(0f, 0f, -Mathf.Atan2(p.Vel.y, p.Vel.x) * Mathf.Rad2Deg);
            }
            else
            {
                rt.sizeDelta = new Vector2(s, s);
                rt.localEulerAngles = new Vector3(0f, 0f, p.Rot);
            }
        }
    }
}
