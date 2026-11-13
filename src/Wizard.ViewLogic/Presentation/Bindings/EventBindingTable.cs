using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Wizard.Sim;

namespace Wizard.ViewLogic.Presentation
{
    // data/presentation/event_bindings.json 과 event_bindings.none.json 을 읽은 표(CONTRACTS 13절, ARCHITECTURE 9절).
    // 이벤트 → 그림·소리의 대응은 코드가 아니라 이 데이터다. 에셋이 생기면 이 표와 카탈로그만 채운다.
    // 읽기 검사(오류 목록에 쌓는다. 파일이 틀린 것은 프로그래머 오류가 아니라 데이터 오류라 던지지 않는다. CONTRACTS 0.2):
    //   - 꼴: 필수 칸, 칸의 종류, 모르는 칸 없음(스키마 data/schema/event_bindings*.schema.json 과 같은 규칙)
    //   - fx 는 fx_catalog(effects.md 20절), sfx 는 sfx_catalog(effects.md 19절) 안의 식별자만. 없는 식별자를 가리키면 오류
    //   - 같은 열쇠가 두 줄에 있거나, 바인딩 줄과 "의도적 없음" 목록에 같이 있으면 오류
    // 빠짐 검사(모든 SimEventType 과 UiEventKeys.All 이 어느 한쪽에 있는가)는 EventBindingCoverage 가 한다.
    public sealed class EventBindingTable
    {
        private readonly Dictionary<string, EventBinding> _byKey;
        private readonly Dictionary<string, string> _none;              // 열쇠 → reason
        private readonly HashSet<string> _fx;
        private readonly HashSet<string> _sfx;

        private EventBindingTable(GrazeChainSettings grazeChain, List<EventBinding> bindings, Dictionary<string, EventBinding> byKey,
            List<string> noneKeys, Dictionary<string, string> none, List<FxCatalogEntry> fxCatalog, List<string> sfxCatalog)
        {
            GrazeChain = grazeChain;
            Bindings = bindings;
            _byKey = byKey;
            NoneKeys = noneKeys;
            _none = none;
            FxCatalog = fxCatalog;
            SfxCatalog = sfxCatalog;
            _fx = new HashSet<string>(StringComparer.Ordinal);
            foreach (FxCatalogEntry e in fxCatalog)
            {
                _fx.Add(e.Fx);
            }
            _sfx = new HashSet<string>(sfxCatalog, StringComparer.Ordinal);
        }

        public GrazeChainSettings GrazeChain { get; }

        public IReadOnlyList<EventBinding> Bindings { get; }              // 파일 순서

        public IReadOnlyList<string> NoneKeys { get; }                    // 파일 순서

        public IReadOnlyList<FxCatalogEntry> FxCatalog { get; }

        public IReadOnlyList<string> SfxCatalog { get; }

        public bool TryGet(string eventKey, [NotNullWhen(true)] out EventBinding? binding) => _byKey.TryGetValue(eventKey, out binding);

        public bool IsIntentionallyNone(string eventKey) => _none.ContainsKey(eventKey);

        public string? NoneReason(string eventKey) => _none.TryGetValue(eventKey, out string? r) ? r : null;

        public bool IsKnownFx(string fxId) => _fx.Contains(fxId);

        public bool IsKnownSfx(string sfxId) => _sfx.Contains(sfxId);

        // 두 파일의 글을 읽는다. 늘 표를 돌려주고(읽을 수 있는 만큼), 문제는 errors 에 더한다. errors 가 비어야 쓸 수 있는 표다
        public static EventBindingTable Load(string bindingsJson, string noneJson, List<string> errors)
        {
            var bindings = new List<EventBinding>();
            var byKey = new Dictionary<string, EventBinding>(StringComparer.Ordinal);
            var noneKeys = new List<string>();
            var none = new Dictionary<string, string>(StringComparer.Ordinal);
            var fxCatalog = new List<FxCatalogEntry>();
            var sfxCatalog = new List<string>();
            var graze = new GrazeChainSettings(0, 0);

            BindingJson.Node? root = BindingJson.Parse(bindingsJson, out string? parseError);
            if (root == null || root.Kind != BindingJson.Kind.Object)
            {
                errors.Add(BindingsFile + ": " + (parseError ?? "root must be an object"));
            }
            else
            {
                CheckMembers(root, BindingsFile, errors, NoteField, "graze_chain", "sfx_catalog", "fx_catalog", "bindings");
                graze = ReadGrazeChain(root.Get("graze_chain"), errors);
                ReadSfxCatalog(root.Get("sfx_catalog"), sfxCatalog, errors);
                ReadFxCatalog(root.Get("fx_catalog"), fxCatalog, errors);
            }

            var fxSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (FxCatalogEntry e in fxCatalog)
            {
                fxSet.Add(e.Fx);
            }
            var sfxSet = new HashSet<string>(sfxCatalog, StringComparer.Ordinal);
            if (root != null && root.Kind == BindingJson.Kind.Object)
            {
                ReadBindings(root.Get("bindings"), fxSet, sfxSet, bindings, byKey, errors);
            }

            BindingJson.Node? noneRoot = BindingJson.Parse(noneJson, out string? noneError);
            if (noneRoot == null || noneRoot.Kind != BindingJson.Kind.Object)
            {
                errors.Add(NoneFile + ": " + (noneError ?? "root must be an object"));
            }
            else
            {
                CheckMembers(noneRoot, NoneFile, errors, NoteField, "none");
                ReadNone(noneRoot.Get("none"), byKey, noneKeys, none, errors);
            }

            return new EventBindingTable(graze, bindings, byKey, noneKeys, none, fxCatalog, sfxCatalog);
        }

        public const string BindingsFile = "data/presentation/event_bindings.json";
        public const string NoneFile = "data/presentation/event_bindings.none.json";
        private const string NoteField = "_note";

        private static GrazeChainSettings ReadGrazeChain(BindingJson.Node? n, List<string> errors)
        {
            const string where = BindingsFile + " graze_chain";
            if (n == null || n.Kind != BindingJson.Kind.Object)
            {
                errors.Add(where + ": missing object");
                return new GrazeChainSettings(0, 0);
            }
            CheckMembers(n, where, errors, "window_seconds", "max_step");
            int windowTicks = 0;
            BindingJson.Node? w = n.Get("window_seconds");
            if (w == null || w.Kind != BindingJson.Kind.Number
                || !decimal.TryParse(w.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal seconds) || seconds <= 0m)
            {
                errors.Add(where + ".window_seconds: positive number required");
            }
            else
            {
                // 초 → 틱(× 60). 가장 가까운 정수, 같으면 짝수(DataBuild Units 와 같은 반올림)
                windowTicks = (int)decimal.Round(seconds * SimVersion.TickRate, MidpointRounding.ToEven);
            }
            int maxStep = ReadNonNegativeInt(n.Get("max_step"), where + ".max_step", errors);
            return new GrazeChainSettings(windowTicks, maxStep);
        }

        private static void ReadSfxCatalog(BindingJson.Node? n, List<string> into, List<string> errors)
        {
            const string where = BindingsFile + " sfx_catalog";
            if (n == null || n.Kind != BindingJson.Kind.Array || n.Items == null)
            {
                errors.Add(where + ": missing array");
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (BindingJson.Node item in n.Items)
            {
                if (item.Kind != BindingJson.Kind.String || !IsSfxId(item.Text))
                {
                    errors.Add(where + ": '" + item.Text + "' is not an sfx id (sfx_<e|c|t|h|n><two digits>[a-c])");
                    continue;
                }
                if (!seen.Add(item.Text))
                {
                    errors.Add(where + ": duplicate '" + item.Text + "'");
                    continue;
                }
                into.Add(item.Text);
            }
        }

        private static void ReadFxCatalog(BindingJson.Node? n, List<FxCatalogEntry> into, List<string> errors)
        {
            const string where = BindingsFile + " fx_catalog";
            if (n == null || n.Kind != BindingJson.Kind.Array || n.Items == null)
            {
                errors.Add(where + ": missing array");
                return;
            }
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (BindingJson.Node item in n.Items)
            {
                if (item.Kind != BindingJson.Kind.Object)
                {
                    errors.Add(where + ": entries must be objects");
                    continue;
                }
                CheckMembers(item, where, errors, "fx", "src");
                string fx = ReadString(item.Get("fx"));
                string src = ReadString(item.Get("src"));
                if (!IsFxId(fx))
                {
                    errors.Add(where + ": '" + fx + "' is not an fx id (fx_… or ui_…)");
                    continue;
                }
                if (src.Length == 0)
                {
                    errors.Add(where + ": '" + fx + "' has no src");
                    continue;
                }
                if (!seen.Add(fx))
                {
                    errors.Add(where + ": duplicate '" + fx + "'");
                    continue;
                }
                into.Add(new FxCatalogEntry(fx, src));
            }
        }

        private static void ReadBindings(BindingJson.Node? n, HashSet<string> fxSet, HashSet<string> sfxSet,
            List<EventBinding> into, Dictionary<string, EventBinding> byKey, List<string> errors)
        {
            const string where = BindingsFile + " bindings";
            if (n == null || n.Kind != BindingJson.Kind.Array || n.Items == null)
            {
                errors.Add(where + ": missing array");
                return;
            }
            foreach (BindingJson.Node item in n.Items)
            {
                if (item.Kind != BindingJson.Kind.Object)
                {
                    errors.Add(where + ": entries must be objects");
                    continue;
                }
                string key = ReadString(item.Get("event"));
                string at = where + " '" + key + "'";
                CheckMembers(item, at, errors, "event", "fx", "sfx", "shake", "hitstop_ms", "confirmed_only");
                if (!IsEventKey(key))
                {
                    errors.Add(at + ": event key must be <zone>.<snake_case>[.<snake_case>…]");
                    continue;
                }
                List<string> fx = ReadIds(item.Get("fx"), at + ".fx", errors);
                List<string> sfx = ReadIds(item.Get("sfx"), at + ".sfx", errors);
                foreach (string f in fx)
                {
                    if (!fxSet.Contains(f))
                    {
                        errors.Add(at + ": fx '" + f + "' is not in fx_catalog (effects.md 20)");
                    }
                }
                foreach (string s in sfx)
                {
                    if (!sfxSet.Contains(s))
                    {
                        errors.Add(at + ": sfx '" + s + "' is not in sfx_catalog (effects.md 19)");
                    }
                }
                BindingShake shake = ReadShake(item.Get("shake"), at, errors);
                int hitstop = ReadNonNegativeInt(item.Get("hitstop_ms"), at + ".hitstop_ms", errors);
                BindingJson.Node? co = item.Get("confirmed_only");
                if (co == null || co.Kind != BindingJson.Kind.Bool)
                {
                    errors.Add(at + ".confirmed_only: boolean required");
                }
                var b = new EventBinding(key, fx, sfx, shake, hitstop, co != null && co.Bool);
                if (byKey.ContainsKey(key))
                {
                    errors.Add(at + ": the same event is bound twice");
                    continue;
                }
                byKey.Add(key, b);
                into.Add(b);
            }
        }

        private static void ReadNone(BindingJson.Node? n, Dictionary<string, EventBinding> byKey, List<string> keys,
            Dictionary<string, string> none, List<string> errors)
        {
            const string where = NoneFile + " none";
            if (n == null || n.Kind != BindingJson.Kind.Array || n.Items == null)
            {
                errors.Add(where + ": missing array");
                return;
            }
            foreach (BindingJson.Node item in n.Items)
            {
                if (item.Kind != BindingJson.Kind.Object)
                {
                    errors.Add(where + ": entries must be objects");
                    continue;
                }
                string key = ReadString(item.Get("event"));
                string reason = ReadString(item.Get("reason"));
                string at = where + " '" + key + "'";
                CheckMembers(item, at, errors, "event", "reason");
                if (!IsEventKey(key))
                {
                    errors.Add(at + ": event key must be <zone>.<snake_case>[.<snake_case>…]");
                    continue;
                }
                if (reason.Length == 0)
                {
                    errors.Add(at + ": reason required");
                }
                if (byKey.ContainsKey(key))
                {
                    errors.Add(at + ": also has a binding line (a key is either bound or intentionally none)");
                }
                if (none.ContainsKey(key))
                {
                    errors.Add(at + ": listed twice");
                    continue;
                }
                none.Add(key, reason);
                keys.Add(key);
            }
        }

        private static List<string> ReadIds(BindingJson.Node? n, string where, List<string> errors)
        {
            var list = new List<string>();
            if (n == null || n.Kind != BindingJson.Kind.Array || n.Items == null)
            {
                errors.Add(where + ": array required");
                return list;
            }
            foreach (BindingJson.Node item in n.Items)
            {
                if (item.Kind != BindingJson.Kind.String)
                {
                    errors.Add(where + ": ids must be strings");
                    continue;
                }
                if (list.Contains(item.Text))
                {
                    errors.Add(where + ": duplicate '" + item.Text + "'");
                    continue;
                }
                list.Add(item.Text);
            }
            return list;
        }

        private static BindingShake ReadShake(BindingJson.Node? n, string at, List<string> errors)
        {
            string s = n != null && n.Kind == BindingJson.Kind.String ? n.Text : "";
            switch (s)
            {
                case "none": return BindingShake.None;
                case "small": return BindingShake.Small;
                case "medium": return BindingShake.Medium;
                case "large": return BindingShake.Large;
                case "xlarge": return BindingShake.XLarge;
                default:
                    errors.Add(at + ".shake: one of none, small, medium, large, xlarge");
                    return BindingShake.None;
            }
        }

        private static int ReadNonNegativeInt(BindingJson.Node? n, string where, List<string> errors)
        {
            if (n == null || n.Kind != BindingJson.Kind.Number
                || !int.TryParse(n.Text, NumberStyles.None, CultureInfo.InvariantCulture, out int v))
            {
                errors.Add(where + ": non-negative integer required");
                return 0;
            }
            return v;
        }

        private static string ReadString(BindingJson.Node? n) => n != null && n.Kind == BindingJson.Kind.String ? n.Text : "";

        // 모르는 칸(스키마의 additionalProperties: false)과 빠진 칸(_note 는 선택)
        private static void CheckMembers(BindingJson.Node obj, string where, List<string> errors, params string[] allowed)
        {
            if (obj.Members == null)
            {
                return;
            }
            foreach (KeyValuePair<string, BindingJson.Node> m in obj.Members)
            {
                if (Array.IndexOf(allowed, m.Key) < 0)
                {
                    errors.Add(where + ": unknown field '" + m.Key + "'");
                }
            }
            foreach (string a in allowed)
            {
                if (!string.Equals(a, NoteField, StringComparison.Ordinal) && obj.Get(a) == null)
                {
                    errors.Add(where + ": missing field '" + a + "'");
                }
            }
        }

        // "<마디>.<마디>[.<마디>…]", 마디는 a-z0-9_ 이고 첫 글자는 a-z
        public static bool IsEventKey(string key)
        {
            if (key.Length == 0 || key[0] < 'a' || key[0] > 'z')
            {
                return false;
            }
            int dots = 0;
            int len = 0;
            foreach (char c in key)
            {
                if (c == '.')
                {
                    if (len == 0)
                    {
                        return false;
                    }
                    dots++;
                    len = 0;
                    continue;
                }
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }
                len++;
            }
            return dots > 0 && len > 0;
        }

        // sfx_ + 사건 묶음 글자(e 강화, c 전투, t 비기, h 적중, n 적·환경) + 두 자리 + 선택 a~c(T-09a 등). effects.md 19절
        public static bool IsSfxId(string id)
        {
            const string prefix = "sfx_";
            if (!id.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }
            int p = prefix.Length;
            int rest = id.Length - p;
            if (rest != 3 && rest != 4)
            {
                return false;
            }
            char group = id[p];
            if (group != 'e' && group != 'c' && group != 't' && group != 'h' && group != 'n')
            {
                return false;
            }
            if (!IsAsciiDigit(id[p + 1]) || !IsAsciiDigit(id[p + 2]))
            {
                return false;
            }
            return rest == 3 || (id[p + 3] >= 'a' && id[p + 3] <= 'c');
        }

        private static bool IsAsciiDigit(char c) => c >= '0' && c <= '9';

        // fx_… (효과 그림) 또는 ui_… (effects.md 20절이 크기를 갖는 1배 화면 요소). spec 01 17.2절의 꼴
        public static bool IsFxId(string id)
        {
            bool prefixed = id.StartsWith("fx_", StringComparison.Ordinal) || id.StartsWith("ui_", StringComparison.Ordinal);
            if (!prefixed || id.Length <= 3)
            {
                return false;
            }
            for (int i = 3; i < id.Length; i++)
            {
                char c = id[i];
                if (!((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_'))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
