using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json.Linq;

namespace Wizard.TestKit.Shared
{
    // 엄격한 JSON 필드 읽기. 문제를 모아 두고 한 번에 보고한다(첫 문제에서 멈추면 고치고 다시 돌리는 일이 늘어난다).
    // 모르는 필드는 문제다(오타로 생긴 필드가 조용히 무시되지 않게).
    internal sealed class StrictJson
    {
        private readonly string _where;
        private readonly List<string> _problems;

        public StrictJson(string where, List<string> problems)
        {
            _where = where;
            _problems = problems;
        }

        public string Where => _where;

        public void Problem(string message)
        {
            _problems.Add(_where + ": " + message);
        }

        public void OnlyFields(JObject obj, params string[] allowed)
        {
            foreach (JProperty p in obj.Properties())
            {
                if (System.Array.IndexOf(allowed, p.Name) < 0)
                {
                    Problem("unknown field '" + p.Name + "'");
                }
            }
        }

        public string? String(JObject obj, string name, bool required)
        {
            JToken? t = obj[name];
            if (t == null || t.Type == JTokenType.Null)
            {
                if (required)
                {
                    Problem("missing string field '" + name + "'");
                }
                return null;
            }
            if (t.Type != JTokenType.String)
            {
                Problem("field '" + name + "' must be a string");
                return null;
            }
            return (string?)t;
        }

        public long? Integer(JObject obj, string name, bool required, long min, long max)
        {
            JToken? t = obj[name];
            if (t == null || t.Type == JTokenType.Null)
            {
                if (required)
                {
                    Problem("missing integer field '" + name + "'");
                }
                return null;
            }
            return IntegerValue(t, "field '" + name + "'", min, max);
        }

        public long? IntegerValue(JToken t, string what, long min, long max)
        {
            if (t.Type == JTokenType.Float)
            {
                Problem(what + " must be an integer, not a real number");
                return null;
            }
            if (t.Type != JTokenType.Integer)
            {
                Problem(what + " must be an integer");
                return null;
            }
            object? raw = ((JValue)t).Value;
            if (!(raw is long v))
            {
                Problem(what + " is out of the 64-bit range");
                return null;
            }
            if (v < min || v > max)
            {
                Problem(what + " = " + v.ToString(CultureInfo.InvariantCulture) + " is outside [" +
                        min.ToString(CultureInfo.InvariantCulture) + ", " + max.ToString(CultureInfo.InvariantCulture) + "]");
                return null;
            }
            return v;
        }

        public JArray? Array(JObject obj, string name, bool required)
        {
            JToken? t = obj[name];
            if (t == null || t.Type == JTokenType.Null)
            {
                if (required)
                {
                    Problem("missing array field '" + name + "'");
                }
                return null;
            }
            if (t.Type != JTokenType.Array)
            {
                Problem("field '" + name + "' must be an array");
                return null;
            }
            return (JArray)t;
        }

        public JObject? Object(JObject obj, string name, bool required)
        {
            JToken? t = obj[name];
            if (t == null || t.Type == JTokenType.Null)
            {
                if (required)
                {
                    Problem("missing object field '" + name + "'");
                }
                return null;
            }
            if (t.Type != JTokenType.Object)
            {
                Problem("field '" + name + "' must be an object");
                return null;
            }
            return (JObject)t;
        }

        public void Format(JObject obj, int expected)
        {
            long? f = Integer(obj, "format", true, 0, int.MaxValue);
            if (f.HasValue && f.Value != expected)
            {
                Problem("format " + f.Value.ToString(CultureInfo.InvariantCulture) + " is not supported (expected " +
                        expected.ToString(CultureInfo.InvariantCulture) + ")");
            }
        }
    }
}
