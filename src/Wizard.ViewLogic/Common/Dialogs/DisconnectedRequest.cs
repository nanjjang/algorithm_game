using System;

namespace Wizard.ViewLogic.Common.Dialogs
{
    // Disconnected 를 열 때 주는 값. ReconnectRemainingUiTicks > 0 이면 다시 잇는 중이고 남은 시간을 보인다
    public sealed class DisconnectedRequest
    {
        public DisconnectedRequest(int reconnectRemainingUiTicks = 0, Action<DialogButtonRole>? onChosen = null)
        {
            ReconnectRemainingUiTicks = reconnectRemainingUiTicks;
            OnChosen = onChosen;
        }

        public int ReconnectRemainingUiTicks { get; }
        public Action<DialogButtonRole>? OnChosen { get; }
    }
}
