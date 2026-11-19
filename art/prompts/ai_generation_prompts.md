# AI 이미지 생성 프롬프트 (ChatGPT / 이미지 생성기용)

사용법: **[공통 스타일]을 먼저 붙이고, 그 뒤에 에셋별 프롬프트를 이어 붙인다.**
외형·색은 `docs/monsters.md` 1.4절과 각 종 설명을 따른다. 각 몬스터의 "Avoid" 줄은 다른 게임 캐릭터와 닮지 않게 하는 규칙이라 꼭 함께 넣는다.

팁
- 첫 장이 마음에 들면, 이후 요청마다 그 이미지를 같이 올리고 "Match this exact art style" 로 시작한다.
- 몬스터는 먼저 **기본 자세 1장**을 확정하고, 그 이미지를 기준으로 동작 프레임을 "same character, same size, same palette, only pose changes" 로 요청한다.
- 결과물은 크게(512~1024) 받아 두면 축소·배경 제거는 내가 처리한다. 파일명은 각 항목의 `id` 로 저장.

---

## A. 공통 스타일

### A-1. 카드 아이콘 공통
```
Game UI skill icon, high-detail pixel art, 128x128 canvas, crisp hard pixel edges, no blur.
Single centered symbol inside a dark navy rounded-square plate (#1b1d33) with a 2px darker outline
and subtle inner bevel highlight on top-left. Strong rim light, 3–4 tone shading per color,
small specular sparkles, soft inner glow around the symbol. Dark fantasy wizard roguelike style,
readable at 48px. Transparent background outside the plate. No text, no letters.
```

### A-2. 몬스터 공통
```
2D top-down action roguelike enemy sprite, high-detail pixel art, chunky cute-but-eerie proportions
(about 2 heads tall), side 3/4 view facing right, full body visible, centered.
1px dark navy outline (#1B2440, never pure black), 3–4 tone shading per material, light from top-left,
small rim highlight. Muted, low-saturation body colors (saturation under 50%), bright enough to read
on dark green, dusk purple and night navy ground. Eyes only white (#FFFFFF) or pale yellow (#FFF2A0).
No teal/cyan, no magenta, no saturated orange on the body. Transparent background, no ground shadow,
no text. Folk-tale field creature that was changed by an old wizard's discarded experiments.
```

---

## B. 카드 아이콘 16종 (A-1 뒤에 붙이기)

| id | 이름 | 프롬프트 |
|---|---|---|
| magic_bolt | 마력탄 | A glowing arcane magic bolt: teal-cyan crystalline arrowhead with a short sparkling energy trail, tiny diamond rune at its core. |
| fireball | 화염구 | A roaring fireball: bright yellow-white core, orange and crimson layered flames curling backward, small embers. |
| ice_spike | 얼음 송곳 | A sharp ice spike shard flying diagonally: pale cyan translucent faceted crystal, frosty mist tail, snow sparkles. |
| chain_lightning | 연쇄 번개 | A forked lightning bolt jumping between three small glowing nodes, electric yellow-white core, blue-violet glow. |
| shadow_orb | 그림자 구체 | A dark shadow orb: deep purple-black sphere with swirling violet smoke tendrils and a faint inner light. |
| boomerang | 부메랑 | A curved arcane boomerang blade mid-spin, silver edge with magenta rune inlay, circular return motion arc. |
| pierce | 관통 +2 | A mint-green magic arrow piercing straight through two hollow ring targets in a diagonal line, motion streaks. |
| ricochet | 벽 튕김 | A white energy arrow bouncing off a vertical grey stone wall at a sharp V angle, impact spark at the bounce. |
| frost | 서리 | A six-pointed frost snowflake, icy light-blue with glowing white core and tiny ice fragments around it. |
| size_double | 탄환 크기 2배 | A small orange ring beside a big glowing orange ring with bright yellow core, outward expansion arrows. |
| split_two | 2갈래 분열 | One glowing bolt splitting into two diverging bolts in a Y shape, cyan energy, burst spark at the fork. |
| split_three | 3갈래 분열 | One glowing bolt splitting into three fanning bolts, golden-cyan energy, burst spark at the split. |
| homing | 유도 | A magic missile following a curved dotted path toward a small crosshair target, green-cyan trail. |
| on_hit | 적중 시 발동 | An impact starburst where a bolt hits, releasing a shockwave ring and a small secondary rune lighting up. |
| heat_triple | 과열 3배 | Three stacked flame tongues rising from a glowing heat gauge, red-orange gradient, wavy heat lines. |
| corrode | 부식 | A toxic acid droplet dissolving a metal plate, sickly green-yellow bubbling liquid, corroded holes, drips. |

### 원소 배지 (카드 오른쪽 위, 32x32)
```
Tiny element badge icon, pixel art, 32x32, bold silhouette, 2px dark outline, bright glow,
transparent background, readable at 12px. No text.
```
- badge_arcane: teal-green rhombus gem with a sparkle
- badge_fire: small orange-red flame
- badge_ice: pale blue ice crystal shard
- badge_lightning: yellow zigzag bolt
- badge_dark: purple crescent shadow drop

---

## C. 일반 적 12종 (A-2 뒤에 붙이기)

### enemy.slime_moss — 이끼 슬라임
```
A flat wet mound of shaggy moss hugging a half-exposed grey stone, crawling on the ground.
Spiky moss tufts on top, a single thin horizontal pale-yellow eye slit in the gap of the stone, wet trail.
Palette: moss #9CCB6A, moss shadow #5E8F4A, stone #9AA39A, stone shadow #5C6670.
Avoid: water-drop shaped slime, a top nub or sprout, two round dot eyes.
```

### enemy.wolf_bramble — 덤불 늑대
```
A low-crouching field wolf with five spiky bramble thorns growing along its spine, a leaf at the tail tip,
small white dot eyes, clearly solid body.
Palette: fur #A89C8A, fur shadow #6A6258, bramble #98BF6E, thorn tips #E6F2C0.
Avoid: translucent shadow beast, red eyes.
```

### enemy.golem_menhir — 선돌지기
```
A standing-stone guardian: tombstone-shaped stone body narrower at the top, a wide stone slab on the front
carved with a spiral pattern glowing pale yellow from the cracks, moss and small grass on the shoulders,
two short stubby stone arms.
Palette: rock #A9AEB8, shadow #5E6678, moss #98BF6E, rune glow #FFF2A0.
Avoid: brick golem with one eye, arms longer than the body. Keep the tombstone silhouette.
```

### enemy.shaman_cap — 갓버섯 술사
```
A scarecrow overgrown by a mushroom: wide mushroom cap, thin stalk body wearing rags, stick arms
poking out sideways, holding a crooked twig staff. NO face under the cap — instead a wobbly fake face
is drawn with charcoal on top of the cap.
Palette: cap #CDB27E, cap shadow #8A6F48, stalk #EDE3C8, twig #7A6248, charcoal face #3A2E2A.
Avoid: glowing eyes in the shadow under the cap.
```

### enemy.bug_lantern — 꽈리 벌레
```
A small six-legged beetle carrying on its back a thin papery lantern-plant (physalis) pouch that is
bigger than its body.
Palette: shell #8E9E7A, shell shadow #56664E, pouch #E9D98A, pouch shadow #B8A560, legs #3A463E.
Avoid: glowing orange fire sac — the pouch is dull pale yellow.
```

### enemy.hog_needle — 솔침 고슴도치
```
A slow round hedgehog whose whole back is long pine-needle spines, long snout, white dot eyes.
Palette: spines #93B87F, spine shadow #587A56, spine tips #E6F2C0, belly #D9C7A3, nose #3A2E2A.
Avoid: blue running hedgehog. Draw it round and slow.
```

### enemy.weasel_pot — 화분 족제비
```
A long slender weasel standing up, wearing a broken clay flower pot on its head like a helmet,
a single withered grass sprout still stuck in the pot. Small front paws, long body.
Palette: fur #C09A78, fur shadow #7A5A44, pot #C9B8A0, pot shadow #8A7A66, grass #98BF6E.
Avoid: wide shovel paws, closed eyes, chubby mole body.
```

### enemy.bat_dusk — 해거름 박쥐
```
A small bat made from an old umbrella's ribs: big ears, tiny body, wide wing membranes with three white
star-like dots, white dot eyes, light blue-grey body.
Palette: body #8496C8, membrane #A9B8E0, shadow #56648C, dots #FFFFFF.
Avoid: black body, red eyes.
```

### enemy.wisp_stray — 도깨비불
```
A floating pale-yellow candle flame spirit, with a pebble-like blue-grey core inside and two white dot eyes,
flame tail flowing downward, hovering above the ground.
Palette: flame #FFF0A8, flame shadow #D8C070, core #7F8FA8, core shadow #4E5A72.
Avoid: teal or cyan flame, tiny fireball size.
```

### enemy.vine_thorn — 가시덩굴 (64x96, 세로로 김)
```
A thick rooted climbing-rose vine stalk, one closed flower bud on top, two thorny vine arms to the sides,
bright light-green stem with yellow thorns. Rooted, does not move.
Palette: stem #98BF6E, stem shadow #5E8F4A, thorns #F2E38C, bud #EDE3C8.
Avoid: carnivorous plant with toothed mouth. The bud has no mouth or teeth.
```

### enemy.boar_tusk — 엄니 멧돼지 (96x64, 가로로 김)
```
A wedge-shaped wild boar: high shoulders, low hips, two upward-curved tusks, short dark mane on the back,
pale yellow dot eyes, just a plump field animal.
Palette: fur #B08A6E, shadow #6E5444, mane #5E5A66, tusks #F0E6C8.
Avoid: tentacles or curses sprouting from the body, giant tusks.
```

### enemy.toad_drum — 북두꺼비
```
A toad with a round taut belly like a drum skin, flat bumps on the back, horizontal pale-yellow eyes,
a throat sac that can puff up.
Palette: back #A9B86A, back shadow #6A7A44, belly #E6D9A8, bumps #7A8A4E, throat sac #F4F1E6.
Avoid: crowned frog, plants growing on its back.
```

### enemy.spore_seedling — 포자 새싹 (포자 여왕의 졸개)
```
A tiny mushroom sprout minion with a small tan cap and short root legs, no face, same style as the spore queen.
Palette: cap #CDB27E, stalk #EDE3C8, roots #8A6F48.
```

---

## D. 엘리트 장식 (기존 몸 위에 얹는 레이어, 투명 배경으로 장식만)

같은 몬스터의 확정 이미지를 같이 올리고 "Add only this decoration, same style, output decoration alone on transparent background" 로 요청.

| id | 바탕 | 추가 장식 |
|---|---|---|
| elite.slime_moss.overlay | 이끼 슬라임 | a three-tier stacked stone cairn on its back |
| elite.golem_menhir.overlay | 선돌지기 | a flat dolmen capstone resting on its head |
| elite.vine_thorn.overlay | 가시덩굴 | the bud fully bloomed into a white flower |
| elite.toad_drum.overlay | 북두꺼비 | two tiny toads sitting on its back |
| elite.horn | 공통 | a small pale gold horn emblem marking elites |

(나머지 엘리트 오버레이 — wolf/bug/hog/weasel/bat/wisp/boar/shaman — 는 문서 3.1절의 장식이 정해지면 같은 방식으로 추가)

---

## E. 보스 6종 (A-2 뒤에 붙이기)

각 보스마다 3장: 전투 스프라이트(id), 초상화(`.portrait`, 96x96 흉상, 어두운 원형 배경), 체력바 아이콘(`.hud`, 32x32 머리 실루엣).
초상화 추가 문구: `Bust portrait, head and shoulders, dramatic top-left lighting, dark navy circular backdrop.`
HUD 추가 문구: `Tiny 32x32 head icon, bold simple silhouette, readable at 16px.`

### boss.kiln_boar — 숯가마 멧돼지왕 "가마등" (192x128)
```
Huge boss: a boar wearing an entire round charcoal kiln as its body shell. A single furnace opening on its back
leaks pale-yellow light. A blacksmith's tongs and two iron skewers stuck diagonally into its back.
Palette: fur #A68A78, shadow #6A5448, kiln wall #8E8A96, kiln shadow #5A5666, furnace light #FFE9A8,
tools #C8D0E0, smoke #DDE3EA.
Avoid: split back, giant tusks, black ooze from the body, orange firelight.
```

### boss.mirror_phantom — 스승의 허상 (64x96)
```
A walking old silver-grey robe with an oval full-length mirror where the face should be, no hat,
no feet — the robe hem dissolves into mist. Holds a plain silver monochrome wizard staff.
Palette: robe #AEB6C2, shadow #5E6678, mirror #F4F1E6, mist #DDE3EA.
Avoid: pointed hat, shadowed face wizard.
```

### boss.spore_queen — 포자 여왕 "천 갓" (128x160)
```
A giant mushroom of many stacked caps, no face. On the top cap small mushrooms sprout where eyes, nose and
mouth would be, forming a fake face. Walks on a bundle of roots.
Palette: cap #CDB27E, cap pattern #F2E6B8, stalk #EDE3C8, roots #8A6F48.
Avoid: many glowing eyes under the cap, red cap with white dots.
```

### boss.storm_owl — 폭풍 수리부엉이 "밤눈" (192x128 날개 편 모습, ground: 96x128 착지 모습)
```
A great horned owl with wings spread, round face and big glowing pale-yellow eyes, feather tips split like
lightning forks (white), cloud patterns inside the wings.
Palette: feathers #8E9CC4, shadow #56648C, feather tips #F4F1E6, belly #C8D0E0, eyes #FFF2A0, beak #3A2E2A.
Avoid: lemon-yellow glowing thunderbird, sparks flying off the body.
```
착지 버전(boss.storm_owl.ground): `same owl, wings folded, standing on the ground, 96x128`.

### boss.frost_goat — 서리 산양 "흰뿔" (128x160)
```
A mountain goat with two big horns curving backward, a row of icicles hanging under the horns like eaves,
frost on the beard, snowy mane on the neck, short thick legs, white breath vapor, white eyes.
Palette: fur #D8E4EC, shadow #7C93A8, horns #8FA9C4, icicles #FFFFFF, hooves #56648C.
Avoid: branching antlers, white deer god, human-like face, bright sky-blue ice.
```

### boss.night_serpent — 별밤 구렁이 "밤삼킴"
```
Head (96x96): a large serpent head with bright night-sky blue scales, constellation-like white dots,
a white crescent-moon crest on the head, vertical pale-yellow slit eyes.
Palette: scales #7386C6, shadow #4A5890, dots #FFFFFF, crest #F4F1E6, belly #A9B8E0.
Avoid: black snake with red eyes, glowing blue-violet snake.
```
- boss.night_serpent.segment: `one round body segment (64x64), same scales and white star dots, seamless to chain`
- boss.night_serpent.tail: `tapered tail tip (64x64), same scales`

---

## F. 동작 프레임 요청 예시

기본 자세 이미지를 올린 뒤:
```
Same character, identical size, palette, outline and style. Make a horizontal sprite strip of 4 frames,
equal spacing, transparent background: <동작 설명>.
```
프레임 내용은 `docs/monsters.md` 각 종의 "프레임" 항목을 그대로 영어로 옮겨 쓴다.
예) 덤불 늑대 이동 4: `four-legged run cycle — front legs reaching, legs gathered, hind legs kicking, airborne.`
공격 예고 프레임에는 `only the weapon part (thorns/tusks/bud/pouch) turns magenta #FF2E88` 를 붙인다.
