using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 왼쪽 위 상태 판(초상 + 지팡이 단계, 체력 칸 줄, 과열 게이지)과 오른쪽 위 기록 판(원정 지갑·점수·처치: 아이콘 + 작은 머리 글 + 큰 숫자).
    /// 자리는 HudOverlayLayout(전장 위 오버레이), 체력 칸 크기·간격은 08 요약서 6.1절(HudLayout) 그대로.
    /// 체력 1칸 남으면 그 칸 귀퉁이에 위험색(허용 세 곳 중 하나). 피격 때 초상이 0.2초 밝아지고 체력 줄이 흔들린다(피격 표시는 줄이지 않는다 —
    /// 흔들림·튀기 같은 꾸밈만 연출 최소에서 끈다). 과열 게이지 색은 열 링과 같은 규칙(금색·빨강 없음, 10 요약서 3.4절).
    /// </summary>
    public sealed class HudStatusPanel
    {
        const int MaxHpCells = 8;
        const float CornerPx = 10f;
        const float HpHitShakePx = 6f, HpHitShakeSeconds = 0.3f;
        const float LastCellBeatScale = 0.12f, LastCellBeatPeriod = 0.9f;

        readonly HudArt _art;
        readonly RectTransform _root;
        readonly RectTransform _plate;
        readonly RectTransform _hpRow;
        readonly UImage[] _hp = new UImage[MaxHpCells];
        readonly UImage[] _hpBreak = new UImage[MaxHpCells];
        readonly float[] _hpBreakT = new float[MaxHpCells];
        readonly UiTriangle _corner;
        readonly SpriteAnim _breakAnim;
        readonly Sprite _hpFull, _hpEmpty;
        int _lastHpCells = -1;
        float _beatT;

        readonly RectTransform _portraitNode;
        readonly UImage _portraitFrame;
        readonly UImage _portrait;
        readonly UImage _portraitFlash;
        float _portraitHitLeft;
        string _charId;
        readonly HudText _stage;

        // 과열 게이지
        readonly HudText _heatCaption;
        readonly HudGauge _heat;
        readonly RectTransform _heatBar;
        readonly UImage _heatFill;
        readonly UImage _heatCrit;
        readonly UImage _heatGlow;

        // 오른쪽 위 기록 판
        readonly RectTransform _wallet;
        readonly HudText[] _walletRows = new HudText[3];
        readonly HudText[] _walletCaps = new HudText[3];
        readonly UiLabel[] _walletNums = new UiLabel[3];
        readonly long[] _walletLast = { long.MinValue, long.MinValue, long.MinValue };

        // 점수 옆 "+n"(이번 웨이브 진행 중 몫). 웨이브 클리어로 본 점수가 오르면 숫자 쪽으로 빨려 들어가며 사라진다.
        const float PendingGapPx = 8f, PendingW = 120f, PendingH = 24f, PendingAlpha = 0.6f;
        const float PendingMergeSeconds = 0.38f;
        readonly RectTransform _pendingNode;
        readonly UiLabel _pending;
        int _pendingShown;
        float _pendingMergeT = -1f;
        Vector2 _pendingFrom, _pendingTo;

        /// <summary>콤보 자리 "HUD"(초상 옆)의 부모.</summary>
        public readonly RectTransform ComboHudAnchor;

        public int HpCellsShown { get { int n = 0; for (int i = 0; i < _hp.Length; i++) if (_hp[i].gameObject.activeSelf) n++; return n; } }
        public float HpRowWidthShown { get { int n = HpCellsShown; return HudLayout.HpRowWidth(n); } }
        public bool LastCellCornerVisible => _corner != null && _corner.gameObject.activeSelf;
        public bool WalletVisible => _wallet != null && _wallet.gameObject.activeSelf;
        /// <summary>점수 옆 "+n"이 보이는가(합쳐지는 연출 중 포함)와 그 글.</summary>
        public bool ScorePendingVisible => _pendingNode != null && _pendingNode.gameObject.activeSelf;
        public string ScorePendingText => _pending != null ? _pending.FullText : null;
        public bool ScorePendingMerging => _pendingMergeT >= 0f;
        public bool PortraitHitActive => _portraitHitLeft > 0f;
        public string WalletRowText(int i) => _walletRows[i].Current;
        /// <summary>과열 게이지 채움 폭(px).</summary>
        public float HeatFillWidth => _heatFill != null && _heatFill.gameObject.activeSelf ? _heatFill.rectTransform.sizeDelta.x : 0f;

        public HudStatusPanel(HudArt art, RectTransform parent, HudTuning tuning)
        {
            _art = art;
            var ctx = art.Ctx;
            _root = UiKit.Node(parent, "HudStatus");
            UiKit.Stretch(_root);

            // ---- 왼쪽 위 판
            const float E = HudOverlayLayout.Edge;
            _plate = art.Plate(_root, "StatusPlate", 0f, 0f, E, E, HudOverlayLayout.StatusW, HudOverlayLayout.StatusH);

            // 초상(테 72 안에 64) + 피격 빛 + 단계 꼬리표
            float fx = HudOverlayLayout.PortraitFrameX, fy = HudOverlayLayout.PortraitFrameY, fp = HudOverlayLayout.PortraitFramePx;
            _portraitNode = UiKit.Node(_plate, "PortraitNode", fx, fy, fp, fp);
            _portraitNode.pivot = new Vector2(0.5f, 0.5f);
            _portraitNode.anchoredPosition = new Vector2(fx + fp * 0.5f, -(fy + fp * 0.5f));
            UiKit.Rect(_portraitNode, 0, 0, fp, fp, UiTheme.WoodDark, false, "PortraitBack");
            float pin = (fp - HudOverlayLayout.PortraitPx) * 0.5f;
            _portrait = HudArt.Image(_portraitNode, "Portrait", pin, pin, HudOverlayLayout.PortraitPx, HudOverlayLayout.PortraitPx, null, UiTheme.WoodLight);
            _portraitFlash = UiKit.Rect(_portraitNode, pin, pin, HudOverlayLayout.PortraitPx, HudOverlayLayout.PortraitPx, UiTheme.WithAlpha(UiTheme.Chalk, 0.35f), false, "PortraitFlash");
            HudArt.SetActive(_portraitFlash, false);
            _portraitFrame = UiKit.Rect(_portraitNode, 0, 0, fp, fp, new Color(0, 0, 0, 0), false, "PortraitFrame");
            HudArt.SetKit(_portraitFrame, art.Kit("S-0802", "portrait"), new Color(0, 0, 0, 0));
            if (!_portraitFrame.sprite) UiFrame.Create(_portraitNode, UiTheme.Brass, 2);

            var stageBg = UiKit.Rect(_plate, HudOverlayLayout.StageX, HudOverlayLayout.StageY, HudOverlayLayout.StageW, HudOverlayLayout.StageH, UiTheme.WoodLight, false, "StageBg");
            var tag = art.Kit("K-46", "label");
            if (tag.IsValid) { UiKit.SetSprite(stageBg, tag); stageBg.color = Color.white; }
            _stage = new HudText(UiKit.Label(ctx, _plate, "", UiFontId.F16, HudOverlayLayout.StageX, HudOverlayLayout.StageY + 2f,
                HudOverlayLayout.StageW, HudOverlayLayout.StageH - 4f, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true));

            // 체력 칸 줄(칸 32, 간격 4 = 6.1절)
            _hpFull = art.Status("health_full");
            _hpEmpty = art.Status("health_empty");
            _breakAnim = art.StatusAnim("health_break");
            _hpRow = UiKit.Node(_plate, "HpRow", HudOverlayLayout.HpX, HudOverlayLayout.HpY, HudLayout.HpRowWidth(MaxHpCells), HudLayout.HpCellPx);
            for (int i = 0; i < MaxHpCells; i++)
            {
                float x = HudLayout.HpCellX(i) - HudLayout.HpX;
                _hp[i] = HudArt.Image(_hpRow, "Hp" + i, x, 0, HudLayout.HpCellPx, HudLayout.HpCellPx, _hpFull, UiTheme.Chalk);
                _hp[i].rectTransform.pivot = new Vector2(0.5f, 0.5f);
                _hp[i].rectTransform.anchoredPosition = new Vector2(x + HudLayout.HpCellPx * 0.5f, -HudLayout.HpCellPx * 0.5f);
                _hpBreak[i] = HudArt.Image(_hpRow, "HpBreak" + i, x, 0, HudLayout.HpCellPx, HudLayout.HpCellPx, null, UiTheme.Chalk);
                HudArt.SetActive(_hpBreak[i], false);
            }
            _corner = UiKit.Triangle(_hpRow, UiTriangleDir.Down, 0, 0, CornerPx, CornerPx, HudColors.Danger);
            _corner.name = "HpLastCellCorner";
            HudArt.SetActive(_corner, false);

            // 과열 게이지(머리 글 "열" + 막대 + 임계 시작 눈금)
            _heatCaption = new HudText(UiKit.Label(ctx, _plate, ctx != null && ctx.Names != null ? ctx.Names.Get(HudKeys.HeatCaption) : string.Empty,
                UiFontId.F16, HudOverlayLayout.HeatCaptionX, HudOverlayLayout.HeatY - 5f, HudOverlayLayout.HeatCaptionW, 24f, UiTheme.SlateBlue, TextAnchor.MiddleLeft, false, true));
            _heat = new HudGauge(_plate, "HeatBar", HudOverlayLayout.HeatBarX, HudOverlayLayout.HeatY, HudOverlayLayout.HeatBarW, HudOverlayLayout.HeatBarH, HudColors.RingSlate, lib: art.Library);
            _heatBar = _heat.Root;
            _heatFill = _heat.Fill;
            _heatGlow = UiKit.Rect(_heatBar, -2, -2, _heat.W + 4, _heat.H + 4, UiTheme.WithAlpha(UiTheme.Chalk, 0f), false, "Glow");
            _heatGlow.rectTransform.SetAsFirstSibling();
            _heatCrit = UiKit.Rect(_heatBar, 0, 0, 2, _heat.H, UiTheme.Chalk, false, "CritTick");

            ComboHudAnchor = UiKit.Node(_plate, "ComboHudAnchor", HudOverlayLayout.ComboX, HudOverlayLayout.ComboY, 0, 0);

            // ---- 오른쪽 위 기록 판
            _wallet = art.Plate(_root, "Wallet", 1f, 0f, -E, E, HudOverlayLayout.StatsW, HudOverlayLayout.StatsH);
            string[] capKeys = { HudModel.KeyWallet, HudModel.KeyScore, HudModel.KeyKills };
            string[] icons = { null, "score", "kills" };
            string[] commonIcons = { UiCommonArt.IconGold, UiCommonArt.IconScore, UiCommonArt.IconKill };
            float ip = HudOverlayLayout.StatsIconPx;
            for (int i = 0; i < 3; i++)
            {
                float y = HudOverlayLayout.StatsRowY + i * HudOverlayLayout.StatsRowStep;
                float h = HudOverlayLayout.StatsRowH;
                if (i > 0) UiKit.Rect(_wallet, 16, y - 2, HudOverlayLayout.StatsW - 32, 1, UiTheme.WithAlpha(UiTheme.SlateBlue, 0.18f), false, "Sep" + i);
                // 공통 HUD 아이콘(금화·별·칼)이 있으면 그것을, 없으면 원정 지갑 아이콘·S-0804.
                Sprite icon = UiCommonArt.Get(art.Library, commonIcons[i]);
                if (icon == null) icon = i == 0 ? art.Icon(Ids.Item.Purse, (int)ip) : art.Kit("S-0804", icons[i]).Sprite;
                var img = HudArt.Image(_wallet, "Icon" + i, 16, y + (h - ip) * 0.5f, ip, ip, icon, UiTheme.SlateBlue);
                if (icon == null) HudArt.SetActive(img, false);
                _walletCaps[i] = new HudText(UiKit.Label(ctx, _wallet, ctx != null && ctx.Names != null ? ctx.Names.Get(capKeys[i]) : string.Empty,
                    UiFontId.F16, 48, y + (h - 24f) * 0.5f, 120, 24, UiTheme.SlateBlue, TextAnchor.MiddleLeft, false, true));
                _walletNums[i] = HudArt.PopLabel(ctx, _wallet, UiFontId.F32, 120, y - 2f, HudOverlayLayout.StatsW - 120 - 16, h + 4f,
                    i == 0 ? UiTheme.Gold : UiTheme.Chalk, TextAnchor.MiddleRight, 1f, 0.5f);
                UiKit.DropShadow(_walletNums[i], 2f);
                // 줄 전체 글(머리 글 + 숫자)은 이름 표 규칙대로 만들어 들고 있되 보이지 않는다(접근성·테스트용 값).
                var full = UiKit.Label(ctx, _wallet, "", UiFontId.F16, 0, 0, 1, 1, UiTheme.Chalk, TextAnchor.MiddleRight);
                full.name = "RowText" + i;
                full.gameObject.SetActive(false);
                _walletRows[i] = new HudText(full);
            }

            // "+n": 점수 줄(1)의 숫자 왼쪽, 작은 흐린 글. 자리는 숫자 글 폭에 맞춰 매 프레임 옮긴다.
            float py = HudOverlayLayout.StatsRowY + HudOverlayLayout.StatsRowStep + (HudOverlayLayout.StatsRowH - PendingH) * 0.5f;
            _pendingNode = UiKit.Node(_wallet, "ScorePendingNode", 0f, py, PendingW, PendingH);
            _pendingNode.pivot = new Vector2(1f, 0.5f);
            _pending = UiKit.Label(ctx, _pendingNode, "", UiFontId.F16, 0, 0, PendingW, PendingH,
                UiTheme.WithAlpha(UiTheme.Chalk, PendingAlpha), TextAnchor.MiddleRight, false, true);
            _pending.name = "ScorePending";
            UiKit.Stretch(_pending.Rect);
            _pendingNode.gameObject.SetActive(false);
        }

        public void OnEvent(HudLayer layer, SimWorld w, in SimEvent e)
        {
            if (e.Slot != 0 && e.Slot != -1) return;
            switch (e.Type)
            {
                case SimEventType.PlayerHit:
                    _portraitHitLeft = HudLayout.PortraitHitSeconds;
                    if (layer.MotionOn)
                    {
                        UiMotion.Shake(_hpRow, HpHitShakePx, HpHitShakeSeconds);
                        UiMotion.Pop(_portraitNode, 1.08f, 0.2f);
                    }
                    layer.Play("ui.hud.portrait_hit");
                    layer.Play("ui.hud.hp_change");
                    break;
                case SimEventType.PlayerHealed:
                    if (layer.MotionOn) UiMotion.Pop(_hpRow, 1.06f, 0.25f);
                    layer.Play("ui.hud.hp_change");
                    break;
            }
        }

        public void Refresh(HudLayer layer, SimWorld w, in HudFrame f, float dt)
        {
            var p = w != null ? w.P0 : null;
            bool hasP = p != null && p.Active;
            HudArt.SetActive(_plate, hasP);
            if (!hasP) { HudArt.SetActive(_wallet, false); return; }
            var names = layer.Names;

            // 체력 칸
            int max = Mathf.Clamp(p.MaxHpCells, 0, MaxHpCells);
            int cells = Mathf.Clamp(p.HpCells, 0, max);
            if (_lastHpCells > cells)
                for (int i = cells; i < _lastHpCells && i < MaxHpCells; i++) _hpBreakT[i] = 0.0001f;
            if (_lastHpCells >= 0 && _lastHpCells != cells && cells == 1) layer.Play("ui.hud.hp_last_cell");
            _lastHpCells = cells;
            bool last = HudModel.HpLastCell(p);
            _beatT += dt;
            for (int i = 0; i < MaxHpCells; i++)
            {
                bool on = i < max;
                HudArt.SetActive(_hp[i], on);
                if (!on) { HudArt.SetActive(_hpBreak[i], false); continue; }
                bool full = i < cells;
                HudArt.SetSprite(_hp[i], full ? _hpFull : _hpEmpty, full ? UiTheme.Chalk : UiTheme.WoodLight);
                // 마지막 한 칸은 느리게 뛴다(작은 면적, 배율만).
                float beat = last && i == 0 && layer.MotionOn ? 1f + LastCellBeatScale * Mathf.Max(0f, Mathf.Sin(_beatT * 2f * Mathf.PI / LastCellBeatPeriod)) : 1f;
                _hp[i].rectTransform.localScale = new Vector3(beat, beat, 1f);
                // 깨짐(4프레임, 0.2초)
                if (_hpBreakT[i] > 0f)
                {
                    _hpBreakT[i] += dt;
                    float dur = _breakAnim != null && _breakAnim.Fps > 0f ? _breakAnim.Duration : 0.2f;
                    if (_hpBreakT[i] >= dur || _breakAnim == null) { _hpBreakT[i] = 0f; HudArt.SetActive(_hpBreak[i], false); }
                    else { HudArt.SetActive(_hpBreak[i], true); HudArt.SetSprite(_hpBreak[i], _breakAnim.At(_hpBreakT[i]), UiTheme.Chalk); }
                }
                else HudArt.SetActive(_hpBreak[i], false);
            }
            HudArt.SetActive(_corner, last);
            if (last) UiKit.Place(_corner.rectTransform, 0, 0, CornerPx, CornerPx);

            // 초상
            if (p.CharId != _charId)
            {
                _charId = p.CharId;
                HudArt.SetSprite(_portrait, _art.Portrait(p.CharId), UiTheme.WoodLight);
            }
            if (_portraitHitLeft > 0f) _portraitHitLeft -= dt;
            bool hit = _portraitHitLeft > 0f;
            HudArt.SetActive(_portraitFlash, hit);
            HudArt.SetKit(_portraitFrame, _art.Kit("S-0802", hit ? "portrait_hit" : "portrait"), new Color(0, 0, 0, 0));
            _stage.Set(HudModel.WandStageText(names, p.Wand != null ? p.Wand.EffectiveStage : 0));

            RefreshHeat(layer, p, f);

            // 지갑·점수·처치(연습 방에서 숨김)
            HudArt.SetActive(_wallet, f.ShowWallet);
            if (f.ShowWallet)
            {
                SetStat(layer, 0, HudModel.KeyWallet, f.Purse);
                long before = _walletLast[1];
                SetStat(layer, 1, HudModel.KeyScore, f.Score);
                SetStat(layer, 2, HudModel.KeyKills, f.Kills);
                RefreshPending(layer, f, before != long.MinValue && f.Score > before, dt);
            }
            else HudArt.SetActive(_pendingNode, false);
        }

        /// <summary>
        /// "+n" 갱신. 웨이브 전투 중이면 시뮬의 진행 중 몫(f.ScorePending)을 보인다. 본 점수가 오른 프레임에 보이던 몫이 있으면
        /// 그 글이 점수 숫자 쪽으로 빨려 들어가며 작아지고 사라진다(연출 감도 최소면 바로 사라진다 — 점수 숫자 튀기는 SetStat이 한다).
        /// </summary>
        void RefreshPending(HudLayer layer, in HudFrame f, bool scoreRose, float dt)
        {
            if (_pendingNode == null) return;
            var names = layer.Names;
            var num = _walletNums[1];
            float numW = num != null && num.Text != null ? num.Text.preferredWidth : 48f;
            float right = HudOverlayLayout.StatsW - 16f;
            float rowMid = -(HudOverlayLayout.StatsRowY + HudOverlayLayout.StatsRowStep + HudOverlayLayout.StatsRowH * 0.5f);
            var rest = new Vector2(right - numW - PendingGapPx, rowMid);

            if (scoreRose && _pendingShown > 0 && _pendingMergeT < 0f)
            {
                if (layer.MotionOn)
                {
                    _pendingMergeT = 0f;
                    _pendingFrom = rest;
                    float tw = _pending.Text != null ? _pending.Text.preferredWidth : 0f;
                    _pendingTo = new Vector2(right - numW * 0.5f + tw * 0.5f, rowMid);
                }
                _pendingShown = 0;
            }

            if (_pendingMergeT >= 0f)
            {
                _pendingMergeT += Mathf.Max(dt, Time.unscaledDeltaTime);
                float t = Mathf.Clamp01(_pendingMergeT / PendingMergeSeconds);
                float e = t * t; // 빨려 들어가듯 끝으로 갈수록 빠르게
                _pendingNode.anchoredPosition = Vector2.Lerp(_pendingFrom, _pendingTo, e);
                float sc = Mathf.Lerp(1f, 0.55f, e);
                _pendingNode.localScale = new Vector3(sc, sc, 1f);
                _pending.Color = UiTheme.WithAlpha(UiTheme.Chalk, PendingAlpha * (1f - e));
                HudArt.SetActive(_pendingNode, true);
                if (t >= 1f)
                {
                    _pendingMergeT = -1f;
                    _pendingNode.localScale = Vector3.one;
                    HudArt.SetActive(_pendingNode, false);
                    // 합쳐진 순간 점수 숫자가 한 번 더 튄다
                    if (num != null) UiMotion.Pop(num.transform.parent, UiTheme.NumberPopScale, UiTheme.NumberPopSeconds);
                }
                return;
            }

            int n = f.ScorePending;
            bool on = n > 0;
            HudArt.SetActive(_pendingNode, on);
            if (!on) { _pendingShown = 0; return; }
            _pendingNode.anchoredPosition = rest;
            _pendingNode.localScale = Vector3.one;
            _pending.Color = UiTheme.WithAlpha(UiTheme.Chalk, PendingAlpha);
            if (n != _pendingShown)
            {
                _pending.SetText(HudModel.ScorePendingText(names, n));
                if (_pendingShown > 0 && layer.MotionOn) UiMotion.Pop(_pendingNode, 1.12f, 0.18f);
                _pendingShown = n;
            }
        }

        void SetStat(HudLayer layer, int i, string key, long value)
        {
            var names = layer.Names;
            _walletRows[i].Set(HudModel.WalletRow(names, key, value));
            if (_walletLast[i] == value) return;
            bool changed = _walletLast[i] != long.MinValue;
            _walletLast[i] = value;
            _walletNums[i].SetText(names != null ? names.Number(value) : value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (changed && layer.MotionOn) UiMotion.Pop(_walletNums[i].transform.parent, UiTheme.NumberPopScale, UiTheme.NumberPopSeconds);
        }

        void RefreshHeat(HudLayer layer, PlayerState p, in HudFrame f)
        {
            var state = HudModel.RingState(p);
            float H = _heat.H;
            float ratio = p.Locked ? Mathf.Clamp01(f.HeatLockRemainRatio) : HudModel.HeatRatio(p.HeatHu);
            Color c;
            switch (state)
            {
                case HeatRingState.Locked: c = HudColors.RingAsh; break;
                case HeatRingState.Critical:
                case HeatRingState.Brink: c = HudColors.RingCritical; break;
                case HeatRingState.Imminent: c = HudColors.RingSlate; break;
                default: c = UiTheme.WithAlpha(HudColors.RingSlate, 0.8f); break;
            }
            _heat.SetRatio(ratio);
            _heat.SetColor(c);
            float crit = HudModel.HeatRatio(HudModel.CritStartHu(p));
            bool showCrit = crit > 0f && crit < 1f && !p.Locked;
            HudArt.SetActive(_heatCrit, showCrit);
            if (showCrit) UiKit.Place(_heatCrit.rectTransform, _heat.InsetX + Mathf.Round(_heat.Span * crit) - 1f, _heat.InsetY - 2f, 2f, H - 2f * _heat.InsetY + 4f);
            // 임계·직전이면 막대 둘레가 은은히 숨쉰다(작은 면적).
            bool hot = state == HeatRingState.Critical || state == HeatRingState.Brink;
            float a = hot && layer.MotionOn ? 0.18f + 0.14f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI / 0.8f) : hot ? 0.25f : 0f;
            _heatGlow.color = UiTheme.WithAlpha(UiTheme.Chalk, a);
            _heatCaption.Color = p.Locked ? HudColors.RingAsh : hot ? UiTheme.Chalk : UiTheme.SlateBlue;
        }

        public void Dispose() => HudArt.Destroy(_root != null ? _root.gameObject : null);
    }
}
