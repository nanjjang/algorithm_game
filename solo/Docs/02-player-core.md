# 02. 플레이어 조작 코어 — 솔로 Unity 구현 요약서

대상: 순정 Unity(MonoBehaviour, float 허용, 결정론·서버·넷코드 없음)로 만드는 솔로(오프라인) 판.
이 문서만 보고 이동·자세·시야·체력·피격·무적·과열·스침·캐릭터 5명의 차이를 코딩할 수 있게 원문을 옮겼다.

출처 약어: `S01 x.y` = `docs/spec/01-sim-core.md`, `S04 x.y` = `docs/spec/04-characters-wands.md`, `S14` = `docs/spec/14-content-ids-assets.md`, `CT` = `docs/dev/CONTRACTS.md`, `Wd/Wc/Wg/Ws/Wm` = `docs/wands/dalgu/chorong/galpi/seonge/geumeum.md`.
표시: [확정] 개발자 확정 / [제안] 원문 시작값(QA 조정 대상) / [임시] 명세가 막지 않으려 넣은 기본값 / [해석] 명세가 두 읽기 중 고른 것.

솔로 제외(한 줄씩): 결투 모드 전체(히트박스 0.30, 무적 없음, 벌칙 창, 순간 피해 상한, 조준 보정 10도, 입력 지연, 성에 금, 결투 피뢰침 등 S01 12, S04 8) / 광장 이동(발사·비기 꺼짐) / 되감기·스냅숏·상태 해시·입력 기록 검증 / 서버 정산·랭킹 / 거래·리그 봉인 / 결제.

---

## 0. 한눈에

| 항목 | 값 | 출처 |
|---|---|---|
| 게임 시간 | 고정 60틱/초. 1틱 = 1/60초 ≈ 16.667ms. 모든 규칙 시간은 정수 틱으로 떨어진다 | S01 2.1 |
| 거리 단위 | 칸. 1칸 = 64px | S01 2.3, DEC 2.1 |
| 전장 | 24×14칸(1536×896px), 사방 벽. 좌표 왼쪽 위 (0,0), **y는 아래가 +** | S01 5.2, 5.6 |
| 조작 | WASD 이동(8방향) / J 발사(누르면 연사) / K 비기 / Shift 자세 / Space 반전 자세 / Esc 일시정지 | S01 4.1 [확정] |
| 이동 방향 = 시야 = 발사 방향 | 자유 조준 없음 | S01 4.1, 5.2 [확정] |
| 피격 판정 원 | 반지름 0.15칸, 중심은 발밑 기준점에서 위로 0.4칸 | S01 6.2, S04 2.3 |
| 피격 후 무적 | 1초 = 60틱 | S01 2.5 |
| 열 | 0~100, 임계 시작 85(캐릭터별), 100 초과면 잠금 | S01 7 |
| 스침 | 감지 반지름 0.6(초롱 0.75), 보상: 열→임계, 다음 1발 열 면제, 잠금 해제, 가속 +20%, 콤보 +2 | S01 8 |

---

## 1. Unity 단위·좌표 환산 제안

원문 코어는 Q32.32 고정소수점(`Fix64`)과 틱을 쓴다. 솔로 Unity에서는 결정론이 필요 없으므로 아래처럼 바꿔 쓴다.

| 원문 | Unity 제안 | 이유 |
|---|---|---|
| 칸(1.0) / `Fix64` / spec 04의 u(= 1/6000칸) | **1 Unity unit = 1칸**, 스프라이트 PPU = 64 | 그림 1px = 화면 1px(기준 화면 1920×1080, DEC 2.1). 속도 4.7칸/초 = 4.7 unit/s 그대로 |
| 좌표 y 아래가 + (0..14) | `unityY = 14 − y` (또는 전장 루트를 y 반전) | 전장 가운데 (12, 7) → Unity (12, 7). N(화면 위) = Unity +y |
| 틱 | `FixedUpdate` 고정 60Hz (`Time.fixedDeltaTime = 1/60f`), 틱 카운터 `int tick` | 모든 기한값("t < until")·냉각·간격이 틱으로 정의돼 있어 그대로 옮겨진다 |
| 열 `HeatHu`(int, 열 1 = 120 HU) | **int HU를 그대로 쓰기 권장** | 임계 경계(정확히 85, 정확히 100)와 냉각 0.75/0.8333/틱이 정확히 정수. float로 하면 검산값(T-H01 등)이 어긋날 수 있다 (S01 2.4) |
| 체력 | int 점수, 1칸 = 100점 [임시] (또는 int 칸) | S01 6.1 |
| 입력 시각 `tick×256+offset`(1/15,360초) | 대각 시야 유지 창(0.08초)과 커맨드 판정용. Unity에서는 `Time.realtimeSinceStartupAsDouble` 또는 Input System 이벤트 시각(double 초) 비교로 충분 | S01 2.1, 5.4 |
| 카메라 | 정사영, `orthographicSize = 1080/2/64 = 8.4375`, 전장(24×14 unit)이 1920×1080 안에 들어감 | |

- 판정 중심(`Pos`) = 그림 발밑 기준점(64×64 캔버스의 (30,60)) + 위로 0.4칸. 코드의 플레이어 위치는 **판정 중심**으로 둔다 (S01 5.6, S04 2.3, S01 15 C14). 스프라이트는 거기서 0.4칸 아래에 발을 맞춘다.
- `Fix64 → float` 변환은 솔로에서는 필요 없다(처음부터 float). 단 열과 틱은 int로 둔다.
- 히트스톱, 화면 흔들림, 시간대 색 전환은 표시만 멈추고 게임 틱은 멈추지 않는다 [조율] (S01 2.1). 틱을 멈추는 것은 일시정지뿐 (CT 0.3, 3.2). 야영 화면이 떠 있어도 틱은 진행한다는 것이 CT 정본이나, 솔로 Unity에서는 야영 중 전투가 없으므로 영향은 시간 기한값뿐이다.

---

## 2. 틱 안의 처리 순서

원문 두 개가 있다. spec 01 3.8의 11단계 [임시]와 CT 3.2의 0~12단계(통합 정본). 솔로 구현은 CT 순서를 따른다(어긋남 목록 M-01).

```
FixedUpdate (틱 t):
 0. 야영·전리품 명령 적용
 1. 기한 처리: 과열 잠금 풀림(t >= UnlockTick → 열 0). 무적·가속 등은 "t >= until"로 읽으므로 따로 할 일 없음
 2. 입력 이벤트 적용(4.3의 정렬 순서): 키 상태 → 자세 → 시야 → 커맨드 판정기(방향, K) → 일시정지
 3. 대각 시야 유지 창 만료 (5.4)
 4. 플레이어 이동과 벽 (5.6)
 5. 비기 발동
 6. J 발사: 과열 판정 → 주문 실행 → 열 반영 (7.3)
 7. 웨이브·적 AI
 8. 예고 대기 줄
 9. 이동과 충돌: 적 이동 → 탄 이동(플레이어 탄, 적 탄) → 플레이어 탄↔적 → 적 탄·돌진·접촉↔플레이어(피격 후보)
    → 판정 틱이 된 예고 구역 → 피격 판정 **뒤** 스침 판정 → 줍기(별똥, 체력 조각)
10. 피해 확정·사망 처리(탐식 포함), 피격 확정, 무적 끝 이벤트
11. 틱 끝: 냉각(7.4), 콤보 끊김 타이머, 별똥, 웨이브 상태 기계
12. t += 1
```

- 같은 단계 안 개체는 id 오름차순 (S01 3.8). 솔로는 플레이어 1명이지만 원문 규칙상 "같은 틱 공격 둘이면 id 작은 것 하나만"(6.2)을 위해 적·탄에 증가하는 정수 id를 준다.
- 참고 구현: `src/Wizard.Sim/Player/SimWorld.Player.cs:116`(2단계), `:127`(3단계), `:143`(4단계 이동), `:168`(10단계 피격 확정), `src/Wizard.Sim/Graze/SimWorld.Graze.cs:195`(9단계 스침).

---

## 3. 키 입력

### 3.1 키 배치 [확정: 기본] [제안: 대체] (S01 4.1, 4.2)

| 논리 키 `LogicalKey` | 번호 | 기본 | 대체 배치 | 기능 | 이름 표 열쇠 |
|---|---|---|---|---|---|
| Up | 0 | W | ↑ | 위 이동 | `str.key_rebind.action.move_up` |
| Left | 1 | A | ← | 왼쪽 | `str.key_rebind.action.move_left` |
| Down | 2 | S | ↓ | 아래 | `str.key_rebind.action.move_down` |
| Right | 3 | D | → | 오른쪽 | `str.key_rebind.action.move_right` |
| Fire | 4 | J | Z | 발사(누르고 있으면 연사) | `str.key_rebind.action.fire` |
| Skill | 5 | K | X | 비기 전용 | `str.key_rebind.action.art` |
| Stance | 6 | Shift(누르는 동안) | C | 자세 | `str.key_rebind.action.stance` |
| Reverse | 7 | Space(누르는 동안) | V | 반전 자세 | `str.key_rebind.action.reverse_stance` |
| Pause | 8 | Esc | — | 일시정지 | `str.key_rebind.action.pause` |

- 전 키 재설정 가능 [제안]. 기본 키·보조 키 두 칸(`str.key_rebind.primary` / `str.key_rebind.secondary`), 충돌 표시 `str.key_rebind.conflict`, 입력 대기 `str.key_rebind.capture_prompt`, 제목 `str.key_rebind.title`.
- 키는 **물리 키 자리**로 읽는다. 한글 입력기 상태와 무관해야 한다(`str.key_rebind.korean_note`). Unity Input System의 `Key`(물리 자리) 사용 권장 (S01 4.1).
- 전투는 키보드만 [확정]. 메뉴는 마우스 가능, 키보드만으로도 된다.
- 대시 없음 [제안].
- 한 논리 키에 물리 키가 둘(W와 ↑)이면 하나라도 눌려 있으면 눌린 것. 논리 상태가 **바뀔 때만** 이벤트를 만든다 [임시] (S01 4.2).

### 3.2 입력 이벤트와 순서 (S01 4.3, 4.4)

- 이벤트 = (유효 틱, 틱 안 시각, 키, 누름/뗌). 솔로 Unity: `Update`에서 키 변화를 감지해 큐에 (시각, 키, down)을 넣고, 다음 `FixedUpdate`가 그 틱의 것을 순서대로 처리.
- 같은 시각에 여러 키가 바뀌면 정렬 키 `(틱, 시각, 키 분류, 순번)`, 키 분류: **자세·반전 0 → 방향 1 → J 2 → K 3 → Pause 4**. 확정은 "방향이 K보다 먼저"뿐이고 나머지는 [임시] (S01 4.3, 15 C4).
- 눌린 키의 누름, 안 눌린 키의 뗌은 무시.
- `KeyState`: 9개 키의 눌림 비트 + 키마다 **마지막 누름 시각**.
- `JPressedThisTick`: 이번 틱에 J 누름 이벤트가 있었으면 참. 같은 틱에 눌렀다 뗀 J도 이번 틱 발사 조건을 만족 [임시]. 쿨다운 중 눌렀다 뗀 J는 보관하지 않는다(버퍼 없음) [임시] (S01 4.4, 7.3).
- 한 틱 한 사람 최대 8개. 넘으면 버림 [임시] (사람 손으로 안 나옴).
- 커맨드(비기 판정기)는 **누르는 순간만** 본다. 이미 누르고 있던 방향은 입력이 아니다 → 판정기에는 Down 이벤트만 넘긴다. 자세 밖의 방향은 커맨드가 아니다 (S01 4.4, 5.5).
- 커맨드 초기화 신호 `CommandReset(reason)`: 자세 진입, 자세 해제, 반전, 일시정지 (S01 4.4).
- 일시정지: 틱 정지, 커맨드 초기화 1회(T-F19). 일시정지 중 뗀 키는 풀리는 틱에 뗌으로 처리 [임시] (S01 3.6). 일시정지 화면 글자: `str.pause.title`, `str.pause.resume`, `str.pause.settings`, `str.pause.controls`, `str.pause.give_up`(+`_confirm`), `str.pause.to_menu`. 누적 표시 `str.pause.pause_total`, `str.pause.pause_limit_note`(순위용 → 솔로 제외, 표시만 할지 미정).
- 참고 구현: 정렬 `src/Wizard.Sim/Input/Types/InputOrder.cs:13`(분류), `:36`(비교). 키 상태 `src/Wizard.Sim/Input/Types/KeyState.cs:32`(Apply, 중복 무시).

---

## 4. 이동, 시야, 자세

### 4.1 방향 (S01 5.2)

```
enum Dir8 { E=0, NE=1, N=2, NW=3, W=4, SW=5, S=6, SE=7 }   // 반시계 45도. N = 화면 위
Flip(d) = (d + 4) & 7
```

- 원문 좌표(y 아래 +)에서 N = (0, −1). 대각 단위 벡터 = (±1/√2, ±1/√2). Unity(y 위 +)로 바꾸면 N = (0, +1).
- 이동 입력 → 방향 [임시]: 가로축 Left만 −1, Right만 +1, **둘 다면 나중에 누른 쪽**(마지막 누름 시각이 큰 쪽), 없으면 0. 세로축도 같다. (0,0)이면 이동 없음.
- 8방향만. **대각 이동 속력은 축 이동과 같게 정규화** [임시] (S01 2.3, U33).
- 참고 구현: `src/Wizard.Sim/World/Movement.cs:55`(ResolveAxis), `:74`(TryDirFromAxes), `src/Wizard.Sim/Player/PlayerRules.cs:51`(TryMoveDir).

### 4.2 시야 상태 기계 (S01 5.3) [확정: 이동=시야=발사]

| 자세 상태 | 시야 규칙 |
|---|---|
| None, 이동 입력 있음 | `Facing = 이동 방향` (4.3 대각 유지 창 중에는 대각 유지) |
| None, 이동 입력 없음 | `Facing` 그대로 |
| Stance / Reverse | `Facing = StanceLockedFacing`(상하좌우 4방향 중 하나). 이동 입력은 시야를 안 바꾼다 |

- 발사 방향 = 항상 `Facing`. 자세 중에는 대각 사격 없음.
- `LastPressedCardinal`: 방향키 누름마다 그 방향으로 갱신.
- 시야 화살표는 항상 발밑. 평소 반투명 / 자세 중 불투명 / 반전 중 다른 색 → `FacingArrowMode {Normal, Stance, Reverse}` (S01 5.3, 17.1). 위험색(색상각 330~15°, 채도·명도 70% 이상) 금지.
- 판 시작 시야: 아래(S) [임시], 위치 (12.0, 7.0) [임시] (S01 3.7, `arena.json` spawn_points).

### 4.3 대각 시야 유지 창 0.08초 [제안] [해석·임시] (S01 5.4)

원문: "대각 이동 중 두 키를 0.08초 안에 차례로 떼면 대각 시야를 유지한다."

```
OnKeyEvent(e)   // Stance == None 일 때만
  if pending.Active and e.Time > pending.ExpireTime:          // 창이 이미 지났다 → 먼저 만료
      Facing = 이 이벤트 직전의 이동 방향(없으면 그대로); pending.Active = false
  if e 가 방향키 뗌:
      prev = 직전 이동 방향, next = 직후 이동 방향
      if prev 가 대각 and next 가 prev 의 축 성분:
          pending = {Active, Diag = prev, ExpireTime = e.Time + 0.08초}; return   // Facing 은 대각 유지
      if pending.Active and next 없음 and e.Time <= pending.ExpireTime:
          Facing = pending.Diag; pending.Active = false; return                  // 대각 유지 확정
  if 방향키 누름: pending.Active = false
  (그 밖은 4.2 규칙)

ResolvePending(t):   // 3단계
  if pending.Active and 틱 t 시작 시각 > pending.ExpireTime:
      pending.Active = false; Facing = 지금 이동 방향(없으면 그대로)
```

- 창이 열린 동안(최대 0.08초) **이동은 남은 한 키 방향으로 바로 바뀌고**, 시야·발사 방향만 대각으로 남는다(T-F16: 창 중 J는 대각으로 나감).
- 경계: 정확히 0.08초 뒤의 두 번째 뗌은 유지(`<=`). 1/15,360초 단위로 0.08초 = 1,229단위.
- 자세에 들어가면 창 취소(4.4에서 시야를 다시 정한다).
- 참고 구현: `src/Wizard.Sim/Player/PlayerMotion.cs:18`(ApplyInput), `:70`(ResolvePending), `:87`(OnDirectionKey).

### 4.4 자세 / 반전 자세 [확정: Shift 자세, Space 반전] (S01 5.5)

- 자세: 시야를 잠그고 느려진 속도로 이동. **이동은 8방향 그대로**, 잠기는 것은 시야뿐 [해석].
- 반전 자세도 자세다(자세 속도, 커맨드 인정).
- 자세 속도는 배율이 아니라 캐릭터의 `stance_speed` **값으로 교체**.

`SnapCardinal(facing)`: 축 방향이면 그대로. 대각이면 그 두 축 성분 중 `LastPressedCardinal`과 같은 쪽, 둘 다 아니면 마지막 누름 시각이 늦은 쪽 [임시].

| 지금 | 이벤트 | 조건 | 다음 | 시야(잠김) | 커맨드 초기화 |
|---|---|---|---|---|---|
| None | Stance 누름 | | Stance | `SnapCardinal(Facing)` | 진입 |
| None | Reverse 누름 | | Reverse | `Flip(SnapCardinal(Facing))` | 진입 |
| Stance | Reverse 누름 | | Reverse | `Flip(Locked)` | 반전 |
| Stance | Stance 뗌 | Reverse 안 눌림 | None | 이동 키가 눌려 있으면 그 방향, 아니면 `Locked` 그대로 [임시] | 해제 |
| Reverse | Reverse 뗌 | Stance 안 눌림 | None | 이동 키가 눌려 있으면 그 방향, 아니면 `Locked`(뒤집힌 방향) | 해제 |
| Reverse | Reverse 뗌 | Stance 눌림 | Stance | `Locked` 그대로(뒤집힌 채) [임시] | 반전 [임시] |
| Reverse | Stance 누름 / 뗌 | | Reverse | 변화 없음 [임시] | 없음 |
| Stance·Reverse | 방향 누름 | | 그대로 | 잠김. 판정기에 **상대 방향**으로 넘김 | |
| None | 방향 누름 | | None | 4.2·4.3 | 판정기에 안 넘김 |

상대 방향(판정기 입력) `diff = (pressed − locked) & 7` → 0 앞(F), 4 뒤(B), 2 왼옆(L), 6 오른옆(R).

| 잠긴 시야 | W | A | S | D |
|---|---|---|---|---|
| N | 앞 | 왼옆 | 뒤 | 오른옆 |
| W | 오른옆 | 앞 | 왼옆 | 뒤 |
| S | 뒤 | 오른옆 | 앞 | 왼옆 |
| E | 왼옆 | 뒤 | 오른옆 | 앞 |

- 참고 구현: `src/Wizard.Sim/Player/PlayerMotion.cs:138`(OnStanceKey), `:183/196/209`(Enter/Switch/Exit), `src/Wizard.Sim/Player/PlayerRules.cs:70`(SnapCardinal), `:93`(Relative).

### 4.5 이동 속도와 배율 (S01 2.3, 5.6, S04 2.2)

```
speed = (자세 또는 반전) ? stance_speed : walk_speed     // 칸/초
mul   = 1.0
if 스침 가속 중 (t < GrazeBuffUntil): mul *= graze_buff_speed (1.2)   // 중첩 없음, 시간만 갱신
if 플레이어 감속(흰뿔 서리밭 −30%, 적 명세가 넣음): mul *= 0.7
pos += dir_unit * speed * mul / 60                      // 틱당. 대각도 단위 벡터라 속력 동일
pos = 벽 자르기(pos)
```

- 플레이어를 느리게 하는 것은 흰뿔(boss.frost_goat로 추정, 적 명세 확인 필요)의 서리밭 −30% 하나뿐 (mon 1.2 → S01 5.6). 둘이 겹치면 곱: 1.2 × 0.7 = 0.84 [임시].
- 정수 버림 규칙(spec 04 0.3 "0 쪽 버림")은 결정론용이라 솔로 float에서는 무시해도 된다.
- 다섯 중 가장 느린 자세 속도 3.76(달구)이 적 회피 보장 기준 V. 감속 전투 기준 2.63 (S01 5.1, S04 14.2). 적 설계 검산용.

속도 검산표(칸/초) (S04 14.2, S01 T-M14·M15):

| | char.1 | char.2 | char.3 | char.4 | char.5 |
|---|---|---|---|---|---|
| 걷기 | 4.7 | 5.6 | 5.0 | 4.5 | 5.2 |
| 자세·반전 | **3.76** | 5.04 | 4.25 | 4.5 | 4.42 |
| 가속 걷기 / 자세 | 5.64 / 4.512 | 6.72 / 6.048 | 6.0 / 5.1 | 5.4 / 5.4 | 6.24 / 5.304 |
| −30% 걷기 / 자세 | 3.29 / 2.632 | 3.92 / 3.528 | 3.5 / 2.975 | 3.15 / 3.15 | 3.64 / 3.094 |

- 참고 구현: `src/Wizard.Sim/World/Movement.cs:39`(SpeedCenti), `:44`(MovePermille), `:150`(Integrate).

### 4.6 전장과 벽 (S01 5.6, `data/sim/arena.json`)

- 무한 웨이브 전장 `place.field`(이름 표 `place.field` = "한뎃벌"): 24×14칸, 사방 막힘, 가장자리 벽 두께 1칸(가시덤불 울타리·돌무더기, 그림). 벽은 탄을 막고 벽 튕김 탄은 튕긴다.
- 플레이어 쪽 판정 있는 장애물은 없다(바위·기둥은 적·보스가 만든다).
- 벽 충돌 몸 반지름 `body_radius = 0.30` [임시]. `Pos`를 `[0.30, 23.70] × [0.30, 13.70]`로 **축별로 따로** 자른다(벽 따라 미끄러짐).
- 플레이어와 적은 서로 밀어내지 않는다(겹치면 접촉 피해) [임시].
- 연습(허수아비) 모드 전장도 `place.field`와 같다 [임시] (arena.json `_note`).
- 결투장 `place.arena`(돌마당, 기둥 4개)는 솔로 제외.
- 참고 구현: `src/Wizard.Sim/World/ArenaGeometry.cs:67`(ResolveBody), `:95`(ClampAxis).

`data/sim/arena.json` 필드:

| 필드 | 뜻 | 단위 |
|---|---|---|
| `arenas[].id` | 장소 id (`place.field` 무한 웨이브·연습, `place.arena` 결투) | 문자열 id |
| `modes` | 이 전장을 쓰는 모드 (`endless`, `practice`, `duel`) | |
| `width`, `height` | 전장 크기 24, 14 | 칸 |
| `pillars[]` `{x, y}` | 1×1칸 기둥의 작은 쪽 모서리 칸 좌표(결투장만) | 칸 |
| `spawn_points[]` `{x, y}` | 슬롯 순 시작 위치(판정 중심). 무한 웨이브 (12.0, 7.0) [임시] | 칸, y 아래 + |

---

## 5. 체력, 피격, 무적 (S01 6, S04 3.4)

### 5.1 수치

| 항목 | 값 |
|---|---|
| 최대 체력(칸) | char.1 8 / char.2 4 / char.3 6 / char.4 7 / char.5 5 (기준값 6) |
| 체력 단위 | 1칸 = 100점 [임시]. 최대 800/400/600/700/500점 |
| 피격 판정 | 반지름 0.15칸 원, 중심 = 판정 중심 |
| 피격 후 무적 | 60틱(1초) |
| 적 공격 한 번 최대 | 3칸(적 쪽 수치에 걸린 상한, monsters 4.2) |
| 접촉 피해 간격 | 같은 적에게서 1.5초(90틱)에 1회 — 적 쪽 값 |
| 판 시작 | 체력 가득, 열 0, 잠금 없음, 콤보 0, 결정 0, 빚 0 (S01 3.7) |

### 5.2 피격 처리

```
OnHit(attack, t):                     // attack.DamageCells 는 이미 3칸 상한이 걸린 값
  if t < InvulnUntil: return          // 무적: 피해도 벌칙도 없음
  if Hp <= 0: return                  // 쓰러진 뒤 [임시: 참고 구현]
  dmg = attack.DamageCells * 100
  if char == char.5 and Debt > 0: dmg += 100     // 빚 +1칸, 3칸 상한 뒤 → 최대 4칸
  Hp = max(0, Hp - dmg)
  InvulnUntil = t + 60
  Combo = 0 (끊김 이유 Hit)
  결정 손실(5.3)
  유리 지팡이 훅(9.4)
  emit PlayerHit(dmg, hpAfter)
  if Hp == 0: emit PlayerDowned → 판 끝 (허상 첫 만남 예외 5.4)
```

- 같은 틱에 공격 둘이 닿으면 **id가 작은 공격 하나만** 들어가고 나머지는 무적에 막힌다 [임시] (T-P05).
- 경계: 틱 100 피격 → 틱 159 무시, 틱 160 피격 (T-P01, P02).
- 비기 무적(잔상 걸음 0.15초, 벼락 질주 0.2초)은 `InvulnUntil = max(InvulnUntil, t + n)`으로 늘린다 (S01 6.2, 참고 `SimWorld.Player.cs:96` GrantPlayerInvuln).
- 무적 중·이번 틱 피격 시 스침은 인정 안 함 (6.3).
- 참고 구현: `src/Wizard.Sim/Player/SimWorld.Player.cs:71`(QueuePlayerHit — id 작은 후보 하나), `:197`(ResolvePlayerHit), `src/Wizard.Sim/Player/PlayerHealth.cs`.

### 5.3 피격 시 결정 손실 (S01 10.4, S04 7.3)

```
char.4(지팡이 특성 얼린 결정): 결정 그대로(콤보만 0). 결정이 있으면 emit CrystalProtected
char.5: 빚 > 0 이면 빚 = min(2, 빚+1)  (결정 파괴 대신)
        아니면 결정 −= min(2, 결정)    // 결정 1개면 0개, 빚은 생기지 않음 [해석/임시 Q-15]
그 밖: 결정 −= min(1, 결정)
```

불변식(char.5): `빚 > 0 ⇒ 결정 == 0`.

### 5.4 쓰러짐 (S01 5.7, 6.3)

- 체력 0 → 판 끝(클리어 없음). 사망 연출 문구 "오늘은 여기까지 위대했다."(S01 17.2) — 이름 표에 열쇠가 없다 → 미정(M-08).
- 예외 [확정 DEC 1.21]: 프로필이 처음 닿는 웨이브 10(스승의 허상 첫 만남, `phantom_first`)에서는 쓰러져도 판이 끝나지 않는다. 맞으면 평소대로 1칸·1초 무적(연출용 무적 없음). 끝은 항상 쓰러짐(체력 0, 또는 45초나 허상 체력 50%에서 마무리 연출 3초), 쓰러짐 연출 4초 뒤 **일어남**:
  - 체력 가득, 열 0, 잠금 풀림, 콤보 0, **빚 0**, 결정은 남겨 평소처럼 웨이브 종료 환전 → 새벽 야영 → 웨이브 11.
  - 세부(45초 등)는 적·보스 명세 소관.

---

## 6. 과열 (S01 7, S04 3.1)

### 6.1 기준값과 캐릭터 값

| 항목 | 값 | HU |
|---|---|---|
| 열 최대(잠금 경계) | 100. **100 초과**면 잠금, 정확히 100은 잠금 아님 | 12,000 |
| 임계 시작 | 기준 85 / char.1 **80**(지팡이 특성) / char.4 **90** / 과민 회로 70 | 10,200 / 9,600 / 10,800 / 8,400 |
| 냉각 대기 | 0.2초 = 12틱 (간격이 끝난 뒤부터) | |
| 냉각 | 초당 45(틱당 0.75) / char.2 초당 50(틱당 0.8333) | 90 / 100 HU/틱 |
| 잠금 | 3.0초 180틱 / char.2 2.5초 150틱 / char.3 3.5초 210틱 | |
| 임계 배율 | 피해 ×2 (과열 3배 블록이면 3, 유리 지팡이 +1) | |

임계 구간: char.1 80~100, char.2·3·5 85~100, char.4 90~100.

임계 시작 결정: `CritStartHu = 120 × min(캐릭터 85|90, 지팡이 wand.1 이면 80, 과민 회로면 70)` — 여럿이면 가장 낮은 값 (S04 9.4). 참고 `src/Wizard.Sim/Heat/HeatRules.cs:26`.

### 6.2 발사 1회의 열 (S01 7.2)

```
ShotHeatHu(tree):
  total = 0
  for scope in 트리의 모든 스코프(트리거 스코프 포함, 적중 여부 무관):
     sum = Σ(분열 블록 제외 블록 열)        // 밑줄(char.3)은 여기서 깎는다 (8.3)
     mult = 첫 분열 블록: 없음 1.0 / 2갈래 1.6 / 3갈래 2.2   // 두 번째 분열은 무시
     total += sum × mult × 120
  if 거울상 변이: total = total × 3/2
  return total   // 항상 정수 HU
```

블록 열(`data/sim/blocks.json`의 `heat`, `heat_multiplier`, `interval_seconds`):

| 투사체 | 간격 | 틱 | 열 | HU |
|---|---|---|---|---|
| 마력탄 `block.magic_bolt` | 0.15초 | 9 | 4 | 480 |
| 거대 화염구 `block.fireball` | 0.6 | 36 | 18 | 2,160 |
| 연쇄 번개 `block.chain_lightning` | 0.45 | 27 | 12 | 1,440 |
| 부메랑 `block.boomerang` | 0.35 | 21 | 10 | 1,200 |
| 얼음 송곳 `block.ice_spike` | 0.3 | 18 | 8 | 960 |
| 그림자 구체 `block.shadow_orb` | 0.5 | 30 | 14 | 1,680 |

변형·제어 블록 열: 벽 튕김 +2, 크기 2배 +3, 관통 +3, 서리 +3, 유도 +5, 과열 3배 +2, 잠식 +5, 적중 시 발동 0, 2갈래 ×1.6, 3갈래 ×2.2. 발사 간격 = 코어 블록의 간격.

검산: 마력탄 + 3갈래 = 8.8(1,056) / + 2갈래 = 6.4(768) / + 거울상 = 6(720) / 3갈래 + 거울상 = 13.2(1,584) / 마력탄 + 적중 시 발동 + (트리거 스코프)화염구 = 22 (T-H19~24).

참고 구현: `src/Wizard.Sim/Heat/ShotHeat.cs:23`(ShotHeatHu), `:77`(UnderlineCutHu).

### 6.3 J 발사 절차 (S01 7.3)

```
TryFire(t):
  want = Fire 눌림 or JPressedThisTick
  if !want: return
  if Locked: if JPressedThisTick: emit FireDryWhileLocked ("지팡이가 헛돈다"); return
  if t < FireReadyTick: return
  h = ShotHeatHu
  exemptUsed = false
  if HeatExemptArmed: h = 0; HeatExemptArmed = false; exemptUsed = true     // 스침 면제가 먼저
  isCrit = Heat + h >= CritStartHu                                          // "발사 후 열" 기준
  if isCrit and !exemptUsed and 불가마 지대 안 and KilnAbsorbLeft > 0:        // char.1 (8.1)
      a = min(KilnAbsorbLeft, h); KilnAbsorbLeft -= a; h -= a
  Heat += h
  willLock = Heat > 12000          // 흡수 뒤 열로 판정 (S04 Q-07)
  if willLock: isCrit = true       // "100을 넘긴 발사는 임계로 나간다"
  unstable = 불안정 핵 변이 and 5% 난수
  주문 실행(isCrit, shotId)
  emit ShotFired(shotId, isCrit, heatAfter);  if isCrit: emit CritShotFired
  interval = 코어 간격; LastFireTick = t; LastFireIntervalTicks = interval; FireReadyTick = t + interval
  if willLock: EnterLock(t, interval, LockTicks + (과민 회로 ? 90 : 0))
  if unstable: UnstableCoreBlowup(t, interval)
```

- 모든 비기는 열을 올리지 않고 임계 배율을 받지 않는다. 비기·사본 마법진·피뢰침은 "발사"가 아니라 냉각 대기를 다시 시작하지 않는다 (S01 7.3, 7.4).
- 발사는 틱 단위. 틱 안 시각은 쓰지 않는다 [임시].
- 참고 구현: `src/Wizard.Sim/Heat/HeatMachine.cs:29`(TryFire).

### 6.4 냉각 (S01 7.4) — [해석] 15 C8: 대기는 마지막 발사의 **간격이 끝난 뒤**부터

```
Cool(t):   // 틱 끝(11단계)
  if Locked: return
  if LastFireTick == t: return
  if t < LastFireTick + LastFireIntervalTicks + 12: return
  cool = cool_per_second * 2  (HU/틱: 45→90, 50→100);  탐식이면 /2 (45, 50)
  Heat = max(0, Heat - cool)
```

예: 마력탄 t0 발사 후 멈추면 t0+21틱의 끝부터 식는다. 참고 `HeatMachine.cs:132`.

### 6.5 잠금 (S01 7.5)

```
EnterLock(t, interval, lockTicks): Locked = true; UnlockTick = t + interval + lockTicks
   emit HeatLockEntered; char.1 이면 끓어넘침(8.1)
ProcessUnlock(t) (1단계): if Locked and t >= UnlockTick: Locked = false; Heat = 0; emit HeatUnlocked(Timeout)
BreakLock(reason, heatAfter): Locked = false; Heat = heatAfter          // 스침, 방열 폭발만
```

- 잠금 중: 발사 불가, 냉각 안 함, 콤보 끊김 타이머 정지. 마력 방출(비기 ①)은 쓸 수 있지만 잠금은 안 풀린다. 잠금을 푸는 수단은 **스침과 방열 폭발뿐**.
- HUD: `LockRemainTicks = UnlockTick − t`, `LockTotal = interval + lockTicks`. 알림 글자 `str.battle_hud.notice.heat_lock`("과열 잠금 {n}초").
- 방열 폭발(비기 ⑦) 인터페이스: 반환 열 = 잠금 중이면 100, 아니면 `Heat / 120`(내림). 잠금이면 BreakLock(VentBlast, 0), 아니면 Heat = 0. 피해 = T × (열 ÷ 25) (S01 7.6.7).
- 참고 `HeatMachine.cs:109`(EnterLock), `:14`(ProcessUnlock), `:154`(BreakLock), `:192`(ConsumeForVentBlast).

### 6.6 열 링 상태(HUD) (S01 7.7, eff 10)

| 상태 | 조건 |
|---|---|
| Cold | 열 0 (잠금 아님) |
| Rising | 0 < 열 < 임계 시작 − 15 |
| Imminent | 임계 시작 − 15 ≤ 열 < 임계 시작 |
| Critical | 임계 시작 ≤ 열 < 95 |
| Brink | 95 ≤ 열 ≤ 100 |
| Locked | 잠금 중 |
| (덧) ExemptArmed | 열 면제 대기 |

- 링 반지름 44px, 발밑, 그 캐릭터의 임계 시작 눈금. 색: 회청색, 임계 흰색+검정 이중 테, 잠금 식은 재색. **금색·빨강 금지** (S01 15 C19).
- 발사당 열 표시 = `ShotHeatHu / 120` 소수 한 자리. 임계 사격 가능 횟수 `n = (12000 − critStartHu) / shotHu + 1`(0이면 ∞, shot > 12000이면 0 → 경고) (S01 7.7). 검산: 마력탄 85→4, 80→6, 90→3.
- 참고 `HeatRules.cs:40`(CritShotCount), `:59`(Ring).

### 6.7 검산 테스트(원문 기대값) (S01 14.5)

| ID | 상황 | 기대 |
|---|---|---|
| T-H01 | 기준값, 열 0, J 계속 | 발사 틱 0, 9, …, 225(26발). 1~21발 일반, 22발(88)~26발 임계. 26발째 104 잠금, UnlockTick 414 |
| T-H02 | 이어서 | 틱 414 풀림, 열 0, 같은 틱에 다음 발사. 주기 6.9초, 44.93 DPS |
| T-H03/04 | 열 96 / 97 + 마력탄 | 100 임계 잠금 아님 / 101 임계 + 잠금 |
| T-H05~08 | 꾹 누르기 1주기 | 달구 47.83 / 초롱 48.44(주기 384틱) / 갈피 41.89(444틱) / 성에 43.48 DPS |
| T-H09 | 열 84, 틱 0·9·18·27 발사 후 뗌 | 88·92·96·100 모두 임계. 틱 48 끝부터 냉각. 틱 69 끝 83.5. 틱 70 발사 87.5 임계 |
| T-H12/13 | 열 50, 마지막 발사 틱 0 | 틱 21 끝부터 0.75/틱(21 끝 49.25) / 초롱 60틱에 정확히 50 |
| T-H10/11 | 임계 유지 정책 6,000틱 | 잠금 0, 평균 DPS 기준 69.6 / 달구 73.8 / 초롱 71.8 / 성에 65.6. 비율 ≥ 1.4 |

화력 검산 표 (S04 14.1): 임계 유지 DPS = 20n ÷ (0.15n + 0.2 + 4n ÷ 냉각). char.1 73.5 / char.2 71.4 / char.3 69.2 / char.4 65.5 / char.5 69.2(결정 4개 77.5, 7개 83.8). 꾹 누르기 47.8 / 48.4 / 41.9 / 43.5 / 44.9. 비율 모두 ≥ 1.4가 통과 기준.

---

## 7. 스쳐 피하기 (S01 8, S04 3.2)

### 7.1 판정

| 항목 | 값 |
|---|---|
| 감지 반지름 R | 0.6칸 / char.2 0.75 |
| 피격 반지름 | 0.15 |
| 인정 대상 | 적의 탄, 적의 돌진(돌진 중 적의 몸 원), 예고 구역이 **터지는 순간** 경계 바깥 R 이내. 보스 탄 전부 대상. 공격 정의의 `Grazeable` 플래그로 가른다 |
| 묶음 | 같은 시전(`CastId`)에서 나온 탄·구역·돌진은 묶어 1회만. 분열·고리·연속 발사라도 한 번의 시전이면 하나 |
| 인정 안 함 | 피격 후 무적 중 [임시], 이번 틱에 맞음 [임시], 얼음 병풍이 막은 탄, 이미 인정한 CastId, 체력 0 |

```
Detect(t):   // 9단계, 피격 판정 뒤
  if t < InvulnUntil (비기 스침 창 제외) or HitThisTick or Hp <= 0: return
  for threat in 위협들 (id 순):
    if !Grazeable or Blocked or GrazedCasts.Contains(CastId): continue
    탄·돌진: d = |Pos − threat.Pos|;  if d <= R + r and d > 0.15 + r: Award
    구역:    그 틱에 터짐 and 플레이어(피격 원)가 구역 밖 and 판정 중심→경계 거리 <= R: Award
```

- 탄: 감지 원과 겹치고 피격 원과는 안 겹치는 **첫 틱**에 인정. 나중에 그 탄에 맞아도 받은 보상은 그대로.
- 구역 거리 R: 원문 "경계 바깥 0.6"이지만 char.2도 0.75를 쓴다 [해석] (S01 15 C6).
- 같은 틱 서로 다른 시전 둘 → 각각 인정(콤보 +4) [임시].
- `GrazedCasts`는 그 시전의 탄이 모두 사라지면 지운다 [임시].
- 검산 (T-G12~19): 탄 반지름 0.12 → 거리 0.70 스침 / 0.73 없음 / 초롱 0.85 스침 / 0.26 피격. 구역 반지름 1.0: 중심 1.5 스침, 1.7 없음, 초롱 1.7 스침, 폭발 전 틱은 없음.
- 참고 구현: `src/Wizard.Sim/Graze/SimWorld.Graze.cs:195`(Stage09_Graze), `:241`(CanGrazeNow), `src/Wizard.Sim/Graze/GrazeRules.cs`(InBand, ZoneEdge).

### 7.2 보상 (무한 웨이브)

```
Award(castId, t):
  GrazedCasts.Add(castId)
  c = CritStartHu
  if Locked: BreakLock(Graze, c); emit LockBrokenByGraze     // 즉시 해제 + 열 = 임계 시작
  else if Heat < c: Heat = c                                 // 낮을 때만 올림
  HeatExemptArmed = true                                     // 다음 발사 1회 열 0. 겹치지 않음(참/거짓)
  GrazeBuffUntil = t + graze_buff_ticks                      // 90틱(char.2 120). 갱신, 중첩 없음
  Combo += 2 (발사 +3 제한과 무관, 끊김 타이머 다시 채움 [임시])
  char.2: 피뢰침 체질 (8.2)
  emit GrazeAwarded(castId, kind)
```

- 잠금 해제와 열 면제 둘 다 준다 [해석].
- 면제 발사의 임계 판정은 `Heat + 0 >= 임계 시작` → 스침 직후 발사는 임계(T-G02).
- 스침 2회 연속 뒤 발사 2발: 첫 발만 면제 (T-G07).
- 가속 중 이동 ×1.2. 표시: 잔상 3장(속성 보조색, 30%).
- 점수(1회 5 × S(w), 일반 20회·보스 40회까지)는 점수 명세 소관.
- 참고 구현: `SimWorld.Graze.cs:256`(AwardGraze), `src/Wizard.Sim/Heat/HeatMachine.cs:167`(ApplyGraze), `src/Wizard.Sim/Heat/SimWorld.Heat.cs:118`(HeatApplyGraze).

---

## 8. 캐릭터 5명 (S04 1~7, S14 3.2, `data/sim/characters.json`)

### 8.1 식별자와 이름 표

| 번호 | id | 이름 표 | 속성 | 지팡이(이름 표) | 패시브 | 지팡이 특성 | 고유 비기 |
|---|---|---|---|---|---|---|---|
| 1 | `char.1` "제자 1" | `element.fire` 화염 | `wand.1` 끓는 부지깽이 | `passive.overflow` 끓어넘침 | `wand_trait.heat_at_80` 임계 구간 80부터 | `art.kiln` 불가마 |
| 2 | `char.2` "제자 2" | `element.lightning` 번개 | `wand.2` 깜빡이 등불대 | `passive.lightning_rod` 피뢰침 체질 | `wand_trait.three_mutations` 변이 3개 보유 | `art.bolt_dash` 벼락 질주 |
| 3 | `char.3` "제자 3" | `element.arcane` 비전 | `wand.3` 밑줄 긋는 참나무 지팡이 | `passive.underline` 밑줄 | `wand_trait.depth_decay` 깊이 감쇠 0.92 | `art.copy_circle` 베껴 쓰기 |
| 4 | `char.4` "제자 4" | `element.ice` 냉기 | `wand.4` 고드름 홀 | `passive.thin_ice` 살얼음 | `wand_trait.keep_crystal` 피격 시 결정 유지 | `art.ice_screen` 얼음 병풍 |
| 5 | `char.5` "제자 5" | `element.dark` 암흑 | `wand.5` 눈 뜬 옹이 지팡이 | `passive.shadow_contract` 그림자 계약 | `wand_trait.on_credit` 외상(빚 결정 2개까지) | `art.shadow_hand` 그림자 손 |

- id는 S14 3.2(정본)와 이름 표·characters.json을 쓴다. spec 04의 `passive.boil_over`, `passive.shadow_pact`, `art.thunder_dash`, `art.transcribe`, `trait.*`, `mut.*`는 옛 표기 → 쓰지 않는다(M-02).
- 코드·데이터에 작업용 이름(달구·초롱·갈피·성에·그믐) 금지. 아래 괄호의 작업용 이름은 읽기용.
- 다섯 명 처음부터 전부 선택 가능. 캐릭터→지팡이 1:1, 지팡이는 그 캐릭터만 쓴다. 판 중 캐릭터·지팡이 안 바꿈. 캐릭터 능력치는 판 사이에 성장하지 않는다(겉모습·이름만) (S04 1.2, 2.2, 13).
- 능력치 화면 라벨: `str.stat.health`, `str.stat.move_speed`, `str.stat.stance_speed`, `str.stat.crystal_cap`, `str.stat.combo_per_crystal`, `str.stat.link_slots`, `str.stat.cooling`, `str.stat.heat_threshold`, `str.stat.lock_time`, `str.stat.graze`, `str.stat.crystal_loss`, `str.stat.passive`, `str.stat.unique_art`, `str.stat.wand_trait`.

### 8.2 `data/sim/characters.json` 필드 (그대로 읽어 쓴다)

구조: `{ "baseline": {...}, "characters": [ {...} ×5 ] }`. `baseline`은 검산·허수아비용 기준값 캐릭터(id 없음, 코드 0). 데이터는 십진 그대로.

| 필드 | 뜻 | 단위 | 1 / 2 / 3 / 4 / 5 (기준) |
|---|---|---|---|
| `id` | 캐릭터 id | | char.1~5 |
| `element` | 속성 id. 패시브·고유 비기 피해의 속성 | | fire / lightning / arcane / ice / dark |
| `passive` | 패시브 id | | 8.1 표 |
| `wand_trait` | 지팡이 특성 id | | 8.1 표 |
| `unique_art` | 고유 비기 id | | 8.1 표 |
| `max_hp_cells` | 최대 체력 | 칸 | 8 / 4 / 6 / 7 / 5 (6) |
| `walk_speed` | 걷기 속도 | 칸/초 | 4.7 / 5.6 / 5.0 / 4.5 / 5.2 (5.0) |
| `stance_speed` | 자세·반전 자세 속도(값 교체) | 칸/초 | 3.76 / 5.04 / 4.25 / 4.5 / 4.42 (4.25) |
| `hit_radius` | 피격 판정 반지름 | 칸 | 0.15 전원 |
| `body_radius` | 벽 충돌 몸 반지름 [임시] | 칸 | 0.30 전원 |
| `graze_radius` | 스침 감지 반지름 | 칸 | 0.6 / **0.75** / 0.6 / 0.6 / 0.6 |
| `invuln_seconds` | 피격 후 무적 | 초 (×60 = 틱) | 1.0 전원 |
| `crystal_cap` | 결정 상한 | 개 | 5 / 5 / 4 / 6 / 7 (5) |
| `combo_per_crystal` | 결정 1개당 콤보 | 콤보 | 15 / **12** / 15 / 15 / 15 |
| `crystal_loss_on_hit` | 피격 시 결정 손실(특성 반영 후) | 개 | 1 / 1 / 1 / **0** / **2** |
| `base_link_slots` | 시작 연결 슬롯(코어 슬롯 별도) | 칸 | 4 / 4 / **5** / 4 / 4 |
| `max_mutations` | 변이 보유 수 | 개 | 2 / **3** / 2 / 2 / 2 |
| `cool_per_second` | 냉각 | 열/초 (×2 = HU/틱) | 45 / **50** / 45 / 45 / 45 |
| `cool_delay_seconds` | 냉각 대기 | 초 (0.2 = 12틱) | 0.2 전원 |
| `crit_start` | 임계 시작(지팡이 특성 반영 후) | 열 (×120 = HU) | **80** / 85 / 85 / **90** / 85 |
| `lock_seconds` | 과열 잠금 | 초 | 3.0 / **2.5** / **3.5** / 3.0 / 3.0 |
| `graze_buff_seconds` | 스침 가속 지속 | 초 | 1.5 / **2.0** / 1.5 / 1.5 / 1.5 |
| `graze_buff_speed` | 스침 가속 배율 | 배 | 1.2 전원 |
| `_note` | 설명. 읽지 않는다 | | |

- 깊이 감쇠(0.85 / char.3 0.92)는 지팡이 쪽 값이라 characters.json에 없다 → 코드에서 `wand_trait.depth_decay`이면 0.92, 아니면 0.85 (S04 7.1). 데이터 위치 미정(M-05).
- 빚 상한 2는 `data/sim/tries.json`(판정기 값, 커맨드 명세 소관).

### 8.3 캐릭터별 정리

#### char.1 (달구, 화염) — 체력 8, 가장 넓은 임계 구간, 가장 느린 자세

- 능력치: 체력 8, 걷기 4.7, 자세 3.76(80%), 결정 상한 5, 콤보 15, 슬롯 4, 냉각 45, 임계 **80**(캐릭터 85 + 지팡이 특성 80, min), 잠금 3.0초, 스침 0.6/1.5초, 결정 손실 1.
- **패시브 끓어넘침** `passive.overflow` (S04 4.1, S01 7.6.6): 과열 잠금에 **들어가는 순간** 1회, 내 자리 반경 2.5칸 안의 적에게 1.5T 화염 피해 + 넉백 2칸(꼬리표별: 일반·비행 2칸, 엘리트 1칸, 중량·보스·뿌리 0, 땅속 효과 없음). 탄은 지우지 않는다. 잠금 길이는 그대로. 경계 포함(거리 ≤ 2.5) [임시]. 콤보에 넣지 않는다 [임시]. 이벤트 `BoiloverTriggered(pos)`. 불안정 핵 자폭 잠금에서 발동 여부는 모순(M-03).
- **지팡이 특성** 임계 구간 80부터.
- **고유 비기 불가마** `art.kiln` (뒤, 뒤, K / 결정 2): 시전 순간 발밑 고정, 반경 2.5칸, 4초(240틱). 지대 안 적: 0.5초마다 0.25T(총 8회 2T, 화염, 시전 뒤 30·60·…·240틱 [임시]), 이동 −20%(보스 −10%, 뿌리 해당 없음, 지대 밖에선 남지 않음 [임시]). 다른 감속과 더하지 않고 큰 값만.
  - 열 흡수: 지대 안(발사 틱 판정 중심이 원 안, 경계 포함 [임시])에서 쏜 **임계 발사**의 열을 합계 16(1,920 HU)까지 대신 먹는다. 임계 판정은 평소대로 먼저. 남은 양보다 크면 남은 만큼만 먹고 나머지는 오른다. 스침 면제가 남아 있으면 면제가 먼저(흡수량 그대로). 동시 1개, 새로 깔면 이전 것 사라짐, 흡수량 이어받지 않음(새 16).
  - 검산 (S04 15.5-71): 열 76에서 깔고 마력탄 연사 → 1~4발 판정 열 80 임계, 흡수(열 76 유지, 16→0). 5~10발 80, 84, …, 100 평소 임계. 10발 연속 임계, 11발째 104 임계+잠금.
  - 데이터: `data/sim/arts.json` `art.kiln.effect` = `{damage 0.25, radius 2.5, duration_seconds 4, interval_seconds 0.5, slow_percent 20, heat_absorb 16, max_concurrent 1}`.
  - 표시: 지대 안에 있으면 지팡이 밑동 주황 빛 한 줄(`InKilnZone`), 열 링 눈금 8×8 표식.
- 지팡이 열 반응(Wd 4.4): 열 0~79 기본 / 80~100 빛나는 부분 한 칸씩 밝게, 김 2배 / 잠금: 무쇠색으로 꺼지고 0.5초마다 한 칸씩 다시 밝아짐(빨강·금색 금지).

#### char.2 (초롱, 번개) — 가장 빠르고 체력 4

- 능력치: 체력 **4**, 걷기 **5.6**, 자세 5.04(90%), 결정 상한 5, 콤보 **12**, 슬롯 4, 냉각 **50**, 임계 85, 잠금 **2.5초**, 스침 **0.75 / 2.0초**, 결정 손실 1.
- **패시브 피뢰침 체질** `passive.lightning_rod` (S04 4.2, S01 8.1-7): 스쳐 피하기 **인정**마다(시전 묶음당 1회) 내 위치 반경 5칸 안의 가까운 적 최대 2체에게 각 1T 번개, 즉시, 쿨다운 없음. 열을 올리지 않고 콤보에 넣지 않는다. 늑대 회피 무시. 땅속 제외. 고르기: 거리 오름차순, 같으면 id 오름차순, 경계 포함 [임시]. 1체뿐이면 1체, 없으면 없음(스침 보상은 그대로). 스침 보상에 **더해진다**. 이벤트 `LightningRodStrike(targets)`.
- **지팡이 특성** 변이 3개 보유(기준 2). 넷째가 가장 오래된 것을 밀어낸다.
- **고유 비기 벼락 질주** `art.bolt_dash` (옆, 앞, K / 결정 2): 시야 정면(4방향) 4칸, 적을 뚫고 지나감, **벽·기둥 앞에서 멈춤**. 이동 중 무적 0.2초(12틱), 이동 시간 12틱 [임시](4칸이면 틱당 1/3칸). 지나간 길 폭 1.0칸 위의 적에게 3T 번개(적마다 1회) + 감전 경직 0.6초(일반·뿌리·비행 0.6, 엘리트 0.3, 중량·보스 없음). 길 위의 적 탄은 스침 1회로 인정(피뢰침도 1회). 데이터 `arts.json` `art.bolt_dash.effect` = `{damage 3, self_move 4, width 1.0, stagger_seconds 0.6, invuln_seconds 0.2}`.
- 지팡이 상태 연동(Wc 3.1): 임계 미만 열 높을수록 깜빡임 빨라짐(초당 2회 상한) / 임계 흰색 고정 / 잠금 모든 등 꺼짐 / 스침 인정 등 2프레임 밝게 / 벼락 질주 잔광 0.3초 / 피격 1초 흔들림.

#### char.3 (갈피, 비전) — 연결 슬롯 5, 조립형

- 능력치: 체력 6, 걷기 5.0, 자세 4.25(85%), 결정 상한 **4**, 콤보 15, 슬롯 **5**, 냉각 45, 임계 85, 잠금 **3.5초**, 스침 0.6/1.5초, 결정 손실 1. (손맛 시제품의 "기준값 캐릭터" = 갈피의 수치, S01 15 C16)
- **패시브 밑줄** `passive.underline` (S04 4.3, S01 7.2): 지팡이의 **마지막 연결 슬롯 칸**(끼운 블록 중 마지막이 아니라 슬롯 인덱스가 가장 큰 칸)에 끼운 블록의 열이 `min(5, ceil(열/2))` 깎인다. 분열 배수는 깎은 **뒤**에 곱한다. 대성공·슬롯 새기기로 슬롯이 늘면 대상 칸도 한 칸 뒤로. 그 칸이 비면 효과 없음. 분열·적중 시 발동 블록은 대상 아님 [임시].

  | 맨 끝 칸 | 원래 | 깎임 | 실제 |
  |---|---|---|---|
  | 변형 +3 | 3 | 2 | 1 |
  | 변형 +5 | 5 | 3 | 2 |
  | 투사체 8 | 8 | 4 | 4 |
  | 투사체 14 | 14 | 5 | 9 |
  | 투사체 18 | 18 | 5 | 13 |

  검산: 코어 마력탄(4) + 맨 끝 화염구(18) → 22가 17. 열 0에서 밑줄 없으면 5발째(110) 잠금, 있으면 6발째(102). 맨 끝 열 8 + 같은 스코프 3갈래 → (8−4)×2.2 = 8.8. 표시: 하단 지팡이 회로 UI의 맨 끝 칸 아래 청록 밑줄(`UnderlineSlotChanged(slotIndex)`).
- **지팡이 특성** 깊이 감쇠 0.85 → **0.92**: 적중 시 발동 깊이 d(0~3)의 피해 항 `decay^d` (0.85: 1, 0.85, 0.7225, 0.614125 / 0.92: 1, 0.92, 0.8464, 0.778688). 표시: 깊이만큼 탄 오른쪽 위 2×2 흰 민트 각주 점(1~3개), 조립 화면 트리 미리보기에도 (Wg 4.2).
- **고유 비기 베껴 쓰기** `art.copy_circle` (앞, 옆, K / 결정 2): 내 자리에 사본 마법진(움직이지 않고 맞지 않음), 6초(360틱). 내가 **J로** 쏘면 마법진도 내 시야와 같은 방향으로 트리를 1회 실행: 피해 50%, 열 0, 콤보 0, 임계 없음 [임시]. 0.3초(18틱)에 한 번까지(그 안의 발사는 따라 쏘지 않음). 12번 쏘면 6초가 남아도 사라짐. 동시 1개, 새로 깔면 이전 것 사라짐(남은 횟수 이어받지 않음). 실행당 탄 64개 상한은 따로, 화면 300개 상한에는 합산. 비기(마력 방출 등)의 트리 실행은 따라 쏘지 않는다. 검산: 마력탄이면 내 발사 2번에 1번, 약 3.6초에 12회 소진.

#### char.4 (성에, 냉기) — 결정 보존, 자세 100%

- 능력치: 체력 7, 걷기 **4.5**, 자세 **4.5(100%)**, 결정 상한 **6**, 콤보 15, 슬롯 4, 냉각 45, 임계 **90**, 잠금 3.0초, 스침 0.6/1.5초, 결정 손실 **0**.
- **패시브 살얼음** `passive.thin_ice` (S04 4.4): 적마다 상태. 같은 적을 **6번** 맞히면 1.0초(60틱) 행동 정지 + 예고 중이던 공격 취소. 한 번 언 적은 빙결 시작부터 6초(360틱) 동안 다시 얼지 않고 횟수도 안 쌓임. 비기로 맞힌 것도 센다. 한 번의 폭발·관통은 적마다 1번. 꼬리표: 엘리트 0.5초·예고 유지 / 중량 빙결 대신 이동 −30% 1초 / 보스 −15% 1초 / 뿌리 일반과 같음 / 비행 뜬 채 빙결 / 땅속 효과 없음, 횟수 유지. 감속형은 다른 감속과 더하지 않고 큰 값만.
  ```
  Counting(n): on Hit: if burrowed: return; n += 1; if n >= 6: 빙결(꼬리표별); immuneUntil = now + 360 → Immune
  Immune: on Hit: 무시; on now >= immuneUntil → Counting(0)
  ```
  검산: 마력탄 한 적만 → 0.9초에 얼림, 주기 6.9초, 언 시간 약 14%.
- **지팡이 특성** 얼린 결정: 피격 시 결정이 파괴되지 않는다(콤보는 0).
- **고유 비기 얼음 병풍** `art.ice_screen` (뒤, 옆, K / 결정 2, 들어오는 간선 0.3초): 시야 정면 1.5칸 앞(판정 중심 → 벽 두께 중심선 [임시]), 폭 3칸 × 두께 0.5칸, 시야에 수직, 4초. 막는 것: 곧게 날아오는 적 탄(막은 탄은 스침 아님), 일반·엘리트 적(못 지나감). 통과: 내 탄(벽 튕김 탄도 튕기지 않고 지나감), 포물선 탄, 비행 적(급강하·활강은 부딪힘). 중량·보스는 닿으면 부숨. 돌진하는 적이 부딪히면 깨지고 그 적 1.5초 경직(엘리트 0.75초, 중량·보스 경직 없이 부수고 계속). 사라질 때(만료·부서짐) 벽 앞쪽 폭 3 × 길이 2칸에 3T 냉기 1회. 다시 깔면 이전 것은 파편 없이 사라짐 [임시]. 동시 1개.

#### char.5 (그믐, 암흑) — 결정 상한 7, 결정당 피해

- 능력치: 체력 5, 걷기 5.2, 자세 4.42(85%), 결정 상한 **7**, 콤보 15, 슬롯 4, 냉각 45, 임계 85, 잠금 3.0초, 스침 0.6/1.5초, 결정 손실 **2**.
- **패시브 그림자 계약** `passive.shadow_contract` (S04 4.5): 캐릭터 피해 배율 = `1 + 0.03 × k`, k = 가진 결정 수(빚은 0개). J 발사와 비기 모두, 블록을 거치지 않는 피해(그림자 손)에도. 비기는 **그 비기가 결정을 쓴 뒤 남은 개수**로(5개에서 2개짜리 → +9%, 만개 +0%, 외상 비기 +0%). 발사·시전 순간 값으로 그 탄에 고정 [임시]. k=0..7 → 1.00~1.21. 표시: 눈빛·구슬 밝기 `60 + 40k/7`% [임시].
- **지팡이 특성** 외상 (S01 10.3, S04 7.4): 결정이 모자라도 비기가 나가고 모자란 만큼 빚(최대 2). 만개는 외상 불가. 판정 순서: 1) 외상 먼저(빚 ≤ 2 범위, 만개 제외) 2) 그래도 모자라고 고유 비기면 부모 기술로 한 번만 내려감(외상 포함 재확인) 3) 불발.
  - 빚은 판이 끝날 때까지 남음(웨이브·야영 넘어감). 예외: 허상 첫 만남 일어남에서 0.
  - 이후 얻는 결정은 빚부터 갚는다(콤보, 별똥). 웨이브 종료 환전(개당 5골드)은 빚 갚고 남은 결정에만.
  - 빚이 있는 동안 받는 피해 +1칸(3칸 상한 뒤 → 최대 4칸. 체력 5칸이라 가득 찬 체력에서 한 방에 안 죽음).
  - 빚이 있을 때 맞으면 결정 파괴 대신 빚 +1(최대 2).
  - 예: 결정 0·빚 1에서 2개짜리 고유 비기 → 외상 불가 → 부모 양익 사격(1개)이 외상으로, 빚 2.
  - HUD: `str.battle_hud.now_k`("K = {art} (◆×{n})") 옆 `str.battle_hud.now_k_debt`("{line} 외상 {n}"). 결정 칸에 빚은 보라 테두리 빈 칸. 지팡이에 빚 1개당 속 빈 연보라 고리(최대 2) (Wm 4.4).
- **고유 비기 그림자 손** `art.shadow_hand` (옆, 반대쪽 옆, K / 결정 2): 중심 = 시야 정면 3칸 지점(벽 밖이면 벽 안쪽으로 자름 [임시]). 그 중심 반경 4.5칸 안 적을 중심으로 최대 3칸 당김(순간, 중심을 넘지 않음, 벽에서 멈춤 [임시]) + 3T 암흑 + 경직 0.5초. 적 탄은 안 움직임. 꼬리표: 일반·비행 3칸·0.5초 / 엘리트 1.5칸·0.25초 / 중량·보스 당김·경직 없음 / 뿌리 당김 없음·경직 0.5초.

### 8.4 고유 비기 공통 (S04 5.1) — 상세는 커맨드·비기 요약서

- 처음부터 쓸 수 있다 [확정 DEC 1.23]. 시작 비기 = 마력 방출 `art.burst`, 반동 사격 `art.recoil`, 그 캐릭터의 고유 비기.
- K로 끝남, 자세 중에만, 열 안 올림, 임계 배율 없음, 강화 배율 적용, 공통 재사용 대기 0.4초(24틱), 대기 끝나기 0.1초 전부터 누른 K는 보관.
- 전부 결정 2개. 결정 모자라면 부모 기술(결정 1)로 한 번 내려감.

| 고유 비기 | 커맨드 | 부모(대체) | 간선 간격 |
|---|---|---|---|
| 불가마 | 뒤, 뒤, K | 반동 사격 | 0.2초 |
| 벼락 질주 | 옆, 앞, K | 양익 사격 | 0.2초 |
| 베껴 쓰기 | 앞, 옆, K | 부채 전개 | 0.2초 |
| 얼음 병풍 | 뒤, 옆, K | 반동 사격 | **0.3초** |
| 그림자 손 | 옆, 반대쪽 옆, K | 양익 사격 | 0.2초 |

- 패시브·고유 비기 피해의 속성 = 캐릭터 속성. 패시브 피해는 비기·임계 배율 안 받음 [임시 Q-02], 콤보에 안 넣음 [임시 Q-03].
- T(트리 1회 피해) [임시 Q-01]: `T = Σ(트리 1회 실행의 투사체마다 기본 피해 × (1+변형 보너스) × 감쇠^깊이) × 강화 배율 × 변이 배율`. 분열은 갈래 수만큼 더함, 트리거 스코프는 모두 적중했다고 보고 더함. 임계·속성·대상·캐릭터 배율은 적용 시 따로. 마력탄 +0 = 10.

### 8.5 꼬리표별 군중 제어 함수 (S04 6)

```
ResolveCc(kind, tags, base):
  if Burrowed: return 0                        // 땅속: 피해 포함 어떤 효과도 없음
  heavyOrBoss = Heavy or Boss
  Knockback/Pull: if heavyOrBoss or Rooted: 0; Elite ? base/2 : base
  Freeze/Stagger/Stun: if heavyOrBoss: (Stun && 뇌광창 && Boss) ? 0.3초 : 0; Elite ? base/2 : base
  Slow: (Boss && (블록 감속 or 불가마)) ? base/2 : base
```

- 꼬리표가 겹치면 더 강한 면역. 감속은 더하지 않고 가장 큰 값 하나.
- id: `tag.none/elite/heavy/boss/flying/burrowed/rooted`.

---

## 9. 지팡이 개체가 전투에 주는 것 (솔로 오프라인 포함) (S04 9, 11)

- 지팡이 단계 0~13. 겉모습(모양·꼬리·섬광·소리)은 **판정·피해·탄 크기·탄 속도·열을 바꾸지 않는다** (모든 wands 문서 머리말). 게임플레이에서 단계가 바꾸는 것은 피해의 강화 배율뿐(오프라인 기준).
- 강화 배율(무한 웨이브) = `1.15^단계`: 1.0, 1.15, 1.3225, 1.5209, 1.7490, 2.0114, 2.3131, 2.6600, 3.0590, 3.5179, 4.0456, 4.6524, 5.3503, 6.1528. 모든 비기·패시브·고유 비기의 nT에도 적용.
- 연결 슬롯 수 = `base_link_slots`(4, char.3 5) + 확장 슬롯(0~3, 대성공으로). 코어 슬롯 1개 별도.
- 판에는 그 캐릭터 지팡이 한 자루를 들고 나가고 판 중 단계는 안 변한다(유리 지팡이 임시 하락만 예외).
- 피해 공식 자리: `기본 × 강화 배율 × (1+변형 보너스 합) × 변이 배율 × 임계 배율 × 비기 배율 × 감쇠^깊이 × 속성 배율 × 대상 상태 배율 × 캐릭터 배율` (주문 컴파일러 요약서 소관).

### 9.1 변이 6종 (S04 11, S01 7.6) — 이 영역에 닿는 규칙

| id (이름 표) | 이득 | 대가 | 이 영역 규칙 |
|---|---|---|---|
| `mutation.twisted_barrel` 뒤틀린 총열 | 변이 배율 ×1.6 | 탄도 ±25° 흔들림 | 발사마다 [−25°, +25°] 정수도 균등 [임시] |
| `mutation.unstable_core` 불안정 핵 | 폭발 반경 +100% | 발사 1회당 5% 내 자리 폭발 → 잠금 2초 + 콤보 0 | 잠금 = t + 간격 + 120틱, 이미 잠겨 있으면 더 긴 쪽 [임시]. 플레이어 피해 없음 [임시]. **코어가 폭발 블록일 때만** 나오는 변이. 자폭 0.3초 전 예고 `UnstableCoreWarn`(항상 보임) |
| `mutation.hypersensitive_circuit` 과민 회로 | 임계 구간 70부터 | 과열 잠금 +1.5초(+90틱) | 임계 = min(…, 70). 잠금: char.1 4.5초, char.2 4.0, char.3 5.0, char.4·5 4.5 |
| `mutation.gluttony` 탐식 | 처치 시 열 −4(초당 최대 −20) | 냉각 절반 | 처치당 −480 HU, 최근 60틱 합 ≤ 2,400 HU [임시]. 잠금 중 처치는 안 깎음 [임시]. 비기·패시브 처치도 센다 [임시]. 냉각 45→45 HU/틱(22.5/초), char.2 50 HU/틱(25/초). 참고 `HeatMachine.cs:202` |
| `mutation.mirror_image` 거울상 | 뒤로도 동시 발사(J와 마력 방출만, 64개 상한 포함) | 발사당 열 +50% | 열 = 총열(밑줄·분열 뒤) × 3/2. 면제·흡수는 그 뒤 |
| `mutation.glass_wand` 유리 지팡이 | 임계 배율 +1 | 피격 시 강화 −1 | 판 안 임시: 웨이브당 1회, 단계 > 3일 때만, +3까지(역대 최고 −2 바닥 미적용). 판 끝나면 원래 단계. 강화 배율에 반영, 흔적·하락 횟수·칭호에는 미반영. 이벤트 `WandTempStageDropped` |

- 보유 수: 2(char.2 3), 넘치면 가장 오래된 것 밀어냄. 같은 변이 중복 불가 [임시 Q-17]. 변이는 강화 실패로만 생김(강화 요약서 소관).
- 변이 수치는 `data/`에 데이터 파일이 없다(M-06). 지금은 위 명세 값으로 코딩.

---

## 10. 지팡이 문서(docs/wands/) 요약

다섯 문서는 모두 **겉모습 전용**이다. "어느 단계의 겉모습도 피해·판정·열에 영향을 주지 않는다." 전투 특성은 각 하나(80부터 / 변이 3 / 감쇠 0.92 / 얼린 결정 / 외상). 강화 확률·비용·실패 규칙은 다섯 지팡이가 같다 [확정].

| 문서 | 성장 문법 | 전투 상태 연동(표시만) |
|---|---|---|
| `docs/wands/dalgu.md` (wand.1) | 무거워지고 아래로 흘러 굳음. 뜨는 부품·도는 조각 없음 | 4.4: 열·임계·잠금·불가마 밑동 빛 |
| `docs/wands/chorong.md` (wand.2) | 등과 침의 개수가 늘고 위로 뻗음 | 3.1: 깜빡임(초당 2회 상한), 잠금 소등, 스침 점등 |
| `docs/wands/galpi.md` (wand.3) | 옆으로 펼침 | 4.2: 각주 점(스코프 깊이) |
| `docs/wands/seonge.md` (wand.4) | 길어지고 각지고 조각이 돈다 | +9: 열 오르면 공전 조각 작아짐 |
| `docs/wands/geumeum.md` (wand.5) | 손이 열리고 구슬이 뜨고 어두워짐 | 4.4: 빚 고리, 결정 수 밝기 |

- 단계 별명 이름 표: `wand.{1..5}.stage.{0..13}.nickname`. 갈피 설명문 `wand.3.stage.{S}.blurb`(11개 빈 문자열).
- 그림 규격: 손 지팡이 32×64(손잡이 기준점 (16,48)), 큰 그림 192×192, 아이콘 64×64, 3방향(옆은 좌우 반전), 자세 2(들기·내밀기). 시각 요소는 회색 도형으로 대체.

---

## 11. HUD·이벤트 연결점 (S01 17)

상태(매 틱 읽기): 열 링 상태·열 비율·임계 눈금·잠금 남은 비율, 시야 화살표 모드, 무적 깜빡임(초당 8회), 체력 칸(1칸 남음 상태), 스침 가속 잔상, 결정 칸(4~7칸)·빚 칸.

이벤트: `ShotFired`, `CritShotFired`, `HeatEnteredImminent/Critical/Brink`, `HeatLockEntered`, `FireDryWhileLocked`, `HeatUnlocked(Timeout|Graze|VentBlast|Reset)`, `HeatExemptArmed/Consumed`, `KilnAbsorbed`, `BoiloverTriggered`, `UnstableCoreExploded`, `GrazeAwarded`, `GrazeBuffStarted/Ended`, `LightningRodStrike`, `PlayerHit(dmg, hpAfter)`, `InvulnEnded`, `PlayerDowned`, `StanceEntered/Exited`, `ReverseFlipped`, `CommandReset(reason)`, `FacingChanged(dir)`.

- 피격 연출: 그림 정지 0.12초, 하얗게 번쩍, 화면 가장자리 0.3초 — 게임 틱은 멈추지 않는다.
- HUD 글자: `str.battle_hud.day_wave`("{day}일째 웨이브 {wave}"), `str.battle_hud.notice.heat_lock`, `str.battle_hud.now_k`, `str.battle_hud.now_k_debt`.

---

## 12. 하루·웨이브 번호 (S01 5.7) — 참고

```
Day(w)  = (w + 9) / 10          // "3일째 웨이브 23"
Slot(w) = (w − 1) % 10 + 1      // 1~4 낮, 5~7 해질녘, 8~9 밤, 10 보스
```

밤에도 시야·판정·적 속도·예고는 낮과 같다. 참고 `src/Wizard.Sim/World/DayCycle.cs:21`.

---

## 13. 미정·모순 목록

| # | 내용 | 정한 것 |
|---|---|---|
| M-01 | 틱 처리 순서: S01 3.8(11단계, 피격 확정 8단계 안, 냉각 10단계) vs CT 3.2(0~12단계, 피격 확정 10단계, 스침 9단계, 냉각 11단계) | CT 3.2(통합 정본, CT 0.3). 결과(피격 뒤 스침, 틱 끝 냉각)는 같다 |
| M-02 | id 표기: S04 `passive.boil_over`/`shadow_pact`, `art.thunder_dash`/`transcribe`, `trait.*`, `mut.*` vs S14·이름 표·characters.json `passive.overflow`/`shadow_contract`, `art.bolt_dash`/`copy_circle`, `wand_trait.*`, `mutation.*` | S14(CLAUDE 5절 정본). S04의 공용 비기 id(`art.mana_burst` 등)도 S14(`art.burst` 등)로 |
| M-03 | 불안정 핵 자폭 잠금이 끓어넘침을 터뜨리나: S01 7.6.6·U19 "아니다" vs S04 4.1·Q-04 "일으킨다" | 같은 우선순위의 두 명세가 어긋남 → **미정**. S01(과열 주인 명세)의 "아니다"를 임시로 권장 |
| M-04 | 불안정 핵 자폭 잠금 길이: S01 "t + 간격 + 120틱, 더 긴 쪽" vs S04 Q-04 "2초 고정" | 같은 뜻(2초 + 그 발사 간격). S01 식 사용 |
| M-05 | 깊이 감쇠 0.92의 데이터 위치: characters.json에 없음(지팡이 쪽 값이라고만) | 미정. 코드에서 wand_trait로 분기 |
| M-06 | 변이 6종 수치(70, +90틱, 5%, 120틱, −480 HU, ×1.5, ×1.6, ±25°)가 `data/`에 없음 | 미정. 명세 값을 상수로 |
| M-07 | 무한 웨이브 시작 시야 S·위치 (12,7), 몸 반지름 0.30, 대각 정규화, 반대 키 나중 우선, 같은 틱 공격 id 작은 것 | 전부 [임시] 값 사용 |
| M-08 | 사망 문구 "오늘은 여기까지 위대했다."(S01 17.2)의 이름 표 열쇠가 없음 | 미정(이름 표 소유자에게 열쇠 추가 요청) |
| M-09 | 흰뿔 서리밭 −30%를 거는 보스 id(`boss.frost_goat`로 보이나 적 명세 미확인) | 미정. 적 요약서 확인 |
| M-10 | 패시브 피해의 임계 배율·콤보(Q-02, Q-03), 끓어넘침 경계 포함 | [임시]: 임계 없음, 콤보 없음, ≤ 포함 |
| M-11 | 구역 스침 거리: 원문 0.6 고정 vs 감지 반지름(초롱 0.75) | [해석] 감지 반지름 사용 (S01 15 C6) |
| M-12 | 냉각 대기·잠금 시작점: GDD 문장(마지막 발사 시각) vs 검산값(간격 끝 뒤) | [해석] 간격 끝 뒤 (S01 15 C8). 검산값과 일치 |
| M-13 | 일시정지 누적 10분 표시(`str.pause.pause_limit_note`)는 순위 판 전용 | 솔로 오프라인에서 표시할지 미정 |
| M-14 | 결정·콤보·별똥·환전은 이 요약서 범위 밖(전투 보상 요약서). 여기에는 피격·스침과 맞물리는 부분만 | — |
| M-15 | 연속 스침 음높이(`GrazeChain` 0~5, 2초): S01은 플레이어 상태, CT는 표시 계층에서 셈 | 솔로 Unity: 표시 쪽에서 셈 |
| M-16 | 야영 중 틱: S01 "돌리지 않는다" vs CT "진행한다" | CT. 솔로에서 야영 중 기한값(가속, 무적)은 웨이브 끝 초기화로 의미 없음(S01 10.6 [임시]: 웨이브 끝 콤보·열·잠금·면제·불가마 흡수·가속 전부 초기화) |
