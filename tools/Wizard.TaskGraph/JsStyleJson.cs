using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wizard.TaskGraph
{
    // node 의 JSON.stringify(값, null, 들여쓰기) + "\n" 과 같은 바이트를 낸다.
    // tasks.json 은 이 꼴로 쓰여 있어서(들여쓰기 1칸), add 가 다시 쓸 때 남의 줄이 바뀌지 않는다.
    // 객체의 열쇠 순서는 넣은 순서 그대로다. 비ASCII 문자는 그대로 쓴다(JSON.stringify 처럼).
    public static class JsStyleJson
    {
        public static string Serialize(JsonNode? node, int indent)
        {
            var sb = new StringBuilder();
            Write(sb, node, indent, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, JsonNode? node, int indent, int depth)
        {
            switch (node)
            {
                case null:
                    sb.Append("null");
                    return;
                case JsonObject obj:
                    if (obj.Count == 0)
                    {
                        sb.Append("{}");
                        return;
                    }
                    sb.Append("{\n");
                    int i = 0;
                    foreach (var kv in obj)
                    {
                        sb.Append(' ', indent * (depth + 1));
                        WriteString(sb, kv.Key);
                        sb.Append(": ");
                        Write(sb, kv.Value, indent, depth + 1);
                        if (++i < obj.Count)
                        {
                            sb.Append(',');
                        }
                        sb.Append('\n');
                    }
                    sb.Append(' ', indent * depth).Append('}');
                    return;
                case JsonArray arr:
                    if (arr.Count == 0)
                    {
                        sb.Append("[]");
                        return;
                    }
                    sb.Append("[\n");
                    for (int k = 0; k < arr.Count; k++)
                    {
                        sb.Append(' ', indent * (depth + 1));
                        Write(sb, arr[k], indent, depth + 1);
                        if (k + 1 < arr.Count)
                        {
                            sb.Append(',');
                        }
                        sb.Append('\n');
                    }
                    sb.Append(' ', indent * depth).Append(']');
                    return;
                case JsonValue v:
                    WriteValue(sb, v);
                    return;
                default:
                    throw new InvalidOperationException("알 수 없는 JSON 노드");
            }
        }

        private static void WriteValue(StringBuilder sb, JsonValue v)
        {
            switch (v.GetValueKind())
            {
                case JsonValueKind.String:
                    WriteString(sb, v.GetValue<string>());
                    return;
                case JsonValueKind.True:
                    sb.Append("true");
                    return;
                case JsonValueKind.False:
                    sb.Append("false");
                    return;
                case JsonValueKind.Null:
                    sb.Append("null");
                    return;
                case JsonValueKind.Number:
                    // 읽은 수는 원래 글자 그대로, 코드가 넣은 정수는 십진수로 나온다. tasks.json 에는 정수만 있다.
                    sb.Append(v.ToJsonString());
                    return;
                default:
                    throw new InvalidOperationException("알 수 없는 JSON 값");
            }
        }

        // 정수 값(읽은 것이든 코드가 넣은 것이든). 정수가 아니면 null.
        public static long? AsLong(JsonNode? node)
        {
            if (node is not JsonValue v || v.GetValueKind() != JsonValueKind.Number)
            {
                return null;
            }
            return long.TryParse(v.ToJsonString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l) ? l : null;
        }

        public static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            AppendU(sb, c);
                        }
                        else if (char.IsHighSurrogate(c))
                        {
                            if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                            {
                                sb.Append(c).Append(s[i + 1]);
                                i++;
                            }
                            else
                            {
                                AppendU(sb, c);
                            }
                        }
                        else if (char.IsLowSurrogate(c))
                        {
                            AppendU(sb, c);
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        private static void AppendU(StringBuilder sb, char c)
        {
            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
        }
    }
}
