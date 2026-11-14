using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Player;

namespace Wizard.Solo.Tests.Player
{
    /// <summary>판 시작 상태, 입력 적용, 시야, 대각 유지 창, 자세 상태 기계. 02 요약서 3·4절.</summary>
    public class PlayerInputTests
    {
        // 0.08초 = 1,229u (02 요약서 4.3절).
        const int HoldU = 1229;

        // ------------------------------------------------------------ 판 시작

        // 5.1절: 최대 체력 8/4/6/7/5칸(기준 6), 1칸 = 100점. 판 시작: 체력 가득, 열 0, 잠금 없음, 콤보 0, 결정 0, 빚 0.
        [TestCase("char.1", 8)]
        [TestCase("char.2", 4)]
        [TestCase("char.3", 6)]
        [TestCase("char.4", 7)]
        [TestCase("char.5", 5)]
        [TestCase(null, 6)]
        public void S01_3_7__SetupState(string charId, int cells)
        {
            var k = Kit.Create(charId);
            var p = k.P;
            Assert.IsTrue(p.Active);
            Assert.AreEqual(cells * 100, p.MaxHp);
            Assert.AreEqual(p.MaxHp, p.Hp);
            Assert.AreEqual(cells, p.MaxHpCells);
            Assert.AreEqual(0, p.HeatHu);
            Assert.IsFalse(p.Locked);
            Assert.AreEqual(0, p.Combo);
            Assert.AreEqual(0, p.Crystals);
            Assert.AreEqual(0, p.Debt);
            // 4.2절: 시야 아래(S), 위치 (12.0, 7.0).
            Assert.AreEqual(Dir8.S, p.Facing);
            Assert.AreEqual(new Vector2(12f, 7f), p.Pos);
            Assert.AreEqual(StanceState.None, p.Stance);
            Assert.IsFalse(p.Downed);
        }

        [Test]
        public void Setup_FillsWandLoadoutAndCompilesThroughTraitsContext()
        {
            var k = Kit.Create("char.3");
            // 다시 세운다: 단계·변이·칸 수·조립을 설정에서 옮기는지.
            var cfg = new RunConfig
            {
                CharId = "char.3", WandId = 77, WandStage = 5, LinkSlotCount = 6,
                Mutations = new System.Collections.Generic.List<string> { Ids.Mutation.All[0] },
                Loadout = new LoadoutSpec
                {
                    Core = Ids.Block.MagicBolt,
                    Links = new System.Collections.Generic.List<string> { Ids.Block.Pierce, null, "", Ids.Block.Frost, null, null }
                }
            };
            int compilesBefore = k.Spells.CompileCalls, ctxBefore = k.Traits.CompileContextCalls, setupBefore = k.Arts.SetupCalls;
            k.Sys.SetupPlayer(k.W, k.P, cfg);
            Assert.AreEqual(77, k.P.Wand.WandId);
            Assert.AreEqual(5, k.P.Wand.Stage);
            Assert.AreEqual(6, k.P.Wand.LinkSlotCount);
            Assert.AreEqual(1, k.P.Wand.Mutations.Count);
            Assert.AreEqual(Ids.Block.MagicBolt, k.P.Loadout.Core.BlockId);
            Assert.AreEqual(0, k.P.Loadout.Core.WildUid);
            Assert.AreEqual(6, k.P.Loadout.Links.Length);
            Assert.AreEqual(Ids.Block.Pierce, k.P.Loadout.Links[0].BlockId);
            Assert.IsTrue(k.P.Loadout.Links[1].IsEmpty);
            Assert.IsTrue(k.P.Loadout.Links[2].IsEmpty);
            Assert.AreEqual(Ids.Block.Frost, k.P.Loadout.Links[3].BlockId);
            Assert.AreEqual(compilesBefore + 1, k.Spells.CompileCalls);
            Assert.AreEqual(ctxBefore + 1, k.Traits.CompileContextCalls, "컴파일 문맥은 Traits.CompileContext로만 만든다");
            Assert.AreEqual(6, k.Spells.LastContext.LinkSlotCount);
            Assert.AreEqual(setupBefore + 1, k.Arts.SetupCalls);
            Assert.IsNotNull(k.P.Spell);
        }

        [Test]
        public void RecompileSpell_EmitsLoadoutChanged()
        {
            var k = Kit.Create();
            k.Sys.RecompileSpell(k.W, k.P);
            Assert.AreEqual(1, k.Count(SimEventType.LoadoutChanged));
        }

        // ------------------------------------------------------------ 시야

        // 4.2절: 이동 입력이 있으면 시야 = 이동 방향, 없으면 그대로.
        [Test]
        public void S01_5_3__FacingFollowsMoveAndStays()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Left);
            k.Step();
            Assert.AreEqual(Dir8.W, k.P.Facing);
            k.Press(LogicalKey.Up);
            k.Step();
            Assert.AreEqual(Dir8.NW, k.P.Facing);
            Assert.AreEqual(Dir8.N, k.P.LastPressedCardinal);
            k.Release(LogicalKey.Left);
            k.Release(LogicalKey.Up, HoldU + 50);   // 창 밖에서 따로 뗌
            k.Step(10);
            // 창이 지난 뒤의 뗌: 시야는 그 직전 이동 방향(N)에서 멈춘다.
            Assert.AreEqual(Dir8.N, k.P.Facing);
            Assert.IsTrue(k.Count(SimEventType.FacingChanged) >= 3);
        }

        // T-F16: 창이 열린 동안(시야 NE, 이동 N) J 발사 → 발사 방향 NE.
        [Test]
        public void S01_T_F16__FireDuringDiagonalHoldGoesDiagonal()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Up);
            k.Press(LogicalKey.Right);
            k.Step(3);
            Assert.AreEqual(Dir8.NE, k.P.Facing);
            Vector2 before = k.P.Pos;
            // 한 키를 뗀다: 창이 열리고 이동은 남은 키(N)로 바로 바뀐다.
            k.Release(LogicalKey.Right, 0);
            k.Press(LogicalKey.Fire, 10);
            k.Step();
            Assert.AreEqual(Dir8.NE, k.P.Facing);
            Assert.IsTrue(k.P.DiagPendingActive);
            Assert.AreEqual(Dir8.N, k.P.MoveDir);
            Assert.AreEqual(before.x, k.P.Pos.x, 1e-5f);
            Assert.Less(k.P.Pos.y, before.y);
            Assert.AreEqual(1, k.Spells.Shots.Count);
            // 화면 각도(반시계 +): NE = 45도.
            Assert.AreEqual(45f, k.Spells.Shots[0].DirDegrees, 1e-4f);
        }

        // 4.3절: 정확히 0.08초(1,229u) 뒤의 두 번째 뗌은 유지(<=), 1u 더 늦으면 유지하지 않는다.
        [TestCase(HoldU, Dir8.NE)]
        [TestCase(HoldU + 1, Dir8.N)]
        [TestCase(100, Dir8.NE)]
        public void S01_5_4__SecondReleaseBoundary(int gapU, Dir8 expected)
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Up);
            k.Press(LogicalKey.Right);
            k.Step(2);
            long t0 = Kit.U(k.W.Tick, 7);
            k.ReleaseAt(LogicalKey.Right, t0);
            k.ReleaseAt(LogicalKey.Up, t0 + gapU);
            k.Step(8);
            Assert.AreEqual(expected, k.P.Facing);
            Assert.IsFalse(k.P.DiagPendingActive);
        }

        // 4.3절 ResolvePending: 나머지 키를 계속 누르고 있으면 창이 지난 뒤 시야 = 지금 이동 방향.
        [Test]
        public void S01_5_4__HoldExpiresToCurrentMoveDir()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Up);
            k.Press(LogicalKey.Right);
            k.Step(2);
            int t = k.W.Tick;
            k.Release(LogicalKey.Right, 0);          // 만료 시각 = t × 256 + 1,229 → 틱 t+5의 시작(1,280)에 지나 있다
            k.Step(5);                               // 틱 t .. t+4
            Assert.AreEqual(Dir8.NE, k.P.Facing, "창이 열린 동안은 대각");
            k.Step();                                // 틱 t+5
            Assert.AreEqual(t + 6, k.W.Tick);
            Assert.AreEqual(Dir8.N, k.P.Facing);
            Assert.IsFalse(k.P.DiagPendingActive);
        }

        // 4.3절: 방향키 누름은 창을 닫는다.
        [Test]
        public void S01_5_4__DirectionPressClosesHold()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Up);
            k.Press(LogicalKey.Right);
            k.Step(2);
            k.Release(LogicalKey.Right, 0);
            k.Press(LogicalKey.Left, 50);
            k.Step();
            Assert.IsFalse(k.P.DiagPendingActive);
            Assert.AreEqual(Dir8.NW, k.P.Facing);
        }

        // ------------------------------------------------------------ 입력 순서

        // 3.2절: 같은 시각이면 자세·반전 0 → 방향 1 → J 2 → K 3. 확정: 방향이 K보다 먼저.
        [Test]
        public void S01_4_3__SameTimeOrder_StanceThenDirectionThenK()
        {
            var k = Kit.Create();
            long t = Kit.U(0, 30);
            k.PressAt(LogicalKey.Skill, t);
            k.PressAt(LogicalKey.Down, t);
            k.PressAt(LogicalKey.Stance, t);
            k.Step();
            // 시야 S로 자세 진입 → S 누름 = 앞 → K.
            CollectionAssert.AreEqual(new[] { "reset:StanceEnter", "dir:Forward", "k" }, k.Arts.Log);
            Assert.AreEqual(t, k.Arts.DirTimes[0]);
            Assert.AreEqual(t, k.Arts.SkillTimes[0]);
        }

        [Test]
        public void S01_4_3__EarlierTimeFirstRegardlessOfClass()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Stance, 0);
            k.Step();
            k.Arts.Log.Clear();
            k.PressAt(LogicalKey.Down, Kit.U(1, 90));
            k.PressAt(LogicalKey.Skill, Kit.U(1, 40));
            k.Step();
            CollectionAssert.AreEqual(new[] { "k", "dir:Forward" }, k.Arts.Log);
        }

        // 3.2절: 눌린 키의 누름, 안 눌린 키의 뗌은 무시.
        [Test]
        public void S01_4_4__DuplicatePressAndReleaseIgnored()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Stance, 0);
            k.Press(LogicalKey.Up, 10);
            k.Press(LogicalKey.Up, 20);
            k.Release(LogicalKey.Left, 30);
            k.Press(LogicalKey.Skill, 40);
            k.Press(LogicalKey.Skill, 50);
            k.Step();
            Assert.AreEqual(1, k.Arts.Dirs.Count);
            Assert.AreEqual(1, k.Arts.SkillTimes.Count);
            Assert.AreEqual(Kit.U(0, 10), k.P.Keys.LastPressU[(int)LogicalKey.Up]);
        }

        // 3.2절: 이 틱의 것(TimeU < (t+1) × 256)만 처리한다.
        [Test]
        public void S01_4_3__FutureEventsWaitForTheirTick()
        {
            var k = Kit.Create();
            k.PressAt(LogicalKey.Left, Kit.U(2, 0));
            k.Step(2);
            Assert.IsFalse(k.P.Keys.IsDown(LogicalKey.Left));
            Assert.AreEqual(1, k.W.InputQueues[0].Count);
            k.Step();
            Assert.IsTrue(k.P.Keys.IsDown(LogicalKey.Left));
            Assert.AreEqual(0, k.W.InputQueues[0].Count);
        }

        // 3.2절: 한 틱 한 사람 최대 8개. 넘으면 버림.
        [Test]
        public void S01_4_4__MaxEightEventsPerTick()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Stance, 0);
            k.Step();
            k.Arts.Log.Clear();
            // 방향 누름·뗌 5쌍 = 10개 → 앞의 8개만: 누름 4번.
            for (int i = 0; i < 5; i++)
            {
                k.Press(LogicalKey.Up, 10 + i * 20);
                k.Release(LogicalKey.Up, 20 + i * 20);
            }
            k.Step();
            Assert.AreEqual(4, k.Arts.Dirs.Count);
            Assert.AreEqual(0, k.W.InputQueues[0].Count);
        }

        // 3.2절: JPressedThisTick은 그 틱에만 참. 같은 틱에 눌렀다 뗀 J도 발사 조건을 채운다.
        [Test]
        public void S01_4_4__JPressedThisTickAndTapFires()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Fire, 0);
            k.Release(LogicalKey.Fire, 100);
            k.Step();
            Assert.IsTrue(k.P.JPressedThisTick);
            Assert.IsFalse(k.P.Keys.IsDown(LogicalKey.Fire));
            Assert.AreEqual(1, k.Spells.Shots.Count);
            k.Step();
            Assert.IsFalse(k.P.JPressedThisTick);
            k.Step(20);
            Assert.AreEqual(1, k.Spells.Shots.Count, "뗀 뒤에는 더 나가지 않는다");
        }

        // 3.2절: 자세 밖의 방향은 커맨드가 아니다. 이미 누르고 있던 방향도 입력이 아니다.
        [Test]
        public void S01_4_4__DirectionOutsideStanceNotForwardedAndHeldIsNotInput()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Up);
            k.Step();
            Assert.AreEqual(0, k.Arts.Dirs.Count);
            k.Press(LogicalKey.Stance);
            k.Step(3);
            Assert.AreEqual(0, k.Arts.Dirs.Count, "누르고 있던 W는 판정기에 가지 않는다");
            k.Press(LogicalKey.Left);
            k.Step();
            Assert.AreEqual(1, k.Arts.Dirs.Count);
        }

        // ------------------------------------------------------------ 자세 상태 기계 (4.4절 표)

        [Test]
        public void S01_5_5__None_StancePress_EntersWithSnappedFacing()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.W;
            k.Press(LogicalKey.Stance);
            k.Step();
            Assert.AreEqual(StanceState.Stance, k.P.Stance);
            Assert.AreEqual(Dir8.W, k.P.LockedFacing);
            Assert.AreEqual(Dir8.W, k.P.Facing);
            Assert.AreEqual(FacingArrowMode.Stance, k.P.ArrowMode);
            CollectionAssert.AreEqual(new[] { CommandResetReason.StanceEnter }, k.Arts.Resets);
            Assert.AreEqual(1, k.Count(SimEventType.StanceEntered));
        }

        [Test]
        public void S01_5_5__None_ReversePress_EntersFlipped()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.W;
            k.Press(LogicalKey.Reverse);
            k.Step();
            Assert.AreEqual(StanceState.Reverse, k.P.Stance);
            Assert.AreEqual(Dir8.E, k.P.LockedFacing);
            Assert.AreEqual(Dir8.E, k.P.Facing);
            Assert.AreEqual(FacingArrowMode.Reverse, k.P.ArrowMode);
            CollectionAssert.AreEqual(new[] { CommandResetReason.StanceEnter }, k.Arts.Resets);
        }

        [Test]
        public void S01_5_5__Stance_ReversePress_FlipsAndResetsAsReverse()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.N;
            k.Press(LogicalKey.Stance);
            k.Step();
            k.Press(LogicalKey.Reverse);
            k.Step();
            Assert.AreEqual(StanceState.Reverse, k.P.Stance);
            Assert.AreEqual(Dir8.S, k.P.LockedFacing);
            Assert.AreEqual(Dir8.S, k.P.Facing);
            CollectionAssert.AreEqual(new[] { CommandResetReason.StanceEnter, CommandResetReason.Reverse }, k.Arts.Resets);
            Assert.AreEqual(1, k.Count(SimEventType.ReverseFlipped));
        }

        [Test]
        public void S01_5_5__Stance_StanceRelease_ExitsKeepingLockedFacingWhenNoMove()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.E;
            k.Press(LogicalKey.Stance);
            k.Step();
            k.Release(LogicalKey.Stance);
            k.Step();
            Assert.AreEqual(StanceState.None, k.P.Stance);
            Assert.AreEqual(Dir8.E, k.P.Facing);
            CollectionAssert.AreEqual(new[] { CommandResetReason.StanceEnter, CommandResetReason.StanceExit }, k.Arts.Resets);
            Assert.AreEqual(1, k.Count(SimEventType.StanceExited));
        }

        [Test]
        public void S01_5_5__Stance_StanceRelease_FacesHeldMoveKey()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.E;
            k.Press(LogicalKey.Stance);
            k.Step();
            k.Press(LogicalKey.Up);
            k.Step();
            Assert.AreEqual(Dir8.E, k.P.Facing, "자세 중 이동 입력은 시야를 바꾸지 않는다");
            k.Release(LogicalKey.Stance);
            k.Step();
            Assert.AreEqual(Dir8.N, k.P.Facing);
        }

        [Test]
        public void S01_5_5__Reverse_ReverseRelease_ExitsWithFlippedFacing()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.N;
            k.Press(LogicalKey.Reverse);
            k.Step();
            k.Release(LogicalKey.Reverse);
            k.Step();
            Assert.AreEqual(StanceState.None, k.P.Stance);
            Assert.AreEqual(Dir8.S, k.P.Facing, "뒤집힌 방향 그대로");
            CollectionAssert.AreEqual(new[] { CommandResetReason.StanceEnter, CommandResetReason.StanceExit }, k.Arts.Resets);
        }

        [Test]
        public void S01_5_5__Reverse_ReverseReleaseWithStanceHeld_BackToStanceKeepingFlip()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.N;
            k.Press(LogicalKey.Stance);
            k.Step();
            k.Press(LogicalKey.Reverse);
            k.Step();
            k.Release(LogicalKey.Reverse);
            k.Step();
            Assert.AreEqual(StanceState.Stance, k.P.Stance);
            Assert.AreEqual(Dir8.S, k.P.LockedFacing, "뒤집힌 채");
            CollectionAssert.AreEqual(
                new[] { CommandResetReason.StanceEnter, CommandResetReason.Reverse, CommandResetReason.Reverse }, k.Arts.Resets);
        }

        [Test]
        public void S01_5_5__Reverse_StanceKeyPressAndRelease_NoChangeNoReset()
        {
            var k = Kit.Create();
            k.P.Facing = Dir8.N;
            k.Press(LogicalKey.Reverse);
            k.Step();
            k.Press(LogicalKey.Stance);
            k.Step();
            k.Release(LogicalKey.Stance);
            k.Step();
            Assert.AreEqual(StanceState.Reverse, k.P.Stance);
            Assert.AreEqual(Dir8.S, k.P.LockedFacing);
            CollectionAssert.AreEqual(new[] { CommandResetReason.StanceEnter }, k.Arts.Resets);
        }

        // T-F19(일시정지의 초기화는 세션이 부른다. 여기서는 자세 진입·반전·해제마다 정확히 1회).
        [Test]
        public void S01_T_F19__CommandResetOncePerStanceTransition()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Stance);
            k.Step(5);
            Assert.AreEqual(1, k.Arts.Resets.Count);
            k.Press(LogicalKey.Reverse);
            k.Step(5);
            Assert.AreEqual(2, k.Arts.Resets.Count);
            k.Release(LogicalKey.Stance);
            k.Step(5);
            Assert.AreEqual(2, k.Arts.Resets.Count);
            k.Release(LogicalKey.Reverse);
            k.Step(5);
            Assert.AreEqual(3, k.Arts.Resets.Count);
            Assert.AreEqual(CommandResetReason.StanceExit, k.Arts.Resets[2]);
        }

        // 4.4절 SnapCardinal: 대각이면 두 축 성분 중 마지막으로 누른 축 쪽.
        [Test]
        public void S01_5_5__SnapCardinal()
        {
            var keys = new KeyState();
            Assert.AreEqual(Dir8.W, PlayerRules.SnapCardinal(Dir8.W, Dir8.N, keys));
            Assert.AreEqual(Dir8.N, PlayerRules.SnapCardinal(Dir8.NE, Dir8.N, keys));
            Assert.AreEqual(Dir8.E, PlayerRules.SnapCardinal(Dir8.NE, Dir8.E, keys));
            Assert.AreEqual(Dir8.S, PlayerRules.SnapCardinal(Dir8.SW, Dir8.S, keys));
            // 둘 다 아니면 마지막 누름 시각이 늦은 쪽.
            keys.LastPressU[(int)LogicalKey.Up] = 500;
            keys.LastPressU[(int)LogicalKey.Right] = 300;
            Assert.AreEqual(Dir8.N, PlayerRules.SnapCardinal(Dir8.NE, Dir8.S, keys));
            keys.LastPressU[(int)LogicalKey.Right] = 900;
            Assert.AreEqual(Dir8.E, PlayerRules.SnapCardinal(Dir8.NE, Dir8.S, keys));
        }

        [Test]
        public void S01_5_5__StanceFromDiagonalSnapsToLastPressedAxis()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Right, 0);
            k.Press(LogicalKey.Up, 50);
            k.Step();
            Assert.AreEqual(Dir8.NE, k.P.Facing);
            k.Press(LogicalKey.Stance);
            k.Step();
            Assert.AreEqual(Dir8.N, k.P.LockedFacing);
            Assert.AreEqual(Dir8.N, k.P.Facing);
        }

        // 4.4절 상대 방향 표: 잠긴 시야 × W/A/S/D.
        [TestCase(Dir8.N, CmdDir.Forward, CmdDir.Left, CmdDir.Back, CmdDir.Right)]
        [TestCase(Dir8.W, CmdDir.Right, CmdDir.Forward, CmdDir.Left, CmdDir.Back)]
        [TestCase(Dir8.S, CmdDir.Back, CmdDir.Right, CmdDir.Forward, CmdDir.Left)]
        [TestCase(Dir8.E, CmdDir.Left, CmdDir.Back, CmdDir.Right, CmdDir.Forward)]
        public void S01_5_5__RelativeDirectionTable(Dir8 locked, CmdDir w, CmdDir a, CmdDir s, CmdDir d)
        {
            var k = Kit.Create();
            k.P.Facing = locked;
            k.Press(LogicalKey.Stance);
            k.Step();
            var keys = new[] { LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right };
            foreach (var key in keys)
            {
                k.Press(key, 0);
                k.Release(key, 100);
                k.Step();
            }
            CollectionAssert.AreEqual(new[] { w, a, s, d }, k.Arts.Dirs);
            CollectionAssert.AreEqual(new[] { Dir8.N, Dir8.W, Dir8.S, Dir8.E }, k.Arts.Physical);
            Assert.AreEqual(locked, k.P.Facing, "자세 중 시야는 잠겨 있다");
        }

        // ------------------------------------------------------------ 잠금

        // 공통 규칙 7: Stage2는 InputLocked만 본다. AttackLocked여도 방향·K는 그대로 판정기에 간다.
        [Test]
        public void Arch_Rule7__AttackLockedStillForwardsDirectionAndK()
        {
            var k = Kit.Create();
            k.P.AttackLocked = true;
            k.Press(LogicalKey.Stance, 0);
            k.Press(LogicalKey.Up, 10);
            k.Press(LogicalKey.Skill, 20);
            k.Step();
            Assert.AreEqual(1, k.Arts.Dirs.Count);
            Assert.AreEqual(1, k.Arts.SkillTimes.Count);
        }

        [Test]
        public void Arch_Rule7__InputLockedIgnoresInputAndReleasesKeys()
        {
            var k = Kit.Create();
            k.Press(LogicalKey.Stance, 0);
            k.Press(LogicalKey.Right, 10);
            k.Step();
            Assert.IsTrue(k.P.InStance);
            k.P.InputLocked = true;
            Vector2 pos = k.P.Pos;
            k.Press(LogicalKey.Skill, 0);
            k.Press(LogicalKey.Up, 10);
            k.Press(LogicalKey.Fire, 20);
            k.Step(3);
            Assert.AreEqual(0, k.Arts.SkillTimes.Count);
            Assert.AreEqual(1, k.Arts.Dirs.Count, "잠기기 전의 D 하나뿐");
            Assert.AreEqual(pos, k.P.Pos);
            Assert.IsFalse(k.P.InStance);
            Assert.IsFalse(k.P.Keys.IsDown(LogicalKey.Right));
            Assert.IsFalse(k.P.JPressedThisTick);
            Assert.AreEqual(0, k.Spells.Shots.Count);
            Assert.AreEqual(CommandResetReason.StanceExit, k.Arts.Resets[k.Arts.Resets.Count - 1]);
            Assert.AreEqual(0, k.W.InputQueues[0].Count, "잠긴 동안의 입력은 버린다");
            // 풀린 뒤에는 새 누름부터 받는다.
            k.P.InputLocked = false;
            k.Press(LogicalKey.Left);
            k.Step();
            Assert.AreEqual(Dir8.W, k.P.Facing);
        }

        // 여러 플레이어: 슬롯마다 자기 큐만 읽는다.
        [Test]
        public void Arch_Players__SecondSlotHasOwnInput()
        {
            var k = Kit.Create();
            var cfg = new RunConfig { CharId = "char.2", LinkSlotCount = 4 };
            k.Sys.SetupPlayer(k.W, k.W.Players[1], cfg);
            var p1 = k.W.Players[1];
            p1.Pos = new Vector2(5f, 5f);
            k.W.EnqueueInput(1, new InputEvent(LogicalKey.Right, true, 0));
            k.Step(60);
            Assert.AreEqual(new Vector2(12f, 7f), k.P.Pos);
            Assert.AreEqual(5f + 5.6f, p1.Pos.x, 0.002f);
        }
    }
}
