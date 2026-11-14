using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    /// <summary>
    /// DataSolo/Loot.json(전리품 개편 손잡이)을 한 번 읽어 둔 것. 조각이 없으면 06 요약서 4.4절 그대로 돈다
    /// (drops.json의 등급 표, 다시 뽑기 30골드 고정, 건너뛰기·운 보정·별 없음).
    /// </summary>
    public sealed class LootTuning
    {
        /// <summary>Resources/DataSolo/Loot.json. 주문 모듈(SpellTuning)도 같은 조각의 star 절을 읽는다.</summary>
        public const string ExtraName = "Loot";

        /// <summary>날별 등급 확률(있으면 drops.json 대신). 비면 drops.json.</summary>
        public readonly List<CardRarityRowDef> RarityByDay = new List<CardRarityRowDef>();
        public int RerollGoldBase, RerollGoldStep;
        public int SkipGoldBase, SkipGoldPerWave, SkipHealCells;
        public int PityAfterDryOffers, PityBpPerDryOffer, PityBpCap;
        public int StarMax;
        /// <summary>보스 카드의 확정 칸이 영웅이 되는 웨이브(0 = drops.json의 boss_hero_guarantee_wave 30).</summary>
        public int BossHeroGuaranteeWave;
        /// <summary>맞물림 짝(순서 없음). 열쇠 = 블록 id, 값 = 짝 블록 id들(데이터 순서).</summary>
        readonly Dictionary<string, List<string>> _pairs = new Dictionary<string, List<string>>();

        public static LootTuning From(IContentDb db)
        {
            var t = new LootTuning();
            JObject jo = db != null ? db.SoloExtra(ExtraName) : null;
            var drops = db != null ? db.Drops : null;
            t.RerollGoldBase = drops != null ? drops.RerollGold : 0;
            if (jo == null) return t;

            if (jo["rarity_by_day"] is JArray rows)
                foreach (var r in rows)
                    if (r is JObject o)
                        t.RarityByDay.Add(new CardRarityRowDef { DayFrom = I(o, "day_from"), Common = I(o, "common"), Rare = I(o, "rare"), Hero = I(o, "hero") });
            if (jo["reroll_gold_base"] != null) t.RerollGoldBase = I(jo, "reroll_gold_base");
            t.RerollGoldStep = I(jo, "reroll_gold_step");
            t.SkipGoldBase = I(jo, "skip_gold_base");
            t.SkipGoldPerWave = I(jo, "skip_gold_per_wave");
            t.SkipHealCells = I(jo, "skip_heal_cells");
            t.PityAfterDryOffers = I(jo, "pity_after_dry_offers");
            t.PityBpPerDryOffer = I(jo, "pity_bp_per_dry_offer");
            t.PityBpCap = I(jo, "pity_bp_cap");
            if (jo["star"] is JObject star) t.StarMax = I(star, "max");
            t.BossHeroGuaranteeWave = I(jo, "boss_hero_guarantee_wave");
            if (jo["pairs"] is JArray pairs)
                foreach (var p in pairs)
                {
                    if (!(p is JArray pa) || pa.Count != 2) continue;
                    string a = (string)pa[0], b = (string)pa[1];
                    if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) continue;
                    t.AddPair(a, b);
                    t.AddPair(b, a);
                }
            return t;
        }

        void AddPair(string a, string b)
        {
            List<string> list;
            if (!_pairs.TryGetValue(a, out list)) _pairs[a] = list = new List<string>();
            if (!list.Contains(b)) list.Add(b);
        }

        public bool IsPair(string a, string b)
        {
            List<string> list;
            return a != null && b != null && _pairs.TryGetValue(a, out list) && list.Contains(b);
        }

        /// <summary>그 날의 등급 확률 줄: 이 조각의 표가 있으면 그것, 없으면 drops.json.</summary>
        public CardRarityRowDef RarityRowForDay(IContentDb db, int day)
        {
            if (RarityByDay.Count == 0) return db != null && db.Drops != null ? db.Drops.RarityRowForDay(day) : null;
            CardRarityRowDef best = null;
            for (int i = 0; i < RarityByDay.Count; i++) if (RarityByDay[i].DayFrom <= day) best = RarityByDay[i];
            return best;
        }

        /// <summary>보스 확정 칸이 영웅인 첫 웨이브(조정 값이 있으면 그것).</summary>
        public int HeroGuaranteeWave(IContentDb db)
        {
            int spec = db != null && db.Drops != null ? db.Drops.BossHeroGuaranteeWave : 0;
            return BossHeroGuaranteeWave > 0 ? BossHeroGuaranteeWave : spec;
        }

        /// <summary>이 제시에서 rerolls번 다시 뽑은 뒤의 다시 뽑기 값.</summary>
        public int RerollCost(int rerolls) => RerollGoldBase + RerollGoldStep * (rerolls > 0 ? rerolls : 0);

        /// <summary>건너뛰기 골드(넘긴 웨이브 기준).</summary>
        public int SkipGold(int wave) => SkipGoldBase + SkipGoldPerWave * (wave > 0 ? wave : 0);

        /// <summary>맞물림 카드 없는 제시가 dry번 이어졌을 때 카드 한 장의 운 보정 확률(bp). 0 = 보정 없음.</summary>
        public int PityBp(int dry)
        {
            if (PityBpPerDryOffer <= 0 || dry < PityAfterDryOffers) return 0;
            int bp = PityBpPerDryOffer * (dry - PityAfterDryOffers + 1);
            return PityBpCap > 0 && bp > PityBpCap ? PityBpCap : bp;
        }

        static int I(JObject o, string key)
        {
            JToken v = o[key];
            if (v == null || (v.Type != JTokenType.Integer && v.Type != JTokenType.Float)) return 0;
            return (int)(float)v;
        }
    }

    /// <summary>
    /// 전리품 카드와 지금 조립의 관계(맞물림, 별 겹치기). 순수 함수 — 시뮬레이션(제시·운 보정)과 시험이 같이 쓴다.
    /// "지금 조립" = 지팡이 칸(코어 + 연결 칸)에 든 블록. 보관함의 블록은 맞물림에 세지 않는다(끼워야 효과가 난다).
    /// </summary>
    public static class LootRules
    {
        /// <summary>
        /// 그 블록이 지금 조립과 맞물리나. 짝(데이터) 먼저, 그다음 같은 속성의 변형·투사체가 서로 효과가 있을 때(화염구 + 크기 2배).
        /// 짝 블록은 칸 순서(코어 → 연결 칸)에서 처음 맞는 것. 이미 끼운 같은 블록은 짝으로 보지 않는다(그것은 별 겹치기).
        /// </summary>
        public static LootSynergy Synergy(IContentDb db, LootTuning t, RunLoadout lo, string blockId, out string partnerId)
        {
            partnerId = null;
            if (db == null || lo == null || string.IsNullOrEmpty(blockId)) return LootSynergy.None;
            BlockDef card = db.Block(blockId);
            if (card == null) return LootSynergy.None;
            int n = (lo.Links != null ? lo.Links.Length : 0) + 1;

            if (t != null)
                for (int i = 0; i < n; i++)
                {
                    string id = SlotId(lo, i);
                    if (id == null || id == blockId) continue;
                    if (t.IsPair(blockId, id)) { partnerId = id; return LootSynergy.Pair; }
                }

            if (string.IsNullOrEmpty(card.Element) || card.Element == Ids.Element.Neutral || card.KindEnum == BlockKind.Control) return LootSynergy.None;
            for (int i = 0; i < n; i++)
            {
                string id = SlotId(lo, i);
                if (id == null || id == blockId) continue;
                BlockDef other = db.Block(id);
                if (other == null || other.Element != card.Element) continue;
                bool ok = card.IsProjectile && other.KindEnum == BlockKind.Modifier ? Affects(other, card)
                        : card.KindEnum == BlockKind.Modifier && other.IsProjectile ? Affects(card, other)
                        : false;
                if (ok) { partnerId = id; return LootSynergy.Element; }
            }
            return LootSynergy.None;
        }

        /// <summary>변형이 그 투사체에 효과가 있나(03 요약서 4.6절 W_NO_EFFECT 표와 같은 규칙. 모듈 구현을 직접 부르지 않으려고 여기 둔다).</summary>
        static bool Affects(BlockDef modifier, BlockDef projectile)
        {
            switch (modifier.Effect)
            {
                case Ids.BlockEffect.Pierce: return !(projectile.HasFlag(Ids.BlockFlag.PassThrough) || projectile.Instant);
                case Ids.BlockEffect.Bounce:
                case Ids.BlockEffect.Homing: return !projectile.Instant;
                default: return true;
            }
        }

        /// <summary>맞물림 "처음 발견"을 가르는 열쇠(카드 블록 + 짝).</summary>
        public static string SynergyKey(string blockId, string partnerId) => blockId + "|" + partnerId;

        static string SlotId(RunLoadout lo, int slot)
        {
            SlotItem it = slot == 0 ? lo.Core : lo.Links[slot - 1];
            return it.IsEmpty ? null : it.BlockId;
        }

        /// <summary>
        /// 그 블록을 고르면 별이 오를 자리. 찾는 순서: 지팡이 칸(코어 → 연결 칸) → 보관함 → 서고 블록 대기 칸. 빌린 블록과 별이 상한인 것은 건너뛴다.
        /// 찾으면 그 자리(SlotRef: 0..L 칸, 100 + i 보관함, 300 + i 대기 칸)와 지금 별 수, 없으면 -1.
        /// </summary>
        public static int StarTargetRef(RunState run, RunLoadout lo, string blockId, int starMax, out int stars)
        {
            stars = 0;
            if (string.IsNullOrEmpty(blockId) || starMax <= 0) return -1;
            if (lo != null)
            {
                int n = (lo.Links != null ? lo.Links.Length : 0) + 1;
                for (int i = 0; i < n; i++)
                {
                    SlotItem it = i == 0 ? lo.Core : lo.Links[i - 1];
                    if (it.IsEmpty || it.Borrowed || it.BlockId != blockId || it.Stars >= starMax) continue;
                    stars = it.Stars;
                    return i;
                }
            }
            if (run != null)
            {
                for (int i = 0; i < run.Storage.Count; i++)
                {
                    var wb = run.Storage[i];
                    if (wb.Borrowed || wb.BlockId != blockId || wb.Stars >= starMax) continue;
                    stars = wb.Stars;
                    return StorageRef + i;
                }
                for (int i = 0; i < run.LibraryBench.Count; i++)
                {
                    var wb = run.LibraryBench[i];
                    if (wb.BlockId != blockId || wb.Stars >= starMax) continue;
                    stars = wb.Stars;
                    return BenchRef + i;
                }
            }
            return -1;
        }

        /// <summary>SlotRef 규칙(RunCommand 주석): 보관함 100 + i, 대기 칸 300 + i.</summary>
        public const int StorageRef = 100, BenchRef = 300;
    }
}
