using UnityEngine;
using UnityEngine.UI;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// 전체 화면 효과 층(10 3.1절 15층: 번쩍, 피격 가장자리). HUD 캔버스 루트(vc.HudRoot)의 맨 위 자식으로 uGUI Image를 둔다.
    /// - 피격 가장자리: 위험색 띠(폭 80px, 40%, 화면 면적 약 22% — 25% 미만)가 0.3초에 사라진다. 줄일 수 없다(10 3.10절).
    ///   위험색이 허용된 세 곳 중 하나(00 문서 7절).
    /// - 번쩍: 번쩍 관리기(FlashGovernor)를 거친 것만. 켜짐 ≤ 0.05초, 꺼짐 ≥ 0.10초.
    /// - 테두리 강조: 흰 선 8px, 0.15초.
    /// </summary>
    public sealed class ScreenOverlay
    {
        RectTransform _root;
        Image[] _edge;
        Image _flash;
        Image[] _border;
        float _edgeLeft, _edgeTotal, _edgeOpacity;
        Color _edgeColor;
        float _flashAge, _flashOn, _flashOff, _flashOpacity;
        bool _flashActive;
        Color _flashColor;
        float _borderLeft, _borderTotal;

        public bool EdgeVisible => _edgeLeft > 0f;
        public float EdgeAlpha => _edgeTotal > 0f ? _edgeOpacity * Mathf.Clamp01(_edgeLeft / _edgeTotal) : 0f;
        public bool FlashVisible => _flashActive;
        public bool BorderVisible => _borderLeft > 0f;

        public void Build(RectTransform hudRoot, float edgePx, float borderPx)
        {
            Dispose();
            if (hudRoot == null) return;
            var go = new GameObject("FxScreenOverlay", typeof(RectTransform));
            _root = (RectTransform)go.transform;
            _root.SetParent(hudRoot, false);
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            _edge = Frame(_root, "HitEdge", edgePx);
            _border = Frame(_root, "Border", borderPx);
            _flash = Img(_root, "Flash");
            Stretch(_flash.rectTransform, 0f, 0f, 0f, 0f, Vector2.zero, Vector2.one);
            SetVisible(_edge, false);
            SetVisible(_border, false);
            _flash.enabled = false;
        }

        static Image Img(RectTransform parent, string name)
        {
            var g = new GameObject(name, typeof(RectTransform));
            var img = g.AddComponent<Image>();
            img.raycastTarget = false;
            img.rectTransform.SetParent(parent, false);
            return img;
        }

        static void Stretch(RectTransform rt, float l, float b, float r, float t, Vector2 min, Vector2 max)
        {
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = new Vector2(l, b);
            rt.offsetMax = new Vector2(-r, -t);
        }

        /// <summary>네 변 띠(위·아래는 전체 폭, 좌우는 그 사이).</summary>
        static Image[] Frame(RectTransform parent, string name, float px)
        {
            var a = new Image[4];
            a[0] = Img(parent, name + "Top");
            Stretch(a[0].rectTransform, 0f, 0f, 0f, 0f, new Vector2(0f, 1f), Vector2.one);
            a[0].rectTransform.offsetMin = new Vector2(0f, -px);
            a[1] = Img(parent, name + "Bottom");
            Stretch(a[1].rectTransform, 0f, 0f, 0f, 0f, Vector2.zero, new Vector2(1f, 0f));
            a[1].rectTransform.offsetMax = new Vector2(0f, px);
            a[2] = Img(parent, name + "Left");
            Stretch(a[2].rectTransform, 0f, px, 0f, px, Vector2.zero, new Vector2(0f, 1f));
            a[2].rectTransform.offsetMax = new Vector2(px, -px);
            a[3] = Img(parent, name + "Right");
            Stretch(a[3].rectTransform, 0f, px, 0f, px, new Vector2(1f, 0f), Vector2.one);
            a[3].rectTransform.offsetMin = new Vector2(-px, px);
            return a;
        }

        static void SetVisible(Image[] imgs, bool on)
        {
            if (imgs == null) return;
            for (int i = 0; i < imgs.Length; i++) if (imgs[i] != null) imgs[i].enabled = on;
        }

        static void SetColor(Image[] imgs, Color c)
        {
            if (imgs == null) return;
            for (int i = 0; i < imgs.Length; i++) if (imgs[i] != null) imgs[i].color = c;
        }

        public void HitEdge(Color color, float opacity, float seconds)
        {
            _edgeColor = color;
            _edgeOpacity = opacity;
            _edgeTotal = seconds;
            _edgeLeft = seconds;
        }

        public void Flash(Color color, FlashResult r)
        {
            if (r.Kind == FlashKind.Border) { Border(r.BorderSeconds); return; }
            if (r.Kind != FlashKind.Full && r.Kind != FlashKind.Local) return;
            _flashColor = color;
            _flashOpacity = r.Opacity;
            _flashOn = r.OnSeconds;
            _flashOff = r.OffSeconds;
            _flashAge = 0f;
            _flashActive = true;
        }

        public void Border(float seconds)
        {
            _borderTotal = seconds;
            _borderLeft = seconds;
        }

        public void Update(float dt)
        {
            // 화면 덮개(피격 가장자리·번쩍·테)는 실제 시간으로 사라진다. 일시정지·그림 정지 동안 표시 층은 dt 0으로 그려지는데,
            // 그러면 0.3초짜리 위험색 가장자리나 번쩍이 일시정지 화면 뒤에 몇 초씩 굳어 남는다(통합 단계 3에서 화면으로 확인).
            dt = Mathf.Max(dt, Time.unscaledDeltaTime);
            if (_root != null) _root.SetAsLastSibling();
            if (_edgeLeft > 0f)
            {
                _edgeLeft -= dt;
                var c = _edgeColor;
                c.a = EdgeAlpha;
                SetColor(_edge, c);
                SetVisible(_edge, _edgeLeft > 0f);
            }
            if (_flashActive && _flash != null)
            {
                _flashAge += dt;
                float a;
                if (_flashAge <= _flashOn) a = _flashOn > 0f ? _flashOpacity * (_flashAge / _flashOn) : _flashOpacity;
                else a = _flashOff > 0f ? _flashOpacity * Mathf.Clamp01(1f - (_flashAge - _flashOn) / _flashOff) : 0f;
                if (_flashAge >= _flashOn + _flashOff) { _flashActive = false; a = 0f; }
                var c = _flashColor;
                c.a = a;
                _flash.color = c;
                _flash.enabled = _flashActive;
            }
            if (_borderLeft > 0f)
            {
                _borderLeft -= dt;
                var c = Color.white;
                c.a = _borderTotal > 0f ? Mathf.Clamp01(_borderLeft / _borderTotal) : 0f;
                SetColor(_border, c);
                SetVisible(_border, _borderLeft > 0f);
            }
        }

        public void Dispose()
        {
            if (_root != null) FxUtil.Destroy(_root.gameObject);
            _root = null;
            _edge = _border = null;
            _flash = null;
            _edgeLeft = _borderLeft = 0f;
            _flashActive = false;
        }
    }
}
