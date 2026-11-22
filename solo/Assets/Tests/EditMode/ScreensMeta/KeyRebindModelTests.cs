using NUnit.Framework;
using UnityEngine.InputSystem;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Settings;

namespace Wizard.Solo.Tests.ScreensMeta
{
    /// <summary>키 재설정 모델(08 12.3절, 02 3.1절, D-74).</summary>
    public class KeyRebindModelTests
    {
        static SettingsData DefaultSettings()
        {
            var s = new SettingsData();
            KeyLayouts.Apply(s, OptionValues.Default);
            return s;
        }

        static void AssertRow(KeyRebindModel m, LogicalKey k, Key primary)
        {
            Assert.AreEqual((int)primary, m.Primary(k), k.ToString());
        }

        [Test]
        public void S08_TV_171__DefaultLayoutWSADJKShiftSpace()
        {
            var m = new KeyRebindModel(DefaultSettings());
            Assert.AreEqual(OptionValues.Default, m.Layout);
            AssertRow(m, LogicalKey.Up, Key.W);
            AssertRow(m, LogicalKey.Down, Key.S);
            AssertRow(m, LogicalKey.Left, Key.A);
            AssertRow(m, LogicalKey.Right, Key.D);
            AssertRow(m, LogicalKey.Fire, Key.J);
            AssertRow(m, LogicalKey.Skill, Key.K);
            AssertRow(m, LogicalKey.Stance, Key.LeftShift);
            AssertRow(m, LogicalKey.Reverse, Key.Space);
        }

        [Test]
        public void S08_TV_172__AlternateLayoutArrowsZXCV()
        {
            var m = new KeyRebindModel(DefaultSettings());
            m.SelectLayout(OptionValues.Alternate);
            Assert.AreEqual(OptionValues.Alternate, m.Layout);
            AssertRow(m, LogicalKey.Up, Key.UpArrow);
            AssertRow(m, LogicalKey.Down, Key.DownArrow);
            AssertRow(m, LogicalKey.Left, Key.LeftArrow);
            AssertRow(m, LogicalKey.Right, Key.RightArrow);
            AssertRow(m, LogicalKey.Fire, Key.Z);
            AssertRow(m, LogicalKey.Skill, Key.X);
            AssertRow(m, LogicalKey.Stance, Key.C);
            AssertRow(m, LogicalKey.Reverse, Key.V);
        }

        [Test]
        public void S08_TV_173__CaptureCancelsAfterFiveSecondsWithoutInput()
        {
            var m = new KeyRebindModel(DefaultSettings());
            Assert.IsTrue(m.BeginCapture(LogicalKey.Fire, false));
            Assert.AreEqual(RebindCaptureEnd.None, m.Tick(2.5f));
            Assert.AreEqual(RebindState.Capturing, m.State);
            Assert.AreEqual(RebindCaptureEnd.TimedOut, m.Tick(2.5f));
            Assert.AreEqual(RebindState.Idle, m.State);
            AssertRow(m, LogicalKey.Fire, Key.J);
            Assert.AreEqual(OptionValues.Default, m.Layout);
            Assert.AreEqual(5f, KeyRebindModel.CaptureTimeoutSeconds);
        }

        [Test]
        public void S08_TV_174__EscCancelsCaptureAndKeepsBinding()
        {
            var m = new KeyRebindModel(DefaultSettings());
            m.BeginCapture(LogicalKey.Fire, false);
            Assert.AreEqual(RebindCaptureEnd.CancelledByEsc, m.OnKey((int)Key.Escape));
            Assert.AreEqual(RebindState.Idle, m.State);
            AssertRow(m, LogicalKey.Fire, Key.J);
            Assert.AreEqual(OptionValues.Default, m.Layout);
            Assert.IsFalse(m.Dirty);
        }

        [Test]
        public void S08_TV_175__ConflictOnBothRowsBlocksSave()
        {
            var store = new CountingSettingsStore { Current = DefaultSettings() };
            var m = new KeyRebindModel(store.Current);
            m.BeginCapture(LogicalKey.Skill, false);
            Assert.AreEqual(RebindCaptureEnd.Assigned, m.OnKey((int)Key.J));
            Assert.AreEqual(OptionValues.Custom, m.Layout);
            Assert.AreEqual(LogicalKey.Fire, m.ConflictOf(LogicalKey.Skill));
            Assert.AreEqual(LogicalKey.Skill, m.ConflictOf(LogicalKey.Fire));
            CollectionAssert.AreEqual(new[] { LogicalKey.Fire, LogicalKey.Skill }, m.Conflicts());
            var names = TestContent.Names;
            Assert.AreEqual("발사와 같은 키",
                names.Format("str.key_rebind.conflict", "action", names.Get(KeyRebindModel.ActionNameKey(m.ConflictOf(LogicalKey.Skill).Value))));
            Assert.AreEqual("비기와 같은 키",
                names.Format("str.key_rebind.conflict", "action", names.Get(KeyRebindModel.ActionNameKey(m.ConflictOf(LogicalKey.Fire).Value))));
            Assert.IsFalse(m.CanSave);
            Assert.IsFalse(m.Save(store));
            Assert.AreEqual(0, store.Saves);
            CollectionAssert.AreEqual(KeyLayouts.DefaultPrimary, store.Current.KeyPrimary, "막힌 저장은 설정을 바꾸지 않는다");
        }

        [Test]
        public void S08_TV_177__CapturedKeyIsPhysicalKeyPosition()
        {
            // 물리 자리(Input System Key) 값을 그대로 담는다 — 한글 입력기의 글자와 무관하다.
            var m = new KeyRebindModel(DefaultSettings());
            m.BeginCapture(LogicalKey.Fire, true);
            m.OnKey((int)Key.J);
            Assert.AreEqual((int)Key.J, m.Secondary(LogicalKey.Fire));
            Assert.AreEqual((int)Key.J, KeyLayouts.PrimaryOf(DefaultSettings(), LogicalKey.Fire));
        }

        [Test]
        public void S08_12_3__PauseRowSecondaryIsFixedEsc()
        {
            var m = new KeyRebindModel(DefaultSettings());
            Assert.IsFalse(m.CanCapture(LogicalKey.Pause, true));
            Assert.IsFalse(m.BeginCapture(LogicalKey.Pause, true));
            Assert.AreEqual((int)Key.Escape, m.DisplayKey(LogicalKey.Pause, true));
            Assert.IsFalse(m.Clear(LogicalKey.Pause, true), "고정 Esc는 지울 수 없다");
            Assert.IsTrue(m.CanCapture(LogicalKey.Pause, false), "일시정지는 재설정 칸 하나만 받는다");
            m.BeginCapture(LogicalKey.Pause, false);
            Assert.AreEqual(RebindCaptureEnd.CancelledByEsc, m.OnKey((int)Key.Escape), "Esc는 어느 칸에도 넣을 수 없다");
            Assert.AreEqual(0, m.Primary(LogicalKey.Pause));
            m.BeginCapture(LogicalKey.Pause, false);
            m.OnKey((int)Key.P);
            Assert.AreEqual((int)Key.P, m.Primary(LogicalKey.Pause));
        }

        [Test]
        public void S08_12_3__SaveCustomSetsLayoutCustomAndSavesOnce()
        {
            var store = new CountingSettingsStore { Current = DefaultSettings() };
            var m = new KeyRebindModel(store.Current);
            m.BeginCapture(LogicalKey.Fire, false);
            m.OnKey((int)Key.L);
            Assert.IsTrue(m.CanSave);
            Assert.IsTrue(m.Save(store));
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual(1, store.ChangedCount);
            Assert.AreEqual(OptionValues.Custom, SettingsOptions.Str(store.Current, OptionKeys.KeyLayout));
            Assert.AreEqual((int)Key.L, KeyLayouts.PrimaryOf(store.Current, LogicalKey.Fire));
            Assert.AreEqual(9, store.Current.KeyPrimary.Count);
            Assert.AreEqual(9, store.Current.KeySecondary.Count);
        }

        [Test]
        public void S08_12_3__SaveAlternateTabAppliesTable()
        {
            var store = new CountingSettingsStore { Current = DefaultSettings() };
            var m = new KeyRebindModel(store.Current);
            m.SelectLayout(OptionValues.Alternate);
            Assert.IsTrue(m.Save(store));
            Assert.AreEqual(OptionValues.Alternate, SettingsOptions.Str(store.Current, OptionKeys.KeyLayout));
            CollectionAssert.AreEqual(KeyLayouts.AlternatePrimary, store.Current.KeyPrimary);
            CollectionAssert.AreEqual(KeyLayouts.AlternateSecondary, store.Current.KeySecondary);
        }

        [Test]
        public void S08_12_3__CannotSaveWhileCapturing()
        {
            var store = new CountingSettingsStore { Current = DefaultSettings() };
            var m = new KeyRebindModel(store.Current);
            m.BeginCapture(LogicalKey.Up, false);
            Assert.IsFalse(m.CanSave);
            Assert.IsFalse(m.Save(store));
        }

        [Test]
        public void S08_12_3__DisplayOrderAndActionKeys()
        {
            CollectionAssert.AreEqual(new[]
            {
                LogicalKey.Up, LogicalKey.Down, LogicalKey.Left, LogicalKey.Right, LogicalKey.Fire, LogicalKey.Skill, LogicalKey.Stance,
                LogicalKey.Reverse, LogicalKey.Pause
            }, KeyRebindModel.DisplayOrder);
            Assert.AreEqual("str.key_rebind.action.art", KeyRebindModel.ActionNameKey(LogicalKey.Skill));
            Assert.AreEqual("str.key_rebind.action.reverse_stance", KeyRebindModel.ActionNameKey(LogicalKey.Reverse));
            foreach (var k in KeyRebindModel.DisplayOrder) Assert.IsTrue(TestContent.Names.Has(KeyRebindModel.ActionNameKey(k)), k.ToString());
        }

        [Test]
        public void S08_12_3__UsedKeysIncludeLayoutAndEsc()
        {
            var m = new KeyRebindModel(DefaultSettings());
            var used = m.UsedKeys();
            Assert.IsTrue(used.Contains((int)Key.W));
            Assert.IsTrue(used.Contains((int)Key.UpArrow));
            Assert.IsTrue(used.Contains((int)Key.Escape));
            Assert.IsFalse(used.Contains((int)Key.Q));
        }
    }
}
