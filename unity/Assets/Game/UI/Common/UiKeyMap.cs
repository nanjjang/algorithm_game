#nullable enable
using System;
using Wizard.Client;

namespace Wizard.Unity.UI.Common
{
    // 메뉴 키 배치: 물리 키(자리, 한글 입력기와 무관) → UiKeyAction. 불변이다.
    // 기본 배치 [임시: spec 13 은 "방향키, J, K, Esc"만 적었다. OPEN-QUESTIONS]
    //   위·아래·왼쪽·오른쪽  방향키, 그리고 전투 기본 배치의 W·S·A·D(DEC 1.19)
    //   확인                 J, Enter, 숫자판 Enter
    //   빼기                 K
    //   나가기               Esc
    // 대체 배치(spec 13 13.4 Alternate: 방향키, Z, X)는 Alternate 다.
    public sealed class UiKeyMap
    {
        private readonly UiKeyAction[] _byKey;

        private UiKeyMap(UiKeyAction[] byKey)
        {
            _byKey = byKey;
        }

        public static readonly UiKeyMap Default = Build(
            new[] { PhysicalKey.UpArrow, PhysicalKey.W },
            new[] { PhysicalKey.DownArrow, PhysicalKey.S },
            new[] { PhysicalKey.LeftArrow, PhysicalKey.A },
            new[] { PhysicalKey.RightArrow, PhysicalKey.D },
            new[] { PhysicalKey.J, PhysicalKey.Enter, PhysicalKey.NumpadEnter },
            new[] { PhysicalKey.K },
            new[] { PhysicalKey.Escape });

        public static readonly UiKeyMap Alternate = Build(
            new[] { PhysicalKey.UpArrow },
            new[] { PhysicalKey.DownArrow },
            new[] { PhysicalKey.LeftArrow },
            new[] { PhysicalKey.RightArrow },
            new[] { PhysicalKey.Z, PhysicalKey.Enter, PhysicalKey.NumpadEnter },
            new[] { PhysicalKey.X },
            new[] { PhysicalKey.Escape });

        public UiKeyAction ActionOf(PhysicalKey key)
        {
            int i = (int)key;
            return i > 0 && i < _byKey.Length ? _byKey[i] : UiKeyAction.None;
        }

        public static UiKeyMap Build(PhysicalKey[] up, PhysicalKey[] down, PhysicalKey[] left, PhysicalKey[] right,
            PhysicalKey[] confirm, PhysicalKey[] secondary, PhysicalKey[] back)
        {
            var byKey = new UiKeyAction[KeyBindings.PhysicalKeyCapacity];
            Assign(byKey, up, UiKeyAction.Up);
            Assign(byKey, down, UiKeyAction.Down);
            Assign(byKey, left, UiKeyAction.Left);
            Assign(byKey, right, UiKeyAction.Right);
            Assign(byKey, confirm, UiKeyAction.Confirm);
            Assign(byKey, secondary, UiKeyAction.Secondary);
            Assign(byKey, back, UiKeyAction.Back);
            return new UiKeyMap(byKey);
        }

        private static void Assign(UiKeyAction[] byKey, PhysicalKey[] keys, UiKeyAction action)
        {
            foreach (PhysicalKey k in keys ?? throw new ArgumentNullException(nameof(keys)))
            {
                int i = (int)k;
                if (i <= 0 || i >= byKey.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(keys), k, "not a physical key");
                }
                if (byKey[i] != UiKeyAction.None)
                {
                    throw new ArgumentException("key " + k + " is bound twice", nameof(keys));
                }
                byKey[i] = action;
            }
        }
    }
}
