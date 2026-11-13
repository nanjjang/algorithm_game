using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wizard.ViewLogic.Assets.Json
{
    // RFC 8259 JSON 을 ManifestJson 나무로 읽는다. 주석·끝 쉼표는 받지 않고, 한 객체 안의 같은 열쇠는 오류다
    // (DataBuild 의 data/ 읽기 규칙과 같다). 문법 오류는 ManifestJsonException 하나로 알린다.
    public static class ManifestJsonReader
    {
        public const int MaxDepth = 64;

        public static ManifestJson Parse(string text)
        {
            var p = new Parser(text);
            p.SkipWhitespace();
            ManifestJson root = p.ReadValue("", 0);
            p.SkipWhitespace();
            if (!p.AtEnd)
            {
                throw new ManifestJsonException("unexpected text after the root value", p.Position);
            }
            return root;
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;

            public Parser(string s)
            {
                _s = s;
                // UTF-8 BOM 을 글자로 받았으면 건너뛴다
                if (_s.Length > 0 && _s[0] == '﻿')
                {
                    _i = 1;
                }
            }

            public int Position => _i;

            public bool AtEnd => _i >= _s.Length;

            public void SkipWhitespace()
            {
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r')
                    {
                        _i++;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            public ManifestJson ReadValue(string pointer, int depth)
            {
                if (depth > MaxDepth)
                {
                    throw new ManifestJsonException("nesting is deeper than " + MaxDepth.ToString(CultureInfo.InvariantCulture), _i);
                }
                if (AtEnd)
                {
                    throw new ManifestJsonException("unexpected end of input", _i);
                }
                char c = _s[_i];
                switch (c)
                {
                    case '{': return ReadObject(pointer, depth);
                    case '[': return ReadArray(pointer, depth);
                    case '"': return ManifestJson.Str(pointer, ReadString());
                    case 't': ExpectWord("true"); return ManifestJson.Bool(pointer, true);
                    case 'f': ExpectWord("false"); return ManifestJson.Bool(pointer, false);
                    case 'n': ExpectWord("null"); return ManifestJson.Null(pointer);
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return ManifestJson.Number(pointer, ReadNumber());
                        }
                        throw new ManifestJsonException("unexpected character '" + c + "'", _i);
                }
            }

            private ManifestJson ReadObject(string pointer, int depth)
            {
                int start = _i;
                _i++; // {
                var props = new List<KeyValuePair<string, ManifestJson>>();
                var index = new Dictionary<string, int>(System.StringComparer.Ordinal);
                SkipWhitespace();
                if (Peek() == '}')
                {
                    _i++;
                    return ManifestJson.Object(pointer, props, index);
                }
                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"')
                    {
                        throw new ManifestJsonException("expected a property name", _i);
                    }
                    int keyAt = _i;
                    string key = ReadString();
                    if (index.ContainsKey(key))
                    {
                        throw new ManifestJsonException("duplicate key '" + key + "' in one object", keyAt);
                    }
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    ManifestJson value = ReadValue(pointer + "/" + Escape(key), depth + 1);
                    index.Add(key, props.Count);
                    props.Add(new KeyValuePair<string, ManifestJson>(key, value));
                    SkipWhitespace();
                    char c = Peek();
                    if (c == ',')
                    {
                        _i++;
                        continue;
                    }
                    if (c == '}')
                    {
                        _i++;
                        return ManifestJson.Object(pointer, props, index);
                    }
                    throw new ManifestJsonException("expected ',' or '}' in the object that starts at " + start.ToString(CultureInfo.InvariantCulture), _i);
                }
            }

            private ManifestJson ReadArray(string pointer, int depth)
            {
                int start = _i;
                _i++; // [
                var items = new List<ManifestJson>();
                SkipWhitespace();
                if (Peek() == ']')
                {
                    _i++;
                    return ManifestJson.Array(pointer, items);
                }
                while (true)
                {
                    SkipWhitespace();
                    items.Add(ReadValue(pointer + "/" + items.Count.ToString(CultureInfo.InvariantCulture), depth + 1));
                    SkipWhitespace();
                    char c = Peek();
                    if (c == ',')
                    {
                        _i++;
                        continue;
                    }
                    if (c == ']')
                    {
                        _i++;
                        return ManifestJson.Array(pointer, items);
                    }
                    throw new ManifestJsonException("expected ',' or ']' in the array that starts at " + start.ToString(CultureInfo.InvariantCulture), _i);
                }
            }

            private string ReadString()
            {
                _i++; // "
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw new ManifestJsonException("unterminated string", _i);
                    }
                    char c = _s[_i++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }
                    if (c < ' ')
                    {
                        throw new ManifestJsonException("control character in a string", _i - 1);
                    }
                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }
                    if (AtEnd)
                    {
                        throw new ManifestJsonException("unterminated escape", _i);
                    }
                    char e = _s[_i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u': sb.Append(ReadHex4()); break;
                        default: throw new ManifestJsonException("unknown escape '\\" + e + "'", _i - 1);
                    }
                }
            }

            private char ReadHex4()
            {
                const int HexDigits = 4;
                const int Radix = 16;
                const int DigitBase = 10;
                if (_i + HexDigits > _s.Length)
                {
                    throw new ManifestJsonException("short \\u escape", _i);
                }
                int v = 0;
                for (int k = 0; k < HexDigits; k++)
                {
                    char h = _s[_i++];
                    int d;
                    if (h >= '0' && h <= '9')
                    {
                        d = h - '0';
                    }
                    else if (h >= 'a' && h <= 'f')
                    {
                        d = h - 'a' + DigitBase;
                    }
                    else if (h >= 'A' && h <= 'F')
                    {
                        d = h - 'A' + DigitBase;
                    }
                    else
                    {
                        throw new ManifestJsonException("bad hex digit in \\u escape", _i - 1);
                    }
                    v = v * Radix + d;
                }
                return (char)v;
            }

            // 문법만 확인하고 원문을 돌려준다: -?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?
            private string ReadNumber()
            {
                int start = _i;
                if (Peek() == '-')
                {
                    _i++;
                }
                if (Peek() == '0')
                {
                    _i++;
                }
                else if (IsDigit(Peek()))
                {
                    while (IsDigit(Peek()))
                    {
                        _i++;
                    }
                }
                else
                {
                    throw new ManifestJsonException("bad number", _i);
                }
                if (Peek() == '.')
                {
                    _i++;
                    if (!IsDigit(Peek()))
                    {
                        throw new ManifestJsonException("bad fraction", _i);
                    }
                    while (IsDigit(Peek()))
                    {
                        _i++;
                    }
                }
                if (Peek() == 'e' || Peek() == 'E')
                {
                    _i++;
                    if (Peek() == '+' || Peek() == '-')
                    {
                        _i++;
                    }
                    if (!IsDigit(Peek()))
                    {
                        throw new ManifestJsonException("bad exponent", _i);
                    }
                    while (IsDigit(Peek()))
                    {
                        _i++;
                    }
                }
                return _s.Substring(start, _i - start);
            }

            private static bool IsDigit(char c) => c >= '0' && c <= '9';

            private char Peek() => _i < _s.Length ? _s[_i] : '\0';

            private void Expect(char c)
            {
                if (Peek() != c)
                {
                    throw new ManifestJsonException("expected '" + c + "'", _i);
                }
                _i++;
            }

            private void ExpectWord(string word)
            {
                if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
                {
                    throw new ManifestJsonException("expected '" + word + "'", _i);
                }
                _i += word.Length;
            }

            private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");
        }
    }
}
