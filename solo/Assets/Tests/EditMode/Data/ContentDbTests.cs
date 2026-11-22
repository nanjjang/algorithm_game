using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Wizard.Solo.Data;

namespace Wizard.Solo.Tests.Data
{
    /// <summary>ContentDb: 전 파일 로드, 개수, 등급 안 순서, 조회 규칙, Ids 대응.</summary>
    public class ContentDbTests
    {
        static ContentDb Db => (ContentDb)TestContent.Db;

        [Test]
        public void S00_Load__AllFilesWithoutErrors()
        {
            var errors = new List<string>();
            var db = ContentDb.LoadFromDirectory(TestContent.DataDir, TestContent.SoloExtraDir, errors.Add);
            CollectionAssert.AreEqual(db.LoadErrors, errors);
            Assert.IsEmpty(db.LoadErrors, string.Join("\n", db.LoadErrors));
            Assert.IsNotNull(db.Elements);
            Assert.IsNotNull(db.Tries);
            Assert.IsNotNull(db.Waves);
            Assert.IsNotNull(db.Scoring);
            Assert.IsNotNull(db.Starfall);
            Assert.IsNotNull(db.Field);
            Assert.IsNotNull(db.Enhance);
            Assert.IsNotNull(db.Economy);
            Assert.IsNotNull(db.Mutations);
            Assert.IsNotNull(db.Drops);
            Assert.IsNotNull(db.Camp);
            Assert.IsNotNull(db.Const);
            Assert.IsNotNull(db.Practice);
            Assert.IsNotNull(db.Practice.Dummy);
            StringAssert.StartsWith("solo-", db.DataVersion);
        }

        [Test]
        public void S00_Load__DataVersionIsStableForSameFiles()
        {
            var a = ContentDb.LoadFromDirectory(TestContent.DataDir, TestContent.SoloExtraDir, msg => { });
            var b = ContentDb.LoadFromDirectory(TestContent.DataDir, TestContent.SoloExtraDir, msg => { });
            Assert.AreEqual(a.DataVersion, b.DataVersion);
        }

        [Test]
        public void S00_Load__MissingFileIsReportedNotThrown()
        {
            var db = ContentDb.Load(rel => null, name => null, msg => { });
            Assert.IsNotEmpty(db.LoadErrors);
            Assert.IsNull(db.Block(Ids.Block.MagicBolt));
            Assert.AreEqual(0, db.Blocks.Count);
            Assert.IsNotNull(db.Enhance);
        }

        // 03 요약서 2.2절, S02 4.1 TV-A02: 블록 16 = 일반 6, 희귀 6, 영웅 4
        [Test]
        public void S02_TV_A02__BlockCountsByRarity()
        {
            Assert.AreEqual(16, Db.Blocks.Count);
            int common = 0, rare = 0, hero = 0;
            foreach (var b in Db.Blocks)
            {
                if (b.Rarity == Ids.Rarity.Common) common++;
                else if (b.Rarity == Ids.Rarity.Rare) rare++;
                else if (b.Rarity == Ids.Rarity.Hero) hero++;
                else Assert.Fail("unknown rarity " + b.Rarity + " on " + b.Id);
            }
            Assert.AreEqual(6, common);
            Assert.AreEqual(6, rare);
            Assert.AreEqual(4, hero);
        }

        // 06 요약서 0.2절: 등급 안 추첨 순서 고정
        [Test]
        public void S07_RarityOrder__FixedOrderWithinRarity()
        {
            CollectionAssert.AreEqual(new[]
            {
                Ids.Block.MagicBolt, Ids.Block.SplitTwo, Ids.Block.WallBounce, Ids.Block.DoubleSize, Ids.Block.Pierce, Ids.Block.Frost
            }, Db.BlocksOfRarity(Ids.Rarity.Common));
            CollectionAssert.AreEqual(new[]
            {
                Ids.Block.Fireball, Ids.Block.Boomerang, Ids.Block.SplitThree, Ids.Block.OnHitTrigger, Ids.Block.IceSpike, Ids.Block.ShadowOrb
            }, Db.BlocksOfRarity(Ids.Rarity.Rare));
            CollectionAssert.AreEqual(new[]
            {
                Ids.Block.ChainLightning, Ids.Block.Homing, Ids.Block.TripleHeat, Ids.Block.Corrosion
            }, Db.BlocksOfRarity(Ids.Rarity.Hero));
            Assert.AreEqual(0, Db.BlocksOfRarity("legendary").Count);
            Assert.AreEqual(0, Db.BlocksOfRarity(null).Count);
        }

        [Test]
        public void S00_Counts__CharactersArtsEnemiesElitesBossesFramesWhimsTods()
        {
            Assert.AreEqual(Ids.Char.Count, Db.Characters.Count);
            Assert.AreEqual(15, Db.Arts.Count);
            int unique = 0, common = 0, secret = 0;
            foreach (var a in Db.Arts)
            {
                if (a.Kind == "unique") unique++;
                else if (a.Kind == "common") common++;
                else if (a.Kind == "secret") secret++;
            }
            Assert.AreEqual(5, unique);
            Assert.AreEqual(10, common + secret);

            // 일반 적 12 + 졸개(갓버섯 새싹) 1. 허수아비는 목록에 없고 Enemy(id)로만 찾는다.
            Assert.AreEqual(13, Db.Enemies.Count);
            int minions = 0;
            foreach (var e in Db.Enemies) if (e.Minion) minions++;
            Assert.AreEqual(1, minions);
            Assert.IsTrue(Db.Enemy(Ids.Enemy.SporeSeedling).Minion);

            Assert.AreEqual(12, Db.Elites.Count);
            foreach (var e in Db.Enemies)
            {
                if (e.Minion) continue;
                Assert.IsNotNull(Db.Elite(Ids.Enemy.EliteOf(e.Id)), "elite of " + e.Id);
            }

            Assert.AreEqual(6, Db.Bosses.Count);
            Assert.AreEqual(5, Db.Frames.Count);
            Assert.AreEqual(8, Db.Modifiers.Count);
            Assert.AreEqual(4, Db.Tods.Count);
            Assert.AreEqual(5, Db.WeatherEvents.Count);
        }

        // 05 요약서 8.1·8.4절, 01 요약서 4.2절: 보스 순환 0~5
        [Test]
        public void S06_BossRotation__IndexZeroToFiveInOrder()
        {
            string[] expected =
            {
                Ids.Boss.KilnBoar, Ids.Boss.MirrorPhantom, Ids.Boss.SporeQueen, Ids.Boss.StormOwl, Ids.Boss.FrostGoat, Ids.Boss.NightSerpent
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], Db.Bosses[i].Id);
                Assert.AreEqual(i, Db.Bosses[i].RotationIndex);
                Assert.AreSame(Db.Bosses[i], Db.Boss(expected[i]));
            }
            Assert.IsNotNull(Db.Boss(Ids.Boss.MirrorPhantom).FirstEncounter);
            Assert.IsNull(Db.Boss(Ids.Boss.KilnBoar).FirstEncounter);
        }

        [Test]
        public void S00_Lookup__MissingIdsReturnNull()
        {
            Assert.IsNull(Db.Block("block.nope"));
            Assert.IsNull(Db.Block(null));
            Assert.IsNull(Db.Character("char.9"));
            Assert.IsNull(Db.Art("art.nope"));
            Assert.IsNull(Db.Enemy("enemy.nope"));
            Assert.IsNull(Db.Elite("elite.nope"));
            Assert.IsNull(Db.Elite("enemy.nope"));
            Assert.IsNull(Db.Elite("enemy."));
            Assert.IsNull(Db.Elite(null));
            Assert.IsNull(Db.Boss("boss.nope"));
            Assert.IsNull(Db.Frame("frame.nope"));
            Assert.IsNull(Db.Modifier("whim.nope"));
            Assert.IsNull(Db.Tod("tod.nope"));
            Assert.IsNull(Db.TrieArts("char.9"));
            Assert.IsNull(Db.SoloExtra("__no_such_fragment__"));
            Assert.IsNull(Db.SoloExtra("__no_such_fragment__"));   // 두 번째는 캐시에서
        }

        [Test]
        public void S00_Elite__AcceptsEliteAndEnemyIds()
        {
            var byElite = Db.Elite("elite.slime_moss");
            Assert.IsNotNull(byElite);
            Assert.AreSame(byElite, Db.Elite(Ids.Enemy.SlimeMoss));
            Assert.AreEqual(Ids.Enemy.EliteOf(Ids.Enemy.WolfBramble), Db.Elite(Ids.Enemy.WolfBramble).Id);
        }

        [Test]
        public void S00_Enemy__DummyComesFromPracticeJson()
        {
            var d = Db.Enemy(Ids.Enemy.Dummy);
            Assert.IsNotNull(d);
            Assert.AreSame(Db.Practice.Dummy, d);
            Assert.AreEqual(Ids.Enemy.Dummy, d.Id);
            Assert.AreEqual(0f, d.MoveSpeed);
            Assert.AreEqual(0, d.ContactDamage);
            Assert.AreEqual(0, d.AttackDamage);
            Assert.AreEqual(0, d.Cost);
            Assert.IsTrue(d.KnockbackImmune);
            Assert.IsEmpty(d.Attacks);
            Assert.IsFalse(d.Minion);
            foreach (var e in Db.Enemies) Assert.AreNotEqual(Ids.Enemy.Dummy, e.Id);
        }

        // 00 문서 D-59: 튜토리얼 블록 = 일반 등급 고정 순서에서 투사체가 아닌 첫 블록
        [Test]
        public void S00_D59__TutorialBlockIsFirstNonProjectileCommon()
        {
            string expected = null;
            foreach (var id in Db.BlocksOfRarity(Ids.Rarity.Common))
                if (!Db.Block(id).IsProjectile) { expected = id; break; }
            Assert.IsNotNull(expected);
            Assert.AreEqual(expected, Db.Practice.TutorialBlockId);
            Assert.AreEqual(Ids.Block.SplitTwo, Db.Practice.TutorialBlockId);
            Assert.AreEqual(Ids.Rarity.Common, Db.Block(Db.Practice.TutorialBlockId).Rarity);
        }

        [Test]
        public void S00_Practice__ReportIntervals()
        {
            Assert.AreEqual(0.5f, Db.Practice.ReportIntervalSeconds, 1e-6f);   // 솔로 임시
            Assert.AreEqual(10f, Db.Practice.RecentDamageWindowSeconds, 1e-6f);   // 08 8.5절
        }

        [Test]
        public void S00_Tries__ElevenArtsPerCharacterEndingWithUnique()
        {
            foreach (var ch in Db.Characters)
            {
                var arts = Db.TrieArts(ch.Id);
                Assert.IsNotNull(arts, ch.Id);
                Assert.AreEqual(11, arts.Count, ch.Id);
                Assert.AreEqual(ch.UniqueArt, arts[10], ch.Id);
                for (int i = 0; i < 10; i++) Assert.AreEqual(Ids.Art.All[i], arts[i]);
                Assert.AreEqual(ch.Id, Db.Art(ch.UniqueArt).Owner);
            }
        }

        [Test]
        public void S00_Arts__JsonOrderMatchesIdsAll()
        {
            Assert.AreEqual(Ids.Art.All.Length, Db.Arts.Count);
            for (int i = 0; i < Ids.Art.All.Length; i++) Assert.AreEqual(Ids.Art.All[i], Db.Arts[i].Id);
            Assert.IsTrue(Db.Art(Ids.Art.Bloom).CostIsAll);
            Assert.AreEqual(Ids.Art.Octastar, Db.Art(Ids.Art.Bloom).Unlock.Prereq);
        }

        [Test]
        public void S00_Field__PlaceFieldIs24By14()
        {
            Assert.AreEqual(Ids.Place.Field, Db.Field.Id);
            Assert.AreEqual(24f, Db.Field.Width);
            Assert.AreEqual(14f, Db.Field.Height);
            Assert.AreEqual(12f, Db.Field.SpawnPoints[0].X);
            Assert.AreEqual(7f, Db.Field.SpawnPoints[0].Y);
        }

        // ---------------------------------------------------------------- Ids ↔ 데이터

        [Test]
        public void S00_Ids__EveryContentIdExistsInData()
        {
            foreach (var id in ConstStrings(typeof(Ids.Block))) Assert.IsNotNull(Db.Block(id), id);
            foreach (var id in Ids.Art.All) Assert.IsNotNull(Db.Art(id), id);
            foreach (var id in ConstStrings(typeof(Ids.Art))) Assert.IsNotNull(Db.Art(id), id);
            for (int n = 1; n <= Ids.Char.Count; n++) Assert.IsNotNull(Db.Character(Ids.Char.Of(n)), Ids.Char.Of(n));
            foreach (var id in ConstStrings(typeof(Ids.Char))) Assert.IsNotNull(Db.Character(id), id);
            foreach (var id in ConstStrings(typeof(Ids.Enemy))) Assert.IsNotNull(Db.Enemy(id), id);
            foreach (var id in ConstStrings(typeof(Ids.Boss))) Assert.IsNotNull(Db.Boss(id), id);
            foreach (var id in ConstStrings(typeof(Ids.Tod))) Assert.IsNotNull(Db.Tod(id), id);
            foreach (var id in ConstStrings(typeof(Ids.Whim))) Assert.IsNotNull(Db.Modifier(id), id);
            foreach (var id in ConstStrings(typeof(Ids.Frame))) Assert.IsNotNull(Db.Frame(id), id);
            foreach (var id in ConstStrings(typeof(Ids.Place))) Assert.AreEqual(id, Db.Field.Id);

            var events = new HashSet<string>();
            foreach (var e in Db.WeatherEvents) events.Add(e.Id);
            foreach (var id in ConstStrings(typeof(Ids.Event))) Assert.IsTrue(events.Contains(id), id);

            var passives = new HashSet<string>();
            var traits = new HashSet<string>();
            var elements = new HashSet<string>();
            foreach (var c in Db.Characters) { passives.Add(c.Passive); traits.Add(c.WandTrait); elements.Add(c.Element); }
            foreach (var b in Db.Blocks) elements.Add(b.Element);
            foreach (var id in ConstStrings(typeof(Ids.Passive))) Assert.IsTrue(passives.Contains(id), id);
            foreach (var id in ConstStrings(typeof(Ids.WandTrait))) Assert.IsTrue(traits.Contains(id), id);
            foreach (var id in ConstStrings(typeof(Ids.Element))) Assert.IsTrue(elements.Contains(id), id);

            var tags = new HashSet<string>();
            foreach (var e in Db.Enemies) foreach (var t in e.Tags) tags.Add(t);
            foreach (var t in new[] { Ids.Tag.Heavy, Ids.Tag.Flying, Ids.Tag.Burrowed, Ids.Tag.Rooted }) Assert.IsTrue(tags.Contains(t), t);

            CollectionAssert.AreEquivalent(Ids.Mutation.All, ConstStrings(typeof(Ids.Mutation)));
        }

        [Test]
        public void S00_Ids__DataIdsAreKnownToIds()
        {
            var blockIds = new HashSet<string>(ConstStrings(typeof(Ids.Block)));
            foreach (var b in Db.Blocks) Assert.IsTrue(blockIds.Contains(b.Id), b.Id);
            var bossIds = new HashSet<string>(ConstStrings(typeof(Ids.Boss)));
            foreach (var b in Db.Bosses) Assert.IsTrue(bossIds.Contains(b.Id), b.Id);
            var enemyIds = new HashSet<string>(ConstStrings(typeof(Ids.Enemy)));
            foreach (var e in Db.Enemies) Assert.IsTrue(enemyIds.Contains(e.Id), e.Id);
            var whimIds = new HashSet<string>(ConstStrings(typeof(Ids.Whim)));
            foreach (var m in Db.Modifiers) Assert.IsTrue(whimIds.Contains(m.Id), m.Id);
        }

        /// <summary>정적 클래스의 const string 값 전부.</summary>
        internal static List<string> ConstStrings(Type t)
        {
            var list = new List<string>();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Static))
                if (f.IsLiteral && f.FieldType == typeof(string)) list.Add((string)f.GetRawConstantValue());
            return list;
        }
    }
}
