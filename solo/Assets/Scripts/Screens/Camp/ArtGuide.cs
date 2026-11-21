using System;
using System.Collections.Generic;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Arts;

namespace Wizard.Solo.Screens.Camp
{
    /// <summary>비기 하나의 풀이(트라이 설명 판·후보 카드·도감). 글은 다 채운 것이고, 없는 줄은 null.</summary>
    public sealed class ArtGuideInfo
    {
        public string ArtId;
        /// <summary>무엇을 하는가(ArtDescriber.OneLine — 수치는 arts.json).</summary>
        public string Effect;
        /// <summary>어떻게 내는가: 지금 키 배치의 키 이름 + 방향 문양("L 누른 채 ↑ ↑ → K").</summary>
        public string HowTo;
        /// <summary>짧은 커맨드("L + ↑ ↑ + K", 마력 방출은 "K"). 카드·도감 옆 글.</summary>
        public string Short;
        /// <summary>방향 입력 사이에 허용된 가장 짧은 간격(tries.json parser). 방향이 하나 이하면 null.</summary>
        public string Gap;
        /// <summary>결정 비용 · 다음 비기까지의 대기(tries.json art_cooldown_seconds, 모든 비기 공통).</summary>
        public string Cost;
        /// <summary>결정이 모자랄 때 대신 나가는 비기(고유 비기만). 없으면 null.</summary>
        public string Fallback;
        /// <summary>배우는 법(처음부터 / 고유 / 선행 비기 + 웨이브).</summary>
        public string Unlock;
        /// <summary>이 비기를 배우면 열리는 다음 가지들. 없으면 null.</summary>
        public string Next;
        /// <summary>고유 비기 강화 한 줄(AttackStyles.json art_upgrades가 켜졌을 때만). 없으면 null.</summary>
        public string Upgrade;
    }

    /// <summary>
    /// 비기 풀이(순수, 엔진 없음). 플레이 피드백 "비기 같은데 설명 더 필요하면 좋을듯" — 트라이 그림의 문양만으로는
    /// 무엇을 하는지·어떻게 내는지·무엇을 쓰는지·다음에 무엇이 열리는지 알기 어려웠다.
    /// 문구는 이름 표(NamesSolo/ArtGuide.ko.json str.art_guide.*, 효과 한 줄은 Tutorial.ko.json desc.art.*)에만 있고,
    /// 수치는 데이터(arts.json·tries.json·AttackStyles.json)에서 읽어 {이름} 자리에 넣는다 — 문구에 숫자를 적지 않는다.
    /// 키 이름은 부르는 쪽이 지금 키 배치로 준다(keyName). 판 규칙은 바꾸지 않는다(읽기만).
    /// </summary>
    public static class ArtGuide
    {
        public const string HowToTapKey = "str.art_guide.howto.tap",
            HowToKey = "str.art_guide.howto.cmd",
            HowToJustKey = "str.art_guide.howto.just",
            MirrorKey = "str.art_guide.mirror",
            ShortKey = "str.art_guide.short",
            GapKey = "str.art_guide.gap",
            CostKey = "str.art_guide.cost",
            NoCreditKey = "str.art_guide.no_credit",
            FallbackKey = "str.art_guide.fallback",
            UnlockStartKey = "str.art_guide.unlock.start",
            UnlockUniqueKey = "str.art_guide.unlock.unique",
            UnlockPrereqKey = "str.art_guide.unlock.prereq",
            UnlockPrereqWaveKey = "str.art_guide.unlock.prereq_wave",
            NextKey = "str.art_guide.next",
            UpgradeKey = "str.art_guide.upgrade",
            UpgradeTextPrefix = "str.attack_style.art_upgrade.",
            ListSepKey = "str.art_guide.list_sep";

        /// <summary>비기 하나의 풀이. charId = 트라이의 캐릭터(다음 가지·외상 여부). keyName = 지금 키 배치의 키 이름(없으면 논리 키 이름).</summary>
        public static ArtGuideInfo Describe(INameTable names, IContentDb db, string charId, string artId, Func<LogicalKey, string> keyName)
        {
            var a = db != null && artId != null ? db.Art(artId) : null;
            var info = new ArtGuideInfo { ArtId = artId };
            if (a == null || names == null) return info;
            Func<LogicalKey, string> k = keyName ?? (x => x.ToString());
            var parser = db.Tries != null ? db.Tries.Parser : null;

            info.Effect = ArtDescriber.OneLine(names, db, artId);
            info.HowTo = HowTo(names, db, a, k);
            info.Short = Short(names, a, k);
            float gap = TightestGapSeconds(a, charId, parser);
            if (gap > 0f) info.Gap = DescribeFormat.Try(names, GapKey, "sec", DescribeFormat.Num(gap));

            string cost = ArtDescriber.CostLine(names, db, artId);
            if (cost != null)
            {
                info.Cost = DescribeFormat.Try(names, CostKey, "cost", cost, "sec", DescribeFormat.Num(parser != null ? parser.ArtCooldownSeconds : 0f));
                // 외상 지팡이(빚 상한) 캐릭터에게만 뜻이 있다: 만개처럼 credit = false인 비기는 빚으로 못 낸다.
                if (!a.Credit && HasCredit(db, charId) && info.Cost != null && names.Has(NoCreditKey))
                    info.Cost += names.Get(ListSepKey) + names.Get(NoCreditKey);
            }
            if (!string.IsNullOrEmpty(a.Fallback)) info.Fallback = DescribeFormat.Try(names, FallbackKey, "art", names.Get(a.Fallback));
            info.Unlock = Unlock(names, a);
            var next = NextArts(db, charId, artId);
            if (next.Count > 0)
            {
                string sep = names.Has(ListSepKey) ? names.Get(ListSepKey) : ", ";
                var parts = new List<string>(next.Count);
                for (int i = 0; i < next.Count; i++) parts.Add(names.Get(next[i]));
                info.Next = DescribeFormat.Try(names, NextKey, "list", string.Join(sep, parts));
            }
            if (AttackStyles.ArtUpgrade(db, artId) != null && names.Has(UpgradeTextPrefix + artId))
                info.Upgrade = DescribeFormat.Try(names, UpgradeKey, "text", names.Get(UpgradeTextPrefix + artId));
            return info;
        }

        /// <summary>방향 문양 줄("↑ ↑"). 좌우 대칭 경로는 첫 경로만(MirrorKey가 덧붙는다).</summary>
        public static string Glyphs(INameTable names, ArtDef a)
        {
            if (names == null || a == null || a.Paths == null || a.Paths.Count == 0 || a.Paths[0] == null) return string.Empty;
            var parts = new List<string>(a.Paths[0].Count);
            for (int i = 0; i < a.Paths[0].Count; i++) parts.Add(names.Get(ArtTrieModel.GlyphKey(DirOf(a.Paths[0][i]))));
            return string.Join(" ", parts);
        }

        /// <summary>어떻게 내는가. 마력 방출(빈 경로) = 비기 키만, 섬 비기 = 마지막 방향 뒤 섬 창 안에.</summary>
        public static string HowTo(INameTable names, IContentDb db, ArtDef a, Func<LogicalKey, string> k)
        {
            if (names == null || a == null) return null;
            string art = k(LogicalKey.Skill), stance = k(LogicalKey.Stance);
            string cmd = Glyphs(names, a);
            if (cmd.Length == 0) return DescribeFormat.Try(names, HowToTapKey, "art", art);
            string s;
            if (a.Just)
            {
                float just = db != null && db.Tries != null && db.Tries.Parser != null ? db.Tries.Parser.JustWindowSeconds : 0f;
                s = DescribeFormat.Try(names, HowToJustKey, "stance", stance, "cmd", cmd, "art", art, "sec", DescribeFormat.Num(just));
            }
            else s = DescribeFormat.Try(names, HowToKey, "stance", stance, "cmd", cmd, "art", art);
            if (s != null && a.Paths.Count > 1 && names.Has(MirrorKey)) s += " " + names.Get(MirrorKey);
            return s;
        }

        /// <summary>짧은 커맨드("L + ↑ ↑ + K"). 마력 방출은 비기 키 하나.</summary>
        public static string Short(INameTable names, ArtDef a, Func<LogicalKey, string> k)
        {
            if (names == null || a == null) return null;
            string art = k(LogicalKey.Skill);
            if (a.Just) art = names.Get(ArtTrieModel.JustGlyphKey) + art;
            string cmd = Glyphs(names, a);
            if (cmd.Length == 0) return art;
            return DescribeFormat.Try(names, ShortKey, "stance", k(LogicalKey.Stance), "cmd", cmd, "art", art);
        }

        /// <summary>배우는 법(arts.json unlock, 고유 비기는 캐릭터의 시작 비기 — 04 11.1절 U-R2).</summary>
        public static string Unlock(INameTable names, ArtDef a)
        {
            if (names == null || a == null) return null;
            if (a.IsUnique) return DescribeFormat.Try(names, UnlockUniqueKey);
            var u = a.Unlock;
            if (u == null || u.AtStart || string.IsNullOrEmpty(u.Prereq)) return DescribeFormat.Try(names, UnlockStartKey);
            if (u.MinWave > 0) return DescribeFormat.Try(names, UnlockPrereqWaveKey, "art", names.Get(u.Prereq), "wave", u.MinWave);
            return DescribeFormat.Try(names, UnlockPrereqKey, "art", names.Get(u.Prereq));
        }

        /// <summary>이 비기를 선행으로 두는 비기들(그 캐릭터의 트라이 안, tries.json 순서). 고유 비기는 선행이 없다.</summary>
        public static List<string> NextArts(IContentDb db, string charId, string artId)
        {
            var list = new List<string>();
            var ids = db != null && charId != null ? db.TrieArts(charId) : null;
            if (ids == null || artId == null) return list;
            for (int i = 0; i < ids.Count; i++)
            {
                var c = db.Art(ids[i]);
                if (c != null && c.Unlock != null && c.Unlock.Prereq == artId && !list.Contains(c.Id)) list.Add(c.Id);
            }
            return list;
        }

        /// <summary>
        /// 그 비기의 (첫) 경로에서 방향 입력 사이 허용 간격 중 가장 짧은 것(초). 판정기의 간선 간격 함수를 그대로 쓴다(CommandTrie.EdgeGapU).
        /// 방향이 하나 이하면 0.
        /// </summary>
        public static float TightestGapSeconds(ArtDef a, string charId, ParserDef parser)
        {
            if (a == null || parser == null || a.Paths == null || a.Paths.Count == 0 || a.Paths[0] == null || a.Paths[0].Count < 2) return 0f;
            var t = ParserTimings.From(parser);
            var path = a.Paths[0];
            float best = 0f;
            for (int len = 2; len <= path.Count; len++)
            {
                var prefix = new CmdDir[len];
                for (int i = 0; i < len; i++) prefix[i] = DirOf(path[i]);
                float sec;
                CommandTrie.EdgeGapU(prefix, charId, t, out sec);
                if (best <= 0f || sec < best) best = sec;
            }
            return best;
        }

        static bool HasCredit(IContentDb db, string charId)
        {
            var ch = db != null && charId != null ? db.Character(charId) : null;
            return ch != null && ch.WandTrait == Ids.WandTrait.OnCredit;
        }

        /// <summary>arts.json 경로 글자(F·B·L·R) → 상대 방향.</summary>
        public static CmdDir DirOf(string letter)
        {
            switch (letter)
            {
                case "B": return CmdDir.Back;
                case "L": return CmdDir.Left;
                case "R": return CmdDir.Right;
                default: return CmdDir.Forward;
            }
        }
    }
}
