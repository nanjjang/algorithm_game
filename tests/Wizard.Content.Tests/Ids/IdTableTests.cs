using System;
using System.Collections.Generic;
using System.Linq;
using Wizard.Content;
using Wizard.Core;
using Xunit;

namespace Wizard.Content.Tests.Ids
{
    // spec 14 3절의 모든 표: "표의 id 전부 ↔ 열거형·ContentIds" 왕복과 정수 코드 대조. 기준은 Spec14Tables(명세에서 옮김).
    // 열거형 쪽 완전성(표에 없는 값이 열거형에 없다)도 같이 본다. 계약에 없는 값(None, Baseline)만 뺀다.
    public sealed class IdTableTests
    {
        private delegate bool Parser<T>(string s, out T v);

        private static void CheckTable<T>(string source, (int Code, string Id, T Value)[] rows,
            Func<T, string> toId, Parser<T> parse, params T[] outsideTable) where T : struct, Enum
        {
            Assert.Equal(rows.Length, rows.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(rows.Length, rows.Select(r => r.Value).Distinct().Count());
            foreach ((int code, string id, T value) in rows)
            {
                Assert.True(ContentId.TryParse(id, out _), source + ": grammar " + id);
                Assert.Equal(code, Convert.ToInt32(value));
                Assert.Equal(id, toId(value));
                Assert.True(parse(id, out T back), source + ": parse " + id);
                Assert.Equal(value, back);
            }
            T[] expected = Enum.GetValues<T>().Where(v => Array.IndexOf(outsideTable, v) < 0).OrderBy(v => Convert.ToInt32(v)).ToArray();
            T[] actual = rows.Select(r => r.Value).OrderBy(v => Convert.ToInt32(v)).ToArray();
            Assert.Equal(expected, actual);
            foreach (T v in outsideTable)
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => toId(v));
            }
        }

        [Fact]
        public void Table3_1_Elements()
        {
            CheckTable("3.1", Spec14Tables.Elements, ContentIds.ToId, (string s, out Element v) => ContentIds.TryParse(s, out v));
        }

        [Fact]
        public void Table3_2_CharactersWandsPassivesTraits()
        {
            CharacterRow[] rows = Spec14Tables.Characters;
            CheckTable("3.2 char", rows.Select(r => (r.Code, r.Id, r.Value)).ToArray(),
                ContentIds.ToId, (string s, out CharacterId v) => ContentIds.TryParse(s, out v), CharacterId.Baseline);
            CheckTable("3.2 passive", rows.Select(r => ((int)r.Passive, r.PassiveText, r.Passive)).ToArray(),
                ContentIds.ToId, (string s, out PassiveId v) => ContentIds.TryParse(s, out v), PassiveId.None);
            CheckTable("3.2 wand_trait", rows.Select(r => ((int)r.Trait, r.TraitText, r.Trait)).ToArray(),
                ContentIds.ToId, (string s, out WandTraitId v) => ContentIds.TryParse(s, out v), WandTraitId.None);
            foreach (CharacterRow r in rows)
            {
                // 패시브·특성의 번호는 캐릭터 번호와 같다(같은 줄)
                Assert.Equal(r.Code, (int)r.Passive);
                Assert.Equal(r.Code, (int)r.Trait);
                Assert.True(ContentId.TryParse(r.WandText, out _));
                Assert.Equal(r.WandText, ContentIds.ToWandId(r.Value));
                Assert.True(ContentIds.TryParseWand(r.WandText, out CharacterId wandOwner));
                Assert.Equal(r.Value, wandOwner);
                Assert.Equal(r.ArtText, ContentIds.ToId(r.Art));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToWandId(CharacterId.Baseline));
            Assert.False(ContentIds.TryParseWand("wand.6", out _));
            Assert.False(ContentIds.TryParseWand("wand.0", out _));
        }

        [Fact]
        public void Table3_3_Blocks()
        {
            CheckTable("3.3", Spec14Tables.Blocks.Select(r => (r.Code, r.Id, r.Value)).ToArray(),
                ContentIds.ToId, (string s, out BlockId v) => ContentIds.TryParse(s, out v), BlockId.None);
            // 종류·등급 열거형의 코드(spec 14 3.3 아래 목록): projectile 1, control 2, modifier 3 / common 1, rare 2, hero 3
            Assert.Equal(new[] { 1, 2, 3 }, Enum.GetValues<BlockKind>().Select(v => (int)v).ToArray());
            Assert.Equal(new[] { "Projectile", "Control", "Modifier" }, Enum.GetNames<BlockKind>());
            Assert.Equal(new[] { 1, 2, 3 }, Enum.GetValues<Rarity>().Select(v => (int)v).ToArray());
            Assert.Equal(new[] { "Common", "Rare", "Hero" }, Enum.GetNames<Rarity>());
        }

        [Fact]
        public void Table3_4_Arts()
        {
            CheckTable("3.4", Spec14Tables.Arts.Select(r => (r.Code, r.Id, r.Value)).ToArray(),
                ContentIds.ToId, (string s, out ArtId v) => ContentIds.TryParse(s, out v), ArtId.None);
            // ArtMask 의 비트 번호 = (int)ArtId 이므로 15개가 ushort 에 들어가야 한다(CONTRACTS 2.1)
            Assert.All(Spec14Tables.Arts, r => Assert.InRange((int)r.Value, 0, (sizeof(ushort) * 8) - 1));
        }

        [Fact]
        public void Table3_5_Mutations()
        {
            CheckTable("3.5", Spec14Tables.Mutations, ContentIds.ToId,
                (string s, out MutationId v) => ContentIds.TryParse(s, out v), MutationId.None);
        }

        [Fact]
        public void Table3_6_Enemies()
        {
            CheckTable("3.6", Spec14Tables.Enemies.Select(r => (r.Code, r.Id, r.Value)).ToArray(),
                ContentIds.ToId, (string s, out EnemyTypeId v) => ContentIds.TryParse(s, out v), EnemyTypeId.None);
        }

        [Fact]
        public void Table3_7_Elites()
        {
            var rows = Spec14Tables.Elites;
            Assert.Equal(rows.Length, rows.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count());
            foreach ((int code, string id, string baseId) in rows)
            {
                Assert.True(ContentId.TryParse(id, out _), id);
                Assert.True(ContentIds.TryParseElite(id, out EnemyTypeId baseType), id);
                Assert.Equal(baseId, ContentIds.ToId(baseType));
                Assert.Equal(id, ContentIds.ToEliteId(baseType));
                Assert.Equal(code, ContentIds.ToEliteCode(baseType));
                // 엘리트 id는 적 id가 아니다(엘리트는 종이 아니라 상태다)
                Assert.False(ContentIds.TryParse(id, out EnemyTypeId _));
            }
            // 일반 적 12종 모두 엘리트가 있고, 졸개와 None 에는 없다
            EnemyTypeId[] withElite = rows.Select(r => { ContentIds.TryParseElite(r.Id, out EnemyTypeId b); return b; }).OrderBy(b => (int)b).ToArray();
            EnemyTypeId[] regular = Spec14Tables.Enemies.Where(r => !r.Minion).Select(r => r.Value).OrderBy(b => (int)b).ToArray();
            Assert.Equal(regular, withElite);
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToEliteId(EnemyTypeId.SporeSeedling));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToEliteCode(EnemyTypeId.SporeSeedling));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToEliteId(EnemyTypeId.None));
            Assert.False(ContentIds.TryParseElite("elite.spore_seedling", out _));
            Assert.False(ContentIds.TryParseElite("enemy.slime_moss", out _));
            // 엘리트 코드 201~212 는 보스 코드(301~)·졸개(101)와 겹치지 않는다
            int[] codes = rows.Select(r => r.Code).Concat(Spec14Tables.Bosses.Select(b => b.Code)).Concat(Spec14Tables.Enemies.Select(e => e.Code)).ToArray();
            Assert.Equal(codes.Length, codes.Distinct().Count());
        }

        [Fact]
        public void Table3_8_Bosses()
        {
            CheckTable("3.8", Spec14Tables.Bosses, ContentIds.ToId,
                (string s, out BossId v) => ContentIds.TryParse(s, out v), BossId.None);
        }

        [Fact]
        public void Table3_9_Tags()
        {
            CheckTable("3.9 tag", Spec14Tables.Tags, ContentIds.ToId,
                (string s, out EnemyTag v) => ContentIds.TryParse(s, out v), EnemyTag.None);
            // 비트 집합이라 둘 이상 켠 값에는 id가 없다
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToId(EnemyTag.Elite | EnemyTag.Heavy));
        }

        [Fact]
        public void Table3_9_TimesOfDay()
        {
            CheckTable("3.9 tod", Spec14Tables.TimesOfDay, ContentIds.ToId,
                (string s, out TimeOfDay v) => ContentIds.TryParse(s, out v), TimeOfDay.None);
        }

        [Fact]
        public void Table3_9_Frames()
        {
            CheckTable("3.9 frame", Spec14Tables.Frames, ContentIds.ToId, (string s, out FrameId v) => ContentIds.TryParse(s, out v));
        }

        [Fact]
        public void Table3_9_Whims()
        {
            CheckTable("3.9 whim", Spec14Tables.Whims, ContentIds.ToId,
                (string s, out ModifierId v) => ContentIds.TryParse(s, out v), ModifierId.None);
        }

        [Fact]
        public void Table3_9_WeatherEvents()
        {
            CheckTable("3.9 event", Spec14Tables.Events, ContentIds.ToId,
                (string s, out WeatherEventId v) => ContentIds.TryParse(s, out v), WeatherEventId.None);
        }

        [Fact]
        public void Table3_10_Items()
        {
            CheckTable("3.10", Spec14Tables.Items, ContentIds.ToId,
                (string s, out ItemCode v) => ContentIds.TryParse(s, out v), ItemCode.None);
            // 귀속 수량은 같은 item_code 의 qty_bound 로 센다 — 따로 id를 만들지 않는다(spec 14 3.10)
            Assert.DoesNotContain(Enum.GetNames<ItemCode>(), n => n.IndexOf("Bound", StringComparison.Ordinal) >= 0);
        }

        [Fact]
        public void Table3_11_Currencies()
        {
            CheckTable("3.11", Spec14Tables.Currencies.Select(r => (r.Code, r.Id, r.Value)).ToArray(),
                ContentIds.ToId, (string s, out CurrencyCode v) => ContentIds.TryParse(s, out v));
            foreach (var r in Spec14Tables.Currencies)
            {
                // DB 열은 키만 쓴다(spec 14 1.3): 키도 같은 값으로 읽힌다
                Assert.Equal(r.Id, ContentIds.ResolveAlias(r.Key));
                Assert.True(ContentIds.TryParse(r.Key, out CurrencyCode fromKey));
                Assert.Equal(r.Value, fromKey);
            }
            // 원정 지갑은 표시 전용(U-23): 문법은 맞지만 재화가 아니다
            Assert.True(ContentId.TryParse(Spec14Tables.ExpeditionPurseId, out _));
            Assert.False(ContentIds.TryParse(Spec14Tables.ExpeditionPurseId, out CurrencyCode _));
            // 버킷 코드(CONTRACTS 2.1): earned 0, paid 1, free 2
            Assert.Equal(new[] { "Earned", "Paid", "Free" }, Enum.GetNames<CurrencyBucket>());
            Assert.Equal(new[] { 0, 1, 2 }, Enum.GetValues<CurrencyBucket>().Select(v => (int)v).ToArray());
        }

        [Fact]
        public void Table3_12_Titles()
        {
            // 강화 단계 칭호: title.stage.N.bK, N = 1~5, K = 0~4 (25개)
            var stage = new List<string>();
            foreach (CharacterRow c in Spec14Tables.Characters)
            {
                for (int band = 0; band <= 4; band++)
                {
                    string expected = "title.stage." + c.Code + ".b" + band;
                    Assert.Equal(expected, ContentIds.ToStageTitleId(c.Value, band));
                    stage.Add(expected);
                }
            }
            Assert.Equal(25, stage.Distinct(StringComparer.Ordinal).Count());
            foreach (string id in stage)
            {
                Assert.True(ContentId.TryParse(id, out _), id);
                Assert.True(ContentIds.TryGetTitleKind(id, out TitleKind kind), id);
                Assert.Equal(TitleKind.Stage, kind);
            }
            foreach ((string id, TitleKind kind) in Spec14Tables.OtherTitles)
            {
                Assert.True(ContentId.TryParse(id, out _), id);
                Assert.True(ContentIds.TryGetTitleKind(id, out TitleKind got), id);
                Assert.Equal(kind, got);
            }
            // 칭호 분류 열거형(CONTRACTS 2.1): Stage 1, Record 2, Revenge 3, Season 4, Feat 5
            Assert.Equal(new[] { "Stage", "Record", "Revenge", "Season", "Feat" }, Enum.GetNames<TitleKind>());
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Enum.GetValues<TitleKind>().Select(v => (int)v).ToArray());
            // 표 밖의 꼴은 받지 않는다
            Assert.False(ContentIds.TryGetTitleKind("title.stage.6.b0", out _));
            Assert.False(ContentIds.TryGetTitleKind("title.stage.1.b5", out _));
            Assert.False(ContentIds.TryGetTitleKind("title.stage.0.b0", out _));
            Assert.False(ContentIds.TryGetTitleKind("title.stage.1", out _));
            Assert.False(ContentIds.TryGetTitleKind("title.record", out _));
            Assert.False(ContentIds.TryGetTitleKind("title.mirror_breaker", out _));
            Assert.False(ContentIds.TryGetTitleKind("Title.feat.x", out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToStageTitleId(CharacterId.Char1, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToStageTitleId(CharacterId.Char1, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToStageTitleId(CharacterId.Baseline, 0));
        }

        [Fact]
        public void Table3_13_Facilities()
        {
            CheckTable("3.13", Spec14Tables.Facilities, ContentIds.ToId,
                (string s, out FacilityId v) => ContentIds.TryParse(s, out v), FacilityId.None);
        }

        [Fact]
        public void Table2_3_PlacesAndPeople()
        {
            foreach ((string id, string? alias) in Spec14Tables.PlacesAndPeople)
            {
                Assert.True(ContentId.TryParse(id, out _), id);
                Assert.Equal(id, ContentIds.ResolveAlias(id));
                if (alias != null)
                {
                    Assert.False(ContentId.TryParse(alias, out _));   // 네임스페이스가 없는 WC 0 표기
                    Assert.Equal(id, ContentIds.ResolveAlias(alias));
                }
            }
        }

        [Fact]
        public void Table3_14_CosmeticSlots()
        {
            CheckTable("3.14 slot", Spec14Tables.CosmeticSlots, ContentIds.ToId,
                (string s, out CosmeticSlot v) => ContentIds.TryParse(s, out v), CosmeticSlot.None);
        }

        [Fact]
        public void Table3_14_CosmeticCategories()
        {
            CheckTable("3.14 cat", Spec14Tables.CosmeticCategories, ContentIds.ToId,
                (string s, out CosmeticCategory v) => ContentIds.TryParse(s, out v), CosmeticCategory.None);
            // 꾸미기 출처(CONTRACTS 2.1, spec 14 3.14 CosmeticSource)
            Assert.Equal(new[] { "Record", "Season", "Trade", "GoldShop", "PaidShop" }, Enum.GetNames<CosmeticSource>());
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Enum.GetValues<CosmeticSource>().Select(v => (int)v).ToArray());
        }

        [Fact]
        public void Table3_14_FixedCosmeticsAndWildStain()
        {
            foreach ((string id, CosmeticCategory cat) in Spec14Tables.FixedCosmetics)
            {
                // "cosmetic.<cat>.<key>": <cat> 마디가 옷장 분류 id의 키다
                Assert.True(ContentId.TryParse(id, out _), id);
                string[] parts = id.Split('.');
                Assert.Equal(3, parts.Length);
                Assert.True(ContentIds.TryParse("cosmetic_cat." + parts[1], out CosmeticCategory parsed), id);
                Assert.Equal(cat, parsed);
            }
            foreach ((int code, string id) in Spec14Tables.WildStains)
            {
                Assert.True(ContentId.TryParse(id, out _), id);
                Assert.Equal("wild_stain." + code, id);
            }
        }

        [Fact]
        public void Table3_15_Leagues()
        {
            CheckTable("3.15 league", Spec14Tables.Leagues, ContentIds.ToId,
                (string s, out LeagueId v) => ContentIds.TryParse(s, out v), LeagueId.None);
        }

        [Fact]
        public void Table3_15_Emotes()
        {
            CheckTable("3.15 emote", Spec14Tables.Emotes, ContentIds.ToId,
                (string s, out EmoteId v) => ContentIds.TryParse(s, out v), EmoteId.None);
        }

        [Fact]
        public void Table3_15_ChatChannels()
        {
            CheckTable("3.15 chat", Spec14Tables.ChatChannels, ContentIds.ToId,
                (string s, out ChatChannel v) => ContentIds.TryParse(s, out v), ChatChannel.None);
        }

        [Fact]
        public void Table3_15_PresenceStates()
        {
            // I-F4 의 여덟 상태(spec 14 3.15). 코드는 CONTRACTS 2.1(Offline = 0)
            Assert.Equal(new[] { "Offline", "Online", "Expedition", "Duel", "Trade", "Idle", "DuelWait", "Spectate" },
                Enum.GetNames<PresenceState>());
            Assert.Equal(Enumerable.Range(0, 8).ToArray(), Enum.GetValues<PresenceState>().Select(v => (int)v).ToArray());
        }

        [Fact]
        public void AllTableIdsAreDistinctAcrossTables()
        {
            var all = new List<string>();
            all.AddRange(Spec14Tables.Elements.Select(r => r.Id));
            all.AddRange(Spec14Tables.Characters.SelectMany(r => new[] { r.Id, r.WandText, r.PassiveText, r.TraitText }));
            all.AddRange(Spec14Tables.Blocks.Select(r => r.Id));
            all.AddRange(Spec14Tables.Arts.Select(r => r.Id));
            all.AddRange(Spec14Tables.Mutations.Select(r => r.Id));
            all.AddRange(Spec14Tables.Enemies.Select(r => r.Id));
            all.AddRange(Spec14Tables.Elites.Select(r => r.Id));
            all.AddRange(Spec14Tables.Bosses.Select(r => r.Id));
            all.AddRange(Spec14Tables.Tags.Select(r => r.Id));
            all.AddRange(Spec14Tables.TimesOfDay.Select(r => r.Id));
            all.AddRange(Spec14Tables.Frames.Select(r => r.Id));
            all.AddRange(Spec14Tables.Whims.Select(r => r.Id));
            all.AddRange(Spec14Tables.Events.Select(r => r.Id));
            all.AddRange(Spec14Tables.Items.Select(r => r.Id));
            all.AddRange(Spec14Tables.Currencies.Select(r => r.Id));
            all.Add(Spec14Tables.ExpeditionPurseId);
            all.AddRange(Spec14Tables.OtherTitles.Select(r => r.Id));
            all.AddRange(Spec14Tables.Facilities.Select(r => r.Id));
            all.AddRange(Spec14Tables.PlacesAndPeople.Select(r => r.Id));
            all.AddRange(Spec14Tables.CosmeticSlots.Select(r => r.Id));
            all.AddRange(Spec14Tables.CosmeticCategories.Select(r => r.Id));
            all.AddRange(Spec14Tables.FixedCosmetics.Select(r => r.Id));
            all.AddRange(Spec14Tables.WildStains.Select(r => r.Id));
            all.AddRange(Spec14Tables.Leagues.Select(r => r.Id));
            all.AddRange(Spec14Tables.Emotes.Select(r => r.Id));
            all.AddRange(Spec14Tables.ChatChannels.Select(r => r.Id));
            Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
            foreach (string id in all)
            {
                Assert.True(ContentId.TryParse(id, out _), id);
                Assert.Equal(id, ContentIds.ResolveAlias(id));   // 정본은 별칭으로 다시 바뀌지 않는다
            }
        }
    }
}
