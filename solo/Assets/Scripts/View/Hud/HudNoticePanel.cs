using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 한 줄 안내(08 요약서 6.1·13.1절). 우선순위: 과열 잠금("과열 잠금 {n}초" + 남은 시간 딱지 64×24)
    /// &gt; 사건 안내(웨이브 클리어, 길이는 솔로 임시) &gt; 출발 안내(야영에서 출발 표식 반경 안, str.camp.depart_hint — 고리 그림은 G14).
    /// 오버레이 배치: 과열 잠금은 상태 판 바로 아래 알약, 사건 안내는 화면 위쪽 가운데 큰 띠(튀어나오며 등장), 출발 안내는 아래 칸 판 위.
    /// 글 칸은 하나이고 종류에 따라 자리만 옮긴다.
    /// </summary>
    public sealed class HudNoticePanel
    {
        enum Mode { None, Lock, Banner, Hint }

        readonly HudTuning _tuning;
        readonly RectTransform _root;
        readonly RectTransform _pill, _banner, _hint;
        readonly RectTransform _badge;
        readonly UImage _badgeFill;
        /// <summary>작은 글(24px, 알약·출발 안내)과 큰 글(48px, 사건 띠). 보이는 글은 하나뿐이다.</summary>
        readonly HudText _small, _big;
        string _current;
        string _eventKey;
        float _eventLeft;
        HudStateNotice _lastState;
        Mode _mode = (Mode)(-1);

        public string Text => _current;
        public bool Visible => (_small.Label != null && _small.Label.gameObject.activeSelf) || (_big.Label != null && _big.Label.gameObject.activeSelf);
        public bool BadgeVisible => _badge != null && _badge.gameObject.activeSelf;

        public HudNoticePanel(HudArt art, RectTransform parent, HudTuning tuning)
        {
            _tuning = tuning;
            var ctx = art.Ctx;
            _root = UiKit.Node(parent, "HudNotice");
            UiKit.Stretch(_root);
            const float E = HudOverlayLayout.Edge;

            // 과열 잠금 알약(상태 판 아래)
            // 과열 잠금: 따로 판을 띄우지 않고 상태 무리 안, 과열 막대 바로 아래 작은 딱지(테 없음).
            _pill = HudArt.Bare(_root, "LockPill", 0f, 0f, E + HudOverlayLayout.HeatBarX - 10f, E + HudOverlayLayout.HeatY + HudOverlayLayout.HeatBarH + 4f, HudOverlayLayout.LockPillW, HudOverlayLayout.LockPillH, 0f);
            var b = HudLayout.NoticeBadge;
            _badge = UiKit.Node(_pill, "LockBadge", 10, (HudOverlayLayout.LockPillH - b.H) * 0.5f, b.W, b.H);
            var bg = UiKit.Rect(_badge, 0, 0, b.W, b.H, HudColors.RingAsh, false, "Bg");
            HudArt.SetKit(bg, art.Kit("S-0805", "heat_lock"), HudColors.RingAsh);
            _badgeFill = UiKit.Rect(_badge, 2, b.H - 4, b.W - 4, 2, UiTheme.Chalk, false, "Remain");

            // 사건 안내 큰 띠(위쪽 가운데)
            float bw = HudOverlayLayout.BannerNoticeW, bh = HudOverlayLayout.BannerNoticeH;
            _banner = HudArt.Anchored(_root, "EventBanner", 0.5f, HudOverlayLayout.BannerNoticeAnchorY, 0f, 0f, bw, bh);
            _banner.pivot = new Vector2(0.5f, 0.5f);
            UiBackdrop.Glow(_banner, -bw * 0.25f, -bh * 0.6f, bw * 1.5f, bh * 2.2f, UiTheme.WithAlpha(UiTheme.Ink, 0.75f));
            var strip = UiKit.Rect(_banner, 0, 8, bw, bh - 16, UiTheme.PlateFill, false, "Strip");
            var sp = art.Kit("K-04", "heading_strip");
            if (sp.IsValid) { UiKit.SetSprite(strip, sp); strip.color = UiTheme.WithAlpha(Color.white, 0.92f); }
            UiKit.AccentRule(_banner, 40, 12, bw - 80);
            UiKit.AccentRule(_banner, 40, bh - 15, bw - 80);
            UiKit.Diamond(_banner, 28f, bh * 0.5f, 16f, UiTheme.Brass);
            UiKit.Diamond(_banner, bw - 28f, bh * 0.5f, 16f, UiTheme.Brass);

            // 출발 안내(아래 칸 판 위)
            _hint = HudArt.Bare(_root, "Hint", 0.5f, 1f, 0f, -(E + HudOverlayLayout.TrayH + 12f), HudOverlayLayout.HintW, HudOverlayLayout.HintH, 0.55f);

            var small = UiKit.Label(ctx, _pill, "", UiFontId.F24, 0, 0, 10, 10, UiTheme.Chalk, TextAnchor.MiddleLeft, false, true);
            UiKit.DropShadow(small, 2f);
            _small = new HudText(small);
            _small.Show(false);
            var big = UiKit.Label(ctx, _banner, "", UiFontId.F48, 48, 16, bw - 96, bh - 32, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            UiKit.DropShadow(big, 3f);
            _big = new HudText(big);
            _big.Show(false);
            SetMode(Mode.None);
        }

        void SetMode(Mode m)
        {
            if (_mode == m) return;
            _mode = m;
            HudArt.SetActive(_pill, m == Mode.Lock);
            HudArt.SetActive(_banner, m == Mode.Banner);
            HudArt.SetActive(_hint, m == Mode.Hint);
            var l = _small.Label;
            if (l == null) return;
            if (m == Mode.Lock)
            {
                l.transform.SetParent(_pill, false);
                float x = HudLayout.NoticeBadge.W + 20f;
                UiKit.Place(l.Rect, x, 4, HudOverlayLayout.LockPillW - x - 12, HudOverlayLayout.LockPillH - 8);
                l.Text.alignment = TextAnchor.MiddleLeft;
            }
            else if (m == Mode.Hint)
            {
                l.transform.SetParent(_hint, false);
                UiKit.Place(l.Rect, 16, 4, HudOverlayLayout.HintW - 32, HudOverlayLayout.HintH - 8);
                l.Text.alignment = TextAnchor.MiddleCenter;
            }
        }

        public void OnEvent(HudLayer layer, SimWorld w, in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.WaveCleared:
                    _eventKey = HudModel.KeyNoticeWaveClear;
                    _eventLeft = _tuning.NoticeSeconds;
                    _mode = (Mode)(-1); // 다시 띄울 때 등장 연출
                    layer.Play("ui.hud.notice");
                    break;
                case SimEventType.HeatLockEntered:
                    if (e.Slot == 0)
                    {
                        if (layer.MotionOn) UiMotion.Shake(_pill, 5f, 0.3f);
                        layer.Play("ui.hud.overheat_lock");
                    }
                    break;
            }
        }

        public void Refresh(HudLayer layer, SimWorld w, in HudFrame f, float dt)
        {
            var names = layer.Names;
            if (_eventLeft > 0f) { _eventLeft -= dt; if (_eventLeft <= 0f) _eventKey = null; }

            string s = null;
            Mode m = Mode.None;
            if (f.StateNotice == HudStateNotice.HeatLock)
            {
                s = names != null ? names.Format(HudModel.KeyNoticeHeatLock, "n", f.HeatLockSecondsLeft) : null;
                m = Mode.Lock;
            }
            else if (_eventKey != null) { s = names != null ? names.Get(_eventKey) : _eventKey; m = Mode.Banner; }
            else if (f.StateNotice == HudStateNotice.DepartHint) { s = names != null ? names.Get(HudModel.KeyDepartHint) : null; m = Mode.Hint; }
            if (f.StateNotice == HudStateNotice.DepartHint && _lastState != HudStateNotice.DepartHint) layer.Play("ui.hud.notice");
            _lastState = f.StateNotice;

            bool show = !string.IsNullOrEmpty(s);
            if (!show) m = Mode.None;
            bool entering = _mode != m;
            SetMode(m);
            if (entering && show && layer.MotionOn)
            {
                if (m == Mode.Banner) UiMotion.Entrance(_banner, 0f, 0.32f, new Vector2(0f, 12f), 1.35f).Replay();
                else UiMotion.Pop(m == Mode.Lock ? _pill : _hint, 1.08f, 0.22f);
            }
            // 사건 띠는 끝나기 직전에 흐려진다.
            if (m == Mode.Banner)
            {
                var cg = _banner.GetComponent<CanvasGroup>();
                if (cg != null && _eventLeft < 0.3f && _eventLeft > 0f) cg.alpha = Mathf.Clamp01(_eventLeft / 0.3f);
            }

            HudArt.SetActive(_badge, m == Mode.Lock);
            if (m == Mode.Lock)
            {
                var b = HudLayout.NoticeBadge;
                _badgeFill.rectTransform.sizeDelta = new Vector2((b.W - 4) * Mathf.Clamp01(f.HeatLockRemainRatio), 2);
            }
            _current = show ? s : _current;
            bool big = show && m == Mode.Banner;
            _big.Show(big);
            _small.Show(show && !big);
            if (show) { if (big) _big.Set(s); else _small.Set(s); }
        }

        public void Dispose() => HudArt.Destroy(_root != null ? _root.gameObject : null);
    }
}
