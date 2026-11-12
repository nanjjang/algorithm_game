using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Wizard.Content;
using Wizard.DataBuild.Core;
using Wizard.TestKit;
using Xunit;

namespace Wizard.Content.Tests.Pack
{
    // CONTRACTS 11.2 의 절 목차, ARCHITECTURE 2절의 data/sim·data/rules 파일, CONTRACTS 2.3 의 접근자, T-33 의 절 훅
    public sealed class ContentSectionTests
    {
        // CONTRACTS 11.2 표 그대로(id, 절 이름)
        public static readonly (ushort Id, string Name)[] Contracts11_2 =
        {
            (1, "characters"), (2, "blocks"), (3, "arts"), (4, "mutations"), (5, "enemies"), (6, "bosses"), (7, "waves"),
            (8, "frames"), (9, "modifiers"), (10, "tod"), (11, "scoring"), (12, "arena"), (13, "duel_rules"), (14, "tries"),
            (15, "starfall"), (16, "weather_events"), (17, "boss_rotation"), (18, "elites"), (19, "elements"),
            (20, "enhance"), (21, "economy"), (22, "drops"), (23, "leagues"), (24, "season"), (25, "titles"), (26, "cosmetics"),
            (27, "plaza"), (28, "shop"), (29, "items"), (30, "bag"), (31, "run_limits"), (32, "il"), (33, "fees"), (34, "net"),
            (35, "chat_filter"),
            (40, "sim_limits"),
        };

        // ARCHITECTURE 2절 레이아웃의 data/sim·data/rules 파일, 그리고 CONTRACTS 2.3 이 규칙 쪽 절로 더 적은 items, net, chat_filter
        public static readonly string[] Architecture2Files =
        {
            "sim/characters.json", "sim/blocks.json", "sim/arts.json", "sim/mutations.json", "sim/enemies.json", "sim/elites.json",
            "sim/bosses.json", "sim/waves.json", "sim/frames.json", "sim/modifiers.json", "sim/tod.json", "sim/scoring.json",
            "sim/starfall.json", "sim/arena.json", "sim/duel_rules.json",
            "rules/enhance.json", "rules/economy.json", "rules/shop.json", "rules/drops.json", "rules/bag.json", "rules/leagues.json",
            "rules/il.json", "rules/fees.json", "rules/season.json", "rules/titles.json", "rules/cosmetics.json", "rules/plaza.json",
            "rules/limits.json",
            "rules/items.json", "rules/net.json", "rules/chat_filter.json",
        };

        [Fact]
        public void SectionIds_MatchContracts11_2Exactly()
        {
            var declared = ((ContentSection[])Enum.GetValues(typeof(ContentSection))).Where(s => s != ContentSection.None).ToArray();
            Assert.Equal(Contracts11_2.Select(t => t.Id).ToArray(), declared.Select(s => (ushort)s).OrderBy(x => x).ToArray());
            foreach (var (id, name) in Contracts11_2)
            {
                Assert.Equal(name, ContentSections.Name((ContentSection)id));
            }
            Assert.Null(ContentSections.Name(ContentSection.None));
            Assert.Null(ContentSections.Name((ContentSection)36));
        }

        [Fact]
        public void Ranges_SimRulesSimExt()
        {
            foreach (var (id, _) in Contracts11_2)
            {
                bool rules = id >= 20 && id <= 39;
                Assert.Equal(rules, ContentSections.IsRules((ContentSection)id));
                Assert.Equal(rules, SectionFiles.DataFile((ContentSection)id).StartsWith("rules/", StringComparison.Ordinal));
            }
        }

        [Fact]
        public void EveryArchitectureDataFile_HasASection()
        {
            foreach (string f in Architecture2Files)
            {
                Assert.True(SectionFiles.TryFromDataFile(f, out ContentSection s), f + " 에 절이 없다");
                Assert.Equal(f, SectionFiles.DataFile(s));
            }
            Assert.True(SectionFiles.TryFromDataFile("rules/limits.json", out ContentSection lim));
            Assert.Equal(ContentSection.RunLimits, lim);
            Assert.True(SectionFiles.TryFromDataFile("sim/sim_limits.json", out ContentSection simLim));
            Assert.Equal(ContentSection.SimLimits, simLim);
            Assert.False(SectionFiles.TryFromDataFile("sim/unknown.json", out _));
            Assert.False(SectionFiles.TryFromDataFile("rules/characters.json", out _));
            Assert.Equal(Contracts11_2.Length, SectionFiles.All.Count);
        }

        // T-33: 절마다 partial void Load<절>(ReadOnlySpan<byte>) 훅이 있고, LoadSections 가 id 오름차순으로 그 훅을 부른다.
        // 구현이 없는 부분 메서드는 메타데이터에 남지 않으므로 선언은 소스에서 본다.
        [Fact]
        public void EverySection_HasALoadHook_CalledInIdOrder()
        {
            string src = File.ReadAllText(TestRepo.Path("src/Wizard.Content/Pack/Core/ContentDb.Core.cs"));
            int last = -1;
            foreach (var (id, _) in Contracts11_2)
            {
                string member = ((ContentSection)id).ToString();
                Assert.Contains("partial void Load" + member + "(ReadOnlySpan<byte> section);", src);
                string call = "TryGetSection((ushort)ContentSection." + member + ", out s)) Load" + member + "(s.Span);";
                int at = src.IndexOf(call, StringComparison.Ordinal);
                Assert.True(at > last, member + " 의 훅 호출이 없거나 id 순서가 아니다");
                last = at;
            }
        }

        // T-47: Build(생성자)가 LoadSections 를 부른다. 부르지 않으면 절 파일이 훅을 구현해도 읽히지 않는다.
        [Fact]
        public void Build_RunsTheSectionHooks()
        {
            string src = File.ReadAllText(TestRepo.Path("src/Wizard.Content/Pack/ContentDb.cs"));
            int ctor = src.IndexOf("private ContentDb(ContentPack pack)", StringComparison.Ordinal);
            Assert.True(ctor >= 0, "ContentDb 의 생성자가 없다");
            int end = src.IndexOf("public static ContentDb Build(ContentPack pack)", ctor, StringComparison.Ordinal);
            Assert.True(end > ctor);
            string body = src.Substring(ctor, end - ctor);
            int assign = body.IndexOf("_pack = pack;", StringComparison.Ordinal);
            int load = body.IndexOf("LoadSections();", StringComparison.Ordinal);
            Assert.True(assign >= 0 && load > assign, "생성자가 _pack 을 넣은 뒤 LoadSections() 를 불러야 한다");
        }

        // CONTRACTS 2.3 의 접근자마다: (접근자, 조회 훅, 절, 규칙 쪽인가)
        public static readonly (string Accessor, string Hook, ContentSection Section, bool Rules)[] FindHooks =
        {
            ("Character", "FindCharacter", ContentSection.Characters, false),
            ("Block", "FindBlock", ContentSection.Blocks, false),
            ("Art", "FindArt", ContentSection.Arts, false),
            ("Mutation", "FindMutation", ContentSection.Mutations, false),
            ("Enemy", "FindEnemy", ContentSection.Enemies, false),
            ("Boss", "FindBoss", ContentSection.Bosses, false),
            ("WaveCurve", "FindWaveCurve", ContentSection.Waves, false),
            ("Frames", "FindFrames", ContentSection.Frames, false),
            ("Modifiers", "FindModifiers", ContentSection.Modifiers, false),
            ("Scoring", "FindScoring", ContentSection.Scoring, false),
            ("Arena", "FindArena", ContentSection.Arena, false),
            ("DuelRules", "FindDuelRules", ContentSection.DuelRules, false),
            ("Tries", "FindTries", ContentSection.Tries, false),
            ("Elite", "FindElite", ContentSection.Elites, false),
            ("Starfall", "FindStarfall", ContentSection.Starfall, false),
            ("WeatherEvents", "FindWeatherEvents", ContentSection.WeatherEvents, false),
            ("BossRotation", "FindBossRotation", ContentSection.BossRotation, false),
            ("TimeOfDay", "FindTimeOfDay", ContentSection.Tod, false),
            ("ElementRules", "FindElementRules", ContentSection.Elements, false),
            ("SimLimits", "FindSimLimits", ContentSection.SimLimits, false),
            ("Enhance", "FindEnhance", ContentSection.Enhance, true),
            ("Economy", "FindEconomy", ContentSection.Economy, true),
            ("Drops", "FindDrops", ContentSection.Drops, true),
            ("Leagues", "FindLeagues", ContentSection.Leagues, true),
            ("Season", "FindSeason", ContentSection.Season, true),
            ("Shop", "FindShop", ContentSection.Shop, true),
            ("Bag", "FindBag", ContentSection.Bag, true),
            ("RunLimits", "FindRunLimits", ContentSection.RunLimits, true),
            ("Titles", "FindTitles", ContentSection.Titles, true),
            ("Cosmetics", "FindCosmetics", ContentSection.Cosmetics, true),
            ("Plaza", "FindPlaza", ContentSection.Plaza, true),
            ("Net", "FindNet", ContentSection.Net, true),
            ("Item", "FindItem", ContentSection.Items, true),
        };

        // T-47: 접근자는 자기 조회 훅에 묻고 Require 로 답한다. 훅의 선언은 전투 쪽이 Pack/Core/ContentDb.Core.cs(해시 안),
        // 규칙 쪽이 Rules/ContentDb.Rules.cs(해시 밖)에 하나씩 있다. 구현이 없는 부분 메서드는 메타데이터에 남지 않으므로 소스에서 본다.
        [Fact]
        public void EveryAccessor_AsksItsFindHook()
        {
            string core = File.ReadAllText(TestRepo.Path("src/Wizard.Content/Pack/Core/ContentDb.Core.cs"));
            string combat = File.ReadAllText(TestRepo.Path("src/Wizard.Content/Pack/ContentDb.cs"));
            string rules = File.ReadAllText(TestRepo.Path("src/Wizard.Content/Rules/ContentDb.Rules.cs"));
            var accessors = Accessors().Select(a => (string)a[0]).Concat(Properties().Select(p => (string)p[0])).OrderBy(n => n, StringComparer.Ordinal);
            Assert.Equal(accessors.ToArray(), FindHooks.Select(h => h.Accessor).OrderBy(n => n, StringComparer.Ordinal).ToArray());
            foreach (var (accessor, hook, section, isRules) in FindHooks)
            {
                Assert.Equal(isRules, ContentSections.IsRules(section));
                string decl = "partial void " + hook + "(";
                Assert.Equal(isRules ? 0 : 1, Count(core, decl));
                Assert.Equal(isRules ? 1 : 0, Count(rules, decl));
                Assert.Equal(0, Count(combat, decl));

                // 접근자의 몸통: 선언에서 다음 접근자(또는 훅 선언) 앞까지에 훅 호출과 그 절의 Require 가 차례로 있다
                string src = isRules ? rules : combat;
                int at = AccessorStart(src, accessor);
                Assert.True(at >= 0, accessor + " 접근자가 없다");
                int call = src.IndexOf(hook + "(", at, StringComparison.Ordinal);
                int require = src.IndexOf(", ContentSection." + section + ");", at, StringComparison.Ordinal);
                int next = src.IndexOf("        public ", at + 1, StringComparison.Ordinal);
                if (next < 0)
                {
                    next = src.Length;
                }
                Assert.True(call > at && call < next, accessor + " 가 " + hook + " 를 부르지 않는다");
                Assert.True(require > call && require < next, accessor + " 가 ContentSection." + section + " 로 답하지 않는다");
                Assert.Contains("return Require(", src.Substring(call, require - call));
            }
        }

        private static int AccessorStart(string src, string name)
        {
            foreach (string tail in new[] { " " + name + "(", " " + name + "\n", " " + name + "\r\n" })
            {
                int from = 0;
                while (true)
                {
                    int at = src.IndexOf(tail, from, StringComparison.Ordinal);
                    if (at < 0)
                    {
                        break;
                    }
                    int line = src.LastIndexOf('\n', at) + 1;
                    if (src.Substring(line, at - line).TrimStart().StartsWith("public ", StringComparison.Ordinal))
                    {
                        return line;
                    }
                    from = at + 1;
                }
            }
            return -1;
        }

        private static int Count(string src, string what)
        {
            int n = 0;
            for (int at = src.IndexOf(what, StringComparison.Ordinal); at >= 0; at = src.IndexOf(what, at + what.Length, StringComparison.Ordinal))
            {
                n++;
            }
            return n;
        }

        // CONTRACTS 2.3 의 접근자(이름, 매개변수, 반환형)
        public static IEnumerable<object[]> Accessors()
        {
            yield return new object[] { "Character", typeof(CharacterDef), new[] { typeof(CharacterId) } };
            yield return new object[] { "Block", typeof(BlockDef), new[] { typeof(BlockId) } };
            yield return new object[] { "Art", typeof(ArtDef), new[] { typeof(ArtId) } };
            yield return new object[] { "Mutation", typeof(MutationDef), new[] { typeof(MutationId) } };
            yield return new object[] { "Enemy", typeof(EnemyDef), new[] { typeof(EnemyTypeId) } };
            yield return new object[] { "Boss", typeof(BossDef), new[] { typeof(BossId) } };
            yield return new object[] { "Arena", typeof(ArenaDef), new[] { typeof(SimMode) } };
            yield return new object[] { "Tries", typeof(TrieDef), new[] { typeof(CharacterId) } };
            yield return new object[] { "Elite", typeof(EliteDef), new[] { typeof(EnemyTypeId) } };
            yield return new object[] { "TimeOfDay", typeof(TimeOfDayDef), new[] { typeof(TimeOfDay) } };
            yield return new object[] { "Item", typeof(Wizard.Content.Rules.ItemDef), new[] { typeof(ItemCode) } };
        }

        public static IEnumerable<object[]> Properties()
        {
            yield return new object[] { "WaveCurve", typeof(WaveCurveDef) };
            yield return new object[] { "Frames", typeof(IReadOnlyList<FrameDef>) };
            yield return new object[] { "Modifiers", typeof(IReadOnlyList<ModifierDef>) };
            yield return new object[] { "Scoring", typeof(ScoringDef) };
            yield return new object[] { "DuelRules", typeof(DuelRuleSet) };
            yield return new object[] { "Starfall", typeof(StarfallDef) };
            yield return new object[] { "WeatherEvents", typeof(IReadOnlyList<WeatherEventDef>) };
            yield return new object[] { "BossRotation", typeof(BossRotationDef) };
            yield return new object[] { "ElementRules", typeof(ElementDef) };
            yield return new object[] { "SimLimits", typeof(SimLimitsDef) };
            yield return new object[] { "Enhance", typeof(Wizard.Content.Rules.EnhanceTable) };
            yield return new object[] { "Economy", typeof(Wizard.Content.Rules.EconomyTable) };
            yield return new object[] { "Drops", typeof(Wizard.Content.Rules.DropTable) };
            yield return new object[] { "Leagues", typeof(Wizard.Content.Rules.LeagueTable) };
            yield return new object[] { "Season", typeof(Wizard.Content.Rules.SeasonTable) };
            yield return new object[] { "Shop", typeof(Wizard.Content.Rules.ShopCatalog) };
            yield return new object[] { "Bag", typeof(Wizard.Content.Rules.BagTable) };
            yield return new object[] { "RunLimits", typeof(Wizard.Content.Rules.RunLimitsTable) };
            yield return new object[] { "Titles", typeof(IReadOnlyList<Wizard.Content.Rules.TitleDef>) };
            yield return new object[] { "Cosmetics", typeof(IReadOnlyList<Wizard.Content.Rules.CosmeticDef>) };
            yield return new object[] { "Plaza", typeof(Wizard.Content.Rules.PlazaDef) };
            yield return new object[] { "Net", typeof(Wizard.Content.Rules.NetRuleSet) };
        }

        private static ContentDb EmptyDb() =>
            ContentDb.Build(ContentPack.Load(PackWriter.Write(PackWriter.SchemaVersion, 1, new byte[16], new List<PackSection>())));

        [Theory]
        [MemberData(nameof(Accessors))]
        public void Accessor_DeclaredAndThrowsContentExceptionWhenUnread(string name, Type returns, Type[] parameters)
        {
            MethodInfo? m = typeof(ContentDb).GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, parameters, null);
            Assert.NotNull(m);
            Assert.Equal(returns, m!.ReturnType);
            object arg = Activator.CreateInstance(parameters[0])!;
            var e = Assert.Throws<TargetInvocationException>(() => m.Invoke(EmptyDb(), new[] { arg }));
            Assert.IsType<ContentException>(e.InnerException);
        }

        [Theory]
        [MemberData(nameof(Properties))]
        public void Property_DeclaredAndThrowsContentExceptionWhenUnread(string name, Type type)
        {
            PropertyInfo? p = typeof(ContentDb).GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(p);
            Assert.Equal(type, p!.PropertyType);
            var e = Assert.Throws<TargetInvocationException>(() => p.GetValue(EmptyDb()));
            Assert.IsType<ContentException>(e.InnerException);
        }

        [Fact]
        public void ContentDb_IsPartialAndImmutableEntry()
        {
            Assert.True(typeof(ContentDb).IsSealed);
            Assert.NotNull(typeof(ContentDb).GetMethod("Build", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(ContentPack) }, null));
            Assert.Throws<ArgumentNullException>(() => ContentDb.Build(null!));
        }
    }
}
