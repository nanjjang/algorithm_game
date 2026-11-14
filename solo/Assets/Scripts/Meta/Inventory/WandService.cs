using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Meta.Inventory
{
    /// <summary>
    /// 지팡이 개체·조립·서고·떠나보내기와 판 설정 만들기(IWandService). 07 요약서 3·7절, 08 요약서 5.3·5.4·8.5·10.2절.
    /// - 지팡이는 그 캐릭터만 쓴다(Equip·출발은 지팡이의 character와 같은 캐릭터만). 개수 제한은 없다(D1.22).
    /// - 프로필을 바꾼 메서드는 스스로 Save()를 부른다. BuildPracticeConfig만은 프로필을 바꾸지 않는다(저장 없음).
    /// - 메타 인터페이스의 캐릭터는 int(1~5). 시뮬레이션에 넘길 때만 Ids.Char.Of로 "char.N"을 만든다.
    /// - 유료 재화·결제 코드는 없다.
    /// </summary>
    public sealed class WandService : IWandService
    {
        readonly GameContext _ctx;
        readonly InventoryTuning _tuning;

        public WandService(GameContext ctx)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _tuning = InventoryTuning.From(ctx.Content);
        }

        ProfileData Profile => _ctx.Profiles != null ? _ctx.Profiles.Current : null;
        IContentDb Db => _ctx.Content;
        EconomyConfig Econ => _ctx.Content != null ? _ctx.Content.Economy : null;
        public InventoryTuning Tuning => _tuning;

        void Save() { if (_ctx.Profiles != null) _ctx.Profiles.Save(); }

        // ------------------------------------------------------------ 잠금(원정 중)

        /// <summary>
        /// 원정 중인 지팡이인가(솔로의 잠금은 OnExpedition 하나 — 07 3.1절 lock_state, 08 5.4절).
        /// 본판(IGameSession.MainWorld)이 무한 웨이브이고 아직 끝나지 않았으며 그 지팡이로 나갔으면 잠겨 있다. 연습·튜토리얼은 잠그지 않는다.
        /// </summary>
        public bool IsLocked(long wandId)
        {
            var session = _ctx.Session;
            SimWorld w = session != null ? session.MainWorld : null;
            if (w == null || w.Run == null) return false;
            RunConfig cfg = w.Run.Config;
            return cfg != null && cfg.Mode == RunMode.Endless && cfg.WandId == wandId && w.Run.Phase != RunPhase.RunEnded;
        }

        // ------------------------------------------------------------ 목록

        public List<WandListEntry> List(int character, WandSort sort)
        {
            var result = new List<WandListEntry>();
            var p = Profile;
            if (p == null || (character != 0 && !InventoryRules.ValidCharacter(character))) return result;

            var held = new List<WandListEntry>();
            var rest = new List<WandListEntry>();
            foreach (var w in p.Wands)
            {
                if (character != 0 && w.Character != character) continue;
                var rec = p.Character(w.Character);
                var e = new WandListEntry
                {
                    Wand = w,
                    Equipped = rec != null && rec.EquippedWandId == w.Id,
                    Locked = IsLocked(w.Id),
                    CoreEmpty = InventoryRules.CoreEmpty(w.Loadout),
                    Appraisal = Appraise(w),
                };
                (e.Equipped ? held : rest).Add(e);
            }
            // 든 지팡이는 어느 정렬에서든 맨 위(08 5.3절). 전부(0)를 볼 때는 캐릭터 순으로.
            held.Sort((a, b) => a.Wand.Character != b.Wand.Character ? a.Wand.Character.CompareTo(b.Wand.Character) : a.Wand.Id.CompareTo(b.Wand.Id));
            Comparison<WandListEntry> cmp = Comparer(sort);
            StableSort(rest, cmp);
            result.AddRange(held);
            result.AddRange(rest);
            return result;
        }

        /// <summary>정렬 키(08 5.3절). 동률 깨기 [임시 U-50]: 단계 desc → acquired_at desc → id asc(IL은 솔로에 없다).
        /// 최근 얻은 순은 acquired_at desc → id desc(같은 시각이면 늦게 만든 번호가 최근).</summary>
        static Comparison<WandListEntry> Comparer(WandSort sort)
        {
            switch (sort)
            {
                case WandSort.Recent:
                    return (a, b) =>
                    {
                        int c = InventoryRules.AcquiredTicks(b.Wand).CompareTo(InventoryRules.AcquiredTicks(a.Wand));
                        return c != 0 ? c : b.Wand.Id.CompareTo(a.Wand.Id);
                    };
                case WandSort.StageAsc:
                    return (a, b) =>
                    {
                        int c = a.Wand.Stage.CompareTo(b.Wand.Stage);
                        return c != 0 ? c : TieBreak(a, b);
                    };
                default:
                    return (a, b) =>
                    {
                        int c = b.Wand.Stage.CompareTo(a.Wand.Stage);
                        return c != 0 ? c : TieBreak(a, b);
                    };
            }
        }

        static int TieBreak(WandListEntry a, WandListEntry b)
        {
            int c = InventoryRules.AcquiredTicks(b.Wand).CompareTo(InventoryRules.AcquiredTicks(a.Wand));
            return c != 0 ? c : a.Wand.Id.CompareTo(b.Wand.Id);
        }

        static void StableSort(List<WandListEntry> list, Comparison<WandListEntry> cmp)
        {
            // 삽입 정렬(안정). 목록은 사람이 보는 크기라 충분하다.
            for (int i = 1; i < list.Count; i++)
            {
                var x = list[i];
                int j = i - 1;
                while (j >= 0 && cmp(list[j], x) > 0) { list[j + 1] = list[j]; j--; }
                list[j + 1] = x;
            }
        }

        // ------------------------------------------------------------ 든 지팡이

        public Result<WandData> Equip(int character, long wandId)
        {
            var p = Profile;
            if (p == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            if (!InventoryRules.ValidCharacter(character)) return Result<WandData>.Fail(ErrorCode.InvalidArgument);
            var w = p.Wand(wandId);
            if (w == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            // 지팡이는 그 캐릭터만 쓴다(07 3.1절, DECISIONS D2.4).
            if (w.Character != character) return Result<WandData>.Fail(ErrorCode.InvalidArgument);
            var rec = p.Character(character);
            if (rec == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            if (rec.EquippedWandId != wandId)
            {
                rec.EquippedWandId = wandId;
                Save();
            }
            return Result<WandData>.Success(w);
        }

        // ------------------------------------------------------------ 출발 조립

        public Result<WandData> SetLoadout(long wandId, LoadoutData loadout)
        {
            var p = Profile;
            if (p == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            if (loadout == null) return Result<WandData>.Fail(ErrorCode.InvalidArgument);
            var w = p.Wand(wandId);
            if (w == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            if (IsLocked(wandId)) return Result<WandData>.Fail(ErrorCode.WandLocked);

            int baseSlots = InventoryRules.BaseLinkSlots(Db, w.Character);
            LoadoutSpec spec = InventoryRules.ToSpec(loadout, baseSlots, w.SlotsExtra);
            if (spec == null) return Result<WandData>.Fail(ErrorCode.InvalidLoadout);
            ErrorCode shape = InventoryRules.CheckShape(Db, spec, baseSlots + w.SlotsExtra);
            if (shape != ErrorCode.None) return Result<WandData>.Fail(shape);

            // 서고 수량 − 끼움 수 검사: 이 지팡이에 이미 끼운 것은 다시 쓸 수 있다(같은 블록 개체를 두 지팡이에 동시에 끼우지 못한다, 07 3.1절).
            var oldCounts = InventoryRules.CountBlocks(w.Loadout);
            var newCounts = InventoryRules.CountBlocks(spec);
            foreach (var kv in newCounts)
            {
                oldCounts.TryGetValue(kv.Key, out int had);
                int need = kv.Value - had;
                if (need > 0 && need > InventoryRules.LibraryFree(p, kv.Key)) return Result<WandData>.Fail(ErrorCode.InvalidLoadout);
            }

            foreach (var kv in oldCounts)
            {
                if (!p.Items.TryGetValue(kv.Key, out var s) || s == null) continue;
                s.QtySocketed = Math.Max(0, s.QtySocketed - kv.Value);
            }
            foreach (var kv in newCounts) p.Item(kv.Key).QtySocketed += kv.Value;

            var stored = new LoadoutData { Core = spec.Core };
            for (int i = 0; i < spec.Links.Count; i++)
                (i < baseSlots ? stored.Links : stored.Extra).Add(spec.Links[i]);
            w.Loadout = stored;
            Save();
            return Result<WandData>.Success(w);
        }

        // ------------------------------------------------------------ 떠나보내기

        /// <summary>
        /// 떠나보내기(07 3.5절). 이름 입력 없이 부르는 줄임 — +7 이상은 NameConfirmRequired(TV-RL-04).
        /// </summary>
        public Result<long> Release(IReadOnlyList<long> wandIds) => Release(wandIds, null);

        /// <summary>
        /// 떠나보내기. typedName = +7 이상 한 자루일 때 입력한 지팡이 이름(앞뒤 공백 제거 뒤 이름 표 "wand.N" 글자와 같아야 한다).
        /// 거절: 원정 중 WandLocked, 변이 결정 대기 MutationPending, 여러 자루인데 +3 초과·변이·확장 있음 NotEligible,
        /// 이름 불일치 NameConfirmRequired, 고른 묶음을 빼면 어느 캐릭터의 지팡이가 0자루 LastWand.
        /// 성공: 지팡이 삭제, 끼운 블록 → 서고(끼움 수 감소), 덧씌운 겉모습 → 옷장, 환급(감정가 × 10% 내림, +0은 0) → 골드, 든 지팡이 다시 고르기.
        /// </summary>
        public Result<long> Release(IReadOnlyList<long> wandIds, string typedName)
        {
            var p = Profile;
            if (p == null) return Result<long>.Fail(ErrorCode.NotFound);
            if (wandIds == null || wandIds.Count == 0) return Result<long>.Fail(ErrorCode.InvalidArgument);

            var targets = new List<WandData>(wandIds.Count);
            var idSet = new HashSet<long>();
            foreach (long id in wandIds)
            {
                if (!idSet.Add(id)) return Result<long>.Fail(ErrorCode.InvalidArgument);
                var w = p.Wand(id);
                if (w == null) return Result<long>.Fail(ErrorCode.NotFound);
                targets.Add(w);
            }

            bool multi = targets.Count > 1;
            foreach (var w in targets)
            {
                if (IsLocked(w.Id)) return Result<long>.Fail(ErrorCode.WandLocked);
                if (w.PendingOffer != null) return Result<long>.Fail(ErrorCode.MutationPending);
                if (multi && !InventoryRules.MultiCheckable(_tuning, w)) return Result<long>.Fail(ErrorCode.NotEligible);
            }

            // 마지막 한 자루(07 3.3절): 고른 묶음을 빼고도 캐릭터마다 1자루 이상 남아야 한다.
            foreach (var w in targets)
            {
                int left = 0;
                foreach (var o in p.Wands) if (o.Character == w.Character && !idSet.Contains(o.Id)) left++;
                if (left < 1) return Result<long>.Fail(ErrorCode.LastWand);
            }

            foreach (var w in targets)
            {
                if (!InventoryRules.NeedsNameConfirm(_tuning, w)) continue;
                if (!InventoryRules.NameMatches(typedName, WandDisplayName(w))) return Result<long>.Fail(ErrorCode.NameConfirmRequired);
            }

            long refund = 0;
            foreach (var w in targets)
            {
                refund += InventoryRules.Refund(Econ, w.Stage);
                foreach (var kv in InventoryRules.CountBlocks(w.Loadout))
                {
                    if (!p.Items.TryGetValue(kv.Key, out var s) || s == null) continue;
                    s.QtySocketed = Math.Max(0, s.QtySocketed - kv.Value);
                }
                if (!string.IsNullOrEmpty(w.SkinId) && !p.Cosmetics.Owned.Contains(w.SkinId)) p.Cosmetics.Owned.Add(w.SkinId);
                p.Wands.Remove(w);
            }

            foreach (var rec in p.Characters)
            {
                if (!idSet.Contains(rec.EquippedWandId)) continue;
                var next = InventoryRules.PickNextEquipped(p.Wands.FindAll(o => o.Character == rec.Id));
                rec.EquippedWandId = next != null ? next.Id : 0;
            }

            p.Currencies.Gold += refund;
            Save();
            return Result<long>.Success(refund);
        }

        /// <summary>이름 입력 비교에 쓰는 지팡이 표시 이름(이름 표 "wand.N"). 이름 표가 없으면 열쇠 그대로.</summary>
        public string WandDisplayName(WandData w)
        {
            string key = Ids.Char.WandKindOf(w.Character);
            return _ctx.Names != null ? _ctx.Names.Get(key) : key;
        }

        /// <summary>떠나보내기 확인 창이 이름 입력 칸을 띄우나(+7 이상).</summary>
        public bool NeedsNameConfirm(WandData w) => InventoryRules.NeedsNameConfirm(_tuning, w);

        /// <summary>여러 자루 고르기의 체크 상자를 켤 수 있나(TV-150). 마지막 한 자루 검사는 고른 묶음을 보고 따로 한다.</summary>
        public bool MultiCheckable(WandData w) => InventoryRules.MultiCheckable(_tuning, w);

        public long Appraise(WandData wand) => wand == null ? 0 : InventoryRules.Appraise(Econ, wand.Stage);

        /// <summary>떠나보내면 돌려받는 골드(확인 창 표시와 Release가 같은 값).</summary>
        public long RefundOf(WandData wand) => wand == null ? 0 : InventoryRules.Refund(Econ, wand.Stage);

        // ------------------------------------------------------------ 서고

        public int LibraryFree(string blockId) => InventoryRules.LibraryFree(Profile, blockId);
        public int LibraryTotal() => InventoryRules.LibraryTotal(Db, Profile);
        public int LibraryCap() => InventoryRules.LibraryCap(Econ, Profile);

        /// <summary>마지막 투사체 보호(01 3.2절): 이 블록을 제물로 올리면 서고 + 조립의 투사체가 0이 되나.</summary>
        public bool IsLastProjectile(string blockId) => InventoryRules.IsLastProjectile(Db, Profile, blockId);

        // ------------------------------------------------------------ 판 설정

        public Result<RunConfig> BuildRunConfig(int character, long wandId, int startWave, ulong seed)
        {
            var p = Profile;
            if (p == null) return Result<RunConfig>.Fail(ErrorCode.NotFound);
            var check = PrepareWand(p, character, wandId, out WandData w);
            if (check != ErrorCode.None) return Result<RunConfig>.Fail(check);
            if (IsLocked(wandId)) return Result<RunConfig>.Fail(ErrorCode.WandLocked);

            // 출발 조건(08 5.4절 CanDepart): 코어가 비면 CoreEmpty(TV-031·032), 컴파일 오류면 InvalidLoadout.
            if (InventoryRules.CoreEmpty(w.Loadout)) return Result<RunConfig>.Fail(ErrorCode.CoreEmpty);
            LoadoutSpec spec = InventoryRules.ToSpec(Db, w);
            int linkSlots = InventoryRules.LinkSlotCount(Db, w);
            ErrorCode e = ValidateSpec(w, spec, linkSlots);
            if (e != ErrorCode.None) return Result<RunConfig>.Fail(e);

            // 시작 웨이브(01 4.6절): skip + 1이 고를 수 있는 건너뛰는 수 안에 있어야 한다. 정산 서비스가 없으면 1만.
            var rec = p.Character(character);
            int best = rec != null ? rec.BestWave : 0;
            List<int> skips = _ctx.Settlement != null ? _ctx.Settlement.StartSkipOptions(best) : null;
            if (!StartWaveAllowed(skips, startWave)) return Result<RunConfig>.Fail(ErrorCode.InvalidArgument);

            var cfg = BaseConfig(p, w, character, spec, linkSlots, seed);
            cfg.Mode = RunMode.Endless;
            cfg.RunId = Guid.NewGuid().ToString("N");
            cfg.StartWave = startWave;
            cfg.PhantomFirst = !p.Flags.PhantomMet;
            cfg.RevengePending = p.RevengeState == RevengeState.Pursuing;
            cfg.CrowBundlePending = !p.Flags.CrowBundleGiven;

            p.RunsStarted++;
            p.LastCharacter = character;   // 원정 준비에서 마지막으로 고른 캐릭터(D-56)
            Save();
            return Result<RunConfig>.Success(cfg);
        }

        public Result<RunConfig> BuildPracticeConfig(int character, long wandId, RunMode mode, LoadoutSpec overrideLoadout, ulong seed)
        {
            if (mode != RunMode.Practice && mode != RunMode.Tutorial) return Result<RunConfig>.Fail(ErrorCode.InvalidArgument);
            var p = Profile;
            if (p == null) return Result<RunConfig>.Fail(ErrorCode.NotFound);
            var check = PrepareWand(p, character, wandId, out WandData w);
            if (check != ErrorCode.None) return Result<RunConfig>.Fail(check);

            int linkSlots = InventoryRules.LinkSlotCount(Db, w);
            LoadoutSpec spec;
            if (overrideLoadout == null)
            {
                if (InventoryRules.CoreEmpty(w.Loadout)) return Result<RunConfig>.Fail(ErrorCode.CoreEmpty);
                spec = InventoryRules.ToSpec(Db, w);
            }
            else
            {
                // 야영에서 연 연습 = 지금 판의 조립(야생 블록 포함) — 서고 수량을 보지 않는다. 코어·칸 수 검사는 한다.
                spec = overrideLoadout.Clone();
                if (string.IsNullOrEmpty(spec.Core)) return Result<RunConfig>.Fail(ErrorCode.CoreEmpty);
            }
            ErrorCode e = ValidateSpec(w, spec, linkSlots);
            if (e != ErrorCode.None) return Result<RunConfig>.Fail(e);

            // 프로필을 바꾸지 않는다(runs_started 그대로, 저장 없음).
            var cfg = BaseConfig(p, w, character, spec, linkSlots, seed);
            cfg.Mode = mode;
            cfg.RunId = string.Empty;
            cfg.StartWave = 1;
            cfg.PhantomFirst = false;
            cfg.RevengePending = false;
            cfg.CrowBundlePending = false;
            if (mode == RunMode.Tutorial)
            {
                cfg.PracticeInfiniteCrystal = true;   // D-59 (다): ④ 반동 사격
                cfg.TutorialStep = 1;
            }
            return Result<RunConfig>.Success(cfg);
        }

        ErrorCode PrepareWand(ProfileData p, int character, long wandId, out WandData w)
        {
            w = null;
            if (!InventoryRules.ValidCharacter(character)) return ErrorCode.InvalidArgument;
            w = p.Wand(wandId);
            if (w == null) return ErrorCode.NotFound;
            if (w.Character != character) return ErrorCode.InvalidArgument;   // 지팡이는 그 캐릭터만
            return ErrorCode.None;
        }

        ErrorCode ValidateSpec(WandData w, LoadoutSpec spec, int linkSlots)
        {
            if (spec == null) return ErrorCode.InvalidLoadout;
            ErrorCode shape = InventoryRules.CheckShape(Db, spec, linkSlots);
            if (shape != ErrorCode.None) return shape;
            return InventoryRules.CheckCompile(_ctx.Rules, Db, w.Character, w.Stage, MutationIds(w), spec, linkSlots);
        }

        static bool StartWaveAllowed(List<int> skips, int startWave)
        {
            if (skips == null) return startWave == 1;
            foreach (int s in skips) if (s + 1 == startWave) return true;
            return false;
        }

        static List<string> MutationIds(WandData w)
        {
            var list = new List<string>();
            if (w.Mutations != null) foreach (var m in w.Mutations) if (m != null && !string.IsNullOrEmpty(m.Id)) list.Add(m.Id);
            return list;
        }

        static RunConfig BaseConfig(ProfileData p, WandData w, int character, LoadoutSpec spec, int linkSlots, ulong seed)
        {
            var rec = p.Character(character);
            return new RunConfig
            {
                Seed = seed,
                CharId = Ids.Char.Of(character),
                WandId = w.Id,
                WandStage = w.Stage,
                LinkSlotCount = linkSlots,
                Mutations = MutationIds(w),
                Loadout = spec.Clone(),
                UnlockedArts = rec != null ? new List<string>(rec.UnlockedArts) : new List<string>(),
                KnownAffinities = new List<string>(p.KnownAffinities ?? new List<string>()),
            };
        }
    }

}
