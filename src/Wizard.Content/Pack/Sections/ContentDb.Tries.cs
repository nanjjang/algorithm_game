using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 14 tries(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/TriesConverter.cs. 원본: data/sim/tries.json.
    // 절 바이트(LE, 모든 수는 이미 정수. 시간은 입력 시각 단위 1/15,360초):
    //   i64 just_window  i64 gap_full_bloom  i64 gap_short  i64 gap_same_side  i64 gap_default  i64 art_cooldown  i64 k_buffer_window
    //   i32 max_debt
    //   u16 개수
    //   항목 × 개수(CharacterId 오름차순, 겹침 없음): u8 character  u8 art_count  u8 art × art_count(ArtId 오름차순, 겹침 없음)
    // 판정기 상수와 빚 상한은 절에 한 벌이고 캐릭터마다 같은 값을 TrieDef 에 싣는다.
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값, 음수 시간, 보관 창 > 재사용 대기)는 ContentException.
    public sealed partial class ContentDb
    {
        // CharacterId 값으로 찾는 표. 비어 있는 칸은 그 캐릭터가 팩에 없다는 뜻이다
        private TrieDef?[] _tries = Array.Empty<TrieDef?>();

        partial void LoadTries(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            long just = TrieTime(ref r);
            long gapBloom = TrieTime(ref r);
            long gapShort = TrieTime(ref r);
            long gapSide = TrieTime(ref r);
            long gapDefault = TrieTime(ref r);
            long cooldown = TrieTime(ref r);
            long buffer = TrieTime(ref r);
            if (buffer > cooldown)
            {
                throw TriesBroken("K buffer window longer than the cooldown");
            }
            if (!r.TryI32(out int maxDebt) || maxDebt < 0)
            {
                throw TriesBroken("truncated or negative max debt");
            }
            if (!r.TryU16(out ushort count))
            {
                throw TriesBroken("truncated count");
            }
            var defs = new TrieDef[count];
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                var d = new TrieDef
                {
                    Character = (CharacterId)TrieEnumByte(ref r, typeof(CharacterId)),
                    JustWindowUnits = just,
                    GapFullBloomUnits = gapBloom,
                    GapShortUnits = gapShort,
                    GapSameSideUnits = gapSide,
                    GapDefaultUnits = gapDefault,
                    ArtCooldownUnits = cooldown,
                    KBufferWindowUnits = buffer,
                    MaxDebt = maxDebt,
                };
                if ((int)d.Character <= previous)
                {
                    throw TriesBroken("entries not in ascending character order");
                }
                previous = (int)d.Character;
                if (!r.TryU8(out byte artCount))
                {
                    throw TriesBroken("truncated entry");
                }
                var arts = new ArtId[artCount];
                int previousArt = -1;
                for (int k = 0; k < artCount; k++)
                {
                    var a = (ArtId)TrieEnumByte(ref r, typeof(ArtId));
                    if ((int)a <= previousArt || a == ArtId.None)
                    {
                        throw TriesBroken("arts not in ascending id order");
                    }
                    previousArt = (int)a;
                    arts[k] = a;
                }
                d.Arts = arts;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw TriesBroken("trailing bytes");
            }
            var table = new TrieDef?[previous + 1];
            foreach (TrieDef d in defs)
            {
                table[(int)d.Character] = d;
            }
            _tries = table;
        }

        partial void FindTries(CharacterId id, ref TrieDef? def)
        {
            int i = (int)id;
            if (i >= 0 && i < _tries.Length)
            {
                def = _tries[i];
            }
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static long TrieTime(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw TriesBroken("truncated parser constants");
            }
            if (v < 0)
            {
                throw TriesBroken("negative parser time");
            }
            return v;
        }

        private static byte TrieEnumByte(ref ByteReader r, Type enumType)
        {
            if (!r.TryU8(out byte v))
            {
                throw TriesBroken("truncated entry");
            }
            if (!Enum.IsDefined(enumType, v))
            {
                throw TriesBroken("unknown enum value");
            }
            return v;
        }

        private static ContentException TriesBroken(string what) => new ContentException("content section 'tries': " + what);
    }
}
