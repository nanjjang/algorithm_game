using System;
using System.Globalization;

namespace Wizard.ViewLogic.Assets
{
    // SplitUiName(spec 14 5.2절 [임시]): 같은 UI Id 안에서 파일을 고르는 변형 열쇠.
    //   Variant = 파일 이름에서 "<id 소문자>_"를 떼고 "_<w>x<h>"와 그 뒤를 뗀 앞부분
    //   State   = 크기 뒤 토큰(프레임 토큰 "_f<N>"은 빼고), 없으면 normal
    // 로더가 파일 이름을 해석하는 곳은 여기 한 곳뿐이고 불러올 때 한 번만 한다(4.4절 C-11, 카드 M9-AST-01).
    // 크기와 프레임 수는 이름에서 읽지 않는다. 매니페스트의 width·height 로 크기 토큰의 자리를 찾을 뿐이다.
    public static class UiAssetName
    {
        public const string DefaultState = "normal";
        public const string PngExtension = ".png";
        private const char Sep = '_';
        private const char SizeSep = 'x';
        private const char FrameToken = 'f';

        // k-20_button_large_48x64_hover.png → (button_large, hover)
        // i-b1_block_fireball_48x48.png     → (block_fireball, normal)
        // k-40_selection_corner_24x24_f2.png → (selection_corner, normal)
        public static bool TrySplit(string id, string fileName, int width, int height, out string variant, out string state)
        {
            variant = "";
            state = "";
            string name = fileName.EndsWith(PngExtension, StringComparison.Ordinal)
                ? fileName.Substring(0, fileName.Length - PngExtension.Length)
                : fileName;
            string prefix = id.ToLowerInvariant() + Sep;
            if (!name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
            string rest = name.Substring(prefix.Length);
            string sizeToken = Sep + width.ToString(CultureInfo.InvariantCulture) + SizeSep + height.ToString(CultureInfo.InvariantCulture);
            int at = -1;
            int from = rest.Length;
            while (from > 0)
            {
                int i = rest.LastIndexOf(sizeToken, from - 1, StringComparison.Ordinal);
                if (i < 0)
                {
                    break;
                }
                int end = i + sizeToken.Length;
                if (end == rest.Length || rest[end] == Sep)
                {
                    at = i;
                    break;
                }
                from = i;
            }
            if (at <= 0)
            {
                return false;
            }
            variant = rest.Substring(0, at);
            string tail = rest.Substring(at + sizeToken.Length);
            string[] tokens = tail.Split(new[] { Sep }, StringSplitOptions.RemoveEmptyEntries);
            int count = tokens.Length;
            if (count > 0 && IsFrameToken(tokens[count - 1]))
            {
                count--;
            }
            state = count == 0 ? DefaultState : string.Join(Sep.ToString(), tokens, 0, count);
            return true;
        }

        // "f2", "f12"
        private static bool IsFrameToken(string token)
        {
            if (token.Length < 2 || token[0] != FrameToken)
            {
                return false;
            }
            for (int i = 1; i < token.Length; i++)
            {
                if (token[i] < '0' || token[i] > '9')
                {
                    return false;
                }
            }
            return true;
        }
    }
}
