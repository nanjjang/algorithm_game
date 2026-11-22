using System.Collections.Generic;
using NUnit.Framework;
using Wizard.Solo.Screens.Camp;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.ScreensCamp
{
    /// <summary>컴파일 트리 미리보기(08 요약서 8.3절, 03 요약서 4.6·13.3·13.4절)와 미리보기의 한 길.</summary>
    public class TreePreviewTests
    {
        static float DefaultDecay => TestContent.Db.Const.DepthDecayDefault;

        static SpellScope Scope(int depth, int trigger, int owner, int instances, float mul)
        {
            var s = new SpellScope { Depth = depth, TriggerSlot = trigger, OwnerSlot = owner, InstancesTotal = instances, DepthMultiplier = mul };
            return s;
        }

        [Test]
        public void S08_TV_073__DefaultDecayTags085_072_061()
        {
            Assert.AreEqual("0.85", TreePreviewModel.ScopeMultiplierText(0.85f, 1));
            Assert.AreEqual("0.72", TreePreviewModel.ScopeMultiplierText(0.85f, 2));
            Assert.AreEqual("0.61", TreePreviewModel.ScopeMultiplierText(0.85f, 3));
            Assert.AreEqual("×0.85", TestContent.Names.Format("str.camp.tree.multiplier", "v", "0.85"));
        }

        [Test]
        public void S08_TV_074__TraitDecayTags092_085_078WithDots()
        {
            Assert.AreEqual("0.92", TreePreviewModel.ScopeMultiplierText(0.92f, 1));
            Assert.AreEqual("0.85", TreePreviewModel.ScopeMultiplierText(0.92f, 2));
            Assert.AreEqual("0.78", TreePreviewModel.ScopeMultiplierText(0.92f, 3));

            var spell = new CompiledSpell { IsValid = true, DepthDecay = 0.92f, MaxDepthNormal = 3 };
            spell.SlotBlockIds = new[] { Ids.Block.MagicBolt, Ids.Block.OnHitTrigger, Ids.Block.MagicBolt, Ids.Block.OnHitTrigger, Ids.Block.MagicBolt, Ids.Block.OnHitTrigger, Ids.Block.MagicBolt };
            spell.Scopes.Add(Scope(0, -1, 0, 1, 1f));
            spell.Scopes.Add(Scope(1, 1, 2, 1, 0.92f));
            spell.Scopes.Add(Scope(2, 3, 4, 1, 0.92f * 0.92f));
            spell.Scopes.Add(Scope(3, 5, 6, 1, 0.92f * 0.92f * 0.92f));
            var m = TreePreviewModel.Build(TestContent.Db, spell, DefaultDecay);
            Assert.AreEqual(new[] { 0, 1, 2, 3 }, new[] { m.Scopes[0].FootnoteDots, m.Scopes[1].FootnoteDots, m.Scopes[2].FootnoteDots, m.Scopes[3].FootnoteDots });
            Assert.AreEqual("0.92", m.Scopes[1].MultiplierText);
            Assert.AreEqual("0.85", m.Scopes[2].MultiplierText);
            Assert.AreEqual("0.78", m.Scopes[3].MultiplierText);

            spell.DepthDecay = DefaultDecay;
            var plain = TreePreviewModel.Build(TestContent.Db, spell, DefaultDecay);
            Assert.AreEqual(0, plain.Scopes[3].FootnoteDots, "기본 감쇠면 각주 점 없음");
        }

        [Test]
        public void S08_TV_075__EightModifiersShowSixPlusTwo()
        {
            var mods = new List<string> { Ids.Block.Pierce, Ids.Block.Frost, Ids.Block.Homing, Ids.Block.DoubleSize, Ids.Block.WallBounce, Ids.Block.Corrosion, Ids.Block.TripleHeat, Ids.Block.Pierce };
            var node = new TreeNodeRow();
            TreePreviewModel.FillModifiers(node, mods);
            Assert.AreEqual(6, node.Modifiers.Count);
            Assert.AreEqual(2, node.MoreModifiers);
            Assert.AreEqual("+2", TestContent.Names.Format("str.format.more_count", "n", node.MoreModifiers));

            // 컴파일 결과에서: 한 스코프에 변형 8개 → 주인 마디 꼬리 6 + 2.
            var slots = new List<string> { Ids.Block.MagicBolt };
            slots.AddRange(mods);
            var spell = new CompiledSpell { IsValid = true, DepthDecay = DefaultDecay, MaxDepthNormal = 0, SlotBlockIds = slots.ToArray() };
            spell.Scopes.Add(Scope(0, -1, 0, 1, 1f));
            var m = TreePreviewModel.Build(TestContent.Db, spell, DefaultDecay);
            Assert.AreEqual(1, m.Scopes[0].Nodes.Count);
            Assert.AreEqual(TreeNodeShape.ScopeOwner, m.Scopes[0].Nodes[0].Shape);
            Assert.AreEqual(6, m.Scopes[0].Nodes[0].Modifiers.Count);
            Assert.AreEqual(2, m.Scopes[0].Nodes[0].MoreModifiers);
        }

        [Test]
        public void S08_TV_076__SeventyProjectilesOmitDeepestScopes_FooterAtMost64()
        {
            var spell = new CompiledSpell { IsValid = true, DepthDecay = DefaultDecay, MaxDepthNormal = 1, InstancesTotal = 70 };
            spell.SlotBlockIds = new[] { Ids.Block.MagicBolt, Ids.Block.SplitThree, Ids.Block.OnHitTrigger, Ids.Block.MagicBolt, Ids.Block.OnHitTrigger, Ids.Block.MagicBolt };
            spell.Scopes.Add(Scope(0, -1, 0, 4, 1f));
            spell.Scopes.Add(Scope(1, 2, 3, 16, 0.85f));
            spell.Scopes.Add(Scope(2, 4, 5, 50, 0.7225f));
            spell.Diagnostics.Add(new SpellDiagnostic { Code = SpellDiagCode.W_PROJECTILE_CAP, Slot = 2 });
            var m = TreePreviewModel.Build(TestContent.Db, spell, DefaultDecay);
            Assert.IsFalse(m.Scopes[1].Omitted);
            Assert.IsTrue(m.Scopes[2].Omitted, "가장 깊은 스코프부터 생략");
            Assert.IsTrue(m.Scopes[2].Nodes.TrueForAll(n => n.Omitted), "생략 스코프의 마디는 점선");
            Assert.AreEqual(20, m.ProjectilesPerShot);
            Assert.LessOrEqual(m.ProjectilesPerShot, TestContent.Db.Const.TreeShotCap);
            Assert.AreEqual("생략", TestContent.Names.Get("str.camp.tree.omitted"));
            Assert.AreEqual(3, m.Scopes[0].Nodes[1].ForkWays, "3갈래 분열 마디");
        }

        [Test]
        public void S08_TV_077__FiveWarningsShowThreeLines()
        {
            var spell = new CompiledSpell { DepthDecay = DefaultDecay, SlotBlockIds = new string[0] };
            spell.Diagnostics.Add(new SpellDiagnostic { Code = SpellDiagCode.W_TRIGGER_NO_PROJECTILE, Slot = 1 });
            spell.Diagnostics.Add(new SpellDiagnostic { Code = SpellDiagCode.W_NO_EFFECT, Slot = 2 });
            spell.Diagnostics.Add(new SpellDiagnostic { Code = SpellDiagCode.W_SPLIT_DUPLICATE, Slot = 3 });
            spell.Diagnostics.Add(new SpellDiagnostic { Code = SpellDiagCode.W_TRIGGER_DEPTH, Slot = 4 });
            spell.Diagnostics.Add(new SpellDiagnostic { Code = SpellDiagCode.W_PROJECTILE_CAP, Slot = 2 });
            var m = TreePreviewModel.Build(TestContent.Db, spell, DefaultDecay);
            Assert.AreEqual(3, m.WarningKeys.Count);
            CollectionAssert.AreEqual(new[] { "warn.trigger_no_projectile", "warn.no_effect", "warn.split_duplicate" }, m.WarningKeys, "정렬 순서 앞 3개");
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, m.WarningSlots);
        }

        [Test]
        public void S03_4_6__DiagnosticsMapToNameKeys()
        {
            var expect = new Dictionary<SpellDiagCode, string>
            {
                { SpellDiagCode.E_CORE_EMPTY, "warn.core_empty" },
                { SpellDiagCode.E_CORE_NOT_PROJECTILE, "warn.core_not_projectile" },
                { SpellDiagCode.W_TRIGGER_NO_PROJECTILE, "warn.trigger_no_projectile" },
                { SpellDiagCode.W_NO_EFFECT, "warn.no_effect" },
                { SpellDiagCode.W_SPLIT_DUPLICATE, "warn.split_duplicate" },
                { SpellDiagCode.W_TRIGGER_DEPTH, "warn.trigger_depth" },
                { SpellDiagCode.W_PROJECTILE_CAP, "warn.projectile_cap" },
            };
            foreach (var kv in expect)
            {
                var spell = new CompiledSpell { SlotBlockIds = new string[0] };
                spell.Diagnostics.Add(new SpellDiagnostic { Code = kv.Key });
                var m = TreePreviewModel.Build(TestContent.Db, spell, DefaultDecay);
                CollectionAssert.AreEqual(new[] { kv.Value }, m.WarningKeys, kv.Key.ToString());
                Assert.IsTrue(TestContent.Names.Has(kv.Value), kv.Value);
            }
            var dev = new CompiledSpell { SlotBlockIds = new string[0] };
            dev.Diagnostics.Add(new SpellDiagnostic { Code = SpellDiagCode.E_SLOT_COUNT_MISMATCH });
            var md = TreePreviewModel.Build(TestContent.Db, dev, DefaultDecay);
            Assert.AreEqual(0, md.WarningKeys.Count, "개발용 코드는 문구 없음");
            Assert.IsTrue(md.HasError);
            Assert.AreEqual("코어에 투사체 블록을 끼워야 출발할 수 있다", TestContent.Names.Get("warn.core_empty"));
        }

        [Test]
        public void S03_13_4__ExampleSplit3TriggerFireball_Footer6Projectiles268Heat()
        {
            // core=magic_bolt; links=split_3,on_hit_trigger,fireball,_ → "발사 1회 = 투사체 6개 · 열 26.8", S1 깊이 1 ×0.85.
            var rules = SimModuleSet.CreateDefault();
            var ch = TestContent.Db.Character(Ids.Char.C1);
            var links = new List<string> { Ids.Block.SplitThree, Ids.Block.OnHitTrigger, Ids.Block.Fireball, null };
            var r = SpellPreview.ForWand(rules, TestContent.Db, ch, 0, new List<string>(), 4, Ids.Block.MagicBolt, links);
            var m = TreePreviewModel.Build(TestContent.Db, r.Spell, DefaultDecay);
            Assert.AreEqual(6, m.ProjectilesPerShot);
            Assert.AreEqual("26.8", CampFormat.OneDecimal(m.HeatPerShot));
            Assert.AreEqual("발사 1회 = 투사체 6개 · 열 26.8",
                TestContent.Names.Format("str.camp.tree.footer", "n", m.ProjectilesPerShot, "heat", CampFormat.OneDecimal(m.HeatPerShot)));
            Assert.AreEqual(2, m.Scopes.Count);
            Assert.AreEqual("0.85", m.Scopes[1].MultiplierText);
            Assert.AreEqual(Ids.Block.Fireball, m.Scopes[1].Nodes.Find(n => n.Shape != TreeNodeShape.Control).BlockId);
        }

        [Test]
        public void S00_G21_C10__PreviewGoesThroughComputeDerivedAndCompileContext()
        {
            var traits = new CampRecordingTraits();
            var spells = new CampRecordingSpells { Stats = new SpellStats { CritShots = 0, HeatPerShot = 4f } };
            var rules = SimModuleSet.Stubs();
            rules.Traits = traits;
            rules.Spells = spells;
            var ch = TestContent.Db.Character(Ids.Char.C3);
            var r = SpellPreview.ForWand(rules, TestContent.Db, ch, 7, new List<string> { Ids.Mutation.TwistedBarrel }, 6, Ids.Block.MagicBolt, new List<string> { null, null, null, null, null, null });
            Assert.AreEqual(1, traits.ComputeCalls, "판 밖: Traits.ComputeDerived");
            Assert.AreEqual(7, traits.LastStage);
            Assert.AreSame(ch, traits.LastChar);
            Assert.AreEqual(1, traits.ContextCalls, "Traits.CompileContext");
            Assert.AreEqual(6, traits.LastLinks);
            Assert.AreEqual(1, spells.CompileCalls);
            Assert.AreEqual(0.25f, spells.LastCtx.DepthDecay, 1e-6f, "컴파일 문맥은 CompileContext가 만든 것 그대로");
            Assert.IsTrue(spells.LastCtx.Underline);
            Assert.AreEqual(1, spells.StatsCalls);
            Assert.AreEqual(0.5f, spells.LastDerived.DepthDecay, 1e-6f, "수치도 같은 derived");
            Assert.IsTrue(CampRules.CritZeroWarning(r.Stats));

            // 판 중: p.Derived를 쓰고 ComputeDerived는 부르지 않는다.
            var w = CampTestKit.World(rules);
            w.P0.Derived = new PlayerDerived { DepthDecay = 0.8f };
            SpellPreview.ForPlayer(rules, TestContent.Db, w.P0);
            Assert.AreEqual(1, traits.ComputeCalls);
            Assert.AreEqual(2, traits.ContextCalls);
            Assert.AreEqual(4, traits.LastLinks, "판 중 조립의 칸 수");
            Assert.AreEqual(0.4f, spells.LastCtx.DepthDecay, 1e-6f);
            Assert.AreEqual(Ids.Block.MagicBolt, spells.LastCore);
        }
    }
}
