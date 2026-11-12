#nullable enable
namespace Wizard.Unity.App.Core
{
    // 일시정지가 있는 세션(무한 웨이브, 연습). 틱을 멈추는 것은 일시정지뿐이다(T-13)
    public interface IPausableSession
    {
        bool IsPaused { get; }

        // 일시정지를 풀어 달라. 세션이 다음(또는 지금 도는) OnInput 의 꺼내기 뒤에 푼다
        void RequestResume();
    }
}
