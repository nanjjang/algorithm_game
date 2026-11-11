using System;

namespace Wizard.Content
{
    // CONTRACTS 2.1. 열거형 ↔ 문자열 id. 저장·통신·DB·데이터 파일은 문자열 id 를 쓴다
    public static class ContentIds
    {
        public static string ToId(CharacterId v)
        {
            switch (v)
            {
                case CharacterId.Char1: return "char.1";
                case CharacterId.Char2: return "char.2";
                case CharacterId.Char3: return "char.3";
                case CharacterId.Char4: return "char.4";
                case CharacterId.Char5: return "char.5";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out CharacterId v)
        {
            switch (ResolveAlias(id))
            {
                case "char.1": v = CharacterId.Char1; return true;
                case "char.2": v = CharacterId.Char2; return true;
                case "char.3": v = CharacterId.Char3; return true;
                case "char.4": v = CharacterId.Char4; return true;
                case "char.5": v = CharacterId.Char5; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(Element v)
        {
            switch (v)
            {
                case Element.None: return "element.neutral";
                case Element.Fire: return "element.fire";
                case Element.Lightning: return "element.lightning";
                case Element.Arcane: return "element.arcane";
                case Element.Ice: return "element.ice";
                case Element.Dark: return "element.dark";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out Element v)
        {
            switch (ResolveAlias(id))
            {
                case "element.neutral": v = Element.None; return true;
                case "element.fire": v = Element.Fire; return true;
                case "element.lightning": v = Element.Lightning; return true;
                case "element.arcane": v = Element.Arcane; return true;
                case "element.ice": v = Element.Ice; return true;
                case "element.dark": v = Element.Dark; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(BlockId v)
        {
            switch (v)
            {
                case BlockId.MagicBolt: return "block.magic_bolt";
                case BlockId.Fireball: return "block.fireball";
                case BlockId.ChainLightning: return "block.chain_lightning";
                case BlockId.Boomerang: return "block.boomerang";
                case BlockId.IceSpike: return "block.ice_spike";
                case BlockId.ShadowOrb: return "block.shadow_orb";
                case BlockId.SplitTwo: return "block.split_two";
                case BlockId.SplitThree: return "block.split_three";
                case BlockId.OnHitTrigger: return "block.on_hit_trigger";
                case BlockId.WallBounce: return "block.wall_bounce";
                case BlockId.DoubleSize: return "block.double_size";
                case BlockId.Pierce: return "block.pierce";
                case BlockId.Frost: return "block.frost";
                case BlockId.Homing: return "block.homing";
                case BlockId.TripleHeat: return "block.triple_heat";
                case BlockId.Corrosion: return "block.corrosion";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out BlockId v)
        {
            switch (ResolveAlias(id))
            {
                case "block.magic_bolt": v = BlockId.MagicBolt; return true;
                case "block.fireball": v = BlockId.Fireball; return true;
                case "block.chain_lightning": v = BlockId.ChainLightning; return true;
                case "block.boomerang": v = BlockId.Boomerang; return true;
                case "block.ice_spike": v = BlockId.IceSpike; return true;
                case "block.shadow_orb": v = BlockId.ShadowOrb; return true;
                case "block.split_two": v = BlockId.SplitTwo; return true;
                case "block.split_three": v = BlockId.SplitThree; return true;
                case "block.on_hit_trigger": v = BlockId.OnHitTrigger; return true;
                case "block.wall_bounce": v = BlockId.WallBounce; return true;
                case "block.double_size": v = BlockId.DoubleSize; return true;
                case "block.pierce": v = BlockId.Pierce; return true;
                case "block.frost": v = BlockId.Frost; return true;
                case "block.homing": v = BlockId.Homing; return true;
                case "block.triple_heat": v = BlockId.TripleHeat; return true;
                case "block.corrosion": v = BlockId.Corrosion; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(ArtId v)
        {
            switch (v)
            {
                case ArtId.Burst: return "art.burst";
                case ArtId.Recoil: return "art.recoil";
                case ArtId.Fan: return "art.fan";
                case ArtId.Wings: return "art.wings";
                case ArtId.Afterimage: return "art.afterimage";
                case ArtId.Spear: return "art.spear";
                case ArtId.Vent: return "art.vent";
                case ArtId.Octastar: return "art.octastar";
                case ArtId.ThunderSpear: return "art.thunder_spear";
                case ArtId.Bloom: return "art.bloom";
                case ArtId.Kiln: return "art.kiln";
                case ArtId.BoltDash: return "art.bolt_dash";
                case ArtId.CopyCircle: return "art.copy_circle";
                case ArtId.IceScreen: return "art.ice_screen";
                case ArtId.ShadowHand: return "art.shadow_hand";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out ArtId v)
        {
            switch (ResolveAlias(id))
            {
                case "art.burst": v = ArtId.Burst; return true;
                case "art.recoil": v = ArtId.Recoil; return true;
                case "art.fan": v = ArtId.Fan; return true;
                case "art.wings": v = ArtId.Wings; return true;
                case "art.afterimage": v = ArtId.Afterimage; return true;
                case "art.spear": v = ArtId.Spear; return true;
                case "art.vent": v = ArtId.Vent; return true;
                case "art.octastar": v = ArtId.Octastar; return true;
                case "art.thunder_spear": v = ArtId.ThunderSpear; return true;
                case "art.bloom": v = ArtId.Bloom; return true;
                case "art.kiln": v = ArtId.Kiln; return true;
                case "art.bolt_dash": v = ArtId.BoltDash; return true;
                case "art.copy_circle": v = ArtId.CopyCircle; return true;
                case "art.ice_screen": v = ArtId.IceScreen; return true;
                case "art.shadow_hand": v = ArtId.ShadowHand; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(PassiveId v)
        {
            switch (v)
            {
                case PassiveId.Overflow: return "passive.overflow";
                case PassiveId.LightningRod: return "passive.lightning_rod";
                case PassiveId.Underline: return "passive.underline";
                case PassiveId.ThinIce: return "passive.thin_ice";
                case PassiveId.ShadowContract: return "passive.shadow_contract";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out PassiveId v)
        {
            switch (ResolveAlias(id))
            {
                case "passive.overflow": v = PassiveId.Overflow; return true;
                case "passive.lightning_rod": v = PassiveId.LightningRod; return true;
                case "passive.underline": v = PassiveId.Underline; return true;
                case "passive.thin_ice": v = PassiveId.ThinIce; return true;
                case "passive.shadow_contract": v = PassiveId.ShadowContract; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(WandTraitId v)
        {
            switch (v)
            {
                case WandTraitId.HeatAt80: return "wand_trait.heat_at_80";
                case WandTraitId.ThreeMutations: return "wand_trait.three_mutations";
                case WandTraitId.DepthDecay: return "wand_trait.depth_decay";
                case WandTraitId.KeepCrystal: return "wand_trait.keep_crystal";
                case WandTraitId.OnCredit: return "wand_trait.on_credit";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out WandTraitId v)
        {
            switch (ResolveAlias(id))
            {
                case "wand_trait.heat_at_80": v = WandTraitId.HeatAt80; return true;
                case "wand_trait.three_mutations": v = WandTraitId.ThreeMutations; return true;
                case "wand_trait.depth_decay": v = WandTraitId.DepthDecay; return true;
                case "wand_trait.keep_crystal": v = WandTraitId.KeepCrystal; return true;
                case "wand_trait.on_credit": v = WandTraitId.OnCredit; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(MutationId v)
        {
            switch (v)
            {
                case MutationId.TwistedBarrel: return "mutation.twisted_barrel";
                case MutationId.UnstableCore: return "mutation.unstable_core";
                case MutationId.HypersensitiveCircuit: return "mutation.hypersensitive_circuit";
                case MutationId.Gluttony: return "mutation.gluttony";
                case MutationId.MirrorImage: return "mutation.mirror_image";
                case MutationId.GlassWand: return "mutation.glass_wand";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out MutationId v)
        {
            switch (ResolveAlias(id))
            {
                case "mutation.twisted_barrel": v = MutationId.TwistedBarrel; return true;
                case "mutation.unstable_core": v = MutationId.UnstableCore; return true;
                case "mutation.hypersensitive_circuit": v = MutationId.HypersensitiveCircuit; return true;
                case "mutation.gluttony": v = MutationId.Gluttony; return true;
                case "mutation.mirror_image": v = MutationId.MirrorImage; return true;
                case "mutation.glass_wand": v = MutationId.GlassWand; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(EnemyTypeId v)
        {
            switch (v)
            {
                case EnemyTypeId.SlimeMoss: return "enemy.slime_moss";
                case EnemyTypeId.WolfBramble: return "enemy.wolf_bramble";
                case EnemyTypeId.GolemMenhir: return "enemy.golem_menhir";
                case EnemyTypeId.ShamanCap: return "enemy.shaman_cap";
                case EnemyTypeId.BugLantern: return "enemy.bug_lantern";
                case EnemyTypeId.HogNeedle: return "enemy.hog_needle";
                case EnemyTypeId.WeaselPot: return "enemy.weasel_pot";
                case EnemyTypeId.BatDusk: return "enemy.bat_dusk";
                case EnemyTypeId.WispStray: return "enemy.wisp_stray";
                case EnemyTypeId.VineThorn: return "enemy.vine_thorn";
                case EnemyTypeId.BoarTusk: return "enemy.boar_tusk";
                case EnemyTypeId.ToadDrum: return "enemy.toad_drum";
                case EnemyTypeId.SporeSeedling: return "enemy.spore_seedling";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out EnemyTypeId v)
        {
            switch (ResolveAlias(id))
            {
                case "enemy.slime_moss": v = EnemyTypeId.SlimeMoss; return true;
                case "enemy.wolf_bramble": v = EnemyTypeId.WolfBramble; return true;
                case "enemy.golem_menhir": v = EnemyTypeId.GolemMenhir; return true;
                case "enemy.shaman_cap": v = EnemyTypeId.ShamanCap; return true;
                case "enemy.bug_lantern": v = EnemyTypeId.BugLantern; return true;
                case "enemy.hog_needle": v = EnemyTypeId.HogNeedle; return true;
                case "enemy.weasel_pot": v = EnemyTypeId.WeaselPot; return true;
                case "enemy.bat_dusk": v = EnemyTypeId.BatDusk; return true;
                case "enemy.wisp_stray": v = EnemyTypeId.WispStray; return true;
                case "enemy.vine_thorn": v = EnemyTypeId.VineThorn; return true;
                case "enemy.boar_tusk": v = EnemyTypeId.BoarTusk; return true;
                case "enemy.toad_drum": v = EnemyTypeId.ToadDrum; return true;
                case "enemy.spore_seedling": v = EnemyTypeId.SporeSeedling; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(BossId v)
        {
            switch (v)
            {
                case BossId.KilnBoar: return "boss.kiln_boar";
                case BossId.MirrorPhantom: return "boss.mirror_phantom";
                case BossId.SporeQueen: return "boss.spore_queen";
                case BossId.StormOwl: return "boss.storm_owl";
                case BossId.FrostGoat: return "boss.frost_goat";
                case BossId.NightSerpent: return "boss.night_serpent";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out BossId v)
        {
            switch (ResolveAlias(id))
            {
                case "boss.kiln_boar": v = BossId.KilnBoar; return true;
                case "boss.mirror_phantom": v = BossId.MirrorPhantom; return true;
                case "boss.spore_queen": v = BossId.SporeQueen; return true;
                case "boss.storm_owl": v = BossId.StormOwl; return true;
                case "boss.frost_goat": v = BossId.FrostGoat; return true;
                case "boss.night_serpent": v = BossId.NightSerpent; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(TimeOfDay v)
        {
            switch (v)
            {
                case TimeOfDay.Day: return "tod.day";
                case TimeOfDay.Dusk: return "tod.dusk";
                case TimeOfDay.Night: return "tod.night";
                case TimeOfDay.BossWave: return "tod.boss";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out TimeOfDay v)
        {
            switch (ResolveAlias(id))
            {
                case "tod.day": v = TimeOfDay.Day; return true;
                case "tod.dusk": v = TimeOfDay.Dusk; return true;
                case "tod.night": v = TimeOfDay.Night; return true;
                case "tod.boss": v = TimeOfDay.BossWave; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(FrameId v)
        {
            switch (v)
            {
                case FrameId.Swarm: return "frame.swarm";
                case FrameId.Hunt: return "frame.hunting_party";
                case FrameId.Battery: return "frame.battery";
                case FrameId.Ambush: return "frame.ambush";
                case FrameId.Mixed: return "frame.mixed";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out FrameId v)
        {
            switch (ResolveAlias(id))
            {
                case "frame.swarm": v = FrameId.Swarm; return true;
                case "frame.hunting_party": v = FrameId.Hunt; return true;
                case "frame.battery": v = FrameId.Battery; return true;
                case "frame.ambush": v = FrameId.Ambush; return true;
                case "frame.mixed": v = FrameId.Mixed; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(ModifierId v)
        {
            switch (v)
            {
                case ModifierId.Rush: return "whim.crowding";
                case ModifierId.ThickHide: return "whim.thick_hide";
                case ModifierId.BigHerd: return "whim.big_pack";
                case ModifierId.ChiefMarch: return "whim.chief_parade";
                case ModifierId.LastBreath: return "whim.last_breath";
                case ModifierId.SlowBarrage: return "whim.slow_barrage";
                case ModifierId.LongNight: return "whim.long_night";
                case ModifierId.NarrowField: return "whim.narrow_field";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out ModifierId v)
        {
            switch (ResolveAlias(id))
            {
                case "whim.crowding": v = ModifierId.Rush; return true;
                case "whim.thick_hide": v = ModifierId.ThickHide; return true;
                case "whim.big_pack": v = ModifierId.BigHerd; return true;
                case "whim.chief_parade": v = ModifierId.ChiefMarch; return true;
                case "whim.last_breath": v = ModifierId.LastBreath; return true;
                case "whim.slow_barrage": v = ModifierId.SlowBarrage; return true;
                case "whim.long_night": v = ModifierId.LongNight; return true;
                case "whim.narrow_field": v = ModifierId.NarrowField; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(LeagueId v)
        {
            switch (v)
            {
                case LeagueId.Sprout: return "league.sprout";
                case LeagueId.Beast: return "league.beast";
                case LeagueId.Heir: return "league.heir";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out LeagueId v)
        {
            switch (ResolveAlias(id))
            {
                case "league.sprout": v = LeagueId.Sprout; return true;
                case "league.beast": v = LeagueId.Beast; return true;
                case "league.heir": v = LeagueId.Heir; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(WeatherEventId v)
        {
            switch (v)
            {
                case WeatherEventId.Shower: return "event.shower";
                case WeatherEventId.Fog: return "event.fog";
                case WeatherEventId.StarCrystal: return "event.star_crystal";
                case WeatherEventId.MasterAdvice: return "event.master_advice";
                case WeatherEventId.DiscardedItem: return "event.discarded_item";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out WeatherEventId v)
        {
            switch (ResolveAlias(id))
            {
                case "event.shower": v = WeatherEventId.Shower; return true;
                case "event.fog": v = WeatherEventId.Fog; return true;
                case "event.star_crystal": v = WeatherEventId.StarCrystal; return true;
                case "event.master_advice": v = WeatherEventId.MasterAdvice; return true;
                case "event.discarded_item": v = WeatherEventId.DiscardedItem; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(ItemCode v)
        {
            switch (v)
            {
                case ItemCode.StoneLow: return "mat.stone_low";
                case ItemCode.StoneMid: return "mat.stone_mid";
                case ItemCode.StoneHigh: return "mat.stone_high";
                case ItemCode.CharmSafeguard: return "charm.safeguard";
                case ItemCode.MaterialBundle: return "item.material_bundle";
                case ItemCode.CrowBundle: return "item.crow_bundle";
                case ItemCode.SeasonFabric: return "item.season_fabric";
                case ItemCode.WandBox: return "item.wand_box";
                case ItemCode.LibraryExpansion: return "item.library_expansion";
                case ItemCode.Rename: return "item.rename";
                case ItemCode.HealthShard: return "item.health_shard";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out ItemCode v)
        {
            switch (ResolveAlias(id))
            {
                case "mat.stone_low": v = ItemCode.StoneLow; return true;
                case "mat.stone_mid": v = ItemCode.StoneMid; return true;
                case "mat.stone_high": v = ItemCode.StoneHigh; return true;
                case "charm.safeguard": v = ItemCode.CharmSafeguard; return true;
                case "item.material_bundle": v = ItemCode.MaterialBundle; return true;
                case "item.crow_bundle": v = ItemCode.CrowBundle; return true;
                case "item.season_fabric": v = ItemCode.SeasonFabric; return true;
                case "item.wand_box": v = ItemCode.WandBox; return true;
                case "item.library_expansion": v = ItemCode.LibraryExpansion; return true;
                case "item.rename": v = ItemCode.Rename; return true;
                case "item.health_shard": v = ItemCode.HealthShard; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(FacilityId v)
        {
            switch (v)
            {
                case FacilityId.BigCampfire: return "facility.big_campfire";
                case FacilityId.StallStreet: return "facility.stall_street";
                case FacilityId.PeddlerCart: return "facility.peddler_cart";
                case FacilityId.DuelBoard: return "facility.duel_board";
                case FacilityId.ArenaPath: return "facility.arena_path";
                case FacilityId.HeirMonument: return "facility.heir_monument";
                case FacilityId.MasterChair: return "facility.master_chair";
                case FacilityId.TrainingDummy: return "facility.training_dummy";
                case FacilityId.CrowMailbox: return "facility.crow_mailbox";
                case FacilityId.FieldGate: return "facility.field_gate";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out FacilityId v)
        {
            switch (ResolveAlias(id))
            {
                case "facility.big_campfire": v = FacilityId.BigCampfire; return true;
                case "facility.stall_street": v = FacilityId.StallStreet; return true;
                case "facility.peddler_cart": v = FacilityId.PeddlerCart; return true;
                case "facility.duel_board": v = FacilityId.DuelBoard; return true;
                case "facility.arena_path": v = FacilityId.ArenaPath; return true;
                case "facility.heir_monument": v = FacilityId.HeirMonument; return true;
                case "facility.master_chair": v = FacilityId.MasterChair; return true;
                case "facility.training_dummy": v = FacilityId.TrainingDummy; return true;
                case "facility.crow_mailbox": v = FacilityId.CrowMailbox; return true;
                case "facility.field_gate": v = FacilityId.FieldGate; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(EmoteId v)
        {
            switch (v)
            {
                case EmoteId.Greet: return "emote.greet";
                case EmoteId.Clap: return "emote.clap";
                case EmoteId.Taunt: return "emote.taunt";
                case EmoteId.Bow: return "emote.bow";
                case EmoteId.Sit: return "emote.sit";
                case EmoteId.Dance: return "emote.dance";
                case EmoteId.RaiseWand: return "emote.raise_wand";
                case EmoteId.Despair: return "emote.despair";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out EmoteId v)
        {
            switch (ResolveAlias(id))
            {
                case "emote.greet": v = EmoteId.Greet; return true;
                case "emote.clap": v = EmoteId.Clap; return true;
                case "emote.taunt": v = EmoteId.Taunt; return true;
                case "emote.bow": v = EmoteId.Bow; return true;
                case "emote.sit": v = EmoteId.Sit; return true;
                case "emote.dance": v = EmoteId.Dance; return true;
                case "emote.raise_wand": v = EmoteId.RaiseWand; return true;
                case "emote.despair": v = EmoteId.Despair; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(ChatChannel v)
        {
            switch (v)
            {
                case ChatChannel.Nearby: return "chat.nearby";
                case ChatChannel.Plaza: return "chat.plaza";
                case ChatChannel.Trade: return "chat.trade";
                case ChatChannel.Whisper: return "chat.whisper";
                case ChatChannel.Friends: return "chat.friends";
                case ChatChannel.DuelRoom: return "chat.duel_room";
                case ChatChannel.System: return "chat.system";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out ChatChannel v)
        {
            switch (ResolveAlias(id))
            {
                case "chat.nearby": v = ChatChannel.Nearby; return true;
                case "chat.plaza": v = ChatChannel.Plaza; return true;
                case "chat.trade": v = ChatChannel.Trade; return true;
                case "chat.whisper": v = ChatChannel.Whisper; return true;
                case "chat.friends": v = ChatChannel.Friends; return true;
                case "chat.duel_room": v = ChatChannel.DuelRoom; return true;
                case "chat.system": v = ChatChannel.System; return true;
                default: v = default; return false;
            }
        }

        public static string ToId(CosmeticCategory v)
        {
            switch (v)
            {
                case CosmeticCategory.Headwear: return "cosmetic_cat.headwear";
                case CosmeticCategory.Cloak: return "cosmetic_cat.cloak";
                case CosmeticCategory.Footprints: return "cosmetic_cat.footprints";
                case CosmeticCategory.Seat: return "cosmetic_cat.seat";
                case CosmeticCategory.Wand: return "cosmetic_cat.wand";
                case CosmeticCategory.Trail: return "cosmetic_cat.trail";
                case CosmeticCategory.KillEffect: return "cosmetic_cat.kill_effect";
                case CosmeticCategory.Ritual: return "cosmetic_cat.ritual";
                case CosmeticCategory.Emote: return "cosmetic_cat.emote";
                case CosmeticCategory.Border: return "cosmetic_cat.border";
                case CosmeticCategory.Crest: return "cosmetic_cat.crest";
                case CosmeticCategory.Title: return "cosmetic_cat.title";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out CosmeticCategory v)
        {
            switch (ResolveAlias(id))
            {
                case "cosmetic_cat.headwear": v = CosmeticCategory.Headwear; return true;
                case "cosmetic_cat.cloak": v = CosmeticCategory.Cloak; return true;
                case "cosmetic_cat.footprints": v = CosmeticCategory.Footprints; return true;
                case "cosmetic_cat.seat": v = CosmeticCategory.Seat; return true;
                case "cosmetic_cat.wand": v = CosmeticCategory.Wand; return true;
                case "cosmetic_cat.trail": v = CosmeticCategory.Trail; return true;
                case "cosmetic_cat.kill_effect": v = CosmeticCategory.KillEffect; return true;
                case "cosmetic_cat.ritual": v = CosmeticCategory.Ritual; return true;
                case "cosmetic_cat.emote": v = CosmeticCategory.Emote; return true;
                case "cosmetic_cat.border": v = CosmeticCategory.Border; return true;
                case "cosmetic_cat.crest": v = CosmeticCategory.Crest; return true;
                case "cosmetic_cat.title": v = CosmeticCategory.Title; return true;
                default: v = default; return false;
            }
        }

        // 옛 이름 → 정본(CONTRACTS 2.1 대응표, spec 14 1.2·3.1). 별칭이 아니면 그대로 돌려준다.
        public static string ResolveAlias(string id)
        {
            switch (id)
            {
                case "block.great_fireball": return "block.fireball";
                case "block.giant_fireball": return "block.fireball";
                case "block.split_2": return "block.split_two";
                case "block.split_3": return "block.split_three";
                case "block.pierce_2": return "block.pierce";
                case "block.overheat_x3": return "block.triple_heat";
                case "block.erosion": return "block.corrosion";
                case "art.mana_release": return "art.burst";
                case "art.transcribe": return "art.copy_circle";
                case "art.thunder_dash": return "art.bolt_dash";
                case "passive.boil_over": return "passive.overflow";
                case "passive.shadow_pact": return "passive.shadow_contract";
                case "boss.boar_kiln": return "boss.kiln_boar";
                case "boss.master_phantom": return "boss.mirror_phantom";
                case "boss.mirror_master": return "boss.mirror_phantom";
                case "boss.star_serpent": return "boss.night_serpent";
                case "element.frost": return "element.ice";
                case "element.shadow": return "element.dark";
                case "stone_low": return "mat.stone_low";
                case "stone_mid": return "mat.stone_mid";
                case "stone_high": return "mat.stone_high";
                case "charm_guard": return "charm.safeguard";
                case "frost": return "ice";
                case "shadow": return "dark";
                case "master": return "npc.master";
                default: return id;
            }
        }
    }
}
