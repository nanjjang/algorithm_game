# 개발 파이프라인 — 엄청엄청 위대한 마법사 키우기

이 문서는 "누가, 어떤 모델로, 어떤 순서로, 어떤 검사를 거쳐 만드는가"를 정한다. 무엇을 만드는가는 `docs/DECISIONS.md`와 `docs/spec/`, 어떤 구조로 만드는가는 `ARCHITECTURE.md`, `CONTRACTS.md`, `DECISIONS-TECH.md`가 주인이다.

- 작업 목록의 정본: `docs/dev/tasks.json` (작업 271개. 그중 16개는 그림·소리 에셋을 기다린다). 검사는 `node tools/check-tasks.mjs`
- 모든 에이전트가 읽는 규칙: 저장소 최상위 `CLAUDE.md` (설치 전 원본은 `docs/dev/agent-config/CLAUDE.proposed.md`, 5.5절)
- 에이전트 정의: `.claude/agents/*.md` (설치 전 원본은 `docs/dev/agent-config/agents/`. 기본 14개 + 모델별 변형 3개)
- 검토에서 나온 문제와 판단: `docs/dev/REVIEW-LOG.md`. 사용자에게 물을 것: `docs/dev/OPEN-QUESTIONS.md`

일정, 마감, 범위 삭감은 다루지 않는다(DECISIONS 1.16). 이 문서의 순서는 전부 의존 관계다.

표시: **[총괄 제안]** 은 명세와 dev 문서에 없어서 이 문서가 정한 값이다. 사용자가 바꿀 수 있고, 수치는 측정 뒤에 고친다.

## 0. 문서 우선순위

위가 아래를 이긴다.

1. `docs/DECISIONS.md` — 게임의 확정 사항. 에이전트는 고치지 않는다
2. `docs/spec/*.md` — 규칙, 수치, 테스트 벡터
3. `docs/dev/CONTRACTS.md` — 타입, 시그니처, 형식, 순서. 명세끼리 표현이 다르면 0.3절 통합 표가 정본이다
4. `docs/dev/ARCHITECTURE.md`, `docs/dev/DECISIONS-TECH.md` — 구조와 그 이유
5. `docs/dev/PIPELINE.md`(이 문서), `docs/dev/tasks.json` — 만드는 방법과 순서
6. 그 밖의 기획 문서(`GDD.md`, `online.md`, `monsters.md`, `effects.md`, `ui-assets.md`, `world-characters.md`, `wands/`) — 명세의 출처. 명세와 다르면 명세가 맞다

## 1. 전체 개발 아키텍처

### 1.1 요약

자세한 내용은 `ARCHITECTURE.md`에 있다. 파이프라인이 기대는 사실만 적는다.

| 사실 | 파이프라인에서의 뜻 | 근거 |
|---|---|---|
| 전투 규칙은 한 벌(`Wizard.Sim`, 60틱, Q32.32, PCG32)이고 클라이언트·결투 서버·검증 워커가 같은 소스를 쓴다 | 코어 작업은 dotnet 테스트만으로 검증할 수 있다. Unity를 열 필요가 없다 | ARCHITECTURE P1, 3절 |
| 싱글도 플레이어 배열을 가진 같은 `SimWorld`다 | 멀티플레이 축(넷코드)이 M1 직후부터 병렬로 간다. 결투를 나중에 덧붙이지 않는다 | P2, T-05 |
| 넷코드는 소켓 없는 `Wizard.Netcode`다 | 지연·유실을 메모리 안에서 흉내 내는 테스트가 서버 없이 돈다 | P8, T-19 |
| 의존은 한 방향이다(`Core ← Content ← Sim ← Rules ← Protocol ← Netcode ← Client`) | 에이전트의 소유 경로를 어셈블리와 폴더로 자를 수 있다 | 3.2절 |
| 수치는 `data/`의 JSON, 이름은 `data/names/names.ko.json` 한 곳이다 | 코드 작업과 데이터 작업이 다른 파일을 만진다 | P9, P10 |
| 화면은 `IBackend`만 부른다 | 화면 작업은 오프라인 백엔드만으로 끝낼 수 있고 서버 작업을 기다리지 않는다 | P7, T-26 |
| 에셋은 식별자와 인터페이스로만 가리킨다 | 그림·소리 없이 전 범위를 만들고, 에셋은 나중에 꽂는다 | P12, T-29 |

런타임 구성, 데이터 흐름, 네트워크 권한표는 `ARCHITECTURE.md` 5~7절, 시그니처는 `CONTRACTS.md`를 본다.

### 1.2 보충 결정

병렬 개발과 자동 검증을 위해 기술 구조에 다섯 가지를 더했다. 작업 `M0-TL-01`이 `DECISIONS-TECH.md`(T-31~T-35, 결정/대안/이유), `ARCHITECTURE.md`, `CONTRACTS.md`에 옮겨 적었다. **정본은 `DECISIONS-TECH.md`다.** 아래 표는 파이프라인이 기대는 요약이고, 다르면 `DECISIONS-TECH.md`가 맞다.

| # | 결정 | 이유 |
|---|---|---|
| T-31 | 공유 패키지 `src/Wizard.ViewLogic`을 더한다(아홉 번째. `netstandard2.1`, C# 9, `noEngineReferences`). 화면 상태 기계와 뷰 모델, HUD 모델, 알림 라우터, 표시 이벤트 바인딩 표, 연출 한도(번쩍·흔들림·입자 예산) 계산, 에셋 매니페스트 로더와 레지스트리가 여기에 있다. 의존: Core, Content, Text, Sim, Rules, Protocol, Netcode, Client. 코어 버전 해시에는 들어가지 않고 실수형을 쓸 수 있다. `Wizard.Unity.UI`와 `Wizard.Unity.Presentation`은 이것을 그리기만 하는 얇은 층이 된다 | spec 13의 테스트 벡터 243개와 spec 14의 화면·에셋 쪽 벡터를 `dotnet test`로 돌리려는 것이다. Unity 어셈블리 안에 두면 검증마다 편집기를 배치 모드로 띄워야 하고, 작업 트리마다 `Library`가 따로 생겨 병렬 작업이 무거워진다. 화면 규칙이 엔진 밖에 있으면 "뷰에 규칙이 없다"를 컴파일러가 지켜 준다 |
| T-32 | 화면 뷰는 uGUI로, **코드로만** 만든다. 프리팹과 장면 편집을 쓰지 않는다(에셋 반입 전까지). 장면 네 개에는 시작 개체 하나만 둔다 | 장면과 프리팹 파일은 병합 충돌이 나면 풀기 어렵다. 에셋이 없는 지금은 코드로 만든 자리 표시 위젯이 가장 단순하다. 그림은 위젯의 겉(스킨)만 바꿔 꽂는다 |
| T-33 | 뼈대를 먼저 만든다. 프로젝트 전부, id 열거형 전부, `SimEventType` 전 구간, `ErrorCode`, 계약 인터페이스를 M0~M1에서 한 번에 선언한다. `SimWorld`와 `ContentDb`는 부분 클래스이고, 틱 단계·상태 절·팩 절마다 부분 메서드 훅을 미리 선언해 둔다. 모듈은 자기 파일에서 훅을 구현하기만 한다. 서버의 엔드포인트·서비스·웹소켓 처리기, DataBuild의 변환기는 모듈 인터페이스를 어셈블리에서 찾아 등록한다 | 여러 에이전트가 같은 파일(틱 파이프라인, 열거형, `Program.cs`, 솔루션 파일)을 동시에 고치는 일을 없앤다. 뒤 작업은 새 파일만 더한다 |
| T-34 | Unity `.meta`의 GUID는 저장소 기준 상대 경로의 MD5로 만든다(`tools/Wizard.MetaGen`). Unity가 이미 만든 `.meta`는 그대로 둔다 | 에이전트는 Unity를 열지 않고 `src/`에 파일을 더한다. 작업 트리마다 Unity가 제각각 GUID를 만들면 병합할 때 같은 파일의 `.meta`가 충돌한다 |
| T-35 | 이름 표는 열쇠 오름차순, 한 줄에 열쇠 하나, 마지막 줄은 표지 열쇠 `"~end": ""`로 둔다. git 병합은 union, 병합 뒤 `databuild names --fix`가 정렬과 중복을 본다 | 이름 표는 유일한 파일이라(DECISIONS 1.25) 여러 작업이 줄을 더한다. 형식을 고정하면 덧붙이기끼리는 충돌하지 않는다 |

T-31이 닿는 곳: `ARCHITECTURE.md` 2절 레이아웃과 3.1절 표(공유 어셈블리 9개), 3.2절 의존 그림(`Client ← ViewLogic ← Wizard.Unity.{Presentation, UI}`), 10.5절(모듈), `CONTRACTS.md` 13절(`IAssetCatalog`의 열쇠와 기록 타입은 ViewLogic, 텍스처를 쥔 구현은 Unity). T-32는 `ARCHITECTURE.md` 4.3절, T-33은 `CONTRACTS.md` 3.2절, T-34는 `ARCHITECTURE.md` 4.2절과 `CONTRACTS.md` 11.5절, T-35는 `ARCHITECTURE.md` 8.3절과 `CONTRACTS.md` 5절에 있다.

T-36 이후의 결정(메일 발송, 외부 라이브러리, 테스트용 DB, Windows 대상, CI 도구와 작업 상태 파일, 다시 만드는 파일, 금지 목록 확장, 솔루션과 빌드 설정, IL2CPP 코드 제거, 결투의 서버 주도 측정)은 `DECISIONS-TECH.md`에 있다. 검토에서 나온 문제와 그 판단은 `REVIEW-LOG.md`에 있다.

T-33의 훅은 **(단계, 모듈)마다 따로** 선언한다(C#의 부분 메서드는 구현이 하나뿐이어야 한다). 목록은 `CONTRACTS.md` 3.2절의 표다. `SimWorld.<모듈>.cs`와 `ContentDb.<절>.cs`는 모듈 폴더에 두되 네임스페이스는 `Wizard.Sim`(`Wizard.Content`)이다. T-31에 따라 `FixConvert`(고정소수점 → 실수)는 `Wizard.ViewLogic`에 있다.

프로젝트 목록(M0-BT-01이 빈 껍데기로 전부 만든다. 실행 파일 프로젝트의 빈 `Program.cs`, 계획된 외부 패키지 참조, 분석기 연결 줄, Unity 쪽 DLL과 asmdef 참조까지 같이 만든다):

| 묶음 | 프로젝트 |
|---|---|
| `src/` 9 | `Wizard.Core`, `Content`, `Text`, `Sim`, `Rules`, `Protocol`, `Netcode`, `Client`, `ViewLogic` |
| `server/` 8 | `Wizard.Server.Data`, `App`, `Api`, `Duel`, `Verify`, `Admin`, `Payments.Fakes`, `Api.DevHost`(개발용 호스트. 가짜 결제 제공자는 여기와 테스트에서만 참조한다) |
| `tests/` 16 | `Wizard.Core.Tests`, `Content.Tests`, `Text.Tests`, `Sim.Tests`, `Rules.Tests`, `Protocol.Tests`, `Netcode.Tests`, `Client.Tests`, `ViewLogic.Tests`, `Server.Tests`, `Analyzers.Tests`, `DataBuild.Tests`, `AssetCheck.Tests`, `Wizard.Perf`, `Wizard.TestKit`(net10.0, xUnit 의존 부분), `Wizard.TestKit/Shared`(`netstandard2.1` + C# 9의 UPM 패키지. 골든 읽기를 dotnet과 Unity EditMode가 같이 쓴다) |
| `tools/` 13 | `Wizard.DataBuild`, `VersionStamp`, `TableGen`, `ReplayCli`, `StatusSign`, `LoadBot`, `Analyzers`(`netstandard2.0`), `MetaGen`, `VectorCoverage`, `TaskGraph`, `AssetCheck`, `BalanceLab`, `Ci`(CI의 로직. T-40). 그 밖에 `tools/check-tasks.mjs`(node로 도는 `tasks.json` 검사) |

## 2. AI 모델 분배

### 2.1 원칙

품질 최우선 변형을 쓴다. 예산은 제약이 아니다.

- **Fable 5.2** — 설계, 검토, 통합 승인. 직접 구현하지 않는다.
- **Opus 5.5** — 구현의 주력.
- **Sonnet 5.5** — 틀이 정해진 일만. 반드시 Opus가 검토한다.
- **Haiku 5.5** — 읽기 전용 조사와 정리. 코드, 데이터, 문서를 고치지 않는다.

### 2.2 선택 기준

| 모델 | 맡는 일의 성격 | 예 |
|---|---|---|
| Fable | 틀리면 되돌리기 어려운 것. 여러 모듈의 경계. 네트워크 권한 | 계약과 결정 기록, 결정론 코어 뼈대의 검토, 넷코드의 검토, 재화가 움직이는 서버 트랜잭션의 검토, 마일스톤 게이트 승인 |
| Opus | 명세를 처음 코드로 옮기는 것. 상태 기계, 알고리즘, 동시성, 판단이 필요한 것 | 전투 코어, 넷코드, 규칙 라이브러리, 서버 유스케이스, 화면 상태 기계, 분석기, 구조가 겹친 데이터(적 공격 목록, 보스 패턴) |
| Sonnet | 본보기가 이미 있고 그 틀을 따라 옮기는 것. 결과를 기계가 대조할 수 있는 것 | 명세 표를 데이터 파일로 옮기기, 화면 모델을 자리 표시 위젯에 잇기, 문서 정리 |
| Haiku | 읽고 찾고 세는 것 | "이 벡터 id가 어느 절에 있는가", "이 이름을 쓰는 곳 목록", 두 문서의 어긋남 목록 |

올려 보내는 규칙:

- Sonnet 작업이 검토에서 두 번 반려되면 Opus로 다시 맡긴다.
- 작업 도중 명세에 없는 판단이 필요해지면 작업자는 멈추고 결과 보고에 올린다. **에이전트끼리 직접 부르지 않는다**(서브에이전트는 다른 서브에이전트를 띄울 수 없다). 지휘하는 세션이 결과 보고를 보고 다음을 띄운다: Sonnet 작업의 물음은 Opus(해당 축 엔지니어)에게, Opus 작업의 물음은 Fable(tech-lead)에게, 근거 찾기는 doc-scout에게.
- 계약(`CONTRACTS.md`, 계약 코드)을 바꿔야 하면 언제나 Fable이 정한다(3.7절).
- 작성자와 검토자는 항상 다른 인스턴스다. 같은 모델이어도 새 컨텍스트에서 본다.

### 2.3 작업별 배정

사용자가 준 표를 품질 최우선 변형으로 옮긴 것이다. 작업 하나하나의 배정은 `tasks.json`의 `owner_agent`, `owner_model`, `reviewer_model`이 정본이다. 모델이 기본과 다른 작업은 **모델별 변형 에이전트**(`content-data-engineer-opus`, `test-engineer-opus`, `ui-view-engineer-opus`)를 `owner_agent`로 가진다. 에이전트 파일의 `model`이 곧 그 작업의 모델이라서 지휘 세션의 호출 인자에 기대지 않는다. `check-tasks.mjs`가 `owner_model` = 그 에이전트의 `default_model`인지 검사한다.

| 작업 | 원래 표 (담당 / 검토) | 채택 (담당 / 검토) | 에이전트 |
|---|---|---|---|
| 게임 전체 아키텍처 | Fable / Fable | Fable / Fable(다른 인스턴스) + 사용자 | tech-lead |
| 멀티플레이 네트워크 설계 | Fable / Fable | Fable / Fable | tech-lead |
| 네트워크 동기화 구현 | Opus / Fable | Opus / Fable | netcode-engineer |
| 적 AI 및 행동 패턴 | Opus / Opus | Opus / Opus. 안전 경로 검사(R5)와 허상은 Fable 검토 | combat-engineer |
| 무기·스킬 시스템 | Opus / Opus | Opus / Opus. 코어 뼈대, 판정기, 결투 규칙은 Fable 검토 | combat-engineer |
| 절차적 맵 생성 | Opus / Opus | Opus / Opus. 이 게임은 전장이 하나로 고정이라, 여기에 해당하는 것은 웨이브 구성·변덕·전리품·드랍이다 | roguelike-engineer |
| 일반 몬스터 구현 | Sonnet / Opus | **Opus / Opus** (AI 상태 기계와 회피 검산이 걸린다) | combat-engineer |
| 아이템·버프·디버프 | Sonnet / Opus | **Opus / Opus**(상태 효과, 변이, 규칙 라이브러리). 수치 표만 Sonnet / Opus | combat-engineer, roguelike-engineer, content-data-engineer |
| Unity UI 연결 | Sonnet / Opus | 화면 로직 **Opus / Opus**, 뷰 연결 Sonnet / Opus. 조립·강화 화면의 뷰는 Opus | ui-logic-engineer, ui-view-engineer |
| 설정·메뉴·로비 | Sonnet / Opus | 로직 **Opus / Opus**, 뷰 Sonnet / Opus | ui-logic-engineer, ui-view-engineer |
| 데이터 테이블 작성 | Haiku / Sonnet | **Sonnet / Opus**. 구조가 겹친 표(적, 보스, 웨이브, 결투 규칙)는 Opus / Opus | content-data-engineer |
| 코드 문서화·정리 | Haiku / Sonnet | **Sonnet / Opus**. Haiku는 조사만 | test-engineer(문서 작업), doc-scout |
| 테스트 코드 생성 | Sonnet / Opus | 명세 벡터는 구현 작업이 같이 만든다. 독립 테스트 묶음은 Sonnet / Opus, 결정론·넷코드·서버 종단은 **Opus / Fable** | test-engineer |
| 최종 시스템 통합 | Opus / Fable | Opus / Fable | integration-engineer |

지금 `tasks.json`의 분포: 담당 Opus 242개, Sonnet 27개, Fable 2개. 검토 Fable 105개, Opus 166개.

## 3. 멀티 에이전트 파이프라인

### 3.1 구조

```
                     tech-lead (Fable)
        계약·결정 / 작업 카드 승인 / 검토 / 게이트 승인
                            │
   ┌────────────┬───────────┼────────────┬─────────────┐
 멀티플레이      전투         로그라이크      UI           기반
 netcode-eng   combat-eng   roguelike-eng  ui-logic-eng  build-tools-eng
 server-eng                                ui-view-eng   content-data-eng
                                           unity-eng     test-engineer
   └────────────┴───────────┼────────────┴─────────────┘
              code-reviewer (Opus) / tech-lead (Fable) 검토
                            │
              integration-engineer (Opus) → main
        통합 빌드마다: 전체 검사, Unity 빌드, 2인 헤드리스 결투, 성능
                            │
                 마일스톤 게이트 (tech-lead 승인)

 doc-scout (Haiku): 읽기 전용 조사. 지휘하는 세션만 부른다
```

모든 호출과 올림은 지휘하는 세션이 한다. 구현 에이전트가 검토자나 doc-scout를 직접 부르는 길은 없다. 작업자는 결과 보고에 "확인이 필요한 것"을 적고, 지휘 세션이 그것을 보고 다음 에이전트를 띄운다.

네 축은 M1이 끝나면 같이 간다. 전투 축이 주문과 적을 만드는 동안 멀티플레이 축은 2인 세계 위에서 봉인·예측·되감기를 만들고, UI 축은 `ViewLogic` 기반을, 로그라이크 축은 웨이브 디렉터를 만든다. 서버 기반(`Server.Data`)은 전투 코드에 기대지 않는다. 다만 원장이 `CurrencyCode`(M1-CONT-01)를, 오류가 `ErrorCode`(M1-CORE-05)를 쓰므로 그 두 작업 뒤부터 시작할 수 있다.

### 3.2 에이전트 역할표

아래의 경로는 그 에이전트가 **주로 맡는 곳**이다(참고). 실제 권한은 작업 카드의 `owned_paths`뿐이다. 한 작업이 다른 역할의 경로를 `owned_paths`로 받았으면(예: 규칙 작업이 자기 데이터 절의 `src/Wizard.Content/Rules/`와 `tools/Wizard.DataBuild/Convert/`를 같이 받는다) 그 작업 안에서는 고칠 수 있다. 에이전트 정의의 경로 목록을 이유로 작업을 거절하지 않는다.

| 이름 | 모델 | 책임 범위 (소유 경로) | 입력 | 산출물 | 검토자 |
|---|---|---|---|---|---|
| tech-lead | fable | `docs/dev/*.md`(계약, 결정, 이 문서), 작업 그래프의 구조 변경, `CLAUDE.md`와 `.claude/agents/`(사용자 승인이 있을 때만) | 명세, 계약 변경 요청, 검토 요청, 게이트 증거 | 결정 기록, 계약 개정, 검토 판정, 게이트 승인 | 다른 Fable 인스턴스, 사용자 |
| netcode-engineer | opus | `src/Wizard.Netcode/`, `src/Wizard.Protocol/`, `src/Wizard.Client/{Input, Net, Duel, Boot, Content, Save/Core, Backend/Contracts, Backend/Server}/`, `server/Wizard.Server.Duel/` | 작업 카드, spec 10·11·12, CONTRACTS 6·8·9·10절 | 넷코드 코어, 프로토콜 코덱과 픽스처, 전송, 서버 백엔드, 결투 프로세스 | tech-lead |
| combat-engineer | opus | `src/Wizard.Core/`, `src/Wizard.Sim/{Input, Events, Runtime, World, Player, Heat, Graze, Combo, Spell, Arts, Characters, Enemies, Bosses, Duel, Replay}/` | spec 01~06, 10(2~8절), CONTRACTS 1·3절 | 결정론 코어, 전투 모듈, 그 모듈의 벡터 테스트 | code-reviewer. 코어 뼈대·판정기·결투 규칙은 tech-lead |
| roguelike-engineer | opus | `src/Wizard.Sim/Waves/`, `src/Wizard.Sim/Arts/Unlock/`, `src/Wizard.Rules/`, `src/Wizard.Client/Backend/Offline/`, `src/Wizard.Client/Run/Flow/` | spec 07·08·09, CONTRACTS 3.7·4절 | 웨이브·야영·전리품·점수, 규칙 라이브러리(강화, 경제, 정산, 리그, 시즌), 오프라인 백엔드 | code-reviewer. 강화·정산·거래 규칙은 tech-lead |
| ui-logic-engineer | opus | `src/Wizard.ViewLogic/`, `src/Wizard.Text/`, `src/Wizard.Client/Settings/` | spec 13·14, `IBackend` | 화면 상태 기계와 뷰 모델, HUD 모델, 이름 표 처리, 에셋 레지스트리, 연출 한도 | code-reviewer |
| ui-view-engineer / ui-view-engineer-opus | sonnet / opus(조립·강화 화면의 뷰) | `unity/Assets/Game/UI/{Screens, Hud}/` | 화면 모델, 뷰 기반 위젯 | 자리 표시 화면 뷰(모델을 그리고 조작을 넘긴다) | code-reviewer |
| unity-engineer | opus | `unity/ProjectSettings/`, `unity/Packages/`, `unity/Assets/Game/{App, Input, Presentation, Debug}/`, `unity/Assets/Game/UI/Common/`, `unity/Assets/Editor/`, `unity/Assets/Tests/` | ARCHITECTURE 4절, CONTRACTS 13절 | Unity 프로젝트, 입력원, 도형 표시, 표시 버스, 반입 편집기 도구, IL2CPP 골든 실행기 | code-reviewer |
| content-data-engineer / content-data-engineer-opus | sonnet / opus(구조가 겹친 표) | `data/`, `src/Wizard.Content/{Ids, Defs, Pack/Sections}/`, `tools/Wizard.DataBuild/Convert/` | 명세의 수치 표, `docs/dev/howto-data-slice.md` | 데이터 JSON, 스키마, 변환기, 팩 절 읽기, 명세와의 대조 테스트 | code-reviewer |
| test-engineer / test-engineer-opus | sonnet / opus(결정론·넷코드·종단) | `tests/`에서 구현 작업이 갖지 않은 폴더, `tests/golden/`, `tools/Wizard.{ReplayCli, LoadBot, BalanceLab}/` | 완성된 모듈, 명세의 통과 기준 | 독립 테스트 묶음, 퍼저, 골든, 종단 테스트, 계측 도구 | code-reviewer. 결정론·넷코드·종단은 tech-lead |
| server-engineer | opus | `server/Wizard.Server.{Data, App, Api, Verify, Admin}/`, `server/migrations/`, `server/deploy/` | spec 08·09·11·12, CONTRACTS 7·8·9·12절 | 스키마, 유스케이스, 엔드포인트, 검증 워커, 배치 | code-reviewer. 원장·정산·검증·거래·결제는 tech-lead |
| build-tools-engineer | opus | 최상위 빌드 파일, `tools/Wizard.{Analyzers, VersionStamp, TableGen, MetaGen, VectorCoverage, TaskGraph, AssetCheck}/`, `tools/Wizard.DataBuild/{Core, Validate}/`, `tools/ci/` | ARCHITECTURE 3·8·11절 | 빌드 설정, 분석기, 생성 도구, CI 스크립트, 검산기 | code-reviewer. 분석기와 팩 형식은 tech-lead |
| integration-engineer | opus | main으로의 병합, `docs/dev/gates/`, `tests/golden/m<N>/`(게이트 골든) | 승인된 작업 브랜치 | 병합, 통합 빌드 결과, 게이트 증거 | tech-lead |
| code-reviewer | opus | 없음(읽기만) | 작업 카드, 브랜치의 변경, 명세 | 검토 판정 | — |
| doc-scout | haiku | 없음(읽기만) | 질문 하나 | 근거 위치가 붙은 답 | 지휘 세션이 확인 |

### 3.3 작업의 수명

```
todo ─▶ in_progress ─▶ review ─▶ done
          │   ▲          │
          └───┘ 반려      └─ (병합 게이트 실패) ─▶ in_progress
blocked-on-assets ─(에셋 반입)─▶ todo
```

1. **고르기.** `taskgraph ready`가 `deps`가 전부 done인 todo 작업을 낸다. 마일스톤이 뒤여도 `deps`가 끝났으면 시작할 수 있다.
2. **시작.** `taskgraph start <id>`가 `.claude/worktrees/<id>`에 `task/<id>` 브랜치의 작업 트리를 만들고 상태 파일(`.claude/taskstate.json`)의 상태를 in_progress로 바꾼다.
3. **작업 카드.** 지휘하는 세션이 `tasks.json`의 항목으로 카드를 채워(3.4절) 담당 에이전트를 `owner_model`로 띄운다.
4. **구현.** 담당자는 카드의 명세와 계약을 읽고, 벡터를 테스트로 옮기고, 구현하고, `tools/ci/ci.sh fast`와 자기 테스트를 통과시키고, 커밋한 뒤 결과 보고(3.5절)를 낸다.
5. **검토.** `reviewer_model`의 검토자가 새 컨텍스트에서 변경과 카드와 명세를 본다. 판정은 승인 또는 반려다(3.6절).
6. **병합.** integration-engineer가 승인된 브랜치를 main 위로 옮겨 병합 게이트를 돌리고 병합한 뒤 done으로 바꾼다(3.8절).
7. **마일스톤 게이트.** 그 마일스톤의 작업이 전부 done이면 `M<N>-INT` 작업이 게이트를 돌리고 tech-lead가 승인한다.

**상태는 `tasks.json`에 두지 않는다**(T-40). `tasks.json`은 구조(정의)만 담고 tech-lead의 작업만 커밋한다. 그 안의 `status`는 처음 분류(`todo`, `blocked-on-assets`)일 뿐이다. 진행 상태(in_progress, review, done)는 git이 추적하지 않는 `.claude/taskstate.json`에 있고 `tools/Wizard.TaskGraph`만 파일 잠금을 잡고 바꾼다. 상태 파일을 잃으면 `taskgraph reconcile`이 main의 커밋 기록에서 done을 다시 만든다. 작업 브랜치는 `tasks.json`을 건드리지 않는다.

**M0 부트스트랩 절차.** `taskgraph`, `ci.sh`, `vector-coverage`, 훅은 M0-BT-05와 M0-BT-06이 만든다. 그보다 앞서는 작업(M0-TL-02, M0-TL-01, M0-BT-01~04, M0-TE-01)은 도구 없이 아래처럼 한다.

1. 작업 트리는 손으로 만든다: `command git -C <저장소 절대 경로> worktree add <저장소>/.claude/worktrees/<id> -b task/<id>`.
2. 검사는 `dotnet build`와 `dotnet test`, 그리고 `node tools/check-tasks.mjs`(이미 있다)로 한다. 결과 보고에 그렇게 적는다.
3. 소유 경로는 integration-engineer가 `command git -C <작업 트리> diff --name-only main`을 카드의 `owned_paths`와 직접 대조한다.
4. M0-BT-06이 끝나면 앞선 M0 작업들의 병합 커밋에 `taskgraph check-diff`를 소급해 돌리고 결과를 `docs/dev/gates/M0.json`에 적는다. 어긋남이 나오면 결함 작업을 만든다.
5. M0-TL-02(에이전트 설정 설치)는 M0의 첫 작업이고 사용자 승인이 있어야 한다. 그 전에 띄우는 작업 카드에는 `CLAUDE.proposed.md`의 2절·2.5절·3절을 그대로 붙인다.

### 3.4 작업 전달: 작업 카드

```markdown
# 작업 카드 <id> — <title>

- 마일스톤 / 묶음: <milestone> / <parallel_group>
- 담당: <owner_agent> (<owner_model>)   검토: <reviewer_model>
- 작업 트리(절대 경로): WT=<저장소 절대 경로>/.claude/worktrees/<id>   브랜치: task/<id>
- **모든 명령과 파일 경로는 $WT 아래로 준다.** 에이전트의 셸은 호출마다 메인 저장소로 돌아간다. 상대 경로로 치면 main 체크아웃이 빌드·검사되고 "통과"가 나온다

## 목표
<한두 문장. 끝났을 때 무엇이 돌아가는가>

## 명세 (먼저 읽는다)
<spec_refs. 절 번호까지>

## 계약 (시그니처는 여기 그대로)
<contracts. CONTRACTS.md의 절>
명세의 표현이 계약과 다르면 CONTRACTS 0.3절 통합 표와 2.1절 대응표로 바꿔 읽는다.

## 소유 파일 (이 밖은 고치지 않는다)
<owned_paths>
공유 추가 전용 파일(PIPELINE 4.4절)은 줄을 더할 수만 있다.

## 앞선 작업
<deps와 각 작업이 남긴 것: 타입 이름, 폴더, 본보기 파일>

## 수락 테스트
<acceptance. 전부 자동 검사로 확인할 수 있어야 한다>
벡터: <vectors. 이 작업이 테스트로 옮길 명세 벡터의 선택자 목록. 전부 통과해야 한다>
공통: $WT/tools/ci/ci.sh fast 통과, vectors의 벡터가 VectorCoverage에서 빠짐 0.

## 금지 사항
- 소유 파일 밖 수정. 계약(시그니처, 열거형 번호, 파일 형식) 변경
- 명세의 테스트 벡터를 구현에 맞춰 고치기
- 코드에 수치 상수나 화면 문자열 넣기
- 코어(Core, Content, Sim)에서 CLAUDE.md의 결정론 금지 목록 쓰기
- 분석기 진단 억제, 테스트 건너뛰기 표시
- 일정·삭감 제안

## 사람 확인 항목
<human_check가 있으면>
```

### 3.5 결과 전달: 결과 보고

담당자의 마지막 메시지이고, 검토자와 통합 담당이 이것부터 읽는다. 병합 커밋의 본문에도 요약이 들어간다.

```markdown
# 결과 보고 <id>

## 한 일
<바뀐 것 요약. 파일 목록은 git이 알려 주므로 구조만>

## 수락 테스트 결과
| 조건 | 결과 | 증거(명령과 출력 요약) |

## 벡터 대응
vector-coverage --task <id> 의 출력: 대상 N개, 통과 N개, 빠짐 0개

## 명세와 다르게 한 곳
<없으면 "없음". 있으면 명세 절, 이유, 누가 정했는지>

## 올리는 것
- 계약 변경 요청: <없음 | 내용>
- 명세 충돌·모호함: <없음 | 절과 내용. 임시로 택한 값>
- 이름 표에 더한 열쇠: <목록>
- 뒤 작업이 알아야 할 것: <새 타입, 훅, 본보기 파일>

## 확인하지 못한 것
<돌려 보지 못한 검사, 사람이 봐야 하는 것>
```

"확인하지 못한 것"을 비우려고 추측을 사실처럼 적지 않는다. 돌려 보지 않은 것은 돌려 보지 않았다고 적는다.

### 3.6 검토

검토자는 구현자의 설명이 아니라 **변경과 명세**를 본다. 다음을 차례로 확인한다.

1. 변경이 `owned_paths` 안인가(`taskgraph check-diff <id>`).
2. 수락 테스트가 실제로 그 조건을 재는가. 테스트가 구현을 따라 쓰이지 않았는가(기대값이 명세에서 왔는가).
3. 명세의 규칙과 수치가 맞는가. 벡터가 다 있는가.
4. 계약의 시그니처·순서·형식과 같은가.
5. 결정론(코어): 처리 순서, 난수 흐름, 상태가 스냅숏과 해시에 다 들어가는가, 할당이 없는가.
6. 권한(서버·넷코드): 클라이언트 값을 믿는 곳이 없는가, 멱등과 잠금 순서, 원장을 거치지 않는 변경이 없는가.
7. 수치 상수와 화면 문자열이 코드에 없는가.
8. 게임 규칙 불변식(CLAUDE.md 2.5절)을 어기지 않는가.
9. 골든의 기대 해시나 생성 파일이 바뀌었으면: 도구의 출력과 같은가, `tests/golden/CHANGELOG.md`에 이유가 있는가, **해시가 바뀔 이유가 있는 변경인가**(상태 절을 더했다, 규칙을 고쳤다). 이유 없이 바뀐 해시는 막는 문제다.

판정 형식:

```markdown
# 검토 <id> — 승인 | 반려

## 막는 문제 (반려 사유)
- [파일:줄] 무엇이 틀렸나 — 근거(명세 절 또는 계약 절) — 실패하는 입력의 예

## 막지 않는 의견
- …

## 확인한 것
<직접 돌린 명령과 결과>
```

- 반려는 "막는 문제"가 하나라도 있을 때만 한다. 취향은 막지 않는다.
- 세 번 반려되면 tech-lead가 본다: 작업을 나누거나, 계약을 고치거나, 담당 모델을 올린다.
- 검토자는 고치지 않는다. 고치는 것은 담당자다.

### 3.7 계약 변경과 막힘

| 상황 | 하는 일 |
|---|---|
| 계약을 바꿔야 구현할 수 있다 | 담당자는 그 부분에서 멈추고 결과 보고에 "계약 변경 요청"을 적는다. tech-lead가 정하면 `CONTRACTS.md`와 계약 코드를 고치는 작은 작업(`CCR-<번호>`)을 `tasks.json`에 더하고, 그 절을 `contracts`로 가진 진행 중 작업에 알린다 |
| 명세끼리 다르다 | CONTRACTS 0.3절 통합 표에 있으면 그대로. 없으면 tech-lead가 정해 통합 표에 더한다 |
| 명세가 정하지 않았다(기술) | tech-lead가 `DECISIONS-TECH.md`에 결정을 더한다 |
| 명세가 정하지 않았다(게임 내용) | 명세의 [임시] 값이 있으면 그 값으로 진행한다. 없으면 tech-lead가 임시값을 정해 진행하고 `docs/dev/OPEN-QUESTIONS.md`에 사용자에게 물을 것으로 적는다. `docs/DECISIONS.md`는 사용자만 고친다 |
| 구현과 벡터가 다르다 | 벡터를 고치지 않는다. 명세가 틀렸다고 보이면 근거를 붙여 올린다 |
| 앞선 작업의 결함을 찾았다 | 자기 작업에서 고치지 않는다. 결함 작업(`FIX-<번호>`)을 더해 그 경로의 주인이 고친다 |
| 통과 기준을 못 넘는다(손맛, 균형, 넷코드 폐기율) | 수치·규칙을 고치는 작업을 새로 만든다. 기준을 낮추지 않는다 |
| 벡터를 지금 시점에 시험할 수 없다(뒤 작업의 기능이 필요하다) | 그 벡터를 구현에서 빼지 않는다. tech-lead가 `tasks.json`의 `vectors` 배정을 옮기거나 `deps`를 더한다 |

### 3.8 결과 통합

순서: **작업 트리 브랜치 → 작업 게이트 → 검토 승인 → main 위로 옮기기 → 병합 게이트 → 병합 → done**.

1. integration-engineer만 main에 넣는다. 한 번에 한 브랜치씩 넣는다.
2. `task/<id>`를 main 위로 옮긴다. **작업 트리 안에서 한다**: `command git -C <작업 트리 절대 경로> rebase main`(그 브랜치는 작업 트리에 체크아웃되어 있어 main 체크아웃에서는 git이 거부한다). 옮긴 뒤 "다시 만드는 파일"(4.4절)을 다시 만들고, 그 작업 트리에서 `ci.sh full`을 돌린다.
3. 통과하면 한 커밋으로 합쳐 넣는다(squash). main의 커밋 하나가 작업 하나다. 커밋 메시지는 4.2절 형식이고 본문은 결과 보고의 요약이다.
4. `taskgraph done <id>`로 상태를 바꾸고 작업 트리와 브랜치를 지운다.
5. 승인된 브랜치들을 다 넣고 나면 통합 빌드를 한 번 돌린다(7.2절).

직렬 단계의 비용을 줄이는 두 가지(순서를 바꾸지 않는다):

- **경로 조건.** `ci.sh full`은 바뀐 경로를 보고 Unity 단계(배치 컴파일, EditMode)를 건너뛸 수 있다. 조건은 변경이 `src/`, `unity/`, `data/`, `tests/golden/`, `tests/Wizard.TestKit/Shared/`, `tools/Wizard.MetaGen/` 어디도 건드리지 않았을 때뿐이다(서버, 서버 테스트, 문서, 그 밖의 도구만 바뀐 경우). 건너뛴 것은 게이트 출력에 적힌다.
- **묶음 검사.** 승인된 브랜치 여럿을 임시 통합 브랜치에 차례로 쌓아 `ci.sh full`을 한 번 돌리고, 통과하면 main에 작업마다 한 커밋으로 넣는다. 실패하면 반으로 나눠 원인 브랜치를 찾고 그 브랜치만 돌려보낸다. main의 커밋 하나가 작업 하나라는 규칙은 그대로다.

충돌이 났을 때:

| 충돌한 곳 | 처리 |
|---|---|
| 공유 추가 전용 파일(4.4절) | 양쪽 줄을 다 살린다. 이름 표는 `databuild names --fix`, 패키지 버전 표는 이름순 정렬 |
| 다시 만드는 파일(`*.g.cs`, 골든의 기대 해시) | 충돌을 손으로 풀지 않는다. main 위에서 다시 만든다. 골든은 `tests/golden/CHANGELOG.md`의 양쪽 줄을 다 살린다 |
| 그 밖의 파일 | 소유 규칙 위반이다. 통합 담당이 풀지 않고 나중에 온 브랜치를 담당자에게 돌려보낸다. 같은 파일을 두 작업이 가져야 했다면 `tasks.json`의 `deps`가 빠진 것이므로 tech-lead가 그래프를 고친다 |

병합 게이트가 실패하면 넣지 않는다. main은 언제나 `ci.sh full`이 통과하는 상태다.

## 4. 개발 환경과 Git 전략

### 4.1 환경

| 항목 | 값 |
|---|---|
| 기기 | 맥 한 대(개발과 서버를 겸한다). 서버 배치는 같은 맥미니의 launchd |
| .NET | SDK 10.0.201 (`global.json` 고정) |
| Unity | 6000.3.20f1. 지금 설치된 것은 macOS의 **Mono 변형뿐**이다(`mac-il2cpp` 모듈이 없다). Windows 모듈은 이 맥에서 `windows-mono`만 가능하다 |
| DB | 테스트: Docker의 `postgres:17` 고정 태그(`tools/ci/pg-test.sh`. 작업 트리마다 빈 포트, 테스트마다 임시 데이터베이스). Docker가 돌고 있지 않으면 `ci.sh full`은 건너뛰지 않고 실패한다(T-38). 배치: 맥미니에 직접 설치(아직 없다) |
| 셸 도구 | `/bin/bash`는 3.2.57이다(연관 배열, `mapfile`, `${x,,}` 없음). `timeout`, `flock`도 없다. 스크립트는 bash 3.2 문법만 쓰고 로직·시간 제한·잠금은 `tools/Wizard.Ci`에 둔다(T-40) |
| x64 | Rosetta 2는 있다. x64 dotnet 런타임은 없다(골든의 x64 열에 필요) |
| 셸 | zsh의 `git` 함수 래퍼가 깨져 있다. git은 `command git`, 그 밖의 명령은 절대 경로(`/bin/ls`, `/usr/bin/grep`)나 에이전트 도구를 쓴다. `cd` 대신 절대 경로와 `command git -C <경로>`를 쓴다 |
| 원격 저장소 | 지금 없다. 원격이 생기면 main을 병합 게이트 뒤에만 올린다. 강제 푸시는 하지 않는다 |

사용자가 직접 해야 하는 것(에이전트는 `sudo`를 쓰지 않고, 기기에 무엇을 설치하지도 않는다):

| 무엇 | 언제 필요한가 | 메모 |
|---|---|---|
| Unity `mac-il2cpp` 모듈 | M1-UE-03(IL2CPP 골든)부터 | sudo 없이 된다: `"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install-modules --version 6000.3.20f1 -m mac-il2cpp` |
| x64 dotnet 런타임 | M1-UE-03(골든의 x64 열)부터 | 사용자 폴더에 설치할 수 있다(`dotnet-install.sh --architecture x64 --install-dir <폴더>`) |
| Docker 실행 | M10-DB-01부터 | 이미 깔려 있다. 돌고 있기만 하면 된다 |
| PostgreSQL과 Caddy 설치(배치용) | M10-OPS-01 | |
| LaunchDaemon plist 설치 | M10-OPS-01 | 로그인 없이 서버를 계속 띄우려면 `/Library/LaunchDaemons`(root 소유)에 넣어야 하고 sudo가 든다. LaunchAgent로는 로그아웃하면 서버가 내려간다. 스크립트는 plist를 만들고 검증하는 데까지만 한다 |
| 공유기 포트와 도메인 | M10-OPS-01 | |
| 메일 사업자의 계정, API 키, 발신 도메인의 SPF·DKIM | M10-ACC-05 | 사업자 선택은 `OPEN-QUESTIONS.md` |
| Windows 기기(또는 가상 머신) | Windows 배포물을 낼 때 | Windows IL2CPP는 Windows에서만 빌드된다. Mono로 낼 때도 골든은 Windows 실기에서 돌린다(T-39) |
| `CLAUDE.md`와 `.claude/agents/`의 승인 | M0-TL-02 | |

### 4.2 브랜치와 커밋

- `main` — 언제나 통과 상태. integration-engineer만 넣는다.
- `task/<id>` — 작업 하나에 브랜치 하나. main에서 갈라져 main으로만 돌아간다. 작업 브랜치끼리 서로를 합치지 않는다.
- 태그 `gate/M<N>` — 마일스톤 게이트를 통과한 main의 커밋.

커밋 메시지:

```
<type>(<작업 id>): <무엇을 했는가, 한 줄>

<왜, 무엇이 달라지는가. 수락 테스트 결과 요약>

Co-Authored-By: <그 커밋을 만든 모델의 줄>
```

- type: `feat`, `fix`, `test`, `data`, `docs`, `build`, `refactor`, `perf`, `art`, `chore`
- Co-Authored-By 줄: Opus는 `Claude Opus 5.5 <noreply@anthropic.com>`, Fable은 `Claude Fable 5.2 <noreply@anthropic.com>`, Sonnet은 `Claude Sonnet 5.5 <noreply@anthropic.com>`
- 커밋과 푸시는 사용자가 이 파이프라인을 돌리라고 한 범위에서만 한다. 작업 브랜치 안의 커밋은 담당자가, main의 커밋은 integration-engineer가 만든다.

### 4.3 작업 트리(worktree)

- 위치: `<저장소>/.claude/worktrees/<작업 id>` (`.gitignore`에 넣는다). 작업 하나에 작업 트리 하나. 저장소 밖으로 빼지 않는 이유: 에이전트가 쓸 수 있는 폴더가 작업 디렉터리 아래로 묶여 있다. 저장소 전체를 훑는 도구(VersionStamp, 이름 검사, 계층 검사)는 `src/`, `server/`, `tests/`, `tools/`, `data/`, `unity/Assets/`를 뿌리로 훑고 `.claude/`를 보지 않으므로 작업 트리 사본을 같이 세지 않는다.
- 작업 트리 안의 명령은 전부 절대 경로다: `$WT/tools/ci/ci.sh fast`, `dotnet build $WT/Wizard.sln`, `command git -C $WT …`, `-projectPath $WT/unity`. `ci.sh`와 모든 도구는 저장소의 뿌리를 현재 폴더가 아니라 **자기 위치**에서 구한다(스크립트의 경로, `git -C <자기 폴더> rev-parse --show-toplevel`). 읽기·고치기 도구도 `$WT` 아래 경로만 쓴다.
- 만들기와 지우기는 `taskgraph start`, `taskgraph done`이 한다.
- dotnet의 출력은 작업 트리마다 자기 `artifacts/`로 간다. 서로 섞이지 않는다.
- `unity/` 아래를 가진 작업은 시작할 때 main 쪽의 `unity/Library`를 APFS 복제(`cp -c`)로 가져와 첫 가져오기를 줄인다. `Library`는 커밋하지 않는다.
- Unity 편집기를 띄우는 작업은 동시에 둘까지만 돌린다 [총괄 제안]. 코어·규칙·넷코드·화면 로직·서버 작업은 Unity를 띄우지 않으므로 이 제한을 받지 않는다.
- 서버 테스트는 작업 트리마다 다른 임시 데이터베이스 이름과 빈 포트를 쓴다. 유닉스 소켓은 `/tmp/wz/<작업 트리 경로의 해시 8자>-<목적>.sock`에 둔다. macOS의 소켓 경로는 104바이트까지라서 작업 트리의 `artifacts/` 아래에 두면 bind가 실패한다(CONTRACTS 7.3절).
- Unity는 프로젝트를 열 때 스스로 파일을 고쳐 쓴다(`Packages/packages-lock.json`, `ProjectSettings/*.asset`의 형식 갱신, `ProjectVersion.txt`, `.meta`가 없는 새 파일의 무작위 GUID). 그래서 Unity를 띄우는 모든 스크립트는 **먼저 `metagen`을 돌리고**, 끝난 뒤 위 세 가지의 변경을 되돌린다(`command git -C $WT checkout -- …`). 그 파일을 실제로 고치는 것은 그것을 `owned_paths`에 가진 unity-engineer 작업뿐이다.

### 4.4 코드 충돌 방지

충돌은 병합 때 푸는 것이 아니라 생기지 않게 한다.

1. **소유 경로.** 모든 작업은 `owned_paths`만 고친다. `tasks.json`은 "경로가 겹치는 두 작업은 반드시 `deps`로 순서가 정해져 있다"와 "같은 `parallel_group` 안의 경로는 겹치지 않는다"를 지킨다. `taskgraph validate`가 검사하고 `taskgraph check-diff`가 브랜치마다 확인한다.
2. **뼈대 선행(T-33).** 함께 고칠 수밖에 없는 파일(솔루션, 열거형, 틱 파이프라인, 상태 절 목록, 팩 절 목차, 서버 시작 코드)은 M0~M1에서 한 번에 만들고, 뒤 작업은 자기 폴더에 새 파일만 더한다.
3. **폴더 하나에 주인 하나.** 모듈은 자기 폴더와 자기 테스트 폴더를 가진다(`src/Wizard.Sim/Heat/` ↔ `tests/Wizard.Sim.Tests/Heat/`).
4. **번호 미리 주기.** DB 마이그레이션 파일의 번호는 `tasks.json`이 미리 정했다(`0010_accounts.sql`, `0020_inventory.sql` …). 작업이 번호를 고르지 않는다.
5. **장면과 프리팹을 쓰지 않는다(T-32).** 화면은 코드로 만든다.
6. **`.meta`는 계산한다(T-34).**

공유 추가 전용 파일 — `owned_paths`에 없어도 **줄을 더할 수만** 있다. 남의 줄을 고치거나 지우지 않는다.

| 파일 | 규칙 |
|---|---|
| `data/names/names.ko.json` | 자기 영역의 열쇠만 더한다. 형식은 T-35 |
| `Directory.Packages.props` | 패키지 버전 한 줄. 이름순. 새 외부 의존은 결과 보고에 적고 검토에서 승인받는다 |
| `src/`·`server/`·`tests/`·`tools/`의 `*.csproj` | `<PackageReference Include="…" />` **한 줄 추가만**. 계획된 의존은 M0-BT-01이 미리 넣었으므로(T-37) 이것은 계획에 없던 의존이 생겼을 때의 길이다. 검토 승인이 있어야 한다 |
| `unity/Assets/Plugins/`의 새 DLL과 그 `.meta` | 위와 같은 경우에만. asmdef의 `precompiledReferences` 한 줄 추가를 같이 허용한다 |
| `tests/golden/CHANGELOG.md` | 골든을 다시 만든 작업이 한 줄을 더한다(작업 id, 골든, 이유) |
| `tests/fixtures/` 아래의 새 파일 | 파일을 더하기만 한다 |
| `src/**`, `unity/Assets/**`의 `.meta` | MetaGen이 만든 것만 |

다시 만드는 파일 — `owned_paths`에 없어도, **도구의 출력과 바이트가 같으면** 바꿀 수 있다(T-41). `taskgraph check-diff`가 다시 만들어 대조한다.

| 파일 | 만드는 도구 | 규칙 |
|---|---|---|
| `src/*/Generated/*.g.cs`(`SimVersion.g.cs`, 표) | `versionstamp`, `tablegen` | 코어의 `.cs`를 고친 작업은 `SimVersion.g.cs`를 다시 만들어 같이 커밋한다. 병합 때 통합 담당이 main 위에서 다시 만든다 |
| `tests/golden/**/expected*.json` | `replaycli golden --update` | 해시가 바뀔 이유가 있는 변경일 때만. `tests/golden/CHANGELOG.md`에 이유를 적는다. 검토 항목 9번이 본다. 통합 빌드가 모든 조합에서 다시 확인한다 |

`check-diff`가 무시하는 변경(Unity가 스스로 고쳐 쓰는 것. 커밋에 들어 있으면 안 된다): `unity/Packages/packages-lock.json`, `unity/ProjectSettings/ProjectVersion.txt`, `unity/ProjectSettings/*.asset`의 형식 갱신, `unity/Library/`, `unity/*.csproj`, `unity/*.sln`. 스크립트가 실행 뒤 되돌린다(4.3절).

### 4.5 Unity 프로젝트 규칙

- 공유 코드는 `src/`의 패키지다. Unity용 사본을 만들지 않는다.
- `unity/Assets/Game/` 아래의 코드는 그리기와 입력만 한다. 규칙과 계산은 `src/`에 있다.
- 새 파일을 더하면 `metagen`으로 `.meta`를 만든다. `metagen --check`가 병합 게이트에 있고, `src/`와 `unity/Assets/Game/`의 GUID가 경로의 MD5와 같은지도 본다(Unity가 먼저 만든 무작위 GUID가 굳지 않게). MetaGen은 확장자별 가져오기 블록(`MonoImporter`, `AssemblyDefinitionImporter`, `PackageManifestImporter`, `PluginImporter`, `folderAsset`)을 Unity와 같은 형식으로 써서 Unity가 다시 쓰지 않게 한다.
- Unity 호출: 빌드처럼 동기로 끝나는 진입점만 `-quit`을 쓴다. 테스트는 `-runTests -testPlatform … -testResults <절대 경로>`(또는 콜백에서 `EditorApplication.Exit`)로 돌리고 `-quit`을 붙이지 않는다. 붙이면 테스트가 돌지 않고 0이 나온다. 프레임 시간은 `-nographics` 없이 플레이어로 잰다. 멈춘 실행은 `Wizard.Ci`의 시간 제한이 끊는다.
- 데이터 팩(`*.wzpack`)과 `unity/Assets/Art/`의 동기화된 그림은 만들어지는 것이라 커밋하지 않는다. Unity 빌드 전 단계와 CI가 만든다. 생성 코드(`*.g.cs`)는 커밋한다.
- `ProjectSettings/`, `Packages/manifest.json`, 장면 파일은 unity-engineer의 작업만 고친다.

### 4.6 CI/CD

원격 CI 서비스에 기대지 않는다. 검사는 전부 `tools/ci/`의 스크립트이고 이 기기에서 돈다. 원격 저장소와 러너가 생기면 같은 스크립트를 부르기만 한다.

| 단계 | 언제 | 무엇 | 명령 |
|---|---|---|---|
| 커밋 훅 | 커밋마다 | 커밋 메시지 형식, 금지 API의 빠른 검사, `.meta`, 이름 표 형식 | `tools/ci/hooks/` (`install-hooks.sh`로 건다) |
| 작업 게이트 | 검토를 청하기 전 | 빌드(Debug, Release), 분석기, 계층, 생성 파일 일치, 바뀐 프로젝트의 테스트, 벡터 대응 | `tools/ci/ci.sh fast`, `vector-coverage --task <id>` |
| 병합 게이트 | main에 넣기 전 | 전체 테스트, 결정론, 넷코드 매트릭스, 프로토콜, 데이터 검사, 서버 통합, Unity 배치 컴파일과 EditMode 테스트 | `tools/ci/ci.sh full` |
| 통합 빌드 | 승인된 브랜치들을 넣은 뒤 | Unity 플레이어 빌드(Mono, IL2CPP × arm64, x64), 런타임 × 아키텍처 골든, 2인 헤드리스 결투, 성능, PlayMode 테스트 | `tools/ci/ci.sh integration` |
| 마일스톤 게이트 | `M<N>-INT` | 위 전부 + 그 마일스톤의 완료 기준 + 사람 확인 항목 기록 | `tools/ci/ci.sh gate M<N>` → `docs/dev/gates/M<N>.json` |

`ci.sh`는 `tools/ci/`의 다른 스크립트 가운데 머리말에 `# ci-stage: fast|full|integration`이 적힌 것을 이름순으로 부른다. 새 검사는 스크립트 파일을 더해서 넣는다(`ci.sh`를 고치지 않는다). 스크립트 하나만 돌릴 때는 `ci.sh run <스크립트 이름>`이다. 하위 명령은 없다: 생성 파일 대조는 `tools/ci/gen-check.sh`(fast, M0-BT-03), 이름 검사는 `tools/ci/names-scan.sh`(fast, M1-CONT-02), 작업 그래프 검사는 `tools/ci/tasks-check.sh`(fast, `node tools/check-tasks.mjs`를 부른다)다.

- 스크립트는 `#!/bin/bash`와 bash 3.2 문법만 쓰는 껍데기다. 단계 실행, 시간 제한(`Process.WaitForExit`), 잠금(파일 배타 열기), 바뀐 경로에 따른 단계 선택은 `tools/Wizard.Ci`(C#)가 한다(T-40).
- 스크립트와 도구는 저장소 뿌리를 자기 위치에서 구한다. 현재 폴더에 기대지 않는다.
- 필요한 것이 기기에 없을 때(Docker, `mac-il2cpp`, x64 런타임) 그 검사는 **건너뛰지 않고** 무엇이 없는지와 사용자가 할 일을 적고 실패한다.

배포(M10부터):

| 대상 | 방법 |
|---|---|
| 서버 | `tools/ci/deploy-server.sh`: `dotnet publish -r osx-arm64` → 마이그레이션 → launchd 재시작 → `/healthz` 확인. 실패하면 직전 배포물로 되돌린다. 검증 워커는 코어 버전마다 따로 빌드해 최근 두 버전을 같이 둔다 |
| 클라이언트 | `tools/ci/release/build-client.sh`: IL2CPP 빌드 → 골든 실행 → 버전이 붙은 묶음. 빌드를 직접 배포한다(DECISIONS 1.13) |
| 상태 파일 | `tools/ci/release/publish-status.sh`: Ed25519로 서명해 맥미니 밖의 정적 호스팅에 올린다 |
| 데이터 팩 | `databuild build` → 서버의 `content/packs/<data_version>.wzpack`. 켜진 팩은 DB의 `config`가 가리킨다 |

배포는 사용자가 하라고 할 때만 한다. 비밀값(DB 암호, 서명 키, pepper)은 저장소에 두지 않는다.

## 5. AI 코딩 가이드라인

### 5.1 CLAUDE.md

모든 에이전트가 자동으로 읽는 짧은 규칙이다. 문서 우선순위, 결정론 금지 목록, 코딩 규약, 소유 경로, 테스트 의무, 명령, git 규칙, 이름과 수치의 위치, 일정·삭감 금지를 담는다. 자세한 내용은 이 문서와 dev 문서를 가리키기만 한다. 길어지면 규칙이 묻히므로 200줄을 넘기지 않는다.

### 5.2 코딩 규약

| 주제 | 규약 |
|---|---|
| 언어 | 공유 코드는 C# 9(`netstandard2.1`). 서버·도구·테스트는 최신 C#. 금지 문법과 API는 ARCHITECTURE 3.3절 |
| 이름 | 타입·메서드·속성은 PascalCase, 지역 변수와 매개변수는 camelCase, 비공개 필드는 `_camelCase`. 식별자는 영어. 주석은 한국어로 써도 된다 |
| 파일 | 공개 타입 하나에 파일 하나, 파일 이름 = 타입 이름. 네임스페이스 = 폴더. 예외 셋(분석기가 이 세 꼴만 허용한다): 부분 클래스 `SimWorld.<모듈>.cs`와 `ContentDb.<절>.cs`는 모듈 폴더에 두되 네임스페이스가 `Wizard.Sim`, `Wizard.Content`다. `ErrorCode` 묶음은 `src/Wizard.Core/Errors/`에 두되 네임스페이스가 `Wizard.Core`다 |
| 단위 | 이름에 단위를 붙인다: `…Ticks`, `…Hu`, `…Centi`(센티도), `…Permille`, `…Bp`(만분율), `…Raw`. 붙지 않은 `Fix64`는 칸 단위 |
| 수치 | 기획 수치는 `data/`에만. 코드의 숫자 리터럴은 0, 1, 2와 단위 환산 상수, 자료구조 크기뿐이다 |
| 문자열 | 화면에 보이는 글자는 이름 표에만. 코드는 `NameKey`와 id를 넘긴다. 로그와 예외 문구는 영어 |
| 오류 | CONTRACTS 0.2절. 코어는 규칙상의 "안 됨"을 예외로 던지지 않는다. 규칙 라이브러리는 `RuleCheck`, 클라이언트는 `Result<T>` |
| 상태 | 코어의 상태는 값 형식 배열과 고정 크기 풀. 참조는 정수 핸들. 새 상태를 더하면 스냅숏·직렬화·해시 훅에 같이 넣는다 |
| 의존 | 새 외부 패키지는 검토 승인이 있어야 한다. 코어 세 어셈블리는 외부 의존이 없다 |
| 주석 | 왜 그렇게 했는지와 명세 절 번호를 적는다. 코드가 말하는 것을 되풀이하지 않는다 |
| 테스트 | 명세 벡터의 테스트 이름은 `S<명세 번호>_<벡터 id>__<설명>`(예: `S01_T_H04__LockAtExactly100`). 기대값은 명세에서 가져온다. 구현을 돌려 얻은 값을 기대값으로 쓰지 않는다(골든 해시만 예외이고, 골든은 모든 런타임·아키텍처 조합이 같아야 한다) |

### 5.3 시스템별 인터페이스 계약

시그니처는 `CONTRACTS.md`가 정본이고 코드의 계약 파일이 그대로 따른다. 계약 파일은 그 줄의 "계약 코드 작업"만 만들고, 바꾸려면 3.7절 절차를 거친다.

| 시스템 | 계약 (CONTRACTS) | 계약 코드의 위치 | 계약 코드 작업 | 구현 축 |
|---|---|---|---|---|
| 고정소수점, 난수, 해시, 자료구조, 오류 | 0.2, 1절 | `src/Wizard.Core/` | M1-CORE-01~05 | 전투 |
| 콘텐츠 id, 정의 타입, 팩 | 2절, 11.2절 | `src/Wizard.Content/{Ids, Defs, Pack}/` | M1-CONT-01~03 | 기반, 데이터 |
| 입력, 세계, 표시 이벤트, 뷰 | 3.1~3.3절 | `src/Wizard.Sim/{Input/Types, Events, Runtime/Contracts, Runtime/Core}/` | M1-SIM-01, M1-SIM-02 | 전투 |
| 주문 컴파일러 | 3.4절 | `src/Wizard.Sim/Spell/` | M2-SPL-02 | 전투 |
| 커맨드 판정기와 비기 | 3.5절 | `src/Wizard.Sim/Arts/` | M1-SIM-08, M5-ART-01 | 전투 |
| 적과 보스 | 3.6절 | `src/Wizard.Sim/{Enemies, Bosses}/` | M3-ENM-01, M8-BOS-01 | 전투 |
| 웨이브, 점수, 판의 결과 | 3.7절 | `src/Wizard.Sim/Waves/` | M3-WAV-01, M6-SIM-01 | 로그라이크 |
| 재실행, 입력 기록 | 3.8절, 11.1절 | `src/Wizard.Sim/Replay/` | M1-SIM-07 | 전투 |
| 결투 코어 | 3.9절 | `src/Wizard.Sim/Duel/` | M14-DUL-01 | 전투 |
| 규칙 라이브러리 | 4절 | `src/Wizard.Rules/Common/` | M6-RUL-01 | 로그라이크 |
| 이름 표 | 5절 | `src/Wizard.Text/` | M6-TXT-01 | UI |
| 백엔드, 저장 | 6.1, 6.2절, 11.3절 | `src/Wizard.Client/{Backend/Contracts, Save/Core}/` | M6-CLI-01 | 멀티플레이 |
| 결투 전송, 넷코드 코어 | 6.3, 6.4절 | `src/Wizard.Client/Duel/`, `src/Wizard.Netcode/` | M2-NET-03~05, M2-NET-07 | 멀티플레이 |
| 입력원 | 6.5절 | `src/Wizard.Client/Input/` | M1-CLI-01 | 멀티플레이 |
| 서버 트랜잭션, 원장, 멱등 | 7.2절, 12절 | `server/Wizard.Server.Data/Core/` | M10-DB-01 | 서버 |
| 결투 프로세스 내부 API | 7.3절 | `src/Wizard.Protocol/Internal/` | M14-PRO-01 | 멀티플레이 |
| HTTP API | 8절 | `src/Wizard.Protocol/Http/` | M10-PRO-01~03, M11-PRO-01, M13-PRO-01, M14-PRO-01 | 멀티플레이 |
| 웹소켓 | 9절 | `src/Wizard.Protocol/Realtime/` | M12-PRO-01 | 멀티플레이 |
| 결투 UDP | 10절 | `src/Wizard.Protocol/Duel/` | M2-NET-01 | 멀티플레이 |
| 표시 계층 | 13절 | `src/Wizard.ViewLogic/{Assets, Presentation}/`, `unity/Assets/Game/Presentation/` | M9-AST-01, M9-PRS-01, M9-PRS-03 | UI |
| 화면 관리 | spec 13 1절 | `src/Wizard.ViewLogic/{Stack, Common}/` | M4-UL-01 | UI |

### 5.4 에이전트용 규칙과 스킬

- **에이전트 정의**(`.claude/agents/*.md`): 책임, 소유 경로, 꼭 지킬 규칙, 완료 정의. 에이전트마다 자기 축의 함정(결정론, 권한, 멱등)을 적어 두었다.
- **작업 카드**(3.4절)가 작업마다의 지시이고, 에이전트 정의는 바뀌지 않는 부분이다.
- **본보기 문서**: 되풀이되는 일은 첫 작업이 절차 문서를 남긴다. 지금 계획된 것은 `docs/dev/howto-data-slice.md`(M1-CONT-03, 데이터 절 하나를 더하는 절차)다. 화면 하나를 더하는 절차는 M4-UL-01과 M4-UE-01이, 서버 유스케이스 하나를 더하는 절차는 M10-ENH-01이 결과 보고에 남기고 tech-lead가 `docs/dev/`에 옮긴다.
- **스킬과 훅**: M0이 끝나면 되풀이 명령을 스킬로 묶을 수 있다(작업 시작, 결과 보고 틀, 벡터 대응 확인). "소유 경로 밖을 쓰면 막는다"를 도구 훅으로 걸 수도 있다(`taskgraph check-diff`를 부른다). 스킬과 훅은 에이전트의 설정을 바꾸는 일이므로 사용자가 승인한 뒤에만 넣는다.

### 5.5 설치

`CLAUDE.md`와 `.claude/agents/`는 이후 모든 에이전트의 지시문이 된다. 그래서 이 설계 단계에서는 제자리에 두지 않고 `docs/dev/agent-config/`에 원본을 두었다. 사용자가 읽고 승인하면 `M0-TL-02`가 아래처럼 옮긴다.

```
/bin/cp docs/dev/agent-config/CLAUDE.proposed.md CLAUDE.md
/bin/mkdir -p .claude/agents
/bin/cp docs/dev/agent-config/agents/*.md .claude/agents/
```

그 뒤 이 두 곳의 변경은 사용자 승인이 있는 tech-lead 작업으로만 한다. 원본(`docs/dev/agent-config/`)을 먼저 고치고 같은 내용을 옮긴다. CI의 `agent-config-check.sh`(fast)가 "설치본과 원본이 바이트 단위로 같다"를 본다. 모델별 변형 에이전트 파일(`*-opus.md`)은 머리말의 `name`·`description`·`model`만 다르고 본문이 기본 파일과 같아야 한다(같은 검사가 본다).

M0-TL-02는 M0의 첫 작업이다. 그 전에 띄우는 작업 카드에는 원본의 2절·2.5절·3절을 그대로 붙인다.

## 6. 에셋 제작 파이프라인

그림과 소리는 사용자가 만든다. 코드는 에셋이 없어도 전 범위가 돌고, 에셋은 들어오는 대로 꽂힌다. 규격의 정본은 `docs/spec/14-content-ids-assets.md` 4절과 5절이고, 여기에는 흐름과 요점만 적는다.

### 6.1 지금 있는 것

`art/production/`에 그림 1,000여 장과 매니페스트 다섯 개(`ui`, `players`, `wands`, `monsters`, `effects`)가 있다. 아직 검수 전이다(spec 14 5.1절). 전장·광장·돌마당 타일과 소리는 없다. `art/player/`는 원본 기준 그림이고 반입 대상이 아니다.

### 6.2 규격 요점

| 항목 | 규격 | 근거 |
|---|---|---|
| 배율 | 1칸 = 64px, 기준 화면 1920×1080, 그림 1픽셀 = 화면 1픽셀 | DECISIONS 2.1 |
| 캔버스 | 캐릭터 64×64(발밑 기준점 30, 60). 적 64×64, 큰 적은 96·128·192 단위. 손에 든 지팡이 32×64(손잡이 16, 48). 강화 화면용 지팡이 192×192. 지팡이 아이콘 64×64 | spec 14 4.1절 |
| 화면 그림 | 위치와 크기는 8px의 배수. 누를 수 있는 것은 32×32 이상. 확대는 정수 배만 | spec 14 4.3절 |
| 상태 | `normal`, `hover`, `selected`, `pressed`, `on`, `disabled` | spec 14 4.3절 |
| 프레임 | 한 파일에 가로로 이어 그린다. 프레임 수는 이름 끝에 `_f<N>` | spec 14 4.4절 |
| 9조각 | 원본 한 장 + 매니페스트의 `nine_slice_ltrb` | spec 14 4.4절 |
| 표본화 | 점 필터, 밉맵 없음, 압축 없음 | spec 14 4.4절 |
| 위험색 | 색상각 330~15도이면서 채도·명도 70% 이상은 적의 공격과 예고 전용. 플레이어의 몸·지팡이·주문·배경·메뉴에는 0픽셀 | DECISIONS 2.2, spec 14 4.2절 |
| 팔레트 | 캐릭터당 16색 이하(지팡이 제외). 그믐은 원본에서 뽑은 16색 | spec 14 4.2절 |
| 글꼴 | 12px급과 16px급 비트맵, 현대 한글 11,172자 전부, 상업 이용과 수정이 허용된 것 | spec 14 4.3절 |
| 소리 | 식별자는 `effects.md` 19절. WAV 48kHz 16비트, `art/production/audio/manifest.json`(id, 파일, 버스, 반복 여부) **[총괄 제안]** | effects 19절 |

### 6.3 이름과 놓는 곳

코드는 파일 이름을 해석하지 않는다. **매니페스트가 정본**이고 파일 이름 규칙은 사람이 찾기 위한 것이다.

| 묶음 | 폴더 | 파일 이름 | 매니페스트의 열쇠 |
|---|---|---|---|
| 화면(UI) | `art/production/ui/{kit, icon, font, screen/<화면>}/` | `<ID 소문자>_<영문 이름>_<폭>x<높이>[_<상태>][_f<N>].png` (예: `k-20_button_large_48x64_hover.png`) | `id`(K-20, I-B1 …) + 변형 + 상태 |
| 캐릭터 | `art/production/players/<폴더 열쇠>/` | `body.png`, `portraits_64x64_f4.png`, `selection_192x192.png`, `silhouette_32x32.png` | 폴더 열쇠 → `char.1`~`char.5`, 클립 `<동작>_<방향>` |
| 지팡이 | `art/production/wands/<폴더 열쇠>/` | `hand.png`, `inspection.png`, `icon.png`, `parts/*.png` | `wand.1`~`wand.5`, 단계 0~13 |
| 몬스터 | `art/production/monsters/` | `<콘텐츠 id>.png` (`enemy.slime_moss.png`, `elite.<키>.overlay.png`, `boss.<키>.png`) | 콘텐츠 id, 클립 이름, 프레임별 기준점 |
| 효과 | `art/production/effects/{player, special, ritual, enemy, loot, wand, status}/` | `<열쇠>_<폭>x<높이>_f<N>.png` | `<묶음>/<열쇠>` |
| 전장·광장 | `art/production/world/` | 몬스터와 같은 꼴의 매니페스트를 새로 둔다 **[총괄 제안]** | 타일·소품 id |
| 배경 | `art/production/backgrounds/` | `<이름>_1920x1080.png` | UI 매니페스트의 `external` |

- 콘텐츠 id와 그림 열쇠가 다른 곳(속성 `frost` → `ice`, 폴더 열쇠 → 캐릭터 번호, 블록 → 탄 그림 열쇠)은 `data/presentation/asset_aliases.json` 한 곳에 적는다.
- 어느 콘텐츠에 어느 그림이 필요한지는 spec 14 5.2절 대응표와 `ui-assets.md`가 정한다.
- 이름에는 화면에 보이는 이름(달구, 초롱 …)이 아니라 id와 폴더 열쇠만 쓴다. 지금의 폴더 열쇠는 매니페스트가 이미 정한 것이라 그대로 둔다.

### 6.4 제작에서 게임까지

```
① 그릴 것 확인   assetcheck report → 슬롯마다 없음 / 반입됨 / 검수됨 / 실패
② 그린다         규격은 spec 14 4절
③ 놓는다         art/production/<묶음>/ 에 이름 규칙대로
④ 매니페스트     assetcheck scaffold <png> 가 항목 초안을 보여 준다 → 확인하고 manifest.json에 넣는다
⑤ 검사           tools/ci/art-check.sh
                   - 파일이 있고 PNG 크기가 (폭 × 프레임 수, 높이)와 맞는가
                   - 플레이어 쪽 그림에 위험색 픽셀이 0개인가. 적 몸의 색 규칙을 지키는가
                   - 큰 배경이 아틀라스에 들어가지 않았는가
                   - 콘텐츠 id마다 필요한 그림이 있는가(없으면 대체 그림으로 표시)
⑥ 커밋           art(<슬롯>): …   (그림은 art/ 에만 커밋한다)
⑦ Unity로        tools/ci/art-sync.sh → unity/Assets/Art/<묶음>/ 로 복사, .meta 계산,
                 가져오기 설정 강제(점 필터, 밉맵·압축 없음, 1칸 = 64px), 매니페스트대로 자르기,
                 카탈로그 다시 만들기 (Wizard.Unity.Editor.ArtImport.Run)
⑧ 게임에서       IAssetCatalog가 id로 찾아 준다. 코드와 바인딩 표는 이미 그 id를 가리키고 있다
⑨ 검수           게임 안에서 보고 assetcheck approve <id> → art/production/approved.json 에 (id, 파일 해시)
```

- `unity/Assets/Art/`와 카탈로그 에셋은 만들어지는 것이다. 손으로 고치지 않고 커밋하지 않는다. 원본은 언제나 `art/production/`이다.
- 없는 그림은 회청·나무색 바둑판에 id 글자를 찍은 대체 그림으로 나온다. 게임은 멈추지 않는다.
- 표시 이벤트에서 그림·소리로 가는 대응은 `data/presentation/event_bindings.json`이다. 새 효과 그림은 매니페스트에 넣으면 그 식별자로 바로 재생된다.

### 6.5 관리

| 상태 | 뜻 | 바뀌는 때 |
|---|---|---|
| 없음 | 매니페스트에 없다. 대체 그림이 나온다 | — |
| 실패 | 매니페스트에 있지만 검사를 통과하지 못했다. 대체 그림이 나온다 | 검사 실패 |
| 반입됨 | 검사를 통과해 게임에 나온다. 아직 사람이 확인하지 않았다 | 검사 통과 |
| 검수됨 | 사용자가 게임 안에서 보고 승인했다 | `assetcheck approve` |

- 승인은 파일 해시에 묶인다. 파일이 바뀌면 "반입됨"으로 돌아간다.
- `assetcheck report`가 슬롯별 상태를 `artifacts/asset-report.json`으로 낸다. 무엇이 남았는지는 이 보고서가 알려 준다.
- 그림을 바꿀 때 코드와 데이터를 고칠 일은 없어야 한다. 고쳐야 한다면 규격이나 식별자가 어긋난 것이므로 tech-lead에게 올린다.
- 시각 작업 `V-01`~`V-16`은 `blocked-on-assets`다. 해당 묶음의 그림이 "반입됨"이 되면 todo로 바꾼다. 이 작업들이 하는 일은 대체 도형을 실제 그림으로 바꾸는 표시 코드(합성 순서, 기준점, 스킨, 실제 FX·SFX 재생기)다.

## 7. 자동화된 품질 검증

### 7.1 품질 게이트 목록

| # | 게이트 | 통과 조건 | 도구 |
|---|---|---|---|
| G0 | 정적 검사 | Debug·Release 빌드 경고 0. 분석기 진단 0(결정론 금지 목록, C# 9 제한, 계층 참조, 결제 값 참조). 저장소의 `.editorconfig`와 `*.csproj`에 WZ 진단을 끄는 설정(`severity`, `NoWarn`, `WarningsNotAsErrors`)이 없다. 생성 파일이 다시 만든 것과 같다. `.meta` 빠짐 0이고 GUID가 계산값과 같다. 이름 표 형식. **제품 코드**(`src/`, `server/`, `unity/Assets/Game/`)의 이름 표 밖 한글 문자열 리터럴 0(`tests/`는 검사하지 않는다. 금칙어·글자 폭 벡터처럼 한글이 있어야 하는 테스트가 있다). 작업용 이름의 로마자 표기가 `src/`, `server/`, `data/`의 식별자에 없다(CONTRACTS 2.1절). `tasks.json` 검사(`check-tasks.mjs`). 에이전트 설정의 설치본 = 원본. 변경이 `owned_paths` 안. 커밋 메시지 형식 | `ci.sh fast` |
| G1 | 단위·벡터 | 작업의 `vectors`가 가리킨 벡터마다 테스트가 있고 통과한다. 빠짐 0. **전역 빠짐 0**: `docs/spec/*.md`의 "테스트 벡터" 절에 있는 모든 벡터 id가 정확히 한 작업에 배정되어 있다(미배정과 이중 배정 모두 실패) | `dotnet test`, `vector-coverage`, `check-tasks.mjs` |
| G2 | 결정론 | 7.3절 | `Wizard.Sim.Tests`, 골든, `unity-golden.sh` |
| G3 | 넷코드 | 7.4절 | `Wizard.Netcode.Tests`, `duel2p.sh`, `duel-net.sh` |
| G4 | 프로토콜 | JSON 픽스처를 두 코덱이 왕복. 패킷 바이트 픽스처 왕복. AEAD가 RFC 8439 벡터 통과. 무작위 바이트에 예외 없음 | `Wizard.Protocol.Tests` |
| G5 | 데이터 | 스키마, 참조 무결성, 약점·저항 분배(속성별 차이 1 이하), 회피 검산 R1~R4, 변덕의 금지 필드, 모든 id에 이름 열쇠. 같은 입력에서 팩 바이트가 같다 | `databuild check` |
| G6 | 서버 통합 | 멱등(같은 요청 두 번, 같은 키에 다른 본문은 충돌), 동시 요청, 교착 없음, 원장 대조(원장 합 = 잔액, 생성 − 소멸 = 현재 수량), 검증 불일치 회수. 실제 PostgreSQL(Docker). DB가 없으면 건너뛰지 않고 실패한다 | `Wizard.Server.Tests`, `pg-test.sh` |
| G7 | 빌드 | Unity 배치 컴파일, EditMode·PlayMode 테스트, 플레이어 빌드(Mono, IL2CPP), 서버 `publish` | `ci.sh integration` |
| G8 | 성능 | 7.5절의 한도와 기준선 대비 회귀 없음 | `perf.sh`, `load.sh` |
| G9 | 에셋 | 6.4절 ⑤의 검사 | `art-check.sh` |
| G10 | 검토 | 검토자의 승인. 작성자와 다른 인스턴스 | 3.6절 |
| G11 | 사람 확인 | `human_check` 항목을 사용자가 확인하고 게이트 파일에 기록 | `docs/dev/gates/` |

어느 때 무엇을 거치는가:

| 때 | 게이트 |
|---|---|
| 검토를 청하기 전(담당자) | G0, G1, 자기 영역의 G2~G6 |
| 검토 | G10 |
| main에 넣기 전(통합) | G0~G6 전부, G7 중 Unity 컴파일과 EditMode |
| 통합 빌드마다 | G7 전부, G2의 런타임 × 아키텍처 골든, G3의 2인 헤드리스 결투, G8 |
| 마일스톤 게이트 | 전부 + G11 |

### 7.2 통합 빌드마다 하는 것

사용자의 그림에 있는 "Unity 통합 프로젝트: 빌드, 2인 테스트, 성능 및 안정성 검증"을 통합 빌드의 고정 단계로 둔다.

1. Unity 플레이어 빌드: macOS의 Mono와 IL2CPP, 각각 arm64와 Intel 64-bit. IL2CPP에는 `mac-il2cpp` 모듈이 있어야 한다. 없으면 `unity-golden.sh`는 건너뛰지 않고 "모듈이 없다"와 설치 명령을 적고 실패한다.
2. 골든(런타임 × 아키텍처): dotnet(arm64, `-a x64`), Unity Mono(EditMode, arm64 플레이어, Rosetta의 x64 플레이어), IL2CPP 플레이어(arm64, Rosetta의 x64)의 해시가 전부 같다. 산술 경계 벡터(0 제수, `MinValue`)와 프로토콜 픽스처 왕복도 플레이어에서 돌린다. 배포하는 다른 조합(Windows)은 그 기기에서 돌린 결과 파일을 게이트에 붙인다(T-39).
3. 2인 헤드리스 결투: M3부터 기기 안 호스트로(`duel2p.sh`), M14부터 실제 UDP와 `wizard-duel`로(`duel-net.sh`).
4. 성능: 틱 시간, 할당, 재실행 속도, (M9부터) Unity 프레임 시간, (M16부터) 부하.
5. 안정성: 봇 입력의 긴 판을 끝까지 돌려 예외 0, 해시 일치. PlayMode 화면 순회에서 막히는 화면 0.

### 7.3 결정론 검증

| 검사 | 내용 | 처음 들어오는 작업 |
|---|---|---|
| 두 번 실행 | 같은 씨앗과 같은 기록을 두 번 돌리면 틱마다 해시가 같다 | M1-TE-01 |
| 입력 순서 | 같은 틱·같은 오프셋의 방향과 K는 방향이 먼저다 | M1-TE-01 |
| 재실행 | 기록 파일을 `ReplayRunner`로 돌린 해시와 `RunOutcome` 다이제스트가 원래 실행과 같다 | M1-SIM-07 |
| 되감기 일치 | 임의 틱에 스냅숏을 저장하고 1~16틱 나아갔다가 되돌려 다시 돌린 해시가 끊지 않은 실행과 같다. 무작위 입력 1,000판 | M1-TE-01 |
| 직렬화 일치 | 임의 틱의 `WriteState`를 새 세계에 `ReadState`해 이어 돌린 해시가 같다 | M1-TE-01 |
| 빌드 구성 | Debug와 Release의 해시가 같다 | M1-CORE-01 |
| 런타임 × 아키텍처 | 골든이 dotnet(CoreCLR), Unity Mono, IL2CPP에서, 각각 arm64와 x64(Rosetta 2)에서 같다. 지금 이 기기에서 Windows 모듈 없이 되는 x64 열: `dotnet test -a x64`, macOS Intel 64-bit 플레이어의 머리 없는 골든 실행 | M1-UE-03 |
| 산술 경계 | 0 제수(예외 분류), `MinValue ÷ −1`, 128÷64의 몫 넘침, 포화 경계의 벡터가 모든 조합에서 같다 | M1-CORE-01, M1-UE-03 |
| 동시 실행 | 세계 16개를 16스레드에서 같은 기록으로 동시에 돌린 해시가 단일 실행과 같다(정적 버퍼가 섞이지 않는다) | M1-TE-01 |
| 핸들 표 | 스냅숏 뒤 새 `SpellHandle`을 만들고 → 복원 → 다른 순서로 다시 만들어도 해시가 같다. `WriteState` → 새 세계 `ReadState` 뒤 모든 핸들이 실행된다 | M2-SPL-02, M1-TE-01 |
| 순번의 독립 | 명령과 키가 섞인 판의 실행 중 해시 = 재실행 해시. 순번을 전부 다른 값으로 바꿔 넣어도(순서는 같게) 해시가 같다 | M4-RUN-01 |
| 신뢰할 수 없는 기록 | 무작위 바이트, 틱당 9개, 역행, 넘침, 무효 명령이 든 기록이 예외 없이 `RecordCorrupt`로 나온다 | M1-SIM-07 |
| 프레임 독립 | 같은 물리 입력을 60·120·144·불규칙 프레임으로 나눠 넣어도 `InputEvent` 열이 같다 | M1-CLI-01 |
| 웨이브 독립 | 웨이브 w의 구성은 씨앗과 w에만 달려 있다 | M3-WAV-01 |
| 긴 판 | 봇 입력으로 웨이브 60 이상, 캐릭터 5 × 2판의 골든 | M8-TE-01 |
| 금지 목록 | 분석기가 실수, 시계, 해시 순회, 불안정 정렬을 컴파일 오류로 막는다 | M0-BT-02 |

- 골든은 `tests/golden/m<N>/`에 마일스톤마다 쌓인다. 규칙을 고치거나 상태 절을 더해 해시가 바뀌면 그 작업이 골든을 다시 만들고 이유를 `tests/golden/CHANGELOG.md`에 한 줄로 적는다(4.4절의 "다시 만드는 파일"). 이유 없이 골든을 다시 만들지 않는다. 검토자가 이유를 본다.
- 개체 풀만으로 이루어진 상태 절은 비어 있으면 해시에 아무것도 넣지 않는다(CONTRACTS 3.2절). 풀 모듈을 더하는 것만으로는 그 모듈을 쓰지 않는 옛 골든이 바뀌지 않는다.
- 해시가 갈라지면 `ReplayCli`가 처음 갈라진 틱과 상태 절을 알려 준다.

### 7.4 멀티플레이 동기화 검증

| 검사 | 내용 | 작업 |
|---|---|---|
| NetSim 매트릭스 | 호스트 1 + 클라이언트 2를 메모리 안에서 잇는다. 왕복 60/90/120ms × 지터 10/20/30ms × 유실 0/1/3% + 순서 바뀜과 중복. 모든 조합에서 두 클라이언트의 확정 해시 열이 호스트와 같다 | M2-NET-06 |
| 연속 유실 | 유실 모형에 버스트(좋은 상태/나쁜 상태의 두 상태 모형)와 정해진 수신 공백을 더한다. 200ms 연속 유실 중에 키를 누르고 뗀다 → 몰수 0, 유실로 통지, 그 뒤 입력은 정상으로 받는다. 늦은 이벤트 하나가 뒤따르는 패킷을 버리게 하지 않는다 | M2-NET-04, M2-NET-06 |
| 마감 여유 | 누른 순간의 틱 안 위치를 균등 분포로 준 입력의 폐기율이 왕복 60/90/120ms, 지터 15ms 이하에서 0이다. 못 넘으면 D의 표를 수치 조정 작업으로 올린다 | M2-NET-06 |
| 되감기 한도와 멈춤 | 모든 조합에서 되감기 깊이 8틱 이하. 한도에 걸리면 예측을 멈추고 기다린다(`Stalled`. 전체 상태를 청하지 않는다). 300ms 수신 공백 뒤 재개: 전체 상태 요청 0, 재개 직후 입력 폐기 0 | M2-NET-05, M2-NET-06 |
| 키 상태 맞추기 | 뗌 이벤트가 버려진 뒤 2틱 안에 서버와 클라이언트의 키 상태가 같다 | M2-NET-05 |
| 늦게 온 확정과 판정기 | 내 K 직전에 상대의 적중으로 결정이 깨지는 확정이 늦게 도착해도, 다시 돌린 판정(비용, 대체 기술)이 호스트와 같다 | M2-NET-06 |
| 판정 동등성 | 마감 안에 도착한 입력의 판정이 통신 없는 실행과 같다. 섬(0.07초)과 만개(0.18초)의 성공 여부가 입력 지연 2·3·4틱 모두에서 같다 | M2-NET-06, M14-NET-01 |
| 이벤트 단계 | 번복된 이벤트가 같은 틱에서 확정으로 나오지 않는다. 같은 이벤트가 두 번 확정되지 않는다 | M2-NET-05, M2-NET-06 |
| 확정 기록 | 호스트의 확정 입력 기록을 `ReplayRunner`로 돌리면 호스트의 최종 해시가 나온다 | M2-NET-04 |
| 2인 헤드리스(기기 안) | 머리 없는 클라이언트 둘 + 기기 안 호스트, 3,600틱, 30틱마다의 해시 열이 셋 다 같다 | M3-NET-01 |
| 2인 헤드리스(실제 UDP) | `wizard-api` + `wizard-duel`에 실제 UDP로 붙어 경기를 끝낸다. 해시 열, 결과, 레이팅 반영이 맞다 | M14-TE-01 |
| 끊김과 재접속 | 한쪽을 죽이면 유예 뒤 몰수, 유예 안에 돌아오면 새 키(epoch + 1)와 전체 상태를 받아 이어 간다. 프로세스를 죽였다 다시 붙기: 같은 (키, nonce)의 재사용 0, 순번 연속, 해시 일치. NAT 포트가 바뀐 패킷을 따라간다 | M14-NET-01, M14-TE-01 |
| 서버 주도 측정 | 클라이언트가 왕복을 속여도(붙들어 둔 시간을 부풀려도) 서버의 D와 대기열 판정이 서버가 잰 값으로 정해진다. 왕복이 한도를 넘는 클라이언트는 대기열에 들어가지 못한다 | M14-SRV-01, M14-TE-01 |
| 대체 경로 | UDP를 막으면 웹소켓으로 같은 바이트가 오간다. 자동 매치는 거절된다 | M14-TE-01 |
| 입력 검증 | CONTRACTS 10.6절의 순서: 패킷 안의 역행은 패킷 거부, 순번 공백은 유실로 소비, 늦은 시각과 이른 시각은 그 이벤트만, 키 상태 모순은 무시, 틱당 8개 초과, 재전송 패킷 | M2-NET-04 |
| 광장 | 클라이언트 40개의 위치가 서버 위치로 수렴한다. 속도 위반은 되돌려진다 | M12-NET-01, M12-TE-01 |
| 관전 | 120틱 전 시점의 상태(`TryWriteSpectatorStart`)에서 출발해 120틱 늦은 확정 입력만으로 관전 쪽 해시가 같다 | M14-NET-01 |
| 사람 2인 | 두 기기, 실제 회선에서 한 판 | M14-INT (사람 확인) |

### 7.5 성능 측정

한도는 명세의 값과 60틱에서 나온 것이다. 명세에 없는 값은 **[총괄 제안]** 이고 M1에서 잰 기준선을 보고 고친다. 재는 기기는 이 맥이다.

| 항목 | 한도 | 출처 |
|---|---|---|
| 결투 틱(두 사람, 탄 300개 이하) | 평균 0.2ms 이하, p99 0.5ms 이하 | spec 10 14.13절의 가정 0.2ms. p99는 [총괄 제안] |
| 무한 웨이브 틱(탄과 적이 상한까지 찬 최악 사례) | p99 1.0ms 이하 | [총괄 제안] |
| `Step`의 관리 힙 할당 | 0바이트 | CONTRACTS 3.2절(할당 없음) |
| 스냅숏 저장 + 복원 | 합쳐서 0.1ms 이하, 할당 0바이트 | [총괄 제안]. 되감기 8틱 + 1틱이 한 프레임 안에 들어야 한다 |
| 재실행 속도 | 실시간의 100배 이상 | spec 12 4절(판 재검증의 가정) |
| `wizard-duel` | 동시 50경기에서 틱 지연 p99 2ms 이하, 경기당 코어의 1.2% 이하 | spec 12 4절. 틱 지연은 [총괄 제안] |
| 광장 | 40명 인스턴스에서 한 사람의 내림 회선이 추정(약 36kbps)의 ±20% | spec 12 4절 |
| API | 강화 요청 p99 100ms 이하(로컬) | [총괄 제안] |
| 부하 | 동시 300명에서 위 서버 한도를 지킨다. 500명은 측정해 보고한다 | spec 12 4절의 목표 |
| Unity 프레임 | 최악 사례 전투 장면에서 프레임 p99 16.6ms 이하. 전투 중 프레임당 할당이 기준선보다 늘지 않는다 | [총괄 제안] |
| 회귀 | 어느 항목이든 `tests/perf/baseline.json`보다 15% 넘게 나빠지면 실패 | [총괄 제안] |

기준선은 마일스톤 게이트를 통과할 때 갱신한다. 더 느려진 값으로 기준선을 갱신하려면 tech-lead의 승인과 이유가 필요하다.

### 7.6 사람이 확인하는 것

자동으로 잴 수 없는 것은 숨기지 않고 `human_check`로 적어 두었다. 지금 18개 작업에 있다.

- 손맛의 통과 기준 세 가지(커맨드 성공률, 섬 성공률, 오발 비율) — M1, M5
- 한글 입력기(전투 입력, 채팅 입력) — M1, M12
- 설치와 기기(`mac-il2cpp` 모듈, x64 dotnet 런타임, PostgreSQL, Caddy, LaunchDaemon, 포트, 도메인, 메일 사업자의 키와 발신 도메인, Windows 기기) — M1, M10, M18
- 맥미니 실기의 배포, 부하, 복원, 전원 연습 — M10, M16
- 두 기기 실제 회선의 결투, 적중률 기준에 못 미칠 때 보정각을 15도로 할지 — M14
- QA 보고서를 본 뒤의 수치 조정 결정, 에셋 검수 — M18
- `CLAUDE.md`와 에이전트 정의의 승인 — M0

사람 확인이 끝나지 않았으면 게이트 파일에 "기록 대기"로 남기고, 그 항목에 기대지 않는 작업은 계속 간다.

## 8. 로드맵

날짜와 기간은 없다. 마일스톤 번호는 통합 게이트의 순서이고, 작업의 착수는 `deps`만 본다. 범위는 GDD 16절의 1~18단계와 `online.md` 11절의 1~10단계 전부다.

### 8.1 마일스톤

| # | 내용 | GDD 16 / online 11 | 작업 수 | 완료 기준(게이트) |
|---|---|---|---|---|
| M0 | 기반: 저장소 뼈대, 분석기, 생성 도구, CI, 작업 그래프 도구, Unity 뼈대 | — | 11 | 깨끗한 복제본에서 `ci.sh full` 통과. 금지 API와 형식이 틀린 커밋이 거절된다 |
| M1 | 결정론 코어 위의 손맛 시제품 | 1 / 1 | 26 | 런타임 × 아키텍처 골든 일치(dotnet, Mono, IL2CPP × arm64, x64). 회색 도형 시제품이 60틱으로 돈다. 화력 비 1.4 이상. 손맛 기준 셋은 사람 확인 |
| M2 | 주문 컴파일러 + 넷코드 뼈대 | 2 / (7의 앞부분) | 13 | 블록 16종의 벡터 전부. 넷코드 매트릭스 전 조합. 한 기기 2인 |
| M3 | 적과 웨이브의 뼈대 | 3 | 14 | 원형 4종, 예고, 웨이브, 약점·저항, 콤보·결정, 디버그 메뉴. 2인 헤드리스 결투 스모크 |
| M4 | 야영 화면 | 4 | 10 | 전투 → 전리품 → 야영(조립, 미리보기, 보관함) → 다음 웨이브. 야영 조작이 든 골든 |
| M5 | 비기 전체와 캐릭터 5명 | 5 | 15 | 캐릭터 다섯 각각의 골든(비기 15종). 손맛 기준을 다섯 명으로 따로 잰다 |
| M6 | 프로필과 오프라인 백엔드 | 6 / 2 | 17 | 오프라인으로 처음 실행 → 판 → 정산 → 상점 → 다시 실행. 이름 표 밖 문자열 0 |
| M7 | 강화 화면 | 7 / 2 | 10 | 강화, 변이, 슬롯 새기기, 흔적이 화면으로 되고 남는다. 마르코프 정확값. 경제 시뮬레이션 회귀 ±10% |
| M8 | 무한 웨이브 완성 | 8 | 21 | 적 12종, 엘리트, 보스 6종, 첫 만남과 복수, 변덕, R5, 시작 웨이브, 점수, 기록. 웨이브 60 이상 골든 |
| M9 | 겉모습 자리와 마감 | 9 | 15 | **서버 없이 완전한 게임.** 네트워크를 끊은 완주 자동 테스트. Mono·IL2CPP 빌드. 모든 그림 슬롯이 대체 그림으로라도 채워진다 |
| M10 | 서버 기반 | 10 / 3 | 22 | 로컬 서버에 가입·로그인·강화. 원장 대조. 서버를 꺼도 오프라인 프로필이 된다. 실제 메일 발송 구현(키는 사람 확인) |
| M11 | 판 제출과 검증, 순위표, 주간 도전 | 11 / 4 | 14 | 제출 → 재실행 facts의 바이트 일치 → 확정. 조작한 기록은 회수. 코어 버전 둘의 워커가 같이 돌고, 서버를 새 버전으로 바꾼 뒤에도 옛 버전의 판이 확정된다 |
| M12 | 광장 | 12 / 5 | 14 | 빌드 두 개가 같은 광장에서 보이고 말하고 친구가 된다. 서버 전체 알림 |
| M13 | 거래 | 13 / 6 | 12 | 직접 거래, 장터, 우편. 무작위 거래 1만 건 뒤 야간 대조 0건 |
| M14 | 결투 | 14 / 7 | 19 | 세 입장 경로의 경기. 실제 UDP 2인 헤드리스. 지연 2·3·4틱에서 섬·만개 동등. 재접속(새 키), 서버 주도 측정. 5×5 상성표와 적중률 측정 |
| M15 | 시즌 | 15 / 8 | 7 | 시험 시즌 하나를 끝에서 끝까지. 정산이 한 번만 된다 |
| M16 | 운영 | 16 / 9 | 6 | 관리 화면. 로컬 부하 300명. 복원 연습과 재시작 연습 |
| M17 | 결제 뼈대 점검 | 17 / 10 | 4 | 가짜 제공자로 세 흐름 통과 뒤 빈 제공자로 되돌림. 유료 상품 0개 |
| M18 | 밸런스 QA와 출시 | 18 | 5 | 8.3절의 출시 기준 |
| V | 시각 작업(에셋 대기) | 9, 18의 그림·소리·발표 자료 | 16 | 게이트 없음. 해당 묶음의 그림이 "반입됨"이 되면 풀린다. 출시는 이 작업들까지 done일 때다(8.3절) |

합계 271개: 지금 만들 수 있는 작업 255개, 에셋을 기다리는 시각 작업 16개(마일스톤 `V`).

### 8.2 마일스톤마다 같이 갈 수 있는 작업

`tasks.json`의 `parallel_group`이 정본이다. 같은 묶음의 작업은 서로 의존이 없고 경로가 겹치지 않는다. 묶음이 달라도 `deps`가 끝났으면 같이 돌 수 있다. 아래는 축으로 본 그림이다.

| # | 전투 | 로그라이크 | 멀티플레이·서버 | UI·Unity | 기반·데이터·테스트 |
|---|---|---|---|---|---|
| M0 | — | — | — | Unity 뼈대 | 뼈대 → (분석기, 생성 도구, MetaGen, 작업 그래프 도구, TestKit) → CI |
| M1 | Core 다섯(같이) → Sim 계약 → 세계 뼈대 → (세계·플레이어 → 과열 / 스침 / 판정기) → 임시 내용 | — | 입력 양자화 → RunSession | 앱 계층 → 도형 표시 → 골든 실행기 | id 열거형 → 팩과 DataBuild → 본보기 슬라이스. 결정론 묶음, 손맛 계측, 성능 하네스 |
| M2 | 컴파일러 → 실행기 → 피해 | — | (패킷 코덱 / AEAD / 시계) → (호스트 / 클라이언트) → Loopback | — | 블록 데이터. NetSim 매트릭스 |
| M3 | 적 런타임 → 예고 → 원형 4종. 콤보·결정 | 웨이브 디렉터 | 2인 헤드리스 스모크 | 적·예고 표시, 디버그 메뉴 | 적 데이터, 웨이브 데이터, 회피 검산기 |
| M4 | — | 야영 → 전리품 | — | ViewLogic 기반 → (HUD / 야영·조립 / 연습) → 뷰 기반 → 뷰 | — |
| M5 | 비기 공통 → (공용 ①~⑤ / ⑥~⑩ / 패시브 → 고유 비기 둘 묶음 / 지팡이 특성 → 변이) | 비기 해금 | — | 비기 탭 → 뷰 | 캐릭터·비기·트라이 데이터. 화력 검산 |
| M6 | — | Rules 계약 → (경제 / 판의 결과 → 정산) → 오프라인 백엔드 → RunSession 완성 | IBackend 계약과 저장 | Text. 시작·메뉴 → (원정 준비 / 정산·상점) → 뷰 | 이름 표 |
| M7 | — | 강화 규칙 1 → 2 → 새기기·기댓값 → 오프라인 강화 | — | 강화 모델 → (상태 기계 / 부속 화면) → 뷰 | 경제 시뮬레이션 회귀 |
| M8 | 신규 적 1 → 2 → 꼬리표·엘리트. R5. 보스 공통 → (보스 넷 묶음, 같이) | 변덕 → 점수·시작 웨이브 → 상한·복수 → 오프라인 기록 | — | 기록·추적기 → 뷰. 보스 표시 | 적·보스·변덕 데이터. 긴 판 결정론 |
| M9 | — | 꾸미기와 칭호 | — | (에셋 레지스트리 / 바인딩 표 / 연출 한도 / 설정 / 튜토리얼) → Unity 표시 버스, 반입, 뷰 | AssetCheck. 지팡이 겉모습 자료. 오프라인 완주 테스트 |
| M10 | — | — | Protocol 공통 → DTO. Data 기반 → 계정 → (인증 → 메일 / 삭제·제재) → 소유물 → 강화 → 상점. HTTP 전송 → ServerBackend, Boot | 인증 모델 → 뷰 | 배치. 서버 통합 묶음 |
| M11 | — | — | DTO. 스키마 → 출발 → 제출·정산 → (부정 방지 / 검증 반영) → 순위표 → 엔드포인트. 검증 워커. 클라이언트 판 서비스 | 순위표 모델 → 뷰 | 제출·검증 종단 |
| M12 | — | — | 메시지 → (광장 넷코드 / 허브) → (광장 인스턴스 / 채팅 → 사회 / 알림). 클라이언트 실시간 | 광장 모델 → 사회 모델, 광장 표시 → 뷰 | 광장 종단 |
| M13 | — | 거래 규칙 | DTO → 직접 거래 → (장터 / 우편) → 야간 대조. 클라이언트 거래 | (거래 / 장터·우편 모델) → 뷰 | 동시성·복제 방지 |
| M14 | 결투 코어 1 → 2 | 리그 규칙 | 넷코드 완성. DTO → (결투 프로세스 / 로비 서버) → 결과 반영. 전송, 봇 | (경기 / 로비 모델), 결투 표시 → 뷰 | 결투 데이터. 실제 UDP 2인, 상성표 |
| M15 | — | 시즌 규칙 | 시즌 서버, 클라이언트 서비스 | 시즌 모델 → 뷰 | 시즌 넘김 테스트 |
| M16 | — | — | 관리 화면, 운영 작업, 연습 스크립트 | 운영 화면 | 부하 시험 |
| M17 | — | — | 결제 뼈대, 상점 유료 탭 자리 | — | 가짜 제공자와 세 흐름 |
| M18 | — | — | — | — | 계측 둘, 출시 빌드, 문서 정리 |

멀티플레이 전제 때문에 순서를 당긴 것: 넷코드의 봉인·예측·되감기(M2), 2인 헤드리스 결투(M3). 결투의 규칙과 서버는 M14이지만 그때는 이미 검증된 넷코드 위에 규칙을 얹는다.

### 8.3 출시 기준

M18 게이트는 아래를 전부 본다. 하나라도 안 되면 그 항목을 고치는 작업을 만든다. 기준을 낮추지 않는다.

**범위**
- `tasks.json`에서 `blocked-on-assets`가 아닌 작업이 전부 done이고 M0~M17 게이트가 통과했다.
- 시각 작업 `V-01`~`V-16`도 출시 범위다(GDD 16-9의 그림·소리, 16-18의 발표 자료). 사용자가 에셋을 넣는 대로 풀려서 진행한다. M18 게이트는 "에셋만 남은 상태"를 확인하고, 출시는 시각 작업까지 done이고 모든 슬롯이 "검수됨"일 때다.

**결정론과 멀티플레이**
- 골든 전부(캐릭터 5, 보스 6, 결투 포함)가 dotnet, Unity Mono, IL2CPP에서 같다.
- 되감기 일치와 직렬화 일치 퍼저 통과. 웨이브 60 이상의 긴 판 통과.
- 넷코드 매트릭스 전 조합 통과. 입력 지연 2·3·4틱에서 섬과 만개의 성공이 통신 없는 판과 같다.
- 실제 UDP 2인 헤드리스 결투, 끊김·재접속, 대체 경로 통과. 두 기기 실제 회선의 사람 2인 테스트 기록.

**서버의 권한**
- 원장 대조 0건: 무작위 요청 1만 건, 무작위 거래 1만 건, 처리 도중 서버 재시작을 넣은 경우.
- 조작한 판 기록이 걸려 회수된다. 모든 변경 요청이 멱등이다. 강화의 난수는 서버가 뽑고 기록이 남는다.
- 결투 프로세스와 검증 워커가 재화·물건 표에 쓰지 못한다(DB 권한).

**서버 없이도, 결제 없이도**
- 네트워크를 끊은 "처음 실행 → 판 → 정산 → 강화 → 다시 실행" 자동 테스트 통과.
- 서버를 내린 상태에서 오프라인 프로필의 모든 기능이 된다.
- 출시 구성에 결제 제공자가 없고 유료 상품이 0개다. 그 상태에서 전 기능의 자동 테스트가 통과한다.

**운영**
- 로컬 부하 300명 통과, 500명 측정 보고. 맥미니 실기의 부하·복원·재시작·전원 연습 기록(사람 확인).
- 배포와 되돌리기가 스크립트로 된다. 운영 설명서가 있다.
- 성능 한도(7.5절) 전부.

**안전**
- 저장소와 배포물에 비밀값이 없다. 로그에 비밀번호·토큰·이메일 평문이 없다.
- 관리 화면은 127.0.0.1에서만 듣는다. 출시 빌드에 디버그 어셈블리와 가짜 결제 제공자가 없다.

**이름과 수치**
- 이름 표 밖에 화면 문자열이 없다. 캐릭터 표시 이름은 이름이 정해질 때까지 "제자 1"~"제자 5"다.
- 코드에 기획 수치가 없다. 데이터 검사(G5) 통과.
- GDD 17절과 `online.md` 12절의 QA 항목이 전부 측정되어 보고서로 있고, 조정할지를 사용자가 정했다(DECISIONS 1.17).

**빌드**
- 배포하는 모든 (OS, 아키텍처, 백엔드) 조합의 플레이어가 만들어지고 **그 조합에서** 골든을 통과한다. 그것이 그 조합의 배포 조건이다. macOS(arm64, x64)의 IL2CPP는 필수다. Windows는 T-39의 길(기본은 이 맥에서 만든 Mono 플레이어)로 만들고 Windows 실기의 골든 결과를 게이트에 붙인다.
- 출시 구성에서 `recovery_mail_enabled = true`이고 복구 메일이 실제로 도착한다(사람 확인). DECISIONS 1.24.
- 출시 빌드에서 디버그 op을 만들 수 있는 경로가 없다. 서버 publish 결과에 `Wizard.Server.Payments.Fakes.dll`이 없다.
- 동봉물(글꼴 등)의 사용 조건 문서가 묶음에 들어 있다.
