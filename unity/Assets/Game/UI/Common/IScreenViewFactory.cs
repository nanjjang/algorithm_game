#nullable enable
using Wizard.ViewLogic.Stack;

namespace Wizard.Unity.UI.Common
{
    // ScreenHost 가 화면 모델 하나의 뷰를 만든다. 화면 뷰 작업(UI/Screens/<화면>/)이 ScreenViewRegistry 에 자기 뷰를 등록한다
    public interface IScreenViewFactory
    {
        UiScreenView Create(IScreen screen, UiViewContext context);
    }
}
