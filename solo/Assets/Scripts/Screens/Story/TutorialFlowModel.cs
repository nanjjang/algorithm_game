using Wizard.Solo.Data;

namespace Wizard.Solo.Screens.Story
{
    /// <summary>튜토리얼 풍선 한 장의 내용(08 요약서 6.1·6.4절). 글은 화면이 이름 표에서 꺼낸다.</summary>
    public struct TutorialBalloon
    {
        public bool Visible;
        public int Step;
        /// <summary>str.tutorial.step.N</summary>
        public string TitleKey;
        /// <summary>str.tutorial.step.N.body — 이름 표에 값이 없으면 null(본문 줄을 숨긴다, 08 N-05, 00 D-59).</summary>
        public string BodyKey;
        /// <summary>⑤ 블록 끼우기: [조립 열기](J) 버튼을 보인다.</summary>
        public bool ShowOpenLoadout;
        /// <summary>이 단계의 완료가 나와 다음 단계로 넘어가길 기다리는 중(완료 표시).</summary>
        public bool Done;
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
    /// (TutorialStepDone 이벤트). 이 모델은 (가) 풍선 내용, (나) 완료 뒤 "잠깐 뒤" 넘길 명령, (다) 끝 처리 여부만 정한다.
    /// 시간은 시뮬레이션 틱으로 센다(창이 위에 떠 있어도 틱은 간다).
    /// </summary>
    public sealed class TutorialFlowModel
    {
        public const string StepKeyPrefix = "str.tutorial.step.";
        public const string BodySuffix = ".body";

        public readonly int StepCount;
        public readonly int AdvanceDelayTicks;

        /// <summary>완료가 나온 단계(0 = 없음)와 넘길 틱.</summary>
        public int PendingStep { get; private set; }
        public int DueTick { get; private set; }
        /// <summary>끝 처리가 시작됐다(완료·건너뛰기 공통, 한 번만).</summary>
        public bool Finished { get; private set; }

        public TutorialFlowModel(int stepCount, int advanceDelayTicks)
        {
            StepCount = stepCount > 0 ? stepCount : 1;
            AdvanceDelayTicks = advanceDelayTicks > 0 ? advanceDelayTicks : 0;
        }

        public static string TitleKeyOf(int step) => StepKeyPrefix + step;
        public static string BodyKeyOf(int step) => StepKeyPrefix + step + BodySuffix;

        /// <summary>지금 단계(w.Run.TutorialStep)의 풍선. 범위 밖(0, 끝 = 마지막 + 1)이면 숨김.</summary>
        public TutorialBalloon Balloon(int currentStep, INameTable names)
        {
            var b = new TutorialBalloon();
            if (Finished || currentStep < 1 || currentStep > StepCount) return b;
            b.Visible = true;
            b.Step = currentStep;
            b.TitleKey = TitleKeyOf(currentStep);
            string body = BodyKeyOf(currentStep);
            b.BodyKey = names != null && names.Has(body) && !string.IsNullOrEmpty(names.Get(body)) ? body : null;
            b.Done = PendingStep == currentStep;
            b.ShowOpenLoadout = currentStep == StepCount && !b.Done;
            return b;
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

        /// <summary>⑤의 [조립 열기]가 지금 되는가.</summary>
        public bool CanOpenLoadout(int currentStep) => !Finished && currentStep == StepCount && PendingStep != currentStep;
    }
}
