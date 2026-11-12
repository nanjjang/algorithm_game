using System;
using Wizard.Core;

namespace Wizard.Content
{
    // 팩 절 1 characters(CONTRACTS 11.2)를 읽는다. 쓰는 쪽: tools/Wizard.DataBuild/Convert/CharactersConverter.cs. 원본: data/sim/characters.json.
    // 절 바이트(LE, 모든 수는 이미 정수):
    //   u16 개수
    //   항목 × 개수(CharacterId 오름차순, 겹침 없음):
    //     u8 id  u8 element  u8 passive  u8 wand_trait  u8 unique_art
    //     i32 max_hp_cells  i32 walk_speed_centi  i32 stance_speed_centi
    //     i64 hit_radius_raw  i64 body_radius_raw  i64 graze_radius_raw
    //     i32 invuln_ticks  i32 crystal_cap  i32 combo_per_crystal  i32 crystal_loss_on_hit
    //     i32 base_link_slots  i32 max_mutations
    //     i32 cool_hu_per_tick  i32 cool_delay_ticks  i32 crit_start_hu  i32 lock_ticks
    //     i32 graze_buff_ticks  i32 graze_buff_permille
    // 결투 덮어쓰기(CharacterDef.Duel)는 아직 필드가 없어 절에 없다(spec 04 8절의 데이터 작업이 끝에 더한다).
    // 깨진 바이트(모자람, 남음, 순서, 모르는 열거형 값)는 ContentException.
    public sealed partial class ContentDb
    {
        // CharacterId 값으로 찾는 표. 비어 있는 칸은 그 id 가 팩에 없다는 뜻이다
        private CharacterDef?[] _characters = Array.Empty<CharacterDef?>();

        partial void LoadCharacters(ReadOnlySpan<byte> section)
        {
            var r = new ByteReader(section);
            if (!r.TryU16(out ushort count))
            {
                throw CharactersBroken("truncated count");
            }
            var defs = new CharacterDef[count];
            int previous = -1;
            for (int i = 0; i < count; i++)
            {
                CharacterDef d = ReadCharacterEntry(ref r);
                if ((int)d.Id <= previous)
                {
                    throw CharactersBroken("entries not in ascending id order");
                }
                previous = (int)d.Id;
                defs[i] = d;
            }
            if (r.Remaining != 0)
            {
                throw CharactersBroken("trailing bytes");
            }
            var table = new CharacterDef?[previous + 1];
            foreach (CharacterDef d in defs)
            {
                table[(int)d.Id] = d;
            }
            _characters = table;
        }

        partial void FindCharacter(CharacterId id, ref CharacterDef? def)
        {
            int i = (int)id;
            if (i < _characters.Length)
            {
                def = _characters[i];
            }
        }

        private static CharacterDef ReadCharacterEntry(ref ByteReader r)
        {
            var d = new CharacterDef();
            d.Id = (CharacterId)CharacterEnumByte(ref r, typeof(CharacterId));
            d.Element = (Element)CharacterEnumByte(ref r, typeof(Element));
            d.Passive = (PassiveId)CharacterEnumByte(ref r, typeof(PassiveId));
            d.WandTrait = (WandTraitId)CharacterEnumByte(ref r, typeof(WandTraitId));
            d.UniqueArt = (ArtId)CharacterEnumByte(ref r, typeof(ArtId));
            d.MaxHpCells = CharacterI32(ref r);
            d.WalkSpeedCenti = CharacterI32(ref r);
            d.StanceSpeedCenti = CharacterI32(ref r);
            d.HitRadius = Fix64.FromRaw(CharacterI64(ref r));
            d.BodyRadius = Fix64.FromRaw(CharacterI64(ref r));
            d.GrazeRadius = Fix64.FromRaw(CharacterI64(ref r));
            d.InvulnTicks = CharacterI32(ref r);
            d.CrystalCap = CharacterI32(ref r);
            d.ComboPerCrystal = CharacterI32(ref r);
            d.CrystalLossOnHit = CharacterI32(ref r);
            d.BaseLinkSlots = CharacterI32(ref r);
            d.MaxMutations = CharacterI32(ref r);
            d.CoolHuPerTick = CharacterI32(ref r);
            d.CoolDelayTicks = CharacterI32(ref r);
            d.CritStartHu = CharacterI32(ref r);
            d.LockTicks = CharacterI32(ref r);
            d.GrazeBuffTicks = CharacterI32(ref r);
            d.GrazeBuffPermille = CharacterI32(ref r);
            return d;
        }

        // 부분 클래스라 다른 절 파일과 이름이 겹치지 않게 절 이름을 붙인다
        private static byte CharacterEnumByte(ref ByteReader r, Type enumType)
        {
            if (!r.TryU8(out byte v))
            {
                throw CharactersBroken("truncated entry");
            }
            if (!Enum.IsDefined(enumType, v))
            {
                throw CharactersBroken("unknown enum value");
            }
            return v;
        }

        private static int CharacterI32(ref ByteReader r)
        {
            if (!r.TryI32(out int v))
            {
                throw CharactersBroken("truncated entry");
            }
            return v;
        }

        private static long CharacterI64(ref ByteReader r)
        {
            if (!r.TryI64(out long v))
            {
                throw CharactersBroken("truncated entry");
            }
            return v;
        }

        private static ContentException CharactersBroken(string what) => new ContentException("content section 'characters': " + what);
    }
}
