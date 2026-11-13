using Wizard.Core;

namespace Wizard.ViewLogic.Presentation
{
    // 라우터가 바인딩 줄의 shake·hitstop_ms 를 넘기는 곳. Unity 쪽 카메라 연출(M9-PRS-03)이 연출 한도(M9-PRS-02)의
    // 흔들림 누적기·그림 정지 시계에 이어 붙인다. 세기·줄이기·광과민 한도는 받는 쪽이 집행한다. 게임 시간은 멈추지 않는다
    public interface ICameraCueSink
    {
        void Shake(BindingShake size);

        // 맞은 대상(target)과 맞힌 것(source)의 그림만 멈춘다(spec 13 14.4절). 코어 밖 사건이면 둘 다 기본값
        void Hitstop(int durationMs, EntityId source, EntityId target);
    }
}
