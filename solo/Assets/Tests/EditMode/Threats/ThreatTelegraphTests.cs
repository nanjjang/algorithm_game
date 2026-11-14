using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Threats;

namespace Wizard.Solo.Tests.Threats
{
    /// <summary>
    /// 예고 수명(05 요약서 1.2절, S5 TV-TEL), 동시 예고 상한과 대기 줄(2.1절, S5 TV-CAP), 표의 틱(Modules.cs 공통 규칙 4),
    /// 마지막 숨의 미루기(06 요약서 1.4절, 00 문서 D-64).
    /// </summary>
    public class ThreatTelegraphTests
    {
        static readonly Vector2 C = ThreatWorld.Center;
        /// <summary>플레이어에게서 먼 자리(구역이 R5에 걸리지 않게).</summary>
        static readonly Vector2 Far = new Vector2(3f, 3f);
        static readonly Vector2 Far2 = new Vector2(21f, 3f);
        static readonly Vector2 Far3 = new Vector2(3f, 11f);

        ThreatWorld _t;

        [SetUp]
        public void SetUp() { _t = ThreatWorld.Create(C); }

        static int CapFor(int wave)
        {
            var table = TestContent.Db.Waves.TelegraphCap;
            for (int i = 0; i < table.Count; i++)
                if (wave >= table[i].From && (table[i].To == null || wave <= table[i].To.Value)) return table[i].Value;
            return 0;
        }

        // ------------------------------------------------------------ 수명 (TV-TEL)

        [Test]
        public void S05_TV_TEL_01__DetonateTickIsStartPlusN_StartEventOnce()
        {
            _t.StepUntil(1000);
            int ticket = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(C, 2.5f, 66, 2)));
            // 7단계에 요청하면 같은 틱(1000)의 8단계에 구역이 생긴다.
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, ticket));
            Assert.IsTrue(_t.Threats.TryGetTiming(_t.W, ticket, out int s, out int det));
            Assert.AreEqual(1000, s);
            Assert.AreEqual(1066, det);
            var zones = new List<ZoneEntity>();
            _t.Threats.GetZones(_t.W, ticket, zones);
            Assert.AreEqual(1, zones.Count);
            Assert.AreEqual(1000, zones[0].StartTick);
            Assert.AreEqual(1066, zones[0].DetonateTick);
            Assert.AreEqual(ZoneShape.Circle, zones[0].Shape);
            CollectionAssert.AreEqual(new[] { 1000 }, _t.EventTicks(SimEventType.TelegraphStarted, zones[0].Id));
        }

        [Test]
        public void S05_TV_TEL_02_03_04_05__FillFlashAndDetonationTicks()
        {
            _t.StepUntil(1000);
            int ticket = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(C, 2.5f, 66, 2)));
            var zones = new List<ZoneEntity>();
            _t.Threats.GetZones(_t.W, ticket, zones);
            var z = zones[0];

            // TV-TEL-02: t = 1033 → fill = 33/66
            Assert.AreEqual(0.5f, z.Fill(1033), 1e-6f);
            Assert.AreEqual(1f, z.Fill(1066), 1e-6f);

            // TV-TEL-05: 반지름은 매 틱 2.5(자라지 않는다). TV-TEL-03: 1065까지 판정 없음.
            while (_t.W.Tick <= 1065)
            {
                Assert.AreEqual(2.5f, z.Radius, 1e-6f);
                _t.W.Step();
            }
            Assert.AreEqual(0, _t.Player.Calls.Count, "1065까지는 판정이 없다");
            Assert.IsFalse(z.Detonated);
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, ticket));

            // 판정은 s + N = 1066, 차오름 끝과 같은 틱
            _t.W.Step();
            Assert.IsTrue(z.Detonated);
            CollectionAssert.AreEqual(new[] { 1066 }, _t.Player.CallTicks);
            CollectionAssert.AreEqual(new[] { 1066 }, _t.EventTicks(SimEventType.TelegraphDetonated, z.Id));
            Assert.AreEqual(z.Id, _t.Player.Calls[0].AttackId, "AttackId = 구역의 EntityId");
            Assert.AreEqual(PlayerDamageSource.Telegraph, _t.Player.Calls[0].Source);
            Assert.AreEqual(2, _t.Player.Calls[0].DamageCells);

            // 1067에는 판정이 없다(구역은 12단계에 지워졌다).
            _t.W.Step();
            Assert.AreEqual(1, _t.Player.Calls.Count);
            Assert.AreEqual(0, _t.W.Zones.Count);

            // TV-TEL-04: 번쩍은 판정 2틱 전(s + N − 2)에 시작한다 — 이벤트 하나, A = 번쩍 틱 수 2(1064, 1065)
            CollectionAssert.AreEqual(new[] { 1064 }, _t.EventTicks(SimEventType.TelegraphFlash, z.Id));
            SimEvent flash = default;
            foreach (var e in _t.W.Events) if (e.Type == SimEventType.TelegraphFlash) flash = e;
            Assert.AreEqual(2, flash.A);
        }

        [Test]
        public void S05_TV_TEL_06_07__LastNTicksTracksThenLocks()
        {
            // 고슴도치 조준 s = 500, N = 60, 고정 24: 535까지 플레이어를 따라가고 536에 고정(이벤트 1회), 560 판정
            var owner = _t.AddEnemy(new Vector2(4f, 7.25f));
            var spec = ThreatSpecs.FromAttack(TestContent.Db.Enemy(Ids.Enemy.HogNeedle).Attacks.Find(a => a.Key == "snipe"));
            Assert.AreEqual(60, spec.TelegraphTicks);
            Assert.AreEqual(24, spec.LockLastTicks);
            var req = ThreatWorld.Request(spec);
            req.OwnerEnemyId = owner.Id;

            _t.StepUntil(500);
            int ticket = _t.RequestAtStage7(req);
            var zones = new List<ZoneEntity>();
            _t.Threats.GetZones(_t.W, ticket, zones);
            var z = zones[0];
            Assert.AreEqual(500, z.StartTick);
            Assert.AreEqual(560, z.DetonateTick);
            Assert.AreEqual(536, z.LockTick);
            Assert.IsFalse(z.DirLocked);
            Assert.AreEqual(1f, z.Dir.x, 1e-4f);
            // 조준선은 벽까지: 주인 x = 4에서 동쪽 벽(24)까지 20칸
            Assert.AreEqual(20f, z.Length, 1e-3f);

            // t = 535: 플레이어가 위로 가면 방향이 따라 바뀐다
            _t.StepUntil(535);
            _t.P.Pos = new Vector2(4f, 2.25f);
            _t.W.Step();   // 틱 535 처리
            Assert.AreEqual(0f, z.Dir.x, 1e-4f);
            Assert.AreEqual(-1f, z.Dir.y, 1e-4f);
            Assert.IsFalse(z.DirLocked);
            Assert.AreEqual(0, _t.CountEvents(SimEventType.TelegraphLocked));

            // t = 536: 그 틱의 플레이어 쪽으로 잡고 고정, 이벤트 1회
            _t.P.Pos = new Vector2(9f, 7.25f);
            _t.W.Step();   // 틱 536
            Assert.IsTrue(z.DirLocked);
            Assert.AreEqual(1f, z.Dir.x, 1e-4f);
            CollectionAssert.AreEqual(new[] { 536 }, _t.EventTicks(SimEventType.TelegraphLocked, z.Id));

            // 그 뒤로는 플레이어가 움직여도 그대로
            _t.P.Pos = new Vector2(4f, 12f);
            _t.StepUntil(560);
            Assert.AreEqual(1f, z.Dir.x, 1e-4f);
            Assert.AreEqual(1, _t.CountEvents(SimEventType.TelegraphLocked));
            Assert.IsFalse(z.Detonated);
            _t.W.Step();   // 틱 560 = 발사 틱
            Assert.IsTrue(z.Detonated);
            Assert.AreEqual(0, _t.Player.Calls.Count, "조준선 자체는 피해가 없다(피해는 탄)");
        }

        [Test]
        public void S05_TV_TEL_08__AtStartKeepsDirection()
        {
            // 늑대 예고 s = 200: 플레이어가 s + 1에 옆으로 가도 띠 방향은 s의 방향 그대로
            var owner = _t.AddEnemy(C + new Vector2(-4f, 0));
            var spec = ThreatSpecs.FromAttack(TestContent.Db.Enemy(Ids.Enemy.WolfBramble).Attacks.Find(a => a.Key == "dash"));
            var req = ThreatWorld.Request(spec);
            req.OwnerEnemyId = owner.Id;
            _t.StepUntil(200);
            int ticket = _t.RequestAtStage7(req);
            var zones = new List<ZoneEntity>();
            _t.Threats.GetZones(_t.W, ticket, zones);
            var z = zones[0];
            Assert.IsTrue(z.DirLocked);
            Assert.AreEqual(200, z.LockTick);
            Assert.AreEqual(owner.Pos.x, z.Pos.x, 1e-5f);
            Assert.AreEqual(1f, z.Dir.x, 1e-4f);

            _t.P.Pos = C + new Vector2(-4f, 5f);
            _t.Steps(3);
            Assert.AreEqual(1f, z.Dir.x, 1e-4f);
            Assert.AreEqual(0f, z.Dir.y, 1e-4f);
            Assert.AreEqual(0, _t.CountEvents(SimEventType.TelegraphLocked), "시작 순간 고정은 떨림 이벤트가 없다");
        }

        [Test]
        public void S05_TV_TEL_09__CancelRemovesZoneWithoutHit()
        {
            // 예고 중 빙결(취소 가능한 적) → 구역 제거, cancelled, 판정 없음, 동시 예고 수 −1
            var owner = _t.AddEnemy(C);
            var spec = ThreatWorld.Circle(Vector2.zero, 1.8f, 54);
            spec.Anchor = ZoneAnchor.Owner;
            var req = ThreatWorld.Request(spec);
            req.OwnerEnemyId = owner.Id;
            int ticket = _t.RequestAtStage7(req);
            Assert.AreEqual(1, _t.W.Zones.Count);
            int zoneId = _t.W.Zones[0].Id;

            _t.Steps(10);
            _t.Threats.CancelTelegraphsOf(_t.W, owner.Id);
            Assert.AreEqual(TicketStatus.Cancelled, _t.Threats.Status(_t.W, ticket));
            Assert.AreEqual(1, _t.CountEvents(SimEventType.TelegraphCancelled, zoneId));
            _t.Steps(60);
            Assert.AreEqual(0, _t.W.Zones.Count);
            Assert.AreEqual(0, _t.Player.Calls.Count);
            Assert.AreEqual(0, _t.CountEvents(SimEventType.TelegraphDetonated));
        }

        // ------------------------------------------------------------ 표의 계약 (공통 규칙 4)

        [Test]
        public void Contract__WaitingTicketHasNoTiming_UnknownTicketIsUnknown()
        {
            Assert.AreEqual(TicketStatus.Unknown, _t.Threats.Status(_t.W, 12345));
            Assert.IsFalse(_t.Threats.TryGetTiming(_t.W, 12345, out _, out _));

            _t.W.Wave.TelegraphCap = 1;
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 1f, 200)));
            int waiting = 0;
            _t.Enemies.OnAi = w => { if (waiting == 0) waiting = _t.Threats.RequestTelegraph(w, ThreatWorld.Request(ThreatWorld.Circle(Far2, 1f, 30))); };
            _t.Steps(5);
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, waiting));
            Assert.IsFalse(_t.Threats.TryGetTiming(_t.W, waiting, out _, out _));
            var zones = new List<ZoneEntity>();
            _t.Threats.GetZones(_t.W, waiting, zones);
            Assert.AreEqual(0, zones.Count);
        }

        [Test]
        public void Contract__TicketStaysDetonatedFor600TicksThenUnknown()
        {
            int ticket = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(Far, 1f, 30)));   // s = 0, 판정 30
            _t.StepUntil(31);
            Assert.AreEqual(TicketStatus.Detonated, _t.Threats.Status(_t.W, ticket));
            Assert.AreEqual(0, _t.W.Zones.Count, "구역은 12단계에 지워진다");
            Assert.IsTrue(_t.Threats.TryGetTiming(_t.W, ticket, out int s, out int det));
            Assert.AreEqual(0, s);
            Assert.AreEqual(30, det);

            // 판정 틱 30 + 600 = 630의 8단계에 지워진다 → 틱 629 처리 뒤까지는 남아 있다.
            _t.StepUntil(630);
            Assert.AreEqual(TicketStatus.Detonated, _t.Threats.Status(_t.W, ticket));
            _t.W.Step();
            Assert.AreEqual(TicketStatus.Unknown, _t.Threats.Status(_t.W, ticket));
            Assert.IsFalse(_t.Threats.TryGetTiming(_t.W, ticket, out _, out _));
        }

        [Test]
        public void Contract__CancelTicketReleasesIt()
        {
            int ticket = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(C, 2f, 60)));
            int zoneId = _t.W.Zones[0].Id;
            _t.Threats.CancelTicket(_t.W, ticket);
            Assert.AreEqual(TicketStatus.Unknown, _t.Threats.Status(_t.W, ticket));
            Assert.AreEqual(1, _t.CountEvents(SimEventType.TelegraphCancelled, zoneId));
            _t.Steps(70);
            Assert.AreEqual(0, _t.Player.Calls.Count);
        }

        [Test]
        public void Contract__MultiZoneTimingIsLatestDetonation()
        {
            int ticket = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(Far, 1f, 30), ThreatWorld.Circle(Far2, 1f, 48)));
            Assert.IsTrue(_t.Threats.TryGetTiming(_t.W, ticket, out int s, out int det));
            Assert.AreEqual(0, s);
            Assert.AreEqual(48, det);
            _t.StepUntil(31);
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, ticket), "늦은 구역이 남아 있다");
            _t.StepUntil(49);
            Assert.AreEqual(TicketStatus.Detonated, _t.Threats.Status(_t.W, ticket));
        }

        // ------------------------------------------------------------ 상한 표와 대기 줄 (TV-CAP)

        [TestCase(1, 2)]
        [TestCase(4, 2)]
        [TestCase(5, 3)]
        [TestCase(8, 3)]
        [TestCase(9, 4)]
        [TestCase(12, 4)]
        [TestCase(13, 5)]
        [TestCase(29, 5)]
        [TestCase(30, 6)]
        [TestCase(100, 6)]
        public void S05_TV_CAP_01__TelegraphCapTable(int wave, int cap)
        {
            Assert.AreEqual(cap, CapFor(wave));
        }

        [Test]
        public void S05_TV_CAP_02_03__OverCapWaitsUntilAZoneDetonates()
        {
            // w = 4(상한 2), 떠 있는 구역 2개 → 늑대 돌진 요청은 대기. 구역 하나가 판정되면 다음 틱에 시작.
            _t.W.Wave.Wave = 4;
            _t.W.Wave.TelegraphCap = CapFor(4);
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 1f, 20)));
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far2, 1f, 200)));
            _t.W.Step();   // 틱 0: 둘 다 시작
            Assert.AreEqual(2, _t.W.Zones.Count);

            int wolf = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Band(Far3, new Vector2(1, 0), 1f, 6f, 30)));   // 틱 1
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, wolf));

            _t.StepUntil(21);   // 틱 20까지 처리: 첫 구역이 20에 판정됐다
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, wolf), "판정된 틱의 8단계에는 아직 떠 있었다");
            _t.W.Step();        // 틱 21
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, wolf));
            Assert.IsTrue(_t.Threats.TryGetTiming(_t.W, wolf, out int s, out int det));
            Assert.AreEqual(21, s);
            Assert.AreEqual(51, det, "기다린 틱은 예고 길이에 들지 않는다");
        }

        [Test]
        public void S05_TV_CAP_04_05__AllOrNothing()
        {
            // w = 30(상한 6), 구역 5개
            _t.W.Wave.Wave = 30;
            _t.W.Wave.TelegraphCap = CapFor(30);
            for (int i = 0; i < 5; i++)
                _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(new Vector2(2f + i * 2f, 2f), 0.5f, 300)));
            _t.W.Step();
            Assert.AreEqual(5, _t.W.Zones.Count);

            // TV-CAP-05: 흙 띠 4개 요청 → 5 + 4 > 6 → 전부 대기(일부만 시작하지 않는다)
            int four = _t.RequestAtStage7(ThreatWorld.Request(
                ThreatWorld.Circle(new Vector2(2f, 12f), 0.5f, 36), ThreatWorld.Circle(new Vector2(4f, 12f), 0.5f, 36),
                ThreatWorld.Circle(new Vector2(6f, 12f), 0.5f, 36), ThreatWorld.Circle(new Vector2(8f, 12f), 0.5f, 36)));
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, four));
            Assert.AreEqual(5, _t.W.Zones.Count);

            // TV-CAP-04: 새 요청 1개(구역 1) → 상한 통과(R5도 통과) → 시작
            int one = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(new Vector2(20f, 12f), 0.5f, 36)));
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, one));
            Assert.AreEqual(6, _t.W.Zones.Count);
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, four));
        }

        [Test]
        public void S05_Cap__BossPatternCountsAsOne()
        {
            // 보스 패턴 하나의 여러 원(낙석 6)은 1개로 센다
            _t.W.Wave.TelegraphCap = 2;
            var req = new TelegraphRequest { CountAsOne = true, CastId = 3 };
            for (int i = 0; i < 6; i++) req.Zones.Add(ThreatWorld.Circle(new Vector2(2f + i * 3f, 2f), 0.5f, 60));
            int rocks = _t.RequestAtStage7(req);
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, rocks));
            Assert.AreEqual(6, _t.W.Zones.Count);
            int counted = 0;
            foreach (var z in _t.W.Zones) if (z.CountsToCap) counted++;
            Assert.AreEqual(1, counted);

            int next = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(new Vector2(2f, 12f), 0.5f, 60)));
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, next), "1 + 1 ≤ 2");
            int third = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(new Vector2(6f, 12f), 0.5f, 60)));
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, third));
        }

        [Test]
        public void S05_TV_CAP_08__BlockedHeadDoesNotBlockLaterRequests()
        {
            // A(t = 0, 구역 2개)와 B(t = 10, 구역 1개). 상한 2에 긴 구역 X와 틱 50에 터지는 Y가 떠 있다.
            // Y가 터진 뒤(틱 51): A는 1 + 2 > 2로 여전히 막히고 B는 1 + 1 ≤ 2 → B 시작(줄 머리 막힘 없음).
            _t.W.Wave.TelegraphCap = 2;
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 0.5f, 900)));
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far2, 0.5f, 50)));
            int a = _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(
                ThreatWorld.Circle(new Vector2(2f, 12f), 0.5f, 30), ThreatWorld.Circle(new Vector2(4f, 12f), 0.5f, 30)));
            _t.StepUntil(10);
            int b = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(new Vector2(20f, 12f), 0.5f, 30)));
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, a));
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, b));

            _t.StepUntil(51);
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, b));
            _t.W.Step();   // 틱 51
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, a));
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, b));
        }

        [Test]
        public void S05_TV_CAP_07__StarvingRequestHoldsBackLaterOnes()
        {
            // 같은 꼴이지만 Y가 틱 130에 터진다: A는 120틱 넘게 기다렸고 여전히 막혀 있다 → B는 통과할 수 있어도 시작하지 않는다.
            _t.W.Wave.TelegraphCap = 2;
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 0.5f, 900)));
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far2, 0.5f, 130)));
            int a = _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(
                ThreatWorld.Circle(new Vector2(2f, 12f), 0.5f, 30), ThreatWorld.Circle(new Vector2(4f, 12f), 0.5f, 30)));
            _t.StepUntil(10);
            int b = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(new Vector2(20f, 12f), 0.5f, 30)));

            _t.StepUntil(140);
            Assert.AreEqual(1, _t.W.Zones.Count, "Y는 130에 터졌다");
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, a));
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, b), "오래 굶은 A가 막혀 있는 동안 B는 시작하지 않는다");

            // A를 치우면 B가 바로 시작한다.
            _t.Threats.CancelTicket(_t.W, a);
            _t.W.Step();
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, b));
        }

        [Test]
        public void S05_TV_CAP_07b__Exactly120TicksIsNotStarving()
        {
            // "120틱 넘게": 정확히 120틱 기다린 틱에는 뒤 요청이 시작한다.
            _t.W.Wave.TelegraphCap = 2;
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 0.5f, 900)));
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far2, 0.5f, 119)));
            int a = _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(
                ThreatWorld.Circle(new Vector2(2f, 12f), 0.5f, 30), ThreatWorld.Circle(new Vector2(4f, 12f), 0.5f, 30)));
            _t.StepUntil(10);
            int b = _t.RequestAtStage7(ThreatWorld.Request(ThreatWorld.Circle(new Vector2(20f, 12f), 0.5f, 30)));
            _t.StepUntil(121);   // 틱 120까지 처리. Y는 119에 터졌고 120의 8단계에서 A는 120 − 0 = 120틱(넘지 않음)
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, a));
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, b));
        }

        [Test]
        public void S05_TV_CAP_09__DeadRequesterIsDropped()
        {
            _t.W.Wave.TelegraphCap = 1;
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 0.5f, 300)));
            var shaman = _t.AddEnemy(Far2);
            var hog = _t.AddEnemy(Far3);
            var r1 = ThreatWorld.Request(ThreatWorld.Circle(Far2, 0.5f, 30)); r1.OwnerEnemyId = shaman.Id;
            var r2 = ThreatWorld.Request(ThreatWorld.Circle(Far3, 0.5f, 30)); r2.OwnerEnemyId = hog.Id;
            int t1 = _t.Threats.RequestTelegraph(_t.W, r1);
            int t2 = _t.Threats.RequestTelegraph(_t.W, r2);
            _t.Steps(2);
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, t1));

            shaman.PendingDeath = true;        // 죽어 가는 중도 "없는 것"
            _t.W.Enemies.Remove(hog);          // 이미 치워진 적
            _t.W.Step();
            Assert.AreEqual(TicketStatus.Cancelled, _t.Threats.Status(_t.W, t1));
            Assert.AreEqual(TicketStatus.Cancelled, _t.Threats.Status(_t.W, t2));

            // 상한이 풀려도 폐기된 요청은 시작하지 않는다.
            _t.W.Wave.TelegraphCap = 0;
            _t.Steps(3);
            Assert.AreEqual(1, _t.W.Zones.Count);
        }

        [Test]
        public void Contract__CapZeroMeansNoCap_AllStartWithoutWaiting()
        {
            _t.W.Wave.TelegraphCap = 0;
            var tickets = new List<int>();
            _t.Enemies.OnAi = w =>
            {
                if (tickets.Count > 0) return;
                for (int i = 0; i < 9; i++)
                    tickets.Add(_t.Threats.RequestTelegraph(w, ThreatWorld.Request(ThreatWorld.Circle(new Vector2(1.5f + i * 2.5f, 1.5f), 0.5f, 60))));
            };
            _t.W.Step();
            Assert.AreEqual(9, _t.W.Zones.Count);
            foreach (int id in tickets) Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, id));
        }

        [Test]
        public void S05_Queue__BypassIgnoresCapAndR5()
        {
            _t.W.Wave.TelegraphCap = 1;
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 0.5f, 300)));
            // 반응 창 밖이고 벗어날 수 없는 큰 원(R5 실패)이지만 Bypass면 바로 시작한다.
            var req = ThreatWorld.Request(ThreatWorld.Circle(C, 6f, 30));
            req.Bypass = true;
            int ticket = _t.Threats.RequestTelegraph(_t.W, req);
            _t.W.Step();
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, ticket));
            Assert.AreEqual(2, _t.W.Zones.Count);
        }

        [Test]
        public void S05_Queue__R5BlockedRequestWaits()
        {
            // TV-R5-15의 대기 줄 꼴: 둘러싼 원 8개가 떠 있으면 가운데 원은 기다리고, 그 원들이 터지고 나면 시작한다.
            var ring = new TelegraphRequest { Bypass = true, CastId = 1 };
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                ring.Zones.Add(ThreatWorld.Circle(C + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f, 1.5f, 54));
            }
            _t.Player.Accept = false;
            _t.Threats.RequestTelegraph(_t.W, ring);
            int mid = _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(C, 1.8f, 54)));
            _t.W.Step();
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, mid));
            _t.StepUntil(56);
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, mid));
        }

        [Test]
        public void S05_Queue__ZonesAreRebuiltAtCurrentPlayerPositionWhenStarting()
        {
            // 재검사 때마다 구역 위치는 지금 플레이어 기준으로 다시 잡는다(U-08).
            _t.W.Wave.TelegraphCap = 1;
            _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 0.5f, 20)));
            var spec = ThreatWorld.Circle(Vector2.zero, 1.2f, 42);
            spec.Anchor = ZoneAnchor.TargetPlayer;
            int ticket = _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(spec));
            _t.Steps(5);
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, ticket));
            _t.P.Pos = new Vector2(18f, 10f);
            _t.StepUntil(22);
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, ticket));
            var zones = new List<ZoneEntity>();
            _t.Threats.GetZones(_t.W, ticket, zones);
            Assert.AreEqual(18f, zones[0].Pos.x, 1e-5f);
            Assert.AreEqual(10f, zones[0].Pos.y, 1e-5f);

            // 시작한 뒤에는 따라가지 않는다(player_pos_at_start).
            _t.P.Pos = new Vector2(5f, 5f);
            _t.Steps(3);
            Assert.AreEqual(18f, zones[0].Pos.x, 1e-5f);
        }

        [Test]
        public void S05_Queue__NoTargetPlayerSkipsThatTick()
        {
            var spec = ThreatWorld.Circle(Vector2.zero, 1.2f, 42);
            spec.Anchor = ZoneAnchor.TargetPlayer;
            _t.P.Downed = true;   // NearestPlayer = null
            int ticket = _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(spec));
            _t.Steps(3);
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, ticket));
            Assert.AreEqual(0, _t.W.Zones.Count);
            _t.P.Downed = false;
            _t.W.Step();
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, ticket));
        }

        // ------------------------------------------------------------ 마지막 숨 (06 요약서 1.4절, D-64)

        [Test]
        public void S06_LastBreath__OnlyTheBlockingCircleIsDelayedBy12Ticks()
        {
            // 구석 칸 (0, 0)의 플레이어. 원 A·B(반경 1.2)는 플레이어를 덮지 않고, 플레이어 자리의 원 C에서 벗어날 칸
            // (11단계 안에 닿는 9칸)을 전부 덮는다 → A, B는 바로 시작하고 C만 길을 막아 12틱 밀린다.
            // 12틱 뒤에는 A·B가 4단계 먼저 터지므로 그 틈에 빠져나갈 수 있어 C가 시작한다.
            var dc = TestContent.Db.Modifier(Ids.Whim.LastBreath).DeathCircle;
            int n = SimConst.Ticks(dc.TelegraphSeconds);
            int stagger = SimConst.Ticks(dc.StaggerSeconds);
            Assert.AreEqual(48, n);
            Assert.AreEqual(12, stagger);

            _t.P.Pos = new Vector2(0.25f, 0.25f);
            _t.W.Wave.TelegraphCap = 1;   // 마지막 숨의 원은 상한에 세지 않는다
            int[] tickets = new int[3];
            Vector2[] centers = { new Vector2(2.15f, 0.75f), new Vector2(0.75f, 2.15f), _t.P.Pos };
            _t.StepUntil(100);
            _t.Enemies.OnAi = w =>
            {
                if (tickets[0] != 0) return;
                for (int i = 0; i < 3; i++)
                {
                    var req = ThreatWorld.Request(ThreatWorld.Circle(centers[i], dc.Radius, n, dc.Damage));
                    req.StaggerIfBlockedTicks = stagger;
                    req.CastId = 10 + i;
                    tickets[i] = _t.Threats.RequestTelegraph(w, req);
                }
            };
            _t.W.Step();   // 틱 100: 요청 셋
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, tickets[0]));
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, tickets[1]));
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, tickets[2]));
            foreach (var z in _t.W.Zones) Assert.IsFalse(z.CountsToCap);

            _t.StepUntil(112);
            Assert.AreEqual(TicketStatus.Waiting, _t.Threats.Status(_t.W, tickets[2]), "12틱 동안은 다시 보지 않는다");
            _t.W.Step();   // 틱 112
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, tickets[2]));
            Assert.IsTrue(_t.Threats.TryGetTiming(_t.W, tickets[0], out int sa, out int da));
            Assert.IsTrue(_t.Threats.TryGetTiming(_t.W, tickets[1], out int sb, out _));
            Assert.IsTrue(_t.Threats.TryGetTiming(_t.W, tickets[2], out int sc, out int dc2));
            Assert.AreEqual(100, sa);
            Assert.AreEqual(100, sb);
            Assert.AreEqual(148, da);
            Assert.AreEqual(112, sc);
            Assert.AreEqual(160, dc2);
        }

        [Test]
        public void S06_LastBreath__NotCountedAgainstCap()
        {
            // 상한이 꽉 차 있어도 마지막 숨의 원은 시작하고(R5만 본다), 다른 요청의 자리를 쓰지도 않는다.
            _t.W.Wave.TelegraphCap = 1;
            var req = ThreatWorld.Request(ThreatWorld.Circle(Far2, 1.2f, 48));
            req.StaggerIfBlockedTicks = 12;
            int breath1 = _t.Threats.RequestTelegraph(_t.W, req);
            int breath2 = _t.Threats.RequestTelegraph(_t.W, req);
            int normal = _t.Threats.RequestTelegraph(_t.W, ThreatWorld.Request(ThreatWorld.Circle(Far, 0.5f, 300)));
            _t.W.Step();
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, breath1));
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, breath2));
            Assert.AreEqual(TicketStatus.Active, _t.Threats.Status(_t.W, normal));
        }

        // ------------------------------------------------------------ 모양 5종의 판정 (0절: 거리 < 반지름 합)

        void AssertZoneHit(ZoneSpec spec, Vector2 playerPos, bool expectHit, string label)
        {
            var t = ThreatWorld.Create(playerPos);
            var req = ThreatWorld.Request(spec);
            req.Bypass = true;   // 판정만 본다(R5와 무관)
            t.Threats.RequestTelegraph(t.W, req);
            t.StepUntil(spec.TelegraphTicks + 1);
            Assert.AreEqual(expectHit ? 1 : 0, t.Player.Calls.Count, label);
        }

        [Test]
        public void S05_Shapes__CircleOverlapUsesHitRadius()
        {
            // 플레이어 판정원 0.15: 중심 거리 < 2.0 + 0.15
            Vector2 o = new Vector2(10f, 7f);
            AssertZoneHit(ThreatWorld.Circle(o, 2.0f, 6), o + new Vector2(2.14f, 0), true, "2.14 < 2.15");
            AssertZoneHit(ThreatWorld.Circle(o, 2.0f, 6), o + new Vector2(2.16f, 0), false, "2.16 > 2.15");
        }

        [Test]
        public void S05_Shapes__BandUsesHalfWidthAndLength()
        {
            Vector2 o = new Vector2(5f, 7f);
            var band = ThreatWorld.Band(o, new Vector2(1, 0), 1.0f, 6f, 6);
            AssertZoneHit(band, o + new Vector2(3f, 0.64f), true, "옆 0.64 < 0.5 + 0.15");
            AssertZoneHit(band, o + new Vector2(3f, 0.66f), false, "옆 0.66");
            AssertZoneHit(band, o + new Vector2(6.14f, 0f), true, "끝 6.14 < 6 + 0.15");
            AssertZoneHit(band, o + new Vector2(6.16f, 0f), false, "끝 6.16");
            AssertZoneHit(band, o + new Vector2(-0.16f, 0f), false, "시작점 뒤 0.16");
        }

        [Test]
        public void S05_Shapes__FanUsesAngleAndRadius()
        {
            Vector2 o = new Vector2(10f, 7f);
            var fan = ThreatWorld.Fan(o, new Vector2(1, 0), 3.0f, 110f, 6);
            AssertZoneHit(fan, o + new Vector2(2f, 0f), true, "이등분선 위");
            AssertZoneHit(fan, o + new Vector2(3.14f, 0f), true, "호 밖 0.14");
            AssertZoneHit(fan, o + new Vector2(3.16f, 0f), false, "호 밖 0.16");
            // 반각 55도: 60도 방향의 2칸 지점은 변에서 2 × sin 5° = 0.174칸 밖(안 맞음), 50도 방향은 안
            AssertZoneHit(fan, o + Dir8Util.DegreesToSimVector(50f) * 2f, true, "50도");
            AssertZoneHit(fan, o + Dir8Util.DegreesToSimVector(60f) * 2f, false, "60도");
            AssertZoneHit(fan, o + new Vector2(-1f, 0f), false, "뒤");
        }

        [Test]
        public void S05_Shapes__RingHasSafeInside()
        {
            // 밤삼킴 또아리: 바깥 5.0, 안 4.0 — 가운데와 바깥은 안전
            Vector2 o = new Vector2(12f, 7f);
            var ring = new ZoneSpec
            {
                Shape = ZoneShape.Ring, Anchor = ZoneAnchor.Fixed, Pos = o, Radius = 5.0f, InnerRadius = 4.0f, TelegraphTicks = 6,
                BaseDamage = 1, DamageSource = PlayerDamageSource.Telegraph
            };
            AssertZoneHit(ring, o, false, "가운데");
            AssertZoneHit(ring, o + new Vector2(3.84f, 0), false, "안쪽 가장자리에서 0.16");
            AssertZoneHit(ring, o + new Vector2(3.86f, 0), true, "안쪽 가장자리에서 0.14");
            AssertZoneHit(ring, o + new Vector2(4.5f, 0), true, "띠 위");
            AssertZoneHit(ring, o + new Vector2(5.16f, 0), false, "바깥 0.16");
        }

        [Test]
        public void S05_Shapes__AimLineReachesTheWall()
        {
            Vector2 o = new Vector2(2f, 7f);
            var line = new ZoneSpec
            {
                Shape = ZoneShape.AimLine, Anchor = ZoneAnchor.Fixed, Pos = o, Dir = new Vector2(1, 0), Width = 0.4f, Length = 0f,
                TelegraphTicks = 6, BaseDamage = 1, DamageSource = PlayerDamageSource.Telegraph
            };
            AssertZoneHit(line, new Vector2(23.5f, 7.3f), true, "벽 앞, 옆 0.3 < 0.2 + 0.15");
            AssertZoneHit(line, new Vector2(23.5f, 7.4f), false, "옆 0.4");
        }

        [Test]
        public void S05_Lifetime__HarmlessAndDownedAreNotHit()
        {
            var harmless = ThreatWorld.Circle(C, 2f, 6);
            harmless.Harmless = true;
            AssertZoneHit(harmless, C, false, "피해 없는 예고");

            var t = ThreatWorld.Create(C);
            t.P.Downed = true;
            var req = ThreatWorld.Request(ThreatWorld.Circle(C, 2f, 6));
            req.Bypass = true;
            t.Threats.RequestTelegraph(t.W, req);
            t.Steps(8);
            Assert.AreEqual(0, t.Player.Calls.Count);
        }

        [Test]
        public void S05_TV_SPC_36__VineSeedLeavesThornField()
        {
            // 가시덩굴 씨앗 판정 → 가시밭 반경 1.5, 240틱
            var spec = ThreatSpecs.FromAttack(TestContent.Db.Enemy(Ids.Enemy.VineThorn).Attacks.Find(a => a.Key == "seed"));
            spec.Anchor = ZoneAnchor.Fixed;
            spec.Pos = Far;
            int ticket = _t.RequestAtStage7(ThreatWorld.Request(spec));   // s = 0
            _t.StepUntil(60);
            Assert.AreEqual(0, _t.W.Hazards.Count);
            _t.W.Step();   // 틱 60 = 판정
            Assert.AreEqual(TicketStatus.Detonated, _t.Threats.Status(_t.W, ticket));
            Assert.AreEqual(1, _t.W.Hazards.Count);
            var h = _t.W.Hazards[0];
            Assert.AreEqual(HazardKind.ThornField, h.Kind);
            Assert.AreEqual(1.5f, h.Radius, 1e-5f);
            Assert.AreEqual(60, h.StartTick);
            Assert.AreEqual(300, h.UntilTick);
            Assert.AreEqual(90, h.ContactCooldownTicks);
            Assert.AreEqual(1, h.BaseDamage);
            Assert.IsFalse(h.SlowOnly);
            Assert.AreEqual(Far.x, h.Pos.x, 1e-5f);
        }
    }
}
