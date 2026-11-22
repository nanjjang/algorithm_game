using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wizard.Solo.App;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.PlayMode.App
{
    /// <summary>
    /// 부팅 스모크(00 문서 4절·12절). 자동 부팅은 Main 장면에서만 돌므로 테스트가 Bootstrap.Boot를 직접 부른다.
    /// 저장은 임시 폴더(DataRoot)에만 — 개발자의 진짜 저장 파일을 건드리지 않는다. UnityEditor를 쓰지 않는다.
    /// </summary>
    public class BootSmokeTests
    {
        string _dataRoot;
        BotInputSource _bot;
        BootResult _boot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _dataRoot = Path.Combine(Path.GetTempPath(), "wizard-solo-boot-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dataRoot);
            _bot = new BotInputSource();
            _boot = Bootstrap.Boot(new BootOptions { BuildVersion = "test", DataRoot = _dataRoot, InputOverride = _bot });
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Bootstrap.Shutdown();
            yield return null;
            try { if (Directory.Exists(_dataRoot)) Directory.Delete(_dataRoot, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        [UnityTest]
        public IEnumerator Boot_BuildsContextSessionCameraAndHud()
        {
            Assert.IsNotNull(_boot);
            var ctx = _boot.Context;
            Assert.IsNotNull(ctx);
            Assert.AreEqual(_dataRoot, ctx.DataRoot);
            Assert.AreSame(_bot, ctx.Input, "InputOverride replaces the keyboard source");
            Assert.IsNotNull(ctx.Session);
            Assert.IsInstanceOf<GameRunner>(ctx.Session);
            Assert.IsFalse(ctx.Session.HasRun);

            var runner = (GameRunner)ctx.Session;
            var cam = runner.Host.Camera;
            Assert.IsNotNull(cam);
            Assert.IsTrue(cam.orthographic);
            Assert.AreEqual(UiScale.OrthographicSize(Screen.height, UiScale.Compute(Screen.width, Screen.height)), cam.orthographicSize, 1e-4f);
            Assert.AreEqual(WorldViewHost.CameraHome, cam.transform.position);

            var hud = runner.Host.HudRoot;
            Assert.IsNotNull(hud);
            Assert.AreSame(runner.Host.WorldTextRoot, hud.GetChild(0), "WorldTextRoot is the first child of the HUD root");
            var canvas = hud.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas);
            Assert.AreEqual(UiSortOrders.Hud, canvas.sortingOrder);

            if (ctx.Screens != null)
            {
                var expected = Bootstrap.FirstScreen(_boot);
                Assert.AreEqual(_boot.FirstRun ? ScreenId.FirstRunPrompt : ScreenId.Title, expected);
                // 첫 화면(G18)이 아직 등록되지 않았으면 관리자는 경고만 내고 아무것도 열지 않는다(미등록 id 규칙). 등록돼 있으면 그 화면이 맨 위다.
                var mgr = ctx.Screens as ScreenManager;
                bool registered = mgr == null || mgr.IsRegistered(expected);
                Assert.AreEqual(registered ? expected : ScreenId.None, ctx.Screens.Top);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FramesWithoutRun_DoNotThrow_PauseToggleSafe()
        {
            var s = _boot.Context.Session;
            for (int i = 0; i < 5; i++) yield return null;
            s.Paused = true;
            yield return null;
            s.Paused = false;
            yield return null;
            Assert.IsFalse(s.HasRun);
            Assert.AreEqual(0f, s.PausedSeconds, "no run → paused time is not counted");
        }

        [UnityTest]
        public IEnumerator Reboot_ReplacesPreviousObjects()
        {
            var firstRunner = (GameRunner)_boot.Context.Session;
            var firstCam = firstRunner.Host.Camera;
            int camerasBefore = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length;
            _boot = Bootstrap.Boot(new BootOptions { BuildVersion = "test", DataRoot = _dataRoot, InputOverride = _bot });
            yield return null;
            Assert.IsTrue(firstRunner == null, "previous GameRunner destroyed");
            Assert.IsTrue(firstCam == null, "previous camera destroyed");
            Assert.AreEqual(1, UnityEngine.Object.FindObjectsByType<GameRunner>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(camerasBefore, UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length,
                "same number of cameras after reboot (old one removed, new one made)");
        }
    }
}
