# 09. 판 제출과 검증 · 정산 멱등성 · 순위표 · 주간 도전 · 시즌·후계 점수 · 부정 방지 — 구현 명세

게임: "엄청엄청 위대한 마법사 키우기". 이 문서는 구현자가 원본을 다시 읽지 않고 서버(`wizard-api`, 검증 워커)와 클라이언트(`ServerBackend`, `Wizard.Sim`의 기록 부분)를 만들 수 있게 원본의 규칙을 옮겨 적고, 비어 있는 곳을 메운 명세다.

## 0. 읽는 법

### 0.1 우선순위

`docs/DECISIONS.md` > 이 명세가 옮긴 원본(`online.md` 3·6·10절, `monsters.md` 3.9·4.7·4.8·6·7절) > `GDD.md`. 원본끼리 다르면 20절(충돌)에 적은 선택을 따른다. 이 명세와 DECISIONS가 다르면 DECISIONS가 맞다.

### 0.2 출처 약어

| 약어 | 파일 |
|---|---|
| D | `docs/DECISIONS.md` |
| O | `docs/online.md` |
| M | `docs/monsters.md` |
| G | `docs/GDD.md` |
| U | `docs/ui-assets.md` |
| W | `docs/world-characters.md` |

예: `(O 3.2-6)` = online.md 3.2절 6번 항목. `(M 4.8)` = monsters.md 4.8절.

### 0.3 표시

| 표시 | 뜻 |
|---|---|
| [확정] | D의 [개발자] 항목, 또는 원본이 [개발자]/[확정]으로 적은 것 |
| [조율] | D의 [조율] 항목(공통 규격) |
| [제안] | 원본의 설계안(표시가 없는 원본 수치도 [제안]이다. O 머리말) |
| [임시] | 원본이 정하지 않아 이 명세가 넣은 기본값. 구현을 막지 않기 위한 값이고 바꿀 수 있다(21절에 모음) |

### 0.4 범위 밖(다른 명세의 몫)

드랍 표의 숫자(M 6), 점수 항목의 정의(M 4.8)는 **값을 그대로 옮겨 검사식에만 쓴다**. 결투 점수(Glicko-2)·리그·IL의 계산(O 4.7~4.10)은 결투 명세의 몫이고 여기서는 후계 점수의 입력으로만 쓴다. 거래·장터 제한(O 5.5)은 거래 명세의 몫이고 여기서는 "검증 대기/갚을 것" 잠금이 거기에 거는 조건만 정한다. 그림·이펙트·소리는 구현하지 않고 22절에 이벤트 이름만 둔다.

---

## 1. 용어와 식별자

| 한국어 | 코드 식별자 | 뜻 |
|---|---|---|
| 판 | `Run` | 무한 웨이브 1회 |
| 판 번호 | `run_id` (UUIDv7 [임시]) | 서버가 출발 때 준다. **정산의 멱등 키** (O 3.2 정산 규칙) |
| 원정 토큰 | `run_token` | 출발 때 주는 1회용 비밀값. 종료 제출에 한 번만 쓰인다 (O 3.2-4) |
| 씨앗 | `seed` (uint64 [임시]) | 판 씨앗. PCG 계열 생성기의 뿌리 (O 3.1) |
| 중간 지점 | `RunCheckpoint` | 5웨이브마다 올리는 "여기까지는 인정" 기록 (O 3.2-2) |
| 종료 요약 | `RunSummary` | 판이 끝났을 때 올리는 요약 (O 3.2-3) |
| 입력 기록 | `InputRecord` / `replay_key` | 판 전체의 입력 이벤트 열. 파일로 저장하고 열쇠만 DB에 둔다 |
| 1차 검사 | `PrimaryCheck` | 제출 즉시 하는 싼 검사 (O 3.2-4) |
| 2차 검증 | `ReplayVerify` | 검증 워커가 판 전체를 다시 돌리는 검사 (O 3.2-6) |
| 검증 대기 | `pending` | 1차 통과 뒤 2차 전까지의 보상 상태 (O 3.2-5) |
| 갚을 것 | `account_debt` | 검증 실패로 회수하지 못한 몫 (O 3.2-6) |
| 원정 정리 | `TidyRun` | 종료 요약을 못 낸 판을 마지막 중간 지점까지 정산 (O 3.2) |
| 원정 지갑 | `run_wallet` | 판 중 번 골드 (O 1.1) |
| 귀환 가방 | `return_bag` | 판이 끝나고 서고로 가져가는 야생 블록 (M 6.2.4) |
| 기회 목록 | `drop_chances` | 재료·부적·한정 드랍을 뽑을 기회 (O 2.2, M 6.3) |
| 일반 판 | `RunKind.Normal` | 서버 씨앗 판. 시즌 순위와 후계 점수 대상 |
| 주간 도전 | `RunKind.WeeklyChallenge` | 그 주의 공용 씨앗 판 |
| 후계 점수 | `HeirScore` | E + P (O 6.3) |
| 후계자 | `heir` | 시즌 후계 리그 1위 (D 1.8, D 2.8) |
| 수제자 | `disciple` | 후계 리그 2~10위 (O 6.4) |
| 허상과의 첫 만남 | `phantom_first` | 프로필의 첫 웨이브 10 (M 3.9) |
| 복수 | `revenge` | 웨이브 20의 허상을 처음 잡음 (M 3.9.4) |

캐릭터 표시 이름은 이름 표의 id로만 가리킨다(D 1.25). 이 명세의 모든 저장값은 `character` 번호(1~5)를 쓴다 [확정: D 1.25].

---

## 2. 상수 표 (60틱·고정소수점 환산 포함)

결정론 코어: **고정 60틱, Q32.32 고정소수점** [제안] (O 3.1). 1틱 = 16.667ms. 틱 안 오프셋 1단위 = 1/256틱 ≈ 0.0651ms.

| 이름 | 원문 값 | 틱/정수 환산 | 출처 · 표시 |
|---|---|---|---|
| `TICK_RATE` | 고정 60틱 | 60 tick/s | O 3.1 [제안] |
| `INPUT_OFFSET_DIV` | 틱 안 오프셋 8비트(1/256틱) | 0..255 | O 3.1 [제안] |
| `INPUT_KEY_BITS` | 키 코드 4비트 | 0..15 | O 3.1 [제안] |
| `MAX_EVENTS_PER_TICK` | 한 틱에 최대 8개 | 8 | O 3.1 [제안] |
| 입력의 실제 시각 | `(유효 틱 + 오프셋 ÷ 256) × 16.667ms` | Q32.32로 `tick + offset/256` | O 3.1 [제안] |
| 섬 판정 | 0.07초 | 4.2틱 = 1075.2 오프셋 단위(256 단위/틱) | O 3.1 [제안] |
| `CHECKPOINT_EVERY_WAVES` | 5웨이브마다 | 5 | O 3.2-2 [제안] |
| 입력 기록 크기 | 1시간에 150KB쯤 | 216,000틱당 ≈150KB (≈0.71 B/tick) | O 3.2-3 [제안] |
| `RANKED_SUBMIT_WINDOW` | 종료 후 10분 | 600 s = 36,000틱 | O 3.2 [제안] |
| `RANKED_PAUSE_LIMIT` | 일시정지 누적 10분까지 | 600 s = 36,000틱 | O 3.4 [제안] |
| `AUTO_TIDY_AFTER` | 아무 소식 없이 14일 | 1,209,600 s | O 3.2 [제안] |
| `SEASON_LENGTH` | 10주 | 70일 = 6,048,000 s | O 6.2 [제안] |
| `SEASON_SETTLE` | 정산 기간 3일 | 259,200 s | O 6.2 [제안] |
| `SEASON_DEADLINE_BANNER` | 시즌 마지막 7일 "마감 임박" | 604,800 s | O 6.2 [제안] |
| `SEASON_LAST_DAY_PRIORITY` | 마지막 24시간의 상위 100위 → 대기열 맨 앞 | 86,400 s, 100 | O 6.2 [제안] |
| `SEASON_DOWNTIME_EXTEND` | 마지막 24시간 안 2시간 넘게 꺼짐 → 그만큼 늦춤 | 7,200 s | O 6.2, O 7.7 [제안] |
| `HEIR_E_MAX`, `HEIR_P_MAX` | 각 0~500 | 500 | O 6.3 [제안] |
| `E_MIN_RUNS` | 일반 판 3판 이상 | 3 | O 6.3 [제안] |
| `P_MIN_MATCHES` | 자동 매치 30판 이상 | 30 | O 6.3 [제안] |
| `HEIR_MIN_MATCHES` | 자동 매치 50판 이상 | 50 | O 6.3 [제안] |
| `HEIR_MIN_RUNS` | 일반 판 3판 이상 | 3 | O 6.3 [제안] |
| `Q_SMALL_POOL` | 대상자 2명 미만이면 q = 1 | 2 | O 6.3 [제안] |
| `TOP_N_REPLAY_PUBLIC` | 상위 100위 다시 보기 공개 | 100 | O 3.5 [제안] |
| `SEASON_TOP_RUNS` | 시즌 상위 3판 평균 | 3 | O 3.5, O 6.3 [제안] |
| `WEEKLY_BAG_FREE_RUNS` | 그 주의 넷째 판부터 가방 불가 | 1~3번째 판만 가방 | O 2.2 [제안] |
| `SEASON_COIN_TO_GOLD` | 남은 주화 개당 10골드 | 10 | O 6.5 [제안] |
| `DUEL_RESET` | `1500 + 0.5 × (R − 1500)`, 편차 200 | 정수: `1500 + (R − 1500) / 2` | O 6.5 [제안] |
| `LEAGUE_REWARD_COEF` | 새싹 0.4 / 맹수 0.7 / 후계 1.0 | 정수 10분율 4 / 7 / 10 | O 4.8, O 6.4 [제안] |
| `SAME_OPP_DAILY` | 같은 상대 하루 3판 반영 | 3 | O 4.10, O 6.6 [제안] |
| `SAME_OPP_WEEKLY_LOWPOP` | 대기 인원 적을 때 주 5판 | 5 | O 6.6 [제안] |
| `BOT_DAILY_HOURS` | 하루 20시간 이상 도는 계정 표시 | 72,000 s | O 10 [제안] |
| 첫 만남 웨이브 10 점수 | 3,648 | `3200 × 19 × 6 / 100` | M 3.9.3 [제안] |
| 첫 만남 웨이브 10 골드 | 20 | 처치·클리어 골드만 | M 3.9.3 [제안] |
| 첫 만남 싸움 길이 | 최대 45초 | 2,700틱 | M 3.9.2 [제안] |
| 마무리 "되비침" | 3.0초 | 180틱 | M 3.9.2 [제안] |
| "쓰러짐" | 4.0초 | 240틱 | M 3.9.2 [제안] |
| 빠른 처리 목표 | 일반 60초 / 보스 120초 | 3,600 / 7,200틱 | M 4.8 [제안] |
| 지체 방지 | 120초 넘으면 처치 점수 절반 | 7,200틱 | M 4.3 [제안] |
| 최종 격노 | 180초 | 10,800틱 | M 3.2 (M 4.8 인용) |
| 스침 상한 | 일반 20회 / 보스 40회 | 20 / 40 | M 4.8 [제안] |
| 변덕 배율 | 1.15 / 1.2 / 1.25 | 정수 115 / 120 / 125 (÷100) | M 4.5, M 4.8 [제안] |
| 웨이브 계수 | `S(w) = 1 + 0.1 × (w − 1)` | 정수 `S10(w) = 9 + w` (÷10) | M 4.8 [제안] |
| 건너뛴 웨이브 점수 | 기본값의 60% | `× 6 / 10` | M 4.7 [제안] |
| 시작 웨이브 한도 | 건너뛰는 수 ≤ 최고 웨이브 × 0.7, 10의 배수 | `skip × 10 ≤ best × 7` | M 4.7, O 1.2 [제안] |
| 결정 환전 | 개당 5골드 | 5 | M 6.1, O 1.1 [제안] |
| 결정 상한(캐릭터별) | 달구 5, 초롱 5, 갈피 4, 성에 6, 그믐 7 | 5/5/4/6/7 | G 6, W 5.1 (교차 확인) |
| 귀환 가방 칸 | `직접 넘긴 웨이브 수 ÷ 3`의 몫, 최대 12 | `min(12, n / 3)` | M 6.2.4 [제안] |
| 가방 영웅 한도 | `직접 넘긴 웨이브 수 ÷ 10`의 몫 | `n / 10` | M 6.2.4 [제안] |
| 가져가지 못한 야생 블록 | 일반 5 / 희귀 15 / 영웅 50 골드 (빌린 블록 50) | | M 6.1 [제안] |

---

## 3. 데이터 구조

### 3.1 열거형 [제안: 원문 O 7.9의 값 / 영어 이름은 이 명세]

```csharp
enum RunKind      { Normal = 0, WeeklyChallenge = 1 }                      // run.kind 일반/주간 도전
enum RunStatus    { InProgress = 0, FirstPassed = 1, Verified = 2,
                    Invalid = 3, Tidied = 4 }                              // 진행/1차 통과/검증됨/무효/정리됨
enum RunEndKind   { None = 0, HpZero = 1, Abandon = 2, Tidy = 3, Auto14d = 4 } // 체력 0/판 포기/원정 정리/14일 정리
enum ProfileKind  { Offline = 0, Server = 1 }                              // O 8.3
enum VerifyResult { Pending = 0, Match = 1, Mismatch = 2, Error = 3, Unverifiable = 4 }
enum DebtKind     { Gold = 0, Item = 1 }                                   // account_debt.kind 골드/물건
enum DropChanceKind { RavenBundle = 0, FifthWave = 1, Boss = 2, Elite = 3,
                      PhantomFirst = 4, Revenge = 5, SeasonLimited = 6 }   // M 6.0, 6.3, 6.4
enum SeasonStatus { Upcoming = 0, Active = 1, Settling = 2, Closed = 3 }   // [임시] O 7.9 season.status의 값
enum BoardKind    { SeasonOverall, Character1, Character2, Character3, Character4, Character5,
                    Weekly, WeeklyStage7OrLess, WeeklyChallenge, Friends, AllTime, HeirScore }
enum AccountFlag  { VerifyFailed, MacroSuspect, SlowPlay, SeedScouting, Bot, DuplicateInput,
                    MatchFixing, SameDeviceTrade, HabitShift, ... }        // [임시] 표시 사유
```

`RunStatus.Invalid`는 1차 검사 실패(21절 [임시])와 2차 검증 불일치 모두에 쓰고 `invalid_reason`으로 나눈다.

### 3.2 입력 이벤트 (`InputEvent`) [제안] (O 3.1)

무한 웨이브 기록과 결투 패킷이 **같은 구조체**를 쓴다.

| 필드 | 타입 | 범위 | 뜻 |
|---|---|---|---|
| `effective_tick` | uint32 | 0..2³²−1 | 유효 틱 |
| `offset` | uint8 | 0..255 | 틱 안 오프셋(1/256틱) |
| `key` | 4비트 | 0..15 | 키 코드 |
| `down` | 1비트 | 0/1 | 누름(1)/뗌(0) |
| `seq` | uint32 | 플레이어별 단조 증가 | 플레이어별 순번 |

키 코드 [임시]: 0=W(위), 1=A(왼쪽), 2=S(아래), 3=D(오른쪽), 4=J(발사), 5=K(비기), 6=Shift(자세), 7=Space(반전 자세), 8~15 예약(D 1.19의 조작).

**순서 규칙** [제안] (O 3.1):

```
sort key = (effective_tick, offset, class(key), seq)
class(key) = 0 if key ∈ {W,A,S,D} else (1 if key == K else 2)   // 오프셋이 같으면 방향 키가 K보다 먼저
```

- 동률 규칙은 코어 안에 있다. 클라이언트가 한 프레임에 두 키를 같이 읽은 경우를 위한 것이다. 섬("방향이 먼저, 0.07초 안에 K")이 다시 돌려도 같게 나오게 한다.
- 방향·K 이외 키끼리의 동률은 seq 순 [임시].
- 한 틱에 9개째 이벤트는 기록 단계에서 버린다(결투에서는 서버가 버린다) [임시: 무한 웨이브에서는 9번째부터 다음 틱으로 미루지 않고 버림 — 기록과 재실행이 같은 규칙을 쓰면 결정론은 지켜진다].

### 3.3 판 명령 이벤트 (`RunCommand`) [임시]

야영의 조립(끌어다 놓기, G 16-4), 회복, 다시 뽑기, 전리품 카드 선택, 보관함에서 버리기, 비기 해금 선택은 4비트 키 코드로 적을 수 없는데 재실행 결과(원정 지갑, 조립, 가방 후보)를 바꾼다. 원문에 형식이 없으므로 입력 기록 안에 따로 둔다.

| 필드 | 타입 | 뜻 |
|---|---|---|
| `effective_tick` | uint32 | 이 틱의 키 이벤트를 처리하기 **전**에 적용 |
| `seq` | uint32 | 키 이벤트와 같은 순번 공간 |
| `op` | uint8 | `PickCard`, `DiscardBlock`, `Socket`, `Unsocket`, `Heal`, `Reroll`, `UnlockArt`, `Depart` … |
| `args` | 최대 8바이트 | op별 인자(카드 번호, 칸 번호, 블록 인스턴스 번호 등) |

### 3.4 일시정지 이벤트 (`PauseMark`) [임시]

일시정지는 게임 시간을 멈춘다(틱이 나가지 않는다). 시뮬레이션 결과에는 영향이 없고, 3.4절(O)의 "판당 누적 시간 기록"에만 쓴다.

| 필드 | 타입 | 뜻 |
|---|---|---|
| `at_tick` | uint32 | 멈춘 틱 |
| `wall_ms` | uint32 | 멈춰 있던 실제 시간(ms, 클라이언트 단조 시계) |

### 3.5 입력 기록 파일 (`InputRecord`) [임시 형식]

```
header: magic "WZRC", format_version u16, core_version_hash [32]byte, data_version u32,
        run_id [16]byte, seed u64, kind u8, start_wave u16, phantom_first u8,
        character u8, wand_snapshot_hash [32]byte
body:   blocks of (Δtick varint, offset u8, key|down<<4 u8, Δseq varint)  // InputEvent
        + tagged RunCommand / PauseMark records
footer: end_tick u32, final_state_hash [32]byte, record_hash = SHA-256(header‖body)
```

저장: `replay_key` = `runs/{run_id}/{checkpoint_wave|final}.wzrc`. 다시 보기도 이 파일을 쓴다 (O 3.5).

### 3.6 중간 지점 (`RunCheckpoint`) [제안] (O 3.2-2, O 7.9)

| 필드 | 타입 | 뜻 |
|---|---|---|
| `run_id` | uuid | |
| `wave` | int16 | 직접 넘긴 웨이브 번호. 5의 배수 |
| `tick` | uint32 | 그 웨이브를 넘긴 뒤 야영이 열린 틱 [임시] |
| `state_hash` | bytes32 | 코어 상태 해시 |
| `score` | int64 | 그때까지 판 점수 |
| `wallet` | int64 | 원정 지갑 |
| `replay_key` | text | 그 지점까지의 입력 기록 |
| `at` | timestamptz | **서버가 받은 시각** |

- 중간 지점은 **지급이 아니라 "여기까지는 인정"이라는 기록**이다. 재료 추첨은 하지 않는다 [제안] (O 3.2-2, M 6.3).
- 직접 넘긴 웨이브 수가 아니라 **웨이브 번호**가 5의 배수일 때 [임시]. 시작 웨이브 21이면 25, 30 … 에서 기록한다.
- 접속이 없을 때는 기기에 쌓아 두고 다시 접속하면 올린다. 원정 정리가 쓰는 것은 **서버가 받은** 중간 지점뿐이다 [임시].

### 3.7 종료 요약 (`RunSummary`) [제안] (O 3.2-3, M 4.8)

| 필드 | 타입 | 뜻 |
|---|---|---|
| `run_id`, `run_token` | uuid, bytes | |
| `core_version` | bytes32 | |
| `end_kind` | RunEndKind | HpZero 또는 Abandon |
| `end_tick` | uint32 | |
| `score` | int64 | 판 점수 |
| `reached_wave` | int16 | 도달 웨이브(마지막으로 시작한 웨이브) [임시 정의] |
| `cleared_wave` | int16 | 직접 넘긴 마지막 웨이브 |
| `start_wave` | int16 | 1, 11, 21 … |
| `waves[]` | `WaveResult[]` | 웨이브별 내역(M 4.8 "웨이브별 점수 내역을 남긴다") |
| `gold` | `GoldBreakdown` | |
| `drop_chances[]` | `DropChance[]` | 기회 목록 |
| `bag_selection[]` | uint16[] | 가방에 담을 야생 블록 인스턴스 번호 |
| `wild_blocks_final[]` | `(instance, block_code, rarity, borrowed)[]` | 판이 끝날 때 들고 있던 야생 블록 |
| `pause_total_ms` | uint32 | 누적 일시정지 |
| `phantom` | `PhantomResult` | 첫 만남·복수 관련 값 |
| `record_hash` | bytes32 | 입력 기록 파일 해시 |

`WaveResult`: `wave`, `skipped`(bool), `whims[]`(변덕 id 0~2개), `score`, `score_parts{kill, boss, clear, fast, no_hit, graze}`, `kill_clear_gold`, `crystal_gold`, `elites_killed`, `boss_killed`, `cleared`(bool), `ticks`(전투 틱).

`GoldBreakdown`: `kill_clear`, `crystal_exchange`, `block_conversion`, `spent_heal`, `spent_reroll`, `wallet_final`. 등식 `wallet_final = kill_clear + crystal_exchange + block_conversion − spent_heal − spent_reroll` (≥ 0) [임시].

`DropChance`: `kind`, `wave`, `count`(엘리트 수 등).

`PhantomResult`: `first_meeting_done`(bool), `revenge_kill`(bool), `wave20_dent_permille`(웨이브 20에서 진 판의 허상 체력 깎은 비율 ‰, 없으면 −1), `wave20_attempt`(bool).

### 3.8 DB 표 (원문 O 7.9 + 이 명세가 더한 것)

원문 열은 그대로 두고, [임시] 표시 열은 이 명세가 더했다.

**`run`** (O 7.9 [제안])

| 열 | 타입 | 비고 |
|---|---|---|
| id | uuid PK | 정산의 멱등 키 |
| account_id | bigint | |
| character | smallint 1..5 | |
| wand_id | bigint | |
| wand_snapshot | jsonb | 출발 때 서버가 아는 지팡이·조립 |
| seed | bigint | |
| kind | smallint RunKind | |
| start_wave | smallint | |
| phantom_first | bool | 출발 때 서버가 정해 적는다. 재실행 검증은 이 칸으로 돌린다 |
| core_version | bytea | |
| started_at, ended_at, submitted_at | timestamptz | ended_at = 서버 추정 종료 시각 [임시] |
| wave, score, gold | int | wave = 도달 웨이브 |
| drop_chances, loot | jsonb | loot = 서버가 뽑은 결과와 확정분 |
| status | smallint RunStatus | |
| end_kind | smallint RunEndKind | |
| ranked | bool | 13절의 조건 |
| replay_key | text | |
| token_hash | bytea [임시] | `run_token`의 해시. 쓴 뒤 `token_used_at` |
| token_used_at | timestamptz [임시] | |
| summary_hash | bytea [임시] | 같은 제출 판별 |
| season_id | bigint [임시] | |
| week_id | int [임시] | ISO 주 `yyyyww` |
| weekly_run_index | smallint [임시] | 그 주 주간 도전 몇 번째 판(1부터) |
| ranked_eligible_start | bool [임시] | 출발 때 시작 웨이브가 시즌 최고 기준 한도 안이었나 |
| pause_total_ms | int [임시] | |
| invalid_reason | text [임시] | |
| last_contact_at | timestamptz [임시] | 출발·중간 지점·제출 중 마지막. 14일 계산 기준 |
| data_version | int [임시] | M 7 "판마다 데이터 판 번호" |

**`run_checkpoint`** (O 7.9): run_id, wave, tick, state_hash, score, wallet, replay_key, at. PK (run_id, wave).

**`verify_queue`** (O 7.9): id, run_id, priority, status, result, worker, at. + `core_version`, `attempts` [임시].

**`account_debt`** (O 7.9): id, account_id, kind, item_code, qty, run_id, created_at, cleared_at.

**`leaderboard_entry`** (O 7.9): board, season_id, account_id, character, score, wave, run_id, at. + `week_id`, `start_wave`, `wand_stage`, `loadout`(jsonb), `verified`(bool) [임시]. 유일 키 [임시]: 판 하나짜리 순위표는 (board, season_id, week_id, account_id[, character]) 하나만 남기고, 시즌 종합은 S 값을 따로 계산한다(15절).

**`season`** (O 7.9): id, starts_at, ends_at, settle_until, status. + `number`(제n대·제n기의 n) [임시], `extended_seconds` [임시].

**`season_result`** (O 7.9): season_id, account_id, league, s, r, q_e, q_p, e, p, total, rank, rewards_granted. q·e·p·total은 고정소수점 정수(16.4절) [임시].

**`account`의 관련 열** (O 7.9): league, league_season_id, phantom_met, revenge_done_at, phantom_tries, phantom_best_dent, revenge_runs, flags, sanction_until, play_seconds.

이 명세가 더하는 표 [임시]:

| 표 | 열 | 쓰임 |
|---|---|---|
| `run_grant` | run_id, account_id, asset(currency/item_code), qty_granted, qty_pending_left, state(pending/confirmed/reverted) | 판 하나가 준 대기 보상과 남은 양. 회수 계산의 바탕 |
| `wand_pending_lock` | wand_id, run_id, created_at, cleared_at | 대기 보상으로 강화한 지팡이의 거래 잠금(한 지팡이에 여러 판이 걸릴 수 있다) |
| `weekly_challenge` | week_id PK, seed, starts_at, ends_at, settled_at | 그 주의 공용 씨앗 |
| `account_flag` | id, account_id, flag, ref_type, ref_id, detail(jsonb), at, reviewed_by, reviewed_at | 자동 제재가 아닌 "표시" |
| `server_downtime` | started_at, ended_at | 시즌 연장 계산 |
| `heir_tombstone` | season_id, account_id, nickname_at_time, character, wand_stage, total | 후계자 비석(영구) |

---

## 4. 판의 상태 기계

```
            StartRun(서버)                    SubmitFinal → PrimaryCheck 통과
 (없음) ─────────────────▶ InProgress ───────────────────────────────▶ FirstPassed
                              │  │                                         │
                              │  │ PrimaryCheck 실패 [임시]                │ ReplayVerify
                              │  └──────────────────────▶ Invalid ◀────────┤  Mismatch
                              │                                            │
                              │ TidyRun(중간 지점 ≥1) 또는 14일(중간 지점 ≥1)│ Match
                              ├──── PrimaryCheck(지점 데이터) 통과 ─▶ FirstPassed ──▶ Verified
                              │
                              │ TidyRun 또는 14일(중간 지점 0)
                              └────────────────────────────────────────▶ Tidied
```

| 전이 | 조건 | 효과 | 출처 |
|---|---|---|---|
| → InProgress | StartRun 성공 | 지팡이 `lock_state = 원정`, `pending_run_id = run_id`. 씨앗·토큰 발급 | O 3.2-1, O 1.2-3 [제안] |
| InProgress → FirstPassed (정상 종료) | 종료 요약 + 입력 기록 제출, 1차 통과 | 드랍 추첨, 보상 "검증 대기" 지급, 원정 잠금 해제, 2차 대기열 등록 | O 3.2-4·5 [제안] |
| InProgress → FirstPassed (원정 정리/14일) | 서버가 받은 중간 지점 ≥ 1 | 마지막 중간 지점까지 정산, 가방 자동, `ranked = false` | O 3.2 [제안] |
| InProgress → Tidied | 중간 지점 0 | **보상 없이** 잠금만 해제 | O 3.2 [제안] |
| InProgress → Invalid | 1차 실패 | 보상 없음, 잠금 해제, 계정 표시 | [임시] (21절) |
| FirstPassed → Verified | 2차 Match | 대기 표시 해제, 지팡이 대기 잠금 해제, 순위 반영 확정, 복수 추적기 값 기록 | O 3.2-6 [제안] |
| FirstPassed → Invalid | 2차 Mismatch | 보상 회수·갚을 것, 기록 삭제, 계정 표시, 복수 완료 되돌림 | O 3.2-6 [제안] |
| Verified, Invalid, Tidied | 끝 상태 | 같은 run_id로 오는 제출은 처음 결과를 돌려준다 | O 3.2 [제안] |

**잠금은 이 길로만 풀린다. 시간으로 풀지 않는다** (14일 정리도 "정리"라는 정산이다) [제안] (O 3.2).

---

## 5. 출발 (`StartRun`)

### 5.1 요청과 응답

```
POST /v1/runs                         (멱등 키: request_id, O 7.9 `request` 표)
req  { request_id, character, wand_id, kind, start_wave, core_version, data_version }
resp { run_id, run_token, seed, kind, start_wave, phantom_first, wand_snapshot, core_version,
       ranked_eligible_start, week_id?, weekly_run_index? }
```

서버가 내려 주는 것: `run_id`, 씨앗, **서버가 알고 있는** 지팡이·조립 상태, 시작 웨이브, 코어 버전, **이 판의 웨이브 10이 허상과의 첫 만남인지**. 클라이언트가 보낸 능력치는 믿지 않는다 [제안] (O 3.2-1, M 7).

### 5.2 절차 (의사코드)

```
StartRun(acc, req):
  if req.request_id seen: return stored result                       // O 7.9 request
  require profile == Server
  require req.core_version == CURRENT_CORE                           // 다르면 순위 제출과 결투 입장을 막는다 (O 3.1)
       // [임시] 다르면 출발 자체를 거절하고 새 빌드를 안내한다(O 7.10 배포)
  BEGIN TX; lock account row; lock wand row
  wand = wands[req.wand_id]; require wand.owner == acc, wand.character == req.character
  require wand.lock_state == 없음                                    // 원정·결투·장터·우편·거래 중이면 불가
  require wand.loadout.core != empty                                 // 코어가 빈 지팡이로는 출발 불가 (O 1.1)
  if acc.phantom_met == false and exists run(acc, status == InProgress):
       reject "UNSETTLED_RUN_FIRST"                                  // 표시가 켜진 계정의 미정산 판 규칙 (O 3.2)
  if req.kind == WeeklyChallenge:
       require acc.phantom_met == true                               // 첫 만남 뒤에만 (O 3.3)
       require req.start_wave == 1                                   // 웨이브 1 출발 고정 (O 3.3, M 7)
       wk = weekly_challenge[current_week]; seed = wk.seed
       weekly_run_index = count(run where acc, kind=Weekly, week_id=wk) + 1
       phantom_first = false                                         // 주간 도전의 웨이브 10은 언제나 가마등 (O 3.3)
  else:
       seed = CSPRNG.u64()                                           // 판마다 서버가 준다 (O 3.3)
       phantom_first = (acc.phantom_met == false)
  skip = req.start_wave - 1                                          // 0, 10, 20 …
  require skip % 10 == 0
  best_all    = character_state.best_wave_passed(acc, character)     // 그 캐릭터로 넘긴 최고 웨이브
  best_season = season_best_wave_passed(acc, character, season)
  require skip * 10 <= best_all * 7                                  // M 4.7
  ranked_eligible_start = (skip * 10 <= best_season * 7)             // 순위 판은 이번 시즌 최고 기준 (M 4.7, O 1.2)
  insert run(status=InProgress, ...); set token_hash
  wand.lock_state = 원정; wand.pending_run_id = run_id
  COMMIT
```

- 시작 웨이브 고르기는 최고 웨이브 15부터 열린다(최고 14까지는 0만). 그래서 첫 만남(웨이브 10)은 건너뛸 수 없고, 복수 전에는 웨이브 20도 건너뛸 수 없다 [제안] (M 4.7, M 3.9.4).
- 서버 프로필의 판은 서버가 씨앗을 줘야 시작한다. 접속이 안 되면 새 판은 오프라인 프로필로만 한다. 서버 프로필의 지팡이를 접속 없이 들고 나가는 길은 없다 [조율] (O 3.2, O 8.2).
- 판 중에는 지팡이를 거래할 수 없으므로 판을 하는 동안 주인이 바뀌는 일이 없다 [제안] (O 3.2).
- 동시에 진행 중인 판의 수: 원문 제한 없음(허상 표시가 켜진 계정만 1판). 21절 [임시] 참조.

---

## 6. 진행과 중간 지점

- 진행 중에는 서버와 통신하지 않는다. 기기에서 돈다 [제안] (O 3.2-2).
- 히트스톱, 화면 흔들림, 시간대 색 전환은 표시 계층의 일이고 게임 시간은 멈추지 않는다 [제안] (O 3.1, M 7).
- 반복 순서가 정해진 자료구조만 쓴다(배열, 정렬된 목록). 난수는 씨앗 하나에서 갈라 낸 자체 PCG 계열만. 유니티 물리를 쓰지 않는다(원 판정 직접 계산) [제안] (O 3.1).
- 웨이브 구성, 블록 카드, 체력 조각은 판 씨앗에서 갈라 낸 전용 난수기로 정한다. **재료·부적·한정 드랍은 판 씨앗으로 뽑지 않는다** [제안] (M 7, O 2.2).

```
POST /v1/runs/{run_id}/checkpoints
req  { run_token_proof, wave, tick, state_hash, score, wallet, record_part }
     // run_token_proof = HMAC(run_token, run_id‖wave) [임시]. 토큰을 소모하지 않는다
resp { accepted: bool }
```

서버 처리: 판이 InProgress가 아니면 무시. 같은 (run_id, wave)는 덮어쓰지 않고 처음 것만 둔다 [임시]. `at`(서버 수신 시각)으로 가속 검사(7.3-④)를 바로 해 두고 결과를 `run`에 적는다. `last_contact_at` 갱신.

---

## 7. 종료 제출과 1차 검사

### 7.1 요청

```
POST /v1/runs/{run_id}/finish        (본문: RunSummary + 입력 기록 파일)
resp { status, ranked, grants[], pending: true, verify_eta? }
```

입력 기록은 1시간에 150KB쯤이다 [제안] (O 3.2-3). 서버 상한 [임시]: 판당 50MB.

### 7.2 멱등 규칙 [제안] (O 3.2 정산 규칙)

```
Finish(run_id, summary):
  run = load(run_id) FOR UPDATE
  if run.status != InProgress:
       if run.summary_hash == hash(summary): return run.first_result        // 같은 제출 → 처음 결과
       else: return 409 ALREADY_SETTLED + run.first_result                  // 정리 뒤 뒤늦은 요약 등은 받지 않는다
  ...
```

- **정산은 `run_id`당 한 번이다.** 중간 지점과 최종 요약을 합쳐 한 번만 지급한다 [제안] (O 3.2).
- 원정 정리 뒤 뒤늦게 올라온 그 판의 종료 요약은 받지 않는다 [제안] (O 3.2).

### 7.3 1차 검사 (`PrimaryCheck`) [제안: 항목 / 임시: 허용 오차]

원문 4개 항목(O 3.2-4)과, 원문 다른 절이 서버 즉시 검사로 넘긴 식(M 4.8, M 7, M 3.9.3)을 하나로 묶었다.

```
PrimaryCheck(run, s, points):           // points = 정상 종료면 요약, 정리면 마지막 중간 지점
  ① token: verify(s.run_token, run.token_hash) and run.token_used_at is null      // 유효, 한 번만
  ② core:  s.core_version == run.core_version
  ③ record: sha256(file) == s.record_hash, header(run_id, seed, kind, start_wave, phantom_first) == run
  ④ time (가속): W = submitted_at − started_at (서버 시계, O 7.8 "시각은 서버 시각")
                T = s.end_tick / 60 s
       reject if W < T × 0.99 − 5 s                         // [임시] 허용 오차 1% + 5초
       for each checkpoint c: reject if (c.at − started_at) < c.tick/60 × 0.99 − 5 s
  ⑤ start: s.start_wave == run.start_wave
  ⑥ per-wave score:
       for w in 1 .. start_wave−1:   expected skip score (8.3) — 요약의 skipped 항목과 정확히 같아야
       for w in played waves:
           if run.phantom_first and w == 10: require score(w) == 3648        // 고정 (M 3.9.3)
           else: require score(w) <= ScoreCap(w, whims_upper(w))             // 8.2
       require Σ score(w) == s.score
  ⑦ per-wave gold:
       if run.phantom_first and w == 10: require kill_clear_gold(10) == 20   // 결정 환전 제외 (M 3.9.3)
       else: require kill_clear_gold(w) <= GoldCap(w)                        // 8.2
       require crystal_gold(w) <= CRYSTAL_CAP[character] × 5   [임시 도출식]
       require s.gold.block_conversion <= 50 × (2 × cleared_count + 1)       [임시 도출식]
       require gold 등식(3.7) 성립, wallet_final >= 0
       skipped waves: kill_clear_gold == 0 and crystal_gold == 0             // 건너뛴 웨이브는 골드 없음 (O 2.1)
  ⑧ drop chances 정합:
       FifthWave(w): w % 10 == 5 and cleared(w) and not skipped(w)
       Boss(w):      w % 10 == 0 and boss_killed(w) and not (phantom_first and w == 10)
       PhantomFirst: only if phantom_first and cleared(10)
       Elite(w,n):   n <= ELITE_COUNT(w) + 2 × [우두머리 행차 가능]           // M 4.3, M 4.5
       RavenBundle:  only if account never received it and cleared(3)        // 서버 기록으로 판정
  ⑨ bag:
       n = cleared_count (직접 넘긴 웨이브 수. 건너뛴 웨이브 제외)
       require |bag| <= min(12, n / 3); heroes(bag) <= n / 10; no borrowed in bag
       require bag ⊆ wild_blocks_final
       if kind == Weekly and run.weekly_run_index >= 4: require |bag| == 0    // O 2.2
```

- 1차를 통과하면 `token_used_at = now`.
- 웨이브별 상한·고정값은 식으로만 정해지므로 서버가 그대로 다시 계산한다 [제안] (M 4.7, M 3.9.3).
- 건너뛴 웨이브는 골드, 블록, 재료, 주화가 없고 가방 칸에도 세지 않는다 [제안] (O 1.2, M 4.7, M 6.2.4).

### 7.4 검사 실패

원문은 1차 실패 시 처리를 적지 않았다. [임시]: `status = Invalid`, `invalid_reason = "primary:<항목>"`, 보상 없음, 원정 잠금 해제, `account_flag(VerifyFailed)`. ④(가속) 실패는 기록을 남기고 검토 대상으로 둔다.

---

## 8. 정산·지급 (멱등)

### 8.1 지급 절차 (`Settle`) [제안] (O 3.2-5, O 2.2, M 6.3)

```
Settle(run, data, mode):                  // mode = Final | Tidy
  BEGIN TX
  // 1. 드랍 추첨: run_id당 한 번. 판 씨앗이 아니라 서버 난수
  if run.loot is null:
      loot = []
      for ch in data.drop_chances: loot += DrawMaterials(ch)          // M 표 6.3-A~D, 6.4 (서버 CSPRNG)
      loot += FixedGrants(data)                                       // 확정분, 시즌 주화(시즌 태그가 켜졌을 때만)
      if run.phantom_first and cleared(10): loot += [lower_stone ×1]  // 확정분만, 추첨 없음 (M 3.9.3)
      if data.phantom.revenge_kill and account.revenge_done_at is null:
          loot += RevengeReward (bound)                               // 칭호, 귀속 머리 장식, 귀속 중급3·상급1·부적1 (M 3.9.4)
          account.revenge_done_at = now (pending)
      run.loot = loot
  // 2. 지급: 모두 pending
  grant(gold = data.wallet_final)                                     // 원정 지갑 → 프로필 (O 1.2-6)
  grant(bag blocks → 서고); 나머지 야생 블록 → 골드(M 6.1)              // 정리면 등급 높은 순 자동 (M 6.2.4)
  grant(loot)
  write run_grant rows (qty_pending_left = qty_granted)
  // 3. 첫 만남 표시
  if run.phantom_first and cleared(10): account.phantom_met = true     // 정산 때 바로. 재실행을 기다리지 않는다
  // 4. 잠금 해제, 상태
  wand.lock_state = 없음; wand.pending_run_id = null
  run.status = FirstPassed; run.end_kind = …; run.ranked = RankedCheck(run, data)   // 13절
  enqueue verify_queue(run, priority = Priority(run))                 // 9.2
  if run.ranked: upsert leaderboard_entry(verified = false)           // 15.3
  COMMIT
```

- 지급은 **원장**(`currency_ledger`, `item_ledger`)에 `pending = true`, `ref_type = run`, `ref_id = run_id`, `request_id`로 남긴다. 원장은 추가만 한다 [제안] (O 7.9).
- 강화석, 부적, 시즌 주화는 가방 칸을 쓰지 않는다 [제안] (O 1.2-6, M 6.2.4).
- 확정 수량은 야영 화면에 바로 보여 주고, 추첨분은 정산 화면에서 꾸러미를 여는 연출로 보여 준다 [제안] (M 6.3). (22절 이벤트)

### 8.2 웨이브 상한 식 (`ScoreCap`, `GoldCap`) [제안] (M 4.8, M 7) — 정수 계산

```
B(w)   = 10 + 6w               (w ≤ 12)
       = min(200, 82 + 4(w − 12)) (w ≥ 13)                         // M 4.3
S10(w) = 9 + w                                                      // S(w) × 10

ScoreCap(w, whimset):
  if w % 10 == 0:  return (3000 + 200 + 240 + 100 + 200) × S10(w) / 10          // 보스
  kill10 = has(큰 무리|우두머리 행차 가능) ? (B(w) × 125 / 100 + 36) × 10 : B(w) × 10   // 20절 충돌 1
  base   = kill10 + 200 + 60 × 2 + 100 + 20 × 5                                  // 클리어, 빠른 처리 상한, 무피격, 스침
  mult   = 1 (변덕 0개) | 125/100 (1개) | 15625/10000 (2개)                      // 변덕 배율 최댓값
  return floor(base × S10(w) × mult / 10)

GoldCap(w):
  if w % 10 == 0: return 200 × S10(w) / 10 + 20
  return 2 × B(w) × 125 / 100 + 72 × 3 + 20
```

- 변덕은 웨이브 21부터 1개, 41부터 2개. 보스 웨이브에는 없다 [제안] (M 4.5). `whims_upper(w)` [임시] = w < 21 → 0개, 21 ≤ w < 41 → 1개, w ≥ 41 → 2개(실제 변덕은 씨앗으로 정해지지만 1차 검사는 싸게 끝내야 하므로 최대치로 잡는다).
- B(w)가 짝수라 `B × 125 / 100`의 소수는 ×10 뒤 사라진다(정수로 정확).

### 8.3 점수 계산 (재실행·요약 공용) [제안] (M 4.8, M 4.7)

```
WaveScore(w):                              // 직접 넘긴 웨이브
  if phantom_first and w == 10: return 3648
  kill  = Σ cost(e) × 10 × (killed_after_7200_ticks(e) ? 1/2 : 1)      // 졸개·낳은 개체 0
  boss  = boss_killed ? 3000 : 0
  clear = 200
  fastT = (w % 10 == 0) ? 7200 : 3600
  fast60 = max(0, fastT − ticks) × 2                                  // 단위: 1/60점
  nohit = no_hit ? 100 : 0
  graze = 5 × min(grazes, (w % 10 == 0) ? 40 : 20)                    // 최종 격노 이후 스침 제외
  base60 = (kill + boss + clear + nohit + graze) × 60 + fast60
  s = floor(base60 × S10(w) / 600)                                    // S로 한 번만 나눠 내림
  for m in whims: s = floor(s × m100 / 100)                           // [임시] 변덕 2개면 차례로 곱하고 내림
  return s

SkipScore(w) = floor(((w % 10 == 0) ? 3200 : (B(w) × 10 + 200)) × S10(w) × 6 / 100)
RunScore = Σ WaveScore(직접 넘긴 웨이브) + Σ SkipScore(1 .. start_wave − 1)
```

- 계산은 정수로만 한다. 걸린 시간은 틱 단위로 잰다. 빠른 처리 = `max(0, 목표 틱 − 걸린 틱) × 2 ÷ 60` [제안] (M 4.8). 원문은 "웨이브 점수를 낼 때 한 번만 10으로 나눠 내림"이라 했으므로 1/60 단위도 같은 한 번의 나눗셈에 합쳤다 [임시: 내림 위치].
- 야영 시간은 점수에 영향을 주지 않는다. 야영 중에는 점수의 시간 계산이 멈춘다 [제안] (M 4.8, M 4.6). 단 야영도 코어 틱은 나간다(조립·허수아비·출발 표식이 코어 안의 일이다) [임시] → 7.3-④의 T에 야영 틱이 들어간다.
- 점수가 없는 것: 보스가 부른 졸개, 엘리트가 낳은 개체, 120초를 넘긴 뒤 처치의 절반, 최종 격노(180초) 이후의 스침, 허상과의 첫 만남 중의 스침과 입힌 피해 [제안] (M 4.8).

---

## 9. 2차 검증 (재실행 워커)

### 9.1 절차 [제안] (O 3.2-6)

```
VerifyWorker loop:
  job = dequeue verify_queue ORDER BY priority, at  FOR UPDATE SKIP LOCKED
  core = load_core(run.core_version)          // 최근 2개 버전을 같이 둔다 (O 7.10). [임시] 대기 판이 남은 버전은 지우지 않는다
  if core missing: result = Unverifiable → 21절 [임시] 처리
  sim = new Sim(core, seed=run.seed, kind, start_wave, phantom_first=run.phantom_first,   // 계정의 지금 표시가 아니다
                wand=run.wand_snapshot, data_version=run.data_version)
  replay(sim, input_record)                   // 정상 종료: 전체. 정리: 마지막 중간 지점까지의 기록
  compare: score, 웨이브별 점수, gold(GoldBreakdown 전부), drop_chances, 가방 내용(가능 여부와 영웅 한도),
           checkpoint state_hash들, final_state_hash, phantom 값
  if all equal: OnVerified(run) else: OnMismatch(run, diff)
  near-duplicate 검사(17.2) 결과를 account_flag로
```

- **모든 판**을 처음부터 다시 돌린다 [제안] (O 3.2-6, O 0, O 7.8).
- 같은 기록을 두 번 돌리면 해시가 같아야 한다(자동 테스트, O 11-1).

### 9.2 대기열 우선순위 [제안] (O 3.2-7, O 6.2)

| priority | 대상 |
|---|---|
| 0 | 시즌 마지막 24시간 안에 들어온 판 중 시즌 순위표 상위 100위 안에 들 점수 (O 6.2) |
| 1 | 순위표 상위에 들어갈 점수 (O 3.2-7) — [임시] 어느 시즌·주간·주간 도전 순위표든 100위 안 |
| 2 | 개인 최고 갱신 |
| 3 | 표시된 계정(`account_flag` 미검토가 있는 계정) |
| 4 | 나머지 |

한가할 때는 몇 분 안에 끝난다. 밀려도 플레이는 막히지 않고 거래만 기다린다 [제안] (O 3.2-7).

### 9.3 통과 (`OnVerified`)

```
OnVerified(run):
  TX: run.status = Verified
      for g in run_grant(run): move qty_pending_left → qty(확정); state = confirmed     // 대기 표시 해제
      ledger: pending=false 확정 줄 추가
      wand_pending_lock where run_id = run → cleared (그 지팡이에 다른 미해결 판이 없으면 거래 잠금 해제)
      leaderboard_entry(run).verified = true
      복수 추적기: phantom_tries += wave20_attempt, phantom_best_dent = max(…), revenge_runs 갱신  // 검증 통과 판만 (M 3.9.5)
      revenge 보상 확정
```

### 9.4 불일치 (`OnMismatch`) — 회수와 갚을 것 [제안] (O 3.2-6)

```
OnMismatch(run, diff):
  TX lock account:
    run.status = Invalid; run.invalid_reason = "replay:" + diff
    delete leaderboard_entry where run_id = run                       // 기록을 지운다
    account_flag(VerifyFailed, ref=run)                               // 계정에 표시
    for g in run_grant(run):                                          // 골드, 강화석, 부적, 블록, 꾸미기
        take = min(g.qty_pending_left, pending_balance(asset))        // 1) 아직 남은 대기분 회수
        rest = g.qty_granted − take
        take2 = min(rest, free_balance(asset))                         // 2) 이미 쓴 만큼 잔액에서 뺀다
        rest -= take2
        if rest > 0: insert account_debt(kind, item_code, qty=rest, run_id)   // 3) 모자라면 갚을 것
        g.state = reverted; ledger에 음수 줄
    // item_stack.qty ≥ 0 제약은 그대로. 빚은 따로 적는다
    if run gave revenge: revenge_done_at = null; 칭호·귀속 장식 회수     // "복수 완료"를 함께 되돌림 (O 3.2, M 3.9.4)
    // phantom_met은 되돌리지 않는다 (O 3.2, M 3.9.5)
    wand_pending_lock(run)은 cleared 하지 않는다 → 갚을 것이 0이 될 때 해제
```

- 대기 상태라 보상이 남의 손에 넘어가 있지 않다 [제안] (O 3.2-6).
- 대기분 소비 순서 [임시]: 쓸 때 `qty_bound` → 확정 수량 → 대기 수량(오래된 판부터). 대기 수량을 하나라도 쓴 강화·슬롯 새기기는 그 지팡이에 `wand_pending_lock(run_id)`를 단다. 귀속 재료를 먼저 쓰는 것은 원문 규칙이다(O 5.5, O 7.9 `qty_bound`).
- 대기 중인 골드와 물건은 **강화와 조립에는 쓸 수 있지만 거래, 장터, 선물에는 못 쓴다**. 대기 보상으로 강화한 지팡이는 검증이 **통과로** 끝날 때까지 거래가 잠긴다 [제안] (O 3.2-5).

### 9.5 갚을 것 (`account_debt`) [제안] (O 3.2-6, O 7.9)

| 규칙 | 값 |
|---|---|
| 상환 | 다음 획득분에서 먼저 뺀다. 골드는 골드 수입에서, 물건은 같은 `item_code` 획득에서 [임시: 같은 코드끼리] |
| 남아 있는 동안 막히는 것 | 직접 거래, 장터 등록·구매, 선물, 지팡이 떠나보내기 |
| 남아 있어도 되는 것 | 원정, 강화, 결투 |
| 무효 보상으로 강화한 지팡이 | 거래 잠금이 검증이 끝나도 풀리지 않고 **갚을 것이 0이 될 때** 풀린다 |

```
OnAcquire(acc, asset, qty):           // 모든 지급 경로가 거친다
  for d in account_debt(acc, asset, cleared_at is null) order by created_at:
      pay = min(qty, d.qty); d.qty -= pay; qty -= pay
      if d.qty == 0: d.cleared_at = now
  grant remaining qty
  if no open debt(acc): clear wand_pending_lock for invalid runs of acc
```

---

## 10. 판이 끝나는 세 길 [제안] (O 3.2, O 1.2-7, M 6.3)

| 길 | 조건 | 정산 범위 | 가방 | 순위 |
|---|---|---|---|---|
| 1. 정상 종료 | 체력 0, 또는 일시정지 메뉴 "판 포기" | 그때까지 직접 넘긴 웨이브 전부 | 플레이어가 고름 | 13절 조건이면 오름 |
| 2. 비정상 종료 → 원정 정리 | 강제 종료·기기 고장으로 종료 요약을 못 냄. 원정 화면 [원정 정리](다른 기기에서도 가능) | 마지막으로 올라온 중간 지점까지, 그 지점의 입력 기록으로 | 등급 높은 것부터 자동(M 6.2.4) | 올리지 않음 |
| 3. 14일 정리 | 아무 소식 없이 14일 | 2와 같다 | 자동 | 올리지 않음 |

```
TidyRun(acc, run_id, confirmed: bool):            // 사용자 요청. 3번은 스케줄러가 confirmed=true로 부른다
  require confirmed                                // "그 기기에 판이 남아 있으면 그쪽에서 제출하는 편이 더 많이 받는다" 확인 1회
  run = load FOR UPDATE; if run.status != InProgress: return first_result
  cp = last run_checkpoint(run)                    // 서버가 받은 것
  if cp is null:
       release lock; run.status = Tidied; run.end_kind = Tidy|Auto14d; return   // 보상 없이 잠금만
  data = DataFromCheckpoint(cp)                    // 기회 목록도 마지막 중간 지점까지
  PrimaryCheck(run, data) → Settle(run, data, Tidy) // 5·6번 검증 흐름을 그대로 탄다
  run.ranked = false

Scheduler(daily): for run in InProgress where now − last_contact_at ≥ 14d: TidyRun(…, confirmed=true)  // [임시] 기준 시각 = 마지막 연락
```

- 보상 없이 잠금만 푸는 "원정 포기"는 따로 두지 않는다 [제안] (O 3.2, M 6.3).
- 원정 정리는 되돌릴 수 없다 [제안] (O 3.2).
- 접속이 끊긴 채 끝난 판(정상 종료)은 기기에 보관했다가 다시 접속하면 제출한다. 보상은 받는다 [제안] (O 3.2, O 7.7).
- 오프라인 프로필의 판에는 이 절차가 없다. 기기가 정산한다 [조율] (O 8.1).

---

## 11. 허상과의 첫 만남·복수의 정산 [확정: D 1.21 / 제안: 세부] (O 3.2, M 3.9)

| 항목 | 규칙 |
|---|---|
| 판 정보 | 출발 때 `run.phantom_first`를 서버가 정해 적는다. 클라이언트는 정할 수 없다. 재실행 검증은 **이 칸으로** 돌린다 |
| 끝난 판인가 | 첫 만남이 든 판은 웨이브 10에서 체력이 0이 되어도 끝난 판이 아니다. 코어가 정해진 틱(되비침 180틱, 쓰러짐 240틱) 뒤 체력을 채우고 웨이브 11로 넘어간다 |
| 고정값 | 웨이브 10 점수 3,648, 골드 20(처치·클리어. 결정 환전은 따로) |
| 표시를 끄는 때 | 웨이브 10을 넘긴 것으로 **정산된** 때(정상 종료, 또는 웨이브 10의 중간 지점까지 정산한 원정 정리). 1차 통과 시점이고 재실행을 기다리지 않는다 |
| 검증 실패 | 표시를 다시 켜지 않는다(보상만 회수) |
| 새 판 금지 | 표시가 켜진 계정은 정산되지 않은 판(진행 중이거나 종료 요약을 못 낸 판)이 하나라도 있으면 새 판을 시작할 수 없다. 원정 화면이 그 판의 제출이나 [원정 정리]를 먼저 요구한다 |
| 왜 | 원정 잠금은 지팡이 한 자루에만 걸리고 지팡이는 개수 제한이 없으므로(D 1.22), 막지 않으면 판을 여러 개 열어 판마다 첫 만남(웨이브 10 확정 통과, 체력 전부 회복, 하급 강화석 1)을 받는다 |
| 복수 보상 | 웨이브 20의 허상을 **처음** 잡은 판의 정산에서 한 번: 칭호 "거울을 깬", 머리 장식 "금 간 거울 조각"(귀속), 귀속 재료 중급 3·상급 1·하락 방지 부적 1. "검증 대기"로 주고, 실패하면 보상과 "복수 완료"를 함께 되돌린다 |
| 보스를 잡은 뒤 죽어도 | 받는다. 웨이브 20 보스 처치 보상(골드 580, 카드 5장, 표 6.3-C 2일째 줄)은 따로 |
| 추적기 값 | 허상 도전 횟수, 가장 큰 흠집, 복수에 걸린 판 수는 **재실행 검증을 통과한 판의 값만** 계정에 적는다. 첫 만남 싸움은 세지 않는다 |
| 주간 도전 | 첫 만남을 넣지 않는다(웨이브 10은 언제나 가마등) |
| 첫 만남 중 비정상 종료 | 마지막 중간 지점이 웨이브 5면 표시는 그대로. 다음에 웨이브 10에 닿는 판에서 처음부터 다시 |

---

## 12. (예약)

다른 명세와 번호를 맞추기 위해 비워 둔다.

---

## 13. 순위 반영 조건 (`RankedCheck`) [제안: 각 조건의 출처 / 임시: 조합]

```
RankedCheck(run, s):
  run.profile == Server                                         // 오프라인 진행은 랭킹에 반영되지 않는다 [조율] (D 2.6)
  and run.end_kind ∈ {HpZero, Abandon}                          // 원정 정리·14일 정리는 올리지 않는다 (O 3.2)
  and run.core_version 가 제출 시점 순위 허용 버전                    // 코어 버전 해시가 다르면 순위 제출을 막는다 (O 3.1)
  and s.pause_total_ms ≤ 600,000                                // 누적 10분까지만 인정 (O 3.4)
  and SubmitDelay(run, s) ≤ 600 s                               // 종료 후 10분 안 (O 3.2)
  and (run.kind == Weekly or run.ranked_eligible_start)         // 순위 판은 이번 시즌 최고 웨이브 기준 시작 (M 4.7)
  and season(run).status == Active and est_end(run) < season.ends_at   // 정산 기간엔 점수 반영 안 함 (O 6.2)

est_end(run)    = started_at + end_tick/60 s + pause_total                [임시]
SubmitDelay     = submitted_at − est_end(run)                              [임시]
```

- 늦게 낸 판은 느리게 돌리기를 가려낼 수 없어서 순위에서 뺀다 [제안] (O 3.2).
- `pause_total`은 클라이언트가 적는 값이라 지연과 일시정지를 서로 옮길 수 있지만 합(W − T)이 최대 1,200초로 묶인다. 중간 지점 수신 시각으로 구간마다 더 좁힐 수 있다 [임시].
- 순위 반영은 1차 통과 때 `verified = false`로 먼저 올리고(화면에 "검증 대기" 딱지), 2차 통과 때 확정, 실패 때 지운다 [임시] (U I-F13 딱지가 이 상태를 전제한다).

---

## 14. 씨앗과 주간 도전

### 14.1 씨앗 표 [제안] (O 3.3, M 7)

| 판의 종류 | 씨앗 | 순위 반영 |
|---|---|---|
| 일반 판(서버 프로필) | 판마다 서버가 준다 | 시즌 순위표, 후계 점수 |
| 주간 도전(서버 프로필) | 그 주의 공용 씨앗. 모두가 같은 웨이브 구성 | "주간 도전" 순위표에만. **후계 점수에는 넣지 않는다.** 그 주가 끝날 때까지 다시 보기 비공개 |
| 오프라인 프로필의 판 | 기기에서 만든다 | 기기 안의 개인 기록 |
| 주간 도전(오프라인 프로필) | 게임에 든 씨앗 목록에서 주마다 하나(기기 시계 기준) | 기기 안의 개인 기록. 주간 보상 없음 |

- 공용 씨앗과 공개 다시 보기를 같이 두면 1위의 입력을 그대로 재생해 같은 점수를 낼 수 있다. 그래서 후계 점수에 들어가는 판은 판마다 다른 씨앗을 쓴다 [제안] (O 3.3, M 7).
- 씨앗은 서버가 주고 시작 전에는 알 수 없다 [제안] (O 3.4).
- 생성기 [임시]: PCG32(XSH-RR, 64비트 상태). 판 씨앗에서 `stream_id`로 갈라 낸다: 0=웨이브 구성·틀·변덕, 1=전리품 카드, 2=체력 조각, 3=적 AI, 4..=예약. 서버의 재료 추첨은 이 생성기를 쓰지 않고 OS CSPRNG를 쓴다.
- 오프라인 주간 씨앗 [임시]: `index = (ISO 연도 × 53 + ISO 주) mod 목록 길이`.

### 14.2 주간 도전 규칙 [제안] (O 3.3, O 2.2, M 7)

| 규칙 | 값 |
|---|---|
| 출발 | 웨이브 1 고정 |
| 열리는 조건 | 허상과의 첫 만남을 치른 프로필만. 그 전에는 메뉴에 "첫 밤을 넘기면 열린다" |
| 웨이브 10 | 언제나 가마등 |
| 판의 골드·블록·재료 | 일반 판과 똑같이 M 6절로 정산. 재료 추첨은 정산 때 하므로 공용 씨앗이어도 고를 수 없다 |
| 넷째 판부터 | 그 주의 넷째 판부터 주간 도전 판의 야생 블록은 귀환 가방에 담지 못하고 전부 M 6.1의 골드가 된다(M 6.2.4의 유일한 예외) |
| 주간 보상(우편, 주가 끝날 때, 주간 도전 순위표 기준) | 상위 10%: 시즌 주화 60 + 그 주의 칭호 / 상위 50%: 시즌 주화 30 / 참가(1판 이상): 시즌 주화 10 |
| 다시 보기 | 그 주가 끝날 때까지 다른 사람의 것은 볼 수 없다 |
| 초기화 | 매주 월요일 |

[임시] 세부:

- 주 경계: 월요일 00:00 KST(UTC+9). `week_id` = ISO 주. 판은 **출발한 주**의 씨앗과 순위표에 속한다.
- "넷째 판" = 그 계정이 그 주에 출발한 주간 도전 판의 순번 ≥ 4(정리·무효 판 포함). 출발 때 `weekly_run_index`로 정해 두고 1차 검사에서 확인한다.
- 보상 구간은 겹치지 않는다(가장 높은 한 구간만). "상위 x%" = 순위 ≤ `ceil(N × x / 100)`, N = 그 주 주간 도전 순위표에 오른 계정 수. 참가 = 그 주에 주간 도전 판을 1판 이상 정산한 계정(순위 반영 여부 무관).
- 주간 보상 지급은 `weekly_challenge.settled_at`으로 멱등. 우편 한 줄(U 4.24 "주간 도전 · 상위 n%").

```
WeeklySettle(week):                                  // 주가 끝난 뒤, 그 주 판의 2차 검증이 끝나면 [임시: 최대 24시간 대기]
  if week.settled_at: return
  board = verified entries of WeeklyChallenge(week) order by score desc, at asc
  N = |board|
  for (rank, acc) in board:
      tier = rank ≤ ceil(N×10/100) ? Top10 : rank ≤ ceil(N×50/100) ? Top50 : Participant
      mail(acc, coins = {Top10:60, Top50:30, Participant:10}[tier], title = tier==Top10 ? week_title : none)
  for acc in participants(week) − board: mail(acc, coins=10)
  week.settled_at = now; 그 주 다시 보기 공개 전환
```

---

## 15. 순위표

### 15.1 종류 [제안] (O 3.5)

| 순위표 | `BoardKind` | 범위 | 초기화 |
|---|---|---|---|
| 시즌 종합 | SeasonOverall | 계정의 시즌 상위 3판 평균 점수(S) | 시즌마다 |
| 캐릭터별 | Character1~5 | 캐릭터 5명 각각의 시즌 최고 점수 | 시즌마다 |
| 주간 | Weekly | 그 주의 최고 점수 | 매주 월요일 |
| 주간 "+7 이하" | WeeklyStage7OrLess | 같은 것, 지팡이 단계 +7 이하 판만 | 매주 월요일 |
| 주간 도전 | WeeklyChallenge | 공용 씨앗 판의 최고 점수 | 매주 월요일 |
| 친구 | Friends | 친구 목록 안 | 시즌마다 |
| 역대 | AllTime | 전 시즌 최고 | 초기화 없음 |
| 후계 점수 | HeirScore (리그 셋) | E + P (16절) | 시즌마다 (U 4.25 큰 탭) |

- 순위표 줄에는 점수, 도달 웨이브, 캐릭터, 지팡이 단계, 조립이 같이 보인다. 상위 100위는 다시 보기(입력 기록 재생)를 누구나 볼 수 있다 [제안] (O 3.5). 건너뛰고 출발한 판은 "웨이브 21에서 출발"을 같이 적는다 [제안] (M 4.7).
- 영구 강화가 점수에 영향을 주므로 순위는 실력과 육성이 섞인 값이다(의도) [제안] (O 3.5).
- 자체 표(`leaderboard_entry`)에 둔다. 서버가 하나이므로 "서버 랭킹"은 이 맥미니의 전체 계정이다 [제안] (O 3.5, D 2.6).

[임시] 세부:

- Weekly·WeeklyStage7OrLess는 **일반 판만**(주간 도전 판은 WeeklyChallenge에만). "+7 이하"는 `run.wand_snapshot.stage ≤ 7`.
- Friends: 시즌 종합(S) 값을 친구 + 나로 거른 것.
- AllTime: 계정의 역대 일반 판 한 판 최고 점수.
- 정렬: 점수 내림차순, 같으면 `at`(달성 시각) 오름차순. 같은 점수는 같은 순위 번호(1, 2, 2, 4 …).
- 주간 순위표의 "그 주" = 판의 `est_end`가 속한 주.

### 15.2 시즌 S 계산 [제안] (O 6.3)

```
S(acc, season) = (top1 + top2 + top3) / 3     // 순위 반영(ranked) 일반 판, 무효 제외. 3판 미만이면 없는 판은 0점
                                              // [임시] 정수 내림. 표시는 정수, 비교용으로 합(top1+top2+top3)을 그대로 둔다
```

### 15.3 갱신 [임시]

1차 통과 때 `verified=false`로 upsert → 2차 통과 때 `verified=true` → 실패 때 그 run_id 줄을 지우고 그 계정의 다음 최고 판으로 다시 계산. 시즌 종합은 run 표에서 상위 3판을 다시 뽑는다(캐시 표를 두어도 원천은 run).

---

## 16. 시즌

### 16.1 이야기와 길이 [확정: D 1.8 / 제안: 길이] (O 6.1, O 6.2)

- 시즌이 끝날 때 서버 랭킹 1위가 그 시즌의 **후계자**가 되어 한정판 보상을 받는다. 랭킹은 무한 웨이브 점수와 결투 점수의 합산이다 [확정] (D 1.8).
- 시즌 10주 + 정산 기간 3일(순위 확정, 검증, 보상 지급). **정산 기간에도 게임은 되고 점수만 반영되지 않는다** [제안] (O 6.2).
- 시즌 마지막 7일은 순위표에 "마감 임박"을 띄운다 [제안].
- 마지막 24시간의 상위 100위 기록은 검증 대기열의 맨 앞(9.2 priority 0) [제안].
- 맥미니가 시즌 마지막 24시간 안에 2시간 넘게 내려가 있었으면 시즌 종료를 그만큼 늦춘다 [제안] (O 6.2, O 7.7). [임시] "그만큼" = 그 24시간 안에 꺼져 있던 시간 전부. 연장된 시간에 다시 2시간 넘게 꺼지면 다시 적용.

### 16.2 시즌 상태 기계 [임시]

```
Upcoming ──starts_at──▶ Active ──ends_at(+연장)──▶ Settling ──settle_until && SettleDone──▶ Closed
                                                       │
                                     다음 시즌 starts_at = 이 시즌 ends_at (정산 기간과 겹친다) [임시]
```

| 상태 | 판 점수 반영 | 결투 점수 반영 | 화면 |
|---|---|---|---|
| Active | 됨 | 됨 | 마지막 7일 "마감 임박" |
| Settling | 안 됨 (13절 조건) | 다음 시즌으로 [임시] | 예상 점수 고정, "정산 중" |
| Closed | — | — | 결과·보상 수령 |

### 16.3 후계 점수 [제안] (O 6.3)

```
후계 점수 = E + P            (각 0 ~ 500, 합 0 ~ 1000)

E = 500 × qE²
    qE = 내 리그 안에서 S의 백분위 = (대상자 수 − 내 순위) ÷ (대상자 수 − 1)    (1위 = 1, 꼴찌 = 0)
    S  = 내 시즌 상위 3판 점수의 평균 (일반 판만. 3판 미만이면 없는 판은 0점)
    대상자 = 그 리그에서 이번 시즌 일반 판을 3판 이상 한 계정

P = 500 × qP²
    qP = 내 리그 안에서 R의 백분위 (같은 식)
    R  = 내 결투 점수
    대상자 = 그 리그에서 이번 시즌 자동 매치를 30판 이상 한 계정. 30판 미만이면 P = 0
```

| 백분위 q | 1위 | 상위 1% | 상위 5% | 상위 10% | 상위 25% | 가운데 |
|---|---|---|---|---|---|---|
| 점수 `500 × q²` | 500 | 490 | 451 | 405 | 281 | 125 |

원문의 근거(구현 판단에 쓰는 성질):

- 백분위는 두 모드의 단위가 무엇이든 "그 모드에서 몇 등인가"를 같은 자로 잰다. 무한 웨이브 1등과 결투 1등의 값이 정확히 같다.
- 제곱은 위쪽을 벌리고 아래쪽을 좁힌다. 상위 10% 안에서 95점이 갈린다. 같은 순위가 아니면 점수가 다르다.
- 둘 다 해야 한다: 무한 웨이브 1위 + 결투 가운데 = 625점, 둘 다 상위 15% = 722점. 한쪽만으로는 후계자가 될 수 없다.
- E의 바탕인 무한 웨이브 점수에는 강화 배율(단계당 ×1.15)이 들어가고, 결투는 강화 배율을 눌렀으므로 P는 손에 가깝다.
- **리그 안에서 잰다.** 후계 점수와 그 순위는 리그마다 따로다. 한 계정은 **정산할 때의 리그 하나**에서만 매겨진다. 리그는 시즌 안에서 올라가기만 한다(O 4.8). 후계자는 후계 리그의 1위다.
- 위 리그로 올라가면: S는 그대로 가져가고, R과 판 수 조건은 정산 리그에서 쌓은 것만 센다(O 4.8, 20절 충돌 4).
- 보상 계수는 후계 점수 계산에 들어가지 않는다(O 4.8).
- 사람이 적을 때: 한 리그의 대상자가 2명 미만이면 q = 1. 대상자가 50명이면 1위와 2위의 차이는 한 모드에서 약 20점. 서버가 작아도 식은 그대로 쓴다.

**정수 구현** [임시] (20절 충돌 3: 정수로 내림하면 큰 리그에서 순위가 달라도 점수가 같아진다):

```
// 나노점 단위(1점 = 10^9)로 저장하고 화면에는 내림한 정수를 보인다
Q(N, r)        = N < 2 ? (1, 1) : (N − r, N − 1)                 // 분자, 분모
Part_nano(N,r) = floor(500 × 10^9 × num² / den²)                  // Int128 / BigInteger
E_nano = isTargetE ? Part_nano(N_E, rank_S) : 0                   // [임시] 대상자가 아니면 E = 0
P_nano = isTargetP ? Part_nano(N_P, rank_R) : 0                   // 원문: 30판 미만이면 P = 0
total_nano = E_nano + P_nano
rank_S: S 내림차순, 같은 S는 같은 순위(가장 작은 번호) [임시]
R = duel_rating.rating (정산 리그, 이 시즌) [임시: Glicko-2의 rating 값]
```

**동점 처리** [제안] (O 6.3): (1) E와 P 중 낮은 쪽이 더 높은 사람, (2) S가 높은 사람, (3) 먼저 달성한 사람. [임시] "달성 시각" = S를 만든 상위 3판 중 마지막 판의 `est_end`와 마지막 반영 결투 시각 중 늦은 것.

**후계자 자격** [제안] (O 6.3): 후계 리그 소속, 자동 매치 50판 이상, 일반 판 3판 이상, 상위 3판이 모두 재실행 검증 통과. [임시] 1위가 자격이 없으면 자격을 갖춘 가장 높은 사람이 후계자. 아무도 없으면 그 시즌 후계자 없음(비석에 "공석").

**시즌 중 표시** [제안]: "지금 기준으로 계산한 예상 점수와 순위"를 보여 주고, 확정은 정산 기간에 한다. 후계 점수 내역 화면은 E와 P 두 막대, 리그 안 백분위, 예상 순위, 모자란 대상 조건을 보인다 (U 4.24).

### 16.4 정산 절차 (`SeasonSettle`) [제안: 단계 / 임시: 순서와 멱등]

```
SeasonSettle(season):                               // Settling 상태에서, 멱등(season_result.rewards_granted)
  1. ends_at 이전에 끝난 판들의 2차 검증이 끝날 때까지 기다린다 (priority 0·1 먼저)
  2. for league in {새싹, 맹수, 후계}:
        members = accounts with account.league == league at ends_at
        compute S, R, run_count(ranked 일반 판, 시즌 전체), match_count(정산 리그 자동 매치)
        E, P, total → rank (동점 규칙)
        upsert season_result
  3. 후계자 후보 10명 수동 검토(O 6.6) → 제재로 빠지면 아래 순위가 한 칸씩 올라간다
  4. 정산 기간 3일 안에 확정 → rewards 우편 지급 (rewards_granted = true)
  5. 후계자 비석 새김(heir_tombstone), 칭호 부여, 다음 시즌 느루 잡담 이름 등록
  6. Closed
```

정산 기간 3일은 제재 검토를 위한 시간이다 [제안] (O 6.6).

### 16.5 보상 [제안] (O 6.4)

후계 리그의 후계 점수 순위:

| 순위 | 보상 | 거래 |
|---|---|---|
| 1위 (후계자) | 칭호 "제n대 후계자", 후계자 비석에 닉네임 새김(영구), 후계자 전용 지팡이 겉모습 5종(캐릭터별, 시즌마다 다른 디자인), 다음 시즌 동안 느루의 한 줄 잡담에 이름 등장, 상급 강화석 30, 부적 10, 골드 200,000 | 칭호와 전용 겉모습은 계정에 묶인다 [조율] (D 2.7). 재료는 거래 가능 |
| 2~10위 (수제자) | 칭호 "제n기 수제자", 수제자 망토(시즌별 색), 상급 강화석 15, 부적 5, 골드 80,000 | 망토는 거래 가능. 칭호는 계정 기록 |

리그마다 따로 매기는 순위 보상(재료와 골드에 리그 보상 계수 0.4 / 0.7 / 1.0):

| 리그 안 순위 | 보상 (후계 리그 기준) | 거래 |
|---|---|---|
| 상위 1% | 시즌 지팡이 효과(발사 궤적), 상급 강화석 8, 골드 40,000 | 꾸미기는 거래 가능 |
| 상위 10% | 시즌 테두리, 상급 강화석 3, 중급 10, 골드 15,000 | 거래 가능 |
| 상위 30% | 중급 강화석 5, 골드 5,000 | (원문 빈칸 → D 2.7에 따라 꾸미기 없음, 재료 거래 가능) |
| 참가 (일반 판 10판 또는 자동 매치 10판) | 시즌 기념 휘장, 하급 강화석 10 | (원문 빈칸 → D 2.7: 시즌 한정 꾸미기는 거래 가능) |

- 후계자 전용 겉모습도 색과 장식만 바꾼다. 단계별 실루엣과 단계 표식은 가리지 못한다 [조율] (D 2.7).
- 리그 결투 점수 상위 10%: 리그 휘장 꾸미기와 골드(새싹 5,000 / 맹수 10,000 / 후계 20,000) [제안].
- 시즌 상점: 판(M 6.4)과 결투(O 2.2: 자동 매치 승리 주화 6, 패배 3, 하루 첫 10판)에서 얻는 시즌 주화로 그 시즌 한정 꾸미기를 산다. 시즌이 끝나면 다시 팔지 않는다. 거래할 수 있다 [제안].
- 후계자와 2~10위는 후계 리그에서만 나온다 [제안] (O 14-8).

[임시] 세부:

- 계수 적용: `floor(base × coef10 / 10)`, 단 base ≥ 1이면 최소 1. 예: 상급 3 × 0.4 → 1.
- 구간은 겹치지 않는다(가장 높은 한 구간). 참가 보상은 순위 구간과 별개로 조건을 채우면 받는다. 후계자·수제자 보상은 리그 구간 보상에 더해 받는다.
- "상위 x%" = 리그 순위 ≤ `ceil(N × x / 100)`, N = 그 리그에서 total > 0인 계정 수.
- 수제자 자격: E 대상자이면서 P 대상자.
- "참가"의 판 수: 정산되고 무효가 아닌 일반 판(순위 반영 여부 무관) 10판, 또는 정산 리그 무관 시즌 자동 매치 10판.

### 16.6 초기화되는 것과 남는 것 [제안 / 확정 표시는 원문대로] (O 6.5)

| 초기화 | 남음 |
|---|---|
| 시즌·주간 순위표 | 지팡이, 강화 단계, 변이, 슬롯 [확정] (D 1.6) |
| 결투 점수: `1500 + 0.5 × (R − 1500)`로 당기고 편차를 200으로 올린다 | 블록, 강화석, 부적, 골드 |
| 시즌 주화(남은 것은 개당 10골드로 환전) | 꾸미기, 칭호, 친구, 차단 목록, 비기 해금 |
| 리그 선택 승급 | 역대 순위표, 전적 |
| 시즌 상점 품목 | 보유 IL. 리그는 새 시즌이 시작할 때의 보유 IL로 다시 정한다(시즌 중에는 올라가기만 했으므로 여기서 내려갈 수 있다) |

```
SeasonRollover(old, new):                         // new.starts_at에 한 번, 멱등
  for acc:
     gold += coins(acc) × 10; coins(acc) = 0      // [임시] 환전 시각 = old.ends_at
     acc.league = LeagueOf(holding_il_14d_max(acc)); acc.league_season_id = new
     clear 리그 선택 승급
     for each league L with duel_rating(acc, L, old):
         new_rating(acc, L, new) = { rating: 1500 + (R − 1500) / 2,      // 정수 나눗셈은 0쪽으로 [임시]
                                     deviation: max(old.dev, 200),        // [임시] "올린다"
                                     volatility: old.volatility, games: 0 }
  remove season shop items of old
```

---

## 17. 부정 방지

### 17.1 입력 기록으로 막지 못하는 것 [제안] (O 3.4)

| 수법 | 탐지 | 처리 | [임시] 기준 |
|---|---|---|---|
| 매크로·입력 보조(완벽한 섬 타이밍) | 입력 간격의 분산이 사람 범위를 벗어나는지 통계. 섬 성공률이 100%에 가깝고 오프셋 분포가 한 점에 몰린 계정 | **자동 제재가 아니라 검토 대상** (`account_flag MacroSuspect`) | 섬 시도 200회 이상에서 성공률 ≥ 98% 이고, 방향→K 간격의 표준편차 < 0.5틱 |
| 느리게 돌리기 | 틱 수에 비해 실제 경과 시간이 길면 표시. 일시정지는 허용하되 판당 누적 시간을 기록, 순위 대상 판은 누적 10분까지만 인정 | `SlowPlay` 표시, 13절 순위 제외 | 중간 지점 사이 구간에서 (수신 간격 − 틱 시간) > 600 s |
| 씨앗 고르기 | 씨앗은 서버가 주고 시작 전엔 모른다. 시작 후 포기한 판도 기록되고, 연속 포기가 많으면 표시 | `SeedScouting` | 24시간 안에 웨이브 5 이전 포기 연속 10판 |

### 17.2 씨앗·기록 복제 [제안] (O 3.3)

- 검증 워커는 다른 계정의 입력 기록과 똑같거나 거의 같은 기록을 찾아 표시한다(`DuplicateInput`).
- [임시] 방법: 키 이벤트를 (틱/6, key, down) 셔플 불가 시퀀스로 줄여 64비트 MinHash 128개를 뽑고, 같은 씨앗(주간 도전) 판끼리 자카드 ≥ 0.9이면 표시. 일반 판은 씨앗이 달라 시퀀스 비교만.

### 17.3 시즌 부정 방지 [제안] (O 6.6)

| 행위 | 대응 |
|---|---|
| 승부 조작(서로 져 주기) | 자동 매치만 반영, 같은 상대 하루 3판 반영, 직전 상대 연속 매치 없음. 두 계정 사이 승패가 한쪽으로 쏠린 쌍, 비정상으로 짧은 경기, 입력이 거의 없는 패배를 탐지해 표시. 모든 결투의 확정 입력이 서버에 있으므로 다시 보기로 검토 |
| 한가한 시간대 대기열 맞추기 | 대기 인원이 적을 때는 매치 범위를 넓히는 대신 "같은 상대 반영 제한"을 주 5판으로 더 조인다 |
| 부계정에 재화 몰아주기 | 직접 거래의 감정가 차이, 선물 조건(친구 7일), 하루 골드 상한(O 5.5). 같은 기기에서 접속한 계정 사이의 거래는 표시 |
| 계정 공유·대리 | 동시 접속 금지(새 접속이 기존 접속을 끊는다), 기기 변경 기록, 입력 습관(오프셋 분포, 키 누름 길이)이 갑자기 바뀐 상위권 계정 표시. 후계자 후보 10명은 정산 기간에 수동 검토. 약관에 금지와 보상 박탈 명시 |
| 기록 조작 | 3절(O)의 전수 재실행 검증 |
| 장비 사서 1위 | 막지 않는다(거래는 확정 사항, D 1.10). 후계 점수의 절반은 결투이고 결투의 강화 배율은 눌려 있다 |

- 제재로 순위에서 빠지면 아래 순위가 한 칸씩 올라간다.
- [임시] 기준: 쏠린 쌍 = 시즌 안 같은 쌍 10판 이상 & 한쪽 승률 ≥ 90%. 짧은 경기 = 라운드 평균 10초 미만. 입력 없는 패배 = 패자 키 누름 < 초당 0.5회. 대기 인원이 적을 때 = 그 리그 대기열 5명 이하가 10분 이상. 입력 습관 변화 = 상위 100위 계정의 최근 20판 오프셋 분포와 이전 100판 분포의 KS 거리 > 0.3.

### 17.4 부정 이용과 안전 전체 표 [제안] (O 10)

이 명세의 영역 밖인 줄도 빠뜨리지 않고 옮긴다. 담당 칸이 그 줄을 구현하는 명세다.

| 주제 | 대응 | 담당 |
|---|---|---|
| 봇(자동 사냥) | 판마다 입력 기록이 서버에 있다. 입력 간격 분포가 기계적인 계정, **하루 20시간 이상** 도는 계정, 같은 입력열이 반복되는 계정을 표시. 깊은 웨이브일수록 수입이 커서 얕은 웨이브 반복의 수익이 낮다. 귀환 가방 칸수와 영웅 블록 제한이 블록 양산의 상한 | 이 명세 (`Bot` 표시. [임시] 하루 = 서버 시각 기준 롤링 24시간, 판 틱 합) |
| 광장 봇·도배 | 채널별 발언 간격, 반복 문장 차단, 새 계정 채팅 제한, 주소·연락처 모양 차단(O 5.3) | 광장 명세 |
| 현금 거래 | 약관으로 금지. 탐지: 감정가와 동떨어진 거래, 한쪽만 주는 거래, 새 계정으로 몰리는 골드, 한 계정에서 여러 계정으로 퍼지는 물건. 억제: 수수료, 하루 골드 상한, 선물 조건, 장터 가격 범위, 지팡이 7일 재거래 잠금. 제재: 양쪽 계정의 해당 물건 회수와 거래 정지 | 거래 명세 |
| 계정 탈취 | Argon2id, 틀린 로그인 대기, 갱신 토큰 재사용 탐지, 새 기기 24시간 거래 제한, 비밀번호 변경 시 전체 토큰 폐기. 메일 계정이 털리면 게임 계정도 넘어갈 수 있다: 재설정 코드는 한 번만 쓰이고 30분 뒤 만료, 재설정 뒤 24시간 거래와 장터 등록 막힘, 비밀번호와 이메일이 바뀌면 알림 메일 | 계정 명세 |
| 부계정 양산 | 가입 제한(IP·기기당 하루 3개), 기능별 플레이 조건, 기기당 자동 매치 계정 3개, 같은 기기 계정 사이 거래 표시. 완전히 막을 수는 없다 | 계정·결투 명세 |
| 메모리 조작·속도 조작 | 서버 프로필의 능력치와 인벤토리는 서버에 있어 고쳐도 반영되지 않는다. 무한 웨이브 결과는 전수 재실행 검증으로 걸린다. 결투는 서버가 시뮬레이션하므로 클라이언트 상태를 고쳐도 자기 화면만 틀어진다 | 이 명세(7.3-④, 9절) + 결투 명세 |
| 결투의 입력 조작 | 봉인 뒤에만 상대 입력 공개, 늦은 입력 폐기, 미래 시각 거절, 순서 검사. 마감에 반복해 걸리거나 이벤트가 자주 버려지는 계정 표시 | 결투 명세 |
| 결투 이탈·고의 지연 | 이탈 패배, 대기열 금지(O 4.10: 하루 3회 넘게 이탈하면 30분). 되감기 한도를 반복해서 넘기게 만드는 쪽은 자기 입력이 버려져 손해 | 결투 명세 |
| 서버 공격 | 열어 둔 포트 최소화, 토큰 없는 UDP 패킷 폐기, IP당 요청 제한, 필요하면 중계 서버 뒤로 | 서버 기반 명세 |
| 채팅 폭력 | 필터, 신고, 자동 임시 차단, 차단 목록, 채널 끄기. 제재 단계: 경고 → 채팅 1일 → 7일 → 30일 → 영구. 결투 중 자유 채팅 없음 | 광장 명세 |
| 닉네임·아이디 | 금칙어 필터, 운영자·NPC 사칭 금지어, 신고 시 강제 변경 | 계정 명세 |
| 미성년자 | 나이를 받지 않으므로 모든 이용자를 미성년자일 수 있다고 보고 기본값: 귓속말 기본 "친구만", 채팅 필터는 끌 수 없다. 현금 가치가 걸린 기능 없음(결제 미연결, D 1.14) | 광장 명세 |
| 개인정보 최소화 | 받는 것: 아이디, 비밀번호 해시, 닉네임, 이메일(암호화, 복구와 보안 알림에만, 계정 삭제 때 삭제), 기기 식별값의 해시, 접속 IP(30일 뒤 삭제). 받지 않는 것: 실명, 생년월일, 전화번호. 채팅 기록 90일, 신고에 붙은 발췌는 처리 뒤 1년 | 계정 명세. **이 명세의 입력 기록 파일에는 개인정보를 넣지 않는다** [임시] |
| 운영자 권한 | 운영자의 지급·회수·제재는 전부 `audit_log`에 남는다. 혼자 운영하더라도 남긴다 | 이 명세(시즌 수동 검토, 회수) + 운영 명세 |

---

## 18. API 요약 [임시]

| 메서드 | 경로 | 멱등 키 | 설명 |
|---|---|---|---|
| POST | `/v1/runs` | request_id | 출발 (5절) |
| POST | `/v1/runs/{run_id}/checkpoints` | (run_id, wave) | 중간 지점 (6절) |
| POST | `/v1/runs/{run_id}/finish` | run_id | 종료 제출 (7절) |
| POST | `/v1/runs/{run_id}/tidy` | run_id | 원정 정리 (10절) |
| GET | `/v1/runs/{run_id}` | — | 상태, 검증 진행 |
| GET | `/v1/runs?status=InProgress` | — | 원정 화면의 미정산 판 목록 |
| GET | `/v1/boards/{board}?season=&week=&league=&page=` | — | 순위표 |
| GET | `/v1/boards/{board}/me` | — | [내 순위로] |
| GET | `/v1/replays/{run_id}` | — | 다시 보기(상위 100위, 주간 도전은 주가 끝난 뒤) |
| GET | `/v1/seasons/current` | — | 상태, 남은 시간, 마감 임박 |
| GET | `/v1/seasons/current/heir-estimate` | — | 예상 E, P, 백분위, 순위, 모자란 조건 |
| GET | `/v1/seasons/{id}/result/me` | — | 지난 시즌 결과 |

`IBackend`(O 8.3)에는 프로필 종류가 있고, 오프라인 프로필에는 순위표·시즌 메서드가 없다(메뉴가 "서버에 접속하면 열립니다"로 묶는다).

---

## 19. 테스트 벡터 (given / when / then)

`S10(w) = 9 + w`, `B(w)`는 8.2. 모든 값은 원문 표·예시·검산값이고, 계산으로 원문 값과 맞춰 본 것은 "검산"이라 적었다.

### 19.1 결정론과 입력

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T001 | 같은 씨앗, 같은 입력 기록 | 두 번 재실행 | 최종 상태 해시가 같다 | O 11-1 |
| T002 | 이벤트 A=(tick 100, off 37, K, down), B=(tick 100, off 37, D(방향), down), seq A<B | 정렬 | B(방향)가 A(K)보다 먼저 | O 3.1 |
| T003 | (tick 100, off 10, K), (tick 100, off 37, W) | 정렬 | K가 먼저(오프셋 우선) | O 3.1 |
| T004 | 방향 (tick 0, off 0), K (tick 4, off 51) | 섬 판정 | 간격 = 4 + 51/256 = 4.199틱 < 4.2틱 → 섬 성공 | O 3.1 (0.07s = 4.2틱) |
| T005 | 방향 (tick 0, off 0), K (tick 4, off 52) | 섬 판정 | 4.203틱 > 4.2틱 → 실패 | O 3.1 |
| T006 | 한 틱에 이벤트 9개 | 기록 | 8개만 남는다 | O 3.1 (최대 8개) [임시 처리] |
| T007 | 입력의 실제 시각 | tick 60, offset 128 | (60 + 0.5) × 16.667ms ≈ 1008.4ms | O 3.1 |
| T008 | 기록에 히트스톱 연출 | 재실행 | 틱 수가 변하지 않는다 | O 3.1 |
| T009 | 코어 버전 해시가 서버와 다름 | 출발 요청 | 거절(순위 제출·결투 입장 차단) | O 3.1 |

### 19.2 점수와 건너뛴 웨이브 (검산)

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T020 | 시작 웨이브 11 (10개 건너뜀) | SkipScore 합 | **8,398** | M 4.7 표 (검산) |
| T021 | 20개 건너뜀 | 합 | **28,859** | M 4.7 |
| T022 | 30개 | 합 | **64,764** | M 4.7 |
| T023 | 40개 | 합 | **120,409** | M 4.7 |
| T024 | 50개 | 합 | **195,829** | M 4.7 |
| T025 | 웨이브 1~10 직접(보너스 제외: 처치 B×10 + 클리어 200, 보스 3200) | 합 | **14,000** | M 4.7 |
| T026 | 1~20 직접 | 합 | **48,104** | M 4.7 |
| T027 | 1~30 | 합 | **107,948** | M 4.7 |
| T028 | 1~40 | 합 | **200,692** | M 4.7 |
| T029 | 1~50 | 합 | **326,392** | M 4.7 |
| T030 | 웨이브 2 건너뜀 | SkipScore(2) = floor(420 × 11 × 6 / 100) | **277** (277.2 내림) | M 4.7, M 4.8 정수 규칙 |
| T031 | 웨이브 10 건너뜀(보스) | SkipScore(10) | **3,648** = 3200 × 19 × 6/100 | M 4.7 |
| T032 | phantom_first, 웨이브 10 | WaveScore | **3,648**, 처치·빠른 처리·무피격·스침 0 | M 3.9.3, M 4.8 |
| T033 | 가마등 처치 웨이브 10(보너스 없음) | 처치 3000×1.9 + 클리어 200×1.9 | **6,080** (> 3,648) | M 3.9.3 |
| T034 | 일반 웨이브, 목표 3,600틱, 걸린 3,000틱, S10=10, 다른 항목 0 | fast | 600 × 2 / 60 = **20점** × S(1) = 20 | M 4.8 |
| T035 | 걸린 틱 > 목표 틱 | fast | 0 | M 4.8 (max 0) |
| T036 | 스침 25회, 일반 웨이브 | graze | 20회만 = 100 × S | M 4.8 |
| T037 | 보스 웨이브 스침 45회 | graze | 40회만 = 200 × S | M 4.8 |
| T038 | 7,200틱 이후 처치(비용 4) | kill | 4 × 10 / 2 = 20 × S | M 4.3, M 4.8 |
| T039 | 변덕 "우두머리 행차"(×1.25), 합 1000점 | 배율 | floor(1000 × 125 / 100) = **1,250** | M 4.5, M 4.8 |
| T040 | 변덕 2개 ×1.15, ×1.2, 합 1000 | 배율 | floor(floor(1000×115/100)×120/100) = **1,380** | M 4.5 [임시 순서] |
| T041 | 졸개 처치 | 점수 | 0 | M 4.8 |

### 19.3 1차 검사 상한 (검산)

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T050 | 웨이브 29, 변덕 없음 | ScoreCap | **7,676** | M 4.8 예시 (검산) |
| T051 | 웨이브 30 보스 | ScoreCap | **14,586** = 3740 × 3.9 | M 4.8 |
| T052 | 웨이브 10 보스(가마등) | ScoreCap | 7,106 | 식 |
| T053 | 웨이브 10, phantom_first | 점수 3,649 제출 | 거절(고정 3,648) | M 3.9.3 |
| T054 | 웨이브 10, phantom_first | kill_clear_gold 20, crystal_gold 15 | 통과(결정 환전 별도) | M 3.9.3 |
| T055 | 웨이브 10, phantom_first | kill_clear_gold 400 | 거절 | M 3.9.3 |
| T056 | 웨이브 29, 변덕 1개 허용 | ScoreCap(최대) | 13,062 [임시 식] | 8.2 |
| T057 | 웨이브 45, 변덕 2개 허용 | ScoreCap(최대) | 28,518 [임시 식] | 8.2 |
| T058 | 보스 웨이브 w | GoldCap | 200 × S(w) + 20. w=10 → **400**, 20 → **600**, 30 → **800**, 50 → **1,200** | M 7, M 6.1 |
| T059 | 일반 웨이브 29 (B=150) | GoldCap | 375 + 216 + 20 = **611** | M 7 식 |
| T060 | 일반 웨이브 42 (B=200) | GoldCap | 736 | M 7 식 |
| T061 | 실제 시각 W = 1,000s, end_tick = 72,000(1,200s) | 가속 검사 | 거절 | O 3.2-4 |
| T062 | W = 1,195s, end_tick = 72,000 | 가속 검사 | 통과(1% + 5s 안) [임시] | O 3.2-4 |
| T063 | 같은 run_token으로 두 번째 finish(다른 내용) | 제출 | 409 + 처음 결과 | O 3.2 |
| T064 | 같은 run_id, 같은 내용 재전송 | 제출 | 처음 결과 그대로, 지급 한 번 | O 3.2 |
| T065 | 시작 웨이브 21, 웨이브 1~20 kill_clear_gold > 0 | 1차 | 거절(건너뛴 웨이브 골드 없음) | O 2.1 |

### 19.4 골드 합계 (경제 검산용, 1차 검사 상한이 아님)

| # | given | then | 출처 |
|---|---|---|---|
| T070 | 웨이브 골드: 1 → 52, 5 → 100, 9 → 148, 10(보스) → 400, 14 → 200, 20 → 600, 30 → 800, 42 이후 일반 → 420, 50 → 1,200 | 식 `2B+20`, `200S+20`과 같다 | M 6.1 |
| T071 | 웨이브 1부터 누적: 5 → 380, 8 → 752, 9 → 900, 10 → 1,300, 14 → 2,048, 20 → 3,768, 30 → 7,160, 40 → 11,472, 50 → 16,448, 60 → 21,628 | 식과 같다 | M 6.1, O 2.1 |
| T072 | 보스 처치 골드 `200 × S(w)`: 10 → 380, 20 → 580, 30 → 780, 50 → 1,180 | | M 6.1 |

### 19.5 시작 웨이브

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T080 | 그 캐릭터 최고 34 | 고를 수 있는 건너뜀 | 0, 10, 20 (23.8 이하) | M 4.7 |
| T081 | 최고 14 | | 0만 | M 4.7 |
| T082 | 최고 15 | | 0, 10 (10.5) | M 4.7, O 1.2 |
| T083 | 최고 29 | | 0, 10, 20 (20.3) | M 3.9.4 |
| T084 | 역대 최고 40, 이번 시즌 최고 15, 건너뜀 20 | 출발 | 허용, `ranked_eligible_start = false` → 순위 미반영 | M 4.7 |
| T085 | 건너뜀 15 | 출발 | 거절(10의 배수 아님) | M 4.7 |

### 19.6 귀환 가방

| # | 직접 넘긴 웨이브 | 칸 | 영웅 한도 | 출처 |
|---|---|---|---|---|
| T090 | 0~2 | 0 | 0 | M 6.2.4 |
| T091 | 3 | 1 | 0 | |
| T092 | 8 | 2 | 0 | |
| T093 | 10 | 3 | 1 | |
| T094 | 14 | 4 | 1 | |
| T095 | 20 | 6 | 2 | |
| T096 | 30 | 10 | 3 | |
| T097 | 36 | 12 | 3 | |
| T098 | 40 | 12 | 4 | |
| T099 | 60 | 12 | 6 | |
| T100 | 시작 웨이브 21, 웨이브 21~29 넘김 | 칸 = 9/3 = 3 (건너뛴 20 미포함) | 영웅 0 | M 6.2.4 |
| T101 | 가방에 빌린 블록 | 1차 | 거절 | M 6.2.3 |
| T102 | 주간 도전 4번째 판, 가방 1개 | 1차 | 거절, 야생 블록 전부 골드 | O 2.2 |
| T103 | 주간 도전 3번째 판 | 가방 | 일반 규칙 | O 2.2 |
| T104 | 원정 정리, 마지막 중간 지점 웨이브 15, 야생 블록 일반 3·희귀 3·영웅 2 | 자동 가방 | 칸 5, 영웅 1: 영웅 1 → 희귀 3 → 일반 1 | M 6.2.4 |

### 19.7 정산·멱등·끝나는 길

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T110 | 정상 종료 판, 1차 통과 | finish | 보상 pending, 원정 잠금 해제, 대기열 등록 | O 3.2-5 |
| T111 | 중간 지점 2개(웨이브 5, 10)를 받은 판 | 원정 정리 | 웨이브 10까지 정산, ranked=false | O 3.2 |
| T112 | 중간 지점 0개 | 원정 정리 | status=Tidied, 보상 없음, 잠금 해제 | O 3.2 |
| T113 | 원정 정리한 판 | 뒤늦은 finish | 받지 않음(처음 결과) | O 3.2 |
| T114 | 마지막 연락 14일 전 | 스케줄러 | 원정 정리와 같은 처리, end_kind=Auto14d | O 3.2 |
| T115 | 마지막 연락 13일 23시간 전 | 스케줄러 | 아무 일 없음 | O 3.2 |
| T116 | 중간 지점 업로드 | 서버 | 지급 없음, 재료 추첨 없음 | O 3.2-2 |
| T117 | 판 하나의 drop_chances | finish 두 번 | 추첨은 run_id당 한 번(loot 같음) | O 3.2-2 |
| T118 | 서버 프로필 판, 접속 끊긴 채 정상 종료, 종료 30분 뒤 제출 | finish | 보상 지급, ranked=false | O 3.2, O 7.7 |
| T119 | 종료 9분 뒤 제출, 일시정지 누적 5분 | finish | ranked=true | O 3.2, O 3.4 |
| T120 | 일시정지 누적 10분 1초 | finish | ranked=false | O 3.4 |
| T121 | 원정 중 지팡이 | 거래 요청 | 거절 | O 3.2 |

### 19.8 검증 실패와 갚을 것

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T130 | 판 X가 골드 10,000 pending 지급, 하나도 안 씀 | 2차 불일치 | 10,000 회수, 빚 0 | O 3.2-6 |
| T131 | pending 10,000 중 7,000을 강화에 씀(확정 잔액 0) | 불일치 | 3,000 회수, `account_debt(Gold, 7,000)` | O 3.2-6 |
| T132 | 확정 잔액 5,000, pending 10,000 중 7,000 씀 [임시 소비 순서상 확정분 먼저였다면 pending은 그대로] | 불일치 | 남은 pending 회수 + 부족분 잔액에서 → 빚 없음 | O 3.2-6 [임시] |
| T133 | 빚 Gold 7,000 | 자동 매치 승리 80골드 | 빚 6,920, 지급 0 | O 3.2-6 |
| T134 | 빚 > 0 | 직접 거래·장터 등록·구매·선물·떠나보내기 | 전부 거절 | O 3.2-6 |
| T135 | 빚 > 0 | 원정 출발·강화·결투 | 허용 | O 3.2-6 |
| T136 | 무효 판 보상으로 강화한 지팡이, 빚 0이 됨 | | 그 지팡이 거래 잠금 해제 | O 3.2-6 |
| T137 | 불일치 판의 순위표 줄 | | 삭제, 계정 표시 | O 3.2-6 |
| T138 | 강화석 pending 2개 모두 소모, item_stack.qty = 0 | 불일치 | qty는 0 유지(음수 금지), `account_debt(Item, 2)` | O 3.2-6, O 7.9 |
| T139 | pending 보상 | 거래·장터·선물 | 거절. 강화·조립은 허용 | O 3.2-5 |
| T140 | pending 보상으로 강화한 지팡이 | 2차 통과 | 거래 잠금 해제 | O 3.2-5 |

### 19.9 허상

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T150 | 새 계정 | 첫 판 출발 | phantom_first = true | O 3.2-1, M 3.9.1 |
| T151 | phantom_first 판 진행 중(정산 전) | 다른 지팡이로 두 번째 출발 | 거절 UNSETTLED_RUN_FIRST | O 3.2 |
| T152 | phantom_first 판, 웨이브 12에서 체력 0, 정산 | 1차 통과 | phantom_met = true (재실행 전) | O 3.2 |
| T153 | T152 판이 2차 불일치 | | phantom_met 그대로 true, 보상만 회수 | O 3.2 |
| T154 | phantom_first 판, 마지막 중간 지점 웨이브 5에서 원정 정리 | | phantom_met 그대로 false | M 3.9.1 |
| T155 | phantom_first 판, 웨이브 10 중간 지점까지 원정 정리 | | phantom_met = true | O 3.2 |
| T156 | phantom_met = false 계정 | 주간 도전 출발 | 거절 "첫 밤을 넘기면 열린다" | O 3.3 |
| T157 | 재실행 시점 계정 phantom_met = true, run.phantom_first = true | 재실행 | phantom_first = true로 돌린다 | O 3.2 |
| T158 | 웨이브 20 허상 첫 처치 후 웨이브 23에서 사망 | 정산 | 복수 보상 pending(칭호, 귀속 장식, 귀속 중급3·상급1·부적1) + 보스 보상 골드 580 | M 3.9.4 |
| T159 | T158 판 2차 불일치 | | 복수 보상과 revenge_done_at 되돌림 | O 3.2 |
| T160 | 웨이브 20에서 진 판(흠집 42%) 1차 통과, 2차 대기 | | phantom_best_dent 아직 갱신 안 함 | O 3.2, M 3.9.5 |
| T161 | phantom_first 판 웨이브 10 | 재료 | 하급 강화석 1 확정만(중급 50%, 부적 2% 추첨 없음) | M 3.9.3 |

### 19.10 후계 점수 (검산)

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T170 | q = 1 | 500 × q² | 500 | O 6.3 표 |
| T171 | 대상자 101명, 2위 (q = 0.99, 상위 1%) | | 490 (490.05) | O 6.3 |
| T172 | 101명, 6위 (q = 0.95) | | 451 (451.25) | O 6.3 |
| T173 | 101명, 11위 (q = 0.9) | | 405 | O 6.3 |
| T174 | 101명, 26위 (q = 0.75) | | 281 (281.25) | O 6.3 |
| T175 | 101명, 51위 (q = 0.5) | | 125 | O 6.3 |
| T176 | 무한 웨이브 1위, 결투 가운데 | E + P | 625 | O 6.3 |
| T177 | 둘 다 상위 15% (q = 0.85) | E + P | 722 (361.25 × 2 = 722.5 → 화면 722) | O 6.3 |
| T178 | 상위 10% 안 1위와 10% 경계 차 | | 95 | O 6.3 |
| T179 | 대상자 50명 1위·2위 | E 차이 | ≈ 20 (500 − 479.80) | O 6.3 |
| T180 | 대상자 1명 | q | 1 → 500 | O 6.3 |
| T181 | 대상자 0명인데 본인 자동 매치 29판 | P | 0 | O 6.3 |
| T182 | 대상자 2명, 2위 | q | 0 → 0점 | O 6.3 식 |
| T183 | 대상자 10,000명, 2위·3위 | E_nano | 서로 다르다(499.8999… vs 499.8000…) | O 6.3 "같은 순위가 아니면 점수가 다르다" [임시 정수 구현] |
| T184 | 일반 판 2판(30,000, 20,000) | S | (30,000 + 20,000 + 0) / 3 = 16,666 (내림) | O 6.3 |
| T185 | total 같음, A: E 300/P 300, B: E 450/P 150 | 동점 처리 | A 승(낮은 쪽 300 > 150) | O 6.3 |
| T186 | total 같음, min 같음, S A > B | | A 승 | O 6.3 |
| T187 | 후계 리그 1위, 자동 매치 49판 | 후계자 자격 | 없음 → 자격자 중 최고가 후계자 [임시] | O 6.3 |
| T188 | 상위 3판 중 1판 검증 대기 | 정산 | 기다림(priority 0·1) → 통과해야 자격 | O 6.3, O 6.2 |
| T189 | 시즌 중 맹수 → 후계 승급, 맹수에서 일반 판 S 50,000, 맹수 자동 매치 40판, 후계 자동 매치 20판 | 정산 | 후계 리그에서 매김, S = 50,000 그대로, P 대상 아님(20 < 30) → P = 0 | O 4.8, O 6.3 |
| T190 | 보상 계수 | 후계 점수 | 계수는 점수에 영향 없음 | O 4.8 |

### 19.11 시즌 보상·초기화

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T200 | 후계 리그 1위(자격 충족), 시즌 3 | 지급 | 칭호 "제3대 후계자", 비석, 전용 겉모습 5종(귀속), 상급 30, 부적 10, 골드 200,000 | O 6.4 |
| T201 | 후계 리그 5위 | | "제n기 수제자", 망토(거래 가능), 상급 15, 부적 5, 골드 80,000 | O 6.4 |
| T202 | 새싹 리그 상위 1% | 지급 | 지팡이 효과, 상급 floor(8×0.4)=3, 골드 16,000 | O 6.4 [임시 내림] |
| T203 | 맹수 리그 상위 10% | | 테두리, 상급 floor(3×0.7)=2, 중급 7, 골드 10,500 | O 6.4 |
| T204 | 새싹 리그 상위 10% | | 상급 floor(1.2)=1, 중급 4, 골드 6,000 | O 6.4 |
| T205 | 일반 판 10판, 자동 매치 0판 | 참가 | 휘장, 하급 10 × 계수 | O 6.4 |
| T206 | 리그 결투 점수 상위 10% (새싹/맹수/후계) | | 휘장 + 5,000 / 10,000 / 20,000 골드 | O 6.4 |
| T207 | 결투 R = 1800 | 새 시즌 | 1650, 편차 200 | O 6.5 |
| T208 | R = 1200 | | 1350 | O 6.5 |
| T209 | R = 2100 | | 1800 | O 6.5 |
| T210 | R = 1500 | | 1500 | O 6.5 |
| T211 | 남은 주화 37 | 시즌 종료 | 골드 +370, 주화 0 | O 6.5 |
| T212 | 시즌 중 IL 하락했지만 후계 리그 유지, 새 시즌 시작 시 보유 IL 120 | 롤오버 | 맹수 리그 | O 6.5, O 4.8 |
| T213 | 시즌 마지막 24시간 안 서버 꺼짐 2시간 30분 | | 시즌 종료 2시간 30분 연장 | O 6.2, O 7.7 |
| T214 | 마지막 24시간 안 꺼짐 1시간 59분 | | 연장 없음 | O 6.2 |
| T215 | 정산 기간 중 끝난 일반 판 | | 보상 지급, 순위·후계 점수 반영 안 함 | O 6.2 |
| T216 | 후계자 후보 3위가 제재로 빠짐 | 정산 | 4위 → 3위 … 한 칸씩 | O 6.6 |
| T217 | 남는 것 | 롤오버 | 지팡이·단계·변이·슬롯·블록·강화석·부적·골드·꾸미기·칭호·친구·차단·비기 해금·역대 순위표·전적 그대로 | O 6.5 |

### 19.12 주간 도전·순위표

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T220 | 주간 도전 순위 100명 | 보상 | 1~10위 주화 60 + 칭호, 11~50위 30, 51~100위 10 | O 2.2 [임시 구간] |
| T221 | 주간 도전 판 1판, 순위 미반영(제출 지연) | 보상 | 참가 10 | O 2.2 |
| T222 | 주간 도전 판 | 후계 점수 | 들어가지 않는다 | O 3.3 |
| T223 | 주간 도전 상위 100위 다시 보기, 주 진행 중 | 다른 계정이 요청 | 거절 | O 3.3 |
| T224 | 같은 요청, 주가 끝난 뒤 | | 허용 | O 3.3, O 3.5 |
| T225 | 일반 판 101위 다시 보기 | | 거절 | O 3.5 |
| T226 | 지팡이 +8로 한 일반 판 | 주간 "+7 이하" | 오르지 않는다 | O 3.5 |
| T227 | 주간 도전 시작 웨이브 11 요청 | 출발 | 거절 | O 3.3 |
| T228 | 오프라인 프로필 | 순위표 메뉴 | "서버에 접속하면 열립니다" 묶음 | O 8.1 |
| T229 | 오프라인 주간 도전 | 판 | 기기 기록만, 주간 보상 없음 | O 3.3 |

### 19.13 부정 방지

| # | given | when | then | 출처 |
|---|---|---|---|---|
| T240 | 같은 상대 4번째 자동 매치(하루) | 점수 | 반영 안 함 | O 6.6, O 4.10 |
| T241 | 대기 인원 적음 상태, 같은 상대 그 주 6번째 | | 반영 안 함 | O 6.6 |
| T242 | 섬 성공률 99.5%, 오프셋 분포 한 점 | | MacroSuspect 표시, 제재 없음 | O 3.4 |
| T243 | 다른 계정과 같은 입력 기록 | 검증 | DuplicateInput 표시 | O 3.3 |
| T244 | 하루 21시간 판 진행 | | Bot 표시 | O 10 |
| T245 | 새 접속 | 기존 접속 | 끊김 | O 6.6 |
| T246 | 운영자 회수 | | audit_log 1줄 | O 10 |

테스트 벡터 수: 162개(T001~T246 중 쓰인 번호).

---

## 20. 충돌 (문서 사이 불일치와 선택)

| # | 내용 | 선택 | 근거 |
|---|---|---|---|
| C1 | M 4.8의 웨이브 점수 상한 식은 `(B × 1.25 + 36) × 10 …× 변덕 배율 최댓값`인데, 같은 절의 예시 "웨이브 29(변덕 없음) 7,676점"은 `(B × 10 + 520) × S`로만 맞는다(1.25·36·배율이 빠진 값) | 변덕별로 나눈다: 변덕이 없을 수 있는 웨이브(1~20)는 `B × 10`, 변덕 1개 이상 가능한 웨이브는 `(B × 1.25 + 36) × 10`과 배율 최댓값(1.25 또는 1.5625). 예시 7,676은 "변덕 없음" 테스트(T050)로 둔다 | 식이 "변덕을 감안한 상한", 예시가 "변덕 없음"이라고 스스로 적었으므로 두 값이 모두 맞도록 조건을 나눔. D는 이 점을 다루지 않는다 |
| C2 | 리그 보상 계수: O 6.4 "재료와 골드에는 … 곱한다" vs O 4.8 "시즌 보상의 재료 양에만 곱한다" vs O 14-8·G 확인 10 "재료 양에 0.4 / 0.7" | O 6.4(재료와 골드)를 따른다. 21절에 개발자 확인 항목으로 올린다 | 보상의 주인 절(6.4)이고 표 머리가 "후계 리그 기준"으로 골드까지 함께 적었다. D는 다루지 않는다 |
| C3 | O 6.3 "500에서 여럿이 같아지는 일이 없다(같은 순위가 아니면 점수가 다르다)" vs 정수 점수로 내림하면 대상자가 수천 명일 때 2위와 3위가 같은 정수가 된다 | 내부값을 나노점(10⁻⁹) 정수로 저장·비교하고 화면만 내림한 정수 | 원문의 의도(순위가 다르면 점수가 다르다)를 지키는 쪽 |
| C4 | O 6.3 E 대상자 "그 리그에서 이번 시즌 일반 판을 3판 이상" vs O 4.8 "S는 리그를 옮겨도 그대로 가져가며, R과 판 수 조건은 정산 리그에서 쌓은 것만 센다" | 일반 판 3판은 **시즌 전체**(S와 함께 따라간다), 자동 매치 판 수(30·50)는 **정산 리그**에서만 | S를 가져가는데 S를 만든 판을 세지 않으면 모순. "판 수 조건"은 R과 나란히 적혔으므로 결투 쪽으로 읽음 |
| C5 | D 1.8 "서버 랭킹 1등이 후계자" vs O 6.3 "후계자는 후계 리그의 1위"(리그마다 따로 매김) | O 6.3. 후계 리그가 가장 위 리그이고 후계자는 그 리그에서만 나온다 | D 1.9·2.7이 리그를 확정했고 D 1.8의 "랭킹"을 리그 단위로 푼 것이 O. D와 모순되지 않는 해석 |
| C6 | O 3.3 "주간 도전은 그 주가 끝날 때까지 다시 보기 비공개" vs O 3.5 "상위 100위는 다시 보기를 누구나" | 주간 도전 순위표의 상위 100위는 **주가 끝난 뒤** 공개 | 둘 다 지키는 유일한 조합 |
| C7 | O 3.2-7 대기열 순서(순위 상위 → 개인 최고 → 표시 계정 → 나머지) vs O 6.2 "마지막 24시간 상위 100위는 대기열 맨 앞" | 6.2를 priority 0으로 맨 앞에 넣고 3.2-7을 1~4로 | 6.2가 더 좁은 조건이라 위에 둔다 |
| C8 | O 3.2 "골드(20)는 고정값" vs M 3.9.3 "결정 환전은 이 20과 따로 더해진다. 서버가 고정값 20으로 확인하는 것은 결정 환전을 뺀 처치·클리어 골드" | 요약의 골드를 처치·클리어 / 결정 환전 / 블록 환전 / 지출로 나눠 받고, 20은 처치·클리어에만 건다 | M이 더 자세하고 O와 모순 없이 합쳐진다 |
| C9 | O 1.2 "최고 웨이브 15를 넘기기 전에는 1에서만 시작" vs M 4.7 "최고 14까지는 건너뛸 수 없다" / M 3.9.1 "최고 웨이브 15부터 열린다" | `skip × 10 ≤ best × 7` 식 하나로 판정(최고 15 → 10 건너뛰기 가능) | 식이 기준(O 1.2가 규칙은 M 4.7이 기준이라고 적었다) |
| C10 | O 6.4 표의 "상위 30%", "참가" 줄의 거래 칸이 비어 있음 | D 2.7 "후계자 칭호와 전용 겉모습만 계정에 묶이고, 그 밖의 시즌 한정 꾸미기는 거래할 수 있다" → 휘장 거래 가능, 재료 거래 가능 | D 우선 |
| C11 | O 3.4 "순위 대상 판은 누적 10분까지만 인정" — 넘으면 판 전체가 빠지는지, 10분만 세는지 불명 | 넘으면 그 판은 순위에서 빠진다(보상은 받는다) | 일시정지 자체는 점수에 영향이 없으므로 "인정"은 순위 대상 여부로만 의미가 있다 |
| C12 | O 2.1 "보스 웨이브 웨이브 10 = 400" vs M 3.9.3 첫 만남 웨이브 10은 20 | 둘 다 맞다: O 2.1은 가마등 기준 경제 표, 첫 만남은 프로필마다 한 번의 예외(M 3.9.3이 경제 차이를 적었다) | 같은 값의 다른 경우 |
| C13 | O 6.5 "편차를 200으로 올린다" — 편차가 이미 200보다 크면? | `max(편차, 200)` | "올린다"를 내리지 않는 뜻으로 읽음 |

---

## 21. 미정 (개발자 확인 필요 + 원문이 정하지 않은 것)

### 21.1 원문의 "개발자 확인 필요" 중 이 영역의 것 (지금 적용한 값)

| # | 항목 | 지금 값 | 출처 |
|---|---|---|---|
| Q1 | 순위 판의 웨이브 구성: 판마다 다른 서버 씨앗(본판) + 후계 점수에 안 들어가는 "주간 도전". 모두 같은 구성을 본판으로 삼을지 | 지금 값 유지 | M 확인 2, G 확인 2 |
| Q2 | 낮은 리그에도 시즌 순위 보상을 줄 것인가 | 리그마다 따로 순위, 재료(와 골드, C2)에 0.4 / 0.7. 후계자와 2~10위는 후계 리그에서만 | O 14-8, G 확인 10 |
| Q3 | 주간 도전 보상과 넷째 판 제한 | 상위 10% 주화 60 + 칭호, 50% 30, 참가 10. 넷째 판부터 가방 불가. 빼려면 O 2.2의 그 문장과 M 6.2.4·7의 예외 줄을 같이 지운다 | G 확인 60 |
| Q4 | 첫 만남 뒤 웨이브 10과 주간 도전 | 첫 만남 뒤 웨이브 10은 가마등, 주간 도전은 첫 만남 뒤에만 | G 확인 71 |
| Q5 | 첫 만남 세부(45초, 50% 바닥, 3,648점, 하급 1, 복수 보상 내용) | 지금 값 | M 확인 1 |
| Q6 | 지팡이를 팔아도 그 시즌에는 리그가 내려가지 않는 규칙 | 시즌 안에서 올라가기만, 다음 시즌 시작에 보유 IL로 다시 | O 14-11 |
| Q7 | 리그 보상 계수를 골드에도 곱하는가 (C2) | 곱한다 [임시] | O 4.8 vs 6.4 |

### 21.2 원문이 정하지 않은 것 (임시 기본값)

| # | 항목 | [임시] 기본값 |
|---|---|---|
| U1 | `run_id` 형식 | UUIDv7 |
| U2 | 씨앗 크기·생성기 | uint64, PCG32(XSH-RR) + stream_id로 갈라 냄 (14.1) |
| U3 | 키 코드 배정 | 0..7 = W A S D J K Shift Space, 8..15 예약 |
| U4 | 야영 조작(카드 선택, 조립, 회복, 다시 뽑기, 비기 선택)의 기록 형식 | `RunCommand` (3.3) |
| U5 | 일시정지 기록 | `PauseMark` (3.4) |
| U6 | 입력 기록 파일 형식 | 3.5 |
| U7 | 한 틱 9번째 이벤트 | 버린다 |
| U8 | 중간 지점 시점 | 웨이브 번호가 5의 배수인 웨이브를 넘긴 뒤 야영이 열린 틱 |
| U9 | 오프라인 상태에서 쌓인 중간 지점 | 다시 접속하면 올림. 원정 정리는 서버가 받은 것만 씀 |
| U10 | 가속 판정 허용 오차 | 1% + 5초 |
| U11 | 1차 검사 실패 처리 | Invalid, 보상 없음, 잠금 해제, 표시 |
| U12 | 결정 환전 상한 | 웨이브당 결정 상한(캐릭터별 4~7) × 5골드 |
| U13 | 블록 환전 상한 | 50 × (2 × 넘긴 웨이브 수 + 1) |
| U14 | 변덕 수 상한(1차 검사) | 웨이브 21~40: 1개, 41~: 2개로 최대치 가정 |
| U15 | 변덕 2개 배율 적용 순서 | 차례로 곱하고 매번 내림 |
| U16 | 빠른 처리의 1/60 단위 내림 위치 | S 나눗셈과 같은 한 번에 합침 |
| U17 | 동시에 진행 중인 판 수(표시가 꺼진 계정) | 원문대로 제한 없음. 계정당 진행 중 판 수를 지표로 남긴다 |
| U18 | 코어 버전 보관 | 최근 2개 + 검증 대기 판이 남은 버전은 지우지 않음. 그래도 없으면 Unverifiable → 대기 표시를 유지하고 운영자 검토 |
| U19 | 14일 기준 시각 | 출발·중간 지점·제출 중 마지막 연락 |
| U20 | 종료 시각 추정과 10분 판정 | `est_end = started_at + end_tick/60 + pause_total`, `submitted_at − est_end ≤ 600s` |
| U21 | 대기 보상 소비 순서 | 귀속 → 확정 → 대기(오래된 판부터) |
| U22 | 빚 상환 | 같은 자산 코드의 다음 획득분에서 먼저 |
| U23 | 순위 정렬 동률 | 같은 점수 같은 순위, 표시 순서는 달성 시각 오름차순 |
| U24 | 주간 순위표에 주간 도전 판 포함 여부 | 포함하지 않음 |
| U25 | 친구 순위표 값 | 시즌 종합(S) |
| U26 | 역대 순위표 값 | 계정의 역대 일반 판 한 판 최고 |
| U27 | 주 경계 | 월요일 00:00 KST. 판은 출발한 주에 속함(주간 도전), 일반 판의 주간 순위표는 끝난 주 |
| U28 | "넷째 판" 세는 법 | 그 주에 출발한 주간 도전 판(정리·무효 포함)의 순번 |
| U29 | 주간·시즌 보상 "상위 x%" | 순위 ≤ ceil(N × x / 100), 구간 겹치지 않음 |
| U30 | 주간 보상 지급 때 | 주가 끝나고 그 주 판의 검증이 끝난 뒤, 최대 24시간 대기 |
| U31 | 정산 기간과 겹친 주의 주간 주화 | 그대로 지급(환전은 시즌 종료 시각에 이미 끝났으므로 다음 시즌으로 넘어감) |
| U32 | E 대상자가 아닌 계정의 E | 0 (P와 같은 규칙) |
| U33 | R의 정의 | Glicko-2 rating 값(정산 리그, 이 시즌) |
| U34 | "먼저 달성한 사람" | S를 만든 마지막 판의 종료와 마지막 반영 결투 중 늦은 시각이 이른 사람 |
| U35 | 1위가 후계자 자격 미달 | 자격자 중 최고 순위. 없으면 공석 |
| U36 | 수제자 자격 | E 대상자 + P 대상자 |
| U37 | 보상 계수 반올림 | 내림, 기본량 ≥ 1이면 최소 1 |
| U38 | 보상 겹침 | 리그 구간은 가장 높은 하나, 참가는 별개, 후계자·수제자는 리그 구간에 더해 |
| U39 | 참가 판 수 | 정산되고 무효가 아닌 일반 판 10, 또는 시즌 자동 매치 10 |
| U40 | 시즌 연장 "그만큼" | 마지막 24시간 안에 꺼져 있던 시간 전부 |
| U41 | 다음 시즌 시작 | 이전 시즌 ends_at과 같은 때(정산 기간과 겹침) |
| U42 | 결투 점수 리셋의 정수 처리 | `1500 + (R − 1500) / 2`, 0 쪽으로 자름 |
| U43 | 매크로·느림·씨앗 고르기·봇·복제·승부 조작·습관 변화의 기준값 | 17절 표의 [임시] 칸 |
| U44 | 도달 웨이브의 정의 | 마지막으로 시작한 웨이브(순위 줄 표시) |
| U45 | 입력 기록 파일 크기 상한 | 판당 50MB |

---

## 22. 시각/소리 연결점 (이벤트 이름만, 구현 안 함)

클라이언트 이벤트 버스에 아래 이름으로 쏜다. 그림·이펙트·소리는 나중에 이 이름에 꽂는다. 이미 있는 그림 id는 괄호에 적었다.

| 이벤트 | 때 | 붙을 것(참고) |
|---|---|---|
| `run_start_issued` | 서버가 run_id·씨앗을 줌 | 원정 출발 연출 |
| `run_start_blocked_unsettled` | 허상 표시 계정의 미정산 판 때문에 막힘 | 안내 창 |
| `run_checkpoint_saved` | 중간 지점 업로드 성공 | HUD 작은 표식 |
| `run_checkpoint_deferred` | 접속 없어 기기에 보관 | HUD 작은 표식 |
| `run_submit_sent` / `run_submit_ack` | 종료 제출 / 응답 | 정산 화면 진입 |
| `run_primary_rejected` | 1차 실패 | 경고 |
| `settle_materials_reveal` | 추첨분 꾸러미 열기 | 꾸러미 연출 (M 6.3) |
| `settle_bag_autofill` | 원정 정리의 자동 가방 | |
| `reward_pending_badge` | 대기 보상 표시 | 기록 딱지 "검증 대기" (U I-F13) |
| `verify_passed` | 2차 통과 | 기록 딱지 "검증 통과" (U I-F13) |
| `verify_failed_reclaimed` | 2차 실패, 회수 | 경고 우편 |
| `debt_created` / `debt_cleared` | 갚을 것 생김 / 0 | 잠금 아이콘 해제 |
| `wand_trade_lock_pending` / `wand_trade_lock_released` | 대기 보상 강화 잠금 | 지팡이 칸 표식 |
| `tidy_confirm_prompt` | 원정 정리 확인 | 확인 창 |
| `tidy_completed` / `tidy_no_checkpoint` | 정리 결과 | |
| `personal_best` | 개인 최고 갱신 | 기록 딱지 "최고 기록 갱신" (U I-F13) |
| `leaderboard_rank_changed` | 순위 변동(오름/내림/그대로) | U I-L9 |
| `leaderboard_top3` | 1~3위 | U I-L8 |
| `replay_opened` | 다시 보기 시작 | |
| `phantom_flag_cleared` | 첫 만남 정산 | 복수 추적기 등장 (M 3.9.5) |
| `revenge_reward_granted` / `revenge_reward_reverted` | 복수 보상 / 되돌림 | 칭호 띠 (effects 15.2) |
| `weekly_challenge_locked` | 첫 만남 전 | "첫 밤을 넘기면 열린다" (U 4.26) |
| `weekly_challenge_unlocked` | 첫 만남 정산 뒤 처음 | |
| `weekly_bag_disabled` | 그 주 넷째 판 출발 | 정산 화면 안내 |
| `weekly_reward_mail` | 주간 보상 우편 | U 4.24 우편 줄 |
| `season_deadline_soon` | 마지막 7일 | 기록 딱지 "마감 임박" (U I-F13) |
| `season_extended` | 다운타임 연장 | 공지 |
| `season_settling_started` | 정산 기간 시작 | |
| `season_result_ready` | 결과 확정 | 시즌 보상 받기 화면 (U 4.24) |
| `heir_crowned` | 후계자 확정 | 비석 새김, 서버 알림 |
| `disciple_awarded` | 수제자 확정 | 망토 |
| `league_tier_reward` | 리그 구간 보상 | |
| `season_rollover` | 새 시즌 시작 | 느루 잡담, 주화 환전 표시 |
| `season_coins_converted` | 주화 → 골드 | |
| `duel_rating_soft_reset` | 결투 점수 당김 | |
| `heir_estimate_updated` | 예상 후계 점수 변화 | E/P 막대 (U S-2406) |
| `account_flagged` | 표시(운영자 화면만) | 관리 화면 |
| `sanction_rank_removed` | 제재로 순위 제외 | 관리 화면 |

---

## 23. 의존 순서 (일정 아님)

O 11·G 16의 순서에서 이 영역의 위치: 결정론 코어(입력 이벤트·순서 규칙·기록·해시) → `Wizard.Rules`와 `OfflineBackend`(정산·드랍·가방) → 서버 기반(계정, 원장, 멱등 키 `request`, `ServerBackend`) → **이 명세 ①: 출발·중간 지점·종료 제출·1차 검사·정산(4~8절, 10~11절)** → **② 검증 워커와 갚을 것(9절)** → **③ 순위표·주간 도전(14~15절)** → 광장 → 거래(9.4의 잠금 조건이 여기 걸린다) → 결투(R, 리그, IL) → **④ 시즌·후계 점수·보상·초기화(16절)** → 운영(17절 표시 검토 화면, 부하 시험에서 검증 대기열 길이).

①은 9절 없이도 돌아간다(모든 판이 FirstPassed에 머무르고 거래만 기다린다). ④의 P는 결투 명세의 `duel_rating`이 생긴 뒤에 붙는다. 그 전에는 P = 0으로 계산해도 식이 깨지지 않는다.
