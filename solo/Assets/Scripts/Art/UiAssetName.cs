using System;
using System.Globalization;

namespace Wizard.Solo.Art
{
    /// <summary>
    /// UI 파일 이름 → (변형, 상태). 09 요약서 6.1절, S14 TV-188~190. 원 구현 src/Wizard.ViewLogic/Assets/UiAssetName.cs와 같은 규칙:
    /// "&lt;id 소문자&gt;_"를 떼고, manifest의 width×height로 크기 토큰 "_&lt;w&gt;x&lt;h&gt;"의 자리를 찾아 앞 = 변형, 뒤 토큰 = 상태
    /// (프레임 토큰 "_fN"은 빼고, 없으면 normal).
    /// </summary>
    public static class UiAssetName
    {
        public const string DefaultState = "normal";
        const string PngExtension = ".png";
        const char Sep = '_';

        public static bool TrySplit(string id, string fileName, int width, int height, out string variant, out string state)
        {
            variant = "";
            state = "";
            if (id == null || fileName == null) return false;
            string name = fileName.EndsWith(PngExtension, StringComparison.Ordinal)
                ? fileName.Substring(0, fileName.Length - PngExtension.Length)
                : fileName;
            string prefix = id.ToLowerInvariant() + Sep;
            if (!name.StartsWith(prefix, StringComparison.Ordinal)) return false;
            string rest = name.Substring(prefix.Length);
            string sizeToken = Sep + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture);
            int at = -1;
            int from = rest.Length;
            while (from > 0)
            {
                int i = rest.LastIndexOf(sizeToken, from - 1, StringComparison.Ordinal);
                if (i < 0) break;
                int end = i + sizeToken.Length;
                if (end == rest.Length || rest[end] == Sep)
                {
                    at = i;
                    break;
                }
                from = i;
            }
            if (at <= 0) return false;
            variant = rest.Substring(0, at);
            string tail = rest.Substring(at + sizeToken.Length);
            string[] tokens = tail.Split(new[] { Sep }, StringSplitOptions.RemoveEmptyEntries);
            int count = tokens.Length;
            if (count > 0 && IsFrameToken(tokens[count - 1])) count--;
            state = count == 0 ? DefaultState : string.Join(Sep.ToString(), tokens, 0, count);
            return true;
        }

        static bool IsFrameToken(string token)
        {
            if (token.Length < 2 || token[0] != 'f') return false;
            for (int i = 1; i < token.Length; i++)
                if (token[i] < '0' || token[i] > '9') return false;
            return true;
        }
    }

    /// <summary>
    /// 버튼·탭 상태 고르기(S14 4.3, 09 6.1절): !Enabled → disabled &gt; Pressed → pressed &gt; On → on &gt; Hover → hover &gt; normal.
    /// 고른 상태의 그림이 없으면 Fallback 순서로 내려간다(예 K-26에는 disabled가 없다).
    /// </summary>
    public static class UiStates
    {
        public const string Normal = "normal", Hover = "hover", Pressed = "pressed", Disabled = "disabled", On = "on";

        public static string Resolve(bool enabled, bool pressed, bool on, bool hover)
        {
            if (!enabled) return Disabled;
            if (pressed) return Pressed;
            if (on) return On;
            if (hover) return Hover;
            return Normal;
        }

        /// <summary>그 상태가 없을 때 다음으로 볼 상태(normal의 다음은 null). pressed → hover → normal, on → normal, disabled → normal.</summary>
        public static string Fallback(string state)
        {
            switch (state)
            {
                case Pressed: return Hover;
                case Hover: return Normal;
                case On: return Normal;
                case Disabled: return Normal;
                case Normal: return null;
                default: return Normal;
            }
        }
    }
}
