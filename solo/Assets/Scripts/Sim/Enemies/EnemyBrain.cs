using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim.Enemies
{
    public enum EnemySpecies { Chaser, Wolf, Golem, Shaman, Bug, Hog, Weasel, Bat, Wisp, Vine, Boar, Toad, Dummy }

    /// <summary>엘리트 고유 능력(elites.json ability). 05 요약서 7절.</summary>
    public enum EliteAbility { None, Split, Howl, Shards, WideSpread, BigBlast, TripleAim, EarthBands, Summon, TwinRing, Bloom, Bounce, BigDrum }

    /// <summary>종별 하위 상태(공통 상태 EnemyAiState 안의 세부).</summary>
    public enum BrainSub
    {
        None,
        Dodge, Howl,                 // 늑대
        Cast,                        // 술사·도깨비불(구역 없는 시전)
        Surface, Dig,                // 족제비
        Vanish, Appear,              // 도깨비불
        BounceStop, WallStun,        // 멧돼지
        Croak                        // 북두꺼비
    }

    public struct PendingShot
    {
        public int Tick;
        public EnemyShotSpec Spec;
    }

    /// <summary>삼연 고슴도치의 조준선 하나.</summary>
    public struct AimSlot
    {
        public bool Requested, Granted, Done;
        public int Ticket;
        public int CastId;
        public int StartTick, DetTick;
        public Vector2 Dir;
    }

    /// <summary>적 하나의 AI 상태(EnemyState.Brain). 주인은 EnemySystem뿐이다. 시간은 전부 틱 번호.</summary>
    public sealed class EnemyBrain
    {
        public EnemySpecies Species;
        public EliteAbility Ability;
        public JObjectView Ai;
        public JObjectView Params;
        /// <summary>바탕 공격에 엘리트의 replaces를 덮은 목록(첨자 = 바탕 공격 첨자).</summary>
        public AttackDef[] Attacks = new AttackDef[0];
        /// <summary>follows로 붙는 공격(첨자 = 바탕 공격 첨자, 없으면 null).</summary>
        public AttackDef[] Follows = new AttackDef[0];
        /// <summary>replaces도 follows도 아닌 엘리트 전용 공격(박쥐 여왕 급강하).</summary>
        public AttackDef Extra;

        public int SpawnTicks;
        /// <summary>접촉 피해 간격(틱).</summary>
        public int ContactTicks;
        public BrainSub Sub;
        public int SubEndTick;

        // 예고 표
        public bool HasTicket;
        public int Ticket;
        public bool Granted;
        public int RequestTick;
        public int StartTick, DetTick;
        public int CastId;
        public AttackDef Pending;
        public int PendingIndex = -1;
        public Vector2 LockedDir;
        public Vector2 TargetPos;
        public bool SelfTarget;
        /// <summary>follows 공격의 표(화분 어미 흙 띠). 주인은 기다리지 않는다.</summary>
        public bool HasFollowTicket;
        public int FollowTicket;

        // 타이머
        public int NextAttackTick;
        public int RecoveryEndTick;
        public int CastEndTick;
        public Vector2 CastDir;
        public int CastRepeatLeft;
        public int CastRepeatTotal;

        // 이동 의도(7단계가 정하고 9단계가 움직인다). 칸/초.
        public Vector2 Vel;
        /// <summary>북두꺼비 버프·감속을 받는 평소 이동인가(회피 대시는 아니다).</summary>
        public bool VelIsWalk;

        // 돌진
        public AttackDef DashAttack;
        public int DashEndTick;
        public bool DashToWall;
        public int DashHitMask;
        public int ChainLeft;
        public int BouncesLeft;
        public Vector2 BounceDir;
        /// <summary>이번 돌진이 튕긴 뒤의 것인가(재사용 대기는 첫 돌진에서만 센다).</summary>
        public bool Bounced;
        public readonly List<int> PathHitIds = new List<int>();

        // 늑대
        public int DodgeReadyTick;
        public Vector2 DodgeDir;
        public int HowlEndTick = -1;
        public readonly List<int> HowlTargets = new List<int>();
        public int HowlNextIndex;
        /// <summary>우두머리가 시킨 돌진(발동 거리와 무관하게 청한다).</summary>
        public bool ForcedDash;

        // 북두꺼비 버프(받는 쪽)
        public int BuffUntilTick;
        public float BuffSpeedRatio, BuffSpeedCap, BuffRecoveryRatio;
        // 북두꺼비(우는 쪽)
        public int NextCroakTick;
        public float CroakDamage;

        // 족제비
        public int BurrowStartTick;
        // 박쥐
        public Vector2 WavePoint;
        public int WavePhase;
        public Vector2 WaveDir;
        public int SummonsDone;
        public float[] SummonRatios;
        // 도깨비불
        public int NextBlinkTick;
        public int NoCastUntilTick;
        // 꽈리
        public bool Exploded;
        public bool BoarTriggered;
        // 고슴도치 엘리트
        public AimSlot[] Aims;
        public bool AimFired;
        // 허수아비
        public int NextFireTick = -1;

        public readonly List<PendingShot> Shots = new List<PendingShot>();
    }

    /// <summary>종별 ai·엘리트 params(JObject)를 수로 읽는 얇은 껍데기. 열쇠가 없으면 0.</summary>
    public sealed class JObjectView
    {
        readonly Newtonsoft.Json.Linq.JObject _j;
        public JObjectView(Newtonsoft.Json.Linq.JObject j) { _j = j; }
        public float F(string key, float fallback = 0f) => EnemyTuning.F(_j, key, fallback);
        public int Ticks(string secondsKey) => SimConst.Ticks(EnemyTuning.F(_j, secondsKey));
        public bool B(string key) => EnemyTuning.B(_j, key);
        public bool Has(string key) => _j != null && _j[key] != null;
        public float[] Floats(string key)
        {
            var a = _j != null ? _j[key] as Newtonsoft.Json.Linq.JArray : null;
            if (a == null) return new float[0];
            var r = new float[a.Count];
            for (int i = 0; i < r.Length; i++) r[i] = (float)a[i];
            return r;
        }
    }
}
