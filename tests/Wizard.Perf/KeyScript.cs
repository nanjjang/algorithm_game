using System;
using System.Collections.Generic;
using Wizard.Content;
using Wizard.Core;
using Wizard.Sim.Input;

namespace Wizard.Perf
{
    // 벤치의 입력 각본. 고정 씨앗의 Pcg32 로 한 번 만들어 두고 같은 배열을 매 회차의 새 세계에 넣는다(재는 동안 만들지 않는다).
    //
    // 손으로 하는 판을 흉내 낸 밀도: 방향 키 넷은 틱마다 1/16 확률로 누름·뗌이 바뀌고, J 는 대부분 누르고 있고(뗄 확률 1/240,
    // 다시 누를 확률 1/8), K 는 1/30 확률로 한 틱 눌렀다 다음 틱에 뗀다(판정기가 커맨드를 읽는다), 자세는 1/120, 역방향은 1/300.
    // 같은 틱에 같은 키를 두 번 바꾸지 않으므로 이벤트는 플레이어당 틱마다 8개 이하다(CONTRACTS 3.1). 일시정지 키는 쓰지 않는다.
    // 연습 세계(시제품)에는 0단계 명령도 넣는다: 0틱에 3갈래 분열을 끼우고, 틱마다 1/600 확률로 코어를 마력탄·화염구로 바꾼다.
    internal sealed class KeyScript
    {
        private const int DirectionToggleOdds = 16;
        private const int FireReleaseOdds = 240;
        private const int FirePressOdds = 8;
        private const int SkillPressOdds = 30;
        private const int StanceToggleOdds = 120;
        private const int ReverseToggleOdds = 300;
        private const int CoreSwapOdds = 600;
        private const int OffsetCount = 256;
        private const int KeyCount = 9;                       // LogicalKey 0..8
        private const int LinkSlot = 1;                       // SetSlot A = 1: 첫 연결 칸(시제품의 조립 바꾸기)
        private const int CoreSlot = 0;

        private static readonly LogicalKey[] Directions = { LogicalKey.Up, LogicalKey.Left, LogicalKey.Down, LogicalKey.Right };

        private readonly ScriptKey[] _keys;
        private readonly int[] _keyStart;                     // [t] .. [t+1] = 틱 t 의 키(슬롯·생성 순)
        private readonly RunCommandDraft[] _drafts;           // 슬롯 0 의 명령
        private readonly int[] _draftStart;

        private KeyScript(int ticks, int players, ScriptKey[] keys, int[] keyStart, RunCommandDraft[] drafts, int[] draftStart)
        {
            Ticks = ticks;
            Players = players;
            _keys = keys;
            _keyStart = keyStart;
            _drafts = drafts;
            _draftStart = draftStart;
        }

        public int Ticks { get; }

        public int Players { get; }

        public int KeyCountTotal => _keys.Length;

        public ReadOnlySpan<ScriptKey> KeysAt(int tick) => new ReadOnlySpan<ScriptKey>(_keys, _keyStart[tick], _keyStart[tick + 1] - _keyStart[tick]);

        public ReadOnlySpan<RunCommandDraft> DraftsAt(int tick) =>
            new ReadOnlySpan<RunCommandDraft>(_drafts, _draftStart[tick], _draftStart[tick + 1] - _draftStart[tick]);

        public static KeyScript Generate(ulong seed, int ticks, int players, bool assemblyCommands)
        {
            if (ticks <= 0 || players <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ticks));
            }
            var rng = new Pcg32(seed, (ulong)players);
            var held = new bool[players, KeyCount];
            var keys = new List<ScriptKey>();
            var keyStart = new int[ticks + 1];
            var drafts = new List<RunCommandDraft>();
            var draftStart = new int[ticks + 1];
            bool fireball = false;

            for (int t = 0; t < ticks; t++)
            {
                keyStart[t] = keys.Count;
                draftStart[t] = drafts.Count;
                if (assemblyCommands)
                {
                    if (t == 0)
                    {
                        drafts.Add(new RunCommandDraft(RunCommandOp.SetSlot, LinkSlot, (int)BlockId.SplitThree));
                    }
                    else if (rng.NextInt(CoreSwapOdds) == 0)
                    {
                        fireball = !fireball;
                        drafts.Add(new RunCommandDraft(RunCommandOp.SetSlot, CoreSlot,
                                                       fireball ? (int)BlockId.Fireball : (int)BlockId.MagicBolt));
                    }
                }
                for (int p = 0; p < players; p++)
                {
                    for (int d = 0; d < Directions.Length; d++)
                    {
                        if (rng.NextInt(DirectionToggleOdds) == 0)
                        {
                            Toggle(keys, held, (uint)t, p, Directions[d], ref rng);
                        }
                    }
                    bool fireHeld = held[p, (int)LogicalKey.Fire];
                    if (fireHeld ? rng.NextInt(FireReleaseOdds) == 0 : rng.NextInt(FirePressOdds) == 0)
                    {
                        Toggle(keys, held, (uint)t, p, LogicalKey.Fire, ref rng);
                    }
                    if (held[p, (int)LogicalKey.Skill] || rng.NextInt(SkillPressOdds) == 0)
                    {
                        Toggle(keys, held, (uint)t, p, LogicalKey.Skill, ref rng);
                    }
                    if (rng.NextInt(StanceToggleOdds) == 0)
                    {
                        Toggle(keys, held, (uint)t, p, LogicalKey.Stance, ref rng);
                    }
                    if (rng.NextInt(ReverseToggleOdds) == 0)
                    {
                        Toggle(keys, held, (uint)t, p, LogicalKey.Reverse, ref rng);
                    }
                }
            }
            keyStart[ticks] = keys.Count;
            draftStart[ticks] = drafts.Count;
            return new KeyScript(ticks, players, keys.ToArray(), keyStart, drafts.ToArray(), draftStart);
        }

        private static void Toggle(List<ScriptKey> keys, bool[,] held, uint tick, int slot, LogicalKey key, ref Pcg32 rng)
        {
            bool down = !held[slot, (int)key];
            held[slot, (int)key] = down;
            keys.Add(new ScriptKey(tick, (byte)slot, (byte)rng.NextInt(OffsetCount), key, down));
        }
    }
}
