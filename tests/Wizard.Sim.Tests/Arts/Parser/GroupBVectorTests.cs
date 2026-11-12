using Wizard.Content;
using Wizard.Sim.Arts;
using Wizard.Sim.Input;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // spec 03 18.2 B군 중 이 작업의 것(B01~B07, B11, B16, B38). B01~B07 은 자세·이벤트 순서 규칙이라 논리 키 이벤트로
    // Player 모듈의 입력 처리를 거쳐 판정기에 넣는다(KeyRig). 시간: 0.15초 = 2304u, 0.1초 = 1536u, 0.05초 = 768u(round(초 × 15,360))
    public sealed class GroupBVectorTests
    {
        private const long T0 = KeyRig.T0;
        private const long S015 = 2304, S010 = 1536, S005 = 768;
        private const LogicalKey Fwd = KeyRig.Fwd;

        private static void AssertFire(in ArtDecision v, ArtId art)
        {
            Assert.Equal(ArtOutcome.Fire, v.Outcome);
            Assert.Equal(art, v.Art);
        }

        // 커서 F(시각 t0) 뒤 t0+2304u 의 같은 틱·같은 오프셋에 F 누름과 K 누름. 기록 순번은 K 가 먼저다 → 정렬은 F → K.
        // 커서 FF, K 간격 0 ≤ 1,075 → 뇌광창, 결정 4
        [Fact]
        public void S03_B01__SameOffset_DirectionBeforeK_ThunderSpear()
        {
            var rig = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            long t = T0 + S015;
            rig.Press(T0 - S010, LogicalKey.Stance)
               .Press(T0, Fwd)
               .Release(T0 + S005, Fwd)
               .PressWithSeq(t, LogicalKey.Skill, 10)
               .PressWithSeq(t, Fwd, 11);
            ArtDecision v = rig.Single();
            AssertFire(v, ArtId.ThunderSpear);
            Assert.True(v.IsJust);
            Assert.Equal(4, rig.Wallet.Crystals);
        }

        // 커서 루트에서 같은 틱·같은 오프셋에 F 와 K → F 먼저 → 부채 전개, 결정 4(K 가 먼저였다면 마력 방출)
        [Fact]
        public void S03_B01b__SameOffsetFromRoot_FanSpread()
        {
            var rig = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            rig.Press(T0 - S010, LogicalKey.Stance)
               .PressWithSeq(T0, LogicalKey.Skill, 10)
               .PressWithSeq(T0, Fwd, 11);
            ArtDecision v = rig.Single();
            AssertFire(v, ArtId.Fan);
            Assert.Equal(4, rig.Wallet.Crystals);

            // 비교: K 가 F 보다 한 오프셋 먼저면 루트의 마력 방출
            var early = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            early.Press(T0 - S010, LogicalKey.Stance).Press(T0, LogicalKey.Skill).Press(T0 + 1, Fwd);
            AssertFire(early.Single(), ArtId.Burst);
        }

        // 자세 밖에서 W(= 시야 앞)를 누른 채 Shift(자세 진입, W 는 계속 누름) → 0.1초 뒤 K.
        // 누른 채인 W 는 입력이 아니다 → 마력 방출, 결정 4
        [Fact]
        public void S03_B02__HeldKeyIsNotInput_ManaRelease()
        {
            var rig = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            rig.Press(T0 - S010, LogicalKey.Up)                       // 시야 위
               .Press(T0, LogicalKey.Stance)
               .Press(T0 + S010, LogicalKey.Skill);
            ArtDecision v = rig.Single();
            AssertFire(v, ArtId.Burst);
            Assert.Equal(4, rig.Wallet.Crystals);
        }

        // 자세 중 F, F+0.15s, Shift 뗌(+0.05s), Shift 누름(+0.05s), K(+0.05s) → 해제·진입 때 초기화 → 마력 방출
        [Fact]
        public void S03_B03__StanceExitAndEnter_ResetCommand()
        {
            var rig = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            long f2 = T0 + S015;
            rig.Press(T0 - S010, LogicalKey.Stance)
               .Press(T0, Fwd).Release(T0 + S005, Fwd)
               .Press(f2, Fwd)
               .Release(f2 + S005, LogicalKey.Stance)
               .Press(f2 + 2 * S005, LogicalKey.Stance)
               .Press(f2 + 3 * S005, LogicalKey.Skill);
            AssertFire(rig.Single(), ArtId.Burst);
        }

        // 자세 중 F, F+0.15s, Shift 뗌(+0.05s), K(+0.05s) → 자세 밖 K → 마력 방출
        [Fact]
        public void S03_B04__KOutsideStance_ManaRelease()
        {
            var rig = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            long f2 = T0 + S015;
            rig.Press(T0 - S010, LogicalKey.Stance)
               .Press(T0, Fwd).Release(T0 + S005, Fwd)
               .Press(f2, Fwd)
               .Release(f2 + S005, LogicalKey.Stance)
               .Press(f2 + 2 * S005, LogicalKey.Skill);
            AssertFire(rig.Single(), ArtId.Burst);
            Assert.False(rig.Parser.InStance);
        }

        // 자세 밖에서 이동 키 F → Shift 누름(+0.05s) → K(+0.05s) → 자세 밖 방향은 기록되지 않는다 → 마력 방출
        [Fact]
        public void S03_B05__DirectionOutsideStanceNotRecorded_ManaRelease()
        {
            var rig = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            rig.Press(T0, Fwd)
               .Press(T0 + S005, LogicalKey.Stance)
               .Press(T0 + 2 * S005, LogicalKey.Skill);
            AssertFire(rig.Single(), ArtId.Burst);

            // 판정기에 바로 넣어도 같다(자세 밖 방향은 무시, R-04)
            var p = new CommandParser(SpecArts.Trie(CharacterId.Char1));
            p.OnDirection(CmdDir.Forward, T0);
            Assert.Equal(CommandTrie.Root, p.Cursor);
            Assert.Equal(0, p.DirCount);
        }

        // 자세 중 F, Space 누름(+0.1s, 반전), F'(+0.1s, 뒤집힌 시야 기준 앞), K(+0.1s) → 반전 때 초기화 → 커서 F → 부채 전개
        [Fact]
        public void S03_B06__ReverseResets_ThenForwardOfFlippedFacing_FanSpread()
        {
            var rig = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            rig.Press(T0 - S010, LogicalKey.Stance)
               .Press(T0, Fwd)
               .Press(T0 + S010, LogicalKey.Reverse)                  // 잠긴 시야 E → W
               .Press(T0 + 2 * S010, LogicalKey.Left)                 // 뒤집힌 시야(W) 기준 앞
               .Press(T0 + 3 * S010, LogicalKey.Skill);
            AssertFire(rig.Single(), ArtId.Fan);
        }

        // 자세 중 F, F+0.15s, 일시정지·해제, K(해제 직후) → 초기화 → 마력 방출
        [Fact]
        public void S03_B07__PauseResets_ManaRelease()
        {
            var rig = new KeyRig(CharacterId.Char1, 5, Unlocks.All);
            long f2 = T0 + S015;
            rig.Press(T0 - S010, LogicalKey.Stance)
               .Press(T0, Fwd).Release(T0 + S005, Fwd)
               .Press(f2, Fwd)
               .Press(f2 + S005, LogicalKey.Pause)
               .Release(f2 + 2 * S005, LogicalKey.Pause)
               .Press(f2 + 2 * S005 + 1, LogicalKey.Skill);
            AssertFire(rig.Single(), ArtId.Burst);
        }

        // 방향 8개 L, R 교대 뒤 F, F, K(모두 0.15s) → 꿰뚫는 창(= A117). 기록은 최근 8개만 남는다
        [Fact]
        public void S03_B11__HistoryKeepsLastEight_PiercingLance()
        {
            CommandParser p = ParserRig.InStance(CharacterId.Char1);
            TestWallet w = SpecArts.Wallet(CharacterId.Char1, 5, 0);
            long t = ParserRig.T0;
            for (int i = 0; i < 8; i++)
            {
                p.OnDirection(i % 2 == 0 ? CmdDir.Left : CmdDir.Right, t);
                t += S015;
            }
            p.OnDirection(CmdDir.Forward, t);
            p.OnDirection(CmdDir.Forward, t + S015);
            Assert.Equal(CommandTrie.DirHistorySize, p.DirCount);
            Assert.Equal(t + S015, p.RecentDirTime(0));
            Assert.Equal(t - 6 * S015, p.RecentDirTime(7));            // 열 개 중 앞의 둘은 버려졌다
            Assert.True(p.OnSkillPress(t + 2 * S015, w, Unlocks.All.For(CharacterId.Char1), out ArtDecision v));
            AssertFire(v, ArtId.Spear);
            Assert.Equal(3, w.Crystals);
        }

        // 늦음 표시: 소수 둘째 자리 올림(spec 03 7.5 [임시]). 153u / 307u / 1,075u / 1u → 0.01 / 0.02 / 0.07 / 0.01초
        [Fact]
        public void S03_B16__LateDisplayRoundsUpToHundredths()
        {
            Assert.Equal(1, LateBy.HundredthsCeil(153));
            Assert.Equal(2, LateBy.HundredthsCeil(307));
            Assert.Equal(7, LateBy.HundredthsCeil(1075));
            Assert.Equal(1, LateBy.HundredthsCeil(1));
        }

        // F, F+0.15s, K+0.09s → 꿰뚫는 창. 연습장 문구 "0.02초 늦음"(1382 − 1075 = 307u)
        [Fact]
        public void S03_B38__LateJustFallsToSpear_ReportsOverrun()
        {
            ArtDecision v = ParserRig.RunA(CharacterId.Char1, Unlocks.All, 5, 0, new[] { In.F(0), In.F(2304), In.K(1382) },
                                          Want.Fire(ArtId.Spear), 3, 0);
            Assert.False(v.IsJust);
            Assert.Equal(307, v.JustLateByUnits);
            Assert.Equal(2, LateBy.HundredthsCeil(v.JustLateByUnits));
        }
    }
}
