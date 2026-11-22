using System.Collections.Generic;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Run;

namespace Wizard.Solo.Net
{
    /// <summary>
    /// 서버 안 동료 봇의 보상 고르기(협동은 사람마다 자기 보상을 고르고, 모두 끝내야 넘어간다 — 봇이 멈춰 세우지 않게).
    /// 서버의 정본 세계를 보고 그 슬롯의 명령만 만든다(시뮬에는 통신이 없다. 부르는 쪽이 World.Enqueue 한다).
    /// 전리품: 별이 오르는 카드 → 맞물림 → 등급 순으로 고르고 받는다. 고를 수 없으면 골드(안 되면 체력)로 건너뛴다.
    /// 넘침: 가장 낮은 등급부터 버린다. 야영: 비기 후보의 첫 것을 고르고, 다 골랐으면 출발 준비.
    /// </summary>
    public static class CoopBotRewards
    {
        public static void Decide(SimWorld w, int slot, List<RunCommand> into)
        {
            if (w == null || w.Run == null || slot < 0 || slot >= w.Players.Length) return;
            var p = w.Players[slot];
            if (!p.Active || p.Left) return;
            var r = w.Run.Rewards[slot];
            if (w.Run.Phase == RunPhase.LootPick) DecideLoot(w, p, r, into);
            else if (w.InCamp) DecideCamp(p, r, into);
        }

        static void DecideLoot(SimWorld w, PlayerState p, SlotReward r, List<RunCommand> into)
        {
            var offer = r.Loot;
            if (r.LootDone || offer == null) return;
            int slot = p.Slot;
            if (offer.OverflowToDiscard > 0)
            {
                int worst = -1, worstRank = int.MaxValue;
                for (int i = 0; i < r.Storage.Count; i++)
                {
                    int rank = RarityRank(r.Storage[i].Rarity) + (r.Storage[i].Borrowed ? 10 : 0);
                    if (rank < worstRank) { worstRank = rank; worst = i; }
                }
                if (worst >= 0) into.Add(new RunCommand(RunCommandKind.LootDiscard, LootRules.StorageRef + worst, slot: slot));
                return;
            }

            var order = new List<int>();
            int picked = 0;
            for (int i = 0; i < offer.Cards.Count; i++)
            {
                if (offer.Cards[i].BlockId == null) continue;
                if (offer.Cards[i].Picked) { picked++; continue; }
                order.Add(i);
            }
            if (picked + order.Count >= offer.PickCount && offer.PickCount > 0)
            {
                order.Sort((a, b) =>
                {
                    int c = Score(offer.Cards[b]).CompareTo(Score(offer.Cards[a]));
                    return c != 0 ? c : a.CompareTo(b);
                });
                for (int k = 0; picked < offer.PickCount && k < order.Count; k++, picked++)
                    into.Add(new RunCommand(RunCommandKind.LootPick, order[k], slot: slot));
                into.Add(new RunCommand(RunCommandKind.LootConfirm, slot: slot));
                return;
            }
            if (offer.SkipGold > 0) into.Add(new RunCommand(RunCommandKind.LootSkip, RunFlow.SkipModeGold, slot: slot));
            else into.Add(new RunCommand(RunCommandKind.LootSkip, RunFlow.SkipModeHeal, slot: slot));
        }

        static void DecideCamp(PlayerState p, SlotReward r, List<RunCommand> into)
        {
            if (p.Downed) return;
            if (r.ArtOffer != null && r.ArtOffer.Candidates.Count > 0)
            {
                into.Add(new RunCommand(RunCommandKind.ArtPick, 0, 0, r.ArtOffer.Candidates[0], p.Slot));
                return;
            }
            if (!r.DepartReady) into.Add(new RunCommand(RunCommandKind.CampDepart, slot: p.Slot));
        }

        static int Score(LootCard c) =>
            (c.StarTarget > 0 ? 1000 : 0) + (c.Synergy != LootSynergy.None ? 100 : 0) + RarityRank(c.Rarity);

        static int RarityRank(string rarity) =>
            rarity == Ids.Rarity.Hero ? 3 : rarity == Ids.Rarity.Rare ? 2 : rarity == Ids.Rarity.Common ? 1 : 0;
    }
}
