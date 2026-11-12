using System;
using System.Collections.Generic;
using Wizard.Content.Tests.Pack;
using Wizard.Core;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests
{
    // data/sim/blocks.json → 팩 절 2 → ContentDb.Block 의 왕복(블록 16종). 기대값은 코드가 아니라 명세의 표에서 온다:
    //   spec 02 4.1(종류·등급·속성·열·피해·간격·속도/사거리), 4.2(틱·Raw·피격 반지름), 2.2·2.4(상수 Raw·틱), spec 01 2.4·2.5(열 HU),
    //   spec 14 3.3(id·코드), DECISIONS 2.5(얼음 송곳·그림자 구체·서리·잠식 네 줄).
    //   표식·효과·동작 값(ProjectileFlags, ModifierEffect, BlockParams. DECISIONS-TECH T-53)은 spec 02 4.1 의 메모, 4.2 의 "기타", 9.3(부채꼴),
    //   12절(감속·잠식), 18절의 [임시] U6~U12 에서 온다.
    public sealed class BlocksPackTests
    {
        private const long OneRaw = 4_294_967_296L;            // 2^32 (spec 14 0.3)
        private const int HuPerHeat = 120;                     // spec 01 2.4

        private static readonly Lazy<byte[]> RepoPack = new Lazy<byte[]>(() => BuildPack(TestRepo.Root));

        private static byte[] BuildPack(string root)
        {
            var d = new Diagnostics();
            Pipeline.Output o = Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            Assert.False(d.HasErrors, string.Join("\n", d.Sorted()));
            return o.Pack!;
        }

        private static List<string> Problems(string root)
        {
            var d = new Diagnostics();
            Pipeline.Run(root, ModuleRegistry.Discover(), PackOptions.Default(PackOptions.DefaultDataVersion), d);
            return new List<string>(d.Sorted());
        }

        private static ContentDb RepoDb() => ContentDb.Build(ContentPack.Load(RepoPack.Value));

        // ── 명세 표(spec 02 4.1·4.2)를 옮긴 기대 행. 수는 모두 정수 단위로 바꾼 값이고, 바꾸는 계산은 주석에 있다 ──
        private sealed class Row
        {
            public BlockId Id;
            public BlockKind Kind;
            public Rarity Rarity;
            public Element Element;
            public int HeatHu;                 // 열 × 120
            public int HeatMulPermille;        // 분열 1600 / 2200, 그 밖 1000
            public int SplitCount;             // 분열 2 / 3, 그 밖 1
            public long DamageWhole;           // 피해(칸이 아닌 수 → Raw 는 × 2^32)
            public int IntervalTicks;
            public long SpeedRaw;              // 칸/틱 Raw. 즉발·비투사체는 0
            public bool Instant;
            public long RangeWhole;            // 사거리(칸)
            public long HitRadiusRaw;
            public int DamageBonusPermille;
            public ProjectileFlags Flags;
            public ModifierEffect Effect;
            public BlockParams Params = new BlockParams();   // 그 블록이 쓰지 않는 값은 0

            public Row With(ProjectileFlags flags, BlockParams? p = null)
            {
                Flags = flags;
                Params = p ?? Params;
                return this;
            }

            public Row With(ModifierEffect effect, BlockParams p)
            {
                Effect = effect;
                Params = p;
                return this;
            }

            public Row With(BlockParams p)
            {
                Params = p;
                return this;
            }
        }

        private static Fix64 Cells(long whole) => Fix64.FromRaw(whole * OneRaw);

        private static Row P(BlockId id, Rarity rarity, Element element, int heat, long damage, int ticks, long speedRaw, bool instant, long range, long hitRaw) =>
            new Row { Id = id, Kind = BlockKind.Projectile, Rarity = rarity, Element = element, HeatHu = heat * HuPerHeat, HeatMulPermille = 1000, SplitCount = 1,
                DamageWhole = damage, IntervalTicks = ticks, SpeedRaw = speedRaw, Instant = instant, RangeWhole = range, HitRadiusRaw = hitRaw };

        private static Row C(BlockId id, Rarity rarity, int mulPermille, int split) =>
            new Row { Id = id, Kind = BlockKind.Control, Rarity = rarity, Element = Element.None, HeatHu = 0, HeatMulPermille = mulPermille, SplitCount = split };

        private static Row M(BlockId id, Rarity rarity, Element element, int heat, int bonusPermille) =>
            new Row { Id = id, Kind = BlockKind.Modifier, Rarity = rarity, Element = element, HeatHu = heat * HuPerHeat, HeatMulPermille = 1000, SplitCount = 1, DamageBonusPermille = bonusPermille };

        // 속도 Raw 는 spec 02 4.2 표에 적힌 값, 피격 반지름 Raw 는 4.2(0.12 → 515,396,076, 0.18 → 773,094,113, 0.25 → 1,073,741,824)
        // 와 0.15 × 2^32 = 644,245,094.4 → 644,245,094 (부메랑 [임시]). 연쇄 번개의 광선 반폭은 0.12 [임시]
        // 메모의 값(4.1 메모, 4.2 "기타", 9.3, 12절, 18절):
        //   거대 화염구: 반경 2 폭발.  연쇄 번개: 3회 전이, 반경 3, 전이마다 ×0.7(= 700‰), 늑대 회피 무시.  부메랑: 왕복(쏜 사람 반경 0.5칸 = 2^31, 돌아오는 길 9칸. U12)
        //   얼음 송곳: −30% 1초(300‰, 60틱).  그림자 구체: 적마다 1회 지나감.  분열: 부채꼴 ±10도(1000 센티도), −15 / 0 / +15도(1500)
        //   벽 튕김: 2회(U6).  크기 2배: 반지름 ×2, 상한 ×4(U8), +20%.  관통: +2체(U7), +10%.  서리: −20% 1초.
        //   유도: 초당 180도 = 틱당 300 센티도, 앞 ±90도, 6칸(U9).  과열 3배: 임계 배율 3.  잠식: +15% 3초(150‰, 180틱)
        private static readonly Row[] Table =
        {
            P(BlockId.MagicBolt,       Rarity.Common, Element.Arcane,    4, 10,  9, 1_002_159_036L, false, 10, 515_396_076L),
            P(BlockId.Fireball,        Rarity.Rare,   Element.Fire,     18, 40, 36,   644_245_094L, false,  8, 773_094_113L)
                .With(ProjectileFlags.Explodes, new BlockParams { ExplosionRadius = Cells(2) }),
            P(BlockId.ChainLightning,  Rarity.Hero,   Element.Lightning, 12, 18, 27,             0L, true,   7, 515_396_076L)
                .With(ProjectileFlags.Chains | ProjectileFlags.IgnoresWolfDodge, new BlockParams { ChainHops = 3, ChainRadius = Cells(3), ChainHopMulPermille = 700 }),
            P(BlockId.Boomerang,       Rarity.Rare,   Element.Arcane,   10, 15, 21,   715_827_883L, false,  6, 644_245_094L)
                .With(ProjectileFlags.Returns, new BlockParams { ReturnArriveRadius = Fix64.FromRaw(2_147_483_648L), ReturnMaxDistance = Cells(9) }),
            P(BlockId.IceSpike,        Rarity.Rare,   Element.Ice,       8, 14, 18,   858_993_459L, false,  9, 515_396_076L)
                .With(new BlockParams { SlowPermille = 300, SlowTicks = 60 }),
            P(BlockId.ShadowOrb,       Rarity.Rare,   Element.Dark,     14, 28, 30,   429_496_730L, false,  8, 1_073_741_824L)
                .With(ProjectileFlags.PassThrough),
            C(BlockId.SplitTwo,        Rarity.Common, 1600, 2).With(new BlockParams { FanHalfAngleCenti = 1000 }),
            C(BlockId.SplitThree,      Rarity.Rare,   2200, 3).With(new BlockParams { FanHalfAngleCenti = 1500 }),
            C(BlockId.OnHitTrigger,    Rarity.Rare,   1000, 1),
            M(BlockId.WallBounce,      Rarity.Common, Element.None,      2, 0).With(ModifierEffect.Bounce, new BlockParams { BounceCount = 2 }),
            M(BlockId.DoubleSize,      Rarity.Common, Element.Fire,      3, 200).With(ModifierEffect.Size, new BlockParams { RadiusMulPermille = 2000, RadiusMulCapPermille = 4000 }),
            M(BlockId.Pierce,          Rarity.Common, Element.Arcane,    3, 100).With(ModifierEffect.Pierce, new BlockParams { PierceCount = 2 }),
            M(BlockId.Frost,           Rarity.Common, Element.Ice,       3, 0).With(ModifierEffect.Frost, new BlockParams { SlowPermille = 200, SlowTicks = 60 }),
            M(BlockId.Homing,          Rarity.Hero,   Element.Lightning, 5, 0)
                .With(ModifierEffect.Homing, new BlockParams { HomingTurnCentiPerTick = 300, HomingConeHalfCenti = 9000, HomingRange = Cells(6) }),
            M(BlockId.TripleHeat,      Rarity.Hero,   Element.Fire,      2, 0).With(ModifierEffect.TripleHeat, new BlockParams { CritMultiplier = 3 }),
            M(BlockId.Corrosion,       Rarity.Hero,   Element.Dark,      5, 0)
                .With(ModifierEffect.Corrosion, new BlockParams { DamageTakenBonusPermille = 150, DamageTakenBonusTicks = 180 }),
        };

        private static Row RowOf(BlockId id)
        {
            foreach (Row r in Table)
            {
                if (r.Id == id)
                {
                    return r;
                }
            }
            throw new InvalidOperationException("no row for " + id);
        }

        private static void AssertRow(BlockDef b, Row r)
        {
            string at = r.Id.ToString();
            Assert.True(r.Id == b.Id, at + " Id");
            Assert.True(r.Kind == b.Kind, at + " Kind");
            Assert.True(r.Rarity == b.Rarity, at + " Rarity");
            Assert.True(r.Element == b.Element, at + " Element");
            Assert.True(r.HeatHu == b.HeatHu, at + " HeatHu");
            Assert.True(r.HeatMulPermille == b.HeatMulPermille, at + " HeatMulPermille");
            Assert.True(r.SplitCount == b.SplitCount, at + " SplitCount");
            Assert.True(r.DamageWhole * OneRaw == b.BaseDamage.Raw, at + " BaseDamage");
            Assert.True(r.IntervalTicks == b.IntervalTicks, at + " IntervalTicks");
            Assert.True(r.SpeedRaw == b.SpeedPerTick.Raw, at + " SpeedPerTick");
            Assert.True(r.Instant == b.IsInstant, at + " IsInstant");
            Assert.True(r.RangeWhole * OneRaw == b.Range.Raw, at + " Range");
            Assert.True(r.HitRadiusRaw == b.HitRadius.Raw, at + " HitRadius");
            Assert.True(r.DamageBonusPermille == b.DamageBonusPermille, at + " DamageBonusPermille");
            Assert.True(r.Flags == b.Flags, at + " Flags");
            Assert.True(r.Effect == b.Effect, at + " Effect");
            BlockParams want = r.Params, got = b.Params;
            Assert.True(want.ExplosionRadius.Raw == got.ExplosionRadius.Raw, at + " ExplosionRadius");
            Assert.True(want.ChainHops == got.ChainHops, at + " ChainHops");
            Assert.True(want.ChainRadius.Raw == got.ChainRadius.Raw, at + " ChainRadius");
            Assert.True(want.ChainHopMulPermille == got.ChainHopMulPermille, at + " ChainHopMulPermille");
            Assert.True(want.ReturnArriveRadius.Raw == got.ReturnArriveRadius.Raw, at + " ReturnArriveRadius");
            Assert.True(want.ReturnMaxDistance.Raw == got.ReturnMaxDistance.Raw, at + " ReturnMaxDistance");
            Assert.True(want.SlowPermille == got.SlowPermille, at + " SlowPermille");
            Assert.True(want.SlowTicks == got.SlowTicks, at + " SlowTicks");
            Assert.True(want.DamageTakenBonusPermille == got.DamageTakenBonusPermille, at + " DamageTakenBonusPermille");
            Assert.True(want.DamageTakenBonusTicks == got.DamageTakenBonusTicks, at + " DamageTakenBonusTicks");
            Assert.True(want.FanHalfAngleCenti == got.FanHalfAngleCenti, at + " FanHalfAngleCenti");
            Assert.True(want.BounceCount == got.BounceCount, at + " BounceCount");
            Assert.True(want.PierceCount == got.PierceCount, at + " PierceCount");
            Assert.True(want.RadiusMulPermille == got.RadiusMulPermille, at + " RadiusMulPermille");
            Assert.True(want.RadiusMulCapPermille == got.RadiusMulCapPermille, at + " RadiusMulCapPermille");
            Assert.True(want.HomingTurnCentiPerTick == got.HomingTurnCentiPerTick, at + " HomingTurnCentiPerTick");
            Assert.True(want.HomingConeHalfCenti == got.HomingConeHalfCenti, at + " HomingConeHalfCenti");
            Assert.True(want.HomingRange.Raw == got.HomingRange.Raw, at + " HomingRange");
            Assert.True(want.CritMultiplier == got.CritMultiplier, at + " CritMultiplier");
        }

        // BlockParams 의 필드가 늘면 AssertRow 와 절 바이트(읽는 쪽·쓰는 쪽)도 같이 늘어야 한다
        [Fact]
        public void BlockParams_HasExactlyTheFieldsThisFileCompares()
        {
            Assert.Equal(19, typeof(BlockParams).GetFields().Length);
        }

        [Fact]
        public void MagicBolt_RoundTrip_EveryField_IsTheSpecInteger()
        {
            BlockDef b = RepoDb().Block(BlockId.MagicBolt);
            Assert.Equal(480, b.HeatHu);                          // 열 4 (spec 01 2.4)
            Assert.Equal(1_002_159_036L, b.SpeedPerTick.Raw);     // 14 칸/초 (spec 02 4.2)
            Assert.Equal(515_396_076L, b.HitRadius.Raw);          // 0.12 × 2^32 = 515,396,075.52 → 반올림 (spec 02 4.2 [임시])
            AssertRow(b, RowOf(BlockId.MagicBolt));
            Assert.Equal(ProjectileFlags.None, b.Flags);
            Assert.Equal(ModifierEffect.None, b.Effect);
        }

        [Fact]
        public void Fireball_RoundTrip_EveryField_IsTheSpecInteger()
        {
            BlockDef b = RepoDb().Block(BlockId.Fireball);
            Assert.Equal(2160, b.HeatHu);                         // 열 18 (spec 01 2.5)
            Assert.Equal(644_245_094L, b.SpeedPerTick.Raw);       // 9 칸/초 → 0.15 (spec 02 4.2)
            Assert.Equal(773_094_113L, b.HitRadius.Raw);          // 0.18 × 2^32 = 773,094,113.28 → 반올림 (spec 02 4.2 [임시])
            Assert.Equal(ProjectileFlags.Explodes, b.Flags);
            Assert.Equal(8_589_934_592L, b.Params.ExplosionRadius.Raw);   // 반경 2칸 = 2 × 2^32 (spec 02 4.1)
            AssertRow(b, RowOf(BlockId.Fireball));
        }

        [Fact]
        public void AllSixteenBlocks_RoundTrip_EveryField_MatchesSpecTable()
        {
            ContentDb db = RepoDb();
            Assert.Equal(16, Table.Length);
            foreach (Row r in Table)
            {
                AssertRow(db.Block(r.Id), r);
            }
        }

        [Fact]
        public void BlocksNotInPack_Throw()
        {
            ContentDb db = RepoDb();
            Assert.Throws<ContentException>(() => db.Block(BlockId.None));
            Assert.Throws<ContentException>(() => db.Block((BlockId)17));
        }

        [Fact]
        public void SameData_SameSectionBytes_InIdOrder()
        {
            byte[] s = Section(RepoPack.Value);
            Assert.Equal(s, Section(BuildPack(TestRepo.Root)));
            // 원본이 어떤 순서든 절은 BlockId 오름차순이다(u16 개수 뒤 항목마다 첫 바이트가 id, 항목은 156바이트)
            Assert.Equal(16, s[0] | (s[1] << 8));
            for (int i = 0; i < 16; i++)
            {
                Assert.Equal((byte)(i + 1), s[2 + i * EntryBytes]);
            }
        }

        [Fact]
        public void ReversedSourceOrder_GivesTheSameBytes()
        {
            using TempDataRepo repo = Repo("[" + FireballJson + ", " + MagicBoltJson + "]");
            using TempDataRepo forward = Repo("[" + MagicBoltJson + ", " + FireballJson + "]");
            Assert.Equal(Section(BuildPack(forward.Root)), Section(BuildPack(repo.Root)));
        }

        [Fact]
        public void Problems_UnknownId_Duplicate_InstantWithSpeed()
        {
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("block.magic_bolt", "block.nope") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/id") && p.Contains("block.nope"));
            }
            using (TempDataRepo repo = Repo("[" + MagicBoltJson + ", " + MagicBoltJson + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/1/id") && p.Contains("두 번"));
            }
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"instant\": false", "\"instant\": true") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/speed"));
            }
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"kind\": \"projectile\"", "\"kind\": \"beam\"") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/kind") && p.Contains("스키마"));
            }
        }

        [Fact]
        public void Problems_KindRules_ProjectileNeedsValues_ControlAndModifierHaveNone()
        {
            // 투사체인데 피해 0
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"base_damage\": 10", "\"base_damage\": 0") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/kind") && p.Contains("투사체"));
            }
            // 즉발이 아닌데 속도 0
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"speed\": 14", "\"speed\": 0") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/speed"));
            }
            // 변형인데 피해가 있다
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"kind\": \"projectile\"", "\"kind\": \"modifier\"") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/kind") && p.Contains("제어·변형"));
            }
        }

        [Fact]
        public void BrokenSectionBytes_ThrowContentException()
        {
            byte[] good = Section(RepoPack.Value);
            Assert.Throws<ContentException>(() => Load(good.AsSpan(0, good.Length - 1).ToArray()));
            var longer = new byte[good.Length + 1];
            good.CopyTo(longer, 0);
            Assert.Throws<ContentException>(() => Load(longer));
            int entry = (good.Length - 2) / 16;
            // 첫 항목: u8 id(2) kind(3) rarity(4) element(5), i32×3(6..17), i64(18..25), i32(26..29), i64(30..37), u8 is_instant(38),
            //          i64(39..46), i64(47..54), i32(55..58), u16 flags(59..60), u8 effect(61),
            //          BlockParams: i64 explosion_radius(62..69), i32 chain_hops(70..73), … 끝은 i32 crit_multiplier(154..157)
            Assert.Equal(EntryBytes, entry);
            byte[] badKind = (byte[])good.Clone();
            badKind[3] = 0;
            Assert.Throws<ContentException>(() => Load(badKind));
            byte[] badInstant = (byte[])good.Clone();
            badInstant[38] = 2;
            Assert.Throws<ContentException>(() => Load(badInstant));
            byte[] badFlags = (byte[])good.Clone();
            badFlags[59] = 0x80;
            badFlags[60] = 0x80;
            Assert.Throws<ContentException>(() => Load(badFlags));
            byte[] badEffect = (byte[])good.Clone();
            badEffect[61] = 8;                                    // ModifierEffect 는 0~7
            Assert.Throws<ContentException>(() => Load(badEffect));
            byte[] negativeParam = (byte[])good.Clone();
            negativeParam[73] = 0x80;                             // chain_hops 의 맨 위 바이트 → 음수
            Assert.Throws<ContentException>(() => Load(negativeParam));
            byte[] negativeRadius = (byte[])good.Clone();
            negativeRadius[69] = 0x80;                            // explosion_radius_raw 의 맨 위 바이트 → 음수
            Assert.Throws<ContentException>(() => Load(negativeRadius));
            byte[] noneId = (byte[])good.Clone();
            noneId[2] = 0;
            Assert.Throws<ContentException>(() => Load(noneId));
            // 순서 위반: 두 항목을 바꾼다
            var swapped = (byte[])good.Clone();
            Array.Copy(good, 2 + entry, swapped, 2, entry);
            Array.Copy(good, 2, swapped, 2 + entry, entry);
            Assert.Throws<ContentException>(() => Load(swapped));
            Load(good);
        }

        // 60(BlockParams 앞) + i64 × 5 + i32 × 14 = 60 + 40 + 56
        private const int EntryBytes = 156;

        // ── 표식·효과·동작 값의 짝(변환기의 교차 검사, T-53) ──

        [Fact]
        public void Problems_FlagsAndParams_MustPairUp()
        {
            // 폭발 표식인데 반경이 없다
            using (TempDataRepo repo = Repo("[" + FireballJson.Replace(", \"params\": { \"explosion_radius\": 2 }", "") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/params/explosion_radius") && p.Contains("explodes"));
            }
            // 표식이 없는데 반경이 있다
            using (TempDataRepo repo = Repo("[" + FireballJson.Replace("\"flags\": [\"explodes\"]", "\"flags\": []") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/params/explosion_radius") && p.Contains("explodes"));
            }
            // 모르는 표식, 같은 표식 두 번, 모르는 params 열쇠는 스키마가 막는다
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"flags\": []", "\"flags\": [\"splits\"]") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/flags") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo("[" + FireballJson.Replace("\"flags\": [\"explodes\"]", "\"flags\": [\"explodes\", \"explodes\"]") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/flags") && p.Contains("스키마"));
            }
            using (TempDataRepo repo = Repo("[" + FireballJson.Replace("\"explosion_radius\": 2", "\"explosion_radius\": 2, \"blast\": 1") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/params") && p.Contains("스키마"));
            }
            // 투사체에 변형 효과
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"effect\": \"none\"", "\"effect\": \"bounce\"") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/effect"));
            }
            // 감속의 폭만 있고 지속이 없다
            using (TempDataRepo repo = Repo("[" + MagicBoltJson.Replace("\"effect\": \"none\"", "\"effect\": \"none\", \"params\": { \"slow_percent\": 30 }") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/params/slow_seconds"));
            }
        }

        [Fact]
        public void Problems_ModifierAndControlShape()
        {
            // 변형인데 효과가 없다
            using (TempDataRepo repo = Repo("[" + BounceJson.Replace("\"effect\": \"bounce\"", "\"effect\": \"none\"") + "]"))
            {
                List<string> problems = Problems(repo.Root);
                Assert.Contains(problems, p => p.Contains("data/sim/blocks.json:/blocks/0/effect"));
                Assert.Contains(problems, p => p.Contains("data/sim/blocks.json:/blocks/0/params/bounce_count"));
            }
            // 효과는 있는데 값이 없다
            using (TempDataRepo repo = Repo("[" + BounceJson.Replace(", \"params\": { \"bounce_count\": 2 }", "") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/params/bounce_count") && p.Contains("bounce"));
            }
            // 변형 블록의 갈래 수·열 배수는 1 이다
            using (TempDataRepo repo = Repo("[" + BounceJson.Replace("\"split_count\": 1", "\"split_count\": 2") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/split_count"));
            }
            using (TempDataRepo repo = Repo("[" + BounceJson.Replace("\"heat_multiplier\": 1", "\"heat_multiplier\": 1.6") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/heat_multiplier"));
            }
            // 분열인데 부채꼴 반각이 없다 / 열 배수가 1 이다 / 열을 더한다
            using (TempDataRepo repo = Repo("[" + SplitTwoJson.Replace(", \"params\": { \"fan_half_angle_degrees\": 10 }", "") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/params/fan_half_angle_degrees"));
            }
            using (TempDataRepo repo = Repo("[" + SplitTwoJson.Replace("\"heat_multiplier\": 1.6", "\"heat_multiplier\": 1") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/heat_multiplier"));
            }
            using (TempDataRepo repo = Repo("[" + SplitTwoJson.Replace("\"heat\": 0", "\"heat\": 1") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/heat") && p.Contains("제어"));
            }
            // 갈래가 하나인 제어 블록(적중 시 발동)은 부채꼴 반각을 갖지 않는다
            using (TempDataRepo repo = Repo("[" + SplitTwoJson.Replace("\"split_count\": 2", "\"split_count\": 1").Replace("\"heat_multiplier\": 1.6", "\"heat_multiplier\": 1") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/params/fan_half_angle_degrees"));
            }
            // 고친 것이 없는 줄은 통과한다
            using (TempDataRepo repo = Repo("[" + BounceJson + ", " + SplitTwoJson + "]"))
            {
                Assert.Empty(Problems(repo.Root));
            }
        }

        // 전이 피해는 코어의 표(0.7^k)로 계산한다. 원본의 배율이 그 표의 밑과 다르면 막는다(spec 02 18절 U18, T-53)
        [Fact]
        public void Problems_ChainHopMultiplier_MustMatchCoreTable()
        {
            using (TempDataRepo repo = Repo("[" + ChainJson.Replace("\"chain_hop_multiplier\": 0.7", "\"chain_hop_multiplier\": 0.75") + "]"))
            {
                Assert.Contains(Problems(repo.Root), p => p.Contains("data/sim/blocks.json:/blocks/0/params/chain_hop_multiplier") && p.Contains("Chain07"));
            }
            using (TempDataRepo repo = Repo("[" + ChainJson + "]"))
            {
                Assert.Empty(Problems(repo.Root));
            }
            Assert.Equal(3_006_477_107L, FixMath.PowTable(PowBase.Chain07, 1).Raw);   // 0.7 × 2^32 = 3,006,477,107.2 → 반올림
        }

        // 제어 블록의 갈래(spec 02 4.2 의 ControlType)는 Kind 와 SplitCount 로 가른다(T-53)
        [Fact]
        public void ControlBlocks_SplitOrTrigger_FromKindAndSplitCount()
        {
            ContentDb db = RepoDb();
            for (int id = 1; id <= 16; id++)
            {
                BlockDef b = db.Block((BlockId)id);
                bool split = b.Id == BlockId.SplitTwo || b.Id == BlockId.SplitThree;
                bool trigger = b.Id == BlockId.OnHitTrigger;
                Assert.True(split == b.IsSplit, b.Id + " IsSplit");
                Assert.True(trigger == b.IsTrigger, b.Id + " IsTrigger");
            }
        }

        // ── 명세 벡터: spec 02 16.1 ──

        [Fact]
        public void S02_TV_A01__SixteenBlocks_ProjectileSix_ControlThree_ModifierSeven()
        {
            ContentDb db = RepoDb();
            int projectile = 0, control = 0, modifier = 0, total = 0;
            for (int id = 1; id <= 16; id++)
            {
                BlockDef b = db.Block((BlockId)id);
                total++;
                switch (b.Kind)
                {
                    case BlockKind.Projectile: projectile++; break;
                    case BlockKind.Control: control++; break;
                    case BlockKind.Modifier: modifier++; break;
                }
            }
            Assert.Equal(16, total);
            Assert.Equal(6, projectile);
            Assert.Equal(3, control);
            Assert.Equal(7, modifier);
        }

        [Fact]
        public void S02_TV_A02__RarityGroups_CommonSix_RareSix_HeroFour()
        {
            ContentDb db = RepoDb();
            // spec 02 4.1: 일반 {magic_bolt, split_2, wall_bounce, double_size, pierce_2, frost}, 희귀 {great_fireball, boomerang, split_3, on_hit_trigger, ice_spike, shadow_orb},
            // 영웅 {chain_lightning, homing, overheat_x3, erosion}(spec 14 3.3 의 id 로 바꿔 적는다)
            AssertRarity(db, Rarity.Common, BlockId.MagicBolt, BlockId.SplitTwo, BlockId.WallBounce, BlockId.DoubleSize, BlockId.Pierce, BlockId.Frost);
            AssertRarity(db, Rarity.Rare, BlockId.Fireball, BlockId.Boomerang, BlockId.SplitThree, BlockId.OnHitTrigger, BlockId.IceSpike, BlockId.ShadowOrb);
            AssertRarity(db, Rarity.Hero, BlockId.ChainLightning, BlockId.Homing, BlockId.TripleHeat, BlockId.Corrosion);
        }

        private static void AssertRarity(ContentDb db, Rarity rarity, params BlockId[] expected)
        {
            var actual = new List<BlockId>();
            for (int id = 1; id <= 16; id++)
            {
                if (db.Block((BlockId)id).Rarity == rarity)
                {
                    actual.Add((BlockId)id);
                }
            }
            var want = new List<BlockId>(expected);
            want.Sort();                                          // BlockId 오름차순(같은 키는 없다)
            Assert.Equal(want, actual);
        }

        [Fact]
        public void S02_TV_A03__EveryElementHasAProjectileBlock()
        {
            ContentDb db = RepoDb();
            AssertProjectilesOf(db, Element.Fire, BlockId.Fireball);
            AssertProjectilesOf(db, Element.Lightning, BlockId.ChainLightning);
            AssertProjectilesOf(db, Element.Arcane, BlockId.MagicBolt, BlockId.Boomerang);
            AssertProjectilesOf(db, Element.Ice, BlockId.IceSpike);
            AssertProjectilesOf(db, Element.Dark, BlockId.ShadowOrb);
        }

        private static void AssertProjectilesOf(ContentDb db, Element element, params BlockId[] expected)
        {
            var actual = new List<BlockId>();
            for (int id = 1; id <= 16; id++)
            {
                BlockDef b = db.Block((BlockId)id);
                if (b.Kind == BlockKind.Projectile && b.Element == element)
                {
                    actual.Add(b.Id);
                }
            }
            var want = new List<BlockId>(expected);
            want.Sort();
            Assert.Equal(want, actual);
            Assert.NotEmpty(actual);
        }

        // 16행의 종류·등급·속성·열·피해·간격·속도·사거리·분열·피해 보너스와 메모의 수치가 4.1 표와 같다.
        // 메모의 수치는 Table 의 With(…)에 있고(AssertRow 가 전부 비교한다), 아래에서 4.1 의 메모 칸을 한 줄씩 다시 적는다
        [Fact]
        public void S02_TV_A04__SixteenRows_MatchTable41_IncludingMemoValues()
        {
            ContentDb db = RepoDb();
            Assert.Equal(16, Table.Length);
            foreach (Row r in Table)
            {
                AssertRow(db.Block(r.Id), r);
            }
            // 거대 화염구: 반경 2 폭발
            BlockDef fireball = db.Block(BlockId.Fireball);
            Assert.True((fireball.Flags & ProjectileFlags.Explodes) != 0);
            Assert.Equal(2 * OneRaw, fireball.Params.ExplosionRadius.Raw);
            // 연쇄 번개: 3회 전이(반경 3, 전이마다 ×0.7), 늑대 회피 무시
            BlockDef chain = db.Block(BlockId.ChainLightning);
            Assert.Equal(ProjectileFlags.Chains | ProjectileFlags.IgnoresWolfDodge, chain.Flags);
            Assert.Equal(3, chain.Params.ChainHops);
            Assert.Equal(3 * OneRaw, chain.Params.ChainRadius.Raw);
            Assert.Equal(700, chain.Params.ChainHopMulPermille);
            // 부메랑: 왕복 2회 타격(갈 때·올 때 각 1회 = 돌아오는 탄), 편도 6
            BlockDef boomerang = db.Block(BlockId.Boomerang);
            Assert.Equal(ProjectileFlags.Returns, boomerang.Flags);
            Assert.Equal(6 * OneRaw, boomerang.Range.Raw);
            // 그림자 구체: 적을 지나가며 적마다 1회 타격
            Assert.Equal(ProjectileFlags.PassThrough, db.Block(BlockId.ShadowOrb).Flags);
            // 분열 ×1.6 / ×2.2, 적중 시 발동은 열 0 과 새 스코프
            Assert.Equal(1600, db.Block(BlockId.SplitTwo).HeatMulPermille);
            Assert.Equal(2200, db.Block(BlockId.SplitThree).HeatMulPermille);
            BlockDef trigger = db.Block(BlockId.OnHitTrigger);
            Assert.Equal(0, trigger.HeatHu);
            Assert.True(trigger.IsTrigger);
            // 변형: 벽 튕김 +2, 크기 2배 +3·+20%, 관통 +2 는 +3·+10%, 유도 +5(선회각 제한), 과열 3배 +2(임계 배율 2 → 3)
            Assert.Equal(ModifierEffect.Bounce, db.Block(BlockId.WallBounce).Effect);
            BlockDef size = db.Block(BlockId.DoubleSize);
            Assert.Equal(ModifierEffect.Size, size.Effect);
            Assert.Equal(200, size.DamageBonusPermille);
            Assert.Equal(2000, size.Params.RadiusMulPermille);
            BlockDef pierce = db.Block(BlockId.Pierce);
            Assert.Equal(ModifierEffect.Pierce, pierce.Effect);
            Assert.Equal(100, pierce.DamageBonusPermille);
            Assert.Equal(2, pierce.Params.PierceCount);
            BlockDef homing = db.Block(BlockId.Homing);
            Assert.Equal(ModifierEffect.Homing, homing.Effect);
            Assert.True(homing.Params.HomingTurnCentiPerTick > 0);
            BlockDef triple = db.Block(BlockId.TripleHeat);
            Assert.Equal(ModifierEffect.TripleHeat, triple.Effect);
            Assert.Equal(3, triple.Params.CritMultiplier);
        }

        // D 2.5 의 네 줄: 얼음 송곳 −30% 1초, 그림자 구체, 서리 −20% 1초, 잠식 +15% 3초(1초 = 60틱, spec 02 2.4)
        [Fact]
        public void S02_TV_A05__FourDecisionRows_WithStatusValues()
        {
            ContentDb db = RepoDb();
            BlockDef ice = db.Block(BlockId.IceSpike);
            Assert.Equal(BlockKind.Projectile, ice.Kind);
            Assert.Equal(Rarity.Rare, ice.Rarity);
            Assert.Equal(Element.Ice, ice.Element);
            Assert.Equal(8 * HuPerHeat, ice.HeatHu);
            Assert.Equal(14 * OneRaw, ice.BaseDamage.Raw);
            Assert.Equal(18, ice.IntervalTicks);                  // 0.3초
            Assert.Equal(858_993_459L, ice.SpeedPerTick.Raw);     // 12 칸/초
            Assert.Equal(9 * OneRaw, ice.Range.Raw);
            Assert.Equal(300, ice.Params.SlowPermille);           // 이동 −30%
            Assert.Equal(60, ice.Params.SlowTicks);               // 1초
            BlockDef orb = db.Block(BlockId.ShadowOrb);
            Assert.Equal(BlockKind.Projectile, orb.Kind);
            Assert.Equal(Rarity.Rare, orb.Rarity);
            Assert.Equal(Element.Dark, orb.Element);
            Assert.Equal(14 * HuPerHeat, orb.HeatHu);
            Assert.Equal(28 * OneRaw, orb.BaseDamage.Raw);
            Assert.Equal(30, orb.IntervalTicks);                  // 0.5초
            Assert.Equal(429_496_730L, orb.SpeedPerTick.Raw);     // 6 칸/초
            Assert.Equal(8 * OneRaw, orb.Range.Raw);
            BlockDef frost = db.Block(BlockId.Frost);
            Assert.Equal(BlockKind.Modifier, frost.Kind);
            Assert.Equal(Rarity.Common, frost.Rarity);
            Assert.Equal(Element.Ice, frost.Element);
            Assert.Equal(3 * HuPerHeat, frost.HeatHu);
            Assert.Equal(ModifierEffect.Frost, frost.Effect);
            Assert.Equal(200, frost.Params.SlowPermille);         // 이동 −20%
            Assert.Equal(60, frost.Params.SlowTicks);             // 1초
            BlockDef erosion = db.Block(BlockId.Corrosion);
            Assert.Equal(BlockKind.Modifier, erosion.Kind);
            Assert.Equal(Rarity.Hero, erosion.Rarity);
            Assert.Equal(Element.Dark, erosion.Element);
            Assert.Equal(5 * HuPerHeat, erosion.HeatHu);
            Assert.Equal(ModifierEffect.Corrosion, erosion.Effect);
            Assert.Equal(150, erosion.Params.DamageTakenBonusPermille);   // 받는 피해 +15%
            Assert.Equal(180, erosion.Params.DamageTakenBonusTicks);      // 3초
        }

        [Fact]
        public void S02_TV_A06__ProjectileIntervals_InTicks()
        {
            ContentDb db = RepoDb();
            Assert.Equal(9, db.Block(BlockId.MagicBolt).IntervalTicks);
            Assert.Equal(36, db.Block(BlockId.Fireball).IntervalTicks);
            Assert.Equal(27, db.Block(BlockId.ChainLightning).IntervalTicks);
            Assert.Equal(21, db.Block(BlockId.Boomerang).IntervalTicks);
            Assert.Equal(18, db.Block(BlockId.IceSpike).IntervalTicks);
            Assert.Equal(30, db.Block(BlockId.ShadowOrb).IntervalTicks);
        }

        [Fact]
        public void S02_TV_A07__ProjectileSpeeds_RawPerTick_ChainLightningIsInstant()
        {
            ContentDb db = RepoDb();
            Assert.Equal(1_002_159_036L, db.Block(BlockId.MagicBolt).SpeedPerTick.Raw);
            Assert.Equal(644_245_094L, db.Block(BlockId.Fireball).SpeedPerTick.Raw);
            Assert.Equal(715_827_883L, db.Block(BlockId.Boomerang).SpeedPerTick.Raw);
            Assert.Equal(858_993_459L, db.Block(BlockId.IceSpike).SpeedPerTick.Raw);
            Assert.Equal(429_496_730L, db.Block(BlockId.ShadowOrb).SpeedPerTick.Raw);
            BlockDef chain = db.Block(BlockId.ChainLightning);
            Assert.True(chain.IsInstant);
            Assert.Equal(0L, chain.SpeedPerTick.Raw);
        }

        // 아래 셋의 표는 코어의 미리 만든 표(FixMath.PowTable, 생성: tools/Wizard.TableGen)다. 이 작업은 표를 만들지 않고 명세 값과 대조만 한다.
        // 벡터 TV-A15·TV-A16 은 결투 열(1 + 0.03 × 단계)까지 보아야 해서 피해 공식 작업(M2-SPL-04)으로 옮겼다(T-53). 여기의 둘은 무한 웨이브 열만 보는
        // 보조 시험이라 벡터 이름을 쓰지 않는다. TV-A17(감쇠 표)은 이 작업의 벡터다
        [Fact]
        public void EnhanceTable_Stage13_EndlessColumn_MatchesSpec02_11_3()
        {
            Fix64 v = FixMath.PowTable(PowBase.Enhance115, 13);
            Assert.Equal(26_426_021_613L, v.Raw);                 // 1.15^13 = 6.152788… (spec 02 11.3 표)
            Assert.Equal(6.152788m, decimal.Round((decimal)v.Raw / OneRaw, 6));
        }

        [Fact]
        public void EnhanceTable_Stage0To13_EndlessColumn_RawMatchesSpec02_11_3()
        {
            long[] expected =
            {
                4_294_967_296L, 4_939_212_390L, 5_680_094_249L, 6_532_108_386L, 7_511_924_644L, 8_638_713_341L, 9_934_520_342L,
                11_424_698_393L, 13_138_403_152L, 15_109_163_625L, 17_375_538_169L, 19_981_868_894L, 22_979_149_228L, 26_426_021_613L,
            };
            for (int n = 0; n < expected.Length; n++)
            {
                Assert.True(expected[n] == FixMath.PowTable(PowBase.Enhance115, n).Raw, "1.15^" + n);
            }
        }

        [Fact]
        public void S02_TV_A17__DepthDecayTables_Depth0To3()
        {
            long[] decay085 = { 4_294_967_296L, 3_650_722_202L, 3_103_113_871L, 2_637_646_791L };   // 1, 0.85, 0.7225, 0.614125
            long[] decay092 = { 4_294_967_296L, 3_951_369_912L, 3_635_260_319L, 3_344_439_494L };   // 1, 0.92, 0.8464, 0.778688
            for (int d = 0; d < 4; d++)
            {
                Assert.True(decay085[d] == FixMath.PowTable(PowBase.Decay085, d).Raw, "0.85^" + d);
                Assert.True(decay092[d] == FixMath.PowTable(PowBase.Decay092, d).Raw, "0.92^" + d);
            }
        }

        private static ContentDb Load(byte[] section) =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[PackWriter.CoreCompatLength],
                new[] { new PackSection((ushort)ContentSection.Blocks, section) })));

        private static byte[] Section(byte[] pack)
        {
            Assert.True(ContentPack.Load(pack).TryGetSection((ushort)ContentSection.Blocks, out ReadOnlyMemory<byte> s));
            return s.ToArray();
        }

        // spec 02 4.1·4.2 의 두 줄(임시 저장소용)
        private const string MagicBoltJson =
            "{ \"id\": \"block.magic_bolt\", \"kind\": \"projectile\", \"rarity\": \"common\", \"element\": \"element.arcane\", \"heat\": 4, \"heat_multiplier\": 1,\n" +
            "  \"split_count\": 1, \"base_damage\": 10, \"interval_seconds\": 0.15, \"speed\": 14, \"instant\": false, \"range\": 10, \"hit_radius\": 0.12, \"damage_bonus\": 0,\n" +
            "  \"flags\": [], \"effect\": \"none\" }";

        private const string FireballJson =
            "{ \"id\": \"block.fireball\", \"kind\": \"projectile\", \"rarity\": \"rare\", \"element\": \"element.fire\", \"heat\": 18, \"heat_multiplier\": 1,\n" +
            "  \"split_count\": 1, \"base_damage\": 40, \"interval_seconds\": 0.6, \"speed\": 9, \"instant\": false, \"range\": 8, \"hit_radius\": 0.18, \"damage_bonus\": 0,\n" +
            "  \"flags\": [\"explodes\"], \"effect\": \"none\", \"params\": { \"explosion_radius\": 2 } }";

        private const string ChainJson =
            "{ \"id\": \"block.chain_lightning\", \"kind\": \"projectile\", \"rarity\": \"hero\", \"element\": \"element.lightning\", \"heat\": 12, \"heat_multiplier\": 1,\n" +
            "  \"split_count\": 1, \"base_damage\": 18, \"interval_seconds\": 0.45, \"speed\": 0, \"instant\": true, \"range\": 7, \"hit_radius\": 0.12, \"damage_bonus\": 0,\n" +
            "  \"flags\": [\"chains\", \"ignores_wolf_dodge\"], \"effect\": \"none\", \"params\": { \"chain_hops\": 3, \"chain_radius\": 3, \"chain_hop_multiplier\": 0.7 } }";

        private const string SplitTwoJson =
            "{ \"id\": \"block.split_two\", \"kind\": \"control\", \"rarity\": \"common\", \"element\": \"element.neutral\", \"heat\": 0, \"heat_multiplier\": 1.6,\n" +
            "  \"split_count\": 2, \"base_damage\": 0, \"interval_seconds\": 0, \"speed\": 0, \"instant\": false, \"range\": 0, \"hit_radius\": 0, \"damage_bonus\": 0,\n" +
            "  \"flags\": [], \"effect\": \"none\", \"params\": { \"fan_half_angle_degrees\": 10 } }";

        private const string BounceJson =
            "{ \"id\": \"block.wall_bounce\", \"kind\": \"modifier\", \"rarity\": \"common\", \"element\": \"element.neutral\", \"heat\": 2, \"heat_multiplier\": 1,\n" +
            "  \"split_count\": 1, \"base_damage\": 0, \"interval_seconds\": 0, \"speed\": 0, \"instant\": false, \"range\": 0, \"hit_radius\": 0, \"damage_bonus\": 0,\n" +
            "  \"flags\": [], \"effect\": \"bounce\", \"params\": { \"bounce_count\": 2 } }";

        private static TempDataRepo Repo(string blocksArray)
        {
            var repo = new TempDataRepo();
            repo.Write("data/schema/blocks.schema.json", System.IO.File.ReadAllText(TestRepo.Path("data/schema/blocks.schema.json")));
            repo.Write("data/sim/blocks.json", "{ \"blocks\": " + blocksArray + " }\n");
            repo.Write("data/names/names.ko.json", "{\n  \"block.chain_lightning\": \"연쇄 번개\",\n  \"block.fireball\": \"거대 화염구\",\n  \"block.magic_bolt\": \"마력탄\",\n  \"block.nope\": \"없음\",\n" +
                "  \"block.split_two\": \"2갈래 분열\",\n  \"block.wall_bounce\": \"벽 튕김\",\n  \"~end\": \"\"\n}\n");
            return repo;
        }
    }
}
