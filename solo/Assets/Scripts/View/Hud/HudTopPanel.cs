using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 위 가운데 웨이브 띠(전장 위 오버레이 판): 날과 웨이브(시간대 아이콘, 변덕 아이콘 최대 2) + 웨이브 진행 바(오른쪽 → 왼쪽으로 줄어듦,
    /// 보스 웨이브에 숨김) + 경과 시간·허상까지 n웨이브 줄. 훈수 한 줄은 띠 아래. 규칙은 08 요약서 6.1절 그대로,
    /// 자리는 HudOverlayLayout. 보스 웨이브에는 띠가 한 줄(16px)로 줄고 보스 판이 그 아래에 뜬다(TV-046의 "16px 한 줄").
    /// </summary>
    public sealed class HudTopPanel
    {
        /// <summary>경과 시간 앞 시계 아이콘 크기(24px 글에 맞춤).</summary>
        const float ClockPx = 22f;
        readonly HudArt _art;
        readonly HudTuning _tuning;
        readonly RectTransform _root;
        readonly RectTransform _banner;
        readonly UImage _bannerBody, _bannerShadow;

        readonly RectTransform _progress;
        readonly UImage _progressFill;
        readonly UImage _progressTip;
        readonly HudGauge _progressGauge;
        readonly UImage _clock;
        int _lastPermille = -1;

        readonly RectTransform _dayWave;
        readonly UImage _todIcon;
        readonly UImage[] _whimIcons = new UImage[HudLayout.MaxWhimIcons];
        readonly HudText _dayWaveText;
        readonly HudText _dayWaveTextBoss;
        bool? _bossLayout;
        string _todId, _whim0, _whim1;

        readonly HudText _elapsed;
        readonly HudText _revenge;
        readonly RectTransform _adviceRoot;
        readonly HudText _advice;
        float _adviceLeft;
        int _lastRevengeLeft;

        public bool ProgressVisible => _progress != null && _progress.gameObject.activeSelf;
        public bool DayWaveVisible => _dayWave != null && _dayWave.gameObject.activeSelf;
        public bool DayWaveBossLayout => _bossLayout == true;
        public bool RevengeVisible => _revenge.Label != null && _revenge.Label.gameObject.activeSelf;
        public string RevengeText => _revenge.Current;
        public string DayWaveText => _bossLayout == true ? _dayWaveTextBoss.Current : _dayWaveText.Current;
        public bool ElapsedVisible => _elapsed.Label != null && _elapsed.Label.gameObject.activeSelf;
        public string AdviceText => _advice.Current;
        public bool AdviceVisible => _adviceRoot != null && _adviceRoot.gameObject.activeSelf;

        /// <summary>진행 바 채움의 폭(px). 왼쪽에 붙어 오른쪽 끝이 왼쪽으로 줄어든다.</summary>
        public float ProgressFillWidth => _progressFill != null ? _progressFill.rectTransform.sizeDelta.x : 0f;

        public HudTopPanel(HudArt art, RectTransform parent, HudTuning tuning)
        {
            _art = art;
            _tuning = tuning;
            var ctx = art.Ctx;
            _root = UiKit.Node(parent, "HudTop");
            UiKit.Stretch(_root);

            float W = HudOverlayLayout.BannerW, H = HudOverlayLayout.BannerH;
            _banner = HudArt.Anchored(_root, "WaveBanner", 0.5f, 0f, 0f, HudOverlayLayout.BannerY, W, H);
            _bannerShadow = UiKit.Rect(_banner, HudOverlayLayout.ShadowPx, HudOverlayLayout.ShadowPx, W, H, UiTheme.Shadow, false, "Shadow");
            _bannerBody = UiKit.Rect(_banner, 0, 0, W, H, UiTheme.PlateFill, false, "Body");
            var strip = art.Kit("K-04", "heading_strip");
            if (UiCommonArt.HasPanel(art.Library))
            {
                UiCommonArt.ApplyPanel(art.Library, _bannerBody, 0.94f);
                UiCommonArt.ApplySliced(_bannerShadow, UiCommonArt.PanelSprite(art.Library)); _bannerShadow.color = UiTheme.Shadow;
            }
            else if (strip.IsValid)
            {
                UiKit.SetSprite(_bannerBody, strip); _bannerBody.color = UiTheme.WithAlpha(Color.white, 0.9f);
                UiKit.SetSprite(_bannerShadow, strip); _bannerShadow.color = UiTheme.Shadow;
            }
            StretchFull(_bannerShadow.rectTransform, HudOverlayLayout.ShadowPx);
            StretchFull(_bannerBody.rectTransform, 0f);
            // 테 있는 판 대신 가벼운 먹색 번짐만: 날·웨이브 글 + 가는 진행 줄이 전장 위에 떠 있게 한다.
            _bannerShadow.enabled = false;
            _bannerBody.sprite = UiBackdrop.RadialSoft;
            _bannerBody.type = UImage.Type.Simple;
            _bannerBody.color = UiTheme.WithAlpha(UiTheme.Ink, 0.5f);
            _bannerBody.raycastTarget = false;

            // 날과 웨이브
            _dayWave = UiKit.Node(_banner, "DayWave", 40, HudOverlayLayout.BannerTextY, W - 80, HudOverlayLayout.BannerTextH);
            _todIcon = UiKit.Rect(_dayWave, 0, 0, HudLayout.TodIconPx, HudLayout.TodIconPx, UiTheme.SlateBlue, false, "Tod");
            for (int i = 0; i < _whimIcons.Length; i++)
                _whimIcons[i] = UiKit.Rect(_dayWave, 0, 0, HudLayout.WhimIconPx, HudLayout.WhimIconPx, UiTheme.SlateBlue, false, "Whim" + i);
            var dw = UiKit.Label(ctx, _dayWave, "", UiFontId.F32, 0, 0, W - 80, HudOverlayLayout.BannerTextH, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            UiKit.DropShadow(dw, 2f);
            _dayWaveText = new HudText(dw);
            _dayWaveTextBoss = new HudText(UiKit.Label(ctx, _dayWave, "", UiFontId.F16, 0, 0, W - 80, HudLayout.DayWaveBoss.H, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true));

            // 진행 바(테 + 바탕 + 채움 + 끝 빛)
            float bw = HudOverlayLayout.BannerBarW, bh = HudOverlayLayout.BannerBarH;
            _progressGauge = new HudGauge(_banner, "WaveProgress", HudOverlayLayout.BannerBarX, HudOverlayLayout.BannerBarY, bw, bh, HudColors.WaveFill,
                art.Kit("S-0801", "wave_rail"), art.Kit("S-0801", "wave_fill"), lib: art.Library);
            _progress = _progressGauge.Root;
            _progressFill = _progressGauge.Fill;
            _progressTip = UiKit.Rect(_progress, 0, 0, 3, _progressGauge.H, UiTheme.Chalk, false, "Tip");

            // 아랫줄: 경과 시간(가운데), 허상까지 n웨이브(오른쪽)
            var clockSp = UiCommonArt.Get(art.Library, UiCommonArt.IconClock);
            _clock = HudArt.Image(_banner, "Clock", 0, 0, ClockPx, ClockPx, clockSp, UiTheme.SlateBlue);
            HudArt.SetActive(_clock, false);
            _elapsed = new HudText(UiKit.Label(ctx, _banner, "", UiFontId.F24, (W - 160) * 0.5f, HudOverlayLayout.BannerSubY, 160, HudOverlayLayout.BannerSubH,
                UiTheme.Chalk, TextAnchor.MiddleCenter, false, true));
            UiKit.DropShadow(_elapsed.Label, 2f);
            _revenge = new HudText(UiKit.Label(ctx, _banner, "", UiFontId.F16, W - 40 - 200, HudOverlayLayout.BannerSubY, 200, HudOverlayLayout.BannerSubH,
                UiTheme.Paper, TextAnchor.MiddleRight, false, true));

            // 훈수 한 줄(띠 아래 알약 판). 폭은 보충 수치(master_advice_rect)의 폭을 쓴다.
            float aw = tuning.MasterAdvice.W > 0f ? tuning.MasterAdvice.W : HudOverlayLayout.AdviceW;
            _adviceRoot = HudArt.Anchored(_root, "Advice", 0.5f, 0f, 0f, HudOverlayLayout.BannerY + H + HudOverlayLayout.AdviceGap, aw, HudOverlayLayout.AdviceH);
            var abg = UiKit.Rect(_adviceRoot, 0, 0, aw, HudOverlayLayout.AdviceH, UiTheme.PlateFill, false, "Bg");
            var tip = art.Kit("K-08", "tooltip");
            if (tip.IsValid) { UiKit.SetSprite(abg, tip); abg.color = UiTheme.WithAlpha(Color.white, 0.9f); }
            _advice = new HudText(UiKit.Label(ctx, _adviceRoot, "", UiFontId.F16, 16, 4, aw - 32, HudOverlayLayout.AdviceH - 8, UiTheme.Paper, TextAnchor.MiddleCenter, false, true));
            HudArt.SetActive(_adviceRoot, false);
        }

        static void StretchFull(RectTransform rt, float offset)
        {
            UiKit.Stretch(rt);
            rt.offsetMin += new Vector2(offset, -offset);
            rt.offsetMax += new Vector2(offset, -offset);
        }

        static RectTransform Ornament(RectTransform banner, bool left)
        {
            var node = UiKit.Node(banner, left ? "OrnL" : "OrnR");
            UiKit.PlaceAnchored(node, left ? 0f : 1f, 0.5f, left ? 10f : -10f, 0f, 24f, 24f);
            UiKit.Diamond(node, 12f, 12f, 14f, UiTheme.Brass);
            return node;
        }

        public void OnEvent(HudLayer layer, SimWorld w, in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.MasterAdvice:
                    if (layer.Names != null)
                    {
                        _advice.Set(layer.Names.Get(HudModel.MasterAdviceKey(e.A)));
                        _adviceLeft = _tuning.MasterAdviceSeconds;
                        HudArt.SetActive(_adviceRoot, true);
                        if (layer.MotionOn) UiMotion.Pop(_adviceRoot, 1.06f, 0.22f);
                    }
                    break;
                case SimEventType.WaveStarted:
                    if (layer.MotionOn) UiMotion.Pop(_banner, 1.08f, 0.3f);
                    layer.Play("ui.hud.day_wave_change");
                    break;
            }
        }

        public void Refresh(HudLayer layer, SimWorld w, in HudFrame f, float dt)
        {
            var names = layer.Names;
            bool anyBanner = f.ShowDayWave || f.ShowWaveProgress || f.ShowElapsed;
            HudArt.SetActive(_banner, anyBanner);

            // 띠 높이: 보스 웨이브면 한 줄.
            bool bossLayout = f.DayWaveBossLayout;
            float H = bossLayout ? HudOverlayLayout.BannerBossH : HudOverlayLayout.BannerH;
            if (Mathf.Abs(_banner.sizeDelta.y - H) > 0.1f)
                _banner.sizeDelta = new Vector2(HudOverlayLayout.BannerW, H);

            // 진행 바
            HudArt.SetActive(_progress, f.ShowWaveProgress);
            if (f.ShowWaveProgress)
            {
                float width = _progressGauge.SetRatio(f.WaveProgressPermille / 1000f);
                float ins = _progressGauge.HasArt ? _progressGauge.InsetY - 2f : -3f;
                HudArt.SetActive(_progressTip, width > 0.5f);
                if (width > 0.5f) UiKit.Place(_progressTip.rectTransform, Mathf.Max(0f, width - 2f), ins, 3f, _progressGauge.H - 2f * ins);
                if (_lastPermille >= 0 && f.WaveProgressPermille / 100 < _lastPermille / 100) layer.Play("ui.hud.wave_progress");
                _lastPermille = f.WaveProgressPermille;
            }

            // 날과 웨이브
            HudArt.SetActive(_dayWave, f.ShowDayWave);
            if (f.ShowDayWave)
            {
                if (_bossLayout != bossLayout)
                {
                    _bossLayout = bossLayout;
                    float th = bossLayout ? HudLayout.DayWaveBoss.H : HudOverlayLayout.BannerTextH;
                    UiKit.Place(_dayWave, 40, bossLayout ? (HudOverlayLayout.BannerBossH - th) * 0.5f : HudOverlayLayout.BannerTextY, HudOverlayLayout.BannerW - 80, th);
                    _dayWaveText.Show(!bossLayout);
                    _dayWaveTextBoss.Show(bossLayout);
                    _todId = null; _whim0 = null; _whim1 = null;
                }
                var txt = bossLayout ? _dayWaveTextBoss : _dayWaveText;
                txt.Set(HudModel.DayWaveText(names, f.Wave));
                LayoutIcons(f);
            }

            // 경과 시간: 보통은 진행 바 아래 가운데, 보스 웨이브에는 한 줄 띠의 오른쪽 끝.
            _elapsed.Show(f.ShowElapsed);
            if (f.ShowElapsed)
            {
                float W = HudOverlayLayout.BannerW;
                float ex = bossLayout ? W - 40 - 120 : (W - 160) * 0.5f;
                float ey = bossLayout ? (HudOverlayLayout.BannerBossH - HudOverlayLayout.BannerSubH) * 0.5f : HudOverlayLayout.BannerSubY;
                var r = _elapsed.Label.Rect;
                if (Mathf.Abs(r.anchoredPosition.x - ex) > 0.1f || Mathf.Abs(-r.anchoredPosition.y - ey) > 0.1f)
                {
                    UiKit.Place(r, ex, ey, bossLayout ? 120 : 160, HudOverlayLayout.BannerSubH);
                    _elapsed.Label.Text.alignment = bossLayout ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;
                }
                _elapsed.Set(HudModel.ElapsedText(f.ElapsedTicks));
                // 시계 아이콘: 글 바로 왼쪽(글 폭 = 숫자 1자 6px 기준, 08 1.4절 폭 규칙).
                bool hasClock = _clock.sprite != null;
                HudArt.SetActive(_clock, hasClock);
                if (hasClock)
                {
                    float tw = UiTextFit.WidthPx(_elapsed.Current, (int)UiFontId.F24);
                    float cx = bossLayout ? ex + 120f - tw - ClockPx - 6f : W * 0.5f - tw * 0.5f - ClockPx - 6f;
                    UiKit.Place(_clock.rectTransform, cx, ey + (HudOverlayLayout.BannerSubH - ClockPx) * 0.5f, ClockPx, ClockPx);
                }
            }

            // 허상까지 n웨이브(0이면 줄 없음)
            bool rev = f.RevengeWavesLeft > 0;
            _revenge.Show(rev);
            if (rev) _revenge.Set(HudModel.RevengeText(names, f.RevengeWavesLeft));
            if (rev && f.RevengeWavesLeft != _lastRevengeLeft) layer.Play("ui.hud.revenge_waves_left");
            _lastRevengeLeft = f.RevengeWavesLeft;

            // 훈수
            if (_adviceLeft > 0f)
            {
                _adviceLeft -= dt;
                if (_adviceLeft <= 0f) HudArt.SetActive(_adviceRoot, false);
            }
            if (_adviceRoot.gameObject.activeSelf)
            {
                float ay = HudOverlayLayout.BannerY + H + HudOverlayLayout.AdviceGap + (f.ShowBoss ? HudOverlayLayout.BossH + 4f : 0f);
                if (Mathf.Abs(-_adviceRoot.anchoredPosition.y - ay) > 0.1f)
                    _adviceRoot.anchoredPosition = new Vector2(_adviceRoot.anchoredPosition.x, -ay);
            }
        }

        void LayoutIcons(in HudFrame f)
        {
            if (_todId == f.TodId && _whim0 == f.Whim0 && _whim1 == f.Whim1) return;
            _todId = f.TodId; _whim0 = f.Whim0; _whim1 = f.Whim1;
            var rr = _dayWave.sizeDelta;
            int iconPx = Mathf.Min(HudLayout.TodIconPx, (int)rr.y);
            float iy = (rr.y - iconPx) * 0.5f;
            UiKit.Place(_todIcon.rectTransform, 0, iy, iconPx, iconPx);
            HudArt.SetSprite(_todIcon, _art.Icon(f.TodId, iconPx), UiTheme.SlateBlue);
            HudArt.SetActive(_todIcon, !string.IsNullOrEmpty(f.TodId));
            string[] whims = { f.Whim0, f.Whim1 };
            float x = rr.x;
            for (int i = _whimIcons.Length - 1; i >= 0; i--)
            {
                bool on = !string.IsNullOrEmpty(whims[i]);
                HudArt.SetActive(_whimIcons[i], on);
                if (!on) continue;
                x -= iconPx;
                UiKit.Place(_whimIcons[i].rectTransform, x, iy, iconPx, iconPx);
                HudArt.SetSprite(_whimIcons[i], _art.Icon(whims[i], iconPx), UiTheme.SlateBlue);
                x -= 4f;
            }
        }

        public void Dispose() => HudArt.Destroy(_root != null ? _root.gameObject : null);
    }
}
