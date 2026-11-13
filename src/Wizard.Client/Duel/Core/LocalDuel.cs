using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Netcode.Duel;
using Wizard.Protocol;
using Wizard.Protocol.Duel;
using Wizard.Sim;

namespace Wizard.Client.Duel
{
    // 한 기기 2인 결투(spec 10 19의 4, ARCHITECTURE 6.4): 기기 안의 호스트(LoopbackHost) 하나에 LoopbackTransport 로 붙은 DuelSession 둘과
    // 키보드 하나를 나눈 입력원 둘(SplitKeyboardInput). 서버 경기와 같은 패킷, 같은 넷코드 코어, 같은 세션 코드로 돈다.
    //
    // 키: 호스트 키(DuelHostKeys)와 같은 값으로 사람마다 DuelClientKeys 를 여기서 한 번만 만든다(T-62: duel.setup 하나에 한 번).
    // 키 바이트를 만드는(무작위) 일은 부르는 쪽이 한다(이 클래스는 난수를 쓰지 않는다).
    //
    // 프레임마다 Frame(now): 호스트 Pump → 사람 0 Step → 사람 1 Step → 호스트 Pump(방금 보낸 것을 같은 프레임에 받는다) → 전체 상태가 필요한
    // 세션에 호스트의 상태를 준다(서버 경기에서는 웹소켓 duel.full_state 가 하는 일).
    public sealed class LocalDuel
    {
        private const double MicrosPerSecond = 1000000.0;   // 단위 환산
        private const int DuelWireSlots = DuelWire.DuelSlots;

        private readonly LoopbackHost _host;
        private readonly DuelSession[] _sessions;
        private readonly LoopbackTransport[] _transports;
        private readonly SplitKeyboardInput _keyboard;
        private readonly ByteWriter _state = new ByteWriter();

        public LocalDuel(ContentDb db, DuelSetup setup, DuelHostKeys keys, IAeadCipher cipher, KeyBindings player0, KeyBindings player1,
                         LoopbackHostOptions hostOptions, DuelSessionOptions sessionOptions)
        {
            _host = new LoopbackHost(db, setup, keys, cipher, hostOptions);
            _keyboard = new SplitKeyboardInput(player0, player1);
            _transports = new LoopbackTransport[DuelWireSlots];
            _sessions = new DuelSession[DuelWireSlots];
            for (int s = 0; s < DuelWireSlots; s++)
            {
                var slot = new PlayerSlot((byte)s);
                DuelPeerKey k = keys.For(slot);
                var clientKeys = new DuelClientKeys(k.TokenId, k.Epoch, k.Key);
                _transports[s] = _host.Connect(slot);
                _sessions[s] = new DuelSession(db, setup, slot, clientKeys, cipher, _transports[s], sessionOptions);
            }
        }

        public LoopbackHost Host => _host;

        public SplitKeyboardInput Keyboard => _keyboard;

        public DuelSession Session(PlayerSlot slot)
        {
            if (slot.Value >= DuelWireSlots)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "duel slots are 0 and 1");
            }
            return _sessions[slot.Value];
        }

        public void Frame(double nowSeconds)
        {
            long nowUs = (long)Math.Floor(nowSeconds * MicrosPerSecond);
            _host.Pump(nowUs);
            for (int s = 0; s < DuelWireSlots; s++)
            {
                _sessions[s].Step(_keyboard.Player(s), nowSeconds);
            }
            _host.Pump(nowUs);
            for (int s = 0; s < DuelWireSlots; s++)
            {
                if (_sessions[s].NeedsFullState)
                {
                    _state.Reset();
                    if (_host.Core.TryWriteFullState(_state, out uint tick))
                    {
                        _sessions[s].OnFullState(tick, _state.Written);
                    }
                }
            }
        }
    }
}
