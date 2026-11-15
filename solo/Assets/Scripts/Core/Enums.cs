using System;
using UnityEngine;

namespace Wizard.Solo
{
    /// <summary>8방향. 반시계 45도씩. N = 화면 위(시뮬레이션 좌표에서는 y−). 02 요약서 4.1절.</summary>
    public enum Dir8 { E = 0, NE = 1, N = 2, NW = 3, W = 4, SW = 5, S = 6, SE = 7 }

    /// <summary>논리 키. 번호는 02 요약서 3.1절 표 그대로.</summary>
    public enum LogicalKey { Up = 0, Left = 1, Down = 2, Right = 3, Fire = 4, Skill = 5, Stance = 6, Reverse = 7, Pause = 8 }

    /// <summary>자세 중 방향 입력의 상대 방향(커맨드 판정기 알파벳). 04 요약서 1.1절.</summary>
    public enum CmdDir { Forward = 0, Back = 1, Left = 2, Right = 3 }

    public enum StanceState { None = 0, Stance = 1, Reverse = 2 }

    public enum FacingArrowMode { Normal = 0, Stance = 1, Reverse = 2 }

    /// <summary>열 링 상태. 02 요약서 6.6절.</summary>
    public enum HeatRingState { Cold, Rising, Imminent, Critical, Brink, Locked }

    public enum HeatUnlockReason { Timeout, Graze, VentBlast, Reset }

    public enum ComboResetReason { Timeout, Hit, UnstableCore, Rise, WaveEnd }

    public enum CrystalSource { Combo, Starfall }

    public enum CommandResetReason { StanceEnter, StanceExit, Reverse, Pause, WaveStart, Fired, Fizzled }

    /// <summary>주문 트리를 실행한 출처. 03 요약서 6절.</summary>
    public enum ShotSource { Fire = 0, ManaDischarge = 1, ArtOther = 2, CopyCircle = 3, EnemyCopy = 4 }

    /// <summary>피해 출처. 콤보·임계·살얼음 셈에 쓴다. 10 요약서 1.2절.</summary>
    public enum DamageOrigin { Projectile = 0, Art = 1, Passive = 2, Zone = 3, Environment = 4 }

    public enum Affinity { Normal = 0, Weak = 1, Resist = 2 }

    /// <summary>피해 숫자 종류. 01 요약서 12절, 08 요약서 6.2절.</summary>
    public enum DamageNumberKind { Normal, Crit, Weak, Resist, Gold }

    public enum BlockKind { Projectile = 0, Control = 1, Modifier = 2 }

    [Flags]
    public enum EnemyTags
    {
        None = 0, Elite = 1, Heavy = 2, Boss = 4, Flying = 8, Burrowed = 16, Rooted = 32
    }

    /// <summary>군중 제어 종류. 02 요약서 8.5절 ResolveCc.</summary>
    public enum CcKind { Knockback, Pull, Freeze, Stagger, Stun, Slow }

    /// <summary>군중 제어를 건 것(꼬리표별 예외 판단용).</summary>
    public enum CcSource { Recoil, Overflow, ThunderSpear, Kiln, BoltDash, ThinIce, IceScreen, ShadowHand, BlockSlow,
        /// <summary>기본 공격 유형(핵 블록)의 덧효과: 당김·빙결 누적 등(Docs/12-attack-styles.md). 꼬리표 예외는 블록 감속과 같다.</summary>
        AttackStyle,
        /// <summary>고유 비기 강화 단계(불기둥·낙뢰 궤적·책장 소용돌이·얼음 가시·그림자 손 짓누름, Docs/12 4절).</summary>
        ArtStage }

    /// <summary>감속 출처별 칸. 더하지 않고 가장 큰 값 하나만 적용한다. 03 요약서 10.1절.</summary>
    public enum SlowSlot { IceSpike = 0, Frost = 1, Kiln = 2, ThinIce = 3, Count = 4 }

    /// <summary>적 공통 상태. 05 요약서 4절.</summary>
    public enum EnemyAiState { Spawning, Act, Waiting, WindUp, Strike, Recovery, Interrupted, Dead }

    /// <summary>예고 모양 5종. 05 요약서 1.2절.</summary>
    public enum ZoneShape { Circle, Band, Fan, AimLine, Ring }

    /// <summary>플레이어가 받는 피해 분류. 05 요약서 3.1절.</summary>
    /// <remarks>Dash = 예고 뒤 돌진의 몸 충돌. 칸 계산은 Telegraph와 같다(공격 정의의 damage + 성장). 번호는 더하기만.</remarks>
    public enum PlayerDamageSource { Contact, Projectile, Telegraph, Lingering, Scripted, Dash }

    public enum HazardKind { ThornField, EmberStrip, SporeCloud, FrostField, ThornBorder, DeathCircle }

    public enum GrazeKind { Bullet, Dash, Zone, BoltDash }

    /// <summary>플레이어가 깐 것. 10 요약서 3.1절 "내 지대" 층.</summary>
    public enum PlayerZoneKind { Kiln, CopyCircle, IceScreen }

    /// <summary>기본 공격 유형의 범위 타격 종류(StyleBlast 이벤트의 B). Docs/12-attack-styles.md 3절. 번호는 더하기만 한다.</summary>
    public enum StyleBlastKind { MortarLand = 0, MortarFloorTick = 1, SkyStrike = 2, IcicleDrop = 3, RuneBurst = 4, OrbZap = 5 }

    /// <summary>기본 공격 유형이 적에게 거는 누적 상태(StyleStatus 이벤트의 B).</summary>
    public enum StyleStatusKind { Burn = 0, Chill = 1 }

    /// <summary>고유 비기 강화의 단계(ArtStage 이벤트의 A). Docs/12-attack-styles.md 4절. 번호는 더하기만 한다.</summary>
    public enum ArtStageKind { Warn = 0, Rise = 1, Land = 2, Blast = 3, Grip = 4, Crush = 5, Pulse = 6, End = 7 }

    public enum ProjectileState { Pending, Flying, Returning, Dead }

    public enum DropKind { HealthShard, MaterialBundle, GoldCoin }

    public enum StarfallPhase { None, Scheduled, Telegraph, Landed, Picked, Expired }

    /// <summary>Duel = 온라인 1대1 결투(Docs/11-online.md, docs/spec/10-duel.md).</summary>
    public enum RunMode { Endless, Practice, Tutorial, Duel }

    /// <summary>연습 방 옵션(08 요약서 8.5절). RunCommand.PracticeSetOption의 A 값(B = 1 켬 / 0 끔).
    /// HeatFreeze = "열 고정": 켠 순간의 열 값에 묶는다(발사·냉각으로 변하지 않고 잠금에 들어가지 않는다. 00 문서 D-57).</summary>
    public enum PracticeOption { InfiniteCrystal = 0, EnemyFire = 1, HeatFreeze = 2 }

    /// <summary>소리 묶음(10 요약서 5.2절, 설정 vol_*). Master는 전체 음량.</summary>
    public enum SoundBus { Master = 0, Bgm = 1, Sfx = 2, Wand = 3, Ui = 4, Enemy = 5 }

    /// <summary>판 상태 기계. 06 요약서 4.1절.</summary>
    public enum RunPhase
    {
        NotStarted, StartCamp, WavePrelude, BossIntro, WaveCombat, PhantomFinale, PhantomDowned,
        WaveClearing, LootPick, Camp, Practice, RunEnded,
        /// <summary>결투: 라운드 시작 카운트다운(움직이지 못한다) → 싸움 → 라운드 끝(승패 표시). 번호는 뒤에만 더한다.</summary>
        DuelCountdown, DuelFight, DuelRoundEnd
    }

    /// <summary>야영 하위 상태. 06 요약서 4.3절.</summary>
    public enum CampSubState { FireIdle, CampScreen, DepartHold }

    public enum RunEndReason { None, HpZero, Abandon }

    /// <remarks>CollapseDone = 첫 만남의 쓰러짐(240틱)이 끝났다 → Run이 일어남 처리를 한다. 번호는 더하기만.</remarks>
    public enum BossFightState { Intro, Waiting, Telegraph, Execute, Recovery, R5Blocked, PhaseTransition, Reflection, Collapse, Dead, CollapseDone }

    /// <summary>재료 추첨 기회의 출처. 06 요약서 5.1절.</summary>
    public enum DropChanceSource { CrowBundle, FifthWave, BossKill, EliteKill, PhantomFirst }

    public enum ProfileKind { Offline, Server }

    /// <summary>복수 추적 상태. 05 요약서 10.3절.</summary>
    public enum RevengeState { BeforeMeeting, Pursuing, Done }

    public static class Dir8Util
    {
        const float D = 0.70710678f;
        static readonly Vector2[] SimVec =
        {
            new Vector2(1, 0), new Vector2(D, -D), new Vector2(0, -1), new Vector2(-D, -D),
            new Vector2(-1, 0), new Vector2(-D, D), new Vector2(0, 1), new Vector2(D, D)
        };

        /// <summary>시뮬레이션 좌표(y 아래가 +)의 단위 벡터. N = (0, −1).</summary>
        public static Vector2 ToSimVector(Dir8 d) => SimVec[(int)d & 7];
        public static Dir8 Flip(Dir8 d) => (Dir8)(((int)d + 4) & 7);
        public static Dir8 Rotate(Dir8 d, int steps45Ccw) => (Dir8)(((int)d + steps45Ccw) & 7);
        public static bool IsCardinal(Dir8 d) => ((int)d & 1) == 0;
        /// <summary>각도(도). 0 = +x, 반시계 +(화면 기준). 시뮬레이션 벡터로는 (cos, −sin).</summary>
        public static float ToDegrees(Dir8 d) => (int)d * 45f;
        /// <summary>자세 중 누른 물리 방향 → 상대 방향. diff 0 앞, 4 뒤, 2 왼옆, 6 오른옆. 02 요약서 4.4절.</summary>
        public static CmdDir Relative(Dir8 pressedCardinal, Dir8 lockedFacing)
        {
            int diff = ((int)pressedCardinal - (int)lockedFacing) & 7;
            switch (diff)
            {
                case 0: return CmdDir.Forward;
                case 4: return CmdDir.Back;
                case 2: return CmdDir.Left;
                default: return CmdDir.Right;
            }
        }
        /// <summary>화면 각도(도, 반시계 +) → 시뮬레이션 단위 벡터.</summary>
        public static Vector2 DegreesToSimVector(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(r), -Mathf.Sin(r));
        }
        /// <summary>시뮬레이션 벡터 → 화면 각도(도, 반시계 +, 0~360).</summary>
        public static float SimVectorToDegrees(Vector2 v)
        {
            float a = Mathf.Atan2(-v.y, v.x) * Mathf.Rad2Deg;
            return a < 0 ? a + 360f : a;
        }
    }

    public static class EnemyTagsUtil
    {
        public static EnemyTags Parse(System.Collections.Generic.IEnumerable<string> tagIds)
        {
            var t = EnemyTags.None;
            if (tagIds == null) return t;
            foreach (var id in tagIds)
            {
                switch (id)
                {
                    case Ids.Tag.Elite: t |= EnemyTags.Elite; break;
                    case Ids.Tag.Heavy: t |= EnemyTags.Heavy; break;
                    case Ids.Tag.Boss: t |= EnemyTags.Boss; break;
                    case Ids.Tag.Flying: t |= EnemyTags.Flying; break;
                    case Ids.Tag.Burrowed: t |= EnemyTags.Burrowed; break;
                    case Ids.Tag.Rooted: t |= EnemyTags.Rooted; break;
                }
            }
            return t;
        }
    }
}
