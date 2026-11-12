using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 10 tod(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/TodConverter.cs. 원본: data/sim/tod.json.
    // 절 바이트(LE, 모든 수는 이미 정수):
    //   u16 개수
    //   항목 × 개수(TimeOfDay 오름차순, 겹침 없음, None 없음):
    //     u8 id  u8 normal_enemies(0 | 1)
    //     u16 가중치 수, (u16 species, i32 weight_pct) × 그 수    species 오름차순, 겹침 없음, None 없음
    //     i32 rim_strength_permille
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값, 0/1 이 아닌 bool)는 ContentException.
    public sealed partial class ContentDb
    {
        // TimeOfDay 값으로 찾는 표. 비어 있는 칸은 그 시간대가 팩에 없다는 뜻이다
        private TimeOfDayDef?[] _tods = Array.Empty<TimeOfDayDef?>();

        partial void LoadTod(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            ushort count = TodU16(ref r);
            var defs = new TimeOfDayDef[count];
            int previous = (int)Wizard.Content.TimeOfDay.None;
            for (int i = 0; i < count; i++)
            {
                var d = new TimeOfDayDef();
                byte id = TodU8(ref r);
                if (!Enum.IsDefined(typeof(Wizard.Content.TimeOfDay), id))
                {
                    throw TodBroken("unknown time of day");
                }
                if (id <= previous)
                {
                    throw TodBroken("entries not in ascending id order (or id none)");
                }
                previous = id;
                d.Id = (Wizard.Content.TimeOfDay)id;
                byte normal = TodU8(ref r);
                if (normal > 1)
                {
                    throw TodBroken("normal_enemies is not 0 or 1");
                }
                d.NormalEnemies = normal == 1;
                ushort weights = TodU16(ref r);
                d.WeightSpecies = new EnemyTypeId[weights];
                d.WeightPct = new int[weights];
                int species = 0;
                for (int k = 0; k < weights; k++)
                {
                    d.WeightSpecies[k] = FramesSpecies(ref r, "tod", ref species);
                    d.WeightPct[k] = TodI32(ref r);
                }
                d.RimStrengthPermille = TodI32(ref r);
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw TodBroken("trailing bytes");
            }
            var table = new TimeOfDayDef?[previous + 1];
            foreach (TimeOfDayDef d in defs)
            {
                table[(int)d.Id] = d;
            }
            _tods = table;
        }

        partial void FindTimeOfDay(TimeOfDay tod, ref TimeOfDayDef? def)
        {
            int i = (int)tod;
            if (i < _tods.Length)
            {
                def = _tods[i];
            }
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte TodU8(ref ByteReader r)
        {
            if (!r.TryU8(out byte v))
            {
                throw TodBroken("truncated entry");
            }
            return v;
        }

        private static ushort TodU16(ref ByteReader r)
        {
            if (!r.TryU16(out ushort v))
            {
                throw TodBroken("truncated entry");
            }
            return v;
        }

        private static int TodI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw TodBroken("truncated entry");
            }
            return v;
        }

        private static ContentException TodBroken(string what) => new ContentException("content section 'tod': " + what);
    }
}
