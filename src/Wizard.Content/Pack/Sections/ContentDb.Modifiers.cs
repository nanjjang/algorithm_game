using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 9 modifiers(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/ModifiersConverter.cs. 원본: data/sim/modifiers.json.
    // 절 바이트(LE, 모든 수는 이미 정수. 필드의 뜻과 "효과 없음" 값은 ModifierDef):
    //   u16 개수
    //   변덕 × 개수(ModifierId 오름차순, 겹침 없음, None 없음):
    //     u8 id
    //     i32 score_pct
    //     u8 only_tod                      TimeOfDay(0 = 모든 시간대). BossWave 는 안 된다
    //     i32 bundle_interval_ticks        0 = 바꾸지 않음
    //     i32 enemy_hp_pct
    //     i32 extra_budget_pct  i32 extra_budget_cost_max
    //     i32 extra_elites
    //     i64 death_circle_radius_raw  i32 death_circle_telegraph_ticks  i32 death_circle_damage  i32 death_circle_stagger_ticks
    //     i32 enemy_shot_count_pct  i32 enemy_shot_speed_pct
    //     u16 가중치 수, (u16 species, i32 weight_pct) × 그 수        species 오름차순, 겹침 없음, None 없음
    //     u16 종 상한 수, (u16 species, i32 cap) × 그 수              species 오름차순, 겹침 없음, None 없음
    //     i64 thorn_border_width_raw  i32 thorn_telegraph_ticks  i32 thorn_contact_interval_ticks  i32 thorn_damage
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값)는 ContentException.
    // spec 07 2.5 의 금지("예고 시간을 줄이거나 적을 빠르게 하는 변덕은 만들지 않는다", TV-109)도 여기서 다시 본다:
    // 적 탄 속도 배율 > 100, 예고 0 틱인 원·가시밭이면 로드 실패다(DataBuild 가 먼저 막지만 읽는 쪽은 바이트를 믿지 않는다).
    public sealed partial class ContentDb
    {
        private IReadOnlyList<ModifierDef>? _modifiers;     // ModifierId 오름차순(spec 07 9절 "데이터 순서 고정")

        partial void LoadModifiers(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            ushort count = ModifiersU16(ref r);
            var defs = new ModifierDef[count];
            int previous = 0;
            for (int i = 0; i < count; i++)
            {
                ModifierDef d = ModifierEntry(ref r);
                if ((int)d.Id <= previous)
                {
                    throw ModifiersBroken("entries not in ascending modifier order");
                }
                previous = (int)d.Id;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw ModifiersBroken("trailing bytes");
            }
            _modifiers = new ReadOnlyCollection<ModifierDef>(defs);
        }

        partial void FindModifiers(ref IReadOnlyList<ModifierDef>? defs)
        {
            if (_modifiers != null)
            {
                defs = _modifiers;
            }
        }

        private static ModifierDef ModifierEntry(ref ByteReader r)
        {
            var d = new ModifierDef();
            byte id = ModifiersU8(ref r);
            if (id == (byte)ModifierId.None || !Enum.IsDefined(typeof(ModifierId), id))
            {
                throw ModifiersBroken("unknown modifier");
            }
            d.Id = (ModifierId)id;
            d.ScorePct = ModifiersI32(ref r);
            byte tod = ModifiersU8(ref r);
            if (!Enum.IsDefined(typeof(Wizard.Content.TimeOfDay), tod) || tod == (byte)Wizard.Content.TimeOfDay.BossWave)
            {
                throw ModifiersBroken("unknown or boss time of day");
            }
            d.OnlyTod = (Wizard.Content.TimeOfDay)tod;
            d.BundleIntervalTicks = ModifiersI32(ref r);
            d.EnemyHpPct = ModifiersI32(ref r);
            d.ExtraBudgetPct = ModifiersI32(ref r);
            d.ExtraBudgetCostMax = ModifiersI32(ref r);
            d.ExtraElites = ModifiersI32(ref r);
            d.DeathCircleRadius = Fix64.FromRaw(ModifiersI64(ref r));
            d.DeathCircleTelegraphTicks = ModifiersI32(ref r);
            d.DeathCircleDamage = ModifiersI32(ref r);
            d.DeathCircleStaggerTicks = ModifiersI32(ref r);
            d.EnemyShotCountPct = ModifiersI32(ref r);
            d.EnemyShotSpeedPct = ModifiersI32(ref r);
            ModifiersSpeciesValues(ref r, out d.WeightSpecies, out d.WeightPct);
            ModifiersSpeciesValues(ref r, out d.CapSpecies, out d.Cap);
            d.ThornBorderWidth = Fix64.FromRaw(ModifiersI64(ref r));
            d.ThornTelegraphTicks = ModifiersI32(ref r);
            d.ThornContactIntervalTicks = ModifiersI32(ref r);
            d.ThornDamage = ModifiersI32(ref r);

            // spec 07 2.5: 적을 빠르게 하거나 예고 없는 위험을 까는 변덕은 로드 실패
            if (d.EnemyShotSpeedPct > ModifierDef.NeutralPct)
            {
                throw ModifiersBroken("modifier speeds enemy shots up (spec 07 2.5)");
            }
            if (d.DeathCircleRadius.Raw != 0 && d.DeathCircleTelegraphTicks <= 0)
            {
                throw ModifiersBroken("death circle without telegraph (spec 07 2.5)");
            }
            if (d.ThornBorderWidth.Raw != 0 && d.ThornTelegraphTicks <= 0)
            {
                throw ModifiersBroken("thorn border without telegraph (spec 07 2.5)");
            }
            return d;
        }

        // (u16 species, i32 값) 배열. species 는 EnemyTypeId 에 있고 None 이 아니며 오름차순이다
        private static void ModifiersSpeciesValues(ref ByteReader r, out EnemyTypeId[] species, out int[] values)
        {
            ushort n = ModifiersU16(ref r);
            species = new EnemyTypeId[n];
            values = new int[n];
            int previous = 0;
            for (int i = 0; i < n; i++)
            {
                ushort s = ModifiersU16(ref r);
                if (s == (ushort)EnemyTypeId.None || !Enum.IsDefined(typeof(EnemyTypeId), s))
                {
                    throw ModifiersBroken("unknown species");
                }
                if (s <= previous)
                {
                    throw ModifiersBroken("species not in ascending order");
                }
                previous = s;
                species[i] = (EnemyTypeId)s;
                values[i] = ModifiersI32(ref r);
            }
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte ModifiersU8(ref ByteReader r)
        {
            if (!r.TryU8(out byte v))
            {
                throw ModifiersBroken("truncated entry");
            }
            return v;
        }

        private static ushort ModifiersU16(ref ByteReader r)
        {
            if (!r.TryU16(out ushort v))
            {
                throw ModifiersBroken("truncated entry");
            }
            return v;
        }

        private static int ModifiersI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw ModifiersBroken("truncated entry");
            }
            return v;
        }

        private static long ModifiersI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw ModifiersBroken("truncated entry");
            }
            return v;
        }

        private static ContentException ModifiersBroken(string what) => new ContentException("content section 'modifiers': " + what);
    }
}
