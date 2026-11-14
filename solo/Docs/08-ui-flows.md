# 08. 솔로 화면 흐름과 UI — 구현 요약서

게임: "엄청엄청 위대한 마법사 키우기". 대상: 새 순정 Unity 프로젝트(`D:\algorithm_game\solo`)의 **솔로(오프라인 프로필) 화면 전부 + 강화 화면**.
이 문서만 보고 화면을 코딩할 수 있게 원문의 규칙·상태 기계·수치·키·전이·이름 열쇠를 옮겼다. 그림·소리는 만들지 않고 회색 도형 + 이벤트 이름으로만 둔다.

- 원문: `docs/spec/13-ui-flows.md`(이하 `S13 n절`), `docs/ui-assets.md`(이하 `UA n절`), `data/names/names.ko.json`, `data/presentation/*.json`.
- 우선순위(CLAUDE.md 1절): `docs/DECISIONS.md` > `docs/spec/*` > `docs/dev/CONTRACTS.md` > … > 기획 문서(`ui-assets.md` 포함). 어긋남은 16절에 모았다.
- 표시: [확정] 원문 확정 / [제안] 원문 시작값(QA에서 조정) / [임시] 원문이 정하지 않아 spec이 넣은 기본값 / **미정** 값이 없음.
- 시간: 화면 타이머는 **고정 60Hz 화면 틱(UiTick)** 기본 [임시](S13 0.2, U-58). `N t` = 60틱 기준 틱 수. 순정 Unity에서는 `Time.unscaledDeltaTime` 누적으로 60Hz 틱을 세면 된다. 전투 틱도 초당 60틱.
- 확률은 만분율 정수(bp). 금액은 64비트 정수, 화면에는 세 자리 쉼표(48,200).
- 1칸 = 64px, 기준 화면 1920×1080, 그림 1px = 화면 1px(UA 1.1).

---

## 0. 솔로 범위

### 0.1 솔로에 들어가는 화면 (`ScreenId`, S13 1.1)

| 묶음 | 화면 |
|---|---|
| 시작 | `FirstRunPrompt`, `Title`, `ProfileSelect`(오프라인 카드만 동작), `OfflineGuide` |
| 메뉴 | `MainMenu`, `ExpeditionPrep`(캐릭터 선택 겸함), `Records`, `TitlePicker`, `Settings`, `KeyRebind`, `Wardrobe`, `Shop`(골드 상점) |
| 판 | `Loading`, `BattleHud`, `LootPick`, `Camp`, `ArtTrie`, `DummyPractice`, `Pause`, `ControlsView`, `Settlement` |
| 강화 | `Enhance`, `WandPicker`, `WandReleaseConfirm`, `MutationRemove`, `SlotEngrave`, `EnhanceTutorial` |
| 공용 | `ConfirmDialog`, `Tooltip`, `SaveRestored`, `ShopBuyConfirm` |

### 0.2 솔로 제외 (한 줄씩)

- 로그인·가입·이메일·비밀번호 찾기·접속 대기·서버 닿지 않음·새 기기 안내(S13 3절): 솔로 제외.
- 결투(사람 상대) HUD·결과·게시판·방(S13 7.2, 11, 15.4): 솔로 제외. 단 **봇 상대 결투 연습은 오프라인에서도 된다**고 원문이 적음 → 진입은 메인 메뉴 "연습" 화면 안 [임시](C-11, U-43). 결투 판정 자체는 다른 요약서 담당.
- 광장 전부(겹침 화면, 채팅, 상호작용, 거래, 장터, 우편함, 친구, 순위표, 시즌, 리그, 비석, 느루의 흔들의자)(S13 15절): 솔로 제외.
- 서버 전체 알림(S13 1.6): 오프라인은 **내보내지도 받지도 않는다**. `server_enhance_notice` 등 서버 전용 설정 줄은 숨김 [임시](U-26).
- 검증 대기, 원정 정리(종료 요약 미제출 판), 갚을 것, 거래 잠금, 이메일 미확인, 결제/별빛: 솔로 제외. 별빛·유료 탭은 **어떤 화면에도 나오지 않는다**(S13 14.6).
- 판 제출 대기 토스트, 접속 끊김, 운영 공지, 제재 안내: 솔로 제외.

### 0.3 오프라인 프로필에서 되는 것 (S13 1.2)

무한 웨이브, 조립, 서고, **강화 화면(슬롯 새기기·변이 제거 포함)**, 상점(같은 값, 같은 주간 한도 — **기기 시계 기준**), 허수아비 연습, 봇 상대 결투 연습, 옷장, 기록(기기 안 개인 기록), 주간 도전(첫 만남 뒤, 주간 보상 없음), 허상 첫 만남·복수 보상·복수 추적기.

- 오프라인 프로필은 **게임을 처음 켤 때 자동 생성**. 계정·이메일 없음. 시작 상태: 캐릭터 5명(`char.1`~`char.5`), 각자 +0 지팡이 1자루(`wand.1`~`wand.5`), 코어에 마력탄(`block.magic_bolt`) 1개씩, 골드·재료 0 (S13 2.1, TV-001).
- 두 프로필이 같이 쓰는 것은 **설정과 키 배치뿐**. 기기에 저장(S13 1.2).
- 오프라인 진행은 서버로 옮길 수 없다. 옮기는 기능을 만들지 않는다(DECISIONS, CLAUDE.md 2.5).
- 오프라인 강화: 같은 화면, 같은 확률, 같은 연출. 결과가 기기에서 바로 나오므로 **결과 늦음 대기(`WaitingLate`)에 들어가지 않는다**(S13 9.1, TV-106). 결과 카드의 "서버 전체에 알렸다" 줄만 빠진다(S13 9.7).
- 저장 파일: 직전 3세대 보관. 깨지면 직전 세대로 되돌리고 `SaveRestored` 창(S13 1.5). 설정 > 계정(오프라인)에는 "저장 파일 위치"와 [저장 파일 되돌리기]만(S13 13.3).

---

## 1. 공통 기반

### 1.1 화면 관리자 (S13 1.1)

```
enum ScreenLayer { Fullscreen, Panel, Modal, Toast, HudOverlay }
IScreen: Id, Layer, BlocksInput(Modal=true), OnEnter(args), OnExit(), OnBack()  // Esc, true면 소비
ScreenStack: Push(id,args), Replace(id,args), Pop() /*들어온 곳으로*/, ReturnTarget
```

- 진입점이 여럿인 화면(강화, 원정 준비, 허수아비 연습, 상점)은 **같은 화면 하나**이고 `ScreenArgs.entry`로 진입점만 다르다 [확정].
- 전체 화면 메뉴 뼈대(UA 4 머리말): **위 띠 80px**(제목, K-05) + **아래 조작 안내 띠 48px**(K-06, "J 고르기 · K 뒤로 · Esc 나가기") + 그 사이 952px 내용.
- 창(Modal)이 뜨면 뒤 화면을 먹색(`#12131C`) 60%로 덮는다(K-10).

### 1.2 메뉴 공통 키 (UA 2.1 K-06, S13 9.1)

| 키 | 동작 |
|---|---|
| 방향키 | 커서(칸) 이동. [임시] 이동 키 배치(W/A/S/D 또는 대체 배치의 방향키)도 같이 받는다 — 원문은 "방향키"만 적음(**미정**) |
| J | 고르기 / 확정 / (강화 화면) 넣기·강화 |
| K | 뒤로 / (강화·야영) 빼기 |
| Esc | 나가기 = `OnBack()` → 들어온 곳으로(`ReturnTarget`) |
| Q / E | 강화 화면에서 지팡이 바꾸기 |
| 마우스 | 메뉴는 마우스 가능. 키보드만으로도 전부 된다 |

### 1.3 누를 수 있는 것의 상태 규약 (S13 1.3, UA 1.3)

모든 버튼·탭·칸·목록 줄·카드가 같다.

| 상태 | 언제 | 모습 / 규칙 |
|---|---|---|
| 기본 | — | 그린 그대로 |
| 올림 Hover | 마우스가 위에 있을 때(**마우스 전용**) | 테 한 단계 밝게(`#3A3F5A`) + 안쪽 1px 밝은 선. **선택 괄호 없음** |
| 선택 Cursor | 키보드 커서가 있는 것, 또는 지금 고른 것(마우스 클릭 포함) | 노란 모서리 괄호 |
| 누름 | 누르는 동안 | 2px 아래로, 아랫변 그늘 사라짐 |
| 켜짐 On | 탭·토글·거르기 켜짐 | 바탕 `#3A3F5A` + 아랫변 분필 흰색 4px 줄 |
| 꺼짐 Disabled | 지금 못 씀 | 채도 0, 밝기 60%, 글자 회청. **커서는 갈 수 있다**. 괄호 회청 + 아래 한 줄에 이유. 눌러도 아무 일 없고 이유를 다시 보여 준다 [임시](U-15) |

**선택 괄호**(Brackets):
- 대상 네 모서리 바깥 ㄱ자 4개, 선 4px, 색 `#FFE94A`, 바깥 먹색 2px, 대상에서 4px 바깥.
- 크기: `L = max(w,h)`; `L ≤ 64` 작은 것(팔 12px), `65~240` 보통(팔 20), 그보다 크면 큰 것(팔 28) [임시: 긴 변](U-16, TV-243: 64×64→Small, 65×40→Medium, 240×336→Large).
- 커서 괄호는 **0.5초(30 t) 주기로 2px 벌어졌다 오므라든다(2프레임, 프레임당 15 t)**. 번쩍이지 않는다.
- 확정(이미 고른 것)은 괄호가 멈추고 오른쪽 위에 확정 체크. **커서와 확정이 한 화면에 동시에** 있을 수 있다(캐릭터 선택).
- 괄호는 화면 그림 층에만. 전장 안의 적·탄·지팡이에는 그리지 않는다.

**입력 장치 모드**(`InputModeTracker`): 키 하나라도 누르면 Keyboard(모든 Hover 해제, 커서 대상에 괄호). 마우스가 움직이면(delta≠0) Mouse(괄호는 "고른 것"에만). Hover 표시 = Mouse 모드 && 포인터 위(TV-241).

### 1.4 글자 표시 (S13 1.4, UA 1.4)

| 규칙 | 값 |
|---|---|
| 넘침 | 줄이지 않고 끝을 "…"로 자른다. 전체 글은 툴팁 |
| 폭 계산 | 한글·기호 1자 = 글자 크기, 숫자·영문 1자 = 글자 크기의 절반 (`"48,200"` 16px → 48px, TV-239) |
| 숫자 | 세 자리 쉼표, 고정폭 숫자 |
| 이름 칸 | 캐릭터 카드·초상 옆·칭호 띠·알림의 이름 칸은 **한글 8자 폭**(16px 글꼴 → 128px). 9자면 "…"(TV-238) |
| 글자 크기 | 12 / 16 / 24 / 32 / 48 / 64px 여섯 가지만(줄 높이 16/24/32/40/56/72). 12px 미만 없음 |
| 쓰임 | 12 꼬리표·수량·HUD "지금 K" 줄 / 16 본문·목록·툴팁 / 24 버튼·탭·판 제목 / 32 화면 제목·캐릭터 이름·"N일째 웨이브 M" / 48 정산 기록 줄 / 64 큰 숫자 |
| 전장 위 글자 | 먹색 1px 외곽선 |

### 1.5 색 규칙 (UA 1.2)

| 이름 | 색 | 쓰는 곳 |
|---|---|---|
| 먹 | `#12131C` | 외곽선. 순수 검정 금지 |
| 나무 어두움 / 나무 / 나무 밝음 | `#1F2130` / `#2A2D40` / `#3A3F5A` | 화면 바탕 / 기본 판 / 올림 테 |
| 회청 | `#8E9AAE` | 보조 글자, 꺼진 괄호 |
| 분필 흰색 | `#F2EDE0` | 본문 글자 |
| 쪽지 / 그늘 / 쪽지 위 글자 | `#E8DDBF` / `#C9BB96` / `#2B2A33` | 결과 카드, 확인 창 |
| 선택 노랑 | `#FFE94A` | 선택 괄호 전용 |
| 금색 | `#FFD24A` | 골드, 영웅 등급, +13 (값진 것만) |
| 희귀 파랑 / 일반 회색 | `#4F8CFF` / `#9AA0AE` | 등급 테 |
| 알림 호박색 | `#F2A33A` | 경고·오류·"조심 버튼" 테 |
| 좋음 초록 | `#6FD36B` | 좋음 표시 |
| 연두 | `#B8F04A` | **변이 전용** |
| 청록 | `#4FE3C1` | **비전 속성 전용** |

- **위험색(빨강 `#FF3B30`, 자홍 `#FF2E88`; 색상각 330~15도·채도·명도 70% 이상)은 메뉴·창·플레이어 쪽에 쓰지 않는다.** HUD 허용 세 곳: 피격 시 화면 가장자리, 체력 1칸 귀퉁이, 보스 최종 격노 테.
- 조심 버튼(포기, 삭제, 떠나보내기)은 빨강이 아니라 **호박색 테 + 경고 삼각**(K-24).
- 색으로만 구분하지 않는다(등급=테 모양, 속성=문양, 상태=아이콘).
- 속성색(UA 3.2): 화염 `#FF8A1F`, 번개 `#FFF36B`, 비전 `#4FE3C1`, 냉기 `#9FDCFF`, 암흑 `#8A5CFF`, 무 `#8E9AAE`.

### 1.6 공용 창 (S13 1.5, UA 4.26)

| 창 | 내용 | 버튼 | 규칙 / 이름 열쇠 |
|---|---|---|---|
| `ConfirmDialog` (560×320) | 물음 한 줄, 설명 두 줄 | [예] [아니오] | 조심할 일은 [예]가 조심 버튼. `str.confirm_dialog.title`, `str.common.yes/no` |
| `Loading` (전체) | 한 줄 도움말, 진행률 0~1(막대 640×16) | 없음 | `str.loading.title` |
| `SaveRestored` (640×360) | 되돌린 사실 | [확인] | `str.save_restored.title`, `.message` |
| `Tooltip` (최대 폭 400) | 물건 이름(등급색), 종류·속성, 수치, 한 줄 설명, 거래 가능 여부(솔로는 생략 가능) | — | `str.tooltip.*` |

---

## 2. 솔로 화면 흐름 전체

```
Boot
 ├ 설정 파일 없음(기기 첫 실행) → FirstRunPrompt →(선택 즉시 저장)→ Title     (오프라인 프로필 자동 생성)
 └ 설정 파일 있음 → Title
Title ─J─▶ (offlineGuideSeen=false면 OfflineGuide ─[확인]─▶) MainMenu
      ─[프로필 바꾸기]─▶ ProfileSelect ─[오프라인 카드]─▶ (OfflineGuide) ▶ MainMenu
MainMenu
 ├ 모드 카드 "원정"      → ExpeditionPrep(entry=MainMenu) ─[출발 J]─▶ Loading ▶ BattleHud
 ├ 강화                  → WandPicker → Enhance(entry=MainMenu)   (Esc → MainMenu)
 ├ 조립과 서고           → Camp(mode=Library)
 ├ 연습                  → DummyPractice(entry=MainMenu) (+[봇 상대 결투 연습] [임시])
 ├ 기록                  → Records (→ TitlePicker)
 ├ 옷장                  → Wardrobe
 ├ 상점                  → Shop(kind=Gold)
 ├ 설정                  → Settings (→ KeyRebind)
 ├ 묶음 상자(서버 전용)   → OfflineGuide 다시 보기
 └ 나가기                → 게임 종료 [임시: 원문은 버튼만 적음]
BattleHud ─(Esc)─▶ Pause ─[계속]/[설정]/[조작 보기]/[판 포기]/[메인 메뉴로]
BattleHud ─(웨이브 클리어)─▶ LootPick ─▶ 전장(모닥불) ─(모닥불 옆 J)─▶ Camp(mode=Camp)
Camp ─(출발 표식 J 1초 또는 화면 [출발])─▶ BattleHud(다음 웨이브)
BattleHud ─(쓰러짐 / 판 포기)─▶ Settlement 1쪽 → 2쪽 → 3쪽
Settlement 3쪽 ─[다시 원정]→ ExpeditionPrep / [강화 화면으로]→ Enhance(entry=MainMenu, 돌아올 곳 MainMenu [임시] U-34) / [메인 메뉴]→ MainMenu
```

- 프로필 전환은 **타이틀에서만**(메인 메뉴에 수단 없음, TV-005).
- 강화 화면은 **판(원정) 중·야영 중에는 열 수 없다**. 메뉴에 없다(TV-133). 야영의 어느 모드에도 강화 탭 없음(TV-068).
- 자동 테스트 흐름(S13 0.4): 처음 실행 → 판 → 정산 → 강화 → 다시 실행.

---

## 3. 첫 실행과 타이틀

### 3.1 `FirstRunPrompt` (S13 2.2, UA 4.1)

| 항목 | 내용 |
|---|---|
| 바탕 | 나무 어두움 단색. **번쩍임 없는 화면**: 번쩍 관리기 요청을 하나도 내지 않는다(TV-002) |
| 판 | 가운데 880×480. 물음 한 줄 |
| 선택지 3개 | 각 256×160, 간격 24: `Full`(전체) / `Reduced`(줄임) / `Minimal`(최소), 각 설명 |
| 확정 | 고르면 **바로** `settings.motion_preset` 저장 → `Title`. 확인 단계 없음 |
| 커서 기본 | `Full` [임시](U-13) |
| Esc/창 닫기 | 저장하지 않는다 → 다음 실행에 다시 묻는다 [임시] |
| 상태 | `Asking → Saved` |
| 이름 열쇠 | `str.first_run_prompt.title`("번쩍임과 흔들림을 줄일까요?"), `.option.full/reduced/minimal`, `.option.*_desc` |
| 이벤트 | `ui.first_run.choice` |

### 3.2 `Title` (S13 2.3, UA 4.2)

| 요소 | 위치(px) | 규칙 |
|---|---|---|
| 배경 | 전체 | 회색 도형(그림은 S-0201, `art/production/backgrounds/title_1920x1080.png`가 있음) |
| 제목 | (480,120) 960×320 | `str.title.title` |
| 프로필 표지 | (48,968) 160×40 | **지금 프로필이 항상 보인다**. 솔로: `str.title.profile.offline` |
| [프로필 바꾸기] | (224,968) 192×40 | `str.title.change_profile` → `ProfileSelect` |
| 시작 안내 | (760,840) 400×48 | `str.title.start_hint`("J를 눌러 시작"). **2초(120 t) 주기로 밝기만** 바뀐다. 깜박이지 않는다 |
| 판 번호, 만든 이 | 오른쪽 아래, 12px | `str.title.build_version`("판 {version}"), `str.title.credits_label` |
| J | 오프라인: (처음이면 `OfflineGuide`) → `MainMenu` |
| 이벤트 | `ui.title.start_prompt_pulse`, `ui.title.start` |

### 3.3 `ProfileSelect` (S13 3.1, UA 4.3)

- 카드 2장, 각 560×640, 간격 48. 오프라인(`str.profile_select.card.offline`, `_desc`) / 서버(`.card.server`, `_desc`). 제목 `str.profile_select.title`.
- 솔로: 오프라인 카드 → (첫 진입이면 `OfflineGuide`) → `MainMenu`. 서버 카드는 솔로 제외(카드 표시 여부 **미정**: [임시] 보이되 `error.not_supported_offline` 사유로 꺼짐).

### 3.4 `OfflineGuide` (S13 4.1, UA 4.4)

- 언제: `profile.offlineGuideSeen == false`일 때 한 번(TV-028), 그리고 메인 메뉴 묶음 상자에서 언제나 다시.
- 판 800×560: 제목 `str.offline_guide.title`. 세 줄 `str.offline_guide.line.1/2/3`(① 무한 웨이브·조립·강화 화면·상점·연습이 전부 된다 ② 랭킹과 거래에 반영되지 않는다 ③ 서버로 옮길 수 없다) [확정].
- 되는 것·안 되는 것 표(736×240) 두 열: `str.offline_guide.column.can/cannot`, 되는 것 `str.offline_guide.can.endless/assembly/enhance/practice/records/shop/wardrobe`, 안 되는 것 `str.offline_guide.cannot.duel/plaza/mail/ranking/season/trade`. **안 되는 것은 흐리게 하지 않고 빈 동그라미**.
- [확인](256×64) → `offlineGuideSeen = true` → `MainMenu`.

---

## 4. 메인 메뉴 `MainMenu` (S13 4.2, UA 4.5)

### 4.1 구성

| 요소 | 위치(px) | 내용 |
|---|---|---|
| 배경 | 전체 | 가운데 지금 고른 캐릭터(64×64 ×4 = 256) + 든 지팡이(현재 단계). 기기 시각에 따라 낮/해질녘/밤 색: **06:00~16:59 낮, 17:00~19:59 해질녘, 20:00~05:59 밤** [임시](U-14) |
| 위 띠 | (0,0) 1920×80 | 프로필 표지, 칭호, 골드(`currency.gold`). 솔로는 시즌 주화·우편 점·이메일 꼬리표·갚을 것 없음 |
| 모드 카드 3장 | (48,128)부터 각 400×520, 간격 24 | 원정(`str.main_menu.mode.expedition` + `.mode.expedition_sub`), 결투(`.mode.duel`), 광장(`.mode.plaza`). 카드: 그림 368×240, 이름 32px, 설명 두 줄, 기록 한 줄 |
| 메뉴 목록 | (1344,128) 528×760 | 12줄, 줄 높이 48, 간격 16 (12×48 + 11×16 = 752) |
| 나가기 | (1680,968) 192×48 | `str.main_menu.item.exit` |

원정 카드 기록 줄: `str.main_menu.best_wave`("최고 웨이브 {n}"). 복수 중이면 아래에 한 줄 꼴 `revenge.tracker.short`(13.2절).

### 4.2 메뉴 목록 (S13 4.2)

| 순서 | id | 이름 열쇠 | 여는 화면 | 솔로 |
|---|---|---|---|---|
| 1 | `enhance` | `str.main_menu.item.enhance` | `WandPicker` → `Enhance(entry=MainMenu)` | ○ |
| 2 | `assembly_library` | `.item.assembly_library` | `Camp(mode=Library)` 탭 [조립 \| 서고] | ○ |
| 3 | `practice` | `.item.practice` | `DummyPractice(entry=MainMenu)` | ○ |
| 4 | `records` | `.item.records` | `Records` | ○ |
| 5 | `wardrobe` | `.item.wardrobe` | `Wardrobe` | ○ |
| 6 | `shop` | `.item.shop` | `Shop(kind=Gold)` | ○ |
| 7 | `settings` | `.item.settings` | `Settings` | ○ |
| 8~12 | `mailbox`,`friends`,`leaderboard`,`season`,`market` | `.item.mailbox/friends/leaderboard/season/market` | — | 묶음 상자 |

- **오프라인에서 서버 전용(결투·광장 카드, 8~12번 줄)은 흐리게 두지 않고 묶음 상자 하나에 넣고** `str.main_menu.server_only`("서버에 접속하면 열립니다") 한 줄. 묶음 상자를 고르면 `OfflineGuide`(`str.main_menu.offline_guide`) [제안](TV-026). 서버 전용 줄은 목록 아래쪽에 모은다.
- 오프라인 프로필은 부적과 +0 새 지팡이를 **메인 메뉴의 상점에서만** 산다.
- 이벤트: `ui.main_menu.mode_card_focus`, `ui.main_menu.bg_time_change`, `ui.offline.group_box_open`.

---

## 5. 원정 준비 `ExpeditionPrep` — 캐릭터 선택 (S13 5절, UA 4.6)

### 5.1 진입

| entry | 여는 곳 | 차이 |
|---|---|---|
| `MainMenu` | 원정 카드 | — |
| `Settlement` | 정산 [다시 원정] | 같은 화면 [임시] |
| `DuelEntry`(봇 연습) | 봇 상대 결투 연습 | **시작 웨이브 줄이 빠지고** [출발] 대신 "준비"(`str.expedition_prep.ready`)(TV-038) |

### 5.2 구성 (1920×1080)

| 요소 | 위치(px) | 내용 |
|---|---|---|
| 캐릭터 카드 5장 | (80,104)부터 각 336×376, 간격 20 | 큰 그림 192×192, 이름(`char.N` = "제자 N"), 칭호(지팡이 단계 칭호), 뒤에 속성 문양(64, 20% 투명). 커서 = 큰 괄호, 고른 캐릭터 = 확정 체크(+ 아랫변 속성색 8px 줄). **커서와 확정이 함께 보인다** |
| 능력치 판 | (80,504) 640×480 | 능력치 11줄(줄 높이 32: 아이콘+이름+값+막대) + 패시브·지팡이 특성 두 줄 + 고유 비기 한 줄 |
| 지팡이 판 | (744,504) 480×480 | 왼쪽 104px: 보관 지팡이 칸(80×80) 세로 줄, 한 화면 5칸, 세로 스크롤. 위에 "n자루" + 정렬 고르기. 오른쪽 376px: 고른 지팡이 큰 그림, 이름·단계·변이 칸·확장 슬롯·IL |
| 출발 조립 판 | (1248,504) 592×352 | 지팡이 회로(코어 + 연결 칸). [조립 바꾸기] → `Camp(mode=LoadoutEdit, wand)`. 경고 있으면 경고 딱지 |
| 시작 웨이브 | (1248,872) 592×48 | 단계 고르기 "◀ 웨이브 1 / 11 / 21 … ▶". 고를 수 없으면 **줄 자체가 없다** |
| 출발 | (1248,936) 592×64 | "출발 J" (주 버튼) |

능력치 11줄 열쇠(UA 3.9, names): `str.stat.health`, `.move_speed`, `.stance_speed`, `.crystal_cap`, `.combo_per_crystal`, `.cooling`, `.heat_threshold`, `.lock_time`, `.graze`, `.link_slots`, `.crystal_loss`. 줄 머리 `str.stat.passive`, `.wand_trait`, `.unique_art`. 값 이름: `passive.*`, `wand_trait.*`, `art.*`. 수치 출처는 `world-characters.md` 5.1(다른 요약서).

### 5.3 지팡이 목록 (원정 준비·강화 공용 `WandListModel`, S13 5.3)

```
WandRow(id, character, stage, bestStage, dropCount, mutationCount, extSlots 0..3, aura 0..3, il, acquiredAt, isHeld, lockState)
WandSort { StageDesc(기본), StageAsc, RecentAcquired, IlDesc }
WandFilter(강화 화면에만) { HasMutation, HasExtSlot, TradeLocked, PlusZeroOnly }   // 여러 개 = AND [임시]
SortRows: 든 지팡이를 어느 정렬에서든 맨 위 고정 ++ 나머지를 정렬 키로,
          동률 깨기 [임시] 단계 desc → IL desc → acquiredAt desc → id asc (U-50)
고른 정렬은 기기에 기억(키 wand_list.sort). 쪽으로 끊지 않는 세로 스크롤. 캐릭터마다 "n자루".
```

- TV-029: 같은 캐릭터 +5(든), +9, +7, 단계 높은 순 → [+5(든), +9, +7]. TV-030: 낮은 순 → [+5(든), +7, +9].
- 지팡이 개수 제한 없음 [확정]. 지팡이는 그 캐릭터만 쓴다.
- 들어올 때 그 캐릭터의 **든 지팡이**가 먼저 골라져 있고 "든 지팡이" 꼬리표(`str.expedition_prep.held_tag`). 다른 지팡이를 고르면 [든 지팡이로](`str.expedition_prep.set_held`)가 켜진다.
- 솔로 잠금: `OnExpedition`만 의미 있음 → 원정 준비에서 고를 수 없다.
- 정렬 이름: `str.wand_list.sort.stage_desc/stage_asc/recent/il_desc`. 개수 `str.wand_list.count`.

### 5.4 출발 조건 `CanDepart` (S13 5.4)

```
if selectedWand.loadout.core == empty → Blocked(str.expedition_prep.blocked.core_empty)
     // 코어 칸 경고 딱지, [조립 바꾸기]에 선택 괄호(커서 이동) (TV-031, TV-032: 상점에서 산 +0 지팡이도 빈 조립)
if selectedWand.lock has OnExpedition → Blocked(str.expedition_prep.blocked.on_expedition)
return Ok
[출발] 꺼짐 + 이유 한 줄. 결투(봇) 입장도 코어 검사는 같다.
```
(서버 조건 `unsettled`, `offline_suggest`는 솔로 제외.)

### 5.5 시작 웨이브 (S13 5.5)

```
AllowedSkips(bestPassed):           // bestPassed = 그 캐릭터로 "넘긴" 최고 웨이브
  list = [0]; for s = 10; s*10 <= bestPassed*7; s += 10: list.add(s)   // s ≤ best × 0.7
  첫 웨이브 = s + 1  (1, 11, 21 …)
ShowStartWaveRow = AllowedSkips.Length > 1
```

| bestPassed | AllowedSkips | 근거 |
|---|---|---|
| 34 | [0,10,20] | TV-033 |
| 14 | [0] → 줄 없음 | TV-034 |
| 15 | [0,10] | TV-035 |
| 28 / 29 | [0,10] / [0,10,20] | TV-036 |

- 건너뛴 점수(`SkippedScore`, TV-037): 10 → 8,398 / 20 → 28,859 / 30 → 64,764 / 40 → 120,409 / 50 → 195,829. 식: 일반 웨이브 `(B(w)×10 + 200) × S(w) × 0.6`, 보스 웨이브 `3200 × S(w) × 0.6`(B, S는 몬스터 명세 — 다른 요약서).
- 표시: `str.expedition_prep.start_wave.label`, `.start_wave.option`("웨이브 {wave}"), `.start_wave.score_note`("건너뛰면 점수 {score}{을/를} 받고 보상은 없다").
- 건너뛴 웨이브: 골드·블록·재료 없음, 귀환 가방 칸에도 세지 않음. 출발 상태: 체력 가득, 결정 0, 출발 조립 그대로. 건너뛴 구간의 비기 해금 차례는 출발 야영에서 차례대로 고른다 [제안].
- 주간 도전 판은 웨이브 1 출발 고정.
- 이벤트: `ui.prep.character_confirm`, `ui.prep.wand_select`, `ui.prep.held_wand_set`, `ui.prep.depart_blocked`, `ui.prep.depart`.

---

## 6. 전투 HUD `BattleHud` (S13 7.1, UA 4.8)

HUD는 **코어 상태를 읽기만** 한다. 전장 24×14칸 = 1536×896px, 화면 가운데(좌우 192, 위 104, 아래 80 여백). 화면 그림은 여백에, 전장 위에는 **캐릭터 둘레 표시와 피해 숫자만**(허수아비 방 튜토리얼 풍선만 예외). HUD는 화면 흔들림을 받지 않는다. 전투 HUD만 가장자리 16px까지 쓴다.

### 6.1 표시 항목과 위치

| 요소 | 위치(px) | 데이터 / 규칙 | 이름 열쇠 |
|---|---|---|---|
| 웨이브 진행 바 | (480,16) 960×12 | 남은 적 체력 비율 0..1000‰. **오른쪽→왼쪽으로 줄어든다**. 채움 분필 흰색. 보스 웨이브에 숨김 | — |
| 체력 | (48,40), 칸 32×32, 간격 4 | 4~8칸(캐릭터별). 8칸이면 폭 284(TV-048). 1칸 남으면 귀퉁이 위험색 허용 | — |
| 초상 | (48,80) 64×64 + 테 72×72 | 옆에 지팡이 단계 "+7"(`str.format.wand_stage`). 피격 시 0.2초 밝아짐 | — |
| 날과 웨이브 | (760,36) 400×40, 32px | "3일째 웨이브 23". 왼쪽 시간대 아이콘(32), 오른쪽 변덕 아이콘(최대 2) | `str.battle_hud.day_wave`, `tod.*`, `whim.*` |
| 경과 시간 | (912,80) 96×16 | 경과 틱(야영 시간 제외는 코어 규칙) | — |
| 허상까지 n웨이브 | (1016,80) 144×16 | 복수 중 && 웨이브 11~19 → `20 − wave` | `revenge.tracker.hud` |
| 보스 표시 | (480,8) 960×60 | 보스 웨이브에만(진행 바 자리). 윗줄 y8~40: 이름, 격노 쌓임, 약점·저항 문양. 아랫줄 y48~64: 체력 바 960×16 + 단계 눈금 | `boss.*`, `str.battle_hud.boss.rage/weak/resist/final_rage`, `revenge.tag` |
| 원정 지갑·점수·처치 | (1744,40) 160×24 세 줄, 줄 간격 28, 오른쪽 맞춤 | 원정 지갑(판 중 골드), 점수, 처치 수 | `str.battle_hud.wallet/score/kills`, `currency.expedition_purse` |
| 열 링 | 캐릭터 발밑, 반지름 44 | 임계 시작 눈금 포함 (**줄일 수 없음**) | — |
| 결정 칸 | 머리 위, 칸 24×24, 간격 2 | 4~7칸. 제자 5(그믐)의 빚 칸 = 보라 테 빈 칸 | — |
| 콤보 숫자 | 캐릭터 (+40,−72) | 옵션 `combo_position`: 캐릭터 옆(기본) / HUD(초상 옆) | — |
| 최근 입력 5개 | 발밑 (−50,+60) 100×16 | 16×16 문양 5개 + 이음줄(이어짐/끊어짐) | — |
| 지금 K 줄 | (−96,+80) 192×16, 12px | "K = 꿰뚫는 창 (◆×2)". 제자 5는 뒤에 " 외상 N" | `str.battle_hud.now_k`, `.now_k_debt` |
| 시야 화살표 | 발밑, 전장 층 | (**줄일 수 없음**) | — |
| 지팡이 회로 | (760,1016)부터 칸 40×40, 간격 4 | 코어 1 + 연결 최대 8 = 9칸(폭 392, TV-049). 실행 중인 칸이 순서대로 켜진다 | — |
| 피해 숫자 | 전장 위 | 6.2절 | — |
| 한 줄 안내 | (16,1008) 720×32 | "웨이브 클리어", "과열 잠금 3초"(앞에 남은 시간 딱지 64×24) | `str.battle_hud.notice.wave_clear`, `.notice.heat_lock` |
| 튜토리얼 풍선 | (560,200) 800×120 | 6.4절 | `str.tutorial.step.1~5` |
| 첫 만남 대사 판 | (480,920) 960×96, 24px | 6.3절 | `phantom_first.master_line` |

| 규칙 | 값 |
|---|---|
| 날 계산 | `day = (wave + 9) / 10` (정수 올림) [확정]. TV-042: 23→3, 10→1, 11→2, 1→1, 20→2 |
| 보스 웨이브 | `wave % 10 == 0` |
| 보스 웨이브 표시 | 진행 바 숨김, 보스 표시가 대신, 날·웨이브 줄은 16px 한 줄로 줄여 (760,76) 400×24로 내려간다(TV-046) |
| 최종 격노 | 보스 바의 **테만 빨강**(허용된 위험색 세 곳 중 하나) |
| 복수 꼬리표 | 복수 중인 프로필이 웨이브 20 허상을 만나면 이름 왼쪽에 "복수" |
| 첫 만남 체력 바 | 웨이브 10 첫 만남에서 50%에서 멈추지만 **눈금·자물쇠로 미리 알리지 않는다**(TV-047) |
| `HudWavesLeft` | `state==Pending && 11 ≤ wave ≤ 19 ? 20 − wave : null`. TV-043: wave14→6. TV-044: 10/19/20 → null/1/null. TV-045: 만나기 전 → null |
| 줄일 수 없는 것 | 적 예고, 적 탄, 열 링, 피격 표시, 시야 화살표 — 어떤 옵션으로도 꺼지지 않는다 [확정] |

### 6.2 피해 숫자 `DamageNumberKind` [확정]

| kind | 모양 |
|---|---|
| Normal | 흰색 |
| Critical | 1.5배, 흰색 + 검정 굵은 테 + "!". **금색 쓰지 않음**(TV-054) |
| Weak | 1.25배 + ▲ + 속성 문양(16×16) |
| Resist | 회청 0.8배 + ▼ |
| Gold | 금색 "+숫자" |

옵션: 투명도 0~100%, 표시 `All`/`CritWeakOnly`/`Off`. 동시 표시 상한 40개, 합치기 창 0.2초(`particle_budget.json` `shares`).

### 6.3 허상 첫 만남 대사 판 (S13 7.1)

쓰러짐 연출 중 스승(느루)의 한마디 한 줄 `phantom_first.master_line`("거울도 못 이기면서 위대하긴."). **초상 없음(목소리만)**. 그동안 입력 잠김, 전장 예고 없음. 쓰러짐 연출 7초는 건너뛸 수 없다(U-12). 연출 끝(4.00초 시점)에 오른쪽 위 쪽지 `revenge.tracker.first_note`("복수: 웨이브 20에서 다시 만난다")가 내려오고 **4초(240 t) 뒤** 올라간다(TV-236).

### 6.4 튜토리얼 풍선 (첫 판 전 허수아비 방, 5단계)

| 단계 | 열쇠 | 완료 조건 [임시](U-53) |
|---|---|---|
| ① | `str.tutorial.step.1` 이동과 발사 | 1칸 이상 이동 + 발사 1회 |
| ② | `.step.2` 자세 | Shift를 누른 채 1칸 이동 |
| ③ | `.step.3` 임계 사격 | 임계 사격 1회 |
| ④ | `.step.4` 반동 사격 | 반동 사격 1회 |
| ⑤ | `.step.5` 블록 끼우기 | 블록 1개 끼움 |

풍선 본문 문구의 열쇠는 이름 표에 없다(**미정**).

### 6.5 HUD 이벤트 (bindings 파일 열쇠 기준, 9절 참고)

`ui.hud.wave_progress`, `ui.hud.hp_change`, `ui.hud.hp_last_cell`, `ui.hud.portrait_hit`, `ui.hud.day_wave_change`, `ui.hud.boss_show`, `ui.hud.boss_rage_stack`, `ui.hud.boss_final_rage`, `ui.hud.boss_phase_tick`, `ui.hud.revenge_tag`, `ui.hud.revenge_waves_left`, `ui.hud.notice`, `ui.hud.overheat_lock`, `ui.hud.circuit_step`, `ui.hud.recent_input`, `ui.hud.damage_number`, `ui.hud.tutorial_step`, `ui.hud.phantom_line`.

---

## 7. 일시정지 `Pause`, 조작 보기 `ControlsView` (S13 7.3, UA 4.15)

| 항목 | 값 |
|---|---|
| 열기 | 전투 중 Esc(일시정지 키, 기본 Esc [임시]) |
| 게임 시간 | 무한 웨이브: **멈춘다**(틱을 멈추는 것은 일시정지뿐) |
| 판 | 가운데 480×560, 버튼 5개(큰 버튼): [계속] `str.pause.resume`, [설정] `.settings`, [조작 보기] `.controls`, [판 포기](조심 버튼) `.give_up`, [메인 메뉴로] `.to_menu` |
| 지금 판 요약 | 판 왼쪽 400×560: 캐릭터, 지팡이, 조립, 지금 점수, 누적 일시정지 시간 `str.pause.pause_total`. (`pause_limit_note` 순위 대상 판 안내는 솔로 제외) |
| 판 포기 | 정상 종료 = 직접 넘긴 웨이브 전부 정산. 확인 창 한 번 `str.pause.give_up_confirm` [임시] → `Settlement` |
| 메인 메뉴로 | 판 포기와 같다: 확인 창 → 정산 → 메인 메뉴 [임시](U-35) |
| 조작 보기 | 가운데 960×640, **지금 키 배치 그대로**의 조작표(키 재설정 바인딩을 읽어 그린다). `str.controls_view.title` |
| Esc(판 열린 상태) | [계속]과 같다 [임시: 원문 없음] |

---

## 8. 전리품, 야영, 조립, 비기, 연습

### 8.1 전리품 고르기 `LootPick` (S13 8.1, UA 4.10)

웨이브가 끝나면 **야영 화면보다 먼저**. 뒤를 60% 어둡게(전장이 비침).

| 때 | 카드 | 고르는 수 |
|---|---|---|
| 일반 웨이브 | 4 | 2 (TV-055) |
| 엘리트를 한 마리라도 잡은 웨이브 | 5 | 2 (TV-056) |
| 보스 처치 | 5 (1장 희귀 이상 확정, 웨이브 30부터 그 1장 영웅 확정) | 2 (TV-057) |
| 허상 첫 만남(첫 웨이브 10) | 4 (확정 칸 없음) | 2 (TV-058) |
| 웨이브 6(1일째만) | 위에 더해 영웅 1장을 그냥 받는다(**빌린 블록**: "빌림" 꼬리표, 점선 테) | — (TV-059) |

```
state LootPickState { Revealing, Choosing, OverflowDiscard, Done }
Revealing: 카드가 한 장씩 0.1초(6 t) 간격으로 뒤집혀 뜬다 (뒤집힘 3프레임)
Choosing:
  toggle(card): picked.Contains(card) ? 빼기 : (picked.Count < pickCount ? 넣기 : 무시)
  [다시 뽑기]: 원정 지갑 ≥ 30 → 지갑 −30, 같은 등급 확률로 카드 전부 새로(확정 칸은 확정 유지), picked 비움
               지갑 < 30이면 꺼짐 + "골드가 모자란다"(error.not_enough_gold) (TV-061, TV-062: 100 → 70)
  [확인] enabled ⇔ picked.Count == pickCount (TV-060: 1장 꺼짐, 2장 켜짐, 3번째 무시)
  확인 → storage.Count + pickCount > 12 ? OverflowDiscard : Done
OverflowDiscard: 버릴 블록을 골라 보관함 ≤ 12. 버린 블록은 그 자리에서 골드(일반 5, 희귀 15, 영웅 50; 빌린 블록 50) (TV-063)
Done → 전장(모닥불) → 모닥불 옆 J → Camp
```

| 요소 | 위치(px) |
|---|---|
| 제목 `str.loot_pick.title`("전리품 · {pick_count}장을 고른다") | (560,120) 800×56, 48px |
| 카드 240×336, 간격 24 | 4장 폭 1032, 5장 폭 1296(TV-064). 위→아래: 종류 꼬리표(`str.block.kind.*`), 아이콘(96×96), 이름(`block.*`), 열·피해·간격 세 줄(`str.block.heat/damage/interval`, 해당하는 것만), 메모 한 줄. 등급 테 `str.block.grade.*` |
| 커서 + 고른 카드 | 큰 괄호 + 확정 체크. 둘이 함께 보인다 |
| [다시 뽑기] `str.loot_pick.reroll` | (760,800) 400×64, 값과 함께 |
| 보관함 `str.camp.storage` | (576,896) 768×64, 12칸 |
| [확인] `str.loot_pick.confirm` | (760,984) 400×64 |
| 기타 | `str.loot_pick.pick_count`("{picked} / {max}장"), `.borrowed`, `.overflow_title`, `.overflow_gold`("버리면 {gold} 골드") |

이벤트: `ui.loot.card_flip`, `ui.loot.pick`, `ui.loot.unpick`, `ui.loot.reroll`, `ui.loot.discard`.

### 8.2 야영 `Camp` (S13 8.2, UA 4.11)

`mode`: `Camp`(판 중, 가운데 탭 [조립 | 비기]) / `Library`(메인 메뉴 "조립과 서고", 탭 [조립 | 서고]) / `LoadoutEdit`(원정 준비의 [조립 바꾸기]). **어느 모드에도 강화 탭은 없다** [확정].
제목: `str.camp.title` / `.title.library` / `.title.loadout_edit`. 탭: `str.camp.tab.assembly/arts/library`.
**야영 창이 떠 있어도 틱은 간다**(틱을 멈추는 것은 일시정지뿐, CLAUDE.md 3절). 단 야영 중에는 점수의 시간 계산이 멈춘다(시간 제한 없음).

3단 구성(왼 480 / 가운데 960 / 오른 480, 위 띠 80):

| 요소 | 위치(px) | 내용 |
|---|---|---|
| 위 띠 | (0,0) 1920×80 | `str.camp.header`("야영 · {day}일째 웨이브 {wave} 다음"), 원정 지갑, 체력 |
| 왼: 지팡이 이름·단계 | (24,96) 432×48 | |
| 왼: 지팡이 회로 | (24,160) 432×168 | 첫 줄: 코어(72) + 연결 4칸(**제자 3(갈피)는 5칸**). 둘째 줄: 확장 칸 3(열린 것만) |
| 왼: 컴파일 트리 | (24,344) 432×480 | 8.3절 |
| 왼: 경고 줄 | (24,840) 432×72 | 조립 경고 최대 3줄(TV-077). 줄을 고르면 트리의 그 마디에 괄호. 문구 `warn.*` |
| 가운데: 조립 탭 보관함 | (504,152) 912×360 | 판 중 보관함 12칸(6×2, 칸 64, 간격 16) + 고른 블록 설명 판 912×200 |
| 가운데: 조작 | (504,536) 912×64 | [끼우기 J] `str.camp.insert` [빼기 K] `.remove` [서로 바꾸기] `.swap` |
| 가운데: 비기 탭 | (504,152) 912×760 | `ArtTrie`(8.4절) — 해금 화면 겸함 |
| 가운데: 아래 버튼 | (504,936) 912×64 | [체력 회복(값)] `.heal`, [전리품 다시 뽑기(값)] `str.loot_pick.reroll`, [허수아비 연습] `.practice` |
| 오른: 수치 | (1464,96) 432×240 | 발사당 열 `str.camp.stat.heat_per_shot`, 임계 사격 가능 횟수 `.stat.crit_shots`(**0이면 경고** `str.camp.warn.crit_zero`, TV-069), 허수아비 DPS `.stat.dummy_dps`. 조립을 바꾸면 바뀐 값이 0.5초(30 t) 밝아짐(TV-070) |
| 오른: 다음 웨이브 | (1464,352) 432×400 | `str.camp.next_wave`: 시간대(`tod.*`), 틀 이름(`frame.*`), 변덕 최대 2(`whim.*`), 나올 적의 약점·저항 문양 줄, 보스 이름(보스 웨이브, `boss.*`), 확정 재료(`mat.*`). 맨 아래 복수 추적기 큰 꼴(432×96) |
| 오른: 귀환 가방 미리보기 | 오른쪽 단 [임시](C-04) | `str.camp.bag_preview`("지금 판을 끝내면 가져가는 귀환 가방 {slots}칸 · 영웅 {hero}개까지"). `slots = min(12, passed/3)`, `hero = passed/10` |
| 오른: 한 줄 소식 | (1464,768) 432×40 | 솔로 제외(서버 알림) |
| 오른: 출발 | (1464,936) 432×64 | `str.camp.depart`, 안내 `str.camp.depart_hint`("J를 1초 누르면 출발") |

| 기능 | 규칙 |
|---|---|
| 체력 회복 | 비용 `30 + 5 × 웨이브` 골드, 같은 야영에서 두 번째부터 **기본값 × 1.5(내림, 곱해 쌓지 않음)**. **원정 지갑에서만**. TV-065: 웨이브 10 → 80 / 120. TV-066: 웨이브 23 → 145 / 217 |
| 새벽 야영 | 보스를 잡은 뒤의 야영. 체력 1칸 무료 회복(`str.camp.heal_dawn`, TV-067) |
| 다시 뽑기 | 30골드(원정 지갑) |
| 허수아비 연습 | `DummyPractice(entry=Camp)` |
| 첫 만남 전 웨이브 9 뒤 야영 | 보스 이름 "스승의 허상"(`boss.mirror_phantom`)만 평소대로, 그 밖 경고 없음(TV-071) |
| 출발 | 모닥불 반대편 "출발" 표식에서 **J를 1초(60 t)** 누르면 다음 웨이브(59 t 출발 안 함, 60 t 출발, TV-072). 남은 시간 고리 표시. 화면에서 바로 출발도 된다. 야영을 건너뛰고 출발해도 된다 |
| 시간대 전환 | 야영 중 4초에 걸쳐 바뀌고 전투 중에는 바뀌지 않는다 |
| 야영 활동 목록 | 조립, 회복, 비기 선택, 허수아비 연습, 다시 뽑기 [임시](U-44, 16절 모순 M-01) |

```
HealCost(wave, healsThisCamp) = healsThisCamp == 0 ? 30 + 5*wave : (30 + 5*wave) * 3 / 2
```

**서고(Library 모드)**(S13 8.2, UA 4.11 S-1108):
- 16종이 8×2(칸 96×112: 아이콘 48 + 이름 + 수량 `str.camp.library_block_count`). 수량 0(가진 적 있음)은 따로 된 칸 상태. **가진 적 없는 블록은 칸 자체가 없다**(TV-083).
- 위에 `str.format.library_counter`("서고 {used} / {cap}"). 한도 = 블록 개수 합(지팡이에 끼운 것 제외), 100에서 시작, 50개당 2,000골드 확장(상점 "그 밖" 탭). 가득 차면 숫자 호박색 + `error.library_full`.
- 끼운 블록은 "끼움"(`str.camp.library_equipped`)으로 따로 센다. 같은 블록 한 개를 두 지팡이에 동시에 못 끼운다. [전부 빼기] `str.camp.remove_all`.
- 계정에 묶인 재료 옆 `str.format.bound_count`("(묶임 {n})").
- 오른쪽 단 다음 웨이브 자리에 서고 거르기.

이벤트: `ui.camp.open`, `ui.camp.slot_insert/slot_remove/slot_swap`, `ui.camp.tree_rebuild`, `ui.camp.stat_highlight`, `ui.camp.heal`, `ui.camp.free_heal`, `ui.camp.depart_hold`, `ui.camp.depart`.

### 8.3 컴파일 트리 미리보기 (S13 8.3, UA 4.11)

조립을 바꾸는 **즉시** 다시 만든다. 화면은 컴파일러 결과(실행 트리, 다른 요약서)를 그대로 그린다.

```
TreeNode(order 1,2,3…, block, shape{Projectile,Control,Modifier,ScopeOwner}, depth 0..3, modifiers[], forkWays 0/2/3, omitted, hasWarning, circuitSlotIndex)
ScopeTag(depth, multiplierText "×0.85", galpiFootnote)
CompileTreePreview(nodes, scopes, projectilesPerShot, heatPerShot)
```

| 규칙 | 값 |
|---|---|
| 마디 | 32×32 틀 안 24×24 아이콘. 세로 간격 16, 가로 간격 8. 왼쪽 위 실행 순서 번호 |
| 스코프 | 들여쓰기 40px + 왼쪽 세로 묶음 선. 깊이 최대 3(깊이 1~3 밝기 100/70/50%) |
| 변형 꼬리 | 투사체 마디 오른쪽에 24×24 아이콘 가로로, **한 줄 최대 6개**, 넘으면 "+N"(`str.format.more_count`). 8개 → 6 + "+2"(TV-075) |
| 분열 | 2갈래 / 3갈래 가지 |
| 스코프 배율 꼬리표 | `감쇠^depth`, 소수 둘째 자리 반올림. 일반 0.85 → "×0.85" / "×0.72" / "×0.61"(TV-073). 제자 3 지팡이 0.92 → "×0.92" / "×0.85" / "×0.78" + 각주 점 1/2/3개(TV-074) |
| 생략 | 한 번 발사 투사체가 상한 **64개**를 넘으면 가장 깊은 스코프부터 점선 마디 + "생략"(`str.camp.tree.omitted`)(TV-076) |
| 경고 마디 | 오른쪽 위에 경고 딱지 16×16 |
| 맨 아래 줄 | `str.camp.tree.footer`("발사 1회 = 투사체 {n}개 · 열 {heat}") |
| 연동 | 마디에 커서 → 회로의 그 칸에도 괄호. 경고 줄 고름 → 그 마디에 괄호 |

### 8.4 비기 트라이 `ArtTrie` (S13 8.4, UA 4.13)

- 그 캐릭터의 트라이만: 공용 10 + 고유 1. 다른 캐릭터의 ★는 없다.
- 그림 영역 912×600, 뿌리 왼쪽, 깊이 0~4 다섯 열(열 간격 176). 기술 마디 64×64 / 지나가는 점 마디 24×24. 가지 위에 입력 문양(앞, 뒤, 옆, 반대쪽 옆, 섬). 모든 기술 마디 끝에는 K.
- 간격 표시: 기본 0.3초(18 t)와 **다른 간격만** 적는다(`str.art_trie.interval` "{sec}초", TV-080: 0.3 → 없음, 0.18 → "0.18초").
- 마디 상태 `TrieNodeState`: `Unlocked`/`Locked`/`Candidate`/`Current`(2프레임) — `str.art_trie.state.*`. **고유 비기 마디는 ★, 새 프로필 첫 화면에서도 Unlocked** [확정](TV-078). Locked/Candidate는 공용 비기에만.
- 설명 판(912×144): 이름(`art.*`), 커맨드 `str.art_trie.command`, 난이도 점(1~5) `.difficulty`, 결정 비용 `.crystal_cost`, 효과 두 줄 `.effect`, 결정이 모자랄 때 나가는 기술 `.fallback`.
- 해금 차례: 720×400 판 위에 후보 카드(320×320) **2개(1개뿐이면 1개, TV-079)**에서 하나 고름 `str.art_trie.unlock_pick`. 차례는 웨이브 1~6을 넘길 때마다 1번, 공용 8종을 다 해금하면 더 오지 않는다. 비기 해금은 계정에 캐릭터별로 남는다.
- 연습 중에는 방금 넣은 입력까지의 길이 밝아진다.
- 이벤트: `ui.trie.unlock_candidate`, `ui.trie.unlock`.

### 8.5 허수아비 연습 `DummyPractice` (S13 8.5, UA 4.14)

진입: 메인 메뉴 "연습", 야영의 허수아비 연습 — **같은 화면**(TV-082). 전장 위에 더해지는 표시:

| 요소 | 위치(px) | 데이터 |
|---|---|---|
| 비기 목록 | (16,104) 320×760, 줄 48 | 고르면 목표 커맨드. [트라이 보기] `str.dummy_practice.view_trie` → `ArtTrie` |
| 목표 커맨드 | (560,104) 800×64 | `str.dummy_practice.target_command`. 넣은 만큼 밝아진다 |
| 입력 간격 표시 | (560,176) 800×48 | 입력 사이마다 막대: 허용 간격 = 막대 길이, 내 간격 = 표식. 넘으면 표식이 막대 밖(호박색) |
| 저스트 게이지 | 캐릭터 (−96,+112) 192×24 | **0.07초(≈ 4.2 t)** 창. 방향 입력 순간부터 차고, K가 창 안이면 흰 별 |
| 실패 원인 한 줄 | (560,904) 800×40 | `str.dummy_practice.fail.late_k`("K가 {sec}초 늦음"), `.fail.late_input`("{n}번째 입력이 {sec}초 늦음"), `.fail.no_crystal`("결정 부족"). sec는 소수 둘째 자리(TV-081) |
| 허수아비 수치 | (1584,104) 320×360 | DPS `.dps`, 최근 10초 피해 `.recent_damage`, 임계 비율 `.heat_ratio`, 결정 1개당 피해 `.damage_per_crystal` |
| 연습 옵션 | (1584,480) 320×360 | 결정 무한 `.option.infinite_crystal`, 적 탄 쏘기 `.option.enemy_fire`, 열 고정 `.option.heat_lock`, 결투 입력 지연 체험 `.option.duel_delay`(지연 값 고르기) |
| [봇 상대 결투 연습] | 이 화면 안 [임시](U-43) | 상대 캐릭터, 세기 3단. 열쇠 **미정**(이름 표에 없음) |

이벤트: `ui.dummy.just_star`(fx_art_just_star), `ui.dummy.late_fail`.

---

## 9. 정산 `Settlement` (S13 10절, UA 4.16)

판이 끝나면 차례로 넘어가는 **세 쪽**. 제목 `str.settlement.title`, 쪽 이름 `str.settlement.page.record/return_bag/takeaways`, [다음] `str.common.next`.

```
Record(1) ──[다음]──▶ ReturnBag(2) ──[다음](가방 확정)──▶ Takeaways(3) ──[다시 원정]▶ ExpeditionPrep
                                                                   ├[강화 화면으로]▶ Enhance(entry=MainMenu)
                                                                   └[메인 메뉴]▶ MainMenu
판이 끝난 뒤 지팡이 조립은 출발 조립으로 되돌아간다.
```

### 9.1 1쪽 기록

| 데이터 | 위치(px) | 규칙 |
|---|---|---|
| 기록 줄 | (360,120) 1200×72, 48px | `str.format.day_wave`. 출발 웨이브가 1이 아니면 `str.format.start_wave`("웨이브 {start}에서 출발")(TV-161: 21 출발 23 도달 → "3일째 웨이브 23" + "웨이브 21에서 출발") |
| 캐릭터와 지팡이 | (160,240) 384×384 + 192×192 | 쓰러진 캐릭터 받침 |
| 점수 내역 | (720,240) 1040×480 | `str.settlement.score.kills/boss/clear/fast/no_hit/graze/variance/skipped/total`. 첫 만남이 든 판은 `str.settlement.score.phantom_first` 줄("허상과의 첫 만남 3,648")(TV-162) |
| 딱지 | 합계 옆 | 최고 기록 갱신 `str.settlement.best_record`. (검증 대기는 솔로 제외) |
| 복수 추적기 | (720,736) 1040×96 | 복수 중 프로필만. 첫 만남이 든 판에서 처음 나타남. 웨이브 20 허상을 처음 잡은 판은 "복수 완료" 꼴 |

```
ScoreBreakdown(kills, boss, clear, fast, noHit, graze, varianceMulPct 100/115/120/125, skipped, phantomFirst 3648 or 0, total)
```
점수 식은 몬스터 명세(다른 요약서). 화면은 내역을 그대로 보인다.

### 9.2 2쪽 귀환 가방

```
BagSlots(passed) = min(12, passed / 3)     HeroCap(passed) = passed / 10     // passed = 이번 판에서 "직접" 넘긴 웨이브
candidates = 판 끝에 들고 있는 야생 블록(보관함 + 지팡이에 끼운 야생 블록)
Toggle(b): b.borrowed → 거부(str.settlement.bag.borrowed_note)
           picked.Count == BagSlots → 거부(str.settlement.bag.full "가방이 찼다")
           b.grade==Hero && heroPicked == HeroCap → 거부(str.settlement.bag.hero_limit "영웅 한도")
서고 남은 자리 < picked.Count → 남은 자리까지만 담기고 나머지 골드 + str.settlement.bag.library_full [임시](U-32, TV-164)
[다음] = 확정. 고르지 않고 끝난 판은 AutoFill:
AutoFill: candidates.Where(!borrowed) 등급 높은 순, 동률 블록 id asc [임시]; picked < slots && (Hero면 heroPicked < heroCap) → 담기
담지 못한 것 = candidates − picked → 골드(일반 5, 희귀 15, 영웅 50, 빌린 블록 50) 합 표시 str.settlement.bag.leftover_gold
```

| passed | 0 | 2 | 3 | 8 | 10 | 14 | 20 | 30 | 36 | 40 | 60 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| BagSlots (TV-154) | 0 | 0 | 1 | 2 | 3 | 4 | 6 | 10 | 12 | 12 | 12 |
| HeroCap (TV-155) | 0 | 0 | 0 | 0 | 1 | 1 | 2 | 3 | 3 | 4 | 6 |

- TV-157: 칸 2, 영웅 한도 0, 후보 [영웅, 희귀, 일반, 빌린 영웅] → AutoFill [희귀, 일반]. TV-160: 일반 2·희귀 1·영웅 1 못 담음 → 75골드.
- 칸은 **한 줄 8칸**(12칸 → 8 + 4, TV-156). 카운터 `str.settlement.bag.slots`("가방 {used} / {max}").
- 배치: 야생 블록 (160,200) 720×560 / 가방 (1000,200) 760×360 / 담지 못한 것 (1000,580) 760×180.
- 가방 칸을 쓰지 않는 것: 강화석, 부적, 시즌 주화, 한정 드랍, 골드. 출발 조립에 쓴 서고 블록은 야생이 아니라 잃지 않는다.
- 비정상 종료 판은 마지막 중간 지점(5웨이브마다)까지 넘긴 수로 센다(오프라인 적용 방식 **미정**, 16절).
- 주간 도전 판은 그 주 넷째 판부터 야생 블록을 담지 못하고 전부 골드.

### 9.3 3쪽 가져가는 것

| 줄 | 데이터 | 이름 열쇠 |
|---|---|---|
| 복수 보상 줄(그 판만, **맨 위**) | 거울 표식(복수 완료), 칭호 `title.revenge.mirror_breaker`("거울을 깬"), 머리 장식, 재료 중급 강화석 3 · 상급 1 · 하락 방지 부적 1. 재료·머리 장식에 자물쇠 + "계정에 묶임"(TV-163) | `str.settlement.take.revenge_reward`, `.take.bound`, `revenge.master_line` |
| 골드 | 원정 지갑 → 프로필 | `.take.gold` |
| 확정 재료 | 강화석, 부적 | `.take.materials`, `mat.*`, `charm.safeguard` |
| 재료 꾸러미 | 여는 연출 | `.take.bundle`, `item.material_bundle`, `item.crow_bundle` |
| 새로 해금한 비기 | — | `.take.new_art` |
| 단추 (360,936) 1200×64 | [다시 원정] `str.settlement.again`, [강화 화면으로] `.to_enhance`, [메인 메뉴] `.to_menu` | |

(시즌 주화·한정 드랍·검증 대기는 솔로 제외.)

**강화 안내 트리거**(S13 9.12): 정산 끝에서 `!profile.enhanceTutorialShown && run.grantedFirstCrowBundle`(그 프로필에서 처음 웨이브 3을 넘긴 판 = 까마귀 보따리 하급 강화석 3개) → `pendingEnhanceTutorial = true`. 그 뒤 처음 강화 화면을 열 때(정산의 [강화 화면으로] 포함) `EnhanceTutorial` 4쪽 한 번(U-33, TV-152). 첫 판이 웨이브 2에서 끝나면 미룬다(TV-153).

이벤트: `ui.settlement.page`, `ui.settlement.new_best`, `ui.settlement.bag_open/bag_put/bag_full`, `ui.settlement.bundle_open`, `ui.settlement.revenge_reward`, `ui.revenge.complete`.

---

## 10. 강화 화면 `Enhance` (S13 9절, UA 4.12)

확률·비용·실패 규칙은 다섯 지팡이가 같다. 계산은 규칙 코드(다른 요약서)가 하고 이 화면은 무엇을 언제 보여 주고 막는지만 정한다. 판정은 버튼을 누른 순간 이미 정해져 있다.

### 10.1 진입과 퇴장 [확정]

| 항목 | 규칙 |
|---|---|
| 들어가는 곳 | 솔로: 메인 메뉴 "강화"(+ 정산 [강화 화면으로]). 광장 메뉴 진입은 솔로 제외. 강화용 장소·NPC 없음 |
| 나가기 | Esc → `ReturnTarget` |
| 열 수 없는 때 | 판(원정) 중, 야영 중 |
| 들어올 때 | `WandPicker`로 지팡이를 고른다. 화면 안에서 **Q / E**로 바꾼다 |
| 키 | 방향키 칸 이동, **J 넣기·강화**, **K 빼기**, Esc 나가기. 마우스 가능 |
| 오프라인 | 같은 화면·확률·연출, 결과 즉시(WaitingLate 없음) |

### 10.2 `WandPicker` (S13 9.2, UA 4.12)

- 가운데 1200×720. 왼쪽 캐릭터 탭 5개(세로 분류 탭), 탭마다 "n자루". 세로 스크롤, 줄 높이 88(한 화면 6줄).
- 위 줄(높이 48): 정렬 고르기(4종) + **거르기 체크 넷**: `str.wand_list.filter.has_mutation/has_ext_slot/trade_locked/plus_zero_only` (솔로에서 거래 잠금은 늘 거짓).
- 줄: 아이콘 64, 이름(`wand.N`), 단계, 변이, 슬롯, 기운. 줄마다 [든 지팡이로](`str.wand_list.set_held`; 든 지팡이에는 단추 대신 꼬리표 `str.wand_list.held_tag`)와 [떠나보내기](조심 버튼 `str.wand_picker.release`).
- 제목 `str.wand_picker.title`. 든 지팡이는 강화 화면과 원정 준비에서 바꾼다.

떠나보내기 판정(솔로 부분):
```
CanRelease(w):
  if HandCount(w.character) <= 1   → No(str.wand_picker.reason.last_wand "마지막 한 자루")   (TV-146)
  if w.lock has OnExpedition       → No(.reason.on_expedition)
  return Yes
HandCount(c) = 그 캐릭터 지팡이 중 (보관 중 ∪ 원정 중) 수
(debt / in_duel / market_listed / pending_verify / trade_locked 는 솔로 제외)
```
- 돌려받는 골드 = 감정가의 10%. **+0은 0골드**(TV-148). 감정가 식은 `online.md` 5.5(다른 요약서 / **미정**이면 표시만 비움). 끼운 블록과 덧씌운 겉모습은 서고·옷장으로. 되돌릴 수 없다.
- `WandReleaseConfirm`(640×400): `str.wand_release_confirm.title`, `.refund`("돌려받는 골드 {gold}"), `.returns`, `.irreversible`, [떠나보내기] `.confirm`(조심), [그만두기]. 확인은 **한 번**. **+7 이상은 지팡이 이름을 입력 칸에 그대로 입력**해야 켜진다(`.type_name`; 비교: 앞뒤 공백 제거 후 이름 표 문자열과 정확히 같음 [임시], TV-149, `error.name_confirm_required`).
- 여러 자루: [여러 자루 고르기] `str.wand_picker.release_multi` → 줄마다 체크 상자. `Checkable(w) = CanRelease(w) && stage ≤ 3 && mutationCount == 0 && extSlots == 0`(TV-150: +3 무변이 무확장 true / +4 false / +2 확장1 false / +1 변이1 false). 고른 집합이 어떤 캐릭터의 손에 있는 지팡이를 모두 포함하면 마지막 체크를 막는다. 아래 줄 `str.wand_picker.release_summary`("{n}자루 고름 · 돌려받는 골드 합계 {gold}") + [떠나보내기]. 확인 창 한 번, `str.wand_release_confirm.multi_summary`.

### 10.3 화면 구성과 모델 (S13 9.3, UA 4.12; 무대 배치의 주인은 `effects.md` 1.2·20.1)

| 영역 | 위치(px) | 내용 | 이름 열쇠 |
|---|---|---|---|
| 위 띠 | (0,0) 1920×80 | "◀ 지팡이 이름 ▶ +7 칭호", 오른쪽에 골드, 강화석 하/중/상 보유량(묶인 수는 "중 8 (묶임 3)") | `wand.N`, `title.stage.N.bK`, `mat.stone_low/mid/high`, `str.format.bound_count` |
| 누계 띠 | (480,88) 960×40 | 이어서 강화 중 | `str.enhance.tally` |
| 왼쪽 칸 | (0,80) 480×840 | 지팡이 회로, 확장 슬롯 "1/3", 변이 칸(2칸, 제자 2(초롱) 3칸), 역대 최고 단계, 하락 바닥, 흔적 공개 [켬/끔], 캐릭터 초상, 흔적 안내 한 줄 | `str.enhance.slots`, `.ext_slots`("확장 슬롯 {n}/{max}"), `.mutations`, `.best_stage`, `.trace_public`, `.trace_hidden_note` |
| 가운데 칸 | (480,80) 960×840 | 무대(마법진, 제물 칸 5개, 부적 칸, 기운 칸 0..3) | `str.enhance.sacrifice`, `.charm`, `.charm_not_needed` |
| 오른쪽 칸 | (1440,80) 480×840 | 큰 확률 숫자(글자 80×128), "상한" 딱지, 내역 네 줄, 결과 띠 400×24, 실패 안내 한 줄, 기댓값 표(432×160) | `str.enhance.prob.base/sacrifice/same_element/aura/cap`, `.hint.*`, `.expect.*` |
| 아래 띠 | (0,920) 1920×160 | 왼쪽: 서고 블록(제물 후보, 칸 64, **한 줄 14칸, 좌우로 넘김**). 오른쪽: 비용 줄, [강화 J](320×64, 속성색 입힘, 금색 금지), [빠른 강화] 토글, [이어서 강화] | `str.enhance.candidates`, `.cost`, `.enhance`, `.fast_mode`, `.auto_continue` |
| "하락 가능" 꼬리표 | 강화 버튼 테 위, 호박색 | 확인 창은 띄우지 않는다 | `str.enhance.drop_possible` |
| [슬롯 새기기] [변이 제거] | 왼쪽 칸 [임시 위치] | 10.10절 | `str.enhance.engrave`, `.remove_mutation` |

```
EnhanceViewModel(wand, stage, titleId, gold, stoneLow/Mid/High, boundLow/Mid/High,
  slots, extSlots 0..3, mutations[], mutationCap(2, 제자2=3), bestStage, floorStage, traceExists, tracePublic,
  sacrifices[5], charm, aura 0..3,
  displayPercent, atCap, breakdown, band, failHint, expectation,
  candidates, cost, canAfford, dropPossibleTag, fastMode, autoContinueArmed, tally?)
```

### 10.4 확률 표시 계산 (S13 9.4)

기본 확률 [확정](DECISIONS 1.18): +1 100 / +2 90 / +3 75 / +4 60 / +5 50 / +6 40 / +7 32 / +8 25 / +9 18 / +10 12 / +11 8 / +12 5 / +13 3 (%).

```
Compute(stage 0..12, sac[], wandElement, aura):
  target = stage + 1; b = BasePct[target-1]
  if target == 1: return (100, 0, 0, 0, total 100, capped false)        // +1 도전은 100%(상한 예외)
  sacPct = min(40, Σ grade점수{일반 4, 희귀 8, 영웅 14})                 // 제물 합 상한 +40
  same   = sac 중 지팡이와 같은 속성이 하나라도 있으면 5 else 0           // 시도당 1회, 상한(+40) 밖 [조율 C-07]
  au     = 15 × clamp(aura,0,3)                                         // 최대 +45
  sum = b + sacPct + same + au; total = min(95, sum); capped = sum >= 95  // 상한 95, 닿으면 "상한"
```

| TV | 입력 | 결과 |
|---|---|---|
| 084 | +7, 제물 희귀+영웅(22), 같은 속성 포함, 기운 2 | 25 + 22 + 5 + 30 = 82 |
| 087 | +0 | 100%, 대성공 0bp, 성공 10000bp |
| 088 | +1, 제물 영웅 14 | 90+14 = 104 → 95, "상한" |
| 089 | +12, 영웅3+희귀1(50→40), 같은 속성, 기운 3 | 3 + 40 + 5 + 45 = 93 |

- 큰 확률 숫자: 값이 바뀔 때마다 **0.2초(12 t) 굴러서** 바뀐다(`ui.enhance.prob_roll`). 내역 줄에 올리면 계산이 펼쳐진다.

결과 띠 4구간(bp):
```
p = totalPct × 100
great = target==1 ? 0 : p × 15 / 100      // 성공의 15%
succ = p − great; failAll = 10000 − p
mut = failAll × 25 / 100                   // 실패의 25%
fail = failAll − mut
TV-086: p=82 → 대성공 1230, 성공 6970, 변이 450, 하락 1350
```
구간 색·무늬: 대성공 금·민무늬 / 성공 속성색·빗금 / 변이 연두·점 / 하락 또는 하락 없음 회색·격자. 구간에 올리면 퍼센트.

하락 판정과 실패 안내:
```
Floor(best) = max(0, best − 2)
DropPossible(stage, best) = stage > 3 && stage > Floor(best)
Hint: stage ≤ 3 → NoDropLow "실패해도 내려가지 않는다(+3 이하)"         str.enhance.hint.no_drop_low
      stage ≤ Floor(best) → AtFloor "바닥이다(역대 최고 +{best})"         .hint.at_floor
      charmSet → CharmProtects "부적이 막아 준다"                          .hint.charm_protects
      else DropTo "실패하면: +{stage−1}{(으)로} 하락"                       .hint.drop_to
```
- TV-085: +7(최고 +8) 부적 없음 → DropTo "+6으로", "하락 가능" 켜짐. TV-090: +3 → NoDropLow / +4 → DropTo(+3). TV-091: +7 최고 +9 → AtFloor, 부적 칸 접힘 "지금은 필요 없다". TV-092: +8 최고 +8 부적 → CharmProtects, 꼬리표 없음.
- "하락 가능" 꼬리표 = `DropPossible && !charmSet`. 확인 창 없음.
- 부적 칸 접힘 + `str.enhance.charm_not_needed` = `!DropPossible`.
- 부적: 시도 1회에 1개까지, 실패했을 때만 소모, 변이는 막지 않는다, 막은 실패에는 기운이 쌓이지 않는다.

비용:
```
GoldCost(stage) = 100 × (stage+1)²       // 현재 단계 기준. TV-093: 0→100, 7→6,400, 12→16,900
StoneFor(target) = target ≤ 5 ? 하급 : target ≤ 9 ? 중급 : 상급   // 1개. TV-094: 5 하, 6 중, 9 중, 10 상, 13 상
계정에 묶인 강화석이 있으면 묶인 것부터 쓴다
```
- 모자라면 [강화 J] 꺼짐 + 모자란 쪽이 좌우 4px 흔들림(`ui.enhance.cost_shake`; 숫자 회청). TV-095: 골드 6,399, +8 도전 → 꺼짐.
- 제물 칸 5개, 같은 속성 블록에는 속성 문양 점(8×8). 서고 블록에서 고른다.
- **마지막 투사체 블록**(서고+조립 합쳐 1개)은 제물에 못 올린다: 칸 "놓을 수 없음"(`str.enhance.cannot_place`) + 툴팁 `str.enhance.last_projectile_tip`(TV-096).

### 10.5 상태 기계 (S13 9.5)

```
enum EnhancePhase { Idle, Committing, Payment, Offering, Charge, Hold, WaitingLate, Judge,
                    ResultPlay, MutationChoice, MutationAccept, MutationRejectReplay,
                    Milestone, Plus13, ResultCard, ShortResult }

[WandPicker] → Idle
Idle:
  J/K/방향키: 제물·부적 넣고 빼기 | Q/E: 지팡이 바꾸기 | [슬롯 새기기]→SlotEngrave | [변이 제거]→MutationRemove
  [흔적 공개] 토글 | Esc → ReturnTarget
  J(강화) && canAfford && 지팡이 잠김 아님 → Committing (화면의 다른 조작 잠금, 결과 요청)
Committing → 즉시 Payment (오프라인: 결과 즉시 준비)
Ritual = Payment → Offering → Charge → Hold → Judge
  (오프라인은 Charge 끝 결과 미도착이 없으므로 WaitingLate 생략)
  의식 중 J 재입력: 누름 뒤 0.3초(18 t) 이후 → ShortResult(0.5초 = 30 t)  (TV-107: 0.2초 무시, TV-108: 0.5초 넘김)
Judge → ResultPlay(kind)
ResultPlay 끝:
  kind == MutationOffer → MutationChoice (제한 없음, 건너뛸 수 없음, TV-109)
      [받는다] → MutationAccept(단계 유지, 기운 +1, 변이 표식) → ResultCard
      [거절한다] → fallbackIfRejected 결과의 0.50초 이후를 재생 → ResultCard (TV-123)
  마일스톤(+4, +7, +10 도달) → Milestone(첫 도달 +2.5초 / 다시 도달 +0.8초) → ResultCard
  도착 단계 13 → Plus13(9초 / 일부) → ResultCard
ResultCard:
  J → 같은 구성으로 다시 시도(조건 검사 후 Committing)  str.enhance.retry
  이어서 강화 켜짐 && 멈춤 조건 아님 → 자동 Committing
```

원칙 [확정]: 결과는 누른 순간 정해져 있다. 연출은 결과를 바꾸지 않고 **충전 길이·소리·움직임은 결과와 무관하게 같다**. 확률 숫자는 실제 값. 결과 카드에서 뽑힌 값을 볼 수 있다. **대성공은 항상 실패처럼 시작한다**(실패의 첫 0.5초와 같은 그림·소리, U-08).

결과 꾸러미(화면이 받는 유일한 입력):
```
EnhanceResultPacket(kind{Success, GreatSuccess, FailDrop, FailCharmBlocked, FailNoDrop, MutationOffer},
  stageBefore, stageAfter, newBest, slotExtended, charmConsumed, auraBefore, auraAfter,
  mutationCandidate?, mutationPushedOut?, breakdown, rollTenths 0..1000 (뽑힌 값×10),
  fallbackIfRejected?)    // MutationOffer일 때 거절하면 갈 결과(시도 시점에 이미 정해짐 [임시] U-41)
```

### 10.6 의식 시간표 (S13 9.6)

```
Intensity(target): 1→0, 2~3→1, 4~6→2, 7~9→3, 10~12→4, 13→5   (TV-103)
```

| 강도 | 도전 | 충전 | 숨 멈춤 | 심장(분당) | 음높이 | 카메라 | 배경 어두움 | 마법진 겹 | 충전 입자/초 | 배경음 |
|---|---|---|---|---|---|---|---|---|---|---|
| 0 | +1 | 0.4초 = 24 t | 없음 | 없음 | — | — | — | — | — | — |
| 1 | +2,+3 | 0.8초 = 48 t | 0.15초 = 9 t | 70→100 | +2반음 | 확대 없음 | 60% 그대로 | 1 | 10 | −6dB |
| 2 | +4~+6 | 1.2초 = 72 t | 0.25초 = 15 t | 70→120 | +4반음 | 1.04배 | 70% | 2 | 20 | −9dB |
| 3 | +7~+9 | 1.8초 = 108 t | 0.35초 = 21 t | 70→140 | +5반음 | 1.08배 | 75% | 3 | 35 | −12dB |
| 4 | +10~+12 | 2.4초 = 144 t | 0.50초 = 30 t | 70→160 | +7반음 | 1.10배 | 85% | 4 | 50 | 끔 |
| 5 | +13 | 3.2초 = 192 t | 0.80초 = 48 t | 70→180 | +9반음 | 1.12배 | 90% | 5 + 바깥 고리 | 70 | 끔 |

구간(강도 3, 제물 있음): Payment 0.00~0.15(0~9 t, 골드 숫자 굴러 내려감 + 강화석 하나 떨어짐) → Offering 0.15~0.75(9~45 t, 제물 ①부터 **0.12초(7.2 t) 간격**, 부적 있으면 마지막에 한 번 빛남, **제물 없으면 0.2초 = 12 t**) → Charge 0.75~2.55(45~153 t, 게이지: 처음 40% 시간에 80%, 남은 60% 시간에 마지막 20%) → Hold 2.55~2.90(153~174 t, 모두 멈춤·모든 소리 끊김) → Judge 2.90(174 t).

```
Timeline(intensity, hasSacrifice):
  pay 0.15; offer = hasSacrifice ? 0.60 : 0.20
  charge = [0.4,0.8,1.2,1.8,2.4,3.2][i]; hold = [0,0.15,0.25,0.35,0.50,0.80][i]
  offerEnd = pay+offer; chargeEnd = offerEnd+charge; judge = chargeEnd+hold
ChargeGauge(t01) = t01 ≤ 0.4 ? t01/0.4×0.8 : 0.8 + (t01−0.4)/0.6×0.2       (TV-101: 0.2→0.4, 0.4→0.8, 0.7→0.9, 1.0→1.0)
OfferTick(i) = 9 + round(i × 7.2) → 9, 16, 23, 31, 38 t                     (TV-102)
```
TV-097: 강도 3 제물 있음 → 0.75(45 t) / 2.55(153 t) / 2.90(174 t). TV-098: 제물 없음 → 판정 2.50초(150 t). TV-099: 강도 1/2/4/5 판정 1.70 / 2.20 / 3.65 / 4.75초. TV-100: +1 도전은 충전 0.4초, 숨 멈춤 0, 심장 소리 없음, 결과 Success만.

### 10.7 결과별 길이와 결과 카드 (S13 9.7)

| 결과 | 길이 | 번쩍 | 흔들림 | 화면 정지 | 어두워짐(초) | 카드 문구 열쇠 |
|---|---|---|---|---|---|---|
| Success | 2.0초 = 120 t | 속성 보조색 25% ×1 | 소(1~2단)/중(3~5단) | 0.08초 | 없음 | `str.enhance.result.success` + 캐릭터 한마디(**미정**: 열쇠 없음) |
| GreatSuccess | 3.6초 = 216 t | 금색 35% ×1 | 대 | 0.12초 | 0.00~1.07 | `.result.great_success`, 금 테. 확장 3칸 이미 차 있으면 `.line.slot_full`(TV-117) |
| FailDrop | 2.2초 = 132 t | 없음 | 없음 | 없음 | 0.00~1.20 | `.result.fail_drop` + 느루 `.line.fail_drop`("원래 그런 거다.") + 흔적 줄 `.trace_banner`("흔적이 남았다 · 지금은 {state}", state=`.trace_state_hidden/public`) + 바로 바꾸는 단추(TV-119) |
| FailCharmBlocked | 2.4초 = 144 t | 없음 | 없음 | 없음 | 0.00~1.00 | `.result.fail_charm` + `.line.fail_charm`("부적 1개가 대신 탔다.") |
| FailNoDrop | 1.6초 = 96 t | 없음 | 없음 | 없음 | 0.00~0.80 | `.result.fail_no_drop` + `.line.fail_no_drop` / 바닥이면 `.line.fail_floor`(TV-121) |
| MutationOffer | 고를 때까지 | 없음 | 없음 | 없음 | 선택 때까지 | 변이 카드: 이름(`mutation.*`), 이득, 대가, [받는다] `.mutation.accept` [거절한다] `.mutation.reject`. 칸이 가득이면 `.mutation.push_out`(TV-122) |
| Milestone 첫 도달 | +2.5초 = 150 t | 없음 | 소 | 없음 | 없음 | 칭호 띠 `.milestone`("{from} → {to}") |
| Milestone 다시 도달 | +0.8초 = 48 t | 없음 | — | — | — | 칭호 띠만 작게 |
| Plus13 | 9초 = 540 t | 흰 금색 35% ×1 | 특대 | 0.15초 | 0.00~0.80 | 금 테. (서버 알림 솔로 제외) |

- 기운: 성공 → 0. FailDrop / FailNoDrop / 변이(받든 거절하든) → +1(최대 3, TV-124). FailCharmBlocked → 그대로(TV-120). 불씨가 켜질 때 큰 확률 숫자가 그 자리에서 "+15" 굴러 오른다.
- 대성공: 단계가 두 번 내려찍힌다("+7 → +8", 0.25초 뒤 "→ +9"). +12에서 대성공이면 한 번만 찍고 Plus13으로(TV-118).
- 실패 연출은 강도가 올라도 커지지 않고 **길어진다**: 0.50 뒤 침묵 1단 0.2초 / 2~4단 0.3·0.4·0.5초 [임시] / 5단 0.6초.
- 하락 없음(1단)의 어두워짐은 40%가 아니라 70%까지만.
- 마일스톤: 칭호가 바뀌는 **+4, +7, +10** 도달 시 성공·대성공 연출 뒤에 덧붙인다. 역대 최고 갱신 도달 → 2.5초 / 전에 닿은 단계 → 0.8초(TV-111: +6→+7 최고 6 → 2.5초, TV-112: 최고 9 → 0.8초, TV-113: +4→+5 없음). 대성공이 칭호 구간(+0~3, 4~6, 7~9, 10~12, 13)을 넘으면 마일스톤 [임시](U-47).
- "서버 전체에 알렸다" 줄(`.line.server_notified`): **오프라인은 빠진다**(TV-114).
- Plus13: **역대 최고 갱신일 때만** 전체 9초. +13에서 내려갔다 다시 오른 경우 0.80~3.00(2.2초)만(TV-116). **처음 2초(120 t)는 건너뛸 수 없고**(TV-110: 1.5초 J 무시, 2.5초 넘김), 두 번째 번쩍은 없다.
- 결과 카드 펼침: `.line.roll`("뽑힌 값 {roll} / {p} 이하면 성공"), 소수 한 자리(TV-131: "뽑힌 값 37.2 / 82.0 이하면 성공").
- 결과 제목 변이 `.result.mutation`.

칭호(지팡이 단계 구간, names): `title.stage.{캐릭터}.b0`(+0~3) / `b1`(+4~6) / `b2`(+7~9) / `b3`(+10~12) / `b4`(+13, "엄청엄청 위대한 {char.N}"). 단계 별명 `wand.N.stage.S.nickname`.

### 10.8 건너뛰기, 빠른 강화, 짧은 형태 (S13 9.8)

| 기능 | 동작 | 제한 |
|---|---|---|
| 건너뛰기 | 의식 중 J 재입력(누름 뒤 0.3초 무시) → 판정 시각으로, 결과 **0.5초 짧은 형태** | 변이 카드, +13 첫 도달 처음 2초는 못 넘김 |
| 빠른 강화(켬/끔) | 충전 0.3초(18 t), 심장·숨 멈춤·카메라·어두워짐 없음, 결과 짧은 형태. **시도 1회 약 0.8초(48 t)** | **+10 이상 도전은 빠른 강화를 켜도 전체 의식**(설정 `enhance_fast_allow_high`로 풀 수 있음) |
| 짧은 형태 | 숫자 내려찍기 + 속성색 또는 회색 고리 + 한 음. 대성공 = 금색 고리 + 두 번 내려찍기, 변이 = 연두 고리 | 기운 불씨와 확률 숫자 변화는 생략하지 않는다 |

```
UseFullRitual(target, s) = s.enhance_ritual == Full || (target >= 10 && !s.enhance_fast_allow_high)
TV-125: 빠른 켬, +9 → false / +10 → true.  TV-126: allow_high 켬, +10 → false
```
- 강화 화면 [빠른 강화] 토글과 설정의 `enhance_ritual`은 **같은 값 하나**(C-08).
- 효과 한도: 시도 1회에 전체 화면 번쩍 최대 1회 / 연속 시도에서 번쩍 사이 0.4초 이상(빠른 강화 번쩍 15% 이하) / 어두워짐은 0.1초에 걸친 1회 전환(빠른 강화에서는 안 씀) / 충전 중 떨림은 지팡이 그림만 4px, 화면은 떨지 않음 / 화면 균열 덮개는 정지 그림 3장.
- 강화 화면은 전투가 아니므로 화면 전체를 멈춰도 된다(그림 정지).

### 10.9 이어서 강화와 기댓값 표 (S13 9.9, 9.10)

```
AutoContinue: 같은 제물 구성(같은 등급·속성 블록이 서고에 남아 있을 때) + 같은 부적 사용 여부로 반복.
  시도마다 따로 결과를 뽑는다(미리 여러 번 뽑지 않는다).
  tally → "12번째 시도 · 성공 5 · 실패 7 · 쓴 골드 44,800" (str.enhance.tally, TV-130)
StopIf(any): stageAfter ≥ 목표 단계 | 골드·강화석 부족 | 제물 부족 | 부적을 쓰는데 0개 |
             MutationOffer | GreatSuccess | 마일스톤 도달 | stopOnDrop && FailDrop (기본 켬 [임시]) |
             J 또는 K 누름 | Q/E로 지팡이 바꿈 [임시]
"실패" 수에 변이·부적이 막음·하락 없음을 모두 센다 [임시]
```
TV-127~129: 목표 +10 도달 → 멈춤 / 대성공·변이·마일스톤 → 멈춤 / 서고에 같은 블록 없음 → 멈춤.

기댓값 표(오른쪽 칸 아래 432×160): 목표 단계 고르기(◀▶, 깃발 표식) `str.enhance.expect.target`, 평균 시도 `.avg_tries`, 평균 골드 `.avg_gold`, 평균 강화석 `.avg_stone`, `.with_owned`("보유 재화로 평균 {n}번").
```
상태 (단계 0~13) × (기운 0~3) × (역대 최고와의 차 0~2) = 168개 (TV-138). 앞으로 제물·부적 안 씀 가정.
E[s,a,g] = 0 (s ≥ target);  1 + Σ P(o|s,a)·E[next]
  P = Band(Compute(s, 제물 0, 같은 속성 0, a))
  성공 (s+1, 0, max(0,g−1)) | 대성공 (min(13,s+2), 0, max(0,g−2))
  실패·하락 가능 (s−1, min(3,a+1), g+1) | 실패·하락 없음 (s, min(3,a+1), g) | 변이(받음 가정) (s, min(3,a+1), g)
가치 반복(수렴 1e-9) 또는 가우스 소거. 골드·강화석은 1 대신 GoldCost(s), StoneFor(s+1) 항으로 따로 푼다.
```
| 목표 (+0에서) | +5 | +7 | +10 | +13 |
|---|---|---|---|---|
| 시도 (TV-135) | 6.71 | 14.78 | 49.01 | 174.92 |
| 골드 (TV-136) | 8,517 | 37,461 | 273,628 | 1,776,723 |
강화석 +13까지 하/중/상 9.0 / 48.1 / 117.8 (TV-137).
"보유 재화로 평균 N번" = 지금 상태에서 제물·부적 없이 계속 도전할 때 골드나 그 구간 강화석이 처음 모자라게 되기까지의 기대 시도 수. 고정 씨앗 2,000회 몬테카를로, (지팡이 상태, 보유량)으로 캐시 [임시](U-45).

### 10.10 슬롯 새기기, 변이 제거, 흔적 공개 (S13 9.11)

`SlotEngrave`(800×560, `str.slot_engrave.title`):

| 항목 | 값 |
|---|---|
| 대상 | 단계 **+7 이상** && 확장 < 3칸 (TV-141: +6 꺼짐, +7 확장 3칸 꺼짐) |
| 비용(1회) | 상급 강화석 2개 + `20,000 × (현재 확장 칸 + 1)` 골드 → 20,000 / 40,000 / 60,000 (TV-139) |
| 성공 확률 | `min(100, 40 + 20 × 이 칸 실패 수)` → 40 / 60 / 80 / 100 (TV-140). **화면에 표시** |
| 실패 | 재료만 사라짐, 단계·기운 불변. 실패마다 눈금 한 칸(지팡이마다 저장) |
| 이벤트 | `ui.enhance.engrave_try/engrave_ok/engrave_fail` |

`MutationRemove`(720×480, `str.mutation_remove.title`): 지울 변이를 고르고 값 확인. **변이 1개당 3,000골드**(TV-142). 이벤트 `ui.enhance.mutation_remove`.

흔적 공개 [확정]:
- 흔적 조건 `stage < bestStage`, 개수 `min(2, best − stage)`(TV-144: +5 최고 +7 → 2, 최고 +6 → 1).
- 설정 위치: 강화 화면 왼쪽 칸 `str.enhance.trace_public` [켬/끔], **지팡이마다 따로 저장, 기본 끔**. 설정 화면에는 없다(안내 `str.settings.trace_note`만, TV-145).
- 내 화면은 항상 보인다. 끔일 때 흔적 옆 가림 표시 + `str.enhance.trace_hidden_note`. (남·거래 화면 규칙은 솔로 제외)
- 역대 최고까지 다시 올리면 조각이 하나씩 떨어져 나간다(성공 연출 0.40 시점, `ui.enhance.trace_shed`).

### 10.11 `EnhanceTutorial`

가운데 800×480, 4쪽(`str.enhance_tutorial.title`, `.page`("{n} / {total}")). 트리거는 9.3절. **쪽 본문 열쇠는 이름 표에 없다(미정)**.

### 10.12 강화 이벤트 (bindings 파일)

`ui.enhance.open/close`(E-01), `sacrifice_put/take`(E-02), `prob_roll`(E-03), `charm_set`(E-04), `cost_shake`, `wand_switch`, `trace_toggle`(E-33), `press`(E-05), `pay`(E-06), `offer`(E-07, 순번 1~5), `charge_begin`(E-08, E-09)/`charge_progress`, `hold`(E-10), `fail_start`(E-13), `success`(E-11, E-12), `great_crack_spread`(E-18)/`great_shatter`(E-19, E-12, 흔들림 large)/`slot_extend`(E-20), `fail_light_out`(E-14)/`wand_drop`(E-15)/`stage_down`(E-16), `charm_catch`(E-21)/`charm_burn`(E-22), `no_drop`(E-23), `aura_ember`(E-17), `mutation_vine`(E-24)/`mutation_card`(E-25)/`mutation_accept`·`mutation_reject`(E-26), `milestone`(E-27)/`title_band`(E-28), `plus13_silence`(E-10)/`plus13_slam`(E-29, 흔들림 xlarge)/`plus13_theme`(E-30)/`master_wakes`, `trace_shed`, `short_result`(E-31), `result_card`(E-36 캐릭터 한마디), `portrait_react`, `release`. (`late_wait`/`late_notice` E-32는 솔로에서 안 쓴다.)

---

## 11. 기록 `Records`, 칭호 고르기 `TitlePicker`, 옷장, 상점

### 11.1 `Records` (S13 12, UA 4.18)

- 오프라인 프로필은 **기기 안 개인 기록만**. 제목 `str.records.title`.
- 캐릭터 탭 6개 (48,104) 1824×48: 전체 `str.records.tab.all` + `char.1~5`.
- 요약 판 (48,168) 560×760: 최고 점수 `.best_score`, 최고 웨이브 `.best_wave`, 누적 웨이브 `.total_waves`(야생 물 단계), 결투 전적 `.duel_record`(솔로: 봇 연습만? **미정**), 시즌 결과 `.season_result`(솔로 제외), 얻은 칭호 `.titles`. "전체" 탭 맨 아래 복수 기록: 복수 중 → 추적기 큰 꼴 / 복수 완료 → `revenge.tracker.done_line`("복수 완료 · {date} · 첫 만남에서 {n}판째") + 거울 표식 / **첫 만남 전 → 자리 없음**(TV-167).
- 판 목록 (632,168) 1240×760, 줄 높이 64: 날짜 `.date`, 캐릭터 `.character`, 도달 웨이브 `.reached_wave`, 출발 웨이브 `.start_wave`, 점수 `.score`, 지팡이 단계 `.wand_stage`, 조립(24×24 아이콘 줄) `.loadout`, [다시 보기] `.replay`(다시 보기 재생 기능은 **미정**: 결정론 기록이 없으므로 [임시] 단추 숨김).
- `TitlePicker`(720×640): 얻은 칭호 중 닉네임 앞에 붙일 것 하나. `str.title_picker.title`, `.equip`, `.none`.

### 11.2 `Wardrobe` (S13 15.7, UA 4.24)

- 전체 화면, **오프라인에서도 된다**. 제목 `str.wardrobe.title`.
- 미리보기 (48,104) 560×880: 캐릭터 256×256, 방향 돌리기(아래·옆·위) `str.wardrobe.preview.turn`, 걷기 `.walk`·앉기 `.sit`·감정표현 `.emote`, 지팡이는 현재 단계 그대로. 위에 캐릭터 초상 5개.
- 분류 (632,104) 280×880: 12분류 `cosmetic_cat.*`. 야생 물(`wild_stain.*`)은 "옷" 분류 맨 위에 단계 고르기 `.wild_stain_pick`.
- 격자 (936,104) 936×880, 칸 96×96 8열: **얻은 것만 나온다**(빈 칸·잠긴 칸·"상점에서 구매" 표시 없음, TV-197). 입는 중 = 확정 체크 `.worn`. "원래 모습" `.original`.
- 정보: 이름, 얻은 곳 `.acquired_from` + `.source.gold_shop/record/season/trade`, `.bound`.

### 11.3 `Shop` (골드 상점, S13 15.6, UA 4.23)

- 가운데 1200×800. 머리 1200×96(행상 한마디 + 내 골드), 탭 1200×48. 제목 `str.shop.title`, `str.shop.kind.gold`.
- 탭(오프라인): 부적 `str.shop.tab.charm` / 새 지팡이 `.tab.wand` / 꾸미기(**탭 열쇠 없음, 미정**) / 그 밖 `.tab.other`(서고 확장. 닉네임 바꾸기는 솔로 제외). **유료 탭은 상품 0개면 없다**(TV-198).
- 품목 카드 216×280, 5열, 간격 24: 아이콘 96×96, 이름, 값 `str.shop.price`, 남은 한도 `str.shop.weekly_limit`("이번 주 {n} / {max}").
- 값: 부적(`charm.safeguard`) **10,000골드, 프로필당 주 3개**(기기 시계 기준 주; 주 시작 요일 **미정**), 새 지팡이(`item.wand_box`) **500골드**, 서고 확장(`item.library_expansion`) **50개당 2,000골드**.
- 새 지팡이는 캐릭터마다 한 장, 카드 아래 `str.shop.wand_count`("지금 {n}자루"). 보관 한도가 없으므로 **골드만 있으면 언제나 켜짐**(TV-232: 499 꺼짐 / 500 켜짐). 부적 이번 주 3개 → "이번 주 3 / 3" 꺼짐(TV-233), `str.shop.sold_out`.
- 구매 확인 `ShopBuyConfirm`(560×360, 수량 조절 "− 12 +"): `str.shop_buy_confirm.title`, `.confirm`, [산다] `str.shop.buy`.
- 이벤트 `ui.shop.buy`.

---

## 12. 설정 `Settings`, 키 재설정 `KeyRebind` (S13 13, UA 4.19)

### 12.1 구조

- 분류 6개(왼쪽 (48,104) 320×880, 세로 탭): 화면 `str.settings.category.display`, 소리 `.sound`, 조작 `.controls`, 전투 보기 `.combat_view`, 대화와 공개 `.chat_privacy`, 계정 `.account`.
- 옵션 목록 (392,104) 1000×880, 줄 높이 56: 이름 + 값(토글 / 슬라이더 / ◀값▶ / 드롭다운). 줄을 고르면 보통 괄호.
- 설명과 미리보기 (1416,104) 456×880: 고른 옵션 설명. "연출 줄이기", "색각 보조", "예고 채움 진하기", "내 탄 투명도"는 작은 전장 그림(424×240)에서 바로 보여 준다.
- 아래 (392,1000): [기본값으로] `str.settings.defaults`(지금 분류만, 확인 창 한 번 `.defaults_confirm` [임시]) [닫기] `str.common.close`.
- **줄일 수 없는 것(적 예고·적 탄·열 링·피격 표시·시야 화살표)은 목록에 없다**(TV-169). **흔적 공개는 없다**(안내 한 줄만).
- 설정은 기기에 저장(파일 위치·형식 **미정**), 오프라인/서버 공용.

### 12.2 옵션 표 (솔로에 보이는 줄)

| 분류 | 키 | 이름 열쇠(`str.settings.opt.*`) | 타입·범위 | 기본 |
|---|---|---|---|---|
| 화면 | `window_mode` | `window_mode` | Windowed / Fullscreen | Fullscreen [임시] |
| 화면 | `resolution` | `resolution` | (w,h), 최소 1920×1080 창 | 기기 화면 [임시] |
| 화면 | `motion_preset` | `motion_preset` | Full / Reduced / Minimal | Full(첫 실행 물음 값) |
| 화면 | `shake_scale` | `shake_scale` | 0~100 | 100 |
| 화면 | `flash_scale` | `flash_scale` | 0~100 | 100 |
| 화면 | `hitstop_enabled` | `hitstop_enabled` | bool | 켬 |
| 소리 | `vol_master`, `vol_bgm`, `vol_sfx`, `vol_wand`, `vol_ui`, `vol_enemy` | 같은 이름 | 0~100 | 100 [임시] |
| 소리 | `vol_notice_bell`, `vol_plaza_others` | 같은 이름 | 0~100 | 100 (솔로에서 숨김 [임시]) |
| 조작 | `key_layout` | `key_layout` | Default / Alternate / Custom | Default |
| 조작 | (키 재설정) | `key_rebind` | 12.3절 | — |
| 전투 보기 | `my_bullet_opacity` | `my_bullet_opacity` | 30~100% (29 → 30으로 고정, TV-170) | 100 |
| 전투 보기 | `damage_number_opacity` | `damage_number_opacity` | 0~100% | 100 |
| 전투 보기 | `damage_number_mode` | `damage_number_mode` | All / CritWeakOnly / Off | All |
| 전투 보기 | `wand_fx_level` | `wand_fx_level` | All / Reduced / Minimal | All |
| 전투 보기 | `wand_fx_opacity` | `wand_fx_opacity` | 20~100% (19 → 20) | 100 |
| 전투 보기 | `others_wand_fx` | `others_wand_fx` | All / Reduced / Off | All (광장 전용 → 솔로 숨김 [임시]) |
| 전투 보기 | `bullet_trail` | `bullet_trail` | On / Short / Off | On |
| 전투 보기 | `ground_marks` | `ground_marks` | bool | 켬 |
| 전투 보기 | `kill_shards` | `kill_shards` | On / SmokeOnly | On |
| 전투 보기 | `art_name_popup` | `art_name_popup` | On / Difficulty5Only / Off | On |
| 전투 보기 | `combo_position` | `combo_position` | NearCharacter / Hud | NearCharacter |
| 전투 보기 | `clone_zone_opacity` | `clone_zone_opacity` | 30~100% (29 → 30) | 100 |
| 전투 보기 | `weather_particles` | `weather_particles` | bool | 켬 |
| 전투 보기 | `telegraph_fill` | `telegraph_fill` | 70 / 80 / 90 % | 70 |
| 전투 보기 | `colorblind_assist` | `colorblind_assist` | bool (켬 = 차오르는 채움에 줄무늬, 적 탄 주황 줄무늬) | 끔 |
| 대화와 공개 | `enhance_ritual` | `enhance_ritual` | Full / Fast | Full (오프라인에서도 보임) |
| 대화와 공개 | `enhance_fast_allow_high` | `enhance_fast_allow_high` | bool | 끔 |
| 계정(오프라인) | — | `str.settings.account.save_path`, `.restore_save` | 저장 파일 위치, [저장 파일 되돌리기] | — |

값 이름: `str.settings.value.*`(full, reduced, minimal, all, crit_weak_only, off, on, short, smoke_only, difficulty5_only, near_character, hud, fast, plus13_only, windowed, fullscreen, default, alternate, custom). 솔로에서 숨기는 줄 [임시](U-26): 채팅 끄기, 조립 공개, 강화 알림에 내 이름, 서버 강화 알림 보기, 차단 목록.

기본값 모음(TV-168): 내 탄 100, 피해 숫자 100·전부, 연출 Full, 흔들림 100, 번쩍 100, 그림 정지 켬, 지팡이 효과 전부·100, 남의 지팡이 효과 전부, 탄 꼬리 켬, 바닥 흔적 켬, 처치 조각 켬, 기술 이름 켬, 콤보 캐릭터 옆, 사본·지대 100, 날씨 켬, 예고 채움 70, 색각 보조 끔, 서버 강화 알림 전체, 강화 의식 전체.

### 12.3 키 재설정 (조작 분류 오른쪽 1480×880)

| 배치 | 위 | 아래 | 왼쪽 | 오른쪽 | 발사 | 비기 | 자세 | 반전 자세 | 일시정지 |
|---|---|---|---|---|---|---|---|---|---|
| Default [확정](TV-171) | W | S | A | D | J | K | Shift | Space | Esc [임시] |
| Alternate [제안](TV-172) | ↑ | ↓ | ← | → | Z | X | C | V | Esc [임시] |

- 배치 고르기 탭 3개: [기본: WASD · J · K · Shift · Space] `str.settings.value.default` [대체: 방향키 · Z · X · C · V] `.alternate` [내 배치] `.custom`.
- 기능 줄 9줄(줄 높이 56) — 열쇠 `str.key_rebind.action.move_up/move_down/move_left/move_right/fire/art/stance/reverse_stance/pause`. 줄마다 키 칸 둘: 기본 키 `str.key_rebind.primary`, 보조 키 `.secondary`. (광장 키 5줄 `interact/emote/chat/nearby_list/plaza_menu`은 솔로 제외)
- 키 받기:
```
Idle ──(키 칸 + J/클릭)──▶ Capturing(slot)  str.key_rebind.capture_prompt "키를 누르세요 · Esc 취소"
Capturing: Esc → Idle(취소, 불변, TV-174) | 5초(300 t) 입력 없음 → Idle(저절로 취소, TV-173) | KeyDown(k) → pending[slot]=k; Validate; → Idle
Validate: 같은 문맥(전투) 안에서 k가 다른 기능의 기본/보조에 있으면 두 줄 모두 경고 str.key_rebind.conflict("{action}{과/와} 같은 키")
canSave = 경고 0개 (겹친 채로 저장 안 됨, TV-175)
저장 → key_layout = Custom
```
- **물리 키 기준**으로 읽고, 전투 중 한글 입력기의 영향을 받지 않는다(TV-177). 안내 `str.key_rebind.korean_note`. Unity: `KeyCode`/Input System의 `Key`(물리 자리) 사용.
- Esc는 받기 중 취소 키라 받을 수 없다 → 일시정지 Esc는 **고정 보조 키** [임시](U-24).
- 키보드 그림(1480×280): 지금 배치가 얹힌 키보드, 쓰이는 키가 밝다.
- 제목 `str.key_rebind.title`, 문맥 `str.key_rebind.context.combat`.

---

## 13. 알림과 안내

### 13.1 화면 안 알림 종류 (솔로)

| 종류 | 자리 | 시간 | 열쇠 |
|---|---|---|---|
| HUD 한 줄 안내 | 전투 (16,1008) 720×32 | 사건마다(길이 **미정**) | `str.battle_hud.notice.*` |
| 토스트 쪽지 | 오른쪽 위 480×96, 내려온다 | 첫 만남 쪽지 4초(240 t) | `revenge.tracker.first_note` |
| 꺼짐 이유 한 줄 | 꺼진 것 아래/툴팁 | 커서가 있는 동안 | `error.*`, 각 화면 `reason` 열쇠 |
| 확인 창 | 가운데 | 닫을 때까지 | 1.6절 |
| 서버 강화 알림 | — | — | **솔로 제외**(오프라인은 받지도 내지도 않는다, TV-200) |

### 13.2 복수 추적기 (S13 16, UA 4.26)

```
RevengeState { NotMet, Pending, Completed }
RevengeTrackerModel(state, bestPassedWave(전 캐릭터 최고), bestDamagePct?(웨이브 20에서 진 판 중 허상 체력을 가장 많이 깎은 %, 첫 만남 제외), phantomAttempts, runsToComplete, completedAt?, justCompletedThisRun)
NotMet: 어디에도 없다(첫 만남을 미리 알리지 않는다)
Pending: 첫 만남 뒤 ~ 웨이브 20 허상을 처음 잡을 때까지 보인다
Completed: 그 판 정산에서 "복수 완료"로 한 번 바뀐 뒤 사라짐. 기록 화면에 날짜·걸린 판 수
Mirror = Completed ? Broken : (bestDamagePct ?? 0) ≥ 50 ? Cracked : Intact
TrackerProgressNumerator = min(19, bestPassed)
```

| 꼴 | 자리 | 내용(열쇠) |
|---|---|---|
| 큰 꼴 | 야영 다음 웨이브 칸·기록 화면 432×96 / 정산 1040×96 | 1줄 `revenge.tracker.goal` / 2줄 `revenge.tracker.progress`("넘긴 최고 웨이브 {n} / 19") + 진행 막대 / 3줄 `revenge.tracker.dent`(한 번도 닿지 못했으면 **줄 없음**, TV-234/235) |
| 한 줄 꼴 | 메인 메뉴 원정 카드 368×24, 원정 준비 시작 웨이브 줄 위 592×24 | `revenge.tracker.short`(TV-237: "복수 · 최고 웨이브 14 / 19") |
| HUD 꼴 | 전투 144×16 | `revenge.tracker.hud` (웨이브 11~19만) |
| 처음 쪽지 | 오른쪽 위 480×96 | `revenge.tracker.first_note`, 4초 뒤 올라감 |
| 복수 완료 꼴 | 큰 꼴 자리 | `revenge.tracker.done` + 확정 체크 |

이벤트: `ui.revenge.note_drop`, `ui.revenge.note_lift`, `ui.revenge.complete`, `ui.revenge.tracker_update`, `ui.revenge.tracker_complete`, `ui.revenge.settlement_row`.

---

## 14. 연출 줄이기와 광과민 안전 (S13 14)

### 14.1 항상 지키는 한도 (옵션과 무관) [확정]

| 항목 | 한도 |
|---|---|
| 전체 화면 번쩍 횟수 | 어느 1초 구간에서도 **2회 이하**, 간격 **0.4초 이상**(1초 = 60 t, 0.4초 = 24 t). 1초 창은 시작 시각 기준 미끄러지는 창 |
| 번쩍 세기 | 덮는 색 불투명도 **35% 이하**, 순백 100% 금지 |
| 번쩍 모양 | 0.05초 안에 켜지고(≤ 3 t) 0.10초 이상에 걸쳐 꺼짐(≥ 6 t) |
| 빨강 전체 화면 번쩍 | **금지**. 피격 표시는 화면 가장자리(면적 25% 미만)에만 |
| 큰 국소 번쩍 | 화면 25% 이상 덮으면 전체 화면 번쩍으로 셈 |
| 깜빡이는 넓은 무늬 | 금지 |
| 밝기 급락 | 1회 전환으로만 |
| 흔들림 | 폭 16px, 길이 0.6초(36 t) 이하. 회전·확대 떨림 금지. **전장 카메라만** 흔들린다 |
| 카메라 확대 | 1.00~1.12배, 초당 0.1배 이하 |

**번쩍 관리기**: 전체 화면 번쩍은 한 곳을 거쳐서만. 한도를 넘는 요청은 버리지 않고 **테두리 강조(흰 선 8px, 0.15초 = 9 t)**로 바꾼다. 적용 불투명도 = `min(요청, 단계 상한) × flash_scale/100`(단계 상한 Full 0.35 / Reduced 0.15 / Minimal 테두리 강조). 테두리 강조 자체는 세지 않는다. TV-178~185 참고(예: 0.5 요청 → Full 0.35 / Reduced 0.15 / Minimal 테두리, Full+flash_scale 50 → 0.175).

**흔들림 누적기**: 사건마다 `acc = min(1, acc + add)`, add = 소 0.5 / 중 0.7071 / 대 0.8660 / 특대 1.0, 초당 2.5씩 감소. 폭 = `acc² × 16px × 단계배율(Full 1, Reduced 0.3, Minimal 0) × shake_scale/100`. TV-186: 소 4 / 중 8 / 대 12 / 특대 16px. 대+대 → 16. Reduced 중 → 2.4px. 소 뒤 0.1초 → 누적 0.25 → 1px.

**그림 정지**: 게임 시간을 멈추지 않고 맞은 대상·탄의 그림만 멈춘다. 배율 Full 1 / Reduced 0.5 / Minimal 0, `hitstop_enabled` 끔이면 0.

### 14.2 3단계 표

| 항목 | Full | Reduced | Minimal |
|---|---|---|---|
| 흔들림 | 100% | 30% | 없음 |
| 전체 화면 번쩍 | 35%까지 | 15%까지 | 없음(테두리 강조) |
| 그림 정지 | 켬 | 절반 | 끔 |
| 카메라 확대(강화 의식) | 켬 | 절반(1.08 → 1.04, TV-193) | 끔 |
| 예고의 흰 번쩍 | 구역 전체 | 테두리만 굵게 | 테두리만 굵게 |
| 무적 중 깜빡임 | 8Hz 반투명 | 깜빡임 없이 60% 투명 + 흰 테두리 | 같음 |
| 화면 덮개(아지랑이, 균열) | 켬 | 정지 그림 | 끔 |
| 입자 예산(전투) | 1,500 | 800 | 300 |
| 강화 의식 | 전체 | 어두워짐 깊이 절반(밝기 40%까지 → 70%까지 [임시] U-18), 균열은 마법진 안에만 | 결과 카드만(변이 카드는 그대로, +13 2초 잠금 없음 [임시] U-19) |

- 첫 만남 연출: 줄임 → 번쩍 15%, 흔들림 30%. 최소 → 테두리 강조, 흔들림 없음, 무채색 전환은 그대로.
- **소리로만 전달되는 정보는 없다**: 모든 소리 이벤트에 같은 뜻의 그림 이벤트가 짝으로 있다(심장 소리 ↔ 게이지 맥동).

---

## 15. data/presentation 파일 (구현은 그대로 읽는다)

### 15.1 `D:\algorithm_game\data\presentation\event_bindings.json`

표시 이벤트 → 그림·소리 연결. 860줄(이 중 `ui.*` 255줄).

| 필드 | 의미 |
|---|---|
| `graze_chain.window_seconds` (2.0, 초) / `max_step` (5) | 연속 스침 음높이: 같은 슬롯의 앞 스침과 2.0초 안이면 한 단씩 최대 5단, 아니면 0 |
| `sfx_catalog[]` | 쓸 수 있는 소리 id(`sfx_e01`~`e36` 강화, `c**` 전투, `t**` 비기, `h**`, `n**`). 번호는 `effects.md` 19절 사건 번호 |
| `fx_catalog[]` | `{fx, src}`: 그림 효과 id와 원문 위치(`effects.md` 절#표 줄) |
| `bindings[].event` | 이벤트 열쇠(`ui.*`, `ui.hud.*`, 코어 이벤트 `art.*` 등) |
| `bindings[].fx[]` / `sfx[]` | 붙일 그림·소리 id. 빈 배열 = 나중에 꽂을 자리(지금은 회색 도형/무음) |
| `bindings[].shake` | `none`/`small`/`medium`/`large`/`xlarge` → 흔들림 누적기 사건 |
| `bindings[].hitstop_ms` | 그림 정지 길이(ms) |
| `bindings[].confirmed_only` | 넷코드 확정 단계에서만 그림을 낸다(솔로는 늘 확정으로 보면 됨) |

- 솔로 화면 로직은 이벤트 이름을 내고(`Emit(name, payload)`), 받는 쪽이 없어도 동작해야 한다.
- 강화에 실제로 fx/sfx가 묶인 줄은 10.12절 목록. 나머지 화면(`ui.camp.*`, `ui.loot.*`, `ui.hud.*`, `ui.settlement.*`, `ui.common.*` 등)은 지금 fx·sfx 모두 비어 있다.
- 같은 뜻의 별칭 줄이 함께 있다(예: `ui.enhance.screen.opened` = `ui.enhance.open`, `ui.enhance.attempt.charge.begin` = `ui.enhance.charge_begin`, `ui.enhance.aura.gained` = `ui.enhance.aura_ember`, `ui.enhance.mutation.offered` ≈ `mutation_vine`+`mutation_card`). 하나만 내면 된다 — spec 21절 이름 쪽을 쓴다 [임시].

### 15.2 `D:\algorithm_game\data\presentation\event_bindings.none.json`

의도적으로 연출이 없는 이벤트 목록 `none[] = {event, reason}`. reason: `not_an_event`, `debug_only`, `spec_no_presentation`, `phase_of_core_event`(결투 예측·확정·번복), `camera_output`(`ui.fx.flash.*`, `ui.fx.hitstop`, `ui.fx.shake.add` — 관리기가 내는 결과라 다시 바인딩하지 않는다). 솔로에서는 "이 이벤트에는 연출을 찾지 말 것" 목록으로만 쓴다.

### 15.3 `D:\algorithm_game\data\presentation\particle_budget.json`

입자 수 상한(세 칸 배열 = Full / Reduced / Minimal).

| 필드 | 의미 / 값 |
|---|---|
| `scenes[].id` | `battle`, `duel`(솔로: 봇 연습), `plaza`(솔로 제외), `enhance` |
| `scenes[].total` | 장면 합계. battle [1500,800,300], enhance [600,300,100] |
| `pools[].category/priority/kind/quota` | 분류, 우선순위(작을수록 먼저 지킴), `one_shot`(터짐)/`recurring`(반복), 몫. battle: art 1 [400,250,120], hit 2 [300,150,60], kill 3 [300,150,60], shot 4 [240,120,0], wand_idle 5 [60,30,0], environment 6 [200,100,60]. enhance: enhance_burst 1, enhance_charge 2(각 [600,300,100], 합계 공유; 넘치면 충전 입자부터 새로 안 만듦) |
| `pools[].outside_total` | 합계 밖 몫(광장 허수아비) |
| `wand_tiers[]` | 지팡이 단계 구간(min_tier~max_tier)별 입자 수: `idle`(대기), `muzzle`(총구), `shed_per_bullet`(탄당 흘림), `hit`(적중), `trail_length_px`(꼬리 길이 px). 0~3: 8/2/0/4/16 · 4~6: 16/4/1/7/24 · 7~9: 30/5/1/8/32 · 10~12: 45/6/2/10/40 · 13: 60/8/2/12/48 |
| `wand_lifetimes_seconds` | 수명(초): idle 2, muzzle 0.2, shed 0.3, hit 0.4 |
| `hit` | 치명 +4, 약점 +2, 저항 −2 입자, 최소 2개; 0.05초 안 3회 넘는 적중은 8개로 합침; 한 프레임 처치 5개 넘으면 처치 입자 절반 |
| `bullet_load[]` | 화면 탄 수 단계: 0~100 흘림 켬·꼬리 100%·적중 장식 full / 101~200 흘림 끔·50%·half / 201+ 끔·0%·minimal |
| `trail` | 꼬리 최대 48px·4프레임·불투명 70%, 바닥 흔적 지팡이당 24개·지름 16px·불투명 60%·수명 0.8초 |
| `shares` | `damage_numbers_max` 40, `damage_merge_window_seconds` 0.2, `one_shot_fade_seconds` 0.1(넘친 일회 입자 사라짐), 결투 상대 대기 30 등 |

### 15.4 `D:\algorithm_game\data\presentation\asset_aliases.json`

콘텐츠 id ↔ 그림 폴더 열쇠.

| 필드 | 의미 |
|---|---|
| `element` | 그림 쪽 속성 열쇠 → 콘텐츠 속성(`frost`→`ice`, `shadow`→`dark`) |
| `character_folder` | 그림 폴더 이름 → `char.N` (dalgu→char.1, chorong→char.2, galpi→char.3, seonge→char.4, geumeum→char.5). 폴더 이름은 그림 경로에만 쓰고 화면·코드 이름으로 쓰지 않는다 |
| `wand_folder` | 같은 방식 → `wand.N` |
| `wand_detached` | 지팡이에서 떨어져 나간 부분 그림 변형 |
| `block_projectile_fx` | 투사체 블록 → 탄 그림 열쇠 |
| `ui_icons[]` | `content_prefix`(block./art./element./passive./wand_trait./currency.) → 아이콘 ID(I-B1 48px, I-B2 24px 등)와 `variant_prefix`, `key_overrides` |
| `character_sheets` | 초상 64×64 4프레임, 선택 192×192 1프레임, 실루엣 32×32 |
| `ui_external_size` | `S-0201` 타이틀 배경 1920×1080 |
| `wand_up_split` | `wand.2` 위쪽 나눔 y28, x오프셋 6 |

그림은 이 작업에서 만들지 않는다(회색 도형). 사용자가 넣은 `art/production/` 그림을 붙일 때 이 표로 경로를 찾는다.

---

## 16. 기존 src/ 참고 위치 (실제 구현이 있는 것)

| 내용 | 위치 |
|---|---|
| 화면 스택 Push/Replace/Pop/Back/ReturnTarget | `D:\algorithm_game\src\Wizard.ViewLogic\Stack\ScreenStack.cs:53`(Push), `:67`(Replace), `:86`(Pop), `:182`(Back), `:48`(ReturnTarget) |
| ScreenId 목록 | `D:\algorithm_game\src\Wizard.ViewLogic\Stack\ScreenId.cs` |
| 선택 괄호 크기·펄스 | `D:\algorithm_game\src\Wizard.ViewLogic\Common\Brackets.cs:34`(SizeFor), `:52`(PulseFrame) |
| 입력 장치 모드 | `D:\algorithm_game\src\Wizard.ViewLogic\Common\InputModeTracker.cs:22-25` |
| 상태 규약·묶음 커서 이동 | `D:\algorithm_game\src\Wizard.ViewLogic\Common\Interactable.cs`, `Common\InteractableGroup.cs` |
| 글자 폭·…자르기·쉼표 | `D:\algorithm_game\src\Wizard.ViewLogic\Common\UiText.cs:30`(WidthPx), `:53`(Fit), `:71`(FormatInt) |
| 번쩍 관리기 | `D:\algorithm_game\src\Wizard.ViewLogic\Presentation\Limits\FlashManager.cs:24`(Submit) |
| 흔들림 누적기 | `D:\algorithm_game\src\Wizard.ViewLogic\Presentation\Limits\ShakeAccumulator.cs:25`(Add), `:27`(Tick), `:37`(AmplitudePx) |
| 3단계 값 | `D:\algorithm_game\src\Wizard.ViewLogic\Presentation\Limits\MotionPresets.cs`, `PhotosensitivityLimits.cs`, `HitstopClock.cs`, `CameraZoomLimiter.cs`, `DamageNumberLimiter.cs` |
| 입자 예산 표 읽기 | `D:\algorithm_game\src\Wizard.ViewLogic\Presentation\Limits\ParticleBudgetTable.cs`, `ParticleBudget.cs` |
| 이벤트 바인딩 읽기·경로 | `D:\algorithm_game\src\Wizard.ViewLogic\Presentation\Bindings\EventBindingTable.cs`, `BindingJson.cs`, `PresentationEventRouter.cs` |
| UI 이벤트 열쇠 전체 | `D:\algorithm_game\src\Wizard.ViewLogic\Presentation\UiEventKeys.cs` |
| 공용 창 모델 | `D:\algorithm_game\src\Wizard.ViewLogic\Common\Dialogs\*.cs`(ConfirmDialogModel, LoadingModel, TooltipModel, DialogScreen) |
| 이름 표 틀 문법(조사 `{이/가}`, `{(으)로}`, 중첩 `{char.1}` 1단계) | `D:\algorithm_game\src\Wizard.Text\Core\TextResolver.cs:9-31` |

**없는 것**: 화면별 상태 기계(Enhance, LootPick, Camp, Settlement 등)와 화면 규칙 함수(Compute/Band/HealCost/BagSlots/AllowedSkips/CanRelease 등)는 src에 구현이 없다(검색 결과 없음). 위 의사코드로 새로 만든다.

---

## 17. 화면별 이름 열쇠 요약 (`data/names/names.ko.json`)

틀 문법: `{param}` 값 넣기, `{name.key}` 다른 이름 1단계 중첩, `{이/가}`·`{을/를}`·`{과/와}` 바로 앞 값의 받침으로 고름, `{(으)로}` 받침 있고 ㄹ 아니면 "으로". 없는 열쇠는 열쇠 문자열 그대로 찍는다.

| 화면 | 열쇠 묶음 |
|---|---|
| 공통 | `str.common.*`(all, back, cancel, caution, close, done, empty, exit, loading, locked, next, no, none, off, ok, on, prev, save, yes), `str.format.*`(bound_count, day_wave, days, gold_amount, hours, library_counter, members, minutes, more_count, percent, rank, ratio, score_change, seconds, start_wave, wand_count, wand_stage, wave), `str.tooltip.*`, `str.confirm_dialog.title`, `str.loading.title`, `str.save_restored.*` |
| 첫 실행 | `str.first_run_prompt.*` |
| 타이틀 | `str.title.*` |
| 프로필 고르기 | `str.profile_select.*` |
| 오프라인 안내 | `str.offline_guide.*` |
| 메인 메뉴 | `str.main_menu.item.*`, `.mode.*`, `.best_wave`, `.server_only`, `.offline_guide`, `.title` |
| 원정 준비 | `str.expedition_prep.*`(blocked.core_empty, blocked.on_expedition, change_loadout, depart, held_tag, loadout, ready, set_held, start_wave.*, title, wand_count, wands), `str.wand_list.*`, `str.stat.*`, `char.*`, `passive.*`, `wand_trait.*`, `element.*` |
| 전투 HUD | `str.battle_hud.*`, `tod.*`, `whim.*`, `boss.*`(+ `.epithet`), `revenge.tag`, `revenge.tracker.hud`, `phantom_first.master_line`, `str.tutorial.step.*` |
| 일시정지 | `str.pause.*`(resume, settings, controls, give_up, give_up_confirm, to_menu, pause_total, title), `str.controls_view.title`, `str.key_rebind.action.*` |
| 전리품 | `str.loot_pick.*`, `str.block.*`, `block.*` |
| 야영·조립·서고 | `str.camp.*`, `warn.*`, `frame.*`, `mat.*`, `str.format.library_counter`, `error.library_full` |
| 비기 트라이 | `str.art_trie.*`, `art.*` |
| 허수아비 연습 | `str.dummy_practice.*` |
| 정산 | `str.settlement.*`(again, bag.*, best_record, page.*, score.*, take.*, title, to_enhance, to_menu), `revenge.*`, `title.revenge.mirror_breaker`, `item.material_bundle`, `item.crow_bundle` |
| 강화 | `str.enhance.*`, `str.enhance_tutorial.*`, `str.wand_picker.*`(title, release, release_multi, release_summary, reason.last_wand, reason.on_expedition), `str.wand_release_confirm.*`, `str.slot_engrave.title`, `str.mutation_remove.title`, `mutation.*`, `title.stage.*`, `wand.*`(이름·단계 별명), `charm.safeguard`, `mat.stone_*`, `npc.master` |
| 기록 | `str.records.*`, `str.title_picker.*`, `title.*` |
| 옷장 | `str.wardrobe.*`, `cosmetic_cat.*`, `cosmetic_slot.*`, `cosmetic.*`, `wild_stain.*`, `emote.*` |
| 상점 | `str.shop.*`, `str.shop_buy_confirm.*`, `item.wand_box`, `item.library_expansion`, `charm.safeguard`, `currency.gold` |
| 설정·키 | `str.settings.*`(category.*, opt.*, value.*, defaults, defaults_confirm, trace_note, title, account.save_path, account.restore_save), `str.key_rebind.*` |
| 꺼짐 이유 | `error.not_enough_gold`, `error.not_enough_stone`, `error.not_enough_charm`, `error.core_empty`, `error.last_projectile`, `error.last_wand`, `error.library_full`, `error.weekly_limit`, `error.max_stage`, `error.name_confirm_required`, `error.mutation_pending`, `error.charm_not_needed`, `error.invalid_sacrifice`, `error.invalid_loadout`, `error.wand_locked`, `error.start_wave_not_allowed`, `error.not_supported_offline` |

솔로에서 쓰지 않는 묶음: `str.login/signup/email_verify/password_*/connect_queue/server_unreachable/new_device_notice/recovery_mail_off`, `str.duel_*`, `str.plaza_*`, `str.trade*`, `str.market*`, `str.mailbox`, `str.mail_compose`, `str.friends`, `str.inspect`, `str.report`, `str.block_confirm`, `str.leaderboard`, `str.league`, `str.monument`, `str.season_*`, `str.succession_score`, `str.master_chair`, `str.debt_list`, `str.expedition_cleanup_confirm`, `str.disconnected`, `str.run_submit_pending`, `str.ops_notice`, `str.sanction_notice`, `str.notice.*`(서버 알림), `chat.*`, `facility.*`.

---

## 18. 미정 · 모순

### 18.1 모순 (CLAUDE.md 1절 우선순위로 정함)

| ID | 내용 | 정한 쪽 |
|---|---|---|
| M-01 | 야영 활동: DECISIONS "웨이브 사이 야영에서는 조립, 회복, 비기 선택만 한다. 강화는 없다" ↔ spec 13 C-12/U-44 "허수아비 연습, 다시 뽑기도 둔다" | DECISIONS가 위다. 문자 그대로면 연습·다시 뽑기는 야영에 두면 안 된다. spec은 문장의 목적이 "강화 없음"이라 보고 다섯을 두며 "개발자 확인 필요"로 남겼다 → **사용자 확인 필요**. 구현은 두 버튼을 데이터 플래그로 끌 수 있게 [임시] |
| M-02 | 이벤트 이름: spec 13 21.3은 `hud.*`, `event_bindings.json`은 `ui.hud.*` | 데이터 파일이 읽히는 정본이므로 구현은 `ui.hud.*`(파일 열쇠)를 쓴다. spec(2순위)과 표기 차이로 남긴다 |
| M-03 | 강화 이벤트 이름 이중 표기(`ui.enhance.open` vs `ui.enhance.screen.opened` 등) | 둘 다 bindings에 있음. spec 21.5 이름을 쓴다 [임시] |
| M-04 | 설정 값 표기: spec "빠른 강화" ↔ 이름 표 `str.settings.value.fast` "빠르게"; spec 콤보 위치 "HUD(초상 옆)" ↔ 이름 표 `str.settings.value.hud` "화면 위" | 화면 글자는 이름 표만 쓴다(CLAUDE.md 5절) → 이름 표 값 |
| M-05 | 광장 메뉴 5줄 vs 8줄(C-01) | 솔로 제외 |
| M-06 | 같은 속성 +5가 제물 상한(+40) 안인가(C-07) | 상한 밖(별도 줄). 규칙 요약서와 맞출 것 |
| M-07 | 흔들림 표의 길이(소 0.15초 등) ↔ 누적기가 만드는 길이(소 0.20, 중 0.28, 대 0.35, 특대 0.40초)(C-03) | 누적기를 규칙으로 쓴다 |
| M-08 | 귀환 가방 옛 식(기본 2칸 + 10웨이브당 1, 최대 6) ↔ `÷3, 최대 12`(C-14) | `÷3, 최대 12` |
| M-09 | 소리 분류 두 목록(C-05) | 합집합 8개 |

### 18.2 미정 (값 없음 → [임시] 또는 비움)

| ID | 내용 | 지금 값 |
|---|---|---|
| N-01 | 메뉴 커서 이동 키: 방향키만인가, WASD도인가 | [임시] 둘 다 |
| N-02 | 메인 메뉴 [나가기] 동작, 일시정지 판에서 Esc | [임시] 게임 종료(확인 창 한 번) / [계속]과 같음 |
| N-03 | 서버 카드(프로필 고르기)를 솔로에서 보일지 | [임시] 보이되 꺼짐 + `error.not_supported_offline` |
| N-04 | 오프라인의 비정상 종료 판(강제 종료) 처리: 원문은 서버 기준 "마지막 중간 지점(5웨이브마다)까지 정산" | [임시] 다음 실행 때 마지막 중간 지점까지 정산하고 지팡이 잠금 해제. 사용자 확인 필요 |
| N-05 | 튜토리얼 풍선 본문, 강화 안내 4쪽 본문, 결과 카드 "캐릭터 한마디", 봇 상대 연습 화면 글자, 상점 꾸미기 탭 이름 | 이름 표에 열쇠 없음 → **미정**. 열쇠만 정하고 값은 비움(없는 열쇠는 열쇠 문자열이 찍힌다) |
| N-06 | 감정가(떠나보내기 환급 10%의 기준) 식 | `online.md` 5.5절(이 요약서 범위 밖). 없으면 환급 표시만 비움 |
| N-07 | 부적 주간 한도의 "주" 경계(요일·시각) | **미정**. [임시] 기기 현지 시각 월요일 00:00 |
| N-08 | 설정·저장 파일 위치·형식 | **미정**. [임시] `Application.persistentDataPath` 아래 JSON, 저장 3세대 보관 |
| N-09 | HUD 한 줄 안내("웨이브 클리어")의 표시 길이 | **미정**. [임시] 2초(120 t) |
| N-10 | 기록의 [다시 보기](결정론 재실행 없이) | [임시] 단추 숨김 |
| N-11 | 기록의 "결투 전적"(솔로는 봇 연습뿐) | [임시] 봇 연습 전적 또는 줄 숨김 |
| N-12 | 지팡이 잠금 중 강화 가능 여부 | 솔로: `OnExpedition`이면 불가(U-49), 나머지 잠금은 솔로에 없음 |
| N-13 | spec 20절 U-13~U-58의 [임시] 값 | 본문 해당 자리에 그대로 적었다 |

---

## 19. 솔로 테스트 벡터 목록 (S13 18절, 옮겨 시험할 것)

TV-001~003(부팅), TV-005(프로필 전환), TV-026, TV-028~038, TV-042~049, TV-054(메뉴·준비·HUD), TV-055~083(전리품·야영·조립·트라이·연습), TV-084~131, TV-135~153(강화; TV-104·105 결과 늦음과 TV-114 알림 줄의 서버 쪽, TV-132~134 결투·광장은 솔로 제외 — TV-106 "오프라인은 WaitingLate 없음"과 TV-114의 "오프라인이면 줄 없음"은 시험), TV-154~164, TV-167(정산·기록), TV-168~175, TV-177~199(설정·키·연출; TV-176 광장 키 겹침 제외), TV-200(오프라인 알림 없음), TV-232~243(상점·복수·글자·괄호). 기대값은 위 각 절에 적었다.
