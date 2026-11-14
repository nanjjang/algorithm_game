using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.View.Effects
{
    /// <summary>event_bindings.json 한 줄(08 요약서 15.1절).</summary>
    public sealed class EventBinding
    {
        public string Event;
        public string[] Fx = new string[0];
        public string[] Sfx = new string[0];
        /// <summary>none / small / medium / large / xlarge.</summary>
        public string Shake = "none";
        public int HitstopMs;
        /// <summary>넷코드 확정 단계 전용 — 솔로는 늘 확정으로 본다.</summary>
        public bool ConfirmedOnly;

        public bool HasSound => Sfx != null && Sfx.Length > 0;
        public bool HasShake => !string.IsNullOrEmpty(Shake) && Shake != ShakeNone;
        public const string ShakeNone = "none";
    }

    /// <summary>
    /// data/presentation/event_bindings.json(사본 Resources/Data/presentation). 표시 이벤트 열쇠 → 그림·소리·흔들림·그림 정지.
    /// 소리 사건 id는 이 열쇠다(ISoundPlayer 주석, 10 요약서 5절 — 섞는 규칙의 분류도 이 열쇠로 한다).
    /// </summary>
    public sealed class EventBindings
    {
        readonly Dictionary<string, EventBinding> _byEvent = new Dictionary<string, EventBinding>();
        readonly List<EventBinding> _all = new List<EventBinding>();
        readonly HashSet<string> _sfxCatalog = new HashSet<string>();

        /// <summary>연속 스침 음높이 창(초). 같은 슬롯의 앞 스침과 이 안이면(같은 값 포함 — Q-22) 한 단씩.</summary>
        public float GrazeChainWindowSeconds;
        public int GrazeChainMaxStep;

        public IReadOnlyList<EventBinding> All => _all;
        public int Count => _all.Count;

        public static EventBindings Parse(string json)
        {
            var b = new EventBindings();
            if (string.IsNullOrEmpty(json)) return b;
            var root = JObject.Parse(json);
            var gc = root["graze_chain"] as JObject;
            if (gc != null)
            {
                b.GrazeChainWindowSeconds = gc.Value<float?>("window_seconds") ?? 0f;
                b.GrazeChainMaxStep = gc.Value<int?>("max_step") ?? 0;
            }
            var cat = root["sfx_catalog"] as JArray;
            if (cat != null)
                foreach (var t in cat) if (t.Type == JTokenType.String) b._sfxCatalog.Add(t.Value<string>());
            var arr = root["bindings"] as JArray;
            if (arr != null)
            {
                foreach (var t in arr)
                {
                    var o = t as JObject;
                    if (o == null) continue;
                    var e = new EventBinding
                    {
                        Event = o.Value<string>("event"),
                        Fx = Strings(o["fx"]),
                        Sfx = Strings(o["sfx"]),
                        Shake = o.Value<string>("shake") ?? EventBinding.ShakeNone,
                        HitstopMs = o.Value<int?>("hitstop_ms") ?? 0,
                        ConfirmedOnly = o.Value<bool?>("confirmed_only") ?? false
                    };
                    if (string.IsNullOrEmpty(e.Event)) continue;
                    b._all.Add(e);
                    b._byEvent[e.Event] = e;
                }
            }
            return b;
        }

        static string[] Strings(JToken t)
        {
            var a = t as JArray;
            if (a == null) return new string[0];
            var r = new List<string>(a.Count);
            foreach (var x in a) if (x.Type == JTokenType.String) r.Add(x.Value<string>());
            return r.ToArray();
        }

        /// <summary>없는 열쇠면 null.</summary>
        public EventBinding Get(string eventKey)
        {
            if (eventKey == null) return null;
            EventBinding b;
            return _byEvent.TryGetValue(eventKey, out b) ? b : null;
        }

        public bool Has(string eventKey) => eventKey != null && _byEvent.ContainsKey(eventKey);
        public bool InSfxCatalog(string sfxId) => sfxId != null && _sfxCatalog.Contains(sfxId);

        /// <summary>그 열쇠에 소리가 묶여 있나(빈 sfx = 나중에 꽂을 자리 — 지금은 부르지 않는다).</summary>
        public bool HasSound(string eventKey)
        {
            var b = Get(eventKey);
            return b != null && b.HasSound;
        }

        public int HitstopMs(string eventKey)
        {
            var b = Get(eventKey);
            return b != null ? b.HitstopMs : 0;
        }

        public string Shake(string eventKey)
        {
            var b = Get(eventKey);
            return b != null ? b.Shake : EventBinding.ShakeNone;
        }
    }
}
