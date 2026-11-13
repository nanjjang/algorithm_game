#nullable enable
using System.IO;
using NUnit.Framework;
using Wizard.TestKit.Shared;
using Wizard.Unity.App.Headless;

namespace Wizard.Unity.Tests.EditMode.Golden
{
    // 머리 없는 골든 실행의 명령 줄(HeadlessArgs)
    public sealed class HeadlessArgsTests
    {
        [Test]
        public void WithoutTheGoldenFlagNothingIsRequested()
        {
            Assert.That(HeadlessArgs.IsRequested(new[] { "Wizard", "-batchmode", "-nographics", "-logFile", "x.log" }), Is.False);
            Assert.That(HeadlessArgs.IsRequested(new[] { "Wizard", "-batchmode", "-golden" }), Is.True);
        }

        [Test]
        public void DefaultsResolveUnderTheRepositoryOfTheGoldenFolder()
        {
            string golden = GoldenPack.GoldenRoot;
            string[] argv = { "/x/Wizard", "-batchmode", "-nographics", "-golden", golden, "-logFile", "/tmp/p.log" };
            Assert.That(HeadlessArgs.TryParse(argv, out HeadlessArgs? args, out string error), Is.True, error);
            string repo = RepoRoot.Find(golden);
            Assert.That(args!.GoldenRoot, Is.EqualTo(Path.GetFullPath(golden)));
            Assert.That(args.PackPath, Is.EqualTo(RepoRoot.Resolve(repo, "artifacts/golden/pack/base.wzpack")));
            Assert.That(args.OutDirectory, Is.EqualTo(RepoRoot.Resolve(repo, "artifacts/golden")));
            Assert.That(args.ExpectedCombo, Is.Null);
        }

        [Test]
        public void ExplicitValuesWin()
        {
            string golden = GoldenPack.GoldenRoot;
            string tmp = Path.GetTempPath();
            string[] argv = { "-golden", golden, "-goldenPack", Path.Combine(tmp, "p.wzpack"), "-goldenOut", tmp, "-goldenCombo", "macos-mono-player-x64" };
            Assert.That(HeadlessArgs.TryParse(argv, out HeadlessArgs? args, out string error), Is.True, error);
            Assert.That(args!.PackPath, Is.EqualTo(Path.GetFullPath(Path.Combine(tmp, "p.wzpack"))));
            Assert.That(args.OutDirectory, Is.EqualTo(Path.GetFullPath(tmp)));
            Assert.That(args.ExpectedCombo, Is.EqualTo("macos-mono-player-x64"));
        }

        [Test]
        public void AFlagWithoutAValueIsAnError()
        {
            Assert.That(HeadlessArgs.TryParse(new[] { "-golden" }, out _, out string e1), Is.False);
            Assert.That(e1, Does.Contain("-golden needs a value"));
            Assert.That(HeadlessArgs.TryParse(new[] { "-golden", GoldenPack.GoldenRoot, "-goldenOut", "-batchmode" }, out _, out string e2), Is.False);
            Assert.That(e2, Does.Contain("-goldenOut needs a value"));
        }

        [Test]
        public void AMissingGoldenFolderIsAnError()
        {
            string missing = Path.Combine(Path.GetTempPath(), "wizard-no-such-golden-folder");
            Assert.That(HeadlessArgs.TryParse(new[] { "-golden", missing }, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("does not exist"));
        }

        // 저장소 밖의 골든(다른 기기)에서는 팩과 출력 폴더를 직접 줘야 한다
        [Test]
        public void OutsideARepositoryThePackAndOutputMustBeGiven()
        {
            string root = Path.Combine(Path.GetTempPath(), "wizard-golden-args-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                Assert.That(HeadlessArgs.TryParse(new[] { "-golden", root }, out _, out string error), Is.False);
                Assert.That(error, Does.Contain("-goldenPack").And.Contain("-goldenOut"));
                Assert.That(HeadlessArgs.TryParse(new[] { "-golden", root, "-goldenPack", "p", "-goldenOut", root }, out _, out string ok), Is.True, ok);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
