using Wizard.Content;

namespace Wizard.Content.Tests.Ids
{
    // spec 14 3절의 표를 한 줄씩 옮긴 것(대조 기준). 구현에서 베끼지 않고 명세의 표에서 옮겼다.
    // 코드 칸이 없는 표는 CONTRACTS 2.1의 규칙대로 "표의 순서, 1부터"를 코드로 적었다(ItemCode·CurrencyCode는 CONTRACTS 2.1의 값).
    internal static class Spec14Tables
    {
        // 3.1 속성 — 6줄
        public static readonly (int Code, string Id, Element Value)[] Elements =
        {
            (0, "element.neutral", Element.None),
            (1, "element.fire", Element.Fire),
            (2, "element.lightning", Element.Lightning),
            (3, "element.arcane", Element.Arcane),
            (4, "element.ice", Element.Ice),
            (5, "element.dark", Element.Dark),
        };

        // 3.2 캐릭터, 지팡이, 패시브, 지팡이 특성, 고유 비기 — 5줄. 속성은 2.3 표(화염, 번개, 비전, 냉기, 암흑)
        public static readonly CharacterRow[] Characters =
        {
            new CharacterRow(1, "char.1", CharacterId.Char1, "wand.1", "passive.overflow", PassiveId.Overflow,
                "wand_trait.heat_at_80", WandTraitId.HeatAt80, "art.kiln", ArtId.Kiln, Element.Fire),
            new CharacterRow(2, "char.2", CharacterId.Char2, "wand.2", "passive.lightning_rod", PassiveId.LightningRod,
                "wand_trait.three_mutations", WandTraitId.ThreeMutations, "art.bolt_dash", ArtId.BoltDash, Element.Lightning),
            new CharacterRow(3, "char.3", CharacterId.Char3, "wand.3", "passive.underline", PassiveId.Underline,
                "wand_trait.depth_decay", WandTraitId.DepthDecay, "art.copy_circle", ArtId.CopyCircle, Element.Arcane),
            new CharacterRow(4, "char.4", CharacterId.Char4, "wand.4", "passive.thin_ice", PassiveId.ThinIce,
                "wand_trait.keep_crystal", WandTraitId.KeepCrystal, "art.ice_screen", ArtId.IceScreen, Element.Ice),
            new CharacterRow(5, "char.5", CharacterId.Char5, "wand.5", "passive.shadow_contract", PassiveId.ShadowContract,
                "wand_trait.on_credit", WandTraitId.OnCredit, "art.shadow_hand", ArtId.ShadowHand, Element.Dark),
        };

        // 3.3 주문 블록 16종 — 16줄 (코드, id, 종류, 등급, 속성)
        public static readonly (int Code, string Id, BlockId Value, BlockKind Kind, Rarity Rarity, Element Element)[] Blocks =
        {
            (1, "block.magic_bolt", BlockId.MagicBolt, BlockKind.Projectile, Rarity.Common, Element.Arcane),
            (2, "block.fireball", BlockId.Fireball, BlockKind.Projectile, Rarity.Rare, Element.Fire),
            (3, "block.chain_lightning", BlockId.ChainLightning, BlockKind.Projectile, Rarity.Hero, Element.Lightning),
            (4, "block.boomerang", BlockId.Boomerang, BlockKind.Projectile, Rarity.Rare, Element.Arcane),
            (5, "block.ice_spike", BlockId.IceSpike, BlockKind.Projectile, Rarity.Rare, Element.Ice),
            (6, "block.shadow_orb", BlockId.ShadowOrb, BlockKind.Projectile, Rarity.Rare, Element.Dark),
            (7, "block.split_two", BlockId.SplitTwo, BlockKind.Control, Rarity.Common, Element.None),
            (8, "block.split_three", BlockId.SplitThree, BlockKind.Control, Rarity.Rare, Element.None),
            (9, "block.on_hit_trigger", BlockId.OnHitTrigger, BlockKind.Control, Rarity.Rare, Element.None),
            (10, "block.wall_bounce", BlockId.WallBounce, BlockKind.Modifier, Rarity.Common, Element.None),
            (11, "block.double_size", BlockId.DoubleSize, BlockKind.Modifier, Rarity.Common, Element.Fire),
            (12, "block.pierce", BlockId.Pierce, BlockKind.Modifier, Rarity.Common, Element.Arcane),
            (13, "block.frost", BlockId.Frost, BlockKind.Modifier, Rarity.Common, Element.Ice),
            (14, "block.homing", BlockId.Homing, BlockKind.Modifier, Rarity.Hero, Element.Lightning),
            (15, "block.triple_heat", BlockId.TripleHeat, BlockKind.Modifier, Rarity.Hero, Element.Fire),
            (16, "block.corrosion", BlockId.Corrosion, BlockKind.Modifier, Rarity.Hero, Element.Dark),
        };

        // 3.4 비기 15종 — 15줄. 공용은 Baseline(주인 없음), 고유는 주인 캐릭터
        public static readonly (int Code, string Id, ArtId Value, CharacterId Owner)[] Arts =
        {
            (1, "art.burst", ArtId.Burst, CharacterId.Baseline),
            (2, "art.recoil", ArtId.Recoil, CharacterId.Baseline),
            (3, "art.fan", ArtId.Fan, CharacterId.Baseline),
            (4, "art.wings", ArtId.Wings, CharacterId.Baseline),
            (5, "art.afterimage", ArtId.Afterimage, CharacterId.Baseline),
            (6, "art.spear", ArtId.Spear, CharacterId.Baseline),
            (7, "art.vent", ArtId.Vent, CharacterId.Baseline),
            (8, "art.octastar", ArtId.Octastar, CharacterId.Baseline),
            (9, "art.thunder_spear", ArtId.ThunderSpear, CharacterId.Baseline),
            (10, "art.bloom", ArtId.Bloom, CharacterId.Baseline),
            (11, "art.kiln", ArtId.Kiln, CharacterId.Char1),
            (12, "art.bolt_dash", ArtId.BoltDash, CharacterId.Char2),
            (13, "art.copy_circle", ArtId.CopyCircle, CharacterId.Char3),
            (14, "art.ice_screen", ArtId.IceScreen, CharacterId.Char4),
            (15, "art.shadow_hand", ArtId.ShadowHand, CharacterId.Char5),
        };

        // 3.5 폭주 변이 6종 — 6줄
        public static readonly (int Code, string Id, MutationId Value)[] Mutations =
        {
            (1, "mutation.twisted_barrel", MutationId.TwistedBarrel),
            (2, "mutation.unstable_core", MutationId.UnstableCore),
            (3, "mutation.hypersensitive_circuit", MutationId.HypersensitiveCircuit),
            (4, "mutation.gluttony", MutationId.Gluttony),
            (5, "mutation.mirror_image", MutationId.MirrorImage),
            (6, "mutation.glass_wand", MutationId.GlassWand),
        };

        // 3.6 일반 적 12종과 졸개 — 13줄 (마지막 줄이 졸개)
        public static readonly (int Code, string Id, EnemyTypeId Value, bool Minion)[] Enemies =
        {
            (1, "enemy.slime_moss", EnemyTypeId.SlimeMoss, false),
            (2, "enemy.wolf_bramble", EnemyTypeId.WolfBramble, false),
            (3, "enemy.golem_menhir", EnemyTypeId.GolemMenhir, false),
            (4, "enemy.shaman_cap", EnemyTypeId.ShamanCap, false),
            (5, "enemy.bug_lantern", EnemyTypeId.BugLantern, false),
            (6, "enemy.hog_needle", EnemyTypeId.HogNeedle, false),
            (7, "enemy.weasel_pot", EnemyTypeId.WeaselPot, false),
            (8, "enemy.bat_dusk", EnemyTypeId.BatDusk, false),
            (9, "enemy.wisp_stray", EnemyTypeId.WispStray, false),
            (10, "enemy.vine_thorn", EnemyTypeId.VineThorn, false),
            (11, "enemy.boar_tusk", EnemyTypeId.BoarTusk, false),
            (12, "enemy.toad_drum", EnemyTypeId.ToadDrum, false),
            (101, "enemy.spore_seedling", EnemyTypeId.SporeSeedling, true),
        };

        // 3.7 엘리트 12종 — 12줄 (코드, id, 바탕 id)
        public static readonly (int Code, string Id, string BaseId)[] Elites =
        {
            (201, "elite.slime_moss", "enemy.slime_moss"),
            (202, "elite.wolf_bramble", "enemy.wolf_bramble"),
            (203, "elite.golem_menhir", "enemy.golem_menhir"),
            (204, "elite.shaman_cap", "enemy.shaman_cap"),
            (205, "elite.bug_lantern", "enemy.bug_lantern"),
            (206, "elite.hog_needle", "enemy.hog_needle"),
            (207, "elite.weasel_pot", "enemy.weasel_pot"),
            (208, "elite.bat_dusk", "enemy.bat_dusk"),
            (209, "elite.wisp_stray", "enemy.wisp_stray"),
            (210, "elite.vine_thorn", "enemy.vine_thorn"),
            (211, "elite.boar_tusk", "enemy.boar_tusk"),
            (212, "elite.toad_drum", "enemy.toad_drum"),
        };

        // 3.8 보스 6종 — 6줄
        public static readonly (int Code, string Id, BossId Value)[] Bosses =
        {
            (301, "boss.kiln_boar", BossId.KilnBoar),
            (302, "boss.mirror_phantom", BossId.MirrorPhantom),
            (303, "boss.spore_queen", BossId.SporeQueen),
            (304, "boss.storm_owl", BossId.StormOwl),
            (305, "boss.frost_goat", BossId.FrostGoat),
            (306, "boss.night_serpent", BossId.NightSerpent),
        };

        // 3.9 꼬리표 — 6줄 (코드 = 비트)
        public static readonly (int Code, string Id, EnemyTag Value)[] Tags =
        {
            (1, "tag.elite", EnemyTag.Elite),
            (2, "tag.heavy", EnemyTag.Heavy),
            (4, "tag.boss", EnemyTag.Boss),
            (8, "tag.flying", EnemyTag.Flying),
            (16, "tag.burrowed", EnemyTag.Burrowed),
            (32, "tag.rooted", EnemyTag.Rooted),
        };

        // 3.9 시간대 — 4줄
        public static readonly (int Code, string Id, TimeOfDay Value)[] TimesOfDay =
        {
            (1, "tod.day", TimeOfDay.Day),
            (2, "tod.dusk", TimeOfDay.Dusk),
            (3, "tod.night", TimeOfDay.Night),
            (4, "tod.boss", TimeOfDay.BossWave),
        };

        // 3.9 웨이브 틀 — 5개(나열 순서)
        public static readonly (int Code, string Id, FrameId Value)[] Frames =
        {
            (1, "frame.swarm", FrameId.Swarm),
            (2, "frame.hunting_party", FrameId.Hunt),
            (3, "frame.battery", FrameId.Battery),
            (4, "frame.ambush", FrameId.Ambush),
            (5, "frame.mixed", FrameId.Mixed),
        };

        // 3.9 야생의 변덕 — 8줄
        public static readonly (int Code, string Id, ModifierId Value)[] Whims =
        {
            (1, "whim.crowding", ModifierId.Rush),
            (2, "whim.thick_hide", ModifierId.ThickHide),
            (3, "whim.big_pack", ModifierId.BigHerd),
            (4, "whim.chief_parade", ModifierId.ChiefMarch),
            (5, "whim.last_breath", ModifierId.LastBreath),
            (6, "whim.slow_barrage", ModifierId.SlowBarrage),
            (7, "whim.long_night", ModifierId.LongNight),
            (8, "whim.narrow_field", ModifierId.NarrowField),
        };

        // 3.9 날씨와 돌발 사건 — 5개(나열 순서)
        public static readonly (int Code, string Id, WeatherEventId Value)[] Events =
        {
            (1, "event.shower", WeatherEventId.Shower),
            (2, "event.fog", WeatherEventId.Fog),
            (3, "event.star_crystal", WeatherEventId.StarCrystal),
            (4, "event.master_advice", WeatherEventId.MasterAdvice),
            (5, "event.discarded_item", WeatherEventId.DiscardedItem),
        };

        // 3.10 재료, 부적, 그 밖의 물건 — 11줄. 코드는 CONTRACTS 2.1 ItemCode
        public static readonly (int Code, string Id, ItemCode Value)[] Items =
        {
            (1, "mat.stone_low", ItemCode.StoneLow),
            (2, "mat.stone_mid", ItemCode.StoneMid),
            (3, "mat.stone_high", ItemCode.StoneHigh),
            (4, "charm.safeguard", ItemCode.CharmSafeguard),
            (10, "item.material_bundle", ItemCode.MaterialBundle),
            (11, "item.crow_bundle", ItemCode.CrowBundle),
            (12, "item.season_fabric", ItemCode.SeasonFabric),
            (13, "item.wand_box", ItemCode.WandBox),
            (14, "item.library_expansion", ItemCode.LibraryExpansion),
            (15, "item.rename", ItemCode.Rename),
            (20, "item.health_shard", ItemCode.HealthShard),
        };

        // 3.11 재화 — 재화 3줄(코드는 CONTRACTS 2.1 CurrencyCode). 넷째 줄 원정 지갑은 표시 전용(U-23)이라 따로 둔다
        public static readonly (int Code, string Id, string Key, CurrencyCode Value)[] Currencies =
        {
            (0, "currency.gold", "gold", CurrencyCode.Gold),
            (1, "currency.season_coin", "season_coin", CurrencyCode.SeasonCoin),
            (2, "currency.starlight", "starlight", CurrencyCode.Starlight),
        };

        public const string ExpeditionPurseId = "currency.expedition_purse";

        // 3.12 그 밖의 칭호 — 8줄(title.record.* 줄은 목록이 아니라 규칙이라 뺐다, U-02). 강화 단계 칭호 25개는 시험에서 만든다
        public static readonly (string Id, TitleKind Kind)[] OtherTitles =
        {
            ("title.record.first_night", TitleKind.Record),
            ("title.record.three_nights", TitleKind.Record),
            ("title.record.ten_days", TitleKind.Record),
            ("title.revenge.mirror_breaker", TitleKind.Revenge),
            ("title.season.heir", TitleKind.Season),
            ("title.season.top_disciple", TitleKind.Season),
            ("title.feat.bloom_100", TitleKind.Feat),
            ("title.feat.flawless_night", TitleKind.Feat),
        };

        // 3.13 시설 — 10줄(표의 순서)
        public static readonly (int Code, string Id, FacilityId Value)[] Facilities =
        {
            (1, "facility.big_campfire", FacilityId.BigCampfire),
            (2, "facility.stall_street", FacilityId.StallStreet),
            (3, "facility.peddler_cart", FacilityId.PeddlerCart),
            (4, "facility.duel_board", FacilityId.DuelBoard),
            (5, "facility.arena_path", FacilityId.ArenaPath),
            (6, "facility.heir_monument", FacilityId.HeirMonument),
            (7, "facility.master_chair", FacilityId.MasterChair),
            (8, "facility.training_dummy", FacilityId.TrainingDummy),
            (9, "facility.crow_mailbox", FacilityId.CrowMailbox),
            (10, "facility.field_gate", FacilityId.FieldGate),
        };

        // 3.13이 가리키는 2.3 "장소·인물" — 8줄 (정식 id, 별칭). 열거형이 없다(CONTRACTS 2.1)
        public static readonly (string Id, string? Alias)[] PlacesAndPeople =
        {
            ("npc.master", "master"),
            ("place.field", "field"),
            ("place.plaza", "plaza"),
            ("place.arena", "arena"),
            ("place.tower", null),
            ("place.workbench", null),
            ("npc.peddler", null),
            ("npc.crow", null),
        };

        // 3.14 치장 칸 — 4줄
        public static readonly (int Code, string Id, CosmeticSlot Value)[] CosmeticSlots =
        {
            (1, "cosmetic_slot.head", CosmeticSlot.Head),
            (2, "cosmetic_slot.cloak", CosmeticSlot.Cloak),
            (3, "cosmetic_slot.footprints", CosmeticSlot.Footprints),
            (4, "cosmetic_slot.seat", CosmeticSlot.Seat),
        };

        // 3.14 옷장 분류 12 — 나열 순서
        public static readonly (int Code, string Id, CosmeticCategory Value)[] CosmeticCategories =
        {
            (1, "cosmetic_cat.headwear", CosmeticCategory.Headwear),
            (2, "cosmetic_cat.cloak", CosmeticCategory.Cloak),
            (3, "cosmetic_cat.footprints", CosmeticCategory.Footprints),
            (4, "cosmetic_cat.seat", CosmeticCategory.Seat),
            (5, "cosmetic_cat.wand", CosmeticCategory.Wand),
            (6, "cosmetic_cat.trail", CosmeticCategory.Trail),
            (7, "cosmetic_cat.kill_effect", CosmeticCategory.KillEffect),
            (8, "cosmetic_cat.ritual", CosmeticCategory.Ritual),
            (9, "cosmetic_cat.emote", CosmeticCategory.Emote),
            (10, "cosmetic_cat.border", CosmeticCategory.Border),
            (11, "cosmetic_cat.crest", CosmeticCategory.Crest),
            (12, "cosmetic_cat.title", CosmeticCategory.Title),
        };

        // 3.14 지금 정해진 꾸미기 — 4줄 (id, 분류). 낱개 꾸미기는 데이터의 문자열 id다(열거형 없음)
        public static readonly (string Id, CosmeticCategory Category)[] FixedCosmetics =
        {
            ("cosmetic.headwear.cracked_mirror_shard", CosmeticCategory.Headwear),
            ("cosmetic.cloak.master_lap_blanket", CosmeticCategory.Cloak),
            ("cosmetic.wand.clothesline", CosmeticCategory.Wand),
            ("cosmetic.seat.rocking_chair", CosmeticCategory.Seat),
        };

        // 3.14 야생 물 — 4줄 (열거형 없음. 코드가 id의 키다)
        public static readonly (int Code, string Id)[] WildStains =
        {
            (1, "wild_stain.1"),
            (2, "wild_stain.2"),
            (3, "wild_stain.3"),
            (4, "wild_stain.4"),
        };

        // 3.15 리그 — 3줄
        public static readonly (int Code, string Id, LeagueId Value)[] Leagues =
        {
            (1, "league.sprout", LeagueId.Sprout),
            (2, "league.beast", LeagueId.Beast),
            (3, "league.heir", LeagueId.Heir),
        };

        // 3.15 감정표현 8종 — 8줄
        public static readonly (int Code, string Id, EmoteId Value)[] Emotes =
        {
            (1, "emote.greet", EmoteId.Greet),
            (2, "emote.clap", EmoteId.Clap),
            (3, "emote.taunt", EmoteId.Taunt),
            (4, "emote.bow", EmoteId.Bow),
            (5, "emote.sit", EmoteId.Sit),
            (6, "emote.dance", EmoteId.Dance),
            (7, "emote.raise_wand", EmoteId.RaiseWand),
            (8, "emote.despair", EmoteId.Despair),
        };

        // 3.15 채팅 채널 — 7줄(표의 순서)
        public static readonly (int Code, string Id, ChatChannel Value)[] ChatChannels =
        {
            (1, "chat.nearby", ChatChannel.Nearby),
            (2, "chat.plaza", ChatChannel.Plaza),
            (3, "chat.trade", ChatChannel.Trade),
            (4, "chat.whisper", ChatChannel.Whisper),
            (5, "chat.friends", ChatChannel.Friends),
            (6, "chat.duel_room", ChatChannel.DuelRoom),
            (7, "chat.system", ChatChannel.System),
        };
    }

    internal sealed class CharacterRow
    {
        public CharacterRow(int code, string id, CharacterId value, string wandText, string passiveText, PassiveId passive,
            string traitText, WandTraitId trait, string artText, ArtId art, Element element)
        {
            Code = code;
            Id = id;
            Value = value;
            WandText = wandText;
            PassiveText = passiveText;
            Passive = passive;
            TraitText = traitText;
            Trait = trait;
            ArtText = artText;
            Art = art;
            Element = element;
        }

        public int Code { get; }
        public string Id { get; }
        public CharacterId Value { get; }
        public string WandText { get; }
        public string PassiveText { get; }
        public PassiveId Passive { get; }
        public string TraitText { get; }
        public WandTraitId Trait { get; }
        public string ArtText { get; }
        public ArtId Art { get; }
        public Element Element { get; }
    }
}
