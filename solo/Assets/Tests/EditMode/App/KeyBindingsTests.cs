using NUnit.Framework;
using UnityEngine.InputSystem;
using Wizard.Solo.App;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Tests.App
{
    /// <summary>키 배치(02 요약서 3.1절): 기본 W/A/S/D/J/K/Shift/Space, 대체 ↑/←/↓/→/Z/X/C/V, Pause는 Esc 고정이라 칸에 없음.</summary>
    public class KeyBindingsTests
    {
        [Test]
        public void NullSettings_DefaultPrimaryAlternateSecondary()
        {
            var b = KeyBindings.FromSettings(null);
            Assert.AreEqual(Key.W, b.Primary[(int)LogicalKey.Up]);
            Assert.AreEqual(Key.UpArrow, b.Secondary[(int)LogicalKey.Up]);
            Assert.AreEqual(Key.J, b.Primary[(int)LogicalKey.Fire]);
            Assert.AreEqual(Key.Z, b.Secondary[(int)LogicalKey.Fire]);
            Assert.AreEqual(Key.LeftShift, b.Primary[(int)LogicalKey.Stance]);
            Assert.AreEqual(Key.C, b.Secondary[(int)LogicalKey.Stance]);
            Assert.AreEqual(Key.Space, b.Primary[(int)LogicalKey.Reverse]);
            Assert.AreEqual(Key.V, b.Secondary[(int)LogicalKey.Reverse]);
            Assert.AreEqual(Key.None, b.Primary[(int)LogicalKey.Pause]);
            Assert.AreEqual(Key.None, b.Secondary[(int)LogicalKey.Pause]);
        }

        [Test]
        public void EmptyLists_UseLayoutTable()
        {
            var s = new SettingsData();
            SettingsOptions.Set(s, OptionKeys.KeyLayout, OptionValues.Alternate);
            var b = KeyBindings.FromSettings(s);
            Assert.AreEqual(Key.LeftArrow, b.Primary[(int)LogicalKey.Left]);
            // 08 요약서 12.3절(TV-172): 대체 배치는 ↑/←/↓/→/Z/X/C/V뿐이다. 보조 칸은 비어 있다(Core의 Meta.KeyLayouts가 정본).
            Assert.AreEqual(Key.None, b.Secondary[(int)LogicalKey.Left]);
        }

        [Test]
        public void Custom_EmptySlotsStayEmpty_EscapeStripped()
        {
            var s = new SettingsData();
            SettingsOptions.Set(s, OptionKeys.KeyLayout, OptionValues.Custom);
            for (int i = 0; i < SimConst.LogicalKeyCount; i++) { s.KeyPrimary.Add(0); s.KeySecondary.Add(0); }
            s.KeyPrimary[(int)LogicalKey.Skill] = (int)Key.L;
            s.KeyPrimary[(int)LogicalKey.Fire] = (int)Key.Escape;
            var b = KeyBindings.FromSettings(s);
            Assert.AreEqual(Key.L, b.Primary[(int)LogicalKey.Skill]);
            Assert.AreEqual(Key.None, b.Secondary[(int)LogicalKey.Skill]);
            Assert.AreEqual(Key.None, b.Primary[(int)LogicalKey.Fire]);
            Assert.AreEqual(Key.None, b.Primary[(int)LogicalKey.Up]);
        }

        [Test]
        public void DefaultLayout_PartialValues_FillRestFromTable()
        {
            var s = new SettingsData();
            s.KeyPrimary.Add((int)Key.I); // Up만 바꿈
            var b = KeyBindings.FromSettings(s);
            Assert.AreEqual(Key.I, b.Primary[(int)LogicalKey.Up]);
            Assert.AreEqual(Key.UpArrow, b.Secondary[(int)LogicalKey.Up]);
            Assert.AreEqual(Key.A, b.Primary[(int)LogicalKey.Left]);
        }

        [Test]
        public void InvalidKeyNumber_Ignored()
        {
            var s = new SettingsData();
            SettingsOptions.Set(s, OptionKeys.KeyLayout, OptionValues.Custom);
            s.KeyPrimary.Add(999999);
            var b = KeyBindings.FromSettings(s);
            Assert.AreEqual(Key.None, b.Primary[(int)LogicalKey.Up]);
        }
    }
}
