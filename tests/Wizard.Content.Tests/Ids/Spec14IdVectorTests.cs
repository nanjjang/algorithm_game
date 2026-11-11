using System;
using System.Linq;
using Wizard.Content;
using Wizard.Core;
using Xunit;

namespace Wizard.Content.Tests.Ids
{
    // spec 14 7.1 식별자 벡터 TV-001 ~ TV-021. 기대값은 명세의 then 칸.
    // 블록·비기 표의 종류·등급·속성·주인(TV-009~012)은 아직 팩 데이터가 없어 spec 14 3.3·3.4 표를 옮긴 Spec14Tables 로 보고,
    // 그 표가 열거형·ContentIds 와 한 줄씩 맞는지는 IdTableTests 가 본다.
    public sealed class Spec14IdVectorTests
    {
        [Fact]
        public void S14_TV_001__CharOnePassesGrammar()
        {
            Assert.True(ContentId.TryParse("char.1", out ContentId id));
            Assert.Equal("char.1", id.Value);
        }

        [Fact]
        public void S14_TV_002__BlockFireballPassesGrammar()
        {
            Assert.True(ContentId.TryParse("block.fireball", out _));
        }

        [Fact]
        public void S14_TV_003__UppercaseFailsGrammar()
        {
            Assert.False(ContentId.TryParse("Block.Fireball", out _));
        }

        [Fact]
        public void S14_TV_004__MissingNamespaceFailsGrammar()
        {
            Assert.False(ContentId.TryParse("fireball", out _));
        }

        [Fact]
        public void S14_TV_005__EnemySlimeMossPassesGrammar()
        {
            Assert.True(ContentId.TryParse("enemy.slime_moss", out _));
        }

        [Fact]
        public void S14_TV_006__EliteBoarTuskPassesGrammar()
        {
            Assert.True(ContentId.TryParse("elite.boar_tusk", out _));
        }

        [Fact]
        public void S14_TV_007__AliasMasterIsNpcMaster()
        {
            Assert.Equal("npc.master", ContentIds.ResolveAlias("master"));
        }

        [Fact]
        public void S14_TV_008__AliasArenaIsPlaceArena()
        {
            Assert.Equal("place.arena", ContentIds.ResolveAlias("arena"));
        }

        [Fact]
        public void S14_TV_009__SixteenBlocks_Projectile6_Control3_Modifier7()
        {
            BlockId[] all = Enum.GetValues<BlockId>().Where(b => b != BlockId.None).ToArray();
            Assert.Equal(16, all.Length);
            Assert.Equal(16, Spec14Tables.Blocks.Length);
            Assert.Equal(6, Spec14Tables.Blocks.Count(b => b.Kind == BlockKind.Projectile));
            Assert.Equal(3, Spec14Tables.Blocks.Count(b => b.Kind == BlockKind.Control));
            Assert.Equal(7, Spec14Tables.Blocks.Count(b => b.Kind == BlockKind.Modifier));
            Assert.All(all, b => Assert.True(ContentIds.TryParse(ContentIds.ToId(b), out BlockId _)));
        }

        [Fact]
        public void S14_TV_010__RarityCounts_Common6_Rare6_Hero4()
        {
            BlockId[] common = Spec14Tables.Blocks.Where(b => b.Rarity == Rarity.Common).Select(b => b.Value).ToArray();
            Assert.Equal(
                new[] { BlockId.MagicBolt, BlockId.SplitTwo, BlockId.WallBounce, BlockId.DoubleSize, BlockId.Pierce, BlockId.Frost },
                common);
            Assert.Equal(6, Spec14Tables.Blocks.Count(b => b.Rarity == Rarity.Rare));
            Assert.Equal(4, Spec14Tables.Blocks.Count(b => b.Rarity == Rarity.Hero));
        }

        [Fact]
        public void S14_TV_011__ProjectilesPerElement_EveryElementHasOne()
        {
            string[] Projectiles(Element e) => Spec14Tables.Blocks
                .Where(b => b.Kind == BlockKind.Projectile && b.Element == e)
                .Select(b => ContentIds.ToId(b.Value)).ToArray();
            Assert.Equal(new[] { "block.fireball" }, Projectiles(Element.Fire));
            Assert.Equal(new[] { "block.chain_lightning" }, Projectiles(Element.Lightning));
            Assert.Equal(new[] { "block.magic_bolt", "block.boomerang" }, Projectiles(Element.Arcane));
            Assert.Equal(new[] { "block.ice_spike" }, Projectiles(Element.Ice));
            Assert.Equal(new[] { "block.shadow_orb" }, Projectiles(Element.Dark));
            Assert.Empty(Projectiles(Element.None));
        }

        [Fact]
        public void S14_TV_012__FifteenArts_Common10_Unique5_OnePerCharacter()
        {
            ArtId[] all = Enum.GetValues<ArtId>().Where(a => a != ArtId.None).ToArray();
            Assert.Equal(15, all.Length);
            Assert.Equal(10, Spec14Tables.Arts.Count(a => a.Owner == CharacterId.Baseline));
            var unique = Spec14Tables.Arts.Where(a => a.Owner != CharacterId.Baseline).ToArray();
            Assert.Equal(5, unique.Length);
            Assert.Equal(
                new[] { CharacterId.Char1, CharacterId.Char2, CharacterId.Char3, CharacterId.Char4, CharacterId.Char5 },
                unique.Select(a => a.Owner).OrderBy(c => (int)c).ToArray());
            // 3.2 표의 고유 비기 칸과 같은 것
            foreach (CharacterRow c in Spec14Tables.Characters)
            {
                Assert.Equal(c.Value, unique.Single(a => a.Value == c.Art).Owner);
            }
        }

        [Fact]
        public void S14_TV_013__SixMutations()
        {
            Assert.Equal(6, Enum.GetValues<MutationId>().Count(m => m != MutationId.None));
        }

        [Fact]
        public void S14_TV_014__TenFacilities()
        {
            Assert.Equal(10, Enum.GetValues<FacilityId>().Count(f => f != FacilityId.None));
        }

        [Fact]
        public void S14_TV_015__TwelveEnemiesPlusOneMinion()
        {
            EnemyTypeId[] all = Enum.GetValues<EnemyTypeId>().Where(e => e != EnemyTypeId.None).ToArray();
            Assert.Equal(13, all.Length);
            Assert.Equal(12, Spec14Tables.Enemies.Count(e => !e.Minion));
            Assert.Equal(new[] { EnemyTypeId.SporeSeedling }, Spec14Tables.Enemies.Where(e => e.Minion).Select(e => e.Value).ToArray());
        }

        [Fact]
        public void S14_TV_016__EveryEliteBaseIsAnEnemyWithSameKey()
        {
            foreach ((int _, string eliteId, string _) in Spec14Tables.Elites)
            {
                Assert.True(ContentIds.TryParseElite(eliteId, out EnemyTypeId baseType), eliteId);
                string key = eliteId.Substring("elite.".Length);
                Assert.Equal("enemy." + key, ContentIds.ToId(baseType));
                Assert.True(ContentIds.TryParse("enemy." + key, out EnemyTypeId enemy));
                Assert.Equal(baseType, enemy);
            }
        }

        [Fact]
        public void S14_TV_017__CharacterElementCodeEqualsN()
        {
            // 1 화염, 2 번개, 3 비전, 4 냉기, 5 암흑
            Assert.Equal(1, (int)Element.Fire);
            Assert.Equal(2, (int)Element.Lightning);
            Assert.Equal(3, (int)Element.Arcane);
            Assert.Equal(4, (int)Element.Ice);
            Assert.Equal(5, (int)Element.Dark);
            foreach (CharacterRow c in Spec14Tables.Characters)
            {
                Assert.Equal((int)c.Value, (int)c.Element);
                Assert.Equal("char." + (int)c.Value, ContentIds.ToId(c.Value));
            }
        }

        [Fact]
        public void S14_TV_018__UiFrostIsIce()
        {
            Assert.Equal("ice", ContentIds.ResolveAlias("frost"));
        }

        [Fact]
        public void S14_TV_019__UiShadowIsDark()
        {
            Assert.Equal("dark", ContentIds.ResolveAlias("shadow"));
        }

        [Fact]
        public void S14_TV_020__EightEmotesInOrder()
        {
            // 인사, 박수, 도발, 절, 앉기, 춤, 지팡이 들어 올리기, 좌절
            EmoteId[] all = Enum.GetValues<EmoteId>().Where(e => e != EmoteId.None).OrderBy(e => (int)e).ToArray();
            Assert.Equal(8, all.Length);
            Assert.Equal(
                new[] { "emote.greet", "emote.clap", "emote.taunt", "emote.bow", "emote.sit", "emote.dance", "emote.raise_wand", "emote.despair" },
                all.Select(ContentIds.ToId).ToArray());
            Assert.Equal(Enumerable.Range(1, 8).ToArray(), all.Select(e => (int)e).ToArray());
        }

        [Fact]
        public void S14_TV_021__SevenChatChannels_NoServerWide()
        {
            ChatChannel[] all = Enum.GetValues<ChatChannel>().Where(c => c != ChatChannel.None).ToArray();
            Assert.Equal(7, all.Length);
            Assert.Contains(ChatChannel.System, all);
            string[] ids = all.Select(ContentIds.ToId).ToArray();
            Assert.Equal(
                new[] { "chat.nearby", "chat.plaza", "chat.trade", "chat.whisper", "chat.friends", "chat.duel_room", "chat.system" },
                ids);
            // 서버 전체 채널은 없다(DECISIONS 2.7): 이름으로도 문자열 id로도 없다
            string[] banned = { "server", "global", "world", "all", "broadcast" };
            foreach (string name in Enum.GetNames<ChatChannel>())
            {
                Assert.DoesNotContain(banned, b => name.ToLowerInvariant().Contains(b));
            }
            foreach (string b in banned)
            {
                Assert.False(ContentIds.TryParse("chat." + b, out ChatChannel _), b);
            }
        }
    }
}
