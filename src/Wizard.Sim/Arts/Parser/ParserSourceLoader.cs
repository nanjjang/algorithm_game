using System;
using Wizard.Content;

namespace Wizard.Sim.Arts
{
    // 콘텐츠 팩의 arts·tries 절에서 판정기 데이터(ParserSource)를 만든다(CONTRACTS 3.5, T-52). CommandTrie.Build(character, ContentDb)가 부른다.
    //   - 판정기 상수(spec 03 1.2)와 빚 상한(9.2): tries 절. 다섯 캐릭터의 TrieDef 가 같은 값을 싣는다(절에 한 벌).
    //   - 비기: 다섯 캐릭터의 트라이에 든 비기의 합(공용 10 + 고유 5 = 15종)을 ArtId 오름차순으로. 칸은 ArtDef(arts 절)에서 옮긴다:
    //     경로 = Paths, 종류 = Kind == Unique, 주인 = Owner, 섬 = IsJust, 비용 = CostKind·Cost·MinCost, 외상 = CreditAllowed.
    // 데이터의 맞물림을 여기서 본다(어긋나면 ContentException): 캐릭터 c 의 TrieDef.Arts = 공용 전부 + c 가 주인인 고유 하나이고
    // 그 고유가 CharacterDef.UniqueArt 다(spec 03 5.1, ParserSource.IsInTrieOf 와 같은 뜻), 다섯 캐릭터의 상수가 같다.
    // 절이 팩에 없으면 접근자가 ContentException 을 던진다. 판을 시작할 때만 불린다(할당한다). Step 안에서 부르지 않는다.
    public static class ParserSourceLoader
    {
        public static ParserSource FromContent(ContentDb db)
        {
            if (db == null)
            {
                throw new ArgumentNullException(nameof(db));
            }
            TrieDef first = db.Tries(CharacterId.Char1);
            int maxId = 0;
            for (CharacterId c = CharacterId.Char1; c <= CharacterId.Char5; c++)
            {
                TrieDef t = db.Tries(c);
                if (!SameConstants(first, t))
                {
                    throw Broken("parser constants differ between characters");
                }
                foreach (ArtId a in t.Arts)
                {
                    if ((int)a > maxId)
                    {
                        maxId = (int)a;
                    }
                }
            }
            var all = new bool[maxId + 1];       // ArtId 값으로 찾는 표
            int artCount = 0;
            for (CharacterId c = CharacterId.Char1; c <= CharacterId.Char5; c++)
            {
                foreach (ArtId a in db.Tries(c).Arts)
                {
                    if (!all[(int)a])
                    {
                        all[(int)a] = true;
                        artCount++;
                    }
                }
            }

            var arts = new ParserArt[artCount];
            int n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (!all[i])
                {
                    continue;
                }
                ArtDef def = db.Art((ArtId)i);
                // 판정기는 부모 대체를 트라이의 부모 노드 K 기술로 낸다(spec 03 7.3). 그 기술이 판정기 데이터에 있어야 한다
                // (경로의 부모 노드 = FallbackParent 는 DataBuild 의 ArtsConverter 가 본다)
                if (def.IsUnique && (def.FallbackParent == ArtId.None || (int)def.FallbackParent >= all.Length || !all[(int)def.FallbackParent]))
                {
                    throw Broken("a unique art's fallback parent is in no trie");
                }
                arts[n++] = ToParserArt(def);
            }
            var source = new ParserSource(Timings(first), arts);

            // 캐릭터마다: 트라이의 비기 = 공용 전부 + 그 캐릭터의 고유 하나 = CharacterDef.UniqueArt
            for (CharacterId c = CharacterId.Char1; c <= CharacterId.Char5; c++)
            {
                TrieDef t = db.Tries(c);
                int expected = 0;
                ArtId unique = ArtId.None;
                for (int i = 0; i < source.Count; i++)
                {
                    ParserArt a = source.At(i);
                    if (!source.IsInTrieOf(a, c))
                    {
                        continue;
                    }
                    expected++;
                    if (Array.IndexOf(t.Arts, a.Id) < 0)
                    {
                        throw Broken("a character's trie misses an art that belongs to it");
                    }
                    if (a.IsUnique)
                    {
                        unique = a.Id;
                    }
                }
                if (expected != t.Arts.Length)
                {
                    throw Broken("a character's trie has another character's unique art");
                }
                if (unique == ArtId.None || unique != db.Character(c).UniqueArt)
                {
                    throw Broken("a character's unique art differs between characters and arts");
                }
            }
            return source;
        }

        private static ParserArt ToParserArt(ArtDef d)
        {
            var paths = new CmdDir[d.Paths.Length][];
            for (int p = 0; p < d.Paths.Length; p++)
            {
                ArtDef.CommandStep[] steps = d.Paths[p];
                var dirs = new CmdDir[steps.Length];
                for (int s = 0; s < steps.Length; s++)
                {
                    dirs[s] = ToDir(steps[s]);
                }
                paths[p] = dirs;
            }
            bool takesAll = d.TakesAllCrystals;
            return new ParserArt(d.Id, d.IsUnique, d.Owner, d.IsJust, takesAll, takesAll ? 0 : d.Cost, takesAll ? d.MinCost : 0,
                                 d.CreditAllowed, paths);
        }

        // ArtDef.CommandStep 과 CmdDir 은 같은 번호(spec 03 2.3)지만 계층이 달라 이름으로 옮긴다
        private static CmdDir ToDir(ArtDef.CommandStep step)
        {
            switch (step)
            {
                case ArtDef.CommandStep.Forward: return CmdDir.Forward;
                case ArtDef.CommandStep.Back: return CmdDir.Back;
                case ArtDef.CommandStep.Left: return CmdDir.Left;
                case ArtDef.CommandStep.Right: return CmdDir.Right;
                default: throw Broken("unknown command step");
            }
        }

        private static ParserTimings Timings(TrieDef t) =>
            new ParserTimings(t.JustWindowUnits, t.GapFullBloomUnits, t.GapShortUnits, t.GapSameSideUnits, t.GapDefaultUnits,
                              t.ArtCooldownUnits, t.KBufferWindowUnits, t.MaxDebt);

        private static bool SameConstants(TrieDef a, TrieDef b) =>
            a.JustWindowUnits == b.JustWindowUnits && a.GapFullBloomUnits == b.GapFullBloomUnits && a.GapShortUnits == b.GapShortUnits
            && a.GapSameSideUnits == b.GapSameSideUnits && a.GapDefaultUnits == b.GapDefaultUnits
            && a.ArtCooldownUnits == b.ArtCooldownUnits && a.KBufferWindowUnits == b.KBufferWindowUnits && a.MaxDebt == b.MaxDebt;

        private static ContentException Broken(string what) => new ContentException("content sections 'arts'/'tries': " + what);
    }
}
