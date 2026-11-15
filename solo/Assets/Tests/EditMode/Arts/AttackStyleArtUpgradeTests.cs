using System;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Tests.Spells;

namespace Wizard.Solo.Tests.Arts
{
    /// <summary>
    /// 고유 비기 강화(Docs/12-attack-styles.md 4절): 불가마 불기둥 고리, 벼락 질주 낙뢰 궤적, 베껴 쓰기 책장 소용돌이, 얼음 병풍 얼음 가시,
    /// 그림자 손 짓누름. 손잡이(art_upgrades.enabled)는 명세 콘텐츠에서 꺼져 있고 FunPass가 켠다 → 켠 콘텐츠로 ArtKit을 세운다.
    /// 기대값: 단계 수·틱·반경은 AttackStyles.json art_upgrades, 덧피해 합은 원래 비기 총량의 20% 이내(사용자 지시).
    /// </summary>
    public sealed class AttackStyleArtUpgradeTests
    {
        const float Eps = 1e-3f;
        const float T = 10f;   // ArtKit의 FakeSpells.StandardT

        static JObject Up(string artId) => AttackStyles.ArtUpgrade(AttackStyleKit.Tuned, artId);
        static int Ticks(float s) => s > 0f ? Mathf.Max(1, SimConst.Ticks(s)) : 0;

        /// <summary>켠 콘텐츠로 ArtKit을 만든다(ArtKit은 TestContent.Db를 읽으므로 잠시 바꾼다).</summary>
        static ArtKit Kit(string charId, int crystals)
        {
            var keep = TestContent.Db;
            TestContent.Db = AttackStyleKit.Tuned;
            try { return new ArtKit(charId, crystals); }
            finally { TestContent.Db = keep; }
        }

        static float SumTo(ArtKit kit, EnemyState e, int fromIndex)
        {
            float s = 0f;
            for (int i = fromIndex; i < kit.Spells.Directs.Count; i++)
                if (kit.Spells.Directs[i].TargetEnemyId == e.Id) s += kit.Spells.Directs[i].Amount;
            return s;
        }

        static int Stages(ArtKit kit, string artId, ArtStageKind kind)
        {
            int n = 0;
            foreach (var ev in kit.W.Events)
                if (ev.Type == SimEventType.ArtStage && ev.Id == artId && ev.A == (int)kind) n++;
            return n;
        }

        [Test]
        public void AS_Art__SpecContent_HasNoUpgrade()
        {
            Assert.IsNull(AttackStyles.ArtUpgrade(TestContent.Db, Ids.Art.Kiln), "명세 콘텐츠는 손잡이가 꺼져 있다");
            var kit = new ArtKit(Ids.Char.C1, 5);
            kit.AddEnemy(13.5f, 7f);
            kit.Seq("B, B+2304, K+2304");
            kit.Stage5(60);
            Assert.AreEqual(0, kit.CountEvents(SimEventType.ArtStage));
        }

        [Test]
        public void AS_Art__Kiln_PillarRingsRiseOutward_OncePerEnemy_Within20Percent()
        {
            var up = Up(Ids.Art.Kiln);
            var radii = (JArray)up["ring_radii"];
            var kit = Kit(Ids.Char.C1, 5);
            var inner = kit.AddEnemy(12.8f, 7f);   // 첫 고리
            var mid = kit.AddEnemy(13.5f, 7f);     // 둘째 고리(불가마 지대 안)
            kit.Seq("B, B+2304, K+2304");
            int cast = kit.W.Tick;
            kit.Stage5(Ticks((float)up["gap_seconds"]) * radii.Count + 1);
            Assert.AreEqual(radii.Count, Stages(kit, Ids.Art.Kiln, ArtStageKind.Rise), "고리 수");
            float amount = (float)up["damage"] * T;
            Assert.AreEqual(amount, SumTo(kit, inner, 0), Eps, "첫 고리에서 한 번");
            Assert.AreEqual(amount, SumTo(kit, mid, 0), Eps, "둘째 고리에서 한 번(고리마다 적 하나에 한 번)");
            Assert.AreEqual(cast, kit.Spells.DirectTicks[0], "내리치는 순간 첫 고리");

            int before = kit.Spells.Directs.Count;
            kit.Stage5(260);
            float kilnTotal = SumTo(kit, mid, before);
            var e = AttackStyleKit.Tuned.Art(Ids.Art.Kiln).Effect;
            Assert.AreEqual(e.Damage * T * (e.DurationSeconds / e.IntervalSeconds), kilnTotal, Eps, "원래 불가마 피해 그대로");
            Assert.LessOrEqual(amount, kilnTotal * 0.2f + Eps, "덧피해 ≤ 원래 총량의 20%");
        }

        [Test]
        public void AS_Art__BoltDash_StrikesFallAlongTheTrailInOrder_Within20Percent()
        {
            var up = Up(Ids.Art.BoltDash);
            int strikes = (int)(float)up["strikes"];
            var kit = Kit(Ids.Char.C2, 5);
            var onPath = kit.AddEnemy(14f, 7.6f);
            kit.Seq("L, F+2304, K+2304");
            int cast = kit.W.Tick;
            kit.Stage5();
            float dash = SumTo(kit, onPath, 0);
            Assert.AreEqual(AttackStyleKit.Tuned.Art(Ids.Art.BoltDash).Effect.Damage * T, dash, Eps, "원래 질주 피해");
            Assert.AreEqual(strikes, Stages(kit, Ids.Art.BoltDash, ArtStageKind.Warn), "떨어질 자리를 미리 알린다");
            int first = Ticks((float)up["first_delay_seconds"]), gap = Ticks((float)up["gap_seconds"]);
            kit.Stage5(first + gap * strikes + 2);
            Assert.AreEqual(strikes, Stages(kit, Ids.Art.BoltDash, ArtStageKind.Land));
            float extra = SumTo(kit, onPath, 1);
            Assert.AreEqual((float)up["damage"] * T, extra, Eps, "궤적 위 적은 한 번만");
            Assert.LessOrEqual(extra, dash * 0.2f + Eps);
            int landIndex = 0;
            foreach (var ev in kit.W.Events)
                if (ev.Type == SimEventType.ArtStage && ev.Id == Ids.Art.BoltDash && ev.A == (int)ArtStageKind.Land)
                {
                    Assert.AreEqual(cast + first + landIndex * gap, ev.Tick, "차례로 " + landIndex);
                    Assert.AreEqual(Mathf.Lerp(12f, 16f, (landIndex + 0.5f) / strikes), ev.Pos.x, Eps, "지나간 길 위");
                    landIndex++;
                }
        }

        [Test]
        public void AS_Art__CopyCircle_PageVortexPullsAndBurns_Within20Percent()
        {
            var up = Up(Ids.Art.CopyCircle);
            var kit = Kit(Ids.Char.C3, 4);
            var e = kit.AddEnemy(13.5f, 7f);
            kit.Seq("F, L+2304, K+2304");
            kit.Stage5();
            var z = kit.Zone(PlayerZoneKind.CopyCircle);
            Assert.IsNotNull(z);
            kit.Stage5(400);
            int pulses = Stages(kit, Ids.Art.CopyCircle, ArtStageKind.Pulse);
            int every = Ticks((float)up["pulse_seconds"]);
            Assert.AreEqual((360 - 1) / every, pulses, "지대가 있는 동안 맥박마다");
            Assert.AreEqual(1, Stages(kit, Ids.Art.CopyCircle, ArtStageKind.End));
            Assert.AreEqual(pulses * (float)up["damage"] * T, SumTo(kit, e, 0), Eps);
            int pulls = 0;
            foreach (var c in kit.Enemies.Ccs) if (c.EnemyId == e.Id && c.Kind == CcKind.Pull) pulls++;
            Assert.AreEqual(pulses, pulls, "끌어당긴다");
            var copy = AttackStyleKit.Tuned.Art(Ids.Art.CopyCircle).Effect;
            Assert.LessOrEqual(SumTo(kit, e, 0), copy.MaxCopies * copy.Damage * T * 0.2f + Eps, "사본 총량(12 × 0.5T)의 20% 이내");
        }

        [Test]
        public void AS_Art__CopyCircle_VortexEndsWithTheCircle()
        {
            var kit = Kit(Ids.Char.C3, 4);
            kit.AddEnemy(13.5f, 7f);
            kit.Seq("F, L+2304, K+2304");
            kit.Stage5();
            for (int i = 0; i < 12; i++) { kit.Arts.OnPlayerFired(kit.W, kit.P, i + 1); kit.Stage5(18); }
            Assert.IsNull(kit.Zone(PlayerZoneKind.CopyCircle), "12번 따라 쏘면 사라진다");
            int pulses = Stages(kit, Ids.Art.CopyCircle, ArtStageKind.Pulse);
            kit.Stage5(120);
            Assert.AreEqual(pulses, Stages(kit, Ids.Art.CopyCircle, ArtStageKind.Pulse), "지대가 사라지면 소용돌이도 끝");
        }

        [Test]
        public void AS_Art__IceScreen_SpikesRiseInALine_StunThenFreeze_Within20Percent()
        {
            var up = Up(Ids.Art.IceScreen);
            int spikes = (int)(float)up["spikes"];
            var kit = Kit(Ids.Char.C4, 6);
            var e = kit.AddEnemy(15.3f, 7.3f);
            kit.Seq("B, L+2304, K+2304");
            kit.Stage5(Ticks((float)up["first_delay_seconds"]) + Ticks((float)up["gap_seconds"]) * spikes + 2);
            Assert.AreEqual(spikes, Stages(kit, Ids.Art.IceScreen, ArtStageKind.Rise));
            Assert.AreEqual((float)up["damage"] * T, SumTo(kit, e, 0), Eps, "가시 줄에서 한 번");
            bool stun = false, freeze = false;
            int stunAt = -1, freezeAt = -1;
            for (int i = 0; i < kit.Enemies.Ccs.Count; i++)
            {
                var c = kit.Enemies.Ccs[i];
                if (c.EnemyId != e.Id) continue;
                if (c.Kind == CcKind.Stun) { stun = true; stunAt = i; Assert.AreEqual(Ticks((float)up["stun_seconds"]), c.Amount, Eps); }
                if (c.Kind == CcKind.Freeze) { freeze = true; freezeAt = i; Assert.AreEqual(Ticks((float)up["freeze_seconds"]), c.Amount, Eps); }
            }
            Assert.IsTrue(stun && freeze, "띄우고(짧은 기절) 얼린다");
            Assert.Less(stunAt, freezeAt);
            var screen = AttackStyleKit.Tuned.Art(Ids.Art.IceScreen).Effect;
            Assert.LessOrEqual(SumTo(kit, e, 0), screen.Damage * T * 0.2f + Eps, "병풍 파편 3T의 20% 이내");
        }

        [Test]
        public void AS_Art__ShadowHand_GripsThenCrushes_Within20Percent()
        {
            var up = Up(Ids.Art.ShadowHand);
            var kit = Kit(Ids.Char.C5, 5);
            var e = kit.AddEnemy(15.5f, 7f);
            kit.Seq("L, R+2304, K+2304");
            int cast = kit.W.Tick;
            kit.Stage5();
            float grip = SumTo(kit, e, 0);
            float n = AttackStyleKit.Tuned.Art(Ids.Art.ShadowHand).Effect.Damage;
            // 제자 5의 피해는 캐릭터 배율(그림자 계약: 쓴 뒤 결정 수)을 곱한다 — 강화 단계도 같은 시전 순간 배율.
            float charMul = grip / (n * T);
            Assert.GreaterOrEqual(charMul, 1f);
            Assert.AreEqual(1, Stages(kit, Ids.Art.ShadowHand, ArtStageKind.Grip));
            int delay = Ticks((float)up["crush_delay_seconds"]);
            kit.Stage5(delay + 1);
            Assert.AreEqual(1, Stages(kit, Ids.Art.ShadowHand, ArtStageKind.Crush));
            float crush = SumTo(kit, e, 1);
            Assert.AreEqual((float)up["damage"] * T * charMul, crush, Eps);
            Assert.LessOrEqual(crush, grip * 0.2f + Eps);
            foreach (var ev in kit.W.Events)
                if (ev.Type == SimEventType.ArtStage && ev.A == (int)ArtStageKind.Crush) Assert.AreEqual(cast + delay, ev.Tick);
        }
    }
}
