#nullable enable
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wizard.Unity.App.Core.Tests
{
    // 장면 파일을 고치지 않고 시작 개체에 Bootstrap 을 붙인다(T-32)
    public sealed class BootstrapTests
    {
        private Scene _scene;

        // 저장하지 않은 이름 없는 장면 옆에는 장면을 더할 수 없으므로 빈 장면 하나로 바꾼다. 장면 파일은 열지도 쓰지도 않는다
        [SetUp]
        public void SetUp() => _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        [Test]
        public void Attach_AddsOnceToTheStartObject()
        {
            var start = new GameObject(Bootstrap.StartObjectName);
            SceneManager.MoveGameObjectToScene(start, _scene);
            var other = new GameObject("Other");
            SceneManager.MoveGameObjectToScene(other, _scene);

            Bootstrap? first = Bootstrap.Attach(_scene);
            Assert.That(first, Is.Not.Null);
            Assert.That(first!.gameObject, Is.SameAs(start));
            Assert.That(Bootstrap.Attach(_scene), Is.SameAs(first));
            Assert.That(start.GetComponents<Bootstrap>().Length, Is.EqualTo(1));
            Assert.That(other.GetComponent<Bootstrap>(), Is.Null);
        }

        [Test]
        public void Attach_SceneWithoutStartObject_DoesNothing()
        {
            var other = new GameObject("Other");
            SceneManager.MoveGameObjectToScene(other, _scene);
            Assert.That(Bootstrap.Attach(_scene), Is.Null);
            Assert.That(other.GetComponent<Bootstrap>(), Is.Null);
        }

        [Test]
        public void StartObjectName_MatchesTheSceneConvention()
        {
            Assert.That(Bootstrap.StartObjectName, Is.EqualTo("Bootstrap"));
        }
    }
}
