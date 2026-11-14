using UnityEngine;
using Wizard.Solo.Art;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.UI
{
    /// <summary>
    /// 선택 괄호(K-40 겹그림, 08 1.3절). 대상 네 모서리 바깥 ㄱ자 4개: 선 4px 선택 노랑(꺼짐이면 회청), 바깥 먹색 2px, 대상에서 4px 바깥.
    /// 팔 길이는 대상 크기로(UiBrackets.SizeFor: 12 / 20 / 28px). 커서 괄호는 0.5초 주기로 2px 벌어졌다 오므라든다(2프레임, 번쩍이지 않는다).
    /// 확정(고른 것)은 멈추고 오른쪽 위에 확정 체크(K-41). 괄호는 화면 그림 층에만 쓴다(전장 안에는 그리지 않는다).
    /// 그림 없이 코드 도형으로 그려 크기·팔 길이가 명세와 정확히 맞는다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UiSelectionBracket : MonoBehaviour
    {
        const int Corners = 4;
        readonly RectTransform[] _outline = new RectTransform[Corners * 2];
        readonly RectTransform[] _line = new RectTransform[Corners * 2];
        readonly UImage[] _lineImg = new UImage[Corners * 2];
        RectTransform _check;
        UiBracketVisual _visual;
        float _time;
        int _frame = -1;
        Vector2 _lastSize = new Vector2(-1f, -1f);
        bool _built;

        public UiBracketVisual Visual => _visual;
        public int Frame => _frame < 0 ? 0 : _frame;

        /// <summary>대상(target) 위에 괄호를 만든다. 대상의 마지막 자식으로 붙고 대상 크기를 따라간다.</summary>
        public static UiSelectionBracket Attach(GameContext ctx, RectTransform target)
        {
            var go = new GameObject("Bracket", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(target, false);
            UiKit.Stretch(rt);
            var b = go.AddComponent<UiSelectionBracket>();
            b.Build(ctx);
            b.Set(default);
            return b;
        }

        void Build(GameContext ctx)
        {
            if (_built) return;
            _built = true;
            for (int i = 0; i < _outline.Length; i++)
            {
                _outline[i] = Bar("Outline" + i, UiTheme.Ink, out _);
            }
            for (int i = 0; i < _line.Length; i++)
            {
                _line[i] = Bar("Line" + i, UiTheme.SelectYellow, out _lineImg[i]);
            }
            var checkGo = new GameObject("Check", typeof(RectTransform), typeof(UImage));
            _check = (RectTransform)checkGo.transform;
            _check.SetParent(transform, false);
            _check.anchorMin = _check.anchorMax = new Vector2(1f, 1f);
            _check.pivot = new Vector2(0.5f, 0.5f);
            var ci = checkGo.GetComponent<UImage>();
            ci.raycastTarget = false;
            UiSprite s = UiKit.KitSprite(ctx, "K-41", "confirmation");
            if (s.IsValid) { ci.sprite = s.Sprite; ci.color = Color.white; _check.sizeDelta = new Vector2(s.Width > 0 ? s.Width : 16, s.Height > 0 ? s.Height : 16); }
            else { ci.color = UiTheme.SelectYellow; _check.sizeDelta = new Vector2(12f, 12f); }
        }

        RectTransform Bar(string name, Color c, out UImage img)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = Vector2.zero;
            img = go.GetComponent<UImage>();
            img.color = c;
            img.raycastTarget = false;
            return rt;
        }

        /// <summary>그릴 모습을 정한다(UiStateRules.Bracket의 결과).</summary>
        public void Set(UiBracketVisual v)
        {
            bool startPulse = v.Pulsing && !_visual.Pulsing;
            _visual = v;
            if (startPulse) _time = 0f;
            gameObject.SetActive(v.Visible);
            if (!v.Visible) return;
            transform.SetAsLastSibling();
            var col = v.Tone == UiBracketTone.Disabled ? UiTheme.SlateBlue : UiTheme.SelectYellow;
            for (int i = 0; i < _lineImg.Length; i++) if (_lineImg[i] != null) _lineImg[i].color = col;
            if (_check != null) _check.gameObject.SetActive(v.ConfirmCheck);
            _frame = -1;
            Layout(true);
        }

        void Update()
        {
            if (!_visual.Visible) return;
            if (_visual.Pulsing) _time += Time.unscaledDeltaTime;
            Layout(false);
        }

        void Layout(bool force)
        {
            var rt = (RectTransform)transform;
            Vector2 size = rt.rect.size;
            int frame = _visual.Pulsing ? UiBrackets.PulseFrameAt(_time) : 0;
            if (!force && frame == _frame && size == _lastSize) return;
            _frame = frame;
            _lastSize = size;
            int arm = UiBrackets.ArmPx(UiBrackets.SizeFor(size.x, size.y));
            int gap = UiBrackets.GapAt(frame);
            const int L = UiBrackets.LineWidthPx, O = UiBrackets.OutlinePx;
            float hw = size.x * 0.5f, hh = size.y * 0.5f;
            for (int c = 0; c < Corners; c++)
            {
                float sx = (c == 0 || c == 2) ? -1f : 1f;
                float sy = (c == 0 || c == 1) ? 1f : -1f;
                var p = new Vector2(sx * (hw + gap + L), sy * (hh + gap + L));
                Rect hBar = Span(p, -sx * arm, -sy * L);
                Rect vBar = Span(p, -sx * L, -sy * arm);
                Apply(_line[c * 2], hBar);
                Apply(_line[c * 2 + 1], vBar);
                Apply(_outline[c * 2], Grow(hBar, O));
                Apply(_outline[c * 2 + 1], Grow(vBar, O));
            }
            if (_check != null)
            {
                _check.anchoredPosition = new Vector2(gap, gap);
            }
        }

        static Rect Span(Vector2 p, float dx, float dy) =>
            new Rect(Mathf.Min(p.x, p.x + dx), Mathf.Min(p.y, p.y + dy), Mathf.Abs(dx), Mathf.Abs(dy));

        static Rect Grow(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2f * by, r.height + 2f * by);

        static void Apply(RectTransform rt, Rect r)
        {
            if (rt == null) return;
            rt.anchoredPosition = r.position;
            rt.sizeDelta = r.size;
        }
    }
}
