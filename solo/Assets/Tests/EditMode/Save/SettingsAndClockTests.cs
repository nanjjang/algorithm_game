using System;
using System.IO;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;
using Wizard.Solo.Save;

namespace Wizard.Solo.Tests.Save
{
    /// <summary>설정 저장(08 요약서 12절, 10 요약서 7절)과 키 배치 표(02 3.1절, 08 12.3절), 주 경계(D-33).</summary>
    public class SettingsAndClockTests
    {
        string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "wizard-solo-settings-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
        }

        SettingsStore Store(string root = null) => new SettingsStore(SavePaths.SettingsFile(root ?? _root));

        // 08 2절·10 7절: 설정 파일이 없으면 기기 첫 실행
        [Test]
        public void FirstRun__NoFileMeansNotExisted()
        {
            var s = Store();
            s.Load();
            Assert.IsFalse(s.ExistedAtLoad);
            Assert.IsFalse(File.Exists(s.FilePath), "Load는 파일을 만들지 않는다");
            s.Save();
            var again = Store();
            again.Load();
            Assert.IsTrue(again.ExistedAtLoad);
        }

        [Test]
        public void Changed__FiresOnSave()
        {
            var s = Store();
            s.Load();
            int n = 0;
            s.Changed += () => n++;
            s.Save();
            Assert.AreEqual(1, n);
            Assert.IsFalse(File.Exists(s.FilePath + ProfileStore.TempSuffix));
        }

        // TV-168: 기본값은 SettingsOptions가 준다(옵션 사전은 비어 있다)
        [Test]
        public void S13_TV_168__DefaultsComeFromSettingsOptions()
        {
            var s = Store();
            s.Load();
            Assert.AreEqual(0, s.Current.Options.Count);
            Assert.AreEqual(100, SettingsOptions.Int(s.Current, OptionKeys.MyBulletOpacity));
            Assert.AreEqual(70, SettingsOptions.Int(s.Current, OptionKeys.TelegraphFill));
            Assert.AreEqual(OptionValues.Full, SettingsOptions.Str(s.Current, OptionKeys.MotionPreset));
            Assert.AreEqual(OptionValues.Default, SettingsOptions.Str(s.Current, OptionKeys.KeyLayout));
            Assert.AreEqual(1f, SettingsOptions.Volume(s.Current, SoundBus.Sfx));
        }

        [Test]
        public void Roundtrip__OptionValuesSurvive()
        {
            var s = Store();
            s.Load();
            SettingsOptions.Set(s.Current, OptionKeys.VolBgm, 40);
            SettingsOptions.Set(s.Current, OptionKeys.MotionPreset, OptionValues.Reduced);
            SettingsOptions.Set(s.Current, OptionKeys.TelegraphFill, 80);
            SettingsOptions.Set(s.Current, OptionKeys.HitstopEnabled, false);
            SettingsOptions.Set(s.Current, OptionKeys.MyBulletOpacity, 29);   // TV-170: 30으로 고정
            s.Save();

            string text = File.ReadAllText(s.FilePath);
            StringAssert.Contains("\"vol_bgm\"", text);
            StringAssert.Contains("\"key_primary\"", text);
            StringAssert.Contains("\"schema_version\"", text);

            var r = Store();
            r.Load();
            Assert.AreEqual(40, SettingsOptions.Int(r.Current, OptionKeys.VolBgm));
            Assert.AreEqual(0.4f, SettingsOptions.Volume(r.Current, SoundBus.Bgm), 1e-6f);
            Assert.AreEqual(OptionValues.Reduced, SettingsOptions.Str(r.Current, OptionKeys.MotionPreset));
            Assert.AreEqual(1, SettingsOptions.EffectsLevel(r.Current));
            Assert.AreEqual(80, SettingsOptions.Int(r.Current, OptionKeys.TelegraphFill));
            Assert.IsFalse(SettingsOptions.Bool(r.Current, OptionKeys.HitstopEnabled));
            Assert.AreEqual(30, SettingsOptions.Int(r.Current, OptionKeys.MyBulletOpacity));
            Assert.AreEqual(100, SettingsOptions.Int(r.Current, OptionKeys.VolSfx));
        }

        [Test]
        public void Load__BrokenFileOpensDefaultsButIsNotFirstRun()
        {
            File.WriteAllText(SavePaths.SettingsFile(_root), "{ broken");
            var s = Store();
            s.Load();
            Assert.IsTrue(s.ExistedAtLoad);
            Assert.AreEqual(KeyLayouts.KeyCount, s.Current.KeyPrimary.Count);
        }

        [Test]
        public void DataRoot__DifferentRootsUseDifferentSettingsFiles()
        {
            var a = Store();
            a.Load();
            SettingsOptions.Set(a.Current, OptionKeys.VolUi, 10);
            a.Save();
            var b = Store(Path.Combine(_root, "other"));
            b.Load();
            Assert.IsFalse(b.ExistedAtLoad);
            Assert.AreEqual(100, SettingsOptions.Int(b.Current, OptionKeys.VolUi));
        }

        // TV-171: 기본 배치 W S A D J K Shift Space (+ 보조 ↑←↓→ Z X C V). Pause 칸은 비어 있다(Esc는 고정 키)
        [Test]
        public void S13_TV_171__DefaultLayout()
        {
            var s = Store();
            s.Load();
            int[] primary = { (int)Key.W, (int)Key.A, (int)Key.S, (int)Key.D, (int)Key.J, (int)Key.K, (int)Key.LeftShift, (int)Key.Space, 0 };
            int[] secondary = { (int)Key.UpArrow, (int)Key.LeftArrow, (int)Key.DownArrow, (int)Key.RightArrow, (int)Key.Z, (int)Key.X, (int)Key.C, (int)Key.V, 0 };
            CollectionAssert.AreEqual(primary, s.Current.KeyPrimary);
            CollectionAssert.AreEqual(secondary, s.Current.KeySecondary);
            CollectionAssert.AreEqual(primary, KeyLayouts.DefaultPrimary);
            Assert.AreEqual((int)Key.W, KeyLayouts.PrimaryOf(s.Current, LogicalKey.Up));
            Assert.AreEqual((int)Key.S, KeyLayouts.PrimaryOf(s.Current, LogicalKey.Down));
            Assert.AreEqual(0, KeyLayouts.PrimaryOf(s.Current, LogicalKey.Pause));
            Assert.AreEqual(0, KeyLayouts.SecondaryOf(s.Current, LogicalKey.Pause));
            CollectionAssert.DoesNotContain(s.Current.KeyPrimary, (int)Key.Escape);
            CollectionAssert.DoesNotContain(s.Current.KeySecondary, (int)Key.Escape);
        }

        // TV-172: 대체 배치 ↑ ↓ ← → Z X C V
        [Test]
        public void S13_TV_172__AlternateLayoutRoundtrip()
        {
            var s = Store();
            s.Load();
            Assert.IsTrue(KeyLayouts.Apply(s.Current, OptionValues.Alternate));
            s.Save();
            var r = Store();
            r.Load();
            Assert.AreEqual(OptionValues.Alternate, SettingsOptions.Str(r.Current, OptionKeys.KeyLayout));
            Assert.AreEqual((int)Key.UpArrow, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Up));
            Assert.AreEqual((int)Key.DownArrow, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Down));
            Assert.AreEqual((int)Key.LeftArrow, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Left));
            Assert.AreEqual((int)Key.RightArrow, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Right));
            Assert.AreEqual((int)Key.Z, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Fire));
            Assert.AreEqual((int)Key.X, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Skill));
            Assert.AreEqual((int)Key.C, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Stance));
            Assert.AreEqual((int)Key.V, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Reverse));
            Assert.AreEqual(0, KeyLayouts.PrimaryOf(r.Current, LogicalKey.Pause));
        }

        [Test]
        public void CustomLayout__KeptAndPaddedOnLoad()
        {
            var s = Store();
            s.Load();
            SettingsOptions.Set(s.Current, OptionKeys.KeyLayout, OptionValues.Custom);
            s.Current.KeyPrimary = new System.Collections.Generic.List<int> { (int)Key.I, (int)Key.J };
            s.Save();
            var r = Store();
            r.Load();
            Assert.AreEqual(KeyLayouts.KeyCount, r.Current.KeyPrimary.Count);
            Assert.AreEqual((int)Key.I, r.Current.KeyPrimary[0]);
            Assert.AreEqual(0, r.Current.KeyPrimary[8]);
            Assert.IsFalse(KeyLayouts.Apply(r.Current, OptionValues.Custom));
        }

        // D-33: 주 경계 = 기기 현지 시간 월요일 00:00
        [TestCase(2026, 11, 18, 15, 0, "2026-11-16")]   // 수요일
        [TestCase(2026, 11, 16, 0, 0, "2026-11-16")]    // 월요일 00:00 정각은 새 주
        [TestCase(2026, 11, 15, 23, 59, "2026-11-09")]  // 일요일 23:59는 지난 주
        [TestCase(2026, 11, 22, 12, 0, "2026-11-16")]   // 일요일
        [TestCase(2027, 1, 1, 8, 0, "2026-12-28")]      // 해 넘김
        public void S08_D33__WeekStartIsLocalMonday(int y, int mo, int d, int h, int mi, string expected)
        {
            var t = new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Local);
            Assert.AreEqual(expected, SystemClock.WeekStartIso(t));
            Assert.AreEqual(DayOfWeek.Monday, SystemClock.WeekStart(t).DayOfWeek);
            Assert.AreEqual(TimeSpan.Zero, SystemClock.WeekStart(t).TimeOfDay);
        }

        [Test]
        public void SameWeek__BoundaryAtMondayMidnight()
        {
            var sun = new DateTime(2026, 11, 15, 23, 59, 59);
            var mon = new DateTime(2026, 11, 16, 0, 0, 0);
            Assert.IsFalse(SystemClock.SameWeek(sun, mon));
            Assert.IsTrue(SystemClock.SameWeek(mon, new DateTime(2026, 11, 22, 23, 59, 59)));
        }

        // ISO 8601 주 번호(2026-01-01은 목요일 → 2026년은 53주)
        [TestCase(2026, 11, 14, "2026-W46")]
        [TestCase(2026, 1, 1, "2026-W01")]
        [TestCase(2025, 12, 29, "2026-W01")]
        [TestCase(2027, 1, 1, "2026-W53")]
        [TestCase(2027, 1, 4, "2027-W01")]
        public void IsoWeek__Iso8601(int y, int m, int d, string expected)
        {
            Assert.AreEqual(expected, SystemClock.IsoWeek(new DateTime(y, m, d, 12, 0, 0)));
        }

        [Test]
        public void SystemClock__ImplementsIClock()
        {
            IClock c = new SystemClock();
            Assert.LessOrEqual((c.UtcNow - DateTime.UtcNow).Duration().TotalSeconds, 5.0);
        }
    }
}
