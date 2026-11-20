using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Wizard.Solo.Audio
{
    /// <summary>고른 클립 하나(겹소리면 여러 개가 나온다).</summary>
    public struct ResolvedClip
    {
        /// <summary>Resources 경로(확장자 없음), 예 "Audio/char.2/fire_band_0".</summary>
        public string Path;
        /// <summary>요청 음높이에 곱할 배율(음계 고르기의 나머지 반음. 그 밖은 1).</summary>
        public float PitchMul;
        /// <summary>요청 음높이를 이미 음계 고르기로 썼다(요청 배율을 다시 곱하지 않는다).</summary>
        public bool PitchConsumed;
        public float GainDb;
        public bool Loop;
        public override string ToString() => Path + (Loop ? "(loop)" : "");
    }

    /// <summary>
    /// 소리 사건 → 클립 표(Resources/DataSolo/Audio.json, 10 요약서 5절). 순수 클래스(클립을 읽지 않는다 — 경로만 고른다).
    /// 찾는 순서: 지금 캐릭터의 events[사건] → 사건에 묶인 sfx id 하나마다 (캐릭터 sfx → 공용 sfx). 공용 events도 캐릭터 events 다음에 본다.
    /// </summary>
    public sealed class SoundBank
    {
        public const string Resource = "DataSolo/Audio";

        abstract class Choice
        {
            public float GainDb;
            public bool Loop;
            public abstract void Collect(List<string> into);
            /// <summary>클립 이름 하나를 고른다(null = 이 단계에는 소리 없음). semis = 요청 음높이(반음).</summary>
            public abstract string Pick(int stage, float semis, Func<int, int> rnd, out float residualSemis, out bool consumed);
        }

        sealed class Fixed : Choice
        {
            public string Name;
            public override void Collect(List<string> into) { into.Add(Name); }
            public override string Pick(int stage, float semis, Func<int, int> rnd, out float r, out bool c) { r = 0f; c = false; return Name; }
        }

        sealed class PickOne : Choice
        {
            public Choice[] Items;
            public override void Collect(List<string> into) { foreach (var i in Items) i.Collect(into); }
            public override string Pick(int stage, float semis, Func<int, int> rnd, out float r, out bool c)
            {
                r = 0f; c = false;
                if (Items.Length == 0) return null;
                int k = Items.Length == 1 ? 0 : rnd(Items.Length);
                if (k < 0 || k >= Items.Length) k = 0;
                return Items[k].Pick(stage, semis, rnd, out r, out c);
            }
        }

        /// <summary>부를 때마다 다음 것(차례로 돈다). 한 연출 안에서 정해진 횟수로 이어 부르는 사건용(E-18 금 번짐 세 번이 한 음씩 위).</summary>
        sealed class Cycle : Choice
        {
            public Choice[] Items;
            int _next;
            public override void Collect(List<string> into) { foreach (var i in Items) i.Collect(into); }
            public override string Pick(int stage, float semis, Func<int, int> rnd, out float r, out bool c)
            {
                r = 0f; c = false;
                if (Items.Length == 0) return null;
                int k = _next % Items.Length;
                _next = (_next + 1) % Items.Length;
                return Items[k].Pick(stage, semis, rnd, out r, out c);
            }
        }

        sealed class ByStage : Choice
        {
            public int[] Lo, Hi;
            public Choice[] Items;
            public override void Collect(List<string> into) { foreach (var i in Items) i.Collect(into); }
            public override string Pick(int stage, float semis, Func<int, int> rnd, out float r, out bool c)
            {
                r = 0f; c = false;
                for (int i = 0; i < Items.Length; i++)
                    if (stage >= Lo[i] && stage <= Hi[i]) return Items[i].Pick(stage, semis, rnd, out r, out c);
                return null;
            }
        }

        sealed class Scale : Choice
        {
            public string[] Names;
            public int[] Semis;
            public override void Collect(List<string> into) { into.AddRange(Names); }
            public override string Pick(int stage, float semis, Func<int, int> rnd, out float r, out bool c)
            {
                c = true;
                r = 0f;
                if (Names.Length == 0) return null;
                // 옥타브 밖은 가장 가까운 아래 음 + 나머지(옥타브 포함)를 음높이로 낸다.
                int whole = (int)Math.Floor(semis + 0.001f);
                int octave = (int)Math.Floor(whole / 12.0);
                int inOct = whole - octave * 12;
                int best = 0;
                for (int i = 0; i < Semis.Length; i++) if (Semis[i] <= inOct && Semis[i] >= Semis[best]) best = i;
                if (Semis[best] > inOct) best = 0;
                r = semis - Semis[best];   // 옥타브만큼은 음높이로 올린다(n2 + 12반음 = n2 × 2)
                return Names[best];
            }
        }

        sealed class Entry
        {
            public Choice[] Layers;
        }

        sealed class Table
        {
            public string Folder;
            public readonly Dictionary<string, Entry> Events = new Dictionary<string, Entry>();
            public readonly Dictionary<string, Entry> Sfx = new Dictionary<string, Entry>();
        }

        readonly Dictionary<string, Table> _chars = new Dictionary<string, Table>();
        Table _common = new Table { Folder = "common" };
        readonly HashSet<string> _cutSfx = new HashSet<string>();

        public string ClipRoot = "Audio";
        public string MusicRoot = "Audio/bgm";
        public float RepeatMinIntervalSeconds = 0.045f;
        public float RepeatWindowSeconds = 0.3f;
        public float RepeatPitchJitterSemitones = 0.4f;
        public float CutFadeSeconds = 0.02f;
        public float StopFadeSeconds = 0.08f;
        public float MusicFadeSeconds = 0.5f;
        public int ExtraVoices = 8;

        public IEnumerable<string> Characters => _chars.Keys;

        public static SoundBank Parse(string json)
        {
            var b = new SoundBank();
            if (string.IsNullOrEmpty(json)) return b;
            var root = JObject.Parse(json);
            b.ClipRoot = root.Value<string>("clip_root") ?? b.ClipRoot;
            b.MusicRoot = root.Value<string>("music_root") ?? b.MusicRoot;
            var mix = root["mix"] as JObject;
            if (mix != null)
            {
                b.RepeatMinIntervalSeconds = mix.Value<float?>("repeat_min_interval_seconds") ?? b.RepeatMinIntervalSeconds;
                b.RepeatWindowSeconds = mix.Value<float?>("repeat_window_seconds") ?? b.RepeatWindowSeconds;
                b.RepeatPitchJitterSemitones = mix.Value<float?>("repeat_pitch_jitter_semitones") ?? b.RepeatPitchJitterSemitones;
                b.CutFadeSeconds = mix.Value<float?>("cut_fade_seconds") ?? b.CutFadeSeconds;
                b.StopFadeSeconds = mix.Value<float?>("stop_fade_seconds") ?? b.StopFadeSeconds;
                b.MusicFadeSeconds = mix.Value<float?>("music_fade_seconds") ?? b.MusicFadeSeconds;
                b.ExtraVoices = mix.Value<int?>("extra_voices") ?? b.ExtraVoices;
            }
            var cut = root["cut_sfx"] as JArray;
            if (cut != null) foreach (var t in cut) if (t.Type == JTokenType.String) b._cutSfx.Add((string)t);
            var common = root["common"] as JObject;
            if (common != null) b._common = ParseTable(common, "common");
            var chars = root["characters"] as JObject;
            if (chars != null)
                foreach (var p in chars.Properties())
                {
                    var o = p.Value as JObject;
                    if (o != null) b._chars[p.Name] = ParseTable(o, p.Name);
                }
            return b;
        }

        static Table ParseTable(JObject o, string defaultFolder)
        {
            var t = new Table { Folder = o.Value<string>("folder") ?? defaultFolder };
            ParseMap(o["events"] as JObject, t.Events);
            ParseMap(o["sfx"] as JObject, t.Sfx);
            return t;
        }

        static void ParseMap(JObject o, Dictionary<string, Entry> into)
        {
            if (o == null) return;
            foreach (var p in o.Properties())
            {
                var layers = new List<Choice>();
                if (p.Value is JArray arr) { foreach (var x in arr) { var c = ParseChoice(x); if (c != null) layers.Add(c); } }
                else { var c = ParseChoice(p.Value); if (c != null) layers.Add(c); }
                into[p.Name] = new Entry { Layers = layers.ToArray() };
            }
        }

        static Choice ParseChoice(JToken t)
        {
            if (t == null) return null;
            if (t.Type == JTokenType.String) return new Fixed { Name = (string)t };
            var o = t as JObject;
            if (o == null) return null;
            Choice c = null;
            if (o["pick"] is JArray pick)
            {
                var items = new List<Choice>();
                foreach (var x in pick) { var ch = ParseChoice(x); if (ch != null) items.Add(ch); }
                c = new PickOne { Items = items.ToArray() };
            }
            else if (o["cycle"] is JArray cyc)
            {
                var items = new List<Choice>();
                foreach (var x in cyc) { var ch = ParseChoice(x); if (ch != null) items.Add(ch); }
                c = new Cycle { Items = items.ToArray() };
            }
            else if (o["stages"] is JArray st)
            {
                var lo = new List<int>(); var hi = new List<int>(); var items = new List<Choice>();
                foreach (var x in st)
                {
                    var a = x as JArray;
                    if (a == null || a.Count < 3) continue;
                    var ch = ParseChoice(a[2]);
                    if (ch == null) continue;
                    lo.Add((int)a[0]); hi.Add((int)a[1]); items.Add(ch);
                }
                c = new ByStage { Lo = lo.ToArray(), Hi = hi.ToArray(), Items = items.ToArray() };
            }
            else if (o["scale"] is JArray sc)
            {
                var names = new List<string>(); var semis = new List<int>();
                foreach (var x in sc)
                {
                    var a = x as JArray;
                    if (a == null || a.Count < 2) continue;
                    names.Add((string)a[0]); semis.Add((int)a[1]);
                }
                c = new Scale { Names = names.ToArray(), Semis = semis.ToArray() };
            }
            else if (o["clip"] != null) c = new Fixed { Name = (string)o["clip"] };
            if (c == null) return null;
            c.GainDb = o.Value<float?>("gain_db") ?? 0f;
            c.Loop = o.Value<bool?>("loop") ?? false;
            return c;
        }

        /// <summary>그 sfx 묶음에 "모든 소리 끊기"(E-10 숨 멈춤)가 있나.</summary>
        public bool IsCut(IReadOnlyList<string> sfxIds)
        {
            if (sfxIds == null) return false;
            for (int i = 0; i < sfxIds.Count; i++) if (_cutSfx.Contains(sfxIds[i])) return true;
            return false;
        }

        /// <summary>사건 하나 → 낼 클립(0개 이상)을 into에 더한다. semis = 요청 음높이(반음, 음계 고르기용). rnd(n) = 0~n−1.</summary>
        public void Resolve(string eventId, IReadOnlyList<string> sfxIds, string characterId, int stage, float semis,
            Func<int, int> rnd, List<ResolvedClip> into)
        {
            if (string.IsNullOrEmpty(eventId)) return;
            if (rnd == null) rnd = n => 0;
            Table ch = null;
            if (characterId != null) _chars.TryGetValue(characterId, out ch);
            Entry e;
            if (ch != null && ch.Events.TryGetValue(eventId, out e)) { AddEntry(ch, e, stage, semis, rnd, into); return; }
            if (_common.Events.TryGetValue(eventId, out e)) { AddEntry(_common, e, stage, semis, rnd, into); return; }
            if (sfxIds == null) return;
            for (int i = 0; i < sfxIds.Count; i++)
            {
                string s = sfxIds[i];
                if (ch != null && ch.Sfx.TryGetValue(s, out e)) AddEntry(ch, e, stage, semis, rnd, into);
                else if (_common.Sfx.TryGetValue(s, out e)) AddEntry(_common, e, stage, semis, rnd, into);
            }
        }

        void AddEntry(Table t, Entry e, int stage, float semis, Func<int, int> rnd, List<ResolvedClip> into)
        {
            for (int i = 0; i < e.Layers.Length; i++)
            {
                var c = e.Layers[i];
                float r; bool consumed;
                string name = c.Pick(stage, semis, rnd, out r, out consumed);
                if (string.IsNullOrEmpty(name)) continue;
                string path = PathOf(t.Folder, name);
                bool dup = false;
                for (int k = 0; k < into.Count; k++) if (into[k].Path == path) { dup = true; break; }
                if (dup) continue;
                into.Add(new ResolvedClip
                {
                    Path = path,
                    PitchMul = consumed ? (float)Math.Pow(2.0, r / 12.0) : 1f,
                    PitchConsumed = consumed,
                    GainDb = c.GainDb,
                    Loop = c.Loop
                });
            }
        }

        /// <summary>클립 경로. 이름에 '/'가 있으면 clip_root 아래 경로로 읽는다(캐릭터 표가 공용 폴더의 클립을 가리킬 때, 예 "common/enh_card_mark_3").</summary>
        string PathOf(string folder, string name) =>
            name.IndexOf('/') >= 0 ? ClipRoot + "/" + name : ClipRoot + "/" + folder + "/" + name;

        /// <summary>표가 가리키는 모든 클립 경로(테스트: Resources에 실제로 있는지).</summary>
        public List<string> AllClipPaths()
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            var names = new List<string>();
            void Add(Table t)
            {
                foreach (var m in new[] { t.Events, t.Sfx })
                    foreach (var kv in m)
                        foreach (var c in kv.Value.Layers)
                        {
                            names.Clear();
                            c.Collect(names);
                            foreach (var n in names) set.Add(PathOf(t.Folder, n));
                        }
            }
            Add(_common);
            foreach (var kv in _chars) Add(kv.Value);
            return new List<string>(set);
        }

        /// <summary>그 캐릭터가 이 사건에 사건 단위 배치를 갖나(빈 배열 = 일부러 무음도 참).</summary>
        public bool HasEventEntry(string characterId, string eventId)
        {
            Table ch;
            if (characterId != null && _chars.TryGetValue(characterId, out ch) && ch.Events.ContainsKey(eventId)) return true;
            return _common.Events.ContainsKey(eventId);
        }
    }
}
