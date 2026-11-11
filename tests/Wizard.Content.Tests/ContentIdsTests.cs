using System;
using System.Linq;
using Wizard.Content;
using Xunit;

namespace Wizard.Content.Tests
{
    // CONTRACTS 2.1, spec 14 3절. 문자열 id 가 정본이고 열거형 번호는 추가만 한다.
    public sealed class ContentIdsTests
    {
        [Theory]
        [InlineData(BlockId.Fireball, "block.fireball")]
        [InlineData(BlockId.SplitTwo, "block.split_two")]
        [InlineData(BlockId.OnHitTrigger, "block.on_hit_trigger")]
        [InlineData(BlockId.TripleHeat, "block.triple_heat")]
        [InlineData(BlockId.Corrosion, "block.corrosion")]
        public void BlockIds(BlockId id, string text)
        {
            Assert.Equal(text, ContentIds.ToId(id));
            Assert.True(ContentIds.TryParse(text, out BlockId back));
            Assert.Equal(id, back);
        }

        [Fact]
        public void EveryNamedValueRoundTrips()
        {
            RoundTrip<BlockId>(ContentIds.ToId, (string s, out BlockId v) => ContentIds.TryParse(s, out v), BlockId.None);
            RoundTrip<ArtId>(ContentIds.ToId, (string s, out ArtId v) => ContentIds.TryParse(s, out v), ArtId.None);
            RoundTrip<MutationId>(ContentIds.ToId, (string s, out MutationId v) => ContentIds.TryParse(s, out v), MutationId.None);
            RoundTrip<EnemyTypeId>(ContentIds.ToId, (string s, out EnemyTypeId v) => ContentIds.TryParse(s, out v), EnemyTypeId.None);
            RoundTrip<BossId>(ContentIds.ToId, (string s, out BossId v) => ContentIds.TryParse(s, out v), BossId.None);
            RoundTrip<FacilityId>(ContentIds.ToId, (string s, out FacilityId v) => ContentIds.TryParse(s, out v), FacilityId.None);
            RoundTrip<ChatChannel>(ContentIds.ToId, (string s, out ChatChannel v) => ContentIds.TryParse(s, out v), ChatChannel.None);
            RoundTrip<CosmeticCategory>(ContentIds.ToId, (string s, out CosmeticCategory v) => ContentIds.TryParse(s, out v), CosmeticCategory.None);
            RoundTrip<ItemCode>(ContentIds.ToId, (string s, out ItemCode v) => ContentIds.TryParse(s, out v), ItemCode.None);
        }

        private delegate bool Parser<T>(string s, out T v);

        private static void RoundTrip<T>(Func<T, string> toId, Parser<T> parse, T none) where T : struct, Enum
        {
            foreach (T v in Enum.GetValues<T>().Where(x => !x.Equals(none)))
            {
                string id = toId(v);
                Assert.True(parse(id, out T back), id);
                Assert.Equal(v, back);
            }
        }

        [Theory]
        [InlineData(CharacterId.Char1, "char.1")]
        [InlineData(CharacterId.Char5, "char.5")]
        public void Characters(CharacterId id, string text)
        {
            Assert.Equal(text, ContentIds.ToId(id));
            Assert.Equal(5, (int)CharacterId.Char5);
        }

        [Fact]
        public void CanonicalCodesFromSpec14()
        {
            Assert.Equal("element.ice", ContentIds.ToId(Element.Ice));
            Assert.Equal("element.dark", ContentIds.ToId(Element.Dark));
            Assert.Equal("tod.boss", ContentIds.ToId(TimeOfDay.BossWave));
            Assert.Equal(4, (int)TimeOfDay.BossWave);
            Assert.Equal("frame.hunting_party", ContentIds.ToId(FrameId.Hunt));
            Assert.Equal("whim.crowding", ContentIds.ToId(ModifierId.Rush));
            Assert.Equal("whim.big_pack", ContentIds.ToId(ModifierId.BigHerd));
            Assert.Equal(1, (int)LeagueId.Sprout);
            Assert.Equal("facility.master_chair", ContentIds.ToId(FacilityId.MasterChair));
            Assert.Equal(301, (int)BossId.KilnBoar);
            Assert.Equal(101, (int)EnemyTypeId.SporeSeedling);
            Assert.Equal("mat.stone_low", ContentIds.ToId(ItemCode.StoneLow));
        }

        [Theory]
        [InlineData("block.great_fireball", "block.fireball")]
        [InlineData("block.split_2", "block.split_two")]
        [InlineData("art.transcribe", "art.copy_circle")]
        [InlineData("boss.master_phantom", "boss.mirror_phantom")]
        [InlineData("element.frost", "element.ice")]
        [InlineData("charm_guard", "charm.safeguard")]
        [InlineData("block.fireball", "block.fireball")]
        public void Aliases(string old, string canonical)
        {
            Assert.Equal(canonical, ContentIds.ResolveAlias(old));
        }

        [Fact]
        public void TryParse_AcceptsOldNames_RejectsUnknown()
        {
            Assert.True(ContentIds.TryParse("block.erosion", out BlockId b));
            Assert.Equal(BlockId.Corrosion, b);
            Assert.False(ContentIds.TryParse("block.nope", out BlockId _));
            Assert.Throws<ArgumentOutOfRangeException>(() => ContentIds.ToId(BlockId.None));
        }

        [Fact]
        public void WorkingNamesAreNotIdentifiers()
        {
            // DECISIONS 1.25, CONTRACTS 2.1: 작업용 이름의 로마자는 식별자에 쓰지 않는다
            string[] banned = { "dalgu", "chorong", "galpi", "seonge", "geumeum", "nuru", "handeot", "dolmadang", "cheotmodak" };
            foreach (Type t in typeof(ContentIds).Assembly.GetTypes().Where(t => t.IsEnum))
            {
                foreach (string name in Enum.GetNames(t))
                {
                    Assert.DoesNotContain(banned, b => name.ToLowerInvariant().Contains(b));
                }
            }
        }
    }
}
