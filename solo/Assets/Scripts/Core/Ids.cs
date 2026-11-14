namespace Wizard.Solo
{
    /// <summary>
    /// 정본 콘텐츠 id 상수(spec 14 3절, data/sim/*.json, names.ko.json과 같은 글자).
    /// 코드에서 id 문자열을 직접 치지 말고 여기 상수를 쓴다. 표시 이름은 <see cref="Data.INameTable"/>로 찾는다(열쇠 = id).
    /// </summary>
    public static class Ids
    {
        public static class Char
        {
            public const string C1 = "char.1", C2 = "char.2", C3 = "char.3", C4 = "char.4", C5 = "char.5";
            public const int Count = 5;
            /// <summary>1~5 → "char.N".</summary>
            public static string Of(int number) => "char." + number;
            /// <summary>"char.N" → N(1~5). 꼴이 다르면 0.</summary>
            public static int NumberOf(string charId)
            {
                if (charId == null || charId.Length != 6 || !charId.StartsWith("char.")) return 0;
                int n = charId[5] - '0';
                return n >= 1 && n <= Count ? n : 0;
            }
            /// <summary>캐릭터 N의 지팡이 종류 id "wand.N"(이름 표·그림 대응용. 지팡이 개체 번호와 다르다).</summary>
            public static string WandKindOf(int number) => "wand." + number;
        }

        public static class Element
        {
            public const string Neutral = "element.neutral", Fire = "element.fire", Lightning = "element.lightning",
                Arcane = "element.arcane", Ice = "element.ice", Dark = "element.dark";
        }

        public static class Block
        {
            public const string MagicBolt = "block.magic_bolt", Fireball = "block.fireball", ChainLightning = "block.chain_lightning",
                Boomerang = "block.boomerang", IceSpike = "block.ice_spike", ShadowOrb = "block.shadow_orb",
                SplitTwo = "block.split_two", SplitThree = "block.split_three", OnHitTrigger = "block.on_hit_trigger",
                WallBounce = "block.wall_bounce", DoubleSize = "block.double_size", Pierce = "block.pierce", Frost = "block.frost",
                Homing = "block.homing", TripleHeat = "block.triple_heat", Corrosion = "block.corrosion";
        }

        /// <summary>블록 flags / effect 글자(blocks.json).</summary>
        public static class BlockFlag
        {
            public const string Explodes = "explodes", Chains = "chains", IgnoresWolfDodge = "ignores_wolf_dodge",
                Returns = "returns", PassThrough = "pass_through";
        }

        public static class BlockEffect
        {
            public const string None = "none", Bounce = "bounce", Size = "size", Pierce = "pierce", Frost = "frost",
                Homing = "homing", TripleHeat = "triple_heat", Corrosion = "corrosion";
        }

        /// <summary>비기 15종. 번호(순서)는 04 요약서 0.1절 표 그대로이고 바꾸지 않는다.</summary>
        public static class Art
        {
            public const string Burst = "art.burst", Recoil = "art.recoil", Fan = "art.fan", Wings = "art.wings",
                Afterimage = "art.afterimage", Spear = "art.spear", Vent = "art.vent", Octastar = "art.octastar",
                ThunderSpear = "art.thunder_spear", Bloom = "art.bloom",
                Kiln = "art.kiln", BoltDash = "art.bolt_dash", CopyCircle = "art.copy_circle",
                IceScreen = "art.ice_screen", ShadowHand = "art.shadow_hand";
            public static readonly string[] All =
            {
                Burst, Recoil, Fan, Wings, Afterimage, Spear, Vent, Octastar, ThunderSpear, Bloom,
                Kiln, BoltDash, CopyCircle, IceScreen, ShadowHand
            };
        }

        public static class Passive
        {
            public const string Overflow = "passive.overflow", LightningRod = "passive.lightning_rod", Underline = "passive.underline",
                ThinIce = "passive.thin_ice", ShadowContract = "passive.shadow_contract";
        }

        public static class WandTrait
        {
            public const string HeatAt80 = "wand_trait.heat_at_80", ThreeMutations = "wand_trait.three_mutations",
                DepthDecay = "wand_trait.depth_decay", KeepCrystal = "wand_trait.keep_crystal", OnCredit = "wand_trait.on_credit";
        }

        public static class Mutation
        {
            public const string TwistedBarrel = "mutation.twisted_barrel", UnstableCore = "mutation.unstable_core",
                HypersensitiveCircuit = "mutation.hypersensitive_circuit", Gluttony = "mutation.gluttony",
                MirrorImage = "mutation.mirror_image", GlassWand = "mutation.glass_wand";
            public static readonly string[] All = { TwistedBarrel, UnstableCore, HypersensitiveCircuit, Gluttony, MirrorImage, GlassWand };
        }

        public static class Tag
        {
            public const string Elite = "tag.elite", Heavy = "tag.heavy", Boss = "tag.boss", Flying = "tag.flying",
                Burrowed = "tag.burrowed", Rooted = "tag.rooted";
        }

        public static class Enemy
        {
            public const string SlimeMoss = "enemy.slime_moss", WolfBramble = "enemy.wolf_bramble", GolemMenhir = "enemy.golem_menhir",
                ShamanCap = "enemy.shaman_cap", BugLantern = "enemy.bug_lantern", HogNeedle = "enemy.hog_needle",
                WeaselPot = "enemy.weasel_pot", BatDusk = "enemy.bat_dusk", WispStray = "enemy.wisp_stray",
                VineThorn = "enemy.vine_thorn", BoarTusk = "enemy.boar_tusk", ToadDrum = "enemy.toad_drum",
                SporeSeedling = "enemy.spore_seedling";
            /// <summary>허수아비(연습·튜토리얼). data/sim에 없고 solo/practice.json이 정의한다.</summary>
            public const string Dummy = "enemy.dummy";
            /// <summary>"enemy.slime_moss" → "elite.slime_moss".</summary>
            public static string EliteOf(string enemyId) => "elite." + enemyId.Substring("enemy.".Length);
        }

        public static class Boss
        {
            public const string KilnBoar = "boss.kiln_boar", MirrorPhantom = "boss.mirror_phantom", SporeQueen = "boss.spore_queen",
                StormOwl = "boss.storm_owl", FrostGoat = "boss.frost_goat", NightSerpent = "boss.night_serpent";
        }

        public static class Tod
        {
            public const string Day = "tod.day", Dusk = "tod.dusk", Night = "tod.night", Boss = "tod.boss";
        }

        public static class Whim
        {
            public const string Crowding = "whim.crowding", ThickHide = "whim.thick_hide", BigPack = "whim.big_pack",
                ChiefParade = "whim.chief_parade", LastBreath = "whim.last_breath", SlowBarrage = "whim.slow_barrage",
                LongNight = "whim.long_night", NarrowField = "whim.narrow_field";
        }

        public static class Frame
        {
            public const string Swarm = "frame.swarm", Battery = "frame.battery", Ambush = "frame.ambush",
                Mixed = "frame.mixed", HuntingParty = "frame.hunting_party";
        }

        public static class Event
        {
            public const string Shower = "event.shower", Fog = "event.fog", StarCrystal = "event.star_crystal",
                MasterAdvice = "event.master_advice", DiscardedItem = "event.discarded_item";
        }

        public static class Item
        {
            public const string Gold = "currency.gold", Purse = "currency.expedition_purse";
            public const string StoneLow = "mat.stone_low", StoneMid = "mat.stone_mid", StoneHigh = "mat.stone_high";
            public const string Charm = "charm.safeguard";
            public const string HealthShard = "item.health_shard", CrowBundle = "item.crow_bundle", MaterialBundle = "item.material_bundle",
                WandBox = "item.wand_box", LibraryExpansion = "item.library_expansion";
        }

        public static class Place
        {
            public const string Field = "place.field";
        }

        public static class Title
        {
            public const string FirstNight = "title.record.first_night", ThreeNights = "title.record.three_nights",
                TenDays = "title.record.ten_days", Bloom100 = "title.feat.bloom_100", FlawlessNight = "title.feat.flawless_night",
                MirrorBreaker = "title.revenge.mirror_breaker";
        }

        public static class Cosmetic
        {
            public const string CrackedMirrorShard = "cosmetic.headwear.cracked_mirror_shard";
        }

        public static class Rarity
        {
            public const string Common = "common", Rare = "rare", Hero = "hero";
        }
    }
}
