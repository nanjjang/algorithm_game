using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 18 elites(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/ElitesConverter.cs. 원본: data/sim/elites.json.
    // 절 바이트(LE, 모든 수는 이미 정수):
    //   u16 개수
    //   엘리트 × 개수(바탕 EnemyTypeId 오름차순, 겹침 없음, None 없음):
    //     u16 base_id  u8 ability
    //     u8 공격 수
    //     공격 × 공격 수(원본의 나열 순서): i32 replaces_index  i32 follows_index(각 −1 = 없음)  공격(절 5 enemies 의 공격 꼴과 같다)
    //     능력 매개변수(EliteDef.AbilityParams 의 필드 순서):
    //       i32 split_count  i64 split_radius_raw  i32 split_start_angle_centi
    //       i32 howl_ticks  i64 howl_radius_raw  i32 howl_step_ticks  i32 chain_count  i32 chain_gap_ticks
    //       i32 armor_half_angle_centi
    //       i64 sympathetic_damage_raw
    //       i32 aim_count  i32 aim_step_ticks
    //       i32 band_count  i32 band_start_angle_centi
    //       i32 summon_count  i64 summon_radius_raw  u8 문턱 수  i32 summon_hp_permille × 문턱 수
    //       i32 ring_repeat  i32 ring_repeat_step_ticks  i32 ring_repeat_angle_centi
    //       i32 seed_count  i64 seed_lead_distance_raw  i64 seed_wall_inset_raw
    //       i32 bounce_count  i32 bounce_stop_ticks
    //       i64 croak_radius_raw  i64 croak_break_damage_raw  u8 croak_break_only(0 | 1)
    // 필드를 더하는 작업은 엘리트 항목 끝에 더하고 변환기와 이 파일을 같은 커밋에서 고친다.
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값, 엘리트가 없는 종, 0/1 이 아닌 bool, 첨자 −1 미만, 둘 다 있는 첨자)는 ContentException.
    // 바탕 종의 공격 수와 첨자가 맞는지, 능력과 매개변수의 짝은 변환기(와 그 교차 검사)가 본다.
    public sealed partial class ContentDb
    {
        // 바탕 EnemyTypeId 값으로 찾는 표. 비어 있는 칸은 그 종의 엘리트가 팩에 없다는 뜻이다
        private EliteDef?[] _elites = Array.Empty<EliteDef?>();

        partial void LoadElites(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            if (!r.TryU16(out ushort count))
            {
                throw ElitesBroken("truncated count");
            }
            var defs = new EliteDef[count];
            int previous = (int)EnemyTypeId.None;
            for (int i = 0; i < count; i++)
            {
                EliteDef d = ReadEliteEntry(ref r);
                if ((int)d.BaseType <= previous)
                {
                    throw ElitesBroken("entries not in ascending base id order (or id none)");
                }
                previous = (int)d.BaseType;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw ElitesBroken("trailing bytes");
            }
            var table = new EliteDef?[previous + 1];
            foreach (EliteDef d in defs)
            {
                table[(int)d.BaseType] = d;
            }
            _elites = table;
        }

        partial void FindElite(EnemyTypeId baseType, ref EliteDef? def)
        {
            int i = (int)baseType;
            if (i < _elites.Length)
            {
                def = _elites[i];
            }
        }

        private static EliteDef ReadEliteEntry(ref ByteReader r)
        {
            var d = new EliteDef();
            if (!r.TryU16(out ushort id))
            {
                throw ElitesBroken("truncated entry");
            }
            if (!Enum.IsDefined(typeof(EnemyTypeId), id) || !HasEliteId((EnemyTypeId)id))
            {
                throw ElitesBroken("base id without an elite");
            }
            d.BaseType = (EnemyTypeId)id;
            byte ability = EliteU8(ref r);
            if (!Enum.IsDefined(typeof(EliteDef.EliteAbility), ability))
            {
                throw ElitesBroken("unknown enum value");
            }
            d.Ability = (EliteDef.EliteAbility)ability;
            byte attacks = EliteU8(ref r);
            d.Attacks = new EliteDef.EliteAttack[attacks];
            for (int i = 0; i < attacks; i++)
            {
                var a = new EliteDef.EliteAttack();
                a.ReplacesIndex = EliteI32(ref r);
                a.FollowsIndex = EliteI32(ref r);
                if (a.ReplacesIndex < -1 || a.FollowsIndex < -1 || (a.ReplacesIndex >= 0 && a.FollowsIndex >= 0))
                {
                    throw ElitesBroken("attack index out of range");
                }
                try
                {
                    a.Attack = ReadAttackEntry(ref r);
                }
                catch (ContentException e)
                {
                    throw ElitesBroken("attack: " + e.Message);
                }
                d.Attacks[i] = a;
            }
            EliteDef.AbilityParams p = d.Params;
            p.SplitCount = EliteI32(ref r);
            p.SplitRadius = Fix64.FromRaw(EliteI64(ref r));
            p.SplitStartAngleCenti = EliteI32(ref r);
            p.HowlTicks = EliteI32(ref r);
            p.HowlRadius = Fix64.FromRaw(EliteI64(ref r));
            p.HowlStepTicks = EliteI32(ref r);
            p.ChainCount = EliteI32(ref r);
            p.ChainGapTicks = EliteI32(ref r);
            p.ArmorHalfAngleCenti = EliteI32(ref r);
            p.SympatheticDamage = Fix64.FromRaw(EliteI64(ref r));
            p.AimCount = EliteI32(ref r);
            p.AimStepTicks = EliteI32(ref r);
            p.BandCount = EliteI32(ref r);
            p.BandStartAngleCenti = EliteI32(ref r);
            p.SummonCount = EliteI32(ref r);
            p.SummonRadius = Fix64.FromRaw(EliteI64(ref r));
            byte thresholds = EliteU8(ref r);
            p.SummonHpPermille = new int[thresholds];
            for (int i = 0; i < thresholds; i++)
            {
                p.SummonHpPermille[i] = EliteI32(ref r);
            }
            p.RingRepeat = EliteI32(ref r);
            p.RingRepeatStepTicks = EliteI32(ref r);
            p.RingRepeatAngleCenti = EliteI32(ref r);
            p.SeedCount = EliteI32(ref r);
            p.SeedLeadDistance = Fix64.FromRaw(EliteI64(ref r));
            p.SeedWallInset = Fix64.FromRaw(EliteI64(ref r));
            p.BounceCount = EliteI32(ref r);
            p.BounceStopTicks = EliteI32(ref r);
            p.CroakRadius = Fix64.FromRaw(EliteI64(ref r));
            p.CroakBreakDamage = Fix64.FromRaw(EliteI64(ref r));
            byte breakOnly = EliteU8(ref r);
            if (breakOnly > 1)
            {
                throw ElitesBroken("bool is not 0 or 1");
            }
            p.CroakBreakOnly = breakOnly == 1;
            return d;
        }

        // spec 14 3.7 의 엘리트가 있는 종인가(일반 12종. None 과 졸개에는 엘리트가 없다)
        private static bool HasEliteId(EnemyTypeId id)
        {
            try
            {
                ContentIds.ToEliteId(id);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte EliteU8(ref ByteReader r)
        {
            if (!r.TryU8(out byte v))
            {
                throw ElitesBroken("truncated entry");
            }
            return v;
        }

        private static int EliteI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw ElitesBroken("truncated entry");
            }
            return v;
        }

        private static long EliteI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw ElitesBroken("truncated entry");
            }
            return v;
        }

        private static ContentException ElitesBroken(string what) => new ContentException("content section 'elites': " + what);
    }
}
