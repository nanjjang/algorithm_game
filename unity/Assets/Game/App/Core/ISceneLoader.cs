#nullable enable
namespace Wizard.Unity.App.Core
{
    // 장면 바꾸기의 엔진 쪽. 테스트는 가짜를 꽂는다
    public interface ISceneLoader
    {
        void Load(string sceneName);
    }
}
