#nullable enable
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Wizard.TestKit.Shared.Golden;
using Wizard.Unity.App.Headless;

namespace Wizard.Unity.Tests.EditMode.Golden
{
    // 플레이어의 머리 없는 골든 실행(HeadlessGolden → HeadlessGoldenRun)을 편집기에서 돌려 본다. 플레이어와 같은 코드이고
    // 다른 것은 입구(RuntimeInitializeOnLoadMethod, Application.Quit)뿐이다. 진짜 골든의 일부를 임시 뿌리로 옮겨 쓴다.
    // 임시 뿌리는 저장소 밖이므로 -goldenPack 과 -goldenOut 을 준다(다른 기기에서 저장소 없이 돌리는 길과 같다).
    public sealed class HeadlessGoldenRunTests
    {
        private const string Bundle = "fixmath-div";

        private string _root = string.Empty;
        private string _out = string.Empty;
        private GoldenEntry _entry = null!;

        [SetUp]
        public void SetUp()
        {
            _entry = GoldenCatalog.Load(GoldenPack.GoldenRoot).Get("m1/practice-solo");
            _root = TempGolden.CopyEntries(_entry, Bundle);
            _out = _root + "-out";                         // 골든 뿌리 밖(뿌리 안의 모르는 폴더는 형식 문제다)
        }

        [TearDown]
        public void TearDown()
        {
            TempGolden.Delete(_root);
            TempGolden.Delete(_out);
        }

        [Test]
        public void MatchingGoldensExitZeroAndWriteTheComboResultFile()
        {
            GoldenCombo combo = RuntimeCombo.Detect();
            var log = new StringWriter();
            int code = HeadlessGoldenRun.Execute(Args(), combo, log);
            Assert.That(code, Is.EqualTo(HeadlessGoldenRun.ExitOk), log.ToString());
            Assert.That(log.ToString(), Does.Contain("golden: ok"));

            string path = Path.Combine(_out, "golden-result." + combo.Name + ".json");
            Assert.That(File.Exists(path), Is.True, log.ToString());
            GoldenRunResult r = GoldenRunResult.Load(path);
            Assert.That(r.Combo.Name, Is.EqualTo(combo.Name));
            Assert.That(r.Entries.Count, Is.EqualTo(1));
            Assert.That(r.Entries[0].Id, Is.EqualTo(_entry.Id));
            Assert.That(r.Entries[0].Error, Is.Null);
            Assert.That(r.Arith.Count, Is.EqualTo(GoldenCatalog.Load(_root).ArithBundles[0].Cases.Count));
        }

        // 결과 파일 이름은 실제 조합에서 온다(플레이어마다 다른 파일)
        [Test]
        public void TheResultFileIsNamedAfterTheActualCombo()
        {
            var combo = new GoldenCombo("macos", "il2cpp", "player", "x64");
            var log = new StringWriter();
            Assert.That(HeadlessGoldenRun.Execute(Args(), combo, log), Is.EqualTo(HeadlessGoldenRun.ExitOk), log.ToString());
            Assert.That(File.Exists(Path.Combine(_out, "golden-result.macos-il2cpp-player-x64.json")), Is.True);
        }

        // 기대와 다르면 0이 아닌 코드로 끝나고, 처음 갈라진 틱을 알려 주고, 결과 파일은 그대로 쓴다
        [Test]
        public void AMismatchExitsNonZeroAndNamesTheFirstDivergentTick()
        {
            var tampered = new List<GoldenHash>(_entry.Expected);
            GoldenHash second = tampered[1];
            tampered[1] = new GoldenHash(second.Tick, second.Hash ^ 1UL, second.Label);
            File.WriteAllText(Path.Combine(_root, "m1", "practice-solo", GoldenEntry.ExpectedFileName), GoldenEntry.FormatExpected(_entry.Id, tampered));

            GoldenCombo combo = RuntimeCombo.Detect();
            var log = new StringWriter();
            int code = HeadlessGoldenRun.Execute(Args(), combo, log);
            Assert.That(code, Is.EqualTo(HeadlessGoldenRun.ExitMismatch), log.ToString());
            Assert.That(log.ToString(), Does.Contain("diff@" + second.Tick));
            Assert.That(log.ToString(), Does.Contain("first divergence at tick " + second.Tick));
            Assert.That(File.Exists(Path.Combine(_out, combo.ResultFileName)), Is.True);
        }

        // 산술 벡터의 기대가 다르면 실패다
        [Test]
        public void AnArithMismatchExitsNonZero()
        {
            string bundlePath = Path.Combine(_root, GoldenCatalog.ArithFolder, Bundle + ".json");
            string json = File.ReadAllText(bundlePath);
            const string Original = "\"expect\": [\"-2147483648\"]";
            Assert.That(json, Does.Contain(Original), "the bundle changed; pick another case to tamper");
            File.WriteAllText(bundlePath, ReplaceFirst(json, Original, "\"expect\": [\"2147483647\"]"));

            var log = new StringWriter();
            Assert.That(HeadlessGoldenRun.Execute(Args(), RuntimeCombo.Detect(), log), Is.EqualTo(HeadlessGoldenRun.ExitMismatch), log.ToString());
            Assert.That(log.ToString(), Does.Contain("arith " + Bundle + "/"));
        }

        [Test]
        public void AComboOtherThanTheRequestedOneExitsTwoWithoutAResult()
        {
            GoldenCombo combo = RuntimeCombo.Detect();
            string other = combo.Arch == "arm64" ? "macos-mono-player-x64" : "macos-mono-player-arm64";
            var log = new StringWriter();
            int code = HeadlessGoldenRun.Execute(Args("-goldenCombo", other), combo, log);
            Assert.That(code, Is.EqualTo(HeadlessGoldenRun.ExitUsage), log.ToString());
            Assert.That(log.ToString(), Does.Contain("asked for " + other));
            Assert.That(Directory.Exists(_out), Is.False);
        }

        [Test]
        public void AMissingPackExitsTwo()
        {
            var log = new StringWriter();
            HeadlessArgs args = Parse("-golden", _root, "-goldenPack", Path.Combine(_root, "nope.wzpack"), "-goldenOut", _out);
            Assert.That(HeadlessGoldenRun.Execute(args, RuntimeCombo.Detect(), log), Is.EqualTo(HeadlessGoldenRun.ExitUsage));
            Assert.That(log.ToString(), Does.Contain("cannot load the content pack"));
        }

        // 골든 뿌리에 모르는 파일이 있으면(이름을 잘못 지은 골든이 조용히 빠지지 않게) 돌리지 않는다
        [Test]
        public void AMalformedGoldenFolderExitsTwo()
        {
            File.WriteAllText(Path.Combine(_root, "stray.json"), "{}");
            var log = new StringWriter();
            Assert.That(HeadlessGoldenRun.Execute(Args(), RuntimeCombo.Detect(), log), Is.EqualTo(HeadlessGoldenRun.ExitUsage));
            Assert.That(log.ToString(), Does.Contain("unexpected file at the golden root: stray.json"));
        }

        private HeadlessArgs Args(params string[] extra)
        {
            var argv = new List<string> { "-batchmode", "-nographics", "-golden", _root, "-goldenPack", GoldenPack.PathOrBuild(), "-goldenOut", _out };
            argv.AddRange(extra);
            return Parse(argv.ToArray());
        }

        private static HeadlessArgs Parse(params string[] argv)
        {
            Assert.That(HeadlessArgs.TryParse(argv, out HeadlessArgs? args, out string error), Is.True, error);
            return args!;
        }

        private static string ReplaceFirst(string text, string find, string with)
        {
            int i = text.IndexOf(find, System.StringComparison.Ordinal);
            return text.Substring(0, i) + with + text.Substring(i + find.Length);
        }
    }
}
