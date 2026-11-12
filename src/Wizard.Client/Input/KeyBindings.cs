using System;
using Wizard.Sim.Input;

namespace Wizard.Client
{
    // CONTRACTS 6.5. 물리 키 → 논리 키 배치. 논리 키마다 자리 둘(spec 13 13.4의 primary·secondary).
    // 불변이다. 바꾸면 새 배치가 나오고 IInputSource.SetBindings로 넘긴다(입력원이 들고 있는 배치를 밖에서 고치지 못하게).
    // 겹침 검사와 금지 키(spec 13 13.4 ValidateConflicts)는 설정 쪽(M9-SET-01)이 한다. 여기서는 한 물리 키가 여러 논리 키에
    // 묶여 있어도 그대로 받아들이고, 눌림은 묶인 논리 키 전부에 간다.
    public sealed class KeyBindings
    {
        public const int SlotsPerKey = 2;                 // 0 = primary, 1 = secondary
        public const int PrimarySlot = 0;
        public const int SecondarySlot = 1;
        public const int PhysicalKeyCapacity = 128;       // PhysicalKey 번호의 상한(미만). 입력원의 눌림 비트 크기

        private readonly PhysicalKey[] _keys;             // [논리 키 × 자리]

        // DECISIONS 1.19 [확정]: WASD 이동, J 발사, K 비기, Shift 자세, Space 반전 자세.
        // 일시정지 Esc는 spec 13 13.4의 [임시] 값. 오른쪽 Shift를 자세의 둘째 자리에 둔 것도 [임시]다(표의 "Shift"를 양쪽으로 읽음)
        public static readonly KeyBindings Default = new KeyBindings(new[]
        {
            PhysicalKey.W, PhysicalKey.None,              // Up
            PhysicalKey.A, PhysicalKey.None,              // Left
            PhysicalKey.S, PhysicalKey.None,              // Down
            PhysicalKey.D, PhysicalKey.None,              // Right
            PhysicalKey.J, PhysicalKey.None,              // Fire
            PhysicalKey.K, PhysicalKey.None,              // Skill
            PhysicalKey.LeftShift, PhysicalKey.RightShift, // Stance
            PhysicalKey.Space, PhysicalKey.None,          // Reverse
            PhysicalKey.Escape, PhysicalKey.None,         // Pause
        });

        // spec 01 4.1, spec 13 13.4 [제안]: 방향키 + Z 발사 / X 비기 / C 자세 / V 반전. 일시정지 Esc [임시]
        public static readonly KeyBindings Alternate = new KeyBindings(new[]
        {
            PhysicalKey.UpArrow, PhysicalKey.None,
            PhysicalKey.LeftArrow, PhysicalKey.None,
            PhysicalKey.DownArrow, PhysicalKey.None,
            PhysicalKey.RightArrow, PhysicalKey.None,
            PhysicalKey.Z, PhysicalKey.None,
            PhysicalKey.X, PhysicalKey.None,
            PhysicalKey.C, PhysicalKey.None,
            PhysicalKey.V, PhysicalKey.None,
            PhysicalKey.Escape, PhysicalKey.None,
        });

        private KeyBindings(PhysicalKey[] keys)
        {
            if (keys.Length != KeyState.KeyCount * SlotsPerKey)
            {
                throw new ArgumentException("bindings table size", nameof(keys));
            }
            foreach (PhysicalKey p in keys)
            {
                CheckPhysical(p);
            }
            _keys = keys;
        }

        // 빈 배치(모든 자리 None). 설정 쪽이 줄마다 채울 때 쓴다
        public static KeyBindings Empty() => new KeyBindings(new PhysicalKey[KeyState.KeyCount * SlotsPerKey]);

        public PhysicalKey Get(LogicalKey key, int slot) => _keys[Index(key, slot)];

        // 한 자리만 바꾼 새 배치. None이면 그 자리를 비운다
        public KeyBindings With(LogicalKey key, int slot, PhysicalKey physical)
        {
            int i = Index(key, slot);
            CheckPhysical(physical);
            PhysicalKey[] copy = (PhysicalKey[])_keys.Clone();
            copy[i] = physical;
            return new KeyBindings(copy);
        }

        // 이 물리 키가 묶인 논리 키들(비트 번호 = (int)LogicalKey). None은 언제나 비어 있다
        public KeyMask MaskFor(PhysicalKey physical)
        {
            CheckPhysical(physical);
            if (physical == PhysicalKey.None)
            {
                return KeyMask.None;
            }
            int mask = 0;
            for (int i = 0; i < _keys.Length; i++)
            {
                if (_keys[i] == physical)
                {
                    mask |= 1 << (i / SlotsPerKey);
                }
            }
            return (KeyMask)mask;
        }

        private static int Index(LogicalKey key, int slot)
        {
            int k = (int)key;
            if (k < 0 || k >= KeyState.KeyCount)
            {
                throw new ArgumentOutOfRangeException(nameof(key));
            }
            if (slot < 0 || slot >= SlotsPerKey)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
            return k * SlotsPerKey + slot;
        }

        internal static void CheckPhysical(PhysicalKey physical)
        {
            if ((int)physical >= PhysicalKeyCapacity)
            {
                throw new ArgumentOutOfRangeException(nameof(physical));
            }
        }
    }
}
