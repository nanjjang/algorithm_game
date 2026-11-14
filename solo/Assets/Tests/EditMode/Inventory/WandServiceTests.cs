using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Inventory;

namespace Wizard.Solo.Tests.Inventory
{
    /// <summary>지팡이 개체·목록·든 지팡이·조립·서고·감정가·떠나보내기. 기대값은 07 요약서 3·7.4·12.10·12.13절, 08 요약서 5.3·10.2절, 03 요약서 3절, 01 요약서 3.2절.</summary>
    public class WandServiceTests
    {
        InvFixture f;

        [SetUp]
        public void SetUp() { f = new InvFixture(); }

        // ------------------------------------------------------------ 12.13 TV-PF-01: 새 프로필의 지팡이·서고

        [Test]
        public void S07_TV_PF_01__NewProfileFiveWandsFiveBoltsSocketed()
        {
            var all = f.Wands.List(0, WandSort.StageDesc);
            Assert.AreEqual(5, all.Count);
            for (int c = 1; c <= 5; c++)
            {
                var mine = f.Wands.List(c, WandSort.StageDesc);
                Assert.AreEqual(1, mine.Count);
                Assert.IsTrue(mine[0].Equipped);
                Assert.IsFalse(mine[0].CoreEmpty);
                Assert.AreEqual(0, mine[0].Wand.Stage);
            }
            // 마력탄 5개 전부 끼움 → 여분 0, 서고 개수(끼운 것 제외) 0, 한도 100
            Assert.AreEqual(0, f.Wands.LibraryFree(Ids.Block.MagicBolt));
            Assert.AreEqual(0, f.Wands.LibraryTotal());
            Assert.AreEqual(100, f.Wands.LibraryCap());
            Assert.AreEqual(0, f.P.Currencies.Gold);
        }

        // ------------------------------------------------------------ 08 5.3절 TV-029·030: 든 지팡이 맨 위 고정

        [Test]
        public void S13_TV_029__StageDescHeldFirst()
        {
            var held = f.Starter(1); held.Stage = 5;
            f.AddWand(1, 9); f.AddWand(1, 7);
            var list = f.Wands.List(1, WandSort.StageDesc);
            CollectionAssert.AreEqual(new[] { 5, 9, 7 }, Stages(list));
            Assert.IsTrue(list[0].Equipped);
        }

        [Test]
        public void S13_TV_030__StageAscHeldFirst()
        {
            var held = f.Starter(1); held.Stage = 5;
            f.AddWand(1, 9); f.AddWand(1, 7);
            CollectionAssert.AreEqual(new[] { 5, 7, 9 }, Stages(f.Wands.List(1, WandSort.StageAsc)));
        }

        [Test]
        public void S08_WandList__RecentSortsByAcquiredAtDesc()
        {
            var a = f.AddWand(1, 1, "2026-03-01T00:00:00+09:00");
            var b = f.AddWand(1, 2, "2026-05-01T00:00:00+09:00");
            var c = f.AddWand(1, 3, "2026-04-01T00:00:00+09:00");
            var list = f.Wands.List(1, WandSort.Recent);
            Assert.AreEqual(f.Starter(1).Id, list[0].Wand.Id);   // 든 지팡이 맨 위
            CollectionAssert.AreEqual(new[] { b.Id, c.Id, a.Id }, new[] { list[1].Wand.Id, list[2].Wand.Id, list[3].Wand.Id });
        }

        [Test]
        public void S08_WandList__EntryFlagsCoreEmptyLockedAppraisal()
        {
            var shopWand = f.AddWand(2, 5);
            f.StartExpedition(f.Starter(2).Id);
            var list = f.Wands.List(2, WandSort.StageDesc);
            var held = list[0];
            Assert.IsTrue(held.Locked);
            Assert.IsFalse(held.CoreEmpty);
            Assert.AreEqual(250, held.Appraisal);
            var other = list.Find(e => e.Wand.Id == shopWand.Id);
            Assert.IsFalse(other.Locked);
            Assert.IsTrue(other.CoreEmpty);
            Assert.AreEqual(3400, other.Appraisal);
        }

        static int[] Stages(List<WandListEntry> list)
        {
            var s = new int[list.Count];
            for (int i = 0; i < list.Count; i++) s[i] = list[i].Wand.Stage;
            return s;
        }

        // ------------------------------------------------------------ 3.1·3.3절: 그 캐릭터만, 개수 제한 없음, 든 지팡이

        [Test]
        public void S07_3_1__EquipOnlyOwnCharactersWand()
        {
            var w2 = f.AddWand(2, 0);
            int before = f.Store.SaveCount;
            var bad = f.Wands.Equip(1, w2.Id);
            Assert.AreEqual(ErrorCode.InvalidArgument, bad.Error);
            Assert.AreEqual(f.Starter(1).Id, f.P.Character(1).EquippedWandId);
            Assert.AreEqual(before, f.Store.SaveCount);

            var ok = f.Wands.Equip(2, w2.Id);
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(w2.Id, f.P.Character(2).EquippedWandId);
            Assert.AreEqual(before + 1, f.Store.SaveCount);
        }

        [Test]
        public void S07_3_3__NoWandCountLimit()
        {
            for (int i = 0; i < 1000; i++) f.AddWand(4, 0);
            Assert.AreEqual(1001, f.Wands.List(4, WandSort.StageDesc).Count);
        }

        // ------------------------------------------------------------ 03 SL-4·SL-6, 3.1절 "같은 블록 개체를 두 지팡이에 동시에 못 끼움"

        [Test]
        public void S03_SL_4__CoreOnlyProjectile()
        {
            f.AddLibrary(Ids.Block.Pierce, 1);
            var w = f.Starter(1);
            var lo = w.Loadout.Clone();
            lo.Core = Ids.Block.Pierce;
            Assert.AreEqual(ErrorCode.InvalidLoadout, f.Wands.SetLoadout(w.Id, lo).Error);
            Assert.AreEqual(Ids.Block.MagicBolt, w.Loadout.Core);
        }

        [Test]
        public void S03_SL_4__ModifierInLinkIsFine()
        {
            f.AddLibrary(Ids.Block.Pierce, 1);
            var w = f.Starter(1);
            var lo = w.Loadout.Clone();
            lo.Links[0] = Ids.Block.Pierce;
            Assert.IsTrue(f.Wands.SetLoadout(w.Id, lo).Ok);
            Assert.AreEqual(1, f.P.Item(Ids.Block.Pierce).QtySocketed);
            Assert.AreEqual(0, f.Wands.LibraryFree(Ids.Block.Pierce));
        }

        [Test]
        public void S07_3_1__SameBlockNotInTwoWands()
        {
            f.AddLibrary(Ids.Block.Fireball, 1);
            var w1 = f.Starter(1);
            var w2 = f.Starter(2);
            var lo1 = w1.Loadout.Clone(); lo1.Links[0] = Ids.Block.Fireball;
            Assert.IsTrue(f.Wands.SetLoadout(w1.Id, lo1).Ok);
            var lo2 = w2.Loadout.Clone(); lo2.Links[0] = Ids.Block.Fireball;
            Assert.AreEqual(ErrorCode.InvalidLoadout, f.Wands.SetLoadout(w2.Id, lo2).Error);
            // 첫 지팡이에서 빼면 둘째에 끼울 수 있다
            var back = w1.Loadout.Clone(); back.Links[0] = null;
            Assert.IsTrue(f.Wands.SetLoadout(w1.Id, back).Ok);
            Assert.IsTrue(f.Wands.SetLoadout(w2.Id, lo2).Ok);
            Assert.AreEqual(1, f.P.Item(Ids.Block.Fireball).QtySocketed);
        }

        [Test]
        public void S07_SetLoadout__SlotCountIsBasePlusExtra()
        {
            f.AddLibrary(Ids.Block.Pierce, 2);
            var w = f.Starter(3);   // char.3 = 기본 연결 5칸(SL-1)
            w.SlotsExtra = 1;
            var lo = new LoadoutData { Core = Ids.Block.MagicBolt };
            for (int i = 0; i < 5; i++) lo.Links.Add(null);
            lo.Extra.Add(Ids.Block.Pierce);
            Assert.IsTrue(f.Wands.SetLoadout(w.Id, lo).Ok);
            Assert.AreEqual(5, w.Loadout.Links.Count);
            Assert.AreEqual(1, w.Loadout.Extra.Count);
            Assert.AreEqual(Ids.Block.Pierce, w.Loadout.Extra[0]);

            // 확장 칸이 1칸인데 2칸째에 블록 → 칸 수가 맞지 않음
            var bad = lo.Clone(); bad.Extra.Add(Ids.Block.Pierce);
            Assert.AreEqual(ErrorCode.InvalidLoadout, f.Wands.SetLoadout(w.Id, bad).Error);
        }

        [Test]
        public void S03_SL_6__RemovingCoreIsSavedButCannotDepart()
        {
            var w = f.Starter(1);
            var lo = w.Loadout.Clone(); lo.Core = null;
            Assert.IsTrue(f.Wands.SetLoadout(w.Id, lo).Ok);
            Assert.AreEqual(1, f.Wands.LibraryFree(Ids.Block.MagicBolt));   // 빼면 서고로
            Assert.AreEqual(ErrorCode.CoreEmpty, f.Wands.BuildRunConfig(1, w.Id, 1, 7).Error);
        }

        // ------------------------------------------------------------ 01 3.2절: 마지막 투사체 보호

        [Test]
        public void S01_3_2__LastProjectileProtected()
        {
            // 서고 + 조립의 투사체 블록이 1개뿐이면 그 블록은 제물로 못 올린다
            f.P.Items[Ids.Block.MagicBolt] = new ItemStack { Qty = 1, QtySocketed = 1 };
            Assert.IsTrue(f.Wands.IsLastProjectile(Ids.Block.MagicBolt));
            f.AddLibrary(Ids.Block.Pierce, 1);
            Assert.IsFalse(f.Wands.IsLastProjectile(Ids.Block.Pierce));   // 투사체가 아니다
            f.AddLibrary(Ids.Block.Fireball, 1);
            Assert.IsFalse(f.Wands.IsLastProjectile(Ids.Block.MagicBolt));
            Assert.IsFalse(f.Wands.IsLastProjectile(Ids.Block.Fireball));
        }

        // ------------------------------------------------------------ 12.10 TV-SH-09 (서고 쪽): 100/100이면 남는 자리 0

        [Test]
        public void S07_TV_SH_09__LibraryFullExcludesSocketed()
        {
            // 끼운 마력탄 5개는 한도에 세지 않는다 → 다른 블록 100개면 100/100.
            // 귀환한 2개가 골드로 바뀌는 것은 정산(G11)이 LibraryCap − LibraryTotal = 0을 보고 한다.
            f.AddLibrary(Ids.Block.Pierce, 100);
            Assert.AreEqual(100, f.Wands.LibraryTotal());
            Assert.AreEqual(100, f.Wands.LibraryCap());
            Assert.AreEqual(0, f.Wands.LibraryCap() - f.Wands.LibraryTotal());
        }

        // ------------------------------------------------------------ 12.10 TV-AP-*

        [Test]
        public void S07_TV_AP_01__PlusZeroAppraisal250Refund0()
        {
            var w = f.AddWand(1, 0);
            Assert.AreEqual(250, f.Wands.Appraise(w));
            long gold = f.P.Currencies.Gold;
            var r = f.Wands.Release(new[] { w.Id });
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(0, r.Value);
            Assert.AreEqual(gold, f.P.Currencies.Gold);
        }

        [TestCase(5, 3400L)]
        [TestCase(7, 12494L)]
        [TestCase(10, 64624L)]
        [TestCase(13, 258309L)]
        public void S07_TV_AP_02__AppraisalTable(int stage, long expected)
        {
            Assert.AreEqual(expected, f.Wands.Appraise(new WandData { Character = 1, Stage = stage }));
        }

        [Test]
        public void S07_TV_AP_03__ReleasePlus5Refunds340()
        {
            var w = f.AddWand(1, 5);
            var r = f.Wands.Release(new[] { w.Id });
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(340, r.Value);
            Assert.AreEqual(340, f.P.Currencies.Gold);
        }

        [Test]
        public void S07_TV_AP_04__RefundIsFivePercentOfMeanGold()
        {
            // +5 평균 누적 강화 골드 6,800(7.4절 표) × 5% = 340
            Assert.AreEqual(6800 * 5 / 100, f.Wands.RefundOf(new WandData { Character = 1, Stage = 5 }));
        }

        [TestCase(1, 5L)]
        [TestCase(2, 26L)]
        [TestCase(8, 2206L)]
        [TestCase(12, 16965L)]
        [TestCase(13, 25830L)]
        public void S07_7_4__RefundTable(int stage, long refund)
        {
            Assert.AreEqual(refund, f.Wands.RefundOf(new WandData { Character = 1, Stage = stage }));
        }

        // ------------------------------------------------------------ 12.10 TV-RL-* (솔로 적용분)

        [Test]
        public void S07_TV_RL_01__LastWandCannotBeReleased()
        {
            int saves = f.Store.SaveCount;
            var r = f.Wands.Release(new[] { f.Starter(1).Id });
            Assert.AreEqual(ErrorCode.LastWand, r.Error);
            Assert.AreEqual(1, f.Wands.List(1, WandSort.StageDesc).Count);
            Assert.AreEqual(saves, f.Store.SaveCount);
        }

        [Test]
        public void S07_TV_RL_04__Plus7NeedsTypedName()
        {
            var w = f.AddWand(1, 7);
            Assert.AreEqual(ErrorCode.NameConfirmRequired, f.Wands.Release(new[] { w.Id }).Error);
            Assert.AreEqual(ErrorCode.NameConfirmRequired, f.Wands.Release(new[] { w.Id }, "x").Error);
            Assert.IsNotNull(f.P.Wand(w.Id));

            string name = f.Ctx.Names.Get(Ids.Char.WandKindOf(1));
            IWandService viaInterface = f.Ctx.Wands;
            var ok = viaInterface.Release(new[] { w.Id }, "  " + name + " ");   // 앞뒤 공백은 지운다
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(1249, ok.Value);
            Assert.IsNull(f.P.Wand(w.Id));
        }

        [Test]
        public void S07_TV_RL_04__Plus6NeedsNoName()
        {
            var w = f.AddWand(1, 6);
            Assert.IsTrue(f.Wands.Release(new[] { w.Id }).Ok);
        }

        [Test]
        public void S07_TV_RL_05__MultiSelectOnlyPlus3NoMutationNoExtra()
        {
            var a = f.AddWand(1, 3);                      // +3 변이 없음 → 고를 수 있음
            var b = f.AddWand(1, 2); b.SlotsExtra = 1;    // +2 확장 1칸 → 안 됨
            var c = f.AddWand(1, 4);                      // +4 → 안 됨
            var d = f.AddWand(1, 1); d.Mutations.Add(new MutationEntry { Id = Ids.Mutation.Gluttony, Seq = 1 });  // TV-150 +1 변이1 → 안 됨
            Assert.IsTrue(f.Wands.MultiCheckable(a));
            Assert.IsFalse(f.Wands.MultiCheckable(b));
            Assert.IsFalse(f.Wands.MultiCheckable(c));
            Assert.IsFalse(f.Wands.MultiCheckable(d));

            Assert.AreEqual(ErrorCode.NotEligible, f.Wands.Release(new[] { a.Id, b.Id }).Error);
            Assert.AreEqual(ErrorCode.NotEligible, f.Wands.Release(new[] { a.Id, c.Id }).Error);
            var e = f.AddWand(2, 0);
            var ok = f.Wands.Release(new[] { a.Id, e.Id });
            Assert.IsTrue(ok.Ok);
            Assert.AreEqual(68, ok.Value);   // +3 68 + +0 0
        }

        [Test]
        public void S07_TV_RL_05__MultiMustLeaveOnePerCharacter()
        {
            var a = f.AddWand(5, 1);
            var r = f.Wands.Release(new[] { a.Id, f.Starter(5).Id });
            Assert.AreEqual(ErrorCode.LastWand, r.Error);
            Assert.AreEqual(2, f.Wands.List(5, WandSort.StageDesc).Count);
        }

        [Test]
        public void S07_TV_RL_06__OnExpeditionRejected()
        {
            var spare = f.AddWand(1, 0);
            f.StartExpedition(spare.Id);
            Assert.AreEqual(ErrorCode.WandLocked, f.Wands.Release(new[] { spare.Id }).Error);
            f.Session.Main.Run.Phase = RunPhase.RunEnded;   // 판이 끝나면 풀린다
            Assert.IsTrue(f.Wands.Release(new[] { spare.Id }).Ok);
        }

        [Test]
        public void S07_14__MutationPendingRejected()
        {
            var w = f.AddWand(1, 2);
            w.PendingOffer = new MutationOffer { AttemptId = 1, Candidate = Ids.Mutation.Gluttony, FallbackOutcome = "FailNoDrop" };
            Assert.AreEqual(ErrorCode.MutationPending, f.Wands.Release(new[] { w.Id }).Error);
        }

        [Test]
        public void S07_TV_RL_08__ReleasingHeldPicksHighestStage()
        {
            var held = f.Starter(1); held.Stage = 5; held.FloorPeak = 5; held.RecordPeak = 5;
            var w7 = f.AddWand(1, 7);
            f.AddWand(1, 3);
            var r = f.Wands.Release(new[] { held.Id });
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(w7.Id, f.P.Character(1).EquippedWandId);
        }

        [Test]
        public void S07_3_3__NextHeldTieBreakRecordPeakThenRecent()
        {
            var held = f.Starter(1);
            var a = f.AddWand(1, 4, "2026-06-01T00:00:00+09:00"); a.RecordPeak = 6;
            var b = f.AddWand(1, 4, "2026-07-01T00:00:00+09:00"); b.RecordPeak = 6;
            var c = f.AddWand(1, 4, "2026-08-01T00:00:00+09:00"); c.RecordPeak = 5;
            Assert.IsTrue(f.Wands.Release(new[] { held.Id }).Ok);
            Assert.AreEqual(b.Id, f.P.Character(1).EquippedWandId);   // 단계 동률 → record_peak 6 둘 → 최근
        }

        [Test]
        public void S07_TV_RL_09__SocketedBlocksReturnSkinToWardrobe()
        {
            f.AddLibrary(Ids.Block.Fireball, 1);
            f.AddLibrary(Ids.Block.Pierce, 2);
            var w = f.AddWand(1, 1);
            w.SkinId = "cosmetic.test_skin";
            var lo = w.Loadout.Clone();
            lo.Core = Ids.Block.Fireball; lo.Links[0] = Ids.Block.Pierce; lo.Links[1] = Ids.Block.Pierce;
            Assert.IsTrue(f.Wands.SetLoadout(w.Id, lo).Ok);
            Assert.AreEqual(0, f.Wands.LibraryFree(Ids.Block.Fireball));
            Assert.AreEqual(0, f.Wands.LibraryFree(Ids.Block.Pierce));
            int total = f.Wands.LibraryTotal();

            Assert.IsTrue(f.Wands.Release(new[] { w.Id }).Ok);
            Assert.AreEqual(total + 3, f.Wands.LibraryTotal());   // 블록 서고 +3
            Assert.AreEqual(1, f.Wands.LibraryFree(Ids.Block.Fireball));
            Assert.AreEqual(2, f.Wands.LibraryFree(Ids.Block.Pierce));
            Assert.Contains("cosmetic.test_skin", f.P.Cosmetics.Owned);   // 겉모습 옷장
        }

        [Test]
        public void S07_Release__SavesOnceAndRejectsUnknownOrDuplicate()
        {
            var w = f.AddWand(1, 1);
            Assert.AreEqual(ErrorCode.NotFound, f.Wands.Release(new long[] { 9999 }).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Wands.Release(new[] { w.Id, w.Id }).Error);
            Assert.AreEqual(ErrorCode.InvalidArgument, f.Wands.Release(new long[0]).Error);
            int saves = f.Store.SaveCount;
            Assert.IsTrue(f.Wands.Release(new[] { w.Id }).Ok);
            Assert.AreEqual(saves + 1, f.Store.SaveCount);
        }

        // ------------------------------------------------------------ 12.13 TV-PF-06, 12.14: 서버로 올리기·결제 API 없음

        [Test]
        public void S07_TV_PF_06__NoUploadOrConvertApi()
        {
            foreach (var t in new[] { typeof(IWandService), typeof(IShopService), typeof(WandService), typeof(Wizard.Solo.Meta.Shop.ShopService) })
                foreach (var m in t.GetMethods())
                {
                    string n = m.Name.ToLowerInvariant();
                    foreach (var bad in new[] { "upload", "server", "migrate", "transfer", "convert", "sync" })
                        Assert.IsFalse(n.Contains(bad), t.Name + "." + m.Name);
                }
        }
    }
}
