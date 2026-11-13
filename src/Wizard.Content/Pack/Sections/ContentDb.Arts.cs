using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 3 arts(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/ArtsConverter.cs. 원본: data/sim/arts.json.
    // 절 바이트(LE, 모든 수는 이미 정수):
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
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값, 0/1 이 아닌 bool, 종류와 주인의 어긋남)는 ContentException.
    public sealed partial class ContentDb
    {
        // ArtId 값으로 찾는 표. 비어 있는 칸은 그 id 가 팩에 없다는 뜻이다
        private ArtDef?[] _arts = Array.Empty<ArtDef?>();

        partial void LoadArts(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            if (!r.TryU16(out ushort count))
            {
                throw ArtsBroken("truncated count");
            }
            var defs = new ArtDef[count];
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                ArtDef d = ReadArtEntry(ref r);
                if ((int)d.Id <= previous)
                {
                    throw ArtsBroken("entries not in ascending id order");
                }
                previous = (int)d.Id;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw ArtsBroken("trailing bytes");
            }
            var table = new ArtDef?[previous + 1];
            foreach (ArtDef d in defs)
            {
                table[(int)d.Id] = d;
            }
            _arts = table;
        }

        partial void FindArt(ArtId id, ref ArtDef? def)
        {
            int i = (int)id;
            if (i >= 0 && i < _arts.Length)
            {
                def = _arts[i];
            }
        }

        private static ArtDef ReadArtEntry(ref ByteReader r)
        {
            var d = new ArtDef();
            d.Id = (ArtId)ArtEnumByte(ref r, typeof(ArtId));
            if (d.Id == ArtId.None)
            {
                throw ArtsBroken("art id None");
            }
            d.Kind = (ArtDef.ArtKind)ArtEnumByte(ref r, typeof(ArtDef.ArtKind));
            d.Owner = (CharacterId)ArtEnumByte(ref r, typeof(CharacterId));
            if (d.IsUnique == (d.Owner == CharacterId.Baseline))
            {
                throw ArtsBroken("a unique art has an owner and a common art has none");
            }
            d.IsJust = ArtBool(ref r);
            d.Difficulty = ArtI32(ref r);
            d.CostKind = (ArtDef.ArtCostKind)ArtEnumByte(ref r, typeof(ArtDef.ArtCostKind));
            d.Cost = ArtI32(ref r);
            d.MinCost = ArtI32(ref r);
            if (d.Cost < 0 || d.MinCost < 0)
            {
                throw ArtsBroken("negative cost");
            }
            d.CreditAllowed = ArtBool(ref r);
            d.FallbackParent = (ArtId)ArtEnumByte(ref r, typeof(ArtId));
            d.Unlock.AtStart = ArtBool(ref r);
            d.Unlock.Prereq = (ArtId)ArtEnumByte(ref r, typeof(ArtId));
            d.Unlock.MinWave = ArtI32(ref r);
            int pathCount = ArtU8(ref r);
            if (pathCount == 0)
            {
                throw ArtsBroken("an art needs at least one path");
            }
            var paths = new ArtDef.CommandStep[pathCount][];
            for (int p = 0; p < pathCount; p++)
            {
                int length = ArtU8(ref r);
                var steps = new ArtDef.CommandStep[length];
                for (int s = 0; s < length; s++)
                {
                    steps[s] = (ArtDef.CommandStep)ArtEnumByte(ref r, typeof(ArtDef.CommandStep));
                }
                paths[p] = steps;
            }
            d.Paths = paths;
            d.Effect = ReadArtEffect(ref r);
            d.Duel = ReadArtEffect(ref r);
            return d;
        }

        private static ArtDef.EffectParams ReadArtEffect(ref ByteReader r)
        {
            var f = new ArtDef.EffectParams();
            f.DamagePermille = ArtI32(ref r);
            f.SideDamagePermille = ArtI32(ref r);
            f.BranchCount = ArtI32(ref r);
            f.BranchAngleCenti = ArtI32(ref r);
            f.Knockback = Fix64.FromRaw(ArtI64(ref r));
            f.SelfMove = Fix64.FromRaw(ArtI64(ref r));
            f.StaggerTicks = ArtI32(ref r);
            f.StunTicks = ArtI32(ref r);
            f.BossHaltTicks = ArtI32(ref r);
            f.InvulnTicks = ArtI32(ref r);
            f.Width = Fix64.FromRaw(ArtI64(ref r));
            f.Thickness = Fix64.FromRaw(ArtI64(ref r));
            f.Range = Fix64.FromRaw(ArtI64(ref r));
            f.Radius = Fix64.FromRaw(ArtI64(ref r));
            f.ClearRadius = Fix64.FromRaw(ArtI64(ref r));
            f.Offset = Fix64.FromRaw(ArtI64(ref r));
            f.ShardLength = Fix64.FromRaw(ArtI64(ref r));
            f.PullDistance = Fix64.FromRaw(ArtI64(ref r));
            f.DurationTicks = ArtI32(ref r);
            f.IntervalTicks = ArtI32(ref r);
            f.RepeatIntervalPermilleTicks = ArtI32(ref r);
            f.SlowPermille = ArtI32(ref r);
            f.HeatAbsorbHu = ArtI32(ref r);
            f.HeatPerTHu = ArtI32(ref r);
            f.MaxConcurrent = ArtI32(ref r);
            f.MaxCopies = ArtI32(ref r);
            f.PrepTicks = ArtI32(ref r);
            f.WallHp = ArtI32(ref r);
            f.TelegraphTicks = ArtI32(ref r);
            f.IgnoresArmor = ArtBool(ref r);
            return f;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte ArtU8(ref ByteReader r)
        {
            if (!r.TryU8(out byte v))
            {
                throw ArtsBroken("truncated entry");
            }
            return v;
        }

        private static byte ArtEnumByte(ref ByteReader r, Type enumType)
        {
            byte v = ArtU8(ref r);
            if (!Enum.IsDefined(enumType, v))
            {
                throw ArtsBroken("unknown enum value");
            }
            return v;
        }

        private static bool ArtBool(ref ByteReader r)
        {
            byte v = ArtU8(ref r);
            if (v > 1)
            {
                throw ArtsBroken("bool is not 0 or 1");
            }
            return v == 1;
        }

        private static int ArtI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw ArtsBroken("truncated entry");
            }
            return v;
        }

        private static long ArtI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw ArtsBroken("truncated entry");
            }
            return v;
        }

        private static ContentException ArtsBroken(string what) => new ContentException("content section 'arts': " + what);
    }
}
