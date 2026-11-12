using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/arena.json → 팩 절 12 arena. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Arena.cs.
    // 스키마: data/schema/arena.schema.json. 원본은 전장마다 한 줄(모드 여럿)이고 절은 모드마다 한 항목(SimMode 오름차순)이다.
    //   modes 의 이름 → SimMode(endless 0, duel 1, plaza 2, practice 3). 모드는 데이터 전체에서 한 번만 나온다.
    // 단위: 칸 → Raw(× 2^32, 가장 가까운 정수, 같으면 짝수). 교차 검사: 기둥(1×1칸)은 전장 안, 시작 위치는 전장 안쪽이고 기둥 밖.
    public sealed class ArenaConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Arena;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Entry>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("arenas").EnumerateArray())
            {
                string at = "/arenas/" + i.ToString(CultureInfo.InvariantCulture);
                Read(row, at, file, diagnostics, entries);
                i++;
            }
            // 정규 순서: SimMode 오름차순. 겹침은 오류로 막으므로 쓰는 바이트의 순서는 하나다.
            // 같은 모드끼리는 원본 순서로 둔다(진단이 늘 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.Mode != b.Mode ? ((byte)a.Mode).CompareTo((byte)b.Mode) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Mode == entries[k - 1].Mode)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer, "모드 '" + entries[k].ModeName + "' 의 전장이 둘이다(모드마다 전장은 하나)");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);       // 모드가 겹치지 않으므로 SimMode 의 값 수 이하다
            foreach (Entry e in entries)
            {
                output.U8((byte)e.Mode);
                output.I64(e.WidthRaw);
                output.I64(e.HeightRaw);
                WritePoints(e.Pillars, output);
                WritePoints(e.Spawns, output);
            }
        }

        private sealed class Entry
        {
            public SimMode Mode;
            public string ModeName = "";
            public string Pointer = "";
            public long WidthRaw, HeightRaw;
            public long[] Pillars = new long[0];      // x0, y0, x1, y1, …
            public long[] Spawns = new long[0];
            public int Order;                         // 원본에서의 순서
        }

        private static void Read(JsonElement row, string at, DataFile file, Diagnostics diagnostics, List<Entry> entries)
        {
            long w = Units.CellsToRaw(row.GetProperty("width").GetDecimal());
            long h = Units.CellsToRaw(row.GetProperty("height").GetDecimal());
            long[] pillars = Points(row.GetProperty("pillars"));
            long[] spawns = Points(row.GetProperty("spawn_points"));
            bool ok = true;
            for (int p = 0; p < pillars.Length; p += 2)
            {
                // 기둥은 1×1칸이다(world-characters 2.6): [x, x + 1칸] × [y, y + 1칸] 이 전장 [0, w] × [0, h] 안
                if (pillars[p] > w - Units.RawPerCell || pillars[p + 1] > h - Units.RawPerCell)
                {
                    diagnostics.Error(file.RepoPath, at + "/pillars/" + (p / 2).ToString(CultureInfo.InvariantCulture), "기둥(1×1칸)이 전장 밖으로 나간다");
                    ok = false;
                }
            }
            for (int s = 0; s < spawns.Length; s += 2)
            {
                string sp = at + "/spawn_points/" + (s / 2).ToString(CultureInfo.InvariantCulture);
                long x = spawns[s];
                long y = spawns[s + 1];
                if (x <= 0 || x >= w || y <= 0 || y >= h)
                {
                    diagnostics.Error(file.RepoPath, sp, "시작 위치가 전장 안쪽(벽 위·밖이 아닌 곳)에 있지 않다");
                    ok = false;
                    continue;
                }
                for (int p = 0; p < pillars.Length; p += 2)
                {
                    if (x >= pillars[p] && x <= pillars[p] + Units.RawPerCell && y >= pillars[p + 1] && y <= pillars[p + 1] + Units.RawPerCell)
                    {
                        diagnostics.Error(file.RepoPath, sp, "시작 위치가 기둥 " + (p / 2).ToString(CultureInfo.InvariantCulture) + " 위에 있다");
                        ok = false;
                    }
                }
            }
            if (!ok)
            {
                return;
            }
            int m = 0;
            foreach (JsonElement mode in row.GetProperty("modes").EnumerateArray())
            {
                string name = mode.GetString() ?? "";
                entries.Add(new Entry
                {
                    Mode = ModeOf(name),
                    ModeName = name,
                    Pointer = at + "/modes/" + m.ToString(CultureInfo.InvariantCulture),
                    WidthRaw = w,
                    HeightRaw = h,
                    Pillars = pillars,
                    Spawns = spawns,
                    Order = entries.Count,
                });
                m++;
            }
        }

        // 스키마의 enum 이 이미 거른 값이다
        private static SimMode ModeOf(string s)
        {
            switch (s)
            {
                case "endless": return SimMode.Endless;
                case "duel": return SimMode.Duel;
                case "plaza": return SimMode.Plaza;
                case "practice": return SimMode.Practice;
                default: throw new DataBuildException("모르는 모드 '" + s + "'");
            }
        }

        private static long[] Points(JsonElement array)
        {
            var list = new List<long>();
            foreach (JsonElement p in array.EnumerateArray())
            {
                list.Add(Units.CellsToRaw(p.GetProperty("x").GetDecimal()));
                list.Add(Units.CellsToRaw(p.GetProperty("y").GetDecimal()));
            }
            if (list.Count / 2 > ushort.MaxValue)
            {
                throw new DataBuildException("점이 너무 많다(절의 개수 칸은 u16)");
            }
            return list.ToArray();
        }

        private static void WritePoints(long[] xy, ByteWriter w)
        {
            w.U16((ushort)(xy.Length / 2));
            for (int i = 0; i < xy.Length; i++)
            {
                w.I64(xy[i]);
            }
        }
    }
}
