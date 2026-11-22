using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>피해 공식·상태·계약 벡터. 03 요약서 9·10절(S02 16.8·16.9), 00 문서 5.4절 (나), D-54.</summary>
    public class SpellDamageTests
    {
        SpellWorld _k;

        [SetUp]
        public void SetUp() { _k = new SpellWorld(); }

        static float Damage(EnemyState e) => e.MaxHp - e.Hp;

        /// <summary>적 하나를 앞에 두고 한 발 쏴서 그 적이 받은 피해를 돌려준다.</summary>
        float OneHit(CompiledSpell s, System.Action<EnemyState> setupEnemy = null, System.Func<ExecuteRequest, ExecuteRequest> edit = null)
        {
            var e = _k.AddEnemy(4f, 7f);
            setupEnemy?.Invoke(e);
            var req = _k.Request(s);
            if (edit != null) req = edit(req);
            _k.Sys.Execute(_k.W, req);
            _k.Step(12);
            return Damage(e);
        }

        CompiledSpell Bolt(params string[] links)
        {
            var four = new string[4];
            for (int i = 0; i < links.Length; i++) four[i] = links[i];
            return _k.Compile(B.Mb, four);
        }

        // ------------------------------------------------------------ 피해 검산 (S02 16.8)

        [Test]
        public void S02_H01__MagicBolt10()
        {
            Assert.AreEqual(10f, OneHit(Bolt()), 1e-3f);
        }

        [Test]
        public void S02_H02__Plus13Is61_528()
        {
            // 강화 배율 1.15^13 ≈ 6.152788 (03 요약서 9절 표)
            _k.P.Derived.EnhanceMul = 6.152788f;
            Assert.AreEqual(61.528f, OneHit(Bolt()), 1e-3f);
        }

        [Test]
        public void S02_H03__Crit20()
        {
            Assert.AreEqual(20f, OneHit(Bolt(), null, r => { r.IsCrit = true; return r; }), 1e-3f);
        }

        [Test]
        public void S02_H04__CritWithTripleHeat30()
        {
            Assert.AreEqual(30f, OneHit(Bolt(B.Triple), null, r => { r.IsCrit = true; return r; }), 1e-3f);
        }

        [Test]
        public void S02_H05__CritWithGlassWand30()
        {
            _k.P.Derived.CritBonus = 1;
            Assert.AreEqual(30f, OneHit(Bolt(), null, r => { r.IsCrit = true; return r; }), 1e-3f);
        }

        [Test]
        public void S02_H06__CritTripleHeatGlassWand40()
        {
            _k.P.Derived.CritBonus = 1;
            Assert.AreEqual(40f, OneHit(Bolt(B.Triple), null, r => { r.IsCrit = true; return r; }), 1e-3f);
        }

        [Test]
        public void S02_H04b__TripleHeatIsPerScope()
        {
            // 과열 3배는 스코프 단위(U4): 루트의 과열 3배는 하위 스코프 화염구에 닿지 않는다 → 40 × 2 × 0.85 = 68.
            var near = _k.AddEnemy(3f, 7f);
            var far = _k.AddEnemy(8f, 7f);
            var s = _k.Compile(B.Mb, B.Triple, B.Trig, B.Fb, B.None);
            var req = _k.Request(s);
            req.IsCrit = true;
            _k.Sys.Execute(_k.W, req);
            _k.Step(60);
            Assert.AreEqual(30f, Damage(near), 1e-3f);
            Assert.AreEqual(68f, Damage(far), 1e-3f);
        }

        [Test]
        public void S02_H08__ManaDischargeHasNoCrit20()
        {
            // 열이 임계 구간이어도 비기는 임계가 없다(비기 배율 2만).
            float d = OneHit(Bolt(), null, r => { r.IsCrit = true; r.Source = ShotSource.ManaDischarge; r.DamageMul = 2f; r.HeatExempt = true; return r; });
            Assert.AreEqual(20f, d, 1e-3f);
            Assert.IsFalse(_k.Player.Hits[0].IsCrit);
            Assert.AreEqual(ShotSource.ManaDischarge, _k.Player.Hits[0].ShotSource);
        }

        [Test]
        public void S02_H08b__CopyCircleHalfDamageNoCrit()
        {
            float d = OneHit(Bolt(), null, r => { r.IsCrit = true; r.Source = ShotSource.CopyCircle; r.DamageMul = 0.5f; r.HeatExempt = true; return r; });
            Assert.AreEqual(5f, d, 1e-3f);
        }

        [Test]
        public void S02_H09__Weak12_5Resist8()
        {
            Assert.AreEqual(12.5f, OneHit(Bolt(), e => e.Weak = Ids.Element.Arcane), 1e-3f);
            var k2 = _k = new SpellWorld();
            Assert.AreEqual(8f, OneHit(Bolt(), e => e.Resist = Ids.Element.Arcane), 1e-3f);
            Assert.AreSame(k2, _k);
        }

        // 05 요약서 3.3절: 슬라임(약점 화염, 저항 냉기) — 데이터의 상성으로
        [Test]
        public void S05_Affinity__SlimeFromDataWeakFireResistIce()
        {
            EnemyDef slime = _k.Db.Enemy(Ids.Enemy.SlimeMoss);
            Assert.AreEqual(Ids.Element.Fire, slime.Weak);
            Assert.AreEqual(Ids.Element.Ice, slime.Resist);
            var e = _k.AddEnemy(4f, 7f);
            e.Weak = slime.Weak;
            e.Resist = slime.Resist;
            _k.Fire(_k.Compile(B.Ice, B.None, B.None, B.None, B.None));
            _k.Step(12);
            Assert.AreEqual(14f * 0.8f, Damage(e), 1e-3f);
            float before = Damage(e);
            _k.Fire(_k.Compile(B.Fb, B.None, B.None, B.None, B.None));
            _k.Step(20);
            Assert.AreEqual(40f * 1.25f, Damage(e) - before, 1e-3f);
        }

        // TV-H10 / H11: 잠식 3발 10, 11.5, 11.5 (첫 발에는 미적용, 겹치지 않음), 180틱 뒤 만료
        [Test]
        public void S02_H10_H11__CorrosionAppliesFromNextHitAndExpires()
        {
            var e = _k.AddEnemy(2.6f, 7f);
            var s = Bolt(B.Corr);
            float[] expected = { 10f, 11.5f, 11.5f };
            int lastHitTick = 0;
            for (int i = 0; i < 3; i++)
            {
                float before = Damage(e);
                _k.Fire(s);
                _k.Step(2);
                lastHitTick = _k.W.Tick - 1;
                Assert.AreEqual(expected[i], Damage(e) - before, 1e-3f, (i + 1) + "발째");
            }
            Assert.AreEqual(3, _k.Enemies.Corrosions.Count);
            Assert.AreEqual(lastHitTick + 180, e.CorrosionUntilTick);

            // 잠식을 걸지 않는 탄으로 만료를 본다: 179틱 뒤 적중은 아직 ×1.15, 180틱 뒤 적중은 10.
            var plain = Bolt();
            _k.Step(lastHitTick + 179 - _k.W.Tick - 1);
            float b1 = Damage(e);
            _k.Fire(plain);
            _k.Step(2);
            Assert.AreEqual(lastHitTick + 179, _k.W.Tick - 1);
            Assert.AreEqual(11.5f, Damage(e) - b1, 1e-3f);

            var k2 = new SpellWorld();
            var e2 = k2.AddEnemy(2.6f, 7f);
            e2.CorrosionUntilTick = 181;
            k2.Step(179);
            k2.Fire(k2.Compile(B.Mb, B.None, B.None, B.None, B.None));
            k2.Step(2); // 적중 틱 180: 아직 유효(t < 181)
            Assert.AreEqual(11.5f, Damage(e2), 1e-3f);
            k2.Fire(k2.Compile(B.Mb, B.None, B.None, B.None, B.None));
            k2.Step(2); // 적중 틱 182: 만료
            Assert.AreEqual(21.5f, Damage(e2), 1e-3f);
        }

        // TV-H12: 잠식 걸린 적에게 비기·패시브 피해 ×1.15
        [Test]
        public void S02_H12__CorrosionAppliesToDirectDamage()
        {
            var e = _k.AddEnemy(10f, 7f);
            e.CorrosionUntilTick = 100;
            var r = _k.Sys.DealDirect(_k.W, new DirectDamage
            {
                AttackerSlot = 0, TargetEnemyId = e.Id, Amount = 20f, Element = Ids.Element.Dark, Origin = DamageOrigin.Art, SourceId = Ids.Art.ShadowHand
            });
            Assert.AreEqual(23f, r.Applied, 1e-3f);
            Assert.AreEqual(23f, Damage(e), 1e-3f);
            var ev = _k.EventsOf(SimEventType.EnemyDamaged)[0];
            Assert.AreEqual(23, ev.A);
            Assert.AreEqual(DamageFlags.Corroded | DamageFlags.Art, ev.B);
        }

        // TV-H14: 트리거 하위 화염구 깊이 1 → 34, 깊이 2 → 28.9
        [Test]
        public void S02_H14__DepthDecay34And28_9()
        {
            Assert.AreEqual(34f, ChildFireballDamage(1, 0f), 1e-3f);
            _k = new SpellWorld();
            Assert.AreEqual(28.9f, ChildFireballDamage(2, 0f), 1e-3f);
        }

        // TV-H15: 갈피(감쇠 0.92) 깊이 1 → 36.8, 깊이 2 → 33.856
        [Test]
        public void S02_H15__TraitDepthDecay36_8And33_856()
        {
            float trait = _k.Db.Const.DepthDecayTrait;
            Assert.AreEqual(36.8f, ChildFireballDamage(1, trait), 1e-3f);
            _k = new SpellWorld();
            Assert.AreEqual(33.856f, ChildFireballDamage(2, trait), 1e-3f);
        }

        /// <summary>적을 5칸 간격으로 늘어놓고, 깊이 depth의 화염구가 마지막 적에게 준 피해를 돌려준다.</summary>
        float ChildFireballDamage(int depth, float decay)
        {
            _k.P.Pos = new Vector2(1f, 7f);
            EnemyState last = null;
            for (int i = 0; i <= depth; i++) last = _k.AddEnemy(3f + 5f * i, 7f);
            var s = depth == 1
                ? _k.CompileWith(false, decay, B.Mb, B.Trig, B.Fb, B.None, B.None)
                : _k.CompileWith(false, decay, B.Mb, B.Trig, B.Mb, B.Trig, B.Fb);
            _k.Fire(s);
            _k.Step(120);
            return Damage(last);
        }

        [Test]
        public void S02_H16__SizeAndPierce13()
        {
            Assert.AreEqual(13f, OneHit(Bolt(B.Size, B.Pierce)), 1e-3f);
        }

        [Test]
        public void S02_H17__ShadowContractSevenCrystals12_1()
        {
            // 캐릭터 배율 1 + 0.03 × 7 = 1.21 은 발사 순간 값으로 넘어온다.
            Assert.AreEqual(12.1f, OneHit(Bolt(), null, r => { r.CharacterMul = 1.21f; return r; }), 1e-3f);
        }

        [Test]
        public void S02_H22__TwistedBarrel16()
        {
            _k.P.Derived.MutationDamageMul = _k.Db.Mutations.TwistedBarrelDamageMul;
            Assert.AreEqual(16f, OneHit(Bolt()), 1e-3f);
        }

        [Test]
        public void S02_H23__Plus5CritWeak50_284()
        {
            _k.P.Derived.EnhanceMul = 2.011357f;
            float d = OneHit(Bolt(), e => e.Weak = Ids.Element.Arcane, r => { r.IsCrit = true; return r; });
            Assert.AreEqual(50.284f, d, 2e-3f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void S02_U14__ChildrenInheritSnapshotOfFiringMoment(bool replaceSpellModule)
        {
            // 발사 뒤 강화 배율이 바뀌어도(유리 지팡이 임시 하락) 이미 나간 탄의 자손은 발사 순간 값을 쓴다.
            _k.P.Pos = new Vector2(1f, 7f);
            var e1 = _k.AddEnemy(3f, 7f);
            var e2 = _k.AddEnemy(8f, 7f);
            _k.P.Derived.EnhanceMul = 2f;
            _k.P.Derived.CritBonus = 1;
            var req = _k.Request(_k.Compile(B.Mb, B.Trig, B.Fb, B.None, B.None));
            req.IsCrit = true;
            req.CharacterMul = 1.5f;
            _k.Sys.Execute(_k.W, req);
            _k.P.Derived.EnhanceMul = 1f;
            _k.P.Derived.CritBonus = 0;
            if (replaceSpellModule) _k.W.Mods.Spells = new Wizard.Solo.Sim.Spells.SpellSystem();
            _k.Step(120);
            Assert.AreEqual(10f * 2f * 3f * 1.5f, Damage(e1), 1e-3f);
            Assert.AreEqual(40f * 2f * 3f * 1.5f * 0.85f, Damage(e2), 1e-2f);
        }

        // ------------------------------------------------------------ 이벤트·훅

        [Test]
        public void S02_U19__DamageNumberRoundsHalfUp()
        {
            // 12.5 → 13 (.5 올림)
            OneHit(Bolt(), e => e.Weak = Ids.Element.Arcane);
            var ev = _k.EventsOf(SimEventType.EnemyDamaged)[0];
            Assert.AreEqual(13, ev.A);
            Assert.AreEqual(DamageFlags.Weak, ev.B);
            Assert.AreEqual(Ids.Element.Arcane, ev.Id);
        }

        [Test]
        public void S02_DamageFlags__CritResistArmored()
        {
            _k.Enemies.IncomingMul = 0.5f;
            float d = OneHit(Bolt(), e => e.Resist = Ids.Element.Arcane, r => { r.IsCrit = true; return r; });
            Assert.AreEqual(10f * 2f * 0.8f * 0.5f, d, 1e-3f);
            var ev = _k.EventsOf(SimEventType.EnemyDamaged)[0];
            Assert.AreEqual(DamageFlags.Crit | DamageFlags.Resist | DamageFlags.Armored, ev.B);
        }

        [Test]
        public void S00_5_4__HooksCalledPlayerThenTraitsWithHitFacts()
        {
            var e = _k.AddEnemy(4f, 7f);
            var req = _k.Request(Bolt());
            req.IsCrit = true;
            _k.Sys.Execute(_k.W, req);
            _k.Step(12);
            Assert.AreEqual(1, _k.Player.Hits.Count);
            Assert.AreEqual(1, _k.Traits.Hits.Count);
            var h = _k.Player.Hits[0];
            Assert.AreEqual(0, h.AttackerSlot);
            Assert.AreEqual(e.Id, h.EnemyId);
            Assert.AreEqual(DamageOrigin.Projectile, h.Origin);
            Assert.AreEqual(ShotSource.Fire, h.ShotSource);
            Assert.AreEqual(req.ShotId, h.ShotId);
            Assert.IsTrue(h.IsCrit);
            Assert.AreEqual(Ids.Element.Arcane, h.Element);
            Assert.AreEqual(20f, h.Amount, 1e-3f);
            Assert.IsFalse(h.Killed);
        }

        [Test]
        public void S02_EL1__ModifierElementDoesNotChangeDamageElement()
        {
            // 서리(냉기) 변형이 붙어도 피해 속성은 맞힌 투사체(마력탄 = 비전)의 속성.
            float d = OneHit(Bolt(B.Frost), e => { e.Weak = Ids.Element.Ice; e.Resist = Ids.Element.Arcane; });
            Assert.AreEqual(8f, d, 1e-3f);
        }

        // ------------------------------------------------------------ 계약 벡터 (공통 규칙 2)

        [Test]
        public void S00_Rule2__SecondShotPassesEnemyKilledEarlierInSameTick()
        {
            var e = _k.AddEnemy(4f, 7f, 10f);
            var behind = _k.AddEnemy(5f, 7f);
            var s = _k.Compile(B.Mb, B.Pierce, B.None, B.None, B.None);
            _k.Fire(s);
            _k.Fire(s);
            var first = _k.W.Projectiles[0];
            var second = _k.W.Projectiles[1];
            int deathTick = -1;
            for (int i = 0; i < 20 && deathTick < 0; i++)
            {
                int tick = _k.W.Tick;
                _k.W.Mods.Spells.Stage9_Projectiles(_k.W);
                if (e.PendingDeath) deathTick = tick; else _k.W.Tick++;
            }
            Assert.GreaterOrEqual(deathTick, 0);
            // 첫 탄에 죽었다: EnemyDamaged 1번, KillingHit는 첫 탄의 것.
            Assert.AreEqual(1, _k.CountEvents(SimEventType.EnemyDamaged, e.Id));
            Assert.IsTrue(e.KillingHit.Killed);
            Assert.AreEqual(first.ShotId, e.KillingHit.ShotId);
            Assert.AreEqual(0f, e.Hp);
            // 첫 탄은 관통을 1번 썼고, 둘째 탄은 그 적을 지나가 관통 횟수가 그대로다.
            Assert.AreEqual(1, first.PierceLeft);
            Assert.AreEqual(2, second.PierceLeft);
            Assert.IsFalse(second.Dead);
            Assert.IsFalse(second.HitSet.Contains(e.Id));
            Assert.AreEqual(1, _k.Player.Hits.Count);

            // 뒤의 적은 두 탄 모두 맞는다.
            for (int i = 0; i < 10; i++) { _k.W.Tick++; _k.W.Mods.Spells.Stage9_Projectiles(_k.W); }
            Assert.AreEqual(2, _k.CountEvents(SimEventType.EnemyDamaged, behind.Id));
            Assert.AreEqual(1, second.PierceLeft);
            Assert.AreEqual(first.ShotId, e.KillingHit.ShotId);
        }

        [Test]
        public void S00_Rule2__DealDirectOnPendingDeathEnemyIsImmune()
        {
            var e = _k.AddEnemy(4f, 7f, 10f);
            var kill = _k.Sys.DealDirect(_k.W, new DirectDamage { AttackerSlot = 0, TargetEnemyId = e.Id, Amount = 15f, Element = Ids.Element.Fire, Origin = DamageOrigin.Passive });
            Assert.IsTrue(kill.Killed);
            Assert.IsFalse(kill.Immune);
            Assert.IsTrue(e.PendingDeath);
            var first = e.KillingHit;

            var again = _k.Sys.DealDirect(_k.W, new DirectDamage { AttackerSlot = 0, TargetEnemyId = e.Id, Amount = 99f, Element = Ids.Element.Dark, Origin = DamageOrigin.Art });
            Assert.IsTrue(again.Immune);
            Assert.IsFalse(again.Killed);
            Assert.AreEqual(0f, again.Applied);
            Assert.AreEqual(first.Element, e.KillingHit.Element);
            Assert.AreEqual(first.Amount, e.KillingHit.Amount);
            Assert.AreEqual(1, _k.CountEvents(SimEventType.EnemyDamaged, e.Id));
            Assert.AreEqual(1, _k.Player.Hits.Count);
            Assert.AreEqual(DamageFlags.Passive, _k.EventsOf(SimEventType.EnemyDamaged)[0].B);

            Assert.IsTrue(_k.Sys.DealDirect(_k.W, new DirectDamage { TargetEnemyId = 9999, Amount = 1f }).Immune);
            var burrowed = _k.AddEnemy(6f, 7f);
            burrowed.Burrowed = true;
            Assert.IsTrue(_k.Sys.DealDirect(_k.W, new DirectDamage { TargetEnemyId = burrowed.Id, Amount = 5f }).Immune);
            Assert.AreEqual(0f, Damage(burrowed));
        }

        [Test]
        public void S00_5_4__HpFloorHoldsAndDoesNotKill()
        {
            var e = _k.AddEnemy(4f, 7f, 100f);
            e.HpFloor = 50f;
            var r = _k.Sys.DealDirect(_k.W, new DirectDamage { AttackerSlot = 0, TargetEnemyId = e.Id, Amount = 80f, Element = Ids.Element.Neutral, Origin = DamageOrigin.Art });
            Assert.AreEqual(50f, e.Hp, 1e-4f);
            Assert.IsFalse(r.Killed);
            Assert.IsFalse(e.PendingDeath);
        }

        [Test]
        public void S08_Dummy__TargetWithoutHpNeverDies()
        {
            // 허수아비(practice.json base_hp 0): 죽지 않고 받은 피해만 쌓인다.
            var dummy = _k.AddEnemy(4f, 7f, 0f, 0.5f, Ids.Enemy.Dummy);
            _k.Fire(Bolt());
            _k.Step(12);
            Assert.IsFalse(dummy.PendingDeath);
            Assert.AreEqual(-10f, dummy.Hp, 1e-3f);
            Assert.AreEqual(1, _k.CountEvents(SimEventType.EnemyDamaged, dummy.Id));
        }

        // ------------------------------------------------------------ 약점·저항 알아냄 (D-54)

        [Test]
        public void S00_D54__AffinityDiscoveredOnceOnFirstWeakHit()
        {
            var e = _k.AddEnemy(2.6f, 7f, 1000f, 0.3f, Ids.Enemy.WispStray);
            e.Weak = Ids.Element.Arcane;
            var s = Bolt();
            _k.Fire(s);
            _k.Step(2);
            var found = _k.EventsOf(SimEventType.AffinityDiscovered);
            Assert.AreEqual(1, found.Count);
            Assert.AreEqual(0, found[0].Slot);
            Assert.AreEqual(e.Id, found[0].EntityId);
            Assert.AreEqual((int)Affinity.Weak, found[0].A);
            Assert.AreEqual(Ids.Element.Arcane, found[0].Id);
            Assert.AreEqual(e.Pos, found[0].Pos);
            Assert.IsTrue(_k.W.Run.AffinityKnown.Contains(AffinityKey.Of(Ids.Enemy.WispStray, Affinity.Weak)));

            // 둘째부터 없음(같은 종의 다른 개체도).
            var other = _k.AddEnemy(2.6f, 7f, 1000f, 0.3f, Ids.Enemy.WispStray);
            other.Weak = Ids.Element.Arcane;
            _k.Fire(s);
            _k.Fire(s);
            _k.Step(3);
            Assert.AreEqual(1, _k.CountEvents(SimEventType.AffinityDiscovered));
            Assert.AreEqual(1, _k.W.Run.AffinityKnown.Count);
        }

        [Test]
        public void S00_D54__ResistIsSeparateKeyAndNormalHitsDiscoverNothing()
        {
            var normal = _k.AddEnemy(2.6f, 7f);
            _k.Fire(Bolt());
            _k.Step(2);
            Assert.AreEqual(0, _k.CountEvents(SimEventType.AffinityDiscovered));
            normal.Dead = true;

            var e = _k.AddEnemy(2.6f, 7f, 1000f, 0.3f, Ids.Enemy.GolemMenhir);
            e.Weak = Ids.Element.Arcane;
            e.Resist = Ids.Element.Lightning;
            _k.Sys.DealDirect(_k.W, new DirectDamage { AttackerSlot = 0, TargetEnemyId = e.Id, Amount = 10f, Element = Ids.Element.Lightning, Origin = DamageOrigin.Passive });
            _k.Sys.DealDirect(_k.W, new DirectDamage { AttackerSlot = 0, TargetEnemyId = e.Id, Amount = 10f, Element = Ids.Element.Arcane, Origin = DamageOrigin.Art });
            var found = _k.EventsOf(SimEventType.AffinityDiscovered);
            Assert.AreEqual(2, found.Count);
            Assert.AreEqual((int)Affinity.Resist, found[0].A);
            Assert.AreEqual((int)Affinity.Weak, found[1].A);
            Assert.IsTrue(_k.W.Run.AffinityKnown.Contains(AffinityKey.Of(Ids.Enemy.GolemMenhir, Affinity.Resist)));
            Assert.IsTrue(_k.W.Run.AffinityKnown.Contains(AffinityKey.Of(Ids.Enemy.GolemMenhir, Affinity.Weak)));
        }

        [Test]
        public void S00_D54__AlreadyKnownFromRunConfigEmitsNothing()
        {
            // Run이 판 시작 때 RunConfig.KnownAffinities를 RunState.AffinityKnown으로 복사해 둔다.
            var cfg = new RunConfig();
            cfg.KnownAffinities.Add(AffinityKey.Of(Ids.Enemy.SlimeMoss, Affinity.Weak));
            foreach (var key in cfg.KnownAffinities) _k.W.Run.AffinityKnown.Add(key);

            var e = _k.AddEnemy(4f, 7f);
            e.Weak = Ids.Element.Arcane;
            _k.Fire(Bolt());
            _k.Step(12);
            Assert.AreEqual(12.5f, Damage(e), 1e-3f);
            Assert.AreEqual(0, _k.CountEvents(SimEventType.AffinityDiscovered));
            Assert.AreEqual(1, _k.W.Run.AffinityKnown.Count);
        }

        [Test]
        public void S02_AF__NeutralElementHasNoAffinity()
        {
            var e = _k.AddEnemy(4f, 7f, 1000f, 0.3f, Ids.Enemy.SporeSeedling);
            e.Weak = Ids.Element.Neutral;
            e.Resist = Ids.Element.Neutral;
            var r = _k.Sys.DealDirect(_k.W, new DirectDamage { AttackerSlot = 0, TargetEnemyId = e.Id, Amount = 10f, Element = Ids.Element.Neutral, Origin = DamageOrigin.Environment });
            Assert.AreEqual(Affinity.Normal, r.Affinity);
            Assert.AreEqual(10f, r.Applied, 1e-4f);
            Assert.AreEqual(0, _k.CountEvents(SimEventType.AffinityDiscovered));
        }

        // ------------------------------------------------------------ 상태 (10절, S02 16.9)

        // TV-I01: 얼음 송곳(−30%, 60틱) 뒤 30틱에 서리(−20%, 60틱) → 0~59틱 −30%, 60~89틱 −20%, 90틱부터 0
        [Test]
        public void S02_TV_I01__SlowsDoNotStackLargestWins()
        {
            var e = _k.AddEnemy(2.6f, 7f);
            _k.Fire(_k.Compile(B.Ice, B.None, B.None, B.None, B.None));
            _k.Step(2);
            int t0 = _k.W.Tick - 1; // 얼음 송곳이 맞은 틱
            Assert.AreEqual(1, _k.Enemies.Slows.Count);
            Assert.AreEqual(SlowSlot.IceSpike, _k.Enemies.Slows[0].Slot);
            Assert.AreEqual(0.3f, _k.Enemies.Slows[0].Strength, 1e-5f);
            Assert.AreEqual(60, _k.Enemies.Slows[0].Ticks);
            Assert.AreEqual(CcSource.BlockSlow, _k.Enemies.Slows[0].Source);

            // 서리 붙은 마력탄이 t0 + 30에 맞도록: 발사는 그 한 틱 전.
            _k.Step(t0 + 29 - _k.W.Tick);
            _k.Fire(_k.Compile(B.Mb, B.Frost, B.None, B.None, B.None));
            _k.Step(2);
            Assert.AreEqual(t0 + 30, _k.W.Tick - 1);
            Assert.AreEqual(2, _k.Enemies.Slows.Count);
            Assert.AreEqual(SlowSlot.Frost, _k.Enemies.Slows[1].Slot);
            Assert.AreEqual(0.2f, _k.Enemies.Slows[1].Strength, 1e-5f);
            Assert.AreEqual(60, _k.Enemies.Slows[1].Ticks);

            Assert.AreEqual(0.3f, e.SlowStrength(t0), 1e-5f);
            Assert.AreEqual(0.3f, e.SlowStrength(t0 + 59), 1e-5f);
            Assert.AreEqual(0.2f, e.SlowStrength(t0 + 60), 1e-5f);
            Assert.AreEqual(0.2f, e.SlowStrength(t0 + 89), 1e-5f);
            Assert.AreEqual(0f, e.SlowStrength(t0 + 90), 1e-5f);
        }

        // TV-I02: 얼음 송곳 두 번(30틱 간격) → −30% 하나, 남은 시간만 60으로 갱신(합 90틱)
        [Test]
        public void S02_I02__SameSourceRefreshesDuration()
        {
            var e = _k.AddEnemy(2.6f, 7f);
            var s = _k.Compile(B.Ice, B.None, B.None, B.None, B.None);
            _k.Fire(s);
            _k.Step(2);
            int t0 = _k.W.Tick - 1;
            _k.Step(t0 + 29 - _k.W.Tick);
            _k.Fire(s);
            _k.Step(2);
            Assert.AreEqual(0.3f, e.SlowStrength(t0 + 89), 1e-5f);
            Assert.AreEqual(0f, e.SlowStrength(t0 + 90), 1e-5f);
        }

        // TV-I12: 폭발 + 서리, 범위 안 적 3 → 3체 모두 −20%
        [Test]
        public void S02_I12__StatusAppliesToEveryExplosionTarget()
        {
            _k.AddEnemy(6f, 7f);
            _k.AddEnemy(6f, 6f);
            _k.AddEnemy(6f, 8f);
            _k.Fire(_k.Compile(B.Fb, B.Frost, B.Corr, B.None, B.None));
            _k.Step(40);
            Assert.AreEqual(3, _k.Enemies.Slows.Count);
            foreach (var c in _k.Enemies.Slows)
            {
                Assert.AreEqual(SlowSlot.Frost, c.Slot);
                Assert.AreEqual(0.2f, c.Strength, 1e-5f);
            }
            Assert.AreEqual(3, _k.Enemies.Corrosions.Count);
        }

        [Test]
        public void S04_Recoil__HitEffectKnocksBackAlongShotAndCancelsDash()
        {
            var e = _k.AddEnemy(4f, 7f);
            var req = _k.Request(Bolt());
            req.Source = ShotSource.ArtOther;
            req.HitEffect = ShotHitEffect.RecoilKnockback;
            _k.Sys.Execute(_k.W, req);
            _k.Step(12);
            Assert.AreEqual(1, _k.Enemies.Ccs.Count);
            var cc = _k.Enemies.Ccs[0];
            Assert.AreEqual(e.Id, cc.EnemyId);
            Assert.AreEqual(CcKind.Knockback, cc.Kind);
            Assert.AreEqual(CcSource.Recoil, cc.Source);
            // arts.json art.recoil: 넉백 2칸, 경직 0.8초(48틱)
            Assert.AreEqual(2f, cc.Amount, 1e-5f);
            Assert.AreEqual(1f, cc.Dir.x, 1e-4f);
            Assert.AreEqual(1, _k.Enemies.DashCancels.Count);
            Assert.AreEqual(48, _k.Enemies.DashCancels[0]);
            Assert.AreEqual(0, _k.CountEvents(SimEventType.EnemyDashCancelled), "돌진 예고 중이 아니면 끊음 표시 없음");
        }

        [Test]
        public void S04_Recoil__CancelledDashEmitsEventForDisplay()
        {
            var e = _k.AddEnemy(4f, 7f);
            _k.Enemies.CancelDashResult = true;
            var req = _k.Request(Bolt());
            req.Source = ShotSource.ArtOther;
            req.HitEffect = ShotHitEffect.RecoilKnockback;
            _k.Sys.Execute(_k.W, req);
            _k.Step(12);
            var evs = _k.EventsOf(SimEventType.EnemyDashCancelled, e.Id);
            Assert.AreEqual(1, evs.Count);
            Assert.AreEqual(0, evs[0].Slot);
        }

        [Test]
        public void S02_Status__NotAppliedToKilledEnemy()
        {
            _k.AddEnemy(4f, 7f, 5f);
            _k.Fire(_k.Compile(B.Ice, B.None, B.None, B.None, B.None));
            _k.Step(12);
            Assert.AreEqual(0, _k.Enemies.Slows.Count);
            Assert.AreEqual(1, _k.Player.Hits.Count);
            Assert.IsTrue(_k.Player.Hits[0].Killed);
        }
    }
}
