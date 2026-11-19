# 03 — 주문 조립과 투사체 (솔로 구현 요약서)

대상: 순정 Unity(MonoBehaviour, float 허용). 결정론·Fix64·서버·넷코드 없음.
정본 출처: `docs/spec/02-spell-compiler.md`(이하 S02) 전부, `data/sim/blocks.json`, `data/sim/elements.json`, `docs/effects.md`(E) 13·10·0.1절, `docs/spec/13-ui-flows.md`(S13) 8절, `docs/ui-assets.md`(U) 4.11, `docs/spec/04`(S04), `docs/spec/08`(S08), `docs/spec/01`(S01) 7.7, `docs/wands/*.md`.
괄호 안은 출처 절. [임시] = 명세가 정하지 않아 둔 기본값(그대로 쓴다). **미정** = 값 없음/모순.

---

## 0. 단위 (S02 2.1, 2.4)

| 항목 | 값 | Unity 권장 |
|---|---|---|
| 거리 | 칸. 1칸 = 64px | **1 Unity unit = 1칸** |
| 속도 | 칸/초 | `speed * Time.fixedDeltaTime` |
| 시간 | 1초 = 60틱 | `FixedUpdate` 60Hz 권장(틱 수치 그대로 쓰려면). 초로 써도 됨 |
| 각도 | 원문은 도. 0도 = +x(오른쪽), 반시계 + | 도 단위 float |
| 전장 | 24×14칸, 사방 벽(튕김 대상) | — |
| 열 | 게이지 0~100. 소수 생김(4×2.2=8.8) | float |

틱 ↔ 초 표 (S02 2.4): 9틱=0.15s, 18=0.3s, 21=0.35s, 27=0.45s, 30=0.5s, 36=0.6s, 60=1s, 180=3s, 360=6s.

솔로 제외: Fx(Q32.32) Raw 값, sin 표, 결정론 해시(S02 2.2, 2.3, 15절), 결투 변환(S02 13.1), 결투 조준 보정, 결투 화면 상한 150/인.

---

## 1. 속성 (S02 3절, `data/sim/elements.json`)

### 1.1 속성 목록 (정본 id는 spec 14 3절)

| 번호 | id | 이름 열쇠 | 주색 / 보조색 | 탄 모양 | 캐릭터 |
|---|---|---|---|---|---|
| 0 | `element.neutral` | `element.neutral` (무) | 회청 `#8E9AAE`(문양) | — (블록에만) | — |
| 1 | `element.fire` | `element.fire` | `#FF8A1F` / `#FFE08A` | 둥글고 꼬리 일렁임 | char.1 |
| 2 | `element.lightning` | `element.lightning` | `#FFF36B` / `#FFFFFF` | 꺾인 선 | char.2 |
| 3 | `element.arcane` | `element.arcane` | `#4FE3C1` / `#D6FFF3` | 네모·마름모 | char.3 |
| 4 | `element.ice` | `element.ice` (냉기) | `#9FDCFF` / `#F4FBFF` | 뾰족한 육각 조각 | char.4 |
| 5 | `element.dark` | `element.dark` | `#8A5CFF` / `#D9C7FF` | 세 겹 고리(밝은 테+어두운 고리+밝은 심) | char.5 |

### 1.2 피해 속성 규칙 (S02 3.2)

| 규칙 | 내용 |
|---|---|
| EL-1/2 | 피해 속성 = **맞힌 투사체 블록의 속성**. 변형·제어 블록 속성은 피해 속성을 바꾸지 않음(서리·잠식 포함). 변형·제어의 속성은 제물(강화) 보너스에만 쓰인다 |
| EL-3 | 캐릭터 속성과 다른 블록을 끼우면 그 블록 속성으로 들어간다(달구가 마력탄 → 비전) |
| EL-4/8 | 블록을 거치지 않는 피해(패시브·고유 비기 자체 피해)는 캐릭터 속성 |
| EL-5 | 꿰뚫는 창·방열 폭발 = 코어 블록 속성 |
| EL-6 | 적의 비블록 피해(유폭, 돌진, 파편)는 무속성 |
| EL-7 | 화염구 폭발, 연쇄 번개 전이 = 그 블록 속성 |

### 1.3 약점·저항 (S02 3.3, elements.json)

`elements.json` 필드:

| 필드 | 값 | 뜻 |
|---|---|---|
| `weak_multiplier` | 1.25 | 대상의 약점 속성 피해 배율 |
| `resist_multiplier` | 0.8 | 대상의 저항 속성 피해 배율 |

- 그 밖 속성 ×1. `element.neutral` ×1.
- 적마다 약점 1·저항 1 (값은 `data/sim/enemies.json`·`bosses.json`의 `weak`/`resist` 필드 — 적 요약서 담당). 엘리트는 바탕 종 그대로, 보스의 졸개(새싹, 허상이 부른 슬라임)는 상성 없음(`enemy.spore_seedling`은 neutral/neutral).
- 참고표(데이터와 S02 3.3 일치 확인): slime_moss 약 fire/저 ice, wolf_bramble lightning/dark, golem_menhir arcane/lightning, shaman_cap ice/dark, bug_lantern ice/fire, hog_needle dark/ice, weasel_pot lightning/fire, bat_dusk fire/dark, wisp_stray arcane/lightning, vine_thorn fire/arcane, boar_tusk ice/arcane, toad_drum dark/ice; boss.kiln_boar ice/fire, boss.mirror_phantom dark/arcane, boss.spore_queen fire/lightning, boss.storm_owl arcane/lightning, boss.frost_goat lightning/ice, boss.night_serpent lightning/dark.

---

## 2. 블록 16종 (S02 4절, `data/sim/blocks.json`)

### 2.1 `blocks.json` 필드 설명 (경로 `D:\algorithm_game\data\sim\blocks.json`, 배열 `blocks`)

| 필드 | 단위 / 값 | 뜻 |
|---|---|---|
| `id` | `block.<key>` | 정본 id. 이름 표 열쇠와 같다 |
| `kind` | `projectile` / `control` / `modifier` | 투사체·제어·변형 |
| `rarity` | `common` / `rare` / `hero` | 일반·희귀·영웅. 드랍 확률과 제물 점수에만 쓰임. 이름 `str.block.grade.*` |
| `element` | `element.*` | 블록 속성 |
| `heat` | 열(게이지 단위) | 투사체·변형이 발사 1회에 더하는 열. 제어 0 |
| `heat_multiplier` | 배수 | 분열만 1.6/2.2. 그 밖 1 |
| `split_count` | 개 | 분열만 2/3. 그 밖 1 |
| `base_damage` | 피해 | 투사체만 |
| `interval_seconds` | 초 | 투사체만. **코어에 끼웠을 때의 발사 간격** |
| `speed` | 칸/초 | 투사체 이동 속도. 즉발 0 |
| `instant` | bool | 즉발(연쇄 번개만) |
| `range` | 칸 | 사거리(이동 거리 누적). 부메랑은 편도 |
| `hit_radius` | 칸 | 판정 반지름. 연쇄 번개는 광선 반폭 |
| `damage_bonus` | 비율 | 변형만: 피해 보너스(합산) |
| `flags` | 배열 | `explodes`, `chains`, `ignores_wolf_dodge`, `returns`, `pass_through` |
| `effect` | 문자열 | 변형 효과: `bounce`, `size`, `pierce`, `frost`, `homing`, `triple_heat`, `corrosion`, 그 밖 `none` |
| `params.explosion_radius` | 칸 | 화염구 폭발 반경 2 |
| `params.chain_hops` / `chain_radius` / `chain_hop_multiplier` | 회 / 칸 / 배수 | 연쇄 번개 3회, 3칸, 전이마다 ×0.7 |
| `params.return_arrive_radius` / `return_max_distance` | 칸 | 부메랑: 쏜 사람 0.5칸 안이면 소멸, 돌아오는 길 9칸 넘으면 소멸 [임시 U12] |
| `params.slow_percent` / `slow_seconds` | % / 초 | 감속(얼음 송곳 30/1, 서리 20/1) |
| `params.fan_half_angle_degrees` | 도 | 분열 부채 반각(2갈래 10 → ±10, 3갈래 15 → −15/0/+15) |
| `params.bounce_count` | 회 | 벽 튕김 블록 1개당 2회 [임시 U6] |
| `params.radius_multiplier` / `radius_multiplier_cap` | 배 | 크기 2배, 겹치면 곱, 상한 ×4 [임시 U8] |
| `params.pierce_count` | 체 | 관통 블록 1개당 +2체 [U7] |
| `params.homing_turn_degrees_per_second` / `homing_cone_half_degrees` / `homing_range` | 도/초, 도, 칸 | 유도 180°/s(=3°/틱), 앞 ±90°, 6칸 [임시 U9] |
| `params.crit_multiplier` | 배 | 과열 3배: 임계 배율 2 → 3 |
| `params.damage_taken_bonus_percent` / `damage_taken_bonus_seconds` | % / 초 | 잠식 +15% / 3초 |

### 2.2 블록 표 (blocks.json 값 = S02 4.1)

| # | id (이름 열쇠 = id) | 이름 | 종류 | 등급 | 속성 | 열 | 피해 | 간격 | 속도/사거리(칸) | 반지름 | 동작 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | `block.magic_bolt` | 마력탄 | 투사체 | 일반 | 비전 | 4 | 10 | 0.15s(9t) | 14 / 10 | 0.12 | 직선, 첫 적에 소멸. **시작 블록** |
| 2 | `block.fireball` | 거대 화염구 | 투사체 | 희귀 | 화염 | 18 | 40 | 0.6s(36t) | 9 / 8 | 0.18 | 반경 2 폭발 |
| 3 | `block.chain_lightning` | 연쇄 번개 | 투사체 | 영웅 | 번개 | 12 | 18 | 0.45s(27t) | 즉발 / 7 | 광선 반폭 0.12 | 3회 전이(반경 3, ×0.7씩), 늑대 회피 무시 |
| 4 | `block.boomerang` | 부메랑 | 투사체 | 희귀 | 비전 | 10 | 15 | 0.35s(21t) | 10 / 편도 6 | 0.15 | 왕복, 갈 때·올 때 각 1회 타격 |
| 5 | `block.ice_spike` | 얼음 송곳 | 투사체 | 희귀 | 냉기 | 8 | 14 | 0.3s(18t) | 12 / 9 | 0.12 | 맞은 적 1초 이동 −30% |
| 6 | `block.shadow_orb` | 그림자 구체 | 투사체 | 희귀 | 암흑 | 14 | 28 | 0.5s(30t) | 6 / 8 | 0.25 | 적을 지나가며 적마다 1회, 벽에서 소멸 |
| 7 | `block.split_two` | 2갈래 분열 | 제어 | 일반 | 무 | ×1.6 | | | | | 부채 ±10° |
| 8 | `block.split_three` | 3갈래 분열 | 제어 | 희귀 | 무 | ×2.2 | | | | | 부채 −15/0/+15° |
| 9 | `block.on_hit_trigger` | 적중 시 발동 | 제어 | 희귀 | 무 | 0 | | | | | 새 스코프 |
| 10 | `block.wall_bounce` | 벽 튕김 | 변형 | 일반 | 무 | +2 | | | | | 튕김 2회/블록 |
| 11 | `block.double_size` | 탄환 크기 2배 | 변형 | 일반 | 화염 | +3 | +20% | | | ×2 | |
| 12 | `block.pierce` | 관통 +2 | 변형 | 일반 | 비전 | +3 | +10% | | | | +2체 관통/블록 |
| 13 | `block.frost` | 서리 | 변형 | 일반 | 냉기 | +3 | | | | | 맞은 적 1초 −20% |
| 14 | `block.homing` | 유도 | 변형 | 영웅 | 번개 | +5 | | | | | 선회각 제한 유도 |
| 15 | `block.triple_heat` | 과열 3배 | 변형 | 영웅 | 화염 | +2 | | | | | 임계 배율 2→3 |
| 16 | `block.corrosion` | 잠식 | 변형 | 영웅 | 암흑 | +5 | | | | | 맞은 적 3초 받는 피해 +15% |

- 등급별: 일반 6(1,7,10,11,12,13), 희귀 6(2,4,5,6,8,9), 영웅 4(3,14,15,16) (S02 4.1, TV-A02).
- 다섯 속성 모두 자기 투사체가 있다(화염 2, 번개 3, 비전 1·4, 냉기 5, 암흑 6).
- 아이콘 바탕 판: 투사체 둥근 판 / 제어 육각 판 / 변형 깎은 네모 판 (S02 4.1, U 3.1). 이름 `str.block.kind.projectile|control|modifier`.
- 블록 설명 판 이름 열쇠: `str.block.heat`(열), `str.block.damage`(피해), `str.block.interval`(간격), `str.block.borrowed`(빌림).
- 사거리 끝까지 대략 시간: 마력탄 0.714s(≈43틱째 소멸, TV-G01), 화염구 0.889s, 부메랑 편도 0.6s, 얼음 송곳 0.75s, 그림자 구체 1.333s.

---

## 3. 지팡이 슬롯 (S02 5절, S04, S08, wands)

| 규칙 | 내용 |
|---|---|
| SL-1 | 코어 1칸 + 연결 4칸. **char.3(갈피)만 연결 5칸** (`characters.json`의 `base_link_slots`) |
| SL-2 | 확장 칸은 지팡이 하나당 평생 최대 3칸(강화 대성공, 슬롯 새기기). 지팡이 데이터 `slots_extra` 0~3 |
| SL-3 | 확장 칸은 연결 칸 **뒤에** 붙는다. 연결 칸 총수 L = base + extra (4~8). UI는 첫 줄 코어+연결 4(5), 둘째 줄 확장 3칸 |
| SL-4 | 코어에는 투사체만 |
| SL-5 | 연결 칸에는 아무 블록이나. 빈 칸은 건너뜀 |
| SL-6 | 코어가 비면 원정 출발 불가(`warn.core_empty`). 상점에서 산 +0 지팡이는 빈 조립 |
| SL-7 | 새 프로필 시작 지팡이: 캐릭터마다 +0 한 자루, **출발 조립 = 코어 마력탄 1개, 연결 칸 비움** (S08 8.4: 서고 = 마력탄 5개, 전부 끼움) |
| SL-8 | 출발 조립은 지팡이마다 저장. 판 중에 바꾼 조립은 판이 끝나면 출발 조립으로 되돌아감. 판 중 조립 변경은 야영에서만 |
| SL-9 | 판 중 지팡이 교체 없음 |

**캐릭터별 시작 블록:** 다섯 캐릭터 모두 같다 — 코어 `block.magic_bolt`, 연결 칸 전부 빈 칸. 캐릭터별 다른 시작 블록은 없다 (S08 8.4, S13 2.1, S04 13).
- 웨이브 1~4의 적은 비전에 저항하지 않는다(시작 블록이 비전이라서, AF-5).

지팡이별 주문 관련 특성(S02 5.2, S04 7):

| 캐릭터 | 주문에 닿는 특성 |
|---|---|
| char.1 | 임계 시작 80 (`crit_start`) — 과열 요약서 |
| char.2 | 변이 상한 3 (`max_mutations`) |
| char.3 | 연결 5칸, 깊이 감쇠 **0.92**(다른 캐릭터 0.85), 패시브 "밑줄"(맨 끝 연결 칸 열 깎기) |
| char.4, char.5 | 주문 계산에 닿는 것 없음(char.5 캐릭터 배율은 11절) |

깊이 감쇠 0.85/0.92와 밑줄 상한 5는 데이터 JSON에 없다 → 코드 상수(또는 솔로 쪽 설정 파일)로 둔다.

---

## 4. 컴파일러 (슬롯 배열 → 실행 트리) (S02 6절)

### 4.1 스코프 규칙

| 블록 | 규칙 |
|---|---|
| 코어 | 루트 스코프 S0(깊이 0)의 주인 |
| 변형 | **현재 스코프의 모든 투사체에 붙는다(누적)**. 슬롯 순서 무관 — 투사체보다 앞 칸이어도 붙음(CP-1) |
| 분열 | 현재 스코프의 투사체를 N개 부채꼴로 복제. **한 스코프에 첫 번째만 유효**, 두 번째부터 `W_SPLIT_DUPLICATE`로 무시 |
| 적중 시 발동 | 새 하위 스코프를 열고, **뒤의 블록은 모두 그 안**(스코프는 닫히지 않음 → 항상 S0→S1→S2→S3 한 줄 사슬, CP-2) |
| 연결 칸 투사체 | 같은 스코프에서 동시 발사. 트리거 뒤 첫 투사체 = 그 스코프의 주인(경고·미리보기 굵은 테 용도일 뿐, CP-7) |

- CP-3 [임시 U3]: 부모 스코프의 변형은 하위 스코프로 이어지지 **않는다**.
- CP-4: 분열은 자기 스코프 투사체만.
- CP-6: 트리거는 부모 스코프의 **투사체 하나하나**에 걸린다(각 탄이 첫 적중에서 하위 스코프 1회).

### 4.2 안전장치 (S02 6.2)

| 번호 | 규칙 |
|---|---|
| SF-1 | 트리거 깊이 최대 3 (S0~S3) |
| SF-2 | 트리 실행 1회당 투사체 최대 **64**. 넘으면 **가장 깊은 스코프부터 통째로** 생략(보전 없음) |
| SF-3 | 깊이마다 피해 ×0.85 (갈피 ×0.92) |
| SF-4 | 화면 전체 플레이어 탄 상한 **300**(사본 마법진 포함), 적 탄 별도 **150**. 넘친 만큼은 생성된 탄 피해 배율로 보전(8절) |
| SF-5 | 비기 1회 투사체 최대 160 (비기 요약서가 예산으로 넘김) |
| SF-6 | 트리거 뒤 투사체 없음 / 효과 없는 조합 → 야영 경고 |
| SF-7 [임시 U5] | 넷째 트리거(깊이 3 스코프 안)는 무시 + 경고, 뒤 블록은 깊이 3에 남음 |

### 4.3 알고리즘 (S02 6.4 의사코드 그대로)

```text
Compile(core, links[L], ctx):
  if links.Length != base+extra: 오류 E_SLOT_COUNT_MISMATCH (개발용)
  if core == None: 오류 E_CORE_EMPTY
  if core.kind != projectile: 오류 E_CORE_NOT_PROJECTILE
  S = [Scope(depth 0, triggerSlot -1)]; cur = S[0]
  cur.AddProjectile(core, slot 0); cur.HeatSum += core.heat
  for slot in 1..L:
    b = links[slot-1]; if empty: continue
    h = b.heat
    if ctx.Underline(갈피) and slot == L and b.kind != control:
        h -= min(5, ceil(h/2))                       # 밑줄
    switch b.kind:
      projectile: cur.AddProjectile(b, slot); cur.HeatSum += h
      modifier:   cur.Mods.Accumulate(b, slot); cur.HeatSum += h
      split:      if cur.SplitCount == 1: cur.SplitCount = b.split_count; cur.HeatMul = b.heat_multiplier; cur.SplitSlot = slot
                  else: diag W_SPLIT_DUPLICATE(slot)
      trigger:    if cur.Depth == 3: diag W_TRIGGER_DEPTH(slot); continue
                  cur = new Scope(depth cur.Depth+1, triggerSlot slot); S.Add(cur)
  for s in S:
    s.Heat = s.HeatSum * s.HeatMul
    s.InstancesPerParent = |s.Projectiles| * s.SplitCount
    s.InstancesTotal = depth0 ? InstancesPerParent : S[d-1].InstancesTotal * InstancesPerParent
    s.DepthMultiplier = decay^depth
    if depth>0 and |Projectiles|==0: diag W_TRIGGER_NO_PROJECTILE(s.TriggerSlot)
    CheckNoEffect(s)
  HeatPerShot = Σ s.Heat
  InstancesTotal = Σ s.InstancesTotal ; Mirrored = 2 × InstancesTotal
  MaxDepthNormal = Prune(S, 1) ; MaxDepthMirrored = Prune(S, 2)
  if MaxDepthNormal < |S|-1: diag W_PROJECTILE_CAP(scope MaxDepthNormal+1)
  diags 정렬: 슬롯 오름차순, 같으면 코드 순
Prune(S, f): d = 가장 깊은 것부터 내려가며 Σ_{k<=d} S[k].InstancesTotal*f <= 64 이면 d 반환
```

- 루트(S0)만으로는 64를 넘지 않는다: 최대 9발 × 3갈래 × 거울상 2 = 54 (6.6).
- 열은 가지치기와 무관하게 모든 스코프를 센다.

### 4.4 스코프 변형 누적 (`ScopeModifiers`, S02 6.3)

| 필드 | 누적 |
|---|---|
| DamageBonusSum | 크기 +0.2, 관통 +0.1 합산 |
| BounceCount | 벽 튕김 수 × 2 |
| PierceCount | 관통 수 × 2 |
| SizeDoublings | 크기 블록 수 → 반지름 ×2^n, 최대 ×4. 폭발 반경은 안 바뀜 |
| Homing, Frost, TripleHeat, Corrosion | bool |

### 4.5 컴파일 결과 (`CompiledSpell`, S02 6.3)

| 값 | 뜻 |
|---|---|
| IsValid | 오류 하나라도 있으면 false(출발 불가) |
| FireIntervalTicks | **코어 블록의 간격** (연결 칸 투사체 간격은 무시) |
| Scopes[0..3] | 각 스코프: Depth, TriggerSlot, OwnerSlot, Projectiles(블록, 슬롯), Mods, SplitCount, SplitSlot, HeatSum, Heat, InstancesPerParent, InstancesTotal, DepthMultiplier |
| HeatPerShot | 발사 1회 열(거울상 +50% 제외 값) |
| InstancesTotal / InstancesTotalMirrored | 가지치기 전 투사체 수 |
| MaxDepthNormal / MaxDepthMirrored | 64 상한 뒤 실행할 가장 깊은 스코프 |
| StandardT | 표준 T (4.7) |
| Diagnostics | 진단 목록 |

### 4.6 진단 (S02 6.3, 6.5) — 화면 문구는 이름 표 열쇠

| 코드 | 심각도 | 이름 열쇠 (문구) | 조건 |
|---|---|---|---|
| E_CORE_EMPTY | 오류 | `warn.core_empty` ("코어에 투사체 블록을 끼워야 출발할 수 있다") | 코어 빔 |
| E_CORE_NOT_PROJECTILE | 오류 | `warn.core_not_projectile` ("코어에는 투사체 블록만 끼울 수 있다") | 코어에 비투사체(UI가 막지만 저장 파일 방어) |
| E_SLOT_COUNT_MISMATCH | 오류 | 없음(개발용) | 연결 칸 수 불일치 |
| W_TRIGGER_NO_PROJECTILE | 경고 | `warn.trigger_no_projectile` | 하위 스코프에 투사체 0 |
| W_NO_EFFECT | 경고 | `warn.no_effect` ("효과 없는 조합") | 아래 표 |
| W_SPLIT_DUPLICATE | 경고 | `warn.split_duplicate` | 같은 스코프 두 번째 이후 분열 |
| W_TRIGGER_DEPTH | 경고 | `warn.trigger_depth` ("트리거는 세 겹까지만 먹는다") | 넷째 트리거 |
| W_PROJECTILE_CAP | 경고 | `warn.projectile_cap` ("생략") | 64 상한으로 스코프 생략 |

효과 없는 조합(`W_NO_EFFECT`) — 그 변형이 **스코프 안 모든 투사체에** 효과가 없을 때만:

| 변형 | 효과 없는 투사체 |
|---|---|
| 관통 +2 | 그림자 구체, 연쇄 번개 [임시 U22] |
| 벽 튕김 | 연쇄 번개 [임시] |
| 유도 | 연쇄 번개 [임시] |

- 투사체 없는 스코프의 변형은 `W_TRIGGER_NO_PROJECTILE` 하나만(`W_NO_EFFECT` 안 냄).
- 그림자 구체 + 벽 튕김은 효과 있음(튕김), 경고 없음 [U31].

### 4.7 표준 T (S02 14.2) — 비기·패시브 피해 단위

[임시 U2] `T = Σ(루트 스코프 탄 하나하나, 분열 반영, 거울상 제외) 기본 피해 × 강화 배율 × (1 + 루트 변형 보너스 합) × 변이 배율(뒤틀린 총열 1.6) × 캐릭터 배율`. 하위 스코프·속성·임계·감쇠 제외. 마력탄 +0 → T = 10.
**모순:** S04 Q-01은 "트리거 스코프도 모두 적중했다고 보고 감쇠 곱해 더하고, 캐릭터 배율은 빼라"고 정의한다. 같은 등급 명세끼리 다르다 → 미정. 마력탄 단독은 두 정의 모두 10. 기존 시제품 코드는 S02 방식(`src/Wizard.Sim/Proto/SimWorld.ProtoSpell.cs:142`).

---

## 5. 발사 1회의 열 (S02 7절)

| 규칙 | 내용 |
|---|---|
| HT-1 | 열 = Σ 스코프 (블록 열 합 × 그 스코프 분열 배수). 2갈래 ×1.6, 3갈래 ×2.2 |
| HT-2 | 투사체·변형 열만 더함. 트리거 0, 분열은 배수만 |
| HT-3 | 하위 스코프에 부모 분열 배수를 곱하지 않음 |
| HT-4 | 맞혔는지와 무관하게 발사 시 모든 스코프 열을 더함 |
| HT-5/6 | **갈피 밑줄**: 맨 끝 연결 칸(확장 포함 L번)의 블록 열을 `min(5, ceil(h/2))` 깎음. 그 칸이 비었거나 분열·트리거면 깎지 않음(앞 칸으로 옮겨가지 않음). 분열 배수는 깎은 뒤 곱함 |
| HT-7 | 변이 거울상: J 발사·마력 방출에서 열 ×1.5 |
| HT-8/9 | 모든 비기·사본 마법진은 열 0 |
| HT-10 | 스쳐 피하기 보상: 다음 발사 1회 열 0 (과열 요약서) |

밑줄 결과: +2→+1, +3→+1, +5→+2, 4→2, 8→4, 10→5, 12→7, 14→9, 18→13.

검산 벡터(S02 16.4): 마력탄 4 / +split_3 8.8 / split_2+double_size 11.2 / trig+fireball 22 / trig,fireball,split_3 43.6 / split_3,trig,fireball 26.8 / 갈피 L(_,_,_,_,fireball) 17 / 갈피 L(_,_,_,split_2,frost) 8.0 / 갈피 L(_,_,_,fireball,_) 22 / 갈피 확장1 L(_,_,_,_,fireball,_) 22 / 거울상 마력탄 6.

과열 쪽으로 넘기는 것: HeatPerShot, 거울상 여부, FireIntervalTicks. 임계 판정(열 더한 뒤 ≥ 임계 시작 → 임계, 100 초과 → 임계 + 잠금)은 과열 요약서. 이 영역은 `isCrit`만 받는다.
기존 구현: `src/Wizard.Sim/Heat/ShotHeat.cs:23`(ShotHeatHu, 1열 = 120HU), `:77`(UnderlineCutHu).

---

## 6. 실행 `Execute` (S02 9절)

`Execute(tree, 위치, 방향, damageMul, heatExempt, 진영, 옵션{IsCrit, GlassCritBonus, CastId, Source, EnhanceStage, CharacterMul, ProjectileBudget})`
- 쓰는 곳: J 발사(damageMul 1), 마력 방출 2, 사본 마법진 0.5, 부채 곁가지 0.7, 팔방 곁가지 0.6 … 호출자가 넣는다. 보스 허상도 진영만 바꿔 같은 함수(13.2 — 보스 요약서).
- Source: J(Fire), ManaDischarge, ArtOther, CopyCircle, EnemyCopy.

절차:
1. `!IsValid` → 아무것도 안 함.
2. mirror = 변이 거울상 && Source ∈ {J, 마력 방출}. maxDepth = mirror ? MaxDepthMirrored : MaxDepthNormal.
3. 불안정 핵(J 발사만): 5% 확률로 자폭 → 탄 안 나감, 열 0, 이벤트(내 자리 폭발, 잠금 2초 + 콤보 0은 과열/콤보 쪽) [U11].
4. 루트 펼치기: S0 투사체를 **슬롯 순**, 각 투사체를 부채 각도 **오름차순**으로 요청 생성.
5. mirror면 같은 요청을 방향 +180°로 한 벌 더(앞 묶음 다음 뒤 묶음).
6. 뒤틀린 총열: 루트 탄마다 방향 += 균등 난수 [−25°, +25°].
7. 예산(비기 160) 자르기 → 화면 상한(8절)으로 생성.
8. 탄 문맥(자손이 물려받음): CastId, Source, 진영, IsCrit, GlassCritBonus, EnhanceStage, CharacterMul, damageMul, maxDepth, tree, ScopeDepth.
9. 반환: HeatToAdd = heatExempt ? 0 : HeatPerShot × (mirror ? 1.5 : 1).

부채 각도 (S02 9.3): 1 → 0 / 2 → −10°, +10° (가운데 없음) / 3 → −15°, 0°, +15°.

### 6.1 하위 스코프(트리거) (S02 9.4)

```text
OnFirstHit(proj, hitPoint, hitDir, hitEnemy):    # 투사체당 1번만
  if proj.TriggerFired: return; proj.TriggerFired = true
  child = tree.Scopes[proj.ScopeDepth+1]
  if child 없음 or child.Depth > proj.MaxDepth or child.Projectiles 0개: return
  for p in child.Projectiles(슬롯 순): for off in Fan(child.SplitCount):
     생성(p, dir = hitDir+off, depth = child.Depth, at = hitPoint, ignoreEnemy = hitEnemy)
```

- 자리 = 첫 적중 지점(폭발이면 폭발 중심, 연쇄 번개면 첫 대상 중심). 방향 = 부모가 날던 방향(연쇄 번개는 발사 방향) [U13].
- 하위 탄은 자기를 만든 적을 맞히지 않는다 [U13]. 생긴 틱에는 움직이지 않고 다음 틱부터 [U30].
- 관통탄·그림자 구체도 첫 적중에서만 발동. 폭발은 중심에서 1회. 빈 땅 폭발(맞은 적 0)은 발동 안 함 [U27].
- 임계·강화 단계·캐릭터 배율은 발사 순간 값을 자손도 씀 [U14].
- 하위 스코프·분열·거울상 탄은 부모와 같은 CastId(콤보 "발사 1회당 최대 +3"용).

---

## 7. 투사체 동작 (S02 10절)

### 7.1 상태 기계

| 상태 | 들어옴 | 하는 일 | 나감 |
|---|---|---|---|
| Pending | 생성 | 생긴 틱엔 안 움직임 [U30] | 다음 틱 → Flying (즉발은 생긴 틱에 판정 후 Dead) |
| Flying | | 유도 조향 → 이동 → 벽 → 적 충돌 → 사거리 | 사거리 도달 / 벽(튕김 없음) / 관통 소진 → Dead(폭발 블록은 폭발 후). 부메랑 편도 끝·벽 → Returning |
| Returning(부메랑) | 편도 6칸 또는 벽 | 쏜 사람의 **지금 위치**로 같은 속도 | 쏜 사람 0.5칸 안 / 돌아오는 길 9칸 초과 → Dead [U12] |
| Dead | | 다음 프레임 제거 | |

- 탄 생성 위치 = 쏜 사람 중심 [U30].
- 한 틱 순서: Pending→Flying 전환 → 탄 id(생성순) 오름차순으로 (a)유도 (b)이동(Traveled 누적) (c)벽 (d)적 판정: 이전 위치→새 위치 선분을 (탄 반지름 + 적 반지름)으로 쓸어서 닿은 적을 **선분 위 거리 순**으로 처리 (e)사거리. 적중으로 생긴 하위 탄·폭발은 같은 틱 끝에 붙는다(하위 탄은 Pending).
- 솔로 구현: Unity 물리 대신 원-원/선분 쓸기를 직접 계산하거나 `Physics2D.CircleCast`로 대체 가능(결정론 불필요).

### 7.2 블록별 동작 (S02 10.4)

| 블록 | 동작 |
|---|---|
| 마력탄 | 직선. 첫 적에 맞고 사라짐(관통 없으면) |
| 거대 화염구 | 직선. 적에 맞으면 그 자리에서 반경 2 폭발. 폭발 안(적 중심 거리 ≤ 2 + 적 반지름)의 모든 적이 각 1회 탄 피해. **직격 피해 따로 없음**. 벽이나 사거리 끝에서도 그 자리에서 터짐 [U11] |
| 연쇄 번개 | 즉발. 발사 방향 사거리 7 광선(반폭 0.12 + 적 반지름)에서 가장 가까운 적 1체 → 그 적에서 반경 3 안의 아직 안 맞은 가장 가까운 적으로 전이, 최대 3회(합 최대 4타). 피해 k번째 ×0.7^k: 18, 12.6, 8.82, 6.174. 대상 없으면 멈춤. 덤불 늑대의 회피 무시 [U10] |
| 부메랑 | 속도 10, 편도 6, 갈 때·올 때 각 다리마다 적 1회(같은 적 2번 가능). 다리마다 HitSet 새로 |
| 얼음 송곳 | 직선. 맞은 적 1초(60틱) 이동 −30% |
| 그림자 구체 | 적을 몇이든 지나감, 같은 적 1번만(튕겨서 다시 지나가도 X). 벽에서 소멸. 사거리 끝에서 **터지지 않고** 사라짐. 반지름 0.25 |
| 벽 튕김 | 벽(전장 가장자리)에 입사각 반사. 튕긴 뒤에도 이동 거리 이어서 셈. 블록 1개당 2회. 성에(char.4)의 얼음 병풍에는 튕기지 않고 지나감 |
| 탄환 크기 2배 | 반지름 ×2(겹치면 곱, 최대 ×4), 피해 +20%(합산). 폭발 반경 불변 |
| 관통 +2 | 적 2체 더 지나감(블록당 +2, 1개면 최대 3체 맞힘). 같은 적 1번 |
| 서리 | 맞은 적 1초 이동 −20% |
| 유도 | 대상: 시야 앞 ±90°, 6칸 안 가장 가까운 적. 틱당 최대 3°(초당 180°) 회전. 대상이 죽으면 다시 고름 [U9] |
| 과열 3배 | 그 스코프 탄의 임계 배율 2 → 3 [U4: 스코프 단위] |
| 잠식 | 맞은 적 3초(180틱) 받는 피해 +15%. 겹치지 않고 시간만 갱신 |

부가:
- 서리·잠식·얼음 송곳 효과는 그 탄이 피해 준 모든 적(폭발 범위, 연쇄 전이 대상 포함)에 건다 [U28].
- 땅속 적(화분 족제비 등)은 피해·효과를 받지 않고 탄이 지나감(탄 소모 없음). 비행 적은 맞음(그림자 위치가 판정).
- 철갑(선돌지기 정면) 등 적 쪽 피해 조정은 피해 공식 끝에 곱함(적 요약서).

### 7.3 적중 1회 처리 순서 (S02 12.3)

1. 피해 계산(이때의 잠식 상태로 대상 상태 배율 결정) → 2. 피해 적용·처치 → 3. 감속·잠식 적용 → 4. 첫 적중이면 트리거 → 5. 이벤트(표시).

---

## 8. 투사체 수 상한과 보전 (S02 8절)

| 상한 | 값 |
|---|---|
| 트리 실행 1회 | 64 (스코프 단위 가지치기, 보전 없음). 거울상 포함. 사본 마법진도 따로 64 |
| 화면 플레이어 탄 | 300 (사본 마법진 포함) |
| 화면 적 탄 | 150 (따로) |
| 비기 1회 | 160 (예산 넘으면 실행 순서 뒤쪽부터 안 만듦, 보전 없음) |

화면 상한 보전 [임시 U15]: 한 묶음 N발 요청, 여유 M발이면 앞쪽 M발만 생성하고 각 탄 `CompensationMul *= N/M`. M = 0이면 생성 안 함. 예: 290발 살아 있고 21발 요청 → 10발 생성, 보전 2.1 (TV-E07, 마력탄이면 피해 21).

수 검산(S02 16.5): split_3,trig,fireball,split_3 → 3 + 9 = 12 / 갈피+3 L(split_3,trig,mb,mb,split_3,trig,mb,split_3) → 3 + 18 + 54 = 75 > 64 → S2 생략, 21 / 달구+3 L(mb,split_3,trig,mb,mb,mb,split_3) → 6 + 54 = 60 / 같은 것 + 거울상 J → 120 > 64 → S1 생략 12.

---

## 9. 피해 공식 (S02 11절)

```
피해 = 기본 × 강화 배율 × (1 + 변형 보너스 합) × 변이 배율 × 임계 배율 × 비기 배율
     × 감쇠^깊이 × 속성 배율 × 대상 상태 배율 × 캐릭터 배율 × 전이 배율 × 보전 배율
     → 그 뒤 적 쪽 조정(철갑 등)
```

| 항 | 값 |
|---|---|
| 기본 | 맞힌 투사체 블록 `base_damage` |
| 강화 배율 | **1.15^단계** (단계 0~13, +13 ≈ 6.152788). 유리 지팡이 판 안 임시 하락 반영한 실효 단계 |
| 변형 보너스 합 | 그 스코프의 크기 +0.2, 관통 +0.1 합 |
| 변이 배율 | 뒤틀린 총열 ×1.6, 없으면 1 |
| 임계 배율 | 임계면 (과열 3배 있으면 3, 없으면 2) + (유리 지팡이면 +1). 아니면 1. **비기·사본은 임계 없음** |
| 비기 배율 | Execute의 damageMul |
| 감쇠^깊이 | 0.85^d (갈피 0.92^d): 1, 0.85, 0.7225, 0.614125 / 1, 0.92, 0.8464, 0.778688 |
| 속성 배율 | 약점 1.25 / 저항 0.8 / 그 밖 1 |
| 대상 상태 배율 | 잠식 중이면 1.15. 잠식을 건 그 탄에는 적용 안 함(다음 피해부터). 비기·패시브 피해에도 적용 [U29] |
| 캐릭터 배율 | char.5(그믐) 1 + 0.03 × 가진 결정 수(빚 결정은 0개, 비기는 쓴 뒤 남은 개수). 그 밖 1 |
| 전이 배율 | 연쇄 번개 0.7^k |
| 보전 배율 | 8절 N/M |

강화 배율 표(1.15^n): 0 1.000000, 1 1.15, 2 1.3225, 3 1.520875, 4 1.749006, 5 2.011357, 6 2.313061, 7 2.660020, 8 3.059023, 9 3.517876, 10 4.045558, 11 4.652391, 12 5.350250, 13 6.152788.
피해 숫자 표시: 가장 가까운 정수(.5 올림) [U19].
블록을 거치지 않는 피해(패시브·고유 비기): 기본 = nT, 변형·변이·임계·감쇠·전이 항 빼고, 속성은 캐릭터 속성 (DM-8).

검산(S02 16.8): 마력탄 10 / +13 61.528 / 임계 20 / 임계+과열3배 30 / 임계+유리 30 / 임계+과열3배+유리 40 / 마력 방출(임계 구간이어도) 20 / 약점 12.5, 저항 8 / 잠식 3발 10, 11.5, 11.5 / 깊이1 화염구 34, 깊이2 28.9 / 갈피 36.8, 33.856 / 크기+관통 13 / 그믐 결정7 12.1 / 뒤틀린 총열 16 / +5 임계 약점 50.284.

---

## 10. 상태이상 (S02 12절)

### 10.1 감속

| 규칙 | 내용 |
|---|---|
| 세기 | 얼음 송곳 −30% 60틱, 서리 −20% 60틱 |
| 겹침 | 출처별 칸(얼음 송곳 / 서리 / 달구 불가마 / 살얼음 변환) 각각 (세기, 남은 틱). **더하지 않고 가장 큰 값 하나** 적용, 시간은 출처별로 따로 갱신 |
| 같은 출처 재적용 | 시간만 60으로 새로(세기도 새 값) |
| 보스 | 절반(−15%, −10%, 불가마 −10%). 엘리트·중량은 그대로 |
| 빙결 내성 | 무관하게 걸림 |
| 땅속 | 안 걸림. 비행은 걸림 |
| 적 이동 속도 | 기본 × (1 − 실효 감속) |

예: 얼음 송곳 뒤 30틱에 서리 → 0~59틱 −30%, 60~89틱 −20%, 90틱부터 0 (TV-I01).

### 10.2 잠식

active + 남은 180틱. 다시 걸면 시간만 180으로. 땅속 무시. 받는 피해 ×1.15. 이번 피해에는 미적용(피해 계산 → 상태 적용 순서).

### 10.3 표시 (E 13.3)

- 감속: 적 발목에 하늘색 서리 고리. −30% 두 줄, −20% 한 줄, 겹치면 큰 쪽. 끝에서부터 녹아 짧아짐.
- 잠식: 적 윤곽 연보라 `#D9C7FF` 점선 테 2px + 보라 금. 시계 방향으로 줄어듦. 잠식 중 피해 숫자에 작은 보라 ▲.
- 재적용 시 깜빡이지 않고 다시 길어짐.

---

## 11. 변이 중 주문에 닿는 것 (S02 9, 11; S08 4.9) — 변이 획득은 강화 요약서

| 변이 (이름 열쇠) | 주문 효과 |
|---|---|
| `mutation.mirror_image` 거울상 | J 발사·마력 방출에서 뒤(+180°)로도 동시 발사, 64 상한에 포함, 열 ×1.5 |
| `mutation.twisted_barrel` 뒤틀린 총열 | 피해 ×1.6, 루트 탄 방향 ±25° 흔들림 |
| `mutation.glass_wand` 유리 지팡이 | 임계 배율 +1. 피격 시 강화 −1(웨이브당 1회, 판 안에서만) |
| `mutation.unstable_core` 불안정 핵 | 폭발 반경 +100%(화염구 2 → 4). J 발사 5% 자폭(잠금 2초 + 콤보 0). 코어가 화염구일 때만 제시 |
| `mutation.hypersensitive_circuit`, `mutation.gluttony` | 과열 쪽(임계 70부터 / 처치 시 열 −4 등) — 과열 요약서 |

---

## 12. 사본 마법진 (갈피 고유 비기, S02 13.3) — 실행 방식만

- `Execute(tree, 마법진 위치, 내 시야, 0.5, heatExempt=true, 플레이어, Source=CopyCircle, IsCrit=false)`, 거울상 미적용, 콤보 제외.
- 6초(360틱) 유지, 0.3초(18틱)에 1번까지만 따라 쏘기, 12번 쏘면 소멸. 동시에 1개, 새로 깔면 이전 것 사라짐(남은 횟수 이어받지 않음). 64 상한은 따로, 화면 300엔 합산.
- 상세(커맨드, 결정 비용)는 비기 요약서.

솔로 제외(보스 쪽): 허상의 복사 변환(S02 13.2)은 보스 요약서가 다룬다. 요점만: 유도·서리·잠식 제거, 튕김 1회, 간격 ×2.5(올림), 속도 6, 피해 1칸, 12개 상한, 연쇄 번개 → 0.6초 예고 직선, 3갈래는 ±19.19°.

---

## 13. 야영 조립 UI가 다룰 정보 (S13 8.2~8.3, U 4.11, S02 14.4)

### 13.1 모드 (S13 8.2)

| 모드 | 진입 | 탭 |
|---|---|---|
| Camp | 판 중 야영(모닥불 옆 J) | [조립 `str.camp.tab.assembly` \| 비기 `str.camp.tab.arts`] |
| Library | 메인 메뉴 "조립과 서고" (`str.camp.title.library`) | [조립 \| 서고 `str.camp.tab.library`] |
| LoadoutEdit | 원정 준비의 [조립 바꾸기] (`str.camp.title.loadout_edit`) | 조립 |

**어느 모드에도 강화 탭 없음** [확정]. 야영은 조립·회복·비기 선택만.

### 13.2 화면 구성 (1920×1080, U 4.11)

| 영역 | 위치·크기 | 내용 |
|---|---|---|
| 위 띠 | (0,0) 1920×80 | `str.camp.header` "야영 · {day}일째 웨이브 {wave} 다음", 원정 지갑 `str.battle_hud.wallet`, 체력 |
| 왼쪽 단 | (0,80) 480×952 | 지팡이 이름·단계(`wand.N`, `wand.N.stage.S.nickname`) / **지팡이 회로** (24,160) 432×168: 첫 줄 코어(72)+연결 4칸(갈피 5), 둘째 줄 확장 3칸(열린 칸만 사용, 나머지 잠김) / **컴파일 트리 미리보기** (24,344) 432×480 / **경고 줄** (24,840) 432×72 최대 3줄 |
| 가운데 단 | (480,80) 960×952 | 조립 탭: **보관함** 판 중 12칸(6×2, 칸 64, 간격 16) + 고른 블록 설명 판 912×200 / 조작 [끼우기 J `str.camp.insert`] [빼기 K `str.camp.remove`] [서로 바꾸기 `str.camp.swap`], 전부 빼기 `str.camp.remove_all` / 아래: 체력 회복 `str.camp.heal`, 다시 뽑기, 허수아비 연습 `str.camp.practice` |
| 오른쪽 단 | (1440,80) 480×952 | **수치** (1464,96) 432×240: 발사당 열 `str.camp.stat.heat_per_shot`, 임계 사격 가능 횟수 `str.camp.stat.crit_shots`(0이면 경고 `str.camp.warn.crit_zero`), 허수아비 DPS `str.camp.stat.dummy_dps`. 조립 바꾸면 바뀐 값 0.5초 밝아짐 / 다음 웨이브 `str.camp.next_wave` / 출발 `str.camp.depart`, `str.camp.depart_hint`(J 1초) |

보관함 `str.camp.storage`. 서고 칸: 수량 `str.camp.library_block_count`("{n}개"), 끼운 것 `str.camp.library_equipped`("끼움").

### 13.3 수치 계산

- 발사당 열 = `HeatPerShot`(소수 한 자리 표시, S01 7.7).
- 임계 사격 가능 횟수 n (S01 7.7): 발사당 열 h가 0이면 ∞, h > 100이면 0, 아니면 `floor((100 − 임계시작)/h) + 1`. 마력탄: 임계 85 → 4, 80 → 6, 90 → 3. (기존 구현 `src/Wizard.Sim/Heat/HeatRules.cs:40`)
- 허수아비 DPS: 계산식 **미정**(명세는 "허수아비 DPS"라는 칸만 둠). 참고 검산: 마력탄 +0 J 누르고만 있음 44.9 DPS, 임계 구간 유지 69.2 DPS(S02 TV-D17/D18, 과열 요약서).

### 13.4 컴파일 트리 미리보기 (S13 8.3, U 4.11, S02 14.4)

| 요소 | 규칙 |
|---|---|
| 마디 | 32×32 틀 안 24×24 아이콘. 세로 간격 16, 가로 8. 틀 모양: 투사체 둥근 / 제어 육각 / 변형 깎은 네모 / 스코프 주인 굵은 테 |
| 스코프 | 들여쓰기 40px + 왼쪽 세로 묶음 선. 깊이 1~3은 분필 흰색 밝기 100/70/50% |
| 변형 | 그 스코프의 투사체 마디 오른쪽에 아이콘 가로 나열, 한 줄 최대 6개, 넘으면 "+N"(N = 개수 − 6) |
| 분열 | 마디 아래 2/3갈래 가지 |
| 배율 꼬리표 | 스코프마다 오른쪽 끝 `감쇠^깊이` 문자열. 갈피는 각주 점(깊이만큼). 형식은 아래 모순 참고 |
| 생략 | MaxDepthNormal보다 깊은 스코프는 점선 마디 + `str.camp.tree.omitted`("생략") |
| 경고 | 경고 걸린 마디 오른쪽 위에 경고 딱지. 경고 줄은 정렬 순서 앞 3개. 줄 고르면 그 마디에 선택 괄호 |
| 실행 순서 번호 | 마디 왼쪽 위 1, 2, 3 … |
| 맨 아래 줄 | `str.camp.tree.footer` "발사 1회 = 투사체 {n}개 · 열 {heat}" — n = 가지치기 뒤 Σ InstancesTotal, heat = HeatPerShot |
| 연동 | 마디 커서 ↔ 회로 칸(슬롯 번호) 선택 괄호 |
| 갱신 | 조립을 바꾸는 즉시 재컴파일·다시 그림 |
| 갈피 밑줄 | 지팡이 회로의 맨 끝 연결 칸 아래에 청록 밑줄. 확장되면 한 칸 뒤로 이동 (wands/galpi.md 6.6) |

예: `core=magic_bolt; links=split_3,on_hit_trigger,fireball,_` → "발사 1회 = 투사체 6개 · 열 26.8", S0(마력탄, 3갈래, 열 8.8, 3발) → S1(깊이1 ×0.85, 화염구, 열 18, 3발).

### 13.5 블록 출처와 규칙 (S13 8.1~8.2, S07)

- 판 중 보관함 12칸. 전리품 카드(일반 웨이브 4장 중 2, 엘리트 잡으면 5장 중 2, 보스 5장 중 2)로 얻음. 12칸 넘으면 버릴 블록을 골라 골드(일반 5, 희귀 15, 영웅 50, 빌린 블록 50) — 웨이브 요약서.
- 1일째 웨이브 6: 영웅 1장 **빌린 블록**(`str.block.borrowed` "빌림" 꼬리표, 점선 테).
- 출발 조립에 쓴 서고 블록은 죽어도 잃지 않음. 판이 끝나면 조립은 출발 조립으로 복귀.
- 서고(Library 모드): 16종 8×2, 가진 적 없는 블록 칸은 없음, 수량 0(가진 적 있음)은 별도 상태. 끼운 블록은 "끼움"으로 따로 셈. 같은 블록 한 개를 두 지팡이에 동시에 끼울 수 없음. 서고 한도 100(끼운 것 제외), 50개당 2,000골드 확장.
- 강화 제물로 바칠 때 "마지막 투사체 블록은 올릴 수 없다"(`str.enhance.last_projectile_tip`, `error.last_projectile`) — 강화 요약서.
- 제물 점수(강화 요약서): 일반 +4, 희귀 +8, 영웅 +14, 지팡이와 같은 속성 블록이 하나라도 있으면 +5(시도당 1회). 무 블록은 어떤 지팡이 속성과도 같지 않음.

### 13.6 HUD 연동

- 전투 화면 아래 지팡이 회로: 실행 중인 블록이 순서대로 켜진다(발사, 트리거 발동 시 해당 슬롯) (S02 14.4).

---

## 14. 표시 연결점 (S02 19절, E 10·13) — 그림은 회색 도형으로 대체

| 사건 | 표시 |
|---|---|
| 발사 | 총구 섬광. 임계 사격이면 흰 고리 + 검정 테 |
| 탄 | 속성 모양·색(1.1 표). 임계 탄은 이중 테(안 흰 2px, 밖 검정 2px) + 앞 쐐기 `>`. 서리 붙은 탄은 흰 하늘 테, 잠식 붙은 탄은 보라 얼룩 꼬리. 그림 크기 = 판정 크기 |
| 적중 | 적 2프레임 흰 번쩍, 2px 밀림, 속성색 불꽃 4개, 흰 피해 숫자(검정 테) |
| 약점 | 속성색 2프레임 물듦, 4px 밀림, 피해 숫자 보조색 1.25배 + ▲ + 속성 문양 |
| 저항 | 1프레임 어두워짐, 숫자 회청 `#8E9AAE` 0.8배 + ▼ |
| 임계 피해 숫자 | 흰색 1.5배 + 굵은 검정 테 + 느낌표 |
| 폭발 | 속성색 고리(굵기 = 반경 25%)가 반경까지 퍼짐, 안쪽 채움 30% 이하, 0.25초 |
| 연쇄 전이 | 적 사이 꺾인 줄기 2프레임, 전이마다 가늘게 |
| 그림자 구체 지나감 | 적마다 보라 파문 1번 |
| 약점·저항 첫 발견 | 그 적 머리 위 속성 문양 1.5초, 이후 야영 "다음 웨이브"에 기록 |

금지(CLAUDE.md 2.5, E 0.1): 플레이어 탄·효과에 위험색(빨강 `#FF3B30`, 자홍 `#FF2E88`, 색상각 330~15° & 채도·명도 70%↑) 금지, 탄·임계에 금색 금지, 플레이어 넓은 효과를 흰색 면으로 채우지 않음. 표시 한도: 적중음 1초 8번, 그림자 구체 지나감 소리 1초 6번.

---

## 15. 핵심 테스트 벡터 (구현 확인용, S02 16절 발췌)

| ID | 입력 | 기대 |
|---|---|---|
| C01 | core=magic_bolt, 빈 4칸 | 유효, 스코프 1, 간격 9틱, 열 4, 투사체 1, 진단 없음 |
| C02 / C03 | core 빔 / core=split_two | E_CORE_EMPTY / E_CORE_NOT_PROJECTILE |
| C05 | core=fireball, L(magic_bolt) | 간격 36틱, S0 탄 2개 동시 |
| C06 | L(double_size, ice_spike) | 마력탄·얼음 송곳 모두 +0.2 |
| C08 | L(split_three, split_two) | split 3, W_SPLIT_DUPLICATE(slot 2), 탄 3 |
| C09 | L(ice_spike, split_three) | 탄 6 |
| C11 / C12 | L(trigger) / L(trigger, frost) | W_TRIGGER_NO_PROJECTILE(slot 1) / 같은 경고만, 열 7 |
| C14 | L(double_size, trigger, fireball) | S0 마력탄 0.2, S1 화염구 0 |
| C15 | 갈피+확장3 L(trig,mb,trig,mb,trig,mb,trig,mb) | S0~S3, slot 7 W_TRIGGER_DEPTH, slot 8 마력탄은 S3(S3 탄 2) |
| C16 / C17 | core=shadow_orb + pierce / core=mb + shadow_orb + pierce | W_NO_EFFECT / 없음 |
| C18 | core=chain_lightning + homing | W_NO_EFFECT |
| G02 | mb + pierce + trig→fireball, 일렬 적 3 | 3체 각 1회, 하위 스코프는 첫 적에서 1회 |
| G05 | 부메랑, 앞 3칸 적 1 | 같은 적 2회, 트리거 1회 |
| G09 | 화염구 A 적중, 2칸 안 B·C, 2.1칸 D | A·B·C 각 40, D 없음 |
| G12 | 벽 튕김 1개, 수직 입사 | 방향 반전, 남은 튕김 1, 이동 거리 누적 |

---

## 16. 기존 src 참고 위치 (실구현이 있는 것만)

| 내용 | 위치 |
|---|---|
| 발사 1회 열 계산(스코프별 합 × 첫 분열 배수, 밑줄, 거울상) | `D:\algorithm_game\src\Wizard.Sim\Heat\ShotHeat.cs:23` |
| 밑줄 깎는 양 `min(상한, ceil(h/2))` | `D:\algorithm_game\src\Wizard.Sim\Heat\ShotHeat.cs:77` |
| 임계 사격 가능 횟수 | `D:\algorithm_game\src\Wizard.Sim\Heat\HeatRules.cs:40` |
| 시제품 발사(마력탄/화염구 + 3갈래 −15/0/+15) | `D:\algorithm_game\src\Wizard.Sim\Proto\SimWorld.ProtoSpell.cs:103` |
| 시제품 표준 T | `D:\algorithm_game\src\Wizard.Sim\Proto\SimWorld.ProtoSpell.cs:143` |
| 시제품 탄 이동·적중·속성 배율 | `D:\algorithm_game\src\Wizard.Sim\Proto\SimWorld.ProtoSpell.cs:152`, `:211`, `:229` |
| 블록 데이터 형식(필드 주석에 단위) | `D:\algorithm_game\src\Wizard.Content\Defs\BlockDef.cs:6`, `BlockParams.cs:8` |
| 블록 열거 번호 | `D:\algorithm_game\src\Wizard.Content\Ids\BlockId.cs:4` |

`src/Wizard.Sim/Spell/*`는 빈 껍데기(타입 선언만). 실제 컴파일러·실행기·투사체 시스템 구현은 없다.

---

## 17. 미정 / 모순

| # | 내용 | 처리 |
|---|---|---|
| M1 | **블록 id 표기가 문서마다 다름.** S02: `great_fireball`, `split_2`, `split_3`, `pierce_2`, `overheat_x3`, `erosion`. S07: `block.mana_bolt`, `block.giant_fireball`, `block.split2`, `block.size_x2`, `block.on_hit` 등. 정본(spec 14 3.3, blocks.json, names.ko.json): `block.fireball`, `block.split_two`, `block.split_three`, `block.pierce`, `block.triple_heat`, `block.corrosion` | 정본 사용 |
| M2 | 속성 표기: S02 `Frost`/`Dark` ↔ 정본 `element.ice`/`element.dark`. `Frost`는 블록 `block.frost`(서리)와 헷갈림 | 정본 사용 |
| M3 | 변이 id: S02 14.5 `mirror`, `hair_trigger` ↔ 이름 표 `mutation.mirror_image`, `mutation.hypersensitive_circuit` | 이름 표 열쇠 사용 |
| M4 | `data/sim/modifiers.json`은 **주문 변형이 아니라 웨이브 "변덕"(whim.*)** 데이터다. 주문 변형 값은 blocks.json의 modifier 행에 있다 | 웨이브 요약서 담당 |
| M5 | 배율 꼬리표 형식: S02 14.4/TV-J03은 소수 둘째 자리 **내림**(갈피 깊이2 "×0.84"), S13 8.3/TV-074는 **반올림**(갈피 깊이2 "×0.85", 깊이3 "×0.78"). 기본 감쇠는 둘 다 "×0.85/×0.72/×0.61" | 미정. 같은 등급 명세 충돌 |
| M6 | 표준 T 정의: S02 14.2(루트만, 캐릭터 배율 포함) vs S04 Q-01(트리거 스코프 포함·감쇠 곱, 캐릭터 배율 제외) | 미정. 마력탄 단독은 둘 다 10 |
| M7 | S13 8.3 `CompileTreePreview.heatPerShot`이 int인데 열은 소수(26.8) | float로 |
| M8 | 허수아비 DPS 계산식 없음 | 미정 |
| M9 | 깊이 감쇠 0.85/0.92, 밑줄 상한 5, 64/300/150/160 상한, 거울상 1.5·뒤틀린 총열 1.6·불안정 핵 5%는 data JSON에 없음(명세 상수) | 솔로 쪽 상수 또는 설정 파일로 |
| M10 | [임시] 목록(그대로 씀): 판정 반지름(U1), 부모 변형 비상속(U3), 과열 3배 스코프 단위(U4), 넷째 트리거 무시(U5), 튕김 2회/블록(U6), 관통 +2/블록(U7), 크기 곱·상한 ×4(U8), 유도 180°/s·±90°·6칸(U9), 연쇄 판정 방식(U10), 폭발 판정·직격 없음(U11), 부메랑 귀환(U12), 하위 스코프 자리·방향(U13), 스냅숏 상속(U14), 상한 보전 N/M(U15), 흔들림 ±25° 균등(U16), 효과 없는 조합 목록(U22), 빈 땅 폭발 트리거 없음(U27), 상태 효과 폭발·전이 대상에도(U28), 잠식 비블록 피해에도(U29), 생긴 틱 정지(U30) | [임시] 값 사용 |
| M11 | 부메랑 탄 그림 없음(마력탄 그림 청록으로 대체), `piercing` 그림의 대응 블록 미정(spec 14 U-17) | 그림 작업 보류(회색 도형) |
