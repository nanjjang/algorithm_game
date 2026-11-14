using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Wizard.Solo.View.Effects
{
    /// <summary>
    /// G16의 표시 수치(Resources/DataSolo/G16.json). 수치는 코드에 두지 않고 여기서 읽는다(00 문서 9절 수치 조각 규칙).
    /// 경로 접근: F("zones", "ice_opacity") 처럼 열쇠를 차례로 준다. 없는 값은 0 / null / 빈 목록(잘못 적힌 JSON이 화면을 죽이지 않게).
    /// </summary>
    public sealed class EffectsTuning
    {
        public readonly JObject Root;

        public EffectsTuning(JObject root) { Root = root ?? new JObject(); }

        public static EffectsTuning Parse(string json) => new EffectsTuning(string.IsNullOrEmpty(json) ? null : JObject.Parse(json));

        public JToken Token(params string[] path)
        {
            JToken t = Root;
            for (int i = 0; i < path.Length; i++)
            {
                var o = t as JObject;
                if (o == null) return null;
                t = o[path[i]];
                if (t == null) return null;
            }
            return t;
        }

        public float F(params string[] path)
        {
            var t = Token(path);
            if (t == null) return 0f;
            if (t.Type == JTokenType.Float || t.Type == JTokenType.Integer) return t.Value<float>();
            return 0f;
        }

        public int I(params string[] path)
        {
            var t = Token(path);
            if (t == null) return 0;
            if (t.Type == JTokenType.Integer) return t.Value<int>();
            if (t.Type == JTokenType.Float) return Mathf.RoundToInt(t.Value<float>());
            return 0;
        }

        public bool B(params string[] path)
        {
            var t = Token(path);
            return t != null && t.Type == JTokenType.Boolean && t.Value<bool>();
        }

        public string S(params string[] path)
        {
            var t = Token(path);
            return t != null && t.Type == JTokenType.String ? t.Value<string>() : null;
        }

        public float[] Floats(params string[] path)
        {
            var a = Token(path) as JArray;
            if (a == null) return new float[0];
            var r = new float[a.Count];
            for (int i = 0; i < a.Count; i++) r[i] = a[i].Type == JTokenType.Float || a[i].Type == JTokenType.Integer ? a[i].Value<float>() : 0f;
            return r;
        }

        public int[] Ints(params string[] path)
        {
            var f = Floats(path);
            var r = new int[f.Length];
            for (int i = 0; i < f.Length; i++) r[i] = Mathf.RoundToInt(f[i]);
            return r;
        }

        public Color Col(params string[] path) => ParseColor(S(path), UnityEngine.Color.white);

        /// <summary>속성 주색(03 1.1절). 모르는 속성이면 무 속성 색.</summary>
        public Color ElementPrimary(string elementId) => ElementColor("element_primary", elementId);
        public Color ElementSecondary(string elementId) => ElementColor("element_secondary", elementId);

        Color ElementColor(string table, string elementId)
        {
            string s = elementId != null ? S("colors", table, elementId) : null;
            if (s == null) s = S("colors", table, Ids.Element.Neutral);
            return ParseColor(s, UnityEngine.Color.white);
        }

        /// <summary>"#RRGGBB" / "#RRGGBBAA" → 색(sRGB 그대로). 틀리면 fallback.</summary>
        public static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            // 순수하게 푼다(ColorUtility는 엔진 내부 호출이라 엔진 밖 단위 시험에서 못 쓴다).
            string h = hex[0] == '#' ? hex.Substring(1) : hex;
            if (h.Length != 6 && h.Length != 8) return fallback;
            uint v;
            if (!uint.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out v)) return fallback;
            if (h.Length == 6) v = (v << 8) | 0xFFu;
            return new Color32((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        /// <summary>문자열 → 문자열 표(예: sound.art_fire).</summary>
        public Dictionary<string, string> StringMap(params string[] path)
        {
            var d = new Dictionary<string, string>();
            var o = Token(path) as JObject;
            if (o == null) return d;
            foreach (var p in o.Properties())
                if (p.Value.Type == JTokenType.String) d[p.Name] = p.Value.Value<string>();
            return d;
        }
    }
}
