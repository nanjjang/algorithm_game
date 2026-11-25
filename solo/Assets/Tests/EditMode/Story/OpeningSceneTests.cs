using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Wizard.Solo.Screens.Story;

namespace Wizard.Solo.Tests.Story
{
    public sealed class OpeningSceneTests
    {
        [Test]
        public void OpeningIsAnEnabledSceneWithItsDirector()
        {
            const string path = TutorialOpening.ScenePath;
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path));
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var director = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<TutorialOpening>()).Single();
                var backdrop = new SerializedObject(director).FindProperty("fieldBackplate").objectReferenceValue;
                Assert.That(AssetDatabase.GetAssetPath(backdrop), Is.EqualTo("Assets/Resources/Art/world/backplates/field.png"));
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
