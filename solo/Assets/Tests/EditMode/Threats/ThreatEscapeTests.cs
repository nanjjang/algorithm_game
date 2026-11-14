using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Threats;

namespace Wizard.Solo.Tests.Threats
{
    /// <summary>
    /// R5 안전 경로 검사. 05 요약서 2.2절, 원문 S5 11.9절 TV-R5-01~28
    /// (플레이어 = 칸 (24, 14)의 중심 (12.25, 7.25), 빈 전장. 기대값은 전부 그 표의 "통과/실패").
    /// </summary>
    public class ThreatEscapeTests
    {
        static readonly Vector2 C = ThreatWorld.Center;
        static readonly Vector2 East = new Vector2(1, 0);

        ThreatWorld _t;

        [SetUp]
        public void SetUp() { _t = ThreatWorld.Create(C); }

        bool Check(params ZoneSpec[] zones) => _t.Threats.CheckEscape(_t.W, zones, 0);

        static AttackDef Attack(string enemyId, string key)
        {
            var def = TestContent.Db.Enemy(enemyId);
            Assert.IsNotNull(def, enemyId);
            var a = def.Attacks.Find(x => x.Key == key);
            Assert.IsNotNull(a, enemyId + "/" + key);
            return a;
        }

        /// <summary>공격 정의를 구역 명세로 옮기고 자리·방향만 시험 값으로 박는다.</summary>
        static ZoneSpec FromData(string enemyId, string key, Vector2 pos, Vector2 dir)
        {
            var s = ThreatSpecs.FromAttack(Attack(enemyId, key));
            s.Anchor = ZoneAnchor.Fixed;
            s.AimAtPlayer = false;
            s.Pos = pos;
            s.Dir = dir;
            return s;
        }

        void FrostEverywhere()
        {
            _t.Threats.SpawnHazard(_t.W, new HazardSpec
            {
                Kind = HazardKind.FrostField, Shape = ZoneShape.Circle, Pos = C, Radius = 40f, SlowOnly = true
            });
        }

        // ---- 단독 공격 14종: 전부 통과 (TV-R5-01 ~ 14)

        [Test]
        public void S05_TV_R5_01__WolfDashAlone_Passes()
        {
            var s = FromData(Ids.Enemy.WolfBramble, "dash", C + new Vector2(-4, 0), East);
            Assert.AreEqual(ZoneShape.Band, s.Shape);
            Assert.AreEqual(1.0f, s.Width, 1e-4f);
            Assert.AreEqual(6f, s.Length, 1e-4f);
            Assert.AreEqual(30, s.TelegraphTicks);
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_02__GolemSlamAtPlayer_Passes()
        {
            var s = FromData(Ids.Enemy.GolemMenhir, "slam", C, East);
            Assert.AreEqual(2.5f, s.Radius, 1e-4f);
            Assert.AreEqual(66, s.TelegraphTicks);
            Assert.AreEqual(ZoneAnchor.Fixed, s.Anchor);
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_03__GolemSweepFromInnerPoint_Passes()
        {
            // 플레이어가 꼭짓점에서 ρ = 3 / (1 + sin 55°) 거리의 이등분선 위
            float rho = 3f / (1f + Mathf.Sin(55f * Mathf.Deg2Rad));
            var s = FromData(Ids.Enemy.GolemMenhir, "sweep", C - new Vector2(rho, 0), East);
            Assert.AreEqual(ZoneShape.Fan, s.Shape);
            Assert.AreEqual(3.0f, s.Radius, 1e-4f);
            Assert.AreEqual(110f, s.FanDegrees, 1e-4f);
            Assert.AreEqual(48, s.TelegraphTicks);
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_04__LanternBugIgnite_Passes()
        {
            var s = FromData(Ids.Enemy.BugLantern, "ignite", C, East);
            Assert.AreEqual(1.8f, s.Radius, 1e-4f);
            Assert.AreEqual(54, s.TelegraphTicks);
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_05__KingLanternCircle_Passes()
        {
            Assert.IsTrue(Check(ThreatWorld.Circle(C, 2.6f, 66)));
        }

        [Test]
        public void S05_TV_R5_06__HogAimLineAfterLock_Passes()
        {
            // 조준선 폭 0.4, 전장 끝까지, 고정 뒤 24틱
            var a = Attack(Ids.Enemy.HogNeedle, "snipe");
            var s = FromData(Ids.Enemy.HogNeedle, "snipe", C + new Vector2(-8, 0), East);
            Assert.AreEqual(ZoneShape.AimLine, s.Shape);
            Assert.AreEqual(0.4f, s.Width, 1e-4f);
            Assert.AreEqual(24, SimConst.Ticks(a.LockSeconds));
            Assert.AreEqual(24, s.LockLastTicks);
            s.TelegraphTicks = s.LockLastTicks;
            s.LockLastTicks = 0;
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_07__WeaselErupt_Passes()
        {
            var s = FromData(Ids.Enemy.WeaselPot, "erupt", C, East);
            Assert.AreEqual(1.2f, s.Radius, 1e-4f);
            Assert.AreEqual(42, s.TelegraphTicks);
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_08__VineSeed_Passes()
        {
            var s = FromData(Ids.Enemy.VineThorn, "seed", C, East);
            Assert.AreEqual(1.5f, s.Radius, 1e-4f);
            Assert.AreEqual(60, s.TelegraphTicks);
            Assert.IsNotNull(s.LeavesHazard);
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_09__BoarCharge_Passes()
        {
            var s = FromData(Ids.Enemy.BoarTusk, "charge", C + new Vector2(-4, 0), East);
            Assert.AreEqual(1.6f, s.Width, 1e-4f);
            Assert.AreEqual(54, s.TelegraphTicks);
            Assert.LessOrEqual(s.Length, 0f);   // 벽까지
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_10__ToadTongue_Passes()
        {
            var s = FromData(Ids.Enemy.ToadDrum, "tongue", C + new Vector2(-2, 0), East);
            Assert.AreEqual(0.8f, s.Width, 1e-4f);
            Assert.AreEqual(3.5f, s.Length, 1e-4f);
            Assert.AreEqual(36, s.TelegraphTicks);
            Assert.IsTrue(Check(s));
        }

        [Test]
        public void S05_TV_R5_11__BatQueenDive_Passes()
        {
            Assert.IsTrue(Check(ThreatWorld.Band(C + new Vector2(-3, 0), East, 0.8f, 5f, 30)));
        }

        [Test]
        public void S05_TV_R5_12__LastBreathCircle_Passes()
        {
            var dc = TestContent.Db.Modifier(Ids.Whim.LastBreath).DeathCircle;
            Assert.AreEqual(1.2f, dc.Radius, 1e-4f);
            Assert.AreEqual(48, SimConst.Ticks(dc.TelegraphSeconds));
            Assert.IsTrue(Check(ThreatWorld.Circle(C, dc.Radius, SimConst.Ticks(dc.TelegraphSeconds))));
        }

        [Test]
        public void S05_TV_R5_13__WeaselMatriarchFourBands_PassesDiagonally()
        {
            Assert.IsTrue(Check(
                ThreatWorld.Band(C, new Vector2(1, 0), 0.8f, 3f, 36),
                ThreatWorld.Band(C, new Vector2(0, 1), 0.8f, 3f, 36),
                ThreatWorld.Band(C, new Vector2(-1, 0), 0.8f, 3f, 36),
                ThreatWorld.Band(C, new Vector2(0, -1), 0.8f, 3f, 36)));
        }

        [Test]
        public void S05_TV_R5_14__WolfDashDiagonal_Passes()
        {
            Vector2 d = new Vector2(1, 1).normalized;
            Assert.IsTrue(Check(FromData(Ids.Enemy.WolfBramble, "dash", C - d * 4f, d)));
        }

        // ---- 겹친 구역 (TV-R5-15 ~ 19)

        [Test]
        public void S05_TV_R5_15__CircleSurroundedByEight_Fails()
        {
            var zones = new List<ZoneSpec> { ThreatWorld.Circle(C, 1.8f, 54) };
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                zones.Add(ThreatWorld.Circle(C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f, 1.5f, 54));
            }
            Assert.IsFalse(Check(zones.ToArray()));
        }

        [Test]
        public void S05_TV_R5_16__TwoSlamsSamePlace_Passes()
        {
            Assert.IsTrue(Check(ThreatWorld.Circle(C, 2.5f, 66), ThreatWorld.Circle(C, 2.5f, 66)));
        }

        [Test]
        public void S05_TV_R5_17__SlamWithBoarBandsAboveAndBelow_Passes()
        {
            Assert.IsTrue(Check(
                ThreatWorld.Circle(C, 2.5f, 66),
                ThreatWorld.Band(new Vector2(0, C.y - 3f), East, 1.6f, 0f, 66),
                ThreatWorld.Band(new Vector2(0, C.y + 3f), East, 1.6f, 0f, 66)));
        }

        [Test]
        public void S05_TV_R5_18__SlamBoxedByFourWideBands_Fails()
        {
            Vector2 south = new Vector2(0, 1);
            Assert.IsFalse(Check(
                ThreatWorld.Circle(C, 2.5f, 66),
                ThreatWorld.Band(new Vector2(0, C.y - 2f), East, 2.0f, 0f, 66),
                ThreatWorld.Band(new Vector2(0, C.y + 2f), East, 2.0f, 0f, 66),
                ThreatWorld.Band(new Vector2(C.x - 2f, 0), south, 2.0f, 0f, 66),
                ThreatWorld.Band(new Vector2(C.x + 2f, 0), south, 2.0f, 0f, 66)));
        }

        [Test]
        public void S05_TV_R5_19__SlamInCorner_Passes()
        {
            // 플레이어 칸 (1, 1) = 구석 (0.75, 0.75)
            _t.P.Pos = new Vector2(0.75f, 0.75f);
            Assert.IsTrue(Check(ThreatWorld.Circle(_t.P.Pos, 2.5f, 66)));
        }

        // ---- 서리밭 칸 (TV-R5-20 ~ 22)

        [Test]
        public void S05_TV_R5_20__IcePillarOnFrost_Passes()
        {
            FrostEverywhere();
            Assert.IsTrue(Check(ThreatWorld.Circle(C, 1.0f, 48)));
        }

        [Test]
        public void S05_TV_R5_21__FrostSwing72Ticks_Passes()
        {
            FrostEverywhere();
            float rho = 5f / (1f + Mathf.Sin(45f * Mathf.Deg2Rad));
            Assert.IsTrue(Check(ThreatWorld.Fan(C - new Vector2(rho, 0), East, 5f, 90f, 72)));
        }

        [Test]
        public void S05_TV_R5_22__FrostSwing60Ticks_Fails()
        {
            FrostEverywhere();
            float rho = 5f / (1f + Mathf.Sin(45f * Mathf.Deg2Rad));
            Assert.IsFalse(Check(ThreatWorld.Fan(C - new Vector2(rho, 0), East, 5f, 90f, 60)));
        }

        [Test]
        public void S05_TV_R5_21b__SameSwingOffFrost_PassesAt60()
        {
            // 서리밭이 아니면 같은 휘두르기가 1.0초로 충분하다(8.5절 표의 "1.0초, 서리밭 안이면 1.2초").
            float rho = 5f / (1f + Mathf.Sin(45f * Mathf.Deg2Rad));
            Assert.IsTrue(Check(ThreatWorld.Fan(C - new Vector2(rho, 0), East, 5f, 90f, 60)));
        }

        // ---- 보스 (TV-R5-23, 24)

        [TestCase(54, true)]
        [TestCase(48, false)]
        public void S05_TV_R5_23__KilnBoarStomp(int ticks, bool passes)
        {
            Assert.AreEqual(passes, Check(ThreatWorld.Circle(C, 2.0f, ticks)));
        }

        [Test]
        public void S05_TV_R5_24__SporeQueenLeap_Passes()
        {
            Assert.IsTrue(Check(ThreatWorld.Circle(C, 3.0f, 72)));
        }

        // ---- 보정·결정성·예외 (TV-R5-25 ~ 28)

        [Test]
        public void S05_TV_R5_25__WithoutQuantizeFix_WolfDashFails()
        {
            // 보정 없이 글자 그대로(구역 + 0.15칸)면 단독 늑대 돌진도 막힌다 — 보정의 근거(C-10)
            var s = FromData(Ids.Enemy.WolfBramble, "dash", C + new Vector2(-4, 0), East);
            var geoms = new List<ShapeGeom>
            {
                new ShapeGeom { Shape = s.Shape, Pos = s.Pos, Dir = s.Dir, Width = s.Width, Length = s.Length }
            };
            var d = _t.Threats.Data(_t.W);
            Assert.IsFalse(_t.Threats.EscapeOk(_t.W, d, new[] { s }, geoms, -0.15f));
            Assert.IsTrue(_t.Threats.EscapeOk(_t.W, d, new[] { s }, geoms, d.Config.R5Shrink));
        }

        [Test]
        public void S05_TV_R5_26__SameInputTwice_SameResultAndHorizon()
        {
            var zones = new[] { ThreatWorld.Circle(C, 1.8f, 54), ThreatWorld.Circle(C + new Vector2(2.6f, 0), 1.5f, 54) };
            bool first = Check(zones);
            int horizon = _t.Threats.Data(_t.W).Solver.LastHorizon;
            var other = ThreatWorld.Create(C);
            bool second = other.Threats.CheckEscape(other.W, zones, 0);
            Assert.AreEqual(first, second);
            Assert.AreEqual(horizon, other.Threats.Data(other.W).Solver.LastHorizon);
            // 지평선 K = max(kz) + 6, kz = floor(54 / 3) = 18
            Assert.AreEqual(24, horizon);
        }

        [Test]
        public void S05_TV_R5_27__OnlyZonesInsideReactionWindow_Passes()
        {
            // kz = floor(17 / 3) = 5 ≤ 5 → 검사에서 빠진다. 18틱이면 kz = 6이라 검사하고, 3.0칸 원은 못 벗어난다.
            Assert.IsTrue(Check(ThreatWorld.Circle(C, 3.0f, 17)));
            Assert.IsFalse(Check(ThreatWorld.Circle(C, 3.0f, 18)));
        }

        [Test]
        public void S05_TV_R5_28__StandingInThornField_StartCellMayLeave()
        {
            // 플레이어가 가시밭 가장자리 안쪽에 서 있다(시작 칸은 막힘). 가시밭 밖으로 가는 길이 있으면 통과.
            _t.Threats.SpawnHazard(_t.W, new HazardSpec
            {
                Kind = HazardKind.ThornField, Shape = ZoneShape.Circle, Pos = C + new Vector2(-1.4f, 0), Radius = 1.5f, BaseDamage = 1
            });
            Assert.IsTrue(Check(ThreatWorld.Circle(C, 1.5f, 60)));
        }

        // ---- 규칙 확인(표 밖)

        [Test]
        public void R5__HazardCellsAreWalls_NoPathThroughThem()
        {
            // 남는 위험 구역은 지나갈 수 없는 칸이다: 가시밭 한가운데(이웃 칸이 전부 막힘)에서는 새 원을 벗어날 길이 없다.
            _t.Threats.SpawnHazard(_t.W, new HazardSpec
            {
                Kind = HazardKind.ThornField, Shape = ZoneShape.Circle, Pos = C, Radius = 1.5f, BaseDamage = 1
            });
            Assert.IsFalse(Check(ThreatWorld.Circle(C, 1.5f, 60)));
        }

        [Test]
        public void R5__ObstaclesBlockCells()
        {
            // 구석의 플레이어를 바위 둘이 가두면(이웃 칸이 전부 막힘) 길이 없다. 바위가 없으면 TV-R5-19처럼 통과.
            _t.P.Pos = new Vector2(0.25f, 0.25f);
            var circle = ThreatWorld.Circle(_t.P.Pos, 1.2f, 48);
            Assert.IsTrue(Check(circle));
            _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec { Kind = "rock", Pos = new Vector2(1.0f, 0.25f), Radius = 0.5f, BlocksEnemyShots = true });
            _t.Threats.SpawnObstacle(_t.W, new ObstacleSpec { Kind = "rock", Pos = new Vector2(0.25f, 1.0f), Radius = 0.5f, BlocksEnemyShots = true });
            Assert.IsFalse(Check(circle));
        }

        [Test]
        public void R5__HarmlessZonesAndNoPlayers_AlwaysPass()
        {
            var harmless = ThreatWorld.Circle(C, 3.0f, 18);
            harmless.Harmless = true;
            Assert.IsTrue(Check(harmless));
            _t.P.Downed = true;
            Assert.IsTrue(Check(ThreatWorld.Circle(C, 3.0f, 18)));
        }

        [Test]
        public void R5__ExistingZonesAreIncluded()
        {
            // 떠 있는 구역 8개(반경 2.6 위 원 1.5)가 이미 있으면 가운데 원 1.8은 시작하지 못한다(TV-R5-15의 대기 줄 꼴).
            var req = new TelegraphRequest { Bypass = true, CastId = 1 };
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                req.Zones.Add(ThreatWorld.Circle(C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f, 1.5f, 54));
            }
            _t.Threats.RequestTelegraph(_t.W, req);
            _t.Threats.Stage8_Queue(_t.W);
            Assert.AreEqual(8, _t.W.Zones.Count);
            Assert.IsFalse(Check(ThreatWorld.Circle(C, 1.8f, 54)));
        }
    }
}
