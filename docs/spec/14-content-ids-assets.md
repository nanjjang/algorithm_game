# 14. 콘텐츠 식별자 · 이름 표 · 에셋 규격 — 구현용 명세서

게임: "엄청엄청 위대한 마법사 키우기". 이 문서는 구현자가 원본 기획 문서를 다시 읽지 않고도 다음을 만들 수 있게 한다.

1. 모든 콘텐츠(캐릭터·지팡이·블록·적·보스·비기·변이·재료·부적·재화·칭호·장소·꾸미기 등)의 **식별자**와 숫자 코드
2. 화면에 찍는 문자열을 한 곳에 모은 **이름 표**와 문구 틀(조사 처리 포함)
3. 그림 **배율·캔버스·팔레트** 규격과 검사기
4. 지금 저장소에 있는 **에셋 매니페스트**의 형식, 파일 이름 규칙, 불러오기 절차

그림·이펙트·소리 자체는 구현하지 않는다. 대신 꽂을 자리(식별자, 인터페이스, 이벤트 이름)를 정한다.

---

## 0. 머리말

### 0.1 출처 약어

| 약어 | 원본 |
|---|---|
| DEC | `docs/DECISIONS.md` (최우선) |
| WC | `docs/world-characters.md` |
| UI | `docs/ui-assets.md` |
| MON | `docs/monsters.md` |
| GDD | `docs/GDD.md` (3판) |
| FX | `docs/effects.md` |
| ON | `docs/online.md` |
| WAND-x | `docs/wands/x.md` |
| MAN-ui / MAN-pl / MAN-wd / MAN-mo / MAN-fx | `art/production/{ui,players,wands,monsters,effects}/manifest.json` |
| RD-ui / RD-mo | `art/production/ui/README.md`, `art/production/monsters/README.md` |

### 0.2 표시

- **[확정]**: DEC의 [개발자] 항목, 또는 원본이 [확정]으로 적은 것.
- **[조율]**: DEC의 [조율] 항목, 또는 이 명세서가 문서 사이 충돌을 DEC 기준으로 정리한 것(8절에 근거).
- **[제안]**: 원본 기획 문서의 시작값(QA에서 조정, DEC 1.17). 매니페스트·README의 현물 사실도 [제안]으로 적는다(그림은 아직 검수 전이다).
- **[임시]**: 원본이 정하지 않아 이 명세서가 구현을 막지 않으려고 넣은 기본값(9절에 모음).

### 0.3 단위와 환산 기준

| 항목 | 값 | 출처 |
|---|---|---|
| 거리 | 1칸 = 64px | DEC 2.1 [조율] |
| 기준 화면 | 1920×1080 | DEC 2.1 [조율] |
| 게임 시간 | 1초 = 60틱 | GDD 머리말 [제안] |
| 고정소수점 | Q32.32 정수(1.0 = 2^32 = 4,294,967,296) | ON 3.1 [제안] |
| 1칸 (Q32.32) | 4,294,967,296 | 환산 |
| 1px (Q32.32) | 2^32 ÷ 64 = 2^26 = 67,108,864 | 환산 |
| 입력 시각 단위 | 틱 × 256 + 틱 안 오프셋. 1단위 = 1/15,360초 | GDD 8 [제안] |

- 칸 → Q32.32: `raw = round_half_even(칸 × 2^32)`. 소수가 남는 값은 아래 표마다 반올림 값을 적었다 [임시: 반올림 방식].
- 그림의 애니메이션(UI·이펙트·캐릭터 프레임)은 **결정론 코어 밖**의 표시용이다. 틱이 아니라 실제 시간으로 재생해도 된다. 틱 환산이 정수가 아닌 것은 표에 소수로 적었다(9절 U-20).

---

## 1. 식별자 규칙

### 1.1 형식 [조율]

출처: WC 0, DEC 1.25.

- **이름은 한 곳에만 둔다.** 코드와 데이터에는 "이름 표"(id → 화면 문자열) 하나를 두고, 전투 코어·저장 파일·서버 DB·통신·자료 파일의 열쇠·그림 파일 이름은 번호나 id로만 가리킨다 [확정: DEC 1.25].
- 이름이 정해지면 이름 표의 문자열만 바꾼다 [확정: DEC 1.25].
- 식별자 문법(이 명세서의 제안) [제안]:

```
ContentId := Namespace "." Key ( "." Key )*
Namespace := [a-z][a-z0-9_]*
Key       := [a-z0-9][a-z0-9_]*
정규식    := ^[a-z][a-z0-9_]*(\.[a-z0-9][a-z0-9_]*)+$
최대 길이 := 64자 (ASCII)
```

- 캐릭터와 전용 지팡이는 숫자 키를 쓴다: `char.1`~`char.5`, `wand.1`~`wand.5` [확정: WC 0].
- 그 밖의 키는 영어 snake_case [제안]. 키는 **한 번 배포하면 바꾸지 않고 재사용하지 않는다**(저장 파일·서버 DB·입력 기록 재실행이 이 문자열에 기댄다) [임시].
- C# 쪽은 `enum`(명시적 정수 값) + `ContentId`(문자열) 두 형태를 둔다. 결정론 코어는 정수 enum만 쓰고, 저장·통신·DB는 문자열 id를 쓴다 [임시]. 정수 값은 **추가만** 하고 바꾸지 않는다.

### 1.2 네임스페이스 목록

| 네임스페이스 | 대상 | 근거 |
|---|---|---|
| `char` | 캐릭터 5 | WC 0 [확정] |
| `wand` | 전용 지팡이 5(종류) | WC 0 [확정] |
| `element` | 속성 5 + 무 | DEC 1.11 [확정], 키는 [조율] |
| `block` | 주문 블록 16 | WC 0 (`block.*`) [제안] |
| `art` | 비기 15 | WC 0 (`art.*`) [제안] |
| `passive` | 패시브 5 | WC 0 (`passive.*`) [제안] |
| `wand_trait` | 지팡이 특성 5 | [제안] |
| `mutation` | 폭주 변이 6 | [제안] |
| `enemy` | 일반 적 12 + 졸개 | MON 2.1, MAN-mo [제안] |
| `elite` | 엘리트 12 | MAN-mo [조율] |
| `boss` | 보스 6 | WC 0 (`boss.*`), MAN-mo [조율] |
| `tag` | 적 꼬리표 | [제안] |
| `tod` | 시간대 4 | [제안] |
| `frame` | 웨이브 틀 5 | [제안] |
| `whim` | 야생의 변덕 8 | [제안] |
| `event` | 날씨와 돌발 사건 5 | [제안] |
| `mat` | 강화석 | [제안] |
| `charm` | 부적 | [제안] |
| `item` | 그 밖의 물건(꾸러미, 상자, 상점 품목 등) | [제안] |
| `currency` | 재화 | ON 9.1 [제안] |
| `title` | 칭호 | WC 0 (`title.*`) [제안] |
| `place` | 장소(벌판, 광장, 결투장 등) | WC 0 [확정 id: `field`, `plaza`, `arena`] |
| `facility` | 광장 시설 10 | WC 0 (`facility.*`) [제안] |
| `npc` | 느루, 행상, 까마귀 | WC 0 (`master`) [제안] |
| `cosmetic` | 꾸미기 낱개 | [제안] |
| `cosmetic_cat` | 꾸미기 분류 12 | UI 3.7 I-M7 [제안] |
| `wild_stain` | 야생 물 단계 4 | WC 6.2 [제안] |
| `league` | 리그 3 | UI 3.4 [제안] |
| `emote` | 감정표현 8 | UI 3.10 I-F10 [제안] |
| `chat` | 채팅 채널 7 | UI 3.10 I-F8 [제안] |
| `season` | 시즌(데이터로 추가) | [임시] |

**WC 0의 기존 표기와의 호환** [조율]: WC 0은 `master`, `field`, `plaza`, `arena`를 네임스페이스 없이 적었다. 이름 표 열쇠로는 그대로 `master`, `field`, `plaza`, `arena`를 받아들이고(별칭), 정식 id는 `npc.master`, `place.field`, `place.plaza`, `place.arena`로 둔다. 별칭 해석: `master → npc.master`, `field/plaza/arena → place.*`.

### 1.3 저장·통신에서의 쓰임 [제안]

- `character_state.character`, `wand.character`, `run.character`, `leaderboard_entry.character` 열은 **1~5 정수**(ON 7.9, WC 0) [확정].
- `item_stack.item_code`, `item_instance.item_code`, `account_debt.item_code`는 이 문서의 문자열 id(`block.fireball`, `mat.stone_low`, `charm.safeguard`, `cosmetic.…`)를 그대로 쓴다 [임시].
- `currency_balance.currency`는 `gold`, `season_coin`, `starlight`(네임스페이스 없이 키만) [임시]. `bucket`은 `earned`(획득), `paid`(유료), `free`(무상) [임시: ON 9.1의 한글 구분의 영어 키].

---

## 2. 이름 표

### 2.1 규칙 [확정: DEC 1.25 / 세부는 WC 0 제안]

1. 이름 표는 `NameKey(string) → 표시 문자열(string)` 하나다. 코드는 id만 보고, 화면에 찍을 때만 이름 표를 읽는다 (WC 0).
2. 지금 넣는 값: 캐릭터 다섯은 "제자 1" ~ "제자 5". 그 밖의 항목은 정해질 때까지 **작업용 이름**을 그대로 넣는다 (WC 0, WC 개발자 확인 4) [확정: 캐릭터 / 제안: 그 밖].
3. 이름이 들어가는 문구는 문장에 이름을 박지 않고 **틀**로 쓴다. 예: +13 칭호 `엄청엄청 위대한 {char.N}` → 지금 "엄청엄청 위대한 제자 5". 서버 전체 알림, 칭호 띠, 대사 견본의 이름 자리도 같다 (WC 0).
4. 틀은 조사를 스스로 고른다(`{char.N}{이/가}`, `{wand.N}{을/를}`). 숫자로 끝나는 임시 이름은 숫자를 읽은 소리(1 일, 2 이, 3 삼, 4 사, 5 오)로 받침을 본다 (WC 0).
5. 표시 이름은 **한글 8자** 안에 들어가게 정한다. 이름 칸(캐릭터 카드, 초상 옆, 칭호 띠, 순위 줄, 알림)은 한글 8자 폭으로 잡혀 있다 (WC 0, UI 0).
6. 문서와 파일 이름(`wands/dalgu.md` 등)은 작업용 이름을 계속 쓴다 (WC 0).
7. **이름은 그림에 넣지 않는다.** 직접 그리는 글씨는 게임 제목(S-0202)과 "대"(S-0902)뿐이고 둘 다 캐릭터 이름이 아니다 (UI 0).
8. 닉네임 길이는 2~8자(ON 7.3, UI 8). 닉네임은 이름 표에 넣지 않는다(플레이어 입력) [제안].

### 2.2 자료 구조

```csharp
// 이름 표: 빌드에 들어가는 데이터 파일 하나 (예: data/names.ko.json)
public sealed class NameTable {
    public string Locale;                       // "ko"
    public int Version;                         // 바뀔 때마다 +1
    public IReadOnlyDictionary<string,string> Entries; // NameKey → 문자열 또는 틀
    public IReadOnlyDictionary<string,string> Aliases; // "master" → "npc.master" 등
}
```

- `NameKey`는 ContentId 그 자체(이름) 또는 ContentId + 접미사(`.epithet`, `.desc`, `.line.<n>`)다 [제안].
- 값이 틀이면 `{...}` 자리표시를 가진다(2.4절).
- 오프라인 프로필과 서버 프로필이 같은 이름 표를 쓴다(빌드에 포함) [임시].
- 경계: 열쇠가 없으면 화면에 열쇠 자체를 회청(`#8E9AAE`)으로 찍고 개발 빌드에서는 경고를 남긴다. 위험색으로 찍지 않는다 [임시].

### 2.3 초기 값 (지금 이름 표에 넣는 값)

**캐릭터와 지팡이** (WC 0, DEC 1.25 표, GDD 10.1)

| id | 속성 | 표시(지금) | 작업용 이름 | 문서 | 그림 폴더 열쇠 |
|---|---|---|---|---|---|
| `char.1` | 화염 | 제자 1 [확정] | 달구 | `wands/dalgu.md` | `dalgu` |
| `char.2` | 번개 | 제자 2 [확정] | 초롱 | `wands/chorong.md` | `chorong` |
| `char.3` | 비전 | 제자 3 [확정] | 갈피 | `wands/galpi.md` | `galpi` |
| `char.4` | 냉기 | 제자 4 [확정] | 성에 | `wands/seonge.md` | `seonge` |
| `char.5` | 암흑 | 제자 5 [확정] | 그믐 (dark_wizard) | `wands/geumeum.md` | `geumeum` |

| id | 표시(지금, 작업용) |
|---|---|
| `wand.1` | 끓는 부지깽이 |
| `wand.2` | 깜빡이 등불대 |
| `wand.3` | 밑줄 긋는 참나무 지팡이 |
| `wand.4` | 고드름 홀 |
| `wand.5` | 눈 뜬 옹이 지팡이 |

- "그림 폴더 열쇠"는 매니페스트(MAN-pl, MAN-wd)가 이미 쓰는 폴더 이름이다. 화면에 찍지 않는 **불투명한 내부 열쇠**로만 쓰고, 이름이 바뀌어도 바꾸지 않는다 [조율: 8절 C-05].

**장소·인물** (WC 0)

| 정식 id | 별칭 | 표시(지금, 작업용) |
|---|---|---|
| `npc.master` | `master` | 느루 |
| `place.field` | `field` | 한뎃벌 |
| `place.plaza` | `plaza` | 첫모닥 야영지 |
| `place.arena` | `arena` | 돌마당 |
| `place.tower` | | 느루의 탑 [제안] |
| `place.workbench` | | 느루의 작업대 (강화 화면 배경. 걸어서 가는 곳이 아니다, WC 2.5) [제안] |
| `npc.peddler` | | 행상 [제안] |
| `npc.crow` | | 까마귀 [제안] |

나머지(블록·적·보스·비기·칭호·시설 …)의 초기 값은 3절의 각 표 "표시(지금)" 칸이다.

### 2.4 틀 문법과 조사 알고리즘 [제안: WC 0 규칙의 구현]

틀 문법:

```
Template  := ( Text | Ref | Josa )*
Ref       := "{" NameKey "}"            // 이름 표에서 읽어 넣는다
           | "{" ParamName "}"          // 호출자가 주는 값 (예: {n})
Josa      := "{" A "/" B "}"            // 바로 앞의 Ref 값의 끝 글자로 고른다
           | "{(으)로}"                  // 특수형
Text      := 그 밖의 글자. "{{" 와 "}}" 는 글자 그대로의 { }
```

조사 쌍(받침 있을 때 / 없을 때): `이/가`, `을/를`, `은/는`, `과/와`, `아/야`, `이에요/예요`, `이라/라`, 특수형 `(으)로` [제안: WC 0은 이/가, 을/를만 예로 들었다. 나머지는 확장].

```
function ResolveTemplate(tpl, params):
    out = ""; last = null              // last: 바로 앞 Ref가 낸 문자열
    for token in Parse(tpl):
        if token is Text: out += token.text; last = null
        elif token is Ref:
            v = params.has(token.key) ? ToString(params[token.key]) : NameTable.Get(Alias(token.key))
            v = ResolveTemplate(v, params) if v contains "{" else v    // 1단계 중첩 허용, 2단계 이상은 오류 [임시]
            out += v; last = v
        elif token is Josa:
            if last == null: out += token.withBatchimForm   // 앞에 이름이 없으면 받침형 [임시]
            else:
                (has, isRieul) = FinalSound(last)
                if token is "(으)로": out += (has and not isRieul) ? "으로" : "로"
                else: out += has ? token.A : token.B
            last = null
    return out

function FinalSound(s):              // 끝 글자의 받침 유무와 ㄹ 받침 여부
    c = 마지막 글자 (끝의 공백, ')', ']', '"', '…' 은 건너뛴다) [임시]
    if 0xAC00 <= c <= 0xD7A3:
        jong = (c - 0xAC00) % 28
        return (jong != 0, jong == 8)       // 8 = ㄹ
    if '0' <= c <= '9':                     // 숫자를 읽은 소리
        // WC 0: 1 일, 2 이, 3 삼, 4 사, 5 오. 0·6~9는 [임시]: 0 영, 6 육, 7 칠, 8 팔, 9 구
        table = { 0:(T,F), 1:(T,T), 2:(F,F), 3:(T,F), 4:(F,F), 5:(F,F), 6:(T,F), 7:(T,T), 8:(T,T), 9:(F,F) }
        return table[c]
    return (false, false)                   // 영문·기호: 받침 없음 [임시]
```

경계:
- 여러 자리 숫자(예: "제자 10")도 **끝 자리 숫자**로 본다("10"을 "십"으로 읽는 규칙은 넣지 않는다) [임시].
- 앞 Ref가 빈 문자열이면 `last = null`과 같다 [임시].

### 2.5 폭과 자르기 [제안: UI 1.4]

- 폭 계산: 한글 한 자 = 글자 크기, 숫자와 영문 한 자 = 글자 크기의 절반 (UI 1.4). 공백·문장 부호는 절반 [임시].
- 칸을 넘으면 줄이지 않고 끝을 "…"로 자른다. 전체 글은 툴팁으로 보여 준다 (UI 1.4).
- 숫자는 세 자리마다 쉼표(48,200) (UI 1.4).

```
function TextWidth(s, size):  sum over ch: IsHangul(ch) ? size : size/2      // 정수 px, size는 짝수만 쓰이므로 나눗셈이 정확하다
function Fit(s, size, maxW):
    if TextWidth(s,size) <= maxW: return s
    e = TextWidth("…", size)            // "…"는 기호이므로 절반 폭 [임시]
    while s != "" and TextWidth(s,size) + e > maxW: s = s[:-1]
    return s + "…"
function FormatInt(n):  부호 + 절댓값을 오른쪽부터 3자리마다 ','   // 음수 부호는 '−'(U+2212) [임시: 글꼴에 있는 기호]
```

- 이름 칸 폭 = 8 × 글자 크기(한글 8자) [제안: UI 0].

---

## 3. 콘텐츠 식별자 전체 목록

모든 표의 "표시(지금)"는 이름 표 초기 값이다(작업용 이름, DEC 1.25). 정수 코드는 결정론 코어용이다 [임시: 값 배정].

### 3.1 속성

| 코드 | id | 표시(지금) | 주색 | 보조색 | 탄 모양 | 출처 |
|---|---|---|---|---|---|---|
| 0 | `element.neutral` | 무 | 회청 `#8E9AAE`(문양) | — | — | GDD 10.2, UI 3.2 |
| 1 | `element.fire` | 화염 | 주황 `#FF8A1F` | 밝은 노랑 `#FFE08A` | 둥글고 꼬리가 일렁인다 | DEC 2.2, FX 0.1 |
| 2 | `element.lightning` | 번개 | 레몬 `#FFF36B` | 흰색 `#FFFFFF` | 꺾인 선(깜빡임) | DEC 2.2, FX 0.1 |
| 3 | `element.arcane` | 비전 | 청록 `#4FE3C1` | 흰 민트 `#D6FFF3` | 네모·마름모(글자 조각) | DEC 2.2, FX 0.1 |
| 4 | `element.ice` | 냉기 | 하늘 `#9FDCFF` | 흰 하늘 `#F4FBFF` | 뾰족한 육각 조각 | DEC 2.2, FX 0.1 |
| 5 | `element.dark` | 암흑 | 푸른 보라 `#8A5CFF` | 연보라 `#D9C7FF` | 세 겹 고리(밝은 테, 어두운 고리, 밝은 심) | DEC 2.2, FX 0.1 |

- 속성 5개 [확정: DEC 1.11]. 색 이름(주황/레몬/청록/하늘/푸른 보라) [조율: DEC 2.2]. 색 값 [제안: WC 2.4, FX 0.1].
- `char.N`의 속성 코드 = N (설계상 맞춰 둠) [임시].
- 영어 키 `ice`, `dark`는 FX 매니페스트의 표기이고, UI 매니페스트는 `frost`, `shadow`를 쓴다. 정식 키는 `ice`, `dark`이고 UI 매니페스트의 `element` 값과 파일 이름의 `element_frost`/`element_shadow`는 불러올 때 별칭으로 바꾼다 [조율: 8절 C-01].

```
ElementAlias = { "frost":"ice", "shadow":"dark", "neutral":"neutral", "fire":"fire", "lightning":"lightning", "arcane":"arcane", "ice":"ice", "dark":"dark" }
```

### 3.2 캐릭터, 지팡이, 패시브, 지팡이 특성

| char | wand 종류 | 패시브 id | 패시브(지금) | 지팡이 특성 id | 지팡이 특성(지금) | 고유 비기 id |
|---|---|---|---|---|---|---|
| `char.1` | `wand.1` | `passive.overflow` | 끓어넘침 | `wand_trait.heat_at_80` | 임계 구간 80부터 | `art.kiln` |
| `char.2` | `wand.2` | `passive.lightning_rod` | 피뢰침 체질 | `wand_trait.three_mutations` | 변이 3개 보유 | `art.bolt_dash` |
| `char.3` | `wand.3` | `passive.underline` | 밑줄 | `wand_trait.depth_decay` | 깊이 감쇠 0.92 | `art.copy_circle` |
| `char.4` | `wand.4` | `passive.thin_ice` | 살얼음 | `wand_trait.keep_crystal` | 피격 시 결정 유지 | `art.ice_screen` |
| `char.5` | `wand.5` | `passive.shadow_contract` | 그림자 계약 | `wand_trait.on_credit` | 외상(빚 결정 2개까지) | `art.shadow_hand` |

- 캐릭터 5명, 전용 지팡이, 고유 능력·스탯이 전부 다르다 [확정: DEC 1.2]. 그믐 = dark_wizard 그림(`art/player/dark_wizard/`) [확정: DEC 1.12].
- 영어 키는 MAN-ui(I-S2, I-S3)의 파일 이름에서 가져왔다 [제안].
- 지팡이 **개체**(한 자루)는 콘텐츠 id가 아니라 DB 행(`wand.id`)이다. 개체는 `character`(1~5), `stage`(0~13), `skin_id` 등을 가진다(ON 7.9). 그림 선택 열쇠는 `(wand 종류, 겉모습 id, 단계)` (WC 3.3) [제안].
- 같은 캐릭터의 지팡이는 개수 제한 없이 가진다 [확정: DEC 1.22]. 지팡이는 그 캐릭터만 쓴다 [조율: DEC 2.4].

### 3.3 주문 블록 16종

| 코드 | id | 표시(지금) | 종류 | 등급 | 속성 | 바탕 판 | 아이콘 그림(UI 3.1) |
|---|---|---|---|---|---|---|---|
| 1 | `block.magic_bolt` | 마력탄 | 투사체 | 일반 | 비전 | 둥근 판 | 청록 마름모 탄 하나와 짧은 꼬리 |
| 2 | `block.fireball` | 거대 화염구 | 투사체 | 희귀 | 화염 | 둥근 판 | 판을 거의 채우는 주황 원, 위로 일렁이는 노랑 꼬리 |
| 3 | `block.chain_lightning` | 연쇄 번개 | 투사체 | 영웅 | 번개 | 둥근 판 | 점 셋을 잇는 꺾인 레몬 선 |
| 4 | `block.boomerang` | 부메랑 | 투사체 | 희귀 | 비전 | 둥근 판 | 청록 ㄱ자 조각과 돌아오는 굽은 화살 |
| 5 | `block.ice_spike` | 얼음 송곳 | 투사체 | 희귀 | 냉기 | 둥근 판 | 비스듬히 놓인 뾰족한 하늘색 육각 조각 |
| 6 | `block.shadow_orb` | 그림자 구체 | 투사체 | 희귀 | 암흑 | 둥근 판 | 푸른 보라 고리: 밝은 테, 어두운 고리, 밝은 심 |
| 7 | `block.split_two` | 2갈래 분열 | 제어 | 일반 | 무 | 육각 판 | 한 줄기가 둘로 갈라지는 가지 |
| 8 | `block.split_three` | 3갈래 분열 | 제어 | 희귀 | 무 | 육각 판 | 한 줄기가 셋으로 갈라지는 가지 |
| 9 | `block.on_hit_trigger` | 적중 시 발동 | 제어 | 희귀 | 무 | 육각 판 | 과녁 점에서 새 화살이 꺾여 나간다. 왼쪽에 `{` |
| 10 | `block.wall_bounce` | 벽 튕김 | 변형 | 일반 | 무 | 깎은 네모 판 | 세로 벽 선에 부딪혀 꺾이는 화살 |
| 11 | `block.double_size` | 탄환 크기 2배 | 변형 | 일반 | 화염 | 깎은 네모 판 | 작은 원과 그것을 감싼 큰 원(주황) |
| 12 | `block.pierce` | 관통 +2 | 변형 | 일반 | 비전 | 깎은 네모 판 | 원 두 개를 꿰뚫는 화살(청록) |
| 13 | `block.frost` | 서리 | 변형 | 일반 | 냉기 | 깎은 네모 판 | 눈 결정 한 송이(하늘) |
| 14 | `block.homing` | 유도 | 변형 | 영웅 | 번개 | 깎은 네모 판 | 표적 점을 향해 휘는 화살(레몬) |
| 15 | `block.triple_heat` | 과열 3배 | 변형 | 영웅 | 화염 | 깎은 네모 판 | 임계 쐐기 `>` 세 개(흰색 + 먹색 테)를 주황 판 위에 |
| 16 | `block.corrosion` | 잠식 | 변형 | 영웅 | 암흑 | 깎은 네모 판 | 아래에서부터 먹어 들어간 원(푸른 보라 얼룩) |

- 블록 16종(냉기·암흑 블록 추가) [조율: DEC 2.5]. 얼음 송곳·그림자 구체·서리·잠식의 수치는 DEC 2.5 [조율] (수치는 블록 명세의 몫. 여기서는 식별자만).
- 영어 키는 MAN-ui I-B1/I-B2 파일 이름 [제안]. FX 매니페스트의 탄 그림 열쇠는 다르다(`mana_bolt`, `ice_lance`, `piercing` …) — 5.6절의 대응표 [조율: C-02].
- 블록 종류 enum: `projectile`(투사체)=1, `control`(제어)=2, `modifier`(변형)=3 [제안: MAN-ui `block_kind`]. 등급 enum: `common`(일반)=1, `rare`(희귀)=2, `hero`(영웅)=3 [임시].
- "무" 속성 블록의 아이콘 그림 색은 분필 흰색이고 속성 문양 자리가 비어 있다. 그림에 위험색을 쓰지 않는다. 화염은 주황·노랑까지만. 판 안의 수량, "빌림", 경고는 아이콘에 굽지 않고 겹쳐 그린다 (UI 3.1) [제안].

### 3.4 비기 15종

| 코드 | id | 표시(지금) | 구분 | 아이콘(UI 3.5) | FX 발동 문양 열쇠 [임시] |
|---|---|---|---|---|---|
| 1 | `art.burst` | 마력 방출 | 공용 ① | 가운데 점에서 퍼지는 고리 하나 | `cast_seal_01` |
| 2 | `art.recoil` | 반동 사격 | 공용 ② | 앞으로 나가는 탄과 뒤로 밀리는 발자국 | `cast_seal_02` |
| 3 | `art.fan` | 부채 전개 | 공용 ③ | 세 갈래 부채 | `cast_seal_03` |
| 4 | `art.wings` | 양익 사격 | 공용 ④ | 좌우로 벌어진 두 날개 | `cast_seal_04` |
| 5 | `art.afterimage` | 잔상 걸음 | 공용 ⑤ | 옆으로 겹친 실루엣 셋 | `cast_seal_05` |
| 6 | `art.spear` | 꿰뚫는 창 | 공용 ⑥ | 곧은 창 한 줄 | `cast_seal_06` |
| 7 | `art.vent` | 방열 폭발 | 공용 ⑦ | 깨지는 열 링과 바깥으로 터지는 고리 | `cast_seal_07` |
| 8 | `art.octastar` | 팔방 전개 | 공용 ⑧ | 팔각 별 | `cast_seal_08` |
| 9 | `art.thunder_spear` | 비장: 뇌광창 | 공용 ⑨ | 창 + 저스트 별 | `cast_seal_09` |
| 10 | `art.bloom` | 비장: 만개 | 공용 ⑩ | 여러 겹으로 핀 꽃 | `cast_seal_10` |
| 11 | `art.kiln` | 불가마 | 고유 `char.1` | 점선 테의 둥근 가마와 벽돌 무늬 | `cast_seal_11` |
| 12 | `art.bolt_dash` | 벼락 질주 | 고유 `char.2` | 곧게 뚫고 지나간 꺾인 줄기 | `cast_seal_12` |
| 13 | `art.copy_circle` | 베껴 쓰기 | 고유 `char.3` | 겹쳐진 마법진 두 개 | `cast_seal_13` |
| 14 | `art.ice_screen` | 얼음 병풍 | 고유 `char.4` | 네 폭 병풍 모양의 얼음 벽 | `cast_seal_14` |
| 15 | `art.shadow_hand` | 그림자 손 | 고유 `char.5` | 바닥 웅덩이에서 뻗은 손 | `cast_seal_15` |

- 공용 10종은 분필 흰색으로 그리고 캐릭터의 속성색을 입혀 쓴다. 고유 5종은 그 캐릭터의 속성색으로 그린다. 비장(난이도 5) 둘은 테에 작은 별 둘을 더한다(금색은 쓰지 않는다). 해금하지 않은 비기는 그림을 따로 그리지 않고 회청 단색 실루엣 + 자물쇠(K-43)(코드) (UI 3.5) [제안].
- 아이콘 문양은 FX 20.4 "발동 문양 고리"와 같은 도안 (UI 3.5) [제안].
- 고유 비기는 처음부터 쓸 수 있다 [확정: DEC 1.23]. 비기 해금은 계정에 캐릭터별로 남는다 [조율: DEC 2.8].

### 3.5 폭주 변이 6종

| 코드 | id | 표시(지금) |
|---|---|---|
| 1 | `mutation.twisted_barrel` | 뒤틀린 총열 |
| 2 | `mutation.unstable_core` | 불안정 핵 |
| 3 | `mutation.hypersensitive_circuit` | 과민 회로 |
| 4 | `mutation.gluttony` | 탐식 |
| 5 | `mutation.mirror_image` | 거울상 |
| 6 | `mutation.glass_wand` | 유리 지팡이 |

- 이름은 GDD 9 [제안]. 영어 키 [임시]. 변이 아이콘 32×32 × 6종의 주인은 FX 20.1(다섯 지팡이 공용) [제안]. 지금 매니페스트에 없다(8절 C-15).
- 변이 칸 K-80: 지팡이마다 2칸(초롱의 지팡이는 3칸), "밀려날 차례"는 가장 오래된 변이에 붙는 점선 테. 연두 `#B8F04A`는 폭주 변이 전용 (UI 3.6, UI 1.2) [제안].

### 3.6 일반 적 12종과 졸개

| 코드 | id | 표시(지금) | 약점 | 저항 | 꼬리표 | 캔버스 | 첫 등장 | 실루엣의 열쇠(MON 5.2) |
|---|---|---|---|---|---|---|---|---|
| 1 | `enemy.slime_moss` | 이끼 슬라임 | 화염 | 냉기 | — | 64×64 | 1 | 납작한 돌무더기 + 윗면의 삐죽한 이끼 술 |
| 2 | `enemy.wolf_bramble` | 덤불 늑대 | 번개 | 암흑 | — | 64×64 | 3 | 낮고 긴 몸 + 등의 톱니 |
| 3 | `enemy.golem_menhir` | 선돌지기 | 비전 | 번개 | 중량 | 96×96 | 5 | 세로로 긴 비석 (유일한 96×96 직립) |
| 4 | `enemy.shaman_cap` | 갓버섯 술사 | 냉기 | 암흑 | — | 64×64 | 3 | 몸보다 넓은 갓 |
| 5 | `enemy.bug_lantern` | 꽈리 벌레 | 냉기 | 화염 | — | 64×64 | 2 | 작은 몸 위의 큰 주머니 |
| 6 | `enemy.hog_needle` | 솔침 고슴도치 | 암흑 | 냉기 | — | 64×64 | 4 | 반원 + 삐죽한 가시 부채 |
| 7 | `enemy.weasel_pot` | 화분 족제비 | 번개 | 화염 | 땅속(둔덕일 때) | 64×64 | 6 | 뒤집힌 화분 + 아래로 길게 선 몸 + 풀 한 포기 |
| 8 | `enemy.bat_dusk` | 해거름 박쥐 | 화염 | 암흑 | 비행 | 64×64 | 7 | 가로로 긴 M자 |
| 9 | `enemy.wisp_stray` | 도깨비불 | 비전 | 번개 | 비행 | 64×64 | 8 | 위로 뾰족한 불꽃, 땅에서 떠 있음 |
| 10 | `enemy.vine_thorn` | 가시덩굴 | 화염 | 비전 | 뿌리 | 64×96 | 11 | 세로로 긴 줄기 + 양팔 (유일한 64×96) |
| 11 | `enemy.boar_tusk` | 엄니 멧돼지 | 냉기 | 비전 | 중량 | 96×64 | 13 | 쐐기 모양 + 위로 휜 엄니 (유일한 96×64) |
| 12 | `enemy.toad_drum` | 북두꺼비 | 암흑 | 냉기 | — | 64×64 | 15 | 옆으로 넓은 둥근 몸 |
| 101 | `enemy.spore_seedling` | 갓버섯 새싹 | — | — | — (졸개) | 32×32 | 천 갓의 졸개 | |

- 데이터 ID(`slime_moss` 등)는 MON 2.1 [제안]. 정식 id는 `enemy.` 접두 (WC 0, RD-mo) [제안].
- 약점·저항·첫 등장은 MON 2.1·2.5 [확정: 규칙(DEC 2.5) / 제안: 배분]. 약점 ×1.25, 저항 ×0.8, 적마다 약점 1개·저항 1개, 속성별 개수 차이 1 이하 [조율: DEC 2.5].
- 허상이 부르는 슬라임은 `enemy.slime_moss`를 졸개 표시(`isMinion=true`)로 쓴다. 졸개는 꼬리표·상성·점수·골드·드랍이 없다 (MON 2.6, 1.3, 3.2) [제안].
- 체력·속도·피해·비용 수치는 적 명세의 몫이다(이 문서는 식별자와 그림만).

### 3.7 엘리트 12종

| 코드 | id | 표시(지금) | 바탕 | 추가 그림(MON 3.1) | 오버레이 캔버스 |
|---|---|---|---|---|---|
| 201 | `elite.slime_moss` | 왕이끼 슬라임 | `enemy.slime_moss` | 등에 돌탑 세 단 | 64×32 |
| 202 | `elite.wolf_bramble` | 우두머리 늑대 | `enemy.wolf_bramble` | 등 가시가 더 길고 흰 꽃이 핌 | 64×32 |
| 203 | `elite.golem_menhir` | 고인돌지기 | `enemy.golem_menhir` | 머리 위에 덮개돌 | 96×32 |
| 204 | `elite.shaman_cap` | 큰갓 술사 | `enemy.shaman_cap` | 갓이 두 겹, 가짜 얼굴이 둘 | 64×32 |
| 205 | `elite.bug_lantern` | 왕꽈리 벌레 | `enemy.bug_lantern` | 주머니 세 개 | 64×32 |
| 206 | `elite.hog_needle` | 삼연 고슴도치 | `enemy.hog_needle` | 가시 끝에 솔방울 | 64×32 |
| 207 | `elite.weasel_pot` | 화분 어미 | `enemy.weasel_pot` | 꽃이 핀 큰 화분 | 64×32 |
| 208 | `elite.bat_dusk` | 박쥐 여왕 | `enemy.bat_dusk` | 날개막 흰 점이 초승달로 (+ 급강하 64×64 1장) | 64×32 |
| 209 | `elite.wisp_stray` | 쌍둥이불 | `enemy.wisp_stray` | 불꽃 두 개가 붙어 있음 (전용 본체 64×64 3장) | 64×32 |
| 210 | `elite.vine_thorn` | 꽃 핀 가시덩굴 | `enemy.vine_thorn` | 봉오리가 활짝 핀 흰 꽃 | 64×32 |
| 211 | `elite.boar_tusk` | 외엄니 | `enemy.boar_tusk` | 부러진 엄니 하나, 흉터 | 96×32 |
| 212 | `elite.toad_drum` | 큰북 두꺼비 | `enemy.toad_drum` | 등에 작은 두꺼비 둘 | 64×32 |

- 이름은 MON 3.1 [제안]. id 형식 `elite.<바탕 키>`는 MAN-mo가 정한 것(기획서에 개별 ID가 없었다, RD-mo) [조율: C-08].
- 엘리트 공통 표시: 금색 `#FFD24A` 2px 외곽선(셰이더) + 머리 위 뿔 표식(16×16, `elite.horn`) + 종별 머리 장식 + 작은 체력 바 (MON 5.3) [제안].
- 엘리트는 바탕 종의 약점·저항·꼬리표를 그대로 갖고 꼬리표 "엘리트"가 더해진다 (MON 1.3, 2.6) [제안].

### 3.8 보스 6종

| 코드 | id | 표시(지금) | 별칭(`.epithet`) | 순번 | 속성 | 약점 | 저항 | 나오는 웨이브 | 캔버스 |
|---|---|---|---|---|---|---|---|---|---|
| 301 | `boss.kiln_boar` | 가마등 | 숯가마 멧돼지왕 | 0 | 화염 | 냉기 | 화염 | 10(첫 만남 뒤), 70, 130 … | 192×128 |
| 302 | `boss.mirror_phantom` | 스승의 허상 | — | 1 | 비전 | 암흑 | 비전 | 20, 80, 140 … (+ 첫 만남의 10) | 64×96 |
| 303 | `boss.spore_queen` | 천 갓 | 포자 여왕 | 2 | 없음 | 화염 | 번개 | 30, 90, 150 … | 128×160 |
| 304 | `boss.storm_owl` | 밤눈 | 폭풍 수리부엉이 | 3 | 번개 | 비전 | 번개 | 40, 100, 160 … | 공중 192×128 / 지상 96×128 |
| 305 | `boss.frost_goat` | 흰뿔 | 서리 산양 | 4 | 냉기 | 번개 | 냉기 | 50, 110, 170 … | 128×160 |
| 306 | `boss.night_serpent` | 밤삼킴 | 별밤 구렁이 | 5 | 암흑 | 번개 | 암흑 | 60, 120, 180 … | 머리 96×96, 마디 64×64, 꼬리 64×64 |

- 보스는 10웨이브마다 순서대로 [조율: DEC 2.3]. 첫 보스는 스승의 허상이고 첫 만남은 이길 수 없다 [확정: DEC 1.21]. 순서와 속성은 MON 3.2 [제안]. 영어 키는 MAN-mo [조율: C-08].
- 첫 만남의 허상은 별도 id를 만들지 않고 `boss.mirror_phantom` + 변형 플래그 `first_encounter`로 둔다. 전용 그림 클립 `crack`(2), `look`(1)을 더 쓴다 (MON 9.3) [제안].

ID 수준의 보스 순번 함수(MON 3.2를 옮김) [제안]:

```
BossOrder = [kiln_boar, mirror_phantom, spore_queen, storm_owl, frost_goat, night_serpent]
function BossForWave(w, phantomFirst):     // w ≥ 1, phantomFirst = 이 판의 웨이브 10이 첫 만남인가 (ON 7.9 run.phantom_first)
    if w % 10 != 0: return none
    if w == 10 and phantomFirst: return (boss.mirror_phantom, first_encounter)
    k = ((w / 10) - 1) % 6                 // 정수 나눗셈
    return (BossOrder[k], normal)
```

### 3.9 꼬리표, 시간대, 틀, 변덕, 날씨와 돌발 사건

**꼬리표** (MON 2.6, WC 3.6) [제안]

| 코드(비트) | id | 표시(지금) |
|---|---|---|
| 1 | `tag.elite` | 엘리트 |
| 2 | `tag.heavy` | 중량 |
| 4 | `tag.boss` | 보스 |
| 8 | `tag.flying` | 비행 |
| 16 | `tag.burrowed` | 땅속 |
| 32 | `tag.rooted` | 뿌리 |

꼬리표는 겹칠 수 있으므로 비트 집합으로 든다 [임시].

**시간대** (DEC 2.3, MON 4.1, WC 2.2)

| 코드 | id | 표시(지금) | 하루 안 웨이브 | 바닥 팔레트 | 밝은 림 세기(MON 1.4) |
|---|---|---|---|---|---|
| 1 | `tod.day` | 낮 | 1~4 | 낮 | 20% |
| 2 | `tod.dusk` | 해질녘 | 5~7 | 해질녘 | 40% |
| 3 | `tod.night` | 밤 | 8~9 | 밤 | 60% |
| 4 | `tod.boss` | 보스 | 10 | 밤 + 달, 달빛 원 | 60% |

하루 = 10웨이브, 낮(1~4)·해질녘(5~7)·밤(8~9)·보스(10) [조율: DEC 2.3].

```
function TimeOfDay(w):  r = ((w - 1) % 10) + 1
                        return r <= 4 ? day : r <= 7 ? dusk : r <= 9 ? night : boss
function DayNumber(w):  return (w + 9) / 10          // ceil(w ÷ 10), MON 4.1
```

**웨이브 틀** (MON 4.4) [제안]: `frame.swarm`(떼), `frame.hunting_party`(사냥대), `frame.battery`(포대), `frame.ambush`(매복), `frame.mixed`(혼합). 틀 아이콘 32×32 × 5종의 주인은 MON 9.5.

**야생의 변덕** (MON 4.5) [제안]

| 코드 | id | 표시(지금) | 점수 배율(100배 정수) |
|---|---|---|---|
| 1 | `whim.crowding` | 몰려옴 | 115 |
| 2 | `whim.thick_hide` | 두꺼운 가죽 | 115 |
| 3 | `whim.big_pack` | 큰 무리 | 115 |
| 4 | `whim.chief_parade` | 우두머리 행차 | 125 |
| 5 | `whim.last_breath` | 마지막 숨 | 120 |
| 6 | `whim.slow_barrage` | 느린 탄막 | 120 |
| 7 | `whim.long_night` | 긴 밤 | 120 |
| 8 | `whim.narrow_field` | 좁은 들 | 125 |

변덕 아이콘 32×32 × 8종의 주인은 MON 9.5. (효과 수치는 웨이브 명세의 몫.)

**날씨와 돌발 사건** (WC 2.3) [제안]: `event.shower`(소나기), `event.fog`(안개), `event.star_crystal`(별똥 결정), `event.master_advice`(느루의 훈수), `event.discarded_item`(버린 물건). 결투에는 넣지 않는다.

### 3.10 재료, 부적, 그 밖의 물건

| id | 표시(지금) | 아이콘(UI 3.7) | 아이콘 ID | 거래 |
|---|---|---|---|---|
| `mat.stone_low` | 하급 강화석 | 거친 회색 돌 조각 하나, 흰 금 한 줄 | I-M1 48, I-M2 24 | 가능 [확정: DEC 1.10] |
| `mat.stone_mid` | 중급 강화석 | 다듬은 육각 돌, 흰 금 두 줄, 놋쇠 띠 | I-M1, I-M2 | 가능 |
| `mat.stone_high` | 상급 강화석 | 맑게 깎인 돌, 흰 금 세 줄, 금 띠. 속이 흰 금빛으로 빛난다 | I-M1, I-M2 | 가능 |
| `charm.safeguard` | 하락 방지 부적 | 느루의 글씨가 적힌 누런 종이 부적 | I-M3 48·24 | 가능 |
| `item.material_bundle` | 재료 꾸러미 | 흰 금빛 매듭 보따리 | I-M4 | — (드랍 용기) |
| `item.crow_bundle` | 까마귀 보따리 | 까마귀 깃이 꽂힌 보따리 | I-M4 | — (드랍 용기) |
| `item.season_fabric` | 시즌 꾸미기 조각 | 무지개 테의 천 조각 | I-M5 48·24 | 시즌 한정 꾸미기 규칙을 따른다 |
| `item.wand_box` | 지팡이 상자(+0 새 지팡이) | 끈으로 묶은 긴 나무 상자. 캐릭터 속성 문양(I-E1)을 겹친다 | I-M6 | — (구매하면 지팡이 개체가 된다) |
| `item.library_expansion` | 서고 확장 | | I-M9 | 상점 품목 |
| `item.rename` | 닉네임 바꾸기 | | I-M9 | 상점 품목 |
| `item.health_shard` | 체력 조각 | 16×16 연두 잎(전장 드랍) | — | 판 안에서만. 프로필에 남지 않는다 |

- 강화석 셋은 **줄의 수(1, 2, 3)와 모양(거친 돌 / 육각 / 깎인 돌)** 으로 구분하고 색만으로 가르지 않는다. 재료에 청록과 위험색을 쓰지 않는다 (UI 3.7, MON 5.4) [제안].
- 귀속 수량(복수 보상의 강화석·부적)은 같은 `item_code`의 `qty_bound`로 센다(ON 7.9) — 별도 id를 만들지 않는다 [제안].
- 영어 키는 MAN-ui I-M1~I-M9 파일 이름을 따랐다(`stone_low` 등) [제안].

### 3.11 재화

| id(키) | 표시(지금) | 구분(bucket) | 거래 | 출시 시 | 아이콘(UI 3.8) |
|---|---|---|---|---|---|
| `currency.gold` (`gold`) | 골드 | earned | 가능 | 사용 | 금색 동전. 가운데 느루의 흔들의자 무늬(드랍 동전 첫 프레임과 같은 도안) |
| `currency.season_coin` (`season_coin`) | 시즌 주화 | earned | 불가 | 사용 | 구멍이 뚫린 놋쇠 엽전. 놋쇠색 `#C8A24A` |
| `currency.starlight` (`starlight`) | 별빛 [제안: 이름] | paid / free | 불가 | 표와 코드만 있고 잔액은 항상 0 | 네 갈래 흰 별. 유료와 무상은 같은 그림, 잔액 줄에서 글자로 나눈다 |
| (표시 전용) `currency.expedition_purse` | 원정 지갑 | — | — | 판 중의 골드 표시 | 끈을 조인 가죽 주머니에 동전 하나 |

- 현금 결제는 지금 넣지 않는다. 결제를 연결하지 않아도 모든 기능이 동작한다 [확정: DEC 1.14, 2.6].
- 별빛은 그림만 만들어 두고, 유료 상품이 0개인 동안에는 **어떤 화면에도 나오지 않는다**(잔액, 탭, 구매 버튼 모두 없음) (UI 3.8) [제안].
- 원정 지갑은 재화가 아니라 "판 중의 골드"를 프로필 골드와 구분해 보여 주는 아이콘이다 (UI 3.8) [제안].
- 아이콘: I-C1(16×16), I-C2(24×24), I-C3(48×48) × 4종. 값 줄 I-C4는 코드("아이콘 + F-N1 숫자", 모자라면 숫자가 회청이 되고 좌우로 4px 떨린다) (UI 3.8) [제안].

### 3.12 칭호

**지팡이 강화 단계 칭호** (WC 6.1) [제안]. 닉네임 앞에 붙는다. 단계가 떨어지면 칭호도 내려간다(최고 기록은 프로필에 남는다).

| id | 묶음(band) | 단계 | char.1 | char.2 | char.3 | char.4 | char.5 |
|---|---|---|---|---|---|---|---|
| `title.stage.N.b0` | 0 | +0~+3 | 미지근한 | 졸린 | 책만 읽은 | 살짝 언 | 말 없는 |
| `title.stage.N.b1` | 1 | +4~+6 | 따끈한 | 깜빡이는 | 밑줄 친 | 서리 낀 | 그늘진 |
| `title.stage.N.b2` | 2 | +7~+9 | 펄펄 끓는 | 번쩍이는 | 주석 다는 | 꽁꽁 언 | 달 없는 밤의 |
| `title.stage.N.b3` | 3 | +10~+12 | 꽤 위대한 불꽃 | 꽤 위대한 벼락 | 꽤 위대한 책벌레 | 꽤 위대한 얼음 | 꽤 위대한 그림자 |
| `title.stage.N.b4` | 4 | +13 | 틀 `엄청엄청 위대한 {char.N}` (다섯 모두 같은 틀) | | | | |

(N = 1~5.) +13 칭호의 이름 자리는 이름 표에서 읽는다 → 지금 "엄청엄청 위대한 제자 1" (WC 0, 6.1) [확정: 틀 규칙(DEC 1.25)].

```
function StageBand(stage):   // stage ∈ [0, 13], 밖이면 오류
    if stage <= 3: return 0;  if stage <= 6: return 1;  if stage <= 9: return 2;  if stage <= 12: return 3;  return 4
```

같은 묶음 함수가 지팡이 칸 단계 묶음 테(I-G1: 민테 → 못 2개 → 못 4개 → 속성색 가는 테 → 금 테)와 실루엣 대조표의 묶음(+0~3, +4~6, +7~9, +10~12, +13)에도 쓰인다 (UI 3.3, WC 3.3).

**그 밖의 칭호** (WC 6.1) [제안]

| id | 표시(지금) | 조건 | 거래 |
|---|---|---|---|
| `title.record.first_night` | 첫 밤을 넘긴 | 웨이브 10 돌파. 허상과의 첫 만남에서 쓰러졌다가 일어나 넘긴 웨이브 10도 센다 | 불가 [임시] |
| `title.record.three_nights` | 3일 밤을 넘긴 | 웨이브 30 돌파 | 불가 [임시] |
| `title.record.ten_days` | 열흘 버틴 | 웨이브 100 돌파 | 불가 [임시] |
| `title.record.*` | "등 도달 일수 기준" | 원문은 위 셋 외의 목록을 정하지 않았다(9절 U-02) | |
| `title.revenge.mirror_breaker` | 거울을 깬 (작업용) | 웨이브 20의 스승의 허상을 그 프로필이 처음 잡았을 때 | 불가 (계정 기록) |
| `title.season.heir` | 틀 `제 {n}대 후계자` | 시즌 1위. 계정에 묶인다 [조율: DEC 2.7] | 불가 |
| `title.season.top_disciple` | 틀 `제{n}기 수제자` | 시즌 2~10위 (ON 6.4) | 불가 [임시] |
| `title.feat.bloom_100` | 만개 100번 피운 | 손맛 칭호 | 불가 [임시] |
| `title.feat.flawless_night` | 한 대도 안 맞고 하룻밤 | 손맛 칭호 | 불가 [임시] |

- 칭호 띠 그림: 머리 위 S-2002(보통·수제자·후계자·+13 네 가지 32×16 9조각), 의식용 FX 20.1 칭호 띠 720×96 × 4프레임 × 2벌(보통, 금) [제안].
- 용어: 시즌 1위는 "후계자" [조율: DEC 2.8].

### 3.13 장소, 시설, 인물

| id | 표시(지금) | 자리와 생김새 (WC 2.5) |
|---|---|---|
| `facility.big_campfire` | 큰 모닥불 | 광장 한가운데. 둘레에 통나무 의자 |
| `facility.stall_street` | 좌판 거리 | 왼쪽. 플레이어가 펴는 돗자리와 나무 궤짝 |
| `facility.peddler_cart` | 행상 수레 | 왼쪽 아래. 떠돌이의 손수레(골드 상점) |
| `facility.duel_board` | 결투 게시판 | 오른쪽. 쪽지가 잔뜩 꽂힌 나무판 |
| `facility.arena_path` | 돌마당 가는 돌길 | 오른쪽 끝. 선돌 사이로 난 돌길 |
| `facility.heir_monument` | 후계자 비석 | 위쪽 가운데. 이름이 새겨진 선돌 |
| `facility.master_chair` | 느루의 흔들의자 | 후계자 비석 옆. 느루가 졸고 있다 |
| `facility.training_dummy` | 연습 허수아비 | 아래쪽 풀밭 |
| `facility.crow_mailbox` | 까마귀 우편함 | 입구(한뎃벌 출입문 옆). 새집이 달린 말뚝 |
| `facility.field_gate` | 한뎃벌 출입문 | 아래쪽 가운데. 덤불이 벌어진 틈 |

- 시설 10곳의 이름은 WC 2.5가 유일한 기준이다. "장터 NPC", "대마법사 NPC", "후계자 동상", "원정 문", "허수아비 연습장" 같은 다른 이름은 쓰지 않는다 (WC 2.5) [제안].
- 광장에 강화용 장소나 NPC는 두지 않는다 [조율: DEC 2.4].
- 장소 id는 3.13 위 표와 2.3절(`place.*`, `npc.*`).

### 3.14 꾸미기

**캐릭터에 겹치는 칸(치장 칸)** (WC 10.1, 6.3) [제안]

| 코드 | id | 표시(지금) | 그리는 법 |
|---|---|---|---|
| 1 | `cosmetic_slot.head` | 머리 장식 | 머리 위치에 붙는 24×24 × 3방향. 캐릭터마다 머리 모양이 달라 캐릭터별로 따로 그린다 |
| 2 | `cosmetic_slot.cloak` | 망토·옷 무늬 | 팔레트 교체 |
| 3 | `cosmetic_slot.footprints` | 발자국 효과 | |
| 4 | `cosmetic_slot.seat` | 앉기 동작 | |

**옷장 분류 12** (UI 3.7 I-M7, 32×32) [제안]: `cosmetic_cat.headwear`(머리 장식), `cosmetic_cat.cloak`(망토·옷 무늬), `cosmetic_cat.footprints`(발자국), `cosmetic_cat.seat`(앉기 동작), `cosmetic_cat.wand`(지팡이 겉모습), `cosmetic_cat.trail`(발사 궤적), `cosmetic_cat.kill_effect`(처치 효과), `cosmetic_cat.ritual`(강화 의식 겉모습), `cosmetic_cat.emote`(감정표현), `cosmetic_cat.border`(테두리), `cosmetic_cat.crest`(휘장), `cosmetic_cat.title`(칭호).

**꾸미기 한 건의 자료** (WC 6.3) [제안]

```csharp
public sealed class CosmeticDef {
    public string Id;                 // "cosmetic.<cat>.<key>"
    public string Category;           // cosmetic_cat.*
    public string Slot;               // cosmetic_slot.* (캐릭터 겹그림일 때만)
    public string ArtRef;             // 에셋 열쇠 (5.8절). 그림이 아직 없으면 null → I-M7 분류 아이콘으로 대신 보인다 (UI 3.7)
    public CosmeticSource[] Sources;  // record | season | trade | gold_shop | paid_shop
    public bool Bound;                // 계정에 묶임
    public string SeasonId;           // 시즌 한정이면 시즌 id
    public int[] Characters;          // 쓸 수 있는 캐릭터(머리 장식은 캐릭터별 그림), 비면 공통
}
public enum CosmeticSource { Record=1, Season=2, Trade=3, GoldShop=4, PaidShop=5 }
```

규칙:
- 치장은 전부 능력치와 무관한 겉모습이고 거래할 수 있다. 예외는 계정에 묶이는 둘: 후계자 전용, 복수 보상 머리 장식 "금 간 거울 조각" (WC 6.3) [제안]. 후계자 칭호와 전용 겉모습은 계정에 묶이고, 그 밖의 시즌 한정 꾸미기는 거래할 수 있다 [조율: DEC 2.7].
- 출처 "유료 상점"(`PaidShop`)은 자리만 두고 비워 둔다. 결제가 연결되지 않은 상태에서 모든 칸이 플레이와 게임 안 재화로 채워지고, **빈 칸이나 잠긴 표시가 화면에 나오지 않는다** (WC 6.3) [제안].
- 지팡이 겉모습(한정판 포함)은 색과 장식만 바꾸고 단계별 실루엣과 단계 표식은 가리지 못한다 [조율: DEC 2.7].
- 치장은 위험색 규칙(4.2절)과 그믐의 "닮으면 안 되는 것"(WC 4.5) 규칙을 지킨다 (WC 6.3) [제안].
- 캐릭터와 전용 지팡이는 유료 판매 대상으로 삼지 않는다. 골드 상점의 자기 캐릭터 +0 새 지팡이만 예외 (WC 6.3) [제안].

**지금 정해진 꾸미기** [제안]

| id | 표시(지금) | 분류/칸 | 묶임 | 그림 |
|---|---|---|---|---|
| `cosmetic.headwear.cracked_mirror_shard` | 금 간 거울 조각 (작업용) | headwear / head | 묶임 | 머리에 붙지 않고 **머리 옆에 뜬다**. 캐릭터 공통 한 벌, 24×24 × 3방향 = 3장. 은회색 `#AEB6C2`, 거울 흰색 `#F4F1E6` (WC 10.1) |
| `cosmetic.cloak.master_lap_blanket` | 느루의 무릎 담요 망토 | cloak | 묶임(후계자) | 미정 |
| `cosmetic.wand.clothesline` | 빨랫줄과 빨래집게가 감긴 지팡이 겉모습 | wand | 묶임(후계자) | 미정(다섯 지팡이 각각) |
| `cosmetic.seat.rocking_chair` | 흔들의자 앉기 동작 | seat | 묶임(후계자) | 미정 |

후계자 한정판은 그 시즌에만 주고 다시 풀지 않는다 (WC 6.3). 시즌마다 다른 한정판은 `SeasonId`로 구분한다 [임시].

**야생 물(누적 웨이브에 따른 옷 변화)** (WC 6.2) [제안]

| 코드 | id | 표시(지금) | 조건(그 캐릭터로 넘긴 누적 웨이브) |
|---|---|---|---|
| 1 | `wild_stain.1` | 갓 쫓겨난 | 0 |
| 2 | `wild_stain.2` | 풀물 든 | 100 |
| 3 | `wild_stain.3` | 한데서 자란 | 500 |
| 4 | `wild_stain.4` | 꽤 위대한 | 2000 |

캐릭터별 모습:

| 단계 | char.1 | char.2 | char.3 | char.4 | char.5 |
|---|---|---|---|---|---|
| 1 | 기본 | 기본 | 기본 | 기본 | 기본 |
| 2 | 주전자에 그을음, 치마 밑단에 풀물 | 고깔에 나뭇잎, 슬리퍼 한 짝이 짚신 | 로브 밑단을 잘라 묶음, 책 지붕에 새똥 자국 | 망토 밑단에 흙, 얼음 조각 하나에 금 | 망토 끝이 해짐, 후드에 도깨비풀 |
| 3 | 주전자에 덧댄 쇳조각, 뚜껑 꼭지에 깃털 | 등불 알 둘레에 반딧불, 로브에 덧댄 천 | 책 지붕에 풀이 자람, 두루마리 통이 둘 | 망토 어깨에 흰 털가죽, 얼음 조각 6개 | 후드 끝에 작은 초승달 장식, 어깨에 까마귀 |
| 4 | 주전자 주둥이에서 늘 불티 | 등불 알이 스스로 떠다님 | 책갈피 끈이 빛나며 길게 끌림 | 발밑 서리 자국이 눈꽃 무늬 | 눈빛이 걸을 때 잔상을 남김 |

- 4단계이고 되돌아가지 않는다. 원래 모습으로 되돌려 입을 수 있다. 실루엣을 바꾸지 않는다. 야생 물은 **몸과 옷**에만 준다. 색만 바뀌는 것은 팔레트 교체, 붙는 것은 머리·어깨 위치의 작은 덧그림. 오프라인 프로필과 서버 프로필은 누적 웨이브를 따로 센다 (WC 6.2).

```
function WildStainStage(cumWaves):   // cumWaves ≥ 0
    if cumWaves >= 2000: return 4;  if cumWaves >= 500: return 3;  if cumWaves >= 100: return 2;  return 1
// 입는 단계: 1 ≤ worn ≤ WildStainStage(cum) 에서 고른다 [임시: 원문은 "원래 모습으로 되돌려 입을 수 있다"만 정함]
```

### 3.15 리그, 감정표현, 채팅 채널, 그 밖의 화면용 열거

**리그** (UI 3.4, ON 4.8) [제안]

| 코드 | id | 표시(지금) | 휘장 그림 | MAN-ui 키 |
|---|---|---|---|---|
| 1 | `league.sprout` | 새싹 | 떡잎 두 장이 난 싹. 방패꼴 나무 바탕 | `league_sprout` |
| 2 | `league.beast` | 맹수 | 엄니 두 개와 발톱 자국. 방패꼴 무쇠 바탕 | `league_beast` |
| 3 | `league.heir` | 후계 | 느루의 흔들의자 등받이 모양 + 별 하나. 방패꼴 금 바탕 | `league_heir` |

리그 3개, 소지한 아이템의 레벨로 나눈다 [확정: DEC 1.9, 조율: DEC 2.7].

**감정표현 8종** (UI 3.10 I-F10, ON 5.1, WC 10.1) [조율: 키는 8절 C-04]

| 코드 | id | 표시(지금) | 아이콘 키(MAN-ui) | 캐릭터 클립(MAN-pl) |
|---|---|---|---|---|
| 1 | `emote.greet` | 인사 | `greet` | `wave_down` |
| 2 | `emote.clap` | 박수 | `clap` | `clap_down` |
| 3 | `emote.taunt` | 도발 | `taunt` | `taunt_down` |
| 4 | `emote.bow` | 절 | `bow` | `bow_down` |
| 5 | `emote.sit` | 앉기 | `sit` | `sit_down_down`(4) 뒤 `sit_down`(2 반복) |
| 6 | `emote.dance` | 춤 | `dance` | `dance_down` |
| 7 | `emote.raise_wand` | 지팡이 들어 올리기 | `raise_wand` | `lift_wand_down` (몸 동작만. 지팡이 쪽 그림과 단계 효과는 각 지팡이 문서) |
| 8 | `emote.despair` | 좌절 | `despair` | `frustrated_down` |

**채팅 채널** (DEC 2.7, UI 3.10 I-F8) [조율: 서버 전체 채팅 없음]

| id | 표시(지금) | 아이콘 키 |
|---|---|---|
| `chat.nearby` | 주변 | `talk` |
| `chat.plaza` | 광장 | `tent` |
| `chat.trade` | 거래 | `trade` |
| `chat.whisper` | 귓속말 | `whisper` |
| `chat.friends` | 친구 | `friends` |
| `chat.duel_room` | 결투방 | `duel` |
| `chat.system` | 시스템 | `paper` |

**머리 위·목록 상태 아이콘(I-F4)** [제안]: `online`(접속 중), `offline`(접속 끊김), `expedition`(원정 중), `duel`(결투 중), `trade`(거래 중), `idle`(자리 비움), `duel_wait`(결투 대기 중), `spectate`(관전 중).

**서버 상태(I-F5)**: `online`(연결됨, 초록 점), `broken`(끊김, 끊어진 줄), `hammer`(점검 중, 망치).

**회선 표시(I-F6)**: `wired`, `wireless`, `latency_low`(넉넉: 막대 3, 초록), `latency_mid`(빠듯: 막대 2, 선택 노랑), `latency_high`(넘침: 막대 1, 호박색).

**방 옵션(I-F9, 13종)** [제안]: 비밀번호 `lock`, 관전 허용 `eye`, 장비 평준화 `gear_limit`, 야생 난입 `expedition`, 가시밭 끔 `thorns`, 라운드 수 `rounds`, 라운드 시간 `clock`, 체력 배율 `heart`, 강화 상한 `enhance_limit`, IL 상한 `il_limit`, 변이 허용 `spark`, 영웅 블록 상한 `hero_limit`, 바닥 색 `floor`. (대응은 MAN-ui 파일 이름에서 추정 [임시: `gear_limit`↔장비 평준화, `expedition`↔야생 난입, `spark`↔변이 허용].)

**메뉴 아이콘(I-F1, 16종)**: 원정 `expedition`, 결투 `duel`, 광장 `square`, 강화 `enhance`, 조립과 서고 `library`, 연습 `training`, 기록 `record`, 설정 `settings`, 옷장 `wardrobe`, 상점 `shop`, 우편함 `mail`, 순위표 `ranking`, 시즌 `season`, 친구 `friends`, 장터 `market`, 나가기 `exit`.

**설정 분류(I-F11, 6종)**: 화면 `display`, 소리 `audio`, 조작 `keyboard`, 전투 보기 `duel`, 대화와 공개 `whisper`, 계정 `person` [임시: 대응].

**능력치 아이콘(I-S1, 11종)**: 체력 `health`, 이동 속도 `move_speed`, 자세 속도 `stance_speed`, 결정 상한 `crystal_cap`, 결정 1개당 콤보 `combo_per_crystal`, 냉각 `cooling`, 임계 시작 `heat_threshold`, 잠금 시간 `lock_time`, 스쳐 피하기 감지 `graze`, 연결 슬롯 `link_slots`, 피격 시 결정 손실 `crystal_loss`.

---

## 4. 에셋 규격

### 4.1 배율과 캔버스 [조율: DEC 2.1]

| 항목 | 값 | 환산 | 출처 |
|---|---|---|---|
| 1칸 | 64px | Q32.32 4,294,967,296 | DEC 2.1 |
| 기준 화면 | 1920×1080 | 30×16.875칸 | DEC 2.1 |
| 그림 1픽셀 | 화면 1픽셀(1배) | | UI 1.1, GDD 1 |
| 캐릭터 캔버스 | 64×64 (dark_wizard 원본 크기) | 1×1칸 | DEC 2.1 |
| 적과 타일 | 같은 배율. 적 기본 캔버스 64×64, 큰 적은 96, 128, 192 단위로 키운다 | | DEC 2.1, MON 1.4 |
| 32px 기준으로 적힌 크기 | 전부 2배로 읽는다 | | DEC 2.1 (예외: 갓버섯 새싹 32×32는 실제 픽셀, MON 9.3 — 8절 C-28) |
| 손에 든 지팡이 캔버스 | 32×64px. 손잡이 기준점 (16, 48) | 0.5×1칸 | DEC 2.1, WC 3.3 |
| 강화 화면용 지팡이 큰 그림 | 192×192px. 손 그림을 키운 것이 아니라 따로 그린다 | 3×3칸 | DEC 2.1, WC 3.3 |
| 지팡이 아이콘(목록, 장터, 서버 알림) | 64×64px. 손 그림을 잘라 쓰지 않고 머리 쪽을 따로 그린다 | | WC 3.3 |
| 게임 수치(피격 판정, 사거리) | 칸 단위 그대로 | | DEC 2.1 |
| 전장 | 24×14칸, 사방이 막힘 = 1536×896px | Q32.32 가로 103,079,215,104 / 세로 60,129,542,144 | DEC 2.3, WC 2.1 |
| 광장 | 40×24칸 = 2560×1536px | | WC 2.5 |
| 돌마당 | 24×14칸 | | WC 2.6 |
| 캐릭터 발밑 기준점 | (30, 60) — 이 점이 게임 좌표 | | WC 3.1, MAN-pl `pivotPx` |
| 피격 판정 반지름 | 0.15칸 = 9.6px | Q32.32 644,245,094 | WC 3.1 |
| 판정 중심 | 발밑 기준점에서 위로 0.4칸(몸통 가운데) = 25.6px | Q32.32 1,717,986,918 | WC 3.1 |

캐릭터 그림 기준(WC 3.1) [제안]:
- 기준 그림 `art/player/dark_wizard/idle-reference-details.png` = 그믐, 그믐 지팡이 +0. 원본 그림이 +0 모습이다 [조율: DEC 2.2].
- 잰 값: 지팡이 포함 몸 x 7~56, y 3~59 (50×57px, 불투명도 50% 이상), 반투명 번짐까지 x 4~56, y 3~60 (53×58px), 지팡이 뺀 몸 x 7~45(폭 약 39px), 지팡이 x 41~56, y 21~59(끝 결정 x 50~53, y 21~28; 외곽선까지 x 49~54), 장화 바닥 y 59(왼발 x 21~27, 오른발 x 33~38), 눈 2×2 두 개 (29~30, 29~30), (38~39, 29~30), 불투명 픽셀 색 812가지(가장자리 반투명 약 60개).
- 정리 작업: 16색으로 줄이고, 불투명도를 0 또는 255로, 지팡이를 별도 층으로 떼고 가린 망토·장화를 채운다.
- 나머지 넷도 64×64, 2등신, 재질마다 3단 명암, 순수 검정이 아닌 색 있는 어두운 외곽선, 발밑 기준점 (30, 60) 근처(프레임마다 자료로 적는다).
- 실루엣 확인은 32×32 검은 덩어리. 다섯 명이 **머리 모양만으로** 구분되어야 한다(달구: 둥근 덩어리 + 옆으로 튀어나온 주둥이 + 위의 꼭지 / 초롱: 옆으로 길게 휜 고깔 + 끝에 매달린 동그라미 / 갈피: 좌우로 넓은 ㅅ자 지붕 / 성에: 머리 위에 떨어져 떠 있는 조각 5개 / 그믐: 앞으로 말린 뾰족 후드).
- 방향 3(아래·옆·위), 옆은 좌우 반전. 팔레트는 캐릭터당 16색 이하(지팡이 제외, 지팡이는 단계마다 따로).
- 기존 시안 세 장(`art/player/kettle_helm`, `lantern_hat`, `crystal_crown`)은 32×32 평면 채색 — 소재만 이어받고 다시 그린다.

지팡이 공통 그림 규격(WC 3.3) [제안]: 방향 3(정면과 위를 한 벌로 같이 쓰는 것 허용), 자세 2(들기, 내밀기). 자료 형식 `(지팡이 종류, 겉모습 id, 단계) → 그림 묶음` + 머리 위치 1점, 변이 표식 위치 2점(깜빡이 등불대 3점), 슬롯 확장 표식 위치 1점. 몸에서 떨어져 뜨는 조각(눈 뜬 옹이 지팡이)과 둘레를 도는 조각(고드름 홀)은 별도 그림, 조각마다 붙는 점 1점. 이웃 단계는 윤곽선에서 4px 이상 달라야 하고 색만 바뀌는 단계는 없다. 한정판은 색과 장식만. 통과 기준: 70개 실루엣(5×14)을 검게 칠한 "실루엣 대조표"에서 지팡이 종류와 단계 묶음을 세 방향 각각에서 맞힐 수 있어야 한다.

### 4.2 색 규칙 [조율: DEC 2.2]

1. **위험색**(적의 공격과 예고 전용): 색상각 330~15도이면서 채도 70% 이상, 명도 70% 이상. 항상 채도 80% 이상과 흰 중심을 같이 쓴다 (DEC 2.2).
2. 플레이어의 몸, 지팡이, 주문에는 위 범위의 색을 쓰지 않는다. 탁하고 어두운 자주색(그믐의 후드처럼 채도·명도가 낮은 색)은 허용한다 (DEC 2.2). 배경, 소품, 치장, 강화 화면, 열 링도 금지 (WC 2.4, FX 0.1).
3. 속성 주문색: 화염 주황, 번개 레몬, 비전 청록, 냉기 하늘, 암흑 푸른 보라. 탄은 색뿐 아니라 모양으로도 구분한다 (DEC 2.2). 값은 3.1절.
4. 그믐의 팔레트는 원본 그림에서 실제로 뽑은 색(16색 이하)을 쓰고, 그믐의 지팡이 끝은 원본대로 보라 결정에서 시작한다. 원본 그림이 +0 모습이다 (DEC 2.2).

채도·명도의 정의: HSV의 S와 V(0~100%). 근거: WC 2.4가 그믐 후드 `#7A336A`를 (314°, 58%, 48%)로 적었는데 HSV로 계산하면 (313.5°, 58.2%, 47.8%)로 맞는다 [조율].

위험색 검사기(그림 반입 도구용, 결정론 코어 밖이므로 실수 계산 허용) [제안]:

```
function HSV(rgb8):  r,g,b ∈ [0,255]
    mx = max(r,g,b); mn = min(r,g,b); d = mx - mn
    V = mx / 255 × 100;  S = (mx == 0) ? 0 : d / mx × 100
    if d == 0: H = 0  (색상각 없음)
    elif mx == r: H = 60 × (((g - b) / d) mod 6)
    elif mx == g: H = 60 × ((b - r) / d + 2)
    else:          H = 60 × ((r - g) / d + 4)
function InDangerRange(rgb):  (H,S,V) = HSV(rgb)
    return S >= 70 and V >= 70 and (H >= 330 or H <= 15)      // 경계 포함 [임시]. d == 0이면 S = 0이라 항상 거짓
function IsProperDangerUse(rgb):  return InDangerRange(rgb) and HSV(rgb).S >= 80      // + 흰 중심을 함께 그렸는지는 그림 검수 항목
function CheckPlayerSideAsset(pixels):  // 플레이어 몸·지팡이·주문·배경·소품·치장·UI 메뉴
    return count(p for p in pixels if alpha(p) > 0 and InDangerRange(p)) == 0
```

- 그믐 원본 그림에서 위험색 범위에 드는 픽셀은 0개(측정값) (WC 2.4).
- 실제 위험색 값: 빨강 `#FF3B30`(채도 81%), 자홍 `#FF2E88`(채도 82%) (FX 0.1).
- 적 몸은 더 엄격하다: 색상각 280~15도 금지, 주색 명도 60% 이상, 채도 50% 이하, 외곽선 `#1B2440` 1px, 눈은 `#FFFFFF`·`#FFF2A0`만, 청록(150~180도) 금지, 주황(20~40)·레몬(50~65)·푸른 보라(250~270)는 채도 50% 이하로만, 밝은 하늘(195~210도, 명도 90% 이상) 금지, 금색 `#FFD24A` 덩어리 금지, 공격 준비 때 무기 부위만 `#FF2E88` (MON 1.4) [제안]. 적 검사기는 `CheckEnemyBody`로 따로 둔다.

**그믐 확정 팔레트(16색)** (WC 4.5) [확정: DEC 2.2 원칙 / 제안: 값]

| 부위 | 어두운 면 | 기본 | 밝은 면 |
|---|---|---|---|
| 외곽선, 후드 속 어둠 | | `#1B0D1B` | |
| 후드, 옷자락 | `#561F4C` | `#7A336A` | `#B25A9B` |
| 눈 | | `#E2ACFA` | |
| 망토 | `#222641` | `#3E4675` | `#6271AE` |
| 걸쇠, 지팡이 금테 | `#CA8F44` | `#F8C062` | |
| 장화, 지팡이 나무 | | `#704456` | `#A47384` |
| 지팡이 나무(결정 쪽) | | `#854972` | |
| 지팡이 결정 | `#8539BF` | `#D68CF9` | `#FDF6FF`(반짝임) |

나머지 네 캐릭터의 12색 팔레트는 WC 4.1~4.4에 있다(5.5절 테스트 벡터에서 위험색 0개를 검사한다).

**화면(UI)의 재료와 색** (UI 1.2) [제안]. 틀은 "느루가 버린 물건"(어두운 나무판, 누런 쪽지, 분필 글씨, 놋쇠 못). 금속 광택·유리 느낌의 매끈한 틀은 쓰지 않는다.

| 토큰 | 색 | 쓰는 곳 |
|---|---|---|
| `ink`(먹) | `#12131C` | 판 바깥 외곽선 2px, 글자 외곽선. 순수 검정 금지 |
| `wood_dark`(나무 어두움) | `#1F2130` | 화면 바탕, 눌린 칸 |
| `wood`(나무) | `#2A2D40` | 기본 판 |
| `wood_light`(나무 밝음) | `#3A3F5A` | 판의 윗변 1px, 올림 상태의 테 |
| `slate`(회청) | `#8E9AAE` | 보조 글자, 꺼진 선택 괄호, 일반 등급 테의 밝은 면 |
| `chalk`(분필 흰색) | `#F2EDE0` | 본문 글자, 아이콘의 기본 선 |
| `paper`(쪽지) / `paper_shade` | `#E8DDBF` / `#C9BB96` | 밝은 판(우편, 결과 카드, 말풍선) |
| `paper_text`(쪽지 위 글자) | `#2B2A33` | 쪽지 판 위의 글자 |
| `select_yellow`(선택 노랑) | `#FFE94A` | 선택 괄호 전용 |
| `gold`(금색) | `#FFD24A` | 골드, 영웅 등급, +13, 후계자 ("값진 것", FX 0.1) |
| `rare_blue`(희귀 파랑) | `#4F8CFF` | 희귀 등급 테 |
| `common_gray`(일반 회색) | `#9AA0AE` | 일반 등급 테 |
| `alert_amber`(알림 호박색) | `#F2A33A` | 경고·오류(경고 삼각, 오류 문구, "크게 손해 보는 거래") |
| `good_green`(좋음 초록) | `#6FD36B` | 접속됨, 준비 완료, 마감 여유 넉넉 |
| 속성색 | FX 0.1의 주색·보조색 | 속성 문양, 캐릭터 이름 밑줄, 강화 버튼의 색 입힘 |

색 규칙(UI 1.2) [제안]:
- 위험색(빨강 `#FF3B30`, 자홍 `#FF2E88`)은 **메뉴와 창에 쓰지 않는다**. HUD에서 허용되는 곳은 FX 0.1의 세 곳(피격 시 화면 가장자리, 체력 1칸 귀퉁이, 최종 격노 표시)뿐. 삭제·포기·신고 같은 "조심할 버튼"은 호박색 테 + 경고 삼각.
- 연두 `#B8F04A`는 폭주 변이 전용(변이 아이콘과 변이 칸에만).
- 청록 `#4FE3C1`은 비전 속성 전용("켜짐"·"성공"의 일반 표시로 쓰지 않는다).
- 금색은 값진 것에만(보통 버튼 강조 금지).
- 색으로만 구분하지 않는다: 등급은 테의 모양, 속성은 문양, 상태는 아이콘이 같이 알려 준다.

### 4.3 화면 그림 공통 규칙 (UI 1.1, 1.3, 1.4) [제안]

**배율과 격자** (UI 1.1)

| 항목 | 규칙 | 틱 환산 |
|---|---|---|
| 화면 | 1920×1080. 화면 그림은 전부 1배 | |
| 확대 | 정수 배(2, 3, 4배)만. 보간하지 않는다(점 필터) | |
| 격자 | 판·버튼의 위치·크기는 8px의 배수. 아이콘 안 여백은 2px 단위 | |
| 안전 여백 | 가장자리 48px 안쪽에 글자와 누를 수 있는 것. 전투 HUD만 16px까지 | |
| 최소 크기 | 누를 수 있는 것 32×32px. 목록 줄 높이 48px 이상 | |
| 다른 해상도 | 2560×1440은 여백을 늘리고 그림 1배, 3840×2160은 전체 2배. 1920보다 작은 창은 내려 그리지 않고 1920×1080 창을 최소로 둔다 | |
| 흔들림 | 화면 그림(HUD·메뉴)은 화면 흔들림을 받지 않는다 (FX 0.2) | |

```
function UiScale(W, H):    // 창의 실제 픽셀 크기
    s = min(W / 1920, H / 1080)  (정수 나눗셈)
    return max(1, s)            // 그 밖의 해상도는 [임시]: 정수 배 중 들어가는 최대, 남는 자리는 여백
function UiOrigin(W, H, s): return ((W - 1920*s)/2, (H - 1080*s)/2)   // 가운데 맞춤 [임시]
```

**상태 규약과 상태 기계** (UI 1.3)

| 상태 | 언제 | 보이는 모습 | MAN-ui 파일 상태 토큰 |
|---|---|---|---|
| 기본 `normal` | 아무 일도 없을 때 | 그린 그대로 | `normal` (또는 생략) |
| 올림 `hover` | 마우스가 위에 있을 때(마우스 전용) | 테가 나무 밝음으로 한 단계 밝아지고 안쪽에 1px 밝은 선. 선택 괄호는 붙지 않는다 | `hover` |
| 선택 `selected` | 키보드 커서가 있는 것, 또는 지금 고른 것 | 노란 모서리 괄호. 마우스로 눌러 고른 것에도 붙는다 | (괄호는 K-40 겹그림) |
| 누름 `pressed` | 누르고 있는 동안 | 그림 전체가 2px 아래로, 아랫변 그늘 2px이 사라진다 | `pressed` |
| 켜짐 `on` | 탭·토글·거르기가 켜져 있을 때 | 바탕이 나무 밝음으로 차고 아랫변에 분필 흰색 4px 줄 | `on` |
| 꺼짐 `disabled` | 지금 쓸 수 없을 때 | 채도 0, 밝기 60%, 글자 회청. 커서는 갈 수 있고 그때 괄호는 회청이며 아래 한 줄에 이유("골드가 모자란다" 등) | `disabled` |

```
상태 = (Enabled: bool, On: bool, Hovered: bool, Cursor: bool, Picked: bool, Pressed: bool), 입력 모드 Mode ∈ {Mouse, Keyboard}

입력 이벤트:
  KeyAny            → Mode = Keyboard; 모든 Hovered 표시를 지운다; 커서 대상에 괄호
  MouseMove         → Mode = Mouse; 괄호는 Picked인 것에만 남긴다; 포인터 아래 대상 Hovered = true
  PointerDown(t)    → if t.Enabled: t.Pressed = true
  PointerUp(t)      → if t.Pressed and 포인터가 t 위: Activate(t); t.Picked = true;  t.Pressed = false
  KeyConfirm(J)     → if cursor.Enabled: cursor.Pressed = true (키 누르는 동안) → 뗄 때 Activate
  Disable/Enable    → Enabled 갱신. Disabled면 Hovered·Pressed 무시

그릴 바탕 그림 선택 (우선순위):
  if !Enabled: disabled
  elif Pressed: pressed
  elif On and 그림에 on 상태가 있음: on
  elif Hovered and Mode == Mouse: hover
  else: normal
괄호 겹그림:
  if Mode == Keyboard and Cursor: 움직이는 괄호(2프레임) — 색은 Enabled ? 선택 노랑 : 회청
  if Picked: 멈춘 괄호 + 오른쪽 위 확정 체크 K-41  (커서와 확정이 한 화면에 함께 있을 수 있다)
  if Mode == Mouse: Picked인 것에만 괄호
```

**선택 괄호** (UI 1.3, GDD 13: 선택 표시는 노란 모서리 괄호로 통일)

| 항목 | 값 | 틱 환산 |
|---|---|---|
| 모양 | 대상의 네 모서리 바깥에 붙는 ㄱ자 넷. 선 굵기 4px, 선택 노랑 `#FFE94A`, 바깥에 먹색 2px 테 | |
| 간격 | 대상 가장자리에서 4px 바깥 | |
| 크기 | 작은 것: 팔 12px(한 변 64px 이하) / 보통: 20px(65~240px) / 큰 것: 28px(그보다 큰 카드와 판) | |
| 움직임 | 커서는 0.5초 주기로 2px 벌어졌다 오므라든다(2프레임). 번쩍이지 않는다 | 0.5초 = 30틱, 프레임당 15틱(MAN-ui `frame_ms` 250) |
| 확정 | 괄호가 멈추고 오른쪽 위에 확정 체크 K-41 | |
| 층 | 화면 그림 층에만. 전장 안의 적·탄·지팡이에는 그리지 않는다 | |
| 그림 | 한 모서리만 그리고 셋은 뒤집어 쓴다(MAN-ui `flip_for_other_corners: true`, `frame_offsets` [[-4,-4],[-6,-6]]) | |

```
function BracketSize(w, h):   e = max(w, h)   // "한 변" [임시: 긴 변]
    return e <= 64 ? Small(arm 12, 그림 16×16) : e <= 240 ? Medium(arm 20, 24×24) : Large(arm 28, 32×32)
```

- 선택 노랑은 금색·번개 레몬과 색상각이 가깝다 → 색이 아니라 **모양**(네 모서리 ㄱ자 + 먹색 테)으로 읽게 한다 (UI 1.3).

**글꼴** (UI 1.4, RD-ui)

| 요건 | 내용 |
|---|---|
| 글자 범위 | 현대 한글 완성형 11,172자 **전부**(2,350자 글꼴 금지), 한글 자모 낱자(ㄱ~ㅎ, ㅏ~ㅣ), 영문 대소문자, 숫자, 문장 부호, 기호 `… · — ~ + − × ÷ % ▲ ▼ ◀ ▶ ● ○ ◆ ◇ ★ → ←` |
| 원본 크기 | 12px급과 16px급 두 벌. 획이 1px로 떨어지는 비트맵 글꼴 |
| 숫자 | 고정폭 |
| 사용 조건 | 빌드에 넣어 직접 배포, 상업 이용과 글자 수정 허용(SIL OFL 같은 조건). "비상업 전용" 금지 |
| 그리는 법 | 정수 배로만 키운다. 점 필터, 안티에일리어싱 끔. 글자 위치는 정수 px |
| 아틀라스 | 글자당 여백 1px: 12px급 14×14, 16px급 18×18. 각각 2048×2048 한 장(한 줄 146자, 113자). 처음부터 전부 굽는다 |
| 채택(현물) | 12px: Galmuri11의 한글·기호 + Galmuri11 Condensed의 반각 ASCII. 16px: Neo둥근모 + 수제 기호 `− ★ → ←` 넷. 둘 다 11,172자. Galmuri v2.40.4, Neo둥근모 v1.601, SIL OFL 1.1 원문 동봉 (RD-ui) |

| ID | 이름 | 크기 | 만드는 법 | 줄 높이 | 쓰는 곳 |
|---|---|---|---|---|---|
| F-12 | 작은 글 | 12px | 12px급 1배 | 16 | 꼬리표, 수량 숫자, 머리 위 닉네임, HUD의 "지금 K" 줄, 채팅 시각 |
| F-16 | 본문 | 16px | 16px급 1배 | 24 | 본문, 목록, 채팅, 툴팁, 입력 칸 |
| F-24 | 버튼 글 | 24px | 12px급 2배 | 32 | 버튼, 탭, 판의 제목, 거래 최종 확인의 지팡이 정보 |
| F-32 | 제목 | 32px | 16px급 2배 | 40 | 화면 제목, 캐릭터 이름, "N일째 웨이브 M" |
| F-48 | 큰 제목 | 48px | 16px급 3배 | 56 | 결과의 승패, 정산의 기록 줄, 카운트다운 |
| F-64 | 타이틀 글 | 64px | 16px급 4배 | 72 | 결투 시작 화면의 이름, 큰 숫자가 필요한 곳 |

- 글자 크기는 이 여섯 가지만. 12px보다 작은 글자는 없다. 글자색 기본은 분필 흰색(어두운 판)과 쪽지 위 글자(밝은 판). 전장 위에 뜨는 글자(머리 위 닉네임, HUD의 글)에는 먹색 1px 외곽선을 코드로 두른다(줄 높이에 2px 여유) (UI 1.4).
- MAN-ui의 글꼴 자료: F-12 `cell` 14, `columns` 146, `baseline` 11, `glyph_map` `f-12_glyphs.json`; F-16 `cell` 18, `columns` 113, `baseline` 13, `glyph_map` `f-16_glyphs.json`. 24·32·48·64px는 해당 비트맵을 정수 배율로 키운다 (RD-ui).

숫자 그림 글꼴 (UI 1.4):

| ID | 에셋 | 크기(px) | 글자 | 장수 | 메모 |
|---|---|---|---|---|---|
| F-N1 | 화면 숫자 글꼴(작은 것) | 글자당 10×16 | `0123456789,.:%+−×/` 18자 | 1 | 분필 흰색으로 그리고 색을 입힌다. HUD 골드·점수·시간, 목록 값 |
| F-N2 | 화면 숫자 글꼴(큰 것) | 글자당 20×32 | 같은 18자 | 1 | 결과 점수, 리그 화면 IL, 결투 남은 시간 |
| F-N3 | 큰 확률 숫자 글꼴 | 글자당 80×128 | `0~9 %` 11자 | 1 | 강화 화면의 큰 확률 숫자(숫자 높이 128px) |
| F-N4 | "상한" 딱지 | 64×32 | 1 | 1 | 확률이 상한에 닿았을 때 숫자 옆 |

콤보 숫자(24×32), 피해 숫자(16×24), 단계 숫자(48×64) 글꼴은 FX 20.1·20.3 (UI 1.4).

### 4.4 파일 이름과 묶음 (UI 6) [제안]

| 항목 | 규칙 (UI 6) | 현물(MAN-ui) |
|---|---|---|
| 폴더 | `art/ui/kit/`(K), `art/ui/icon/`(I), `art/ui/font/`(F), `art/ui/screen/<화면 이름>/`(S) | 실제 루트는 `art/production/ui/`(8절 C-10). 화면 이름: `title, profile, offline, menu, expedition, hud, duel, loot, assembly, enhance, techniques, training, results, duel_results, settings, plaza, duel_rooms, market, mail_wardrobe, rankings, shared` |
| 파일 이름 | `<ID 소문자>_<영문 이름>_<폭>x<높이>[_<상태>].png`. 예: `k-20_button_large_48x64_hover.png`, `i-b1_block_fireball_48x48.png` | 627개 파일 모두 ID 소문자로 시작한다. 상태 토큰이 크기 앞에 오는 경우와 목록 밖의 토큰이 있다(C-11) |
| 상태 이름 | `normal`, `hover`, `selected`, `pressed`, `on`, `disabled` | 그 밖에 `empty`, `off`, `filled`, `locked`, `unavailable`, `editing`, `error`, `expanded`, `hero`, `rare`, `closed`, `open`, `expedition_locked` 등이 영문 이름 안에 들어 있다 |
| 프레임 | 한 파일에 가로로 이어 그린다. 프레임 수는 이름 끝에 `_f4` | 그대로. `width`·`height`는 프레임 하나의 크기, `frame_ms` 있음 |
| 9조각 | 원본 한 장 + 모서리 값(px)을 옆의 자료 파일에 | 매니페스트 `nine_slice_ltrb` = [왼, 위, 오른, 아래] |
| 아틀라스 | 공용 부품과 아이콘은 2048×2048 한 장씩, 화면 전용은 화면별로. 1920×1080 배경은 묶지 않는다 | `atlas/kit-0.png`, `atlas/icon-0.png` + 화면별 21장 = 23장. 아틀라스에 없는 16개 = 글꼴 6, 런처 아이콘 4, 1920×1080 배경 6 |
| 색 입힘 | 분필 흰색으로 그린 그림에 코드로 색을 곱한다. 색을 입히는 그림은 회색 단계만 | `tint`, `tint_edge`, `tint_rank_strip`, `tint_bottom_strip` 필드 |
| 필터 | 전부 점 필터, 밉맵 없음, 압축 없음 | `sampling: nearest`, `mipmaps: false`, `texture_compression: false` |

- ID 머리글자: K 공용 부품, I 아이콘, F 글꼴, S 화면 전용 그림 (UI 0).
- "9조각"은 네 모서리·네 변·가운데를 나눠 늘려 쓰는 틀(원본 한 장이 어떤 크기의 판도 만든다). "장수"는 그려야 하는 그림 파일의 장수(상태와 프레임을 모두 센 값), 코드·셰이더·글꼴로 만드는 것은 0 (UI 0).
- 한 아이콘을 여러 크기로 쓸 때는 큰 것을 줄이지 않고 **크기마다 따로 그린다**(24px 이하에서 획이 뭉개진다). 2배로 키워 쓰는 것은 따로 그리지 않는다. 아이콘은 분필 흰색 선 + 먹색 2px 외곽이 기본이고, 뜻이 색에 걸린 것(속성, 재화, 등급)만 색을 쓴다 (UI 3).
- **불러오기는 파일 이름을 해석하지 않는다.** 파일 이름 규칙은 사람이 찾기 위한 것이고, 코드는 매니페스트의 `id`·`file`·`width`·`height`·`frames`·`atlas`만 본다 [조율: C-11].

### 4.5 화면 그림 합계 (UI 7) [제안]

"장수"는 UI 4절까지의 에셋 표 장수의 합. 코드·셰이더·글꼴(0), 꾸미기 낱개 아이콘(I-M8), 5절의 다른 문서 그림은 들어 있지 않다.

| 묶음 | 내용 | 종류(줄) | 장수 |
|---|---|---|---|
| 글꼴 | 화면 숫자 글꼴 2, 큰 확률 숫자 글꼴, "상한" 딱지 (한글 글꼴은 고르는 것이라 세지 않는다) | 4 | 4 |
| 공용 부품: 판과 창 | K-01 ~ K-12 | 12 | 14 |
| 공용 부품: 버튼, 탭, 입력 | K-20 ~ K-38 | 19 | 63 |
| 공용 부품: 선택 표시와 작은 표시 | K-40 ~ K-47 | 8 | 20 |
| 공용 부품: 키와 마우스 | K-50 ~ K-55 | 6 | 21 |
| 공용 부품: 칸과 테 | K-60 ~ K-73, K-80, K-81 | 16 | 38 |
| 아이콘: 주문 블록 | 16종 × 2크기 + 종류 바탕 | 4 | 38 |
| 아이콘: 속성 문양 | 32px 6, 64px 5, 무 16px, 약점·저항 틀 | 4 | 14 |
| 아이콘: 지팡이 단계 테, 흔적 딱지 | I-G1, I-G2 | 2 | 6 |
| 아이콘: 리그 휘장과 순위 | I-L1 ~ I-L9 | 9 | 30 |
| 아이콘: 비기 | 15종 × 2크기 + 난이도 점 + 결정 비용 | 4 | 34 |
| 아이콘: 재료, 부적, 물건 | I-M1 ~ I-M9 | 9 | 27 |
| 아이콘: 재화 | 4종 × 3크기 | 4 | 12 |
| 아이콘: 능력치와 패시브 | I-S1 ~ I-S5 | 5 | 24 |
| 아이콘: 메뉴와 기능 | I-F1 ~ I-F13 | 13 | 107 |
| 화면 전용: 타이틀, 프로필, 메인 메뉴, 원정 준비 | S-02 ~ S-06 | 11 | 28 |
| 화면 전용: 전투 HUD, 결투 | S-08, S-09 | 12 | 29 |
| 화면 전용: 전리품, 야영과 조립, 강화, 트라이, 연습 | S-10 ~ S-14 | 20 | 52 |
| 화면 전용: 정산, 결투 결과, 설정 | S-16, S-17, S-19 | 7 | 18 |
| 화면 전용: 광장 | S-20 ~ S-24 | 26 | 60 |
| 화면 전용: 리그, 순위표, 비석, 공용 화면 | S-25, S-26 | 9 | 23 |
| **합계** | | **204** | **662** |

| 크기별 | 장수 |
|---|---|
| 1920×1080 배경 | 7 (타이틀, 메인 메뉴, 결투 시작, 정산, 장터, 순위표, 불러오는 화면) |
| 9조각 틀이 들어간 줄(늘려 쓰는 원본) | 121 |
| 그 밖의 고정 크기 그림 | 534 |

화면 수: 26개 항목(첫 실행 물음, 타이틀, 프로필·로그인, 오프라인 안내, 메인 메뉴, 원정 준비, 모드 진입, 전투 HUD, 결투, 전리품, 야영·조립, 강화, 트라이, 연습, 일시정지, 정산, 결투 결과, 기록, 설정·키 재설정, 광장 겹침 화면, 결투 게시판, 친구·살펴보기, 거래·장터·상점, 우편·옷장·느루, 리그·순위표·비석, 공용 화면) (UI 7).

현물 대조(MAN-ui): `assets` 627개 파일, 고유 ID 197개, 프레임 합 660, `external` 3개(S-0201 타이틀 본체, S-2405 느루 대화 초상, S-2407 행상 대화 초상). 문서의 204줄 중 코드로 만드는 7줄(K-10, K-37, K-47, K-72, I-B4, I-C4, I-M8)을 빼면 197 = 매니페스트 ID 수와 같다. ID별 차이는 8절 C-12, C-13, C-18.

### 4.6 캐릭터 그림 (WC 10.1) [제안]

옆 방향은 한쪽만 그리고 좌우로 뒤집는다 (WC 10).

**다섯 층(아래 → 위)**

| 층 | 내용 |
|---|---|
| 1. 기본 몸 | 지팡이를 뺀 몸 전체. 64×64 |
| 2. 반응 부품 | 열·스침·결정에 반응하는 작은 부품(달구의 김, 초롱의 등불 알 등) |
| 3. 야생 물 덧그림 | 2~4단계. 팔레트 교체 + 머리·어깨에 붙는 작은 그림 |
| 4. 치장 | 칸별 겹그림(머리 장식, 망토 무늬, 발자국) |
| 5. 지팡이 | 32×64, 손 위치에 붙는다. 방향이 "위"일 때는 몸 아래 층으로 내려간다. 지팡이 문서가 위 방향 겹침을 따로 정한 경우(WAND-chorong 2.2)는 그것을 따른다 |

- 프레임마다 적는 자료: 발밑 기준점, 손 위치, 머리 위치, 어깨 위치. 2~5층은 이 점에 붙는다.
- 손 모양은 캐릭터마다 "지팡이 쥔 손" 하나. 성에만 +13 고드름 홀용 "기둥을 짚은 손"을 하나 더 갖는다(서기·걷기·자세 걷기에 손 덧그림으로 얹는다).
- 초롱 위 방향 예외(WAND-chorong 2.2): 위 방향에서는 지팡이를 몸 오른쪽으로 6px 빼서 들고, 손잡이 위 20px부터는 몸 층보다 위에 그린다.

```
function CompositeOrder(charId, dir, wandType):
    base = [Body, ReactiveParts, WildStain, Cosmetics]
    if dir != Up: return base + [Wand]
    if wandType == wand.2:                       // 초롱 예외
        // 지팡이 그림을 손잡이 기준점(16,48)에서 위로 20px 되는 줄(y = 28)에서 둘로 나눈다
        lower = WandRows(28..63); upper = WandRows(0..27)
        return [lower(+6px x)] + base + [upper(+6px x)]
    return [Wand] + base
function AnchorMirror(dxRight):  return -dxRight     // 왼쪽 = 오른쪽 그림을 발밑 기준점 둘레로 좌우 반전, 기준점에서의 x 차이 부호만 바꾼다 [임시]
```

**기본 몸 (캐릭터 한 명분)**

| 동작 | 방향 | 프레임 | 장수 | 메모 | MAN-pl 클립 (fps, 반복) |
|---|---|---|---|---|---|
| 서기 | 3 | 4 | 12 | 숨쉬기 | `idle_{down,right,up}` (7, 반복) |
| 걷기 | 3 | 6 | 18 | | `walk_*` (10, 반복) |
| 자세 걷기 | 3 | 4 | 12 | 몸을 낮추고 지팡이를 내민 걸음. 반전 자세는 방향만 뒤집어 같이 쓴다 | `stance_walk_*` (9, 반복) |
| 발사 | 3 | 3 | 9 | 반동 | `fire_*` (14, 한 번) |
| 비기 시전(공용) | 3 | 4 | 12 | | `special_*` (12, 한 번) |
| 고유 비기 | 3 | 6 | 18 | 캐릭터마다 다른 동작 | `signature_*` (12, 한 번) |
| 피격 | 3 | 2 | 6 | | `hit_*` (12, 한 번) |
| 사망 | 1 | 6 | 6 | 지팡이만 남는 마지막 장 포함 | `death_down` (10, 한 번) |
| 앉기 | 1 | 2 | 2 | 광장 모닥불 | `sit_down` (4, 반복) |
| 감정표현 8종 | 1 | 4 | 32 | 인사, 박수, 도발, 절, 앉기, 춤, 지팡이 들어 올리기, 좌절. 앉기는 앉는 동작 4프레임 뒤 "앉기" 2프레임 반복. 지팡이 들어 올리기는 몸 동작만 | `wave_down`(7), `clap_down`(8), `taunt_down`(7), `bow_down`(8), `sit_down_down`(8), `dance_down`(9), `lift_wand_down`(8), `frustrated_down`(6) — 모두 한 번 |
| 강화 반응 | 1 | 2 + 2 + 4 + 4 | 12 | 기다림, 충전, 성공, 실패. 강화 화면 초상 자리에 2배로 놓는다 | `upgrade_wait_down`(5), `upgrade_charge_down`(5), `upgrade_success_down`(8), `upgrade_fail_down`(8) |
| 결투 결과 | 1 | 4 + 4 | 8 | 승리, 패배 | `duel_win_down`(8), `duel_loss_down`(8) |
| **합계** | | | **147** | 다섯 명 735장 | MAN-pl `frames: 147` × 5 |

그믐은 기준 그림 한 장(서기, 아래)이 있다. 정리 작업(16색, 불투명도, 지팡이 분리, 가린 부분 채우기)을 먼저 한다.

**반응 부품**

| 캐릭터 | 부품 | 크기 | 장수 |
|---|---|---|---|
| char.1 | 주둥이 김 3단계(보통, 임계, 잠금 직전) × 4프레임 | 16×16 | 12 |
| char.1 | 들썩이는 뚜껑 | 24×16 | 4 |
| char.2 | 등불 알 밝기 3단계 × 2프레임 + 번쩍 3프레임 | 16×16 | 9 |
| char.3 | 책갈피 끈 흔들림 4프레임 × 3방향 | 16×24 | 12 |
| char.3 | 떨어져 나가는 글자 조각 | 8×8 | 4 |
| char.4 | 얼음 왕관 조각 5개 × 크기 3단계 | 8×12 | 15 |
| char.4 | 서리 발자국 | 16×8 | 3 |
| char.5 | 눈 모양 5종(기본, 밝음, 깜빡임, 반달, 점) × 2방향 | 16×8 | 10 |
| char.5 | 빚 표시(머리 위 보라 고리) | 16×16 | 2 |

**야생 물 덧그림**: 단계 2~4마다 캐릭터당 팔레트 1벌 + 덧그림 2~3개 × 3방향. 캐릭터당 약 21장, 다섯 명 약 105장. 4단계의 움직이는 효과(불티, 떠다니는 등불 알, 잔상)는 FX의 입자 예산 안에서 그린다.

**치장 겹그림**: 칸은 머리 장식, 망토·옷 무늬(팔레트 교체), 발자국 효과, 앉기 동작 넷. 머리 장식은 24×24 × 3방향(캐릭터별). 정해진 것: 금 간 거울 조각(3.14절).

**초상과 큰 그림**

| 그림 | 크기 | 장수 | MAN-pl |
|---|---|---|---|
| 초상(HUD, 목록, 대화) | 64×64 | 캐릭터당 1 + 표정 3 = 4 | `portraits_64x64_f4.png` (256×64) |
| 캐릭터 선택 화면의 큰 그림 | 192×192 | 캐릭터당 1 | `selection_192x192.png` |
| 실루엣 확인용 | 32×32 | 캐릭터당 1(검은 덩어리) | `silhouette_32x32.png` |
| 타이틀·대표 이미지 | 따로 정함 | 그믐 중심 1 | (UI의 S-0201 외부 파일) |

### 4.7 전장, 광장, 돌마당 그림 (WC 10.2~10.5) [제안]

**무한 웨이브 전장 (10.2)**

| 그림 | 크기 | 장수 | 메모 |
|---|---|---|---|
| 풀 바닥 타일 | 64×64 | 4 | 섞어 까는 무늬 |
| 맨흙 타일 | 64×64 | 4 | |
| 풀과 맨흙의 경계 타일 | 64×64 | 16 | 변 4, 바깥 모서리 4, 안쪽 모서리 4, 얼룩 4 |
| 가시덤불 울타리 | 64×64 | 12 | 윗변 3, 아랫변 3, 왼쪽 2(오른쪽은 뒤집기), 모서리 4 |
| 돌무더기(울타리에 섞는 변형) | 64×64 | 3 | |
| 등장구(덤불 틈) | 128×64 | 방향 3 × (열린 모습 1 + 흔들림 4) = 15 | |
| 납작한 소품 | 64×64 또는 128×64 | 12 | 표지판, 물약병 2, 흔들의자, 과녁, 버섯 고리, 느루가 버린 물건 6 |
| 탑 밑동과 빨랫줄 | 512×128 | 1 + 빨래 흔들림 4 | 위쪽 가장자리 너머 |
| 느루의 창문 | 64×64 | 4 | 내려다보기, 물건 던지기 |
| 야영 모닥불 | 64×64 | 4 | MON 야영과 같이 씀 |
| 달빛 원(보스 웨이브) | 256×256 | 1 | |
| 달 | 96×96 | 1 | 화면 밖 위쪽 |
| 빗줄기, 물웅덩이 반사 | 화면 띠, 64×32 | 4, 3 | 소나기 |
| 안개 띠 | 512×192 | 2 | |
| 별똥 결정 | 32×32 | 예고 고리 4 + 낙하 6 + 반짝임 4 = 14 | |
| 까마귀와 쪽지 | 32×32 | 날기 4 + 쪽지 2 = 6 | 느루의 훈수, 광장 알림과 같이 씀 |

- 배치 자료 6벌(그림이 아니라 자료) — 날이 바뀔 때마다 돌려 쓴다(WC 2.2).
- 팔레트: 낮, 해질녘, 밤 3벌. 타일을 다시 그리지 않고 색 교체. 가장자리 윗면 테두리빛(호박색, 달빛)은 덧그림 2벌.
- 바닥 명도 35% 이하(채도 35% 이하) — 적 몸 명도 규칙이 기댄다 (MON 9.6, WC 2.4). 바닥 색: 낮 `#2F4A3A`/맨흙 `#4A4034`, 해질녘 `#3A2E3E`/`#4A3830`, 밤 `#161F3A`/`#222842` (WC 2.4).

**광장 (10.3)**

| 그림 | 크기 | 장수 | 메모 |
|---|---|---|---|
| 바닥 타일(풀, 다져진 흙, 돌길) | 64×64 | 풀은 전장과 같이 씀 + 흙 4 + 돌길 6 + 경계 12 | |
| 큰 모닥불 | 128×128 | 6 | 불꽃 반복 |
| 통나무 의자 | 64×64 | 3 | |
| 좌판(돗자리 3종, 나무 궤짝 2종) | 128×64 | 5 | 물건은 아이콘을 얹어 표현 |
| 결투 게시판 | 128×128 | 1 + 쪽지 펄럭임 2 | |
| 돌마당 가는 돌길(선돌 문 포함) | 128×128 | 1 | |
| 후계자 비석 | 96×128 | 1 + 이름 새겨질 때 빛 4 | |
| 느루의 흔들의자(앉은 느루 포함) | 96×96 | 졸기 4, 눈 뜨기 2, 말하기 2, 보상 건네기 4 = 12 | |
| 연습 허수아비 | 64×96 | 서기 1 + 맞는 반응 3 | 메인 메뉴의 연습과 같이 씀 |
| 행상 수레(행상 포함) | 128×96 | 수레 1 + 행상 서기 2 + 손짓 2 = 5 | |
| 까마귀 우편함 | 64×96 | 1 + 편지 있음 표시 2 + 앉은 까마귀 2 | |
| 한뎃벌 출입문 | 192×128 | 1 + 덤불 흔들림 3 | |
| 탑 밑동(광장 쪽) | 640×192 | 1 | 위쪽 배경 |
| 알림 까마귀 | 32×32 | 전장 것과 같이 씀 | +10 이상, +13 알림. 알림 띠(960×80)는 FX 20.2 |
| 장식 소품 | 64×64 | 8 | 천막, 빨랫줄, 솥, 장작더미 등 |
| 말풍선, 닉네임 판, 칭호 띠 | | | UI |

팔레트는 전장과 같은 3벌(색 교체). 강화 화면 배경(느루의 작업대)은 FX 20.1.

**돌마당 (10.4)**

| 그림 | 크기 | 장수 | 메모 |
|---|---|---|---|
| 돌 포장 타일 | 64×64 | 4 | 회청색 `#2C3442` 기준 |
| 분필 선(가운데 세로선, 시작 위치 표시 2) | 64×64 | 3 | 덧그림. 세로선은 x = 12.0(칸 경계)에 가운데, 시작 표시는 (6.0, 7.0)과 (18.0, 7.0)에 가운데 |
| 선돌 울타리 | 64×96 | 윗변 3, 아랫변 3, 옆 2, 모서리 4 = 12 | |
| 선돌 기둥 | 64×96 | 1 + 탄 맞을 때 돌가루 3 | 1×1칸 판정. 부서지지 않는다 |
| 관중 실루엣 | 64×64 | 6종 × 2프레임 = 12 | 플레이어 관전자는 실제 캐릭터를 줄여 세운다 |
| 관전하는 느루 | 96×96 | 광장의 것 + 박수 2 | |
| 흐린 하늘빛 덧그림 | 화면 | 1 | |
| 사용자 방 시간대 | | 색 교체 3벌 | |

돌마당 좌표의 px 환산(왼쪽 위 안쪽 칸 (0,0) 기준): x = 12.0 → 768px, (6.0, 7.0) → (384, 448)px, (18.0, 7.0) → (1152, 448)px (WC 2.6 환산).

**합계 (10.5)**: 캐릭터 기본 몸 735, 반응 부품 71, 야생 물 덧그림 105, 초상과 큰 그림 30, 전장 약 110, 광장 약 90, 돌마당 약 45. 치장 겹그림은 치장 목록이 정해진 뒤에 센다.

### 4.8 몬스터 그림 (MON 9) [제안]

공통: 1칸 = 64px. 방향 없이 한 장, 좌우 반전 [GDD 14]. 피격 번쩍, 잔상, 외곽선(금색·밝은 림), 예고 구역, 조각 튀김은 셰이더나 도형(그림 없음).

**9.1 일반 적** (캔버스 / 이동 / 예고·시전 / 공격 / 빈틈·기타 / 사망 / 합계 → MAN-mo 클립)

| 적 | 캔버스 | 이동 | 예고·시전 | 공격 | 빈틈·기타 | 사망 | 합계 | MAN-mo 클립(프레임) |
|---|---|---|---|---|---|---|---|---|
| 이끼 슬라임 | 64×64 | 2 | - | - | - | 2 | 4 | move 2, death 2 |
| 덤불 늑대 | 64×64 | 4 | 1 | 1 | 경직 2, 회피 1 | 2 | 11 | move 4, warn 1, lunge 1, stagger 2, dodge 1, death 2 |
| 선돌지기 | 96×96 | 2 | 내려찍기 2, 휩쓸기 1 | 2 | 빈틈 2 | 3 | 12 | move 2, slam_warn 2, sweep_warn 1, slam 1, sweep 1, open 2, death 3 |
| 갓버섯 술사 | 64×64 | 2 | 2 | 1 | 엄폐 1 | 2 | 8 | move 2, cast 2, fire 1, hide 1, death 2 |
| 꽈리 벌레 | 64×64 | 2 | 2 | - | - | - (폭발 효과) | 4 | move 2, ignite 2 |
| 솔침 고슴도치 | 64×64 | 2 | 2 | 1 | 빈틈 1 | 2 | 8 | move 2, aim 2, fire 1, recover 1, death 2 |
| 화분 족제비 | 64×64 | 둔덕 2 | 솟구침 2 | - | 지상 2, 파고들기 2 | 2 | 10 | burrow 2, rise 2, stand 2, dive 2, death 2 |
| 해거름 박쥐 | 64×64 | 3 | - | - | - | 2 | 5 | fly 3, death 2 |
| 도깨비불 | 64×64 | 3 | 2 | - | 깜박 이동 2 | 2 | 9 | hover 3, cast 2, blink 2, death 2 |
| 가시덩굴 | 64×96 | 대기 2 | 2 | - | 땅 뚫고 나오기 2 | 2 | 8 | idle 2, cast 2, emerge 2, death 2 |
| 엄니 멧돼지 | 96×64 | 4 | 2 | 2 | 기절 2 | 2 | 12 | move 4, warn 2, charge 2, stagger 2, death 2 |
| 북두꺼비 | 64×64 | 2 | 울음 3, 혀 1 | 1 | - | 2 | 9 | hop 2, call 3, tongue_warn 1, tongue 1, death 2 |
| **합계** | | | | | | | **100** | 100 |

**9.2 엘리트**

| 항목 | 크기 | 수량 | MAN-mo |
|---|---|---|---|
| 종별 머리 장식(본체 위에 겹침) | 64×32 (선돌지기·멧돼지는 96×32) | 12종 × 1장 | `elite.<base>.overlay` |
| 뿔 표식 | 16×16 | 1 | `elite.horn` (origin [8,12]) |
| 박쥐 여왕 급강하 | 64×64 | 1 | `elite.bat_dusk.dive` |
| 쌍둥이불 본체(불꽃 두 개) | 64×64 | 3 (떠 있기) | `elite.wisp_stray.twin` (hover 3) |
| 갈라진 슬라임, 불러낸 박쥐 | 일반 적 그림 재사용 | 0 | |

**9.3 보스**

| 보스 | 캔버스 | 이동 | 패턴별 | 빈틈·기절 | 단계 전환 | 사망 | 합계 | MAN-mo 클립 |
|---|---|---|---|---|---|---|---|---|
| 가마등 | 192×128 | 4 | 돌진 예고 2 + 돌진 2, 발구르기 3, 낙석 포효 2 | 기절 2 | 2 | 4 | 21 | walk 4, charge_warn 2, charge 2, stomp 3, roar 2, stun 2, phase 2, death 4 |
| 스승의 허상 | 64×96 | 떠 있기 3 | 발사 2, 비기 시전 2, 만개 흉내 3 | 빈틈 2 | 2 | 4 | 18 | hover 3, fire 2, cast 2, burst 3, open 2, phase 2, death 4 |
| 허상: 첫 만남 전용 | 64×96 | - | 거울에 금이 갔다가 메워짐 2, 내려다봄 1 | - | - | 안개로 풀려 사라짐은 사망 4장 중 뒤 2장 재사용 | 3 | crack 2, look 1 (같은 시트 `boss.mirror_phantom`) |
| 천 갓 | 128×160 | 2 | 고리 3, 구름 2, 부르기 2, 도약 4 | 빈틈 2 | 3 (뿌리 뽑기) | 4 | 22 | walk 2, ring 3, cloud 2, call 2, jump 4, open 2, phase 3, death 4 |
| 밤눈 (공중) | 192×128 | 날기 4 | 활강 2, 깃털 부채 3, 낙뢰·밭갈이 2 | - | 2 | 4 | 17 | fly 4, dive 2, fan 3, weather 2, phase 2, death 4 |
| 밤눈 (지상) | 96×128 | - | - | 내려앉음 2 | - | - | 2 | `boss.storm_owl.ground`: land 2 |
| 흰뿔 | 128×160 | 4 | 기둥 2, 휘두르기 3, 숨결 3, 돌진 예고 2 + 돌진 2 | 벽 충돌 2 | 2 | 4 | 24 | walk 4, pillars 2, sweep 3, breath 3, charge_warn 2, charge 2, impact 2, phase 2, death 4 |
| 밤삼킴 머리 | 96×96 | 2 | 물기 3, 침 2, 잠행 들어가기 2 + 솟구침 2 | 멈춤 2 | 2 | 3 | 18 | move 2, bite 3, spit 2, sink 2, rise 2, rest 2, phase 2, death 3 |
| 밤삼킴 마디 | 64×64 | 2 | 비늘 탄 깜박 1 | - | - | 1 | 4 | `boss.night_serpent.segment`: move 2, warn 1, death 1 |
| 밤삼킴 꼬리 | 64×64 | 2 | - | - | - | 1 | 3 | `boss.night_serpent.tail`: move 2, death 1 |
| 갓버섯 새싹(천 갓의 졸개) | 32×32 (실제 픽셀. 반 칸짜리 졸개라 64×64로 키우지 않는다) | 2 | - | - | - | 1 | 3 | `enemy.spore_seedling`: move 2, death 1 |
| **합계** | | | | | | | **135** | 135 |

- 허상이 드는 지팡이는 플레이어 손 지팡이 그림(32×64, 캐릭터 5 × 강화 14단계)을 회색조 셰이더로 재사용(새 그림 없음). 보스 초상(이름 띠용)과 보스 체력 바 아이콘은 화면 그림이라 UI 요청 표(아래)에 있다.

**9.4 탄, 구역, 효과** (현물은 MAN-fx `enemy/` 묶음)

| 항목 | 크기 | 프레임 | 쓰는 곳 | MAN-fx 열쇠 |
|---|---|---|---|---|
| 둥근 탄 (소, 판정 반지름 0.12칸) | 16×16 | 2 | 술사, 도깨비불, 각종 고리, 서리 숨결 | `round_small` |
| 둥근 탄 (대, 판정 반지름 0.18칸) | 24×24 | 2 | 보스 고리, 파편 | `round_large` |
| 솔침 | 24×8 | 1 | 고슴도치 | `pine_needle` |
| 깃털 | 24×12 | 2 | 밤눈 | `feather` |
| 얼음 파편 | 16×16 | 1 | 흰뿔 | `ice_shard` |
| 어둠 침 | 24×12 | 2 | 밤삼킴 | `shadow_needle` |
| 씨앗(포물선) + 그림자 | 16×16, 16×8 | 2, 1 | 가시덩굴 | `vine_seed`, `seed_shadow` |
| 가시밭 바닥 | 96×96 타일 | 2 | 가시덩굴, 좁은 들 | `thorn_ground` |
| 불씨 띠 | 64×64 타일 | 3 | 가마등 | `ember_lane` |
| 포자 구름 | 128×128 | 4 | 천 갓 | `spore_cloud` |
| 서리밭 | 160×160 | 2 | 흰뿔 | `frost_field` |
| 얼음 기둥 (솟음, 서 있음, 깨짐) | 64×96 | 2, 1, 3 | 흰뿔 | `ice_pillar` (6) |
| 바위 (떨어짐, 놓임, 깨짐) | 64×64 | 2, 1, 2 | 가마등 | `rock` (5) |
| 번개 줄기 | 64×128 (세로 반복) | 3 | 밤눈, 허상의 직선 번개 | `lightning_strike` |
| 흙 금(솟기 전 표시) | 64×32 | 2 | 족제비, 가시덩굴, 새싹, 밤삼킴 | `soil_crack` |
| 둔덕의 흙 알갱이 | 8×8 | 1 | 화분 족제비(항상 2개) | `soil_particle` |
| 폭발 | 128×128 | 5 | 꽈리 벌레, 마지막 숨 | `explosion` |
| 공용 사망 연기 | 48×48 | 4 | 모든 일반 적 | `death_smoke` |
| 엘리트 등장 빛기둥 | 32×128 | 3 | 엘리트 | `elite_column` |
| 금색 고리(엘리트 사망) | 셰이더 | - | | — |
| 북두꺼비 버프 고리 | 64×32 | 3 | 버프 받은 적 | `toad_buff` |
| 기절 별 | 32×16 | 3 | 멧돼지, 가마등 | `stun_star` |
| 커맨드 화살표(허상 머리 위) | 16×16 | 4종 × 1 | 스승의 허상. UI 입력 표시 그림 재사용 가능 | 없음 → UI K-53 16×16 재사용 [조율: C-16] |

판정 반지름 환산: 0.12칸 = 7.68px = Q32.32 515,396,076; 0.18칸 = 11.52px = Q32.32 773,094,113. 예고 구역(테두리, 채움, 빗금)은 도형으로 그린다(값은 FX 14.1).

**9.5 드랍과 표식** (전장 위, MAN-fx `loot/`)

| 항목 | 크기 | 프레임 | MAN-fx 열쇠 |
|---|---|---|---|
| 골드 동전 | 16×16 | 4 | `coin` (7fps 반복) |
| 블록 조각(등급 테두리 3종) | 24×24 | 2 × 3 | `block_common`, `block_rare`, `block_heroic` |
| 재료 꾸러미 | 24×24 | 2 | `material_pack` |
| 까마귀 보따리(첫 웨이브 3. 까마귀가 떨구고 감) | 64×64 | 4 | `crow_pack` |
| 한정 드랍 | 32×32 | 4 | `season_drop` |
| 체력 조각 | 16×16 | 2 | `health_shard` |
| 드랍 빛기둥 | 16×64 | 3 | `drop_column` |
| 모닥불 | 64×64 | 4 | `campfire` |
| 출발 표식 | 64×64 | 2 | `exit_marker` |

UI로 넘기는 요청(MON 9.5. 크기는 요청값이고 UI가 다르게 정하면 UI가 맞다):

| 항목 | 요청 크기 | 수량 | 쓰는 곳 | 현물/처리 |
|---|---|---|---|---|
| 속성 문양(약점·저항 공용) | 16×16 | 5종 | 피해 숫자, 보스 체력 바, 야영의 다음 웨이브 칸 | MAN-ui I-E3 6장(무 포함) [조율: C-12] |
| 약점·저항 기호(▲, ▼) | 8×8 | 2종 | 피해 숫자 | MAN-fx `damage_glyphs`(13자: 0~9, !, ▲, ▼)와 UI I-E4로 대신 [조율] |
| 변덕 아이콘 | 32×32 | 8종 | 웨이브 번호 옆, 야영 | 없음 (9절 U-10) |
| 틀 아이콘 | 32×32 | 5종 | 야영 | 없음 |
| 시간대 아이콘(낮, 해질녘, 밤, 보스) | 32×32 | 4종 | 야영, HUD | 없음 |
| "빌림" 꼬리표 | 32×16 | 1 | 전리품 카드, 조립 | 없음(K-69 점선 테는 있음) |
| 꾸러미 여는 연출 | 64×64 | 5프레임 | 정산 | 없음 |
| 강화석 3종, 하락 방지 부적, 시즌 주화 아이콘 | 32×32 | 5종 | 야영의 확정 재료, 정산 | UI가 48×48(I-M1, I-M3, I-C3)과 24×24(I-M2, I-M3, I-C2)로 정함 → UI가 맞다 [조율: C-14] |
| 귀환 가방 칸 표시(빈 칸, 찬 칸, 영웅 칸) | 32×32 | 3종 | 야영, 정산 | 없음(S-1601 가방 띠는 있음) |
| 보스 초상(이름 띠용) | 96×96 | 6장 | 보스 등장 연출 | 없음 (9절 U-01) |
| 보스 체력 바 아이콘 | 32×32 | 6장 | 보스 체력 바 | 없음 (9절 U-01) |
| 복수 추적기 | 공용 부품 | 새 그림 1종(거울 표식) | 메인 메뉴, 원정 준비, 야영, HUD, 정산, 기록 | S-2603 32×32 × 3(복수 중, 흠집 50% 이상, 복수 완료) |
| 복수 보상 줄(칭호, 머리 장식, 귀속 재료) | 공용 부품 | 0 | 정산 | — |

**9.6 전장**: 스테이지가 하나이므로 타일은 한 벌(64×64), 시간대는 팔레트 교체나 색 보정. 바닥은 명도 35% 이하. 보스 웨이브 전용: 전장 가운데 달빛 원(바닥 장식, 판정 없음). 밤 전용 장식: 반딧불(4×4 점, 셰이더 깜박임).

몬스터 그림 공통 규칙(MON 1.4, 5.2): 2등신 안팎, 어두운 남색 `#1B2440` 1px 외곽선, 재질마다 3~4단 명암, 빛은 왼쪽 위. 모든 적 발밑에 타원 그림자를 그리고 **그림자 중심이 판정 위치**. 밝은 림 2px `#EAF2FF` 항상(세기 낮 20%, 해질녘 40%, 밤·보스 60%). 플레이어 탄과 적 탄이 겹치면 적 탄을 위에. 가장 작은 몸(박쥐 28×20)도 날개 폭 48.

---

## 5. 기존 에셋 매니페스트와 불러오기

### 5.1 저장소에 있는 것 (현물) [제안]

| 경로(루트 기준) | 내용 | 매니페스트 |
|---|---|---|
| `art/production/ui/` | K·I·F·S 화면 그림 627장, 아틀라스 23장, 글꼴 비트맵, 굽는 스크립트 `source/build.py`(Pillow, fontTools 필요. 큰 배경은 480×270에서 그려 4배 저장) | `manifest.json` (+ `generated-art.json`) |
| `art/production/players/{dalgu,chorong,galpi,seonge,geumeum}/` | `body.png`(768×832, 147프레임), `portraits_64x64_f4.png`(256×64), `selection_192x192.png`, `silhouette_32x32.png` | `manifest.json` |
| `art/production/wands/{dalgu,…}/` | `hand.png`(448×384), `inspection.png`(1344×384), `icon.png`(896×64), `parts/*.png` | `manifest.json` |
| `art/production/monsters/` | 적·엘리트·보스 시트 37개, 미리보기 3장 | `manifest.json`, `README.md` |
| `art/production/effects/{player,special,ritual,enemy,loot,wand,status}/` | 효과 그림 303장 | `manifest.json` |
| `art/production/backgrounds/` | `title_1920x1080.png`, `title_preview.webp` | (UI `external` S-0201) |
| `art/production/world/` | `pixel_art.py`만 있다. 전장·광장·돌마당 타일과 S-2405·S-2407은 없다 | 없음 (C-13) |
| `art/player/dark_wizard/` 등 | 원본 기준 그림(`idle-reference-details.png` 64×64)과 옛 시안(`kettle_helm`, `lantern_hat`, `crystal_crown` 32×32) | 없음. 반입 대상 아님 |

### 5.2 UI 매니페스트 (`art/production/ui/manifest.json`)

최상위: `basis: "docs/ui-assets.md"`, `reference_resolution: [1920,1080]`, `sampling: "nearest"`, `mipmaps: false`, `texture_compression: false`, `frames: "horizontal strips; width and height are per frame"`, `nine_slice: "pixel insets [left, top, right, bottom]"`, `atlases`(그룹 → 페이지 경로 목록), `external`, `assets`(배열).

```csharp
public sealed class UiAssetEntry {
    public string Id;             // "K-20", "I-B1" … (여러 파일이 같은 Id를 공유한다 — 변형)
    public string File;           // "kit/k-20_button_large_48x64_hover.png" (ui 폴더 기준)
    public int Width, Height;     // 프레임 하나
    public int Frames;            // ≥ 1, 가로 띠
    public int? FrameMs;          // 프레임 시간(ms)
    public int[] NineSliceLtrb;   // [l,t,r,b] 또는 null
    public AtlasRef Atlas;        // { Page, Rect[x,y,w,h] } — 띠 전체 영역. 없으면 낱장 로드
    public string Element;        // "fire|lightning|arcane|frost|shadow|neutral" → 3.1 별칭으로 정규화
    public string BlockKind;      // "projectile|control|modifier"
    public object Tint, TintEdge, TintRankStrip, TintBottomStrip; // 색 입힘 지시
    public int[][] FrameOffsets;  // 프레임별 위치 보정(K-40: [[-4,-4],[-6,-6]])
    public bool FlipForOtherCorners;
    public int[] SpriteOrigin, Hotspot, SuggestedAnchor, FireAnchor, CompositeAnchor, RotationPivot, MovableMarkerCenter;
    public string Verbatim;       // 그림에 박힌 글자(S-0902 "대")
    public string Usage; public string[] SharedWith; public int[] LogicalResolution;
    // 글꼴 전용: Cell 또는 CellWidth/CellHeight, Columns, Baseline, GlyphMap, Characters
}
```

변형 열쇠(같은 Id 안에서 파일을 고르는 열쇠): `Variant = 파일 이름에서 "<id 소문자>_"를 떼고 "_<w>x<h>"와 그 뒤를 뗀 앞부분` + `State = 크기 뒤 토큰(없으면 normal)`. 예: `k-20_button_large_48x64_hover` → Variant `button_large`, State `hover`. `i-b1_block_fireball_48x48` → Variant `block_fireball`, State `normal`. 이 열쇠 추출은 불러올 때 한 번만 하고 표(딕셔너리)로 든다 [임시].

`external`: `S-0201 → ../backgrounds/title_1920x1080.png (variant "selected title illustration")`, `S-2405 → ../world/sprites/S-2405.png (manifest ../world/manifest.json)`, `S-2407 → ../world/sprites/S-2407.png`. 뒤의 둘은 파일이 없다(C-13).

**콘텐츠 id → UI 아이콘 변형 대응** [제안]

| 콘텐츠 | 아이콘 ID | Variant |
|---|---|---|
| `block.<k>` | I-B1(48), I-B2(24) | `block_<k>` |
| 블록 종류 | I-B3 | `plate_projectile`, `plate_control`, `plate_modifier` |
| `element.<k>` | I-E1(32, 무 포함), I-E2(64, 무 없음), I-E3(16) | `element_<UI 키>` (ice→`frost`, dark→`shadow`) |
| 약점/저항 틀 | I-E4 | `weakness`, `resistance` |
| `art.<k>` | I-T1(48), I-T2(24) | `<k>` |
| 난이도 점 | I-T3 | `difficulty_empty`, `difficulty_filled` |
| 결정 비용 | I-T4 | `diamond`(결정 1개), `debt`(빚 1개) |
| `passive.<k>` | I-S2 | `<k>` |
| `wand_trait.<k>` | I-S3 | `<k>` |
| `mat.stone_*` | I-M1(48), I-M2(24) | `stone_low|mid|high` |
| `charm.safeguard` | I-M3 | `safeguard_charm` (48, 24) |
| `item.material_bundle`, `item.crow_bundle` | I-M4 | `material_bundle`, `crow_bundle` |
| `item.season_fabric` | I-M5 | `season_fabric` (48, 24) |
| `item.wand_box` | I-M6 | `wand_box` |
| `cosmetic_cat.<k>` | I-M7 | `cosmetic_<k>` |
| `item.library_expansion`, `item.rename` | I-M9 | `library_expansion`, `rename` |
| `currency.*` | I-C1(16), I-C2(24), I-C3(48) | `gold`, `season_coin`, `starlight`, `expedition_purse` |
| `league.<k>` | I-L1(24), I-L2(48), I-L3(128), I-L4(덧테, 3크기) | `league_<k>`, `league_laurel_<k>` |
| 시즌 참가 휘장 | I-L5 | `season_participation` |
| 승급 화살 | I-L6 | `promotion` (2프레임, 250ms) |
| 순위 표식 | I-L7 | `heir`(1위), `disciple`(2~10위) |
| 순위 메달 | I-L8 | `place_1`, `place_2`, `place_3` |
| 순위 변동 | I-L9 | `rank_up`, `rank_down`, `rank_flat` |
| 지팡이 단계 묶음 | I-G1 | `wand_tier_0_to_3`, `wand_tier_4_to_6`, `wand_tier_7_to_9`, `wand_tier_10_to_12`, `wand_tier_13` |
| 흔적 딱지 | I-G2 | `scar` |
| `emote.<k>` | I-F10 | 3.15절 표 |
| 아이템 레벨 | I-S5 | `item_level` |
| 프로필 표지 | I-F12 | `profile_offline`(접힌 쪽지), `profile_server`(까마귀가 앉은 쪽지) |
| 기록 딱지 | I-F13 | `record`(최고 기록 갱신), `pending`(검증 대기), `verified`(검증 통과), `deadline`(마감 임박) |
| 우편 봉투 | I-F7 | `mail`(안 읽음), `mail_open`(읽음), `mail_attach`(첨부 있음), `mail_claimed`(받음) |
| 사람 관계 | I-F3 | `whisper`, `friend_add`, `person`(친구), `blocked`(차단), `report`, `trade`, `duel`(결투 도전), `follow`, `inspect`, `duel_invite` |
| 기능 | I-F2 | `search, sort, filter, refresh, back, plus, minus, info, warning, question, eye, eye_hidden, clock, hourglass, play, pause, spectate, copy, trash, pencil, pin` |

### 5.3 캐릭터 매니페스트 (`art/production/players/manifest.json`)

최상위: `source: "docs/world-characters.md §3, §10.1"`, `filter: "point"`, `mipmaps: false`, `canvas: [64,64]`, `pivotPx: [30,60]`, `mirrorRightForLeft: true`, `characters: { <폴더 열쇠>: {...} }`.

```csharp
public sealed class PlayerCharacterEntry {
    public string Body;        // "dalgu/body.png"
    public int Frames;         // 147
    public string Portrait;    // 64×64 × 4 띠
    public string Selection;   // 192×192
    public string Silhouette;  // 32×32
    public Dictionary<string, PlayerClip> Clips;   // 37개 클립 (4.6절 표)
}
public sealed class PlayerClip { public int Fps; public bool Loop; public PlayerFrame[] Frames; }
public sealed class PlayerFrame { public int[] Rect; public int[] Feet, Hand, Head, Shoulder; }  // 모두 프레임 안 px, 왼쪽 위 원점
```

- 클립 이름 = `<동작>_<방향>`, 방향 ∈ {down, right, up}. 왼쪽은 `right`를 반전(`mirrorRightForLeft`).
- 다섯 캐릭터의 클립 이름·fps·반복·프레임 수가 같고, 모든 프레임의 `feet` = (30, 60)이다(현물 확인).
- 폴더 열쇠 → char 번호: `dalgu→1, chorong→2, galpi→3, seonge→4, geumeum→5` (2.3절) [조율: C-05].
- 없는 것: 반응 부품, 야생 물 덧그림, 치장 겹그림, 성에의 "기둥을 짚은 손", 초상 표정의 이름(4프레임의 순서: 0 기본, 1~3 표정 [임시]) (C-22).

### 5.4 지팡이 매니페스트 (`art/production/wands/manifest.json`)

최상위: `source: "docs/wands/*.md"`, `handCanvas: [32,64]`, `grip: [16,48]`, `filter: "point"`, `mipmaps: false`, `sprites: {<폴더 열쇠>: …}`, `detachedEffects`.

```csharp
public sealed class WandSpriteEntry {
    public SheetRef Hand;        // { File, Cell=[32,64], Columns=14, Rows=["down_hold","down_aim","right_hold","right_aim","up_hold","up_aim"] }
    public SheetRef Inspection;  // { File, Cell=[192,192], Columns=7, Rows=2 }   — 단계 14개를 7×2로
    public SheetRef Icon;        // { File, Cell=[64,64], Columns=14 }
    public WandStagePoints[] Stages;   // 14개: { Stage, Head[x,y], Mutations[[x,y]…](2개, chorong 3개), Expansion[x,y] }
}
public sealed class DetachedEffect { public string File; public int[] Cell; public int Frames; public int[] Anchor; }
// chorong_max_lightning: 8×16 × 4, anchor [4,15];  seonge_orbit: 40×16 × 8, anchor [20,8];  geumeum_orb: 20×20 × 3, anchor [10,10]
```

셀 선택 [제안]:

```
function WandHandRect(stage, dir, pose):          // stage 0..13, dir ∈ {down, right, up}(left는 right 반전), pose ∈ {hold, aim}
    row = DirIndex(dir) * 2 + (pose == aim ? 1 : 0)       // down=0, right=1, up=2
    return (stage * 32, row * 64, 32, 64)
function WandInspectionRect(stage): return ((stage % 7) * 192, (stage / 7) * 192, 192, 192)
function WandIconRect(stage):       return (stage * 64, 0, 64, 64)
function AttachWand(charFrame, wandStage):         // 손잡이 기준점 (16,48)을 캐릭터 프레임의 Hand 점에 맞춘다
    wandTopLeft = charFrame.Hand - (16, 48)
```

- 단계별 점(`head`, `mutations`, `expansion`)은 단계마다 한 벌이고 방향·자세별로 따로 없다(C-21) → [임시] 모든 방향·자세에 같은 점을 쓰고, 왼쪽은 x를 `31 − x`로 반전한다.
- 겉모습(`skin_id`)별 시트는 아직 없다. 지금은 겉모습 = 기본 하나 [임시].

### 5.5 몬스터 매니페스트 (`art/production/monsters/manifest.json`)

최상위: `schema: 1`, `artwork: "hand-authored pixel sprites"`, `scaleMode: "nearest"`, `entries: {<에셋 ID>: …}`(37개).

```csharp
public sealed class MonsterEntry {
    public string Path;            // "enemy.slime_moss.png" (monsters 폴더 기준)
    public int[] Size;             // 프레임 크기 [w,h]
    public int[] Grid;             // 시트 칸 [열, 행]
    public int[] Origin;           // 기본 기준점(프레임 안 px)
    public Dictionary<string, MonsterFrame[]> Clips;
}
public sealed class MonsterFrame { public int[] Rect; public int[] Origin; }   // 프레임마다 기준점이 다를 수 있다
```

- **프레임별 `origin`을 써야 한다.** 22개 프레임이 기본값과 다르다(예: `enemy.bat_dusk` death (32,54), `boss.kiln_boar` (96,116)~(96,124), `boss.night_serpent` (48,66)~(48,90)) [제안: 현물].
- 기본 기준점: 발이 있는 적은 발 위치, 이동 방향에 따라 좌우 반전, 애니메이션 속도와 피격 판정은 게임이 정한다 (RD-mo).
- 허상의 지팡이는 플레이어 지팡이를 회색으로 렌더링해 손 위치에 붙인다. 밝은 림, 타격 번쩍임, 잔상, 공격 구역은 게임이 그린다. 엘리트 장식은 오버레이(종과 자세에 따라 붙는 위치를 맞춰야 한다). 쌍둥이불은 전용 본체 시트 (RD-mo).
- 엔트리 기준점 표(현물): 64×64 일반 적 (32,58); 가시덩굴 (32,90); 선돌지기 (48,90); 멧돼지 (48,58); 새싹 (16,26); 가마등·밤눈 (96,122); 허상 (32,90); 천 갓·흰뿔 (64,154); 밤눈 지상 (48,122); 밤삼킴 머리 (48,70), 마디·꼬리 (32,36); 오버레이 (32,26~31) / 96폭 (48,26~30); 뿔 (8,12); 급강하 (32,54); 쌍둥이불 (32,60).
- RD-mo는 "검수 상태를 `artwork`와 `missingOrProvisional`에 표시"한다고 했지만 매니페스트에는 `missingOrProvisional`이 없다(C-07). 로더는 이 필드를 선택 항목으로 받는다.

### 5.6 효과 매니페스트 (`art/production/effects/manifest.json`)

최상위: 묶음 7개(`player` 55, `special` 89, `ritual` 77, `enemy` 31, `loot` 11, `wand` 15, `status` 25 = 303개, 프레임 합 1,201) + `notes`(`colorSources: ["docs/DECISIONS.md §2.2","docs/effects.md §0.1"]`, `origin: "top-left"`, `filter: "point"`, `mipmaps: false`).

```csharp
public sealed class FxEntry { public string Path; public int[] Size; public int[][] Frames; public int Fps; public bool Loop; }
// Path 예: "player/hit_fire_32x32_f3.png" — 이름 규칙 <열쇠>_<w>x<h>_f<N>.png (프레임 1이어도 _f1)
```

- 속성별 묶음 열쇠의 속성 접미사: `fire`, `lightning`, `arcane`, `ice`, `dark` (정식 키와 같다).
- 블록 탄 그림 대응 [조율: C-02]: `block.magic_bolt → mana_bolt`(16×16×2), `block.fireball → fireball`(32×24×4), `block.chain_lightning → chain_lightning`(64×16×2), `block.ice_spike → ice_lance`(12×28×2) + `ice_muzzle`, `ice_tail`, `block.shadow_orb → shadow_orb`(40×40×4) + `shadow_orb_cast`, `shadow_orb_ripple`, `shadow_orb_fade`, 작은 암흑 탄 기준 `shadow_small`(16×16×2). `piercing`(24×12×2)의 대응 블록은 미정(9절 U-17). `block.boomerang` 탄 그림 없음.
- 비기 그림 대응 [조율: C-03]: ① `release_<el>`, ② `muzzle_storm_<el>` + `dodge_dust`, ③ `fan_<el>`, ④ `wing_<el>`, ⑥ `spear_shaft_<el>`/`spear_tip_<el>` + `break_armor`, ⑦ `purge_steam`, ⑧ `octagram_<el>`, ⑨ `thunderlance_shaft_<el>`/`thunderlance_tip_<el>` + `just_star`, ⑩ `bloom_<el>` + `petal_<el>`, 발동 문양 `cast_seal_01`~`cast_seal_15`, 기술 이름 띠 `technique_name_ribbon`(192×32), `technique_name_ribbon_large`(320×48), 불가마 `kiln_border` + `kiln_ember`, 벼락 질주 `lightning_dash`, 베껴 쓰기 `arcane_copy` + `scattered_pages`, 얼음 병풍 `ice_screen_horizontal`/`ice_screen_vertical`(192×96 × 10 = 솟음 3 + 서 있음 1 + 금 감 2 + 깨짐 4) + `ice_screen_ripple` + `ice_screen_shard`, 그림자 손 `shadow_pool`, `shadow_hand`, `shadow_arm`, `shadow_fist`.
- 강화 의식(`ritual/`): 속성별 `seal_<el>`(240×240), `outer_glyph_<el>`(480×480×4), `charge_<el>`(96×96×6), `success_column_<el>`(192×540×6), `max_column_<el>`(192×540×8), `success_mote_<el>`(16×16×3), `break_pieces_<el>`(64×64×18), `extinguish_<el>`(96×96×5), `ward_ribbon_<el>`(128×128×6), `stage_hold_<el>`(128×96×4); 공용 `seal_base`, `gold_burst`, `gold_aureole`, `failure_dust`, `failure_smoke`, `artisan_ember`(32×32×6 = 오름 4 + 앉음 2), `ward_burn`, `slot_open`, `mutation_vine`(192×256×24 = 8+4+6+6), `trace_fragment`, `screen_crack`(1920×1080×3), `max_sky_band`(1920×240×4), `offering_slot`, `ward_slot`, `artisan_slot`, `ward_attached`, `mutation_card`(480×272), `result_card`(560×320×2), `title_ribbon_normal`/`_gold`(720×96×4), `plaza_notice_normal`/`_gold`(960×80×3), `news_line_normal`/`_gold`(960×40), `stage_digits_tint`/`_gold`/`_muted`(48×64×11).
- FX 20.1·20.2와 대조해 없는 것: 변이 아이콘 32×32 × 6, 변이 표식 고리 32×32 × 2, "지켰다" 도장 96×48 × 3, 깨지는 조각의 두 번째 벌(FX 20.1 "18개 × 2"; 현물은 속성마다 18프레임 한 벌), 느루 반응 64×64(2/3/2), 강화 화면 배경(작업대 1920×1080), 흔적 가림 아이콘 16×16, 지팡이 고르기 화살표 24×24, 모닥불 속성색 겹그림 96×128 × 4 (C-15). ("부적이 탐" 64×96 × 6은 `ward_burn`, "하락 없음의 받침"은 `stage_hold_<el>`, "실패: 꺼지는 겹그림"은 `extinguish_<el>`로 있다.)

### 5.7 통합 에셋 레지스트리 (인터페이스) [임시]

게임 코드는 매니페스트 형식 다섯 가지를 직접 보지 않고 하나의 레지스트리를 본다.

```csharp
public enum AssetPack { Ui, Character, Wand, Monster, Fx, World, Background }
public readonly struct AssetKey {
    public readonly AssetPack Pack;
    public readonly string Id;       // Ui: "I-B1"; Character: "char.1"; Wand: "wand.1"; Monster: "enemy.slime_moss"; Fx: "player/hit_fire"
    public readonly string Variant;  // Ui: "block_fireball"; Character/Monster: 클립 이름; Wand: "hand"|"inspection"|"icon"
    public readonly string State;    // Ui 상태, 또는 null
}
public interface IAssetCatalog {
    bool TryGetSprite(AssetKey key, int frame, out SpriteRef sprite);   // 텍스처 + 사각형 + 기준점 + 9조각
    bool TryGetClip(AssetKey key, out ClipRef clip);                    // 프레임 목록 + 시간
    AssetKey Placeholder(AssetKey missing);                             // 없을 때의 대체
}
```

없는 그림의 대체 [임시]: 원하는 크기의 회청 `#8E9AAE`·나무 어두움 `#1F2130` 8px 바둑판 + F-12로 Id 글자. **위험색·금색·연두·청록을 쓰지 않는다**(4.2절). 대체를 쓴 Id는 개발 빌드에서 한 번 기록한다.

불러오기 절차:

```
function LoadAll(root = "art/production"):
    reg = new Registry()
    ui = Json(root/ui/manifest.json)
    for e in ui.assets:
        (variant, state) = SplitUiName(e.id, basename(e.file))
        if e.element: e.element = ElementAlias[e.element]
        reg.Add(Ui, e.id, variant, state, e)
    for (id, ext) in ui.external:
        if Exists(root/ui/ext.file): reg.Add(Ui, id, "external", null, ext) else reg.MarkMissing(Ui, id)
    pl = Json(root/players/manifest.json)
    for (slug, c) in pl.characters: n = SlugToChar[slug]; for (clip, v) in c.clips: reg.Add(Character, "char."+n, clip, null, v)
    wd = Json(root/wands/manifest.json)
    for (slug, w) in wd.sprites: n = SlugToChar[slug]; reg.Add(Wand, "wand."+n, …)
    mo = Json(root/monsters/manifest.json)
    for (id, m) in mo.entries: for (clip, frames) in m.clips: reg.Add(Monster, id, clip, null, frames)
    fx = Json(root/effects/manifest.json)
    for group in [player, special, ritual, enemy, loot, wand, status]: for (k, v) in fx[group]: reg.Add(Fx, group+"/"+k, null, null, v)
    Validate(reg)      // 5.7.1
    return reg
```

5.7.1 반입 검사(빌드 단계) [제안]:
1. 모든 파일이 존재하고 PNG 크기 = (`width × frames`, `height`) 또는 매니페스트 사각형을 담는다.
2. 필터 점, 밉맵 없음, 압축 없음(UI 6, MAN-*).
3. 플레이어 쪽 그림(Character, Wand, Ui, Fx의 player/special/wand/ritual)에 위험색 픽셀 0개(4.2절). 단 FX의 위험색 전용 그림(예고·적 탄)은 `enemy/` 묶음에만 있어야 한다 [임시].
4. 1920×1080 배경은 아틀라스에 넣지 않는다.
5. 콘텐츠 id마다 필요한 아이콘이 있는지(3절 표 → 5.2 대응표) 보고서로 낸다(없으면 대체 사용).

---

## 6. 알고리즘 요약 (구현 순서와 의존)

순서(의존 관계)만 적는다(DEC 1.16).

1. `ContentId` 문법 검사기와 enum 표(3절) — 의존 없음.
2. 이름 표 파일과 `ResolveTemplate`·`FinalSound`(2.4절) — 1에 의존.
3. 폭·자르기·숫자 형식(2.5절) — 글꼴 자료(F-12/F-16 `glyph_map`)에 의존.
4. 매니페스트 로더 다섯 개 → 통합 레지스트리(5.7절) — 1에 의존(슬러그·별칭 표).
5. 반입 검사(위험색 검사기 포함, 4.2절, 5.7.1) — 4에 의존.
6. UI 상태 기계와 선택 괄호(4.3절) — 4에 의존.
7. 캐릭터 합성 순서·지팡이 셀 선택·기준점 반전(4.6, 5.4절) — 4에 의존.
8. 칭호·야생 물·보스 순번·시간대 함수(3.8, 3.9, 3.12, 3.14절) — 1, 2에 의존.

---

## 7. 테스트 벡터

형식: `TV-번호 | given | when | then`. 모두 자동 테스트로 만든다.

### 7.1 식별자

| TV | given | when | then |
|---|---|---|---|
| TV-001 | 문자열 `char.1` | 문법 검사 | 통과 |
| TV-002 | `block.fireball` | 문법 검사 | 통과 |
| TV-003 | `Block.Fireball` | 문법 검사 | 실패(대문자) |
| TV-004 | `fireball` | 문법 검사 | 실패(네임스페이스 없음) |
| TV-005 | `enemy.slime_moss` | 문법 검사 | 통과 |
| TV-006 | `elite.boar_tusk` | 문법 검사 | 통과 |
| TV-007 | 별칭 표 | `Alias("master")` | `npc.master` |
| TV-008 | 별칭 표 | `Alias("arena")` | `place.arena` |
| TV-009 | 블록 표 | 개수 | 16, 투사체 6 / 제어 3 / 변형 7 |
| TV-010 | 블록 표 | 등급별 개수 | 일반 6(마력탄, 2갈래, 벽 튕김, 크기 2배, 관통, 서리), 희귀 6, 영웅 4 |
| TV-011 | 블록 표 | 속성별 투사체 | 화염: fireball / 번개: chain_lightning / 비전: magic_bolt, boomerang / 냉기: ice_spike / 암흑: shadow_orb (다섯 속성 모두 1개 이상) |
| TV-012 | 비기 표 | 개수 | 15 = 공용 10 + 고유 5, 고유는 char.1~5에 하나씩 |
| TV-013 | 변이 표 | 개수 | 6 |
| TV-014 | 시설 표 | 개수 | 10 |
| TV-015 | 일반 적 표 | 개수 | 12, 졸개 1 별도 |
| TV-016 | 엘리트 표 | 각 엘리트의 바탕 | 모두 `enemy.<같은 키>`가 존재 |
| TV-017 | `char.N` | 속성 코드 | N (1 화염, 2 번개, 3 비전, 4 냉기, 5 암흑) |
| TV-018 | UI 매니페스트 원소 값 `frost` | `ElementAlias` | `ice` |
| TV-019 | UI 원소 값 `shadow` | `ElementAlias` | `dark` |
| TV-020 | 감정표현 표 | 개수와 순서 | 8: 인사, 박수, 도발, 절, 앉기, 춤, 지팡이 들어 올리기, 좌절 |
| TV-021 | 채팅 채널 표 | 서버 전체 채널 | 없음(DEC 2.7). 7개(시스템 포함) |

### 7.2 약점·저항 배분 (MON 2.5 검산)

| TV | given | when | then |
|---|---|---|---|
| TV-030 | 일반 12 + 보스 6의 약점 | 속성별 개수 | 화염 4, 번개 4, 비전 3, 냉기 4, 암흑 3 (합 18, 최대-최소 = 1) |
| TV-031 | 같은 집합의 저항 | 속성별 개수 | 화염 3, 번개 4, 비전 3, 냉기 4, 암흑 4 (합 18, 차 1) |
| TV-032 | 일반 적 12만 | 약점 속성별 | 화염 3, 번개 2, 비전 2, 냉기 3, 암흑 2 (차 1) |
| TV-033 | 보스 6만 | 약점 속성별 | 화염 1, 번개 2, 비전 1, 냉기 1, 암흑 1 (차 1) |
| TV-034 | 모든 종 | 약점 == 저항인 종 | 0개 |
| TV-035 | 속성 보스 5(천 갓 제외) | 저항 == 자기 속성 | 5개 모두 참 |
| TV-036 | 웨이브 1~4에 처음 나오는 5종(슬라임, 벌레, 늑대, 술사, 고슴도치) | 저항 == 비전 | 0개 |

### 7.3 보스 순번과 시간대

| TV | given | when | then |
|---|---|---|---|
| TV-040 | phantomFirst = true | `BossForWave(10)` | (`boss.mirror_phantom`, first_encounter) |
| TV-041 | phantomFirst = false | `BossForWave(10)` | `boss.kiln_boar` |
| TV-042 | — | `BossForWave(20)` | `boss.mirror_phantom` (normal) |
| TV-043 | — | 30 / 40 / 50 / 60 | spore_queen / storm_owl / frost_goat / night_serpent |
| TV-044 | — | 70 / 80 / 130 / 140 / 180 | kiln_boar / mirror_phantom / kiln_boar / mirror_phantom / night_serpent |
| TV-045 | — | `BossForWave(15)` | 없음 |
| TV-046 | phantomFirst = true | `BossForWave(70)` | kiln_boar (첫 만남 플래그는 10에만) |
| TV-047 | — | `TimeOfDay(1)`, `(4)`, `(5)`, `(7)`, `(8)`, `(9)`, `(10)` | day, day, dusk, dusk, night, night, boss |
| TV-048 | — | `TimeOfDay(23)` | day (3일째의 세 번째) |
| TV-049 | — | `TimeOfDay(27)`, `(30)` | dusk, boss |
| TV-050 | — | `DayNumber(1)`, `(10)`, `(11)`, `(23)` | 1, 1, 2, 3 |
| TV-051 | 시간대 | 밝은 림 세기 | day 20%, dusk 40%, night 60%, boss 60% |

### 7.4 이름 표와 틀

| TV | given | when | then |
|---|---|---|---|
| TV-060 | 초기 이름 표 | `Name("char.1")` … `("char.5")` | "제자 1" … "제자 5" |
| TV-061 | 초기 이름 표 | `Name("master")` | "느루" (별칭 경유) |
| TV-062 | 초기 이름 표 | `Name("field")`, `("plaza")`, `("arena")` | "한뎃벌", "첫모닥 야영지", "돌마당" |
| TV-063 | 틀 `엄청엄청 위대한 {char.5}` | 해석 | "엄청엄청 위대한 제자 5" |
| TV-064 | 틀 `title.stage.1.b4` | 해석 | "엄청엄청 위대한 제자 1" |
| TV-065 | `{char.1}{이/가}` | 해석 | "제자 1이" (1 = 일, 받침) |
| TV-066 | `{char.2}{이/가}` | 해석 | "제자 2가" (2 = 이) |
| TV-067 | `{char.3}{을/를}` | 해석 | "제자 3을" (3 = 삼) |
| TV-068 | `{char.4}{을/를}` | 해석 | "제자 4를" (4 = 사) |
| TV-069 | `{char.5}{은/는}` | 해석 | "제자 5는" (5 = 오) |
| TV-070 | `{char.1}{(으)로}` | 해석 | "제자 1로" (ㄹ 받침) |
| TV-071 | `{char.3}{(으)로}` | 해석 | "제자 3으로" |
| TV-072 | 이름 표에서 char.1 = "달구" | `{char.1}{이/가}` | "달구가" |
| TV-073 | char.3 = "갈피" | `{char.3}{을/를}` | "갈피를" |
| TV-074 | `{wand.1}{을/를}` (끓는 부지깽이) | 해석 | "끓는 부지깽이를" |
| TV-075 | `{wand.4}{을/를}` (고드름 홀) | 해석 | "고드름 홀을" (ㄹ 받침은 을/를에서 받침 있음) |
| TV-076 | `{wand.4}{(으)로}` | 해석 | "고드름 홀로" |
| TV-077 | 틀 `제 {n}대 후계자`, n = 3 | 해석 | "제 3대 후계자" |
| TV-078 | 틀 `제{n}기 수제자`, n = 2 | 해석 | "제2기 수제자" |
| TV-079 | 틀 `{{char.1}}` | 해석 | 글자 그대로 "{char.1}" |
| TV-080 | 이름 표에 없는 열쇠 `boss.unknown` | `Name` | 열쇠 문자열을 회청으로, 경고 1회 |
| TV-081 | `FinalSound("제자 10")` | — | 끝 자리 0 = 영 → (받침 있음, ㄹ 아님) [임시 규칙 U-06] |

### 7.5 칭호와 야생 물

| TV | given | when | then |
|---|---|---|---|
| TV-090 | stage 0, 3, 4, 6, 7, 9, 10, 12, 13 | `StageBand` | 0, 0, 1, 1, 2, 2, 3, 3, 4 |
| TV-091 | stage −1 또는 14 | `StageBand` | 오류 |
| TV-092 | char.1, stage 5 | 칭호 | "따끈한" |
| TV-093 | char.5, stage 8 | 칭호 | "달 없는 밤의" |
| TV-094 | char.3, stage 11 | 칭호 | "꽤 위대한 책벌레" |
| TV-095 | char.2, stage 2 | 칭호 | "졸린" |
| TV-096 | char.4, stage 13 | 칭호 | "엄청엄청 위대한 제자 4" |
| TV-097 | char.1 지팡이가 +7에서 +6으로 떨어짐 | 칭호 | "펄펄 끓는" → "따끈한"(내려간다), 최고 기록은 +7 유지 |
| TV-098 | 누적 웨이브 0, 99, 100, 499, 500, 1999, 2000, 1,000,000 | `WildStainStage` | 1, 1, 2, 2, 3, 3, 4, 4 |
| TV-099 | 서버 누적 150, 오프라인 누적 20 | 각 프로필의 야생 물 | 서버 2, 오프라인 1 (따로 센다) |
| TV-100 | 넘긴 최고 웨이브 9 / 10 / 30 / 100 | 기록 칭호 | 없음 / first_night / + three_nights / + ten_days |
| TV-101 | 첫 만남에서 쓰러진 뒤 웨이브 10을 넘김 | 기록 칭호 | first_night 부여 |

### 7.6 위험색 검사기

| TV | 색 | (H°, S%, V%) | `InDangerRange` | `IsProperDangerUse` |
|---|---|---|---|---|
| TV-110 | `#FF3B30` 빨강 | (3.19, 81.18, 100) | 참 | 참 |
| TV-111 | `#FF2E88` 자홍 | (334.16, 81.96, 100) | 참 | 참 |
| TV-112 | `#A3004E` 적 탄 외곽선 | (331.29, 100, 63.92) | 거짓(명도 < 70) | 거짓 |
| TV-113 | `#7A336A` 그믐 후드 | (313.52, 58.2, 47.84) | 거짓 | — |
| TV-114 | `#704456` 그믐 장화 | (335.45, 39.29, 43.92) | 거짓(색상각은 범위 안이지만 채도·명도가 낮다) | — |
| TV-115 | `#B25A9B` 그믐 후드 밝은 면 | (315.68, 49.44, 69.8) | 거짓 | — |
| TV-116 | `#FF8A1F` 화염 주황 | (28.66, 87.84, 100) | 거짓 (주황 색상각 25도 이상 규칙도 만족) | — |
| TV-117 | `#FF4040` | (0, 74.9, 100) | 참 | 거짓(채도 < 80) |
| TV-118 | `#FF6040` | (10.05, 74.9, 100) | 참 | 거짓 |
| TV-119 | `#FF2659` | (345.9, 85.1, 100) | 참 | 참 |
| TV-120 | `#FF4D4D` | (0, 69.8, 100) | 거짓(채도 69.8 < 70) | — |
| TV-121 | `#B3B3B3` 회색 | (0, 0, 70.2) | 거짓(채도 0, 색상각 없음) | — |
| TV-122 | `#FFFFFF` | (0, 0, 100) | 거짓 | — |
| TV-123 | `#F2A33A` 알림 호박색 | (34.24, 76.03, 94.9) | 거짓 | — |
| TV-124 | `#FFE94A`, `#FFD24A`, `#B8F04A`, `#4FE3C1`, `#9FDCFF`, `#8A5CFF` | — | 모두 거짓 | — |
| TV-125 | 다섯 캐릭터 팔레트 64색(달구 12, 초롱 12, 갈피 12, 성에 12, 그믐 16) | 위험색 개수 | 0 |
| TV-126 | 금 간 거울 조각 색 `#AEB6C2`, `#F4F1E6` | 검사 | 거짓, 거짓 |
| TV-127 | 대체 그림 색 `#8E9AAE`, `#1F2130` | 검사 | 거짓 |

### 7.7 UI 규칙

| TV | given | when | then |
|---|---|---|---|
| TV-140 | 대상 64×64 | `BracketSize` | 작은 것(팔 12, 그림 16×16) |
| TV-141 | 대상 65×40 | `BracketSize` | 보통(팔 20, 24×24) |
| TV-142 | 대상 240×336 | `BracketSize` | 큰 것(팔 28, 32×32) [임시: 긴 변] |
| TV-143 | 대상 240×100 | `BracketSize` | 보통 |
| TV-144 | 창 1920×1080 | `UiScale` | 1 |
| TV-145 | 창 2560×1440 | `UiScale` | 1 (여백 증가) |
| TV-146 | 창 3840×2160 | `UiScale` | 2 |
| TV-147 | 창 1600×900 | 창 크기 요청 | 1920×1080으로 최소화(내려 그리지 않는다) |
| TV-148 | 꺼진 버튼에 키보드 커서 | 그림 | `disabled` 바탕 + 회청 괄호 + 이유 한 줄 |
| TV-149 | 마우스로 올린 버튼 | 그림 | `hover`, 괄호 없음 |
| TV-150 | 마우스 올림 상태에서 키보드를 누름 | 상태 | 올림 표시 사라지고 커서 대상에 괄호 |
| TV-151 | 키보드 커서 상태에서 마우스 이동 | 상태 | 괄호는 Picked인 것에만 남는다 |
| TV-152 | 켜진 탭을 누르고 있음 | 바탕 | `pressed` (우선순위가 on보다 높다) |
| TV-153 | 커서 괄호 애니메이션 | 주기 | 0.5초(30틱), 2프레임, 프레임당 250ms(15틱), 오프셋 [-4,-4] → [-6,-6] |
| TV-154 | 숫자 48200 | `FormatInt` | "48,200" |
| TV-155 | 999 / 1000 / 0 | `FormatInt` | "999" / "1,000" / "0" |
| TV-156 | "제자 1", F-16 | `TextWidth` | 16+16+8+8 = 48 [임시: 공백 절반] |
| TV-157 | 한글 8자 이름, F-16 | 이름 칸 128px에 `Fit` | 자르지 않음 |
| TV-158 | "밑줄 긋는 참나무 지팡이"(한글 10 + 공백 3 → 폭 160+24 = 184), F-16, 칸 128px | `Fit` | 끝을 "…"로 자른 문자열, 전체 폭 ≤ 128 |
| TV-159 | 글자 크기 목록 | 허용 크기 | {12, 16, 24, 32, 48, 64}만. 10px 요청은 오류 |
| TV-160 | F-24 | 원본 | 12px급 2배, 줄 높이 32 |
| TV-161 | F-48 | 원본 | 16px급 3배, 줄 높이 56 |
| TV-162 | F-12 아틀라스 | 칸 수 | 14×14 칸, 한 줄 146자, 2048×2048 한 장 |
| TV-163 | F-16 아틀라스 | 칸 수 | 18×18, 한 줄 113자 |
| TV-164 | F-N1 | 글자 순서 | `0123456789,.:%+−×/` 18자, 그림 180×16 |
| TV-165 | F-N3 | 크기 | 80×128 × 11자 = 880×128 |

### 7.8 매니페스트·그림 규격 검산

| TV | given | when | then |
|---|---|---|---|
| TV-180 | MAN-ui | 고유 ID 수 | 197 = UI 7의 204줄 − 코드 7줄(K-10, K-37, K-47, K-72, I-B4, I-C4, I-M8) |
| TV-181 | MAN-ui | 파일 수 / 아틀라스 페이지 | 627 / 23 |
| TV-182 | MAN-ui 아틀라스 없는 항목 | 목록 | 글꼴 6, S-0203 런처 4, 1920×1080 배경 6 = 16 |
| TV-183 | 1920×1080 배경 | 개수 | 7 (S-0501, S-0901, S-1602, S-2307, S-2506, S-2601 + 외부 S-0201) |
| TV-184 | MAN-ui K-20 | 9조각 | [12,12,12,12], 상태 4(normal, hover, pressed, disabled) |
| TV-185 | MAN-ui K-01 | 9조각 | [16,16,16,16] (UI 2.1 "모서리 16") |
| TV-186 | MAN-ui K-04 | 9조각 | [24,12,24,12] (좌우 모서리 24) |
| TV-187 | MAN-ui K-44 | 프레임 | 6프레임 × 100ms = 0.6초 반복(36틱) |
| TV-188 | `k-20_button_large_48x64_hover.png` | `SplitUiName` | (Variant `button_large`, State `hover`) |
| TV-189 | `i-b1_block_fireball_48x48.png` | `SplitUiName` | (`block_fireball`, `normal`) |
| TV-190 | `k-40_selection_corner_24x24_f2.png` | `SplitUiName` | (`selection_corner`, `normal`), frames 2 |
| TV-191 | MAN-pl 각 캐릭터 | 프레임 수 | 147 = 클립 프레임 합 |
| TV-192 | MAN-pl | 모든 프레임 feet | (30, 60) |
| TV-193 | MAN-pl `body.png` | 크기 | 768×832 (12열 × 13행 = 156칸 ≥ 147) |
| TV-194 | MAN-pl 초상 | 크기 | 256×64 = 64×64 × 4 |
| TV-195 | WC 10.1 표 | 장수 합 | 12+18+12+9+12+18+6+6+2+32+12+8 = 147, × 5 = 735 |
| TV-196 | 반응 부품 표 | 합 | 12+4+9+12+4+15+3+10+2 = 71 |
| TV-197 | 초상과 큰 그림 | 합 | 5 × (4 + 1 + 1) = 30 |
| TV-198 | MAN-wd 각 지팡이 | `hand.png` 크기 | 448×384 = 32×14 열, 64×6 행 |
| TV-199 | MAN-wd | `inspection.png` | 1344×384 = 192×7, 192×2 (14단계) |
| TV-200 | MAN-wd | `icon.png` | 896×64 = 64×14 |
| TV-201 | stage 13, up, aim | `WandHandRect` | (416, 320, 32, 64) |
| TV-202 | stage 0, down, hold | `WandHandRect` | (0, 0, 32, 64) |
| TV-203 | stage 5, right, aim | `WandHandRect` | (160, 192, 32, 64) |
| TV-204 | stage 8 | `WandInspectionRect` | (192, 192, 192, 192) |
| TV-205 | stage 6 | `WandInspectionRect` | (1152, 0, 192, 192) |
| TV-206 | stage 13 | `WandIconRect` | (832, 0, 64, 64) |
| TV-207 | MAN-wd chorong | 변이 점 개수 | 3 (나머지 넷은 2) |
| TV-208 | 캐릭터 손 (34, 44), 손잡이 (16, 48) | `AttachWand` | 지팡이 왼쪽 위 = (18, −4) |
| TV-209 | char.1, dir = up | `CompositeOrder` | [Wand, Body, Reactive, WildStain, Cosmetics] |
| TV-210 | char.2(wand.2), dir = up | `CompositeOrder` | [지팡이 아래쪽(y 28~63, x+6), Body, …, 지팡이 위쪽(y 0~27, x+6)] |
| TV-211 | char.3, dir = down | `CompositeOrder` | [Body, Reactive, WildStain, Cosmetics, Wand] |
| TV-212 | MAN-mo | 일반 적 12종 프레임 합 | 100 |
| TV-213 | MAN-mo | 보스 관련 프레임 합(허상 21 포함, 새싹 포함) | 135 |
| TV-214 | MAN-mo | 엘리트 관련 프레임 합 | 17 = 오버레이 12 + 뿔 1 + 급강하 1 + 쌍둥이불 3 |
| TV-215 | MAN-mo `enemy.bug_lantern` | 클립 | move 2, ignite 2, death 없음(폭발 효과로 대신) |
| TV-216 | MAN-mo `boss.mirror_phantom` | 클립 | 첫 만남 전용 `crack` 2, `look` 1 포함 |
| TV-217 | MAN-mo `enemy.bat_dusk` death 프레임 | origin | (32, 54) — 기본값 (32, 58)이 아니라 프레임 값 |
| TV-218 | MAN-mo `enemy.spore_seedling` | size | 32×32 (2배로 키우지 않는다) |
| TV-219 | MAN-fx | 항목 수 | 303 (player 55, special 89, ritual 77, enemy 31, loot 11, wand 15, status 25) |
| TV-220 | MAN-fx `enemy/ice_pillar` | 프레임 | 6 = 솟음 2 + 서 있음 1 + 깨짐 3 |
| TV-221 | MAN-fx `enemy/rock` | 프레임 | 5 = 2 + 1 + 2 |
| TV-222 | MAN-fx `special/ice_screen_horizontal` | 프레임 | 10 = 3 + 1 + 2 + 4 |
| TV-223 | MAN-fx `ritual/mutation_vine` | 프레임 | 24 = 8 + 4 + 6 + 6 |
| TV-224 | MAN-fx `special/cast_seal_*` | 개수 | 15 (공용 10 + 고유 5) |
| TV-225 | 전장 24×14칸 | px | 1536×896 |
| TV-226 | 피격 반지름 0.15칸 | px / Q32.32 | 9.6 / 644,245,094 |
| TV-227 | 1px | Q32.32 | 67,108,864 |
| TV-228 | 판정 중심 0.4칸 위 | px / Q32.32 | 25.6 / 1,717,986,918 |
| TV-229 | 돌마당 시작 (6.0, 7.0), (18.0, 7.0) | px | (384, 448), (1152, 448) |
| TV-230 | 돌마당 세로선 x = 12.0 | px | 768 |
| TV-231 | 모든 반입 그림 | 텍스처 설정 | 점 필터, 밉맵 없음, 압축 없음 |
| TV-232 | UI 아이콘 I-B1 + I-B2 | 개수 | 각 16, 블록 id 16개와 1:1 |
| TV-233 | UI I-T1 + I-T2 | 개수 | 각 15, 비기 id와 1:1 |
| TV-234 | UI I-E2 | 개수 | 5 (무 없음) |
| TV-235 | UI I-C1~I-C3 | 개수 | 각 4(골드, 원정 지갑, 시즌 주화, 별빛) |
| TV-236 | 유료 상품 0개 | 화면 목록 | 별빛 잔액·탭·구매 버튼이 어떤 화면에도 없다 |
| TV-237 | 꾸미기 출처가 PaidShop뿐인 칸 | 옷장 표시 | 그 칸 자체를 숨긴다(빈 칸·자물쇠 없음) |
| TV-238 | 위험색 픽셀이 1개 있는 치장 그림 | 반입 검사 | 실패 |

---

## 8. 충돌

DEC가 최우선이다. DEC가 말하지 않는 것은 "주인 문서" 규칙(UI 0, WC 머리말, MON 머리말)으로 정했다.

| 번호 | 내용 | 택한 쪽 | 근거 |
|---|---|---|---|
| C-01 | 속성의 영어 키: MAN-ui는 `frost`/`shadow`, MAN-fx는 `ice`/`dark` | 정식 `ice`/`dark`. UI 값은 별칭 | DEC는 키를 정하지 않음. `frost`는 블록 `block.frost`(서리)와, `shadow`는 `shadow_orb`·`shadow_hand`와 겹쳐 헷갈림 [조율] |
| C-02 | 블록 영어 이름: UI `magic_bolt`/`ice_spike`/`pierce` vs FX `mana_bolt`/`ice_lance`/`piercing` | 콘텐츠 id는 UI 이름. FX 열쇠는 그림 열쇠로 남기고 대응표(5.6) | 아이콘(UI)이 블록 16종 전부를 1:1로 가진다 [조율] |
| C-03 | 비기 영어 이름: UI `burst`/`wings`/`octastar`/`thunder_spear`/`copy_circle`/`bolt_dash` vs FX `release`/`wing`/`octagram`/`thunderlance`/`arcane_copy`/`lightning_dash` | 콘텐츠 id는 UI 이름, FX는 대응표 | C-02와 같음 [조율] |
| C-04 | 감정표현 이름: UI `greet`/`sit`/`raise_wand`/`despair` vs MAN-pl `wave`/`sit_down`/`lift_wand`/`frustrated` | 콘텐츠 id는 UI 이름, 클립은 대응표(3.15) | [조율] |
| C-05 | WC 0 "그림 파일 이름도 번호나 id로만 가리킨다" vs 현물 폴더 이름이 작업용 이름(`dalgu` 등) | 폴더 이름을 화면에 쓰지 않는 불투명 내부 열쇠로 고정하고 `SlugToChar` 표로 번호에 묶는다 | DEC 1.25의 목적(이름이 바뀌면 이름 표만 바꾼다)은 지켜진다: 이름이 바뀌어도 폴더를 고칠 필요가 없다. 나중에 `char1`~`char5`로 바꾸려면 표 한 줄씩만 바꾼다 [조율] |
| C-06 | RD-mo "비행 적은 캔버스 가운데가 기본 기준점" vs MAN-mo 박쥐·도깨비불 기준점 (32,58)(발밑 쪽)과 MON 5.2 "그림자의 중심이 판정 위치" | MAN-mo의 값(그림자/발밑) | MON 5.2가 판정 기준 [조율] |
| C-07 | RD-mo "ImageGen으로 생성, 검수 상태는 `artwork`와 `missingOrProvisional`" vs MAN-mo `artwork: "hand-authored pixel sprites"`, `missingOrProvisional` 없음 | 로더는 `missingOrProvisional`을 선택 필드로 받고, 없으면 전부 "검수 전"으로 본다 | 그림은 QA 전 시작값(DEC 1.17) [임시] |
| C-08 | 보스·엘리트 id: 기획서에 개별 id 없음(RD-mo), WC 0은 `boss.*`만 적음 | MAN-mo의 `boss.kiln_boar` … `elite.<base>`를 정식 id로 | 현물과 맞추는 쪽이 비용 없음 [조율] |
| C-09 | FX 20.1 "강화 반응의 기다림·충전에는 따로 그림이 없다(서기를 쓴다)" vs WC 10.1 "강화 반응 2 + 2 + 4 + 4 = 12"와 MAN-pl `upgrade_wait_down`, `upgrade_charge_down` | WC 10.1(캐릭터 그림의 주인)과 현물 | FX 20 머리말: 캐릭터 그림의 주인은 WC 10절 [조율] |
| C-10 | UI 6의 폴더 `art/ui/...` vs 현물 `art/production/ui/...` | 현물 경로. 규칙의 하위 구조(kit/icon/font/screen)는 그대로 | [조율] |
| C-11 | UI 6 파일 이름 상태 목록(6개, 크기 뒤) vs 현물(상태가 크기 앞에 오거나 `empty`, `off`, `editing`, `error`, `expanded`, `hero`, `rare` 등 목록 밖 토큰; K-66/K-67/K-68의 `normal`은 "일반 등급"을 뜻함) | 코드는 파일 이름을 해석해 상태를 정하지 않는다. 변형 열쇠는 5.2의 `SplitUiName`으로 한 번 뽑아 표로 들고, 상태 6개는 그림이 그 상태를 가질 때만 쓴다 | [조율] |
| C-12 | UI 3.2 I-E3는 "무" 16×16 1장(다섯 속성 16×16은 FX 20.3 주인) vs MAN-ui I-E3 6장(다섯 속성 포함), MAN-fx에는 속성 문양 16×16 없음 | MAN-ui의 6장을 FX 20.3 "속성 문양 16×16"으로도 쓴다(같은 도안으로 맞추라는 UI 3.2 규칙과 일치) | [조율] |
| C-13 | MAN-ui `external`의 S-2405(느루 대화 초상 128×128 × 5)와 S-2407(행상 대화 초상 128×128 × 3)이 `../world/manifest.json`, `../world/sprites/`를 가리키지만 없음. `art/production/world/`에는 `pixel_art.py`뿐 | 없는 그림으로 등록하고 대체 그림 사용(5.7) | [임시] |
| C-14 | MON 9.5가 UI에 요청한 32×32 재료 아이콘 5종 vs UI는 48×48(I-M1, I-M3, I-C3)과 24×24(I-M2, I-M3, I-C2)만 정함 | UI의 크기 | MON 9.5 스스로 "UI가 다르게 정하면 그쪽이 맞다" [조율] |
| C-15 | FX 20.1이 정한 변이 아이콘 32×32 × 6 등이 MAN-fx에 없음 (5.6 "없는 것") | 없는 그림으로 등록, 대체 사용 | [임시] |
| C-16 | MON 9.4 커맨드 화살표 16×16 × 4종이 MAN-fx에 없음 | UI K-53 16×16(앞, 뒤, 옆, 반대쪽 옆)을 재사용 | MON 9.4가 "UI 입력 표시 그림 재사용 가능"이라 적음 [조율] |
| C-17 | UI 3 "아이콘은 크기마다 따로 그린다(24px 이하에서 뭉개짐)" vs UI 4.26 "S-2603은 16×16 자리에 코드로 줄여 쓴다" | S-2603만 예외로 줄여 쓴다(점 필터 1/2) | 개별 항목의 명시가 일반 규칙보다 앞선다 [조율] |
| C-18 | UI 7 합계 662장 vs MAN-ui 프레임 합 660(외부 3개 제외). ID별: I-E3 문서 1 / 현물 6, S-0201 문서 5 / 현물 띠 4 + 외부 1, S-2405·S-2407 현물 없음 | 문서 합계는 그대로 두고, 반입 보고서는 ID별 차이를 낸다 | [조율] |
| C-19 | WC 6.1 표의 +13 칭호 "엄청엄청 위대한 달구" 등(이름 박음) vs WC 0 틀 규칙 | 틀 `엄청엄청 위대한 {char.N}` | DEC 1.25 [확정] |
| C-20 | WC 0이 장소·인물 id를 `master`, `field`, `plaza`, `arena`로 네임스페이스 없이 적음 vs 이 명세의 문법 | 정식 `npc.master`, `place.*` + 별칭 | [조율] |
| C-21 | WC 3.3 "조각마다 붙는 점 1점"·자료 형식 vs MAN-wd: 단계별 점이 방향·자세 구분 없이 한 벌, 떨어지는 조각의 지팡이 쪽 부착점 없음(조각 자체의 `anchor`만 있음) | [임시] 모든 방향·자세에 같은 점, 조각은 `head` 점에 붙인다(9절 U-04, U-05) | |
| C-22 | WC 10.1 반응 부품 71장, 야생 물 약 105장, 성에의 기둥 짚은 손이 MAN-pl에 없음 | 없는 그림으로 등록 | [임시] |
| C-23 | MAN-fx가 MON 9.4·9.5의 탄·드랍 그림을 `effects/enemy`, `effects/loot`에 둠(주인은 MON) | 그림 위치는 현물, 목록의 주인은 MON | 장수를 두 번 세지 않는다(UI 0 원칙과 같음) [조율] |
| C-24 | MON 9.5 "약점·저항 기호 8×8 × 2종" vs 현물: MAN-fx `damage_glyphs`(16×24, ▲ ▼ 포함, FX 20.3) | 피해 숫자 옆은 FX 글꼴의 ▲ ▼를 쓴다. 8×8 따로 없음 | FX 20.3이 피해 숫자 글꼴에 ▲ ▼를 넣음 [조율] |
| C-25 | DEC 2.1 "32px 기준 크기는 2배" vs MON 9.3 갓버섯 새싹 32×32 실제 픽셀 | 새싹만 32×32 그대로 | MON이 "반 칸짜리 졸개라 키우지 않는다"고 명시, DEC 규칙의 취지(옛 32px 기준을 고치는 것)와 다름 [조율] |
| C-26 | 몬스터 그림의 흰 중심 규칙: DEC 2.2 "위험색은 항상 채도 80% 이상 + 흰 중심" vs 적 탄 외곽선 `#A3004E`(명도 64%, 위험색 범위 밖) | 외곽선은 위험색이 아니라 "진한 자홍 외곽선"으로 허용. 검사기는 위험색 **범위 안의** 색에만 80% 규칙을 건다 | [조율] |

---

## 9. 미정

원문이 정하지 않았거나 "개발자 확인 필요"인 것. 구현을 막지 않도록 임시 기본값을 둔다.

| 번호 | 항목 | 임시 기본값 [임시] |
|---|---|---|
| U-01 | 보스 초상 96×96 × 6, 보스 체력 바 아이콘 32×32 × 6 (MON 9.5 요청, UI 목록에 없음) | 에셋 ID를 `I-BP1`(96×96), `I-BP2`(32×32), Variant = 보스 키로 예약. 그림이 없으면 몬스터 시트의 첫 프레임을 정수 배로 가운데에 놓은 대체 |
| U-02 | 기록 칭호 목록("등 도달 일수 기준") | 지금은 웨이브 10, 30, 100 세 개만. 더하는 것은 데이터로(`title.record.<key>`, 조건 = 넘긴 최고 웨이브 ≥ N) |
| U-03 | 손맛 칭호 목록("등") | 두 개만. 조건(만개 100번, 하룻밤 = 웨이브 10개 무피격)의 세부 판정은 기록 명세로 넘긴다 |
| U-04 | 지팡이 단계별 점의 방향·자세별 값 | 모든 방향·자세에 같은 점, 왼쪽은 x' = 31 − x |
| U-05 | 떨어지는/도는 조각의 지팡이 쪽 부착점 | 조각의 `anchor`를 그 단계의 `head` 점에 맞춘다 |
| U-06 | 한글·숫자 밖 글자로 끝나는 이름의 조사 | 받침 없음으로 본다. 숫자 0, 6~9는 영·육·칠·팔·구로 읽는다 |
| U-07 | 폭 계산에서 공백·기호 폭 | 글자 크기의 절반 |
| U-08 | 선택 괄호 크기의 "한 변" | 긴 변 |
| U-09 | 1920×1080, 2560×1440, 3840×2160 밖의 해상도 | 들어가는 가장 큰 정수 배율, 나머지는 여백, 가운데 맞춤 |
| U-10 | 없는 그림 전부: 전장·광장·돌마당 타일과 시설(WC 10.2~10.4), 반응 부품·야생 물·치장 겹그림, 변덕 8·틀 5·시간대 4 아이콘, "빌림" 꼬리표, 꾸러미 여는 연출, 귀환 가방 칸 3종, 변이 아이콘 6, S-2405, S-2407 | 에셋 열쇠만 정해 두고 5.7의 대체 그림. 열쇠: `world/field/<타일>`, `world/plaza/<시설 키>`, `world/arena/<타일>`, `char.N/reactive/<부품>`, `char.N/wild_stain/<단계>`, `ui/I-W1/<whim 키>`(변덕), `ui/I-W2/<frame 키>`(틀), `ui/I-W3/<tod 키>`(시간대), `ui/I-M10/borrowed_tag`, `ui/I-M11/bundle_open`, `ui/I-M12/bag_slot_{empty,filled,hero}`, `fx/ritual/mutation_icon_<mutation 키>` |
| U-11 | 초상 4프레임의 표정 순서 | 0 기본, 1~3 표정(이름은 미정). 강화 화면의 성공/실패는 초상이 아니라 강화 반응 클립을 쓴다 |
| U-12 | 칭호를 정하는 지팡이(여러 자루를 가질 때) | 그 캐릭터의 "든 지팡이"(`character_state.equipped_wand_id`)의 현재 단계 |
| U-13 | 야생 물을 입는 단계 선택 | 1 ~ 도달 단계 중 하나를 고른다(기본값 = 도달 단계) |
| U-14 | 시즌 칭호·꾸미기의 시즌 id 형식 | `season.<정수>` (예: `season.1`), 이름은 `season.1.name` |
| U-15 | 후계자 전용 꾸미기의 그림 | 그림 없음. 대체는 I-M7 분류 아이콘(UI 3.7 규칙) |
| U-16 | 정수 코드 배정(3절 표) | 표의 값으로 시작. 추가만 한다 |
| U-17 | MAN-fx `player/piercing`(24×12 × 2)의 대응 블록 | `block.pierce`의 탄 덧그림으로 본다. 부메랑 탄 그림은 없음 → 마력탄 그림을 청록 그대로 대체 |
| U-18 | `cast_seal_NN` 번호 순서 | 01~10 = 공용 ①~⑩, 11~15 = char.1~5 고유 |
| U-19 | 이름 칸 폭을 넘는 이름(지팡이 이름 "밑줄 긋는 참나무 지팡이" 13자) | "…" 자르기 + 툴팁(UI 1.4 규칙). 최종 이름은 8자 안으로 정하길 권한다 |
| U-20 | 표시용 애니메이션의 틱 환산(정수가 아닌 것: 7fps = 8.57틱, 125ms = 7.5틱, 110ms = 6.6틱, 90ms = 5.4틱, 220ms = 13.2틱) | 결정론 코어 밖이므로 실제 시간으로 재생. 히트스톱처럼 게임 시간을 건드리지 않는다(FX 0.2) |
| U-21 | `item_code`·`currency` 문자열의 최종 형식 | 1.3절대로 |
| U-22 | 별빛의 이름 | "별빛"(ON 9.1 [제안: 이름]) |
| U-23 | 원정 지갑 아이콘을 재화 enum에 넣을지 | 넣지 않는다(표시 전용) |
| U-24 | (개발자 확인, WC 1) 그믐 원본 스프라이트를 고칠지 | 64×64 원본은 그대로(색 정리와 지팡이 분리만), 큰 그림에서만 목도리 끝과 세로 눈 |
| U-25 | (개발자 확인, WC 4) 이름 표 범위 | 임시 표기 "제자 N"으로 바꿔 내는 것은 캐릭터 다섯뿐, 나머지는 작업용 이름을 그대로 화면에 |
| U-26 | (개발자 확인, UI 1) 선택 괄호 색 | `#FFE94A` 유지. 분필 흰색 괄호로 바꿀 수 있게 색을 토큰 `select_yellow` 하나로 둔다 |
| U-27 | (개발자 확인, UI 2) 메뉴에서 빨강 허용 여부 | 허용하지 않는다(호박색) |
| U-28 | (개발자 확인, UI 3) 글꼴 | RD-ui대로 Galmuri11 / Neo둥근모 채택(현물). 제목 그림(S-0202)과 "대"(S-0902)만 직접 그림 |
| U-29 | (개발자 확인, UI 5) 별빛 아이콘을 그려 둘지 | 이미 그려져 있음(I-C1~C3 `starlight`). 유료 상품 0개 동안 화면에 나오지 않는다 |
| U-30 | (개발자 확인, MON 7) 겉모습을 바꾼 적들의 방향 | 현물 그림 그대로 |
| U-31 | 감정표현 `sit`의 끝 | `sit_down_down` 4프레임 뒤 다른 입력이 있을 때까지 `sit_down` 2프레임 반복 |
| U-32 | 첫 만남의 허상 "사라짐" 연출 | `death` 4장 중 뒤 2장(MON 9.3) — 프레임 인덱스 2, 3 |

---

## 10. 시각/소리 연결점

그림·이펙트·소리가 붙을 **이벤트 이름**만 정한다(구현 안 함). 이름 형식 `<영역>.<사건>` [임시]. 괄호는 이미 정해진 소리 번호(FX 19)나 그림 열쇠다.

**이름·칭호·꾸미기**
- `name_table.reloaded` — 이름 표 버전이 바뀜(화면의 이름 칸 다시 그리기)
- `title.changed` — 머리 위 칭호가 바뀜(단계 오름/내림, 칭호 띠 S-2002)
- `title.ribbon_shown` — 의식용 칭호 띠 펼침(FX `ritual/title_ribbon_*`, 소리 E-28)
- `title.granted` — 새 기록·복수·시즌·손맛 칭호를 얻음
- `wild_stain.advanced` — 야생 물 단계가 오름
- `wild_stain.worn_changed` — 입는 단계를 바꿈
- `cosmetic.equipped`, `cosmetic.unequipped` — 칸별 치장 바뀜(옷장 S-2402 미리보기)
- `cosmetic.preview_rotated` — 옷장 미리보기 방향 바뀜
- `emote.played` — 감정표현(캐릭터 클립 + I-F10, 광장 거리 감쇠: 8칸 −12dB, 16칸 무음, FX 19.5)

**캐릭터·지팡이 그림**
- `character.selected` — 캐릭터 고르기(큰 그림 192×192, 속성 문양 I-E2)
- `character.anim_changed` — 클립 전환(idle/walk/stance_walk/fire/special/signature/hit/death/sit)
- `character.facing_changed` — 방향 바뀜(합성 순서·지팡이 층 다시 계산)
- `wand.visual_stage_changed` — 손 그림 단계 바뀜(강화 결과, 유리 지팡이의 판 안 하락)
- `wand.skin_changed` — 지팡이 겉모습 바뀜
- `wand.detached_part_tick` — 떠 있는/도는 조각 갱신(`chorong_max_lightning`, `seonge_orbit`, `geumeum_orb`)
- `reactive_part.state_changed` — 반응 부품 상태(김 3단계, 등불 밝기, 결정, 빚 고리 등)

**적·보스 그림**
- `enemy.spawned` (덤불 흔들림 N-05), `enemy.anim_changed`, `enemy.died` (사망 연기 `death_smoke`, H-05)
- `elite.spawned` (금색 기둥 빛 `elite_column` 0.5초 + 짧은 효과음), `elite.died` (H-07, 금색 고리)
- `boss.intro` (1.2초 멈춤 + 이름 띠, 보스 초상 U-01), `boss.phase_changed` (N-07), `boss.enrage_stack` (N-06), `boss.final_enrage` (N-08), `boss.died` (H-07)
- `phantom.first_encounter.crack` / `.look` / `.fade` (N-11), `phantom.revenge.shatter` (N-13, FX `enemy/mirror_shards`)
- `drop.spawned.<kind>` — kind ∈ coin, block_common, block_rare, block_heroic, material_pack, crow_pack, season_drop, health_shard (전용 효과음: season_drop)
- `drop.collected.<kind>`

**전장·광장·시간대**
- `tod.transition_started` / `tod.transition_finished` — 야영 중 4초 팔레트 전환(N-09)
- `field.layout_rotated` — 날이 바뀜(배치 자료 6벌 중 다음)
- `weather.started.<event 키>` / `weather.ended.<event 키>` (N-10 비)
- `plaza.facility_focused.<facility 키>` — 시설 상호작용 범위 진입(S-2007 시설 표지판)
- `plaza.server_notice` — +10~+12 성공 / +13 달성 알림 띠(E-34, E-35, 알림 까마귀, 흔들의자 느루 눈 뜨기)
- `plaza.time_of_day_changed` — 광장 24분 하루(낮 12분, 해질녘 4분, 밤 8분)

**UI 공통**
- `ui.cursor_moved` — 키보드 커서 이동(선택 괄호)
- `ui.hovered`, `ui.pressed`, `ui.activated`, `ui.toggled_on`, `ui.toggled_off`
- `ui.disabled_attempt` — 꺼진 것을 누름(이유 한 줄)
- `ui.picked` — 확정(확정 체크 K-41)
- `ui.toast_shown` — 알림 쪽지 K-11(친구 접속, 우편 도착, 거래 신청)
- `ui.value_insufficient` — 값 줄이 모자람(숫자 회청 + 좌우 4px 떨림)
- `ui.new_badge_shown` — 새 소식 점 K-42
- `ui.loading_started` / `ui.loading_finished` — K-44 작은 모닥불
- `ui.screen_opened.<화면 이름>` / `ui.screen_closed.<화면 이름>` (화면 이름은 4.4절 목록)
- `revenge_mark.state_changed` — 거울 표식 S-2603 상태(whole → cracked → shattered)

**에셋 시스템**
- `asset.placeholder_used` — 대체 그림을 썼다(개발 빌드 기록)
- `asset.import_failed` — 반입 검사 실패(위험색, 크기 불일치, 파일 없음)
