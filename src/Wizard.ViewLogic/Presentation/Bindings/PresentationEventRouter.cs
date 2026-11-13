using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Sim.Events;

namespace Wizard.ViewLogic.Presentation
{
    // CONTRACTS 13절, ARCHITECTURE 9절. 표시 이벤트를 바인딩 표로 찾아 그림·소리·카메라 신호로 넘긴다. 결과(세계 상태)를 바꾸지 않는다.
    //   Handle  — 코어 이벤트. SimEventDocs.BindingKey 로 찾는다(열쇠 → 줄은 만들 때 한 번 풀어 둔다. 프레임마다 할당하지 않는다)
    //   RaiseUi — 코어 밖 사건(UiEventKeys). 같은 표에서 찾는다. 늘 Confirmed 로 본다
    // 단계(CONTRACTS 6.4):
    //   - 소리, 흔들림, 그림 정지는 Confirmed 에서만 낸다.
    //   - confirmed_only 줄: 그림도 Confirmed 에서만(Predicted·Retracted 는 아무것도 내지 않는다).
    //   - 그 밖의 줄: Predicted 에서 그림을 내고 열쇠 (Tick, Type, Slot, Ordinal)를 기억한다. 같은 열쇠가 Confirmed 로 오면 다시 그리지 않고 이어서 그린다.
    //     Retracted 로 오면 Phase = Retracted 인 요청을 보내 0.1초 흐려지게 한다. 예측 없이 Confirmed 로 온 것은 그때 그린다.
    // 연속 스침의 음높이: Confirmed 인 GrazeAwarded 를 GrazeChainCounter 로 세어 그 줄의 소리에 pitchStep 으로 준다(T-50).
    // 대응이 없는 열쇠는 조용히 지나간다. onUnbound 가 있으면 열쇠마다 한 번 알린다(개발 빌드의 기록). "의도적 없음" 목록의 열쇠는 알리지 않는다.
    public sealed class PresentationEventRouter : IPresentationEventHandler
    {
        private const int PredictedCapacity = 256;            // 기억할 예측 열쇠 수(자료구조 크기). 넘치면 가장 오래된 것을 버린다
        private const string UnknownSimKeyPrefix = "sim.unknown_";

        private readonly EventBindingTable _table;
        private readonly IFxPlayer _fx;
        private readonly ISfxPlayer _sfx;
        private readonly ICameraCueSink? _camera;
        private readonly Action<string>? _onUnbound;
        private readonly EventBinding?[] _bySimType;
        private readonly string?[] _simKey;                  // 정의된 값의 열쇠. 빈 번호는 null
        private readonly bool[] _simNone;
        private readonly GrazeChainCounter _grazeChain;
        private readonly List<PredictedKey> _predicted = new List<PredictedKey>(PredictedCapacity);
        private readonly HashSet<string> _reportedUnbound = new HashSet<string>(StringComparer.Ordinal);

        public PresentationEventRouter(EventBindingTable table, IFxPlayer fx, ISfxPlayer sfx, ICameraCueSink? camera = null, Action<string>? onUnbound = null)
        {
            _table = table;
            _fx = fx;
            _sfx = sfx;
            _camera = camera;
            _onUnbound = onUnbound;
            _grazeChain = new GrazeChainCounter(table.GrazeChain);

            List<SimEventType> types = EventBindingCoverage.AllSimEventTypes();
            int size = types.Count == 0 ? 1 : (ushort)types[types.Count - 1] + 1;
            _bySimType = new EventBinding?[size];
            _simKey = new string?[size];
            _simNone = new bool[size];
            foreach (SimEventType t in types)
            {
                string key = SimEventDocs.BindingKey(t);
                _simKey[(ushort)t] = key;
                if (table.TryGet(key, out EventBinding? b))
                {
                    _bySimType[(ushort)t] = b;
                }
                _simNone[(ushort)t] = table.IsIntentionallyNone(key);
            }
        }

        public EventBindingTable Table => _table;

        // HUD·디버그 표시용: 이 슬롯의 마지막 연속 스침 단
        public int GrazeChainStep(PlayerSlot slot) => _grazeChain.Current(slot);

        public void Handle(in PresentationEvent e, ISimView view)
        {
            SimEventType type = e.Event.Type;
            int index = (ushort)type;
            EventBinding? binding = index < _bySimType.Length ? _bySimType[index] : null;
            if (binding == null)
            {
                if (index >= _simNone.Length || !_simNone[index])
                {
                    // 열거형에 없는 번호(다른 판의 코어가 낸 것)는 SimEventDocs 가 모르므로 번호로 알린다
                    string? simKey = index < _simKey.Length ? _simKey[index] : null;
                    ReportUnbound(simKey ?? UnknownSimKeyPrefix + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
                return;
            }

            int pitchStep = 0;
            if (type == SimEventType.GrazeAwarded && e.Phase == EventPhase.Confirmed)
            {
                pitchStep = _grazeChain.Next(e.Event.Slot, e.Event.Tick);
            }

            var pos = FixConvert.ToWorld(new FixVec2(Fix64.FromRaw(e.Event.X), Fix64.FromRaw(e.Event.Y)));
            var key = new PredictedKey(e.Event.Tick, type, e.Event.Slot, e.Ordinal);
            switch (e.Phase)
            {
                case EventPhase.Predicted:
                    if (!binding.ConfirmedOnly && binding.Fx.Count > 0)
                    {
                        PlayFx(binding, pos, Element.None, e.Event.A, e.Event.B, EventPhase.Predicted);
                        Remember(key);
                    }
                    break;
                case EventPhase.Retracted:
                    if (Forget(key))
                    {
                        PlayFx(binding, pos, Element.None, e.Event.A, e.Event.B, EventPhase.Retracted);
                    }
                    break;
                default:
                    PruneBefore(e.Event.Tick);
                    if (!Forget(key))
                    {
                        PlayFx(binding, pos, Element.None, e.Event.A, e.Event.B, EventPhase.Confirmed);
                    }
                    PlaySfx(binding, pitchStep);
                    Cue(binding, e.Event.Source, e.Event.Target);
                    break;
            }
        }

        public void RaiseUi(string uiEventKey, in UiEventArgs args)
        {
            if (!_table.TryGet(uiEventKey, out EventBinding? binding))
            {
                if (!_table.IsIntentionallyNone(uiEventKey))
                {
                    ReportUnbound(uiEventKey);
                }
                return;
            }
            PlayFx(binding, default, args.Element, args.A, args.B, EventPhase.Confirmed);
            PlaySfx(binding, 0);
            Cue(binding, default, default);
        }

        // 새 판·새 경기·결투 재동기화 때 부른다(예측 기억과 연속 스침을 비운다)
        public void Reset()
        {
            _predicted.Clear();
            _grazeChain.Reset();
        }

        private void PlayFx(EventBinding b, System.Numerics.Vector2 pos, Element element, int a, int bArg, EventPhase phase)
        {
            for (int i = 0; i < b.Fx.Count; i++)
            {
                _fx.Play(new FxRequest(b.Fx[i], pos, 0f, element, a, bArg, phase));
            }
        }

        private void PlaySfx(EventBinding b, int pitchStep)
        {
            for (int i = 0; i < b.Sfx.Count; i++)
            {
                _sfx.Play(b.Sfx[i], pitchStep);
            }
        }

        private void Cue(EventBinding b, EntityId source, EntityId target)
        {
            if (_camera == null)
            {
                return;
            }
            if (b.Shake != BindingShake.None)
            {
                _camera.Shake(b.Shake);
            }
            if (b.HitstopMs > 0)
            {
                _camera.Hitstop(b.HitstopMs, source, target);
            }
        }

        private void ReportUnbound(string key)
        {
            if (_onUnbound != null && _reportedUnbound.Add(key))
            {
                _onUnbound(key);
            }
        }

        private void Remember(PredictedKey key)
        {
            if (IndexOf(key) >= 0)
            {
                return;
            }
            if (_predicted.Count == PredictedCapacity)
            {
                _predicted.RemoveAt(0);
            }
            _predicted.Add(key);
        }

        private bool Forget(PredictedKey key)
        {
            int i = IndexOf(key);
            if (i < 0)
            {
                return false;
            }
            _predicted.RemoveAt(i);
            return true;
        }

        // 확정 세계가 tick 에 왔으면 그 앞 틱의 예측은 확정되지도 번복되지도 않은 채 남지 않는다(번복은 그 전에 온다). 남은 것은 버린다
        private void PruneBefore(uint tick)
        {
            for (int i = _predicted.Count - 1; i >= 0; i--)
            {
                if (_predicted[i].Tick < tick)
                {
                    _predicted.RemoveAt(i);
                }
            }
        }

        private int IndexOf(PredictedKey key)
        {
            for (int i = 0; i < _predicted.Count; i++)
            {
                if (_predicted[i].SameAs(key))
                {
                    return i;
                }
            }
            return -1;
        }

        // CONTRACTS 6.4의 열쇠. EntityId 를 넣지 않는다
        private readonly struct PredictedKey
        {
            public PredictedKey(uint tick, SimEventType type, PlayerSlot slot, ushort ordinal)
            {
                Tick = tick;
                Type = type;
                Slot = slot;
                Ordinal = ordinal;
            }

            public uint Tick { get; }

            public SimEventType Type { get; }

            public PlayerSlot Slot { get; }

            public ushort Ordinal { get; }

            public bool SameAs(PredictedKey o) => Tick == o.Tick && Type == o.Type && Slot == o.Slot && Ordinal == o.Ordinal;
        }
    }
}
