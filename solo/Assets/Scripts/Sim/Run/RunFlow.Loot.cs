using System.Collections.Generic;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    // 전리품 카드(06 요약서 4.4절 + 사용자 조정 DataSolo/Loot.json). 난수는 w.Rng(RngStream.Loot) 하나만 쓴다.
    // 흐름: 카드 제시 → LootPick/LootUnpick으로 PickCount장 고름 → (LootReroll, 값이 오른다) → LootConfirm 또는 LootSkip
    //       → 고른 카드: 같은 블록을 이미 가졌으면 그 블록의 별(★)이 오르고, 아니면 보관함에 들어간다. 빌린 블록은 늘 보관함에
    //       → 보관함이 상한을 넘으면 Loot.OverflowToDiscard만큼 LootDiscard → 야영.
    // 조정(06 4.4절 [임시]를 바꾼 것): 한 제시 안 중복 없음, 맞물림 표식, 운 보정, 다시 뽑기 값이 오름, 건너뛰기, 별 겹치기, 1일째 영웅 소량.
    public sealed partial class RunFlow
    {
        static readonly string[] RarityFallbackAny = { Ids.Rarity.Common, Ids.Rarity.Rare, Ids.Rarity.Hero };
        static readonly string[] RarityFallbackHigh = { Ids.Rarity.Rare, Ids.Rarity.Hero };

        LootTuning LootTune(SimWorld w, RunScratch s) => s.Loot ?? (s.Loot = LootTuning.From(w.Content));

        /// <summary>보상을 고르는 사람인가(협동에서 판 도중 나간 사람은 빠진다). 쓰러져 있어도 고른다.</summary>
        static bool Participating(PlayerState p) => p != null && p.Active && !p.Left;

        /// <summary>
        /// 웨이브를 넘긴 뒤 참여 중인 사람마다 자기 전리품 제시를 만든다(슬롯 순서, 같은 RngStream.Loot — 솔로는 예전과 같은 난수 순서).
        /// 맞물림·별·운 보정은 그 사람의 조립과 보관함으로 본다. 고를 사람이 없으면 false.
        /// </summary>
        bool OfferLootToAll(SimWorld w, RunScratch s)
        {
            bool any = false;
            for (int i = 0; i < w.Players.Length; i++)
            {
                var p = w.Players[i];
                var r = w.Run.Rewards[i];
                r.LootDone = true;
                r.Loot = null;
                if (!Participating(p)) continue;
                r.Loot = BuildLoot(w, s, p);
                r.LootDone = false;
                any = true;
            }
            return any;
        }

        LootOffer BuildLoot(SimWorld w, RunScratch s, PlayerState p)
        {
            var drops = w.Content.Drops;
            var lt = LootTune(w, s);
            var rng = w.Rng(RngStream.Loot);
            var offer = new LootOffer { Wave = s.ClearedWave, PickCount = drops.CardsPick };

            int count;
            if (s.ClearedPhantomFirst) count = drops.CardsPhantomFirst;
            else if (s.ClearedIsBoss) count = drops.CardsBoss;
            else if (s.ClearedElites > 0) count = drops.CardsElite;
            else count = drops.CardsNormal;
            // 확정 칸은 보스 처치 카드의 마지막 칸([임시]). 첫 만남에는 없다.
            int guaranteed = s.ClearedIsBoss && !s.ClearedPhantomFirst ? count - 1 : -1;

            for (int i = 0; i < count; i++) offer.Cards.Add(new LootCard { Guaranteed = i == guaranteed });
            RollCards(w, s, rng, offer, p);

            // 빌린 블록: 1일째 웨이브 6을 넘겼을 때만, 정해진 등급에서 균등 1장.
            if (offer.Wave == drops.BorrowedBlockWave && drops.BorrowedBlockCount > 0)
            {
                var pool = w.Content.BlocksOfRarity(drops.BorrowedBlockRarity);
                if (pool != null && pool.Count > 0) offer.BorrowedBlockId = pool[rng.Range(0, pool.Count)];
            }

            offer.RerollCost = lt.RerollCost(0);
            offer.SkipGold = lt.SkipGold(offer.Wave);
            offer.SkipHealCells = lt.SkipHealCells;
            return offer;
        }

        /// <summary>
        /// 제시의 카드를 모두 (다시) 뽑는다. 카드마다: 등급(날별 확률, 확정 칸은 일반을 뺀 비율) → 종류(그 등급에서 이미 나온 블록을 뺀 것 중 균등,
        /// 운 보정이 걸리면 그중 맞물림 블록에서) → 맞물림·별 표시. 끝에 맞물림이 없던 제시 수를 갱신한다.
        /// </summary>
        void RollCards(SimWorld w, RunScratch s, Pcg32 rng, LootOffer offer, PlayerState p)
        {
            var lt = LootTune(w, s);
            var lo = p != null ? p.Loadout : null;
            int slot = p != null ? p.Slot : 0;
            var seen = s.SeenSynergy[slot];
            int pityBp = lt.PityBp(s.LootDryOffers[slot]);
            offer.PityBp = pityBp;
            var used = new List<string>(offer.Cards.Count);
            for (int i = 0; i < offer.Cards.Count; i++)
            {
                var card = RollCard(w, lt, rng, offer.Wave, offer.Cards[i].Guaranteed, used, lo, pityBp);
                offer.Cards[i] = card;
                if (card.BlockId != null) used.Add(card.BlockId);
            }

            bool any = false;
            for (int i = 0; i < offer.Cards.Count; i++)
            {
                var c = offer.Cards[i];
                if (c.BlockId == null) continue;
                string partner;
                c.Synergy = LootRules.Synergy(w.Content, lt, lo, c.BlockId, out partner);
                c.SynergyPartnerId = partner;
                if (c.Synergy != LootSynergy.None)
                {
                    any = true;
                    string key = LootRules.SynergyKey(c.BlockId, partner);
                    c.SynergyFirst = !seen.Contains(key);
                    seen.Add(key);
                }
                int stars;
                c.StarTarget = LootRules.StarTargetRef(w.Run.Rewards[slot], lo, c.BlockId, lt.StarMax, out stars) >= 0 ? stars + 1 : 0;
            }
            s.LootDryOffers[slot] = any ? 0 : s.LootDryOffers[slot] + 1;
        }

        LootCard RollCard(SimWorld w, LootTuning lt, Pcg32 rng, int wave, bool guaranteed, List<string> used, RunLoadout lo, int pityBp)
        {
            CardRarityRowDef row = lt.RarityRowForDay(w.Content, DayCycle.Day(wave));
            int common = row != null ? row.Common : 0, rare = row != null ? row.Rare : 0, hero = row != null ? row.Hero : 0;

            string rarity;
            if (guaranteed)
            {
                // 일반을 뺀 비율(희귀 : 영웅)로. 정해진 웨이브(30)부터의 보스는 영웅.
                // 확정 칸은 명세 표(drops.json)의 비율 그대로 둔다(TV-212~214: 1일째 희귀 100%, 2일째 25:2). 조정 표는 보통 카드에만.
                CardRarityRowDef spec = w.Content.Drops.RarityRowForDay(DayCycle.Day(wave));
                int gRare = spec != null ? spec.Rare : rare, gHero = spec != null ? spec.Hero : hero;
                if (wave >= lt.HeroGuaranteeWave(w.Content)) rarity = Ids.Rarity.Hero;
                else if (gHero <= 0) rarity = Ids.Rarity.Rare;
                else rarity = rng.Range(0, gRare + gHero) < gRare ? Ids.Rarity.Rare : Ids.Rarity.Hero;
            }
            else
            {
                int r = rng.Range(0, common + rare + hero);
                rarity = r < common ? Ids.Rarity.Common : r < common + rare ? Ids.Rarity.Rare : Ids.Rarity.Hero;
            }

            // 한 제시 안 중복 없음: 그 등급에서 이미 나온 것을 뺀다. 다 나왔으면 다른 등급(확정 칸은 희귀·영웅 안에서만)으로.
            var pool = Remaining(w.Content.BlocksOfRarity(rarity), used);
            if (pool.Count == 0)
            {
                var order = guaranteed ? RarityFallbackHigh : RarityFallbackAny;
                for (int k = 0; k < order.Length && pool.Count == 0; k++)
                {
                    if (order[k] == rarity) continue;
                    pool = Remaining(w.Content.BlocksOfRarity(order[k]), used);
                    if (pool.Count > 0) rarity = order[k];
                }
            }
            if (pool.Count == 0) return new LootCard { Rarity = rarity, Guaranteed = guaranteed };

            // 운 보정: 맞물림 없는 제시가 이어졌으면 이 확률로 종류를 맞물림 블록에서 뽑는다(등급은 그대로).
            bool boosted = false;
            if (pityBp > 0 && rng.Range(0, BpScale) < pityBp)
            {
                var syn = new List<string>();
                for (int i = 0; i < pool.Count; i++)
                {
                    string partner;
                    if (LootRules.Synergy(w.Content, lt, lo, pool[i], out partner) != LootSynergy.None) syn.Add(pool[i]);
                }
                if (syn.Count > 0) { pool = syn; boosted = true; }
            }
            string blockId = pool[rng.Range(0, pool.Count)];
            return new LootCard { BlockId = blockId, Rarity = rarity, Guaranteed = guaranteed, PityBoosted = boosted };
        }

        /// <summary>bp(만분율) 분모.</summary>
        const int BpScale = 10000;

        static List<string> Remaining(IReadOnlyList<string> pool, List<string> used)
        {
            var list = new List<string>();
            if (pool == null) return list;
            for (int i = 0; i < pool.Count; i++) if (!used.Contains(pool[i])) list.Add(pool[i]);
            return list;
        }

        static int PickedCount(LootOffer offer)
        {
            int n = 0;
            for (int i = 0; i < offer.Cards.Count; i++) if (offer.Cards[i].Picked) n++;
            return n;
        }

        /// <summary>그 슬롯 사람의 지금 제시(고르는 중일 때만). 끝냈거나 없으면 null.</summary>
        static LootOffer OfferOf(SimWorld w, int slot)
        {
            var p = PlayerOf(w, slot);
            if (!Participating(p)) return null;
            var r = w.Run.Rewards[slot];
            return r.LootDone ? null : r.Loot;
        }

        void CmdLootPick(SimWorld w, int slot, int index, bool pick)
        {
            var offer = OfferOf(w, slot);
            if (offer == null || offer.OverflowToDiscard > 0) return;
            if (index < 0 || index >= offer.Cards.Count) return;
            var card = offer.Cards[index];
            if (card.BlockId == null) return;
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
            w.Emit(SimEventType.LootChanged, slot);
        }

        /// <summary>다시 뽑기: 지갑에서 지금 값을 치르고 같은 확률로 전부 새로 뽑는다. 확정 칸은 확정으로 남는다. 고른 것은 비운다.
        /// 값은 이 제시에서 다시 뽑을 때마다 오른다(Brotato 방식, 다음 제시에서 처음 값으로). 지갑은 원정대 공용이다.</summary>
        void CmdLootReroll(SimWorld w, RunScratch s, int slot)
        {
            var run = w.Run;
            var offer = OfferOf(w, slot);
            if (offer == null || offer.OverflowToDiscard > 0) return;
            var lt = LootTune(w, s);
            int cost = lt.RerollCost(offer.Rerolls);
            if (run.Purse < cost) return;
            run.Purse -= cost;
            w.Emit(SimEventType.PurseChanged, a: run.Purse);
            var rng = w.Rng(RngStream.Loot);
            for (int i = 0; i < offer.Cards.Count; i++) offer.Cards[i] = new LootCard { Guaranteed = offer.Cards[i].Guaranteed };
            RollCards(w, s, rng, offer, w.Players[slot]);
            offer.Rerolls++;
            offer.RerollCost = lt.RerollCost(offer.Rerolls);
            w.Emit(SimEventType.LootChanged, slot);
        }

        WildBlock AddWild(SimWorld w, RunScratch s, int slot, string blockId, string rarity, bool borrowed, int wave)
        {
            var run = w.Run;
            var wb = new WildBlock { Uid = run.NextWildUid++, BlockId = blockId, Rarity = rarity, Borrowed = borrowed, AcquiredWave = wave };
            run.Rewards[slot].Storage.Add(wb);
            s.WildByUid[wb.Uid] = wb;
            return wb;
        }

        /// <summary>
        /// 고른 블록을 받는다: 같은 블록(빌림 제외, 별이 상한 아래)이 있으면 그 별을 하나 올리고(지팡이 칸 → 보관함 → 대기 칸 순서),
        /// 없으면 보관함에 새로 넣는다. 지팡이 칸의 별이 올랐으면 true(다시 컴파일한다). 보관함·대기 칸은 그 사람(p) 것이다.
        /// </summary>
        bool TakeBlock(SimWorld w, RunScratch s, LootTuning lt, PlayerState p, string blockId, string rarity, int wave)
        {
            var r0 = w.Run.Rewards[p.Slot];
            var lo = p.Loadout;
            int stars;
            int r = LootRules.StarTargetRef(r0, lo, blockId, lt.StarMax, out stars);
            if (r < 0)
            {
                AddWild(w, s, p.Slot, blockId, rarity, false, wave);
                return false;
            }
            if (r >= LootRules.BenchRef)
            {
                r0.LibraryBench[r - LootRules.BenchRef].Stars = stars + 1;
                return false;
            }
            if (r >= LootRules.StorageRef)
            {
                r0.Storage[r - LootRules.StorageRef].Stars = stars + 1;
                return false;
            }
            SlotItem it = GetSlot(lo, r);
            it.Stars = stars + 1;
            SetSlot(lo, r, it);
            WildBlock wb;
            if (it.WildUid != 0 && s.WildByUid.TryGetValue(it.WildUid, out wb)) wb.Stars = it.Stars;
            return true;
        }

        void CmdLootConfirm(SimWorld w, RunScratch s, int slot)
        {
            var offer = OfferOf(w, slot);
            if (offer == null || offer.OverflowToDiscard > 0) return;
            if (PickedCount(offer) != offer.PickCount) return;
            var lt = LootTune(w, s);
            var p = w.Players[slot];

            bool recompile = false;
            for (int i = 0; i < offer.Cards.Count; i++)
            {
                var card = offer.Cards[i];
                if (card.Picked && card.BlockId != null) recompile |= TakeBlock(w, s, lt, p, card.BlockId, card.Rarity, offer.Wave);
            }
            if (recompile) w.Mods.Player.RecompileSpell(w, p);
            TakeBorrowedAndFinish(w, s, slot);
        }

        /// <summary>건너뛰기: 카드를 받지 않고 작은 확정 보상(mode 0 골드 / 1 체력). 빌린 블록은 그대로 받는다.</summary>
        void CmdLootSkip(SimWorld w, RunScratch s, int slot, int mode)
        {
            var offer = OfferOf(w, slot);
            if (offer == null || offer.OverflowToDiscard > 0) return;
            if (mode == SkipModeHeal)
            {
                var p = w.Players[slot];
                if (offer.SkipHealCells <= 0 || p.Hp >= p.MaxHp) return;
                w.Mods.Player.Heal(w, p, offer.SkipHealCells);
            }
            else if (mode == SkipModeGold)
            {
                if (offer.SkipGold <= 0) return;
                GainGold(w, s, offer.SkipGold, NoPos);
            }
            else return;
            for (int i = 0; i < offer.Cards.Count; i++) offer.Cards[i].Picked = false;
            TakeBorrowedAndFinish(w, s, slot);
        }

        /// <summary>LootSkip의 A.</summary>
        public const int SkipModeGold = 0, SkipModeHeal = 1;

        void TakeBorrowedAndFinish(SimWorld w, RunScratch s, int slot)
        {
            var r = w.Run.Rewards[slot];
            var offer = r.Loot;
            var drops = w.Content.Drops;
            if (offer.BorrowedBlockId != null)
            {
                AddWild(w, s, slot, offer.BorrowedBlockId, drops.BorrowedBlockRarity, true, offer.Wave);
                offer.BorrowedBlockId = null;
            }

            int over = r.Storage.Count - drops.StorageCap;
            if (over > 0)
            {
                // 보관함이 넘쳤다: 지금 보관함(방금 넣은 카드 포함)에서 버릴 블록을 고르게 한다.
                offer.OverflowToDiscard = over;
                w.Emit(SimEventType.LootChanged, slot);
                return;
            }
            FinishLoot(w, s, slot);
        }

        /// <summary>넘친 만큼 버린다. 버린 블록은 그 자리에서 골드(일반 5, 희귀 15, 영웅 50, 빌림 50).</summary>
        void CmdLootDiscard(SimWorld w, RunScratch s, int slot, int slotRef)
        {
            var offer = OfferOf(w, slot);
            if (offer == null || offer.OverflowToDiscard <= 0) return;
            var storage = w.Run.Rewards[slot].Storage;
            int index = slotRef >= StorageBase && slotRef < BenchBase ? slotRef - StorageBase : slotRef;
            if (index < 0 || index >= storage.Count) return;
            var wb = storage[index];
            storage.RemoveAt(index);
            s.WildByUid.Remove(wb.Uid);
            GainGold(w, s, w.Content.Drops.DiscardGold(wb.Rarity, wb.Borrowed), NoPos);
            offer.OverflowToDiscard--;
            w.Emit(SimEventType.LootChanged, slot);
            if (offer.OverflowToDiscard <= 0) FinishLoot(w, s, slot);
        }

        /// <summary>그 사람의 고르기가 끝났다. 참여 중인 사람이 모두 끝냈으면 야영으로(솔로는 바로).</summary>
        void FinishLoot(SimWorld w, RunScratch s, int slot)
        {
            var r = w.Run.Rewards[slot];
            r.LootDone = true;
            if (AllLootDone(w)) EnterCamp(w, s, startCamp: false);
            else w.Emit(SimEventType.LootChanged, slot); // 동료를 기다린다(내 화면은 기다림으로 바뀐다)
        }

        /// <summary>참여 중인 사람이 모두 전리품 고르기를 끝냈나.</summary>
        static bool AllLootDone(SimWorld w)
        {
            for (int i = 0; i < w.Players.Length; i++)
                if (Participating(w.Players[i]) && !w.Run.Rewards[i].LootDone) return false;
            return true;
        }
    }
}
