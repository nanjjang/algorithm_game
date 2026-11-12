#nullable enable
using System;

namespace Wizard.Unity.App.Core
{
    // 장면 전환(ARCHITECTURE 4.3). 장면에 들어온 것은 그 장면의 시작 개체(Bootstrap)가 알린다(OnSceneEntered).
    // 어느 장면에서 무엇을 꽂을지는 듣는 쪽(CompositionRoot)이 정한다. 화면 전환은 여기가 아니다(ScreenStack, spec 13 1.1).
    public sealed class SceneFlow
    {
        // 장면 파일 이름(unity/Assets/Scenes/<이름>.unity). 화면 글자가 아니다
        private static readonly string[] Names = { "Boot", "Menu", "Battle", "Plaza" };

        private readonly ISceneLoader _loader;

        public SceneFlow(ISceneLoader loader)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
        }

        // 마지막으로 들어온 장면. 아직 없으면 null
        public GameScene? Current { get; private set; }

        // 불러 달라고 했지만 아직 들어오지 않은 장면
        public GameScene? Pending { get; private set; }

        public event Action<GameScene>? Entered;

        public static string NameOf(GameScene scene)
        {
            int i = (int)scene;
            if (i < 0 || i >= Names.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(scene));
            }
            return Names[i];
        }

        public static bool TryParse(string? sceneName, out GameScene scene)
        {
            for (int i = 0; i < Names.Length; i++)
            {
                if (string.Equals(Names[i], sceneName, StringComparison.Ordinal))
                {
                    scene = (GameScene)i;
                    return true;
                }
            }
            scene = default;
            return false;
        }

        public void Request(GameScene scene)
        {
            string name = NameOf(scene);
            Pending = scene;
            _loader.Load(name);
        }

        // 장면의 시작 개체가 부른다. 모르는 장면(테스트 장면 등)이면 거짓이고 아무 일도 없다
        public bool OnSceneEntered(string sceneName)
        {
            if (!TryParse(sceneName, out GameScene scene))
            {
                return false;
            }
            Current = scene;
            if (Pending == scene)
            {
                Pending = null;
            }
            Entered?.Invoke(scene);
            return true;
        }
    }
}
