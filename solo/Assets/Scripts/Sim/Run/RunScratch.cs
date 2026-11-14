using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Run
{
    /// <summary>DataSolo/G10.json의 값(판 흐름 보충 수치). 파일이 없으면 전부 0 = 그 기능이 꺼진다.</summary>
    internal sealed class RunTuning
    {
        public float DummyOffsetX, DummyOffsetY;
        public float DummyHpPool, DummyHpFloor;
        public float HealthShardPickupRadius;
        public float GoldPullRadius;
        public float TutorialMoveCells;
        public int TutorialLastStep;
        public int RevengeWave;
        public float PracticeDpsMinSeconds;

        public static RunTuning From(IContentDb db)
        {
            var t = new RunTuning();
            JObject jo = db != null ? db.SoloExtra(RunFlow.ExtraName) : null;
            if (jo == null) return t;
            t.DummyOffsetX = F(jo, "dummy_offset_x");
            t.DummyOffsetY = F(jo, "dummy_offset_y");
            t.DummyHpPool = F(jo, "dummy_hp_pool");
            t.DummyHpFloor = F(jo, "dummy_hp_floor");
            t.HealthShardPickupRadius = F(jo, "health_shard_pickup_radius");
            t.GoldPullRadius = F(jo, "gold_pull_radius");
            t.TutorialMoveCells = F(jo, "tutorial_move_cells");
            t.TutorialLastStep = (int)F(jo, "tutorial_last_step");
            t.RevengeWave = (int)F(jo, "revenge_wave");
            t.PracticeDpsMinSeconds = F(jo, "practice_dps_min_seconds");
            return t;
        }

        static float F(JObject jo, string key)
        {
            JToken v = jo[key];
            return v == null || v.Type == JTokenType.Null ? 0f : v.Value<float>();
        }
    }

    /// <summary>
    /// RunFlow가 세계마다 따로 갖는 보조 상태(RunState에 자리가 없는 것). 표시 층은 읽지 않는다.
    /// 정적 필드가 아니라 RunFlow 인스턴스의 표(세계 → 이것)에 든다.
    /// </summary>
    internal sealed class RunScratch
    {
        public RunTuning Tuning = new RunTuning();

        // ---- 웨이브
        public int PreludeTicks;
        /// <summary>이번 웨이브에 지갑에 들어온 골드(처치 + 클리어 + 보스 + 환전). WaveResult.GoldEarned.</summary>
        public int WaveGold;
        /// <summary>지금의 WaveClearing이 복수 처치 연출인가(J로 넘길 수 있다).</summary>
        public bool RevengeClearing;
        // 전리품 생성에 쓰는 방금 넘긴 웨이브의 사실(Bosses.Dismiss·Waves.EndWave 뒤에도 남게 따로 든다)
        public int ClearedWave;
        public bool ClearedIsBoss;
        public bool ClearedPhantomFirst;
        public int ClearedElites;
        /// <summary>웨이브 20 허상의 최대 체력(흠집 % 계산). 0 = 아직 모름.</summary>
        public float PhantomHpMax;

        /// <summary>이 판에서 얻은 야생 블록 전부(uid → 기록). 지팡이에 끼워 보관함에서 빠진 것의 등급·얻은 웨이브를 잃지 않게.</summary>
        public readonly Dictionary<int, WildBlock> WildByUid = new Dictionary<int, WildBlock>();

        // ---- 연습 방 수치판
        public int DummyId;
        public float TotalDamage;
        public int FirstDamageTick = -1;
        public float[] Recent = new float[0];
        public int RecentIndex;
        public int Shots, CritShots, CrystalsSpent;
        public int NextReportTick;
        public readonly int[] LastFireSeen = new int[SimConst.MaxPlayers];
        public readonly Vector2[] LastPos = new Vector2[SimConst.MaxPlayers];
        public readonly bool[] HasLast = new bool[SimConst.MaxPlayers];

        // ---- 튜토리얼 단계의 진행(단계가 바뀌면 비운다)
        public float TutMoved;
        public float TutStanceMoved;
        public int TutShots;
        public int TutCritShots;
        public bool TutRecoil;
        public bool TutInserted;
        public int TutStepStartTick;
        /// <summary>TutorialStepDone을 이미 낸 단계(0 = 없음).</summary>
        public int TutDoneStep;

        public void ResetPracticeStats(int tick)
        {
            TotalDamage = 0f;
            FirstDamageTick = -1;
            for (int i = 0; i < Recent.Length; i++) Recent[i] = 0f;
            RecentIndex = 0;
            Shots = 0; CritShots = 0; CrystalsSpent = 0;
            for (int i = 0; i < HasLast.Length; i++) { HasLast[i] = false; LastFireSeen[i] = int.MinValue; }
        }

        public void ResetTutorialProgress(int tick)
        {
            TutMoved = 0f; TutStanceMoved = 0f; TutShots = 0; TutCritShots = 0;
            TutRecoil = false; TutInserted = false; TutStepStartTick = tick;
        }
    }
}
