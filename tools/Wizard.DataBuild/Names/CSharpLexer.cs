using System;
using System.Collections.Generic;

namespace Wizard.DataBuild.Names
{
    // names-scan 이 쓰는 작은 C# 어휘 분석기. 소스를 코드·주석·문자열 리터럴·문자 리터럴 조각으로 나눈다.
    // 다루는 꼴: // 와 /* */ 주석, #region·#endregion·#pragma·#error·#warning 줄(주석으로 본다), "…"(이스케이프),
    // @"…"(""), $"…"·$@"…"·@$"…"(구멍 {…} 안의 중첩 문자열·문자·주석과 중괄호 깊이), """…"""(원시 문자열, $ 접두 포함), '…'.
    // 컴파일러가 아니므로 문법 오류가 있는 소스에서는 근사로 끝까지 나아간다(멈추거나 던지지 않는다).
    public static class CSharpLexer
    {
        public enum Kind
        {
            Code,
            Comment,
            String,
            Char,
        }

        public readonly struct Segment
        {
            public Segment(Kind kind, int start, int length, bool decodesEscapes)
            {
                SegmentKind = kind;
                Start = start;
                Length = length;
                DecodesEscapes = decodesEscapes;
            }

            public Kind SegmentKind { get; }

            public int Start { get; }

            public int Length { get; }

            // 보통 문자열·문자 리터럴(\uXXXX 같은 이스케이프가 글자를 만든다)
            public bool DecodesEscapes { get; }
        }

        public static List<Segment> Lex(string s)
        {
            var result = new List<Segment>();
            int codeStart = 0;
            int i = 0;
            while (i < s.Length)
            {
                int end;
                Kind kind;
                bool decodes = false;
                if (StartsWith(s, i, "//"))
                {
                    end = LineEnd(s, i);
                    kind = Kind.Comment;
                }
                else if (StartsWith(s, i, "/*"))
                {
                    end = BlockCommentEnd(s, i);
                    kind = Kind.Comment;
                }
                else if (s[i] == '#' && AtLineStart(s, i) && IsCommentDirective(s, i + 1))
                {
                    end = LineEnd(s, i);
                    kind = Kind.Comment;
                }
                else if (TryStringStart(s, i, out end, out decodes))
                {
                    kind = Kind.String;
                }
                else if (s[i] == '\'' && TryCharLiteral(s, i, out end))
                {
                    kind = Kind.Char;
                    decodes = true;
                }
                else if (IsIdentChar(s[i]))
                {
                    // 식별자 안의 $·@·따옴표가 아닌 글자는 한 번에 건넌다(접두 판정이 식별자 중간에서 일어나지 않게)
                    int j = i;
                    while (j < s.Length && IsIdentChar(s[j]))
                    {
                        j++;
                    }
                    i = j;
                    continue;
                }
                else
                {
                    i++;
                    continue;
                }
                if (i > codeStart)
                {
                    result.Add(new Segment(Kind.Code, codeStart, i - codeStart, false));
                }
                result.Add(new Segment(kind, i, end - i, decodes));
                i = end;
                codeStart = end;
            }
            if (s.Length > codeStart)
            {
                result.Add(new Segment(Kind.Code, codeStart, s.Length - codeStart, false));
            }
            return result;
        }

        // 리터럴 안의 \uXXXX, \UXXXXXXXX, \xH…(1~4자리)를 푼 글자들(그 밖의 글자는 그대로)
        public static string DecodeEscapes(string literal)
        {
            var sb = new System.Text.StringBuilder(literal.Length);
            for (int i = 0; i < literal.Length; i++)
            {
                char c = literal[i];
                if (c != '\\' || i + 1 >= literal.Length)
                {
                    sb.Append(c);
                    continue;
                }
                char n = literal[i + 1];
                int digits = n == 'u' ? 4 : n == 'U' ? 8 : n == 'x' ? -4 : 0;
                if (digits == 0)
                {
                    sb.Append(c).Append(n);
                    i++;
                    continue;
                }
                int max = digits < 0 ? -digits : digits;
                int j = i + 2;
                long v = 0;
                int count = 0;
                while (count < max && j < literal.Length && Uri.IsHexDigit(literal[j]))
                {
                    v = v * 16 + Convert.ToInt32(literal[j].ToString(), 16);
                    j++;
                    count++;
                }
                if (count == 0 || (digits > 0 && count != max) || v > 0x10FFFF)
                {
                    sb.Append(c).Append(n);
                    i++;
                    continue;
                }
                sb.Append(char.ConvertFromUtf32((int)v >= 0xD800 && v <= 0xDFFF ? 0xFFFD : (int)v));
                i = j - 1;
            }
            return sb.ToString();
        }

        private static bool TryStringStart(string s, int i, out int end, out bool decodes)
        {
            end = i;
            decodes = false;
            int j = i;
            int dollars = 0;
            bool verbatim = false;
            while (j < s.Length && (s[j] == '$' || s[j] == '@'))
            {
                if (s[j] == '$')
                {
                    dollars++;
                }
                else
                {
                    if (verbatim)
                    {
                        return false;
                    }
                    verbatim = true;
                }
                j++;
            }
            if (j >= s.Length || s[j] != '"')
            {
                return false;
            }
            int quotes = 0;
            while (j + quotes < s.Length && s[j + quotes] == '"')
            {
                quotes++;
            }
            if (quotes >= 3 && !verbatim)
            {
                end = RawEnd(s, j + quotes, quotes);
                return true;
            }
            bool interpolated = dollars > 0;
            end = verbatim ? VerbatimEnd(s, j + 1, interpolated) : RegularEnd(s, j + 1, interpolated);
            decodes = !verbatim;
            return true;
        }

        // 원시 문자열: 여는 따옴표와 같은 수의 따옴표에서 끝난다
        private static int RawEnd(string s, int from, int quotes)
        {
            int i = from;
            while (i < s.Length)
            {
                if (s[i] == '"')
                {
                    int run = 0;
                    while (i + run < s.Length && s[i + run] == '"')
                    {
                        run++;
                    }
                    if (run >= quotes)
                    {
                        return i + run;
                    }
                    i += run;
                    continue;
                }
                i++;
            }
            return s.Length;
        }

        private static int RegularEnd(string s, int from, bool interpolated)
        {
            int i = from;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '\\')
                {
                    i += 2;
                    continue;
                }
                if (c == '"')
                {
                    return i + 1;
                }
                if (c == '\n')
                {
                    return i;   // 닫히지 않은 문자열: 줄 끝에서 멈춘다
                }
                if (interpolated && c == '{')
                {
                    if (i + 1 < s.Length && s[i + 1] == '{')
                    {
                        i += 2;
                        continue;
                    }
                    i = HoleEnd(s, i + 1);
                    continue;
                }
                i++;
            }
            return s.Length;
        }

        private static int VerbatimEnd(string s, int from, bool interpolated)
        {
            int i = from;
            while (i < s.Length)
            {
                char c = s[i];
                if (c == '"')
                {
                    if (i + 1 < s.Length && s[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }
                    return i + 1;
                }
                if (interpolated && c == '{')
                {
                    if (i + 1 < s.Length && s[i + 1] == '{')
                    {
                        i += 2;
                        continue;
                    }
                    i = HoleEnd(s, i + 1);
                    continue;
                }
                i++;
            }
            return s.Length;
        }

        // 보간 구멍 { … } 의 끝(닫는 중괄호 다음). 안의 문자열·문자·주석은 건넌다
        private static int HoleEnd(string s, int from)
        {
            int depth = 1;
            int i = from;
            while (i < s.Length)
            {
                if (StartsWith(s, i, "//"))
                {
                    i = LineEnd(s, i);
                    continue;
                }
                if (StartsWith(s, i, "/*"))
                {
                    i = BlockCommentEnd(s, i);
                    continue;
                }
                if (TryStringStart(s, i, out int end, out _))
                {
                    i = end;
                    continue;
                }
                if (s[i] == '\'' && TryCharLiteral(s, i, out int cend))
                {
                    i = cend;
                    continue;
                }
                if (s[i] == '{')
                {
                    depth++;
                }
                else if (s[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i + 1;
                    }
                }
                else if (s[i] == '"')
                {
                    return i;   // 구멍이 닫히지 않은 채 문자열이 끝났다
                }
                i++;
            }
            return s.Length;
        }

        private static bool TryCharLiteral(string s, int i, out int end)
        {
            end = i;
            int j = i + 1;
            if (j >= s.Length || s[j] == '\n')
            {
                return false;
            }
            if (s[j] == '\\')
            {
                j += 2;
                while (j < s.Length && j - i <= 11 && s[j] != '\'' && s[j] != '\n')
                {
                    j++;
                }
            }
            else
            {
                j += char.IsHighSurrogate(s[j]) ? 2 : 1;
            }
            if (j < s.Length && s[j] == '\'')
            {
                end = j + 1;
                return true;
            }
            return false;
        }

        private static bool IsCommentDirective(string s, int i)
        {
            int j = i;
            while (j < s.Length && (s[j] == ' ' || s[j] == '\t'))
            {
                j++;
            }
            foreach (string d in new[] { "region", "endregion", "pragma", "error", "warning" })
            {
                if (StartsWith(s, j, d) && (j + d.Length >= s.Length || !IsIdentChar(s[j + d.Length])))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool AtLineStart(string s, int i)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                if (s[j] == '\n')
                {
                    return true;
                }
                if (s[j] != ' ' && s[j] != '\t')
                {
                    return false;
                }
            }
            return true;
        }

        private static int LineEnd(string s, int i)
        {
            int n = s.IndexOf('\n', i);
            return n < 0 ? s.Length : n;
        }

        private static int BlockCommentEnd(string s, int i)
        {
            int n = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
            return n < 0 ? s.Length : n + 2;
        }

        private static bool StartsWith(string s, int i, string p) => string.CompareOrdinal(s, i, p, 0, p.Length) == 0 && i + p.Length <= s.Length;

        private static bool IsIdentChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
