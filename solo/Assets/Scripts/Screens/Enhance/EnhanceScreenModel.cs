using System.Collections.Generic;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Enhance
{
    /// <summary>의식 중 J를 눌렀을 때 일어난 일.</summary>
    public enum EnhanceJResult
    {
        /// <summary>대기 화면: 화면이 넣기·강화로 처리한다.</summary>
        None,
        /// <summary>무시(누름 뒤 0.3초 안, 변이 카드, +13 첫 도달 처음 2초).</summary>
        Ignored,
        /// <summary>판정으로 넘어가 짧은 결과(또는 +13 연출의 처음)로.</summary>
        Skipped,
        /// <summary>결과 카드: 같은 구성으로 다시 시도(화면이 조건을 검사하고 Begin을 부른다).</summary>
        Retry
    }

    /// <summary>
    /// 강화 화면 상태 기계와 의식 시간표(08 요약서 10.5~10.8절, 07 5.2~5.4절). 엔진 없는 순수 클래스, 화면 틱(60Hz) 단위.
    ///
    /// 결과는 Begin에 넘긴 EnhanceResult가 정한다 — 이 모델은 그것을 읽어 연출 순서·길이·소리 사건만 정하고 결과를 바꾸지 않는다.
    /// 판정 전 구간(Payment~Hold)은 결과를 보지 않고 만든다(RitualTimeline) → 충전 길이·소리는 결과와 무관하게 같다.
    /// 화면은 Tick()을 60Hz로 부르고, DrainCues로 나온 사건을 ctx.Sound.Play·화면 효과로 낸다.
    /// </summary>
    public sealed class EnhanceScreenModel
    {
        readonly EnhanceScreenTuning _t;
        readonly int _maxStage;
        readonly List<EnhanceSegment> _queue = new List<EnhanceSegment>();
        readonly List<EnhanceCue> _out = new List<EnhanceCue>();
        EnhanceSegment _seg;
        int _cueIndex;

        public EnhanceScreenModel(EnhanceScreenTuning tuning, int maxStage)
        {
            _t = tuning ?? new EnhanceScreenTuning();
            _maxStage = maxStage;
        }

        public EnhanceScreenTuning Tuning => _t;
        public EnhancePhase Phase { get; private set; } = EnhancePhase.Idle;
        /// <summary>지금 구간에 들어온 뒤 지난 틱.</summary>
        public int PhaseTick { get; private set; }
        /// <summary>지금 구간 길이(틱). 대기·결과 카드·변이 카드는 0(끝없음).</summary>
        public int PhaseLength => _seg != null ? _seg.Length : 0;
        /// <summary>지금 결과 연출 종류(G23 result_events 열쇠).</summary>
        public string PhaseKind => _seg != null ? _seg.Kind : null;
        /// <summary>강화 버튼을 누른 뒤 지난 틱(시도가 없으면 −1).</summary>
        public int AttemptTick { get; private set; } = -1;
        /// <summary>이번 시도의 결과(Begin이 받은 것 — 연출 전에 이미 정해져 있다).</summary>
        public EnhanceResult Result { get; private set; }
        /// <summary>변이 결정의 결과(받음/거절). 없으면 null.</summary>
        public EnhanceResult Decision { get; private set; }
        public RitualTimeline Timeline { get; private set; }
        public RitualStyle Style { get; private set; }
        public bool Plus13FirstReach { get; private set; }
        /// <summary>지나온 상태(시험·기록용). Begin마다 비운다.</summary>
        public readonly List<EnhancePhase> History = new List<EnhancePhase>();

        /// <summary>제물·부적을 넣고 빼거나 지팡이를 바꿀 수 있는 때(대기, 결과 카드).</summary>
        public bool AcceptsSetup => Phase == EnhancePhase.Idle || Phase == EnhancePhase.ResultCard;
        /// <summary>의식·연출 중(화면의 다른 조작이 잠긴다).</summary>
        public bool Busy => !AcceptsSetup && Phase != EnhancePhase.MutationChoice;
        /// <summary>지금 화면에 보이는 결과 = 변이 결정이 있으면 그것, 없으면 시도 결과.</summary>
        public EnhanceResult Shown => Decision ?? Result;

        /// <summary>충전 게이지 0..1(충전 전 0, 충전 뒤 1).</summary>
        public double Gauge
        {
            get
            {
                if (Timeline == null || AttemptTick < 0) return 0;
                if (Phase == EnhancePhase.Payment || Phase == EnhancePhase.Offering) return 0;
                if (Phase == EnhancePhase.Charge)
                {
                    int len = Timeline.ChargeTicks;
                    return len <= 0 ? 1 : EnhanceRitual.ChargeGauge(_t, PhaseTick / (double)len);
                }
                return Phase == EnhancePhase.Idle ? 0 : 1;
            }
        }

        // ------------------------------------------------------------------ 입력

        /// <summary>
        /// 시도를 시작한다(Idle·ResultCard에서만). result는 이미 IEnhanceService.Enhance가 돌려준 것이다.
        /// Committing → (오프라인이라 기다림 없이) Payment → Offering → Charge → Hold → Judge → 결과 연출.
        /// </summary>
        public bool Begin(EnhanceResult result, int sacrificeCount, RitualStyle style)
        {
            if (result == null || !AcceptsSetup) return false;
            History.Clear();
            _queue.Clear();
            Result = result;
            Decision = null;
            Style = style;
            Plus13FirstReach = EnhanceResultPlan.ReachesPlus13(result, _maxStage) && EnhanceResultPlan.Plus13First(result);
            int target = result.StageBefore + 1;
            Timeline = EnhanceRitual.Build(_t, target, sacrificeCount, style);
            AttemptTick = 0;
            Enter(new EnhanceSegment { Phase = EnhancePhase.Committing });

            var tl = Timeline;
            var segs = new[]
            {
                new EnhanceSegment { Phase = EnhancePhase.Payment, Length = tl.OfferStart - tl.PayStart },
                new EnhanceSegment { Phase = EnhancePhase.Offering, Length = tl.ChargeStart - tl.OfferStart },
                new EnhanceSegment { Phase = EnhancePhase.Charge, Length = tl.HoldStart - tl.ChargeStart },
                new EnhanceSegment { Phase = EnhancePhase.Hold, Length = tl.JudgeTick - tl.HoldStart },
                new EnhanceSegment { Phase = EnhancePhase.Judge },
            };
            int[] starts = { tl.PayStart, tl.OfferStart, tl.ChargeStart, tl.HoldStart, tl.JudgeTick };
            // 사건 하나는 그 틱을 품은 첫 구간 하나에만 들어간다(길이 0인 구간은 건너뛴다. 판정 시각의 사건은 Judge).
            foreach (var c in tl.Cues)
            {
                int k = segs.Length - 1;
                for (int i = 0; i < segs.Length - 1; i++)
                    if (c.Tick >= starts[i] && c.Tick < starts[i] + segs[i].Length) { k = i; break; }
                var cc = c;
                cc.Tick = c.Tick - starts[k];
                segs[k].Cues.Add(cc);
            }
            _queue.AddRange(segs);
            Advance();
            return true;
        }

        /// <summary>화면을 열었을 때 결정 대기 중인 변이 제시가 있으면 변이 카드부터(TV-M-07). offerResult = 제시를 꾸러미 꼴로 옮긴 것.</summary>
        public void ResumePendingOffer(EnhanceResult offerResult)
        {
            History.Clear();
            _queue.Clear();
            Result = offerResult;
            Decision = null;
            Timeline = null;
            AttemptTick = -1;
            Style = RitualStyle.Full;
            Plus13FirstReach = false;
            Enter(new EnhanceSegment { Phase = EnhancePhase.MutationChoice });
        }

        /// <summary>J(넣기·강화·건너뛰기·다시 시도).</summary>
        public EnhanceJResult PressJ()
        {
            switch (Phase)
            {
                case EnhancePhase.Idle: return EnhanceJResult.None;
                case EnhancePhase.ResultCard: return EnhanceJResult.Retry;
                case EnhancePhase.MutationChoice: return EnhanceJResult.Ignored;   // TV-109: 변이 카드는 건너뛸 수 없다
                case EnhancePhase.Plus13:
                    // TV-110: 첫 도달의 처음 2초(120틱)는 넘길 수 없다. 그 뒤는 결과 카드로.
                    if (Plus13FirstReach && Style != RitualStyle.CardOnly && PhaseTick < EnhanceScreenTuning.Ticks(_t.Plus13LockSeconds))
                        return EnhanceJResult.Ignored;
                    _queue.Clear();
                    Enter(new EnhanceSegment { Phase = EnhancePhase.ResultCard });
                    return EnhanceJResult.Skipped;
                case EnhancePhase.ShortResult:
                case EnhancePhase.MutationAccept:
                case EnhancePhase.MutationRejectReplay:
                    return EnhanceJResult.Ignored;
            }
            // 의식(Payment~Hold)과 결과 연출 중: 누름 뒤 0.3초는 무시(TV-107·108).
            if (AttemptTick < EnhanceScreenTuning.Ticks(_t.SkipIgnoreSeconds)) return EnhanceJResult.Ignored;
            Skip();
            return EnhanceJResult.Skipped;
        }

        /// <summary>변이 받기. decision = IEnhanceService.ResolveMutation(accept: true)의 결과.</summary>
        public bool Accept(EnhanceResult decision)
        {
            if (Phase != EnhancePhase.MutationChoice || decision == null) return false;
            Decision = decision;
            _queue.Clear();
            int intensity = Intensity;
            if (Style == RitualStyle.CardOnly)
            {
                Enter(new EnhanceSegment { Phase = EnhancePhase.ResultCard });
                return true;
            }
            var seg = EnhanceResultPlan.Segment(_t, EnhancePhase.MutationAccept, EnhanceResultPlan.KindMutationAccepted, decision, intensity);
            _queue.Add(new EnhanceSegment { Phase = EnhancePhase.ResultCard });
            Enter(seg);
            return true;
        }

        /// <summary>변이 거절. decision = ResolveMutation(accept: false)의 결과(fallback이 적용된 것). 그 결과의 0.50 이후를 재생한다(TV-123).</summary>
        public bool Reject(EnhanceResult decision)
        {
            if (Phase != EnhancePhase.MutationChoice || decision == null) return false;
            Decision = decision;
            _queue.Clear();
            if (Style == RitualStyle.CardOnly)
            {
                Enter(new EnhanceSegment { Phase = EnhancePhase.ResultCard });
                return true;
            }
            string kind = EnhanceResultPlan.KindOf(decision.Outcome);
            var seg = EnhanceResultPlan.Segment(_t, EnhancePhase.MutationRejectReplay, kind, decision, Intensity, _t.RejectReplayFromSeconds);
            seg.Cues.Insert(0, new EnhanceCue { Tick = 0, EventId = EnhanceSoundIds.MutationReject, Bus = SoundBus.Ui, Pitch = 1f, Fx = EnhanceFx.Vine });
            _queue.Add(new EnhanceSegment { Phase = EnhancePhase.ResultCard });
            Enter(seg);
            return true;
        }

        /// <summary>결과 카드를 닫고 대기로(K, 방향, 제물 넣기 등). 결과 카드가 아니면 아무 일 없다.</summary>
        public void Dismiss()
        {
            if (Phase != EnhancePhase.ResultCard) return;
            _queue.Clear();
            Enter(new EnhanceSegment { Phase = EnhancePhase.Idle });
        }

        /// <summary>화면을 나가거나 지팡이를 바꿀 때(이미 정해진 결과는 서비스가 저장했다). 변이 카드는 프로필에 남아 다음에 다시 뜬다.</summary>
        public void Reset()
        {
            _queue.Clear();
            Result = null;
            Decision = null;
            Timeline = null;
            AttemptTick = -1;
            History.Clear();
            Enter(new EnhanceSegment { Phase = EnhancePhase.Idle });
        }

        /// <summary>화면 틱 하나.</summary>
        public void Tick()
        {
            if (AttemptTick >= 0) AttemptTick++;
            if (_seg == null || _seg.Length <= 0) return;   // 끝없는 구간(대기·카드·변이 카드)
            PhaseTick++;
            EmitUpTo(PhaseTick);
            if (PhaseTick >= _seg.Length) Advance();
        }

        /// <summary>지난 호출 뒤 나온 사건을 into에 옮긴다.</summary>
        public void DrainCues(List<EnhanceCue> into)
        {
            into.AddRange(_out);
            _out.Clear();
        }

        /// <summary>이번 시도의 강도(도전 단계로).</summary>
        public int Intensity => Timeline != null ? Timeline.Intensity : (Result != null ? EnhanceRitual.Intensity(_t, Result.StageBefore + 1) : 0);

        // ------------------------------------------------------------------ 안쪽

        void Enter(EnhanceSegment seg)
        {
            _seg = seg;
            _cueIndex = 0;
            PhaseTick = 0;
            Phase = seg.Phase;
            bool skipZeroRitual = seg.Length <= 0 && seg.Cues.Count == 0 && IsRitual(seg.Phase);
            if (!skipZeroRitual) History.Add(seg.Phase);
            if (seg.Phase == EnhancePhase.ResultCard)
                _out.Add(new EnhanceCue { Tick = 0, EventId = EnhanceSoundIds.ResultCard, Bus = SoundBus.Ui, Pitch = 1f });
            EmitUpTo(0);
        }

        static bool IsRitual(EnhancePhase p) =>
            p == EnhancePhase.Payment || p == EnhancePhase.Offering || p == EnhancePhase.Charge || p == EnhancePhase.Hold;

        void EmitUpTo(int tick)
        {
            if (_seg == null) return;
            var cues = _seg.Cues;
            while (_cueIndex < cues.Count && cues[_cueIndex].Tick <= tick)
            {
                _out.Add(cues[_cueIndex]);
                _cueIndex++;
            }
        }

        /// <summary>지금 구간이 끝났다: 다음 구간으로(길이 0인 구간은 그 자리에서 지나간다).</summary>
        void Advance()
        {
            while (true)
            {
                if (_seg != null && _seg.Phase == EnhancePhase.Judge) { PlanAfterJudge(); }
                if (_queue.Count == 0) return;
                var next = _queue[0];
                _queue.RemoveAt(0);
                Enter(next);
                bool endless = next.Phase == EnhancePhase.Idle || next.Phase == EnhancePhase.ResultCard || next.Phase == EnhancePhase.MutationChoice;
                if (endless || next.Length > 0) return;
            }
        }

        /// <summary>판정 뒤: 결과 연출 → (변이 카드) → (마일스톤 / +13) → 결과 카드.</summary>
        void PlanAfterJudge()
        {
            _queue.Clear();
            var r = Result;
            int intensity = Intensity;
            if (Style == RitualStyle.CardOnly)
            {
                _queue.Add(new EnhanceSegment { Phase = r.Outcome == EnhanceOutcome.MutationOffered ? EnhancePhase.MutationChoice : EnhancePhase.ResultCard });
                return;
            }
            if (Style == RitualStyle.Fast)
            {
                AddShortTail(intensity);
                return;
            }
            string kind = EnhanceResultPlan.KindOf(r.Outcome);
            bool plus13 = EnhanceResultPlan.ReachesPlus13(r, _maxStage);
            double cut = plus13 ? EnhanceResultPlan.Plus13Cut(_t, r) : 0;
            _queue.Add(EnhanceResultPlan.Segment(_t, EnhancePhase.ResultPlay, kind, r, intensity, 0, cut));
            if (r.Outcome == EnhanceOutcome.MutationOffered)
            {
                _queue.Add(new EnhanceSegment { Phase = EnhancePhase.MutationChoice });
                return;
            }
            if (plus13)
                _queue.Add(EnhanceResultPlan.Plus13Segment(_t, Plus13FirstReach, intensity));
            else
            {
                int ms = EnhanceResultPlan.MilestoneTicks(_t, r, _maxStage);
                if (ms > 0)
                {
                    string mk = r.MilestoneFirstTime ? EnhanceResultPlan.KindMilestoneFirst : EnhanceResultPlan.KindMilestoneAgain;
                    var seg = EnhanceResultPlan.Segment(_t, EnhancePhase.Milestone, mk, r, intensity);
                    seg.Length = ms;
                    _queue.Add(seg);
                }
            }
            _queue.Add(new EnhanceSegment { Phase = EnhancePhase.ResultCard });
        }

        /// <summary>짧은 형태(0.5초) 뒤: 변이면 변이 카드, 아니면 결과 카드.</summary>
        void AddShortTail(int intensity)
        {
            var r = Result;
            string kind = r.Outcome == EnhanceOutcome.GreatSuccess ? EnhanceResultPlan.KindShortGreat : EnhanceResultPlan.KindShort;
            var seg = EnhanceResultPlan.Segment(_t, EnhancePhase.ShortResult, kind, r, intensity);
            seg.Length = EnhanceScreenTuning.Ticks(_t.ShortResultSeconds);
            _queue.Add(seg);
            _queue.Add(new EnhanceSegment { Phase = r.Outcome == EnhanceOutcome.MutationOffered ? EnhancePhase.MutationChoice : EnhancePhase.ResultCard });
        }

        /// <summary>건너뛰기: 판정으로 넘어가 짧은 결과. +13 첫 도달이면 +13 연출의 처음(못 넘기는 2초)부터.</summary>
        void Skip()
        {
            _queue.Clear();
            var r = Result;
            int intensity = Intensity;
            bool ritual = IsRitual(Phase) || Phase == EnhancePhase.Committing;
            if (ritual) History.Add(EnhancePhase.Judge);
            if (Plus13FirstReach && Style != RitualStyle.CardOnly)
            {
                _queue.Add(EnhanceResultPlan.Plus13Segment(_t, true, intensity));
                _queue.Add(new EnhanceSegment { Phase = EnhancePhase.ResultCard });
            }
            else if (Phase == EnhancePhase.Milestone)
            {
                _queue.Add(new EnhanceSegment { Phase = EnhancePhase.ResultCard });
            }
            else
            {
                AddShortTail(intensity);
            }
            _seg = null;
            Advance();
        }
    }
}
