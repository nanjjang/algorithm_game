namespace Wizard.ViewLogic.Stack
{
    // spec 13 1.1절. 화면 모델 하나. 뷰(unity/Assets/Game/UI)는 이 모델을 그리기만 한다.
    public interface IScreen
    {
        ScreenId Id { get; }
        ScreenLayer Layer { get; }
        bool BlocksInput { get; }            // Modal 은 이 값과 무관하게 가로막는다(ScreenLayer 주석)
        void OnEnter(ScreenArgs args);
        void OnExit();
        bool OnBack();                       // Esc. true 면 소비. false 면 스택이 이 화면을 닫는다(ScreenStack.Back)
    }
}
