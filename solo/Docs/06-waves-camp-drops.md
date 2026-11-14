# 06. 웨이브 · 변덕 · 야영 · 전리품 · 판 보상(오프라인) — 솔로 구현 요약서

대상: 순정 Unity(MonoBehaviour, float 허용, 서버·넷코드·결정론 없음)로 만드는 솔로 플레이.
원문: `docs/spec/07-waves-drops.md`(이하 S07), `data/sim/waves.json`·`modifiers.json`·`frames.json`·`tod.json`·`scoring.json`·`enemies.json`·`elites.json`·`blocks.json`, `docs/GDD.md`(G) 2.2·7·8(해금)·11·12·13절, `docs/spec/09-run-verify-ranking-season.md`(S09) 8·10·11·14절, `docs/DECISIONS.md`(D) 2.3.
괄호 안은 출처 절. [제안]=원문 시작값, [임시]=명세가 넣은 기본값, **미정**=값 없음.

---

## 0. 먼저 읽을 것

### 0.1 단위 (S07 0.2)

| 항목 | 원문 단위 | 솔로 구현 |
|---|---|---|
| 시간 | 초. 원 명세는 60틱/초 | `float` 초 또는 60Hz 고정 틱 카운터. **점수·지체·묶음 간격은 틱 정수로 세는 것을 권장**(테스트 벡터가 틱 값) |
| 길이 | 칸(1칸 = 64px), 전장 24×14칸 | Unity 1유닛 = 1칸 권장 |
| 확률 | % | bp(1/10000) 정수 + `Random.Range(0,10000) < bp` 권장 |
| 점수·골드·체력 | 정수 | `int`/`long`. 내림 위치는 12절 식 그대로(벡터 일치용) |

자주 쓰는 틱↔초: 12=0.2초, 48=0.8초, 60=1초, 72=1.2초, 180=3초, 240=4초, 420=7초, 1,200=20초, 2,700=45초, 3,600=60초, 7,200=120초, 10,800=180초.

### 0.2 식별자 대응 — **데이터 파일의 id를 쓴다** (S07 0.3 ↔ data, CONTRACTS 2.1 대응표)

S07 본문은 옛 id를 쓰고 `data/`와 이름 표는 새 id를 쓴다. 구현은 오른쪽(data) id만 쓴다.

| S07 표기 | data / 이름 표 id | 표시 이름 열쇠 |
|---|---|---|
| modifier.rush | `whim.crowding` | `whim.crowding` (몰려옴) |
| modifier.thick_hide | `whim.thick_hide` | `whim.thick_hide` |
| modifier.big_herd | `whim.big_pack` | `whim.big_pack` |
| modifier.chief_march | `whim.chief_parade` | `whim.chief_parade` |
| modifier.last_breath | `whim.last_breath` | `whim.last_breath` |
| modifier.slow_barrage | `whim.slow_barrage` | `whim.slow_barrage` |
| modifier.long_night | `whim.long_night` | `whim.long_night` |
| modifier.narrow_field | `whim.narrow_field` | `whim.narrow_field` |
| frame.hunt | `frame.hunting_party` | `frame.hunting_party` (그 밖 `frame.swarm/battery/ambush/mixed`) |
| boss.mirror_master | `boss.mirror_phantom` | `boss.mirror_phantom` |
| boss.star_serpent | `boss.night_serpent` | `boss.night_serpent` (+ `.epithet`) |
| boss.kiln_boar / spore_queen / storm_owl / frost_goat | 같음 | 같음 |
| tod.day/dusk/night/boss | 같음 | `tod.day` 등 |
| mat.stone_low/mid/high | 같음 | `mat.stone_low` 등 |
| mat.charm_guard | `charm.safeguard` | `charm.safeguard` |
| cur.gold | `currency.gold` | `currency.gold`, 판 중 지갑은 `currency.expedition_purse` |
| cur.season_coin | `currency.season_coin` | 솔로 제외 |
| item.limited_drop | `item.season_fabric` | 솔로 제외 |
| 체력 조각 | `item.health_shard` | `item.health_shard` |
| 까마귀 보따리 | `item.crow_bundle` | `item.crow_bundle` |
| 재료 꾸러미 | `item.material_bundle` | `item.material_bundle` |

블록 16종(S07 0.3 ↔ `data/sim/blocks.json`, 등급은 `rarity`). **등급 안 추첨 순서는 아래 순서로 고정**(S07 13.2):

| 등급 | 순서대로 (data id) |
|---|---|
| 일반 `common` (6) | `block.magic_bolt`, `block.split_two`, `block.wall_bounce`, `block.double_size`, `block.pierce`, `block.frost` |
| 희귀 `rare` (6) | `block.fireball`(거대 화염구), `block.boomerang`, `block.split_three`, `block.on_hit_trigger`, `block.ice_spike`, `block.shadow_orb` |
| 영웅 `hero` (4) | `block.chain_lightning`, `block.homing`, `block.triple_heat`, `block.corrosion` |

### 0.3 기존 src 구현 상태

`src/Wizard.Sim/Waves/*`, `src/Wizard.Rules/Settlement/*`는 **빈 껍데기**(7~24줄 타입 선언뿐). 웨이브 디렉터·정산 알고리즘은 아직 없다. 참고할 만한 것은 데이터 로더뿐:
- `src/Wizard.Content/Pack/Sections/ContentDb.Waves.cs` (waves.json 팩 읽기, 183줄)
- `tools/Wizard.DataBuild/Convert/WavesConverter.cs:20` (waves.json 의 실수 → 정수 변환 규칙)

→ 이 영역은 **이 요약서와 JSON만으로 새로 만든다.**

### 0.4 솔로 제외(한 줄씩)

- 서버 검증·1차 검사·재실행 검증·점수/골드 상한(S07 12.3, 14.4; S09 7~9): 솔로 제외.
- 원정 잠금·원정 정리·14일 정리의 서버 절차(S09 10): 솔로 제외(오프라인 비정상 종료는 5.5절 참고).
- 시즌 주화·한정 드랍(`item.season_fabric`)·시즌 태그(S07 13.6): 오프라인 프로필에서 **나오지 않는다**. 솔로 제외.
- 순위표·후계 점수·주간 도전 보상(S09 13~16): 솔로 제외.
- 결투·광장·거래·장터·결제: 솔로 제외.
- 판 씨앗 분기(PCG `Derive`)와 결정론(S07 14.2~14.3): 솔로에서는 필수 아님. 단 "같은 씨앗 → 같은 웨이브" 성질을 원하면 흐름별 `System.Random(seed ^ tag ^ wave)`로 흉내 낼 수 있다(선택).

---

## 1. 데이터 파일과 필드

### 1.1 `data/sim/waves.json` — 웨이브 곡선 (S07 2.1, 5, 6, 7.2)

| 필드 | 값 | 의미·단위 |
|---|---|---|
| `day_length` | 10 | 하루 = 10웨이브 [확정 D2.3] |
| `tod_ranges[]` | {from,to,tod} (1–4 day, 5–7 dusk, 8–9 night, 10 boss) | **하루 속 순번**(LocalWave) 구간 → 시간대 |
| `hp_linear_until` | 12 | 이 웨이브까지 선형 체력 배율 |
| `hp_linear_step` | 0.12 | 선형 구간 웨이브당 +0.12 배 |
| `hp_exp_base` | 2.32 | w=12의 배율(지수 구간 시작) |
| `hp_exp_rate` | 1.06 | w≥13에서 웨이브당 ×1.06 |
| `elite_hp_mul` | 3 | 엘리트 체력 배수 |
| `boss_hp_ref_wave` | 10 | 보스 체력 = 기본 × H(w)/H(이 웨이브) |
| `budget_linear` | {base 10, per_wave 6, until 12} | B = 10 + 6w (w ≤ 12) |
| `budget_late` | {base 82, per_wave 4, cap 200} | B = min(200, 82 + 4(w−12)) (w ≥ 13) |
| `bundle_rule` | {base 4, grow_after 12, grow_every 8, max 8} | 묶음 수 = w≤12: 4, 그 뒤 min(8, 4 + (w−12)/8) |
| `bundle_interval_seconds` | 7 | 묶음 방출 간격 7초(420틱) |
| `bundle_early_release_alive` | 3 | 살아 있는 적 ≤ 3이면 다음 묶음 즉시 |
| `max_alive` | 40 | 전장 동시 적 상한 |
| `telegraph_cap[]` | {from,to,value}: 1–4:2, 5–8:3, 9–12:4, 13–29:5, 30–∞:6 (`to:null`=무한) | 동시 예고 수 상한(**누적 웨이브** 기준) |
| `elite_count[]` | 12–19:1, 20–29:2, 30–39:3, 40–∞:4 | 엘리트 수(보스 웨이브 0) |
| `elite_cost_mul` | 3 | 엘리트는 예산에서 비용 ×3 지불 |
| `species_cap[]` | golem_menhir 3, vine_thorn 4, toad_drum 2, boar_tusk 3, hog_needle 4, wisp_stray 4 | 한 웨이브 **구성 전체**의 종 상한(엘리트 포함) [임시 해석] |
| `stall_seconds` | 120 | 지체 방지 발동(7,200틱) |
| `stall_kill_score` | 0.5 | 지체 뒤 처치 점수 배율 |
| `modifier_count[]` | 21–40:1, 41–∞:2 | 변덕 수(그 밖 0, 보스 웨이브 0) |
| `damage` | contact 1, telegraph_plus 1, elite_boss_plus_from 31, all_plus_from 61, attack_cap 3 | 피해 성장(3.4절) |
| `fill_species` | `enemy.slime_moss` | 남는 예산을 채우는 종 |
| `script[]` | 웨이브 1–9, 11–13, 15 고정 각본 | 3.3절 |

`script[]` 항목: `wave`, `frame`(표시용 틀), `fixed[]`({species,min,max} 고정 개수. 박쥐는 "무리 하나"로 셈), `group[]`(주 묶음 종), `group_share`(예산 %), `rest`(`"mixed"`=혼합 / `"fill"`=남은 예산 전부 `fill_species`).

### 1.2 `data/sim/tod.json` — 시간대 가중치 (S07 2.3)

`tods[]`: `id`, `normal_enemies`(false면 일반 적 없음 = 보스), `weights[]`({species, weight} 배율. 적히지 않은 종 = 1, **0 = 나오지 않음**), `rim_strength_percent`(표시용 림 세기 20/40/60).

| tod | 가중치 |
|---|---|
| day | hog 1.5, vine 1.5, boar 1.5, bat 0, wisp 0 |
| dusk | wolf 2, bat 0.5, wisp 0 |
| night | bat 2, wisp 2, slime 0.5, hog 0, vine 0, boar 0 |
| boss | 일반 적 없음 |

### 1.3 `data/sim/frames.json` — 틀 (S07 2.4)

`frames[]`: `id`, `fixed[]`({species,min,max}), `group[]`(주 묶음 종), `group_share`(예산 %; 나머지 = 혼합).

| 틀 | 고정 | 주 묶음 | 몫 |
|---|---|---|---|
| frame.swarm 떼 | — | slime, bat, bug | 70% |
| frame.hunting_party 사냥대 | toad 1 | wolf, boar | 60% |
| frame.battery 포대 | golem 1~2 | shaman, hog, wisp, vine | 60% |
| frame.ambush 매복 | — | weasel, bug | 50% |
| frame.mixed 혼합 | — | — | 0 (전부 혼합) |

### 1.4 `data/sim/modifiers.json` — 변덕 (S07 2.5, 9)

`modifiers[]` 공통 `id`, `score_multiplier`(점수 배율, ×100 정수로 써라: 115/120/125). 효과 필드:

| id | 효과 필드 | 의미 | 점수 |
|---|---|---|---|
| whim.crowding 몰려옴 | `bundle_interval_seconds: 4` | 묶음 간격 7초 → 4초(240틱) | ×1.15 |
| whim.thick_hide 두꺼운 가죽 | `enemy_hp_multiplier: 1.3` | 적 체력 +30% | ×1.15 |
| whim.big_pack 큰 무리 | `extra_budget {percent 25, cost_max 2}` | 예산 +25%(내림), 추가분은 비용 ≤ 2 종만 | ×1.15 |
| whim.chief_parade 우두머리 행차 | `extra_elites: 2` | 엘리트 +2(예산 밖) | ×1.25 |
| whim.last_breath 마지막 숨 | `death_circle {radius 1.2, telegraph_seconds 0.8, damage 1, stagger_seconds 0.2}` | 모든 적 사망 자리에 반경 1.2칸 원, 0.8초(48틱) 예고 뒤 피해 1. 길이 막히면 늦게 생긴 원부터 0.2초(12틱)씩 미룸 | ×1.2 |
| whim.slow_barrage 느린 탄막 | `enemy_shots {count_multiplier 1.5, speed_multiplier 0.8}` | 적 탄 수 floor(n×3/2), 탄 속도 ×0.8(탄 사이 1.0칸 간격 유지) | ×1.2 |
| whim.long_night 긴 밤 | `only_tod: tod.night`, `weights` bat 2·wisp 2, `species_caps` wisp 6 | **밤 웨이브에만** 후보. 시간대 배율 뒤에 다시 ×2(→ 밤 bat·wisp = 4배), 도깨비불 상한 4→6 | ×1.2 |
| whim.narrow_field 좁은 들 | `thorn_border {width 2, telegraph_seconds 3, contact_interval_seconds 1.5, damage 1}` | 전장 가장자리 2칸 띠 가시밭, 웨이브 시작 전 3초(180틱) 예고, 같은 가시밭에서 1.5초에 1회 피해 1. 적 스폰은 가시밭 바로 안쪽 | ×1.25 |

설계 규칙: 예고 시간을 줄이거나 적을 빠르게 하는 변덕은 만들지 않는다(로더가 그런 필드를 거부) (S07 2.5, TV-109).

### 1.5 `data/sim/enemies.json` 에서 이 영역이 쓰는 필드

`id`, `cost`, `first_wave`, `times[]`(나오는 시간대), `base_hp`, `species_cap`. 표(S07 2.2):

| 종 | cost | 첫 등장 | 시간대 | 기본 체력 | 처치 골드(=cost×2) | 체력 조각 bp(=30×cost) |
|---|---|---|---|---|---|---|
| slime_moss | 1 | 1 | 항상 | 20 | 2 | 30 |
| wolf_bramble | 3 | 3 | 항상 | 45 | 6 | 90 |
| golem_menhir | 6 | 5 | 항상 | 300 | 12 | 180 |
| shaman_cap | 4 | 3 | 항상 | 40 | 8 | 120 |
| bug_lantern | 2 | 2 | 항상 | 18 | 4 | 60 |
| hog_needle | 4 | 4 | 낮·해질녘 | 35 | 8 | 120 |
| weasel_pot | 3 | 6 | 항상 | 55 | 6 | 90 |
| bat_dusk | 1 | 7 | 해질녘·밤 | 12 | 2 | 30 |
| wisp_stray | 4 | 8 | 밤 | 35 | 8 | 120 |
| vine_thorn | 4 | 11 | 낮·해질녘 | 70 | 8 | 120 |
| boar_tusk | 5 | 13 | 낮·해질녘 | 160 | 10 | 150 |
| toad_drum | 5 | 15 | 항상 | 90 | 10 | 150 |

- 박쥐 `spawn_pack` = 6~8마리 한 무리(S07 2.2 추가 필드). 기본 가중치 모든 종 100 [임시 U-01]. 예고 없는 적 = slime, bat(예고 상한과 무관).
- `enemy.spore_seedling`(19번째 줄 이후)은 보스 졸개 — 점수·골드·드랍 0.
- 엘리트(`elites.json`)는 바탕 종 수치 그대로 + 체력 ×3, 비용 ×3, 골드 ×3, 고유 능력(그 파일 몫). 엘리트가 낳은 개체(갈라진 슬라임 등)는 점수·골드·드랍 0.

### 1.6 `data/sim/scoring.json` — 점수 (S07 12)

`wave_coef {at_wave_1 1, per_wave 0.1}`(S(w)=1+0.1(w−1), 정수 S10=9+w), `kill_per_cost 10`, `boss_kill 3000`, `wave_clear 200`, `fast_clear {per_second 2, target_seconds 60, boss_target_seconds 120}`, `no_hit 100`, `graze {per_graze 5, cap 20, boss_cap 40}`, `phantom_first 3648`.

### 1.7 데이터 파일이 **없는** 수치 (드랍·야영·가방)

`data/`에 드랍 표·야영 비용·귀환 가방 JSON이 없다(**미정: 파일 없음**). 아래 4~5절 수치는 S07 원문 값이다. 구현 쪽에서 `solo` 전용 JSON(예: `drops.json`, `camp.json`)으로 옮겨 두기를 권장한다(코드에 박지 않는다는 원칙 S07 2).

---

## 2. 하루·웨이브 번호 (S07 3.1)

`w` = 누적 웨이브(1부터). 모든 곡선·기록은 누적 웨이브를 쓴다.

```
Day(w)        = (w + 9) / 10           // 올림(w/10). 1~10 = 1일째
LocalWave(w)  = (w - 1) % 10 + 1       // 1..10
Tod(w)        = LocalWave 1–4 day, 5–7 dusk, 8–9 night, 10 boss
IsBossWave(w) = w % 10 == 0
IsFifthWave(w)= w % 10 == 5            // 5, 15, 25 …
```

- HUD 표기: `str.battle_hud.day_wave` = "{day}일째 웨이브 {wave}" + 시간대 이름(`tod.*`). 예 "3일째 · 웨이브 27 (해질녘)"(TV-006). 기록 표기: `str.format.day_wave`.
- 밤에도 밝기·시야·적 속도·예고 시간은 낮과 같다. 밤에만 다른 것: 스폰 가중치, 돌발 사건 "별똥 결정"(`data/sim/starfall.json`, 다른 요약서 몫) (S07 3.1, G2.2).
- 시간대 색 전환: **야영 중** 4초에 걸쳐 바뀐다. 웨이브 4·7·9·10 뒤 야영에서만 전환, 9 뒤에는 달이 뜬다. 전투 중에는 안 바뀐다(표시 전용) (S07 3.1, TV-007).
- 날이 바뀌면 풀밭 무늬·소품 6벌 중 다음 것(표시 전용).

### 2.1 보스 순서 (S07 2.6, 7.5)

`k = ((w / 10) − 1) % 6` → 0 `boss.kiln_boar`, 1 `boss.mirror_phantom`, 2 `boss.spore_queen`, 3 `boss.storm_owl`, 4 `boss.frost_goat`, 5 `boss.night_serpent`. 즉 10,70,130…=가마등, 20,80…=허상 …

- 예외: 프로필이 **처음** 닿는 웨이브 10은 `boss.mirror_phantom` + 첫 만남 모드(PhantomFirst). 판 시작 때 `run.PhantomFirst = profile.PhantomNotMet && 일반 판` 으로 고정. 프로필당 1회(캐릭터마다 아님) (S07 8.2, TV-010~015).
- 보스 웨이브: 일반 적·엘리트·변덕·예산 없음. 보스가 부른 졸개만(점수·골드·드랍 0).

### 2.2 전장과 스폰 위치 (S07 3.2)

24×14칸, 사방 막힘. 적은 **플레이어에게서 먼 두 변**에서 나온다 [임시]: 플레이어 y < 7칸 → 위 변, 아니면 아래 변 / x < 12칸 → 오른쪽 변, 아니면 왼쪽 변. 정확히 가운데면 위·오른쪽. 두 변 위에서 무작위, 모서리 1칸과 다른 개체 1칸 이내는 피함. 좁은 들이면 가장자리에서 2칸 안쪽.

---

## 3. 웨이브 디렉터

### 3.1 곡선 함수 (S07 5, 6)

```
Budget(w)       = w <= 12 ? 10 + 6w : min(200, 82 + 4(w-12))      // 보스 웨이브는 0
BundleCount(w)  = w <= 12 ? 4 : min(8, 4 + (w-12)/8)
TelegraphCap(w) = w<=4:2, <=8:3, <=12:4, <=29:5, else 6
EliteCount(w, chiefParade) = (보스 || w<12) ? 0 : (w<=19?1 : w<=29?2 : w<=39?3 : 4) + (chiefParade?2:0)
ModifierCount(w)= 보스 ? 0 : w>=41 ? 2 : w>=21 ? 1 : 0
```

벡터: Budget 1,2,9,12,13,20,30,41,42,100 → 16,22,64,82,86,114,154,198,200,200 (TV-060/061). BundleCount 20,27,28,36,44 → 5,5,6,7,8 (TV-063). TelegraphCap 4,5,8,9,12,13,29,30 → 2,3,3,4,4,5,5,6 (TV-064). EliteCount 11,12,19,21,29,31,39,41 → 0,1,1,2,2,3,3,4; 보스 0; w=25+행차 → 4(예산 밖 2) (TV-065~067).

**체력 배율 H(w)** — 벡터 일치를 위해 정수 마이크로 표현 권장 (S07 6.1):
```
HMicro(w) = w<=12 ? 1_000_000 + 120_000*(w-1)
                  : 2_320_000 을 (w-12)번 h = h*106/100 (매번 내림)
EnemyHp(base,w,elite,thick) = clamp(base*HMicro(w)*(elite?3:1)*(thick?130:100) / 100_000_000, 1, int.Max)  // long으로
BossHp(base,w) = base*HMicro(w)/HMicro(10)
```
벡터: HMicro 13→2,459,200, 20→3,697,724, 30→6,622,055 (TV-022/023). slime w=1,12,20,30 → 20,46,73,132; golem w=5 → 444; golem 엘리트 w=12 → 2,088; golem 두꺼운 가죽 w=30 → 2,582; 허상 w=20 → 7,111 (TV-024~029).
(float `pow(1.06, w-12)`로 계산하면 1~2 HP 어긋날 수 있다 — 벡터를 맞추려면 위 정수식.)

**피해 성장** (S07 6.3, waves.json `damage`):
```
d = 예고 공격 ? 종별 값 : 1      // 접촉·탄은 언제나 1
if 예고 공격: if w>=61 d+=1; else if w>=31 && (엘리트||보스) d+=1
if 보스 최종 격노(전투 180초 뒤) d+=1
d = min(d, 3)                    // 한 번에 3칸 상한 [확정]
받는 피해 = d + (그믐 빚 ? 1 : 0)   // 최대 4
```
적 이동 속도·예고 시간은 웨이브가 깊어져도 **변하지 않는다**(TV-050).

### 3.2 웨이브 구성 생성 (S07 7) — 야영 진입 때 다음 웨이브를 미리 만든다

`WavePlan { wave, tod, frame, modifiers[0..2], entries[{species, elite, outsideBudget}], bundleOf[], boss, phantomFirst, previewSpecies[] }`.
**이전 웨이브가 끝나 야영에 들어갈 때** 생성한다(야영 화면에 미리 보여 줘야 하므로) (S07 7.1).

종 가용 조건: `w ≥ first_wave` && `tod ∈ times` && 시간대 배율 ≠ 0. 가중치 = 100 × 시간대 배율 × (긴 밤 배율).

생성 순서 (S07 7.4 [임시]):
1. `B = Budget(w)`, 큰 무리면 `extra = B*25/100`.
2. 변덕 선택(3.4절) → 긴 밤이면 가중치·상한 반영.
3. **엘리트(예산 안)** `EliteCount(w,false)`마리: 가용 종 중 `cost×3 ≤ 남은 예산` && 종 상한 미만에서 가중치 추첨, 예산 차감.
4. **고정 개수**: 각본(`script[].fixed`) 또는 틀(`frames[].fixed`). min~max는 무작위.
5. **주 묶음**: 목표 = `B × group_share / 100`(내림). 그룹 안 가용 종만 가중치 추첨, `cost ≤ 그룹 남은 몫` && 상한 미만인 종이 없을 때까지 반복. 남은 몫은 혼합으로 이월.
6. **나머지(혼합)**: 주 묶음 밖 가용 종 전체로 같은 방식(`rest:"fill"`이면 이 단계 없이 7로).
7. 남은 예산 ≥1 은 전부 `enemy.slime_moss`로 채운다 → **일반 웨이브 비용 합 = B 정확히**(TV-094).
8. 큰 무리 추가분 `extra`: 비용 1~2 가용 종으로 같은 방식, 남으면 slime (TV-095: w=22 → +30).
9. 우두머리 행차: 엘리트 2마리 추가(`outsideBudget=true`, 예산 조건 없음).
10. 박쥐는 뽑힐 때마다 6~8마리 무리(남은 몫이 모자라면 남은 몫만큼, 최소 1).
11. **묶음 나누기**: 엔트리를 섞은 뒤 각 묶음 비용 합이 `ceil(총비용/묶음수)`를 넘지 않게 앞에서부터 채움. 박쥐 무리는 한 묶음. 엘리트는 마지막 묶음부터 거꾸로 하나씩.

### 3.3 고정 각본 1~12와 13·15 (S07 7.2, waves.json `script`)

| w | 시간대 | 새 종 | 구성 |
|---|---|---|---|
| 1 | 낮 | 슬라임 | slime × 16 (TV-080) |
| 2 | 낮 | 꽈리 벌레 | bug 2~3, 나머지 slime (합 22) |
| 3 | 낮 | 늑대, 술사 | shaman 1, wolf 최대(=floor(남은/3)), 남는 예산 slime → shaman 1·wolf 8·slime 0 (TV-082) |
| 4 | 낮 | 고슴도치 | shaman 1, hog 1, wolf 최대, slime → wolf 8·slime 2 (TV-083) |
| 5 | 해질녘 | 선돌지기 | golem 1 + 혼합 |
| 6 | 해질녘 | 족제비 | golem 1, shaman 1, weasel 1 + 혼합. **클리어 시 빌린 영웅 블록 1장**(1일째만) |
| 7 | 해질녘 | 박쥐 | bat 무리 1(6~8) + 혼합 |
| 8 | 밤 | 도깨비불 | wisp 2 + 혼합 |
| 9 | 밤 | — | bat 무리 1, wolf 60% 몫 + 혼합 |
| 10 | 보스 | 허상(첫 만남)/가마등 | 보스 웨이브 |
| 11 | 낮 | 가시덩굴 | vine 1 + 혼합 |
| 12 | 낮 | 엘리트 | 엘리트 1(elite_count 표, 예산 ×3) + 혼합 |
| 13 | 낮 | 엄니 멧돼지 | 틀 = 혼합 강제, boar ≥1 [조율 C-04] |
| 15 | 해질녘 | 북두꺼비 | 틀 = 혼합 강제, toad ≥1 |
| 14, 16~ | | | 틀 추첨(3.4) |
| 21 / 41 | | | 변덕 1개 / 2개 시작 |

데이터 검증: 웨이브 1~4에 나오는 종은 비전 저항이 아니다(시작 블록 마력탄이 비전) (TV-097).

### 3.4 틀·변덕 고르기 (S07 7.3, 9)

**틀** (w ≥ 13 일반 웨이브, 13·15는 혼합 강제):
- 후보 = 5틀 중 **직전 일반 웨이브의 틀이 아닌 것**(보스 웨이브는 건너뛰고 그 앞을 본다) && 주 묶음 종이 하나라도 가용한 것(혼합은 항상). 후보 중 균등 [임시 U-02].
- 시간대에 없는 종은 틀에서 빠지고 그 몫은 혼합으로.
- 틀은 이전 결과에 의존하므로 순서대로 계산(TV-091, 092).

**변덕**:
```
n = ModifierCount(w)
pool = 8종 중 (long_night 는 Tod==night 일 때만)    // 데이터 순서
중복 없이 n개 균등 추첨 [임시 U-08]. 연속 웨이브 반복은 허용
```
적용 순서 [임시]: 큰 무리(예산) → 우두머리 행차(엘리트) → 긴 밤(가중치·상한) → 구성 생성 → 두꺼운 가죽(체력) → 몰려옴(간격) → 느린 탄막(적 탄) → 마지막 숨(사망 시) → 좁은 들(WavePrelude).
마지막 숨은 갈라진 슬라임·불러낸 개체 사망에도 생긴다. 웨이브 클리어 순간 남은 원은 즉시 사라진다.
다음 웨이브의 변덕은 **야영 화면에 미리** 보여 준다.

### 3.5 런타임: 묶음 방출·상한·지체 방지 (S07 5.2~5.6)

| 규칙 | 값 |
|---|---|
| 첫 묶음 | 웨이브 0틱 [임시] |
| 다음 묶음 | 직전 방출 후 420틱(7초) 경과(몰려옴 240틱/4초) **또는** 살아 있는 적 ≤ 3 이면 즉시 (TV-069, 070: 적 4마리일 때 419틱 X / 420틱 O) |
| "살아 있는 적" | 갈라진 슬라임·불러낸 박쥐 포함 [임시] |
| 동시 상한 40 | 넘치면 대기열. 자리가 나면 한 마리씩 바로 나옴(박쥐 무리도 낱마리로) (TV-068) |
| 동시 예고 상한 | `TelegraphCap(w)`. 상한·안전 경로 검사(R5)에 걸린 적은 예고 시작 대신 대기 줄. 2초(120틱) 넘게 기다리면 가장 오래 기다린 적부터 재검사. 예고 없는 적(slime·bat)은 무관. R5 자체는 적 AI 요약서 몫 |
| 지체 방지 | 일반 웨이브 `wave_tick ≥ 7200`(120초) → 남은 묶음 전부 대기열로(40 상한 유지, 간격 없음). 이후 처치 원점수 `cost × 5`(절반). 보스 웨이브 미적용 (TV-072) |
| 웨이브 끝 | 일반: 모든 묶음 방출 + 생존 0. 보스: 보스 사망(졸개 즉시 제거, 적 탄은 골드 조각 연출로만) |

---

## 4. 판 상태 기계 (S07 4, 11.4)

### 4.1 상태와 전이

| 현재 | 조건 | 다음 | 동작 |
|---|---|---|---|
| (출발) | skip = 0 | WavePrelude(w=1) | 체력 가득, 결정 0, 지갑 0 |
| (출발) | skip ∈ {10,20,…} | StartCamp | 첫 웨이브 = skip+1, 건너뛴 점수 합산(6.2), 건너뛴 구간의 비기 해금 차례를 여기서 차례대로 |
| StartCamp | 출발 | WavePrelude(skip+1) | |
| WavePrelude | 좁은 들 예고 180틱(3초) 끝(없으면 0틱) | 보스면 BossIntro, 아니면 WaveCombat | `wave_tick=0`(예고 구간은 시간에 안 넣음) |
| BossIntro | 72틱(1.2초) | WaveCombat | 화면 멈춤 + 이름 띠, 플레이어 무적·이동 가능. 보스 `wave_tick`은 BossIntro 시작부터 |
| WaveCombat | 일반: 전멸 / 보스: 보스 사망 | WaveClearing | 클리어 처리(4.2) |
| WaveCombat | 체력 0 (첫 만남 아님) | RunEnded(death) | |
| WaveCombat | 첫 만남: 체력 0 | PhantomDowned | |
| WaveCombat | 첫 만남: 허상 체력 ≤ 50%(2000) **또는** 2,700틱(45초) | PhantomFinale | |
| PhantomFinale | 180틱(3초), 입력 무시 | PhantomDowned | 적 탄 제거, 플레이어 체력 0 |
| PhantomDowned | 240틱(4초) | WaveClearing → LootPick | "일어남": 체력 가득, 열 0·잠금 해제, 콤보 0, 결정 유지(환전 평소대로), 그믐 빚 0. **웨이브 10을 직접 넘긴 것으로 기록** |
| WaveClearing | 60틱(1초) | LootPick | 드랍 빨아들이기 연출, 남은 예고·가시밭·원 제거. 이 상태부터 피해 판정 없음 |
| LootPick | 2장 확정 | Camp | |
| Camp | 출발 | WavePrelude(w+1) | |
| 아무 상태 | 일시정지 "판 포기"(`str.pause.give_up`) | RunEnded(abandon) | 확인 문구 `str.pause.give_up_confirm`. 직접 넘긴 웨이브 전부 정산 |

판이 끝나는 길은 체력 0 또는 포기뿐, 클리어 없음 [확정 G2.2]. 같은 틱에 플레이어와 마지막 적이 죽으면 플레이어 피해를 먼저 처리 → 체력 0이면 그 웨이브 미클리어 [임시 U-25].

### 4.2 웨이브 클리어 처리(같은 프레임 안 순서) (S07 11.1)

1. `elapsed = clear_tick − wave_start_tick`.
2. 남은 마력 결정 환전: 개당 **5골드**(그믐 빚이 있으면 빚부터 갚고 남은 것만) (TV-175: 4개 → 20, 빚 1이면 15).
3. 웨이브 클리어 골드 **20**(보스 웨이브에도) + 보스면 보스 처치 골드 `20 × (9 + w)`.
4. (시즌 주화 — 솔로 제외)
5. **기회 목록**에 추가: 다섯 번째 웨이브 / 보스 처치 / 처음 웨이브 3을 넘긴 까마귀 보따리 / 첫 만남. (엘리트 기회는 처치 때 이미 +1)
6. 웨이브 점수 확정(6.1).
7. `clearedDirect += 1`. `w % 5 == 0`이면 중간 지점 기록(5.5절).
8. 바닥 드랍 회수(체력 조각은 남아 있으면 자동 회수 = 체력 +1, 가득이면 효과 없음).

### 4.3 야영 (S07 11.2~11.4, D2.3, G2.2·13)

**[확정] 야영에서는 조립, 회복, 비기 선택만 한다. 강화 탭·강화 기능 없음.** (TV-196)

진행:
1. 마지막 적 사망 → 드랍이 1초 동안 빨려 옴(WaveClearing).
2. 전장 가운데 모닥불, 남은 예고·가시밭 사라짐.
3. **전리품 고르기가 먼저**(LootPick, 4.4절). 고른 블록 → 판 중 보관함(12칸).
4. 모닥불 옆에서 **J** → 야영 화면 열림. 탭 [조립 `str.camp.tab.assembly` | 비기 `str.camp.tab.arts`]. 체력 회복 버튼(`str.camp.heal`). 허수아비 연습(`str.camp.practice`)은 **조립 탭의 하위 기능**(조립 시험)으로 둔다 [조율 C-02].
5. 시간 제한 없음. 야영 중에는 점수용 `wave_tick`이 흐르지 않는다(TV-158).
6. 야영 화면 내용(헤더 `str.camp.header` "야영 · {day}일째 웨이브 {wave} 다음"): 3단 — 왼쪽 지팡이 슬롯·컴파일 트리 미리보기, 가운데 탭, 오른쪽 수치(`str.camp.stat.heat_per_shot`, `str.camp.stat.crit_shots`, `str.camp.stat.dummy_dps`; 임계 사격 0이면 `str.camp.warn.crit_zero`) + 다음 웨이브(`str.camp.next_wave`): 시간대, 틀 이름, 변덕, 나올 종의 약점·저항 문양, 보스면 보스 이름, **그 웨이브의 확정 재료**(표 B·C의 확정분). 항상 `str.camp.bag_preview` "지금 판을 끝내면 가져가는 귀환 가방 {slots}칸 · 영웅 {hero}개까지"(TV-197: cleared 14 → 4칸·영웅 1; TV-198: 다음 15 → 하급1·중급1; TV-199: 다음 30 → 중급1·상급1).
7. 출발: 모닥불 반대편 출발 표식 위에서 **J를 1초(60틱) 누르고 있으면** 출발(59틱에 떼면 취소) (`str.camp.depart_hint` "J를 1초 누르면 출발"), 또는 야영 화면의 [출발](`str.camp.depart`). 야영을 건너뛰고 바로 출발해도 된다.

야영 하위 상태:
```
LootPick --2장 확정--> FireIdle
FireIdle --모닥불 옆 J--> CampScreen(Assembly|Arts)
CampScreen --닫기--> FireIdle
FireIdle --출발 표식에서 J 누름--> DepartHold(60틱 카운트, 떼면 FireIdle)
DepartHold --60틱--> WavePrelude(w+1)
CampScreen --[출발]--> WavePrelude(w+1)
```

**새벽 야영**(보스를 잡은 뒤): 진입 시 체력 1칸 무료 회복(`str.camp.heal_dawn`) (TV-193: 3/8 → 4/8). 첫 만남 직후 새벽 야영은 이미 가득.
첫 만남 전의 웨이브 9 뒤 야영: 다음 웨이브 보스 이름에 "스승의 허상"을 평소대로 띄운다(이길 수 없다고 알리지 않음).

**회복 비용** (S07 11.3 [제안], U-13 [임시]):
```
HealCost(wJustCleared, healsThisCamp) = base = 30 + 5 × wJustCleared
                                        healsThisCamp==0 ? base : base*3/2   // 두 번째부터 1.5배(누적 아님)
```
1회 = 체력 1칸. 최대 체력이면 버튼 비활성. **원정 지갑에서만** 지불, 모자라면 불가. 벡터: w=5 → 55 / 82 / 82, w=10 → 80 / 120 (TV-190, 191).

**비기 선택**: 해금은 프로필에 캐릭터별로 남는다 [확정 D2.8]. 해금 차례 [제안 G8]: 웨이브 1~6을 넘길 때마다 그 캐릭터가 해금하지 못한 공용 비기 후보 2개 중 1개 무료 선택(공용 8종 다 해금하면 없음, 부모 기술 필요, 후보 1개면 1개만, 뇌광창은 꿰뚫는 창 + 웨이브 4 이후, 만개는 팔방 전개 + 웨이브 6 이후). 고르는 곳 = 야영 비기 탭. 이 영역은 "야영 진입 때 차례가 있는가" 알림만(세부는 비기 요약서). 고유 비기는 처음부터 해금.

**출발 야영(StartCamp)**: 조립·비기 탭만 의미 있음, 전리품 카드 없음, 지갑 0이라 회복 불가. 다음 웨이브 미리보기는 같다.

야영에서 귀환 가방 블록은 고르지 않는다(정산 화면에서) (S07 11.2).

### 4.4 전리품 카드 (S07 13.2, 13.3)

블록은 적이 떨구지 않는다. 웨이브를 넘긴 뒤 카드로만.

| 때 | 제시 | 고름 |
|---|---|---|
| 일반 웨이브 | 4 | 2 |
| 엘리트를 1마리 이상 잡은 웨이브 | 5 | 2 |
| 보스 처치 | 5, 그중 1장 희귀 이상 확정. **w ≥ 30 보스는 그 1장 영웅 확정** | 2 |
| 허상 첫 만남 | 4, 확정 칸 없음 | 2 |
| 1일째 웨이브 6 | 위에 더해 영웅 블록 1장 그냥 받음(**빌린 블록**) | — |

카드 한 장의 등급 확률 (`r = Random(0,100)`):

| 날 | 일반 | 희귀 | 영웅 | 판정 |
|---|---|---|---|---|
| 1 (w1~10) | 78 | 22 | 0 | r<78 일반, 나머지 희귀 |
| 2 (11~20) | 73 | 25 | 2 | <73, <98, <100 |
| 3 (21~30) | 70 | 26 | 4 | <70, <96, <100 |
| 4 이후 (31~) | 70 | 25 | 5 | <70, <95, <100 |

- 확정 칸(마지막 칸 [임시]): 일반을 뺀 비율로 — 1일째 희귀 100%, 2일째 `Random(0,27) < 25` 희귀 아니면 영웅, w≥30 영웅 (TV-212~214).
- 등급 안 종류: 0.2절 순서로 균등(`Random(0,6)` / `Random(0,4)`). 한 제시 안 중복 허용 [임시].
- **다시 뽑기**(`str.loot_pick.reroll`): **30골드**(지갑), 같은 확률로 전부 새로, 확정 칸은 유지, 횟수 무제한 [임시 U-14]. **전리품 고르기 화면에서만**(확정 전) [조율 C-08]. 지갑 < 30이면 불가(TV-192).
- 확인(`str.loot_pick.confirm`)은 2장을 채워야 켜짐. 제목 `str.loot_pick.title`, 카운터 `str.loot_pick.pick_count`.
- 보관함 12칸이 차면 버릴 블록을 고른다(`str.loot_pick.overflow_title`, 현재 보관함 + 새 카드 중). 버린 블록은 즉시 골드: 일반 5, 희귀 15, 영웅 50, 빌림 50 (`str.loot_pick.overflow_gold`) (TV-224).

빌린 블록 (S07 13.3): 1일째 w6 클리어만(시작 웨이브 11 이후의 16·26엔 없음). 영웅 4종 균등. 그 판에서만, 가방 불가, 판 끝에 50골드. 카드에 "빌림" 꼬리표(`str.block.borrowed` / `str.loot_pick.borrowed`) + 점선 테두리.

`WildBlock { blockId, grade, borrowed, acquiredWave, uid(판 안 순번) }`.

### 4.5 판 중 드랍 (S07 13.1, 13.6, 13.7)

| 출처 | 골드 | 기타 |
|---|---|---|
| 일반 적 처치 | cost × 2 (**처치 순간 지갑에 넣음**, 동전 줍기는 연출) | 체력 조각 `30 × cost` bp |
| 엘리트 처치 | cost × 2 × 3 (slime 엘리트 6, golem 36) | 체력 조각 1000bp(10%), 재료 기회 +1 |
| 보스 처치 | `20 × (9 + w)` (10:380, 20:580, 30:780, 50:1,180) | 카드 5장, 재료 기회, 새벽 야영 회복 |
| 웨이브 클리어 | 20 | 카드, 다섯 번째면 재료 기회 |
| 남은 결정 | 개당 5 | |
| 졸개·갈라진·불러낸 개체 | 0 | 점수·드랍도 0 |

일반 웨이브 처치+클리어 = `2B + 20`(w1 52, w5 100, w9 148, w14 200, w20 보스 600, w42+ 420) (TV-173). 누적 w1~n: 5→380, 8→752, 10→1,300, 14→2,048, 30→7,160, 60→21,628 (TV-174, 웨이브 10 = 가마등 가정).

**체력 조각** (`item.health_shard`): 처치 시 — 졸개·낳은 개체면 없음; 이번 웨이브에 이미 나왔거나 플레이어 체력이 가득이면 **추첨 자체를 안 함**; 아니면 bp 확률로 그 자리에 떨굼(웨이브당 1개). 주우면 체력 +1칸. 프로필에 안 남음 (TV-267~270).

**원정 지갑**(`currency.expedition_purse`): 판 중 골드는 지갑에 쌓이고 회복·다시 뽑기로 줄어든 뒤 정산 때 프로필로.

---

## 5. 판 종료와 오프라인 정산

### 5.1 기회 목록 (S07 13.5, 13.8)

판 중에는 재료를 **뽑지 않고** 기회만 기록, 정산 때 기기 난수로 뽑는다(오프라인은 기기가 뽑는다. 솔로에서 `UnityEngine.Random` 사용 가능).

`DropChance { source: CrowBundle | FifthWave | BossKill | EliteKill | PhantomFirst, wave, count(EliteKill=마리 수, 그 밖 1) }`. 건너뛴 웨이브는 넣지 않는다.

### 5.2 재료 표 (S07 13.5 [제안])

"확정" = 추첨 없이 그 수. "N%" = **줄마다 독립 1회**, 맞으면 1개. 한 기회에서 여러 줄 동시 당첨 가능. 엘리트 줄은 **한 마리마다** 1회(행차 엘리트 포함). `d = Day(w)`.

**A. 까마귀 보따리** (프로필당 1회): 그 프로필에서 **처음** 웨이브 3을 넘긴 판 → 하급 3 확정(`item.crow_bundle`). 받으면 `CrowBundleReceived=true` (TV-250, 251).

**B. 하루의 다섯 번째 웨이브** (w = 5, 15, 25…):

| d | 하급 | 중급 | 상급 |
|---|---|---|---|
| 1 (w5) | 확정 1 | | |
| 2 (w15) | 확정 1 | 확정 1 | |
| ≥3 | | 확정 1 | `bp = min(2500, 500 × (d−2))` |

**C. 보스 처치** (w = 10, 20, 30…):

| d | 하급 | 중급 | 상급 | 상급 추가 | 부적 `charm.safeguard` |
|---|---|---|---|---|---|
| 1 | 확정 1 | 5000bp | | | 200bp |
| 2 | | 확정 1 | 2000bp | | 400bp |
| ≥3 | | 확정 1 | 확정 1 | `bp = min(5000, 1000 × (d−3))`(d=3이면 추첨 생략) | `bp = min(1000, 200 × d)` |

첫 만남(프로필 첫 w10): **하급 1 확정만, 추첨 없음**(PhantomFirst 기회) (TV-257).

**D. 엘리트 1마리** (w ≥ 12):

| d | 하급 | 중급 | 상급 | 부적 |
|---|---|---|---|---|
| 2 (w12~19) | 1000bp | 800bp | | |
| ≥3 | | 500bp | `bp = min(500, 200 + 50 × (d−3))` | 50bp |

- 하급 강화석은 **웨이브 21부터 나오지 않는다**(표에서 자연히) (TV-261).
- 추첨 줄 순서 고정: 하급, 중급, 상급, 상급 추가, 부적.
- 확정 수량은 야영 화면에 바로 보이고, 추첨분은 정산 화면 "재료 꾸러미"(`item.material_bundle`, `str.settlement.take.bundle`) 여는 연출로.
- 재료가 어느 강화 단계에 쓰이는지는 강화 요약서 몫.

벡터: TV-252~260 (예 w25 B: 중급1 + 상급 500bp; w40 C: 상급 추가 1000bp·부적 800bp; w45 엘리트 상급 300bp).

### 5.3 귀환 가방 (S07 13.4 [제안 (가)안])

| 항목 | 규칙 |
|---|---|
| 칸수 | `min(12, cleared / 3)` — 기본 칸 없음 |
| 영웅 한도 | `cleared / 10` |
| 담을 수 있는 것 | 판 끝에 들고 있는 야생 블록(보관함 + 지팡이에 낀 야생 블록). **빌린 블록 불가** |
| `cleared` | 이번 판에서 **직접** 넘긴 웨이브 수(건너뛴 웨이브 제외, 첫 만남 w10 포함) |
| 고르는 사람 | 정산 화면(`str.settlement.page.return_bag`)에서 플레이어. 못 고른 판은 자동: 빌림 제외 → 등급 내림 → 블록 표 순서 → uid 오름, 영웅은 한도까지, 칸 차면 멈춤 |
| 검증 | 칸 초과·영웅 초과·빌림 포함은 거부(TV-241) |
| 남은 블록 | 골드(일반 5, 희귀 15, 영웅 50, 빌림 50) (`str.settlement.bag.leftover_gold`) |
| 서고 한도 | 블록 100개 시작(50개당 2,000골드 확장). 가득이면 담지 못한 것 → 골드(`str.settlement.bag.library_full`) (TV-239) [오프라인 적용 여부는 원문 명시 없음, 적용으로 가정] |
| 가방 칸을 안 쓰는 것 | 강화석, 부적, 골드 |

벡터 BagSlots: cleared 0,1,2,3,8,10,14,20,30,36,40,60 → 0,0,0,1,2,3,4,6,10,12,12,12; HeroLimit → 0,0,0,0,0,1,1,2,3,3,4,6 (TV-230, 231). 자동: cleared 20, 영웅4·희귀5·일반3 → 영웅2·희귀4 (TV-236).
출발 조립에 쓴 프로필 블록(서고 블록)은 야생 블록이 아니라 죽어도 잃지 않는다. 판이 끝나면 조립은 출발 조립으로 되돌아간다.

### 5.4 오프라인 정산 절차 (S07 13.8, S09 8.1·10)

```
Settle(run):        // 오프라인: 기기가 즉시 Final 로 처리
  cleared = run.ClearedDirect
  slots = min(12, cleared/3); heroCap = cleared/10
  (주간 도전 판이고 그 주 4번째 이후 판이면 slots = 0)        // 5.6
  bag = 플레이어 선택(검증) 또는 AutoPick
  bag = 서고 한도 적용(넘치면 골드)
  gold = run.WalletGold + Σ 골드(보유 야생 블록 − bag)
  materials = Σ 확정분 + 기회마다 줄별 독립 추첨(기기 난수)
  profile.gold += gold; 서고 += bag; 재료·부적 += materials
  if run.PhantomFirst && cleared >= 10: profile.PhantomNotMet = false
  if run.KilledPhantomAtWave20 && !profile.RevengeDone: 복수 보상(5.7), RevengeDone = true
  최고 웨이브(캐릭터별) 갱신, 비기 해금 기록, 기록 저장
```

- 정상 종료(사망, 판 포기) 모두 그때까지 직접 넘긴 웨이브 전부 정산. **죽은 웨이브는 넘긴 게 아니므로** 점수·보상 없음(그 웨이브에서 이미 지갑에 들어간 처치 골드는 지갑에 남는다 — 골드는 처치 순간 지갑 [임시]).
- 오프라인은 "검증 대기" 없음 → 즉시 확정(Status=Final). 같은 판을 두 번 정산하지 않도록 판 id로 한 번만 [임시].
- 정산 화면(`str.settlement.title`): 페이지 [가져가는 것 `str.settlement.page.takeaways` | 귀환 가방 | 기록 `str.settlement.page.record`]. 항목 열쇠: `str.settlement.take.gold`, `.materials`(확정 재료), `.bundle`, `.new_art`, `.revenge_reward`, `.bound`(계정에 묶임). 점수 내역 `str.settlement.score.kills/boss/clear/fast/no_hit/graze/phantom_first/skipped/variance/total`. 버튼 `str.settlement.again`, `str.settlement.to_enhance`(강화 화면으로), `str.settlement.to_menu`. 최고 기록 `str.settlement.best_record`.
- 튜토리얼: 하급 강화석을 처음 받은 정산(까마귀 보따리) 뒤 강화 화면을 한 번 안내 (G13).

### 5.5 오프라인 비정상 종료 — **미정**

원 명세의 "중간 지점(w%5==0) 기록 → 원정 정리로 마지막 중간 지점까지 정산"(S07 4.3)은 서버 절차이고, S09 10은 "오프라인 프로필의 판에는 이 절차가 없다. 기기가 정산한다"고만 적는다. 오프라인에서 강제 종료된 판의 처리 규칙은 **미정**.
제안 [솔로 임시 아님, 구현 판단 필요]: 중간 지점마다 판 요약(cleared, 지갑, 보유 야생 블록, 기회 목록)을 로컬 저장하고, 다음 실행 때 그 지점까지 자동 정산(가방 자동 담기). 이렇게 하면 원 규칙(마지막 중간 지점까지, 가방 자동, 중간 지점 없으면 보상 없음)과 같다.

### 5.6 시작 웨이브 고르기 (S07 10)

| 항목 | 규칙 |
|---|---|
| 값 | `skip ∈ {0,10,20,…}`, 첫 웨이브 = skip+1, 출발 야영에서 시작 |
| 한도 | `maxSkip = (best × 7 / 100) × 10`, best = **그 캐릭터로 넘긴 최고 웨이브(오프라인: 기기 기록)** |
| 열림 | best ≥ 15일 때만 선택지 표시(그 전엔 숨김) [조율 C-03] |
| 건너뛴 보상 | 없음(골드·블록·재료·가방 칸 0) |
| 출발 상태 | 체력 가득, 결정 0, 지갑 0, 출발 조립 그대로 |
| 표시 | `str.expedition_prep.start_wave.label`, `.option` "웨이브 {wave}", `.score_note` "건너뛰면 점수 {score}…", 기록에 `str.format.start_wave` "웨이브 {start}에서 출발" |

벡터: best 34 → {0,10,20}; 14 → 숨김; 15 → {0,10}; 28/29/43/100 → maxSkip 10/20/30/70 (TV-120~124). 첫 만남(10)은 건너뛸 수 없고, 복수 전에는 20도 못 건너뜀 — 한도에서 자동 성립.

주간 도전(오프라인): 게임에 든 씨앗 목록에서 주마다 하나(`index = (ISO연도×53 + ISO주) mod 길이`), 웨이브 1 고정, 웨이브 10은 언제나 가마등, 첫 만남을 치른 프로필만, 기기 기록만·보상 없음 (S09 14.1). 그 주 4번째 판부터 가방 0칸 규칙의 오프라인 적용 여부는 **원문 명시 없음**(S07 13.4는 "주간 도전 판" 전체에 건다 → 적용으로 읽음). 솔로에서 주간 도전을 넣을지는 구현 판단.

### 5.7 첫 만남·복수 (S07 8, S09 11)

- 프로필 필드: `PhantomNotMet=true`(새 프로필), `RevengeDone=false`, `CrowBundleReceived=false`, 캐릭터별 `BestClearedWave`, 추적기용 `PhantomAttempts`, `BestScratchPct`, `RunsToRevenge`.
- 첫 만남 수치: 허상 체력 4000(바 50%에서 멈춤), 최대 45초(2,700틱), 격노 5스택 시작·최종 격노 없음, 졸개 없음, 점수 고정 **3,648**, 골드 클리어 20만(+결정 환전), 카드 4장 중 2장(확정 칸 없음), 재료 하급 1 확정만, 웨이브 10을 직접 넘긴 것으로 셈.
- 표시 끄기: 웨이브 10을 넘긴 것으로 **정산된** 판. 첫 만남 도중 판 포기 → 표시 유지, 웨이브 1~9 정산 (TV-305).
- 복수: 웨이브 20 허상(체력 7,111)을 **처음** 잡은 판의 정산에서 1회 — 칭호 `title.revenge.mirror_breaker`("거울을 깬"), 머리 장식 `cosmetic.headwear.cracked_mirror_shard`(계정 귀속), 귀속 재료 중급 3·상급 1·부적 1(강화에서 먼저 쓰임). 보스 처치 뒤 그 판에서 죽어도 받음. 웨이브 20 보스 처치 보상(580골드, 카드 5, 표 C 2일째)은 별도 (TV-309, 310).
- 추적기 문구: `revenge.tracker.*`, HUD `revenge.tracker.hud` "허상까지 {n}웨이브"(웨이브 11~19), 웨이브 20 허상 이름 옆 `revenge.tag`. "가장 큰 흠집" = floor(입힌 피해×100 ÷ 허상 최대 체력), 첫 만남은 세지 않음. 진행 = `min(전 캐릭터 최고 웨이브, 19) / 19`.
- 대사: `phantom_first.master_line`, `revenge.master_line`.

---

## 6. 점수 (S07 12, 10.4)

### 6.1 웨이브 점수 (직접 넘긴 웨이브만)

```
if (PhantomFirst && w==10) return 3648
KillRaw = Σ cost×10 (엘리트 cost×3×10, 지체 120초 뒤 cost×5; 졸개·낳은 개체 0)
raw60 = 60 × (KillRaw + (보스?3000:0) + 200 + (무피격?100:0) + 5×min(스침, 보스?40:20))
      + 2 × max(0, (보스?7200:3600) − elapsedTicks)
score = raw60 × (9 + w) / 600                // 한 번에 내림
foreach 변덕: score = score × pct / 100      // 115/120/125, 차례로 내림
RunScore = Σ WaveScore + SkippedScore(skip)
```
- 무피격 = 웨이브 동안 체력이 한 번도 안 줄었음 [임시]. 보스 걸린 시간 = BossIntro 시작부터.
- 최종 격노(보스 전투 180초) 이후 스침 0. 첫 만남 중 스침·피해 점수 0. 야영 시간 무관.
- 벡터: TV-141(w1 → 535), TV-142(w23 몰려옴 → 5,740), TV-143(w45 두 변덕 → 18,008), TV-147(w30 보스 → 13,806), TV-148(지체 → 2,070).

### 6.2 건너뛴 점수

```
SkippedScore(skip) = Σ_{w=1..skip} ( (보스? 3200 : Budget(w)×10+200) × (9+w) × 6 / 100 )  // 웨이브마다 내림
```
skip 10/20/30/40/50 → 8,398 / 28,859 / 64,764 / 120,409 / 195,829 (TV-126).

점수 기록(오프라인 기기 기록): 판 점수, 도달 웨이브, 출발 웨이브, 캐릭터, 지팡이 단계, 웨이브별 점수 내역 (`str.records.*`).

---

## 7. 화면·키 요약

| 장면 | 키/조작 | 이름 열쇠 |
|---|---|---|
| 전투 HUD | — | `str.battle_hud.day_wave`, `tod.*`, `str.battle_hud.wallet`, `.score`, `.kills`, `.notice.wave_clear`, 보스 `.boss.rage`/`.boss.final_rage` |
| 전리품 | 카드 2장 클릭 → 확인, 다시 뽑기 | `str.loot_pick.*` |
| 모닥불 | 옆에서 **J** = 야영 화면 | `str.camp.title` |
| 야영 화면 | 탭 조립/비기, 체력 회복, 허수아비 연습, [출발] | `str.camp.*` |
| 출발 표식 | **J 1초(60틱) 유지** | `str.camp.depart_hint` |
| 일시정지 | (키는 조작 요약서 몫) "판 포기" | `str.pause.give_up`, `str.pause.give_up_confirm` |
| 원정 준비 | 캐릭터·지팡이·출발 조립·시작 웨이브 | `str.expedition_prep.*` |
| 정산 | 가방 고르기 → 확인 | `str.settlement.*` |

---

## 8. 미정·모순 목록

### 8.1 모순(우선순위로 정한 것)
1. **야영 기능**: D2.3 "조립·회복·비기 선택만" vs G2.2·G13·monsters 4.6 "허수아비 연습, 다시 뽑기도". → D 우선: 연습은 조립 탭 하위, 다시 뽑기는 전리품 화면에서만 (S07 C-02, C-08).
2. **id 표기**: S07 본문(modifier.*, frame.hunt, boss.mirror_master, boss.star_serpent, mat.charm_guard, block.mana_bolt 등) vs data·이름 표(whim.*, frame.hunting_party, boss.mirror_phantom, boss.night_serpent, charm.safeguard, block.magic_bolt 등). → data id 사용(0.2절).
3. **elite_count 20~29**: waves.json `from 20` vs S07 C-12 "21~29". w20은 보스라 결과 같음.
4. **점수 상한**: S07 12.3(그 웨이브 변덕에 조건부) vs S09 8.2(최대치로 잡음) — 솔로 제외라 무관.
5. **난수 흐름 번호**: S07 14.2 StreamTag(1~7) vs S09 14.1 stream_id(0~4) — 솔로 무관.
6. **체력 배율 표현**: waves.json은 실수(1.06), S07은 매 단계 내림 정수. 벡터는 정수식 기준.
7. **무료 회복 위치**: GDD 2판 "웨이브 11 뒤" vs 3판/monsters "보스 뒤 새벽 야영마다" → 후자.
8. **카드 등급 확률**: GDD 2판 70/25/5 고정 vs monsters 날별 → 날별 표.

### 8.2 미정 / [임시]
1. 드랍 표·야영 비용·가방·카드 확률을 담은 **data JSON이 없음** — S07 원문 값을 솔로용 JSON으로 새로 만들어야 함.
2. 오프라인 비정상 종료 처리 규칙 미정(5.5절 제안).
3. 서고 한도(100, 50개당 2,000골드)의 오프라인 적용 여부 명시 없음(적용 가정).
4. 오프라인 주간 도전의 "4번째 판부터 가방 0" 적용 여부 명시 없음(적용으로 읽음); "4번째 판" 셈 = 그 주(월요일 시작)에 시작한 주간 판 순번 [임시 U-19].
5. [임시] 값들: 종 기본 가중치 100(U-01), 틀 균등(U-02), 예산 배분 절차(U-03), 엘리트 종 선택(U-04), 묶음 나누기·첫 묶음 0틱(U-05), 스폰 변(U-06), 변덕 균등·중복 없음(U-08), 변덕 적용 순서·느린 탄막 내림·좁은 들·마지막 숨 세부(U-09), 점수 내림 위치(U-10), 무피격·보스 걸린 시간 정의(U-11), 죽은 웨이브 처치 점수 버림(U-12), 회복 비용 "방금 넘긴 웨이브"(U-13), 다시 뽑기 무제한 30 고정(U-14), 카드 중복 허용(U-16), 자동 가방 동률(U-17), 체력 조각 클리어 때 자동 회수(U-18), 지체 방지 보스 미적용(U-21), 같은 틱 사망 순서(U-25), 종 상한을 구성 기준으로 해석, 보스 처치 시 졸개 즉시 제거.
6. 개발자 확인 대기(지금 값 적용): 첫 만남 세부(Q-01), 복수 보상 구성(Q-02), 시작 웨이브 규칙(Q-04), 웨이브 6 빌린 블록(Q-05), 가방 크기(Q-06), 하급 3일째 미출현(Q-07), 강화석 드랍 시점(Q-08), 보스 골드 계수 200(Q-12), 허상 체력 기준 H(w)/H(10)(Q-13).
7. 비기 해금 차례의 판정 세부(후보 트리)는 비기 요약서 몫, 별똥 결정은 `starfall.json`(다른 요약서), R5 안전 경로 검사는 적 AI 요약서 몫.
