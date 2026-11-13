using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/weather_events.json → 팩 절 16 weather_events. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.WeatherEvents.cs.
    // 스키마: data/schema/weather_events.schema.json. world-characters 2.3(표 5행), spec 14 3.9, DECISIONS-TECH T-55.
    // 원본은 사건마다 한 줄이고 절은 WeatherEventId 오름차순이다. WeatherEventId(None 제외)마다 정확히 한 줄이 있어야 한다(spec 14 "날씨와 돌발 사건 5").
    // 단위: % → 만분율(× 100), 초 → 틱(× 60), 칸 → Raw(× 2^32). 시간대 id 는 TodMask 의 비트(1 << 코드)로 모은다.
    // 교차 검사:
    //   - when 이 wave_start 면 tods 가 있어야 하고, day_change 면 tods 와 max_per_day 를 두지 않는다.
    //   - span 이 timed 일 때만 duration_seconds 가 있고 0 틱이 아니다.
    //   - 규칙에 영향을 주는 사건(affects_rules)은 event.star_crystal 뿐이다. 그 줄은 span 이 instant 이고 수치 칸을 두지 않는다
    //     (확률·시간·결정 수는 starfall.json 한 곳에만 둔다). 그 밖의 줄에는 chance_percent 가 있어야 한다.
    public sealed class WeatherEventsConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.WeatherEvents;

        // 규칙에 영향을 주는 줄에 둘 수 없는 수치 칸
        private static readonly string[] NumberFields = { "chance_percent", "max_per_day", "duration_seconds", "edge_width", "variants" };

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Entry>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("events").EnumerateArray())
            {
                string at = "/events/" + i.ToString(CultureInfo.InvariantCulture);
                Entry? e = Read(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = i;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: WeatherEventId 오름차순. 같은 id 끼리는 원본 순서(진단이 늘 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.D.Id != b.D.Id ? ((byte)a.D.Id).CompareTo((byte)b.D.Id) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].D.Id == entries[k - 1].D.Id)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "사건 '" + ContentIds.ToId(entries[k].D.Id) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            foreach (WeatherEventId id in (WeatherEventId[])Enum.GetValues(typeof(WeatherEventId)))
            {
                if (id != WeatherEventId.None && !entries.Exists(e => e.D.Id == id))
                {
                    diagnostics.Error(file.RepoPath, "/events", "사건 '" + ContentIds.ToId(id) + "' 가 없다(spec 14 3.9: 사건마다 한 줄)");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);
            foreach (Entry e in entries)
            {
                WeatherEventDef d = e.D;
                output.U8((byte)d.Id);
                output.U8(d.AffectsRules ? (byte)1 : (byte)0);
                output.U8(d.OnDayChange ? (byte)1 : (byte)0);
                output.U8((byte)d.TodMask);
                output.I32(d.ChanceBp);
                output.I32(d.MaxPerDay);
                output.U8(d.IsInstant ? (byte)1 : (byte)0);
                output.I32(d.DurationTicks);
                output.I64(d.EdgeWidth.Raw);
                output.I32(d.VariantCount);
            }
        }

        private sealed class Entry
        {
            public WeatherEventDef D = new WeatherEventDef();
            public string Pointer = "";
            public int Order;
        }

        private static Entry? Read(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var e = new Entry { Pointer = at };
            WeatherEventDef d = e.D;

            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParse(id, out d.Id) || d.Id == WeatherEventId.None)
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 사건 id(spec 14 3.9 event.*)가 아니다");
            }

            d.AffectsRules = row.GetProperty("affects_rules").GetBoolean();
            string span = row.GetProperty("span").GetString() ?? "";
            d.IsInstant = span == "instant";
            bool timed = span == "timed";

            if (d.AffectsRules)
            {
                // 규칙에 영향을 주는 사건은 별똥 결정 하나이고 수치는 그 절(starfall)이 주인이다
                if (d.Id != WeatherEventId.None && d.Id != WeatherEventId.StarCrystal)
                {
                    diagnostics.Error(file.RepoPath, at + "/affects_rules", "규칙에 영향을 주는 사건은 event.star_crystal 뿐이다(world-characters 2.3). 그 밖의 사건은 연출이다");
                }
                if (!d.IsInstant)
                {
                    diagnostics.Error(file.RepoPath, at + "/span", "규칙에 영향을 주는 사건의 줄은 span 이 instant 다(수명은 starfall.json 이 정한다)");
                }
                foreach (string name in NumberFields)
                {
                    if (row.TryGetProperty(name, out _))
                    {
                        diagnostics.Error(file.RepoPath, at + "/" + name, "규칙에 영향을 주는 사건의 수치는 starfall.json 한 곳에만 둔다. 이 줄에 두지 않는다");
                    }
                }
            }
            else if (d.Id == WeatherEventId.StarCrystal)
            {
                diagnostics.Error(file.RepoPath, at + "/affects_rules", "event.star_crystal 은 규칙에 영향을 준다(마력 결정 +1, world-characters 2.3)");
            }

            d.OnDayChange = row.GetProperty("when").GetString() == "day_change";
            bool hasTods = row.TryGetProperty("tods", out JsonElement tods);
            if (d.OnDayChange)
            {
                if (hasTods)
                {
                    diagnostics.Error(file.RepoPath, at + "/tods", "날이 바뀔 때 일어나는 사건에는 시간대를 두지 않는다");
                }
                if (row.TryGetProperty("max_per_day", out _))
                {
                    diagnostics.Error(file.RepoPath, at + "/max_per_day", "날이 바뀔 때 일어나는 사건은 하루에 한 번뿐이다. 상한을 두지 않는다");
                }
            }
            else if (!hasTods)
            {
                diagnostics.Error(file.RepoPath, at + "/tods", "웨이브가 시작될 때 뽑는 사건에는 시간대가 하나 이상 있어야 한다");
            }
            else
            {
                int k = 0;
                foreach (JsonElement t in tods.EnumerateArray())
                {
                    string where = at + "/tods/" + k.ToString(CultureInfo.InvariantCulture);
                    string name = t.GetString() ?? "";
                    if (!ContentIds.TryParse(name, out TimeOfDay tod) || tod == TimeOfDay.None)
                    {
                        diagnostics.Error(file.RepoPath, where, "'" + name + "' 는 시간대 id(spec 14 3.9)가 아니다");
                    }
                    else if ((d.TodMask & (1 << (int)tod)) != 0)
                    {
                        diagnostics.Error(file.RepoPath, where, "시간대 '" + name + "' 가 두 번 있다");
                    }
                    else
                    {
                        d.TodMask |= 1 << (int)tod;
                    }
                    k++;
                }
            }

            if (row.TryGetProperty("chance_percent", out JsonElement chance))
            {
                d.ChanceBp = Units.PercentToBp(chance.GetDecimal());
            }
            else if (!d.AffectsRules)
            {
                diagnostics.Error(file.RepoPath, at + "/chance_percent", "규칙에 영향이 없는 사건에는 일어날 확률이 있어야 한다");
            }
            if (row.TryGetProperty("max_per_day", out _))
            {
                d.MaxPerDay = WavesConverter.Int(row, "max_per_day");
            }

            bool hasDuration = row.TryGetProperty("duration_seconds", out JsonElement duration);
            if (timed != hasDuration)
            {
                diagnostics.Error(file.RepoPath, at + (hasDuration ? "/duration_seconds" : "/span"), "duration_seconds 는 span 이 timed 일 때만, 그때는 반드시 둔다");
            }
            else if (timed)
            {
                d.DurationTicks = Units.SecondsToTicks(duration.GetDecimal());
                if (d.DurationTicks <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/duration_seconds", "길이가 0 틱이 된다");
                }
            }

            if (row.TryGetProperty("edge_width", out JsonElement edge))
            {
                d.EdgeWidth = Fix64.FromRaw(Units.CellsToRaw(edge.GetDecimal()));
            }
            d.VariantCount = row.TryGetProperty("variants", out _) ? WavesConverter.Int(row, "variants") : 1;

            return diagnostics.Count == before ? e : null;
        }
    }
}
