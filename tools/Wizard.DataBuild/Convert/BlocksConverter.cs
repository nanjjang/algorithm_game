using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Wizard.Content;
using Wizard.Core;
using Wizard.DataBuild.Core;

// 네임스페이스는 폴더의 앞머리(Wizard.DataBuild)다. Wizard.DataBuild.Convert 로 두면 이 어셈블리 안의 System.Convert 호출(Names/CSharpLexer.cs)이 가려진다
namespace Wizard.DataBuild
{
    // data/sim/blocks.json → 팩 절 2 blocks. 읽는 쪽과 절 바이트의 꼴: src/Wizard.Content/Pack/Sections/ContentDb.Blocks.cs.
    // 스키마: data/schema/blocks.schema.json(꼴과 범위는 스키마가 이미 보았다).
    //   id      → BlockId(spec 14 3.3, ContentIds)         kind → BlockKind(projectile 1, control 2, modifier 3)
    //   rarity  → Rarity(common 1, rare 2, hero 3)          element → Element(ContentIds)
    // 단위(spec 02 4.2, spec 01 2.4·2.5, CONTRACTS 0.3. 반올림은 가장 가까운 정수, 같으면 짝수):
    //   열 → HU(× 120), 배율 → 천분율(× 1000), 피해 → Fix64 Raw(× 2^32), 초 → 틱(× 60), 칸/초 → 틱당 Raw(× 2^32 ÷ 60), 칸 → Raw
    // Flags·Effect 는 아직 비트·값이 없어 0 을 쓴다. BlockParams 도 아직 필드가 없다(M2-SPL-01 이 원본·스키마·이 파일·읽는 쪽을 같이 늘린다).
    public sealed class BlocksConverter : ISectionConverter
    {
        public ContentSection Section => ContentSection.Blocks;

        public void Convert(DataFile file, ByteWriter output, Diagnostics diagnostics)
        {
            int before = diagnostics.Count;
            var entries = new List<Entry>();
            int i = 0;
            foreach (JsonElement row in file.Root.GetProperty("blocks").EnumerateArray())
            {
                string at = "/blocks/" + i.ToString(CultureInfo.InvariantCulture);
                Entry? e = Read(row, at, file, diagnostics);
                if (e != null)
                {
                    e.Order = entries.Count;
                    entries.Add(e);
                }
                i++;
            }
            // 정규 순서: BlockId 오름차순(원본의 나열 순서에 기대지 않는다). 겹침은 오류로 막으므로 쓰는 바이트의 순서는 하나다.
            // 같은 id 끼리는 원본 순서로 둔다(진단이 늘 뒤에 나온 줄을 가리키게)
            entries.Sort((a, b) => a.Id != b.Id ? ((byte)a.Id).CompareTo((byte)b.Id) : a.Order.CompareTo(b.Order));
            for (int k = 1; k < entries.Count; k++)
            {
                if (entries[k].Id == entries[k - 1].Id)
                {
                    diagnostics.Error(file.RepoPath, entries[k].Pointer + "/id", "블록 '" + ContentIds.ToId(entries[k].Id) + "' 가 두 번 있다");
                }
            }
            if (diagnostics.Count != before)
            {
                return;
            }
            output.U16((ushort)entries.Count);       // id 가 겹치지 않으므로 BlockId 의 값 수 이하다
            foreach (Entry e in entries)
            {
                Write(e, output);
            }
        }

        private sealed class Entry
        {
            public BlockId Id;
            public BlockKind Kind;
            public Rarity Rarity;
            public Element Element;
            public int HeatHu, HeatMulPermille, SplitCount, IntervalTicks, DamageBonusPermille;
            public long BaseDamageRaw, SpeedPerTickRaw, RangeRaw, HitRadiusRaw;
            public bool IsInstant;
            public string Pointer = "";
            public int Order;                         // 원본에서의 순서
        }

        private static Entry? Read(JsonElement row, string at, DataFile file, Diagnostics diagnostics)
        {
            var e = new Entry { Pointer = at };
            bool ok = true;
            string id = row.GetProperty("id").GetString() ?? "";
            if (!ContentIds.TryParse(id, out e.Id) || e.Id == BlockId.None)
            {
                diagnostics.Error(file.RepoPath, at + "/id", "'" + id + "' 는 블록 id(spec 14 3.3)가 아니다");
                ok = false;
            }
            string element = row.GetProperty("element").GetString() ?? "";
            if (!ContentIds.TryParse(element, out e.Element))
            {
                diagnostics.Error(file.RepoPath, at + "/element", "'" + element + "' 는 속성 id(spec 14 3.1)가 아니다");
                ok = false;
            }
            e.Kind = Kind(row.GetProperty("kind").GetString());
            e.Rarity = RarityOf(row.GetProperty("rarity").GetString());
            e.HeatHu = Units.HeatToHu(Dec(row, "heat"));
            e.HeatMulPermille = Units.RatioToPermille(Dec(row, "heat_multiplier"));
            e.SplitCount = Int(row, "split_count");
            e.BaseDamageRaw = FixRaw(Dec(row, "base_damage"));
            e.IntervalTicks = Units.SecondsToTicks(Dec(row, "interval_seconds"));
            e.SpeedPerTickRaw = Units.CellsPerSecondToRawPerTick(Dec(row, "speed"));
            e.IsInstant = row.GetProperty("instant").GetBoolean();
            e.RangeRaw = Units.CellsToRaw(Dec(row, "range"));
            e.HitRadiusRaw = Units.CellsToRaw(Dec(row, "hit_radius"));
            e.DamageBonusPermille = Units.RatioToPermille(Dec(row, "damage_bonus"));
            // spec 02 4.2: 즉발이면 속도 0 + IsInstant
            if (e.IsInstant && e.SpeedPerTickRaw != 0)
            {
                diagnostics.Error(file.RepoPath, at + "/speed", "즉발 블록의 speed 는 0 이다(spec 02 4.2)");
                ok = false;
            }
            return ok ? e : null;
        }

        // 스키마의 enum 이 이미 거른 값이다
        private static BlockKind Kind(string? s)
        {
            switch (s)
            {
                case "projectile": return BlockKind.Projectile;
                case "control": return BlockKind.Control;
                case "modifier": return BlockKind.Modifier;
                default: throw new DataBuildException("모르는 블록 종류 '" + s + "'");
            }
        }

        private static Rarity RarityOf(string? s)
        {
            switch (s)
            {
                case "common": return Rarity.Common;
                case "rare": return Rarity.Rare;
                case "hero": return Rarity.Hero;
                default: throw new DataBuildException("모르는 등급 '" + s + "'");
            }
        }

        // Fix64 의 값(칸이 아닌 수, 예: 피해) → Raw(× 2^32)
        private static long FixRaw(decimal v)
        {
            decimal raw = Units.RoundHalfEven(v, Fix64.OneRaw, 1);
            if (raw < long.MinValue || raw > long.MaxValue)
            {
                throw new DataBuildException("값 " + v.ToString(CultureInfo.InvariantCulture) + " 을 바꾼 Raw 가 64비트 범위를 넘는다");
            }
            return (long)raw;
        }

        private static decimal Dec(JsonElement o, string name) => o.GetProperty(name).GetDecimal();

        // 스키마의 "integer" 는 2.0 도 받으므로 decimal 로 읽어 정수인지 본다
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
            w.U8((byte)e.Kind);
            w.U8((byte)e.Rarity);
            w.U8((byte)e.Element);
            w.I32(e.HeatHu);
            w.I32(e.HeatMulPermille);
            w.I32(e.SplitCount);
            w.I64(e.BaseDamageRaw);
            w.I32(e.IntervalTicks);
            w.I64(e.SpeedPerTickRaw);
            w.U8(e.IsInstant ? (byte)1 : (byte)0);
            w.I64(e.RangeRaw);
            w.I64(e.HitRadiusRaw);
            w.I32(e.DamageBonusPermille);
            w.U16(0);                                 // ProjectileFlags.None
            w.U8(0);                                  // ModifierEffect.None
        }
    }
}
