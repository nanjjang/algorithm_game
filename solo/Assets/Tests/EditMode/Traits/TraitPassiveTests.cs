using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Sim;

namespace Wizard.Solo.Tests.Traits
{
    /// <summary>캐릭터 패시브(02 요약서 8.3절, 05 요약서 5절): 끓어넘침, 피뢰침 체질, 살얼음, 그림자 계약.</summary>
    public class TraitPassiveTests
    {
        // ------------------------------------------------------------ 그림자 계약

        // 02 요약서 8.3절: 캐릭터 배율 = 1 + 0.03 × k, k = 0..7 → 1.00 ~ 1.21.
        [TestCase(0, 1.00f)]
        [TestCase(1, 1.03f)]
        [TestCase(2, 1.06f)]
        [TestCase(3, 1.09f)]
        [TestCase(4, 1.12f)]
        [TestCase(5, 1.15f)]
        [TestCase(6, 1.18f)]
        [TestCase(7, 1.21f)]
        public void S04_4_5__ShadowContractMul(int crystals, float expected)
        {
            var rig = new TraitRig("char.5");
            Assert.AreEqual(expected, rig.Traits.CharacterMul(rig.W, rig.P, crystals), 1e-5f);
        }

        [Test]
        public void S04_4_5__ShadowContractOnlyForChar5AndDebtCountsZero()
        {
            for (int n = 1; n <= 4; n++)
            {
                var other = new TraitRig("char." + n);
                Assert.AreEqual(1f, other.Traits.CharacterMul(other.W, other.P, 7), 1e-6f);
            }
            var rig = new TraitRig("char.5");
            Assert.AreEqual(1f, rig.Traits.CharacterMul(rig.W, rig.P, -2), 1e-6f);
        }

        // ------------------------------------------------------------ 끓어넘침

        // 02 요약서 8.3절: 잠금에 들어가는 순간 반경 2.5칸 안(거리 ≤ 2.5 포함)의 적에게 1.5T 화염 + 넉백 2칸.
        [Test]
        public void S04_4_1__BoiloverBoundaryInclusiveDamageAndKnockback()
        {
            var rig = new TraitRig("char.1");
            var edge = rig.AddEnemy(14.5f, 7f);          // 거리 2.5 (경계)
            var outside = rig.AddEnemy(12f, 9.75f);      // 거리 2.75
            var near = rig.AddEnemy(12f, 6f);            // 거리 1

            rig.Traits.OnHeatLockEntered(rig.W, rig.P, false);

            Assert.AreEqual(2, rig.Spells.Direct.Count);
            Assert.AreEqual(edge.Id, rig.Spells.Direct[0].TargetEnemyId, "id 오름차순");
            Assert.AreEqual(near.Id, rig.Spells.Direct[1].TargetEnemyId);
            for (int i = 0; i < 2; i++)
            {
                var d = rig.Spells.Direct[i];
                Assert.AreEqual(15f, d.Amount, 1e-4f);                   // 1.5 × T(10)
                Assert.AreEqual(Ids.Element.Fire, d.Element);
                Assert.AreEqual(DamageOrigin.Passive, d.Origin);
                Assert.AreEqual(Ids.Passive.Overflow, d.SourceId);
                Assert.AreEqual(0, d.AttackerSlot);
            }

            Assert.AreEqual(2, rig.Enemies.Cc.Count);
            Assert.AreEqual(edge.Id, rig.Enemies.Cc[0].EnemyId);
            Assert.AreEqual(CcKind.Knockback, rig.Enemies.Cc[0].Kind);
            Assert.AreEqual(2f, rig.Enemies.Cc[0].Amount, 1e-6f);
            Assert.AreEqual(CcSource.Overflow, rig.Enemies.Cc[0].Source);
            Assert.AreEqual(1f, rig.Enemies.Cc[0].Dir.x, 1e-5f);         // 플레이어에게서 멀어지는 쪽
            Assert.AreEqual(0f, rig.Enemies.Cc[0].Dir.y, 1e-5f);
            Assert.AreEqual(0f, rig.Enemies.Cc[1].Dir.x, 1e-5f);
            Assert.AreEqual(-1f, rig.Enemies.Cc[1].Dir.y, 1e-5f);

            Assert.AreEqual(1, rig.CountEvents(SimEventType.BoiloverTriggered));
            Assert.AreEqual(0, rig.W.Events[rig.W.Events.Count - 1].Slot);
            Assert.AreNotEqual(outside.Id, rig.Spells.Direct[0].TargetEnemyId);
        }

        // 05 요약서 5절 꼬리표 표: 끓어넘침 넉백은 일반·비행 2 / 엘리트 1 / 중량·보스·뿌리 없음. 변환은 Enemies.ApplyCc가 하므로
        // Traits는 어느 꼬리표든 기본 2칸과 출처 Overflow를 그대로 넘긴다(가짜 EnemyModule로 인자 확인).
        [TestCase(EnemyTags.None)]
        [TestCase(EnemyTags.Elite)]
        [TestCase(EnemyTags.Heavy)]
        [TestCase(EnemyTags.Boss)]
        [TestCase(EnemyTags.Rooted)]
        [TestCase(EnemyTags.Flying)]
        public void S05_6__BoiloverKnockbackPassesBaseAmountForEveryTag(EnemyTags tags)
        {
            var rig = new TraitRig("char.1");
            var e = rig.AddEnemy(13f, 7f, tags);
            rig.Traits.OnHeatLockEntered(rig.W, rig.P, false);
            Assert.AreEqual(1, rig.Spells.Direct.Count);
            Assert.AreEqual(1, rig.Enemies.Cc.Count);
            Assert.AreEqual(e.Id, rig.Enemies.Cc[0].EnemyId);
            Assert.AreEqual(CcKind.Knockback, rig.Enemies.Cc[0].Kind);
            Assert.AreEqual(2f, rig.Enemies.Cc[0].Amount, 1e-6f);
            Assert.AreEqual(CcSource.Overflow, rig.Enemies.Cc[0].Source);
        }

        // 00 문서 D-04: 불안정 핵 자폭 잠금은 끓어넘침을 일으키지 않는다.
        [Test]
        public void S01_7_6_6__BoiloverNotTriggeredByUnstableCoreLock()
        {
            var rig = new TraitRig("char.1");
            rig.AddEnemy(13f, 7f);
            rig.Traits.OnHeatLockEntered(rig.W, rig.P, true);
            Assert.AreEqual(0, rig.Spells.Direct.Count);
            Assert.AreEqual(0, rig.Enemies.Cc.Count);
            Assert.AreEqual(0, rig.CountEvents(SimEventType.BoiloverTriggered));
        }

        [Test]
        public void S04_4_1__BoiloverOnlyForOverflowPassive()
        {
            for (int n = 2; n <= 5; n++)
            {
                var rig = new TraitRig("char." + n);
                rig.AddEnemy(13f, 7f);
                rig.Traits.OnHeatLockEntered(rig.W, rig.P, false);
                Assert.AreEqual(0, rig.Spells.Direct.Count);
                Assert.AreEqual(0, rig.CountEvents(SimEventType.BoiloverTriggered));
            }
        }

        // 땅속 효과 없음, 죽어 가는 적 제외(공통 규칙 2), 이 피해로 죽은 적은 밀지 않는다. 적이 없어도 이벤트는 난다.
        [Test]
        public void S04_4_1__BoiloverSkipsBurrowedAndDyingEnemies()
        {
            var rig = new TraitRig("char.1");
            var burrowed = rig.AddEnemy(13f, 7f);
            burrowed.Burrowed = true;
            var dying = rig.AddEnemy(11f, 7f);
            dying.PendingDeath = true;
            var killedByIt = rig.AddEnemy(12f, 8f);
            rig.Spells.KillOnDirect.Add(killedByIt.Id);

            rig.Traits.OnHeatLockEntered(rig.W, rig.P, false);

            Assert.AreEqual(1, rig.Spells.Direct.Count);
            Assert.AreEqual(killedByIt.Id, rig.Spells.Direct[0].TargetEnemyId);
            Assert.AreEqual(0, rig.Enemies.Cc.Count);
            Assert.AreEqual(1, rig.CountEvents(SimEventType.BoiloverTriggered));

            var empty = new TraitRig("char.1");
            empty.Traits.OnHeatLockEntered(empty.W, empty.P, false);
            Assert.AreEqual(0, empty.Spells.Direct.Count);
            Assert.AreEqual(1, empty.CountEvents(SimEventType.BoiloverTriggered));
        }

        // ------------------------------------------------------------ 피뢰침 체질

        // 02 요약서 8.3절: 스침 인정마다 반경 5칸 안 가까운 적 최대 2체에게 각 1T 번개. 거리 오름차순.
        [Test]
        public void S04_4_2__LightningRodPicksNearestTwo()
        {
            var rig = new TraitRig("char.2");
            var far = rig.AddEnemy(15f, 7f);      // 3
            var near = rig.AddEnemy(13f, 7f);     // 1
            var mid = rig.AddEnemy(12f, 9f);      // 2

            rig.Traits.OnGrazeAwarded(rig.W, rig.P, 5);

            Assert.AreEqual(2, rig.Spells.Direct.Count);
            Assert.AreEqual(near.Id, rig.Spells.Direct[0].TargetEnemyId);
            Assert.AreEqual(mid.Id, rig.Spells.Direct[1].TargetEnemyId);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(10f, rig.Spells.Direct[i].Amount, 1e-4f);        // 1 × T(10)
                Assert.AreEqual(Ids.Element.Lightning, rig.Spells.Direct[i].Element);
                Assert.AreEqual(DamageOrigin.Passive, rig.Spells.Direct[i].Origin);
                Assert.AreEqual(Ids.Passive.LightningRod, rig.Spells.Direct[i].SourceId);
            }
            Assert.AreEqual(2, rig.CountEvents(SimEventType.LightningRodStrike));
            Assert.AreNotEqual(far.Id, rig.Spells.Direct[1].TargetEnemyId);
            Assert.AreEqual(0, rig.Enemies.Cc.Count, "군중 제어 없음");
        }

        // 같은 거리면 id 오름차순.
        [Test]
        public void S04_4_2__LightningRodTieBreaksByIdAscending()
        {
            var rig = new TraitRig("char.2");
            var a = rig.AddEnemy(14f, 7f);        // 2
            var b = rig.AddEnemy(10f, 7f);        // 2
            var c = rig.AddEnemy(12f, 9f);        // 2
            rig.Traits.OnGrazeAwarded(rig.W, rig.P, 1);
            Assert.AreEqual(2, rig.Spells.Direct.Count);
            Assert.AreEqual(a.Id, rig.Spells.Direct[0].TargetEnemyId);
            Assert.AreEqual(b.Id, rig.Spells.Direct[1].TargetEnemyId);
            Assert.Less(b.Id, c.Id);
        }

        // 1체뿐이면 1체, 없으면 없음. 경계(거리 = 5) 포함.
        [Test]
        public void S04_4_2__LightningRodOneNoneAndBoundary()
        {
            var one = new TraitRig("char.2");
            var edge = one.AddEnemy(15f, 11f);    // (3, 4) → 거리 5
            one.AddEnemy(12f, 12.25f);            // 5.25
            one.Traits.OnGrazeAwarded(one.W, one.P, 1);
            Assert.AreEqual(1, one.Spells.Direct.Count);
            Assert.AreEqual(edge.Id, one.Spells.Direct[0].TargetEnemyId);
            Assert.AreEqual(1, one.CountEvents(SimEventType.LightningRodStrike));

            var none = new TraitRig("char.2");
            none.Traits.OnGrazeAwarded(none.W, none.P, 1);
            Assert.AreEqual(0, none.Spells.Direct.Count);
            Assert.AreEqual(0, none.CountEvents(SimEventType.LightningRodStrike));
        }

        // 죽어 가는 적(PendingDeath)과 땅속은 대상이 아니다 — 더 가까워도 건너뛴다.
        [Test]
        public void S04_4_2__LightningRodSkipsPendingDeathAndBurrowed()
        {
            var rig = new TraitRig("char.2");
            var dying = rig.AddEnemy(12.5f, 7f);
            dying.PendingDeath = true;
            var burrowed = rig.AddEnemy(12f, 7.5f);
            burrowed.Burrowed = true;
            var a = rig.AddEnemy(14f, 7f);
            var b = rig.AddEnemy(12f, 10f);
            rig.Traits.OnGrazeAwarded(rig.W, rig.P, 1);
            Assert.AreEqual(2, rig.Spells.Direct.Count);
            Assert.AreEqual(a.Id, rig.Spells.Direct[0].TargetEnemyId);
            Assert.AreEqual(b.Id, rig.Spells.Direct[1].TargetEnemyId);
        }

        [Test]
        public void S04_4_2__LightningRodOnlyForChar2()
        {
            var rig = new TraitRig("char.1");
            rig.AddEnemy(13f, 7f);
            rig.Traits.OnGrazeAwarded(rig.W, rig.P, 1);
            Assert.AreEqual(0, rig.Spells.Direct.Count);
        }

        // ------------------------------------------------------------ 살얼음

        // 02 요약서 8.3절: 같은 적을 6번 맞히면 빙결 1.0초(60틱). 다섯 번까지는 아무 일도 없다.
        [Test]
        public void S04_4_4__ThinIceFreezesOnSixthHit()
        {
            var rig = new TraitRig("char.4");
            var e = rig.AddEnemy(13f, 7f);
            for (int i = 1; i <= 5; i++)
            {
                rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));
                Assert.AreEqual(i, e.ThinIceCount);
                Assert.AreEqual(0, rig.Enemies.Cc.Count);
            }
            rig.W.Tick = 100;
            rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));

            Assert.AreEqual(1, rig.Enemies.Cc.Count);
            Assert.AreEqual(e.Id, rig.Enemies.Cc[0].EnemyId);
            Assert.AreEqual(CcKind.Freeze, rig.Enemies.Cc[0].Kind);
            Assert.AreEqual(60f, rig.Enemies.Cc[0].Amount, 1e-6f);
            Assert.AreEqual(CcSource.ThinIce, rig.Enemies.Cc[0].Source);
            Assert.AreEqual(0, e.ThinIceCount);
            Assert.AreEqual(460, e.FreezeImmuneUntilTick);       // 빙결 시작부터 6초(360틱)
            Assert.AreEqual(1, rig.CountEvents(SimEventType.ThinIceFroze));
        }

        // 한 번 언 적은 6초 동안 다시 얼지 않고 횟수도 안 쌓인다. 끝나면 0부터 다시 센다.
        [Test]
        public void S04_4_4__ThinIceImmuneWindowIgnoresHitsThenCountsFromZero()
        {
            var rig = new TraitRig("char.4");
            var e = rig.AddEnemy(13f, 7f);
            for (int i = 0; i < 6; i++) rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));     // 틱 0에 빙결
            Assert.AreEqual(1, rig.Enemies.Cc.Count);

            rig.W.Tick = 359;
            for (int i = 0; i < 20; i++) rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));
            Assert.AreEqual(0, e.ThinIceCount);
            Assert.AreEqual(1, rig.Enemies.Cc.Count);

            rig.W.Tick = 360;
            for (int i = 1; i <= 5; i++)
            {
                rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));
                Assert.AreEqual(i, e.ThinIceCount);
            }
            Assert.AreEqual(1, rig.Enemies.Cc.Count);
            rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));
            Assert.AreEqual(2, rig.Enemies.Cc.Count);
            Assert.AreEqual(720, e.FreezeImmuneUntilTick);
        }

        // 적마다 따로 센다. 비기로 맞힌 것도 센다. 땅속은 효과 없음·횟수 유지.
        [Test]
        public void S04_4_4__ThinIcePerEnemyArtHitsCountBurrowedKeepsCount()
        {
            var rig = new TraitRig("char.4");
            var a = rig.AddEnemy(13f, 7f);
            var b = rig.AddEnemy(11f, 7f);
            for (int i = 0; i < 3; i++) rig.Traits.OnEnemyHit(rig.W, rig.Hit(a));
            for (int i = 0; i < 2; i++) rig.Traits.OnEnemyHit(rig.W, rig.Hit(a, DamageOrigin.Art));
            rig.Traits.OnEnemyHit(rig.W, rig.Hit(b));
            Assert.AreEqual(5, a.ThinIceCount);
            Assert.AreEqual(1, b.ThinIceCount);

            a.Burrowed = true;
            rig.Traits.OnEnemyHit(rig.W, rig.Hit(a));
            Assert.AreEqual(5, a.ThinIceCount);
            Assert.AreEqual(0, rig.Enemies.Cc.Count);

            a.Burrowed = false;
            rig.Traits.OnEnemyHit(rig.W, rig.Hit(a));
            Assert.AreEqual(1, rig.Enemies.Cc.Count);
            Assert.AreEqual(a.Id, rig.Enemies.Cc[0].EnemyId);
        }

        // 꼬리표별 변환(엘리트 0.5초, 중량·보스 감속)은 Enemies가 한다: Traits는 어느 꼬리표든 Freeze 60틱·출처 ThinIce로 청한다.
        [TestCase(EnemyTags.Elite)]
        [TestCase(EnemyTags.Heavy)]
        [TestCase(EnemyTags.Boss)]
        [TestCase(EnemyTags.Rooted)]
        [TestCase(EnemyTags.Flying)]
        public void S05_6__ThinIcePassesBaseFreezeForEveryTag(EnemyTags tags)
        {
            var rig = new TraitRig("char.4");
            var e = rig.AddEnemy(13f, 7f, tags);
            for (int i = 0; i < 6; i++) rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));
            Assert.AreEqual(1, rig.Enemies.Cc.Count);
            Assert.AreEqual(CcKind.Freeze, rig.Enemies.Cc[0].Kind);
            Assert.AreEqual(60f, rig.Enemies.Cc[0].Amount, 1e-6f);
            Assert.AreEqual(CcSource.ThinIce, rig.Enemies.Cc[0].Source);
        }

        [Test]
        public void S04_4_4__ThinIceOnlyForChar4()
        {
            var rig = new TraitRig("char.1");
            var e = rig.AddEnemy(13f, 7f);
            for (int i = 0; i < 12; i++) rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));
            Assert.AreEqual(0, e.ThinIceCount);
            Assert.AreEqual(0, rig.Enemies.Cc.Count);
        }

        // 검산(02 요약서 8.3절): 마력탄(9틱 간격)으로 한 적만 → 여섯 번째 적중에 언다(첫 적중 뒤 45틱 — 요약서의 "0.9초"는
        // 발사 6번 × 0.15초). 주기는 요약서 "6.9초"(= 내성 6초 + 여섯 발 0.9초 = 414틱) 이하이고 내성 6초 + 다섯 간격(405틱) 이상이다.
        [Test]
        public void S04_4_4__ThinIceCycleWithMagicBolt()
        {
            var rig = new TraitRig("char.4");
            var e = rig.AddEnemy(13f, 7f);
            int firstFreeze = -1, secondFreeze = -1;
            for (int t = 0; t <= 900; t += 9)
            {
                rig.W.Tick = t;
                int before = rig.Enemies.Cc.Count;
                rig.Traits.OnEnemyHit(rig.W, rig.Hit(e));
                if (rig.Enemies.Cc.Count > before)
                {
                    if (firstFreeze < 0) firstFreeze = t;
                    else if (secondFreeze < 0) secondFreeze = t;
                }
            }
            Assert.AreEqual(45, firstFreeze);
            Assert.GreaterOrEqual(secondFreeze - firstFreeze, 360 + 45);
            Assert.LessOrEqual(secondFreeze - firstFreeze, 414);
        }
    }
}
