# 03. 커맨드 비기 판정기 — 구현 명세

영역: 커맨드 비기 판정기(트라이 + 아호-코라식 실패 링크), 공용 비기 10종, 고유 비기 5종, 해금.
게임: "엄청엄청 위대한 마법사 키우기". 대상: 결정론 코어(`Wizard.Sim`, 순수 C#) 구현자.

이 문서만 읽고 판정기와 비기 데이터, 해금을 만들 수 있게 쓴다. 원본 문서를 다시 열 필요가 없도록 원본의 규칙과 수치를 전부 옮겼다.

## 0. 읽는 법

### 0.1 우선순위

1. `docs/DECISIONS.md`가 모든 문서보다 우선한다.
2. 그다음 각 내용의 "주인" 문서(GDD 0절 문서 지도): 판정기·공용 비기·해금 규칙은 `GDD.md` 8절, 고유 비기의 세부·꼬리표 반응·결투 캐릭터 수치는 `world-characters.md` 3.5·3.6·4·5.4절, 결투 공통 규칙은 `online.md` 4절, 연출·소리는 `effects.md` 12·19·20절.
3. `docs/sim/gdd_parser_check.py`는 GDD 8절 규칙을 파이썬으로 옮긴 검산 스크립트다. 규칙의 원본은 아니지만 결과는 GDD와 일치한다(GDD 18절). 이 문서의 테스트 벡터 A군은 그 스크립트로 다시 돌려 얻은 값이다.

### 0.2 표시

- 각 항목 끝의 `(출처)`는 원본 파일과 절 번호다. `GDD` = `docs/GDD.md`, `WC` = `docs/world-characters.md`, `FX` = `docs/effects.md`, `ON` = `docs/online.md`, `MO` = `docs/monsters.md`, `UI` = `docs/ui-assets.md`, `DEC` = `docs/DECISIONS.md`, `PY` = `docs/sim/gdd_parser_check.py`.
- **[확정]**: 개발자가 정한 것(DECISIONS [개발자] 항목, 원문의 [확정]/[개발자]).
- **[조율]**: 문서 사이 충돌을 풀려고 DECISIONS 2절 또는 원문에서 [조율]로 정리한 것.
- **[제안]**: 원문의 설계안(원문에 표시가 없는 수치도 [제안]이다. GDD 머리말).
- **[임시]**: 원문이 정하지 않아 이 명세가 구현을 막지 않으려고 넣은 기본값. 20절 "미정"에 모두 다시 모았다.
- 수치는 원문 그대로 적고, 틱·내부 단위로 바꿀 수 있는 것은 `→` 뒤에 환산값을 적는다.

### 0.3 범위 밖

- 그림, 이펙트 그림, 소리 자체는 만들지 않는다. 붙을 자리(이벤트 이름, 에셋 ID)만 21절에 둔다.
- 투사체를 실제로 만드는 `Execute(...)`와 T(트리 1회 실행 피해)의 계산은 주문 컴파일러 명세의 몫이다. 이 문서는 그 함수를 어떤 인자로 부르는지만 정한다.
- 이동·충돌·적 AI·과열·콤보 계산 자체는 다른 명세의 몫이다. 비기가 그들에게 넘기는 값만 정한다.
- 일정·마감은 다루지 않는다(DEC 1.16 [확정]). 22절에는 의존 순서만 적는다.

---

## 1. 단위와 환산

### 1.1 게임 시간

- 게임 시간은 1초 = 60틱이다. (GDD 머리말) [조율]
- 입력 시각은 정수 **서브틱 단위**로 다룬다: `시각 = 유효 틱 × 256 + 틱 안 오프셋(0~255)`. 1단위 = 1/15,360초. (GDD 3·8 판정기, ON 3.1) [조율]
- 실제 시각 환산 `(유효 틱 + 오프셋 ÷ 256) × 16.667ms`. (ON 3.1) [조율]
- 프레임 단위로 읽지 않는다. 간격은 정수로 비교한다. (GDD 8 판정기) [제안]
- 히트스톱, 화면 흔들림은 표시 계층의 일이고 게임 시간(틱)을 멈추지 않으므로 판정에 영향이 없다. (GDD 3, GDD 8 판정기, ON 3.1) [조율]
- 코드 상수 이름 제안: `const long SubTicksPerTick = 256; const long SubTicksPerSecond = 15360;`

### 1.2 판정기 상수 (서브틱)

| 상수 이름 | 원문 값 | 서브틱 | 틱 | 출처 |
|---|---|---|---|---|
| `JustWindow` | 0.07초 | **1,075** (원문 지정) | 4.2틱 | GDD 8 판정기 [제안] |
| `GapFullBloom` | 0.18초 | **2,765** (원문 지정) | 10.8틱 | GDD 8 판정기 [제안] |
| `GapShort` | 0.2초 | **3,072** | 12틱 | GDD 8 판정기 [제안] |
| `GapSameSide` | 0.25초 | **3,840** | 15틱 | GDD 8 판정기 [제안] |
| `GapDefault` (= 커맨드 초기화 시간) | 0.3초 | **4,608** | 18틱 | GDD 8 판정기 [제안] |
| `ArtCooldown` | 0.4초 | 6,144 | 24틱 | GDD 8 입력 규칙 [제안] |
| `KBufferWindow` | 0.1초 | 1,536 | 6틱 | GDD 8 입력 규칙 [제안] |
| `DirHistorySize` | 최근 방향 입력 8개 | — | — | GDD 8 판정기 [제안] |

- 0.07초 × 15,360 = 1,075.2, 0.18초 × 15,360 = 2,764.8이다. 원문이 1,075와 2,765로 못박았으므로 그 정수를 그대로 쓴다(가장 가까운 정수). (GDD 8 판정기)
- 비교 규칙: **경과 > 한도이면 실패, 경과 ≤ 한도이면 통과**. 한도와 같은 값은 통과다. (PY `t - times[-1] > T[0.3]`, `0 <= t - times[-1] <= T[0.07]`) [제안]

### 1.3 거리

- 거리는 칸(1칸 = 64px). 게임 수치는 칸 단위 그대로다. (DEC 2.1) [조율]
- 고정소수점 Q32.32 정수를 쓴다(1.0 = 2^32 = 4,294,967,296). (ON 3.1) [조율]
- 이 문서에 나오는 칸 값의 Q32.32 원시값(정확히 표현됨): 0.5칸 = 2,147,483,648 / 1칸 = 4,294,967,296 / 1.5칸 = 6,442,450,944 / 2칸 = 8,589,934,592 / 2.5칸 = 10,737,418,240 / 3칸 = 12,884,901,888 / 4칸 = 17,179,869,184 / 4.5칸 = 19,327,352,832 / 12칸 = 51,539,607,552.
- 퍼센트 배율(70%, 60%, 50%, −20% 등)은 Q32.32로 근사하지 말고 정수 백분율로 저장해 `x * p / 100`으로 적용한다(0.7은 2진 고정소수점으로 정확히 표현되지 않는다). [임시]

### 1.4 효과 시간 환산 (틱, 60틱/초)

| 원문 | 틱 | | 원문 | 틱 |
|---|---|---|---|---|
| 0.12초(만개 반복 간격) | 7.2틱 → 20절 U-13 | | 0.8초 | 48 |
| 0.15초 | 9 | | 1초 | 60 |
| 0.2초 | 12 | | 1.5초 | 90 |
| 0.25초 | 15 | | 2초 | 120 |
| 0.3초 | 18 | | 4초 | 240 |
| 0.5초 | 30 | | 6초 | 360 |
| 0.6초 | 36 | | 0.75초 | 45 |

---

## 2. 식별자

이름은 전부 작업용이다. 코드와 데이터는 이름을 이름 표 한 곳에만 두고 id로 가리킨다. 화면의 캐릭터 이름은 임시로 "제자 1"~"제자 5"다. (DEC 1.25 [확정], GDD 13)

### 2.1 캐릭터

| `CharacterId` | 문자열 id | 속성 | 작업용 이름 | 결정 상한 | 피격 시 결정 손실 | 고유 비기 |
|---|---|---|---|---|---|---|
| `Char1` | `char_1` | 화염 `fire` | 달구 | 5 | 1 | `Kiln` |
| `Char2` | `char_2` | 번개 `lightning` | 초롱 | 5 | 1 | `BoltDash` |
| `Char3` | `char_3` | 비전 `arcane` | 갈피 | 4 | 1 | `Transcribe` |
| `Char4` | `char_4` | 냉기 `frost` | 성에 | 6 | 0 | `IceScreen` |
| `Char5` | `char_5` | 암흑 `dark` | 그믐 | 7 | 2 | `ShadowHand` |

(DEC 1.25 표 [확정], GDD 7·10.1 [제안]) 결정 상한은 만개 최대 반복 수이기도 하다(GDD 8 공용 비기).

### 2.2 비기 (`ArtId`, 15종)

| `ArtId` | 문자열 id | 번호 | 작업용 이름 | 종류 |
|---|---|---|---|---|
| `ManaRelease` | `art.mana_release` | ① | 마력 방출 | 공용 |
| `RecoilShot` | `art.recoil_shot` | ② | 반동 사격 | 공용 |
| `FanSpread` | `art.fan_spread` | ③ | 부채 전개 | 공용 |
| `TwinWing` | `art.twin_wing` | ④ | 양익 사격 | 공용 |
| `AfterimageStep` | `art.afterimage_step` | ⑤ | 잔상 걸음 | 공용 |
| `PiercingLance` | `art.piercing_lance` | ⑥ | 꿰뚫는 창 | 공용 |
| `VentBurst` | `art.vent_burst` | ⑦ | 방열 폭발 | 공용 |
| `EightfoldSpread` | `art.eightfold_spread` | ⑧ | 팔방 전개 | 공용 |
| `ThunderLance` | `art.thunder_lance` | ⑨ | 비장: 뇌광창 | 공용(비장) |
| `FullBloom` | `art.full_bloom` | ⑩ | 비장: 만개 | 공용(비장) |
| `Kiln` | `art.kiln` | ★ | 불가마 | 고유(`char_1`) |
| `BoltDash` | `art.bolt_dash` | ★ | 벼락 질주 | 고유(`char_2`) |
| `Transcribe` | `art.transcribe` | ★ | 베껴 쓰기 | 고유(`char_3`) |
| `IceScreen` | `art.ice_screen` | ★ | 얼음 병풍 | 고유(`char_4`) |
| `ShadowHand` | `art.shadow_hand` | ★ | 그림자 손 | 고유(`char_5`) |

- 이름 표 키 제안: `name.art.<snake>`(예: `name.art.kiln` → "불가마"). 이름은 작업용이고 화면에는 이름 표의 문자열을 찍는다. (DEC 1.25 [확정], WC 개발자 확인 4: 기술 이름은 정해질 때까지 작업용 이름을 그대로 찍는다)
- 열거값의 정수 순서는 위 표 순서(0~14)로 고정하고 바꾸지 않는다(입력 기록·해시·DB 호환). [임시]
- 원문 기호: ①~⑩는 공용, ★는 고유 비기 자리. (GDD 8 트리)

### 2.3 상대 방향 (`CmdDir`)

| `CmdDir` | 값 | 원문 |
|---|---|---|
| `Forward` | 0 | 앞 |
| `Back` | 1 | 뒤 |
| `Left` | 2 | 좌(옆) |
| `Right` | 3 | 우(옆) |

트라이 알파벳은 이 4개다. 원문 표기와 트리 그림은 좌우를 "옆"으로 묶지만 노드는 좌우를 따로 만든다. (GDD 8 판정기) [제안]

---

## 3. 입력 규칙

| ID | 규칙 | 출처 |
|---|---|---|
| R-01 | 조작: WASD 이동(이동 방향 = 시야 = 발사 방향, 8방향), J 발사, **K 비기 전용**, Shift(누르는 동안) 자세, Space(누르는 동안) 반전 자세. 전투는 키보드만. | DEC 1.19 [확정], GDD 3 [확정] |
| R-02 | 대체 배치: 방향키 + Z 발사 / X 비기 / C 자세 / V 반전. 모든 키 재설정 가능. 판정기는 물리 키가 아니라 논리 키(이동 4방향, Fire, Art, Stance, Reverse)를 받는다. | GDD 3 [제안]; 논리 키 분리는 [임시] |
| R-03 | 키는 물리 키 기준으로 읽고 한글 입력기의 영향을 받지 않아야 한다(빌드에서 확인). | GDD 3 [제안] |
| R-04 | 방향 입력(앞/뒤/옆)은 **자세 중에만** 커맨드로 읽힌다. **시야 기준**이다. 반전 자세도 자세이고 커맨드를 인정한다. | GDD 8 입력 규칙, GDD 3 [제안] |
| R-05 | 자세에 들어갈 때 시야가 대각선이면 마지막에 누른 키 방향(상하좌우)으로 맞춘다. 자세 중에는 대각 사격이 없다. → 자세 중 시야는 항상 4방향 중 하나다. | GDD 3 [제안] |
| R-06 | 모든 비기는 K로 끝난다. **자세 밖의 K는 항상 마력 방출이다.** | GDD 8 입력 규칙 [제안] |
| R-07 | 모든 입력은 **키를 누르는 순간**만 본다. 이미 누르고 있던 방향키는 입력이 아니므로, 쓰려면 뗐다가 다시 눌러야 한다. | GDD 8 입력 규칙 [제안] |
| R-08 | 섬(저스트): 방향 키를 누르고 0.07초 안에 K. 방향이 먼저여야 한다. 늦으면 같은 자리의 일반 기술이 나간다. K가 먼저면 그 시점의 커맨드로 처리한다(앞, K = 부채 전개). 방향과 K가 같은 오프셋이면 방향이 먼저다. | GDD 8 입력 규칙 [제안] |
| R-09 | 입력 사이 최대 간격은 4절 표. | GDD 8 입력 규칙 [제안] |
| R-10 | 마지막 방향 입력 후 0.3초(4,608u)가 지나면 커맨드는 처음(루트)으로 돌아간다. | GDD 8 입력 규칙 [제안] |
| R-11 | 자세 진입, 해제, 반전 때 커맨드는 처음으로 돌아간다. 일시정지에서도 처음으로 돌아간다. | GDD 8 입력 규칙 [제안] |
| R-12 | Shift를 누른 채 Space를 누르면 잠긴 시야만 180도 뒤집는다. Space를 떼면 시야는 뒤집힌 방향으로 남고, 이동 키가 눌려 있으면 그 방향으로 바로 바뀐다. → 둘 다 "반전"이므로 R-11로 커맨드가 처음으로 돌아간다. | GDD 3 [제안]; 초기화 적용은 R-11의 해석 |
| R-13 | 입력은 프레임이 아니라 이벤트로 기록한다. 이벤트 = (유효 틱, 틱 안 오프셋 1/256틱, 키, 누름/뗌, 순번). 구조체는 하나이고 무한 웨이브 기록과 결투 패킷이 같이 쓴다: `(유효 틱, 오프셋 8비트, 키 코드 4비트, 누름/뗌 1비트, 플레이어별 순번)`. 한 틱에 최대 8개. | GDD 3 [조율], ON 3.1 [조율] |
| R-14 | 처리 순서: (유효 틱, 오프셋, 순번) 순. **같은 틱·같은 오프셋이면 방향이 K보다 먼저.** 판정기, 판 재실행 검증, 결투가 모두 이 순서를 쓴다. 이 규칙이 없으면 기록을 다시 돌릴 때 다른 기술이 나갈 수 있다. | GDD 3 [조율], ON 3.1 [조율] |
| R-15 | 결정이 모자라면 불발. 더 싼 기술로 바뀌지 않는다. 예외 둘: 고유 비기 5종은 결정이 2개에 못 미치면 부모 노드의 기술(결정 1개)이 대신 나가고, 그믐은 외상(빚 결정 2개까지)을 먼저 적용한다. | GDD 8 입력 규칙 [제안], WC 3.5 |
| R-16 | 비기 공통 재사용 대기 0.4초(24틱). 대기가 끝나기 0.1초(6틱) 전부터 누른 K는 보관했다가 끝나는 순간 처리한다. | GDD 8 입력 규칙 [제안] |
| R-17 | 판정기는 유니티와 분리된 순수 C# 클래스로 결정론 코어 안에 만들고 입력열 단위 테스트를 붙인다. | GDD 8 판정기 [제안], GDD 15 [조율] |
| R-18 | 대시는 없다. | GDD 3 [제안] |

### 3.1 키 → 상대 방향 변환 [임시]

자세 중 시야 `facing ∈ {Up, Down, LeftScreen, RightScreen}`(R-05로 항상 4방향). 이동 키 하나를 누르면 그 물리 방향 `p`를 다음처럼 바꾼다.

```
rel(p, facing):
  if p == facing                 -> Forward
  if p == opposite(facing)       -> Back
  if p == rotateCCW90(facing)    -> Left     // 캐릭터 기준 왼쪽
  if p == rotateCW90(facing)     -> Right
```

- 반전 자세 중에는 `facing`이 뒤집힌 시야다(R-04, GDD 3).
- 좌/우 이름은 판정 결과에 영향이 없다. 트라이가 좌우 대칭이고(5.1절), 구분하는 것은 "같은 쪽"과 "반대쪽"뿐이다. 잔상 걸음이 이동하는 방향은 상대 방향이 아니라 **마지막으로 누른 물리 방향**이다(GDD 8 ⑤ "누른 쪽으로").
- 대각선을 만드는 두 키 누름은 각각 별도 방향 입력이다. [임시]

---

## 4. 입력 간격 규칙

### 4.1 간선별 허용 간격

| 구간(간선) | 간격 | 서브틱 | 해당 노드 | 출처 |
|---|---|---|---|---|
| 기본 | 0.3초 | 4,608 | 앞→앞(`FF`), 뒤→앞(`BF`), 뒤→옆(`BL`/`BR`, 얼음 병풍) | GDD 8 [제안] |
| 같은 쪽 옆 두 번(잔상 걸음) | 0.25초 | 3,840 | `LL`, `RR` | GDD 8 [제안] |
| 앞→뒤 | 0.2초 | 3,072 | `FB` | GDD 8 [제안] |
| 옆→앞 | 0.2초 | 3,072 | `LF`, `RF`(초롱) | GDD 8 [제안], WC 3.5 |
| 앞→옆 | 0.2초 | 3,072 | `FL`, `FR`(갈피) | GDD 8 [제안], WC 3.5 |
| 좌→우(반대쪽 옆) | 0.2초 | 3,072 | `LR`, `RL`(그믐) | GDD 8 [제안], WC 3.5 |
| 달구의 뒤→뒤 | 0.2초 | 3,072 | `BB`(달구에만 있다) | GDD 8 [제안], WC 3.5 |
| 만개의 각 입력(앞→뒤, 뒤→뒤, 뒤→앞, 앞→K) | 0.18초 | 2,765 | `FBB`, `FBBF`(그리고 K 판정 때 재검사) | GDD 8 [제안] |

- 앞→뒤 간선은 팔방 전개와 만개가 같이 쓴다. 노드 `FB`는 기본값 0.2초를 저장하고, 팔방 전개에는 0.2초, 만개에는 0.18초로 적용한다. (GDD 8 입력 규칙·판정기) [제안]
- 깊이 1 노드(루트→X)로 들어오는 간선에는 간격이 없다(직전 입력이 없다). (PY `edge_gap` `len(path) < 2 → None`)
- 마지막 방향 입력에서 K까지의 한도는 0.3초다(섬은 0.07초, 만개는 0.18초). (GDD 8 판정기) [제안]
- 이유(참고): 옆→앞, 앞→옆, 좌→우, 달구의 뒤→뒤를 0.2초로 잡은 것은 자세 중에 움직이다 누른 K가 고유 비기로 읽히는 오입력을 줄이기 위해서다. GDD의 "같은 방향 두 번 0.25초"는 잔상 걸음에만 남는다. 앞→뒤 0.2초는 "앞으로 걷다가 뒤, K"가 반동 사격 대신 팔방 전개로 읽히는 오입력을 줄이려고 줄였다. (WC 3.5, GDD 18 기록)

### 4.2 간격 함수 (트라이 생성 때 노드에 저장)

```
EdgeGap(path, character) -> long?   // path = 노드의 경로, 마지막 간선 = path[^2] -> path[^1]
  if path.Length < 2: return null
  a = path[^2]; b = path[^1]
  if path == [F,B,B] or path == [F,B,B,F]: return GapFullBloom   // 2765
  if a is Side and b == a:            return GapSameSide            // 3840
  if a == F and b == B:               return GapShort               // 3072
  if a is Side and b == F:            return GapShort
  if a == F and b is Side:            return GapShort
  if a is Side and b is Side and a!=b:return GapShort
  if character == Char1 and path == [B,B]: return GapShort
  return GapDefault                                                 // 4608
```
(PY `edge_gap`, GDD 8) — 이 순서 그대로 쓴다. `FBB` 노드에는 기술이 없으므로 그 값은 만개 노드의 경로 검사에서만 의미가 있다.

---

## 5. 트라이

### 5.1 트리 (원문 그대로)

```
루트 ─ K ················ ① 마력 방출
 ├ 앞 ─ K ·············· ③ 부채 전개
 │  ├ 앞 ─ K ··········· ⑥ 꿰뚫는 창
 │  │     └ 섬 ········· ⑨ 비장: 뇌광창
 │  ├ 뒤 ─ K ··········· ⑧ 팔방 전개
 │  │  └ 뒤 ─ 앞 ─ K ··· ⑩ 비장: 만개
 │  └ 옆 ─ K ··········· ★ 갈피: 베껴 쓰기
 ├ 뒤 ─ K ·············· ② 반동 사격
 │  ├ 앞 ─ K ··········· ⑦ 방열 폭발
 │  ├ 뒤 ─ K ··········· ★ 달구: 불가마
 │  └ 옆 ─ K ··········· ★ 성에: 얼음 병풍
 └ 옆 ─ K ·············· ④ 양익 사격
    ├ 옆(같은 쪽) ─ K ··· ⑤ 잔상 걸음
    ├ 옆(반대쪽) ─ K ··· ★ 그믐: 그림자 손
    └ 앞 ─ K ··········· ★ 초롱: 벼락 질주
```
(GDD 8 트리, WC 3.5 — 두 문서가 같다)

- ★ 다섯 자리는 공용 10종의 경로와 겹치지 않는 빈자리다. 한 캐릭터의 트라이에는 ★가 하나만 있다. (GDD 8 [제안], WC 3.5)
- 각 캐릭터는 공용 10 + 고유 1 = 11종을 쓴다. 트라이는 **판을 시작할 때 캐릭터별로** 만든다. 다른 캐릭터의 고유 노드는 트라이에 없다. 결투에서도 공용 10 + 고유 1이다. (GDD 8 [제안], WC 3.5)
- "좌, 좌"(잔상 걸음)와 "좌, 우"(그림자 손)는 서로 다른 노드다. 좌우는 대칭으로 양쪽 다 만든다. GDD가 잔상 걸음을 "같은 쪽 두 번만 인정"으로 정해 둔 덕에 반대쪽 자리가 비어 있다. (GDD 8 판정기, WC 3.5)

### 5.2 넣을 경로 (`ArtDef.Paths`)

| ArtId | 경로(좌우 대칭 모두) | 붙는 노드 슬롯 |
|---|---|---|
| ManaRelease | `[]`(루트) | K |
| RecoilShot | `[B]` | K |
| FanSpread | `[F]` | K |
| TwinWing | `[L]`, `[R]` | K |
| AfterimageStep | `[L,L]`, `[R,R]` | K |
| PiercingLance | `[F,F]` | K |
| ThunderLance | `[F,F]` | **섬**(Just) 슬롯 |
| VentBurst | `[B,F]` | K |
| EightfoldSpread | `[F,B]` | K |
| FullBloom | `[F,B,B,F]` | K (노드 `FBB`는 기술 없는 통과 노드) |
| Kiln (char_1) | `[B,B]` | K, 부모 대체 |
| BoltDash (char_2) | `[L,F]`, `[R,F]` | K, 부모 대체 |
| Transcribe (char_3) | `[F,L]`, `[F,R]` | K, 부모 대체 |
| IceScreen (char_4) | `[B,L]`, `[B,R]` | K, 부모 대체 |
| ShadowHand (char_5) | `[L,R]`, `[R,L]` | K, 부모 대체 |

(GDD 8 트리·공용 비기·고유 비기, WC 3.5, PY `COMMON`/`UNIQUE`/`JUST`)

### 5.3 노드 구조

```csharp
public struct TrieNode {
    public int    Id;            // 0 = 루트. BFS 순서로 부여 [임시]
    public byte   Depth;         // 0~4
    public CmdDir[] Path;        // 루트에서의 경로(디버그·간격 검사용)
    public int[]  Child;         // 길이 4 (F,B,L,R). 없으면 -1
    public int[]  Next;          // 길이 4. 실패 링크까지 펼친 전이(DFA). 항상 유효 [임시: 입력당 O(1)]
    public int    Fail;          // 실패 링크. 루트는 0(자기 자신)
    public ArtId? KArt;          // K 기술. 없으면 null
    public ArtId? JustArt;       // 섬 기술. FF 노드에만 ThunderLance
    public long?  InEdgeGap;     // 들어오는 간선의 허용 간격(서브틱). 깊이 0~1은 null
    public bool   UniqueFallback;// 부모 대체 여부(고유 비기 노드만 true)
    public bool   IsFullBloom;   // FBBF 노드만 true
    public int    Parent;        // 부모 노드 id (부모 대체용)
}
```

- 원문 필드: 자식 4개, K 기술, 섬 기술, 들어오는 간선의 허용 간격, 실패 링크, 해금 여부, 부모 대체 여부. (GDD 8 판정기) [제안]
- "해금 여부"는 노드에 굽지 않고 판 동안의 `UnlockSet`(15.4절)에서 읽는다. 판 중에 해금이 바뀌어도(야영에서 고름) 트라이를 다시 만들 필요가 없다. 동작은 원문과 같다. [임시]
- 노드 수: 달구 13, 그 밖 14(루트 포함). 최대 깊이 4. (PY 계산)

### 5.4 트라이 생성

```
BuildTrie(character):
  root = new node(depth 0)
  for art in CommonArts + [UniqueArtOf(character)]:
    for path in art.Paths:
      n = root
      for d in path: n = n.Child[d] ?? create child(depth+1, Path = n.Path + d, Parent = n)
      if art == ThunderLance: n.JustArt = ThunderLance
      else: n.KArt = art
      if art.Kind == Unique: n.UniqueFallback = true
      if art == FullBloom: n.IsFullBloom = true
  for each node n (depth >= 2): n.InEdgeGap = EdgeGap(n.Path, character)
  // 실패 링크 (아호-코라식, BFS)
  root.Fail = root
  queue = children of root; for c in them: c.Fail = root
  while queue not empty:
    u = dequeue
    for d in [F,B,L,R]:
      v = u.Child[d]; if v == null: continue
      f = u.Fail
      while f != root and f.Child[d] == null: f = f.Fail
      v.Fail = (f.Child[d] != null and f.Child[d] != v) ? f.Child[d] : root
      enqueue v
  // DFA 전이
  for n in BFS order:
    for d: n.Next[d] = n.Child[d] ?? (n == root ? root.Child[d] ?? root : n.Fail.Next[d])
```

- 실패 링크 = 그 노드 경로의 가장 긴 진접미사 중 트라이에 있는 노드. (GDD 15 "최장 접미사 일치" [제안], PY `fail`)
- 방향 입력마다 커서가 한 칸 이동한다. 자식이 없으면 실패 링크를 따라간다. 입력당 O(1), 최악 O(깊이). (GDD 8 판정기) `Next` 표를 미리 펼치면 항상 O(1)이다. 결과는 같다.
- 루트는 네 방향 자식을 모두 가지므로, 방향 입력 뒤의 커서는 항상 깊이 1 이상이다.

### 5.5 캐릭터별 실패 링크와 간격 (생성 결과, 테스트 벡터 TV-S로 검사)

공통 노드(다섯 캐릭터 모두):

| 노드 | 실패 링크 | InEdgeGap | K 기술 |
|---|---|---|---|
| root | root | — | ManaRelease |
| B | root | — | RecoilShot |
| F | root | — | FanSpread |
| L / R | root | — | TwinWing |
| BF | F | 4,608 | VentBurst |
| FB | B | 3,072 | EightfoldSpread |
| FF | F | 4,608 | PiercingLance (+ 섬 ThunderLance) |
| LL / RR | L / R | 3,840 | AfterimageStep |
| FBB | **달구 BB / 그 밖 B** | 2,765 | (없음) |
| FBBF | BF | 2,765 | FullBloom |

캐릭터별 고유 노드:

| 캐릭터 | 노드 | 실패 링크 | InEdgeGap | K 기술 |
|---|---|---|---|---|
| char_1 | BB | B | 3,072 | Kiln |
| char_2 | LF / RF | F / F | 3,072 | BoltDash |
| char_3 | FL / FR | L / R | 3,072 | Transcribe |
| char_4 | BL / BR | L / R | 4,608 | IceScreen |
| char_5 | LR / RL | R / L | 3,072 | ShadowHand |

### 5.6 DFA 전이 표 (`Next[node][F,B,L,R]`)

char_1(달구):

| 노드 | F | B | L | R |
|---|---|---|---|---|
| root | F | B | L | R |
| B | BF | BB | L | R |
| F | FF | FB | L | R |
| L | F | B | LL | R |
| R | F | B | L | RR |
| BB | BF | BB | L | R |
| BF | FF | FB | L | R |
| FB | BF | FBB | L | R |
| FF | FF | FB | L | R |
| LL | F | B | LL | R |
| RR | F | B | L | RR |
| FBB | FBBF | BB | L | R |
| FBBF | FF | FB | L | R |

char_2(초롱):

| 노드 | F | B | L | R |
|---|---|---|---|---|
| root | F | B | L | R |
| B | BF | B | L | R |
| F | FF | FB | L | R |
| L | LF | B | LL | R |
| R | RF | B | L | RR |
| BF | FF | FB | L | R |
| FB | BF | FBB | L | R |
| FF | FF | FB | L | R |
| LF | FF | FB | L | R |
| LL | LF | B | LL | R |
| RF | FF | FB | L | R |
| RR | RF | B | L | RR |
| FBB | FBBF | B | L | R |
| FBBF | FF | FB | L | R |

char_3(갈피):

| 노드 | F | B | L | R |
|---|---|---|---|---|
| root | F | B | L | R |
| B | BF | B | L | R |
| F | FF | FB | FL | FR |
| L | F | B | LL | R |
| R | F | B | L | RR |
| BF | FF | FB | FL | FR |
| FB | BF | FBB | L | R |
| FF | FF | FB | FL | FR |
| FL | F | B | LL | R |
| FR | F | B | L | RR |
| LL | F | B | LL | R |
| RR | F | B | L | RR |
| FBB | FBBF | B | L | R |
| FBBF | FF | FB | FL | FR |

char_4(성에):

| 노드 | F | B | L | R |
|---|---|---|---|---|
| root | F | B | L | R |
| B | BF | B | BL | BR |
| F | FF | FB | L | R |
| L | F | B | LL | R |
| R | F | B | L | RR |
| BF | FF | FB | L | R |
| BL | F | B | LL | R |
| BR | F | B | L | RR |
| FB | BF | FBB | BL | BR |
| FF | FF | FB | L | R |
| LL | F | B | LL | R |
| RR | F | B | L | RR |
| FBB | FBBF | B | BL | BR |
| FBBF | FF | FB | L | R |

char_5(그믐):

| 노드 | F | B | L | R |
|---|---|---|---|---|
| root | F | B | L | R |
| B | BF | B | L | R |
| F | FF | FB | L | R |
| L | F | B | LL | LR |
| R | F | B | RL | RR |
| BF | FF | FB | L | R |
| FB | BF | FBB | L | R |
| FF | FF | FB | L | R |
| LL | F | B | LL | LR |
| LR | F | B | RL | RR |
| RL | F | B | LL | LR |
| RR | F | B | RL | RR |
| FBB | FBBF | B | L | R |
| FBBF | FF | FB | L | R |

(PY의 `fail`로 계산한 전이. 판정 결과는 PY `direction()`과 같다)

---

## 6. 판정기 상태와 상태 기계

### 6.1 상태

```csharp
public sealed class CommandParser {
    // 판 시작 때 고정
    CommandTrie trie;            // 캐릭터별
    CharacterId character;
    // 커서
    int  cursor;                 // 노드 id, 0 = 루트
    RingBuffer<long> dirTimes;   // 최근 방향 입력 시각(서브틱), 용량 8 (DirHistorySize)
    bool hasDir;                 // dirTimes가 비어 있지 않음
    long lastDirTime;            // dirTimes 마지막 값
    // 자세
    bool inStance;               // Shift 또는 Space 중
    // 재사용 대기
    long cooldownEnd;            // 서브틱. 0 = 대기 없음
    PendingArt? buffered;        // 보관된 K의 판정 결과(최대 1개) [임시]
}
```
- 커서·입력 기록은 판 안의 상태이고 저장하지 않는다. 상태 해시에는 넣는다(결정론). [임시]
- 결정과 빚은 판정기가 갖지 않고 `ICrystalWallet`(9.1절)에서 읽고 쓴다. [임시]

### 6.2 상태 기계

상태: `OutOfStance`(자세 밖), `StanceRoot`(자세 중, 커서 = 루트), `StanceChain`(자세 중, 커서 ≠ 루트). 직교 상태: `Ready` / `Cooldown` / `CooldownBuffered`.

| 현재 | 사건 | 조건 | 동작 | 다음 |
|---|---|---|---|---|
| OutOfStance | 방향 누름 | — | 무시(기록하지 않음) [임시] | OutOfStance |
| OutOfStance | K 누름 | — | 루트 기술(마력 방출)로 판정(R-06) → 7.3 결제 | OutOfStance |
| OutOfStance | Shift/Space 누름 | — | Reset | StanceRoot |
| StanceRoot/Chain | 방향 누름 | `hasDir` 이고 `t − lastDirTime > 4608` | Reset 뒤 아래 동작 | — |
| StanceRoot/Chain | 방향 누름 | — | `cursor = Next[cursor][d]`, dirTimes에 t 추가 | StanceChain |
| StanceChain | 시간 경과 | `now − lastDirTime > 4608` | (지연 처리) 다음 입력·미리보기 때 Reset으로 본다 | StanceRoot |
| StanceRoot/Chain | K 누름 | — | 7.2 Evaluate → 7.3 결제 → Reset | StanceRoot |
| StanceRoot/Chain | 자세 해제(두 키 모두 뗌) | — | Reset | OutOfStance |
| StanceRoot/Chain | 반전(Space 누름/뗌, Shift 중 Space) | — | Reset | StanceRoot |
| 어떤 상태 | 일시정지 | — | Reset | (유지) |
| 어떤 상태 | 판 시작, 웨이브 시작, 결투 라운드 시작 | — | Reset, 재사용 대기 해제 [임시] | — |
| Ready | 비기 발동 | — | `cooldownEnd = t_judge + 6144` | Cooldown |
| Cooldown | K 누름 | `t < cooldownEnd − 1536` | 무시. 커서·기록 그대로 [임시] | Cooldown |
| Cooldown | K 누름 | `cooldownEnd − 1536 ≤ t < cooldownEnd` | 그 시각 t로 선택+결제를 끝내고 결과를 보관, Reset [임시] | CooldownBuffered |
| CooldownBuffered | K 누름 | — | 무시 [임시] | CooldownBuffered |
| Cooldown(Buffered) | 시각이 `cooldownEnd`에 닿음 | — | 보관 결과를 발동(틱 = `ceil(cooldownEnd / 256)`) [임시], 발동이면 새 대기 시작 | Ready 또는 Cooldown |

- Reset = 커서를 루트로, 입력 기록(dirTimes)을 비운다. 발동하거나 불발하면 커서와 입력 기록을 비운다. (GDD 8 판정기) [제안]
- 불발은 재사용 대기를 시작하지 않는다. [임시]
- 자세 진입·해제·반전의 정의: `inStance = Shift 눌림 || Space 눌림`의 값이 바뀌는 순간, 그리고 시야가 180도 뒤집히는 순간(Space 누름, Space 뗌, Shift 중 Space 누름). (R-11, R-12) [임시: 사건 목록]

---

## 7. 판정 알고리즘

### 7.1 방향 입력

```
OnDirectionPress(CmdDir d, long t):
  if !inStance: return                                  // R-04
  if hasDir and t - lastDirTime > GapDefault: Reset()   // R-10
  cursor = trie.Next[cursor][d]
  dirTimes.Push(t)                                      // 용량 8, 오래된 것부터 버림
  lastDirTime = t; hasDir = true
  emit cmd.dir_input(d, chained = (cursor depth >= 2 && 마지막 간선 간격 통과))
```
(PY `direction`, GDD 8 판정기)

### 7.2 K 판정 (선택)

```
Evaluate(long t, ICrystalWallet w, UnlockSet u, bool commit) -> ArtDecision
  if !inStance: node = root                              // R-06
  else:
    if hasDir and t - lastDirTime > GapDefault: node = root (commit이면 Reset())
    else: node = cursor
  // 실패 링크를 따라 첫 유효 노드를 찾는다
  loop:
    if node.KArt != null and u.IsUnlocked(node.KArt):
      if node.IsFullBloom:
        (ok, idx, lateBy) = CheckFullBloom(node, t)
        if !ok: return Fizzle(FullBloomLate, idx, lateBy)   // 실패 링크로 가지 않는다
        break
      if GapsOk(node): break
    node = node.Fail          // 루트의 마력 방출은 항상 해금·간격 없음 → 반드시 끝남
  art = node.KArt
  // 섬
  if node.JustArt != null and u.IsUnlocked(node.JustArt)
     and hasDir and 0 <= t - lastDirTime <= JustWindow:
       art = node.JustArt; just = true
  return Pay(art, node, w, commit)      // 7.3
```

```
GapsOk(node):          // 경로 위 노드들의 기본값으로 검사
  k = node.Depth
  if k <= 1: return true
  ts = dirTimes 마지막 k개 (오래된 것 → 최근)
  for i in 1..k-1:
    if ts[i] - ts[i-1] > InEdgeGap(node.Path[0..i]): return false
  return true

CheckFullBloom(node, tK):   // 만개 노드만. 경로의 간격 넷을 전부 0.18초로 재검사
  seq = dirTimes 마지막 4개 + [tK]
  for i in 1..4:
    if seq[i] - seq[i-1] > GapFullBloom:
       return (false, i + 1, seq[i] - seq[i-1] - GapFullBloom)   // "i+1번째 입력"
  return (true, -, 0)
```

규칙 대응:

- K 판정: 현재 노드부터 실패 링크를 따라가며 "기술이 있고, 해금됐고, 그 경로의 간격을 모두 통과한" 첫 노드를 고른다. 해금하지 않은 노드는 기술이 없는 노드로 본다. 루트에는 마력 방출이 있어 항상 끝난다. (GDD 8 판정기) [제안]
- 간선의 허용 간격은 그 간선으로 들어가는 노드에 하나만 저장한다(그 노드의 기본값). 어떤 노드의 간격 검사는 그 노드의 깊이만큼의 최근 방향 입력을 놓고, 경로 위 노드들의 기본값으로 한다. 실패 링크로 내려온 노드도 같은 방법으로 검사한다. (GDD 8 판정기) [제안]
  - 실패 링크 사슬의 노드는 모두 현재 입력열의 접미사이므로 "깊이만큼의 최근 방향 입력"이 곧 그 노드의 경로다.
  - 마지막 방향→K 간격은 0.3초 초기화(R-10)가 이미 보장하므로 `GapsOk`에서 따로 보지 않는다(만개 제외). (PY `gaps_ok`)
- 만개 노드만 예외: K 판정 때 자기 경로의 간격 넷(앞→뒤, 뒤→뒤, 뒤→앞, 앞→K)을 전부 0.18초로 다시 검사한다. (GDD 8 판정기) [제안]
- 만개를 해금했고 커서가 만개 노드에 있는데 이 검사에서 떨어지면, 실패 링크를 따라가지 않고 **불발**로 끝낸다(K 판정의 유일한 예외. 결정 소모 없음, 늦은 입력을 표시). "n번째 입력이 0.0x초 늦음". (GDD 8 공용 비기·판정기) [제안]
- 만개를 해금하지 않았으면 만개 노드는 기술이 없는 노드이므로 실패 링크를 따라간다. 이때 먼저 걸리는 것은 뒤-앞 노드의 방열 폭발이다. (GDD 8 판정기, GDD 개발자 확인 64: 지금 값 유지) [제안]
- 섬은 `FF` 노드가 선택됐을 때만 본다. 섬 기술이 해금되지 않았거나 0.07초를 넘기면 같은 자리의 일반 기술(꿰뚫는 창)이 나간다. (GDD 8 입력 규칙, PY `JUST`) [제안]
- 시각은 입력 이벤트의 시각(서브틱)을 쓴다. (GDD 8 판정기) [조율]
- 간격 검사는 최근 방향 입력 8개의 시각으로 한다. 최대 깊이가 4이므로 8개면 충분하다. (GDD 8 판정기) [제안]

### 7.3 결제 (결정이 모자랄 때)

```
Pay(art, node, w, commit) -> ArtDecision
  r = TryPay(art, w)                      // 외상 포함
  if r.ok: return Fire(art, paid = r)
  if node.UniqueFallback:                 // 고유 비기만, 한 번만
    parentArt = trie.Nodes[node.Parent].KArt
    if parentArt != null and u.IsUnlocked(parentArt):   // 부모 해금 검사 [임시] (19절 C-02)
      r2 = TryPay(parentArt, w)           // 외상 포함해 다시 본다
      if r2.ok: return Fire(parentArt, viaFallback = true, paid = r2)
  return Fizzle(NoCrystal)                // 더 내려가지 않는다

TryPay(art, w):
  if art.Cost == AllMin3:                 // 만개
    if w.Crystals >= 3: repeats = w.Crystals; w.Crystals = 0; ok   // 외상 불가
    else fail
  c = art.Cost (1 또는 2)
  if w.Crystals >= c: w.Crystals -= c; ok
  if w.HasCredit:                         // 그믐(char_5)만
    need = c - w.Crystals
    if w.Debt + need <= 2: w.Crystals = 0; w.Debt += need; ok(debtUsed = need)
  fail
```
(GDD 8 판정기 "고른 기술의 결정이 모자랄 때" 1~3, PY `pay`·`k`)

1. 그믐이면 외상을 먼저 적용한다(빚이 2개를 넘지 않는 범위에서. 만개는 제외). (GDD 8, WC 4.5) [제안]
2. 그래도 모자라고 고른 노드가 고유 비기면 부모 노드의 기술로 **한 번만** 내려간다(외상 포함해 다시 본다). (GDD 8, WC 3.5) [제안]
3. 그 밖에는 불발로 끝낸다. 더 내려가지 않는다. (GDD 8) [제안]

- `commit = false`이면 지갑을 바꾸지 않는 복사본으로 계산한다(미리보기용, 11절).
- 발동이면 결정 소모 → `ArtCast` 사건, 불발이면 `ArtFizzle` 사건. 어느 쪽이든 Reset. (GDD 8 판정기)
- 만개 반복 수 = 소모 직전 결정 수(최소 3, 최대 결정 상한). (GDD 8 공용 비기) [제안]

### 7.4 판정 결과 구조

```csharp
public enum ArtOutcome : byte { Fire, Fizzle }
public enum FizzleReason : byte { None, NoCrystal, FullBloomLate }
public readonly struct ArtDecision {
    public ArtOutcome Outcome;
    public ArtId      Art;            // Fire: 나간 기술. Fizzle: 고른 기술(FullBloomLate는 FullBloom)
    public ArtId      SelectedArt;    // 부모 대체 전의 기술
    public bool       ViaFallback;    // 고유 → 부모 대체로 나갔다
    public bool       IsJust;         // 섬으로 나갔다
    public byte       CrystalsPaid;   // 실제 결정 소모
    public byte       DebtAdded;      // 외상으로 생긴 빚(0~2)
    public byte       CrystalsAfter;  // 소모 뒤 결정 수(그림자 계약 계산용)
    public byte       Repeats;        // 만개 반복 수(그 밖 0)
    public FizzleReason Reason;
    public byte       LateIndex;      // FullBloomLate: 2~5 ("n번째 입력")
    public long       LateBy;         // FullBloomLate: 초과량(서브틱)
    public long       JudgeTime;      // 판정 시각(서브틱)
    public int        NodeId;         // 고른 노드(디버그, 연습장 표시)
}
```
[임시: 구조]. 원문이 요구하는 값(기술명, 비용, 부모 대체, 외상 N, 늦은 입력 번호와 초과량)을 모두 담는다.

### 7.5 불발 문구

- 만개 간격 초과: `"{LateIndex}번째 입력이 {초}초 늦음"`. (GDD 8 공용 비기, FX 12.1) [제안]
- 초 표시는 소수 둘째 자리이며 **올림**한다: `ceil(LateBy × 100 / 15360) / 100`. 예: 153u → 0.00996초 → "0.01초", 307u → "0.02초", 1,075u → "0.07초". 내림하면 "0.00초 늦음"이 나오므로 올림을 쓴다. [임시] (PY는 소수 셋째 자리로 출력한다. 19절 C-03)
- 연습장의 실패 원인 한 줄: "K가 0.0x초 늦음", "3번째 입력이 0.02초 늦음", "결정 부족". 뇌광창이 늦어 꿰뚫는 창으로 나가면 연습장에서는 "0.02초 늦음"처럼 섬 창을 넘긴 양이 뜬다. (GDD 13, UI 4.14, FX 12.2) [제안]. 섬 초과량 = `(t_K − lastDirTime) − 1075`, 같은 올림 규칙 [임시].

---

## 8. 비기 공통 규칙

| ID | 규칙 | 출처 |
|---|---|---|
| A-01 | 모든 비기는 열을 올리지 않는다. | GDD 8 [제안] |
| A-02 | 모든 비기는 임계 배율을 받지 않는다(비기는 임계 판정을 받지 않는다). 비기 배율은 임계 배율과 같이 적용하지 않는다. | GDD 8, GDD 4 [제안] |
| A-03 | 모든 비기는 강화 배율을 받는다(무한 웨이브 1.15^단계, 결투 `1 + 0.03 × 단계`). | GDD 8, GDD 4, ON 4.2 [제안] |
| A-04 | 과열 잠금 중: 마력 방출은 쓸 수 있지만 잠금은 풀리지 않는다. 잠금을 푸는 수단은 스쳐 피하기와 방열 폭발뿐이다. | GDD 8 [제안] |
| A-05 | 트리를 실행하는 비기의 피해 속성은 맞힌 투사체 블록의 속성이다. 트리를 쓰지 않는 고유 비기의 피해는 캐릭터 속성이다(꿰뚫는 창·뇌광창·방열 폭발은 코어 블록의 속성). | GDD 8, GDD 4·10.1 [조율] |
| A-06 | 비기로 맞힌 것은 콤보에 넣지 않는다. | GDD 7 [제안] |
| A-07 | 넉백, 기절, 경직은 적의 꼬리표에 따라 줄거나 듣지 않는다. 엘리트는 거리와 시간이 절반, 중량과 보스는 면역. 수치는 14.6절 표가 기준. 보스만 예외로 뇌광창의 기절을 0.3초 동작 멈춤으로 받는다. | GDD 8, WC 3.6, MO 3.2 [제안] |
| A-08 | 비기 한 번이 만드는 투사체는 최대 160개. 트리 실행 1회당 64개 상한, 화면 전체 플레이어 투사체 300개 상한(결투 1인 150)도 그대로 걸린다. | GDD 8, GDD 4, ON 4.2 [제안] |
| A-09 | 실행 함수는 `Execute(트리, 위치, 방향, 피해 배율, 열 면제 여부, 진영)`. 비기, 갈피의 사본 마법진, 보스(스승의 허상)가 같은 함수를 쓴다. | GDD 4 [제안] |
| A-10 | 피해 공식: `기본 × 강화 배율 × (1 + 변형 보너스 합) × 변이 배율 × 임계 배율 × 비기 배율 × 감쇠^깊이 × 속성 배율 × 대상 상태 배율 × 캐릭터 배율`. 비기에서는 임계 배율 = 1. | GDD 4 [제안] |
| A-11 | 그믐의 그림자 계약(결정 1개당 모든 피해 +3%, 7개에서 +21%)은 비기에는 **그 비기가 결정을 쓴 뒤 남은 개수**로 계산한다. 결정 5개에서 2개짜리 비기 → +9%. 만개 → +0%. 빚 결정은 0개로 센다. | WC 4.5, GDD 4·10.1 [제안] |
| A-12 | T = "지팡이 트리 1회 실행의 피해". "nT"는 그 n배. | GDD 머리말 [제안] |
| A-13 | 결정은 판 안에만 있다. 웨이브가 끝날 때 남은 결정은 개당 5골드(그믐의 빚이 있으면 빚부터 갚는다). | GDD 7 [제안] |
| A-14 | 피격 시 콤보 0, 결정 파괴(달구·초롱·갈피 1, 성에 0, 그믐 2). | GDD 7·10.1 [제안] |

---

## 9. 결정 지갑과 그믐의 외상

### 9.1 인터페이스

```csharp
public interface ICrystalWallet {
    int  Crystals { get; set; }   // 0 ~ Cap
    int  Cap { get; }             // 2.1절 결정 상한
    int  Debt { get; set; }       // 0 ~ 2 (그믐만, 그 밖 항상 0)
    bool HasCredit { get; }       // char_5만 true
}
```
[임시]. 결정을 얻는 쪽(콤보, 별똥 결정, 결투 라운드 시작)은 다른 명세가 맡지만 아래 규칙을 지킨다.

### 9.2 외상 규칙 (그믐)

1. 결정이 모자라도 비기가 나간다. 모자란 만큼 "빚 결정"이 생긴다(최대 2개). 만개는 외상으로 쓸 수 없다. (WC 4.5) [제안]
2. 빚은 **판이 끝날 때까지** 남는다. 웨이브가 끝나도, 야영을 해도 지워지지 않는다. 결투에서는 라운드가 바뀌어도 남는다. (WC 4.5·5.4) [제안]
3. 이후에 얻는 결정은 빚부터 갚는다. 별똥 결정도, 결투에서 라운드 시작에 받는 결정도 빚부터 갚는다. 웨이브 끝 환전은 빚을 갚고 남은 결정에만 한다. (WC 4.5·5.4, GDD 7) [제안]
4. 빚이 있는 동안 받는 피해 +1칸(적 공격의 한 번 3칸 상한 뒤에 더한다 → 최대 4칸). 결투에서는 +100이고 벌칙이 발동하는 피격에만 붙는다. (WC 4.5·5.4) [제안]
5. 빚이 있을 때 맞으면(결정이 0개이므로) 결정 파괴 대신 빚이 1개 늘어난다. 2개를 넘지 않는다. 결투에서는 "벌칙이 발동하면". (WC 4.5·5.4) [제안]
6. 빚이 2개면 외상으로 더 쓸 수 없다. 결정 0개·빚 1개에서 2개짜리 고유 비기를 누르면 부모 대체 규칙으로 양익 사격(1개)이 외상으로 나가고 빚이 2개가 된다. (WC 4.5) [제안]
7. 결정이 모자랄 때의 판정 순서(외상 먼저, 그다음 부모 기술)는 결투에서도 같다. (WC 5.4) [제안]
8. HUD: 빚 결정은 결정 칸에 보라 테두리의 빈 칸, "지금 K = 기술명(비용)" 옆에 "외상 N". (WC 4.5, GDD 13) [제안]

---

## 10. 재사용 대기와 K 보관

- 공통 재사용 대기 0.4초 = 6,144u(24틱). 대기가 끝나기 0.1초(1,536u, 6틱) 전부터 누른 K는 보관했다가 끝나는 순간 처리한다. (GDD 8 입력 규칙) [제안]
- 대기 시작 시각 = 판정 시각 `JudgeTime`(결투의 준비 2틱과 무관). [임시]
- 보관한 K는 **누른 시각**으로 선택과 결제를 끝내고(7.2~7.3) 커서를 비운다. 발동만 대기 끝 틱 `ceil(cooldownEnd / 256)`으로 미룬다. 그 사이에 들어온 방향 입력은 새 커맨드로 쌓인다. [임시]
- 보관 창 밖에서 대기 중에 누른 K는 아무 일도 하지 않는다(커서와 기록도 그대로). 보관은 1개뿐이고 보관 중의 추가 K는 무시한다. [임시]
- 연출: "지금 K" 표시의 테두리가 0.4초 동안 다시 차고, 보관된 K가 있으면 테두리 안에 점. (FX 12.1) → 21절 이벤트.

---

## 11. HUD 미리보기 "지금 K = 기술명(비용)"

- 같은 판정 함수의 결과를 매 프레임 표시한다. (GDD 8 판정기) → `Evaluate(now, wallet, unlocks, commit:false)`. `now`는 표시 시점의 게임 시각(서브틱). [제안]
- 섬 기술이 있는 자리에서는 두 줄로 보여 준다. → 고른 노드에 해금된 `JustArt`가 있으면 항상 두 줄: 윗줄 섬 기술(`Evaluate`를 `t = lastDirTime`으로 부른 결과), 아랫줄 일반 기술(섬 창을 넘긴 시각으로 부른 결과). [제안 / 두 줄 계산법 임시]
- 결정이 모자라 부모 기술로 내려갈 자리에서는 내려간 기술명을 보여 주고, 그믐은 "외상 N"을 붙인다. (GDD 8 판정기, WC 4.5) [제안]
- 불발이 될 자리는 기술명 대신 불발 표시. 만개 간격 초과가 확정된 자리(앞의 간격 셋 중 하나가 이미 0.18초를 넘음)는 "불발(n번째 입력이 x초 늦음)". [임시]
- 자세 밖에서는 항상 마력 방출(1). [제안: R-06]
- 표시 형식 예: 아이콘(I-T2) + "꿰뚫는 창" + 비용 "◆×2"(I-T4). (UI 3.5) 최근 입력 5개도 캐릭터 주변에 표시한다. (GDD 13) [제안]
- 화살표 표시: 커맨드가 이어지는 중이면 화살표 사이가 선으로 이어지고, 간격 초과로 끊기면 선이 끊어진다. (FX 12.1) → `cmd.dir_input`의 `chained`.

---

## 12. 공용 비기 10종

공통 표(GDD 8 "공용 비기 10종" [제안]):

| # | ArtId | 커맨드 | 난이도 | 결정 | 효과(원문) |
|---|---|---|---|---|---|
| 1 | ManaRelease | K | 1 | 1 | 트리 1회, 2T |
| 2 | RecoilShot | 뒤, K | 2 | 1 | 트리 1회 + 넉백 2칸, 나는 뒤로 2칸(벽 앞에서 멈춤). 돌진 예고 중인 일반 적(꼬리표 없는 적. 예: 덤불 늑대)은 돌진 취소 + 0.8초 경직. 엘리트·중량·보스의 돌진은 취소되지 않는다 |
| 3 | FanSpread | 앞, K | 2 | 1 | 트리를 세 갈래로 실행. 중앙 100%, 좌우 70% |
| 4 | TwinWing | 옆, K | 2 | 1 | 좌우 양쪽으로 트리 1회씩 |
| 5 | AfterimageStep | 옆, 옆, K | 3 | 2 | 누른 쪽으로 2.5칸 순간 이동(벽과 적 앞에서 멈춤), 0.15초 무적, 스쳐 피하기 1회만 인정. 같은 쪽 두 번만 인정 |
| 6 | PiercingLance | 앞, 앞, K | 3 | 2 | 3T를 한 줄 관통탄 1발로(폭 0.5, 사거리 12). 선돌지기의 철갑 무시. 연결 슬롯은 실행하지 않음. 속성은 코어 블록의 속성 |
| 7 | VentBurst | 뒤, 앞, K | 3 | 2 | 반경 3 폭발, 피해 = T × (열 ÷ 25), 주변 탄 소거, 열 0. 잠금 중에도 쓸 수 있고(열 100으로 계산) 잠금을 푼다. 속성은 코어 블록의 속성 |
| 8 | EightfoldSpread | 앞, 뒤, K | 3 | 2 | 8방향에 트리 1회씩. 정면 100%, 나머지 60% |
| 9 | ThunderLance | 앞, 앞, 섬 | 5 | 1 | 꿰뚫는 창의 저스트판. 6T 관통 + 1초 기절. 늦으면 꿰뚫는 창(2결정) |
| 10 | FullBloom | 앞, 뒤, 뒤, 앞, K | 5 | 전부(최소 3) | 팔방 전개를 결정 수만큼 0.12초 간격으로 반복, 각 2배, 주변 탄 소거. 외상으로는 쓸 수 없다 |

### 12.1 데이터 (`ArtDef`)

```csharp
public sealed class ArtDef {
    public ArtId Id; public string NameKey; public ArtKind Kind;   // Common, CommonSecret(비장), Unique
    public CharacterId? Owner;                                       // 고유만
    public CmdDir[][] Paths; public bool IsJust;
    public byte Difficulty;                                          // 1,2,3,5
    public CostKind CostKind; public byte Cost; public byte MinCost; // Fixed / AllMin3
    public bool CreditAllowed;                                       // 만개만 false
    public ArtId? FallbackParent;                                    // 고유만
    public UnlockRule Unlock;                                        // 15절
    public ArtEffectParams Effect;                                   // 12.2 / 14절
    public DuelOverride Duel;                                        // 16절
}
```

| ArtId | Kind | Diff | CostKind/Cost | Credit | FallbackParent | 시작 해금 | 해금 선행 | 최소 웨이브 |
|---|---|---|---|---|---|---|---|---|
| ManaRelease | Common | 1 | Fixed 1 | 예 | — | 예 | — | — |
| RecoilShot | Common | 2 | Fixed 1 | 예 | — | 예 | — | — |
| FanSpread | Common | 2 | Fixed 1 | 예 | — | 아니오 | (루트 = 마력 방출) | — |
| TwinWing | Common | 2 | Fixed 1 | 예 | — | 아니오 | (루트) | — |
| AfterimageStep | Common | 3 | Fixed 2 | 예 | — | 아니오 | TwinWing | — |
| PiercingLance | Common | 3 | Fixed 2 | 예 | — | 아니오 | FanSpread | — |
| VentBurst | Common | 3 | Fixed 2 | 예 | — | 아니오 | RecoilShot | — |
| EightfoldSpread | Common | 3 | Fixed 2 | 예 | — | 아니오 | FanSpread | — |
| ThunderLance | CommonSecret | 5 | Fixed 1 | 예 | — | 아니오 | PiercingLance | 4 |
| FullBloom | CommonSecret | 5 | AllMin3 (Min 3) | **아니오** | — | 아니오 | EightfoldSpread | 6 |
| Kiln | Unique | 3 | Fixed 2 | 예 | RecoilShot | **예** | 없음 | — |
| BoltDash | Unique | 3 | Fixed 2 | 예 | TwinWing | **예** | 없음 | — |
| Transcribe | Unique | 3 | Fixed 2 | 예 | FanSpread | **예** | 없음 | — |
| IceScreen | Unique | 3 | Fixed 2 | 예 | RecoilShot | **예** | 없음 | — |
| ShadowHand | Unique | 3 | Fixed 2 | 예 | TwinWing | **예** | 없음 | — |

("Credit"은 그믐의 외상 허용 여부. 출처: GDD 8 공용·고유 비기·해금, WC 3.5·4.5)

### 12.2 효과 파라미터 (공용)

`ArtEffectParams`는 기술마다 필드가 다르다. 실제 효과 실행은 다른 명세가 하지만 값은 여기서 정의한다.

**① ManaRelease** (GDD 8 [제안])
- `Execute(지팡이 트리, 내 위치, 시야 방향, 비기 배율 2, 열 면제 true, 아군)` 1회 → "2T".
- 연출 참고: 이번 트리의 탄이 1.25배 밝기 + 흰 중심(FX 12.2).

**② RecoilShot** (GDD 8, WC 3.6, MO 2.6 [제안])
- 트리 1회(비기 배율 1).
- 넉백 2칸(Q32.32 8,589,934,592). 꼬리표별: 일반 2칸 / 엘리트 1칸 / 중량 없음 / 보스 없음 / 뿌리 없음 / 비행 2칸. 결투 1.5칸.
- 넉백 대상: 이 비기로 실행한 트리의 탄에 맞은 적. [임시] (20절 U-17)
- 시전자는 시야 반대쪽으로 2칸 이동, 벽 앞에서 멈춤.
- 돌진 예고 중인 꼬리표 없는 일반 적(예: 덤불 늑대)을 맞히면 돌진 취소 + 0.8초(48틱) 경직. 엘리트(우두머리 늑대)·중량(멧돼지)·보스의 돌진 예고는 취소되지 않는다.

**③ FanSpread** (GDD 8 [제안])
- 트리를 세 갈래로 실행: 중앙 100%, 좌우 70%. 갈래 각도는 ±15도 [임시](GDD 4의 3갈래 분열 −15/0/+15도를 따름).

**④ TwinWing** (GDD 8 [제안])
- 좌우 양쪽(시야 기준 −90도, +90도)으로 트리 1회씩. 배율 각 100% [임시].

**⑤ AfterimageStep** (GDD 8 [제안])
- 마지막에 누른 물리 방향으로 2.5칸 순간 이동(10,737,418,240). 벽과 적 앞에서 멈춘다.
- 0.15초(9틱) 무적. 그 동안 스쳐 피하기는 1회만 인정.
- 피해 없음. 같은 쪽 두 번만 인정(반대쪽은 그림자 손 자리 또는 양익 사격).

**⑥ PiercingLance** (GDD 8, MO 2.x [제안])
- 피해 3T, 한 줄 관통탄 1발. 폭 0.5칸, 사거리 12칸. 연결 슬롯은 실행하지 않는다(트리를 실행하지 않고 T만 쓴다). 속성 = 코어 블록의 속성.
- 선돌지기의 철갑 무시.
- 판정 방식: 한 번에 뻗는 순간 판정(즉발 선분) [임시](FX 12.2 "한 번에 뻗고").

**⑦ VentBurst** (GDD 8 [제안])
- 시전자 중심 반경 3칸 폭발. 피해 = T × (열 ÷ 25). 잠금 중이면 열 100으로 계산(= 4T). 속성 = 코어 블록의 속성.
- 주변 탄 소거(반경 3칸 [임시]). 열 0. 잠금 해제.

**⑧ EightfoldSpread** (GDD 8 [제안])
- 8방향(45도 간격)으로 트리 1회씩. 정면 100%, 나머지 7방향 60%.

**⑨ ThunderLance** (GDD 8, WC 3.6, MO 3.2 [제안])
- 꿰뚫는 창과 같은 모양(폭 0.5, 사거리 12, 철갑 무시, 코어 속성)에 피해 6T + 1초(60틱) 기절.
- 기절 꼬리표별: 일반 1초 / 엘리트 0.5초 / 중량 없음 / 보스 0.3초 동작 멈춤 / 뿌리 1초 / 비행 1초. 결투 0.5초.

**⑩ FullBloom** (GDD 8, FX 12.2 [제안])
- 팔방 전개(정면 100%, 나머지 60%)를 반복 수(= 소모한 결정 수)만큼, 0.12초 간격으로, 각 2배(정면 200%, 나머지 120%).
- 최대 반복 = 결정 상한: 갈피 4회, 달구·초롱 5회, 성에 6회, 그믐 7회.
- 주변 탄 소거(마지막 파동에, 시전자 중심 반경 3칸 [임시]).
- 반복 틱: 판정 틱 + `round(i × 7.2)`, i = 0..N−1 → 0, 7, 14, 22, 29, 36, 43틱 [임시](20절 U-13).

조정 과제(참고): 결정 1개를 버는 사격량에 비해 비기 피해가 작으면 비기가 열 관리·탄 소거용으로만 쓰인다. 허수아비에서 "결정 1개당 피해"를 공용 10종과 고유 5종 모두 같은 표로 재서 배율을 올린다. (GDD 8, GDD 17) [제안]

---

## 13. 비기 효과 실행 순서

비기 발동 시 코어가 하는 일의 순서 [임시]:

1. `ArtDecision`이 Fire면 결정을 이미 소모했다(7.3).
2. 무한 웨이브: 판정 틱에 효과 시작. 결투: 판정 틱 + 준비 2틱(16절).
3. 기술별 준비 시간이 있는 경우 그만큼 더 늦춘다(20절 U-14의 임시값: ⑥ 5틱, ⑦ 4틱, ⑩ 15틱, 그 밖 0).
4. 효과 실행(12.2·14절). 피해 계산은 A-10, 그림자 계약은 `CrystalsAfter`.
5. 재사용 대기 시작(10절).

---

## 14. 고유 비기 5종

공통 (GDD 8 고유 비기, WC 3.5 [제안], 처음부터 사용은 [확정]):

- 전부 결정 2개, 난이도 3. 결정이 모자라면 부모 노드의 기술(결정 1개)이 나간다. 부모 기술도 낼 수 없으면 불발.
- **처음부터 쓸 수 있다.** 해금 차례를 기다리지 않고 새 프로필의 첫 판부터 나간다. 고유 비기에는 부모 기술 보유 조건을 적용하지 않는다. (DEC 1.23 [확정], GDD 8, WC 3.5)
- 공통 규칙(K로 끝남, 자세 중에만, 열 없음, 임계 없음, 강화 배율 적용, 공통 대기 0.4초)을 그대로 따른다. (WC 3.5)
- 블록을 거치지 않는 피해(불가마 지대, 벼락 질주, 병풍 파편, 그림자 손)의 속성은 캐릭터 속성이다. (GDD 10.1) 베껴 쓰기는 트리를 실행하므로 투사체 블록의 속성.
- 값 범위: "허수아비 한 마리에게 결정 2개로 주는 피해"로 재서 3T 이상 6T 이하. 아래쪽 기준 꿰뚫는 창(결정 2, 3T), 위쪽 기준 뇌광창 두 번(결정 2, 12T, 난이도 5)의 절반. 이동·방어·모으기 효과가 붙은 기술은 3T, 조건을 채워야 값이 다 나오는 기술은 6T 쪽. (WC 3.5)
- 이유(참고): 고유 비기의 커맨드가 급할 때 나오는 손버릇과 겹친다. 달구가 늑대 돌진을 끊으려고 뒤를 급히 두 번 치고 K를 눌렀을 때, 결정이 1개뿐이라고 반동 사격까지 불발하면 안 된다. (WC 3.5)

| 이름 | 캐릭터 | 커맨드 | 붙는 자리 | 입력 간격 | 결정이 모자라면 | 효과 요약(GDD) |
|---|---|---|---|---|---|---|
| 불가마 | 달구 | 뒤, 뒤, K | ② 노드의 "뒤" 자식 | 0.2초 | 반동 사격 | 발밑 반경 2.5 지대 4초. 지대 안의 적에게 총 2T(화염)와 감속 −20%. 지대 안에서 쏜 임계 발사의 열을 합계 16까지 대신 먹는다 |
| 벼락 질주 | 초롱 | 옆, 앞, K | ④ 노드의 "앞" 자식 | 0.2초 | 양익 사격 | 정면으로 4칸을 뚫고 지나간다. 길 위의 적에게 3T(번개) + 경직 0.6초, 이동 중 0.2초 무적 |
| 베껴 쓰기 | 갈피 | 앞, 옆, K | ③ 노드의 "옆" 자식 | 0.2초 | 부채 전개 | 사본 마법진 6초. 내가 J로 쏘면 마법진도 트리를 1회 실행(피해 50%, 열 없음). 0.3초에 1회, 최대 12회 |
| 얼음 병풍 | 성에 | 뒤, 옆, K | ② 노드의 "옆" 자식 | 0.3초 | 반동 사격 | 정면 1.5칸 앞에 폭 3칸·두께 0.5칸의 벽 4초. 곧게 오는 탄과 일반 적을 막는다. 사라질 때 벽 앞쪽 3×2칸에 파편 3T(냉기) |
| 그림자 손 | 그믐 | 옆, 반대쪽 옆, K | ④ 노드의 반대쪽 "옆" 자식 | 0.2초 | 양익 사격 | 정면 3칸 지점으로 반경 4.5 안의 적을 최대 3칸 끌어당긴다. 3T(암흑) + 경직 0.5초 |

(GDD 8, WC 3.5 표 — 두 표가 같다)

| 고유 비기 | 한 마리 기준 피해 | 붙은 효과 | 조건 |
|---|---|---|---|
| 불가마 | 지대 2T + 열 흡수로 약 3.6T = 5.6T | 감속 | 4초 동안 지대 안에 서서 임계 사격을 해야 한다 |
| 벼락 질주 | 3T | 4칸 이동, 0.2초 무적, 경직 | 없음 |
| 베껴 쓰기 | 12회 × 50% = 6T | 두 지점 교차 사격 | 사본이 12번 따라 쏠 때까지 내가 쏴야 한다 |
| 얼음 병풍 | 파편 3T | 4초 동안 탄과 적을 막는다 | 파편 범위에 적이 있어야 한다 |
| 그림자 손 | 3T | 모으기, 경직 | 없음 |

- 불가마의 3.6T는 마력탄 기준이다. 임계 사격 4발을 열 없이 쏘면 8T이고, 그 0.6초 동안 평소에도 4.4T(임계 유지 화력 73.5 × 0.6초)는 냈을 것이므로 순이득은 3.6T다. (WC 3.5) [제안]

### 14.1 Kiln (불가마, char_1) — WC 4.1 [제안]

| 파라미터 | 값 | 환산 |
|---|---|---|
| `ZoneRadius` | 반경 2.5칸 (지름 5칸, 그림 320px) | 10,737,418,240 |
| `Duration` | 4초 | 240틱 |
| `TickInterval` | 0.5초마다 | 30틱 (4초 동안 8회) |
| `TickDamage` | 0.25T (총 2T, 화염) | 비기 배율 0.25 × 8 |
| `SlowPercent` | 이동 속도 −20% (보스 −10%, 중량 그대로) | 20 |
| `HeatAbsorbTotal` | 합계 16 (마력탄이면 4발) | 정수 16 |
| `MaxConcurrent` | 동시에 1개 | 1 |

- 위치: 시전 순간의 내 발밑에 고정. [제안: "내 발밑에 깐다"]
- 내가 지대 안에서 쏘는 **임계 발사**는 열이 오르지 않는다. 임계인지는 평소처럼 "이번 발사의 열을 더했을 때 임계 시작값 이상인가"로 먼저 판정하고, 임계로 판정된 발사의 열만 먹는다. 16을 다 먹으면 그 뒤 발사는 평소대로 오른다. 발사 한 번의 열이 남은 양보다 크면 남은 만큼만 먹고 나머지는 오른다.
- 스쳐 피하기의 "다음 발사 열 면제"가 남아 있으면 그것을 먼저 쓴다(그 발사는 흡수량을 쓰지 않는다).
- 새로 깔면 이전 것은 사라지고 남은 열 흡수량도 이어받지 않는다.
- 예고 공격은 그대로 들어온다. 쓰는 법(참고): 열을 80 근처로 올린 뒤 깔면 열 없는 임계 4발 + 평소 임계 6발 = 10발 연속 임계 사격.
- 감속은 다른 감속(얼음 송곳, 서리, 살얼음의 감속 전환)과 더하지 않고 가장 큰 값 하나만. (WC 3.6, GDD 10.3)
- 결투: 상대 감속 −10%, 지대 피해(총 2T)와 열 흡수 16은 그대로, 상대 화면에도 지대가 보인다. (WC 5.4)

### 14.2 BoltDash (벼락 질주, char_2) — WC 4.2 [제안]

| 파라미터 | 값 | 환산 |
|---|---|---|
| `Distance` | 시야 정면 4칸 (그림 256px) | 17,179,869,184 |
| `PathWidth` | 지나간 길 폭 1.0칸 | 4,294,967,296 |
| `Damage` | 3T (번개) | 비기 배율 3 |
| `Stagger` | 0.6초 감전 경직 | 36틱 |
| `Invuln` | 이동 중 0.2초 무적 | 12틱 |

- 적을 뚫고 지나간다. 벽과 기둥 앞에서 멈춘다.
- 길 위의 적: 3T + 경직(꼬리표별 14.6절). 피해는 적마다 1회 [임시].
- 길 위에 있던 적 탄은 스쳐 피하기 1회로 인정한다(피뢰침 체질도 1회 발동).
- 이동 시간: 순간 효과(그림 0.2초 동안 줄기). 위치 이동은 판정 틱에 즉시 [임시].
- 결투: 3T 그대로, 경직 0.3초, 무적 0.2초 그대로, 상대를 뚫고 지나간다. 길 위의 상대 탄은 스침 1회로 치되 "1.0초에 1회" 제한 안에 든다. (WC 5.4)

### 14.3 Transcribe (베껴 쓰기, char_3) — WC 4.3 [제안]

| 파라미터 | 값 | 환산 |
|---|---|---|
| `Duration` | 6초 | 360틱 |
| `CopyDamagePercent` | 50% | 50 |
| `MinCopyInterval` | 0.3초에 한 번까지 | 18틱 |
| `MaxCopies` | 12번 (결투 8번 = 4T) | 12 / 8 |
| `MaxConcurrent` | 동시에 1개 | 1 |

- 시전 자리에 사본 마법진을 남긴다. 움직이지 않고 맞지 않는다.
- 내가 J로 쏘면 마법진도 **내 시야와 같은 방향으로** 트리를 1회 실행: `Execute(트리, 마법진 위치, 내 시야, 피해 50%, 열 면제 true, 아군)`. 열 없음, 콤보에 넣지 않는다.
- 0.3초에 한 번까지만 따라 쏘고 모두 12번 쏘면 6초가 남아 있어도 사라진다(마력탄이면 내 발사 2번에 1번꼴, 3.6초 만에 다 쓴다. 간격 0.5초 코어는 6초 동안 12번).
- 트리 실행 1회당 64개 상한은 사본에도 따로 적용하고, 화면 전체 300개 상한에는 함께 센다. 결투에서는 사본의 탄도 그 사람의 150개 상한과 스침 묶음(본체의 같은 발사와 한 묶음)에 센다. (WC 4.3, ON 4.2·4.3·4.5)
- 새로 깔면 이전 것은 사라지고 남은 횟수는 이어받지 않는다.
- 결투: 따라 쏘는 횟수 최대 8번(4T), 지속 6초·0.3초에 한 번·피해 50%는 그대로. (WC 5.4)

### 14.4 IceScreen (얼음 병풍, char_4) — WC 4.4 [제안]

| 파라미터 | 값 | 환산 |
|---|---|---|
| `Offset` | 시야 정면 1.5칸 앞 | 6,442,450,944 |
| `Width` × `Thickness` | 폭 3칸 × 두께 0.5칸 | 12,884,901,888 × 2,147,483,648 |
| `Duration` | 4초 | 240틱 |
| `ShardArea` | 벽 앞쪽(시전자 반대쪽) 폭 3칸 × 길이 2칸 | |
| `ShardDamage` | 3T (냉기), 한 번 (결투 2T) | 비기 배율 3 |
| `MaxConcurrent` | 동시에 1개 | 1 |
| 결투 `WallHp` | 150 | 정수 |

- 곧게 날아오는 적 탄을 막는다(막은 탄은 스쳐 피하기로 치지 않는다). 포물선 탄(씨앗 등)은 넘어간다.
- 일반 적과 엘리트는 지나가지 못한다. 비행은 넘어가지만 돌진으로 적힌 급강하·활강은 부딪힌다. 중량과 보스는 닿으면 부수고 계속 간다. 돌진하는 적이 부딪히면 벽이 깨지고 그 적은 1.5초(90틱) 경직(엘리트 0.75초 = 45틱, 중량·보스는 경직 없이 부수고 계속 달린다).
- 파편: 벽이 사라질 때(4초가 다 됐을 때와 부서졌을 때 모두) 1회.
- 내 탄은 통과한다. 벽 튕김 블록의 탄도 이 벽에는 튕기지 않고 지나간다.
- 방향: 자세 중 시야는 상하좌우뿐이므로 벽은 가로·세로 두 방향만 있다. (FX 20.4)
- 결투: 상대의 **탄만** 막는다. 상대 플레이어는 걸어서 지나갈 수 있다. 벽 체력 150, 상대 탄의 피해가 깎고 0이면 부서진다. 지속 4초 그대로, 파편 2T(사라질 때와 부서질 때 모두, 한 번). (WC 5.4)

### 14.5 ShadowHand (그림자 손, char_5) — WC 4.5 [제안]

| 파라미터 | 값 | 환산 |
|---|---|---|
| `CenterOffset` | 시야 정면 3칸 지점 | 12,884,901,888 |
| `PullRadius` | 중심에서 반경 4.5 | 19,327,352,832 |
| `PullDistance` | 최대 3칸 (결투 2칸) | 12,884,901,888 / 8,589,934,592 |
| `Damage` | 3T (암흑) | 비기 배율 3 |
| `Stagger` | 0.5초 (결투 없음) | 30틱 |
| 결투 `TelegraphToOpponent` | 발동 전 0.3초 동안 범위를 상대에게 보여 준다 | 18틱 |

- 반경 안의 적을 중심 쪽으로 최대 3칸 끌어당긴다(중심을 지나치지 않는다 [임시]).
- 적 탄은 움직이지 않는다.
- 닿으면 터지는 적(꽈리 벌레 등)은 끌려와도 스스로 터지는 조건이 바뀌지 않는다. (WC 3.6)
- 결투: 끌어당기는 거리 2칸, 경직 없음, 3T 그대로, 발동 전 0.3초 범위 표시. (WC 5.4)

### 14.6 꼬리표별 반응 (비기 관련 줄만, WC 3.6 [제안])

| 효과 | 일반 | 엘리트 | 중량 | 보스 | 뿌리 | 비행 |
|---|---|---|---|---|---|---|
| ② 반동 사격 넉백 2칸 | 2칸 | 1칸 | 없음 | 없음 | 없음 | 2칸 |
| ⑨ 뇌광창 기절 1초 | 1초 | 0.5초 | 없음 | 0.3초 동작 멈춤 | 1초 | 1초 |
| 불가마 감속 −20% | −20% | −20% | −20% | −10% | 해당 없음 | −20% |
| 벼락 질주 경직 0.6초 | 0.6초 | 0.3초 | 없음 | 없음 | 0.6초 | 0.6초 |
| 얼음 병풍 통과 | 못 지나감 | 못 지나감 | 닿으면 부숨 | 닿으면 부숨 | 해당 없음 | 넘어감(돌진으로 적힌 급강하·활강은 부딪힘) |
| 그림자 손 당김 3칸, 경직 0.5초 | 3칸, 0.5초 | 1.5칸, 0.25초 | 없음 | 없음 | 당김 없음, 경직 0.5초 | 3칸, 0.5초 |

공통: 피해는 누구에게나 그대로. 엘리트는 밀리고 끌리는 거리 절반, 빙결·경직·기절 시간 절반, 감속은 그대로, 예고 중인 공격은 취소되지 않는다. 꼬리표가 겹치면 효과마다 더 강한 면역 쪽(엘리트+중량은 중량 줄). 땅속에 있는 동안은 어떤 효과도 받지 않는다(피해 포함). ② 반동 사격의 돌진 예고 취소는 꼬리표 없는 일반 적의 돌진에만. 어느 적이 어느 꼬리표인지는 `MO 2.6`. (WC 3.6, MO 2.6·3.2)

---

## 15. 해금

### 15.1 규칙

| ID | 규칙 | 출처 |
|---|---|---|
| U-R1 | 비기 해금은 **캐릭터별로** 남는다. 한 번 해금하면 그 캐릭터로는 다음 판에도 쓴다(어느 지팡이를 들든 같다). | DEC 2.8 [조율], GDD 8 [조율], ON 1.1 |
| U-R2 | 시작 해금: 마력 방출, 반동 사격, 그 캐릭터의 고유 비기(3종). 고유 비기를 처음부터 주는 것은 확정. | GDD 8 [확정 부분], DEC 1.23 [확정], ON 1.1 |
| U-R3 | 해금 차례: 웨이브 1~6을 넘길 때마다, 그 캐릭터가 아직 해금하지 못한 공용 비기 중 후보 2개에서 1개를 무료로 고른다. | GDD 8 [제안] |
| U-R4 | 고르는 자리는 야영의 비기 탭(트라이 그림이 곧 커맨드 목록이자 해금 화면). | GDD 8·13 [제안], MO 4.6 |
| U-R5 | 공용 8종(①② 제외)을 다 해금하면 차례는 더 오지 않는다(두 판이면 다 모인다). | GDD 8 [제안] |
| U-R6 | 시작 웨이브를 골라 1~6을 건너뛴 판에서는 출발 야영에서 차례대로 고른다. | GDD 8 [제안], MO 4.7 |
| U-R7 | 부모 기술이 있어야 후보가 된다. 후보가 1개뿐이면 1개만 보여 준다. | GDD 8 [제안], UI 4.13 |
| U-R8 | 뇌광창은 꿰뚫는 창 보유 + 웨이브 4 이후, 만개는 팔방 전개 보유 + 웨이브 6 이후. | GDD 8 [제안] |
| U-R9 | 결투에서는 해금과 무관하게 공용 10 + 고유 1을 전부 쓴다. | GDD 8 [제안], ON 4.1 (GDD 개발자 확인 9, ON 개발자 확인 4: 지금 값 유지) |
| U-R10 | 오프라인 프로필과 서버 프로필은 따로이고 섞이지 않는다. 해금도 프로필마다 따로다. 오프라인 진행은 서버로 옮길 수 없다. | DEC 2.6 [조율] |
| U-R11 | 지팡이 거래로 해금은 넘어가지 않는다(캐릭터에 묶인 것). | ON 1.6·5.5 표 |
| U-R12 | 서버 DB: `character_state(account_id, character, equipped_wand_id, unlocked_arts, best_score, best_wave)`, 계정당 5행. 비기 해금이 `unlocked_arts`에 남는다. | ON 9(DB 표) |
| U-R13 | 고유 비기 노드는 비기 탭에서 새 프로필의 첫 화면에서도 "해금" 상태로 그린다. "잠김"과 "후보"는 공용 비기에만 쓰인다. 아직 해금하지 않은 비기는 회청 단색 실루엣 + 자물쇠. | UI 4.13·3.5 |
| U-R14 | 정산 화면의 "가져가는 것" 목록에 새로 해금한 비기를 보여 준다. | UI 4.x(정산) |
| U-R15 | 스승의 허상(보스) 2단계는 플레이어가 **해금한** 비기 중 ③④⑧을 따라 쓰고(해금한 것이 없으면 ①만), 허상과의 첫 만남은 해금과 무관하게 셋 다 쓴다. → 판정기는 `UnlockSet` 조회를 보스 AI에 내준다. | MO 3.2·3.9 |

### 15.2 데이터

```csharp
[Flags] public enum ArtMask : ushort { /* bit = (int)ArtId */ }
public sealed class UnlockSet {               // 판 중 사용
    ArtMask commonUnlocked;                   // 프로필에서 복사 + 판 중 고른 것
    CharacterId character;
    bool duelAll;                             // 결투: 전부 true
    public bool IsUnlocked(ArtId a) =>
        duelAll || a == ManaRelease || a == RecoilShot
        || (IsUnique(a) && OwnerOf(a) == character)
        || commonUnlocked.Has(a);
}
public sealed class UnlockTurn { byte waveIndex; ArtId[] candidates; ArtId? picked; }   // 판 상태
```
- 저장 형식: 프로필(오프라인 파일, 서버 `unlocked_arts`)에는 공용 비기 문자열 id 배열(예: `["art.fan_spread","art.piercing_lance"]`)로 둔다. 시작 3종은 저장하지 않아도 항상 참이다. [임시]

### 15.3 차례와 후보 알고리즘

```
OnWaveCleared(w):                  // 웨이브 w를 넘겼을 때(적 전멸로 끝남)
  if 1 <= w <= 6: pendingTurns.Enqueue(w)

OnDepartureCamp(startWave):        // 출발 야영
  for w in 1 .. min(startWave - 1, 6): pendingTurns.Enqueue(w)

OfferNextTurn():                   // 야영의 비기 탭에서, 차례마다 하나씩
  w = pendingTurns.Dequeue()
  eligible = [a in CommonArts(8종: ③~⑩)
              where !unlocked(a)
                and PrereqOwned(a)            // 부모 기술 보유
                and w >= a.MinWave]           // 뇌광창 4, 만개 6
  if eligible.Count == 0: return (차례 소모) [임시]
  candidates = eligible.Count == 1 ? eligible : DrawTwo(eligible, rng = runSeed.Fork("art_unlock"))
  show candidates; on pick a: unlocked += a (즉시 이 판에 적용)
```

- 부모 기술(선행): ③④ = 마력 방출(항상 보유), ⑤ = ④, ⑥ = ③, ⑦ = ②(항상 보유), ⑧ = ③, ⑨ = ⑥, ⑩ = ⑧. (GDD 8 트리 + U-R8)
- 차례 번호 = 넘긴 웨이브 번호. 건너뛴 차례도 그 웨이브 번호로 조건을 본다(시작 웨이브 11이면 출발 야영에서 1~6번 차례를 차례대로, 4번 차례부터 뇌광창 후보, 6번 차례에 만개 후보). [임시]
- 같은 야영에 차례가 여러 개면 하나를 고른 뒤 다음 차례를 보여 준다. 앞 차례에서 고른 것이 뒤 차례의 선행 조건을 채울 수 있다. [임시]
- 후보 2개 뽑기는 판 씨앗에서 갈라 낸 난수기로 균등하게(결정론, 서버 재실행 검증). [임시]
- 야영을 떠나기 전에 남은 차례는 모두 골라야 한다(출발 버튼 전에 고르는 판을 띄운다). [임시]
- 두 판으로 다 모이는가(검산): 1판에서 6종, 2판에서 나머지 2종. 최악(1판에서 ③④⑤⑥⑦⑧): 2판 1~3번 차례 후보 0, 4번 차례 ⑨, 5번 0, 6번 ⑩ → 2판에 다 모인다. (GDD 8 "두 판이면 다 모인다"와 맞음)

### 15.4 프로필 반영 [임시]

- 판 안: 고른 즉시 `UnlockSet`에 들어가고 판정기가 바로 쓴다.
- 오프라인 프로필: 고른 즉시 프로필에 저장한다.
- 서버 프로필: 고르기는 판의 입력 기록에 남는 사건이다. 서버는 판 기록을 다시 돌려 검증할 때(중간 지점의 "여기까지는 인정" 또는 종료 요약의 1차 검사 통과) 그 지점까지 고른 해금을 `unlocked_arts`에 반영한다. (ON 3.2의 판 제출 절차 위에 얹은 임시안)

---

## 16. 결투에서 달라지는 것

| 항목 | 무한 웨이브 | 결투 | 출처 |
|---|---|---|---|
| 쓸 수 있는 비기 | 해금한 것 + 고유 | 공용 10 + 고유 1 전부 | ON 4.1, GDD 8 [제안] |
| 시작 결정 | 판 시작 0 | 라운드마다 1개로 다시 시작(그믐은 빚부터 갚는다) | ON 4.1, WC 5.4 [제안] |
| 비기 발동 | 판정 즉시 | 판정 뒤 **준비 2틱**을 거쳐 발동(고유 비기도). 문양과 소리는 발동 틱이 서버 확정을 받았을 때 낸다 | ON 4.2·4.5·4.11 [제안] |
| 상태를 바꾸는 효과(기절, 넉백, 감속, 결정 파괴, 당김, 경직) | 즉시 | 적중 3틱 뒤 적용 | ON 4.2·4.5·4.11 [제안] |
| 강화 배율 | 1.15^단계 | `1 + 0.03 × 단계` | ON 4.2 [제안] |
| 속성 약점·저항 | 있음 | 없음(고유 비기·패시브 피해도) | ON 4.2·4.5 [제안] |
| 순간 피해 상한 | 없음 | 0.5초 동안 최대 체력의 30%(비기 피해도 포함) | ON 4.2·4.5 [제안] |
| 뇌광창 기절 | 1초 | 0.5초 | ON 4.2 [제안] |
| 반동 사격 넉백 | 2칸 | 1.5칸 | ON 4.2 [제안] |
| 방열 폭발·만개 탄 소거 | 주변 탄 | 동일(상대 탄을 지운다) | ON 4.2 [제안] |
| 조준 보정 | 없음 | J 발사와 **방향이 있는 비기**는 상대 쪽으로 최대 10도. 3갈래·부채·팔방의 곁가지 탄은 보정 없음(가운데 탄만). 내 발밑이나 내 둘레에 생기는 것(불가마, 방열 폭발 등)에는 없다 | GDD 3, ON 4.4·4.5 [제안] |
| 탄 상한 | 플레이어 300 | 한 사람당 150(사본 포함) | ON 4.2 [제안] |
| 고유 비기 값 | 14절 | 14절 각 항의 "결투" 줄(WC 5.4가 유일한 기준) | WC 5.4 [제안] |
| 커맨드 해석 | — | **되감기지 않는다.** 내 입력의 시각과 순서만 보고 정해지고, 입력이 서버에 제때 닿으면 바뀌지 않는다 | ON 4.11 [제안] |
| 발동 취소 | — | 상대 입력이 늦게 확정되어 그 사이에 내가 기절하거나 밀려났다면 나간 것처럼 보였던 비기가 취소될 수 있다. 취소되면 문양이 0.1초 동안 흐려지며 사라진다 | ON 4.11, FX 17 [제안] |
| 섬과 지연 | — | 섬은 내 입력 두 개의 간격이라 지연의 크기와 무관. 서버 왕복 120ms 이하, 흔들림이 마감 여유 안이면 오프라인과 같다. 서버 도착 시각으로 판정하지 않는다 | ON 4.12 [제안] |

- 입력 지연 D: 서버 왕복 60ms 이하 2틱, 90ms 이하 3틱, 120ms 이하 4틱(초과 4틱 고정). 내가 틱 t에 누른 키는 유효 틱 t + D에 적용되고 틱 안 오프셋은 보존한다. 판정기는 유효 틱 기준 시각을 쓴다. (ON 4.11) [제안]
- 마감 여유 = `(D + 1)틱 − 편도 지연`. 여유를 넘긴 입력은 버려지고(다른 틱으로 옮기지 않는다) 비기가 안 나간다. 표시: 마감 여유 초록(15ms 이상)/노랑/호박색(5ms 미만). "입력이 회선 때문에 버려짐" 표시. (ON 4.11·4.12, GDD 13) [제안]
- 서버의 입력 검증(순서, 같은 오프셋 방향 → K 재정렬, 늦은·이른 시각, 키 상태, 틱당 8개). (ON 4.11) [제안]
- 매크로 탐지: 섬 성공률이 100%에 가깝고 오프셋 분포가 한 점에 몰린 계정을 검토 대상으로 표시(자동 제재 아님). (ON 10) [제안] → 판정기는 `IsJust`와 `t_K − lastDirTime`을 기록 통계로 내준다.
- 준비 2틱 동안 내가 기절·경직되면 그 비기는 취소된다. 결정은 돌려준다. 재사용 대기는 시작하지 않는다. [임시] (20절 U-48)

---

## 17. 경계 조건 모음

1. 간격이 한도와 **정확히 같으면 통과**(0.07초 = 1,075u는 섬, 0.18초 = 2,765u는 만개 통과, 0.3초 = 4,608u는 커맨드 유지). TV-A096, A102, A088.
2. K가 마지막 방향 입력과 같은 시각(간격 0)이면 섬이다(방향이 먼저 처리되므로). TV-A095, TV-B01.
3. 자세 밖 K는 커서와 무관하게 마력 방출. TV-B04.
4. 루트에서 K: 마력 방출(결정 1). 결정 0이면 불발, 그믐은 외상(빚 1). TV-A090, A091, A077.
5. 만개: 해금 + 커서 FBBF + 간격 초과 → 불발(실패 링크 안 탐, 결정 소모 없음). 해금 + 간격 통과 + 결정 < 3 → 불발(더 싼 기술로 안 바뀜). 미해금 → 실패 링크로 방열 폭발(또는 그 아래). TV-A037~A053.
6. 만개 뒤에 방향을 하나 더 넣으면(앞,뒤,뒤,앞,앞) 커서는 FF → 꿰뚫는 창. TV-A103.
7. 고유 비기 부모 대체는 한 번만. 부모도 못 내면 불발. TV-A058, A074.
8. 공용 비기는 결정이 모자라도 부모로 내려가지 않는다(꿰뚫는 창 결정 1 → 불발). TV-A092.
9. 뇌광창(결정 1)은 꿰뚫는 창(결정 2)보다 싸다. 결정 1개로 섬을 넣으면 뇌광창이 나간다. TV-A093.
10. 실패 링크로 내려온 노드는 그 노드 경로의 간격만 본다. 앞 입력이 늦었어도 접미사가 맞으면 통과. TV-A116.
11. 커서는 최근 8개 방향 입력만 기록하지만 최대 깊이 4라 결과에 영향이 없다. TV-A117.
12. 같은 키를 누른 채 있는 것은 입력이 아니다. 키보드 자동 반복(누름 반복 이벤트)도 입력이 아니다. TV-B02. [제안 + 자동 반복은 임시]
13. 해금 안 된 노드에서 K: 해금된 가장 긴 접미사 기술. 시작 상태(①②+고유)에서 앞,앞,K → 마력 방출. TV-A106.
14. 그믐 결정 2개·빚 0에서 만개 → 불발(외상 불가). TV-A080.
15. 결정 상한보다 많은 결정은 존재하지 않는다(테스트 입력도 상한 이하로 준다).
16. 비기 한 번 160개 상한을 넘는 만개(예: 7회 × 8방향 × 트리 투사체 다수)는 20절 U-19의 처리.

---

## 18. 테스트 벡터

형식: 각 행이 `given`(캐릭터, 해금, 결정/빚, 자세 중) / `when`(입력열) / `then`(판정 결과, 결정/빚)이다. 테스트 하네스 규칙:

- `given` 공통: 자세 중(Shift 누른 채, 입력열 시작 전에 이미 자세에 들어가 있음), 재사용 대기 없음, 커서 루트, 무한 웨이브 모드.
- 입력열 `X+g s(u)`: 직전 입력에서 g초 뒤(서브틱 u = round(g × 15360))에 X를 누른다. 첫 입력 시각은 임의(PY는 100,000u). 이벤트의 틱 = 시각 ÷ 256, 오프셋 = 시각 mod 256.
- F/B/L/R = 상대 방향 앞/뒤/좌/우(키 → 상대 방향 변환을 거치지 않고 판정기에 바로 넣는다). K = 비기 키.
- 해금 "전부" = 공용 10 + 고유. `{…} + 고유` = 그 공용 비기 + 그 캐릭터의 고유 비기.
- `then`의 결정/빚은 판정 직후의 값. `Fire(X, via_fallback)` = 고유 비기 대신 부모 기술이 나감.
- A군: `docs/sim/gdd_parser_check.py`의 규칙으로 실제로 돌린 결과(GDD 8 입력열 표의 모든 행, PY의 모든 행, "전원"과 "그 밖" 행은 다섯 캐릭터로 펼침, 이 명세가 더한 파생 행). B군: PY가 다루지 않는 규칙(자세, 이벤트 순서, 재사용 대기, 미리보기, 해금, 결투)을 손으로 정한 것. S군: 트라이 구조.

### 18.1 A군 — 입력열 판정 (117건)

| ID | 출처 | 캐릭터 | 해금 | 결정 / 빚 (given) | 입력열 (when) | 결과 (then) | 결정 / 빚 (then) |
|---|---|---|---|---|---|---|---|
| TV-A001 | GDD8 표1 / py | char_1(달구) | 전부 | 5 / 0 | F, F+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(PiercingLance) | 3 / 0 |
| TV-A002 | GDD8 표1 / py | char_2(초롱) | 전부 | 5 / 0 | F, F+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(PiercingLance) | 3 / 0 |
| TV-A003 | GDD8 표1 / py | char_3(갈피) | 전부 | 4 / 0 | F, F+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(PiercingLance) | 2 / 0 |
| TV-A004 | GDD8 표1 / py | char_4(성에) | 전부 | 6 / 0 | F, F+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(PiercingLance) | 4 / 0 |
| TV-A005 | GDD8 표1 / py | char_5(그믐) | 전부 | 7 / 0 | F, F+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(PiercingLance) | 5 / 0 |
| TV-A006 | GDD8 표2 / py | char_3(갈피) | 전부 | 4 / 0 | F, F+0.15s(2304), L+0.15s(2304), K+0.15s(2304) | Fire(Transcribe) | 2 / 0 |
| TV-A007 | GDD8 표2' (우) | char_3(갈피) | 전부 | 4 / 0 | F, F+0.15s(2304), R+0.15s(2304), K+0.15s(2304) | Fire(Transcribe) | 2 / 0 |
| TV-A008 | GDD8 표3 / py | char_1(달구) | 전부 | 5 / 0 | B, B+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(VentBurst) | 3 / 0 |
| TV-A009 | GDD8 표4 / py | char_5(그믐) | 전부 | 7 / 0 | L, R+0.15s(2304), R+0.15s(2304), K+0.15s(2304) | Fire(AfterimageStep) | 5 / 0 |
| TV-A010 | GDD8 표4' (거울) | char_5(그믐) | 전부 | 7 / 0 | R, L+0.15s(2304), L+0.15s(2304), K+0.15s(2304) | Fire(AfterimageStep) | 5 / 0 |
| TV-A011 | GDD8 표5 / py | char_1(달구) | 전부 | 5 / 0 | F, B+0.15s(2304), B+0.15s(2304), K+0.15s(2304) | Fire(Kiln) | 3 / 0 |
| TV-A012 | GDD8 표5 / py | char_2(초롱) | 전부 | 5 / 0 | F, B+0.15s(2304), B+0.15s(2304), K+0.15s(2304) | Fire(RecoilShot) | 4 / 0 |
| TV-A013 | GDD8 표5 / py | char_3(갈피) | 전부 | 4 / 0 | F, B+0.15s(2304), B+0.15s(2304), K+0.15s(2304) | Fire(RecoilShot) | 3 / 0 |
| TV-A014 | GDD8 표5 / py | char_4(성에) | 전부 | 6 / 0 | F, B+0.15s(2304), B+0.15s(2304), K+0.15s(2304) | Fire(RecoilShot) | 5 / 0 |
| TV-A015 | GDD8 표5 / py | char_5(그믐) | 전부 | 7 / 0 | F, B+0.15s(2304), B+0.15s(2304), K+0.15s(2304) | Fire(RecoilShot) | 6 / 0 |
| TV-A016 | GDD8 표6 / py | char_1(달구) | 전부 | 1 / 0 | B, B+0.15s(2304), K+0.15s(2304) | Fire(RecoilShot, via_fallback) | 0 / 0 |
| TV-A017 | GDD8 표7 / py | char_5(그믐) | 전부 | 7 / 0 | L, R+0.19s(2918), K+0.15s(2304) | Fire(ShadowHand) | 5 / 0 |
| TV-A018 | GDD8 표7 / py | char_5(그믐) | 전부 | 7 / 0 | L, R+0.22s(3379), K+0.15s(2304) | Fire(TwinWing) | 6 / 0 |
| TV-A019 | GDD8 표8 / py | char_2(초롱) | 전부 | 5 / 0 | L, F+0.22s(3379), K+0.15s(2304) | Fire(FanSpread) | 4 / 0 |
| TV-A020 | GDD8 표9 / py | char_5(그믐) | 전부 | 0 / 1 | L, R+0.15s(2304), K+0.15s(2304) | Fire(TwinWing, via_fallback) | 0 / 2 |
| TV-A021 | GDD8 표11 / py | char_1(달구) | 전부 | 5 / 0 | F, B+0.19s(2918), K+0.15s(2304) | Fire(EightfoldSpread) | 3 / 0 |
| TV-A022 | GDD8 표11 / py | char_1(달구) | 전부 | 5 / 0 | F, B+0.22s(3379), K+0.15s(2304) | Fire(RecoilShot) | 4 / 0 |
| TV-A023 | GDD8 표11 / py | char_2(초롱) | 전부 | 5 / 0 | F, B+0.19s(2918), K+0.15s(2304) | Fire(EightfoldSpread) | 3 / 0 |
| TV-A024 | GDD8 표11 / py | char_2(초롱) | 전부 | 5 / 0 | F, B+0.22s(3379), K+0.15s(2304) | Fire(RecoilShot) | 4 / 0 |
| TV-A025 | GDD8 표11 / py | char_3(갈피) | 전부 | 4 / 0 | F, B+0.19s(2918), K+0.15s(2304) | Fire(EightfoldSpread) | 2 / 0 |
| TV-A026 | GDD8 표11 / py | char_3(갈피) | 전부 | 4 / 0 | F, B+0.22s(3379), K+0.15s(2304) | Fire(RecoilShot) | 3 / 0 |
| TV-A027 | GDD8 표11 / py | char_4(성에) | 전부 | 6 / 0 | F, B+0.19s(2918), K+0.15s(2304) | Fire(EightfoldSpread) | 4 / 0 |
| TV-A028 | GDD8 표11 / py | char_4(성에) | 전부 | 6 / 0 | F, B+0.22s(3379), K+0.15s(2304) | Fire(RecoilShot) | 5 / 0 |
| TV-A029 | GDD8 표11 / py | char_5(그믐) | 전부 | 7 / 0 | F, B+0.19s(2918), K+0.15s(2304) | Fire(EightfoldSpread) | 5 / 0 |
| TV-A030 | GDD8 표11 / py | char_5(그믐) | 전부 | 7 / 0 | F, B+0.22s(3379), K+0.15s(2304) | Fire(RecoilShot) | 6 / 0 |
| TV-A031 | py 팔방 0.17 | char_1(달구) | 전부 | 5 / 0 | F, B+0.17s(2611), K+0.15s(2304) | Fire(EightfoldSpread) | 3 / 0 |
| TV-A032 | GDD8 표12 / py | char_1(달구) | 전부 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(FullBloom) | 0 / 0 |
| TV-A033 | GDD8 표12 / py | char_2(초롱) | 전부 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(FullBloom) | 0 / 0 |
| TV-A034 | GDD8 표12 / py | char_3(갈피) | 전부 | 4 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(FullBloom) | 0 / 0 |
| TV-A035 | GDD8 표12 / py | char_4(성에) | 전부 | 6 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(FullBloom) | 0 / 0 |
| TV-A036 | GDD8 표12 / py | char_5(그믐) | 전부 | 7 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(FullBloom) | 0 / 0 |
| TV-A037 | GDD8 표13 / py | char_1(달구) | 전부 | 5 / 0 | F, B+0.19s(2918), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fizzle(FullBloomLate, late_index=2, late_by=153u), 표시 "2번째 입력이 0.01초 늦음" | 5 / 0 |
| TV-A038 | GDD8 표13 / py | char_2(초롱) | 전부 | 5 / 0 | F, B+0.19s(2918), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fizzle(FullBloomLate, late_index=2, late_by=153u), 표시 "2번째 입력이 0.01초 늦음" | 5 / 0 |
| TV-A039 | GDD8 표13 / py | char_3(갈피) | 전부 | 4 / 0 | F, B+0.19s(2918), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fizzle(FullBloomLate, late_index=2, late_by=153u), 표시 "2번째 입력이 0.01초 늦음" | 4 / 0 |
| TV-A040 | GDD8 표13 / py | char_4(성에) | 전부 | 6 / 0 | F, B+0.19s(2918), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fizzle(FullBloomLate, late_index=2, late_by=153u), 표시 "2번째 입력이 0.01초 늦음" | 6 / 0 |
| TV-A041 | GDD8 표13 / py | char_5(그믐) | 전부 | 7 / 0 | F, B+0.19s(2918), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fizzle(FullBloomLate, late_index=2, late_by=153u), 표시 "2번째 입력이 0.01초 늦음" | 7 / 0 |
| TV-A042 | GDD8 표14 / py | char_1(달구) | 전부 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.25s(3840) | Fizzle(FullBloomLate, late_index=5, late_by=1075u), 표시 "5번째 입력이 0.07초 늦음" | 5 / 0 |
| TV-A043 | GDD8 표14 / py | char_2(초롱) | 전부 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.25s(3840) | Fizzle(FullBloomLate, late_index=5, late_by=1075u), 표시 "5번째 입력이 0.07초 늦음" | 5 / 0 |
| TV-A044 | GDD8 표14 / py | char_3(갈피) | 전부 | 4 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.25s(3840) | Fizzle(FullBloomLate, late_index=5, late_by=1075u), 표시 "5번째 입력이 0.07초 늦음" | 4 / 0 |
| TV-A045 | GDD8 표14 / py | char_4(성에) | 전부 | 6 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.25s(3840) | Fizzle(FullBloomLate, late_index=5, late_by=1075u), 표시 "5번째 입력이 0.07초 늦음" | 6 / 0 |
| TV-A046 | GDD8 표14 / py | char_5(그믐) | 전부 | 7 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.25s(3840) | Fizzle(FullBloomLate, late_index=5, late_by=1075u), 표시 "5번째 입력이 0.07초 늦음" | 7 / 0 |
| TV-A047 | GDD8 표15 / py | char_1(달구) | {ManaRelease, RecoilShot, VentBurst, FanSpread, EightfoldSpread} + 고유 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(VentBurst) | 3 / 0 |
| TV-A048 | GDD8 표15 / py | char_2(초롱) | {ManaRelease, RecoilShot, VentBurst, FanSpread, EightfoldSpread} + 고유 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(VentBurst) | 3 / 0 |
| TV-A049 | GDD8 표15 / py | char_3(갈피) | {ManaRelease, RecoilShot, VentBurst, FanSpread, EightfoldSpread} + 고유 | 4 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(VentBurst) | 2 / 0 |
| TV-A050 | GDD8 표15 / py | char_4(성에) | {ManaRelease, RecoilShot, VentBurst, FanSpread, EightfoldSpread} + 고유 | 6 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(VentBurst) | 4 / 0 |
| TV-A051 | GDD8 표15 / py | char_5(그믐) | {ManaRelease, RecoilShot, VentBurst, FanSpread, EightfoldSpread} + 고유 | 7 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(VentBurst) | 5 / 0 |
| TV-A052 | py 만개 미해금·방열 미해금 | char_1(달구) | {ManaRelease, RecoilShot, FanSpread} + 고유 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(FanSpread) | 4 / 0 |
| TV-A053 | py 만개 결정 2 | char_1(달구) | 전부 | 2 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fizzle(NoCrystal) | 2 / 0 |
| TV-A054 | py 섬 | char_1(달구) | 전부 | 5 / 0 | F, F+0.15s(2304), K+0.06s(922) | Fire(ThunderLance) | 4 / 0 |
| TV-A055 | py 섬 늦음 | char_1(달구) | 전부 | 5 / 0 | F, F+0.15s(2304), K+0.09s(1382) | Fire(PiercingLance) | 3 / 0 |
| TV-A056 | 파생 | char_1(달구) | 전부 | 5 / 0 | B, B+0.22s(3379), K+0.15s(2304) | Fire(RecoilShot) | 4 / 0 |
| TV-A057 | 파생 | char_1(달구) | 전부 | 5 / 0 | B, B+0.2s(3072), K+0.15s(2304) | Fire(Kiln) | 3 / 0 |
| TV-A058 | 파생 | char_1(달구) | 전부 | 0 / 0 | B, B+0.15s(2304), K+0.15s(2304) | Fizzle(NoCrystal) | 0 / 0 |
| TV-A059 | 파생 | char_1(달구) | 전부 | 5 / 0 | B, B+0.15s(2304), B+0.15s(2304), K+0.15s(2304) | Fire(Kiln) | 3 / 0 |
| TV-A060 | 파생 | char_2(초롱) | 전부 | 5 / 0 | L, F+0.15s(2304), K+0.15s(2304) | Fire(BoltDash) | 3 / 0 |
| TV-A061 | 파생 | char_2(초롱) | 전부 | 5 / 0 | R, F+0.2s(3072), K+0.15s(2304) | Fire(BoltDash) | 3 / 0 |
| TV-A062 | 파생 | char_2(초롱) | 전부 | 1 / 0 | L, F+0.15s(2304), K+0.15s(2304) | Fire(TwinWing, via_fallback) | 0 / 0 |
| TV-A063 | 파생 | char_3(갈피) | 전부 | 1 / 0 | F, L+0.15s(2304), K+0.15s(2304) | Fire(FanSpread, via_fallback) | 0 / 0 |
| TV-A064 | 파생 | char_3(갈피) | 전부 | 4 / 0 | F, L+0.22s(3379), K+0.15s(2304) | Fire(TwinWing) | 3 / 0 |
| TV-A065 | 파생 | char_3(갈피) | 전부 | 4 / 0 | F, R+0.19s(2918), K+0.15s(2304) | Fire(Transcribe) | 2 / 0 |
| TV-A066 | 파생 | char_3(갈피) | 전부 | 4 / 0 | F, L+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(FanSpread) | 3 / 0 |
| TV-A067 | 파생 | char_4(성에) | 전부 | 6 / 0 | B, L+0.15s(2304), K+0.15s(2304) | Fire(IceScreen) | 4 / 0 |
| TV-A068 | 파생 | char_4(성에) | 전부 | 6 / 0 | B, L+0.29s(4454), K+0.15s(2304) | Fire(IceScreen) | 4 / 0 |
| TV-A069 | 파생 | char_4(성에) | 전부 | 6 / 0 | B, L+0.31s(4762), K+0.15s(2304) | Fire(TwinWing) | 5 / 0 |
| TV-A070 | 파생 | char_4(성에) | 전부 | 1 / 0 | B, R+0.15s(2304), K+0.15s(2304) | Fire(RecoilShot, via_fallback) | 0 / 0 |
| TV-A071 | 파생 | char_5(그믐) | 전부 | 7 / 0 | R, L+0.15s(2304), K+0.15s(2304) | Fire(ShadowHand) | 5 / 0 |
| TV-A072 | 파생 | char_5(그믐) | 전부 | 1 / 0 | L, R+0.15s(2304), K+0.15s(2304) | Fire(ShadowHand) | 0 / 1 |
| TV-A073 | 파생 | char_5(그믐) | 전부 | 0 / 0 | L, R+0.15s(2304), K+0.15s(2304) | Fire(ShadowHand) | 0 / 2 |
| TV-A074 | 파생 | char_5(그믐) | 전부 | 0 / 2 | L, R+0.15s(2304), K+0.15s(2304) | Fizzle(NoCrystal) | 0 / 2 |
| TV-A075 | 파생 | char_5(그믐) | 전부 | 1 / 1 | F, F+0.15s(2304), K+0.15s(2304) | Fire(PiercingLance) | 0 / 2 |
| TV-A076 | 파생 | char_5(그믐) | 전부 | 0 / 1 | F, F+0.15s(2304), K+0.15s(2304) | Fizzle(NoCrystal) | 0 / 1 |
| TV-A077 | 파생 | char_5(그믐) | 전부 | 0 / 0 | K | Fire(ManaRelease) | 0 / 1 |
| TV-A078 | 파생 | char_5(그믐) | 전부 | 7 / 0 | L, R+0.15s(2304), L+0.15s(2304), K+0.15s(2304) | Fire(ShadowHand) | 5 / 0 |
| TV-A079 | 파생 | char_5(그믐) | 전부 | 3 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(FullBloom) | 0 / 0 |
| TV-A080 | 파생 | char_5(그믐) | 전부 | 2 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fizzle(NoCrystal) | 2 / 0 |
| TV-A081 | 파생 | char_1(달구) | 전부 | 5 / 0 | L, R+0.15s(2304), K+0.15s(2304) | Fire(TwinWing) | 4 / 0 |
| TV-A082 | 파생 | char_3(갈피) | 전부 | 4 / 0 | L, L+0.24s(3686), K+0.15s(2304) | Fire(AfterimageStep) | 2 / 0 |
| TV-A083 | 파생 | char_3(갈피) | 전부 | 4 / 0 | L, L+0.26s(3994), K+0.15s(2304) | Fire(TwinWing) | 3 / 0 |
| TV-A084 | 파생 | char_3(갈피) | 전부 | 4 / 0 | L, L+0.15s(2304), L+0.15s(2304), K+0.15s(2304) | Fire(AfterimageStep) | 2 / 0 |
| TV-A085 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, B+0.21s(3226), K+0.15s(2304) | Fire(RecoilShot) | 4 / 0 |
| TV-A086 | 파생 | char_1(달구) | 전부 | 5 / 0 | B, F+0.29s(4454), K+0.15s(2304) | Fire(VentBurst) | 3 / 0 |
| TV-A087 | 파생 | char_3(갈피) | 전부 | 4 / 0 | F, B+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(VentBurst) | 2 / 0 |
| TV-A088 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, K+0.3s(4608) | Fire(FanSpread) | 4 / 0 |
| TV-A089 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, K+0.31s(4762) | Fire(ManaRelease) | 4 / 0 |
| TV-A090 | 파생 | char_1(달구) | 전부 | 5 / 0 | K | Fire(ManaRelease) | 4 / 0 |
| TV-A091 | 파생 | char_1(달구) | 전부 | 0 / 0 | K | Fizzle(NoCrystal) | 0 / 0 |
| TV-A092 | 파생 | char_1(달구) | 전부 | 1 / 0 | F, F+0.15s(2304), K+0.15s(2304) | Fizzle(NoCrystal) | 1 / 0 |
| TV-A093 | 파생 | char_1(달구) | 전부 | 1 / 0 | F, F+0.15s(2304), K+0.05s(768) | Fire(ThunderLance) | 0 / 0 |
| TV-A094 | 파생 | char_1(달구) | 전부 | 0 / 0 | F, F+0.15s(2304), K+0.05s(768) | Fizzle(NoCrystal) | 0 / 0 |
| TV-A095 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, F+0.15s(2304), K+0s(0) | Fire(ThunderLance) | 4 / 0 |
| TV-A096 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, F+0.15s(2304), K+0.07s(1075) | Fire(ThunderLance) | 4 / 0 |
| TV-A097 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, F+0.15s(2304), K+0.0703s(1080) | Fire(PiercingLance) | 3 / 0 |
| TV-A098 | 파생 | char_1(달구) | {PiercingLance, ManaRelease, RecoilShot, FanSpread} + 고유 | 5 / 0 | F, F+0.15s(2304), K+0.05s(768) | Fire(PiercingLance) | 3 / 0 |
| TV-A099 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, F+0.15s(2304), F+0.15s(2304), K+0.05s(768) | Fire(ThunderLance) | 4 / 0 |
| TV-A100 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, B+0.17s(2611), B+0.19s(2918), F+0.17s(2611), K+0.17s(2611) | Fizzle(FullBloomLate, late_index=3, late_by=153u), 표시 "3번째 입력이 0.01초 늦음" | 5 / 0 |
| TV-A101 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.2s(3072), K+0.17s(2611) | Fizzle(FullBloomLate, late_index=4, late_by=307u), 표시 "4번째 입력이 0.02초 늦음" | 5 / 0 |
| TV-A102 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, B+0.18s(2765), B+0.18s(2765), F+0.18s(2765), K+0.18s(2765) | Fire(FullBloom) | 0 / 0 |
| TV-A103 | 파생 | char_1(달구) | 전부 | 5 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(PiercingLance) | 3 / 0 |
| TV-A104 | 파생 | char_1(달구) | 전부 | 1 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fizzle(NoCrystal) | 1 / 0 |
| TV-A105 | 파생 | char_4(성에) | 전부 | 6 / 0 | F, B+0.17s(2611), B+0.17s(2611), F+0.17s(2611), K+0.17s(2611) | Fire(FullBloom) | 0 / 0 |
| TV-A106 | 파생 | char_3(갈피) | {ManaRelease, RecoilShot} + 고유 | 4 / 0 | F, F+0.15s(2304), K+0.15s(2304) | Fire(ManaRelease) | 3 / 0 |
| TV-A107 | 파생 | char_3(갈피) | {ManaRelease, RecoilShot, FanSpread} + 고유 | 4 / 0 | F, F+0.15s(2304), K+0.15s(2304) | Fire(FanSpread) | 3 / 0 |
| TV-A108 | 파생 | char_2(초롱) | {ManaRelease, RecoilShot} + 고유 | 5 / 0 | L, L+0.15s(2304), K+0.15s(2304) | Fire(ManaRelease) | 4 / 0 |
| TV-A109 | 파생 | char_1(달구) | {ManaRelease, RecoilShot} + 고유 | 5 / 0 | F, B+0.15s(2304), K+0.15s(2304) | Fire(RecoilShot) | 4 / 0 |
| TV-A110 | 파생(실패 링크 DFA) | char_4(성에) | 전부 | 6 / 0 | F, B+0.15s(2304), L+0.15s(2304), K+0.15s(2304) | Fire(IceScreen) | 4 / 0 |
| TV-A111 | 파생(실패 링크 DFA) | char_3(갈피) | 전부 | 4 / 0 | B, F+0.15s(2304), L+0.15s(2304), K+0.15s(2304) | Fire(Transcribe) | 2 / 0 |
| TV-A112 | 파생(실패 링크 DFA) | char_2(초롱) | 전부 | 5 / 0 | L, L+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(BoltDash) | 3 / 0 |
| TV-A113 | 파생(실패 링크 DFA) | char_5(그믐) | 전부 | 7 / 0 | L, L+0.15s(2304), R+0.15s(2304), K+0.15s(2304) | Fire(ShadowHand) | 5 / 0 |
| TV-A114 | 파생(실패 링크 DFA) | char_1(달구) | 전부 | 5 / 0 | F, B+0.15s(2304), B+0.15s(2304), B+0.15s(2304), K+0.15s(2304) | Fire(Kiln) | 3 / 0 |
| TV-A115 | 파생 | char_2(초롱) | 전부 | 5 / 0 | L, R+0.15s(2304), K+0.15s(2304) | Fire(TwinWing) | 4 / 0 |
| TV-A116 | 파생(앞 간선이 늦어도 접미사만 검사) | char_4(성에) | 전부 | 6 / 0 | F, B+0.25s(3840), L+0.15s(2304), K+0.15s(2304) | Fire(IceScreen) | 4 / 0 |
| TV-A117 | 파생(입력 기록 8개) | char_1(달구) | 전부 | 5 / 0 | L, R+0.15s(2304), L+0.15s(2304), R+0.15s(2304), L+0.15s(2304), R+0.15s(2304), L+0.15s(2304), R+0.15s(2304), F+0.15s(2304), F+0.15s(2304), K+0.15s(2304) | Fire(PiercingLance) | 3 / 0 |

GDD 8 입력열 표와의 대응: 표1 "앞,앞,앞,K 전원" = A001~A005, 표2 = A006~A007, 표3 = A008, 표4 = A009~A010, 표5 "앞,뒤,뒤,K 달구/그 밖" = A011~A015, 표6 = A016, 표7 = A017~A018, 표8 = A019, 표9 = A020, 표10 "같은 틱·같은 오프셋" = B01·B01b(이벤트 정렬 규칙이라 PY 검산에서 빠져 있다. GDD 18), 표11 = A021~A030, 표12 = A032~A036, 표13 = A037~A041, 표14 = A042~A046, 표15 = A047~A051. WC 3.5의 판정기 확인 다섯 줄 = A006, A008, A009, A011~A015, A019; "뒤,뒤,K 결정 1개" = A016; "좌,우,K 0.2초 안팎" = A017~A018.

주의: A063(갈피, 결정 1, 앞·옆·K → 부채 전개 부모 대체)은 부채 전개가 해금된 경우다. 부채 전개가 잠겨 있으면 B12(이 명세의 임시 규칙으로 불발)를 본다.

### 18.2 B군 — 규칙별 손 사례

| ID | given | when | then | 근거 |
|---|---|---|---|---|
| B01 | char_1, 전부, 결정 5, 자세 중, F를 시각 t0에 이미 넣음(커서 F) | t0+2304u에 **같은 틱·같은 오프셋**으로 F 누름과 K 누름 이벤트(기록 순번은 K가 먼저여도) | 처리 순서 F → K. 커서 FF, K 간격 0 ≤ 1,075 → Fire(ThunderLance), 결정 4 | R-14, GDD 8 표10 |
| B01b | char_1, 전부, 결정 5, 커서 루트 | 같은 틱·같은 오프셋에 F와 K | F 먼저 → Fire(FanSpread), 결정 4 (K가 먼저였다면 마력 방출) | R-08, R-14 |
| B02 | char_1, 전부, 결정 5, 자세 밖에서 W(=시야 앞) 누른 채 | Shift 누름(자세 진입, W는 계속 누름) → 0.1초 뒤 K | 누른 채인 W는 입력이 아님 → Fire(ManaRelease), 결정 4 | R-07 |
| B03 | char_1, 전부, 결정 5, 자세 중 | F, F+0.15s, Shift 뗌(+0.05s), Shift 누름(+0.05s), K(+0.05s) | 해제·진입 때 초기화 → Fire(ManaRelease) | R-11 |
| B04 | char_1, 전부, 결정 5, 자세 중 | F, F+0.15s, Shift 뗌(+0.05s), K(+0.05s) | 자세 밖 K → Fire(ManaRelease) | R-06 |
| B05 | char_1, 전부, 결정 5, 자세 밖 | 이동 키 F(자세 밖) → Shift 누름(+0.05s) → K(+0.05s) | 자세 밖 방향은 기록 안 됨 → Fire(ManaRelease) | R-04, [임시] |
| B06 | char_1, 전부, 결정 5, 자세 중(Shift) | F, Space 누름(+0.1s, 반전), F'(+0.1s, 뒤집힌 시야 기준 앞), K(+0.1s) | 반전 때 초기화 → 커서 F → Fire(FanSpread) | R-11, R-12 |
| B07 | char_1, 전부, 결정 5, 자세 중 | F, F+0.15s, 일시정지·해제, K(해제 직후) | 초기화 → Fire(ManaRelease) | R-11 |
| B08 | char_1, 전부, 결정 5, 커서 F(시각 t0) | 미리보기 `Evaluate(t0+4608)` / `Evaluate(t0+4609)` | "부채 전개(1)" / "마력 방출(1)". 지갑 변화 없음 | R-10, 11절 |
| B09 | char_1, 전부, 결정 5, 자세 밖 | K(t0) → K(t0+3000u) → K(t0+4700u) | 1번째 Fire(ManaRelease), 대기 끝 t0+6144u. 2번째 무시(보관 창 4608u 전). 3번째 보관 → 시각 t0+4700u로 판정, 발동 틱 = ceil((t0+6144)/256). 결정 5→4→3 | R-16, 10절 [임시] |
| B09b | B09와 같음 | K(t0) → K(t0+4608u) | 보관 창 시작과 같은 시각 → 보관된다 | 10절 [임시] |
| B09c | B09와 같음 | K(t0) → K(t0+4700u) → K(t0+5000u) | 3번째 K는 보관 중이라 무시 | 10절 [임시] |
| B10 | char_1, 결정 0, 자세 밖 | K(t0, 불발) → 결정 +1 → K(t0+100u) | 1번째 Fizzle(NoCrystal), 대기 없음 → 2번째 Fire(ManaRelease) | 20절 U-05 [임시] |
| B11 | char_1, 전부, 결정 5 | 방향 8개 L,R 교대 뒤 F,F,K(모두 0.15s) | Fire(PiercingLance) (= A117) | GDD 8 판정기 |
| B12 | char_3, 해금 {①②+Transcribe}, 결정 1 | F, L+0.15s, K+0.15s | 베껴 쓰기 결제 실패 → 부모 부채 전개 미해금 → Fizzle(NoCrystal), 결정 1 [임시] (PY는 부채 전개를 낸다. 19절 C-02) | 7.3 [임시] |
| B13 | char_5, 전부, 결정 1·빚 0, 커서 LR | 미리보기 | "그림자 손(2) 외상 1" | 11절, WC 4.5 |
| B14 | char_1, 전부, 결정 1, 커서 BB | 미리보기 | "반동 사격(1)"(부모 대체로 내려간 이름) | 11절 |
| B15 | char_1, 전부, 결정 5, 커서 FF | 미리보기 | 두 줄: "섬: 뇌광창(1)" / "K: 꿰뚫는 창(2)" | 11절 |
| B15b | char_1, 해금 {①②③⑥+Kiln}, 결정 5, 커서 FF | 미리보기 | 한 줄: "꿰뚫는 창(2)"(뇌광창 미해금) | 11절 |
| B16 | — | LateBy 153u / 307u / 1,075u / 1u | "0.01초" / "0.02초" / "0.07초" / "0.01초" | 7.5 [임시] |
| B17 | 새 오프라인 프로필, char_3 | 판 시작 | `IsUnlocked`: ManaRelease, RecoilShot, Transcribe = 참. 그 밖 12종 = 거짓(다른 캐릭터의 고유 3종은 트라이에 없음) | U-R2, DEC 1.23 |
| B18 | B17 상태, 시작 웨이브 1 | 웨이브 1 전멸 → 야영 | 차례 1개. 후보 ⊆ {FanSpread, TwinWing, VentBurst}(선행 충족), 2개 | U-R3, U-R7 |
| B19 | char_1, 해금 {①②③⑥+Kiln} | 웨이브 3 차례 / 웨이브 4 차례 | 3번 차례 후보에 ThunderLance 없음 / 4번 차례 후보 집합에 ThunderLance 있음 | U-R8 |
| B20 | char_1, 해금 {①②③⑧+Kiln} | 웨이브 5 차례 / 웨이브 6 차례 | FullBloom 후보 아님 / 후보 | U-R8 |
| B21 | char_1, 해금 = 공용 9종(⑩만 없음), 팔방 보유 | 웨이브 6 차례 | 후보 1개(FullBloom)만 보여 준다 | U-R7 |
| B21b | char_1, 해금 = 공용 9종(⑩만 없음) | 웨이브 1 차례 | 적격 0개 → 차례를 보여 주지 않고 소모 [임시] | 15.3 [임시] |
| B22 | 새 프로필 char_2, 시작 웨이브 11 | 출발 야영 | 차례 6개(1~6번)를 차례대로. 4번 차례부터 뇌광창 적격(꿰뚫는 창을 앞에서 골랐다면), 6번에 만개 적격(팔방이 있으면) | U-R6, [임시] |
| B23 | 새 프로필, 시작 웨이브 4 | 출발 야영 → 웨이브 4·5·6 전멸 | 출발 야영 차례 3개(1~3번) + 웨이브 4·5·6 차례 = 6개 | U-R3, U-R6 |
| B24 | char_4, 공용 8종 모두 해금 | 웨이브 1~6 전멸 | 차례 0개 | U-R5 |
| B25 | char_1, 1판에서 ③④⑤⑥⑦⑧ 해금 | 2판 웨이브 1~6 | 1~3번 차례 0개, 4번 ⑨ 1개, 5번 0개, 6번 ⑩ 1개 → 2판 끝에 공용 8종 완성 | U-R5 검산 |
| B26 | char_1의 해금 {③}, char_2 새 상태 | char_2로 판 시작 | char_2에서 FanSpread 미해금(캐릭터별) | U-R1 |
| B27 | 오프라인 프로필 char_1 해금 {③⑥}, 서버 프로필 char_1 새 상태 | 서버 프로필로 판 시작 | 서버 프로필에서 ③⑥ 미해금 | U-R10 |
| B28 | 결투, char_1, 새 프로필(공용 미해금), 라운드 시작 결정 1 | F, F+0.15s, K+0.15s | 결정 1 < 2 → Fizzle(NoCrystal)(꿰뚫는 창은 공용이라 부모 대체 없음). 해금 검사는 통과(전부 사용) | U-R9, R-15 |
| B28b | 결투, char_1, 새 프로필, 결정 2 | F, F+0.15s, K+0.15s(판정 틱 T) | Fire(PiercingLance), 효과는 틱 T+2에 시작 | 16절 |
| B29 | 결투, char_5, 빚 1, 라운드 시작 | 라운드 시작 결정 1 지급 | 빚부터 갚는다 → 결정 0, 빚 0 | WC 5.4 |
| B30 | char_5, 전부, 결정 5 | L, R+0.15s, K+0.15s | Fire(ShadowHand), CrystalsAfter 3 → 그림자 계약 +9% | A-11 |
| B31 | char_5, 전부, 결정 7 | 만개 입력(모두 0.17s) | Fire(FullBloom), Repeats 7, CrystalsAfter 0 → 그림자 계약 +0% | A-11, GDD 8 |
| B32 | char_3, 전부, 결정 4 | 만개 입력(모두 0.17s) | Repeats 4(갈피 상한) | GDD 8 |
| B33 | char_1, 전부, 결정 5, 과열 잠금 중 | 자세 밖 K | Fire(ManaRelease), 잠금 유지 | A-04 |
| B34 | char_1, 전부, 결정 5, 과열 잠금 중 | B, F+0.15s, K+0.15s | Fire(VentBurst), 피해 = T × (100 ÷ 25) = 4T, 잠금 해제, 열 0 | A-04, 12.2 ⑦ |
| B35 | char_1, 전부, 결정 5, 열 50 | B, F+0.15s, K+0.15s | Fire(VentBurst), 피해 2T | 12.2 ⑦ |
| B36 | 같은 기록(씨앗 + 입력 이벤트)을 두 번 재실행 | — | 모든 ArtDecision과 상태 해시가 같다 | GDD 16 1단계 [조율] |
| B37 | char_1, 전부, 결정 5, 커서 FBB, 앞 간격 셋 중 FB가 0.19s | 미리보기 | "불발(2번째 입력이 0.01초 늦음)" | 11절 [임시] |
| B38 | char_1, 전부, 결정 5 | F, F+0.15s, K+0.09s | Fire(PiercingLance), 연습장 문구 "0.02초 늦음"(1382−1075 = 307u) | 7.5, FX 12.2 |

### 18.3 S군 — 트라이 구조

| ID | given | then |
|---|---|---|
| S01 | BuildTrie(char_1) | 노드 13개. 5.5절 표의 실패 링크·InEdgeGap·K 기술과 같다(FBB.Fail = BB) |
| S02 | BuildTrie(char_2) | 노드 14개. LF/RF 존재, Fail = F, Gap 3,072. FBB.Fail = B |
| S03 | BuildTrie(char_3) | 노드 14개. FL/FR 존재, Fail = L/R, Gap 3,072 |
| S04 | BuildTrie(char_4) | 노드 14개. BL/BR 존재, Fail = L/R, Gap 4,608 |
| S05 | BuildTrie(char_5) | 노드 14개. LR/RL 존재, Fail = R/L, Gap 3,072 |
| S06~S10 | 각 캐릭터의 `Next` 표 | 5.6절의 표와 칸마다 같다 |
| S11 | 모든 캐릭터 | 다른 캐릭터의 고유 노드가 없다(예: char_1에 LR 없음 → L,R,K는 양익 사격, A081) |
| S12 | 모든 캐릭터 | FF 노드만 JustArt = ThunderLance, FBBF만 IsFullBloom, 고유 노드만 UniqueFallback |
| S13 | 모든 노드 | `Next[n][d]`는 항상 깊이 ≥ 1 노드(루트로 떨어지지 않는다) |

합계: A군 117 + B군 44 + S군 13(S06~S10은 캐릭터마다 1건) = **174건**.

---

## 19. 충돌

| ID | 내용 | 택한 쪽과 근거 |
|---|---|---|
| C-01 | 해금이 남는 단위: DEC 2.8 "계정에 캐릭터별로" / GDD 8 "프로필에 캐릭터별로" / ON 1.1 "프로필". | 둘 다 쓴다: 서버 프로필 = 계정이므로 `character_state.unlocked_arts`, 오프라인 프로필은 기기 안 프로필에 캐릭터별로(DEC 2.6이 두 프로필을 분리하라고 정함). DEC 2.8과 모순되지 않는다 |
| C-02 | 고유 비기 부모 대체 때 부모 기술이 해금되지 않았으면: PY는 해금을 보지 않고 부모 기술을 낸다 / GDD 8 "해금하지 않은 노드는 기술이 없는 노드로 본다". | GDD 쪽(규칙 원본)을 택해 **불발** [임시]. 초롱·그믐(부모 양익 사격)과 갈피(부모 부채 전개)는 시작 상태에서 부모가 잠겨 있으므로 실제로 일어난다. 20절 U-01에서 개발자 확인 대상으로 올린다 |
| C-03 | 불발 문구 소수 자릿수: PY "%.3f"(0.010초) / GDD 8·FX 12.1·UI 4.14 "0.0x초"(둘째 자리). | 문서 쪽: 소수 둘째 자리, 올림 [임시] |
| C-04 | 시작 해금 표현: GDD 8 "마력 방출, 반동 사격, 그 캐릭터의 고유 비기 3종" (고유 비기가 3개처럼 읽힘) / ON 1.1 "시작은 3종(마력 방출, 반동 사격, 그 캐릭터의 고유 비기)" / WC 3.5 같은 뜻. | 합계 3종(① ② + 고유 1). 트라이에 고유는 1개뿐이다(GDD 8) |
| C-05 | 벼락 질주의 폭: WC 4.2 "지나간 길(폭 1.0)" / FX 12.4 "줄기는 폭 0.5칸의 선". | 판정 폭은 WC(캐릭터 규칙의 주인, GDD 0) 1.0칸. FX의 0.5칸은 그림의 굵기다(FX 12.4가 규칙은 WC가 정한다고 명시) |
| C-06 | 비기 효과의 "준비" 시간: FX 12.2 표는 ⑥ 0.08초 조준선, ⑦ 0.06초 열 링 오므라듦, ⑩ 0.25초 결정 모으기를 "준비"로 적음 / GDD 8은 준비 시간을 정하지 않음(즉시 발동으로 읽힘). | 규칙 주인은 GDD이지만 FX의 순서가 게임 시간을 전제하므로 임시로 게임 시간에 넣는다(U-14). 개발자 확인 대상 |
| C-07 | 옛 판(2판)의 해금 "판마다 웨이브 1~6에서 선택, 시작 2종"(ON 13 기록 표, GDD 20) / 지금 규칙(캐릭터별 유지, 시작 3종). | 지금 규칙. 기록 표는 효력이 없다(GDD 머리말) |
| C-08 | 노드 FBB의 저장 간격: GDD 8 "간선의 허용 간격은 그 간선으로 들어가는 노드에 하나만 저장"(FBB 값을 명시하지 않음) / PY는 0.18초. | PY 값 0.18초(GDD 표 "만개의 각 입력 0.18초"와 같음). FBB는 기술이 없어 결과에 영향이 없다 |
| C-09 | 반동 사격 넉백: GDD 8 "넉백 2칸" / ON 4.2 결투 1.5칸 / WC 3.6 꼬리표별 표. | 충돌 아님. 무한 웨이브는 WC 3.6 꼬리표 표, 결투는 ON 4.2 |
| C-10 | 뇌광창 기절: GDD 8 "1초" / WC 3.6 엘리트 0.5초·보스 0.3초 동작 멈춤 / ON 4.2 결투 0.5초. | 충돌 아님. 꼬리표·결투별 값을 따로 둔다(GDD 8이 WC 3.6을 기준으로 지정) |
| C-11 | 해금 차례 수와 "두 판이면 다 모인다": 판당 차례 6개 vs 공용 8종 + 뇌광창(웨이브 4 이후)·만개(웨이브 6 이후) 조건. | 충돌 아님. 15.3절 검산(B25)으로 최악에도 두 판에 모인다 |
| C-12 | 허상의 비기 따라 쓰기: MO 3.2(2단계) "해금한" 비기만 / MO 3.9(첫 만남) "해금과 무관하게 셋 다". | 충돌 아님(상황이 다름). 판정기는 `UnlockSet` 조회만 제공 |
| C-13 | 마지막 방향→K 한도: GDD 8 "0.3초"(일반) / 만개 0.18초 / 섬 0.07초. | 충돌 아님. 만개 노드만 K 간격을 0.18초로 재검사(GDD 8 판정기 명시). GDD 개발자 확인 65의 지금 값 유지 |
| C-14 | 효과 상한: GDD 8 "비기 한 번 160개" / GDD 4 "트리 1회 64개, 화면 300개" / ON 4.2 결투 150개. | 셋 다 적용(A-08). 가장 먼저 닿는 상한이 건다 |
| C-15 | 얼음 병풍 파편 범위 표기: GDD 8 "벽 앞쪽 3×2칸" / WC 4.4 "폭 3칸, 길이 2칸" / FX 12.4 "폭 3칸·길이 2칸 직사각형". | 같은 뜻. 폭 3칸(벽과 나란히) × 길이 2칸(시전자 반대쪽으로) |
| C-16 | 반전 자세 중 Shift+Space로 시야만 뒤집는 것(GDD 3)이 "반전"인지. | R-11의 "반전"에 넣는다(시야 기준이 바뀌므로 커맨드 의미가 바뀐다) [임시 해석] |

---

## 20. 미정 (개발자 확인 필요 + 원문이 정하지 않은 것)

### 20.1 원문의 "개발자 확인 필요" 중 이 영역에 걸린 것 (지금 값이 기본값)

| ID | 항목 | 지금 값(그대로 구현) | 출처 |
|---|---|---|---|
| D-06 | 공용 비기를 모으는 속도 | 웨이브 1~6마다 하나씩, 두 판이면 8종 | GDD 개발자 확인 6, GDD 17-7 |
| D-09 | 결투의 비기 | 해금과 무관하게 공용 10 + 고유 1 전부 | GDD 개발자 확인 9, ON 개발자 확인 4 |
| D-08 | 결투 조준 보정 | 최대 10도(방향이 있는 비기 포함) | GDD 개발자 확인 8 |
| D-44 | 결투에서 그믐의 빚이 있을 때의 벌칙 | WC 5.4 그대로(벌칙 1회에 빚 +1, +100은 벌칙 피격에만) | GDD 개발자 확인 44 |
| D-50 | 결투 환산값 전체(베껴 쓰기 8번, 병풍 체력 150·파편 2T, 그림자 손 2칸 등) | WC 5.4 값 | GDD 개발자 확인 50, WC 개발자 확인 9 |
| D-64 | 만개 미해금 상태에서 만개 커맨드 | 실패 링크 그대로 방열 폭발 | GDD 개발자 확인 64 |
| D-65 | 만개의 마지막 간격(앞→K) | 0.18초(풀어 주면 0.3초) | GDD 개발자 확인 65 |
| D-40 | 과제·평가용 발표 구도 | 실행 트리와 입력 트라이, 실패 링크 시연(앞,앞,앞,K = 꿰뚫는 창 / 앞,앞,옆,K = 베껴 쓰기) | GDD 개발자 확인 40, GDD 15 |

### 20.2 원문이 정하지 않은 것 — 임시 기본값

| ID | 질문 | [임시] 기본값 |
|---|---|---|
| U-01 | 고유 비기 부모 대체 때 부모가 미해금이면? | 불발(C-02). 대안: 해금과 무관하게 부모를 낸다(PY 동작) |
| U-02 | 재사용 대기 중, 보관 창 밖의 K | 무시. 커서·기록 유지 |
| U-03 | 보관한 K의 판정 시각 | 누른 시각으로 선택·결제, 발동만 대기 끝 틱 `ceil(cooldownEnd/256)` |
| U-04 | 보관 중 두 번째 K | 무시 |
| U-05 | 불발도 재사용 대기를 시작하나 | 시작하지 않는다 |
| U-06 | 재사용 대기 시작 기준 | 판정 시각(결투 준비 2틱·기술 준비 시간과 무관) |
| U-07 | 과열 잠금 중 마력 방출·방열 폭발 외 비기 | 모두 쓸 수 있다(비기는 열을 쓰지 않는다). 잠금은 방열 폭발만 푼다 |
| U-08 | 같은 오프셋의 자세 키(Shift/Space)와 방향·K 순서 | 자세 키 → 방향 → K. 같은 종류 안에서는 순번 |
| U-09 | J 입력이 커서에 영향을 주나 | 주지 않는다 |
| U-10 | 자세 밖에서 누른 방향 | 기록하지 않는다(자세 진입 때 어차피 초기화) |
| U-11 | 키보드 자동 반복 | 판정기 앞단에서 걸러 "누름" 가장자리만 이벤트로 기록 |
| U-12 | 늦음 표시 반올림 | 소수 둘째 자리 올림 |
| U-13 | 만개 반복 간격 0.12초 = 7.2틱 | 판정 틱 + round(i × 7.2): 0, 7, 14, 22, 29, 36, 43틱. 대안: 7틱 고정(0.1167초) |
| U-14 | FX 12.2의 "준비" 시간이 게임 시간인가 | 넣는다: ⑥ 0.08초 → 5틱, ⑦ 0.06초 → 4틱, ⑩ 0.25초 → 15틱, 그 밖 0. ⑨는 K 순간의 저스트 신호뿐이라 0. 대안: 전부 0(연출만 겹쳐 재생) |
| U-15 | T의 정확한 정의 | 주문 컴파일러 명세의 `TreeDamageT(wand)`(트리 1회 실행의 피해 합, 강화 배율 등 A-10의 곱셈 항은 비기 피해 계산 때 따로 곱함) |
| U-16 | 부채 전개 각도, 양익 사격 배율 | ±15도, 좌우 각 100% |
| U-17 | 반동 사격 넉백·돌진 취소 대상 | 이 비기 트리의 탄에 맞은 적 |
| U-18 | 방열 폭발·만개의 탄 소거 범위와 시점 | 시전자 중심 반경 3칸. 방열 폭발은 발동 틱, 만개는 마지막 파동 틱(FX 12.2 "마지막 파동에 탄 소거 고리") |
| U-19 | 비기 한 번 160개 상한을 넘을 때 | GDD 4의 처리(생략분을 생성된 탄의 피해 배율로 환산)를 따르고, 만개는 마지막 반복부터, 팔방은 정면 외 방향의 뒤쪽부터 생략 |
| U-20 | 해금 후보 뽑기 | 판 씨앗에서 갈라 낸 난수기 `art_unlock`, 균등 무작위 2개 |
| U-21 | 적격 후보 0개인 차례 | 보여 주지 않고 소모 |
| U-22 | 고르지 않은 차례 | 야영을 떠나기 전에 반드시 고른다 |
| U-23 | 해금의 프로필 반영 시점 | 15.4절 |
| U-24 | 건너뛴 차례의 웨이브 조건 | 차례 번호 = 웨이브 번호 |
| U-25 | "웨이브를 넘긴다" | 그 웨이브의 적을 전멸시켜 끝냄(허상과의 첫 만남인 웨이브 10은 1~6 밖이라 무관) |
| U-26 | 허수아비 연습(야영·메인 메뉴·광장)에서 쓸 수 있는 비기 | 프로필의 해금 그대로. 봇 결투·로컬 결투는 전부 |
| U-27 | 상대 좌/우의 정의 | 캐릭터 기준(시야를 반시계 90도 = 좌). 판정은 대칭이라 결과 무관 |
| U-28 | 일시정지 중 게임 시간 | 틱이 멈춘다. 어차피 초기화된다 |
| U-29 | 섬 미리보기 두 줄 조건 | 고른 노드에 해금된 섬 기술이 있으면 항상 두 줄 |
| U-30 | 미리보기에서 만개 간격 초과 | 현재 시각을 K 시각으로 놓고 판정, 불발이면 "불발(n번째 입력이 x초 늦음)" |
| U-31 | 벼락 질주 이동 시간·피해 횟수 | 판정 틱에 즉시 이동, 길 위의 적마다 1회 |
| U-32 | 그림자 손이 중심을 지나치는가 | 지나치지 않는다(중심까지의 거리와 3칸 중 작은 값) |
| U-33 | 불가마·베껴 쓰기·병풍·그림자 손 위치 | 시전 순간의 위치·시야로 고정 |
| U-34 | 열거값의 정수 순서 | 2.2절 표 순서 고정 |
| U-35 | 판·웨이브·라운드 시작 때 재사용 대기 | 해제 |
| U-48 | 결투 준비 2틱 중 시전자가 기절·경직되면 | 취소, 결정 환불, 대기 없음, 연출 "문양 0.1초 흐려짐" |

---

## 21. 시각/소리 연결점 (이벤트 이름만)

코어는 아래 이벤트를 내보내기만 하고 그림·소리는 표시 계층이 붙인다. 이벤트 이름은 `snake_case`, 인자는 괄호. 결투에서는 발동 틱이 서버 확정을 받은 뒤 문양·소리를 낸다(ON 4.11).

### 21.1 커맨드 입력

| 이벤트 | 인자 | 붙는 것 (원문 ID) |
|---|---|---|
| `cmd.dir_input` | dir, node_id, chained | 최근 입력 5개에 화살표 쌓임, 이어지면 선(FX 12.1, GDD 13) |
| `cmd.chain_broken` | reason(gap/timeout) | 화살표 사이 선이 끊어짐(FX 12.1) |
| `cmd.reset` | reason(stance_enter/stance_exit/flip/pause/resolved/timeout) | 최근 입력 표시 비우기 |
| `art.preview_changed` | lines[{art, cost, via_fallback, debt_n, fizzle}] | "지금 K" 줄, 아이콘 I-T2, 비용 I-T4(UI 3.5) |

### 21.2 발동·불발·대기

| 이벤트 | 인자 | 붙는 것 |
|---|---|---|
| `art.cast` | art, difficulty, element, via_fallback, debt_used, is_just, repeats | 발동 문양 고리 96×96(15종, 속성색), 결정이 지팡이로 빨려 듦, 머리 위 기술 이름 0.6초(옵션으로 끔), 기술 이름 띠 192×32 / 큰 띠 320×48. 난이도 1~2 화면 효과 없음, 3 흔들림 소, 5 그림 정지 + 흔들림 중·대 + 큰 띠. 소리 T-00(FX 12.1·19.3·20.4) |
| `art.cast_debt` | art, debt_n | 문양 고리에 보라 테두리 한 겹, 빚 결정 연출(FX 12.4·11.3), 머리 위 빚 표시 16×16(WC 10.1) |
| `art.fizzle_no_crystal` | art | 결정 칸 회색 좌우 떨림, 지팡이 연기 한 점, "지금 K" 두 번 깜빡임, 소리 T-16 "틱틱"(FX 12.1) |
| `art.fizzle_late` | late_index, late_by | 머리 위 "n번째 입력이 0.0x초 늦음", 소리 T-17 "툭"(FX 12.1) |
| `art.cooldown_start` / `art.cooldown_end` | end_time | "지금 K" 테두리가 0.4초 동안 다시 참(FX 12.1) |
| `art.k_buffered` | — | 테두리 안의 점(FX 12.1) |
| `art.duel_cancelled` | art | 문양 0.1초 흐려지며 사라짐(ON 4.11) |
| `art.duel_telegraph` | art, shape, duration | 상대 화면 예고: 그림자 손 범위 0.3초, 병풍 파편 범위 테두리, 불가마 지대 표시(FX 17·20.4) |

### 21.3 공용 비기 효과

| 이벤트 | 붙는 것 (FX 12.2·19.3·20.4) |
|---|---|
| `art.mana_release.fire` | 방출 고리 64×64(4프레임, ×5속성), 탄 1.25배 밝기 + 흰 중심, T-01 굵은 발사음 |
| `art.recoil_shot.fire` | 총구 폭풍 96×64, 잔상 2장, 미끄럼 먼지 32×32, 밀린 적 흰 번쩍, T-02 |
| `art.recoil_shot.charge_broken` | 빨간 예고 띠가 회색 조각으로 깨짐, "끊음!" 64×24, 흔들림 소, T-18 유리 "쨍" |
| `art.fan_spread.fire` | 부채꼴 호 192×128, T-03 "촤악" |
| `art.twin_wing.fire` | 날개 호 96×64(좌우 반전), T-04 2연음 |
| `art.afterimage_step.move` | 잔상 3장(속성 보조색), 무적 동안 흰 테두리, 도착 먼지, T-05 "슉" |
| `art.piercing_lance.aim` / `.fire` / `.armor_break` | 정면 12칸 흰 2px 선(셰이더), 창 64×32(몸통·끝), 긁힌 자국 64×16(2층), 철갑 깨짐 불꽃 48×48, 흔들림 소, T-06 |
| `art.vent_burst.charge` / `.fire` / `.bullets_cleared` / `.lock_broken` | 열 링 오므라듦, 충격파 고리(셰이더, 열에 따라 굵기·색), 소거 김 32×32(30개까지), 재색 잠금 링 깨짐, 흔들림 소~중, T-07, T-19 "치익"(한 번) |
| `art.eightfold_spread.fire` | 팔각 별 문양 128×128(45도 회전), 흔들림 소, T-08 |
| `art.thunder_lance.just_signal` | K 순간 흰 네 갈래 별 48×48(검정 테), T-09a "칭—" |
| `art.thunder_lance.fire` | 톱니 창(몸통·끝·흰 심), 맞은 적 그림 정지 0.08초, 기절 별, 잔광 0.4초(셰이더), 흔들림 중, 큰 띠, T-09b |
| `art.full_bloom.gather` | 결정이 머리 위를 돌다 지팡이로(0.25초), 배경 1~2층 20% 어두워짐(예고·적·탄 제외), T-10a |
| `art.full_bloom.wave` (index) | 만개 문양 192×192(6프레임), 꽃잎 32×48 8장(속성별 모양), 파동마다 흔들림 소, T-10b(7음) |
| `art.full_bloom.final_wave` | 탄 소거 고리(셰이더), 흔들림 대, 번쩍 1회(속성 보조색 20%), T-10c |

### 21.4 고유 비기 효과

| 이벤트 | 붙는 것 (FX 12.4·19.3·20.4) |
|---|---|
| `art.kiln.place` / `.tick` / `.fading` / `.end` | 테두리 320×320(주황 점선 + 벽돌 무늬), 불티 8×8 초당 8개, 아지랑이 셰이더, 안의 적 0.5초마다 작은 불꽃, 끝나기 1초 전 흐려짐. T-11(까는 소리 + 이어지는 울림). 입자 24 |
| `art.kiln.heat_absorbed` | (흡수 남은 양 표시 자리 [임시]) |
| `art.bolt_dash.fire` / `.arrive` / `.graze` | 줄기 256×64(3프레임, 0.2초), 레몬색 잔광 0.3초, 감전 떨림, 등불 알 켜짐, 스침 "챙", T-12. 입자 20 |
| `art.transcribe.place` / `.copy_fire` / `.end` | 사본 마법진 96×96, 갈피 사본(청록 단색 40%), 사본 탄 불투명도 70% + 점선 테두리, 책장 흩어짐 16×16. T-13(책 펴는 소리, 사본 발사음은 −6dB·한 옥타브 위). 입자 16 |
| `art.ice_screen.rise` / `.block` / `.ripple` / `.cracking` / `.shatter` / `.expire` | 병풍 192×96(솟음 3·서 있음 1·금 감 2·깨짐 4, 가로·세로 2벌), 0.15초에 솟음, 서리 반짝임, 잔물결 32×32, 파편 16×16 12조각, 6층·불투명도 75%, T-14(솟음·막음·깨짐). 입자 16 |
| `art.shadow_hand.range_show` / `.pull` / `.fist` | 웅덩이 128×128, 범위 고리(4px 선, 0.15초, 결투 0.3초), 손 32×32·팔 32×16(8개까지), 주먹 64×64, T-15. 입자 24 |

- 다섯 고유 비기 모두 FX 9.3의 "내 비기" 입자 몫 400에서 받고, 지대는 3층, 터지는 것은 9.2절 버퍼를 거친다. 지름 2칸 이상의 면을 흰색이나 위험색으로 채우지 않는다. (FX 12.4)
- 공용 비기의 속성 변주(입자 모양, 0.3초 흔적, 움직임, 겹치는 소리, 만개 꽃잎)는 `element` 인자로 고른다. (FX 12.3)

### 21.5 해금·연습 화면

| 이벤트 | 붙는 것 |
|---|---|
| `unlock.turn_offered` (wave_index, candidates) | 720×400 판, 후보 카드 S-1303 ×1~2(UI 4.13) |
| `unlock.picked` (art) | 트라이 마디 S-1301 상태 "잠김/후보" → "해금" |
| `unlock.all_complete` | (자리만) |
| `practice.gap_report` (edges[{limit, actual}]) | 입력 간격 막대 S-1401(UI 4.14) |
| `practice.just_gauge` (elapsed) | 저스트 게이지 S-1402, 창 안이면 흰 별 |
| `practice.fail_reason` (text) | 실패 원인 한 줄 |
| — | 트라이 그림: 마디 64×64 / 점 마디 24×24, 가지 S-1302, 간격 표시는 기본(0.3초)과 다른 것만, 비기 아이콘 I-T1 48×48 15종, 난이도 점 I-T3(UI 4.13·3.5) |

---

## 22. 구현 의존 순서

(일정이 아니라 의존 관계만)

1. 입력 이벤트 구조체와 정렬(R-13, R-14) — 결정론 코어 기반. 다른 모든 것이 의존.
2. `CmdDir` 변환(3.1)과 자세 상태(6.2) — 1에 의존.
3. `ArtDef` 데이터(12.1)와 `BuildTrie`(5.4) — 독립. S군 테스트.
4. `CommandParser`의 `OnDirectionPress`/`Evaluate`/`Pay`(7절)와 `ICrystalWallet`(9절) — 2, 3에 의존. A군 테스트.
5. 재사용 대기와 보관(10절), 미리보기(11절) — 4에 의존. B군 일부.
6. 해금(15절)의 `UnlockSet`(판 안) — 4가 조회. 프로필 저장은 오프라인 백엔드(GDD 16의 6단계)에 의존.
7. 효과 실행(12.2, 14절) — 주문 컴파일러의 `Execute`·T 계산, 과열, 이동·충돌, 적 꼬리표에 의존.
8. 결투 차이(16절) — 4~7과 결투 동기화(ON 4.11)에 의존.
9. 이벤트 연결(21절) — 4~7이 이벤트를 내보낸 뒤 표시 계층에서.

GDD 16의 단계와의 대응: 1단계(손맛 시제품)에 판정기 + 임시 효과 3개(①②⑥)와 섬 간격 밀리초 표시, 5단계에 비기 전체·고유 5종·캐릭터별 트라이·비기 탭·연습장, 6단계에 비기 해금 저장. 1단계 통과 기준: 두 번 누르는 커맨드 성공률 90% 이상, 10분 연습 후 섬 성공률 30% 이상, 자세 중 이동하다 엉뚱한 기술이 나가는 비율 10% 미만(5단계에서 캐릭터 다섯 명을 따로 잰다. 달구의 "뒤, 뒤"와 그믐의 "좌, 우"를 따로 본다). (GDD 16·17, WC 3.5) [제안]
