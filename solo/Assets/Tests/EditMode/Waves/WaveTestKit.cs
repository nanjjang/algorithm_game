using System;
using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;
using Wizard.Solo.Sim;
using Wizard.Solo.Sim.Waves;

namespace Wizard.Solo.Tests.Waves
{
    /// <summary>예고·남는 구역 요청을 받아 적기만 하는 가짜 Threats.</summary>
    public sealed class FakeThreats : ThreatModule
    {
        public readonly List<TelegraphRequest> Requests = new List<TelegraphRequest>();
        public readonly List<int> RequestTicks = new List<int>();
        public readonly List<HazardSpec> Hazards = new List<HazardSpec>();
        public readonly List<int> HazardTicks = new List<int>();
        public readonly List<int> Cancelled = new List<int>();
        /// <summary>null이면 어디든 비어 있다.</summary>
        public Func<Vector2, float, bool> Clear;
        /// <summary>true면 표의 시작 틱 = 청한 틱, 판정 틱 = 시작 + 첫 구역의 예고 길이로 답한다.</summary>
        public bool AnswerTiming;

        public override int RequestTelegraph(SimWorld w, TelegraphRequest req)
        {
            Requests.Add(req);
            RequestTicks.Add(w.Tick);
            return Requests.Count;
        }

        public override bool TryGetTiming(SimWorld w, int ticket, out int startTick, out int detonateTick)
        {
            startTick = 0; detonateTick = 0;
            if (!AnswerTiming || ticket < 1 || ticket > Requests.Count) return false;
            startTick = RequestTicks[ticket - 1];
            detonateTick = startTick + Requests[ticket - 1].Zones[0].TelegraphTicks;
            return true;
        }

        public override HazardEntity SpawnHazard(SimWorld w, in HazardSpec spec)
        {
            Hazards.Add(spec);
            HazardTicks.Add(w.Tick);
            return null;
        }

        public override void CancelTicket(SimWorld w, int ticket) { Cancelled.Add(ticket); }
        public override bool IsClear(SimWorld w, Vector2 pos, float clearance) => Clear == null || Clear(pos, clearance);
    }

    /// <summary>적을 목록에 넣기만 하는 가짜 Enemies(AI 없음).</summary>
    public sealed class FakeEnemies : EnemyModule
    {
        public readonly List<EnemySpawnSpec> Specs = new List<EnemySpawnSpec>();

        public override EnemyState Spawn(SimWorld w, in EnemySpawnSpec spec)
        {
            Specs.Add(spec);
            var def = w.Content.Enemy(spec.DefId);
            var e = new EnemyState
            {
                Id = w.NewEntityId(), DefId = spec.DefId, Def = def, Pos = spec.Pos, PrevPos = spec.Pos,
                Hp = def != null ? def.BaseHp : 1f, MaxHp = def != null ? def.BaseHp : 1f,
                BodyRadius = def != null ? def.BodyRadius : 0f, Ai = EnemyAiState.Act, NoReward = spec.NoReward,
                SummonerId = spec.SummonerId
            };
            w.Enemies.Add(e);
            return e;
        }
    }

    /// <summary>결정 얻기의 답을 정할 수 있는 가짜 Player.</summary>
    public sealed class FakePlayer : PlayerModule
    {
        public bool GainResult = true;
        public int GainCalls;
        public int Gained;

        public override bool GainCrystal(SimWorld w, PlayerState p, CrystalSource source)
        {
            GainCalls++;
            if (GainResult) Gained++;
            return GainResult;
        }
    }

    public sealed class WaveRig
    {
        public SimWorld World;
        public WaveDirector Director;
        public FakeThreats Threats;
        public FakeEnemies Enemies;
        public FakePlayer Player;

        /// <summary>디렉터만 진짜이고 나머지는 가짜·빈 구현인 세계. 판 상태는 WaveCombat, 슬롯 0이 전장 가운데 왼쪽 위에 선다.
        /// horde = 떼 출현 손잡이(G09.json "horde", 솔로 조정)를 쓰는가. 명세 벡터 테스트는 기본(false)으로 명세 값 그대로 돈다.</summary>
        public static WaveRig Create(ulong seed = 1, bool horde = false)
        {
            var rig = new WaveRig
            {
                Director = new WaveDirector { HordeEnabled = horde }, Threats = new FakeThreats(), Enemies = new FakeEnemies(), Player = new FakePlayer()
            };
            var mods = SimModuleSet.Stubs();
            mods.Waves = rig.Director;
            mods.Threats = rig.Threats;
            mods.Enemies = rig.Enemies;
            mods.Player = rig.Player;
            rig.World = new SimWorld(TestContent.Db, mods, seed);
            rig.World.Run.Phase = RunPhase.WaveCombat;
            rig.World.Players[0].Active = true;
            rig.World.Players[0].Pos = new Vector2(6f, 4f);
            return rig;
        }

        public void Steps(int n) { for (int i = 0; i < n; i++) World.Step(); }

        /// <summary>손으로 만든 일반 웨이브 구성: 묶음마다 이끼 슬라임 perBundle[i]마리.</summary>
        public static WavePlan SlimePlan(int wave, params int[] perBundle)
        {
            var plan = new WavePlan { Wave = wave, Tod = WavePlanner.TodOf(TestContent.Db, wave), Frame = Ids.Frame.Mixed, BundleCount = perBundle.Length };
            for (int b = 0; b < perBundle.Length; b++)
                for (int i = 0; i < perBundle[b]; i++)
                    plan.Entries.Add(new WaveEntry { Species = Ids.Enemy.SlimeMoss, Bundle = b });
            return plan;
        }

        public static int Count(WavePlan plan, string species, bool? elite = null)
        {
            int n = 0;
            for (int i = 0; i < plan.Entries.Count; i++)
                if (plan.Entries[i].Species == species && (!elite.HasValue || plan.Entries[i].Elite == elite.Value)) n++;
            return n;
        }

        /// <summary>구성의 비용 합(엘리트는 비용 × elite_cost_mul). includeOutside = 예산 밖 엘리트도 넣는가.</summary>
        public static int Cost(WavePlan plan, bool includeOutside)
        {
            IContentDb db = TestContent.Db;
            int sum = 0;
            for (int i = 0; i < plan.Entries.Count; i++)
            {
                var e = plan.Entries[i];
                if (e.OutsideBudget && !includeOutside) continue;
                sum += db.Enemy(e.Species).Cost * (e.Elite ? db.Waves.EliteCostMul : 1);
            }
            return sum;
        }

        public static WavePlan Build(ulong seed, int wave, PlanOverrides ov = null, PlanTrace trace = null, bool phantomFirst = false)
        {
            return WavePlanner.Build(TestContent.Db, WaveTuning.From(TestContent.Db, false), null, seed, wave, phantomFirst, ov, trace);
        }

        public static PlanOverrides Whims(params string[] ids) => new PlanOverrides { Whims = new List<string>(ids) };
    }
}
