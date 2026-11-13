using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 2 blocks(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/BlocksConverter.cs. 원본: data/sim/blocks.json.
    // 절 바이트(LE, 모든 수는 이미 정수):
    //   u16 개수
    //   항목 × 개수(BlockId 오름차순, 겹침 없음, None 없음):
    //     u8 id  u8 kind  u8 rarity  u8 element
    //     i32 heat_hu  i32 heat_mul_permille  i32 split_count
    //     i64 base_damage_raw  i32 interval_ticks  i64 speed_per_tick_raw  u8 is_instant(0 | 1)
    //     i64 range_raw  i64 hit_radius_raw  i32 damage_bonus_permille
    //     u16 flags  u8 effect
    //     BlockParams(T-53. 쓰지 않는 값은 0, 음수 없음):
    //       i64 explosion_radius_raw  i32 chain_hops  i64 chain_radius_raw  i32 chain_hop_mul_permille
    //       i64 return_arrive_radius_raw  i64 return_max_distance_raw
    //       i32 slow_permille  i32 slow_ticks  i32 damage_taken_bonus_permille  i32 damage_taken_bonus_ticks
    //       i32 fan_half_angle_centi
    //       i32 bounce_count  i32 pierce_count  i32 radius_mul_permille  i32 radius_mul_cap_permille
    //       i32 homing_turn_centi_per_tick  i32 homing_cone_half_centi  i64 homing_range_raw  i32 crit_multiplier
    //   항목 하나는 156바이트다. 필드를 더할 때는 항목 끝에 더한다.
    //   꼬리(T-57. 없거나 정확히 12바이트): 주문 규칙 SpellRulesDef
    //     i32 tree_projectile_cap(1 이상)  i32 underline_max_cut_hu(0 이상)  i32 mirror_heat_mul_permille(1000 이상)
    //   원본에 spell_rules 가 없으면 꼬리도 없다(ContentDb.SpellRules 는 ContentException). 저장소의 데이터에는 언제나 있다.
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값·비트, 음수인 BlockParams 값)는 ContentException.
    // 값끼리의 규칙(폭발 표식이면 반경이 있다 등)은 변환기가 본다. 읽는 쪽은 꼴만 본다.
    public sealed partial class ContentDb
    {
        // BlockId 값으로 찾는 표. 비어 있는 칸은 그 id 가 팩에 없다는 뜻이다
        private BlockDef?[] _blocks = Array.Empty<BlockDef?>();

        // 주문 규칙(절의 꼬리). 꼬리가 없으면 null
        private SpellRulesDef? _spellRules;

        partial void LoadBlocks(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            if (!r.TryU16(out ushort count))
            {
                throw BlocksBroken("truncated count");
            }
            ushort knownFlags = BlockKnownFlags();
            var defs = new BlockDef[count];
            int previous = (int)BlockId.None;
            for (int i = 0; i < count; i++)
            {
                BlockDef d = ReadBlockEntry(ref r, knownFlags);
                if ((int)d.Id <= previous)
                {
                    throw BlocksBroken("entries not in ascending id order (or id none)");
                }
                previous = (int)d.Id;
                defs[i] = d;
            }
            SpellRulesDef? rules = null;
            if (r.Remaining != 0)
            {
                rules = ReadSpellRules(ref r);
            }
            if (r.Remaining != 0)
            {
                throw BlocksBroken("trailing bytes");
            }
            var table = new BlockDef?[previous + 1];
            foreach (BlockDef d in defs)
            {
                table[(int)d.Id] = d;
            }
            _blocks = table;
            _spellRules = rules;
        }

        partial void FindSpellRules(ref SpellRulesDef? def)
        {
            if (_spellRules != null)
            {
                def = _spellRules;
            }
        }

        // 꼬리의 범위는 읽는 쪽도 본다: 상한이 0 이하이면 컴파일러의 가지치기가, 배수가 1 미만이면 열이 뜻을 잃는다
        private static SpellRulesDef ReadSpellRules(ref ByteReader r)
        {
            var d = new SpellRulesDef
            {
                TreeProjectileCap = BlockI32(ref r),
                UnderlineMaxCutHu = BlockI32(ref r),
                MirrorHeatMulPermille = BlockI32(ref r),
            };
            if (d.TreeProjectileCap < 1 || d.UnderlineMaxCutHu < 0 || d.MirrorHeatMulPermille < SpellRulesPermilleOne)
            {
                throw BlocksBroken("spell rules out of range");
            }
            return d;
        }

        private const int SpellRulesPermilleOne = 1000;   // 천분율 1.0(단위 환산)

        partial void FindBlock(BlockId id, ref BlockDef? def)
        {
            int i = (int)id;
            if (i < _blocks.Length)
            {
                def = _blocks[i];
            }
        }

        private static BlockDef ReadBlockEntry(ref ByteReader r, ushort knownFlags)
        {
            var d = new BlockDef();
            d.Id = (BlockId)BlockEnumByte(ref r, typeof(BlockId));
            d.Kind = (BlockKind)BlockEnumByte(ref r, typeof(BlockKind));
            d.Rarity = (Rarity)BlockEnumByte(ref r, typeof(Rarity));
            d.Element = (Element)BlockEnumByte(ref r, typeof(Element));
            d.HeatHu = BlockI32(ref r);
            d.HeatMulPermille = BlockI32(ref r);
            d.SplitCount = BlockI32(ref r);
            d.BaseDamage = Fix64.FromRaw(BlockI64(ref r));
            d.IntervalTicks = BlockI32(ref r);
            d.SpeedPerTick = Fix64.FromRaw(BlockI64(ref r));
            if (!r.TryU8(out byte instant))
            {
                throw BlocksBroken("truncated entry");
            }
            if (instant > 1)
            {
                throw BlocksBroken("is_instant is not 0 or 1");
            }
            d.IsInstant = instant == 1;
            d.Range = Fix64.FromRaw(BlockI64(ref r));
            d.HitRadius = Fix64.FromRaw(BlockI64(ref r));
            d.DamageBonusPermille = BlockI32(ref r);
            if (!r.TryU16(out ushort flags))
            {
                throw BlocksBroken("truncated entry");
            }
            if ((flags & ~knownFlags) != 0)
            {
                throw BlocksBroken("unknown projectile flag bits");
            }
            d.Flags = (ProjectileFlags)flags;
            d.Effect = (ModifierEffect)BlockEnumByte(ref r, typeof(ModifierEffect));
            BlockParams p = d.Params;
            p.ExplosionRadius = Fix64.FromRaw(BlockParamI64(ref r));
            p.ChainHops = BlockParamI32(ref r);
            p.ChainRadius = Fix64.FromRaw(BlockParamI64(ref r));
            p.ChainHopMulPermille = BlockParamI32(ref r);
            p.ReturnArriveRadius = Fix64.FromRaw(BlockParamI64(ref r));
            p.ReturnMaxDistance = Fix64.FromRaw(BlockParamI64(ref r));
            p.SlowPermille = BlockParamI32(ref r);
            p.SlowTicks = BlockParamI32(ref r);
            p.DamageTakenBonusPermille = BlockParamI32(ref r);
            p.DamageTakenBonusTicks = BlockParamI32(ref r);
            p.FanHalfAngleCenti = BlockParamI32(ref r);
            p.BounceCount = BlockParamI32(ref r);
            p.PierceCount = BlockParamI32(ref r);
            p.RadiusMulPermille = BlockParamI32(ref r);
            p.RadiusMulCapPermille = BlockParamI32(ref r);
            p.HomingTurnCentiPerTick = BlockParamI32(ref r);
            p.HomingConeHalfCenti = BlockParamI32(ref r);
            p.HomingRange = Fix64.FromRaw(BlockParamI64(ref r));
            p.CritMultiplier = BlockParamI32(ref r);
            return d;
        }

        // BlockParams 의 값은 전부 0 이상이다(쓰지 않는 값은 0)
        private static int BlockParamI32(ref ByteReader r)
        {
            int v = BlockI32(ref r);
            if (v < 0)
            {
                throw BlocksBroken("negative block param");
            }
            return v;
        }

        private static long BlockParamI64(ref ByteReader r)
        {
            long v = BlockI64(ref r);
            if (v < 0)
            {
                throw BlocksBroken("negative block param");
            }
            return v;
        }

        // ProjectileFlags 에 선언된 비트 전부(비트를 더하면 읽는 코드는 그대로 따라간다)
        private static ushort BlockKnownFlags()
        {
            ushort mask = 0;
            foreach (object v in Enum.GetValues(typeof(ProjectileFlags)))
            {
                mask |= (ushort)(ProjectileFlags)v;
            }
            return mask;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte BlockEnumByte(ref ByteReader r, Type enumType)
        {
            if (!r.TryU8(out byte v))
            {
                throw BlocksBroken("truncated entry");
            }
            if (!Enum.IsDefined(enumType, v))
            {
                throw BlocksBroken("unknown enum value");
            }
            return v;
        }

        private static int BlockI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw BlocksBroken("truncated entry");
            }
            return v;
        }

        private static long BlockI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw BlocksBroken("truncated entry");
            }
            return v;
        }

        private static ContentException BlocksBroken(string what) => new ContentException("content section 'blocks': " + what);
    }
}
