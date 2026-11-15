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

        /// <summary>쓰러진 캐릭터가 화면의 어느 높이에 있는가(뷰포트 y, 0 = 아래, 1 = 위, 모르면 −1). 사망 글이 캐릭터를 가리지 않게 BattleScreen이 읽는다.</summary>
        public float SubjectViewportY { get; set; } = -1f;

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

        public void ClearFocus() { _focusLeft = 0f; FocusWeight = 0f; TimeScale = 1f; ZoomHold = 1f; ClampSlack = 0f; }

        /// <summary>카메라가 전장 경계 밖으로 더 갈 수 있는 거리(칸). 쓰러짐 연출이 구석의 캐릭터를 가운데로 데려올 때.</summary>
        public float ClampSlack { get; private set; }
        public void SetClampSlack(float cells) { ClampSlack = float.IsNaN(cells) ? 0f : Mathf.Max(0f, cells); }

        /// <summary>
        /// 표시 시간 배율(0~1, 1 = 보통). 쓰러짐 슬로모션 같은 데 쓴다. 호스트가 표시 층에 넘기는 dt에만 곱한다 —
        /// 시뮬레이션 틱은 고정 틱 누적기 그대로 흐른다(규칙·결정론과 무관). 층이 매 프레임 넣고, 판이 바뀌면(ClearFocus) 1로 돌아간다.
        /// </summary>
        public float TimeScale { get; private set; } = 1f;
        public void SetTimeScale(float scale) { TimeScale = float.IsNaN(scale) ? 1f : Mathf.Clamp01(scale); }

        /// <summary>붙잡아 두는 줌 배율(1 = 없음, 1보다 크면 당김). 동적 줌 목표에 곱한다(CameraDirector가 ZoomRate로 천천히 따라간다).</summary>
        public float ZoomHold { get; private set; } = 1f;
        public void SetZoomHold(float mul) { ZoomHold = float.IsNaN(mul) || mul <= 0f ? 1f : mul; }
    }
}
