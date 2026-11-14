using System.Text;
using Wizard.Solo.Meta;

namespace Wizard.Solo.Screens.Prep
{
    /// <summary>오류 코드 → 이름 표 열쇠 "error.&lt;코드 이름 snake_case&gt;"(MetaContracts의 ErrorCode 주석, 07 요약서 11절).</summary>
    public static class PrepErrorKeys
    {
        public static string Of(ErrorCode code)
        {
            if (code == ErrorCode.None) return null;
            string name = code.ToString();
            var sb = new StringBuilder("error.", name.Length + 12);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (c >= 'A' && c <= 'Z')
                {
                    if (i > 0) sb.Append('_');
                    sb.Append((char)(c - 'A' + 'a'));
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
