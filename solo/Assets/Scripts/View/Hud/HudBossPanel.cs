using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 보스 판(w.Boss != null일 때만, 위 가운데 넓게 — 웨이브 띠 아래). 위: 이름판(K-04 띠, 큰 글) + 복수 꼬리표(이름 왼쪽) +
    /// 격노 쌓임(이름 오른쪽) + 약점·저항 문양(왼쪽 끝). 아래: 체력 바 960 + 단계 눈금(첫 만남이면 눈금·자물쇠 없음, TV-047) +
    /// 깎인 만큼 늦게 따라 줄어드는 잔상. 최종 격노는 바의 테만 빨강(허용된 위험색). 08 요약서 6.1절, 05 요약서 8.3절.
    /// 바의 폭은 6.1절 그대로 960(HudLayout.BossBar.W). 판 자리는 HudOverlayLayout.
    /// </summary>
    public sealed class HudBossPanel
    {
        const int MaxEmbers = 5;
        const float FrameWidthPx = 2f;
        const float TrailDelaySeconds = 0.35f, TrailSpeedPerSecond = 0.6f, HitFlashSeconds = 0.12f;

        readonly HudArt _art;
        readonly RectTransform _root;
        readonly RectTransform _tag;
        readonly HudText _tagText;
        readonly RectTransform _nameStrip;
        readonly HudText _name;
        readonly HudText _rage;
        readonly UImage[] _embers = new UImage[MaxEmbers];
        readonly SpriteAnim _emberAnim;
        readonly HudText _weakLabel, _resistLabel;
        readonly UImage _weakIcon, _resistIcon;
        readonly RectTransform _bar;
        readonly HudGauge _gauge;
        readonly UImage _fill;
        readonly UImage _trail;
        readonly UImage _hitFlash;
        readonly RectTransform _ticksRoot;
        readonly List<UImage> _ticks = new List<UImage>();
        readonly UImage[] _rageFrame = new UImage[4];
        float _t;
        float _trailRatio = -1f, _lastRatio = -1f, _trailHold, _flashLeft;

        string _bossId;
        bool _firstEncounter;
        bool _revengeTag;
        bool _wasVisible;

        public bool Visible => _root != null && _root.gameObject.activeSelf;
        public int TickCount { get { int n = 0; for (int i = 0; i < _ticks.Count; i++) if (_ticks[i].gameObject.activeSelf) n++; return n; } }
        public bool RevengeTagVisible => _tag != null && _tag.gameObject.activeSelf;
        public bool FinalRageFrameVisible => _rageFrame[0] != null && _rageFrame[0].gameObject.activeSelf;
        public string NameText => _name.Current;
        /// <summary>게이지 그림의 틀 두께(채움 안쪽 위아래 여백, px). 그림이 없으면 0.</summary>
        float BarInset => _gauge != null ? _gauge.InsetY : 0f;
        public float FillWidth => _fill != null ? _fill.rectTransform.sizeDelta.x : 0f;

        public HudBossPanel(HudArt art, RectTransform parent)
        {
            _art = art;
            var ctx = art.Ctx;
            float W = HudOverlayLayout.BossW, H = HudOverlayLayout.BossH;
            _root = art.Plate(parent, "HudBoss", 0.5f, 0f, 0f, HudOverlayLayout.BossY, W, H, 0.84f);

            // 이름판(가운데)
            float nw = HudOverlayLayout.BossNameW, nh = HudOverlayLayout.BossNameH;
            float nx = (W - nw) * 0.5f;
            _nameStrip = UiKit.Node(_root, "NameStrip", nx, HudOverlayLayout.BossNameY, nw, nh);
            var strip = UiKit.Rect(_nameStrip, 0, 0, nw, nh, UiTheme.WoodLight, false, "Strip");
            var plate = UiCommonArt.NameplateSprite(art.Library);
            float namePad = 32f;
            if (plate != null)
            {
                // 공통 그림 boss_nameplate 512×64(원래 크기, 좌우 장식 끝 고정).
                UiCommonArt.ApplySliced(strip, plate);
                strip.color = Color.white;
                namePad = UiCommonArt.NameplateCapPx;
            }
            else
            {
                var sp = art.Kit("K-04", "heading_strip");
                if (sp.IsValid) { UiKit.SetSprite(strip, sp); strip.color = Color.white; }
                UiKit.Rect(_nameStrip, 12, nh - 3, nw - 24, 2, UiTheme.WithAlpha(UiTheme.Brass, 0.8f), false, "Rule");
                UiKit.Diamond(_nameStrip, 18f, nh * 0.5f, 12f, UiTheme.Brass);
                UiKit.Diamond(_nameStrip, nw - 18f, nh * 0.5f, 12f, UiTheme.Brass);
            }
            var name = UiKit.Label(ctx, _nameStrip, "", UiFontId.F32, namePad, 0, nw - 2f * namePad, nh, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true);
            UiKit.DropShadow(name, 2f);
            _name = new HudText(name);

            // 복수 꼬리표(이름판 왼쪽)
            _tag = UiKit.Node(_root, "RevengeTag", nx - 8 - 72, HudOverlayLayout.BossNameY + (nh - 28) * 0.5f, 72, 28);
            var tagBg = UiKit.Rect(_tag, 0, 0, 72, 28, UiTheme.WoodLight, false, "Bg");
            HudArt.SetKit(tagBg, art.Kit("K-46", "label"), UiTheme.WoodLight);
            tagBg.type = tagBg.sprite != null ? UImage.Type.Sliced : UImage.Type.Simple;
            _tagText = new HudText(UiKit.Label(ctx, _tag, "", UiFontId.F16, 0, 0, 72, 28, UiTheme.Paper, TextAnchor.MiddleCenter));

            // 격노(이름판 오른쪽): 글 + 불씨
            float rx = nx + nw + 12;
            _rage = new HudText(UiKit.Label(ctx, _root, "", UiFontId.F16, rx, HudOverlayLayout.BossNameY + 2, 140, 20, UiTheme.Amber, TextAnchor.MiddleLeft, false, true));
            _emberAnim = art.KitAnim("S-0803", "enrage_ember");
            for (int i = 0; i < MaxEmbers; i++)
                _embers[i] = HudArt.Image(_root, "Ember" + i, rx + i * 20, HudOverlayLayout.BossNameY + 24, 16, 16, null, UiTheme.Amber);

            // 약점·저항(왼쪽 끝)
            float ay = HudOverlayLayout.BossNameY + (nh - 24) * 0.5f;
            _weakLabel = new HudText(UiKit.Label(ctx, _root, "", UiFontId.F16, 24, ay, 56, 24, UiTheme.SlateBlue, TextAnchor.MiddleLeft, false, true));
            _weakIcon = HudArt.Image(_root, "WeakIcon", 80, ay + 4, 16, 16, null, UiTheme.SlateBlue);
            _resistLabel = new HudText(UiKit.Label(ctx, _root, "", UiFontId.F16, 108, ay, 56, 24, UiTheme.SlateBlue, TextAnchor.MiddleLeft, false, true));
            _resistIcon = HudArt.Image(_root, "ResistIcon", 164, ay + 4, 16, 16, null, UiTheme.SlateBlue);

            // 체력 바(960 × 20): 테, 바탕, 잔상, 채움, 맞은 빛, 눈금
            float bw = HudLayout.BossBar.W, bh = HudOverlayLayout.BossBarH;
            _gauge = new HudGauge(_root, "Bar", HudOverlayLayout.BossBarX, HudOverlayLayout.BossBarY, bw, bh, HudColors.BossFill,
                art.Kit("S-0803", "boss_health_rail"), art.Kit("S-0803", "boss_health_fill"), true, null, art.Library);
            _bar = _gauge.Root;
            bh = _gauge.H;
            _trail = _gauge.Trail;
            _fill = _gauge.Fill;
            _hitFlash = UiKit.Rect(_bar, _gauge.InsetX, BarInset, bw, bh - 2f * BarInset, UiTheme.WithAlpha(UiTheme.Chalk, 0f), false, "HitFlash");
            if (!_gauge.HasArt) UiKit.Rect(_bar, 0, 0, bw, 2, UiTheme.WithAlpha(UiTheme.Chalk, 0.25f), false, "Sheen");
            _ticksRoot = UiKit.Node(_bar, "Ticks", 0, 0, bw, bh);
            // 최종 격노 테(빨강 2px, 위·아래·왼·오른)
            _rageFrame[0] = UiKit.Rect(_bar, -FrameWidthPx - 3, -FrameWidthPx - 3, bw + 2 * (FrameWidthPx + 3), FrameWidthPx, HudColors.Danger, false, "RageTop");
            _rageFrame[1] = UiKit.Rect(_bar, -FrameWidthPx - 3, bh + 3, bw + 2 * (FrameWidthPx + 3), FrameWidthPx, HudColors.Danger, false, "RageBottom");
            _rageFrame[2] = UiKit.Rect(_bar, -FrameWidthPx - 3, -3, FrameWidthPx, bh + 6, HudColors.Danger, false, "RageLeft");
            _rageFrame[3] = UiKit.Rect(_bar, bw + 3, -3, FrameWidthPx, bh + 6, HudColors.Danger, false, "RageRight");
            for (int i = 0; i < 4; i++) HudArt.SetActive(_rageFrame[i], false);
            _root.gameObject.SetActive(false);
        }

        public void OnEvent(HudLayer layer, SimWorld w, in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.BossIntroBegin:
                    _revengeTag = e.A != 0;
                    layer.Play("ui.hud.boss_show");
                    if (_revengeTag) layer.Play("ui.hud.revenge_tag");
                    break;
                case SimEventType.BossRageStack:
                    if (layer.MotionOn) UiMotion.Pop(_rage.Label, 1.3f, 0.25f);
                    layer.Play("ui.hud.boss_rage_stack");
                    break;
                case SimEventType.BossFinalRage:
                    if (layer.MotionOn) UiMotion.Shake(_root, 6f, 0.35f);
                    layer.Play("ui.hud.boss_final_rage");
                    break;
                case SimEventType.BossPhaseChanged:
                    if (layer.MotionOn) UiMotion.Pop(_nameStrip, 1.08f, 0.3f);
                    layer.Play("ui.hud.boss_phase_tick");
                    break;
            }
        }

        /// <summary>Build 때(이벤트 없이) 복수 꼬리표를 상태에서 정한다.</summary>
        public void SyncFromState(SimWorld w)
        {
            _revengeTag = HudModel.RevengeTagFromState(w);
            _bossId = null;
        }

        public void Refresh(HudLayer layer, SimWorld w, in HudFrame f, float dt)
        {
            var b = w != null ? w.Boss : null;
            bool show = f.ShowBoss && b != null;
            HudArt.SetActive(_root, show);
            if (!show) { _bossId = null; _wasVisible = false; _trailRatio = -1f; return; }
            if (!_wasVisible && layer.MotionOn) UiMotion.Entrance(_root, 0f, 0.35f, new Vector2(0f, 24f), 0.9f).Replay();
            _wasVisible = true;
            _t += dt;
            var names = layer.Names;
            string id = b.Def != null ? b.Def.Id : null;
            if (id != _bossId || b.FirstEncounter != _firstEncounter)
            {
                _bossId = id;
                _firstEncounter = b.FirstEncounter;
                _name.Set(names != null && id != null ? names.Get(id) : id);
                _tagText.Set(names != null ? names.Get(HudModel.KeyRevengeTag) : string.Empty);
                BuildTicks(b);
                SetAffinity(names, b);
            }
            HudArt.SetActive(_tag, _revengeTag);

            // 격노
            _rage.Set(HudModel.BossRageText(names, b));
            int stacks = Mathf.Clamp(b.RageStacks, 0, MaxEmbers);
            Sprite ember = _emberAnim != null ? _emberAnim.At(_t) : null;
            for (int i = 0; i < MaxEmbers; i++)
            {
                bool on = i < stacks;
                HudArt.SetActive(_embers[i], on);
                if (on) HudArt.SetSprite(_embers[i], ember, UiTheme.Amber);
            }
            bool final = b.FinalRage;
            for (int i = 0; i < 4; i++) HudArt.SetActive(_rageFrame[i], final);

            // 체력 + 잔상
            var e = w.FindEnemy(b.EnemyId);
            float ratio = HudModel.BossHpRatio(e);
            float bh = _gauge.H;
            _gauge.SetRatio(ratio);
            if (_trailRatio < 0f || ratio > _trailRatio) { _trailRatio = ratio; _trailHold = 0f; }
            if (_lastRatio >= 0f && ratio < _lastRatio - 0.0001f)
            {
                _trailHold = TrailDelaySeconds;
                _flashLeft = layer.MotionOn ? HitFlashSeconds : 0f;
            }
            _lastRatio = ratio;
            if (_trailHold > 0f) _trailHold -= dt;
            else if (_trailRatio > ratio) _trailRatio = Mathf.Max(ratio, _trailRatio - TrailSpeedPerSecond * dt);
            _gauge.SetTrail(_trailRatio);
            HudArt.SetActive(_trail, _trailRatio > ratio + 0.0005f);
            if (_flashLeft > 0f) _flashLeft -= dt;
            _hitFlash.color = UiTheme.WithAlpha(UiTheme.Chalk, Mathf.Clamp01(_flashLeft / HitFlashSeconds) * 0.35f);
            _hitFlash.rectTransform.sizeDelta = new Vector2(Mathf.Round(_gauge.Span * ratio), bh - 2f * BarInset);
        }

        void BuildTicks(BossState b)
        {
            for (int i = 0; i < _ticks.Count; i++) HudArt.SetActive(_ticks[i], false);
            var pcts = HudModel.BossTickPercents(b.Def, b.FirstEncounter);
            float bw = _gauge.Span, bh = _gauge.H - 2f * BarInset;
            var tickSprite = _art.Kit("S-0803", "boss_health_tick");
            for (int i = 0; i < pcts.Count; i++)
            {
                if (i >= _ticks.Count) _ticks.Add(UiKit.Rect(_ticksRoot, 0, 0, HudLayout.BossTickW, bh, UiTheme.Ink, false, "Tick"));
                var t = _ticks[i];
                HudArt.SetKit(t, tickSprite, UiTheme.Ink);
                float x = _gauge.InsetX + HudModel.BossTickX(pcts[i], bw) - HudLayout.BossTickW * 0.5f;
                UiKit.Place(t.rectTransform, x, BarInset, HudLayout.BossTickW, bh);
                HudArt.SetActive(t, true);
            }
        }

        void SetAffinity(Wizard.Solo.Data.INameTable names, BossState b)
        {
            string weak = b.Def != null ? b.Def.Weak : null;
            string resist = b.Def != null ? b.Def.Resist : null;
            bool hasWeak = !string.IsNullOrEmpty(weak), hasResist = !string.IsNullOrEmpty(resist);
            _weakLabel.Show(hasWeak); HudArt.SetActive(_weakIcon, hasWeak);
            _resistLabel.Show(hasResist); HudArt.SetActive(_resistIcon, hasResist);
            if (hasWeak)
            {
                _weakLabel.Set(names != null ? names.Get(HudModel.KeyBossWeak) : string.Empty);
                HudArt.SetSprite(_weakIcon, _art.Icon(weak, 16), UiTheme.ElementColor(weak));
            }
            if (hasResist)
            {
                _resistLabel.Set(names != null ? names.Get(HudModel.KeyBossResist) : string.Empty);
                HudArt.SetSprite(_resistIcon, _art.Icon(resist, 16), UiTheme.ElementColor(resist));
            }
        }

        public void Dispose() => HudArt.Destroy(_root != null ? _root.gameObject : null);
    }
}
