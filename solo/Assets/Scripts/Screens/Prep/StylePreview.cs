using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Prep
{
    /// <summary>
    /// 기본 공격 유형 미리보기(원정 준비 카드 안의 작은 상자, 반복). 그 유형의 실제 그림(effects/styles 등)을 DataSolo/StylePreview.json의
    /// 줄대로 움직여 보여 준다 — 포탄은 포물선으로 날아 터지고, 불꽃은 뿜고, 사슬은 옮겨 붙고, 책은 갔다 돌아온다.
    /// 표시 전용(규칙·판정과 무관). 화면의 Tick이 실제 시간을 넘긴다. 그림이 없는 줄은 건너뛴다.
    /// </summary>
    public sealed class StylePreview
    {
        public const string DataName = "StylePreview";

        sealed class Step
        {
            public SpriteAnim Anim;
            public float At, Dur, Scale = 1f, ScaleTo, Rot, RotTo, Arc, Fade, Alpha = 1f, Fps, Orbit;
            public bool HasRotTo, Travel, Loop, Stretch, Flip;
            public Vector2 From, To;
            public UImage Img;
        }

        readonly GameContext _ctx;
        readonly RectTransform _box;
        readonly List<Step> _steps = new List<Step>();
        readonly List<UImage> _pool = new List<UImage>();
        JObject _data;
        float _t, _cycle;
        string _id;

        public RectTransform Box => _box;
        public string CurrentId => _id;
        public int VisibleCount
        {
            get { int n = 0; for (int i = 0; i < _steps.Count; i++) if (_steps[i].Img != null && _steps[i].Img.enabled) n++; return n; }
        }

        public StylePreview(GameContext ctx, RectTransform parent, float x, float y, float w, float h)
        {
            _ctx = ctx;
            _box = UiKit.Node(parent, "StylePreview", x, y, w, h);
            _box.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            try { _data = ctx != null && ctx.Content != null ? ctx.Content.SoloExtra(DataName) : null; }
            catch (System.Exception) { _data = null; }
        }

        /// <summary>그 유형의 미리보기로 바꾼다(같은 유형이면 그대로 이어 간다). null이면 비운다.</summary>
        public void Show(string blockId)
        {
            if (blockId == _id) return;
            _id = blockId;
            _t = 0f;
            for (int i = 0; i < _steps.Count; i++) if (_steps[i].Img != null) _steps[i].Img.enabled = false;
            _steps.Clear();
            _cycle = 0f;
            var arr = blockId != null && _data != null ? _data["styles"]?[blockId] as JArray : null;
            if (arr == null || _ctx == null || _ctx.Art == null) return;
            int used = 0;
            foreach (var tok in arr)
            {
                var o = tok as JObject;
                if (o == null) continue;
                var anim = Anim((string)o["anim"]);
                if (anim == null || anim.Frames == null || anim.Frames.Length == 0) continue;
                var s = new Step
                {
                    Anim = anim, At = F(o, "at"), Dur = Mathf.Max(0.05f, F(o, "dur")), Scale = F(o, "scale", 1f), Rot = F(o, "rot"),
                    Arc = F(o, "arc"), Fade = F(o, "fade"), Alpha = F(o, "alpha", 1f), Fps = F(o, "fps"), Orbit = F(o, "orbit"),
                    Travel = B(o, "travel"), Loop = B(o, "loop"), Stretch = B(o, "stretch"), Flip = B(o, "flip"),
                    From = V(o, "from"), To = o["to"] != null ? V(o, "to") : V(o, "from")
                };
                s.ScaleTo = o["scale_to"] != null ? F(o, "scale_to") : s.Scale;
                s.HasRotTo = o["rot_to"] != null;
                s.RotTo = s.HasRotTo ? F(o, "rot_to") : s.Rot;
                if (used >= _pool.Count)
                {
                    var img = UiKit.Image(_box, null, 0f, 0f, 8f, 8f, null, false);
                    img.raycastTarget = false;
                    _pool.Add(img);
                }
                s.Img = _pool[used++];
                s.Img.enabled = false;
                _steps.Add(s);
                _cycle = Mathf.Max(_cycle, s.At + s.Dur);
            }
            _cycle += _data["pause"] != null ? (float)_data["pause"] : 0.35f;
            Tick(0f);
        }

        public void Tick(float dt)
        {
            if (_steps.Count == 0 || _cycle <= 0f) return;
            _t += Mathf.Max(0f, dt);
            if (_t >= _cycle) _t %= _cycle;
            for (int i = 0; i < _steps.Count; i++) Apply(_steps[i], _t);
        }

        void Apply(Step s, float t)
        {
            var img = s.Img;
            float local = t - s.At;
            if (local < 0f || local >= s.Dur) { img.enabled = false; return; }
            float k = Mathf.Clamp01(local / s.Dur);
            int n = s.Anim.Frames.Length;
            float fps = s.Fps > 0f ? s.Fps : (s.Anim.Fps > 0f ? s.Anim.Fps : 12f);
            int fi = (int)(local * fps);
            fi = s.Loop ? fi % n : Mathf.Min(fi, n - 1);
            var spr = s.Anim.Frames[Mathf.Clamp(fi, 0, n - 1)];
            if (spr == null) { img.enabled = false; return; }
            img.enabled = true;
            img.sprite = spr;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            float scale = Mathf.Lerp(s.Scale, s.ScaleTo, k);
            Vector2 size = spr.rect.size;
            float ppuScale = spr.pixelsPerUnit > 0f ? 64f / spr.pixelsPerUnit : 1f; // 해상도 배수 그림(PPU 128)은 화면 크기를 같게
            size *= ppuScale;
            Vector2 pos;
            float rot;
            if (s.Stretch)
            {
                Vector2 d = s.To - s.From;
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(d.magnitude, size.y * scale);
                pos = s.From;
                rot = Mathf.Atan2(-d.y, d.x) * Mathf.Rad2Deg;
                rt.localScale = new Vector3(s.Flip ? -1f : 1f, 1f, 1f);
            }
            else
            {
                rt.pivot = new Vector2(spr.pivot.x / Mathf.Max(1f, spr.rect.width), spr.pivot.y / Mathf.Max(1f, spr.rect.height));
                rt.sizeDelta = size * scale;
                rt.localScale = new Vector3(s.Flip ? -1f : 1f, 1f, 1f);
                if (s.Orbit > 0f)
                {
                    float e = 1f - (1f - k) * (1f - k);
                    float th = Mathf.Lerp(s.Rot, s.RotTo, e);
                    float r = th * Mathf.Deg2Rad;
                    pos = s.From + new Vector2(Mathf.Cos(r), -Mathf.Sin(r)) * s.Orbit;
                    rot = th + 180f;
                }
                else
                {
                    pos = Vector2.Lerp(s.From, s.To, k) + new Vector2(0f, -s.Arc * 4f * k * (1f - k));
                    rot = s.HasRotTo ? Mathf.Lerp(s.Rot, s.RotTo, k) : s.Rot;
                    if (s.Travel)
                    {
                        float vx = s.To.x - s.From.x;
                        float vyDown = (s.To.y - s.From.y) - s.Arc * 4f * (1f - 2f * k);
                        rot = Mathf.Atan2(-vyDown, vx) * Mathf.Rad2Deg;
                    }
                }
            }
            rt.anchoredPosition = new Vector2(pos.x, -pos.y);
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);
            float a = s.Alpha;
            if (s.Fade > 0f && s.Dur - local < s.Fade) a *= Mathf.Clamp01((s.Dur - local) / s.Fade);
            img.color = new Color(1f, 1f, 1f, a);
        }

        SpriteAnim Anim(string spec)
        {
            if (string.IsNullOrEmpty(spec)) return null;
            int slash = spec.IndexOf('/');
            if (slash <= 0) return null;
            string group = spec.Substring(0, slash), key = spec.Substring(slash + 1);
            try
            {
                if (_ctx.Art.EffectInfo(group, key) == null) return null;
                return _ctx.Art.Effect(group, key);
            }
            catch (System.Exception) { return null; }
        }

        static float F(JObject o, string k, float d = 0f)
        {
            var t = o[k];
            return t != null && (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) ? (float)t : d;
        }

        static bool B(JObject o, string k)
        {
            var t = o[k];
            return t != null && t.Type == JTokenType.Boolean && (bool)t;
        }

        static Vector2 V(JObject o, string k)
        {
            var a = o[k] as JArray;
            return a != null && a.Count >= 2 ? new Vector2((float)a[0], (float)a[1]) : Vector2.zero;
        }
    }
}
