using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    // 전리품 카드(06 요약서 4.4절). 난수는 w.Rng(RngStream.Loot) 하나만 쓴다.
    // 흐름: 카드 제시 → LootPick/LootUnpick으로 PickCount장 고름 → (LootReroll) → LootConfirm
    //       → 고른 카드와 빌린 블록이 보관함에 들어간다 → 보관함이 상한을 넘으면 Loot.OverflowToDiscard만큼 LootDiscard → 야영.
    public sealed partial class RunFlow
    {
        LootOffer BuildLoot(SimWorld w, RunScratch s)
        {
            var drops = w.Content.Drops;
            var rng = w.Rng(RngStream.Loot);
            var offer = new LootOffer { Wave = s.ClearedWave, PickCount = drops.CardsPick };

            int count;
            if (s.ClearedPhantomFirst) count = drops.CardsPhantomFirst;
            else if (s.ClearedIsBoss) count = drops.CardsBoss;
            else if (s.ClearedElites > 0) count = drops.CardsElite;
            else count = drops.CardsNormal;
            // 확정 칸은 보스 처치 카드의 마지막 칸([임시]). 첫 만남에는 없다.
            int guaranteed = s.ClearedIsBoss && !s.ClearedPhantomFirst ? count - 1 : -1;

            for (int i = 0; i < count; i++) offer.Cards.Add(RollCard(w, rng, offer.Wave, i == guaranteed));

            // 빌린 블록: 1일째 웨이브 6을 넘겼을 때만, 정해진 등급에서 균등 1장.
            if (offer.Wave == drops.BorrowedBlockWave && drops.BorrowedBlockCount > 0)
            {
                var pool = w.Content.BlocksOfRarity(drops.BorrowedBlockRarity);
                if (pool != null && pool.Count > 0) offer.BorrowedBlockId = pool[rng.Range(0, pool.Count)];
            }
            return offer;
        }

        static LootCard RollCard(SimWorld w, Pcg32 rng, int wave, bool guaranteed)
        {
            var drops = w.Content.Drops;
            CardRarityRowDef row = drops.RarityRowForDay(DayCycle.Day(wave));
            int common = row != null ? row.Common : 0, rare = row != null ? row.Rare : 0, hero = row != null ? row.Hero : 0;

            string rarity;
            if (guaranteed)
            {
                // 일반을 뺀 비율(희귀 : 영웅)로. 정해진 웨이브(30)부터의 보스는 영웅.
                if (wave >= drops.BossHeroGuaranteeWave) rarity = Ids.Rarity.Hero;
                else if (hero <= 0) rarity = Ids.Rarity.Rare;
                else rarity = rng.Range(0, rare + hero) < rare ? Ids.Rarity.Rare : Ids.Rarity.Hero;
            }
            else
            {
                int r = rng.Range(0, common + rare + hero);
                rarity = r < common ? Ids.Rarity.Common : r < common + rare ? Ids.Rarity.Rare : Ids.Rarity.Hero;
            }

            // 등급 안 종류는 고정 순서에서 균등. 한 제시 안 중복을 허용한다([임시]).
            IReadOnlyList<string> pool = w.Content.BlocksOfRarity(rarity);
            string blockId = pool != null && pool.Count > 0 ? pool[rng.Range(0, pool.Count)] : null;
            return new LootCard { BlockId = blockId, Rarity = rarity, Guaranteed = guaranteed };
        }

        static int PickedCount(LootOffer offer)
        {
            int n = 0;
            for (int i = 0; i < offer.Cards.Count; i++) if (offer.Cards[i].Picked) n++;
            return n;
        }

        void CmdLootPick(SimWorld w, int index, bool pick)
        {
            var offer = w.Run.Loot;
            if (offer == null || offer.OverflowToDiscard > 0) return;
            if (index < 0 || index >= offer.Cards.Count) return;
            var card = offer.Cards[index];
            if (pick)
            {
                // 이미 다 골랐으면 더 고른 것은 무시한다.
                if (card.Picked || PickedCount(offer) >= offer.PickCount) return;
                card.Picked = true;
            }
            else
            {
                if (!card.Picked) return;
                card.Picked = false;
            }
            w.Emit(SimEventType.LootChanged);
        }

        /// <summary>다시 뽑기: 지갑에서 값을 치르고 같은 확률로 전부 새로 뽑는다. 확정 칸은 확정으로 남는다. 고른 것은 비운다.</summary>
        void CmdLootReroll(SimWorld w)
        {
            var run = w.Run;
            var offer = run.Loot;
            if (offer == null || offer.OverflowToDiscard > 0) return;
            int cost = w.Content.Drops.RerollGold;
            if (run.Purse < cost) return;
            run.Purse -= cost;
            w.Emit(SimEventType.PurseChanged, a: run.Purse);
            var rng = w.Rng(RngStream.Loot);
            for (int i = 0; i < offer.Cards.Count; i++)
                offer.Cards[i] = RollCard(w, rng, offer.Wave, offer.Cards[i].Guaranteed);
            offer.Rerolls++;
            w.Emit(SimEventType.LootChanged);
        }

        WildBlock AddWild(SimWorld w, RunScratch s, string blockId, string rarity, bool borrowed, int wave)
        {
            var run = w.Run;
            var wb = new WildBlock { Uid = run.NextWildUid++, BlockId = blockId, Rarity = rarity, Borrowed = borrowed, AcquiredWave = wave };
            run.Storage.Add(wb);
            s.WildByUid[wb.Uid] = wb;
            return wb;
        }

        void CmdLootConfirm(SimWorld w, RunScratch s)
        {
            var run = w.Run;
            var offer = run.Loot;
            if (offer == null || offer.OverflowToDiscard > 0) return;
            if (PickedCount(offer) != offer.PickCount) return;
            var drops = w.Content.Drops;

            for (int i = 0; i < offer.Cards.Count; i++)
            {
                var card = offer.Cards[i];
                if (card.Picked && card.BlockId != null) AddWild(w, s, card.BlockId, card.Rarity, false, offer.Wave);
            }
            if (offer.BorrowedBlockId != null)
            {
                AddWild(w, s, offer.BorrowedBlockId, drops.BorrowedBlockRarity, true, offer.Wave);
                offer.BorrowedBlockId = null;
            }

            int over = run.Storage.Count - drops.StorageCap;
            if (over > 0)
            {
                // 보관함이 넘쳤다: 지금 보관함(방금 넣은 카드 포함)에서 버릴 블록을 고르게 한다.
                offer.OverflowToDiscard = over;
                w.Emit(SimEventType.LootChanged);
                return;
            }
            FinishLoot(w, s);
        }

        /// <summary>넘친 만큼 버린다. 버린 블록은 그 자리에서 골드(일반 5, 희귀 15, 영웅 50, 빌림 50).</summary>
        void CmdLootDiscard(SimWorld w, RunScratch s, int slotRef)
        {
            var run = w.Run;
            var offer = run.Loot;
            if (offer == null || offer.OverflowToDiscard <= 0) return;
            int index = slotRef >= StorageBase && slotRef < BenchBase ? slotRef - StorageBase : slotRef;
            if (index < 0 || index >= run.Storage.Count) return;
            var wb = run.Storage[index];
            run.Storage.RemoveAt(index);
            s.WildByUid.Remove(wb.Uid);
            GainGold(w, s, w.Content.Drops.DiscardGold(wb.Rarity, wb.Borrowed), NoPos);
            offer.OverflowToDiscard--;
            w.Emit(SimEventType.LootChanged);
            if (offer.OverflowToDiscard <= 0) FinishLoot(w, s);
        }

        void FinishLoot(SimWorld w, RunScratch s)
        {
            EnterCamp(w, s, startCamp: false);
        }
    }
}
