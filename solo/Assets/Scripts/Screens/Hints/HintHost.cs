using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Enhance;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

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
        /// <summary>쪽지를 띄운 문맥 화면(바탕이 이것이 아니게 되면 닫는다).</summary>
        ScreenId _shownIn = ScreenId.None;
        int _enhanceDrops = -1;
        bool _enhanceOfferSeen;

        RectTransform _canvasRoot, _card;
        HintSpot _spot;
        float _cardH;
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
            // 자기 문맥이 아닌 화면에서는 청하지 않는다(정산·메뉴에 판 힌트가 뜨지 않게).
            if (!HintContexts.Accepts(id, ContextScreen())) return false;
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
            DropOutOfContext();
            // 일시정지·확인 창·안내 같은 창(Modal)이 맨 위면 쪽지를 잠깐 감추고 시계도 멈춘다(창의 글을 가리지 않는다).
            // 판이 끝난 뒤(쓰러짐 연출·사망 글)에도 감춘다 — 쪽지가 어두워진 화면 위에 밝게 남아 연출을 가리지 않게.
            bool ended = _session != null && _session.MainWorld != null && _session.MainWorld.Run.Phase == RunPhase.RunEnded;
            bool held = (_session != null && _session.Paused) || ScreenHoldsHints() || ended;
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

        /// <summary>
        /// 문맥 화면 = 창(Modal)·토스트·HUD 겹층을 뺀 맨 위 화면(일시정지·안내 창이 떠도 문맥은 그 아래 화면이다).
        /// 화면 관리자가 없으면 Top.
        /// </summary>
        public ScreenId ContextScreen()
        {
            var m = _ctx != null ? _ctx.Screens as ScreenManager : null;
            var entries = m != null && m.Stack != null ? m.Stack.Entries : null;
            if (entries == null) return _ctx != null && _ctx.Screens != null ? _ctx.Screens.Top : ScreenId.None;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var layer = entries[i].Layer;
                if (layer == ScreenLayer.Modal || layer == ScreenLayer.Toast || layer == ScreenLayer.HudOverlay) continue;
                return entries[i].Id;
            }
            return ScreenId.None;
        }

        /// <summary>문맥이 바뀌었으면 떠 있는 쪽지는 닫고(이미 본 것으로 적혔다), 줄의 힌트 중 문맥을 잃은 것은 버린다(다음에 그 문맥에서 다시 청해진다).</summary>
        void DropOutOfContext()
        {
            var here = ContextScreen();
            if (_showing != null && here != _shownIn) Dismiss();
            for (int i = _queue.Count - 1; i >= 0; i--)
                if (!HintContexts.Accepts(_queue[i], here)) { _queue.RemoveAt(i); _queuedAt.RemoveAt(i); }
            for (int i = _delayed.Count - 1; i >= 0; i--)
                if (!HintContexts.Accepts(_delayed[i].Value, here)) _delayed.RemoveAt(i);
        }

        bool ScreenHoldsHints()
        {
            var m = _ctx != null ? _ctx.Screens as ScreenManager : null;
            var top = m != null && m.Stack != null ? m.Stack.TopScreen : null;
            if (top is EnhanceScreen enhance && enhance.Model != null)
            {
                var phase = enhance.Model.Phase;
                return phase != EnhancePhase.Idle && phase != EnhancePhase.ResultCard && phase != EnhancePhase.MutationChoice;
            }
            return top != null && top.Layer == ScreenLayer.Modal;
        }

        void WatchScreens(float dt)
        {
            var screens = _ctx != null ? _ctx.Screens : null;
            if (screens == null) return;
            var top = ContextScreen();
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
                case ScreenId.Camp:
                    if (real && _session.HasRun && !_session.HasSubRun)
                    {
                        Request(HintIds.FirstCamp);
                        // 처음 비기를 고를 차례: 야영 쪽지 다음에 "비기란?"(자세 + 방향 + 비기 키, 결정으로 값을 치른다).
                        if (Camp.CampRules.ArtPickPending(Camp.CampRules.RewardOf(w, Camp.CampRules.MeOf(w)))) Request(HintIds.FirstArts);
                    }
                    break;
                case ScreenId.ArtTrie: Request(HintIds.FirstArts); break;
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
            var here = ContextScreen();
            if (!HintContexts.Accepts(id, here)) return;
            var names = _ctx.Names;
            string body = HintText.Body(_ctx, _session != null ? _session.MainWorld : null, id);
            BuildView(_t.SpotFor(here), names != null ? names.Get(HintIds.TitleKey(id)) : id, body);
            if (_card == null) return;
            _showing = id;
            _shownIn = here;
            _age = 0f;
            HintBook.MarkShown(p, id, _ctx.Profiles);
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
            float slide = (1f - UiMotion.EaseOutCubic(t)) * (_spot.W + 24f);
            UiKit.Place(_card, _spot.X + slide, _spot.Y, _spot.W, _cardH);
            if (_timer != null) _timer.Value01 = 1f - Mathf.Clamp01(_age / Mathf.Max(0.01f, _t.CardSeconds));
        }

        /// <summary>
        /// 쪽지를 그 화면의 안전 자리(폭)에 맞춰 새로 짓는다. 높이는 본문 줄 수로 정한다(긴 글이 칸 밖으로 넘치지 않게).
        /// </summary>
        void BuildView(HintSpot spot, string title, string body)
        {
            if (_canvasRoot == null)
            {
                _canvasRoot = UiCanvasFactory.Create(RootName + ".Canvas", SortOrder);
                _canvasRoot.parent.SetParent(transform, false);
            }
            if (_card != null) { Destroy(_card.gameObject); _card = null; }
            _spot = spot;
            float w = spot.W;
            const float Pad = 14f, Gap = 6f, TimerW = 56f;
            float l24 = UiTheme.LineHeight(UiFontId.F24), l12 = UiTheme.LineHeight(UiFontId.F12);
            float bodyW = w - 2f * Pad - 8f;
            float bodyH = BodyHeight(body, bodyW);
            float h = Pad + l24 + 4f + bodyH + Gap + l12 + Pad;
            _cardH = h;
            var btn = UiKit.Button(_ctx, _canvasRoot, null, spot.X, spot.Y, w, h, Dismiss, UiButtonStyle.Plain, null, "");
            btn.name = "HintCard";
            _card = btn.Rect;
            var root = btn.ContentRoot != null ? btn.ContentRoot : _card;
            UiKit.Rect(root, 0f, 0f, w, h, UiTheme.WithAlpha(UiTheme.Night, 0.94f), false, "HintBacking");
            UiKit.Rect(root, 0f, 0f, 3f, h, UiTheme.SlateBlue, false, "HintRule");
            UiKit.Label(_ctx, root, title ?? string.Empty, UiFontId.F24, Pad + 4f, Pad - 2f, bodyW, l24, UiTheme.Paper);
            UiKit.Label(_ctx, root, body, UiFontId.F16, Pad + 4f, Pad + l24 + 4f, bodyW, bodyH,
                UiTheme.Chalk, TextAnchor.UpperLeft, true);
            float footY = h - Pad - l12 + 4f;
            UiKit.Label(_ctx, root, _ctx.Names != null ? _ctx.Names.Get(CloseHintKey) : CloseHintKey, UiFontId.F12,
                Pad + TimerW + Gap, footY, w - 2f * Pad - TimerW - Gap, l12, UiTheme.SlateBlue, TextAnchor.MiddleRight);
            _timer = UiKit.ProgressBar(_ctx, root, Pad + 4f, footY + (l12 - 4f) * 0.5f, TimerW, 4f, 1f);
            _timer.FillColor = UiTheme.SlateBlue;
            _card.gameObject.SetActive(false);
        }

        /// <summary>
        /// 본문 높이: 글자 폭 규칙(UiTextFit)으로 줄 수를 세고 낱말 줄바꿈 여유로 한 줄을 더한다.
        /// 여러 줄 칸은 lineSpacing 1이라 한 줄이 글자 크기 + 2px쯤이다(LineHeight는 한 줄 칸의 높이라 쓰지 않는다).
        /// </summary>
        public static float BodyHeight(string body, float widthPx)
        {
            float lh = (int)UiFontId.F16 + 2f;
            if (string.IsNullOrEmpty(body) || widthPx <= 0f) return lh;
            int lines = 0;
            var paras = body.Split('\n');
            for (int i = 0; i < paras.Length; i++)
            {
                int px = UiTextFit.WidthPx(paras[i], (int)UiFontId.F16);
                lines += Mathf.Max(1, Mathf.CeilToInt(px / widthPx));
            }
            return (lines + 1) * lh;
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
            var p = w != null && w.Players != null && w.Players.Length > 0 ? w.Me : null;
            var db = ctx.Content;
            switch (id)
            {
                case HintIds.FirstLock:
                    return names.Format(key, "sec", Num(p != null ? p.Derived.LockTicks / (float)SimConst.TicksPerSecond : 0f));
                case HintIds.FirstCrystal:
                case HintIds.FirstFizzle:
                case HintIds.StuckFizzle:
                    return names.Format(key, "n", p != null && p.Def != null ? p.Def.ComboPerCrystal : 0, "cap", p != null && p.Def != null ? p.Def.CrystalCap : 0);
                case HintIds.FirstArts:
                {
                    // 판 밖(메인 메뉴 연습의 트라이)이면 기준 캐릭터의 콤보·결정 수치.
                    var ch = p != null && p.Def != null ? p.Def : db != null ? db.BaselineCharacter : null;
                    return names.Format(key, "stance", KeyLabel(ctx, LogicalKey.Stance), "art", KeyLabel(ctx, LogicalKey.Skill),
                        "n", ch != null ? ch.ComboPerCrystal : 0, "cap", ch != null ? ch.CrystalCap : 0);
                }
                case HintIds.FirstFinal:
                    return names.Format(key, "n", AttackStyles.FinalMinCrystals(db), "stance", KeyLabel(ctx, LogicalKey.Stance), "art", KeyLabel(ctx, LogicalKey.Skill));
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

        static string KeyLabel(GameContext ctx, LogicalKey k)
        {
            var st = ctx != null && ctx.Settings != null ? ctx.Settings.Current : null;
            int bound = st != null ? KeyLayouts.PrimaryOf(st, k) : 0;
            if (bound <= 0) { var table = KeyLayouts.DefaultPrimary; bound = (int)k < table.Count ? table[(int)k] : 0; }
            return UiKeyLabels.Of(ctx != null ? ctx.Names : null, (UnityEngine.InputSystem.Key)bound);
        }

        /// <summary>소수 둘째 자리까지, 끝의 0은 지운다(1.25, 0.8, 3).</summary>
        public static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
