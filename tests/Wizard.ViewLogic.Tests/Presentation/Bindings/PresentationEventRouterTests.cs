using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Sim.Events;
using Wizard.ViewLogic.Presentation;
using Xunit;
using static Wizard.ViewLogic.Tests.Presentation.Bindings.EventBindingTableTests;

namespace Wizard.ViewLogic.Tests.Presentation.Bindings
{
    // CONTRACTS 13절 PresentationEventRouter, 6.4절 표시 이벤트의 단계, 0.3절 T-50 연속 스침
    public sealed class PresentationEventRouterTests
    {
        private sealed class FxLog : IFxPlayer
        {
            public readonly List<FxRequest> Played = new List<FxRequest>();
            public void Play(in FxRequest request) => Played.Add(request);
            public void StopAll() { }
        }

        private sealed class SfxLog : ISfxPlayer
        {
            public readonly List<(string Id, int Pitch)> Played = new List<(string, int)>();
            public void Play(string sfxId, int pitchStep = 0, float volumeDb = 0f) => Played.Add((sfxId, pitchStep));
            public void SetBus(SfxBus bus, float volume) { }
        }

        private sealed class CameraLog : ICameraCueSink
        {
            public readonly List<BindingShake> Shakes = new List<BindingShake>();
            public readonly List<(int Ms, EntityId Source, EntityId Target)> Hitstops = new List<(int, EntityId, EntityId)>();
            public void Shake(BindingShake size) => Shakes.Add(size);
            public void Hitstop(int durationMs, EntityId source, EntityId target) => Hitstops.Add((durationMs, source, target));
        }

        private readonly FxLog _fx = new FxLog();
        private readonly SfxLog _sfx = new SfxLog();
        private readonly CameraLog _camera = new CameraLog();
        private readonly List<string> _unbound = new List<string>();

        private static readonly string Hit = SimEventDocs.BindingKey(SimEventType.PlayerHit);
        private static readonly string Lock = SimEventDocs.BindingKey(SimEventType.HeatLockEntered);
        private static readonly string Art = SimEventDocs.BindingKey(SimEventType.DuelArtActivated);
        private static readonly string Graze = SimEventDocs.BindingKey(SimEventType.GrazeAwarded);

        private PresentationEventRouter Router()
        {
            string lines =
                Line(Hit, "\"fx_lock_x\"", "\"sfx_c07\"", "medium", 120, true) + ", " +
                Line(Lock, "\"fx_lock_x\"", "\"sfx_c07\"", "none", 0, true) + ", " +
                Line(Art, "\"fx_graze_ring\"", "\"sfx_t09a\"", "small", 0, false) + ", " +
                Line(Graze, "\"fx_graze_ring\"", "\"sfx_c10\"", "none", 0, true) + ", " +
                Line(UiEventKeys.DuelMatchFound, "\"fx_graze_ring\"", "\"sfx_c10\"", "large", 50, false);
            string none = "{ \"none\": [ { \"event\": \"sim.none\", \"reason\": \"not_an_event\" }, { \"event\": \"duel.hit_predicted\", \"reason\": \"phase_of_core_event\" } ] }";
            var errors = new List<string>();
            EventBindingTable t = EventBindingTable.Load(BindingsJson(lines), none, errors);
            Assert.Empty(errors);
            return new PresentationEventRouter(t, _fx, _sfx, _camera, k => _unbound.Add(k));
        }

        private static PresentationEvent Ev(SimEventType type, EventPhase phase, uint tick = 10, byte slot = 0, ushort ordinal = 0, long x = 0, long y = 0)
        {
            return new PresentationEvent
            {
                Event = new SimEvent { Type = type, Slot = new PlayerSlot(slot), Tick = tick, A = 7, B = 9, X = x, Y = y, Source = new EntityId(3), Target = new EntityId(4) },
                Phase = phase,
                Ordinal = ordinal,
            };
        }

        private void Handle(PresentationEventRouter r, PresentationEvent e) => r.Handle(e, null!);

        [Fact]
        public void ConfirmedOnlyLineIsSilentAndInvisibleWhenPredicted()
        {
            // 수락 3: confirmed_only 줄은 Predicted 단계에서 소리와 피격 연출을 내지 않는다
            PresentationEventRouter r = Router();
            Handle(r, Ev(SimEventType.PlayerHit, EventPhase.Predicted));
            Assert.Empty(_fx.Played);
            Assert.Empty(_sfx.Played);
            Assert.Empty(_camera.Shakes);
            Assert.Empty(_camera.Hitstops);
            Handle(r, Ev(SimEventType.PlayerHit, EventPhase.Retracted));
            Assert.Empty(_fx.Played);
            Assert.Empty(_sfx.Played);

            Handle(r, Ev(SimEventType.PlayerHit, EventPhase.Confirmed));
            Assert.Single(_fx.Played);
            Assert.Equal(EventPhase.Confirmed, _fx.Played[0].Phase);
            Assert.Equal(new[] { ("sfx_c07", 0) }, _sfx.Played);
            Assert.Equal(new[] { BindingShake.Medium }, _camera.Shakes);
            Assert.Equal(new[] { (120, new EntityId(3), new EntityId(4)) }, _camera.Hitstops);
        }

        [Fact]
        public void NonConfirmedOnlyLineDrawsPredictedButSoundsOnlyOnConfirm()
        {
            // 비기 발동 문양: Predicted 에서 그리고, 확정되면 이어서 그린다(다시 내지 않는다). 소리·흔들림은 확정에서만
            PresentationEventRouter r = Router();
            Handle(r, Ev(SimEventType.DuelArtActivated, EventPhase.Predicted, tick: 20, slot: 1));
            Assert.Single(_fx.Played);
            Assert.Equal(EventPhase.Predicted, _fx.Played[0].Phase);
            Assert.Empty(_sfx.Played);
            Assert.Empty(_camera.Shakes);

            Handle(r, Ev(SimEventType.DuelArtActivated, EventPhase.Confirmed, tick: 20, slot: 1));
            Assert.Single(_fx.Played);
            Assert.Equal(new[] { ("sfx_t09a", 0) }, _sfx.Played);
            Assert.Equal(new[] { BindingShake.Small }, _camera.Shakes);
        }

        [Fact]
        public void RetractedPredictionFadesOnceAndNeverSounds()
        {
            PresentationEventRouter r = Router();
            Handle(r, Ev(SimEventType.DuelArtActivated, EventPhase.Predicted, tick: 20));
            Handle(r, Ev(SimEventType.DuelArtActivated, EventPhase.Retracted, tick: 20));
            Assert.Equal(2, _fx.Played.Count);
            Assert.Equal(EventPhase.Retracted, _fx.Played[1].Phase);
            Assert.Empty(_sfx.Played);
            // 같은 열쇠의 두 번째 번복은 아무것도 내지 않는다
            Handle(r, Ev(SimEventType.DuelArtActivated, EventPhase.Retracted, tick: 20));
            Assert.Equal(2, _fx.Played.Count);
        }

        [Fact]
        public void PredictionKeyIsTickTypeSlotOrdinal()
        {
            // 열쇠가 다르면(순번이 다르면) 확정은 새로 그린다
            PresentationEventRouter r = Router();
            Handle(r, Ev(SimEventType.DuelArtActivated, EventPhase.Predicted, tick: 20, ordinal: 0));
            Handle(r, Ev(SimEventType.DuelArtActivated, EventPhase.Confirmed, tick: 20, ordinal: 1));
            Assert.Equal(2, _fx.Played.Count);
            Assert.Equal(EventPhase.Confirmed, _fx.Played[1].Phase);
            // 남은 예측(순번 0)은 번복되면 흐려진다
            Handle(r, Ev(SimEventType.DuelArtActivated, EventPhase.Retracted, tick: 20, ordinal: 0));
            Assert.Equal(EventPhase.Retracted, _fx.Played[2].Phase);
        }

        [Fact]
        public void ConfirmedWithoutPredictionDraws()
        {
            // 무한 웨이브는 모든 이벤트가 곧바로 Confirmed 다(CONTRACTS 6.4)
            PresentationEventRouter r = Router();
            Handle(r, Ev(SimEventType.HeatLockEntered, EventPhase.Confirmed, x: Fix64.FromInt(3).Raw, y: Fix64.FromInt(2).Raw));
            Assert.Single(_fx.Played);
            FxRequest f = _fx.Played[0];
            Assert.Equal("fx_lock_x", f.FxId);
            Assert.Equal(3f, f.WorldPos.X);
            Assert.Equal(-2f, f.WorldPos.Y);            // FixConvert.ToWorld: y 뒤집기
            Assert.Equal(7, f.A);
            Assert.Equal(9, f.B);
            Assert.Equal(Element.None, f.Element);
            Assert.Equal(new[] { ("sfx_c07", 0) }, _sfx.Played);
        }

        [Fact]
        public void GrazeChainPitchClimbsWithinWindowAndCapsAtFive()
        {
            // 수락 6: Confirmed 인 GrazeAwarded 가 앞 것의 2초 안이면 한 단씩 5단까지, 아니면 0. 코어의 C 는 0이다
            PresentationEventRouter r = Router();
            uint[] ticks = { 100, 160, 220, 340, 400, 460, 520, 580 };   // 60·60·120(경계 포함)·60·60·60·60 틱 간격
            foreach (uint t in ticks)
            {
                Handle(r, Ev(SimEventType.GrazeAwarded, EventPhase.Confirmed, tick: t));
            }
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 5, 5 }, _sfx.Played.ConvertAll(p => p.Pitch));
            Assert.All(_sfx.Played, p => Assert.Equal("sfx_c10", p.Id));
            Assert.Equal(5, r.GrazeChainStep(new PlayerSlot(0)));

            Handle(r, Ev(SimEventType.GrazeAwarded, EventPhase.Confirmed, tick: 580 + 121));   // 2초 넘음 → 0
            Assert.Equal(0, _sfx.Played[_sfx.Played.Count - 1].Pitch);
        }

        [Fact]
        public void GrazeChainIsCountedByViewNotReadFromCoreFieldC()
        {
            // 수락 6·T-50: 단은 표시 쪽이 센다. 이벤트 칸 C 에 무엇이 와도(코어는 0을 낸다) 음높이는 라우터의 셈을 따른다
            PresentationEventRouter r = Router();
            PresentationEvent first = Ev(SimEventType.GrazeAwarded, EventPhase.Confirmed, tick: 100);
            first.Event.C = 4;
            Handle(r, first);
            PresentationEvent second = Ev(SimEventType.GrazeAwarded, EventPhase.Confirmed, tick: 130);
            second.Event.C = 0;
            Handle(r, second);
            Assert.Equal(new[] { 0, 1 }, _sfx.Played.ConvertAll(p => p.Pitch));
        }

        [Fact]
        public void GrazeChainCountsOnlyConfirmedAndPerSlot()
        {
            PresentationEventRouter r = Router();
            Handle(r, Ev(SimEventType.GrazeAwarded, EventPhase.Confirmed, tick: 100, slot: 0));
            Handle(r, Ev(SimEventType.GrazeAwarded, EventPhase.Predicted, tick: 110, slot: 0));   // 세지 않는다(소리도 없다)
            Handle(r, Ev(SimEventType.GrazeAwarded, EventPhase.Confirmed, tick: 120, slot: 1));   // 다른 슬롯은 따로
            Handle(r, Ev(SimEventType.GrazeAwarded, EventPhase.Confirmed, tick: 130, slot: 0));
            Assert.Equal(new[] { 0, 0, 1 }, _sfx.Played.ConvertAll(p => p.Pitch));
            r.Reset();
            Handle(r, Ev(SimEventType.GrazeAwarded, EventPhase.Confirmed, tick: 140, slot: 0));
            Assert.Equal(0, _sfx.Played[_sfx.Played.Count - 1].Pitch);
        }

        [Fact]
        public void RaiseUiUsesTheSameTable()
        {
            // 수락 4: 코어 밖 사건은 RaiseUi(key, args)로 같은 표에서 찾는다. 늘 확정으로 본다
            PresentationEventRouter r = Router();
            r.RaiseUi(UiEventKeys.DuelMatchFound, new UiEventArgs(4, 5, "x", Element.Fire));
            Assert.Single(_fx.Played);
            Assert.Equal(EventPhase.Confirmed, _fx.Played[0].Phase);
            Assert.Equal(Element.Fire, _fx.Played[0].Element);
            Assert.Equal(4, _fx.Played[0].A);
            Assert.Equal(5, _fx.Played[0].B);
            Assert.Equal(new[] { ("sfx_c10", 0) }, _sfx.Played);
            Assert.Equal(new[] { BindingShake.Large }, _camera.Shakes);
            Assert.Equal(50, _camera.Hitstops[0].Ms);
        }

        [Fact]
        public void UnboundKeysPassQuietlyAndAreReportedOnce()
        {
            PresentationEventRouter r = Router();
            Handle(r, Ev(SimEventType.ComboChanged, EventPhase.Confirmed));
            Handle(r, Ev(SimEventType.ComboChanged, EventPhase.Confirmed));
            r.RaiseUi(UiEventKeys.DuelQueueEnter, default);
            r.RaiseUi(UiEventKeys.DuelQueueEnter, default);
            Handle(r, Ev(SimEventType.None, EventPhase.Confirmed));                 // 의도적 없음: 알리지 않는다
            r.RaiseUi("duel.hit_predicted", default);
            Assert.Empty(_fx.Played);
            Assert.Empty(_sfx.Played);
            Assert.Equal(new[] { SimEventDocs.BindingKey(SimEventType.ComboChanged), UiEventKeys.DuelQueueEnter }, _unbound);
        }

        [Fact]
        public void UndefinedEventNumberIsReportedNotThrown()
        {
            // 열거형의 빈 번호(105)와 범위 밖 번호(60000)는 SimEventDocs 가 모른다. 던지지 않고 번호로 한 번 알린다
            PresentationEventRouter r = Router();
            Handle(r, Ev((SimEventType)105, EventPhase.Confirmed));
            Handle(r, Ev((SimEventType)60000, EventPhase.Confirmed));
            Handle(r, Ev((SimEventType)60000, EventPhase.Confirmed));
            Assert.Equal(new[] { "sim.unknown_105", "sim.unknown_60000" }, _unbound);
            Assert.Empty(_fx.Played);
        }

        [Fact]
        public void WorksWithoutCameraOrLogger()
        {
            var errors = new List<string>();
            EventBindingTable t = EventBindingTable.Load(BindingsJson(Line(Hit, "\"fx_lock_x\"", "\"sfx_c07\"", "medium", 120, true)), NoneEmpty, errors);
            var nullFx = new NullFxPlayer();
            var nullSfx = new NullSfxPlayer();
            var r = new PresentationEventRouter(t, nullFx, nullSfx);
            r.Handle(Ev(SimEventType.PlayerHit, EventPhase.Confirmed), null!);
            r.Handle(Ev(SimEventType.ComboReset, EventPhase.Confirmed), null!);
            r.RaiseUi("ui.common.press", default);
            Assert.Equal(1, nullFx.PlayCount);
            Assert.Equal(1, nullSfx.PlayCount);
        }

        [Fact]
        public void RealTableDrawsPredictedDuelHitAndFadesItOnRollback()
        {
            // spec 10 23.3 duel.hit_predicted·duel.hit_rollback, effects.md 17절, CONTRACTS 6.4: 저장소의 표로
            // 예측 적중은 적중 불꽃·피해 숫자를 Predicted 로 그리고(소리 없음), 확정은 다시 그리지 않고 소리를 내고,
            // 번복은 그린 것을 Retracted 로 흐린다
            var r = new PresentationEventRouter(RepoFiles.LoadRealTable(), _fx, _sfx, _camera, k => _unbound.Add(k));
            Handle(r, Ev(SimEventType.ProjectileHit, EventPhase.Predicted, tick: 50, slot: 1, ordinal: 0));
            Assert.Equal(new[] { "fx_element_hit", "ui_damage_digits" }, _fx.Played.ConvertAll(f => f.FxId));
            Assert.All(_fx.Played, f => Assert.Equal(EventPhase.Predicted, f.Phase));
            Assert.Empty(_sfx.Played);
            Assert.Empty(_camera.Shakes);

            Handle(r, Ev(SimEventType.ProjectileHit, EventPhase.Confirmed, tick: 50, slot: 1, ordinal: 0));
            Assert.Equal(2, _fx.Played.Count);                                      // 이어서 그린다(다시 내지 않는다)
            Assert.Equal(new[] { ("sfx_h01", 0), ("sfx_c02", 0) }, _sfx.Played);

            // 다른 예측 적중이 번복된다
            Handle(r, Ev(SimEventType.ProjectileHit, EventPhase.Predicted, tick: 51, slot: 1, ordinal: 0));
            Handle(r, Ev(SimEventType.ProjectileHit, EventPhase.Retracted, tick: 51, slot: 1, ordinal: 0));
            Assert.Equal(6, _fx.Played.Count);
            Assert.Equal(new[] { "fx_element_hit", "ui_damage_digits" }, _fx.Played.GetRange(4, 2).ConvertAll(f => f.FxId));
            Assert.All(_fx.Played.GetRange(4, 2), f => Assert.Equal(EventPhase.Retracted, f.Phase));
            Assert.Equal(2, _sfx.Played.Count);                                     // 번복된 적중은 소리를 내지 않는다

            // hit.enemy 의 적중 불꽃도 예측에서 그린다
            Handle(r, Ev(SimEventType.HitEnemy, EventPhase.Predicted, tick: 52));
            Assert.Equal("fx_element_hit", _fx.Played[6].FxId);
            Assert.Equal(EventPhase.Predicted, _fx.Played[6].Phase);

            // 피격 연출(player.hit)은 확정에서만
            int before = _fx.Played.Count;
            Handle(r, Ev(SimEventType.PlayerHit, EventPhase.Predicted, tick: 53));
            Assert.Equal(before, _fx.Played.Count);
            Assert.Empty(_camera.Hitstops);
            Assert.Empty(_unbound);
        }

        [Fact]
        public void RealTableRoutesEveryEventWithoutUnbound()
        {
            // 저장소의 표로: 모든 SimEventType 과 UiEventKeys 를 확정으로 흘려도 "대응 없음"이 나오지 않는다(빠짐 0)
            var r = new PresentationEventRouter(RepoFiles.LoadRealTable(), _fx, _sfx, _camera, k => _unbound.Add(k));
            uint tick = 1;
            foreach (SimEventType t in EventBindingCoverage.AllSimEventTypes())
            {
                r.Handle(Ev(t, EventPhase.Confirmed, tick: tick++), null!);
            }
            foreach (string k in UiEventKeys.All)
            {
                r.RaiseUi(k, default);
            }
            Assert.Empty(_unbound);
            Assert.NotEmpty(_fx.Played);
            Assert.NotEmpty(_sfx.Played);
            Assert.All(_fx.Played, f => Assert.True(r.Table.IsKnownFx(f.FxId), f.FxId));
            Assert.All(_sfx.Played, s => Assert.True(r.Table.IsKnownSfx(s.Id), s.Id));
        }
    }
}
