# 아키텍처 — 엄청엄청 위대한 마법사 키우기

이 문서는 저장소 구조, 어셈블리와 의존 방향, 런타임 구성, 데이터 흐름, 네트워크 권한을 정한다. 인터페이스의 실제 C# 시그니처는 `docs/dev/CONTRACTS.md`, 각 결정의 대안과 이유는 `docs/dev/DECISIONS-TECH.md`에 있다.

## 0. 문서의 위치

우선순위는 아래와 같다. 위가 아래를 이긴다.

1. `docs/DECISIONS.md` — 게임의 확정 사항과 공통 규격
2. `docs/spec/*.md` — 영역별 구현 명세(규칙, 수치, 테스트 벡터)
3. `docs/dev/*.md` (이 문서 묶음) — 기술 구조와 인터페이스 계약

단, **명세끼리 기술 표현이 다른 곳**(타입 이름, 단위, 식별자, 키 코드, 해시 알고리즘 등)은 이 문서 묶음이 하나로 정한다. 그 목록은 `CONTRACTS.md` 0.3절 "통합 표"다. 명세의 규칙과 수치는 그대로이고 표현만 통일한다.

일정과 범위 삭감은 다루지 않는다(DECISIONS 1.16). 순서는 의존 관계로만 적는다.

## 1. 설계 원칙

처음부터 멀티플레이를 전제로 만든다. 싱글 시스템을 만든 뒤 네트워크를 덧붙이지 않는다.

| # | 원칙 | 구조에서의 뜻 |
|---|---|---|
| P1 | 전투 규칙은 한 벌이다 | `Wizard.Sim`(60틱, Q32.32, PCG32)을 클라이언트, 결투 서버, 검증 워커가 **같은 소스**로 쓴다. 모드 차이는 `SimConfig` 값이다 |
| P2 | 싱글도 "플레이어 1명짜리 멀티"다 | `SimWorld`는 항상 플레이어 배열을 가진다(무한 웨이브 1명, 결투 2명). 입력은 언제나 `(슬롯, InputEvent)` 열로만 들어간다 |
| P3 | 코어는 화면을 모른다 | 코어는 그림, 소리, 이름 문자열, `UnityEngine`, 실수를 참조하지 않는다. 표시 이벤트와 읽기 전용 뷰만 내보낸다 |
| P4 | 입력 이벤트 구조체는 하나다 | 무한 웨이브 기록 파일, 결투 UDP 패킷, 다시 보기, 관전이 같은 `InputEvent`와 같은 정렬 규칙을 쓴다 |
| P5 | 서버가 정본이다 | 재화, 물건, 강화 난수, 결투 판정, 순위는 서버가 정한다. 무한 웨이브는 클라이언트가 돌리고 서버가 모든 판을 재실행한다 |
| P6 | 규칙 코드도 한 벌이다 | 강화, 정산, 상점, 수수료, IL은 `Wizard.Rules` 하나다. 서버와 `OfflineBackend`가 같은 소스를 쓴다 |
| P7 | 화면은 `IBackend`만 부른다 | 화면과 게임 로직은 프로필 종류(오프라인/서버)를 `Kind`로만 구분한다. 통신 코드를 직접 부르지 않는다 |
| P8 | 넷코드는 소켓과 분리한다 | 결투의 봉인, 확정, 예측, 되감기는 소켓과 시계가 없는 순수 클래스(`Wizard.Netcode`)다. 서버 프로세스, 클라이언트, 한 기기 2인, 봇 연습, 테스트가 같은 클래스를 쓴다 |
| P9 | 수치는 데이터다 | 기획 수치는 `data/`의 JSON 한 곳에 두고, 빌드 도구가 정수 이진 팩으로 바꾼다. 코어는 실행 중에 소수 문자열을 읽지 않는다 |
| P10 | 이름은 한 곳이다 | 화면 문자열은 이름 표(`data/names/names.ko.json`) 하나에만 있다. 코드, DB, 통신, 저장 파일은 id와 번호만 쓴다(DECISIONS 1.25) |
| P11 | 결제는 빈 자리다 | `IPaymentProvider`, `IPurchaseFlow`의 실제 구현 없이 모든 기능이 동작한다(DECISIONS 1.14) |
| P12 | 에셋은 나중에 꽂는다 | 그림, 이펙트, 소리는 식별자와 인터페이스(`IAssetCatalog`, `IFxPlayer`, `ISfxPlayer`)만 정의하고, 지금은 회색 도형과 빈 구현으로 돈다 |

## 2. 저장소 레이아웃

```
algorithm_project/
├ docs/                      기획(DECISIONS, GDD 등), spec/, dev/(이 문서 묶음), sim/(기획 검산 파이썬)
├ art/                       그림 현물과 매니페스트(player/, production/{ui,players,wands,monsters,effects,…})
├ data/                      기획 수치의 유일한 원본(JSON). 8절
│  ├ sim/                    전투: characters, blocks, arts, mutations, enemies, elites, bosses,
│  │                         waves, frames, modifiers, tod, scoring, starfall, arena, duel_rules
│  ├ rules/                  규칙: enhance, economy, shop, drops, bag, leagues, il, fees, season,
│  │                         titles, cosmetics, plaza(시설·채널 상수), limits(상한 식의 계수)
│  ├ names/names.ko.json     이름 표(유일한 위치. 형식은 T-35). DataBuild가 이진 절(names.ko.wznm)로 바꾼다
│  ├ presentation/           표시 연결: event_bindings, asset_aliases, particle_budget, wand_looks/
│  └ schema/                 각 파일의 JSON Schema
├ src/                       공유 C# (netstandard2.1, C# 9). 폴더 하나가 UPM 로컬 패키지 하나
│  ├ Wizard.Core/            고정소수점, 난수, 해시, 결정론 자료구조, 이진 코덱
│  ├ Wizard.Content/         id 열거형, 데이터 정의 타입, 이진 팩 읽기
│  ├ Wizard.Text/            이름 표, 틀·조사, 글자 폭
│  ├ Wizard.Sim/             결정론 전투 코어
│  ├ Wizard.Rules/           강화·경제·정산·리그 규칙
│  ├ Wizard.Protocol/        HTTP DTO, 웹소켓 메시지, 결투 패킷 코덱, 파일 형식
│  ├ Wizard.Netcode/         결투·광장의 호스트/클라이언트 로직(소켓 없음)
│  ├ Wizard.Client/          IBackend와 두 구현, 전송 구현, 판·결투 세션(유니티 무관)
│  └ Wizard.ViewLogic/       화면 상태 기계와 뷰 모델, HUD 모델, 바인딩 표, 연출 한도, 에셋 레지스트리, FixConvert(T-31)
├ server/                    서버 C# (net10.0)
│  ├ Wizard.Server.Data/     DB 접근, 마이그레이션 실행기, 원장·멱등 도우미
│  ├ Wizard.Server.App/      유스케이스(트랜잭션 스크립트): 강화, 정산, 거래, 장터, 시즌 …
│  ├ Wizard.Server.Api/      wizard-api: HTTPS, 웹소켓, 광장 인스턴스, 매치메이커, 백그라운드 작업
│  ├ Wizard.Server.Duel/     wizard-duel: UDP 소켓 + Netcode 호스트
│  ├ Wizard.Server.Verify/   wizard-verify: 재실행 워커(코어 버전마다 따로 빌드)
│  ├ Wizard.Server.Admin/    wizard-admin: 관리 웹(127.0.0.1 전용)
│  ├ Wizard.Server.Payments.Fakes/  가짜 결제 제공자. Server.Tests와 Api.DevHost만 참조한다(Api는 참조하지 않는다)
│  ├ Wizard.Server.Api.DevHost/     개발용 호스트(Api + 가짜 제공자 등록). 출시 배포물에 없다
│  ├ migrations/             번호가 붙은 SQL 파일
│  └ deploy/                 launchd plist, Caddyfile, 백업·DDNS 스크립트
├ unity/                     Unity 6000.3.20f1 프로젝트(2D URP). 4절
│  ├ Assets/Game/{App,Input,Presentation,UI,Debug}/   그리기와 입력만. 뷰는 코드로 만든다(T-32)
│  ├ Assets/Scenes/          Boot, Menu, Battle, Plaza. 시작 개체 하나씩만 둔다(T-32)
│  ├ Assets/Tests/{EditMode,PlayMode}/
│  ├ Assets/StreamingAssets/content/   빌드에 든 기본 데이터 팩과 이름 표
│  ├ Assets/Plugins/         Newtonsoft 이외의 외부 DLL(BouncyCastle)
│  ├ Assets/link.xml         IL2CPP 코드 제거에서 지킬 어셈블리(T-44)
│  ├ Directory.Build.props   빈 <Project/>. 최상위 빌드 설정을 물려받지 않는다(T-43)
│  ├ Packages/manifest.json  src/의 로컬 패키지를 file: 로 참조
│  └ ProjectSettings/
├ tests/                     xUnit (net10.0)
│  ├ Wizard.Core.Tests/  Wizard.Content.Tests/  Wizard.Text.Tests/  Wizard.Sim.Tests/  Wizard.Rules.Tests/
│  ├ Wizard.Protocol.Tests/  Wizard.Netcode.Tests/  Wizard.Client.Tests/
│  ├ Wizard.ViewLogic.Tests/ 화면 로직의 벡터(spec 13·14). Unity 없이 돈다(T-31)
│  ├ Wizard.Analyzers.Tests/  Wizard.DataBuild.Tests/  Wizard.AssetCheck.Tests/  Wizard.Perf/
│  ├ Wizard.Server.Tests/    PostgreSQL을 쓰는 통합 테스트(Docker의 고정 이미지, T-38)
│  ├ Wizard.TestKit/         테스트 공통. Shared/는 netstandard2.1 + C# 9의 UPM 패키지(골든 읽기를 Unity도 쓴다)
│  ├ golden/                 결정론 골든(씨앗, 입력 기록, 기대 해시). dotnet과 Unity가 같이 읽는다
│  └ fixtures/               프로토콜 JSON 픽스처, 패킷 바이트 픽스처, RFC 벡터
├ tools/                     net10.0 콘솔 도구(Wizard.Analyzers만 netstandard2.0)
│  ├ Wizard.DataBuild/       data/ JSON → 검증 → 이진 팩(.wzpack) + 이름 표 검사
│  ├ Wizard.VersionStamp/    공유 소스 해시 → SimVersion.g.cs
│  ├ Wizard.TableGen/        sin 표, 거듭제곱 표 → *.g.cs
│  ├ Wizard.ReplayCli/       .wzrc 재실행, 해시 출력, 두 실행 비교
│  ├ Wizard.StatusSign/      상태 파일 Ed25519 서명
│  ├ Wizard.LoadBot/         머리 없는 클라이언트(부하 시험, 결투 지연 실험)
│  ├ Wizard.Analyzers/       Roslyn 분석기(코어 금지 목록, 결제 값 참조 금지). netstandard2.0
│  ├ Wizard.Ci/              CI의 로직(단계 실행, 시간 제한, 잠금). tools/ci/의 셸은 이것을 부르는 껍데기(T-40)
│  ├ Wizard.MetaGen/         Unity .meta를 경로에서 계산해 만들고 검사한다(T-34)
│  ├ Wizard.{VectorCoverage, TaskGraph, AssetCheck, BalanceLab}/
│  └ check-tasks.mjs         tasks.json 검사(JSON, 위상정렬, 묶음 안 경로 겹침, 벡터 배정). node로 돈다
├ Wizard.sln                 (dotnet new sln --format sln. .slnx가 아니다)
├ Directory.Build.props      공통 설정(UseArtifactsOutput, Nullable, 경고를 오류로)
├ Directory.Packages.props   NuGet 버전 한 곳 관리
├ global.json                .NET SDK 10.0.201, rollForward: latestPatch
└ artifacts/                 모든 빌드 출력(.gitignore). src/ 안에 bin/obj가 생기지 않게 한다
```

`data/`와 `src/`를 저장소 최상위에 두는 이유: 서버, 클라이언트, 도구가 모두 같은 원본을 읽고, 어느 쪽도 다른 쪽의 폴더 안에 들어가 있지 않게 하려는 것이다.

## 3. 프로젝트와 어셈블리

### 3.1 목록

공유 어셈블리 아홉(전부 `netstandard2.1`, C# 9, `UnityEngine` 참조 없음):

| 어셈블리 | 책임 | 의존 | 근거 |
|---|---|---|---|
| `Wizard.Core` | `Fix64`, `FixVec2`, `Angle`, `FixMath`(제곱근, sin/cos 표, atan2), `Pcg32`, `RngStreams`, `StateHasher`, `ByteWriter`/`ByteReader`(LE, varint), `DetSort`, `SortedIntList`, `RingBuffer`, `EntityId`, `ContentId`, `Result<T>` | 없음 | spec 01 2~3절 |
| `Wizard.Content` | id 열거형(`CharacterId`, `Element`, `BlockId`, `ArtId`, `MutationId`, `EnemyTypeId`, `BossId` …), 정의 타입(`CharacterDef`, `BlockDef`, `ArtDef`, `EnemyDef`, `BossDef`, `WaveCurveDef`, `DuelRuleSet`, `EnhanceTable`, `DropTable` …), `ContentPack`(이진 팩 읽기와 해시), `ContentDb`(조회) | Core | spec 14, 각 명세의 데이터 절 |
| `Wizard.Text` | `NameTable`(이진 절을 `ByteReader`로 읽는다. JSON 파서 없음), 틀과 조사 처리, 글자 폭·자르기·숫자 형식 | Core | spec 14 2절 |
| `Wizard.Sim` | 결정론 전투 코어: 입력, 이동·자세, 과열, 스침, 콤보·결정, 주문 컴파일러와 투사체, 커맨드 판정기와 비기, 적·엘리트·보스, 예고와 R5, 웨이브 디렉터, 야영, 점수, 결투 규칙, 스냅숏, 표시 이벤트, 입력 기록과 재실행 | Core, Content | spec 01~07, 10 |
| `Wizard.Rules` | 강화(확률, 분기, 변이, 새기기, 기댓값), 경제(비용, 상점, 수수료, 감정가), 정산(드랍 추첨, 귀환 가방, 지급 목록), `RunResultFacts`(판의 결과를 읽는 정규 형식), 1차 검사 상한, IL과 리그, 시즌 점수 계산 | Core, Content, Sim(리그 제한의 `WandSnapshot`만) | spec 08, 09, 10(10~11절), 11(6~8절) |
| `Wizard.Protocol` | HTTP 요청·응답 DTO, 웹소켓 봉투와 메시지, 결투 UDP 패킷 코덱, 상태 파일 스키마, 경로 상수, `IJsonCodec`, `IAeadCipher`. (입력 기록 `.wzrc`의 코덱은 `Wizard.Sim`에, 오류 코드는 `Wizard.Core`에 있다) | Core, Content, Sim, Rules | spec 09, 10(14절), 11(13절), 12 |
| `Wizard.Netcode` | `DuelHostCore`(봉인·검증·확정), `DuelClientCore`(예측·되감기·이벤트 조정), `ClockSync`, `PlazaHostCore`, `PlazaClientCore`, 관전 스트림 | Core, Content, Sim, Protocol | spec 10 14절, spec 11 2절 |
| `Wizard.Client` | `IBackend`/`IOnlineBackend`, `OfflineBackend`, `ServerBackend`, `ISaveStore`, HTTP·웹소켓·UDP 전송 구현, `RunSession`, `DuelSession`, 상태 파일 읽기와 서명 검증, 설정 저장 | 위 전부, Newtonsoft.Json, BouncyCastle | spec 08 8절, 12, 13 1절 |
| `Wizard.ViewLogic` | 화면 상태 기계와 뷰 모델, HUD 모델, 알림 라우터, 표시 이벤트 바인딩 표와 `UiEventKeys`, 연출 한도, 에셋 매니페스트 로더와 레지스트리, `FixConvert`. 실수형을 쓸 수 있고 코어 버전 해시에 들어가지 않는다 | 위 전부 | spec 13, 14, T-31 |

서버 프로젝트(`net10.0`):

| 프로젝트 | 프로세스 | 책임 | 의존 |
|---|---|---|---|
| `Wizard.Server.Data` | (라이브러리) | Npgsql 연결, SQL 마이그레이션 실행, `UnitOfWork`, 원장 쓰기, 멱등 `request` 처리, 행 잠금 도우미, 저장소 클래스 | Protocol, Rules |
| `Wizard.Server.App` | (라이브러리) | 유스케이스: 계정·인증, 강화, 상점, 판 출발·제출·1차 검사·정산·회수, 거래·장터·우편, 친구·차단·신고, 매치메이킹·결투 결과 반영·레이팅, 시즌 정산, 알림, 결제 주문, 메일 발송, 야간 대조 | Data, Rules, Sim, Protocol, Netcode |
| `Wizard.Server.Api` | `wizard-api` | ASP.NET Core 호스트: HTTPS 엔드포인트, 웹소켓 허브, 광장 인스턴스(10틱), 백그라운드 작업(검증 결과 반영, 메일 재시도, 정리 작업), 결투 프로세스 제어 | App |
| `Wizard.Server.Duel` | `wizard-duel` | UDP 소켓 루프, 웹소켓 대체 경로, 경기마다 `DuelHostCore` 1개, 결과를 `wizard-api`에 보고. **DB에 접속하지 않는다** | Netcode, Sim, Protocol |
| `Wizard.Server.Verify` | `wizard-verify` ×2 | `verify_queue`에서 꺼내 `run.setup_canonical`과 기록으로 `ReplayRunner`를 돌리고 **재실행 facts와 상태만** 적는다. 주장 값과 비교하지 않는다. 보상 확정·회수는 하지 않는다 | Sim, Protocol, Data(대기열 저장소만) |
| `Wizard.Server.Admin` | `wizard-admin` | 관리 웹(127.0.0.1): 계정 조회, 제재, 설정, 검토 표시, 지표, 감사 로그 | App |
| `Wizard.Server.Payments.Fakes` | (라이브러리) | `FakeReceiptProvider`, `FakeOrderProvider`. `Wizard.Server.Api`는 이 프로젝트를 **참조하지 않는다**. `Wizard.Server.Tests`와 `Wizard.Server.Api.DevHost`만 참조한다. 그래서 출시 publish 결과에 이 DLL이 없다 | App |
| `Wizard.Server.Api.DevHost` | (개발용 실행 파일) | `Api`의 호스트에 가짜 결제 제공자를 등록해 띄운다. 배포하지 않는다 | Api, Payments.Fakes |

Unity 어셈블리(`unity/Assets/Game/`의 asmdef):

| 어셈블리 | 책임 | 의존 |
|---|---|---|
| `Wizard.Unity.Input` | `IInputSource` 구현(Input System 저수준 이벤트의 시각), 키 바인딩 적용. 결투 중에는 입력 폴링을 프레임과 분리한다(CONTRACTS 6.5절) | Core, Sim, Client |
| `Wizard.Unity.Presentation` | 표시 이벤트 소비, 개체 뷰(보간), 회색 도형 렌더러, `IAssetCatalog`·`IFxPlayer`·`ISfxPlayer`의 Unity 쪽 구현과 빈 구현, 카메라·흔들림·히트스톱(표시 전용). `Fix64 → float` 변환은 `Wizard.ViewLogic.FixConvert`를 쓴다 | ViewLogic과 그 아래 |
| `Wizard.Unity.UI` | `ScreenHost`와 코드로 만든 uGUI 위젯, 화면 80여 개와 HUD의 **뷰**. `Wizard.ViewLogic`의 모델을 그리고 조작을 모델에 넘기기만 한다. 규칙과 상태 기계는 없다(T-31, T-32) | Presentation, ViewLogic과 그 아래 |
| `Wizard.Unity.App` | 시작 절차(상태 파일 → 프로필 고르기), 조립 루트(DI), 고정 틱 루프, 머리 없는 골든 실행기 | 위 전부 |
| `Wizard.Unity.Debug` | 디버그 메뉴(웨이브 건너뛰기, 지급, 해시 표시, 넷 지연 흉내) | App |

화면의 규칙과 상태 기계, 뷰 모델, HUD 모델은 Unity 어셈블리가 아니라 `Wizard.ViewLogic`에 있다(T-31). Unity 어셈블리의 테스트는 `unity/Assets/Tests/{EditMode,PlayMode}`의 asmdef 둘이다.

### 3.2 의존 방향

화살표는 "참조한다"다(화살촉이 참조되는 쪽). 순환은 없다. 그림의 위쪽이 낮은 계층이다. 낮은 계층이 높은 계층을 참조하는 것은 금지다.

```
                         Wizard.Core
                        ▲     ▲     ▲
              Wizard.Content  │   Wizard.Text
                   ▲          │        ▲
               Wizard.Sim ────┘        │
                ▲      ▲               │
        Wizard.Rules   │               │
                ▲      │               │
           Wizard.Protocol             │
            ▲         ▲                │
     Wizard.Netcode   │                │
        ▲    ▲        │                │
        │   Wizard.Client ─────────────┤
        │        ▲                     │
        │   Wizard.ViewLogic ◀─ Wizard.Unity.Presentation ◀─ Wizard.Unity.UI ◀─ Wizard.Unity.App ◀─ Wizard.Unity.Debug
        │
   Wizard.Server.Duel        Wizard.Server.Data ◀─ Wizard.Server.App ◀─ Wizard.Server.{Api, Admin}
   Wizard.Server.Verify ──▶ (Sim, Protocol, Data)
```

한 줄로 쓰면 `Core ← Content ← Sim ← Rules ← Protocol ← Netcode ← Client ← ViewLogic ← Wizard.Unity.{Presentation, UI, App}`, `Text ← Client`다. `Wizard.Unity.Input`은 `Core`, `Sim`, `Client`만 참조하고(`ViewLogic`을 참조하지 않는다) `Wizard.Unity.App`이 `Input`을 참조한다.

금지 규칙(빌드에서 검사한다):

- `Wizard.Sim`은 `Rules`, `Protocol`, `Netcode`, `Client`, `Text`를 참조하지 않는다.
- `Wizard.Core`, `Content`, `Sim`에는 `float`, `double`, `decimal`, `System.Random`, `DateTime`, `Guid.NewGuid`, 문자열 비교에 기대는 로직이 없다(11절). 가변 정적 필드, `[ThreadStatic]`, `unsafe`, `BitConverter`·`MemoryMarshal`·`Unsafe`·`Buffer.BlockCopy`·`Marshal`, 상수가 아닌 제수의 원시 `/`·`%`, `checked`도 없다(T-42).
- `Wizard.Sim`과 코어 버전 해시 안의 `Wizard.Content`는 `Wizard.Content.Rules`(규칙 전용 정의)와 `ErrorCode`·`Result`를 참조하지 않는다(T-18).
- `Wizard.Rules`에는 실수형이 없다. `Wizard.Netcode`에는 시계(`DateTime`, `Stopwatch`)와 소켓이 없다.
- `Wizard.Server.Api`는 `Wizard.Server.Payments.Fakes`를 참조하지 않는다.
- 공유 어셈블리 9개의 asmdef는 `noEngineReferences: true`다. `UnityEngine`을 쓰면 Unity에서 컴파일되지 않는다.
- 서버 프로젝트는 `Wizard.Client`와 `Wizard.ViewLogic`을 참조하지 않는다. `Wizard.Client`는 서버 프로젝트를 참조하지 않는다.
- `Wizard.ViewLogic`은 `UnityEngine`과 Unity 어셈블리를 참조하지 않는다. `unity/Assets/Game/UI`의 뷰에는 규칙이 없다(T-31).
- `Wizard.Server.Duel`은 `Wizard.Server.Data`를 참조하지 않는다.
- `Wizard.Sim`과 `Wizard.Rules`의 강화·매치·리그 코드는 유료 재화 값(`Starlight`, `CurrencyBucket.Paid`, `ItemOrigin.Paid`)을 읽지 않는다(spec 12 10.7절).

### 3.3 대상 프레임워크와 언어 제약

| 대상 | 프레임워크 | C# | 이유 |
|---|---|---|---|
| 공유 어셈블리 | `netstandard2.1` 단일 타깃 | 9.0 | Unity 6000.3.20f1의 컴파일러(Roslyn 4.3.1)는 C# 9로 고정되어 있고 API는 .NET Standard 2.1이다. 다중 타깃과 `#if`를 두면 코어 계산이 런타임마다 달라질 수 있다 |
| 서버, 도구, 테스트 | `net10.0` | 기본(14) | .NET 10은 LTS이고 SDK 10.0.201이 설치되어 있다 |
| `tools/Wizard.Analyzers` | `netstandard2.0` | 기본 | 컴파일러 확장은 `netstandard2.0`이어야 한다. `net10.0`이면 RS1041 경고가 오류가 되고 IDE에서 올라오지 않는다. `Microsoft.CodeAnalysis.CSharp`는 Unity의 Roslyn(4.3.1)에 맞춘 판으로 고정한다 |
| `tests/Wizard.TestKit/Shared` | `netstandard2.1` | 9.0 | 골든 읽기를 dotnet 테스트와 Unity EditMode가 같이 쓴다. Unity는 UPM 패키지(package.json + asmdef)로만 프로젝트 밖의 소스를 컴파일한다. JSON은 Newtonsoft |

공유 코드에서 쓰지 않는 문법과 API(Unity가 컴파일하지 못하거나 런타임에 없다):

| 금지 | 대신 |
|---|---|
| `record struct`, 파일 범위 네임스페이스, `global using`, `required`, 원시 문자열, 목록 패턴, 제네릭 수학(`INumber<T>`) | 일반 `struct`/`class`, 블록 네임스페이스 |
| `record`, `init` 접근자 | `readonly struct` 또는 생성자가 있는 `sealed class`. DTO는 `get; set;` 클래스 |
| `Int128`, `UInt128`, `Math.BigMul(long, long, out long)`, `BitOperations` | `Wizard.Core.Wide`의 직접 구현(64×64→128) |
| `System.Text.Json` | `IJsonCodec`(서버는 System.Text.Json 구현, 클라이언트는 Newtonsoft 구현) |
| `System.Security.Cryptography.ChaCha20Poly1305` | `IAeadCipher`(클라이언트는 BouncyCastle) |
| `Array.Sort`, `List<T>.Sort`(불안정 정렬) | `DetSort`(안정 정렬) 또는 완전 순서 비교자 |

Nullable 참조 형식은 켠다. 공유 패키지는 asmdef 옆 `csc.rsp`에 `-nullable:enable`을 두고, csproj에는 `<Nullable>enable</Nullable>`을 둔다.

### 3.4 빌드 구성

- `Directory.Build.props`: `UseArtifactsOutput=true`(출력이 전부 `artifacts/`로 간다), `TreatWarningsAsErrors=true`, `Deterministic=true`, `LangVersion`은 공유 프로젝트만 `9.0`. 분석기를 공유·서버 프로젝트에 거는 줄(`ProjectReference … OutputItemType="Analyzer"`)도 여기에 있다. M0-BT-01이 넣는다.
- `unity/Directory.Build.props`는 빈 `<Project/>`다. Unity가 IDE용으로 만드는 `unity/*.csproj`가 최상위 설정을 물려받으면 같은 이름의 프로젝트 출력이 `artifacts/bin/Wizard.Core/`에서 겹치고 경고가 오류가 된다. `unity/*.csproj`와 `unity/*.sln`은 커밋하지 않는다. 프로젝트 파일을 훑는 도구는 `src/`, `server/`, `tests/`, `tools/`만 본다(`.claude/worktrees/`와 `unity/`를 보지 않는다).
- 실행 파일 프로젝트(`server/`의 Api·Duel·Verify·Admin·Api.DevHost, `tools/`의 콘솔 도구)는 M0-BT-01이 빈 `Program.cs`를 같이 만든다. 진입점이 없으면 CS5001로 빌드가 깨진다. 뒤 작업은 그 파일을 `owned_paths`에 가진 작업만 고치고, 그 밖에는 모듈 등록으로 확장한다(T-33).
- 외부 패키지와 Unity 쪽 DLL·asmdef 참조는 M0-BT-01이 계획된 것 전부를 한꺼번에 넣는다(T-37). 뒤 작업이 csproj를 고칠 일이 없다.
- 공유 프로젝트의 csproj는 패키지 폴더 안에 asmdef와 나란히 있다. Unity는 csproj를 컴파일하지 않고, dotnet은 asmdef를 보지 않는다. 같은 `.cs` 파일을 둘이 따로 컴파일한다.
- 생성 파일(`*.g.cs`)은 커밋한다: `SimVersion.g.cs`(소스 해시), `SinTable.g.cs`, `PowTables.g.cs`. CI가 다시 만들어 차이가 있으면 실패한다. 생성 파일과 골든의 기대 해시는 "다시 만드는 파일"이다. 어느 작업이든 도구의 출력과 바이트가 같으면 바꿀 수 있다(T-41, PIPELINE 4.4절).
- 서버 배포: `dotnet publish -r osx-arm64`. 맥 전용 API는 쓰지 않는다(spec 12 1.2절의 이식성).

## 4. Unity 프로젝트

### 4.1 환경

- 편집기 6000.3.20f1, 2D URP, Input System 패키지, Test Framework.
- 스크립팅 백엔드: 개발은 Mono, 배포는 IL2CPP. 둘 다, 그리고 arm64와 x64 둘 다 골든 테스트를 통과해야 한다(11절, T-30).
- 대상은 데스크톱 스탠드얼론이다. **지금 이 기기에 설치된 것은 macOS의 Mono 변형뿐이다**(`MacStandaloneSupport/Variations`에 `*_mono`만 있고 `modules.json`의 `mac-il2cpp`가 꺼져 있다). IL2CPP 골든과 출시 빌드에는 `mac-il2cpp` 모듈이 있어야 한다. 사용자가 깐다(sudo 없이 된다): `"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install-modules --version 6000.3.20f1 -m mac-il2cpp`. Xcode는 이미 있다.
- Windows: 이 맥의 Unity가 주는 Windows 모듈은 `windows-mono`뿐이다. Windows IL2CPP 플레이어는 Windows 기기에서 Visual Studio C++ 도구로만 만든다(T-39).
- 출시 빌드의 Managed Stripping Level은 Minimal로 고정하고 `Assets/link.xml`로 `Wizard.Protocol`, `Wizard.Client`, BouncyCastle, Newtonsoft를 지킨다(T-44).
- Unity를 부르는 방식은 둘이다. 빌드처럼 동기로 끝나는 진입점은 `-batchmode -nographics -quit -executeMethod …`. 테스트는 `-quit`을 **쓰지 않는다**(`-quit`이 있으면 비동기로 도는 테스트가 시작도 하기 전에 편집기가 끝나고 0을 돌려준다): `-batchmode -nographics -runTests -testPlatform EditMode|PlayMode -testResults <절대 경로>`를 쓰거나, 진입점이 `RunFinished` 콜백에서 `EditorApplication.Exit(실패 수 > 0 ? 1 : 0)`을 부른다. 프레임 시간은 `-nographics` 없이 플레이어를 띄워 잰다. 멈춘 배치 실행은 `Wizard.Ci`의 시간 제한이 끊는다.
- 물리 엔진은 쓰지 않는다. `Physics2D` 자동 시뮬레이션을 끈다. 충돌은 코어의 원 판정이다(spec 01 D5).
- `Time.timeScale`은 건드리지 않는다. 히트스톱과 느린 연출은 표시 계층이 자기 시계로 처리한다(spec 01 D12).

### 4.2 코어를 넣는 방식: 로컬 UPM 패키지로 소스 공유

`src/`의 폴더 9개가 각각 UPM 패키지다(`tests/Wizard.TestKit/Shared`도 같은 방식의 패키지다). 폴더마다 `package.json`, asmdef, csproj가 있다.

```
src/Wizard.Sim/
├ package.json          { "name": "com.wizard.sim", "version": "0.0.0", … }
├ Wizard.Sim.asmdef     noEngineReferences: true, references: [Wizard.Core, Wizard.Content]
├ Wizard.Sim.csproj     netstandard2.1, LangVersion 9.0, ProjectReference(Core, Content)
├ csc.rsp               -nullable:enable
├ Generated/SimVersion.g.cs
└ (소스 폴더들)
```

`unity/Packages/manifest.json`:

```json
"com.wizard.core":     "file:../../src/Wizard.Core",
"com.wizard.content":  "file:../../src/Wizard.Content",
"com.wizard.text":     "file:../../src/Wizard.Text",
"com.wizard.sim":      "file:../../src/Wizard.Sim",
"com.wizard.rules":    "file:../../src/Wizard.Rules",
"com.wizard.protocol": "file:../../src/Wizard.Protocol",
"com.wizard.netcode":  "file:../../src/Wizard.Netcode",
"com.wizard.client":   "file:../../src/Wizard.Client",
"com.wizard.viewlogic": "file:../../src/Wizard.ViewLogic",
"com.wizard.testkit.shared": "file:../../tests/Wizard.TestKit/Shared"
```

- `.meta` 파일은 `src/` 안에 생기고 커밋한다. Unity가 무작위 GUID로 먼저 만들지 못하게, Unity를 띄우는 모든 스크립트는 먼저 `metagen`을 돌린다(GUID = 저장소 기준 상대 경로의 MD5, T-34). 이미 있는 `.meta`는 덮어쓰지 않는다. `metagen --check`는 `src/`와 `unity/Assets/Game/`의 GUID가 계산값과 같은지도 본다. Unity가 실행 중에 스스로 고쳐 쓰는 파일(`Packages/packages-lock.json`, `ProjectSettings/*.asset`의 형식 갱신, `ProjectVersion.txt`)은 실행 뒤 되돌린다(PIPELINE 4.4절).
- `artifacts/` 출력 덕분에 패키지 폴더에 `bin/`, `obj/`가 생기지 않는다. 생기면 Unity가 DLL을 중복으로 읽어 충돌한다.
- 외부 DLL: Newtonsoft.Json은 Unity 패키지(`com.unity.nuget.newtonsoft-json`), BouncyCastle은 `unity/Assets/Plugins/`. `Wizard.Client.asmdef`가 `overrideReferences: true`와 `precompiledReferences`로 가리키고, `com.wizard.client`의 `package.json`이 newtonsoft 패키지에 의존한다. 전부 M0-BT-01이 만든다.
- DLL로 넣는 방식을 버린 이유는 `DECISIONS-TECH.md` T-02에 있다.

### 4.3 폴더와 장면

```
unity/Assets/Game/
├ App/            Core/(Bootstrap, CompositionRoot, FixedTickDriver, SceneFlow), Boot/, Headless/(골든 실행기)
├ Input/          UnityInputSource, KeyBindingStore, InputModeTracker
├ Presentation/
│  ├ Core/        도형 표시의 공통부(좌표 적용, 보간). UnityConvert는 Presentation 바로 아래에 있다
│  ├ Bus/         ViewLogic의 PresentationEventRouter에 Unity 쪽 재생기와 카메라를 꽂는 연결부
│  ├ World/       PlayerView, EnemyView, ProjectileView, TelegraphView, HazardView (지금은 도형)
│  ├ Duel/  Plaza/ 결투의 예측·확정·번복 표시, 광장 장면 표시
│  ├ Assets/      IAssetCatalog의 Unity 구현(카탈로그, 대체 그림). 매니페스트 로더와 레지스트리는 ViewLogic에 있다
│  ├ Fx/  Sfx/    IFxPlayer/ISfxPlayer 구현 자리(지금은 빈 재생기와 디버그 표시)
│  └ Camera/      CameraDirector. 흔들림 누적과 그림 정지 시계의 계산은 ViewLogic의 연출 한도가 한다
├ UI/             Common/(코드로 만드는 위젯, ScreenHost, 입력 라우팅), Screens/<화면>/, Hud/ — 뷰만 있다
└ Debug/
```

장면은 `Boot`, `Menu`, `Battle`(무한 웨이브·결투·연습 공용), `Plaza` 넷이다. 화면은 장면이 아니라 `ScreenStack`(`Wizard.ViewLogic`)으로 전환하고, Unity 쪽 `ScreenHost`가 그 스택을 따라 뷰를 만들고 없앤다(spec 13 1.1절).

- 장면에는 시작 개체 하나만 둔다. 나머지 개체와 화면 뷰는 **코드로** 만든다. 에셋을 들여오기 전까지 프리팹과 장면 편집을 쓰지 않는다(T-32). 장면과 프리팹은 병합 충돌을 풀기 어려운 파일이기 때문이다.
- 뷰는 uGUI 위젯이다. 모델(`Wizard.ViewLogic`)을 읽어 그리고 조작을 모델에 넘기기만 한다. 그림은 위젯의 겉(스킨)만 바꿔 꽂는다.
- 새 파일의 `.meta`는 Unity가 아니라 `metagen`이 만든다(T-34, 4.2절).

## 5. 런타임 구성

```
[Unity 클라이언트]
  Wizard.Unity.App ── FixedTickDriver(60틱)
   ├ IInputSource ─▶ InputQuantizer ─▶ InputEvent
   ├ RunSession / DuelSession / PracticeSession ─▶ SimWorld (Wizard.Sim)
   ├ PresentationEventRouter ◀─ 표시 이벤트, ISimView
   ├ IBackend ── OfflineBackend ── ISaveStore(기기 파일) + Wizard.Rules
   │           └ ServerBackend ── HTTPS(JSON) + 웹소켓
   ├ IDuelTransport ── Udp / WebSocket / Loopback(+ 기기 안의 DuelHostCore)
   └ IPurchaseFlow ── (출시 빌드에는 등록 없음)

[상태 파일]  맥미니 밖 정적 호스팅. Ed25519 서명 JSON

[맥미니 한 대]
  Caddy :443 ─┬─▶ wizard-api (ASP.NET Core)  HTTPS + 웹소켓(/v1/ws)
              └─▶ wizard-duel 의 웹소켓 대체 경로(/v1/duel/ws)
  wizard-duel   UDP 1포트. 경기마다 DuelHostCore + SimWorld
        ▲▼ 유닉스 도메인 소켓(HTTP): 경기 생성, 결과 보고, 생존 신호
  wizard-api ◀─▶ PostgreSQL ◀─▶ wizard-verify ×N (verify_queue, 결과만 기록)
  wizard-admin  127.0.0.1 전용
  launchd: 프로세스 유지, 백업, DDNS
  (나가는 쪽) IMailSender ─▶ 외부 메일 사업자 HTTPS API
```

프로세스 사이의 연결:

| 연결 | 방식 | 내용 |
|---|---|---|
| 클라이언트 ↔ `wizard-api` | HTTPS(JSON), 웹소켓 | 요청·응답, 광장·채팅·알림·매치·거래 창 |
| 클라이언트 ↔ `wizard-duel` | UDP(ChaCha20-Poly1305). 막히면 웹소켓 | 결투 입력과 확정 |
| `wizard-api` ↔ `wizard-duel` | 유닉스 도메인 소켓 위의 HTTP(JSON) | 경기 생성(양쪽 설정과 키), 결과 보고(멱등), 생존 신호 |
| `wizard-api` ↔ `wizard-verify` | PostgreSQL(`verify_queue`) | 워커는 결과를 적고, `wizard-api`의 백그라운드 작업이 확정·회수 트랜잭션을 돌린다 |
| `wizard-api` → 메일 사업자 | HTTPS | 확인·복구·보안 알림 메일 |

`wizard-verify`는 **코어 버전마다 따로 빌드한 바이너리**다. 최근 두 버전을 같이 띄우고, 각 워커는 자기 코어 버전의 판만 꺼낸다. 정산 로직은 워커에 없으므로 서버 규칙이 바뀌어도 옛 워커를 다시 빌드할 필요가 없다.

## 6. 데이터 흐름

### 6.1 입력 → 시뮬레이션 → 표시 이벤트 → 표시 계층

```
매 렌더 프레임 (FixedTickDriver):
  now = 단조 시계
  raw  = IInputSource.Drain()                   // 물리 키 이벤트 + 하드웨어 시각
  keys = InputQuantizer.Quantize(raw, clock, D) // 틱 = 하드웨어 시각의 틱(+D), 오프셋 0~255. 순번 없음. 틱 진행과 무관
  session.OnInput(keys, IInputSource.CurrentKeys)   // 결투: 순번을 매기고 **곧바로 송신**. 무한 웨이브: 틱별 대기 줄에 넣는다
  while clock.NextTickDue(now):
      t = clock.Tick
      session.Step(t)                           // 아래 세 가지 중 하나
      clock.Advance()
  alpha = simClock.Alpha(now)                   // 표시 보간 비율(실수는 여기서부터)
  PresentationEventRouter.Drain(session.Events) // 이번 프레임에 나온 표시 이벤트
  Views.Render(session.View, alpha)             // 위치 보간, HUD 값 읽기

session.OnInput:
  RunSession      : queue.Enqueue(keys)
  DuelSession     : client.OnLocalInput(keys, physicalKeys); transport.Send(client.PollOutgoing())   // 프레임을 기다리지 않는다

session.Step(t):
  RunSession      : keys = queue.TakeForTick(t); recorder.Append(t, cmds, keys → 순번을 매긴 열); world.Step(…)   // 무한 웨이브
  DuelSession     : 수신 패킷을 전부 client.OnPacket; client.Advance(입력 시계의 틱)   // 예측·되감기는 DuelClientCore 안. 멈춤이 풀리면 여러 틱을 따라잡는다
  PracticeSession : RunSession과 같되 기록·제출 없음. 봇 결투는 Loopback + 기기 안의 DuelHostCore
```

- 키는 프레임 단위로 읽지 않는다. Input System의 이벤트 시각으로 틱과 틱 안 오프셋을 계산한다(spec 01 4.2절).
- `SimWorld.Step`은 입력을 받아 상태를 한 틱 진행시키고 표시 이벤트를 `ISimEventSink`에 쓴다. 반환값은 없다.
- 표시 계층은 `ISimView`(읽기 전용 상태)와 표시 이벤트만 본다. 코어의 내부 타입을 바꾸지 않는다.
- 히트스톱은 표시 이벤트 `HitstopRequest`를 받은 표시 계층이 **그림만** 멈춘다. 틱은 계속 간다.
- **틱을 멈추는 것은 일시정지뿐이다.** 전리품 창과 야영 화면이 떠 있어도 틱은 진행한다(T-13 개정). 그동안의 조작은 `RunCommand`로 기록되고 발생한 틱의 0단계에서 적용된다. 창이 떠 있는 동안 키는 세계에 넣지 않는다(CONTRACTS 3.1절).
- 결투에서 키 이벤트는 틱 진행에 묶어 보내지 않는다. 프레임마다 `Drain` 직후에 양자화하고 순번을 매겨 바로 보낸다. 결투 중에는 입력 폴링도 프레임과 분리한다. 마감 여유는 `(D + 1 − f)틱 − 편도 − 전송 대기`다(`f` = 누른 순간의 틱 안 위치. CONTRACTS 6.5절).
- `wizard-duel`의 루프는 "수신 버퍼를 다 비운 뒤 `Advance`"이고, `OnPacket`에 주는 시각은 큐에서 꺼낸 시각이 아니라 커널의 수신 타임스탬프다. 서버의 GC와 루프 지연이 늦음 판정에 섞이지 않는다.

### 6.2 판 → 제출 → 검증 → 정산 (서버 프로필)

```
① 출발      클라 ─ POST /v1/runs ─▶ App.StartRun: 지팡이 잠금(원정), 씨앗·run_id·run_token 발급,
                                     서버가 아는 지팡이·조립 스냅숏, 해금, phantom_first, season_tag_on을 묶어
                                     EndlessSetup의 정규 바이트를 만들고 run.setup_canonical에 저장, 같은 바이트를 내려 준다
② 진행      클라: SimWorld.CreateEndless(setup) → 틱마다 Step. InputRecorder가 .wzrc에 누적
             5웨이브마다 POST /v1/runs/{id}/checkpoints (틱, 해시, 점수, 지갑, 그때까지의 기록)
③ 종료      클라: RunOutcome.WriteFacts → RunResultFacts 바이트(점수·골드 내역, 드랍 기회, 귀환 가방 후보, 허상, 해금, 중간 지점)
             POST /v1/runs/{id}/finish (facts + 가방 선택 + 입력 기록 파일)
④⑤ 한 트랜잭션(멱등 키 run_id): run 행 FOR UPDATE → 토큰 소모 →
   1차 검사  기록 구조(InputRecordCodec.TryRead), 파일 머리말 = run.setup_canonical(바이트 대조), 디버그 op 없음,
             코어 버전, 기록 해시, 가속 여부, 웨이브별 점수·골드 상한(IRunLimits.ScoreCap(w, WhimsUpper(w))), 가방 선택 ⊆ 후보
   정산      claimed_facts와 claimed_digest 저장 → ISettlementRules.Settle(facts, ctx, 서버 난수) → 지급 목록
             원장에 "검증 대기"로 기록, 해금 반영, 지팡이 원정 잠금 해제, verify_queue 등록 → COMMIT
⑥ 2차 검증  wizard-verify(그 판의 코어 버전): run.setup_canonical로 머리말을 만들고 ReplayRunner.Run(팩, 머리말, 기록 본문)
             → 상태와 재실행 facts(바이트, 다이제스트)만 적는다. 주장 값을 읽지 않는다
⑦ 확정/회수 wizard-api 백그라운드: 재실행 facts = claimed_facts(바이트)이고 run_checkpoint의 모든 (wave, tick, hash)가 같으면
             match → 대기 해제·순위 확정 / 다르거나 기록이 깨졌으면 mismatch → 회수·갚을 것·기록 삭제·계정 표시 /
             코어 예외·시간 초과·팩 없음·시도 초과는 unverifiable → 보상은 대기 그대로, 운영자 검토
```

오프라인 프로필은 ②③⑤만 한다. `OfflineBackend`가 같은 facts 바이트를 거쳐 같은 `ISettlementRules.Settle`을 기기 난수로 부르고 저장 파일에 쓴다. ①의 씨앗은 기기가 만든다. ④⑥⑦은 없다.

- `wizard-api`는 이 경로에서 코어의 코드(`WaveMath`, `SimWorld`)를 부르지 않는다. 코어 N으로 출발한 판을 서버가 N+1로 바뀐 뒤에 받아도 결과가 같다(T-18).
- ④와 ⑤가 한 트랜잭션인 이유: 토큰 소모와 정산 사이에 프로세스가 죽으면 재제출이 "토큰 이미 사용"으로 막혀 판이 정산되지 못한다.
- 출발 뒤에 계정의 해금이나 지팡이가 바뀌어도 재실행의 시작 조건은 `run.setup_canonical`이다. 파일의 머리말은 대조만 하고 재실행의 입력으로 쓰지 않는다.

접속이 끊긴 채 끝난 서버 프로필의 판은 기기에 요약과 기록을 보관했다가 다시 접속하면 ③부터 이어 간다.

### 6.3 강화 요청 → 서버 트랜잭션 → 원장

```
화면(Enhance) ─ IBackend.Enhance.EnhanceAsync(requestId, wandId, sacrifices, useCharm)
 ├ ServerBackend ─ POST /v1/wands/{id}/enhance (Idempotency-Key: requestId)
 │    App.Enhance:
 │      BEGIN
 │        request(account_id, request_id, 본문 지문) INSERT … ON CONFLICT DO NOTHING
 │          → 0행이면 그 행을 FOR UPDATE로 기다렸다가: 지문이 다르면 IdempotencyConflict, 같으면 저장된 결과 반환
 │        account, wand, item_stack, currency_balance 행을 FOR UPDATE
 │        Wizard.Rules.IEnhanceRules.Validate → 실패면 거절 결과를 저장하고 COMMIT
 │        roll = 서버 CSPRNG [0, 10000)
 │        res  = IEnhanceRules.Resolve(wand, req, roll, mutationPick)
 │        currency_ledger(−골드), item_ledger(−강화석, −제물, −부적)
 │        wand 갱신(version+1), enhance_log 1줄, +10 이상 성공이면 알림 큐
 │        request 에 결과 저장
 │      COMMIT → EnhanceResultDto
 └ OfflineBackend: 같은 IEnhanceRules, roll = 기기 난수, ISaveStore에 원자적으로 저장(3세대)
화면: 결과 꾸러미를 받은 뒤에 연출을 재생한다. 연출은 결과를 바꾸지 않는다.
```

두 프로필의 결과 꾸러미는 모양이 같다. 화면 코드는 한 줄도 갈라지지 않는다.

`request` 행을 먼저 넣는 이유(spec 12 1.3절): 조회를 먼저 하면 같은 키의 동시 요청 둘이 모두 통과해 뒤의 것이 강화를 다시 굴린 뒤 기본 키 충돌로 되돌려지고, 저장된 결과가 아닌 오류를 받는다.

### 6.4 결투

```
(자동 매치) 대기열 진입 전: wizard-duel과 UDP 탐침 16회 → wizard-duel이 잰 왕복을 wizard-api에 보고 → 서버가 자격 판정
매치 성립 → 수락(duel.match_accept) → (도전·방) 캐릭터·지팡이 고르기(duel.prep_select) → 지팡이 잠금, 리그 제한 적용
wizard-api ─ UDS ─▶ wizard-duel: 경기 생성(양쪽 DuelPlayerSetup, 씨앗, 규칙, 키. D는 여기에 없다)
wizard-api ─ 웹소켓 duel.setup ─▶ 양쪽 클라이언트(같은 설정, 각자 키와 epoch, UDP 주소)
시계 맞추기 16회(서버도 자기 표본으로 왕복을 잰다) → 서버가 D를 정해 HelloAck로 내려 준다
  → 서버가 잰 왕복이 한도를 넘으면 시작 전 취소 → 로딩 → 카운트다운

클라 매 프레임(키가 생기면 즉시), 매 틱:
  내 키 → 유효 틱 = 누른 순간의 입력 시계 틱 + D, 순번 → ClientInput 패킷(최근 8틱분 겹침, base_seq)
  DuelClientCore: 확정 세계(확정 입력만) + 예측 세계(상대 입력은 "직전 키 상태 유지")
서버(DuelHostCore) 유효 틱 T:
  입력 검증(CONTRACTS 10.6절: 늦은 이벤트만 버림, 건너뛴 순번은 유실로 소비, 패킷 거부는 패킷 안의 역행뿐)
  양쪽 봉인(패킷이 T를 봉인했거나 서버 시각이 T+1틱) → 정렬 → SimWorld.Step → 확정 입력 방송, 30틱마다 해시
클라: 확정이 예측과 다르면 확정 세계를 예측 세계에 복사해 다시 돌린다(한도 8틱).
      한도에 걸리면 예측을 멈추고 확정을 기다린다(Stalled). 입력 시계는 멈추지 않는다.
      해시가 다르거나 확정 공백이 길면 웹소켓으로 상태 전체를 받는다
      내 입력이 버려졌으면 확정 키 상태와 물리 키 상태의 차이를 보정 이벤트로 낸다
끊김: 서버가 그 사람의 키를 뗀 것으로 만들고(순번 소비, 확정 열에 실린다) 유예를 센다
      돌아오면 duel.rejoin → 새 키(epoch + 1)·이어 갈 순번·키 상태 + 상태 전체
경기 끝: wizard-duel ─ UDS ─▶ wizard-api: 결과 보고(받아들여질 때까지 다시 보낸다. match_id당 처음 확정된 결과가 이긴다)
         → 레이팅·보상·전적·다시 보기·지팡이 잠금 해제(트랜잭션)
```

한 기기 2인과 봇 연습은 `LoopbackTransport`로 기기 안의 `DuelHostCore`에 붙는다. 서버 경기와 코드 경로가 같다.

### 6.5 광장

```
클라: 10틱(100ms)마다 plaza.input(순번, 이동 방향, 최근 3개 겹침). 자기 이동은 PlazaClientCore로 예측
서버(PlazaHostCore, 인스턴스당 1개): 속도·벽 검사 → 위치 확정 → plaza.snapshot(이진, 사람당 약 10바이트)
클라: ack_seq 이후의 내 입력을 다시 적용해 맞춘다. 남의 캐릭터는 스냅숏 두 개 사이를 보간
```

광장은 `Wizard.Sim.World`의 방향 벡터(1/√2 = 3,037,000,500)와 벽 판정을 쓰되, 스텝은 spec 11 2절의 0.1초 고정 스텝 `MoveAndSlide`(x축 먼저, 그다음 y축)다. 전투의 60틱 이동 적분과 식이 다르다. 발사와 비기는 `SimConfig`로 끈다. 광장은 해시로 검증하지 않고 서버 위치로 맞춘다.

## 7. 네트워크 권한과 프로토콜

### 7.1 권한표

| 대상 | 정본(판정) | 클라이언트 예측 | 검증 | 근거 |
|---|---|---|---|---|
| 재화, 인벤토리, 지팡이 상태 | 서버(트랜잭션 + 원장) | 없음. 응답을 받아 표시 | 야간 대조(원장 합 = 잔액) | spec 12 7절 |
| 강화 난수와 결과 | 서버(CSPRNG) | 없음. 연출만 | `enhance_log`(확률 내역, 뽑힌 값) | spec 08 4절 |
| 무한 웨이브 전투 | 클라이언트가 실행 | — | 서버가 **모든 판**을 재실행. 1차 검사는 즉시 | spec 09 7~9절 |
| 무한 웨이브 씨앗, 시작 조건(지팡이, 해금, 시즌 꼬리표) | 서버가 발급하고 `run.setup_canonical`에 저장 | — | 기록 머리말을 바이트로 대조. 재실행은 DB의 값으로만 | spec 09 5절 |
| 무한 웨이브 드랍(재료·부적·한정) | 서버가 정산 때 추첨 | — | 클라이언트는 기회 목록만 올린다 | spec 07 14절 |
| 점수, 골드, 순위 | 서버(재실행 결과) | 클라이언트 계산을 임시 표시 | 재실행 불일치면 삭제·회수 | spec 09 |
| 결투 전투와 승패 | 서버(`wizard-duel`)가 시뮬레이션 | 같은 코어로 예측, 되감기 8틱 | 30틱마다 해시 비교, 어긋나면 서버 상태로 덮음 | spec 10 14절 |
| 결투 입력의 시각과 순서 | 클라이언트가 적고 서버가 확정 | — | CONTRACTS 10.6절의 순서: 패킷 안의 역행, 순번 공백(유실), 늦은 시각, 이른 시각, 키 상태, 틱당 8개 | spec 10 14.8절 |
| 결투의 왕복 지연, 입력 지연 D, 대기열 자격 | 서버가 직접 잰다(에코와 붙들어 둔 시간, 대기열 진입 전 UDP 탐침) | — | 클라이언트가 알려 준 왕복을 믿지 않는다 | CONTRACTS 10.5절 |
| 무한 웨이브의 결과(정산 입력) | 클라이언트가 facts를 주장, 서버가 재실행 facts와 **바이트로** 비교 | 임시 지급(검증 대기) | 정산에 쓰이는 모든 필드가 facts 안에 있다. 중간 지점 해시도 대조 | CONTRACTS 3.7·8.3절 |
| 결투 장비·리그 제한 적용 | 서버가 경기 시작 때 내려 준다 | — | — | spec 10 11절 |
| 매치, 결투 점수, 리그, IL | 서버 | — | — | spec 10 10~13절 |
| 광장 위치 | 서버(속도·벽 검사) | 자기 이동만 예측 | 벗어나면 서버 위치로 되돌림 | spec 11 2절 |
| 거래, 장터, 우편, 선물 | 서버(트랜잭션) | 없음 | 멱등 키, 행 잠금 | spec 11 6~12절 |
| 채팅 필터와 제한 | 서버 | 없음 | — | spec 11 4절 |
| 시각(주간 한도, 시즌, 잠금) | 서버 시각 | — | 클라이언트 시계는 믿지 않는다 | spec 12 7절 |
| 코어·데이터 버전 | 서버가 요구 값을 알린다 | — | 다르면 순위 제출과 결투 입장을 막는다 | spec 01 3.5절 |
| 오프라인 프로필의 모든 것 | 기기 | — | 없음. 결과는 서버로 가지 않는다 | DECISIONS 2.6 |

### 7.2 프로토콜

| 채널 | 쓰임 | 직렬화 | 인증 |
|---|---|---|---|
| HTTPS (TLS 1.3) | 요청·응답 전부 | JSON(UTF-8, snake_case). 입력 기록은 이진 본문 | `Authorization: Bearer <접근 토큰>`. 변경 요청은 `Idempotency-Key: <uuid>` |
| 웹소켓 `/v1/ws` | 광장, 채팅, 알림, 친구 상태, 거래 창, 매치·방·도전, 결투 수명주기, 상태 전체 전송, 관전 | 글자 프레임 = JSON 봉투 `{t, s, d}`. 이진 프레임 = 1바이트 종류 + 본문(`plaza.snapshot`, `plaza.input`, `duel.full_state`, `duel.spectate`) | 연결 때 접근 토큰 |
| UDP(결투) | 입력, 확정, 해시, 시계 | 직접 만든 LE 이진(CONTRACTS 10절) | 경기마다 받은 키로 ChaCha20-Poly1305. 패킷 번호로 재전송 차단 |
| 웹소켓 `/v1/duel/ws` | UDP가 막혔을 때의 결투 | UDP와 **같은 바이트**를 이진 프레임으로 | 같음 |
| UDS HTTP | `wizard-api` ↔ `wizard-duel` | JSON | 소켓 파일 권한 + 공유 비밀 |

- JSON 규칙: 열거형은 snake_case 문자열, 시각은 ISO-8601 UTC 문자열, 64비트 정수는 숫자(부호 없는 64비트인 씨앗과 상태 해시만 16자리 hex 문자열이다. T-20), 바이트열은 base64url, 콘텐츠는 문자열 id(`block.fireball`), 캐릭터는 1~5 정수.
- 서버는 System.Text.Json, 클라이언트는 Newtonsoft.Json을 쓴다. `tests/fixtures/protocol/`의 JSON을 양쪽 코덱이 모두 왕복시키는 테스트로 어긋남을 막는다.
- 버전: `protocol_version`(정수)을 상태 파일과 `/v1/features`가 알린다. 다르면 새 빌드를 안내한다.

## 8. 콘텐츠 데이터 파이프라인

### 8.1 형식

| 선택지 | 판단 |
|---|---|
| ScriptableObject | 서버와 도구가 읽을 수 없다. 버림 |
| CSV | 공격 목록, 패턴, 변이처럼 겹친 구조를 담기 어렵다. 버림 |
| **JSON(저작) → 이진 팩(실행)** | 사람이 고치기 쉽고, 코어는 실수 파싱 없이 정수만 읽는다. **채택** |

### 8.2 흐름

```
data/**/*.json  (소수는 십진 그대로 적는다: "move_speed": 4.7, "crit_start": 80)
   │  tools/Wizard.DataBuild
   │   1. JSON Schema 검사
   │   2. decimal로 읽어 변환: 칸 → Q32.32 Raw(가장 가까운 정수, 같으면 짝수), 초 → 틱, 열 → HU(×120),
   │      칸/초 → 틱당 Raw, 도 → 센티도, % → 천분율·만분율
   │   3. 교차 검사: id가 열거형과 일치, 참조 무결성, 약점·저항 분배(속성별 차이 ≤ 1),
   │      변덕에 금지 필드 없음, 적 예고의 회피 검산 R1~R5(EvasionValidator), 이름 표 열쇠 누락
   │   4. 정규 순서로 이진 직렬화(LE, id 오름차순) → SHA-256
   ▼
content/base.wzpack  (머리말: magic, schema, data_version, core_compat, 절 목차, 해시)
   ├ unity/Assets/StreamingAssets/content/   클라이언트 기본 팩
   └ 서버 배포물 content/packs/<data_version>.wzpack
```

- 코어는 `ContentPack.Load(bytes)`로 팩을 읽어 `ContentDb`를 만든다. 정수 읽기뿐이라 어느 런타임에서나 결과가 같다.
- `ContentDb`는 불변이다. `SimWorld`와 규칙 함수는 생성 때 받은 `ContentDb`만 본다. 전역 싱글턴이 아니다(서버는 여러 판을 동시에 돌린다).
- 판은 `core_version`(코드)과 `data_version`(팩)을 기록한다. 검증 워커는 그 판의 팩으로 재실행한다.
- 서버가 수치를 조정하면 새 팩을 낸다. 클라이언트는 `GET /v1/content/manifest`로 새 판 번호를 보고 내려받아 `persistentDataPath`에 둔다. 실패하면 빌드에 든 기본 팩을 쓴다. 오프라인 프로필은 언제나 기본 팩이다.
- 서버 운영 값(접속 상한, 기능 스위치, 메일 한도)은 팩이 아니라 DB의 `config` 표다. 어느 팩이 켜져 있는지도 `config`가 가리킨다.

### 8.3 이름 표

- 유일한 위치: `data/names/names.ko.json` (`NameKey → 문자열 또는 틀`). 파일의 뼈대(빈 표와 `"~end"`)와 `databuild names --check`는 M1-CONT-02가 만든다. 데이터 작업은 자기 id의 열쇠를 같이 더한다. 그래서 M1부터 G5의 "모든 id에 이름 열쇠"가 통과한다.
- 형식(T-35): 열쇠 오름차순, 한 줄에 열쇠 하나, 마지막 줄은 표지 열쇠 `"~end": ""`다. git 병합은 union(`.gitattributes`)이고 병합 뒤 `databuild names --fix`가 정렬과 중복을 본다. 여러 작업이 줄을 더해도 덧붙이기끼리는 충돌하지 않는다. 작업은 자기 영역의 열쇠만 더한다.
- 실행 때 읽는 것은 JSON이 아니라 DataBuild가 만든 이진 절 `names.ko.wznm`이다. `Wizard.Text`는 `ByteReader`로 읽는다(공유 코드에 JSON 파서를 들이지 않는다).
- 화면에 보이지 않는 한글 데이터(채팅 금칙어 등)는 이름 표가 아니라 `data/rules/chat_filter.json` 같은 데이터 파일에 둔다. 코드에 한글 리터럴로 두지 않는다.
- 코어, 규칙, 프로토콜, DB는 이 파일을 읽지 않는다. 읽는 곳은 `Wizard.Text`를 쓰는 표시 계층과 관리 화면뿐이다.
- 서버 전체 알림처럼 이름이 들어가는 문구는 서버가 id와 인자만 보내고 클라이언트가 틀로 조립한다.
- `Wizard.DataBuild`가 "데이터의 모든 id에 이름 열쇠가 있는가"를 검사한다. 없는 열쇠는 화면에 열쇠 문자열 그대로 찍힌다.

## 9. 표시 이벤트 버스

코어는 그림과 소리를 모르고 이벤트만 낸다.

```
SimWorld.Step ──▶ ISimEventSink.Emit(in SimEvent)     고정 크기 구조체, 할당 없음
                    │
                    ├ (무한 웨이브) 그대로 Confirmed 단계로 전달
                    └ (결투) DuelClientCore의 EventLedger:
                         예측 틱의 이벤트 = Predicted
                         되감기로 사라진 이벤트 = Retracted (문양이 0.1초 흐려짐)
                         틱이 확정되면 = Confirmed (소리와 피격 연출은 여기서만)
                    ▼
PresentationEventRouter ─ EventBindingTable(data/presentation/event_bindings.json)
     ├ IFxPlayer.Play(fxId, 위치, 인자)       지금은 Null 구현 + 디버그 표시
     ├ ISfxPlayer.Play(sfxId, 인자)           지금은 Null 구현
     ├ CameraDirector(흔들림 누적기, 히트스톱 시계)
     └ HUD 뷰 모델 갱신
```

- `PresentationEventRouter`, 바인딩 표, `UiEventKeys`, 연출 한도, 그리고 `IAssetCatalog`·`IFxPlayer`·`ISfxPlayer`의 타입은 `Wizard.ViewLogic`에 있다(T-31, CONTRACTS 13절). Unity 쪽(`Presentation/Bus`, `Fx`, `Sfx`, `Camera`)은 라우터에 실제 재생기와 카메라를 꽂는다. 그래서 바인딩과 한도의 규칙은 `dotnet test`로 검증한다.
- 코어 밖에서 생기는 표시 사건(화면 조작, 연결, 대기열, 서버 전체 알림, 거래, 시즌)은 `UiEventKeys`의 문자열 열쇠로 `PresentationEventRouter.RaiseUi`에 넣는다. 같은 바인딩 표에서 그림과 소리를 찾는다. 두 카탈로그의 모든 열쇠가 바인딩 줄이나 "의도적 없음" 목록에 있는지 테스트가 본다(CONTRACTS 13절).
- 이벤트 종류는 `SimEventType` 열거형 하나로 모은다. 각 명세의 "시각/소리 연결점" 절에 있는 이름을 모듈별 번호 구간에 넣는다(CONTRACTS 3.3절).
- 이벤트에서 에셋으로 가는 대응은 코드가 아니라 데이터(`event_bindings.json`)다. 에셋이 생기면 이 표와 카탈로그만 채운다.
- 표시용 난수(입자, 흔들림)는 코어 난수기를 쓰지 않는다. 쓰면 재실행 결과가 달라진다.
- 매 틱 읽는 연속 값(열 링, 콤보 막대, 체력)은 이벤트가 아니라 `ISimView`에서 읽는다.
- 입자 수 예산과 넘쳤을 때의 처리는 `effects.md`가 유일한 기준이고(DECISIONS 2.8), `Wizard.ViewLogic`의 `ParticleBudget`이 계산하고 재생기가 그 결과를 따른다. 코어는 관여하지 않는다.

## 10. 서브시스템 모듈

### 10.1 `Wizard.Sim` 내부

| 모듈(네임스페이스) | 내용 | 의존하는 모듈 | 명세 |
|---|---|---|---|
| `Sim.Input` | `LogicalKey`, `InputEvent`, `InputOrder`, `KeyState`, `RunCommand`, `InputRecorder` | Core | 01 4절 |
| `Sim.World` | `ArenaDef`, 벽·기둥, 이동 적분, `DayCycle` | Core, Content | 01 5절 |
| `Sim.Player` | `PlayerState`, 시야·자세 상태 기계, 체력·피격·무적 | Input, World | 01 5~6절 |
| `Sim.Heat` | 열, 임계, 잠금, 냉각, 열 예외(밑줄, 불가마, 변이) | Player | 01 7절 |
| `Sim.Graze` | 스침 판정과 보상 | Player, Heat | 01 8절 |
| `Sim.Combo` | 콤보, 결정, 빚, 별똥, 웨이브 종료 환전 | Player | 01 9~10절 |
| `Sim.Spell` | `ISpellCompiler`, `CompiledSpell`, `ISpellExecutor`, 투사체, 피해 공식, 감속·잠식 | Player, Heat, Combo | 02 |
| `Sim.Arts` | `CommandTrie`, `ICommandParser`, 공용 10종과 고유 5종의 효과, 해금 차례 | Spell, Combo | 03, 04 5절 |
| `Sim.Characters` | 패시브 5종, 지팡이 특성, 변이 6종의 전투 효과, 강화 배율 | Heat, Spell, Arts | 04 |
| `Sim.Enemies` | `EnemyState`, `IEnemyBrain` 12종, 엘리트, 꼬리표 반응, 예고 구역, 대기 줄, R5 | World, Player | 05 |
| `Sim.Bosses` | `IBossBrain` 6종, 격노, 첫 만남·복수 | Enemies, Spell(허상의 복사) | 06 |
| `Sim.Waves` | `IWaveDirector`, 틀·변덕, 스폰, 야영, 전리품 카드, 점수, 귀환 가방 후보 | Enemies, Bosses, Combo | 07 |
| `Sim.Duel` | 결투 라운드 상태 기계, 가시밭 고리, 벌칙, 조준 보정, 야생 난입 | Player, Spell, Arts | 10 2~8절 |
| `Sim.Events` | `SimEvent`, `SimEventType`, `ISimEventSink`, `ISimView` | (모두가 쓴다) | 각 명세의 연결점 절 |
| `Sim.Runtime` | `SimWorld`, `SimConfig`, 틱 파이프라인, 스냅숏, 상태 직렬화와 해시, `ReplayRunner` | 전부 | 01 3절 |

모듈 사이의 호출은 `SimWorld`가 틱 파이프라인의 정해진 순서로 한다. 모듈이 서로를 직접 구독하지 않는다(순서가 숨지 않게).

### 10.2 `Wizard.Rules` 내부

| 모듈 | 내용 | 명세 |
|---|---|---|
| `Rules.Enhance` | `IEnhanceRules`: 검증, 확률 내역, 판정 구간, 결과 분기, 변이 제시, 새기기, 마르코프 기댓값 | 08 4~5절 |
| `Rules.Economy` | `IEconomyRules`: 비용, 상점 값과 주간 한도, 수수료, 보증금, 감정가, 떠나보내기 환급 | 08 6~7절, 11 6절 |
| `Rules.Settlement` | `ISettlementRules`: 드랍 추첨, 귀환 가방, 지급 목록, 첫 만남·복수 보상 | 07 13절, 09 8절 |
| `Rules.RunLimits` | `IRunLimits`: 웨이브별 점수·골드 상한, 건너뛴 웨이브 점수, 순위 반영 조건 | 09 7·13절 |
| `Rules.League` | `ILeagueRules`: 장착·보유 IL, 리그 판정, 제한에 맞춰 깎아 적용 | 10 10~11절 |
| `Rules.Season` | 후계 점수(정수 나노점), 보상 구간 | 09 16절 |
| `Rules.Profile` | 새 프로필 시작 상태, 지팡이 불변식, 든 지팡이·마지막 한 자루 | 08 2·8절 |

### 10.3 서버 유스케이스(`Wizard.Server.App`)

| 묶음 | 유스케이스 | 쓰는 표(주요) |
|---|---|---|
| Accounts | 가입, 로그인, 토큰 회전, 이메일 확인·변경, 복구, 삭제, 제재 | `account`, `session`, `device`, `account_token`, `mail_outbox`, `sanction` |
| Inventory | 프로필 조회, 조립 저장, 든 지팡이, 떠나보내기, 옷장, 칭호 | `wand`, `item_stack`, `item_instance`, `character_state` |
| Enhance | 강화, 변이 결정·제거, 슬롯 새기기, 흔적 공개 | `wand`, `enhance_log`, 원장 |
| Shop / Store | 골드 상점, 결제 주문·확인·환불 | `shop_purchase`, `product`, `purchase_order`, `entitlement` |
| Runs | 출발, 중간 지점, 종료 제출, 1차 검사, 정산, 원정 정리, 검증 결과 반영 | `run`, `run_checkpoint`, `run_grant`, `verify_queue`, `account_debt` |
| Boards / Season | 순위표, 주간 도전, 시즌 정산, 후계자 | `leaderboard_entry`, `season`, `season_result`, `weekly_challenge` |
| Plaza / Social | 인스턴스 배정, 채팅, 친구, 차단, 신고, 살펴보기, 서버 전체 알림 | `friend`, `block`, `chat_log`, `report` |
| Trade | 직접 거래, 장터, 우편, 선물 | `trade`, `market_listing`, `market_sale`, `mail` |
| Duel | 도전, 방, 대기열, 매치 생성, 결과 반영, 레이팅, 하루 보상 | `duel_match`, `duel_rating`, `il_history`, `duel_daily` |
| Ops | 야간 대조, 정리 작업, 지표, 감사 로그, 정지 시간 기록 | `audit_log`, `server_downtime`, `config` |

### 10.4 클라이언트(`Wizard.Client`)

| 모듈 | 내용 |
|---|---|
| `Client.Backend` | `IBackend`, `IOnlineBackend`, 서비스 인터페이스, `OfflineBackend`, `ServerBackend` |
| `Client.Save` | `ISaveStore`(원자적 쓰기, 3세대, 체크섬), 오프라인 프로필 파일, 미제출 판 보관함 |
| `Client.Net` | `IHttpTransport`, `IRealtimeChannel`(웹소켓), `NewtonsoftJsonCodec`, 재접속, 토큰 갱신 |
| `Client.Duel` | `IDuelTransport` 구현 3종, `DuelSession`(전송 + `DuelClientCore` + 시계) |
| `Client.Run` | `RunSession`(세계 + 기록기 + 중간 지점 올리기 + 종료 제출), `PracticeSession` |
| `Client.Boot` | 상태 파일 읽기와 서명 검증, 버전 검사, 접속 가능 여부 판정 |
| `Client.Settings` | 설정과 키 배치(두 프로필이 같이 쓰는 유일한 것) |

### 10.5 화면 로직(`Wizard.ViewLogic`)

엔진이 없는 어셈블리다(T-31). 실수형을 쓸 수 있고 코어 버전 해시에 들어가지 않는다. 폴더 하나가 모듈 하나이고 테스트는 `tests/Wizard.ViewLogic.Tests`의 대응하는 폴더다.

| 모듈(폴더) | 내용 | 명세 |
|---|---|---|
| `ViewLogic.Stack`, `ViewLogic.Common` | `ScreenStack`(Push/Replace/Pop, 층, 입력 가로막기), 누를 수 있는 것의 상태 기계, 입력 모드, 공용 창, `UiScale` | 13 1절, 14 4.3절 |
| `ViewLogic.Screens.<화면>` | 화면마다의 상태 기계와 뷰 모델: 시작·메인 메뉴, 원정 준비와 시작 웨이브, 야영·조립, 연습, 비기, 서고, 정산·상점, 강화, 기록, 설정, 튜토리얼, 옷장, 인증, 순위표, 광장·사회, 거래·장터·우편, 결투, 시즌, 운영, 상점의 유료 탭 자리 | 13 |
| `ViewLogic.Hud` | 전투 HUD(웨이브 진행, 처치 수, 최근 입력, 회로 점등), 보스 HUD, 결투 HUD의 모델 | 13 7절 |
| `ViewLogic.Notify` | 알림 라우터(`NotificationRouter`) | 13 1.6절 |
| `ViewLogic.Presentation` | 표시 계약의 타입(`IAssetCatalog`, `IFxPlayer`, `ISfxPlayer`, `FxRequest`, `AssetKey`), `UiEventKeys`, `PresentationEventRouter`. 하위 폴더: `Bindings`(바인딩 표), `UiEvents`, `Limits`(광과민 한도, 번쩍, 흔들림 누적, 그림 정지 시계, 연출 줄이기, 입자 예산), `WandLooks`(지팡이 겉모습 자료) | 13 14절, 14 5절, `effects.md`, CONTRACTS 13절 |
| `ViewLogic.Assets` | 매니페스트 로더와 통합 에셋 레지스트리(`AssetKey` → 기록) | 14 5절 |
| `FixConvert` | `Fix64 → float`, 칸 → 표시 좌표(`System.Numerics.Vector2`), 센티도 → 도. 이 변환은 코어 밖에서 여기에만 있다(T-27) | CONTRACTS 13절 |

화면은 `IBackend`만 부른다(P7). 화면 문자열은 이름 표의 열쇠로만 가리킨다(P10). Unity 쪽 뷰(`unity/Assets/Game/UI`)는 이 모델을 그리기만 한다.

## 11. 결정론 보증

같은 씨앗과 같은 입력 기록이면 Unity(Mono, IL2CPP)와 서버(CoreCLR)에서, arm64와 x64에서 같은 해시가 나와야 한다.

| 장치 | 내용 |
|---|---|
| 금지 목록 | `Core`/`Content`/`Sim`에서 실수형, `System.Random`, 시계, `Guid.NewGuid`, `GetHashCode` 기반 순서, `Dictionary`/`HashSet` 순회, 불안정 정렬, 문화권에 기대는 문자열 연산, 멀티스레드 시뮬레이션, 가변 정적 필드, `[ThreadStatic]`, `unsafe`와 메모리 재해석(`BitConverter`, `MemoryMarshal`, `Unsafe`, `Buffer.BlockCopy`, `Marshal`), 상수가 아닌 제수의 원시 `/`·`%`, `checked` 금지 |
| 분석기 | `tools/Wizard.Analyzers`가 위 금지 목록을 컴파일 오류로 만든다. dotnet 빌드에서 돈다(같은 소스이므로 Unity 쪽도 지켜진다) |
| 연산 규칙 | 덧셈·뺄셈은 모든 빌드에서 `unchecked` 래핑, 곱셈은 128비트 곱 뒤 산술 시프트(내림), 나눗셈은 0 쪽 버림. 나눗셈은 전 함수다: 제수 0은 코드가 검사해 `SimInvariantException`, `MinValue ÷ −1`은 래핑. 적 체력과 체력 배율 피해만 포화. 디버그 빌드도 결과가 같다(검사는 단언문으로만) |
| 세계의 격리 | 세계끼리 상태를 나누지 않는다(정적 버퍼 없음). 세계 16개를 16스레드에서 같은 기록으로 동시에 돌린 해시가 단일 실행과 같다 |
| 상태의 범위 | `SpellTable`(길이와 원천), 판정기 상태, 지연 효과 큐가 스냅숏·직렬화·해시에 들어간다. 순번(`Seq`)은 상태와 해시에 넣지 않는다 |
| 순서 규칙 | 개체 처리는 id 오름차순. 입력은 `(틱, 오프셋, 키 분류, 슬롯, 순번)`. 동률이 남는 정렬은 금지 |
| 난수 | `Pcg32`만. 용도·웨이브별로 `RngStreams.Derive(씨앗, 흐름, 웨이브, 보조)`에서 얻는다 |
| 상태 해시 | 정규 직렬화(LE, 고정 순서)의 FNV-1a 64. 무한 웨이브는 5웨이브 지점과 판 끝, 결투는 30틱마다 |
| 버전 해시 | 재실행에 영향을 주는 소스(T-18의 목록. `Core/Errors`와 `Content/Rules`는 뺀다)의 SHA-256을 `SimVersion.g.cs`에 박는다 |
| 골든 테스트 | `tests/golden/`의 기록과 산술 경계 벡터를 dotnet(arm64, x64), Unity Mono(arm64, x64), IL2CPP(arm64, x64)가 각각 돌려 같은 해시를 확인한다. x64는 Rosetta 2로 돈다. 배포하는 조합은 그 조합의 통과가 배포 조건이다(T-30, T-39) |

## 12. 테스트 구조

| 층 | 대상 | 방법 |
|---|---|---|
| 단위 | 각 명세의 테스트 벡터(given/when/then) | xUnit. 명세의 벡터 id를 테스트 이름에 넣는다 |
| 결정론 | "같은 기록을 두 번 돌리면 해시가 같다", "같은 오프셋의 방향과 K는 방향이 먼저", 스냅숏 저장·복원 뒤 진행이 같다 | `Wizard.Sim.Tests` + 골든 |
| 교차 런타임·아키텍처 | 골든 해시가 dotnet, Unity Mono, IL2CPP × arm64, x64에서 같다 | 11절 |
| 넷코드 | 호스트 1 + 클라이언트 2를 메모리 안에서 잇고 지연·지터·유실(독립, 연속)·순서 바뀜·수신 공백을 흉내 낸다. 섬(0.07초) 판정이 통신 없는 실행과 같은지, 연속 유실 뒤 몰수가 0인지, 오프셋 균등 분포 입력의 폐기율이 0인지 본다 | `Wizard.Netcode.Tests`의 `NetSim` |
| 프로토콜 | JSON 픽스처 왕복(두 코덱), 패킷 바이트 픽스처, AEAD는 RFC 8439 벡터 | `Wizard.Protocol.Tests` |
| 규칙 | 강화 마르코프 정확값, 경제 시뮬레이션 회귀(`docs/sim/*.py`와 같은 모형) | `Wizard.Rules.Tests` |
| 화면 로직 | spec 13·14의 화면 벡터(화면 상태 기계, HUD 모델, 연출 한도, 에셋 레지스트리), 바인딩 표의 빠짐 검사 | `Wizard.ViewLogic.Tests`. Unity를 띄우지 않는다(T-31) |
| 서버 통합 | 멱등(같은 요청 두 번), 동시 요청, 교착 없음, 원장 대조, 검증 불일치 회수 | 실제 PostgreSQL |
| 오프라인 완주 | 네트워크를 끊고 "처음 실행 → 판 → 정산 → 강화 → 다시 실행" | Unity PlayMode + `Wizard.Client.Tests` |
| 결제 | 가짜 제공자로 세 흐름, 같은 영수증 두 번, 환불로 음수 잔액 | `Wizard.Server.Tests` |

## 13. 만드는 순서(의존 관계)

GDD 16절의 1~18단계와 `online.md` 11절의 1~10단계를 마일스톤과 작업에 대응시킨 것이다. 앞 단계가 뒤 단계의 전제다. 일정이 아니고 범위를 줄이지 않는다. 작업 하나하나의 착수는 `tasks.json`의 `deps`만 본다.

| GDD 16 | online 11 | 마일스톤 | 단계의 항목(GDD 16절 그대로) | 필요한 것(이 구조에서) | 대표 작업 |
|---|---|---|---|---|---|
| — | — | M0 | (기반) | 저장소 뼈대, 분석기, 생성 도구, CI, 작업 그래프 도구, Unity 뼈대, 에이전트 설정 설치, 보충 결정(T-31~T-35)의 문서 반영 | M0-TL-01·02, M0-BT-01~06, M0-UE-01, M0-TE-01 |
| 1 손맛 시제품 | 1 결정론 코어 | M1 | Unity 프로젝트와 git, 입력 설정, **한글 입력기 확인**. 60틱 고정소수점 코어, 입력 이벤트와 순서 규칙, 입력 기록과 재실행, 상태 해시, 두 자동 테스트. 8방향 이동, 자세와 반전, 시야 화살표. 고정 투사체 2종과 3갈래, 과열·임계·잠금·열 링. 슬라임 무한 생성, 포탑, 체력과 무적, 스쳐 피하기. 판정기와 임시 기술 3개, **섬 간격의 밀리초 표시**. **통과 기준 네 가지** | `Wizard.Core` 전부, `Sim.Input`·`World`·`Player`·`Heat`·`Graze`, `Sim.Runtime`(세계, 해시, 스냅숏, 재실행), `InputRecorder`, `Wizard.DataBuild` 최소판, Unity 뼈대(`App`, `Input`, 도형 표시), 골든(런타임 × 아키텍처) | M1-CORE-01~05, M1-SIM-01~09, M1-CLI-01·02, M1-UE-01~03, M1-TE-01·02 |
| 2 주문 컴파일러 | (7의 앞부분: 넷코드 뼈대) | M2 | 블록 16종, 속성, 실행 트리, 안전장치, 단위 테스트, 글자 출력 조립기 | `Sim.Spell`, 블록 데이터 16종. 멀티플레이 전제로 당긴 것: 패킷 코덱, AEAD, 시계, `DuelHostCore`, `DuelClientCore`, NetSim, 한 기기 2인 | M2-SPL-01~04, M2-NET-01~07 |
| 3 적과 웨이브의 뼈대 | — | M3 | 원형 4종(늑대 회피, 술사 엄폐), 예고 구역, 웨이브 생성, 약점과 저항, 콤보와 결정, 디버그 메뉴(웨이브 건너뛰기, 무적, 골드·결정·블록 지급, 강화 단계 설정) | `Sim.Enemies`(원형 4종), 예고, `Sim.Waves` 기본, `Sim.Combo`, 디버그 op, 2인 헤드리스 스모크 | M3-ENM-01~03, M3-WAV-01, M3-CMB-01, M3-CB-03, M3-DBG-01, M3-NET-01 |
| 4 야영 화면 | — | M4 | 조립(끌어다 놓기), 컴파일 트리 미리보기, **전리품 선택, 보관함, 허수아비 연습** | `RunCommand`와 야영 하위 상태, 전리품, `Wizard.ViewLogic` 기반(`ScreenStack`), HUD 모델(웨이브 진행, 처치 수, 최근 입력 5개, 회로 점등), 조립·연습 모델, 뷰 기반 | M4-RUN-01·02, M4-UL-01~04, M4-UE-01, M4-UV-01·02 |
| 5 비기 전체와 캐릭터 5명 | — | M5 | 공용 10종의 실제 효과, 능력치 5벌, 패시브 5종, 고유 비기 5종, 캐릭터별 트라이, 비기 탭(트라이 그림), 연습장의 저스트 게이지와 실패 원인. 1단계 통과 기준을 다섯 명으로 | `Sim.Arts`, `Sim.Characters`, 캐릭터 데이터 5벌, 캐릭터별 판정기 벡터 | M5-DAT-01, M5-ART-01~05, M5-CHR-01~05, M5-UL-01, M5-TE-01 |
| 6 프로필과 오프라인 백엔드 | 2 `IBackend`, `Wizard.Rules`, `OfflineBackend` | M6 | `IBackend`, 규칙 라이브러리(강화·정산·상점), `OfflineBackend`. 이름 표, 프로필, **서고**, 지팡이 개체(여러 자루, 정렬·거르기, +0 구입), **출발 조립**, 원정 지갑과 정산, 귀환 가방, 드랍, 비기 해금 저장, 상점 | `Wizard.Rules`, `Wizard.Text`와 이름 표, `Wizard.Client`의 `IBackend`·`OfflineBackend`·`ISaveStore`, `RunResultFacts`와 정산, 서고·조립 바꾸기 화면 | M6-RUL-01~03, M6-SIM-01, M6-CLI-01~03, M6-TXT-01·02, M6-UL-01~04 |
| 7 강화 화면 | 2(강화 화면과 슬롯 새기기) | M7 | 제물, 결과 분기, 변이 6종, 부적, 슬롯 새기기, 기댓값 계산과 표시, 하락 흔적, 결과 꾸러미, 공통 연출 | `Rules.Enhance` 전부, 강화 화면 상태 기계, 경제 시뮬레이션 회귀 | M7-RUL-01~03, M7-CLI-01, M7-UL-01~03, M7-TE-01 |
| 8 무한 웨이브 완성 | — | M8 | 신규 8종, **꼬리표**, 엘리트, 보스 6종, **허상과의 첫 만남과 복수 보상·복수 추적기, 하루와 시간대**, 야생의 변덕, R5, **시작 웨이브 고르기**, 점수, 기기 안의 기록 표 | 적 12종, 엘리트, `Sim.Bosses`, 변덕, R5, 점수, 상한 규칙, 오프라인 기록 | M8-ENM-01~04, M8-BOS-01~05, M8-WAV-01·02, M8-RUL-01, M8-CLI-01, M8-UL-01, M8-TE-01 |
| 9 겉모습과 마감 | — | M9 (+ V) | 캐릭터 그림, 지팡이 5종 × 14단계, 전투 효과와 층 순서·입자 예산, 소리, 타격감, 타이틀·일시정지·결과, 설정과 키 재설정, 튜토리얼. 네트워크를 끊은 완주 자동 테스트 | **지금 만드는 것**: 에셋 레지스트리, `UiEventKeys`(식별자), 연출 한도, FX·SFX는 **빈 구현과 바인딩 표(실제 재생기는 V-13·V-14)**, 설정, 튜토리얼, 옷장 뷰, 오프라인 완주 테스트. **에셋을 기다리는 것**: 실제 그림 반입과 합성(V-01~V-12), 소리의 실제 재생기(V-13), 입자 효과의 실제 재생기(V-14) | M9-AST-01~03, M9-PRS-01~03, M9-SET-01·02, M9-TUT-01·02, M9-COS-01·02, M9-TE-01 |
| 10 서버 기반 | 3 서버 기반 | M10 | 맥미니에 API 서버와 DB, 자체 계정(가입·로그인, 이메일 확인, **이메일로 하는 비밀번호 복구**), 재화 원장과 멱등 키, 강화 서버 함수, `ServerBackend`, TLS와 백업 | `Wizard.Protocol`, `Server.Data`(스키마, 원장, 멱등), `Server.App` 계정·강화, `Server.Api`, `ServerBackend`, 실제 메일 발송(`ApiMailSender`), 배치 | M10-PRO-01~03, M10-DB-01, M10-ACC-01~05, M10-API-01·02, M10-INV-01, M10-ENH-01, M10-SHP-01, M10-CLI-01~03, M10-OPS-01 |
| 11 판 제출과 검증, 순위표, 주간 도전 | 4 제출과 검증 워커, 순위표 | M11 | (제목 그대로) | Runs 유스케이스(`setup_canonical`, `claimed_facts`), `Server.Verify`, 순위표, 주간 도전, 부정 방지 | M11-RUN-01~04, M11-VER-01, M11-BRD-01, M11-ANT-01, M11-TE-01 |
| 12 광장 | 5 광장 | M12 | 인스턴스, 이동, **시설**, 채팅, 친구, **살펴보기, 서버 전체 알림** | `Netcode`의 광장 호스트·클라이언트, 웹소켓 허브(`play_seconds`), 시설, 채팅, 친구, 살펴보기, 알림 | M12-NET-01, M12-API-01, M12-PLZ-01, M12-CHT-01, M12-SOC-01, M12-NTC-01 |
| 13 거래 | 6 거래 | M13 | 직접 거래, 장터, 우편함, 야간 대조 | Trade 유스케이스, 야간 대조 | M13-RUL-01, M13-TRD-01, M13-MKT-01, M13-MAIL-01, M13-OPS-01 |
| 14 결투 | 7 결투 | M14 | 로컬 2인(**4.4절 통과 기준**) → 서버 시뮬레이션과 판정 → 예측과 되감기 → 결투 방 → 자동 매치, 아이템 레벨과 리그, **결투 점수**. **캐릭터 5×5 상성표** | `Sim.Duel`, 결투 넷코드 완성(끊김, 재접속, 관전), `Server.Duel` → UDP → 방·매치, `Rules.League`, 결투 점수, 상성표와 적중률 계측 | M14-DUL-01·02, M14-NET-01, M14-SRV-01~03, M14-RUL-01, M14-TE-01~03 |
| 15 시즌 | 8 시즌 | M15 | 후계 점수, 정산, **후계자와 한정 보상, 시즌 상점** | `Rules.Season`, 시즌 정산(주화 환전 포함), 시즌 상점 | M15-RUL-01, M15-SRV-01, M15-CLI-01, M15-UL-01 |
| 16 운영 | 9 운영 | M16 | 관리 화면, **이상 탐지**, 부하 시험, 복원 연습, **서버가 꺼졌다 켜지는 연습** | `Server.Admin`, 운영 작업, `LoadBot`, 연습 스크립트 | M16-ADM-01, M16-OPS-01·02, M16-BOT-01 |
| 17 결제 뼈대 점검 | 10 결제 뼈대 점검 | M17 | 가짜 제공자로 구매 흐름 통과 뒤 빈 제공자로 되돌림. 유료 상품 0개 | `Payments.Fakes`(DevHost와 테스트에서만)로 세 흐름 통과 | M17-PAY-01·02, M17-CLI-01 |
| 18 밸런스 QA와 발표 자료 | — | M18 (+ V-16) | GDD 17절의 항목, 지팡이 회로 점등, 트라이 그림, 실패 링크 시연 화면 | 밸런스 계측 둘(GDD 17, online 12), 출시 빌드(조합별 골든), 문서 정리. 발표 자료의 그림은 V-16(회로 점등의 **로직**은 M4의 HUD에 이미 있다) | M18-QA-01·02, M18-REL-01, M18-DOC-01, V-16 |

멀티플레이 전제 때문에 1단계에서 미리 넣는 것: 플레이어 배열, 슬롯이 붙은 입력, 스냅숏·복원, 상태 해시, `SimConfig` 모드 스위치, 표시 이벤트의 예측/확정 단계. 결투 규칙 자체는 14단계에서 채우지만 자리는 처음부터 있다. 넷코드의 봉인·예측·되감기는 2단계 직후(M2)에 만든다.
