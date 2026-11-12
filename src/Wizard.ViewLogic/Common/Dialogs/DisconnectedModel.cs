using System;
using Wizard.ViewLogic.Stack;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // spec 13 1.5절 Disconnected: "까마귀가 돌아오지 않는다.", 다시 잇는 중이면 남은 시간,
    // 버튼 2개 [임시: 다시 시도 / 오프라인 프로필로], 이미 시작한 판은 끝까지 하고 나중에 낸다는 안내(on 8.2, on 3.2).
    // Modal. 취소 버튼이 없어 Esc 는 소비만 한다. 다시 잇는 일 자체는 이 창이 하지 않는다(고른 결과를 요청한 쪽이 처리한다).
    public sealed class DisconnectedModel : DialogScreen
    {
        public DisconnectedModel(ScreenStack stack, InputModeTracker mode)
            : base(stack, mode)
        {
        }

        public override ScreenId Id => ScreenId.Disconnected;
        public override ScreenLayer Layer => ScreenLayer.Modal;

        public string MessageKey => CommonTextKeys.DisconnectedMessage;
        public string RunNoteKey => CommonTextKeys.DisconnectedRunNote;
        public string ReconnectingKey => CommonTextKeys.DisconnectedReconnecting;

        public int ReconnectRemainingUiTicks { get; private set; }

        public bool IsReconnecting => ReconnectRemainingUiTicks > 0;

        // 화면에 보일 남은 초(올림). 다시 잇는 중이 아니면 0
        public int ReconnectRemainingSeconds =>
            (ReconnectRemainingUiTicks + UiTime.UiTicksPerSecond - 1) / UiTime.UiTicksPerSecond;

        public void SetReconnecting(int remainingUiTicks) => ReconnectRemainingUiTicks = Math.Max(0, remainingUiTicks);

        public void Tick()
        {
            if (ReconnectRemainingUiTicks > 0)
            {
                ReconnectRemainingUiTicks--;
            }
            if (IsOpen)
            {
                Group.Tick();
            }
        }

        protected override Action<DialogButtonRole>? Setup(ScreenArgs args)
        {
            var r = args.Payload as DisconnectedRequest;
            ReconnectRemainingUiTicks = Math.Max(0, r?.ReconnectRemainingUiTicks ?? 0);
            AddButton(DialogButtonRole.Retry, CommonTextKeys.DisconnectedRetry);
            AddButton(DialogButtonRole.SwitchToOffline, CommonTextKeys.DisconnectedGoOffline);
            return r?.OnChosen;
        }
    }
}
