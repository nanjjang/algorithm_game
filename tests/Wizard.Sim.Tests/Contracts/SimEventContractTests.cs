using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Wizard.Core;
using Wizard.Sim.Events;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // CONTRACTS 3.3: SimEvent(고정 크기, 참조 없음), CONTRACTS가 번호를 고정한 값, SimEventDocs.BindingKey
    public sealed class SimEventContractTests
    {
        private static readonly Regex KeyForm = new Regex("^[a-z][a-z0-9_]*(\\.[a-z0-9]+(_[a-z0-9]+)*)+$", RegexOptions.CultureInvariant);

        [Fact]
        public void SimEvent_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(SimEvent),
                ("Type", typeof(SimEventType)), ("Slot", typeof(PlayerSlot)), ("Tick", typeof(uint)),
                ("Source", typeof(EntityId)), ("Target", typeof(EntityId)),
                ("A", typeof(int)), ("B", typeof(int)), ("C", typeof(int)),
                ("X", typeof(long)), ("Y", typeof(long)), ("V", typeof(long)));
            Assert.True(typeof(SimEvent).IsValueType);
        }

        [Fact]
        public void SimEvent_HasNoReferencesAndFixedSize()
        {
            Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<SimEvent>());
            // ushort(2) + PlayerSlot(1) + 채움(1) + uint(4) + EntityId×2(8) + int×3(12) + 채움(4) + long×3(24) = 56
            Assert.Equal(56, Unsafe.SizeOf<SimEvent>());
            AssertNoReferenceFields(typeof(SimEvent), new HashSet<Type>());
        }

        private static void AssertNoReferenceFields(Type t, HashSet<Type> seen)
        {
            if (!seen.Add(t))
            {
                return;
            }
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Type ft = f.FieldType;
                Assert.True(ft.IsValueType, t.Name + "." + f.Name + " 은 참조 형식이다");
                Assert.False(ft.IsPointer, t.Name + "." + f.Name + " 은 포인터다");
                if (!ft.IsPrimitive && !ft.IsEnum)
                {
                    AssertNoReferenceFields(ft, seen);
                }
            }
        }

        [Fact]
        public void SimEventType_IsUshort()
        {
            Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(SimEventType)));
        }

        // CONTRACTS 3.3의 코드 조각이 번호까지 적은 값 전부
        [Fact]
        public void SimEventType_ContractFixedNumbers()
        {
            var fixedValues = new (SimEventType Type, int Value)[]
            {
                (SimEventType.StanceEntered, 100), (SimEventType.StanceExited, 101), (SimEventType.ReverseFlipped, 102),
                (SimEventType.FacingChanged, 103), (SimEventType.CommandReset, 104),
                (SimEventType.ShotFired, 200), (SimEventType.CritShotFired, 201), (SimEventType.CritHit, 202),
                (SimEventType.HeatEnteredImminent, 203), (SimEventType.HeatEnteredCritical, 204), (SimEventType.HeatEnteredBrink, 205),
                (SimEventType.HeatLockEntered, 206), (SimEventType.FireDryWhileLocked, 207), (SimEventType.HeatUnlocked, 208),
                (SimEventType.HeatExemptArmed, 209), (SimEventType.HeatExemptConsumed, 210), (SimEventType.KilnAbsorbed, 211),
                (SimEventType.BoiloverTriggered, 212), (SimEventType.UnstableCoreExploded, 213),
                (SimEventType.GrazeAwarded, 300), (SimEventType.GrazeCosmeticOnly, 301), (SimEventType.GrazeBuffStarted, 302),
                (SimEventType.GrazeBuffEnded, 303), (SimEventType.LightningRodStrike, 304), (SimEventType.ComboChanged, 310),
                (SimEventType.ComboReset, 311), (SimEventType.CrystalGained, 320), (SimEventType.CrystalsFull, 321),
                (SimEventType.CrystalGainBlocked, 322), (SimEventType.CrystalsSpent, 323), (SimEventType.CrystalsShattered, 324),
                (SimEventType.CrystalProtected, 325), (SimEventType.CrackChanged, 326), (SimEventType.DebtCreated, 327),
                (SimEventType.DebtRepaid, 328), (SimEventType.CrystalsConverted, 329),
                (SimEventType.StarfallTelegraph, 340), (SimEventType.StarfallLanded, 341), (SimEventType.StarfallBlink, 342),
                (SimEventType.StarfallPicked, 343), (SimEventType.StarfallExpired, 344),
                (SimEventType.PlayerHit, 360), (SimEventType.InvulnEnded, 361), (SimEventType.PlayerDowned, 362),
                (SimEventType.PlayerRose, 363), (SimEventType.HitstopRequest, 370),
                (SimEventType.SpellNodeExecuted, 480),
            };
            foreach (var (type, value) in fixedValues)
            {
                Assert.Equal(value, (int)type);
            }
            // 100~399에는 CONTRACTS가 적은 것 말고는 없다
            int[] below400 = AllTypes().Select(t => (int)t).Where(v => v >= 100 && v < 400).OrderBy(v => v).ToArray();
            Assert.Equal(fixedValues.Select(f => f.Value).Where(v => v < 400).OrderBy(v => v), below400);
        }

        [Fact]
        public void SimEventType_HasEventsInEveryRange100To999()
        {
            int[] values = AllTypes().Select(t => (int)t).ToArray();
            for (int lo = 100; lo < 1000; lo += 100)
            {
                Assert.True(values.Any(v => v >= lo && v < lo + 100), lo + "번대가 비었다");
            }
            Assert.All(values, v => Assert.True(v == 0 || v >= 100, v + " 는 구간 밖이다"));
        }

        [Fact]
        public void BindingKey_IsUniqueAndWellFormedForEveryValue()
        {
            var seen = new Dictionary<string, SimEventType>(StringComparer.Ordinal);
            foreach (SimEventType t in Enum.GetValues(typeof(SimEventType)).Cast<SimEventType>())
            {
                string key = SimEventDocs.BindingKey(t);
                Assert.Matches(KeyForm, key);
                Assert.False(seen.TryGetValue(key, out SimEventType other), key + " 가 " + t + " 와 " + other + " 에서 겹친다");
                seen.Add(key, t);
            }
            Assert.Equal(Enum.GetValues(typeof(SimEventType)).Length, seen.Count);
        }

        [Fact]
        public void BindingKey_ContractExample()
        {
            ContractShape.HasPublicStaticMethod(typeof(SimEventDocs), "BindingKey", typeof(string), typeof(SimEventType));
            Assert.True(typeof(SimEventDocs).IsAbstract && typeof(SimEventDocs).IsSealed);           // static class

            Assert.Equal("heat.lock_entered", SimEventDocs.BindingKey(SimEventType.HeatLockEntered));   // CONTRACTS 3.3·13절의 예
            Assert.Equal("telegraph.start", SimEventDocs.BindingKey(SimEventType.TelegraphStart));     // CONTRACTS 3.3의 예(TelegraphStart ↔ telegraph.start)
            Assert.Equal("spell.node_executed", SimEventDocs.BindingKey(SimEventType.SpellNodeExecuted));
        }

        // 400번대 이후의 점 이름 열쇠는 열거형 이름과 같은 말이다(PascalCase ↔ 구역.snake_case). spec 04의 PascalCase 이름은 char./wand. 구역
        [Fact]
        public void BindingKey_DottedSpecNamesArePascalCaseOfEnumName()
        {
            foreach (SimEventType t in AllTypes().Where(t => (int)t >= 400))
            {
                string key = SimEventDocs.BindingKey(t);
                if (key.StartsWith("char.", StringComparison.Ordinal) || key.StartsWith("wand.", StringComparison.Ordinal))
                {
                    continue;
                }
                Assert.Equal(t.ToString(), Pascal(key));
            }
        }

        [Fact]
        public void BindingKey_UnknownValueIsInvariantViolation()
        {
            Assert.Throws<SimInvariantException>(() => SimEventDocs.BindingKey((SimEventType)65000));
        }

        // 같은 각본을 여러 번 돌려 가장 작은 값이 0인지 본다(AllocationProbe. 한 번 재기는 병렬 실행에서 가끔 흔들렸다, FIX-1)
        [Fact]
        public void BindingKey_AllocatesNothing()
        {
            SimEventType[] all = AllTypes().ToArray();
            foreach (SimEventType t in all)
            {
                SimEventDocs.BindingKey(t);
            }
            int total = 0;
            long least = AllocationProbe.LeastBytes(() =>
            {
                total = 0;
                return () =>
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        total += SimEventDocs.BindingKey(all[i]).Length;
                    }
                };
            }, () => Assert.True(total > 0));
            Assert.Equal(0L, least);
        }

        internal static IEnumerable<SimEventType> AllTypes() =>
            Enum.GetValues(typeof(SimEventType)).Cast<SimEventType>().Where(t => t != SimEventType.None);

        internal static string Pascal(string key) =>
            string.Concat(key.Split('.', '_').Select(s => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1)));
    }
}
