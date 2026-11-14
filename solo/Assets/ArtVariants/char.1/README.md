# 제자 1 그래픽 에셋

제자 1(`char.1`)의 선택 일러스트를 기준으로 한 Unity용 그래픽 세트입니다. 각 PNG 옆의 `.meta`에 슬라이스와 피벗이 있습니다. 표의 프레임 순서는 PNG 위에서 아래, 각 행 왼쪽에서 오른쪽입니다.

| 용도 | 파일 | 구성 |
|---|---|---|
| 캐릭터 선택 | `selection_192x192.png` | 전신 일러스트 |
| 표정 초상 | `portraits_128x128_f4.png` | 기본, 기쁨, 결의, 당황 |
| 실루엣 | `silhouette_32x32.png` | 정면 서기 |
| 기본 전투 | `combat_128x128_f12.png` | 행은 아래, 오른쪽, 위. 열은 대기, 이동 A/B, 시전 |
| 보행과 자세 보행 | `locomotion_128x128_f12.png` | 방향별 이동 중간 A/B, 자세 이동 A/B |
| 전투 반응 | `battle_reactions_128x128_f12.png` | 방향별 자세 준비, 피격, 발사 반동, 강한 피격 |
| 고유 비기 | `signature_cast_128x128_f12.png` | 방향별 준비, 모으기, 발동, 회복 |
| 쓰러짐 | `knockout_128x128_f8.png` | 반동, 휘청, 무릎 꿇기, 넘어짐, 쓰러짐, 웅크림, 기상, 앉기 |
| 야영과 강화 반응 | `camp_upgrade_128x128_f12.png` | 앉기, 인사, 박수, 도발 / 절, 춤, 손 들기, 좌절 / 강화 대기, 충전, 성공, 실패 |
| 과열 몸통 | `body_lidless_128x128_f12.png` | 뚜껑을 분리한 방향별 기본 포즈. 뚜껑 스프라이트와 합성 |
| 과열 부품 | `steam_32x32_f12.png`, `lid_48x32_f4.png` | 김 3단계 × 4프레임, 흔들리는 뚜껑 4프레임 |
| 야생 물 | `wild_stain_64x64_f15.png` | 그을음, 풀 옷자락, 금속 덧댐, 깃털, 불티. 부품별 아래·오른쪽·위 |
| 불가마 | `kiln_border_320x320_f4.png`, `kiln_ember_16x16_f2.png`, `kiln_cast_seal_128x128_f4.png` | 테두리, 불티, 시전 문양 |
| 끓어넘침 | `overflow_320x320_f5.png` | 확대되며 흩어지는 증기 5프레임 |
| HUD | `hud_icons_64x64_f4.png` | 불가마, 끓어넘침, 과열 잠금, 불가마 적용 표시 |
| 지팡이 상세와 목록 | `wand_inspection_192x192_f14.png`, `wand_icons_64x64_f14.png` | +0부터 +13 순서 |
| 손 지팡이 | `wand_hand_32x64_f84.png` | 열은 +0부터 +13. 행은 아래 들기·조준, 오른쪽 들기·조준, 위 들기·조준 |
| 대장간 | `forge_backdrop_1024x576.png`, `forge_128x128_f8.png`, `forge_sparks_64x64_f4.png` | 배경, 망치 동작, 불티, 석탄, 구리 고리, 강화 흔적 |

128px 몸 시트의 발 피벗은 셀 왼쪽 위에서 `(60,120)`입니다. 손 지팡이 피벗은 `(16,48)`입니다. 왼쪽 방향에는 오른쪽 스프라이트를 좌우 반전해 사용합니다.

현재 게임은 `Resources/Art`의 리소스와 매니페스트를 읽습니다. 이 세트를 런타임에 쓰려면 해당 매니페스트에 새 프레임 좌표, 재생 순서, 손·머리 부착점을 연결하고 실제 화면에서 합성을 확인해야 합니다. 이 세트는 기존 147프레임 본체 시트와 프레임 구성이 다릅니다.

## 생성 프롬프트

내장 ImageGen에 기존 선택 일러스트 또는 기본 전투 시트를 참조로 제공했습니다. PNG 크롭, 리샘플링과 시트 배치는 별도로 적용했습니다.

### portraits

2x2 square game UI portrait atlas, exactly four centered bust icons, matching attached char.1: copper hammered kettle hat with spout, deep teal side-swept hair, amber eyes and teal coat with cream seams. Rows left to right: confident neutral, delighted laughing; determined gritted teeth, embarrassed upset. Preserve same face, hat shape, scale and eye height; only expression and slight head tilt change. Warm highlights and finely textured pixel clusters, no wand.  Produce production-quality rich 128px-style pixel art, with crisp intentional pixel clusters, readable silhouettes and material detail. Copper orange, muted teal, cream and warm gold, avoid saturated danger red and magenta. Precisely regimented uniform grid, no labels, no dividers, no text, no ground shadow, hard true alpha transparent backdrop. Leave generous clear margin on all four sides of every cell; absolutely no overlap.

### battle_reactions

Premium detailed top-down pixel art, match char.1 in the attached existing atlas. 4 columns, 3 rows, exact grid and liberal transparent gaps. Each sprite is a full body with copper hammered kettle hat, teal hair, dark teal cream-piped coat and oven motif on the tan apron. Row 1 down/front, row 2 right profile, row 3 up/back. Within each row: ready Aim low stance with two visible open palms; flinch in pain shoulders backwards with two visible empty fists; recoil after firing with lead open palm pulled halfway back; deep wound recoil with empty palms. Every hand must show skin or gloves and be entirely empty. Absolutely no rods, wands, weapons, sticks, held objects, VFX, symbols, streaks or shadows. Preserve visual richness, camera and scale. True transparent background.

### camp_upgrade

Exactly 4 columns by 3 rows of full-body, down/front facing char.1 in the attached finished character model, same copper hat details, teal coat and tan furnace apron. In order row 1: sit down cross-legged, greeting wave, clap hands, cheeky taunt; row 2: deep bow, twirling dance, raise empty casting hand high, head lowered frustrated; row 3: anxious but composed hold before upgrade, concentrating charge crouch, triumphant clenched fists, dismayed slump. Strong unique pose silhouettes, expressive eyes, unchanged design. No wand or VFX.  Produce production-quality rich 128px-style pixel art, with crisp intentional pixel clusters, readable silhouettes and material detail. Copper orange, muted teal, cream and warm gold, avoid saturated danger red and magenta. Precisely regimented uniform grid, no labels, no dividers, no text, no ground shadow, hard true alpha transparent backdrop. Leave generous clear margin on all four sides of every cell; absolutely no overlap.

### knockout

Exactly 4 columns by 2 rows, eight distinct FULL BODY animation poses for char.1 (use attached visual model) at same top-down camera and scale. Row 1: frontal struck full recoil, knees buckle with hat tilted, one knee and palm at ground, sideways collapsing. Row 2: fully down seen at the same camera, curled on ground, wobbling trying to rise, stable seated resting pose. Preserve specific hammered copper kettle hat, teal hair and coat, brass closures and furnace apron. Each cell has uniform body scale, isolated character only; no impact streaks, effects, wand or floor.  Produce production-quality rich 128px-style pixel art, with crisp intentional pixel clusters, readable silhouettes and material detail. Copper orange, muted teal, cream and warm gold, avoid saturated danger red and magenta. Precisely regimented uniform grid, no labels, no dividers, no text, no ground shadow, hard true alpha transparent backdrop. Leave generous clear margin on all four sides of every cell; absolutely no overlap.

### wand_stages

Create the fourteen stages of one fire apprentice wand family, isolated item atlas for a fantasy action RPG. Exactly 7 columns x 2 rows, one complete wand per cell. Reference character's dark iron bent fireplace poker wand is the baseline. Row 1: stages 0,1,2,3,4,5,6, starting from visibly plain dark iron hook; add small warm copper collars stages 1–3, bronze warm head and copper handle reinforcement stage 4–6. Row 2 stages 7,8,9,10,11,12,13: increasingly intricate hammered bronze head, furnace-core perforations, amber enamel and bright warm-gold trim, finally a small stylized furnace crown enclosing glowing ember at stage 13. Every object is upright, hook/head at top and handle at bottom, same single-poker family without totally changing proportions. Uniform graphic extent and grip alignment, around 60% height of cell with generous padding, sensible object evolution, 14 separate items. No environment or smears.  Produce production-quality rich 128px-style pixel art, with crisp intentional pixel clusters, readable silhouettes and material detail. Copper orange, muted teal, cream and warm gold, avoid saturated danger red and magenta. Precisely regimented uniform grid, no labels, no dividers, no text, no ground shadow, hard true alpha transparent backdrop. Leave generous clear margin on all four sides of every cell; absolutely no overlap.

### heat_parts

Exactly 4 columns × 4 rows, 16 separate top-down pixel-art game overlays for this copper kettle hat fire apprentice. No character. Rows 1–3 each show four animation phases of distinct isolated offwhite steam wisps rising from a kettle spout, row 1 thin calm translucent wisps, row 2 clearly thicker vigorous wisps, row 3 very thick billowing wisps; no smoke behind. Fourth row shows four phases of the same isolated copper kettle lid with knob as in reference, wobbling and tilting during overheat, centered at same baseline, lid only. High-detail pixels, clean alpha.  Produce production-quality rich 128px-style pixel art, with crisp intentional pixel clusters, readable silhouettes and material detail. Copper orange, muted teal, cream and warm gold, avoid saturated danger red and magenta. Precisely regimented uniform grid, no labels, no dividers, no text, no ground shadow, hard true alpha transparent backdrop. Leave generous clear margin on all four sides of every cell; absolutely no overlap.

### wild_stain

Exactly 3 columns by 5 rows of isolated pixel-art gear overlays for char.1, 15 tiles. Columns are facing down, right, up (show the same accessory in each row from the three directions). Row 1 tiny soot smudges as gentle charcoal powder texture; row 2 stitched ragged mossy grass hem strip in muted olive; row 3 hammered bronze repair patches with two rivets; row 4 single ochre feather for same copper kettle lid; row 5 small spout ember wisps curving outward. Assets isolated on fully transparent alpha, no character, no complete copper hat. Match the reference pixel color material style.  Produce production-quality rich 128px-style pixel art, with crisp intentional pixel clusters, readable silhouettes and material detail. Copper orange, muted teal, cream and warm gold, avoid saturated danger red and magenta. Precisely regimented uniform grid, no labels, no dividers, no text, no ground shadow, hard true alpha transparent backdrop. Leave generous clear margin on all four sides of every cell; absolutely no overlap.

### kiln

Exactly 4 columns x 2 rows 8 isolated game FX sprites. Row 1: four phases of ground kiln area effect, a top-down CIRCULAR perimeter (circle as seen orthogonally from above), amber dashed outline with thick rim of individual orange terracotta brick segments, empty transparent center with a few faint golden embers hugging the rim. Same size and center per cell; use brick glow to differentiate phases. No white outline or red. Row 2 first 2 cells: two tiny gold ember flicker stages; row 2 last 2 cells: two phases of a copper amber casting seal with a stylized kettle and small sparse angular runic markings, transparent center except for symbol, no readable words. Clean contrast, professional rich pixel art.  Produce production-quality rich 128px-style pixel art, with crisp intentional pixel clusters, readable silhouettes and material detail. Copper orange, muted teal, cream and warm gold, avoid saturated danger red and magenta. Precisely regimented uniform grid, no labels, no dividers, no text, no ground shadow, hard true alpha transparent backdrop. Leave generous clear margin on all four sides of every cell; absolutely no overlap.

### overflow

Exactly a horizontal row of 5 equally spaced isolated sprite cells for a fire apprentice steam burst animation shown orthogonally top-down. Each phase shows six separate broken offwhite steam arcs edged in orange, surrounding an entirely transparent center. Never a closed ring. Frame 1 radius small and dense, frame 2 broader, frame 3 broad burst with six clearly disconnected curved cloud pieces, frame 4 expands and thins, frame 5 the faint largest ring dissipation. All rings individually centered, no lighting outside the wisps, no characters. Crisp pixel clusters, subtle layered steam highlights.  Produce production-quality rich 128px-style pixel art, with crisp intentional pixel clusters, readable silhouettes and material detail. Copper orange, muted teal, cream and warm gold, avoid saturated danger red and magenta. Precisely regimented uniform grid, no labels, no dividers, no text, no ground shadow, hard true alpha transparent backdrop. Leave generous clear margin on all four sides of every cell; absolutely no overlap.

### forge

Make a true transparent pixel art game effect atlas precisely 4 cols x 2 rows, each equal-sized cell isolated with clear margin. Premium handcrafted pixel art with the warm copper lighting and dark teal details of the provided game art. Row 1 left to right: isolated chunky bronze forge hammer raised on diagonal; same hammer mid downstroke; hammer at bottom with small orange impact streak; isolated spray of warm gold sparks expanding outward. Row 2: isolated small glowing coal; copper ring with top-left highlight, a tiny blackened scar on a copper chip; same scar with wispy off-white smoke. No floor, no people, no words, no outlines around the cells. Objects are distinct and fully visible. Avoid red and magenta.

### kiln_hud

Create EXACT 2x2 pixel art icon atlas, isolated onto true transparent alpha with generous clear invisible gaps, matching the rich copper/amber and dark teal details of the reference game character. Upper left an ember-filled circular kiln icon framed by orange segmented brick; upper right a white steam puff motif surrounded by several discontinuous copper arcs for an overflow passive; lower left an isolated copper kettle-lid venting steam that reads at 24px for overheat; lower right small glowing furnace ember in a thin copper hexagon for inside-kiln benefit. Graphic coherent professional top-down action RPG item icons, no letters, no backgrounds, no badges or icon boxes, no red danger colors.

### signature

Create premium detailed retro pixel art ACTUAL ANIMATION FRAMES of one character in the supplied reference. Exact 4 columns x 3 rows, 12 separate isolated sprites with generous transparent gaps. Rows face down/front, right profile, up/back. Four columns each are successive frames of one signature spell cast: spread boots and pull right elbow in; crouch with both hands gathered at chest and head down; hard casting extension with palm far out and lean into motion; settle weight onto front foot with casting hand held out lower. Same height and fixed feet baseline within each row. Fully render reference kettle hat of faceted hammered copper, expressive amber eyes when visible, layered teal hair, long teal cream-piped coat and stitched glowing oven apron. Exact consistent identity and costume. Strong contrasting silhouettes and lifelike follow-through. Only character, empty hands, no wand, no magic, no motion lines, no shadow, no color background, no text. True transparent background. Clean detailed fine pixel clustering as in the reference.

### forge_backdrop

A premium detailed pixel art game UI background for this copper-kettle fire apprentice's wand enhancement station. Wide 16:9 environment panel. Cozy small medieval smithy lit by a brick kiln on the back right, copper pipes, workbench with plain dark iron anvil in lower middle, small coal bed and bronze casting tools around edges. Upper left and center mostly dark neutral empty workshop wall with fine texture so UI panels can overlay. Golden amber and dark teal palette with neutral dark umber. Sharp refined clustered pixel detail and no text, people, interface, logos, rectangular panel borders or signage. The supplied character illustration is palette and material reference only. Full-bleed opaque art.

### locomotion

Create high-detail pixel animation sprite atlas in exact 4x3 grid for same referenced kettle hat character, copper hat highlights, teal cream-piped coat and glowing apron pocket. Rows down/front, right profile, up/back. Four animation poses each: first two columns relaxed walk passing poses with legs directly under hips (first left passing leg raised, then right passing leg raised); last two columns braced walking with bent knees, left then right leg forward, one empty open palm held ready. Keep full body, hat shape camera and apparent proportions consistent. Separate figures with broad gutters. Body only, absolutely no wands, rods, VFX, numbers, ground or text. Transparent RGBA.

### lidless

Create exact 4x3 pixel art sprite atlas for the copper kettle fire apprentice in attached grid. Match the cell-by-cell poses, same top-down camera, turning front/right/back by row; columns idle, step A, step B, cast. Same finely faceted hammered copper helmet, same rolled brim and spout, aubergine outline, teal hair and coat, ivory trims, gold buckles, furnace apron. The one visual change is that the SMALL central upper removable cap and its knob are missing; the open mouth of the helmet reads as a DARK OVAL aperture ring on the copper kettle's crown. Keep all of the remaining copper kettle helmet and spout. The engine will draw the lid as another sprite. 12 isolated tiles, generous gaps, transparent RGBA, no text nor wands or other objects.

### ritual_guard

Transparent detailed pixel-art game FX atlas, exact 4x2 equal-sized cells. Top row four separately centered animation frames of a stylized copper rune ring for the kettle wizard's kiln skill. Keep consistent geometry and colors (dark burnt bronze rim, amber golden stamped line, little angular runes with no readable alphabet, copper kettle insignia in center). Cycle from subdued outlines to glowing perimeter to amber inner symbol to fading. Bottom row four separate amber-and-copper particles for a forge enhancement ritual: tiny ember, diamond-shaped spark, joined sparks, flash with deep amber outline. Purposeful crisp pixels, true alpha, no background, no characters, no saturated danger red/magenta. Each visible design has ample empty padding.
