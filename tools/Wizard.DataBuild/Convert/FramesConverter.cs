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
    // data/sim/frames.json → 팩 절 8 frames. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Frames.cs.
    // 스키마: data/schema/frames.schema.json. 원본은 틀마다 한 줄이고 절은 FrameId 오름차순이다. FrameId 마다 정확히 한 줄이 있어야 한다(spec 07 2.4 "5행").
    // 틀 하나의 꼴(FramePlan)은 waves.json 의 고정 각본(spec 07 7.2)도 쓴다: WavesConverter 가 ReadPlan·WritePlan 을 부른다.
    //   종 id → EnemyTypeId(ContentIds), 틀 id → FrameId. 종 배열은 EnemyTypeId 오름차순으로 정렬해 쓴다(원본 순서에 기대지 않는다).
    //   주 묶음이 비었으면 몫은 0, 주 묶음이 있으면 몫은 1 이상. 고정 개수는 min ≤ max. 종은 배열마다 한 번만.
    public sealed class FramesConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Frames;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var plans = new List<Plan>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("frames").EnumerateArray())
            {
                string at = "/frames/" + i.ToString(CultureInfo.InvariantCulture);
                Plan? p = ReadPlan(row, at, "id", true, file, diagnostics);
                if (p != null)
                {
                    p.Order = i;
                    plans.Add(p);
                }
                i++;
            }
            // 정규 순서: FrameId 오름차순. 같은 id 끼리는 원본 순서(진단이 늘 뒤에 나온 줄을 가리키게)
            plans.Sort((a, b) => a.Frame != b.Frame ? ((byte)a.Frame).CompareTo((byte)b.Frame) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < plans.Count; k++)
            {
                if (plans[k].Frame == plans[k - 1].Frame)
                {
                    diagnostics.Error(file.RepoPath, plans[k].Pointer + "/id", "틀 '" + ContentIds.ToId(plans[k].Frame) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            foreach (FrameId f in (FrameId[])Enum.GetValues(typeof(FrameId)))
            {
                if (!plans.Exists(p => p.Frame == f))
                {
                    diagnostics.Error(file.RepoPath, "/frames", "틀 '" + ContentIds.ToId(f) + "' 가 없다(spec 07 2.4: 틀마다 한 줄)");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)plans.Count);          // id 가 겹치지 않으므로 FrameId 의 값 수 이하다
            foreach (Plan p in plans)
            {
                WritePlan(p, output);
            }
        }

        // 틀 하나(또는 각본 웨이브 하나)의 구성. 절 바이트에는 아래 순서로 쓴다(ContentDb.Frames.cs 의 FramePlan 과 같은 꼴)
        internal sealed class Plan
        {
            public FrameId Frame;
            public EnemyTypeId[] Group = Array.Empty<EnemyTypeId>();
            public int GroupSharePct;
            public EnemyTypeId[] FixedSpecies = Array.Empty<EnemyTypeId>();
            public int[] FixedMin = Array.Empty<int>();
            public int[] FixedMax = Array.Empty<int>();
            public bool RestMixed;
            public string Pointer = "";
            public int Order;                         // 원본에서의 순서
        }

        // idProperty: 틀이면 "id"(정의), 각본이면 "frame"(참조). restMixed: 틀은 늘 혼합(true), 각본은 "rest" 를 읽는다(null 을 넘긴다)
        internal static Plan? ReadPlan(JsonElement o, string at, string idProperty, bool? restMixed, DataFile file, Diagnostics diagnostics)
        {
            var p = new Plan { Pointer = at };
            bool ok = true;
            string id = o.GetProperty(idProperty).GetString() ?? "";
            if (!ContentIds.TryParse(id, out p.Frame))
            {
                diagnostics.Error(file.RepoPath, at + "/" + idProperty, "'" + id + "' 는 틀 id(spec 14 3.9)가 아니다");
                ok = false;
            }

            var group = new List<(EnemyTypeId Species, int Order)>();
            int g = 0;
            foreach (JsonElement s in o.GetProperty("group").EnumerateArray())
            {
                string sp = at + "/group/" + g.ToString(CultureInfo.InvariantCulture);
                if (TryEnemy(s, sp, file, diagnostics, out EnemyTypeId v))
                {
                    group.Add((v, g));
                }
                else
                {
                    ok = false;
                }
                g++;
            }
            group.Sort((a, b) => a.Species != b.Species ? ((ushort)a.Species).CompareTo((ushort)b.Species) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < group.Count; k++)
            {
                if (group[k].Species == group[k - 1].Species)
                {
                    diagnostics.Error(file.RepoPath, at + "/group/" + group[k].Order.ToString(CultureInfo.InvariantCulture), "주 묶음에 종 '" + ContentIds.ToId(group[k].Species) + "' 가 두 번 있다");
                    ok = false;
                }
            }
            p.GroupSharePct = WavesConverter.Int(o, "group_share");
            if (g == 0 && p.GroupSharePct != 0)
            {
                diagnostics.Error(file.RepoPath, at + "/group_share", "주 묶음이 없는데 몫이 0 이 아니다");
                ok = false;
            }
            if (g != 0 && p.GroupSharePct == 0)
            {
                diagnostics.Error(file.RepoPath, at + "/group_share", "주 묶음이 있는데 몫이 0 이다");
                ok = false;
            }

            var fixedRows = new List<(EnemyTypeId Species, int Min, int Max, int Order)>();
            int f = 0;
            foreach (JsonElement row in o.GetProperty("fixed").EnumerateArray())
            {
                string fp = at + "/fixed/" + f.ToString(CultureInfo.InvariantCulture);
                int min = WavesConverter.Int(row, "min");
                int max = WavesConverter.Int(row, "max");
                if (min > max)
                {
                    diagnostics.Error(file.RepoPath, fp + "/min", "고정 개수의 min 이 max 보다 크다");
                    ok = false;
                }
                if (TryEnemy(row.GetProperty("species"), fp + "/species", file, diagnostics, out EnemyTypeId v))
                {
                    fixedRows.Add((v, min, max, f));
                }
                else
                {
                    ok = false;
                }
                f++;
            }
            fixedRows.Sort((a, b) => a.Species != b.Species ? ((ushort)a.Species).CompareTo((ushort)b.Species) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < fixedRows.Count; k++)
            {
                if (fixedRows[k].Species == fixedRows[k - 1].Species)
                {
                    diagnostics.Error(file.RepoPath, at + "/fixed/" + fixedRows[k].Order.ToString(CultureInfo.InvariantCulture) + "/species", "고정 개수에 종 '" + ContentIds.ToId(fixedRows[k].Species) + "' 가 두 번 있다");
                    ok = false;
                }
            }

            if (restMixed.HasValue)
            {
                p.RestMixed = restMixed.Value;
            }
            else
            {
                // 스키마의 enum 이 이미 거른 값이다
                p.RestMixed = string.Equals(o.GetProperty("rest").GetString(), "mixed", StringComparison.Ordinal);
            }
            if (!ok)
            {
                return null;
            }
            if (group.Count > ushort.MaxValue || fixedRows.Count > ushort.MaxValue)
            {
                throw new DataBuildException("종이 너무 많다(절의 개수 칸은 u16)");
            }
            p.Group = group.ConvertAll(x => x.Species).ToArray();
            p.FixedSpecies = fixedRows.ConvertAll(x => x.Species).ToArray();
            p.FixedMin = fixedRows.ConvertAll(x => x.Min).ToArray();
            p.FixedMax = fixedRows.ConvertAll(x => x.Max).ToArray();
            return p;
        }

        internal static void WritePlan(Plan p, ByteWriter w)
        {
            w.U8((byte)p.Frame);
            w.U16((ushort)p.Group.Length);
            foreach (EnemyTypeId s in p.Group)
            {
                w.U16((ushort)s);
            }
            w.I32(p.GroupSharePct);
            w.U16((ushort)p.FixedSpecies.Length);
            for (int i = 0; i < p.FixedSpecies.Length; i++)
            {
                w.U16((ushort)p.FixedSpecies[i]);
                w.I32(p.FixedMin[i]);
                w.I32(p.FixedMax[i]);
            }
            w.U8(p.RestMixed ? (byte)1 : (byte)0);
        }

        // 일반 적 종 id → EnemyTypeId. 모르는 id 는 진단
        internal static bool TryEnemy(JsonElement e, string at, DataFile file, Diagnostics diagnostics, out EnemyTypeId v)
        {
            string id = e.GetString() ?? "";
            if (!ContentIds.TryParse(id, out v) || v == EnemyTypeId.None)
            {
                diagnostics.Error(file.RepoPath, at, "'" + id + "' 는 적 종 id(spec 14 3.6)가 아니다");
                return false;
            }
            return true;
        }
    }
}
