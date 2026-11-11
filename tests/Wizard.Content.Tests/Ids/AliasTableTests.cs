using System;
using Wizard.Content;
using Xunit;

namespace Wizard.Content.Tests.Ids
{
    // CONTRACTS 2.1 "명세의 옛 이름 → 정본" 대응표 전체. 한 줄의 문자열 꼴(키만 쓴 꼴과 다른 명세가 실제로 적은
    // 네임스페이스 꼴)은 ResolveAlias 와 TryParse 로 정본이 되는지, 열거형 이름 꼴(GreatFireball 등)은 열거형에 없고
    // 정본 열거형이 정본 id로 가는지 본다. 시설의 fac_* 는 별칭으로도 받지 않는다.
    public sealed class AliasTableTests
    {
        // 블록 줄 5개: (옛 문자열, 정본)
        [Theory]
        [InlineData("great_fireball", BlockId.Fireball, "block.fireball")]
        [InlineData("giant_fireball", BlockId.Fireball, "block.fireball")]
        [InlineData("block.great_fireball", BlockId.Fireball, "block.fireball")]
        [InlineData("block.giant_fireball", BlockId.Fireball, "block.fireball")]
        [InlineData("split_2", BlockId.SplitTwo, "block.split_two")]
        [InlineData("block.split_2", BlockId.SplitTwo, "block.split_two")]
        [InlineData("split_3", BlockId.SplitThree, "block.split_three")]
        [InlineData("block.split_3", BlockId.SplitThree, "block.split_three")]
        [InlineData("pierce_2", BlockId.Pierce, "block.pierce")]
        [InlineData("block.pierce_2", BlockId.Pierce, "block.pierce")]
        [InlineData("overheat_x3", BlockId.TripleHeat, "block.triple_heat")]
        [InlineData("block.overheat_x3", BlockId.TripleHeat, "block.triple_heat")]
        [InlineData("erosion", BlockId.Corrosion, "block.corrosion")]
        [InlineData("block.erosion", BlockId.Corrosion, "block.corrosion")]
        public void BlockRows_StringForms(string old, BlockId canonical, string canonicalId)
        {
            Assert.Equal(canonicalId, ContentIds.ResolveAlias(old));
            Assert.True(ContentIds.TryParse(old, out BlockId v));
            Assert.Equal(canonical, v);
            Assert.Equal(canonicalId, ContentIds.ToId(v));
        }

        [Theory]
        [InlineData("GreatFireball", BlockId.Fireball)]
        [InlineData("Split2", BlockId.SplitTwo)]
        [InlineData("Split3", BlockId.SplitThree)]
        [InlineData("Pierce2", BlockId.Pierce)]
        [InlineData("Overheat3", BlockId.TripleHeat)]
        [InlineData("Erosion", BlockId.Corrosion)]
        public void BlockRows_EnumNameForms(string oldName, BlockId canonical)
        {
            Assert.False(Enum.IsDefined(typeof(BlockId), oldName), oldName);
            Assert.True(Enum.IsDefined(typeof(BlockId), canonical));
        }

        // 비기 줄 4개. 열거형 이름 꼴 + 명세 03·04·10이 적은 문자열 꼴(art.<snake>)
        [Theory]
        [InlineData("ManaRelease", "art.mana_release", ArtId.Burst, "art.burst")]
        [InlineData("RecoilShot", "art.recoil_shot", ArtId.Recoil, "art.recoil")]
        [InlineData("FanSpread", "art.fan_spread", ArtId.Fan, "art.fan")]
        [InlineData("TwinWing", "art.twin_wing", ArtId.Wings, "art.wings")]
        [InlineData("AfterimageStep", "art.afterimage_step", ArtId.Afterimage, "art.afterimage")]
        [InlineData("PiercingLance", "art.piercing_lance", ArtId.Spear, "art.spear")]
        [InlineData("VentBurst", "art.vent_burst", ArtId.Vent, "art.vent")]
        [InlineData("EightfoldSpread", "art.eightfold_spread", ArtId.Octastar, "art.octastar")]
        [InlineData("ThunderLance", "art.thunder_lance", ArtId.ThunderSpear, "art.thunder_spear")]
        [InlineData("FullBloom", "art.full_bloom", ArtId.Bloom, "art.bloom")]
        [InlineData("Transcribe", "art.transcribe", ArtId.CopyCircle, "art.copy_circle")]
        [InlineData("ThunderDash", "art.thunder_dash", ArtId.BoltDash, "art.bolt_dash")]
        public void ArtRows(string oldName, string oldId, ArtId canonical, string canonicalId)
        {
            Assert.False(Enum.IsDefined(typeof(ArtId), oldName), oldName);
            Assert.Equal(canonicalId, ContentIds.ResolveAlias(oldId));
            Assert.True(ContentIds.TryParse(oldId, out ArtId v));
            Assert.Equal(canonical, v);
            Assert.Equal(canonicalId, ContentIds.ToId(canonical));
        }

        [Theory]
        [InlineData("passive.boil_over", PassiveId.Overflow, "passive.overflow")]
        [InlineData("passive.shadow_pact", PassiveId.ShadowContract, "passive.shadow_contract")]
        public void PassiveRow(string old, PassiveId canonical, string canonicalId)
        {
            Assert.Equal(canonicalId, ContentIds.ResolveAlias(old));
            Assert.True(ContentIds.TryParse(old, out PassiveId v));
            Assert.Equal(canonical, v);
        }

        [Theory]
        [InlineData("boss.boar_kiln", BossId.KilnBoar, "boss.kiln_boar")]
        [InlineData("boss.master_phantom", BossId.MirrorPhantom, "boss.mirror_phantom")]
        [InlineData("boss.mirror_master", BossId.MirrorPhantom, "boss.mirror_phantom")]
        [InlineData("boss.star_serpent", BossId.NightSerpent, "boss.night_serpent")]
        public void BossRow(string old, BossId canonical, string canonicalId)
        {
            Assert.Equal(canonicalId, ContentIds.ResolveAlias(old));
            Assert.True(ContentIds.TryParse(old, out BossId v));
            Assert.Equal(canonical, v);
        }

        [Theory]
        [InlineData("stone_low", ItemCode.StoneLow, "mat.stone_low")]
        [InlineData("stone_mid", ItemCode.StoneMid, "mat.stone_mid")]
        [InlineData("stone_high", ItemCode.StoneHigh, "mat.stone_high")]
        [InlineData("charm_guard", ItemCode.CharmSafeguard, "charm.safeguard")]
        [InlineData("mat.charm_guard", ItemCode.CharmSafeguard, "charm.safeguard")]   // spec 06·07·08 의 꼴
        public void MaterialRow(string old, ItemCode canonical, string canonicalId)
        {
            Assert.Equal(canonicalId, ContentIds.ResolveAlias(old));
            Assert.True(ContentIds.TryParse(old, out ItemCode v));
            Assert.Equal(canonical, v);
        }

        [Fact]
        public void ElementRow()
        {
            // Frost(열거형 이름 꼴)는 Element 에 없다. frost·shadow 는 spec 14 3.1 ElementAlias 대로 키 → 키
            Assert.False(Enum.IsDefined(typeof(Element), "Frost"));
            Assert.False(Enum.IsDefined(typeof(Element), "Shadow"));
            Assert.Equal("ice", ContentIds.ResolveAlias("frost"));
            Assert.Equal("dark", ContentIds.ResolveAlias("shadow"));
            Assert.Equal("element.ice", ContentIds.ResolveAlias("element.frost"));
            Assert.Equal("element.dark", ContentIds.ResolveAlias("element.shadow"));
            Assert.True(ContentIds.TryParse("element.frost", out Element ice));
            Assert.Equal(Element.Ice, ice);
            Assert.True(ContentIds.TryParse("element.shadow", out Element dark));
            Assert.Equal(Element.Dark, dark);
            // spec 14 3.1 ElementAlias 의 나머지 키는 그대로다
            foreach (string key in new[] { "neutral", "fire", "lightning", "arcane", "ice", "dark" })
            {
                Assert.Equal(key, ContentIds.ResolveAlias(key));
            }
            // "frost" 는 블록 서리(block.frost)와 헷갈리지 않는다(8절 C-01)
            Assert.Equal("block.frost", ContentIds.ResolveAlias("block.frost"));
        }

        // 변덕 줄: 열거형 이름(Rush …) → spec 14 whim.* (코드 1~8). spec 07 의 modifier.<열거형 이름> 꼴도 정본으로
        [Theory]
        [InlineData(ModifierId.Rush, 1, "whim.crowding", "modifier.rush")]
        [InlineData(ModifierId.ThickHide, 2, "whim.thick_hide", "modifier.thick_hide")]
        [InlineData(ModifierId.BigHerd, 3, "whim.big_pack", "modifier.big_herd")]
        [InlineData(ModifierId.ChiefMarch, 4, "whim.chief_parade", "modifier.chief_march")]
        [InlineData(ModifierId.LastBreath, 5, "whim.last_breath", "modifier.last_breath")]
        [InlineData(ModifierId.SlowBarrage, 6, "whim.slow_barrage", "modifier.slow_barrage")]
        [InlineData(ModifierId.LongNight, 7, "whim.long_night", "modifier.long_night")]
        [InlineData(ModifierId.NarrowField, 8, "whim.narrow_field", "modifier.narrow_field")]
        public void WhimRow(ModifierId value, int code, string canonicalId, string spec07Id)
        {
            Assert.Equal(code, (int)value);
            Assert.Equal(canonicalId, ContentIds.ToId(value));
            Assert.Equal(canonicalId, ContentIds.ResolveAlias(spec07Id));
            Assert.True(ContentIds.TryParse(spec07Id, out ModifierId v));
            Assert.Equal(value, v);
        }

        [Theory]
        [InlineData(FrameId.Swarm, "frame.swarm")]
        [InlineData(FrameId.Hunt, "frame.hunting_party")]
        [InlineData(FrameId.Battery, "frame.battery")]
        [InlineData(FrameId.Ambush, "frame.ambush")]
        [InlineData(FrameId.Mixed, "frame.mixed")]
        public void FrameRow(FrameId value, string canonicalId)
        {
            Assert.Equal(canonicalId, ContentIds.ToId(value));
            Assert.True(ContentIds.TryParse(canonicalId, out FrameId v));
            Assert.Equal(value, v);
        }

        [Fact]
        public void FrameRow_Spec07HuntForm()
        {
            Assert.Equal("frame.hunting_party", ContentIds.ResolveAlias("frame.hunt"));
            Assert.True(ContentIds.TryParse("frame.hunt", out FrameId v));
            Assert.Equal(FrameId.Hunt, v);
        }

        // 시설 줄 전부: 정본 열거형 ↔ facility.*. spec 11 의 fac_* 는 별칭으로도 받지 않는다(카드 수락 조건)
        [Theory]
        [InlineData("fac_bonfire", FacilityId.BigCampfire, "facility.big_campfire")]
        [InlineData("fac_stall_street", FacilityId.StallStreet, "facility.stall_street")]
        [InlineData("fac_peddler_cart", FacilityId.PeddlerCart, "facility.peddler_cart")]
        [InlineData("fac_duel_board", FacilityId.DuelBoard, "facility.duel_board")]
        [InlineData("fac_stone_path", FacilityId.ArenaPath, "facility.arena_path")]
        [InlineData("fac_heir_monument", FacilityId.HeirMonument, "facility.heir_monument")]
        [InlineData("fac_nuru_chair", FacilityId.MasterChair, "facility.master_chair")]
        [InlineData("fac_practice_dummy", FacilityId.TrainingDummy, "facility.training_dummy")]
        [InlineData("fac_crow_mailbox", FacilityId.CrowMailbox, "facility.crow_mailbox")]
        [InlineData("fac_wilds_gate", FacilityId.FieldGate, "facility.field_gate")]
        public void FacilityRows_FacIsNotAccepted(string spec11Id, FacilityId canonical, string canonicalId)
        {
            Assert.Equal(canonicalId, ContentIds.ToId(canonical));
            Assert.True(ContentIds.TryParse(canonicalId, out FacilityId v));
            Assert.Equal(canonical, v);
            Assert.Equal(spec11Id, ContentIds.ResolveAlias(spec11Id));
            Assert.False(ContentIds.TryParse(spec11Id, out FacilityId _), spec11Id);
            // fac_ 를 facility. 로 바꿔 끼운 꼴도 정본과 다르면 받지 않는다(facility.bonfire, facility.nuru_chair …)
            string swapped = "facility." + spec11Id.Substring("fac_".Length);
            if (swapped != canonicalId)
            {
                Assert.False(ContentIds.TryParse(swapped, out FacilityId _), swapped);
            }
        }

        [Fact]
        public void UnknownStringsPassThroughAndFail()
        {
            Assert.Equal("block.nope", ContentIds.ResolveAlias("block.nope"));
            Assert.False(ContentIds.TryParse("block.nope", out BlockId _));
            Assert.False(ContentIds.TryParse("", out BlockId _));
            Assert.False(ContentIds.TryParse("Block.Fireball", out BlockId _));
        }
    }
}
