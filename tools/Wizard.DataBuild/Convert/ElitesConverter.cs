using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.DataBuild.Validate.Evasion;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/elites.json → 팩 절 18 elites. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Elites.cs.
    // 스키마: data/schema/elites.schema.json(꼴과 범위는 스키마가 이미 보았다. 공격 한 줄의 꼴은 enemies.schema.json 의 attack).
    //   id → 바탕 EnemyTypeId(spec 14 3.7, ContentIds.TryParseElite)    ability → EliteDef.EliteAbility(spec 05 7.2 줄 순서)
    //   공격 한 줄은 적 절과 같이 읽고 검사하고 쓴다(EnemiesConverter.ReadAttack·WriteAttack). 단위도 같다.
    // 이 절 안에서 보는 규칙(spec 05 7.1~7.3):
    //   겹치는 엘리트, 능력이 종마다 하나(같은 능력이 두 번 나오지 않는다), 능력이 쓰는 매개변수만 있고 다 있다,
    //   능력마다 공격의 짝(대신하는 공격·이어 나오는 공격·따로 쓰는 공격의 수), 첨자 replaces·follows 는 둘 중 하나만,
    //   엘리트의 공격에 접촉은 없다(바탕 종의 접촉을 그대로 쓴다), 체력 문턱은 내림차순.
    // 다른 파일(enemies.json)과의 짝은 CrossCheck(교차 검사), 회피 검산(R1~R4)의 원본은 EvasionSource 가 맡는다(둘 다 이 어셈블리에서 찾는다).
    public sealed class ElitesConverter : ISectionConverter
    {
        private const int MaxAttacks = 16;            // 스키마의 maxItems 와 같다(절의 u8 개수 안)
        private const int MaxThresholds = 16;         // 스키마의 summon_hp_ratios maxItems 와 같다

        public const string ElitesFile = "sim/elites.json";
        public const string EnemiesFile = "sim/enemies.json";

        public ContentSection Section => ContentSection.Elites;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Elite>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("elites").EnumerateArray())
            {
                string at = "/elites/" + Num(i);
                Elite? e = ReadElite(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = entries.Count;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: 바탕 EnemyTypeId 오름차순(원본의 나열 순서에 기대지 않는다). 같은 id 끼리는 원본 순서
            entries.Sort((a, b) => a.Base != b.Base ? ((ushort)a.Base).CompareTo((ushort)b.Base) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Base == entries[k - 1].Base)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "엘리트 '" + ContentIds.ToEliteId(entries[k].Base) + "' 가 두 번 있다");
                }
            }
            // 고유 능력은 종마다 하나다(spec 05 7.1): 같은 능력을 두 종이 갖지 않는다
            var byAbility = new List<Elite>(entries);
            byAbility.Sort((a, b) => a.Ability != b.Ability ? ((byte)a.Ability).CompareTo((byte)b.Ability) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < byAbility.Count; k++)
            {
                if (byAbility[k].Ability == byAbility[k - 1].Ability && byAbility[k].Base != byAbility[k - 1].Base)
                {
                    diagnostics.Error(file.RepoPath, byAbility[k].Pointer + "/ability", "고유 능력 '" + AbilityName(byAbility[k].Ability) + "' 를 두 엘리트가 갖는다(종마다 하나, spec 05 7.1)");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);
            foreach (Elite e in entries)
            {
                WriteElite(e, output);
            }
        }

        private sealed class Elite
        {
            public EnemyTypeId Base;
            public EliteDef.EliteAbility Ability;
            public List<EliteAttackRow> Attacks = new List<EliteAttackRow>();
            public Params P = new Params();
            public string Pointer = "";
            public int Order;
        }

        private sealed class EliteAttackRow
        {
            public int Replaces = -1, Follows = -1;
            public EnemiesConverter.Attack Attack = new EnemiesConverter.Attack();
        }

        private sealed class Params
        {
            public int SplitCount, SplitStartAngleCenti, HowlTicks, HowlStepTicks, ChainCount, ChainGapTicks, ArmorHalfAngleCenti;
            public long SplitRadiusRaw, HowlRadiusRaw, SympatheticDamageRaw, SummonRadiusRaw, SeedLeadRaw, SeedInsetRaw, CroakRadiusRaw, CroakBreakRaw;
            public int AimCount, AimStepTicks, BandCount, BandStartAngleCenti, SummonCount;
            public List<int> SummonHpPermille = new List<int>();
            public int RingRepeat, RingRepeatStepTicks, RingRepeatAngleCenti, SeedCount, BounceCount, BounceStopTicks;
            public bool CroakBreakOnly;
        }

        // 능력마다 쓰는 매개변수(spec 05 7.2·7.3). 이것만 있고 다 있어야 한다
        public static IReadOnlyList<string> ParamsOf(EliteDef.EliteAbility a)
        {
            switch (a)
            {
                case EliteDef.EliteAbility.Split: return new[] { "split_count", "split_radius", "split_start_angle_degrees" };
                case EliteDef.EliteAbility.Howl: return new[] { "howl_seconds", "howl_radius", "howl_step_seconds", "chain_count", "chain_gap_seconds" };
                case EliteDef.EliteAbility.Shards: return new[] { "armor_half_angle_degrees" };
                case EliteDef.EliteAbility.WideSpread: return new string[0];
                case EliteDef.EliteAbility.BigBlast: return new[] { "sympathetic_damage" };
                case EliteDef.EliteAbility.TripleAim: return new[] { "aim_count", "aim_step_seconds" };
                case EliteDef.EliteAbility.EarthBands: return new[] { "band_count", "band_start_angle_degrees" };
                case EliteDef.EliteAbility.Summon: return new[] { "summon_count", "summon_radius", "summon_hp_ratios" };
                case EliteDef.EliteAbility.TwinRing: return new[] { "ring_repeat", "ring_repeat_step_seconds", "ring_repeat_angle_degrees" };
                case EliteDef.EliteAbility.Bloom: return new[] { "seed_count", "seed_lead_distance", "seed_wall_inset" };
                case EliteDef.EliteAbility.Bounce: return new[] { "bounce_count", "bounce_stop_seconds" };
                default: return new[] { "croak_radius", "croak_break_damage", "croak_break_only" };
            }
        }

        // 능력마다 공격의 짝(대신하는 수, 이어 나오는 수, 따로 쓰는 수). spec 05 7.2: 고인돌 파편은 내려찍기에 이어,
        // 큰갓 5갈래·왕꽈리 점화·꽃 핀 씨앗은 바탕 공격 대신, 화분 어미 흙 띠는 솟구침에 이어, 박쥐 여왕 급강하는 따로
        public static (int Replaces, int Follows, int Extra) AttackShapeOf(EliteDef.EliteAbility a)
        {
            switch (a)
            {
                case EliteDef.EliteAbility.Shards: return (0, 1, 0);
                case EliteDef.EliteAbility.WideSpread: return (1, 0, 0);
                case EliteDef.EliteAbility.BigBlast: return (1, 0, 0);
                case EliteDef.EliteAbility.EarthBands: return (0, 1, 0);
                case EliteDef.EliteAbility.Summon: return (0, 0, 1);
                case EliteDef.EliteAbility.Bloom: return (1, 0, 0);
                default: return (0, 0, 0);
            }
        }

        private static Elite? ReadElite(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            var e = new Elite { Pointer = at };
            bool ok = true;
            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParseElite(id, out e.Base))
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 엘리트 id(spec 14 3.7)가 아니다");
                ok = false;
            }
            e.Ability = AbilityOf(row.GetProperty("ability").GetString());

            int a = 0;
            int replaces = 0, follows = 0, extra = 0;
            foreach (JsonElement ar in row.GetProperty("attacks").EnumerateArray())
            {
                string p = at + "/attacks/" + Num(a);
                var x = new EliteAttackRow
                {
                    Replaces = OptionalIndex(ar, "replaces"),
                    Follows = OptionalIndex(ar, "follows"),
                };
                if (x.Replaces >= 0 && x.Follows >= 0)
                {
                    ok &= Fail(file, p + "/follows", "replaces 와 follows 는 둘 중 하나만 적는다", diagnostics);
                }
                replaces += x.Replaces >= 0 ? 1 : 0;
                follows += x.Follows >= 0 ? 1 : 0;
                extra += x.Replaces < 0 && x.Follows < 0 ? 1 : 0;
                JsonElement attackRow = ar.GetProperty("attack");
                if (attackRow.GetProperty("kind").GetString() == "contact")
                {
                    ok &= Fail(file, p + "/attack/kind", "엘리트 공격에 접촉은 없다(바탕 종의 접촉을 그대로 쓴다, spec 05 7.1)", diagnostics);
                }
                EnemiesConverter.Attack? attack = EnemiesConverter.ReadAttack(attackRow, p + "/attack", 0, file, diagnostics);
                if (attack == null)
                {
                    ok = false;
                }
                else
                {
                    x.Attack = attack;
                    e.Attacks.Add(x);
                }
                a++;
            }
            if (a > MaxAttacks)
            {
                throw new DataBuildException("엘리트 공격이 " + Num(MaxAttacks) + " 개를 넘는다(스키마가 막는다)");
            }
            (int wantReplaces, int wantFollows, int wantExtra) = AttackShapeOf(e.Ability);
            if (replaces != wantReplaces || follows != wantFollows || extra != wantExtra)
            {
                ok &= Fail(file, at + "/attacks", "고유 능력 '" + AbilityName(e.Ability) + "' 의 공격은 대신하는 것 " + Num(wantReplaces)
                    + "개, 이어 나오는 것 " + Num(wantFollows) + "개, 따로 쓰는 것 " + Num(wantExtra) + "개다(spec 05 7.2). 지금 "
                    + Num(replaces) + ", " + Num(follows) + ", " + Num(extra), diagnostics);
            }

            ok &= ReadParams(row.GetProperty("params"), at + "/params", e, file, diagnostics);
            return ok ? e : null;
        }

        private static bool ReadParams(JsonElement o, string at, Elite e, DataFile file, Diagnostics diagnostics)
        {
            bool ok = true;
            IReadOnlyList<string> want = ParamsOf(e.Ability);
            foreach (JsonProperty prop in o.EnumerateObject())
            {
                if (prop.Name == "_note")
                {
                    continue;
                }
                if (!Contains(want, prop.Name))
                {
                    ok &= Fail(file, at + "/" + prop.Name, "고유 능력 '" + AbilityName(e.Ability) + "' 는 매개변수 '" + prop.Name + "' 를 쓰지 않는다(spec 05 7.2)", diagnostics);
                }
            }
            foreach (string name in want)
            {
                if (!o.TryGetProperty(name, out _))
                {
                    ok &= Fail(file, at, "고유 능력 '" + AbilityName(e.Ability) + "' 의 매개변수 '" + name + "' 가 없다(spec 05 7.2)", diagnostics);
                }
            }
            if (!ok)
            {
                return false;
            }
            Params p = e.P;
            p.SplitCount = IntOr(o, "split_count");
            p.SplitRadiusRaw = Units.CellsToRaw(EnemiesConverter.DecOr(o, "split_radius"));
            p.SplitStartAngleCenti = Units.DegreesToCenti(EnemiesConverter.DecOr(o, "split_start_angle_degrees"));
            p.HowlTicks = Units.SecondsToTicks(EnemiesConverter.DecOr(o, "howl_seconds"));
            p.HowlRadiusRaw = Units.CellsToRaw(EnemiesConverter.DecOr(o, "howl_radius"));
            p.HowlStepTicks = Units.SecondsToTicks(EnemiesConverter.DecOr(o, "howl_step_seconds"));
            p.ChainCount = IntOr(o, "chain_count");
            p.ChainGapTicks = Units.SecondsToTicks(EnemiesConverter.DecOr(o, "chain_gap_seconds"));
            p.ArmorHalfAngleCenti = Units.DegreesToCenti(EnemiesConverter.DecOr(o, "armor_half_angle_degrees"));
            p.SympatheticDamageRaw = EnemiesConverter.FixRaw(EnemiesConverter.DecOr(o, "sympathetic_damage"));
            p.AimCount = IntOr(o, "aim_count");
            p.AimStepTicks = Units.SecondsToTicks(EnemiesConverter.DecOr(o, "aim_step_seconds"));
            p.BandCount = IntOr(o, "band_count");
            p.BandStartAngleCenti = Units.DegreesToCenti(EnemiesConverter.DecOr(o, "band_start_angle_degrees"));
            p.SummonCount = IntOr(o, "summon_count");
            p.SummonRadiusRaw = Units.CellsToRaw(EnemiesConverter.DecOr(o, "summon_radius"));
            if (o.TryGetProperty("summon_hp_ratios", out JsonElement ratios))
            {
                int k = 0;
                foreach (JsonElement r in ratios.EnumerateArray())
                {
                    int v = Units.RatioToPermille(r.GetDecimal());
                    if (p.SummonHpPermille.Count > 0 && v >= p.SummonHpPermille[p.SummonHpPermille.Count - 1])
                    {
                        ok &= Fail(file, at + "/summon_hp_ratios/" + Num(k), "부르는 체력 문턱은 내림차순이고 겹치지 않는다(60%, 20%)", diagnostics);
                    }
                    p.SummonHpPermille.Add(v);
                    k++;
                }
                if (k > MaxThresholds)
                {
                    throw new DataBuildException("체력 문턱이 " + Num(MaxThresholds) + " 개를 넘는다(스키마가 막는다)");
                }
            }
            p.RingRepeat = IntOr(o, "ring_repeat");
            p.RingRepeatStepTicks = Units.SecondsToTicks(EnemiesConverter.DecOr(o, "ring_repeat_step_seconds"));
            p.RingRepeatAngleCenti = Units.DegreesToCenti(EnemiesConverter.DecOr(o, "ring_repeat_angle_degrees"));
            p.SeedCount = IntOr(o, "seed_count");
            p.SeedLeadRaw = Units.CellsToRaw(EnemiesConverter.DecOr(o, "seed_lead_distance"));
            p.SeedInsetRaw = Units.CellsToRaw(EnemiesConverter.DecOr(o, "seed_wall_inset"));
            p.BounceCount = IntOr(o, "bounce_count");
            p.BounceStopTicks = Units.SecondsToTicks(EnemiesConverter.DecOr(o, "bounce_stop_seconds"));
            p.CroakRadiusRaw = Units.CellsToRaw(EnemiesConverter.DecOr(o, "croak_radius"));
            p.CroakBreakRaw = EnemiesConverter.FixRaw(EnemiesConverter.DecOr(o, "croak_break_damage"));
            p.CroakBreakOnly = EnemiesConverter.BoolOr(o, "croak_break_only");
            return ok;
        }

        private static void WriteElite(Elite e, ByteWriter w)
        {
            w.U16((ushort)e.Base);
            w.U8((byte)e.Ability);
            w.U8((byte)e.Attacks.Count);
            foreach (EliteAttackRow a in e.Attacks)
            {
                w.I32(a.Replaces);
                w.I32(a.Follows);
                EnemiesConverter.WriteAttack(a.Attack, w);
            }
            Params p = e.P;
            w.I32(p.SplitCount);
            w.I64(p.SplitRadiusRaw);
            w.I32(p.SplitStartAngleCenti);
            w.I32(p.HowlTicks);
            w.I64(p.HowlRadiusRaw);
            w.I32(p.HowlStepTicks);
            w.I32(p.ChainCount);
            w.I32(p.ChainGapTicks);
            w.I32(p.ArmorHalfAngleCenti);
            w.I64(p.SympatheticDamageRaw);
            w.I32(p.AimCount);
            w.I32(p.AimStepTicks);
            w.I32(p.BandCount);
            w.I32(p.BandStartAngleCenti);
            w.I32(p.SummonCount);
            w.I64(p.SummonRadiusRaw);
            w.U8((byte)p.SummonHpPermille.Count);
            foreach (int v in p.SummonHpPermille)
            {
                w.I32(v);
            }
            w.I32(p.RingRepeat);
            w.I32(p.RingRepeatStepTicks);
            w.I32(p.RingRepeatAngleCenti);
            w.I32(p.SeedCount);
            w.I64(p.SeedLeadRaw);
            w.I64(p.SeedInsetRaw);
            w.I32(p.BounceCount);
            w.I32(p.BounceStopTicks);
            w.I64(p.CroakRadiusRaw);
            w.I64(p.CroakBreakRaw);
            w.U8(p.CroakBreakOnly ? (byte)1 : (byte)0);
        }

        // 스키마의 enum 이 이미 거른 값이다
        public static EliteDef.EliteAbility AbilityOf(string? s)
        {
            switch (s)
            {
                case "split": return EliteDef.EliteAbility.Split;
                case "howl": return EliteDef.EliteAbility.Howl;
                case "shards": return EliteDef.EliteAbility.Shards;
                case "wide_spread": return EliteDef.EliteAbility.WideSpread;
                case "big_blast": return EliteDef.EliteAbility.BigBlast;
                case "triple_aim": return EliteDef.EliteAbility.TripleAim;
                case "earth_bands": return EliteDef.EliteAbility.EarthBands;
                case "summon": return EliteDef.EliteAbility.Summon;
                case "twin_ring": return EliteDef.EliteAbility.TwinRing;
                case "bloom": return EliteDef.EliteAbility.Bloom;
                case "bounce": return EliteDef.EliteAbility.Bounce;
                case "big_drum": return EliteDef.EliteAbility.BigDrum;
                default: throw new DataBuildException("모르는 엘리트 능력 '" + s + "'");
            }
        }

        private static string AbilityName(EliteDef.EliteAbility a)
        {
            switch (a)
            {
                case EliteDef.EliteAbility.Split: return "split";
                case EliteDef.EliteAbility.Howl: return "howl";
                case EliteDef.EliteAbility.Shards: return "shards";
                case EliteDef.EliteAbility.WideSpread: return "wide_spread";
                case EliteDef.EliteAbility.BigBlast: return "big_blast";
                case EliteDef.EliteAbility.TripleAim: return "triple_aim";
                case EliteDef.EliteAbility.EarthBands: return "earth_bands";
                case EliteDef.EliteAbility.Summon: return "summon";
                case EliteDef.EliteAbility.TwinRing: return "twin_ring";
                case EliteDef.EliteAbility.Bloom: return "bloom";
                case EliteDef.EliteAbility.Bounce: return "bounce";
                default: return "big_drum";
            }
        }

        private static int OptionalIndex(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement v) && v.ValueKind != JsonValueKind.Null ? EnemiesConverter.IntOf(v, name) : -1;

        private static int IntOr(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement v) ? EnemiesConverter.IntOf(v, name) : 0;

        private static bool Contains(IReadOnlyList<string> list, string s)
        {
            foreach (string x in list)
            {
                if (x == s)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Fail(DataFile file, string pointer, string message, Diagnostics diagnostics)
        {
            diagnostics.Error(file.RepoPath, pointer, message);
            return false;
        }

        private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static DataFile? FileOf(DataSet data, string dataPath)
        {
            foreach (DataFile f in data.Files)
            {
                if (f.DataPath == dataPath)
                {
                    return f;
                }
            }
            return null;
        }

        private static string Str(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement e) && e.ValueKind == JsonValueKind.String ? e.GetString() ?? "" : "";

        // 엘리트 줄마다 (바탕 적 줄, 엘리트 줄, 엘리트 JSON 위치). 바탕 줄을 찾지 못하면 바탕 줄은 default
        private static List<(JsonElement Base, bool HasBase, JsonElement Elite, string Pointer, string Id)> Pairs(DataSet data)
        {
            var list = new List<(JsonElement, bool, JsonElement, string, string)>();
            DataFile? elites = FileOf(data, ElitesFile);
            if (elites == null || !elites.Root.TryGetProperty("elites", out JsonElement rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return list;
            }
            DataFile? enemies = FileOf(data, EnemiesFile);
            int i = 0;
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string id = Str(row, "id");
                JsonElement found = default;
                bool has = false;
                if (ContentIds.TryParseElite(id, out EnemyTypeId baseType) && enemies != null
                    && enemies.Root.TryGetProperty("enemies", out JsonElement er) && er.ValueKind == JsonValueKind.Array)
                {
                    string baseId = ContentIds.ToId(baseType);
                    foreach (JsonElement en in er.EnumerateArray())
                    {
                        if (Str(en, "id") == baseId)
                        {
                            found = en;
                            has = true;
                            break;
                        }
                    }
                }
                list.Add((found, has, row, "/elites/" + Num(i), id));
                i++;
            }
            return list;
        }

        private static List<JsonElement> AttacksOf(JsonElement enemyRow)
        {
            var list = new List<JsonElement>();
            if (enemyRow.ValueKind == JsonValueKind.Object && enemyRow.TryGetProperty("attacks", out JsonElement a) && a.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement x in a.EnumerateArray())
                {
                    list.Add(x);
                }
            }
            return list;
        }

        // 바탕 종이 그 종류의 공격을 가졌는가(첫 첨자, 없으면 −1)
        private static int FirstOfKind(List<JsonElement> attacks, string kind)
        {
            for (int i = 0; i < attacks.Count; i++)
            {
                if (Str(attacks[i], "kind") == kind)
                {
                    return i;
                }
            }
            return -1;
        }

        // 능력이 바탕 종의 어느 공격을 되풀이·변형하는가(spec 05 7.3): 우두머리 늑대·외엄니는 돌진, 삼연 고슴도치는 조준 사격,
        // 쌍둥이불은 고리 탄을 쓴다. 없으면 null
        public static string? BaseKindUsedBy(EliteDef.EliteAbility a)
        {
            switch (a)
            {
                case EliteDef.EliteAbility.Howl: return "dash";
                case EliteDef.EliteAbility.Bounce: return "dash";
                case EliteDef.EliteAbility.TripleAim: return "aimed_shot";
                case EliteDef.EliteAbility.TwinRing: return "ring_shot";
                default: return null;
            }
        }

        // 교차 검사(data/sim/enemies.json ↔ elites.json): 바탕 종이 있고 졸개가 아니다, replaces·follows 첨자가 바탕 공격 안이다,
        // 대신하는 공격은 바탕 공격과 종류가 같다, 이어 나오는 공격의 바탕 공격은 예고 구역이 있다(그 자리에서 나온다),
        // 능력이 되풀이하는 바탕 공격(돌진, 조준 사격, 고리 탄)이 바탕 종에 있다. 엘리트 파일이 없으면 보지 않는다
        public sealed class CrossCheck : IDataValidator
        {
            public string Name => "elites-base";

            public void Validate(DataSet data, Diagnostics diagnostics)
            {
                DataFile? elites = FileOf(data, ElitesFile);
                if (elites == null)
                {
                    return;
                }
                foreach (var (baseRow, hasBase, row, at, id) in Pairs(data))
                {
                    if (!ContentIds.TryParseElite(id, out EnemyTypeId baseType))
                    {
                        continue;                     // 변환기가 알린다
                    }
                    string baseId = ContentIds.ToId(baseType);
                    if (!hasBase)
                    {
                        diagnostics.Error(elites.RepoPath, at + "/id", id + ": 바탕 종 '" + baseId + "' 가 " + DataLoader.DataDir + "/" + EnemiesFile + " 에 없다(spec 05 7.2)");
                        continue;
                    }
                    if (baseRow.TryGetProperty("minion", out JsonElement m) && m.ValueKind == JsonValueKind.True)
                    {
                        diagnostics.Error(elites.RepoPath, at + "/id", id + ": 졸개에는 엘리트가 없다(spec 14 3.7)");
                        continue;
                    }
                    List<JsonElement> baseAttacks = AttacksOf(baseRow);
                    EliteDef.EliteAbility ability = AbilityOf(Str(row, "ability"));
                    string? usedKind = BaseKindUsedBy(ability);
                    if (usedKind != null && FirstOfKind(baseAttacks, usedKind) < 0)
                    {
                        diagnostics.Error(elites.RepoPath, at + "/ability", id + ": 고유 능력이 되풀이하는 바탕 공격(" + usedKind + ")이 " + baseId + " 에 없다(spec 05 7.3)");
                    }
                    int j = 0;
                    foreach (JsonElement ar in row.GetProperty("attacks").EnumerateArray())
                    {
                        string p = at + "/attacks/" + Num(j);
                        string kind = Str(ar.GetProperty("attack"), "kind");
                        int replaces = OptionalIndex(ar, "replaces");
                        int follows = OptionalIndex(ar, "follows");
                        if (replaces >= baseAttacks.Count || follows >= baseAttacks.Count)
                        {
                            diagnostics.Error(elites.RepoPath, p + (replaces >= 0 ? "/replaces" : "/follows"),
                                id + ": 바탕 종 " + baseId + " 의 공격은 " + Num(baseAttacks.Count) + "개다(첨자 0~" + Num(baseAttacks.Count - 1) + ")");
                        }
                        else if (replaces >= 0 && Str(baseAttacks[replaces], "kind") != kind)
                        {
                            diagnostics.Error(elites.RepoPath, p + "/replaces",
                                id + ": 대신하는 바탕 공격 " + baseId + "." + Str(baseAttacks[replaces], "key") + " 은 " + Str(baseAttacks[replaces], "kind") + " 인데 이 공격은 " + kind + " 이다");
                        }
                        else if (follows >= 0 && Str(baseAttacks[follows], "shape").Length == 0)
                        {
                            diagnostics.Error(elites.RepoPath, p + "/follows",
                                id + ": 이어 나오는 공격은 바탕 공격의 예고 구역에서 나온다. " + baseId + "." + Str(baseAttacks[follows], "key") + " 에는 예고 구역이 없다");
                        }
                        j++;
                    }
                }
            }
        }

        // 회피 검산(spec 05 10절)의 원본: 엘리트 공격을 그대로 넘기고, 쌍둥이불처럼 고리를 어긋나게 되풀이하면 두 고리를 합친 탄 사이를
        // R3 으로 한 줄 더 넘긴다(spec 05 10.6 "쌍둥이불 두 고리 사이 22.5도 1.18 통과" [검산 추가]). 엘리트의 몸은 바탕 종과 같아 R2 줄은 없다
        public sealed class EvasionSource : IEvasionSource
        {
            public void Collect(DataSet data, EvasionInput input, Diagnostics diagnostics)
            {
                DataFile? elites = FileOf(data, ElitesFile);
                if (elites == null)
                {
                    return;
                }
                foreach (var (baseRow, hasBase, row, at, id) in Pairs(data))
                {
                    if (row.TryGetProperty("attacks", out JsonElement attacks) && attacks.ValueKind == JsonValueKind.Array)
                    {
                        int j = 0;
                        foreach (JsonElement ar in attacks.EnumerateArray())
                        {
                            if (ar.TryGetProperty("attack", out JsonElement attackRow))
                            {
                                EvasionAttack? a = EnemiesEvasionSource.ReadAttack(attackRow);
                                if (a != null)
                                {
                                    a.File = elites.RepoPath;
                                    a.Pointer = at + "/attacks/" + Num(j) + "/attack";
                                    a.Owner = id;
                                    input.Attacks.Add(a);
                                }
                            }
                            j++;
                        }
                    }
                    EvasionAttack? twin = hasBase ? RepeatedRing(baseRow, row) : null;
                    if (twin != null)
                    {
                        twin.File = elites.RepoPath;
                        twin.Pointer = at + "/params";
                        twin.Owner = id;
                        input.Attacks.Add(twin);
                    }
                }
            }

            // 바탕 고리 탄(count 발, spread 도 간격)을 ring_repeat 번, 매번 ring_repeat_angle 도 어긋나게 쏠 때 모든 탄의 이웃 사이 가장 작은 각을
            // 두 발짜리 탄으로 넘긴다(EvasionCheck 가 3칸 지점 호 길이 ≥ 1.0칸을 본다)
            private static EvasionAttack? RepeatedRing(JsonElement baseRow, JsonElement row)
            {
                if (!row.TryGetProperty("params", out JsonElement p) || !p.TryGetProperty("ring_repeat", out JsonElement rr)
                    || !rr.TryGetInt32(out int repeat) || repeat < 2)
                {
                    return null;
                }
                decimal offset = EnemiesConverter.DecOr(p, "ring_repeat_angle_degrees");
                List<JsonElement> baseAttacks = AttacksOf(baseRow);
                int ring = FirstOfKind(baseAttacks, "ring_shot");
                if (ring < 0)
                {
                    return null;                      // CrossCheck 가 알린다
                }
                EvasionAttack? a = EnemiesEvasionSource.ReadAttack(baseAttacks[ring]);
                if (a?.Proj == null)
                {
                    return null;
                }
                decimal gap = CombinedGapDegrees(a.Proj.SpreadDegrees, a.Proj.Count, offset, repeat);
                a.Key = Str(baseAttacks[ring], "key") + " × ring_repeat " + Num(repeat);
                a.Proj = new EvasionProj { SpeedMilli = a.Proj.SpeedMilli, Count = 2, SpreadDegrees = gap };
                return a;
            }

            // {i × spread + k × offset (mod 360) | i < count, k < repeat} 의 이웃 사이 가장 작은 각(도)
            public static decimal CombinedGapDegrees(decimal spread, int count, decimal offset, int repeat)
            {
                var angles = new List<decimal>();
                for (int k = 0; k < repeat; k++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        angles.Add(decimal.Remainder(spread * i + offset * k, EvasionMath.FullTurnDegrees));
                    }
                }
                angles.Sort();                         // decimal 의 전순서
                decimal min = angles[0] + EvasionMath.FullTurnDegrees - angles[angles.Count - 1];
                for (int i = 1; i < angles.Count; i++)
                {
                    decimal g = angles[i] - angles[i - 1];
                    if (g < min)
                    {
                        min = g;
                    }
                }
                return min;
            }
        }
    }
}
