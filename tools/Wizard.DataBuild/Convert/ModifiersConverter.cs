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
    // data/sim/modifiers.json → 팩 절 9 modifiers. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Modifiers.cs.
    // 스키마: data/schema/modifiers.schema.json. spec 07 2.5(8행), 9절, spec 14 3.9. 원본은 변덕마다 한 줄이고 절은 ModifierId 오름차순이다.
    // ModifierId(None 제외)마다 정확히 한 줄이 있어야 한다(spec 07 2.5 "8행").
    // 단위: 배율(×1.15) → 백분율 정수(× 100, 정확해야 한다), 초 → 틱(× 60), 칸 → Raw(× 2^32).
    // 적지 않은 효과는 "효과 없음" 값(ModifierDef 의 주석: 배율 100, 그 밖 0)으로 쓴다. 한 줄에 효과가 하나 이상 있어야 한다.
    // 금지(spec 07 2.5 "예고 시간을 줄이거나 적을 빠르게 하는 변덕은 만들지 않는다", 2.5 데이터 검증 규칙 [임시], TV-109):
    //   - 아는 필드 밖의 필드는 전부 오류다. 이름이 예고·속도 계열이면 "금지 필드"로 따로 알린다(스키마가 열려 있어도 여기서 막는다).
    //   - 적 탄 속도 배율은 100 을 넘지 못한다. 예고가 있는 효과(마지막 숨의 원, 좁은 들의 가시밭)는 예고가 0 보다 커야 한다.
    // 교차 검사: 종은 배열마다 한 번만, only_tod 는 tod.boss 가 아니다(보스 웨이브에는 변덕이 없다, spec 07 9절).
    public sealed class ModifiersConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Modifiers;

        private const long PctPerUnit = 100;                // 백분율 정수(spec 07 2.5 "점수 배율(정수)", hp_pct, proj_*_pct)

        // 예고를 줄이거나 적을 빠르게 하는 필드로 보는 이름 조각(아는 필드 밖에서만 본다)
        private static readonly string[] ForbiddenFragments = { "telegraph", "warn", "windup", "prelude", "notice", "speed", "haste", "fast", "move", "accel" };

        private static readonly string[] TopFields =
        {
            "_note", "id", "score_multiplier", "only_tod", "bundle_interval_seconds", "enemy_hp_multiplier", "extra_budget", "extra_elites",
            "death_circle", "enemy_shots", "weights", "species_caps", "thorn_border",
        };
        private static readonly string[] ExtraBudgetFields = { "percent", "cost_max" };
        private static readonly string[] DeathCircleFields = { "radius", "telegraph_seconds", "damage", "stagger_seconds" };
        private static readonly string[] EnemyShotsFields = { "count_multiplier", "speed_multiplier" };
        private static readonly string[] WeightFields = { "species", "weight" };
        private static readonly string[] CapFields = { "species", "cap" };
        private static readonly string[] ThornBorderFields = { "width", "telegraph_seconds", "contact_interval_seconds", "damage" };

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Entry>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("modifiers").EnumerateArray())
            {
                string at = "/modifiers/" + i.ToString(CultureInfo.InvariantCulture);
                Entry? e = Read(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = i;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: ModifierId 오름차순. 같은 id 끼리는 원본 순서(진단이 늘 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.D.Id != b.D.Id ? ((byte)a.D.Id).CompareTo((byte)b.D.Id) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].D.Id == entries[k - 1].D.Id)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "변덕 '" + ContentIds.ToId(entries[k].D.Id) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            foreach (ModifierId m in (ModifierId[])Enum.GetValues(typeof(ModifierId)))
            {
                if (m != ModifierId.None && !entries.Exists(e => e.D.Id == m))
                {
                    diagnostics.Error(file.RepoPath, "/modifiers", "변덕 '" + ContentIds.ToId(m) + "' 가 없다(spec 07 2.5: 변덕마다 한 줄)");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);
            foreach (Entry e in entries)
            {
                Write(e.D, output);
            }
        }

        private sealed class Entry
        {
            public ModifierDef D = new ModifierDef();
            public string Pointer = "";
            public int Order;
        }

        private static void Write(ModifierDef d, ByteWriter w)
        {
            w.U8((byte)d.Id);
            w.I32(d.ScorePct);
            w.U8((byte)d.OnlyTod);
            w.I32(d.BundleIntervalTicks);
            w.I32(d.EnemyHpPct);
            w.I32(d.ExtraBudgetPct);
            w.I32(d.ExtraBudgetCostMax);
            w.I32(d.ExtraElites);
            w.I64(d.DeathCircleRadius.Raw);
            w.I32(d.DeathCircleTelegraphTicks);
            w.I32(d.DeathCircleDamage);
            w.I32(d.DeathCircleStaggerTicks);
            w.I32(d.EnemyShotCountPct);
            w.I32(d.EnemyShotSpeedPct);
            w.U16((ushort)d.WeightSpecies.Length);
            for (int i = 0; i < d.WeightSpecies.Length; i++)
            {
                w.U16((ushort)d.WeightSpecies[i]);
                w.I32(d.WeightPct[i]);
            }
            w.U16((ushort)d.CapSpecies.Length);
            for (int i = 0; i < d.CapSpecies.Length; i++)
            {
                w.U16((ushort)d.CapSpecies[i]);
                w.I32(d.Cap[i]);
            }
            w.I64(d.ThornBorderWidth.Raw);
            w.I32(d.ThornTelegraphTicks);
            w.I32(d.ThornContactIntervalTicks);
            w.I32(d.ThornDamage);
        }

        private static Entry? Read(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var e = new Entry { Pointer = at };
            ModifierDef d = e.D;

            // 금지 필드와 모르는 필드는 값을 읽기 전에 본다(스키마가 열려 있어도 막는다)
            CheckFields(row, at, TopFields, file, diagnostics);
            if (row.TryGetProperty("extra_budget", out JsonElement budget))
            {
                CheckFields(budget, at + "/extra_budget", ExtraBudgetFields, file, diagnostics);
            }
            if (row.TryGetProperty("death_circle", out JsonElement circle))
            {
                CheckFields(circle, at + "/death_circle", DeathCircleFields, file, diagnostics);
            }
            if (row.TryGetProperty("enemy_shots", out JsonElement shots))
            {
                CheckFields(shots, at + "/enemy_shots", EnemyShotsFields, file, diagnostics);
            }
            if (row.TryGetProperty("thorn_border", out JsonElement thorn))
            {
                CheckFields(thorn, at + "/thorn_border", ThornBorderFields, file, diagnostics);
            }
            if (row.TryGetProperty("weights", out JsonElement weights))
            {
                CheckItemFields(weights, at + "/weights", WeightFields, file, diagnostics);
            }
            if (row.TryGetProperty("species_caps", out JsonElement caps))
            {
                CheckItemFields(caps, at + "/species_caps", CapFields, file, diagnostics);
            }
            if (diagnostics.Count != before)
            {
                return null;
            }

            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParse(id, out d.Id) || d.Id == ModifierId.None)
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 변덕 id(spec 14 3.9 whim.*)가 아니다");
            }
            d.ScorePct = (int)WavesConverter.Exact(row, "score_multiplier", at, PctPerUnit, file, diagnostics);

            if (row.TryGetProperty("only_tod", out JsonElement tod))
            {
                string t = tod.GetString() ?? "";
                if (!ContentIds.TryParse(t, out d.OnlyTod) || d.OnlyTod == TimeOfDay.None)
                {
                    diagnostics.Error(file.RepoPath, at + "/only_tod", "'" + t + "' 는 시간대 id(spec 14 3.9)가 아니다");
                }
                else if (d.OnlyTod == TimeOfDay.BossWave)
                {
                    diagnostics.Error(file.RepoPath, at + "/only_tod", "보스 웨이브에는 변덕이 없다(spec 07 9절). only_tod 에 tod.boss 를 둘 수 없다");
                }
            }

            int effects = 0;
            if (row.TryGetProperty("bundle_interval_seconds", out JsonElement interval))
            {
                d.BundleIntervalTicks = Units.SecondsToTicks(interval.GetDecimal());
                if (d.BundleIntervalTicks <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/bundle_interval_seconds", "묶음 간격이 0 틱이 된다");
                }
                effects++;
            }
            if (row.TryGetProperty("enemy_hp_multiplier", out _))
            {
                d.EnemyHpPct = (int)WavesConverter.Exact(row, "enemy_hp_multiplier", at, PctPerUnit, file, diagnostics);
                effects++;
            }
            if (row.TryGetProperty("extra_budget", out _))
            {
                d.ExtraBudgetPct = WavesConverter.Int(budget, "percent");
                d.ExtraBudgetCostMax = WavesConverter.Int(budget, "cost_max");
                effects++;
            }
            if (row.TryGetProperty("extra_elites", out _))
            {
                d.ExtraElites = WavesConverter.Int(row, "extra_elites");
                effects++;
            }
            if (row.TryGetProperty("death_circle", out _))
            {
                d.DeathCircleRadius = Fix64.FromRaw(Units.CellsToRaw(circle.GetProperty("radius").GetDecimal()));
                d.DeathCircleTelegraphTicks = Units.SecondsToTicks(circle.GetProperty("telegraph_seconds").GetDecimal());
                d.DeathCircleDamage = WavesConverter.Int(circle, "damage");
                d.DeathCircleStaggerTicks = Units.SecondsToTicks(circle.GetProperty("stagger_seconds").GetDecimal());
                if (d.DeathCircleTelegraphTicks <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/death_circle/telegraph_seconds", "예고 없는 위험 구역은 둘 수 없다(예고가 0 틱이 된다)");
                }
                if (d.DeathCircleRadius.Raw <= 0 || d.DeathCircleStaggerTicks <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/death_circle", "원의 반경과 미루는 간격은 0 보다 커야 한다");
                }
                effects++;
            }
            if (row.TryGetProperty("enemy_shots", out _))
            {
                d.EnemyShotCountPct = (int)WavesConverter.Exact(shots, "count_multiplier", at + "/enemy_shots", PctPerUnit, file, diagnostics);
                d.EnemyShotSpeedPct = (int)WavesConverter.Exact(shots, "speed_multiplier", at + "/enemy_shots", PctPerUnit, file, diagnostics);
                if (d.EnemyShotSpeedPct > ModifierDef.NeutralPct)
                {
                    diagnostics.Error(file.RepoPath, at + "/enemy_shots/speed_multiplier", "금지: 적 탄을 빠르게 하는 변덕은 만들지 않는다(spec 07 2.5)");
                }
                if (d.EnemyShotSpeedPct <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/enemy_shots/speed_multiplier", "탄 속도 배율은 0 보다 커야 한다");
                }
                effects++;
            }
            if (row.TryGetProperty("weights", out _))
            {
                var list = SpeciesValues(weights, at + "/weights", "weight", true, file, diagnostics);
                d.WeightSpecies = list.ConvertAll(x => x.Species).ToArray();
                d.WeightPct = list.ConvertAll(x => x.Value).ToArray();
                effects++;
            }
            if (row.TryGetProperty("species_caps", out _))
            {
                var list = SpeciesValues(caps, at + "/species_caps", "cap", false, file, diagnostics);
                d.CapSpecies = list.ConvertAll(x => x.Species).ToArray();
                d.Cap = list.ConvertAll(x => x.Value).ToArray();
                effects++;
            }
            if (row.TryGetProperty("thorn_border", out _))
            {
                d.ThornBorderWidth = Fix64.FromRaw(Units.CellsToRaw(thorn.GetProperty("width").GetDecimal()));
                d.ThornTelegraphTicks = Units.SecondsToTicks(thorn.GetProperty("telegraph_seconds").GetDecimal());
                d.ThornContactIntervalTicks = Units.SecondsToTicks(thorn.GetProperty("contact_interval_seconds").GetDecimal());
                d.ThornDamage = WavesConverter.Int(thorn, "damage");
                if (d.ThornTelegraphTicks <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/thorn_border/telegraph_seconds", "예고 없는 위험 구역은 둘 수 없다(예고가 0 틱이 된다)");
                }
                if (d.ThornBorderWidth.Raw <= 0 || d.ThornContactIntervalTicks <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/thorn_border", "띠의 폭과 접촉 간격은 0 보다 커야 한다");
                }
                effects++;
            }
            if (effects == 0)
            {
                diagnostics.Error(file.RepoPath, at, "효과가 하나도 없는 변덕이다");
            }
            return diagnostics.Count == before ? e : null;
        }

        // 객체의 필드가 아는 것뿐인지 본다. 예고·속도 계열 이름이면 금지 필드로 알린다(spec 07 2.5, TV-109)
        private static void CheckFields(JsonElement o, string at, string[] known, DataFile file, Diagnostics diagnostics)
        {
            if (o.ValueKind != JsonValueKind.Object)
            {
                return;                                     // 꼴은 스키마가 본다
            }
            foreach (JsonProperty p in o.EnumerateObject())
            {
                if (Array.IndexOf(known, p.Name) >= 0)
                {
                    continue;
                }
                string where = at + "/" + JsonPointer.Escape(p.Name);
                if (IsForbiddenName(p.Name))
                {
                    diagnostics.Error(file.RepoPath, where, "금지 필드 '" + p.Name + "': 예고 시간을 줄이거나 적을 빠르게 하는 변덕은 만들지 않는다(spec 07 2.5)");
                }
                else
                {
                    diagnostics.Error(file.RepoPath, where, "모르는 필드 '" + p.Name + "'(ModifierDef 에 없다)");
                }
            }
        }

        private static void CheckItemFields(JsonElement a, string at, string[] known, DataFile file, Diagnostics diagnostics)
        {
            if (a.ValueKind != JsonValueKind.Array)
            {
                return;
            }
            int i = 0;
            foreach (JsonElement item in a.EnumerateArray())
            {
                CheckFields(item, at + "/" + i.ToString(CultureInfo.InvariantCulture), known, file, diagnostics);
                i++;
            }
        }

        internal static bool IsForbiddenName(string name)
        {
            foreach (string f in ForbiddenFragments)
            {
                if (name.IndexOf(f, StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        // (종, 값) 배열 → EnemyTypeId 오름차순. pct 면 값은 배율(× 100, 정확해야 한다), 아니면 정수 그대로
        private static List<(EnemyTypeId Species, int Value)> SpeciesValues(JsonElement a, string at, string valueName, bool pct, DataFile file, Diagnostics diagnostics)
        {
            var rows = new List<(EnemyTypeId Species, int Value, int Order)>();
            int i = 0;
            foreach (JsonElement item in a.EnumerateArray())
            {
                string ip = at + "/" + i.ToString(CultureInfo.InvariantCulture);
                int before = diagnostics.Count;
                int v = pct ? (int)WavesConverter.Exact(item, valueName, ip, PctPerUnit, file, diagnostics) : WavesConverter.Int(item, valueName);
                bool known = FramesConverter.TryEnemy(item.GetProperty("species"), ip + "/species", file, diagnostics, out EnemyTypeId species);
                if (known && diagnostics.Count == before)
                {
                    rows.Add((species, v, i));
                }
                i++;
            }
            rows.Sort((x, y) => x.Species != y.Species ? ((ushort)x.Species).CompareTo((ushort)y.Species) : x.Order.CompareTo(y.Order));
            for (int k = 1; k < rows.Count; k++)
            {
                if (rows[k].Species == rows[k - 1].Species)
                {
                    diagnostics.Error(file.RepoPath, at + "/" + rows[k].Order.ToString(CultureInfo.InvariantCulture) + "/species", "종 '" + ContentIds.ToId(rows[k].Species) + "' 가 두 번 있다");
                }
            }
            if (rows.Count > ushort.MaxValue)
            {
                throw new DataBuildException("종이 너무 많다(절의 개수 칸은 u16)");
            }
            return rows.ConvertAll(x => (x.Species, x.Value));
        }
    }
}
