using Wizard.Content;
using Wizard.Sim.Arts;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // spec 03 6.2의 직교 상태 Ready / Cooldown / CooldownBuffered 와 10절(K 보관). B09·B10 벡터는 다른 작업에 배정되어 있어
    // 벡터 이름을 쓰지 않고 같은 사례를 상태 기계 시험으로 본다(결정 5 → 4 → 3, 보관 창 4,608u, 발동 틱 ceil(대기 끝 ÷ 256))
    public sealed class CooldownStateMachineTests
    {
        private const long T0 = ParserRig.T0;

        private static (CommandParser p, TestWallet w, UnlockSet u) OutOfStance(int crystals)
        {
            var p = new CommandParser(SpecArts.Trie(CharacterId.Char1));
            return (p, SpecArts.Wallet(CharacterId.Char1, crystals, 0), Unlocks.All.For(CharacterId.Char1));
        }

        [Fact]
        public void Fire_StartsCooldown_EarlyKIgnored_WindowKBuffered_ReleasedAtCeilTick()
        {
            var (p, w, u) = OutOfStance(5);
            Assert.True(p.OnSkillPress(T0, w, in u, out ArtDecision first));
            Assert.Equal(ArtId.Burst, first.Art);
            Assert.Equal(T0 + SpecArts.ArtCooldown, p.CooldownEnd);
            Assert.Equal(4, w.Crystals);

            Assert.False(p.OnSkillPress(T0 + 3000, w, in u, out _));          // 보관 창(대기 끝 − 1,536u) 전: 무시
            Assert.False(p.HasBuffered);
            Assert.Equal(4, w.Crystals);

            Assert.False(p.OnSkillPress(T0 + 4700, w, in u, out _));          // 보관: 누른 시각으로 선택·결제
            Assert.True(p.HasBuffered);
            Assert.Equal(3, w.Crystals);

            long end = T0 + SpecArts.ArtCooldown;
            long fireTick = (end + 255) / 256;                                 // ceil(대기 끝 ÷ 256)
            Assert.False(p.TryTakeBuffered((fireTick - 1) * 256, out _));
            Assert.True(p.TryTakeBuffered(fireTick * 256, out ArtDecision held));
            Assert.Equal(ArtOutcome.Fire, held.Outcome);
            Assert.Equal(ArtId.Burst, held.Art);
            Assert.Equal(T0 + 4700, held.JudgeTime);
            Assert.False(p.HasBuffered);
            Assert.Equal(fireTick * 256 + SpecArts.ArtCooldown, p.CooldownEnd);
        }

        [Fact]
        public void BufferWindowStart_IsBuffered_AndSecondBufferedKIgnored()
        {
            var (p, w, u) = OutOfStance(5);
            Assert.True(p.OnSkillPress(T0, w, in u, out _));
            Assert.False(p.OnSkillPress(T0 + SpecArts.ArtCooldown - SpecArts.KBufferWindow, w, in u, out _));   // 4,608u: 경계 포함
            Assert.True(p.HasBuffered);
            Assert.Equal(3, w.Crystals);
            Assert.False(p.OnSkillPress(T0 + 5000, w, in u, out _));           // 보관 중의 K: 무시
            Assert.Equal(3, w.Crystals);
            Assert.False(p.OnSkillPress(T0 + SpecArts.ArtCooldown - SpecArts.KBufferWindow - 1, w, in u, out _));
        }

        [Fact]
        public void IgnoredKDuringCooldown_KeepsCursorAndHistory()
        {
            CommandParser p = ParserRig.InStance(CharacterId.Char1);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            Assert.True(p.OnSkillPress(T0, w, in u, out _));
            p.OnDirection(CmdDir.Forward, T0 + 100);
            Assert.False(p.OnSkillPress(T0 + 200, w, in u, out _));
            Assert.Equal("F", ParserRig.Letters(p.Trie.Path(p.Cursor)));
            Assert.Equal(1, p.DirCount);
        }

        [Fact]
        public void Fizzle_DoesNotStartCooldown()
        {
            var (p, w, u) = OutOfStance(0);
            Assert.True(p.OnSkillPress(T0, w, in u, out ArtDecision v));
            Assert.Equal(FizzleReason.NoCrystal, v.Reason);
            Assert.Equal(0, p.CooldownEnd);
            w.Crystals = 1;
            Assert.True(p.OnSkillPress(T0 + 100, w, in u, out ArtDecision v2));
            Assert.Equal(ArtOutcome.Fire, v2.Outcome);
            Assert.Equal(ArtId.Burst, v2.Art);
        }

        [Fact]
        public void WaveOrRoundStart_ClearsCooldown_PauseKeepsIt()
        {
            var (p, w, u) = OutOfStance(5);
            Assert.True(p.OnSkillPress(T0, w, in u, out _));
            p.Reset(CommandResetReason.Pause);
            Assert.Equal(T0 + SpecArts.ArtCooldown, p.CooldownEnd);
            p.Reset(CommandResetReason.WaveStart);
            Assert.Equal(0, p.CooldownEnd);
            Assert.True(p.OnSkillPress(T0 + 10, w, in u, out _));
            p.Reset(CommandResetReason.RoundStart);
            Assert.Equal(0, p.CooldownEnd);
        }

        [Fact]
        public void StanceChange_OnlyOnValueChange_Resets()
        {
            CommandParser p = ParserRig.InStance(CharacterId.Char1);
            p.OnDirection(CmdDir.Forward, T0);
            p.OnStanceChanged(true, T0 + 1);                                  // 값이 같으면 아무 일 없음
            Assert.Equal(1, p.DirCount);
            p.OnStanceChanged(false, T0 + 2);
            Assert.Equal(0, p.DirCount);
            Assert.Equal(CommandTrie.Root, p.Cursor);
            Assert.False(p.InStance);
        }

        [Fact]
        public void Preview_DoesNotTouchWalletOrState()
        {
            CommandParser p = ParserRig.InStance(CharacterId.Char1);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            p.OnDirection(CmdDir.Forward, T0);
            ArtDecision at = p.Preview(T0 + 4608, w, in u).Normal;
            ArtDecision after = p.Preview(T0 + 4609, w, in u).Normal;
            Assert.Equal(ArtId.Fan, at.Art);
            Assert.Equal(ArtId.Burst, after.Art);
            Assert.Equal(5, w.Crystals);
            Assert.Equal(1, p.DirCount);
            Assert.Equal(0, p.CooldownEnd);
        }

        // 보관된 K 는 5단계(TryTakeBuffered)에서 나간다. 대기 끝과 같은 틱의 2단계에 대기 끝 뒤의 시각으로 들어온 K 는
        // 아직 보관 중이라 무시된다(U-04): 뒤에 누른 K 가 먼저 나가고 보관된 K 가 한 대기만큼 밀리면 안 된다
        [Fact]
        public void KAfterCooldownEnd_WhileBufferedNotYetTaken_IsIgnored()
        {
            CommandParser p = ParserRig.InStance(CharacterId.Char1);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            UnlockSet u = Unlocks.All.For(CharacterId.Char1);
            long t0 = 400 * 256;                                               // 틱 경계: 대기 끝도 틱 경계(424 × 256)
            Assert.True(p.OnSkillPress(t0, w, in u, out _));
            long end = t0 + SpecArts.ArtCooldown;
            Assert.False(p.OnSkillPress(end - 100, w, in u, out _));          // 보관(마력 방출, 결정 4 → 3)
            Assert.True(p.HasBuffered);
            Assert.Equal(3, w.Crystals);

            p.OnDirection(CmdDir.Forward, end + 5);                            // 대기 끝 틱의 2단계: 새 커맨드는 쌓인다
            Assert.False(p.OnSkillPress(end + 10, w, in u, out _));           // 같은 틱의 K: 보관 중이라 무시
            Assert.Equal(3, w.Crystals);
            Assert.Equal("F", ParserRig.Letters(p.Trie.Path(p.Cursor)));      // 무시된 K 는 커서·기록을 건드리지 않는다
            Assert.Equal(end, p.CooldownEnd);

            Assert.True(p.TryTakeBuffered(end, out ArtDecision held));        // 같은 틱의 5단계
            Assert.Equal(ArtId.Burst, held.Art);
            Assert.Equal(end - 100, held.JudgeTime);
            Assert.Equal(end + SpecArts.ArtCooldown, p.CooldownEnd);
            Assert.False(p.OnSkillPress(end + 20, w, in u, out _));           // 새 대기의 보관 창 밖
            Assert.False(p.HasBuffered);
        }

        // 웨이브·라운드 시작은 대기를 풀지만 결제가 끝난 보관 결과는 버리지 않는다. 그 틱의 5단계에 나간다 [임시, Q-11]
        [Fact]
        public void WaveStart_KeepsBufferedDecision_ReleasedOnNextTake()
        {
            var (p, w, u) = OutOfStance(5);
            Assert.True(p.OnSkillPress(T0, w, in u, out _));
            Assert.False(p.OnSkillPress(T0 + 5000, w, in u, out _));
            Assert.True(p.HasBuffered);
            p.Reset(CommandResetReason.WaveStart);
            Assert.Equal(0, p.CooldownEnd);
            Assert.True(p.HasBuffered);
            Assert.True(p.TryTakeBuffered(T0 + 5120, out ArtDecision held));
            Assert.Equal(T0 + 5000, held.JudgeTime);
            Assert.Equal(3, w.Crystals);
        }
    }
}
