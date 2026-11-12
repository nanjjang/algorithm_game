using System;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.ReplayCli
{
    // 씨앗 고정 봇 입력. 틱마다 플레이어마다 순번 없는 키(QuantizedKey)와 명령 후보 하나를 낸다. 난수는 Pcg32 하나뿐이다
    // (같은 씨앗·같은 꼴이면 모든 런타임에서 같은 입력). 순번은 기록기(InputRecorder)나 결투 쪽 셈(SeqCounter)이 매긴다(CONTRACTS 3.1).
    public sealed class BotScript
    {
        // 한 틱에 낼 수 있는 키의 상한(몰린 틱 포함). 기록기는 아홉째부터 버린다
        public const int MaxKeysOut = 12;

        private const ulong BotStream = 0x626f74UL;      // "bot". 값 자체에 뜻은 없다
        private const int PermilleBase = 1000;
        private const int OffsetCount = 256;
        private const int BurstMin = 9;
        private const int BurstSpan = 4;                 // 9..12
        private const int SlotCount = 4;
        private const int SmallArg = 6;
        private const int ArtArg = 16;
        private const int DirectionCount = 4;           // Up, Left, Down, Right = 0..3 (LogicalKey)

        private static readonly RunCommandOp[] CommandOps =
        {
            RunCommandOp.SetSlot, RunCommandOp.SetSlot, RunCommandOp.SetSlot, RunCommandOp.SwapSlots, RunCommandOp.PickCard,
            RunCommandOp.RerollCards, RunCommandOp.DiscardBlock, RunCommandOp.Heal, RunCommandOp.UnlockArt, RunCommandOp.Depart,
            RunCommandOp.CloseCampScreen, RunCommandOp.EnterPractice, RunCommandOp.ExitPractice,
            RunCommandOp.DebugInvulnerable, RunCommandOp.DebugGrant, RunCommandOp.DebugSetStage,
        };

        private static readonly BlockId[] CoreBlocks = { BlockId.MagicBolt, BlockId.Fireball };
        private static readonly BlockId[] LinkBlocks = { BlockId.None, BlockId.SplitThree };

        private readonly BotProfile _profile;
        private readonly int[] _held = new int[SlotCount];   // 봇이 아는 논리 키 상태(플레이어마다). 쓸데없는 이벤트를 고르는 데만 쓴다
        private Pcg32 _rng;

        public BotScript(ulong seed, BotProfile profile)
        {
            _profile = profile ?? throw new ArgumentNullException(nameof(profile));
            _rng = new Pcg32(seed, BotStream);
        }

        // 틱 tick 의 slot 키. 오프셋은 아무렇게나(정렬은 받는 쪽이 한다). 돌려주는 값 = 쓴 수
        public int Keys(uint tick, int slot, Span<QuantizedKey> output)
        {
            if (output.Length < MaxKeysOut)
            {
                throw new ArgumentException("output must hold MaxKeysOut keys", nameof(output));
            }
            int n;
            if (Roll(_profile.BurstTickPermille))
            {
                n = BurstMin + (int)_rng.NextInt(BurstSpan);
            }
            else if (Roll(_profile.KeyTickPermille))
            {
                n = 1 + (int)_rng.NextInt(Math.Max(1, _profile.MaxKeysPerTick));
            }
            else
            {
                return 0;
            }
            for (int i = 0; i < n; i++)
            {
                LogicalKey key = _profile.Keys[_rng.NextInt(_profile.Keys.Length)];
                int bit = 1 << (int)key;
                bool isDown = (_held[slot] & bit) != 0;
                bool down = Roll(_profile.RedundantPermille) ? isDown : !isDown;
                if (down != isDown)
                {
                    _held[slot] ^= bit;
                }
                output[i] = new QuantizedKey(tick, (byte)_rng.NextInt(OffsetCount), key, down);
            }
            // 같은 오프셋에 방향과 K(spec 01 T-I01): 마지막 둘을 같은 자리로 모은다
            if (n >= 2 && Roll(_profile.SameOffsetPermille))
            {
                byte at = output[0].Offset;
                LogicalKey dir = (LogicalKey)_rng.NextInt(DirectionCount);
                output[n - 2] = Toggle(slot, tick, at, dir);
                output[n - 1] = Toggle(slot, tick, at, LogicalKey.Skill);
            }
            return n;
        }

        // 이 틱에 시도할 명령(없으면 false). 받을지는 세계가 정한다(CanApply)
        public bool Command(out RunCommandDraft draft)
        {
            draft = default;
            if (!Roll(_profile.CommandTickPermille))
            {
                return false;
            }
            RunCommandOp op = CommandOps[_rng.NextInt(CommandOps.Length)];
            switch (op)
            {
                case RunCommandOp.SetSlot:
                    bool core = _rng.NextInt(2) == 0;
                    BlockId b = core ? CoreBlocks[_rng.NextInt(CoreBlocks.Length)] : LinkBlocks[_rng.NextInt(LinkBlocks.Length)];
                    draft = new RunCommandDraft(op, core ? 0 : 1 + (int)_rng.NextInt(2), (int)b);
                    break;
                case RunCommandOp.UnlockArt:
                    draft = new RunCommandDraft(op, (int)_rng.NextInt(ArtArg), 0);
                    break;
                default:
                    draft = new RunCommandDraft(op, (int)_rng.NextInt(SmallArg), (int)_rng.NextInt(SmallArg));
                    break;
            }
            return true;
        }

        private QuantizedKey Toggle(int slot, uint tick, byte offset, LogicalKey key)
        {
            int bit = 1 << (int)key;
            bool down = (_held[slot] & bit) == 0;
            _held[slot] ^= bit;
            return new QuantizedKey(tick, offset, key, down);
        }

        private bool Roll(int permille) => permille > 0 && _rng.NextInt(PermilleBase) < permille;
    }
}
