using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 콤보 숫자(combo_digits 글리프 24×32) + 아래 2초 막대(남은 비율 1 − idle/120, 잠금 중 멈춤 + 자물쇠 점) + 결정 직전 윤곽.
    /// +1 튐(1.2배 0.08초), 5·10 단위 색, 끊김 연출(시간 초과 = 회색으로 흐려지며 떨어짐, 피격 = 조각 6개로 깨져 흩어짐). 10 요약서 1.3절.
    /// 자리는 옵션 combo_position(캐릭터 옆 (+40,−72) / HUD 초상 옆)으로 부모를 바꿔 정한다.
    /// </summary>
    public sealed class ComboView
    {
        const int MaxDigits = 4;
        const float LockDotPx = 4f, OutlinePx = 16f, PieceFallPx = 16f;

        readonly HudArt _art;
        readonly HudTuning _tuning;
        public readonly RectTransform Root;
        readonly RectTransform _number;
        readonly UImage[] _digits = new UImage[MaxDigits];
        readonly HudText _fallback;
        readonly SpriteAnim _digitAnim;
        readonly UImage _barBg, _barFill, _lockDot;
        readonly UImage _outline, _outlineFill;
        readonly UImage[] _pieces = new UImage[HudLayout.ComboShatterPieces];
        readonly Vector2[] _pieceVel = new Vector2[HudLayout.ComboShatterPieces];
        readonly int[] _digitBuf = new int[MaxDigits];

        float _popLeft;
        float _breakLeft;
        bool _breakShatter;
        int _breakValue;

        public int ShownValue { get; private set; }
        public bool Visible => Root != null && Root.gameObject.activeSelf;
        public float BarRatio { get; private set; }
        public bool LockDotShown { get; private set; }
        public bool NearCrystalShown { get; private set; }
        public int Tier { get; private set; }
        public bool Breaking => _breakLeft > 0f;

        public ComboView(HudArt art, RectTransform parent, HudTuning tuning)
        {
            _art = art;
            _tuning = tuning;
            Root = HudArt.NodeRel(parent, "Combo", 0, 0, MaxDigits * HudLayout.ComboDigitW + OutlinePx + 4, HudLayout.ComboDigitH + 8);
            _number = UiKit.Node(Root, "Number", 0, 0, MaxDigits * HudLayout.ComboDigitW, HudLayout.ComboDigitH);
            _number.pivot = new Vector2(0f, 0.5f);
            _number.anchoredPosition = new Vector2(0f, -HudLayout.ComboDigitH * 0.5f);
            _digitAnim = art.StatusAnim("combo_digits");
            for (int i = 0; i < MaxDigits; i++)
            {
                _digits[i] = HudArt.Image(_number, "D" + i, i * HudLayout.ComboDigitW, 0, HudLayout.ComboDigitW, HudLayout.ComboDigitH, null, UiTheme.Chalk);
                HudArt.SetActive(_digits[i], false);
            }
            _fallback = new HudText(UiKit.Label(art.Ctx, _number, "", UiFontId.F32, 0, 0, MaxDigits * HudLayout.ComboDigitW, HudLayout.ComboDigitH,
                UiTheme.Chalk, TextAnchor.MiddleLeft, false, true));
            _fallback.Show(_digitAnim == null);

            float barY = HudLayout.ComboDigitH + 2f;
            _barBg = UiKit.Rect(Root, 0, barY, tuning.ComboBarWidth, tuning.ComboBarHeight, UiTheme.WithAlpha(UiTheme.Ink, 0.7f), false, "BarBg");
            _barFill = UiKit.Rect(Root, 0, barY, tuning.ComboBarWidth, tuning.ComboBarHeight, UiTheme.Chalk, false, "BarFill");
            _lockDot = UiKit.Rect(Root, tuning.ComboBarWidth + 2f, barY - 1f, LockDotPx, LockDotPx, HudColors.RingAsh, false, "LockDot");
            HudArt.SetActive(_lockDot, false);

            _outline = HudArt.Image(Root, "CrystalOutline", 0, (HudLayout.ComboDigitH - OutlinePx) * 0.5f, OutlinePx, OutlinePx,
                art.Status("crystal_empty"), HudColors.CrystalEmpty);
            _outlineFill = HudArt.Image(_outline.rectTransform, "Fill", 0, 0, OutlinePx, OutlinePx, null, UiTheme.Chalk);
            _outlineFill.type = UImage.Type.Filled;
            _outlineFill.fillMethod = UImage.FillMethod.Vertical;
            _outlineFill.fillOrigin = (int)UImage.OriginVertical.Bottom;
            HudArt.SetActive(_outline, false);

            for (int i = 0; i < _pieces.Length; i++)
            {
                _pieces[i] = UiKit.Rect(Root, 0, 0, 5, 5, UiTheme.Chalk, false, "Piece" + i);
                HudArt.SetActive(_pieces[i], false);
            }
        }

        public void OnEvent(in SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.ComboChanged:
                    if (e.B > 0) _popLeft = HudLayout.ComboPopSeconds;
                    _breakLeft = 0f;
                    break;
                case SimEventType.ComboReset:
                    if (e.B <= 0) break;
                    _breakValue = e.B;
                    _breakShatter = e.A == (int)ComboResetReason.Hit || e.A == (int)ComboResetReason.UnstableCore;
                    _breakLeft = _tuning.ComboBreakSeconds;
                    if (_breakShatter) StartShatter();
                    break;
            }
        }

        void StartShatter()
        {
            for (int i = 0; i < _pieces.Length; i++)
            {
                float a = (i / (float)_pieces.Length) * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                _pieceVel[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(60f, 110f);
                var rt = _pieces[i].rectTransform;
                rt.anchoredPosition = new Vector2(HudLayout.ComboDigitW, -HudLayout.ComboDigitH * 0.5f);
                HudArt.SetActive(_pieces[i], true);
            }
        }

        /// <summary>엮인 부모와 자리(왼쪽 위 기준 dx, dy)를 정한다. 같은 부모면 자리만 바꾼다.</summary>
        public void Attach(RectTransform parent, float dx, float dy, bool centerAnchored)
        {
            if (Root.parent != parent) Root.SetParent(parent, false);
            if (centerAnchored) HudArt.PlaceRel(Root, dx, dy, Root.sizeDelta.x, Root.sizeDelta.y);
            else UiKit.Place(Root, dx, dy, Root.sizeDelta.x, Root.sizeDelta.y);
        }

        public void Render(PlayerState p, Color elementColor, float dt)
        {
            int combo = p != null ? p.Combo : 0;
            bool breaking = _breakLeft > 0f;
            if (breaking) _breakLeft -= dt;
            bool show = combo > 0 || breaking;
            HudArt.SetActive(Root, show);
            if (!show) { ShownValue = 0; return; }

            int value = combo > 0 ? combo : _breakValue;
            ShownValue = value;
            int n = HudModel.ComboDigits(value, _digitBuf);
            Tier = HudModel.ComboTier(value);
            Color col = HudColors.ComboTierColor(Tier, elementColor);
            float alpha = 1f;
            Vector2 fall = Vector2.zero;
            if (combo <= 0 && breaking)
            {
                float k = 1f - Mathf.Clamp01(_breakLeft / Mathf.Max(0.001f, _tuning.ComboBreakSeconds));
                if (_breakShatter) alpha = 0f;
                else { col = HudColors.ComboFaded; alpha = 1f - k; fall = new Vector2(0f, -PieceFallPx * k); }
            }
            col.a *= alpha;

            if (_digitAnim != null)
            {
                for (int i = 0; i < MaxDigits; i++)
                {
                    bool on = i < n;
                    HudArt.SetActive(_digits[i], on);
                    if (!on) continue;
                    int d = _digitBuf[i];
                    _digits[i].sprite = d < _digitAnim.Frames.Length ? _digitAnim.Frames[d] : null;
                    _digits[i].color = col;
                }
            }
            else
            {
                _fallback.Set(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                _fallback.Color = col;
            }

            float scale = 1f;
            if (_popLeft > 0f)
            {
                _popLeft -= dt;
                float k = Mathf.Clamp01(_popLeft / HudLayout.ComboPopSeconds);
                scale = 1f + (HudLayout.ComboPopScale - 1f) * k;
            }
            _number.localScale = new Vector3(scale, scale, 1f);
            _number.anchoredPosition = new Vector2(0f, -HudLayout.ComboDigitH * 0.5f) + fall;

            // 2초 막대(잠금 중 멈춤 + 자물쇠 점)
            bool live = combo > 0;
            BarRatio = live && p != null ? HudModel.ComboBarRatio(p.ComboIdleTicks) : 0f;
            HudArt.SetActive(_barBg, live);
            HudArt.SetActive(_barFill, live && BarRatio > 0f);
            if (live) _barFill.rectTransform.sizeDelta = new Vector2(_tuning.ComboBarWidth * BarRatio, _tuning.ComboBarHeight);
            LockDotShown = live && p != null && p.Locked;
            HudArt.SetActive(_lockDot, LockDotShown);

            // 결정 직전(남은 콤보 3 이하): 숫자 옆 작은 결정 윤곽이 차오름
            int per = p != null && p.Def != null ? p.Def.ComboPerCrystal : 0;
            NearCrystalShown = live && HudModel.ComboNearCrystal(combo, per);
            HudArt.SetActive(_outline, NearCrystalShown);
            if (NearCrystalShown)
            {
                float x = n * HudLayout.ComboDigitW + 4f;
                UiKit.Place(_outline.rectTransform, x, (HudLayout.ComboDigitH - OutlinePx) * 0.5f, OutlinePx, OutlinePx);
                _outlineFill.color = elementColor;
                _outlineFill.fillAmount = HudModel.ComboCrystalFill(combo, per);
            }

            // 깨진 조각
            for (int i = 0; i < _pieces.Length; i++)
            {
                if (!_pieces[i].gameObject.activeSelf) continue;
                if (!breaking || !_breakShatter) { HudArt.SetActive(_pieces[i], false); continue; }
                var rt = _pieces[i].rectTransform;
                _pieceVel[i] += new Vector2(0f, -240f) * dt;
                rt.anchoredPosition += _pieceVel[i] * dt;
                float k = Mathf.Clamp01(_breakLeft / Mathf.Max(0.001f, _tuning.ComboBreakSeconds));
                _pieces[i].color = UiTheme.WithAlpha(HudColors.ComboTierColor(HudModel.ComboTier(_breakValue), elementColor), k);
            }
        }

        public void Dispose() => HudArt.Destroy(Root != null ? Root.gameObject : null);
    }
}
