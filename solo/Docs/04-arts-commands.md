# 04. 커맨드 입력과 비기(art) — 솔로 Unity 구현 요약서

대상: 순정 Unity(MonoBehaviour, float 허용) 솔로 구현자. 이 문서만 보고 판정기·비기·해금을 만들 수 있게 썼다.
원문: `docs/spec/03-command-parser.md`(이하 S03), `docs/spec/04-characters-wands.md`(이하 S04), `data/sim/arts.json`, `data/sim/tries.json`, `docs/spec/14`(id 정본), `data/names/names.ko.json`.
괄호 안은 출처 절. [임시] = 원문이 안 정해서 명세가 넣은 기본값. **솔로 제외** = 온라인 전용이라 이번 구현에서 뺀다.

---

## 0. 먼저 알아 둘 것

### 0.1 id는 spec 14 / arts.json 쪽이 정본 (CLAUDE.md 5절)

문서마다 비기 id가 다르다. **코드·데이터·저장 파일은 아래 "정본 id"만 쓴다.** 화면 이름은 이름 표 열쇠 = 정본 id 그대로.

| 번호(enum 순서, 바꾸지 않음) | 정본 id (arts.json, names) | S03 이름 | S04 1.3 id(옛) | 화면 이름(names.ko.json) | 종류 |
|---|---|---|---|---|---|
| 0 | `art.burst` | ManaRelease ① | art.mana_burst | 마력 방출 | 공용 |
| 1 | `art.recoil` | RecoilShot ② | art.recoil_shot | 반동 사격 | 공용 |
| 2 | `art.fan` | FanSpread ③ | art.fan_spread | 부채 전개 | 공용 |
| 3 | `art.wings` | TwinWing ④ | art.twin_wing | 양익 사격 | 공용 |
| 4 | `art.afterimage` | AfterimageStep ⑤ | art.afterimage_step | 잔상 걸음 | 공용 |
| 5 | `art.spear` | PiercingLance ⑥ | art.piercing_lance | 꿰뚫는 창 | 공용 |
| 6 | `art.vent` | VentBurst ⑦ | art.heat_vent | 방열 폭발 | 공용 |
| 7 | `art.octastar` | EightfoldSpread ⑧ | art.eight_way | 팔방 전개 | 공용 |
| 8 | `art.thunder_spear` | ThunderLance ⑨ | art.thunder_lance | 비장: 뇌광창 | 공용(비장, `kind: secret`) |
| 9 | `art.bloom` | FullBloom ⑩ | art.full_bloom | 비장: 만개 | 공용(비장, `kind: secret`) |
| 10 | `art.kiln` | Kiln ★ | art.kiln | 불가마 | 고유 `char.1` |
| 11 | `art.bolt_dash` | BoltDash ★ | art.thunder_dash | 벼락 질주 | 고유 `char.2` |
| 12 | `art.copy_circle` | Transcribe ★ | art.transcribe | 베껴 쓰기 | 고유 `char.3` |
| 13 | `art.ice_screen` | IceScreen ★ | art.ice_screen | 얼음 병풍 | 고유 `char.4` |
| 14 | `art.shadow_hand` | ShadowHand ★ | art.shadow_hand | 그림자 손 | 고유 `char.5` |

- 캐릭터 id는 `char.1`~`char.5`(S03의 `char_1` 표기는 옛것). 표시 이름 "제자 1"~"제자 5". 작업용 이름(달구·초롱·갈피·성에·그믐)은 이 문서 설명용이고 코드에 쓰지 않는다.
- 이 문서 아래에서는 읽기 쉽게 S03 영문 이름(ManaRelease 등)을 섞어 쓰지만, 구현에서는 위 표로 정본 id에 붙인다.

### 0.2 시간 단위 (S03 1.1·1.2)

- 게임 1초 = 60틱. 1틱 = 16.667ms.
- 판정기 시각은 정수 **입력 단위 u**: `시각 = 틱 × 256 + 틱 안 오프셋(0~255)`, 1u = 1/15,360초.
- **권장(솔로 Unity)**: 결정론은 필요 없지만 테스트 벡터가 경계값(같으면 통과)을 정수로 검사하므로, 입력 시각을 `long u = (long)Math.Round(seconds × 15360)`로 바꿔 정수 비교한다. float 초로 직접 비교하면 TV-A096(1,075u), TV-A102(2,765u), TV-A088(4,608u) 같은 경계가 틀어질 수 있다.
- 비교 규칙: **경과 > 한도이면 실패, 경과 ≤ 한도이면 통과**(한도와 같으면 통과). (S03 1.2)

### 0.3 판정기 상수 — `data/sim/tries.json`의 `parser` 절

| JSON 필드 | 값(초) | u | 틱 | 의미 |
|---|---|---|---|---|
| `just_window_seconds` | 0.07 | **1,075** | 4.2 | 섬(저스트) 창: 마지막 방향 → K |
| `gap_full_bloom_seconds` | 0.18 | **2,765** | 10.8 | 만개의 모든 간격(앞→뒤, 뒤→뒤, 뒤→앞, 앞→K) |
| `gap_short_seconds` | 0.2 | 3,072 | 12 | 앞→뒤, 옆→앞, 앞→옆, 좌→우(반대쪽 옆), char.1의 뒤→뒤 |
| `gap_same_side_seconds` | 0.25 | 3,840 | 15 | 같은 쪽 옆 두 번(잔상 걸음) |
| `gap_default_seconds` | 0.3 | 4,608 | 18 | 기본 간격 = 커맨드 초기화 시간 = 마지막 방향 → K 한도 |
| `art_cooldown_seconds` | 0.4 | 6,144 | 24 | 비기 공통 재사용 대기 |
| `k_buffer_window_seconds` | 0.1 | 1,536 | 6 | 대기 끝 전 K 보관 창 |
| `max_debt` | 2 | — | — | 그믐(char.5) 외상 빚 상한(결정 개수) |

- 0.07×15,360 = 1,075.2, 0.18×15,360 = 2,764.8 → **가장 가까운 정수 1,075 / 2,765로 반올림**한다(원문 지정). 나머지는 정확히 나눠진다.
- 최근 방향 입력 기록 크기 `DirHistorySize = 8`은 자료구조 크기라 JSON에 없다(코드 상수).
- `tries.json`의 `characters[]`: 캐릭터별 트라이에 넣을 비기 11개(공용 10 + 자기 고유 1). `character`(char.N), `arts`(정본 id 배열).

---

## 1. 키 입력 (S03 3절)

| ID | 규칙 |
|---|---|
| R-01 | WASD 이동(이동 방향 = 시야 = 발사 방향, 8방향), **J 발사, K 비기 전용**, **Shift(누르는 동안) 자세**, **Space(누르는 동안) 반전 자세**. 전투는 키보드만. [확정] |
| R-02 | 대체 배치: 방향키 + Z 발사 / X 비기 / C 자세 / V 반전. 모든 키 재설정 가능 → 판정기는 **논리 키**(이동 4방향, Fire, Art, Stance, Reverse)만 받는다. (재설정 화면 이름 열쇠 `str.key_rebind.action.art`/`.stance`/`.reverse_stance`) |
| R-03 | 물리 키 기준. 한글 입력기 영향 없어야 함(Unity Input System의 `Key`/`KeyCode`는 물리 키라 괜찮다). |
| R-04 | 방향 입력은 **자세 중에만** 커맨드로 읽는다. **시야 기준 상대 방향**. 반전 자세도 자세다. |
| R-05 | 자세 진입 시 시야가 대각선이면 **마지막에 누른 방향 키(상하좌우)**로 맞춘다. 자세 중엔 대각 사격 없음 → 자세 중 시야는 항상 4방향. |
| R-06 | 모든 비기는 K로 끝난다. **자세 밖의 K는 항상 마력 방출(`art.burst`).** |
| R-07 | **누르는 순간(KeyDown)만** 입력이다. 이미 누르고 있던 키는 입력 아님(떼고 다시 눌러야 함). 키보드 자동 반복도 입력 아님(U-11). |
| R-08 | 섬: 방향을 누르고 0.07초(1,075u) 안(경계 포함)에 K. 방향이 먼저여야. 늦으면 같은 자리 일반 기술. K가 먼저면 그 시점 커맨드로 처리(앞→K = 부채 전개). |
| R-10 | 마지막 방향 입력 후 0.3초(4,608u) 초과면 커맨드는 루트로. |
| R-11 | 자세 진입, 해제, 반전 때, 일시정지 때 커맨드는 루트로(초기화). |
| R-12 | Shift를 누른 채 Space 누르면 잠긴 시야만 180도 뒤집는다. Space를 떼면 시야는 뒤집힌 방향으로 남고, 이동 키가 눌려 있으면 그 방향으로 바로 바뀐다. 둘 다 "반전" → 초기화(C-16 [임시 해석]). |
| R-14 / U-08 | **같은 시각(같은 틱·같은 오프셋)이면 처리 순서: 자세 키(Shift/Space) → 방향 → J → K → 일시정지.** 같은 종류 안에서는 누른 순번. (참고 구현 `src/Wizard.Sim/Input/Types/InputOrder.cs:8`) |
| R-15 | 결정이 모자라면 불발. 더 싼 기술로 안 바뀐다. 예외 둘: 고유 비기는 부모 기술(결정 1)로 한 번 대체, 그믐은 외상 먼저(7절). |
| R-16 | 재사용 대기 0.4초. 끝나기 0.1초 전부터 누른 K는 보관 후 끝날 때 처리(8절). |
| R-18 | 대시 없음. |
| U-09 | J 입력은 커서에 영향 없다. |
| U-10 | 자세 밖에서 누른 방향은 기록 안 함. |

### 1.1 물리 방향 → 상대 방향 `CmdDir` (S03 2.3, 3.1 [임시])

`CmdDir`: `Forward=0(F, 앞)`, `Back=1(B, 뒤)`, `Left=2(L, 좌)`, `Right=3(R, 우)`. 트라이 알파벳은 이 4개(좌우 따로 노드).

```
rel(p, facing):            // facing ∈ {Up, Down, LeftScreen, RightScreen} (자세 중 항상 4방향)
  p == facing              -> F
  p == opposite(facing)    -> B
  p == rotateCCW90(facing) -> L     // 캐릭터 기준 왼쪽 (U-27)
  p == rotateCW90(facing)  -> R
```
- 반전 자세 중 `facing`은 뒤집힌 시야.
- 좌/우 이름은 결과에 영향 없음(트라이가 좌우 대칭, "같은 쪽/반대쪽"만 구분).
- 대각선을 만드는 두 키 누름은 각각 별도 방향 입력 [임시].
- 잔상 걸음의 이동 방향은 상대 방향이 아니라 **마지막으로 누른 물리 방향**.

### 1.2 Unity 구현 메모(이 문서의 권고, 원문 없음)

- 한 프레임에 여러 KeyDown이 들어오면 위 R-14 순서로 처리하고, 같은 프레임 키는 같은 시각으로 둔다(→ 방향+K 동시면 간격 0 = 섬. TV-B01과 같은 동작).
- 더 정밀하게 하려면 Input System 이벤트의 `time`을 써서 u로 바꾼다. 어느 쪽이든 판정기는 `(시각 u, 논리 키, 누름)`만 받는 순수 C# 클래스로 둔다(R-17 정신).

---

## 2. 트라이 (S03 4·5절)

### 2.1 트리(원문 그대로)

```
루트 ─ K ················ ① 마력 방출
 ├ 앞 ─ K ·············· ③ 부채 전개
 │  ├ 앞 ─ K ··········· ⑥ 꿰뚫는 창
 │  │     └ 섬 ········· ⑨ 비장: 뇌광창
 │  ├ 뒤 ─ K ··········· ⑧ 팔방 전개
 │  │  └ 뒤 ─ 앞 ─ K ··· ⑩ 비장: 만개
 │  └ 옆 ─ K ··········· ★ char.3: 베껴 쓰기
 ├ 뒤 ─ K ·············· ② 반동 사격
 │  ├ 앞 ─ K ··········· ⑦ 방열 폭발
 │  ├ 뒤 ─ K ··········· ★ char.1: 불가마
 │  └ 옆 ─ K ··········· ★ char.4: 얼음 병풍
 └ 옆 ─ K ·············· ④ 양익 사격
    ├ 옆(같은 쪽) ─ K ··· ⑤ 잔상 걸음
    ├ 옆(반대쪽) ─ K ··· ★ char.5: 그림자 손
    └ 앞 ─ K ··········· ★ char.2: 벼락 질주
```
- **트라이는 판 시작 때 캐릭터별로** 만든다: 공용 10 + 자기 고유 1 = 11종. 다른 캐릭터의 ★ 노드는 없다.
- 노드 수: char.1 13개, 나머지 14개(루트 포함). 최대 깊이 4.

### 2.2 경로 (`arts.json`의 `paths`, 좌우 대칭 둘 다)

| 비기 | 경로 | 슬롯 |
|---|---|---|
| burst | `[]`(루트) | K |
| recoil | `[B]` | K |
| fan | `[F]` | K |
| wings | `[L]`,`[R]` | K |
| afterimage | `[L,L]`,`[R,R]` | K |
| spear | `[F,F]` | K |
| thunder_spear | `[F,F]` | **섬 슬롯**(`just: true`) |
| vent | `[B,F]` | K |
| octastar | `[F,B]` | K |
| bloom | `[F,B,B,F]` | K (중간 노드 `FBB`는 기술 없는 통과 노드) |
| kiln | `[B,B]` | K, 부모 대체 |
| bolt_dash | `[L,F]`,`[R,F]` | K, 부모 대체 |
| copy_circle | `[F,L]`,`[F,R]` | K, 부모 대체 |
| ice_screen | `[B,L]`,`[B,R]` | K, 부모 대체 |
| shadow_hand | `[L,R]`,`[R,L]` | K, 부모 대체 |

### 2.3 노드 필드 (S03 5.3)

`Id`(BFS 순, 0=루트, 자식 F,B,L,R 순), `Depth`(0~4), `Path`, `Child[4]`(없으면 -1), `Next[4]`(실패 링크까지 펼친 DFA 전이, 항상 유효), `Fail`, `KArt`(없으면 null), `JustArt`(FF 노드만 thunder_spear), `InEdgeGap`(들어오는 간선 허용 간격 u, 깊이 0~1은 없음), `UniqueFallback`(고유 노드만 true), `IsFullBloom`(FBBF만 true), `Parent`.
- 해금 여부는 노드에 굽지 않고 `UnlockSet`에서 조회(판 중 해금이 바뀌어도 트라이 재생성 불필요).

### 2.4 간선 간격 함수 — 이 순서 그대로 (S03 4.2)

```
EdgeGap(path, character):   // 깊이 ≥ 2 노드만. a = path[^2], b = path[^1]
  if path == [F,B,B] or [F,B,B,F]:        return 2765   // 만개
  if a is Side and b == a:                return 3840   // 같은 쪽 옆
  if a == F and b == B:                   return 3072
  if a is Side and b == F:                return 3072
  if a == F and b is Side:                return 3072
  if a is Side and b is Side and a != b:  return 3072
  if character == char.1 and path == [B,B]: return 3072
  return 4608
```
- 깊이 1 노드로 들어오는 간선은 간격 없음(직전 입력이 없다).
- 앞→뒤 간선(FB)은 팔방 전개에 3,072, 만개 판정 때는 2,765로 재검사.

### 2.5 생성 (S03 5.4) — 아호-코라식

```
BuildTrie(character):
  root = node(depth 0)
  for art in 공용10 + [고유(character)]:
    for path in art.paths:
      n = root; for d in path: n = n.Child[d] ?? 새 자식(depth+1, Path = n.Path+d, Parent = n)
      if art == thunder_spear: n.JustArt = art else n.KArt = art
      if art.kind == unique: n.UniqueFallback = true
      if art == bloom: n.IsFullBloom = true
  for n (depth ≥ 2): n.InEdgeGap = EdgeGap(n.Path, character)
  // 실패 링크(BFS)
  root.Fail = root; 루트의 자식들 Fail = root; 큐에 넣음
  while queue: u = dequeue
    for d in F,B,L,R: v = u.Child[d]; if null continue
      f = u.Fail; while f != root and f.Child[d] == null: f = f.Fail
      v.Fail = (f.Child[d] != null && f.Child[d] != v) ? f.Child[d] : root
      enqueue v
  // DFA
  for n in BFS 순: for d: n.Next[d] = n.Child[d] ?? (n == root ? (root.Child[d] ?? root) : n.Fail.Next[d])
```
- 루트는 네 방향 자식을 모두 가지므로 방향 입력 뒤 커서는 항상 깊이 ≥ 1.
- 참고 구현: `src/Wizard.Sim/Arts/Parser/CommandTrie.cs:71`(Build), `:236~258`(실패 링크), `:261~280`(DFA), `:294`(EdgeGap).

### 2.6 생성 결과 검사표 (S03 5.5)

공통 노드:

| 노드 | Fail | InEdgeGap | K 기술 |
|---|---|---|---|
| root | root | — | burst |
| B | root | — | recoil |
| F | root | — | fan |
| L / R | root | — | wings |
| BF | F | 4,608 | vent |
| FB | B | 3,072 | octastar |
| FF | F | 4,608 | spear (+섬 thunder_spear) |
| LL / RR | L / R | 3,840 | afterimage |
| FBB | **char.1은 BB / 나머지 B** | 2,765 | 없음 |
| FBBF | BF | 2,765 | bloom |

고유 노드:

| 캐릭터 | 노드 | Fail | InEdgeGap | K |
|---|---|---|---|---|
| char.1 | BB | B | 3,072 | kiln |
| char.2 | LF / RF | F / F | 3,072 | bolt_dash |
| char.3 | FL / FR | L / R | 3,072 | copy_circle |
| char.4 | BL / BR | L / R | **4,608** | ice_screen |
| char.5 | LR / RL | R / L | 3,072 | shadow_hand |

DFA 전이 표 예시 — char.1(달구) `Next[노드][F,B,L,R]` (나머지 캐릭터 표는 S03 5.6):

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

char.5(그믐) 차이점: `L→R = LR`, `R→L = RL`, `LR: L→RL, R→RR`, `RL: L→LL, R→LR`, `LL: R→LR`, `RR: L→RL`, BB 노드 없음(`B→B = B`).

---

## 3. 판정기 상태 기계 (S03 6절)

### 3.1 상태

| 필드 | 의미 |
|---|---|
| `trie`, `character` | 판 시작 때 고정 |
| `cursor` | 노드 id, 0 = 루트 |
| `dirTimes` | 최근 방향 입력 시각(u) 링 버퍼, 용량 8(오래된 것부터 버림) |
| `hasDir`, `lastDirTime` | 기록 유무, 마지막 방향 시각 |
| `inStance` | Shift 또는 Space가 눌려 있음 |
| `cooldownEnd` | 재사용 대기 끝 시각(u). 0 = 대기 없음 |
| `buffered` | 보관된 K의 판정 결과(최대 1개) |

- 결정·빚은 판정기가 갖지 않고 지갑(`ICrystalWallet{Crystals, Cap, Debt, HasCredit}`)에서 읽고 쓴다. `HasCredit`은 char.5만 true(실제로는 wand.5 특성 `wand_trait.on_credit`, 지팡이가 캐릭터 전용이라 결과 같음).
- 커서·기록은 판 안의 상태, 저장하지 않는다.

### 3.2 전이표

상태: `OutOfStance` / `StanceRoot`(자세, 커서=루트) / `StanceChain`(자세, 커서≠루트). 직교: `Ready` / `Cooldown` / `CooldownBuffered`.

| 현재 | 사건 | 조건 | 동작 | 다음 |
|---|---|---|---|---|
| OutOfStance | 방향 누름 | — | 무시(기록 안 함) | OutOfStance |
| OutOfStance | K 누름 | — | 루트 기술(마력 방출) 판정 → 결제 | OutOfStance |
| OutOfStance | Shift/Space 누름 | — | Reset | StanceRoot |
| Stance* | 방향 누름 | `hasDir && t − lastDirTime > 4608` | 먼저 Reset, 그다음 아래 | — |
| Stance* | 방향 누름 | — | `cursor = Next[cursor][d]`, dirTimes에 t 추가 | StanceChain |
| StanceChain | 시간 경과 | `now − lastDirTime > 4608` | 지연 처리(다음 입력·미리보기 때 루트로 본다) | StanceRoot |
| Stance* | K 누름 | — | 판정(4절) → 결제(5절) → Reset | StanceRoot |
| Stance* | 자세 해제(두 키 다 뗌) | — | Reset | OutOfStance |
| Stance* | 반전(Space 누름/뗌, Shift 중 Space) | — | Reset | StanceRoot |
| 아무 상태 | 일시정지 | — | Reset | 유지 |
| 아무 상태 | 판 시작, 웨이브 시작 | — | Reset + 재사용 대기 해제(U-35) | — |
| Ready | 비기 발동 | — | `cooldownEnd = 판정시각 + 6144` | Cooldown |
| Cooldown | K 누름 | `t < cooldownEnd − 1536` | 무시(커서·기록 그대로) | Cooldown |
| Cooldown | K 누름 | `cooldownEnd − 1536 ≤ t < cooldownEnd` | 그 시각 t로 판정+결제를 끝내 결과 보관, Reset | CooldownBuffered |
| CooldownBuffered | K 누름 | — | 무시 | 유지 |
| Cooldown(Buffered) | 시각이 cooldownEnd에 닿음 | — | 보관 결과를 발동(틱 = `ceil(cooldownEnd/256)`), 발동이면 새 대기 | Ready / Cooldown |

- Reset = 커서 루트 + dirTimes 비움. 발동하거나 불발하면 항상 Reset.
- **불발은 재사용 대기를 시작하지 않는다**(U-05).
- 자세 진입·해제·반전 사건 = `inStance = Shift||Space` 값이 바뀌는 순간 + 시야가 180도 뒤집히는 순간.
- 참고 구현: `src/Wizard.Sim/Arts/Parser/CommandParser.cs:64`(Reset), `:76`(자세 변화), `:87`(방향), `:122`(K·대기·보관), `:160`(보관 발동), `:177`(미리보기).
  - 참고 구현의 해석 차이 둘(원문 [임시] 범위): ① 보관 결과를 낸 뒤 새 대기를 "실제 낸 시각(틱 시작) + 6144"로 시작(`:170`). ② 웨이브 시작 Reset은 대기를 풀지만 보관된 K는 버리지 않는다(`:62~72`). 솔로에서도 이대로 따르면 된다.

---

## 4. K 판정 알고리즘 (S03 7.1·7.2)

```
OnDirectionPress(d, t):
  if !inStance: return
  if hasDir && t - lastDirTime > 4608: Reset()
  cursor = Next[cursor][d]; dirTimes.Push(t); lastDirTime = t; hasDir = true
  emit cmd.dir_input(d, chained = depth(cursor) ≥ 2 && 마지막 간선 간격 통과)

Evaluate(t, wallet, unlocks, commit) -> ArtDecision:
  if !inStance: node = root
  else if hasDir && t - lastDirTime > 4608: node = root (commit이면 Reset)
  else node = cursor
  loop:                                         // 실패 링크를 따라 첫 유효 노드
    if node.KArt != null && unlocked(node.KArt):
      if node.IsFullBloom:
        (ok, idx, lateBy) = CheckFullBloom(node, t)
        if !ok: return Fizzle(FullBloomLate, idx, lateBy)   // 실패 링크로 안 감, 결정 소모 없음
        break
      if GapsOk(node): break
    if node == root: break                      // 루트(마력 방출)는 항상 해금·간격 없음
    node = node.Fail
  art = node.KArt
  if node.JustArt != null && unlocked(node.JustArt) && hasDir && 0 ≤ t - lastDirTime ≤ 1075:
    art = node.JustArt; isJust = true
  return Pay(art, node, wallet, commit)

GapsOk(node):                // 깊이 k 노드: 최근 k개 방향 시각을 경로 위 노드의 InEdgeGap으로
  k = node.Depth; if k ≤ 1: return true
  ts = dirTimes 마지막 k개(오래된 → 최근)
  for i in 1..k-1: if ts[i] - ts[i-1] > InEdgeGap(Path[0..i] 노드): return false
  return true                // 마지막 방향→K는 0.3초 초기화가 이미 보장 → 따로 안 봄

CheckFullBloom(node, tK):    // 만개 노드만. 네 간격 + K 간격을 전부 2765로
  seq = dirTimes 마지막 4개 + [tK]
  for i in 1..4: if seq[i]-seq[i-1] > 2765: return (false, i+1, seq[i]-seq[i-1]-2765)
  return (true)
```

규칙 정리:
- 해금 안 된 노드 = 기술 없는 노드로 보고 실패 링크로 내려간다.
- 실패 링크로 내려온 노드도 **그 노드 경로(=입력열의 접미사)의 간격만** 본다. 앞쪽 입력이 늦었어도 접미사가 맞으면 통과(TV-A116).
- 만개 해금 + 커서 FBBF + 간격 초과 → **불발**(유일한 예외). 만개 미해금 → 실패 링크로 방열 폭발(BF) 또는 그 아래.
- 섬은 FF 노드가 선택됐을 때만. 섬 기술 미해금이거나 0.07초 넘기면 꿰뚫는 창.
- 만개 뒤에 방향 하나 더(앞,뒤,뒤,앞,앞) → 커서 FF → 꿰뚫는 창(TV-A103).
- 참고 구현: `CommandParser.cs:202`(Evaluate), `:351`(GapsOk), `:372`(CheckFullBloom).

---

## 5. 결제 (S03 7.3, 9절, S04 5.1·7.4)

```
Pay(art, node, w, commit):
  r = TryPay(art, w); if ok: Fire(art)
  if node.UniqueFallback:                          // 고유 비기만, 한 번만
    p = Parent(node).KArt                          // recoil / wings / fan (결정 1)
    if p != null && unlocked(p) && TryPay(p, w): Fire(p, viaFallback = true)   // [임시] 부모 미해금이면 불발(C-02)
  Fizzle(NoCrystal)                                // 더 내려가지 않는다

TryPay(art, w):
  if art.cost == "all":                            // 만개
    if w.Crystals ≥ 3: repeats = w.Crystals; w.Crystals = 0; ok   // 외상 불가
    else fail
  c = art.cost (1 또는 2)
  if w.Crystals ≥ c: w.Crystals -= c; ok
  if w.HasCredit && art.credit:                    // char.5만
    need = c - w.Crystals
    if w.Debt + need ≤ 2: w.Crystals = 0; w.Debt += need; ok(debtUsed = need)
  fail
```
순서: ① 그믐 외상 먼저 ② 그래도 안 되면 고유 비기만 부모로 한 번(외상 포함 다시 봄) ③ 그 밖 불발.
- `commit=false`면 지갑을 바꾸지 않는 계산(미리보기).
- 만개 반복 수 = 소모 직전 결정 수(최소 3, 최대 캐릭터 결정 상한: char.3 4, char.1·2 5, char.4 6, char.5 7).
- 참고 구현: `CommandParser.cs:266`(Pay), `:314`(TryPay).

### 5.1 판정 결과 `ArtDecision` (S03 7.4 [임시 구조])

`Outcome(Fire/Fizzle)`, `Art`(나간 기술 / 불발이면 고른 기술), `SelectedArt`(부모 대체 전), `ViaFallback`, `IsJust`, `CrystalsPaid`, `DebtAdded(0~2)`, `CrystalsAfter`(그림자 계약 계산용), `Repeats`(만개), `Reason(None/NoCrystal/FullBloomLate)`, `LateIndex(2~5)`, `LateBy(u)`, `JudgeTime(u)`, `NodeId`, (섬을 놓친 초과량 `JustLateBy` — 연습장용).

### 5.2 결정 지갑·그믐 외상 (S03 9.2, S04 3.3·7.3·7.4)

- 결정은 판 안에만. 판 시작 0개. 웨이브 끝 남은 결정 개당 5골드(빚부터 갚고 남은 것만).
- 결정 획득(콤보·별똥 결정)은 빚부터 갚는다:
  ```
  GainCrystal(n): repeat n: if debt>0: debt-- else if crystals<cap: crystals++ (상한이면 버림)
  ```
- 빚: 0~2, **판 끝까지 남음**(웨이브 끝·야영에도). 빚 > 0인 동안 받는 피해 +1칸(적 공격 3칸 상한 뒤에 더함 → 최대 4칸). 빚 > 0일 때 맞으면 결정 파괴 대신 빚 +1(최대 2). 결정 0·빚 0에서 맞으면 아무 일 없음(Q-15 [임시]).
- 피격 시 결정 손실: char.1·2·3 1개, char.4 0개(wand.4 특성 얼린 결정, 콤보는 0), char.5 2개.
- 빚 2면 외상 불가. 결정 0·빚 1에서 2결정 고유 비기 → 부모 양익 사격(1)이 외상으로 나가고 빚 2.
- HUD: 빚 결정 = 결정 칸의 보라 테두리 빈 칸. "지금 K" 옆 "외상 N".

---

## 6. HUD 미리보기 "지금 K" (S03 11절)

- 매 프레임 `Evaluate(now, wallet, unlocks, commit:false)` 결과를 보여 준다. 재사용 대기는 보지 않는다.
- 표시 문자열: `str.battle_hud.now_k` = "K = {art} (◆×{n})", 외상이면 `str.battle_hud.now_k_debt` = "{line} 외상 {n}". `{art}`는 이름 표 `art.*`.
- 자세 밖: 항상 마력 방출(1).
- 고른 노드에 **해금된 섬 기술이 있으면 항상 두 줄**(U-29): 윗줄 = `Evaluate(lastDirTime)`(섬 기술), 아랫줄 = `Evaluate(lastDirTime + 1075 + 1)`(섬 창을 넘긴 일반 기술; 이미 지났으면 now 결과).
- 부모로 내려갈 자리(결정 부족)는 내려간 기술명. 그믐은 "외상 N".
- 불발 자리는 기술명 대신 불발 표시. 만개 간격 초과가 확정된 자리는 "불발(n번째 입력이 x초 늦음)" [임시] — "불발"·"섬:" 두 줄 머리 글자는 **이름 표 열쇠 없음 → 미정**(아래 미정 목록).
- 최근 입력 5개를 캐릭터 주변에 화살표로. 이어지면 화살표 사이 선, 간격 초과면 끊김(`chained`).
- 참고 구현: `CommandParser.cs:177`.

### 6.1 늦음 문구 (S03 7.5)

- 초 = `ceil(LateBy × 100 / 15360) / 100`(소수 둘째 자리 **올림**). 153u → 0.01, 307u → 0.02, 1,075u → 0.07, 1u → 0.01. (참고 `src/Wizard.Sim/Arts/Parser/LateBy.cs:238`)
- 만개: `str.dummy_practice.fail.late_input` = "{n}번째 입력이 {sec}초 늦음" (머리 위 표시와 연습장 둘 다 이 틀을 쓴다).
- 섬을 놓쳐 꿰뚫는 창이 나간 경우 연습장: 초과량 = `(t_K − lastDirTime) − 1075`, `str.dummy_practice.fail.late_k` = "K가 {sec}초 늦음".
- 결정 부족: `str.dummy_practice.fail.no_crystal` = "결정 부족".

---

## 7. 재사용 대기와 K 보관 (S03 10절)

- 공통 대기 0.4초(6,144u, 24틱), 시작 = 판정 시각.
- 대기 끝 0.1초(1,536u) 전부터 누른 K: **누른 시각으로 선택+결제를 끝내** 결과를 보관하고 커서를 비운다. 발동만 대기 끝 틱 `ceil(cooldownEnd/256)`로 미룬다. 그 사이 방향 입력은 새 커맨드로 쌓인다.
- 보관 창 밖 K = 아무 일 없음(커서·기록 그대로). 보관은 1개, 보관 중 추가 K 무시.
- 보관 창 경계: `t = cooldownEnd − 1536`이면 보관된다(TV-B09b).
- 연출: "지금 K" 테두리가 0.4초 동안 다시 참, 보관 중이면 테두리 안 점.

---

## 8. 비기 공통 규칙 (S03 8절)

| ID | 규칙 |
|---|---|
| A-01 | 모든 비기는 열(과열)을 올리지 않는다. |
| A-02 | 비기는 임계 판정·임계 배율을 받지 않는다. |
| A-03 | 강화 배율은 받는다: 솔로(무한 웨이브) `1.15^강화단계`. (결투 `1+0.03×단계`는 솔로 제외) |
| A-04 | 과열 잠금 중: 마력 방출은 쓸 수 있지만 잠금은 안 풀림. 잠금을 푸는 건 스쳐 피하기와 방열 폭발뿐. 다른 비기도 잠금 중 사용 가능(U-07). |
| A-05 | 트리를 실행하는 비기의 피해 속성 = 맞힌 투사체 블록의 속성. 꿰뚫는 창·뇌광창·방열 폭발 = 코어 블록의 속성. 트리를 안 쓰는 고유 비기(불가마·벼락 질주·병풍 파편·그림자 손) = 캐릭터 속성(char.1 화염, 2 번개, 3 비전, 4 냉기, 5 암흑). |
| A-06 | 비기로 맞힌 것은 콤보에 넣지 않는다. |
| A-07 | 넉백·기절·경직은 적 꼬리표에 따라(10절 표). |
| A-08 | 비기 한 번 투사체 최대 160개, 트리 실행 1회 64개, 화면 플레이어 투사체 300개 상한. 넘치면 만개는 마지막 반복부터, 팔방은 정면 외 뒤쪽부터 생략(U-19). |
| A-09 | 실행 함수 `Execute(트리, 위치, 방향, 피해 배율, 열 면제, 진영)` 하나를 비기·사본 마법진·보스가 같이 쓴다. |
| A-10 | 피해 = `기본 × 강화 배율 × (1+변형 보너스 합) × 변이 배율 × 임계 배율(비기는 1) × 비기 배율 × 감쇠^깊이 × 속성 배율 × 대상 상태 배율 × 캐릭터 배율`. |
| A-11 | 그믐 그림자 계약 `(100 + 3k)/100`: 비기에는 **그 비기가 결정을 쓴 뒤 남은 개수 k**(=`CrystalsAfter`). 결정 5에서 2결정 비기 → +9%. 만개 → +0%. 외상 비기 → +0%. 빚은 0개로 센다. |
| A-12 | **T = 지팡이 트리 1회 실행의 피해.** "nT" = 그 n배. (마력탄 하나 낀 +0 지팡이에서 T = 10, S04 0.4) |

### 8.1 발동 순서 (S03 13절 [임시])
1. Fire면 결정은 이미 소모됨. 2. 판정 틱에 효과 시작. 3. 기술별 준비 시간만큼 지연: 꿰뚫는 창 0.08초(5틱), 방열 폭발 0.06초(4틱), 만개 0.25초(15틱), 나머지 0(`arts.json`의 `prep_seconds`, U-14). 4. 효과 실행. 5. 재사용 대기 시작(시각은 판정 시각 기준).

---

## 9. 비기 15종 데이터 — `data/sim/arts.json`

### 9.1 필드 의미

| 필드 | 의미 / 단위 |
|---|---|
| `id` | 정본 id |
| `kind` | `common`(공용) / `secret`(비장, 공용에 속함) / `unique`(고유) |
| `owner` | 고유 비기의 주인 캐릭터(`char.N`) |
| `paths` | 경로 배열. 글자 F/B/L/R(2.2절) |
| `just` | true면 섬 슬롯에 붙는다(thunder_spear만) |
| `difficulty` | 난이도 1/2/3/5(연출 세기·트라이 화면의 점) |
| `cost` | 결정 비용 정수, 또는 `"all"`(만개: 결정 전부) |
| `min_cost` | `cost:"all"`의 최소 결정(3) |
| `credit` | 그믐 외상 허용(만개만 false) |
| `fallback` | 고유 비기의 부모 대체 기술 id |
| `unlock.at_start` | 시작 해금(burst, recoil, 모든 고유) |
| `unlock.prereq` | 후보가 되려면 먼저 가져야 하는 비기 |
| `unlock.min_wave` | 이 번호 이상의 해금 차례에서만 후보(thunder_spear 4, bloom 6) |
| `effect.damage` | 비기 배율 = T의 배수(burst 2 = 2T). 불가마는 **지대 1회당** 0.25T, 병풍은 **파편** 3T |
| `effect.side_damage` | 곁가지 배율(T 배수): fan 0.7, octastar 0.6, bloom 1.2 |
| `effect.branches` | 트리 실행 갈래 수 |
| `effect.branch_angle_degrees` | 갈래 간 각도(도) |
| `effect.knockback` | 넉백 거리(칸, 1칸 = 64px) |
| `effect.self_move` | 시전자 이동 거리(칸) |
| `effect.stagger_seconds` | 경직(초). recoil은 "돌진 예고 취소 시" 경직, ice_screen은 "부딪힌 돌진 적" 경직 |
| `effect.invuln_seconds` | 시전자 무적(초) |
| `effect.width` / `range` | 관통탄·길 폭 / 사거리(칸) |
| `effect.ignores_armor` | 선돌지기 철갑 무시 |
| `effect.radius` | 반경(칸) |
| `effect.heat_per_t` | 방열 폭발: 피해 = T × (열 ÷ 25) |
| `effect.clear_radius` | 적 탄 소거 반경(칸) |
| `effect.prep_seconds` | 준비 시간(초, [임시] U-14) |
| `effect.stun_seconds` / `boss_halt_seconds` | 기절 / 보스 동작 멈춤(초) |
| `effect.repeat_interval_seconds` | 만개 반복 간격 0.12초 |
| `effect.duration_seconds` / `interval_seconds` | 지속 / 반복 간격(초) |
| `effect.slow_percent` | 감속 % |
| `effect.heat_absorb` | 불가마 열 흡수 합계(열 점수 16) |
| `effect.max_concurrent` | 동시 개수 1(새로 깔면 이전 것 사라짐) |
| `effect.max_copies` | 사본 따라 쏘기 최대 횟수 |
| `effect.offset` | 시야 정면 거리(칸) |
| `effect.thickness` / `shard_length` | 병풍 두께 / 파편 범위 길이(칸) |
| `effect.pull_distance` | 당김 최대 거리(칸) |
| `duel` | **솔로 제외**(결투 값) |

### 9.2 공용 비기 10종 (S03 12절)

| # | id | 커맨드 | 난이도 | 결정 | 외상 | 효과(솔로) |
|---|---|---|---|---|---|---|
| ① | burst | K (자세 밖 K도) | 1 | 1 | 예 | 시야 방향으로 트리 1회, 비기 배율 2 → **2T**. 열 면제. |
| ② | recoil | 뒤, K | 2 | 1 | 예 | 트리 1회(1T). 그 트리 탄에 맞은 적 넉백 2칸(꼬리표별). 시전자 시야 반대쪽으로 2칸 이동(벽 앞 멈춤). **돌진 예고 중인 꼬리표 없는 일반 적**(예: 덤불 늑대)을 맞히면 돌진 취소 + 0.8초(48틱) 경직. 엘리트·중량·보스 돌진은 안 끊김. |
| ③ | fan | 앞, K | 2 | 1 | 예 | 트리 3갈래: 중앙 100%, 좌우 70%, 갈래 각 ±15도 [임시]. |
| ④ | wings | 옆, K | 2 | 1 | 예 | 시야 −90도·+90도로 트리 1회씩, 각 100% [임시]. |
| ⑤ | afterimage | 옆, 옆(같은 쪽), K / 간격 0.25초 | 3 | 2 | 예 | 마지막 누른 물리 방향으로 2.5칸 순간 이동(벽·적 앞 멈춤), 0.15초(9틱) 무적, 그동안 스쳐 피하기 1회만 인정. 피해 없음. |
| ⑥ | spear | 앞, 앞, K | 3 | 2 | 예 | **3T** 한 줄 관통탄 1발(폭 0.5칸, 사거리 12칸, 즉발 선분 [임시]). 연결 슬롯 실행 안 함(T만 씀). 철갑 무시. 코어 블록 속성. 준비 5틱. |
| ⑦ | vent | 뒤, 앞, K | 3 | 2 | 예 | 시전자 중심 반경 3칸 폭발, 피해 = **T × 열/25**(잠금 중이면 열 100 → 4T). 반경 3칸 적 탄 소거, 열 0, 잠금 해제. 코어 블록 속성. 준비 4틱. |
| ⑧ | octastar | 앞, 뒤, K / 간격 0.2초 | 3 | 2 | 예 | 8방향(45도)으로 트리 1회씩: 정면 100%, 나머지 60%. |
| ⑨ | thunder_spear | 앞, 앞, **섬**(마지막 앞 후 0.07초 안 K) | 5 | **1** | 예 | 꿰뚫는 창 모양에 **6T** + 1초(60틱) 기절(꼬리표별). 늦으면 꿰뚫는 창(2결정). |
| ⑩ | bloom | 앞, 뒤, 뒤, 앞, K / 모든 간격 0.18초 | 5 | 전부(최소 3) | **아니오** | 팔방 전개를 결정 수 N만큼 0.12초 간격으로 반복, 각 2배(정면 200%, 나머지 120%). 반복 틱 = 판정 틱 + round(i×7.2) → 0,7,14,22,29,36,43틱 [임시]. 마지막 파동에 반경 3칸 탄 소거. 준비 15틱. |

### 9.3 고유 비기 5종 (S03 14절, S04 5절)

공통: 결정 2, 난이도 3, **처음부터 해금**[확정 DEC 1.23], 부모 보유 조건 없음, 결정 모자라면 부모 기술(결정 1)로 한 번 대체. 위치·시야는 시전 순간 값으로 고정(U-33). 값 범위 3T~6T.

| id | 캐릭터 | 커맨드 / 간격 | 부모 대체 | 효과(솔로) |
|---|---|---|---|---|
| kiln 불가마 | char.1 | 뒤, 뒤, K / 0.2초 | recoil | 시전 순간 발밑에 반경 2.5칸 지대 4초(240틱). 0.5초(30틱)마다 지대 안 적에게 0.25T(화염) × 8회 = 2T(시전 뒤 30,60,…,240틱 [임시]). 지대 안 적 이동 −20%(보스 −10%, 지대 밖으로 나가면 즉시 해제 [임시]). 지대 안에서 쏜 **임계 발사**의 열을 합계 16까지 대신 먹음(임계 판정은 평소처럼 먼저, 남은 만큼만 먹고 나머지는 오름; 스쳐 피하기 열 면제가 남아 있으면 그것을 먼저 쓰고 흡수량 안 씀). 동시 1개, 새로 깔면 이전 것과 남은 흡수량 소멸. 감속은 다른 감속과 더하지 않고 최대값 하나. |
| bolt_dash 벼락 질주 | char.2 | 옆, 앞, K / 0.2초 | wings | 시야 정면 4칸을 적을 뚫고 이동(벽·기둥 앞 멈춤), 판정 틱에 즉시 이동 [임시]. 지나간 길 폭 1.0칸 위 적마다 1회 3T(번개) + 0.6초(36틱) 경직(꼬리표별). 이동 중 0.2초(12틱) 무적. 길 위 적 탄은 스쳐 피하기 1회로 인정(피뢰침 체질도 1회). |
| copy_circle 베껴 쓰기 | char.3 | 앞, 옆, K / 0.2초 | fan | 시전 자리에 사본 마법진 6초(360틱), 움직이지 않고 맞지 않음. **내가 J로 쏠 때마다** 마법진도 내 시야 방향으로 트리 1회 실행(피해 50%, 열 없음, 콤보 없음, 임계 없음 [임시]). 0.3초(18틱)에 1번까지, 최대 12번 쏘면 소멸. 비기의 트리 실행에는 따라 쏘지 않음. 동시 1개. 64개 상한은 사본 따로, 300개 상한은 함께. |
| ice_screen 얼음 병풍 | char.4 | 뒤, 옆, K / **0.3초** | recoil | 시야 정면 1.5칸 앞에 시야에 수직인 폭 3칸 × 두께 0.5칸 벽 4초(240틱). 곧게 오는 적 탄을 막음(막은 탄은 스쳐 피하기 아님), 포물선 탄 통과. 일반·엘리트 적 못 지나감, 비행은 넘어감(급강하·활강 돌진은 부딪힘), 중량·보스는 닿으면 부숨. 돌진 적이 부딪히면 벽이 깨지고 그 적 1.5초(90틱) 경직(엘리트 0.75초, 중량·보스 경직 없음). 내 탄은 통과(벽 튕김 블록도 튕기지 않음). 사라질 때(만료·부서짐 모두) 벽 앞쪽(시전자 반대편) 폭 3칸 × 길이 2칸에 파편 3T(냉기) 1회. 다시 깔면 이전 것은 파편 없이 제거 [임시 Q-13]. 동시 1개. |
| shadow_hand 그림자 손 | char.5 | 옆, 반대쪽 옆, K / 0.2초 | wings | 시야 정면 3칸 지점(벽 밖이면 벽 안쪽으로 자름 [임시]) 중심 반경 4.5칸 안 적을 중심으로 최대 3칸 순간 당김(중심을 지나치지 않음, 벽에서 멈춤). 3T(암흑) + 0.5초(30틱) 경직. 적 탄은 안 움직임. 닿으면 터지는 적의 폭발 조건은 그대로. |

불가마 쓰는 법 검산: 열 80 근처에서 깔면 열 없는 임계 4발 + 평소 임계 6발 = 10발 연속 임계(S04 5.2). 값 표: 불가마 5.6T, 벼락 질주 3T, 베껴 쓰기 6T, 병풍 3T, 그림자 손 3T.

---

## 10. 적 꼬리표별 반응 (S03 14.6, S04 6절)

| 효과 | 일반 | 엘리트 | 중량 | 보스 | 뿌리 | 비행 |
|---|---|---|---|---|---|---|
| ② 넉백 2칸 | 2칸 | 1칸 | 없음 | 없음 | 없음 | 2칸 |
| ⑨ 기절 1초 | 1초 | 0.5초 | 없음 | 0.3초 동작 멈춤 | 1초 | 1초 |
| 불가마 감속 −20% | −20% | −20% | −20% | −10% | 해당 없음 | −20% |
| 벼락 질주 경직 0.6초 | 0.6초 | 0.3초 | 없음 | 없음 | 0.6초 | 0.6초 |
| 얼음 병풍 통과 | 못 지나감 | 못 지나감 | 닿으면 부숨 | 닿으면 부숨 | 해당 없음 | 넘어감(급강하·활강은 부딪힘) |
| 그림자 손 당김 3칸·경직 0.5초 | 3칸, 0.5초 | 1.5칸, 0.25초 | 없음 | 없음 | 당김 없음, 경직 0.5초 | 3칸, 0.5초 |

공통: 피해는 누구에게나 그대로. 엘리트 = 거리·시간 절반, 감속 그대로, 예고 공격 취소 안 됨. 꼬리표 겹치면 더 강한 면역 쪽. **땅속(burrowed)은 어떤 효과도, 피해도 받지 않음.** ②의 돌진 예고 취소는 꼬리표 없는 일반 적만. 함수형(S04 6절):

```
ResolveCc(kind, tags, base):
  if Burrowed: 0
  Knockback/Pull: (Heavy||Boss||Rooted) ? 0 : Elite ? base/2 : base
  Stagger/Stun:   (Heavy||Boss) ? (Stun && 뇌광창 && Boss ? 0.3초 : 0) : Elite ? base/2 : base
  Slow:           Boss && (블록 감속 || 불가마) ? base/2 : base
```

---

## 11. 해금 (S03 15절) — 오프라인 프로필 포함

### 11.1 규칙

| ID | 규칙 |
|---|---|
| U-R1 | 해금은 **캐릭터별로** 프로필에 남는다. 한 번 해금하면 그 캐릭터로 다음 판에도(어느 지팡이든). [확정 DEC 2.8] |
| U-R2 | 시작 해금 3종: 마력 방출, 반동 사격, 그 캐릭터의 고유 비기. (S03 C-04: "고유 비기 3종"은 오기, 합계 3종) |
| U-R3 | 웨이브 1~6을 **넘길 때마다**(적 전멸로 끝냄, U-25) 해금 차례 1개: 아직 해금 못 한 공용 비기 중 후보 2개에서 1개 무료 선택. |
| U-R4 | 고르는 곳 = 야영의 **비기 탭**(트라이 그림 = 커맨드 목록 = 해금 화면). 야영에서 하는 일은 조립·회복·비기 선택(+허수아비 연습)이고 **강화는 없다**. |
| U-R5 | 공용 8종(①② 제외)을 다 해금하면 차례는 더 없음(두 판이면 다 모인다). |
| U-R6 | 시작 웨이브를 골라 1~6을 건너뛴 판은 출발 야영에서 차례대로 고른다. |
| U-R7 | 부모 기술을 가져야 후보. 후보가 1개뿐이면 1개만 보여 줌. |
| U-R8 | 뇌광창 = 꿰뚫는 창 보유 + 웨이브 4 이후 차례, 만개 = 팔방 전개 보유 + 웨이브 6 이후 차례. |
| U-R10 | 오프라인 프로필과 서버 프로필은 따로. 오프라인 진행은 서버로 옮길 수 없다(옮기는 기능 만들지 않음). |
| U-R13 | 비기 탭: 고유 비기 노드는 새 프로필 첫 화면에서도 "해금". "잠김"/"후보"는 공용에만. 미해금 = 회청 단색 실루엣 + 자물쇠. |
| U-R14 | 정산 화면 "가져가는 것"에 새로 해금한 비기(`str.settlement.take.new_art`). |
| U-R15 | 보스(스승의 허상) 2단계는 플레이어가 해금한 ③④⑧ 중에서 따라 씀(없으면 ①), 첫 만남(웨이브 10)은 해금과 무관하게 셋 다 → `UnlockSet` 조회를 보스 AI에 내준다. |
| 솔로 제외 | U-R9(결투는 전부 사용), U-R11(거래), U-R12(서버 DB `character_state.unlocked_arts`), 서버 판 재실행 반영(15.4 서버 줄). |

선행(부모) 표: ③ fan·④ wings ← ① burst(항상 보유), ⑤ afterimage ← ④, ⑥ spear ← ③, ⑦ vent ← ②(항상 보유), ⑧ octastar ← ③, ⑨ thunder_spear ← ⑥ (+차례 ≥ 4), ⑩ bloom ← ⑧ (+차례 ≥ 6). = `arts.json`의 `unlock`.

### 11.2 판 안 조회

```
IsUnlocked(a) = a == burst || a == recoil || (a가 고유 && owner(a) == 이 캐릭터) || commonUnlocked.Has(a)
```
(다른 캐릭터의 고유는 트라이에 없으므로 무관.) 비트 번호 = 0.1절 enum 번호.

### 11.3 차례와 후보 (S03 15.3)

```
OnWaveCleared(w): if 1 ≤ w ≤ 6: pendingTurns.Enqueue(w)
OnDepartureCamp(startWave): for w in 1..min(startWave-1, 6): pendingTurns.Enqueue(w)
OfferNextTurn():                                   // 비기 탭에서 차례마다 하나씩
  w = pendingTurns.Dequeue()
  eligible = [a ∈ 공용 ③~⑩ | !unlocked(a) && prereq 보유 && w ≥ a.min_wave]
  if eligible 비었음: 차례 소모, 보여 주지 않음 [임시 U-21]
  candidates = eligible.Count == 1 ? eligible : 균등 무작위 2개(판 씨앗에서 갈라 낸 난수, U-20)
  고르면 unlocked += a → 즉시 이 판에 적용, 오프라인 프로필에 즉시 저장
```
- 차례 번호 = 넘긴 웨이브 번호(건너뛴 차례도 그 번호로 min_wave를 본다).
- 같은 야영에 차례가 여럿이면 하나 고른 뒤 다음 차례. 앞에서 고른 것이 뒤 차례의 선행을 채울 수 있다.
- **야영을 떠나기 전 남은 차례를 모두 골라야 한다**(출발 버튼 전에 고르는 판을 띄움) [임시 U-22].
- 판 중 야영의 창이 떠 있어도 시간이 간다는 규칙은 결정론 코어용(CLAUDE.md 3절) — 솔로 Unity에서는 야영이 전투 밖이므로 영향 없음.
- 저장 형식(오프라인 프로필): 캐릭터별 공용 비기 정본 id 배열(예 `["art.fan","art.spear"]`). 시작 3종은 저장 안 해도 항상 참 [임시].
- 허수아비 연습(메인 메뉴 "연습"·야영·광장 — 같은 화면)에서 쓸 수 있는 비기 = 프로필의 해금 그대로(U-26). 솔로에서는 메인 메뉴·야영 진입만(광장 솔로 제외).

### 11.4 관련 화면 글자(이름 표 열쇠)

| 자리 | 열쇠 |
|---|---|
| 야영 탭 | `str.camp.tab.arts`("비기"), `str.camp.practice` |
| 비기 탭 제목·항목 | `str.art_trie.title`, `.command`, `.crystal_cost`, `.difficulty`, `.effect`, `.fallback`, `.interval`("{sec}초"), `.unlock_pick`("새 비기를 고른다") |
| 노드 상태 | `str.art_trie.state.unlocked`/`.locked`/`.candidate`/`.current` |
| 비기 이름 | `art.<id>` 15개 |
| 정산 | `str.settlement.take.new_art` |
| 연습장 | `str.dummy_practice.title`, `.target_command`, `.view_trie`, `.fail.late_input`, `.fail.late_k`, `.fail.no_crystal`, `.option.infinite_crystal`, `.option.heat_lock`, `.option.enemy_fire`, `.damage_per_crystal`, `.dps`, `.recent_damage`, `.heat_ratio` (`.option.duel_delay`는 솔로 제외) |
| HUD | `str.battle_hud.now_k`, `str.battle_hud.now_k_debt` |
| 비기 탭 간격 표시 | 기본 0.3초와 **다른 간격만** 적는다("0.2초", "0.25초", "0.18초") (spec 13 8.4) |

---

## 12. 솔로 제외 (한 줄씩)

- 결투 전체(S03 16절: 준비 2틱, 상태 효과 3틱 지연, 조준 보정 10도, 150개 상한, 결투 값 `duel` 칸, 입력 지연·마감 여유, 발동 취소, 매크로 탐지) — 솔로 제외.
- 서버 해금 반영(`unlocked_arts`, 판 재실행 검증) — 솔로 제외.
- 입력 기록 파일·패킷 형식(R-13), 상태 해시·스냅숏 — 솔로 제외(결정론 불필요).

---

## 13. 표시 이벤트 자리 (S03 21절, 그림·소리는 만들지 않는다)

`cmd.dir_input(dir,node,chained)`, `cmd.chain_broken(reason)`, `cmd.reset(reason: stance_enter/stance_exit/flip/pause/resolved/timeout)`, `art.preview_changed(lines)`, `art.cast(art,difficulty,element,via_fallback,debt_used,is_just,repeats)`, `art.cast_debt`, `art.fizzle_no_crystal`, `art.fizzle_late(late_index,late_by)`, `art.cooldown_start/end`, `art.k_buffered`, `art.<id>.*` 효과별(예 `art.recoil_shot.charge_broken`, `art.full_bloom.wave(index)`, `art.ice_screen.shatter`), `unlock.turn_offered(wave_index,candidates)`, `unlock.picked(art)`, `practice.gap_report`, `practice.just_gauge`, `practice.fail_reason`. 연출 세기: 난이도 1~2 화면 효과 없음, 3 흔들림 소, 5 정지+흔들림 중·대+큰 띠.

---

## 14. 테스트 벡터 (구현 검증용, S03 18절에서 그대로)

하네스: 시작 시 자세 중(Shift 누른 채), 대기 없음, 커서 루트. `X+g s(u)` = 직전 입력에서 g초(u) 뒤 X. F/B/L/R은 상대 방향을 판정기에 바로 넣는다. "전부" = 공용 10 + 고유. 결정/빚은 판정 직후 값. 결정 상한: char.1 5, char.2 5, char.3 4, char.4 6, char.5 7.

### 14.1 A군(입력열) 핵심

| ID | 캐릭터 | 해금 | 결정/빚 | 입력열 | 결과 | 결정/빚 후 |
|---|---|---|---|---|---|---|
| A001 | char.1 | 전부 | 5/0 | F, F+0.15(2304), F+0.15(2304), K+0.15(2304) | Fire(spear) | 3/0 |
| A006 | char.3 | 전부 | 4/0 | F, F+2304, L+2304, K+2304 | Fire(copy_circle) | 2/0 |
| A008 | char.1 | 전부 | 5/0 | B, B+2304, F+2304, K+2304 | Fire(vent) | 3/0 |
| A009 | char.5 | 전부 | 7/0 | L, R+2304, R+2304, K+2304 | Fire(afterimage) | 5/0 |
| A011 | char.1 | 전부 | 5/0 | F, B+2304, B+2304, K+2304 | Fire(kiln) | 3/0 |
| A012 | char.2 | 전부 | 5/0 | F, B+2304, B+2304, K+2304 | Fire(recoil) | 4/0 |
| A016 | char.1 | 전부 | 1/0 | B, B+2304, K+2304 | Fire(recoil, via_fallback) | 0/0 |
| A017 | char.5 | 전부 | 7/0 | L, R+0.19(2918), K+2304 | Fire(shadow_hand) | 5/0 |
| A018 | char.5 | 전부 | 7/0 | L, R+0.22(3379), K+2304 | Fire(wings) | 6/0 |
| A019 | char.2 | 전부 | 5/0 | L, F+0.22(3379), K+2304 | Fire(fan) | 4/0 |
| A020 | char.5 | 전부 | 0/1 | L, R+2304, K+2304 | Fire(wings, via_fallback) | 0/2 |
| A021 | char.1 | 전부 | 5/0 | F, B+0.19(2918), K+2304 | Fire(octastar) | 3/0 |
| A022 | char.1 | 전부 | 5/0 | F, B+0.22(3379), K+2304 | Fire(recoil) | 4/0 |
| A032 | char.1 | 전부 | 5/0 | F, B+0.17(2611), B+2611, F+2611, K+2611 | Fire(bloom), repeats 5 | 0/0 |
| A037 | char.1 | 전부 | 5/0 | F, B+0.19(2918), B+2611, F+2611, K+2611 | Fizzle(FullBloomLate, idx 2, 153u) "2번째 입력이 0.01초 늦음" | 5/0 |
| A042 | char.1 | 전부 | 5/0 | F, B+2611, B+2611, F+2611, K+0.25(3840) | Fizzle(FullBloomLate, idx 5, 1075u) "5번째 입력이 0.07초 늦음" | 5/0 |
| A047 | char.1 | {burst,recoil,vent,fan,octastar}+고유 | 5/0 | 만개 입력(모두 2611) | Fire(vent) | 3/0 |
| A052 | char.1 | {burst,recoil,fan}+고유 | 5/0 | 만개 입력(모두 2611) | Fire(fan) | 4/0 |
| A053 | char.1 | 전부 | 2/0 | 만개 입력(모두 2611) | Fizzle(NoCrystal) | 2/0 |
| A054 | char.1 | 전부 | 5/0 | F, F+2304, K+0.06(922) | Fire(thunder_spear) | 4/0 |
| A055 | char.1 | 전부 | 5/0 | F, F+2304, K+0.09(1382) | Fire(spear) | 3/0 |
| A057 | char.1 | 전부 | 5/0 | B, B+0.2(3072), K+2304 | Fire(kiln) | 3/0 |
| A058 | char.1 | 전부 | 0/0 | B, B+2304, K+2304 | Fizzle(NoCrystal) | 0/0 |
| A062 | char.2 | 전부 | 1/0 | L, F+2304, K+2304 | Fire(wings, via_fallback) | 0/0 |
| A063 | char.3 | 전부 | 1/0 | F, L+2304, K+2304 | Fire(fan, via_fallback) | 0/0 |
| A068 | char.4 | 전부 | 6/0 | B, L+0.29(4454), K+2304 | Fire(ice_screen) | 4/0 |
| A069 | char.4 | 전부 | 6/0 | B, L+0.31(4762), K+2304 | Fire(wings) | 5/0 |
| A072 | char.5 | 전부 | 1/0 | L, R+2304, K+2304 | Fire(shadow_hand) 외상 | 0/1 |
| A073 | char.5 | 전부 | 0/0 | L, R+2304, K+2304 | Fire(shadow_hand) 외상 | 0/2 |
| A074 | char.5 | 전부 | 0/2 | L, R+2304, K+2304 | Fizzle(NoCrystal) | 0/2 |
| A075 | char.5 | 전부 | 1/1 | F, F+2304, K+2304 | Fire(spear) | 0/2 |
| A076 | char.5 | 전부 | 0/1 | F, F+2304, K+2304 | Fizzle(NoCrystal) | 0/1 |
| A077 | char.5 | 전부 | 0/0 | K | Fire(burst) | 0/1 |
| A080 | char.5 | 전부 | 2/0 | 만개 입력(모두 2611) | Fizzle(NoCrystal)(외상 불가) | 2/0 |
| A081 | char.1 | 전부 | 5/0 | L, R+2304, K+2304 | Fire(wings)(LR 노드 없음) | 4/0 |
| A082 | char.3 | 전부 | 4/0 | L, L+0.24(3686), K+2304 | Fire(afterimage) | 2/0 |
| A083 | char.3 | 전부 | 4/0 | L, L+0.26(3994), K+2304 | Fire(wings) | 3/0 |
| A088 | char.1 | 전부 | 5/0 | F, K+0.3(4608) | Fire(fan) | 4/0 |
| A089 | char.1 | 전부 | 5/0 | F, K+0.31(4762) | Fire(burst) | 4/0 |
| A091 | char.1 | 전부 | 0/0 | K | Fizzle(NoCrystal) | 0/0 |
| A092 | char.1 | 전부 | 1/0 | F, F+2304, K+2304 | Fizzle(NoCrystal)(공용은 부모 대체 없음) | 1/0 |
| A093 | char.1 | 전부 | 1/0 | F, F+2304, K+0.05(768) | Fire(thunder_spear) | 0/0 |
| A095 | char.1 | 전부 | 5/0 | F, F+2304, K+0(0) | Fire(thunder_spear) | 4/0 |
| A096 | char.1 | 전부 | 5/0 | F, F+2304, K+0.07(1075) | Fire(thunder_spear) | 4/0 |
| A097 | char.1 | 전부 | 5/0 | F, F+2304, K+1080 | Fire(spear) | 3/0 |
| A098 | char.1 | {spear,burst,recoil,fan}+고유 | 5/0 | F, F+2304, K+768 | Fire(spear) | 3/0 |
| A100 | char.1 | 전부 | 5/0 | F, B+2611, B+0.19(2918), F+2611, K+2611 | Fizzle(FullBloomLate, idx 3, 153u) | 5/0 |
| A101 | char.1 | 전부 | 5/0 | F, B+2611, B+2611, F+0.2(3072), K+2611 | Fizzle(FullBloomLate, idx 4, 307u) "0.02초" | 5/0 |
| A102 | char.1 | 전부 | 5/0 | F, B+2765, B+2765, F+2765, K+2765 | Fire(bloom) | 0/0 |
| A103 | char.1 | 전부 | 5/0 | F, B+2611, B+2611, F+2611, F+2611, K+2611 | Fire(spear) | 3/0 |
| A106 | char.3 | {burst,recoil}+고유 | 4/0 | F, F+2304, K+2304 | Fire(burst) | 3/0 |
| A107 | char.3 | {burst,recoil,fan}+고유 | 4/0 | F, F+2304, K+2304 | Fire(fan) | 3/0 |
| A110 | char.4 | 전부 | 6/0 | F, B+2304, L+2304, K+2304 | Fire(ice_screen) | 4/0 |
| A112 | char.2 | 전부 | 5/0 | L, L+2304, F+2304, K+2304 | Fire(bolt_dash) | 3/0 |
| A114 | char.1 | 전부 | 5/0 | F, B+2304, B+2304, B+2304, K+2304 | Fire(kiln) | 3/0 |
| A116 | char.4 | 전부 | 6/0 | F, B+0.25(3840), L+2304, K+2304 | Fire(ice_screen) | 4/0 |
| A117 | char.1 | 전부 | 5/0 | L,R,L,R,L,R,L,R,F,F (각 +2304), K+2304 | Fire(spear) | 3/0 |

### 14.2 B군(규칙) 핵심

| ID | given | when | then |
|---|---|---|---|
| B01 | char.1, 전부, 결정 5, 커서 F(t0) | t0+2304에 같은 시각 F와 K(K가 먼저 기록돼도) | F→K 순 처리, 커서 FF, 간격 0 → Fire(thunder_spear), 결정 4 |
| B01b | char.1, 전부, 5, 커서 루트 | 같은 시각 F와 K | Fire(fan), 결정 4 |
| B02 | char.1, 자세 밖에서 W(시야 앞) 누른 채 | Shift 누름 → 0.1초 뒤 K | 누른 채 W는 입력 아님 → Fire(burst) |
| B03 | char.1, 자세 중 | F, F+0.15, Shift 뗌(+0.05), Shift 누름(+0.05), K(+0.05) | Fire(burst) |
| B04 | char.1, 자세 중 | F, F+0.15, Shift 뗌(+0.05), K(+0.05) | Fire(burst)(자세 밖 K) |
| B05 | char.1, 자세 밖 | 방향 F → Shift(+0.05) → K(+0.05) | Fire(burst) |
| B06 | char.1, 자세 중(Shift) | F, Space 누름(+0.1, 반전), F'(+0.1, 뒤집힌 시야 기준 앞), K(+0.1) | 초기화 → 커서 F → Fire(fan) |
| B07 | char.1, 자세 중 | F, F+0.15, 일시정지·해제, K | Fire(burst) |
| B08 | char.1, 결정 5, 커서 F(t0) | 미리보기 t0+4608 / t0+4609 | "부채 전개(1)" / "마력 방출(1)", 지갑 변화 없음 |
| B09 | char.1, 결정 5, 자세 밖 | K(t0), K(t0+3000), K(t0+4700) | 1번째 Fire, 대기 끝 t0+6144. 2번째 무시. 3번째 보관(t0+4700으로 판정), 발동 틱 ceil((t0+6144)/256). 결정 5→4→3 |
| B09b | 같음 | K(t0), K(t0+4608) | 보관됨(창 시작과 같음) |
| B09c | 같음 | K(t0), K(t0+4700), K(t0+5000) | 3번째 무시(보관 중) |
| B10 | char.1, 결정 0, 자세 밖 | K(t0) 불발 → 결정 +1 → K(t0+100) | 불발은 대기 없음 → 2번째 Fire(burst) |
| B12 | char.3, 해금 {①②+copy_circle}, 결정 1 | F, L+0.15, K+0.15 | 부모 fan 미해금 → Fizzle(NoCrystal), 결정 1 [임시 C-02] |
| B13 | char.5, 전부, 결정 1·빚 0, 커서 LR | 미리보기 | "그림자 손(2) 외상 1" |
| B14 | char.1, 전부, 결정 1, 커서 BB | 미리보기 | "반동 사격(1)" |
| B15 | char.1, 전부, 결정 5, 커서 FF | 미리보기 | 두 줄: 섬 뇌광창(1) / K 꿰뚫는 창(2) |
| B15b | char.1, {①②③⑥+kiln}, 커서 FF | 미리보기 | 한 줄 "꿰뚫는 창(2)" |
| B16 | — | LateBy 153/307/1075/1 u | "0.01초"/"0.02초"/"0.07초"/"0.01초" |
| B17 | 새 오프라인 프로필 char.3 | 판 시작 | 해금 = burst, recoil, copy_circle만 |
| B18 | B17, 시작 웨이브 1 | 웨이브 1 전멸 → 야영 | 차례 1개, 후보 2개 ⊆ {fan, wings, vent} |
| B19 | char.1, {①②③⑥+kiln} | 웨이브 3 차례 / 4 차례 | thunder_spear 후보 아님 / 후보 집합에 있음 |
| B20 | char.1, {①②③⑧+kiln} | 웨이브 5 / 6 차례 | bloom 후보 아님 / 후보 |
| B21 | char.1, 공용 9종(⑩만 없음) | 웨이브 6 차례 | 후보 1개(bloom)만 |
| B21b | 같음 | 웨이브 1 차례 | 적격 0 → 보여 주지 않고 소모 |
| B22 | 새 프로필 char.2, 시작 웨이브 11 | 출발 야영 | 차례 6개(1~6번)를 차례대로. 4번부터 뇌광창 적격(앞에서 창 골랐다면), 6번 만개 적격(팔방 있으면) |
| B24 | char.4, 공용 8종 모두 해금 | 웨이브 1~6 전멸 | 차례 0개 |
| B25 | char.1, 1판에서 ③④⑤⑥⑦⑧ | 2판 웨이브 1~6 | 1~3번 0개, 4번 ⑨ 1개, 5번 0개, 6번 ⑩ 1개 |
| B26 | char.1 해금 {③}, char.2 새 상태 | char.2로 판 시작 | char.2는 fan 미해금(캐릭터별) |
| B30 | char.5, 결정 5 | L, R+0.15, K+0.15 | Fire(shadow_hand), CrystalsAfter 3 → 그림자 계약 +9% |
| B31 | char.5, 결정 7 | 만개 입력 | Fire(bloom), repeats 7, +0% |
| B32 | char.3, 결정 4 | 만개 입력 | repeats 4 |
| B33 | char.1, 과열 잠금 중 | 자세 밖 K | Fire(burst), 잠금 유지 |
| B34 | char.1, 과열 잠금 중 | B, F+0.15, K+0.15 | Fire(vent) 4T, 잠금 해제, 열 0 |
| B35 | char.1, 열 50 | B, F+0.15, K+0.15 | Fire(vent) 2T |
| B37 | char.1, 커서 FBB, FB 간격 0.19초 | 미리보기 | "불발(2번째 입력이 0.01초 늦음)" |
| B38 | char.1, 결정 5 | F, F+0.15, K+0.09 | Fire(spear), 연습장 "K가 0.02초 늦음"(1382−1075=307u) |

### 14.3 S군(트라이 구조)
S01 char.1 13노드, FBB.Fail = BB / S02 char.2 14노드, LF/RF Fail F, Gap 3072, FBB.Fail = B / S03 char.3 FL/FR Fail L/R, 3072 / S04 char.4 BL/BR Fail L/R, **4608** / S05 char.5 LR/RL Fail R/L, 3072 / S06~S10 Next 표가 S03 5.6과 칸마다 같음 / S11 다른 캐릭터 고유 노드 없음 / S12 FF만 JustArt, FBBF만 IsFullBloom, 고유 노드만 UniqueFallback / S13 모든 Next는 깊이 ≥ 1.

---

## 15. 미정·모순

| # | 내용 | 이 문서의 처리 |
|---|---|---|
| M-1 | 비기 id가 세 벌(S03 `art.mana_release` 등 / S04 1.3 `art.mana_burst`·`art.thunder_dash`·`art.transcribe`·`art.heat_vent`·`art.eight_way` / spec 14·arts.json·names `art.burst` 등). 지팡이 특성 id도 S04(`trait.credit` 등)와 data/characters.json(`wand_trait.on_credit` 등)이 다름 | CLAUDE.md 5절에 따라 spec 14 = arts.json = names 쪽을 정본으로(0.1절 표) |
| M-2 | 거리 단위: S03은 Q32.32(1칸 = 2^32), S04는 1칸 = 6,000u | 솔로는 float 칸(1칸 = 64px)으로. 수치는 칸 그대로 |
| M-3 | 고유 비기 부모 대체 때 부모 미해금: 검산 스크립트(PY)는 부모를 냄 / GDD는 "미해금 = 기술 없음" | [임시] 불발(B12). 개발자 확인 대상(U-01) |
| M-4 | 불발 문구 자릿수: PY 셋째 자리 / 문서 둘째 자리 | 둘째 자리 올림 |
| M-5 | 준비 시간(FX)이 게임 시간인가 | [임시] 넣는다: spear 5틱, vent 4틱, bloom 15틱 |
| M-6 | 만개 반복 간격 0.12초 = 7.2틱 | [임시] round(i×7.2): 0,7,14,22,29,36,43틱. 솔로 float면 0.12초 간격 그대로 써도 됨 |
| M-7 | 시작 웨이브: S03 B23은 "시작 웨이브 4" 예를 들지만 spec 07은 건너뛰기를 10의 배수(`start_skip ∈ {10,20,…}`, 시작 11·21…)만 허용 | spec 07 쪽을 따른다. B23은 일반식 `min(startWave−1, 6)` 검사로만 쓰고 실제 선택지는 11, 21… |
| M-8 | 미리보기 "불발" 표시, 섬 두 줄의 머리 글자("섬:"/"K:") | **names.ko.json에 열쇠 없음 → 미정**. 열쇠를 더해야 함(예 `str.battle_hud.now_k_fizzle`, `str.battle_hud.now_k_just`) — 이름 표 담당 확인 필요 |
| M-9 | 보관된 K 발동 뒤 새 대기 시작 시각(원문: "발동이면 새 대기 시작"만) | 참고 구현처럼 실제 발동 시각 + 0.4초 |
| M-10 | 다시 깐 병풍의 이전 벽 파편 | [임시] 파편 없이 제거 |
| M-11 | 불가마 지대 감속이 나간 뒤 남는가, 지대 피해 틱 시점 | [임시] 즉시 해제 / 시전 뒤 30·60·…·240틱 |
| M-12 | 그림자 손 중심이 벽 밖일 때, 당김이 순간인가 | [임시] 벽 안쪽으로 자름, 순간 이동 |
| M-13 | 벼락 질주 이동 시간 | S03 [임시]는 판정 틱 즉시, S04 의사코드는 12틱 동안 이동(Q-10 [임시]) — 둘 다 임시. 무적 12틱과 맞춰 12틱 이동을 권장, 확정은 미정 |
| M-14 | 베껴 쓰기 사본 탄의 임계 배율 | [임시] 받지 않음 |
| M-15 | 해금 후보 뽑기 난수 | [임시] 판 씨앗에서 갈라 낸 균등 무작위 2개(솔로는 판 시작 씨앗의 System.Random으로 충분) |
| M-16 | 야영에서 할 일 목록(DECISIONS "조립, 회복, 비기 선택만" vs 허수아비 연습) | spec 13 C-12: 연습도 둔다(상태를 바꾸지 않음). 강화는 절대 없음 |
| M-17 | 결정 0·빚 0 그믐 피격 | [임시] 빚 안 생김 |
| M-18 | T의 정확한 계산식 | 주문 컴파일러 영역(다른 요약서). 이 영역은 "T × 비기 배율"로만 부른다 |

### 참고 구현 위치(실제 코드, 결정론 버전 — 알고리즘만 참고)
- 판정기: `D:\algorithm_game\src\Wizard.Sim\Arts\Parser\CommandParser.cs` (상태 기계 :64~172, 미리보기 :177, 판정 :202, 결제 :266·:314, 간격 :351·:372)
- 트라이: `D:\algorithm_game\src\Wizard.Sim\Arts\Parser\CommandTrie.cs` (Build :71, 실패 링크 :236, DFA :261, EdgeGap :294)
- 입력 순서: `D:\algorithm_game\src\Wizard.Sim\Input\Types\InputOrder.cs:8`
- 입력 → 판정기 연결: `D:\algorithm_game\src\Wizard.Sim\Arts\Parser\CommandInputRouter.cs:202`
- 늦음 올림: `D:\algorithm_game\src\Wizard.Sim\Arts\Parser\LateBy.cs:238`
- 해금 차례·후보 뽑기·비기 효과 실행은 src에 구현 없음(이름·이벤트 자리만, `src/Wizard.Sim/Events/CampView.cs:11`).
