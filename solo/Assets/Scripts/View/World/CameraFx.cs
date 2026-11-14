using UnityEngine;

namespace Wizard.Solo.View
{
    /// <summary>
    /// 표시 층 → 카메라 연출 요청 창구(줌 펀치, 잠깐 다른 곳 보기). WorldViewHost가 전장 카메라에 붙이고 Director를 넣는다.
    /// ViewContext에 자리를 더하지 않고(공용 계약을 바꾸지 않는다) vc.Camera에서 찾는다: CameraFx.Of(vc).
    /// 카메라가 없거나(EditMode 시험) 붙어 있지 않으면 null — 부르는 쪽은 null이면 그냥 넘어간다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFx : MonoBehaviour
    {
        public CameraDirector Director;

        /// <summary>잠깐 볼 곳(view 좌표)과 끝나는 표시 시각. 보스 등장 같은 데 쓴다.</summary>
        public Vector2 FocusPoint { get; private set; }
        public float FocusWeight { get; private set; }
        float _focusLeft;

        public static CameraFx Of(ViewContext vc)
        {
            if (vc == null || vc.Camera == null) return null;
            return vc.Camera.GetComponent<CameraFx>();
        }

        public void Punch(float amount)
        {
            if (Director != null) Director.AddPunch(amount);
        }

        /// <summary>seconds 동안 따라가는 점을 focus 쪽으로 weight(0~1)만큼 당긴다.</summary>
        public void Focus(Vector2 viewPoint, float weight, float seconds)
        {
            FocusPoint = viewPoint;
            FocusWeight = Mathf.Clamp01(weight);
            _focusLeft = Mathf.Max(_focusLeft, seconds);
        }

        /// <summary>호스트가 매 프레임 부른다. 남은 잠깐 보기의 무게(0이면 없음).</summary>
        public float TickFocus(float dt)
        {
            if (_focusLeft <= 0f) return 0f;
            _focusLeft -= dt;
            if (_focusLeft <= 0f) { _focusLeft = 0f; FocusWeight = 0f; }
            return FocusWeight;
        }

        public void ClearFocus() { _focusLeft = 0f; FocusWeight = 0f; }
    }
}
