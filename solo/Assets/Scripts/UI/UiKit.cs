using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wizard.Solo.Art;
using UImage = UnityEngine.UI.Image;
using UText = UnityEngine.UI.Text;

namespace Wizard.Solo.UI
{
    /// <summary>판 꼴(UI 키트, 09 요약서 6.3절). 그림이 없으면 괄호 안의 색으로 그린다.</summary>
    public enum UiPanelKind
    {
        /// <summary>K-01 판(나무).</summary>
        Board = 0,
        /// <summary>K-02 안쪽 판(나무 어두움).</summary>
        Recess = 1,
        /// <summary>K-03 쪽지 판(쪽지) — 결과 카드, 확인 창.</summary>
        Paper = 2,
        /// <summary>K-04 제목 띠(나무 밝음).</summary>
        Heading = 3,
        /// <summary>K-05 화면 제목 띠 96×80(나무).</summary>
        ScreenHeading = 4,
        /// <summary>K-06 아래 조작 안내 띠 96×48(나무 어두움).</summary>
        ControlStrip = 5,
        /// <summary>K-08 툴팁(먹).</summary>
        Tooltip = 6,
        /// <summary>K-11 토스트(쪽지).</summary>
        Toast = 7,
        /// <summary>그림 없는 단색 판(색은 인자).</summary>
        Solid = 8
    }

    /// <summary>
    /// uGUI를 코드로 조립하는 정적 함수 묶음. 좌표는 전부 1920×1080 기준 px, **부모의 왼쪽 위가 원점, y는 아래로 +**(Place).
    /// ctx.Art가 null이거나 그림이 없으면 예외 없이 단색 Image로 그린다. 글자는 UnityEngine.UI.Text + ctx.Art.Font(크기 6종).
    /// 화면에 보이는 글은 부르는 쪽이 ctx.Names로 만든 글을 넘긴다(UiKit은 이름 표 열쇠를 받지 않는다 — LabelKey만 예외).
    /// </summary>
    public static class UiKit
    {
        // ------------------------------------------------------------------ 배치

        /// <summary>빈 RectTransform 하나(부모 안, 왼쪽 위 원점 px).</summary>
        public static RectTransform Node(RectTransform parent, string name, float x = 0f, float y = 0f, float w = 0f, float h = 0f)
        {
            var go = new GameObject(name ?? "Node", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            return rt;
        }

        /// <summary>부모의 왼쪽 위를 원점으로 (x, y) px에 w×h로 놓는다(y 아래가 +).</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float w, float h)
        {
            if (rt == null) return null;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        /// <summary>부모를 꽉 채운다(여백 px: 왼, 위, 오른, 아래).</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0f, float top = 0f, float right = 0f, float bottom = 0f)
        {
            if (rt == null) return null;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>부모 가운데에 w×h로(창 배치).</summary>
        public static RectTransform Center(RectTransform rt, float w, float h, float offsetX = 0f, float offsetY = 0f)
        {
            if (rt == null) return null;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(offsetX, -offsetY);
            return rt;
        }

        // ------------------------------------------------------------------ 그림·글꼴

        /// <summary>UI 키트 그림(없으면 IsValid = false). 예외를 내지 않는다.</summary>
        public static UiSprite KitSprite(GameContext ctx, string id, string variant = null, string state = null)
        {
            if (ctx == null || ctx.Art == null || string.IsNullOrEmpty(id)) return default;
            try { return ctx.Art.Ui(id, variant, state); }
            catch (Exception ex) { Debug.LogWarning("UiKit: Art.Ui(" + id + ") failed: " + ex.Message); return default; }
        }

        /// <summary>아이콘(콘텐츠 id). 없으면 null.</summary>
        public static Sprite IconSprite(GameContext ctx, string contentId, int sizePx = 32)
        {
            if (ctx == null || ctx.Art == null || string.IsNullOrEmpty(contentId)) return null;
            try { return ctx.Art.Icon(contentId, sizePx); }
            catch (Exception ex) { Debug.LogWarning("UiKit: Art.Icon(" + contentId + ") failed: " + ex.Message); return null; }
        }

        /// <summary>글꼴(ctx.Art.Font). 없으면 Unity 내장 글꼴(한글이 안 보일 수 있으나 죽지 않는다).</summary>
        public static Font FontFor(GameContext ctx, UiFontId size)
        {
            Font f = null;
            if (ctx != null && ctx.Art != null)
            {
                try { f = ctx.Art.Font(size); } catch (Exception ex) { Debug.LogWarning("UiKit: Art.Font failed: " + ex.Message); }
            }
            if (f != null) return f;
            try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { f = null; }
            return f;
        }

        /// <summary>Image에 키트 그림을 넣는다(9조각이면 Sliced, 1px = 1px).</summary>
        public static void SetSprite(UImage img, UiSprite sp)
        {
            if (img == null) return;
            img.sprite = sp.Sprite;
            img.type = sp.NineSlice ? UImage.Type.Sliced : UImage.Type.Simple;
            img.pixelsPerUnitMultiplier = 1f;
            img.fillCenter = true;
        }

        // ------------------------------------------------------------------ 판·그림

        /// <summary>단색 사각형(마우스를 막을지 고른다).</summary>
        public static UImage Rect(RectTransform parent, float x, float y, float w, float h, Color color, bool raycast = false, string name = "Rect")
        {
            var img = NewImage(parent, name, x, y, w, h);
            img.color = color;
            img.raycastTarget = raycast;
            return img;
        }

        /// <summary>판(K-01·02·03·04·05·06·08·11). Solid면 color로만.</summary>
        public static UImage Panel(GameContext ctx, RectTransform parent, UiPanelKind kind, float x, float y, float w, float h, Color? color = null)
        {
            var img = NewImage(parent, "Panel." + kind, x, y, w, h);
            img.raycastTarget = true;
            if (kind != UiPanelKind.Solid)
            {
                PanelArt(kind, out string id, out string variant);
                var sp = KitSprite(ctx, id, variant);
                if (sp.IsValid) { SetSprite(img, sp); img.color = color ?? Color.white; return img; }
            }
            img.color = color ?? PanelFallback(kind);
            return img;
        }

        static void PanelArt(UiPanelKind k, out string id, out string variant)
        {
            switch (k)
            {
                case UiPanelKind.Board: id = "K-01"; variant = "board"; break;
                case UiPanelKind.Recess: id = "K-02"; variant = "recess"; break;
                case UiPanelKind.Paper: id = "K-03"; variant = "paper"; break;
                case UiPanelKind.Heading: id = "K-04"; variant = "heading_strip"; break;
                case UiPanelKind.ScreenHeading: id = "K-05"; variant = "screen_heading"; break;
                case UiPanelKind.ControlStrip: id = "K-06"; variant = "control_strip"; break;
                case UiPanelKind.Tooltip: id = "K-08"; variant = "tooltip"; break;
                case UiPanelKind.Toast: id = "K-11"; variant = "toast"; break;
                default: id = null; variant = null; break;
            }
        }

        public static Color PanelFallback(UiPanelKind k)
        {
            switch (k)
            {
                case UiPanelKind.Recess: return UiTheme.WoodDark;
                case UiPanelKind.Paper: return UiTheme.Paper;
                case UiPanelKind.Heading: return UiTheme.WoodLight;
                case UiPanelKind.ControlStrip: return UiTheme.WoodDark;
                case UiPanelKind.Tooltip: return UiTheme.Ink;
                case UiPanelKind.Toast: return UiTheme.Paper;
                default: return UiTheme.Wood;
            }
        }

        /// <summary>9조각 키트 그림 아무거나(id·variant·state = 09 요약서 6.1절 규칙). 없으면 fallback 색.</summary>
        public static UImage NineSlice(GameContext ctx, RectTransform parent, string id, string variant, string state,
            float x, float y, float w, float h, Color fallback)
        {
            var img = NewImage(parent, id ?? "NineSlice", x, y, w, h);
            img.raycastTarget = false;
            var sp = KitSprite(ctx, id, variant, state);
            if (sp.IsValid) { SetSprite(img, sp); img.color = Color.white; }
            else img.color = fallback;
            return img;
        }

        /// <summary>그림 한 장(sprite가 null이면 color 단색). preserveAspect면 비율 유지.</summary>
        public static UImage Image(RectTransform parent, Sprite sprite, float x, float y, float w, float h, Color? color = null, bool preserveAspect = true)
        {
            var img = NewImage(parent, sprite != null ? sprite.name : "Image", x, y, w, h);
            img.raycastTarget = false;
            img.sprite = sprite;
            img.preserveAspect = preserveAspect && sprite != null;
            img.color = color ?? (sprite != null ? Color.white : UiTheme.WoodLight);
            return img;
        }

        /// <summary>콘텐츠 아이콘(ctx.Art.Icon) sizePx 정사각. 없으면 대체 그림(회청 사각).</summary>
        public static UImage Icon(GameContext ctx, RectTransform parent, string contentId, int sizePx, float x, float y)
        {
            var sp = IconSprite(ctx, contentId, sizePx);
            if (sp == null && ctx != null && ctx.Art != null)
            {
                try { sp = ctx.Art.Placeholder(sizePx, sizePx); } catch (Exception) { sp = null; }
            }
            var img = Image(parent, sp, x, y, sizePx, sizePx, sp != null ? Color.white : UiTheme.SlateBlue);
            img.name = "Icon." + contentId;
            return img;
        }

        static UImage NewImage(RectTransform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            return go.GetComponent<UImage>();
        }

        // ------------------------------------------------------------------ 글자

        /// <summary>
        /// 글자 칸. 한 줄(wrap = false)이면 넘칠 때 끝을 "…"로 자르고 전체 글을 툴팁으로. 여러 줄이면 칸 안에서 줄을 바꾼다.
        /// outline = 먹색 1px 외곽선(전장 위 글자). text는 이미 이름 표를 거친 글.
        /// </summary>
        public static UiLabel Label(GameContext ctx, RectTransform parent, string text, UiFontId size, float x, float y, float w, float h,
            Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false, bool outline = false)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(UText));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, x, y, w, h > 0f ? h : UiTheme.LineHeight(size));
            var t = go.GetComponent<UText>();
            t.font = FontFor(ctx, size);
            t.fontSize = (int)size;
            t.fontStyle = FontStyle.Normal;
            t.lineSpacing = 1f;
            t.alignment = align;
            t.supportRichText = false;
            t.resizeTextForBestFit = false;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.color = color ?? UiTheme.Chalk;
            t.raycastTarget = false;
            if (outline)
            {
                var o = go.AddComponent<UnityEngine.UI.Outline>();
                o.effectColor = UiTheme.Ink;
                o.effectDistance = new Vector2(1f, -1f);
            }
            var l = go.AddComponent<UiLabel>();
            l.Init(t, size, wrap);
            l.SetText(text);
            return l;
        }

        /// <summary>이름 표 열쇠로 글자 칸(ctx.Names.Get(key)).</summary>
        public static UiLabel LabelKey(GameContext ctx, RectTransform parent, string nameKey, UiFontId size, float x, float y, float w, float h,
            Color? color = null, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false)
        {
            string s = ctx != null && ctx.Names != null ? ctx.Names.Get(nameKey) : nameKey;
            return Label(ctx, parent, s, size, x, y, w, h, color, align, wrap);
        }

        // ------------------------------------------------------------------ 누를 수 있는 것

        /// <summary>
        /// 버튼. text는 이미 이름 표를 거친 글(null이면 글 없음). onClick은 켜져 있을 때 J·클릭에. pressSound = 누를 때 낼 ui.* 사건
        /// (null = 기본 ui.common.press, "" = 소리 없음). Caution 꼴은 호박색 테 + 경고 삼각. group이 있으면 커서 묶음에 넣는다.
        /// </summary>
        public static UiButton Button(GameContext ctx, RectTransform parent, string text, float x, float y, float w, float h,
            Action onClick, UiButtonStyle style = UiButtonStyle.Medium, UiFocusGroup group = null, string pressSound = null,
            UiFontId fontSize = UiFontId.F24, Sprite icon = null)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            var b = go.AddComponent<UiButton>();
            b.Style = style;
            b.OnClick = onClick;
            if (pressSound != null) b.PressSound = pressSound.Length == 0 ? null : pressSound;
            b.InitWidgetParts(ctx, text, fontSize, icon);
            group?.Add(b);
            return b;
        }

        /// <summary>그림만 있는 버튼(닫기 K-35, 쪽 넘김 K-38 등은 sprite로 넘긴다). 바탕은 style(기본 Small).</summary>
        public static UiButton IconButton(GameContext ctx, RectTransform parent, Sprite icon, float x, float y, float w, float h,
            Action onClick, UiButtonStyle style = UiButtonStyle.Small, UiFocusGroup group = null, string pressSound = null)
        {
            var b = Button(ctx, parent, null, x, y, w, h, onClick, style, group, pressSound, UiFontId.F16, icon);
            b.name = "IconButton";
            if (icon == null && b.Icon == null)
            {
                // 그림이 없으면 대체 그림.
                Sprite ph = null;
                if (ctx != null && ctx.Art != null) { try { ph = ctx.Art.Placeholder((int)Mathf.Max(8f, h - 8f), (int)Mathf.Max(8f, h - 8f)); } catch (Exception) { ph = null; } }
                if (ph != null) Image(b.ContentRoot, ph, 4f, 4f, w - 8f, h - 8f);
            }
            return b;
        }

        /// <summary>키트 그림 하나를 그림 버튼으로(id·variant, 상태는 hover/pressed/disabled 파일을 자동으로 고른다).</summary>
        public static UiButton KitIconButton(GameContext ctx, RectTransform parent, string id, string variant, float x, float y, float w, float h,
            Action onClick, UiFocusGroup group = null, string pressSound = null)
        {
            var b = Button(ctx, parent, null, x, y, w, h, onClick, UiButtonStyle.Plain, group, pressSound, UiFontId.F16, null);
            b.name = "KitIconButton." + id;
            var sw = b.gameObject.AddComponent<UiKitStateSkin>();
            sw.Init(ctx, b, id, variant);
            return b;
        }

        /// <summary>켬/끔 토글(K-27). label이 있으면 왼쪽에 글, 오른쪽에 48×24 스위치.</summary>
        public static UiToggle Toggle(GameContext ctx, RectTransform parent, string label, float x, float y, float w, float h,
            bool value, Action<bool> onChanged, UiFocusGroup group = null)
        {
            var go = new GameObject("Toggle", typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            var hit = go.GetComponent<UImage>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var t = go.AddComponent<UiToggle>();
            const float SwW = 48f, SwH = 24f;
            var sw = NewImage(rt, "Switch", w - SwW, (h - SwH) * 0.5f, SwW, SwH);
            sw.raycastTarget = false;
            t.Switch = sw;
            var knob = NewImage(sw.rectTransform, "Knob", 0f, 0f, SwH - 6f, SwH - 6f);
            knob.raycastTarget = false;
            t.Knob = knob;
            if (!string.IsNullOrEmpty(label))
                t.Label = Label(ctx, rt, label, UiFontId.F16, 0f, (h - UiTheme.LineHeight(UiFontId.F16)) * 0.5f, Mathf.Max(1f, w - SwW - 16f), UiTheme.LineHeight(UiFontId.F16));
            t.Value = value;
            t.Changed = onChanged;
            t.InitWidget(ctx);
            group?.Add(t);
            return t;
        }

        /// <summary>
        /// 숫자 슬라이더 "◀ 막대 값 ▶"(0~100 기본). w×h 안에 ◀(24) 막대 값(64) ▶(24). 좌우 키·화살 클릭·막대 끌기로 바꾼다.
        /// </summary>
        public static UiSlider Slider(GameContext ctx, RectTransform parent, float x, float y, float w, float h, int value,
            Action<int> onChanged, int min = 0, int max = 100, int step = 1, UiFocusGroup group = null, int[] steps = null)
        {
            var go = new GameObject("Slider", typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            var hit = go.GetComponent<UImage>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;
            var s = go.AddComponent<UiSlider>();
            s.Min = min; s.Max = max; s.Step = step; s.Steps = steps;
            const float Arrow = 24f, ValueW = 64f, Pad = 8f;
            float cy = (h - Arrow) * 0.5f;

            // K-32 단계 화살 그림은 왼쪽(◀)을 본다(통합 단계에서 실제 그림으로 확인). 왼쪽 화살은 그대로, 오른쪽 화살만 뒤집는다.
            s.LeftArrow = NewImage(rt, "Left", 0f, cy, Arrow, Arrow);
            s.LeftTri = Triangle(s.LeftArrow.rectTransform, UiTriangleDir.Left, 4f, 4f, Arrow - 8f, Arrow - 8f, UiTheme.Chalk);
            var la = s.LeftArrow.gameObject.AddComponent<UiSliderArrowInput>();
            la.Owner = s; la.Direction = -1;

            float railX = Arrow + Pad, railW = Mathf.Max(16f, w - 2f * Arrow - 2f * Pad - ValueW - Pad);
            const float RailH = 8f;
            var rail = NewImage(rt, "Rail", railX, (h - RailH) * 0.5f, railW, RailH);
            rail.raycastTarget = true;
            s.Rail = rail.rectTransform;
            s.RailImage = rail;
            var ri = rail.gameObject.AddComponent<UiSliderRailInput>();
            ri.Owner = s;
            var fill = NewImage(rail.rectTransform, "Fill", 0f, 0f, 0f, 0f);
            fill.raycastTarget = false;
            Stretch(fill.rectTransform);
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            s.FillRect = fill.rectTransform;
            s.FillImage = fill;
            var thumb = NewImage(rail.rectTransform, "Thumb", 0f, 0f, 16f, 24f);
            thumb.raycastTarget = false;
            thumb.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s.Thumb = thumb.rectTransform;
            s.ThumbImage = thumb;

            s.ValueLabel = Label(ctx, rt, string.Empty, UiFontId.F16, railX + railW + Pad, (h - UiTheme.LineHeight(UiFontId.F16)) * 0.5f,
                ValueW, UiTheme.LineHeight(UiFontId.F16), UiTheme.Chalk, TextAnchor.MiddleRight);

            s.RightArrow = NewImage(rt, "Right", w - Arrow, cy, Arrow, Arrow);
            s.RightArrow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            s.RightArrow.rectTransform.anchoredPosition = new Vector2(w - Arrow * 0.5f, -(cy + Arrow * 0.5f));
            s.RightArrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            // 부모가 좌우로 뒤집혀 있으므로 대체 삼각형도 Left로 둔다(뒤집혀 ▶로 보인다).
            s.RightTri = Triangle(s.RightArrow.rectTransform, UiTriangleDir.Left, 4f, 4f, Arrow - 8f, Arrow - 8f, UiTheme.Chalk);
            var ra = s.RightArrow.gameObject.AddComponent<UiSliderArrowInput>();
            ra.Owner = s; ra.Direction = 1;

            s.InitWidget(ctx);
            s.SetValue(value, false);
            s.Changed = onChanged;
            s.Refresh();
            group?.Add(s);
            return s;
        }

        /// <summary>삼각형 도형(UiTriangle).</summary>
        public static UiTriangle Triangle(RectTransform parent, UiTriangleDir dir, float x, float y, float w, float h, Color color, bool warningMark = false)
        {
            var go = new GameObject("Triangle", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiTriangle));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            var t = go.GetComponent<UiTriangle>();
            t.Direction = dir;
            t.InnerMark = warningMark;
            t.color = color;
            t.raycastTarget = false;
            return t;
        }

        /// <summary>경고 삼각(호박색, 먹색 느낌표). 조심할 일을 알리는 곳에.</summary>
        public static UiTriangle WarningTriangle(RectTransform parent, float x, float y, float size) =>
            Triangle(parent, UiTriangleDir.Up, x, y, size, size, UiTheme.Amber, true);

        /// <summary>진행 막대(K-36). 0~1.</summary>
        public static UiProgressBar ProgressBar(GameContext ctx, RectTransform parent, float x, float y, float w, float h, float value01 = 0f)
        {
            var go = new GameObject("ProgressBar", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            if (parent != null) rt.SetParent(parent, false);
            Place(rt, x, y, w, h);
            var bar = go.AddComponent<UiProgressBar>();
            var rail = NewImage(rt, "Rail", 0f, 0f, w, h);
            Stretch(rail.rectTransform);
            rail.raycastTarget = false;
            var rs = KitSprite(ctx, "K-36", "progress_rail");
            if (rs.IsValid) { SetSprite(rail, rs); rail.color = Color.white; } else rail.color = UiTheme.WoodDark;
            var fill = NewImage(rail.rectTransform, "Fill", 0f, 0f, 0f, 0f);
            fill.raycastTarget = false;
            var fs = KitSprite(ctx, "K-36", "progress_fill");
            if (fs.IsValid) { SetSprite(fill, fs); fill.color = Color.white; } else fill.color = UiTheme.Chalk;
            bar.Rail = rail;
            bar.Fill = fill;
            bar.Value01 = value01;
            return bar;
        }

        /// <summary>
        /// 탭 줄(가로 K-25, vertical이면 세로 K-26). labels는 이미 이름 표를 거친 글. 탭 하나 = tabW×tabH, 사이 gap.
        /// 처음 고른 탭 = selected. Q/E는 화면의 OnMenuKey에서 tabs.HandleKey(key)로 넘긴다.
        /// </summary>
        public static UiTabs Tabs(GameContext ctx, RectTransform parent, IList<string> labels, float x, float y, float tabW, float tabH,
            Action<int> onSelected, int selected = 0, bool vertical = false, float gap = 4f, UiFocusGroup group = null)
        {
            int n = labels != null ? labels.Count : 0;
            float totalW = vertical ? tabW : n * tabW + Mathf.Max(0, n - 1) * gap;
            float totalH = vertical ? n * tabH + Mathf.Max(0, n - 1) * gap : tabH;
            var rt = Node(parent, "Tabs", x, y, totalW, totalH);
            var tabs = rt.gameObject.AddComponent<UiTabs>();
            tabs.Init(ctx);
            for (int i = 0; i < n; i++)
            {
                float tx = vertical ? 0f : i * (tabW + gap);
                float ty = vertical ? i * (tabH + gap) : 0f;
                var b = Button(ctx, rt, labels[i], tx, ty, tabW, tabH, null, vertical ? UiButtonStyle.TabVertical : UiButtonStyle.Tab, group);
                b.name = "Tab" + i;
                tabs.AddButton(b);
            }
            tabs.Select(selected, false);
            tabs.Selected = onSelected;
            return tabs;
        }

        /// <summary>
        /// 세로 목록. 줄은 list.AddRow(높이). followFocus가 있으면 그 묶음의 커서가 옮겨 갈 때 커서 칸이 보이도록 굴린다.
        /// </summary>
        public static UiScrollList ScrollList(GameContext ctx, RectTransform parent, float x, float y, float w, float h,
            UiFocusGroup followFocus = null, float rowGap = 4f)
        {
            const float BarW = 8f, BarGap = 8f;
            var rt = Node(parent, "ScrollList", x, y, w, h);
            var list = rt.gameObject.AddComponent<UiScrollList>();
            list.Gap = rowGap;
            var vp = Node(rt, "Viewport", 0f, 0f, w - BarW - BarGap, h);
            vp.gameObject.AddComponent<RectMask2D>();
            var vpHit = vp.gameObject.AddComponent<UImage>();
            vpHit.color = new Color(0f, 0f, 0f, 0f);
            vpHit.raycastTarget = true;
            var content = Node(vp, "Content", 0f, 0f, 0f, 0f);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            content.sizeDelta = new Vector2(0f, 0f);
            var sr = rt.gameObject.AddComponent<ScrollRect>();
            sr.viewport = vp;
            sr.content = content;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.inertia = false;
            sr.scrollSensitivity = 32f;
            list.Scroll = sr;
            list.Viewport = vp;
            list.Content = content;

            var rail = NineSlice(ctx, rt, "K-34", "scroll_rail", null, w - BarW, 0f, BarW, h, UiTheme.WoodDark);
            var thumb = NineSlice(ctx, rail.rectTransform, "K-34", "scroll_thumb", null, 0f, 0f, BarW, 24f, UiTheme.SlateBlue);
            list.ScrollRail = rail.rectTransform;
            list.ScrollThumb = thumb.rectTransform;

            if (followFocus != null)
            {
                var prev = followFocus.CursorChanged;
                followFocus.CursorChanged = i =>
                {
                    prev?.Invoke(i);
                    var cur = followFocus.Current;
                    if (cur != null && list != null) list.EnsureVisible(cur.Rect);
                };
            }
            return list;
        }

        /// <summary>격자 칸의 왼쪽 위 좌표(순수 함수). 줄 순(왼쪽 → 오른쪽, 위 → 아래).</summary>
        public static Vector2 GridCell(int index, int columns, float cellW, float cellH, float gapX, float gapY)
        {
            if (columns <= 0) columns = 1;
            int col = index % columns, row = index / columns;
            return new Vector2(col * (cellW + gapX), row * (cellH + gapY));
        }

        /// <summary>격자 칸 count개를 만든다(빈 RectTransform). 칸 안을 채우는 것은 부르는 쪽.</summary>
        public static List<RectTransform> Grid(RectTransform parent, float x, float y, int columns, int count, float cellW, float cellH,
            float gapX = 8f, float gapY = 8f)
        {
            var cells = new List<RectTransform>(count);
            int rows = columns > 0 ? (count + columns - 1) / columns : count;
            var root = Node(parent, "Grid", x, y,
                columns * cellW + Mathf.Max(0, columns - 1) * gapX, rows * cellH + Mathf.Max(0, rows - 1) * gapY);
            for (int i = 0; i < count; i++)
            {
                var p = GridCell(i, columns, cellW, cellH, gapX, gapY);
                cells.Add(Node(root, "Cell" + i, p.x, p.y, cellW, cellH));
            }
            return cells;
        }

        /// <summary>선택 괄호를 아무 RectTransform에 붙인다(위젯이 아닌 칸에). bracket.Set(UiStateRules.Bracket(...))로 켠다.</summary>
        public static UiSelectionBracket SelectionBracket(GameContext ctx, RectTransform target) => UiSelectionBracket.Attach(ctx, target);

        /// <summary>아래 조작 안내 띠(K-06)와 그 안의 글 "J 고르기 · K 뒤로 · Esc 나가기". 글은 hints로(null이면 기본).</summary>
        public static UiLabel KeyHintBar(GameContext ctx, RectTransform parent, float x, float y, float w, float h, UiKeyHint[] hints = null)
        {
            var strip = Panel(ctx, parent, UiPanelKind.ControlStrip, x, y, w, h);
            strip.name = "KeyHintBar";
            strip.raycastTarget = false;
            float lh = UiTheme.LineHeight(UiFontId.F16);
            var l = Label(ctx, strip.rectTransform, UiKeyHint.Compose(ctx != null ? ctx.Names : null, hints ?? UiKeyHint.Defaults),
                UiFontId.F16, 24f, (h - lh) * 0.5f, w - 48f, lh, UiTheme.Chalk, TextAnchor.MiddleLeft);
            l.name = "Hints";
            return l;
        }

        /// <summary>구분선(K-07). vertical이면 4×length, 아니면 length×4.</summary>
        public static UImage Divider(GameContext ctx, RectTransform parent, float x, float y, float length, bool vertical = false)
        {
            float w = vertical ? 4f : length, h = vertical ? length : 4f;
            var img = NineSlice(ctx, parent, "K-07", vertical ? "divider_vertical" : "divider_horizontal", null, x, y, w, h, UiTheme.WoodLight);
            img.name = "Divider";
            if (img.sprite != null) img.type = UImage.Type.Tiled;
            return img;
        }

        /// <summary>툴팁을 붙인다(마우스가 머물면, 또는 키보드 커서가 오면). text는 부를 때마다 받는다(이름 표를 거친 글).</summary>
        public static UiTooltipTrigger AttachTooltip(GameObject target, Func<string> text)
        {
            if (target == null) return null;
            var t = target.GetComponent<UiTooltipTrigger>();
            if (t == null) t = target.AddComponent<UiTooltipTrigger>();
            t.TextProvider = text;
            t.enabled = true;
            var g = target.GetComponent<Graphic>();
            if (g != null) g.raycastTarget = true;
            return t;
        }

        /// <summary>끌어다 놓기의 끄는 쪽. payload = 놓는 곳에 넘길 값.</summary>
        public static UiDragSource MakeDraggable(GameObject target, object payload, Action<bool> onEnded = null, Func<bool> canDrag = null)
        {
            if (target == null) return null;
            var d = target.GetComponent<UiDragSource>();
            if (d == null) d = target.AddComponent<UiDragSource>();
            d.Payload = payload;
            d.Ended = onEnded;
            d.CanDrag = canDrag;
            EnsureRaycast(target);
            return d;
        }

        /// <summary>끌어다 놓기의 받는 쪽. canAccept(값)이 참이면 놓을 때 onDrop(값). onHover(true/false) = 받을 수 있는 것이 위에 있다.</summary>
        public static UiDropTarget MakeDropTarget(GameObject target, Func<object, bool> canAccept, Action<object> onDrop, Action<bool> onHover = null)
        {
            if (target == null) return null;
            var d = target.GetComponent<UiDropTarget>();
            if (d == null) d = target.AddComponent<UiDropTarget>();
            d.CanAccept = canAccept;
            d.Dropped = onDrop;
            d.HoverChanged = onHover;
            EnsureRaycast(target);
            return d;
        }

        static void EnsureRaycast(GameObject target)
        {
            var g = target.GetComponent<Graphic>();
            if (g == null)
            {
                var img = target.AddComponent<UImage>();
                img.color = new Color(0f, 0f, 0f, 0f);
                g = img;
            }
            g.raycastTarget = true;
        }

        // ------------------------------------------------------------------ 꾸밈(솔로 UI 다듬기, 더하기만)

        /// <summary>
        /// 그림자 있는 판: 같은 키트 그림을 먹색 반투명으로 (shadowPx, shadowPx) 아래에 한 장 깔고 그 위에 판을 놓는다.
        /// 돌려주는 것은 판(위) 그림. 그림자는 판의 형제(판보다 먼저)다.
        /// </summary>
        public static UImage Plate(GameContext ctx, RectTransform parent, UiPanelKind kind, float x, float y, float w, float h,
            Color? color = null, float shadowPx = 6f)
        {
            if (shadowPx > 0f)
            {
                var sh = Panel(ctx, parent, kind == UiPanelKind.Solid ? UiPanelKind.Solid : kind, x + shadowPx, y + shadowPx, w, h, UiTheme.Shadow);
                sh.name = "PlateShadow";
                sh.raycastTarget = false;
            }
            var p = Panel(ctx, parent, kind, x, y, w, h, color);
            p.name = "Plate." + kind;
            return p;
        }

        /// <summary>
        /// 장식 판: 공통 HUD 그림 panel_9slice(금 테, 네 변 12px)를 scale 정수배로(기본 2배 = 테 24px), 그림자와 함께.
        /// 그림이 없으면 K-01 판(Board)으로 그린다. 중요한 카드·결과 판에 쓴다(모든 판에 쓰면 무겁다).
        /// </summary>
        public static UImage OrnatePanel(GameContext ctx, RectTransform parent, float x, float y, float w, float h, int scale = 2, float shadowPx = 6f)
        {
            var art = ctx != null ? ctx.Art : null;
            if (!UiCommonArt.HasPanel(art)) return Plate(ctx, parent, UiPanelKind.Board, x, y, w, h, null, shadowPx);
            if (shadowPx > 0f)
            {
                var sh = NewImage(parent, "PlateShadow", x + shadowPx, y + shadowPx, w, h);
                UiCommonArt.ApplySliced(sh, UiCommonArt.PanelSprite(art), scale);
                sh.color = UiTheme.Shadow;
                sh.raycastTarget = false;
            }
            var p = NewImage(parent, "Plate.Ornate", x, y, w, h);
            UiCommonArt.ApplySliced(p, UiCommonArt.PanelSprite(art), scale);
            p.color = Color.white;
            p.raycastTarget = true;
            return p;
        }

        /// <summary>판 위쪽 안의 은은한 빛줄(1px 분필 10%)과 아래 안쪽 그늘. 판을 입체로 보이게 한다.</summary>
        public static void Bevel(RectTransform panel, float inset = 6f)
        {
            if (panel == null) return;
            var size = panel.rect.size;
            if (size.x <= 0f) size = panel.sizeDelta;
            Rect(panel, inset, inset - 2f, Mathf.Max(0f, size.x - 2f * inset), 1f, UiTheme.Sheen, false, "Sheen");
        }

        /// <summary>놋쇠 장식 줄(2px 놋쇠 + 1px 그늘) 길이 w. 제목 띠 아래·구역 머리에.</summary>
        public static RectTransform AccentRule(RectTransform parent, float x, float y, float w)
        {
            var rt = Node(parent, "AccentRule", x, y, w, 3f);
            Rect(rt, 0f, 0f, w, 2f, UiTheme.WithAlpha(UiTheme.Brass, 0.85f), false, "Line");
            Rect(rt, 0f, 2f, w, 1f, UiTheme.WithAlpha(UiTheme.BrassDark, 0.8f), false, "Shade");
            return rt;
        }

        /// <summary>마름모 장식(45도 돌린 정사각, 먹색 1px 테). (cx, cy)가 가운데.</summary>
        public static UImage Diamond(RectTransform parent, float cx, float cy, float size, Color color)
        {
            float d = size / 1.41421356f;
            var rim = NewImage(parent, "DiamondRim", 0f, 0f, d + 2f, d + 2f);
            rim.raycastTarget = false;
            rim.color = UiTheme.Ink;
            var rrt = rim.rectTransform;
            rrt.pivot = new Vector2(0.5f, 0.5f);
            rrt.anchoredPosition = new Vector2(cx, -cy);
            rrt.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var img = NewImage(rrt, "Diamond", 1f, 1f, d, d);
            img.raycastTarget = false;
            img.color = color;
            return img;
        }

        /// <summary>글자에 먹색 그림자(2px 아래)를 단다(밝은 그림 위·큰 제목).</summary>
        public static void DropShadow(UiLabel label, float px = 2f)
        {
            if (label == null) return;
            var sh = label.gameObject.GetComponent<UnityEngine.UI.Shadow>();
            if (sh == null || sh is UnityEngine.UI.Outline) sh = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            sh.effectColor = UiTheme.WithAlpha(UiTheme.Ink, 0.85f);
            sh.effectDistance = new Vector2(px, -px);
        }

        /// <summary>
        /// 구역 머리: K-04 제목 띠 + 왼쪽 놋쇠 마름모 + 24px 글. 높이 40. 돌려주는 것은 글 칸.
        /// </summary>
        public static UiLabel SectionHeader(GameContext ctx, RectTransform parent, string text, float x, float y, float w, float h = 40f)
        {
            var strip = Panel(ctx, parent, UiPanelKind.Heading, x, y, w, h);
            strip.name = "SectionHeader";
            strip.raycastTarget = false;
            Diamond(strip.rectTransform, 20f, h * 0.5f, 10f, UiTheme.Brass);
            float lh = UiTheme.LineHeight(UiFontId.F24);
            var l = Label(ctx, strip.rectTransform, text, UiFontId.F24, 36f, (h - lh) * 0.5f, Mathf.Max(1f, w - 48f), lh, UiTheme.Chalk, TextAnchor.MiddleLeft);
            DropShadow(l, 1f);
            return l;
        }

        /// <summary>부모 기준 고정점 배치: (ax, ay) = 부모 안의 고정점(0~1, ay는 위 0 · 아래 1), 축도 같은 점. (x, y)는 그 점에서 px(오른쪽 +, 아래 +).</summary>
        public static RectTransform PlaceAnchored(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            if (rt == null) return null;
            var a = new Vector2(ax, 1f - ay);
            rt.anchorMin = rt.anchorMax = a;
            rt.pivot = a;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
            return rt;
        }

        /// <summary>키 마개(K-50 24×24) + 키 글자. 키 재설정·조작 보기용.</summary>
        public static UImage KeyCap(GameContext ctx, RectTransform parent, Key key, float x, float y, float size = 24f, bool pressed = false)
        {
            var cap = NineSlice(ctx, parent, size > 24f ? "K-51" : "K-50", "keycap", pressed ? "pressed" : null, x, y, size, size, UiTheme.WoodLight);
            cap.name = "KeyCap." + key;
            var font = size > 24f ? UiFontId.F24 : UiFontId.F12;
            Label(ctx, cap.rectTransform, UiKeyLabels.Of(ctx != null ? ctx.Names : null, key), font, 0f, 0f, size, size, UiTheme.Chalk, TextAnchor.MiddleCenter);
            return cap;
        }
    }
}
