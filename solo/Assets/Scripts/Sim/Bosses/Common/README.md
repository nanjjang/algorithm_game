# Bosses/Common — 보스 공통 뼈대 (G07)

`BossSystem : BossModule`이 싸움 상태 시계, 격노, 단계, 패턴 고르기, 데이터 구동 패턴 실행, 첫 만남·복수를 맡는다.
보스마다 다른 행동은 `IBossBrain`(보스 한 마리에 객체 하나)이 맡는다. 나머지 네 보스(G08)는 **이 문서의 인터페이스와 등록 방법만** 쓰면 된다.

## 파일

| 파일 | 내용 |
|---|---|
| `BossSystem.cs` | SelectBoss, MaxHp, SpawnBoss, Dismiss, ModifyIncomingDamage, ActionHalt |
| `BossSystem.Ai.cs` | 7단계: 상태 시계, 격노, 패턴 고르기, 접촉 피해 |
| `BossSystem.Runner.cs` | 패턴 실행기(steps/repeat/예고/돌진/탄/장애물) |
| `BossSystem.Phases.cs` | 10단계: HpMin, 단계 전환, 사망, 첫 만남(Reflection/Collapse), BeginCollapse |
| `BossRules.cs` | 순수 함수: H(w), 체력, 대기·연사 간격, 격노, 단계, 후보, R1, 기하 |
| `BossPatternData.cs` | `BossPattern`/`BossStep` — 단계 overrides·second_cycle을 반영한 패턴 표 |
| `BossContext.cs` | 싸움 문맥(`BossState.Brain`에 들어 있다) + 도우미, `BossStepInstance`, `BossPatternRun` |
| `IBossBrain.cs` | 두뇌 인터페이스, `BossBrainBase`(기본 구현), `DefaultBossBrain` |
| `BossBrainRegistry.cs` | 보스 ID와 행동 클래스 연결 |
| `BossTuning.cs` | `Resources/DataSolo/G07.json`(bosses.json에 없는 수치) |

## 등록

보스 폴더에 행동 클래스를 두고 `BossBrainRegistry.Build()`에 추가한다.

```csharp
table.Add(Ids.Boss.SporeQueen, () => new SporeQueenBrain());
```

등록이 없는 보스는 `DefaultBossBrain`으로 돈다(플레이어를 쫓고, 데이터의 패턴을 기본 처리로 실행).
테스트는 `sys.Brains.Add(id, () => new 가짜두뇌())`로 바꿔 끼울 수 있다(SpawnBoss 전에).

## IBossBrain (서명)

```csharp
public interface IBossBrain
{
    void Init(BossContext c);                                   // SpawnBoss 직후 한 번
    void Move(BossContext c, PlayerState target);               // 전투 틱마다(동작 멈춤·돌진 중 제외). 상태를 보고 스스로 가린다
    void Always(BossContext c, PlayerState target, bool halted);// 상시 행동(passives). 이동 뒤·패턴 처리 앞
    bool Eligible(BossContext c, BossPattern pattern);          // 지금 고를 수 있나
    string Choose(BossContext c, IReadOnlyList<string> candidates, string picked); // 균등 난수 결과를 강제 규칙으로 바꿈
    void OnPatternStart(BossContext c, BossPattern pattern);
    void OnPatternEnd(BossContext c, BossPattern pattern, bool aborted);
    bool BuildZones(BossContext c, BossStep step, BossStepInstance inst, PlayerState target, List<ZoneSpec> zones); // true = 직접 짰다
    void OnTelegraphStart(BossContext c, BossStep step, BossStepInstance inst);    // 예고가 허가돼 떴다
    BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target); // Default / Done / Busy
    void OnDashMove(BossContext c, BossStep step, BossStepInstance inst, Vector2 from, Vector2 to);
    void OnDashEnd(BossContext c, BossStep step, BossStepInstance inst, bool hitWall);
    void OnPhaseChanged(BossContext c, int oldPhase, int newPhase);
    void OnStateChanged(BossContext c, BossFightState oldState, BossFightState newState); // 표시용 클립 등
    float ModifyIncomingDamage(BossContext c, float amount, bool isCrit, Vector2 fromPos); // 부위별 배율
    bool TouchesPlayer(BossContext c, PlayerState p);           // 접촉 판정(기본: 몸통 원 하나)
    void OnFightEnd(BossContext c);                             // 사망·되비침·쓰러짐·Dismiss. 여러 번 불려도 된다
}
```

`BossBrainBase`를 상속하면 전부 기본 구현이 있다. **두뇌가 하지 않는 것**: `BossState.State`·`StateStartTick`을 바꾸기,
`PlayerState.InputLocked`·`AttackLocked`를 건드리기, 격노·단계·사망 판정, 정적 가변 필드. 상태는 두뇌 객체의 필드에 둔다.

`BossContext`의 주요 멤버: `W`, `Boss`, `Body`(몸통 EnemyState — 주인이 Bosses라 Pos·Facing·Tags·Burrowed·AnimClip을 바꿔도 된다),
`Def`, `Common`, `Tuning`, `Sys`, `Patterns`/`Pattern(key)`, `PhaseDef`, `Rng`(RngStream.Boss), `Halted`, `SecondCycle`, `Run`(진행 중 패턴).
도우미: `Chase(to, speed)`, `MoveBody(delta)`, `DirTo(pos)`, `HitRadius(p)`, `SetAnim(clip)`, `IntervalTicks(step)`(연사형이면 격노 반영),
`Fire(...)`, `FireRing(...)`, `FireSpread(...)`, `FireAttack(attack, pos, aim, extraDegrees, castId)`,
`Summon(summonDef, positions)`(NoReward, SummonerId = 몸통 Id, field_cap은 SummonerId로 센다), `CountSummoned(defId)`,
`DefaultZones(step, inst, target, zones)`, `Place(placement, count, playerPos, into)`, `ExpireHazard(h)`, `BreakObstacle(o)`.
`BossStep`: `Attack`(덮어쓰기 반영), `Repeat`, `Count`, `RepeatIntervalSeconds`, `RepeatAngleDegrees`, `StartDelaySeconds`,
`TelegraphEachRepeat`, `Def`(원본), 그 밖의 덮어쓰기 열쇠는 `step.Float("sweep_degrees", 기본)`·`step.Int("shard_count", 기본)`·`step.Has(key)`.

## 틱 규칙

- **7단계(Stage7_Ai)** 순서: 상태 시계(Intro 72 → Waiting + `BossIntroEnd`, PhaseTransition 36, Reflection 180, Collapse 240 — `RunPhase`·`CombatActive`와 무관)
  → 격노 갱신 → (`CombatActive`이고 몸통이 살아 있고 `NearestPlayer`가 있을 때만) `Move` → `Always` → 패턴 처리 → 접촉 피해.
  대상이 없으면 그 틱은 아무것도 하지 않는다.
- **대기**: Waiting에 들어간 틱 T(격노 n) → T + 90 × (10 − n)/10 틱에 패턴을 고른다. 후보 = 단계의 patterns − 직전 패턴, `Eligible`로 거름 →
  비면 직전 포함 → 그래도 비면 30틱 더. `BossPatternStarted`(Id = 패턴 key, A·B = `c.PatternEventA/B` — `OnPatternStart`에서 채울 수 있다).
- **예고**: step에 모양과 예고 시간이 있으면 → `BuildZones`(false면 `DefaultZones`) → `Threats.CheckEscape`(막히면 3틱마다 다시, 첫 예고가 120틱 넘게 막히면
  다른 패턴, 뒤 예고가 그러면 패턴을 끊는다) → `Threats.RequestTelegraph`(CountAsOne) → 허가를 `TryGetTiming`으로 읽는다(요청한 다음 틱에 보인다).
- **자체 타격은 `w.Tick >= detonateTick`인 틱의 7단계**(= 판정 틱)에 `Act`로 온다. 돌진이면 그 틱부터 몸이 움직인다.
  `Act`가 `Default`를 돌려주면 kind대로: `dash` 돌진 / `ring_shot`·`spread`·`aimed_shot` 탄 / 그 밖(zone·lob)은 판정을 Threats에 맡기고 `obstacle`만 세운다 /
  공격 없는 step은 `summon`이 있으면 졸개. `Busy`를 돌려주면 다음 틱에 `Act`가 다시 불린다(`inst.Phase == Busy`).
- **step 차례**: 다음 step은 앞 step의 마지막 반복이 끝난 틱(= 판정 틱)에 시작한다(+ `start_delay_seconds`).
  반복: 간격 0 = 앞 반복이 끝난 틱에 다음 / 간격 > 0 + `telegraph_each_repeat` = 예고 시작 사이가 간격(예고가 겹친다) /
  간격 > 0 + 예고 한 번 = 첫 타격 뒤 간격마다 타격(뒤 반복은 `inst.Dir`을 물려받는다). 구역 없는 예고(`copy.warn_seconds`, `summon.warn_seconds`, `cast_seconds`)는 그 틱만큼 기다린 뒤 `Act`.
- **돌진**: 띠 예고는 `BaseDamage = 0`, `DamageSource = Dash`로 청한다(피해는 몸 충돌이 준다). 돌진 중 `Body.DashActive/DashCastId/DashBaseDamage/DashDir`이 켜지고,
  지나간 선분에 닿은 플레이어에게 `Player.QueueHit(Source = Dash, AttackId = 몸통 Id, 칸 = 그 틱의 Threats.DamageCells(damage, Dash, 몸통))` — 돌진 한 번에 플레이어당 한 번.
  설치 벽(얼음 병풍)은 `Arts.OnEnemyTouchedScreen(dash: true)` 뒤 계속 달린다. `active_ticks` 0 = 벽까지.
  두뇌가 직접 돌진시키려면 `Act`에서 `c.Sys.StartDash(c, inst, dir, activeTicks, baseDamage)` 뒤 `return c.Sys.DashTick(c, step, inst) ? BossActResult.Done : BossActResult.Busy;`
  (속도는 `step.Attack.DashSpeed`. 그 뒤 틱은 뼈대가 옮긴다). 뇌광창의 동작 멈춤 중에도 돌진은 계속된다.
- **접촉**: `contact_damage > 0`이면 싸움 중 매 틱 `TouchesPlayer` → `QueueHit(Contact, AttackId = 몸통 Id)`. 쿨다운(1.5초)은 `QueueHit`이 true일 때만 소비.
  등장·단계 전환·돌진 중·`Body.Burrowed`일 때는 없다. 머리·마디·꼬리는 `TouchesPlayer`만 덮어쓰면 "같은 적 쿨다운 하나"가 된다.
- **빈틈**: 패턴이 끝나면 `recovery_seconds`만큼 Recovery(받는 피해 × `recovery_damage_ratio`) → Waiting. 끊긴 패턴은 빈틈이 없다.
- **동작 멈춤**(`ActionHalt`): 그동안 `Move`가 불리지 않고 대기·빈틈·반복 간격·구역 없는 예고가 멈춘다. 이미 뜬 예고와 돌진은 그대로 간다. `Always`에는 `halted = true`가 온다.
- **10단계(Stage10_AfterDamage)**: `HpMin` 갱신 → 첫 만남 종료 조건 → 사망(`State = Dead`, `BossKilled`, 복수 대상이면 `RevengeKill`, 졸개·적 탄 제거.
  `RunPhase`가 이미 `RunEnded`면 두 이벤트를 내지 않는다) → 단계 전환(패턴 끊음, 미판정 예고 삭제, `Patterns` 다시 만듦, `Body.Tags` = 단계 꼬리표, `OnPhaseChanged`).
- **피해 조정**: Intro·Reflection·Collapse·CollapseDone·Dead는 0, Recovery는 × 배율, 그 뒤 두뇌의 `ModifyIncomingDamage`.

## 이벤트 보탬

- `BossPatternStarted`: 보통 Id = 패턴 key. 허상의 따라 쓰기는 A = 비기 번호(`Ids.Art.All`의 순서, 1부터), B = 예고 틱.
  되비침의 만개 연출은 Id = `BossSystem.ReflectionBloomKey`("reflection_bloom"), A = 몇 번째(1~3) — 판정 없음.
- 보스 몸통도 `EnemySpawned`(Id = 보스 id)를 한 번 낸다. 그 뒤 `BossIntroBegin`.

## 예: 고리 탄을 두 번 쏘는 특수 step

```csharp
public sealed class ExampleBrain : BossBrainBase
{
    int _left;
    public override bool Eligible(BossContext c, BossPattern p) => p.Key != "summon_sprouts" || c.CountSummoned(Ids.Enemy.SporeSeedling) < 8;
    public override BossActResult Act(BossContext c, BossStep step, BossStepInstance inst, PlayerState target)
    {
        if (step.Attack == null || step.Attack.Key != "special") return BossActResult.Default;   // 나머지는 뼈대에 맡긴다
        if (inst.Phase == BossStepPhase.Act) _left = 2;                                            // 첫 호출(판정 틱)
        c.FireAttack(step.Attack, c.Body.Pos, c.DirTo(target.Pos), 0f, inst.CastId);
        return --_left > 0 ? BossActResult.Busy : BossActResult.Done;                              // Busy면 다음 틱에 다시 불린다
    }
}
```
