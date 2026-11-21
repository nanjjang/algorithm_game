using System;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;
using UInputField = UnityEngine.UI.InputField;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.Screens.Online
{
    /// <summary>
    /// 온라인 화면 공용 부품(docs/ui-assets.md 1~2절, 4.3·4.5·4.20~4.25절의 그림을 그대로 쓴다). 화면은 "판 하나를 가운데" 대신
    /// 장면 그림(야영지·결투장·장터 거리·탑 벽) 위에 비대칭으로 놓고, 글보다 그림·아이콘·숫자로 읽게 한다.
    /// 그림이 없으면(테스트·대체) 단색으로 그린다 — 새 그림은 만들지 않는다.
    /// </summary>
    public static class OnlineUi
    {
        public const float Edge = 48f, Band = 80f;

        // ---- 격자와 글자 크기(1920×1080, docs/ui-assets.md 1.1: 8px 격자, 가장자리 48) ----
        /// <summary>12칸 격자: 바깥 여백 48, 칸 사이 24, 한 칸 130.</summary>
        public const float Gutter = 24f, Col = 130f;
        /// <summary>n칸 너비(사이 여백 포함).</summary>
        public static float Span(int n) => n * Col + (n - 1) * Gutter;
        /// <summary>k번째 칸(0부터)의 왼쪽 x.</summary>
        public static float ColX(int k) => Edge + k * (Col + Gutter);

        // ---- 받아 올 그림(Docs/15-online-asset-requests.md): Resources/Art/online/&lt;이름&gt;.png. 없으면 null ----
        static readonly System.Collections.Generic.Dictionary<string, Sprite> _online = new System.Collections.Generic.Dictionary<string, Sprite>();

        public static Sprite Online(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_online.TryGetValue(name, out var s)) return s;
            try { s = Resources.Load<Sprite>("Art/online/" + name); } catch (Exception) { s = null; }
            _online[name] = s;
            return s;
        }

        /// <summary>받아 온 그림이 있으면 그것을, 없으면 null(부르는 쪽이 지금 그림으로 대신한다).</summary>
        public static UImage OnlineImg(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var s = Online(name);
            if (s == null) return null;
            var img = UiKit.Image(parent, s, x, y, w, h, Color.white, false);
            img.name = name;
            return img;
        }

        public static Sprite Sprite(GameContext ctx, string id, string variant, string state = null)
        {
            if (ctx == null || ctx.Art == null) return null;
            var sp = ctx.Art.Ui(id, variant, state);
            return sp.IsValid ? sp.Sprite : null;
        }

        /// <summary>그림 한 장. w·h가 0이면 그림 크기(1배).</summary>
        public static UImage Img(GameContext ctx, RectTransform parent, string id, string variant, float x, float y, float w = 0f, float h = 0f, string state = null, Color? tint = null)
        {
            Sprite s = null;
            int nw = 0, nh = 0;
            if (ctx != null && ctx.Art != null)
            {
                var sp = ctx.Art.Ui(id, variant, state);
                if (sp.IsValid) { s = sp.Sprite; nw = sp.Width; nh = sp.Height; }
            }
            if (w <= 0f) w = nw > 0 ? nw : 32f;
            if (h <= 0f) h = nh > 0 ? nh : 32f;
            var img = UiKit.Image(parent, s, x, y, w, h, s != null ? tint ?? Color.white : UiTheme.WithAlpha(UiTheme.WoodLight, 0.6f), false);
            img.name = id + (variant != null ? "/" + variant : string.Empty);
            return img;
        }

        /// <summary>9조각 그림(판, 이름판, 말풍선, 순위 줄).</summary>
        public static UImage Nine(GameContext ctx, RectTransform parent, string id, string variant, float x, float y, float w, float h, string state = null, Color? fallback = null)
            => UiKit.NineSlice(ctx, parent, id, variant, state, x, y, w, h, fallback ?? UiTheme.Wood);

        /// <summary>
        /// 장면 바탕: 화면 전체 그림 + 먹색 덮개(dim 0~1). 아래쪽을 조금 더 어둡게 해 글자가 읽히게 한다.
        /// </summary>
        public static RectTransform Scene(GameContext ctx, RectTransform parent, string id, string variant, float dim, string onlineBg = null)
        {
            // 반응형: 장면 칸은 창 끝까지(UiFullBleed), 단색·덮개는 칸에 붙여 늘이고, 그림은 비율을 지켜 덮는다(가운데에서 잘림).
            var node = UiKit.Node(parent, "Scene", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            UiFullBleed.Fill(node);
            UiKit.Stretch(UiKit.Rect(node, 0f, 0f, 0f, 0f, UiTheme.WoodDark, true, "Background").rectTransform);
            var custom = Online(onlineBg);
            if (custom != null)
            {
                UiFullBleed.Cover(UiKit.Image(node, custom, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, Color.white, false));
                if (dim > 0f) UiKit.Stretch(UiKit.Rect(node, 0f, 0f, 0f, 0f, UiTheme.WithAlpha(UiTheme.Ink, dim * 0.5f), false, "Dim").rectTransform);
                return node;
            }
            // UI 그림(S-xxxx)은 manifest에서, 그 밖의 열쇠("enhance.workbench" 등)는 배경에서 찾는다(없는 UI id는 바둑판 대체 그림이 나오므로 섞지 않는다).
            Sprite s = id != null && id.StartsWith("S-", StringComparison.Ordinal) ? Sprite(ctx, id, variant) : null;
            if (s == null && ctx != null && ctx.Art != null) { try { s = ctx.Art.Background(id); } catch (Exception) { s = null; } }
            if (s != null) UiFullBleed.Cover(UiKit.Image(node, s, 0f, 0f, UiScale.RefWidth, UiScale.RefHeight, Color.white, false));
            if (dim > 0f) UiKit.Stretch(UiKit.Rect(node, 0f, 0f, 0f, 0f, UiTheme.WithAlpha(UiTheme.Ink, dim), false, "Dim").rectTransform);
            UiBackdrop.VignetteOver(node, 0.6f);
            return node;
        }

        /// <summary>위 띠(K-05, 1920×80): 왼쪽에 아이콘 + 제목(F-32). 오른쪽은 부르는 쪽이 채운다.</summary>
        public static UiLabel HeaderBand(GameContext ctx, RectTransform parent, string iconId, string iconVariant, string title, out RectTransform band)
        {
            // 띠 판은 좌우·위 끝까지(UiFullBleed), 안의 아이콘·제목·단추 자리(band)는 1920 안전 영역 그대로.
            var img = Nine(ctx, parent, "K-05", null, 0f, 0f, UiScale.RefWidth, Band);
            img.name = "HeaderBandBg";
            UiFullBleed.Fill(img.rectTransform, UiBleedEdges.Horizontal | UiBleedEdges.Top);
            band = UiKit.Node(parent, "K-05", 0f, 0f, UiScale.RefWidth, Band);
            float x = Edge;
            if (iconId != null)
            {
                Img(ctx, band, iconId, iconVariant, x, (Band - 32f) * 0.5f, 32f, 32f);
                x += 44f;
            }
            var l = UiKit.Label(ctx, band, title, UiFontId.F32, x, 0f, 760f, Band, UiTheme.Chalk, TextAnchor.MiddleLeft);
            UiKit.DropShadow(l);
            return l;
        }

        /// <summary>아이콘 + 글 한 줄(재화·기록 칩). 돌려주는 것은 글.</summary>
        public static UiLabel IconText(GameContext ctx, RectTransform parent, string iconId, string iconVariant, string text, float x, float y, float w, float h,
            UiFontId font = UiFontId.F24, Color? color = null, float iconSize = 24f)
        {
            Img(ctx, parent, iconId, iconVariant, x, y + (h - iconSize) * 0.5f, iconSize, iconSize);
            return UiKit.Label(ctx, parent, text, font, x + iconSize + 8f, y, w - iconSize - 8f, h, color ?? UiTheme.Chalk, TextAnchor.MiddleLeft);
        }

        /// <summary>꼬리표: 색 띠 위 먹색 작은 글("지금은 보낼 수 없음", "봇", "이메일 계정"). K-46 그림은 어두워 색을 입히면 글이 묻혀서 단색 띠로 그린다.</summary>
        public static UiLabel Tag(GameContext ctx, RectTransform parent, string text, float x, float y, Color tint, float w = 0f)
        {
            if (w <= 0f) w = Mathf.Max(48f, UiTextFit.WidthPx(text, 12) + 20f);
            var bg = UiKit.Rect(parent, x, y, w, 24f, tint, false, "Tag");
            UiKit.Rect(bg.rectTransform, 0f, 22f, w, 2f, UiTheme.WithAlpha(UiTheme.Ink, 0.5f), false, "TagShade");
            return UiKit.Label(ctx, bg.rectTransform, text, UiFontId.F12, 0f, 0f, w, 22f, UiTheme.Ink, TextAnchor.MiddleCenter);
        }

        /// <summary>여러 프레임 그림을 돌린다(모닥불·모래시계·VS). 시간은 화면 시계(unscaled).</summary>
        public static UiFrameAnim Animate(UImage img, SpriteAnim anim, float fps = 0f, bool loop = true)
        {
            if (img == null || anim == null || anim.Frames == null || anim.Frames.Length == 0) return null;
            var a = img.gameObject.GetComponent<UiFrameAnim>() ?? img.gameObject.AddComponent<UiFrameAnim>();
            a.Frames = anim.Frames;
            a.Fps = fps > 0f ? fps : (anim.Fps > 0f ? anim.Fps : 6f);
            a.Loop = loop;
            img.sprite = anim.Frames[0];
            return a;
        }

        public static UImage AnimImg(GameContext ctx, RectTransform parent, string id, string variant, float x, float y, float w, float h, float fps = 0f, bool loop = true)
        {
            var img = UiKit.Image(parent, null, x, y, w, h, Color.white, false);
            img.name = id + "/" + variant;
            var anim = ctx != null && ctx.Art != null ? ctx.Art.UiAnim(id, variant) : null;
            if (Animate(img, anim, fps, loop) == null) img.color = UiTheme.WithAlpha(UiTheme.WoodLight, 0.6f);
            return img;
        }

        /// <summary>
        /// 입력 칸(K-33). 위에 작은 이름(F-16 회청)을 두고, 칸 안은 쪽지 글자색. password면 ● 가림 + 눈 단추(S-0302)를 오른쪽에.
        /// </summary>
        public static UInputField Field(GameContext ctx, UiFocusGroup focus, RectTransform parent, string caption, string hint, float x, float y, float w,
            int limit, bool password = false, UiFontId font = UiFontId.F24, Color? captionColor = null)
        {
            const float H = 48f;
            // captionColor: 쪽지·장부 그림 위면 UiTheme.PaperMuted(회청은 밝은 바탕에서 1.6:1).
            if (caption != null) UiKit.Label(ctx, parent, caption, UiFontId.F16, x + 2f, y, w, 24f, captionColor ?? UiTheme.SlateBlue, TextAnchor.MiddleLeft);
            float fy = caption != null ? y + 26f : y;
            var bg = UiKit.NineSlice(ctx, parent, "K-33", "input", null, x, fy, w, H, UiTheme.Chalk);
            bg.raycastTarget = true;
            var field = bg.gameObject.AddComponent<UInputField>();
            float textW = password ? w - 64f : w - 24f;
            var text = UiKit.Label(ctx, bg.rectTransform, string.Empty, font, 12f, 0f, textW, H, UiTheme.PaperText, TextAnchor.MiddleLeft);
            var ut = text.GetComponent<UText>();
            ut.supportRichText = false;
            field.textComponent = ut;
            field.lineType = UInputField.LineType.SingleLine;
            field.characterLimit = limit;
            if (password)
            {
                field.contentType = UInputField.ContentType.Password;
                field.asteriskChar = '●';
            }
            var ph = UiKit.Label(ctx, bg.rectTransform, hint ?? string.Empty, UiFontId.F16, 12f, 0f, textW, H, UiTheme.WithAlpha(UiTheme.PaperText, 0.45f), TextAnchor.MiddleLeft);
            field.placeholder = ph.GetComponent<UText>();
            if (password)
            {
                var eyeOff = Sprite(ctx, "S-0302", "password_eye_hidden");
                var eyeOn = Sprite(ctx, "S-0302", "password_eye");
                UiButton eye = null;
                eye = UiKit.IconButton(ctx, bg.rectTransform, eyeOff, w - 48f, 8f, 32f, 32f, () =>
                {
                    bool show = field.contentType == UInputField.ContentType.Password;
                    field.contentType = show ? UInputField.ContentType.Standard : UInputField.ContentType.Password;
                    field.ForceLabelUpdate();
                    if (eye != null && eye.Icon != null) eye.Icon.sprite = show ? eyeOn : eyeOff;
                }, UiButtonStyle.Plain, null);
            }
            return field;
        }

        /// <summary>오류 줄: 경고 세모(I-F2) + 호박색 글. 빈 글이면 숨는다.</summary>
        public sealed class ErrorLine
        {
            public RectTransform Root;
            public UiLabel Text;
            public void Set(string s)
            {
                Root.gameObject.SetActive(!string.IsNullOrEmpty(s));
                Text.SetText(s ?? string.Empty);
            }
        }

        /// <summary>color: 쪽지·장부 그림 위면 UiTheme.PaperWarn(호박색은 밝은 바탕에서 1.2:1).</summary>
        public static ErrorLine Error(GameContext ctx, RectTransform parent, float x, float y, float w, Color? color = null)
        {
            var root = UiKit.Node(parent, "ErrorLine", x, y, w, 32f);
            Img(ctx, root, "I-F2", "warning", 0f, 4f, 24f, 24f);
            var t = UiKit.Label(ctx, root, string.Empty, UiFontId.F16, 32f, 0f, w - 32f, 32f, color ?? UiTheme.Amber, TextAnchor.MiddleLeft, wrap: true);
            var e = new ErrorLine { Root = root, Text = t };
            e.Set(null);
            return e;
        }

        /// <summary>캐릭터 큰 그림(192×192, world-characters.md). 없으면 null.</summary>
        public static Sprite CharacterArt(GameContext ctx, string charId)
        {
            if (ctx == null || ctx.Art == null || string.IsNullOrEmpty(charId)) return null;
            try { return ctx.Art.CharacterSelection(charId); } catch (Exception) { return null; }
        }

        public static Sprite Portrait(GameContext ctx, string charId)
        {
            if (ctx == null || ctx.Art == null || string.IsNullOrEmpty(charId)) return null;
            try { return ctx.Art.CharacterPortrait(charId, 0); } catch (Exception) { return null; }
        }
    }

    /// <summary>프레임 그림 돌리기(UI 전용, 화면 시계).</summary>
    public sealed class UiFrameAnim : MonoBehaviour
    {
        public Sprite[] Frames;
        public float Fps = 6f;
        public bool Loop = true;
        float _t;
        UImage _img;

        /// <summary>처음부터 다시(한 번만 도는 그림을 다시 보일 때).</summary>
        public void Restart() { _t = 0f; }

        void Update()
        {
            if (Frames == null || Frames.Length == 0) return;
            if (_img == null) _img = GetComponent<UImage>();
            _t += Time.unscaledDeltaTime;
            int i = (int)(_t * Fps);
            i = Loop ? i % Frames.Length : Mathf.Min(i, Frames.Length - 1);
            if (_img != null && _img.sprite != Frames[i]) _img.sprite = Frames[i];
        }
    }
}
