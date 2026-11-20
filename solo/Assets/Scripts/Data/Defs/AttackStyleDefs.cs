using System.Collections.Generic;
using Newtonsoft.Json;

namespace Wizard.Solo.Data
{
    /// <summary>기본 공격 유형의 탄 동작. AttackStyleParams.Behavior 글자("lob" …)와 같은 이름. Docs/12-attack-styles.md 2절.</summary>
    public enum AttackBehavior
    {
        /// <summary>동작 글자가 없다(부속 탄 — 얼음 창 파편 등은 보통 직선 탄).</summary>
        None = 0,
        /// <summary>포물선으로 날아 떨어진 자리에서 터지고 불바닥을 남긴다(화로 포탄).</summary>
        Lob,
        /// <summary>앞쪽 부채꼴을 반지름(또는 각도)으로 휩쓴다(불꽃 방사, 눈보라, 그림자 파동, 그림자 낫).</summary>
        Cone,
        /// <summary>빠른 작은 탄이 벽·적에 튕긴다(튀는 불씨).</summary>
        Ricochet,
        /// <summary>즉발 선 + 옮겨 붙기(연쇄 번개 동작을 그대로 쓴다).</summary>
        Chain,
        /// <summary>느린 구체가 꿰뚫고 지나가며 주기적으로 둘레를 지진다(번개 구체).</summary>
        Orb,
        /// <summary>앞쪽 한 자리에 예고 뒤 범위 타격(낙뢰).</summary>
        Strike,
        /// <summary>앞쪽 줄을 따라 차례로 떨어지는 범위 타격(고드름 낙하).</summary>
        Icicles,
        /// <summary>여러 장이 휘며 날아가 한 번 꿰뚫는다(책장 날리기).</summary>
        Pages,
        /// <summary>갔다 돌아오며 두 번 맞힌다(책 던지기. 부메랑 동작을 그대로 쓴다).</summary>
        Boomerang,
        /// <summary>앞에 새긴 지뢰가 밟히거나 시간이 다 되면 터진다(룬 문장).</summary>
        Mine,
        /// <summary>느리고 멀리 꿰뚫다가 멈추는 곳에서 파편으로 깨진다(얼음 창).</summary>
        Lance,
        /// <summary>적을 쫓고 처치하면 둘로 갈라진다(눈알 추적탄).</summary>
        Seeker
    }

    /// <summary>
    /// 기본 공격 유형 핵 블록의 동작 수치(DataSolo/AttackStyles.json blocks[].style). 단위는 JSON 그대로(초, 칸, 도, 비율)이고
    /// 쓰는 쪽(Sim/Spells)이 틱으로 바꾼다. 없는 값은 0(그 동작이 없다). 뜻은 Docs/12-attack-styles.md 2절 표.
    /// </summary>
    public sealed class AttackStyleParams
    {
        /// <summary>동작 글자: lob, cone, ricochet, chain, orb, strike, icicles, pages, boomerang, mine, lance, seeker.</summary>
        public string Behavior;
        /// <summary>주인 캐릭터(char.N)와 자리(a/b/c). 부속 탄은 null.</summary>
        public string Character;
        public string Slot;
        /// <summary>특징 꼬리표(이름 표 str.attack_style.tag.&lt;꼬리표&gt;).</summary>
        public List<string> Tags = new List<string>();

        // ---- 공통
        /// <summary>표준 T의 바탕으로 쓰는 값(0 = 기본 피해). 비기·패시브 피해 단위를 마력탄과 같게 둔다.</summary>
        public float StandardTBase;
        /// <summary>J 발사가 아닌 실행(비기·사본 마법진)에서 이 블록 피해에 곱한다(0 = 1). 발사 1회 피해가 큰 유형이 비기로 몇 배가 되지 않게.</summary>
        public float ArtDamageScale;
        /// <summary>한 번의 발동(폭발·부채꼴·탄의 한 다리)에서 둘째 적부터 받는 피해 배율(0 = 1). 범위 유형이 무리에서 과하지 않게.</summary>
        public float ExtraTargetMul;
        /// <summary>조립 수치(허수아비 DPS)의 어림: 과녁 하나가 발사 1회에 받는 피해 ÷ 기본 피해(지속 피해 포함). 0 = 1.</summary>
        public float EstimateHits;
        /// <summary>이미 꿰뚫는 유형에서 관통 수 1당 피해 보너스(0.05 = 관통 +2 블록 하나에 +10%).</summary>
        public float PierceBonusPerCount;
        /// <summary>J 발사 탄이 처음 맞힌 순간 더 주는 콤보(발사 1회에 한 번). 느린 유형의 결정 속도를 마력탄과 맞춘다.</summary>
        public int ComboOnHit;
        /// <summary>둘째 적부터 맞힐 때마다 더 주는 콤보(그림자 낫).</summary>
        public int ComboPerExtraHit;
        /// <summary>튕김 변형이 "되풀이"(부채꼴 메아리, 포탄 한 번 더 튐, 낙뢰 다시 떨어짐, 지뢰 다시 새김)일 때 그 피해 배율.</summary>
        public float EchoDamageMul;

        // ---- 겨누기(포탄·낙뢰·지뢰·고드름·부채꼴 유도)
        /// <summary>앞 ±각 안의 가장 가까운 적을 겨눈다(0 = 겨누지 않음).</summary>
        public float AimConeDegrees;
        public float AimRange;
        /// <summary>겨눌 적이 없을 때의 거리, 가장 가까운 거리.</summary>
        public float DefaultDistance;
        public float MinDistance;

        // ---- 포탄
        public float FlightSeconds;
        public float BlastRadius;
        /// <summary>튕김 변형: 떨어진 뒤 앞으로 이만큼 한 번 더 튄다(튕김 블록 하나에 한 번).</summary>
        public float BounceHopDistance;
        public float BounceFlightSeconds;
        /// <summary>불바닥: 시간, 반경, 피해 간격, 한 번 피해(기본 피해 대비 비율).</summary>
        public float FloorSeconds;
        public float FloorRadius;
        public float FloorIntervalSeconds;
        public float FloorDamage;

        // ---- 부채꼴
        public float ConeRadius;
        public float ConeHalfDegrees;
        public float SweepSeconds;
        /// <summary>"radial"(반지름이 퍼진다) / "angular"(각도로 휩쓴다, 번갈아).</summary>
        public string Sweep;
        /// <summary>원점이 쏜 사람을 따라간다(불꽃 방사·눈보라·낫).</summary>
        public bool Attached;
        /// <summary>맞은 적을 쏜 사람 쪽으로 당기는 거리(칸).</summary>
        public float PullDistance;

        // ---- 화상(불꽃 방사·튀는 불씨)
        public int BurnStacksPerHit;
        public int BurnMaxStacks;
        public float BurnSeconds;
        public float BurnIntervalSeconds;
        /// <summary>한 겹이 화상 한 번에 주는 피해(맞힌 피해 대비 비율).</summary>
        public float BurnDamagePerStack;

        // ---- 냉기 누적(눈보라)
        public float ChillSlowPerStack;
        public float ChillSeconds;
        public int ChillFreezeAt;
        public float FreezeSeconds;

        // ---- 튀는 불씨
        public int Ricochets;
        public float RicochetRadius;
        /// <summary>블록 자체의 벽 튕김 수(튕김 변형과 더한다).</summary>
        public int WallBounces;

        // ---- 번개 구체
        public float ZapIntervalSeconds;
        public float ZapRadius;
        /// <summary>지짐 한 번 피해(기본 피해 대비 비율).</summary>
        public float ZapDamage;
        public int ZapTargets;

        // ---- 낙뢰
        public float DelaySeconds;
        /// <summary>튕김 변형: 같은 자리에 다시 떨어지기까지.</summary>
        public float RestrikeSeconds;

        // ---- 고드름 낙하
        public int DropCount;
        public float DropStart;
        public float DropStep;
        public float DropGapSeconds;
        public float DropRadius;

        // ---- 책장
        public int Copies;
        public float CopySpreadDegrees;
        public float CurveDegreesPerSecond;
        /// <summary>블록 자체의 관통 수(관통 변형과 더한다).</summary>
        public int Pierce;

        // ---- 책 던지기
        /// <summary>튕김 변형 블록 하나당 편도 사거리 덤(칸).</summary>
        public float BounceRangeBonus;

        // ---- 룬 문장
        public float ArmSeconds;
        public float FuseSeconds;
        public float TriggerRadius;
        public float RearmSeconds;
        public int MaxMines;

        // ---- 얼음 창
        public string ShardBlock;
        public int ShardCount;
        public float ShardSpreadDegrees;

        // ---- 눈알 추적탄
        public float SeekTurnDegreesPerSecond;
        public float SeekConeDegrees;
        public float SeekRange;
        public int SplitCount;
        public int SplitGenerations;
        public float SplitDamageMul;
        public float SplitSpreadDegrees;

        // ---- 연쇄 번개
        /// <summary>튕김 변형 블록 하나당 옮겨 붙는 반경 덤(칸).</summary>
        public float HopRadiusBonusPerBounce;

        AttackBehavior? _kind;

        /// <summary>Behavior 글자를 열거로(모르는 글자 = None).</summary>
        [JsonIgnore]
        public AttackBehavior Kind
        {
            get
            {
                if (_kind.HasValue) return _kind.Value;
                AttackBehavior k;
                switch (Behavior)
                {
                    case "lob": k = AttackBehavior.Lob; break;
                    case "cone": k = AttackBehavior.Cone; break;
                    case "ricochet": k = AttackBehavior.Ricochet; break;
                    case "chain": k = AttackBehavior.Chain; break;
                    case "orb": k = AttackBehavior.Orb; break;
                    case "strike": k = AttackBehavior.Strike; break;
                    case "icicles": k = AttackBehavior.Icicles; break;
                    case "pages": k = AttackBehavior.Pages; break;
                    case "boomerang": k = AttackBehavior.Boomerang; break;
                    case "mine": k = AttackBehavior.Mine; break;
                    case "lance": k = AttackBehavior.Lance; break;
                    case "seeker": k = AttackBehavior.Seeker; break;
                    default: k = AttackBehavior.None; break;
                }
                _kind = k;
                return k;
            }
        }

        /// <summary>각도로 휩쓰는 부채꼴(낫)인가.</summary>
        [JsonIgnore] public bool AngularSweep => Sweep == "angular";

        /// <summary>범위·관통이 본래 있어 관통 변형을 피해 보너스로 바꾸는 유형인가(Docs/12 2.2절 표).</summary>
        [JsonIgnore]
        public bool PierceIsBonus
        {
            get
            {
                switch (Kind)
                {
                    case AttackBehavior.Lob:
                    case AttackBehavior.Cone:
                    case AttackBehavior.Orb:
                    case AttackBehavior.Strike:
                    case AttackBehavior.Icicles:
                    case AttackBehavior.Mine:
                    case AttackBehavior.Boomerang:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>몸이 날아가며 부딪치는 탄인가(벽 튕김·유도 조향·관통 수를 그대로 쓴다).</summary>
        [JsonIgnore]
        public bool BodyProjectile
        {
            get
            {
                switch (Kind)
                {
                    case AttackBehavior.Ricochet:
                    case AttackBehavior.Orb:
                    case AttackBehavior.Pages:
                    case AttackBehavior.Boomerang:
                    case AttackBehavior.Lance:
                    case AttackBehavior.Seeker:
                    case AttackBehavior.None:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>0이면 1인 배율(ArtDamageScale, ExtraTargetMul, EstimateHits).</summary>
        public static float OneIfZero(float v) => v > 0f ? v : 1f;
    }
}
