using Wizard.Core;
using Wizard.Sim.Arts;

namespace Wizard.Sim.Events
{
    // CONTRACTS 3.3, spec 03 21.1(cmd.dir_input의 dir·chained). HUD의 "최근 입력 5개".
    // 참조 없는 값이다: 방향은 2비트씩, 이어짐은 1비트씩 정수 하나에 담는다. 0번이 가장 최근
    public readonly struct RecentInputs
    {
        public const int Capacity = 5;
        private const int DirBits = 2;
        private const int DirMask = 3;
        private const int AllDirs = (1 << (DirBits * Capacity)) - 1;
        private const int AllChained = (1 << Capacity) - 1;

        public readonly int Count;
        private readonly int _dirs;      // i번 = 비트 [2i, 2i+1]
        private readonly int _chained;   // i번 = 비트 i

        private RecentInputs(int count, int dirs, int chained)
        {
            Count = count;
            _dirs = dirs;
            _chained = chained;
        }

        public static RecentInputs Empty => default;

        public CmdDir Dir(int index) => (CmdDir)((_dirs >> (CheckIndex(index) * DirBits)) & DirMask);

        public bool Chained(int index) => ((_chained >> CheckIndex(index)) & 1) != 0;   // 앞 입력과 이어짐(간격 안)

        // 새 입력을 0번에 넣고 나머지를 한 칸씩 민다. 5개를 넘으면 가장 오래된 것이 빠진다
        public RecentInputs Push(CmdDir dir, bool chained)
        {
            int d = (int)dir;
            if (d < 0 || d > DirMask)
            {
                throw new SimInvariantException("RecentInputs: bad CmdDir");
            }
            int dirs = ((_dirs << DirBits) | d) & AllDirs;
            int ch = ((_chained << 1) | (chained ? 1 : 0)) & AllChained;
            int count = Count < Capacity ? Count + 1 : Capacity;
            return new RecentInputs(count, dirs, ch);
        }

        private int CheckIndex(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new SimInvariantException("RecentInputs: index out of range");
            }
            return index;
        }
    }
}
