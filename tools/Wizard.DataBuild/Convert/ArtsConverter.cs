using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/arts.json → 팩 절 3 arts. 읽는 쪽: src/Wizard.Content/Pack/Sections/ContentDb.Arts.cs. 스키마: data/schema/arts.schema.json.
    //   id → ArtId(spec 14 3.4, ContentIds)      kind → ArtDef.ArtKind(common 1, secret 2, unique 3)
    //   owner → CharacterId(공용은 Baseline)      cost → ArtCostKind(정수 Fixed 1, "all" AllCrystals 2)
    //   paths 글자 F/B/L/R → CommandStep 0/1/2/3(spec 03 2.3)
    //   effect → ArtDef.Effect, duel → ArtDef.Duel(effect 위에 duel 의 칸만 덮어쓴 한 벌)
    // 단위(CONTRACTS 0.3, 반올림은 가장 가까운 정수, 같으면 짝수): 배율 → 천분율(× 1000), 칸 → Raw(× 2^32), 초 → 틱(× 60),
    //   도 → 센티도(× 100), % → 천분율(× 10), 열 → HU(× 120), 반복 간격 초 → 틱 × 1000(× 60,000, 나누어떨어져야 한다)
    // 절 안의 규칙(변환기가 본다): id 겹침, 고유 ⇔ 주인·부모 대체가 있다, 고유는 섬 슬롯에 없다, 비용 꼴과 min_cost,
    //   한 비기 안의 경로 겹침, 고유의 경로는 루트가 아니다, 시작 해금 ⇔ 선행 기술 없음, 선행·부모 대체가 이 절에 있다,
    //   부모 대체 = 고유 경로의 부모 노드에 붙은 공용 K 기술(spec 03 5.2, 14절), 공용끼리 같은 (경로, 슬롯) 없음, 고유가 공용 K 경로와 겹치지 않음.
    //
    // 절 바이트(LE). 꼴은 ContentDb.Arts.cs 머리 주석과 같다:
    //   u16 개수
    //   항목 × 개수(ArtId 오름차순, 겹침 없음):
    //     u8 id  u8 kind  u8 owner  u8 is_just  i32 difficulty  u8 cost_kind  i32 cost  i32 min_cost  u8 credit  u8 fallback
    //     u8 unlock_at_start  u8 unlock_prereq  i32 unlock_min_wave
    //     u8 path_count  경로 × path_count: u8 길이, u8 step × 길이
    //     효과 × 2(effect, duel): EffectLayout
    //   EffectLayout:
    //     i32 damage_permille  i32 side_damage_permille  i32 branch_count  i32 branch_angle_centi
    //     i64 knockback_raw  i64 self_move_raw
    //     i32 stagger_ticks  i32 stun_ticks  i32 boss_halt_ticks  i32 invuln_ticks
    //     i64 width_raw  i64 thickness_raw  i64 range_raw  i64 radius_raw  i64 clear_radius_raw  i64 offset_raw  i64 shard_length_raw  i64 pull_distance_raw
    //     i32 duration_ticks  i32 interval_ticks  i32 repeat_interval_permille_ticks  i32 slow_permille  i32 heat_absorb_hu  i32 heat_per_t_hu
    //     i32 max_concurrent  i32 max_copies  i32 prep_ticks  i32 wall_hp  i32 telegraph_ticks  u8 ignores_armor
    public sealed class ArtsConverter : ISectionConverter
    {
        // 반복 간격: 초 → 틱 × 1000(만개 0.12초 = 7.2틱 = 7,200)
        private const long PermilleTicksPerSecond = (long)Units.TicksPerSecond * Units.PermillePerUnit;

        public ContentSection Section => ContentSection.Arts;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Entry>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("arts").EnumerateArray())
            {
                string at = "/arts/" + i.ToString(CultureInfo.InvariantCulture);
                Entry? e = Read(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = entries.Count;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: ArtId 오름차순(원본의 나열 순서에 기대지 않는다). 같은 id 끼리는 원본 순서(진단이 뒤에 나온 줄을 가리킨다)
            entries.Sort((a, b) => a.Id != b.Id ? ((byte)a.Id).CompareTo((byte)b.Id) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Id == entries[k - 1].Id)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "비기 '" + ContentIds.ToId(entries[k].Id) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            CheckAcrossArts(entries, file, diagnostics);
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);       // id 가 겹치지 않으므로 ArtId 의 값 수 이하다
            foreach (Entry e in entries)
            {
                Write(e, output);
            }
        }

        private sealed class Entry
        {
            public ArtId Id;
            public ArtDef.ArtKind Kind;
            public CharacterId Owner;
            public bool IsJust;
            public int Difficulty;
            public ArtDef.ArtCostKind CostKind;
            public int Cost, MinCost;
            public bool Credit;
            public ArtId Fallback;
            public bool AtStart;
            public ArtId Prereq;
            public int MinWave;
            public List<byte[]> Paths = new List<byte[]>();
            public Effect Effect = new Effect();
            public Effect Duel = new Effect();
            public string Pointer = "";
            public int Order;
        }

        private sealed class Effect
        {
            public int DamagePermille, SideDamagePermille, BranchCount, BranchAngleCenti;
            public long KnockbackRaw, SelfMoveRaw;
            public int StaggerTicks, StunTicks, BossHaltTicks, InvulnTicks;
            public long WidthRaw, ThicknessRaw, RangeRaw, RadiusRaw, ClearRadiusRaw, OffsetRaw, ShardLengthRaw, PullDistanceRaw;
            public int DurationTicks, IntervalTicks, RepeatIntervalPermilleTicks, SlowPermille, HeatAbsorbHu, HeatPerTHu;
            public int MaxConcurrent, MaxCopies, PrepTicks, WallHp, TelegraphTicks;
            public bool IgnoresArmor;

            public Effect Copy() => (Effect)MemberwiseClone();
        }

        private static Entry? Read(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            var e = new Entry { Pointer = at };
            bool ok = true;
            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParse(id, out e.Id) || e.Id == ArtId.None)
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 비기 id(spec 14 3.4)가 아니다");
                return null;
            }
            switch (row.GetProperty("kind").GetString())
            {
                case "common": e.Kind = ArtDef.ArtKind.Common; break;
                case "secret": e.Kind = ArtDef.ArtKind.Secret; break;
                default: e.Kind = ArtDef.ArtKind.Unique; break;          // 스키마가 셋만 받는다
            }
            bool unique = e.Kind == ArtDef.ArtKind.Unique;

            // 주인
            if (row.TryGetProperty("owner", out JsonElement owner))
            {
                string o = owner.GetString() ?? "";
                if (!unique)
                {
                    diagnostics.Error(file.RepoPath, at + "/owner", "공용 비기에는 주인이 없다(spec 03 12.1)");
                    ok = false;
                }
                else if (!ContentIds.TryParse(o, out e.Owner) || e.Owner == CharacterId.Baseline)
                {
                    diagnostics.Error(file.RepoPath, at + "/owner", "'" + o + "' 는 캐릭터 id(char.1~char.5, spec 14 3.2)가 아니다");
                    ok = false;
                }
            }
            else if (unique)
            {
                diagnostics.Error(file.RepoPath, at + "/owner", "고유 비기에는 주인(owner)이 있어야 한다(spec 03 12.1)");
                ok = false;
            }

            // 경로
            int p = 0;
            foreach (JsonElement path in row.GetProperty("paths").EnumerateArray())
            {
                var steps = new List<byte>();
                foreach (JsonElement s in path.EnumerateArray())
                {
                    steps.Add(Step(s.GetString()));
                }
                byte[] arr = steps.ToArray();
                string pathAt = at + "/paths/" + p.ToString(CultureInfo.InvariantCulture);
                if (IndexOfPath(e.Paths, arr) >= 0)
                {
                    diagnostics.Error(file.RepoPath, pathAt, "같은 경로가 두 번 있다");
                    ok = false;
                }
                if (unique && arr.Length == 0)
                {
                    diagnostics.Error(file.RepoPath, pathAt, "고유 비기는 루트에 붙을 수 없다(부모 대체할 부모가 없다, spec 03 5.2)");
                    ok = false;
                }
                e.Paths.Add(arr);
                p++;
            }

            // 섬, 난이도, 비용, 외상
            e.IsJust = row.TryGetProperty("just", out JsonElement just) && just.GetBoolean();
            if (unique && e.IsJust)
            {
                diagnostics.Error(file.RepoPath, at + "/just", "고유 비기는 섬 슬롯에 붙지 않는다(spec 03 5.2)");
                ok = false;
            }
            e.Difficulty = IntProp(row, "difficulty");
            JsonElement cost = row.GetProperty("cost");
            bool hasMin = row.TryGetProperty("min_cost", out JsonElement minCost);
            if (cost.ValueKind == JsonValueKind.String)
            {
                e.CostKind = ArtDef.ArtCostKind.AllCrystals;
                if (!hasMin)
                {
                    diagnostics.Error(file.RepoPath, at + "/min_cost", "cost 가 \"all\"이면 min_cost 가 있어야 한다(spec 03 12.1 AllMin3)");
                    ok = false;
                }
                else
                {
                    e.MinCost = Int(minCost, "min_cost");
                }
            }
            else
            {
                e.CostKind = ArtDef.ArtCostKind.Fixed;
                e.Cost = Int(cost, "cost");
                if (hasMin)
                {
                    diagnostics.Error(file.RepoPath, at + "/min_cost", "고정 비용 비기에는 min_cost 가 없다");
                    ok = false;
                }
            }
            e.Credit = row.GetProperty("credit").GetBoolean();

            // 부모 대체
            if (row.TryGetProperty("fallback", out JsonElement fallback))
            {
                string f = fallback.GetString() ?? "";
                if (!unique)
                {
                    diagnostics.Error(file.RepoPath, at + "/fallback", "부모 대체는 고유 비기에만 있다(spec 03 7.3)");
                    ok = false;
                }
                else if (!ContentIds.TryParse(f, out e.Fallback) || e.Fallback == ArtId.None)
                {
                    diagnostics.Error(file.RepoPath, at + "/fallback", "'" + f + "' 는 비기 id(spec 14 3.4)가 아니다");
                    ok = false;
                }
            }
            else if (unique)
            {
                diagnostics.Error(file.RepoPath, at + "/fallback", "고유 비기에는 부모 대체(fallback)가 있어야 한다(spec 03 12.1)");
                ok = false;
            }

            // 해금
            JsonElement unlock = row.GetProperty("unlock");
            e.AtStart = unlock.TryGetProperty("at_start", out JsonElement atStart) && atStart.GetBoolean();
            bool hasPrereq = unlock.TryGetProperty("prereq", out JsonElement prereq);
            if (hasPrereq)
            {
                string pr = prereq.GetString() ?? "";
                if (!ContentIds.TryParse(pr, out e.Prereq) || e.Prereq == ArtId.None)
                {
                    diagnostics.Error(file.RepoPath, at + "/unlock/prereq", "'" + pr + "' 는 비기 id(spec 14 3.4)가 아니다");
                    ok = false;
                }
                else if (e.Prereq == e.Id)
                {
                    diagnostics.Error(file.RepoPath, at + "/unlock/prereq", "자기 자신을 선행 기술로 둘 수 없다");
                    ok = false;
                }
            }
            if (unlock.TryGetProperty("min_wave", out JsonElement minWave))
            {
                e.MinWave = Int(minWave, "min_wave");
            }
            if (e.AtStart && (hasPrereq || e.MinWave != 0))
            {
                diagnostics.Error(file.RepoPath, at + "/unlock", "시작 해금 비기에는 선행 기술과 최소 웨이브가 없다(spec 03 15.1 U-R2)");
                ok = false;
            }
            if (!e.AtStart && !hasPrereq)
            {
                diagnostics.Error(file.RepoPath, at + "/unlock", "시작 해금이 아니면 선행 기술(prereq)이 있어야 한다(spec 03 15.3)");
                ok = false;
            }
            if (unique && !e.AtStart)
            {
                diagnostics.Error(file.RepoPath, at + "/unlock", "고유 비기는 처음부터 쓸 수 있다(DECISIONS 1.23, spec 03 15.1 U-R2)");
                ok = false;
            }

            // 효과
            e.Effect = ReadEffect(row.GetProperty("effect"), new Effect());
            e.Duel = row.TryGetProperty("duel", out JsonElement duel) ? ReadEffect(duel, e.Effect.Copy()) : e.Effect.Copy();
            return ok ? e : null;
        }

        // 절 안에서 비기끼리 맞물리는 규칙
        private static void CheckAcrossArts(List<Entry> entries, DataFile file, Diagnostics diagnostics)
        {
            foreach (Entry e in entries)
            {
                if (e.Prereq != ArtId.None && Find(entries, e.Prereq) == null)
                {
                    diagnostics.Error(file.RepoPath, e.Pointer + "/unlock/prereq", "선행 기술 '" + ContentIds.ToId(e.Prereq) + "' 가 이 절에 없다");
                }
                if (e.Kind != ArtDef.ArtKind.Unique)
                {
                    // 공용끼리 같은 경로의 같은 슬롯에 붙지 않는다(꿰뚫는 창과 뇌광창은 FF 의 K 와 섬으로 다르다)
                    foreach (Entry o in entries)
                    {
                        if (o.Order >= e.Order || o.Kind == ArtDef.ArtKind.Unique || o.IsJust != e.IsJust)
                        {
                            continue;
                        }
                        foreach (byte[] path in e.Paths)
                        {
                            if (IndexOfPath(o.Paths, path) >= 0)
                            {
                                diagnostics.Error(file.RepoPath, e.Pointer + "/paths", "'" + ContentIds.ToId(o.Id) + "' 와 같은 경로의 같은 슬롯에 붙는다");
                            }
                        }
                    }
                    continue;
                }
                Entry? parent = e.Fallback == ArtId.None ? null : Find(entries, e.Fallback);
                if (parent == null)
                {
                    diagnostics.Error(file.RepoPath, e.Pointer + "/fallback", "부모 대체 '" + ContentIds.ToId(e.Fallback) + "' 가 이 절에 없다");
                    continue;
                }
                if (parent.Kind == ArtDef.ArtKind.Unique || parent.IsJust)
                {
                    diagnostics.Error(file.RepoPath, e.Pointer + "/fallback", "부모 대체는 공용 K 기술이다(spec 03 14절)");
                    continue;
                }
                for (int p = 0; p < e.Paths.Count; p++)
                {
                    byte[] path = e.Paths[p];
                    string pathAt = e.Pointer + "/paths/" + p.ToString(CultureInfo.InvariantCulture);
                    if (path.Length > 0 && IndexOfPath(parent.Paths, Prefix(path)) < 0)
                    {
                        diagnostics.Error(file.RepoPath, pathAt, "부모 대체 '" + ContentIds.ToId(parent.Id) + "' 가 이 경로의 부모 노드에 없다(spec 03 5.2)");
                    }
                    foreach (Entry o in entries)
                    {
                        if (o.Kind != ArtDef.ArtKind.Unique && !o.IsJust && IndexOfPath(o.Paths, path) >= 0)
                        {
                            diagnostics.Error(file.RepoPath, pathAt, "고유 비기의 자리가 공용 '" + ContentIds.ToId(o.Id) + "' 의 경로와 겹친다(spec 03 5.1)");
                        }
                    }
                }
            }
        }

        private static Entry? Find(List<Entry> entries, ArtId id)
        {
            foreach (Entry e in entries)
            {
                if (e.Id == id)
                {
                    return e;
                }
            }
            return null;
        }

        private static byte[] Prefix(byte[] path)
        {
            var r = new byte[path.Length - 1];
            for (int i = 0; i < r.Length; i++)
            {
                r[i] = path[i];
            }
            return r;
        }

        private static int IndexOfPath(List<byte[]> paths, byte[] path)
        {
            for (int i = 0; i < paths.Count; i++)
            {
                byte[] q = paths[i];
                if (q.Length != path.Length)
                {
                    continue;
                }
                bool same = true;
                for (int k = 0; k < q.Length; k++)
                {
                    if (q[k] != path[k])
                    {
                        same = false;
                        break;
                    }
                }
                if (same)
                {
                    return i;
                }
            }
            return -1;
        }

        // spec 03 2.3 CmdDir 번호. 스키마가 넷만 받는다
        private static byte Step(string? letter)
        {
            switch (letter)
            {
                case "F": return (byte)ArtDef.CommandStep.Forward;
                case "B": return (byte)ArtDef.CommandStep.Back;
                case "L": return (byte)ArtDef.CommandStep.Left;
                default: return (byte)ArtDef.CommandStep.Right;
            }
        }

        // into 위에 o 에 있는 칸만 덮어쓴다(effect 는 빈 값 위에, duel 은 effect 의 사본 위에)
        private static Effect ReadEffect(JsonElement o, Effect into)
        {
            if (o.TryGetProperty("damage", out JsonElement v)) into.DamagePermille = Units.RatioToPermille(v.GetDecimal());
            if (o.TryGetProperty("side_damage", out v)) into.SideDamagePermille = Units.RatioToPermille(v.GetDecimal());
            if (o.TryGetProperty("branches", out v)) into.BranchCount = Int(v, "branches");
            if (o.TryGetProperty("branch_angle_degrees", out v)) into.BranchAngleCenti = Units.DegreesToCenti(v.GetDecimal());
            if (o.TryGetProperty("knockback", out v)) into.KnockbackRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("self_move", out v)) into.SelfMoveRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("stagger_seconds", out v)) into.StaggerTicks = Units.SecondsToTicks(v.GetDecimal());
            if (o.TryGetProperty("stun_seconds", out v)) into.StunTicks = Units.SecondsToTicks(v.GetDecimal());
            if (o.TryGetProperty("boss_halt_seconds", out v)) into.BossHaltTicks = Units.SecondsToTicks(v.GetDecimal());
            if (o.TryGetProperty("invuln_seconds", out v)) into.InvulnTicks = Units.SecondsToTicks(v.GetDecimal());
            if (o.TryGetProperty("width", out v)) into.WidthRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("thickness", out v)) into.ThicknessRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("range", out v)) into.RangeRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("radius", out v)) into.RadiusRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("clear_radius", out v)) into.ClearRadiusRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("offset", out v)) into.OffsetRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("shard_length", out v)) into.ShardLengthRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("pull_distance", out v)) into.PullDistanceRaw = Units.CellsToRaw(v.GetDecimal());
            if (o.TryGetProperty("duration_seconds", out v)) into.DurationTicks = Units.SecondsToTicks(v.GetDecimal());
            if (o.TryGetProperty("interval_seconds", out v)) into.IntervalTicks = Units.SecondsToTicks(v.GetDecimal());
            if (o.TryGetProperty("repeat_interval_seconds", out v)) into.RepeatIntervalPermilleTicks = ExactPermilleTicks(v.GetDecimal());
            if (o.TryGetProperty("slow_percent", out v)) into.SlowPermille = Units.PercentToPermille(v.GetDecimal());
            if (o.TryGetProperty("heat_absorb", out v)) into.HeatAbsorbHu = Units.HeatToHu(v.GetDecimal());
            if (o.TryGetProperty("heat_per_t", out v)) into.HeatPerTHu = Units.HeatToHu(v.GetDecimal());
            if (o.TryGetProperty("max_concurrent", out v)) into.MaxConcurrent = Int(v, "max_concurrent");
            if (o.TryGetProperty("max_copies", out v)) into.MaxCopies = Int(v, "max_copies");
            if (o.TryGetProperty("prep_seconds", out v)) into.PrepTicks = Units.SecondsToTicks(v.GetDecimal());
            if (o.TryGetProperty("wall_hp", out v)) into.WallHp = Int(v, "wall_hp");
            if (o.TryGetProperty("telegraph_seconds", out v)) into.TelegraphTicks = Units.SecondsToTicks(v.GetDecimal());
            if (o.TryGetProperty("ignores_armor", out v)) into.IgnoresArmor = v.GetBoolean();
            return into;
        }

        // 반복 간격은 틱의 천분율까지 정확해야 한다(round(i × 7.2) 의 7.2, spec 03 U-13). 나누어떨어지지 않으면 반올림하지 않고 오류
        private static int ExactPermilleTicks(decimal seconds)
        {
            decimal v = seconds * PermilleTicksPerSecond;
            if (v != decimal.Truncate(v) || v > int.MaxValue)
            {
                throw new DataBuildException("반복 간격 " + seconds.ToString(CultureInfo.InvariantCulture) + "초는 틱의 천분율로 나누어떨어지지 않는다");
            }
            return (int)v;
        }

        private static int IntProp(JsonElement o, string name) => Int(o.GetProperty(name), name);

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

        private static void Write(Entry e, ByteWriter w)
        {
            w.U8((byte)e.Id);
            w.U8((byte)e.Kind);
            w.U8((byte)e.Owner);
            w.U8(e.IsJust ? (byte)1 : (byte)0);
            w.I32(e.Difficulty);
            w.U8((byte)e.CostKind);
            w.I32(e.Cost);
            w.I32(e.MinCost);
            w.U8(e.Credit ? (byte)1 : (byte)0);
            w.U8((byte)e.Fallback);
            w.U8(e.AtStart ? (byte)1 : (byte)0);
            w.U8((byte)e.Prereq);
            w.I32(e.MinWave);
            w.U8((byte)e.Paths.Count);             // 스키마가 16 개 이하로 막는다
            foreach (byte[] path in e.Paths)
            {
                w.U8((byte)path.Length);           // 스키마가 8 이하로 막는다
                foreach (byte s in path)
                {
                    w.U8(s);
                }
            }
            WriteEffect(e.Effect, w);
            WriteEffect(e.Duel, w);
        }

        private static void WriteEffect(Effect f, ByteWriter w)
        {
            w.I32(f.DamagePermille);
            w.I32(f.SideDamagePermille);
            w.I32(f.BranchCount);
            w.I32(f.BranchAngleCenti);
            w.I64(f.KnockbackRaw);
            w.I64(f.SelfMoveRaw);
            w.I32(f.StaggerTicks);
            w.I32(f.StunTicks);
            w.I32(f.BossHaltTicks);
            w.I32(f.InvulnTicks);
            w.I64(f.WidthRaw);
            w.I64(f.ThicknessRaw);
            w.I64(f.RangeRaw);
            w.I64(f.RadiusRaw);
            w.I64(f.ClearRadiusRaw);
            w.I64(f.OffsetRaw);
            w.I64(f.ShardLengthRaw);
            w.I64(f.PullDistanceRaw);
            w.I32(f.DurationTicks);
            w.I32(f.IntervalTicks);
            w.I32(f.RepeatIntervalPermilleTicks);
            w.I32(f.SlowPermille);
            w.I32(f.HeatAbsorbHu);
            w.I32(f.HeatPerTHu);
            w.I32(f.MaxConcurrent);
            w.I32(f.MaxCopies);
            w.I32(f.PrepTicks);
            w.I32(f.WallHp);
            w.I32(f.TelegraphTicks);
            w.U8(f.IgnoresArmor ? (byte)1 : (byte)0);
        }
    }
}
