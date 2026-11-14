using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.ScreensCamp
{
    /// <summary>세션 호출을 순서대로 적는 가짜(StartRun/EndRun/PushSubRun/PopSubRun, InputScreen).</summary>
    sealed class CampFakeSession : IGameSession
    {
        public readonly List<string> Calls = new List<string>();
        public SimWorld Main, Sub;
        public RunConfig LastStart, LastPush;
        public SimWorld World => Sub ?? Main;
        public SimWorld MainWorld => Main;
        public bool HasRun => Main != null;
        public bool HasSubRun => Sub != null;
        public bool Paused { get; set; }
        public float PausedSeconds => 0f;
        ScreenId _input;
        public ScreenId InputScreen { get => _input; set { _input = value; Calls.Add("input " + value); } }
        public void StartRun(RunConfig cfg) { Calls.Add("start"); LastStart = cfg; Main = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), cfg.Seed); }
        public void EndRun() { Calls.Add("end"); Main = null; Sub = null; }
        public void PushSubRun(RunConfig cfg)
        {
            if (Main == null) throw new InvalidOperationException();
            Calls.Add("push"); LastPush = cfg; Sub = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), cfg.Seed);
        }
        public void PopSubRun() { Calls.Add("pop"); Sub = null; }
        public RunSummary LastSummary => null;
        public void SetInputOverride(IInputSource source) { }
        public event Action<SimWorld> RunStarted { add { } remove { } }
        public event Action<SimWorld, SimEvent> SimEventRaised { add { } remove { } }
        public event Action<RunSummary> RunEnded { add { } remove { } }
        public event Action PauseRequested { add { } remove { } }
    }

    /// <summary>IWandService 가짜: BuildPracticeConfig 인자, BuildRunConfig 호출 여부, SetLoadout 대상을 적는다.</summary>
    sealed class CampFakeWands : IWandService
    {
        public int PracticeCalls, RunConfigCalls;
        public int LastCharacter;
        public long LastWandId;
        public RunMode LastMode;
        public LoadoutSpec LastOverride;
        public ErrorCode PracticeError = ErrorCode.None;
        public readonly List<long> SetLoadoutWands = new List<long>();
        public LoadoutData LastSetLoadout;
        public ErrorCode SetLoadoutError = ErrorCode.None;
        public readonly Dictionary<string, int> Free = new Dictionary<string, int>();
        public int Total, Cap;
        public readonly List<WandListEntry> Wands = new List<WandListEntry>();

        public List<WandListEntry> List(int character, WandSort sort) => Wands.FindAll(e => character == 0 || e.Wand.Character == character);
        public Result<WandData> Equip(int character, long wandId) => Result<WandData>.Fail(ErrorCode.NotFound);
        public Result<WandData> SetLoadout(long wandId, LoadoutData loadout)
        {
            SetLoadoutWands.Add(wandId);
            LastSetLoadout = loadout;
            return SetLoadoutError == ErrorCode.None ? Result<WandData>.Success(null) : Result<WandData>.Fail(SetLoadoutError);
        }
        public Result<long> Release(IReadOnlyList<long> wandIds, string typedName) => Result<long>.Fail(ErrorCode.NotFound);
        public long Appraise(WandData wand) => 0;
        public int LibraryFree(string blockId) { int n; return Free.TryGetValue(blockId, out n) ? n : 0; }
        public int LibraryTotal() => Total;
        public int LibraryCap() => Cap;
        public Result<RunConfig> BuildRunConfig(int character, long wandId, int startWave, ulong seed)
        {
            RunConfigCalls++;
            return Result<RunConfig>.Fail(ErrorCode.NotFound);
        }
        public Result<RunConfig> BuildPracticeConfig(int character, long wandId, RunMode mode, LoadoutSpec overrideLoadout, ulong seed)
        {
            PracticeCalls++;
            LastCharacter = character; LastWandId = wandId; LastMode = mode; LastOverride = overrideLoadout;
            if (PracticeError != ErrorCode.None) return Result<RunConfig>.Fail(PracticeError);
            return Result<RunConfig>.Success(new RunConfig { Mode = mode, CharId = Ids.Char.Of(character), WandId = wandId, Seed = seed, Loadout = overrideLoadout });
        }
    }

    sealed class CampFakeSettlement : ISettlementService
    {
        public BagLimits Limits;
        public int LastCleared = -1;
        public BagLimits BagLimitsFor(int clearedDirect) { LastCleared = clearedDirect; return Limits; }
        public bool IsValidStartSkip(int bestWave, int skip) => StartSkipOptions(bestWave).Contains(skip);
        public List<int> StartSkipOptions(int bestWave) => new List<int>();
        public SettlementView Prepare(RunSummary summary) => null;
        public Result<SettlementResult> Commit(RunSummary summary, IReadOnlyList<int> bagUids) => Result<SettlementResult>.Fail(ErrorCode.NotFound);
        public List<int> AutoPick(SettlementView view) => new List<int>();
        public void SaveCheckpoint(RunSummary summary) { }
        public SettlementResult SettlePendingCheckpoint() => null;
        public void RecordArtUnlock(int character, string artId) { }
    }

    /// <summary>호출을 세는 TraitModule. 문맥에 표식 값을 넣어 컴파일 문맥이 이 함수에서 왔는지 가린다.</summary>
    sealed class CampRecordingTraits : TraitModule
    {
        public int ComputeCalls, ContextCalls;
        public int LastStage = -1, LastLinks = -1;
        public CharacterDef LastChar;
        public override PlayerDerived ComputeDerived(IContentDb db, CharacterDef ch, int effectiveStage, IReadOnlyList<string> mutations)
        {
            ComputeCalls++;
            LastChar = ch;
            LastStage = effectiveStage;
            var d = base.ComputeDerived(db, ch, effectiveStage, mutations);
            d.DepthDecay = 0.5f;
            return d;
        }
        public override SpellCompileContext CompileContext(in PlayerDerived derived, int linkSlotCount)
        {
            ContextCalls++;
            LastLinks = linkSlotCount;
            return new SpellCompileContext { LinkSlotCount = linkSlotCount, Underline = true, DepthDecay = derived.DepthDecay * 0.5f };
        }
    }

    sealed class CampRecordingSpells : SpellModule
    {
        public int CompileCalls, StatsCalls;
        public SpellCompileContext LastCtx;
        public string LastCore;
        public PlayerDerived LastDerived;
        public SpellStats Stats;
        public override CompiledSpell Compile(IContentDb db, string core, IReadOnlyList<string> links, in SpellCompileContext ctx)
        {
            CompileCalls++;
            LastCtx = ctx;
            LastCore = core;
            return new CompiledSpell { IsValid = true };
        }
        public override SpellStats ComputeStats(IContentDb db, CompiledSpell spell, CharacterDef ch, in PlayerDerived derived)
        {
            StatsCalls++;
            LastDerived = derived;
            return Stats;
        }
    }

    sealed class CampFixedHealRun : RunModule
    {
        public int Cost;
        public override int HealCost(SimWorld w) => Cost;
    }

    sealed class CampFakeArts : ArtModule
    {
        public List<ArtTrieNodeView> Views = new List<ArtTrieNodeView>();
        public override List<ArtTrieNodeView> DescribeTrie(IContentDb db, string charId, ICollection<string> unlocked, ICollection<string> candidates) => Views;
    }

    static class CampTestKit
    {
        public static GameContext Ctx(CampFakeSession session, CampFakeWands wands, ProfileData profile = null)
        {
            var store = new CampFakeProfiles { Current = profile ?? NewProfile() };
            return new GameContext { Content = TestContent.Db, Names = TestContent.Names, Session = session, Wands = wands, Profiles = store, Rules = SimModuleSet.Stubs() };
        }

        public static ProfileData NewProfile()
        {
            var p = new ProfileData { LastCharacter = 2 };
            for (int c = 1; c <= Ids.Char.Count; c++)
            {
                p.Characters.Add(new CharacterRecord { Id = c, EquippedWandId = 10 + c });
                p.Wands.Add(new WandData { Id = 10 + c, Character = c, Loadout = new LoadoutData { Core = Ids.Block.MagicBolt } });
            }
            return p;
        }

        /// <summary>판 중 세계: 슬롯 0 플레이어, 코어 + 연결 4칸.</summary>
        public static SimWorld World(SimModuleSet mods = null, string charId = Ids.Char.C1)
        {
            var w = new SimWorld(TestContent.Db, mods ?? SimModuleSet.Stubs(), 7);
            var p = w.Players[0];
            p.Active = true;
            p.CharId = charId;
            p.Def = TestContent.Db.Character(charId);
            p.Wand.WandId = 11;
            p.Loadout = new RunLoadout { Core = new SlotItem { BlockId = Ids.Block.MagicBolt }, Links = new SlotItem[4] };
            p.MaxHp = 4 * SimConst.HpPerCell;
            p.Hp = 2 * SimConst.HpPerCell;
            w.Run.Phase = RunPhase.Camp;
            return w;
        }
    }

    sealed class CampFakeProfiles : IProfileStore
    {
        public ProfileData Current { get; set; }
        public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
        public void Save() { }
        public string Directory => string.Empty;
        public bool RestorePrevious() => false;
        public event Action Saved { add { } remove { } }
    }

    sealed class CampFakeScreens : IScreenManager
    {
        public readonly List<ScreenId> Registered = new List<ScreenId>();
        public void Register(ScreenId id, Func<IScreen> factory) { Registered.Add(id); }
        public void Push(ScreenId id, ScreenArgs args = null) { }
        public void Replace(ScreenId id, ScreenArgs args = null) { }
        public void Pop(object result = null) { }
        public void PopTo(ScreenId id, ScreenArgs args = null) { }
        public ScreenId Top => ScreenId.None;
        public bool IsOpen(ScreenId id) => false;
        public void Close(IScreen screen, object result = null) { }
        public void Confirm(string titleKey, string bodyKey, bool caution, Action<bool> onResult) { }
        public void ConfirmText(string titleKey, string bodyText, bool caution, Action<bool> onResult) { }
        public void Toast(string text, float seconds = 3f) { }
        public void ShowTooltip(string text, UnityEngine.RectTransform anchor) { }
        public void HideTooltip(UnityEngine.RectTransform anchor = null) { }
        public bool CaptureKeys { get; set; }
    }
}
