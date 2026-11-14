using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>전리품 화면의 상태(08 요약서 8.1절 LootPickState).</summary>
    public enum LootPickPhase { Revealing, Choosing, OverflowDiscard, Done }

    /// <summary>카드 한 장의 그릴 값(엔진 없음). 글자는 열쇠로.</summary>
    public sealed class LootCardView
    {
        public int Index;
        public string BlockId;
        public string Rarity;
        public string GradeKey;
        public string KindKey;
        public BlockKind Kind;
        public string Element;
        public bool Guaranteed;
        public bool Picked;
        public bool Borrowed;
        /// <summary>열·피해·간격 줄(해당하는 것만): 열쇠 + 값 글자.</summary>
        public readonly List<KeyValuePair<string, string>> StatLines = new List<KeyValuePair<string, string>>();
    }

    /// <summary>
    /// 전리품 고르기의 화면 규칙(08 요약서 8.1절, 06 요약서 4.4절). 상태는 시뮬레이션(w.Run.Loot)이 정본이고, 모델은 그것을 읽어
    /// 그릴 값·버튼 상태·보낼 명령을 정한다. 명령: LootPick/LootUnpick(A = 카드 번호), LootReroll, LootDiscard(A = 100 + i), LootConfirm.
    /// </summary>
    public sealed class LootPickModel
    {
        /// <summary>카드 크기와 간격(08 8.1절 카드 240×336, 간격 24). 화면 배치 상수.</summary>
        public const float CardWidth = 240f, CardHeight = 336f, CardGap = 24f;

        public readonly List<LootCardView> Cards = new List<LootCardView>();
        public LootCardView BorrowedCard;
        public int PickCount;
        public int Picked;
        public int Purse;
        public int RerollCost;
        public int OverflowToDiscard;
        public int StorageCount;
        public int StorageCap;

        /// <summary>4장 1032, 5장 1296(TV-064).</summary>
        public static float RowWidth(int cards) => cards <= 0 ? 0f : cards * CardWidth + (cards - 1) * CardGap;

        public static LootPickModel From(IContentDb db, RunState run)
        {
            var m = new LootPickModel();
            if (run == null) return m;
            var drops = db != null ? db.Drops : null;
            m.RerollCost = drops != null ? drops.RerollGold : 0;
            m.StorageCap = drops != null ? drops.StorageCap : 0;
            m.Purse = run.Purse;
            m.StorageCount = run.Storage.Count;
            var offer = run.Loot;
            if (offer == null) return m;
            m.PickCount = offer.PickCount;
            m.OverflowToDiscard = offer.OverflowToDiscard;
            for (int i = 0; i < offer.Cards.Count; i++)
            {
                var c = offer.Cards[i];
                var v = Card(db, c.BlockId, c.Rarity, false);
                v.Index = i;
                v.Guaranteed = c.Guaranteed;
                v.Picked = c.Picked;
                if (c.Picked) m.Picked++;
                m.Cards.Add(v);
            }
            if (offer.BorrowedBlockId != null)
            {
                m.BorrowedCard = Card(db, offer.BorrowedBlockId, drops != null ? drops.BorrowedBlockRarity : Ids.Rarity.Hero, true);
                m.BorrowedCard.Index = -1;
            }
            return m;
        }

        public static LootCardView Card(IContentDb db, string blockId, string rarity, bool borrowed)
        {
            var v = new LootCardView { BlockId = blockId, Rarity = rarity, Borrowed = borrowed };
            v.GradeKey = "str.block.grade." + (rarity ?? Ids.Rarity.Common);
            BlockDef def = db != null && blockId != null ? db.Block(blockId) : null;
            if (def != null)
            {
                v.Kind = def.KindEnum;
                v.KindKey = "str.block.kind." + def.Kind;
                v.Element = def.Element;
                // 해당하는 것만(08 8.1절): 열은 0이 아닐 때, 피해·간격은 투사체만.
                if (def.Heat != 0f) v.StatLines.Add(new KeyValuePair<string, string>("str.block.heat", CampFormat.OneDecimal(def.Heat)));
                if (def.IsProjectile && def.BaseDamage > 0f) v.StatLines.Add(new KeyValuePair<string, string>("str.block.damage", CampFormat.OneDecimal(def.BaseDamage)));
                if (def.IsProjectile && def.IntervalSeconds > 0f) v.StatLines.Add(new KeyValuePair<string, string>("str.block.interval", CampFormat.ShortSeconds(def.IntervalSeconds)));
            }
            return v;
        }

        public LootPickPhase Phase => OverflowToDiscard > 0 ? LootPickPhase.OverflowDiscard : LootPickPhase.Choosing;

        /// <summary>[확인] 켜짐 ⇔ 고른 수 == 고를 수(TV-060).</summary>
        public bool ConfirmEnabled => Phase == LootPickPhase.Choosing && PickCount > 0 && Picked == PickCount;

        /// <summary>[다시 뽑기]: 지갑 ≥ 30(TV-061 지갑 29 → 꺼짐, TV-192). 넘침 처리 중에는 꺼짐.</summary>
        public bool RerollEnabled => Phase == LootPickPhase.Choosing && Purse >= RerollCost;

        /// <summary>꺼진 이유 열쇠(지갑 부족이면 error.not_enough_gold).</summary>
        public string RerollDisabledReason => Purse < RerollCost ? "error.not_enough_gold" : null;

        /// <summary>
        /// 카드를 눌렀을 때 보낼 명령. 고른 카드 → 빼기, 안 고른 카드 → 고를 수가 남았으면 넣기, 아니면 무시(TV-060 3번째 무시).
        /// 보낼 것이 없으면 false.
        /// </summary>
        public bool ToggleCommand(int index, out RunCommand cmd)
        {
            cmd = default;
            if (Phase != LootPickPhase.Choosing || index < 0 || index >= Cards.Count) return false;
            if (Cards[index].Picked) { cmd = new RunCommand(RunCommandKind.LootUnpick, index); return true; }
            if (Picked >= PickCount) return false;
            cmd = new RunCommand(RunCommandKind.LootPick, index);
            return true;
        }

        /// <summary>확인 뒤 넘침으로 버려야 할 수(TV-063: 보관함 11 + 2장 = 13 → 1). 빌린 블록도 보관함에 들어간다.</summary>
        public static int OverflowAfterConfirm(int storageCount, int picks, bool borrowed, int cap)
        {
            int over = storageCount + picks + (borrowed ? 1 : 0) - cap;
            return over > 0 ? over : 0;
        }

        /// <summary>넘침 처리에서 보관함 i번째를 버릴 때 받는 골드(06 4.4절: 일반 5, 희귀 15, 영웅 50, 빌림 50).</summary>
        public static int DiscardGold(IContentDb db, WildBlock wb)
        {
            if (db == null || db.Drops == null || wb == null) return 0;
            return db.Drops.DiscardGold(wb.Rarity, wb.Borrowed);
        }

        /// <summary>넘침 처리의 버리기 명령(A = 보관함 자리 SlotRef 100 + i). 서고 블록 대기 칸은 대상이 아니다.</summary>
        public static RunCommand DiscardCommand(int storageIndex) => new RunCommand(RunCommandKind.LootDiscard, SlotRefs.Storage(storageIndex));
    }
}
