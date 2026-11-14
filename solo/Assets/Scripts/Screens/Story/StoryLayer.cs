using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using Wizard.Solo.View;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>
    /// 첫 만남·복수 연출, 대사 판, 보스 등장 이름 띠, 처음 쪽지, 최종 격노 가장자리(00 문서 7절 AddStory).
    /// vc.HudRoot 아래 uGUI로 그린다(캔버스·EventSystem을 만들지 않는다). 시뮬레이션 상태를 읽기만 하고, 무엇이 보일지는
    /// StoryDirector(순수)가 정한다. 연출은 결과를 바꾸지 않고 게임 틱을 멈추지 않으며, 복수 연출의 길이는 Run이 WaveClearing으로 잡는다.
    /// Build는 이벤트 없이 지금 상태만 보고 시작한다(진행 중이던 일회 연출은 다시 틀지 않는다).
    /// 위험색은 적의 공격(되비침의 만개 탄)과 최종 격노 가장자리(허용된 HUD 자리)에만 쓴다.
    /// </summary>
    public sealed class StoryLayer : IWorldViewLayer
    {
        public const string SkipActionKey = "str.story.skip";
        public const string InputLockedKey = "str.story.input_locked";
        public const string RevengeTagKey = "revenge.tag";
        public const string TitleBandKey = "revenge.title_band";
        public const string FirstNoteKey = "revenge.tracker.first_note";
        public const string EpithetSuffix = ".epithet";

        ViewContext _vc;
        SimWorld _w;
        StoryTuning _t;
        StoryDirector _dir;
        StoryFrame _last;

        RectTransform _root;      // 왼쪽 위 원점 1920×1080
        RectTransform _world;     // HudRoot와 같은 가운데 원점(WorldToHud 좌표)

        // 화면 고정
        UImage _collapse, _flash;
        readonly UImage[] _border = new UImage[4];
        readonly UImage[] _rageEdge = new UImage[4];
        RectTransform _intro;
        UiLabel _introName, _introEpithet, _introTag;
        RectTransform _dialog;
        UiLabel _dialogText;
        RectTransform _note;
        UiLabel _noteText;
        RectTransform _band;
        UiLabel _bandText, _skipHint;

        // 전장 위
        UiLabel _locked;
        readonly List<UImage> _crack = new List<UImage>();
        readonly List<UImage> _bursts = new List<UImage>();
        readonly List<UImage> _dust = new List<UImage>();
        readonly List<UImage> _shards = new List<UImage>();
        readonly List<UImage> _revCrack = new List<UImage>();
        UImage _ornament;

        string _shownDialogKey, _shownIntroBoss;
        bool _shownIntroTag;

        public StoryDirector Director => _dir;
        public StoryFrame LastFrame => _last;
        public RectTransform Root => _root;

        GameContext Ctx => _vc != null ? _vc.Game : null;
        INameTable Names => Ctx != null ? Ctx.Names : null;

        public void Build(ViewContext vc, SimWorld w)
        {
            Dispose();
            _vc = vc;
            _w = w;
            var ctx = Ctx;
            _t = StoryTuning.From(ctx != null ? ctx.Content : (w != null ? w.Content : null));
            _dir = new StoryDirector(_t);
            _dir.Reset();
            var parent = vc != null ? vc.HudRoot : null;
            if (parent == null) return;

            _root = UiKit.Node(parent, "Story", 0f, 0f, UiScale.RefWidth, UiScale.RefHeight);
            var wgo = new GameObject("StoryWorld", typeof(RectTransform));
            _world = (RectTransform)wgo.transform;
            _world.SetParent(_root, false);
            _world.anchorMin = _world.anchorMax = _world.pivot = new Vector2(0.5f, 0.5f);
            _world.sizeDelta = new Vector2(UiScale.RefWidth, UiScale.RefHeight);
            _world.anchoredPosition = Vector2.zero;
            // _root는 HudRoot 왼쪽 위에 1920×1080으로 놓였고 HudRoot도 1920×1080 가운데 정렬이라, _world의 가운데 = HudRoot의 원점이다.

            BuildWorldMarks(ctx);
            BuildScreenFixed(ctx);
            Render(w, 1f, 0f);
        }

        void BuildScreenFixed(GameContext ctx)
        {
            float W = UiScale.RefWidth, H = UiScale.RefHeight;
            _collapse = UiKit.Rect(_root, 0f, 0f, W, H, StoryColors.CollapseGray, false, "CollapseTint");

            var ir = _t.IntroBand;
            _intro = UiKit.Node(_root, "IntroBand", ir.X, ir.Y, ir.W, ir.H);
            UiKit.Panel(ctx, _intro, UiPanelKind.Heading, 0f, 0f, ir.W, ir.H);
            float l32 = UiTheme.LineHeight(UiFontId.F32), l16 = UiTheme.LineHeight(UiFontId.F16), l24 = UiTheme.LineHeight(UiFontId.F24);
            float pad = (ir.H - l32 - l16) / 3f;
            _introTag = UiKit.Label(ctx, _intro, T(RevengeTagKey), UiFontId.F24, pad, (ir.H - l24) * 0.5f, ir.W * 0.25f, l24, StoryColors.Silver);
            _introName = UiKit.Label(ctx, _intro, string.Empty, UiFontId.F32, 0f, pad, ir.W, l32, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _introEpithet = UiKit.Label(ctx, _intro, string.Empty, UiFontId.F16, 0f, pad * 2f + l32, ir.W, l16, UiTheme.SlateBlue, TextAnchor.MiddleCenter);

            var br = _t.TitleBand;
            _band = UiKit.Node(_root, "RevengeTitleBand", br.X, br.Y, br.W, br.H);
            UiKit.Rect(_band, 0f, 0f, br.W, br.H, StoryColors.Silver, false, "SilverFrame");
            UiKit.Rect(_band, 2f, 2f, br.W - 4f, br.H - 4f, UiTheme.Wood, false, "Inner");
            _bandText = UiKit.Label(ctx, _band, string.Empty, UiFontId.F32, 0f, (br.H - l32) * 0.5f, br.W, l32, UiTheme.Chalk, TextAnchor.MiddleCenter);
            _skipHint = UiKit.Label(ctx, _root, SkipHintText(), UiFontId.F16, br.X, br.Y + br.H + 8f, br.W, l16, UiTheme.SlateBlue, TextAnchor.MiddleRight);

            var dr = _t.Dialog;
            _dialog = UiKit.Node(_root, "PhantomDialog", dr.X, dr.Y, dr.W, dr.H);
            UiKit.Panel(ctx, _dialog, UiPanelKind.Board, 0f, 0f, dr.W, dr.H);
            _dialogText = UiKit.Label(ctx, _dialog, string.Empty, UiFontId.F24, 16f, 0f, dr.W - 32f, dr.H, UiTheme.Chalk, TextAnchor.MiddleCenter, true);

            _note = UiKit.Node(_root, "FirstNote", W - ScreenManager.ToastMargin - ScreenManager.ToastWidth, ScreenManager.ToastMargin,
                ScreenManager.ToastWidth, ScreenManager.ToastHeight);
            UiKit.Panel(ctx, _note, UiPanelKind.Toast, 0f, 0f, ScreenManager.ToastWidth, ScreenManager.ToastHeight);
            _noteText = UiKit.Label(ctx, _note, T(FirstNoteKey), UiFontId.F16, 16f, 0f, ScreenManager.ToastWidth - 32f, ScreenManager.ToastHeight,
                UiTheme.PaperText, TextAnchor.MiddleCenter, true);

            float e = _t.FinalRageEdgePx;
            _rageEdge[0] = UiKit.Rect(_root, 0f, 0f, W, e, StoryColors.DangerMagenta, false, "RageEdgeTop");
            _rageEdge[1] = UiKit.Rect(_root, 0f, H - e, W, e, StoryColors.DangerMagenta, false, "RageEdgeBottom");
            _rageEdge[2] = UiKit.Rect(_root, 0f, 0f, e, H, StoryColors.DangerMagenta, false, "RageEdgeLeft");
            _rageEdge[3] = UiKit.Rect(_root, W - e, 0f, e, H, StoryColors.DangerMagenta, false, "RageEdgeRight");

            _flash = UiKit.Rect(_root, 0f, 0f, W, H, StoryColors.FlashWhite, false, "Flash");
            float b = _t.FlashBorderPx;
            _border[0] = UiKit.Rect(_root, 0f, 0f, W, b, StoryColors.FlashWhite, false, "FlashBorderTop");
            _border[1] = UiKit.Rect(_root, 0f, H - b, W, b, StoryColors.FlashWhite, false, "FlashBorderBottom");
            _border[2] = UiKit.Rect(_root, 0f, 0f, b, H, StoryColors.FlashWhite, false, "FlashBorderLeft");
            _border[3] = UiKit.Rect(_root, W - b, 0f, b, H, StoryColors.FlashWhite, false, "FlashBorderRight");
        }

        void BuildWorldMarks(GameContext ctx)
        {
            int dirs = Dir8Count;
            for (int i = 0; i < Ids3; i++) _crack.Add(Dot(_t.RevengeCrackPx, StoryColors.Silver));
            int bursts = _t.ReflectionBurstTicks != null ? Math.Min(_t.ReflectionBurstTicks.Length, Ids3) : 0;
            for (int i = 0; i < bursts * dirs; i++) _bursts.Add(Dot(_t.ParticlePx * 2f, StoryColors.DangerRed));
            for (int i = 0; i < _t.DissolveCount; i++) _dust.Add(Dot(_t.ParticlePx, StoryColors.Silver));
            for (int i = 0; i < _t.RevengeShardCount; i++) _shards.Add(Dot(_t.RevengeShardPx, StoryColors.Silver));
            for (int i = 0; i < Ids3; i++) _revCrack.Add(Dot(_t.RevengeCrackPx, StoryColors.Silver));
            _ornament = Dot(_t.RevengeOrnamentPx, StoryColors.Silver);
            _ornament.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            float l12 = UiTheme.LineHeight(UiFontId.F12);
            _locked = UiKit.Label(ctx, _world, T(InputLockedKey), UiFontId.F12, 0f, 0f, 240f, l12, StoryColors.Silver, TextAnchor.MiddleCenter, false, true);
            var lr = _locked.Rect;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>8방향(만개의 방향 수). 01 요약서의 "8방향".</summary>
        const int Dir8Count = 8;
        /// <summary>금 줄기·만개 묶음 수(자료구조 크기).</summary>
        const int Ids3 = 3;

        UImage Dot(float size, Color c)
        {
            var go = new GameObject("StoryMark", typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_world, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<UImage>();
            img.color = c;
            img.raycastTarget = false;
            var art = Ctx != null ? Ctx.Art : null;
            if (art != null)
            {
                try { img.sprite = art.WhitePixel; } catch (Exception) { }
            }
            go.SetActive(false);
            return img;
        }

        string T(string key) => Names != null ? Names.Get(key) : key;

        string SkipHintText()
        {
            var names = Names;
            Key k = Key.J;
            var settings = Ctx != null && Ctx.Settings != null ? Ctx.Settings.Current : null;
            if (settings != null)
            {
                int bound = KeyLayouts.PrimaryOf(settings, LogicalKey.Fire);
                if (bound > 0) k = (Key)bound;
            }
            return UiKeyHint.Compose(names, new[] { new UiKeyHint(k, SkipActionKey) });
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_dir == null) return;
            _dir.OnEvent(w, e);
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (_dir == null) return;
            if (w == null) w = _w;
            var opts = _vc != null ? _vc.Options : null;
            int level = opts != null ? opts.EffectsLevel : 0;
            float flashScale = opts != null ? opts.FlashScale : 1f;
            var f = _dir.Compute(w, level, flashScale);
            _last = f;
            FlushCues();
            if (_root == null) return;
            Apply(w, f);
        }

        void FlushCues()
        {
            var snd = Ctx != null ? Ctx.Sound : null;
            for (int i = 0; i < _dir.Cues.Count; i++)
            {
                if (snd == null) break;
                try { snd.Play(_dir.Cues[i].Id, _dir.Cues[i].Bus); }
                catch (Exception ex) { Debug.LogWarning("StoryLayer: sound " + _dir.Cues[i].Id + ": " + ex.Message); }
            }
            _dir.Cues.Clear();
            for (int i = 0; i < _dir.PendingShakes; i++)
                if (_vc != null && _vc.Shake != null) _vc.Shake(_t.ShakeBigPx, _t.ShakeBigSeconds);
            _dir.PendingShakes = 0;
        }

        void Apply(SimWorld w, StoryFrame f)
        {
            // 쓰러짐 무채색(셰이더 없이 회색 덮개로 대신한다).
            SetAlpha(_collapse, f.Collapse.Visible ? f.Collapse.OverlayAlpha : 0f, StoryColors.CollapseGray);

            // 보스 등장 이름 띠.
            Show(_intro, f.IntroVisible);
            if (f.IntroVisible && (f.IntroBossId != _shownIntroBoss || f.IntroRevengeTag != _shownIntroTag))
            {
                _shownIntroBoss = f.IntroBossId;
                _shownIntroTag = f.IntroRevengeTag;
                string id = f.IntroBossId ?? string.Empty;
                _introName.SetText(T(id));
                string ep = id + EpithetSuffix;
                bool hasEp = Names != null && Names.Has(ep);
                _introEpithet.SetText(hasEp ? T(ep) : string.Empty);
                Show(_introEpithet, hasEp);
                Show(_introTag, f.IntroRevengeTag);
            }

            // 대사 판.
            Show(_dialog, f.DialogKey != null);
            if (f.DialogKey != null && f.DialogKey != _shownDialogKey) _dialogText.SetText(T(f.DialogKey));
            _shownDialogKey = f.DialogKey;

            // 처음 쪽지(위에서 내려와 4초 뒤 올라감).
            Show(_note, f.Note.Visible);
            if (f.Note.Visible)
            {
                float y = ScreenManager.ToastMargin - (1f - f.Note.Drop01) * (ScreenManager.ToastHeight + ScreenManager.ToastMargin);
                UiKit.Place(_note, UiScale.RefWidth - ScreenManager.ToastMargin - ScreenManager.ToastWidth, y,
                    ScreenManager.ToastWidth, ScreenManager.ToastHeight);
            }

            // 복수 칭호 띠 + 넘기기 안내.
            Show(_band, f.Revenge.Visible && f.Revenge.TitleBand);
            if (f.Revenge.Visible && f.Revenge.TitleBand) _bandText.SetText(TitleBandText(w));
            Show(_skipHint, f.Revenge.Visible && f.Revenge.SkipHint);

            // 최종 격노 가장자리.
            for (int i = 0; i < _rageEdge.Length; i++) Show(_rageEdge[i], f.FinalRageEdge);

            // 번쩍(관리기를 거친 값).
            SetAlpha(_flash, f.FlashAlpha, StoryColors.FlashWhite);
            for (int i = 0; i < _border.Length; i++) Show(_border[i], f.FlashBorder);

            ApplyWorld(w, f);
        }

        string TitleBandText(SimWorld w)
        {
            // 솔로의 ○○○ = 캐릭터 표시 이름(00 D-27, 10 G-4).
            string charId = null;
            if (w != null)
                for (int i = 0; i < w.Players.Length; i++)
                    if (w.Players[i] != null && w.Players[i].Active) { charId = w.Players[i].CharId; break; }
            if (charId == null && w != null && w.Run != null && w.Run.Config != null) charId = w.Run.Config.CharId;
            string name = charId != null ? T(charId) : string.Empty;
            return Names != null ? Names.Format(TitleBandKey, "nickname", name) : TitleBandKey;
        }

        void ApplyWorld(SimWorld w, StoryFrame f)
        {
            // 거울 금(허상 체력 바닥, 0.4초).
            PlaceCrack(_crack, f.MirrorCrackT >= 0f, f.MirrorCrackPos, f.MirrorCrackT < 0f ? 0f : 1f - f.MirrorCrackT, 1f);

            // 되비침 만개 세 번(8방향, 위험색 — 적의 공격).
            int dirs = Dir8Count;
            for (int b = 0; b * dirs < _bursts.Count; b++)
            {
                float p = f.Reflection.Visible ? f.Reflection.BurstAt(b) : -1f;
                for (int d = 0; d < dirs; d++)
                {
                    var img = _bursts[b * dirs + d];
                    if (p < 0f) { Show(img, false); continue; }
                    float ang = d * (360f / dirs) * Mathf.Deg2Rad;
                    Vector2 c = WorldToHud(f.ReflectionPos);
                    Show(img, true);
                    img.rectTransform.anchoredPosition = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (p * _t.ReflectionBurstRadiusPx);
                }
            }

            // 쓰러짐 은색 가루(2.60초).
            for (int i = 0; i < _dust.Count; i++)
            {
                var img = _dust[i];
                float p = f.Collapse.DissolveT;
                if (p < 0f) { Show(img, false); continue; }
                Show(img, true);
                float ang = i * (360f / _dust.Count) * Mathf.Deg2Rad;
                Vector2 c = WorldToHud(f.CollapsePos);
                img.rectTransform.anchoredPosition = c + new Vector2(Mathf.Cos(ang) * _t.ParticlePx * 4f, p * _t.DissolveRisePx + Mathf.Sin(ang) * _t.ParticlePx * 4f);
                SetAlpha(img, 1f - p, StoryColors.Silver);
            }

            // 입력 무시 표시.
            bool locked = f.InputLockedMark && w != null;
            Show(_locked, locked);
            if (locked)
            {
                var p0 = FirstActive(w);
                if (p0 != null) _locked.Rect.anchoredPosition = WorldToHud(p0.Pos) + new Vector2(_t.InputLockedOffsetPx.x, -_t.InputLockedOffsetPx.y);
            }

            // 복수: 금(0~60) → 조각(60~84) → 머리 장식(84~).
            var r = f.Revenge;
            PlaceCrack(_revCrack, r.Visible && r.SlowMotion, f.RevengePos, 1f, r.CrackT);
            for (int i = 0; i < _shards.Count; i++)
            {
                var img = _shards[i];
                if (!(r.Visible && r.Shards)) { Show(img, false); continue; }
                Show(img, true);
                float ang = i * (360f / _shards.Count) * Mathf.Deg2Rad;
                img.rectTransform.anchoredPosition = WorldToHud(f.RevengePos) + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (r.ShardsT * _t.RevengeShardSpreadPx);
                SetAlpha(img, 1f - r.ShardsT, StoryColors.Silver);
            }
            bool orn = r.Visible && r.Ornament && w != null;
            Show(_ornament, orn);
            if (orn)
            {
                var p0 = FirstActive(w);
                Vector2 head = p0 != null ? p0.Pos + _t.OrnamentOffsetCells : f.RevengePos;
                Vector2 pos = Vector2.Lerp(f.RevengePos, head, r.OrnamentT);
                _ornament.rectTransform.anchoredPosition = WorldToHud(pos);
            }
        }

        void PlaceCrack(List<UImage> lines, bool visible, Vector2 simPos, float alpha, float grow)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var img = lines[i];
                if (!visible) { Show(img, false); continue; }
                Show(img, true);
                var rt = img.rectTransform;
                rt.anchoredPosition = WorldToHud(simPos);
                rt.sizeDelta = new Vector2(Mathf.Max(1f, _t.RevengeCrackPx * grow), 2f);
                rt.localRotation = Quaternion.Euler(0f, 0f, i * (180f / lines.Count) + 30f);
                SetAlpha(img, alpha, StoryColors.Silver);
            }
        }

        static PlayerState FirstActive(SimWorld w)
        {
            if (w == null) return null;
            for (int i = 0; i < w.Players.Length; i++)
                if (w.Players[i] != null && w.Players[i].Active) return w.Players[i];
            return null;
        }

        Vector2 WorldToHud(Vector2 simPos) => _vc != null ? _vc.WorldToHud(simPos) : Vector2.zero;

        static void Show(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }

        static void SetAlpha(UImage img, float a, Color baseColor)
        {
            if (img == null) return;
            bool on = a > 0f;
            if (img.gameObject.activeSelf != on) img.gameObject.SetActive(on);
            if (on) img.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * Mathf.Clamp01(a));
        }

        public void Dispose()
        {
            _crack.Clear(); _bursts.Clear(); _dust.Clear(); _shards.Clear(); _revCrack.Clear();
            _ornament = null; _locked = null;
            for (int i = 0; i < _border.Length; i++) _border[i] = null;
            for (int i = 0; i < _rageEdge.Length; i++) _rageEdge[i] = null;
            _intro = null; _dialog = null; _note = null; _band = null; _collapse = null; _flash = null;
            _shownDialogKey = null; _shownIntroBoss = null; _shownIntroTag = false;
            if (_root != null)
            {
                var go = _root.gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(go);
                else UnityEngine.Object.DestroyImmediate(go);
            }
            _root = null;
            _world = null;
            _vc = null;
            _w = null;
        }
    }
}
