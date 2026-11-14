using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Hints
{
    /// <summary>
    /// 문맥 힌트 쪽지(처음 일어난 일을 그 순간 한 번만 짧게 알려 준다). 화면 관리자와 따로 도는 캔버스 하나(정렬 350 —
    /// 전체 화면·판·HUD 위 판 위, 확인 창·일시정지 아래)에 그린다. 화면 쌓기에 들어가지 않으므로 전투 입력의 문(Top)을 건드리지 않는다.
    ///
    /// 들을 것: 세션의 RunStarted(막힘 창 비움)·SimEventRaised(HintRules), 매 프레임 맨 위 화면(전리품·야영·정산·강화 첫 진입),
    /// 강화 화면에 있는 동안 프로필 지팡이의 하락 횟수·변이 제시(첫 하락·첫 변이 카드 — 의식 연출 뒤에 띄운다).
    /// 켬/끔은 설정 context_hints, 본 기록은 프로필 flags.hints_seen(HintBook).
    /// 정적 싱글턴이 아니다: ScreenRegistry.RegisterTutorial이 부팅마다 Attach로 새로 만들고, 앞서 만든 것은 걷는다.
    /// </summary>
    public sealed class HintHost : MonoBehaviour
    {
        public const string RootName = "Wizard.Hints";
        public const int SortOrder = 350;
        public const string CloseHintKey = "str.hint.close";
        public const string MarkKey = "str.hint.mark";
        public const string FirstEnhance = "first_enhance";

        GameContext _ctx;
        HintTuning _t;
        HintRules _rules;
        IGameSession _session;
        readonly List<string> _queue = new List<string>();
        /// <summary>줄에 든 때(실시간 초). 너무 오래 기다린 힌트는 그 순간이 지나 버렸으므로 띄우지 않는다(본 것으로 적지도 않는다).</summary>
        readonly List<float> _queuedAt = new List<float>();
        float _clock;
        readonly List<string> _buf = new List<string>();
        readonly List<KeyValuePair<float, string>> _delayed = new List<KeyValuePair<float, string>>();
        ScreenId _lastTop = ScreenId.None;
        int _enhanceDrops = -1;
        bool _enhanceOfferSeen;

        RectTransform _canvasRoot, _card;
        UiLabel _title, _body, _foot;
        UiProgressBar _timer;
        string _showing;
        float _age, _gap;

        public string Showing => _showing;
        public IReadOnlyList<string> Queue => _queue;

        /// <summary>부팅마다 하나. 앞서 만든 것이 있으면 걷는다(PlayMode 테스트가 Boot를 여러 번 부른다).</summary>
        public static HintHost Attach(GameContext ctx)
        {
            var old = FindObjectsByType<HintHost>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < old.Length; i++) if (old[i] != null) Destroy(old[i].gameObject);
            if (!Application.isPlaying) return null;
            var go = new GameObject(RootName);
            DontDestroyOnLoad(go);
            var h = go.AddComponent<HintHost>();
            h.Init(ctx);
            return h;
        }

        void Init(GameContext ctx)
        {
            _ctx = ctx;
            _t = HintTuning.From(ctx != null ? ctx.Content : null);
            _rules = new HintRules(_t);
            _session = ctx != null ? ctx.Session : null;
            if (_session != null)
            {
                _session.RunStarted += OnRunStarted;
                _session.SimEventRaised += OnSimEvent;
            }
        }

        void OnDestroy()
        {
            if (_session != null)
            {
                _session.RunStarted -= OnRunStarted;
                _session.SimEventRaised -= OnSimEvent;
            }
            if (_canvasRoot != null && _canvasRoot.parent != null) Destroy(_canvasRoot.parent.gameObject);
        }

        void OnRunStarted(SimWorld w) => _rules.BeginRun();

        void OnSimEvent(SimWorld w, SimEvent e)
        {
            if (_session == null || !ReferenceEquals(w, _session.MainWorld)) return;
            _buf.Clear();
            _rules.OnEvent(w, e, _buf);
            for (int i = 0; i < _buf.Count; i++) Request(_buf[i]);
        }

        /// <summary>힌트를 띄워 달라(켜져 있고 아직 보일 차례면 줄에 넣는다). 테스트·다른 화면도 부를 수 있다.</summary>
        public bool Request(string id)
        {
            var p = _ctx != null ? _ctx.Profile : null;
            if (id == null || p == null) return false;
            if (!HintBook.Enabled(_ctx.Settings != null ? _ctx.Settings.Current : null)) return false;
            if (!HintBook.CanShow(p, id, _t.StuckMaxShows)) return false;
            if (_showing == id || _queue.Contains(id)) return false;
            _queue.Add(id);
            _queuedAt.Add(_clock);
            return true;
        }

        // ---------------------------------------------------------------- 프레임

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _clock += dt;
            WatchScreens(dt);
            DropStale();
            // 일시정지·확인 창·안내 같은 창(Modal)이 맨 위면 쪽지를 잠깐 감추고 시계도 멈춘다(창의 글을 가리지 않는다).
            bool held = (_session != null && _session.Paused) || ModalOnTop();
            if (_card != null && _showing != null && _card.gameObject.activeSelf == held) _card.gameObject.SetActive(!held);
            if (_showing != null)
            {
                if (!held) _age += dt;
                if (_age >= _t.CardSeconds) Dismiss();
                else Layout();
            }
            else if (_gap > 0f) _gap -= dt;
            else if (_queue.Count > 0 && !held) ShowNext();
        }

        void DropStale()
        {
            for (int i = _queue.Count - 1; i >= 0; i--)
                if (_clock - _queuedAt[i] > _t.QueueMaxWaitSeconds) { _queue.RemoveAt(i); _queuedAt.RemoveAt(i); }
        }

        bool ModalOnTop()
        {
            var m = _ctx != null ? _ctx.Screens as ScreenManager : null;
            var top = m != null && m.Stack != null ? m.Stack.TopScreen : null;
            return top != null && top.Layer == ScreenLayer.Modal;
        }

        void WatchScreens(float dt)
        {
            var screens = _ctx != null ? _ctx.Screens : null;
            if (screens == null) return;
            var top = screens.Top;
            if (top != _lastTop)
            {
                _lastTop = top;
                OnTopChanged(top);
            }
            if (top == ScreenId.Enhance) WatchEnhance();
            for (int i = _delayed.Count - 1; i >= 0; i--)
            {
                var d = _delayed[i];
                float left = d.Key - dt;
                if (left <= 0f) { _delayed.RemoveAt(i); Request(d.Value); }
                else _delayed[i] = new KeyValuePair<float, string>(left, d.Value);
            }
        }

        void OnTopChanged(ScreenId top)
        {
            var w = _session != null ? _session.MainWorld : null;
            bool real = HintRules.IsRealRun(w);
            switch (top)
            {
                case ScreenId.LootPick: if (real) Request(HintIds.FirstLoot); break;
                case ScreenId.Camp: if (real && _session.HasRun && !_session.HasSubRun) Request(HintIds.FirstCamp); break;
                case ScreenId.Settlement:
                    Request(HintIds.FirstSettlement);
                    Request(HintIds.FirstBag);
                    break;
                case ScreenId.Enhance:
                case ScreenId.WandPicker:
                    var p = _ctx.Profile;
                    if (p != null && p.Flags != null && !p.Flags.CrowBundleGiven) Request(FirstEnhance);
                    _enhanceDrops = DropTotal(p);
                    _enhanceOfferSeen = AnyOffer(p);
                    break;
            }
        }

        void WatchEnhance()
        {
            var p = _ctx.Profile;
            int drops = DropTotal(p);
            if (_enhanceDrops >= 0 && drops > _enhanceDrops) Delay(HintIds.FirstDrop);
            _enhanceDrops = drops;
            bool offer = AnyOffer(p);
            if (offer && !_enhanceOfferSeen) Delay(HintIds.FirstMutation);
            _enhanceOfferSeen = offer;
        }

        void Delay(string id) => _delayed.Add(new KeyValuePair<float, string>(_t.EnhanceDelaySeconds, id));

        static int DropTotal(ProfileData p)
        {
            if (p == null || p.Wands == null) return 0;
            int n = 0;
            for (int i = 0; i < p.Wands.Count; i++) if (p.Wands[i] != null) n += p.Wands[i].DropCount;
            return n;
        }

        static bool AnyOffer(ProfileData p)
        {
            if (p == null || p.Wands == null) return false;
            for (int i = 0; i < p.Wands.Count; i++) if (p.Wands[i] != null && p.Wands[i].PendingOffer != null) return true;
            return false;
        }

        // ---------------------------------------------------------------- 쪽지

        void ShowNext()
        {
            string id = _queue[0];
            _queue.RemoveAt(0);
            _queuedAt.RemoveAt(0);
            var p = _ctx.Profile;
            if (!HintBook.CanShow(p, id, _t.StuckMaxShows)) return;
            EnsureView();
            if (_card == null) return;
            _showing = id;
            _age = 0f;
            HintBook.MarkShown(p, id, _ctx.Profiles);
            var names = _ctx.Names;
            _title.SetText((names != null ? names.Get(MarkKey) : string.Empty) + " " + (names != null ? names.Get(HintIds.TitleKey(id)) : id));
            _body.SetText(HintText.Body(_ctx, _session != null ? _session.MainWorld : null, id));
            _card.gameObject.SetActive(true);
            UiSounds.Play(_ctx.Sound, UiSounds.Press);
            Layout();
        }

        /// <summary>지금 쪽지를 닫는다(클릭, 시간 끝).</summary>
        public void Dismiss()
        {
            _showing = null;
            _gap = _t.GapSeconds;
            if (_card != null) _card.gameObject.SetActive(false);
        }

        void Layout()
        {
            if (_card == null) return;
            const float slideSeconds = 0.2f;
            float t = Mathf.Clamp01(_age / slideSeconds);
            float slide = (1f - UiMotion.EaseOutCubic(t)) * (_t.CardW + 24f);
            UiKit.Place(_card, _t.CardX + slide, _t.CardY, _t.CardW, _t.CardH);
            if (_timer != null) _timer.Value01 = 1f - Mathf.Clamp01(_age / Mathf.Max(0.01f, _t.CardSeconds));
        }

        void EnsureView()
        {
            if (_card != null) return;
            _canvasRoot = UiCanvasFactory.Create(RootName + ".Canvas", SortOrder);
            _canvasRoot.parent.SetParent(transform, false);
            float w = _t.CardW, h = _t.CardH;
            const float Pad = 18f;
            var btn = UiKit.Button(_ctx, _canvasRoot, null, _t.CardX, _t.CardY, w, h, Dismiss, UiButtonStyle.Plain, null, "");
            btn.name = "HintCard";
            _card = btn.Rect;
            var root = btn.ContentRoot != null ? btn.ContentRoot : _card;
            UiKit.OrnatePanel(_ctx, root, 0f, 0f, w, h, 2, 6f).raycastTarget = false;
            float l24 = UiTheme.LineHeight(UiFontId.F24), l16 = UiTheme.LineHeight(UiFontId.F16), l12 = UiTheme.LineHeight(UiFontId.F12);
            _title = UiKit.Label(_ctx, root, string.Empty, UiFontId.F24, Pad + 4f, Pad - 2f, w - 2f * Pad, l24, UiTheme.Brass);
            UiKit.DropShadow(_title, 2f);
            _body = UiKit.Label(_ctx, root, string.Empty, UiFontId.F16, Pad + 4f, Pad + l24 + 4f, w - 2f * Pad - 8f, h - Pad * 2f - l24 - l12 - 8f,
                UiTheme.Chalk, TextAnchor.UpperLeft, true);
            _foot = UiKit.Label(_ctx, root, _ctx.Names != null ? _ctx.Names.Get(CloseHintKey) : CloseHintKey, UiFontId.F12,
                Pad, h - Pad - l12 + 4f, w - 2f * Pad, l12, UiTheme.SlateBlue, TextAnchor.MiddleRight);
            _timer = UiKit.ProgressBar(_ctx, root, Pad + 4f, h - Pad + 2f, 160f, 4f, 1f);
            _timer.FillColor = UiTheme.Brass;
            _card.gameObject.SetActive(false);
        }
    }

    /// <summary>힌트 본문 글(값이 필요한 것은 데이터·지금 판에서 읽어 서식으로 넣는다 — 문구에 숫자를 적지 않는다).</summary>
    public static class HintText
    {
        public static string Body(GameContext ctx, SimWorld w, string id)
        {
            var names = ctx != null ? ctx.Names : null;
            if (names == null) return id;
            string key = HintIds.BodyKey(id);
            var p = w != null && w.Players != null && w.Players.Length > 0 ? w.Players[0] : null;
            var db = ctx.Content;
            switch (id)
            {
                case HintIds.FirstLock:
                    return names.Format(key, "sec", Num(p != null ? p.Derived.LockTicks / (float)SimConst.TicksPerSecond : 0f));
                case HintIds.FirstCrystal:
                case HintIds.FirstFizzle:
                case HintIds.StuckFizzle:
                    return names.Format(key, "n", p != null && p.Def != null ? p.Def.ComboPerCrystal : 0, "cap", p != null && p.Def != null ? p.Def.CrystalCap : 0);
                case HintIds.FirstAffinity:
                    return names.Format(key, "weak", Num(db != null && db.Elements != null ? db.Elements.WeakMultiplier : 0f),
                        "resist", Num(db != null && db.Elements != null ? db.Elements.ResistMultiplier : 0f));
                case HintIds.FirstDrop:
                    var e = db != null ? db.Enhance : null;
                    return names.Format(key, "gap", e != null ? e.FloorGap : 0, "aura", e != null ? e.AuraStepPct : 0);
                default:
                    return names.Get(key);
            }
        }

        /// <summary>소수 둘째 자리까지, 끝의 0은 지운다(1.25, 0.8, 3).</summary>
        public static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
