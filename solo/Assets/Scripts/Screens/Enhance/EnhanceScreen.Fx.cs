using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;
using UImage = UnityEngine.UI.Image;

namespace Wizard.Solo.Screens.Enhance
{
    // 그리기: 화면 값 갱신과 의식 연출(ritual 효과 그림, 번쩍·흔들림·어두워짐·그림 정지 — 08 14절 한도는 EnhanceFlashGuard가 맡는다).
    // 강도별 덧연출(10 요약서 4.1절): 2단부터 지팡이가 떠오르고, 3단부터 마법진 바깥 무늬, 4단부터 구석의 느루가 한쪽 눈을 뜬다.
    // 속성별 변주(4.2절)는 ritual 묶음의 속성 열쇠(seal_/charge_/success_column_/extinguish_/ward_ribbon_/stage_hold_<속성>)로 고른다.
    // 덧연출(이 파일): 마법진 다섯 겹 회전, 빛기둥, 빨려 드는 입자, 숨 멈춤의 정지와 미세 떨림, 성공 빛줄기·폭발·숫자 내려찍기,
    // 실패의 유리 금·연기·숫자 떨어짐. 모두 시간표 사건(cue)과 구간에 매달리고 결과·시간표를 바꾸지 않는다.
    // 전체 화면 번쩍은 여전히 시도당 1회(EnhanceFlashGuard). 그 밖의 빛은 마법진 둘레의 국소 빛이다.
    public sealed partial class EnhanceScreen
    {
        const float DigitsBaseScale = 1.6f;
        const float DigitsCy = 56f;

        // 연출 상태(표시 전용)
        float _vdt;
        float _slowMoLeft;
        float _cardAnimT = -1f, _bandAnimT = -1f;
        float _digitT = -1f, _digitPeak = 1f;
        int _digitMode;   // 0 내려찍기, 1 떨어짐, 2 흔들림
        Color _digitFlash = Color.white;
        float _coreT = -1f, _corePeak, _coreDur = 0.6f;
        Color _coreColor = Color.white;
        float _raysT = -1f, _raysDur = 1.4f, _raysPeak, _raysSize = 700f, _raysAngle;
        Color _raysColor = Color.white;
        float _crackAlpha, _crackHold;
        bool _breakSetFlip;
        SpriteAnim _crackAnim;
        float _crackDim;
        float _smokeLeft, _sparkleLeft, _emberLeft, _vineLeft;
        Color _sparkleColor = Color.white;
        float _chargeEmitAcc;
        float _heartPulse, _holdGlow;
        float _wandDropT = -1f, _wandDim, _wandDimTarget;
        float _mood, _moodTarget;   // +1 성공 빛, −1 꺼짐
        Color _moodColor = Color.white;
        float _focusDimA;
        int _offerIndex, _resultTier;
        readonly float[] _layerAngle = new float[8];
        float _sigilAngle, _glyphAngle;
        readonly List<KeyValuePair<UImage, float>> _tinted = new List<KeyValuePair<UImage, float>>();
        string _costStoneShownId;
        float _spokeT = -1f, _spokeDur = 1.4f, _spokePeak, _spokeAngle, _spokeLen = 1f;
        Color _spokeColor = Color.white;
        string _auraKey;
        Sprite[] _auraFrames;
        float _auraA;
        Sprite _commonCrack;

        /// <summary>입자용 작은 빛 점(공통 그림 bloom_glow, 없으면 코드로 만든 점).</summary>
        Sprite Dot()
        {
            var b = EnhanceCommonArt.First(EnhanceCommonArt.Combat(Ctx != null ? Ctx.Art : null, EnhanceCommonArt.BloomGlow));
            return b != null ? b : EnhanceGfx.SoftDot;
        }

        /// <summary>속성색을 입힐 그림(지팡이를 바꾸면 다시 칠한다).</summary>
        UImage Tint(UImage img, float alpha)
        {
            if (img != null) _tinted.Add(new KeyValuePair<UImage, float>(img, alpha));
            return img;
        }

        void ApplyElementTint()
        {
            foreach (var kv in _tinted) if (kv.Key != null) kv.Key.color = EnhanceFxStyle.WithA(ElementColor, kv.Value);
            if (_sigil != null)
            {
                _sigil.sprite = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupEnhance, "sigil_" + _elementSuffix);
            }
            _crackAnim = EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "screen_crack");
            // 사용자 제작 균열 덮개(알파 투명 1920×1080)가 있으면 그것을 쓴다.
            _commonCrack = EnhanceCommonArt.First(EnhanceCommonArt.Enh(Ctx != null ? Ctx.Art : null, EnhanceCommonArt.FailureCracks));
        }

        /// <summary>효과 양 배율: 연출 감도(최대 1, 줄임 0.5, 최소 0) × 이어서 강화 중이면 줄인다.</summary>
        float Amount
        {
            get
            {
                int level = EffectsLevel;
                float a = level <= 0 ? 1f : level == 1 ? 0.5f : 0f;
                if (_cont != null && _cont.Running) a *= EnhanceFxStyle.ContinuousParticleScale;
                return a;
            }
        }

        int Scaled(int n) => Mathf.Max(0, Mathf.RoundToInt(n * Amount));

        // ------------------------------------------------------------------ 갱신

        void RefreshAll()
        {
            if (_wand != null)
            {
                var prev = _prob;
                _prob = Preview();
                if (_prob != null && prev != null && prev.TotalPct != _prob.TotalPct && _model.AcceptsSetup)
                {
                    _rollFrom = CurrentRollValue();
                    _rollTo = _prob.TotalPct;
                    _rollTick = 0;
                    PlaySound(EnhanceSoundIds.ProbRoll, SoundBus.Ui);
                }
                else if (_prob != null && _rollTick < 0) _rollFrom = _rollTo = _prob.TotalPct;
            }
            RefreshTop();
            RefreshLeft();
            RefreshStage();
            RefreshRight();
            RefreshCandidates();
            RefreshBottom();
            RefreshExpected();
            RefreshTally();
            RefreshCards();
            RefreshRecordPanel();
        }

        /// <summary>버튼 안의 바뀌는 그림 자리(버튼 자신의 글자·바탕은 건드리지 않는다).</summary>
        static RectTransform Holder(UiButton b, float w, float h)
        {
            var t = b.ContentRoot.Find("Dyn") as RectTransform;
            return t != null ? t : UiKit.Node(b.ContentRoot, "Dyn", 0f, 0f, w, h);
        }

        double CurrentRollValue()
        {
            int len = Math.Max(1, EnhanceScreenTuning.Ticks(_t.ProbRollSeconds));
            if (_rollTick < 0 || _rollTick >= len) return _rollTo;
            return _rollFrom + (_rollTo - _rollFrom) * _rollTick / len;
        }

        static readonly string[] StoneIds = { Ids.Item.StoneLow, Ids.Item.StoneMid, Ids.Item.StoneHigh };
        static readonly string[] StoneShortKeys = { EnhanceNameKeys.StoneShortLow, EnhanceNameKeys.StoneShortMid, EnhanceNameKeys.StoneShortHigh };

        static string StoneShortKey(string stoneId)
        {
            for (int i = 0; i < StoneIds.Length; i++) if (StoneIds[i] == stoneId) return StoneShortKeys[i];
            return stoneId;
        }

        void RefreshTop()
        {
            var names = Ctx != null ? Ctx.Names : null;
            if (_wand == null) { _wandName.SetText(string.Empty); _stageTitle.SetText(string.Empty); return; }
            _wandName.SetText(EnhanceNameKeys.WandName(names, _character));
            _stageTitle.SetText(EnhanceNameKeys.Stage(names, _shownStage) + " " + EnhanceNameKeys.StageTitle(names, _cfg, _character, _shownStage));
            _stageTitle.Color = StageColor(_shownStage);
            _gold.SetText(N(Ctx.Profile.Currencies.Gold));
            _gold.Color = _prob != null && (_prob.Shortage & CostShortage.Gold) != 0 ? UiTheme.SlateBlue : UiTheme.Gold;
            for (int i = 0; i < 3; i++) _stoneLabels[i].SetText(StoneText(StoneIds[i], StoneShortKeys[i]));
            bool canSwitch = Ctx.Wands != null && Ctx.Wands.List(_character, WandSort.StageDesc).Count > 1;
            _prevWand.gameObject.SetActive(canSwitch);
            _nextWand.gameObject.SetActive(canSwitch);
        }

        /// <summary>단계 숫자 색: +0~6 분필, +7~9 금, +10~12 흰 금, +13 금(등급 색과 같다).</summary>
        Color StageColor(int stage)
        {
            int tier = EnhanceFxStyle.Tier(stage);
            if (stage >= _cfg.MaxStage) return UiTheme.Gold;
            return tier >= 2 ? EnhanceFxStyle.TierColor(tier, ElementColor) : UiTheme.Chalk;
        }

        string StoneText(string id, string nameKey)
        {
            string s = F("str.enhance.stone_count", "name", T(nameKey), "n", N(Qty(id)));
            int bound = Bound(id);
            return bound > 0 ? s + " " + F(EnhanceNameKeys.FormatBound, "n", bound) : s;
        }

        void RefreshLeft()
        {
            if (_wand == null) return;
            int baseLinks = _wand.Loadout != null && _wand.Loadout.Links != null ? _wand.Loadout.Links.Count : 0;
            _slotsLabel.SetText(T(EnhanceNameKeys.Slots) + " " + F(EnhanceNameKeys.FormatRatio, "a", baseLinks + _wand.SlotsExtra, "b", baseLinks + _cfg.SlotExtraMax));
            var circuit = new List<string>();
            if (_wand.Loadout != null)
            {
                circuit.Add(_wand.Loadout.Core != null ? T(_wand.Loadout.Core) : T(EnhanceNameKeys.CommonNone));
                foreach (var l in _wand.Loadout.AllLinks()) circuit.Add(l != null ? T(l) : T("str.common.empty"));
            }
            _circuit.SetText(string.Join(T(UiNameKeys.HintSep), circuit));
            _extLabel.SetText(F(EnhanceNameKeys.ExtSlots, "n", _wand.SlotsExtra, "max", _cfg.SlotExtraMax));
            int cap = Ctx.Content != null ? _cfg.MutationCapFor(Ids.Char.Of(_character)) : _cfg.MutationCapDefault;
            var cd = Ctx.Content != null ? Ctx.Content.Character(Ids.Char.Of(_character)) : null;
            if (cd != null && cd.MaxMutations > 0) cap = cd.MaxMutations;
            _mutLabel.SetText(T(EnhanceNameKeys.Mutations) + " " + F(EnhanceNameKeys.FormatRatio, "a", _wand.Mutations.Count, "b", cap));
            for (int i = 0; i < _mutationCells.Count; i++)
            {
                var b = _mutationCells[i];
                bool show = i < cap;
                b.gameObject.SetActive(show);
                if (!show) continue;
                var holder = Holder(b, 48f, 48f);
                EnhanceUi.Clear(holder);
                bool has = i < _wand.Mutations.Count;
                // 파일 이름 규칙(09 6.1절): k-80_mutation_slot_filled_48x48 → 변형 "mutation_slot_filled", 상태 normal.
                var sp = UiKit.KitSprite(Ctx, "K-80", has ? "mutation_slot_filled" : "mutation_slot_empty", null);
                if (sp.IsValid) UiKit.Image(holder, sp.Sprite, 0f, 0f, 48f, 48f);
                else UiKit.Rect(holder, 0f, 0f, 48f, 48f, has ? UiTheme.WoodLight : UiTheme.Wood);
                if (has)
                {
                    string mid = _wand.Mutations[i].Id;
                    UiKit.Icon(Ctx, holder, mid, 32, 8f, 8f);
                    UiKit.AttachTooltip(b.gameObject, () => T(mid));
                }
                b.Picked = has && i == _selectedMutation;
            }
            _bestLabel.SetText(F(EnhanceNameKeys.BestStage, "n", _wand.RecordPeak));
            _floorLabel.SetText(F(EnhanceNameKeys.FloorStage, "n", ProbabilityDisplayModel.DisplayFloor(_cfg, _wand.FloorPeak)));
            int scars = EnhanceResultPlan.ScarCount(_wand.Stage, _wand.RecordPeak, _t.ScarMax);
            _traceToggle.Value = _wand.ShowScars;
            _traceNote.gameObject.SetActive(scars > 0 && !_wand.ShowScars);
            bool engraveOk = Ctx.Enhance != null && Ctx.Enhance.PreviewCarve(_wandId).Blocked != ErrorCode.NotEligible;
            if (engraveOk) _engraveButton.SetEnabled(); else _engraveButton.SetDisabled(EnhanceNameKeys.ErrorNotEligible);
            if (_wand.Mutations.Count == 0) _removeButton.SetDisabled(EnhanceNameKeys.RemoveNone);
            else if (_wand.PendingOffer != null) _removeButton.SetDisabled(EnhanceNameKeys.ErrorKey(ErrorCode.MutationPending));
            else _removeButton.SetEnabled();
            Sprite portrait = null;
            if (Ctx.Art != null) { try { portrait = Ctx.Art.CharacterPortrait(Ids.Char.Of(_character), 0); } catch (Exception) { portrait = null; } }
            _portrait.sprite = portrait;
            _portrait.preserveAspect = true;
            _portrait.color = portrait != null ? Color.white : UiTheme.WoodLight;
        }

        void SetupStageArt()
        {
            _sealBase.sprite = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "seal_base");
            _sealElement.sprite = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "seal_" + _elementSuffix);
            _outerGlyph.sprite = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "outer_glyph_" + _elementSuffix);
            _sealBase.color = _sealBase.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            _sealElement.color = _sealElement.sprite != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            _outerGlyph.color = new Color(1f, 1f, 1f, 0f);
            ApplyElementTint();
            ResetVisuals();
        }

        void RefreshStage()
        {
            if (_wand == null) return;
            var names = Ctx.Names;
            Sprite ws = null;
            if (Ctx.Art != null) { try { ws = Ctx.Art.WandInspection(Ids.Char.Of(_character), _shownStage); } catch (Exception) { ws = null; } }
            _wandImage.sprite = ws;
            _wandImage.preserveAspect = ws != null;
            _wandImage.color = ws != null ? Color.white : UiTheme.WoodLight;
            _nickname.SetText(EnhanceNameKeys.Nickname(names, _character, _shownStage));
            RefreshCharIdle();
            _stageDigits.SetText(EnhanceNameKeys.Stage(names, _shownStage));
            _stageDigits.Color = StageColor(_shownStage);
            RefreshMarkers();
            for (int i = 0; i < _sacrificeSlots.Count; i++)
            {
                var b = _sacrificeSlots[i];
                var cr = Holder(b, SlotPx, SlotPx);
                EnhanceUi.Clear(cr);
                bool filled = i < _sacrifices.Count;
                var mark = b.ContentRoot.Find("EmptyMark");
                if (mark != null) mark.gameObject.SetActive(!filled);
                if (filled)
                {
                    string id = _sacrifices[i];
                    var g = EnhanceGfx.Img(cr, EnhanceGfx.Glow, SlotPx * 0.5f, SlotPx * 0.5f, SlotPx * 1.6f, SlotPx * 1.6f, EnhanceFxStyle.WithA(ElementColor, 0.35f), "FilledGlow");
                    EnhanceGfx.Additive(Ctx, g);
                    UiKit.Icon(Ctx, cr, id, 48, 4f, 4f);
                    var def = Ctx.Content != null ? Ctx.Content.Block(id) : null;
                    if (def != null && def.Element == _element) UiKit.Rect(cr, SlotPx - 12f, 4f, 8f, 8f, ElementColor);
                }
            }
            if (_prob != null && _prob.CharmSlotFolded)
            {
                _charmSlot.SetDisabled(EnhanceNameKeys.CharmNotNeeded);
                _charmNote.SetText(T(EnhanceNameKeys.CharmNotNeeded));
            }
            else
            {
                _charmSlot.SetEnabled();
                _charmNote.SetText(_charmSet ? T(EnhanceNameKeys.Charm) : F("str.enhance.stone_count", "name", T(Ids.Item.Charm), "n", CharmCount()));
            }
            _charmSlot.Picked = _charmSet;
            var ember = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupEnhance, "warm_ember");
            for (int i = 0; i < _auraCells.Count; i++)
            {
                bool lit = _wand.Aura > i;
                var img = _auraCells[i];
                if (lit && ember != null) { img.sprite = ember; img.color = Color.white; }
                else
                {
                    img.sprite = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "artisan_slot", lit ? 1 : 0);
                    img.color = img.sprite != null ? Color.white : (lit ? UiTheme.Amber : UiTheme.Wood);
                }
            }
        }

        /// <summary>흔적(최대 2)·변이 표식·확장 표식(09 3절 manifest 점, 10 요약서 4절). 그림 위 덧그림은 단순 도형으로.</summary>
        void RefreshMarkers()
        {
            EnhanceUi.Clear(_markers);
            if (_wand == null) return;
            // manifest 점은 32×64 손 칸 기준이라 큰 그림(192 → 384)에는 쓰지 않고 정해진 자리(오른쪽 위부터)에 둔다.
            int scars = EnhanceResultPlan.ScarCount(_wand.Stage, _wand.RecordPeak, _t.ScarMax);
            var frag = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "trace_fragment");
            for (int i = 0; i < scars; i++)
            {
                if (frag != null) UiKit.Image(_markers, frag, WandPx - 48f - i * 28f, 24f, 24f, 24f);
                else UiKit.Rect(_markers, WandPx - 48f - i * 28f, 24f, 24f, 24f, UiTheme.SlateBlue);
            }
            if (scars > 0 && !_wand.ShowScars)
            {
                var hid = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupEnhance, "trace_hidden");
                if (hid != null) UiKit.Image(_markers, hid, WandPx - 48f - scars * 28f, 28f, 16f, 16f);
            }
            for (int i = 0; i < _wand.Mutations.Count; i++)
                UiKit.Icon(Ctx, _markers, _wand.Mutations[i].Id, 32, 8f, WandPx - 40f - i * 36f);
            for (int i = 0; i < _wand.SlotsExtra; i++)
                UiKit.Rect(_markers, WandPx * 0.5f - 24f + i * 18f, WandPx - 20f, 12f, 12f, UiTheme.Gold);
        }

        void RefreshRight()
        {
            var m = _prob;
            bool show = m != null && !m.AtMax;
            _capTag.gameObject.SetActive(show && m.Capped);
            if (m == null) return;
            if (m.AtMax)
            {
                _bigProb.SetText(string.Empty);
                _hint.SetText(T(EnhanceNameKeys.ErrorMaxStage));
                foreach (var b in _band) UiKit.Place(b.rectTransform, 0f, 0f, 0f, 24f);
                _lineBase.SetText(string.Empty); _lineSac.SetText(string.Empty); _lineSame.SetText(string.Empty); _lineAura.SetText(string.Empty);
                return;
            }
            // 큰 확률 숫자는 화면을 연 첫 프레임부터 실제 값(굴림 중이면 굴림 값). 매 프레임 그리기도 같은 값을 쓴다(08 10.3절).
            ShowBigProb(CurrentRollValue());
            _lineBase.SetText(T(EnhanceNameKeys.ProbBase) + " " + F(EnhanceNameKeys.FormatPercent, "n", m.BasePct));
            _lineSac.SetText(T(EnhanceNameKeys.ProbSacrifice) + " +" + m.SacrificeLinePct);
            _lineSame.SetText(T(EnhanceNameKeys.ProbSameElement) + " +" + m.SameElementPct);
            _lineAura.SetText(T(EnhanceNameKeys.ProbAura) + " +" + m.AuraPct);
            _lineSac.Color = m.SacrificeLinePct > 0 ? UiTheme.Good : UiTheme.SlateBlue;
            _lineSame.Color = m.SameElementPct > 0 ? UiTheme.Good : UiTheme.SlateBlue;
            _lineAura.Color = m.AuraPct > 0 ? UiTheme.Amber : UiTheme.SlateBlue;
            int[] bp = { m.Band.GreatBp, m.Band.SuccessBp, m.Band.MutationBp, m.Band.FailBp };
            float x = 0f;
            for (int i = 0; i < 4; i++)
            {
                float w = 400f * bp[i] / 10000f;
                UiKit.Place(_band[i].rectTransform, x, 0f, w, 24f);
                x += w;
            }
            _hint.SetText(m.HintText(Ctx != null ? Ctx.Names : null));
            Color hc = m.Hint == EnhanceHint.DropTo ? UiTheme.Amber : m.Hint == EnhanceHint.CharmProtects ? EnhanceFxStyle.WardTeal : UiTheme.Good;
            _hint.Color = hc;
            _hintBox.color = EnhanceFxStyle.WithA(hc, 0.13f);
        }

        Color ShowBigProb(double v)
        {
            int n = (int)Math.Round(v);
            string text = F(EnhanceNameKeys.FormatPercent, "n", n);
            if (_bigProb.FullText != text) _bigProb.SetText(text);
            var pc = EnhanceFxStyle.ProbColor(n);
            _bigProb.Color = pc;
            return pc;
        }

        string BandTooltip(int k)
        {
            if (_prob == null) return string.Empty;
            int[] bp = { _prob.Band.GreatBp, _prob.Band.SuccessBp, _prob.Band.MutationBp, _prob.Band.FailBp };
            string[] keys = { "str.enhance.result.great_success", "str.enhance.result.success", "str.enhance.result.mutation", "str.enhance.result.fail_drop" };
            return T(keys[k]) + " " + F(EnhanceNameKeys.FormatPercent, "n", F1(bp[k] / 100.0));
        }

        void RefreshCandidates()
        {
            _candidates.Clear();
            var p = Ctx != null ? Ctx.Profile : null;
            if (p != null && Ctx.Content != null)
                foreach (var b in Ctx.Content.Blocks)
                    if (p.Items.TryGetValue(b.Id, out var st) && st != null && st.Qty - st.QtySocketed > 0) _candidates.Add(b);
            int pages = Math.Max(1, (_candidates.Count + CandidatesPerRow - 1) / CandidatesPerRow);
            if (_candPage >= pages) _candPage = pages - 1;
            _candPrev.gameObject.SetActive(pages > 1);
            _candNext.gameObject.SetActive(pages > 1);
            int projTotal = ProjectileTotal(), placedProj = PlacedProjectiles();
            for (int i = 0; i < _candidateCells.Count; i++)
            {
                var cell = _candidateCells[i];
                var cr = Holder(cell, Cell, Cell);
                EnhanceUi.Clear(cr);
                var b = CandidateAt(i);
                cell.gameObject.SetActive(b != null);
                if (b == null) continue;
                int free = FreeOf(b.Id);
                var slot = UiKit.KitSprite(Ctx, "K-60", free > 0 ? "item_slot_filled" : "item_slot_empty", null);
                if (slot.IsValid) UiKit.Image(cr, slot.Sprite, 0f, 0f, Cell, Cell);
                else EnhanceGfx.Panel(cr, 0f, 0f, Cell, Cell, EnhanceFxStyle.WithA(UiTheme.WoodDark, 0.9f), false, "CellFill");
                UiKit.Icon(Ctx, cr, b.Id, 48, 8f, 8f);
                UiKit.Label(Ctx, cr, N(free), UiFontId.F12, 0f, Cell - 16f, Cell - 4f, 16f, UiTheme.Chalk, TextAnchor.MiddleRight, false, true);
                if (b.Element == _element) UiKit.Rect(cr, 4f, 4f, 8f, 8f, ElementColor);
                if (ProbabilityDisplayModel.CannotPlace(b, projTotal, placedProj)) cell.SetDisabled(EnhanceNameKeys.CannotPlace);
                else if (free <= 0 || _sacrifices.Count >= MaxSacrifice) cell.SetDisabled(null);
                else cell.SetEnabled();
            }
        }

        string CandidateTooltip(int cell)
        {
            var b = CandidateAt(cell);
            if (b == null) return string.Empty;
            if (CannotPlace(b)) return T(EnhanceNameKeys.LastProjectileTip);
            return T(b.Id);
        }

        void RefreshBottom()
        {
            var m = _prob;
            if (m == null || m.AtMax)
            {
                _costGold.SetText(string.Empty);
                _costStone.SetText(string.Empty);
                _costGoldChip.gameObject.SetActive(false);
                _costStoneChip.gameObject.SetActive(false);
                _cost.gameObject.SetActive(false);
                _dropTag.gameObject.SetActive(false);
                _enhanceButton.SetDisabled(EnhanceNameKeys.ErrorMaxStage);
                return;
            }
            _cost.gameObject.SetActive(true);
            _costGoldChip.gameObject.SetActive(true);
            _costStoneChip.gameObject.SetActive(true);
            _costGold.SetText(N(m.CostGold));
            _costGold.Color = (m.Shortage & CostShortage.Gold) != 0 ? UiTheme.SlateBlue : UiTheme.Gold;
            _costStone.SetText(F("str.enhance.stone_count", "name", T(StoneShortKey(m.StoneId)), "n", m.StoneCount));
            _costStone.Color = (m.Shortage & CostShortage.Stone) != 0 ? UiTheme.SlateBlue : UiTheme.Chalk;
            if (_costStoneShownId != m.StoneId)
            {
                _costStoneShownId = m.StoneId;
                EnhanceUi.Clear(_costStoneIconRoot);
                if (m.StoneId != null) UiKit.Icon(Ctx, _costStoneIconRoot, m.StoneId, 32, 0f, 0f);
            }
            _dropTag.gameObject.SetActive(m.DropPossibleTag);
            if (m.Blocked == ErrorCode.None) _enhanceButton.SetEnabled();
            else _enhanceButton.SetDisabled(EnhanceNameKeys.ErrorKey(m.Blocked));
        }

        void RefreshExpected()
        {
            if (_wand == null || Ctx.Enhance == null || _wand.Stage >= _cfg.MaxStage)
            {
                _expectTarget.SetText(string.Empty);
                _expectTries.SetText(string.Empty); _expectGold.SetText(string.Empty); _expectStone.SetText(string.Empty); _expectOwned.SetText(string.Empty);
                foreach (var c in _expectStoneCells) c.SetText(string.Empty);
                return;
            }
            if (_targetStage <= _wand.Stage) _targetStage = NextMilestone(_wand.Stage);
            _expectTarget.SetText(T(EnhanceNameKeys.ExpectTarget) + " " + EnhanceNameKeys.Stage(Ctx.Names, _targetStage));
            var rows = Ctx.Enhance.ExpectedTable(_wandId, _targetStage);
            ExpectedRow row = default;
            bool found = false;
            foreach (var r in rows) if (r.TargetStage == _targetStage) { row = r; found = true; }
            if (!found) { _expectTries.SetText(string.Empty); _expectGold.SetText(string.Empty); _expectStone.SetText(string.Empty); }
            else
            {
                _expectTries.SetText(T(EnhanceNameKeys.ExpectAvgTries) + " " + F2(row.ExpectedAttempts));
                _expectGold.SetText(T(EnhanceNameKeys.ExpectAvgGold) + " " + N((long)Math.Round(row.ExpectedGold)));
                _expectStone.SetText(T(EnhanceNameKeys.ExpectAvgStone));
                double[] stones = { row.ExpectedStonesLow, row.ExpectedStonesMid, row.ExpectedStonesHigh };
                for (int i = 0; i < _expectStoneCells.Length; i++)
                    _expectStoneCells[i].SetText(F("str.enhance.stone_count", "name", T(StoneShortKeys[i]), "n", F1(stones[i])));
            }
            if (!found) foreach (var c in _expectStoneCells) c.SetText(string.Empty);
            int owned = Ctx.Enhance != null ? Ctx.Enhance.AttemptsWithOwned(_wand.Id, _targetStage) : 0;
            _expectOwned.SetText(F(EnhanceNameKeys.ExpectWithOwned, "n", N(owned)));
            if (_targetStage <= _wand.Stage + 1) _targetPrev.SetDisabled(null); else _targetPrev.SetEnabled();
            if (_targetStage >= _cfg.MaxStage) _targetNext.SetDisabled(null); else _targetNext.SetEnabled();
        }

        void RefreshTally()
        {
            bool show = _cont.Running || (_cont.Armed && _cont.Attempts > 0);
            _tally.gameObject.SetActive(show);
            if (show) _tally.SetText(_cont.TallyText(Ctx != null ? Ctx.Names : null));
        }

        void RefreshCards()
        {
            var phase = _model.Phase;
            bool card = phase == EnhancePhase.ResultCard && _model.Shown != null;
            _resultCard.gameObject.SetActive(card);
            if (card) BuildResultCard();
            bool mut = phase == EnhancePhase.MutationChoice && _model.Result != null;
            _mutationCard.gameObject.SetActive(mut);
            if (mut) BuildMutationCard();
            if (phase != EnhancePhase.Milestone && phase != EnhancePhase.Plus13) _titleBandRoot.gameObject.SetActive(false);
        }

        /// <summary>결과 종류의 색(카드 테·제목·빛). 실패 계열은 보라·회색·청록(위험색 없음).</summary>
        Color OutcomeColor(EnhanceResult r)
        {
            if (r == null) return UiTheme.Chalk;
            switch (r.Outcome)
            {
                case EnhanceOutcome.GreatSuccess: return UiTheme.Gold;
                case EnhanceOutcome.Success: return EnhanceFxStyle.TierColor(EnhanceFxStyle.Tier(r.StageAfter), ElementColor);
                case EnhanceOutcome.FailDrop: return EnhanceFxStyle.FailViolet;
                case EnhanceOutcome.FailCharmBlocked: return EnhanceFxStyle.WardTeal;
                case EnhanceOutcome.FailNoDrop: return EnhanceFxStyle.FailAsh;
                default: return UiTheme.Mutation;
            }
        }

        void BuildResultCard()
        {
            var names = Ctx.Names;
            EnhanceUi.Clear(_resultLines);
            var shown = _model.Shown;
            var lines = EnhanceResultCardModel.Build(names, _cfg, _model.Result, shown, Ctx.Kind, _t.RollDecimals, _cfg.SlotExtraMax);
            float y = 0f;
            bool gold = shown.Outcome == EnhanceOutcome.GreatSuccess || shown.StageAfter >= _cfg.MaxStage;
            Color oc = gold ? UiTheme.Gold : OutcomeColor(shown);
            _resultCardEdge.color = EnhanceFxStyle.WithA(oc, 0.95f);
            _resultCardGlow.color = EnhanceFxStyle.WithA(oc, gold ? 0.45f : 0.25f);
            _resultCardBg.color = EnhanceFxStyle.WithA(UiTheme.Ink, 0.94f);
            bool good = shown.Outcome == EnhanceOutcome.Success || shown.Outcome == EnhanceOutcome.GreatSuccess || shown.Outcome == EnhanceOutcome.MutationAccepted;
            var banner = EnhanceCommonArt.First(EnhanceCommonArt.Enh(Ctx != null ? Ctx.Art : null, good ? EnhanceCommonArt.BannerSuccess : EnhanceCommonArt.BannerFailure));
            _resultBanner.sprite = banner;
            _resultBanner.color = banner != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            bool trace = false;
            foreach (var l in lines)
            {
                UiFontId size = UiFontId.F16;
                Color col = UiTheme.Chalk;
                bool outline = false;
                if (l.Kind == CardLineKind.Title) { size = UiFontId.F48; col = oc; outline = true; }
                else if (l.Kind == CardLineKind.Stage) { size = UiFontId.F32; col = UiTheme.Chalk; outline = true; }
                else if (l.Kind == CardLineKind.Roll) col = UiTheme.SlateBlue;
                else if (l.Kind == CardLineKind.Slot) col = UiTheme.Gold;
                else if (l.Kind == CardLineKind.Aura) col = UiTheme.Amber;
                else if (l.Kind == CardLineKind.Charm) col = EnhanceFxStyle.WardTeal;
                float h = UiTheme.LineHeight(size);
                var anchor = l.Kind == CardLineKind.Title || l.Kind == CardLineKind.Stage ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
                if (l.Kind == CardLineKind.Title)
                {
                    // 제목은 결과 띠(카드 위 12..132) 한가운데에.
                    UiKit.Label(Ctx, _resultLines, l.Text, size, 0f, -28f, 536f, 120f, col, anchor, false, outline);
                    y = 100f;
                    continue;
                }
                UiKit.Label(Ctx, _resultLines, l.Text, size, 0f, y, 536f, h, col, anchor, false, outline);
                y += h + (l.Kind == CardLineKind.Stage ? 14f : 4f);
                if (l.Kind == CardLineKind.Trace) trace = true;
            }
            _traceCardButton.gameObject.SetActive(trace);
            if (trace) _traceCardButton.SetText(T(EnhanceNameKeys.TracePublic) + " " + T(_wand != null && _wand.ShowScars ? EnhanceNameKeys.CommonOn : EnhanceNameKeys.CommonOff));
        }

        void BuildMutationCard()
        {
            var r = _model.Result;
            var names = Ctx.Names;
            string mid = r.MutationCandidate;
            _mutName.SetText(mid != null ? T(mid) : string.Empty);
            var icon = UiKit.IconSprite(Ctx, mid, 64);
            _mutIcon.sprite = icon;
            _mutIcon.color = icon != null ? Color.white : UiTheme.Mutation;
            string gain = mid != null ? EnhanceNameKeys.IfPresent(names, EnhanceNameKeys.MutationGainPrefix + mid) : null;
            string cost = mid != null ? EnhanceNameKeys.IfPresent(names, EnhanceNameKeys.MutationCostPrefix + mid) : null;
            _mutGain.gameObject.SetActive(gain != null);
            _mutGain.SetText(gain ?? string.Empty);
            _mutCost.gameObject.SetActive(cost != null);
            _mutCost.SetText(cost ?? string.Empty);
            string push = EnhanceResultCardModel.PushOutLine(names, r.MutationPushedOut);
            _mutPush.gameObject.SetActive(push != null);
            _mutPush.SetText(push ?? string.Empty);
            if (_cardFocus.Current == null) _cardFocus.SetCursor(_acceptButton, false);
        }

        // ------------------------------------------------------------------ 연출 상태

        /// <summary>지팡이를 바꾸거나 처음 열 때: 남은 연출을 비운다.</summary>
        void ResetVisuals()
        {
            _particles?.Clear();
            _slowMoLeft = 0f;
            _digitT = -1f; _raysT = -1f; _cardAnimT = -1f; _bandAnimT = -1f;
            StopImpactLight();
            _crackAlpha = 0f; _crackHold = 0f; _crackDim = 0f;
            _smokeLeft = _sparkleLeft = _emberLeft = _vineLeft = 0f;
            _wandDropT = -1f; _wandDim = _wandDimTarget = 0f;
            _mood = _moodTarget = 0f;
            _heartPulse = _holdGlow = 0f;
            _offerIndex = 0;
        }

        /// <summary>시간표 효과(그림 재생, 어두워짐, 번쩍, 정지)를 멈춘다. 입자·빛 번짐은 스스로 사라지게 둔다(결과 팝이 카드와 겹쳐 남는다).</summary>
        void ResetRitualVisuals()
        {
            _fx?.StopAll();
            _darkenTicks = _borderTicks = _hitstopTicks = 0;
            _flashStart = -1;
        }

        /// <summary>새 시도: 앞 시도의 남은 연출을 거둔다(이어서 강화에서도 결과 팝은 남는다 — 다음 시도 전까지).</summary>
        void BeginAttemptVisuals()
        {
            _offerIndex = 0;
            _crackAlpha = 0f; _crackHold = 0f; _crackDim = 0f;
            _wandDropT = -1f; _wandDimTarget = 0f;
            _moodTarget = 0f;
            _smokeLeft = _vineLeft = 0f;
            _cardAnimT = -1f;
            var r = _model.Result;
            _resultTier = r != null ? EnhanceFxStyle.Tier(r.StageBefore + 1) : 0;
        }

        /// <summary>구간이 바뀔 때(EnhanceScreen.OnPhaseChanged에서 부른다).</summary>
        void OnVisualPhase(EnhancePhase from, EnhancePhase to)
        {
            SyncLightObjects();
            if (to == EnhancePhase.ResultCard) _cardAnimT = 0f;
            if (to == EnhancePhase.Idle) { _moodTarget = 0f; _wandDimTarget = 0f; }
            if (to == EnhancePhase.ResultCard && _model.Shown != null)
            {
                var o = _model.Shown.Outcome;
                bool fail = o == EnhanceOutcome.FailDrop || o == EnhanceOutcome.FailNoDrop || o == EnhanceOutcome.FailCharmBlocked;
                _wandDimTarget = fail ? 0.25f : 0f;
            }
        }

        void Slow(int tier)
        {
            if (!HitstopOn || EffectsLevel >= 2) return;
            float s = EnhanceFxStyle.SlowMoSeconds[Mathf.Clamp(tier, 0, EnhanceFxStyle.SlowMoSeconds.Length - 1)];
            if (_cont != null && _cont.Running) s *= 0.5f;
            _slowMoLeft = Mathf.Max(_slowMoLeft, s);
        }

        void CoreFlash(Color c, float peak, float dur)
        {
            if (Amount <= 0f) return;
            _coreColor = c; _corePeak = peak * Mathf.Max(0.5f, Amount); _coreDur = dur; _coreT = 0f;
            SetActiveIfChanged(_coreFlash, true);
        }

        void RaysBurst(Color c, float size, float peak, float dur)
        {
            if (Amount <= 0f) return;
            _raysColor = c; _raysSize = size; _raysPeak = peak; _raysDur = dur; _raysT = 0f;
        }

        void DigitSlam(Color flash, float peak)
        {
            _digitMode = 0; _digitT = 0f; _digitPeak = peak; _digitFlash = flash;
            if (_digitsGlow != null) _digitsGlow.color = EnhanceFxStyle.WithA(flash, 0.9f);
        }

        void DigitDrop() { _digitMode = 1; _digitT = 0f; _digitFlash = EnhanceFxStyle.FailAsh; }
        void DigitWobble() { _digitMode = 2; _digitT = 0f; _digitFlash = EnhanceFxStyle.FailAsh; }

        void Shock(Vector2 at, float from, float to, float life, Color c)
        {
            if (Amount <= 0f || _particles == null) return;
            var p = _particles.Spawn(EnhanceGfx.RingThin, at, Vector2.zero, life, from, to, c, EnhanceFxStyle.WithA(c, 0f));
            if (p != null) p.Drag = 0f;
        }

        static Vector2 DigitsScreen => new Vector2(SideW + RitualCx, PanelTop + DigitsCy);

        Sprite Mote()
        {
            var a = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, "success_mote_" + _elementSuffix, 0);
            return a != null ? a : EnhanceGfx.SoftDot;
        }

        /// <summary>성공 터짐: 등급이 오를수록 더 크고 많고 느리게.</summary>
        void SuccessPop(int tier, bool great)
        {
            var c = RitualScreen;
            Color main = great ? UiTheme.Gold : EnhanceFxStyle.TierColor(tier, ElementColor);
            Color acc = great ? Color.Lerp(UiTheme.Gold, Color.white, 0.6f) : EnhanceFxStyle.TierAccent(tier, ElementColor);
            int t = Mathf.Clamp(tier + (great ? 1 : 0), 0, 4);
            CoreFlash(Color.Lerp(Color.white, main, 0.25f), 1f, 0.55f + 0.1f * t);
            RaysBurst(main, EnhanceFxStyle.RaysSize[t], 0.85f, 1.2f + 0.25f * t);
            if (Amount > 0f)
            {
                // 공통 그림: 성공 폭발(256 → 3배·4배)과 충격파 고리, 방사형 빛살.
                float bs = t >= 3 ? 1024f : 768f;
                _fx.Play(EnhanceCommonArt.Anim(EnhanceCommonArt.Enh(Ctx != null ? Ctx.Art : null, EnhanceCommonArt.SuccessBurst), EnhanceCommonArt.BurstFps, false, "success_burst"),
                    RitualCx - bs * 0.5f, RitualCy - bs * 0.5f, bs, bs, Color.white);
                _fx.Play(EnhanceCommonArt.Anim(EnhanceCommonArt.Combat(Ctx != null ? Ctx.Art : null, EnhanceCommonArt.Shockwave), EnhanceCommonArt.ShockFps, false, "shockwave"),
                    RitualCx - 320f, RitualCy - 320f, 640f, 640f, Color.Lerp(main, Color.white, 0.3f));
                _spokeT = 0f; _spokeColor = main; _spokePeak = 0.6f + 0.1f * t; _spokeDur = 1.1f + 0.3f * t; _spokeLen = 0.9f + 0.25f * t;
            }
            _moodTarget = 1f; _moodColor = main;
            _wandDimTarget = 0f;
            if (_particles != null && Amount > 0f)
            {
                int n = Scaled(EnhanceFxStyle.BurstParticles[t]);
                _particles.Burst(Dot(), c, n, 300f, 900f + 150f * t, 1.1f, 22f, main, acc, 420f, 1.2f, true);
                _particles.Burst(EnhanceGfx.Sparkle, c, Scaled(14 + 8 * t), 150f, 600f, 1.3f, 40f, Color.white, acc, 160f, 1.6f);
                _particles.Burst(Mote(), c, Scaled(18 + 6 * t), 200f, 500f, 0.9f, 28f, main, Color.white, 0f, 2f);
                Shock(c, 120f, 900f + 200f * t, 0.6f, EnhanceFxStyle.WithA(main, 0.9f));
                if (t >= 2) Shock(c, 80f, 1400f, 0.9f, EnhanceFxStyle.WithA(acc, 0.6f));
            }
            if (t >= 2) { _sparkleLeft = 0.8f + 0.4f * t; _sparkleColor = main; }
            Slow(t);
        }

        void FailSmoke(float seconds, bool shards)
        {
            _smokeLeft = Mathf.Max(_smokeLeft, seconds);
            _moodTarget = -1f;
            _wandDimTarget = 0.7f;
            if (shards && _particles != null && Amount > 0f)
                _particles.Burst(EnhanceGfx.Shard, RitualScreen, Scaled(26), 120f, 420f, 1.4f, 26f, EnhanceFxStyle.CrackTint, EnhanceFxStyle.FailViolet, 700f, 0.6f);
        }

        // ------------------------------------------------------------------ 연출 사건

        void StartCharge()
        {
            if (_model.Style != RitualStyle.Full && _model.Style != RitualStyle.Fast) return;
            var anim = EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "charge_" + _elementSuffix);
            if (anim != null) _fx.Play(anim, RitualCx - 48f, RitualCy - WandPx * 0.5f - 24f, 96f, 96f, Color.white, 0f, true, "charge");
            Shock(RitualScreen, 200f, 700f, 0.5f, EnhanceFxStyle.WithA(ElementColor, 0.7f));
        }

        void SetMaster(string key)
        {
            if (_master == null) return;
            var sp = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupEnhance, key, key == "master_awake" ? 2 : 0);
            if (sp != null) _master.sprite = sp;
        }

        void StartCostShake()
        {
            _costShakeTicks = EnhanceScreenTuning.Ticks(_t.CostShakeSeconds);
            _shake = _prob != null ? _prob.Shortage : CostShortage.Gold;
            PlaySound(EnhanceSoundIds.CostShake, SoundBus.Ui);
        }

        Color CueColor(string c)
        {
            if (c == EnhanceFx.ColorGold) return UiTheme.Gold;
            if (c == EnhanceFx.ColorWhiteGold) return Color.Lerp(UiTheme.Gold, Color.white, (float)_t.WhiteGoldMix);
            return ElementColor;
        }

        void ApplyCue(EnhanceCue c)
        {
            if (c.HasSound) PlaySound(c.EventId, c.Bus, c.Pitch);
            ApplySoundVisual(c.EventId);
            if (string.IsNullOrEmpty(c.Fx)) return;
            int level = EffectsLevel;
            bool fast = _model.Style == RitualStyle.Fast;
            var r = _model.Shown;
            float cx = RitualCx, cy = RitualCy;
            switch (c.Fx)
            {
                case EnhanceFx.Pay:
                    if (_particles != null && Amount > 0f)
                    {
                        for (int i = 0; i < Scaled(12); i++)
                        {
                            var p = _particles.Spawn(EnhanceGfx.Sparkle, new Vector2(1200f + UnityEngine.Random.Range(-60f, 60f), 40f),
                                new Vector2(UnityEngine.Random.Range(-200f, 200f), UnityEngine.Random.Range(50f, 250f)), 0.9f, 22f, 10f,
                                UiTheme.Gold, EnhanceFxStyle.WithA(UiTheme.Gold, 0.6f));
                            if (p == null) break;
                            p.Target = RitualScreen; p.Seek = 6f; p.Drag = 1.5f;
                        }
                    }
                    break;
                case EnhanceFx.Offer:
                {
                    var from = new Vector2(784f + 72f * _offerIndex, 824f);
                    _offerIndex++;
                    if (_particles != null && Amount > 0f)
                    {
                        for (int i = 0; i < Scaled(16); i++)
                        {
                            var p = _particles.Spawn(Dot(), from + UnityEngine.Random.insideUnitCircle * 20f,
                                new Vector2(UnityEngine.Random.Range(-180f, 180f), UnityEngine.Random.Range(-300f, -80f)), 0.7f, 18f, 6f,
                                Color.Lerp(ElementColor, Color.white, 0.4f), ElementColor);
                            if (p == null) break;
                            p.Target = RitualScreen; p.Seek = 7f; p.Drag = 1.2f; p.Stretch = true;
                        }
                        Shock(from, 30f, 140f, 0.35f, EnhanceFxStyle.WithA(ElementColor, 0.9f));
                    }
                    break;
                }
                case EnhanceFx.Heartbeat:
                    _heartPulse = 1f;
                    if (!fast && _model.Intensity >= 3 && Amount > 0f)
                        _fx.Play(EnhanceCommonArt.Anim(EnhanceCommonArt.Combat(Ctx != null ? Ctx.Art : null, EnhanceCommonArt.Shockwave), EnhanceCommonArt.ShockFps * 1.5f, false, "beat"),
                            RitualCx - 256f, RitualCy - 256f, 512f, 512f, EnhanceFxStyle.WithA(ElementColor, 0.45f));
                    if (!fast) Shock(RitualScreen, 380f, 640f + 40f * _model.Intensity, 0.45f, EnhanceFxStyle.WithA(ElementColor, 0.3f + 0.05f * _model.Intensity));
                    break;
                case EnhanceFx.Hold:
                    _holdGlow = 1f;
                    break;
                case EnhanceFx.Flash:
                {
                    var d = _guard.RequestFlash(c.Alpha, level, FlashScale, fast);
                    if (d.Border) _borderTicks = EnhanceScreenTuning.Ticks(_t.BorderHighlightSeconds);
                    else if (d.Alpha > 0) { _flashAlpha = d.Alpha; _flashColor = CueColor(c.Color); _flashStart = _guard.Now; }
                    break;
                }
                case EnhanceFx.Shake: _guard.AddShake(c.Shake); break;
                case EnhanceFx.Hitstop:
                    if (HitstopOn) _hitstopTicks = (int)Math.Round(c.FxTicks * EnhanceScreenTuning.At(_t.HitstopLevelScale, level));
                    break;
                case EnhanceFx.Darken:
                    if (fast) break;
                    int pct = level >= 1 ? _t.DarkenBrightnessReducedPct : _t.DarkenBrightnessPct;
                    if (r != null && r.Outcome == EnhanceOutcome.FailNoDrop && _model.Intensity <= 1) pct = Math.Max(pct, _t.NoDropIntensity1BrightnessPct);
                    _darkenTarget = 1f - pct / 100f;
                    _darkenTotal = _darkenTicks = c.FxTicks;
                    // 대성공도 처음은 실패처럼(U-08): 빛이 꺼지듯 마법진이 식는다.
                    _moodTarget = -0.6f; _moodColor = EnhanceFxStyle.FailSmoke;
                    break;
                case EnhanceFx.Slam:
                case EnhanceFx.SlamGold:
                case EnhanceFx.StageChange:
                    AdvanceShownStage(c.Fx);
                    break;
                case EnhanceFx.Column:
                {
                    bool plus13 = _model.Phase == EnhancePhase.Plus13;
                    var a = EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, (plus13 ? "max_column_" : "success_column_") + _elementSuffix);
                    _fx.Play(a, cx - 120f, plus13 ? -80f : cy - 420f, 240f, 640f, Color.white);
                    if (plus13)
                    {
                        _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "gold_aureole"), cx - 256f, cy - 256f, 512f, 512f, Color.white);
                        _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "max_sky_band"), -SideW, -PanelTop, 1920f, 240f, Color.white, 2.5f);
                        SuccessPop(4, true);
                    }
                    else SuccessPop(r != null ? EnhanceFxStyle.Tier(r.StageAfter) : 0, false);
                    // 캐릭터 묶음: 지팡이에 내리꽂히는 벼락(96×160 시트의 2배, 아래 끝이 지팡이 가운데).
                    var bolt = CharFxAnim("success");
                    if (bolt != null) _fx.Play(bolt, cx - 96f, cy - 320f, 192f, 320f, Color.white);
                    break;
                }
                case EnhanceFx.Burst:
                {
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "gold_burst"), cx - 256f, cy - 256f, 512f, 512f, Color.white);
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "break_pieces_" + _elementSuffix), cx - 160f, cy - 160f, 320f, 320f, Color.white);
                    // 깨지는 조각 둘째 벌(보충 그림 ritual/break_pieces_b_<속성>, FX 20.1 "18개 × 2"): 첫 벌과 섞어 같이 흩뿌린다.
                    // 매번 어느 쪽이 위로 가는지 번갈아(둘째 벌을 먼저/나중에) 같은 모양이 반복되지 않게 한다.
                    var piecesB = EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "break_pieces_b_" + _elementSuffix);
                    if (piecesB != null)
                    {
                        _breakSetFlip = !_breakSetFlip;
                        float off = _breakSetFlip ? 24f : -24f;
                        _fx.Play(piecesB, cx - 160f + off, cy - 160f - off * 0.5f, 320f, 320f, Color.white);
                    }
                    PlayOnWand(CharFxAnim("great_success"));
                    PlayOnWand(CharFxAnim("break"));
                    // 금 간 유리가 산산이 부서지며 금빛이 터진다.
                    if (_particles != null && Amount > 0f)
                        _particles.Burst(EnhanceGfx.Shard, RitualScreen, Scaled(40), 500f, 1300f, 1.2f, 34f, EnhanceFxStyle.CrackTint, UiTheme.Gold, 500f, 0.8f);
                    _crackAlpha = 0f; _crackHold = 0f; _crackDim = 0f;
                    SuccessPop(r != null ? EnhanceFxStyle.Tier(r.StageAfter) : 2, true);
                    break;
                }
                case EnhanceFx.Extinguish:
                {
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "extinguish_" + _elementSuffix), cx - 128f, cy - 128f, 256f, 256f, Color.white);
                    var charSmoke = CharFxAnim("failure_smoke");
                    if (charSmoke != null) PlayOnWand(charSmoke);
                    else _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "failure_smoke"), cx - 96f, cy + 40f, 192f, 192f, EnhanceFxStyle.FailViolet);
                    bool drop = r != null && r.Outcome == EnhanceOutcome.FailDrop;
                    var soot = drop ? CharFxAnim("failure_soot") : null;
                    if (soot != null) _fx.Play(soot, cx - 64f, cy + 96f, 128f, 64f, Color.white, 1.2f);
                    FailSmoke(drop ? 1.6f : 1.0f, drop);
                    if (drop)
                    {
                        // 유리 깨지듯 금 가는 화면(정지 그림 3장 중 마지막, 08 10.8절).
                        _crackAlpha = 0.85f; _crackHold = 1.4f; _crackDim = 0.35f; _crackFrame = 2;
                        Shock(RitualScreen, 600f, 200f, 0.5f, EnhanceFxStyle.WithA(EnhanceFxStyle.FailViolet, 0.8f));
                    }
                    break;
                }
                case EnhanceFx.Ward:
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "ward_ribbon_" + _elementSuffix), cx - 160f, cy - 96f, 320f, 320f, Color.white);
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupEnhance, "protected_stamp"), cx + 96f, 140f, 192f, 96f, Color.white, 1.2f);
                    PlayOnWand(CharFxAnim("ward"));
                    // 보호막: 청록 돔이 지팡이를 감싼다.
                    CoreFlash(EnhanceFxStyle.WardTeal, 0.55f, 0.9f);
                    Shock(RitualScreen, 200f, 520f, 0.7f, EnhanceFxStyle.WithA(EnhanceFxStyle.WardTeal, 0.9f));
                    if (_particles != null) _particles.Burst(EnhanceGfx.Sparkle, RitualScreen, Scaled(20), 100f, 300f, 1f, 26f, EnhanceFxStyle.WardTeal, Color.white, -60f, 1.5f);
                    _wandDimTarget = 0.3f;
                    break;
                case EnhanceFx.HoldProp:
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "stage_hold_" + _elementSuffix), cx - 128f, cy + 96f, 256f, 192f, Color.white);
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "failure_dust"), cx - 64f, cy + 120f, 128f, 128f, EnhanceFxStyle.FailAsh);
                    FailSmoke(0.7f, false);
                    _wandDimTarget = 0.5f;
                    DigitWobble();
                    break;
                case EnhanceFx.Vine:
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "mutation_vine"), cx - 192f, cy - 256f, 384f, 512f, Color.white);
                    PlayOnWand(CharFxAnim("vine"));
                    _vineLeft = 1.0f;
                    _moodTarget = 0.6f; _moodColor = UiTheme.Mutation;
                    CoreFlash(UiTheme.Mutation, 0.4f, 0.8f);
                    break;
                case EnhanceFx.Slot:
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "slot_open"), 0f - SideW + 200f, 120f, 128f, 128f, Color.white);
                    var install = CharFxAnim("slot");
                    if (install != null) _fx.Play(install, 0f - SideW + 200f, 120f, 128f, 128f, Color.white);
                    if (_particles != null) _particles.Burst(EnhanceGfx.Sparkle, new Vector2(264f, 264f), Scaled(16), 80f, 260f, 0.9f, 24f, UiTheme.Gold, Color.white);
                    break;
                case EnhanceFx.Aura:
                    _fx.Play(EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupRitual, "artisan_ember"), cx - 32f, 788f, 64f, 64f, Color.white);
                    _emberLeft = 0.8f;
                    if (_wand != null) { _shownStage = _wand.Stage; RefreshStage(); }
                    break;
                case EnhanceFx.Ring:
                {
                    Color ring = r == null ? UiTheme.SlateBlue
                        : r.Outcome == EnhanceOutcome.GreatSuccess ? UiTheme.Gold
                        : r.Outcome == EnhanceOutcome.Success ? EnhanceFxStyle.TierColor(EnhanceFxStyle.Tier(r.StageAfter), ElementColor)
                        : r.Outcome == EnhanceOutcome.MutationOffered ? UiTheme.Mutation : EnhanceFxStyle.FailAsh;
                    Sprite circle = null;
                    if (Ctx != null && Ctx.Art != null) { try { circle = Ctx.Art.Circle(96, true, 6); } catch (Exception) { circle = null; } }
                    if (circle != null)
                    {
                        var anim = new SpriteAnim { Key = "ring", Frames = new[] { circle }, Fps = 1f, Loop = false };
                        _fx.Play(anim, cx - 128f, cy - 128f, 256f, 256f, ring, (float)_t.ShortResultSeconds);
                    }
                    // 짧은 형태도 결과 팝은 남긴다: 성공 계열은 작은 터짐, 실패 계열은 회색 고리.
                    bool good = r != null && (r.Outcome == EnhanceOutcome.Success || r.Outcome == EnhanceOutcome.GreatSuccess);
                    Shock(RitualScreen, 120f, good ? 700f : 420f, 0.4f, EnhanceFxStyle.WithA(ring, 0.9f));
                    if (good && _particles != null && Amount > 0f)
                    {
                        int tier = EnhanceFxStyle.Tier(r.StageAfter);
                        _particles.Burst(Dot(), RitualScreen, Mathf.Max(8, Scaled(EnhanceFxStyle.BurstParticles[tier] / 3)), 250f, 650f, 0.7f, 18f, ring, Color.white, 300f, 1.5f, true);
                        CoreFlash(ring, 0.6f, 0.35f);
                    }
                    else if (!good) { _wandDimTarget = 0.4f; _moodTarget = -0.5f; }
                    // 단계 숫자는 고리가 아니라 시간표의 slam/slam_gold 사건이 찍는다(G23 short·short_great).
                    // 고리에서도 찍으면 대성공 짧은 형태가 0초에 두 번 찍혀 첫 단계(+1)가 보이지 않는다(08 10.8절 "두 번 내려찍기").
                    break;
                }
                case EnhanceFx.TitleBand:
                    ShowTitleBand();
                    break;
            }
        }

        int _crackFrame;

        /// <summary>소리 사건에만 매달린 그림(시간표의 fx가 없는 순간: 대성공의 금 번짐, 지팡이 떨어짐, 부적 탐).</summary>
        void ApplySoundVisual(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            switch (eventId)
            {
                case "ui.enhance.great_crack_spread":
                    // 대성공의 앞부분: 실패처럼 금이 번진다(정지 그림 3장을 한 장씩).
                    _crackFrame = Mathf.Min(2, _crackAlpha > 0f ? _crackFrame + 1 : 0);
                    _crackAlpha = 0.45f + 0.2f * _crackFrame; _crackHold = 1f; _crackDim = 0.15f + 0.08f * _crackFrame;
                    _guard.AddShake(EnhanceFx.ShakeSmall);
                    break;
                case "ui.enhance.wand_drop":
                    _wandDropT = 0f;
                    break;
                case "ui.enhance.charm_burn":
                    if (_particles != null && Amount > 0f)
                    {
                        var at = new Vector2(SideW + RitualCx + 220f, PanelTop + 716f);
                        _particles.Burst(EnhanceGfx.SoftDot, at, Scaled(24), 40f, 200f, 1.2f, 16f, EnhanceFxStyle.DullGold, EnhanceFxStyle.FailAsh, -220f, 1f);
                    }
                    break;
                case "ui.enhance.plus13_silence":
                    _moodTarget = -1f; _moodColor = EnhanceFxStyle.FailSmoke;
                    break;
            }
        }

        /// <summary>단계 숫자 내려찍기: 두 단계 오른 대성공은 두 번(첫 번째 +1, 두 번째 도착 단계, TV-118), 그 밖은 도착 단계로.</summary>
        // ---------- 캐릭터 교체 묶음의 강화 연출(G23 character_fx, 효과 묶음 "char.N") ----------

        JObject CharFxTable => _t != null && _t.CharacterFx != null ? _t.CharacterFx[Ids.Char.Of(_character)] as JObject : null;

        string CharFxKey(string role)
        {
            var v = CharFxTable?[role];
            return v != null && v.Type == JTokenType.String ? (string)v : null;
        }

        /// <summary>그 캐릭터 묶음의 역할 그림(없으면 null — 공용 연출만 남는다).</summary>
        SpriteAnim CharFxAnim(string role) => EnhanceUi.EffectAnim(Ctx, Ids.Char.Of(_character), CharFxKey(role));

        /// <summary>단계 전환 시트(from→to 한 칸). 하락이면 to→from 시트를 거꾸로 재생한다(manifest reverseForDowngrade).</summary>
        SpriteAnim CharTransition(int from, int to)
        {
            string pattern = CharFxKey("transition");
            if (pattern == null || from == to) return null;
            int hi = Math.Max(from, to), lo = hi - 1; // 두 단계 이상 움직이면 도착 쪽 한 칸만
            if (lo < 0) return null;
            string key = pattern.Replace("{from}", lo.ToString("00", System.Globalization.CultureInfo.InvariantCulture))
                .Replace("{to}", hi.ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            var a = EnhanceUi.EffectAnim(Ctx, Ids.Char.Of(_character), key);
            if (a == null || to > from) return a;
            var rev = new Sprite[a.Frames.Length];
            for (int i = 0; i < rev.Length; i++) rev[i] = a.Frames[rev.Length - 1 - i];
            return new SpriteAnim { Key = a.Key + "|reverse", Frames = rev, Fps = a.Fps, Loop = false };
        }

        /// <summary>지팡이 그림 자리(384×384, 192 시트의 2배)에 캐릭터 연출을 겹친다.</summary>
        void PlayOnWand(SpriteAnim a, float duration = 0f, bool loop = false, string tag = null)
        {
            if (a == null || _fx == null) return;
            const float Half = 192f;
            _fx.Play(a, RitualCx - Half, RitualCy - Half, Half * 2f, Half * 2f, Color.white, duration, loop, tag);
        }

        const string CharIdleTag = "char_idle";
        int _charIdleBand = -1;

        /// <summary>단계 띠가 바뀌면 지팡이 둘레 반복 연출(idle_band_N)을 바꿔 건다.</summary>
        void RefreshCharIdle()
        {
            string pattern = CharFxKey("idle");
            if (pattern == null || _fx == null) return;
            int band = 0;
            if (CharFxTable?["bands"] is JArray bands && bands.Count > 0)
            {
                band = bands.Count - 1;
                for (int i = 0; i < bands.Count; i++)
                    if (_shownStage <= (int)bands[i]) { band = i; break; }
            }
            if (band == _charIdleBand && _fx.IsPlaying(CharIdleTag)) return;
            _charIdleBand = band;
            _fx.Stop(CharIdleTag);
            var a = EnhanceUi.EffectAnim(Ctx, Ids.Char.Of(_character), pattern.Replace("{band}", band.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            PlayOnWand(a, 0f, true, CharIdleTag);
        }

        void AdvanceShownStage(string fx)
        {
            var r = _model.Shown;
            if (r == null) return;
            _slamCount++;
            bool twoStep = r.Outcome == EnhanceOutcome.GreatSuccess && r.StageAfter - r.StageBefore >= 2 && _model.Phase != EnhancePhase.Plus13;
            _shownStage = twoStep && _slamCount == 1 ? r.StageBefore + 1 : r.StageAfter;
            _stageReveals.Add(_shownStage);
            bool down = r.StageAfter < r.StageBefore;
            bool same = r.StageAfter == r.StageBefore;
            int tier = EnhanceFxStyle.Tier(_shownStage);
            if (down) DigitDrop();
            else if (same) DigitWobble();
            else
            {
                Color col = fx == EnhanceFx.SlamGold ? UiTheme.Gold : EnhanceFxStyle.TierColor(tier, ElementColor);
                DigitSlam(col, EnhanceFxStyle.SlamScale[tier] * (_model.Style == RitualStyle.Full ? 1f : 0.75f));
                if (_particles != null && Amount > 0f)
                {
                    _particles.Burst(EnhanceGfx.Sparkle, DigitsScreen, Scaled(10 + 6 * tier), 200f, 520f, 0.7f, 30f, col, Color.white, 300f, 2f);
                    Shock(DigitsScreen, 60f, 360f + 60f * tier, 0.45f, EnhanceFxStyle.WithA(col, 0.9f));
                }
                _guard.AddShake(tier >= 2 ? EnhanceFx.ShakeMedium : EnhanceFx.ShakeSmall);
            }
            RefreshTop();
            RefreshStage();
            // 캐릭터 묶음의 단계 전환(오르면 앞으로, 내리면 거꾸로). 큰 성공 두 칸이면 첫 칸·둘째 칸이 차례로 온다.
            if (!same) PlayOnWand(CharTransition(down ? r.StageBefore : _shownStage - 1, _shownStage));
        }

        void ShowTitleBand()
        {
            var r = _model.Shown;
            if (r == null) return;
            string text = EnhanceResultCardModel.MilestoneBand(Ctx.Names, _cfg, _character, r.StageBefore, r.StageAfter);
            _titleBand.SetText(text);
            bool gold = r.StageAfter >= _cfg.MaxStage || EnhanceFxStyle.Tier(r.StageAfter) >= 2;
            _titleBand.Color = gold ? UiTheme.Gold : UiTheme.Chalk;
            if (_titleRibbon != null)
            {
                var rib = EnhanceUi.EffectFrame(Ctx, EnhanceUi.GroupRitual, gold ? "title_ribbon_gold" : "title_ribbon_normal", 1);
                _titleRibbon.sprite = rib;
                _titleRibbon.color = rib != null ? Color.white : EnhanceFxStyle.WithA(UiTheme.Ink, 0.85f);
            }
            _titleBandRoot.gameObject.SetActive(true);
            _bandAnimT = 0f;
            if (_particles != null && Amount > 0f)
                _particles.Burst(EnhanceGfx.Sparkle, new Vector2(SideW + RitualCx, 470f), Scaled(30), 200f, 700f, 1f, 30f, UiTheme.Gold, Color.white, 200f, 1.6f);
        }

        // ------------------------------------------------------------------ 매 프레임

        static float EaseOutBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        bool RitualPhase(EnhancePhase p) =>
            p == EnhancePhase.Committing || p == EnhancePhase.Payment || p == EnhancePhase.Offering || p == EnhancePhase.Charge
            || p == EnhancePhase.Hold || p == EnhancePhase.Judge;

        /// <summary>매 프레임 그리기: 게이지, 확률 숫자 굴림, 카메라 확대, 배경 어두움, 번쩍, 흔들림, 지팡이 떠오름·떨림, 덧연출.</summary>
        void RenderFrame()
        {
            if (_stage == null) return;
            float dt = _vdt;
            int level = EffectsLevel;
            var phase = _model.Phase;
            bool full = _model.Style == RitualStyle.Full;
            bool showRitual = _model.Style != RitualStyle.CardOnly && RitualPhase(phase);
            int intensity = _model.Intensity;
            float g = (float)_model.Gauge;
            bool hold = phase == EnhancePhase.Hold;
            float time = _animClock;

            // 큰 확률 숫자(0.2초 굴림)와 계기판.
            if (_prob != null && !_prob.AtMax)
            {
                double v = CurrentRollValue();
                var pc = ShowBigProb(v);
                float w = MeterW * Mathf.Clamp01((float)v / 100f);
                _meterFill.gameObject.SetActive(w > 1f);
                UiKit.Place(_meterFill.rectTransform, MeterX, MeterY, Mathf.Max(w, 28f), MeterH);
                _meterFill.color = pc;
                _meterGlow.color = EnhanceFxStyle.WithA(pc, 0.6f + 0.25f * Mathf.Sin(time * 4f));
                EnhanceGfx.Centered(_meterGlow.rectTransform, MeterX + w, MeterY + MeterH * 0.5f, 72f, 72f);
            }
            else if (_meterFill != null) { _meterFill.gameObject.SetActive(false); _meterGlow.color = new Color(1f, 1f, 1f, 0f); }

            // 충전 중 카메라 확대·배경 어두움(빠른 강화·최소에서는 없음). 강도 2부터 지팡이가 떠오르고 충전 중 지팡이만 떤다.
            bool charging = full && (phase == EnhancePhase.Charge || hold);
            double zoom = 1.0;
            float dimPct = _t.BackgroundDimIdlePct;
            float floatPx = 0f, trem = 0f;
            if (charging)
            {
                double z = EnhanceScreenTuning.At(_t.CameraZoom, intensity);
                zoom = 1.0 + (z - 1.0) * EnhanceScreenTuning.At(_t.CameraLevelScale, level) * g;
                double dimTo = _t.BackgroundDimIdlePct + (EnhanceScreenTuning.At(_t.BackgroundDimPct, intensity) - _t.BackgroundDimIdlePct)
                    * EnhanceScreenTuning.At(_t.DimLevelScale, level);
                dimPct = (float)(_t.BackgroundDimIdlePct + (dimTo - _t.BackgroundDimIdlePct) * g);
                if (intensity >= _t.WandFloatMinIntensity) floatPx = (float)(_t.WandFloatPx * g);
                if (phase == EnhancePhase.Charge && _hitstopTicks <= 0)
                    trem = (float)(_t.ChargeTremblePx * g);
            }
            _ritual.localScale = new Vector3((float)zoom, (float)zoom, 1f);
            _dim.color = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, dimPct / 100f);
            // 집중 어둠: 의식 동안 양옆 판이 가라앉아 마법진만 남는다(빠른 강화는 옅게).
            float focusTarget = 0f;
            if (showRitual)
            {
                float dimScale = (float)EnhanceScreenTuning.At(_t.DimLevelScale, level);
                focusTarget = full ? (0.3f + 0.35f * g + (hold ? 0.1f : 0f)) * dimScale : 0.2f * dimScale;
            }
            _focusDimA = Mathf.MoveTowards(_focusDimA, focusTarget, dt * (focusTarget > _focusDimA ? 2.5f : 1.2f));
            _focusDim.color = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, _focusDimA);
            _vignette.color = EnhanceFxStyle.WithA(UiTheme.Ink, 0.45f + 0.45f * _focusDimA);

            float tx = trem > 0f ? UnityEngine.Random.Range(-trem, trem) : 0f;
            float ty = trem > 0f ? UnityEngine.Random.Range(-trem, trem) : 0f;
            // 지팡이 떨어짐(하락): 가라앉았다가 천천히 돌아온다.
            float dropY = 0f, tilt = 0f;
            if (_wandDropT >= 0f)
            {
                _wandDropT += dt;
                float k = _wandDropT;
                dropY = k < 0.25f ? Mathf.Lerp(0f, 46f, k / 0.25f) : 46f * Mathf.Exp(-(k - 0.25f) * 1.6f) * Mathf.Cos((k - 0.25f) * 9f) * 0.6f + 46f * Mathf.Exp(-(k - 0.25f) * 1.2f) * 0.4f;
                tilt = (k < 0.25f ? k / 0.25f : Mathf.Exp(-(k - 0.25f) * 1.5f)) * -14f;
                if (k > 4f) _wandDropT = -1f;
            }
            float bob = phase == EnhancePhase.Idle || phase == EnhancePhase.ResultCard ? Mathf.Sin(time * 1.6f) * 5f : 0f;
            UiKit.Place(_wandRoot, 380f - WandPx * 0.5f + tx, 380f - WandPx * 0.5f - floatPx + ty + dropY + bob, WandPx, WandPx);
            _wandRoot.localEulerAngles = new Vector3(0f, 0f, tilt);
            _wandDim = Mathf.MoveTowards(_wandDim, _wandDimTarget, dt * 1.5f);
            if (_wandImage.sprite != null) _wandImage.color = Color.Lerp(Color.white, EnhanceFxStyle.Hex(0x6B6680), _wandDim);

            // 숨 멈춤의 미세 떨림: 마법진·지팡이 묶음만(화면 전체는 떨지 않는다).
            float holdTrem = 0f;
            if (showRitual && full && _hitstopTicks <= 0)
            {
                float hp = EnhanceFxStyle.HoldTremblePx[Mathf.Clamp(intensity, 0, EnhanceFxStyle.HoldTremblePx.Length - 1)];
                holdTrem = hold ? hp : (phase == EnhancePhase.Charge && g > 0.8f ? hp * (g - 0.8f) * 5f * 0.5f : 0f);
                holdTrem *= Amount > 0f ? 1f : 0f;
            }
            float rx = holdTrem > 0f ? UnityEngine.Random.Range(-holdTrem, holdTrem) : 0f;
            float ry = holdTrem > 0f ? UnityEngine.Random.Range(-holdTrem, holdTrem) : 0f;
            _ritual.anchoredPosition = new Vector2(RitualCx + rx, -RitualCy + ry);

            // 마법진 회전: 기본 6초 → 충전 끝 1초에 한 바퀴(effects 2절 — 표시만). 숨 멈춤에는 모두 멈춘다.
            float idleDeg = _t.SealSpinIdleSeconds > 0 ? (float)(360.0 / _t.SealSpinIdleSeconds) : 0f;
            float fastDeg = _t.SealSpinChargedSeconds > 0 ? (float)(360.0 / _t.SealSpinChargedSeconds) : idleDeg;
            float spin = charging ? Mathf.Lerp(idleDeg, fastDeg, g) : (showRitual ? idleDeg * 2f : idleDeg);
            if (hold) spin = 0f;
            if (_mood > 0.2f) spin += fastDeg * 0.5f * _mood;
            if (_hitstopTicks <= 0) _sealBase.rectTransform.localEulerAngles = new Vector3(0f, 0f, _sealBase.rectTransform.localEulerAngles.z - spin * dt);
            _sigilAngle += spin * 0.6f * dt;
            _glyphAngle -= spin * 0.25f * dt;
            _sigil.rectTransform.localEulerAngles = new Vector3(0f, 0f, _sigilAngle);
            _outerGlyph.rectTransform.localEulerAngles = new Vector3(0f, 0f, _glyphAngle);

            // 분위기(성공 빛 +1 / 꺼짐 −1).
            _mood = Mathf.MoveTowards(_mood, _moodTarget, dt * (Mathf.Abs(_moodTarget) > Mathf.Abs(_mood) ? 4f : 0.8f));
            if (phase == EnhancePhase.ResultCard || phase == EnhancePhase.Idle) _moodTarget = Mathf.MoveTowards(_moodTarget, 0f, dt * 0.5f);
            _heartPulse = Mathf.Max(0f, _heartPulse - dt * 4f);
            if (!hold) _holdGlow = Mathf.Max(0f, _holdGlow - dt * 2f);
            float bright = Mathf.Clamp01(Mathf.Max(0f, _mood));
            float dark = Mathf.Clamp01(Mathf.Max(0f, -_mood));
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 2.2f);
            Color baseCol = Color.Lerp(ElementColor, _moodColor, bright);
            baseCol = Color.Lerp(baseCol, EnhanceFxStyle.FailSmoke, dark);

            // 겹 마법진: 대기 2겹 → 충전 중 강도만큼(시간표 circle_layers) + 1겹. 성공하면 다 켜진다.
            int layersOn = showRitual ? Mathf.Min(_sealLayers.Count, EnhanceScreenTuning.At(_t.CircleLayers, intensity) + 1) : 2;
            if (bright > 0.3f) layersOn = _sealLayers.Count;
            Color layerCol = Color.Lerp(baseCol, Color.white, 0.35f + 0.3f * bright);
            for (int i = 0; i < _sealLayers.Count; i++)
            {
                float target = i < layersOn ? (showRitual ? 0.35f + 0.55f * g : 0.28f + 0.1f * breathe) : 0f;
                target = Mathf.Max(target, bright * 0.9f) * (1f - 0.6f * dark);
                var img = _sealLayers[i];
                float a = Mathf.MoveTowards(img.color.a, target, dt * 3f);
                img.color = EnhanceFxStyle.WithA(layerCol, a);
                float layerSpin = spin * EnhanceFxStyle.LayerSpin[i] * (showRitual ? 1.5f : 1f);
                _layerAngle[i] += layerSpin * dt;
                img.rectTransform.localEulerAngles = new Vector3(0f, 0f, _layerAngle[i]);
                float pulse = 1f + 0.04f * _heartPulse * (i % 2 == 0 ? 1f : -1f);
                img.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
            _sigil.color = _sigil.sprite != null ? EnhanceFxStyle.WithA(Color.Lerp(baseCol, Color.white, 0.2f), (showRitual ? 0.5f + 0.5f * g : 0.45f) * (1f - 0.6f * dark)) : new Color(1f, 1f, 1f, 0f);
            float glyphA = charging && intensity >= _t.OuterGlyphMinIntensity && _outerGlyph.sprite != null ? g : (bright > 0.3f && _outerGlyph.sprite != null ? bright * 0.8f : 0f);
            _outerGlyph.color = new Color(1f, 1f, 1f, glyphA);

            // 빛 번짐: 마법진 둘레, 지팡이 뒤, 빛기둥, 충전 고리.
            float glowA = 0.2f + 0.08f * breathe + (showRitual ? 0.45f * g + 0.15f * _heartPulse : 0f) + 0.5f * bright + 0.3f * _holdGlow;
            glowA *= 1f - 0.75f * dark;
            _stageGlow.color = EnhanceFxStyle.WithA(baseCol, Mathf.Clamp01(glowA));
            float wg = 0.3f + 0.1f * breathe + (showRitual ? 0.4f * g + 0.3f * _heartPulse : 0f) + 0.6f * _holdGlow + 0.6f * bright;
            _wandGlow.color = EnhanceFxStyle.WithA(Color.Lerp(baseCol, Color.white, 0.25f + 0.5f * _holdGlow), Mathf.Clamp01(wg * (1f - 0.8f * dark)));
            float ws = 1f + 0.12f * _heartPulse + 0.25f * _holdGlow + 0.2f * bright;
            _wandGlow.rectTransform.localScale = new Vector3(ws, ws, 1f);
            float beamA = showRitual && full && intensity >= 1 ? g * (0.35f + 0.1f * intensity) + 0.3f * _holdGlow : 0f;
            beamA = Mathf.Max(beamA, bright * 0.7f) * Amount;
            _beam.color = EnhanceFxStyle.WithA(baseCol, Mathf.Clamp01(beamA));
            _beam.rectTransform.sizeDelta = new Vector2(160f + 200f * Mathf.Max(g, bright), 1000f);
            _beamCore.color = EnhanceFxStyle.WithA(Color.white, Mathf.Clamp01(beamA * 0.8f));
            _beamCore.rectTransform.sizeDelta = new Vector2(40f + 70f * Mathf.Max(g, bright) + 30f * _heartPulse, 1000f);
            float ringA = ChargeLightOn(phase) ? 0.85f : 0f;
            SetActiveIfChanged(_chargeRing, ringA > 0f);
            _chargeRing.fillAmount = showRitual ? g : 0f;
            _chargeRing.color = EnhanceFxStyle.WithA(Color.Lerp(ElementColor, Color.white, g * 0.6f + _holdGlow * 0.4f), ringA);
            _chargeRingBack.color = EnhanceFxStyle.WithA(UiTheme.Ink, showRitual ? 0.45f : 0f);
            _portraitGlow.color = EnhanceFxStyle.WithA(ElementColor, 0.18f + 0.08f * breathe);

            // 빨려 드는 입자(제물 바침·충전). 숨 멈춤에는 새로 나지 않고 모두 멈춘다.
            if (showRitual && !hold && _particles != null && (phase == EnhancePhase.Offering || phase == EnhancePhase.Charge))
            {
                float rate = Mathf.Max(12f, EnhanceScreenTuning.At(_t.ChargeParticlesPerSecond, intensity)) * EnhanceFxStyle.ChargeParticleBoost * (0.5f + g) * Amount;
                _chargeEmitAcc += rate * dt;
                Color pc = Color.Lerp(ElementColor, Color.white, 0.3f);
                while (_chargeEmitAcc >= 1f)
                {
                    _chargeEmitAcc -= 1f;
                    _particles.Converge(UnityEngine.Random.value < 0.6f ? Dot() : Mote(), RitualScreen, 260f, 460f, 1, 0.9f, 16f + 8f * g, pc, ElementColor);
                }
            }
            else _chargeEmitAcc = 0f;

            // 연기·반짝임·불씨·덩굴 같은 지속 방출.
            EmitOngoing(dt);

            // 성공 국소 섬광과 빛줄기.
            if (_coreT >= 0f)
            {
                _coreT += dt;
                float k = _coreT / Mathf.Max(0.05f, _coreDur);
                float env = _coreT < 0.06f ? _coreT / 0.06f : Mathf.Pow(Mathf.Clamp01(1f - k), 1.6f);
                _coreFlash.color = EnhanceFxStyle.WithA(_coreColor, _corePeak * env);
                float sc = 0.5f + 1.1f * Mathf.Clamp01(k * 1.5f);
                _coreFlash.rectTransform.localScale = new Vector3(sc, sc, 1f);
                if (k >= 1f) StopImpactLight();
            }
            if (_raysT >= 0f)
            {
                _raysT += dt;
                float k = _raysT / Mathf.Max(0.1f, _raysDur);
                float env = _raysT < 0.1f ? _raysT / 0.1f : Mathf.Pow(Mathf.Clamp01(1f - k), 1.3f);
                _raysAngle += dt * 28f;
                _rays.color = EnhanceFxStyle.WithA(_raysColor, _raysPeak * env);
                _raysBack.color = EnhanceFxStyle.WithA(Color.Lerp(_raysColor, Color.white, 0.4f), _raysPeak * env * 0.6f);
                float s = _raysSize * (0.7f + 0.4f * Mathf.Clamp01(k * 2f));
                _rays.rectTransform.sizeDelta = new Vector2(s, s);
                _raysBack.rectTransform.sizeDelta = new Vector2(s * 1.3f, s * 1.3f);
                _rays.rectTransform.localEulerAngles = new Vector3(0f, 0f, _raysAngle);
                _raysBack.rectTransform.localEulerAngles = new Vector3(0f, 0f, -_raysAngle * 0.6f);
                if (k >= 1f) { _raysT = -1f; _rays.color = _raysBack.color = new Color(1f, 1f, 1f, 0f); }
            }

            // 방사형 빛살(성공).
            if (_spokeT >= 0f && _spokes.Count > 0)
            {
                _spokeT += dt;
                float k = _spokeT / Mathf.Max(0.1f, _spokeDur);
                float env = _spokeT < 0.08f ? _spokeT / 0.08f : Mathf.Pow(Mathf.Clamp01(1f - k), 1.4f);
                _spokeAngle += dt * 40f;
                _spokeRoot.localEulerAngles = new Vector3(0f, 0f, _spokeAngle);
                float len = _spokeLen * (0.35f + 0.9f * Mathf.Clamp01(k * 3f));
                for (int i = 0; i < _spokes.Count; i++)
                {
                    float flick = 0.75f + 0.25f * Mathf.Sin(time * 13f + i * 1.7f);
                    _spokes[i].color = EnhanceFxStyle.WithA(Color.Lerp(_spokeColor, Color.white, (i % 2) * 0.4f), _spokePeak * env * flick);
                    _spokes[i].rectTransform.localScale = new Vector3(i % 2 == 0 ? 1f : 0.6f, len * (i % 2 == 0 ? 1f : 0.7f), 1f);
                }
                if (k >= 1f) { _spokeT = -1f; foreach (var sp in _spokes) sp.color = new Color(1f, 1f, 1f, 0f); }
            }

            // 단계 오라(+7 / +10 / +13 이상에서 반복). 실패로 꺼진 동안은 옅게.
            string auraKey = EnhanceCommonArt.AuraFor(_shownStage);
            if (auraKey != _auraKey)
            {
                _auraKey = auraKey;
                _auraFrames = auraKey != null ? EnhanceCommonArt.Enh(Ctx != null ? Ctx.Art : null, auraKey) : null;
            }
            bool hasAura = _auraFrames != null && _auraFrames.Length > 0;
            float auraTarget = hasAura ? (0.85f + 0.15f * bright) * (1f - 0.7f * dark) : 0f;
            _auraA = Mathf.MoveTowards(_auraA, auraTarget, dt * 2f);
            _aura.sprite = hasAura ? _auraFrames[Mathf.Abs((int)(time * EnhanceCommonArt.AuraFps)) % _auraFrames.Length] : null;
            _aura.color = new Color(1f, 1f, 1f, _aura.sprite != null ? _auraA : 0f);
            float auraScale = 1f + 0.05f * _heartPulse + 0.12f * bright;
            _aura.rectTransform.localScale = new Vector3(auraScale, auraScale, 1f);

            // 유리 금(실패·대성공 앞부분).
            if (_crackHold > 0f) _crackHold -= dt;
            else _crackAlpha = Mathf.MoveTowards(_crackAlpha, 0f, dt * 0.9f);
            if (_commonCrack != null) _crack.sprite = _commonCrack;
            else if (_crackAnim != null && _crackAnim.Frames.Length > 0) _crack.sprite = _crackAnim.Frames[Mathf.Clamp(_crackFrame, 0, _crackAnim.Frames.Length - 1)];
            // 공통 균열 그림은 한 장이라 번지는 단계를 크기로 보인다(가운데에서 바깥으로).
            float crackScale = _commonCrack != null ? 0.55f + 0.225f * Mathf.Clamp(_crackFrame, 0, 2) : 1f;
            _crack.rectTransform.localScale = new Vector3(crackScale, crackScale, 1f);
            _crack.color = _crack.sprite != null ? EnhanceFxStyle.WithA(_commonCrack != null ? Color.white : EnhanceFxStyle.CrackTint, _crackAlpha) : new Color(1f, 1f, 1f, 0f);
            if (_crackHold <= 0f) _crackDim = Mathf.MoveTowards(_crackDim, 0f, dt * 0.5f);
            _crackShadeImg.color = EnhanceFxStyle.WithA(EnhanceFxStyle.FailSmoke, _crackDim);

            // 어두워짐: 0.1초에 걸친 1회 전환.
            float darkA = 0f;
            if (_darkenTicks > 0)
            {
                int trans = Math.Max(1, EnhanceScreenTuning.Ticks(_t.DarkenTransitionSeconds));
                int elapsed = _darkenTotal - _darkenTicks;
                darkA = _darkenTarget * Mathf.Clamp01((elapsed + 1) / (float)trans);
            }
            _darken.color = new Color(UiTheme.Ink.r, UiTheme.Ink.g, UiTheme.Ink.b, darkA);

            // 번쩍(관리기가 정한 불투명도 × 모양).
            double fenv = _flashStart >= 0 ? _guard.Envelope(_guard.Now - _flashStart) : 0;
            if (fenv <= 0 && _flashStart >= 0 && _guard.Now - _flashStart > 1) _flashStart = -1;
            _flash.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, (float)(_flashAlpha * fenv));
            _border.gameObject.SetActive(_borderTicks > 0);
            if (_borderTicks > 0)
            {
                var o = _border.GetComponent<UnityEngine.UI.Outline>();
                if (o != null) o.effectColor = new Color(UiTheme.Chalk.r, UiTheme.Chalk.g, UiTheme.Chalk.b, 1f);
            }

            // 흔들림: 무대만(전장 카메라가 없는 화면이라 무대 판을 옮긴다).
            double px = _guard.ShakePx(level, ShakeScale);
            float sx = px > 0 ? UnityEngine.Random.Range((float)-px, (float)px) : 0f;
            float sy = px > 0 ? UnityEngine.Random.Range((float)-px, (float)px) : 0f;
            _stage.anchoredPosition = new Vector2(SideW + sx, -PanelTop + sy);

            // 비용 모자람 흔들림(모자란 쪽 칩 좌우 4px).
            float cs = _costShakeTicks > 0 ? ((_costShakeTicks / 2) % 2 == 0 ? _t.CostShakePx : -_t.CostShakePx) : 0f;
            UiKit.Place(_costStoneChip, CostStoneX + ((_shake & CostShortage.Stone) != 0 ? cs : 0f), CostChipY, CostStoneW, CostChipH);
            UiKit.Place(_costGoldChip, CostGoldX + ((_shake & CostShortage.Gold) != 0 ? cs : 0f), CostChipY, CostGoldW, CostChipH);
            UiKit.Place(_goldChip, 1084f + ((_shake & CostShortage.Gold) != 0 ? cs : 0f), 16f, 236f, 48f);

            RenderDigits(dt);
            RenderButton(time, phase);
            RenderCards(dt);

            if (_candle != null)
            {
                var a = EnhanceUi.EffectAnim(Ctx, EnhanceUi.GroupEnhance, "candle");
                if (a != null) _candle.sprite = a.At(_animClock);
            }
            // 숨 멈춤에는 입자도 멈춘다(모두 멈춤, 08 10.6절).
            _particles?.Update(hold ? 0f : dt);
        }

        void EmitOngoing(float dt)
        {
            if (_particles == null || dt <= 0f) return;
            var c = RitualScreen;
            float amt = Amount;
            if (_smokeLeft > 0f)
            {
                _smokeLeft -= dt;
                if (amt > 0f && UnityEngine.Random.value < 40f * amt * dt * 2f)
                {
                    var p = _particles.Spawn(EnhanceGfx.SoftDot, c + new Vector2(UnityEngine.Random.Range(-90f, 90f), UnityEngine.Random.Range(-20f, 120f)),
                        new Vector2(UnityEngine.Random.Range(-40f, 40f), UnityEngine.Random.Range(-140f, -60f)), 1.6f, 60f, 200f,
                        EnhanceFxStyle.WithA(EnhanceFxStyle.FailSmoke, 0.75f), EnhanceFxStyle.WithA(EnhanceFxStyle.FailViolet, 0f), false);
                    if (p != null) { p.Drag = 0.6f; p.RotVel = UnityEngine.Random.Range(-40f, 40f); }
                }
                if (amt > 0f && UnityEngine.Random.value < 14f * amt * dt)
                {
                    var p = _particles.Spawn(EnhanceGfx.SoftDot, c + UnityEngine.Random.insideUnitCircle * 120f, new Vector2(0f, UnityEngine.Random.Range(20f, 80f)), 1.2f, 10f, 4f,
                        EnhanceFxStyle.FailAsh, EnhanceFxStyle.WithA(EnhanceFxStyle.FailAsh, 0f));
                    if (p != null) p.Gravity = 60f;
                }
            }
            if (_sparkleLeft > 0f)
            {
                _sparkleLeft -= dt;
                if (amt > 0f && UnityEngine.Random.value < 50f * amt * dt)
                {
                    var p = _particles.Spawn(EnhanceGfx.Sparkle, new Vector2(UnityEngine.Random.Range(560f, 1360f), UnityEngine.Random.Range(60f, 200f)),
                        new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(80f, 200f)), 1.6f, 30f, 8f,
                        _sparkleColor, EnhanceFxStyle.WithA(Color.white, 0f));
                    if (p != null) { p.Gravity = 120f; p.RotVel = UnityEngine.Random.Range(-180f, 180f); }
                }
            }
            if (_emberLeft > 0f)
            {
                _emberLeft -= dt;
                if (amt > 0f && UnityEngine.Random.value < 30f * amt * dt)
                {
                    var p = _particles.Spawn(EnhanceGfx.SoftDot, new Vector2(SideW + RitualCx + UnityEngine.Random.Range(-60f, 60f), PanelTop + 820f),
                        new Vector2(UnityEngine.Random.Range(-30f, 30f), UnityEngine.Random.Range(-160f, -80f)), 1f, 12f, 4f, UiTheme.Amber, EnhanceFxStyle.WithA(UiTheme.Gold, 0f));
                    if (p != null) p.Drag = 0.5f;
                }
            }
            if (_vineLeft > 0f)
            {
                _vineLeft -= dt;
                if (amt > 0f && UnityEngine.Random.value < 40f * amt * dt)
                {
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    var p = _particles.Spawn(EnhanceGfx.SoftDot, c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 60f,
                        new Vector2(Mathf.Cos(ang + 1.2f), Mathf.Sin(ang + 1.2f)) * 220f, 1.1f, 16f, 6f, UiTheme.Mutation, EnhanceFxStyle.WithA(UiTheme.Good, 0f));
                    if (p != null) p.Drag = 1.2f;
                }
            }
        }

        /// <summary>충전 빛(고리)이 켜지는 때: 의식 그림이 있는 구간 중 값 치르기 뒤(바침·충전·숨 멈춤·판정).</summary>
        bool ChargeLightOn(EnhancePhase phase) =>
            _model.Style != RitualStyle.CardOnly && RitualPhase(phase) && phase != EnhancePhase.Payment;

        /// <summary>국소 섬광(충격 빛)을 끈다. 결과 카드·지팡이 바꾸기에서 남지 않게 한다.</summary>
        void StopImpactLight()
        {
            _coreT = -1f;
            if (_coreFlash == null) return;
            _coreFlash.color = new Color(1f, 1f, 1f, 0f);
            SetActiveIfChanged(_coreFlash, false);
        }

        /// <summary>구간이 바뀌는 즉시(프레임 그리기 전에도) 빛 오브젝트의 켜짐을 맞춘다.</summary>
        void SyncLightObjects()
        {
            if (_chargeRing != null && !ChargeLightOn(_model.Phase)) SetActiveIfChanged(_chargeRing, false);
            if (_model.Phase == EnhancePhase.ResultCard || _model.Phase == EnhancePhase.Idle || _model.Phase == EnhancePhase.MutationChoice)
                StopImpactLight();
        }

        static void SetActiveIfChanged(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }

        void RenderDigits(float dt)
        {
            float scale = 1f, ox = 0f, oy = 0f;
            Color col = StageColor(_shownStage);
            if (_digitT >= 0f)
            {
                _digitT += dt;
                float t = _digitT;
                if (_digitMode == 0)
                {
                    // 쾅: 크게 나타나 박히고 튕기며 자리 잡는다.
                    scale = 1f + (_digitPeak - 1f) * Mathf.Exp(-t * 7f) * Mathf.Cos(t * 18f);
                    col = Color.Lerp(Color.white, Color.Lerp(_digitFlash, col, Mathf.Clamp01(t / 0.8f)), Mathf.Clamp01(t / 0.08f));
                    if (t > 1.2f) _digitT = -1f;
                }
                else if (_digitMode == 1)
                {
                    // 떨어짐: 아래로 툭 떨어져 흔들리다 제자리로(회색으로 식는다).
                    oy = t < 0.18f ? Mathf.Lerp(0f, 34f, t / 0.18f) : 34f * Mathf.Exp(-(t - 0.18f) * 2.2f);
                    ox = Mathf.Sin(t * 38f) * 10f * Mathf.Exp(-t * 2.5f);
                    scale = 1f - 0.15f * Mathf.Exp(-t * 3f);
                    col = Color.Lerp(EnhanceFxStyle.FailViolet, col, Mathf.Clamp01((t - 0.6f) / 1.2f));
                    if (t > 2.4f) _digitT = -1f;
                }
                else
                {
                    ox = Mathf.Sin(t * 42f) * 8f * Mathf.Exp(-t * 3.5f);
                    col = Color.Lerp(EnhanceFxStyle.FailAsh, col, Mathf.Clamp01(t / 1f));
                    if (t > 1.2f) _digitT = -1f;
                }
            }
            _stageDigits.Rect.localScale = Vector3.one * DigitsBaseScale * Mathf.Max(0.3f, scale);
            EnhanceGfx.Centered(_stageDigits.Rect, RitualCx + ox, DigitsCy + oy, 400f, 96f);
            _stageDigits.Color = col;
            var gc = _digitsGlow.color;
            _digitsGlow.color = EnhanceFxStyle.WithA(Color.Lerp(gc, col, dt * 2f), Mathf.MoveTowards(gc.a, 0.14f, dt * 1.2f));
        }

        void RenderButton(float time, EnhancePhase phase)
        {
            bool ready = _prob != null && !_prob.AtMax && _prob.Blocked == ErrorCode.None && _model.AcceptsSetup;
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 3.6f);
            _enhanceHalo.color = EnhanceFxStyle.WithA(ElementColor, ready ? 0.25f + 0.35f * pulse : 0f);
            float s = ready ? 1f + 0.06f * pulse : 1f;
            _enhanceHalo.rectTransform.localScale = new Vector3(s, s, 1f);
            _enhanceGlow.color = EnhanceFxStyle.WithA(Color.Lerp(ElementColor, Color.white, 0.5f), ready ? 0.35f + 0.5f * pulse : 0.1f);
            float bs = ready ? 1f + 0.015f * pulse : 1f;
            _enhanceButton.Rect.localScale = new Vector3(bs, bs, 1f);
            // 빈 제물 칸의 반짝임이 은은히 숨 쉰다.
            for (int i = 0; i < _sacrificeSlots.Count; i++)
            {
                var m = _sacrificeSlots[i].ContentRoot.Find("EmptyMark");
                if (m == null) continue;
                var img = m.GetComponent<UImage>();
                if (img != null) img.color = EnhanceFxStyle.WithA(ElementColor, 0.2f + 0.25f * (0.5f + 0.5f * Mathf.Sin(time * 2.4f + i * 0.9f)));
            }
        }

        void RenderCards(float dt)
        {
            if (_cardAnimT >= 0f && _resultCard.gameObject.activeSelf)
            {
                _cardAnimT += dt;
                float k = EaseOutBack(_cardAnimT / 0.28f);
                _resultCard.localScale = new Vector3(0.8f + 0.2f * k, 0.8f + 0.2f * k, 1f);
                if (_cardAnimT > 0.6f) { _cardAnimT = -1f; _resultCard.localScale = Vector3.one; }
            }
            var phase = _model.Phase;
            if (phase != EnhancePhase.Milestone && phase != EnhancePhase.Plus13 && _titleBandRoot.gameObject.activeSelf) _titleBandRoot.gameObject.SetActive(false);
            if (_bandAnimT >= 0f && _titleBandRoot.gameObject.activeSelf)
            {
                _bandAnimT += dt;
                float k = EaseOutBack(_bandAnimT / 0.35f);
                _titleBandRoot.localScale = new Vector3(0.6f + 0.4f * k, 0.3f + 0.7f * k, 1f);
                if (_bandAnimT > 0.6f) { _bandAnimT = -1f; _titleBandRoot.localScale = Vector3.one; }
            }
        }
    }
}
