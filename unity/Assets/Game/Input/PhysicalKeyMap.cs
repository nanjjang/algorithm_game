using System;
using UnityEngine.InputSystem;
using Wizard.Client;

namespace Wizard.Unity.Input
{
    // Input System 의 Key ↔ 엔진 없는 PhysicalKey 번호(src/Wizard.Client/Input/PhysicalKey.cs).
    // Key 는 글자가 아니라 키보드 위의 자리다(미국 배열 이름을 붙였을 뿐 배열·입력기와 무관하다). 그래서 한글 입력기가 켜져 있어도
    // W 자리를 누르면 Key.W 다(spec 01 4.2, T-28). 글자(onTextInput, IME 조합)는 보지 않는다.
    // PhysicalKey 의 모든 값(None 제외)이 서로 다른 Key 하나에 대응한다. 표는 불변이다.
    public static class PhysicalKeyMap
    {
        private static readonly Key[] KeyByPhysical = BuildKeyByPhysical();
        private static readonly PhysicalKey[] PhysicalByKey = BuildPhysicalByKey(KeyByPhysical);

        // 대응이 있는 PhysicalKey 의 개수(None 제외)
        public static readonly int MappedCount = CountMapped(KeyByPhysical);

        // 없으면 Key.None
        public static Key ToKey(PhysicalKey physical)
        {
            int i = (int)physical;
            return i < KeyByPhysical.Length ? KeyByPhysical[i] : Key.None;
        }

        // 대응이 없는 키(OEM 키, 입력기 전용 키 등)는 거짓
        public static bool TryToPhysical(Key key, out PhysicalKey physical)
        {
            int i = (int)key;
            physical = i >= 0 && i < PhysicalByKey.Length ? PhysicalByKey[i] : PhysicalKey.None;
            return physical != PhysicalKey.None;
        }

        private static Key[] BuildKeyByPhysical()
        {
            var t = new Key[KeyBindings.PhysicalKeyCapacity];
            void Map(PhysicalKey p, Key k) => t[(int)p] = k;

            Map(PhysicalKey.A, Key.A); Map(PhysicalKey.B, Key.B); Map(PhysicalKey.C, Key.C); Map(PhysicalKey.D, Key.D);
            Map(PhysicalKey.E, Key.E); Map(PhysicalKey.F, Key.F); Map(PhysicalKey.G, Key.G); Map(PhysicalKey.H, Key.H);
            Map(PhysicalKey.I, Key.I); Map(PhysicalKey.J, Key.J); Map(PhysicalKey.K, Key.K); Map(PhysicalKey.L, Key.L);
            Map(PhysicalKey.M, Key.M); Map(PhysicalKey.N, Key.N); Map(PhysicalKey.O, Key.O); Map(PhysicalKey.P, Key.P);
            Map(PhysicalKey.Q, Key.Q); Map(PhysicalKey.R, Key.R); Map(PhysicalKey.S, Key.S); Map(PhysicalKey.T, Key.T);
            Map(PhysicalKey.U, Key.U); Map(PhysicalKey.V, Key.V); Map(PhysicalKey.W, Key.W); Map(PhysicalKey.X, Key.X);
            Map(PhysicalKey.Y, Key.Y); Map(PhysicalKey.Z, Key.Z);

            Map(PhysicalKey.Digit0, Key.Digit0); Map(PhysicalKey.Digit1, Key.Digit1); Map(PhysicalKey.Digit2, Key.Digit2);
            Map(PhysicalKey.Digit3, Key.Digit3); Map(PhysicalKey.Digit4, Key.Digit4); Map(PhysicalKey.Digit5, Key.Digit5);
            Map(PhysicalKey.Digit6, Key.Digit6); Map(PhysicalKey.Digit7, Key.Digit7); Map(PhysicalKey.Digit8, Key.Digit8);
            Map(PhysicalKey.Digit9, Key.Digit9);

            Map(PhysicalKey.UpArrow, Key.UpArrow); Map(PhysicalKey.DownArrow, Key.DownArrow);
            Map(PhysicalKey.LeftArrow, Key.LeftArrow); Map(PhysicalKey.RightArrow, Key.RightArrow);

            Map(PhysicalKey.LeftShift, Key.LeftShift); Map(PhysicalKey.RightShift, Key.RightShift);
            Map(PhysicalKey.LeftCtrl, Key.LeftCtrl); Map(PhysicalKey.RightCtrl, Key.RightCtrl);
            Map(PhysicalKey.LeftAlt, Key.LeftAlt); Map(PhysicalKey.RightAlt, Key.RightAlt);
            Map(PhysicalKey.LeftMeta, Key.LeftMeta); Map(PhysicalKey.RightMeta, Key.RightMeta);

            Map(PhysicalKey.Space, Key.Space); Map(PhysicalKey.Enter, Key.Enter); Map(PhysicalKey.Tab, Key.Tab);
            Map(PhysicalKey.Escape, Key.Escape); Map(PhysicalKey.Backspace, Key.Backspace);
            Map(PhysicalKey.Backquote, Key.Backquote); Map(PhysicalKey.Minus, Key.Minus); Map(PhysicalKey.Equals, Key.Equals);
            Map(PhysicalKey.LeftBracket, Key.LeftBracket); Map(PhysicalKey.RightBracket, Key.RightBracket);
            Map(PhysicalKey.Backslash, Key.Backslash); Map(PhysicalKey.Semicolon, Key.Semicolon); Map(PhysicalKey.Quote, Key.Quote);
            Map(PhysicalKey.Comma, Key.Comma); Map(PhysicalKey.Period, Key.Period); Map(PhysicalKey.Slash, Key.Slash);

            Map(PhysicalKey.F1, Key.F1); Map(PhysicalKey.F2, Key.F2); Map(PhysicalKey.F3, Key.F3); Map(PhysicalKey.F4, Key.F4);
            Map(PhysicalKey.F5, Key.F5); Map(PhysicalKey.F6, Key.F6); Map(PhysicalKey.F7, Key.F7); Map(PhysicalKey.F8, Key.F8);
            Map(PhysicalKey.F9, Key.F9); Map(PhysicalKey.F10, Key.F10); Map(PhysicalKey.F11, Key.F11); Map(PhysicalKey.F12, Key.F12);

            Map(PhysicalKey.Numpad0, Key.Numpad0); Map(PhysicalKey.Numpad1, Key.Numpad1); Map(PhysicalKey.Numpad2, Key.Numpad2);
            Map(PhysicalKey.Numpad3, Key.Numpad3); Map(PhysicalKey.Numpad4, Key.Numpad4); Map(PhysicalKey.Numpad5, Key.Numpad5);
            Map(PhysicalKey.Numpad6, Key.Numpad6); Map(PhysicalKey.Numpad7, Key.Numpad7); Map(PhysicalKey.Numpad8, Key.Numpad8);
            Map(PhysicalKey.Numpad9, Key.Numpad9);
            Map(PhysicalKey.NumpadDivide, Key.NumpadDivide); Map(PhysicalKey.NumpadMultiply, Key.NumpadMultiply);
            Map(PhysicalKey.NumpadMinus, Key.NumpadMinus); Map(PhysicalKey.NumpadPlus, Key.NumpadPlus);
            Map(PhysicalKey.NumpadEnter, Key.NumpadEnter); Map(PhysicalKey.NumpadPeriod, Key.NumpadPeriod);

            Map(PhysicalKey.Insert, Key.Insert); Map(PhysicalKey.Delete, Key.Delete); Map(PhysicalKey.Home, Key.Home);
            Map(PhysicalKey.End, Key.End); Map(PhysicalKey.PageUp, Key.PageUp); Map(PhysicalKey.PageDown, Key.PageDown);
            Map(PhysicalKey.CapsLock, Key.CapsLock);
            return t;
        }

        private static PhysicalKey[] BuildPhysicalByKey(Key[] keyByPhysical)
        {
            int size = 0;
            foreach (Key k in keyByPhysical)
            {
                if ((int)k + 1 > size)
                {
                    size = (int)k + 1;
                }
            }
            var t = new PhysicalKey[size];
            for (int p = 1; p < keyByPhysical.Length; p++)
            {
                Key k = keyByPhysical[p];
                if (k == Key.None)
                {
                    continue;
                }
                if (t[(int)k] != PhysicalKey.None)
                {
                    throw new InvalidOperationException("PhysicalKeyMap: two physical keys share one Input System key");
                }
                t[(int)k] = (PhysicalKey)p;
            }
            return t;
        }

        private static int CountMapped(Key[] keyByPhysical)
        {
            int n = 0;
            for (int p = 1; p < keyByPhysical.Length; p++)
            {
                if (keyByPhysical[p] != Key.None)
                {
                    n++;
                }
            }
            return n;
        }
    }
}
