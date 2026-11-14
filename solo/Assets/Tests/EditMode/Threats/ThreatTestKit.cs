using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Threats;

namespace Wizard.Solo.Tests.Threats
{
    /// <summary>피격 후보를 받아 적기만 하는 가짜 플레이어 모듈. Accept = false면 무적처럼 버린다(QueueHit = false).</summary>
    public sealed class FakePlayerModule : PlayerModule
    {
        public bool Accept = true;
        /// <summary>QueueHit이 불린 것 전부(받았든 버렸든).</summary>
        public readonly List<PlayerHitRequest> Calls = new List<PlayerHitRequest>();
        public readonly List<int> CallTicks = new List<int>();
        /// <summary>후보로 올라간 것(true를 돌려준 것)의 틱.</summary>
        public readonly List<int> AcceptedTicks = new List<int>();
        public readonly List<PlayerHitRequest> Accepted = new List<PlayerHitRequest>();

        public override bool QueueHit(SimWorld w, PlayerState p, in PlayerHitRequest hit)
        {
            Calls.Add(hit);
            CallTicks.Add(w.Tick);
            if (!Accept) return false;
            Accepted.Add(hit);
            AcceptedTicks.Add(w.Tick);
            return true;
        }
    }

    /// <summary>7단계에 테스트가 넣은 일을 하는 가짜 적 모듈(예고 요청은 7단계에 들어온다 — 공통 규칙 4).</summary>
    public sealed class FakeEnemyModule : EnemyModule
    {
        public Action<SimWorld> OnAi;
        public override void Stage7_Ai(SimWorld w) { if (OnAi != null) OnAi(w); }
    }

    /// <summary>x = ScreenX인 세로 선을 얼음 병풍으로 치는 가짜 비기 모듈.</summary>
    public sealed class FakeArtModule : ArtModule
    {
        public bool HasScreen;
        public float ScreenX;
        public int ScreenZoneId = 77;

        public override bool SegmentHitsScreen(SimWorld w, Vector2 a, Vector2 b, out int zoneId)
        {
            zoneId = -1;
            if (!HasScreen) return false;
            bool crosses = (a.x - ScreenX) * (b.x - ScreenX) <= 0f && a.x != b.x;
            if (crosses) zoneId = ScreenZoneId;
            return crosses;
        }
    }

    /// <summary>위협 모듈만 진짜로 끼운 세계.</summary>
    public sealed class ThreatWorld
    {
        public SimWorld W;
        public ThreatSystem Threats;
        public FakePlayerModule Player;
        public FakeEnemyModule Enemies;
        public FakeArtModule Arts;
        public PlayerState P => W.Players[0];

        /// <summary>TV-R5의 기준 자리: 칸 (24, 14)의 중심.</summary>
        public static readonly Vector2 Center = new Vector2(12.25f, 7.25f);

        public static ThreatWorld Create(Vector2 playerPos, string charId = Ids.Char.C1)
        {
            var t = new ThreatWorld
            {
                Threats = new ThreatSystem(), Player = new FakePlayerModule(), Enemies = new FakeEnemyModule(), Arts = new FakeArtModule()
            };
            var mods = SimModuleSet.Stubs();
            mods.Threats = t.Threats;
            mods.Player = t.Player;
            mods.Enemies = t.Enemies;
            mods.Arts = t.Arts;
            t.W = new SimWorld(TestContent.Db, mods, 1);
            t.W.Run.Phase = RunPhase.WaveCombat;
            var p = t.W.Players[0];
            p.Active = true;
            p.CharId = charId;
            p.Def = TestContent.Db.Character(charId);
            p.Pos = playerPos;
            return t;
        }

        public void Steps(int n) { for (int i = 0; i < n; i++) W.Step(); }

        public void StepUntil(int tick) { while (W.Tick < tick) W.Step(); }

        public EnemyState AddEnemy(Vector2 pos, bool elite = false, bool boss = false)
        {
            var e = new EnemyState { Id = W.NewEntityId(), Pos = pos, BodyRadius = 0.4f, Hp = 10f, MaxHp = 10f, Ai = EnemyAiState.Act, IsBoss = boss };
            if (elite) e.Elite = new Wizard.Solo.Data.EliteDef();
            W.Enemies.Add(e);
            return e;
        }

        public int CountEvents(SimEventType type, int entityId = 0)
        {
            int n = 0;
            for (int i = 0; i < W.Events.Count; i++)
                if (W.Events[i].Type == type && (entityId == 0 || W.Events[i].EntityId == entityId)) n++;
            return n;
        }

        public List<int> EventTicks(SimEventType type, int entityId = 0)
        {
            var ticks = new List<int>();
            for (int i = 0; i < W.Events.Count; i++)
                if (W.Events[i].Type == type && (entityId == 0 || W.Events[i].EntityId == entityId)) ticks.Add(W.Events[i].Tick);
            return ticks;
        }

        public static ZoneSpec Circle(Vector2 pos, float radius, int ticks, int damage = 1)
        {
            return new ZoneSpec
            {
                Shape = ZoneShape.Circle, Anchor = ZoneAnchor.Fixed, Pos = pos, Radius = radius, TelegraphTicks = ticks,
                BaseDamage = damage, DamageSource = PlayerDamageSource.Telegraph, Grazeable = true
            };
        }

        public static ZoneSpec Band(Vector2 from, Vector2 dir, float width, float length, int ticks, int damage = 1)
        {
            return new ZoneSpec
            {
                Shape = ZoneShape.Band, Anchor = ZoneAnchor.Fixed, Pos = from, Dir = dir, Width = width, Length = length,
                TelegraphTicks = ticks, BaseDamage = damage, DamageSource = PlayerDamageSource.Telegraph, Grazeable = true
            };
        }

        public static ZoneSpec Fan(Vector2 apex, Vector2 dir, float radius, float degrees, int ticks, int damage = 1)
        {
            return new ZoneSpec
            {
                Shape = ZoneShape.Fan, Anchor = ZoneAnchor.Fixed, Pos = apex, Dir = dir, Radius = radius, FanDegrees = degrees,
                TelegraphTicks = ticks, BaseDamage = damage, DamageSource = PlayerDamageSource.Telegraph, Grazeable = true
            };
        }

        public static TelegraphRequest Request(params ZoneSpec[] zones)
        {
            var r = new TelegraphRequest { CastId = 1 };
            r.Zones.AddRange(zones);
            return r;
        }

        /// <summary>다음 Step의 7단계에 요청을 넣고 표를 돌려받을 자리를 만든다.</summary>
        public int RequestAtStage7(TelegraphRequest req)
        {
            int ticket = 0;
            Enemies.OnAi = world => { ticket = Threats.RequestTelegraph(world, req); Enemies.OnAi = null; };
            W.Step();
            return ticket;
        }
    }
}
