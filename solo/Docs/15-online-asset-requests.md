# 15. 온라인 화면에 필요한 그림 — 목록과 생성 프롬프트

지금 온라인 화면은 원래 다른 화면용으로 그린 그림(야영지 배경, 결투장 바닥, 장터 거리 …)을 빌려 쓴다. 아래 그림이 생기면 화면이 제자리를 찾는다.

**넣는 법.** 받은 그림을 `solo/Assets/Resources/Art/online/` 폴더에 **아래 파일 이름 그대로**(PNG) 넣는다. 게임이 그 이름으로 찾아 쓰고, 없으면 지금 그림을 쓴다. 코드는 바꿀 필요가 없다. 가져오기 설정(점 필터, 압축 없음)은 폴더가 알아서 맞춘다.

**받는 크기.** 생성기에서는 표의 "최종 크기"의 2~4배로 받는다. 줄이기와 배경 지우기(투명)는 내가 한다. 9조각 틀은 모서리 장식이 잘리지 않게 크게 받는다.

**같은 그림체 유지.** 첫 장이 마음에 들면, 다음 요청부터 그 그림을 같이 올리고 "Match this exact art style, palette and pixel density" 로 시작한다. 배경 5장은 같은 대화에서 차례로 만드는 것이 좋다.

---

## A. 공통 스타일 (모든 프롬프트 맨 앞에 붙인다)

### A-1. 배경용
```
High-detail 2D pixel art for a cozy-but-eerie wizard roguelike, 1920x1080 composition (16:9),
crisp hard pixel edges, no blur, no anti-aliasing, no painterly gradients, visible pixel clusters.
World: a forest campsite where an old forgotten wizard dumped broken magical junk —
dark wooden planks, yellowed paper notes, chalk scribbles, brass nails, lanterns, laundry lines.
Palette: ink navy #12131C, dark wood #1F2130 / #2A2D40, slate blue #8E9AAE, chalk white #F2EDE0,
old paper #E8DDBF, warm lantern amber #F2A33A, moonlight blue. Night or late dusk, light from top-left
plus warm firelight. Low contrast in large calm areas so game UI can sit on top.
No text, no letters, no logos, no UI elements, no characters unless asked.
```

### A-2. UI 틀·작은 그림용
```
Game UI element, high-detail pixel art, crisp hard pixel edges, transparent background,
front-facing flat view (no perspective), 1px ink-navy outline (#12131C, never pure black),
3–4 tone shading, light from top-left, small brass or chalk details.
Materials only from: dark wood boards, yellowed paper, chalk, brass nails, rope, cloth.
No glossy metal, no glass, no neon, no text, no letters, no numbers.
```

### A-3. 9조각 틀에 덧붙이는 줄
```
Designed as a 9-slice frame: decorated corners, plain repeatable straight edges,
a completely flat empty center area of a single solid color so it can be stretched.
```

---

## B. 배경 (1920×1080, A-1 뒤에 붙인다)

화면 위에 판과 카드가 올라가므로 "UI가 놓일 자리"를 조용하게 비워 둔 그림이 필요하다(지금 야영지 배경은 가운데가 꽉 차 있어 판과 싸운다).

| 파일 이름 | 쓰는 화면 | UI가 놓일 자리(비워 둘 곳) |
|---|---|---|
| `bg_online_hub.png` | 온라인 들머리(모드 카드 3장) | 가운데 가로 띠 전체(y 120~800). 아래 1/4에 장면의 볼거리 |
| `bg_login.png` | 로그인 | 왼쪽 45%가 어둡고 조용해야 함 |
| `bg_room.png` | 협동 방(자리 4개) | 아래 가운데에 사람이 설 자리 4곳 |
| `bg_records_hall.png` | 기록·강화 | 가운데·오른쪽 판 자리 |
| `bg_market.png` | 장터 | 왼쪽 2/3(카드 격자) |
| `plaza_ground.png` | 광장 걷는 땅(1840×1012) | 가운데 넓게 걸을 수 있는 땅 |

**bg_online_hub.png**
```
Wide view of a moonlit forest clearing at late dusk used as a gathering point for wizards.
Far background: purple-blue mountains, a crooked stone watchtower on the left, big moon top-right.
Bottom quarter: a low campfire slightly right of center with log seats, a wooden notice board
covered in pinned paper notes on the far right, a laundry line with patched robes on the far left.
The middle horizontal band of the image (from 12% to 75% height) must be calm, dark sky and
distant treeline only, with very low detail, because three large UI cards will cover it.
```

**bg_login.png**
```
Night campsite. On the right half: a wooden lectern holding a huge open guest-book ledger with a
quill, lit by a hanging lantern and a small campfire behind it; three small hooded wizard silhouettes
sitting around the fire far behind. The left 45% of the image is dark forest shadow and night sky
with almost no detail (a UI panel will cover it). Calm, inviting, slightly mysterious.
```

**bg_room.png** (협동 방)
```
Inside a large patched canvas tent at night, warm lantern light from above.
On the floor in the lower middle: four round worn rugs spaced evenly in one horizontal row
(where four wizards will stand), each with a small chalk circle drawn around it.
Back wall: hanging maps, a chalkboard with a crossed-out wave tally, stacked crates on the sides.
The upper-left area has an empty wooden board nailed to the tent pole (a room code note will be pinned there).
Keep the area above the rugs fairly plain.
```

**bg_records_hall.png** (기록·강화)
```
Interior of an old stone tower room used as a hall of records. Center-left: a heavy dark anvil-like
enchanting altar with chalk runes drawn around it on the floor, faint violet glow.
Right third: a tall cracked stone wall with carved horizontal lines like a ranking tablet, a few
candles on ledges. Left edge: bookshelves and five small wooden pedestals in a vertical row.
Mid-tone dark walls, low detail in the middle so UI panels can sit on top.
```

**bg_market.png**
```
A night market street in the forest camp seen slightly from above. Left two-thirds: an open
cobblestone square with low detail where item cards will be shown. Right side and edges:
wooden merchant stalls with patched awnings, a merchant cart, hanging paper lanterns, barrels and
baskets of crystals and scrolls. Warm lantern light pools on dark cobblestones.
```

**plaza_ground.png** (광장, 1840×1012, 위에서 내려다본 걷는 땅)
```
Top-down 3/4 view game map of a small village plaza for a multiplayer social hub, 1840x1012.
Center: a round stone bonfire pit with a warm glow and a ring of log benches, wide walkable
cobblestone and packed-dirt area around it (keep at least 60% of the map open and walkable).
Left edge (vertically centered): a wooden field gate leading out to grassland, with a small
signpost. Right edge (vertically centered): a stone arch with a chalk-drawn dueling circle behind it.
Top edge: two merchant stalls, a notice board, a laundry line. Bottom edge: bushes, a tent, a well.
Even top-down lighting with gentle moonlight, no strong cast shadows across the walkable area.
```

---

## C. 광장 소품 (투명 배경, A-2 뒤에 붙인다. 위에서 내려다본 3/4 시점)

| 파일 이름 | 최종 크기 | 프롬프트(A-2 뒤) |
|---|---|---|
| `plaza_gate_field.png` | 192×192 | `Top-down 3/4 view wooden field gate with a hanging lantern and a small grass tuft, open doors, game map prop.` |
| `plaza_gate_duel.png` | 192×192 | `Top-down 3/4 view stone arch with two crossed wands carved on top and a chalk duel circle on the ground in front, game map prop.` |
| `plaza_notice_board.png` | 128×128 | `Top-down 3/4 view wooden notice board on two posts covered with pinned yellowed paper notes, game map prop.` |
| `plaza_stall.png` | 192×160 | `Top-down 3/4 view small merchant stall with a patched striped cloth awning, crates and a lantern, game map prop.` |
| `plaza_lamp.png` | 48×112 | `Top-down 3/4 view thin wooden lamp post with a warm paper lantern, game map prop.` |

---

## D. UI 틀과 판 (투명 배경, A-2 + A-3 뒤에 붙인다)

| 파일 이름 | 최종 크기(원본) | 쓰는 곳 | 프롬프트(A-2, A-3 뒤) |
|---|---|---|---|
| `frame_mode_card.png` | 400×520 | 모드 카드 틀 | `A tall wooden poster frame with a brass nail in each corner and a hanging rope at the top center, a rectangular window in the upper half for an illustration and a plain dark-wood lower half for text.` |
| `frame_mode_card_selected.png` | 400×520 | 고른 모드 카드 | `Same poster frame as before but with warm lantern-amber glow along the inner edge and a small chalk star scribbled in the top-left corner.` |
| `frame_seat.png` | 288×440 | 방의 자리(한 사람) | `A tall hanging cloth banner on a horizontal wooden rod, dark indigo fabric with a slightly frayed bottom edge and a stitched border, empty center.` |
| `frame_seat_empty.png` | 288×440 | 빈자리 | `Same hanging cloth banner but faded and translucent-looking, with a dashed chalk outline of a person in the middle.` |
| `frame_code_note.png` | 480×176 | 방 코드 쪽지 | `A wide sheet of yellowed paper pinned to a wooden board with two brass pins, slightly curled corners, plain center for large handwritten letters.` |
| `frame_tablet.png` | 496×872 | 순위 석판 | `A tall carved grey stone tablet with a cracked top edge, carved rune border, small candle stubs on the top corners, flat smooth center.` |
| `frame_ledger.png` | 640×936 | 로그인 판 | `A tall open leather-bound ledger page standing upright, yellowed paper center, dark leather border with brass corner caps and a ribbon bookmark hanging at the bottom.` |
| `frame_chatbox.png` | 720×200 | 광장 대화창 | `A wide semi-transparent dark parchment strip with torn edges and a small quill drawn in the bottom-right corner.` |
| `frame_drawer.png` | 640×928 | 친구 서랍 | `A tall wooden mailbox-cabinet side panel with small pigeonhole slots carved along the top edge and a crow feather tucked in one corner, flat dark-wood center.` |
| `frame_receipt.png` | 512×160 | 장터 영수증 | `A narrow receipt slip of yellowed paper with a torn zigzag bottom edge and a small wax stamp in the top-right corner.` |

---

## E. 작은 그림 (투명 배경, A-2 뒤에 붙인다)

| 파일 이름 | 최종 크기 | 프롬프트(A-2 뒤) |
|---|---|---|
| `badge_bot.png` | 32×32 | `Small round wooden token with a brass gear symbol carved in it.` |
| `badge_ready.png` | 96×40 | `A red-brown wax seal stamp pressed onto a small paper tag, with a bold chalk check-mark shape (no letters).` |
| `badge_waiting.png` | 96×40 | `A small paper tag with a chalk hourglass drawing, slightly faded.` |
| `icon_email_account.png` | 32×32 | `A small sealed paper envelope with a wax seal.` |
| `icon_copy.png` | 24×24 | `Two overlapping small paper notes, the front one with a folded corner.` |
| `empty_friends.png` | 256×256 | `A crow sitting on an empty wooden mailbox, holding a blank letter in its beak, looking curious.` |
| `empty_market.png` | 256×256 | `An empty wooden merchant cart with a rolled-up awning and a single small coin left on the boards.` |
| `empty_ranking.png` | 256×256 | `A blank carved stone tablet with dust and one unlit candle, no carvings yet.` |
| `vs_emblem.png` | 256×256 | `Two crossed magic wands over a round chalk circle, sparks where they cross, dramatic duel emblem.` |

---

## F. 만들지 말고 받아 와야 하는 것

| 것 | 왜 | 어디서 |
|---|---|---|
| Google 로그인 버튼 / "G" 로고 | Google 상표 — 생성하거나 다시 그리면 안 된다(브랜드 규정) | Google Identity "Sign in with Google" 브랜딩 가이드의 공식 버튼 자산(밝은/어두운). 받은 PNG를 `google_button.png`(높이 48 기준)로 넣는다 |

---

## G. 지금 이미 있어서 만들 필요 없는 것

모드 카드 그림(S-0502 3장), 결투장 바닥(S-0901), "대" 글자(S-0902), 서버·오프라인 카드 그림(S-0301), 비밀번호 눈(S-0302), 방 쪽지(S-2102), 준비 표시(S-2103), 방장 왕관(S-2104), 모래시계(S-2105), 이름판(S-2001), 말풍선(S-2005), 감정 아이콘(I-F10), 장터 카드(S-2306), 수레 띠(S-2305), 순위 줄(S-2503), 메달(I-L8), 캐릭터 큰 그림·얼굴, 지팡이 아이콘·검사 그림, 작업대 배경.
