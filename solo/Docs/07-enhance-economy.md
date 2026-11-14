# 07. 지팡이 강화 · 경제 · 오프라인 프로필 — 솔로 구현 요약서

순정 Unity(MonoBehaviour, float 허용, 결정론·서버 없음) 솔로 구현용. 이 파일만 보고 강화 화면, 지팡이 개체, 재화, 상점, 오프라인 저장을 만들 수 있게 정리했다.

- 주 출처: `docs/spec/08-enhance-economy-profile.md`(이하 S8, 절 번호는 `(S8 4.2)`처럼). 보조: `docs/DECISIONS.md`(D), `docs/spec/13-ui-flows.md` 9절(S13), `docs/spec/04-characters-wands.md` 9~11절(S4), `docs/spec/07-waves-drops.md` 13.5절(S7), `docs/spec/14-content-ids-assets.md`(S14), `docs/dev/CONTRACTS.md`(C), `docs/wands/*.md`(W).
- 우선순위(CLAUDE.md 1절): D > spec > CONTRACTS > … 문서끼리 어긋나는 곳은 **13절 모순 목록**에 적고 고른 값을 본문에 썼다.
- 표기: [확정]=개발자 확정, [제안]=원문 설계안, [임시]=원문에 없어 명세가 정한 기본값, **미정**=값이 없음.
- 시간: 60틱 = 1초. 강화 화면은 전투가 아니므로 실시간(초)으로 돌려도 된다. 틱 값은 참고로 함께 적는다.

---

## 0. 솔로 범위 요약

| 포함 (오프라인 프로필) | 솔로 제외 (한 줄씩) |
|---|---|
| 강화(확률·비용·제물·부적·기운·대성공·변이·하락 바닥), 변이 제거, 슬롯 새기기, 흔적 공개 토글, 기댓값 표, 이어서·빠른 강화, 마일스톤 칭호 | 서버 전체 강화 알림(+10~+12, +13) — 오프라인은 `NoticeFor`가 항상 null, 결과 카드의 "서버 전체에 알렸다" 줄만 빠진다 |
| 지팡이 개체 여러 자루, 든 지팡이, 마지막 한 자루, 보관 목록 정렬·거르기, 떠나보내기(감정가 10% 환급) | 거래·장터·우편·선물·수수료·보증금 (S8 2.7, 6.5, 10.2~10.3) |
| 골드 상점(부적 주 3개, +0 지팡이 500, 서고 확장) — **기기 시계** 기준 주간 한도 | 결투·리그·IL·결투 강화 배율 1+0.03n (S8 10.4, 4.13 결투 열) |
| 재화: 골드, 하급·중급·상급 강화석, 하락 방지 부적, 서고 블록 | 시즌 주화, 시즌 상점, 별빛(유료), 결제 뼈대 전체 (S8 11절) — 코드에 넣지 않는다 |
| 오프라인 저장 파일(원자적 쓰기, 3세대, 체크섬) | 서버 검증 대기(`pending_run_id`, `qty_pending`), 갚을 것(`account_debt`), 멱등 키 DB, `enhance_log` DB, 닉네임 변경 |
| 원정 지갑·결정 환전·웨이브 골드·재료 추첨(정산 때 기기 난수) | 광장 행상 수레(오프라인은 메인 메뉴 상점만) |

**결제/서버로 옮기기 금지**: 오프라인 진행을 서버로 올리는 호출·파일 업로드를 만들지 않는다(D2.6, S8 8.2). 나중에 멀티를 붙일 때도 오프라인 프로필은 따로다.

---

## 1. 식별자 (정본 id)

CONTRACTS 0.3 / 2.1 대응표가 S8의 옛 id(`char_1`, `stone_low`, `charm_guard`, `giant_fireball` …)를 정본으로 바꾼다. **저장 파일·데이터·코드에서는 정본 id만 쓴다.**

| 대상 | S8 표기 | 정본 id (사용) | 이름 표 열쇠 |
|---|---|---|---|
| 캐릭터 | `char_1`~`char_5` | `char.1`~`char.5`, 정수 1~5 | `char.1`~`char.5` ("제자 1"~"제자 5") |
| 전용 지팡이 종류 | `wand_char_N` | `wand.1`~`wand.5` | `wand.N` (본 이름), `wand.N.stage.K.nickname` (단계 별명 70개) |
| 하급/중급/상급 강화석 | `stone_low/mid/high` | `mat.stone_low`, `mat.stone_mid`, `mat.stone_high` | 같은 열쇠 |
| 하락 방지 부적 | `charm_guard` | `charm.safeguard` | `charm.safeguard` |
| 블록 | `magic_bolt`, `giant_fireball`, `split_2`, `split_3`, `pierce_2`, `overheat_x3`, `erosion` … | `block.magic_bolt`, `block.fireball`, `block.split_two`, `block.split_three`, `block.pierce`, `block.triple_heat`, `block.corrosion` … (전체는 `data/sim/blocks.json`) | `block.*` |
| 속성 | Fire, Lightning, Arcane, Frost, Dark, None | `element.fire/lightning/arcane/ice/dark/neutral` | `element.*` |
| 변이 | `TwistedBarrel` … | `MutationId` 1~6: 1 `mutation.twisted_barrel`, 2 `mutation.unstable_core`, 3 `mutation.hypersensitive_circuit`, 4 `mutation.gluttony`, 5 `mutation.mirror_image`, 6 `mutation.glass_wand` (C 1절, S14) | 같은 열쇠 |
| 칭호 | `title.stage.<char>.<band>` | `title.stage.N.b0`~`b4` (b0 +0~3, b1 +4~6, b2 +7~9, b3 +10~12, b4 +13) | 같은 열쇠. b4는 틀 `엄청엄청 위대한 {char.N}` |
| 상점 품목 | sku | `charm.safeguard`, `item.wand_box`(+0 새 지팡이), `item.library_expansion` | 같은 열쇠 |
| 까마귀 보따리 | | `item.crow_bundle` | 같은 열쇠 |

캐릭터 ↔ 속성 ↔ 지팡이 (S8 1.2, S14 3.2, `data/sim/characters.json`):

| id | 속성 | 지팡이 | 기본 연결 슬롯 (`base_link_slots`) | 변이 상한 (`max_mutations`) | 작업용 이름(코드 금지) |
|---|---|---|---|---|---|
| char.1 | 화염 | wand.1 | 4 | 2 | 달구 |
| char.2 | 번개 | wand.2 | 4 | **3** | 초롱 |
| char.3 | 비전 | wand.3 | **5** | 2 | 갈피 |
| char.4 | 냉기 | wand.4 | 4 | 2 | 성에 |
| char.5 | 암흑 | wand.5 | 4 | 2 | 그믐 |

`data/sim/characters.json`의 `characters[i].base_link_slots`, `characters[i].max_mutations`를 그대로 읽는다(단위: 칸 수, 개수). 지팡이 속성 = 캐릭터 속성.

제물 점수용 블록 열 (`data/sim/blocks.json` → `blocks[]`의 `id`, `kind`(projectile/control/modifier), `rarity`(common/rare/hero), `element`, `flags`):

| id | kind | rarity | element | 비고 |
|---|---|---|---|---|
| block.magic_bolt | projectile | common | arcane | 시작 블록 |
| block.fireball | projectile | rare | fire | `flags: ["explodes"]` → "폭발 블록"(불안정 핵 출현 조건) |
| block.chain_lightning | projectile | hero | lightning | |
| block.boomerang | projectile | rare | arcane | |
| block.ice_spike | projectile | rare | ice | |
| block.shadow_orb | projectile | rare | dark | |
| block.split_two | control | common | neutral | |
| block.split_three | control | rare | neutral | |
| block.on_hit_trigger | control | rare | neutral | |
| block.wall_bounce | modifier | common | neutral | |
| block.double_size | modifier | common | fire | |
| block.pierce | modifier | common | arcane | |
| block.frost | modifier | common | ice | |
| block.homing | modifier | hero | lightning | |
| block.triple_heat | modifier | hero | fire | |
| block.corrosion | modifier | hero | dark | |

등급 개수: 일반 6, 희귀 6, 영웅 4. 냉기·암흑 블록이 있어 다섯 지팡이 모두 같은 속성 보너스를 받을 수 있다(S8 4.8).

---

## 2. 강화 수치의 자리 (데이터 파일)

- **현재 `data/` 아래에 강화·경제 수치 파일은 없다.** `data/rules/`에는 `net.json`(넷코드)만 있고, `data/sim/`에도 강화 확률·비용 표가 없다(전체 검색 결과). 관련된 것은 `characters.json`의 `base_link_slots`·`max_mutations`와 `blocks.json`의 `kind`·`rarity`·`element`·`flags`뿐이다.
- `src/Wizard.Content/Rules/Defs/EnhanceTable.cs`는 주석만 있는 빈 클래스, `src/Wizard.Rules/Enhance/*.cs`는 계약용 빈 껍데기(`WandState`, `ProbBreakdown` 필드 없음, `IEnhanceRules` 인터페이스만)다. **참고할 실제 강화 구현은 없다.** 유일한 실구현은 강화 배율 표 `src/Wizard.Core/Generated/PowTables.g.cs:10`(`Enhance115`, Q32.32)이다.
- 따라서 솔로 구현은 S8 6.9절의 `config` 키를 그대로 옮긴 새 JSON을 만든다. 권장 경로 `solo/…/StreamingAssets` 또는 `Resources/enhance.json` (경로 미정, 구현자가 정함). 내용(값과 단위):

```
enhance.base_prob = [100,90,75,60,50,40,32,25,18,12,8,5,3]   # %, 인덱스 = 현재 단계 0..12 (도전 +1..+13)
enhance.prob_cap_pct = 95            # % (도전 +1은 예외 100)
enhance.great_ratio_pct = 15         # 성공 확률 중 대성공 비율 %
enhance.mutation_ratio_pct = 25      # 실패 확률 중 변이 비율 %
enhance.aura_step_pct = 15 ; enhance.aura_max = 3     # 기운 1개당 %p, 최대 개수
enhance.drop_safe_max_stage = 3 ; enhance.floor_gap = 2   # 이 단계 이하는 하락 없음, 바닥 = floor_peak − 2
enhance.cost_coeff = 100            # 골드 = 100 × (현재 단계 + 1)^2
enhance.stone_tier_bounds = [4, 8]  # 현재 단계 ≤4 하급, ≤8 중급, 그 밖 상급
enhance.stones_per_attempt = 1      # 시도당 강화석 개수
enhance.slot_extra_max = 3          # 지팡이당 평생 확장 칸
enhance.mutation_cap_default = 2 ; enhance.mutation_cap.char_2 = 3   # (characters.json max_mutations와 같은 값)
enhance.great_slot_min_stage = 0    # [임시] 대성공이 슬롯을 주는 최소 단계(슬롯 낚시 막는 손잡이, 기본 0 = 제한 없음, S8 15 Q-12)
sacrifice.max_blocks = 5 ; sacrifice.score = {common:4, rare:8, hero:14} ; sacrifice.same_element = 5 ; sacrifice.cap = 40   # %p
carve.min_stage = 7 ; carve.base_pct = 40 ; carve.step_pct = 20 ; carve.gold_coeff = 20000 ; carve.high_stones = 2
mutation.remove_gold = 3000
shop.charm_gold = 10000 ; shop.charm_weekly_limit = 3
shop.wand_plus0_gold = 500
shop.library_step_blocks = 50 ; shop.library_step_gold = 2000 ; library.base_cap = 100
appraisal.wand_factor = 0.5 ; appraisal.wand_plus0 = 250 ; release.refund_pct = 10
crystal.gold_each = 5
# 솔로 제외: shop.nickname_change_gold, fee.*, market.*, trade.*, season_coin.*, notice.*, payments_enabled
```

강화 배율 표(전투가 읽는 값)도 같은 파일에 상수 배열로 둔다(4.10절).

---

## 3. 지팡이 개체 (Wand)

### 3.1 필드 (S8 2.1) — 솔로에 필요한 것

| 필드 | 타입 | 범위 | 뜻 |
|---|---|---|---|
| `id` | long | 유일 | 개체 번호(프로필 안에서 증가) |
| `character` | int 1~5 | 불변 | 이 지팡이를 쓰는 캐릭터. 그 캐릭터만 쓴다(D2.4) |
| `stage` | int | 0~13 | 강화 단계 |
| `floor_peak` | int | `stage ≤ floor_peak ≤ min(13, stage+2)` | **하락 바닥 계산용** 역대 최고 |
| `record_peak` | int | `≥ stage` | **흔적용** 역대 최고(거래해도 남음. 솔로에선 floor_peak과 항상 같게 움직인다) |
| `drop_count` | int | ≥0 | 하락 횟수 |
| `aura` | int | 0~3 | 장인의 기운 |
| `slots_extra` | int | 0~3 | 확장 연결 슬롯(평생) |
| `carve_bonus` | int | 0~3 | 슬롯 새기기 같은 칸 누적 실패 수 |
| `mutations` | List<(MutationId, seq)> | 길이 ≤ 상한 | 받은 순서(앞이 가장 오래된 것) |
| `pending_offer` | MutationOffer? | | 결정 대기 중인 변이 제시 [임시 U-04] |
| `loadout` | 코어 1칸 + 연결 칸(4/5 + slots_extra) | | 출발 조립 |
| `skin_id` | id? | | 덧씌운 겉모습(솔로: 옷장 있으면) |
| `show_scars` | bool | 기본 false | 흔적 공개 |
| `origin` | Starter/Shop/(Trade 솔로 없음) | | |
| `acquired_at` | 시각 | | "최근 얻은 순" 정렬 |
| `lock_state` | None / Expedition | | 솔로는 원정 중 잠금만 의미 있음 (Duel/Market/Mail/Trade 솔로 제외) |

`MutationOffer { attemptId, candidate, wouldPushOut?, fallbackOutcome(FailDrop/FailCharmBlocked/FailNoDrop), fallbackStageAfter, fallbackCharmConsumed }` (S8 2.1).

불변식 (S8 2.1): `0≤stage≤13`, `stage≤floor_peak≤min(13,stage+2)`, `record_peak≥stage`, `mutations.Count ≤ cap(character)`, `slots_extra≤3`, `carve_bonus≤3`, 같은 블록 개체를 두 지팡이에 동시에 끼우지 못함(서고 수량에서 "끼움"으로 따로 셈).

### 3.2 새 지팡이 (S8 2.2)

| 경우 | 상태 |
|---|---|
| 새 프로필 시작 지팡이(캐릭터마다 1, 합 5) | +0, peaks 0, 하락 0, 변이·확장·기운 0, origin Starter, **코어에 마력탄 1개**, 연결 칸 빔 |
| 상점 +0 (500골드) | +0, 같은 0 상태, **빈 조립**(코어도 빔), 겉모습 없음, origin Shop |

코어가 빈 지팡이로는 원정을 시작할 수 없다 → 원정 화면이 조립 화면으로 보낸다(S8 2.2).

### 3.3 개수, 든 지팡이, 마지막 한 자루 (S8 2.3)

| 규칙 | 값 |
|---|---|
| 한도 | **없음** [확정 D1.22]. 캐릭터당·프로필 전체 모두 |
| 최소 | 캐릭터당 1자루. 마지막 한 자루는 떠나보낼 수 없다(`LastWand`) |
| 든 지팡이 | 캐릭터마다 1자루(`characters[].equipped_wand_id`). 강화 화면·원정 화면에서 바꾼다 |
| 든 지팡이를 떠나보냈을 때 | 남은 것 중 단계 최고 → 동률 record_peak 높은 순 → acquired_at 최근 순 [임시 U-19] |
| 원정 | 한 자루만 들고 나가고 판 중엔 바꾸지 않는다. 판 중에는 강화 화면 자체를 열 수 없다(솔로) |

### 3.4 보관 목록 `WandListModel` (S8 2.5, S13 5.3·9.2)

- 캐릭터 탭 5개, 탭마다 "n자루"(`str.wand_list.count`). 세로 스크롤.
- 정렬: 단계 높은 순(기본, `str.wand_list.sort.stage_desc`) / 단계 낮은 순(`stage_asc`) / 최근 얻은 순(`recent`) / 아이템 레벨 높은 순(`il_desc` — 솔로는 IL 계산이 리그용이라 **미정**: 넣으려면 S8 10.4 장착 IL식 사용). 어느 정렬이든 **든 지팡이가 맨 위 고정**. 고른 정렬은 기기에 기억.
- 거르기: 변이 있음 / 확장 슬롯 있음 / 거래 잠금(솔로 무의미) / +0만 (`str.wand_list.filter.*`).
- 줄 데이터: 아이콘, 이름, 단계, 변이, 슬롯, 기운. [든 지팡이로](`str.wand_list.set_held`) / 꼬리표 "든 지팡이"(`str.wand_list.held_tag`), [떠나보내기](`str.wand_picker.release`).
- 강화 화면 안에서 **Q / E**로 지팡이를 바꾼다.

### 3.5 떠나보내기 (S8 2.6, S13 9.2)

| 항목 | 값 |
|---|---|
| 결과 | 지팡이 삭제(되돌릴 수 없음). 끼운 블록 → 서고, 덧씌운 겉모습 → 옷장 |
| 돌려받는 골드 | 감정가(6.4절)의 **10%, 내림**. **+0은 0골드** |
| 못 하는 때(솔로) | 캐릭터의 마지막 한 자루, 원정 중, 변이 결정 대기(`MutationPending`) |
| 확인 | 한 번. **+7 이상은 지팡이 이름(`wand.N` 표시 이름)을 그대로 입력**해야 단추가 켜짐(앞뒤 공백 제거 후 비교 [임시]). 아니면 `NameConfirmRequired` |
| 여러 자루 | **+3 이하 && 변이 0 && 확장 0**인 것만 체크 가능. 고른 묶음을 빼고도 캐릭터마다 1자루 이상 남아야(그 지팡이만 체크 불가로 표시 U-30). 확인 한 번, "n자루 · 합계 g 골드" |
| 이름 표 | `str.wand_release_confirm.title/confirm/irreversible/refund/returns/type_name/multi_summary`, `str.wand_picker.release_multi/release_summary`, `str.wand_picker.reason.last_wand` |

---

## 4. 강화 규칙

### 4.1 진입과 전제 (S8 3, S13 9.1)

- 강화는 **전용 강화 화면에서만** [확정 D1.7]. 솔로 진입로: 메인 메뉴 "강화"(`str.main_menu.item.enhance`), 정산 3쪽의 [강화 화면으로](`str.settlement.to_enhance`). Esc → 들어온 곳으로. 판 중·야영 중엔 열 수 없다(야영은 조립·회복·비기 선택만).
- 확률·비용·실패 규칙은 다섯 지팡이가 똑같다 [확정 D2.4]. 지팡이 특성이 강화에 영향 없음.
- 난수: **판 씨앗을 쓰지 않는다.** 기기 난수 하나(`roll ∈ [0,10000)` 정수 균등). Unity면 `System.Security.Cryptography.RandomNumberGenerator.GetInt32(0,10000)` 또는 `UnityEngine.Random.Range(0,10000)` 모두 가능(S8 U-05는 CSPRNG 권장).

시도 전제 `ValidateAttempt(wand, sacrifices, useCharm)` — 하나라도 어기면 거절 (S8 3, 13):

| # | 조건 | 오류 코드 | 화면 문구 열쇠 |
|---|---|---|---|
| 2 | `stage < 13` | `MaxStage` | (+13이면 강화 버튼 꺼짐, U-22) |
| 3 | 지팡이가 원정 중 아님 | `WandLocked` | `error.wand_locked` |
| 4 | `pending_offer == null` | `MutationPending` | `error.mutation_pending` |
| 5 | 골드 ≥ `Cost(stage)` | `NotEnoughGold` | `error.not_enough_gold` |
| 6 | 구간 강화석 ≥ 1 | `NotEnoughStone` | `error.not_enough_stone` |
| 7 | 제물 0~5개, 전부 서고(끼우지 않은 것), 수량 충분 | `InvalidSacrifice` | `error.invalid_sacrifice` |
| 8 | 제물을 빼도 프로필 전체(서고+조립)에 투사체 블록이 1개 이상 남는다 | `LastProjectile` | 칸에 "놓을 수 없음"(`str.enhance.cannot_place`) + `str.enhance.last_projectile_tip` |
| 9 | `useCharm`이면 부적 ≥1 **그리고** `DropPossible`(4.4) | `NotEnoughCharm` / `CharmNotNeeded` | `error.not_enough_charm` / `error.charm_not_needed` |

(1번 `NotOwner`, 10번 갚을 것은 솔로 제외.)

### 4.2 확률 (S8 4.1·4.2·4.8) [확정 표 / 제안 그 밖]

기본 확률표 [확정 D1.18]:

| 현재 stage | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 도전 | +1 | +2 | +3 | +4 | +5 | +6 | +7 | +8 | +9 | +10 | +11 | +12 | +13 |
| 기본 % | 100 | 90 | 75 | 60 | 50 | 40 | 32 | 25 | 18 | 12 | 8 | 5 | 3 |

```
int SuccessPct(wand, sacrifices):
    if wand.stage == 0: return 100                         // +1 도전은 예외로 100%, 대성공 없음
    sac  = SacrificeBonus(sacrifices, wandElement)         // 0..40
    aura = 15 * wand.aura                                  // 0/15/30/45
    return min(95, BaseProb[stage] + sac + aura)           // 상한 95

int SacrificeBonus(blocks, wandElement):
    gradeSum = Σ score(b.rarity)                            // common 4, rare 8, hero 14
    elem     = any(b.element == wandElement) ? 5 : 0        // 시도당 1회
    return min(40, gradeSum + elem)                         // 상한은 등급+속성 "합계"에 건다 (S8 C-01, 13절 모순 M-1)
```

- 하한 없음(기본 ≥3).
- 표시 내역(`ProbBreakdown`): 기본 / 제물 합 / 같은 속성 / 기운 4줄(`str.enhance.prob.base`, `.sacrifice`, `.same_element`, `.aura`). "제물 합" 줄에는 `min(40, gradeSum+elem) − elem`, "같은 속성" 줄에는 `elem`을 적어 두 줄 합이 40을 넘지 않게 한다[임시]. 95에 닿으면 숫자 옆 "상한"(`str.enhance.prob.cap`). 큰 숫자는 바뀔 때마다 0.2초(12틱) 굴러서 바뀐다.
- 제물: 서고의 블록 최대 5개(끼운 것 불가). 같은 속성 블록에는 속성 문양 점. 제물은 시도 때 결과와 무관하게 사라진다(U-18).

### 4.3 비용 (S8 4.3) [제안]

```
long Cost(stage) = 100 * (stage + 1)^2        // 현재 단계 기준 골드
StoneTier StoneTierFor(stage) = stage <= 4 ? Low : (stage <= 8 ? Mid : High)   // 도전 +1~+5 하급 / +6~+9 중급 / +10~+13 상급, 1개
```

| 현재→도전 | 0→1 | 1→2 | 2→3 | 3→4 | 4→5 | 5→6 | 6→7 | 7→8 | 8→9 | 9→10 | 10→11 | 11→12 | 12→13 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 골드 | 100 | 400 | 900 | 1,600 | 2,500 | 3,600 | 4,900 | 6,400 | 8,100 | 10,000 | 12,100 | 14,400 | 16,900 |
| 강화석 | 하 | 하 | 하 | 하 | 하 | 중 | 중 | 중 | 중 | 상 | 상 | 상 | 상 |

- 비용(골드·강화석·제물)은 **시도 순간 즉시** 빠지고 결과와 무관하게 돌아오지 않는다(대성공·실패·변이 모두) [임시].
- 귀속 수량(`qty_bound`, 묶임)이 있으면 **귀속부터** 쓴다(S8 4.3). 솔로에서 귀속 재료는 복수 보상(중급3·상급1·부적1, S7)뿐이고 거래가 없으니 차이는 표시("(묶임 n)" `str.format.bound_count`)뿐이다.
- 부적은 결과 뒤에 빠진다(막았을 때만).

### 4.4 하락 바닥·하락 가능 (S8 4.6)

```
bool DropPossible(wand) = wand.stage > 3 && (wand.stage - 1) >= wand.floor_peak - 2
표시 바닥 = max(3, floor_peak - 2)            // "바닥 +N" [임시]. 예: 역대 최고 +8 → 바닥 +6
```

실패 안내 한 줄 (강화 버튼 위):

| 조건 | 문구 열쇠 |
|---|---|
| `stage ≤ 3` | `str.enhance.hint.no_drop_low` "실패해도 내려가지 않는다(+3 이하)" |
| `stage > 3 && !DropPossible` | `str.enhance.hint.at_floor` "바닥이다(역대 최고 +{best})" (best = floor_peak) |
| `DropPossible && !useCharm` | `str.enhance.hint.drop_to` "실패하면: +{n}{(으)로} 하락" (n = stage−1) |
| `DropPossible && useCharm` | `str.enhance.hint.charm_protects` "부적이 막아 준다" |

- 하락 가능하고 부적을 안 끼웠으면 강화 버튼에 "하락 가능"(`str.enhance.drop_possible`, 호박색) 꼬리표. **확인 창은 띄우지 않는다.**
- `!DropPossible`이면 부적 칸이 접히고 "지금은 필요 없다"(`str.enhance.charm_not_needed`).

### 4.5 판정 — 난수 한 번 (S8 4.4)

```
p_bp     = SuccessPct * 100
great_bp = (stage == 0) ? 0 : SuccessPct * 15           // 성공 확률의 15%
mut_end  = p_bp + (10000 - p_bp) / 4                    // 실패의 25%가 변이 구간
roll     = Random[0, 10000)
roll < great_bp        → GreatSuccess
roll < p_bp            → Success
roll < mut_end         → 변이 후보 추첨(4.7). 후보 없으면 일반 실패로 처리 [임시 U-03]
else                   → 일반 실패 → DropPossible/부적으로 FailDrop / FailCharmBlocked / FailNoDrop
```

- 판정은 **미만**(`roll < p_bp`). 결과 카드 "뽑힌 값 {roll/100, 소수 둘째} / {p:.2} 미만이면 성공"(S8 C-07, 13절 M-3).
- 결과는 버튼을 누른 순간 정해진다. 연출은 결과를 바꾸지 않는다. 충전 길이·소리는 결과와 무관하게 같다.
- 결과 띠 4구간(실제 비율 bp): 대성공(금) / 성공(속성색) / 변이(연두) / 하락 또는 하락 없음(회색).

### 4.6 결과 분기 (S8 4.5)

| 분기 | 단계 | 기운 | 확장 슬롯 | 부적 | 그 밖 |
|---|---|---|---|---|---|
| 대성공 | `min(13, stage+2)` | 0 | `slots_extra<3`이면 +1(평생 3). 이미 3이면 `slot_already_full` ("슬롯은 이미 다 자랐다" `str.enhance.line.slot_full`) | 소모 안 함(칸에 남음, U-17) | +1 도전에선 없음. +12에서 나오면 +13에서 멈춤 |
| 성공 | `stage+1` | 0 | | 소모 안 함 | |
| 실패, 하락 | `stage−1` | `min(3, aura+1)` | | 없음 | `drop_count+1` |
| 실패, 부적이 막음 | 그대로 | **그대로** | | **1개 소모** | |
| 실패, 하락 없음 | 그대로 | `min(3, aura+1)` | | 소모 안 함(칸 접힘) | +3 이하 또는 바닥 |
| 변이 받음 | 그대로 | `min(3, aura+1)` | | 소모 안 함(부적은 변이를 막지 않는다) | 변이 추가, 넘치면 가장 오래된 것 밀어냄 |
| 변이 거절 | 일반 실패와 같다(하락 / 부적이 막음 / 하락 없음) | 그 분기대로 | | 그 분기대로 | |

- 성공·대성공 뒤 `floor_peak = max(floor_peak, stage)`, `record_peak = max(record_peak, stage)`. record_peak를 새로 쓰면 `record_peak_updated = true`.
- 기운은 단계가 바뀌어도 유지되고 지팡이마다 따로다. 다른 자루로 넘어가지 않는다.

상태 기계(S8 4.5):

```
[대기] ─시도→ [판정] ─GreatSuccess→ 단계+2(≤13), 슬롯+1(≤3), aura=0 → [결과]
                     ─Success→ 단계+1, aura=0 → [결과]
                     ─MutationRoll→ [변이 제시: 결정 대기 pending_offer]
                           ├ Accept → 단계 유지, 변이 추가, aura+1 → [결과]
                           └ Decline → fallback(일반 실패 분기) → [결과]
                     ─Fail→ DropPossible?
                           ├ 아니오 → FailNoDrop: 단계 유지, aura+1
                           ├ 예 + 부적 → FailCharmBlocked: 단계 유지, 부적 −1, aura 그대로
                           └ 예 + 부적 없음 → FailDrop: 단계 −1, drop_count+1, aura+1
```

변이 구간이 나오면 **그 자리에서 fallback(거절 때의 결과)까지 계산**해 `pending_offer`로 저장한다. 결정 전에 게임을 꺼도 다음에 강화 화면을 열면 변이 카드부터 보인다(저장 파일에 남음). 결정 대기 중인 지팡이는 강화·새기기·변이 제거·떠나보내기 불가, 원정은 가능(판 중엔 변이 없는 상태로 싸움, U-04·U-26).

### 4.7 변이 (S8 4.9) [제안]

| 규칙 | 값 |
|---|---|
| 발생 | 실패 중 25% |
| 결정 | 받거나 거절. 시간 제한 없음, 변이 카드는 건너뛸 수 없다 |
| 상한 | 2개, char.2만 3개. 넘치면 **가장 오래된 것**을 밀어냄 |
| 생기는 길 | 강화 실패로만 |
| 후보 | 1개. 6종 중 출현 조건 만족 + **이미 가진 것 제외**에서 균등 추첨 [임시 U-02·U-03]. 목록이 비면 일반 실패 |
| 출현 조건 | 불안정 핵만: 지팡이 **코어 블록이 폭발 블록**(`flags`에 `explodes` = `block.fireball`)일 때 |
| 카드 | 이름(`mutation.*`), 이득, 대가, [받는다](`str.enhance.mutation.accept`) [거절한다](`str.enhance.mutation.reject`). 가득이면 `str.enhance.mutation.push_out` "가장 오래된 {mutation}{이/가} 밀려난다" |

```
MutationId? DrawCandidate(wand, rng):
    pool = All6.Where(m => m != UnstableCore || CoreIsExplosive(wand.loadout))
               .Where(m => !wand.mutations.Contains(m))
    return pool.Count == 0 ? null : pool[rng(0, pool.Count)]

ResolveMutation(wand, decision):
    if Accept: if count == cap: pushed = RemoveAt(0);  Add(candidate);  aura = min(3, aura+1);  부적 소모 없음
    else:      fallback 결과 그대로 적용
    pending_offer = null
```

변이 6종 데이터 (전투 효과는 전투 문서. 이득/대가 설명 문구 열쇠는 이름 표에 **없음 → 미정**):

| 번호 | id | 이득 | 대가 | 출현 조건 |
|---|---|---|---|---|
| 1 | twisted_barrel 뒤틀린 총열 | 변이 배율 ×1.6 | 탄도 ±25도 흔들림 | 없음 |
| 2 | unstable_core 불안정 핵 | 폭발 반경 +100% | 발사 1회당 5% 확률로 내 자리에서 터져 잠금 2초(120틱) + 콤보 0 | 코어가 폭발 블록 |
| 3 | hypersensitive_circuit 과민 회로 | 임계 구간 70부터 | 과열 잠금 +1.5초(90틱) | 없음 |
| 4 | gluttony 탐식 | 처치 시 열 −4 (초당 최대 −20, 60틱 창 합) | 냉각 속도 절반 | 없음 |
| 5 | mirror_image 거울상 | 뒤로도 동시 발사(J 발사·마력 방출만, 64발 상한 포함) | 발사당 열 +50% | 없음 |
| 6 | glass_wand 유리 지팡이 | 임계 배율 +1 | 무한 웨이브: 피격 시 강화 −1(웨이브당 1회, +3까지). **판 안에서만**, 판 끝나면 원래 단계 | 없음 |

- 임계 시작을 바꾸는 효과가 여럿이면 가장 낮은 값(S8 4.9.2).
- 변이의 전투 난수(불안정 핵, 뒤틀린 총열)는 판 난수기, 강화 난수와 다르다.
- 유리 지팡이 판 안 하락은 지팡이 개체의 `stage`를 바꾸지 않는다(판 상태에만).

변이 제거 (S8 4.9.3): 강화 화면 [변이 제거](`str.enhance.remove_mutation`, 창 제목 `str.mutation_remove.title`). **1개당 3,000골드**. 고른 1개만 없어지고 단계·기운·슬롯·새기기 누적 변화 없음(U-23). 조건: 원정 중 아님, `pending_offer == null`. 골드 부족이면 `NotEnoughGold`.

### 4.8 슬롯 확장과 슬롯 새기기 (S8 4.10) [제안]

| 항목 | 값 |
|---|---|
| 기본 연결 슬롯 | 4칸, char.3만 5칸 (+ 코어 1칸 따로) |
| 확장 | 지팡이당 **평생 최대 3칸**(판당 아님). 하락해도 남는다 |
| 얻는 길 | 대성공(+1칸), 슬롯 새기기 |
| 새기기 대상 | 단계 **+7 이상**, 확장 3칸 미만. 아니면 `NotEligible` |
| 새기기 비용(1회) | **상급 강화석 2개 + `20,000 × (현재 확장 칸 수 + 1)` 골드** (20,000 / 40,000 / 60,000) |
| 확률 | `min(100, 40 + 20 × carve_bonus)` → 40 / 60 / 80 / 100%. 화면에 표시 |
| 실패 | 재료만 사라짐. 단계·기운 불변. `carve_bonus += 1` (실패마다 눈금 한 칸) |
| 성공 | `slots_extra += 1`, `carve_bonus = 0` [임시] |
| 판정 | `roll = Random[0,10000); success = roll < CarvePct*100` |
| 평균 | 칸 하나 1.888회. 0→3칸 평균 226,560골드 + 상급 11.33개 |
| 문구 | `str.enhance.engrave`, 창 제목 `str.slot_engrave.title`, `str.enhance.ext_slots` "확장 슬롯 {n}/{max}" |

### 4.9 하락 흔적 (S8 4.7, S13 9.11, S4 10.1) [확정: 공개는 본인 선택, 거래에선 항상 보임]

| 항목 | 규칙 |
|---|---|
| 흔적이 있는 조건 | `stage < record_peak` |
| 흔적 조각 수 | `min(2, record_peak − stage)` |
| 공개 설정 | `show_scars`, **지팡이마다**, 기본 끔. 이름 "흔적 공개"(`str.enhance.trace_public`). **강화 화면 왼쪽 칸에만** 있고 설정 화면엔 없다 |
| 내 화면 | 항상 보인다. 끔이면 흔적 옆 가림 표시(16×16) + 안내 `str.enhance.trace_hidden_note` "남에게는 안 보인다 · 거래에서는 항상 보인다" |
| 남의 화면 / 거래 | 솔로 제외(남이 없음). 단 값은 저장해 둔다(나중에 멀티) |
| 사라지는 때 | 역대 최고까지 다시 올리면 조각이 하나씩 떨어져 나감(성공 연출 0.40초 시점) |
| 하락 직후 | 결과 카드에 `str.enhance.trace_banner` "흔적이 남았다 · 지금은 {state}"({state} = `str.enhance.trace_state_hidden`/`_public`) + 바로 바꾸는 단추 |
| 판정 영향 | 없음(겉모습만) |

`scar_added` = 이번 하락으로 흔적이 생기거나 늘었는가.

### 4.10 강화가 전투 수치에 주는 효과 (S8 4.13, S4 9.2·9.3) [제안]

- **무한 웨이브 피해 배율 = 1.15^단계** (+13 ≈ 6.15배). 모든 비기, 패시브·고유 비기의 피해도 받는다(S4 9.2). 결투 배율 `1+0.03n`은 솔로 제외(봇 결투 연습을 만들면 그때 사용).

| 단계 | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1.15^n | 1.0000 | 1.1500 | 1.3225 | 1.5209 | 1.7490 | 2.0114 | 2.3131 | 2.6600 | 3.0590 | 3.5179 | 4.0456 | 4.6524 | 5.3503 | 6.1528 |

- 피해 공식에서의 자리(S4 9.2): `기본 × 강화 배율 × (1 + 변형 보너스 합) × 변이 배율 × 임계 배율 × 비기 배율 × 감쇠^깊이 × 속성 배율 × 대상 상태 배율 × 캐릭터 배율`. (변이 배율 = 뒤틀린 총열 ×1.6, 임계 배율 = (과열 3배 있으면 3, 없으면 2) + 유리 지팡이 1.)
- 단계가 게임플레이에서 바꾸는 것은 **피해 배율뿐**이다. 단계별 겉모습(모양, 꼬리, 섬광, 소리)은 판정·피해·탄 크기·속도·열을 바꾸지 않는다(S4 9.3).
- 확장 슬롯 = 연결 칸 수 증가(조립 화면 칸 수 = base_link_slots + slots_extra).
- 성공 연출 배율 표기 예 "×2.66 → ×3.06"(+7→+8).
- 유리 지팡이 판 안 임시 하락 시 배율도 내려간 단계로(예: +10, 임시 하락 1 → 1.15^9, S4 TV 108).

### 4.11 마일스톤과 칭호 (S8 4.16, S13 9.7)

| 단계 | 묶음 | char.1 | char.2 | char.3 | char.4 | char.5 |
|---|---|---|---|---|---|---|
| +0~+3 | b0 | 미지근한 | 졸린 | 책만 읽은 | 살짝 언 | 말 없는 |
| +4~+6 | b1 | 따끈한 | 깜빡이는 | 밑줄 친 | 서리 낀 | 그늘진 |
| +7~+9 | b2 | 펄펄 끓는 | 번쩍이는 | 주석 다는 | 꽁꽁 언 | 달 없는 밤의 |
| +10~+12 | b3 | 꽤 위대한 불꽃 | 꽤 위대한 벼락 | 꽤 위대한 책벌레 | 꽤 위대한 얼음 | 꽤 위대한 그림자 |
| +13 | b4 | 엄청엄청 위대한 {char.N} | 〃 | 〃 | 〃 | 〃 |

- 열쇠 `title.stage.N.bK`. 단계가 떨어지면 칭호도 내려간다.
- 마일스톤 = **+4, +7, +10, +13**. `record_peak`를 새로 쓰며 도달 → `first_time=true`(덧붙임 2.5초 = 150틱), 전에 닿았던 단계 → 0.8초(48틱) 칭호 띠만. 칭호 띠 문구 `str.enhance.milestone` "{from} → {to}".
- 대성공으로 마일스톤을 건너뛰면(+3→+5) 건넌 것도 도달로 본다. 여러 개면 가장 높은 것 하나만 연출(U-11).
- +13 전체 연출(9초 = 540틱)은 역대 최고 갱신일 때만, 다시 오르면 짧게(0.80~3.00초 구간). 처음 2초(120틱)는 건너뛸 수 없다.

### 4.12 결과 꾸러미 `EnhanceResult` (S8 4.11)

화면이 받는 유일한 입력. 필드: `attempt_id`, `outcome`, `stage_before`, `stage_after`(변이 제시면 = before), `record_peak_updated`, `slot_expanded`, `slot_already_full`, `charm_consumed`, `aura_before`, `aura_after`, `mutation_candidate`, `mutation_pushed_out`, `fallback {outcome, stage_after, charm_consumed, aura_after}`(변이 제시일 때만), `breakdown`, `roll_bp`, `success_bp`, `great_bp`, `mutation_end_bp`, `cost {gold, stone_tier}`, `milestone {stage, first_time}?`, `server_notice`(솔로 항상 null), `scar_added`, `wand`(갱신 상태), `profile_balances`(골드·강화석·부적 잔액). 변이 결정도 같은 모양의 꾸러미를 한 번 더 돌려준다.

---

## 5. 강화 화면 (S13 9, S8 4.15·16)

### 5.1 키와 구성

| 키 | 동작 |
|---|---|
| 방향키 | 칸 이동 |
| J | 넣기 / 강화 / 의식 중 재입력 = 건너뛰기 / 결과 카드에서 "같은 구성으로 다시 시도"(`str.enhance.retry`) |
| K | 빼기 / 이어서 강화 중지 |
| Q / E | 지팡이 바꾸기(이어서 강화 끝남) |
| Esc | 나가기(들어온 곳으로) |
| 마우스 | 메뉴이므로 가능(키보드만으로도 됨) |

화면 모델(`EnhanceViewModel`, S13 9.3): 위 띠(◀ 지팡이 이름 ▶ +N 칭호, 골드, 하·중·상 강화석과 묶임 수) / 왼쪽 칸(연결 슬롯·확장 n/3, 변이 목록·상한, 역대 최고 `str.enhance.best_stage`, 바닥, 흔적 존재·공개 토글) / 가운데(제물 칸 5 `str.enhance.sacrifice`, 부적 칸 `str.enhance.charm`, 기운 0~3) / 오른쪽(큰 확률 숫자, 상한, 내역 4줄, 결과 띠 4구간, 실패 안내, 기댓값 표) / 아래 띠(제물 후보 = 서고, 한 줄 14칸 좌우 넘김 `str.enhance.candidates`; 비용 `str.enhance.cost`; 하락 가능 꼬리표; 빠른 강화 `str.enhance.fast_mode`; 이어서 강화 `str.enhance.auto_continue`; 누계 `str.enhance.tally`).

- 비용이 모자라면 [강화 J]가 꺼지고 모자란 쪽이 흔들린다.

### 5.2 상태 기계 (S13 9.5)

```
enum EnhancePhase { Idle, Committing, Payment, Offering, Charge, Hold, WaitingLate, Judge,
                    ResultPlay, MutationChoice, MutationAccept, MutationRejectReplay,
                    Milestone, Plus13, ResultCard, ShortResult }
```

| 지금 | 사건 | 다음 |
|---|---|---|
| Idle | J(조건 충족) | Committing — 결과를 **즉시 로컬 계산**(오프라인은 기다림 없음). 다른 조작 잠금 |
| Committing | 즉시 | Payment → Offering → Charge → Hold → Judge |
| 의식 중 | J 재입력(누름 뒤 0.3초 = 18틱 이후) | ShortResult(0.5초 = 30틱) |
| Judge | | ResultPlay(kind) |
| ResultPlay 끝 | kind == MutationOffered | MutationChoice(건너뛸 수 없음) |
| MutationChoice | 받는다 | MutationAccept → ResultCard |
| MutationChoice | 거절한다 | fallback 결과의 0.50초 이후 연출 재생 → ResultCard |
| ResultPlay 끝 | 마일스톤 | Milestone(+2.5초 / +0.8초) |
| ResultPlay 끝 | 도착 13 | Plus13 |
| ResultCard | J | 같은 구성 재시도 → Committing |
| ResultCard | 이어서 강화 켜짐 & 멈춤 조건 아님 | 자동 Committing |

WaitingLate(서버 결과 지연, "까마귀가 늦는다")는 **솔로 제외**.

### 5.3 시간표 (S8 4.15, S13 9.6)

강도 = 도전 단계로: +1 → 0, +2·+3 → 1, +4~+6 → 2, +7~+9 → 3, +10~+12 → 4, +13 → 5.

| 강도 | 충전 | 숨 멈춤 | 심장(분당) | 실패 뒤 침묵 |
|---|---|---|---|---|
| 0 | 0.4초 = 24틱 (심장·숨 멈춤 없이 바로 성공) | — | — | — |
| 1 | 0.8초 = 48틱 | 0.15초 = 9틱 | 70→100 | 0.2초 = 12틱 |
| 2 | 1.2초 = 72틱 | 0.25초 = 15틱 | 70→120 | 0.3초 = 18틱 [임시] |
| 3 | 1.8초 = 108틱 | 0.35초 = 21틱 | 70→140 | 0.4초 = 24틱 [임시] |
| 4 | 2.4초 = 144틱 | 0.50초 = 30틱 | 70→160 | 0.5초 = 30틱 [임시] |
| 5 | 3.2초 = 192틱 | 0.80초 = 48틱 | 70→180 | 0.6초 = 36틱 |

구간: Payment 0.15초(9틱) → Offering 제물 있으면 0.60초(제물 간격 0.12초 = 7.2틱, 틱 9,16,23,31,38), 없으면 0.20초(12틱) → Charge(위 표, 게이지는 처음 40% 시간에 80%, 남은 60% 시간에 20%) → Hold → Judge. 강도 3 예: 판정 2.90초(174틱).

결과 연출 길이: 성공 2.0초(120틱), 대성공 3.6초(216틱), 하락 2.2초(132틱), 부적이 막음 2.4초(144틱), 하락 없음 1.6초(96틱), 변이 제시 = 고를 때까지, 마일스톤 첫 +2.5초(150틱)·다시 +0.8초(48틱), +13 약 9초(540틱). **대성공은 항상 실패처럼 시작**(첫 0.5초가 실패와 같음). 대성공은 숫자를 두 번 찍음("+7 → +8", 0.25초 뒤 "→ +9").

결과 카드 문구 열쇠: 성공 `str.enhance.result.success`, 대성공 `.great_success`, 하락 `.fail_drop` + 느루 `str.enhance.line.fail_drop` "원래 그런 거다.", 부적 `.fail_charm` + `str.enhance.line.fail_charm` "부적 1개가 대신 탔다.", 하락 없음 `.fail_no_drop` + `str.enhance.line.fail_no_drop` / 바닥이면 `str.enhance.line.fail_floor`, 변이 `.mutation`, 뽑힌 값 `str.enhance.line.roll`(문구 경계 모순 13절 M-3).

기운 불씨가 켜질 때 큰 확률 숫자가 "+15" 굴러 오른다.

### 5.4 건너뛰기 · 빠른 강화 · 이어서 강화 (S8 4.15, S13 9.8·9.9)

| 기능 | 규칙 |
|---|---|
| 건너뛰기 | 의식 중 J(누름 뒤 0.3초 무시) → 짧은 결과 0.5초. 변이 카드, +13 첫 도달 처음 2초는 못 넘김 |
| 빠른 강화 | 충전 0.3초(18틱), 심장·숨 멈춤·카메라·어두워짐 없음, 시도 약 0.8초(48틱). **+10 이상 도전(stage ≥ 9)은 켜도 전체 의식**(설정 `str.settings.opt.enhance_fast_allow_high`로 풀 수 있음, 기본 끔). 강화 화면 토글과 설정 `enhance_ritual`(`str.settings.opt.enhance_ritual`)은 같은 값 |
| 짧은 형태 | 숫자 내려찍기 + 속성색/회색 고리 + 한 음. 대성공 금 고리·두 번, 변이 연두 고리. 기운·확률 숫자 변화는 생략하지 않음 |
| 이어서 강화 | 같은 제물 구성(같은 block id 우선, 없으면 같은 (등급, 속성) 다른 블록으로, U-21) + 같은 부적 여부로 반복. **시도마다 따로 판정** |

이어서 강화 멈춤 조건(하나라도): 목표 단계 도달(기댓값 표 목표와 같은 값), 골드·강화석·제물·부적 부족, 변이 제시, 대성공, 마일스톤 도달(+4/+7/+10/+13), (선택) 하락 시 멈춤(기본 켬 [임시]), J/K 누름, 지팡이 바꿈. 하락 없는 시도로 바뀌면 그 시도엔 부적을 쓰지 않는다. 누계 "12번째 시도 · 성공 5 · 실패 7 · 쓴 골드 44,800"(실패 수에 변이·부적이 막음·하락 없음 포함 [임시]).

### 5.5 강화 첫 안내 (S13 9.12)

4쪽 안내(`str.enhance_tutorial.title`, `.page`). 그 프로필에서 **까마귀 보따리(하급 3개)를 처음 받은 정산 뒤 한 번**. 저장 플래그 `enhanceTutorialShown`.

---

## 6. 기댓값 표 (S8 5) [제안]

- 상태 `(s, a, g)`: s 단계 0~13, a 기운 0~3, g = `min(2, floor_peak − s)`. 최대 168개.
- 가정: **앞으로 제물·부적을 쓰지 않는다**, 변이는 받는다(단계 유지, 기운+1).
- 흡수: `s ≥ T`(대성공으로 넘어도 도달).

전이(시도 1회):

```
p  = (s == 0) ? 1.00 : min(0.95, (Base[s] + 15a + sac) / 100)     // 표시용 sac = 0
pg = (s == 0) ? 0 : 0.15p ;  ps = p − pg ;  f = 1 − p
대성공 (pg):     s' = min(13, s+2), a' = 0, g' = max(0, g − (s'−s))
성공 (ps):       s' = s+1,          a' = 0, g' = max(0, g − 1)
변이 (0.25f):    s' = s, a' = min(3, a+1), g' = g
일반 실패 (0.75f): s>3 && g<2 → s' = s−1, a' = min(3,a+1), g' = g+1 (하락)
                  그 밖       → s' = s,   a' = min(3,a+1), g' = g   (하락 없음)
비용: 시도 1, 골드 100(s+1)², 강화석 tier(s) 1개
```

풀이: `E[x] = c(x) + Σ P(x→y) E[y]` 연립 방정식을 가우스 소거(정확) 또는 반복(상대 오차 1e-10)으로. 같은 행렬로 오른쪽 벡터만 바꿔 시도 수 / 골드 / 하·중·상 강화석을 구한다. **이 요약서 작성 중 위 전이로 직접 풀어 TV-MK-01~04, 06, 09, 10, 20(+13)이 소수 둘째 자리까지 맞는 것을 확인했다**(TV-MK-10은 하락 조건에서 `g<2` 대신 항상 하락, g는 2에서 고정).

표시(S8 5.4):

| 표시 | 계산 | 열쇠 |
|---|---|---|
| 목표 단계 | 현재+1 ~ +13, 기본 = 다음 마일스톤 [임시] | `str.enhance.expect.target` |
| 평균 시도·골드·강화석 | 현재 (stage, aura, min(2, floor_peak−stage))에서 T까지 E | `str.enhance.expect.avg_tries/avg_gold/avg_stone` |
| 보유 재화로 평균 N번 | [임시 U-12] 현재 분포에서 한 시도씩 앞으로 굴리며 시도 k의 기대 골드·강화석을 누적. 누적 기대 골드가 보유 골드를 넘거나 어느 구간 누적 기대 강화석이 보유량을 넘기 직전까지의 k. 최대 1,000 | `str.enhance.expect.with_owned` |

기댓값은 기기에서 계산(통신 없음). 강화 화면을 열 때·지팡이를 바꿀 때 다시 푼다.

---

## 7. 경제 (S8 6, S7 13.5)

### 7.1 재화와 물건 (솔로)

| 재화/물건 | 열쇠 | 얻는 곳 | 쓰는 곳 |
|---|---|---|---|
| 골드 | `currency.gold` | 웨이브 골드(7.2), 남은 결정 개당 5, 가져가지 못한 야생 블록(일반 5/희귀 15/영웅 50), 떠나보내기 환급 | 강화, 슬롯 새기기, 변이 제거, 상점, 판 중 야영 회복·다시 뽑기(원정 지갑에서) |
| 하급 강화석 | `mat.stone_low` | 까마귀 보따리(첫 웨이브 3 넘김 3개, 프로필당 1번), 다섯 번째 웨이브(1일째 웨이브 5 확정 1, 2일째 웨이브 15 확정 1), 1일째 보스(웨이브 10) 확정 1, 2일째 엘리트 10%. **웨이브 21부터 나오지 않음** | 도전 +1~+5 |
| 중급 강화석 | `mat.stone_mid` | 다섯 번째 웨이브(2일째부터 확정 1), 보스(웨이브 10 50%, 20부터 확정 1), 엘리트(2일째 8%, 3일째부터 5%) | 도전 +6~+9 |
| 상급 강화석 | `mat.stone_high` | 다섯 번째 웨이브 d≥3 `min(2500, 500(d−2))`bp, 보스(웨이브 20 20%, 30부터 확정 1 + d≥4 추가 1개 `min(5000, 1000(d−3))`bp), 엘리트 d≥3 `min(500, 200+50(d−3))`bp | 도전 +10~+13, 슬롯 새기기 2개 |
| 하락 방지 부적 | `charm.safeguard` | 보스 `min(1000, 200d)`bp(웨이브 10 2% … 최대 10%), 3일째 이후 엘리트 50bp, 상점 10,000골드(주 3개) | 하락 막기 |
| 블록(서고) | `block.*` | 판에서 귀환 가방으로 | 조립, 제물 |
| 복수 보상(귀속) | | 웨이브 20 허상 첫 처치: 중급 3·상급 1·부적 1(묶임, 강화에서 먼저 쓰임) | |

- 표는 S7 13.5 표 6.3-A~D의 요약. 세부 정본은 S7(웨이브·드랍 요약서 담당). "N%"는 줄마다 따로 한 번 뽑아 맞으면 1개, "확정"은 추첨 없음. 엘리트 줄은 엘리트 한 마리마다. 프로필 첫 웨이브 10(허상 첫 만남)은 하급 1 확정만, 추첨 없음.
- **재료·부적 추첨은 판 씨앗이 아니라 정산 때 기기 난수**로(S8 6.1). 판 중에는 기회 목록(넘긴 다섯 번째 웨이브, 잡은 보스, 잡은 엘리트 수)만 기록. 줄 순서 고정: 하급, 중급, 상급, 상급 추가, 부적.
- 시즌 주화·자동 매치 보상·주간 도전 보상·시즌 환전·별빛: 솔로 제외(오프라인 주간 도전은 기록만, 보상 없음).

### 7.2 웨이브 골드와 원정 지갑 (S8 6.2, M6.1)

```
S(w) = 1 + 0.1 (w − 1)
B(w) = (w ≤ 12) ? 10 + 6w : min(200, 82 + 4(w − 12))
WaveGold(w) = (w % 10 == 0) ? 200·S(w) + 20      // 보스 처치 + 클리어
                            : 2·B(w) + 20         // 처치(비용×2) + 클리어 20 (기대값 모형)
```

- 실제 지급: 일반 적 처치 = 비용 × 2(슬라임 2, 박쥐 2, 꽈리 벌레 4, 늑대 6, 족제비 6, 술사 8, 고슴도치 8, 도깨비불 8, 가시덩굴 8, 멧돼지 10, 북두꺼비 10, 선돌지기 12), 엘리트 = 바탕 종 3배, 웨이브 클리어 20, 보스 처치 `200 × S(w)` (10: 380, 20: 580, 30: 780, 50: 1,180).
- 0골드: 건너뛴 웨이브, 보스의 졸개, 갈라진·불러낸 개체.
- 허상 첫 만남(프로필 첫 웨이브 10): 클리어 20만(보스 처치 골드 없음).
- **원정 지갑**: 판 중 번 골드는 원정 지갑에 쌓이고 판 중 지출(야영 회복, 다시 뽑기 30골드 등)은 지갑에서만. 정산 때 남은 금액이 프로필로.
- 남은 마력 결정은 웨이브 끝에 **개당 5골드**(char.5의 빚이 있으면 빚부터 갚음).
- 새벽 야영(보스 뒤) 체력 1칸 무료 회복.

### 7.3 지출 (솔로) (S8 6.4)

| 지출 | 금액 |
|---|---|
| 강화 | `100 × (stage+1)²` |
| 슬롯 새기기 | `20,000 × (확장 칸+1)` + 상급 2 |
| 하락 방지 부적(상점) | 10,000골드, 프로필당 주 3개 |
| 새 지팡이 +0 | 500골드, 개수 제한 없음 |
| 변이 제거 | 1개당 3,000골드 |
| 서고 확장 | 서고 한도 = 블록 개수 합(끼운 블록 제외). 100에서 시작, 50개당 2,000골드, 횟수 제한 없음 |
| 골드 꾸미기 | **미정**(U-08: `shop_cosmetics.json`, 값 비워 둠) |
| 판 중 | 야영 회복·다시 뽑기(원정 지갑) |

서고 한도: `LibraryCap = 100 + 50 × library_expansions`, `LibraryCount = Σ block qty − qty_socketed`. 한도가 차면 귀환 가방에 담지 못한 것으로 보고 골드(7.1 값)로 바뀐다.

### 7.4 지팡이 감정가와 떠나보내기 환급 (S8 6.6) [임시 값]

감정가 = 그 단계까지 평균 강화 골드(제물 +15 모든 시도 기준 마르코프) × 0.5, 반올림. +0은 250(규칙). 환급 = 감정가 × 10% 내림, +0은 0. 변이·슬롯은 감정가에 넣지 않는다. 솔로에서 감정가는 떠나보내기 환급에만 쓴다(그대로 표로 넣는다).

| 단계 | 평균 누적 골드(+15) | 감정가 | 떠나보내기 환급 | 평균 시도 |
|---|---|---|---|---|
| +0 | 0 | 250 | 0 | 0 |
| +1 | 100 | 50 | 5 | 1.00 |
| +2 | 521 | 261 | 26 | 2.05 |
| +3 | 1,367 | 683 | 68 | 2.99 |
| +4 | 3,148 | 1,574 | 157 | 4.11 |
| +5 | 6,800 | 3,400 | 340 | 5.69 |
| +6 | 13,495 | 6,748 | 674 | 7.77 |
| +7 | 24,989 | 12,494 | 1,249 | 10.45 |
| +8 | 44,137 | 22,068 | 2,206 | 13.92 |
| +9 | 76,093 | 38,047 | 3,804 | 18.55 |
| +10 | 129,247 | 64,624 | 6,462 | 24.85 |
| +11 | 213,439 | 106,720 | 10,672 | 33.16 |
| +12 | 339,311 | 169,656 | 16,965 | 43.60 |
| +13 | 516,618 | 258,309 | 25,830 | 56.10 |

### 7.5 수급 사실 (S8 6.7·6.8, 조정 참고)

- 목표: 한 자루 +13까지 보통 플레이어(제물 매번 +15) 65~90시간 [확정 D1.20]. 막히는 자원은 **골드**. 강화석은 크게 남는다(+13까지 하 7·중 18·상 31, 65시간 동안 하 50·중 480·상 165).
- 조정 손잡이: 비용 계수 100, 부적 값·주간 한도, 부적 드랍 확률, 보스 골드 계수 200, 시작 웨이브 비율 70%. 데이터 키로 둔다.
- 오프라인만으로 +13까지 갈 수 있다(S8 8.1).

---

## 8. 상점 (S8 7) [제안]

| 항목 | 솔로 값 |
|---|---|
| 여는 곳 | 메인 메뉴 "상점"(`str.main_menu.item.shop`), 제목 `str.shop.title`, 종류 `str.shop.kind.gold` |
| 탭 | 부적(`str.shop.tab.charm`), 지팡이(`str.shop.tab.wand`), 그 밖(`str.shop.tab.other`) |
| 부적 | 10,000골드, **주 3개**(`str.shop.weekly_limit` "이번 주 {n} / {max}", 다 사면 `str.shop.sold_out`). 초과 `WeeklyLimit` |
| +0 지팡이 | 500골드, 캐릭터 5명 누구 것이든, 조건·한도 없음, 강화석 들지 않음. 산 즉시 지팡이 개체(빈 조립, origin Shop). `str.shop.wand_count` "지금 {n}자루" |
| 서고 확장 | 2,000골드 → 한도 +50 (`item.library_expansion`) |
| 구매 확인 | `str.shop_buy_confirm.title/confirm`, 값 `str.shop.price`, 산다 `str.shop.buy` |
| 주의 경계 | **기기 현지 시간 월요일 00:00 ~ 다음 월요일 00:00** [임시 U-07]. 저장: `shop_week {week_start, charm_bought}` |
| 유료 탭 | 없음. 별빛 탭·잔액·구매 버튼·안내문을 **아예 그리지 않는다** |

```
Buy(sku, qty):
  charm.safeguard: require charm_bought + qty ≤ 3 && gold ≥ 10000·qty ; gold −= 10000·qty ; charm += qty ; charm_bought += qty
  item.wand_box(character): require gold ≥ 500 ; gold −= 500 ; new Wand(+0, 빈 조립, origin Shop, acquired_at=now)
  item.library_expansion: require gold ≥ 2000 ; gold −= 2000 ; library_expansions += 1
  → 저장 파일 쓰기
```

---

## 9. 오프라인 프로필 (S8 8, C 11.3)

### 9.1 원칙

- 게임을 처음 켤 때 자동 생성(계정·이메일 불필요). 권위 = 기기.
- 오프라인에서 되는 것: 무한 웨이브, 조립·서고, 강화 화면(새기기·변이 제거 포함), 상점, 연습, 옷장, 기록, 주간 도전(기록만), 허상 첫 만남·복수(기기 보상). 안 되는 것(광장, 거래·장터, 우편·친구, 사람 결투, 순위표, 시즌)은 메뉴에 흐린 단추를 두지 않고 "서버에 접속하면 열립니다" 한 줄과 묶는다. 안내 열쇠 `str.offline_guide.*`(제목 `str.offline_guide.title`, 세 줄 `line.1~3`).
- 섞지 않는다: 오프라인 지팡이·재화·블록·기록은 서버로 올라가지 않는다(올리기·바꿔 주기·환산 모두 없음). 같이 쓰는 것은 설정·키 배치뿐. 프로필 전환은 타이틀에서만, 타이틀에 지금 프로필 표시(`str.title.profile.offline`).
- 모든 화면은 `IBackend`(또는 같은 역할의 인터페이스)만 부르고 `OfflineBackend`가 처리하게 두면 나중에 멀티(서버 백엔드)를 붙이기 쉽다(S8 8.3). 메서드: `GetProfile`, `Enhance(requestId, wandId, sacrifices, useCharm)`, `ResolveMutation(requestId, wandId, attemptId, decision)`, `RemoveMutation`, `CarveSlot`, `SetScarVisibility`, `SetEquippedWand`, `ReleaseWands(ids, typedName)`, `SetLoadout`, `GetShop`, `Buy(sku, qty)`. 결제 메서드는 오프라인에서 부르지 않는다(유료 상품 0개).

### 9.2 새 프로필 시작 상태 (S8 8.4) [제안]

| 항목 | 값 |
|---|---|
| 캐릭터 | 5명 전원 |
| 지팡이 | 캐릭터마다 +0 한 자루(코어 마력탄 1개), 각자 든 지팡이 |
| 서고 | 마력탄 5개(전부 끼워 둔 것), 여분 없음 |
| 골드·강화석·부적 | 0 |
| 비기 | 캐릭터마다 시작 3종(마력 방출 `art.burst`, 반동 사격 `art.recoil`, 고유 비기) |
| 기록 | 최고 웨이브 0, 시작 웨이브 선택 없음, "허상을 만난 적 없음" |
| 서고 확장 0(한도 100), 상점 주간 구매 0 | |

### 9.3 저장 파일 (S8 8.5, C 11.3)

| 규칙 | 값 |
|---|---|
| 경로 | `Application.persistentDataPath/profiles/offline/profile.json` + 이전 세대 `.1` `.2` `.3` |
| 형식 | UTF-8 JSON. **마지막 줄에 본문의 SHA-256**(C 11.3. S8은 "CRC32/SHA-256" — C를 따름). 깨짐 감지용, 변조 방지 아님. `schema_version`, `data_version` 필드 |
| 쓰는 때 | 판 끝, 강화(새기기·변이 결정·변이 제거 포함), 조립 변경, 상점 구매, 떠나보내기, 흔적 공개 변경, 든 지팡이 변경 |
| 쓰는 법 | 임시 파일에 쓰고 이름 바꾸기(원자적). 직전 3세대 유지(.2→.3, .1→.2, 현재→.1, 임시→현재) |
| 깨진 파일 | 직전 세대로 되돌리고 한 줄 알림 |
| 고치기 | 막지 않는다(남에게 피해 없음) |
| 시각 | 주간 한도 등은 기기 시계 |
| id | 정본(`"character": 1`, `"core": "block.magic_bolt"`, `"mat.stone_low"`) |

최상위 구조(S8 8.5 예시를 정본 id로 고친 것):

```json
{
  "schema_version": 1, "data_version": "…",
  "profile_kind": "Offline", "created_at": "…",
  "characters": [{"id":1,"equipped_wand_id":1,"unlocked_arts":["art.burst","art.recoil","art.kiln"],"best_score":0,"best_wave":0}],
  "wands": [{"id":1,"character":1,"stage":0,"floor_peak":0,"record_peak":0,"drop_count":0,"aura":0,
             "slots_extra":0,"carve_bonus":0,"mutations":[],"pending_offer":null,
             "loadout":{"core":"block.magic_bolt","links":[null,null,null,null],"extra":[]},
             "skin_id":null,"show_scars":false,"origin":"Starter","owner_count":1,"acquired_at":"…","version":1}],
  "currencies": {"gold": 0},
  "items": {"mat.stone_low":{"qty":0,"qty_bound":0}, "mat.stone_mid":{"qty":0,"qty_bound":0}, "mat.stone_high":{"qty":0,"qty_bound":0},
            "charm.safeguard":{"qty":0,"qty_bound":0}, "block.magic_bolt":{"qty":5,"qty_socketed":5}},
  "library_expansions": 0, "shop_week": {"week_start":"…","charm_bought":0},
  "flags": {"phantom_met": false, "revenge_done_at": null, "crow_bundle_given": false, "enhance_tutorial_shown": false},
  "records": {}, "enhance_log_tail": []
}
```

캐릭터별 저장 항목 정리: `equipped_wand_id`, `unlocked_arts`(비기 해금, 캐릭터별 계정에 남음 D2.8), `best_score`, `best_wave`(+ 시작 웨이브 범위). 지팡이 목록은 `wands[]`(캐릭터 필드로 거름, 개수 제한 없음). 재화 `currencies.gold`, 물건 `items`. 해금·플래그 `flags`. (`enhance_tutorial_shown`은 S13 9.12 플래그를 넣은 것 [임시].)

---

## 10. 겉모습 — 단계별 (W*.md, 그림은 이미 있음)

- 그림은 `art/production/wands/<dalgu|chorong|galpi|seonge|geumeum>/` 에 이미 있다: `hand.png`(32×64 셀, **14열 = 단계 0~13**, 6행 = down_hold, down_aim, right_hold, right_aim, up_hold, up_aim), `inspection.png`(192×192 셀, 7열×2행 = 14단계, 강화 화면 큰 그림 — 화면에서 2배 정수 확대 384×384), `icon.png`(64×64, 14열). `art/production/wands/manifest.json`이 손잡이 기준점 `grip [16,48]`, 필터 point, 단계마다 `head`(머리 위치), `mutations`(변이 표식 2점, char.2는 3점), `expansion`(슬롯 확장 표식 1점) 좌표를 준다. 폴더 이름은 작업용 이름이므로 코드는 manifest로 `wand.N` ↔ 폴더를 잇는다(S14: char.1=dalgu, 2=chorong, 3=galpi, 4=seonge, 5=geumeum).
- 단계 별명: 이름 표 `wand.N.stage.K.nickname` (예: wand.1 +0 "식은 부지깽이", +4 "쇳물 송이", +13 "백열 쇳덩이"). 큰 변신 단계(+4/+7/+10/+13)에 처음 닿으면 변신 뒤 새 별명을 한 번 띄운다.
- 머리 모양 다섯 묶음(+0~3 / +4~6 / +7~9 / +10~12 / +13). 큰 변신 = +4, +7, +10, +13(마일스톤과 같음). 이웃 단계는 윤곽이 4px 이상 다르다(색만 바뀌는 단계 없음).
- 하락하면 한 단계 낮은 모양으로 **완전히** 돌아간다(높은 단계 모양을 반쯤 남기지 않음).
- 흔적 덧그림: 손 그림 2×2 점, 큰 그림 6×6 딱지 + 연기(윤곽 바꾸지 않음), 최대 2개. 다시 오르면 하나씩 없어짐.
- 변이 표식: 지팡이 그림 위 덧그림(manifest의 변이 표식 좌표). 변이 카드·목록용 **변이 아이콘 6종(32×32, `icon.mutation.<id>`)**은 공용 에셋.
- 슬롯 확장 표식(확장 칸 수만큼, 하락·거래해도 남음): char.1 구리 고리, char.2 민트 고리(장대 아래 1/3), char.3 색인 꼬리표, char.4 금 방울(손잡이 금실 끝), char.5 금 가락지.
- 유리 지팡이 판 안 임시 하락: 내려간 단계 모양 + 흔적 대신 **흰 금**(내려간 수만큼, 최대 3줄), 판 끝나면 원래대로. 흔적 공개와 무관하게 항상 보임.
- 겉모습은 판정·피해에 영향 없음. 오프라인에서도 연출 전부 같다.
- 강화 화면 지팡이별 연출(대장간 망치, 등불 번개 등)은 각 W 문서 6절. 그림·소리 제작은 범위 밖, 이벤트 자리만(S8 16절 이벤트 이름: `enhance.result.success`, `enhance.result.great_success`, `enhance.result.fail_drop`, `enhance.result.charm_blocked`, `enhance.result.fail_no_drop`, `enhance.mutation.offered/accepted/declined/removed`, `enhance.aura.gained/consumed`, `enhance.scar.added/piece_removed`, `enhance.milestone.reached`, `enhance.plus13.reached`, `enhance.carve.attempted` 등).

---

## 11. 오류 코드 (솔로에서 쓰는 것) (S8 13, C ErrorCode 200~)

`MaxStage`(201), `WandLocked`(202), `MutationPending`(203), `NoPendingMutation`(204), `NotEnoughGold`(205), `NotEnoughStone`(206), `NotEnoughCharm`(207), `CharmNotNeeded`(208), `InvalidSacrifice`(209), `LastProjectile`(210), `NotEligible`(211), `LastWand`(212), `NameConfirmRequired`(213), `WeeklyLimit`(217), `LibraryFull`(218), `InvalidLoadout`(219), `CoreEmpty`(220). 화면은 코드만 보고 `error.*` 문구를 고른다. 솔로 제외: NotOwner, TradeLocked, PendingVerification, HasDebt, PaymentsDisabled, NotSupportedOffline.

---

## 12. 테스트 벡터 (S8 12절 원문 그대로)

각 표 앞에 솔로 적용 여부를 적었다. 원문 표기의 `char_2`, `magic_bolt`, `giant_fireball`은 각각 `char.2`, `block.magic_bolt`, `block.fireball`로 읽는다.

형식: `TV-<영역>-<번호>`: given / when / then. 정수 확률은 %(bp = ×100). "근사"는 몬테카를로 회귀 테스트로 허용 오차를 적는다. 마르코프 값은 5절 풀이로 소수 둘째 자리까지 맞아야 한다(상대 오차 1e-6).

### 12.1 확률 계산
> 솔로: 전부 적용.

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-P-01 | stage 0, aura 3, 제물 영웅 5개 | SuccessPct | 100, great_bp = 0 | G9 |
| TV-P-02 | stage 7(+8 도전), 제물 등급 합 22, 같은 속성 있음, aura 2 | SuccessPct | 25 + 22 + 5 + 30 = **82** | E1.2 예시 |
| TV-P-03 | stage 1, aura 1, 제물 없음 | SuccessPct | min(95, 90 + 15) = **95**, cap_hit = true | G9 |
| TV-P-04 | stage 12, 제물 영웅 3개(42) + 같은 속성, aura 3 | SuccessPct | 제물 min(40, 47) = 40 → 3 + 40 + 45 = **88** | G9 [임시 상한 해석] |
| TV-P-05 | 제물 영웅 5개(70), 다른 속성 | SacrificeBonus | **40** | G9 |
| TV-P-06 | 제물 일반 3개, 그중 1개 같은 속성 | SacrificeBonus | 12 + 5 = **17** | O2.5 |
| TV-P-07 | 제물 일반 2개, 둘 다 같은 속성 | SacrificeBonus | 8 + 5 = **13**(속성 보너스는 시도당 1회) | G9 |
| TV-P-08 | 제물 희귀 1개(무 속성) | SacrificeBonus | **8** | G9 |
| TV-P-09 | 제물 6개 | ValidateAttempt | `InvalidSacrifice` | G9 |
| TV-P-10 | stage 11, aura 0, 제물 0 | SuccessPct | **5** | D1.18 |
| TV-P-11 | stage 9(+10 도전), aura 3, 제물 0 | SuccessPct | 12 + 45 = **57** | G9 |
| TV-P-12 | 기본 확률표 전체 | BaseProb[0..12] | 100,90,75,60,50,40,32,25,18,12,8,5,3 | D1.18 [확정] |

### 12.2 판정 구간
> 솔로: 전부 적용. (TV-R-02 "뽑힌 값 37.20"은 미만 판정 기준)

| ID | given | when | then |
|---|---|---|---|
| TV-R-01 | stage 7, SuccessPct 82 | 구간 | great [0,1230), success [1230,8200), mutation [8200,8650), fail [8650,10000) |
| TV-R-02 | 위, roll = 3720 | 판정 | Success("뽑힌 값 37.20") (E7 예시 값) |
| TV-R-03 | 위, roll = 1229 / 1230 | 판정 | GreatSuccess / Success |
| TV-R-04 | 위, roll = 8199 / 8200 / 8649 / 8650 | 판정 | Success / Mutation / Mutation / Fail |
| TV-R-05 | stage 0, roll = 0 / 9999 | 판정 | Success / Success(great_bp 0, p_bp 10000) |
| TV-R-06 | SuccessPct 95 | 구간 | great 1425, mut_end 9500 + 125 = 9625 |
| TV-R-07 | SuccessPct 3 | 구간 | great 45, mut_end 300 + 2425 = 2725 |
| TV-R-08 | 대성공 비율 | 모든 p(1..95) | great_bp / p_bp = 0.15 정확히 |

### 12.3 비용과 강화석
> 솔로: 전부 적용(TV-C-06 귀속 우선은 복수 보상 재료에만 해당).

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-C-01 | stage 0..12 | Cost | 100, 400, 900, 1600, 2500, 3600, 4900, 6400, 8100, 10000, 12100, 14400, **16900** | O1.3, O2.4 |
| TV-C-02 | stage 4 / 5 / 8 / 9 / 12 | StoneTierFor | Low / Mid / Mid / High / High | O1.3 |
| TV-C-03 | 입문자: 골드 1,400, 하급 3 | +1, +2, +3 연속 도전 | 100 + 400 + 900 = 1,400골드, 하급 3개 소모(첫 판 재료로 충분) | O2.2 |
| TV-C-04 | 골드 16,899, 상급 1, stage 12 | 시도 | `NotEnoughGold` | |
| TV-C-05 | 골드 충분, 상급 0, 중급 5, stage 9 | 시도 | `NotEnoughStone`(구간 강화석은 상급) | O1.3 |
| TV-C-06 | 하급 귀속 1 + 거래 가능 2 | 시도(stage 0) | `qty_bound` 1 → 0, 거래 가능 2 그대로 | O5.5 |
| TV-C-07 | 대성공·실패·변이 모두 | 비용 | 결과와 무관하게 골드·강화석·제물이 빠져 있다 | [임시] |

### 12.4 결과 분기
> 솔로: 전부 적용. 단 "알림 Plus13/Plus10to12"는 오프라인에서 null(TV-N-05).

| ID | given (stage, floor_peak, aura, 부적) | when | then | 출처 |
|---|---|---|---|---|
| TV-B-01 | (3, 3, 0, 없음) | 일반 실패 | FailNoDrop, stage 3, aura 1 | G9 |
| TV-B-02 | (4, 4, 0, 없음) | 일반 실패 | FailDrop, stage 3, floor_peak 4, drop_count +1, aura 1, 흔적 1 | G9 |
| TV-B-03 | (7, 8, 2, 없음) | 일반 실패 | FailDrop → +6 ("실패하면: +6으로 하락") | E1.2 예시 |
| TV-B-04 | (6, 8, 0, 없음) | 일반 실패 | FailNoDrop("바닥이다(역대 최고 +8)"), stage 6, aura 1 | G9, E3.5 |
| TV-B-05 | (6, 8, 0, 부적 끼움 시도) | ValidateAttempt | `CharmNotNeeded`(하락 없는 시도, 칸 접힘) | E1.2 |
| TV-B-06 | (9, 9, 1, 부적 1) | 일반 실패 | FailCharmBlocked, stage 9, aura **1 그대로**, 부적 0 | G9, E3.4 |
| TV-B-07 | (9, 9, 1, 부적 1) | 성공 | Success, stage 10, aura 0, **부적 1 그대로** | G9 |
| TV-B-08 | (9, 9, 1, 부적 1) | 변이 받음 | stage 9, aura 2, 부적 1 그대로 | G9(부적은 변이를 막지 않는다) |
| TV-B-09 | (9, 9, 1, 부적 1) | 변이 거절 | FailCharmBlocked와 같다: stage 9, aura 1, 부적 0 | G9 |
| TV-B-10 | (9, 9, 1, 없음) | 변이 거절 | FailDrop: stage 8, aura 2 | G9 |
| TV-B-11 | (12, 12, 0, slots 0) | 대성공 | stage **13**(+14 아님), slots 1, aura 0, 알림 Plus13 | G9 |
| TV-B-12 | (11, 11, 0, slots 2) | 대성공 | stage 13, slots 3 | G9 |
| TV-B-13 | (5, 5, 0, slots 3) | 대성공 | stage 7, slots 3, slot_already_full | G9, E3.2 |
| TV-B-14 | (0, 0, 0) | 대성공 가능성 | 없음(great_bp 0) | G9 |
| TV-B-15 | aura 3 | 일반 실패 | aura 3(최대) | G9 |
| TV-B-16 | (6, 8, 2) | 성공 | stage 7, floor_peak 8, aura 0, 흔적 1 남음 | G9 |
| TV-B-17 | (7, 8, 0) record_peak 8 | 성공 → +8 | 흔적 0, record_peak_updated false | E1.3 |
| TV-B-18 | (8, 8, 0) | 대성공 | stage 10, floor_peak 10, record_peak 10, record_peak_updated, 알림 Plus10to12 **한 번**(stage 10) | E8 |
| TV-B-19 | 확장 2칸, stage 8 | 대성공 → +10 | 확장 3칸, 카드에 "연결 슬롯 +1 (3/3)" | E3.2 |
| TV-B-20 | (4, 6, 0)이 되는 경로: +6에서 두 번 하락 | 세 번째 일반 실패 | +4에서 멈춤(바닥 = 6 − 2 = 4) | G9 |

### 12.5 변이
> 솔로: 전부 적용(TV-M-07 "재접속" = 게임 재실행 후 강화 화면이 변이 카드부터). TV-M-10·11은 전투 쪽.

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-M-01 | char_1, 변이 [A(1), B(2)] | 변이 C 받음 | [B, C], 밀려남 A | G9 |
| TV-M-02 | char_2(초롱), 변이 [A, B] | C 받음 | [A, B, C] | G9, O1.1 |
| TV-M-03 | char_2, [A, B, C] | D 받음 | [B, C, D] | G9 |
| TV-M-04 | 코어 = magic_bolt | 후보 추첨 1만 회 | UnstableCore 0회 | G9 |
| TV-M-05 | 코어 = giant_fireball | 후보 추첨 | UnstableCore 포함 | G9 [임시: 폭발 블록 정의] |
| TV-M-06 | 변이 제시 대기 중 | 다시 EnhanceAsync | `MutationPending` | [임시] |
| TV-M-07 | 변이 제시 뒤 접속 끊김 | 재접속 | GetProfile에 pending_offer 있음, 화면이 변이 카드부터 | O1.4 [임시] |
| TV-M-08 | 변이 2개, 골드 3,000 | RemoveMutation(1개) | 변이 1개, 골드 0, stage·aura·slots 변화 없음 | O2.4 |
| TV-M-09 | 골드 2,999 | RemoveMutation | `NotEnoughGold` | O2.4 |
| TV-M-10 | GlassWand, 무한 웨이브에서 stage 8 지팡이가 피격 6번(웨이브마다 1번) | 판 종료 | 판 안 적용 단계 8→7→…→3(+3에서 멈춤), 판 끝난 뒤 wand.stage = 8 | G9, O1.3 |
| TV-M-11 | GlassWand, 같은 웨이브에서 피격 2번 | | 판 안 단계 −1만 | G9 |

### 12.6 기댓값 마르코프 (정확값, 제물·부적 없음, 변이 받음)
> 솔로: 전부 적용(기댓값 표). TV-MK-20~22, MC는 회귀용.

| ID | given | when | then (평균 시도 / 평균 골드 / 강화석 하·중·상) | 원문 비교(O2.5, sim 2만 회) |
|---|---|---|---|---|
| TV-MK-01 | +0, aura 0 | T=+5 | 6.71 / 8,517 / 6.71·0·0 | 7회 / 8,500 |
| TV-MK-02 | +0 | T=+7 | 14.78 / 37,461 / 9.04·5.74·0 | 15회 / 37,600 |
| TV-MK-03 | +0 | T=+10 | 49.01 / 273,628 / 9.04·36.09·3.88 | 49회 / 272,000 |
| TV-MK-04 | +0 | T=+13 | 174.92 / 1,776,723 / 9.04·48.11·117.77 | 175회 / 1,780,000, 9 / 48 / 118 |
| TV-MK-05 | 상태 수 | | (s, a, g) = 14 × 4 × 3 = **168** | G9, G15 |
| TV-MK-06 | +7, aura 2, floor_peak 8(g=1) | T=+10 | 30.40 / 218,423 | E1.2의 "+10까지 평균 14회"는 그림 예시(14절 C-04) |
| TV-MK-07 | +7, aura 0, g=0 | T=+8 | 7.70 / 39,617 | |
| TV-MK-08 | +9, aura 0, g=2 | T=+10 | 3.18 / 31,761 | |
| TV-MK-09 | +12, aura 0 / aura 3 | T=+13 | 63.15 / 849,523 , 29.49 / 399,489 | |
| TV-MK-10 | 바닥 규칙 없음(실패마다 −1, +3 이하 하락 없음은 유지) | +0→+13 | 1,265.15 / 8,769,262 | 1,274회 / 883만(MC 잡음) |
| TV-MK-11 | 단계별(제물 0) +1..+13 | +0→T 평균 시도 | 1.000, 2.105, 3.190, 4.508, 6.712, 10.053, 14.785, 21.629, 32.092, 49.008, 75.962, 116.776, 174.921 | |
| TV-MK-12 | 단계별(제물 0) | +0→T 평균 골드 | 100, 542, 1,518, 3,627, 8,517, 18,541, 37,461, 72,668, 140,091, 273,628, 530,301, 994,531, 1,776,723 | |
| TV-MK-13 | +3, aura 0 | T=+4 | 1.511 / 2,417 | |
| TV-MK-14 | +4, aura 0 | T=+5 | 2.536 / 5,626 | |

같은 풀이기를 제물 값만 바꿔 돌린 회귀(시뮬레이션 비교용, 표시 기능은 아님):

| ID | 제물(모든 시도) | T | 평균 시도 / 골드 / 강화석 | 원문(O2.5) |
|---|---|---|---|---|
| TV-MK-20 | +15 | +5 / +7 / +10 / +13 | 5.69 / 6,800 · 10.45 / 24,989 · 24.85 / 129,247 · 56.10 / 516,618, 강화석 6.57 / 18.26 / 31.28 | 6 / 6,800 · 10 / 25,000 · 25 / 130,000 · 56 / 517,000, 7 / 18 / 31 |
| TV-MK-21 | +30 | 같음 | 5.12 / 5,746 · 8.33 / 18,628 · 16.34 / 79,280 · 29.93 / 255,560, 강화석 5.45 / 9.76 / 14.73 | 5 / 5,700 · 8 / 18,700 · 16 / 79,000 · 30 / 256,000, 6 / 10 / 15 |
| TV-MK-22 | +15, 현재 단계 ≥ 10에서 부적(하락만 막음, 기운 안 쌓임) | +13 | 34.70 / 272,826 + 부적 5.42, 강화석 6.57 / 15.88 / 12.26 | 35 / 273,000 + 부적 5.4, 7 / 16 / 12 |

몬테카를로 회귀(규칙 엔진을 그대로 돌림, `seed` 고정, 2만 회, 허용 ±3%):

| ID | 조건 | then | 출처 |
|---|---|---|---|
| TV-MC-01 | 제물 +15, +0→+13 | 골드 중앙값 ≈ 451,000, 상위 10% 경계 ≈ 925,000 | O2.5 |
| TV-MC-02 | 위 시뮬레이터의 시도 평균 | TV-MK-20과 ±2% | |

### 12.7 슬롯 새기기
> 솔로: TV-S-12(거래 후)만 제외.

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-S-01 | stage 6, 확장 0 | CarveSlot | `NotEligible`(+7 이상) | O1.5 |
| TV-S-02 | stage 7, 확장 3 | CarveSlot | `NotEligible` | O1.5 |
| TV-S-03 | stage 7, 확장 0, 상급 2, 골드 20,000 | CarveSlot | 비용 상급 2 + 20,000. 확률 40% | O1.5 |
| TV-S-04 | 확장 1 / 2 | 비용 | 40,000 / 60,000골드 + 상급 2 | O1.5 |
| TV-S-05 | carve_bonus 0,1,2,3 | CarvePct | 40, 60, 80, **100** | O1.5 |
| TV-S-06 | 실패 | | stage·aura 그대로, carve_bonus +1 | O1.5 |
| TV-S-07 | 성공 | | slots_extra +1, carve_bonus 0 | [임시] |
| TV-S-08 | 칸 하나 기대 시도 | 분포 | 1회 40%, 2회 36%, 3회 19.2%, 4회 4.8%, 평균 **1.888** (≈1.9) | O1.5 |
| TV-S-09 | 0칸 → 3칸 기대 비용 | | 골드 1.888 × (20,000 + 40,000 + 60,000) = **226,560**(≈226,000), 상급 2 × 1.888 × 3 = **11.33**(≈11) | O1.5 |
| TV-S-10 | 첫 칸 기대 비용 | | 37,760골드(≈38,000) + 상급 3.78(≈3.8) | O1.6 |
| TV-S-11 | 2칸(0→2) | | 113,280골드(≈114,000) + 상급 7.55(≈7.6) | O1.6 |
| TV-S-12 | 거래 후 | | carve_bonus 0 | O5.5 |

### 12.8 경제 시뮬레이션 회귀 (online_econ3*.py와 같은 모형을 C#으로 옮겨 돌림. 1,500명, 허용: 평균 ±10%)
> 솔로: 경제 시뮬레이션 회귀(선택). 수치 조정 참고용.

+13에 처음 닿기까지(평균 / 중앙값 / 운 나쁜 10%), 기본 판 길이 (O2.6):

| ID | 플레이어와 방식 | +7 | +10 | +13 |
|---|---|---|---|---|
| TV-E-01 | 보통, 제물 +15, 골드 전부 | 4.4 / 3.9 / 7.1 | 19 / 16 / 35 | **65 / 59 / 112시간** |
| TV-E-02 | 보통, 제물 +15, 골드 70% | 6.1 / 5.3 / 10 | 27 / 23 / 49 | **90 / 80 / 157시간** |
| TV-E-03 | 보통, 제물 +15, +10부터 부적, 골드 전부 | 4.6 / 4.0 / 7.6 | 20 / 17 / 36 | 37 / 35 / 56 |
| TV-E-04 | 보통, 제물 +15, +10부터 부적, 골드 70% | 6.3 / 5.5 / 10 | 27 / 23 / 49 | 51 / 48 / 79 |
| TV-E-05 | 보통, 제물 없음, 골드 전부 | 6.5 / 5.3 / 12 | 41 / 35 / 75 | 220 / 192 / 399 |
| TV-E-06 | 입문, 제물 +15, 골드 전부 | 5.3 / 4.6 / 8.8 | 21 / 18 / 37 | 73 / 64 / 130 |
| TV-E-07 | 숙련, 제물 +15, 골드 전부 | 3.7 / 3.3 / 6.2 | 16 / 14 / 28 | 58 / 51 / 102 |
| TV-E-08 | 보통, 제물 +15, 골드 전부, 강화석 무제한 | | | 65 / 58 / 111 |

빠른 쪽 판 길이(+13): 보통 제물+15 골드 전부 44 / 40 / 75, 골드 70% 61 / 54 / 105, +10부터 부적 25 / 24 / 38, 제물 없음 147 / 128 / 265, 입문 51 / 45 / 89, 숙련 36 / 32 / 64 (TV-E-09~14).

+13 때 확장 슬롯 분포 (TV-E-15): 3칸 79%, 2칸 15%, 1칸 5%, 0칸 1%. +10부터 부적: 48 / 30 / 18 / 4% (TV-E-16). 시도 수 57 → 37 (O2.6).

여러 자루 (O2.6, sim/online_econ3_multi.py):

| ID | 골드를 쓰는 방법 | 첫 +13 |
|---|---|---|
| TV-E-20 | 한 자루 | 65 / 59 / 112 |
| TV-E-21 | 2·3·5·10자루 사 두고 가장 높은 한 자루만 | 64~66 / 56~59 / 111~115 |
| TV-E-22 | 2 / 3 / 5자루를 고르게 | 99 / 91 / 157, 134 / 127 / 201, 202 / 196 / 280 |
| TV-E-23 | 3자루 +5까지 뒤 하나 / 3자루 +7까지 뒤 하나 | 67 / 60 / 113, 74 / 67 / 122 |
| TV-E-24 | 5자루 / 10자루 +7까지 뒤 하나 | 78 / 70 / 130, 99 / 92 / 151 |
| TV-E-25 | 3자루 +10까지 뒤 하나 | 92 / 86 / 141 |
| TV-E-26 | 실패마다 다른 지팡이(2 / 3자루) | 84 / 75 / 150, 96 / 87 / 164 |
| TV-E-27 | 빠른 쪽: 한 자루 / 3자루 고르게 / 3자루 +7 뒤 하나 | 44 / 40 / 75, 88 / 84 / 131, 50 / 45 / 81 |
| TV-E-28 | **불변식**: 어느 방법도 한 자루보다 평균이 작지 않다(허용 1시간) | |

슬롯 낚시 (20만 자루, 제물 +4 도전부터, 허용 ±3%):

| ID | 올리는 곳 | 한 자루 골드 / 강화석 | 확장 ≥1 / ≥2 / 3 | 그런 한 자루 값(+0 포함) |
|---|---|---|---|---|
| TV-E-30 | +4 | 3,300 / 하급 4.3 | 38.5% / 2.3% / 0% | 1칸: 9,900골드, 하급 11 (2.6자루) |
| TV-E-31 | +5 | 6,900 / 하급 5.9 | 50.5% / 7.3% / 0% | 1칸: 14,700, 하급 12. 2칸: 101,700, 하급 80 (13.7자루) |
| TV-E-32 | +7 | 25,100 / 하급 6.8, 중급 3.9 | 71.5% / 26.0% / 3.8% | 2칸: 98,600, 하급 26 (3.8자루). 3칸: 680,000 (26.5자루) |

시간당 수입 (O2.3, 기본 판 길이, 괄호는 빠른 쪽, 허용 ±10%):

| ID | 판의 모양 | 판당 골드 | 판 길이 | 시간당 순골드 | 시간당 강화석 하/중/상 | 부적 |
|---|---|---|---|---|---|---|
| TV-E-40 | 1에서 시작, 9에서 끝 | 1,010 | 15분 (12) | 약 3,500 (4,300) | 4.1 / 0 / 0 | 0 |
| TV-E-41 | 1→14 | 2,250 | 23분 (18) | 약 4,900 (6,300) | 5.9 / 1.9 / 0 | 0.07 |
| TV-E-42 | 10→24 | 3,770 | 26분 (18) | 약 7,400 (10,500) | 4.2 / 7.0 / 0.8 | 0.19 |
| TV-E-43 | 20→30 | 3,550 | 21분 (14) | 약 8,800 (13,000) | 0 / 8.4 / 4.1 | 0.44 |
| TV-E-44 | 20→40 | 8,020 | 41분 (26) | 약 9,900 (15,900) | 0 / 9.1 / 4.8 | 0.52 |

보통(k=1.74) 단계별 (TV-E-50): +0 도달 7·시작 1·12분·3,100·5.1/0/0·0 / +3 14·1·23분·4,900·5.9/1.9/0·0.06 / +5 19·10·16분·6,200·6.6/6.0/0·0 / +7 23·10·24분·7,300·4.5/7.2/0.8·0.18 / +9 28·10·33분·7,700·3.3/8.0/1.1·0.22 / +10 31·20·23분·8,800·0/8.1/4.0·0.43 / +13 38·20·37분·9,200·0/8.4/3.5·0.46. 빠른 쪽 순골드 3,700 / 6,300 / 8,500 / 10,300 / 11,300 / 13,300 / 14,800.

민감도 (TV-E-60, sim/online_econ3_sens.py): 중급 드랍 1/5 → +13 65시간 그대로. 중급 1/10·상급 1/3 → +10이 19 → 27시간, +13은 67시간.

### 12.9 웨이브 골드와 원정 지갑 (M6.1, O2.1)
> 솔로: 전부 적용.

| ID | given | when | then |
|---|---|---|---|
| TV-G-01 | w = 1, 5, 9, 14, 42 | WaveGold | 52, 100, 148, 200, 420 |
| TV-G-02 | w = 10(가마등), 20, 30, 50 | WaveGold | 400, 600, 800, 1,200 |
| TV-G-03 | 웨이브 1부터 누적 5, 8, 9, 10, 14, 20, 30, 40, 50, 60 | Σ | 380, 752, 900, 1,300, 2,048, 3,768, 7,160, 11,472, 16,448, 21,628 |
| TV-G-04 | 프로필 첫 웨이브 10(허상 첫 만남) | 그 웨이브 골드 | 20(보스 처치 골드 0) |
| TV-G-05 | S(10), S(20), S(30), S(50) | 보스 처치 골드 | 380, 580, 780, 1,180 |
| TV-G-06 | 남은 결정 3개 | 웨이브 끝 | +15골드 |
| TV-G-07 | 가져가지 못한 블록 일반 2, 희귀 1, 영웅 1(빌린 블록) | 정산 | 10 + 15 + 50 = 75골드 |
| TV-G-08 | 원정 지갑 1,000, 판 중 회복 지출 150 | 정산 | 프로필 골드 +850 |
| TV-G-09 | 건너뛴 웨이브 1~20 | 골드 | 0 |

### 12.10 상점, 감정가, 수수료, 떠나보내기
> 솔로: TV-SH-*, TV-AP-*, TV-RL-01·04·05·08·09 적용. TV-RL-02·03·06(결투·거래 잠금·검증 대기 부분)·07, TV-F-*, TV-T-* 는 솔로 제외(원정 중 거절만 적용).

| ID | given | when | then | 출처 |
|---|---|---|---|---|
| TV-SH-01 | 골드 30,000, 이번 주 부적 0개 | 부적 3개 구매 | 골드 0, 부적 +3 | O2.4 |
| TV-SH-02 | 이번 주 3개 산 뒤 | 1개 더 | `WeeklyLimit` | O2.4 |
| TV-SH-03 | 다음 주 월요일 00:00 지나 | 1개 | 성공 | [임시] |
| TV-SH-04 | 오프라인, 기기 시계 기준 | 같은 한도 | 같은 결과 | O8.1 |
| TV-SH-05 | 골드 500 | char_3 +0 구입 | 골드 0, 새 지팡이(+0, 빈 조립, 주인 1번째, origin Shop), char_3 보관 수 +1 | O1.6 |
| TV-SH-06 | 골드 499 | +0 구입 | `NotEnoughGold` | |
| TV-SH-07 | 지팡이 1,000자루 | +0 구입 | 성공(한도 없음) | D1.22 |
| TV-SH-08 | 서고 한도 100, 골드 2,000 | 서고 확장 | 한도 150 | O2.4 |
| TV-SH-09 | 서고 100/100(끼운 5개 제외) | 정산에서 블록 2개 귀환 | 2개 골드로(M6.1 값) | O2.4 |
| TV-SH-10 | 오프라인 | GetStoreAsync | 유료 상품 0개 | O9.6 |
| TV-AP-01 | +0 지팡이 | 감정가 / 떠나보내기 | 250 / **0** | O5.5, O1.6 |
| TV-AP-02 | +5 / +7 / +10 / +13 | 감정가 | 3,400 / 12,494 / 64,624 / 258,309 | 6.6절 [임시 값] |
| TV-AP-03 | +5 | 떠나보내기 | 340골드 | O1.6 |
| TV-AP-04 | 감정가 대비 | 돌려받음 | = 평균 강화 골드의 5%(+5: 6,800 × 5% = 340) | O1.6 |
| TV-RL-01 | char_1 지팡이 1자루 | 떠나보내기 | `LastWand` | O1.6 |
| TV-RL-02 | char_1 2자루, 1자루는 장터 등록 중 | 남은 1자루 떠나보내기 | `LastWand`(장터 것은 세지 않는다) | O1.6 |
| TV-RL-03 | 우편함에 안 꺼낸 지팡이 + 손에 1자루 | 손의 것 팔기 | 거절 | O1.6 |
| TV-RL-04 | +7 지팡이 | 이름 입력 없이 떠나보내기 | 거절(`NameConfirmRequired`) | O1.6 |
| TV-RL-05 | 한꺼번에: +3 변이 없음, +2 확장 1칸, +4 | 고르기 | +3만 고를 수 있음 | O1.6 |
| TV-RL-06 | 원정 중 / 결투 중 / 거래 잠금 6일째 / 검증 대기 | 떠나보내기 | 각각 거절 | O1.6 |
| TV-RL-07 | 계정에 갚을 것 1건 | 어느 지팡이든 떠나보내기 | 거절 | O3.2 |
| TV-RL-08 | 든 지팡이(+5)를 떠나보냄, 남은 +7, +3 | 결과 | 든 지팡이 = +7 | O5.5 |
| TV-RL-09 | 끼운 블록 3개 + 겉모습 | 떠나보내기 | 블록 서고 +3, 겉모습 옷장 | O1.6 |
| TV-F-01 | 장터 블록 판매 10,000 | 수수료 | 500 | O2.4 |
| TV-F-02 | 장터 지팡이 판매 100,000 | 수수료 | 10,000 | O2.4 |
| TV-F-03 | 장터 등록 50,000 | 보증금 | 500, 팔리면 반환, 48시간 뒤 미판매면 소멸·물건은 우편 | O5.5 |
| TV-F-04 | 직접 거래 A 줌 감정 8,000, B 줌 감정 12,000(지팡이 없음) | 수수료 | 12,000 × 5% = 600, 양쪽 300씩 | O5.5 |
| TV-F-05 | 계정 누적 획득 골드 100,000 | 하루 골드 주기 | 20,000까지 | O5.5 |
| TV-T-01 | stage 8, floor 10, record 10, aura 2, carve 1, show_scars true, owner_count 1 | 거래 | stage 8, floor 8, record 10, aura 0, carve 0, show_scars false, owner_count 2, 7일 잠금, 끼운 블록 0 | O5.5 |
| TV-T-02 | TV-T-01 뒤 새 주인이 +8에서 실패 | | +7로 하락(바닥 = 8 − 2 = 6) | O5.5 |
| TV-T-03 | TV-T-01 뒤 장터 상세 | ForOthers(MarketDetail) | record_peak 10, drop_count 표시(show_scars false여도) | D1.15 |
| TV-T-04 | +0 지팡이 | 장터 등록 | 거절(+1 이상만) | O1.6, O5.5 |

### 12.11 알림
> 솔로: TV-N-05만 적용(오프라인 null). 나머지 솔로 제외.

| ID | given | when | then |
|---|---|---|---|
| TV-N-01 | 서버, +9 → 성공 +10 | NoticeFor | Plus10to12 |
| TV-N-02 | 서버, +8 → 대성공 +10 | | Plus10to12 한 번 |
| TV-N-03 | 서버, +12 → 성공 +13 | | Plus13 |
| TV-N-04 | 서버, +11 실패 / 변이 / 하락 | | null |
| TV-N-05 | 오프라인, +12 → +13 | | null, 결과 카드에 "서버 전체에 알렸다" 줄 없음 |
| TV-N-06 | 서버, +10에서 하락 후 다시 +10 | | Plus10to12(또 알림) |
| TV-N-07 | show_name_in_alerts = false | 알림 문구 | "어느 제자의 …" |
| TV-N-08 | 받는 쪽 큐에 +10~12 3건이 0초에 | 표시 시각 | 0, 6, 12초 |
| TV-N-09 | +10~12 1건 표시 중(0초)에 +13 도착(1초) | | +13이 다음 차례(맨 앞) |
| TV-N-10 | 밀린 +10~12 11건 | | 한 줄 "방금 +10 이상 성공 11건" |
| TV-N-11 | 받는 사람이 무한 웨이브 전투 중 | | 띄우지 않고 다음 야영 화면으로 미룸 |
| TV-N-12 | 설정 "+13만" | +11 알림 | 표시 안 함 |

### 12.12 건너뛰기·빠른·이어서 강화
> 솔로: 전부 적용.

| ID | given | when | then |
|---|---|---|---|
| TV-Q-01 | 누름 뒤 0.2초(12틱)에 J | 건너뛰기 | 무시 |
| TV-Q-02 | 누름 뒤 0.3초(18틱) 이후 J | | 판정으로 넘어가 짧은 결과 0.5초 |
| TV-Q-03 | 변이 카드 중 J | | 넘어가지 않음 |
| TV-Q-04 | 빠른 강화 켬, stage 9(+10 도전) | 연출 형식 | 전체 의식(설정에서 풀지 않았으면) |
| TV-Q-05 | 빠른 강화 켬, stage 5 | | 충전 0.3초, 시도 약 0.8초 |
| TV-Q-06 | 이어서 강화, 목표 +7, +5에서 대성공 | | 멈춤(대성공) |
| TV-Q-07 | 이어서, +3 → +4 성공 | | 멈춤(마일스톤 +4) |
| TV-Q-08 | 이어서, 같은 속성 일반 블록이 서고에 2개뿐(구성 3개) | 다음 시도 | 멈춤(제물 부족) |
| TV-Q-09 | 이어서, "하락에서 멈춤" 켬 | FailDrop | 멈춤 |
| TV-Q-10 | 이어서 | 결과 요청 | 시도마다 1회 요청(한 번에 여러 결과 없음) |

### 12.13 프로필과 저장
> 솔로: TV-PF-01~06, 09, 11 적용. 07·08·10·12·13 솔로 제외.

| ID | given | when | then |
|---|---|---|---|
| TV-PF-01 | 처음 실행, 네트워크 끊김 | 오프라인 프로필 | 자동 생성, 8.4절 시작 상태와 같음(지팡이 5, 마력탄 5 끼움, 골드 0) |
| TV-PF-02 | 네트워크 끊김 | 처음 실행 → 판 → 정산 → 강화 → 다시 실행 | 통과. 서버 주소로 나간 호출 0(상태 파일 읽기 시도만 예외, 실패해도 조용히) |
| TV-PF-03 | 저장 3세대 있음, 최신 파일 체크섬 깨짐 | 불러오기 | 직전 세대로 되돌리고 알림 |
| TV-PF-04 | 저장 중 강제 종료 | 다시 실행 | 이전 완전한 파일로 열린다(임시 파일 + 이름 바꾸기) |
| TV-PF-05 | 오프라인 강화와 서버 강화(같은 입력·같은 roll 주입) | EnhanceResult | 필드 단위로 같다(난수 출처만 다름) |
| TV-PF-06 | 오프라인 프로필 | 서버로 올리기/바꿔 주기 API | 존재하지 않는다 |
| TV-PF-07 | 서버 프로필, 접속 끊김 | 타이틀 | 서버 프로필 열 수 없음, 오프라인으로 놀 수 있음 |
| TV-PF-08 | 서버 프로필로 시작한 판 중 접속 끊김 | 판 | 끝까지 하고 나중에 제출 |
| TV-PF-09 | 오프라인 메뉴 | 광장·거래·결투·시즌 | "서버에 접속하면 열립니다" 묶음, 흐린 버튼 없음 |
| TV-PF-10 | 같은 requestId로 EnhanceAsync 두 번(서버) | | 두 번째는 첫 결과를 그대로, 재화 한 번만 빠짐 |
| TV-PF-11 | 판 중(같은 기기) | 강화 화면 열기 | 불가 |
| TV-PF-12 | char_1 지팡이 A 원정 중, 다른 기기에서 지팡이 B 강화 | 서버 | 허용(A만 잠김) |
| TV-PF-13 | 검증 대기 골드로 강화 | 지팡이 | 검증 통과까지 거래 잠금, 강화 자체는 됨 |

### 12.14 결제
> 솔로 제외(결제 없음). TV-PAY-03·10·13의 "유료 탭을 그리지 않는다"만 해당.

| ID | given | when | then |
|---|---|---|---|
| TV-PAY-01 | `payments_enabled = false` | CreateOrder 직접 호출 | 거절(제공자 호출 0회) |
| TV-PAY-02 | NullPaymentProvider | IsEnabled | false |
| TV-PAY-03 | 상품 0개 | 상점 화면 | 별빛 탭·잔액·구매 버튼·안내문 없음 |
| TV-PAY-04 | FakeReceipt, 같은 영수증 두 번 | ConfirmOrder | 한 번만 지급(provider_ref 유일) |
| TV-PAY-05 | FakeOrder, 같은 알림 두 번 | Webhook | 한 번만 지급 |
| TV-PAY-06 | 같은 order_id 두 번 Confirm | | 처음 결과 반환 |
| TV-PAY-07 | 주문 생성 후 30분 경과, 미승인 | 상태 | Cancelled |
| TV-PAY-08 | 결제창 닫고 게임 종료, 제공자는 승인 | 다음 접속 | "승인됨" 주문 마저 지급 |
| TV-PAY-09 | 쓴 교환 코드 | 다시 Confirm | 거절 |
| TV-PAY-10 | 유료 별빛 100 지급 후 80 사용, 환불 | 잔액 | 유료 −80, 0 이상이 될 때까지 별빛 사용 막음, 골드·진행 그대로 |
| TV-PAY-11 | 무상 30 + 유료 50, 40 사용 | 차감 | 무상 30 → 0, 유료 50 → 40 |
| TV-PAY-12 | `bound = true` 물건 | 거래 창에 올림 | 거절(결제 없이도 지금 켜져 있음) |
| TV-PAY-13 | 오프라인 | CreateOrderAsync | 호출되지 않는다(OfflineBackend에 결제 코드 없음) |
| TV-PAY-14 | 정적 검사 | 강화·드랍·매치·리그·순위 코드 | 별빛·origin·entitlement.source 참조 0 |

---

## 13. 모순과 고른 값

| ID | 내용 | 고른 것 | 근거 |
|---|---|---|---|
| M-1 | 제물 상한 +40에 "같은 속성 +5"가 포함되는가. S8 4.8·C-01: 합계(등급+속성) ≤ 40. S13 9.4: 같은 속성 +5는 상한 **밖**(그 문서의 C-07) | **S8(합계 ≤ 40)** | 강화 규칙의 주인 명세는 S8이고 테스트 벡터 TV-P-04(=88)가 S8 해석으로 적혀 있다. S13 해석이면 93. 구현 전 사용자 확인 권장 |
| M-2 | 변이 **거절** 때 기운. S13 9.7: "변이(받든 거절하든) → 기운 +1" | **S8**: 거절은 fallback 분기를 그대로(부적이 막으면 기운 그대로) | TV-B-09: (9,9,1,부적1) 변이 거절 → aura 1 그대로 |
| M-3 | 판정 경계. 이름 표 `str.enhance.line.roll` "뽑힌 값 {roll} / {p} 이하면 성공", S13 9.7 "{roll:0.0} / {p:0.0} 이하면 성공", 꾸러미 `rollTenths 0..1000` | 판정은 **미만**(`roll_bp < p_bp`, bp 0~9999), 소수 둘째 자리 표기 | S8 4.4·C-07·U-20. 이름 표 문구는 "이하"로 남아 있어 그대로 쓰면 틀린 안내가 된다 → 문구 열쇠 값 수정 필요(이름 표 영역 소관) |
| M-4 | 바닥 표시. S8: `max(3, floor_peak−2)`. S13: `Floor = max(0, best−2)`, `DropPossible = stage>3 && stage>Floor` | 판정은 둘이 같다. 표시는 S8 `max(3, …)` | S8 4.6 |
| M-5 | 바닥 기준 값. S13은 `bestStage`(역대 최고 하나)로 바닥을 계산, S8은 `floor_peak`(바닥용)과 `record_peak`(흔적용)을 나눔 | S8 두 칸 구조. 솔로는 거래가 없어 두 값이 항상 같다 | S8 C-13 |
| M-6 | 체크섬. S8 8.5 "CRC32/SHA-256", C 11.3 "마지막 줄에 본문의 SHA-256" | SHA-256 마지막 줄 | CONTRACTS가 형식의 주인 |
| M-7 | 부적 id. S7 13.5 `mat.charm_guard`, S8 `charm_guard`, S14·C·이름 표 `charm.safeguard` | `charm.safeguard` | C 2.1 대응표 |
| M-8 | 캐릭터·재료·블록 id. S8은 `char_1`, `stone_low`, `giant_fireball` 등 옛 표기 | 정본 `char.1`/정수 1, `mat.stone_low`, `block.fireball` | C 0.3·2.1 |
| M-9 | 대성공 부적. S8 4.5 표 "쓰지 않음(돌려준다 [임시])" vs U-17 "소모하지 않고 칸에 남는다" | 소모 안 함, 칸에 남음 | 같은 뜻 |
| M-10 | 기댓값 예시. effects 그림 "+7(기운 2, 최고 +8)에서 +10까지 평균 14회" vs 계산 30.40회 | 계산값 30.40 | S8 C-04 |
| M-11 | 마일스톤에 +13 포함 여부. S8 4.16: +4/+7/+10/+13. S13 9.7: Milestone은 +4/+7/+10, +13은 Plus13 별도 | 둘 다: +13은 Plus13 연출, 이어서 강화 멈춤 조건에는 +13 포함 | 실질 차이 없음 |
| M-12 | 상점 +0 지팡이 sku. S8 `wand_plus0(character)`, S14 `item.wand_box` | `item.wand_box` + 캐릭터 인자 | S14 |

## 14. 미정 목록

| 항목 | 상태 |
|---|---|
| 변이 6종의 이득·대가 **설명 문구** 이름 표 열쇠 | 이름 표에 없음(이름만 `mutation.*`). 미정 |
| 변이 후보 고르기 방식 | [임시] 조건 만족·미보유 6종 중 균등 1개 (U-02·U-03) |
| 같은 변이 중복 보유 | [임시] 불가 |
| 변이 결정 대기 저장·제약 | [임시] `pending_offer`, 결정 전 강화·새기기·제거·떠나보내기 불가, 원정 가능 (U-04, U-26) |
| 슬롯 새기기 성공 시 누적 초기화 | [임시] `carve_bonus = 0` |
| 변이 제거가 기운·새기기에 주는 영향 | [임시] 없음 |
| 비용 환불 | [임시] 어떤 결과든 비용·제물 돌려주지 않음 |
| 감정가 표 값 | [임시] 7.4절(마르코프 제물 +15) |
| 반올림 | [임시] 감정가 반올림, 환급 내림 |
| 상점 주의 경계 | [임시] 기기 현지 월요일 00:00 |
| 골드 꾸미기 목록·값 | 미정(`shop_cosmetics.json` 비워 둠) |
| "보유 재화로 평균 N번" 계산법 | [임시] 앞으로 굴리기, 최대 1,000 |
| 기댓값 목표 기본값 | [임시] 다음 마일스톤 |
| 이어서 강화의 "같은 제물 구성" | [임시] 같은 block id → 없으면 같은 (등급, 속성) |
| 이어서 강화 "하락에서 멈춤" 기본값 | [임시] 켬 |
| 이어서 강화 실패 수 집계 | [임시] 변이·부적 막음·하락 없음 포함 |
| 든 지팡이 자동 지정 동률 | [임시] 단계 → record_peak → 최근 |
| +13 지팡이 강화 화면 | [임시] 강화 버튼 꺼짐, 나머지 기능 됨 |
| 실패 뒤 침묵 2~4단 | [임시] 0.3 / 0.4 / 0.5초 |
| 슬롯 낚시 방지 손잡이 `great_slot_min_stage` | [임시] 0(제한 없음) |
| "폭발 블록"의 정의 | [임시] `block.fireball`(`flags`에 `explodes`) |
| 아이템 레벨 높은 순 정렬(솔로) | 미정(IL은 리그용. 넣으면 S8 10.4 장착 IL식) |
| 강화 수치 JSON 경로·파일명 | 미정(현재 `data/`에 없음. 2절 키로 새로 만든다) |
| 대성공의 "가짜 실패" 범위 | 원문 확인 필요 Q-01: 대성공은 항상 실패처럼 시작(적용됨) |
| 고드름 홀 "한 번도 녹지 않은" 꼬리표 | Q-03: `drop_count==0 && stage≥10 && character==4`로 판정 가능하게만 둠. 다섯 지팡이 공통 여부 미정 |
