using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 전투 HUD 전부와 캐릭터 둘레 표시(00 문서 7절 AddHud = G17, 08 요약서 6절).
    /// - HUD의 모든 것은 vc.HudRoot(1920×1080 루트) 아래에 둔다. 캔버스·CanvasScaler·EventSystem을 만들지 않는다.
    /// - 캐릭터 둘레의 글자·숫자는 vc.WorldTextRoot 아래 vc.WorldToHud 자리에, 열 링·시야 화살표는 전장 층 SortBands.PlayerMarks에.
    /// - 상태를 읽기만 한다. Build는 이벤트 없이 지금 상태만 보고 그릴 수 있다(보조 판에서 돌아올 때) — 이벤트는 한 번 일어나는 연출만 켠다.
    /// - 열 링·시야 화살표·피격 표시는 EffectsLevel·옵션과 무관하게 그린다(줄일 수 없는 것).
    /// </summary>
    public sealed class HudLayer : IWorldViewLayer
    {
        ViewContext _vc;
        SimWorld _w;
        HudArt _art;
        HudTuning _tuning;
        RectTransform _root;

        HudTopPanel _top;
        HudBossPanel _boss;
        HudStatusPanel _status;
        HudCircuitPanel _circuit;
        HudNoticePanel _notice;
        readonly List<CharacterMarksView> _marks = new List<CharacterMarksView>();
        readonly List<HeatRingView> _rings = new List<HeatRingView>();

        public HudFrame LastFrame { get; private set; }
        public HudTopPanel Top => _top;
        public HudBossPanel Boss => _boss;
        public HudStatusPanel Status => _status;
        public HudCircuitPanel Circuit => _circuit;
        public HudNoticePanel Notice => _notice;
        public IReadOnlyList<CharacterMarksView> Marks => _marks;
        public IReadOnlyList<HeatRingView> Rings => _rings;
        public HudTuning Tuning => _tuning;
        public RectTransform Root => _root;

        /// <summary>꾸밈 연출(튀기·흔들기·숨쉬기)을 켜는가. 연출 감도 2(최소)면 끈다 — 정보 표시(피격 빛, 깨짐, 열 링)는 그대로.</summary>
        public bool MotionOn => _vc == null || _vc.Options == null || _vc.Options.EffectsLevel < 2;

        public INameTable Names => _vc != null && _vc.Game != null ? _vc.Game.Names : null;

        /// <summary>HUD 사건 소리(ui.hud.*, 08 요약서 6.5절). 소리 파일이 없어 무음 재생기가 받는다.</summary>
        public void Play(string eventId)
        {
            var s = _vc != null && _vc.Game != null ? _vc.Game.Sound : null;
            if (s == null) return;
            try { s.Play(eventId, SoundBus.Ui); } catch (Exception ex) { Debug.LogWarning("HudLayer: sound " + eventId + ": " + ex.Message); }
        }

        public void Build(ViewContext vc, SimWorld w)
        {
            Dispose();
            _vc = vc;
            _w = w;
            var ctx = vc != null ? vc.Game : null;
            _art = new HudArt(ctx);
            _tuning = HudTuning.FromJson(ctx != null && ctx.Content != null ? SafeExtra(ctx.Content) : null);
            var parent = vc != null ? vc.HudRoot : null;
            _root = UiKit.Node(parent, "BattleHud", 0, 0, UiScale.RefWidth, UiScale.RefHeight);
            FitToCanvas();

            _top = new HudTopPanel(_art, _root, _tuning);
            _boss = new HudBossPanel(_art, _root);
            _status = new HudStatusPanel(_art, _root, _tuning);
            _circuit = new HudCircuitPanel(_art, _root, _tuning);
            _notice = new HudNoticePanel(_art, _root, _tuning);

            if (w != null && vc != null)
            {
                for (int i = 0; i < w.Players.Length; i++)
                {
                    _marks.Add(new CharacterMarksView(vc, _art, _tuning, i, i == w.ViewSlot ? _status.ComboHudAnchor : null));
                    _rings.Add(new HeatRingView(vc, _art, _tuning));
                }
                _boss.SyncFromState(w);
            }
            // 이벤트 없이 지금 상태로 한 번 그린다.
            Render(w, 1f, 0f);
        }

        static Newtonsoft.Json.Linq.JObject SafeExtra(IContentDb db)
        {
            try { return db.SoloExtra(HudTuning.ExtraName); } catch (Exception) { return null; }
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (_root == null) return;
            _top.OnEvent(this, w, e);
            _boss.OnEvent(this, w, e);
            _status.OnEvent(this, w, e);
            _circuit.OnEvent(this, w, e);
            _notice.OnEvent(this, w, e);
            for (int i = 0; i < _marks.Count; i++) _marks[i].OnEvent(this, w, e);
            if (e.Type == SimEventType.HeatUnlocked && e.Slot >= 0 && e.Slot < _rings.Count) _rings[e.Slot].OnUnlocked();
        }

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (_root == null) return;
            if (w == null) w = _w;
            FitToCanvas();
            var ctx = _vc != null ? _vc.Game : null;
            var frame = HudModel.Compute(w, ctx != null ? ctx.Profile : null, ctx != null ? ctx.Content : null, _vc != null ? _vc.Options : null);
            LastFrame = frame;

            _top.Refresh(this, w, frame, dt);
            _boss.Refresh(this, w, frame, dt);
            _status.Refresh(this, w, frame, dt);
            _circuit.Refresh(this, w, frame, dt);
            _notice.Refresh(this, w, frame, dt);

            if (w == null) return;
            var options = _vc != null ? _vc.Options : null;
            for (int i = 0; i < _marks.Count && i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                _marks[i].Render(w, p, options, alpha, dt);
                Vector2 feet = HudModel.FeetOf(HudModel.Interp(p, alpha));
                _rings[i].Render(w, p, feet, dt);
            }
        }

        /// <summary>
        /// HUD 루트를 캔버스 전체 크기로 맞춘다(가운데 고정). 판들이 고정점으로 모서리·가장자리에 붙으므로 16:9가 아닌 창에서도
        /// 화면 가장자리를 따른다(1920×1080 루트는 가운데 16:9 상자). 캔버스가 없으면(테스트) 1920×1080.
        /// </summary>
        void FitToCanvas()
        {
            var hudRoot = _root.parent as RectTransform;
            var canvasRt = hudRoot != null ? hudRoot.parent as RectTransform : null;
            Vector2 size = new Vector2(UiScale.RefWidth, UiScale.RefHeight);
            if (canvasRt != null && canvasRt.GetComponent<Canvas>() != null)
            {
                var r = canvasRt.rect.size;
                size = new Vector2(Mathf.Max(size.x, Mathf.Round(r.x)), Mathf.Max(size.y, Mathf.Round(r.y)));
            }
            if (_root.anchorMin != new Vector2(0.5f, 0.5f) || _root.sizeDelta != size)
            {
                _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
                _root.anchoredPosition = Vector2.zero;
                _root.sizeDelta = size;
            }
        }

        public void Dispose()
        {
            for (int i = 0; i < _marks.Count; i++) _marks[i].Dispose();
            _marks.Clear();
            for (int i = 0; i < _rings.Count; i++) _rings[i].Dispose();
            _rings.Clear();
            if (_top != null) _top.Dispose();
            if (_boss != null) _boss.Dispose();
            if (_status != null) _status.Dispose();
            if (_circuit != null) _circuit.Dispose();
            if (_notice != null) _notice.Dispose();
            _top = null; _boss = null; _status = null; _circuit = null; _notice = null;
            HudArt.Destroy(_root != null ? _root.gameObject : null);
            _root = null;
            _vc = null;
            _w = null;
        }
    }
}
