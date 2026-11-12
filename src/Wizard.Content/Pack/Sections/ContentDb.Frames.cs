using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 8 frames(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/FramesConverter.cs. 원본: data/sim/frames.json.
    // 절 바이트(LE, 모든 수는 이미 정수):
    //   u16 개수
    //   틀 × 개수(FrameId 오름차순, 겹침 없음):
    //     u8 frame
    //     u16 주 묶음 종 수, u16 species × 그 수              EnemyTypeId 오름차순, 겹침 없음, None 없음
    //     i32 group_share_pct
    //     u16 고정 개수 수, (u16 species, i32 min, i32 max) × 그 수   species 오름차순, 겹침 없음
    //     u8 rest_mixed(0 | 1)
    // 틀 하나의 꼴(FramePlan)은 절 7 waves 의 고정 각본도 쓴다(ContentDb.Waves.cs).
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값, 0/1 이 아닌 bool)는 ContentException.
    public sealed partial class ContentDb
    {
        private IReadOnlyList<FrameDef>? _frames;           // FrameId 오름차순(spec 07 7.3 "순서 고정")

        partial void LoadFrames(ReadOnlySpan<byte> section)
        {
            const string name = "frames";
            var r = new ByteReader(section);
            ushort count = FramesU16(ref r, name);
            var defs = new FrameDef[count];
            int previous = 0;
            for (int i = 0; i < count; i++)
            {
                FrameDef d = FramePlan(ref r, name);
                if ((int)d.Id <= previous)
                {
                    throw FramesBroken(name, "entries not in ascending frame order");
                }
                previous = (int)d.Id;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw FramesBroken(name, "trailing bytes");
            }
            _frames = new ReadOnlyCollection<FrameDef>(defs);
        }

        partial void FindFrames(ref IReadOnlyList<FrameDef>? defs)
        {
            if (_frames != null)
            {
                defs = _frames;
            }
        }

        // 틀 하나(각본 웨이브 하나). section 은 오류 문구의 절 이름이다
        private static FrameDef FramePlan(ref ByteReader r, string section)
        {
            var d = new FrameDef();
            if (!r.TryU8(out byte frame))
            {
                throw FramesBroken(section, "truncated entry");
            }
            if (!Enum.IsDefined(typeof(FrameId), frame))
            {
                throw FramesBroken(section, "unknown frame");
            }
            d.Id = (FrameId)frame;
            ushort groupCount = FramesU16(ref r, section);
            var group = new EnemyTypeId[groupCount];
            int previous = 0;
            for (int i = 0; i < groupCount; i++)
            {
                group[i] = FramesSpecies(ref r, section, ref previous);
            }
            d.GroupSpecies = group;
            d.GroupSharePct = FramesI32(ref r, section);
            ushort fixedCount = FramesU16(ref r, section);
            var species = new EnemyTypeId[fixedCount];
            var min = new int[fixedCount];
            var max = new int[fixedCount];
            previous = 0;
            for (int i = 0; i < fixedCount; i++)
            {
                species[i] = FramesSpecies(ref r, section, ref previous);
                min[i] = FramesI32(ref r, section);
                max[i] = FramesI32(ref r, section);
            }
            d.FixedSpecies = species;
            d.FixedMin = min;
            d.FixedMax = max;
            d.RestMixed = FramesBool(ref r, section);
            return d;
        }

        // 종 id 하나. EnemyTypeId 에 있고 None 이 아니며 앞의 것보다 커야 한다(오름차순, 겹침 없음)
        private static EnemyTypeId FramesSpecies(ref ByteReader r, string section, ref int previous)
        {
            ushort v = FramesU16(ref r, section);
            if (v == (ushort)EnemyTypeId.None || !Enum.IsDefined(typeof(EnemyTypeId), v))
            {
                throw FramesBroken(section, "unknown species");
            }
            if (v <= previous)
            {
                throw FramesBroken(section, "species not in ascending order");
            }
            previous = v;
            return (EnemyTypeId)v;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static ushort FramesU16(ref ByteReader r, string section)
        {
            if (!r.TryU16(out ushort v))
            {
                throw FramesBroken(section, "truncated entry");
            }
            return v;
        }

        private static int FramesI32(ref ByteReader r, string section)
        {
            if (!r.TryI32(out int v))
            {
                throw FramesBroken(section, "truncated entry");
            }
            return v;
        }

        private static bool FramesBool(ref ByteReader r, string section)
        {
            if (!r.TryU8(out byte v))
            {
                throw FramesBroken(section, "truncated entry");
            }
            if (v > 1)
            {
                throw FramesBroken(section, "bool is not 0 or 1");
            }
            return v == 1;
        }

        private static ContentException FramesBroken(string section, string what) => new ContentException("content section '" + section + "': " + what);
    }
}
