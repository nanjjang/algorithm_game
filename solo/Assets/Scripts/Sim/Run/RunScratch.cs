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
        /// <summary>솔로 조정: 이 웨이브까지는 결정 1개를 쥐고 시작한다(0 = 없음, 명세 그대로).</summary>
        public int WaveStartCrystalUntilWave;

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
            t.WaveStartCrystalUntilWave = (int)F(jo, "wave_start_crystal_until_wave");
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
        /// <summary>튜토리얼 단계 표(DataSolo/Tutorial.json). Mode == Tutorial일 때만 읽는다.</summary>
        public TutorialPlan Plan = new TutorialPlan();

        // ---- 웨이브
        public int PreludeTicks;
        /// <summary>이번 웨이브에 지갑에 들어온 골드(처치 + 클리어 + 보스 + 환전). WaveResult.GoldEarned.</summary>
        public int WaveGold;
        /// <summary>떼 출현 보상 나누기(RunFlow.HordeReward.cs). 웨이브 구성이 바뀌면 새로 만든다.</summary>
        public RunFlow.HordeShare HordeShare;
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

        // ---- 전리품 개편(DataSolo/Loot.json)
        public LootTuning Loot;
        /// <summary>사람(슬롯)마다: 맞물림 카드가 하나도 없던 제시(다시 뽑기 포함)가 이어진 수. 운 보정이 읽는다.</summary>
        public readonly int[] LootDryOffers = new int[SimConst.MaxPlayers];
        /// <summary>사람(슬롯)마다: 이 판에서 이미 보인 맞물림(카드 블록 + 짝). 조회만 한다(순회하지 않는다).</summary>
        public readonly HashSet<string>[] SeenSynergy = NewSeen();

        static HashSet<string>[] NewSeen()
        {
            var a = new HashSet<string>[SimConst.MaxPlayers];
            for (int i = 0; i < a.Length; i++) a[i] = new HashSet<string>();
            return a;
        }

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
        // 관문 진척(단계가 바뀌면 비운다)
        public int TutHits;
        public int TutCritStreak;
        public int TutGrazes;
        public int TutGrazePendingUntil;
        public int TutLastGrazeBuffTick;
        public bool TutWasLocked;
        public bool TutLockSeen;
        public int TutCrystalsAtStart;
        public int TutSpent;
        public bool TutArtPaid;
        public bool TutCommandArt;
        public int TutReverseShots;
        public int TutProgressSent = -1;
        public bool TutStepPrepared;
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
            TutHits = 0; TutCritStreak = 0; TutGrazes = 0; TutGrazePendingUntil = -1; TutLastGrazeBuffTick = int.MinValue;
            TutWasLocked = false; TutLockSeen = false; TutCrystalsAtStart = 0; TutSpent = 0; TutArtPaid = false; TutCommandArt = false;
            TutReverseShots = 0; TutProgressSent = -1; TutStepPrepared = false;
        }
    }
}
