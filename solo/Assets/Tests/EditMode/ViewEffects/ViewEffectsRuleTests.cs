using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Art;
using Wizard.Solo.Meta;
using Wizard.Solo.Sim;
using Wizard.Solo.View.Effects;

namespace Wizard.Solo.Tests.ViewEffects
{
    /// <summary>
    /// G16 순수 규칙: 바인딩 읽기, 이벤트 → 소리 사건·묶음, 그림 정지 셋, 연속 스침 창, 피해 숫자, 입자 예산, 탄 그림 대응,
    /// 번쩍 한도(08 14.1절), 옵션 적용, 누적 가림 넓이 셈, 배경음 id 규칙. 기대값은 요약서에서 가져왔다.
    /// </summary>
    public class ViewEffectsRuleTests
    {
        static PresentationData D => ViewEffectsTestData.Shared;

        // ------------------------------------------------------------ event_bindings.json (08 15.1절, 10 8절 Q-22)

        [Test]
        public void S08_15_1__BindingsParse()
        {
            var b = D.Bindings;
            Assert.AreEqual(860, b.Count); // 860줄(08 15.1절)
            Assert.AreEqual(2.0f, b.GrazeChainWindowSeconds, 1e-6f);
            Assert.AreEqual(5, b.GrazeChainMaxStep);
            var hit = b.Get("player.hit");
            Assert.IsNotNull(hit);
            CollectionAssert.Contains(hit.Sfx, "sfx_h08");
            Assert.AreEqual("medium", hit.Shake);
            Assert.IsTrue(b.InSfxCatalog("sfx_h15"));
            Assert.IsNull(b.Get("no.such.event"));
        }

        [Test]
        public void S10_Q22__HitStopSet()
        {
            var b = D.Bindings;
            Assert.AreEqual(30, b.HitstopMs("heat.crit_hit"));
            Assert.AreEqual(80, b.HitstopMs("art.thunder_lance.fire"));
            Assert.AreEqual(120, b.HitstopMs("player.hit"));
            int n = 0;
            foreach (var x in b.All) if (x.HitstopMs > 0) n++;
            Assert.AreEqual(3, n); // 그림 정지 셋
        }

        [Test]
        public void S10_Q22__FourteenShakeBindings()
        {
            int n = 0;
            foreach (var x in D.Bindings.All) if (x.HasShake) n++;
            Assert.AreEqual(14, n); // 흔들림 있는 바인딩 14줄
        }

        // ------------------------------------------------------------ 이벤트 → 소리 사건 → 묶음 (10 5절)

        static SimWorld NewWorld()
        {
            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            w.Players[0].Active = true;
            return w;
        }

        static List<SoundCue> Map(SoundEventMap m, SimWorld w, SimEvent e)
        {
            var l = new List<SoundCue>();
            m.Map(w, e, l);
            return l;
        }

        static SoundEventMap NewMap() => new SoundEventMap(D.Bindings, D.Tuning, (a, b) => 0f);

        [Test]
        public void S10_5_1__EventToSoundIdAndBus()
        {
            var w = NewWorld();
            var m = NewMap();

            var c = Map(m, w, new SimEvent { Type = SimEventType.PlayerHit, Slot = 0, A = 100 });
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual("player.hit", c[0].Id);
            Assert.AreEqual(SoundBus.Sfx, c[0].Bus); // 내 소리

            c = Map(m, w, new SimEvent { Type = SimEventType.ShotFired, Slot = 0, B = 0 });
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual("heat.shot_fired", c[0].Id);
            Assert.AreEqual(SoundBus.Wand, c[0].Bus); // 지팡이 소리

            c = Map(m, w, new SimEvent { Type = SimEventType.ShotFired, Slot = 0, B = 1 });
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual("heat.crit_shot_fired", c[1].Id);
            Assert.AreEqual(SoundBus.Wand, c[1].Bus);

            c = Map(m, w, new SimEvent { Type = SimEventType.EnemyShotFired, EntityId = 50, A = 0 });
            Assert.AreEqual("enemy.projectile_fire", c[0].Id);
            Assert.AreEqual(SoundBus.Enemy, c[0].Bus); // 적 소리

            w.Zones.Add(new ZoneEntity { Id = 70, OwnerEnemyId = 0, Shape = ZoneShape.Circle, Radius = 1f });
            c = Map(m, w, new SimEvent { Type = SimEventType.TelegraphStarted, EntityId = 70 });
            Assert.AreEqual(2, c.Count);
            Assert.AreEqual("telegraph.start", c[0].Id);
            Assert.AreEqual("telegraph.fill", c[1].Id);
            Assert.AreEqual(SoundBus.Enemy, c[0].Bus);

            c = Map(m, w, new SimEvent { Type = SimEventType.AffinityDiscovered, EntityId = 9, A = (int)Affinity.Weak, Id = Ids.Element.Fire });
            Assert.AreEqual(1, c.Count);
            Assert.AreEqual("hit.affinity_discovered", c[0].Id, "H-15");
            CollectionAssert.Contains(D.Bindings.Get(c[0].Id).Sfx, "sfx_h15");

            c = Map(m, w, new SimEvent { Type = SimEventType.EnemyKilled, EntityId = 9, B = 0, Tick = 10 });
            Assert.AreEqual("enemy.death", c[0].Id);
            Assert.AreEqual(SoundBus.Enemy, c[0].Bus);

            c = Map(m, w, new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 9, B = DamageFlags.Weak });
            Assert.AreEqual("hit.weakness", c[0].Id);
            Assert.AreEqual(SoundEventMap.SemitonesToPitch(2f), c[0].Pitch, 1e-5f); // 약점 +2반음

            c = Map(m, w, new SimEvent { Type = SimEventType.EnemyDamaged, EntityId = 9, B = DamageFlags.Resist });
            Assert.AreEqual("hit.resistance", c[0].Id);
            Assert.AreEqual(SoundEventMap.SemitonesToPitch(-3f), c[0].Pitch, 1e-5f); // 저항 −3반음
            Assert.AreEqual(SoundEventMap.DbToVolume(-3f), c[0].Volume, 1e-5f); // 저항 −3dB
        }

        [Test]
        public void S10_5_1__EveryMappedIdHasSoundInCatalog()
        {
            var w = NewWorld();
            var m = NewMap();
            var events = new[]
            {
                new SimEvent { Type = SimEventType.ShotFired, B = 1 },
                new SimEvent { Type = SimEventType.FireDryWhileLocked },
                new SimEvent { Type = SimEventType.HeatRingChanged, A = (int)HeatRingState.Imminent },
                new SimEvent { Type = SimEventType.HeatRingChanged, A = (int)HeatRingState.Critical },
                new SimEvent { Type = SimEventType.HeatRingChanged, A = (int)HeatRingState.Brink },
                new SimEvent { Type = SimEventType.HeatLockEntered },
                new SimEvent { Type = SimEventType.HeatUnlocked, A = (int)HeatUnlockReason.Timeout },
                new SimEvent { Type = SimEventType.LockBrokenByGraze },
                new SimEvent { Type = SimEventType.GrazeAwarded, Tick = 1 },
                new SimEvent { Type = SimEventType.PlayerHit },
                new SimEvent { Type = SimEventType.InvulnEnded },
                new SimEvent { Type = SimEventType.PlayerDowned },
                new SimEvent { Type = SimEventType.ComboReset, B = 3 },
                new SimEvent { Type = SimEventType.CrystalGained, A = 1 },
                new SimEvent { Type = SimEventType.CrystalsFull },
                new SimEvent { Type = SimEventType.CrystalLost },
                new SimEvent { Type = SimEventType.DebtAdded },
                new SimEvent { Type = SimEventType.ArtFired, Id = Ids.Art.Burst },
                new SimEvent { Type = SimEventType.ArtFizzled, A = (int)ArtFizzleReason.NoCrystal },
                new SimEvent { Type = SimEventType.BoiloverTriggered },
                new SimEvent { Type = SimEventType.Explosion },
                new SimEvent { Type = SimEventType.ChainLightningArc, A = 1 },
                new SimEvent { Type = SimEventType.EnemyDamaged },
                new SimEvent { Type = SimEventType.EnemySpawnNotice },
                new SimEvent { Type = SimEventType.BossPhaseChanged },
                new SimEvent { Type = SimEventType.TodChanged, Id = Ids.Tod.Dusk },
                new SimEvent { Type = SimEventType.StarfallLanded },
            };
            foreach (var e in events)
            {
                var cues = Map(m, w, e);
                Assert.Greater(cues.Count, 0, e.Type.ToString());
                foreach (var c in cues)
                {
                    var b = D.Bindings.Get(c.Id);
                    Assert.IsNotNull(b, c.Id);
                    Assert.IsTrue(b.HasSound, c.Id);
                    foreach (var s in b.Sfx) Assert.IsTrue(D.Bindings.InSfxCatalog(s), s);
                }
            }
        }

        [Test]
        public void S10_5_1__CrystalGainedUsesPentatonicScale()
        {
            var m = NewMap();
            // 같은 5음 음계(10 5.2절): 0, 2, 4, 7, 9, 다음 바퀴 12 …
            Assert.AreEqual(0, m.PentatonicSemitones(0));
            Assert.AreEqual(4, m.PentatonicSemitones(2));
            Assert.AreEqual(9, m.PentatonicSemitones(4));
            Assert.AreEqual(12, m.PentatonicSemitones(5));
            var c = Map(m, NewWorld(), new SimEvent { Type = SimEventType.CrystalGained, A = 3 });
            Assert.AreEqual(SoundEventMap.SemitonesToPitch(4f), c[0].Pitch, 1e-5f);
        }

        [Test]
        public void S10_3_7__MultiKillMergesIntoOneSound()
        {
            var m = NewMap();
            var w = NewWorld();
            var ids = new List<string>();
            for (int i = 0; i < 6; i++)
                foreach (var c in Map(m, w, new SimEvent { Type = SimEventType.EnemyKilled, EntityId = 100 + i, Tick = 100 + i * 2 })) ids.Add(c.Id);
            // 처음 넷은 낱 처치음, 다섯째에서 한꺼번에 처치 하나, 여섯째는 그 묶음에 합쳐짐
            CollectionAssert.AreEqual(new[] { "enemy.death", "enemy.death", "enemy.death", "enemy.death", "enemy.multi_kill" }, ids);
        }

        // ------------------------------------------------------------ 연속 스침 창 (D-12, Q-22)

        [Test]
        public void S10_Q22__GrazeChainExactly2SecondsContinues()
        {
            var g = new GrazePitchCounter(D.Bindings.GrazeChainWindowSeconds, D.Bindings.GrazeChainMaxStep);
            Assert.AreEqual(120, g.WindowTicks);
            Assert.AreEqual(0, g.Next(0, 1000));
            Assert.AreEqual(1, g.Next(0, 1120)); // 정확히 2.0초(120틱) 차는 이어진다
            Assert.AreEqual(0, g.Next(0, 1241)); // 2.0초 초과(121틱)는 끊긴다
        }

        [Test]
        public void S10_3_5__GrazeChainCapsAtFiveSteps()
        {
            var g = new GrazePitchCounter(D.Bindings.GrazeChainWindowSeconds, D.Bindings.GrazeChainMaxStep);
            var got = new List<int>();
            for (int i = 0; i < 7; i++) got.Add(g.Next(0, i * 60));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 5 }, got);
            Assert.AreEqual(0, g.Next(1, 360)); // 슬롯마다 따로
        }

        [Test]
        public void S10_3_5__GrazeSoundPitchRisesOneSemitone()
        {
            var m = NewMap();
            var w = NewWorld();
            var a = Map(m, w, new SimEvent { Type = SimEventType.GrazeAwarded, Slot = 0, Tick = 0 });
            var b = Map(m, w, new SimEvent { Type = SimEventType.GrazeAwarded, Slot = 0, Tick = 120 });
            var c = Map(m, w, new SimEvent { Type = SimEventType.GrazeAwarded, Slot = 0, Tick = 241 });
            Assert.AreEqual("graze.awarded", a[0].Id);
            Assert.AreEqual(1f, a[0].Pitch, 1e-5f);
            Assert.AreEqual(SoundEventMap.SemitonesToPitch(1f), b[0].Pitch, 1e-5f);
            Assert.AreEqual(1f, c[0].Pitch, 1e-5f);
        }

        // ------------------------------------------------------------ 피해 숫자 (08 6.2절, TV-054)

        [Test]
        public void S08_6_2__DamageNumberKindFromFlags()
        {
            Assert.AreEqual(DamageNumberKind.Normal, DamageNumberRules.KindOf(0));
            Assert.AreEqual(DamageNumberKind.Normal, DamageNumberRules.KindOf(DamageFlags.Corroded | DamageFlags.Art));
            Assert.AreEqual(DamageNumberKind.Crit, DamageNumberRules.KindOf(DamageFlags.Crit));
            Assert.AreEqual(DamageNumberKind.Weak, DamageNumberRules.KindOf(DamageFlags.Weak));
            Assert.AreEqual(DamageNumberKind.Resist, DamageNumberRules.KindOf(DamageFlags.Resist));
            Assert.AreEqual(DamageNumberKind.Crit, DamageNumberRules.KindOf(DamageFlags.Crit | DamageFlags.Weak));
        }

        [Test]
        public void S13_TV_054__DamageNumberShapes()
        {
            var t = D.Tuning;
            var n = DamageNumberRules.Build(t, DamageNumberKind.Normal, 123, false, false, null);
            Assert.AreEqual("123", n.Glyphs);
            Assert.AreEqual(1f, n.Scale, 1e-6f);
            var c = DamageNumberRules.Build(t, DamageNumberKind.Crit, 45, false, false, null);
            Assert.AreEqual("45!", c.Glyphs); // 임계 = 느낌표
            Assert.AreEqual(1.5f, c.Scale, 1e-6f); // 임계 1.5배
            Assert.Greater(c.OutlinePx, n.OutlinePx); // 검정 굵은 테
            var wk = DamageNumberRules.Build(t, DamageNumberKind.Weak, 30, false, false, Ids.Element.Fire);
            Assert.AreEqual("30▲", wk.Glyphs);
            Assert.AreEqual(1.25f, wk.Scale, 1e-6f);
            Assert.AreEqual(Ids.Element.Fire, wk.ElementId); // 속성 문양
            var rs = DamageNumberRules.Build(t, DamageNumberKind.Resist, 8, false, false, null);
            Assert.AreEqual("8▼", rs.Glyphs);
            Assert.AreEqual(0.8f, rs.Scale, 1e-6f);
            var gd = DamageNumberRules.Build(t, DamageNumberKind.Gold, 25, false, false, null);
            Assert.AreEqual("+", gd.Prefix); // 골드 +숫자
            Assert.AreEqual("25", gd.Glyphs);
            // 금색은 골드만, 임계는 흰색 — 색 표에서 임계용 금색 열쇠가 없다
            Assert.AreEqual((Color)new Color32(0xFF, 0xD2, 0x4A, 0xFF), D.Tuning.Col("colors", "gold"));
        }

        [Test]
        public void S09_5_7__GlyphOrderAndLayout()
        {
            string order = D.Tuning.S("damage_numbers", "glyph_order");
            Assert.AreEqual("0123456789!▲▼", order);
            Assert.AreEqual(7, DamageNumberRules.GlyphIndex(order, '7'));
            Assert.AreEqual(10, DamageNumberRules.GlyphIndex(order, '!'));
            Assert.AreEqual(11, DamageNumberRules.GlyphIndex(order, '▲'));
            Assert.AreEqual(12, DamageNumberRules.GlyphIndex(order, '▼'));
            Assert.AreEqual(-1, DamageNumberRules.GlyphIndex(order, '+')); // '+'는 글리프에 없다 → uGUI Text
            Assert.AreEqual(-16f, DamageNumberRules.GlyphOffsetPx(0, 3, 16f, 1f), 1e-5f);
            Assert.AreEqual(0f, DamageNumberRules.GlyphOffsetPx(1, 3, 16f, 1f), 1e-5f);
            Assert.AreEqual(24f, DamageNumberRules.GlyphOffsetPx(2, 3, 16f, 1.5f), 1e-5f);
            Assert.AreEqual(-8f, DamageNumberRules.GlyphOffsetPx(0, 2, 16f, 1f), 1e-5f);
        }

        [Test]
        public void S08_6_2__MergeWithin02SecondsAndCap40()
        {
            var cfg = D.Budget;
            Assert.AreEqual(40, cfg.DamageNumbersMax);
            Assert.AreEqual(0.2f, cfg.DamageMergeWindowSeconds, 1e-6f);
            var m = new DamageNumberMerger(cfg.DamageMergeWindowSeconds, cfg.DamageNumbersMax);
            bool merged; int ev;
            var a = m.Add(7, 100, DamageNumberKind.Normal, 12, false, false, null, out merged, out ev);
            Assert.IsFalse(merged);
            var b = m.Add(7, 112, DamageNumberKind.Crit, 13, false, false, null, out merged, out ev);
            Assert.IsTrue(merged); // 0.2초(12틱) 안 = 합침
            Assert.AreEqual(a.Handle, b.Handle);
            Assert.AreEqual(25, b.Amount); // 반올림한 피해의 합
            Assert.AreEqual(DamageNumberKind.Crit, b.Kind); // 강조가 큰 쪽
            m.Add(7, 113, DamageNumberKind.Normal, 5, false, false, null, out merged, out ev);
            Assert.IsFalse(merged); // 0.2초 넘으면 새 숫자
            Assert.AreEqual(2, m.Count);
            m.Add(8, 113, DamageNumberKind.Normal, 1, false, false, null, out merged, out ev);
            Assert.IsFalse(merged); // 다른 적은 따로

            var cap = new DamageNumberMerger(cfg.DamageMergeWindowSeconds, cfg.DamageNumbersMax);
            int firstHandle = 0;
            for (int i = 0; i < 40; i++)
            {
                var l = cap.Add(1000 + i, 0, DamageNumberKind.Normal, 1, false, false, null, out merged, out ev);
                if (i == 0) firstHandle = l.Handle;
                Assert.AreEqual(0, ev);
            }
            cap.Add(2000, 0, DamageNumberKind.Normal, 1, false, false, null, out merged, out ev);
            Assert.AreEqual(firstHandle, ev); // 41번째는 가장 오래된 것을 내보낸다
            Assert.AreEqual(40, cap.Count);
        }

        // ------------------------------------------------------------ 입자 예산 3단계 (08 15.3절, 10 3.3절)

        [Test]
        public void S08_15_3__ParticleBudgetThreeLevels()
        {
            var b = new ParticleBudget(D.Budget);
            int[] totals = { 1500, 800, 300 };
            int[] hit = { 300, 150, 60 };
            int[] art = { 400, 250, 120 };
            int[] shot = { 240, 120, 0 };
            int[] idle = { 60, 30, 0 };
            int[] env = { 200, 100, 60 };
            for (int lv = 0; lv < 3; lv++)
            {
                b.Level = lv;
                Assert.AreEqual(totals[lv], b.TotalCap, "total " + lv);
                Assert.AreEqual(hit[lv], b.Quota(ParticleBudgetConfig.CatHit));
                Assert.AreEqual(art[lv], b.Quota(ParticleBudgetConfig.CatArt));
                Assert.AreEqual(hit[lv], b.Quota(ParticleBudgetConfig.CatKill));
                Assert.AreEqual(shot[lv], b.Quota(ParticleBudgetConfig.CatShot));
                Assert.AreEqual(idle[lv], b.Quota(ParticleBudgetConfig.CatWandIdle));
                Assert.AreEqual(env[lv], b.Quota(ParticleBudgetConfig.CatEnvironment));
            }
            int token;
            b.Level = 2;
            Assert.IsFalse(b.TryAcquire(ParticleBudgetConfig.CatShot, 0f, 1f, out token)); // 최소: 탄 입자 0
        }

        [Test]
        public void S10_3_3__OneShotEvictsOldestRecurringRefuses()
        {
            var b = new ParticleBudget(D.Budget) { Level = 2 };
            var tokens = new List<int>();
            Assert.AreEqual(60, b.Acquire(ParticleBudgetConfig.CatHit, 60, 0f, 10f, tokens));
            int t;
            Assert.IsTrue(b.TryAcquire(ParticleBudgetConfig.CatHit, 0f, 10f, out t)); // 일회: 새 사건은 항상 보인다
            var ev = new List<int>();
            b.DrainEvicted(ev);
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual(tokens[0], ev[0]); // 가장 오래된 것부터
            Assert.AreEqual(60, b.Count(ParticleBudgetConfig.CatHit));

            var r = new ParticleBudget(D.Budget) { Level = 1 };
            Assert.AreEqual(120, r.Acquire(ParticleBudgetConfig.CatShot, 200, 0f, 10f, null)); // 반복: 몫이 차면 새로 안 만든다
            Assert.IsFalse(r.TryAcquire(ParticleBudgetConfig.CatShot, 0f, 10f, out t));
            r.Prune(11f);
            Assert.AreEqual(0, r.Count(ParticleBudgetConfig.CatShot)); // 수명이 다하면 빠진다
        }

        [Test]
        public void S08_15_3__BattleQuotasFillTotal()
        {
            // 전투 표: 몫의 합 = 합계(400+300+300+240+60+200 = 1500 / 800 / 300)
            for (int lv = 0; lv < 3; lv++)
            {
                int sum = 0;
                foreach (var p in D.Budget.Pools) sum += p.Quota[lv];
                Assert.AreEqual(D.Budget.Total[lv], sum, "level " + lv);
            }
        }

        static ParticleBudgetConfig SmallTotal()
        {
            var c = new ParticleBudgetConfig { Total = new[] { 10, 10, 10 } };
            c.Pools.Add(new ParticlePool { Category = ParticleBudgetConfig.CatArt, Priority = 1, OneShot = true, Quota = new[] { 10, 10, 10 } });
            c.Pools.Add(new ParticlePool { Category = ParticleBudgetConfig.CatHit, Priority = 2, OneShot = true, Quota = new[] { 10, 10, 10 } });
            c.Pools.Add(new ParticlePool { Category = ParticleBudgetConfig.CatShot, Priority = 4, OneShot = false, Quota = new[] { 10, 10, 10 } });
            c.Pools.Add(new ParticlePool { Category = ParticleBudgetConfig.CatEnvironment, Priority = 6, OneShot = false, Quota = new[] { 10, 10, 10 } });
            return c;
        }

        [Test]
        public void S10_3_3__TotalOverflowReducesSixFiveFourThenArtLast()
        {
            var b = new ParticleBudget(SmallTotal());
            b.Acquire(ParticleBudgetConfig.CatEnvironment, 4, 0f, 10f, null);
            b.Acquire(ParticleBudgetConfig.CatShot, 3, 0f, 10f, null);
            b.Acquire(ParticleBudgetConfig.CatHit, 3, 0f, 10f, null);
            Assert.AreEqual(10, b.Total);
            int t;
            Assert.IsTrue(b.TryAcquire(ParticleBudgetConfig.CatHit, 0f, 10f, out t)); // 일회: 합계가 차면 6(환경)부터 줄인다
            Assert.AreEqual(3, b.Count(ParticleBudgetConfig.CatEnvironment));
            Assert.IsTrue(b.TryAcquire(ParticleBudgetConfig.CatShot, 0f, 10f, out t)); // 반복도 자기보다 뒤(6)는 밀어낸다
            Assert.AreEqual(2, b.Count(ParticleBudgetConfig.CatEnvironment));
            Assert.IsFalse(b.TryAcquire(ParticleBudgetConfig.CatEnvironment, 0f, 10f, out t)); // 6은 밀어낼 뒤 분류가 없다
            for (int i = 0; i < 2; i++) b.TryAcquire(ParticleBudgetConfig.CatArt, 0f, 10f, out t);
            Assert.AreEqual(0, b.Count(ParticleBudgetConfig.CatEnvironment));
            Assert.AreEqual(4, b.Count(ParticleBudgetConfig.CatShot)); // 환경이 먼저 비었다
            b.TryAcquire(ParticleBudgetConfig.CatArt, 0f, 10f, out t);
            Assert.AreEqual(3, b.Count(ParticleBudgetConfig.CatShot)); // 그다음 4(탄)
            Assert.IsFalse(b.TryAcquire(ParticleBudgetConfig.CatShot, 0f, 10f, out t)); // 탄(4)은 앞 분류(1·2)를 밀지 못한다
            for (int i = 0; i < 3; i++) b.TryAcquire(ParticleBudgetConfig.CatArt, 0f, 10f, out t);
            Assert.AreEqual(0, b.Count(ParticleBudgetConfig.CatShot));
            Assert.AreEqual(4, b.Count(ParticleBudgetConfig.CatHit));
            for (int i = 0; i < 4; i++) b.TryAcquire(ParticleBudgetConfig.CatArt, 0f, 10f, out t);
            Assert.AreEqual(0, b.Count(ParticleBudgetConfig.CatHit)); // 2까지 줄이고
            Assert.AreEqual(10, b.Count(ParticleBudgetConfig.CatArt)); // 1(내 비기)은 마지막
            Assert.IsTrue(b.TryAcquire(ParticleBudgetConfig.CatArt, 0f, 10f, out t)); // 새 비기 사건도 보인다(자기 가장 오래된 것)
            Assert.AreEqual(10, b.Total);
        }

        [Test]
        public void S10_3_3__HitParticleCountRules()
        {
            var c = D.Budget;
            Assert.AreEqual(4, c.TierFor(0).Hit);
            Assert.AreEqual(12, c.TierFor(13).Hit);
            Assert.AreEqual(16, c.TierFor(3).TrailLengthPx);
            Assert.AreEqual(48, c.TierFor(13).TrailLengthPx);
            Assert.AreEqual(8, EffectOptionRules.HitParticles(c, 4, true, false, false, ParticleBudgetConfig.HitDecorFull, OptionValues.All)); // 임계 +4
            Assert.AreEqual(6, EffectOptionRules.HitParticles(c, 4, false, true, false, ParticleBudgetConfig.HitDecorFull, OptionValues.All)); // 약점 +2
            Assert.AreEqual(2, EffectOptionRules.HitParticles(c, 4, false, false, true, ParticleBudgetConfig.HitDecorFull, OptionValues.All)); // 저항 −2, 최소 2
            Assert.AreEqual(4, EffectOptionRules.HitParticles(c, 4, true, false, false, ParticleBudgetConfig.HitDecorHalf, OptionValues.All)); // 탄 101~200 = 절반
            Assert.AreEqual(2, EffectOptionRules.HitParticles(c, 12, true, false, false, ParticleBudgetConfig.HitDecorMinimal, OptionValues.All)); // 201+ = 최소형 2개
            Assert.AreEqual(4, EffectOptionRules.HitParticles(c, 4, true, false, false, ParticleBudgetConfig.HitDecorFull, OptionValues.Reduced)); // 지팡이 효과 줄임 = 절반(올림)
            Assert.AreEqual(ParticleBudgetConfig.HitDecorHalf, c.LoadFor(150).HitDecor);
            Assert.AreEqual(50, c.LoadFor(101).TrailLengthPercent);
            Assert.AreEqual(0, c.LoadFor(201).TrailLengthPercent);
            Assert.AreEqual(100, c.LoadFor(100).TrailLengthPercent);
        }

        // ------------------------------------------------------------ 블록 → 탄 그림 (09 8절, D-41)

        [TestCase(Ids.Block.MagicBolt, "player", "mana_bolt")]
        [TestCase(Ids.Block.Fireball, "player", "fireball")]
        [TestCase(Ids.Block.ChainLightning, "player", "chain_lightning")]
        [TestCase(Ids.Block.IceSpike, "player", "ice_lance")]
        [TestCase(Ids.Block.ShadowOrb, "player", "shadow_orb")]
        // D-41의 mana_bolt 대체는 부메랑 그림(supplement/solo player/boomerang)이 생겨 G16에서 바꿨다.
        [TestCase(Ids.Block.Boomerang, "player", "boomerang")]
        public void S09_8__BlockToShotSprite(string blockId, string group, string key)
        {
            string g, k;
            Assert.IsTrue(D.Shots.TryGet(blockId, out g, out k), blockId);
            Assert.AreEqual(group, g);
            Assert.AreEqual(key, k);
        }

        [TestCase(Ids.Block.SplitTwo)]
        [TestCase(Ids.Block.DoubleSize)]
        [TestCase(Ids.Block.Frost)]
        public void S09_8__ModifierBlocksHaveNoShotSprite(string blockId)
        {
            Assert.IsNull(D.Shots.PathFor(blockId)); // 변형·제어는 탄 몸을 바꾸지 않는다
        }

        // ------------------------------------------------------------ 번쩍 관리기 (08 14.1절, TV-178~185)

        static FlashRequest Full(float op) => new FlashRequest { Opacity = op, OnSeconds = 0.03f, OffSeconds = 0.2f, AreaFraction = 1f };

        [Test]
        public void S13_TV_178__FlashWithin04SecondsBecomesBorder()
        {
            var g = FlashGovernor.FromTuning(D.Tuning);
            Assert.AreEqual(FlashKind.Full, g.Submit(Full(0.35f), 0.0, 0, 1f).Kind);
            Assert.AreEqual(FlashKind.Border, g.Submit(Full(0.35f), 0.3, 0, 1f).Kind);
        }

        [Test]
        public void S13_TV_179_180__TwoPerSlidingSecond()
        {
            var g = FlashGovernor.FromTuning(D.Tuning);
            Assert.AreEqual(FlashKind.Full, g.Submit(Full(0.35f), 0.0, 0, 1f).Kind);
            Assert.AreEqual(FlashKind.Full, g.Submit(Full(0.35f), 0.5, 0, 1f).Kind);
            Assert.AreEqual(FlashKind.Border, g.Submit(Full(0.35f), 0.9, 0, 1f).Kind); // TV-179: 1초 안 2회
            Assert.AreEqual(FlashKind.Full, g.Submit(Full(0.35f), 1.001, 0, 1f).Kind); // TV-180: 창 안에는 0.5 하나, 간격 0.501
        }

        [Test]
        public void S13_TV_181__LevelCaps()
        {
            var r0 = FlashGovernor.FromTuning(D.Tuning).Submit(Full(0.5f), 0.0, 0, 1f);
            var r1 = FlashGovernor.FromTuning(D.Tuning).Submit(Full(0.5f), 0.0, 1, 1f);
            var r2 = FlashGovernor.FromTuning(D.Tuning).Submit(Full(0.5f), 0.0, 2, 1f);
            Assert.AreEqual(0.35f, r0.Opacity, 1e-6f);
            Assert.AreEqual(0.15f, r1.Opacity, 1e-6f);
            Assert.AreEqual(FlashKind.Border, r2.Kind);
            Assert.AreEqual(8f, r2.BorderPx, 1e-6f); // 흰 선 8px
            Assert.AreEqual(0.15f, r2.BorderSeconds, 1e-6f); // 0.15초
        }

        [Test]
        public void S13_TV_182__FlashScaleMultiplies()
        {
            var r = FlashGovernor.FromTuning(D.Tuning).Submit(Full(0.5f), 0.0, 0, 0.5f);
            Assert.AreEqual(0.175f, r.Opacity, 1e-6f);
        }

        [Test]
        public void S13_TV_183__RedFullscreenFlashForbidden()
        {
            var req = Full(0.3f);
            req.Red = true;
            Assert.AreEqual(FlashKind.Border, FlashGovernor.FromTuning(D.Tuning).Submit(req, 0.0, 0, 1f).Kind);
        }

        [Test]
        public void S13_TV_184__LocalFlashUnder25PercentNotCounted()
        {
            var g = FlashGovernor.FromTuning(D.Tuning);
            var local = Full(0.3f);
            local.AreaFraction = 0.2f;
            Assert.AreEqual(FlashKind.Local, g.Submit(local, 0.0, 0, 1f).Kind);
            Assert.AreEqual(0, g.AcceptedInWindow(0.0)); // 관리기 횟수에 안 셈
            var big = Full(0.3f);
            big.AreaFraction = 0.25f;
            Assert.AreEqual(FlashKind.Full, g.Submit(big, 0.1, 0, 1f).Kind);
            Assert.AreEqual(1, g.AcceptedInWindow(0.1)); // 25%는 전체 화면 번쩍으로 셈
        }

        [Test]
        public void S13_TV_185__OnOffDurationsForced()
        {
            var r = FlashGovernor.FromTuning(D.Tuning).Submit(new FlashRequest { Opacity = 0.3f, OnSeconds = 0.2f, OffSeconds = 0.05f, AreaFraction = 1f }, 0.0, 0, 1f);
            Assert.AreEqual(0.05f, r.OnSeconds, 1e-6f);
            Assert.AreEqual(0.10f, r.OffSeconds, 1e-6f);
        }

        [Test]
        public void S13_TV_186__ShakeBindingPx()
        {
            Assert.AreEqual(4f, D.Tuning.F("shake_px", "small"));
            Assert.AreEqual(8f, D.Tuning.F("shake_px", "medium"));
            Assert.AreEqual(12f, D.Tuning.F("shake_px", "large"));
            Assert.AreEqual(16f, D.Tuning.F("shake_px", "xlarge"));
            Assert.LessOrEqual(D.Tuning.F("shake_seconds"), 0.6f); // 흔들림 길이 0.6초 이하
        }

        [Test]
        public void S10_3_4__HitStopBudgetOneTenthPerSecond()
        {
            var b = new HitStopBudget(D.Tuning.F("hitstop_budget", "budget_seconds"), D.Tuning.F("hitstop_budget", "window_seconds"));
            Assert.AreEqual(0.03f, b.Request(0.03f, 0.0), 1e-6f);
            Assert.AreEqual(0.03f, b.Request(0.03f, 0.1), 1e-6f);
            Assert.AreEqual(0.03f, b.Request(0.03f, 0.2), 1e-6f);
            Assert.AreEqual(0.01f, b.Request(0.03f, 0.3), 1e-5f); // 1초 합계 0.1초까지
            Assert.AreEqual(0f, b.Request(0.03f, 0.4), 1e-6f);
            Assert.AreEqual(0.03f, b.Request(0.03f, 1.05), 1e-6f); // 창이 지나면 다시
        }

        [Test]
        public void S10_3_4__CritShakeOncePer03Seconds()
        {
            var g = new GapGate(D.Tuning.F("crit_shake_gap_seconds"));
            Assert.AreEqual(18, g.GapTicks);
            Assert.IsTrue(g.TryPass(0, 100));
            Assert.IsFalse(g.TryPass(0, 117));
            Assert.IsTrue(g.TryPass(0, 118));
        }

        // ------------------------------------------------------------ 옵션 적용 (08 12.2절)

        [Test]
        public void S08_12_2__DamageNumberModes()
        {
            foreach (DamageNumberKind k in System.Enum.GetValues(typeof(DamageNumberKind)))
            {
                Assert.IsTrue(DamageNumberRules.Visible(OptionValues.All, k), "all " + k);
                Assert.IsFalse(DamageNumberRules.Visible(OptionValues.Off, k), "off " + k); // off에서 전부 없음 
            }
            Assert.IsFalse(DamageNumberRules.Visible(OptionValues.CritWeakOnly, DamageNumberKind.Normal)); // crit_weak_only에서 일반 숫자 없음
            Assert.IsTrue(DamageNumberRules.Visible(OptionValues.CritWeakOnly, DamageNumberKind.Crit));
            Assert.IsTrue(DamageNumberRules.Visible(OptionValues.CritWeakOnly, DamageNumberKind.Weak));
            Assert.IsFalse(DamageNumberRules.Visible(OptionValues.CritWeakOnly, DamageNumberKind.Resist));
        }

        [Test]
        public void S08_12_2__BulletTrailThreeValues()
        {
            int max = D.Budget.TrailMaxLengthPx;
            float shortScale = D.Tuning.F("shots", "trail_short_scale");
            Assert.AreEqual(16f, EffectOptionRules.TrailLengthPx(OptionValues.On, 16, 100, shortScale, max, OptionValues.All), 1e-5f);
            Assert.AreEqual(8f, EffectOptionRules.TrailLengthPx(OptionValues.Short, 16, 100, shortScale, max, OptionValues.All), 1e-5f);
            Assert.AreEqual(0f, EffectOptionRules.TrailLengthPx(OptionValues.Off, 16, 100, shortScale, max, OptionValues.All), 1e-5f);
            Assert.AreEqual(8f, EffectOptionRules.TrailLengthPx(OptionValues.On, 16, 50, shortScale, max, OptionValues.All), 1e-5f); // 탄 101~200 꼬리 절반
            Assert.AreEqual(0f, EffectOptionRules.TrailLengthPx(OptionValues.On, 48, 0, shortScale, max, OptionValues.All), 1e-5f); // 201+ 꼬리 끔
            Assert.AreEqual(0f, EffectOptionRules.TrailLengthPx(OptionValues.On, 48, 100, shortScale, max, OptionValues.Minimal), 1e-5f); // 지팡이 효과 최소 = 꼬리 없음
            Assert.AreEqual(48, max); // 꼬리 길이 0.75칸(48px) 이하
            Assert.AreEqual(48f, EffectOptionRules.TrailLengthPx(OptionValues.On, 60, 100, shortScale, max, OptionValues.All), 1e-5f);
            Assert.AreEqual(70, D.Budget.TrailMaxOpacityPercent); // 꼬리 불투명도 70% 이하
        }

        [Test]
        public void S08_12_2__KillShardsSmokeOnly()
        {
            Assert.AreEqual(5, EffectOptionRules.KillShardCount(OptionValues.On, 5));
            Assert.AreEqual(0, EffectOptionRules.KillShardCount(OptionValues.SmokeOnly, 5));
        }

        [Test]
        public void S08_12_2__ArtNamePopupModes()
        {
            int large = D.Tuning.I("art", "name_large_difficulty");
            Assert.AreEqual(5, large);
            Assert.IsTrue(EffectOptionRules.ShowArtName(OptionValues.On, 1, large));
            Assert.IsTrue(EffectOptionRules.ShowArtName(OptionValues.Difficulty5Only, 5, large));
            Assert.IsFalse(EffectOptionRules.ShowArtName(OptionValues.Difficulty5Only, 3, large));
            Assert.IsFalse(EffectOptionRules.ShowArtName(OptionValues.Off, 5, large));
        }

        [Test]
        public void S08_12_2__GroundMarksAndOpacities()
        {
            Assert.IsFalse(EffectOptionRules.DrawGroundMark(false));
            Assert.IsTrue(EffectOptionRules.DrawGroundMark(true));
            Assert.AreEqual(0.5f, EffectOptionRules.MyBulletAlpha(1f, 0.5f), 1e-6f);
            Assert.AreEqual(0.3f, EffectOptionRules.MyBulletAlpha(1f, 0.3f), 1e-6f);
            Assert.AreEqual(0.375f, EffectOptionRules.CloneZoneAlpha(0.75f, 0.5f), 1e-6f); // 병풍 75% × 사본·지대 50%
            Assert.AreEqual(0.35f, EffectOptionRules.CloneZoneAlpha(0.7f, 0.5f), 1e-6f); // 사본 탄 70% × 0.5
            Assert.AreEqual(0.15f, EffectOptionRules.FlashAlpha(0.3f, 0.5f), 1e-6f);
            Assert.AreEqual(0.25f, EffectOptionRules.DamageNumberAlpha(1f, 0.25f), 1e-6f);
        }

        [Test]
        public void S10_3_1__AdditiveOnlyUpTo32Px()
        {
            int max = D.Tuning.I("additive_max_px");
            Assert.AreEqual(32, max);
            Assert.AreEqual(SpriteMaterialKind.SpriteAdditive, EffectOptionRules.MaterialFor(32, 32, true, max));
            Assert.AreEqual(SpriteMaterialKind.Sprite, EffectOptionRules.MaterialFor(64, 64, true, max)); // 64px 적중 불꽃은 알파
            Assert.AreEqual(SpriteMaterialKind.Sprite, EffectOptionRules.MaterialFor(256, 256, true, max)); // 폭발 고리 가산 금지
            Assert.AreEqual(SpriteMaterialKind.Sprite, EffectOptionRules.MaterialFor(16, 16, false, max));
        }

        // ------------------------------------------------------------ 누적 가림 넓이 셈 (10 3.2절)

        [Test]
        public void S10_3_2__CapsByPlace()
        {
            var f = OcclusionField.FromTuning(D.Tuning);
            Assert.AreEqual(0.8f, f.CapAt(new Vector2(5, 5)), 1e-6f); // 예고 마스크 밖 80%
            f.AddMask(new OcclusionField.MaskShape { Shape = ZoneShape.Circle, Pos = new Vector2(5, 5), Radius = 1f });
            Assert.AreEqual(0.4f, f.CapAt(new Vector2(5, 5)), 1e-6f); // 안 40%
            Assert.AreEqual(0.4f, f.CapAt(new Vector2(6.2f, 5)), 1e-6f); // 바깥으로 0.25칸 넓힌 모양
            Assert.AreEqual(0.8f, f.CapAt(new Vector2(6.3f, 5)), 1e-6f);
            f.ClearMask();
            f.AddMask(new OcclusionField.MaskShape { Shape = ZoneShape.Circle, Pos = new Vector2(5, 5), Radius = 1f, Flashing = true });
            Assert.AreEqual(0f, f.CapAt(new Vector2(5, 5)), 1e-6f); // 번쩍이는 마지막 2프레임 0%
            f.ClearMask();
            f.AddMask(new OcclusionField.MaskShape { Shape = ZoneShape.Band, Pos = new Vector2(0, 0), Dir = new Vector2(1, 0), Length = 4f, Width = 1f });
            Assert.AreEqual(0.4f, f.CapAt(new Vector2(3, 0.6f)), 1e-6f);
            Assert.AreEqual(0.8f, f.CapAt(new Vector2(3, 0.9f)), 1e-6f);
            Assert.AreEqual(2, D.Tuning.I("occlusion", "flash_ticks"));
        }

        [Test]
        public void S10_3_2__AreaSumReducesNewEffects()
        {
            var f = OcclusionField.FromTuning(D.Tuning);
            var c = new Vector2(10, 7);
            float a1 = f.Admit(c, 1f, 1f);
            Assert.AreEqual(0.8f, a1, 1e-6f); // 첫 효과도 자리 상한까지
            f.Register(c, 1f, a1);
            Assert.AreEqual(0f, f.Admit(c, 1f, 1f)); // 같은 자리를 덮으면 생략
            var c2 = c + new Vector2(1f, 0f);
            float overlap = OcclusionField.CircleOverlap(c, 1f, c2, 1f) / Mathf.PI;
            float a2 = f.Admit(c2, 1f, 1f);
            Assert.AreEqual(0.8f - 0.8f * overlap, a2, 1e-4f); // 겹친 넓이만큼 줄어든다
            Assert.Less(a2, 0.8f);
            Assert.Greater(a2, 0f);
            Assert.AreEqual(0.5f, f.Admit(c + new Vector2(5f, 0f), 1f, 0.5f), 1e-6f); // 안 겹치면 그대로
        }

        [Test]
        public void S10_3_2__CircleOverlapGeometry()
        {
            Assert.AreEqual(Mathf.PI, OcclusionField.CircleOverlap(Vector2.zero, 1f, Vector2.zero, 1f), 1e-4f);
            Assert.AreEqual(0f, OcclusionField.CircleOverlap(Vector2.zero, 1f, new Vector2(2f, 0f), 1f), 1e-6f);
            Assert.AreEqual(Mathf.PI * 0.25f, OcclusionField.CircleOverlap(Vector2.zero, 2f, new Vector2(0.5f, 0f), 0.5f), 1e-4f); // 작은 원이 안에
            // 반지름 1 두 원, 중심 거리 1: 2·acos(1/2) − (√3)/2 = 1.2284
            Assert.AreEqual(1.2284f, OcclusionField.CircleOverlap(Vector2.zero, 1f, new Vector2(1f, 0f), 1f), 1e-3f);
        }

        // ------------------------------------------------------------ 배경음 id 규칙 (D-72)

        [Test]
        public void S10_5_1__MusicIdRules()
        {
            var m = new MusicRules(D.Tuning);
            Assert.AreEqual("bgm.tod.day", m.ForTod(Ids.Tod.Day));
            Assert.AreEqual("bgm.tod.night", m.ForTod(Ids.Tod.Night));
            Assert.AreEqual("bgm.boss", m.ForTod(Ids.Tod.Boss));
            Assert.AreEqual("bgm.boss", m.Boss);
            Assert.AreEqual("bgm.camp", m.Camp);
            bool changed;
            Assert.AreEqual("bgm.tod.dusk", m.ForEvent(null, new SimEvent { Type = SimEventType.TodChanged, Id = Ids.Tod.Dusk }, out changed));
            Assert.IsTrue(changed);
            Assert.AreEqual("bgm.camp", m.ForEvent(null, new SimEvent { Type = SimEventType.CampEntered }, out changed));
            Assert.AreEqual("bgm.boss", m.ForEvent(null, new SimEvent { Type = SimEventType.BossIntroBegin }, out changed));
            Assert.AreEqual("bgm.tod.day", m.ForEvent(null, new SimEvent { Type = SimEventType.WaveStarted, A = 11 }, out changed)); // 야영 뒤 같은 시간대도 되돌림
            m.ForEvent(null, new SimEvent { Type = SimEventType.WaveStarted, A = 10 }, out changed);
            Assert.IsFalse(changed); // 보스 웨이브는 등장이 바꾼다
            Assert.AreEqual("bgm.run_end", m.ForEvent(null, new SimEvent { Type = SimEventType.RunEnded }, out changed));
            m.ForEvent(null, new SimEvent { Type = SimEventType.ShotFired }, out changed);
            Assert.IsFalse(changed);

            var w = new SimWorld(TestContent.Db, SimModuleSet.Stubs(), 1);
            Assert.IsNull(m.ForState(w)); // 판 전에는 바꾸지 않는다
            w.Run.Phase = RunPhase.Camp;
            Assert.AreEqual("bgm.camp", m.ForState(w));
            w.Run.Phase = RunPhase.WaveCombat;
            w.Wave.Wave = 8;
            Assert.AreEqual("bgm.tod.night", m.ForState(w));
            w.Run.Phase = RunPhase.Practice;
            Assert.AreEqual("bgm.practice", m.ForState(w));
        }

        [Test]
        public void S10_3_7__KillBurstCounter()
        {
            var k = new KillBurstCounter(D.Tuning.F("multi_kill", "window_seconds"), D.Tuning.I("multi_kill", "count"));
            Assert.AreEqual(KillBurstCounter.Result.Single, k.Kill(0));
            Assert.AreEqual(KillBurstCounter.Result.Single, k.Kill(4));
            Assert.AreEqual(KillBurstCounter.Result.Single, k.Kill(8));
            Assert.AreEqual(KillBurstCounter.Result.Single, k.Kill(12));
            Assert.AreEqual(KillBurstCounter.Result.Merged, k.Kill(16)); // 0.3초(18틱) 안 5마리
            Assert.AreEqual(KillBurstCounter.Result.Suppressed, k.Kill(20));
            var k2 = new KillBurstCounter(D.Tuning.F("multi_kill", "window_seconds"), D.Tuning.I("multi_kill", "count"));
            for (int i = 0; i < 5; i++) Assert.AreEqual(KillBurstCounter.Result.Single, k2.Kill(i * 18)); // 0.3초 간격이면 낱 처치
        }
    }
}
