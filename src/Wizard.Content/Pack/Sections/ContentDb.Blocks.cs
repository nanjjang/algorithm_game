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
    // BlockParams 는 아직 필드가 없어 절에 없다(spec 02 4.2 의 폭발 반경 등은 M2-SPL-01 이 끝에 더한다).
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값·비트)는 ContentException.
    public sealed partial class ContentDb
    {
        // BlockId 값으로 찾는 표. 비어 있는 칸은 그 id 가 팩에 없다는 뜻이다
        private BlockDef?[] _blocks = Array.Empty<BlockDef?>();

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
        }

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
            return d;
        }

        // ProjectileFlags 에 선언된 비트 전부(비트는 M2-SPL-01 이 더한다. 읽는 코드는 그대로 따라간다)
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
