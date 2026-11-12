namespace Wizard.ViewLogic.Stack
{
    // ScreenStack 이 ScreenId 로 화면 모델을 만든다. 조립하는 쪽(Unity 의 CompositionRoot, 테스트)이 구현한다.
    // 돌려준 화면의 Id 는 요청한 id 와 같아야 한다.
    public interface IScreenFactory
    {
        IScreen Create(ScreenId id);
    }
}
