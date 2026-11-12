using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 5 enemies(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/EnemiesConverter.cs. 원본: data/sim/enemies.json.
    // 절 바이트(LE, 모든 수는 이미 정수. "opt" = u8 있음(0 | 1) 다음에 값, 없으면 값은 0):
    //   u16 개수
    //   적 × 개수(EnemyTypeId 오름차순, 겹침 없음, None 없음):
    //     u16 id  u8 role  i64 base_hp_raw  i32 move_speed_centi
    //     i32 attack_damage  i32 contact_damage  i32 cost  i32 first_wave
    //     u8 time_mask  u8 weak  u8 resist  u16 base_tags  u8 knockback_immune(0 | 1)
    //     opt i32 species_cap  i64 body_radius_raw
    //     u8 공격 수(1 이상)
    //     공격 × 공격 수(원본의 나열 순서 = EnemyDef.Attacks 의 첨자):
    //       u8 kind  opt u8 shape  i64 radius_raw  i64 inner_radius_raw  i64 width_raw  opt i64 length_raw  i32 fan_centi
    //       opt i64 range_raw  opt i64 trigger_min_raw  opt i64 trigger_max_raw
    //       i32 cast_ticks  i32 telegraph_ticks  u8 lock  i32 lock_ticks  u8 anchor
    //       i32 active_ticks  i32 dash_speed_centi  i32 recovery_ticks  i32 damage  u8 damage_source
    //       u8 has_proj  i64 proj_speed_per_tick_raw  i32 proj_count  i32 proj_spread_centi  i64 proj_range_raw  i64 proj_hit_radius_raw
    //       u8 grazeable  u8 is_dash  u8 lob(각 0 | 1)  opt i32 escape_override_milli  i32 cooldown_ticks
    //     AI 매개변수(EnemyDef.AiParams 의 필드 순서):
    //       i64 dodge_distance_raw  i32 dodge_cooldown_ticks  i32 dodge_window_ticks  i32 dodge_dash_ticks
    //       i32 armor_half_angle_centi  i32 armor_damage_permille  i32 cover_hp_permille
    //       i64 cover_offset_raw  i64 keep_distance_min_raw  i64 keep_distance_max_raw  i64 kite_distance_min_raw
    // 필드를 더하는 작업은 적 항목 끝(AI 매개변수 뒤) 또는 공격 항목 끝에 더하고 변환기와 이 파일을 같은 커밋에서 고친다.
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값·비트, 0/1 이 아닌 bool, 없음인데 0 이 아닌 값)는 ContentException.
    // 규칙 검사(약점 ≠ 저항, 모양과 예고, 돌진·탄의 짝 …)는 변환기가 한다. 여기서는 바이트의 꼴만 믿지 않는다.
    public sealed partial class ContentDb
    {
        // EnemyTypeId 값으로 찾는 표. 비어 있는 칸은 그 id 가 팩에 없다는 뜻이다
        private EnemyDef?[] _enemies = Array.Empty<EnemyDef?>();

        partial void LoadEnemies(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            if (!r.TryU16(out ushort count))
            {
                throw EnemiesBroken("truncated count");
            }
            var defs = new EnemyDef[count];
            int previous = (int)EnemyTypeId.None;
            for (int i = 0; i < count; i++)
            {
                EnemyDef d = ReadEnemyEntry(ref r);
                if ((int)d.Id <= previous)
                {
                    throw EnemiesBroken("entries not in ascending id order (or id none)");
                }
                previous = (int)d.Id;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw EnemiesBroken("trailing bytes");
            }
            var table = new EnemyDef?[previous + 1];
            foreach (EnemyDef d in defs)
            {
                table[(int)d.Id] = d;
            }
            _enemies = table;
        }

        partial void FindEnemy(EnemyTypeId id, ref EnemyDef? def)
        {
            int i = (int)id;
            if (i < _enemies.Length)
            {
                def = _enemies[i];
            }
        }

        private static EnemyDef ReadEnemyEntry(ref ByteReader r)
        {
            var d = new EnemyDef();
            if (!r.TryU16(out ushort id))
            {
                throw EnemiesBroken("truncated entry");
            }
            if (!Enum.IsDefined(typeof(EnemyTypeId), id))
            {
                throw EnemiesBroken("unknown enemy id");
            }
            d.Id = (EnemyTypeId)id;
            d.Role = (EnemyDef.EnemyRole)EnemyEnumByte(ref r, typeof(EnemyDef.EnemyRole));
            d.BaseHp = Fix64.FromRaw(EnemyI64(ref r));
            d.MoveSpeedCenti = EnemyI32(ref r);
            d.AttackDamage = EnemyI32(ref r);
            d.ContactDamage = EnemyI32(ref r);
            d.Cost = EnemyI32(ref r);
            d.FirstWave = EnemyI32(ref r);
            byte times = EnemyU8(ref r);
            if (times == 0 || (times & ~(byte)EnemyDef.TimeMask.Always) != 0)
            {
                throw EnemiesBroken("time mask empty or with unknown bits");
            }
            d.Times = (EnemyDef.TimeMask)times;
            d.Weak = (Element)EnemyEnumByte(ref r, typeof(Element));
            d.Resist = (Element)EnemyEnumByte(ref r, typeof(Element));
            if (!r.TryU16(out ushort tags))
            {
                throw EnemiesBroken("truncated entry");
            }
            if ((tags & ~EnemyKnownTags()) != 0)
            {
                throw EnemiesBroken("unknown tag bits");
            }
            d.BaseTags = (EnemyTag)tags;
            d.KnockbackImmune = EnemyBool(ref r);
            bool hasCap = EnemyBool(ref r);
            int cap = EnemyI32(ref r);
            d.SpeciesCap = hasCap ? cap : EnemyAbsent(cap);
            d.BodyRadius = Fix64.FromRaw(EnemyI64(ref r));
            byte attacks = EnemyU8(ref r);
            if (attacks == 0)
            {
                throw EnemiesBroken("enemy without attacks");
            }
            d.Attacks = new AttackDef[attacks];
            for (int i = 0; i < attacks; i++)
            {
                d.Attacks[i] = ReadAttackEntry(ref r);
            }
            EnemyDef.AiParams ai = d.Ai;
            ai.DodgeDistance = Fix64.FromRaw(EnemyI64(ref r));
            ai.DodgeCooldownTicks = EnemyI32(ref r);
            ai.DodgeWindowTicks = EnemyI32(ref r);
            ai.DodgeDashTicks = EnemyI32(ref r);
            ai.ArmorHalfAngleCenti = EnemyI32(ref r);
            ai.ArmorDamagePermille = EnemyI32(ref r);
            ai.CoverHpPermille = EnemyI32(ref r);
            ai.CoverOffset = Fix64.FromRaw(EnemyI64(ref r));
            ai.KeepDistanceMin = Fix64.FromRaw(EnemyI64(ref r));
            ai.KeepDistanceMax = Fix64.FromRaw(EnemyI64(ref r));
            ai.KiteDistanceMin = Fix64.FromRaw(EnemyI64(ref r));
            return d;
        }

        private static AttackDef ReadAttackEntry(ref ByteReader r)
        {
            var a = new AttackDef();
            a.Kind = (AttackDef.AttackKind)EnemyEnumByte(ref r, typeof(AttackDef.AttackKind));
            bool hasShape = EnemyBool(ref r);
            if (hasShape)
            {
                a.Shape = (AttackDef.TelegraphShape)EnemyEnumByte(ref r, typeof(AttackDef.TelegraphShape));
            }
            else
            {
                EnemyAbsent(EnemyU8(ref r));
            }
            a.Radius = Fix64.FromRaw(EnemyI64(ref r));
            a.InnerRadius = Fix64.FromRaw(EnemyI64(ref r));
            a.Width = Fix64.FromRaw(EnemyI64(ref r));
            a.Length = EnemyOptionalFix(ref r);
            a.FanCenti = EnemyI32(ref r);
            a.Range = EnemyOptionalFix(ref r);
            a.TriggerMin = EnemyOptionalFix(ref r);
            a.TriggerMax = EnemyOptionalFix(ref r);
            a.CastTicks = EnemyI32(ref r);
            a.TelegraphTicks = EnemyI32(ref r);
            a.Lock = (AttackDef.LockMode)EnemyEnumByte(ref r, typeof(AttackDef.LockMode));
            a.LockTicks = EnemyI32(ref r);
            a.Anchor = (AttackDef.AnchorMode)EnemyEnumByte(ref r, typeof(AttackDef.AnchorMode));
            a.ActiveTicks = EnemyI32(ref r);
            a.DashSpeedCenti = EnemyI32(ref r);
            a.RecoveryTicks = EnemyI32(ref r);
            a.Damage = EnemyI32(ref r);
            a.Source = (AttackDef.DamageSource)EnemyEnumByte(ref r, typeof(AttackDef.DamageSource));
            bool hasProj = EnemyBool(ref r);
            long speed = EnemyI64(ref r);
            int projCount = EnemyI32(ref r);
            int spread = EnemyI32(ref r);
            long range = EnemyI64(ref r);
            long hitRadius = EnemyI64(ref r);
            if (hasProj)
            {
                a.Proj = new AttackDef.ProjDef
                {
                    SpeedPerTick = Fix64.FromRaw(speed),
                    Count = projCount,
                    SpreadCenti = spread,
                    Range = Fix64.FromRaw(range),
                    HitRadius = Fix64.FromRaw(hitRadius),
                };
            }
            else if (speed != 0 || projCount != 0 || spread != 0 || range != 0 || hitRadius != 0)
            {
                throw EnemiesBroken("absent projectile with nonzero fields");
            }
            a.Grazeable = EnemyBool(ref r);
            a.IsDash = EnemyBool(ref r);
            a.Lob = EnemyBool(ref r);
            bool hasEscape = EnemyBool(ref r);
            int escape = EnemyI32(ref r);
            a.EscapeOverrideMilli = hasEscape ? escape : EnemyAbsent(escape);
            a.CooldownTicks = EnemyI32(ref r);
            return a;
        }

        private static Fix64? EnemyOptionalFix(ref ByteReader r)
        {
            bool has = EnemyBool(ref r);
            long v = EnemyI64(ref r);
            if (has)
            {
                return Fix64.FromRaw(v);
            }
            if (v != 0)
            {
                throw EnemiesBroken("absent value is not zero");
            }
            return null;
        }

        // "없음" 칸의 값은 0 이어야 한다
        private static int? EnemyAbsent(int v)
        {
            if (v != 0)
            {
                throw EnemiesBroken("absent value is not zero");
            }
            return null;
        }

        // EnemyTag 에 선언된 비트 전부
        private static ushort EnemyKnownTags()
        {
            ushort mask = 0;
            foreach (object v in Enum.GetValues(typeof(EnemyTag)))
            {
                mask |= (ushort)(EnemyTag)v;
            }
            return mask;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte EnemyEnumByte(ref ByteReader r, Type enumType)
        {
            byte v = EnemyU8(ref r);
            if (!Enum.IsDefined(enumType, v))
            {
                throw EnemiesBroken("unknown enum value");
            }
            return v;
        }

        private static bool EnemyBool(ref ByteReader r)
        {
            byte v = EnemyU8(ref r);
            if (v > 1)
            {
                throw EnemiesBroken("bool is not 0 or 1");
            }
            return v == 1;
        }

        private static byte EnemyU8(ref ByteReader r)
        {
            if (!r.TryU8(out byte v))
            {
                throw EnemiesBroken("truncated entry");
            }
            return v;
        }

        private static int EnemyI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw EnemiesBroken("truncated entry");
            }
            return v;
        }

        private static long EnemyI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw EnemiesBroken("truncated entry");
            }
            return v;
        }

        private static ContentException EnemiesBroken(string what) => new ContentException("content section 'enemies': " + what);
    }
}
