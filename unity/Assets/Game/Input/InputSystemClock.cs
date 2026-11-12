using UnityEngine.InputSystem.LowLevel;

namespace Wizard.Unity.Input
{
    // Input System 의 시간 축(InputState.currentTime). 키 이벤트의 시각(InputEvent.time)이 이 축에 있으므로 틱 시계도 이것을 쓴다.
    // Time.realtimeSinceStartupAsDouble 과 같은 축이고 Time.timeScale 과 무관하다. 테스트 고정 장치(InputTestFixture)가 바꾼 시각도 따른다.
    public sealed class InputSystemClock : IMonotonicClock
    {
        public double NowSeconds => InputState.currentTime;
    }
}
