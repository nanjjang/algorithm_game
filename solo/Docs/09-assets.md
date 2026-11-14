# 09. 그림 에셋 목록 (솔로 순정 Unity 구현용)

> 출처: `art/` 전체(실제 PNG 크기는 PIL로 재서 확인), `docs/spec/14-content-ids-assets.md`(이하 S14), `docs/ui-assets.md`(UI), `docs/effects.md` 20절(FX), `data/presentation/asset_aliases.json`.
> 모든 경로는 저장소 루트(`D:\algorithm_game\`) 기준. 크기는 **가로×세로 px**. "×N"은 프레임 수.
> 화면 글자는 `data/names/names.ko.json` 열쇠로만 적는다(예: 캐릭터 이름 = 열쇠 `char.1`~`char.5`, 보스 이름 = `boss.kiln_boar` 등 콘텐츠 id 그대로가 열쇠).

---

## 0. 결론 먼저 (정본 추천)

| 대상 | 정본(이걸 쓴다) | 보강(정본에 없는 것만 여기서 가져온다) | 쓰지 않음(다른 화풍/부분본) |
|---|---|---|---|
| 캐릭터 5명 몸·초상·선택·실루엣 | `art/production/players/` | `art/sprite-pack/characters/players/layers/` (반응 부품·야생 물·금 간 거울·성에 기둥 손) | `sprite-pack/native-64/characters`(1방향 5동작뿐), `sprite-pack/redrawn/characters`(ImageGen, 서기+오른쪽 걷기뿐), `sprite-pack/combat-pixel/players`(다른 화풍), `art/player/*`(옛 시안·기준 그림) |
| 지팡이 5종 × +0~+13 | `art/production/wands/` | (선택) `sprite-pack/native-64/enhancement/wands/<slug>/+07.png` = +7 큰 그림 새 버전 | `sprite-pack/redrawn/wands`, `sprite-pack/combat-pixel/combat-art/wands` |
| 몬스터·엘리트·보스·보스 초상/HUD | `art/production/monsters/` | — (`sprite-pack/characters/monsters`는 바이트 동일 사본) | `sprite-pack/redrawn/characters/enemy.*`, `combat-pixel/enemies` |
| 효과 | `art/production/effects/` (manifest 303개) | 강화 화면 신규: `sprite-pack/native-64/enhancement/`(변이 아이콘, "지켰다" 도장, 흔적 가림, 고르기 화살표, 작업대 배경, 느루·촛불) | `effects/supplement/`(build.py만 있고 **출력 PNG 없음**, 스크립트 끝부분 미완성), `sprite-pack/redrawn/effects`, `combat-pixel/combat-art/fx` |
| UI 키트·아이콘·아틀라스·글꼴 | `art/production/ui/` (627 파일, 23 아틀라스) | 강화 화면 조합 패널: `sprite-pack/native-64/enhancement/ui-native/` | `sprite-pack/redrawn/gui`, `combat-pixel/gui` |
| 배경·월드 타일·시설·NPC | `art/production/world/` + `art/production/backgrounds/title_1920x1080.png` | — | — |

근거:
- `production/players/*/body.png`(작업 트리 판) = `sprite-pack/characters/players/*/body.png` = `sprite-pack/redrawn/characters/catalog/*/body.png` (md5 동일, dalgu·geumeum 확인). 즉 production이 최신이고 sprite-pack은 그것을 묶어 배포한 사본 + 덧그림 추가본이다. (HEAD 커밋 판과는 다르다 = 작업 트리가 더 새것.)
- production만 S14 5.2~5.6절의 매니페스트 형식·테스트 벡터(TV-180~TV-224)와 일치하고, 3방향 × 37클립 전체가 있다.
- `sprite-pack/*` 폴더는 git에 추적되지 않은 상태(`?? art/sprite-pack/`)이고 zip 7개는 풀린 폴더와 같은 내용으로 보고 풀지 않았다.
- `S14 5.1`의 "world/에는 pixel_art.py만 있다(C-13)"는 **지금은 틀렸다**: `art/production/world/`에 manifest·타일·스프라이트·맵 118개 파일이 있다. S-2405/S-2407도 존재한다.

---

## 1. 공통 규격 (Unity 반입 규칙)

| 항목 | 값 | 출처 |
|---|---|---|
| 1칸 | 64px. **Pixels Per Unit = 64** 권장(1칸 = 1 Unity 단위) | S14 4.1 |
| 기준 화면 | 1920×1080, 그림 1px = 화면 1px | S14 4.1, UI 1.1 |
| 확대 | 정수 배만(2,3,4배). 보간 금지 | UI 1.1 |
| 텍스처 설정 | Filter Mode = **Point**, Mipmap 끔, Compression = **None** | 모든 manifest(`filter: point`, `mipmaps: false`, `texture_compression: false`) |
| 좌표 원점 | 모든 매니페스트는 **왼쪽 위** 원점, y 아래로 증가 | 각 manifest, `effects/manifest.json notes.origin = top-left` |
| Unity 변환 | 시트 높이 `TH`, 칸 rect `[x,y,w,h]` → Unity `Rect(x, TH − y − h, w, h)`. 프레임 안 기준점 `(px,py)` → Unity pivot `(px / w, (h − py) / h)` | (환산) |
| 좌우 반전 | 캐릭터: `right` 클립을 뒤집어 `left`(`mirrorRightForLeft: true`). 몬스터: 방향 없이 한 장, 이동 방향으로 좌우 반전. 지팡이: 왼쪽은 right 행 반전 | S14 4.6, 5.3, 5.5 |
| 반전 시 점 | 캐릭터 프레임 점은 발밑 기준점 둘레로 x 차이 부호만 바꾼다 `dx' = −dx` [임시]. 지팡이 단계 점은 `x' = 31 − x` [임시] | S14 4.6 `AnchorMirror`, 5.4, U-04 |
| 틱 환산 | 60틱 = 1초 (S14 4.3: 0.5초 = 30틱). `fps` f 의 한 프레임 = 1/f 초 (12fps = 0.0833초 = 5틱, 10fps = 6틱, 7fps ≈ 8.57틱). 표시 애니메이션은 실제 시간으로 재생해도 된다 | S14 U-20 |
| 위험색 | 색상각 330~15°, HSV 채도 ≥70%, 명도 ≥70% = 적 공격·예고 전용. 플레이어 그림·메뉴·대체 도형에 쓰지 않는다. 적 탄 빨강 `#FF3B30`, 자홍 `#FF2E88` | S14 4.2 |
| 없는 그림 대체 | 원하는 크기에 회청 `#8E9AAE` · 나무 어두움 `#1F2130` 8px 바둑판 + F-12로 id 글자. 위험색·금색·연두·청록 금지 | S14 5.7 [임시] |

캐릭터 폴더 열쇠(작업용 로마자, 화면에 쓰지 않음) ↔ 콘텐츠 id (`data/presentation/asset_aliases.json`, S14 5.3 C-05):

| 폴더 | char | wand | 속성 | 이름 열쇠 |
|---|---|---|---|---|
| `dalgu` | `char.1` | `wand.1` | 화염 `element.fire` | `char.1` |
| `chorong` | `char.2` | `wand.2` | 번개 `element.lightning` | `char.2` |
| `galpi` | `char.3` | `wand.3` | 비전 `element.arcane` | `char.3` |
| `seonge` | `char.4` | `wand.4` | 냉기 `element.ice` | `char.4` |
| `geumeum` | `char.5` | `wand.5` | 암흑 `element.dark` | `char.5` |

속성 별칭: UI 매니페스트는 `frost`/`shadow`, FX는 `ice`/`dark`. 정식 = `ice`/`dark` (S14 3.1, C-01). `asset_aliases.json`의 `element` 표 그대로 쓴다.

---

## 2. 캐릭터 (`art/production/players/`)

### 2.1 파일 (5명 모두 같은 구성)

| 파일 | 픽셀 크기 | 내용 |
|---|---|---|
| `players/<slug>/body.png` | 768×832 | 64×64 칸, **12열 × 13행**(156칸 중 147칸 사용). 칸 번호 `i = row*12 + col` |
| `players/<slug>/portraits_64x64_f4.png` | 256×64 | 64×64 × 4 (0 기본, 1~3 표정 — 표정 이름 미정, S14 U-11) |
| `players/<slug>/selection_192x192.png` | 192×192 | 캐릭터 선택 큰 그림 1장 |
| `players/<slug>/silhouette_32x32.png` | 32×32 | 실루엣 확인용 검은 덩어리 |
| `players/manifest.json` | — | 최상위 `canvas [64,64]`, `pivotPx [30,60]`, `mirrorRightForLeft true`, `characters.<slug>.{body,frames:147,portrait,selection,silhouette,clips}` |
| `players/source/*.png` | turnaround 등 | ImageGen 원본(참고만, 반입 안 함) |

### 2.2 클립 표 (5명 동일 — 이름·fps·반복·프레임 수·칸 위치 전부 같음, 프레임은 칸 번호 연속)

| 클립 | 시작 칸 | 프레임 | fps | 반복 | 쓰는 곳 (S14 4.6) |
|---|---|---|---|---|---|
| `idle_down` / `idle_right` / `idle_up` | 0 / 4 / 8 | 4 | 7 | 예 | 서기(숨쉬기) |
| `walk_down` / `walk_right` / `walk_up` | 12 / 18 / 24 | 6 | 10 | 예 | 걷기 |
| `stance_walk_down/right/up` | 30 / 34 / 38 | 4 | 9 | 예 | 자세 걷기(몸 낮추고 지팡이 내민 걸음). 반전 자세는 방향만 뒤집어 같이 씀 |
| `fire_down/right/up` | 42 / 45 / 48 | 3 | 14 | 아니오 | 발사(반동) |
| `special_down/right/up` | 51 / 55 / 59 | 4 | 12 | 아니오 | 공용 비기 시전 |
| `signature_down/right/up` | 63 / 69 / 75 | 6 | 12 | 아니오 | 고유 비기 |
| `hit_down/right/up` | 81 / 83 / 85 | 2 | 12 | 아니오 | 피격 |
| `death_down` | 87 | 6 | 10 | 아니오 | 사망. 마지막 장 = 지팡이만 남는 빈 몸 |
| `sit_down` | 93 | 2 | 4 | 예 | 앉기 반복 |
| `wave_down` | 95 | 4 | 7 | 아니오 | 감정표현 인사(`emote.greet`) |
| `clap_down` | 99 | 4 | 8 | 아니오 | 박수 |
| `taunt_down` | 103 | 4 | 7 | 아니오 | 도발 |
| `bow_down` | 107 | 4 | 8 | 아니오 | 절 |
| `sit_down_down` | 111 | 4 | 8 | 아니오 | 앉는 동작(뒤에 `sit_down` 반복, U-31) |
| `dance_down` | 115 | 4 | 9 | 아니오 | 춤 |
| `lift_wand_down` | 119 | 4 | 8 | 아니오 | 지팡이 들어 올리기(몸만) |
| `frustrated_down` | 123 | 4 | 6 | 아니오 | 좌절 |
| `upgrade_wait_down` | 127 | 2 | 5 | 아니오 | **강화 반응: 기다림** (강화 화면 초상 자리에 2배) |
| `upgrade_charge_down` | 129 | 2 | 5 | 아니오 | **강화 반응: 충전** |
| `upgrade_success_down` | 131 | 4 | 8 | 아니오 | **강화 반응: 성공** |
| `upgrade_fail_down` | 135 | 4 | 8 | 아니오 | **강화 반응: 실패** |
| `duel_win_down` / `duel_loss_down` | 139 / 143 | 4 | 8 | 아니오 | 결투 결과 — 솔로 제외 |

칸 rect 계산: `x = (i % 12) * 64`, `y = (i / 12) * 64` (top-left). 합계 147 = TV-191.

프레임별 점(각 프레임 `frames[k]`에 있음, 프레임 안 px, top-left):
- `rect [x,y,w,h]`, `feet` (**모든 프레임 (30,60)** = 게임 좌표, TV-192), `hand`(지팡이 손잡이를 붙일 점), `head`(머리 위 표시·반응 부품), `shoulder`.
- 감정표현 8종은 솔로에서 광장 없이 쓸 일이 거의 없음(메뉴 연출로만 선택 사용).

캐릭터 판정(그림과 별개, S14 4.1): 피격 반지름 0.15칸(9.6px), 판정 중심 = 발밑에서 위로 0.4칸(25.6px).

### 2.3 합성 순서 (S14 4.6 `CompositeOrder`, 구현 참고 `src/Wizard.ViewLogic/Assets/CharacterComposite.cs:11`)

- 기본 층(아래→위): Body → ReactiveParts → WildStain → Cosmetics → Wand.
- 방향이 `up`이면 Wand를 맨 아래로: [Wand, Body, Reactive, WildStain, Cosmetics] (TV-209).
- 예외 `wand.2`(초롱) + up: 지팡이를 y=28에서 둘로 나눠 아래쪽(y 28~63)은 맨 아래, 위쪽(y 0~27)은 맨 위, 둘 다 x +6px (TV-210, `asset_aliases.json wand_up_split`).
- 지팡이 부착: 지팡이 칸 왼쪽 위 = 프레임 `hand` − 손잡이 (16,48). 예 hand (34,44) → (18,−4) (TV-208, `WandSheet.cs:105`).

### 2.4 덧그림 층 (정본에 없음 → `art/sprite-pack/characters/players/layers/`)

`layers/manifest.json`: `layerOrder ["body","reactive","wild-stain","cosmetic","wand"]`, `mirrorRightForLeft true`, `wildStainStageWaves {1:0, 2:100, 3:500, 4:2000}`(누적 웨이브), `wildStainStageInheritance {2:[], 3:[2], 4:[2,3]}`(앞 단계 장식 계속 붙음), `paletteReplacements.<slug>.<단계>` = 색 교체표 `{"#원색":"#바꿀색"}`, `groups.<그룹>` = `{sheet, frameSize, columns, frames[{key,rect,file}], attachment(head|feet), pivot, offsetByDirection{down,right,up}, mirrorFor, fps, ...}`. 부착 위치·속도는 "엔진 QA용 시작값"(manifest notes).

| 그룹 | 프레임 크기 | 프레임 | 비고 (S14 4.6 반응 부품 표와 대조) |
|---|---|---|---|
| `reactive/dalgu/steam` | 16×16 | 12 (normal/임계/잠금직전 × 4) | head, fps 10 |
| `reactive/dalgu/lid` | 24×16 | 4 | 과열 잠금 중(useDuring "heat lock") |
| `reactive/chorong/head_lantern` | 16×16 | 9 (밝기 3 × 2 + 번쩍 3) | 지팡이 번쩍임 0.1초 뒤 반응(`wandFlashDelayMs 100`), 번쩍 프레임 50ms |
| `reactive/galpi/bookmark` | 16×24 | 12 (4 × 3방향) | 걷기와 동기 |
| `reactive/galpi/falling_glyph` | 8×8 | 4 | 발사 때, fps 12 |
| `reactive/seonge/crown_shard` | 8×12 | 15 (조각 5 × 크기 3) | offsets 5점, 열에 따라 크기 선택 |
| `reactive/seonge/frost_footprint` | 16×8 | 3 | feet, 수명 500ms(167/166/167), 바닥 층 |
| `reactive/geumeum/eyes` | 16×8 | 10 (5종 × 2방향) | up은 눈 없음 |
| `reactive/geumeum/debt_ring` | 16×16 | 2 (dim, lit) | 머리 위 offset (0,−15), fps 3 |
| `wild-stain/<slug>/stage-{2,3,4}/<부품>` | 부품마다 (예 dalgu soot 8×5) | 3방향(4단계 움직이는 것은 방향별 3프레임) | 각 캐릭터 2~3개 × 3단계, 다섯 명 합 105장. 부품 목록: dalgu soot, grass_hem, metal_patch, lid_feather, spout_embers / chorong hat_leaf, straw_slipper, fireflies, sewn_robe, floating_lantern / galpi tied_hem, roof_spot, roof_grass, second_scroll, glowing_bookmark / seonge earth_hem, shard_crack, fur_collar, sixth_crown_shard, snowflake_track / geumeum frayed_cape, hood_burr, hood_moon, shoulder_crow, eye_afterimage |
| `cosmetic/cracked-mirror-shard` | 24×24 | 3방향 | `cosmetic.headwear.cracked_mirror_shard`(머리 옆에 뜸, 계정 묶임) |
| `equipment/seonge/pillar-grip` | — | 3방향 | 성에 +13 고드름 홀용 "기둥 짚은 손" |
| `reactive/<slug>/body_without_*.png` | 768×832 | — | 부품을 뺀 몸 시트(예 `body_without_kettle_lid`, `body_without_eyes`). 반응 부품을 따로 그릴 때 몸 대신 씀 |

솔로에서 야생 물은 **오프라인 프로필 누적 웨이브**로 단계를 정한다(S14 3.14, 서버와 따로 셈).

### 2.5 옛 그림(반입 안 함)

`art/player/dark_wizard/idle-reference-details.png` 64×64 = 그믐 +0 기준 그림(DEC 1.12). `kettle_helm`, `lantern_hat`, `crystal_crown`의 `sheet.png` 256×32(32×32×8) = 옛 32px 시안. 쓰지 않는다(S14 5.1).

---

## 3. 지팡이 (`art/production/wands/`)

단계 `stage` 0~13 = 강화 단계 +0~+13 (`build.py`가 열 n에 "+n"을 적어 그림). 겉모습(`skin_id`)별 시트 없음 → 기본 하나 [임시] (S14 5.4).

| 파일 | 픽셀 | 칸 | 배치 |
|---|---|---|---|
| `wands/<slug>/hand.png` | 448×384 | 32×64, 14열 × 6행 | 열 = 단계 0~13, 행 = `down_hold, down_aim, right_hold, right_aim, up_hold, up_aim`. 손잡이 기준점 (16,48) |
| `wands/<slug>/inspection.png` | 1344×384 | 192×192, 7열 × 2행 | 강화 화면·선택 화면 큰 그림. 단계 n → 열 n%7, 행 n/7. 강화 화면에서는 **2배(384×384)** 로 보인다(FX 21.2) |
| `wands/<slug>/icon.png` | 896×64 | 64×64 × 14 | 목록 아이콘(지팡이 칸 K-62 안) |
| `wands/parts/chorong_max_lightning.png` | 32×16 | 8×16 × 4 | anchor (4,15). 초롱 최고 단계 분리 번개 |
| `wands/parts/seonge_orbit.png` | 320×16 | 40×16 × 8 | anchor (20,8). 성에 고드름 홀 둘레 도는 조각 |
| `wands/parts/geumeum_orb.png` | 60×20 | 20×20 × 3 | anchor (10,10). 그믐 떠 있는 눈 |
| `wands/preview.png` | 784×447 | — | 미리보기(반입 안 함) |

칸 공식(S14 5.4, 구현 `src/Wizard.ViewLogic/Assets/WandSheet.cs:54,60,66`):
```
WandHandRect(stage, dir, pose) = (stage*32, (DirIndex(dir)*2 + (pose==aim?1:0))*64, 32, 64)   // down=0, right=1, up=2, left=right 반전
WandInspectionRect(stage)      = ((stage%7)*192, (stage/7)*192, 192, 192)
WandIconRect(stage)            = (stage*64, 0, 64, 64)
```
벡터: stage13·up·aim → (416,320); stage0·down·hold → (0,0); stage5·right·aim → (160,192); inspection 8 → (192,192); inspection 6 → (1152,0); icon 13 → (832,0) (TV-201~206).

단계별 점(`manifest.sprites.<slug>.stages[n]`, 32×64 칸 안 px): `head [16, y]`(머리=효과 붙는 곳, 분리 조각의 anchor도 여기에 맞춤 U-05), `mutations` 2점(초롱만 3점, TV-207), `expansion [18,49]`(확장 슬롯 표식). 방향·자세별 값 없음 → 모든 방향에 같은 점 [임시] (C-21, U-04).

손 행 이름의 자세: `hold` = 서기·걷기, `aim` = 자세 걷기·발사 (WC 3.3 "자세 2(들기, 내밀기)").

단계 묶음(칭호·단계 테): `StageBand` = +0~3 / +4~6 / +7~9 / +10~12 / +13 → UI I-G1 `wand_tier_0_to_3 … wand_tier_13` (S14 3.12).

스승의 허상이 드는 지팡이 = 플레이어 손 지팡이 그림을 회색조 셰이더로 재사용(새 그림 없음, S14 4.8).

보강/대안: `art/sprite-pack/native-64/enhancement/wands/<slug>/+00.png … +13.png`(각 192×192 낱장, 594개 중 일부). README상 **+07만 새로 그림**, 나머지는 production 사본. 원하면 +7 큰 그림만 교체 가능(결정 필요 → 미정 목록).

---

## 4. 몬스터·엘리트·보스 (`art/production/monsters/`)

`manifest.json`: `entries.<id>.{path, size[w,h], grid[열,행], origin[x,y], clips{이름: [{rect, origin}]}}`, `bossUi.<boss id>.{portrait 96×96, hud 32×32}`. **프레임마다 `origin`을 써야 한다**(기본값과 다른 프레임 있음, S14 5.5). origin = 발밑/그림자 중심 = 판정 위치(C-06). 칸 번호 `i = row*grid열 + col`, 시트 행 우선. **재생 fps는 매니페스트에 없다 → 미정**(README "재생 속도는 게임에서 정함").

### 4.1 일반 적 12종 + 졸개 (S14 3.6 id = 파일 이름)

| id (이름 열쇠 동일) | 파일 | 시트 | 칸 | grid | 기본 origin | 클립: 칸 범위 (origin 예외) |
|---|---|---|---|---|---|---|
| `enemy.slime_moss` | `enemy.slime_moss.png` | 256×64 | 64×64 | 4×1 | (32,58) | move 0-1, death 2-3 |
| `enemy.wolf_bramble` | `enemy.wolf_bramble.png` | 384×128 | 64×64 | 6×2 | (32,58) | move 0-3, warn 4, lunge 5, stagger 6-7, dodge 8, death 9-10 |
| `enemy.golem_menhir` | `enemy.golem_menhir.png` | 576×192 | 96×96 | 6×2 | (48,90) | move 0-1, slam_warn 2-3, slam 4, sweep_warn 5, sweep 6, open 7-8, death 9-11 |
| `enemy.shaman_cap` | `enemy.shaman_cap.png` | 384×128 | 64×64 | 6×2 | (32,58) | move 0-1, cast 2-3, fire 4, hide 5, death 6-7 |
| `enemy.bug_lantern` | `enemy.bug_lantern.png` | 256×64 | 64×64 | 4×1 | (32,58) | move 0-1, ignite 2-3 (death 없음 → FX `enemy/explosion`, TV-215) |
| `enemy.hog_needle` | `enemy.hog_needle.png` | 384×128 | 64×64 | 6×2 | (32,58) | move 0-1, aim 2-3, fire 4, recover 5, death 6-7 |
| `enemy.weasel_pot` | `enemy.weasel_pot.png` | 384×128 | 64×64 | 6×2 | (32,58) | burrow 0-1, rise 2-3, stand 4-5, dive 6-7, death 8-9 |
| `enemy.bat_dusk` | `enemy.bat_dusk.png` | 320×64 | 64×64 | 5×1 | (32,58) | fly 0-2, death 3-4 (death 첫 프레임 origin (32,54), TV-217) |
| `enemy.wisp_stray` | `enemy.wisp_stray.png` | 384×128 | 64×64 | 6×2 | (32,58) | hover 0-2, cast 3-4, blink 5-6, death 7-8 |
| `enemy.vine_thorn` | `enemy.vine_thorn.png` | 384×192 | 64×96 | 6×2 | (32,90) | idle 0-1, cast 2-3, emerge 4-5, death 6-7 |
| `enemy.boar_tusk` | `enemy.boar_tusk.png` | 576×128 | 96×64 | 6×2 | (48,58) | move 0-3, warn 4-5, charge 6-7, stagger 8-9, death 10-11 |
| `enemy.toad_drum` | `enemy.toad_drum.png` | 384×128 | 64×64 | 6×2 | (32,58) | hop 0-1, call 2-4, tongue_warn 5, tongue 6 (origin (26,58)), death 7-8 |
| `enemy.spore_seedling` (졸개) | `enemy.spore_seedling.png` | 96×32 | **32×32 실제 픽셀**(키우지 않음, C-25) | 3×1 | (16,26) | move 0-1, death 2 |

합 100(일반) — TV-212. 일반 적 사망 공용 연기 = FX `enemy/death_smoke`.

### 4.2 엘리트 (S14 3.7: `elite.<바탕>` = 바탕 종 그림 + 덧그림)

| 에셋 | 파일 | 크기 | origin | 비고 |
|---|---|---|---|---|
| `elite.<base>.overlay` × 12 | `elite.<base>.overlay.png` | 64×32 (golem_menhir·boar_tusk는 96×32) | (32,26~31) / (48,26~30) | 종별 머리 장식 1장. 자세별 붙는 위치는 게임에서 맞춤(미정) |
| `elite.horn` | `elite.horn.png` | 16×16 | (8,12) | 머리 위 뿔 표식 |
| `elite.bat_dusk.dive` | `elite.bat_dusk.dive.png` | 64×64 | (32,54) | 박쥐 여왕 급강하 1장 |
| `elite.wisp_stray.twin` | `elite.wisp_stray.twin.png` | 192×64 (64×64×3) | (32,60) | 쌍둥이불 전용 본체 hover 3 |

엘리트 공통 표시: 금색 `#FFD24A` 2px 외곽선(셰이더) + 뿔 + 장식 + 작은 체력 바, 등장 빛기둥 FX `enemy/elite_column` (S14 3.7, 4.8). 합 17 — TV-214.

### 4.3 보스 6종 (S14 3.8)

| id | 파일 | 시트 | 칸 | grid | origin | 클립: 칸 범위 (origin 예외) |
|---|---|---|---|---|---|---|
| `boss.kiln_boar` | `boss.kiln_boar.png` | 1152×512 | 192×128 | 6×4 | (96,122) | walk 0-3, charge_warn 4-5, charge 6-7 (일부 (96,124)), stomp 8-10 ((96,124),(96,120)), roar 11-12, stun 13-14, phase 15-16, death 17-20 ((96,120),(96,116)) |
| `boss.mirror_phantom` | `boss.mirror_phantom.png` | 384×384 | 64×96 | 6×4 | (32,90) | hover 0-2, fire 3-4, cast 5-6, burst 7-9, open 10-11 ((36,90),(30,90)), phase 12-13, death 14-17, **crack 18-19, look 20**(첫 만남 전용). 첫 만남 "사라짐" = death의 뒤 2장(칸 16,17, U-32) |
| `boss.spore_queen` | `boss.spore_queen.png` | 768×640 | 128×160 | 6×4 | (64,154) | walk 0-1, ring 2-4, cloud 5-6, call 7-8, jump 9-12 ((64,159)), open 13-14 ((62,154)), phase 15-17, death 18-21 ((64,152),(64,150)) |
| `boss.storm_owl` (공중) | `boss.storm_owl.png` | 1152×384 | 192×128 | 6×3 | (96,122) | fly 0-3 ((96,116)), dive 4-5, fan 6-8, weather 9-10 ((96,118)), phase 11-12, death 13-16 |
| `boss.storm_owl.ground` | `boss.storm_owl.ground.png` | 192×128 | 96×128 | 2×1 | (48,122) | land 0-1 |
| `boss.frost_goat` | `boss.frost_goat.png` | 768×640 | 128×160 | 6×4 | (64,154) | walk 0-3, pillars 4-5, sweep 6-8, breath 9-11 ((62,154)), charge_warn 12-13, charge 14-15, impact 16-17, phase 18-19, death 20-23 ((64,152)) |
| `boss.night_serpent` (머리) | `boss.night_serpent.png` | 576×288 | 96×96 | 6×3 | (48,70) | move 0-1, bite 2-4, spit 5-6, sink 7-8, rise 9-10, rest 11-12, phase 13-14, death 15-17 ((48,66),(48,90)) |
| `boss.night_serpent.segment` | `….segment.png` | 256×64 | 64×64 | 4×1 | (32,36) | move 0-1, warn 2, death 3 |
| `boss.night_serpent.tail` | `….tail.png` | 192×64 | 64×64 | 3×1 | (32,36) | move 0-1, death 2 |

보스 UI(`bossUi`, 6종 모두 존재 → S14 U-01은 이제 해결됨):
- `monsters/boss.<key>.portrait.png` 96×96 — 보스 등장 이름 띠.
- `monsters/boss.<key>.hud.png` 32×32 — 보스 체력 바 아이콘.

미리보기(반입 안 함): `preview-regular.png`, `preview-elites.png`, `preview-bosses.png`, `preview-portraits.png`, `preview-boss-icons.png`.
보스 별칭(이름 띠의 둘째 줄) 열쇠 = `boss.<key>.epithet` (S14 3.8).

---

## 5. 효과 (`art/production/effects/`)

`manifest.json` 최상위 묶음 7개: `player` 55, `special` 89, `ritual` 77, `enemy` 31, `loot` 11, `wand` 15, `status` 25 = **303** (TV-219) + `notes`. 항목 = `{path, size[w,h], frames[[x,y,w,h]…], fps, loop}`. 파일 이름 `<열쇠>_<w>x<h>_f<N>.png`. 전부 파일 존재 확인. **아래 7개는 가로 한 줄이 아니라 여러 줄 격자**이므로 반드시 `frames` rect를 쓴다: `ritual/mutation_vine`(1920×768), `screen_crack`(1920×3240), `max_sky_band`(1920×960), `title_ribbon_normal/gold`(1440×192), `plaza_notice_normal/gold`(1920×160).

표기: `열쇠 W×H×프레임 fps(L=반복)`. `<el>` = fire, lightning, arcane, ice, dark (5벌).

### 5.1 `player/` (전투: 플레이어 탄·적중)
- 속성별 ×5: `hit_<el>` 32×32×3 12 · `hit_large_<el>` 64×64×4 12 · `weakness_<el>` 64×64×4 12(약점 큰 불꽃) · `explosion_<el>` 256×256×5 12(반경 2칸 폭발 고리, 다른 반경은 늘려 씀) · `resist_<el>` 32×16×3 12(저항 튕김 호) · `crystal_<el>` 24×24×2 5L(찬 결정) · `trace_<el>` 64×32×2 12(바닥 속성 흔적).
- 탄: `mana_bolt` 16×16×2 10L · `fireball` 32×24×4 14L · `chain_lightning` 64×16×2 12(이어 붙임) · `piercing` 24×12×2 12 · `ice_lance` 12×28×2 12 · `ice_muzzle` 32×32×3 · `ice_tail` 8×24×2 · `shadow_orb` 40×40×4 8L · `shadow_orb_cast` 48×48×4 · `shadow_orb_ripple` 32×32×3 · `shadow_orb_fade` 40×40×4 · `shadow_small` 16×16×2.
- 임계·열: `critical_muzzle` 32×32×3 · `critical_hit` 32×32×3 · `critical_chevron` 6×8×1(탄 앞 쐐기) · `graze_strike` 32×128×2(피뢰침 벼락 줄기로 추정) · `heat_lock_steam` 32×32×4 · `heat_lock_x` 24×24×2 · `overflow_warning` 16×16×1 · `overflow_steam_arc` 320×320×5(끓어넘침 증기 호 6조각, 반경 2.5칸).

### 5.2 `special/` (비기)
- 속성별 ×5: `release_<el>` 64×64×4(①) · `muzzle_storm_<el>` 96×64×4(②) · `fan_<el>` 192×128×4(③) · `wing_<el>` 96×64×4(④, 좌우 반전) · `octagram_<el>` 128×128×4(⑧) · `bloom_<el>` 192×192×6(⑩) · `spear_shaft_<el>`/`spear_tip_<el>` 64×32×3(⑥) · `thunderlance_shaft_<el>`/`thunderlance_tip_<el>` 64×48×4(⑨) · `petal_<el>` 32×48×3(⑩ 꽃잎).
- 발동 문양: `cast_seal_01` ~ `cast_seal_15` 96×96×4 (01~10 = 공용 ①~⑩, 11~15 = char.1~5 고유, U-18; 속성색 입힘).
- 공용: `dodge_dust` 32×32×4 · `purge_steam` 32×32×4(⑦ 소거 김) · `break_armor` 48×48×4 · `just_star` 48×48×2(저스트 별) · `technique_name_ribbon` 192×32×2 · `technique_name_ribbon_large` 320×48×2.
- 고유 비기: `kiln_border` 320×320×4 + `kiln_ember` 8×8×2(불가마) · `lightning_dash` 256×64×3(벼락 질주) · `arcane_copy` 96×96×4 + `scattered_pages` 16×16×3(베껴 쓰기) · `ice_screen_horizontal`/`ice_screen_vertical` 192×96×10(솟음 0-2, 서 있음 3, 금 감 4-5, 깨짐 6-9; TV-222) + `ice_screen_ripple` 32×32×3 + `ice_screen_shard` 16×16×3(얼음 병풍) · `shadow_pool` 128×128×4, `shadow_hand` 32×32×4, `shadow_arm` 32×16×2(이어 붙임), `shadow_fist` 64×64×3(그림자 손).
- 모든 fps 12, 반복 없음.

### 5.3 `ritual/` (강화 의식 — 솔로 오프라인 강화 화면에서 그대로 사용)
- 속성별 ×5 (지팡이 속성으로 고름): `seal_<el>` 240×240×1(마법진 속성 무늬) · `outer_glyph_<el>` 480×480×4(바깥 번짐) · `charge_<el>` 96×96×6(충전 중 지팡이 끝) · `success_column_<el>` 192×540×6(성공 빛기둥) · `max_column_<el>` 192×540×8 L(+13 빛기둥) · `success_mote_<el>` 16×16×3 · `break_pieces_<el>` 64×64×18(깨지는 조각 — FX 20.1은 "18×2벌", 현물은 1벌) · `extinguish_<el>` 96×96×5(실패: 꺼지는 겹그림) · `ward_ribbon_<el>` 128×128×6(부적 종이 띠 풀림) · `stage_hold_<el>` 128×96×4(하락 없음의 받침).
- 공용: `seal_base` 240×240×1(공용 고리, 회전은 코드) · `gold_burst` 256×256×8(대성공) · `gold_aureole` 256×256×8(+13 후광) · `failure_dust` 48×48×4 · `failure_smoke` 64×64×5(회색, 실패색 입힘) · `artisan_ember` 32×32×6(기운 불씨: 오름 0-3, 앉음 4-5) · `ward_burn` 64×96×6(부적이 탐) · `slot_open` 64×64×6(슬롯 열림) · `mutation_vine` 192×256×24(자람 0-7, 숨 8-11, 스며듦 12-17, 시듦 18-23; TV-223) · `trace_fragment` 16×16×3(흔적 조각) · `screen_crack` 1920×1080×3 · `max_sky_band` 1920×240×4 · `offering_slot` 56×56×2(제물 칸: 빔/참) · `ward_slot` 48×80×2(부적 칸) · `artisan_slot` 24×24×2(기운 칸) · `ward_attached` 48×80×1 · `mutation_card` 480×272×1 · `result_card` 560×320×2(보통, 금) · `title_ribbon_normal`/`_gold` 720×96×4 · `stage_digits_tint`/`_gold`/`_muted` 48×64×11(글자 순서 `+0123456789`, 그림을 열어 확인함).
- 솔로 제외(서버 알림): `plaza_notice_normal/gold` 960×80×3, `news_line_normal/gold` 960×40×1.

### 5.4 `enemy/` (적 탄·구역 — 위험색 허용 묶음)
`round_small` 16×16×2(판정 0.12칸) · `round_large` 24×24×2(0.18칸) · `pine_needle` 24×8×1 · `feather` 24×12×2 · `ice_shard` 16×16×1 · `shadow_needle` 24×12×2 · `vine_seed` 16×16×2 + `seed_shadow` 16×8×1 · `thorn_ground` 96×96×2 · `ember_lane` 64×64×3 · `spore_cloud` 128×128×4 · `frost_field` 160×160×2 · `ice_pillar` 64×96×6(솟음 0-1, 서 있음 2, 깨짐 3-5) · `rock` 64×64×5(떨어짐 0-1, 놓임 2, 깨짐 3-4) · `soil_crack` 64×32×2 · `stain` 64×64×1(판정 자국) · `lightning_strike` 64×128×3(세로 반복) · `soil_particle` 8×8×1 · `explosion` 128×128×5 · `death_smoke` 48×48×4 · `elite_column` 32×128×3 · `toad_buff` 64×32×3 · `stun_star` 32×16×3 · `rage_smoke` 32×48×4 · `mirror_shards` 16×16×2 · `impact_circle` 128×128×3 · `impact_fan` 128×128×3 · `impact_lane` 128×64×3 · `rage_slot` 16×16×2 · `final_rage_ribbon` 480×64×3 · `mirror_silver_particle` 8×8×2. 모두 fps 12. 예고 구역 자체는 셰이더(그림 없음).

### 5.5 `loot/` (전장 드랍)
`coin` 16×16×4 7L · `block_common`/`block_rare`/`block_heroic` 24×24×2 7L · `material_pack` 24×24×2 7L · `crow_pack` 64×64×4 7L · `season_drop` 32×32×4 7L(솔로에서도 한정 드랍이 나오는지는 다른 요약서 소관) · `health_shard` 16×16×2 7L · `exit_marker` 64×64×2 7L(출발 표식) · `drop_column` 16×64×3 12 · `campfire` 64×64×4 8L(야영 모닥불).

### 5.6 `wand/` (지팡이 장식, 속성 ×5)
`trail_<el>` 32×16×4 · `muzzle_<el>` 32×32×3 · `footprint_<el>` 32×16×4. fps 12.

### 5.7 `status/` (HUD·상태)
`combo_digits` 24×32×10(0~9) · `damage_glyphs` 16×24×13(순서 `0123456789!▲▼`, 그림을 열어 확인함) · `critical_ring_chevrons` 8×8×2 · `health_full` 32×32×1 · `health_empty` 32×32×1 · `health_break` 32×32×4 · `crystal_empty` 24×24×1 · `crystal_debt` 24×24×1(그믐 빚) · `crystal_cracked` 24×24×1(금 감 겹그림, 성에 결투용) · `crystal_break` 32×32×4 · `crystal_particle` 16×16×3 · `armor_bounce` 32×32×3 · `frost_mark` 16×8×1 · `frozen_shell_64` 64×64×1 · `frozen_shell_96` 96×96×1 · `frozen_shatter` 96×96×4 · `static_outline` 64×64×2(감전 떨림) · `slow_ankle_ring` 32×8×2 · `slow_ankle_ring_double` 32×8×2 · `corrosion` 32×32×2 · `corrosion_tail` 8×16×2 · `frost_shield` 192×32×3(성에) · `meteor_fall` 32×64×3 · `meteor_ground` 32×32×4 · `frost_particle` 4×4×2. 찬 결정 칸은 `player/crystal_<el>`.

### 5.8 강화 화면 보강 (`art/sprite-pack/native-64/enhancement/`, 정본에 없는 것)
`manifest.json`: `referenceSize [1920,1080]`, `layout {header [0,0,1920,80], circuit [0,80,480,840], stage [480,80,960,840], probability [1440,80,480,840], footer [0,920,1920,160]}`, `new.<키>.{sheet, size, fps, loop, frames[{file, rect}], pivot?}`.

| 키 | 파일 | 프레임 크기 × 수 | fps/반복 | 대응 (FX 20.1) |
|---|---|---|---|---|
| (배경) | `background/workbench.png` | 1920×1080 | — | 강화 화면 배경(작업대) — production 없음 |
| `candle` | `background/candle/sheet.png` | 40×23 × 2 | 6 L | 촛불 겹그림 2프레임 |
| `master_sleep` / `master_peek` / `master_awake` / `master_settle` | `background/master_*/sheet.png` | 64×64 × 2/2/3/2 | 2L / 6 / 7 / 5 | 구석의 작은 느루(졸기, 한쪽 눈, 눈 뜸, 다시 졸기). pivot (32,60). 대안: `world/sprites/ritual.master.reaction.png` 64×64×7 (oneEye 0-1, awake 2-4, doze 5-6) |
| `seal_layers` | `ritual-new/seal_layers/sheet.png` | 240×240 × 5 | — | 공용 마법진 5겹(겹마다 따로 회전) |
| `sigil_<el>` | `ritual-new/sigil_<el>/sheet.png` | 240×240 × 1 | — | 속성 무늬(=`ritual/seal_<el>` 대안) |
| `protected_stamp` | `ritual-new/protected_stamp/sheet.png` | 96×48 × 3 | 12 | "지켰다" 도장 — production 없음 |
| `mutation_icons` | `ui-native/mutation_icons/sheet.png` | 32×32 × 6 | — | 변이 아이콘 6종 — production 없음. 순서 = 변이 코드 1~6 |
| `talisman_paper` | `new/talisman_paper/sheet.png` | 48×80 × 1 | — | 부적 종이 |
| `warm_ember` | `new/warm_ember/sheet.png` | 24×24 × 1 | — | 기운 불씨 |
| `circuit_panel` / `odds_panel` | `ui-native/*/sheet.png` | 432×792 | — | 왼쪽·오른쪽 칸 바탕 |
| `cost_panel` | | 432×160 | — | 기댓값/비용 판 |
| `wand_picker` | | 1200×720 | — | 지팡이 고르기 목록 창 |
| `release_confirm` | | 640×400 | — | 떠나보내기 확인 창 |
| `engrave_modal` | | 800×560 | — | 슬롯 새기기 창 |
| `mutation_remove_modal` | | 720×480 | — | 변이 제거 창 |
| `enhance_button_{normal,hover,pressed,disabled}` | | 320×64 | — | [강화 J] 주 버튼(속성색 입힘 전 회색 단계인지 미확인) |
| `picker_arrow_left` / `_right` | | 24×24 | — | 지팡이 고르기 화살표 |
| `trace_hidden` | | 16×16 | — | 흔적 가림 아이콘 |
| `rapid_off/on`, `repeat_off/on` | | 48×24 | — | [빠른 강화]·[이어서 강화] 토글 |
| 미리보기 | `previews/*.png`, `ritual.gif` | 1920×1080 | — | 배치 참고(반입 안 함) |

`existingUI`(125개)·`existingRitual`·`wands` 키는 production 사본을 가리키므로 production 쪽을 쓴다.

---

## 6. UI (`art/production/ui/`)

### 6.1 형식
`manifest.json`: `reference_resolution [1920,1080]`, `sampling nearest`, `atlases`(23묶음 × `atlas/<묶음>-0.png` 각 2048×2048), `external`, `assets`(627 항목, 고유 ID 197). 항목 필드: `id`, `file`(ui 폴더 기준), `width`·`height`(**프레임 하나**), `frames`(가로 띠), `frame_ms`, `nine_slice_ltrb [l,t,r,b]`, `atlas {page, rect}`(띠 전체), `element`, `block_kind`, `tint*`, `frame_offsets`, `flip_for_other_corners`, `hotspot`, 글꼴 전용 `cell/cell_width/cell_height/columns/baseline/glyph_map/characters`. 627개 파일 모두 존재, 크기 = (width×frames, height) 전부 일치(검사함). 아틀라스 없는 16개 = 글꼴 6, S-0203 런처 4, 1920×1080 배경 6.

- 낱장 PNG(`kit/`, `icon/`, `font/`, `screen/<화면>/`)를 그대로 쓰거나 `atlas.rect`로 아틀라스에서 잘라 쓴다. Unity 순정이면 낱장을 Sprite로 들이고 9조각은 Sprite Editor Border = `nine_slice_ltrb`(Unity 순서 L,B,R,T로 바꿈)로 넣으면 된다.
- 같은 ID 안의 변형 고르기 열쇠: 파일 이름에서 `<id 소문자>_` 떼고 `_<w>x<h>` 앞 = Variant, 뒤 토큰 = State(없으면 normal, `_fN` 제외). 예 `k-20_button_large_48x64_hover` → (`button_large`, `hover`) (TV-188~190, 구현 `src/Wizard.ViewLogic/Assets/UiAssetName.cs:22`).
- 상태 고르기(S14 4.3): !Enabled → disabled > Pressed → pressed > On → on > Hover(마우스) → hover > normal. 선택 괄호 K-40은 별도 겹그림.
- 색 입힘: 분필 흰색/회색으로 그린 그림에 코드로 곱한다(`tint` 필드가 있는 33개, 예 K-23 강화 버튼에 속성색, 금색 금지).

### 6.2 글꼴 (`ui/font/`)
| ID | 파일 | 규격 |
|---|---|---|
| F-12 | `f-12_hangul_atlas_2048x2048.png` + `f-12_glyphs.json` | 12px 비트맵, 칸 14(여백 1), 146열, baseline 11. 글리프 json = `{pixel_size, columns, cell_size, gutter, baseline, characters(순서 문자열), advances, glyph_vertical_offsets}`. 원본 `Galmuri11.ttf` + `Galmuri11-Condensed.ttf`(반각 ASCII) |
| F-16 | `f-16_hangul_atlas_2048x2048.png` + `f-16_glyphs.json` | 16px, 칸 18, 113열, baseline 13. 원본 `neodgm.ttf` |
| F-24/32/48/64 | (없음) | F-12 2배 / F-16 2배 / 3배 / 4배. 줄 높이 F-12 16, F-16 24, F-24 32, F-32 40, F-48 56, F-64 72 (S14 4.3) |
| F-N1 | `f-n1_numbers_180x16.png` | 10×16 × 18자 `0123456789,.:%+−×/` |
| F-N2 | `f-n2_numbers_360x32.png` | 20×32 × 18자(같은 순서) |
| F-N3 | `f-n3_numbers_880x128.png` | 80×128 × 11자 `0123456789%` — 강화 화면 큰 확률 숫자 |
| F-N4 | `f-n4_probability_cap_64x32.png` | "상한" 딱지 1장 |

TTF 3종과 OFL 원문(`Galmuri-OFL.txt`, `neodgm-OFL.txt`)이 같이 있어 Unity 순정 Text/TextMeshPro에 TTF를 Point 필터로 써도 된다(11,172자 전부 포함). 글자 크기는 위 6가지만.

### 6.3 ID 목록 (솔로에서 쓰는 것) — `크기: 변형[_상태]` (`@`=크기 자리, 9s=9조각 ltrb, fN=프레임)

공용 부품 `ui/kit/`:
- K-01 판 48×48 9s16 · K-02 안쪽 판 48×48 9s16 · K-03 쪽지 판 48×48 9s16 · K-04 제목 띠 96×48 9s[24,12,24,12] · K-05 화면 제목 띠 96×80 9s[24,16,24,16] · K-06 아래 조작 안내 띠 96×48 9s[16,8,16,8] · K-07 구분선 16×4 / 4×16 · K-08 툴팁 24×24 9s8 · K-09 툴팁 꼬리 12×8 · K-11 토스트 48×48 9s16 · K-12 귀퉁이 장식 32×32(chalk_scrap, clothespin, crow_feather).
- K-20 큰 버튼 48×64 9s12 (normal/hover/pressed/disabled) · K-21 보통 48×48 · K-22 작은 32×32 9s8 · K-23 주 버튼 48×64 · K-24 조심 버튼 48×48 · K-25 탭 48×48 (normal/hover/on/disabled) · K-26 세로 탭 48×56 (normal/hover/on) · K-27 토글 48×24 (toggle_off, on, disabled) · K-28 체크 24×24 (checkbox_empty, on, disabled) · K-29 라디오 24×24 · K-30 슬라이더 rail 16×8 9s2, fill 16×8 9s[4,2,4,2], thumb 16×24 (normal/hover/pressed) · K-31 드롭다운 48×40 9s12 (normal/hover, dropdown_expanded) · K-32 단계 화살 24×24 (normal/hover/disabled) · K-33 입력 칸 48×40 9s12 (normal/disabled, input_editing, input_error) · K-34 스크롤 rail 8×16 9s2, thumb 8×24 · K-35 닫기 32×32 · K-36 진행 막대 rail 16×16 9s5, fill 9s4 · K-38 쪽 넘김 32×32.
- K-40 선택 괄호 16/24/32 정사각 ×2 (250ms, `frame_offsets [[-4,-4],[-6,-6]]`, 한 모서리 그림 뒤집어 넷) · K-41 확정 체크 16/24 · K-42 새 소식 점 12×12×2 250ms · K-43 자물쇠 16/24 · K-44 불러오는 모닥불 32×32×6 100ms(0.6초=36틱, TV-187) · K-45 숫자 딱지 16×16 9s4 · K-46 꼬리표 24×16 9s4.
- K-50 키 마개 24×24 9s6 (normal/pressed) · K-51 큰 키 마개 48×48 9s12 · K-52 마우스 24×24 (left/right/wheel) · K-53 방향 입력 문양 16/24 (forward, back, side, reverse_side) · K-54 K·저스트 문양 16/24 · K-55 입력 이음줄 8×16 (connected/gap).
- K-60 물건 칸 64×64 (empty/filled/locked/unavailable) · K-61 작은 칸 32×32 · K-62 지팡이 칸 80×80 (empty/filled/expedition_locked) · K-63 코어 칸 72×72 · K-64 확장 칸 64×64 (closed/open) · K-65 칸 이음 16×8 (normal, flow ×2 120ms) · K-66 등급 테 64×64 (normal=일반/rare/hero) · K-67 등급 테 32×32 · K-68 카드 등급 테 64×64 9s24 · K-69 빌림 점선 테 64×64 (borrowed_slot, borrowed_card 9s16) · K-70 HUD 회로 칸 40×40 (off, on ×2 110ms) · K-71 조립 경고 16/24 · K-73 같은 속성 점 8×8 · K-80 변이 칸 48×48 (empty/filled/next_to_replace) · K-81 변이 비용/이득 16×16.
- 코드로 만드는 것(그림 없음): K-10 창 뒤 어둡게(먹색 60%), K-37 수량 조절, K-47 원형 남은 시간, K-72 끌기 그림자.

아이콘 `ui/icon/`:
- I-B1 블록 48×48 / I-B2 24×24 × 16 (`block_<k>`; 필드 `element`, `block_kind`) · I-B3 블록 종류 바탕 24/48 (plate_projectile/control/modifier).
- I-E1 속성 32×32 × 6(무 포함) · I-E2 64×64 × 5(무 없음) · I-E3 16×16 × 6(피해 숫자 옆·보스 바 문양 겸용, C-12) · I-E4 약점/저항 틀 24×24 9s8.
- I-T1 비기 48×48 / I-T2 24×24 × 15 (`<art 키>`) · I-T3 난이도 점 8×8 (empty/filled) · I-T4 결정 비용 16×16 (diamond, debt).
- I-S1 능력치 24×24 × 11 · I-S2 패시브 48×48 × 5 · I-S3 지팡이 특성 48×48 × 5 · I-S4 능력치 막대 16×8 9s[4,2,4,2] (rail/fill) · I-S5 아이템 레벨 24×24.
- I-M1 강화석 48×48 / I-M2 24×24 (stone_low/mid/high) · I-M3 부적 24/48 (safeguard_charm) · I-M4 꾸러미 48 (material_bundle, crow_bundle) · I-M5 시즌 천 24/48 · I-M6 지팡이 상자 48 · I-M7 옷장 분류 32 × 12 · I-M9 서고 확장·닉네임 48.
- I-C1 16 / I-C2 24 / I-C3 48 × (gold, expedition_purse, season_coin, starlight). **starlight는 유료 상품 0개라 어떤 화면에도 내지 않는다**(S14 3.11).
- I-G1 지팡이 단계 테 80×80 × 5 · I-G2 흔적 딱지 24×24.
- I-F1 메뉴 32×32 × 16 · I-F2 기능 24×24 × 21 · I-F10 감정표현 48 × 8 · I-F11 설정 분류 32 × 6 · I-F12 프로필 표지 160×40 (profile_offline, profile_server) · I-F13 기록 딱지 96×32 (record, pending, verified, deadline — 솔로는 record만).

화면 전용 `ui/screen/<화면>/` (솔로 화면):
- title: S-0201 모닥불 겹그림 128×128×4 125ms + 외부 배경 `../backgrounds/title_1920x1080.png` · S-0202 제목 그림 960×320 · S-0203 실행 파일 아이콘 1024/256/64/32.
- profile: S-0301 프로필 카드 그림 496×240 (offline_card, server_card) · S-0302 비밀번호 점/눈(솔로 불필요).
- offline: S-0401 "서버에 접속하면 열립니다" 묶음 상자 48×48 9s16 (오프라인 메인 메뉴에서 서버 전용 메뉴를 묶는 데 사용).
- menu: S-0501 메인 메뉴 배경 1920×1080 + 모닥불 128×128×4 125ms · S-0502 모드 카드 그림 368×240 (mode_expedition; mode_duel·mode_square는 묶음 상자 안) · S-0503 캐릭터 받침 320×96.
- expedition: S-0601 캐릭터 카드 336×376 (normal/selected) · S-0602 지팡이 받침대 224×48.
- hud: S-0801 웨이브 진행 바 24×12 9s[6,3,6,3] (rail/fill) · S-0802 초상 테 72×72 (normal, portrait_hit) · S-0803 보스 체력 바 32×16 9s[8,4,8,4] (rail/fill) + 눈금 4×16 + 격노 불씨 16×16×2 220ms · S-0804 점수/처치 24×24 · S-0805 과열 잠금 딱지 64×24.
- loot: S-1001 전리품 카드 240×336 (loot_front/loot_back) · S-1002 카드 뒤집힘 240×336×3 33ms.
- assembly: S-1101 트리 마디 32×32 × 4 · S-1102 스코프 선 8×16/8×8 · S-1103 분열 가지 96×24 (split_2/3) · S-1104 생략 마디 · S-1105 이음선 · S-1106 실행 번호 16×16 · S-1107 수치 아이콘 24×24 (dps/heat/threshold) · S-1108 서고 칸 96×112 (normal, empty).
- enhance: S-1201 결과 띠 16×24 9s4 (odds_rail/fill/boundary) · S-1202 확률 내역 머리 16×16 (base/sacrifice/same_element/momentum) · S-1203 새기기 누적 눈금 16×16 (empty/filled) · S-1204 목표 단계 깃발 16×24.
- techniques: S-1301 트라이 마디 64×64 (candidate/current ×2 250ms/locked/unlocked) + 점 마디 24×24 (on/off) · S-1302 가지 16×16 (line/corner × on/off) · S-1303 해금 후보 카드 320×320.
- training: S-1401 입력 간격 막대 16×16 9s4 + 표식 8×16 (valid/late) · S-1402 저스트 게이지 192×24 (rail/window/marker) · S-1403 무한 표시 24×16.
- results: S-1601 귀환 가방 760×120 (closed/open) · S-1602 정산 배경 1920×1080 · S-1603 쓰러진 캐릭터 받침 384×96.
- settings: S-1901 옵션 미리보기 424×240 × 7 · S-1902 키보드 바탕 1200×280 · S-1903 칭호 줄 32×32 9s8 (ordinary/thirteen/heir).
- shared: S-2601 불러오는 화면 배경 1920×1080 · S-2602 마우스 커서 32×32 (normal/click/drag/text, `hotspot` 있음) · S-2603 복수 거울 표식 32×32 (whole/cracked/shattered).
- mail_wardrobe 중 솔로에서 쓸 수 있는 것: S-2402 옷장 미리보기 560×880 · S-2403 치장 칸 96×96 · S-2404 대화 판 96×96 9s32(허상 첫 만남 대사 판) · 외부 S-2405 느루 대화 초상(`world/sprites/S-2405.png` 128×128×5: sleep 0, oneEye 1, awake 2, talk 3-4, 튜토리얼 안내).
- market 중: S-2305 상점 띠 1200×96 · S-2306 상품 카드 216×280 · 외부 S-2407 행상 대화 초상(`world/sprites/S-2407.png` 128×128×3: idle 0, talk 1-2) — 오프라인 상점(골드)에서 사용 가능.

솔로 제외(온라인 전용 그림): I-L1~I-L9(리그·순위), I-F3(사람 관계), I-F4·I-F5·I-F6·I-F7·I-F8·I-F9(접속·회선·우편·채팅·방 옵션), S-0901~S-0907(결투), S-1701(결투 결과), S-2001~S-2007(광장), S-2101~S-2105(결투 게시판), S-2301~S-2304·S-2307(거래·장터), S-2401·S-2406(우편함·후계자 점수), S-2501~S-2506(리그·순위표·비석).

---

## 7. 배경·월드 (`art/production/world/`, `art/production/backgrounds/`)

### 7.1 manifest 형식
`world/manifest.json`: `tilePx 64`, `textureFilter nearest`, `sheetOrder row-major`, `paletteTargets.{day,dusk,night}.{grass,dirt,packed,slate,paving,hedge}`(색 값), `layouts`(맵 8개), `shared {plaza.grass → field.tile.grass.*, plaza.alert_crow → field.crow, arena.master_idle → facility.master_chair}`, `assets.<id>`:
- 타일형: `{size [64,64], variants {day|dusk|night: {file, rect}}}` — 시간대마다 **미리 칠한 다른 시트**(색 교체 코드 불필요).
- 스프라이트형: `{file, frameSize, frameCount, columns, origin, fps?, poseFrames?, mirrorFor?, collision?, …}` — 칸 = `(i % columns, i / columns)`.

시간대(S14 3.9): 웨이브 `r = ((w−1)%10)+1` → 1~4 day, 5~7 dusk, 8~9 night, 10 boss(=night 팔레트 + `field.moon` + `field.moonlight`).

### 7.2 타일 시트
| 파일(× day/dusk/night 3장) | 픽셀 | 내용 (8열, 행 우선) |
|---|---|---|
| `world/tiles/field.<tod>.png` | 512×192 | 24칸: `field.tile.grass.01~04`, `dirt.01~04`, `edge.n/e/s/w`, `outer.nw/ne/se/sw` + `inner.nw/ne/se/sw`(교대로), `mottle.01~04` |
| `world/tiles/plaza.<tod>.png` | 512×192 | `plaza.tile.packed.01~04`, `paving.01~06`, `edge.*`, `outer/inner.*` (풀은 field 것 공유) — 솔로 제외(광장) |
| `world/tiles/arena.<tod>.png` | 512×64 | `arena.tile.slate.01~04` — 솔로 제외(돌마당) |
정확한 rect는 각 타일 항목의 `variants.<tod>.rect`.

### 7.3 전장(무한 웨이브) 스프라이트 — 솔로 필수
| id | 파일 | 프레임 | 수 | origin | 비고 |
|---|---|---|---|---|---|
| `field.wall.n.01~03`, `field.wall.s.01~03` | `sprites/field.wall.*.png` | 64×64 | 1 | (32,62) | 가시덤불 울타리 윗변/아랫변 |
| `field.wall.w.01~02` | | 64×64 | 1 | (32,62) | 왼쪽, `mirrorFor: e`(오른쪽은 반전) |
| `field.wall.corner.nw/ne/se/sw` | | 64×64 | 1 | (32,62) | 모서리 |
| `field.wall.rocks.01~03` | | 64×64 | 1 | (32,62) | 울타리에 섞는 돌무더기 |
| `field.wall.rim.dusk` / `.night` | 192×64 | 64×64 | 3 | (32,62) | 가장자리 윗면 테두리빛 덧그림 |
| `field.spawn.n` / `.s` | 640×64 | 128×64 | 5 | (64,62) | 등장구(덤불 틈). openFrame 0, shakeFrames 1-4, fps 8. 칸 2×1 |
| `field.spawn.w` | 320×128 | 64×128 | 5 | (32,126) | 왼쪽 등장구, `mirrorFor: e`. 칸 1×2 |
| `field.prop.sign` | 64×64 | | 1 | (32,62) | 표지판. `displayText`에 글자가 박혀 있음(아래 모순 참고) |
| `field.prop.potion.round` / `.triangular`, `target`, `discarded_tongs/pot/hook/slate/pouch/rope` | 64×64 | | 1 | (32,62) | 납작한 소품, `collision false` |
| `field.prop.discarded_chair`, `mushroom_ring` | 128×64 | | 1 | (64,62) | |
| `field.tower` | 2560×128 | 512×128 | 5 | (256,126) | 탑 밑동과 빨랫줄, idle 0 / laundry 1-4, fps 6. 위쪽 가장자리 너머 |
| `field.tower.window` | 256×64 | 64×64 | 4 | (32,62) | 느루의 창문 closed 0, look 1-2, throw 3, fps 6 |
| `field.campfire` | 256×64 | 64×64 | 4 | (32,62) | 야영 모닥불 fps 8 (FX `loot/campfire`와 같은 용도) |
| `field.moon` | 96×96 | | 1 | (48,94) | 보스 웨이브 달(화면 밖 위) |
| `field.moonlight` | 256×256 | | 1 | (128,128) | 보스 웨이브 바닥 달빛 원(판정 없음) |
| `field.weather.rain` | 7680×128 | 1920×128 | 4 | (0,0) | 빗줄기 화면 띠 fps 9 (`event.shower`) |
| `field.weather.puddle` | 192×32 | 64×32 | 3 | (32,16) | 물웅덩이 반사 fps 5 |
| `field.weather.fog` | 1024×192 | 512×192 | 2 | (0,0) | 안개 띠 fps 3 (`event.fog`) |
| `field.meteor.ring` / `.fall` / `.sparkle` | 128/192/128×32 | 32×32 | 4 / 6 / 4 | (16,16) | 별똥 결정(`event.star_crystal`): 예고 고리 fps 6, 낙하 fps 12, 반짝임 fps 8 |
| `field.crow` / `field.crow.note` | 128×32 / 64×32 | 32×32 | 4 / 2 | (16,16) | 까마귀 날기 fps 9 / 쪽지 fps 7 (`event.master_advice`, 까마귀 보따리) |
| `field.backplate` | `backplates/field.png` 1920×1080 | | 1 | | 풍경 구성용. 판정 형상은 모듈 타일로("use modular assets for exact gameplay geometry") |

### 7.4 맵 배치 (`world/maps/`)
- `field.01.json` ~ `field.06.json`: `{id, tilePx 64, sizeTiles [24,14], ground[14][24](타일 id), walls[{tile[x,y], sprite, mirrorX}], spawns[{side n|s|w|e, tile, tileExtent, sprite, mirrorX}](각 변 3개, 총 12), props[{tile, sprite}], wallCollision(설명 문자열), campfireCenter [12,7]}`. 날이 바뀔 때마다 6벌을 돌려 쓴다(S14 4.7, `field.layout_rotated`). 전장 = 24×14칸 = 1536×896px, 화면 가운데(좌우 192, 위 104, 아래 80 여백, UI 4.8).
- `plaza.json`(40×24칸, facilities/decorations), `arena.json`(24×14, pillars, chalk) — 솔로 제외.

### 7.5 시설·NPC 스프라이트 (솔로에서 쓸 수 있는 것)
| id | 파일 픽셀 | 프레임 × 수 | origin | poseFrames / fps | 쓰는 곳 |
|---|---|---|---|---|---|
| `facility.training_dummy` | 256×96 | 64×96 × 4 | (32,94) | 서기 1 + 맞는 반응 3, fps 10 | 허수아비 연습(솔로 필수) |
| `facility.master_chair` | 768×192 | 96×96 × 12 (8열 2행) | (48,94) | sleep 0-3, wake 4-5, speak 6-7, reward 8-11, fps 6 | 느루(튜토리얼·메뉴 연출에 선택 사용) |
| `ritual.master.reaction` | 448×64 | 64×64 × 7 | (32,62) | oneEye 0-1, awake 2-4, doze 5-6, fps 7 | 강화 화면 구석 느루 반응(FX 20.1 "2/3/2") |
| `npc.merchant` | 256×64 | 64×64 × 4 | (32,62) | idle 0-1, gesture 2-3 | 행상(오프라인 골드 상점 연출, 선택) |
| `S-2405` / `S-2407` | 640×128 / 384×128 | 128×128 × 5 / × 3 | (64,126) | 6.3절 | 느루·행상 대화 초상 |
솔로 제외(광장·돌마당): `facility.campfire`(128×128×6), `facility.log_seat`, `facility.market.mat/crate`, `facility.duel_board`, `facility.arena_path`, `facility.heir_monument`, `facility.merchant_cart`, `facility.crow_mail`, `facility.field_gate`, `plaza.tower`, `plaza.prop.*`(8종), `arena.*`(벽·기둥·관중·분필·느루 박수·흐린 하늘), `plaza.backplate`, `arena.backplate`.

### 7.6 배경 (`art/production/backgrounds/`)
| 파일 | 픽셀 | 비고 |
|---|---|---|
| `title_1920x1080.png` | 1920×1080 | 타이틀 배경 = UI external `S-0201`. 지금 판은 `draw_title.py`(480×270 격자 → 4배)로 그린 것(작업 트리에서 수정됨). `generated-art.json`의 ImageGen 설명은 이전 판 |
| `title_preview.png` / `.webp` | 960×540 / — | 미리보기(반입 안 함) |
그 밖의 1920×1080 화면 배경은 UI에 있다: S-0501(메인 메뉴), S-1602(정산), S-2601(불러오기), S-2307·S-2506·S-0901(솔로 제외). 강화 화면 배경은 5.8절 `workbench.png`.

---

## 8. 콘텐츠 id ↔ 그림 대응

| 콘텐츠 | UI 아이콘 | 전투 그림 |
|---|---|---|
| `block.magic_bolt` | I-B1/I-B2 `block_magic_bolt` | FX `player/mana_bolt` |
| `block.fireball` | `block_fireball` | `player/fireball` |
| `block.chain_lightning` | `block_chain_lightning` | `player/chain_lightning` |
| `block.boomerang` | `block_boomerang` | **없음** → `mana_bolt` 청록 그대로 대체 (U-17) |
| `block.ice_spike` | `block_ice_spike` | `player/ice_lance` + `ice_muzzle` + `ice_tail` |
| `block.shadow_orb` | `block_shadow_orb` | `player/shadow_orb` + `_cast`/`_ripple`/`_fade` |
| `block.pierce` | `block_pierce` | (덧그림) `player/piercing` [임시: U-17] |
| `block.split_two/split_three/on_hit_trigger/wall_bounce/double_size/frost/homing/triple_heat/corrosion` | `block_<k>` | 탄 그림 없음(변형·제어는 탄 몸을 바꾸지 않음). frost → `status/frost_particle`·서리 테 셰이더, corrosion → `status/corrosion_tail` |
| 블록 종류 | I-B3 `plate_*`, S-1101 | — |
| `art.burst`(①) … `art.bloom`(⑩) | I-T1/I-T2 `<k>` (burst, recoil, fan, wings, afterimage, spear, vent, octastar, thunder_spear, bloom) | 발동 문양 `special/cast_seal_01~10` + ① `release_<el>` ② `muzzle_storm_<el>`+`dodge_dust` ③ `fan_<el>` ④ `wing_<el>` ⑤ 잔상 셰이더+`dodge_dust` ⑥ `spear_shaft/tip_<el>`+`break_armor` ⑦ `purge_steam`+충격파 셰이더 ⑧ `octagram_<el>` ⑨ `thunderlance_shaft/tip_<el>`+`just_star` ⑩ `bloom_<el>`+`petal_<el>` (S14 5.6 C-03) |
| `art.kiln`(char.1) | `kiln` | `cast_seal_11`, `kiln_border`, `kiln_ember` |
| `art.bolt_dash`(char.2) | `bolt_dash` | `cast_seal_12`, `lightning_dash` |
| `art.copy_circle`(char.3) | `copy_circle` | `cast_seal_13`, `arcane_copy`, `scattered_pages` |
| `art.ice_screen`(char.4) | `ice_screen` | `cast_seal_14`, `ice_screen_horizontal/vertical`, `_ripple`, `_shard` |
| `art.shadow_hand`(char.5) | `shadow_hand` | `cast_seal_15`, `shadow_pool`, `shadow_hand`, `shadow_arm`, `shadow_fist` |
| `element.<k>` | I-E1/I-E3 `element_<UI키>`(ice→frost, dark→shadow), I-E2(무 없음) | FX 접미사 `_<k>` 그대로 |
| `passive.<k>` / `wand_trait.<k>` | I-S2 / I-S3 `<k>` | — |
| `mutation.*` (코드 1~6) | **production 없음** → native-64 `mutation_icons` 칸 0~5 (그 파일의 키 이름 `oversensitive_circuit`, `mirror`는 id `hypersensitive_circuit`, `mirror_image`와 다름 — 순서로 대응) | 변이 칸 K-80, 변이 덩굴 `ritual/mutation_vine`, 변이 카드 `ritual/mutation_card` |
| `mat.stone_low/mid/high` | I-M1(48)/I-M2(24) `stone_*` | 드랍 `loot/material_pack` |
| `charm.safeguard` | I-M3 `safeguard_charm` | 부적 `ritual/ward_*`, `ward_burn` |
| `item.material_bundle` / `item.crow_bundle` | I-M4 | `loot/material_pack` / `loot/crow_pack` |
| `item.wand_box` | I-M6 `wand_box` | — |
| `item.health_shard` | — | `loot/health_shard` |
| `currency.gold` / `currency.expedition_purse` | I-C1~C3 `gold` / `expedition_purse` | `loot/coin` |
| `currency.season_coin` / `currency.starlight` | `season_coin` / `starlight` | 솔로 제외(시즌 주화는 서버 프로필, 별빛은 표시 금지) |
| 드랍 블록 조각 | — | `loot/block_common/rare/heroic` (등급 `common/rare/hero`) |
| `enemy.*`, `elite.*`, `boss.*` | 보스만 `monsters/boss.<k>.portrait/hud.png` | 4절 |
| `emote.<k>` | I-F10 `greet/clap/taunt/bow/sit/dance/raise_wand/despair` | 캐릭터 클립 `wave/clap/taunt/bow/sit_down_down→sit_down/dance/lift_wand/frustrated_down` (S14 3.15 C-04) |
| `tod.*`, `whim.*`, `frame.*` | **아이콘 없음** | `tod` → 타일 팔레트 + 달/달빛 |
| `event.shower/fog/star_crystal/master_advice` | — | `field.weather.rain/puddle`, `field.weather.fog`, `field.meteor.*`, `field.crow`·`field.crow.note`·`field.tower.window` |
| `cosmetic_cat.<k>` | I-M7 `cosmetic_<k>` | — |
| `cosmetic.headwear.cracked_mirror_shard` | (I-M8 낱개 없음 → I-M7 대체) | sprite-pack layers `cosmetic/cracked-mirror-shard` |

---

## 9. 빠진 그림 (id·규격은 있는데 PNG 없음)

솔로에 영향 있는 것:

| 대상 | 규격(출처) | 처리 |
|---|---|---|
| 시간대 아이콘 4 (`tod.day/dusk/night/boss`) | 32×32 (MON 9.5, S14 U-10 열쇠 `ui/I-W3/<tod>`) | 대체 그림 |
| 변덕 아이콘 8 (`whim.*`) | 32×32 (`ui/I-W1/<whim>`) | 대체 그림 |
| 웨이브 틀 아이콘 5 (`frame.*`) | 32×32 (`ui/I-W2/<frame>`) | 대체 그림 |
| "빌림" 꼬리표 | 32×16 (`ui/I-M10/borrowed_tag`) | K-69 점선 테 + K-46 꼬리표로 대신 |
| 꾸러미 여는 연출 | 64×64 × 5 (`ui/I-M11/bundle_open`) | 대체 |
| 귀환 가방 칸 3종 (빈/찬/영웅) | 32×32 (`ui/I-M12/bag_slot_*`) | K-60 + K-66 으로 대신 가능 |
| 변이 표식 고리(지팡이에 붙는) | 32×32 × 2 (FX 20.1) | 대체 |
| 깨지는 조각 둘째 벌 | 64×64 × 18 × 5 (FX 20.1 "18개 × 2") | 첫 벌 재사용 |
| 부메랑 탄 | — | `mana_bolt` 대체(U-17) |
| ⑨ 뇌광창 흰 심 | 64×16 × 2 (FX 20.4) | 셰이더/생략 |
| ② "끊음!" 표시 | 64×24 × 3 (FX 20.4) | 대체 |
| ⑥ 긁힌 자국 | 64×16 × 1 (FX 20.4) | `enemy/stain` 축소 대체 [임시] |
| 반딧불 | 4×4 × 2 (FX 20.5, 밤 장식) | 코드로 점 |
| FX 빗줄기 4×24 / 물 튐 16×16×3 | (FX 20.5) | world `field.weather.rain`/`puddle` 사용 |
| 꾸미기 낱개 아이콘 I-M8 | (UI: 품목 데이터에서) | I-M7 분류 아이콘 대체 |
| 후계자 전용 꾸미기 3종 | 미정 (S14 3.14) | 솔로 제외 |
| 지팡이 겉모습(skin)별 시트 | (WC 3.3) | 기본 하나 [임시] |
| 몬스터 재생 fps | — | 미정 |

`effects/supplement/build.py`는 위 일부(변이 고리 반복, "지켰다" 글자, 흔적 가림, 화살표, 모닥불 색 겹그림, 긁힌 자국, 흰 심, 반딧불, 빗방울, 물 튐)를 그리려는 스크립트이나 **main/저장 호출 부분이 없고 함수 본문이 중간에 끊겨 있어 실행해도 PNG가 나오지 않는다**. 출력물 없음.

솔로 제외(온라인 전용, 빠진 것 무관): 모닥불 속성색 겹그림 96×128×4(광장 알림), 광장 시설은 모두 있음.

이제 있는 것(S14 기록과 달리): S-2405, S-2407, 전장·광장·돌마당 타일/시설 전부(`world/`), 보스 초상·HUD 6+6(`monsters/`), 반응 부품 71·야생 물 105·금 간 거울·성에 손(`sprite-pack/characters/players/layers/`), 변이 아이콘 6·"지켰다" 도장·흔적 가림·고르기 화살표·작업대 배경·느루 반응(`sprite-pack/native-64/enhancement/` 또는 `world/sprites/ritual.master.reaction.png`).

---

## 10. 기존 src 구현 참고 (엔진 무관, 순정 Unity로 옮길 때 알고리즘만 참고)

| 내용 | 위치 |
|---|---|
| 5개 매니페스트 → 통합 레지스트리 | `src/Wizard.ViewLogic/Assets/AssetManifestLoader.cs:14` (`LoadAll`) |
| 캐릭터 매니페스트 읽기(canvas, pivotPx, clips, 폴더→char.N) | `src/Wizard.ViewLogic/Assets/Loaders/PlayerManifestLoader.cs:20` |
| 몬스터 매니페스트(프레임별 origin) | `src/Wizard.ViewLogic/Assets/Loaders/MonsterManifestLoader.cs:19` |
| FX / UI / 지팡이 로더 | `Loaders/FxManifestLoader.cs`, `Loaders/UiManifestLoader.cs`, `Loaders/WandManifestLoader.cs` |
| 지팡이 칸 계산, 손잡이 부착, 반전 x | `src/Wizard.ViewLogic/Assets/WandSheet.cs:54` (HandRect), `:60` (InspectionRect), `:66` (IconRect), `:73` (HandRow), `:105` (AttachTopLeft) |
| 합성 순서(초롱 up 나눔 포함) | `src/Wizard.ViewLogic/Assets/CharacterComposite.cs:11` |
| UI 파일 이름 → 변형·상태 | `src/Wizard.ViewLogic/Assets/UiAssetName.cs:22` (`TrySplit`) |
| 격자 칸 rect | `src/Wizard.ViewLogic/Assets/SheetGrid.cs:30,43` |
| 없는 그림 대체 | `src/Wizard.ViewLogic/Assets/AssetPlaceholder.cs:20` |
| 별칭 표 | `data/presentation/asset_aliases.json` (element 별칭, character_folder, wand_folder, wand_detached, block_projectile_fx, ui_icons, character_sheets, wand_up_split) |
| 테스트 벡터(기대값 참고) | `tests/Wizard.ViewLogic.Tests/Assets/ManifestUiCharacterVectorTests.cs` |

world 매니페스트와 native-64·sprite-pack 매니페스트를 읽는 코드는 src에 없다(새로 짜야 함).

---

## 11. 미정 / 모순

1. **모순(S14 5.1·C-13·U-01·U-10 vs 현물)**: S14는 world/에 그림 없음, S-2405/2407 없음, 보스 초상·HUD 없음, 반응 부품·야생 물 없음이라 적었으나 지금은 전부 있다(위치는 9절 끝). 현물 기준으로 쓴다(문서가 낡음).
2. **모순(S14 5.6 "303장" vs `effects/` 307파일)**: 차이는 `supplement/build.py` 등 비PNG. manifest 303개는 그대로 맞다.
3. **모순(FX 20.1 "강화 반응 기다림·충전 그림 없음" vs 현물 `upgrade_wait/charge_down`)**: 현물 사용(S14 C-09와 같음).
4. **모순(FX 20.1 깨지는 조각 18×2벌 vs 현물 1벌)**: 1벌로 진행.
5. **모순(UI 4.12 강화 버튼 = K-23 속성색 입힘 vs native-64 `enhance_button_*` 320×64 전용 그림)**: 정본 UI는 K-23을 320×64로 9조각 늘려 쓰는 것. native-64 버튼을 쓸지 미정.
6. **미정**: native-64의 +7 큰 그림(새 그림)으로 production `inspection.png`의 +7 칸을 교체할지.
7. **모순(native-64 mutation_icons 키 `oversensitive_circuit`, `mirror` vs id `mutation.hypersensitive_circuit`, `mutation.mirror_image`)**: 순서(코드 1~6)로 대응한다.
8. **모순(이름 규칙 위반)**: `world/manifest.json`의 `field.prop.sign.displayText` = "탑 ← 3000걸음"이 그림 데이터에 화면 글자로 들어 있다(CLAUDE.md 5절: 화면 글자는 이름 표에만). 표지판 글자를 그림에 굽지 말고 이름 표 열쇠로 그릴지, 그림에 이미 박혀 있는지 미확인 → 미정.
9. **미정**: 몬스터 클립 재생 fps(매니페스트에 없음, README "게임에서 정함"). 일반 적 엘리트 덧그림의 자세별 부착 위치.
10. **미정**: 초상 4프레임의 표정 이름(0 기본, 1~3 표정, U-11).
11. **참고**: `status/damage_glyphs`(`0123456789!▲▼`)와 `ritual/stage_digits_*`(`+0123456789`)는 매니페스트에 글자 순서 필드가 없다. 순서는 그림을 직접 열어 확인했으므로 코드에 상수로 둔다.
12. **[임시]**: 지팡이 단계 점은 방향·자세별 값이 없어 모든 방향 공통, 왼쪽 x' = 31 − x (U-04). 분리 조각 anchor는 head 점에 (U-05).
13. **미정**: sprite-pack layers의 부착 offset·fps는 "엔진 QA용 시작값"(manifest notes, `timingSource: suggested`).
14. **참고(정본 판단)**: `sprite-pack/redrawn`(ImageGen), `combat-pixel`(다른 화풍), `native-64/characters`(1방향 5동작)는 동작 범위가 모자라 캐릭터 정본이 될 수 없다. 화풍을 바꾸려면 사용자 결정이 필요.
15. **참고**: `art/production/players/*`, `monsters/*` 등은 작업 트리에서 수정됨(HEAD와 다름). 이 문서의 크기·md5 판단은 작업 트리 기준.
