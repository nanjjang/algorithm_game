#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Unity.Presentation.Core;

namespace Wizard.Unity.App.Core.Tests
{
    // 조립 루트가 Battle 장면에서 하는 일(M1-UE-02 수락 조건 2 의 이음): 세계 표시기를 만들어 고정 틱 구동기에 꽂고,
    // 구동기의 프레임마다 그 표시기가 뷰를 읽어 그리고, 치우면 구동기에서 빠지고 도형이 사라진다.
    public sealed class BattlePresentationTests
    {
        private const double T = 1.0 / 60;

        private GameObject? _holder;

        [SetUp]
        public void SetUp() => _holder = new GameObject("Holder");

        [TearDown]
        public void TearDown()
        {
            if (_holder != null)
            {
                UnityEngine.Object.DestroyImmediate(_holder);
            }
        }

        private WorldPresenterSetup Setup() =>
            new WorldPresenterSetup(_ => 0.3f) { Parent = _holder!.transform, EnsureCamera = false, ShowDebugText = true };

        [Test]
        public void RegistersTheWorldPresenter_AndTheDriverRendersThroughIt()
        {
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            using (var battle = new BattlePresentation(driver, Setup()))
            {
                Assert.That(driver.PresenterCount, Is.EqualTo(1));
                Assert.That(battle.World.Root.transform.parent, Is.SameAs(_holder!.transform));
                Assert.That(battle.World.Shapes.Current.HasTick, Is.False);

                driver.RunFrame();                                               // 세션이 없으면 표시기를 부르지 않는다
                Assert.That(battle.World.Shapes.Current.HasTick, Is.False);

                var session = new FakeSession(new List<string>(), 0);
                driver.SetSession(session);
                clock.NowSeconds = 2.5 * T;
                driver.RunFrame();
                Assert.That(battle.World.Shapes.Current.HasTick, Is.True);       // 그 프레임의 뷰를 읽었다
                Assert.That(battle.World.Shapes.Current.Tick, Is.EqualTo(session.View.Tick));
                StringAssert.StartsWith("tick ", battle.World.DebugText);
            }
        }

        [Test]
        public void Dispose_UnregistersAndRemovesTheShapes()
        {
            var clock = new FakeClock();
            var driver = new FixedTickDriver(clock, new FakeInput());
            var battle = new BattlePresentation(driver, Setup());
            GameObject root = battle.World.Root;
            driver.SetSession(new FakeSession(new List<string>(), 0));
            driver.RunFrame();

            battle.Dispose();
            Assert.That(driver.PresenterCount, Is.EqualTo(0));
            Assert.That(root == null, Is.True);
            Assert.That(_holder!.transform.childCount, Is.EqualTo(0));
            Assert.DoesNotThrow(() => driver.RunFrame());                        // 치운 표시기는 더 불리지 않는다
            battle.Dispose();                                                    // 두 번 치워도 된다
        }

        [Test]
        public void Constructor_RejectsNulls()
        {
            var driver = new FixedTickDriver(new FakeClock(), new FakeInput());
            Assert.Throws<ArgumentNullException>(() => new BattlePresentation(null!, Setup()));
            Assert.Throws<ArgumentNullException>(() => new BattlePresentation(driver, null!));
            Assert.Throws<ArgumentNullException>(() => BattlePresentation.For(driver, null!, Wizard.Content.SimMode.Practice, null));
            Assert.That(driver.PresenterCount, Is.EqualTo(0));
        }
    }
}
