using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Meta;
using Wizard.Solo.Screens.Camp;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Screens.Hints
{
    /// <summary>도감 한 줄: 머리(이름) + 본문. 머리가 null이면 설명 문단.</summary>
    public struct CodexRow
    {
        public string Head;
        public string Body;
        /// <summary>그림 아이콘 id(블록·비기·변이·변덕). null = 없음.</summary>
        public string IconId;
        /// <summary>오른쪽 작은 글(비용·점수 배율·커맨드).</summary>
        public string Side;
        /// <summary>둘째 본문 줄(변이의 대가 등). null = 없음.</summary>
        public string Body2;
    }

    /// <summary>
    /// 조작·도감 화면의 내용(순수). 분류마다 줄 목록을 만든다. 글은 이름 표(str.codex.*, desc.*, 블록은 WP3의 BlockDescriber),
    /// 수치는 데이터·지금 판의 캐릭터(판이 없으면 기준 캐릭터)에서 읽어 서식으로 넣는다.
    /// </summary>
    public static class CodexModel
    {
        public const string Controls = "controls", Heat = "heat", Graze = "graze", Combo = "combo", Arts = "arts", Blocks = "blocks",
            Mutations = "mutations", Whims = "whims", Affinity = "affinity", Styles = "styles";

        public static readonly string[] Sections = { Controls, Heat, Graze, Combo, Arts, Styles, Blocks, Mutations, Whims, Affinity };

        public static string TabKey(string section) => "str.codex.tab." + section;
        public static string LineKey(string section, int n) => "str.codex." + section + ".line." + n;
        /// <summary>문단 줄 수의 상한(자료구조 크기).</summary>
        public const int MaxLines = 12;

        public static readonly string[] MutationIds =
        {
            "mutation.twisted_barrel", "mutation.unstable_core", "mutation.hypersensitive_circuit",
            "mutation.gluttony", "mutation.mirror_image", "mutation.glass_wand"
        };

        /// <summary>문단의 값 묶음: 키 이름(지금 키 배치)과 캐릭터 수치.</summary>
        public static object[] Args(IContentDb db, CharacterDef ch, PlayerState p, System.Func<LogicalKey, string> keyName)
        {
            System.Func<LogicalKey, string> k = keyName ?? (x => x.ToString());
            float crit = p != null ? p.Derived.CritStartHu / (float)SimConst.HuPerHeat : ch != null ? ch.CritStart : 0f;
            float lockSec = p != null && p.Derived.LockTicks > 0 ? p.Derived.LockTicks / (float)SimConst.TicksPerSecond : ch != null ? ch.LockSeconds : 0f;
            return new object[]
            {
                "up", k(LogicalKey.Up), "left", k(LogicalKey.Left), "down", k(LogicalKey.Down), "right", k(LogicalKey.Right),
                "fire", k(LogicalKey.Fire), "art", k(LogicalKey.Skill), "stance", k(LogicalKey.Stance), "reverse", k(LogicalKey.Reverse),
                "pause", k(LogicalKey.Pause),
                "crit", DescribeFormat.Num(crit),
                "lock_sec", DescribeFormat.Num(lockSec),
                "cool", DescribeFormat.Num(ch != null ? ch.CoolPerSecond : 0f),
                "graze_r", DescribeFormat.Num(ch != null ? ch.GrazeRadius : 0f),
                "buff_sec", DescribeFormat.Num(ch != null ? ch.GrazeBuffSeconds : 0f),
                "combo_n", ch != null ? ch.ComboPerCrystal : 0,
                "cap", ch != null ? ch.CrystalCap : 0,
                "loss", ch != null ? ch.CrystalLossOnHit : 0,
                "weak", DescribeFormat.Num(db != null && db.Elements != null ? db.Elements.WeakMultiplier : 0f),
                "resist", DescribeFormat.Num(db != null && db.Elements != null ? db.Elements.ResistMultiplier : 0f)
            };
        }

        /// <summary>한 분류의 줄들. charId = 비기 목록의 캐릭터(트라이 11종).</summary>
        public static List<CodexRow> Rows(string section, INameTable names, IContentDb db, string charId, object[] args)
        {
            var rows = new List<CodexRow>();
            for (int i = 1; i <= MaxLines; i++)
            {
                string key = LineKey(section, i);
                if (names == null || !names.Has(key)) break;
                rows.Add(new CodexRow { Body = names.Format(key, args) });
            }
            switch (section)
            {
                case Arts: AddArts(rows, names, db, charId); break;
                case Blocks: AddBlocks(rows, names, db); break;
                case Styles: AddStyles(rows, names, db); break;
                case Mutations:
                    foreach (var id in MutationIds)
                        rows.Add(new CodexRow
                        {
                            Head = Get(names, id), IconId = id,
                            Body = MutationDescriber.Gain(names, db, id), Body2 = MutationDescriber.Cost(names, db, id)
                        });
                    break;
                case Whims:
                    if (db != null && db.Modifiers != null)
                        foreach (var m in db.Modifiers)
                            rows.Add(new CodexRow
                            {
                                Head = Get(names, m.Id), IconId = m.Id,
                                Body = WhimDescriber.OneLine(names, db, m.Id), Side = WhimDescriber.ScoreLine(names, db, m.Id)
                            });
                    break;
                case Affinity:
                    rows.Add(new CodexRow { Head = Get(names, "str.codex.affinity.weak_head"), Body = AffinityDescriber.Weak(names, db) });
                    rows.Add(new CodexRow { Head = Get(names, "str.codex.affinity.resist_head"), Body = AffinityDescriber.Resist(names, db) });
                    break;
            }
            return rows;
        }

        static void AddArts(List<CodexRow> rows, INameTable names, IContentDb db, string charId)
        {
            if (db == null) return;
            var ids = charId != null ? db.TrieArts(charId) : null;
            if (ids == null || ids.Count == 0) return;
            foreach (var id in ids)
            {
                var a = db.Art(id);
                if (a == null) continue;
                rows.Add(new CodexRow
                {
                    Head = Get(names, id), IconId = id,
                    Side = Command(names, a) + "  " + ArtDescriber.CostLine(names, db, id),
                    Body = ArtDescriber.OneLine(names, db, id)
                });
            }
        }

        /// <summary>커맨드 글: 첫 경로의 입력 문양 + K(섬 기술은 섬 문양).</summary>
        public static string Command(INameTable names, ArtDef a)
        {
            if (a == null || names == null) return string.Empty;
            var s = string.Empty;
            if (a.Paths != null && a.Paths.Count > 0 && a.Paths[0] != null)
                foreach (var d in a.Paths[0]) s += names.Get(ArtTrieModel.GlyphKey(DirOf(d)));
            s += names.Get(a.Just ? ArtTrieModel.JustGlyphKey : ArtTrieModel.KGlyphKey);
            return s;
        }

        static CmdDir DirOf(string letter)
        {
            switch (letter)
            {
                case "B": return CmdDir.Back;
                case "L": return CmdDir.Left;
                case "R": return CmdDir.Right;
                default: return CmdDir.Forward;
            }
        }

        /// <summary>기본 공격 15종(Docs/12-attack-styles.md 2절): 캐릭터 순, 그 안에서 A·B·C. 본문 = 한 줄 설명, 옆 = 캐릭터 · 자리.</summary>
        static void AddStyles(List<CodexRow> rows, INameTable names, IContentDb db)
        {
            if (db == null) return;
            for (int n = 1; n <= Ids.Char.Count; n++)
            {
                string ch = Ids.Char.Of(n);
                var ids = AttackStyles.StylesOf(db, ch);
                for (int i = 0; i < ids.Count; i++)
                {
                    string id = ids[i];
                    string slot = AttackStyles.SlotOf(db, ch, id);
                    string side = names != null
                        ? names.Format("str.codex.styles.side", "char", names.Get(ch), "slot", slot != null ? names.Get(Prep.ExpeditionPrepModel.StyleSlotPrefix + slot) : string.Empty)
                        : null;
                    rows.Add(new CodexRow { Head = Get(names, id), IconId = id, Body = Get(names, Prep.ExpeditionPrepModel.StyleDescKey(id)), Side = side });
                }
            }
        }

        static void AddBlocks(List<CodexRow> rows, INameTable names, IContentDb db)
        {
            if (db == null || db.Blocks == null) return;
            foreach (var b in db.Blocks)
                rows.Add(new CodexRow { Head = Get(names, b.Id), IconId = b.Id, Body = BlockDescriber.Text(names, b, false), Side = HeatSide(names, b) });
        }

        static string HeatSide(INameTable names, BlockDef b)
        {
            if (names == null || b == null) return null;
            if (b.IsSplit) return names.Format("str.codex.blocks.heat_mul", "mul", DescribeFormat.Num(b.HeatMultiplier));
            return b.Heat > 0f ? names.Format("str.codex.blocks.heat", "heat", DescribeFormat.Num(b.Heat)) : null;
        }

        static string Get(INameTable names, string key) => names != null ? names.Get(key) : key;
    }
}
