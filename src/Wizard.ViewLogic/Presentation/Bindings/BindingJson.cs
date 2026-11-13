using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wizard.ViewLogic.Presentation
{
    // data/presentation 의 바인딩 파일만 읽는 작은 JSON 읽개. 공유 코드는 System.Text.Json 을 쓰지 않고(ARCHITECTURE 3.3),
    // 이 표는 팩에 들지 않는 표시 데이터라 IJsonCodec(통신용 DTO 직렬화)과도 따로 둔다.
    // 엄격하다: 주석, 끝 쉼표, 한 객체 안의 같은 열쇠, 남는 글자는 오류다(DataBuild 의 DataLoader 와 같은 규칙).
    // 수는 문자열 그대로 들고 있다가 쓰는 쪽이 decimal 로 읽는다(실수 반올림에 기대지 않는다).
    internal sealed class BindingJson
    {
        internal enum Kind
        {
            Null,
            Bool,
            Number,
            String,
            Array,
            Object,
        }

        internal sealed class Node
        {
            public Kind Kind;
            public bool Bool;
            public string Text = "";                                   // 문자열 값, 또는 수의 글자 그대로
            public List<Node>? Items;
            public List<KeyValuePair<string, Node>>? Members;          // 파일에 적힌 순서

            public Node? Get(string name)
            {
                if (Members == null)
                {
                    return null;
                }
                foreach (KeyValuePair<string, Node> m in Members)
                {
                    if (string.Equals(m.Key, name, StringComparison.Ordinal))
                    {
                        return m.Value;
                    }
                }
                return null;
            }
        }

        private const int MaxDepth = 64;

        private readonly string _s;
        private int _i;

        private BindingJson(string s)
        {
            _s = s;
        }

        // 실패하면 null 과 오류 문장(위치 포함)
        public static Node? Parse(string text, out string? error)
        {
            var p = new BindingJson(text);
            try
            {
                p.SkipWs();
                Node n = p.Value(0);
                p.SkipWs();
                if (p._i != p._s.Length)
                {
                    throw p.Fail("trailing characters after the value");
                }
                error = null;
                return n;
            }
            catch (FormatException e)
            {
                error = e.Message;
                return null;
            }
        }

        private Node Value(int depth)
        {
            if (depth > MaxDepth)
            {
                throw Fail("nesting too deep");
            }
            if (_i >= _s.Length)
            {
                throw Fail("missing value");
            }
            char c = _s[_i];
            switch (c)
            {
                case '{':
                    return ObjectValue(depth);
                case '[':
                    return ArrayValue(depth);
                case '"':
                    return new Node { Kind = Kind.String, Text = StringValue() };
                case 't':
                    Literal("true");
                    return new Node { Kind = Kind.Bool, Bool = true };
                case 'f':
                    Literal("false");
                    return new Node { Kind = Kind.Bool, Bool = false };
                case 'n':
                    Literal("null");
                    return new Node { Kind = Kind.Null };
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return new Node { Kind = Kind.Number, Text = NumberText() };
                    }
                    throw Fail("unexpected character '" + c + "'");
            }
        }

        private Node ObjectValue(int depth)
        {
            var node = new Node { Kind = Kind.Object, Members = new List<KeyValuePair<string, Node>>() };
            _i++;
            SkipWs();
            if (Peek('}'))
            {
                _i++;
                return node;
            }
            while (true)
            {
                SkipWs();
                if (!Peek('"'))
                {
                    throw Fail("object key must be a string");
                }
                string name = StringValue();
                if (node.Get(name) != null)
                {
                    throw Fail("duplicate key '" + name + "' in one object");
                }
                SkipWs();
                Expect(':');
                SkipWs();
                node.Members.Add(new KeyValuePair<string, Node>(name, Value(depth + 1)));
                SkipWs();
                if (Peek(','))
                {
                    _i++;
                    continue;
                }
                Expect('}');
                return node;
            }
        }

        private Node ArrayValue(int depth)
        {
            var node = new Node { Kind = Kind.Array, Items = new List<Node>() };
            _i++;
            SkipWs();
            if (Peek(']'))
            {
                _i++;
                return node;
            }
            while (true)
            {
                SkipWs();
                node.Items.Add(Value(depth + 1));
                SkipWs();
                if (Peek(','))
                {
                    _i++;
                    continue;
                }
                Expect(']');
                return node;
            }
        }

        private string StringValue()
        {
            Expect('"');
            var sb = new StringBuilder();
            while (true)
            {
                if (_i >= _s.Length)
                {
                    throw Fail("unterminated string");
                }
                char c = _s[_i++];
                if (c == '"')
                {
                    return sb.ToString();
                }
                if (c < ' ')
                {
                    throw Fail("control character in string");
                }
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                if (_i >= _s.Length)
                {
                    throw Fail("unterminated escape");
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
                    case 'u':
                        if (_i + 4 > _s.Length || !int.TryParse(_s.Substring(_i, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                        {
                            throw Fail("\\u needs four hex digits");
                        }
                        sb.Append((char)code);
                        _i += 4;
                        break;
                    default:
                        throw Fail("unknown escape \\" + e);
                }
            }
        }

        // JSON 수 문법(RFC 8259) 그대로 글자를 잘라 낸다
        private string NumberText()
        {
            int start = _i;
            if (Peek('-'))
            {
                _i++;
            }
            if (Peek('0'))
            {
                _i++;
            }
            else if (!Digits())
            {
                throw Fail("number has no digits");
            }
            if (Peek('.'))
            {
                _i++;
                if (!Digits())
                {
                    throw Fail("no digits after decimal point");
                }
            }
            if (Peek('e') || Peek('E'))
            {
                _i++;
                if (Peek('+') || Peek('-'))
                {
                    _i++;
                }
                if (!Digits())
                {
                    throw Fail("no digits in exponent");
                }
            }
            return _s.Substring(start, _i - start);
        }

        private bool Digits()
        {
            int start = _i;
            while (_i < _s.Length && _s[_i] >= '0' && _s[_i] <= '9')
            {
                _i++;
            }
            return _i > start;
        }

        private void Literal(string word)
        {
            if (string.CompareOrdinal(_s, _i, word, 0, word.Length) != 0)
            {
                throw Fail("expected '" + word + "'");
            }
            _i += word.Length;
        }

        private void SkipWs()
        {
            while (_i < _s.Length)
            {
                char c = _s[_i];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r' && c != '﻿')
                {
                    return;
                }
                _i++;
            }
        }

        private bool Peek(char c) => _i < _s.Length && _s[_i] == c;

        private void Expect(char c)
        {
            if (!Peek(c))
            {
                throw Fail("expected '" + c + "'");
            }
            _i++;
        }

        private FormatException Fail(string what)
        {
            int line = 1;
            int col = 1;
            for (int k = 0; k < _i && k < _s.Length; k++)
            {
                if (_s[k] == '\n')
                {
                    line++;
                    col = 1;
                }
                else
                {
                    col++;
                }
            }
            return new FormatException("JSON " + line.ToString(CultureInfo.InvariantCulture) + ":" + col.ToString(CultureInfo.InvariantCulture) + " " + what);
        }
    }
}
