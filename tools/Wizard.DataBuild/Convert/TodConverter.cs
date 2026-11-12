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
    // data/sim/tod.json → 팩 절 10 tod. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Tod.cs.
    // 스키마: data/schema/tod.schema.json. spec 07 2.3, spec 14 3.9. 원본은 시간대마다 한 줄이고 절은 TimeOfDay 오름차순이다.
    // TimeOfDay(None 제외)마다 정확히 한 줄이 있어야 한다(spec 07 2.3 "4행").
    // 단위: 가중치 배율(×1.5) → 백분율 정수(× 100, 정확해야 한다), 림 세기 % → 천분율(× 10).
    // 교차 검사: 종은 줄마다 한 번만. 일반 적이 나오지 않는 시간대(tod.boss)는 가중치가 비어 있다.
    public sealed class TodConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Tod;

        private const long PctPerUnit = 100;                // 백분율 정수(spec 07 2.3 "가중치 배율은 백분율 정수")

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Entry>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("tods").EnumerateArray())
            {
                string at = "/tods/" + i.ToString(CultureInfo.InvariantCulture);
                Entry? e = Read(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = i;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: TimeOfDay 오름차순. 같은 id 끼리는 원본 순서(진단이 늘 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.Id != b.Id ? ((byte)a.Id).CompareTo((byte)b.Id) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Id == entries[k - 1].Id)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "시간대 '" + ContentIds.ToId(entries[k].Id) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            foreach (TimeOfDay t in (TimeOfDay[])Enum.GetValues(typeof(TimeOfDay)))
            {
                if (t != TimeOfDay.None && !entries.Exists(e => e.Id == t))
                {
                    diagnostics.Error(file.RepoPath, "/tods", "시간대 '" + ContentIds.ToId(t) + "' 가 없다(spec 07 2.3: 시간대마다 한 줄)");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);
            foreach (Entry e in entries)
            {
                output.U8((byte)e.Id);
                output.U8(e.NormalEnemies ? (byte)1 : (byte)0);
                output.U16((ushort)e.Weights.Count);
                foreach (var w in e.Weights)
                {
                    output.U16((ushort)w.Species);
                    output.I32(w.Pct);
                }
                output.I32(e.RimPermille);
            }
        }

        private sealed class Entry
        {
            public TimeOfDay Id;
            public bool NormalEnemies;
            public List<(EnemyTypeId Species, int Pct)> Weights = new List<(EnemyTypeId Species, int Pct)>();
            public int RimPermille;
            public string Pointer = "";
            public int Order;
        }

        private static Entry? Read(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            var e = new Entry { Pointer = at };
            bool ok = true;
            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParse(id, out e.Id) || e.Id == TimeOfDay.None)
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 시간대 id(spec 14 3.9)가 아니다");
                ok = false;
            }
            e.NormalEnemies = row.GetProperty("normal_enemies").GetBoolean();
            e.RimPermille = Units.PercentToPermille(row.GetProperty("rim_strength_percent").GetDecimal());

            var weights = new List<(EnemyTypeId Species, int Pct, int Order)>();
            int i = 0;
            foreach (JsonElement w in row.GetProperty("weights").EnumerateArray())
            {
                string wp = at + "/weights/" + i.ToString(CultureInfo.InvariantCulture);
                int before = diagnostics.Count;
                int pct = (int)WavesConverter.Exact(w, "weight", wp, PctPerUnit, file, diagnostics);
                bool known = FramesConverter.TryEnemy(w.GetProperty("species"), wp + "/species", file, diagnostics, out EnemyTypeId species);
                if (known && diagnostics.Count == before)
                {
                    weights.Add((species, pct, i));
                }
                else
                {
                    ok = false;
                }
                i++;
            }
            if (!e.NormalEnemies && i != 0)
            {
                diagnostics.Error(file.RepoPath, at + "/weights", "일반 적이 나오지 않는 시간대(normal_enemies = false)에 가중치가 있다");
                ok = false;
            }
            weights.Sort((a, b) => a.Species != b.Species ? ((ushort)a.Species).CompareTo((ushort)b.Species) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < weights.Count; k++)
            {
                if (weights[k].Species == weights[k - 1].Species)
                {
                    diagnostics.Error(file.RepoPath, at + "/weights/" + weights[k].Order.ToString(CultureInfo.InvariantCulture) + "/species", "종 '" + ContentIds.ToId(weights[k].Species) + "' 의 가중치가 두 번 있다");
                    ok = false;
                }
            }
            if (!ok)
            {
                return null;
            }
            e.Weights = weights.ConvertAll(x => (x.Species, x.Pct));
            return e;
        }
    }
}
