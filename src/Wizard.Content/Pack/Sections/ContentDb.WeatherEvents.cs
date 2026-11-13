using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 16 weather_events(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/WeatherEventsConverter.cs.
    // 원본: data/sim/weather_events.json.
    // 절 바이트(LE, 모든 수는 이미 정수. 필드의 뜻은 WeatherEventDef):
    //   u16 개수
    //   사건 × 개수(WeatherEventId 오름차순, 겹침 없음, None 없음):
    //     u8 id
    //     u8 affects_rules(0 | 1)
    //     u8 on_day_change(0 | 1)
    //     u8 tod_mask                      비트 (1 << TimeOfDay). on_day_change 면 0, 아니면 0 이 아니다. None 의 비트는 없다
    //     i32 chance_bp                    0 ~ 10,000
    //     i32 max_per_day                  0 = 상한 없음
    //     u8 is_instant(0 | 1)
    //     i32 duration_ticks               is_instant 면 0
    //     i64 edge_width_raw
    //     i32 variant_count                1 이상
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값·비트, 0/1 이 아닌 bool, 범위 밖의 수)는 ContentException.
    public sealed partial class ContentDb
    {
        private IReadOnlyList<WeatherEventDef>? _weatherEvents;     // WeatherEventId 오름차순

        partial void LoadWeatherEvents(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            if (!r.TryU16(out ushort count))
            {
                throw WeatherEventsBroken("truncated entry");
            }
            var defs = new WeatherEventDef[count];
            int previous = (int)WeatherEventId.None;
            for (int i = 0; i < count; i++)
            {
                WeatherEventDef d = WeatherEventEntry(ref r);
                if ((int)d.Id <= previous)
                {
                    throw WeatherEventsBroken("entries not in ascending event order");
                }
                previous = (int)d.Id;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw WeatherEventsBroken("trailing bytes");
            }
            _weatherEvents = new ReadOnlyCollection<WeatherEventDef>(defs);
        }

        partial void FindWeatherEvents(ref IReadOnlyList<WeatherEventDef>? defs)
        {
            if (_weatherEvents != null)
            {
                defs = _weatherEvents;
            }
        }

        private static WeatherEventDef WeatherEventEntry(ref ByteReader r)
        {
            var d = new WeatherEventDef();
            byte id = WeatherEventsU8(ref r);
            if (id == (byte)WeatherEventId.None || !Enum.IsDefined(typeof(WeatherEventId), id))
            {
                throw WeatherEventsBroken("unknown event");
            }
            d.Id = (WeatherEventId)id;
            d.AffectsRules = WeatherEventsBool(ref r);
            d.OnDayChange = WeatherEventsBool(ref r);
            d.TodMask = WeatherEventsU8(ref r);
            if ((d.TodMask & ~WeatherEventDef.AllTodsMask) != 0)
            {
                throw WeatherEventsBroken("unknown time of day bit");
            }
            if (d.OnDayChange != (d.TodMask == 0))
            {
                throw WeatherEventsBroken("day change event with times of day, or wave event without any");
            }
            d.ChanceBp = WeatherEventsI32(ref r);
            if (d.ChanceBp < 0 || d.ChanceBp > WeatherEventDef.ChanceBpMax)
            {
                throw WeatherEventsBroken("chance out of range");
            }
            d.MaxPerDay = WeatherEventsI32(ref r);
            if (d.MaxPerDay < 0)
            {
                throw WeatherEventsBroken("negative day cap");
            }
            d.IsInstant = WeatherEventsBool(ref r);
            d.DurationTicks = WeatherEventsI32(ref r);
            if (d.DurationTicks < 0 || (d.IsInstant && d.DurationTicks != 0))
            {
                throw WeatherEventsBroken("negative duration, or instant event with a duration");
            }
            if (!r.TryI64(out long edge))
            {
                throw WeatherEventsBroken("truncated entry");
            }
            if (edge < 0)
            {
                throw WeatherEventsBroken("negative edge width");
            }
            d.EdgeWidth = Fix64.FromRaw(edge);
            d.VariantCount = WeatherEventsI32(ref r);
            if (d.VariantCount < 1)
            {
                throw WeatherEventsBroken("variant count below one");
            }
            return d;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte WeatherEventsU8(ref ByteReader r)
        {
            if (!r.TryU8(out byte v))
            {
                throw WeatherEventsBroken("truncated entry");
            }
            return v;
        }

        private static bool WeatherEventsBool(ref ByteReader r)
        {
            byte v = WeatherEventsU8(ref r);
            if (v > 1)
            {
                throw WeatherEventsBroken("flag is not 0 or 1");
            }
            return v == 1;
        }

        private static int WeatherEventsI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw WeatherEventsBroken("truncated entry");
            }
            return v;
        }

        private static ContentException WeatherEventsBroken(string what) => new ContentException("content section 'weather_events': " + what);
    }
}
