using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.ScreensPrep
{
    /// <summary>저장 횟수를 세는 가짜 프로필 저장소.</summary>
    public sealed class FakeProfileStore : IProfileStore
    {
        public ProfileData Data = new ProfileData();
        public int SaveCount;
        public ProfileData Current => Data;
        public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
        public void Save() { SaveCount++; Saved?.Invoke(); }
        public string Directory => string.Empty;
        public bool RestorePrevious() => false;
        public event Action Saved;
    }

    /// <summary>
    /// 가짜 지팡이 서비스. List는 프로필의 순서 그대로(정렬하지 않는다 — 화면 모델이 순서 규칙을 거는지 본다).
    /// BuildRunConfig의 인자를 적어 두고, Fail이 None이 아니면 그 오류를 돌려준다.
    /// </summary>
    public sealed class FakeWandService : IWandService
    {
        readonly FakeProfileStore _store;
        public FakeWandService(FakeProfileStore store) { _store = store; }

        public readonly HashSet<long> LockedIds = new HashSet<long>();
        public ErrorCode Fail = ErrorCode.None;
        public int BuildCalls;
        public int LastCharacter;
        public long LastWandId;
        public int LastStartWave;
        public ulong LastSeed;
        public int EquipCalls;
        /// <summary>false면 줄의 CoreEmpty를 늘 false로 준다(모델이 조립을 직접 보는지).</summary>
        public bool ReportCoreEmpty = true;

        public List<WandListEntry> List(int character, WandSort sort)
        {
            var p = _store.Data;
            var list = new List<WandListEntry>();
            foreach (var w in p.Wands)
            {
                if (character != 0 && w.Character != character) continue;
                var rec = p.Character(w.Character);
                list.Add(new WandListEntry
                {
                    Wand = w,
                    Equipped = rec != null && rec.EquippedWandId == w.Id,
                    Locked = LockedIds.Contains(w.Id),
                    CoreEmpty = ReportCoreEmpty && (w.Loadout == null || string.IsNullOrEmpty(w.Loadout.Core)),
                });
            }
            return list;
        }

        public Result<WandData> Equip(int character, long wandId)
        {
            EquipCalls++;
            var p = _store.Data;
            var w = p.Wand(wandId);
            var rec = p.Character(character);
            if (w == null || rec == null) return Result<WandData>.Fail(ErrorCode.NotFound);
            if (w.Character != character) return Result<WandData>.Fail(ErrorCode.NotEligible);
            rec.EquippedWandId = wandId;
            _store.Save();
            return Result<WandData>.Success(w);
        }

        public Result<WandData> SetLoadout(long wandId, LoadoutData loadout) => Result<WandData>.Fail(ErrorCode.NotFound);
        public Result<long> Release(IReadOnlyList<long> wandIds, string typedName) => Result<long>.Fail(ErrorCode.NotFound);
        public long Appraise(WandData wand) => 0;
        public int LibraryFree(string blockId) => 0;
        public int LibraryTotal() => 0;
        public int LibraryCap() => 0;

        public Result<RunConfig> BuildRunConfig(int character, long wandId, int startWave, ulong seed)
        {
            BuildCalls++;
            LastCharacter = character;
            LastWandId = wandId;
            LastStartWave = startWave;
            LastSeed = seed;
            if (Fail != ErrorCode.None) return Result<RunConfig>.Fail(Fail);
            return Result<RunConfig>.Success(new RunConfig
            {
                Mode = RunMode.Endless,
                Seed = seed,
                CharId = Ids.Char.Of(character),
                WandId = wandId,
                StartWave = startWave,
            });
        }

        public Result<RunConfig> BuildPracticeConfig(int character, long wandId, RunMode mode, LoadoutSpec overrideLoadout, ulong seed) =>
            Result<RunConfig>.Fail(ErrorCode.NotFound);
    }

    /// <summary>
    /// 가짜 정산 서비스: StartSkipOptions만 쓴다. 규칙은 01 요약서 4.6절(08 5.5절 AllowedSkips)을 그대로 옮긴 것 —
    /// 화면 모델이 서비스가 준 선택지를 그대로 보여 주는지만 본다(01 T080~T085 "Settlement 가짜로 선택지 표시만").
    /// </summary>
    public sealed class FakeSettlementService : ISettlementService
    {
        public int LastBestWave = -1;

        public bool IsValidStartSkip(int bestWave, int skip) => StartSkipOptions(bestWave).Contains(skip);
        public List<int> StartSkipOptions(int bestWave)
        {
            LastBestWave = bestWave;
            var list = new List<int> { 0 };
            for (int s = 10; s * 10 <= bestWave * 7; s += 10) list.Add(s);
            return list;
        }

        public BagLimits BagLimitsFor(int clearedDirect) => default;
        public SettlementView Prepare(RunSummary summary) => null;
        public Result<SettlementResult> Commit(RunSummary summary, IReadOnlyList<int> bagUids) => Result<SettlementResult>.Fail(ErrorCode.NotFound);
        public List<int> AutoPick(SettlementView view) => new List<int>();
        public void SaveCheckpoint(RunSummary summary) { }
        public SettlementResult SettlePendingCheckpoint() => null;
        public void RecordArtUnlock(int character, string artId) { }
    }

    /// <summary>01 요약서 6.3절 SkipScore 식을 그대로 옮긴 가짜 점수 규칙(B(w)의 상수도 그 절의 식).</summary>
    public sealed class FakeScoreRules : ScoreRules
    {
        public override int SkipScore(IContentDb db, int wave)
        {
            int b = wave <= 12 ? 10 + 6 * wave : Math.Min(200, 82 + 4 * (wave - 12));
            int baseScore = wave % 10 == 0 ? 3200 : b * 10 + 200;
            return baseScore * (9 + wave) * 6 / 100;
        }
    }

    public static class PrepFixture
    {
        /// <summary>캐릭터 5명(지팡이 없음) 프로필 + 가짜 서비스가 든 문맥.</summary>
        public static GameContext Make(out FakeProfileStore store, out FakeWandService wands, out FakeSettlementService settle)
        {
            store = new FakeProfileStore();
            for (int i = 1; i <= Ids.Char.Count; i++) store.Data.Characters.Add(new CharacterRecord { Id = i });
            store.Data.Flags.TutorialDummyDone = true;
            wands = new FakeWandService(store);
            settle = new FakeSettlementService();
            var rules = SimModuleSet.Stubs();
            rules.Score = new FakeScoreRules();
            return new GameContext
            {
                Profiles = store,
                Wands = wands,
                Settlement = settle,
                Rules = rules,
                Content = TestContent.Db,
                Names = TestContent.Names,
            };
        }

        public static WandData AddWand(FakeProfileStore store, int character, long id, int stage, bool held, string core = Ids.Block.MagicBolt,
            string acquiredAt = "2026-01-01T00:00:00Z")
        {
            var w = new WandData { Id = id, Character = character, Stage = stage, AcquiredAt = acquiredAt };
            w.Loadout.Core = core;
            store.Data.Wands.Add(w);
            if (held) store.Data.Character(character).EquippedWandId = id;
            return w;
        }
    }
}
