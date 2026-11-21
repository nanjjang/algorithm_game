using UnityEngine;
using UnityEngine.InputSystem;
using Wizard.Solo.Art;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.View.Hud
{
    /// <summary>
    /// 아래 가운데 칸 판: 지팡이 회로(코어 1 + 연결 최대 8 = 9칸, TV-049 칸 수)와 그 오른쪽 비기 칸(K).
    /// 회로: 발사(ShotFired)마다 블록이 든 칸이 순서대로 켜진다(켜진 칸은 빛 테 + 살짝 튐). 빈 칸은 빈 칸 그림, 밑줄(갈피 패시브) 칸에는 밑줄 표식.
    /// 비기 칸: 지금 K로 나갈 비기의 아이콘, 결정 값 마름모, 재사용 대기(어두운 부채꼴), 쓸 수 있으면 테가 숨쉰다.
    /// 칸 크기는 오버레이 배치(HudOverlayLayout, 키트 K-60 64×64 칸 그림 그대로)이고, 칸 수·켜짐 규칙은 08 요약서 6.1절 그대로.
    /// </summary>
    public sealed class HudCircuitPanel
    {
        const float UnderlineH = 4f;
        const int MaxCostPips = 7;

        readonly HudArt _art;
        readonly HudTuning _tuning;
        readonly RectTransform _root;
        readonly UImage _trayBody, _trayShadow;
        readonly RectTransform _cellsRoot;
        readonly RectTransform[] _cellNodes = new RectTransform[HudLayout.CircuitMaxCells];
        readonly UImage[] _cells = new UImage[HudLayout.CircuitMaxCells];
        readonly UImage[] _icons = new UImage[HudLayout.CircuitMaxCells];
        readonly UiFrame[] _litFrames = new UiFrame[HudLayout.CircuitMaxCells];
        readonly float[] _litGlow = new float[HudLayout.CircuitMaxCells];
        readonly string[] _blockIds = new string[HudLayout.CircuitMaxCells];
        // 판 한정 블록 성장(★) 표식: 칸 오른쪽 아래 작은 금빛 글자.
        readonly UiLabel[] _stars = new UiLabel[HudLayout.CircuitMaxCells];
        readonly int[] _starShown = new int[HudLayout.CircuitMaxCells];
        readonly UImage _underline;
        readonly UiSprite _empty, _filled;
        int _underlineSlot = -1;
        float _litT = -1f;
        int _cellCount;
        // 블록이 든 칸만 판에 보인다: 칸 번호 → 보이는 자리(-1 = 숨김), 보이는 칸 수.
        readonly int[] _visIndex = new int[HudLayout.CircuitMaxCells];
        int _visCount;
        int _lastLit = -1;
        int _layoutCells = -1;
        bool _layoutArt;

        // 비기 칸
        readonly RectTransform _art0;
        readonly UImage _artIcon;
        readonly UImage _artCooldown;
        readonly UImage _artReady;
        readonly UiPulse _artPulse;
        readonly UImage[] _pips = new UImage[MaxCostPips];
        string _artId;
        bool _wasReady;

        public int CellCount => _cellCount;
        public float RowWidth => HudLayout.CircuitWidth(_cellCount);
        public int UnderlineSlot => _underlineSlot;
        /// <summary>비기 칸이 보이는가.</summary>
        public bool ArtSlotVisible => _art0 != null && _art0.gameObject.activeSelf;

        /// <summary>패시브 밑줄의 칸(02 요약서 8.3절 "맨 끝 연결 칸", 슬롯이 늘면 한 칸 뒤로). 없으면 −1.</summary>
        public static int UnderlineSlotOf(PlayerState p)
        {
            if (p == null || !p.Derived.Underline) return -1;
            int links = p.Loadout != null && p.Loadout.Links != null ? p.Loadout.Links.Length : 0;
            return links > 0 ? links : -1;
        }
        /// <summary>칸(0 = 코어, 1..L)의 판 한정 별(★). 없으면 0.</summary>
        public static int StarsAt(PlayerState p, int cell)
        {
            if (p == null || p.Loadout == null) return 0;
            if (cell == 0) return p.Loadout.Core.Stars;
            var links = p.Loadout.Links;
            return links != null && cell - 1 < links.Length && cell >= 1 ? links[cell - 1].Stars : 0;
        }

        /// <summary>지금 켜진 칸(-1 = 없음).</summary>
        public int LitCell { get; private set; } = -1;

        public HudCircuitPanel(HudArt art, RectTransform parent, HudTuning tuning)
        {
            _art = art;
            _tuning = tuning;
            var ctx = art.Ctx;
            float maxW = HudOverlayLayout.TrayWidth(HudLayout.CircuitMaxCells, true);
            float H = HudOverlayLayout.TrayH;
            _root = HudArt.Anchored(parent, "HudCircuit", 0.5f, 1f, 0f, -HudOverlayLayout.Edge, maxW, H);
            _trayShadow = UiKit.Rect(_root, HudOverlayLayout.ShadowPx, HudOverlayLayout.ShadowPx, maxW, H, UiTheme.Shadow, false, "Shadow");
            _trayBody = UiKit.Rect(_root, 0, 0, maxW, H, UiTheme.PlateFill, false, "Body");
            var recess = art.Kit("K-02", "recess");
            if (UiCommonArt.HasPanel(art.Library))
            {
                UiCommonArt.ApplyPanel(art.Library, _trayBody, 0.94f);
                UiCommonArt.ApplySliced(_trayShadow, UiCommonArt.PanelSprite(art.Library)); _trayShadow.color = UiTheme.Shadow;
            }
            else if (recess.IsValid)
            {
                UiKit.SetSprite(_trayBody, recess); _trayBody.color = UiTheme.WithAlpha(Color.white, 0.86f);
                UiKit.SetSprite(_trayShadow, recess); _trayShadow.color = UiTheme.Shadow;
            }
            // 금 테 판 대신 아래로 흐려지는 먹색 번짐(칸만 떠 보이게).
            _trayShadow.enabled = false;
            _trayBody.sprite = UiBackdrop.RadialSoft;
            _trayBody.type = UImage.Type.Simple;
            _trayBody.color = UiTheme.WithAlpha(UiTheme.Ink, 0.55f);
            _trayBody.raycastTarget = false;

            _empty = art.Kit("K-60", "item_slot_empty");
            _filled = art.Kit("K-60", "item_slot_filled");
            float cp = HudOverlayLayout.CellPx, ip = HudOverlayLayout.CellIconPx;
            _cellsRoot = UiKit.Node(_root, "Cells", HudOverlayLayout.TrayPad, HudOverlayLayout.CellY, HudOverlayLayout.CircuitRowWidth(HudLayout.CircuitMaxCells), cp + UnderlineH + 4);
            for (int i = 0; i < _cells.Length; i++)
            {
                float x = HudOverlayLayout.CellX(i);
                var node = UiKit.Node(_cellsRoot, "Cell" + i, x, 0, cp, cp);
                node.pivot = new Vector2(0.5f, 0.5f);
                node.anchoredPosition = new Vector2(x + cp * 0.5f, -cp * 0.5f);
                _cellNodes[i] = node;
                _cells[i] = UiKit.Rect(node, 0, 0, cp, cp, HudColors.CircuitOff, false, "Slot");
                HudArt.SetKit(_cells[i], _empty, HudColors.CircuitOff);
                float pad = (cp - ip) * 0.5f;
                _icons[i] = HudArt.Image(node, "Icon", pad, pad, ip, ip, null, UiTheme.SlateBlue);
                HudArt.SetActive(_icons[i], false);
                _litFrames[i] = UiFrame.Create(node, UiTheme.WithAlpha(UiTheme.Chalk, 0f), 3);
                _stars[i] = UiKit.Label(ctx, node, string.Empty, UiFontId.F16, cp - 40f, cp - 22f, 38f, 20f, UiTheme.Gold, TextAnchor.MiddleRight, outline: true);
                _stars[i].name = "Stars";
            }
            _underline = UiKit.Rect(_cellsRoot, 0, cp + 3, cp, UnderlineH, UiTheme.Chalk, false, "Underline");
            HudArt.SetActive(_underline, false);

            // 비기 칸
            float ap = HudOverlayLayout.ArtSlotPx;
            _art0 = UiKit.Node(_root, "ArtSlot", 0, HudOverlayLayout.ArtSlotY, ap, ap);
            var artBg = UiKit.Rect(_art0, 0, 0, ap, ap, UiTheme.WoodDark, false, "Slot");
            HudArt.SetKit(artBg, _filled, UiTheme.WoodDark);
            _artIcon = HudArt.Image(_art0, "Icon", (ap - ip) * 0.5f, (ap - ip) * 0.5f - 6f, ip, ip, null, UiTheme.SlateBlue);
            _artCooldown = UiKit.Rect(_art0, 4, 4, ap - 8, ap - 8, UiTheme.WithAlpha(UiTheme.Ink, 0.72f), false, "Cooldown");
            _artCooldown.sprite = UiBackdrop.Solid; // 스프라이트가 있어야 Filled(부채꼴)가 된다
            _artCooldown.type = UImage.Type.Filled;
            _artCooldown.fillMethod = UImage.FillMethod.Radial360;
            _artCooldown.fillOrigin = (int)UImage.Origin360.Top;
            _artCooldown.fillClockwise = false;
            var readyFrame = UiFrame.Create(_art0, UiTheme.Chalk, 3);
            readyFrame.name = "ReadyFrame";
            _artReady = UiKit.Rect(_art0, -4, -4, ap + 8, ap + 8, UiTheme.WithAlpha(UiTheme.Chalk, 0.0f), false, "ReadyGlow");
            _artReady.sprite = UiBackdrop.RadialSoft;
            _artReady.rectTransform.SetAsFirstSibling();
            _artPulse = UiPulse.Attach(_artReady, 0.08f, 0.35f);
            for (int i = 0; i < MaxCostPips; i++)
            {
                _pips[i] = UiKit.Diamond(_art0, 0, 0, 10f, UiTheme.Chalk);
                HudArt.SetActive(_pips[i].transform.parent as RectTransform, false);
            }
            var keyBg = UiKit.Rect(_art0, -8, -8, 30, 30, UiTheme.Ink, false, "KeyBg");
            var cap = art.Kit("K-50", "keycap");
            if (cap.IsValid) { UiKit.SetSprite(keyBg, cap); keyBg.color = Color.white; }
            UiKit.Label(ctx, keyBg.rectTransform, UiKeyLabels.Of(ctx != null ? ctx.Names : null, Key.K), UiFontId.F16, 0, 0, 30, 30, UiTheme.Chalk, TextAnchor.MiddleCenter);
            HudArt.SetActive(_art0, false);
        }

        public void OnEvent(HudLayer layer, SimWorld w, in SimEvent e)
        {
            if (e.Slot != 0) return;
            switch (e.Type)
            {
                case SimEventType.ShotFired:
                    _litT = 0f;
                    layer.Play("ui.hud.circuit_step");
                    break;
                case SimEventType.UnderlineSlotChanged:
                    _underlineSlot = e.A;
                    break;
                case SimEventType.LoadoutChanged:
                    for (int i = 0; i < _blockIds.Length; i++) _blockIds[i] = "\u0000";
                    break;
            }
        }

        public void Refresh(HudLayer layer, SimWorld w, in HudFrame f, float dt)
        {
            var p = w != null ? w.Me : null;
            bool on = p != null && p.Active;
            HudArt.SetActive(_root, on);
            if (!on) return;
            _cellCount = HudModel.CircuitCells(p);
            _visCount = 0;
            for (int i = 0; i < _visIndex.Length; i++)
                _visIndex[i] = i < _cellCount && HudModel.CircuitBlock(p, i) != null ? _visCount++ : -1;

            // 비기 미리보기(캐릭터 둘레 표시가 지난 프레임에 읽은 값)
            ArtPreview pv = layer.Marks.Count > 0 ? layer.Marks[0].LastPreview : default;
            bool artOn = !string.IsNullOrEmpty(pv.ArtId);
            Relayout(artOn);

            // 켜짐 차례: 블록이 든 칸을 앞에서부터 하나씩
            int lit = -1;
            if (_litT >= 0f)
            {
                _litT += dt;
                float step = Mathf.Max(0.001f, _tuning.CircuitStepSeconds);
                int k = (int)(_litT / step + 1e-3f);
                int seen = 0;
                for (int i = 0; i < _cellCount; i++)
                {
                    if (HudModel.CircuitBlock(p, i) == null) continue;
                    if (seen == k) { lit = i; break; }
                    seen++;
                }
                if (lit < 0) _litT = -1f;
            }
            LitCell = lit;
            if (lit >= 0 && lit != _lastLit && layer.MotionOn) UiMotion.Pop(_cellNodes[lit], 1.12f, 0.16f);
            _lastLit = lit;

            for (int i = 0; i < _cells.Length; i++)
            {
                bool shown = _visIndex[i] >= 0;
                HudArt.SetActive(_cellNodes[i], shown);
                if (!shown) continue;
                float vx = HudOverlayLayout.CellX(_visIndex[i]) + HudOverlayLayout.CellPx * 0.5f;
                if (Mathf.Abs(_cellNodes[i].anchoredPosition.x - vx) > 0.1f)
                    _cellNodes[i].anchoredPosition = new Vector2(vx, _cellNodes[i].anchoredPosition.y);
                string block = HudModel.CircuitBlock(p, i);
                if (i == lit) _litGlow[i] = 1f;
                else _litGlow[i] = Mathf.Max(0f, _litGlow[i] - dt * 6f);
                _litFrames[i].Color = UiTheme.WithAlpha(UiTheme.Chalk, _litGlow[i] * 0.9f);
                if (_blockIds[i] != block)
                {
                    _blockIds[i] = block;
                    HudArt.SetKit(_cells[i], block != null ? _filled : _empty, block != null ? UiTheme.Wood : HudColors.CircuitOff);
                    HudArt.SetActive(_icons[i], block != null);
                    // 빈 칸은 흐린 자리 표시로만(빈 도구 막대처럼 보이지 않게).
                    if (block == null) _cells[i].color = UiTheme.WithAlpha(_cells[i].color, 0.3f);
                    if (block != null) HudArt.SetSprite(_icons[i], _art.Icon(block, (int)HudOverlayLayout.CellIconPx), UiTheme.SlateBlue);
                }
                int st = block != null ? StarsAt(p, i) : 0;
                if (_starShown[i] != st)
                {
                    _starShown[i] = st;
                    var names = _art.Ctx != null ? _art.Ctx.Names : null;
                    _stars[i].SetText(st > 0 ? (names != null ? names.Format("str.block.stars", "n", st) : st.ToString(System.Globalization.CultureInfo.InvariantCulture)) : string.Empty);
                }
                Color cc = _cells[i].sprite != null ? Color.Lerp(Color.white, UiTheme.Chalk * 1.2f, _litGlow[i] * 0.5f) : (i == lit ? HudColors.CircuitOn : _cells[i].color);
                if (block == null) cc.a = 0.3f;
                _cells[i].color = cc;
            }

            // 밑줄 칸 = 맨 끝 연결 칸(칸 번호: 코어 0, 연결 1..L). 상태에서 바로 정하므로 보조 판에서 돌아와 다시 만들어도 바로 보인다.
            _underlineSlot = UnderlineSlotOf(p);
            bool ul = _underlineSlot >= 0 && _underlineSlot < _cellCount;
            bool ulShown = ul && _visIndex[_underlineSlot] >= 0;
            HudArt.SetActive(_underline, ulShown);
            if (ulShown) UiKit.Place(_underline.rectTransform, HudOverlayLayout.CellX(_visIndex[_underlineSlot]) + 6f, HudOverlayLayout.CellPx + 3f, HudOverlayLayout.CellPx - 12f, UnderlineH);

            RefreshArt(layer, p, pv, artOn);
        }

        void Relayout(bool artOn)
        {
            if (_layoutCells == _visCount && _layoutArt == artOn) return;
            _layoutCells = _visCount;
            _layoutArt = artOn;
            float w = HudOverlayLayout.TrayWidth(_visCount, artOn);
            _root.sizeDelta = new Vector2(w, HudOverlayLayout.TrayH);
            _trayBody.rectTransform.sizeDelta = new Vector2(w, HudOverlayLayout.TrayH);
            _trayShadow.rectTransform.sizeDelta = new Vector2(w, HudOverlayLayout.TrayH);
            float ax = HudOverlayLayout.TrayPad + HudOverlayLayout.CircuitRowWidth(_visCount) + (_visCount > 0 ? HudOverlayLayout.ArtSlotGap : 0f);
            UiKit.Place(_art0, ax, HudOverlayLayout.ArtSlotY, HudOverlayLayout.ArtSlotPx, HudOverlayLayout.ArtSlotPx);
            HudArt.SetActive(_art0, artOn);
        }

        void RefreshArt(HudLayer layer, PlayerState p, in ArtPreview pv, bool artOn)
        {
            if (!artOn) { _artId = null; _wasReady = false; return; }
            if (pv.ArtId != _artId)
            {
                _artId = pv.ArtId;
                // 파이널 어빌리티(final.*)는 따로 아이콘이 없다 → 그 캐릭터 고유 비기 아이콘.
                string iconId = pv.ArtId != null && pv.ArtId.StartsWith("final.", System.StringComparison.Ordinal) && p != null && p.Def != null
                    ? p.Def.UniqueArt : pv.ArtId;
                HudArt.SetSprite(_artIcon, _art.Icon(iconId, (int)HudOverlayLayout.CellIconPx), UiTheme.SlateBlue);
                if (layer.MotionOn) UiMotion.Pop(_art0, 1.1f, 0.2f);
            }
            float cd = Mathf.Clamp01(pv.CooldownRatio);
            bool cooling = cd < 1f;
            HudArt.SetActive(_artCooldown, cooling);
            if (cooling) _artCooldown.fillAmount = 1f - cd;
            bool ready = !cooling && !pv.Fizzle;
            _artIcon.color = ready ? Color.white : UiTheme.DisabledTint;
            HudArt.SetActive(_artReady, ready);
            if (_artPulse != null) _artPulse.enabled = ready && layer.MotionOn;
            if (ready && !_wasReady && layer.MotionOn) UiMotion.Pop(_art0, 1.12f, 0.22f);
            _wasReady = ready;

            // 결정 값 마름모(칸 아래쪽 안, 가운데 맞춤)
            int cost = Mathf.Clamp(pv.Cost, 0, MaxCostPips);
            float ap = HudOverlayLayout.ArtSlotPx, step = 11f;
            float x0 = (ap - (cost - 1) * step) * 0.5f;
            int have = p != null ? p.Crystals : 0;
            for (int i = 0; i < MaxCostPips; i++)
            {
                var pipRim = _pips[i].transform.parent as RectTransform;
                bool show = i < cost;
                HudArt.SetActive(pipRim, show);
                if (!show) continue;
                pipRim.anchoredPosition = new Vector2(x0 + i * step, -(ap - 9f));
                _pips[i].color = i < have ? UiTheme.Chalk : UiTheme.WithAlpha(UiTheme.SlateBlue, 0.6f);
            }
        }

        public void Dispose() => HudArt.Destroy(_root != null ? _root.gameObject : null);
    }
}
