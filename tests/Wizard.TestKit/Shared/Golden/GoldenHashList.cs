using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Wizard.TestKit.Shared.Golden
{
    // 해시 목록 [{ "tick": u32, "hash": "16진 16자리", "label": "…"? }] 의 읽기와 쓰기.
    // 틱은 엄격히 늘어나야 한다(같은 틱 두 번, 역행 금지). 비어 있으면 안 된다.
    internal static class GoldenHashList
    {
        public static List<GoldenHash> Read(StrictJson j, JArray? array, string field, List<string> problems)
        {
            var list = new List<GoldenHash>();
            if (array == null)
            {
                return list;
            }
            if (array.Count == 0)
            {
                j.Problem("'" + field + "' is empty");
                return list;
            }
            long previous = -1;
            for (int i = 0; i < array.Count; i++)
            {
                var item = new StrictJson(j.Where + " " + field + "[" + i + "]", problems);
                if (array[i].Type != JTokenType.Object)
                {
                    item.Problem("must be an object");
                    continue;
                }
                int before = problems.Count;
                var o = (JObject)array[i];
                item.OnlyFields(o, "tick", "hash", "label");
                long? tick = item.Integer(o, "tick", true, 0, uint.MaxValue);
                string? hashText = item.String(o, "hash", true);
                string? label = item.String(o, "label", false);
                ulong hash = 0;
                if (hashText != null && !HashText.TryParseHash(hashText, out hash))
                {
                    item.Problem("hash '" + hashText + "' must be 16 lowercase hex digits");
                }
                if (tick.HasValue)
                {
                    if (tick.Value <= previous)
                    {
                        item.Problem("tick must increase strictly (previous " + previous + ", got " + tick.Value + ")");
                    }
                    previous = tick.Value;
                }
                if (problems.Count == before && tick.HasValue)
                {
                    list.Add(new GoldenHash((uint)tick.Value, hash, label));
                }
            }
            return list;
        }

        public static void Write(JsonWriter w, IReadOnlyList<GoldenHash> hashes)
        {
            w.WriteStartArray();
            foreach (GoldenHash h in hashes)
            {
                w.WriteStartObject();
                w.WritePropertyName("tick");
                w.WriteValue((long)h.Tick);
                w.WritePropertyName("hash");
                w.WriteValue(HashText.Format(h.Hash));
                if (h.Label != null)
                {
                    w.WritePropertyName("label");
                    w.WriteValue(h.Label);
                }
                w.WriteEndObject();
            }
            w.WriteEndArray();
        }
    }
}
