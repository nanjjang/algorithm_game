using System;
using System.Linq;
using Wizard.Core;
using Wizard.Sim.Input;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Sim.Tests.Contracts
{
    // CONTRACTS 3.1 입력 타입의 모양과 정렬 키, spec 01 4.3~4.4의 KeyState 규칙
    public sealed class InputContractTests
    {
        private static PlayerInput In(byte slot, uint tick, byte offset, LogicalKey key, uint seq, bool down = true) =>
            new PlayerInput(new PlayerSlot(slot), new InputEvent(tick, offset, key, down, seq));

        [Fact]
        public void LogicalKey_IsSpec01CodesInFourBits()
        {
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LogicalKey)));
            ContractShape.Enum<LogicalKey>(
                ("Up", 0), ("Left", 1), ("Down", 2), ("Right", 3), ("Fire", 4), ("Skill", 5), ("Stance", 6), ("Reverse", 7), ("Pause", 8));
        }

        [Fact]
        public void InputEvent_FieldsAndPacking()
        {
            ContractShape.Fields(typeof(InputEvent),
                ("Tick", typeof(uint)), ("Offset", typeof(byte)), ("Key", typeof(LogicalKey)), ("Down", typeof(bool)), ("Seq", typeof(uint)));
            Assert.NotNull(typeof(InputEvent).GetConstructor(new[] { typeof(uint), typeof(byte), typeof(LogicalKey), typeof(bool), typeof(uint) }));
            var e = new InputEvent(10, 50, LogicalKey.Right, false, 8);
            Assert.Equal(10L * 256 + 50, e.Time);
            Assert.Equal((byte)LogicalKey.Right, e.PackKey());
            Assert.Equal((byte)(0x10 | (int)LogicalKey.Pause), new InputEvent(0, 0, LogicalKey.Pause, true, 1).PackKey());
        }

        [Fact]
        public void QuantizedKey_HasNoSeq()
        {
            ContractShape.Fields(typeof(QuantizedKey),
                ("Tick", typeof(uint)), ("Offset", typeof(byte)), ("Key", typeof(LogicalKey)), ("Down", typeof(bool)));
        }

        [Fact]
        public void SeqCounter_LastAndNext()
        {
            ContractShape.Fields(typeof(SeqCounter), ("Last", typeof(uint)));
            var c = default(SeqCounter);
            Assert.Equal(0u, c.Last);
            Assert.Equal(1u, c.Next());
            Assert.Equal(2u, c.Next());
            Assert.Equal(2u, c.Last);
        }

        [Fact]
        public void KeyMask_IsUshortFlagsWithBitPerLogicalKey()
        {
            Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(KeyMask)));
            Assert.True(typeof(KeyMask).IsDefined(typeof(FlagsAttribute), false));
            Assert.Equal(0, (int)KeyMask.None);
            foreach (LogicalKey k in Enum.GetValues(typeof(LogicalKey)))
            {
                Assert.Equal(1 << (int)k, (int)KeyState.Bit(k));
            }
        }

        [Fact]
        public void PlayerInput_IsSlotAndEvent()
        {
            ContractShape.Fields(typeof(PlayerInput), ("Slot", typeof(PlayerSlot)), ("Event", typeof(InputEvent)));
        }

        [Fact]
        public void InputOrder_SortKeyIsTickOffsetClassSlotSeq()
        {
            Assert.Equal(8, InputOrder.MaxEventsPerTickPerPlayer);
            // 앞의 열쇠가 다르면 뒤의 열쇠는 보지 않는다. 각 줄은 앞 열쇠가 같고 그 열쇠에서만 순서가 갈린다
            Assert.True(InputOrder.Compare(In(1, 9, 255, LogicalKey.Pause, 9), In(0, 10, 0, LogicalKey.Stance, 1)) < 0);   // 틱
            Assert.True(InputOrder.Compare(In(1, 10, 3, LogicalKey.Pause, 9), In(0, 10, 4, LogicalKey.Stance, 1)) < 0);    // 오프셋
            Assert.True(InputOrder.Compare(In(1, 10, 4, LogicalKey.Right, 9), In(0, 10, 4, LogicalKey.Skill, 1)) < 0);     // 분류: 방향 < K
            Assert.True(InputOrder.Compare(In(1, 10, 4, LogicalKey.Stance, 9), In(0, 10, 4, LogicalKey.Up, 1)) < 0);       // 분류: 자세 < 방향
            Assert.True(InputOrder.Compare(In(0, 10, 4, LogicalKey.Fire, 9), In(1, 10, 4, LogicalKey.Fire, 1)) < 0);       // 슬롯
            Assert.True(InputOrder.Compare(In(0, 10, 4, LogicalKey.Up, 1), In(0, 10, 4, LogicalKey.Left, 2)) < 0);         // 순번
            Assert.Equal(0, InputOrder.Compare(In(0, 10, 4, LogicalKey.Up, 1), In(0, 10, 4, LogicalKey.Up, 1)));
            // 대칭
            Assert.True(InputOrder.Compare(In(0, 10, 4, LogicalKey.Skill, 1), In(0, 10, 4, LogicalKey.Right, 2)) > 0);
        }

        // CONTRACTS 3.1·0.3: KeyClass 는 Stance·Reverse 0, 방향 1, Fire 2, Skill 3, Pause 4. 시그니처는 계약의 코드 조각 그대로
        [Fact]
        public void InputOrder_KeyClassAndSignaturesMatchContract()
        {
            var expected = new (LogicalKey Key, int Class)[]
            {
                (LogicalKey.Up, 1), (LogicalKey.Left, 1), (LogicalKey.Down, 1), (LogicalKey.Right, 1),
                (LogicalKey.Fire, 2), (LogicalKey.Skill, 3), (LogicalKey.Stance, 0), (LogicalKey.Reverse, 0), (LogicalKey.Pause, 4),
            };
            Assert.Equal(Enum.GetValues(typeof(LogicalKey)).Length, expected.Length);
            foreach (var (key, cls) in expected)
            {
                Assert.Equal(cls, InputOrder.KeyClass(key));
            }
            Assert.Throws<SimInvariantException>(() => InputOrder.KeyClass((LogicalKey)9));

            Type t = typeof(InputOrder);
            Assert.True(t.IsAbstract && t.IsSealed);                // static class
            ContractShape.HasPublicStaticMethod(t, "KeyClass", typeof(int), typeof(LogicalKey));
            System.Reflection.MethodInfo compare = t.GetMethod("Compare",
                new[] { typeof(PlayerInput).MakeByRefType(), typeof(PlayerInput).MakeByRefType() })!;
            Assert.NotNull(compare);
            Assert.Equal(typeof(int), compare.ReturnType);
            Assert.All(compare.GetParameters(), p => Assert.True(p.IsIn, p.Name + " 은 in 매개변수가 아니다"));
            ContractShape.HasPublicStaticMethod(t, "Sort", typeof(void), typeof(Span<PlayerInput>));
        }

        [Fact]
        public void InputOrder_SortIsStableTotalOrder()
        {
            var inputs = new[]
            {
                In(1, 10, 50, LogicalKey.Skill, 7),
                In(0, 10, 50, LogicalKey.Skill, 7),
                In(0, 10, 50, LogicalKey.Right, 8),
                In(0, 10, 49, LogicalKey.Pause, 9),
                In(0, 9, 255, LogicalKey.Fire, 6),
            };
            InputOrder.Sort(inputs);
            Assert.Equal(new[] { (0, 6u), (0, 9u), (0, 8u), (0, 7u), (1, 7u) }, inputs.Select(x => ((int)x.Slot.Value, x.Event.Seq)));
        }

        [Fact]
        public void RunCommandOp_ValuesMatchContract()
        {
            Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(RunCommandOp)));
            ContractShape.Enum<RunCommandOp>(
                ("PickCard", 1), ("RerollCards", 2), ("DiscardBlock", 3), ("SetSlot", 4), ("SwapSlots", 5), ("Heal", 6),
                ("UnlockArt", 7), ("Depart", 8), ("AbandonRun", 9), ("CloseCampScreen", 10), ("EnterPractice", 11), ("ExitPractice", 12),
                ("DebugSkipWave", 200), ("DebugInvulnerable", 201), ("DebugGrant", 202), ("DebugSetStage", 203));
        }

        [Fact]
        public void RunCommand_FieldsMatchContract()
        {
            ContractShape.Fields(typeof(RunCommand),
                ("Tick", typeof(uint)), ("Seq", typeof(uint)), ("Slot", typeof(PlayerSlot)), ("Op", typeof(RunCommandOp)),
                ("A", typeof(int)), ("B", typeof(int)));
            Assert.True(new RunCommand(1, 1, new PlayerSlot(0), RunCommandOp.DebugGrant, 0, 0).IsDebug);
            Assert.False(new RunCommand(1, 1, new PlayerSlot(0), RunCommandOp.ExitPractice, 0, 0).IsDebug);
        }

        [Fact]
        public void KeyState_IgnoresRepeatedPressAndStrayRelease()
        {
            var ks = default(KeyState);
            Assert.False(ks.IsDown(LogicalKey.Up));
            Assert.False(ks.TryGetLastPress(LogicalKey.Up, out _));

            Assert.True(ks.Apply(new InputEvent(5, 10, LogicalKey.Up, true, 1)));
            Assert.True(ks.IsDown(LogicalKey.Up));
            Assert.True(ks.TryGetLastPress(LogicalKey.Up, out long t));
            Assert.Equal(5L * 256 + 10, t);

            // 눌린 키의 누름: 무시, 마지막 누름 시각도 그대로
            Assert.False(ks.Apply(new InputEvent(6, 0, LogicalKey.Up, true, 2)));
            Assert.True(ks.TryGetLastPress(LogicalKey.Up, out t));
            Assert.Equal(5L * 256 + 10, t);

            // 안 눌린 키의 뗌: 무시
            Assert.False(ks.Apply(new InputEvent(6, 1, LogicalKey.Left, false, 3)));
            Assert.False(ks.IsDown(LogicalKey.Left));

            Assert.True(ks.Apply(new InputEvent(7, 0, LogicalKey.Up, false, 4)));
            Assert.False(ks.IsDown(LogicalKey.Up));
            Assert.True(ks.TryGetLastPress(LogicalKey.Up, out t));   // 뗀 뒤에도 마지막 누름 시각은 남는다
            Assert.Equal(5L * 256 + 10, t);
            Assert.Equal(KeyMask.None, ks.Mask);
        }

        [Fact]
        public void KeyState_PressAtTimeZeroIsDistinguishedFromNever()
        {
            var ks = default(KeyState);
            Assert.True(ks.Apply(new InputEvent(0, 0, LogicalKey.Fire, true, 1)));
            Assert.True(ks.TryGetLastPress(LogicalKey.Fire, out long t));
            Assert.Equal(0L, t);
            Assert.False(ks.TryGetLastPress(LogicalKey.Skill, out _));
        }

        [Fact]
        public void KeyState_MaskMatchesDownKeys()
        {
            var ks = default(KeyState);
            ks.Apply(new InputEvent(1, 0, LogicalKey.Stance, true, 1));
            ks.Apply(new InputEvent(1, 1, LogicalKey.Right, true, 2));
            ks.Apply(new InputEvent(1, 2, LogicalKey.Pause, true, 3));
            Assert.Equal(KeyState.Bit(LogicalKey.Stance) | KeyState.Bit(LogicalKey.Right) | KeyState.Bit(LogicalKey.Pause), ks.Mask);
        }

        [Fact]
        public void KeyState_IsAValueCopy()
        {
            var a = default(KeyState);
            a.Apply(new InputEvent(1, 0, LogicalKey.Up, true, 1));
            KeyState b = a;                                   // 스냅숏은 값 복사다
            b.Apply(new InputEvent(2, 0, LogicalKey.Up, false, 2));
            b.Apply(new InputEvent(2, 1, LogicalKey.Down, true, 3));
            Assert.True(a.IsDown(LogicalKey.Up));
            Assert.False(a.IsDown(LogicalKey.Down));
            Assert.True(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<KeyState>() == false);
        }

        [Fact]
        public void KeyState_WriteReadRoundTripAndHash()
        {
            var ks = default(KeyState);
            ks.Apply(new InputEvent(3, 7, LogicalKey.Skill, true, 1));
            ks.Apply(new InputEvent(4, 9, LogicalKey.Reverse, true, 2));
            ks.Apply(new InputEvent(5, 0, LogicalKey.Skill, false, 3));

            var bytes = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bytes);
            ks.Write(ref w);
            StateWriter h1 = StateWriter.ForHash();
            ks.Write(ref h1);

            var back = default(KeyState);
            var r = new StateReader(bytes.Written);
            back.Read(ref r);
            Assert.Equal(0, r.Remaining);
            Assert.Equal(ks.Mask, back.Mask);
            Assert.True(back.TryGetLastPress(LogicalKey.Skill, out long t1));
            Assert.Equal(3L * 256 + 7, t1);
            Assert.True(back.TryGetLastPress(LogicalKey.Reverse, out long t2));
            Assert.Equal(4L * 256 + 9, t2);
            Assert.False(back.TryGetLastPress(LogicalKey.Up, out _));

            StateWriter h2 = StateWriter.ForHash();
            back.Write(ref h2);
            Assert.Equal(h1.Hash, h2.Hash);

            // 상태가 다르면 해시가 다르다(누름 시각 하나만 달라도)
            var other = default(KeyState);
            other.Apply(new InputEvent(3, 8, LogicalKey.Skill, true, 1));
            other.Apply(new InputEvent(4, 9, LogicalKey.Reverse, true, 2));
            other.Apply(new InputEvent(5, 0, LogicalKey.Skill, false, 3));
            StateWriter h3 = StateWriter.ForHash();
            other.Write(ref h3);
            Assert.NotEqual(h1.Hash, h3.Hash);
        }

        [Fact]
        public void KeyState_ReadRejectsImpossibleBits()
        {
            var bytes = new ByteWriter();
            StateWriter w = StateWriter.ForBytes(bytes);
            w.U32(1u);       // Up 눌림
            w.U32(0u);       // 그런데 한 번도 안 눌림 → 깨진 상태
            for (int i = 0; i < KeyState.KeyCount; i++)
            {
                w.I64(0);
            }
            byte[] data = bytes.ToArray();
            Assert.Throws<SimInvariantException>(() =>
            {
                var ks = default(KeyState);
                var r = new StateReader(data);
                ks.Read(ref r);
            });
        }

        // 같은 각본을 새 키 상태로 여러 번 돌려 가장 작은 값이 0인지 본다(AllocationProbe. 한 번 재기는 병렬 실행에서 가끔 흔들렸다, FIX-1)
        [Fact]
        public void KeyState_ApplyAllocatesNothing()
        {
            var down = new InputEvent(1, 0, LogicalKey.Fire, true, 1);
            var up = new InputEvent(1, 1, LogicalKey.Fire, false, 2);
            var ks = default(KeyState);
            long least = AllocationProbe.LeastBytes(() =>
            {
                ks = default(KeyState);
                ks.Apply(down);
                ks.Apply(up);
                return () =>
                {
                    for (int i = 0; i < 1000; i++)
                    {
                        ks.Apply(down);
                        ks.Apply(up);
                    }
                };
            }, () => Assert.False(ks.IsDown(LogicalKey.Fire)));
            Assert.Equal(0L, least);
        }
    }
}
