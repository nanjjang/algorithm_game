using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/characters.json → 팩 절 1 characters. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Characters.cs.
    // 스키마: data/schema/characters.schema.json(꼴과 범위는 스키마가 이미 보았다).
    //   baseline     → CharacterId.Baseline(0). 속성·패시브·지팡이 특성·고유 비기는 None(spec 01 5.1 표의 "—")
    //   characters[] → id 의 CharacterId(char.1~char.5, ContentIds). 참조 id 는 ContentIds 로 열거형에 맞춘다
    // 단위(spec 01 2.3~2.5, CONTRACTS 0.3. 반올림은 가장 가까운 정수, 같으면 짝수):
    //   칸/초 → 센티(× 100), 칸 → Raw(× 2^32), 초 → 틱(× 60), 열 → HU(× 120), 열/초 → 틱당 HU(× 120 ÷ 60), 배율 → 천분율(× 1000)
    public sealed class CharactersConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Characters;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Entry>();
            entries.Add(ReadStats(file.Root.GetProperty("baseline"), CharacterId.Baseline));
            if (file.Root.TryGetProperty("characters", out JsonElement list))
            {
                int i = 0;
                foreach (JsonElement row in list.EnumerateArray())
                {
                    string at = "/characters/" + i.ToString(CultureInfo.InvariantCulture);
                    Entry? e = ReadCharacter(row, at, file, diagnostics);
                    if (e != null)
                    {
                        e.Pointer = at;
                        e.Order = entries.Count;
                        entries.Add(e);
                    }
                    i++;
                }
            }
            // 정규 순서: CharacterId 오름차순(원본의 나열 순서에 기대지 않는다). 겹침은 오류로 막으므로 쓰는 바이트의 순서는 하나다.
            // 같은 id 끼리는 원본 순서로 둔다(진단이 늘 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.Id != b.Id ? ((byte)a.Id).CompareTo((byte)b.Id) : a.Order.CompareTo(b.Order));
            for (int i = 1; i < entries.Count; i++)
            {
                if (entries[i].Id == entries[i - 1].Id)
                {
                    diagnostics.Error(file.RepoPath, entries[i].Pointer + "/id", "캐릭터 '" + ContentIds.ToId(entries[i].Id) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);       // id 가 겹치지 않으므로 CharacterId 의 값 수 이하다
            foreach (Entry e in entries)
            {
                Write(e, output);
            }
        }

        private sealed class Entry
        {
            public CharacterId Id;
            public Element Element;
            public PassiveId Passive;
            public WandTraitId WandTrait;
            public ArtId UniqueArt;
            public int MaxHpCells, WalkSpeedCenti, StanceSpeedCenti;
            public long HitRadiusRaw, BodyRadiusRaw, GrazeRadiusRaw;
            public int InvulnTicks, CrystalCap, ComboPerCrystal, CrystalLossOnHit, BaseLinkSlots, MaxMutations;
            public int CoolHuPerTick, CoolDelayTicks, CritStartHu, LockTicks, GrazeBuffTicks, GrazeBuffPermille;
            public string Pointer = "/baseline";
            public int Order;                         // 원본에서의 순서(baseline 0)
        }

        private static Entry? ReadCharacter(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParse(id, out CharacterId character))
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 캐릭터 id(char.1~char.5, spec 14 3.2)가 아니다");
                return null;
            }
            Entry e = ReadStats(row, character);
            bool ok = true;
            string element = row.GetProperty("element").GetString() ?? "";
            if (!ContentIds.TryParse(element, out e.Element))
            {
                diagnostics.Error(file.RepoPath, at + "/element", "'" + element + "' 는 속성 id(spec 14 3.1)가 아니다");
                ok = false;
            }
            string passive = row.GetProperty("passive").GetString() ?? "";
            if (!ContentIds.TryParse(passive, out e.Passive))
            {
                diagnostics.Error(file.RepoPath, at + "/passive", "'" + passive + "' 는 패시브 id(spec 14 3.2)가 아니다");
                ok = false;
            }
            string trait = row.GetProperty("wand_trait").GetString() ?? "";
            if (!ContentIds.TryParse(trait, out e.WandTrait))
            {
                diagnostics.Error(file.RepoPath, at + "/wand_trait", "'" + trait + "' 는 지팡이 특성 id(spec 14 3.2)가 아니다");
                ok = false;
            }
            string art = row.GetProperty("unique_art").GetString() ?? "";
            if (!ContentIds.TryParse(art, out e.UniqueArt))
            {
                diagnostics.Error(file.RepoPath, at + "/unique_art", "'" + art + "' 는 비기 id(spec 14 3.4)가 아니다");
                ok = false;
            }
            return ok ? e : null;
        }

        private static Entry ReadStats(JsonElement o, CharacterId id)
        {
            var e = new Entry { Id = id };
            e.MaxHpCells = Int(o, "max_hp_cells");
            e.WalkSpeedCenti = Units.ToCenti(Dec(o, "walk_speed"));
            e.StanceSpeedCenti = Units.ToCenti(Dec(o, "stance_speed"));
            e.HitRadiusRaw = Units.CellsToRaw(Dec(o, "hit_radius"));
            e.BodyRadiusRaw = Units.CellsToRaw(Dec(o, "body_radius"));
            e.GrazeRadiusRaw = Units.CellsToRaw(Dec(o, "graze_radius"));
            e.InvulnTicks = Units.SecondsToTicks(Dec(o, "invuln_seconds"));
            e.CrystalCap = Int(o, "crystal_cap");
            e.ComboPerCrystal = Int(o, "combo_per_crystal");
            e.CrystalLossOnHit = Int(o, "crystal_loss_on_hit");
            e.BaseLinkSlots = Int(o, "base_link_slots");
            e.MaxMutations = Int(o, "max_mutations");
            e.CoolHuPerTick = HeatPerSecondToHuPerTick(Dec(o, "cool_per_second"));
            e.CoolDelayTicks = Units.SecondsToTicks(Dec(o, "cool_delay_seconds"));
            e.CritStartHu = Units.HeatToHu(Dec(o, "crit_start"));
            e.LockTicks = Units.SecondsToTicks(Dec(o, "lock_seconds"));
            e.GrazeBuffTicks = Units.SecondsToTicks(Dec(o, "graze_buff_seconds"));
            e.GrazeBuffPermille = Units.RatioToPermille(Dec(o, "graze_buff_speed"));
            return e;
        }

        // 냉각 R 열/초 → 틱당 R × 120 ÷ 60 HU(spec 01 2.4: 45 → 90, 50 → 100)
        private static int HeatPerSecondToHuPerTick(decimal heatPerSecond)
        {
            decimal hu = Units.RoundHalfEven(heatPerSecond, Units.HuPerHeat, Units.TicksPerSecond);
            if (hu < int.MinValue || hu > int.MaxValue)
            {
                throw new DataBuildException("냉각 " + heatPerSecond.ToString(CultureInfo.InvariantCulture) + " 열/초를 바꾼 틱당 HU 가 32비트 범위를 넘는다");
            }
            return (int)hu;
        }

        private static decimal Dec(JsonElement o, string name) => o.GetProperty(name).GetDecimal();

        // 스키마의 "integer" 는 6.0 도 받으므로 decimal 로 읽어 정수인지 본다
        private static int Int(JsonElement o, string name)
        {
            decimal v = o.GetProperty(name).GetDecimal();
            if (v != decimal.Truncate(v) || v < int.MinValue || v > int.MaxValue)
            {
                throw new DataBuildException(name + " 값 " + v.ToString(CultureInfo.InvariantCulture) + " 은 32비트 정수가 아니다");
            }
            return (int)v;
        }

        private static void Write(Entry e, ByteWriter w)
        {
            w.U8((byte)e.Id);
            w.U8((byte)e.Element);
            w.U8((byte)e.Passive);
            w.U8((byte)e.WandTrait);
            w.U8((byte)e.UniqueArt);
            w.I32(e.MaxHpCells);
            w.I32(e.WalkSpeedCenti);
            w.I32(e.StanceSpeedCenti);
            w.I64(e.HitRadiusRaw);
            w.I64(e.BodyRadiusRaw);
            w.I64(e.GrazeRadiusRaw);
            w.I32(e.InvulnTicks);
            w.I32(e.CrystalCap);
            w.I32(e.ComboPerCrystal);
            w.I32(e.CrystalLossOnHit);
            w.I32(e.BaseLinkSlots);
            w.I32(e.MaxMutations);
            w.I32(e.CoolHuPerTick);
            w.I32(e.CoolDelayTicks);
            w.I32(e.CritStartHu);
            w.I32(e.LockTicks);
            w.I32(e.GrazeBuffTicks);
            w.I32(e.GrazeBuffPermille);
        }
    }
}
