# 16. 게임 UI 그림 전부 다시 그리기 — 생성 프롬프트 (자동 생성)

`Assets/Resources/Art/ui/manifest.json`의 UI 그림 621장(그림 글꼴 제외)을 **그림 ID 191개 단위**로 묶었다. ID 하나 = 프롬프트 하나 = 시트 한 장(그 ID의 상태·변형·프레임을 격자로 늘어놓은 그림, 한 줄에 8칸까지).
설명(한국어)은 `docs/ui-assets.md`의 기획 그대로다. 이 파일은 그 둘에서 만들어진다 — 기획이 바뀌면 다시 만든다.

## 0. 넘기는 법 (순서가 중요하다)

1. **스타일 기준 한 장부터.** 아래 "0단계 기준 시트" 프롬프트로 판·버튼·입력 칸·선택 괄호를 한 장에 그린다. 마음에 들 때까지 이것만 다시 뽑는다. 이 한 장이 나머지 190개의 기준이다.
2. **그다음부터는 언제나 기준 시트를 같이 올린다.** 대화 첫 줄: `Match this exact art style, palette, outline weight and pixel density.` 그 아래에 1절의 공통 스타일 + 해당 ID의 프롬프트를 붙인다.
3. **한 번에 ID 하나.** 여러 ID를 한 요청에 섞으면 크기와 그림체가 흔들린다. 같은 묶음(예: 버튼 K-20~K-24)은 같은 대화에서 차례로.
4. **크게 받는다.** 각 칸을 최종 픽셀의 4배로 그리게 했다(최종 1픽셀 = 4×4 덩어리). 줄이기·자르기·투명 처리·9조각 여백 맞추기는 내가 한다.
5. **저장.** 받은 그림을 `solo/ArtIncoming/ui/<ID>.png`(예: `K-20.png`, 여러 장이면 `K-20_2.png`)로 넣고 알려 준다. 내가 칸을 잘라 정확한 크기·파일 이름으로 바꿔 넣고(9조각 키트는 2배 판 `solo_kit`도 같이), 게임을 띄워 화면을 찍어 확인한다. **크기와 파일 이름이 같으면 코드 수정 없이 바로 바뀐다.**
6. 큰 배경(1920×1080) 10장은 시트가 아니라 한 장씩, 배경용 스타일로.

순서 추천: 0단계 → 2.1 판과 창 → 2.2 버튼·탭·입력 → 2.3 선택 표시 → 2.5 칸과 테 → 3절 아이콘 → 4절 화면별 그림.

## 1. 공통 스타일 (모든 프롬프트 앞에 붙인다)

### 1-A. UI 부품·아이콘
```
Game UI asset sheet for a cozy-but-eerie 2D wizard roguelike. High-detail pixel art, crisp hard pixel edges,
no blur, no anti-aliasing, no gradients smoother than 3-4 tone pixel shading. Flat front view, no perspective.
Theme: things an old forgetful wizard threw away — dark wooden boards, yellowed paper notes, chalk marks,
small brass nails, rope, cloth. No glossy metal, no glass, no neon, no sci-fi.
Palette: ink #12131C (outlines, never pure black), dark wood #1F2130, wood #2A2D40, light wood edge #3A3F5A,
slate #8E9AAE, chalk white #F2EDE0, paper #E8DDBF / #C9BB96, selection yellow #FFE94A (selection marks only),
gold #FFD24A (valuable things only), amber #F2A33A (warnings), good green #6FD36B.
Never use red #FF3B30 or magenta #FF2E88. 1px ink outline, light from top-left, 2px darker bottom edge shadow.
Transparent background. Absolutely no text, letters, numbers or logos.
Draw every cell at 4x its final pixel size (each final pixel is a 4x4 block) on a clean pixel grid.
Lay the cells out in a grid, at most 8 cells per row, left to right then top to bottom, same scale,
at least 32px of empty transparent space between cells.
```

### 1-B. 9조각(늘어나는 틀)일 때 덧붙인다
```
Each cell is a 9-slice frame: all decoration lives inside the corners (the corner size is given per cell);
the four straight edges are plain and seamlessly repeatable; the center is one flat solid color.
```

### 1-C. 여러 상태(기본·올림·누름·꺼짐)일 때 덧붙인다
```
All state cells are the same object with identical shape and size. normal = as designed;
hover = edge one step lighter (#3A3F5A) plus a 1px light inner line; pressed = whole object 2px lower and the
bottom shadow gone; disabled = fully desaturated at 60% brightness; on = filled with #3A3F5A plus a 4px
chalk-white line along the bottom.
```

### 1-D. 큰 배경(1920×1080)
```
High-detail 2D pixel art scene, 16:9, crisp pixels, no blur. Same world and palette as above, night or late dusk,
light from top-left plus warm firelight. Large calm low-detail areas where game UI will sit (described per image).
No text, no UI, no characters unless asked. Deliver at 1920x1080 or larger.
```

## 0단계 기준 시트
```
(1-A + 1-B + 1-C를 붙인 뒤)
Style reference sheet with 8 cells: (1) a dark wooden board panel 48x48 with brass nails in the corners,
corner 16px; (2) a pressed-in recessed dark panel 48x48, corner 16px; (3) a yellowed paper note panel 48x48 with
one folded corner, corner 16px; (4) a large button 48x64, corner 12px, normal state; (5) the same button hover;
(6) the same button pressed; (7) a text input field 48x40 made of light planed wood, corner 12px;
(8) one corner of a selection bracket: a thick 4px yellow #FFE94A L-shape with a 2px ink outline, 32x32.
```

## 2.1 판과 창

### K-01 — 기본 판(어두운 나무판)
- 파일 1장, 기획 크기: 9조각, 원본 48×48, 모서리 16, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-01: 기본 판(어두운 나무판). Design note (Korean, follow it exactly): 먹색 2px 외곽 + 윗변 밝은 1px + 아랫변 그늘 2px. 네 모서리에 놋쇠 못 4×4
Sheet with 1 cells, left to right:
  1. board — final 48x48 px, 9-slice corners 16/16/16/16 px
```
### K-02 — 안쪽 판(눌린 칸)
- 파일 1장, 기획 크기: 9조각, 원본 48×48, 모서리 16, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-02: 안쪽 판(눌린 칸). Design note (Korean, follow it exactly): 목록 바탕, 미리보기 바탕
Sheet with 1 cells, left to right:
  1. recess — final 48x48 px, 9-slice corners 16/16/16/16 px
```
### K-03 — 쪽지 판
- 파일 1장, 기획 크기: 9조각, 원본 48×48, 모서리 16, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-03: 쪽지 판. Design note (Korean, follow it exactly): 우편 본문, 결과 카드 안쪽, 확인 창. 한쪽 모서리가 접힌 종이
Sheet with 1 cells, left to right:
  1. paper — final 48x48 px, 9-slice corners 16/16/16/16 px
```
### K-04 — 제목 띠
- 파일 1장, 기획 크기: 9조각, 원본 96×48, 좌우 모서리 24, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-04: 제목 띠. Design note (Korean, follow it exactly): 판 위에 얹는 머리. F-24 한 줄
Sheet with 1 cells, left to right:
  1. heading strip — final 96x48 px, 9-slice corners 24/12/24/12 px
```
### K-05 — 화면 제목 띠
- 파일 1장, 기획 크기: 9조각, 원본 96×80, 좌우 모서리 24, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-05: 화면 제목 띠. Design note (Korean, follow it exactly): 전체 화면 메뉴의 위 띠(높이 80. 강화 화면의 위 띠와 같은 높이)
Sheet with 1 cells, left to right:
  1. screen heading — final 96x80 px, 9-slice corners 24/16/24/16 px
```
### K-06 — 아래 조작 안내 띠
- 파일 1장, 기획 크기: 9조각, 원본 96×48, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-06: 아래 조작 안내 띠. Design note (Korean, follow it exactly): 화면 맨 아래 높이 48의 띠. "J 고르기 · K 뒤로 · Esc 나가기"를 키 마개(K-50)와 글로 적는다
Sheet with 1 cells, left to right:
  1. control strip — final 96x48 px, 9-slice corners 16/8/16/8 px
```
### K-07 — 구분선
- 파일 2장, 기획 크기: 가로 16×4, 세로 4×16, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset K-07: 구분선. Design note (Korean, follow it exactly): 이어 붙인다
Sheet with 2 cells, left to right:
  1. divider horizontal — final 16x4 px
  2. divider vertical — final 4x16 px
```
### K-08 — 툴팁 판
- 파일 1장, 기획 크기: 9조각, 원본 24×24, 모서리 8, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-08: 툴팁 판. Design note (Korean, follow it exactly): F-16, 최대 폭 400
Sheet with 1 cells, left to right:
  1. tooltip — final 24x24 px, 9-slice corners 8/8/8/8 px
```
### K-09 — 툴팁 꼬리
- 파일 1장, 기획 크기: 12×8, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset K-09: 툴팁 꼬리. Design note (Korean, follow it exactly): 위·아래는 뒤집어 쓴다
Sheet with 1 cells, left to right:
  1. tooltip tail — final 12x8 px
```
### K-11 — 알림 쪽지(토스트)
- 파일 1장, 기획 크기: 9조각, 원본 48×48, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-11: 알림 쪽지(토스트). Design note (Korean, follow it exactly): 화면 오른쪽 위에서 내려오는 480×96 쪽지. 친구 접속, 우편 도착, 거래 신청
Sheet with 1 cells, left to right:
  1. toast — final 48x48 px, 9-slice corners 16/16/16/16 px
```
### K-12 — 판 귀퉁이 장식(까마귀 깃, 빨래집게, 분필 낙서)
- 파일 3장, 기획 크기: 32×32, 상태·프레임: 3종
- 붙일 공통: 1-A
```
Asset K-12: 판 귀퉁이 장식(까마귀 깃, 빨래집게, 분필 낙서). Design note (Korean, follow it exactly): 판 모서리에 얹는 덧그림. 큰 판(가로 960 이상)에만
Sheet with 3 cells, left to right:
  1. chalk scrap — final 32x32 px
  2. clothespin — final 32x32 px
  3. crow feather — final 32x32 px
```

## 2.2 버튼, 탭, 입력

### K-20 — 큰 버튼
- 파일 4장, 기획 크기: 9조각, 높이 64, 원본 48×64, 상태·프레임: 기본, 올림, 누름, 꺼짐
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-20: 큰 버튼. Design note (Korean, follow it exactly): F-24. 최소 폭 192
Sheet with 4 cells, left to right:
  1. button large disabled — final 48x64 px, 9-slice corners 12/12/12/12 px
  2. button large hover — final 48x64 px, 9-slice corners 12/12/12/12 px
  3. button large normal — final 48x64 px, 9-slice corners 12/12/12/12 px
  4. button large pressed — final 48x64 px, 9-slice corners 12/12/12/12 px
```
### K-21 — 보통 버튼
- 파일 4장, 기획 크기: 9조각, 높이 48, 원본 48×48, 상태·프레임: 기본, 올림, 누름, 꺼짐
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-21: 보통 버튼. Design note (Korean, follow it exactly): F-24. 최소 폭 128
Sheet with 4 cells, left to right:
  1. button medium disabled — final 48x48 px, 9-slice corners 12/12/12/12 px
  2. button medium hover — final 48x48 px, 9-slice corners 12/12/12/12 px
  3. button medium normal — final 48x48 px, 9-slice corners 12/12/12/12 px
  4. button medium pressed — final 48x48 px, 9-slice corners 12/12/12/12 px
```
### K-22 — 작은 버튼
- 파일 4장, 기획 크기: 9조각, 높이 32, 원본 32×32, 상태·프레임: 기본, 올림, 누름, 꺼짐
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-22: 작은 버튼. Design note (Korean, follow it exactly): F-16. 아이콘 하나만 넣으면 32×32
Sheet with 4 cells, left to right:
  1. button small disabled — final 32x32 px, 9-slice corners 8/8/8/8 px
  2. button small hover — final 32x32 px, 9-slice corners 8/8/8/8 px
  3. button small normal — final 32x32 px, 9-slice corners 8/8/8/8 px
  4. button small pressed — final 32x32 px, 9-slice corners 8/8/8/8 px
```
### K-23 — 주 버튼(출발, 강화, 확인)
- 파일 4장, 기획 크기: 9조각, 높이 64, 원본 48×64, 상태·프레임: 기본, 올림, 누름, 꺼짐
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-23: 주 버튼(출발, 강화, 확인). Design note (Korean, follow it exactly): 분필 흰색 굵은 테. 강화 버튼은 속성색을 입힌다. 금색은 입히지 않는다
Sheet with 4 cells, left to right:
  1. button primary disabled — final 48x64 px, 9-slice corners 12/12/12/12 px
  2. button primary hover — final 48x64 px, 9-slice corners 12/12/12/12 px
  3. button primary normal — final 48x64 px, 9-slice corners 12/12/12/12 px
  4. button primary pressed — final 48x64 px, 9-slice corners 12/12/12/12 px
```
### K-24 — 조심 버튼(포기, 삭제, 신고, 승급 확정)
- 파일 4장, 기획 크기: 9조각, 높이 48, 원본 48×48, 상태·프레임: 기본, 올림, 누름, 꺼짐
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-24: 조심 버튼(포기, 삭제, 신고, 승급 확정). Design note (Korean, follow it exactly): 호박색 테. 빨강을 쓰지 않는다
Sheet with 4 cells, left to right:
  1. button caution disabled — final 48x48 px, 9-slice corners 12/12/12/12 px
  2. button caution hover — final 48x48 px, 9-slice corners 12/12/12/12 px
  3. button caution normal — final 48x48 px, 9-slice corners 12/12/12/12 px
  4. button caution pressed — final 48x48 px, 9-slice corners 12/12/12/12 px
```
### K-25 — 탭
- 파일 4장, 기획 크기: 9조각, 높이 48, 원본 48×48, 상태·프레임: 기본, 올림, 켜짐, 꺼짐
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-25: 탭. Design note (Korean, follow it exactly): 판 위쪽에 붙는다. 켜진 탭은 판과 이어진다
Sheet with 4 cells, left to right:
  1. tab disabled — final 48x48 px, 9-slice corners 12/12/12/12 px
  2. tab hover — final 48x48 px, 9-slice corners 12/12/12/12 px
  3. tab normal — final 48x48 px, 9-slice corners 12/12/12/12 px
  4. tab on — final 48x48 px, 9-slice corners 12/12/12/12 px
```
### K-26 — 세로 분류 탭
- 파일 3장, 기획 크기: 9조각, 높이 56, 원본 48×56, 상태·프레임: 기본, 올림, 켜짐
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-26: 세로 분류 탭. Design note (Korean, follow it exactly): 왼쪽 분류 목록(장터, 옷장, 설정)
Sheet with 3 cells, left to right:
  1. tab vertical hover — final 48x56 px, 9-slice corners 12/12/12/12 px
  2. tab vertical normal — final 48x56 px, 9-slice corners 12/12/12/12 px
  3. tab vertical on — final 48x56 px, 9-slice corners 12/12/12/12 px
```
### K-27 — 토글 스위치
- 파일 3장, 기획 크기: 48×24, 상태·프레임: 끔, 켬, 꺼짐
- 붙일 공통: 1-A + 1-C
```
Asset K-27: 토글 스위치. Design note (Korean, follow it exactly): 켬은 손잡이가 오른쪽 + 분필 흰색 줄
Sheet with 3 cells, left to right:
  1. toggle disabled — final 48x24 px
  2. toggle on — final 48x24 px
  3. toggle off — final 48x24 px
```
### K-28 — 체크 상자
- 파일 3장, 기획 크기: 24×24, 상태·프레임: 빔, 참, 꺼짐
- 붙일 공통: 1-A + 1-C
```
Asset K-28: 체크 상자. Design note (Korean, follow it exactly): -
Sheet with 3 cells, left to right:
  1. checkbox disabled — final 24x24 px
  2. checkbox on — final 24x24 px
  3. checkbox empty — final 24x24 px
```
### K-29 — 고르기 단추(라디오)
- 파일 3장, 기획 크기: 24×24, 상태·프레임: 빔, 참, 꺼짐
- 붙일 공통: 1-A + 1-C
```
Asset K-29: 고르기 단추(라디오). Design note (Korean, follow it exactly): -
Sheet with 3 cells, left to right:
  1. radio disabled — final 24x24 px
  2. radio on — final 24x24 px
  3. radio empty — final 24x24 px
```
### K-30 — 슬라이더
- 파일 5장, 기획 크기: 레일 9조각 16×8, 채움 16×8, 손잡이 16×24, 상태·프레임: 손잡이 기본·올림·누름
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-30: 슬라이더. Design note (Korean, follow it exactly): 0~100 값. 오른쪽에 숫자
Sheet with 5 cells, left to right:
  1. slider fill — final 16x8 px, 9-slice corners 4/2/4/2 px
  2. slider rail — final 16x8 px, 9-slice corners 2/2/2/2 px
  3. slider thumb hover — final 16x24 px
  4. slider thumb normal — final 16x24 px
  5. slider thumb pressed — final 16x24 px
```
### K-31 — 고르기 상자(드롭다운)
- 파일 3장, 기획 크기: 9조각, 높이 40, 원본 48×40, 상태·프레임: 기본, 올림, 펼침
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-31: 고르기 상자(드롭다운). Design note (Korean, follow it exactly): 펼친 목록은 K-02
Sheet with 3 cells, left to right:
  1. dropdown hover — final 48x40 px, 9-slice corners 12/12/12/12 px
  2. dropdown normal — final 48x40 px, 9-slice corners 12/12/12/12 px
  3. dropdown expanded — final 48x40 px, 9-slice corners 12/12/12/12 px
```
### K-32 — 단계 고르기(◀ 값 ▶)
- 파일 3장, 기획 크기: 화살 24×24, 상태·프레임: 기본, 올림, 꺼짐
- 붙일 공통: 1-A + 1-C
```
Asset K-32: 단계 고르기(◀ 값 ▶). Design note (Korean, follow it exactly): 한쪽만 그려 뒤집는다. "전체 / 줄임 / 최소" 같은 값
Sheet with 3 cells, left to right:
  1. step disabled — final 24x24 px
  2. step hover — final 24x24 px
  3. step normal — final 24x24 px
```
### K-33 — 입력 칸
- 파일 4장, 기획 크기: 9조각, 높이 40, 원본 48×40, 상태·프레임: 기본, 입력 중, 오류, 꺼짐
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-33: 입력 칸. Design note (Korean, follow it exactly): 오류는 호박색 테. 깜박이는 커서는 코드(초당 1회)
Sheet with 4 cells, left to right:
  1. input disabled — final 48x40 px, 9-slice corners 12/12/12/12 px
  2. input normal — final 48x40 px, 9-slice corners 12/12/12/12 px
  3. input editing — final 48x40 px, 9-slice corners 12/12/12/12 px
  4. input error — final 48x40 px, 9-slice corners 12/12/12/12 px
```
### K-34 — 스크롤 막대
- 파일 4장, 기획 크기: 레일 8×16, 손잡이 9조각 8×24, 상태·프레임: 손잡이 기본·올림·누름
- 붙일 공통: 1-A + 1-B + 1-C
```
Asset K-34: 스크롤 막대. Design note (Korean, follow it exactly): 폭 8
Sheet with 4 cells, left to right:
  1. scroll rail — final 8x16 px, 9-slice corners 2/2/2/2 px
  2. scroll thumb hover — final 8x24 px, 9-slice corners 2/2/2/2 px
  3. scroll thumb normal — final 8x24 px, 9-slice corners 2/2/2/2 px
  4. scroll thumb pressed — final 8x24 px, 9-slice corners 2/2/2/2 px
```
### K-35 — 닫기 단추(×)
- 파일 3장, 기획 크기: 32×32, 상태·프레임: 기본, 올림, 누름
- 붙일 공통: 1-A + 1-C
```
Asset K-35: 닫기 단추(×). Design note (Korean, follow it exactly): 창의 오른쪽 위
Sheet with 3 cells, left to right:
  1. close hover — final 32x32 px
  2. close normal — final 32x32 px
  3. close pressed — final 32x32 px
```
### K-36 — 진행 막대
- 파일 2장, 기획 크기: 레일 9조각 16×16, 채움 9조각 16×16, 상태·프레임: 2
- 붙일 공통: 1-A + 1-B
```
Asset K-36: 진행 막대. Design note (Korean, follow it exactly): 불러오기, 대기, 남은 시간. 색을 입혀 쓴다
Sheet with 2 cells, left to right:
  1. progress fill — final 16x16 px, 9-slice corners 4/4/4/4 px
  2. progress rail — final 16x16 px, 9-slice corners 5/5/5/5 px
```
### K-38 — 쪽 넘김 화살
- 파일 3장, 기획 크기: 32×32, 상태·프레임: 기본, 올림, 꺼짐
- 붙일 공통: 1-A + 1-C
```
Asset K-38: 쪽 넘김 화살. Design note (Korean, follow it exactly): 한쪽만 그려 뒤집는다. 목록의 쪽, 캐릭터 넘기기
Sheet with 3 cells, left to right:
  1. page disabled — final 32x32 px
  2. page hover — final 32x32 px
  3. page normal — final 32x32 px
```

## 2.3 선택 표시와 작은 표시

### K-40 — 선택 괄호(한 모서리)
- 파일 3장, 기획 크기: 16×16 / 24×24 / 32×32 (팔 12 / 20 / 28), 상태·프레임: 크기 3 × 2프레임
- 붙일 공통: 1-A
```
Asset K-40: 선택 괄호(한 모서리). Design note (Korean, follow it exactly): 1.3절. 회청 괄호는 색을 입혀 만든다
Sheet with 3 cells, left to right:
  1. selection corner — final 16x16 px, 2 animation frames (draw each frame as its own cell)
  2. selection corner — final 24x24 px, 2 animation frames (draw each frame as its own cell)
  3. selection corner — final 32x32 px, 2 animation frames (draw each frame as its own cell)
```
### K-41 — 확정 체크
- 파일 2장, 기획 크기: 16×16, 24×24, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset K-41: 확정 체크. Design note (Korean, follow it exactly): 분필 흰색 체크 + 먹색 테
Sheet with 2 cells, left to right:
  1. confirmation — final 16x16 px
  2. confirmation — final 24x24 px
```
### K-42 — 새 소식 점
- 파일 1장, 기획 크기: 12×12, 상태·프레임: 2프레임
- 붙일 공통: 1-A
```
Asset K-42: 새 소식 점. Design note (Korean, follow it exactly): 선택 노랑 점. 메뉴 항목과 탭의 오른쪽 위. 빨간 점을 쓰지 않는다
Sheet with 1 cells, left to right:
  1. unread dot — final 12x12 px, 2 animation frames (draw each frame as its own cell)
```
### K-43 — 자물쇠(잠김 표시)
- 파일 2장, 기획 크기: 16×16, 24×24, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset K-43: 자물쇠(잠김 표시). Design note (Korean, follow it exactly): 잠긴 칸, 비밀번호 방, 거래 잠금 지팡이
Sheet with 2 cells, left to right:
  1. lock — final 16x16 px
  2. lock — final 24x24 px
```
### K-44 — 불러오는 중(작은 모닥불)
- 파일 1장, 기획 크기: 32×32, 상태·프레임: 6프레임
- 붙일 공통: 1-A
```
Asset K-44: 불러오는 중(작은 모닥불). Design note (Korean, follow it exactly): 서버를 기다릴 때. 0.6초 반복
Sheet with 1 cells, left to right:
  1. loading campfire — final 32x32 px, 6 animation frames (draw each frame as its own cell)
```
### K-45 — 숫자 딱지
- 파일 1장, 기획 크기: 9조각, 원본 16×16, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-45: 숫자 딱지. Design note (Korean, follow it exactly): 수량, 안 읽은 수. F-12
Sheet with 1 cells, left to right:
  1. count tag — final 16x16 px, 9-slice corners 4/4/4/4 px
```
### K-46 — 꼬리표
- 파일 1장, 기획 크기: 9조각, 원본 24×16, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset K-46: 꼬리표. Design note (Korean, follow it exactly): "하락 가능", "거래 잠금 6일", "마감 임박", "적용 +9" 같은 짧은 글. 색을 입혀 쓴다
Sheet with 1 cells, left to right:
  1. label — final 24x16 px, 9-slice corners 4/4/4/4 px
```

## 2.4 키와 마우스 표시

### K-50 — 키 마개
- 파일 2장, 기획 크기: 9조각, 원본 24×24, 상태·프레임: 기본, 눌림
- 붙일 공통: 1-A + 1-B
```
Asset K-50: 키 마개. Design note (Korean, follow it exactly): 글자 하나면 24×24, 긴 이름(Shift, Space, Enter, Esc, Tab)은 옆으로 늘린다. F-12
Sheet with 2 cells, left to right:
  1. keycap normal — final 24x24 px, 9-slice corners 6/6/6/6 px
  2. keycap pressed — final 24x24 px, 9-slice corners 6/6/6/6 px
```
### K-51 — 큰 키 마개
- 파일 2장, 기획 크기: 9조각, 원본 48×48, 상태·프레임: 기본, 눌림
- 붙일 공통: 1-A + 1-B
```
Asset K-51: 큰 키 마개. Design note (Korean, follow it exactly): 튜토리얼과 키 재설정 화면. F-24
Sheet with 2 cells, left to right:
  1. keycap normal — final 48x48 px, 9-slice corners 12/12/12/12 px
  2. keycap pressed — final 48x48 px, 9-slice corners 12/12/12/12 px
```
### K-52 — 마우스(왼쪽 누름, 오른쪽 누름, 휠)
- 파일 3장, 기획 크기: 24×24, 상태·프레임: 3종
- 붙일 공통: 1-A
```
Asset K-52: 마우스(왼쪽 누름, 오른쪽 누름, 휠). Design note (Korean, follow it exactly): 메뉴 조작 안내
Sheet with 3 cells, left to right:
  1. mouse left — final 24x24 px
  2. mouse right — final 24x24 px
  3. mouse wheel — final 24x24 px
```
### K-53 — 방향 입력 문양(앞, 뒤, 옆, 반대쪽 옆)
- 파일 8장, 기획 크기: 16×16, 24×24, 상태·프레임: 4종 × 2크기
- 붙일 공통: 1-A
```
Asset K-53: 방향 입력 문양(앞, 뒤, 옆, 반대쪽 옆). Design note (Korean, follow it exactly): 시야 기준의 방향이다. 화살표 모양이 아니라 "앞 = 삼각 하나, 뒤 = 꺾인 갈고리, 옆 = 옆 삼각, 반대쪽 옆 = 옆 삼각 + 되돌이 점"으로 그려 키보드 화살표와 헷갈리지 않게 한다. HUD의 최근 입력(16), 트라이 그림과 연습(24)
Sheet with 8 cells, left to right:
  1. direction back — final 16x16 px
  2. direction back — final 24x24 px
  3. direction forward — final 16x16 px
  4. direction forward — final 24x24 px
  5. direction reverse side — final 16x16 px
  6. direction reverse side — final 24x24 px
  7. direction side — final 16x16 px
  8. direction side — final 24x24 px
```
### K-54 — K 입력 문양, 섬(저스트) 문양
- 파일 4장, 기획 크기: 16×16, 24×24, 상태·프레임: 2종 × 2크기
- 붙일 공통: 1-A
```
Asset K-54: K 입력 문양, 섬(저스트) 문양. Design note (Korean, follow it exactly): 섬은 흰색 + 먹색 테의 작은 별(`effects.md`의 저스트 별과 같은 도안)
Sheet with 4 cells, left to right:
  1. input just — final 16x16 px
  2. input just — final 24x24 px
  3. input k — final 16x16 px
  4. input k — final 24x24 px
```
### K-55 — 입력 사이 이음줄(이어짐, 끊어짐)
- 파일 2장, 기획 크기: 8×16, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset K-55: 입력 사이 이음줄(이어짐, 끊어짐). Design note (Korean, follow it exactly): 최근 입력 표시에서 커맨드가 이어지는 중이면 줄, 간격을 넘기면 끊긴 줄(`effects.md` 12.1절)
Sheet with 2 cells, left to right:
  1. input link connected — final 8x16 px
  2. input link gap — final 8x16 px
```

## 2.5 칸과 테

### K-60 — 물건 칸
- 파일 4장, 기획 크기: 64×64, 상태·프레임: 빔, 참, 잠김, 놓을 수 없음
- 붙일 공통: 1-A
```
Asset K-60: 물건 칸. Design note (Korean, follow it exactly): 안에 48×48 아이콘. 서고, 거래, 우편 첨부, 장터, 연결 슬롯
Sheet with 4 cells, left to right:
  1. item slot empty — final 64x64 px
  2. item slot filled — final 64x64 px
  3. item slot locked — final 64x64 px
  4. item slot unavailable — final 64x64 px
```
### K-61 — 작은 칸
- 파일 2장, 기획 크기: 32×32, 상태·프레임: 빔, 참
- 붙일 공통: 1-A
```
Asset K-61: 작은 칸. Design note (Korean, follow it exactly): 안에 24×24 아이콘. HUD의 회로는 K-70
Sheet with 2 cells, left to right:
  1. small slot empty — final 32x32 px
  2. small slot filled — final 32x32 px
```
### K-62 — 지팡이 칸
- 파일 3장, 기획 크기: 80×80, 상태·프레임: 빔, 참, 원정 중(잠김)
- 붙일 공통: 1-A
```
Asset K-62: 지팡이 칸. Design note (Korean, follow it exactly): 안에 지팡이 목록 아이콘 64×64(`world-characters.md` 3.3절)
Sheet with 3 cells, left to right:
  1. wand slot empty — final 80x80 px
  2. wand slot expedition locked — final 80x80 px
  3. wand slot filled — final 80x80 px
```
### K-63 — 코어 칸
- 파일 2장, 기획 크기: 72×72, 상태·프레임: 빔, 참
- 붙일 공통: 1-A
```
Asset K-63: 코어 칸. Design note (Korean, follow it exactly): 지팡이 회로의 첫 칸. 굵은 테 + 위쪽에 작은 세모
Sheet with 2 cells, left to right:
  1. core slot empty — final 72x72 px
  2. core slot filled — final 72x72 px
```
### K-64 — 확장 칸
- 파일 2장, 기획 크기: 64×64, 상태·프레임: 아직 없음(점선), 열림
- 붙일 공통: 1-A
```
Asset K-64: 확장 칸. Design note (Korean, follow it exactly): 금색 가는 테. 평생 3칸까지(`GDD.md` 9절)
Sheet with 2 cells, left to right:
  1. extension slot closed — final 64x64 px
  2. extension slot open — final 64x64 px
```
### K-65 — 칸 사이 이음
- 파일 2장, 기획 크기: 16×8, 상태·프레임: 기본, 흐름(2프레임)
- 붙일 공통: 1-A
```
Asset K-65: 칸 사이 이음. Design note (Korean, follow it exactly): 회로의 칸을 잇는 선. 실행 순서 방향으로 흐른다
Sheet with 2 cells, left to right:
  1. circuit link normal — final 16x8 px
  2. circuit link flow — final 16x8 px, 2 animation frames (draw each frame as its own cell)
```
### K-66 — 등급 테(칸용)
- 파일 3장, 기획 크기: 64×64, 상태·프레임: 일반, 희귀, 영웅
- 붙일 공통: 1-A
```
Asset K-66: 등급 테(칸용). Design note (Korean, follow it exactly): K-60 위에 겹친다. 일반 = 회색 민테, 희귀 = 파랑 테 + 네 모서리 점, 영웅 = 금 테 + 모서리 장식 + 윗변 꼭지
Sheet with 3 cells, left to right:
  1. rarity normal — final 64x64 px
  2. rarity hero — final 64x64 px
  3. rarity rare — final 64x64 px
```
### K-67 — 등급 테(작은 칸용)
- 파일 3장, 기획 크기: 32×32, 상태·프레임: 일반, 희귀, 영웅
- 붙일 공통: 1-A
```
Asset K-67: 등급 테(작은 칸용). Design note (Korean, follow it exactly): 같은 모양 규칙
Sheet with 3 cells, left to right:
  1. rarity normal — final 32x32 px
  2. rarity hero — final 32x32 px
  3. rarity rare — final 32x32 px
```
### K-68 — 등급 테(카드용)
- 파일 3장, 기획 크기: 9조각, 원본 64×64, 모서리 24, 상태·프레임: 일반, 희귀, 영웅
- 붙일 공통: 1-A + 1-B
```
Asset K-68: 등급 테(카드용). Design note (Korean, follow it exactly): 전리품 카드, 상점 카드
Sheet with 3 cells, left to right:
  1. rarity normal — final 64x64 px, 9-slice corners 24/24/24/24 px
  2. rarity hero — final 64x64 px, 9-slice corners 24/24/24/24 px
  3. rarity rare — final 64x64 px, 9-slice corners 24/24/24/24 px
```
### K-69 — 빌림 점선 테
- 파일 2장, 기획 크기: 칸용 64×64, 카드용 9조각 64×64, 상태·프레임: 2
- 붙일 공통: 1-A + 1-B
```
Asset K-69: 빌림 점선 테. Design note (Korean, follow it exactly): 웨이브 6의 빌린 블록(`monsters.md` 6.2절). "빌림" 꼬리표 그림은 `monsters.md` 9.5절
Sheet with 2 cells, left to right:
  1. borrowed card — final 64x64 px, 9-slice corners 16/16/16/16 px
  2. borrowed slot — final 64x64 px
```
### K-70 — HUD 회로 칸
- 파일 2장, 기획 크기: 40×40, 상태·프레임: 꺼짐, 켜짐(2프레임)
- 붙일 공통: 1-A + 1-C
```
Asset K-70: HUD 회로 칸. Design note (Korean, follow it exactly): 전투 중 하단의 지팡이 회로. 안에 24×24 블록 아이콘. 실행 중인 칸이 켜진다
Sheet with 2 cells, left to right:
  1. hud circuit on — final 40x40 px, 2 animation frames (draw each frame as its own cell)
  2. hud circuit off — final 40x40 px
```
### K-71 — 조립 경고 딱지
- 파일 2장, 기획 크기: 16×16, 24×24, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset K-71: 조립 경고 딱지. Design note (Korean, follow it exactly): 호박색 세모 + 느낌표. "트리거 뒤에 투사체가 없다", "효과 없는 조합"
Sheet with 2 cells, left to right:
  1. assembly warning — final 16x16 px
  2. assembly warning — final 24x24 px
```
### K-73 — 같은 속성 점
- 파일 1장, 기획 크기: 8×8, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset K-73: 같은 속성 점. Design note (Korean, follow it exactly): 제물 칸에서 지팡이와 같은 속성인 블록에 붙는 점. 속성색을 입힌다(`effects.md` 1.2절)
Sheet with 1 cells, left to right:
  1. element dot — final 8x8 px
```

## 3.1 주문 블록 아이콘 16종

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.1절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-B1 — 블록 아이콘(보통)
- 파일 16장, 기획 크기: 48×48, 상태·프레임: 16종
- 붙일 공통: 1-A
```
Asset I-B1: 블록 아이콘(보통). Design note (Korean, follow it exactly): 물건 칸, 제물 칸(56×56 틀 안), 서고, 장터. 전리품 카드에서는 2배(96×96)
Sheet with 16 cells, left to right:
  1. block boomerang — final 48x48 px
  2. block chain lightning — final 48x48 px
  3. block corrosion — final 48x48 px
  4. block double size — final 48x48 px
  5. block fireball — final 48x48 px
  6. block frost — final 48x48 px
  7. block homing — final 48x48 px
  8. block ice spike — final 48x48 px
  9. block magic bolt — final 48x48 px
  10. block on hit trigger — final 48x48 px
  11. block pierce — final 48x48 px
  12. block shadow orb — final 48x48 px
  13. block split three — final 48x48 px
  14. block split two — final 48x48 px
  15. block triple heat — final 48x48 px
  16. block wall bounce — final 48x48 px
```
### I-B2 — 블록 아이콘(작은 것)
- 파일 16장, 기획 크기: 24×24, 상태·프레임: 16종
- 붙일 공통: 1-A
```
Asset I-B2: 블록 아이콘(작은 것). Design note (Korean, follow it exactly): HUD 회로, 컴파일 트리의 마디, 채팅의 물건 이음, 순위표의 조립 줄, 살펴보기
Sheet with 16 cells, left to right:
  1. block boomerang — final 24x24 px
  2. block chain lightning — final 24x24 px
  3. block corrosion — final 24x24 px
  4. block double size — final 24x24 px
  5. block fireball — final 24x24 px
  6. block frost — final 24x24 px
  7. block homing — final 24x24 px
  8. block ice spike — final 24x24 px
  9. block magic bolt — final 24x24 px
  10. block on hit trigger — final 24x24 px
  11. block pierce — final 24x24 px
  12. block shadow orb — final 24x24 px
  13. block split three — final 24x24 px
  14. block split two — final 24x24 px
  15. block triple heat — final 24x24 px
  16. block wall bounce — final 24x24 px
```
### I-B3 — 종류 바탕 판
- 파일 6장, 기획 크기: 48×48, 24×24, 상태·프레임: 3종 × 2크기
- 붙일 공통: 1-A
```
Asset I-B3: 종류 바탕 판. Design note (Korean, follow it exactly): 둥근 판, 육각 판, 깎은 네모 판
Sheet with 6 cells, left to right:
  1. plate control — final 24x24 px
  2. plate control — final 48x48 px
  3. plate modifier — final 24x24 px
  4. plate modifier — final 48x48 px
  5. plate projectile — final 24x24 px
  6. plate projectile — final 48x48 px
```

## 3.2 속성 문양

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.2절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-E1 — 속성 문양(보통)
- 파일 6장, 기획 크기: 32×32, 상태·프레임: 6종(무 포함)
- 붙일 공통: 1-A
```
Asset I-E1: 속성 문양(보통). Design note (Korean, follow it exactly): 캐릭터 카드, 지팡이 정보, 야영의 약점·저항 줄, 장터 분류
Sheet with 6 cells, left to right:
  1. element arcane — final 32x32 px
  2. element fire — final 32x32 px
  3. element frost — final 32x32 px
  4. element lightning — final 32x32 px
  5. element neutral — final 32x32 px
  6. element shadow — final 32x32 px
```
### I-E2 — 속성 문양(큰 것)
- 파일 5장, 기획 크기: 64×64, 상태·프레임: 5종
- 붙일 공통: 1-A
```
Asset I-E2: 속성 문양(큰 것). Design note (Korean, follow it exactly): 캐릭터 선택의 배경 문양, 결투 시작 화면
Sheet with 5 cells, left to right:
  1. element arcane — final 64x64 px
  2. element fire — final 64x64 px
  3. element frost — final 64x64 px
  4. element lightning — final 64x64 px
  5. element shadow — final 64x64 px
```
### I-E3 — 속성 문양 "무"(작은 것)
- 파일 6장, 기획 크기: 16×16, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset I-E3: 속성 문양 "무"(작은 것). Design note (Korean, follow it exactly): 다섯 속성의 16×16은 `effects.md` 20.3절(`monsters.md` 9.5절과 같은 그림). 같은 도안으로 맞춘다
Sheet with 6 cells, left to right:
  1. element arcane — final 16x16 px
  2. element fire — final 16x16 px
  3. element frost — final 16x16 px
  4. element lightning — final 16x16 px
  5. element neutral — final 16x16 px
  6. element shadow — final 16x16 px
```
### I-E4 — 약점·저항 묶음 틀
- 파일 2장, 기획 크기: 9조각, 원본 24×24, 상태·프레임: 약점(▲), 저항(▼)
- 붙일 공통: 1-A + 1-B
```
Asset I-E4: 약점·저항 묶음 틀. Design note (Korean, follow it exactly): 야영의 "다음 웨이브" 줄과 보스 체력 바 옆. 화살표 그림은 `monsters.md` 9.5절
Sheet with 2 cells, left to right:
  1. resistance — final 24x24 px, 9-slice corners 8/8/8/8 px
  2. weakness — final 24x24 px, 9-slice corners 8/8/8/8 px
```

## 3.3 등급 테

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.3절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-G1 — 지팡이 단계 묶음 테
- 파일 5장, 기획 크기: 80×80, 상태·프레임: +0~+3, +4~+6, +7~+9, +10~+12, +13
- 붙일 공통: 1-A
```
Asset I-G1: 지팡이 단계 묶음 테. Design note (Korean, follow it exactly): 민테 → 못 2개 → 못 4개 → 속성색 가는 테 → 금 테. 단계 숫자는 F-12로 오른쪽 아래에 적는다
Sheet with 5 cells, left to right:
  1. wand tier 0 to 3 — final 80x80 px
  2. wand tier 10 to 12 — final 80x80 px
  3. wand tier 13 — final 80x80 px
  4. wand tier 4 to 6 — final 80x80 px
  5. wand tier 7 to 9 — final 80x80 px
```
### I-G2 — 흔적 딱지
- 파일 1장, 기획 크기: 24×24, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset I-G2: 흔적 딱지. Design note (Korean, follow it exactly): 거래 창과 장터 목록에서 "현재 단계가 역대 최고보다 낮은 지팡이"에 항상 붙는다(`DECISIONS.md` 1.15). 금 간 원. 옆에 "역대 최고 +N"을 F-12로
Sheet with 1 cells, left to right:
  1. scar — final 24x24 px
```

## 3.4 리그 휘장

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.4절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-L1 — 리그 휘장(작은 것)
- 파일 3장, 기획 크기: 24×24, 상태·프레임: 3종
- 붙일 공통: 1-A
```
Asset I-L1: 리그 휘장(작은 것). Design note (Korean, follow it exactly): 머리 위 닉네임 판 왼쪽, 목록 줄
Sheet with 3 cells, left to right:
  1. league beast — final 24x24 px
  2. league heir — final 24x24 px
  3. league sprout — final 24x24 px
```
### I-L2 — 리그 휘장(보통)
- 파일 3장, 기획 크기: 48×48, 상태·프레임: 3종
- 붙일 공통: 1-A
```
Asset I-L2: 리그 휘장(보통). Design note (Korean, follow it exactly): 살펴보기, 결투 시작 화면, 결과
Sheet with 3 cells, left to right:
  1. league beast — final 48x48 px
  2. league heir — final 48x48 px
  3. league sprout — final 48x48 px
```
### I-L3 — 리그 휘장(큰 것)
- 파일 3장, 기획 크기: 128×128, 상태·프레임: 3종
- 붙일 공통: 1-A
```
Asset I-L3: 리그 휘장(큰 것). Design note (Korean, follow it exactly): 리그 화면, 자동 매치 화면
Sheet with 3 cells, left to right:
  1. league beast — final 128x128 px
  2. league heir — final 128x128 px
  3. league sprout — final 128x128 px
```
### I-L4 — 휘장 덧테(리그 결투 점수 상위 10%의 휘장 꾸미기)
- 파일 9장, 기획 크기: 24×24, 48×48, 128×128, 상태·프레임: 3리그 × 3크기
- 붙일 공통: 1-A
```
Asset I-L4: 휘장 덧테(리그 결투 점수 상위 10%의 휘장 꾸미기). Design note (Korean, follow it exactly): 휘장 둘레의 잎 장식. 시즌마다 색만 바꾼다
Sheet with 9 cells, left to right:
  1. league laurel beast — final 128x128 px
  2. league laurel beast — final 24x24 px
  3. league laurel beast — final 48x48 px
  4. league laurel heir — final 128x128 px
  5. league laurel heir — final 24x24 px
  6. league laurel heir — final 48x48 px
  7. league laurel sprout — final 128x128 px
  8. league laurel sprout — final 24x24 px
  9. league laurel sprout — final 48x48 px
```
### I-L5 — 시즌 기념 휘장(참가 보상)
- 파일 2장, 기획 크기: 24×24, 48×48, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset I-L5: 시즌 기념 휘장(참가 보상). Design note (Korean, follow it exactly): 시즌 번호는 글자로 적는다. 시즌마다 색만 바꾼다
Sheet with 2 cells, left to right:
  1. season participation — final 24x24 px
  2. season participation — final 48x48 px
```
### I-L6 — 승급 화살
- 파일 1장, 기획 크기: 32×32, 상태·프레임: 2프레임
- 붙일 공통: 1-A
```
Asset I-L6: 승급 화살. Design note (Korean, follow it exactly): "위 리그로 올라갈 수 있다" 안내, 승급 선택 단추
Sheet with 1 cells, left to right:
  1. promotion — final 32x32 px, 2 animation frames (draw each frame as its own cell)
```
### I-L7 — 순위 표식(1위 후계자, 2~10위 수제자)
- 파일 2장, 기획 크기: 32×32, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset I-L7: 순위 표식(1위 후계자, 2~10위 수제자). Design note (Korean, follow it exactly): 순위표 줄 맨 앞. 후계자 = 흔들의자 + 금, 수제자 = 망토 깃
Sheet with 2 cells, left to right:
  1. disciple — final 32x32 px
  2. heir — final 32x32 px
```
### I-L8 — 순위 숫자 메달(1, 2, 3)
- 파일 3장, 기획 크기: 32×32, 상태·프레임: 3
- 붙일 공통: 1-A
```
Asset I-L8: 순위 숫자 메달(1, 2, 3). Design note (Korean, follow it exactly): 모든 순위표의 1~3위
Sheet with 3 cells, left to right:
  1. place 1 — final 32x32 px
  2. place 2 — final 32x32 px
  3. place 3 — final 32x32 px
```
### I-L9 — 순위 변동(오름, 내림, 그대로)
- 파일 3장, 기획 크기: 16×16, 상태·프레임: 3
- 붙일 공통: 1-A
```
Asset I-L9: 순위 변동(오름, 내림, 그대로). Design note (Korean, follow it exactly): 오름과 내림은 모양(▲ ▼)으로 구분하고 색은 분필 흰색·회청
Sheet with 3 cells, left to right:
  1. rank down — final 16x16 px
  2. rank flat — final 16x16 px
  3. rank up — final 16x16 px
```

## 3.5 비기 아이콘 15종

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.5절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-T1 — 비기 아이콘(보통)
- 파일 15장, 기획 크기: 48×48, 상태·프레임: 15종
- 붙일 공통: 1-A
```
Asset I-T1: 비기 아이콘(보통). Design note (Korean, follow it exactly): 트라이 그림의 마디, 해금 후보 카드, 연습 화면
Sheet with 15 cells, left to right:
  1. afterimage — final 48x48 px
  2. bloom — final 48x48 px
  3. bolt dash — final 48x48 px
  4. burst — final 48x48 px
  5. copy circle — final 48x48 px
  6. fan — final 48x48 px
  7. ice screen — final 48x48 px
  8. kiln — final 48x48 px
  9. octastar — final 48x48 px
  10. recoil — final 48x48 px
  11. shadow hand — final 48x48 px
  12. spear — final 48x48 px
  13. thunder spear — final 48x48 px
  14. vent — final 48x48 px
  15. wings — final 48x48 px
```
### I-T2 — 비기 아이콘(작은 것)
- 파일 15장, 기획 크기: 24×24, 상태·프레임: 15종
- 붙일 공통: 1-A
```
Asset I-T2: 비기 아이콘(작은 것). Design note (Korean, follow it exactly): HUD의 "지금 K = 기술명(비용)" 줄 앞, 결과의 사용 횟수 줄
Sheet with 15 cells, left to right:
  1. afterimage — final 24x24 px
  2. bloom — final 24x24 px
  3. bolt dash — final 24x24 px
  4. burst — final 24x24 px
  5. copy circle — final 24x24 px
  6. fan — final 24x24 px
  7. ice screen — final 24x24 px
  8. kiln — final 24x24 px
  9. octastar — final 24x24 px
  10. recoil — final 24x24 px
  11. shadow hand — final 24x24 px
  12. spear — final 24x24 px
  13. thunder spear — final 24x24 px
  14. vent — final 24x24 px
  15. wings — final 24x24 px
```
### I-T3 — 난이도 점
- 파일 2장, 기획 크기: 8×8, 상태·프레임: 빔, 참
- 붙일 공통: 1-A
```
Asset I-T3: 난이도 점. Design note (Korean, follow it exactly): 난이도 1~5를 점 다섯 개로
Sheet with 2 cells, left to right:
  1. difficulty empty — final 8x8 px
  2. difficulty filled — final 8x8 px
```
### I-T4 — 결정 비용 표시
- 파일 2장, 기획 크기: 16×16, 상태·프레임: 결정 1개, 빚 1개
- 붙일 공통: 1-A
```
Asset I-T4: 결정 비용 표시. Design note (Korean, follow it exactly): 기술명 옆 "◆×2". 큰 결정 그림(24×24)은 `effects.md` 20.3절
Sheet with 2 cells, left to right:
  1. debt — final 16x16 px
  2. diamond — final 16x16 px
```

## 3.6 변이 아이콘

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.6절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### K-80 — 변이 칸
- 파일 3장, 기획 크기: 48×48, 상태·프레임: 빔, 참, 밀려날 차례
- 붙일 공통: 1-A
```
Asset K-80: 변이 칸. Design note (Korean, follow it exactly): 지팡이마다 2칸(초롱의 지팡이는 3칸). "밀려날 차례"는 가장 오래된 변이에 붙는 점선 테
Sheet with 3 cells, left to right:
  1. mutation slot empty — final 48x48 px
  2. mutation slot filled — final 48x48 px
  3. mutation slot next to replace — final 48x48 px
```
### K-81 — 변이 이득·대가 줄 머리
- 파일 2장, 기획 크기: 16×16, 상태·프레임: 이득(위 세모), 대가(아래 세모)
- 붙일 공통: 1-A
```
Asset K-81: 변이 이득·대가 줄 머리. Design note (Korean, follow it exactly): 변이 설명에서 이득 줄과 대가 줄 앞
Sheet with 2 cells, left to right:
  1. mutation cost — final 16x16 px
  2. mutation gain — final 16x16 px
```

## 3.7 재료, 부적, 그 밖의 물건

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.7절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-M1 — 강화석(하급, 중급, 상급)
- 파일 3장, 기획 크기: 48×48, 상태·프레임: 3종
- 붙일 공통: 1-A
```
Asset I-M1: 강화석(하급, 중급, 상급). Design note (Korean, follow it exactly): -
Sheet with 3 cells, left to right:
  1. stone high — final 48x48 px
  2. stone low — final 48x48 px
  3. stone mid — final 48x48 px
```
### I-M2 — 강화석(작은 것)
- 파일 3장, 기획 크기: 24×24, 상태·프레임: 3종
- 붙일 공통: 1-A
```
Asset I-M2: 강화석(작은 것). Design note (Korean, follow it exactly): 강화 화면 위 띠의 보유량, 비용 줄, 채팅 이음
Sheet with 3 cells, left to right:
  1. stone high — final 24x24 px
  2. stone low — final 24x24 px
  3. stone mid — final 24x24 px
```
### I-M3 — 하락 방지 부적
- 파일 2장, 기획 크기: 48×48, 24×24, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset I-M3: 하락 방지 부적. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. safeguard charm — final 24x24 px
  2. safeguard charm — final 48x48 px
```
### I-M4 — 재료 꾸러미, 까마귀 보따리
- 파일 2장, 기획 크기: 48×48, 상태·프레임: 2종
- 붙일 공통: 1-A
```
Asset I-M4: 재료 꾸러미, 까마귀 보따리. Design note (Korean, follow it exactly): 정산 화면의 목록용. 여는 연출은 `monsters.md` 9.5절
Sheet with 2 cells, left to right:
  1. crow bundle — final 48x48 px
  2. material bundle — final 48x48 px
```
### I-M5 — 시즌 꾸미기 조각
- 파일 2장, 기획 크기: 48×48, 24×24, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset I-M5: 시즌 꾸미기 조각. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. season fabric — final 24x24 px
  2. season fabric — final 48x48 px
```
### I-M6 — 지팡이 상자
- 파일 1장, 기획 크기: 48×48, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset I-M6: 지팡이 상자. Design note (Korean, follow it exactly): 속성 문양(I-E1)을 겹친다
Sheet with 1 cells, left to right:
  1. wand box — final 48x48 px
```
### I-M7 — 꾸미기 분류 아이콘
- 파일 12장, 기획 크기: 32×32, 상태·프레임: 머리 장식, 망토·옷 무늬, 발자국, 앉기 동작, 지팡이 겉모습, 발사 궤적, 처치 효과, 강화 의식 겉모습, 감정표현, 테두리, 휘장, 칭호
- 붙일 공통: 1-A
```
Asset I-M7: 꾸미기 분류 아이콘. Design note (Korean, follow it exactly): 옷장의 분류 탭과, 그림이 아직 없는 꾸미기의 대신 아이콘
Sheet with 12 cells, left to right:
  1. cosmetic border — final 32x32 px
  2. cosmetic cloak — final 32x32 px
  3. cosmetic crest — final 32x32 px
  4. cosmetic emote — final 32x32 px
  5. cosmetic footprints — final 32x32 px
  6. cosmetic headwear — final 32x32 px
  7. cosmetic kill effect — final 32x32 px
  8. cosmetic ritual — final 32x32 px
  9. cosmetic seat — final 32x32 px
  10. cosmetic title — final 32x32 px
  11. cosmetic trail — final 32x32 px
  12. cosmetic wand — final 32x32 px
```
### I-M9 — 서고 확장, 닉네임 바꾸기
- 파일 2장, 기획 크기: 48×48, 상태·프레임: 2종
- 붙일 공통: 1-A
```
Asset I-M9: 서고 확장, 닉네임 바꾸기. Design note (Korean, follow it exactly): 골드 상점의 품목 카드용
Sheet with 2 cells, left to right:
  1. library expansion — final 48x48 px
  2. rename — final 48x48 px
```

## 3.8 재화

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.8절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-C1 — 재화 아이콘(작은 것)
- 파일 4장, 기획 크기: 16×16, 상태·프레임: 골드, 원정 지갑, 시즌 주화, 별빛
- 붙일 공통: 1-A
```
Asset I-C1: 재화 아이콘(작은 것). Design note (Korean, follow it exactly): 글 줄 안, 값 옆
Sheet with 4 cells, left to right:
  1. expedition purse — final 16x16 px
  2. gold — final 16x16 px
  3. season coin — final 16x16 px
  4. starlight — final 16x16 px
```
### I-C2 — 재화 아이콘(보통)
- 파일 4장, 기획 크기: 24×24, 상태·프레임: 4종
- 붙일 공통: 1-A
```
Asset I-C2: 재화 아이콘(보통). Design note (Korean, follow it exactly): 화면 위 띠의 잔액, HUD
Sheet with 4 cells, left to right:
  1. expedition purse — final 24x24 px
  2. gold — final 24x24 px
  3. season coin — final 24x24 px
  4. starlight — final 24x24 px
```
### I-C3 — 재화 아이콘(큰 것)
- 파일 4장, 기획 크기: 48×48, 상태·프레임: 4종
- 붙일 공통: 1-A
```
Asset I-C3: 재화 아이콘(큰 것). Design note (Korean, follow it exactly): 보상 목록, 상점
Sheet with 4 cells, left to right:
  1. expedition purse — final 48x48 px
  2. gold — final 48x48 px
  3. season coin — final 48x48 px
  4. starlight — final 48x48 px
```

## 3.9 능력치와 패시브

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.9절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-S1 — 능력치 아이콘
- 파일 11장, 기획 크기: 24×24, 상태·프레임: 체력, 이동 속도, 자세 속도, 결정 상한, 결정 1개당 콤보, 냉각, 임계 시작, 잠금 시간, 스쳐 피하기 감지, 연결 슬롯, 피격 시 결정 손실
- 붙일 공통: 1-A
```
Asset I-S1: 능력치 아이콘. Design note (Korean, follow it exactly): -
Sheet with 11 cells, left to right:
  1. combo per crystal — final 24x24 px
  2. cooling — final 24x24 px
  3. crystal cap — final 24x24 px
  4. crystal loss — final 24x24 px
  5. graze — final 24x24 px
  6. health — final 24x24 px
  7. heat threshold — final 24x24 px
  8. link slots — final 24x24 px
  9. lock time — final 24x24 px
  10. move speed — final 24x24 px
  11. stance speed — final 24x24 px
```
### I-S2 — 패시브 아이콘
- 파일 5장, 기획 크기: 48×48, 상태·프레임: 끓어넘침, 피뢰침 체질, 밑줄, 살얼음, 그림자 계약
- 붙일 공통: 1-A
```
Asset I-S2: 패시브 아이콘. Design note (Korean, follow it exactly): 캐릭터 속성색
Sheet with 5 cells, left to right:
  1. lightning rod — final 48x48 px
  2. overflow — final 48x48 px
  3. shadow contract — final 48x48 px
  4. thin ice — final 48x48 px
  5. underline — final 48x48 px
```
### I-S3 — 지팡이 특성 아이콘
- 파일 5장, 기획 크기: 48×48, 상태·프레임: 임계 구간 80부터, 변이 3개, 깊이 감쇠 0.92, 피격 시 결정 유지, 외상
- 붙일 공통: 1-A
```
Asset I-S3: 지팡이 특성 아이콘. Design note (Korean, follow it exactly): 그림의 뜻만 담는다. 숫자는 글자로 적는다
Sheet with 5 cells, left to right:
  1. depth decay — final 48x48 px
  2. heat at 80 — final 48x48 px
  3. keep crystal — final 48x48 px
  4. on credit — final 48x48 px
  5. three mutations — final 48x48 px
```
### I-S4 — 능력치 막대
- 파일 2장, 기획 크기: 9조각, 원본 16×8, 상태·프레임: 바탕, 채움
- 붙일 공통: 1-A + 1-B
```
Asset I-S4: 능력치 막대. Design note (Korean, follow it exactly): 다섯 캐릭터 중 최소~최대 사이의 위치. 기준값 자리에 눈금
Sheet with 2 cells, left to right:
  1. stat fill — final 16x8 px, 9-slice corners 4/2/4/2 px
  2. stat rail — final 16x8 px, 9-slice corners 4/2/4/2 px
```
### I-S5 — 아이템 레벨(IL) 표식
- 파일 1장, 기획 크기: 24×24, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset I-S5: 아이템 레벨(IL) 표식. Design note (Korean, follow it exactly): 장착 IL과 보유 IL 숫자 앞
Sheet with 1 cells, left to right:
  1. item level — final 24x24 px
```

## 3.10 메뉴와 기능 아이콘

> 아이콘은 칸마다 무엇을 그릴지가 `docs/ui-assets.md` 3.10절의 표(예: "블록 | 속성 | 그림")에 있다. 그 표의 줄을 프롬프트 끝에 그대로 붙인다.

### I-F1 — 메뉴 아이콘
- 파일 16장, 기획 크기: 32×32, 상태·프레임: 원정, 결투, 광장, 강화, 조립과 서고, 연습, 기록, 설정, 옷장, 상점, 우편함, 순위표, 시즌, 친구, 장터, 나가기
- 붙일 공통: 1-A
```
Asset I-F1: 메뉴 아이콘. Design note (Korean, follow it exactly): 메인 메뉴와 광장 메뉴(Esc)
Sheet with 16 cells, left to right:
  1. duel — final 32x32 px
  2. enhance — final 32x32 px
  3. exit — final 32x32 px
  4. expedition — final 32x32 px
  5. friends — final 32x32 px
  6. library — final 32x32 px
  7. mail — final 32x32 px
  8. market — final 32x32 px
  9. ranking — final 32x32 px
  10. record — final 32x32 px
  11. season — final 32x32 px
  12. settings — final 32x32 px
  13. shop — final 32x32 px
  14. square — final 32x32 px
  15. training — final 32x32 px
  16. wardrobe — final 32x32 px
```
### I-F2 — 기능 아이콘
- 파일 21장, 기획 크기: 24×24, 상태·프레임: 찾기, 정렬, 거르기, 새로 고침, 뒤로, 더하기, 빼기, 알림(i), 경고 세모, 물음표, 눈(공개), 눈 가림(비공개), 시계, 모래시계, 재생(다시 보기), 일시정지, 관전, 복사, 휴지통, 연필(쓰기), 고정 핀
- 붙일 공통: 1-A
```
Asset I-F2: 기능 아이콘. Design note (Korean, follow it exactly): 16×16의 흔적 가림 아이콘은 `effects.md` 20.1절
Sheet with 21 cells, left to right:
  1. back — final 24x24 px
  2. clock — final 24x24 px
  3. copy — final 24x24 px
  4. eye — final 24x24 px
  5. eye hidden — final 24x24 px
  6. filter — final 24x24 px
  7. hourglass — final 24x24 px
  8. info — final 24x24 px
  9. minus — final 24x24 px
  10. pause — final 24x24 px
  11. pencil — final 24x24 px
  12. pin — final 24x24 px
  13. play — final 24x24 px
  14. plus — final 24x24 px
  15. question — final 24x24 px
  16. refresh — final 24x24 px
  17. search — final 24x24 px
  18. sort — final 24x24 px
  19. spectate — final 24x24 px
  20. trash — final 24x24 px
  21. warning — final 24x24 px
```
### I-F3 — 사람 관계 아이콘
- 파일 10장, 기획 크기: 24×24, 상태·프레임: 귓속말, 친구 신청, 친구, 차단, 신고, 거래 신청, 결투 도전, 따라가기, 살펴보기, 결투 초대
- 붙일 공통: 1-A
```
Asset I-F3: 사람 관계 아이콘. Design note (Korean, follow it exactly): 상호작용 목록, 친구 줄의 단추
Sheet with 10 cells, left to right:
  1. blocked — final 24x24 px
  2. duel — final 24x24 px
  3. duel invite — final 24x24 px
  4. follow — final 24x24 px
  5. friend add — final 24x24 px
  6. inspect — final 24x24 px
  7. person — final 24x24 px
  8. report — final 24x24 px
  9. trade — final 24x24 px
  10. whisper — final 24x24 px
```
### I-F4 — 상태 아이콘(머리 위, 목록)
- 파일 8장, 기획 크기: 16×16, 상태·프레임: 접속 중, 접속 끊김, 원정 중, 결투 중, 거래 중, 자리 비움, 결투 대기 중, 관전 중
- 붙일 공통: 1-A
```
Asset I-F4: 상태 아이콘(머리 위, 목록). Design note (Korean, follow it exactly): 친구 목록과 머리 위 닉네임 판 오른쪽
Sheet with 8 cells, left to right:
  1. duel — final 16x16 px
  2. duel wait — final 16x16 px
  3. expedition — final 16x16 px
  4. idle — final 16x16 px
  5. offline — final 16x16 px
  6. online — final 16x16 px
  7. spectate — final 16x16 px
  8. trade — final 16x16 px
```
### I-F5 — 서버 상태
- 파일 3장, 기획 크기: 16×16, 상태·프레임: 연결됨, 끊김, 점검 중
- 붙일 공통: 1-A
```
Asset I-F5: 서버 상태. Design note (Korean, follow it exactly): 타이틀과 메인 메뉴의 프로필 표지 옆. 초록 점 / 끊어진 줄 / 망치
Sheet with 3 cells, left to right:
  1. broken — final 16x16 px
  2. hammer — final 16x16 px
  3. online — final 16x16 px
```
### I-F6 — 회선 표시
- 파일 5장, 기획 크기: 24×24, 상태·프레임: 유선, 무선, 지연 3단(넉넉, 빠듯, 넘침)
- 붙일 공통: 1-A
```
Asset I-F6: 회선 표시. Design note (Korean, follow it exactly): 자동 매치 화면과 결투 HUD. 3단은 막대 수(3, 2, 1)와 색(초록, 선택 노랑, 호박색)으로 함께 표시
Sheet with 5 cells, left to right:
  1. latency high — final 24x24 px
  2. latency low — final 24x24 px
  3. latency mid — final 24x24 px
  4. wired — final 24x24 px
  5. wireless — final 24x24 px
```
### I-F7 — 우편 봉투
- 파일 4장, 기획 크기: 32×32, 상태·프레임: 안 읽음, 읽음, 첨부 있음, 받음
- 붙일 공통: 1-A
```
Asset I-F7: 우편 봉투. Design note (Korean, follow it exactly): -
Sheet with 4 cells, left to right:
  1. mail — final 32x32 px
  2. mail attach — final 32x32 px
  3. mail claimed — final 32x32 px
  4. mail open — final 32x32 px
```
### I-F8 — 채팅 채널 아이콘
- 파일 7장, 기획 크기: 16×16, 상태·프레임: 주변, 광장, 거래, 귓속말, 친구, 결투방, 시스템
- 붙일 공통: 1-A
```
Asset I-F8: 채팅 채널 아이콘. Design note (Korean, follow it exactly): 채널마다 글자색도 다르다(4.20절)
Sheet with 7 cells, left to right:
  1. duel — final 16x16 px
  2. friends — final 16x16 px
  3. paper — final 16x16 px
  4. talk — final 16x16 px
  5. tent — final 16x16 px
  6. trade — final 16x16 px
  7. whisper — final 16x16 px
```
### I-F9 — 방 옵션 아이콘
- 파일 13장, 기획 크기: 16×16, 상태·프레임: 비밀번호, 관전 허용, 장비 평준화, 야생 난입, 가시밭 끔, 라운드 수, 라운드 시간, 체력 배율, 강화 상한, IL 상한, 변이 허용, 영웅 블록 상한, 바닥 색
- 붙일 공통: 1-A
```
Asset I-F9: 방 옵션 아이콘. Design note (Korean, follow it exactly): 결투 방 목록 줄에 켜진 옵션만 나란히 놓는다. 옵션의 선택지는 `online.md` 4.6절
Sheet with 13 cells, left to right:
  1. clock — final 16x16 px
  2. enhance limit — final 16x16 px
  3. expedition — final 16x16 px
  4. eye — final 16x16 px
  5. floor — final 16x16 px
  6. gear limit — final 16x16 px
  7. heart — final 16x16 px
  8. hero limit — final 16x16 px
  9. il limit — final 16x16 px
  10. lock — final 16x16 px
  11. rounds — final 16x16 px
  12. spark — final 16x16 px
  13. thorns — final 16x16 px
```
### I-F10 — 감정표현 아이콘
- 파일 8장, 기획 크기: 48×48, 상태·프레임: 8종
- 붙일 공통: 1-A
```
Asset I-F10: 감정표현 아이콘. Design note (Korean, follow it exactly): 감정표현 바퀴. 8종은 `online.md` 5.1절의 목록(인사, 박수, 도발, 절, 앉기, 춤, 지팡이 들어 올리기, 좌절)
Sheet with 8 cells, left to right:
  1. bow — final 48x48 px
  2. clap — final 48x48 px
  3. dance — final 48x48 px
  4. despair — final 48x48 px
  5. greet — final 48x48 px
  6. raise wand — final 48x48 px
  7. sit — final 48x48 px
  8. taunt — final 48x48 px
```
### I-F11 — 설정 분류 아이콘
- 파일 6장, 기획 크기: 32×32, 상태·프레임: 화면, 소리, 조작, 전투 보기, 대화와 공개, 계정
- 붙일 공통: 1-A
```
Asset I-F11: 설정 분류 아이콘. Design note (Korean, follow it exactly): 설정의 세로 분류 탭
Sheet with 6 cells, left to right:
  1. audio — final 32x32 px
  2. display — final 32x32 px
  3. duel — final 32x32 px
  4. keyboard — final 32x32 px
  5. person — final 32x32 px
  6. whisper — final 32x32 px
```
### I-F12 — 프로필 표지
- 파일 2장, 기획 크기: 160×40, 상태·프레임: 오프라인 프로필, 서버 프로필
- 붙일 공통: 1-A
```
Asset I-F12: 프로필 표지. Design note (Korean, follow it exactly): 타이틀과 메인 메뉴에 항상 보인다(`online.md` 8.2절). 오프라인 = 접힌 쪽지, 서버 = 까마귀가 앉은 쪽지
Sheet with 2 cells, left to right:
  1. profile offline — final 160x40 px
  2. profile server — final 160x40 px
```
### I-F13 — 기록 딱지
- 파일 4장, 기획 크기: 96×32, 상태·프레임: 최고 기록 갱신, 검증 대기, 검증 통과, 마감 임박
- 붙일 공통: 1-A
```
Asset I-F13: 기록 딱지. Design note (Korean, follow it exactly): 결과와 순위표
Sheet with 4 cells, left to right:
  1. deadline — final 96x32 px
  2. pending — final 96x32 px
  3. record — final 96x32 px
  4. verified — final 96x32 px
```

## 4.2 타이틀

### S-0201 — 타이틀 배경
- 파일 1장, 기획 크기: 1920×1080, 상태·프레임: 1 + 모닥불 겹그림 4프레임(128×128)
- 붙일 공통: 1-A
```
Asset S-0201: 타이틀 배경. Design note (Korean, follow it exactly): 다섯 제자의 뒷모습은 `world-characters.md` 3.1절의 머리 실루엣으로 구분되게 그린다. 그믐이 가운데
Sheet with 1 cells, left to right:
  1. campfire overlay — final 128x128 px, 4 animation frames (draw each frame as its own cell)
```
### S-0202 — 제목 그림
- 파일 1장, 기획 크기: 960×320, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-0202: 제목 그림. Design note (Korean, follow it exactly): -
Sheet with 1 cells, left to right:
  1. title korean — final 960x320 px
```
### S-0203 — 게임 아이콘(실행 파일용)
- 파일 4장, 기획 크기: 1024×1024, 256×256, 64×64, 32×32, 상태·프레임: 4크기
- 붙일 공통: 1-D
```
Full scene, final 1024x1024. Subject (Korean design note, follow it exactly): 게임 아이콘(실행 파일용) — 그믐의 후드와 보라 결정. 작은 크기는 따로 그린다.
Also deliver these separate parts: launcher icon — final 1024x1024 px; launcher icon — final 256x256 px; launcher icon — final 32x32 px; launcher icon — final 64x64 px
```

## 4.3 프로필 고르기, 로그인, 가입

### S-0301 — 프로필 카드 그림
- 파일 2장, 기획 크기: 496×240, 상태·프레임: 오프라인(혼자 앉은 모닥불), 서버(여럿이 앉은 모닥불)
- 붙일 공통: 1-A
```
Asset S-0301: 프로필 카드 그림. Design note (Korean, follow it exactly): 카드 위쪽에 놓는다
Sheet with 2 cells, left to right:
  1. offline card — final 496x240 px
  2. server card — final 496x240 px
```
### S-0302 — 비밀번호 가림 점, 보기 단추
- 파일 3장, 기획 크기: 16×16, 24×24, 상태·프레임: 점 1, 눈 2상태
- 붙일 공통: 1-A
```
Asset S-0302: 비밀번호 가림 점, 보기 단추. Design note (Korean, follow it exactly): -
Sheet with 3 cells, left to right:
  1. password dot — final 16x16 px
  2. password eye — final 24x24 px
  3. password eye hidden — final 24x24 px
```

## 4.4 오프라인 프로필 안내

### S-0401 — 묶음 상자
- 파일 1장, 기획 크기: 9조각, 원본 48×48, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset S-0401: 묶음 상자. Design note (Korean, follow it exactly): 점선 테의 쪽지. 왼쪽 위에 앉은 까마귀 빈자리(횃대만 있다)
Sheet with 1 cells, left to right:
  1. server features group — final 48x48 px, 9-slice corners 16/16/16/16 px
```

## 4.5 메인 메뉴

### S-0501 — 메인 메뉴 배경
- 파일 2장, 기획 크기: 1920×1080, 상태·프레임: 낮, 해질녘, 밤(색 교체 3벌은 코드) + 모닥불 4프레임(128×128)
- 붙일 공통: 1-D
```
Full scene, final 1920x1080. Subject (Korean design note, follow it exactly): 메인 메뉴 배경 — 기기 시각에 따라 색이 바뀐다.
Also deliver these separate parts: camp background — final 1920x1080 px; campfire — final 128x128 px, 4 animation frames (draw each frame as its own cell)
```
### S-0502 — 모드 카드 그림
- 파일 3장, 기획 크기: 368×240, 상태·프레임: 원정(덤불 틈 너머의 한뎃벌), 결투(돌마당의 분필 선과 마주 선 두 사람), 광장(첫모닥 야영지의 큰 모닥불)
- 붙일 공통: 1-A
```
Asset S-0502: 모드 카드 그림. Design note (Korean, follow it exactly): -
Sheet with 3 cells, left to right:
  1. mode duel — final 368x240 px
  2. mode expedition — final 368x240 px
  3. mode square — final 368x240 px
```
### S-0503 — 캐릭터 받침(발밑 흙더미)
- 파일 1장, 기획 크기: 320×96, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-0503: 캐릭터 받침(발밑 흙더미). Design note (Korean, follow it exactly): 가운데 캐릭터 아래
Sheet with 1 cells, left to right:
  1. dirt pedestal — final 320x96 px
```

## 4.6 원정 준비: 캐릭터와 지팡이 고르기, 출발 조립, 시작 웨이브

### S-0601 — 캐릭터 카드 틀
- 파일 2장, 기획 크기: 336×376, 상태·프레임: 기본, 고름
- 붙일 공통: 1-A
```
Asset S-0601: 캐릭터 카드 틀. Design note (Korean, follow it exactly): 고른 카드는 아랫변에 속성색 8px 줄
Sheet with 2 cells, left to right:
  1. character card normal — final 336x376 px
  2. character card selected — final 336x376 px
```
### S-0602 — 지팡이 받침대
- 파일 1장, 기획 크기: 224×48, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-0602: 지팡이 받침대. Design note (Korean, follow it exactly): 지팡이 큰 그림 아래
Sheet with 1 cells, left to right:
  1. wand pedestal — final 224x48 px
```

## 4.8 전투 HUD (무한 웨이브)

### S-0801 — 웨이브 진행 바
- 파일 2장, 기획 크기: 9조각, 원본 24×12, 상태·프레임: 바탕, 채움
- 붙일 공통: 1-A + 1-B
```
Asset S-0801: 웨이브 진행 바. Design note (Korean, follow it exactly): 채움은 분필 흰색. 보스 웨이브에는 숨긴다(보스 체력 바가 대신한다)
Sheet with 2 cells, left to right:
  1. wave fill — final 24x12 px, 9-slice corners 6/3/6/3 px
  2. wave rail — final 24x12 px, 9-slice corners 6/3/6/3 px
```
### S-0802 — 초상 테
- 파일 2장, 기획 크기: 72×72, 상태·프레임: 기본, 피격(흰 테 1프레임이 아니라 0.2초 밝아짐)
- 붙일 공통: 1-A
```
Asset S-0802: 초상 테. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. portrait normal — final 72x72 px
  2. portrait hit — final 72x72 px
```
### S-0803 — 보스 체력 바
- 파일 4장, 기획 크기: 9조각, 원본 32×16 + 단계 눈금 4×16 + 격노 불씨 16×16(2프레임), 상태·프레임: 바탕, 채움, 눈금, 불씨 2
- 붙일 공통: 1-A + 1-B
```
Asset S-0803: 보스 체력 바. Design note (Korean, follow it exactly): 최종 격노의 빨강 테는 색 입힘
Sheet with 4 cells, left to right:
  1. boss health fill — final 32x16 px, 9-slice corners 8/4/8/4 px
  2. boss health rail — final 32x16 px, 9-slice corners 8/4/8/4 px
  3. boss health tick — final 4x16 px
  4. enrage ember — final 16x16 px, 2 animation frames (draw each frame as its own cell)
```
### S-0804 — 점수, 처치 수 아이콘
- 파일 2장, 기획 크기: 24×24, 상태·프레임: 2종
- 붙일 공통: 1-A
```
Asset S-0804: 점수, 처치 수 아이콘. Design note (Korean, follow it exactly): 별 조각 / 꺾인 뿔
Sheet with 2 cells, left to right:
  1. kills — final 24x24 px
  2. score — final 24x24 px
```
### S-0805 — 과열 잠금 남은 시간 딱지
- 파일 1장, 기획 크기: 64×24, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-0805: 과열 잠금 남은 시간 딱지. Design note (Korean, follow it exactly): 한 줄 안내의 머리
Sheet with 1 cells, left to right:
  1. heat lock — final 64x24 px
```

## 4.9 결투: 시작 화면, HUD, 라운드 사이, 관전

### S-0901 — 결투 시작 배경
- 파일 1장, 기획 크기: 1920×1080, 상태·프레임: 1
- 붙일 공통: 1-D
```
Full scene, final 1920x1080. Subject (Korean design note, follow it exactly): 결투 시작 배경 — 회청 `#2C3442` 돌 포장 + 분필 선.
```
### S-0902 — "대" 글씨
- 파일 1장, 기획 크기: 256×160, 상태·프레임: 3프레임(분필로 쓰이는 순서)
- 붙일 공통: 1-A
```
Asset S-0902: "대" 글씨. Design note (Korean, follow it exactly): -
Sheet with 1 cells, left to right:
  1. versus hangul — final 256x160 px, 3 animation frames (draw each frame as its own cell)
```
### S-0903 — 결투 체력 바
- 파일 4장, 기획 크기: 9조각, 원본 32×32 + 눈금 4×32, 상태·프레임: 바탕, 채움, 방금 깎인 부분(밝은 회청), 눈금
- 붙일 공통: 1-A + 1-B
```
Asset S-0903: 결투 체력 바. Design note (Korean, follow it exactly): 채움은 분필 흰색. 빨강을 쓰지 않는다
Sheet with 4 cells, left to right:
  1. duel health fill — final 32x32 px, 9-slice corners 8/8/8/8 px
  2. duel health rail — final 32x32 px, 9-slice corners 8/8/8/8 px
  3. duel health recent — final 32x32 px, 9-slice corners 8/8/8/8 px
  4. duel health tick — final 4x32 px
```
### S-0904 — 라운드 점
- 파일 4장, 기획 크기: 16×16, 상태·프레임: 빔, 내가 이김, 상대가 이김, 무승부
- 붙일 공통: 1-A
```
Asset S-0904: 라운드 점. Design note (Korean, follow it exactly): 내 점 = 찬 원, 상대 점 = 찬 마름모
Sheet with 4 cells, left to right:
  1. round draw — final 16x16 px
  2. round empty — final 16x16 px
  3. round opponent — final 16x16 px
  4. round player — final 16x16 px
```
### S-0905 — 가시밭 경고
- 파일 1장, 기획 크기: 24×24, 상태·프레임: 2프레임
- 붙일 공통: 1-A
```
Asset S-0905: 가시밭 경고. Design note (Korean, follow it exactly): -
Sheet with 1 cells, left to right:
  1. thorn warning — final 24x24 px, 2 animation frames (draw each frame as its own cell)
```
### S-0906 — 입력 버려짐 아이콘
- 파일 1장, 기획 크기: 24×24, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-0906: 입력 버려짐 아이콘. Design note (Korean, follow it exactly): 끊어진 줄과 떨어지는 키 마개
Sheet with 1 cells, left to right:
  1. dropped input — final 24x24 px
```
### S-0907 — 문구 바퀴
- 파일 2장, 기획 크기: 360×360, 상태·프레임: 바탕 1 + 조각 강조 1
- 붙일 공통: 1-A
```
Asset S-0907: 문구 바퀴. Design note (Korean, follow it exactly): 8조각. 감정표현 바퀴(S-2003)와 같은 뼈대, 다른 바탕
Sheet with 2 cells, left to right:
  1. phrase highlight — final 360x360 px
  2. phrase wheel — final 360x360 px
```

## 4.10 전리품 카드 고르기

### S-1001 — 전리품 카드 바탕
- 파일 2장, 기획 크기: 240×336, 상태·프레임: 앞면, 뒷면
- 붙일 공통: 1-A
```
Asset S-1001: 전리품 카드 바탕. Design note (Korean, follow it exactly): 누런 쪽지. 등급 테(K-68)를 겹친다
Sheet with 2 cells, left to right:
  1. loot back — final 240x336 px
  2. loot front — final 240x336 px
```
### S-1002 — 카드 뒤집힘
- 파일 1장, 기획 크기: 240×336, 상태·프레임: 3프레임
- 붙일 공통: 1-A
```
Asset S-1002: 카드 뒤집힘. Design note (Korean, follow it exactly): 뜰 때 한 장씩 0.1초 간격
Sheet with 1 cells, left to right:
  1. loot flip — final 240x336 px, 3 animation frames (draw each frame as its own cell)
```

## 4.11 야영 화면과 조립 화면 (컴파일 트리 미리보기)

### S-1101 — 트리 마디 틀
- 파일 4장, 기획 크기: 32×32, 상태·프레임: 투사체(둥근), 제어(육각), 변형(깎은 네모), 스코프 주인(굵은 테)
- 붙일 공통: 1-A
```
Asset S-1101: 트리 마디 틀. Design note (Korean, follow it exactly): 3.1절의 종류 바탕과 같은 모양
Sheet with 4 cells, left to right:
  1. tree node control — final 32x32 px
  2. tree node modifier — final 32x32 px
  3. tree node projectile — final 32x32 px
  4. tree node scope — final 32x32 px
```
### S-1102 — 스코프 묶음 선
- 파일 3장, 기획 크기: 8×16(이어 붙임), 머리 8×8, 끝 8×8, 상태·프레임: 3
- 붙일 공통: 1-A
```
Asset S-1102: 스코프 묶음 선. Design note (Korean, follow it exactly): 깊이 1~3은 밝기로 구분(분필 흰색 100 / 70 / 50%)
Sheet with 3 cells, left to right:
  1. scope head — final 8x8 px
  2. scope line — final 8x16 px
  3. scope tail — final 8x8 px
```
### S-1103 — 분열 가지
- 파일 2장, 기획 크기: 96×24, 상태·프레임: 2갈래, 3갈래
- 붙일 공통: 1-A
```
Asset S-1103: 분열 가지. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. split 2 — final 96x24 px
  2. split 3 — final 96x24 px
```
### S-1104 — 생략 마디(점선)
- 파일 1장, 기획 크기: 32×32, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-1104: 생략 마디(점선). Design note (Korean, follow it exactly): -
Sheet with 1 cells, left to right:
  1. omitted node — final 32x32 px
```
### S-1105 — 마디 이음 선
- 파일 2장, 기획 크기: 4×16(이어 붙임), 꺾임 16×16, 상태·프레임: 2
- 붙일 공통: 1-A
```
Asset S-1105: 마디 이음 선. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. tree link corner — final 16x16 px
  2. tree link line — final 4x16 px
```
### S-1106 — 실행 순서 번호 딱지
- 파일 1장, 기획 크기: 16×16, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-1106: 실행 순서 번호 딱지. Design note (Korean, follow it exactly): 마디 왼쪽 위에 1, 2, 3 …(F-12)
Sheet with 1 cells, left to right:
  1. execution number — final 16x16 px
```
### S-1107 — 수치 아이콘(발사당 열, 임계 사격 횟수, DPS)
- 파일 3장, 기획 크기: 24×24, 상태·프레임: 3
- 붙일 공통: 1-A
```
Asset S-1107: 수치 아이콘(발사당 열, 임계 사격 횟수, DPS). Design note (Korean, follow it exactly): -
Sheet with 3 cells, left to right:
  1. dps — final 24x24 px
  2. heat — final 24x24 px
  3. threshold — final 24x24 px
```
### S-1108 — 서고 칸
- 파일 2장, 기획 크기: 96×112, 상태·프레임: 기본, 0개(가진 적 있음)
- 붙일 공통: 1-A
```
Asset S-1108: 서고 칸. Design note (Korean, follow it exactly): 메인 메뉴의 서고 탭: 아이콘 48 + 이름 + 수량. 16종이 8×2로 놓인다. 가진 적 없는 블록은 칸 자체가 없다
Sheet with 2 cells, left to right:
  1. library slot normal — final 96x112 px
  2. library slot empty — final 96x112 px
```

## 4.12 강화 화면

### S-1201 — 결과 띠
- 파일 3장, 기획 크기: 9조각, 원본 16×24, 상태·프레임: 바탕, 구간 채움(색 입힘), 구간 경계
- 붙일 공통: 1-A + 1-B
```
Asset S-1201: 결과 띠. Design note (Korean, follow it exactly): 대성공(금) · 성공(속성색) · 변이(연두) · 하락 또는 하락 없음(회색). 구간마다 무늬도 다르다(민무늬 / 빗금 / 점 / 격자)는 코드로 입힌다
Sheet with 3 cells, left to right:
  1. odds boundary — final 16x24 px, 9-slice corners 4/4/4/4 px
  2. odds fill — final 16x24 px, 9-slice corners 4/4/4/4 px
  3. odds rail — final 16x24 px, 9-slice corners 4/4/4/4 px
```
### S-1202 — 확률 내역 줄 머리
- 파일 4장, 기획 크기: 16×16, 상태·프레임: 기본, 제물, 같은 속성, 기운
- 붙일 공통: 1-A
```
Asset S-1202: 확률 내역 줄 머리. Design note (Korean, follow it exactly): -
Sheet with 4 cells, left to right:
  1. odds base — final 16x16 px
  2. odds momentum — final 16x16 px
  3. odds sacrifice — final 16x16 px
  4. odds same element — final 16x16 px
```
### S-1203 — 새기기 누적 눈금
- 파일 2장, 기획 크기: 16×16, 상태·프레임: 빔, 참
- 붙일 공통: 1-A
```
Asset S-1203: 새기기 누적 눈금. Design note (Korean, follow it exactly): 실패할 때마다 한 칸씩 찬다
Sheet with 2 cells, left to right:
  1. engraving tick empty — final 16x16 px
  2. engraving tick filled — final 16x16 px
```
### S-1204 — 목표 단계 깃발
- 파일 1장, 기획 크기: 16×24, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-1204: 목표 단계 깃발. Design note (Korean, follow it exactly): 기댓값 표와 이어서 강화의 목표
Sheet with 1 cells, left to right:
  1. enhance goal — final 16x24 px
```

## 4.13 비기 트라이 그림 (비기 탭, 해금)

### S-1301 — 트라이 마디
- 파일 6장, 기획 크기: 64×64, 점 마디 24×24, 상태·프레임: 해금, 잠김, 후보, 지금 위치(2프레임), 점 마디 2
- 붙일 공통: 1-A
```
Asset S-1301: 트라이 마디. Design note (Korean, follow it exactly): 고유 비기 마디는 테에 ★. 고유 비기는 처음부터 쓸 수 있으므로(DECISIONS 1.23) 새 프로필의 첫 화면에서도 "해금" 상태로 그린다. "잠김"과 "후보"는 공용 비기에만 쓰인다
Sheet with 6 cells, left to right:
  1. trie dot on — final 24x24 px
  2. trie dot off — final 24x24 px
  3. trie node candidate — final 64x64 px
  4. trie node current — final 64x64 px, 2 animation frames (draw each frame as its own cell)
  5. trie node locked — final 64x64 px
  6. trie node unlocked — final 64x64 px
```
### S-1302 — 트라이 가지
- 파일 4장, 기획 크기: 16×16(가로, 세로 이어 붙임), 꺾임 16×16, 상태·프레임: 꺼짐, 밝아짐
- 붙일 공통: 1-A + 1-C
```
Asset S-1302: 트라이 가지. Design note (Korean, follow it exactly): -
Sheet with 4 cells, left to right:
  1. trie corner on — final 16x16 px
  2. trie corner off — final 16x16 px
  3. trie line on — final 16x16 px
  4. trie line off — final 16x16 px
```
### S-1303 — 해금 후보 카드
- 파일 1장, 기획 크기: 320×320, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-1303: 해금 후보 카드. Design note (Korean, follow it exactly): 아이콘 2배(96×96), 이름, 커맨드, 효과
Sheet with 1 cells, left to right:
  1. unlock candidate — final 320x320 px
```

## 4.14 허수아비 연습

### S-1401 — 입력 간격 막대
- 파일 3장, 기획 크기: 9조각 16×16 + 표식 8×16, 상태·프레임: 막대, 표식(안, 밖)
- 붙일 공통: 1-A + 1-B
```
Asset S-1401: 입력 간격 막대. Design note (Korean, follow it exactly): "밖"은 호박색
Sheet with 3 cells, left to right:
  1. timing marker late — final 8x16 px
  2. timing marker valid — final 8x16 px
  3. timing rail — final 16x16 px, 9-slice corners 4/4/4/4 px
```
### S-1402 — 저스트 게이지
- 파일 3장, 기획 크기: 192×24, 상태·프레임: 바탕, 창 구간, 표식
- 붙일 공통: 1-A
```
Asset S-1402: 저스트 게이지. Design note (Korean, follow it exactly): 성공의 흰 별은 `effects.md` 20.4절
Sheet with 3 cells, left to right:
  1. just marker — final 192x24 px
  2. just rail — final 192x24 px
  3. just window — final 192x24 px
```
### S-1403 — 무한 표시
- 파일 1장, 기획 크기: 24×16, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-1403: 무한 표시. Design note (Korean, follow it exactly): -
Sheet with 1 cells, left to right:
  1. infinity — final 24x16 px
```

## 4.16 정산과 결과 (무한 웨이브)

### S-1601 — 귀환 가방 그림
- 파일 2장, 기획 크기: 760×120, 상태·프레임: 닫힘, 열림
- 붙일 공통: 1-A
```
Asset S-1601: 귀환 가방 그림. Design note (Korean, follow it exactly): 칸 위에 얹는 가방 아가리. 칸은 K-60
Sheet with 2 cells, left to right:
  1. return bag closed — final 760x120 px
  2. return bag open — final 760x120 px
```
### S-1602 — 정산 배경
- 파일 1장, 기획 크기: 1920×1080, 상태·프레임: 1
- 붙일 공통: 1-D
```
Full scene, final 1920x1080. Subject (Korean design note, follow it exactly): 정산 배경 — 새벽의 한뎃벌. 꺼져 가는 모닥불.
```
### S-1603 — 쓰러진 캐릭터 받침
- 파일 1장, 기획 크기: 384×96, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-1603: 쓰러진 캐릭터 받침. Design note (Korean, follow it exactly): 캐릭터의 사망 마지막 장(지팡이만 남는 장)을 놓는 자리
Sheet with 1 cells, left to right:
  1. fallen pedestal — final 384x96 px
```

## 4.17 결투 결과

### S-1701 — 승패 띠
- 파일 3장, 기획 크기: 1200×120, 상태·프레임: 승리(속성색 입힘), 패배(회청), 무승부·무효(분필 흰색)
- 붙일 공통: 1-D
```
Full scene, final 1200x120. Subject (Korean design note, follow it exactly): 승패 띠 — 금색은 쓰지 않는다.
Also deliver these separate parts: result banner defeat — final 1200x120 px; result banner draw — final 1200x120 px; result banner victory — final 1200x120 px
```

## 4.19 설정과 키 재설정

### S-1901 — 옵션 미리보기 그림(작은 전장)
- 파일 7장, 기획 크기: 424×240, 상태·프레임: 바탕 1 + 겹그림 6(예고 원, 적 탄, 내 탄, 줄무늬 채움, 흔들림 표시, 번쩍 표시)
- 붙일 공통: 1-A
```
Asset S-1901: 옵션 미리보기 그림(작은 전장). Design note (Korean, follow it exactly): 옵션 값에 따라 겹그림을 바꿔 보여 준다
Sheet with 7 cells, left to right:
  1. options enemy bolt — final 424x240 px
  2. options field — final 424x240 px
  3. options flash — final 424x240 px
  4. options hatching — final 424x240 px
  5. options player bolt — final 424x240 px
  6. options shake — final 424x240 px
  7. options telegraph — final 424x240 px
```
### S-1902 — 키보드 바탕
- 파일 1장, 기획 크기: 1200×280, 상태·프레임: 1
- 붙일 공통: 1-D
```
Full scene, final 1200x280. Subject (Korean design note, follow it exactly): 키보드 바탕 — 키 자리는 K-50으로 얹는다.
```
### S-1903 — 칭호 줄
- 파일 3장, 기획 크기: 9조각, 원본 32×32, 상태·프레임: 보통, 금(+13), 후계자
- 붙일 공통: 1-A + 1-B
```
Asset S-1903: 칭호 줄. Design note (Korean, follow it exactly): 칭호 고르기 목록의 줄 바탕. 머리 위의 칭호 띠(S-2002)와 같은 색 규칙
Sheet with 3 cells, left to right:
  1. title row heir — final 32x32 px, 9-slice corners 8/8/8/8 px
  2. title row ordinary — final 32x32 px, 9-slice corners 8/8/8/8 px
  3. title row thirteen — final 32x32 px, 9-slice corners 8/8/8/8 px
```

## 4.20 광장: 머리 위 표시, 대화, 상호작용, 메뉴

### S-2001 — 닉네임 판
- 파일 3장, 기획 크기: 9조각, 원본 24×20, 상태·프레임: 남, 나, 친구
- 붙일 공통: 1-A + 1-B
```
Asset S-2001: 닉네임 판. Design note (Korean, follow it exactly): 먹색 70% 바탕. 나 = 분필 흰색 테
Sheet with 3 cells, left to right:
  1. nameplate friend — final 24x20 px, 9-slice corners 8/5/8/5 px
  2. nameplate other — final 24x20 px, 9-slice corners 8/5/8/5 px
  3. nameplate self — final 24x20 px, 9-slice corners 8/5/8/5 px
```
### S-2002 — 칭호 띠(머리 위)
- 파일 4장, 기획 크기: 9조각, 원본 32×16, 상태·프레임: 보통, 금(+13), 후계자, 수제자
- 붙일 공통: 1-A + 1-B
```
Asset S-2002: 칭호 띠(머리 위). Design note (Korean, follow it exactly): 의식용 큰 칭호 띠(720×96)는 `effects.md` 20.1절
Sheet with 4 cells, left to right:
  1. title ribbon disciple — final 32x16 px, 9-slice corners 8/4/8/4 px
  2. title ribbon heir — final 32x16 px, 9-slice corners 8/4/8/4 px
  3. title ribbon ordinary — final 32x16 px, 9-slice corners 8/4/8/4 px
  4. title ribbon thirteen — final 32x16 px, 9-slice corners 8/4/8/4 px
```
### S-2003 — 감정표현 바퀴
- 파일 2장, 기획 크기: 360×360, 상태·프레임: 바탕 1 + 조각 강조 1
- 붙일 공통: 1-A
```
Asset S-2003: 감정표현 바퀴. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. emote highlight — final 360x360 px
  2. emote wheel — final 360x360 px
```
### S-2004 — 말줄임표
- 파일 1장, 기획 크기: 24×16, 상태·프레임: 3프레임
- 붙일 공통: 1-A
```
Asset S-2004: 말줄임표. Design note (Korean, follow it exactly): -
Sheet with 1 cells, left to right:
  1. typing ellipsis — final 24x16 px, 3 animation frames (draw each frame as its own cell)
```
### S-2005 — 말풍선
- 파일 4장, 기획 크기: 9조각, 원본 32×32 + 꼬리 12×8, 상태·프레임: 주변, 시스템(느루의 말)
- 붙일 공통: 1-A + 1-B
```
Asset S-2005: 말풍선. Design note (Korean, follow it exactly): 쪽지 색
Sheet with 4 cells, left to right:
  1. bubble nearby — final 32x32 px, 9-slice corners 8/8/8/8 px
  2. bubble system — final 32x32 px, 9-slice corners 8/8/8/8 px
  3. bubble tail nearby — final 12x8 px
  4. bubble tail system — final 12x8 px
```
### S-2006 — 물건 이름표
- 파일 1장, 기획 크기: 9조각, 원본 24×24, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset S-2006: 물건 이름표. Design note (Korean, follow it exactly): 등급색을 입힌다
Sheet with 1 cells, left to right:
  1. item nameplate — final 24x24 px, 9-slice corners 8/8/8/8 px
```
### S-2007 — 시설 팻말
- 파일 1장, 기획 크기: 9조각, 원본 48×32, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset S-2007: 시설 팻말. Design note (Korean, follow it exactly): 나무 팻말
Sheet with 1 cells, left to right:
  1. facility sign — final 48x32 px, 9-slice corners 8/8/8/8 px
```

## 4.21 광장: 결투 게시판, 방, 도전

### S-2101 — 결투 게시판 판
- 파일 1장, 기획 크기: 9조각, 원본 96×96, 모서리 32, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset S-2101: 결투 게시판 판. Design note (Korean, follow it exactly): 쪽지가 꽂힌 나무판. 광장의 게시판 그림(`world-characters.md` 10.3절)과 같은 재질
Sheet with 1 cells, left to right:
  1. duel board — final 96x96 px, 9-slice corners 32/32/32/32 px
```
### S-2102 — 방 줄 쪽지
- 파일 2장, 기획 크기: 9조각, 원본 48×56, 상태·프레임: 기본, 가득 참
- 붙일 공통: 1-A + 1-B
```
Asset S-2102: 방 줄 쪽지. Design note (Korean, follow it exactly): 핀에 꽂힌 쪽지 한 장
Sheet with 2 cells, left to right:
  1. room note normal — final 48x56 px, 9-slice corners 12/12/12/12 px
  2. room note full — final 48x56 px, 9-slice corners 12/12/12/12 px
```
### S-2103 — 준비 표시
- 파일 2장, 기획 크기: 96×32, 상태·프레임: 준비 전, 준비 됨
- 붙일 공통: 1-A
```
Asset S-2103: 준비 표시. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. ready not ready — final 96x32 px
  2. ready ready — final 96x32 px
```
### S-2104 — 방장 표식
- 파일 1장, 기획 크기: 16×16, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-2104: 방장 표식. Design note (Korean, follow it exactly): -
Sheet with 1 cells, left to right:
  1. room leader — final 16x16 px
```
### S-2105 — 대기 중 표시
- 파일 2장, 기획 크기: 240×48, 상태·프레임: 바탕 1 + 모래시계 4프레임(24×24)
- 붙일 공통: 1-A
```
Asset S-2105: 대기 중 표시. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. waiting banner — final 240x48 px
  2. waiting hourglass — final 24x24 px, 4 animation frames (draw each frame as its own cell)
```

## 4.23 광장: 직접 거래, 장터, 상점

### S-2301 — 거래 돗자리
- 파일 2장, 기획 크기: 640×520, 상태·프레임: 열림, 잠김
- 붙일 공통: 1-A
```
Asset S-2301: 거래 돗자리. Design note (Korean, follow it exactly): 좌판의 돗자리. 잠기면 끈이 둘린다
Sheet with 2 cells, left to right:
  1. trade mat locked — final 640x520 px
  2. trade mat open — final 640x520 px
```
### S-2302 — 거래 자물쇠
- 파일 3장, 기획 크기: 96×96, 상태·프레임: 열림, 잠김, 잠기는 중 3프레임
- 붙일 공통: 1-A
```
Asset S-2302: 거래 자물쇠. Design note (Korean, follow it exactly): -
Sheet with 3 cells, left to right:
  1. trade lock locked — final 96x96 px
  2. trade lock open — final 96x96 px
  3. trade locking — final 96x96 px, 3 animation frames (draw each frame as its own cell)
```
### S-2303 — 바뀐 칸 테
- 파일 1장, 기획 크기: 72×72, 상태·프레임: 2프레임
- 붙일 공통: 1-A
```
Asset S-2303: 바뀐 칸 테. Design note (Korean, follow it exactly): 선택 노랑이 아니라 분필 흰색 굵은 점선
Sheet with 1 cells, left to right:
  1. trade changed slot — final 72x72 px, 2 animation frames (draw each frame as its own cell)
```
### S-2304 — 시세 그림 틀
- 파일 1장, 기획 크기: 9조각, 원본 32×32, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset S-2304: 시세 그림 틀. Design note (Korean, follow it exactly): 꺾은선과 막대는 코드
Sheet with 1 cells, left to right:
  1. price history frame — final 32x32 px, 9-slice corners 8/8/8/8 px
```
### S-2305 — 상점 머리 그림
- 파일 2장, 기획 크기: 1200×96, 상태·프레임: 행상 수레, 느루의 흔들의자
- 붙일 공통: 1-D
```
Full scene, final 1200x96. Subject (Korean design note, follow it exactly): 상점 머리 그림 — .
Also deliver these separate parts: shop banner cart — final 1200x96 px; shop banner chair — final 1200x96 px
```
### S-2306 — 품목 카드 바탕
- 파일 2장, 기획 크기: 216×280, 상태·프레임: 기본, 다 팔림
- 붙일 공통: 1-A
```
Asset S-2306: 품목 카드 바탕. Design note (Korean, follow it exactly): -
Sheet with 2 cells, left to right:
  1. catalog card normal — final 216x280 px
  2. catalog card sold out — final 216x280 px
```
### S-2307 — 장터 배경
- 파일 1장, 기획 크기: 1920×1080, 상태·프레임: 1
- 붙일 공통: 1-D
```
Full scene, final 1920x1080. Subject (Korean design note, follow it exactly): 장터 배경 — 좌판 거리를 위에서 본 그림. 어둡게 깔린다.
```

## 4.24 광장: 우편함, 옷장, 느루의 흔들의자

### S-2401 — 우편함 판
- 파일 1장, 기획 크기: 9조각, 원본 96×96, 모서리 32, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset S-2401: 우편함 판. Design note (Korean, follow it exactly): 새집 지붕 모양의 윗변
Sheet with 1 cells, left to right:
  1. mailbox board — final 96x96 px, 9-slice corners 32/32/32/32 px
```
### S-2402 — 옷장 미리보기 바탕
- 파일 1장, 기획 크기: 560×880, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-2402: 옷장 미리보기 바탕. Design note (Korean, follow it exactly): 빨랫줄과 빨래집게. 가운데에 캐릭터 받침(S-0503을 같이 쓴다)
Sheet with 1 cells, left to right:
  1. wardrobe preview — final 560x880 px
```
### S-2403 — 꾸미기 칸
- 파일 2장, 기획 크기: 96×96, 상태·프레임: 기본, 입는 중
- 붙일 공통: 1-A
```
Asset S-2403: 꾸미기 칸. Design note (Korean, follow it exactly): 입는 중은 확정 체크
Sheet with 2 cells, left to right:
  1. cosmetic slot normal — final 96x96 px
  2. cosmetic slot equipped — final 96x96 px
```
### S-2404 — 대화 판(느루와 NPC)
- 파일 1장, 기획 크기: 9조각, 원본 96×96, 모서리 32, 상태·프레임: 1
- 붙일 공통: 1-A + 1-B
```
Asset S-2404: 대화 판(느루와 NPC). Design note (Korean, follow it exactly): 행상, 튜토리얼의 느루도 같이 쓴다
Sheet with 1 cells, left to right:
  1. dialogue board — final 96x96 px, 9-slice corners 32/32/32/32 px
```
### S-2406 — 후계 점수 막대
- 파일 3장, 기획 크기: 9조각, 원본 16×24, 상태·프레임: 바탕, E 채움, P 채움
- 붙일 공통: 1-A + 1-B
```
Asset S-2406: 후계 점수 막대. Design note (Korean, follow it exactly): E와 P는 무늬(빗금 / 점)로도 구분
Sheet with 3 cells, left to right:
  1. heir score expedition — final 16x24 px, 9-slice corners 4/6/4/6 px
  2. heir score pvp — final 16x24 px, 9-slice corners 4/6/4/6 px
  3. heir score rail — final 16x24 px, 9-slice corners 4/6/4/6 px
```

## 4.25 리그, 시즌 순위표, 후계자 비석

### S-2501 — 리그 카드 바탕
- 파일 3장, 기획 크기: 528×640, 상태·프레임: 새싹(나무), 맹수(무쇠), 후계(금 테)
- 붙일 공통: 1-A
```
Asset S-2501: 리그 카드 바탕. Design note (Korean, follow it exactly): -
Sheet with 3 cells, left to right:
  1. league card beast — final 528x640 px
  2. league card heir — final 528x640 px
  3. league card sprout — final 528x640 px
```
### S-2502 — IL 긴 막대
- 파일 4장, 기획 크기: 9조각, 원본 16×32 + 경계 표식 8×48 + 내 위치 표식 24×32, 상태·프레임: 바탕, 채움, 경계, 내 위치
- 붙일 공통: 1-A + 1-B
```
Asset S-2502: IL 긴 막대. Design note (Korean, follow it exactly): -
Sheet with 4 cells, left to right:
  1. item level fill — final 16x32 px, 9-slice corners 4/8/4/8 px
  2. item level rail — final 16x32 px, 9-slice corners 4/8/4/8 px
  3. item level tick — final 8x48 px
  4. item level you — final 24x32 px
```
### S-2503 — 순위 줄 바탕
- 파일 4장, 기획 크기: 9조각, 원본 48×56, 상태·프레임: 기본, 짝수 줄, 나, 1~3위
- 붙일 공통: 1-A + 1-B
```
Asset S-2503: 순위 줄 바탕. Design note (Korean, follow it exactly): 1~3위는 왼쪽에 금·은회색·놋쇠 띠 8px
Sheet with 4 cells, left to right:
  1. leaderboard row normal — final 48x56 px, 9-slice corners 12/12/12/12 px
  2. leaderboard row alternate — final 48x56 px, 9-slice corners 12/12/12/12 px
  3. leaderboard row top three — final 48x56 px, 9-slice corners 12/12/12/12 px
  4. leaderboard row you — final 48x56 px, 9-slice corners 12/12/12/12 px
```
### S-2504 — 비석 판
- 파일 1장, 기획 크기: 720×880, 상태·프레임: 1
- 붙일 공통: 1-A
```
Asset S-2504: 비석 판. Design note (Korean, follow it exactly): 광장의 비석(96×128)을 키운 것이 아니라 따로 그린다
Sheet with 1 cells, left to right:
  1. heir monument — final 720x880 px
```
### S-2505 — 새긴 이름 줄
- 파일 2장, 기획 크기: 9조각, 원본 48×72, 상태·프레임: 보통, 가장 최근
- 붙일 공통: 1-A + 1-B
```
Asset S-2505: 새긴 이름 줄. Design note (Korean, follow it exactly): 가장 최근 줄은 새김 자국이 밝다
Sheet with 2 cells, left to right:
  1. monument inscription latest — final 48x72 px, 9-slice corners 12/12/12/12 px
  2. monument inscription ordinary — final 48x72 px, 9-slice corners 12/12/12/12 px
```
### S-2506 — 순위표 배경
- 파일 1장, 기획 크기: 1920×1080, 상태·프레임: 1
- 붙일 공통: 1-D
```
Full scene, final 1920x1080. Subject (Korean design note, follow it exactly): 순위표 배경 — 느루의 탑 밑동의 벽.
```

## 4.26 그 밖의 공용 화면

### S-2601 — 불러오는 화면 배경
- 파일 1장, 기획 크기: 1920×1080, 상태·프레임: 1
- 붙일 공통: 1-D
```
Full scene, final 1920x1080. Subject (Korean design note, follow it exactly): 불러오는 화면 배경 — 덤불 틈으로 난 길. 어둡다.
```
### S-2602 — 마우스 커서
- 파일 4장, 기획 크기: 32×32, 상태·프레임: 기본, 누를 수 있음, 끌어 옮김, 글 입력
- 붙일 공통: 1-A
```
Asset S-2602: 마우스 커서. Design note (Korean, follow it exactly): 분필 조각 모양. 전투 중에는 숨긴다
Sheet with 4 cells, left to right:
  1. cursor normal — final 32x32 px
  2. cursor click — final 32x32 px
  3. cursor drag — final 32x32 px
  4. cursor text — final 32x32 px
```
### S-2603 — 복수 추적기의 거울 표식
- 파일 3장, 기획 크기: 32×32, 상태·프레임: 복수 중(금 없는 타원 거울), 흠집 50% 이상(금 간 거울), 복수 완료(깨진 거울)
- 붙일 공통: 1-A
```
Asset S-2603: 복수 추적기의 거울 표식. Design note (Korean, follow it exactly): 은회색과 분필 흰색. 위험색과 금색을 쓰지 않는다. 16×16 자리에는 코드로 줄여 쓴다. 추적기의 나머지는 전부 공용 부품이다
Sheet with 3 cells, left to right:
  1. revenge mirror cracked — final 32x32 px
  2. revenge mirror shattered — final 32x32 px
  3. revenge mirror whole — final 32x32 px
```
