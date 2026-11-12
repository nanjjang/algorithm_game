namespace Wizard.Client
{
    // 키보드의 물리 키 자리(spec 01 4.1·4.2, spec 13 13.4). 글자가 아니라 자리라서 한글 입력기 상태와 무관하다.
    // 엔진 없는 번호 공간이다. 유니티 쪽 입력원(M1-UE-01)이 Input System의 키를 이 번호로 옮긴다.
    // 키 배치가 설정에 저장되므로 번호는 더하기만 하고 바꾸거나 다시 쓰지 않는다(CLAUDE.md 5절).
    public enum PhysicalKey : ushort
    {
        None = 0,

        A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8, I = 9, J = 10, K = 11, L = 12, M = 13,
        N = 14, O = 15, P = 16, Q = 17, R = 18, S = 19, T = 20, U = 21, V = 22, W = 23, X = 24, Y = 25, Z = 26,

        Digit0 = 27, Digit1 = 28, Digit2 = 29, Digit3 = 30, Digit4 = 31,
        Digit5 = 32, Digit6 = 33, Digit7 = 34, Digit8 = 35, Digit9 = 36,

        UpArrow = 37, DownArrow = 38, LeftArrow = 39, RightArrow = 40,

        LeftShift = 41, RightShift = 42, LeftCtrl = 43, RightCtrl = 44,
        LeftAlt = 45, RightAlt = 46, LeftMeta = 47, RightMeta = 48,

        Space = 49, Enter = 50, Tab = 51, Escape = 52, Backspace = 53,
        Backquote = 54, Minus = 55, Equals = 56, LeftBracket = 57, RightBracket = 58,
        Backslash = 59, Semicolon = 60, Quote = 61, Comma = 62, Period = 63, Slash = 64,

        F1 = 65, F2 = 66, F3 = 67, F4 = 68, F5 = 69, F6 = 70,
        F7 = 71, F8 = 72, F9 = 73, F10 = 74, F11 = 75, F12 = 76,

        Numpad0 = 77, Numpad1 = 78, Numpad2 = 79, Numpad3 = 80, Numpad4 = 81,
        Numpad5 = 82, Numpad6 = 83, Numpad7 = 84, Numpad8 = 85, Numpad9 = 86,
        NumpadDivide = 87, NumpadMultiply = 88, NumpadMinus = 89, NumpadPlus = 90, NumpadEnter = 91, NumpadPeriod = 92,

        Insert = 93, Delete = 94, Home = 95, End = 96, PageUp = 97, PageDown = 98, CapsLock = 99,
    }
}
