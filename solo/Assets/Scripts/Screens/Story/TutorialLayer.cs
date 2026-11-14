using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;
using Wizard.Solo.UI;
using Wizard.Solo.View;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>
    /// 튜토리얼 판의 전장 위 표시(튜토리얼 판일 때만, 다른 판에서는 아무것도 그리지 않는다):
    ///   이동 단계의 목표 고리(천천히 숨 쉬는 테 + "여기" 글자), 허수아비를 겨누는 단계의 표적 고리,
    ///   단계 완료 때 캐릭터 둘레의 칭찬 반짝(좋음 초록 점 8개가 퍼진다).
    /// 상태(w.Run.TutorialStep + TutorialPlan)만 보고 그린다 — 이벤트는 반짝의 시작 틱만 쓴다.
    /// 위험색을 쓰지 않는다.
    /// </summary>
    public sealed class TutorialLayer : IWorldViewLayer
    {
        public const string TargetLabelKey = "str.tutorial.target_here";
        const int SparkCount = 8;
        const float RingPx = 2f;

        ViewContext _vc;
        TutorialPlan _plan;
        RectTransform _root;
        UImage _target, _targetCore, _dummyRing;
        UiLabel _targetLabel;
        readonly List<UImage> _sparks = new List<UImage>();
        int _praiseTick = int.MinValue;
        float _time;

        GameContext Ctx => _vc != null ? _vc.Game : null;

        public void Build(ViewContext vc, SimWorld w)
        {
            Dispose();
            _vc = vc;
            _plan = TutorialPlan.From(Ctx != null ? Ctx.Content : (w != null ? w.Content : null));
            var parent = vc != null ? vc.HudRoot : null;
            if (parent == null) return;
            var go = new GameObject("TutorialMarks", typeof(RectTransform));
            _root = (RectTransform)go.transform;
            _root.SetParent(parent, false);
            _root.anchorMin = _root.anchorMax = _root.pivot = new Vector2(0.5f, 0.5f);
            _root.sizeDelta = new Vector2(UiScale.RefWidth, UiScale.RefHeight);
            _root.anchoredPosition = Vector2.zero;
            _root.SetAsFirstSibling();

            float cell = SimConst.PixelsPerUnit;
            float r = Mathf.Max(0.5f, _plan.MoveTargetRadius) * cell;
            _target = Circle(r, true, UiTheme.SelectYellow);
            _targetCore = Circle(r * 0.35f, false, UiTheme.WithAlpha(UiTheme.SelectYellow, 0.35f));
            _dummyRing = Circle(cell * 0.8f, true, UiTheme.WithAlpha(UiTheme.Chalk, 0.8f));
            var names = Ctx != null ? Ctx.Names : null;
            _targetLabel = UiKit.Label(Ctx, _root, names != null ? names.Get(TargetLabelKey) : TargetLabelKey, UiFontId.F16, 0f, 0f, 160f,
                UiTheme.LineHeight(UiFontId.F16), UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            var lr = _targetLabel.Rect;
            lr.anchorMin = lr.anchorMax = lr.pivot = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < SparkCount; i++) _sparks.Add(Circle(5f, false, UiTheme.Good));
            Render(w, 1f, 0f);
        }

        UImage Circle(float radiusPx, bool ring, Color c)
        {
            var go = new GameObject("TutorialMark", typeof(RectTransform), typeof(UImage));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_root, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(radiusPx * 2f, radiusPx * 2f);
            var img = go.GetComponent<UImage>();
            img.raycastTarget = false;
            img.color = c;
            var art = Ctx != null ? Ctx.Art : null;
            if (art != null)
            {
                try { img.sprite = art.Circle(Mathf.Max(2, Mathf.RoundToInt(radiusPx)), ring, ring ? Mathf.RoundToInt(RingPx * 2f) : 2); }
                catch (System.Exception) { }
            }
            go.SetActive(false);
            return img;
        }

        public void OnEvent(SimWorld w, in SimEvent e)
        {
            if (e.Type == SimEventType.TutorialStepDone) _praiseTick = e.Tick;
        }

        static bool IsTutorial(SimWorld w) =>
            w != null && w.Run != null && w.Run.Config != null && w.Run.Config.Mode == RunMode.Tutorial && w.Run.Phase == RunPhase.Practice;

        public void Render(SimWorld w, float alpha, float dt)
        {
            if (_root == null) return;
            _time += dt;
            bool on = IsTutorial(w) && _plan != null;
            string id = on ? _plan.IdOf(w.Run.TutorialStep) : null;
            if (_root.gameObject.activeSelf != on) _root.gameObject.SetActive(on);
            if (!on) return;

            bool move = id == TutorialStepIds.Move;
            Show(_target, move); Show(_targetCore, move); _targetLabel.gameObject.SetActive(move);
            if (move)
            {
                Vector2 pos = _vc.WorldToHud(_plan.MoveTarget(w));
                float breathe = 1f + 0.08f * Mathf.Sin(_time * Mathf.PI * 2f * 0.75f);
                Place(_target, pos, breathe);
                Place(_targetCore, pos, 1f);
                _targetLabel.Rect.anchoredPosition = pos + new Vector2(0f, Mathf.Max(0.5f, _plan.MoveTargetRadius) * SimConst.PixelsPerUnit + 18f);
            }

            bool aim = id == TutorialStepIds.Fire || id == TutorialStepIds.Crit || id == TutorialStepIds.Combo || id == TutorialStepIds.Reverse;
            EnemyState dummy = aim ? FindDummy(w) : null;
            Show(_dummyRing, dummy != null);
            if (dummy != null) Place(_dummyRing, _vc.WorldToHud(dummy.Pos), 1f + 0.05f * Mathf.Sin(_time * Mathf.PI * 2f));

            var p = w.Players != null && w.Players.Length > 0 ? w.Players[0] : null;
            int age = w.Tick - _praiseTick;
            int life = SimConst.Ticks(_plan.PraiseSeconds > 0f ? _plan.PraiseSeconds : 1f);
            bool spark = p != null && age >= 0 && age < life;
            for (int i = 0; i < _sparks.Count; i++)
            {
                Show(_sparks[i], spark);
                if (!spark) continue;
                float t = (float)age / life;
                float ang = i * Mathf.PI * 2f / _sparks.Count + t;
                Vector2 c = _vc.WorldToHud(p.Pos) + new Vector2(0f, SimConst.PixelsPerUnit * 0.4f);
                float rad = 24f + 72f * UiMotion.EaseOutCubic(t);
                _sparks[i].rectTransform.anchoredPosition = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad;
                _sparks[i].color = UiTheme.WithAlpha(UiTheme.Good, 1f - t);
            }
        }

        static EnemyState FindDummy(SimWorld w)
        {
            for (int i = 0; i < w.Enemies.Count; i++)
            {
                var e = w.Enemies[i];
                if (!e.Dead && e.DefId == Ids.Enemy.Dummy) return e;
            }
            return null;
        }

        static void Show(UImage img, bool on)
        {
            if (img != null && img.gameObject.activeSelf != on) img.gameObject.SetActive(on);
        }

        static void Place(UImage img, Vector2 pos, float scale)
        {
            var rt = img.rectTransform;
            rt.anchoredPosition = pos;
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            _sparks.Clear();
            _praiseTick = int.MinValue;
        }
    }
}
