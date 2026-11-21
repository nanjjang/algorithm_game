using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 플레이어 하나의 캐릭터 둘레 표시 중 HUD 캔버스에 그리는 것: 결정 칸(머리 위), 콤보 숫자, 최근 입력 5개, 지금 K 줄,
    /// 머리 위 늦음 문구. 전부 vc.WorldTextRoot 아래에서 발밑의 WorldToHud 자리를 따라간다(00 문서 7절 "전장 위 글자", D-69).
    /// 열 링·시야 화살표는 전장 층(HeatRingView)이다.
    /// </summary>
    public sealed class CharacterMarksView
    {
        const int MaxCrystalCells = 8;

        readonly ViewContext _vc;
        readonly HudArt _art;
        readonly HudTuning _tuning;
        readonly int _slot;
        public readonly RectTransform Root;

        // 결정 칸
        readonly RectTransform _crystalRow;
        readonly UImage[] _crystals = new UImage[MaxCrystalCells];
        readonly UImage[] _crystalFx = new UImage[MaxCrystalCells];
        readonly float[] _gainLeft = new float[MaxCrystalCells];
        readonly float[] _spendLeft = new float[MaxCrystalCells];
        readonly float[] _breakT = new float[MaxCrystalCells];
        readonly float[] _repayLeft = new float[MaxCrystalCells];
        readonly UImage _frostShield;
        float _frostLeft;
        float _t;
        Sprite _crystalFull, _crystalEmpty, _crystalDebt;
        SpriteAnim _crystalBreak, _frostShieldAnim;
        string _element;
        int _lastCrystals;
        readonly List<UImage> _coins = new List<UImage>();
        readonly List<Vector2> _coinFrom = new List<Vector2>();
        float _coinT = -1f;

        // 콤보(옵션에 따라 HUD 쪽 부모로 옮긴다)
        public readonly ComboView Combo;
        readonly RectTransform _comboHudAnchor;

        // 최근 입력
        readonly RectTransform _recentRoot;
        readonly UImage[] _glyphs = new UImage[HudLayout.RecentInputCount];
        readonly UImage[] _links = new UImage[HudLayout.RecentInputCount - 1];
        readonly List<RecentInput> _recent = new List<RecentInput>();

        // 지금 K
        readonly RectTransform _nowRoot;
        readonly HudText _nowMain, _nowJust;
        readonly UImage[] _border = new UImage[4];
        readonly UImage _bufferDot;
        float _blinkLeft;

        // 협동 부활 막대(쓰러졌고 서 있는 동료가 있을 때, Docs/11-online.md)
        const float ReviveBarW = 72f, ReviveBarH = 10f;
        readonly UImage _reviveBg, _reviveFill;
        int _reviveTicksMax;

        // 늦음 문구
        readonly HudText _late;
        float _lateLeft;

        public int CrystalCellsShown { get; private set; }
        public int RecentShown { get; private set; }
        public NowKText LastNowK { get; private set; }
        /// <summary>마지막으로 읽은 비기 미리보기(아래 비기 칸이 같은 값을 그린다 — 미리보기를 두 번 부르지 않는다). 없으면 ArtId null.</summary>
        public ArtPreview LastPreview { get; private set; }
        public bool NowKVisible => _nowRoot != null && _nowRoot.gameObject.activeSelf;
        public string LateText => _late.Current;
        public bool LateVisible => _late.Label != null && _late.Label.gameObject.activeSelf;
        public bool ComboInHud { get; private set; }

        public CharacterMarksView(ViewContext vc, HudArt art, HudTuning tuning, int slot, RectTransform comboHudAnchor)
        {
            _vc = vc;
            _art = art;
            _tuning = tuning;
            _slot = slot;
            _comboHudAnchor = comboHudAnchor;
            var parent = vc.WorldTextRoot != null ? vc.WorldTextRoot : vc.HudRoot;
            Root = HudArt.NodeRel(parent, "Marks." + slot, 0, 0, 0, 0);
            Root.pivot = new Vector2(0.5f, 0.5f);

            _reviveBg = HudArt.ImageRel(Root, "ReviveBar", -ReviveBarW * 0.5f, tuning.CrystalRowOffsetY - 24f, ReviveBarW, ReviveBarH, null, UiTheme.PlateFill);
            _reviveFill = HudArt.Image(_reviveBg.rectTransform, "Fill", 2f, 2f, ReviveBarW - 4f, ReviveBarH - 4f, null, UiTheme.Good);
            _reviveBg.raycastTarget = false;
            _reviveFill.raycastTarget = false;
            HudArt.SetActive(_reviveBg.rectTransform, false);

            _crystalRow = HudArt.NodeRel(Root, "Crystals", 0, tuning.CrystalRowOffsetY, HudLayout.CrystalRowWidth(MaxCrystalCells), HudLayout.CrystalCellPx);
            for (int i = 0; i < MaxCrystalCells; i++)
            {
                float x = i * (HudLayout.CrystalCellPx + HudLayout.CrystalGapPx);
                _crystals[i] = HudArt.Image(_crystalRow, "C" + i, x, 0, HudLayout.CrystalCellPx, HudLayout.CrystalCellPx, null, HudColors.CrystalEmpty);
                _crystalFx[i] = HudArt.Image(_crystalRow, "Fx" + i, x - 4, -4, 32, 32, null, UiTheme.Chalk);
                HudArt.SetActive(_crystalFx[i], false);
            }
            _frostShield = HudArt.Image(_crystalRow, "FrostShield", 0, -4, 192, 32, null, UiTheme.WithAlpha(HudColors.FrostShield, 0.5f));
            HudArt.SetActive(_frostShield, false);
            _crystalEmpty = art.Status("crystal_empty");
            _crystalDebt = art.Status("crystal_debt");
            _crystalBreak = art.StatusAnim("crystal_break");
            _frostShieldAnim = art.StatusAnim("frost_shield");

            Combo = new ComboView(art, Root, tuning);

            var rr = HudLayout.RecentInputs;
            _recentRoot = HudArt.NodeRel(Root, "Recent", rr.X, rr.Y, rr.W, rr.H);
            int glyph = HudLayout.RecentGlyphPx;
            float linkW = (rr.W - HudLayout.RecentInputCount * glyph) / (HudLayout.RecentInputCount - 1);
            for (int i = 0; i < _glyphs.Length; i++)
            {
                _glyphs[i] = HudArt.Image(_recentRoot, "G" + i, i * (glyph + linkW), 0, glyph, glyph, null, UiTheme.Chalk);
                if (i < _links.Length)
                    _links[i] = HudArt.Image(_recentRoot, "L" + i, i * (glyph + linkW) + glyph, 0, linkW, glyph, null, UiTheme.Chalk);
            }

            var nk = HudLayout.NowK;
            _nowRoot = HudArt.NodeRel(Root, "NowK", nk.X, nk.Y, nk.W, nk.H * 2);
            _nowJust = new HudText(UiKit.Label(art.Ctx, _nowRoot, "", UiFontId.F16, 0, 0, nk.W, nk.H, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true));
            UiKit.DropShadow(_nowJust.Label, 2f);
            _nowMain = new HudText(UiKit.Label(art.Ctx, _nowRoot, "", UiFontId.F16, 0, nk.H, nk.W, nk.H, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true));
            UiKit.DropShadow(_nowMain.Label, 2f);
            for (int i = 0; i < 4; i++) _border[i] = UiKit.Rect(_nowRoot, 0, 0, 1, 1, UiTheme.WithAlpha(UiTheme.Chalk, 0.8f), false, "Border" + i);
            _bufferDot = UiKit.Rect(_nowRoot, 3, 0, 4, 4, UiTheme.Chalk, false, "BufferDot");
            HudArt.SetActive(_bufferDot, false);

            _late = new HudText(UiKit.Label(art.Ctx, Root, "", UiFontId.F16, 0, 0, nk.W, nk.H, UiTheme.Chalk, TextAnchor.MiddleCenter, false, true));
            HudArt.PlaceRel(_late.Label.Rect, -nk.W * 0.5f, tuning.LateLineOffsetY, nk.W, nk.H);
            _late.Show(false);
        }

        public void OnEvent(HudLayer layer, SimWorld w, in SimEvent e)
        {
            if (e.Slot != _slot && e.Type != SimEventType.WaveCleared) return;
            var p = w != null && _slot >= 0 && _slot < w.Players.Length ? w.Players[_slot] : null;
            switch (e.Type)
            {
                case SimEventType.ComboChanged:
                case SimEventType.ComboReset:
                    Combo.OnEvent(e);
                    break;
                case SimEventType.CrystalGained:
                    SetTimer(_gainLeft, e.A - 1, HudLayout.CrystalGainSeconds);
                    break;
                case SimEventType.CrystalSpent:
                {
                    int now = p != null ? p.Crystals : 0;
                    for (int i = 0; i < e.A; i++) SetTimer(_spendLeft, now + i, HudLayout.CrystalSpendSeconds);
                    break;
                }
                case SimEventType.CrystalLost:
                {
                    int now = p != null ? p.Crystals : 0;
                    // 맨 끝 결정부터 깨진다. 둘이면 0.06초 차.
                    for (int i = 0; i < e.A; i++)
                    {
                        int idx = now + e.A - 1 - i;
                        if (idx >= 0 && idx < MaxCrystalCells) _breakT[idx] = -i * HudLayout.CrystalSecondBreakDelay + 0.0001f;
                    }
                    break;
                }
                case SimEventType.CrystalProtected:
                    _frostLeft = _tuning.FrostShieldSeconds;
                    break;
                case SimEventType.DebtRepaid:
                    SetTimer(_repayLeft, e.A, HudLayout.CrystalGainSeconds);
                    break;
                case SimEventType.ArtFizzled:
                    _blinkLeft = _tuning.FizzleBlinkSeconds;
                    if (e.A == (int)ArtFizzleReason.FullBloomLate && e.B > 0 && layer.Names != null)
                    {
                        _late.Set(HudModel.LateInputText(layer.Names, e.B, e.F));
                        _lateLeft = _tuning.LateLineSeconds;
                        _late.Show(true);
                    }
                    break;
                case SimEventType.CommandDirInput:
                    layer.Play("ui.hud.recent_input");
                    break;
                case SimEventType.WaveCleared:
                    if (_lastCrystals > 0) StartCoins(_lastCrystals);
                    break;
            }
        }

        static void SetTimer(float[] arr, int i, float v) { if (i >= 0 && i < arr.Length) arr[i] = v; }

        void StartCoins(int count)
        {
            var parent = _vc.WorldTextRoot != null ? _vc.WorldTextRoot : _vc.HudRoot;
            _coinFrom.Clear();
            Vector2 rowOrigin = Root.anchoredPosition + new Vector2(_crystalRow.anchoredPosition.x, _crystalRow.anchoredPosition.y);
            for (int i = 0; i < count && i < MaxCrystalCells; i++)
            {
                if (i >= _coins.Count)
                {
                    var img = UiKit.Rect(parent, 0, 0, 16, 16, UiTheme.Gold, false, "Coin");
                    img.rectTransform.anchorMin = img.rectTransform.anchorMax = img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    _coins.Add(img);
                }
                float x = i * (HudLayout.CrystalCellPx + HudLayout.CrystalGapPx) + HudLayout.CrystalCellPx * 0.5f;
                _coinFrom.Add(rowOrigin + new Vector2(x, -HudLayout.CrystalCellPx * 0.5f));
            }
            _coinT = 0f;
        }

        void RenderCoins(float dt)
        {
            if (_coinT < 0f) return;
            _coinT += dt;
            float dur = Mathf.Max(0.01f, _tuning.WaveEndCoinSeconds);
            // 원정 지갑 줄(1744,40)의 HUD 좌표 → 가운데 원점
            Vector2 to = new Vector2(HudLayout.WalletX - UiScale.RefWidth * 0.5f, UiScale.RefHeight * 0.5f - HudLayout.WalletY - HudLayout.WalletRowH * 0.5f);
            bool any = false;
            for (int i = 0; i < _coins.Count; i++)
            {
                if (i >= _coinFrom.Count) { HudArt.SetActive(_coins[i], false); continue; }
                float k = Mathf.Clamp01((_coinT - i * 0.05f) / dur);
                bool on = k > 0f && k < 1f;
                HudArt.SetActive(_coins[i], on);
                if (!on) { if (k <= 0f) any = true; continue; }
                any = true;
                Vector2 pos = Vector2.Lerp(_coinFrom[i], to, k * k);
                _coins[i].rectTransform.anchoredPosition = pos;
            }
            if (!any) _coinT = -1f;
        }

        public void Render(SimWorld w, PlayerState p, ViewOptions options, float alpha, float dt)
        {
            _t += dt;
            bool show = p != null && p.Active;
            HudArt.SetActive(Root, show);
            RenderCoins(dt);
            if (!show) { HudArt.SetActive(Combo.Root, false); return; }

            Vector2 feet = HudModel.FeetOf(HudModel.Interp(p, alpha));
            Root.anchoredPosition = _vc.WorldToHud(feet);

            string element = p.Def != null ? p.Def.Element : null;
            if (element != _element)
            {
                _element = element;
                string suf = HudModel.ElementSuffix(element);
                _crystalFull = suf != null ? _art.EffectFrame("player", "crystal_" + suf, 0) : null;
            }
            Color elementColor = UiTheme.ElementColor(element);

            RenderCrystals(p, elementColor, dt);

            // 콤보 자리
            bool hudMode = HudModel.ComboInHud(options) && _slot == w.ViewSlot && _comboHudAnchor != null;
            ComboInHud = hudMode;
            if (hudMode) Combo.Attach(_comboHudAnchor, 0, 0, false);
            else Combo.Attach(Root, HudLayout.ComboOffset.x, HudLayout.ComboOffset.y - HudLayout.ComboDigitH * 0.5f, true);
            Combo.Render(p, elementColor, dt);

            RenderRecent(w, p);
            RenderNowK(w, p, dt);
            RenderRevive(w, p);

            if (_lateLeft > 0f) { _lateLeft -= dt; if (_lateLeft <= 0f) _late.Show(false); }
        }

        /// <summary>마지막으로 그린 부활 진행(0~1, 막대가 없으면 −1). 테스트용.</summary>
        public float ReviveShown { get; private set; } = -1f;

        void RenderRevive(SimWorld w, PlayerState p)
        {
            bool show = p.Downed && CoopRules.HasStandingAlly(w, p);
            HudArt.SetActive(_reviveBg.rectTransform, show);
            if (!show) { ReviveShown = -1f; return; }
            if (_reviveTicksMax <= 0) _reviveTicksMax = System.Math.Max(1, CoopRules.Revive(w.Content).Ticks);
            float f = Mathf.Clamp01(p.ReviveTicks / (float)_reviveTicksMax);
            ReviveShown = f;
            var rt = _reviveFill.rectTransform;
            rt.sizeDelta = new Vector2((ReviveBarW - 4f) * f, rt.sizeDelta.y);
            // 진행이 없으면 테두리만 깜박여 "곁에 가면 살릴 수 있다"를 알린다.
            _reviveBg.color = p.ReviveTicks > 0 ? UiTheme.PlateFill : UiTheme.WithAlpha(UiTheme.Good, 0.35f + 0.25f * Mathf.Sin(_t * 6f));
        }

        void RenderCrystals(PlayerState p, Color elementColor, float dt)
        {
            int cells = Mathf.Min(MaxCrystalCells, HudModel.CrystalCells(p));
            CrystalCellsShown = cells;
            float rowW = HudLayout.CrystalRowWidth(cells);
            HudArt.PlaceRel(_crystalRow, -rowW * 0.5f, _tuning.CrystalRowOffsetY, rowW, HudLayout.CrystalCellPx);
            bool full = cells > 0 && p.Crystals >= cells;
            float breath = full ? 1f + 0.06f * Mathf.Sin(_t * Mathf.PI * 2f / HudLayout.CrystalFullBreathPeriod) : 1f;
            for (int i = 0; i < MaxCrystalCells; i++)
            {
                var img = _crystals[i];
                bool on = i < cells;
                HudArt.SetActive(img, on);
                if (!on) { HudArt.SetActive(_crystalFx[i], false); continue; }
                int kind = HudModel.CrystalSlotKind(i, p.Crystals, p.Debt);
                float scale = 1f;
                Color tint = Color.white;
                if (kind == 1)
                {
                    SetCrystalSprite(img, _crystalFull, elementColor);
                    if (_gainLeft[i] > 0f)
                    {
                        _gainLeft[i] -= dt;
                        scale = 1f + 0.35f * Mathf.Clamp01(_gainLeft[i] / HudLayout.CrystalGainSeconds);
                    }
                    else scale = breath;
                }
                else if (_spendLeft[i] > 0f)
                {
                    // 비기로 씀: 칸에서 지팡이로 빨려 듦(0.1초)
                    _spendLeft[i] -= dt;
                    SetCrystalSprite(img, _crystalFull, elementColor);
                    scale = Mathf.Clamp01(_spendLeft[i] / HudLayout.CrystalSpendSeconds);
                }
                else if (kind == 2)
                {
                    SetCrystalSprite(img, _crystalDebt, HudColors.DebtPurple, true);
                }
                else if (_repayLeft[i] > 0f)
                {
                    // 빚 갚음: 테두리와 함께 사라짐
                    _repayLeft[i] -= dt;
                    SetCrystalSprite(img, _crystalDebt, HudColors.DebtPurple, true);
                    tint.a = Mathf.Clamp01(_repayLeft[i] / HudLayout.CrystalGainSeconds);
                }
                else SetCrystalSprite(img, _crystalEmpty, HudColors.CrystalEmpty, true);
                if (tint.a < 1f) img.color = new Color(img.color.r, img.color.g, img.color.b, tint.a);
                img.rectTransform.localScale = new Vector3(scale, scale, 1f);

                // 피격 깨짐(4프레임)
                var fx = _crystalFx[i];
                if (_breakT[i] != 0f)
                {
                    _breakT[i] += dt;
                    float dur = Mathf.Max(0.01f, _tuning.CrystalBreakSeconds);
                    if (_breakT[i] >= dur) { _breakT[i] = 0f; HudArt.SetActive(fx, false); }
                    else if (_breakT[i] > 0f)
                    {
                        HudArt.SetActive(fx, true);
                        Sprite s = null;
                        if (_crystalBreak != null)
                        {
                            int f = Mathf.Clamp((int)(_breakT[i] / dur * _crystalBreak.Frames.Length), 0, _crystalBreak.Frames.Length - 1);
                            s = _crystalBreak.Frames[f];
                        }
                        HudArt.SetSprite(fx, s, UiTheme.WithAlpha(elementColor, 1f - _breakT[i] / dur));
                    }
                    else HudArt.SetActive(fx, false);
                }
                else HudArt.SetActive(fx, false);
            }
            _lastCrystals = p.Crystals;

            // 제자 4 지킴: 결정 칸 전체에 서리 방패가 한 번 빛남
            bool frost = _frostLeft > 0f;
            HudArt.SetActive(_frostShield, frost);
            if (frost)
            {
                _frostLeft -= dt;
                var r = _frostShield.rectTransform;
                UiKit.Place(r, (rowW - 192f) * 0.5f, -4f, 192f, 32f);
                Sprite s = null;
                if (_frostShieldAnim != null)
                {
                    float k = 1f - Mathf.Clamp01(_frostLeft / Mathf.Max(0.01f, _tuning.FrostShieldSeconds));
                    s = _frostShieldAnim.Frames[Mathf.Clamp((int)(k * _frostShieldAnim.Frames.Length), 0, _frostShieldAnim.Frames.Length - 1)];
                }
                HudArt.SetSprite(_frostShield, s, UiTheme.WithAlpha(HudColors.FrostShield, 0.5f));
            }
        }

        static void SetCrystalSprite(UImage img, Sprite s, Color fallback, bool keepWhite = false)
        {
            if (s != null) { if (img.sprite != s) img.sprite = s; img.color = Color.white; }
            else { img.sprite = null; img.color = fallback; }
        }

        void RenderRecent(SimWorld w, PlayerState p)
        {
            _recent.Clear();
            if (w != null && w.Mods != null && w.Mods.Arts != null) w.Mods.Arts.GetRecentInputs(p, _recent);
            int n = Mathf.Min(_recent.Count, HudLayout.RecentInputCount);
            int start = _recent.Count - n;
            RecentShown = n;
            HudArt.SetActive(_recentRoot, n > 0);
            if (n <= 0) return;
            for (int i = 0; i < _glyphs.Length; i++)
            {
                bool on = i < n;
                HudArt.SetActive(_glyphs[i], on);
                if (on)
                {
                    var ri = _recent[start + i];
                    HudArt.SetKit(_glyphs[i], _art.Kit("K-53", GlyphVariant(ri.Dir)), UiTheme.Chalk);
                }
                if (i < _links.Length)
                {
                    bool link = i + 1 < n;
                    HudArt.SetActive(_links[i], link);
                    if (link)
                    {
                        bool chained = _recent[start + i + 1].Chained;
                        HudArt.SetKit(_links[i], _art.Kit("K-55", chained ? "input_link_connected" : "input_link_gap"),
                            chained ? UiTheme.Chalk : UiTheme.WithAlpha(UiTheme.SlateBlue, 0.4f));
                    }
                }
            }
        }

        /// <summary>K-53 방향 입력 문양: 앞 / 뒤 / 왼옆(side) / 오른옆(reverse_side).</summary>
        public static string GlyphVariant(CmdDir d)
        {
            switch (d)
            {
                case CmdDir.Forward: return "direction_forward";
                case CmdDir.Back: return "direction_back";
                case CmdDir.Left: return "direction_side";
                default: return "direction_reverse_side";
            }
        }

        void RenderNowK(SimWorld w, PlayerState p, float dt)
        {
            bool phase = w != null && HudModel.NowKPhase(w.Run.Phase) && !p.Downed;
            NowKText t = default;
            ArtPreview pv = default;
            if (phase && w.Mods != null && w.Mods.Arts != null)
            {
                pv = w.Mods.Arts.Preview(w, p, w.NowU);
                t = HudModel.NowK(_vc.Game != null ? _vc.Game.Names : null, pv);
            }
            LastNowK = t;
            LastPreview = t.Visible ? pv : default;
            HudArt.SetActive(_nowRoot, t.Visible);
            if (!t.Visible) return;
            bool two = !string.IsNullOrEmpty(t.Just);
            var nk = HudLayout.NowK;
            _nowJust.Show(two);
            if (two) _nowJust.Set(t.Just);
            _nowMain.Set(t.Main);
            // 섬 줄이 있으면 윗줄 = 섬, 아랫줄 = 보통. 없으면 한 줄을 (−96,+80)에.
            UiKit.Place(_nowMain.Label.Rect, 0, two ? nk.H : 0, nk.W, nk.H);

            // 불발: 두 번 깜빡
            float a = 1f;
            if (_blinkLeft > 0f)
            {
                _blinkLeft -= dt;
                float k = 1f - Mathf.Clamp01(_blinkLeft / Mathf.Max(0.01f, _tuning.FizzleBlinkSeconds));
                a = ((int)(k * 4f) % 2 == 0) ? 0.25f : 1f;
            }
            _nowMain.Color = UiTheme.WithAlpha(t.Fizzle ? UiTheme.SlateBlue : UiTheme.Chalk, a);
            _nowJust.Color = UiTheme.WithAlpha(UiTheme.Chalk, a);

            // 재사용 대기 테두리(0.4초에 다시 참) + 보관된 K 점
            float h = two ? nk.H * 2f : nk.H;
            // 상자 테 대신 글 아래 가운데의 가는 줄 하나: 재사용 대기 동안 가운데에서 양쪽으로 자라고, 다 차면 사라진다.
            float cd = Mathf.Clamp01(pv.CooldownRatio);
            bool cooling = cd < 1f;
            float uw = nk.W * 0.5f * cd;
            UiKit.Place(_border[2].rectTransform, (nk.W - uw) * 0.5f, h, uw, 1);
            HudArt.SetActive(_border[0], false); HudArt.SetActive(_border[1], false);
            HudArt.SetActive(_border[2], cooling && uw > 0f); HudArt.SetActive(_border[3], false);
            HudArt.SetActive(_bufferDot, pv.Buffered);
            if (pv.Buffered) UiKit.Place(_bufferDot.rectTransform, 3, (h - 4f) * 0.5f, 4, 4);
        }

        public void Dispose()
        {
            Combo.Dispose();
            for (int i = 0; i < _coins.Count; i++) HudArt.Destroy(_coins[i] != null ? _coins[i].gameObject : null);
            _coins.Clear();
            HudArt.Destroy(Root != null ? Root.gameObject : null);
        }
    }
}
