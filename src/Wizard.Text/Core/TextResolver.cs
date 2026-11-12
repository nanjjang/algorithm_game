using System;
using System.Collections.Generic;
using System.Text;

namespace Wizard.Text
{
    // CONTRACTS 5절 ITextResolver 의 구현. spec 14 2.1·2.2·2.4절.
    //
    // 틀 문법(spec 14 2.4):
    //   Template := ( Text | Ref | Josa )*
    //   Ref      := "{" NameKey "}" | "{" ParamName "}"     호출자가 준 값이 먼저, 없으면 이름 표(별칭을 푼 뒤)
    //   Josa     := "{" A "/" B "}"                          바로 앞 Ref 값이 받침으로 끝나면 A, 아니면 B (이/가, 을/를, 은/는, 과/와 …)
    //             | "{" "(" A ")" B "}"                       받침이 있고 ㄹ 받침이 아니면 A+B, 아니면 B. spec 의 특수형 (으)로 가 이 꼴이다
    //   Text     := 그 밖의 글자. "{{" 와 "}}" 는 글자 그대로의 { }
    // 조사의 글자는 틀에 적힌 것을 그대로 쓴다(코드에 한글 문구를 두지 않는다, CLAUDE.md 5절).
    //
    // 경계 [임시]:
    //   - 바로 앞이 Ref 가 아니거나(글자, 조사, 틀의 처음) 앞 Ref 값이 빈 문자열이면 받침형(A, A+B)을 쓴다.
    //   - 이름 표 값이 틀이면 한 단계까지 풀어 넣는다. 그 안에서 또 틀을 만나면(2단계) 열쇠를 그대로 찍고 기록한다.
    //   - 호출자가 준 값({n}, 닉네임 등)도 spec 14 2.4 의사코드대로 "{" 가 있으면 같은 깊이 한도로 한 단계 푼다
    //     (`v = ResolveTemplate(v, params) if v contains "{"`). 닉네임은 한글 음절만이라(spec 12 U-25) "{" 가 없다.
    //     플레이어가 자유롭게 적은 글(대화, 쪽지)을 값으로 넘길 때는 호출자가 "{" 를 "{{" 로 바꿔 넘긴다.
    //   - 닫히지 않은 "{" 는 거기서부터 끝까지 글자 그대로, 짝 없는 "}" 하나는 글자 그대로다.
    //
    // 없는 열쇠(spec 14 2.2 경계, TV-080): 열쇠 문자열을 그대로 돌려준다. 회청으로 칠하는 것은 표시 계층이고,
    // 칠할지 알 수 있게 TryName 을 둔다. 기록은 생성자에 받은 log 로 열쇠마다 한 번만 한다.
    // 개발 빌드만 log 를 넘긴다(#if 로 가르지 않는다, CLAUDE.md 4절). log 가 null 이면 기록하지 않는다.
    // 한 개체는 한 스레드(화면 스레드)에서 쓴다. 기록 중복 거르기만 잠근다.
    public sealed class TextResolver : ITextResolver
    {
        private const int MaxNestedDepth = 1;   // spec 14 2.4 "1단계 중첩 허용, 2단계 이상은 오류" [임시]

        private readonly NameTable _table;
        private readonly Action<string>? _log;
        private readonly HashSet<string> _logged = new HashSet<string>(StringComparer.Ordinal);
        private readonly object _logLock = new object();

        public TextResolver(NameTable table, Action<string>? log = null)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
            _log = log;
        }

        public NameTable Table => _table;

        // 이름 표의 값. 값이 틀이면 풀어서 돌려준다(호출자 값 없이). 없으면 열쇠 그대로
        public string Name(string nameKey)
        {
            TryName(nameKey, out string text);
            return text;
        }

        // Name 과 같고, 이름 표에 열쇠가 있었는지를 돌려준다(false 면 표시 계층이 회청으로 찍는다. spec 14 2.2)
        public bool TryName(string nameKey, out string text)
        {
            if (nameKey == null)
            {
                throw new ArgumentNullException(nameof(nameKey));
            }
            if (!_table.TryGet(nameKey, out string value))
            {
                ReportMissing(nameKey);
                text = nameKey;
                return false;
            }
            text = NeedsParse(value) ? Resolve(value, null, 0) : value;
            return true;
        }

        public string Format(string templateKey, IReadOnlyDictionary<string, string>? args = null)
        {
            if (templateKey == null)
            {
                throw new ArgumentNullException(nameof(templateKey));
            }
            if (!_table.TryGet(templateKey, out string template))
            {
                ReportMissing(templateKey);
                return templateKey;
            }
            return Resolve(template, args, 0);
        }

        public string ResolveTemplate(string template, IReadOnlyDictionary<string, string>? args)
        {
            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }
            return Resolve(template, args, 0);
        }

        private string Resolve(string template, IReadOnlyDictionary<string, string>? args, int depth)
        {
            var sb = new StringBuilder(template.Length + 16);
            string? last = null;    // 바로 앞 Ref 가 낸 문자열. 글자나 조사가 오면 지운다
            int n = template.Length;
            int i = 0;
            while (i < n)
            {
                char c = template[i];
                if (c == '{')
                {
                    if (i + 1 < n && template[i + 1] == '{')
                    {
                        sb.Append('{');
                        last = null;
                        i += 2;
                        continue;
                    }
                    int close = template.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        sb.Append(template, i, n - i);
                        last = null;
                        break;
                    }
                    string inner = template.Substring(i + 1, close - i - 1);
                    i = close + 1;
                    if (TryAppendJosa(sb, inner, last))
                    {
                        last = null;
                        continue;
                    }
                    string v = ResolveRef(inner, args, depth);
                    sb.Append(v);
                    last = v.Length == 0 ? null : v;
                    continue;
                }
                if (c == '}')
                {
                    sb.Append('}');
                    last = null;
                    i += i + 1 < n && template[i + 1] == '}' ? 2 : 1;
                    continue;
                }
                int next = NextBrace(template, i);
                sb.Append(template, i, next - i);
                last = null;
                i = next;
            }
            return sb.ToString();
        }

        private string ResolveRef(string key, IReadOnlyDictionary<string, string>? args, int depth)
        {
            string value;
            if (args != null && args.TryGetValue(key, out string? given))
            {
                // spec 14 2.4: 호출자 값은 "{" 가 있을 때만 다시 푼다
                value = given ?? string.Empty;
                if (value.IndexOf('{') < 0)
                {
                    return value;
                }
            }
            else
            {
                if (!_table.TryGet(key, out value))
                {
                    ReportMissing(key);
                    return key;
                }
                if (!NeedsParse(value))
                {
                    return value;
                }
            }
            if (depth >= MaxNestedDepth)
            {
                Report("Wizard.Text: template nested deeper than " + MaxNestedDepth.ToString(System.Globalization.CultureInfo.InvariantCulture)
                       + " level at '" + key + "'");
                return key;
            }
            return Resolve(value, args, depth + 1);
        }

        // {A/B} 또는 {(A)B}. 조사가 아니면 false(그때는 Ref 다)
        private static bool TryAppendJosa(StringBuilder sb, string inner, string? last)
        {
            if (inner.Length > 0 && inner[0] == '(')
            {
                int closeParen = inner.IndexOf(')');
                if (closeParen > 1 && closeParen < inner.Length - 1)
                {
                    bool insert = true;   // 앞 Ref 가 없으면 받침형 [임시]
                    if (last != null)
                    {
                        Josa.FinalSound(last, out bool has, out bool rieul);
                        insert = has && !rieul;
                    }
                    if (insert)
                    {
                        sb.Append(inner, 1, closeParen - 1);
                    }
                    sb.Append(inner, closeParen + 1, inner.Length - closeParen - 1);
                    return true;
                }
                return false;
            }
            int slash = inner.IndexOf('/');
            if (slash < 0)
            {
                return false;
            }
            bool withFinal = true;
            if (last != null)
            {
                Josa.FinalSound(last, out bool has, out _);
                withFinal = has;
            }
            if (withFinal)
            {
                sb.Append(inner, 0, slash);
            }
            else
            {
                sb.Append(inner, slash + 1, inner.Length - slash - 1);
            }
            return true;
        }

        private static bool NeedsParse(string s) => s.IndexOf('{') >= 0 || s.IndexOf('}') >= 0;

        private static int NextBrace(string s, int from)
        {
            for (int k = from; k < s.Length; k++)
            {
                if (s[k] == '{' || s[k] == '}')
                {
                    return k;
                }
            }
            return s.Length;
        }

        private void ReportMissing(string key) => Report("Wizard.Text: missing name key '" + key + "'");

        private void Report(string message)
        {
            if (_log == null)
            {
                return;
            }
            lock (_logLock)
            {
                if (!_logged.Add(message))
                {
                    return;
                }
            }
            _log(message);
        }
    }
}
