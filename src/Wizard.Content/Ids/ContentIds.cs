using System;
using Wizard.Core;

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

        // spec 14 3.9 꼬리표. 꼬리표는 비트 집합이라 id는 비트 하나에만 있다(둘 이상 켜진 값은 id가 없다)
        public static string ToId(EnemyTag v)
        {
            switch (v)
            {
                case EnemyTag.Elite: return "tag.elite";
                case EnemyTag.Heavy: return "tag.heavy";
                case EnemyTag.Boss: return "tag.boss";
                case EnemyTag.Flying: return "tag.flying";
                case EnemyTag.Burrowed: return "tag.burrowed";
                case EnemyTag.Rooted: return "tag.rooted";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out EnemyTag v)
        {
            switch (ResolveAlias(id))
            {
                case "tag.elite": v = EnemyTag.Elite; return true;
                case "tag.heavy": v = EnemyTag.Heavy; return true;
                case "tag.boss": v = EnemyTag.Boss; return true;
                case "tag.flying": v = EnemyTag.Flying; return true;
                case "tag.burrowed": v = EnemyTag.Burrowed; return true;
                case "tag.rooted": v = EnemyTag.Rooted; return true;
                default: v = default; return false;
            }
        }

        // spec 14 3.11. 정본 id는 currency.*. DB 열(currency_balance.currency)의 키만 쓴 꼴(gold …)은 별칭으로 받는다(spec 14 1.3).
        // 원정 지갑(currency.expedition_purse)은 표시 전용이라 재화가 아니다(spec 14 9절 U-23)
        public static string ToId(CurrencyCode v)
        {
            switch (v)
            {
                case CurrencyCode.Gold: return "currency.gold";
                case CurrencyCode.SeasonCoin: return "currency.season_coin";
                case CurrencyCode.Starlight: return "currency.starlight";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out CurrencyCode v)
        {
            switch (ResolveAlias(id))
            {
                case "currency.gold": v = CurrencyCode.Gold; return true;
                case "currency.season_coin": v = CurrencyCode.SeasonCoin; return true;
                case "currency.starlight": v = CurrencyCode.Starlight; return true;
                default: v = default; return false;
            }
        }

        // spec 14 3.14 치장 칸
        public static string ToId(CosmeticSlot v)
        {
            switch (v)
            {
                case CosmeticSlot.Head: return "cosmetic_slot.head";
                case CosmeticSlot.Cloak: return "cosmetic_slot.cloak";
                case CosmeticSlot.Footprints: return "cosmetic_slot.footprints";
                case CosmeticSlot.Seat: return "cosmetic_slot.seat";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no content id");
            }
        }

        public static bool TryParse(string id, out CosmeticSlot v)
        {
            switch (ResolveAlias(id))
            {
                case "cosmetic_slot.head": v = CosmeticSlot.Head; return true;
                case "cosmetic_slot.cloak": v = CosmeticSlot.Cloak; return true;
                case "cosmetic_slot.footprints": v = CosmeticSlot.Footprints; return true;
                case "cosmetic_slot.seat": v = CosmeticSlot.Seat; return true;
                default: v = default; return false;
            }
        }

        // spec 14 3.2. 전용 지팡이의 종류 id wand.N 은 그 캐릭터 번호를 쓴다(WC 0). 지팡이 개체는 콘텐츠 id가 아니라 DB 행이다
        public static string ToWandId(CharacterId v)
        {
            switch (v)
            {
                case CharacterId.Char1: return "wand.1";
                case CharacterId.Char2: return "wand.2";
                case CharacterId.Char3: return "wand.3";
                case CharacterId.Char4: return "wand.4";
                case CharacterId.Char5: return "wand.5";
                default: throw new ArgumentOutOfRangeException(nameof(v), v, "no wand id");
            }
        }

        public static bool TryParseWand(string id, out CharacterId v)
        {
            switch (ResolveAlias(id))
            {
                case "wand.1": v = CharacterId.Char1; return true;
                case "wand.2": v = CharacterId.Char2; return true;
                case "wand.3": v = CharacterId.Char3; return true;
                case "wand.4": v = CharacterId.Char4; return true;
                case "wand.5": v = CharacterId.Char5; return true;
                default: v = default; return false;
            }
        }

        // spec 14 3.7. 엘리트는 종이 아니다(EnemyState.IsElite). 표시 id는 elite.<바탕 키>, 코드는 EliteCodeBase + 바탕 종 번호
        // (CONTRACTS 2.1 EnemyTypeId 주석). 졸개(갓버섯 새싹)에는 엘리트가 없다
        public const ushort EliteCodeBase = 200;

        public static string ToEliteId(EnemyTypeId baseType)
        {
            switch (baseType)
            {
                case EnemyTypeId.SlimeMoss: return "elite.slime_moss";
                case EnemyTypeId.WolfBramble: return "elite.wolf_bramble";
                case EnemyTypeId.GolemMenhir: return "elite.golem_menhir";
                case EnemyTypeId.ShamanCap: return "elite.shaman_cap";
                case EnemyTypeId.BugLantern: return "elite.bug_lantern";
                case EnemyTypeId.HogNeedle: return "elite.hog_needle";
                case EnemyTypeId.WeaselPot: return "elite.weasel_pot";
                case EnemyTypeId.BatDusk: return "elite.bat_dusk";
                case EnemyTypeId.WispStray: return "elite.wisp_stray";
                case EnemyTypeId.VineThorn: return "elite.vine_thorn";
                case EnemyTypeId.BoarTusk: return "elite.boar_tusk";
                case EnemyTypeId.ToadDrum: return "elite.toad_drum";
                default: throw new ArgumentOutOfRangeException(nameof(baseType), baseType, "no elite id");
            }
        }

        public static bool TryParseElite(string id, out EnemyTypeId baseType)
        {
            switch (ResolveAlias(id))
            {
                case "elite.slime_moss": baseType = EnemyTypeId.SlimeMoss; return true;
                case "elite.wolf_bramble": baseType = EnemyTypeId.WolfBramble; return true;
                case "elite.golem_menhir": baseType = EnemyTypeId.GolemMenhir; return true;
                case "elite.shaman_cap": baseType = EnemyTypeId.ShamanCap; return true;
                case "elite.bug_lantern": baseType = EnemyTypeId.BugLantern; return true;
                case "elite.hog_needle": baseType = EnemyTypeId.HogNeedle; return true;
                case "elite.weasel_pot": baseType = EnemyTypeId.WeaselPot; return true;
                case "elite.bat_dusk": baseType = EnemyTypeId.BatDusk; return true;
                case "elite.wisp_stray": baseType = EnemyTypeId.WispStray; return true;
                case "elite.vine_thorn": baseType = EnemyTypeId.VineThorn; return true;
                case "elite.boar_tusk": baseType = EnemyTypeId.BoarTusk; return true;
                case "elite.toad_drum": baseType = EnemyTypeId.ToadDrum; return true;
                default: baseType = default; return false;
            }
        }

        public static ushort ToEliteCode(EnemyTypeId baseType)
        {
            ToEliteId(baseType);   // 엘리트가 없는 종이면 여기서 던진다
            return (ushort)(EliteCodeBase + (ushort)baseType);
        }

        // spec 14 3.12. 칭호 하나하나는 데이터의 문자열 id(title.<분류>.<키>)이고 분류만 열거형이다(CONTRACTS 2.1 TitleKind).
        // 강화 단계 칭호는 title.stage.N.bK (N = 캐릭터 1~5, K = 묶음 0~4)만 받는다. 그 밖의 분류는 키를 데이터가 더한다(spec 14 9절 U-02, U-03)
        public const int StageTitleBandCount = 5;

        private const string StageTitlePrefix = "title.stage.";

        public static bool TryGetTitleKind(string titleId, out TitleKind kind)
        {
            kind = default;
            if (!ContentId.TryParse(titleId, out _))
            {
                return false;
            }
            if (titleId.StartsWith(StageTitlePrefix, StringComparison.Ordinal))
            {
                if (!IsStageTitleId(titleId))
                {
                    return false;
                }
                kind = TitleKind.Stage;
                return true;
            }
            if (HasKeyAfter(titleId, "title.record.")) { kind = TitleKind.Record; return true; }
            if (HasKeyAfter(titleId, "title.revenge.")) { kind = TitleKind.Revenge; return true; }
            if (HasKeyAfter(titleId, "title.season.")) { kind = TitleKind.Season; return true; }
            if (HasKeyAfter(titleId, "title.feat.")) { kind = TitleKind.Feat; return true; }
            return false;
        }

        public static string ToStageTitleId(CharacterId character, int band)
        {
            if (band < 0 || band >= StageTitleBandCount)
            {
                throw new ArgumentOutOfRangeException(nameof(band), band, "stage title band is 0..4");
            }
            string wand = ToWandId(character);   // Baseline 이면 여기서 던진다
            char n = wand[wand.Length - 1];
            char k = (char)('0' + band);
            return StageTitlePrefix + n + ".b" + k;
        }

        // title.stage.N.bK 의 25개(캐릭터 5 × 묶음 5) 가운데 하나인가
        private static bool IsStageTitleId(string id)
        {
            for (byte c = (byte)CharacterId.Char1; c <= (byte)CharacterId.Char5; c++)
            {
                for (int band = 0; band < StageTitleBandCount; band++)
                {
                    if (string.Equals(id, ToStageTitleId((CharacterId)c, band), StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool HasKeyAfter(string id, string prefix)
        {
            return id.Length > prefix.Length && id.StartsWith(prefix, StringComparison.Ordinal);
        }

        // 옛 이름 → 정본. 별칭이 아니면 그대로 돌려준다.
        // 출처: CONTRACTS 2.1 대응표(다른 명세가 실제로 적은 꼴 — 네임스페이스가 붙은 꼴과 키만 쓴 꼴 둘 다),
        // spec 14 1.2(WC 0의 네임스페이스 없는 장소·인물), 3.1(ElementAlias), 1.3(재화 키).
        // spec 11의 fac_* 는 별칭으로도 받지 않는다(CONTRACTS 2.1). 데이터에는 정본만 적는다.
        public static string ResolveAlias(string id)
        {
            switch (id)
            {
                // 블록 (spec 02·07·08)
                case "great_fireball": return "block.fireball";
                case "giant_fireball": return "block.fireball";
                case "split_2": return "block.split_two";
                case "split_3": return "block.split_three";
                case "pierce_2": return "block.pierce";
                case "overheat_x3": return "block.triple_heat";
                case "erosion": return "block.corrosion";
                case "block.great_fireball": return "block.fireball";
                case "block.giant_fireball": return "block.fireball";
                case "block.split_2": return "block.split_two";
                case "block.split_3": return "block.split_three";
                case "block.pierce_2": return "block.pierce";
                case "block.overheat_x3": return "block.triple_heat";
                case "block.erosion": return "block.corrosion";
                // 비기 (spec 03·04·10)
                case "art.mana_release": return "art.burst";
                case "art.recoil_shot": return "art.recoil";
                case "art.fan_spread": return "art.fan";
                case "art.twin_wing": return "art.wings";
                case "art.afterimage_step": return "art.afterimage";
                case "art.piercing_lance": return "art.spear";
                case "art.vent_burst": return "art.vent";
                case "art.eightfold_spread": return "art.octastar";
                case "art.thunder_lance": return "art.thunder_spear";
                case "art.full_bloom": return "art.bloom";
                case "art.transcribe": return "art.copy_circle";
                case "art.thunder_dash": return "art.bolt_dash";
                // 패시브 (spec 04)
                case "passive.boil_over": return "passive.overflow";
                case "passive.shadow_pact": return "passive.shadow_contract";
                // 보스 (spec 06·07)
                case "boss.boar_kiln": return "boss.kiln_boar";
                case "boss.master_phantom": return "boss.mirror_phantom";
                case "boss.mirror_master": return "boss.mirror_phantom";
                case "boss.star_serpent": return "boss.night_serpent";
                // 재료 (spec 06·07·08)
                case "stone_low": return "mat.stone_low";
                case "stone_mid": return "mat.stone_mid";
                case "stone_high": return "mat.stone_high";
                case "charm_guard": return "charm.safeguard";
                case "mat.charm_guard": return "charm.safeguard";
                // 속성 (spec 14 3.1 ElementAlias, 8절 C-01)
                case "element.frost": return "element.ice";
                case "element.shadow": return "element.dark";
                case "frost": return "ice";
                case "shadow": return "dark";
                // 틀과 변덕 (spec 07 4절의 frame.hunt, modifier.<열거형 이름>)
                case "frame.hunt": return "frame.hunting_party";
                case "modifier.rush": return "whim.crowding";
                case "modifier.thick_hide": return "whim.thick_hide";
                case "modifier.big_herd": return "whim.big_pack";
                case "modifier.chief_march": return "whim.chief_parade";
                case "modifier.last_breath": return "whim.last_breath";
                case "modifier.slow_barrage": return "whim.slow_barrage";
                case "modifier.long_night": return "whim.long_night";
                case "modifier.narrow_field": return "whim.narrow_field";
                // 장소·인물 (spec 14 1.2, 2.3, 8절 C-20)
                case "master": return "npc.master";
                case "field": return "place.field";
                case "plaza": return "place.plaza";
                case "arena": return "place.arena";
                // 재화 키 (spec 14 1.3)
                case "gold": return "currency.gold";
                case "season_coin": return "currency.season_coin";
                case "starlight": return "currency.starlight";
                default: return id;
            }
        }
    }
}
