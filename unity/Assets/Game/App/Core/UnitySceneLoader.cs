#nullable enable
using UnityEngine.SceneManagement;

namespace Wizard.Unity.App.Core
{
    // 장면 하나만 둔다(Single). 화면은 장면이 아니라 ScreenStack 으로 바꾼다(ARCHITECTURE 4.3)
    public sealed class UnitySceneLoader : ISceneLoader
    {
        public void Load(string sceneName) => SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }
}
