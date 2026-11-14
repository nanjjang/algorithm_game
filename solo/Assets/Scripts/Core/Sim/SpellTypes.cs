using System.Collections.Generic;
using UnityEngine;
using Wizard.Solo.Data;

namespace Wizard.Solo.Sim
{
    /// <summary>컴파일 진단 코드. 03 요약서 4.6절. E_* = 오류(출발 불가), W_* = 경고.</summary>
    public enum SpellDiagCode
    {
        E_CORE_EMPTY, E_CORE_NOT_PROJECTILE, E_SLOT_COUNT_MISMATCH,
        W_TRIGGER_NO_PROJECTILE, W_NO_EFFECT, W_SPLIT_DUPLICATE, W_TRIGGER_DEPTH, W_PROJECTILE_CAP
    }

    public struct SpellDiagnostic
    {
        public SpellDiagCode Code;
        /// <summary>슬롯 번호(0 = 코어, 1..L = 연결 칸). W_PROJECTILE_CAP은 생략된 첫 스코프 깊이.</summary>
        public int Slot;
        public bool IsError => Code <= SpellDiagCode.E_SLOT_COUNT_MISMATCH;
        /// <summary>이름 표 열쇠(warn.core_empty 등). 개발용 코드는 null.</summary>
        public string NameKey
        {
            get
            {
                switch (Code)
                {
                    case SpellDiagCode.E_CORE_EMPTY: return "warn.core_empty";
                    case SpellDiagCode.E_CORE_NOT_PROJECTILE: return "warn.core_not_projectile";
                    case SpellDiagCode.W_TRIGGER_NO_PROJECTILE: return "warn.trigger_no_projectile";
                    case SpellDiagCode.W_NO_EFFECT: return "warn.no_effect";
                    case SpellDiagCode.W_SPLIT_DUPLICATE: return "warn.split_duplicate";
                    case SpellDiagCode.W_TRIGGER_DEPTH: return "warn.trigger_depth";
                    case SpellDiagCode.W_PROJECTILE_CAP: return "warn.projectile_cap";
                    default: return null;
                }
            }
        }
    }

    /// <summary>스코프의 변형 누적. 03 요약서 4.4절.</summary>
    public struct ScopeModifiers
    {
        public float DamageBonusSum;
        public int BounceCount;
        public int PierceCount;
        public int SizeDoublings;
        public bool Homing, Frost, TripleHeat, Corrosion;
    }

    public struct ScopeProjectile
    {
        public BlockDef Block;
        public int Slot;
    }

    /// <summary>실행 트리의 스코프 하나(S0~S3 한 줄 사슬).</summary>
    public sealed class SpellScope
    {
        public int Depth;
        /// <summary>이 스코프를 연 트리거의 슬롯(-1 = 루트).</summary>
        public int TriggerSlot = -1;
        /// <summary>주인 투사체의 슬롯(-1 = 투사체 없음).</summary>
        public int OwnerSlot = -1;
        public readonly List<ScopeProjectile> Projectiles = new List<ScopeProjectile>();
        public ScopeModifiers Mods;
        public int SplitCount = 1;
        public int SplitSlot = -1;
        public float FanHalfAngleDegrees;
        /// <summary>분열 배수 전 열 합(HU. 밑줄을 깎은 뒤).</summary>
        public int HeatSumHu;
        public float HeatMul = 1f;
        /// <summary>HeatSumHu × HeatMul(HU, 정수).</summary>
        public int HeatHu;
        public int InstancesPerParent;
        public int InstancesTotal;
        /// <summary>감쇠^깊이.</summary>
        public float DepthMultiplier = 1f;
    }

    /// <summary>슬롯 배열을 컴파일한 결과. 03 요약서 4.5절. 불변으로 다룬다(조립이 바뀌면 새로 만든다).</summary>
    public sealed class CompiledSpell
    {
        /// <summary>컴파일에 사용한 트리당 탄 상한. 다른 콘텐츠를 컴파일해도 진단 값은 바뀌지 않는다.</summary>
        public int TreeShotCap;
        public bool IsValid;
        public BlockDef Core;
        public int FireIntervalTicks;
        public readonly List<SpellScope> Scopes = new List<SpellScope>();
        /// <summary>발사 1회 열(HU). 거울상 +50% 제외 값.</summary>
        public int HeatPerShotHu;
        public int InstancesTotal;
        public int InstancesTotalMirrored;
        public int MaxDepthNormal;
        public int MaxDepthMirrored;
        /// <summary>표준 T의 바탕 = Σ 루트 스코프 탄(분열 반영, 거울상 제외) 기본 피해 × (1 + 루트 변형 보너스 합).
        /// T = 이 값 × 강화 배율 × 변이 배율. 캐릭터 배율은 피해를 줄 때 곱한다(00 문서 결정 D-07).</summary>
        public float StandardTBase;
        public readonly List<SpellDiagnostic> Diagnostics = new List<SpellDiagnostic>();
        /// <summary>컴파일에 쓴 슬롯(0 = 코어). HUD 회로·트리 미리보기용.</summary>
        public string[] SlotBlockIds;
        public float DepthDecay;
    }

    /// <summary>컴파일 입력의 캐릭터·지팡이 쪽 값.</summary>
    public struct SpellCompileContext
    {
        /// <summary>연결 칸 총수 L = base_link_slots + slots_extra. links 길이와 다르면 E_SLOT_COUNT_MISMATCH.</summary>
        public int LinkSlotCount;
        /// <summary>패시브 밑줄(맨 끝 연결 칸 열 깎기).</summary>
        public bool Underline;
        /// <summary>깊이 감쇠(0.85, 지팡이 특성 0.92).</summary>
        public float DepthDecay;
    }

    /// <summary>트리 실행 요청. 03 요약서 6절 Execute의 인자.</summary>
    public struct ExecuteRequest
    {
        public int OwnerSlot;
        public CompiledSpell Tree;
        public Vector2 Pos;
        /// <summary>화면 각도(도, 반시계 +). Dir8Util.ToDegrees(facing).</summary>
        public float DirDegrees;
        /// <summary>비기 배율(J 1, 마력 방출 2, 사본 0.5, 곁가지 0.7/0.6 …).</summary>
        public float DamageMul;
        public bool HeatExempt;
        public bool IsCrit;
        public ShotSource Source;
        /// <summary>스침 묶음·콤보 +3 상한을 나누는 발사 번호. 자손 탄이 물려받는다.</summary>
        public int ShotId;
        /// <summary>이번 호출이 만들 수 있는 최대 탄 수(비기 160 예산의 남은 양). 0 이하 = 제한 없음.</summary>
        public int ProjectileBudget;
        /// <summary>캐릭터 배율(그림자 계약). 발사·시전 순간 값으로 고정해 넘긴다.</summary>
        public float CharacterMul;
        /// <summary>맞은 적에게 붙는 덧효과(반동 사격의 넉백·돌진 취소).</summary>
        public ShotHitEffect HitEffect;
        /// <summary>뒤틀린 총열의 흔들림을 적용하지 않는다(사본·비기 곁가지 등 호출자가 정한다).</summary>
        public bool NoSpread;
    }

    public enum ShotHitEffect { None, RecoilKnockback }

    public struct ExecuteResult
    {
        /// <summary>더할 열(HU). 면제면 0, 거울상이면 ×1.5.</summary>
        public int HeatToAddHu;
        public int Spawned;
        public int Requested;
        public bool Mirrored;
    }

    /// <summary>블록을 거치지 않는 피해(비기·패시브·지대). 03 요약서 9절 DM-8.</summary>
    public struct DirectDamage
    {
        public int AttackerSlot;
        public int TargetEnemyId;
        /// <summary>nT × 캐릭터 배율까지 곱한 값. 속성·대상 상태·적 쪽 조정은 받는 쪽이 곱한다.</summary>
        public float Amount;
        /// <summary>피해 속성 id(Ids.Element.*).</summary>
        public string Element;
        public DamageOrigin Origin;
        public bool IgnoresArmor;
        /// <summary>출처 비기·패시브 id(표시·기록용).</summary>
        public string SourceId;
    }

    public struct DamageResult
    {
        public float Applied;
        public bool Killed;
        public Affinity Affinity;
        /// <summary>땅속 등으로 아무 일도 없었다.</summary>
        public bool Immune;
    }

    /// <summary>적중 하나의 사실. 모듈 훅(콤보, 살얼음, 탐식)에 넘긴다.</summary>
    public struct HitInfo
    {
        public int AttackerSlot;
        public int EnemyId;
        public DamageOrigin Origin;
        public ShotSource ShotSource;
        public int ShotId;
        public bool IsCrit;
        public string Element;
        public float Amount;
        public bool Killed;
        public Vector2 Pos;
        public Affinity Affinity;
    }
}
