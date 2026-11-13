using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.Sim;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/tries.json → 팩 절 14 tries. 읽는 쪽: src/Wizard.Content/Pack/Sections/ContentDb.Tries.cs. 스키마: data/schema/tries.schema.json.
    //   parser       → 판정기 상수(spec 03 1.2). 초 → 입력 시각 단위(× 15,360 = SimTime.InputUnitsPerSecond, 가장 가까운 정수, 같으면 짝수).
    //                  0.07초 = 1,075.2 → 1,075, 0.18초 = 2,764.8 → 2,765(spec 03 1.2 "원문 지정"과 같다). max_debt 는 결정 수 그대로(9.2)
    //   characters[] → character 의 CharacterId(char.1~char.5), arts 의 ArtId(ContentIds)를 오름차순으로 바꿔 쓴다
    // 절 안의 규칙: 캐릭터 겹침, 한 캐릭터 안의 비기 겹침, K 보관 창 ≤ 재사용 대기(spec 03 10절).
    // 비기가 공용 + 그 캐릭터의 고유인지(arts 절과의 맞물림)는 판정기 데이터를 만드는 곳(Wizard.Sim.Arts.ParserSourceLoader)이 본다.
    //
    // 절 바이트(LE). 꼴은 ContentDb.Tries.cs 머리 주석과 같다:
    //   i64 just_window  i64 gap_full_bloom  i64 gap_short  i64 gap_same_side  i64 gap_default  i64 art_cooldown  i64 k_buffer_window
    //   i32 max_debt
    //   u16 개수
    //   항목 × 개수(CharacterId 오름차순, 겹침 없음): u8 character  u8 art_count  u8 art × art_count(ArtId 오름차순, 겹침 없음)
    public sealed class TriesConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Tries;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            JsonElement parser = file.Root.GetProperty("parser");
            long just = InputUnits(parser, "just_window_seconds");
            long gapBloom = InputUnits(parser, "gap_full_bloom_seconds");
            long gapShort = InputUnits(parser, "gap_short_seconds");
            long gapSide = InputUnits(parser, "gap_same_side_seconds");
            long gapDefault = InputUnits(parser, "gap_default_seconds");
            long cooldown = InputUnits(parser, "art_cooldown_seconds");
            long buffer = InputUnits(parser, "k_buffer_window_seconds");
            int maxDebt = Int(parser.GetProperty("max_debt"), "max_debt");
            if (buffer > cooldown)
            {
                diagnostics.Error(file.RepoPath, "/parser/k_buffer_window_seconds", "K 보관 창이 재사용 대기보다 길다(spec 03 10절)");
            }

            var entries = new List<Entry>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("characters").EnumerateArray())
            {
                string at = "/characters/" + i.ToString(CultureInfo.InvariantCulture);
                Entry? e = Read(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = entries.Count;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: CharacterId 오름차순. 같은 id 끼리는 원본 순서(진단이 뒤에 나온 줄을 가리킨다)
            entries.Sort((a, b) => a.Character != b.Character ? ((byte)a.Character).CompareTo((byte)b.Character) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Character == entries[k - 1].Character)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/character", "캐릭터 '" + ContentIds.ToId(entries[k].Character) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.I64(just);
            output.I64(gapBloom);
            output.I64(gapShort);
            output.I64(gapSide);
            output.I64(gapDefault);
            output.I64(cooldown);
            output.I64(buffer);
            output.I32(maxDebt);
            output.U16((ushort)entries.Count);
            foreach (Entry e in entries)
            {
                output.U8((byte)e.Character);
                output.U8((byte)e.Arts.Count);     // 겹침이 없으므로 ArtId 의 값 수 이하다
                foreach (ArtId a in e.Arts)
                {
                    output.U8((byte)a);
                }
            }
        }

        private sealed class Entry
        {
            public CharacterId Character;
            public List<ArtId> Arts = new List<ArtId>();
            public string Pointer = "";
            public int Order;
        }

        private static Entry? Read(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            var e = new Entry { Pointer = at };
            bool ok = true;
            string c = row.GetProperty("character").GetString() ?? "";
            if (!ContentIds.TryParse(c, out e.Character) || e.Character == CharacterId.Baseline)
            {
                diagnostics.Error(file.RepoPath, at + "/character", "'" + c + "' 는 캐릭터 id(char.1~char.5, spec 14 3.2)가 아니다");
                ok = false;
            }
            int k = 0;
            foreach (JsonElement art in row.GetProperty("arts").EnumerateArray())
            {
                string id = art.GetString() ?? "";
                string artAt = at + "/arts/" + k.ToString(CultureInfo.InvariantCulture);
                if (!ContentIds.TryParse(id, out ArtId a) || a == ArtId.None)
                {
                    diagnostics.Error(file.RepoPath, artAt, "'" + id + "' 는 비기 id(spec 14 3.4)가 아니다");
                    ok = false;
                }
                else if (e.Arts.Contains(a))
                {
                    diagnostics.Error(file.RepoPath, artAt, "비기 '" + id + "' 가 두 번 있다");
                    ok = false;
                }
                else
                {
                    e.Arts.Add(a);
                }
                k++;
            }
            e.Arts.Sort((x, y) => ((byte)x).CompareTo((byte)y));    // 겹침이 없으므로 순서는 하나다
            return ok ? e : null;
        }

        // 초 → 입력 시각 단위(1/15,360초, spec 03 1.1)
        private static long InputUnits(JsonElement o, string name)
        {
            decimal units = Units.RoundHalfEven(o.GetProperty(name).GetDecimal(), SimTime.InputUnitsPerSecond, 1);
            if (units > int.MaxValue)
            {
                throw new DataBuildException(name + " 이 입력 시각 단위로 32비트 범위를 넘는다");
            }
            return (long)units;
        }

        // 스키마의 "integer" 는 6.0 도 받으므로 decimal 로 읽어 정수인지 본다
        private static int Int(JsonElement v, string name)
        {
            decimal d = v.GetDecimal();
            if (d != decimal.Truncate(d) || d < int.MinValue || d > int.MaxValue)
            {
                throw new DataBuildException(name + " 값 " + d.ToString(CultureInfo.InvariantCulture) + " 은 32비트 정수가 아니다");
            }
            return (int)d;
        }
    }
}
