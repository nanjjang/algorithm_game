using System;
using System.Collections.Generic;
using System.Globalization;

namespace Wizard.ViewLogic.Assets.Json
{
    // 그림 매니페스트를 읽기 위한 읽기 전용 JSON 나무(spec 14 5절).
    // 공유 코드는 System.Text.Json 을 쓰지 않고(ARCHITECTURE 3.3), Newtonsoft 는 Wizard.Client 만 참조하므로
    // 매니페스트 로더는 이 작은 판독기만 쓴다. 객체의 속성 순서는 파일 순서 그대로 지킨다(나열이 결정적이도록).
    // 숫자는 원문 그대로 들고 있다가 정수로 읽을 때만 바꾼다(소수는 정수로 읽히지 않는다).
    public sealed class ManifestJson
    {
        private static readonly IReadOnlyList<ManifestJson> NoItems = new ManifestJson[0];
        private static readonly IReadOnlyList<KeyValuePair<string, ManifestJson>> NoProperties = new KeyValuePair<string, ManifestJson>[0];

        private readonly string? _text;
        private readonly bool _bool;
        private readonly List<ManifestJson>? _items;
        private readonly List<KeyValuePair<string, ManifestJson>>? _properties;
        private readonly Dictionary<string, int>? _index;

        private ManifestJson(ManifestJsonKind kind, string pointer, string? text, bool b,
            List<ManifestJson>? items, List<KeyValuePair<string, ManifestJson>>? properties, Dictionary<string, int>? index)
        {
            Kind = kind;
            Pointer = pointer;
            _text = text;
            _bool = b;
            _items = items;
            _properties = properties;
            _index = index;
        }

        public ManifestJsonKind Kind { get; }

        // RFC 6901 꼴의 위치("/assets/3/file"). 진단 문구에 쓴다
        public string Pointer { get; }

        public bool IsNull => Kind == ManifestJsonKind.Null;

        public int Count => _items != null ? _items.Count : _properties != null ? _properties.Count : 0;

        public IReadOnlyList<ManifestJson> Items => _items ?? NoItems;

        public IReadOnlyList<KeyValuePair<string, ManifestJson>> Properties => _properties ?? NoProperties;

        public ManifestJson this[int index] => Items[index];

        internal static ManifestJson Null(string pointer) => new ManifestJson(ManifestJsonKind.Null, pointer, null, false, null, null, null);

        internal static ManifestJson Bool(string pointer, bool value) => new ManifestJson(ManifestJsonKind.Boolean, pointer, null, value, null, null, null);

        internal static ManifestJson Number(string pointer, string raw) => new ManifestJson(ManifestJsonKind.Number, pointer, raw, false, null, null, null);

        internal static ManifestJson Str(string pointer, string value) => new ManifestJson(ManifestJsonKind.String, pointer, value, false, null, null, null);

        internal static ManifestJson Array(string pointer, List<ManifestJson> items) => new ManifestJson(ManifestJsonKind.Array, pointer, null, false, items, null, null);

        internal static ManifestJson Object(string pointer, List<KeyValuePair<string, ManifestJson>> properties, Dictionary<string, int> index) =>
            new ManifestJson(ManifestJsonKind.Object, pointer, null, false, null, properties, index);

        public bool Has(string name) => _index != null && _index.ContainsKey(name);

        public bool TryGet(string name, out ManifestJson value)
        {
            if (_index != null && _index.TryGetValue(name, out int i))
            {
                value = _properties![i].Value;
                return true;
            }
            value = null!;
            return false;
        }

        // 없거나 null 이면 null
        public ManifestJson? Get(string name) => TryGet(name, out ManifestJson v) && !v.IsNull ? v : null;

        public bool TryGetString(out string value)
        {
            if (Kind == ManifestJsonKind.String)
            {
                value = _text!;
                return true;
            }
            value = "";
            return false;
        }

        public bool TryGetBool(out bool value)
        {
            value = _bool;
            return Kind == ManifestJsonKind.Boolean;
        }

        // 부호 있는 32비트 정수로 읽히는 숫자만(소수점·지수 표기는 거절)
        public bool TryGetInt(out int value)
        {
            value = 0;
            return Kind == ManifestJsonKind.Number
                && int.TryParse(_text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        // 길이 n 인 정수 배열([x, y], [l, t, r, b] …)
        public bool TryGetIntArray(int length, out int[] values)
        {
            values = new int[length];
            if (Kind != ManifestJsonKind.Array || _items!.Count != length)
            {
                return false;
            }
            for (int i = 0; i < length; i++)
            {
                if (!_items[i].TryGetInt(out values[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // 숫자의 원문(정수가 아닌 값을 진단에 그대로 보여 줄 때)
        public string RawText => _text ?? "";

        public override string ToString()
        {
            switch (Kind)
            {
                case ManifestJsonKind.Null: return "null";
                case ManifestJsonKind.Boolean: return _bool ? "true" : "false";
                case ManifestJsonKind.Number: return _text!;
                case ManifestJsonKind.String: return "\"" + _text + "\"";
                case ManifestJsonKind.Array: return "[" + Count.ToString(CultureInfo.InvariantCulture) + " items]";
                case ManifestJsonKind.Object: return "{" + Count.ToString(CultureInfo.InvariantCulture) + " properties}";
                default: throw new InvalidOperationException();
            }
        }
    }
}
