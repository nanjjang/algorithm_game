using System;
using System.Collections.Generic;

namespace Wizard.BalanceLab.Core
{
    // 명령줄 인자: 위치 인자와 `--이름 [값]`. 값을 받는 옵션과 깃발을 부르는 쪽이 미리 알려 준다(모르는 옵션은 오류).
    internal sealed class CliArgs
    {
        private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

        public List<string> Positionals { get; } = new List<string>();

        public static bool TryParse(string[] args, ISet<string> valueOptions, ISet<string> flagOptions, out CliArgs parsed, out string error)
        {
            parsed = new CliArgs();
            error = string.Empty;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (!a.StartsWith("--", StringComparison.Ordinal))
                {
                    parsed.Positionals.Add(a);
                    continue;
                }
                string name = a.Substring(2);
                if (flagOptions.Contains(name))
                {
                    parsed._flags.Add(name);
                }
                else if (valueOptions.Contains(name))
                {
                    if (i + 1 >= args.Length)
                    {
                        error = "option --" + name + " needs a value";
                        return false;
                    }
                    parsed._values[name] = args[++i];
                }
                else
                {
                    error = "unknown option --" + name;
                    return false;
                }
            }
            return true;
        }

        public bool Has(string flag) => _flags.Contains(flag);

        public bool TryGet(string name, out string value) => _values.TryGetValue(name, out value!);

        // 음이 아닌 정수 옵션. 없으면 fallback. 형식이 틀리면 false
        public bool TryGetUInt(string name, long fallback, out long value, out string error)
        {
            error = string.Empty;
            value = fallback;
            if (!_values.TryGetValue(name, out string? text))
            {
                return true;
            }
            if (!long.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                error = "option --" + name + " needs a non-negative integer, got '" + text + "'";
                return false;
            }
            return true;
        }
    }
}
