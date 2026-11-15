# Godot FX 굽기 결과

Godot 4.7.2(호환 렌더러, 창 모드)로 그린 `_v2` 교체 그림 목록. 원본 옆에 같은 이름 + `_v2`로 둔다.
프레임 크기, 프레임 수, 가로 한 줄 배치는 원본과 같다(`post.py`, `audit.py`가 크기를 검사한다).
`.meta`는 만들지 않았다. Unity 연결(v2 우선 로드)은 이 작업 범위 밖이다.

## 다시 굽기

```
solo/Tools/godot-fx/bake.sh            # 전부 (jobs.py -> Godot -> post.py -> tiles.py -> preview.py -> audit.py)
solo/Tools/godot-fx/bake.sh --only fire   # 작업 id에 'fire'가 들어간 것만
GODOT=<경로> solo/Tools/godot-fx/bake.sh  # Godot 실행 파일 경로를 바꿀 때
```

- `--headless`는 렌더러가 없어 쓰지 않는다. 창이 잠깐 떴다가 스스로 닫힌다(전체 약 15초).
- 중간 렌더는 `$TEMP/godot-fx-raw`(바꾸려면 `FX_RAW`)에 쌓인다. 저장소에 넣지 않는다.
- 생성물은 PNG `Software=godot-fx` 표식을 단다. 표식이 없는 `_v2`(손으로 만든 GPT 교체본)는 덮어쓰지 않고 멈춘다.
- `preview.png`: 생성물 전부, 위 줄 원본 / 아래 줄 `_v2`, 최근접 확대.

## 구성

| 파일 | 역할 |
|---|---|
| `project.godot`, `bake.gd` | `jobs.json`의 작업마다 SubViewport(프레임 크기 × ss배)에 셰이더를 그려 프레임별 PNG로 저장 |
| `shaders/*.gdshader` | burst(타격 섬광), ring(충격파 고리·호), smoke(연기·먼지·김), comet(꼬리), muzzle(발사 섬광), bolt(번개), pillar(빛기둥·불기둥), flame(불꽃 반복), orb(구슬·탄·티끌), star(네 갈래 반짝임), portal(소용돌이 바닥 문), telegraph(바닥 구역 질감), cracks(갈라짐), ground(이음매 없는 바닥 질감) |
| `jobs.py` | 작업 목록(대응 원본, 규격, 셰이더 값, 색 램프) |
| `palettes.py` | 8단 색 램프. dark·ice는 GPT v2 시트의 불투명 화소를 중앙값 분할로 뽑았다 |
| `post.py` | 축소, 램프 색칠, 알파 단계화, 시트 조립, 위험색 검사 |
| `tiles.py` | 바닥 타일 시트 조립(원본 낮 시트에서 재질 경계 모양을 그대로 따온다) |
| `preview.py`, `audit.py`, `dev.sh` | 미리보기, 크기 검사와 남은 목록, 일부만 굽는 개발용 |

## 스타일 맞춤

- 셰이더는 색 대신 "열"(0~1)과 덮임(알파)만 낸다. `post.py`가 ss×ss 칸을 알파 가중 평균으로 줄이고(가는 심지는 최댓값으로 살림), 열을 원소별 8단 램프의 칸 번호로 바꾼다. 그래서 한 장 안의 색 수가 램프 8색으로 묶이고 v2처럼 어두운 그림자색 → 몸통 4색 → 옅은 색 → 흰 심지 구조가 된다.
- 램프: dark·ice는 v2에서 직접 뽑은 값, fire·lightning·arcane·gold 등은 같은 명도 구조로 만든 값.
- 알파는 7단계로 끊는다(0, .22, .4, .58, .76, .9, 1). 반투명 화소는 램프 밝은 쪽(4번 이상)으로 올려 v2의 옅은 빛무리처럼 보이게 한다(연기·갈라짐은 예외).
- 외곽선은 검은 선을 두르지 않는다(v2에도 없다). 대신 덩어리 가장자리 1px을 램프 1~2칸 어둡게 한다(`rim`).
- v2에서 반복되는 "+" 반짝임, 흰 심지, 바깥으로 떨어져 나가는 파편을 공통 모티프로 썼다.
- 위험색(색상각 330~15도, 채도·명도 70% 이상): 플레이어 쪽 램프에는 그 색이 없고, `post.py`가 적 램프(enemy_red, enemy_mag, spore, shadow_e)가 아닌 시트의 모든 화소를 검사해 걸리면 쓰지 않는다. 이번 굽기에서 걸린 것 없음.

## 생성 목록 (86장)

### 효과 시트 77장 (`Assets/Resources/Art/` 기준, 대응 원본 = `_v2`를 뺀 이름)

| 파일 | 규격 | 셰이더 | 램프 |
|---|---|---|---|
| `effects/player/hit_arcane_32x32_f3_v2.png` | 32x32, 3프레임 | burst | arcane |
| `effects/player/hit_large_arcane_64x64_f4_v2.png` | 64x64, 4프레임 | burst | arcane |
| `effects/player/hit_fire_32x32_f3_v2.png` | 32x32, 3프레임 | burst | fire |
| `effects/player/hit_large_fire_64x64_f4_v2.png` | 64x64, 4프레임 | burst | fire |
| `effects/player/hit_lightning_32x32_f3_v2.png` | 32x32, 3프레임 | burst | lightning |
| `effects/player/hit_large_lightning_64x64_f4_v2.png` | 64x64, 4프레임 | burst | lightning |
| `effects/player/explosion_arcane_256x256_f5_v2.png` | 256x256, 5프레임 | ring | arcane |
| `effects/player/explosion_fire_256x256_f5_v2.png` | 256x256, 5프레임 | ring | fire |
| `effects/player/explosion_lightning_256x256_f5_v2.png` | 256x256, 5프레임 | ring | lightning |
| `effects/player/explosion_ice_256x256_f5_v2.png` | 256x256, 5프레임 | ring | ice |
| `effects/player/critical_hit_32x32_f3_v2.png` | 32x32, 3프레임 | burst | steam |
| `effects/player/critical_muzzle_32x32_f3_v2.png` | 32x32, 3프레임 | ring | steam |
| `effects/player/heat_lock_steam_32x32_f4_v2.png` | 32x32, 4프레임 | smoke | steam |
| `effects/player/mana_bolt_16x16_f2_v2.png` | 16x16, 2프레임 | orb | arcane |
| `effects/player/fireball_32x24_f4_v2.png` | 32x24, 4프레임 | flame | fire |
| `effects/player/chain_lightning_64x16_f2_v2.png` | 64x16, 2프레임 | bolt | lightning |
| `effects/player/graze_strike_32x128_f2_v2.png` | 32x128, 2프레임 | bolt | lightning |
| `effects/wand/muzzle_arcane_32x32_f3_v2.png` | 32x32, 3프레임 | muzzle | arcane |
| `effects/wand/trail_arcane_32x16_f4_v2.png` | 32x16, 4프레임 | comet | arcane |
| `effects/wand/footprint_arcane_32x16_f4_v2.png` | 32x16, 4프레임 | smoke | arcane |
| `effects/wand/muzzle_fire_32x32_f3_v2.png` | 32x32, 3프레임 | muzzle | fire |
| `effects/wand/trail_fire_32x16_f4_v2.png` | 32x16, 4프레임 | comet | fire |
| `effects/wand/footprint_fire_32x16_f4_v2.png` | 32x16, 4프레임 | smoke | fire |
| `effects/wand/muzzle_lightning_32x32_f3_v2.png` | 32x32, 3프레임 | muzzle | lightning |
| `effects/wand/trail_lightning_32x16_f4_v2.png` | 32x16, 4프레임 | comet | lightning |
| `effects/wand/footprint_lightning_32x16_f4_v2.png` | 32x16, 4프레임 | smoke | lightning |
| `effects/wand/muzzle_ice_32x32_f3_v2.png` | 32x32, 3프레임 | muzzle | ice |
| `effects/wand/trail_ice_32x16_f4_v2.png` | 32x16, 4프레임 | comet | ice |
| `effects/wand/footprint_ice_32x16_f4_v2.png` | 32x16, 4프레임 | smoke | ice |
| `effects/special/release_arcane_64x64_f4_v2.png` | 64x64, 4프레임 | ring | arcane |
| `effects/special/release_fire_64x64_f4_v2.png` | 64x64, 4프레임 | ring | fire |
| `effects/special/release_lightning_64x64_f4_v2.png` | 64x64, 4프레임 | ring | lightning |
| `effects/special/release_ice_64x64_f4_v2.png` | 64x64, 4프레임 | ring | ice |
| `effects/special/dodge_dust_32x32_f4_v2.png` | 32x32, 4프레임 | smoke | smoke |
| `effects/special/purge_steam_32x32_f4_v2.png` | 32x32, 4프레임 | smoke | steam |
| `effects/special/kiln_ember_8x8_f2_v2.png` | 8x8, 2프레임 | orb | fire |
| `effects/special/just_star_48x48_f2_v2.png` | 48x48, 2프레임 | star | steam |
| `effects/special/lightning_dash_256x64_f3_v2.png` | 256x64, 3프레임 | bolt | lightning |
| `effects/special/kiln_border_320x320_f4_v2.png` | 320x320, 4프레임 | ring | fire |
| `effects/enemy/death_smoke_48x48_f4_v2.png` | 48x48, 4프레임 | smoke | smoke |
| `effects/enemy/explosion_128x128_f5_v2.png` | 128x128, 5프레임 | burst | enemy_red |
| `effects/enemy/impact_circle_128x128_f3_v2.png` | 128x128, 3프레임 | ring | enemy_red |
| `effects/enemy/impact_lane_128x64_f3_v2.png` | 128x64, 3프레임 | ring | enemy_red |
| `effects/enemy/impact_fan_128x128_f3_v2.png` | 128x128, 3프레임 | ring | enemy_mag |
| `effects/enemy/frost_field_160x160_f2_v2.png` | 160x160, 2프레임 | telegraph | enemy_mag/ice |
| `effects/enemy/rage_smoke_32x48_f4_v2.png` | 32x48, 4프레임 | smoke | shadow_e |
| `effects/enemy/round_large_24x24_f2_v2.png` | 24x24, 2프레임 | orb | enemy_red |
| `effects/enemy/round_small_16x16_f2_v2.png` | 16x16, 2프레임 | orb | enemy_red |
| `effects/enemy/lightning_strike_64x128_f3_v2.png` | 64x128, 3프레임 | bolt | enemy_red |
| `effects/enemy/elite_column_32x128_f3_v2.png` | 32x128, 3프레임 | pillar | khaki |
| `effects/enemy/spore_cloud_128x128_f4_v2.png` | 128x128, 4프레임 | smoke | spore |
| `effects/enemy/stain_64x64_f1_v2.png` | 64x64, 1프레임 | cracks | smoke |
| `effects/enemy/soil_crack_64x32_f2_v2.png` | 64x32, 2프레임 | cracks | soil |
| `effects/enemy/soil_particle_8x8_f1_v2.png` | 8x8, 1프레임 | orb | soil |
| `effects/enemy/mirror_silver_particle_8x8_f2_v2.png` | 8x8, 2프레임 | orb | steam |
| `effects/status/frost_particle_4x4_f2_v2.png` | 4x4, 2프레임 | orb | ice |
| `effects/status/crystal_particle_16x16_f3_v2.png` | 16x16, 3프레임 | star | ice |
| `effects/status/static_outline_64x64_f2_v2.png` | 64x64, 2프레임 | ring | lightning |
| `effects/status/corrosion_32x32_f2_v2.png` | 32x32, 2프레임 | cracks | dark |
| `effects/loot/drop_column_16x64_f3_v2.png` | 16x64, 3프레임 | pillar | gold |
| `effects/loot/exit_marker_64x64_f2_v2.png` | 64x64, 2프레임 | portal | arcane |
| `effects/loot/campfire_64x64_f4_v2.png` | 64x64, 4프레임 | flame | fire |
| `effects/ritual/failure_smoke_64x64_f5_v2.png` | 64x64, 5프레임 | smoke | smoke |
| `effects/ritual/failure_dust_48x48_f4_v2.png` | 48x48, 4프레임 | smoke | soil |
| `effects/ritual/gold_burst_256x256_f8_v2.png` | 256x256, 8프레임 | burst | gold |
| `effects/ritual/gold_aureole_256x256_f8_v2.png` | 256x256, 8프레임 | ring | gold |
| `effects/ritual/artisan_ember_32x32_f6_v2.png` | 32x32, 6프레임 | flame | fire |
| `effects/ritual/ward_burn_64x96_f6_v2.png` | 64x96, 6프레임 | flame | fire |
| `effects/ritual/success_mote_arcane_16x16_f3_v2.png` | 16x16, 3프레임 | orb | arcane |
| `effects/ritual/success_column_arcane_192x540_f6_v2.png` | 192x540, 6프레임 | pillar | arcane |
| `effects/ritual/max_column_arcane_192x540_f8_v2.png` | 192x540, 8프레임 | pillar | arcane |
| `effects/ritual/success_mote_fire_16x16_f3_v2.png` | 16x16, 3프레임 | orb | fire |
| `effects/ritual/success_column_fire_192x540_f6_v2.png` | 192x540, 6프레임 | pillar | fire |
| `effects/ritual/max_column_fire_192x540_f8_v2.png` | 192x540, 8프레임 | pillar | fire |
| `effects/ritual/success_mote_lightning_16x16_f3_v2.png` | 16x16, 3프레임 | orb | lightning |
| `effects/ritual/success_column_lightning_192x540_f6_v2.png` | 192x540, 6프레임 | pillar | lightning |
| `effects/ritual/max_column_lightning_192x540_f8_v2.png` | 192x540, 8프레임 | pillar | lightning |

### 바닥 타일 9장

| 파일 | 규격 | 내용 |
|---|---|---|
| `world/tiles/field.{day,dusk,night}_v2.png` | 512x192, 64px 칸 8x3 | 풀·흙 각 4변형, 가장자리·모서리·얼룩 칸은 원본 낮 시트의 풀/흙 경계 모양 그대로 |
| `world/tiles/plaza.{day,dusk,night}_v2.png` | 512x192, 칸 22개(나머지 투명 유지) | 다진 흙 4, 포석 6, 가장자리·모서리 |
| `world/tiles/arena.{day,dusk,night}_v2.png` | 512x64, 칸 4개(나머지 투명 유지) | 판석 4 |

색은 `world/manifest.json`의 `paletteTargets`(시간대·재질별 기준색)를 가운데 둔 8단 램프. 질감은 64px 주기로 이어지고, 변형 칸의 장식은 가장자리 8px 안쪽에만 있어 어떤 칸끼리 붙여도 이음매가 없다.

## 뺀 것과 이유

- `bloom_*`, `fan_*`, `wing_*`, `petal_*`, `spear_*`, `thunderlance_*`, `muzzle_storm_*`, `octagram_*`, `cast_seal_*`(특수기): v2(dark)가 캐릭터 고유 형태 디자인이라 절차 생성으로는 짝이 맞지 않는다.
- `charge_*`, `extinguish_*`, `outer_glyph_*`, `seal_*`, `stage_hold_*`, `ward_ribbon_*`, `break_pieces_*`(강화 의식): v2가 원소별 문양(눈꽃, 그림자 낫 등) 디자인. 같은 수준의 문양은 손그림이 낫다.
- `trace_*`, `weakness_*`, `resist_*`, `crystal_*`(원소 표식): 형태 아이콘 성격.
- `ember_lane`(가로·세로 반복 타일 규칙 확인 필요), `thorn_ground`, `toad_buff`, `stun_star`, `ice_pillar`, `rock`, 탄 모양 그림(`feather`, `pine_needle`, `shadow_needle`, `vine_seed`, `ice_shard`, `mirror_shards`): 형태 디자인이 핵심.
- `screen_crack`, `max_sky_band`, `overflow_steam_arc`, `meteor_fall/ground`, `slow_ankle_ring*`, `crystal_break`, `frozen_shell_*`: 절차 생성 가능하지만 이번 굽기에 넣지 않았다(아래 후보).
- `world/backplates`, `world/sprites`(벽, 탑, 소품, NPC, 날씨), `ui/kit`, `ui/icon`, `monsters`: 형태·손그림 중심.

## Godot로 더 만들 수 있는 후보

`slow_ankle_ring_32x8_f2`, `slow_ankle_ring_double_32x8_f2`(ring, squash), `meteor_fall_32x64_f3`(comet 세로), `meteor_ground_32x32_f4`(ring+cracks), `crystal_break_32x32_f4`(burst shard), `overflow_steam_arc_320x320_f5`(smoke 호), `screen_crack_1920x1080_f3`(cracks), `max_sky_band_1920x240_f4`(오로라 띠), `field.weather.fog/rain/puddle`, `field.moonlight`, `field.meteor.ring/sparkle`(원본 규격 확인 뒤).

## GPT 필요 목록

아래 공통 지시 뒤에 항목별 지시를 붙여 넣는다.

> 공통: 2D 탑다운 판타지 픽셀아트 게임 스프라이트, 64PPU. 투명 배경 PNG. 검은 외곽선 없이 가장자리를 한 단계 어두운 같은 색으로 처리, 왼쪽 위 광원, 흰 심지와 옅은 빛무리, "+" 모양 반짝임. 색은 원소별 8단 램프(불: #3c1a0c~#fff6dc 주황, 번개: #3a3412~#fffce8 노랑, 비전: #0e3a3a~#f2fffb 청록, 얼음: #2c4a8c~#fafdfe 파랑, 그림자: #2e1248~#f6eeff 보라). 플레이어 쪽 그림에는 진한 빨강·자홍(색상각 330~15도, 채도·명도 70% 이상)을 쓰지 않는다. 프레임은 가로 한 줄, 왼쪽에서 오른쪽, 지정 크기 그대로.

크기 표기 `WxH_fN` = 한 프레임 WxH, N프레임 가로 한 줄. 프레임 없는 이름은 단일 그림(크기 그대로). `{a,f,i,l}` = arcane·fire·ice·lightning 각각 한 장.

### effects/special (비기, 플레이어 쪽)
- `bloom_{a,f,i,l}_192x192_f6`: 원소색 초승달 베기 궤적이 크게 휘둘러졌다가 흩어지며 사라지는 6프레임(dark v2와 같은 구도).
- `fan_{a,f,i,l}_192x128_f4`: 시전자(왼쪽)에서 오른쪽으로 부채꼴로 퍼지는 원소 파동 호, 퍼질수록 옅어짐.
- `wing_{a,f,i,l}_96x64_f4`: 등 뒤에 펼쳐지는 원소 날개 한 쌍, 펼침→정지→흩어짐.
- `petal_{a,f,i,l}_32x48_f3`: 떨어지며 회전하는 원소 꽃잎 하나.
- `spear_shaft_{a,f,i,l}_64x32_f3`, `spear_tip_{a,f,i,l}_64x32_f3`: 오른쪽을 향한 마법 창의 자루(가로 반복 가능)와 창끝, 일렁임 3프레임.
- `thunderlance_shaft_{a,f,i,l}_64x48_f4`, `thunderlance_tip_{a,f,i,l}_64x48_f4`: 오른쪽을 향한 번개 창의 몸통(가로 반복)과 끝, 전류 일렁임 4프레임.
- `muzzle_storm_{a,f,i,l}_96x64_f4`: 오른쪽으로 쏟아지는 연사 섬광 폭풍.
- `octagram_{a,f,i,l}_128x128_f4`: 바닥에 그려지는 팔각별 마법진, 그려짐→빛남→사라짐.
- `cast_seal_01~12_96x96_f4`, `cast_seal_15_96x96_f4`: 비기별 시전 인장(원형 문양), 13·14번 v2와 같은 양식으로 비기마다 다른 문양.
- `break_armor_48x48_f4`(갑옷 조각이 깨져 튀는 4프레임), `shadow_arm_32x16_f2`, `shadow_fist_64x64_f3`, `shadow_hand_32x32_f4`, `shadow_pool_128x128_f4`(그림자 팔·주먹·손·웅덩이, 보라 램프), `technique_name_ribbon_192x32_f2`, `technique_name_ribbon_large_320x48_f2`(비기 이름 띠, 글자 없음).

### effects/ritual (강화 의식 화면)
- `charge_{a,f,l}_96x96_f6`: 원소 문양 마법진이 점점 커지며 충전되는 6프레임(ice v2의 눈꽃 문양처럼 원소별 상징: 불꽃, 번개, 비전 룬).
- `extinguish_{a,f,l}_96x96_f5`, `outer_glyph_{a,f,l}_480x480_f4`, `seal_{a,f,l}_240x240_f1`, `seal_base_240x240_f1`, `stage_hold_{a,f,l}_128x96_f4`, `ward_ribbon_{a,f,l}_128x128_f6`: 각각 dark/ice v2와 같은 구도로 원소만 바꿔서.
- `break_pieces_{a,f,l}_64x64_f18`: 지팡이 결정이 깨져 조각이 흩어지는 18프레임(v2와 같은 궤적).
- `artisan_slot_24x24_f2`, `offering_slot_56x56_f2`, `slot_open_64x64_f6`(마름모 칸이 열리는 6프레임), `ward_attached_48x80_f1`, `ward_slot_48x80_f2`(부적과 칸), `trace_fragment_16x16_f3`.
- `mutation_card_480x272_f1`, `mutation_vine_192x256_f24`(덩굴이 자라 감기는 24프레임), `result_card_560x320_f2`, `title_ribbon_{gold,normal}_720x96_f4`, `plaza_notice_{gold,normal}_960x80_f3`, `news_line_{gold,normal}_960x40_f1`: 글자 없는 틀만.
- `stage_digits_{gold,muted,tint}_48x64_f11`: 숫자 0~9와 "+" 11칸.
- `screen_crack_1920x1080_f3`, `max_sky_band_1920x240_f4`: Godot로도 가능(후보).

### effects/player
- `crystal_{a,f,l}_24x24_f2`: 원소 결정 보석 아이콘, 반짝임 2프레임(dark/ice v2와 같은 꼴).
- `resist_{a,f,i,l}_32x16_f3`(저항 방패 문양), `weakness_{a,f,i,l}_64x64_f4`(약점 표식이 맺히는 4프레임), `trace_{a,f,i,l}_64x32_f2`(원소 흔적): dark v2와 같은 구도.
- `critical_chevron_6x8_f1`, `heat_lock_x_24x24_f2`, `ice_lance_12x28_f2`(위를 향한 얼음 창), `piercing_24x12_f2`(관통 화살촉), `overflow_warning_16x16_f1`(느낌표 경고, 위험색 금지 → 주황·노랑), `overflow_steam_arc_320x320_f5`(Godot 후보).

### effects/enemy (적: 빨강·자홍 사용 가능)
- 탄: `feather_24x12_f2`, `pine_needle_24x8_f1`, `shadow_needle_24x12_f2`, `vine_seed_16x16_f2`, `seed_shadow_16x8_f1`, `ice_shard_16x16_f1`, `mirror_shards_16x16_f2` — 모두 오른쪽을 향한 탄, 빨간 테두리로 적 탄임을 표시.
- `ice_pillar_64x96_f6`(솟음 2, 서 있음 1, 깨짐 3), `rock_64x64_f5`(떨어짐 2, 놓임 1, 깨짐 2), `thorn_ground_96x96_f2`(가시밭 바닥 타일, 사방 이어짐), `ember_lane_64x64_f3`(불씨 띠 바닥 타일, 사방 이어짐), `toad_buff_64x32_f3`(발밑 버프 고리), `stun_star_32x16_f3`(머리 위 도는 별), `rage_slot_16x16_f2`, `final_rage_ribbon_480x64_f3`.

### effects/status, effects/loot
- `armor_bounce_32x32_f3`, `corrosion_tail_8x16_f2`, `critical_ring_chevrons_8x8_f2`, `crystal_break_32x32_f4`, `crystal_{cracked,debt,empty}_24x24_f1`, `frost_mark_16x8_f1`, `frozen_shell_64_64x64_f1`, `frozen_shell_96_96x96_f1`(적을 감싼 얼음 껍질), `health_{full,empty}_32x32_f1`, `health_break_32x32_f4`(하트), `combo_digits_24x32_f10`(0~9), `damage_glyphs_16x24_f13`(0~9와 기호 3), `meteor_fall_32x64_f3`, `meteor_ground_32x32_f4`, `slow_ankle_ring_32x8_f2`, `slow_ankle_ring_double_32x8_f2`.
- `block_{common,rare,heroic}_24x24_f2`(등급 테두리 블록 조각), `coin_16x16_f4`(도는 금화), `crow_pack_64x64_f4`, `health_shard_16x16_f2`, `material_pack_24x24_f2`, `season_drop_32x32_f4`.

### monsters (칸 배치는 `monsters/manifest.json`의 프레임 사각형 그대로)
- 일반 13: `enemy.bat_dusk`(320x64), `enemy.boar_tusk`(576x128), `enemy.bug_lantern`(256x64), `enemy.golem_menhir`(576x192), `enemy.hog_needle`(384x128), `enemy.shaman_cap`(384x128), `enemy.slime_moss`(256x64), `enemy.spore_seedling`(96x32), `enemy.toad_drum`(384x128), `enemy.vine_thorn`(384x192), `enemy.weasel_pot`(384x128), `enemy.wisp_stray`(384x128), `enemy.wolf_bramble`(384x128).
- 정예 덧그림 15: `elite.*.overlay`(64x32 또는 96x32), `elite.bat_dusk.dive`(64x64), `elite.horn`(16x16), `elite.wisp_stray.twin`(192x64).
- 보스 6 × (몸 시트, `.portrait` 96x96, `.hud` 32x32): `boss.frost_goat`(768x640), `boss.kiln_boar`(1152x512), `boss.mirror_phantom`(384x384), `boss.night_serpent`(576x288, `.segment` 256x64, `.tail` 192x64), `boss.spore_queen`(768x640), `boss.storm_owl`(1152x384, `.ground` 192x128).

### world
- `world/backplates/{arena,field,plaza}.png`(1920x1080 배경판), `world/sprites/*` 94장(벽·모서리, 탑, 시설, 소품, 관중, NPC, 날씨, 달빛). 벽·모서리는 같은 장소끼리 이어져야 한다. 날씨(fog, rain, puddle)와 달빛·유성(`field.meteor.*`, `field.moonlight`)은 Godot 후보.

### ui/kit (145장), ui/icon (284장)
- kit: 판·오목판·종이(k-01~03 48x48, 9-slice 테두리 12px), 머리띠·조작띠(k-04~06), 버튼 4종 × normal/hover/pressed/disabled(k-20~24), 탭, 토글, 체크, 라디오, 슬라이더, 드롭다운, 입력칸, 스크롤, 닫기, 진행 막대, 페이지, 선택 모서리(f2), 키캡·마우스·방향 입력 그림(k-50~55), 아이템·지팡이·코어·확장 칸(k-60~64), 회로 연결(k-65, k-70), 등급 테두리(k-66~68), 변이 칸(k-80~81). 같은 번호의 상태 그림은 같은 틀에서 색·눌림만 다르게.
- icon: 블록(i-b1 48, i-b2 24 각 14종, 같은 그림 두 크기), 판(i-b3), 재화(i-c1~3: 16/24/48 같은 그림), 원소(i-e1~4), 메뉴·조작·상태(i-f1~f13), 지팡이 단계(i-g1 80x80 5단계, 단계별 실루엣이 달라야 함), 리그(i-l1~l9), 재료·꾸미기(i-m1~m9), 능력치·패시브(i-s1~s5), 비기(i-t1 48, i-t2 24 각 13종), 기타(i-t3, i-t4). 이미 v2가 있는 i-b1/b2 corrosion·shadow_orb, i-t1/t2 copy_circle·shadow_hand, i-e* shadow, i-s2 shadow_contract와 같은 양식으로.

## 캐릭터 공격 유형·비기 강화 시트 (styles)

`styles.py`(목록) → Godot(`cone`, `plasma`, `rune`, `swirl`, `debris`, `patch` 셰이더 추가) → `post.py` → `compose.py`(책, 얼음 창·고드름·가시, 거대한 손, 책장 소용돌이: 도형/기존 GPT 그림 조합) → `manifest.py`.
결과는 `Assets/Resources/Art/effects/styles/<char.N|common>/<키>_<W>x<H>_f<N>.png`와 같은 폴더 `manifest.json`(묶음 `styles.<폴더>`, 효과 manifest 형식 + `pivot`(프레임 왼쪽 위 기준 px), `clips`, `use`, notes.reuse = 재사용할 기존 그림).
미리보기 `preview-styles.png`(위: 어두운 바탕, 아래: 풀밭색 바탕). `--only` 로 일부만 구울 때 `page_vortex`는 `vortex_base` 부품이 같이 걸리게(`--only styles` 또는 `--only char.3`) 한다.
