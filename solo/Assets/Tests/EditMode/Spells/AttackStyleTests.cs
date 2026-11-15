using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Spells
{
    /// <summary>
    /// 기본 공격 유형 15종(Docs/12-attack-styles.md)의 규칙: 데이터, 판 시작의 코어 바꾸기, 유형별 동작(착탄 범위·선 적중 수·튕김·귀환·
    /// 지뢰·낙하 지연·추적·분열…), 변형 블록 결합, 결정론. 진짜 모듈 전부 + 연습 방(허수아비)에서 돈다(명세 콘텐츠 TestContent.Db).
    /// 기대값은 AttackStyles.json의 수치에서 읽는다(구현 출력에서 베끼지 않는다).
    /// </summary>
    public sealed class AttackStyleTests
    {
        const float Eps = 1e-3f;
        static IContentDb Db => TestContent.Db;
        static AttackStyleParams St(string id) => Db.Block(id).Style;
        static int Ticks(float s) => Mathf.Max(1, SimConst.Ticks(s));
        static readonly Vector2 S = new Vector2(0f, 1f);   // 시야 S = 시뮬레이션 +y

        sealed class Rig
        {
            public SimWorld W;
            public PlayerState P;
            public EnemyState Dummy;
            public readonly List<SimEvent> Ev = new List<SimEvent>();
            bool _fire;

            public static Rig Make(int character, string style, float dist = 3f, List<string> links = null, IContentDb db = null, ulong seed = 7UL)
            {
                db = db ?? Db;
                var r = new Rig();
                r.W = AttackStyleKit.Start(db, AttackStyleKit.PracticeConfig(db, character, style, RunMode.Practice, seed, links));
                r.P = r.W.P0;
                r.Dummy = AttackStyleKit.Dummy(r.W);
                r.P.Pos = r.Dummy.Pos - S * dist;
                r.P.PrevPos = r.P.Pos;
                r.P.Facing = Dir8.S;
                r.W.Events.Clear();
                return r;
            }

            /// <summary>허수아비를 치운다(빈 방).</summary>
            public void ClearDummy()
            {
                Dummy.Pos = new Vector2(1f, 1f);
                Dummy.Burrowed = true;
            }

            public EnemyState Add(Vector2 pos) => AttackStyleKit.AddDummy(W, pos);

            public EnemyState AddSlime(Vector2 pos, float hp)
            {
                var e = W.Mods.Enemies.Spawn(W, new EnemySpawnSpec { DefId = Ids.Enemy.SlimeMoss, Pos = pos, Instant = true, NoReward = true });
                e.Hp = hp;
                e.MaxHp = hp;
                e.MoveSpeed = 0f;
                return e;
            }

            public void Fire(bool down)
            {
                if (_fire == down) return;
                _fire = down;
                W.EnqueueInput(0, new InputEvent(LogicalKey.Fire, down, SimConst.TickStartU(W.Tick)));
            }

            public void Step(int n = 1)
            {
                for (int i = 0; i < n; i++)
                {
                    P.InvulnUntilTick = W.Tick + 2;
                    W.Step();
                    Ev.AddRange(W.Events);
                    W.Events.Clear();
                }
            }

            /// <summary>J 한 번(한 틱 누르고 뗀다). 쏜 틱을 돌려준다.</summary>
            public int Shoot()
            {
                int t = W.Tick;
                Fire(true);
                Step();
                Fire(false);
                return t;
            }

            public List<SimEvent> Of(SimEventType type)
            {
                var l = new List<SimEvent>();
                for (int i = 0; i < Ev.Count; i++) if (Ev[i].Type == type) l.Add(Ev[i]);
                return l;
            }

            public int Damage(EnemyState e, int tick = -1)
            {
                int n = 0;
                for (int i = 0; i < Ev.Count; i++)
                    if (Ev[i].Type == SimEventType.EnemyDamaged && Ev[i].EntityId == e.Id && (tick < 0 || Ev[i].Tick == tick)) n += Ev[i].A;
                return n;
            }

            public int Hits(EnemyState e)
            {
                int n = 0;
                for (int i = 0; i < Ev.Count; i++) if (Ev[i].Type == SimEventType.EnemyDamaged && Ev[i].EntityId == e.Id) n++;
                return n;
            }
        }

        // ================================================================== 데이터·판 시작

        [Test]
        public void AS_Data__FifteenStyles_ThreePerCharacter_LoadedNamedAndOutOfLootLists()
        {
            var cdb = (ContentDb)Db;
            foreach (var err in cdb.LoadErrors) Assert.IsFalse(err.Contains("AttackStyles"), err);
            var seen = new HashSet<string>();
            for (int c = 1; c <= 5; c++)
            {
                string charId = Ids.Char.Of(c);
                var list = AttackStyles.StylesOf(Db, charId);
                Assert.AreEqual(3, list.Count, charId);
                CollectionAssert.AreEqual(AttackStyleKit.Styles[c - 1], list);
                Assert.AreEqual(list[0], AttackStyles.DefaultOf(Db, charId));
                for (int k = 0; k < 3; k++)
                {
                    var b = Db.Block(list[k]);
                    Assert.IsNotNull(b, list[k]);
                    Assert.IsTrue(b.IsProjectile && b.Style != null && b.Style.Kind != AttackBehavior.None, list[k]);
                    Assert.AreEqual(charId, b.Style.Character, list[k]);
                    Assert.AreEqual(Db.Character(charId).Element, b.Element, "유형의 속성 = 캐릭터 속성: " + list[k]);
                    Assert.AreEqual(new[] { "a", "b", "c" }[k], b.Style.Slot);
                    Assert.IsTrue(TestContent.Names.Has(list[k]), "이름 " + list[k]);
                    Assert.IsTrue(TestContent.Names.Has(Wizard.Solo.Screens.Prep.ExpeditionPrepModel.StyleDescKey(list[k])), "설명 " + list[k]);
                    foreach (var tag in b.Style.Tags) Assert.IsTrue(TestContent.Names.Has("str.attack_style.tag." + tag), tag);
                    Assert.IsTrue(seen.Add(list[k]), "유형은 캐릭터마다 다르다");
                    // 전리품·서고·제물 목록에는 나오지 않는다.
                    foreach (var lb in Db.Blocks) Assert.AreNotEqual(list[k], lb.Id);
                    foreach (var r in new[] { Ids.Rarity.Common, Ids.Rarity.Rare, Ids.Rarity.Hero })
                        CollectionAssert.DoesNotContain(Db.BlocksOfRarity(r), list[k]);
                    Assert.IsFalse(AttackStyles.IsStyleOf(Db, Ids.Char.Of(c % 5 + 1), list[k]), "다른 캐릭터는 못 쓴다");
                }
            }
            Assert.AreEqual(15, seen.Count);
            Assert.IsNotNull(Db.Block("block.core.frost_lance_shard"));
        }

        [Test]
        public void AS_Unlock__KnobOffByDefault_AllThreeOpenFromStart()
        {
            Assert.IsFalse(AttackStyles.UnlockEnabled(Db));
            for (int c = 1; c <= 5; c++)
                foreach (var id in AttackStyles.StylesOf(Db, Ids.Char.Of(c)))
                {
                    Assert.AreEqual(0, AttackStyles.UnlockWave(Db, Ids.Char.Of(c), id));
                    Assert.IsTrue(AttackStyles.IsUnlocked(Db, Ids.Char.Of(c), id, 0));
                }
        }

        [Test]
        public void AS_Run__StartReplacesMagicBoltCoreWithChosenStyle_KeepsOtherCores()
        {
            var r = Rig.Make(1, "block.core.flame_jet");
            Assert.AreEqual("block.core.flame_jet", r.P.Loadout.Core.BlockId);
            Assert.AreEqual("block.core.flame_jet", r.P.Spell.Core.Id);
            Assert.AreEqual(Ticks(Db.Block("block.core.flame_jet").IntervalSeconds), r.P.Spell.FireIntervalTicks);

            var none = Rig.Make(1, null);
            Assert.AreEqual(Ids.Block.MagicBolt, none.P.Spell.Core.Id, "고르지 않으면 명세 그대로(마력탄)");

            var other = Rig.Make(1, "block.core.blizzard");
            Assert.AreEqual(Ids.Block.MagicBolt, other.P.Spell.Core.Id, "다른 캐릭터의 유형은 쓸 수 없다");

            var cfg = AttackStyleKit.PracticeConfig(Db, 1, "block.core.ember_mortar");
            cfg.Loadout.Core = Ids.Block.Fireball;
            var w = AttackStyleKit.Start(Db, cfg);
            Assert.AreEqual(Ids.Block.Fireball, w.P0.Spell.Core.Id, "일부러 코어에 끼운 다른 투사체는 그대로");

            // 표준 T(비기 단위)는 마력탄과 같다(standard_t_base).
            Assert.AreEqual(Rig.Make(3, null).P.Spell.StandardTBase, Rig.Make(3, "block.core.rune_mine").P.Spell.StandardTBase, Eps);
        }

        [Test]
        public void AS_Run__TutorialUsesStyleA_WhenKnobOn_SpecKeepsChoice()
        {
            for (int c = 1; c <= 5; c++)
            {
                var cfg = AttackStyleKit.PracticeConfig(AttackStyleKit.Tuned, c, AttackStyleKit.Styles[c - 1][2], RunMode.Tutorial);
                var w = AttackStyleKit.Start(AttackStyleKit.Tuned, cfg);
                Assert.AreEqual(AttackStyleKit.Styles[c - 1][0], w.P0.Spell.Core.Id, "튜토리얼은 A 유형 char." + c);
            }
            Assert.IsTrue(AttackStyles.TutorialUsesDefault(AttackStyleKit.Tuned));
            Assert.IsFalse(AttackStyles.TutorialUsesDefault(Db), "명세 콘텐츠는 손잡이가 꺼져 있다");
            var spec = AttackStyleKit.Start(Db, AttackStyleKit.PracticeConfig(Db, 2, null, RunMode.Tutorial));
            Assert.AreEqual(Ids.Block.MagicBolt, spec.P0.Spell.Core.Id);
        }

        [Test]
        public void AS_Compile__StyleCoresTakeEveryModifier_NoEffectWarningNever()
        {
            for (int c = 1; c <= 5; c++)
                foreach (var id in AttackStyleKit.Styles[c - 1])
                {
                    var r = Rig.Make(c, id, 3f, new List<string> { Ids.Block.Pierce, Ids.Block.WallBounce, Ids.Block.Homing, Ids.Block.DoubleSize });
                    Assert.IsTrue(r.P.Spell.IsValid, id);
                    foreach (var d in r.P.Spell.Diagnostics) Assert.AreNotEqual(SpellDiagCode.W_NO_EFFECT, d.Code, id);
                }
        }

        // ================================================================== 동작

        [Test]
        public void AS_Lob__EmberMortar_LandsOnAimedEnemyAfterFlight_BlastRadius_FloorTicks()
        {
            var st = St("block.core.ember_mortar");
            var r = Rig.Make(1, "block.core.ember_mortar");
            var near = r.Add(r.Dummy.Pos + new Vector2(1.2f, 0f));     // 겉면 0.7 ≤ 반경 1.4, 겨누기 원뿔(±20°) 밖
            var far = r.Add(r.Dummy.Pos + new Vector2(3f, 0f));
            int t0 = r.Shoot();
            int flight = Ticks(st.FlightSeconds);
            r.Step(flight + Ticks(st.FloorSeconds) + 5);

            var lobs = r.Of(SimEventType.StyleLobLaunched);
            Assert.AreEqual(1, lobs.Count);
            Assert.AreEqual(t0, lobs[0].Tick);
            Assert.AreEqual(flight, lobs[0].A);
            Assert.AreEqual(r.Dummy.Pos.x, lobs[0].Pos2.x, Eps, "앞 원뿔 안의 가장 가까운 적을 겨눈다");
            Assert.AreEqual(r.Dummy.Pos.y, lobs[0].Pos2.y, Eps);

            var blasts = r.Of(SimEventType.StyleBlast);
            var land = blasts.Find(e => e.B == (int)StyleBlastKind.MortarLand);
            Assert.AreEqual(t0 + flight, land.Tick, "날아가는 동안은 맞지 않고 떨어지는 틱에 터진다");
            Assert.AreEqual(st.BlastRadius, land.F, Eps);
            Assert.AreEqual(2, land.A, "반경 안 둘");
            Assert.AreEqual(0, r.Damage(r.Dummy, t0 + flight - 1) + r.Damage(near, t0 + flight - 1));
            int main = r.Damage(r.Dummy, land.Tick), side = r.Damage(near, land.Tick);
            Assert.AreEqual(Mathf.RoundToInt(Db.Block("block.core.ember_mortar").BaseDamage), main);
            Assert.AreEqual(Mathf.Floor(Db.Block("block.core.ember_mortar").BaseDamage * st.ExtraTargetMul + 0.5f), side, "둘째 적부터 extra_target_mul");
            Assert.AreEqual(0, r.Damage(far), "반경 밖은 맞지 않는다");

            int floorTicks = Ticks(st.FloorSeconds) / Ticks(st.FloorIntervalSeconds);
            Assert.AreEqual(floorTicks, blasts.FindAll(e => e.B == (int)StyleBlastKind.MortarFloorTick).Count, "불바닥 1초 = 0.25초 × 4");
            Assert.AreEqual(1, r.Of(SimEventType.StyleGroundSpawned).Count);
            Assert.AreEqual(1, r.Of(SimEventType.StyleGroundExpired).Count);
            Assert.AreEqual(1, r.Of(SimEventType.Explosion).Count, "지금 그림용 속성 폭발도 한 번(B = 1)");
            Assert.AreEqual(1, r.Of(SimEventType.Explosion)[0].B);
        }

        [Test]
        public void AS_Cone__FlameJet_HitsInsideFanOnly_BurnStacksToCap()
        {
            var st = St("block.core.flame_jet");
            var r = Rig.Make(1, "block.core.flame_jet", 1.8f);
            var side = r.Add(r.P.Pos + new Vector2(1.8f, 0f));           // 정면에서 90도
            var beyond = r.Add(r.P.Pos + S * (st.ConeRadius + 1.2f));     // 반경 밖
            r.Fire(true);
            r.Step(90);
            Assert.Greater(r.Damage(r.Dummy), 0);
            Assert.AreEqual(0, r.Damage(side), "부채꼴 밖");
            Assert.AreEqual(0, r.Damage(beyond), "반경 밖");
            Assert.AreEqual(st.BurnMaxStacks, r.Dummy.BurnStacks, "맞을수록 화상이 쌓인다(상한)");
            var started = r.Of(SimEventType.StyleConeStarted);
            Assert.Greater(started.Count, 0);
            Assert.AreEqual(Mathf.RoundToInt(st.ConeHalfDegrees), started[0].A);
            Assert.AreEqual(Ticks(st.SweepSeconds), started[0].B, "반지름이 퍼지는 휩쓸기");
            // 화상 피해는 지대 피해(콤보 없음)로 따로 들어온다.
            Assert.Greater(r.Of(SimEventType.StyleStatus).Count, 0);
        }

        [Test]
        public void AS_Ricochet__BouncingEmber_JumpsToSecondEnemy_AndIgnites()
        {
            var r = Rig.Make(1, "block.core.bouncing_ember");
            var second = r.Add(r.Dummy.Pos + new Vector2(2.2f, 0f));
            r.Shoot();
            r.Step(40);
            Assert.AreEqual(1, r.Of(SimEventType.StyleRicochet).Count);
            Assert.AreEqual(second.Id, r.Of(SimEventType.StyleRicochet)[0].A);
            Assert.Greater(r.Hits(r.Dummy), 0);
            Assert.Greater(r.Hits(second), 0, "튄 불씨가 둘째 적을 맞힌다");
            Assert.Greater(r.Dummy.BurnStacks + second.BurnStacks, 0, "착화");
        }

        [Test]
        public void AS_Chain__ChainBolt_InstantLineHitsThenHopsTwice_ShortRange()
        {
            var b = Db.Block("block.core.chain_bolt");
            var r = Rig.Make(2, "block.core.chain_bolt");
            var two = r.Add(r.Dummy.Pos + new Vector2(1.6f, 0f));
            var three = r.Add(two.Pos + new Vector2(1.6f, 0f));
            int t0 = r.Shoot();
            Assert.AreEqual(1, r.Hits(r.Dummy), "즉발: 쏜 틱에 맞는다");
            Assert.AreEqual(t0, r.Of(SimEventType.EnemyDamaged)[0].Tick);
            Assert.AreEqual(1, r.Hits(two));
            Assert.AreEqual(1, r.Hits(three), "옮겨 붙기 2번 = 셋을 맞힌다");
            Assert.AreEqual(1 + b.Params.ChainHops, r.Of(SimEventType.ChainLightningArc).Count);

            var far = Rig.Make(2, "block.core.chain_bolt", b.Range + b.HitRadius + 0.5f + 0.2f);   // 몸 겉면 + 광선 반폭이 사거리 밖
            far.Shoot();
            Assert.AreEqual(0, far.Hits(far.Dummy), "짧은 사거리 밖은 맞지 않는다");
        }

        [Test]
        public void AS_Orb__ThunderOrb_PassesThroughAndZapsEnemiesBesideThePath()
        {
            var st = St("block.core.thunder_orb");
            var r = Rig.Make(2, "block.core.thunder_orb");
            var beside = r.Add(r.Dummy.Pos + new Vector2(st.ZapRadius + 0.4f, -1.5f));   // 길에서 비켜 있다(몸에 닿지 않음)
            var behind = r.Add(r.Dummy.Pos + S * 1.5f);                                  // 꿰뚫고 지나가 뒤의 적도 맞힌다
            r.Shoot();
            r.Step(120);
            Assert.Greater(r.Of(SimEventType.StyleZap).FindAll(e => e.A == beside.Id).Count, 0, "둘레를 지진다");
            Assert.Greater(r.Damage(beside), 0);
            Assert.Greater(r.Damage(behind), 0, "꿰뚫고 지나간다");
            Assert.Greater(r.Of(SimEventType.ProjectilePassed).Count, 0);
        }

        [Test]
        public void AS_Strike__SkyStrike_WarnsThenLandsAfterDelay()
        {
            var st = St("block.core.sky_strike");
            var r = Rig.Make(2, "block.core.sky_strike");
            int t0 = r.Shoot();
            int delay = Ticks(st.DelaySeconds);
            r.Step(delay + 3);
            var warn = r.Of(SimEventType.StyleStrikeWarned);
            Assert.AreEqual(1, warn.Count);
            Assert.AreEqual(t0, warn[0].Tick);
            Assert.AreEqual(delay, warn[0].A);
            Assert.AreEqual(r.Dummy.Pos.x, warn[0].Pos.x, Eps, "앞 원뿔 안의 적 발밑");
            Assert.AreEqual(r.Dummy.Pos.y, warn[0].Pos.y, Eps);
            var dmg = r.Of(SimEventType.EnemyDamaged);
            Assert.AreEqual(1, dmg.Count);
            Assert.AreEqual(t0 + delay, dmg[0].Tick, "예고 뒤에만 맞는다");
            Assert.AreEqual(t0 + delay, r.Of(SimEventType.StyleBlast)[0].Tick);
        }

        [Test]
        public void AS_Strike__NoEnemy_LandsAtDefaultDistance()
        {
            var st = St("block.core.sky_strike");
            var r = Rig.Make(2, "block.core.sky_strike");
            r.ClearDummy();
            Vector2 from = r.P.Pos;
            r.Shoot();
            var warn = r.Of(SimEventType.StyleStrikeWarned)[0];
            Assert.AreEqual((from + S * st.DefaultDistance).y, warn.Pos.y, Eps);
        }

        [Test]
        public void AS_Pages__PageFlurry_ThreePagesPerShot_EachPiercesOnce()
        {
            var st = St("block.core.page_flurry");
            var r = Rig.Make(3, "block.core.page_flurry");
            int t0 = r.Shoot();
            var spawned = r.Of(SimEventType.ProjectileSpawned).FindAll(e => e.Tick == t0);
            Assert.AreEqual(st.Copies, spawned.Count, "책장 셋");
            r.Step(40);
            Assert.AreEqual(st.Copies, r.Hits(r.Dummy), "휘어 들어와 셋 다 맞는다");

            // 일렬 둘: 한 장이 한 번 꿰뚫어 뒤의 적도 맞힌다(둘째는 extra_target_mul).
            var line = Rig.Make(3, "block.core.page_flurry", 2f);
            var back = line.Add(line.Dummy.Pos + S * 1.1f);
            line.Shoot();
            line.Step(40);
            Assert.Greater(line.Hits(back), 0, "관통 1");
        }

        [Test]
        public void AS_Boomerang__TomeThrow_HitsTheSameEnemyGoingAndReturning()
        {
            var r = Rig.Make(3, "block.core.tome_throw", 2.5f);
            r.Shoot();
            r.Step(120);
            Assert.AreEqual(2, r.Hits(r.Dummy), "갈 때·올 때 한 번씩");
            var exp = r.Of(SimEventType.ProjectileExpired);
            Assert.AreEqual(1, exp.Count);
            Assert.AreEqual(3, exp[0].A, "쏜 사람에게 돌아와 사라진다");
        }

        [Test]
        public void AS_Mine__RuneMine_TriggersWhenSteppedOn_FusesOtherwise_CapsCount()
        {
            var st = St("block.core.rune_mine");
            // 빈 방: 시간이 다 되면 터진다.
            var r = Rig.Make(3, "block.core.rune_mine");
            r.ClearDummy();
            int t0 = r.Shoot();
            r.Step(Ticks(st.FuseSeconds) + 2);
            var blast = r.Of(SimEventType.StyleBlast);
            Assert.AreEqual(1, blast.Count);
            Assert.AreEqual(t0 + Ticks(st.FuseSeconds), blast[0].Tick, "fuse");
            Assert.AreEqual(1, r.Of(SimEventType.StyleMineArmed).Count);

            // 무장 뒤 적이 밟으면 그 틱에 터진다.
            var m = Rig.Make(3, "block.core.rune_mine");
            m.ClearDummy();
            m.Shoot();
            m.Step(Ticks(st.ArmSeconds) + 2);
            var mine = m.W.Projectiles.Find(p => p.Block.Id == "block.core.rune_mine" && !p.Dead);
            Assert.IsNotNull(mine);
            var walker = m.Add(mine.Pos + new Vector2(0.5f + st.TriggerRadius + 0.3f, 0f));     // 겉면이 발동 반경 밖
            m.Step();
            Assert.AreEqual(0, m.Of(SimEventType.StyleBlast).Count);
            walker.Pos = mine.Pos + new Vector2(walker.BodyRadius + st.TriggerRadius * 0.5f, 0f);   // 겉면 거리 = 발동 반경의 절반
            int stepTick = m.W.Tick;
            m.Step();
            Assert.AreEqual(1, m.Of(SimEventType.StyleBlast).Count);
            Assert.AreEqual(stepTick, m.Of(SimEventType.StyleBlast)[0].Tick);
            Assert.Greater(m.Damage(walker), 0);

            // 동시 상한: 하나 더 새기면 가장 오래된 것이 바로 터진다.
            var c = Rig.Make(3, "block.core.rune_mine");
            c.ClearDummy();
            int interval = Ticks(Db.Block("block.core.rune_mine").IntervalSeconds);
            for (int i = 0; i < st.MaxMines; i++) { c.Shoot(); c.Step(interval); }
            Assert.AreEqual(0, c.Of(SimEventType.StyleBlast).Count);
            c.Shoot();
            Assert.AreEqual(1, c.Of(SimEventType.StyleBlast).Count, "상한을 넘으면 가장 오래된 것이 터진다");
        }

        [Test]
        public void AS_Lance__FrostLance_PiercesOnce_ShattersIntoShardsWhereItStops_Slows()
        {
            var st = St("block.core.frost_lance");
            var r = Rig.Make(4, "block.core.frost_lance", 2.5f);
            var second = r.Add(r.Dummy.Pos + S * 1.5f);
            var third = r.Add(second.Pos + S * 1.5f);
            r.Shoot();
            r.Step(60);
            Assert.AreEqual(1, r.Hits(r.Dummy));
            Assert.AreEqual(1, r.Hits(second), "관통 1 → 둘째에서 멈춘다");
            var shatter = r.Of(SimEventType.StyleShatter);
            Assert.AreEqual(1, shatter.Count);
            Assert.AreEqual(st.ShardCount, shatter[0].A);
            Assert.AreEqual(second.Pos.y - second.BodyRadius, shatter[0].Pos.y, 0.4f, "멈춘 자리(둘째 적의 겉면)에서 깨진다");
            Assert.AreEqual(st.ShardCount, r.Of(SimEventType.ProjectileSpawned).FindAll(e => e.Id == st.ShardBlock).Count);
            Assert.Greater(r.Hits(third), 0, "파편이 뒤의 적을 맞힌다");
            Assert.Greater(r.Dummy.Slows[(int)SlowSlot.IceSpike].Strength, 0f, "맞은 적은 느려진다");
        }

        [Test]
        public void AS_Cone__Blizzard_ChillStacksIntoFreeze()
        {
            var st = St("block.core.blizzard");
            var r = Rig.Make(4, "block.core.blizzard", 1.6f);
            r.Fire(true);
            int guard = 0;
            while (r.Dummy.FrozenUntilTick <= r.W.Tick && guard++ < 300) r.Step();
            Assert.Less(guard, 300, "둔화가 쌓이면 언다");
            Assert.AreEqual(0, r.Dummy.ChillStacks, "얼면 겹이 비워진다");
            var chill = r.Of(SimEventType.StyleStatus).FindAll(e => e.B == (int)StyleStatusKind.Chill && e.EntityId == r.Dummy.Id);
            Assert.AreEqual(st.ChillFreezeAt - 1, chill[st.ChillFreezeAt - 2].A);
        }

        [Test]
        public void AS_Icicles__IcicleFall_DropsInOrderAlongTheLine()
        {
            var st = St("block.core.icicle_fall");
            var r = Rig.Make(4, "block.core.icicle_fall");
            r.ClearDummy();
            int t0 = r.Shoot();
            int delay = Ticks(st.DelaySeconds), gap = Ticks(st.DropGapSeconds);
            r.Step(delay + gap * st.DropCount + 2);
            var warns = r.Of(SimEventType.StyleStrikeWarned);
            Assert.AreEqual(st.DropCount, warns.Count, "떨어질 자리를 미리 알린다");
            var drops = r.Of(SimEventType.StyleBlast);
            Assert.AreEqual(st.DropCount, drops.Count);
            for (int i = 0; i < st.DropCount; i++)
            {
                Assert.AreEqual(t0 + delay + i * gap, drops[i].Tick, "차례로 " + i);
                Assert.AreEqual((r.P.Pos + S * (st.DropStart + i * st.DropStep)).y, drops[i].Pos.y, Eps);
                Assert.AreEqual((int)StyleBlastKind.IcicleDrop, drops[i].B);
            }
        }

        [Test]
        public void AS_Cone__ShadowWave_PiercesEveryoneInTheFan_PullsTowardCaster()
        {
            var st = St("block.core.shadow_wave");
            var r = Rig.Make(5, "block.core.shadow_wave", 2f);
            var back = r.Add(r.Dummy.Pos + S * 1.2f);
            var slime = r.AddSlime(r.P.Pos + new Vector2(0.6f, 1.2f), 100000f);
            r.Shoot();
            r.Step(Ticks(st.SweepSeconds) + 2);
            Assert.AreEqual(1, r.Hits(r.Dummy));
            Assert.AreEqual(1, r.Hits(back), "전부 꿰뚫는다");
            Assert.AreEqual(1, r.Hits(slime));
            var pulls = r.Of(SimEventType.EnemyCc).FindAll(e => e.EntityId == slime.Id && e.A == (int)CcKind.Pull);
            Assert.AreEqual(1, pulls.Count, "살짝 끌어당긴다");
            Assert.AreEqual(st.PullDistance, pulls[0].F, 0.05f);
        }

        [Test]
        public void AS_Cone__ShadowScythe_SweepsBothSides_ExtraHitsAddCombo()
        {
            var st = St("block.core.shadow_scythe");
            var r = Rig.Make(5, "block.core.shadow_scythe", 1.6f);
            var left = r.Add(r.P.Pos + new Vector2(-1.4f, 0.6f));
            var right = r.Add(r.P.Pos + new Vector2(1.4f, 0.6f));
            int combo0 = r.P.Combo;
            r.Shoot();
            r.Step(Ticks(st.SweepSeconds) + 2);
            Assert.AreEqual(1, r.Hits(r.Dummy));
            Assert.AreEqual(1, r.Hits(left), "호 양쪽");
            Assert.AreEqual(1, r.Hits(right));
            var cone = r.Of(SimEventType.StyleConeStarted)[0];
            Assert.Less(cone.B, 0, "각도로 휩쓴다");
            // 콤보: 적중 +1(발사당 +3 상한) + 처음 맞힘 덤 + 둘째부터 1씩.
            Assert.AreEqual(combo0 + 3 + st.ComboOnHit + 2 * st.ComboPerExtraHit, r.P.Combo);
        }

        [Test]
        public void AS_Seeker__EyeSeeker_TurnsTowardOffAxisEnemy_SplitsOnKill()
        {
            var r = Rig.Make(5, "block.core.eye_seeker");
            r.ClearDummy();
            var off = r.Add(r.P.Pos + new Vector2(3f, 2f));   // 정면에서 56도
            r.Shoot();
            r.Step(90);
            Assert.AreEqual(1, r.Hits(off), "적을 쫓는다");

            var k = Rig.Make(5, "block.core.eye_seeker");
            k.ClearDummy();
            var weak = k.AddSlime(k.P.Pos + S * 2.5f, 1f);
            var other = k.Add(k.P.Pos + new Vector2(1.5f, 5f));
            k.Shoot();
            k.Step(120);
            var split = k.Of(SimEventType.StyleSplit);
            Assert.AreEqual(1, split.Count, "처치하면 갈라진다");
            Assert.AreEqual(St("block.core.eye_seeker").SplitCount, split[0].A);
            Assert.AreEqual(1, split[0].B, "세대 1");
            Assert.Greater(k.Hits(other), 0, "갈라진 탄이 다음 적을 쫓는다");
            Assert.AreEqual(0, k.Of(SimEventType.StyleSplit).FindAll(e => e.B > 1).Count, "한 세대까지만");
        }

        // ================================================================== 변형 블록 결합

        [Test]
        public void AS_Mods__SplitTwo_FiresTwoShells()
        {
            var r = Rig.Make(1, "block.core.ember_mortar", 3f, new List<string> { Ids.Block.SplitTwo });
            r.Shoot();
            Assert.AreEqual(2, r.Of(SimEventType.StyleLobLaunched).Count);
        }

        [Test]
        public void AS_Mods__DoubleSize_DoublesBlastRadius()
        {
            var st = St("block.core.ember_mortar");
            float mul = Db.Block(Ids.Block.DoubleSize).Params.RadiusMultiplier;
            var r = Rig.Make(1, "block.core.ember_mortar", 3f, new List<string> { Ids.Block.DoubleSize });
            r.Shoot();
            r.Step(Ticks(st.FlightSeconds) + 1);
            Assert.AreEqual(st.BlastRadius * mul, r.Of(SimEventType.StyleBlast)[0].F, Eps, "크기 → 폭발 반경");
        }

        [Test]
        public void AS_Mods__PierceOnAlreadyPiercingStyle_BecomesDamageBonus()
        {
            var st = St("block.core.shadow_wave");
            var plain = Rig.Make(5, "block.core.shadow_wave", 2f);
            var pierced = Rig.Make(5, "block.core.shadow_wave", 2f, new List<string> { Ids.Block.Pierce });
            plain.Shoot();
            pierced.Shoot();
            var a = plain.W.Projectiles.Find(p => p.Block.Id == "block.core.shadow_wave");
            var b = pierced.W.Projectiles.Find(p => p.Block.Id == "block.core.shadow_wave");
            var pb = Db.Block(Ids.Block.Pierce);
            float expected = (1f + pb.DamageBonus) * (1f + st.PierceBonusPerCount * pb.Params.PierceCount);
            Assert.AreEqual(expected, b.DamageMul / a.DamageMul, 1e-4f, "관통 블록 = 피해 +10% × 꿰뚫음 보너스");
            Assert.AreEqual(0, b.PierceLeft);
        }

        [Test]
        public void AS_Mods__WallBounceOnMortar_HopsOnceMoreAndBlastsAgain()
        {
            var st = St("block.core.ember_mortar");
            var r = Rig.Make(1, "block.core.ember_mortar", 3f, new List<string> { Ids.Block.WallBounce });
            r.Shoot();
            r.Step(Ticks(st.FlightSeconds) + Ticks(st.BounceFlightSeconds) + 2);
            Assert.AreEqual(2, r.Of(SimEventType.StyleLobLaunched).Count);
            Assert.AreEqual(1, r.Of(SimEventType.StyleLobLaunched)[1].B, "두 번째 튐");
            Assert.AreEqual(2, r.Of(SimEventType.StyleBlast).FindAll(e => e.B == (int)StyleBlastKind.MortarLand).Count);
        }

        [Test]
        public void AS_Mods__HomingWidensStrikeAim()
        {
            var st = St("block.core.sky_strike");
            Vector2 target;
            {
                var r = Rig.Make(2, "block.core.sky_strike");
                r.ClearDummy();
                var side = r.Add(r.P.Pos + new Vector2(3f, 2f));   // 56도: 기본 원뿔(25도) 밖
                target = side.Pos;
                r.Shoot();
                Assert.AreNotEqual(target.x, r.Of(SimEventType.StyleStrikeWarned)[0].Pos.x, "유도 없으면 겨누지 않는다");
            }
            {
                var r = Rig.Make(2, "block.core.sky_strike", 3f, new List<string> { Ids.Block.Homing });
                r.ClearDummy();
                var side = r.Add(r.P.Pos + new Vector2(3f, 2f));
                r.Shoot();
                Assert.AreEqual(side.Pos.x, r.Of(SimEventType.StyleStrikeWarned)[0].Pos.x, Eps, "유도 → 겨누는 범위가 넓어진다");
            }
            Assert.Greater(st.AimConeDegrees, 0f);
        }

        [Test]
        public void AS_Mods__TriggerOnMortar_FiresChildFromBlastCenter()
        {
            var st = St("block.core.ember_mortar");
            var r = Rig.Make(1, "block.core.ember_mortar", 3f, new List<string> { Ids.Block.OnHitTrigger, Ids.Block.MagicBolt });
            r.Shoot();
            r.Step(Ticks(st.FlightSeconds) + 1);
            var child = r.Of(SimEventType.ProjectileSpawned).Find(e => e.Id == Ids.Block.MagicBolt);
            Assert.AreEqual(1, child.B, "하위 스코프(깊이 1)");
            var land = r.Of(SimEventType.StyleBlast)[0];
            Assert.AreEqual(land.Tick, child.Tick, "첫 피해 순간");
            Assert.AreEqual(land.Pos.y, child.Pos.y, Eps);
        }

        [Test]
        public void AS_Mods__PierceOnChainBolt_AddsAHop()
        {
            var r = Rig.Make(2, "block.core.chain_bolt", 3f, new List<string> { Ids.Block.Pierce });
            var a = r.Add(r.Dummy.Pos + new Vector2(1.6f, 0f));
            var b = r.Add(a.Pos + new Vector2(1.6f, 0f));
            var c = r.Add(b.Pos + new Vector2(1.6f, 0f));
            r.Shoot();
            Assert.AreEqual(1, r.Hits(c), "관통 블록 하나 = 옮겨 붙기 +1");
            Assert.AreEqual(Db.Block("block.core.chain_bolt").Params.ChainHops + 2, r.Of(SimEventType.ChainLightningArc).Count);
        }

        [Test]
        public void AS_Mods__FrostOnStyle_SlowsThroughTheBlockRule()
        {
            var r = Rig.Make(1, "block.core.ember_mortar", 3f, new List<string> { Ids.Block.Frost });
            r.Shoot();
            r.Step(Ticks(St("block.core.ember_mortar").FlightSeconds) + 1);
            Assert.Greater(r.Dummy.Slows[(int)SlowSlot.Frost].Strength, 0f, "서리는 블록 그대로 맞은 적에게");
        }

        [Test]
        public void AS_Arts__ExecuteFromArtIsScaledToBoltUnit()
        {
            // 비기(마력 방출 등)가 트리를 돌릴 때: 핵 블록 피해 × art_damage_scale(발사 1회 피해가 큰 유형이 비기로 몇 배가 되지 않게).
            var r = Rig.Make(1, "block.core.ember_mortar");
            var req = new ExecuteRequest
            {
                OwnerSlot = 0, Tree = r.P.Spell, Pos = r.P.Pos, DirDegrees = Dir8Util.ToDegrees(Dir8.S), DamageMul = 1f, HeatExempt = true,
                Source = ShotSource.ManaDischarge, ShotId = -1, CharacterMul = 1f
            };
            r.W.Mods.Spells.Execute(r.W, req);
            var art = r.W.Projectiles[r.W.Projectiles.Count - 1];
            r.Shoot();
            var fire = r.W.Projectiles[r.W.Projectiles.Count - 1];
            Assert.AreEqual(St("block.core.ember_mortar").ArtDamageScale, art.DamageMul / fire.DamageMul, 1e-4f);
        }

        // ================================================================== 결정론

        [Test]
        public void AS_Determinism__EveryStyle_TwoRunsSameSeed_SameDigestEverySecond()
        {
            for (int c = 1; c <= 5; c++)
                foreach (var id in AttackStyleKit.Styles[c - 1])
                {
                    var a = AttackStyleKit.RunScripted(AttackStyleKit.Tuned, c, id, 600, 11UL, true);
                    var b = AttackStyleKit.RunScripted(AttackStyleKit.Tuned, c, id, 600, 11UL, true);
                    CollectionAssert.AreEqual(a, b, id);
                }
        }

        [Test]
        public void AS_Determinism__SplitRunEqualsStraightRun()
        {
            // 솔로 빌드에는 세계 스냅숏 기능이 없다(Docs/00 — Fix64·해시 없음). 대신 "같은 씨앗 + 같은 입력 기록을 처음부터 다시 돌려 중간에서
            // 이어 가기"가 한 번에 돈 것과 같은지 본다: 유형 상태(TimerTick·StepIndex·화상 겹 …)가 전부 세계에 있고 모듈 객체에 남지 않는다는 뜻.
            for (int c = 1; c <= 5; c++)
                foreach (var id in AttackStyleKit.Styles[c - 1])
                {
                    var mods = SimModuleSet.CreateDefault();
                    var cfg = AttackStyleKit.PracticeConfig(AttackStyleKit.Tuned, c, id, RunMode.Practice, 5UL);
                    var w1 = new SimWorld(AttackStyleKit.Tuned, mods, 5UL);
                    w1.StartRun(cfg);
                    // 같은 모듈 객체를 쓰는 두 번째 세계를 섞어 돌린다 — 모듈에 세계 상태가 남으면 어긋난다.
                    var w2 = new SimWorld(AttackStyleKit.Tuned, mods, 5UL);
                    w2.StartRun(AttackStyleKit.PracticeConfig(AttackStyleKit.Tuned, c, id, RunMode.Practice, 5UL));
                    var solo = AttackStyleKit.Start(AttackStyleKit.Tuned, AttackStyleKit.PracticeConfig(AttackStyleKit.Tuned, c, id, RunMode.Practice, 5UL));
                    foreach (var w in new[] { w1, w2, solo })
                    {
                        var d = AttackStyleKit.Dummy(w);
                        w.P0.Pos = d.Pos - S * AttackStyleKit.DistanceFor(id);
                        AttackStyleKit.AddDummy(w, d.Pos + new Vector2(-0.8f, 0.7f));
                        w.EnqueueInput(0, new InputEvent(LogicalKey.Fire, true, SimConst.TickStartU(w.Tick)));
                    }
                    for (int t = 0; t < 400; t++)
                    {
                        w1.Step(); w1.Events.Clear();
                        w2.Step(); w2.Events.Clear();
                        solo.Step(); solo.Events.Clear();
                    }
                    Assert.AreEqual(AttackStyleKit.Digest(solo), AttackStyleKit.Digest(w1), id);
                    Assert.AreEqual(AttackStyleKit.Digest(solo), AttackStyleKit.Digest(w2), id);
                }
        }
    }
}
