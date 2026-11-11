using System;

namespace Wizard.Core
{
    // CONTRACTS 1.5, spec 14 1.1. "block.fireball" 같은 문자열 id.
    // 꼴: ^[a-z][a-z0-9_]*(\.[a-z0-9][a-z0-9_]*)+$, 64자 이하. 정규식 엔진에 기대지 않고 글자 단위로 검사한다.
    public readonly struct ContentId : IEquatable<ContentId>
    {
        public const int MaxLength = 64;

        public readonly string Value;

        private ContentId(string value)
        {
            Value = value;
        }

        public static bool TryParse(string s, out ContentId id)
        {
            id = default;
            if (s == null || s.Length == 0 || s.Length > MaxLength || !IsLower(s[0]))
            {
                return false;
            }
            int segments = 0;
            bool segmentStart = false;
            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '.')
                {
                    if (segmentStart)
                    {
                        return false;   // 빈 마디
                    }
                    segmentStart = true;
                    segments++;
                    continue;
                }
                if (segmentStart)
                {
                    if (!IsLower(c) && !IsDigit(c))
                    {
                        return false;
                    }
                    segmentStart = false;
                    continue;
                }
                if (!IsLower(c) && !IsDigit(c) && c != '_')
                {
                    return false;
                }
            }
            if (segments == 0 || segmentStart)
            {
                return false;
            }
            id = new ContentId(s);
            return true;
        }

        public bool Equals(ContentId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is ContentId other && Equals(other);

        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value ?? string.Empty;

        private static bool IsLower(char c) => c >= 'a' && c <= 'z';

        private static bool IsDigit(char c) => c >= '0' && c <= '9';
    }
}
