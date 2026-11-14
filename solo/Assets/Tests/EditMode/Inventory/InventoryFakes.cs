using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Inventory;
using Wizard.Solo.Meta.Shop;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.Inventory
{
    sealed class InvClock : IClock
    {
        public DateTime Value;
        public InvClock(DateTime value) { Value = value; }
        public DateTime Now => Value;
        public DateTime UtcNow => Value.ToUniversalTime();
    }

    /// <summary>메모리 프로필 저장소. Save 횟수를 센다.</summary>
    sealed class MemoryProfileStore : IProfileStore
    {
        public ProfileData Current { get; set; }
        public int SaveCount;
        public void LoadOrCreate(out bool created, out bool restored) { created = false; restored = false; }
        public void Save() { SaveCount++; Saved?.Invoke(); }
        public string Directory => string.Empty;
        public bool RestorePrevious() => false;
        public event Action Saved;
    }

    /// <summary>시작 웨이브 선택지만 주는 정산 가짜.</summary>
    sealed class FakeSettlement : ISettlementService
    {
        public List<int> Skips = new List<int> { 0 };
        public int LastBest = -1;
        public BagLimits BagLimitsFor(int clearedDirect) => default;
        public bool IsValidStartSkip(int bestWave, int skip) => StartSkipOptions(bestWave).Contains(skip);
        public List<int> StartSkipOptions(int bestWave) { LastBest = bestWave; return new List<int>(Skips); }
        public SettlementView Prepare(RunSummary summary) => null;
        public Result<SettlementResult> Commit(RunSummary summary, IReadOnlyList<int> bagUids) => Result<SettlementResult>.Fail(ErrorCode.NotFound);
        public List<int> AutoPick(SettlementView view) => new List<int>();
        public void SaveCheckpoint(RunSummary summary) { }
        public SettlementResult SettlePendingCheckpoint() => null;
        public void RecordArtUnlock(int character, string artId) { }
    }

    /// <summary>본판 세계만 들고 있는 세션 가짜(원정 중 잠금 확인용).</summary>
    sealed class FakeSession : IGameSession
    {
        public SimWorld Main;
        public SimWorld World => Main;
        public SimWorld MainWorld => Main;
        public bool HasRun => Main != null;
        public bool HasSubRun => false;
        public bool Paused { get; set; }
        public float PausedSeconds => 0f;
        public ScreenId InputScreen { get; set; }
        public void StartRun(RunConfig cfg) { }
        public void EndRun() { Main = null; }
        public void PushSubRun(RunConfig cfg) { }
        public void PopSubRun() { }
        public RunSummary LastSummary => null;
        public void SetInputOverride(IInputSource source) { }
        public event Action<SimWorld> RunStarted { add { } remove { } }
        public event Action<SimWorld, SimEvent> SimEventRaised { add { } remove { } }
        public event Action<RunSummary> RunEnded { add { } remove { } }
        public event Action PauseRequested { add { } remove { } }
    }

    /// <summary>호출을 적어 두는 TraitModule(컴파일 문맥이 Traits.CompileContext에서 오는지 본다).</summary>
    sealed class RecordingTraits : TraitModule
    {
        public int ComputeCalls, ContextCalls;
        public CharacterDef LastChar;
        public int LastStage = -1;
        public List<string> LastMutations;
        public int LastLinkSlotCount = -1;
        public SpellCompileContext Returned;

        public override PlayerDerived ComputeDerived(IContentDb db, CharacterDef ch, int effectiveStage, IReadOnlyList<string> mutations)
        {
            ComputeCalls++;
            LastChar = ch;
            LastStage = effectiveStage;
            LastMutations = new List<string>(mutations);
            var d = base.ComputeDerived(db, ch, effectiveStage, mutations);
            d.Underline = true;
            d.DepthDecay = 0.5f;   // 구현 값과 다른 표식 값: 문맥이 이 함수에서 온 것인지 가린다
            return d;
        }

        public override SpellCompileContext CompileContext(in PlayerDerived derived, int linkSlotCount)
        {
            ContextCalls++;
            LastLinkSlotCount = linkSlotCount;
            Returned = new SpellCompileContext { LinkSlotCount = linkSlotCount, Underline = derived.Underline, DepthDecay = derived.DepthDecay * 0.5f };
            return Returned;
        }
    }

    /// <summary>받은 문맥을 적고, 정해 둔 진단을 돌려주는 SpellModule.</summary>
    sealed class RecordingSpells : SpellModule
    {
        public int Calls;
        public SpellCompileContext LastCtx;
        public string LastCore;
        public List<string> LastLinks;
        public List<SpellDiagCode> Diagnose = new List<SpellDiagCode>();

        public override CompiledSpell Compile(IContentDb db, string core, IReadOnlyList<string> links, in SpellCompileContext ctx)
        {
            Calls++;
            LastCtx = ctx;
            LastCore = core;
            LastLinks = new List<string>(links);
            var s = new CompiledSpell();
            foreach (var c in Diagnose) s.Diagnostics.Add(new SpellDiagnostic { Code = c, Slot = 0 });
            s.IsValid = s.Diagnostics.TrueForAll(d => !d.IsError);
            return s;
        }
    }

    /// <summary>테스트 문맥 한 벌: 실제 데이터(TestContent) + 새 프로필 + 메모리 저장소 + 고정 시계.</summary>
    sealed class InvFixture
    {
        public GameContext Ctx;
        public MemoryProfileStore Store;
        public InvClock Clock;
        public FakeSettlement Settlement;
        public FakeSession Session;
        public WandService Wands;
        public ShopService Shop;

        public ProfileData P => Store.Current;
        public IContentDb Db => Ctx.Content;

        /// <summary>2026-11-11(수) 12:00 현지.</summary>
        public static readonly DateTime Wednesday = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Local);

        public InvFixture(DateTime? now = null)
        {
            Clock = new InvClock(now ?? Wednesday);
            Store = new MemoryProfileStore();
            Settlement = new FakeSettlement();
            Session = new FakeSession();
            Ctx = new GameContext
            {
                Content = TestContent.Db,
                Names = TestContent.Names,
                Clock = Clock,
                Profiles = Store,
                Rules = SimModuleSet.Stubs(),
                Settlement = Settlement,
                Session = Session,
            };
            Store.Current = Wizard.Solo.Save.NewProfile.Create(Ctx.Content, Clock);
            Wands = new WandService(Ctx);
            Shop = new ShopService(Ctx);
            Ctx.Wands = Wands;
            Ctx.Shop = Shop;
        }

        public WandData Starter(int character) => P.Wands.Find(w => w.Character == character && w.Origin == "Starter");

        /// <summary>프로필에 지팡이를 직접 더한다(상태를 세우는 용도).</summary>
        public WandData AddWand(int character, int stage, string acquiredAt = null)
        {
            var w = InventoryRules.NewShopWand(Db, P, character, acquiredAt ?? "2026-01-01T00:00:00+09:00");
            w.Stage = stage;
            w.FloorPeak = stage;
            w.RecordPeak = stage;
            P.Wands.Add(w);
            return w;
        }

        public void AddLibrary(string blockId, int qty) => P.Item(blockId).Qty += qty;

        /// <summary>그 지팡이로 원정 중인 본판을 세운다.</summary>
        public SimWorld StartExpedition(long wandId)
        {
            var w = new SimWorld(Db, SimModuleSet.Stubs());
            w.Run.Config = new RunConfig { Mode = RunMode.Endless, WandId = wandId };
            w.Run.Phase = RunPhase.WavePrelude;
            Session.Main = w;
            return w;
        }
    }
}
