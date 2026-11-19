# 12. 기본 공격 유형 — 캐릭터마다 셋, 고유 비기 강화, 표시 이벤트 (정본)

사용자 지시(2026-11-15): "모든 캐릭터의 공격 방식이 똑같다. 다 다르게 — 하나는 쏘고, 하나는 에너지를 방출하고, 하나는 책을 던지고. 책 페이지가 촤라락
펼쳐져 소용돌이, 땅을 쳐서 뭔가 솟아오르는 임팩트. 캐릭터당 기본 공격 유형을 세 개로." DECISIONS 1.2("캐릭터 5명, 전용 지팡이·고유 능력·스탯 전부 다르다")와 맞는다.

불변식은 그대로다: 강화 규칙·판 중 강화 금지(이 작업은 강화와 무관), 위험색은 적 전용(유형의 그림은 속성색만), 결정론(난수 없음 — 유형 동작은 상태만으로
정해진다, 새 상태는 전부 세계에 있다), 화면 글자는 이름 표에만(`Resources/NamesSolo/AttackStyles.ko.json`), id는 더하기만.

다른 작업이 이 문서를 보고 그림·소리를 잇는다: **3절 SimEvent 표**와 **4절 비기 단계 표**가 계약이다.

---

## 1. 고르기와 판 시작

| 무엇 | 규칙 | 자리 |
|---|---|---|
| 유형 = 핵 블록 | 캐릭터마다 A·B·C 세 핵 블록 `block.core.<이름>`. 투사체 블록(`kind: projectile`)이라 컴파일러·변형·트리거·거울상·별(★)·사본 마법진을 그대로 탄다 | `Resources/DataSolo/AttackStyles.json` `characters`, `blocks` |
| 블록 목록 | `ContentDb`가 `Block(id)` 조회에만 더한다. `Blocks`·`BlocksOfRarity`에는 넣지 않는다 → 전리품·서고·제물·상점에 나오지 않는다 | `Data/ContentDb.cs` `LoadAttackStyleBlocks` |
| 판 시작 | 출발 조립의 코어가 `replace_cores`(다섯 캐릭터 공통 시작 블록 `block.magic_bolt`)이면 고른 유형의 핵으로 바꾼다. 일부러 코어에 끼운 다른 투사체(화염구 등)는 그대로. 다른 캐릭터의 유형 id는 무시 | `Sim/Run/RunFlow.cs` `StartRun` → `Core/Sim/AttackStyles.cs` `EffectiveStyle`, `ApplyToLoadout` |
| 판 설정 | `RunConfig.AttackStyle`(더함). 원정 준비가 고른 값을 싣고, `WandService.BaseConfig`가 프로필의 마지막 선택을 싣는다(메인 메뉴 연습·튜토리얼) | `Core/Sim/RunTypes.cs`, `Meta/Inventory/WandService.cs`(한 줄) |
| 튜토리얼 | 손잡이 `tutorial_uses_default_style`이 켜져 있으면 튜토리얼 판은 그 캐릭터의 A. 명세 값 꺼짐, FunPass가 켠다 | `AttackStyles.json`, `DataSolo/FunPass.json` `attack_style.tutorial_default` |
| 프로필 | `characters[].attack_style`(더함). 없으면(옛 저장) A. 고르는 즉시 저장, 출발할 때도 적는다 | `Core/Meta/ProfileData.cs` `CharacterRecord.AttackStyle` |
| 해금 손잡이 | `unlock.enabled`(기본 **꺼짐** = 셋 다 처음부터 — 사용자가 바로 QA). 켜면 그 캐릭터로 넘긴 최고 웨이브가 `min_best_wave`(a 0, b 5, c 10) 이상일 때 열린다 | `AttackStyles.json` `unlock` |
| 원정 준비 화면 | 캐릭터 카드 아래 카드 3장(아이콘·이름·한 줄 설명·특징 꼬리표·"기본 공격 · 기본/둘째/셋째"). 고른 카드는 아랫변 속성색 줄, 잠긴 카드는 어두운 덮개 + "웨이브 n을 넘기면 열린다". 이 줄을 넣느라 캐릭터 카드 높이 376 → 288, 아래 판들 y 504 → 536 | `Screens/Prep/ExpeditionPrepScreen.cs`, `ExpeditionPrepModel.StyleCards/SelectStyle` |

비기 단위: 핵 블록의 `standard_t_base` = 10(마력탄과 같다) → 표준 T·비기·패시브 피해는 유형과 무관하게 명세 그대로. 비기·사본 마법진이 트리를 돌릴 때(J 발사가 아닌 실행)
핵 블록 피해에 `art_damage_scale`(≈ 10 ÷ 그 유형의 발사 1회 피해)을 곱한다 → 마력 방출(2T)·부채꼴·팔방·만개·사본 마법진의 총량이 마력탄 때와 같은 자릿수.

---

## 2. 유형 15종 — 동작과 수치

수치는 전부 `AttackStyles.json`(초·칸·도·비율). 아래 값은 이 문서를 쓴 때의 값이다(측정은 5절).
공통: 피해 속성 = 캐릭터 속성(EL-1: 맞힌 탄 블록의 속성), 한 번의 발동(폭발 하나·부채꼴 한 번·탄의 한 다리)에서 둘째 적부터 `extra_target_mul`,
J 발사 탄은 첫 적중에 `combo_on_hit` 콤보를 더 준다(느린 유형의 결정 속도를 마력탄과 맞춤 — 적중 콤보 +1/+2·발사당 +3 상한은 그대로).

| 제자 | 자리 | id (이름) | 동작 | 간격 s / 열 / 피해 | 핵심 수치 |
|---|---|---|---|---|---|
| char.1 | A | `block.core.ember_mortar` 화로 포탄 | **lob**: 앞 ±30°·7칸 안 가장 가까운 적(없으면 5칸 앞, 가장 가까운 거리 1.5)에 0.4초 포물선 → 반경 1.4 폭발 → 반경 1.1 불바닥 1초(0.25초마다 피해 ×0.06). 날아가는 동안 맞지 않는다 | 0.35 / 16.2 / 20.7 | 둘째 적 ×0.3, 콤보 덤 1 |
| char.1 | B | `block.core.flame_jet` 불꽃 방사 | **cone**(쏜 사람에 붙음, 반지름이 4틱에 퍼짐): 앞 ±28°·2.6칸. 맞을 때마다 화상 +1겹(최대 5, 1.5초, 0.25초마다 겹 × 맞힌 피해 ×0.05) | 0.1 / 5.5 / 7.2 | 둘째 적 ×0.35 |
| char.1 | C | `block.core.bouncing_ember` 튀는 불씨 | **ricochet**: 속도 16·사거리 9의 작은 탄. 적을 맞히면 3.5칸 안 다른 적에게 튐(2번), 벽 튕김 2번 내장. 화상 +1겹(최대 2) | 0.18 / 5 / 10.4 | 튄 적 ×0.35 |
| char.2 | A | `block.core.chain_bolt` 사슬 번개 | **chain**(연쇄 번개 동작 그대로): 즉발 선 4.5칸(반폭 0.2) → 2.5칸 안으로 2번 옮겨 붙음(×0.35씩). 늑대 회피 무시 | 0.15 / 4 / 10 | 사거리 짧음 |
| char.2 | B | `block.core.thunder_orb` 번개 구체 | **orb**: 속도 4.5·반지름 0.3, 적을 꿰뚫고 지나감(적마다 한 번) + 0.2초마다 겉면 1.6칸 안 적 2마리를 지짐(×0.26) | 0.4 / 16.6 / 13.8 | 콤보 덤 1 |
| char.2 | C | `block.core.sky_strike` 낙뢰 | **strike**: 앞 ±40°·6.5칸 안 가장 가까운 적 발밑(없으면 4.5칸 앞)에 0.35초 예고 → 반경 1.3 벼락 | 0.35 / 10.7 / 21.7 | 콤보 덤 2 |
| char.3 | A | `block.core.page_flurry` 책장 날리기 | **pages**: 한 발 = 책장 3장(−20°·0°·+20°), 바깥 장은 초당 110°로 가운데 쪽으로 휜다. 장마다 1번 꿰뚫음 | 0.3 / 8 / 6.3(장당) | 꿰뚫은 적 ×0.5 |
| char.3 | B | `block.core.tome_throw` 책 던지기 | **boomerang**(부메랑 동작 그대로): 반지름 0.3 무거운 책, 편도 5칸 갔다 돌아오며 다리마다 적마다 한 번(두 번 맞힘) | 0.35 / 10.8 / 11 | 다리마다 둘째 적 ×0.35 |
| char.3 | C | `block.core.rune_mine` 룬 문장 | **mine**: 앞 2.5칸(앞 ±30°·4칸 안 적이 있으면 그 발밑)에 새김 → 0.2초 뒤 무장 → 겉면 0.6칸 안에 적이 오거나 2.5초가 지나면 반경 1.5 폭발. 동시 5개(넘으면 가장 오래된 것이 바로 터진다) | 0.4 / 10 / 22.9 | 콤보 덤 2 |
| char.4 | A | `block.core.frost_lance` 얼음 창 | **lance**: 속도 8·사거리 10, 1번 꿰뚫음. 멈추는 곳(관통이 다한 적·사거리 끝·벽)에서 파편 3갈래(±25°, `block.core.frost_lance_shard` 피해 8.5·사거리 2.5)로 깨짐. 창·파편 모두 감속(30%/20%, 1초) | 0.35 / 13.8 / 24.2 | 콤보 덤 2 |
| char.4 | B | `block.core.blizzard` 눈보라 | **cone**(붙음, 6틱): 앞 ±45°·2.2칸. 맞을 때마다 냉기 +1겹(겹 × 7% 감속, 1.2초), 6겹이면 0.8초 빙결 후 0 | 0.15 / 3.2 / 9.6 | 둘째 적 ×0.35 |
| char.4 | C | `block.core.icicle_fall` 고드름 낙하 | **icicles**: 줄 방향 = 앞 ±25°·5칸 안 적 쪽(없으면 시야). 0.8·1.8·2.8·3.8칸에 0.3초 뒤부터 0.08초 간격으로 반경 0.6 고드름. 감속 20% | 0.4 / 13.3 / 12.8 | 콤보 덤 1 |
| char.5 | A | `block.core.shadow_wave` 그림자 파동 | **cone**(제자리, 12틱에 퍼짐): 앞 ±35°·4칸, 전부 꿰뚫고 0.25칸 끌어당김 | 0.3 / 8 / 18.4 | 콤보 덤 1 |
| char.5 | B | `block.core.shadow_scythe` 그림자 낫 | **cone 각도 휩쓸기**(붙음, 6틱, 발사마다 번갈아 −75° → +75° / +75° → −75°): 2.4칸 근접 호. 둘째 적부터 맞힐 때마다 콤보 +1 | 0.33 / 10.7 / 21.1 | 콤보 덤 1 |
| char.5 | C | `block.core.eye_seeker` 눈알 추적탄 | **seeker**: 속도 7, 앞 ±180°·8칸 안 적을 초당 300°로 쫓는다. 처치하면 그 자리에서 2개(±30°, 피해 ×0.5, 한 세대만)로 갈라진다 | 0.33 / 10.7 / 21.1 | 콤보 덤 2 |

동작 공통: 바짝 붙은 적(겉면이 `min_distance` 안)은 시야와 무관하게 겨눈다(포탄·낙뢰는 그 방향으로 가장 가까운 거리에, 룬은 그 발밑에). 범위 타격은 겉면 거리 순(같으면 id 순)으로
맞히고 첫 적만 온전한 피해. 빈 땅 폭발은 트리거를 발동하지 않는다(U27과 같다).

### 2.1 공통 수치 칸(`style` 절)

| 칸 | 뜻 |
|---|---|
| `standard_t_base` | 표준 T의 바탕(10 = 마력탄). 비기 단위가 유형에 흔들리지 않게 |
| `art_damage_scale` | J 발사가 아닌 실행(비기·사본)에서 이 블록 피해에 곱함 |
| `extra_target_mul` | 한 발동에서 둘째 적부터의 피해 배율(무리 DPS ≤ 마력탄 × 1.8) |
| `estimate_hits` | 야영 수치판 허수아비 DPS 어림(과녁 하나가 발사 1회에 받는 피해 ÷ 기본 피해, 지속 피해 포함) |
| `combo_on_hit`, `combo_per_extra_hit` | J 발사 탄의 첫 적중 콤보 덤(발사당 탄 하나에 한 번), 둘째 적부터의 덤(낫) |
| `pierce_bonus_per_count` | 이미 꿰뚫는 유형에서 관통 수 1당 피해 보너스 |
| `echo_damage_mul` | 튕김 변형의 "되풀이" 피해 배율 |

### 2.2 변형·제어 블록이 핵에 붙는 뜻 (컴파일러는 그대로, `SpellSystem.Styles.cs` `InitStyle`)

| 블록 | 몸 탄(튀는 불씨·번개 구체·책장·책·얼음 창·추적탄) | 범위 유형(포탄·부채꼴·낙뢰·고드름·룬) | 사슬 번개 |
|---|---|---|---|
| 2·3갈래 분열 | 탄을 부채로 N발(책장은 발마다 3장) | 포탄·낙뢰·룬·고드름 줄·부채꼴을 부채로 N개 | 선 N개 |
| 탄환 크기 2배 | 탄 반지름 ×2(+20% 피해) | **범위 반경 ×2**(폭발·불바닥·부채꼴·지짐·발동 반경, +20% 피해) | 옮겨 붙는 반경 ×2 |
| 관통 +2 | 관통 수 +2(책 던지기·번개 구체는 이미 꿰뚫음 → 피해 보너스) | **피해 +10%(블록) × (1 + 0.05 × 관통 수)** — 관통 대신 | **옮겨 붙기 +1**(블록당) |
| 벽 튕김 | 벽 튕김 +2(책은 돌아오므로 **편도 사거리 +1칸**) | **되풀이**: 포탄 = 떨어진 뒤 2칸 더 튀어 터짐(×0.6) / 부채꼴 = 한 번 더 휩씀(×0.5) / 낙뢰 = 같은 자리에 0.3초 뒤 다시(×0.5) / 룬 = 터진 자리에 다시 새김(×0.5) / 고드름 = **+1개** | 옮겨 붙는 반경 +1칸 |
| 유도 | 조향(추적탄은 더 날카롭게) | **겨누기 원뿔이 유도 원뿔(±90°·6칸)로 넓어짐**, 포탄은 떨어질 자리가 쫓는 적을 따라감 | 선에 걸린 적이 없으면 원뿔 안 적을 겨눔 |
| 서리·잠식·과열 3배 | 블록 규칙 그대로(맞은 모든 적, 폭발 범위 포함 — U28) | 같음 | 같음 |
| 적중 시 발동 | 첫 적중에서 | 첫 피해 순간 그 중심에서(포탄·낙뢰·룬·고드름), 부채꼴은 첫 맞은 적 자리에서 | 첫 대상에서 |

→ 핵 블록에는 `W_NO_EFFECT`가 나지 않는다(`SpellCompiler.ModifierAffects`). 문구는 `BlockDescriber.Describe`(핵 한 줄) + `StyleExtras`(불바닥·화상·빙결·당김·콤보·둘째 적 배율) +
`OnCore(블록, 핵)`(위 표의 그 칸, 이름 표 `str.block_desc.on_core.*`) — 야영·전리품 화면이 `OnCore`를 부르면 "이 핵에서는 …" 줄이 나온다(지금 화면은 `Describe`만 부른다 — 화면 작업이 이으면 된다).

### 2.3 상태(결정론)

유형의 상태는 전부 세계에 있다: `ProjectileEntity`의 `StylePhase, TargetPos, Origin, AimDir, TimerTick, EndTick, ArmTick, StepIndex, StepCount, StyleHits, RicochetsLeft,
CurveLeftDegrees, AreaScale, RangeBonus, Generation, TargetEnemyId, ComboBonusGiven, StyleEnded`(주인 Spells)와 `EnemyState`의 `BurnStacks, BurnUntilTick, BurnNextTick,
BurnPerStack, BurnOwnerSlot, BurnIntervalTicks, ChillStacks, ChillUntilTick`(주인 Spells — 피해 적용과 같은 모듈), 고유 비기 강화는 `ArtPlayerState.Sequences`(주인 Arts).
난수를 쓰지 않는다. 솔로 빌드에는 세계 스냅숏·상태 해시가 없어(00 문서: Fix64·해시 제외) 스냅숏에 넣을 자리가 없다 — 대신 "같은 모듈 객체로 세 세계를 섞어 돌려도 같은 지문"
시험이 상태가 모듈 객체에 남지 않음을 지킨다(6절).

---

## 3. SimEvent 표 — 그림·소리 연결용 (`Core/Sim/SimEvents.cs` 260~, 280)

필드: S = Slot, E = EntityId, A/B = 정수, F = 실수, P = Pos, P2 = Pos2, Id = 문자열. **Id = 블록 id**(그림·소리 열쇠로 쓴다). 좌표는 시뮬레이션 좌표(표시 = `vc.ToView`).

| 이벤트 | 번호 | 필드 | 내는 때 | 그릴 것(제안) |
|---|---|---|---|---|
| `StyleLobLaunched` | 260 | S, E = 탄, Id, P = 출발, P2 = 떨어질 자리, A = 날아가는 틱, B = 몇 번째 튐(0 처음) | 포탄 발사, 튕김의 다음 튐 | 포물선 탄(상태의 `Pos`가 땅 위 진행, 높이는 표시가 A틱 동안 그린다), 떨어질 자리 작은 표식(위험색 금지) |
| `StyleBlast` | 261 | S, E = 탄, Id, P = 중심, F = 반경, A = 맞은 적 수, B = `StyleBlastKind` | 포탄 착탄(0)·불바닥 한 번(1)·낙뢰(2)·고드름(3)·룬 폭발(4) | 종류별 폭발. 0·2·4는 큰 폭발, 1은 바닥 일렁임, 3은 얼음 깨짐 |
| `StyleGroundSpawned` | 262 | S, E = 탄, Id, P, F = 반경, A = 남는 틱, B = 0 불바닥 / 1 룬 | 불바닥 시작, 룬 새김(튕김의 다시 새김 포함) | 불바닥 원(속성색 반투명), 룬 문양 |
| `StyleGroundExpired` | 263 | S, E = 탄, Id, P, B = 0 시간 끝 / 1 발동 | 불바닥 끝, 룬 터짐 | 사라짐 |
| `StyleConeStarted` | 264 | S, E = 탄, Id, P = 원점, P2 = 가운데 방향(단위), F = 반경, A = 반각(도), B = 휩쓰기(n > 0 반지름 퍼짐 n틱 / −n 각도 −반각 → +반각 / −(1000+n) 각도 +반각 → −반각) | 불꽃 방사·눈보라·그림자 파동·그림자 낫 한 번(튕김 메아리 포함). 붙는 유형(불꽃·눈보라·낫)은 원점이 쏜 사람을 따라간다 — 탄 상태의 `Origin`을 매 프레임 읽는다 | 부채꼴 불길·눈송이·어둠 파동·낫 궤적 |
| `StyleStrikeWarned` | 265 | S, E = 탄, Id, P = 떨어질 자리, F = 반경, A = 남은 예고 틱, B = 순번(고드름 줄) | 낙뢰 예고, 고드름 줄의 자리마다(발사 틱에 한꺼번에) | 하늘 번개 예고·고드름 그림자(**플레이어 쪽 예고 — 위험색 금지**, 속성색 점선) |
| `StyleRicochet` | 266 | S, E = 탄, Id, P = 튄 자리, P2 = 새 대상 위치, A = 새 대상 적 id | 튀는 불씨가 적에서 다음 적으로 | 불티 |
| `StyleZap` | 267 | S, E = 탄, Id, P = 구체, P2 = 맞은 적, A = 적 id | 번개 구체가 둘레를 지짐 | 짧은 번개 줄기(`ChainLightningArc` 그림 재사용 가능) |
| `StyleSplit` | 268 | S, E = 부모 탄, Id, P, A = 새 탄 수, B = 세대 | 눈알 추적탄이 처치로 갈라짐 | 눈 갈라짐 |
| `StyleShatter` | 269 | S, E = 탄, Id, P, A = 파편 수 | 얼음 창이 멈춘 곳에서 깨짐(파편은 `ProjectileSpawned` Id = `block.core.frost_lance_shard`) | 얼음 깨짐 |
| `StyleStatus` | 270 | S, E = **적 id**, A = 지금 겹 수(0 = 풀림), B = `StyleStatusKind`(0 화상 / 1 냉기), P = 적 위치 | 화상 겹 바뀜·풀림, 냉기 겹(빙결은 기존 `EnemyCc` Freeze) | 적 발밑 불씨 겹 표시, 서리 겹 표시 |
| `StyleMineArmed` | 271 | S, E = 탄, Id, P, F = 발동 반경 | 룬 무장 | 룬이 빛남 |
| `ArtStage` | 280 | S, Id = **비기 id**, A = `ArtStageKind`, B = 순번, P = 중심, P2 = 아래 4절, F = 반경 | 고유 비기 강화의 단계 | 4절 |

함께 쓰는 기존 이벤트: `ProjectileSpawned`(모든 핵 탄·책장 3장·파편·갈라진 탄, A = 슬롯), `ProjectileExpired`(A: 0 시간·사거리, 1 벽, 2 적중·발동 끝, 3 돌아옴), `ProjectileBounced`,
`ProjectilePassed`(번개 구체가 꿰뚫음), `ChainLightningArc`(사슬 번개), `EnemyDamaged`(화상 틱은 `DamageOrigin.Zone`이라 Art 표식 없음), `EnemyCc`(당김·빙결).
**지금 그림 대체**: `StyleBlast` 0·2·4는 기존 `Explosion`도 같이 낸다(**B = 1** = 유형이 낸 폭발, Id = 속성 id) — 전용 그림을 붙이면 `Explosion`의 B = 1을 걸러 두 번 그리지 않는다.
탄 몸은 `DataSolo/G16.json` `shots.block_fx_fallback`이 같은 속성의 있는 탄·흔적 그림에 잇는다(포탄·불씨 → fireball, 불꽃 → trace_fire, 구체 → crystal_lightning, 낙뢰 → hit_large_lightning,
책장 → special/scattered_pages, 책 → mana_bolt, 룬 → crystal_arcane, 창·파편 → ice_lance, 눈보라 → trace_ice, 고드름 → crystal_ice, 파동·낫 → trace_dark, 추적탄 → shadow_orb).

---

## 4. 고유 비기 강화 (게임 규칙, `Sim/Arts/ArtSystem.Upgrades.cs`)

비용·커맨드·원래 효과는 그대로. 덧피해 = `damage` × 시전 순간 T × 캐릭터 배율(비기 피해 `DamageOrigin.Art`, 캐릭터 속성, 콤보·임계 없음), **원래 총량의 +20% 이내**.
손잡이 `art_upgrades.enabled`(명세 기본 꺼짐, FunPass `attack_style.art_upgrades`가 켠다). 단계는 `ArtStage` 이벤트.

| 비기 | 원래 총량(적 하나) | 강화 | 덧피해(적 하나) | 단계 이벤트 (`ArtStageKind`) |
|---|---|---|---|---|
| 불가마 `art.kiln` (char.1) | 0.25T × 8 = 2T | 내리치는 순간부터 불기둥 고리 3개가 바깥으로 0.1초 간격으로 솟는다(반경 0~1.0, 1.0~1.9, 1.9~2.8, 적마다 한 번) | 0.4T (+20%) | `Rise` × 3: P = 중심, F = 바깥 반경, P2.x = 안 반경, B = 고리 번호 |
| 벼락 질주 `art.bolt_dash` (char.2) | 3T | 지나간 길을 5등분한 자리에 0.15초 뒤부터 0.07초 간격 낙뢰(반경 0.8, 적마다 한 번) | 0.6T (+20%) | 시전 때 `Warn` × 5(P = 자리, P2.x = 남은 틱), 떨어질 때 `Land` × 5 |
| 베껴 쓰기 `art.copy_circle` (char.3) | 사본 12 × 0.5T = 6T | 사본 마법진 둘레(2.6칸) 책장 소용돌이: 0.5초마다 0.35칸 끌어당기고 0.1T. 마법진이 사라지면 끝 | 최대 0.1T × 11 = 1.1T (+18%) | `Pulse` 맥박마다(B = 번호), 끝날 때 `End` |
| 얼음 병풍 `art.ice_screen` (char.4) | 파편 3T | 병풍 자리에서 시야 쪽으로 0.9칸 간격 얼음 가시 4개가 0.08초 간격으로 솟아 0.4초 기절(띄움) 뒤 0.6초 빙결(빙결 내성 규칙 그대로) | 0.6T (+20%) | `Rise` × 4: P = 가시 자리, P2 = 방향 |
| 그림자 손 `art.shadow_hand` (char.5) | 3T | 움켜쥔(원래 효과) 자리를 0.45초 뒤 짓눌러 반경 2.2 범위 피해 | 0.6T (+20%) | 시전 때 `Grip`, 0.45초 뒤 `Crush` |

`ArtStageKind`: 0 Warn(예고), 1 Rise(솟음), 2 Land(착지), 3 Blast(폭발 — 예약), 4 Grip(움켜쥠), 5 Crush(짓누름), 6 Pulse(맥박), 7 End(끝). 비기 이름 표 열쇠 `str.attack_style.art_upgrade.<비기 id>`(설명 한 줄).

---

## 5. 측정 (허수아비 방, FunPass 켬 = 게임 값, 6,000틱, 씨앗 7)

도구: `Tests/EditMode/Spells/AttackStyleKit.cs`(`Measure`, `BalanceTable`, 수치 찾기 `TuneHeat`). 정책: 꾹 = J 누르고만 있음 / 끊어 = 다음 발이 잠그면 손을 뗀다(임계 끊어 쏘기).
허수아비 앞 3칸(불꽃 1.8, 눈보라·낫 1.6칸). 무리 = 정면 하나 + 뒤 좌우 둘(±0.8칸, 0.7칸 뒤). 피해 = `EnemyDamaged` A(반올림)의 합.

| 캐릭터 | 유형 | 꾹 DPS | 끊어 DPS | 무리(3) 꾹 | 꾹/마력탄 | 끊어/마력탄 | 무리/마력탄 무리 | 끊어/꾹 | 콤보/초 |
|---|---|---|---|---|---|---|---|---|---|
| char.1 | 마력탄 | 48.7 | 62.7 | 48.7 | 1.00 | 1.00 | 1.00 | 1.29 | 4.9 |
| char.1 | 화로 포탄 | 45.8 | 62.2 | 72.5 | 0.94 | 0.99 | 1.49 | 1.36 | 5.2 |
| char.1 | 불꽃 방사 | 46.8 | 62.3 | 79.9 | 0.96 | 0.99 | 1.64 | 1.33 | 5.0 |
| char.1 | 튀는 불씨 | 45.7 | 64.0 | 78.9 | 0.94 | 1.02 | 1.62 | 1.40 | 4.1 |
| char.2 | 마력탄 | 49.6 | 64.3 | 49.6 | 1.00 | 1.00 | 1.00 | 1.30 | 5.0 |
| char.2 | 사슬 번개 | 49.6 | 64.3 | 73.6 | 1.00 | 1.00 | 1.48 | 1.30 | 5.0 |
| char.2 | 번개 구체 | 49.8 | 68.6 | 64.6 | 1.00 | 1.07 | 1.30 | 1.38 | 5.3 |
| char.2 | 낙뢰 | 48.1 | 66.5 | 82.5 | 0.97 | 1.03 | 1.66 | 1.38 | 5.6 |
| char.3 | 마력탄 | 43.2 | 62.5 | 43.2 | 1.00 | 1.00 | 1.00 | 1.45 | 4.3 |
| char.3 | 책장 날리기 | 41.2 | 69.7 | 54.5 | 0.95 | 1.11 | 1.26 | 1.69 | 5.4 |
| char.3 | 책 던지기 | 41.0 | 65.6 | 41.0 | 0.95 | 1.05 | 0.95 | 1.60 | 4.8 |
| char.3 | 룬 문장 | 41.9 | 65.3 | 71.0 | 0.97 | 1.05 | 1.64 | 1.56 | 4.7 |
| char.4 | 마력탄 | 44.2 | 62.4 | 44.2 | 1.00 | 1.00 | 1.00 | 1.41 | 4.4 |
| char.4 | 얼음 창 | 41.8 | 64.8 | 41.8 | 0.94 | 1.04 | 0.94 | 1.55 | 4.5 |
| char.4 | 눈보라 | 46.3 | 62.8 | 75.4 | 1.05 | 1.01 | 1.71 | 1.36 | 4.7 |
| char.4 | 고드름 낙하 | 41.9 | 66.6 | 55.4 | 0.95 | 1.07 | 1.25 | 1.59 | 4.2 |
| char.5 | 마력탄 | 53.8 | 73.8 | 53.8 | 1.00 | 1.00 | 1.00 | 1.37 | 4.6 |
| char.5 | 그림자 파동 | 51.2 | 78.8 | 88.8 | 0.95 | 1.07 | 1.65 | 1.54 | 4.3 |
| char.5 | 그림자 낫 | 51.9 | 76.6 | 90.0 | 0.97 | 1.04 | 1.67 | 1.47 | 3.7 |
| char.5 | 눈알 추적탄 | 52.4 | 77.1 | 52.4 | 0.97 | 1.04 | 0.97 | 1.47 | 5.3 |

- 목표(사용자 지시): 꾹·끊어 ±12% → 전부 0.94~1.11. 끊어 ≥ 꾹 × 1.25 → 1.30~1.69. 무리 ≤ 마력탄 × 1.8 → 1.25~1.71.
- 열을 정한 방법: 끊어/꾹 비율은 열로만 정해지고(피해에 선형) 피해는 두 DPS를 같이 옮긴다 → 열을 처음 값에서 가까운 쪽부터 훑어 마력탄과 같은 비율(±12%)이 되는
  값을 고르고, 피해 = √(꾹 비 × 끊어 비)의 역수로(`TuneHeat`). 느린 유형은 한 발 열이 크다(포탄 16.2, 구체 16.6) — 끊어 쏘기의 냉각 대기(5틱)가 긴 간격에 묻혀 끊어 쏘기가 너무
  세지는 것을 열로 되돌렸다. 꾹 누르면 3~6발에 잠긴다(묵직한 무기의 리듬).
- 판 연기(`AttackStyleRunTests`, 진짜 판·단순 봇·무적, 웨이브 1~3을 넘긴 틱 ÷ 마력탄): 포탄 0.96, 불꽃 0.95, 불씨 0.83, 사슬 0.78, 구체 0.93, 낙뢰 0.93, 책장 1.09, 책 0.99, 룬 0.99,
  창 1.00, 눈보라 0.86, 고드름 1.25, 파동 0.86, 낫 1.03, 추적탄 0.68. 봇은 4방향으로만 보고 가까이 다가가므로 추적탄·사슬 번개가 빠르고 줄 무기(고드름)가 느리다 — 사람 QA에서 본다.

---

## 6. 시험 (EditMode)

| 파일 | 무엇 |
|---|---|
| `Tests/EditMode/Spells/AttackStyleTests.cs` | 데이터(15종·캐릭터마다 3·이름·꼬리표·전리품 목록 밖), 해금 손잡이 기본 꺼짐, 판 시작 코어 바꾸기(마력탄만·다른 캐릭터 무시·화염구 그대로·T 같음), 튜토리얼 A, 핵 + 변형 4종에 W_NO_EFFECT 없음 / 유형별 동작 15(착탄 범위·비행 중 안 맞음·불바닥 4번, 부채꼴 안만·화상 상한, 튐, 선 적중 3·짧은 사거리, 지나가며 지짐, 낙뢰 지연, 기본 거리, 책장 3·관통, 부메랑 두 번·돌아옴, 룬 밟힘·시간·상한, 창 관통·파편·감속, 냉기 → 빙결, 고드름 차례, 파동 꿰뚫음·당김, 낫 양쪽·콤보, 추적·분열 1세대) / 변형 결합(분열 2발, 크기 → 반경, 관통 → 피해, 튕김 → 한 번 더 튐, 유도 → 겨누기, 트리거 → 폭발 중심, 사슬 + 관통 → +1, 서리) / 비기 실행 배율 / 결정론(15종 두 번 같은 지문, 같은 모듈 객체로 세 세계를 섞어 돌려도 같은 지문) |
| `Tests/EditMode/Spells/AttackStyleBalanceTests.cs` (분류 Balance) | 5절 표의 세 조건(캐릭터마다), 측정 결정론, 야영 허수아비 DPS 어림이 측정의 ±35% |
| `Tests/EditMode/Arts/AttackStyleArtUpgradeTests.cs` | 명세 콘텐츠는 강화 없음, 5 강화의 단계 수·틱·자리·적마다 한 번·기절 → 빙결 순·소용돌이 당김·지대와 함께 끝, 덧피해 ≤ 원래 총량의 20% |
| `Tests/EditMode/ScreensPrep/AttackStylePrepTests.cs` | 카드 3장·기본 A·전부 열림, 캐릭터별 저장·다시 들어와 복원, 출발 판 설정에 실림·처음 출발 때 A 기록, 남의 값·모르는 값 → A, 해금 손잡이(웨이브 5·10), 프로필 JSON 왕복·옛 저장 null |
| `Tests/EditMode/Story/AttackStyleTutorialTests.cs` | 다섯 캐릭터의 A로 튜토리얼 발사·과열·임계(끊어 쏘기)·콤보 관문을 J만으로 통과, 꾹 눌러서는 임계 관문 통과 못 함(관문 설계 유지) |
| `Tests/EditMode/Integration/AttackStyleRunTests.cs` | 15종 모두 진짜 판 웨이브 1~3을 봇으로 넘김(5절 끝 표) |

---

## 7. 남은 것·위험

- 콤보 속도: 책장 +26%(한 발 3장 = 발사당 +3), 낫 −20%(과녁 하나에서). 무리에서는 낫이 크게 오른다. 결정·비기 빈도 차이는 QA에서 본다(`combo_on_hit`로 조정).
- 사람 기준 체감(근접 유형의 위험, 사슬 번개의 짧은 사거리, 고드름 줄 겨누기)은 재지 않았다 — 봇은 피하지 않는다.
- 허상(보스)의 따라 쓰기는 핵 블록의 flags만 본다: 포탄 = 폭발 탄, 사슬 = 예고선 번개, 책 = 부메랑, 나머지 = 곧은 탄(05 9.2절 변환 그대로). 유형 동작 자체를 따라 하지는 않는다.
- 그림·소리 배치와 손맛 수치(흔들림·정지·어둡힘)는 솔로 임시다(Juice.json styles, Audio.json) — 사람 QA에서 정한다.
- 그믐(char.5) 거대한 손의 "펼친 손" 프레임은 그림 쪽 필요 목록에 있다(지금은 솟음 → 움켜쥠 → 짓누름 → 흩어짐).

---

## 8. 그림·소리 연결 (구현: `View/Effects/StyleFxLayer*.cs`, `SoundEventMap`, `AudioLayer`)

그림 묶음: `Resources/Art/effects/styles/char.N`·`common`(→ `styles.char.N`·`styles.common`, 기준점 pivot·구간 clips를 읽는다), 다시 쓰는 있는 그림은
`effects/styles/reuse.json`(→ `styles.reuse`, 아이콘 표 `style_icons`). 손맛 수치는 `DataSolo/Juice.json` `styles`, 소리는 `DataSolo/Audio.json`
`common.events`(`style.*`, `heat.shot_block.core.*`)와 `G16.json` `sound.style_loops`. 합성 소리 `sty_*`는 `Tools/sfx/synth.py`.

| 이벤트 | 그림 | 소리 | 화면 반응 |
|---|---|---|---|
| 발사(ShotFired) | 탄 몸: 구체 ball_lightning, 책 book_spin(돌리지 않음), 창 ice_spear, 파편 ice_screen_shard, 책장 page_large, 불씨 kiln_ember, 눈알 floating_eye + shadow_orb 둘레(G16 `shots.block_fx_fallback`·`body_scale`·`unrotated_blocks`). 몸 없는 동작(포탄·부채꼴·낙뢰·고드름·룬·사슬)은 내 탄 층이 그리지 않는다(`shots.style_body_hidden`) | heat.shot_block.core.*: 포탄 퉁·사슬 찌직·낙뢰 예고 찌릿·책장 펄럭·책 휙(char.3)·룬 각인·얼음 창 쨍·고드름 휙·파동 우웅·낫 휙·눈알 깜빡·불씨(char.1)·구체(사슬 발사음). 불꽃·눈보라는 비움(반복음) | 기존 발사 섬광 |
| StyleLobLaunched(상태로) | mortar_shell 포물선(높이 = 거리에 비례, 진행 방향으로 돎) + 땅 그림자 + 떨어질 자리 고리(속성 보조색) + 불씨 꼬리 | — | — |
| StyleBlast 0 착탄 | mortar_blast + dust_skirt + 빛(땅) + 고리 + 튀는 불티 | style.lob.land 쿵 | 아래로 4px 방향 흔들림, 줌 펀치 소 |
| StyleBlast 1 불바닥 틱 / StyleGroundSpawned 0 | fire_floor 반복(펼쳐짐) + 올라가는 불씨 | style.floor.start 지글 | — |
| StyleConeStarted(불꽃·눈보라, 상태로) | flame_jet / blizzard 부채 3장(가운데 + 양옆), 쏘는 동안 이어짐(0.14초 유예), 불씨·눈송이 | 반복음 style.flame.loop / style.blizzard.loop | — |
| StyleConeStarted(파동) | shadow_wave 5장이 반지름으로 퍼짐 + 어둠 입자 | (발사음) | — |
| StyleConeStarted(낫) | bloom_dark 초승달 날이 −반각 → +반각(번갈아) 잔상 | (발사음) | — |
| StyleRicochet | hit_fire + trail_fire 꼬리 + 불티 | style.ricochet | — |
| ChainLightningArc(사슬 핵) | chain_link 이어 붙임 2겹(옮겨 붙을수록 가늘게) + hit_lightning + 빛. 기존 연쇄 번개 선은 건너뛴다 | chain.hop(기존) | — |
| StyleZap | chain_link 짧은 호 + hit_lightning | style.zap | — |
| 번개 구체(상태로) | — | 반복음 style.orb.loop | — |
| StyleStrikeWarned(낙뢰) | strike_marker 고리가 좁혀 듦 + 하늘에서 내려오는 빛 | (발사음) | — |
| StyleBlast 2 낙뢰 | strike_bolt(아래 끝 = 착지) + strike_blast + 빛 + 불꽃 | style.strike.land 콰광 | 아래로 5px |
| StyleStrikeWarned(고드름) | icicle_marker 자리마다 + icicle가 착지 순간에 맞춰 위에서 떨어짐 | (발사음 휙) | — |
| StyleBlast 3 고드름 | frozen_shatter + hit_ice + 얼음 파편 | style.icicle.land 쨍 | 1.5px(장식) |
| StyleGroundSpawned 1 / StyleMineArmed / Blast 4 | rune_trap 각인(크게 찍혔다 자리로) → 무장 고리 + 맥박, 0.5초 전 깜빡 → rune_burst + 고리 + 빛 | 각인(발사음) / style.rune.arm / style.rune.burst | 3.5px |
| StyleShatter | hit_ice + frozen_shatter + 파편 | style.shatter | 2px |
| StyleSplit | shadow_orb_fade + hit_dark + 고리 | style.split | — |
| StyleStatus | 화상 겹 = 적 발밑 불씨(겹이 많을수록 자주), 냉기 겹 = 눈송이 | — | — |
| 탄 끝(책장 적중 / 책 돌아옴) | hit_page + 작은 책장 / 받는 빛 | — | — |
| ArtStage 불가마 Rise | B0: 땅 내리침(ground_slam·ground_crack·dust_skirt·debris_burst·흙 조각) + 빛 + 고리. 고리마다 fire_pillar 둘레 시간차(첫 고리는 캐릭터를 가리지 않게 0.9칸 바깥) | style.art.kiln.slam + pillar(고리마다 높아짐) | 11px 아래로, 정지 0.07, 줌 0.09, 어둡힘 |
| ArtStage 벼락 질주 Warn/Land | strike_marker → strike_bolt + strike_blast | style.art.bolt_dash.land(5음 음계로 오름) | 낙뢰마다 6px, 마지막 정지 0.06·어둡힘 |
| ArtStage 베껴 쓰기 Pulse/End | 첫 맥박: book_open → book_flip 반복(머리 위) + page_vortex 땅 소용돌이 + 책장 촤라락. 맥박마다 책장이 둘레에서 소용돌이치며 빨려 듦 + 오므라드는 고리. 끝: 책장이 흩날림 | style.art.copy.open 촤라락 / pulse / end(char.3 책 덮음) | 첫 맥박 7px·어둡힘, 맥박 2px |
| ArtStage 얼음 병풍 Rise | B0: 얼음빛 땅 내리침. 가시마다 ice_spike rise → hold(0.5초) → break + 흙 + 파편 | style.art.ice.slam + spike 콰직 | 9px, 정지 0.06, 어둡힘 |
| ArtStage 그림자 손 Grip/Crush | shadow_pool(지름 4.4) + giant_hand rise → grip 유지 → slam → fade + 보랏빛 땅 내리침 | style.art.hand.grip(char.5 움켜쥠) / crush 쾅 | 쥘 때 3px, 짓누를 때 12px·정지 0.09·줌 0.1·어둡힘 |

지키는 것: 큰 빛 덩이는 땅 띠(예고 채움 바로 아래)에 깔아 캐릭터·적·적 탄을 덮지 않는다. 어둡힘은 바닥·내 장판 띠만(적·적 탄·적 예고·내 캐릭터는 밝은 채).
입자 = 입자 예산 + 프레임 상한(우선순위), 흔들림 = FxGovernor 창·상한(일반 공격은 장식·일반 타격 순위), 그림 정지 = 무게 비례(일반 공격 0, 비기만) + 1초 예산.
'내 이펙트 불투명도'·누적 가림 상한·섬광 줄이기(빛·어둡힘 절반)·연출 감도(줄임 = 입자 절반, 최소 = 입자·흔들림·어둡힘 없음, 공격 모양은 남김).
속성색·Juice 색은 PlayerColorGuard를 거친다(위험색이면 경고 + 주황 쪽으로).
기존 `Explosion`(B = 1)은 그림(EffectLayer·JuiceLayer)·소리(explosion.detonated) 모두 건너뛴다.

화면: 원정 준비 카드 3장 = 실제 그림 아이콘(`ArtLibrary.Icon` → `style_icons`) + 미리보기 상자(`DataSolo/StylePreview.json`, 반복), 조립 판 코어 칸은 판 시작 규칙대로
고른 유형의 핵(`ExpeditionPrepModel.EffectiveCore`). 야영 설명 판·전리품 카드에 `BlockDescriber.OnCore`("이 핵에서는 …"). 도감에 "기본 공격" 분류(15종).
확인: `Tests/PlayMode/Flow/AttackStyleShotsTests.cs`(창 모드 스크린숏 `Logs/shots/flow/styles/`), `Tests/EditMode/ViewEffects/AttackStyleFxTests.cs`.
