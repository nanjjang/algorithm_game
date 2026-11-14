using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wizard.Solo.Art;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 전장 위 글자(숫자가 아닌 것: 기술 이름, 골드 '+', 늦은 입력 문구) — D-69: vc.WorldTextRoot 아래 uGUI Text + Outline을
    /// vc.WorldToHud(simPos) 자리에 둔다. TextMesh를 쓰지 않는다. 글자는 이름 표를 거친 것만 받는다.
    /// </summary>
    public sealed class WorldTextPopups
    {
        public sealed class Popup
        {
            public GameObject Go;
            public RectTransform Rt;
            public Text Text;
            public Outline Outline;
            public Image Ribbon;
            public Vector2 SimPos;
            public float Age, Life, RiseCells, FadeOut;
            public Color Color;
            public System.Func<Vector2?> Follow;
            public bool Active;
        }

        readonly EffectsShared _s;
        readonly List<Popup> _live = new List<Popup>();
        readonly Stack<Popup> _free = new Stack<Popup>();

        public WorldTextPopups(EffectsShared shared) { _s = shared; }

        public int LiveCount => _live.Count;
        public IReadOnlyList<Popup> Live => _live;

        /// <param name="ribbon">글자 뒤에 깔 띠 그림(기술 이름 띠). null이면 없음.</param>
        public Popup Show(string text, Vector2 simPos, UiFontId font, Color color, float life, float riseCells, float fadeOut,
            Sprite ribbon = null, Color? ribbonColor = null, System.Func<Vector2?> follow = null)
        {
            var vc = _s.Vc;
            if (vc == null || vc.WorldTextRoot == null || string.IsNullOrEmpty(text)) return null;
            var p = _free.Count > 0 ? _free.Pop() : Create(vc.WorldTextRoot);
            p.Go.SetActive(true);
            var art = vc.Game != null ? vc.Game.Art : null;
            var f = art != null ? art.Font(font) : null;
            if (f != null) p.Text.font = f;
            p.Text.fontSize = (int)font;
            p.Text.text = text;
            p.Text.color = color;
            p.Color = color;
            p.SimPos = simPos;
            p.Age = 0f;
            p.Life = life;
            p.RiseCells = riseCells;
            p.FadeOut = fadeOut;
            p.Follow = follow;
            p.Active = true;
            if (ribbon != null)
            {
                p.Ribbon.enabled = true;
                p.Ribbon.sprite = ribbon;
                p.Ribbon.color = ribbonColor ?? Color.white;
                p.Ribbon.rectTransform.sizeDelta = new Vector2(ribbon.rect.width, ribbon.rect.height);
            }
            else p.Ribbon.enabled = false;
            _live.Add(p);
            Place(p);
            return p;
        }

        static Popup Create(RectTransform parent)
        {
            var go = new GameObject("WorldText", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero; // 글자는 넘쳐 그린다(Overflow) — 칸 크기가 필요 없다
            var rib = new GameObject("Ribbon", typeof(RectTransform)).AddComponent<Image>();
            rib.rectTransform.SetParent(rt, false);
            rib.raycastTarget = false;
            rib.enabled = false;
            var tgo = new GameObject("Text", typeof(RectTransform));
            var trt = (RectTransform)tgo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var text = tgo.AddComponent<Text>();
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = tgo.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(1f, -1f);
            return new Popup { Go = go, Rt = rt, Text = text, Outline = outline, Ribbon = rib };
        }

        public void Update(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.Age += dt;
                if (p.Age >= p.Life) { Recycle(i); continue; }
                if (p.Follow != null)
                {
                    var q = p.Follow();
                    if (q.HasValue) p.SimPos = q.Value;
                }
                Place(p);
            }
        }

        void Place(Popup p)
        {
            var vc = _s.Vc;
            float k = p.Life > 0f ? Mathf.Clamp01(p.Age / p.Life) : 1f;
            // 시뮬레이션 y는 아래가 + — 위로 뜨게 뺀다.
            Vector2 pos = p.SimPos - new Vector2(0f, p.RiseCells * k);
            if (vc != null) p.Rt.localPosition = vc.WorldToHud(pos);
            float a = 1f;
            if (p.FadeOut > 0f)
            {
                float left = p.Life - p.Age;
                if (left < p.FadeOut) a = Mathf.Clamp01(left / p.FadeOut);
            }
            var c = p.Color;
            c.a *= a;
            p.Text.color = c;
            var oc = p.Outline.effectColor;
            oc.a = a;
            p.Outline.effectColor = oc;
            if (p.Ribbon.enabled)
            {
                var rc = p.Ribbon.color;
                rc.a = a;
                p.Ribbon.color = rc;
            }
        }

        void Recycle(int i)
        {
            var p = _live[i];
            _live.RemoveAt(i);
            p.Active = false;
            p.Follow = null;
            if (p.Go != null) { p.Go.SetActive(false); _free.Push(p); }
        }

        public void DisposeAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--) Recycle(i);
            while (_free.Count > 0)
            {
                var p = _free.Pop();
                if (p.Go != null) FxUtil.Destroy(p.Go);
            }
        }
    }
}
