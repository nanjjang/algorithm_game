using System;
using System.Collections.Generic;
using System.Text;
using Wizard.DataBuild.Names;

namespace Wizard.Text.Tests
{
    // 테스트용 이름 표. DataBuild 와 같은 길(names.ko.json 꼴의 글 → NamesFile → NameTableWriter → 이진 절)로 만들어 NameTable.Load 로 읽는다.
    // 저장소의 data/names/names.ko.json 은 M6-TXT-02 가 초기 값 전체를 채운다. 벡터의 "초기 이름 표"는 spec 14 2.3절·3.12절의 값을 여기 적는다.
    internal static class NameFixtures
    {
        // spec 14 2.3절(캐릭터, 지팡이, 장소·인물)과 3.12절(+13 칭호 틀, 시즌 칭호 틀)
        public static readonly KeyValuePair<string, string>[] Initial =
        {
            Kv("char.1", "제자 1"),
            Kv("char.2", "제자 2"),
            Kv("char.3", "제자 3"),
            Kv("char.4", "제자 4"),
            Kv("char.5", "제자 5"),
            Kv("npc.crow", "까마귀"),
            Kv("npc.master", "느루"),
            Kv("npc.peddler", "행상"),
            Kv("place.arena", "돌마당"),
            Kv("place.field", "한뎃벌"),
            Kv("place.plaza", "첫모닥 야영지"),
            Kv("place.tower", "느루의 탑"),
            Kv("place.workbench", "느루의 작업대"),
            Kv("title.season.heir", "제 {n}대 후계자"),
            Kv("title.season.top_disciple", "제{n}기 수제자"),
            Kv("title.stage.1.b4", "엄청엄청 위대한 {char.1}"),
            Kv("title.stage.2.b4", "엄청엄청 위대한 {char.2}"),
            Kv("title.stage.3.b4", "엄청엄청 위대한 {char.3}"),
            Kv("title.stage.4.b4", "엄청엄청 위대한 {char.4}"),
            Kv("title.stage.5.b4", "엄청엄청 위대한 {char.5}"),
            Kv("wand.1", "끓는 부지깽이"),
            Kv("wand.2", "깜빡이 등불대"),
            Kv("wand.3", "밑줄 긋는 참나무 지팡이"),
            Kv("wand.4", "고드름 홀"),
            Kv("wand.5", "눈 뜬 옹이 지팡이"),
        };

        public static KeyValuePair<string, string> Kv(string key, string value) => new KeyValuePair<string, string>(key, value);

        public static NameTable InitialTable() => Table(Initial);

        // 초기 값 위에 몇 줄을 바꾸거나 더한 표(TV-072·073: char.1 = "달구" 등)
        public static NameTable InitialWith(params KeyValuePair<string, string>[] overrides)
        {
            var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> kv in Initial)
            {
                map[kv.Key] = kv.Value;
            }
            foreach (KeyValuePair<string, string> kv in overrides)
            {
                map[kv.Key] = kv.Value;
            }
            return Table(map);
        }

        public static NameTable Table(IEnumerable<KeyValuePair<string, string>> entries) => NameTable.Load(Bytes(entries));

        // names.ko.json 꼴(CONTRACTS 5절)의 글을 DataBuild 로 읽어 이진 절을 만든다
        public static byte[] Bytes(IEnumerable<KeyValuePair<string, string>> entries)
        {
            var sorted = new List<KeyValuePair<string, string>>(entries);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var sb = new StringBuilder();
            sb.Append("{\n");
            foreach (KeyValuePair<string, string> kv in sorted)
            {
                sb.Append("  \"").Append(kv.Key).Append("\": \"").Append(Escape(kv.Value)).Append("\",\n");
            }
            sb.Append("  \"~end\": \"\"\n}\n");
            return NameTableWriter.Write(NamesFile.FromText(sb.ToString()));
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
