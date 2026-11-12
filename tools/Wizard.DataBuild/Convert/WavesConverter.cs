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
    // data/sim/waves.json → 팩 절 7 waves(WaveCurveDef 하나). 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Waves.cs.
    // 스키마: data/schema/waves.schema.json. spec 07 2.1, 3.1, 5절, 6절, 7.2.
    // 단위: 초 → 틱(× 60). 체력 배율(0.12, 2.32) → × 1,000,000, 배율(1.06, 0.5) → 백분율 정수(× 100).
    //   배율의 정수는 spec 07 0.2 의 정수 표현이 그대로 식에 들어가므로 정확해야 한다(반올림하면 다른 곡선이 된다): 자리가 남으면 진단.
    // 교차 검사:
    //   tod_ranges 는 하루 속 순번 1..day_length 를 빈틈·겹침 없이 덮고, tod.boss 는 정확히 순번 day_length 하나다(spec 07 3.1 IsBossWave).
    //   구간 표(telegraph_cap, elite_count, modifier_count)는 from 오름차순으로 빈틈·겹침 없이 잇고, to = null(끝 없음)은 마지막 줄만.
    //   species_cap 의 종은 한 번만. script 의 웨이브는 한 번만이고 보스 웨이브(w % day_length == 0)가 아니다. 각본 하나의 꼴은 FramesConverter.ReadPlan.
    public sealed class WavesConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Waves;

        private const long MicroPerUnit = 1_000_000;        // H(w) 의 정수 표현(spec 07 0.2 H_micro)
        private const long PctPerUnit = 100;                // 백분율 정수(spec 07 0.2 "배율")

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            JsonElement root = file.Root;
            int dayLength = Int(root, "day_length");
            TodRange[] tods = ReadTodRanges(root.GetProperty("tod_ranges"), dayLength, file, diagnostics);

            int hpLinearUntil = Int(root, "hp_linear_until");
            long hpLinearStepMicro = Exact(root, "hp_linear_step", "", MicroPerUnit, file, diagnostics);
            long hpExpBaseMicro = Exact(root, "hp_exp_base", "", MicroPerUnit, file, diagnostics);
            int hpExpRatePct = (int)Exact(root, "hp_exp_rate", "", PctPerUnit, file, diagnostics);
            int eliteHpMul = Int(root, "elite_hp_mul");
            int bossHpRefWave = Int(root, "boss_hp_ref_wave");

            JsonElement linear = root.GetProperty("budget_linear");
            JsonElement late = root.GetProperty("budget_late");
            JsonElement bundle = root.GetProperty("bundle_rule");
            int bundleIntervalTicks = Units.SecondsToTicks(root.GetProperty("bundle_interval_seconds").GetDecimal());
            int earlyRelease = Int(root, "bundle_early_release_alive");
            int maxAlive = Int(root, "max_alive");

            Step[] telegraph = ReadSteps(root.GetProperty("telegraph_cap"), "/telegraph_cap", file, diagnostics);
            Step[] elites = ReadSteps(root.GetProperty("elite_count"), "/elite_count", file, diagnostics);
            int eliteCostMul = Int(root, "elite_cost_mul");
            List<(EnemyTypeId Species, int Cap)> caps = ReadSpeciesCaps(root.GetProperty("species_cap"), file, diagnostics);
            int stallTicks = Units.SecondsToTicks(root.GetProperty("stall_seconds").GetDecimal());
            int stallKillScorePct = (int)Exact(root, "stall_kill_score", "", PctPerUnit, file, diagnostics);
            Step[] modifiers = ReadSteps(root.GetProperty("modifier_count"), "/modifier_count", file, diagnostics);
            JsonElement damage = root.GetProperty("damage");
            FramesConverter.TryEnemy(root.GetProperty("fill_species"), "/fill_species", file, diagnostics, out EnemyTypeId fill);
            List<(int Wave, FramesConverter.Plan Plan)> script = ReadScript(root.GetProperty("script"), dayLength, file, diagnostics);

            if (diagnostics.Count != before)
            {
                return;
            }
            output.I32(dayLength);
            output.U16((ushort)tods.Length);
            foreach (TodRange t in tods)
            {
                output.I32(t.From);
                output.I32(t.To);
                output.U8((byte)t.Tod);
            }
            output.I32(hpLinearUntil);
            output.I64(hpLinearStepMicro);
            output.I64(hpExpBaseMicro);
            output.I32(hpExpRatePct);
            output.I32(eliteHpMul);
            output.I32(bossHpRefWave);
            output.I32(Int(linear, "base"));
            output.I32(Int(linear, "per_wave"));
            output.I32(Int(linear, "until"));
            output.I32(Int(late, "base"));
            output.I32(Int(late, "per_wave"));
            output.I32(Int(late, "cap"));
            output.I32(Int(bundle, "base"));
            output.I32(Int(bundle, "grow_after"));
            output.I32(Int(bundle, "grow_every"));
            output.I32(Int(bundle, "max"));
            output.I32(bundleIntervalTicks);
            output.I32(earlyRelease);
            output.I32(maxAlive);
            WriteSteps(telegraph, output);
            WriteSteps(elites, output);
            output.I32(eliteCostMul);
            output.U16((ushort)caps.Count);
            foreach (var c in caps)
            {
                output.U16((ushort)c.Species);
                output.I32(c.Cap);
            }
            output.I32(stallTicks);
            output.I32(stallKillScorePct);
            WriteSteps(modifiers, output);
            output.I32(Int(damage, "contact"));
            output.I32(Int(damage, "telegraph_plus"));
            output.I32(Int(damage, "elite_boss_plus_from"));
            output.I32(Int(damage, "all_plus_from"));
            output.I32(Int(damage, "attack_cap"));
            output.U16((ushort)fill);
            output.U16((ushort)script.Count);
            foreach (var s in script)
            {
                output.I32(s.Wave);
                FramesConverter.WritePlan(s.Plan, output);
            }
        }

        private struct TodRange
        {
            public int From, To;
            public TimeOfDay Tod;
            public int Order;
        }

        private struct Step
        {
            public int From, To, Value;               // To = WaveCurveDef.Unbounded 이면 끝 없음
            public int Order;
        }

        private static TodRange[] ReadTodRanges(JsonElement array, int dayLength, DataFile file, Diagnostics diagnostics)
        {
            var list = new List<TodRange>();
            int i = 0;
            bool ok = true;
            foreach (JsonElement row in array.EnumerateArray())
            {
                string at = "/tod_ranges/" + i.ToString(CultureInfo.InvariantCulture);
                var t = new TodRange { From = Int(row, "from"), To = Int(row, "to"), Order = i };
                string id = row.GetProperty("tod").GetString() ?? "";
                if (!ContentIds.TryParse(id, out t.Tod))
                {
                    diagnostics.Error(file.RepoPath, at + "/tod", "'" + id + "' 는 시간대 id(spec 14 3.9)가 아니다");
                    ok = false;
                }
                if (t.From > t.To)
                {
                    diagnostics.Error(file.RepoPath, at + "/from", "구간의 from 이 to 보다 크다");
                    ok = false;
                }
                list.Add(t);
                i++;
            }
            if (!ok)
            {
                return list.ToArray();
            }
            list.Sort((a, b) => a.From != b.From ? a.From.CompareTo(b.From) : a.Order.CompareTo(b.Order));
            int expect = 1;
            foreach (TodRange t in list)
            {
                string at = "/tod_ranges/" + t.Order.ToString(CultureInfo.InvariantCulture);
                if (t.From != expect)
                {
                    diagnostics.Error(file.RepoPath, at + "/from", "하루 속 순번 " + expect.ToString(CultureInfo.InvariantCulture) + " 부터 이어지지 않는다(빈틈이나 겹침)");
                    return list.ToArray();
                }
                bool boss = t.Tod == TimeOfDay.BossWave;
                bool last = t.From == dayLength && t.To == dayLength;
                if (boss != last)
                {
                    diagnostics.Error(file.RepoPath, at + "/tod", "tod.boss 는 정확히 하루의 마지막 순번(" + dayLength.ToString(CultureInfo.InvariantCulture) + ") 하나여야 한다");
                }
                expect = t.To + 1;
            }
            if (expect != dayLength + 1)
            {
                diagnostics.Error(file.RepoPath, "/tod_ranges", "하루 속 순번 1.." + dayLength.ToString(CultureInfo.InvariantCulture) + " 을 정확히 덮지 않는다");
            }
            return list.ToArray();
        }

        private static Step[] ReadSteps(JsonElement array, string at, DataFile file, Diagnostics diagnostics)
        {
            var list = new List<Step>();
            int i = 0;
            foreach (JsonElement row in array.EnumerateArray())
            {
                JsonElement to = row.GetProperty("to");
                var s = new Step
                {
                    From = Int(row, "from"),
                    To = to.ValueKind == JsonValueKind.Null ? WaveCurveDef.Unbounded : Int(row, "to"),
                    Value = Int(row, "value"),
                    Order = i,
                };
                if (s.From > s.To)
                {
                    diagnostics.Error(file.RepoPath, at + "/" + i.ToString(CultureInfo.InvariantCulture) + "/from", "구간의 from 이 to 보다 크다");
                    return list.ToArray();
                }
                list.Add(s);
                i++;
            }
            list.Sort((a, b) => a.From != b.From ? a.From.CompareTo(b.From) : a.Order.CompareTo(b.Order));
            for (int k = 0; k < list.Count; k++)
            {
                string row = at + "/" + list[k].Order.ToString(CultureInfo.InvariantCulture);
                bool lastRow = k == list.Count - 1;
                if (!lastRow && list[k].To == WaveCurveDef.Unbounded)
                {
                    diagnostics.Error(file.RepoPath, row + "/to", "끝 없음(null)은 마지막 줄만 쓸 수 있다");
                    return list.ToArray();
                }
                if (!lastRow && list[k].To + 1 != list[k + 1].From)
                {
                    diagnostics.Error(file.RepoPath, at + "/" + list[k + 1].Order.ToString(CultureInfo.InvariantCulture) + "/from", "앞 줄의 to 바로 다음 웨이브에서 시작하지 않는다(빈틈이나 겹침)");
                    return list.ToArray();
                }
                if (lastRow && list[k].To != WaveCurveDef.Unbounded)
                {
                    diagnostics.Error(file.RepoPath, row + "/to", "마지막 줄은 끝 없음(null)이어야 한다(뒤의 웨이브 값이 비지 않게)");
                }
            }
            return list.ToArray();
        }

        private static void WriteSteps(Step[] steps, ByteWriter w)
        {
            w.U16((ushort)steps.Length);
            foreach (Step s in steps)
            {
                w.I32(s.From);
                w.I32(s.To);
                w.I32(s.Value);
            }
        }

        private static List<(EnemyTypeId Species, int Cap)> ReadSpeciesCaps(JsonElement array, DataFile file, Diagnostics diagnostics)
        {
            var list = new List<(EnemyTypeId Species, int Cap, int Order)>();
            int i = 0;
            foreach (JsonElement row in array.EnumerateArray())
            {
                string at = "/species_cap/" + i.ToString(CultureInfo.InvariantCulture);
                if (FramesConverter.TryEnemy(row.GetProperty("species"), at + "/species", file, diagnostics, out EnemyTypeId v))
                {
                    list.Add((v, Int(row, "cap"), i));
                }
                i++;
            }
            list.Sort((a, b) => a.Species != b.Species ? ((ushort)a.Species).CompareTo((ushort)b.Species) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < list.Count; k++)
            {
                if (list[k].Species == list[k - 1].Species)
                {
                    diagnostics.Error(file.RepoPath, "/species_cap/" + list[k].Order.ToString(CultureInfo.InvariantCulture) + "/species", "종 '" + ContentIds.ToId(list[k].Species) + "' 의 상한이 두 번 있다");
                }
            }
            return list.ConvertAll(x => (x.Species, x.Cap));
        }

        private static List<(int Wave, FramesConverter.Plan Plan)> ReadScript(JsonElement array, int dayLength, DataFile file, Diagnostics diagnostics)
        {
            var list = new List<(int Wave, FramesConverter.Plan Plan)>();
            int i = 0;
            foreach (JsonElement row in array.EnumerateArray())
            {
                string at = "/script/" + i.ToString(CultureInfo.InvariantCulture);
                int wave = Int(row, "wave");
                if (dayLength > 0 && wave % dayLength == 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/wave", "웨이브 " + wave.ToString(CultureInfo.InvariantCulture) + " 은 보스 웨이브다(보스 웨이브에는 각본이 없다, spec 07 7.5)");
                }
                FramesConverter.Plan? p = FramesConverter.ReadPlan(row, at, "frame", null, file, diagnostics);
                if (p != null)
                {
                    p.Order = i;
                    list.Add((wave, p));
                }
                i++;
            }
            list.Sort((a, b) => a.Wave != b.Wave ? a.Wave.CompareTo(b.Wave) : a.Plan.Order.CompareTo(b.Plan.Order));
            for (int k = 1; k < list.Count; k++)
            {
                if (list[k].Wave == list[k - 1].Wave)
                {
                    diagnostics.Error(file.RepoPath, list[k].Plan.Pointer + "/wave", "웨이브 " + list[k].Wave.ToString(CultureInfo.InvariantCulture) + " 의 각본이 두 번 있다");
                }
            }
            if (list.Count > ushort.MaxValue)
            {
                throw new DataBuildException("각본이 너무 많다(절의 개수 칸은 u16)");
            }
            return list;
        }

        // value × multiplier 가 정수여야 한다(spec 07 0.2 의 정수 표현을 반올림 없이 얻는다). 아니면 진단하고 0
        internal static long Exact(JsonElement o, string name, string at, long multiplier, DataFile file, Diagnostics diagnostics)
        {
            decimal v = o.GetProperty(name).GetDecimal();
            decimal r = Units.RoundHalfEven(v, multiplier, 1);
            if (r != v * multiplier)
            {
                diagnostics.Error(file.RepoPath, at + "/" + name, name + " 값 " + v.ToString(CultureInfo.InvariantCulture) + " × " + multiplier.ToString(CultureInfo.InvariantCulture) + " 이 정수가 아니다(정수 표현이 반올림 없이 나와야 한다)");
                return 0;
            }
            if (r < int.MinValue || r > int.MaxValue)
            {
                diagnostics.Error(file.RepoPath, at + "/" + name, name + " 값 " + v.ToString(CultureInfo.InvariantCulture) + " 의 정수 표현이 32비트 범위를 넘는다");
                return 0;
            }
            return (long)r;
        }

        // 스키마의 "integer" 는 6.0 도 받으므로 decimal 로 읽어 정수인지 본다
        internal static int Int(JsonElement o, string name)
        {
            decimal v = o.GetProperty(name).GetDecimal();
            if (v != decimal.Truncate(v) || v < int.MinValue || v > int.MaxValue)
            {
                throw new DataBuildException(name + " 값 " + v.ToString(CultureInfo.InvariantCulture) + " 은 32비트 정수가 아니다");
            }
            return (int)v;
        }
    }
}
