using Wizard.Solo.Data;
using Wizard.Solo.Sim.Run;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>튜토리얼 풍선 한 장의 내용. 글은 화면이 이름 표에서 꺼낸다(열쇠만 든다).</summary>
    public struct TutorialBalloon
    {
        public bool Visible;
        public int Step;
        public int Total;
        /// <summary>단계 id(TutorialStepIds).</summary>
        public string StepId;
        /// <summary>str.tutorial.gate.&lt;id&gt;.title</summary>
        public string TitleKey;
        /// <summary>본문(1~2문장). 과열 단계는 잠긴 동안 .body_locked. 이름 표에 없으면 null(줄을 숨긴다).</summary>
        public string BodyKey;
        /// <summary>화면 안 목표 줄 str.tutorial.gate.&lt;id&gt;.goal ({n}, {max}). 없으면 null.</summary>
        public string GoalKey;
        public int Progress;
        /// <summary>진척 막대의 분모. 0이면 막대 없음.</summary>
        public int Goal;
        /// <summary>막혔을 때·실수했을 때 한 줄(null = 숨김).</summary>
        public string HintKey;
        /// <summary>블록 단계: [조립 열기](J) 버튼을 보인다.</summary>
        public bool ShowOpenLoadout;
        /// <summary>완료가 나와 다음 단계를 기다리는 중(칭찬 표시).</summary>
        public bool Done;
        /// <summary>완료 칭찬 한 줄 str.tutorial.gate.&lt;id&gt;.done.</summary>
        public string PraiseKey;
        /// <summary>도입 대사 단계(스승 대사 판을 쓴다).</summary>
        public bool IsIntro;
    }

    public enum TutorialActionKind
    {
        None = 0,
        /// <summary>world.Enqueue(TutorialAdvance, A = Next).</summary>
        Advance = 1,
        /// <summary>TutorialAdvance(A = Next = 마지막 + 1)을 넣고 튜토리얼을 끝낸다.</summary>
        AdvanceAndFinish = 2
    }

    public struct TutorialAction
    {
        public TutorialActionKind Kind;
        public int Next;
        public static readonly TutorialAction None = new TutorialAction();
    }

    /// <summary>
    /// 튜토리얼 흐름(순수). 지금 단계는 시뮬레이션의 w.Run.TutorialStep이 정본이고 완료 판정도 시뮬레이션(Run)이 한다
    /// (TutorialStepDone·PracticeReport 이벤트). 이 모델은 (가) 풍선 내용, (나) 완료 뒤 "잠깐 뒤" 넘길 명령,
    /// (다) 막힘 힌트(진척 없이 hint_after_seconds가 지나면)와 실수 힌트, (라) 도입 대사의 줄 넘김을 정한다.
    /// 시간은 시뮬레이션 틱으로 센다(창이 위에 떠 있어도 틱은 간다).
    /// </summary>
    public sealed class TutorialFlowModel
    {
        public const string GatePrefix = "str.tutorial.gate.";
        public const string IntroLinePrefix = "str.tutorial.intro.line.";
        public const string SetbackPrefix = "str.tutorial.setback.";

        public readonly TutorialPlan Plan;
        public readonly int StepCount;
        public readonly int AdvanceDelayTicks;
        public readonly int HintAfterTicks;
        public readonly int SetbackTicks;

        /// <summary>완료가 나온 단계(0 = 없음)와 넘길 틱.</summary>
        public int PendingStep { get; private set; }
        public int DueTick { get; private set; }
        /// <summary>끝 처리가 시작됐다(완료·건너뛰기 공통, 한 번만).</summary>
        public bool Finished { get; private set; }
        /// <summary>도입 대사의 지금 줄(1부터).</summary>
        public int IntroLine { get; private set; } = 1;

        int _trackedStep = -1;
        int _stepStartTick;
        int _lastProgressTick;
        int _progress, _goal;
        TutorialSetback _setback;
        int _setbackUntil = -1;
        bool _fizzled;

        public TutorialFlowModel(TutorialPlan plan, int advanceDelayTicks, int hintAfterTicks, int setbackTicks)
        {
            Plan = plan ?? new TutorialPlan();
            StepCount = Plan.Count > 0 ? Plan.Count : 1;
            AdvanceDelayTicks = advanceDelayTicks > 0 ? advanceDelayTicks : 0;
            HintAfterTicks = hintAfterTicks > 0 ? hintAfterTicks : 0;
            SetbackTicks = setbackTicks > 0 ? setbackTicks : 0;
        }

        public int IntroLineCount => Plan.IntroLines > 0 ? Plan.IntroLines : 1;

        public static string TitleKeyOf(string id) => GatePrefix + id + ".title";
        public static string BodyKeyOf(string id) => GatePrefix + id + ".body";
        public static string LockedBodyKeyOf(string id) => GatePrefix + id + ".body_locked";
        public static string GoalKeyOf(string id) => GatePrefix + id + ".goal";
        public static string HintKeyOf(string id) => GatePrefix + id + ".hint";
        public static string DoneKeyOf(string id) => GatePrefix + id + ".done";
        public static string IntroLineKey(int n) => IntroLinePrefix + n;
        public static string SetbackKey(TutorialSetback why) =>
            why == TutorialSetback.Overflow ? SetbackPrefix + "overflow" :
            why == TutorialSetback.Hit ? SetbackPrefix + "hit" :
            why == TutorialSetback.Fizzle ? SetbackPrefix + "fizzle" : null;

        /// <summary>단계가 바뀌었는지 보고 단계별 기억(진척·힌트 시계·실수)을 비운다. 매 프레임 지금 단계·틱으로 부른다.</summary>
        public void Track(int currentStep, int tick)
        {
            if (currentStep == _trackedStep) return;
            _trackedStep = currentStep;
            _stepStartTick = tick;
            _lastProgressTick = tick;
            _progress = 0;
            _goal = 0;
            _setback = TutorialSetback.None;
            _setbackUntil = -1;
            _fizzled = false;
            if (currentStep != Plan.StepOf(TutorialStepIds.Intro)) IntroLine = 1;
        }

        /// <summary>PracticeReport(tutorial.progress). 진척이 오르면 막힘 시계를 다시 잰다.</summary>
        public void OnProgress(int step, int value, int goal, int tick)
        {
            if (step < _trackedStep) return;
            Track(step, tick);
            if (value > _progress) _lastProgressTick = tick;
            _progress = value;
            _goal = goal;
        }

        /// <summary>PracticeReport(tutorial.setback) 또는 화면이 본 불발. 그 순간부터 setback_hint_seconds 동안 그 힌트를 보인다.</summary>
        public void OnSetback(int step, TutorialSetback why, int tick)
        {
            if (step < _trackedStep) return;
            Track(step, tick);
            if (why == TutorialSetback.None) return;
            _setback = why;
            _setbackUntil = tick + SetbackTicks;
            if (why == TutorialSetback.Fizzle) _fizzled = true;
        }

        /// <summary>지금 실수 힌트가 떠 있나.</summary>
        public bool SetbackActive(int tick) => _setback != TutorialSetback.None && tick < _setbackUntil;
        public TutorialSetback Setback => _setback;
        public bool SawFizzle => _fizzled;

        /// <summary>진척 없이 hint_after_seconds가 지났나(막힘). 완료 대기 중·도입 대사 중에는 아니다.</summary>
        public bool Stuck(int tick) => HintAfterTicks > 0 && tick - _lastProgressTick >= HintAfterTicks;

        /// <summary>지금 단계(w.Run.TutorialStep)의 풍선. 범위 밖(0, 끝 = 마지막 + 1)이면 숨김. locked = 플레이어가 과열 잠금 중.</summary>
        public TutorialBalloon Balloon(int currentStep, int tick, INameTable names, bool locked)
        {
            var b = new TutorialBalloon();
            if (Finished || currentStep < 1 || currentStep > StepCount) return b;
            Track(currentStep, tick);
            string id = Plan.IdOf(currentStep);
            if (id == null) return b;
            b.Visible = true;
            b.Step = currentStep;
            b.Total = StepCount;
            b.StepId = id;
            b.IsIntro = id == TutorialStepIds.Intro;
            b.TitleKey = TitleKeyOf(id);
            string body = locked && id == TutorialStepIds.Overheat && Has(names, LockedBodyKeyOf(id)) ? LockedBodyKeyOf(id) : BodyKeyOf(id);
            b.BodyKey = Has(names, body) ? body : null;
            b.GoalKey = Has(names, GoalKeyOf(id)) ? GoalKeyOf(id) : null;
            b.Progress = _progress;
            b.Goal = _goal;
            b.Done = PendingStep == currentStep;
            b.PraiseKey = b.Done && Has(names, DoneKeyOf(id)) ? DoneKeyOf(id) : null;
            b.ShowOpenLoadout = id == TutorialStepIds.Block && !b.Done;
            if (!b.Done && !b.IsIntro)
            {
                if (SetbackActive(tick)) b.HintKey = SetbackKey(_setback);
                else if (Stuck(tick) && Has(names, HintKeyOf(id))) b.HintKey = HintKeyOf(id);
            }
            return b;
        }

        static bool Has(INameTable names, string key) => names != null && key != null && names.Has(key) && !string.IsNullOrEmpty(names.Get(key));

        /// <summary>
        /// 도입 대사에서 J: 다음 줄. 마지막 줄 뒤면 다음 단계로 넘기는 명령을 돌려준다(완료 판정이 없는 단계).
        /// </summary>
        public TutorialAction NextIntroLine(int currentStep)
        {
            if (Finished || Plan.IdOf(currentStep) != TutorialStepIds.Intro) return TutorialAction.None;
            if (IntroLine < IntroLineCount) { IntroLine++; return TutorialAction.None; }
            int next = currentStep + 1;
            if (next > StepCount)
            {
                Finished = true;
                return new TutorialAction { Kind = TutorialActionKind.AdvanceAndFinish, Next = next };
            }
            return new TutorialAction { Kind = TutorialActionKind.Advance, Next = next };
        }

        /// <summary>
        /// TutorialStepDone(step)을 받았다. 지금 단계의 완료면 넘김을 예약하고 true. 마지막 단계면 lastStep = true
        /// (화면은 조립 화면을 PopTo(Tutorial)로 닫는다).
        /// </summary>
        public bool OnStepDone(int step, int currentStep, int tick, out bool lastStep)
        {
            lastStep = false;
            if (Finished || step < 1 || step > StepCount) return false;
            if (step != currentStep || PendingStep == step) return false;
            PendingStep = step;
            DueTick = tick + AdvanceDelayTicks;
            lastStep = step == StepCount;
            return true;
        }

        /// <summary>매 프레임 지금 틱으로 본다. 예약한 넘김의 때가 됐으면 명령을 돌려준다(한 번).</summary>
        public TutorialAction Poll(int nowTick)
        {
            if (Finished || PendingStep == 0 || nowTick < DueTick) return TutorialAction.None;
            int next = PendingStep + 1;
            bool last = PendingStep == StepCount;
            PendingStep = 0;
            if (last)
            {
                Finished = true;
                return new TutorialAction { Kind = TutorialActionKind.AdvanceAndFinish, Next = next };
            }
            return new TutorialAction { Kind = TutorialActionKind.Advance, Next = next };
        }

        /// <summary>건너뛰기 확인(또는 그 밖의 끝). 처음이면 true.</summary>
        public bool RequestFinish()
        {
            if (Finished) return false;
            Finished = true;
            PendingStep = 0;
            return true;
        }

        /// <summary>블록 단계의 [조립 열기]가 지금 되는가.</summary>
        public bool CanOpenLoadout(int currentStep) =>
            !Finished && Plan.IdOf(currentStep) == TutorialStepIds.Block && PendingStep != currentStep;
    }
}
