using System.IO;
using NUnit.Framework;
using Wizard.Solo.Screens.Start;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensStart
{
    /// <summary>시네마틱 화면의 파일 고르기와 인자 넘기기(첫 실행 물음 → 인트로 → 타이틀).</summary>
    public class CinematicScreenTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "solo-cinematic-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch (IOException) { }
        }

        void Touch(string name) => File.WriteAllBytes(Path.Combine(_dir, name), new byte[] { 0 });

        [Test]
        public void PathFor_FullLevelPrefersFull_ReducedPrefersCalm()
        {
            Touch("intro.mp4");
            Touch("intro_calm.mp4");
            Assert.AreEqual(Path.Combine(_dir, "intro.mp4"), CinematicScreen.PathFor(_dir, "intro", 0));
            Assert.AreEqual(Path.Combine(_dir, "intro_calm.mp4"), CinematicScreen.PathFor(_dir, "intro", 1));
            Assert.AreEqual(Path.Combine(_dir, "intro_calm.mp4"), CinematicScreen.PathFor(_dir, "intro", 2));
        }

        [Test]
        public void PathFor_ReducedNeverFallsBackToFlashingVideo()
        {
            Touch("intro.mp4");
            Assert.IsNull(CinematicScreen.PathFor(_dir, "intro", 1));
            Assert.AreEqual(Path.Combine(_dir, "intro.mp4"), CinematicScreen.PathFor(_dir, "intro", 0));
        }

        [Test]
        public void PathFor_FullFallsBackToCalm_NoneWhenMissing()
        {
            Assert.IsNull(CinematicScreen.PathFor(_dir, "intro", 0));
            Touch("intro_calm.mp4");
            Assert.AreEqual(Path.Combine(_dir, "intro_calm.mp4"), CinematicScreen.PathFor(_dir, "intro", 0));
        }

        [Test]
        public void ArgsFor_KeepsStartFlowValues_AndForwardsThemToNext()
        {
            var incoming = ScreenArgs.From(ScreenId.FirstRunPrompt).With(ScreenArgKeys.ProfileRestored, true);
            var a = CinematicScreen.ArgsFor(CinematicScreen.IntroVideo, ScreenId.Title, incoming);
            Assert.AreEqual(CinematicScreen.IntroVideo, a.Get<string>(CinematicScreen.ArgVideo));
            Assert.AreEqual(ScreenId.Title, a.Get(CinematicScreen.ArgNext, ScreenId.None));
            Assert.IsTrue(StartFlow.Restored(a));

            var next = StartFlow.Forward(a, ScreenId.Cinematic, true);
            Assert.IsTrue(StartFlow.Restored(next));
            Assert.IsFalse(next.Values.ContainsKey(CinematicScreen.ArgVideo));
        }
    }
}
