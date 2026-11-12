#nullable enable
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wizard.Unity.App.Core
{
    // 장면의 시작 개체(T-32: 장면마다 "Bootstrap" 하나만 있다)에 붙는 입구. 장면 파일에는 스크립트가 없고, 이 코드가 불러온 장면마다
    // 그 개체를 찾아 붙인다(장면을 편집하지 않는다). 붙으면 조립 루트를 보장하고 장면에 들어왔다고 알린다.
    // 시작 개체가 없는 장면(테스트 실행기의 장면 등)에는 아무것도 하지 않는다.
    public sealed class Bootstrap : MonoBehaviour
    {
        public const string StartObjectName = "Bootstrap";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;    // 편집기에서 도메인을 다시 읽지 않고 재생할 때 두 번 붙지 않게
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // 첫 장면에 sceneLoaded 가 오지 않는 경우를 위해서도 한 번 본다(붙이기는 한 번만 된다)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AttachToLoadedScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Attach(SceneManager.GetSceneAt(i));
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach(scene);

        // 그 장면의 시작 개체에 Bootstrap 을 붙인다. 이미 붙어 있거나 시작 개체가 없으면 아무 일도 없다
        public static Bootstrap? Attach(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != StartObjectName)
                {
                    continue;
                }
                Bootstrap? existing = root.GetComponent<Bootstrap>();
                return existing != null ? existing : root.AddComponent<Bootstrap>();
            }
            return null;
        }

        private void Start()
        {
            CompositionRoot.Ensure().Flow.OnSceneEntered(gameObject.scene.name);
        }
    }
}
