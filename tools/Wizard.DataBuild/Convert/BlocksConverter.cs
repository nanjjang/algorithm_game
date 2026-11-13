using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/blocks.json → 팩 절 2 blocks. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Blocks.cs.
    // 스키마: data/schema/blocks.schema.json(꼴과 범위는 스키마가 이미 보았다).
    //   id      → BlockId(spec 14 3.3, ContentIds)         kind → BlockKind(projectile 1, control 2, modifier 3)
    //   rarity  → Rarity(common 1, rare 2, hero 3)          element → Element(ContentIds)
    // 단위(spec 02 4.2, spec 01 2.4·2.5, CONTRACTS 0.3. 반올림은 가장 가까운 정수, 같으면 짝수):
    //   열 → HU(× 120), 배율 → 천분율(× 1000), 피해 → Fix64 Raw(× 2^32), 초 → 틱(× 60), 칸/초 → 틱당 Raw(× 2^32 ÷ 60), 칸 → Raw,
    //   % → 천분율(× 10), 도 → 센티도(× 100), 도/초 → 틱당 센티도(× 100 ÷ 60)
    //   flags  → ProjectileFlags(explodes 1, chains 2, returns 4, pass_through 8, ignores_wolf_dodge 16)
    //   effect → ModifierEffect(none 0, bounce 1, pierce 2, size 3, homing 4, frost 5, triple_heat 6, corrosion 7)
    //   params → BlockParams(DECISIONS-TECH T-53). 원본에 없는 값은 0 이다
    // 교차 검사(spec 02 4.2, T-53): 즉발이면 속도 0, 투사체만 피해·간격·속도·사거리·반지름과 표식을 갖는다, 변형만 효과와 피해 보너스를 갖는다,
    //   제어 블록은 분열(갈래 ≥ 2, 열 배수 > 1, 부채꼴 반각)이거나 적중 시 발동(갈래 1, 열 배수 1)이다, 표식·효과와 그 값(params)은 짝이 맞는다,
    //   전이 배율은 코어의 표(PowBase.Chain07)와 같다.
    // spell_rules(T-57. spec 02 SF-2, HT-5, HT-7) → 절의 꼬리 SpellRulesDef: 상한(정수), 열 → HU, 배수 → 천분율. 원본에 없으면 꼬리를 쓰지 않는다.
    public sealed class BlocksConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Blocks;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Entry>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("blocks").EnumerateArray())
            {
                string at = "/blocks/" + i.ToString(CultureInfo.InvariantCulture);
                Entry? e = Read(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = entries.Count;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: BlockId 오름차순(원본의 나열 순서에 기대지 않는다). 겹침은 오류로 막으므로 쓰는 바이트의 순서는 하나다.
            // 같은 id 끼리는 원본 순서로 둔다(진단이 늘 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.Id != b.Id ? ((byte)a.Id).CompareTo((byte)b.Id) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Id == entries[k - 1].Id)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "블록 '" + ContentIds.ToId(entries[k].Id) + "' 가 두 번 있다");
                }
            }
            bool hasRules = file.Root.TryGetProperty("spell_rules", out JsonElement rules);
            int treeCap = 0, underlineMaxCutHu = 0, mirrorHeatMulPermille = 0;
            if (hasRules)
            {
                treeCap = Int(rules, "tree_projectile_cap");
                underlineMaxCutHu = Units.HeatToHu(Dec(rules, "underline_max_cut_heat"));
                mirrorHeatMulPermille = Units.RatioToPermille(Dec(rules, "mirror_heat_multiplier"));
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);       // id 가 겹치지 않으므로 BlockId 의 값 수 이하다
            foreach (Entry e in entries)
            {
                Write(e, output);
            }
            if (hasRules)
            {
                // 꼬리. 순서는 읽는 쪽(ContentDb.Blocks.cs)의 머리 주석과 같다. 범위는 스키마가 보았다
                output.I32(treeCap);
                output.I32(underlineMaxCutHu);
                output.I32(mirrorHeatMulPermille);
            }
        }

        private sealed class Entry
        {
            public BlockId Id;
            public BlockKind Kind;
            public Rarity Rarity;
            public Element Element;
            public int HeatHu, HeatMulPermille, SplitCount, IntervalTicks, DamageBonusPermille;
            public long BaseDamageRaw, SpeedPerTickRaw, RangeRaw, HitRadiusRaw;
            public bool IsInstant;
            public ProjectileFlags Flags;
            public ModifierEffect Effect;
            public long ExplosionRadiusRaw, ChainRadiusRaw, ReturnArriveRadiusRaw, ReturnMaxDistanceRaw, HomingRangeRaw;
            public int ChainHops, ChainHopMulPermille, SlowPermille, SlowTicks, DamageTakenBonusPermille, DamageTakenBonusTicks;
            public int FanHalfAngleCenti, BounceCount, PierceCount, RadiusMulPermille, RadiusMulCapPermille;
            public int HomingTurnCentiPerTick, HomingConeHalfCenti, CritMultiplier;
            public string Pointer = "";
            public int Order;                         // 원본에서의 순서
        }

        private static Entry? Read(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            var e = new Entry { Pointer = at };
            bool ok = true;
            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParse(id, out e.Id) || e.Id == BlockId.None)
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 블록 id(spec 14 3.3)가 아니다");
                ok = false;
            }
            string element = row.GetProperty("element").GetString() ?? "";
            if (!ContentIds.TryParse(element, out e.Element))
            {
                diagnostics.Error(file.RepoPath, at + "/element", "'" + element + "' 는 속성 id(spec 14 3.1)가 아니다");
                ok = false;
            }
            e.Kind = Kind(row.GetProperty("kind").GetString());
            e.Rarity = RarityOf(row.GetProperty("rarity").GetString());
            e.HeatHu = Units.HeatToHu(Dec(row, "heat"));
            e.HeatMulPermille = Units.RatioToPermille(Dec(row, "heat_multiplier"));
            e.SplitCount = Int(row, "split_count");
            e.BaseDamageRaw = FixRaw(Dec(row, "base_damage"));
            e.IntervalTicks = Units.SecondsToTicks(Dec(row, "interval_seconds"));
            e.SpeedPerTickRaw = Units.CellsPerSecondToRawPerTick(Dec(row, "speed"));
            e.IsInstant = row.GetProperty("instant").GetBoolean();
            e.RangeRaw = Units.CellsToRaw(Dec(row, "range"));
            e.HitRadiusRaw = Units.CellsToRaw(Dec(row, "hit_radius"));
            e.DamageBonusPermille = Units.RatioToPermille(Dec(row, "damage_bonus"));
            e.Flags = FlagsOf(row.GetProperty("flags"));
            e.Effect = EffectOf(row.GetProperty("effect").GetString());
            if (row.TryGetProperty("params", out JsonElement p))
            {
                ReadParams(p, e);
            }
            // spec 02 4.2: 즉발이면 속도 0 + IsInstant
            if (e.IsInstant && e.SpeedPerTickRaw != 0)
            {
                diagnostics.Error(file.RepoPath, at + "/speed", "즉발 블록의 speed 는 0 이다(spec 02 4.2)");
                ok = false;
            }
            // spec 02 4.2: 피해·간격·속도·사거리·반지름은 "투사체만" 갖는다. 제어·변형 블록이 값을 가지면 어긋난 행이다
            if (e.Kind == BlockKind.Projectile)
            {
                if (e.BaseDamageRaw <= 0 || e.IntervalTicks <= 0 || e.RangeRaw <= 0 || e.HitRadiusRaw <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/kind", "투사체 블록의 피해·간격·사거리·반지름은 0 보다 커야 한다(spec 02 4.2)");
                    ok = false;
                }
                if (!e.IsInstant && e.SpeedPerTickRaw <= 0)
                {
                    diagnostics.Error(file.RepoPath, at + "/speed", "즉발이 아닌 투사체 블록의 speed 는 0 보다 커야 한다(spec 02 4.2)");
                    ok = false;
                }
            }
            else if (e.BaseDamageRaw != 0 || e.IntervalTicks != 0 || e.SpeedPerTickRaw != 0 || e.RangeRaw != 0 || e.HitRadiusRaw != 0 || e.IsInstant)
            {
                diagnostics.Error(file.RepoPath, at + "/kind", "제어·변형 블록은 피해·간격·속도·사거리·반지름을 0 으로 둔다(spec 02 4.2 \"투사체만\")");
                ok = false;
            }
            if (!CheckShape(e, at, file, diagnostics))
            {
                ok = false;
            }
            return ok ? e : null;
        }

        // 원본에 없는 값은 0 으로 남는다. 범위(0 보다 크다 등)는 스키마가 이미 보았다
        private static void ReadParams(JsonElement p, Entry e)
        {
            if (p.TryGetProperty("explosion_radius", out JsonElement v)) { e.ExplosionRadiusRaw = Units.CellsToRaw(v.GetDecimal()); }
            if (p.TryGetProperty("chain_hops", out v)) { e.ChainHops = Int(p, "chain_hops"); }
            if (p.TryGetProperty("chain_radius", out v)) { e.ChainRadiusRaw = Units.CellsToRaw(v.GetDecimal()); }
            if (p.TryGetProperty("chain_hop_multiplier", out v)) { e.ChainHopMulPermille = Units.RatioToPermille(v.GetDecimal()); }
            if (p.TryGetProperty("return_arrive_radius", out v)) { e.ReturnArriveRadiusRaw = Units.CellsToRaw(v.GetDecimal()); }
            if (p.TryGetProperty("return_max_distance", out v)) { e.ReturnMaxDistanceRaw = Units.CellsToRaw(v.GetDecimal()); }
            if (p.TryGetProperty("slow_percent", out v)) { e.SlowPermille = Units.PercentToPermille(v.GetDecimal()); }
            if (p.TryGetProperty("slow_seconds", out v)) { e.SlowTicks = Units.SecondsToTicks(v.GetDecimal()); }
            if (p.TryGetProperty("damage_taken_bonus_percent", out v)) { e.DamageTakenBonusPermille = Units.PercentToPermille(v.GetDecimal()); }
            if (p.TryGetProperty("damage_taken_bonus_seconds", out v)) { e.DamageTakenBonusTicks = Units.SecondsToTicks(v.GetDecimal()); }
            if (p.TryGetProperty("fan_half_angle_degrees", out v)) { e.FanHalfAngleCenti = Units.DegreesToCenti(v.GetDecimal()); }
            if (p.TryGetProperty("bounce_count", out v)) { e.BounceCount = Int(p, "bounce_count"); }
            if (p.TryGetProperty("pierce_count", out v)) { e.PierceCount = Int(p, "pierce_count"); }
            if (p.TryGetProperty("radius_multiplier", out v)) { e.RadiusMulPermille = Units.RatioToPermille(v.GetDecimal()); }
            if (p.TryGetProperty("radius_multiplier_cap", out v)) { e.RadiusMulCapPermille = Units.RatioToPermille(v.GetDecimal()); }
            if (p.TryGetProperty("homing_turn_degrees_per_second", out v)) { e.HomingTurnCentiPerTick = DegreesPerSecondToCentiPerTick(v.GetDecimal()); }
            if (p.TryGetProperty("homing_cone_half_degrees", out v)) { e.HomingConeHalfCenti = Units.DegreesToCenti(v.GetDecimal()); }
            if (p.TryGetProperty("homing_range", out v)) { e.HomingRangeRaw = Units.CellsToRaw(v.GetDecimal()); }
            if (p.TryGetProperty("crit_multiplier", out v)) { e.CritMultiplier = Int(p, "crit_multiplier"); }
        }

        // 종류·표식·효과와 값의 짝(spec 02 4.2, T-53). 어긋난 행은 읽는 코드가 조용히 무시하게 되므로 여기서 막는다
        private static bool CheckShape(Entry e, string at, DataFile file, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            string path = file.RepoPath;
            bool projectile = e.Kind == BlockKind.Projectile, control = e.Kind == BlockKind.Control, modifier = e.Kind == BlockKind.Modifier;
            bool split = control && e.SplitCount > 1;

            if (!projectile && e.Flags != ProjectileFlags.None)
            {
                diagnostics.Error(path, at + "/flags", "표식(flags)은 투사체 블록만 갖는다(spec 02 4.2)");
            }
            if (modifier != (e.Effect != ModifierEffect.None))
            {
                diagnostics.Error(path, at + "/effect", "변형 블록은 효과(effect)가 하나 있고, 그 밖의 블록은 none 이다(spec 02 4.2)");
            }
            if (!modifier && e.DamageBonusPermille != 0)
            {
                diagnostics.Error(path, at + "/damage_bonus", "피해 보너스는 변형 블록만 갖는다(spec 02 4.2)");
            }
            // 제어 블록: 분열(갈래 ≥ 2, 열 배수 > 1)이거나 적중 시 발동(갈래 1, 열 배수 1). 열은 더하지 않는다
            if (control && e.HeatHu != 0)
            {
                diagnostics.Error(path, at + "/heat", "제어 블록은 열을 더하지 않는다(spec 02 4.2)");
            }
            if (!control && e.SplitCount != 1)
            {
                diagnostics.Error(path, at + "/split_count", "갈래 수는 분열 블록만 2 이상이다(spec 02 4.2)");
            }
            if (split != (e.HeatMulPermille != Units.PermillePerUnit) || e.HeatMulPermille < Units.PermillePerUnit)
            {
                diagnostics.Error(path, at + "/heat_multiplier", "열 배수는 분열 블록만 1 보다 크고 그 밖은 1 이다(spec 02 4.2)");
            }
            Pair(split, e.FanHalfAngleCenti > 0, path, at, "fan_half_angle_degrees", "분열 블록", diagnostics);

            bool explodes = (e.Flags & ProjectileFlags.Explodes) != 0, chains = (e.Flags & ProjectileFlags.Chains) != 0, returns = (e.Flags & ProjectileFlags.Returns) != 0;
            Pair(explodes, e.ExplosionRadiusRaw > 0, path, at, "explosion_radius", "표식 explodes", diagnostics);
            Pair(chains, e.ChainHops > 0, path, at, "chain_hops", "표식 chains", diagnostics);
            Pair(chains, e.ChainRadiusRaw > 0, path, at, "chain_radius", "표식 chains", diagnostics);
            Pair(chains, e.ChainHopMulPermille > 0, path, at, "chain_hop_multiplier", "표식 chains", diagnostics);
            Pair(returns, e.ReturnArriveRadiusRaw > 0, path, at, "return_arrive_radius", "표식 returns", diagnostics);
            Pair(returns, e.ReturnMaxDistanceRaw > 0, path, at, "return_max_distance", "표식 returns", diagnostics);
            // 전이 피해는 코어의 거듭제곱 표로 계산한다(spec 02 18절 U18). 원본의 값이 표의 밑과 다르면 표시와 계산이 갈린다
            if (e.ChainHopMulPermille > 0 && FixRaw((decimal)e.ChainHopMulPermille / Units.PermillePerUnit) != FixMath.PowTable(PowBase.Chain07, 1).Raw)
            {
                diagnostics.Error(path, at + "/params/chain_hop_multiplier", "전이 배율이 코어의 표(PowBase.Chain07)와 다르다. 값을 바꾸려면 표(tools/Wizard.TableGen)부터 바꾼다");
            }

            // 감속: 얼음 송곳(투사체)과 서리(변형). 잠식: 변형만
            bool frost = e.Effect == ModifierEffect.Frost;
            if (frost && e.SlowPermille == 0)
            {
                diagnostics.Error(path, at + "/params/slow_percent", "효과 frost 에는 slow_percent 가 있어야 한다(spec 02 4.2)");
            }
            if (e.SlowPermille > 0 && !projectile && !frost)
            {
                diagnostics.Error(path, at + "/params/slow_percent", "slow_percent 는 투사체 블록과 효과 frost 만 갖는다(spec 02 4.2)");
            }
            if ((e.SlowPermille > 0) != (e.SlowTicks > 0))
            {
                diagnostics.Error(path, at + "/params/slow_seconds", "slow_percent 와 slow_seconds 는 같이 있어야 한다");
            }
            bool corrosion = e.Effect == ModifierEffect.Corrosion;
            Pair(corrosion, e.DamageTakenBonusPermille > 0, path, at, "damage_taken_bonus_percent", "효과 corrosion", diagnostics);
            Pair(corrosion, e.DamageTakenBonusTicks > 0, path, at, "damage_taken_bonus_seconds", "효과 corrosion", diagnostics);

            Pair(e.Effect == ModifierEffect.Bounce, e.BounceCount > 0, path, at, "bounce_count", "효과 bounce", diagnostics);
            Pair(e.Effect == ModifierEffect.Pierce, e.PierceCount > 0, path, at, "pierce_count", "효과 pierce", diagnostics);
            bool size = e.Effect == ModifierEffect.Size;
            Pair(size, e.RadiusMulPermille > 0, path, at, "radius_multiplier", "효과 size", diagnostics);
            Pair(size, e.RadiusMulCapPermille > 0, path, at, "radius_multiplier_cap", "효과 size", diagnostics);
            if (e.RadiusMulCapPermille < e.RadiusMulPermille)
            {
                diagnostics.Error(path, at + "/params/radius_multiplier_cap", "반지름 배수의 상한이 배수보다 작다");
            }
            bool homing = e.Effect == ModifierEffect.Homing;
            Pair(homing, e.HomingTurnCentiPerTick > 0, path, at, "homing_turn_degrees_per_second", "효과 homing", diagnostics);
            Pair(homing, e.HomingConeHalfCenti > 0, path, at, "homing_cone_half_degrees", "효과 homing", diagnostics);
            Pair(homing, e.HomingRangeRaw > 0, path, at, "homing_range", "효과 homing", diagnostics);
            Pair(e.Effect == ModifierEffect.TripleHeat, e.CritMultiplier > 0, path, at, "crit_multiplier", "효과 triple_heat", diagnostics);
            return diagnostics.Count == before;
        }

        // owner(표식·효과·종류)와 값은 짝이다: 주인이 있으면 값이 있고, 주인이 없으면 값도 없다
        private static void Pair(bool owner, bool has, string path, string at, string key, string ownerName, Diagnostics diagnostics)
        {
            if (owner && !has)
            {
                diagnostics.Error(path, at + "/params/" + key, ownerName + " 에는 " + key + " 가 있어야 한다(spec 02 4.2)");
            }
            else if (!owner && has)
            {
                diagnostics.Error(path, at + "/params/" + key, key + " 는 " + ownerName + " 만 갖는다(spec 02 4.2)");
            }
        }

        // 스키마의 enum 과 uniqueItems 가 이미 거른 값이다
        private static ProjectileFlags FlagsOf(JsonElement array)
        {
            ProjectileFlags flags = ProjectileFlags.None;
            foreach (JsonElement f in array.EnumerateArray())
            {
                switch (f.GetString())
                {
                    case "explodes": flags |= ProjectileFlags.Explodes; break;
                    case "chains": flags |= ProjectileFlags.Chains; break;
                    case "returns": flags |= ProjectileFlags.Returns; break;
                    case "pass_through": flags |= ProjectileFlags.PassThrough; break;
                    case "ignores_wolf_dodge": flags |= ProjectileFlags.IgnoresWolfDodge; break;
                    default: throw new DataBuildException("모르는 투사체 표식 '" + f.GetString() + "'");
                }
            }
            return flags;
        }

        private static ModifierEffect EffectOf(string? s)
        {
            switch (s)
            {
                case "none": return ModifierEffect.None;
                case "bounce": return ModifierEffect.Bounce;
                case "pierce": return ModifierEffect.Pierce;
                case "size": return ModifierEffect.Size;
                case "homing": return ModifierEffect.Homing;
                case "frost": return ModifierEffect.Frost;
                case "triple_heat": return ModifierEffect.TripleHeat;
                case "corrosion": return ModifierEffect.Corrosion;
                default: throw new DataBuildException("모르는 변형 효과 '" + s + "'");
            }
        }

        // 도/초 → 틱당 센티도(× 100 ÷ 60). 초당 180도 → 300
        private static int DegreesPerSecondToCentiPerTick(decimal degreesPerSecond)
        {
            decimal r = Units.RoundHalfEven(degreesPerSecond, Units.CentiPerDegree, Units.TicksPerSecond);
            if (r < int.MinValue || r > int.MaxValue)
            {
                throw new DataBuildException("도/초 값 " + degreesPerSecond.ToString(CultureInfo.InvariantCulture) + " 을 바꾼 정수가 32비트 범위를 넘는다");
            }
            return (int)r;
        }

        // 스키마의 enum 이 이미 거른 값이다
        private static BlockKind Kind(string? s)
        {
            switch (s)
            {
                case "projectile": return BlockKind.Projectile;
                case "control": return BlockKind.Control;
                case "modifier": return BlockKind.Modifier;
                default: throw new DataBuildException("모르는 블록 종류 '" + s + "'");
            }
        }

        private static Rarity RarityOf(string? s)
        {
            switch (s)
            {
                case "common": return Rarity.Common;
                case "rare": return Rarity.Rare;
                case "hero": return Rarity.Hero;
                default: throw new DataBuildException("모르는 등급 '" + s + "'");
            }
        }

        // Fix64 의 값(칸이 아닌 수, 예: 피해) → Raw(× 2^32)
        private static long FixRaw(decimal v)
        {
            decimal raw = Units.RoundHalfEven(v, Fix64.OneRaw, 1);
            if (raw < long.MinValue || raw > long.MaxValue)
            {
                throw new DataBuildException("값 " + v.ToString(CultureInfo.InvariantCulture) + " 을 바꾼 Raw 가 64비트 범위를 넘는다");
            }
            return (long)raw;
        }

        private static decimal Dec(JsonElement o, string name) => o.GetProperty(name).GetDecimal();

        // 스키마의 "integer" 는 2.0 도 받으므로 decimal 로 읽어 정수인지 본다
        private static int Int(JsonElement o, string name)
        {
            decimal v = o.GetProperty(name).GetDecimal();
            if (v != decimal.Truncate(v) || v < int.MinValue || v > int.MaxValue)
            {
                throw new DataBuildException(name + " 값 " + v.ToString(CultureInfo.InvariantCulture) + " 은 32비트 정수가 아니다");
            }
            return (int)v;
        }

        private static void Write(Entry e, ByteWriter w)
        {
            w.U8((byte)e.Id);
            w.U8((byte)e.Kind);
            w.U8((byte)e.Rarity);
            w.U8((byte)e.Element);
            w.I32(e.HeatHu);
            w.I32(e.HeatMulPermille);
            w.I32(e.SplitCount);
            w.I64(e.BaseDamageRaw);
            w.I32(e.IntervalTicks);
            w.I64(e.SpeedPerTickRaw);
            w.U8(e.IsInstant ? (byte)1 : (byte)0);
            w.I64(e.RangeRaw);
            w.I64(e.HitRadiusRaw);
            w.I32(e.DamageBonusPermille);
            w.U16((ushort)e.Flags);
            w.U8((byte)e.Effect);
            // BlockParams. 순서는 읽는 쪽(ContentDb.Blocks.cs)의 머리 주석과 같다
            w.I64(e.ExplosionRadiusRaw);
            w.I32(e.ChainHops);
            w.I64(e.ChainRadiusRaw);
            w.I32(e.ChainHopMulPermille);
            w.I64(e.ReturnArriveRadiusRaw);
            w.I64(e.ReturnMaxDistanceRaw);
            w.I32(e.SlowPermille);
            w.I32(e.SlowTicks);
            w.I32(e.DamageTakenBonusPermille);
            w.I32(e.DamageTakenBonusTicks);
            w.I32(e.FanHalfAngleCenti);
            w.I32(e.BounceCount);
            w.I32(e.PierceCount);
            w.I32(e.RadiusMulPermille);
            w.I32(e.RadiusMulCapPermille);
            w.I32(e.HomingTurnCentiPerTick);
            w.I32(e.HomingConeHalfCenti);
            w.I64(e.HomingRangeRaw);
            w.I32(e.CritMultiplier);
        }
    }
}
