using System;
using NUnit.Framework;
using Wizard.Solo.Meta;
using Wizard.Solo.Meta.Enhance;
using Wizard.Solo.Sim;
using Wizard.Solo.UI;

namespace Wizard.Solo.Tests.Enhance
{
    /// <summary>본판 세계만 들고 있는 세션(원정 중 잠금을 보는 데 필요한 만큼).</summary>
    sealed class EnhanceFakeSession : IGameSession
    {
        public SimWorld Main;
        public SimWorld World => Main;
        public SimWorld MainWorld => Main;
        public bool HasRun => Main != null;
        public bool HasSubRun => false;
        public bool Paused { get; set; }
        public float PausedSeconds => 0;
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

    /// <summary>07 요약서 4.1절 전제 3번(원정 중인 지팡이), 등록 자리(00 문서 2.1절), 보충 수치 파일.</summary>
    public class EnhanceLockAndRegistrationTests
    {
        EnhanceKit _kit;
        EnhanceFakeSession _session;

        [SetUp]
        public void SetUp()
        {
            _kit = new EnhanceKit();
            _kit.Rich();
            _session = new EnhanceFakeSession();
            _kit.Ctx.Session = _session;
        }

        void StartRunWith(WandData w, RunMode mode, RunPhase phase)
        {
            var world = new SimWorld(_kit.Ctx.Content, SimModuleSet.Stubs());
            world.Run.Config = new RunConfig { Mode = mode, WandId = w.Id, CharId = Ids.Char.Of(w.Character) };
            world.Run.Phase = phase;
            _session.Main = world;
        }

        [Test]
        public void S07_4_1__WandOnExpeditionIsLocked()
        {
            WandData w = _kit.Set(8, 8, 0);
            _kit.SetMutations(w, Ids.Mutation.Gluttony);
            StartRunWith(w, RunMode.Endless, RunPhase.WaveCombat);
            Assert.AreEqual(ErrorCode.WandLocked, _kit.Service.Enhance(w.Id, null, false).Error);
            Assert.AreEqual(ErrorCode.WandLocked, _kit.Preview(w).Blocked);
            Assert.AreEqual(ErrorCode.WandLocked, _kit.Service.CarveSlot(w.Id).Error);
            Assert.AreEqual(ErrorCode.WandLocked, _kit.Service.PreviewCarve(w.Id).Blocked);
            Assert.AreEqual(ErrorCode.WandLocked, _kit.Service.RemoveMutation(w.Id, Ids.Mutation.Gluttony).Error);
            Assert.AreEqual(0, _kit.Store.SaveCount, "nothing changed");

            // 다른 지팡이는 잠기지 않는다(그 한 자루만 원정 중이다).
            Assert.AreEqual(ErrorCode.None, _kit.Preview(_kit.Set(8, 8, 0, character: 2)).Blocked);
        }

        [Test]
        public void S07_4_1__LockEndsWithTheRun()
        {
            WandData w = _kit.Set(8, 8, 0);
            StartRunWith(w, RunMode.Endless, RunPhase.RunEnded);   // 판이 끝나 정산을 기다리는 동안
            Assert.AreEqual(ErrorCode.None, _kit.Preview(w).Blocked);
            _session.EndRun();
            Assert.AreEqual(ErrorCode.None, _kit.Preview(w).Blocked);
            StartRunWith(w, RunMode.Practice, RunPhase.Practice);  // 연습 방은 원정이 아니다
            Assert.AreEqual(ErrorCode.None, _kit.Preview(w).Blocked);
        }

        [Test]
        public void S07_4_1__PendingOfferCannotBeResolvedWhileOnExpedition()
        {
            // 결정 대기 중인 지팡이로도 원정은 갈 수 있다(판 중엔 변이 없는 상태로 싸운다). 결정은 돌아와서 한다.
            WandData w = _kit.Set(8, 8, 0);
            _kit.Mutate(w);
            StartRunWith(w, RunMode.Endless, RunPhase.Camp);
            Assert.AreEqual(ErrorCode.WandLocked, _kit.Service.ResolveMutation(w.Id, w.PendingOffer.AttemptId, true).Error);
            _session.EndRun();
            Assert.AreEqual(ErrorCode.None, _kit.Service.ResolveMutation(w.Id, w.PendingOffer.AttemptId, true).Error);
        }

        [Test]
        public void S00_2_1__MetaFactoryRegistersEnhanceService()
        {
            var ctx = new GameContext { Content = _kit.Ctx.Content, Profiles = _kit.Store, Clock = _kit.Ctx.Clock };
            MetaFactory.Fill(ctx);
            Assert.IsInstanceOf<EnhanceService>(ctx.Enhance);
            // 등록된 서비스는 기기 난수로 돈다: 같은 규칙(+1 도전은 반드시 성공).
            ctx.Profile.Currencies.Gold = 100;
            ctx.Profile.Item(Ids.Item.StoneLow).Qty = 1;
            long wandId = ctx.Profile.Character(3).EquippedWandId;
            Result<EnhanceResult> r = ctx.Enhance.Enhance(wandId, null, false);
            Assert.AreEqual(ErrorCode.None, r.Error);
            Assert.AreEqual(EnhanceOutcome.Success, r.Value.Outcome);
            Assert.That(r.Value.RollBp, Is.InRange(0, 9999));
            Assert.AreEqual(1, ctx.Profile.Wand(wandId).Stage);
        }

        [Test]
        public void S07_2__SupplementFileValues()
        {
            // DataSolo/G12.json: 요약서 값 그대로(4.9절 흔적 2조각, 6절 상한 1,000, 5.4절 0.3초·0.5초·0.3초·0.8초·stage 9, 4.11절 2초).
            EnhanceTuning t = EnhanceTuning.From(_kit.Ctx.Content);
            Assert.AreEqual(2, t.ScarMax);
            Assert.AreEqual(1000, t.OwnedForecastMaxAttempts);
            Assert.AreEqual(18, t.SkipIgnoreTicks);
            Assert.AreEqual(30, t.ShortResultTicks);
            Assert.AreEqual(18, t.FastChargeTicks);
            Assert.AreEqual(48, t.FastAttemptTicks);
            Assert.AreEqual(9, t.FastFullRitualMinStage);
            Assert.AreEqual(120, t.Plus13FirstLockTicks);
            Assert.IsTrue(t.AutoStopOnDropDefault);
            Assert.Greater(t.LogTailMax, 0);
            Assert.AreEqual(0, EnhanceTuning.FromJson(null).ScarMax, "missing file: zeros, no crash");
        }

        [Test]
        public void S07_2__EnhanceConfigValuesUsedByTheRules()
        {
            // solo/enhance.json(07 요약서 2절)의 값이 규칙이 기대하는 꼴로 읽힌다.
            var c = _kit.Cfg;
            Assert.AreEqual(95, c.ProbCapPct);
            Assert.AreEqual(15, c.GreatRatioPct);
            Assert.AreEqual(25, c.MutationRatioPct);
            Assert.AreEqual(15, c.AuraStepPct);
            Assert.AreEqual(3, c.AuraMax);
            Assert.AreEqual(3, c.DropSafeMaxStage);
            Assert.AreEqual(2, c.FloorGap);
            Assert.AreEqual(100, c.CostCoeff);
            CollectionAssert.AreEqual(new[] { 4, 8 }, c.StoneTierBounds);
            Assert.AreEqual(1, c.StonesPerAttempt);
            Assert.AreEqual(3, c.SlotExtraMax);
            Assert.AreEqual(13, c.MaxStage);
            Assert.AreEqual(5, c.Sacrifice.MaxBlocks);
            Assert.AreEqual(4, c.Sacrifice.ScoreCommon);
            Assert.AreEqual(8, c.Sacrifice.ScoreRare);
            Assert.AreEqual(14, c.Sacrifice.ScoreHero);
            Assert.AreEqual(5, c.Sacrifice.SameElement);
            Assert.AreEqual(40, c.Sacrifice.Cap);
            Assert.AreEqual(7, c.Carve.MinStage);
            Assert.AreEqual(3000, c.MutationRemoveGold);
            CollectionAssert.AreEqual(new[] { 4, 7, 10, 13 }, c.Milestones);
            // 변이 상한: 2개, char.2만 3개(characters.json max_mutations).
            int[] cap = { 2, 3, 2, 2, 2 };
            for (int n = 1; n <= 5; n++) Assert.AreEqual(cap[n - 1], EnhanceRules.MutationCap(c, _kit.Ctx.Content, n), "char " + n);
        }
    }
}
