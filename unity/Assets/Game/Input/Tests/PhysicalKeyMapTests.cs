#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.InputSystem;
using Wizard.Client;

namespace Wizard.Unity.Input.Tests
{
    // PhysicalKey(엔진 없는 번호) ↔ Input System Key(자리). 모든 PhysicalKey 가 서로 다른 Key 하나에 대응하고 되돌아온다
    public sealed class PhysicalKeyMapTests
    {
        [Test]
        public void EveryPhysicalKey_MapsToADistinctKey_AndBack()
        {
            var seen = new HashSet<Key>();
            int count = 0;
            foreach (PhysicalKey p in (PhysicalKey[])Enum.GetValues(typeof(PhysicalKey)))
            {
                if (p == PhysicalKey.None)
                {
                    continue;
                }
                Key key = PhysicalKeyMap.ToKey(p);
                Assert.That(key, Is.Not.EqualTo(Key.None), p.ToString());
                Assert.That(seen.Add(key), Is.True, "two physical keys share " + key);
                Assert.That(PhysicalKeyMap.TryToPhysical(key, out PhysicalKey back), Is.True, p.ToString());
                Assert.That(back, Is.EqualTo(p));
                count++;
            }
            Assert.That(PhysicalKeyMap.MappedCount, Is.EqualTo(count));
        }

        [Test]
        public void BattleKeys_MapToTheirPositions()
        {
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.W), Is.EqualTo(Key.W));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.A), Is.EqualTo(Key.A));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.S), Is.EqualTo(Key.S));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.D), Is.EqualTo(Key.D));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.J), Is.EqualTo(Key.J));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.K), Is.EqualTo(Key.K));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.LeftShift), Is.EqualTo(Key.LeftShift));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.RightShift), Is.EqualTo(Key.RightShift));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.Space), Is.EqualTo(Key.Space));
            Assert.That(PhysicalKeyMap.ToKey(PhysicalKey.Escape), Is.EqualTo(Key.Escape));
        }

        [TestCase(Key.None)]
        [TestCase(Key.OEM1)]
        [TestCase(Key.NumpadEquals)]
        [TestCase(Key.ContextMenu)]
        public void KeysWithoutAPhysicalNumber_AreNotMapped(Key key)
        {
            Assert.That(PhysicalKeyMap.TryToPhysical(key, out PhysicalKey p), Is.False);
            Assert.That(p, Is.EqualTo(PhysicalKey.None));
        }

        [Test]
        public void OutOfRange_IsNotMapped()
        {
            Assert.That(PhysicalKeyMap.ToKey((PhysicalKey)KeyBindings.PhysicalKeyCapacity), Is.EqualTo(Key.None));
            Assert.That(PhysicalKeyMap.TryToPhysical((Key)(-1), out _), Is.False);
            Assert.That(PhysicalKeyMap.TryToPhysical((Key)10000, out _), Is.False);
        }
    }
}
