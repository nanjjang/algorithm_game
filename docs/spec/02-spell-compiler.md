# 구현 명세 02 — 주문 컴파일러, 주문 블록 16종, 속성

게임: "엄청엄청 위대한 마법사 키우기". 이 문서는 구현자가 원본 기획 문서를 다시 열지 않고 주문 컴파일러(슬롯 배열 → 실행 트리 → 실행), 주문 블록 16종의 데이터와 동작, 속성(피해 속성, 약점·저항)을 만들 수 있게 옮긴 **구현용 명세**다.

---

## 0. 읽는 법

### 0.1 우선순위

1. `docs/DECISIONS.md`가 모든 문서보다 우선한다. 이 명세와 원본이 다르면 DECISIONS가 맞다.
2. 그다음은 GDD 0절의 "주인 문서" 규칙이다(같은 내용이 두 문서에 있으면 주인 쪽이 맞다).
3. 이 명세가 원문을 풀어 쓰면서 새로 정한 값은 [제안](원문에 근거가 있는 해석) 또는 [임시](원문이 정하지 않아 구현을 막지 않으려고 둔 기본값)로 표시한다.

### 0.2 출처 약어

| 약어 | 파일 |
|---|---|
| D | `docs/DECISIONS.md` |
| G | `docs/GDD.md` |
| E | `docs/effects.md` |
| M | `docs/monsters.md` |
| W | `docs/world-characters.md` |
| O | `docs/online.md` |
| U | `docs/ui-assets.md` |

"(G 4)"는 GDD 4절, "(D 2.5)"는 DECISIONS 2.5를 뜻한다.

### 0.3 표시

| 표시 | 뜻 |
|---|---|
| [확정] | 개발자가 직접 정한 것(D 1절의 [개발자], 원문의 **[확정]**) |
| [조율] | 문서 사이의 충돌을 풀려고 D 2절에 정리한 공통 규격, 원문의 **[조율]** |
| [제안] | 원문의 [제안] 또는 표시 없는 수치(G 머리말: "표시가 없는 수치는 [제안]이다"). 이 명세가 원문을 구현 가능하게 풀어 쓴 해석도 [제안]으로 적고 "(명세 해석)"을 붙인다 |
| [임시] | 원문이 정하지 않은 것. 18절 "미정"에 같은 번호로 모았다. QA나 개발자 확인 전까지 이 값으로 만든다 |

모든 수치는 플레이 테스트 전의 시작값이다. 이 값으로 먼저 만들고 QA에서 조정한다 (D 1.17, G 머리말) [확정].

### 0.4 이 명세의 경계

| 이 명세가 정하는 것 | 다른 영역(다른 명세)이 정하고 이 명세는 인터페이스만 맞추는 것 |
|---|---|
| 블록 16종의 데이터와 투사체 동작, 슬롯 배열, 컴파일(트리 구성·검증·가지치기), 발사 1회의 열 **계산**, 실행 함수 `Execute`, 투사체 수 안전장치, 피해 공식, 속성과 약점·저항, 블록이 거는 상태(감속·잠식), 적의 복사(허상)·결투·사본 마법진이 쓰는 트리 변환, 조립 경고, 컴파일 트리 미리보기의 자료 | 열 게이지의 누적·냉각·임계·잠금 상태 기계(과열), 콤보와 결정, 비기 판정기와 각 비기의 효과, 강화 확률과 변이 부여, 적 AI와 꼬리표 반응 전반, 화면 그림과 소리, 입력 이벤트 |

---

## 1. 용어

| 용어 | 정의 | 출처 |
|---|---|---|
| 지팡이 | 코어 슬롯 1개 + 연결 슬롯 N개를 가진 개체. 캐릭터 전용 | G 4, G 10.1 |
| 슬롯 배열 | 코어(0번) 다음에 연결 슬롯(1번~L번)이 순서대로 놓인 배열 | G 4 (명세 해석) |
| 블록 | 슬롯에 끼우는 주문 조각. 종류는 투사체·제어·변형 셋 | G 10.3 |
| 스코프 | 같은 때에 함께 발사되는 블록 묶음. 루트 스코프(깊이 0)와 "적중 시 발동"이 여는 하위 스코프(깊이 1~3) | G 4 |
| 스코프의 주인 | 트리거 뒤 첫 투사체. 루트 스코프의 주인은 코어 블록 | G 4, U 4.11 |
| 실행 트리 | 슬롯 배열을 컴파일한 결과(`CompiledSpell`). 깊이 우선으로 실행한다 | G 4 |
| 트리 실행 1회 | `Execute` 한 번. J 발사 1회, 비기의 트리 1회, 사본 마법진의 따라 쏘기 1회, 허상의 복사 사격 1회가 각각 1회 | G 4, G 8, W 4.3, M 3.4 |
| T | "지팡이 트리 1회 실행의 피해". 비기·패시브 피해의 단위 | G 머리말, W 머리말 |
| 발사 1회(cast) | J를 한 번 눌러 나가는 한 묶음. 분열·하위 스코프·거울상으로 생긴 탄도 같은 발사에 속한다 | G 7, E 9.3 (명세 해석) |
| 진영 | 플레이어 쪽 / 적 쪽 | G 4 |

---

## 2. 단위와 수치 표현

### 2.1 원문 단위

- 거리는 칸, 1칸 = 64px. 속도는 칸/초. 시간은 초. 게임 시간 1초 = 60틱 (G 머리말, D 2.1) [조율].
- 피격 판정, 사거리 같은 게임 수치는 칸 단위 그대로다 (D 2.1) [조율].
- 전장 24×14칸, 사방이 막힘 (D 2.3) [조율]. 결투장 돌마당도 24×14칸, 선돌 기둥 4개(각 1×1칸, 칸 좌표 (9,3), (14,3), (9,10), (14,10). 탄을 막고 벽 튕김이 된다. 부서지지 않는다) (O 4.1, W 2.6) [제안].
- 컴파일과 실행은 유니티와 분리된 결정론 코어 안에서 고정소수점 정수로 계산한다. 같은 입력이면 기기와 서버에서 같은 결과가 나와야 한다 (G 4, G 15) [조율].
- 고정 60틱, 고정소수점은 Q32.32 정수, 유니티 물리는 쓰지 않는다(탄과 적은 원 판정, 직접 계산) (O 3.1) [제안].
- 난수는 판 씨앗에서 갈라 낸 자체 생성기(PCG 계열)만 쓴다. 반복 순서가 정해진 자료구조(배열, 정렬된 목록)만 쓴다 (O 3.1) [제안].
- 코어 어셈블리 이름은 `Wizard.Sim` (O 3.1) [제안].

### 2.2 고정소수점 `Fx` (Q32.32)

| 항목 | 규칙 | 표시 |
|---|---|---|
| 형 | `readonly struct Fx { long Raw; }`, 1.0 = `1L << 32` = 4,294,967,296 | [제안] (O 3.1) |
| 상수 만들기 | 소수 상수는 **정확한 십진값 × 2^32를 가장 가까운 정수로(같으면 짝수로)** 반올림한 값을 소스에 정수로 박는다. 실행 중에 `double`에서 바꾸지 않는다 | [임시] (U18) |
| 곱셈 | 64×64 → 128비트 곱 뒤 산술 오른쪽 시프트 32(음의 무한대 쪽으로 내림). `Math.BigMul`이 없는 런타임이면 직접 구현한다 | [임시] (U18) |
| 나눗셈 | `(a.Raw << 32) / b.Raw`를 128비트로 계산, 0 쪽으로 버림 | [임시] (U18) |
| 거듭제곱 | 강화 배율 `1.15^n`, 감쇠 `0.85^d`, 전이 `0.7^k`는 실행 중에 곱하지 않고 **미리 만든 표**(정확한 십진 거듭제곱을 반올림)에서 읽는다 | [임시] (U18) |
| 피해 숫자 표시 | 표시 계층에서 가장 가까운 정수(.5는 올림) | [임시] (U19) |

자주 쓰는 상수의 Raw 값(위 규칙으로 계산):

| 값 | Raw | 쓰는 곳 |
|---|---|---|
| 1.25 | 5,368,709,120 | 약점 |
| 0.8 | 3,435,973,837 | 저항 |
| 0.85 | 3,650,722,202 | 깊이 감쇠 |
| 0.92 | 3,951,369,912 | 갈피 지팡이 감쇠 |
| 0.7225 (0.85²) | 3,103,113,871 | 깊이 2 |
| 0.614125 (0.85³) | 2,637,646,791 | 깊이 3 |
| 0.8464 (0.92²) | 3,635,260,319 | 갈피 깊이 2 |
| 0.778688 (0.92³) | 3,344,439,494 | 갈피 깊이 3 |
| 0.7 | 3,006,477,107 | 연쇄 번개 전이 |
| 1.6 | 6,871,947,674 | 2갈래 열 배수, 뒤틀린 총열 |
| 2.2 | 9,448,928,051 | 3갈래 열 배수 |
| 1.5 | 6,442,450,944 | 거울상 열 |
| 0.5 | 2,147,483,648 | 사본 마법진 피해 |
| 0.12 | 515,396,076 | 작은 탄 반지름 |
| 0.18 | 773,094,113 | 큰 탄 반지름 |
| 0.25 | 1,073,741,824 | 그림자 구체 반지름 |

### 2.3 각도 `Angle`

- 각도는 정수 **센티도**(1/100도)로 든다. 한 바퀴 = 36,000. 0 = 오른쪽(+x), 반시계 방향이 + [임시] (U17).
- 8방향: 0, 4500, 9000, … 31500.
- `sin`/`cos`는 0~9000(1/4바퀴) 9,001칸의 Fx 표를 빌드 전에 만들어 소스에 넣고, 나머지 사분면은 대칭으로 얻는다. 실행 중 `Math.Sin`을 부르지 않는다 [임시] (U17).
- 원문 각도의 환산: ±10도 = ±1000, ±15도 = ±1500, ±25도 = ±2500, 180도 = 18000, 10도(결투 보정 상한) = 1000.

### 2.4 시간 환산 (1초 = 60틱)

| 원문 | 틱 | 쓰는 곳 |
|---|---|---|
| 0.15초 | 9 | 마력탄 간격 |
| 0.3초 | 18 | 얼음 송곳 간격, 사본 마법진 최소 간격 |
| 0.35초 | 21 | 부메랑 간격 |
| 0.45초 | 27 | 연쇄 번개 간격 |
| 0.5초 | 30 | 그림자 구체 간격 |
| 0.6초 | 36 | 거대 화염구 간격, 허상의 연쇄 번개 조준선 |
| 1초 | 60 | 얼음 송곳·서리 감속 |
| 3초 | 180 | 잠식, 결투 유리 지팡이 꺼짐 |
| 6초 | 360 | 사본 마법진 |
| 0.25초 | 15 | 결투 연쇄 번개 조준선 |
| 0.8초 | 48 | 허상 폭발 예고 원 |
| 1.5초 | 90 | 결투 유도 끊김 |
| 3틱 | 3 | 결투의 상태 효과 지연 |

---

## 3. 속성

### 3.1 속성 열거형

| 값 | 식별자 | 원문 | 주문색(주/보조) | 탄 모양 | 캐릭터 |
|---|---|---|---|---|---|
| 0 | `None` | 무 (블록에만 있다) | 분필 흰색(아이콘) | — | — |
| 1 | `Fire` | 화염 | 주황 `#FF8A1F` / 밝은 노랑 `#FFE08A` | 둥글고 꼬리가 일렁인다 | `char.1` |
| 2 | `Lightning` | 번개 | 레몬 `#FFF36B` / 흰색 `#FFFFFF` | 꺾인 선 | `char.2` |
| 3 | `Arcane` | 비전 | 청록 `#4FE3C1` / 흰 민트 `#D6FFF3` | 네모·마름모 | `char.3` |
| 4 | `Frost` | 냉기 | 하늘 `#9FDCFF` / 흰 하늘 `#F4FBFF` | 뾰족한 육각 조각 | `char.4` |
| 5 | `Dark` | 암흑 | 푸른 보라 `#8A5CFF` / 연보라 `#D9C7FF` | 고리(밝은 테 + 어두운 고리 + 밝은 심) | `char.5` |

- 속성은 화염, 번개, 비전, 냉기, 암흑 다섯 (D 1.11) [확정]. 블록에는 속성이 없는 "무"도 있다 (G 10.2) [조율].
- 주문색: 화염 주황, 번개 레몬, 비전 청록, 냉기 하늘, 암흑 푸른 보라. 탄은 색뿐 아니라 모양으로도 구분한다 (D 2.2) [조율]. 색 값과 모양 (E 0.1) [제안]. 코어는 색 값을 갖지 않고 `Element`만 내보낸다(그림은 표시 계층).
- 캐릭터 번호와 속성: 제자 1 화염, 2 번개, 3 비전, 4 냉기, 5 암흑 (D 1.25, W 0) [확정]. 캐릭터의 속성은 전용 지팡이의 속성이다 (W 3.4) [제안].

### 3.2 피해 속성 규칙

| 번호 | 규칙 | 출처 | 표시 |
|---|---|---|---|
| EL-1 | 피해 속성은 맞힌 투사체 블록의 속성이다 | D 2.5, G 10.2 | [조율] |
| EL-2 | 탄의 피해 속성, 색, 모양은 그 탄을 만든 투사체 블록의 속성이다. 변형·제어 블록의 속성은 피해 속성을 바꾸지 않는다(제물 보너스에만 쓰인다). 서리와 잠식도 변형이라 피해 속성을 바꾸지 않는다 | G 4, G 10.3 | [조율] |
| EL-3 | 트리를 실행해서 나가는 탄(J 발사, 트리를 쓰는 비기, 베껴 쓰기의 사본)의 피해 속성은 맞힌 투사체 블록의 속성이다. 캐릭터 속성과 다른 블록을 끼우면 그 블록의 속성으로 들어간다 | W 3.4, G 8 | [조율] |
| EL-4 | 블록을 거치지 않는 피해(패시브와 고유 비기가 스스로 내는 피해: 끓어넘침, 불가마 지대, 피뢰침 벼락, 벼락 질주, 병풍 파편, 그림자 손)는 캐릭터의 속성이다 | G 4, G 10.1, W 3.4 | [조율] |
| EL-5 | 꿰뚫는 창, 방열 폭발의 속성은 코어 블록의 속성이다 | G 8 | [제안] |
| EL-6 | 적이 내는 피해 가운데 투사체 블록에서 나오지 않은 것(꽈리 벌레의 유폭, 멧돼지에게 치인 피해, 돌 파편)은 무속성이고 상성을 받지 않는다 | M 1.3 | [제안] |
| EL-7 | 거대 화염구의 폭발, 연쇄 번개의 전이도 그 블록의 속성이다(폭발·전이는 블록의 일부) | G 10.3 (명세 해석) | [제안] |
| EL-8 | 블록을 거치지 않는 플레이어 쪽 피해 가운데 원문이 따로 정하지 않은 것은 캐릭터 속성으로 둔다(EL-4의 일반 규칙을 따른다) | 17절 충돌 C5 | [제안] |

### 3.3 약점과 저항

| 번호 | 규칙 | 출처 | 표시 |
|---|---|---|---|
| AF-1 | 약점 속성 피해 ×1.25, 저항 속성 ×0.8, 나머지 세 속성 ×1 | D 2.5, M 1.3 | [조율] |
| AF-2 | 적마다 약점 1개와 저항 1개. 다섯 속성이 약점과 저항으로 고르게 나오게 배치한다(속성별 개수 차이 1 이하) | D 1.11, D 2.5 | [확정]/[조율] |
| AF-3 | 어떤 종도 약점과 저항이 같은 속성이 아니다. 속성 보스 다섯은 자기 속성에 저항한다 | M 2.5 | [제안] |
| AF-4 | 엘리트는 바탕 종의 약점과 저항을 그대로 갖는다. 보스의 졸개(갓버섯 새싹, 허상이 부르는 슬라임)는 상성이 없다 | M 1.3, M 2.6 | [제안] |
| AF-5 | 처음 만나는 적(웨이브 1~4의 다섯 종)은 비전에 저항하지 않는다(시작 블록이 마력탄) | M 1.3 | [제안] |
| AF-6 | 종을 더하거나 뺄 때는 아래 표의 합이 속성끼리 1 넘게 벌어지지 않게 한다 → 데이터 검증 테스트로 둔다 | M 2.5 | [제안] |
| AF-7 | 결투에서는 속성 약점·저항이 없다(플레이어에게는 약점이 없다). 속성 배율 = 1 | O 4.2 | [제안] |
| AF-8 | 무속성 피해(EL-6)와 `Element.None`은 속성 배율 1 | M 1.3 | [제안] |

**상성 표 `AffinityTable`** (M 2.1, M 3.2) [제안]. 적의 id는 M 2.1의 "데이터 ID"를 그대로 쓰고, 보스 id는 원문에 없어 이 명세가 정했다 [임시] (U33).

| id | 이름(작업용) | 구분 | 첫 등장 웨이브 | 약점 | 저항 |
|---|---|---|---|---|---|
| `slime_moss` | 이끼 슬라임 | 일반 | 1 | Fire | Frost |
| `wolf_bramble` | 덤불 늑대 | 일반 | 3 | Lightning | Dark |
| `golem_menhir` | 선돌지기 | 일반 | 5 | Arcane | Lightning |
| `shaman_cap` | 갓버섯 술사 | 일반 | 3 | Frost | Dark |
| `bug_lantern` | 꽈리 벌레 | 일반 | 2 | Frost | Fire |
| `hog_needle` | 솔침 고슴도치 | 일반 | 4 | Dark | Frost |
| `weasel_pot` | 화분 족제비 | 일반 | 6 | Lightning | Fire |
| `bat_dusk` | 해거름 박쥐 | 일반 | 7 | Fire | Dark |
| `wisp_stray` | 도깨비불 | 일반 | 8 | Arcane | Lightning |
| `vine_thorn` | 가시덩굴 | 일반 | 11 | Fire | Arcane |
| `boar_tusk` | 엄니 멧돼지 | 일반 | 13 | Frost | Arcane |
| `toad_drum` | 북두꺼비 | 일반 | 15 | Dark | Frost |
| `boss_phantom` | 스승의 허상 (속성 비전) | 보스 | 10(첫 만남), 20, 80, 140 … | Dark | Arcane |
| `boss_kiln` | 숯가마 멧돼지왕 "가마등" (화염) | 보스 | 10(첫 만남 뒤), 70, 130 … | Frost | Fire |
| `boss_spore_queen` | 포자 여왕 "천 갓" (속성 없음) | 보스 | 30, 90, 150 … | Fire | Lightning |
| `boss_storm_owl` | 폭풍 수리부엉이 "밤눈" (번개) | 보스 | 40, 100, 160 … | Arcane | Lightning |
| `boss_frost_ram` | 서리 산양 "흰뿔" (냉기) | 보스 | 50, 110, 170 … | Lightning | Frost |
| `boss_night_serpent` | 별밤 구렁이 "밤삼킴" (암흑) | 보스 | 60, 120, 180 … | Lightning | Dark |

속성별 합(검산, M 2.5): 약점 화염 4 / 번개 4 / 비전 3 / 냉기 4 / 암흑 3, 저항 화염 3 / 번개 4 / 비전 3 / 냉기 4 / 암흑 4. 합계 18 / 18.

```csharp
// 제안 자료형
public readonly struct Affinity { public readonly Element Weak, Resist; }
public interface IAffinitySource { Affinity GetAffinity(EnemyTypeId id); bool IsMinion(EntityId e); }
static Fx ElementMultiplier(Element dmg, Affinity a, GameMode mode)
{
    if (mode == GameMode.Duel || dmg == Element.None) return Fx.One;   // AF-7, AF-8
    if (dmg == a.Weak)   return Fx.FromRaw(5_368_709_120);               // 1.25
    if (dmg == a.Resist) return Fx.FromRaw(3_435_973_837);               // 0.8
    return Fx.One;
}
```

---

## 4. 주문 블록 16종

### 4.1 원본 표 (G 10.3, D 2.5)

| # | id(제안) | 원문 이름 | 종류 | 등급 | 속성 | 열 | 피해 | 간격 | 속도 / 사거리 | 메모 | 표시 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `magic_bolt` | 마력탄 | 투사체 | 일반 | 비전 | 4 | 10 | 0.15초 | 14 / 10 | 시작 블록 | [제안] |
| 2 | `great_fireball` | 거대 화염구 | 투사체 | 희귀 | 화염 | 18 | 40 | 0.6초 | 9 / 8 | 반경 2 폭발 | [제안] |
| 3 | `chain_lightning` | 연쇄 번개 | 투사체 | 영웅 | 번개 | 12 | 18 | 0.45초 | 즉발 / 7 | 3회 전이(반경 3, 전이마다 ×0.7), 늑대 회피 무시 | [제안] |
| 4 | `boomerang` | 부메랑 | 투사체 | 희귀 | 비전 | 10 | 15 | 0.35초 | 10 / 편도 6 | 왕복 2회 타격 | [제안] |
| 5 | `ice_spike` | 얼음 송곳 | 투사체 | 희귀 | 냉기 | 8 | 14 | 0.3초 | 12 / 9 | 맞은 적 1초 동안 이동 −30% | [조율] (D 2.5) |
| 6 | `shadow_orb` | 그림자 구체 | 투사체 | 희귀 | 암흑 | 14 | 28 | 0.5초 | 6 / 8 | 느리고 큰 탄. 적을 지나가며 적마다 1회 타격 | [조율] (D 2.5) |
| 7 | `split_2` | 2갈래 분열 | 제어 | 일반 | 무 | ×1.6 | | | | | [제안] |
| 8 | `split_3` | 3갈래 분열 | 제어 | 희귀 | 무 | ×2.2 | | | | | [제안] |
| 9 | `on_hit_trigger` | 적중 시 발동 | 제어 | 희귀 | 무 | 0 | | | | 새 스코프 | [제안] |
| 10 | `wall_bounce` | 벽 튕김 | 변형 | 일반 | 무 | +2 | | | | | [제안] |
| 11 | `double_size` | 탄환 크기 2배 | 변형 | 일반 | 화염 | +3 | +20% | | | | [제안] |
| 12 | `pierce_2` | 관통 +2 | 변형 | 일반 | 비전 | +3 | +10% | | | | [제안] |
| 13 | `frost` | 서리 | 변형 | 일반 | 냉기 | +3 | | | | 맞은 적 1초 동안 이동 −20% | [조율] (D 2.5) |
| 14 | `homing` | 유도 | 변형 | 영웅 | 번개 | +5 | | | | 선회각 제한 | [제안] |
| 15 | `overheat_x3` | 과열 3배 | 변형 | 영웅 | 화염 | +2 | | | | 임계 배율을 2에서 3으로 교체 | [제안] |
| 16 | `erosion` | 잠식 | 변형 | 영웅 | 암흑 | +5 | | | | 맞은 적이 3초 동안 받는 피해 +15% | [조율] (D 2.5) |

- 냉기와 암흑 블록을 추가해 총 16종 (D 2.5) [조율]. 얼음 송곳, 그림자 구체, 서리, 잠식 네 줄은 D 2.5의 값 그대로다 (G 10.3) [조율].
- 다섯 속성 모두 자기 속성의 투사체 블록이 있다: 화염 거대 화염구, 번개 연쇄 번개, 비전 마력탄·부메랑, 냉기 얼음 송곳, 암흑 그림자 구체 (G 10.3, W 3.4) [조율].
- 등급은 드랍 확률과 제물 점수에만 영향을 준다 (G 10.3) [제안]. 등급별 개수: 일반 6(마력탄, 2갈래 분열, 벽 튕김, 탄환 크기 2배, 관통 +2, 서리), 희귀 6(거대 화염구, 부메랑, 3갈래 분열, 적중 시 발동, 얼음 송곳, 그림자 구체), 영웅 4(연쇄 번개, 유도, 과열 3배, 잠식) (M 6.2) [제안].
- 자유 조준이 없으므로 유도와 연쇄 번개의 가치가 높다. 그래서 영웅 등급이다 (G 10.3) [제안].
- 아이콘 바탕 판: 투사체 둥근 판, 제어 육각 판, 변형 모서리를 깎은 네모 판 (U 3.1) [제안]. 코어는 판 종류만 내보낸다.

### 4.2 코어용 데이터 (`BlockDef`)

```csharp
public enum BlockId : byte {
    None = 0, MagicBolt = 1, GreatFireball = 2, ChainLightning = 3, Boomerang = 4, IceSpike = 5, ShadowOrb = 6,
    Split2 = 7, Split3 = 8, OnHitTrigger = 9,
    WallBounce = 10, DoubleSize = 11, Pierce2 = 12, Frost = 13, Homing = 14, Overheat3 = 15, Erosion = 16 }
public enum BlockKind : byte { Projectile = 1, Control = 2, Modifier = 3 }
public enum ControlType : byte { None = 0, Split = 1, Trigger = 2 }
public enum BlockRarity : byte { Common = 1, Rare = 2, Heroic = 3 }   // 일반, 희귀, 영웅

public sealed class BlockDef {
    public BlockId Id; public string Key;          // "magic_bolt" 등. 이름 표 키는 "block.magic_bolt"
    public BlockKind Kind; public ControlType Control; public BlockRarity Rarity; public Element Element;
    public Fx Heat;                 // 투사체·변형: 더하는 열. 트리거 0
    public Fx HeatMultiplier;       // 분열만: 1.6 / 2.2. 그 밖 1
    public int SplitCount;          // 분열만: 2 / 3. 그 밖 1
    public Fx BaseDamage;           // 투사체만
    public int IntervalTicks;       // 투사체만(코어일 때 발사 간격)
    public Fx SpeedPerSecond;       // 칸/초. 즉발이면 0 + IsInstant
    public bool IsInstant;
    public Fx Range;                // 칸. 부메랑은 편도
    public Fx HitRadius;            // 칸. 18절 U1
    public Fx DamageBonus;          // 변형만: +0.2, +0.1
    public ProjectileFlags Flags;   // Explodes, Chains, Returns, PassThrough, IgnoresWolfDodge
    public ModifierEffect Effect;   // 변형만: Bounce, Pierce, Size, Homing, Frost, Overheat3, Erosion
}
```

필드 값 (위 4.1 표를 코어 값으로 옮긴 것):

| id | Kind | Heat | HeatMul | Split | BaseDamage | IntervalTicks | 속도 칸/초 → 칸/틱 (Raw) | Range | HitRadius | 기타 |
|---|---|---|---|---|---|---|---|---|---|---|
| magic_bolt | P | 4 | 1 | 1 | 10 | 9 | 14 → 0.2333… (1,002,159,036) | 10 | 0.12 [임시] | |
| great_fireball | P | 18 | 1 | 1 | 40 | 36 | 9 → 0.15 (644,245,094) | 8 | 0.18 [임시] | Explodes, 폭발 반경 2 |
| chain_lightning | P | 12 | 1 | 1 | 18 | 27 | 즉발 | 7 | 광선 반폭 0.12 [임시] | Chains: 전이 3회, 전이 반경 3, 전이마다 ×0.7; IgnoresWolfDodge |
| boomerang | P | 10 | 1 | 1 | 15 | 21 | 10 → 0.1666… (715,827,883) | 편도 6 | 0.15 [임시] | Returns, 갈 때·올 때 각 1회 타격 |
| ice_spike | P | 8 | 1 | 1 | 14 | 18 | 12 → 0.2 (858,993,459) | 9 | 0.12 [임시] | OnHit: 감속 30% 60틱 |
| shadow_orb | P | 14 | 1 | 1 | 28 | 30 | 6 → 0.1 (429,496,730) | 8 | 0.25 [제안] (M 3.4) | PassThrough: 적마다 1회, 벽에서 소멸 |
| split_2 | C/Split | 0 | 1.6 | 2 | | | | | | 부채꼴 ±10도 |
| split_3 | C/Split | 0 | 2.2 | 3 | | | | | | 부채꼴 −15 / 0 / +15도 |
| on_hit_trigger | C/Trigger | 0 | 1 | 1 | | | | | | 새 스코프 |
| wall_bounce | M | 2 | | | | | | | | Bounce |
| double_size | M | 3 | | | | | | | | Size, DamageBonus 0.2 |
| pierce_2 | M | 3 | | | | | | | | Pierce, DamageBonus 0.1 |
| frost | M | 3 | | | | | | | | OnHit: 감속 20% 60틱 |
| homing | M | 5 | | | | | | | | Homing(선회각 제한) |
| overheat_x3 | M | 2 | | | | | | | | 임계 배율 2 → 3 |
| erosion | M | 5 | | | | | | | | OnHit: 받는 피해 +15% 180틱 |

사거리에 닿기까지의 대략 틱(참고, 실제 판정은 이동 거리 누적으로 한다): 마력탄 10÷14×60 ≈ 42.86틱, 거대 화염구 ≈ 53.33틱, 부메랑 편도 36틱, 얼음 송곳 45틱, 그림자 구체 80틱.

---

## 5. 지팡이 슬롯과 조립

### 5.1 규칙

| 번호 | 규칙 | 출처 | 표시 |
|---|---|---|---|
| SL-1 | 지팡이는 코어 슬롯 1개와 연결 슬롯 4개로 시작한다. 갈피의 지팡이만 연결 5개 | G 4, G 10.1, W 4.3 | [제안] |
| SL-2 | 확장 슬롯은 지팡이 하나당 평생 3칸까지 늘어난다(대성공, 슬롯 새기기) | G 4, G 9 | [제안] |
| SL-3 | 확장 칸은 연결 슬롯 뒤에 붙는다. 대성공으로 슬롯이 늘면 갈피의 밑줄이 깎아 주는 "맨 끝 칸"도 한 칸 뒤로 밀린다 | W 4.3, U 4.11("첫 줄: 코어 + 연결 4칸(갈피 5칸). 둘째 줄: 확장 칸 3") | [제안] (명세 해석) |
| SL-4 | 코어에는 투사체 블록만 들어간다 | G 4 | [제안] |
| SL-5 | 연결 슬롯에는 어떤 블록이든 들어간다. 빈 칸은 건너뛴다 | G 4 (명세 해석) | [제안] |
| SL-6 | 코어가 빈 지팡이로는 원정과 결투를 시작할 수 없다. 새로 산 +0 지팡이는 빈 조립이다 | O 1.1, U | [제안] |
| SL-7 | 새 프로필의 출발 조립: 코어에 마력탄 1개, 연결 칸은 빈다 | O 1.1 | [제안] |
| SL-8 | 조립은 지팡이마다 "출발 조립"으로 남고, 판 중에 바꾼 조립은 판이 끝나면 출발 조립으로 되돌아간다. 판 중 조립 변경은 야영에서만 한다 | O 1.1, D 2.3 | [제안]/[조율] |
| SL-9 | 판 중에는 지팡이를 바꾸지 않는다(판을 시작할 때 한 자루를 고른다) | G 2.2, G 10.1 | [제안] |

### 5.2 자료형

```csharp
public sealed class WandLoadout {
    public BlockId Core;                 // 0번 슬롯
    public BlockId[] Links;              // 길이 L = BaseLinks + ExtraSlots. 빈 칸 = BlockId.None
}
public readonly struct WandCombatProfile {        // 컴파일 문맥. 캐릭터·지팡이·변이에서 만든다
    public int CharacterNo;          // 1~5
    public int BaseLinks;            // 4, 갈피(3) 5
    public int ExtraSlots;           // 0~3
    public Fx DepthDecay;            // 0.85, 갈피 0.92
    public bool UnderlinePassive;    // 갈피만 true
    public MutationSet Mutations;    // 변이(최대 2, 초롱 3): Mirror, TwistedBarrel, GlassWand, UnstableCore, ... (과열 관련은 과열 명세)
}
```

범위: `CharacterNo` 1..5, `BaseLinks` ∈ {4, 5}, `ExtraSlots` 0..3, `L` 4..8. `Links.Length != L`이면 컴파일 오류 `E_SLOT_COUNT_MISMATCH` [임시].

---

## 6. 컴파일러

### 6.1 블록 종류별 규칙 (G 4 표) [제안]

| 블록 종류 | 규칙 |
|---|---|
| 코어 | 투사체 블록만 들어간다 |
| 변형 | 현재 스코프의 모든 투사체에 붙는다(누적) |
| 분열 | 현재 스코프의 투사체를 N개 부채꼴로 복제. 한 스코프에 1개만 유효하고, 두 번째는 경고와 함께 무시 |
| 적중 시 발동 | 새 스코프를 열고, 뒤에 오는 블록은 모두 그 안에 들어간다 |
| 투사체(연결 슬롯) | 트리거 뒤 첫 투사체는 그 스코프의 주인, 그 외에는 같은 스코프에서 동시 발사 |

명세 해석 [제안]:

- CP-1 "현재 스코프의 모든 투사체"는 슬롯 순서와 무관하다. 변형이 투사체보다 앞 칸에 있어도 같은 스코프의 투사체에 붙는다.
- CP-2 스코프는 닫히지 않는다("뒤에 오는 블록은 모두 그 안"). 그래서 스코프는 언제나 **한 줄의 사슬** S0 → S1 → S2 → S3이다. 가지가 갈라지지 않는다.
- CP-3 변형은 자기 스코프에만 붙고, 부모 스코프의 변형은 하위 스코프로 이어지지 않는다 [임시] (U3).
- CP-4 분열은 자기 스코프의 투사체만 복제한다. 하위 스코프는 하위 스코프의 분열만 받는다.
- CP-5 루트 스코프(S0)는 코어와, 첫 트리거 앞의 연결 칸 블록이다. 루트의 주인은 코어다.
- CP-6 트리거는 부모 스코프의 **투사체 하나하나**에 걸린다. 부모 스코프의 탄 각각이 첫 적중에서 하위 스코프를 한 번 실행한다 ("트리거는 투사체당 첫 적중 1회만 발동", G 4).
- CP-7 스코프의 주인의 기계적인 뜻은 (a) 그 스코프가 투사체를 하나라도 가졌는지의 기준(주인이 없으면 "트리거 뒤에 투사체가 없다" 경고), (b) 미리보기의 굵은 테 마디(U S-1101 "스코프 주인") 둘이다. 그 밖에는 주인과 다른 투사체가 똑같이 동시 발사된다 [임시] (U13).

### 6.2 안전장치 (G 4) [제안]

| 번호 | 규칙 |
|---|---|
| SF-1 | 트리거 깊이 최대 3단. 깊이 = 그 스코프 위에 있는 트리거 수(S0 = 0, 최대 S3 = 3) |
| SF-2 | 트리 실행 1회당 투사체 최대 64개. 넘으면 가장 깊은 스코프부터 생략 |
| SF-3 | 스코프가 깊어질 때마다 피해 ×0.85(갈피의 지팡이만 ×0.92) |
| SF-4 | 화면 전체 플레이어 투사체 상한 300개, 적 투사체는 별도로 150개. 생략된 만큼은 생성된 탄의 피해 배율로 환산한다. 결투의 상한은 한 사람당 150(사본 마법진의 탄 포함) (O 4.2) |
| SF-5 | 비기 한 번이 만드는 투사체는 최대 160개 (G 8) |
| SF-6 | 트리거 뒤에 투사체가 없으면 야영의 조립 화면에 경고. 그림자 구체에 "관통 +2"를 붙인 것처럼 효과가 없는 조합에도 경고 |
| SF-7 | 넷째 트리거(깊이 3인 스코프 안의 트리거)는 무시하고 경고한다. 그 뒤 블록은 깊이 3 스코프에 그대로 남는다 [임시] (U5) |

### 6.3 출력 자료형

```csharp
public sealed class CompiledSpell {
    public bool IsValid;                        // 오류가 하나라도 있으면 false (출발 불가)
    public int FireIntervalTicks;               // 코어 블록의 간격 (G 4)
    public SpellScope[] Scopes;                 // 길이 1~4, Scopes[d].Depth == d
    public Fx HeatPerShot;                      // 7절. 변이 '거울상'의 +50%는 넣지 않은 값
    public int InstancesTotal;                  // 가지치기 전 트리 실행 1회의 투사체 수(거울상 없음)
    public int InstancesTotalMirrored;          // 거울상(J, 마력 방출)일 때
    public int MaxDepthNormal, MaxDepthMirrored; // 64 상한 가지치기 뒤 실행할 가장 깊은 스코프
    public Fx StandardT;                        // 14.2절
    public CompileDiagnostic[] Diagnostics;     // 슬롯 순서대로
    public ulong Hash;                          // 결정론 확인용(슬롯 배열 + 문맥의 해시)
}
public sealed class SpellScope {
    public int Depth;                 // 0..3
    public int TriggerSlot;           // 이 스코프를 연 트리거의 슬롯 번호. S0은 -1
    public int OwnerSlot;             // 주인 투사체의 슬롯. 없으면 -1
    public ScopeProjectile[] Projectiles; // 슬롯 순서. (BlockId, Slot)
    public ScopeModifiers Mods;
    public int SplitCount;            // 1, 2, 3
    public int SplitSlot;             // 유효한 분열의 슬롯. 없으면 -1
    public Fx HeatSum;                // 이 스코프 블록 열의 합(밑줄 반영)
    public Fx Heat;                   // HeatSum × 분열 배수
    public int InstancesPerParent;    // |Projectiles| × SplitCount
    public int InstancesTotal;        // 부모 InstancesTotal × InstancesPerParent (S0은 InstancesPerParent)
    public Fx DepthMultiplier;        // decay^Depth
}
public struct ScopeModifiers {
    public Fx DamageBonusSum;   // Σ(+0.2 탄환 크기, +0.1 관통)
    public int BounceCount;     // 벽 튕김 수 × 2 [임시] (U6)
    public int PierceCount;     // 관통 +2 수 × 2 (U7)
    public int SizeDoublings;   // 탄환 크기 2배 수 (U8)
    public bool Homing, Frost, Overheat3, Erosion;
    public int[] SourceSlots;   // 미리보기용
}
public readonly struct CompileDiagnostic { public DiagCode Code; public DiagSeverity Severity; public int Slot; public int ScopeDepth; }
```

`DiagCode`와 이름 표 키(문구는 이름 표에서 읽는다, W 0):

| 코드 | 심각도 | 이름 표 키 | 조건 | 출처 |
|---|---|---|---|---|
| `E_CORE_EMPTY` | 오류 | `warn.core_empty` ("코어에 투사체 블록을 끼워야 출발할 수 있다") | 코어가 빔 | O 1.1, U 4.x |
| `E_CORE_NOT_PROJECTILE` | 오류 | `warn.core_not_projectile` | 코어에 투사체가 아닌 블록(UI가 막지만 저장 파일·통신 방어) | G 4 |
| `E_SLOT_COUNT_MISMATCH` | 오류 | — (개발용) | 연결 칸 수가 문맥과 다름 | [임시] |
| `W_TRIGGER_NO_PROJECTILE` | 경고 | `warn.trigger_no_projectile` ("트리거 뒤에 투사체가 없다") | 하위 스코프에 투사체 0 | G 4, U K-71 |
| `W_NO_EFFECT` | 경고 | `warn.no_effect` ("효과 없는 조합") | 변형이 스코프 안 어떤 투사체에도 효과가 없음(6.5절) | G 4, U K-71 |
| `W_SPLIT_DUPLICATE` | 경고 | `warn.split_duplicate` | 같은 스코프의 두 번째 이후 분열 | G 4 |
| `W_TRIGGER_DEPTH` | 경고 | `warn.trigger_depth` | 넷째 트리거 | SF-7 [임시] |
| `W_PROJECTILE_CAP` | 경고 | `warn.projectile_cap` (미리보기의 "생략") | 64 상한으로 스코프 생략 | G 4, U 4.11 |

### 6.4 알고리즘 (의사코드)

```text
Compile(loadout, prof) -> CompiledSpell
  diags = []
  L = prof.BaseLinks + prof.ExtraSlots
  if loadout.Links.Length != L: return Invalid(E_SLOT_COUNT_MISMATCH)
  if loadout.Core == None: return Invalid(E_CORE_EMPTY)
  if Def(loadout.Core).Kind != Projectile: return Invalid(E_CORE_NOT_PROJECTILE)

  S = [ NewScope(depth=0, triggerSlot=-1) ]
  cur = S[0]
  AddProjectile(cur, loadout.Core, slot=0)          # 루트의 주인 = 코어
  cur.HeatSum += Def(core).Heat

  for slot in 1..L:                                  # 슬롯 순서 = 실행 순서
    b = loadout.Links[slot-1]
    if b == None: continue
    d = Def(b)
    h = d.Heat
    if prof.UnderlinePassive and slot == L and d.Kind != Control:   # 갈피 밑줄 (7.2)
        h = h - min(5, ceil(h / 2))
    switch d.Kind:
      Projectile:
        AddProjectile(cur, b, slot)                  # 첫 투사체가 주인
        cur.HeatSum += h
      Modifier:
        AccumulateModifier(cur.Mods, d, slot)        # 누적
        cur.HeatSum += h
      Control when d.Control == Split:
        if cur.SplitCount == 1: cur.SplitCount = d.SplitCount; cur.SplitSlot = slot; cur.HeatMul = d.HeatMultiplier
        else: diags += W_SPLIT_DUPLICATE(slot)       # 무시
      Control when d.Control == Trigger:
        if cur.Depth == 3: diags += W_TRIGGER_DEPTH(slot); continue
        nxt = NewScope(depth=cur.Depth+1, triggerSlot=slot)
        S += nxt; cur = nxt

  for s in S:
    s.Heat = s.HeatSum * s.HeatMul                    # 분열 배수(없으면 1)
    s.InstancesPerParent = |s.Projectiles| * s.SplitCount
    s.InstancesTotal = (s.Depth == 0) ? s.InstancesPerParent : S[s.Depth-1].InstancesTotal * s.InstancesPerParent
    s.DepthMultiplier = DecayTable(prof.DepthDecay)[s.Depth]
    if s.Depth > 0 and |s.Projectiles| == 0: diags += W_TRIGGER_NO_PROJECTILE(s.TriggerSlot)
    CheckNoEffect(s, diags)                           # 6.5

  HeatPerShot = Σ s.Heat                              # 7.1
  InstancesTotal          = Σ s.InstancesTotal
  InstancesTotalMirrored  = 2 * InstancesTotal       # 거울상은 루트를 두 배로, 자손도 두 배
  MaxDepthNormal   = Prune(S, rootFactor=1)
  MaxDepthMirrored = Prune(S, rootFactor=2)
  if MaxDepthNormal < |S|-1: diags += W_PROJECTILE_CAP(scope=MaxDepthNormal+1)
  diags 정렬: 슬롯 번호 오름차순, 같으면 코드 값 오름차순
  return CompiledSpell{...}

Prune(S, rootFactor, cap=64) -> maxDepth
  for d from |S|-1 down to 0:
    total = Σ_{k<=d} S[k].InstancesTotal * rootFactor
    if total <= cap: return d
  return 0      # 루트만으로 64를 넘는 일은 없다(6.6의 상한 계산). 그래도 넘으면 실행 시 앞에서부터 64개만 만든다
```

- 가지치기 단위는 **스코프 전체**다(가장 깊은 스코프를 통째로 뺀다) [제안] (G 4 "가장 깊은 스코프부터 생략", 명세 해석).
- 64개 상한으로 생략한 스코프의 피해는 보전하지 않는다 [임시] (U15). (300개 화면 상한의 보전은 8.2절.)
- 열(HeatPerShot)은 가지치기와 무관하게 모든 스코프를 센다 [제안] (G 5 "스코프별 … 의 합", 명세 해석).

### 6.5 "효과 없는 조합" 검사 (`CheckNoEffect`)

| 변형 | 효과가 없는 투사체 | 표시 |
|---|---|---|
| 관통 +2 | 그림자 구체(이미 적을 몇이든 지나간다), 연쇄 번개(즉발) | 그림자 구체 [제안] (G 10.3), 연쇄 번개 [임시] (U22) |
| 벽 튕김 | 연쇄 번개(즉발) | [임시] (U22) |
| 유도 | 연쇄 번개(즉발) | [임시] (U22) |

- 경고는 그 변형이 **스코프 안의 모든 투사체에** 효과가 없을 때만 낸다. 마력탄 + 그림자 구체 + 관통 +2는 마력탄에 효과가 있으므로 경고하지 않는다 [제안] (명세 해석).
- 투사체가 없는 스코프의 변형은 `W_TRIGGER_NO_PROJECTILE` 하나로 알리고 `W_NO_EFFECT`를 따로 내지 않는다 [임시].

### 6.6 상한 계산 (검산)

- 루트만의 최대: 코어 + 연결 8칸이 모두 투사체(갈피 5 + 확장 3) = 9발, 3갈래 ×3 = 27, 거울상 ×2 = 54 ≤ 64. 그래서 S0은 절대 생략되지 않는다 [제안] (명세 해석).

---

## 7. 발사 1회의 열 (컴파일러가 계산하는 부분)

열 게이지의 누적·냉각·임계·잠금은 과열 명세가 정한다. 이 절은 컴파일러가 내보내는 `HeatPerShot`과 그 계산 규칙만 정한다.

### 7.1 규칙

| 번호 | 규칙 | 출처 | 표시 |
|---|---|---|---|
| HT-1 | 발사 1회의 열 = 스코프별 (블록 열 합 × 그 스코프의 분열 배수)의 합. 분열 배수는 2갈래 ×1.6, 3갈래 ×2.2 | G 5 | [제안] |
| HT-2 | 블록 열 합에는 투사체와 변형의 열을 더한다. 적중 시 발동은 0, 분열은 열을 더하지 않고 배수만 준다 | G 10.3 (명세 해석) | [제안] |
| HT-3 | 하위 스코프의 열에는 부모의 분열 배수를 곱하지 않는다(그 스코프의 분열 배수만) | G 5 (명세 해석) | [제안] |
| HT-4 | 열은 실제로 맞혔는지와 무관하게 발사할 때 모든 스코프 분을 더한다 | G 5 (명세 해석) | [제안] |
| HT-5 | 갈피의 밑줄: 연결 슬롯 맨 끝 칸 블록의 열이 절반(올림)만큼 깎인다. 깎이는 양 = 그 블록 열의 절반(올림), 최대 5. 분열 블록의 배수는 깎은 뒤의 값에 곱한다 | W 4.3, G 5, G 10.1 | [제안] |
| HT-6 | "맨 끝 칸"은 확장을 포함한 마지막 연결 칸(L번)이다. 그 칸이 비었거나 분열·트리거면 깎는 것이 없다 | W 4.3 (SL-3), [임시] (U21) | [임시] |
| HT-7 | 변이 거울상: 발사당 열 +50%. 실행 시 `HeatPerShot × 1.5` | G 9 | [제안] |
| HT-8 | 모든 비기는 열을 올리지 않는다(`heatExempt = true`) | G 8 | [제안] |
| HT-9 | 사본 마법진의 따라 쏘기는 열 없음 | W 4.3, G 8 | [제안] |
| HT-10 | 스쳐 피하기 보상: 다음 발사 1회는 열이 오르지 않는다(결투에서는 없다) | G 6, O 4.3 | [제안] |
| HT-11 | 열 값은 Fx로 든다(분열 배수 때문에 소수가 생긴다. 예: 4 × 2.2 = 8.8) | 명세 해석 | [제안] |

밑줄 표 (W 4.3):

| 맨 끝 칸의 블록 | 원래 열 | 깎이는 양 | 실제 열 |
|---|---|---|---|
| 변형 (+3) | +3 | 2 | +1 |
| 변형 (+5) | +5 | 3 | +2 |
| 투사체 (열 8) | 8 | 4 | 4 |
| 투사체 (열 14) | 14 | 5 | 9 |
| 투사체 (열 18) | 18 | 5 | 13 |

(+2 변형은 깎이는 양 1, 실제 +1. 열 10 투사체는 5, 실제 5. 열 12 투사체는 6이 아니라 상한 5, 실제 7. 열 4 마력탄은 2, 실제 2 — 위 식에서 따라 나오는 값 [제안].)

### 7.2 과열 명세로 넘기는 값

- `HeatPerShot`(Fx), 거울상 여부, `FireIntervalTicks`.
- 임계 판정·잠금은 과열 명세: "이번 발사를 더한 뒤의 열이 임계 시작값(85) 이상이면 그 발사는 피해 2배", "100을 넘긴 발사는 임계로 나가고 직후 3초 동안 발사 불가" (G 5) [제안]. 이 명세는 그 결과(`isCrit`)를 `Execute`에 받는다.
- 야영 수치 칸: 발사당 열, 임계 사격 가능 횟수(0이면 경고), 허수아비 DPS (G 13, U 4.11) [제안]. "임계 사격 가능 횟수"의 계산식은 과열 명세가 정한다 [임시] (U20).

---

## 8. 투사체 수 상한

### 8.1 트리 실행 1회 64개

- 6.4절의 `Prune`이 정한 `MaxDepth`보다 깊은 스코프는 실행하지 않는다. 트리거가 걸린 탄이 맞아도 그 깊이의 하위 스코프는 만들지 않는다 [제안].
- 거울상은 64개 상한에 포함한다 (G 9) [제안]. 거울상은 J 발사와 마력 방출에만 적용한다 (G 9) [제안] → `MaxDepthMirrored`를 그 두 경우에만 쓴다.
- 사본 마법진의 트리 실행 1회당 64개 상한은 사본에도 따로 적용한다 (W 4.3) [제안].
- 비기 한 번 160개 상한은 비기 시스템이 `ExecuteRequest.Budget`으로 넘긴다. `Execute`는 남은 예산보다 많이 만들지 않고 만든 수를 돌려준다. 예산이 모자라면 실행 순서(8.3)의 뒤쪽부터 만들지 않는다 [임시] (U15).

### 8.2 화면 상한과 보전

| 모드 | 상한 | 출처 |
|---|---|---|
| 무한 웨이브 | 플레이어 쪽 300 (사본 마법진 포함, W 4.3), 적 쪽 150 별도 | G 4, M 1.1 |
| 결투 | 한 사람당 150 (사본 마법진 포함) | O 4.2 |
| 적(허상의 복사 포함) | 150 | G 4, M 1.1 |

보전 규칙 (G 4 "생략된 만큼은 생성된 탄의 피해 배율로 환산한다") [제안], 세부는 [임시] (U15):

```text
SpawnBatch(requests[N], faction):              # 한 번에 생기는 묶음(루트 펼침 1회, 또는 하위 스코프 1회)
  free = Cap(faction) - Live(faction)
  M = clamp(free, 0, N)
  if M == N: spawn all; return
  if M == 0: spawn none; emit event spell.cap_dropped(N); return
  comp = Fx(N) / Fx(M)                          # 보전 배율
  spawn requests[0 .. M-1] (실행 순서 앞쪽부터), 각 탄의 CompensationMul *= comp
  emit event spell.cap_compensated(N, M)
```

### 8.3 실행 순서 (결정론)

- 스코프 안의 투사체: 슬롯 번호 오름차순. 같은 투사체의 분열 복제: 부채꼴 각도 오름차순(−15, 0, +15 / −10, +10). 거울상: 앞쪽 묶음 전부 → 뒤쪽 묶음 전부 [임시] (U37).
- 투사체 id는 판 안에서 1씩 늘어나는 정수이고 생성 순서와 같다. 갱신·충돌은 id 오름차순 [제안] (O 3.1 "반복 순서가 정해진 자료구조만").

---

## 9. 실행 함수 `Execute`

### 9.1 서명

원문: 실행 함수는 처음부터 `Execute(트리, 위치, 방향, 피해 배율, 열 면제 여부, 진영)` 형태로 만든다. 비기, 갈피의 사본 마법진, 보스(스승의 허상)가 같은 함수를 쓴다 (G 4) [제안]. 허상은 같은 함수를 진영만 바꿔 호출한다 (M 3.4) [확정: M 3.4 표기].

원문의 여섯 인자를 그대로 두고, 원문이 다른 절에서 요구하는 값(임계, 발사 식별자, 출처, 예산, 모드)을 옵션 구조체로 붙인다 [제안] (17절 C13).

```csharp
public readonly struct ExecuteOptions {
    public bool IsCrit;              // 과열 시스템이 판정(J 발사만). 비기·사본은 false (G 4 "비기는 임계 판정을 받지 않는다")
    public bool GlassCritBonus;      // 유리 지팡이 변이의 임계 +1이 지금 켜져 있는가(결투에서 피격 후 3초 꺼짐)
    public int CastId;               // 발사 1회 식별자(콤보 '발사 1회당 최대 +3'용)
    public SourceKind Source;        // J, ManaDischarge, SkillOther, CopyCircle, EnemyCopy
    public int EnhanceStage;         // 실행 순간의 실효 단계(유리 지팡이의 판 안 임시 하락 반영) — 스냅숏
    public Fx CharacterMul;          // 실행 순간의 캐릭터 배율(그믐) — 스냅숏 (11.2)
    public int ProjectileBudget;     // 비기 160 상한의 남은 몫. 제한 없음 = int.MaxValue
    public GameMode Mode;            // Wave, Duel
    public EntityId AimTarget;       // 결투 조준 보정 대상. 없으면 None
    public IRng MutationRng;         // 판 씨앗에서 갈라 낸 '변이' 난수기 (G 9)
}
public readonly struct ExecuteResult { public Fx HeatToAdd; public int Spawned; public int Omitted; public bool SelfDetonated; }

ExecuteResult Execute(CompiledSpell tree, FxVec2 position, Angle direction, Fx damageMul,
                      bool heatExempt, Faction faction, in ExecuteOptions opt);
```

- `damageMul`은 피해 공식의 "비기 배율" 자리다. J 발사 1, 마력 방출 2(2T), 사본 마법진 0.5, 부채 전개의 곁가지 0.7, 팔방 전개의 곁가지 0.6 등은 호출하는 쪽이 넣는다 (G 8, W 4.3) [제안].
- `HeatToAdd`는 `heatExempt`가 거짓일 때만 `HeatPerShot × (거울상 && Source ∈ {J, ManaDischarge} ? 1.5 : 1)`이다. 실제로 게이지에 더하는 것은 과열 시스템이다 [제안].

### 9.2 절차

```text
Execute(tree, pos, dir, damageMul, heatExempt, faction, opt):
  if !tree.IsValid: return {0}
  mirror = Mutations.Mirror and opt.Source in {J, ManaDischarge}
  maxDepth = mirror ? tree.MaxDepthMirrored : tree.MaxDepthNormal

  # 1) 불안정 핵 (무한 웨이브, J 발사): 5% 자폭 판정 — 난수 1회
  if Mutations.UnstableCore and opt.Mode == Wave and opt.Source == J:
      if opt.MutationRng.NextInt(100) < 5:
          emit mutation.unstable_core.self_detonate(pos)    # 내 자리 폭발, 잠금 2초 + 콤보 0 (G 9). 처리는 과열/콤보 명세
          return { HeatToAdd: 0, SelfDetonated: true }       # 이 발사의 탄은 나가지 않는다 [임시] (U11)

  # 2) 루트 펼치기
  baseDir = dir
  if opt.Mode == Duel and opt.AimTarget != None and opt.Source in {J, 방향 있는 비기}:
      assistedDir = Clamp(AngleTo(pos, target) - dir, -1000, +1000) + dir     # 최대 10도 (O 4.4)
  reqs = []
  for p in S0.Projectiles (slot 순):
     for off in FanOffsets(S0.SplitCount):                     # 9.3
        isCenter = (off == 0) or (S0.SplitCount == 1)
        d = (isCenter && 보정 있음) ? assistedDir + off : baseDir + off   # 곁가지는 보정 없음 (O 4.4)
        reqs += Req(p, d, depth=0)
  if mirror: reqs += [ r with dir = r.dir + 18000, 보정 없음 ] for r in reqs   # [임시] (U37)

  # 3) 뒤틀린 총열: 각 루트 탄에 ±25도 흔들림 — 결투에서는 보정 뒤에 더한다 (O 4.4)
  if Mutations.TwistedBarrel:
     for r in reqs (순서대로): r.dir += opt.MutationRng.NextIntInclusive(-2500, +2500)   # [임시] (U16)

  # 4) 예산과 화면 상한
  reqs = reqs[0 .. min(|reqs|, opt.ProjectileBudget)-1]
  SpawnBatch(reqs, faction)            # 8.2. 각 탄에 아래 '탄 문맥'을 실어 보낸다

  탄 문맥(모든 루트 탄과, 나중에 생길 자손이 물려받음):
    CastId, Source, faction, IsCrit, GlassCritBonus, EnhanceStage, CharacterMul, damageMul,
    Mode, maxDepth, tree 참조, ScopeDepth = 0

  return { HeatToAdd: heatExempt ? 0 : tree.HeatPerShot * (mirror ? 1.5 : 1), Spawned, Omitted }
```

### 9.3 부채꼴 각도 (G 4) [제안]

| SplitCount | 각도(도) | 센티도 |
|---|---|---|
| 1 | 0 | 0 |
| 2 | −10, +10 | −1000, +1000 |
| 3 | −15, 0, +15 | −1500, 0, +1500 |

- 2갈래에는 가운데 탄이 없다. 결투의 조준 보정은 두 탄 모두 받지 않는다 [임시] (U24).
- 분열은 복제할 때마다 그 투사체의 다른 성질(변형, 트리거)을 그대로 갖는다 [제안] (명세 해석).

### 9.4 하위 스코프 실행 (트리거)

```text
OnFirstHit(proj, hitPoint, hitDir, hitEnemy):        # 투사체당 한 번만 (G 4)
  if proj.TriggerFired: return
  proj.TriggerFired = true
  child = proj.Tree.Scopes[proj.ScopeDepth + 1] if exists else null
  if child == null or child.Depth > proj.MaxDepth or |child.Projectiles| == 0: return
  emit scope.triggered(depth = child.Depth, at = hitPoint)
  reqs = []
  for p in child.Projectiles (slot 순):
     for off in FanOffsets(child.SplitCount):
        reqs += Req(p, hitDir + off, depth = child.Depth, spawnAt = hitPoint, ignoreEnemy = hitEnemy)
  SpawnBatch(reqs, proj.Faction)    # 탄 문맥을 그대로 물려받는다(CastId, IsCrit, 단계, 캐릭터 배율, damageMul, maxDepth)
```

- 트리거는 투사체당 첫 적중 1회만 발동한다. 폭발은 중심에서 1회. 적을 지나가는 탄(그림자 구체, 관통탄)도 첫 적중에서만 발동한다 (G 4) [제안].
- 하위 스코프가 나오는 자리 = 첫 적중 지점(폭발이면 폭발 중심, 연쇄 번개면 첫 대상의 중심). 방향 = 부모 탄이 그 순간 날던 방향(연쇄 번개는 발사 방향) [임시] (U13).
- 하위 스코프의 탄은 자기를 만든 그 적을 맞히지 않는다(같은 자리에서 생겨 곧바로 다시 맞는 것을 막는다) [임시] (U13).
- 하위 스코프의 탄은 생긴 틱에는 움직이지 않고 다음 틱부터 움직인다 [임시] (U30).
- 임계 여부, 강화 단계, 캐릭터 배율은 발사 순간의 값을 자손도 쓴다 [임시] (U14).
- 거대 화염구의 폭발이 적을 하나도 맞히지 않으면(벽·사거리 끝에서 빈 땅 폭발) 트리거는 발동하지 않는다 [임시] (U27).

---

## 10. 투사체의 동작

### 10.1 런타임 자료형

```csharp
public struct ProjectileState {
    public int Id; public int CastId; public Faction Faction; public SourceKind Source; public GameMode Mode;
    public BlockId Block; public byte ScopeDepth; public byte MaxDepth; public CompiledSpell Tree;
    public FxVec2 Pos; public Angle Dir; public Fx SpeedPerTick; public Fx Traveled; public Fx Range; public Fx Radius;
    public ProjPhase Phase;            // Pending, Flying, Returning, Dead
    public Element Element;            // = Def(Block).Element (EL-1)
    public bool IsCrit; public bool GlassCritBonus; public int EnhanceStage; public Fx CharacterMul;
    public Fx DamageMul;               // Execute의 damageMul
    public Fx CompensationMul;         // 8.2 보전, 기본 1
    public int BouncesLeft, PierceLeft; public bool Homing; public EntityId HomingTarget;
    public bool AppliesFrost, AppliesErosion, Overheat3; public Fx DamageBonusSum;
    public bool TriggerFired; public EntityId IgnoreEnemy;
    public SortedIntList HitSet;       // 이미 맞힌 적(그림자 구체·관통·부메랑의 다리별)
    public byte BoomerangLeg;          // 0 갈 때, 1 올 때
}
```

### 10.2 상태 기계

| 상태 | 들어오는 조건 | 하는 일 | 나가는 전이 |
|---|---|---|---|
| Pending | `SpawnBatch`가 만듦 | 생긴 틱에는 움직이지 않는다 [임시] | 다음 틱 → Flying (즉발이면 생긴 틱에 Resolve 후 Dead) |
| Flying | | 유도 조향 → 이동 → 벽 → 적 충돌 | 사거리 도달 / 벽(튕김 없음) / 관통 소진 → Dead(폭발 블록은 폭발 후). 부메랑의 편도 끝·벽 → Returning |
| Returning (부메랑만) | 편도 6칸 도달 또는 벽 [임시] | 쏜 사람의 지금 위치로 향해 같은 속도로 이동 | 쏜 사람 반경 0.5칸 안 / 돌아오는 거리 9칸 초과 → Dead [임시] (U12) |
| Dead | | 다음 틱에 목록에서 뺀다 | — |

### 10.3 한 틱의 처리 순서 [제안] (명세 해석, 결정론)

1. Pending → Flying 전환(전 틱에 생긴 것).
2. id 오름차순으로 각 탄: (a) 유도 조향 (b) 이동: `prev = Pos; Pos += Dir × SpeedPerTick; Traveled += SpeedPerTick` (c) 벽·기둥 판정(선분) (d) 적 판정: 선분 `prev → Pos`를 반지름 `Radius + 적 반지름`으로 쓸어서 닿은 적을 **선분 위 거리 오름차순, 같으면 적 id 오름차순**으로 처리 (e) 사거리 판정.
3. 적중 처리에서 생긴 하위 스코프 탄과 폭발은 같은 틱 안에서 처리 목록 끝에 붙는다(하위 탄은 Pending).
4. 상태 효과(감속·잠식)의 남은 틱 감소는 상태 명세 순서를 따른다(12.4).

### 10.4 블록별 동작

| 블록 | 동작 | 출처·표시 |
|---|---|---|
| 마력탄 | 직선. 첫 적에게 맞고 사라진다(관통이 없으면) | G 10.3 [제안] |
| 거대 화염구 | 직선. 적에 맞으면 그 자리에서 반경 2 폭발. 폭발 안의 적 모두(맞은 적 포함, 각 1회)가 탄의 피해를 받는다. 벽이나 사거리 끝에서도 그 자리에서 터진다 | 반경 2 [제안] (G 10.3). 폭발 판정 방식 [임시] (U11). 멈춘 자리에서 터지는 것은 M 3.4의 허상 복사 규칙("탄이 멈춘 자리에 0.8초 예고 원을 먼저 그린 뒤 터진다")에서 미룬 해석 [제안] |
| 연쇄 번개 | 즉발. 발사 방향 사거리 7의 광선에서 가장 가까운 적 1체 → 그 적에서 반경 3 안의, 아직 맞지 않은 가장 가까운 적으로 3회 전이(전이마다 ×0.7). 합 최대 4타. 전이 대상이 없으면 멈춘다. 늑대(덤불 늑대)의 탄 회피 대시를 무시한다 | 3회 전이·반경 3·×0.7·늑대 회피 무시 [제안] (G 10.3). 광선 반폭, 대상 고르는 법, "3회 전이 = 4타" [임시] (U10) |
| 부메랑 | 속도 10, 편도 6. 가는 길과 오는 길에 각각 타격(같은 적을 두 번 맞힐 수 있다 = 왕복 2회 타격) | [제안] (G 10.3). 돌아오는 법 [임시] (U12) |
| 얼음 송곳 | 직선. 맞은 적 1초(60틱) 이동 −30% | [조율] (D 2.5) |
| 그림자 구체 | 느리고 큰 탄. 적을 몇이든 지나가고 벽에서 사라진다. 같은 적은 한 번만 맞는다. 사거리 끝에서 터지지 않고 사라진다. 판정 반지름 0.25 | [조율] (D 2.5), [제안] (G 10.3, E 13.3, M 3.4) |
| 벽 튕김 | 벽(전장 가장자리, 결투장 선돌 기둥)에 닿으면 입사각 반사로 튕긴다. 튕긴 뒤에도 이동 거리는 이어서 센다. 성에의 얼음 병풍에는 튕기지 않고 지나간다 | 기둥 [제안] (O 4.1, W 2.6), 얼음 병풍 [제안] (W 4.4), 튕김 횟수 [임시] (U6) |
| 탄환 크기 2배 | 판정 반지름 ×2, 피해 +20%(합산) | 피해 [제안] (G 10.3), 크기 [임시] (U8) |
| 관통 +2 | 적을 2체 더 지나간다(최대 3체를 맞힘). 같은 적은 한 번 | [제안] (명세 해석: 이름 그대로), 겹침 [임시] (U7) |
| 서리 | 맞은 적 1초(60틱) 이동 −20% | [조율] (D 2.5) |
| 유도 | 선회각 제한 안에서 가장 가까운 적을 향해 방향을 튼다 | [제안] (G 10.3), 수치 [임시] (U9) |
| 과열 3배 | 임계 배율을 2에서 3으로 교체 | [제안] (G 4, G 10.3), 적용 범위 [임시] (U4) |
| 잠식 | 맞은 적이 3초(180틱) 동안 받는 피해 +15% | [조율] (D 2.5) |

부가 규칙:

- 서리·잠식·얼음 송곳의 효과는 그 탄이 피해를 준 모든 적(폭발 범위 안, 연쇄 전이 대상 포함)에게 건다 [임시] (U28).
- 관통·그림자 구체·부메랑은 `HitSet`으로 같은 적을 다시 맞히지 않는다(부메랑은 다리마다 `HitSet`을 새로 쓴다).
- 땅속에 있는 적은 피해도 효과도 받지 않는다(탄은 그 적을 지나간다) (M 2.6, W 3.6) [제안].
- 비행하는 적도 탄에 맞는다(그림자가 판정 위치) (M 2.6) [제안].
- 철갑(선돌지기 정면 120도: 임계가 아닌 피해 절반)과 밤삼킴의 마디 배율·"한 발당 최대 3마디"는 적 쪽 규칙이다. 피해 공식(11절)이 끝난 값에 적 명세의 `ITargetDamageModifier`를 곱한다 (M 2.2, M 3.8) [제안].

---

## 11. 피해 공식

### 11.1 공식 (G 4) [제안]

`기본 × 강화 배율 × (1 + 변형 보너스 합) × 변이 배율 × 임계 배율 × 비기 배율 × 감쇠^깊이 × 속성 배율 × 대상 상태 배율 × 캐릭터 배율`

| 항 | 값 | 출처 | 표시 |
|---|---|---|---|
| 기본 | 맞힌 투사체 블록의 피해(마력탄 10 …) | G 10.3 | [제안] |
| 강화 배율 | 무한 웨이브 1.15^단계(+13에서 약 6.2배). 결투 `1 + 0.03 × 단계`(+13 = 1.39배) | G 4, G 9, O 4.2 | [제안] |
| 변형 보너스 합 | 그 스코프 변형의 피해 보너스 합(탄환 크기 2배 +0.2, 관통 +2 +0.1) | G 4, G 10.3 | [제안] |
| 변이 배율 | 뒤틀린 총열 ×1.6. 없으면 1 | G 9 | [제안] |
| 임계 배율 | 임계이면 (과열 3배가 있으면 3, 없으면 2) + (유리 지팡이 변이가 있으면 1). 임계가 아니면 1. 결투에서 유리 지팡이의 +1은 맞은 뒤 3초 동안 꺼진다 | G 4, G 9, O 4.2 | [제안] |
| 비기 배율 | `Execute`의 `damageMul`. 비기 배율은 임계 배율과 같이 적용하지 않는다(비기는 임계 판정을 받지 않는다) | G 4, G 8 | [제안] |
| 감쇠^깊이 | 0.85^깊이(갈피 0.92^깊이). 깊이 0이면 1 | G 4 | [제안] |
| 속성 배율 | 약점 1.25 / 저항 0.8 / 그 밖 1. 결투에서는 1 | G 4, D 2.5, O 4.2 | [조율] |
| 대상 상태 배율 | 잠식에 걸린 적이면 1.15(결투 1.08), 아니면 1 | G 4, O 4.2 | [제안] |
| 캐릭터 배율 | 그믐의 그림자 계약: 1 + 0.03 × 가진 결정 수. 다른 캐릭터는 1 | G 4, W 4.5 | [제안] |

이 명세가 더하는 항 (원문 공식 뒤에 곱한다):

| 항 | 값 | 출처 | 표시 |
|---|---|---|---|
| 전이 배율 | 연쇄 번개 k번째 전이 0.7^k (k = 0, 1, 2, 3) | G 10.3 | [제안] |
| 보전 배율 | 화면 상한 보전 N/M (8.2) | G 4 | [제안] / 방식 [임시] |

### 11.2 세부 규칙

| 번호 | 규칙 | 출처 | 표시 |
|---|---|---|---|
| DM-1 | 곱하는 순서는 위 표의 순서(왼쪽부터)로 고정한다. Fx 곱의 내림 오차까지 기기와 서버가 같아야 하기 때문이다 | G 15 (명세 해석) | [제안] |
| DM-2 | 임계 배율은 탄 하나하나가 속한 스코프의 과열 3배 유무로 정한다 | [임시] (U4) | [임시] |
| DM-3 | 그믐: 가진 결정 1개당 모든 피해 +3%(7개에서 +21%). J 발사와 비기 모두. 비기에는 그 비기가 결정을 쓴 뒤 남은 개수로 계산한다(결정 5개에서 2개짜리 비기 → +9%, 만개 → +0%). 빚 결정은 0개로 센다 | W 4.5 | [제안] |
| DM-4 | 캐릭터 배율과 강화 단계는 `Execute` 순간에 스냅숏을 뜬다(자손 탄도 같은 값) | [임시] (U14) | [임시] |
| DM-5 | 잠식의 +15%는 겹치지 않고 시간만 갱신한다. 잠식을 건 그 탄의 피해에는 적용하지 않고 다음 피해부터 적용한다 | G 10.3 | [제안] |
| DM-6 | 잠식의 +15%는 그 적이 받는 모든 피해(다른 탄, 비기, 패시브)에 적용한다 | [임시] (U29) | [임시] |
| DM-7 | 비기로 나간 탄(`damageMul` ≠ 1이든 아니든 Source가 비기)은 `IsCrit = false` | G 8 | [제안] |
| DM-8 | 블록을 거치지 않는 피해(패시브·고유 비기)는 이 공식의 "기본"을 nT로 두고, 변형 보너스·변이·임계·감쇠·전이 항을 빼고 계산한다. 속성 배율은 EL-4의 캐릭터 속성으로 | G 8, W 3.5 (명세 해석) | [제안] |
| DM-9 | 적 체력과 피해는 Fx로 든다. 적 체력 배율 H(w)는 적 명세 | M 4.2 | [임시] (U19) |

### 11.3 계산 함수 (의사코드)

```text
ComputeHitDamage(proj, enemy, hopIndex) -> Fx
  d = Def(proj.Block).BaseDamage
  d *= EnhanceTable(proj.Mode)[proj.EnhanceStage]          # 2.2 표
  d *= (One + proj.DamageBonusSum)
  d *= MutationMul(proj)                                    # 뒤틀린 총열 1.6
  if proj.IsCrit: d *= Fx((proj.Overheat3 ? 3 : 2) + (proj.GlassCritBonus ? 1 : 0))
  d *= proj.DamageMul                                       # 비기 배율
  d *= DecayTable[proj.ScopeDepth]
  d *= ElementMultiplier(proj.Element, Affinity(enemy), proj.Mode)
  d *= enemy.HasErosion ? (proj.Mode == Duel ? 1.08 : 1.15) : 1
  d *= proj.CharacterMul
  d *= ChainHopTable[hopIndex]                              # 연쇄 번개가 아니면 hopIndex = 0 → 1
  d *= proj.CompensationMul
  d = TargetModifiers(enemy, proj, d)                       # 철갑, 마디 등 (적 명세)
  return d
```

강화 배율 표 (Fx Raw, 2.2절 규칙):

| 단계 | 무한 웨이브 1.15^n | Raw | 결투 1+0.03n | Raw |
|---|---|---|---|---|
| 0 | 1.000000 | 4,294,967,296 | 1.00 | 4,294,967,296 |
| 1 | 1.150000 | 4,939,212,390 | 1.03 | 4,423,816,315 |
| 2 | 1.322500 | 5,680,094,249 | 1.06 | 4,552,665,334 |
| 3 | 1.520875 | 6,532,108,386 | 1.09 | 4,681,514,353 |
| 4 | 1.749006 | 7,511,924,644 | 1.12 | 4,810,363,372 |
| 5 | 2.011357 | 8,638,713,341 | 1.15 | 4,939,212,390 |
| 6 | 2.313061 | 9,934,520,342 | 1.18 | 5,068,061,409 |
| 7 | 2.660020 | 11,424,698,393 | 1.21 | 5,196,910,428 |
| 8 | 3.059023 | 13,138,403,152 | 1.24 | 5,325,759,447 |
| 9 | 3.517876 | 15,109,163,625 | 1.27 | 5,454,608,466 |
| 10 | 4.045558 | 17,375,538,169 | 1.30 | 5,583,457,485 |
| 11 | 4.652391 | 19,981,868,894 | 1.33 | 5,712,306,504 |
| 12 | 5.350250 | 22,979,149,228 | 1.36 | 5,841,155,523 |
| 13 | 6.152788 | 26,426,021,613 | 1.39 | 5,970,004,541 |

- 강화 범위 +0~+13 (D 1.3) [확정]. 유리 지팡이(무한 웨이브): 피격 시 강화 −1(웨이브당 1회, 횟수 상한 없이 +3까지), 판 안에서만 적용 (G 9) [제안] → `EnhanceStage`는 그 실효 단계.

---

## 12. 블록이 거는 상태 (감속, 잠식)

### 12.1 감속

| 번호 | 규칙 | 출처 | 표시 |
|---|---|---|---|
| SL-A | 얼음 송곳 −30% 1초, 서리 −20% 1초 | D 2.5 | [조율] |
| SL-B | 이동 감속(얼음 송곳, 서리, 달구의 불가마)은 서로 더하지 않는다. 가장 큰 값 하나만 적용하고 시간은 각자 갱신한다 | G 10.3 | [제안] |
| SL-C | 같은 감속 풀에 "중량·보스에게 감속으로 바뀐 살얼음"도 들어간다(중량 −30%, 보스 −15%) | W 3.6 | [제안] |
| SL-D | 보스에게는 절반만 들어간다(−30% → −15%, −20% → −10%, 불가마 −20% → −10%). 엘리트와 중량에게는 그대로 | G 10.3, W 3.6 | [제안] |
| SL-E | 얼음 송곳·서리의 감속은 빙결 내성 6초와 무관하게 걸린다 | M 2.6 | [제안] |
| SL-F | 땅속에 있는 적은 받지 않는다. 비행 적은 받는다 | W 3.6, M 2.6 | [제안] |
| SL-G | 결투: 감속 폭 절반(−15%, −10%), 겹치지 않는다. 상태를 바꾸는 효과는 적중 3틱 뒤에 적용한다 | O 4.2 | [제안] |
| SL-H | 허상의 복사 사격: 얼음 송곳의 감속은 플레이어에게 걸지 않는다. 서리·잠식 변형의 부가 효과도 복사하지 않는다 | M 3.4, G 10.3 | [확정: M 3.4 표기] |

상태 기계 (적 하나):

```text
SlowSlots: 출처별 칸 { IceSpike, Frost, Furnace, ShallowIceAsSlow } 각 (strength%, ticksLeft)
Apply(source, strength, ticks=60):
  s = AdjustForTag(strength, enemy.Tags)        # 보스 절반, 땅속이면 무시
  slot[source] = (s, ticks)                      # 같은 출처는 시간만 새로(세기도 새 값)
  emit status.slow.applied / refreshed
Tick:
  for each slot: ticksLeft -= 1; if 0: clear, emit status.slow.expired(source)
EffectiveSlow = max(slot.strength for active slots)     # 더하지 않는다
이동 속도 = 기본 속도 × (1 − EffectiveSlow)
```

### 12.2 잠식

```text
Erosion: (active, ticksLeft)
Apply(ticks=180):                              # 결투는 적중 3틱 뒤 (O 4.2)
  if땅속: 무시
  active = true; ticksLeft = 180               # 겹치지 않고 시간만 갱신 (G 10.3)
  # 이번 피해에는 적용하지 않는다: 적중 처리 순서 = 피해 계산 → 상태 적용
Tick: ticksLeft -= 1; if 0: active = false
DamageTakenMul = active ? 1.15 (결투 1.08) : 1
```

### 12.3 적중 1회의 처리 순서 [제안] (명세 해석)

1. 피해 계산(11.3) — 이때의 잠식 상태로 대상 상태 배율을 정한다(DM-5).
2. 피해 적용, 처치 판정.
3. 블록 상태(감속, 잠식) 적용 — 결투는 3틱 뒤 예약.
4. 트리거(첫 적중이면) → 하위 스코프.
5. 이벤트 발행(19절).

### 12.4 표시 보조 값 (코어가 내보내는 것)

- 감속 표시는 −30%이면 두 줄, −20%이면 한 줄. 두 종류가 겹치면 큰 쪽 (E 13.3) [제안] → 코어는 `EffectiveSlow`와 출처를 내보낸다.

---

## 13. 모드와 호출자별 변환

### 13.1 결투 (O 4.2, O 4.4) [제안]

| 항목 | 무한 웨이브 | 결투 |
|---|---|---|
| 강화 배율 | 1.15^단계 | 1 + 0.03 × 단계 |
| 속성 약점·저항 | 있음 | 없음 |
| 연쇄 번개 | 즉발 | 0.25초(15틱) 조준선 뒤 직선 번개. 전이 없음 |
| 유도 | 선회각 제한 | 선회각 절반, 1.5초(90틱) 뒤 유도 끊김 |
| 얼음 송곳·서리 감속 | −30% / −20% | −15% / −10%, 겹치지 않는다 |
| 잠식 | +15% 3초 | +8% 3초 |
| 상태 효과 적용 | 즉시 | 적중 3틱 뒤 |
| 화면 탄 상한 | 플레이어 300 | 한 사람당 150(사본 마법진 포함) |
| 불안정 핵 | 5% 자폭, 폭발 반경 +100% | 자폭 없음, 폭발 반경 +50% |
| 유리 지팡이 | 피격 시 강화 임시 −1 | 강화는 내려가지 않고, 맞은 뒤 3초 동안 임계 배율 +1이 꺼진다 |
| 나머지 변이 4종 | GDD | 동일 |
| 조준 보정 | 없음 | J 발사와 방향이 있는 비기는 상대 쪽으로 최대 10도. 3갈래·부채·팔방의 곁가지 탄, 폭발 지점, 벽 튕김 뒤의 탄은 보정하지 않는다. 뒤틀린 총열의 흔들림은 보정 뒤에 더한다 |
| 과열, 임계, 잠금 | GDD | 동일 |

- 결투 직선 번개의 판정: 조준선 방향으로 사거리 7의 광선, 첫 대상 1회 [임시] (U10).

### 13.2 적의 복사: 스승의 허상 (M 3.4)

GDD 규칙 그대로 [확정: M 3.4 표기]:

- 플레이어 지팡이의 컴파일 결과를 복사해 쏜다. 같은 `Execute`를 진영만 바꿔 호출한다.
- 유도는 뺀다. 연쇄 번개는 조준 예고선 0.6초(36틱) 뒤의 직선 번개로 바꾼다. 발사 1회 최대 12개, 발사 간격은 플레이어의 2.5배, 탄 속도 6, 피해 1(칸).

복사 규칙 보강 [제안] (M 3.4):

- 벽 튕김은 1회로 제한.
- 폭발 반경은 플레이어의 60%, 최대 1.5칸. 탄이 멈춘 자리에 0.8초(48틱) 예고 원을 먼저 그린 뒤 터진다.
- 부메랑은 돌아오는 길에도 판정이 있다.
- 분열로 탄이 늘어도 3칸 지점의 탄 사이가 1.0칸보다 좁아지면 갈래 사이 각도를 벌려 1.0칸을 맞춘다(R3).
- 냉기·암흑: 얼음 송곳의 감속은 플레이어에게 걸지 않는다. 그림자 구체는 느린 큰 탄(반지름 0.25) 그대로. 서리·잠식 변형의 부가 효과는 복사하지 않는다.
- 보스가 쏘는 탄은 전부 위험색이다. 스코프 깊이는 탄 가운데의 흰 점 개수(1~3개)로 읽힌다(표시 계층에 `ScopeDepth`를 내보낸다).
- 허상의 탄과 구역은 속성과 상관없이 위험색 (M 3.2) — 그래도 탄의 `Element`는 남긴다(피해는 칸 단위라 속성 배율 없음).

변환 함수 `EnemyCopyTransform(CompiledSpell player) -> CompiledSpell enemy` [제안]:

```text
copy = Clone(player)
for s in copy.Scopes:
  s.Mods.Homing = false; s.Mods.Frost = false; s.Mods.Erosion = false
  s.Mods.BounceCount = min(s.Mods.BounceCount, 1)
  s.Mods.DamageBonusSum = 0                  # 피해는 고정 1칸 [임시]
  for p in s.Projectiles: if p == ChainLightning: p.Variant = TelegraphedLine(36틱)
  if s.SplitCount >= 2: s.FanStep = max(원래 간격, 2·asin(1.0 / (2·3)))  # 3칸 지점 현 길이 ≥ 1.0칸
copy.FireIntervalTicks = ceil(player.FireIntervalTicks × 2.5)          # [임시] (U25)
copy.SpeedOverride = 6 칸/초; copy.DamageOverride = 1칸; copy.ExplosionRadius = min(r × 0.6, 1.5)
copy.Cap = 12: 64 대신 12로 Prune, 루트만으로 12를 넘으면 실행 순서 앞쪽 12개 [임시] (U26)
IceSpike: 플레이어에게 감속 없음
```

- 각도 환산: 3갈래 −15/0/+15에서 이웃한 탄 사이는 3칸 지점 현 길이 2·3·sin(7.5°) ≈ 0.783칸 < 1.0 → 간격을 2·asin(1/6) ≈ 19.19도(1,919 센티도)로 벌린다. 2갈래 ±10(사이 20도)은 2·3·sin(10°) ≈ 1.042칸 ≥ 1.0 → 그대로 [제안] (명세 해석: "탄 사이"를 현 길이로 읽음).
- 허상의 2단계 비기 흉내(부채·양익·팔방)와 3단계 만개 흉내는 비기·보스 명세. 같은 `Execute`를 쓴다.

### 13.3 사본 마법진 (갈피의 베껴 쓰기, W 4.3) [제안]

- 내 자리에 사본 마법진을 6초(360틱) 남긴다. 내가 J로 쏘면 마법진도 내 시야와 같은 방향으로 지팡이 트리를 1회 실행한다. 피해 50%, 열 없음, 콤보에 넣지 않는다.
- 0.3초(18틱)에 한 번까지만 따라 쏘고, 모두 12번 쏘면 6초가 남아 있어도 사라진다.
- `Execute(트리, 마법진 위치, 내 시야, 0.5, heatExempt=true, 플레이어 진영, Source=CopyCircle, IsCrit=false)`. 64개 상한은 사본에도 따로, 화면 300개 상한에는 함께 센다.
- 동시에 1개. 새로 깔면 이전 것은 사라지고 남은 횟수는 이어받지 않는다. 마법진은 움직이지 않고 맞지 않는다.
- 임계: 사본은 J의 임계를 따라 받지 않는다(비기에서 나온 것) [임시] (U14 참고). 거울상: 사본에는 적용하지 않는다(G 9는 J와 마력 방출만).

### 13.4 비기

- 모든 비기는 열을 올리지 않고, 임계 배율을 받지 않으며, 강화 배율은 받는다 (G 8) [제안].
- 트리를 실행하는 비기의 피해 속성은 맞힌 투사체 블록의 속성이다. 트리를 쓰지 않는 고유 비기의 피해는 캐릭터 속성이다 (G 8) [제안].
- 비기별 실행 방법(마력 방출 트리 1회 2T, 반동 사격 트리 1회, 부채 전개 세 갈래 중앙 100%·좌우 70%, 양익 사격 좌우 1회씩, 팔방 전개 8방향 정면 100%·나머지 60%, 만개 반복·각 2배, 꿰뚫는 창은 연결 슬롯을 실행하지 않음)은 비기 명세가 `Execute`를 불러 구현한다 (G 8) [제안].
- 비기로 맞힌 것은 콤보에 넣지 않는다 (G 7) → 탄 문맥의 `Source`로 콤보 시스템이 거른다.

---

## 14. 다른 시스템에 내보내는 것

### 14.1 콤보·결정

- 탄은 `CastId`, `Source`, `IsCrit`을 갖는다. 콤보 규칙(맞힐 때마다 +1, 임계 적중 +2, 발사 1회로 오르는 콤보 최대 +3, 비기·사본은 넣지 않음)은 콤보 명세 (G 7, W 4.3) [제안]. 하위 스코프·분열·거울상 탄은 부모와 같은 `CastId`다 [제안] (명세 해석).

### 14.2 표준 T

- T = "지팡이 트리 1회 실행의 피해" (G 머리말) [제안].
- 구현값 [임시] (U2): `StandardT = Σ(루트 스코프 탄 하나하나, 분열 반영, 거울상 제외) 기본 × 강화 배율 × (1 + 루트 변형 보너스 합) × 변이 배율 × 캐릭터 배율`. 하위 스코프는 적중에 달려 있어 넣지 않고, 속성·대상 상태·임계·감쇠는 넣지 않는다. 마력탄 하나 +0이면 T = 10.
- 근거: W 3.5 "불가마의 3.6T는 마력탄 기준이다. 임계 사격 4발을 열 없이 쏘면 8T" → 마력탄 1발 = 1T, 임계 1발 = 2T. "임계 유지 화력 73.5 × 0.6초 = 4.4T" → T ≈ 10.

### 14.3 강화(제물)와 드랍

- 제물 점수에 쓰는 블록 데이터: 등급(일반 +4, 희귀 +8, 영웅 +14), 속성(지팡이와 같은 속성의 블록이 하나라도 있으면 +5, 시도당 1회). "무" 블록은 어떤 지팡이 속성과도 같지 않다 (G 9) [제안], "무" 처리 [임시] (U34).
- 카드의 등급 확률(1일째 일반 78 / 희귀 22 / 영웅 0 → 4일째 이후 70 / 25 / 5)과 등급 안 균등 (G 10.3, M 6.2) [제안] → `BlockDef.Rarity`를 드랍 명세가 읽는다.
- 불안정 핵 변이는 코어가 폭발 블록(거대 화염구)일 때만 제시된다 (G 9) [제안] → `BlockDef.Flags.Explodes`를 강화 명세가 읽는다.

### 14.4 조립 화면 미리보기 자료 (U 4.11) [제안]

| 항목 | 컴파일 결과에서 |
|---|---|
| 트리 마디 | 스코프마다 투사체 마디(주인은 굵은 테), 변형은 그 스코프 투사체 마디 오른쪽 꼬리(한 줄 최대 6개, 넘으면 "+N"), 분열은 가지(2/3), 스코프는 들여쓰기 40px + 세로 묶음 선(깊이 1~3은 밝기 100/70/50%) |
| 스코프 배율 꼬리표 | `DepthMultiplier`: "×0.85", "×0.72"(0.7225를 소수 둘째 자리 내림), 갈피 "×0.92", "×0.84" |
| 생략 | `MaxDepthNormal`보다 깊은 스코프는 점선 마디 + "생략" |
| 경고 | 경고 마디에 딱지, 경고 줄은 최대 3줄(진단 정렬 순서의 앞 3개) |
| 맨 아래 한 줄 | "발사 1회 = 투사체 N개 · 열 H" (N = 가지치기 뒤 `Σ InstancesTotal`, H = `HeatPerShot`) |
| 커서 연동 | 마디 ↔ 회로 칸(슬롯 번호)으로 잇는다 |
| HUD 지팡이 회로 | 실행 중인 블록이 순서대로 켜진다 (G 13) → `scope.triggered`, `spell.cast` 이벤트의 슬롯 번호 |

### 14.5 디버그 글자 출력 (G 16 2단계 "트리를 글자로 출력하는 임시 조립기") [제안], 형식 [임시]

입력 문법:

```text
loadout := "core=" id "; links=" slot ("," slot)* [ "; char=" 1..5 ] [ "; ext=" 0..3 ] [ "; mut=" mut ("," mut)* ]
slot    := id | "_"          # _ = 빈 칸
id      := magic_bolt | great_fireball | ... (4.1의 id)
mut     := mirror | twisted_barrel | glass_wand | unstable_core | hair_trigger | gluttony
```

출력 형식(소수는 둘째 자리까지, 반올림):

```text
SPELL interval=9t heat=26.80 count=6/64 mirror_count=12/64 T=30.00
S0 depth=0 decay=1.00 split=3@1 heat=8.80 inst=3
  [0] P magic_bolt *owner
S1 depth=1 decay=0.85 trigger@2 split=1 heat=18.00 inst=3
  [3] P great_fireball *owner
DIAG none
```

(위는 `core=magic_bolt; links=split_3,on_hit_trigger,great_fireball,_`의 출력)

---

## 15. 결정론 요구

- 같은 씨앗과 같은 입력 기록이면 기기와 서버에서 같은 결과와 같은 해시가 나와야 한다 (G 15) [조율].
- 컴파일러는 순수 함수다: 같은 `WandLoadout`과 같은 `WandCombatProfile`이면 같은 `CompiledSpell`(같은 `Hash`).
- `float`/`double`, `Dictionary`의 열거 순서, `HashSet` 순회, 시스템 시간, `UnityEngine.Random`을 코어에서 쓰지 않는다.
- 변이 난수(불안정 핵, 뒤틀린 총열)는 판 씨앗에서 갈라 낸 난수기를 쓴다 (G 9) [제안]. 뽑는 순서: 발사 1회마다 (1) 불안정 핵 1회 (2) 뒤틀린 총열 루트 탄 수만큼, 실행 순서대로 [임시] (U16).
- 히트스톱은 그림만 멈추고 게임 시간(틱)은 멈추지 않는다 (G 3) [조율].

---

## 16. 테스트 벡터

표기: `L(...)`는 연결 칸 배열, 기본 문맥은 캐릭터 1(달구, 연결 4칸, 감쇠 0.85, 밑줄 없음), 확장 0, 변이 없음, 무한 웨이브. 소수 비교는 |오차| ≤ 2^−20(Fx) 또는 표기한 자리까지. "→ 결정론"은 같은 입력을 두 번 돌려 같은 값/해시인지 함께 본다.

### 16.1 데이터

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-A01 | 블록 표 | 개수를 센다 | 16. 종류별 투사체 6, 제어 3, 변형 7 | D 2.5, G 10.3, U 3.1 |
| TV-A02 | 블록 표 | 등급별로 센다 | 일반 6 {magic_bolt, split_2, wall_bounce, double_size, pierce_2, frost}, 희귀 6 {great_fireball, boomerang, split_3, on_hit_trigger, ice_spike, shadow_orb}, 영웅 4 {chain_lightning, homing, overheat_x3, erosion} | M 6.2 |
| TV-A03 | 블록 표 | 속성마다 투사체 블록을 찾는다 | Fire {great_fireball}, Lightning {chain_lightning}, Arcane {magic_bolt, boomerang}, Frost {ice_spike}, Dark {shadow_orb}. 빈 속성 없음 | G 10.3, W 3.4 |
| TV-A04 | 블록 표 | 각 행을 읽는다 | 4.1 표의 값과 같다(16행 표 기반 테스트: 종류, 등급, 속성, 열, 피해, 간격, 속도, 사거리, 메모의 수치) | G 10.3 |
| TV-A05 | 4종 행 | D 2.5 표와 비교 | ice_spike (투사체, 희귀, 냉기, 8, 14, 0.3초, 12/9, −30% 1초), shadow_orb (투사체, 희귀, 암흑, 14, 28, 0.5초, 6/8), frost (변형, 일반, 냉기, +3, −20% 1초), erosion (변형, 영웅, 암흑, +5, +15% 3초) | D 2.5 |
| TV-A06 | 투사체 간격 | 틱으로 바꾼다 | magic_bolt 9, great_fireball 36, chain_lightning 27, boomerang 21, ice_spike 18, shadow_orb 30 | G 10.3 |
| TV-A07 | 투사체 속도 | 칸/틱 Raw | magic_bolt 1,002,159,036; great_fireball 644,245,094; boomerang 715,827,883; ice_spike 858,993,459; shadow_orb 429,496,730; chain_lightning 즉발 | 2.2 |
| TV-A08 | 상성 표 18행 | 각 행 | 3.3 표와 같다 | M 2.1, M 3.2 |
| TV-A09 | 상성 표 | 속성별 약점 수 | Fire 4, Lightning 4, Arcane 3, Frost 4, Dark 3, 최대 − 최소 = 1 ≤ 1 | D 2.5, M 2.5 |
| TV-A10 | 상성 표 | 속성별 저항 수 | Fire 3, Lightning 4, Arcane 3, Frost 4, Dark 4, 최대 − 최소 = 1 ≤ 1 | D 2.5, M 2.5 |
| TV-A11 | 상성 표 | 일반 12종만 / 보스 6종만 | 일반: 약점 Fire 3, Lightning 2, Arcane 2, Frost 3, Dark 2 / 저항 2, 2, 2, 3, 3 (차이 1). 보스: 약점 1, 2, 1, 1, 1 / 저항 1, 2, 1, 1, 1 (차이 1) | M 2.5 ("일반 적만 봐도 2~3, 보스만 봐도 1~2") |
| TV-A12 | 상성 표 | 종마다 약점 == 저항? | 없음 | M 2.5 |
| TV-A13 | 속성 보스 5(kiln 화염, phantom 비전, storm_owl 번개, frost_ram 냉기, night_serpent 암흑) | 저항 확인 | 각자 자기 속성 | M 2.5 |
| TV-A14 | 첫 등장 ≤ 4인 종 {slime_moss, bug_lantern, wolf_bramble, shaman_cap, hog_needle} | 저항 | 어느 것도 Arcane이 아니다 | M 1.3 |
| TV-A15 | 강화 표 | 단계 13 | 무한 웨이브 6.152788(“약 6.2배”), 결투 1.39 | G 4, O 4.2 |
| TV-A16 | 강화 표 | 0~13 Raw | 11.3 표와 같다 | 2.2 |
| TV-A17 | 감쇠 표 | 깊이 0~3 | 0.85: 1, 0.85, 0.7225, 0.614125 / 0.92: 1, 0.92, 0.8464, 0.778688 | G 4 |

### 16.2 속성 배율

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-B01 | 피해 속성 Fire, 대상 slime_moss(약점 Fire) | 배율 | 1.25 | D 2.5 |
| TV-B02 | 피해 속성 Frost, 대상 slime_moss(저항 Frost) | 배율 | 0.8 | D 2.5 |
| TV-B03 | 피해 속성 Arcane, 대상 slime_moss | 배율 | 1 | D 2.5 |
| TV-B04 | 피해 속성 None | 배율 | 1 | M 1.3 |
| TV-B05 | 결투, 피해 속성 Fire, 대상 플레이어 | 배율 | 1 | O 4.2 |
| TV-B06 | 왕이끼 슬라임(엘리트, 바탕 slime_moss), Fire | 배율 | 1.25 | M 1.3 |
| TV-B07 | 허상이 부르는 슬라임(졸개), Fire | 배율 | 1 | M 1.3 |
| TV-B08 | 달구(화염)가 마력탄으로 slime_moss(약점 화염)를 맞힘 | 피해 속성 | Arcane, 배율 1 (캐릭터 속성이 아니다) | D 2.5, W 3.4 |
| TV-B09 | 마력탄 + 서리(냉기 변형), 대상 shaman_cap(약점 냉기) | 피해 속성 | Arcane, 배율 1 (변형은 속성을 바꾸지 않는다) | G 4, G 10.3 |
| TV-B10 | 달구의 끓어넘침 1.5T가 slime_moss를 맞힘 | 피해 속성 | Fire(캐릭터 속성), 배율 1.25 | G 10.1, W 3.4 |
| TV-B11 | 꽈리 벌레 유폭 20이 shaman_cap에게 | 속성 | None, 배율 1 | M 1.3 |
| TV-B12 | 꿰뚫는 창, 코어 ice_spike, 대상 boar_tusk(약점 냉기) | 속성 | Frost, 배율 1.25 | G 8 |
| TV-B13 | 거대 화염구 폭발이 golem_menhir(저항 번개)와 slime_moss(약점 화염)를 맞힘 | 각 배율 | golem 1, slime 1.25 (적마다 따로) | EL-7 |

### 16.3 컴파일 구조

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-C01 | core=magic_bolt, L(_,_,_,_) | 컴파일 | 유효, 스코프 1, S0 투사체 [magic_bolt], 주인 0, split 1, 간격 9틱, 열 4, 투사체 1, 진단 없음 | G 4 |
| TV-C02 | core=None | 컴파일 | 무효, `E_CORE_EMPTY`, 출발 불가 | O 1.1 |
| TV-C03 | core=split_2 | 컴파일 | 무효, `E_CORE_NOT_PROJECTILE` | G 4 |
| TV-C04 | 캐릭터 3(갈피), L 길이 4 | 컴파일 | `E_SLOT_COUNT_MISMATCH` (갈피는 5) | G 4, SL-1 |
| TV-C05 | core=great_fireball, L(magic_bolt,_,_,_) | 간격 | 36틱(코어의 간격), S0 투사체 2개 동시 발사 | G 4 |
| TV-C06 | core=magic_bolt, L(double_size, ice_spike,_,_) | 변형 적용 | magic_bolt와 ice_spike 모두 보너스 0.2(변형이 앞 칸이어도 같은 스코프 전부) | G 4 (CP-1) |
| TV-C07 | L(double_size, pierce_2,_,_) | 보너스 | 0.3(누적), 피해 10 × 1.3 = 13 | G 4 |
| TV-C08 | L(split_3, split_2,_,_) | 컴파일 | split 3, 진단 `W_SPLIT_DUPLICATE`(slot 2), 투사체 3 | G 4 |
| TV-C09 | L(ice_spike, split_3,_,_) | 투사체 | 6 (스코프의 투사체 2개를 각각 3갈래) | G 4 |
| TV-C10 | L(on_hit_trigger, great_fireball,_,_) | 구조 | S0 [magic_bolt], S1(깊이 1, trigger@1, 주인 slot 2 great_fireball), 감쇠 S1 0.85 | G 4 |
| TV-C11 | L(on_hit_trigger,_,_,_) | 컴파일 | 유효, `W_TRIGGER_NO_PROJECTILE`(slot 1), 적중해도 하위 탄 0 | G 4 |
| TV-C12 | L(on_hit_trigger, frost,_,_) | 컴파일 | `W_TRIGGER_NO_PROJECTILE`, `W_NO_EFFECT` 없음, 열 4 + 3 = 7 | G 4, 6.5 |
| TV-C13 | L(on_hit_trigger, ice_spike, great_fireball,_) | 주인 | S1 주인 ice_spike(slot 2), great_fireball 같은 스코프 동시 | G 4 |
| TV-C14 | L(double_size, on_hit_trigger, great_fireball,_) | 보너스 | S0 magic_bolt 0.2, S1 great_fireball 0 (스코프를 건너지 않는다) | CP-3 [임시] |
| TV-C15 | 갈피+확장 3(L=8): L(trig, magic_bolt, trig, magic_bolt, trig, magic_bolt, trig, magic_bolt) | 컴파일 | 스코프 S0~S3, slot 7의 트리거 `W_TRIGGER_DEPTH`, slot 8 magic_bolt는 S3에 추가(S3 투사체 2) | SF-1, SF-7 [임시] |
| TV-C16 | L(shadow_orb, pierce_2,_,_) — core=shadow_orb | 진단 | `W_NO_EFFECT`(slot 2) | G 4, G 10.3 |
| TV-C17 | core=magic_bolt, L(shadow_orb, pierce_2,_,_) | 진단 | 없음(마력탄에 효과) | 6.5 |
| TV-C18 | core=chain_lightning, L(homing,_,_,_) | 진단 | `W_NO_EFFECT` | U22 [임시] |
| TV-C19 | 캐릭터 1, 확장 2 | L 길이 | 6, 맨 끝 칸 = slot 6 | SL-2, SL-3 |
| TV-C20 | 같은 입력을 두 번 | 컴파일 | `Hash` 같음, 출력 글자 같음 → 결정론 | G 15 |
| TV-C21 | 진단 여러 개 | 정렬 | 슬롯 오름차순, 같으면 코드 순 | 6.4 |

### 16.4 열

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-D01 | core=magic_bolt | HeatPerShot | 4 | G 10.3 |
| TV-D02 | L(split_3,_,_,_) | HeatPerShot | 4 × 2.2 = 8.8 | G 5 |
| TV-D03 | L(split_2, double_size,_,_) | HeatPerShot | (4 + 3) × 1.6 = 11.2 | G 5 |
| TV-D04 | L(on_hit_trigger, great_fireball,_,_) | HeatPerShot | 4 + 18 = 22 | G 5 |
| TV-D05 | L(on_hit_trigger, great_fireball, split_3,_) | HeatPerShot | 4 + 18 × 2.2 = 43.6 | G 5 |
| TV-D06 | L(split_3, on_hit_trigger, great_fireball,_) | HeatPerShot | 4 × 2.2 + 18 = 26.8 (부모의 분열 배수를 하위 스코프에 곱하지 않는다) | HT-3 |
| TV-D07 | 갈피, 맨 끝 칸(slot 5)에 각 블록 | 실제 열 | +3 → +1, +5 → +2, 8 → 4, 14 → 9, 18 → 13 | W 4.3 |
| TV-D08 | 갈피, core=magic_bolt, L(_,_,_,_,great_fireball) | HeatPerShot | 4 + 13 = 17 (22에서 17) | W 4.3 |
| TV-D09 | TV-D08, 열 0에서 쏨(과열 명세와 함께) | 잠금까지 발사 수 | 6 (17 × 6 = 102 > 100). 같은 조립의 달구(밑줄 없음): 22 → 5발 (22 × 5 = 110) | W 4.3 |
| TV-D10 | 갈피, L(_,_,_,split_2, frost) | HeatPerShot | (4 + 1) × 1.6 = 8.0 (분열 배수는 깎은 뒤에) | W 4.3 |
| TV-D11 | 갈피, L(_,_,_,great_fireball,_) (맨 끝 칸 빔) | HeatPerShot | 22 (깎지 않음) | HT-6 [임시] |
| TV-D12 | 갈피, 확장 1(L=6), L(_,_,_,_,great_fireball,_) | HeatPerShot | 22 (맨 끝 칸이 slot 6으로 밀림) | SL-3 |
| TV-D13 | 갈피, 맨 끝 칸 on_hit_trigger / split_3 | 깎임 | 없음 | HT-6 |
| TV-D14 | 거울상, core=magic_bolt | HeatToAdd (J) | 6 | G 9 |
| TV-D15 | 거울상, 사본 마법진 | HeatToAdd | 0 (열 면제, 거울상 미적용) | W 4.3, G 9 |
| TV-D16 | 마력 방출(비기) | HeatToAdd | 0 | G 8 |
| TV-D17 | 기준 캐릭터 값(임계 85, 냉각 45, 잠금 3초), 마력탄, 강화 +0 — 과열 명세와 공유 | J를 누르고만 있음 | 26발(일반 21 + 임계 5) + 잠금 3초 = 6.9초 주기, 310 피해, 44.9 DPS | G 18 |
| TV-D18 | 같은 조건 | 임계 구간 유지(임계 n발 → 0.2초 대기 → 4n 냉각) | n = 4, 69.2 DPS. 비율 1.54 ≥ 1.4 | G 5, G 18 |
| TV-D19 | 달구(임계 80) / 초롱(냉각 50) / 성에(임계 90) | 임계 유지 | n = 6, 73.5 / n = 4, 71.4 / n = 3, 65.5 DPS | G 18 |

### 16.5 투사체 수와 상한

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-E01 | L(split_3, on_hit_trigger, great_fireball, split_3) | 수 | S0 3, S1 3 × 3 = 9, 합 12, 가지치기 없음 | G 4 |
| TV-E02 | 갈피+확장 3(L=8), L(split_3, trig, magic_bolt, magic_bolt, split_3, trig, magic_bolt, split_3) | 수 | S0 3, S1 3 × 6 = 18, S2 18 × 3 = 54, 합 75 > 64 → S2 생략, 실행 21, `W_PROJECTILE_CAP`, 미리보기 S2 점선 | G 4 |
| TV-E03 | 달구+확장 3(L=7), L(magic_bolt, split_3, trig, magic_bolt, magic_bolt, magic_bolt, split_3) | 수 | S0 6, S1 6 × 9 = 54, 합 60 ≤ 64 (생략 없음) | G 4 |
| TV-E04 | TV-E03 + 거울상, J 발사 | 수 | 120 > 64 → S1 생략, 실행 12 | G 9 |
| TV-E05 | TV-E03 + 거울상, 양익 사격(비기) | 수 | 거울상 미적용, 60 | G 9 |
| TV-E06 | 6.6의 최대 루트(갈피+3, 투사체 9개, split_3, 거울상) | 수 | 54 ≤ 64 | 6.6 |
| TV-E07 | 무한 웨이브, 플레이어 탄 290개 살아 있음 | 21발 묶음 생성 | 10발 생성, 각 `CompensationMul` = 2.1, 이벤트 `spell.cap_compensated(21,10)` | G 4 [임시] |
| TV-E08 | 플레이어 탄 300개 | 적이 탄 생성 | 적 상한 150은 따로 → 생성됨 | G 4 |
| TV-E09 | 결투, 내 탄 140 + 사본 마법진 탄 10 | 내 J 발사 | 생성 0(한 사람당 150에 사본 포함) | O 4.2 |
| TV-E10 | 사본 마법진, TV-E02 조립 | 따라 쏘기 1회 | 사본에도 64 상한(21), 화면 300에는 내 탄과 합산 | W 4.3 |
| TV-E11 | 비기 예산 Budget = 5, 트리 실행 1회가 6발 | Execute | 5발, `Omitted` 1 | G 8 [임시] |

### 16.6 실행과 기하

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-F01 | split_2, 시야 0 | 루트 방향 | −1000, +1000 | G 4 |
| TV-F02 | split_3, 시야 9000 | 루트 방향 | 7500, 9000, 10500 | G 4 |
| TV-F03 | 거울상, J, core=magic_bolt, 시야 0 | 루트 | 0과 18000의 2발, 같은 CastId | G 9 |
| TV-F04 | 결투, 상대가 시야 기준 +7도 | J 발사 | 가운데 탄 방향 +700 | O 4.4 |
| TV-F05 | 결투, 상대가 +15도 | J 발사 | +1000(상한 10도) | O 4.4 |
| TV-F06 | 결투, split_3, 상대 +7도 | J 발사 | 700, −1500, +1500 (곁가지는 보정 없음) | O 4.4 |
| TV-F07 | 결투, 뒤틀린 총열, 상대 +15도, 흔들림 뽑기 +2000 | J 발사 | 1000 + 2000 = 3000 (흔들림은 보정 뒤) | O 4.4 |
| TV-F08 | 뒤틀린 총열, 같은 씨앗 두 번 | 1000발 | 흔들림 순서 같음, 모두 [−2500, +2500] → 결정론 | G 9 |
| TV-F09 | 불안정 핵, 무한 웨이브, 같은 씨앗 | 10,000발 | 자폭 횟수 같음(두 번 돌림), 비율 ≈ 5% | G 9 |
| TV-F10 | 불안정 핵, 결투 | J | 자폭 판정 없음, 폭발 반경 2 × 1.5 = 3 | O 4.2 |
| TV-F11 | 불안정 핵, 무한 웨이브, 거대 화염구 | 폭발 반경 | 4 (+100%) | G 9 |
| TV-F12 | 사본 마법진(위치 P, 내 시야 D) | 내가 J | `Execute(tree, P, D, 0.5, true, Player)`, 콤보 없음 | W 4.3 |
| TV-F13 | 사본 마법진, 마력탄(9틱 간격) | 내가 J를 계속 | 마법진은 18틱에 1회 → 내 발사 2번에 1번(0, 18, 36 … 198틱), 12회째(198틱)를 쏘고 사라짐. 원문 "3.6초 만에 다 쓴다"는 12 × 0.3초로 센 값 | W 4.3 |
| TV-F14 | 사본 마법진, 간격 30틱 코어 | 6초 | 12회(360틱 안), 그 뒤 소멸 | W 4.3 |
| TV-F15 | 마법진 남은 5회일 때 새로 깖 | 상태 | 이전 것 사라짐, 새 것 12회 | W 4.3 |
| TV-F16 | 허상, 플레이어 조립 core=magic_bolt | 복사 | 간격 ceil(9 × 2.5) = 23틱, 속도 6, 피해 1칸, 진영 적 | M 3.4 [임시: 반올림] |
| TV-F17 | 허상, L(homing, frost, erosion, wall_bounce, wall_bounce) 상당 | 복사 | 유도·서리·잠식 제거, 튕김 1회 | M 3.4 |
| TV-F18 | 허상, split_3 | 복사 각도 | ±1919 (3칸 지점 1.0칸) | M 3.4 |
| TV-F19 | 허상, split_2 | 복사 각도 | ±1000 그대로 | M 3.4 |
| TV-F20 | 허상, core=ice_spike, L(split_3, trig, magic_bolt, split_3) | 복사 수 | 3 + 9 = 12 ≤ 12 | M 3.4 |
| TV-F21 | 허상, core=magic_bolt, L(ice_spike, split_3, trig, magic_bolt, split_3)(L≥5) | 복사 수 | 6 + 18 = 24 > 12 → S1 생략, 6 | M 3.4 [임시] |
| TV-F22 | 허상, core=great_fireball(반경 2) | 복사 폭발 | 반경 min(1.2, 1.5) = 1.2, 48틱 예고 뒤 | M 3.4 |
| TV-F23 | 허상, core=chain_lightning | 복사 | 36틱 조준선 뒤 직선 번개 | M 3.4 |
| TV-F24 | 허상, ice_spike가 플레이어를 맞힘 | 상태 | 감속 없음 | M 3.4 |
| TV-F25 | 허상, shadow_orb | 복사 반지름 | 0.25 | M 3.4 |

### 16.7 투사체 동작

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-G01 | magic_bolt, 적 없음 | 날아감 | 이동 거리 10 이상이 되는 틱(43틱째)에 소멸, `projectile.expired(range)` | G 10.3 |
| TV-G02 | magic_bolt + pierce_2 + 트리거→great_fireball, 일렬 적 3 | 날아감 | 적 3체 각 1회 피해, 하위 스코프는 첫 적에서 1회만 | G 4 |
| TV-G03 | shadow_orb, 일렬 적 5 | 날아감 | 5체 각 1회, 트리거 첫 적에서 1회, 벽에서 소멸, 사거리 끝이면 터지지 않고 사라짐 | G 10.3, E 13.3 |
| TV-G04 | shadow_orb가 튕김으로 같은 적 위를 다시 지남 | 날아감 | 두 번째 피해 없음 | G 10.3 |
| TV-G05 | boomerang, 앞 3칸에 적 1 | 왕복 | 같은 적에게 2회(가는 길 1, 오는 길 1), 트리거 1회 | G 10.3, G 4 |
| TV-G06 | chain_lightning, 첫 대상 + 반경 3 안에 적 3 | 발사 | 같은 틱에 4타: 18, 12.6, 8.82, 6.174 | G 10.3 |
| TV-G07 | chain_lightning, 첫 대상 주변 3칸 안에 적 없음 | 발사 | 1타 18, 전이 없음 | U10 [임시] |
| TV-G08 | chain_lightning, 대상이 덤불 늑대 | 발사 | 늑대의 탄 회피가 일어나지 않고 맞는다 | G 10.3 |
| TV-G09 | great_fireball, 적 A에 맞음, 반경 2 안에 B·C, 2.1칸에 D | 폭발 | A, B, C 각 40 1회, D 없음. 트리거는 폭발 중심에서 1회 | G 4, G 10.3 |
| TV-G10 | great_fireball이 빈 벽에 닿음 | 폭발 | 그 자리 폭발, 맞은 적이 없으면 트리거 없음 | U11, U27 [임시] |
| TV-G11 | 트리거 하위 탄 | 생성 | 다음 틱부터 이동, 트리거를 일으킨 적은 맞히지 않음 | U13, U30 [임시] |
| TV-G12 | wall_bounce 1개, 벽 수직 입사 | 튕김 | 방향 반전, 남은 튕김 1, 이동 거리 누적 계속 | U6 [임시] |
| TV-G13 | 성에의 얼음 병풍 앞, 내 wall_bounce 탄 | 지남 | 튕기지 않고 지나감 | W 4.4 |
| TV-G14 | 결투장 선돌 기둥 | wall_bounce 탄 | 튕김 | O 4.1 |
| TV-G15 | 땅속 화분 족제비 위를 탄이 지남 | 충돌 | 피해·효과 없음, 탄 소모 없음 | M 2.6 |
| TV-G16 | 같은 입력 기록 2회 | 전투 100초 | 탄 목록·적 체력 해시 같음 → 결정론 | G 15 |

### 16.8 피해

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-H01 | magic_bolt +0, 일반 적(상성 무관 속성) | 적중 | 10 | G 10.3 |
| TV-H02 | magic_bolt +13 | 적중 | 61.528 (10 × 6.152788) | G 4 |
| TV-H03 | magic_bolt +0, 임계 | 적중 | 20 | G 4 |
| TV-H04 | 임계 + overheat_x3 같은 스코프 | 적중 | 30 | G 4 |
| TV-H05 | 임계 + 유리 지팡이 | 적중 | 30 | G 4 |
| TV-H06 | 임계 + overheat_x3 + 유리 지팡이 | 적중 | 40 | G 4 |
| TV-H07 | 결투, 유리 지팡이, 맞은 뒤 2초, 임계 | 적중 | 임계 배율 2(+1 꺼짐), 맞은 뒤 3초가 지나면 3 | O 4.2 |
| TV-H08 | 열이 임계 구간인데 마력 방출(2T) | 적중 | 20 (임계 없음, 비기 배율 2) | G 4, G 8 |
| TV-H09 | magic_bolt vs 약점 / 저항 | 적중 | 12.5 / 8 | D 2.5 |
| TV-H10 | magic_bolt + erosion, 같은 적에게 3발 | 1·2·3발째 | 10, 11.5, 11.5 (첫 발에는 미적용, 겹치지 않음) | G 10.3 |
| TV-H11 | 잠식 건 뒤 180틱 경과, 다음 탄 | 적중 | 10 (만료) | G 10.3 |
| TV-H12 | 잠식 걸린 적에게 비기·패시브 피해 | 적중 | ×1.15 | U29 [임시] |
| TV-H13 | 결투, 잠식 걸린 상대에게 10 | 적중 | 10.8 | O 4.2 |
| TV-H14 | 트리거 하위 great_fireball(깊이 1) / 깊이 2 | 폭발 | 34 / 28.9 | G 4 |
| TV-H15 | 갈피, 깊이 1 / 2 | 폭발 | 36.8 / 33.856 | G 4 |
| TV-H16 | double_size + pierce_2 | 적중 | 13 | G 10.3 |
| TV-H17 | 그믐, 결정 7개, magic_bolt | 적중 | 12.1 | W 4.5 |
| TV-H18 | 그믐, 결정 0개 빚 2개 | 적중 | 10 (빚은 0개) | W 4.5 |
| TV-H19 | 그믐, 결정 5개, 결정 2개 비기 | 비기 피해의 캐릭터 배율 | 1.09 | W 4.5 |
| TV-H20 | 그믐, 만개 | 캐릭터 배율 | 1.00 | W 4.5 |
| TV-H21 | 그믐, 결정 4 / 7, 임계 유지(과열 명세와 공유) | 임계 유지 DPS | 77.5 / 83.8 (결정 0은 69.2) | W 4.5 |
| TV-H22 | 뒤틀린 총열 | magic_bolt 적중 | 16 | G 9 |
| TV-H23 | +5, 임계, 약점 | magic_bolt 적중 | 10 × 2.011357 × 2 × 1.25 = 50.284 | G 4 |
| TV-H24 | 결투 +13 | magic_bolt 적중 | 13.9 | O 4.2 |
| TV-H25 | 같은 피해 계산, 곱 순서를 바꾼 구현 | 비교 | 고정 순서 구현만 통과(Raw 일치) — 순서 고정 회귀 테스트 | DM-1 |
| TV-H26 | 표준 T, core=magic_bolt +0 | StandardT | 10. 임계 4발 = 80 = 8T | W 3.5, U2 [임시] |
| TV-H27 | 화면 상한 보전 2.1 | magic_bolt 적중 | 21 | G 4 [임시] |
| TV-H28 | 적이 쏜 허상 복사탄 | 플레이어 피격 | 1칸 | M 3.4 |

### 16.9 상태

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-I01 | 일반 적에게 ice_spike(−30%, 60틱) 뒤 30틱에 frost(−20%, 60틱) | 시간 | 0~59틱 −30%, 60~89틱 −20%, 90틱부터 0 | G 10.3 |
| TV-I02 | ice_spike 두 번(30틱 간격) | 시간 | −30% 하나, 남은 시간만 60으로 갱신(합 90틱) | G 10.3 |
| TV-I03 | 보스에게 ice_spike / frost | 세기 | −15% / −10% | G 10.3 |
| TV-I04 | 엘리트 / 중량에게 ice_spike | 세기 | −30% / −30% | G 10.3, W 3.6 |
| TV-I05 | 불가마 지대(−20%) 안에서 ice_spike | 세기 | −30%(큰 값 하나) | G 10.3, W 3.6 |
| TV-I06 | 보스, 불가마 + frost | 세기 | −10% | W 3.6 |
| TV-I07 | 중량 적, 살얼음 6타(감속 −30% 1초) + frost | 세기 | −30% | W 3.6 |
| TV-I08 | 빙결 내성 6초 중인 적에게 frost | 결과 | 감속 걸림 | M 2.6 |
| TV-I09 | 땅속 적에게 ice_spike | 결과 | 감속 없음 | W 3.6 |
| TV-I10 | 결투, 상대에게 ice_spike | 시간 | 적중 3틱 뒤에 −15% 시작, 60틱 | O 4.2 |
| TV-I11 | 결투, ice_spike + frost | 세기 | −15%(겹치지 않음) | O 4.2 |
| TV-I12 | 폭발 + frost, 범위 안 적 3 | 결과 | 3체 모두 −20% | U28 [임시] |

### 16.10 진단·미리보기·출력

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-J01 | `core=magic_bolt; links=split_3,on_hit_trigger,great_fireball,_` | 글자 출력 | 14.5의 예시와 한 글자도 다르지 않다 | G 16 |
| TV-J02 | 같은 입력 | 미리보기 맨 아래 줄 | "발사 1회 = 투사체 6개 · 열 26.8" | U 4.11 |
| TV-J03 | 깊이 2 스코프, 기본 감쇠 / 갈피 | 꼬리표 | "×0.72" / "×0.84" | U 4.11 |
| TV-J04 | 진단 5개 | 경고 줄 | 정렬 앞 3개만 | U 4.11 |
| TV-J05 | 경고 마디 | 미리보기 | 해당 슬롯 마디에 경고 딱지 | U 4.11 |
| TV-J06 | 글자 출력 → 다시 파싱 → 컴파일 | 비교 | 같은 `Hash` | 14.5 |

---

## 17. 충돌

| 번호 | 내용 | 택한 쪽과 이유 |
|---|---|---|
| C1 | G 5 "갈피의 밑줄(맨 끝 칸 블록의 열 절반, 최대 −5)"은 "열이 절반이 된다"로도 읽히고, W 4.3·G 10.1은 "절반(올림)만큼 깎인다, 최대 5"다. 열 18이면 앞의 읽기로는 9, 뒤의 읽기로는 13 | W 4.3(캐릭터 수치의 주인, G 0)과 G 10.1. 깎는 양 = ceil(h/2), 최대 5. D와 무관 |
| C2 | D 2.5의 새 블록 4종 표와 G 10.3의 같은 4줄 | 값이 같다(확인 완료). D가 우선이라는 표시만 [조율]로 둔다 |
| C3 | G 4 피해 공식에는 속성 배율이 있고, O 4.2 결투에는 "속성 약점·저항은 없다" | 모드 규칙으로 둘 다 산다. 무한 웨이브는 D 2.5(×1.25/×0.8), 결투는 O 4.2(1). D 2.5는 적의 약점·저항을 정한 것이고 플레이어 대상은 말하지 않는다 |
| C4 | G 10.3의 감속 풀은 얼음 송곳·서리·불가마 셋이고, W 3.6은 "중량·보스에게 감속으로 바뀐 살얼음"까지 넷 | W 3.6(꼬리표 반응의 유일한 기준, G 0·M 2.6). 넷을 한 풀에 넣는다. G의 "더하지 않고 가장 큰 값"은 그대로 |
| C5 | M 1.3 "비기와 패시브의 속성은 W가 정하고, 정하지 않은 것은 무속성"과 G 4 "블록을 거치지 않는 피해(패시브와 고유 비기)는 캐릭터의 속성" | 둘이 겹치는 모든 경우(W가 정한 여섯 가지)는 같다. 원문이 정하지 않은 플레이어 쪽 비블록 피해는 G 4의 일반 규칙(캐릭터 속성)을 택했다(G가 피해 공식의 주인, G 0). 적의 비블록 피해는 M 1.3대로 무속성. D 2.5는 "맞힌 투사체 블록"만 정하므로 어느 쪽과도 충돌하지 않는다 (EL-8) |
| C6 | G 4 "임계 배율 = 과열 3배 블록이 있으면 3"은 트리 전체로 읽히고, G 4 표 "변형: 현재 스코프의 모든 투사체에 붙는다"는 스코프 단위다 | 변형 블록의 일반 규칙(스코프)을 택했다 [임시] (U4). D와 무관. 개발자 확인 대상 |
| C7 | E 13.3 "탄 그림의 크기는 판정 크기와 같게 그린다(판정 크기는 블록 사양이 정한다)"인데 G 10.3 블록 사양에는 판정 크기가 없다 | 판정 반지름을 이 명세가 [임시]로 정했다(U1). 그림은 그 값을 따른다 |
| C8 | M 3.4는 그림자 구체를 "반지름 0.25"로 복사한다고 적고, E 13.3의 그림자 구체 탄 그림은 40×40px(지름 0.625칸 = 반지름 0.3125)이다. E는 "그림 크기 = 판정 크기" | 게임 수치는 칸 단위 그대로(D 2.1) → 판정 반지름 0.25칸. 40×40 그림 가운데 바깥 4px 테를 뺀 몸이 32px(= 반지름 0.25칸)이라 "몸 = 판정"으로 맞춘다. E 13.3도 크기를 "시작값"이라 했다 |
| C9 | G 20(2판 기록): 블록 12종, 속성 3개, 연결 4칸 공통, 감쇠 0.85 공통 | 효력 없음(G 머리말). 16종·5속성·갈피 5칸·0.92 |
| C10 | G 4의 `Execute` 여섯 인자에는 임계, 발사 식별자, 출처가 없는데 G 4·7·8은 그 값을 쓰는 규칙(비기는 임계 없음, 발사 1회 콤보 +3 상한, 비기 적중은 콤보 제외)을 둔다 | 여섯 인자를 그대로 두고 옵션 구조체로 붙였다(9.1). 원문의 형태 요구("처음부터 이 형태로")와 충돌하지 않게 인자 순서를 지켰다 |
| C11 | G 4 "화면 전체 플레이어 투사체 상한 300"과 O 4.2 "결투 한 사람당 150(사본 포함)", W 4.3 "사본은 300에 함께 센다" | 모드별로 둘 다 산다(결투 수치의 주인은 O 4.2, G 0) |
| C12 | G 9 거울상 "64개 상한에 포함"과 6.6절 계산(루트 최대 54) | 충돌 아님. 거울상이 하위 스코프까지 두 배로 만들어 64를 넘길 수 있어 가지치기 대상이 된다(TV-E04) |
| C13 | E 0.1의 "위험색은 적의 공격과 적 탄에만"과 M 3.4 "허상이 쏘는 탄은 전부 위험색" | 충돌 아님. 진영이 적이면 표시 계층이 위험색을 쓴다. 코어는 `Faction`과 `Element`를 둘 다 내보낸다 |
| C14 | U 4.11 미리보기 꼬리표 "×0.72"와 0.85² = 0.7225 | 표시는 소수 둘째 자리 내림 문자열, 계산은 0.7225 표값 |

---

## 18. 미정

원문이 정하지 않았거나 "개발자 확인 필요"에 걸린 것. 구현을 막지 않도록 [임시] 기본값으로 만든다. G "개발자 확인 필요" 목록에는 이 영역의 항목이 따로 없다(40번 발표 기준만 15절 발표 구도와 닿는다: 실행 트리와 입력 트라이를 양 축으로 보여 준다 — 14.5의 글자 출력과 14.4 미리보기가 그 자료다).

| 번호 | 정하지 않은 것 | [임시] 기본값 | 바꿀 때 영향 |
|---|---|---|---|
| U1 | 투사체 판정 반지름 | magic_bolt 0.12, great_fireball 0.18, boomerang 0.15, ice_spike 0.12(원 판정), chain_lightning 광선 반폭 0.12, shadow_orb 0.25([제안], M 3.4). 적 작은 탄 0.12(16×16)·큰 탄 0.18(24×24) 관례에 맞춤 (M 9) | 그림 크기(E 13.3) |
| U2 | T의 계산식 | 14.2절의 `StandardT` | 비기·패시브 피해 전부 |
| U3 | 부모 스코프의 변형이 하위 스코프로 이어지는가 | 이어지지 않는다 | 트리거 조립의 가치 |
| U4 | 과열 3배의 적용 범위(스코프 / 트리 전체) | 스코프(변형 일반 규칙) | C6 |
| U5 | 넷째 트리거 | 무시 + `W_TRIGGER_DEPTH`, 뒤 블록은 깊이 3 스코프 | |
| U6 | 벽 튕김 횟수와 겹침 | 블록 1개당 2회, 블록 수만큼 더함. 반사는 입사각 반사(축 정렬 벽·기둥) | 허상 복사 1회 제한(M 3.4)이 플레이어보다 적다는 원문과 맞음 |
| U7 | 관통 +2 겹침 | 블록 1개당 +2체 더함 | |
| U8 | 탄환 크기 2배의 크기 효과 | 블록 1개당 판정 반지름 ×2, 겹치면 곱(최대 ×4로 자름). 폭발 반경은 바꾸지 않는다. 피해 +20%는 합산 | |
| U9 | 유도의 "선회각 제한" 수치 | 초당 180도(틱당 300 센티도), 대상은 시야 앞 ±90도·6칸 안 가장 가까운 적(같으면 id 작은 쪽), 대상이 죽으면 다시 고름. 결투는 선회각 절반(틱당 150)·90틱 뒤 끊김(O 4.2) | |
| U10 | 연쇄 번개의 즉발 판정 | 발사 방향 사거리 7 광선(반폭 0.12 + 적 반지름)의 가장 가까운 적. 전이: 현재 대상에서 반경 3 안의 아직 안 맞은 가장 가까운 적(같으면 id). "3회 전이" = 첫 타 + 전이 3 = 최대 4타. 결투·허상의 직선 번개는 첫 대상 1회 | |
| U11 | 거대 화염구 폭발의 판정 | 적에 맞은 자리, 벽, 사거리 끝에서 터진다. 반경 2 안(적 중심까지의 거리 ≤ 2 + 적 반지름)의 모든 적이 각 1회 탄의 피해. 직격 피해를 따로 주지 않는다. 불안정 핵 자폭 시 그 발사의 탄은 나가지 않는다 | |
| U12 | 부메랑의 돌아오는 법 | 편도 6칸 또는 벽에서 돌아섬, 쏜 사람의 지금 위치로 같은 속도, 반경 0.5칸 안에 들어오거나 돌아오는 길 9칸을 넘으면 소멸. 다리마다 적 1회 | |
| U13 | 하위 스코프의 자리·방향·주인의 뜻 | 첫 적중 지점, 부모 방향, 트리거를 일으킨 적 제외. 주인은 경고·미리보기용 | |
| U14 | 자손 탄의 임계·단계·캐릭터 배율 | 발사 순간 스냅숏을 물려받는다. 사본 마법진은 임계 없음 | |
| U15 | 상한 넘침의 보전 | 64 상한 생략은 보전 없음. 300/150 화면 상한은 N/M 균등 보전(앞쪽부터 생성). 비기 160 예산은 뒤쪽부터 안 만듦, 보전 없음 | 화력 |
| U16 | 뒤틀린 총열 흔들림의 단위 | 루트 탄마다 [−2500, +2500] 센티도 균등 정수, 실행 순서대로 뽑음 | |
| U17 | 각도 표현과 삼각함수 | 센티도 정수, 9,001칸 sin 표 | |
| U18 | Fx 반올림 | 상수는 십진값 최근접(짝수), 곱은 내림(산술 시프트), 거듭제곱은 표 | |
| U19 | 피해·체력의 수 형식 | Fx, 표시만 반올림 정수 | |
| U20 | "임계 사격 가능 횟수" 계산 | 과열 명세가 정한다. 컴파일러는 `HeatPerShot`만 준다 | |
| U21 | 갈피 밑줄의 맨 끝 칸이 빈 경우 | 깎지 않는다(앞 칸으로 옮겨 가지 않는다) | |
| U22 | "효과 없는 조합"의 전체 목록 | 6.5 표(그림자 구체+관통은 원문, 연쇄 번개+관통/튕김/유도는 임시) | |
| U23 | 트리거가 부모의 어느 탄에 걸리는가 | 부모 스코프의 모든 탄 각각([제안], G 4 "투사체당") | 투사체 수 |
| U24 | 2갈래의 결투 조준 보정 | 두 탄 모두 보정 없음 | |
| U25 | 허상 발사 간격 2.5배의 틱 반올림 | 올림 | |
| U26 | 허상 발사 1회 12개의 줄이는 법 | 깊은 스코프부터 생략, 그래도 넘으면 실행 순서 앞 12개 | |
| U27 | 빈 땅 폭발의 트리거 | 적을 하나도 맞히지 않으면 발동하지 않음 | |
| U28 | 서리·잠식·얼음 송곳 효과가 폭발·전이 대상에도 걸리는가 | 건다 | |
| U29 | 잠식이 비블록 피해에도 적용되는가 | 적용("받는 피해") | |
| U30 | 탄이 생기는 자리와 첫 이동 | 쏜 사람 중심, 생긴 틱에는 움직이지 않음 | 근접 적중감 |
| U31 | 그림자 구체 + 벽 튕김 | 튕긴다(블록을 넣었으면 효과가 있다), 경고 없음 | |
| U32 | 블록 이름 표 키 | `block.<id>`(W 0의 `block.*`) | |
| U33 | 보스 id | 3.3 표의 `boss_*` | 적 명세와 맞출 것 |
| U34 | 제물에서 "무" 블록 | 어떤 지팡이 속성과도 같지 않음 | |
| U35 | 판정 형식(점 대 원, 쓸기) | 선분 쓸기(이전 위치 → 새 위치), 원 대 원 | |
| U36 | 실행 순서 세부(거울상 묶음 순서) | 앞 묶음 다음 뒤 묶음 | 결정론 해시 |
| U37 | 거울상 뒤쪽 탄의 조준 보정 | 없음 | |

---

## 19. 시각·소리 연결점

코어는 아래 이벤트만 발행한다. 그림·이펙트·소리는 표시 계층이 붙인다(지금은 구현하지 않는다). 이벤트는 틱 번호와 함께 순서가 정해진 목록으로 낸다(다시 보기와 같은 순서).

| 이벤트 | 실을 값 | 붙을 것(원문의 이름) |
|---|---|---|
| `spell.cast` | castId, source, block(코어), pos, dir, isCrit, mirror | C-01 발사(블록별), 총구 섬광, 얼음 송곳 육각 서리꽃, 그림자 구체 발사 부풂(0.1초 머묾), 임계 총구 고리(E 10), HUD 회로 점등 |
| `spell.cast.crit` | castId | C-05 임계 사격 금속음, 탄의 이중 테·쐐기 |
| `projectile.spawned` | id, block, element, scopeDepth, faction, radius, isCrit, mods(frost/erosion/size) | 탄 그림(속성 모양), 꼬리 띠, 서리 변형 테·가루, 잠식 얼룩 꼬리, 허상 탄의 흰 점 개수(깊이) |
| `projectile.hit` | id, target, damage, element, affinity(weak/resist/none), isCrit, hopIndex | H-01 일반 적중, C-02 속성 겹소리, 적중 불꽃, 피해 숫자, C-23 서리/잠식 겹소리 |
| `projectile.passed` | id, target | 그림자 구체 파문, C-24 "붑" |
| `projectile.pierced` | id, target | 관통 표시 |
| `projectile.bounced` | id, pos, normal | 벽 튕김 표시 |
| `projectile.boomerang_turned` | id, pos | 부메랑 돌아섬 |
| `projectile.expired` | id, reason(range/wall/returned/pierce_spent) | 그림자 구체 오므라듦, 탄 사라짐 |
| `explosion.detonated` | castId, pos, radius, element, isCrit | H-03 폭발음, 폭발 고리(256×256, 반경 2 기준) |
| `chain.hop` | castId, from, to, hopIndex | H-04 연쇄 전이, 연쇄 줄기 |
| `chain.telegraph` | castId, pos, dir, ticks(15 / 36) | 결투·허상의 직선 번개 조준선 |
| `scope.triggered` | castId, depth, pos, triggerSlot | HUD 회로의 트리거 칸 점등 |
| `status.slow.applied` / `.refreshed` / `.expired` | target, source, strength | C-25 감속 걸림, 발목 서리 고리(한 줄/두 줄) |
| `status.erosion.applied` / `.refreshed` / `.expired` | target | C-25 잠식 걸림, 윤곽 점선 테 + 보라 금, 피해 숫자의 작은 보라 ▲ |
| `hit.affinity_revealed` | species, kind(weak/resist), element | 머리 위 속성 문양 1.5초, 도감·다음 웨이브 칸 기록 (E 13.4) |
| `hit.weakness` / `hit.resistance` | target, element | 약점 큰 불꽃·속성색 물듦·4px 밀림 / 저항 튕김 호·어두워짐 (E 13.4) |
| `spell.cap_compensated` / `spell.cap_dropped` | castId, requested, created | (표시 없음, 디버그 계측) |
| `mutation.unstable_core.self_detonate` | pos | 연두 테두리 폭발 + 잠금 연출 (E 13.2) |
| `copy_circle.fired` / `copy_circle.expired` | pos, remaining | T-13 베껴 쓰기(책 펴는 소리, 끝날 때 책장 흩어짐) |
| `compile.changed` | CompiledSpell 해시 | 컴파일 트리 미리보기 다시 그림, 바뀐 수치 0.5초 밝힘 (U 4.11) |
| `compile.diagnostic` | code, slot | K-71 조립 경고 딱지 |

표시 계층이 지킬 한도(코어는 정보만 준다): 적중음 1초 8번, 그림자 구체 지나감 1초 6번, 상태 걸림 소리 같은 적 0.5초에 1번, 입자 예산은 E 9.3이 유일한 기준 (E 13.1, E 19.2, D 2.8).

---

## 20. 만드는 순서 (의존 관계만)

1. `Fx`, `FxVec2`, `Angle`과 상수·거듭제곱·sin 표 (2절) — 모든 계산이 기댄다.
2. `Element`, `AffinityTable`과 데이터 검증 테스트(TV-A08~A14) — 피해 공식이 기댄다.
3. `BlockDef` 표 16종과 데이터 테스트(TV-A01~A07).
4. `WandLoadout`, `WandCombatProfile` → `SpellCompiler.Compile`, 진단, 가지치기, 열(TV-C, TV-D01~D16, TV-E01~E06).
5. 글자 출력·파서(14.5, TV-J01, TV-J06) — 이후 테스트 입력 형식.
6. `ProjectileSystem`(상태 기계, 틱 순서, 충돌, 블록별 동작) + 화면 상한(TV-G, TV-E07~E09).
7. `DamageCalculator`(TV-H) — 5와 6이 있어야 통합 테스트.
8. `StatusEffectSystem`(감속, 잠식)(TV-I).
9. `SpellExecutor.Execute`와 트리거 하위 스코프(TV-F01~F15). 과열 명세의 임계 판정과 맞물린다.
10. 결투 변환(13.1)과 허상 복사 변환(13.2)(TV-F16~F25, 결투 행).
11. 미리보기 자료와 이벤트 발행(14.4, 19절) — 야영 화면과 표시 계층이 받는다.
