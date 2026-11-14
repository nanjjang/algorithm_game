using System;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// HUD가 쓰는 그림 찾기와 uGUI 조립 도우미. 그림 창고(ctx.Art)가 없거나 그림이 없으면 null을 돌려주고,
    /// 부르는 쪽이 단색 도형으로 대신 그린다(09 요약서: 없는 것은 단순 도형으로). 예외를 내지 않는다.
    /// </summary>
    public sealed class HudArt
    {
        public readonly GameContext Ctx;
        public HudArt(GameContext ctx) { Ctx = ctx; }

        IArtLibrary Lib => Ctx != null ? Ctx.Art : null;

        /// <summary>그림 창고(없으면 null). 공통 HUD 그림(UiCommonArt)을 읽을 때 넘긴다.</summary>
        public IArtLibrary Library => Lib;

        /// <summary>효과 묶음 그림(manifest에 있을 때만). 없으면 null.</summary>
        public SpriteAnim Effect(string group, string key)
        {
            var art = Lib;
            if (art == null) return null;
            try
            {
                if (art.EffectInfo(group, key) == null) return null;
                var a = art.Effect(group, key);
                return a != null && a.Frames != null && a.Frames.Length > 0 ? a : null;
            }
            catch (Exception) { return null; }
        }

        public Sprite EffectFrame(string group, string key, int frame)
        {
            var a = Effect(group, key);
            if (a == null) return null;
            if (frame < 0) frame = 0;
            if (frame >= a.Frames.Length) frame = a.Frames.Length - 1;
            return a.Frames[frame];
        }

        /// <summary>status/ 묶음(HUD·상태 그림, 09 요약서 5.7절).</summary>
        public Sprite Status(string key, int frame = 0) => EffectFrame("status", key, frame);
        public SpriteAnim StatusAnim(string key) => Effect("status", key);

        /// <summary>UI 키트·화면 그림(09 요약서 6.3절). 없으면 IsValid = false.</summary>
        public UiSprite Kit(string id, string variant, string state = null) => UiKit.KitSprite(Ctx, id, variant, state);

        public SpriteAnim KitAnim(string id, string variant, string state = null, int sizePx = 0)
        {
            var art = Lib;
            if (art == null) return null;
            try
            {
                if (art.UiInfo(id, variant, state, sizePx) == null) return null;
                var a = art.UiAnim(id, variant, state, sizePx);
                return a != null && a.Frames != null && a.Frames.Length > 0 ? a : null;
            }
            catch (Exception) { return null; }
        }

        public UiSprite KitSized(string id, string variant, string state, int sizePx)
        {
            var art = Lib;
            if (art == null) return default;
            try { return art.UiSized(id, variant, state, sizePx); } catch (Exception) { return default; }
        }

        /// <summary>콘텐츠 아이콘(블록·속성·재화·보스). 그림이 없으면 창고가 대체 그림을 준다. 창고가 없으면 null.</summary>
        public Sprite Icon(string contentId, int sizePx)
        {
            var art = Lib;
            if (art == null || string.IsNullOrEmpty(contentId)) return null;
            try { return art.Icon(contentId, sizePx); } catch (Exception) { return null; }
        }

        public Sprite Portrait(string charId)
        {
            var art = Lib;
            if (art == null || string.IsNullOrEmpty(charId)) return null;
            try { return art.CharacterPortrait(charId, 0); } catch (Exception) { return null; }
        }

        public Font Font(UiFontId size) => UiKit.FontFor(Ctx, size);

        public Sprite WhitePixel
        {
            get
            {
                var art = Lib;
                if (art == null) return null;
                try { return art.WhitePixel; } catch (Exception) { return null; }
            }
        }

        public Material Material(SpriteMaterialKind kind)
        {
            var art = Lib;
            if (art == null) return null;
            try { return art.Material(kind); } catch (Exception) { return null; }
        }

        // ------------------------------------------------------------ uGUI 조립

        /// <summary>왼쪽 위 원점 자리의 그림 칸(sprite가 없으면 fallback 단색).</summary>
        public static UImage Image(RectTransform parent, string name, float x, float y, float w, float h, Sprite sprite, Color fallback)
        {
            var img = UiKit.Rect(parent, x, y, w, h, fallback, false, name);
            SetSprite(img, sprite, fallback);
            return img;
        }

        public static void SetSprite(UImage img, Sprite sprite, Color fallback)
        {
            if (img == null) return;
            if (img.sprite == sprite && sprite != null) return;
            img.sprite = sprite;
            img.type = UImage.Type.Simple;
            img.color = sprite != null ? Color.white : fallback;
        }

        public static void SetKit(UImage img, UiSprite sp, Color fallback)
        {
            if (img == null) return;
            if (sp.IsValid)
            {
                if (img.sprite != sp.Sprite) UiKit.SetSprite(img, sp);
                img.color = Color.white;
            }
            else
            {
                img.sprite = null;
                img.color = fallback;
            }
        }

        /// <summary>
        /// 부모 가운데를 원점으로 (dx, dy) px(오른쪽 +, 아래 +)에 w×h 칸의 왼쪽 위를 둔다. 캐릭터 둘레 표시(발밑이 부모 가운데)용.
        /// </summary>
        public static RectTransform PlaceRel(RectTransform rt, float dx, float dy, float w, float h)
        {
            if (rt == null) return null;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(dx, -dy);
            return rt;
        }

        public static UImage ImageRel(RectTransform parent, string name, float dx, float dy, float w, float h, Sprite sprite, Color fallback)
        {
            var img = UiKit.Rect(parent, 0, 0, w, h, fallback, false, name);
            PlaceRel(img.rectTransform, dx, dy, w, h);
            SetSprite(img, sprite, fallback);
            return img;
        }

        public static RectTransform NodeRel(RectTransform parent, string name, float dx, float dy, float w, float h)
        {
            var rt = UiKit.Node(parent, name, 0, 0, w, h);
            return PlaceRel(rt, dx, dy, w, h);
        }

        /// <summary>
        /// 전장 위 오버레이 판: 고정점(ax, ay; ay 위 0 · 아래 1) 기준 (x, y)에 w×h. 그림자 + 반투명 먹색 판(K-02 그림이 있으면 그 꼴) +
        /// 위쪽 빛줄. 돌려주는 것은 판 안(내용을 놓을 노드, 왼쪽 위 원점).
        /// </summary>
        public RectTransform Plate(RectTransform parent, string name, float ax, float ay, float x, float y, float w, float h, float alpha = 0.86f)
        {
            var root = UiKit.Node(parent, name);
            UiKit.PlaceAnchored(root, ax, ay, x, y, w, h);
            var shadow = UiKit.Rect(root, HudOverlayLayout.ShadowPx, HudOverlayLayout.ShadowPx, w, h, UiTheme.Shadow, false, "Shadow");
            var body = UiKit.Rect(root, 0, 0, w, h, UiTheme.WithAlpha(UiTheme.Ink, alpha), false, "Body");
            var sp = Kit("K-02", "recess");
            var common = UiCommonArt.PanelSprite(Lib);
            if (common != null)
            {
                // 공통 HUD 판(금 테 9조각, 네 변 12px). 그림자는 같은 꼴의 먹색.
                UiCommonArt.ApplySliced(body, common);
                body.color = UiTheme.WithAlpha(Color.white, Mathf.Clamp01(alpha + 0.08f));
                UiCommonArt.ApplySliced(shadow, common);
                shadow.color = UiTheme.Shadow;
            }
            else if (sp.IsValid)
            {
                UiKit.SetSprite(body, sp);
                body.color = UiTheme.WithAlpha(Color.white, alpha);
                UiKit.SetSprite(shadow, sp);
                shadow.color = UiTheme.Shadow;
            }
            UiKit.Rect(root, 8f, 3f, Mathf.Max(0f, w - 16f), 1f, UiTheme.Sheen, false, "Sheen");
            return root;
        }

        /// <summary>고정점 배치 노드(판 없이).</summary>
        public static RectTransform Anchored(RectTransform parent, string name, float ax, float ay, float x, float y, float w, float h)
        {
            var rt = UiKit.Node(parent, name);
            return UiKit.PlaceAnchored(rt, ax, ay, x, y, w, h);
        }

        /// <summary>
        /// 튀기 연출용 숫자 칸: 축이 (px, py)인 노드 안에 글자 칸을 꽉 채운다(배율이 그 점을 중심으로 커진다). 노드의 왼쪽 위 = (x, y).
        /// </summary>
        public static UiLabel PopLabel(GameContext ctx, RectTransform parent, UiFontId size, float x, float y, float w, float h,
            Color color, TextAnchor align, float px, float py)
        {
            var node = UiKit.Node(parent, "PopNode", x, y, w, h);
            node.pivot = new Vector2(px, py);
            node.anchoredPosition = new Vector2(x + w * px, -(y + h * (1f - py)));
            var l = UiKit.Label(ctx, node, "", size, 0, 0, w, h, color, align, false, true);
            UiKit.Stretch(l.Rect);
            return l;
        }

        public static void SetActive(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }

        public static void Destroy(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }

    /// <summary>글을 바꿀 때만 SetText를 부르는 얇은 감싸개(매 프레임 같은 글을 다시 자르지 않는다).</summary>
    public sealed class HudText
    {
        public readonly UiLabel Label;
        string _last;
        public HudText(UiLabel label) { Label = label; }
        public void Set(string s)
        {
            s = s ?? string.Empty;
            if (s == _last || Label == null) return;
            _last = s;
            Label.SetText(s);
        }
        public string Current => _last;
        public void Show(bool on) { if (Label != null) HudArt.SetActive(Label, on); }
        public Color Color { set { if (Label != null) Label.Color = value; } }
    }
}
