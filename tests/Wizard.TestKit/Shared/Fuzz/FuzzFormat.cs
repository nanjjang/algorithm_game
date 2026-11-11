using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wizard.TestKit.Shared.Fuzz
{
    // 보고용 입력 표기
    public static class FuzzFormat
    {
        public const int MaxBytesShown = 256;

        // "[3 bytes] 0a ff 2a". 길면 앞 MaxBytesShown 바이트만 보이고 남은 수를 적는다
        public static string Hex(byte[] data)
        {
            var sb = new StringBuilder();
            sb.Append('[').Append(data.Length.ToString(CultureInfo.InvariantCulture)).Append(" bytes]");
            int n = data.Length < MaxBytesShown ? data.Length : MaxBytesShown;
            for (int i = 0; i < n; i++)
            {
                sb.Append(' ').Append(data[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            if (data.Length > n)
            {
                sb.Append(" …(+").Append((data.Length - n).ToString(CultureInfo.InvariantCulture)).Append(')');
            }
            return sb.ToString();
        }

        public static string Int64(long v) => v.ToString(CultureInfo.InvariantCulture);

        public static string Int32(int v) => v.ToString(CultureInfo.InvariantCulture);

        public static string List<T>(List<T> items, System.Func<T, string> element)
        {
            var parts = new List<string>(items.Count);
            foreach (T t in items)
            {
                parts.Add(element(t));
            }
            return "[" + string.Join(", ", parts) + "]";
        }
    }
}
