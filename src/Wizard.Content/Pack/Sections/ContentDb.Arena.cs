using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 12 arena(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/ArenaConverter.cs. 원본: data/sim/arena.json.
    // 절 바이트(LE, 모든 수는 이미 정수):
    //   u16 개수
    //   항목 × 개수(SimMode 오름차순, 겹침 없음. 원본에서 전장 하나가 모드 여럿에 쓰이면 모드마다 같은 값이 한 번씩 들어 있다):
    //     u8 mode  i64 width_raw  i64 height_raw
    //     u16 기둥 수, (i64 x_raw, i64 y_raw) × 기둥 수      기둥 칸의 작은 쪽 모서리(ArenaDef 주석)
    //     u16 시작 위치 수, (i64 x_raw, i64 y_raw) × 그 수   슬롯 순
    // 깨진 바이트(모자람, 남음, 순서, 모르는 모드)는 ContentException.
    public sealed partial class ContentDb
    {
        // SimMode 값으로 찾는 표. 비어 있는 칸은 그 모드의 전장이 팩에 없다는 뜻이다
        private ArenaDef?[] _arenas = Array.Empty<ArenaDef?>();

        partial void LoadArena(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            ushort count = ArenaU16(ref r);
            var modes = new SimMode[count];
            var defs = new ArenaDef[count];
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                if (!r.TryU8(out byte mode))
                {
                    throw ArenaBroken("truncated entry");
                }
                if (!Enum.IsDefined(typeof(SimMode), mode))
                {
                    throw ArenaBroken("unknown mode");
                }
                if (mode <= previous)
                {
                    throw ArenaBroken("entries not in ascending mode order");
                }
                previous = mode;
                var d = new ArenaDef();
                d.Width = Fix64.FromRaw(ArenaI64(ref r));
                d.Height = Fix64.FromRaw(ArenaI64(ref r));
                d.Pillars = ArenaPoints(ref r);
                d.SpawnPoints = ArenaPoints(ref r);
                modes[i] = (SimMode)mode;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw ArenaBroken("trailing bytes");
            }
            var table = new ArenaDef?[previous + 1];
            for (int i = 0; i < count; i++)
            {
                table[(int)modes[i]] = defs[i];
            }
            _arenas = table;
        }

        partial void FindArena(SimMode mode, ref ArenaDef? def)
        {
            int i = (int)mode;
            if (i < _arenas.Length)
            {
                def = _arenas[i];
            }
        }

        private static FixVec2[] ArenaPoints(ref ByteReader r)
        {
            ushort n = ArenaU16(ref r);
            if (n == 0)
            {
                return Array.Empty<FixVec2>();
            }
            var points = new FixVec2[n];
            for (int i = 0; i < n; i++)
            {
                Fix64 x = Fix64.FromRaw(ArenaI64(ref r));
                Fix64 y = Fix64.FromRaw(ArenaI64(ref r));
                points[i] = new FixVec2(x, y);
            }
            return points;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static ushort ArenaU16(ref ByteReader r)
        {
            if (!r.TryU16(out ushort v))
            {
                throw ArenaBroken("truncated entry");
            }
            return v;
        }

        private static long ArenaI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw ArenaBroken("truncated entry");
            }
            return v;
        }

        private static ContentException ArenaBroken(string what) => new ContentException("content section 'arena': " + what);
    }
}
