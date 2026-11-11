using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Wizard.TestKit.Shared.Golden
{
    // 골든 항목의 "setup": 재실행기가 세계를 만들 때 쓰는 설정(모드, 인원, 캐릭터 id …).
    // 값은 문자열, 정수, 참거짓만 된다. 실수와 중첩 객체·배열은 거절한다(결정론 입력에 실수를 들이지 않는다).
    // 열쇠는 서수 오름차순으로 둔다. 무엇을 넣을지는 골든을 만드는 작업(M1-TE-01 이후)이 정한다.
    public sealed class GoldenSetup
    {
        private readonly string[] _keys;
        private readonly string[] _texts;
        private readonly JTokenType[] _types;

        private GoldenSetup(string[] keys, string[] texts, JTokenType[] types)
        {
            _keys = keys;
            _texts = texts;
            _types = types;
        }

        public static readonly GoldenSetup Empty = new GoldenSetup(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<JTokenType>());

        public int Count => _keys.Length;

        // 열쇠 오름차순(서수). 값은 글자 표현(정수는 십진, 참거짓은 true/false).
        public IReadOnlyList<string> Keys => _keys;

        public bool Contains(string key) => IndexOf(key) >= 0;

        public string GetText(string key) => _texts[Require(key)];

        public string GetString(string key)
        {
            int i = Require(key);
            if (_types[i] != JTokenType.String)
            {
                throw new FormatException("setup '" + key + "' is not a string");
            }
            return _texts[i];
        }

        public long GetInt64(string key)
        {
            int i = Require(key);
            if (_types[i] != JTokenType.Integer)
            {
                throw new FormatException("setup '" + key + "' is not an integer");
            }
            return long.Parse(_texts[i], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        }

        public bool GetBool(string key)
        {
            int i = Require(key);
            if (_types[i] != JTokenType.Boolean)
            {
                throw new FormatException("setup '" + key + "' is not a boolean");
            }
            return _texts[i] == "true";
        }

        private int Require(string key)
        {
            int i = IndexOf(key);
            if (i < 0)
            {
                throw new KeyNotFoundException("setup has no key '" + key + "'");
            }
            return i;
        }

        private int IndexOf(string key)
        {
            for (int i = 0; i < _keys.Length; i++)
            {
                if (string.Equals(_keys[i], key, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        internal static GoldenSetup Read(StrictJson j, JObject? obj)
        {
            if (obj == null)
            {
                return Empty;
            }
            var keys = new List<string>();
            foreach (JProperty p in obj.Properties())
            {
                keys.Add(p.Name);
            }
            keys.Sort(StringComparer.Ordinal);
            var texts = new string[keys.Count];
            var types = new JTokenType[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                JToken v = obj[keys[i]]!;
                types[i] = v.Type;
                switch (v.Type)
                {
                    case JTokenType.String:
                        texts[i] = (string)v!;
                        break;
                    case JTokenType.Integer:
                        long? n = j.IntegerValue(v, "setup '" + keys[i] + "'", long.MinValue, long.MaxValue);
                        texts[i] = n.HasValue ? n.Value.ToString(CultureInfo.InvariantCulture) : "";
                        break;
                    case JTokenType.Boolean:
                        texts[i] = (bool)v ? "true" : "false";
                        break;
                    case JTokenType.Float:
                        j.Problem("setup '" + keys[i] + "' is a real number; use an integer in fixed units (Raw, Permille, Centi …)");
                        texts[i] = "";
                        break;
                    default:
                        j.Problem("setup '" + keys[i] + "' must be a string, an integer or a boolean (got " + v.Type + ")");
                        texts[i] = "";
                        break;
                }
            }
            return new GoldenSetup(keys.ToArray(), texts, types);
        }
    }
}
