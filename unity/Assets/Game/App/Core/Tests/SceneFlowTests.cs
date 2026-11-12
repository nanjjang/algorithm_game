#nullable enable
using System.Collections.Generic;
using NUnit.Framework;

namespace Wizard.Unity.App.Core.Tests
{
    public sealed class SceneFlowTests
    {
        [TestCase(GameScene.Boot, "Boot")]
        [TestCase(GameScene.Menu, "Menu")]
        [TestCase(GameScene.Battle, "Battle")]
        [TestCase(GameScene.Plaza, "Plaza")]
        public void NameOf_AndTryParse_RoundTrip(GameScene scene, string name)
        {
            Assert.That(SceneFlow.NameOf(scene), Is.EqualTo(name));
            Assert.That(SceneFlow.TryParse(name, out GameScene parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(scene));
        }

        [Test]
        public void Request_LoadsByName_EnteredClearsPendingAndNotifies()
        {
            var loader = new FakeSceneLoader();
            var flow = new SceneFlow(loader);
            var entered = new List<GameScene>();
            flow.Entered += s => entered.Add(s);

            flow.Request(GameScene.Battle);
            Assert.That(loader.Loaded, Is.EqualTo(new[] { "Battle" }));
            Assert.That(flow.Pending, Is.EqualTo(GameScene.Battle));
            Assert.That(flow.Current, Is.Null);

            Assert.That(flow.OnSceneEntered("Battle"), Is.True);
            Assert.That(flow.Current, Is.EqualTo(GameScene.Battle));
            Assert.That(flow.Pending, Is.Null);
            Assert.That(entered, Is.EqualTo(new[] { GameScene.Battle }));
        }

        [Test]
        public void UnknownScene_IsIgnored()
        {
            var flow = new SceneFlow(new FakeSceneLoader());
            int calls = 0;
            flow.Entered += _ => calls++;
            Assert.That(flow.OnSceneEntered("InitTestScene"), Is.False);
            Assert.That(flow.OnSceneEntered("battle"), Is.False);
            Assert.That(flow.OnSceneEntered(string.Empty), Is.False);
            Assert.That(calls, Is.EqualTo(0));
            Assert.That(flow.Current, Is.Null);
        }
    }
}
