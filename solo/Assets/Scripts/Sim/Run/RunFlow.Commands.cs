using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    // 0단계: 화면이 넣은 RunCommand를 적용한다. 받을 수 없는 상태의 명령은 조용히 버린다(예외 없음).
    //
    // 명령의 인자(RunTypes.cs의 주석에 없는 것은 여기가 정한다):
    //   LootPick / LootUnpick   A = 카드 번호(0부터)
    //   LootReroll, LootConfirm 인자 없음
    //   LootDiscard             A = 버릴 보관함 자리(SlotRef 100 + i, 또는 i 그대로). LootConfirm 뒤 Loot.OverflowToDiscard &gt; 0일 때만
    //   CampOpen / CampClose / CampDepart  인자 없음
    //   CampHeal                Slot = 회복할 플레이어
    //   LoadoutMove             Slot = 플레이어, A = 원본 자리, B = 대상 자리(SlotRef)
    //   LoadoutRemoveAll        Slot = 플레이어
    //   ArtPick                 Slot = 플레이어, Id = 고른 비기 id(지금 차례의 후보 중 하나)
    //   PracticeSetOption       A = PracticeOption, B = 1 켬 / 0 끔
    //   TutorialAdvance         A = 넘어갈 단계(마지막 단계 + 1 = 끝)
    public sealed partial class RunFlow
    {
        const int CoreRef = 0;
        const int StorageBase = 100;
        const int BenchBase = 300;
        const int NoneRef = -1;

        public override void Stage0_Commands(SimWorld w)
        {
            if (w.Commands.Count == 0) return;
            var s = Scratch(w);
            // 적용 중에 쌓인 명령은 없다(화면은 틱 밖에서 넣는다). 차례대로 적용하고 비운다.
            for (int i = 0; i < w.Commands.Count; i++) Apply(w, s, w.Commands[i]);
            w.Commands.Clear();
        }

        void Apply(SimWorld w, RunScratch s, in RunCommand cmd)
        {
            var run = w.Run;
            if (run.Phase == RunPhase.NotStarted || run.Phase == RunPhase.RunEnded) return;

            switch (cmd.Kind)
            {
                case RunCommandKind.LootPick:
                    if (run.Phase == RunPhase.LootPick) CmdLootPick(w, cmd.A, true);
                    break;
                case RunCommandKind.LootUnpick:
                    if (run.Phase == RunPhase.LootPick) CmdLootPick(w, cmd.A, false);
                    break;
                case RunCommandKind.LootReroll:
                    if (run.Phase == RunPhase.LootPick) CmdLootReroll(w);
                    break;
                case RunCommandKind.LootDiscard:
                    if (run.Phase == RunPhase.LootPick) CmdLootDiscard(w, s, cmd.A);
                    break;
                case RunCommandKind.LootConfirm:
                    if (run.Phase == RunPhase.LootPick) CmdLootConfirm(w, s);
                    break;

                case RunCommandKind.CampOpen:
                    if (w.InCamp) OpenCampScreen(w);
                    break;
                case RunCommandKind.CampClose:
                    if (w.InCamp && run.CampSub == CampSubState.CampScreen)
                    {
                        run.CampSub = CampSubState.FireIdle;
                        w.Emit(SimEventType.CampChanged);
                    }
                    break;
                case RunCommandKind.CampHeal:
                    if (w.InCamp) CmdCampHeal(w, cmd.Slot);
                    break;
                case RunCommandKind.CampDepart:
                    // 남은 해금 차례가 있으면 거절한다(야영 화면이 고르는 판을 먼저 띄운다).
                    if (w.InCamp && !TryDepart(w, s)) w.Emit(SimEventType.CampChanged);
                    break;

                case RunCommandKind.LoadoutMove:
                    if (w.InCamp || run.Phase == RunPhase.Practice) CmdLoadoutMove(w, s, cmd.Slot, cmd.A, cmd.B);
                    break;
                case RunCommandKind.LoadoutRemoveAll:
                    if (w.InCamp || run.Phase == RunPhase.Practice) CmdLoadoutRemoveAll(w, s, cmd.Slot);
                    break;

                case RunCommandKind.ArtPick:
                    if (w.InCamp) CmdArtPick(w, cmd.Slot, cmd.Id);
                    break;

                case RunCommandKind.GiveUp:
                    // 되비침·쓰러짐(입력 잠금) 중에는 포기를 받지 않는다.
                    if (run.Phase == RunPhase.PhantomFinale || run.Phase == RunPhase.PhantomDowned || AnyInputLocked(w)) break;
                    run.GaveUp = true;
                    EndRun(w, s, RunEndReason.Abandon);
                    break;

                case RunCommandKind.PracticeReset:
                    if (run.Phase == RunPhase.Practice) CmdPracticeReset(w, s);
                    break;
                case RunCommandKind.PracticeSetOption:
                    if (run.Phase == RunPhase.Practice) CmdPracticeSetOption(w, cmd.A, cmd.B != 0);
                    break;
                case RunCommandKind.TutorialAdvance:
                    if (run.Phase == RunPhase.Practice) CmdTutorialAdvance(w, s, cmd.A);
                    break;
            }
        }

        static PlayerState PlayerOf(SimWorld w, int slot)
        {
            if (slot < 0 || slot >= w.Players.Length) return null;
            var p = w.Players[slot];
            return p.Active ? p : null;
        }

        void CmdCampHeal(SimWorld w, int slot)
        {
            var run = w.Run;
            var p = PlayerOf(w, slot);
            if (p == null || p.Hp >= p.MaxHp) return;
            // 원정 지갑에서만 낸다. 모자라면 못 한다.
            int cost = HealCost(w);
            if (run.Purse < cost) return;
            run.Purse -= cost;
            run.HealsThisCamp++;
            w.Emit(SimEventType.PurseChanged, a: run.Purse);
            w.Mods.Player.Heal(w, p, w.Content.Camp.HealCells);
            w.Emit(SimEventType.CampChanged);
        }

        void CmdArtPick(SimWorld w, int slot, string artId)
        {
            var run = w.Run;
            var offer = run.CurrentArtOffer;
            var p = PlayerOf(w, slot);
            if (offer == null || p == null || artId == null || !offer.Candidates.Contains(artId)) return;
            if (!w.Mods.Arts.Unlock(w, p, artId)) return;
            if (!run.NewArtsThisRun.Contains(artId)) run.NewArtsThisRun.Add(artId);
            if (run.PendingArtTurns.Count > 0) run.PendingArtTurns.Dequeue();
            run.CurrentArtOffer = null;
            // 같은 야영에 차례가 여럿이면 다음 차례(앞에서 고른 것이 뒤 차례의 선행을 채울 수 있다).
            AdvanceArtOffer(w);
            w.Emit(SimEventType.CampChanged);
        }

        // ------------------------------------------------------------ 조립 (D-53)

        static bool IsWandRef(RunLoadout lo, int r) => r >= CoreRef && r <= lo.Links.Length;
        static bool IsStorageRef(int r) => r >= StorageBase && r < BenchBase;
        static bool IsBenchRef(int r) => r >= BenchBase;

        static SlotItem GetSlot(RunLoadout lo, int r) => r == CoreRef ? lo.Core : lo.Links[r - 1];

        static void SetSlot(RunLoadout lo, int r, SlotItem item)
        {
            if (r == CoreRef) lo.Core = item; else lo.Links[r - 1] = item;
        }

        /// <summary>그 블록을 그 자리에 둘 수 있나: 코어에는 투사체만.</summary>
        static bool Fits(IContentDb db, int r, string blockId)
        {
            if (r != CoreRef || string.IsNullOrEmpty(blockId)) return true;
            BlockDef def = db.Block(blockId);
            return def != null && def.IsProjectile;
        }

        WildBlock WildOf(SimWorld w, RunScratch s, SlotItem item)
        {
            WildBlock wb;
            if (s.WildByUid.TryGetValue(item.WildUid, out wb)) return wb;
            BlockDef def = w.Content.Block(item.BlockId);
            wb = new WildBlock { Uid = item.WildUid, BlockId = item.BlockId, Rarity = def != null ? def.Rarity : null, Borrowed = item.Borrowed, AcquiredWave = w.Run.LastClearedWave };
            s.WildByUid[wb.Uid] = wb;
            return wb;
        }

        /// <summary>칸에서 뺀 서고 블록을 대기 칸에 올릴 기록으로 바꾼다(새 uid, FromLibrary).</summary>
        static WildBlock BenchOf(SimWorld w, SlotItem item)
        {
            BlockDef def = w.Content.Block(item.BlockId);
            return new WildBlock { Uid = w.Run.NextWildUid++, BlockId = item.BlockId, Rarity = def != null ? def.Rarity : null, FromLibrary = true };
        }

        bool StorageHasRoom(SimWorld w) => w.Run.Storage.Count < w.Content.Drops.StorageCap;

        void CmdLoadoutMove(SimWorld w, RunScratch s, int slot, int a, int b)
        {
            var p = PlayerOf(w, slot);
            if (p == null) return;
            if (!MoveBlock(w, s, p, a, b)) return;
            w.Mods.Player.RecompileSpell(w, p);
        }

        bool MoveBlock(SimWorld w, RunScratch s, PlayerState p, int a, int b)
        {
            var run = w.Run;
            var lo = p.Loadout;
            if (a == b || a == NoneRef) return false;

            if (IsWandRef(lo, a))
            {
                if (IsWandRef(lo, b)) return SwapSlots(w, lo, a, b);
                if (b == NoneRef) return RemoveToHome(w, s, lo, a);
                // 칸 → 보관함·대기 칸: 그 자리에 블록이 있으면 서로 바꾸고, 없으면 빼기와 같다.
                if (IsStorageRef(b))
                    return b - StorageBase < run.Storage.Count ? InsertFromStorage(w, s, lo, b - StorageBase, a) : RemoveToHome(w, s, lo, a);
                if (IsBenchRef(b))
                    return b - BenchBase < run.LibraryBench.Count ? InsertFromBench(w, s, lo, b - BenchBase, a) : RemoveToHome(w, s, lo, a);
                return false;
            }
            if (IsStorageRef(a))
            {
                int i = a - StorageBase;
                if (i >= run.Storage.Count) return false;
                if (IsWandRef(lo, b)) return InsertFromStorage(w, s, lo, i, b);
                if (IsStorageRef(b) && b - StorageBase < run.Storage.Count)
                {
                    // 보관함 안 자리 바꾸기
                    var tmp = run.Storage[i];
                    run.Storage[i] = run.Storage[b - StorageBase];
                    run.Storage[b - StorageBase] = tmp;
                    return true;
                }
                return false;
            }
            if (IsBenchRef(a))
            {
                int i = a - BenchBase;
                if (i >= run.LibraryBench.Count) return false;
                if (IsWandRef(lo, b)) return InsertFromBench(w, s, lo, i, b);
                if (IsBenchRef(b) && b - BenchBase < run.LibraryBench.Count)
                {
                    var tmp = run.LibraryBench[i];
                    run.LibraryBench[i] = run.LibraryBench[b - BenchBase];
                    run.LibraryBench[b - BenchBase] = tmp;
                    return true;
                }
                return false;
            }
            return false;
        }

        bool SwapSlots(SimWorld w, RunLoadout lo, int a, int b)
        {
            SlotItem ia = GetSlot(lo, a), ib = GetSlot(lo, b);
            if (ia.IsEmpty && ib.IsEmpty) return false;
            if (!Fits(w.Content, b, ia.BlockId) || !Fits(w.Content, a, ib.BlockId)) return false;
            SetSlot(lo, a, ib);
            SetSlot(lo, b, ia);
            return true;
        }

        /// <summary>보관함의 야생 블록을 칸에 끼운다(uid 유지). 칸에 있던 것은 야생이면 그 보관함 자리로, 서고 블록이면 대기 칸으로.</summary>
        bool InsertFromStorage(SimWorld w, RunScratch s, RunLoadout lo, int index, int slotRef)
        {
            var run = w.Run;
            WildBlock wb = run.Storage[index];
            if (!Fits(w.Content, slotRef, wb.BlockId)) return false;
            SlotItem old = GetSlot(lo, slotRef);
            s.WildByUid[wb.Uid] = wb;
            SetSlot(lo, slotRef, new SlotItem { BlockId = wb.BlockId, WildUid = wb.Uid, Borrowed = wb.Borrowed });
            if (old.IsEmpty)
            {
                run.Storage.RemoveAt(index);
                s.TutInserted = true;
            }
            else if (old.WildUid != 0) run.Storage[index] = WildOf(w, s, old);
            else
            {
                run.Storage.RemoveAt(index);
                run.LibraryBench.Add(BenchOf(w, old));
            }
            return true;
        }

        /// <summary>대기 칸의 서고 블록을 다시 끼운다(WildUid 0으로 돌아간다).</summary>
        bool InsertFromBench(SimWorld w, RunScratch s, RunLoadout lo, int index, int slotRef)
        {
            var run = w.Run;
            WildBlock lb = run.LibraryBench[index];
            if (!Fits(w.Content, slotRef, lb.BlockId)) return false;
            SlotItem old = GetSlot(lo, slotRef);
            if (!old.IsEmpty && old.WildUid != 0 && !StorageHasRoom(w)) return false;
            SetSlot(lo, slotRef, new SlotItem { BlockId = lb.BlockId });
            if (old.IsEmpty)
            {
                run.LibraryBench.RemoveAt(index);
                s.TutInserted = true;
            }
            else if (old.WildUid == 0) run.LibraryBench[index] = BenchOf(w, old);
            else
            {
                run.LibraryBench.RemoveAt(index);
                run.Storage.Add(WildOf(w, s, old));
            }
            return true;
        }

        /// <summary>칸에서 뺀다: 야생 블록은 보관함으로(자리가 없으면 못 뺀다), 출발 조립의 서고 블록은 대기 칸으로.</summary>
        bool RemoveToHome(SimWorld w, RunScratch s, RunLoadout lo, int slotRef)
        {
            var run = w.Run;
            SlotItem old = GetSlot(lo, slotRef);
            if (old.IsEmpty) return false;
            if (old.WildUid != 0)
            {
                if (!StorageHasRoom(w)) return false;
                run.Storage.Add(WildOf(w, s, old));
            }
            else run.LibraryBench.Add(BenchOf(w, old));
            SetSlot(lo, slotRef, default(SlotItem));
            return true;
        }

        void CmdLoadoutRemoveAll(SimWorld w, RunScratch s, int slot)
        {
            var p = PlayerOf(w, slot);
            if (p == null) return;
            var lo = p.Loadout;
            bool changed = false;
            for (int r = CoreRef; r <= lo.Links.Length; r++) changed |= RemoveToHome(w, s, lo, r);
            if (changed) w.Mods.Player.RecompileSpell(w, p);
        }
    }
}
