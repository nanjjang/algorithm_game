using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Wizard.Solo.Data
{
    /// <summary>
    /// 이름 표(00 문서 9절). 원본 사본 Resources/Data/names/names.ko.json + 조각 Resources/NamesSolo/*.ko.json을 합친다.
    /// 조각이 원본이나 다른 조각과 같은 열쇠를 다시 적으면 오류로 적고(Debug.LogError) 먼저 읽은 값을 쓴다.
    ///
    /// 틀 문법(08 요약서 17절, spec 14 2.4 — src/Wizard.Text TextResolver와 같은 규칙):
    ///   {param}      Format에 준 값. 없으면 이름 표의 열쇠로 보고 한 단계까지 풀어 넣는다({char.1} 등).
    ///                둘 다 없으면 점이 든 이름(이름 열쇠)은 열쇠 글자 그대로, 점이 없는 이름(값 자리)은 "{param}" 그대로 둔다.
    ///   {A/B}        바로 앞 값이 받침으로 끝나면 A, 아니면 B(이/가, 을/를, 은/는, 과/와).
    ///   {(A)B}       받침이 있고 ㄹ 받침이 아니면 A+B, 아니면 B((으)로).
    ///   {{ }}        글자 그대로의 { }.
    /// 바로 앞이 값이 아니면(글자·조사·틀의 처음) 받침형을 쓴다 [임시]. 조사 글자는 틀에 적힌 것을 그대로 쓴다.
    /// </summary>
    public sealed class NameTable : INameTable
    {
        /// <summary>원본 이름 표의 Resources 경로와 조각 폴더.</summary>
        public const string ResourcesMainPath = "Data/names/names.ko";
        public const string ResourcesFragmentsFolder = "NamesSolo";
        /// <summary>원본 표의 끝 표식 열쇠(값 없음). 이름으로 쓰지 않는다.</summary>
        public const string EndMarkerKey = "~end";
        const int MaxNestedDepth = 1;

        readonly Dictionary<string, string> _map = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly Dictionary<string, string> _origin = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly List<string> _errors = new List<string>();
        readonly Action<string> _logError;

        NameTable(Action<string> logError)
        {
            _logError = logError ?? (m => Debug.LogError(m));
        }

        /// <summary>합치는 동안 생긴 오류(중복 열쇠, 파싱 실패). 비어 있어야 정상.</summary>
        public IReadOnlyList<string> Errors => _errors;
        /// <summary>합친 열쇠 수.</summary>
        public int Count => _map.Count;
        /// <summary>열쇠가 온 곳(원본이면 "names.ko", 조각이면 조각 이름). 없으면 null.</summary>
        public string OriginOf(string key)
        {
            string o;
            return key != null && _origin.TryGetValue(key, out o) ? o : null;
        }
        /// <summary>모든 열쇠(순서 없음).</summary>
        public IEnumerable<string> Keys => _map.Keys;

        // ------------------------------------------------------------------ 읽기

        /// <summary>부팅 경로: Resources의 원본 사본과 NamesSolo 조각 전부(이름 오름차순으로 합친다).</summary>
        public static NameTable LoadFromResources(Action<string> logError = null)
        {
            var main = Resources.Load<TextAsset>(ResourcesMainPath);
            var frags = Resources.LoadAll<TextAsset>(ResourcesFragmentsFolder);
            var list = new List<KeyValuePair<string, string>>();
            if (frags != null)
                for (int i = 0; i < frags.Length; i++)
                    if (frags[i] != null) list.Add(new KeyValuePair<string, string>(frags[i].name, frags[i].text));
            var t = FromJson(main != null ? main.text : null, list, logError);
            if (main == null) t.Error("NameTable: missing '" + ResourcesMainPath + "'");
            return t;
        }

        /// <summary>테스트·도구용: 원본 파일 경로와 조각 폴더(*.json 전부)를 File IO로 읽는다.</summary>
        public static NameTable LoadFromDirectory(string mainFile, string fragmentsDir, Action<string> logError = null)
        {
            string main = mainFile != null && File.Exists(mainFile) ? File.ReadAllText(mainFile, Encoding.UTF8) : null;
            var list = new List<KeyValuePair<string, string>>();
            if (fragmentsDir != null && Directory.Exists(fragmentsDir))
            {
                var files = Directory.GetFiles(fragmentsDir, "*.json");
                for (int i = 0; i < files.Length; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(files[i]);   // "F1.ko"
                    list.Add(new KeyValuePair<string, string>(name, File.ReadAllText(files[i], Encoding.UTF8)));
                }
            }
            var t = FromJson(main, list, logError);
            if (main == null) t.Error("NameTable: missing main names file '" + mainFile + "'");
            return t;
        }

        /// <summary>원본 JSON 글과 (조각 이름, 조각 JSON 글) 목록을 합친다. 조각은 이름 오름차순(서수)으로 합친다.</summary>
        public static NameTable FromJson(string mainJson, IEnumerable<KeyValuePair<string, string>> fragments, Action<string> logError = null)
        {
            var t = new NameTable(logError);
            if (mainJson != null) t.Merge("names.ko", mainJson, true);
            if (fragments != null)
            {
                var list = new List<KeyValuePair<string, string>>(fragments);
                list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                for (int i = 0; i < list.Count; i++) t.Merge(list[i].Key, list[i].Value, false);
            }
            return t;
        }

        void Merge(string source, string json, bool isMain)
        {
            JObject jo;
            try { jo = JObject.Parse(json); }
            catch (Exception e) { Error("NameTable: cannot parse '" + source + "': " + e.Message); return; }
            foreach (var prop in jo.Properties())
            {
                string key = prop.Name;
                if (key == EndMarkerKey) continue;
                if (prop.Value.Type != JTokenType.String)
                {
                    Error("NameTable: '" + source + "' key '" + key + "' is not a string");
                    continue;
                }
                string prev;
                if (_origin.TryGetValue(key, out prev))
                {
                    Error("NameTable: duplicate key '" + key + "' in '" + source + "' (already in '" + prev + "')");
                    continue;
                }
                _map[key] = (string)prop.Value;
                _origin[key] = source;
            }
        }

        void Error(string message)
        {
            _errors.Add(message);
            _logError(message);
        }

        // ------------------------------------------------------------------ INameTable

        public bool Has(string key) => key != null && _map.ContainsKey(key);

        /// <summary>열쇠의 글자. 값 속의 이름 열쇠({char.1} 등)는 한 단계까지 풀고, 값 자리({n})는 그대로 둔다. 없으면 열쇠 그대로.</summary>
        public string Get(string key)
        {
            if (key == null) return string.Empty;
            string v;
            if (!_map.TryGetValue(key, out v)) return key;
            return NeedsParse(v) ? Resolve(v, null, 0) : v;
        }

        /// <summary>"{name}" 자리 채우기. nameValuePairs = (이름, 값) 번갈아. 짝이 안 맞는 마지막 하나는 버린다.</summary>
        public string Format(string key, params object[] nameValuePairs)
        {
            if (key == null) return string.Empty;
            string template;
            if (!_map.TryGetValue(key, out template)) return key;
            Dictionary<string, string> args = null;
            if (nameValuePairs != null && nameValuePairs.Length >= 2)
            {
                args = new Dictionary<string, string>(StringComparer.Ordinal);
                for (int i = 0; i + 1 < nameValuePairs.Length; i += 2)
                {
                    string name = nameValuePairs[i] as string ?? Convert.ToString(nameValuePairs[i], CultureInfo.InvariantCulture);
                    if (name == null) continue;
                    args[name] = ValueText(nameValuePairs[i + 1]);
                }
            }
            return Resolve(template, args, 0);
        }

        /// <summary>세 자리 쉼표 숫자(08 요약서 1.4절): 48200 → "48,200".</summary>
        public string Number(long value) => value.ToString("#,0", CultureInfo.InvariantCulture);

        static string ValueText(object v)
        {
            if (v == null) return string.Empty;
            var s = v as string;
            if (s != null) return s;
            var f = v as IFormattable;
            return f != null ? f.ToString(null, CultureInfo.InvariantCulture) : v.ToString();
        }

        // ------------------------------------------------------------------ 틀 풀기

        string Resolve(string template, Dictionary<string, string> args, int depth)
        {
            var sb = new StringBuilder(template.Length + 16);
            string last = null;   // 바로 앞 값이 낸 글자. 글자·조사가 오면 지운다.
            int n = template.Length;
            int i = 0;
            while (i < n)
            {
                char c = template[i];
                if (c == '{')
                {
                    if (i + 1 < n && template[i + 1] == '{') { sb.Append('{'); last = null; i += 2; continue; }
                    int close = template.IndexOf('}', i + 1);
                    if (close < 0) { sb.Append(template, i, n - i); last = null; break; }
                    string inner = template.Substring(i + 1, close - i - 1);
                    i = close + 1;
                    if (TryAppendJosa(sb, inner, last)) { last = null; continue; }
                    bool resolved;
                    string v = ResolveRef(inner, args, depth, out resolved);
                    sb.Append(v);
                    last = resolved && v.Length > 0 ? v : null;
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

        string ResolveRef(string name, Dictionary<string, string> args, int depth, out bool resolved)
        {
            resolved = true;
            string given;
            if (args != null && args.TryGetValue(name, out given))
            {
                if (given.IndexOf('{') < 0 || depth >= MaxNestedDepth) return given;
                return Resolve(given, args, depth + 1);
            }
            string value;
            if (_map.TryGetValue(name, out value))
            {
                if (!NeedsParse(value)) return value;
                if (depth >= MaxNestedDepth) return name;
                return Resolve(value, args, depth + 1);
            }
            resolved = false;
            // 점이 든 이름은 이름 열쇠(없으면 열쇠 그대로 찍어 빠진 것이 보이게), 점이 없으면 아직 채우지 않은 값 자리.
            return name.IndexOf('.') >= 0 ? name : "{" + name + "}";
        }

        static bool TryAppendJosa(StringBuilder sb, string inner, string last)
        {
            if (inner.Length > 0 && inner[0] == '(')
            {
                int closeParen = inner.IndexOf(')');
                if (closeParen > 1 && closeParen < inner.Length - 1)
                {
                    bool insert = true;
                    if (last != null)
                    {
                        bool has, rieul;
                        FinalSound(last, out has, out rieul);
                        insert = has && !rieul;
                    }
                    if (insert) sb.Append(inner, 1, closeParen - 1);
                    sb.Append(inner, closeParen + 1, inner.Length - closeParen - 1);
                    return true;
                }
                return false;
            }
            int slash = inner.IndexOf('/');
            if (slash < 0) return false;
            bool withFinal = true;
            if (last != null)
            {
                bool has, rieul;
                FinalSound(last, out has, out rieul);
                withFinal = has;
            }
            if (withFinal) sb.Append(inner, 0, slash);
            else sb.Append(inner, slash + 1, inner.Length - slash - 1);
            return true;
        }

        static bool NeedsParse(string s) => s.IndexOf('{') >= 0 || s.IndexOf('}') >= 0;

        static int NextBrace(string s, int from)
        {
            for (int k = from; k < s.Length; k++)
                if (s[k] == '{' || s[k] == '}') return k;
            return s.Length;
        }

        // 받침 판정(src/Wizard.Text/Core/Josa.cs와 같은 규칙). 한글 음절: 종성 = (c − 0xAC00) mod 28, 0 = 없음, 8 = ㄹ.
        // 숫자는 읽은 소리(영 일 이 삼 사 오 육 칠 팔 구)의 끝 자리만 본다. 그 밖은 받침 없음. 끝의 공백·닫는 괄호·따옴표·줄임표는 건너뛴다.
        const int SyllableFirst = 0xAC00, SyllableLast = 0xD7A3, FinalCount = 28, RieulFinal = 8;
        static readonly bool[] DigitHasFinal = { true, true, false, true, false, false, true, true, true, false };
        static readonly bool[] DigitIsRieul = { false, true, false, false, false, false, false, true, true, false };

        /// <summary>끝 글자의 받침 유무와 ㄹ 받침 여부.</summary>
        public static void FinalSound(string s, out bool hasFinal, out bool isRieul)
        {
            hasFinal = false;
            isRieul = false;
            if (s == null) return;
            int i = s.Length - 1;
            while (i >= 0 && IsSkipped(s[i])) i--;
            if (i < 0) return;
            char c = s[i];
            if (c >= SyllableFirst && c <= SyllableLast)
            {
                int final = (c - SyllableFirst) % FinalCount;
                hasFinal = final != 0;
                isRieul = final == RieulFinal;
                return;
            }
            if (c >= '0' && c <= '9')
            {
                hasFinal = DigitHasFinal[c - '0'];
                isRieul = DigitIsRieul[c - '0'];
            }
        }

        static bool IsSkipped(char c) => c == ' ' || c == ')' || c == ']' || c == '"' || c == '…';
    }
}
