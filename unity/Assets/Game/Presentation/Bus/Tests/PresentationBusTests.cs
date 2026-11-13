#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Wizard.Core;
using EntityId = Wizard.Core.EntityId;
using Wizard.Netcode.Duel;
using Wizard.Sim.Events;
using Wizard.Unity.Presentation.Core.Tests;
using Wizard.ViewLogic;
using Wizard.ViewLogic.Presentation;

namespace Wizard.Unity.Presentation.Bus.Tests
{
    // 수락 1: 버스(라우터)가 바인딩 표(data/presentation/event_bindings.json 그대로)대로 그림 재생기·소리 재생기·카메라 연출·HUD 모델에 넘긴다.
    // 수락 2: 빈 재생기는 디버그 겹침 화면에 식별자와 위치를 찍는다. 실제 재생기는 PresentationBusSetup.Fx·Sfx 에 꽂기만 하면 된다.
    // 수락 3: 그림 정지 중에도 틱(뷰의 틱, 다음 틱들의 사건)은 그대로 간다. timeScale 을 건드리지 않는다.
    public sealed class PresentationBusTests
    {
        private const int FrameMs = 16;

        private static EventBindingTable? _table;
        private readonly List<PresentationBus> _buses = new List<PresentationBus>();
        private readonly List<UnityEngine.Object> _made = new List<UnityEngine.Object>();
        private double _nowSeconds;

        private sealed class FxLog : IFxPlayer
        {
            public readonly List<FxRequest> Played = new List<FxRequest>();
            public int Stops;
            public void Play(in FxRequest request) => Played.Add(request);
            public void StopAll() => Stops++;
        }

        private sealed class SfxLog : ISfxPlayer
        {
            public readonly List<(string Id, int Pitch)> Played = new List<(string, int)>();
            public void Play(string sfxId, int pitchStep = 0, float volumeDb = 0f) => Played.Add((sfxId, pitchStep));
            public void SetBus(SfxBus bus, float volume) { }
        }

        private sealed class HudLog : IPresentationEventHandler
        {
            public readonly List<PresentationEvent> Seen = new List<PresentationEvent>();
            public ISimView? LastView;
            public void Handle(in PresentationEvent e, ISimView view)
            {
                Seen.Add(e);
                LastView = view;
            }
        }

        private static EventBindingTable Table()
        {
            if (_table != null)
            {
                return _table;
            }
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string bindings = File.ReadAllText(Path.Combine(root, EventBindingTable.BindingsFile));
            string none = File.ReadAllText(Path.Combine(root, EventBindingTable.NoneFile));
            var errors = new List<string>();
            EventBindingTable t = EventBindingTable.Load(bindings, none, errors);
            Assert.That(errors, Is.Empty);
            _table = t;
            return t;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (PresentationBus b in _buses)
            {
                b.Dispose();
            }
            _buses.Clear();
            foreach (UnityEngine.Object o in _made)
            {
                if (o != null)
                {
                    UnityEngine.Object.DestroyImmediate(o);
                }
            }
            _made.Clear();
            _nowSeconds = 0;
        }

        private PresentationBus Bus(Action<PresentationBusSetup>? edit = null)
        {
            var setup = new PresentationBusSetup(Table(), () => _nowSeconds) { OnUnbound = _ => { } };
            edit?.Invoke(setup);
            var bus = new PresentationBus(setup);
            _buses.Add(bus);
            return bus;
        }

        private static SimEvent Ev(SimEventType type, uint tick, byte slot = 0, long x = 0, long y = 0) =>
            new SimEvent { Type = type, Tick = tick, Slot = new PlayerSlot(slot), Source = new EntityId(3), Target = new EntityId(4), A = 7, B = 9, X = x, Y = y };

        private static EventBinding BindingOf(SimEventType type)
        {
            Assert.That(Table().TryGet(SimEventDocs.BindingKey(type), out EventBinding? b), Is.True, type.ToString());
            return b!;
        }

        // 그림이 있고 확정 전에도 그리는(confirmed_only 가 아닌) 코어 사건 하나
        private static SimEventType PredictableType()
        {
            foreach (SimEventType t in EventBindingCoverage.AllSimEventTypes())
            {
                if (Table().TryGet(SimEventDocs.BindingKey(t), out EventBinding? b) && !b!.ConfirmedOnly && b.Fx.Count > 0)
                {
                    return t;
                }
            }
            Assert.Fail("no binding line with fx that is not confirmed_only");
            return default;
        }

        [Test]
        public void ConfirmedEvents_ReachFxSfxCameraAndHud_ExactlyAsTheBindingTableSays()
        {
            var fx = new FxLog();
            var sfx = new SfxLog();
            var hud = new HudLog();
            PresentationBus bus = Bus(s => { s.Fx = fx; s.Sfx = sfx; });
            bus.AddHudModel(hud);
            var view = new FakeSimView { Tick = 1 };

            int checkedLines = 0;
            uint tick = 1;
            foreach (SimEventType type in EventBindingCoverage.AllSimEventTypes())
            {
                if (!Table().TryGet(SimEventDocs.BindingKey(type), out EventBinding? b))
                {
                    continue;
                }
                fx.Played.Clear();
                sfx.Played.Clear();
                hud.Seen.Clear();
                int shakes = bus.Camera.ShakeCount;
                int hitstops = bus.Camera.HitstopCount;

                tick += 1000;                                               // 연속 스침이 이어지지 않게 띄운다
                SimEvent[] events = { Ev(type, tick) };
                bus.OnEvents(events, view);

                Assert.That(fx.Played.Count, Is.EqualTo(b!.Fx.Count), b.EventKey);
                for (int i = 0; i < b.Fx.Count; i++)
                {
                    Assert.That(fx.Played[i].FxId, Is.EqualTo(b.Fx[i]), b.EventKey);
                    Assert.That(fx.Played[i].Phase, Is.EqualTo(EventPhase.Confirmed), b.EventKey);
                    Assert.That(fx.Played[i].A, Is.EqualTo(7));
                    Assert.That(fx.Played[i].B, Is.EqualTo(9));
                }
                Assert.That(sfx.Played.Count, Is.EqualTo(b.Sfx.Count), b.EventKey);
                for (int i = 0; i < b.Sfx.Count; i++)
                {
                    Assert.That(sfx.Played[i].Id, Is.EqualTo(b.Sfx[i]), b.EventKey);
                }
                Assert.That(bus.Camera.ShakeCount - shakes, Is.EqualTo(b.Shake == BindingShake.None ? 0 : 1), b.EventKey);
                Assert.That(bus.Camera.HitstopCount - hitstops, Is.EqualTo(b.HitstopMs > 0 ? 1 : 0), b.EventKey);
                Assert.That(hud.Seen.Count, Is.EqualTo(1), b.EventKey);
                Assert.That(hud.Seen[0].Event.Type, Is.EqualTo(type));
                Assert.That(hud.Seen[0].Phase, Is.EqualTo(EventPhase.Confirmed));
                Assert.That(hud.LastView, Is.SameAs(view));
                checkedLines++;
            }
            Assert.That(checkedLines, Is.GreaterThan(0));
            Assert.That(bus.DebugFx, Is.Null, "no debug player when a real one is plugged in");
            Assert.That(bus.DebugSfx, Is.Null);
            Assert.That(bus.Overlay, Is.Null);
        }

        [Test]
        public void PlayerHit_ShakesTheWorldCameraAndFreezesTheHitPictures()
        {
            EventBinding b = BindingOf(SimEventType.PlayerHit);
            Assert.That(b.Shake, Is.Not.EqualTo(BindingShake.None));
            Assert.That(b.HitstopMs, Is.GreaterThan(0));
            var camGo = new GameObject("World");
            _made.Add(camGo);
            UnityEngine.Camera cam = camGo.AddComponent<UnityEngine.Camera>();
            PresentationBus bus = Bus(s => s.WorldCamera = cam);
            var view = new FakeSimView { Tick = 5 };

            bus.OnEvents(new[] { Ev(SimEventType.PlayerHit, 5) }, view);
            Assert.That(bus.Camera.Accumulated, Is.GreaterThan(0f));
            Assert.That(bus.Camera.IsFrozen(new EntityId(4)), Is.True);
            Assert.That(bus.Camera.IsFrozen(new EntityId(3)), Is.True);
            Assert.That(bus.Camera.Target, Is.SameAs(cam.transform));
        }

        [Test]
        public void Hitstop_FreezesPicturesOnly_TicksAndLaterEventsKeepFlowing()
        {
            EventBinding hit = BindingOf(SimEventType.PlayerHit);
            Assert.That(hit.HitstopMs, Is.GreaterThan(0));
            var fx = new FxLog();
            PresentationBus bus = Bus(s => s.Fx = fx);
            var view = new FakeSimView { Tick = 100 };
            SimEventType steady = PredictableType();

            bus.OnEvents(new[] { Ev(SimEventType.PlayerHit, view.Tick) }, view);
            bus.Render(view, 0);
            Assert.That(bus.Camera.IsFrozen(new EntityId(4)), Is.True);

            int frames = hit.HitstopMs / FrameMs + 2;
            int frozenFrames = 0;
            for (int i = 0; i < frames; i++)
            {
                _nowSeconds += FrameMs / 1000.0;
                view.Tick++;                                                // 구동기는 틱을 그대로 돌린다(버스는 틱을 쥐지 않는다)
                int before = fx.Played.Count;
                SimEvent next = Ev(steady, view.Tick);
                next.Source = EntityId.None;                                // 맞은 개체(4)를 다시 얼리지 않게 한다
                next.Target = EntityId.None;
                bus.OnEvents(new[] { next }, view);
                bus.Render(view, 0.5);
                Assert.That(fx.Played.Count, Is.GreaterThan(before), "later ticks still draw during hitstop");
                if (bus.Camera.IsFrozen(new EntityId(4)))
                {
                    frozenFrames++;
                }
            }
            Assert.That(view.Tick, Is.EqualTo(100u + (uint)frames));
            Assert.That(frozenFrames, Is.GreaterThan(0));
            Assert.That(bus.Camera.IsFrozen(new EntityId(4)), Is.False, "hitstop ends after its length");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test]
        public void DebugPlayers_PrintIdentifierAndPositionOnTheOverlay()
        {
            PresentationBus bus = Bus();
            Assert.That(bus.DebugFx, Is.Not.Null);
            Assert.That(bus.DebugSfx, Is.Not.Null);
            Assert.That(bus.Overlay, Is.Not.Null);
            EventBinding b = BindingOf(SimEventType.PlayerHit);
            Assert.That(b.Fx.Count, Is.GreaterThan(0));
            Assert.That(b.Sfx.Count, Is.GreaterThan(0));

            long x = 3L << 31;                                              // 1.5칸(Q32.32)
            long y = -(5L << 32);                                           // -5칸
            bus.OnEvents(new[] { Ev(SimEventType.PlayerHit, 1, 0, x, y) }, new FakeSimView { Tick = 1 });

            System.Numerics.Vector2 pos = FixConvert.ToWorld(new FixVec2(Fix64.FromRaw(x), Fix64.FromRaw(y)));
            Assert.That(bus.DebugFx!.Count, Is.EqualTo(b.Fx.Count));
            Assert.That(bus.DebugFx.Mark(0).WorldPos, Is.EqualTo(pos));
            IReadOnlyList<string> lines = bus.Overlay!.CollectLines();
            string expectFx = "fx " + b.Fx[0] + " (" + pos.X.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ", " +
                              pos.Y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ") Confirmed";
            Assert.That(lines, Has.Member(expectFx));
            Assert.That(lines, Has.Member("sfx " + b.Sfx[0]));
        }

        [Test]
        public void PredictedThenRetracted_DrawsBeforeConfirmationAndFades_WithoutSoundOrShake()
        {
            SimEventType type = PredictableType();
            EventBinding b = BindingOf(type);
            PresentationBus bus = Bus();
            var hud = new HudLog();
            bus.AddHudModel(hud);
            var view = new FakeSimView { Tick = 20 };
            var e = new PresentationEvent { Event = Ev(type, 20), Phase = EventPhase.Predicted, Ordinal = 0 };

            bus.Publish(e, view);
            Assert.That(bus.DebugFx!.Count, Is.EqualTo(b.Fx.Count));
            Assert.That(bus.DebugFx.Mark(0).Phase, Is.EqualTo(EventPhase.Predicted));
            Assert.That(bus.DebugSfx!.Count, Is.EqualTo(0), "sound only when Confirmed");
            Assert.That(bus.Camera.ShakeCount, Is.EqualTo(0));
            Assert.That(bus.Camera.HitstopCount, Is.EqualTo(0));

            e.Phase = EventPhase.Retracted;
            bus.Publish(e, view);
            Assert.That(bus.DebugFx.Mark(0).Phase, Is.EqualTo(EventPhase.Retracted));
            Assert.That(bus.DebugFx.Mark(0).Fading, Is.True);
            Assert.That(hud.Seen.Count, Is.EqualTo(2), "HUD model gets the phase as is");
            Assert.That(hud.Seen[1].Phase, Is.EqualTo(EventPhase.Retracted));

            _nowSeconds += 0.1;
            bus.Render(view, 0);
            Assert.That(bus.DebugFx.Count, Is.EqualTo(0), "gone after a 0.1 s fade");
        }

        [Test]
        public void RaiseUi_UsesTheSameTable()
        {
            string? key = null;
            EventBinding? binding = null;
            foreach (string k in UiEventKeys.All)
            {
                if (Table().TryGet(k, out EventBinding? b) && b!.Fx.Count > 0)
                {
                    key = k;
                    binding = b;
                    break;
                }
            }
            Assert.That(key, Is.Not.Null);
            var fx = new FxLog();
            PresentationBus bus = Bus(s => s.Fx = fx);
            bus.RaiseUi(key!, new UiEventArgs(1, 2));
            Assert.That(fx.Played.Count, Is.EqualTo(binding!.Fx.Count));
            Assert.That(fx.Played[0].FxId, Is.EqualTo(binding.Fx[0]));
        }

        [Test]
        public void UnboundKeys_AreReportedOnceAndOtherwiseIgnored()
        {
            var reported = new List<string>();
            PresentationBus bus = Bus(s => s.OnUnbound = reported.Add);
            bus.RaiseUi("ui.no_such_key_for_test", default);
            bus.RaiseUi("ui.no_such_key_for_test", default);
            Assert.That(reported, Is.EqualTo(new[] { "ui.no_such_key_for_test" }));
        }

        [Test]
        public void Ordinal_CountsSameTypeAndSlotWithinATick()
        {
            SimEvent[] events =
            {
                Ev(SimEventType.PlayerHit, 1, 0),
                Ev(SimEventType.PlayerHit, 2, 0),
                Ev(SimEventType.PlayerHit, 2, 1),
                Ev(SimEventType.GrazeAwarded, 2, 0),
                Ev(SimEventType.PlayerHit, 2, 0),
                Ev(SimEventType.PlayerHit, 2, 0),
            };
            var got = new ushort[events.Length];
            for (int i = 0; i < events.Length; i++)
            {
                got[i] = PresentationBus.OrdinalOf(events, i);
            }
            Assert.That(got, Is.EqualTo(new ushort[] { 0, 0, 0, 0, 1, 2 }));
        }

        [Test]
        public void HudModels_CanBeAddedOnceAndRemoved()
        {
            PresentationBus bus = Bus();
            var hud = new HudLog();
            bus.AddHudModel(hud);
            bus.AddHudModel(hud);
            Assert.That(bus.HudModelCount, Is.EqualTo(1));
            Assert.That(bus.RemoveHudModel(hud), Is.True);
            bus.OnEvents(new[] { Ev(SimEventType.PlayerHit, 1) }, new FakeSimView());
            Assert.That(hud.Seen, Is.Empty);
        }

        [Test]
        public void Reset_ClearsPicturesShakeAndHitstop_DisposeRemovesTheOverlay()
        {
            PresentationBus bus = Bus();
            bus.OnEvents(new[] { Ev(SimEventType.PlayerHit, 1) }, new FakeSimView { Tick = 1 });
            Assert.That(bus.DebugFx!.Count, Is.GreaterThan(0));
            bus.Reset();
            Assert.That(bus.DebugFx.Count, Is.EqualTo(0));
            Assert.That(bus.DebugSfx!.Count, Is.EqualTo(0));
            Assert.That(bus.Camera.Accumulated, Is.EqualTo(0f));
            Assert.That(bus.Camera.IsFrozen(new EntityId(4)), Is.False);

            GameObject root = bus.Overlay!.gameObject;
            bus.Dispose();
            Assert.That(root == null, Is.True);
            Assert.Throws<ObjectDisposedException>(() => bus.Render(new FakeSimView(), 0));
        }
    }
}
