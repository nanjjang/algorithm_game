using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 15 starfall(CONTRACTS 11.2)을 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/StarfallConverter.cs. 원본: data/sim/starfall.json.
    // 절 바이트(LE, 모든 수는 이미 정수. 필드의 뜻은 StarfallDef):
    //   u8 only_tod                       TimeOfDay. None·BossWave 는 안 된다
    //   i32 chance_bp
    //   i32 drop_from_ticks  i32 drop_to_ticks           from ≤ to
    //   i32 telegraph_ticks  i32 landed_ticks  i32 blink_ticks     blink ≤ landed
    //   i64 area_width_raw  i64 area_height_raw  i64 grid_step_raw
    //   i64 hazard_clearance_raw  i64 player_clearance_raw
    //   i32 retry_ticks
    //   i64 pick_radius_raw
    //   i32 crystals
    // 깨진 바이트(모자람, 남음, 모르는 열거형 값, 구간 뒤집힘)는 ContentException.
    public sealed partial class ContentDb
    {
        private StarfallDef? _starfall;

        partial void LoadStarfall(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            var d = new StarfallDef();
            if (!r.TryU8(out byte tod))
            {
                throw StarfallBroken("truncated entry");
            }
            if (tod == (byte)Wizard.Content.TimeOfDay.None || tod == (byte)Wizard.Content.TimeOfDay.BossWave || !Enum.IsDefined(typeof(Wizard.Content.TimeOfDay), tod))
            {
                throw StarfallBroken("unknown or boss time of day");
            }
            d.OnlyTod = (Wizard.Content.TimeOfDay)tod;
            d.ChanceBp = StarfallI32(ref r);
            d.DropFromTicks = StarfallI32(ref r);
            d.DropToTicks = StarfallI32(ref r);
            d.TelegraphTicks = StarfallI32(ref r);
            d.LandedTicks = StarfallI32(ref r);
            d.BlinkTicks = StarfallI32(ref r);
            d.AreaWidth = Fix64.FromRaw(StarfallI64(ref r));
            d.AreaHeight = Fix64.FromRaw(StarfallI64(ref r));
            d.GridStep = Fix64.FromRaw(StarfallI64(ref r));
            d.HazardClearance = Fix64.FromRaw(StarfallI64(ref r));
            d.PlayerClearance = Fix64.FromRaw(StarfallI64(ref r));
            d.RetryTicks = StarfallI32(ref r);
            d.PickRadius = Fix64.FromRaw(StarfallI64(ref r));
            d.Crystals = StarfallI32(ref r);
            if (r.Remaining != 0)
            {
                throw StarfallBroken("trailing bytes");
            }
            if (d.DropFromTicks > d.DropToTicks || d.BlinkTicks > d.LandedTicks)
            {
                throw StarfallBroken("reversed window");
            }
            _starfall = d;
        }

        partial void FindStarfall(ref StarfallDef? def)
        {
            if (_starfall != null)
            {
                def = _starfall;
            }
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static int StarfallI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw StarfallBroken("truncated entry");
            }
            return v;
        }

        private static long StarfallI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw StarfallBroken("truncated entry");
            }
            return v;
        }

        private static ContentException StarfallBroken(string what) => new ContentException("content section 'starfall': " + what);
    }
}
