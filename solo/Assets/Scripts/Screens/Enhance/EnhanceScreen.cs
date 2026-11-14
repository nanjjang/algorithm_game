using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.UI;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>
    /// 강화 화면(08 요약서 10절, 07 5절). 인자: WandId(long), ReturnTo(ScreenId). 진입은 메인 메뉴(→ WandPicker)와 정산의 버튼뿐이다.
    /// 강화의 확률·비용·실패 규칙은 다섯 지팡이가 같고(규칙은 IEnhanceService), 결과는 EnhanceResult가 정한다 —
    /// 이 화면은 결과를 받은 뒤 의식 시간표(EnhanceScreenModel)대로 보여 주기만 한다. 오프라인이라 결과 늦음 대기·서버 알림이 없다.
    /// 키: 방향 칸 이동, J 넣기·강화(의식 중 건너뛰기, 결과 카드에서 다시 시도), K 빼기(이어서 강화 멈춤), Q/E 지팡이 바꾸기, Esc 나가기.
    /// 닫힐 때 의식 그림 묶음을 내린다(ctx.Art.Release("ritual"), 약 100MB — 00 10절).
    /// </summary>
    public sealed partial class EnhanceScreen : ScreenBase
    {
        public const string RitualGroup = "ritual";

        EnhanceScreenTuning _t;
        EnhanceConfig _cfg;
        EnhanceScreenModel _model;
        ContinuousEnhanceModel _cont;
        EnhanceFlashGuard _guard;
        EnhanceFxPlayer _fx;
        readonly List<EnhanceCue> _cues = new List<EnhanceCue>();

        int MaxSacrifice = 5, AuraMax = 3, MaxMutationCells = 3, CandidatesPerRow = 14;

        WandData _wand;
        long _wandId;
        int _character = 1;
        string _element, _elementSuffix;
        Color ElementColor = UiTheme.ElementNone;
        readonly List<string> _sacrifices = new List<string>();
        readonly List<string> _lastComposition = new List<string>();
        bool _charmSet, _lastCharm;
        int _candPage;
        int _selectedMutation;
        int _targetStage;
        readonly List<BlockDef> _candidates = new List<BlockDef>();
        ProbabilityDisplayModel _prob;
        EnhancePhase _lastPhase = EnhancePhase.Idle;
        float _acc, _animClock;
        int _hitstopTicks, _darkenTicks, _darkenTotal, _borderTicks, _costShakeTicks;
        int _flashStart = -1;
        double _flashAlpha;
        Color _flashColor = Color.white;
        float _darkenTarget;
        int _shownStage, _slamCount;
        readonly List<int> _stageReveals = new List<int>();
        bool _pendingAuto;
        double _rollFrom, _rollTo;
        int _rollTick = -1;
        CostShortage _shake;

        public override ScreenId Id => ScreenId.Enhance;
        protected override bool HasFrame => false;
        public override bool BlocksInput => true;

        /// <summary>시험용: 상태 기계.</summary>
        public EnhanceScreenModel Model => _model;
        public ContinuousEnhanceModel Continuous => _cont;
        public WandData Wand => _wand;
        public IReadOnlyList<string> Sacrifices => _sacrifices;
        public ProbabilityDisplayModel Probability => _prob;
        /// <summary>시험용: 이번 시도에서 단계 숫자를 내려찍은 차례(대성공 두 칸이면 +1, 도착 단계 — 08 10.7절).</summary>
        public IReadOnlyList<int> StageReveals => _stageReveals;

        void InitState()
        {
            var content = Ctx != null ? Ctx.Content : null;
            _t = EnhanceScreenTuning.From(content);
            _cfg = content != null && content.Enhance != null ? content.Enhance : new EnhanceConfig();
            _model = new EnhanceScreenModel(_t, _cfg.MaxStage);
            _cont = new ContinuousEnhanceModel(_t.AutoStopOnDropDefault);
            _guard = new EnhanceFlashGuard(_t);
            if (_cfg.Sacrifice != null && _cfg.Sacrifice.MaxBlocks > 0) MaxSacrifice = _cfg.Sacrifice.MaxBlocks;
            if (_cfg.AuraMax > 0) AuraMax = _cfg.AuraMax;
            int cap = _cfg.MutationCapDefault;
            if (_cfg.MutationCap != null) foreach (var kv in _cfg.MutationCap) if (kv.Value > cap) cap = kv.Value;
            if (cap > 0) MaxMutationCells = cap;
            if (_t.CandidatesPerRow > 0) CandidatesPerRow = _t.CandidatesPerRow;
        }

        // ------------------------------------------------------------------ 수명

        public override void OnEnter(ScreenArgs args)
        {
            base.OnEnter(args);
            long id = Args.Get(ScreenArgKeys.WandId, 0L);
            var p = Ctx != null ? Ctx.Profile : null;
            if (p != null && p.Wand(id) == null)
            {
                var rec = p.Character(p.LastCharacter);
                id = rec != null ? rec.EquippedWandId : (p.Wands.Count > 0 ? p.Wands[0].Id : 0);
            }
            _acc = 0f;
            _cont.Armed = false;
            _cont.Stop(ContinueStop.Input);
            _continueToggle.Value = false;
            _fastToggle.Value = FastSetting();
            _stopOnDropToggle.Value = _cont.StopOnDrop;
            LoadWand(id);
            PlaySound(EnhanceSoundIds.Open, SoundBus.Ui);
            EnhanceTutorialGate.TryOpen(Ctx, Id);
        }

        public override void OnExit()
        {
            _cont.Stop(ContinueStop.Input);
            _model.Reset();
            _fx?.StopAll();
            PlaySound(EnhanceSoundIds.Close, SoundBus.Ui);
            Ctx?.Sound.StopLoop(EnhanceSoundIds.WandIdle);
            ReleaseRitualArt(Ctx);
            base.OnExit();
        }

        /// <summary>의식 그림 묶음을 내린다(ctx.Art가 null이면 건너뛴다).</summary>
        public static void ReleaseRitualArt(GameContext ctx)
        {
            if (ctx == null || ctx.Art == null) return;
            ctx.Art.Release(RitualGroup);
        }

        void LoadWand(long id)
        {
            var p = Ctx != null ? Ctx.Profile : null;
            _wandId = id;
            _wand = p != null ? p.Wand(id) : null;
            _sacrifices.Clear();
            _lastComposition.Clear();
            _charmSet = false;
            _candPage = 0;
            _selectedMutation = 0;
            ResetRitualVisuals();
            _model.Reset();
            _rollTick = -1;
            if (_wand == null) { RefreshAll(); return; }
            _character = _wand.Character;
            var def = Ctx.Content != null ? Ctx.Content.Character(Ids.Char.Of(_character)) : null;
            _element = def != null ? def.Element : null;
            _elementSuffix = EnhanceUi.ElementSuffix(_element);
            ElementColor = UiTheme.ElementColor(_element);
            _enhanceButton.Tint = ElementColor;
            _band[1].color = ElementColor;
            _targetStage = NextMilestone(_wand.Stage);
            _shownStage = _wand.Stage;
            RestartIdleLoop(_wand.Stage);
            SetupStageArt();
            // 결정 대기 중인 변이 제시가 있으면 변이 카드부터(TV-M-07).
            var offer = EnhanceResultCardModel.FromOffer(_wand);
            if (offer != null) _model.ResumePendingOffer(offer);
            RefreshAll();
            _prob = Preview();
            _rollFrom = _rollTo = _prob != null ? _prob.TotalPct : 0;
            _lastPhase = _model.Phase;
        }

        int NextMilestone(int stage)
        {
            int best = _cfg.MaxStage;
            if (_cfg.Milestones != null) foreach (int m in _cfg.Milestones) if (m > stage && m < best) best = m;
            return Math.Max(stage + 1, Math.Min(best, _cfg.MaxStage));
        }

        // ------------------------------------------------------------------ 설정

        Wizard.Solo.Meta.SettingsData Settings => Ctx != null && Ctx.Settings != null ? Ctx.Settings.Current : null;
        bool FastSetting() => SettingsOptions.Str(Settings, OptionKeys.EnhanceRitual) == OptionValues.Fast;
        bool FastAllowHigh() => SettingsOptions.Bool(Settings, OptionKeys.EnhanceFastAllowHigh);
        int EffectsLevel => SettingsOptions.EffectsLevel(Settings);
        int FlashScale => SettingsOptions.Int(Settings, OptionKeys.FlashScale);
        int ShakeScale => SettingsOptions.Int(Settings, OptionKeys.ShakeScale);
        bool HitstopOn => SettingsOptions.Bool(Settings, OptionKeys.HitstopEnabled);

        /// <summary>[빠른 강화] 토글과 설정의 enhance_ritual은 같은 값 하나(C-08).</summary>
        void SetFastMode(bool on)
        {
            var s = Settings;
            if (s == null) return;
            SettingsOptions.Set(s, OptionKeys.EnhanceRitual, on ? OptionValues.Fast : OptionValues.Full);
            Ctx.Settings.Save();
        }

        void SetContinue(bool on)
        {
            _cont.Armed = on;
            if (!on) _cont.Stop(ContinueStop.Input);
            RefreshTally();
        }

        // ------------------------------------------------------------------ 제물·부적

        BlockDef CandidateAt(int cell)
        {
            int i = _candPage * CandidatesPerRow + cell;
            return i >= 0 && i < _candidates.Count ? _candidates[i] : null;
        }

        int Placed(string id)
        {
            int n = 0;
            foreach (var s in _sacrifices) if (s == id) n++;
            return n;
        }

        int FreeOf(string id)
        {
            var p = Ctx != null ? Ctx.Profile : null;
            if (p == null || !p.Items.TryGetValue(id, out var st) || st == null) return 0;
            return st.Qty - st.QtySocketed - Placed(id);
        }

        int ProjectileTotal()
        {
            int n = 0;
            var p = Ctx != null ? Ctx.Profile : null;
            if (p == null || Ctx.Content == null) return 0;
            foreach (var kv in p.Items)
            {
                var b = Ctx.Content.Block(kv.Key);
                if (b != null && b.IsProjectile && kv.Value != null) n += kv.Value.Qty;
            }
            return n;
        }

        int PlacedProjectiles()
        {
            int n = 0;
            foreach (var s in _sacrifices) { var b = Ctx.Content.Block(s); if (b != null && b.IsProjectile) n++; }
            return n;
        }

        bool CannotPlace(BlockDef b) => ProbabilityDisplayModel.CannotPlace(b, ProjectileTotal(), PlacedProjectiles());

        void PutCandidate(int cell)
        {
            if (!PrepareSetup()) return;
            var b = CandidateAt(cell);
            if (b == null || _sacrifices.Count >= MaxSacrifice || FreeOf(b.Id) <= 0 || CannotPlace(b)) return;
            _sacrifices.Add(b.Id);
            PlaySound(EnhanceSoundIds.SacrificePut, SoundBus.Ui);
            RefreshAll();
        }

        void RemoveSacrificeAt(int i)
        {
            if (!PrepareSetup() || i < 0 || i >= _sacrifices.Count) return;
            _sacrifices.RemoveAt(i);
            PlaySound(EnhanceSoundIds.SacrificeTake, SoundBus.Ui);
            RefreshAll();
        }

        /// <summary>K: 커서가 칸에 있으면 그 칸, 아니면 마지막에 넣은 제물(없으면 부적)을 뺀다.</summary>
        void RemoveOne()
        {
            if (!PrepareSetup()) return;
            for (int i = 0; i < _sacrificeSlots.Count; i++)
                if (ReferenceEquals(Focus.Current, _sacrificeSlots[i]) && i < _sacrifices.Count) { RemoveSacrificeAt(i); return; }
            if (ReferenceEquals(Focus.Current, _charmSlot) && _charmSet) { ToggleCharm(); return; }
            if (_sacrifices.Count > 0) { RemoveSacrificeAt(_sacrifices.Count - 1); return; }
            if (_charmSet) ToggleCharm();
        }

        void ToggleCharm()
        {
            if (!PrepareSetup() || _prob == null) return;
            if (!_charmSet && (_prob.CharmSlotFolded || CharmCount() < 1)) return;
            _charmSet = !_charmSet;
            if (_charmSet) PlaySound(EnhanceSoundIds.CharmSet, SoundBus.Ui);
            RefreshAll();
        }

        int CharmCount() => Qty(Ids.Item.Charm);

        int Qty(string id)
        {
            var p = Ctx != null ? Ctx.Profile : null;
            return p != null && p.Items.TryGetValue(id, out var s) && s != null ? s.Qty : 0;
        }

        int Bound(string id)
        {
            var p = Ctx != null ? Ctx.Profile : null;
            return p != null && p.Items.TryGetValue(id, out var s) && s != null ? s.QtyBound : 0;
        }

        void PageCandidates(int dir)
        {
            int pages = Math.Max(1, (_candidates.Count + CandidatesPerRow - 1) / CandidatesPerRow);
            _candPage = ((_candPage + dir) % pages + pages) % pages;
            RefreshCandidates();
        }

        /// <summary>조작 전: 결과 카드면 닫고 대기로. 의식 중이면 조작하지 않는다.</summary>
        bool PrepareSetup()
        {
            if (_model.Phase == EnhancePhase.ResultCard) _model.Dismiss();
            return _model.Phase == EnhancePhase.Idle && _wand != null;
        }

        // ------------------------------------------------------------------ 강화

        ProbabilityDisplayModel Preview()
        {
            if (_wand == null || Ctx == null || Ctx.Enhance == null) return null;
            ProbBreakdown b = Ctx.Enhance.Preview(_wandId, _sacrifices, _charmSet);
            ProbabilityDisplayModel.SacrificeParts(_cfg, Ctx.Content, _sacrifices, _element, out _, out bool same);
            var m = ProbabilityDisplayModel.FromPreview(_cfg, b, _wand.Stage, _wand.FloorPeak, same, _charmSet);
            m.SetHoldings(Ctx.Profile.Currencies.Gold, m.StoneId != null ? Qty(m.StoneId) : 0);
            return m;
        }

        void OnEnhancePressed() => TryAttempt(_sacrifices, _charmSet, false);

        /// <summary>[강화 J]와 같다(봇·시험 입력).</summary>
        public bool PressEnhance() => TryAttempt(_sacrifices, _charmSet, false);

        /// <summary>변이 카드의 [받는다]/[거절한다](봇·시험 입력).</summary>
        public void ChooseMutation(bool accept) => ResolveMutation(accept);

        /// <summary>시도: 전제 검사 → 결과를 받는다(이미 정해진다) → 의식 시작.</summary>
        bool TryAttempt(IReadOnlyList<string> sacrifices, bool useCharm, bool auto)
        {
            if (_wand == null || Ctx == null || Ctx.Enhance == null || !_model.AcceptsSetup) return false;
            if (_model.Phase == EnhancePhase.ResultCard) _model.Dismiss();
            var list = new List<string>(sacrifices);
            ProbBreakdown pre = Ctx.Enhance.Preview(_wandId, list, useCharm);
            if (pre.Blocked != ErrorCode.None)
            {
                if (pre.Blocked == ErrorCode.NotEnoughGold || pre.Blocked == ErrorCode.NotEnoughStone) StartCostShake();
                if (!auto) Screens?.Toast(T(EnhanceNameKeys.ErrorKey(pre.Blocked)));
                return false;
            }
            Result<EnhanceResult> r = Ctx.Enhance.Enhance(_wandId, list, useCharm);
            if (!r.Ok)
            {
                if (!auto) Screens?.Toast(T(EnhanceNameKeys.ErrorKey(r.Error)));
                return false;
            }
            // 이 시도의 소리(마일스톤·+13 종 등)는 결과 단계로 고른다(Audio.json의 stages).
            Ctx.Sound.SetSoundContext(Ids.Char.Of(_character), r.Value.StageAfter);
            _lastComposition.Clear();
            _lastComposition.AddRange(list);
            _lastCharm = useCharm;
            _sacrifices.Clear();
            if (_charmSet && CharmCount() < 1) _charmSet = false;
            var style = EnhanceRitual.StyleFor(_t, r.Value.StageBefore + 1, FastSetting(), FastAllowHigh(), EffectsLevel);
            ResetRitualVisuals();
            _guard.NewAttempt();
            _shownStage = r.Value.StageBefore;
            _slamCount = 0;
            _stageReveals.Clear();
            if (_cont.Armed && !_cont.Running) _cont.Start(list, useCharm, _targetStage);
            if (_cont.Running) _cont.Count(r.Value);
            // 결과 카드에서 곧바로 결과 카드로 가는 시도(연출 감도 최소)도 상태 변화로 잡히게 한다.
            _lastPhase = EnhancePhase.Committing;
            _model.Begin(r.Value, list.Count, style);
            BeginAttemptVisuals();
            if (style == RitualStyle.Full && EnhanceRitual.Intensity(_t, r.Value.StageBefore + 1) >= _t.MasterPeekMinIntensity) SetMaster("master_peek");
            RefreshTop();
            RefreshTally();
            DrainAndApply();
            return true;
        }

        /// <summary>결과 카드의 J: 같은 구성으로 다시 시도(같은 블록이 없으면 같은 등급·속성).</summary>
        void CloseResultCard()
        {
            if (_model.Phase != EnhancePhase.ResultCard) return;
            StopContinue(ContinueStop.Input);
            _model.Dismiss();
            DrainAndApply();
        }

        void Retry()
        {
            if (_model.Phase != EnhancePhase.ResultCard) return;
            var picked = ContinuousEnhanceModel.PickSacrifices(Ctx.Content, Ctx.Profile, _lastComposition);
            if (picked == null) { Screens?.Toast(T(ContinuousEnhanceModel.StopKey(ContinueStop.SacrificeShort))); return; }
            bool charm = ContinuousEnhanceModel.CharmFor(_lastCharm, ProbabilityDisplayModel.DropPossibleOf(_cfg, _wand.Stage, _wand.FloorPeak))
                && CharmCount() > 0;
            TryAttempt(picked, charm, false);
        }

        /// <summary>결과 카드에 닿았을 때 이어서 강화를 멈출지, 다음 시도를 할지(TV-127~129).</summary>
        void ContinueAfterCard()
        {
            if (!_cont.Running) return;
            var stop = ContinuousEnhanceModel.StopAfter(_model.Result, _cont.TargetStage, _cont.StopOnDrop);
            if (stop != ContinueStop.None) { StopContinue(stop); return; }
            _pendingAuto = true;
        }

        void RunAuto()
        {
            _pendingAuto = false;
            if (!_cont.Running || _model.Phase != EnhancePhase.ResultCard || _wand == null) return;
            var picked = ContinuousEnhanceModel.PickSacrifices(Ctx.Content, Ctx.Profile, _cont.Template);
            bool dropPossible = ProbabilityDisplayModel.DropPossibleOf(_cfg, _wand.Stage, _wand.FloorPeak);
            bool charm = ContinuousEnhanceModel.CharmFor(_cont.UseCharm, dropPossible);
            ErrorCode blocked = picked != null ? Ctx.Enhance.Preview(_wandId, picked, charm).Blocked : ErrorCode.None;
            var stop = ContinuousEnhanceModel.StopBefore(picked, blocked);
            if (stop != ContinueStop.None)
            {
                if (blocked == ErrorCode.NotEnoughGold || blocked == ErrorCode.NotEnoughStone) StartCostShake();
                StopContinue(stop);
                return;
            }
            if (!TryAttempt(picked, charm, true)) StopContinue(ContinueStop.CannotAttempt);
        }

        void StopContinue(ContinueStop why)
        {
            if (!_cont.Running) return;
            _cont.Stop(why);
            _pendingAuto = false;
            Screens?.Toast(T(ContinuousEnhanceModel.StopKey(why)));
            RefreshTally();
        }

        void ResolveMutation(bool accept)
        {
            if (_model.Phase != EnhancePhase.MutationChoice || Ctx == null || Ctx.Enhance == null || _model.Result == null) return;
            var r = Ctx.Enhance.ResolveMutation(_wandId, _model.Result.AttemptId, accept);
            if (!r.Ok) { Screens?.Toast(T(EnhanceNameKeys.ErrorKey(r.Error))); return; }
            if (accept) _model.Accept(r.Value); else _model.Reject(r.Value);
            _wand = Ctx.Profile.Wand(_wandId);
            DrainAndApply();
        }

        // ------------------------------------------------------------------ 지팡이 바꾸기·옆 화면

        void SwitchWand(int dir)
        {
            if (Ctx == null || Ctx.Wands == null || _wand == null) return;
            if (!_model.AcceptsSetup && _model.Phase != EnhancePhase.MutationChoice) return;
            var list = Ctx.Wands.List(_character, WandSort.StageDesc);
            if (list.Count <= 1) return;
            int i = list.FindIndex(e => e.Wand.Id == _wandId);
            int j = ((i + dir) % list.Count + list.Count) % list.Count;
            StopContinue(ContinueStop.Input);
            PlaySound(EnhanceSoundIds.WandSwitch, SoundBus.Ui);
            LoadWand(list[j].Wand.Id);
        }

        void SelectMutation(int i)
        {
            if (_wand == null || i >= _wand.Mutations.Count) return;
            _selectedMutation = i;
            RefreshLeft();
        }

        void OpenEngrave()
        {
            if (!PrepareSetup()) return;
            var a = EnhanceNav.EngraveArgs(_wandId);
            a.OnResult = _ => RefreshAll();
            Screens?.Push(ScreenId.SlotEngrave, a);
        }

        void OpenRemoveMutation()
        {
            if (!PrepareSetup() || _wand.Mutations.Count == 0) return;
            int i = Math.Min(_selectedMutation, _wand.Mutations.Count - 1);
            var a = EnhanceNav.MutationRemoveArgs(_wandId, _wand.Mutations[i].Id);
            a.OnResult = _ => { _selectedMutation = 0; RefreshAll(); };
            Screens?.Push(ScreenId.MutationRemove, a);
        }

        void SetTracePublic(bool show)
        {
            if (Ctx == null || Ctx.Enhance == null || _wand == null) return;
            var r = Ctx.Enhance.SetScarVisibility(_wandId, show);
            if (r.Ok) PlaySound(EnhanceSoundIds.TraceToggle, SoundBus.Ui);
            RefreshAll();
        }

        void ToggleTraceFromCard()
        {
            if (_wand == null) return;
            SetTracePublic(!_wand.ShowScars);
        }

        void MoveTarget(int dir)
        {
            if (_wand == null) return;
            _targetStage = Math.Max(_wand.Stage + 1, Math.Min(_cfg.MaxStage, _targetStage + dir));
            if (_cont.Running) _cont.TargetStage = _targetStage;
            RefreshExpected();
        }

        // ------------------------------------------------------------------ 입력

        public override bool OnBack()
        {
            if (_model.Busy) return true;   // 의식 중에는 나가지 않는다(이미 정해진 결과는 저장돼 있다)
            StopContinue(ContinueStop.Input);
            return EnhanceNav.Back(Screens, Args);
        }

        public override bool OnMenuKey(MenuKey key)
        {
            if (_model.Phase == EnhancePhase.MutationChoice)
            {
                if (key == MenuKey.PrevTab || key == MenuKey.NextTab) { SwitchWand(key == MenuKey.PrevTab ? -1 : 1); return true; }
                _cardFocus.HandleKey(key);
                return true;
            }
            if (_model.Busy)
            {
                if (key == MenuKey.Confirm || key == MenuKey.Cancel) StopContinue(ContinueStop.Input);
                if (key == MenuKey.Confirm)
                {
                    if (_model.PressJ() == EnhanceJResult.Skipped) ResetRitualVisuals();
                    DrainAndApply();
                }
                return true;
            }
            if (_model.Phase == EnhancePhase.ResultCard)
            {
                if (key == MenuKey.Confirm) { StopContinue(ContinueStop.Input); Retry(); return true; }
                CloseResultCard();
                if (key == MenuKey.Cancel) return true;
            }
            switch (key)
            {
                case MenuKey.PrevTab: SwitchWand(-1); return true;
                case MenuKey.NextTab: SwitchWand(1); return true;
                case MenuKey.Cancel: RemoveOne(); return true;
                case MenuKey.Confirm:
                    if (ReferenceEquals(Focus.Current, _enhanceButton) && _prob != null && !_prob.CanAfford) StartCostShake();
                    break;
            }
            return base.OnMenuKey(key);
        }

        void OnCursorChanged() { }

        // ------------------------------------------------------------------ 틱

        public override void Tick(float dt)
        {
            if (_model == null) return;
            _acc += Mathf.Min(dt, 0.25f);
            float step = 1f / EnhanceScreenTuning.TicksPerSecond;
            while (_acc >= step)
            {
                _acc -= step;
                TickOnce();
            }
            float visualDt = _hitstopTicks > 0 ? 0f : dt;
            // 느린 화면(높은 등급 성공): 그림 시계만 늦춘다. 상태 기계·시간표의 틱은 그대로 간다.
            if (_slowMoLeft > 0f) { _slowMoLeft -= dt; visualDt *= EnhanceFxStyle.SlowMoScale; }
            _vdt = visualDt;
            _animClock += visualDt;
            _fx?.Update(visualDt);
            RenderFrame();
        }

        /// <summary>화면 틱 하나(60Hz): 상태 기계 → 사건 → 효과 시계.</summary>
        public void TickOnce()
        {
            _model.Tick();
            _guard.Tick();
            if (_hitstopTicks > 0) _hitstopTicks--;
            if (_borderTicks > 0) _borderTicks--;
            if (_costShakeTicks > 0) _costShakeTicks--;
            if (_darkenTicks > 0) _darkenTicks--;
            if (_rollTick >= 0) _rollTick++;
            DrainAndApply();
            if (_pendingAuto) RunAuto();
        }

        void DrainAndApply()
        {
            _cues.Clear();
            _model.DrainCues(_cues);
            for (int i = 0; i < _cues.Count; i++) ApplyCue(_cues[i]);
            if (_model.Phase != _lastPhase)
            {
                var prev = _lastPhase;
                _lastPhase = _model.Phase;
                OnPhaseChanged(prev, _model.Phase);
            }
        }

        void OnPhaseChanged(EnhancePhase from, EnhancePhase to)
        {
            OnVisualPhase(from, to);
            if (to == EnhancePhase.Charge) StartCharge();
            if (from == EnhancePhase.Charge || to == EnhancePhase.Idle || to == EnhancePhase.ResultCard) _fx?.Stop("charge");
            if (to == EnhancePhase.ResultCard)
            {
                ResetRitualVisuals();
                var p = Ctx != null ? Ctx.Profile : null;
                _wand = p != null ? p.Wand(_wandId) : _wand;
                _shownStage = _wand != null ? _wand.Stage : _shownStage;
                RestartIdleLoop(_shownStage);
                if (_master != null) SetMaster("master_sleep");
                RefreshAll();
                ContinueAfterCard();
            }
            else if (to == EnhancePhase.Idle)
            {
                if (_wand != null) _shownStage = _wand.Stage;
                RefreshAll();
            }
            else
            {
                if (to == EnhancePhase.MutationChoice) ResetRitualVisuals();
                RefreshCards();
            }
        }

        /// <summary>지팡이 대기 반복음을 지금 단계로(숨 멈춤이 끊은 뒤 결과 카드에서 다시 켠다). 같은 클립이면 그대로 둔다.</summary>
        void RestartIdleLoop(int stage)
        {
            if (Ctx == null || Ctx.Sound == null) return;
            Ctx.Sound.SetSoundContext(Ids.Char.Of(_character), stage);
            Ctx.Sound.StartLoop(EnhanceSoundIds.WandIdle, EnhanceSoundIds.WandIdle, SoundBus.Wand);
        }

        void PlaySound(string id, SoundBus bus, float pitch = 1f)
        {
            if (Ctx == null || Ctx.Sound == null || string.IsNullOrEmpty(id)) return;
            Ctx.Sound.Play(id, bus, 1f, pitch);
        }

        static string F2(double v) => v.ToString("F2", CultureInfo.InvariantCulture);
        static string F1(double v) => v.ToString("F1", CultureInfo.InvariantCulture);
    }
}
