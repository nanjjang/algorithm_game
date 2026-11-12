using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Arts;
using Wizard.Sim.Input;
using Wizard.Sim.Player;
using Xunit;

namespace Wizard.Sim.Tests.Arts.Parser
{
    // 논리 키 이벤트로 판정기를 돌린다: InputOrder 정렬(R-14) → Player 모듈의 입력 처리(KeyState, 자세, 시야, 상대 방향,
    // spec 01 3.8) → CommandInputRouter → 판정기. 2단계의 실제 처리 순서와 같다(Stage02 훅은 이벤트마다 Player → Arts).
    // 시작 시야는 E(오른쪽)이다: 자세 중 앞 = Right, 뒤 = Left, 좌(반시계 90도) = Up, 우 = Down.
    internal sealed class KeyRig
    {
        public const long T0 = ParserRig.T0;

        private readonly List<(long time, LogicalKey key, bool down, uint seq)> _events = new List<(long, LogicalKey, bool, uint)>();
        private readonly NullEventSink _sink = new NullEventSink();
        private PlayerState _player;
        private uint _seq;

        public readonly CommandParser Parser;
        public readonly TestWallet Wallet;
        public readonly UnlockSet Unlocks;
        public readonly List<ArtDecision> Verdicts = new List<ArtDecision>();

        public KeyRig(CharacterId c, int crystals, Unlocks unlocks)
        {
            Parser = new CommandParser(SpecArts.Trie(c));
            Wallet = SpecArts.Wallet(c, crystals, 0);
            Unlocks = unlocks.For(c);
            _player.Character = c;
            _player.Facing = Dir8.E;
            _player.LastPressedCardinal = Dir8.E;
            _player.StanceLockedFacing = Dir8.E;
        }

        public const LogicalKey Fwd = LogicalKey.Right;      // 시야 E 기준 앞

        public KeyRig Press(long time, LogicalKey key) => Add(time, key, true, ++_seq);

        public KeyRig Release(long time, LogicalKey key) => Add(time, key, false, ++_seq);

        // 순번을 직접 준다(같은 틱·같은 오프셋에서 기록 순번이 정렬을 바꾸지 않는지 볼 때)
        public KeyRig PressWithSeq(long time, LogicalKey key, uint seq) => Add(time, key, true, seq);

        private KeyRig Add(long time, LogicalKey key, bool down, uint seq)
        {
            _events.Add((time, key, down, seq));
            if (seq > _seq)
            {
                _seq = seq;
            }
            return this;
        }

        // 쌓인 이벤트를 정렬해 처리하고 판정들을 돌려준다
        public List<ArtDecision> Run()
        {
            var inputs = new PlayerInput[_events.Count];
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                Assert.True(e.time >= 0);
                uint tick = (uint)(e.time / SimTime.OffsetsPerTick);
                byte offset = (byte)(e.time % SimTime.OffsetsPerTick);
                inputs[i] = new PlayerInput(new PlayerSlot(0), new InputEvent(tick, offset, e.key, e.down, e.seq));
            }
            InputOrder.Sort(inputs);
            foreach (PlayerInput pi in inputs)
            {
                InputEvent e = pi.Event;
                PlayerMotion.ApplyInput(ref _player, in e, pi.Slot, e.Tick, _sink, out PlayerInputResult r);
                if (CommandInputRouter.Route(Parser, in r, Wallet, in Unlocks, out ArtDecision v))
                {
                    Verdicts.Add(v);
                }
            }
            _events.Clear();
            return Verdicts;
        }

        public ArtDecision Single()
        {
            Run();
            return Assert.Single(Verdicts);
        }
    }
}
