using System;
using System.Collections.Generic;
using System.Globalization;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Meta.Inventory
{
    /// <summary>
    /// 지팡이·서고의 순수 규칙(프로필을 바꾸지 않는다). WandService·ShopService와 화면(서고·원정 준비·강화의 고르기)이 같은 값을 쓴다.
    /// 07 요약서 3절(지팡이 개체), 7.3·7.4절(서고 한도, 감정가·환급), 01 요약서 3.2절(마지막 투사체 보호).
    /// </summary>
    public static class InventoryRules
    {
        public const string OriginShop = "Shop";

        // ------------------------------------------------------------ 칸 수와 조립 변환

        /// <summary>그 캐릭터의 기본 연결 칸(characters.json base_link_slots, 03 SL-1).</summary>
        public static int BaseLinkSlots(IContentDb db, int character)
        {
            CharacterDef def = db != null ? db.Character(Ids.Char.Of(character)) : null;
            return def != null ? def.BaseLinkSlots : 0;
        }

        /// <summary>연결 칸 총수 L = base_link_slots + slots_extra(03 SL-3).</summary>
        public static int LinkSlotCount(IContentDb db, WandData wand) =>
            wand == null ? 0 : BaseLinkSlots(db, wand.Character) + wand.SlotsExtra;

        /// <summary>
        /// 프로필의 출발 조립(Links = 기본 칸, Extra = 확장 칸) → 시뮬레이션의 LoadoutSpec(Links = 연결 칸 전부, 03 SL-3 "확장 칸은 연결 칸 뒤").
        /// 이 변환은 여기 한 곳이다. 저장된 칸 수가 모자라면(예: 강화로 확장 칸이 막 늘었다) 빈 칸으로 채우고,
        /// 남는 칸에 블록이 있으면 조립이 지팡이와 맞지 않는 것이라 null을 돌려준다(InvalidLoadout).
        /// </summary>
        public static LoadoutSpec ToSpec(LoadoutData loadout, int baseSlots, int extraSlots)
        {
            var spec = new LoadoutSpec { Core = Normalize(loadout != null ? loadout.Core : null) };
            if (!AppendFixed(spec.Links, loadout != null ? loadout.Links : null, baseSlots)) return null;
            if (!AppendFixed(spec.Links, loadout != null ? loadout.Extra : null, extraSlots)) return null;
            return spec;
        }

        public static LoadoutSpec ToSpec(IContentDb db, WandData wand) =>
            wand == null ? null : ToSpec(wand.Loadout, BaseLinkSlots(db, wand.Character), wand.SlotsExtra);

        static bool AppendFixed(List<string> into, List<string> src, int count)
        {
            int n = src != null ? src.Count : 0;
            for (int i = 0; i < count; i++) into.Add(i < n ? Normalize(src[i]) : null);
            for (int i = count; i < n; i++) if (Normalize(src[i]) != null) return false;
            return true;
        }

        static string Normalize(string id) => string.IsNullOrEmpty(id) ? null : id;

        public static bool CoreEmpty(LoadoutData loadout) => loadout == null || string.IsNullOrEmpty(loadout.Core);

        /// <summary>조립 안 블록 id별 개수(코어 + 연결 + 확장).</summary>
        public static Dictionary<string, int> CountBlocks(LoadoutData loadout)
        {
            var counts = new Dictionary<string, int>();
            if (loadout == null) return counts;
            Add(counts, loadout.Core);
            if (loadout.Links != null) foreach (var id in loadout.Links) Add(counts, id);
            if (loadout.Extra != null) foreach (var id in loadout.Extra) Add(counts, id);
            return counts;
        }

        public static Dictionary<string, int> CountBlocks(LoadoutSpec spec)
        {
            var counts = new Dictionary<string, int>();
            if (spec == null) return counts;
            Add(counts, spec.Core);
            if (spec.Links != null) foreach (var id in spec.Links) Add(counts, id);
            return counts;
        }

        static void Add(Dictionary<string, int> counts, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            counts.TryGetValue(id, out int c);
            counts[id] = c + 1;
        }

        /// <summary>
        /// 조립의 모양 검사(서고 수량은 보지 않는다): 코어는 투사체만(03 SL-4), 모든 칸이 아는 블록, 칸 수 = L.
        /// 코어 빔은 여기서 오류가 아니다(저장은 되고, 출발·연습에서 CoreEmpty).
        /// </summary>
        public static ErrorCode CheckShape(IContentDb db, LoadoutSpec spec, int linkSlotCount)
        {
            if (spec == null || spec.Links == null || spec.Links.Count != linkSlotCount) return ErrorCode.InvalidLoadout;
            if (!string.IsNullOrEmpty(spec.Core))
            {
                BlockDef core = db != null ? db.Block(spec.Core) : null;
                if (core == null || !core.IsProjectile) return ErrorCode.InvalidLoadout;
            }
            foreach (var id in spec.Links)
                if (!string.IsNullOrEmpty(id) && (db == null || db.Block(id) == null)) return ErrorCode.InvalidLoadout;
            return ErrorCode.None;
        }

        /// <summary>
        /// 주문 컴파일 검사(00 문서 5.2절: 문맥은 ctx.Rules.Traits 하나로 만든다 — 밑줄·감쇠를 여기서 뽑지 않는다).
        /// ComputeDerived(db, 캐릭터 정의, 단계, 변이) → CompileContext(derived, L) → Spells.Compile. 오류 진단이 있으면 InvalidLoadout.
        /// rules가 null이거나 Spells가 빈 구현(기본 클래스 그대로)이면 검사를 건너뛴다.
        /// </summary>
        public static ErrorCode CheckCompile(SimModuleSet rules, IContentDb db, int character, int stage,
            IReadOnlyList<string> mutations, LoadoutSpec spec, int linkSlotCount)
        {
            if (rules == null || rules.Traits == null || rules.Spells == null) return ErrorCode.None;
            if (rules.Spells.GetType() == typeof(SpellModule)) return ErrorCode.None;
            CharacterDef ch = db != null ? db.Character(Ids.Char.Of(character)) : null;
            PlayerDerived derived = rules.Traits.ComputeDerived(db, ch, stage, mutations ?? (IReadOnlyList<string>)Array.Empty<string>());
            SpellCompileContext cctx = rules.Traits.CompileContext(derived, linkSlotCount);
            CompiledSpell spell = rules.Spells.Compile(db, spec.Core, spec.Links, cctx);
            if (spell == null) return ErrorCode.InvalidLoadout;
            foreach (var d in spell.Diagnostics)
            {
                if (!d.IsError) continue;
                return d.Code == SpellDiagCode.E_CORE_EMPTY ? ErrorCode.CoreEmpty : ErrorCode.InvalidLoadout;
            }
            return ErrorCode.None;
        }

        // ------------------------------------------------------------ 서고 (07 요약서 7.3절)

        /// <summary>서고 한도 = library_base_cap + shop_library_step_blocks × library_expansions (100 + 50 × n, D-32).</summary>
        public static int LibraryCap(EconomyConfig econ, ProfileData profile)
        {
            if (econ == null || profile == null) return 0;
            return econ.LibraryBaseCap + econ.ShopLibraryStepBlocks * profile.LibraryExpansions;
        }

        /// <summary>서고 개수 = Σ 블록 qty − qty_socketed(끼운 블록은 한도에 세지 않는다).</summary>
        public static int LibraryTotal(IContentDb db, ProfileData profile)
        {
            if (profile == null) return 0;
            int total = 0;
            foreach (var kv in profile.Items)
                if (IsBlock(db, kv.Key) && kv.Value != null) total += Math.Max(0, kv.Value.Qty - kv.Value.QtySocketed);
            return total;
        }

        /// <summary>그 블록의 아직 안 끼운 수량.</summary>
        public static int LibraryFree(ProfileData profile, string blockId)
        {
            if (profile == null || string.IsNullOrEmpty(blockId)) return 0;
            if (!profile.Items.TryGetValue(blockId, out var s) || s == null) return 0;
            return Math.Max(0, s.Qty - s.QtySocketed);
        }

        public static bool IsBlock(IContentDb db, string id) =>
            !string.IsNullOrEmpty(id) && (db != null ? db.Block(id) != null : id.StartsWith("block.", StringComparison.Ordinal));

        /// <summary>
        /// 마지막 투사체 보호(01 요약서 3.2절, O 1.1): 서고 + 조립에 있는 투사체 블록 수 합(끼운 것 포함)이 1개뿐이면 그 블록은
        /// 제물로 올릴 수 없다(강화 쪽 LastProjectile). blockId가 투사체가 아니면 false.
        /// </summary>
        public static bool IsLastProjectile(IContentDb db, ProfileData profile, string blockId)
        {
            if (db == null || profile == null) return false;
            BlockDef b = db.Block(blockId);
            if (b == null || !b.IsProjectile) return false;
            return ProjectileBlockCount(db, profile) <= 1;
        }

        /// <summary>서고의 투사체 블록 수(끼운 것 포함).</summary>
        public static int ProjectileBlockCount(IContentDb db, ProfileData profile)
        {
            int n = 0;
            if (db == null || profile == null) return 0;
            foreach (var kv in profile.Items)
            {
                BlockDef b = db.Block(kv.Key);
                if (b != null && b.IsProjectile && kv.Value != null) n += Math.Max(0, kv.Value.Qty);
            }
            return n;
        }

        // ------------------------------------------------------------ 감정가·환급 (07 요약서 7.4절)

        /// <summary>감정가 = 7.4절 표(그 단계까지 평균 강화 골드 × 0.5). +0은 appraisal_wand_plus0(250). 변이·슬롯은 넣지 않는다.</summary>
        public static long Appraise(EconomyConfig econ, int stage)
        {
            if (econ == null) return 0;
            if (stage <= 0) return econ.AppraisalWandPlus0;
            var table = econ.AppraisalByStage;
            if (table == null || table.Count == 0) return 0;
            int i = Math.Min(stage, table.Count - 1);
            return table[i];
        }

        /// <summary>떠나보내기 환급 = 감정가 × release_refund_pct% 내림. +0은 0골드(TV-AP-01).</summary>
        public static long Refund(EconomyConfig econ, int stage)
        {
            if (econ == null || stage <= 0) return 0;
            return Appraise(econ, stage) * econ.ReleaseRefundPct / 100;
        }

        // ------------------------------------------------------------ 떠나보내기 (07 요약서 3.5절, 08 요약서 10.2절)

        /// <summary>+7 이상은 지팡이 이름을 그대로 입력해야 한다.</summary>
        public static bool NeedsNameConfirm(InventoryTuning t, WandData wand) =>
            wand != null && wand.Stage >= t.ReleaseNameConfirmMinStage;

        /// <summary>입력한 이름 비교: 앞뒤 공백 제거 뒤 이름 표 글자와 정확히 같다([임시], TV-149).</summary>
        public static bool NameMatches(string typed, string expected)
        {
            if (typed == null || expected == null) return false;
            return string.Equals(typed.Trim(), expected.Trim(), StringComparison.Ordinal);
        }

        /// <summary>여러 자루 고르기에 넣을 수 있나(TV-150): +3 이하 && 변이 0 && 확장 0.</summary>
        public static bool MultiCheckable(InventoryTuning t, WandData wand) =>
            wand != null && wand.Stage <= t.ReleaseMultiMaxStage
            && (wand.Mutations != null ? wand.Mutations.Count : 0) <= t.ReleaseMultiMaxMutations
            && wand.SlotsExtra <= t.ReleaseMultiMaxSlotsExtra;

        /// <summary>
        /// 든 지팡이를 떠나보냈을 때 다음 든 지팡이(07 3.3절 [임시 U-19]): 단계 최고 → record_peak 높은 순 → acquired_at 최근 순 → id 작은 순.
        /// </summary>
        public static WandData PickNextEquipped(IEnumerable<WandData> remaining)
        {
            WandData best = null;
            foreach (var w in remaining)
                if (best == null || CompareForEquip(w, best) < 0) best = w;
            return best;
        }

        static int CompareForEquip(WandData a, WandData b)
        {
            int c = b.Stage.CompareTo(a.Stage);
            if (c != 0) return c;
            c = b.RecordPeak.CompareTo(a.RecordPeak);
            if (c != 0) return c;
            c = AcquiredTicks(b).CompareTo(AcquiredTicks(a));
            if (c != 0) return c;
            return a.Id.CompareTo(b.Id);
        }

        /// <summary>acquired_at(ISO 8601)의 UTC 틱. 읽지 못하면 0(가장 오래된 것으로 본다).</summary>
        public static long AcquiredTicks(WandData w)
        {
            if (w == null || string.IsNullOrEmpty(w.AcquiredAt)) return 0;
            return DateTimeOffset.TryParse(w.AcquiredAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t.UtcTicks : 0;
        }

        // ------------------------------------------------------------ 새 지팡이 (07 요약서 3.2절)

        /// <summary>상점 +0 지팡이: +0, 같은 0 상태, 빈 조립(코어도 빔, 연결 칸 base_link_slots개 빈 칸), 겉모습 없음, origin Shop, 주인 1번째.</summary>
        public static WandData NewShopWand(IContentDb db, ProfileData profile, int character, string acquiredAtIso)
        {
            long id = profile.NextWandId;
            foreach (var old in profile.Wands) if (old.Id >= id) id = old.Id + 1;   // 번호가 겹치지 않게(손댄 저장 파일 방어)
            profile.NextWandId = id + 1;
            var w = new WandData
            {
                Id = id,
                Character = character,
                Origin = OriginShop,
                OwnerCount = 1,
                AcquiredAt = acquiredAtIso,
                Loadout = new LoadoutData { Core = null }
            };
            int baseSlots = BaseLinkSlots(db, character);
            for (int i = 0; i < baseSlots; i++) w.Loadout.Links.Add(null);
            return w;
        }

        public static bool ValidCharacter(int character) => character >= 1 && character <= Ids.Char.Count;
    }
}
