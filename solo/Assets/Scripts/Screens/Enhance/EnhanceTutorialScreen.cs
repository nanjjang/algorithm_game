using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 첫 안내(08 10.11절, 07 5.5절): 4쪽. 쪽마다 강화 화면의 실제 영역(DataSolo/EnhanceTutorial.json focus — 08 10.3절 칸 좌표)을
    /// 밝게 남기고 나머지를 어둡게 덮은 뒤, 선택 노랑 테와 화살표로 가리킨다. 쪽지(쪽지 판)는 가리키는 곳을 피해 놓인다.
    ///   1 확률: 늘 보이는 숫자, 다섯 지팡이가 같은 확률, 연출은 결과를 바꾸지 않는다(누르는 순간 정해진다).
    ///   2 제물·기운: 제물 점수와 상한, 같은 속성, 실패할 때마다 쌓이는 기운, 확률 상한.
    ///   3 실패하면: 결과 띠 네 구간, 안전 단계, 하락 바닥, 부적, 변이. 누르기 전에 실패 결과가 늘 적혀 있다.
    ///   4 비용과 운: 비용, 지금 지팡이의 평균과 운이 나쁜 10%(EnhanceTutorialOddsCalc), 하락해도 다음 시도는 따로 정해지고 기운이 쌓인다.
    /// 수치는 enhance.json에서 읽어 서식으로 넣는다(문구에 숫자를 적지 않는다). 위험색을 쓰지 않는다.
    /// 닫을 때 flags.enhance_tutorial_shown = true + 저장(한 번만). 인자 NextScreen이 있으면 닫은 뒤 그 화면으로 Replace, 없으면 Pop.
    /// </summary>
    public sealed class EnhanceTutorialScreen : ScreenBase
    {
        public const float Width = 760f, Height = 440f;
        public const string PageTitlePrefix = "str.enhance_tutorial.page.";
        EnhanceTutorialModel _model;
        EnhanceTutorialTuning _tt;
        UiLabel _page, _title, _body, _odds;
        UiButton _prev, _next, _close;
        RectTransform _card, _focusFrame, _arrowRoot, _map, _mapFocus;
        readonly UImage[] _dim = new UImage[4];
        readonly List<UImage> _frameEdges = new List<UImage>();
        float _age;

        public override ScreenId Id => ScreenId.EnhanceTutorial;
        public override ScreenLayer Layer => ScreenLayer.Modal;
        public override bool BlocksInput => true;
        public EnhanceTutorialModel Model => _model;

        protected override void BuildContent(RectTransform content)
        {
            var tuning = EnhanceScreenTuning.From(Ctx != null ? Ctx.Content : null);
            _tt = EnhanceTutorialTuning.From(Ctx != null ? Ctx.Content : null);
            _model = new EnhanceTutorialModel(tuning.TutorialPages);
            for (int i = 0; i < 4; i++) _dim[i] = UiKit.Rect(content, 0f, 0f, 0f, 0f, UiTheme.ModalDim, true, "Dim" + i);
            _focusFrame = UiKit.Node(content, "Focus", 0f, 0f, 0f, 0f);
            for (int i = 0; i < 4; i++) _frameEdges.Add(UiKit.Rect(_focusFrame, 0f, 0f, 0f, 0f, UiTheme.SelectYellow, false, "Edge" + i));
            _arrowRoot = UiKit.Node(content, "Arrow", 0f, 0f, 32f, 32f);
            BuildMap(content);

            _card = UiKit.Node(content, "EnhanceTutorial", 0f, 0f, Width, Height);
            var panel = UiKit.Panel(Ctx, _card, UiPanelKind.Paper, 0f, 0f, Width, Height);
            panel.name = "EnhanceTutorialCard";
            const float Pad = 28f;
            float l32 = UiTheme.LineHeight(UiFontId.F32), l24 = UiTheme.LineHeight(UiFontId.F24);
            UiKit.LabelKey(Ctx, _card, EnhanceNameKeys.TutorialTitle, UiFontId.F16, Pad, Pad - 4f, Width - 2 * Pad - 160f, 24f, UiTheme.PaperText);
            _page = UiKit.Label(Ctx, _card, string.Empty, UiFontId.F16, Width - Pad - 160f, Pad - 4f, 160f, 24f, UiTheme.PaperText, TextAnchor.MiddleRight);
            _title = UiKit.Label(Ctx, _card, string.Empty, UiFontId.F32, Pad, Pad + 24f, Width - 2 * Pad, l32, UiTheme.PaperText);
            _body = UiKit.Label(Ctx, _card, string.Empty, UiFontId.F16, Pad, Pad + 24f + l32 + 12f, Width - 2 * Pad, 168f, UiTheme.PaperText, TextAnchor.UpperLeft, wrap: true);
            _odds = UiKit.Label(Ctx, _card, string.Empty, UiFontId.F16, Pad, Height - Pad - 56f - 64f, Width - 2 * Pad, 56f, UiTheme.PaperText, TextAnchor.UpperLeft, wrap: true);
            const float BtnW = 180f, BtnH = 56f, Gap = 16f;
            float by = Height - Pad - BtnH;
            _prev = UiKit.Button(Ctx, _card, T(EnhanceNameKeys.CommonPrev), Pad, by, BtnW, BtnH, () => Turn(false), UiButtonStyle.Large, Focus, null, UiFontId.F24);
            _next = UiKit.Button(Ctx, _card, T(EnhanceNameKeys.CommonNext), Width - Pad - 2 * BtnW - Gap, by, BtnW, BtnH, () => Turn(true), UiButtonStyle.Large, Focus, null, UiFontId.F24);
            _close = UiKit.Button(Ctx, _card, T(EnhanceNameKeys.CommonClose), Width - Pad - BtnW, by, BtnW, BtnH, CloseTutorial, UiButtonStyle.Primary, Focus, null, UiFontId.F24);
        }

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            _model.Reset();
            Refresh();
            Focus.SetCursor(_next, false);
        }

        void Turn(bool forward)
        {
            if (forward ? _model.Next() : _model.Prev()) Refresh();
        }

        EnhanceTutorialPage PageLayout(int page)
        {
            if (_tt != null && page >= 1 && page <= _tt.Pages.Count) return _tt.Pages[page - 1];
            float cx = (UiScale.RefWidth - Width) * 0.5f, cy = (UiScale.RefHeight - Height) * 0.5f;
            return new EnhanceTutorialPage { CardX = cx, CardY = cy, CardW = Width, CardH = Height };
        }

        void Refresh()
        {
            var names = Ctx != null ? Ctx.Names : null;
            _page.SetText(_model.PageLabel(names));
            _title.SetText(T(PageTitlePrefix + _model.Page + ".title"));
            string body = _model.Body(names, PageArgs(_model.Page));
            _body.gameObject.SetActive(body != null);
            _body.SetText(body ?? string.Empty);
            string odds = _model.Page == _model.Pages ? OddsLine() : null;
            _odds.gameObject.SetActive(odds != null);
            _odds.SetText(odds ?? string.Empty);
            if (_model.IsFirst) _prev.SetDisabled(null); else _prev.SetEnabled();
            if (_model.IsLast) _next.SetDisabled(null); else _next.SetEnabled();
            Layout(PageLayout(_model.Page));
            UiMotion.Entrance(_card, 0f, UiTheme.PanelPopSeconds, new Vector2(0f, 12f), 0.98f);
            _age = 0f;
        }

        /// <summary>
        /// 강화 화면 밖(지팡이 고르기 위)에서 열렸을 때 쓰는 강화 화면 약도(1/5 크기): 08 10.3절의 칸(위 띠·왼쪽·가운데·오른쪽·아래 띠)을
        /// 회청 테로 그리고, 그 쪽이 가리키는 곳만 선택 노랑으로 채운다. 강화 화면 위에서 열리면 진짜 화면을 직접 가리킨다.
        /// </summary>
        void BuildMap(RectTransform content)
        {
            const float S = MapScale;
            float mw = UiScale.RefWidth * S, mh = UiScale.RefHeight * S;
            _map = UiKit.Node(content, "EnhanceMap", (UiScale.RefWidth - mw) * 0.5f, MapTop, mw, mh);
            UiKit.Rect(_map, 0f, 0f, mw, mh, UiTheme.WoodDark, false, "MapBg");
            float[,] areas = { { 0, 0, 1920, 80 }, { 0, 80, 480, 840 }, { 480, 80, 960, 840 }, { 1440, 80, 480, 840 }, { 0, 920, 1920, 160 } };
            for (int i = 0; i < areas.GetLength(0); i++)
            {
                float x = areas[i, 0] * S, y = areas[i, 1] * S, w = areas[i, 2] * S, h = areas[i, 3] * S;
                UiKit.Rect(_map, x + 1f, y + 1f, w - 2f, h - 2f, UiTheme.Wood, false, "MapArea");
                Outline(_map, x, y, w, h, UiTheme.SlateBlue, 1f);
            }
            _mapFocus = UiKit.Node(_map, "MapFocus", 0f, 0f, 0f, 0f);
            _map.gameObject.SetActive(false);
        }

        const float MapScale = 0.2f, MapTop = 64f;

        static void Outline(RectTransform parent, float x, float y, float w, float h, Color c, float t)
        {
            UiKit.Rect(parent, x, y, w, t, c);
            UiKit.Rect(parent, x, y + h - t, w, t, c);
            UiKit.Rect(parent, x, y, t, h, c);
            UiKit.Rect(parent, x + w - t, y, t, h, c);
        }

        /// <summary>강화 화면 위에서 열렸나(진짜 칸을 가리킬 수 있나).</summary>
        bool OnEnhanceScreen => Args != null && (Args.Entry == ScreenId.Enhance || (Screens != null && Screens.IsOpen(ScreenId.Enhance)));

        void Layout(EnhanceTutorialPage pg)
        {
            if (!OnEnhanceScreen) { LayoutWithMap(pg); return; }
            _map.gameObject.SetActive(false);
            float W = UiScale.RefWidth, H = UiScale.RefHeight;
            bool focus = pg.FocusW > 0f && pg.FocusH > 0f;
            float fx = pg.FocusX, fy = pg.FocusY, fw = pg.FocusW, fh = pg.FocusH;
            if (!focus) { fx = W; fy = H; fw = 0f; fh = 0f; }
            // 가리키는 곳만 밝게: 네 쪽 덮개.
            UiKit.Place(_dim[0].rectTransform, 0f, 0f, W, fy);
            UiKit.Place(_dim[1].rectTransform, 0f, fy + fh, W, Mathf.Max(0f, H - fy - fh));
            UiKit.Place(_dim[2].rectTransform, 0f, fy, fx, fh);
            UiKit.Place(_dim[3].rectTransform, fx + fw, fy, Mathf.Max(0f, W - fx - fw), fh);
            _focusFrame.gameObject.SetActive(focus);
            if (focus)
            {
                const float t = 4f;
                UiKit.Place(_focusFrame, fx, fy, fw, fh);
                UiKit.Place(_frameEdges[0].rectTransform, 0f, 0f, fw, t);
                UiKit.Place(_frameEdges[1].rectTransform, 0f, fh - t, fw, t);
                UiKit.Place(_frameEdges[2].rectTransform, 0f, 0f, t, fh);
                UiKit.Place(_frameEdges[3].rectTransform, fw - t, 0f, t, fh);
            }
            UiKit.Place(_card, pg.CardX, pg.CardY, pg.CardW > 0f ? pg.CardW : Width, pg.CardH > 0f ? pg.CardH : Height);

            // 쪽지에서 가리키는 곳 쪽으로 화살표(쪽지 가장자리 가운데).
            for (int i = _arrowRoot.childCount - 1; i >= 0; i--) Object.Destroy(_arrowRoot.GetChild(i).gameObject);
            _arrowRoot.gameObject.SetActive(focus);
            if (!focus) return;
            float cw = pg.CardW > 0f ? pg.CardW : Width, ch = pg.CardH > 0f ? pg.CardH : Height;
            Vector2 cc = new Vector2(pg.CardX + cw * 0.5f, pg.CardY + ch * 0.5f);
            Vector2 fc = new Vector2(fx + fw * 0.5f, fy + fh * 0.5f);
            Vector2 d = fc - cc;
            const float S = 40f;
            UiTriangleDir dir;
            Vector2 at;
            if (Mathf.Abs(d.x) * ch >= Mathf.Abs(d.y) * cw)
            {
                dir = d.x > 0f ? UiTriangleDir.Right : UiTriangleDir.Left;
                at = new Vector2(d.x > 0f ? pg.CardX + cw + 8f : pg.CardX - 8f - S, cc.y - S * 0.5f);
            }
            else
            {
                dir = d.y > 0f ? UiTriangleDir.Down : UiTriangleDir.Up;
                at = new Vector2(cc.x - S * 0.5f, d.y > 0f ? pg.CardY + ch + 8f : pg.CardY - 8f - S);
            }
            UiKit.Place(_arrowRoot, at.x, at.y, S, S);
            UiKit.Triangle(_arrowRoot, dir, 0f, 0f, S, S, UiTheme.SelectYellow);
        }

        void LayoutWithMap(EnhanceTutorialPage pg)
        {
            float W = UiScale.RefWidth, H = UiScale.RefHeight;
            UiKit.Place(_dim[0].rectTransform, 0f, 0f, W, H);
            for (int i = 1; i < 4; i++) UiKit.Place(_dim[i].rectTransform, 0f, 0f, 0f, 0f);
            _focusFrame.gameObject.SetActive(false);
            _arrowRoot.gameObject.SetActive(false);
            _map.gameObject.SetActive(true);
            for (int i = _mapFocus.childCount - 1; i >= 0; i--) Object.Destroy(_mapFocus.GetChild(i).gameObject);
            if (pg.FocusW > 0f && pg.FocusH > 0f)
            {
                float x = pg.FocusX * MapScale, y = pg.FocusY * MapScale, w = pg.FocusW * MapScale, h = pg.FocusH * MapScale;
                UiKit.Rect(_mapFocus, x, y, w, h, UiTheme.WithAlpha(UiTheme.SelectYellow, 0.35f), false, "Fill");
                Outline(_mapFocus, x, y, w, h, UiTheme.SelectYellow, 3f);
            }
            float cw = pg.CardW > 0f ? pg.CardW : Width, ch = pg.CardH > 0f ? pg.CardH : Height;
            UiKit.Place(_card, (W - cw) * 0.5f, MapTop + H * MapScale + 24f, cw, ch);
        }

        public override void Tick(float dt)
        {
            _age += dt;
            // 테가 천천히 숨 쉰다(번쩍이지 않는다 — 08 14.1절).
            float a = 0.7f + 0.3f * Mathf.Sin(_age * Mathf.PI);
            for (int i = 0; i < _frameEdges.Count; i++) _frameEdges[i].color = UiTheme.WithAlpha(UiTheme.SelectYellow, a);
        }

        /// <summary>쪽 본문의 값(enhance.json).</summary>
        object[] PageArgs(int page)
        {
            var cfg = Ctx != null && Ctx.Content != null ? Ctx.Content.Enhance : null;
            if (cfg == null) return new object[0];
            var sac = cfg.Sacrifice ?? new SacrificeConfig();
            return new object[]
            {
                "cap", cfg.ProbCapPct,
                "sac_common", sac.ScoreCommon, "sac_rare", sac.ScoreRare, "sac_hero", sac.ScoreHero, "sac_cap", sac.Cap, "sac_max", sac.MaxBlocks,
                "same", sac.SameElement,
                "aura_step", cfg.AuraStepPct, "aura_max", cfg.AuraMax, "aura_total", cfg.AuraStepPct * cfg.AuraMax,
                "great", cfg.GreatRatioPct, "mut", cfg.MutationRatioPct,
                "safe", cfg.DropSafeMaxStage, "gap", cfg.FloorGap,
                "first", cfg.BaseProb != null && cfg.BaseProb.Count > 0 ? cfg.BaseProb[0] : 0,
                "last", cfg.BaseProb != null && cfg.BaseProb.Count > 0 ? cfg.BaseProb[cfg.BaseProb.Count - 1] : 0,
                "max_stage", cfg.MaxStage
            };
        }

        /// <summary>지금 가진 지팡이(가장 높은 단계) 기준 "다음 단계까지 평균 · 운이 나쁜 10%".</summary>
        string OddsLine()
        {
            var p = Ctx != null ? Ctx.Profile : null;
            var cfg = Ctx != null && Ctx.Content != null ? Ctx.Content.Enhance : null;
            if (p == null || cfg == null || p.Wands == null || p.Wands.Count == 0) return null;
            WandData best = null;
            foreach (var w in p.Wands) if (w != null && (best == null || w.Stage > best.Stage)) best = w;
            if (best == null || best.Stage >= cfg.MaxStage) return null;
            var o = EnhanceTutorialOddsCalc.Compute(cfg, best.Stage, best.Aura, best.FloorPeak, _tt);
            if (!o.Valid) return null;
            var names = Ctx.Names;
            return F(EnhanceTutorialKeys.Odds, "wand", T("wand." + best.Character), "from", o.Stage, "to", o.Target,
                "avg", DescribeFormat.Num((float)o.AvgTries), "bad", o.UnluckyTries,
                "pct", 100 - _tt.OddsPercentile,
                "gold", names != null ? names.Number((long)System.Math.Round(o.AvgGold)) : o.AvgGold.ToString(),
                "bad_gold", names != null ? names.Number(o.UnluckyGold) : o.UnluckyGold.ToString());
        }

        void CloseTutorial()
        {
            if (Ctx == null) return;
            _model.Close(Ctx.Profile, Ctx.Profiles);
            var next = Args.Get(ScreenArgKeys.NextScreen, ScreenId.None);
            if (next != ScreenId.None) Screens?.Replace(next, Args.Get<ScreenArgs>(ScreenArgKeys.NextArgs));
            else Close();
        }

        public override bool OnBack()
        {
            CloseTutorial();
            return true;
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (key == MenuKey.Cancel) { if (!_model.IsFirst) Turn(false); else CloseTutorial(); return true; }
            if (key == MenuKey.PrevTab || key == MenuKey.Left && Focus.Current == null) { Turn(false); return true; }
            if (key == MenuKey.NextTab) { Turn(true); return true; }
            base.OnMenuKey(key);
            return true;
        }
    }

    public static class EnhanceTutorialKeys
    {
        public const string Odds = "str.enhance_tutorial.odds";
    }
}
