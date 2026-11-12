using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/enemies.json → 팩 절 5 enemies. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Enemies.cs.
    // 스키마: data/schema/enemies.schema.json(꼴과 범위는 스키마가 이미 보았다).
    //   id → EnemyTypeId(spec 14 3.6, ContentIds)    weak·resist → Element    tags → EnemyTag(tag.*)    times → TimeMask(tod.*)
    //   role·kind·shape·lock·anchor·damage_source → EnemyDef·AttackDef 안의 열거형(spec 05 2.1·2.3 선언 순서)
    // 단위(spec 05 0.3, CONTRACTS 0.3. 반올림은 가장 가까운 정수, 같으면 짝수):
    //   칸 → Raw(× 2^32), 체력 → Fix64 Raw, 이동·돌진 속도 → 센티 칸/초(× 100), 탄속 → 틱당 Raw(× 2^32 ÷ 60),
    //   초 → 틱(× 60), 도 → 센티도(× 100), 배율 → 천분율(× 1000)
    // 이 절 안에서 보는 규칙(spec 05 2.2·2.3·3.6·3.7·3.10): 겹치는 id·공격 열쇠, 약점 ≠ 저항(None 아님), 넉백 면역 = 중량·뿌리,
    // 엘리트·보스는 바탕 꼬리표가 아님, 보스 웨이브에는 일반 적이 없음, 모양 ⇔ 예고 > 0, 모양마다 필요한 크기, 고정 방식과 n,
    // 접촉 공격 ⇔ 접촉 피해, 돌진 ⇔ is_dash(띠, 길이, 속도 × 시간 = 길이, 스침 대상), 탄 ⇔ 탄 피해(피해 1, 스침 대상).
    // 회피 검산(R1~R4)과 속성 분배는 IDataValidator(tools/Wizard.DataBuild/Validate/, M3-BT-01)가 본다.
    public sealed class EnemiesConverter : ISectionConverter
    {
        private const int MaxAttacks = 16;            // 스키마의 maxItems 와 같다(절의 u8 개수 안)

        public ContentSection Section => ContentSection.Enemies;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Enemy>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("enemies").EnumerateArray())
            {
                string at = "/enemies/" + Num(i);
                Enemy? e = ReadEnemy(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = entries.Count;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: EnemyTypeId 오름차순(원본의 나열 순서에 기대지 않는다). 같은 id 끼리는 원본 순서(진단이 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.Id != b.Id ? ((ushort)a.Id).CompareTo((ushort)b.Id) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Id == entries[k - 1].Id)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "적 '" + ContentIds.ToId(entries[k].Id) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);       // id 가 겹치지 않으므로 EnemyTypeId 의 값 수 이하다
            foreach (Enemy e in entries)
            {
                WriteEnemy(e, output);
            }
        }

        private sealed class Enemy
        {
            public EnemyTypeId Id;
            public EnemyDef.EnemyRole Role;
            public long BaseHpRaw, BodyRadiusRaw;
            public int MoveSpeedCenti, AttackDamage, ContactDamage, Cost, FirstWave;
            public EnemyDef.TimeMask Times;
            public Element Weak, Resist;
            public EnemyTag Tags;
            public bool KnockbackImmune;
            public int? SpeciesCap;
            public List<Attack> Attacks = new List<Attack>();
            public Ai Ai = new Ai();
            public string Pointer = "";
            public int Order;                         // 원본에서의 순서
        }

        private sealed class Attack
        {
            public AttackDef.AttackKind Kind;
            public AttackDef.TelegraphShape? Shape;
            public long RadiusRaw, InnerRadiusRaw, WidthRaw;
            public long? LengthRaw, RangeRaw, TriggerMinRaw, TriggerMaxRaw;
            public int FanCenti, CastTicks, TelegraphTicks, LockTicks, ActiveTicks, DashSpeedCenti, RecoveryTicks, Damage, CooldownTicks;
            public AttackDef.LockMode Lock;
            public AttackDef.AnchorMode Anchor;
            public AttackDef.DamageSource Source;
            public Proj? Proj;
            public bool Grazeable, IsDash, Lob;
            public int? EscapeOverrideMilli;
        }

        private sealed class Proj
        {
            public long SpeedPerTickRaw, RangeRaw, HitRadiusRaw;
            public int Count, SpreadCenti;
        }

        private sealed class Ai
        {
            public long DodgeDistanceRaw, CoverOffsetRaw, KeepMinRaw, KeepMaxRaw, KiteMinRaw;
            public int DodgeCooldownTicks, DodgeWindowTicks, DodgeDashTicks, ArmorHalfAngleCenti, ArmorDamagePermille, CoverHpPermille;
        }

        private static Enemy? ReadEnemy(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            var e = new Enemy { Pointer = at };
            bool ok = true;
            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParse(id, out e.Id) || e.Id == EnemyTypeId.None)
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 적 id(spec 14 3.6)가 아니다");
                ok = false;
            }
            e.Role = Role(row.GetProperty("role").GetString());
            e.BaseHpRaw = FixRaw(Dec(row, "base_hp"));
            e.MoveSpeedCenti = Units.ToCenti(Dec(row, "move_speed"));
            e.AttackDamage = Int(row, "attack_damage");
            e.ContactDamage = Int(row, "contact_damage");
            e.Cost = Int(row, "cost");
            e.FirstWave = Int(row, "first_wave");

            int t = 0;
            foreach (JsonElement tod in row.GetProperty("times").EnumerateArray())
            {
                string s = tod.GetString() ?? "";
                string p = at + "/times/" + Num(t);
                if (!ContentIds.TryParse(s, out TimeOfDay v))
                {
                    diagnostics.Error(file.RepoPath, p, "'" + s + "' 는 시간대 id(spec 14 3.9)가 아니다");
                    ok = false;
                }
                else if (v == TimeOfDay.BossWave)
                {
                    diagnostics.Error(file.RepoPath, p, "보스 웨이브에는 일반 적이 나오지 않는다(spec 05 3.12)");
                    ok = false;
                }
                else
                {
                    e.Times |= (EnemyDef.TimeMask)(1 << ((int)v - 1));
                }
                t++;
            }

            ok &= ReadElement(row, "weak", at, file, diagnostics, out e.Weak);
            ok &= ReadElement(row, "resist", at, file, diagnostics, out e.Resist);
            if (ok && e.Weak == e.Resist)
            {
                diagnostics.Error(file.RepoPath, at + "/resist", "약점과 저항이 같다(spec 05 2.2 weak ≠ resist)");
                ok = false;
            }

            int g = 0;
            foreach (JsonElement tag in row.GetProperty("tags").EnumerateArray())
            {
                string s = tag.GetString() ?? "";
                string p = at + "/tags/" + Num(g);
                if (!ContentIds.TryParse(s, out EnemyTag v))
                {
                    diagnostics.Error(file.RepoPath, p, "'" + s + "' 는 꼬리표 id(spec 14 3.9)가 아니다");
                    ok = false;
                }
                else if (v == EnemyTag.Elite || v == EnemyTag.Boss)
                {
                    diagnostics.Error(file.RepoPath, p, "엘리트·보스는 종의 바탕 꼬리표가 아니다(spec 05 2.5: 엘리트는 개체 상태, 보스는 보스 절)");
                    ok = false;
                }
                else
                {
                    e.Tags |= v;
                }
                g++;
            }
            e.KnockbackImmune = row.GetProperty("knockback_immune").GetBoolean();
            bool tagImmune = (e.Tags & (EnemyTag.Heavy | EnemyTag.Rooted)) != EnemyTag.None;
            if (ok && e.KnockbackImmune != tagImmune)
            {
                diagnostics.Error(file.RepoPath, at + "/knockback_immune", "넉백 면역은 중량·뿌리 꼬리표가 정한다(spec 05 2.2). 꼬리표와 다르다");
                ok = false;
            }

            JsonElement cap = row.GetProperty("species_cap");
            e.SpeciesCap = cap.ValueKind == JsonValueKind.Null ? (int?)null : IntOf(cap, "species_cap");
            e.BodyRadiusRaw = Units.CellsToRaw(Dec(row, "body_radius"));

            int a = 0;
            var keys = new List<string>();
            bool hasContact = false;
            foreach (JsonElement ar in row.GetProperty("attacks").EnumerateArray())
            {
                string p = at + "/attacks/" + Num(a);
                string key = ar.GetProperty("key").GetString() ?? "";
                if (keys.Contains(key))
                {
                    diagnostics.Error(file.RepoPath, p + "/key", "공격 열쇠 '" + key + "' 가 이 적에 두 번 있다");
                    ok = false;
                }
                keys.Add(key);
                Attack? x = ReadAttack(ar, p, e, file, diagnostics);
                if (x == null)
                {
                    ok = false;
                }
                else
                {
                    hasContact |= x.Kind == AttackDef.AttackKind.Contact;
                    e.Attacks.Add(x);
                }
                a++;
            }
            if (a > MaxAttacks)
            {
                throw new DataBuildException("공격이 " + Num(MaxAttacks) + " 개를 넘는다(스키마가 막는다)");
            }
            if (ok && hasContact != (e.ContactDamage > 0))
            {
                diagnostics.Error(file.RepoPath, at + "/contact_damage",
                    "접촉 피해가 있으면 contact 공격이 하나 있고, 없으면 contact 공격이 없다(spec 05 2.2, 3.6)");
                ok = false;
            }

            if (row.TryGetProperty("ai", out JsonElement ai))
            {
                ReadAi(ai, e.Ai);
            }
            return ok ? e : null;
        }

        private static Attack? ReadAttack(JsonElement row, string at, Enemy owner, DataFile file, Diagnostics diagnostics)
        {
            var x = new Attack();
            bool ok = true;
            x.Kind = Kind(row.GetProperty("kind").GetString());
            x.Shape = OptionalString(row, "shape") is string shape ? Shape(shape) : (AttackDef.TelegraphShape?)null;
            x.RadiusRaw = Units.CellsToRaw(DecOr(row, "radius"));
            x.InnerRadiusRaw = Units.CellsToRaw(DecOr(row, "inner_radius"));
            x.WidthRaw = Units.CellsToRaw(DecOr(row, "width"));
            x.LengthRaw = OptionalCells(row, "length");
            x.FanCenti = Units.DegreesToCenti(DecOr(row, "fan_degrees"));
            x.RangeRaw = OptionalCells(row, "range");
            x.TriggerMinRaw = OptionalCells(row, "trigger_min");
            x.TriggerMaxRaw = OptionalCells(row, "trigger_max");
            x.CastTicks = Units.SecondsToTicks(DecOr(row, "cast_seconds"));
            x.TelegraphTicks = Units.SecondsToTicks(DecOr(row, "telegraph_seconds"));
            x.Lock = LockOf(OptionalString(row, "lock") ?? "at_start");
            x.LockTicks = Units.SecondsToTicks(DecOr(row, "lock_seconds"));
            x.Anchor = AnchorOf(OptionalString(row, "anchor") ?? "self");
            x.ActiveTicks = row.TryGetProperty("active_ticks", out JsonElement active) ? IntOf(active, "active_ticks") : 0;
            decimal dashSpeed = DecOr(row, "dash_speed");
            x.DashSpeedCenti = Units.ToCenti(dashSpeed);
            x.RecoveryTicks = Units.SecondsToTicks(DecOr(row, "recovery_seconds"));
            x.Damage = Int(row, "damage");
            x.Source = SourceOf(row.GetProperty("damage_source").GetString());
            x.Grazeable = BoolOr(row, "grazeable");
            x.IsDash = BoolOr(row, "is_dash");
            x.Lob = BoolOr(row, "lob");
            x.EscapeOverrideMilli = row.TryGetProperty("escape_override_milli", out JsonElement esc) && esc.ValueKind != JsonValueKind.Null
                ? IntOf(esc, "escape_override_milli") : (int?)null;
            x.CooldownTicks = Units.SecondsToTicks(Dec(row, "cooldown_seconds"));
            if (row.TryGetProperty("proj", out JsonElement pj) && pj.ValueKind != JsonValueKind.Null)
            {
                x.Proj = new Proj
                {
                    SpeedPerTickRaw = Units.CellsPerSecondToRawPerTick(Dec(pj, "speed")),
                    Count = Int(pj, "count"),
                    SpreadCenti = Units.DegreesToCenti(Dec(pj, "spread_degrees")),
                    RangeRaw = Units.CellsToRaw(Dec(pj, "range")),
                    HitRadiusRaw = Units.CellsToRaw(Dec(pj, "hit_radius")),
                };
            }

            // 예고(spec 05 3.3): 모양이 있으면 예고가 있고, 없으면 없다
            if (x.Shape.HasValue != (x.TelegraphTicks > 0))
            {
                ok &= Fail(file, at + "/telegraph_seconds", "예고 모양이 있으면 예고 시간이 0 보다 크고, 없으면 0 이다(spec 05 2.3·3.3)", diagnostics);
            }
            if (x.Source == AttackDef.DamageSource.Telegraph && !x.Shape.HasValue)
            {
                ok &= Fail(file, at + "/shape", "예고 공격(damage_source telegraph)에 예고 모양이 없다", diagnostics);
            }
            ok &= CheckShape(x, at, file, diagnostics);

            // 방향 고정(spec 05 3.3 LockMode)
            if (x.Lock == AttackDef.LockMode.LastNTicks)
            {
                if (x.LockTicks <= 0 || x.LockTicks > x.TelegraphTicks)
                {
                    ok &= Fail(file, at + "/lock_seconds", "last_n_ticks 의 n 은 0 보다 크고 예고 시간 이하다(spec 05 3.3)", diagnostics);
                }
            }
            else if (x.LockTicks != 0)
            {
                ok &= Fail(file, at + "/lock_seconds", "at_start 에는 lock_seconds 가 없다(0)", diagnostics);
            }
            if (x.TriggerMinRaw.HasValue && x.TriggerMaxRaw.HasValue && x.TriggerMinRaw.Value > x.TriggerMaxRaw.Value)
            {
                ok &= Fail(file, at + "/trigger_max", "거리 조건의 위 끝이 아래 끝보다 작다", diagnostics);
            }

            // 접촉(spec 05 3.6, 3.7): 접촉 공격 ⇔ 접촉 피해 분류, 예고·탄 없음, 피해 = 종의 접촉 피해
            bool contact = x.Kind == AttackDef.AttackKind.Contact;
            if (contact != (x.Source == AttackDef.DamageSource.Contact))
            {
                ok &= Fail(file, at + "/damage_source", "contact 공격과 피해 분류 contact 는 짝이다", diagnostics);
            }
            if (contact && (x.Shape.HasValue || x.Proj != null || x.IsDash))
            {
                ok &= Fail(file, at + "/kind", "contact 공격에는 예고·탄·돌진이 없다", diagnostics);
            }
            if (contact && x.Damage != owner.ContactDamage)
            {
                ok &= Fail(file, at + "/damage", "contact 공격의 피해는 그 적의 contact_damage 와 같다(spec 05 3.6)", diagnostics);
            }

            // 돌진(spec 05 2.3 is_dash, 3.10, 4.2): 띠 모양, 길이, 속도 × 시간 = 길이, 스침 대상
            bool dash = x.Kind == AttackDef.AttackKind.Dash;
            if (dash != x.IsDash)
            {
                ok &= Fail(file, at + "/is_dash", "dash 공격과 is_dash 는 짝이다(spec 05 2.3)", diagnostics);
            }
            if (dash)
            {
                if (x.Shape != AttackDef.TelegraphShape.Band || !x.LengthRaw.HasValue)
                {
                    ok &= Fail(file, at + "/shape", "돌진의 예고는 길이가 있는 띠(band)다", diagnostics);
                }
                else if (dashSpeed <= 0m || x.ActiveTicks <= 0 || x.ActiveTicks * dashSpeed != Dec(row, "length") * Units.TicksPerSecond)
                {
                    ok &= Fail(file, at + "/active_ticks", "돌진 시간(틱) × 돌진 속도(칸/초) 는 길이(칸) × 60 과 같다", diagnostics);
                }
                if (!x.Grazeable)
                {
                    ok &= Fail(file, at + "/grazeable", "적의 돌진은 스쳐 피하기 대상이다(spec 05 1.1 P4)", diagnostics);
                }
            }
            else if (x.DashSpeedCenti != 0)
            {
                ok &= Fail(file, at + "/dash_speed", "돌진이 아닌 공격에는 돌진 속도가 없다(0)", diagnostics);
            }

            // 탄(spec 05 3.7, 3.10): 쏘는 공격 ⇔ 탄, 탄 피해 분류, 피해 1, 스침 대상
            bool shoots = x.Kind == AttackDef.AttackKind.AimedShot || x.Kind == AttackDef.AttackKind.Spread || x.Kind == AttackDef.AttackKind.RingShot;
            bool noProj = contact || dash || x.Kind == AttackDef.AttackKind.Zone;
            if (shoots && x.Proj == null)
            {
                ok &= Fail(file, at + "/proj", "탄을 쏘는 공격(" + KindName(x.Kind) + ")에 proj 가 없다", diagnostics);
            }
            if (noProj && x.Proj != null)
            {
                ok &= Fail(file, at + "/proj", KindName(x.Kind) + " 공격에는 proj 가 없다", diagnostics);
            }
            if ((x.Source == AttackDef.DamageSource.Projectile) != (x.Proj != null))
            {
                ok &= Fail(file, at + "/damage_source", "탄(proj)이 있는 공격과 피해 분류 projectile 은 짝이다", diagnostics);
            }
            if (x.Proj != null)
            {
                if (x.Damage != 1)
                {
                    ok &= Fail(file, at + "/damage", "탄의 피해는 늘 1칸이다(spec 05 3.7)", diagnostics);
                }
                if (!x.Grazeable)
                {
                    ok &= Fail(file, at + "/grazeable", "적의 탄은 스쳐 피하기 대상이다(spec 05 1.1 P4)", diagnostics);
                }
                if (x.Proj.Count == 1 && x.Proj.SpreadCenti != 0)
                {
                    ok &= Fail(file, at + "/proj/spread_degrees", "한 발이면 퍼짐 각도가 없다(0)", diagnostics);
                }
            }
            return ok ? x : null;
        }

        // 모양마다 필요한 크기(spec 05 2.3, 3.5 모양 판정)
        private static bool CheckShape(Attack x, string at, DataFile file, Diagnostics diagnostics)
        {
            bool ok = true;
            AttackDef.TelegraphShape? s = x.Shape;
            bool round = s == AttackDef.TelegraphShape.Circle || s == AttackDef.TelegraphShape.Fan || s == AttackDef.TelegraphShape.Ring;
            bool line = s == AttackDef.TelegraphShape.Band || s == AttackDef.TelegraphShape.AimLine;
            if (round && x.RadiusRaw <= 0)
            {
                ok &= Fail(file, at + "/radius", ShapeName(s) + " 예고에 반지름이 없다", diagnostics);
            }
            if (line && x.WidthRaw <= 0)
            {
                ok &= Fail(file, at + "/width", ShapeName(s) + " 예고에 폭이 없다", diagnostics);
            }
            if ((s == AttackDef.TelegraphShape.Fan) != (x.FanCenti > 0))
            {
                ok &= Fail(file, at + "/fan_degrees", "부채꼴 각도는 fan 예고에만, fan 이면 0 보다 크다", diagnostics);
            }
            if (s == AttackDef.TelegraphShape.Ring)
            {
                if (x.InnerRadiusRaw >= x.RadiusRaw)
                {
                    ok &= Fail(file, at + "/inner_radius", "고리의 안 반지름은 바깥 반지름보다 작다", diagnostics);
                }
            }
            else if (x.InnerRadiusRaw != 0)
            {
                ok &= Fail(file, at + "/inner_radius", "안 반지름은 ring 예고에만 있다", diagnostics);
            }
            if (!line && x.LengthRaw.HasValue)
            {
                ok &= Fail(file, at + "/length", "길이는 band·aim_line 예고에만 있다", diagnostics);
            }
            return ok;
        }

        private static void ReadAi(JsonElement ai, Ai a)
        {
            a.DodgeDistanceRaw = Units.CellsToRaw(DecOr(ai, "dodge_distance"));
            a.DodgeCooldownTicks = Units.SecondsToTicks(DecOr(ai, "dodge_cooldown_seconds"));
            a.DodgeWindowTicks = Units.SecondsToTicks(DecOr(ai, "dodge_window_seconds"));
            a.DodgeDashTicks = Units.SecondsToTicks(DecOr(ai, "dodge_dash_seconds"));
            a.ArmorHalfAngleCenti = Units.DegreesToCenti(DecOr(ai, "armor_half_angle_degrees"));
            a.ArmorDamagePermille = Units.RatioToPermille(DecOr(ai, "armor_damage_ratio"));
            a.CoverHpPermille = Units.RatioToPermille(DecOr(ai, "cover_hp_ratio"));
            a.CoverOffsetRaw = Units.CellsToRaw(DecOr(ai, "cover_offset"));
            a.KeepMinRaw = Units.CellsToRaw(DecOr(ai, "keep_distance_min"));
            a.KeepMaxRaw = Units.CellsToRaw(DecOr(ai, "keep_distance_max"));
            a.KiteMinRaw = Units.CellsToRaw(DecOr(ai, "kite_distance_min"));
        }

        private static bool ReadElement(JsonElement row, string name, string at, DataFile file, Diagnostics diagnostics, out Element v)
        {
            string s = row.GetProperty(name).GetString() ?? "";
            if (!ContentIds.TryParse(s, out v))
            {
                diagnostics.Error(file.RepoPath, at + "/" + name, "'" + s + "' 는 속성 id(spec 14 3.1)가 아니다");
                return false;
            }
            if (v == Element.None)
            {
                diagnostics.Error(file.RepoPath, at + "/" + name, "일반 적의 약점·저항은 무속성이 아니다(spec 05 2.2)");
                return false;
            }
            return true;
        }

        private static bool Fail(DataFile file, string pointer, string message, Diagnostics diagnostics)
        {
            diagnostics.Error(file.RepoPath, pointer, message);
            return false;
        }

        // 스키마의 enum 이 이미 거른 값이다
        private static EnemyDef.EnemyRole Role(string? s)
        {
            switch (s)
            {
                case "swarm": return EnemyDef.EnemyRole.Swarm;
                case "evasive_charger": return EnemyDef.EnemyRole.EvasiveCharger;
                case "armored_heavy": return EnemyDef.EnemyRole.ArmoredHeavy;
                case "ranged_cover": return EnemyDef.EnemyRole.RangedCover;
                case "suicide": return EnemyDef.EnemyRole.Suicide;
                case "sniper": return EnemyDef.EnemyRole.Sniper;
                case "ambush": return EnemyDef.EnemyRole.Ambush;
                case "night_swarm": return EnemyDef.EnemyRole.NightSwarm;
                case "ring_barrage": return EnemyDef.EnemyRole.RingBarrage;
                case "zone_denial": return EnemyDef.EnemyRole.ZoneDenial;
                case "long_charger": return EnemyDef.EnemyRole.LongCharger;
                case "support": return EnemyDef.EnemyRole.Support;
                default: throw new DataBuildException("모르는 적 역할 '" + s + "'");
            }
        }

        private static AttackDef.AttackKind Kind(string? s)
        {
            switch (s)
            {
                case "contact": return AttackDef.AttackKind.Contact;
                case "dash": return AttackDef.AttackKind.Dash;
                case "zone": return AttackDef.AttackKind.Zone;
                case "aimed_shot": return AttackDef.AttackKind.AimedShot;
                case "spread": return AttackDef.AttackKind.Spread;
                case "ring_shot": return AttackDef.AttackKind.RingShot;
                case "lob": return AttackDef.AttackKind.Lob;
                case "self_destruct": return AttackDef.AttackKind.SelfDestruct;
                case "tongue": return AttackDef.AttackKind.Tongue;
                default: throw new DataBuildException("모르는 공격 종류 '" + s + "'");
            }
        }

        private static string KindName(AttackDef.AttackKind k)
        {
            switch (k)
            {
                case AttackDef.AttackKind.Contact: return "contact";
                case AttackDef.AttackKind.Dash: return "dash";
                case AttackDef.AttackKind.Zone: return "zone";
                case AttackDef.AttackKind.AimedShot: return "aimed_shot";
                case AttackDef.AttackKind.Spread: return "spread";
                case AttackDef.AttackKind.RingShot: return "ring_shot";
                case AttackDef.AttackKind.Lob: return "lob";
                case AttackDef.AttackKind.SelfDestruct: return "self_destruct";
                default: return "tongue";
            }
        }

        private static AttackDef.TelegraphShape Shape(string s)
        {
            switch (s)
            {
                case "circle": return AttackDef.TelegraphShape.Circle;
                case "band": return AttackDef.TelegraphShape.Band;
                case "fan": return AttackDef.TelegraphShape.Fan;
                case "aim_line": return AttackDef.TelegraphShape.AimLine;
                case "ring": return AttackDef.TelegraphShape.Ring;
                default: throw new DataBuildException("모르는 예고 모양 '" + s + "'");
            }
        }

        private static string ShapeName(AttackDef.TelegraphShape? s)
        {
            switch (s)
            {
                case AttackDef.TelegraphShape.Circle: return "circle";
                case AttackDef.TelegraphShape.Band: return "band";
                case AttackDef.TelegraphShape.Fan: return "fan";
                case AttackDef.TelegraphShape.AimLine: return "aim_line";
                default: return "ring";
            }
        }

        private static AttackDef.LockMode LockOf(string s)
        {
            switch (s)
            {
                case "at_start": return AttackDef.LockMode.AtStart;
                case "last_n_ticks": return AttackDef.LockMode.LastNTicks;
                default: throw new DataBuildException("모르는 방향 고정 '" + s + "'");
            }
        }

        private static AttackDef.AnchorMode AnchorOf(string s)
        {
            switch (s)
            {
                case "self": return AttackDef.AnchorMode.Self;
                case "player_pos_at_start": return AttackDef.AnchorMode.PlayerPosAtStart;
                case "player_pos_lead_ahead": return AttackDef.AnchorMode.PlayerPosLeadAhead;
                case "along_line_to_wall": return AttackDef.AnchorMode.AlongLineToWall;
                default: throw new DataBuildException("모르는 구역 위치 '" + s + "'");
            }
        }

        private static AttackDef.DamageSource SourceOf(string? s)
        {
            switch (s)
            {
                case "contact": return AttackDef.DamageSource.Contact;
                case "projectile": return AttackDef.DamageSource.Projectile;
                case "telegraph": return AttackDef.DamageSource.Telegraph;
                case "lingering": return AttackDef.DamageSource.Lingering;
                default: throw new DataBuildException("모르는 피해 분류 '" + s + "'");
            }
        }

        // Fix64 의 값(칸이 아닌 수, 예: 체력) → Raw(× 2^32)
        private static long FixRaw(decimal v)
        {
            decimal raw = Units.RoundHalfEven(v, Fix64.OneRaw, 1);
            if (raw < long.MinValue || raw > long.MaxValue)
            {
                throw new DataBuildException("값 " + v.ToString(CultureInfo.InvariantCulture) + " 을 바꾼 Raw 가 64비트 범위를 넘는다");
            }
            return (long)raw;
        }

        private static long? OptionalCells(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement v) && v.ValueKind != JsonValueKind.Null ? Units.CellsToRaw(v.GetDecimal()) : (long?)null;

        private static string? OptionalString(JsonElement o, string name) =>
            o.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static decimal Dec(JsonElement o, string name) => o.GetProperty(name).GetDecimal();

        // 적지 않은 칸은 0(스키마의 attack·ai 설명)
        private static decimal DecOr(JsonElement o, string name) => o.TryGetProperty(name, out JsonElement v) ? v.GetDecimal() : 0m;

        private static bool BoolOr(JsonElement o, string name) => o.TryGetProperty(name, out JsonElement v) && v.GetBoolean();

        private static int Int(JsonElement o, string name) => IntOf(o.GetProperty(name), name);

        // 스키마의 "integer" 는 2.0 도 받으므로 decimal 로 읽어 정수인지 본다
        private static int IntOf(JsonElement e, string name)
        {
            decimal v = e.GetDecimal();
            if (v != decimal.Truncate(v) || v < int.MinValue || v > int.MaxValue)
            {
                throw new DataBuildException(name + " 값 " + v.ToString(CultureInfo.InvariantCulture) + " 은 32비트 정수가 아니다");
            }
            return (int)v;
        }

        private static string Num(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static void WriteEnemy(Enemy e, ByteWriter w)
        {
            w.U16((ushort)e.Id);
            w.U8((byte)e.Role);
            w.I64(e.BaseHpRaw);
            w.I32(e.MoveSpeedCenti);
            w.I32(e.AttackDamage);
            w.I32(e.ContactDamage);
            w.I32(e.Cost);
            w.I32(e.FirstWave);
            w.U8((byte)e.Times);
            w.U8((byte)e.Weak);
            w.U8((byte)e.Resist);
            w.U16((ushort)e.Tags);
            Bool(w, e.KnockbackImmune);
            Bool(w, e.SpeciesCap.HasValue);
            w.I32(e.SpeciesCap ?? 0);
            w.I64(e.BodyRadiusRaw);
            w.U8((byte)e.Attacks.Count);
            foreach (Attack a in e.Attacks)
            {
                WriteAttack(a, w);
            }
            Ai ai = e.Ai;
            w.I64(ai.DodgeDistanceRaw);
            w.I32(ai.DodgeCooldownTicks);
            w.I32(ai.DodgeWindowTicks);
            w.I32(ai.DodgeDashTicks);
            w.I32(ai.ArmorHalfAngleCenti);
            w.I32(ai.ArmorDamagePermille);
            w.I32(ai.CoverHpPermille);
            w.I64(ai.CoverOffsetRaw);
            w.I64(ai.KeepMinRaw);
            w.I64(ai.KeepMaxRaw);
            w.I64(ai.KiteMinRaw);
        }

        // 값이 없을 수 있는 칸은 u8 있음(0 | 1) + 값. 없으면 값은 0 이다(읽는 쪽이 확인한다)
        private static void WriteAttack(Attack a, ByteWriter w)
        {
            w.U8((byte)a.Kind);
            Bool(w, a.Shape.HasValue);
            w.U8(a.Shape.HasValue ? (byte)a.Shape.Value : (byte)0);
            w.I64(a.RadiusRaw);
            w.I64(a.InnerRadiusRaw);
            w.I64(a.WidthRaw);
            OptionalI64(w, a.LengthRaw);
            w.I32(a.FanCenti);
            OptionalI64(w, a.RangeRaw);
            OptionalI64(w, a.TriggerMinRaw);
            OptionalI64(w, a.TriggerMaxRaw);
            w.I32(a.CastTicks);
            w.I32(a.TelegraphTicks);
            w.U8((byte)a.Lock);
            w.I32(a.LockTicks);
            w.U8((byte)a.Anchor);
            w.I32(a.ActiveTicks);
            w.I32(a.DashSpeedCenti);
            w.I32(a.RecoveryTicks);
            w.I32(a.Damage);
            w.U8((byte)a.Source);
            Bool(w, a.Proj != null);
            w.I64(a.Proj?.SpeedPerTickRaw ?? 0);
            w.I32(a.Proj?.Count ?? 0);
            w.I32(a.Proj?.SpreadCenti ?? 0);
            w.I64(a.Proj?.RangeRaw ?? 0);
            w.I64(a.Proj?.HitRadiusRaw ?? 0);
            Bool(w, a.Grazeable);
            Bool(w, a.IsDash);
            Bool(w, a.Lob);
            Bool(w, a.EscapeOverrideMilli.HasValue);
            w.I32(a.EscapeOverrideMilli ?? 0);
            w.I32(a.CooldownTicks);
        }

        private static void OptionalI64(ByteWriter w, long? v)
        {
            Bool(w, v.HasValue);
            w.I64(v ?? 0);
        }

        private static void Bool(ByteWriter w, bool v) => w.U8(v ? (byte)1 : (byte)0);
    }
}
