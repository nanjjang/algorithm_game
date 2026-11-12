# 기술 결정 기록 — 엄청엄청 위대한 마법사 키우기

기술 구조에 대한 결정을 짧게 적는다. 게임 내용의 결정은 `docs/DECISIONS.md`가 주인이고, 이 문서는 그 아래에서 "어떻게 만드는가"만 다룬다. 구조는 `ARCHITECTURE.md`, 시그니처는 `CONTRACTS.md`에 있다.

형식: **결정** / **대안** / **이유** / (있으면) **따라오는 일**.

표시: [통합]은 명세끼리 표현이 달라 하나로 정한 것, [신규]는 명세가 정하지 않아 새로 정한 것이다. 명세의 [임시] 값을 대신하는 결정에는 그 항목 번호를 적었다.

## 요약

| # | 결정 | 표시 |
|---|---|---|
| T-01 | 공유 코드는 `netstandard2.1` + C# 9 단일 타깃 | [신규] |
| T-02 | 코어는 로컬 UPM 패키지로 소스를 공유한다(DLL 아님) | [신규] |
| T-03 | 서버·도구·테스트는 .NET 10 | [신규] |
| T-04 | 공유 어셈블리 9개, 의존은 한 방향 (개정) | [신규] |
| T-05 | 싱글도 플레이어 배열을 가진 같은 `SimWorld` | [신규] |
| T-06 | 고정소수점은 `Fix64`, 연산과 반올림 규칙 하나 | [통합] |
| T-07 | 각도는 센티도 정수 | [통합] |
| T-08 | 열은 정수 HU | [통합] |
| T-09 | 난수는 PCG32, `(씨앗, 흐름, 웨이브, 보조)` 파생 | [통합] |
| T-10 | 상태 해시는 FNV-1a 64, 기록 무결성은 SHA-256 | [통합] |
| T-11 | 불안정 정렬과 해시 순회 금지, `DetSort` | [신규] |
| T-12 | 키 코드와 입력 정렬 키 하나, 슬롯 포함 | [통합] |
| T-13 | 야영 조작은 `RunCommand`, 틱을 멈추는 것은 일시정지뿐 (개정) | [통합] |
| T-14 | 스냅숏은 값 복사, 결투 클라이언트는 확정 세계 + 예측 세계 (개정) | [통합] |
| T-15 | 표시 이벤트는 고정 크기 구조체, 예측/확정/번복 단계 | [신규] |
| T-16 | 콘텐츠 id의 정본은 spec 14 | [통합] |
| T-17 | 데이터는 JSON으로 적고 정수 이진 팩으로 싣는다 | [신규] |
| T-18 | 코어 버전은 재실행에 영향을 주는 소스의 해시, 워커는 버전별 바이너리, `wizard-api`는 코어 버전과 무관 (개정) | [신규] |
| T-19 | 넷코드는 소켓 없는 `Wizard.Netcode`에 둔다 | [신규] |
| T-20 | HTTP는 JSON, 서버 System.Text.Json / 클라 Newtonsoft, 픽스처로 묶는다 | [신규] |
| T-21 | 결투 UDP는 직접 만든 이진 + ChaCha20-Poly1305, 수명주기는 웹소켓 | [신규] |
| T-22 | `wizard-api` ↔ `wizard-duel`은 유닉스 도메인 소켓 HTTP | [신규] |
| T-23 | 검증 워커는 재실행 facts만 적고, 비교·확정·회수는 `wizard-api`가 한다 (개정) | [신규] |
| T-24 | DB 접근은 Npgsql + Dapper + SQL 마이그레이션 | [신규] |
| T-25 | `run.id`와 `duel_match.id`는 uuid | [통합] |
| T-26 | `IBackend` / `IOnlineBackend` 분리, `Result<T>` | [신규] |
| T-27 | 실수는 표시 계층과 서버의 레이팅에만 | [신규] |
| T-28 | 입력은 Input System 이벤트 시각으로 양자화 | [신규] |
| T-29 | 에셋 없이 도는 표시 계층(도형 + 빈 구현 + 바인딩 표) | [신규] |
| T-30 | 결정론은 런타임 × 아키텍처 조합의 골든 테스트로 지킨다 (개정) | [신규] |
| T-31 | 화면 로직은 엔진 없는 공유 패키지 `Wizard.ViewLogic`에 둔다 | [신규] |
| T-32 | 화면 뷰는 uGUI로, 코드로만 만든다(프리팹·장면 편집 없음) | [신규] |
| T-33 | 뼈대를 먼저 만들고, 모듈은 부분 클래스의 훅과 모듈 등록으로 붙는다 | [신규] |
| T-34 | Unity `.meta`의 GUID는 경로에서 계산한다(`MetaGen`) | [신규] |
| T-35 | 이름 표는 병합되는 형식으로 고정한다(정렬, 한 줄에 열쇠 하나, 끝 표지, union) | [신규] |
| T-36 | 메일은 HTTPS API 방식의 `ApiMailSender` + 사업자 어댑터 | [신규] |
| T-37 | 외부 라이브러리: BouncyCastle, Argon2, JSON Schema, Newtonsoft | [신규] |
| T-38 | 테스트용 PostgreSQL은 Docker의 고정 이미지 | [신규] |
| T-39 | 배포 대상마다 골든이 배포 조건. Windows IL2CPP는 Windows 기기에서만 | [신규] |
| T-40 | CI의 로직은 C# 도구, 작업 상태는 추적하지 않는 상태 파일 | [신규] |
| T-41 | 다시 만드는 파일(생성 코드, 골든 기대값)과 절 꼬리표 해시 | [신규] |
| T-42 | 코어의 금지 목록 확장: 가변 정적, 메모리 재해석, 원시 나눗셈, `checked` | [신규] |
| T-43 | 솔루션은 `.sln`, 분석기는 `netstandard2.0`, Unity 쪽은 최상위 빌드 설정을 끊는다 | [신규] |
| T-44 | IL2CPP 코드 제거는 `link.xml`로 막고 플레이어에서 왕복 테스트를 돌린다 | [신규] |
| T-45 | 서버 주도의 왕복 측정, 결투 키의 epoch, 입력 검증 순서 | [신규] |
| T-46 | `EndlessSetup`의 정규 바이트는 `.wzrc` 머리말 전체이고, 코어·데이터 버전은 `EndlessSetup`의 칸이다 | [신규] |

---

## T-01 공유 코드는 `netstandard2.1` + C# 9 단일 타깃

- **결정**: `Wizard.Core`, `Content`, `Text`, `Sim`, `Rules`, `Protocol`, `Netcode`, `Client`, `ViewLogic`(T-31)은 `netstandard2.1` 하나만 대상으로 하고 C# 9 문법만 쓴다. `#if`로 런타임을 가르지 않는다.
- **대안**: (가) `netstandard2.1;net10.0` 다중 타깃으로 서버에서 `Int128`, `Math.BigMul` 같은 빠른 API를 쓴다. (나) 서버용 코어를 따로 둔다.
- **이유**: 설치된 Unity 6000.3.20f1의 컴파일러는 Roslyn 4.3.1이고 언어 판은 C# 9로 고정이며 API는 .NET Standard 2.1이다. 다중 타깃은 코어 계산 경로를 둘로 만들어 "같은 소스"라는 전제를 깬다. 결투 한 경기의 서버 비용이 코어 하나의 1.2%쯤이라(spec 10 14.13) 성능을 위해 위험을 살 이유가 없다.
- **따라오는 일**: `record`, `init`, `record struct`, 파일 범위 네임스페이스, `Int128`, `BitOperations`, `System.Text.Json`, `ChaCha20Poly1305` 클래스를 공유 코드에서 쓰지 않는다. 명세의 `record struct`(spec 10 17.2)는 일반 구조체로 바꾼다. 128비트 곱과 나눗셈은 `Wizard.Core.Wide`에 직접 만든다(spec 01 2.2의 [임시]를 확정).

## T-02 코어는 로컬 UPM 패키지로 소스를 공유한다

- **결정**: `src/`의 폴더마다 `package.json` + asmdef + csproj를 두고, Unity는 `Packages/manifest.json`의 `file:` 참조로 소스를 직접 컴파일한다. 공유 asmdef는 전부 `noEngineReferences: true`다. dotnet의 출력은 `UseArtifactsOutput`으로 저장소 최상위 `artifacts/`에 모은다.
- **대안**: (가) dotnet으로 DLL을 빌드해 `Assets/Plugins`에 복사한다. (나) `Assets/` 안에 소스를 두고 서버 csproj가 `..\unity\Assets\…`를 링크한다. (다) git 서브모듈.
- **이유**: DLL 방식은 Unity를 열기 전에 빌드 단계가 하나 더 생기고, 낡은 DLL로 테스트하는 사고가 난다. 디버깅과 IL2CPP 스트리핑도 소스 쪽이 쉽다. (나)는 서버가 Unity 프로젝트 폴더에 묶인다. `noEngineReferences`는 코어가 `UnityEngine`을 건드리는 순간 컴파일이 깨지게 해서 원칙 P3을 기계가 지키게 한다.
- **따라오는 일**: `src/` 안의 `.meta`를 커밋한다. Unity가 만든 것이 아니라 `MetaGen`이 경로에서 계산한 것이다(T-34). `bin/`·`obj/`가 패키지 폴더에 생기면 Unity가 DLL을 중복으로 읽으므로 `artifacts/` 출력이 필수다.

## T-03 서버·도구·테스트는 .NET 10

- **결정**: `server/`, `tools/`, `tests/`는 `net10.0`. `global.json`으로 SDK 10.0.201을 고정한다.
- **대안**: .NET 8.
- **이유**: .NET 10은 LTS이고 이미 설치되어 있다. 맥미니(arm64)에서 그대로 돈다. 서버만 쓰는 코드(`Wizard.Server.*`)는 최신 C#을 쓸 수 있다.

## T-04 공유 어셈블리 9개, 의존은 한 방향 (개정)

- **결정**: `Core ← Content ← Sim ← Rules ← Protocol ← Netcode ← Client ← ViewLogic ← Wizard.Unity.{Presentation, UI, App}`, `Text ← Client`. 공유 어셈블리는 아홉이다(여덟에 `Wizard.ViewLogic`을 더했다. T-31). 서버는 `Data ← App ← Api/Admin`, `Duel`은 `Netcode`까지만, `Verify`는 `Sim`, `Protocol`과 `Data`의 대기열 저장소만.
- **대안**: (가) spec 01 3.2처럼 `Wizard.Sim` 하나에 전부. (나) 과열, 컴파일러, 판정기를 각각 어셈블리로.
- **이유**: (가)는 규칙 라이브러리와 통신 DTO가 전투 코어에 섞여 버전 해시가 불필요하게 바뀐다. (나)는 서로 호출이 잦은 전투 모듈을 갈라 순환을 만든다. 전투 모듈은 `Wizard.Sim` 안의 네임스페이스로 나누고, 호출 순서는 `SimWorld`의 틱 파이프라인 한 곳에 둔다.
- **개정 이유**: 처음 구성은 공유 어셈블리가 여덟이었고 화면의 상태 기계가 `Wizard.Unity.UI`에 있었다. 화면 규칙을 `dotnet test`로 검증하려고 아홉 번째를 더했다(T-31). 서버 프로젝트는 `ViewLogic`을 참조하지 않는다.
- **따라오는 일**: `Rules`가 `Sim`을 참조하는 곳은 리그 제한(`WandSnapshot`)뿐이다. 정산과 1차 검사는 Sim 타입이 아니라 형식 판이 붙은 정규 바이트 `RunResultFacts`를 받는다(T-18·T-23 개정). 반대 방향은 금지다. 야영의 회복 값처럼 재실행 결과를 바꾸는 판 안의 경제는 `Sim` 쪽에 있다.

## T-05 싱글도 플레이어 배열을 가진 같은 `SimWorld`

- **결정**: `SimWorld`는 모드와 무관하게 플레이어 배열을 가지고, 입력은 `(슬롯, InputEvent)`로만 받는다. 무한 웨이브는 길이 1, 결투는 길이 2다. 모드 차이는 `SimConfig`의 값이다. 광장은 `Sim.World`의 방향 벡터와 벽 판정을 같이 쓰되, 스텝은 spec 11 2절의 0.1초 고정 스텝 `MoveAndSlide`다(전투의 60틱 적분과 식이 다르다. 광장은 해시로 검증하지 않고 서버 위치로 맞춘다).
- **대안**: 무한 웨이브용 세계를 먼저 만들고 결투용을 나중에 따로 만든다.
- **이유**: 처음부터 멀티플레이를 전제로 한다는 요구. 스냅숏, 상태 해시, 슬롯이 붙은 입력, 표시 이벤트의 단계는 나중에 끼워 넣기가 가장 어려운 것들이다. 무한 웨이브도 서버 재실행이 있어 결정론 요구는 같다.

## T-06 고정소수점은 `Fix64`, 연산과 반올림 규칙 하나 [통합]

- **결정**: 타입 이름 `Fix64`(Q32.32), 벡터 `FixVec2`. 십진 상수는 가장 가까운 Raw로, 같으면 짝수로. 곱셈은 128비트 곱 뒤 산술 시프트(내림), 나눗셈은 0 쪽 버림. 덧셈·뺄셈·곱셈의 넘침은 **모든 빌드에서** 래핑한다. **나눗셈은 전 함수로 정의한다**: 제수 0은 코드가 직접 검사해 `SimInvariantException`, `MinValue ÷ −1`은 `MinValue`(래핑), 128÷64의 몫 넘침은 아래 64비트. 적 체력과 체력 배율 적용 피해만 포화시킨다(spec 07 6.1).
- **대안**: spec 01의 "검사 모드에서는 넘침 예외", spec 02의 타입 이름 `Fx`, spec 04의 1/6000칸 정수 단위.
- **이유**: 디버그 빌드에서만 예외가 나면 디버그 클라이언트와 릴리스 서버의 결과가 달라진다. 범위 확인은 결과를 바꾸지 않는 단언문으로 한다. spec 04의 U 단위는 세 번째 수 체계라서 버리고, 데이터는 칸 단위 십진으로 적어 빌드 도구가 바꾼다(spec 04의 값 자체는 같다).
- **개정 이유(나눗셈)**: IL2CPP는 0으로 나누기 검사가 기본으로 꺼져 있어 arm64에서는 0을 돌려주고 x64에서는 프로세스가 죽는다. CoreCLR과 Mono는 예외를 던진다. `MinValue ÷ −1`도 런타임과 아키텍처마다 다르다. 런타임에 맡기면 서버는 판을 중단하고 클라이언트는 0으로 계속 가는 분기가 생긴다. 분석기가 코어의 상수가 아닌 제수의 원시 `/`·`%`와 `checked`를 막는다(T-42).
- **대신하는 [임시]**: spec 01 U39, spec 02 U18.

## T-07 각도는 센티도 정수 [통합]

- **결정**: `Angle`은 1/100도 정수(한 바퀴 36,000). sin/cos는 0~90.00도의 9,001칸 표와 대칭으로 얻고 보간하지 않는다. `Atan2`는 표 위의 이분 탐색(정수 비교)이다. 방향은 0 = 오른쪽, 화면에서 반시계가 +이고 `Dir8`의 순서와 같다. 전장의 y는 아래가 +이므로 방향 벡터는 `(cos a, −sin a)`다.
- **대안**: spec 01의 1/65536바퀴 + 4096칸 선형 보간, spec 10의 BAM32와 시계 방향 +.
- **이유**: 기획 수치가 전부 도 단위다(±10, ±15, ±25, 20, 110, 초당 180도). 센티도에서는 이 값들이 정확히 떨어진다. BAM32에서는 10도가 정수가 아니다(spec 10이 119,304,647로 반올림해 둔 값). 표는 72KB라 부담이 없고 보간이 없으면 구현 차이가 날 자리가 없다. 방향은 이미 확정된 `Dir8`(E, NE, N … 반시계)에 맞춰야 `Dir8 × 4500`이 그대로 각도가 된다.
- **대신하는 [임시]**: spec 01 U40, spec 02 U17, spec 10 U-35.

## T-08 열은 정수 HU [통합]

- **결정**: 열은 `int`, 열 1 = 120 HU. 분열과 거울상의 배수는 천분율 정수(1600, 2200, 1500)로 곱한다. `BlockDef.HeatHu`, `CompiledSpell.HeatPerShotHu`.
- **대안**: spec 02의 `Fx Heat`.
- **이유**: spec 01 2.4의 근거 그대로다. 초당 50 냉각(틱당 0.8333…)을 Q32.32로 쌓으면 임계 경계(정확히 85, 100) 판정이 검산값과 어긋날 수 있다. HU에서는 모든 열 연산이 나머지 없이 떨어진다.
- **대신하는 [임시]**: spec 01 U14.

## T-09 난수는 PCG32, `(씨앗, 흐름, 웨이브, 보조)` 파생 [통합]

- **결정**: 생성기는 PCG32 XSH-RR 하나. 모든 흐름은 `RngStreams.Derive(seed, stream, wave, sub)`로 만든다(SplitMix64로 섞고 `seq = stream`). 틱에 걸쳐 쓰는 흐름은 세계가 보관하고 웨이브(결투는 라운드) 시작 때 다시 파생한다. 흐름 표는 `RngStream` 하나다.
- **대안**: spec 01의 "판 씨앗 + 고정 `StreamId`로 판 전체에 하나씩".
- **이유**: 웨이브마다 독립이면 (1) 시작 웨이브를 건너뛰어도 뒤 웨이브의 구성이 같고, (2) 서버가 전투를 돌리지 않고 `Modifier` 흐름만으로 웨이브별 상한을 계산할 수 있고(spec 07 C-07, spec 09 C1), (3) 앞 웨이브의 난수 소비량이 바뀌는 수정이 뒤 웨이브 전체를 흔들지 않는다.
- **대신하는 [임시]**: spec 01 U37, spec 03 U-20(비기 해금 후보는 `ArtUnlock` 흐름).

## T-10 상태 해시는 FNV-1a 64, 기록 무결성은 SHA-256 [통합]

- **결정**: 상태 해시는 정규 직렬화(LE, 고정 순서, id 오름차순)의 FNV-1a 64비트. 입력 기록 파일과 콘텐츠 팩의 무결성은 SHA-256.
- **대안**: xxHash64(spec 10의 예시), 상태 해시를 SHA-256으로.
- **이유**: 상태 해시는 보안 경계가 아니다. 서버는 해시를 믿지 않고 직접 다시 돌린다(무한 웨이브), 결투에서는 서버 상태가 정본이다. 해시의 일은 "어긋났는가"를 싸게 알아내는 것뿐이라 구현이 열 줄인 FNV-1a가 맞다. 파일은 전송과 보관 중의 손상·바꿔치기를 봐야 해서 SHA-256을 쓴다.
- **대신하는 [임시]**: spec 01 U36, spec 10 U-39. DB의 `state_hash`는 8바이트다.

## T-11 불안정 정렬과 해시 순회 금지 [신규]

- **결정**: 코어에서 `Array.Sort`, `List<T>.Sort`를 쓰지 않는다. `DetSort`(안정 정렬)를 쓰거나 동률이 없는 완전 순서 비교자를 쓴다. `Dictionary`, `HashSet`은 순회하지 않는다. 분석기가 컴파일 오류로 막는다.
- **대안**: "비교 키를 정수로만 쓴다"는 규칙만 둔다(spec 01 3.1).
- **이유**: .NET의 `Array.Sort`는 불안정 정렬이고 구현이 런타임마다 다르다(Mono, IL2CPP, CoreCLR). 키가 같은 원소의 순서가 기기와 서버에서 달라지면 재실행이 어긋난다. 이런 차이는 가끔만 나타나서 찾기 어렵다.

## T-12 키 코드와 입력 정렬 키 하나 [통합]

- **결정**: `LogicalKey`는 Up 0, Left 1, Down 2, Right 3, Fire 4, Skill 5, Stance 6, Reverse 7, Pause 8. 정렬 키는 `(틱, 오프셋, 키 분류, 슬롯, 순번)`, 키 분류는 자세 0, 방향 1, 발사 2, 비기 3, 일시정지 4.
- **대안**: spec 10의 키 순서(Up, Down, Left, Right), spec 09의 분류(방향 0, K 1, 그 밖 2).
- **이유**: 코어의 주인인 spec 01을 따른다. 확정 규칙 "방향이 K보다 먼저"는 셋 다 지킨다. 슬롯을 넣은 것은 두 사람의 이벤트가 `(틱, 오프셋, 분류, 순번)`까지 같을 때도 순서가 정해지게 하려는 것이다(입력 적용은 자기 상태만 바꾸므로 공정성에는 영향이 없다).
- **대신하는 [임시]**: spec 01 U8, spec 03 U-08, spec 10 U-17·U-18.

## T-13 야영 조작은 `RunCommand`, 틱을 멈추는 것은 일시정지뿐 [통합] (개정)

- **결정**: 키로 적을 수 없는 판 안의 조작(카드 고르기, 다시 뽑기, 조립, 회복, 비기 해금, 야영 화면 닫기, 연습 들어가기·나오기, 출발, 포기)은 `RunCommand`로 기록한다. **전리품 창과 야영 화면이 떠 있어도 틱은 진행한다.** 틱을 멈추는 것은 일시정지뿐이고, 일시정지는 실제 시간을 `PauseMark`로 남긴다. 창이 열린 동안 코어는 야영 하위 상태(`CampSub`)에 따라 키를 무시하고, 명령은 발생한 틱의 0단계에서 바로 적용된다.
- **대안**: spec 01·07의 이름 `CampCommand`. 창이 떠 있는 동안 틱을 멈춘다(이 문서의 처음 결정. 버림).
- **이유**: spec 09의 구조(틱, 순번, op, 인자)가 가장 구체적이라 그 이름을 쓴다. 틱 진행은 spec 09 8.3절("야영도 코어 틱은 나간다")과 spec 07 11.4절·TV-158을 따른다. spec 09의 시간 모형(`est_end = started_at + end_tick/60 + pause_total`, 제출 지연 600초, 느리게 돌리기 탐지)이 야영의 틱 진행을 전제로 한다. 창 동안 틱을 멈추면 야영 화면에서 보낸 시간이 "실제 시간 − 틱 시간"에 쌓여, 야영을 오래 본 정직한 판이 순위에서 빠지고 `SlowPlay` 표시가 붙는다. 창 시간을 따로 인정해 주면 클라이언트가 주장하는 무제한 여유가 생겨 탐지가 무력해진다. 처음 결정의 이유였던 "기록이 불어난다"는 틀렸다. 입력이 없는 틱은 기록에 바이트를 남기지 않는다(Δtick). 출발 표식의 60틱 누름과 허수아비 연습도 틱이 가야 돈다.
- **따라오는 일**: 야영 하위 상태의 전이 가운데 화면 조작으로 일어나는 것은 전부 명령이 있어야 한다(`CloseCampScreen`, `EnterPractice`, `ExitPractice`). 하위 상태별로 받는 키와 명령의 표는 `CONTRACTS.md` 3.2절. 재실행에서 무효 명령이 나오면 `RecordCorrupt`다.
- **대신하는 [임시]**: spec 01 U38, spec 07 U-15, spec 09 U4. spec 01 16절의 "코어는 야영 중 틱을 돌리지 않는다 [임시]"는 버린다.

## T-14 스냅숏은 값 복사, 결투 클라이언트는 확정 세계 + 예측 세계 [통합] (개정)

- **결정**: `SimWorld`는 `SaveSnapshot`/`LoadSnapshot`만 제공한다(값 복사, 할당 없음). `DuelClientCore`는 확정 세계 1개와 예측 세계 1개를 두고, 예측이 틀리면 확정 세계를 스냅숏 버퍼 1개로 예측 세계에 복사해 다시 돌린다. 해시는 확정 세계에서 계산한다. 세계 상태 안의 참조는 정수 핸들로 바꾸고(`SpellHandle`), 핸들이 가리키는 `SpellTable`도 세계 상태다(길이와 원천이 스냅숏·직렬화·해시에 들어간다).
- **대안**: 세계가 고리 버퍼 9개를 가진다(spec 01). 틱별 스냅숏 16개를 클라이언트가 보관한다(이 문서의 처음 결정). 상태 전체를 직렬화해 되감는다.
- **이유**: 되감기는 결투에서 틱마다 일어날 수 있어서 직렬화보다 배열 복사가 맞다. 직렬화(`WriteState`)는 드문 일(재접속, 해시 불일치, 관전 시작)에만 쓴다. 보관 개수는 넷코드의 사정이지 세계의 사정이 아니다. 세계를 둘로 두면 (1) 확정 상태의 해시를 따로 계산할 길이 생기고(스냅숏은 불투명하다), (2) 확정 이벤트가 확정 세계에서만 나와 "같은 이벤트를 두 번 확정"이 구조상 생기지 않고, (3) 틱별 고리가 없어도 되감기 한도 8틱 안의 재실행 비용은 같다. `SpellTable`을 상태에 넣지 않으면 되감은 뒤 핸들 번호가 서버와 달라지거나, 상태 전체를 받은 클라이언트가 자기 표에 없는 핸들을 가리킨다.
- **대신하는 [임시]**: spec 01 U43, spec 10 14.7의 보관 방식.

## T-15 표시 이벤트는 고정 크기 구조체, 예측/확정/번복 단계 [신규]

- **결정**: 코어는 `SimEvent`(참조 없는 고정 크기 구조체)를 `ISimEventSink`에 쓴다. 결투에서는 `DuelClientCore`가 이벤트 장부를 두고 `Predicted`, `Confirmed`, `Retracted` 단계로 표시 계층에 넘긴다. 이벤트에서 에셋으로 가는 대응은 데이터(`event_bindings.json`)다.
- **대안**: C# 이벤트·델리게이트를 종류별로 둔다. 이벤트에 `IsConfirmed` 표시 하나만 단다(spec 01 12.4).
- **이유**: 델리게이트는 할당과 호출 순서 문제를 만든다. 되감기 뒤에 같은 이벤트를 두 번 재생하지 않으려면 "이 이벤트가 전에 나왔던 것인가"를 알아야 하는데, 그 판단은 표시 계층이 아니라 넷코드가 가진 정보로만 할 수 있다. 번복 단계가 있어야 "문양이 0.1초 흐려짐"(effects 17)을 그릴 수 있다.

## T-16 콘텐츠 id의 정본은 spec 14 [통합]

- **결정**: 문자열 id와 정수 코드는 spec 14 3절을 따른다(`block.fireball`, `art.burst`, `boss.kiln_boar`, `mat.stone_low`, `element.ice`). 다른 명세의 이름은 `CONTRACTS.md` 2.1절의 대응표로 바꿔 읽는다. 코어는 정수 열거형, 저장·통신·DB는 문자열 id를 쓴다.
- **대안**: 명세마다의 이름을 별칭으로 모두 받아 준다.
- **이유**: 같은 블록에 이름이 셋(`great_fireball`, `giant_fireball`, `fireball`)이면 DB와 기록 파일에서 반드시 사고가 난다. spec 14가 그림 매니페스트의 실제 파일 이름과 맞춘 유일한 목록이다. 별칭은 매니페스트를 읽을 때만 쓴다.
- **대신하는 [임시]**: spec 02 U33, spec 07 U-27.

## T-17 데이터는 JSON으로 적고 정수 이진 팩으로 싣는다 [신규]

- **결정**: 기획 수치의 원본은 `data/`의 JSON이다. `tools/Wizard.DataBuild`가 검증하고 정수(Raw, 틱, HU, 센티도, 천분율)로 바꿔 `.wzpack`을 만든다. 코어는 팩만 읽는다. 팩에는 `data_version`과 SHA-256이 있고 판마다 기록한다.
- **대안**: (가) ScriptableObject. (나) CSV. (다) JSON을 실행 중에 직접 읽는다. (라) 수치를 생성 코드(`*.g.cs`)로 코어에 박는다.
- **이유**: (가)는 서버가 못 읽는다. (나)는 공격 목록 같은 겹친 구조에 약하다. (다)는 코어에 JSON 파서와 소수 파싱이 들어가고, 파서 구현 차이가 결정론 위험이 된다. (라)는 "서버가 데이터를 내려 덮어쓴다"(spec 07 2절)를 못 한다. 팩 방식은 변환과 검증(회피 검산 R1~R5, 약점·저항 분배, 금지 필드)을 빌드 때 한 번에 끝낸다.
- **따라오는 일**: `ContentDb`는 전역이 아니라 주입한다. 검증 워커는 판의 `data_version`에 맞는 팩을 읽는다.

## T-18 코어 버전은 재실행에 영향을 주는 소스의 해시, 워커는 버전별 바이너리, `wizard-api`는 코어 버전과 무관 [신규] (개정)

- **결정**: `SimVersion.CoreVersion`은 **재실행 결과에 영향을 주는 소스**의 SHA-256(앞 16바이트)이고 `SimVersion.g.cs`에 박아 커밋한다. `wizard-verify`는 코어 버전마다 따로 빌드해 최근 두 버전을 같이 띄우고, 각 워커는 자기 버전의 판만 꺼낸다. **`wizard-api`의 판 처리 경로(출발, 1차 검사, 정산, 검증 결과 비교)는 코어 버전에 기대지 않는다.**
- **해시의 대상(VersionStamp의 목록)**: `src/Wizard.Core/**`에서 `Errors/`와 `Generated/` 밖의 `.cs`, `src/Wizard.Core/Generated/*.g.cs`(표), `src/Wizard.Content/**`에서 `Rules/` 밖의 `.cs`, `src/Wizard.Sim/**`에서 `Generated/SimVersion.g.cs` 밖의 `.cs`. 경로 오름차순으로 (경로, 줄 끝을 LF로 맞춘 내용)을 이어 해시한다. 빠지는 것: `ErrorCode`·`Result`(`Core/Errors/`), 규칙 전용 정의와 절 읽기(`Content/Rules/`). 분석기가 `Wizard.Sim`과 해시 안의 `Wizard.Content`가 빠진 쪽을 참조하지 못하게 막는다.
- **대안**: (가) git 커밋 id를 빌드 때 읽는다. (나) 워커 하나가 옛 코어 어셈블리를 동적으로 불러온다.
- **이유**: Unity는 소스를 직접 컴파일하므로 빌드 시점의 git 정보에 기댈 수 없다. 소스 해시는 "계산에 영향이 있는 변경"만 버전을 바꾼다. 동적 로딩은 의존 어셈블리 충돌과 실수 여지가 크고, 버전별 바이너리는 폴더 하나와 launchd 항목 하나면 된다. 해시의 범위를 좁힌 것은, 거래 오류 코드 하나나 상점 표의 필드 하나를 바꿀 때마다 코어 버전이 바뀌면 워커를 새로 빌드해야 하고 "최근 두 버전"에서 밀려난 대기 판이 처리되지 못하기 때문이다.
- **`wizard-api`의 독립**: `wizard-api`는 지금 버전의 코어 한 벌만 링크한다. 배포 전에 옛 코어로 출발한 판이 배포 뒤에 끝나서 오면, 새 코드의 정규 형식·새 `WaveMath`로 처리해 1차 검사가 거짓으로 실패하거나 워커(옛 형식)와 다이제스트가 달라 정직한 판이 회수될 수 있다. 그래서 (1) 정산과 1차 검사의 입력은 Sim 타입이 아니라 형식 판이 붙은 정규 바이트 `RunResultFacts`다. 쓰는 쪽은 그 판의 코어(클라이언트와 워커), 읽는 쪽은 `Wizard.Rules`이고 지금 돌고 있는 워커 버전들의 형식을 모두 읽는다. (2) 1차 검사의 변덕은 spec 09 8.2의 `whims_upper(w)`(최대치)를 쓰고 씨앗에서 다시 계산하지 않는다. (3) 재실행의 시작 조건은 출발 때 서버가 만든 `run.setup_canonical`이다. `CONTRACTS.md` 3.7·4.3·8.3절.
- **대신하는 [임시]**: spec 01 3.5의 만드는 법, spec 09 U18의 일부(대기 판이 남은 버전의 워커는 내리지 않는다).

## T-19 넷코드는 소켓 없는 `Wizard.Netcode`에 둔다 [신규]

- **결정**: 결투의 봉인·검증·확정(`DuelHostCore`), 예측·되감기(`DuelClientCore`), 시계 맞추기, 광장의 호스트·클라이언트 로직은 소켓과 시계를 인자로만 받는 순수 클래스다. `wizard-duel`, 클라이언트, `LoopbackTransport`(한 기기 2인, 봇 연습), 테스트가 같은 클래스를 쓴다.
- **대안**: 서버 프로세스와 클라이언트에 각각 구현한다.
- **이유**: 처음부터 멀티플레이 전제. 한 기기 2인 시제품(GDD 16의 14단계 첫 걸음)이 서버 경기와 같은 코드 경로를 타면, 서버를 세우기 전에 넷코드의 대부분을 시험할 수 있다. 지연·지터·유실을 메모리 안에서 흉내 내는 테스트로 "섬 판정이 통신 없는 실행과 같다"(spec 10 15절의 통과 기준)를 자동으로 확인한다.

## T-20 HTTP는 JSON, 두 코덱을 픽스처로 묶는다 [신규]

- **결정**: 요청·응답은 JSON(snake_case). DTO는 주석 없는 `get; set;` 클래스로 `Wizard.Protocol`에 둔다. 서버는 System.Text.Json, 클라이언트는 Newtonsoft.Json이 `IJsonCodec`을 구현한다. `tests/fixtures/protocol/`의 JSON을 양쪽 코덱이 왕복시키는 테스트를 둔다.
- **대안**: (가) 양쪽 다 System.Text.Json. (나) MessagePack·Protobuf 같은 이진 형식. (다) 직접 만든 JSON 코덱.
- **이유**: System.Text.Json은 Unity에 기본으로 없고 IL2CPP에서 다루기 번거롭다. Newtonsoft는 Unity가 패키지로 제공한다. 요청·응답은 빈도가 낮아 이진 형식의 이득이 없고, JSON은 `request.result` 저장과 디버깅에 그대로 쓰인다. 두 코덱의 차이(열거형, 날짜, null)는 픽스처 테스트가 막는다.
- **따라오는 일**: 부호 없는 64비트(씨앗, 상태 해시)는 hex 문자열로 싣는다. 빈도가 높은 것(광장 스냅숏, 결투)은 이진이다.

## T-21 결투 UDP는 직접 만든 이진 + ChaCha20-Poly1305 [신규]

- **결정**: 결투 패킷은 `ByteWriter`로 직접 짠 LE 이진이다(`CONTRACTS.md` 10절). 암호는 ChaCha20-Poly1305, 키는 (경기, 사람, epoch)마다 다르고 nonce는 방향 바이트와 패킷 번호다. 같은 키에서 패킷 번호는 되돌아가지 않는다. 재접속은 새 키(epoch + 1)를 받는다(T-45). 매치 성립, 설정과 키 전달, 결과, 상대 끊김, 상태 전체 전송, 관전은 웹소켓으로 한다. UDP가 막히면 같은 바이트를 웹소켓 이진 프레임에 싣는다.
- **대안**: (가) 네트워크 라이브러리(ENet, LiteNetLib 등)의 신뢰 채널. (나) 상태 전체와 관전도 UDP 조각으로. (다) 두 사람이 키 하나를 같이 쓴다.
- **이유**: 필요한 것은 "최근 8틱을 겹쳐 보내는 비신뢰 채널" 하나뿐이라 라이브러리의 재전송·순서 보장이 오히려 방해다(늦은 입력은 버려야 한다). 크고 드물고 반드시 가야 하는 자료는 이미 있는 웹소켓이 맞다. 키를 사람마다 달리하면 한쪽이 상대의 패킷을 지어낼 수 없다.
- **따라오는 일**: 클라이언트의 AEAD는 BouncyCastle(`netstandard2.1`에 클래스가 없다), 서버는 런타임 구현이 있으면 그것을 쓴다. 둘을 RFC 8439 벡터로 맞춘다.
- **대신하는 [임시]**: spec 10 14.3의 직렬화, 14.9의 관전 시작, 14.10의 재전송 차단과 키 배포.

## T-22 `wizard-api` ↔ `wizard-duel`은 유닉스 도메인 소켓 HTTP [신규]

- **결정**: 경기 생성과 결과 보고는 유닉스 도메인 소켓 위의 HTTP(JSON)로 한다. 결과 보고는 `match_id`로 멱등이다. `wizard-duel`은 DB에 접속하지 않는다. 2초마다 생존 신호를 보내고, 끊기면 `wizard-api`가 진행 중 경기를 무효로 정리한다.
- **대안**: (가) PostgreSQL `LISTEN/NOTIFY`와 표를 버스로 쓴다. (나) 결투를 `wizard-api` 프로세스 안에서 돌린다.
- **이유**: (가)는 경기 키를 DB에 적어야 하고 결투 프로세스에 DB 권한이 생긴다. (나)는 가비지 수집과 요청 폭주가 60틱 루프를 흔든다(spec 12의 프로세스 분리와 어긋난다). 유닉스 소켓은 포트를 열지 않고 리눅스로 옮겨도 그대로다.

## T-23 검증 워커는 재실행 facts만 적고, 비교·확정·회수는 `wizard-api`가 한다 [신규] (개정)

- **결정**: 무엇과 무엇을 비교하는지를 고정한다.
  1. `finish` 때 `wizard-api`가 클라이언트의 `RunResultFacts` 바이트를 `run.claimed_facts`에, 그 SHA-256을 `run.claimed_digest`에 저장한다. 정산은 이 바이트의 필드와 `bag_selection`(`wild_blocks_final`의 부분집합인지 검사)만 입력으로 쓴다. **정산에 쓰이는 모든 필드는 이 바이트 안에 있다**(웨이브별 내역, 골드 내역, 드랍 기회, 귀환 가방 후보, 허상 결과, 해금, 중간 지점).
  2. `wizard-verify`는 `run.setup_canonical`과 기록 본문으로 재실행하고, 결과를 같은 정규 형식으로 써서 `verify_queue.result`(상태, 다이제스트)와 `result_facts`(바이트)에 적기만 한다. 워커는 주장 값을 읽지 않고 "차이"를 계산하지 않는다.
  3. `wizard-api`의 백그라운드 작업이 **바이트 동등**으로 일치를 정하고, 서버가 판 도중에 받아 둔 `run_checkpoint`의 `(wave, tick, state_hash)` 전부를 재실행 값과 대조한다. 보상 확정, 회수, 갚을 것, 순위 반영은 `Wizard.Server.App`의 트랜잭션으로 한다.
  4. 판정은 셋이다: `match`, `mismatch`(회수), `unverifiable`(코어 예외, 시간 초과, 팩 없음, 시도 횟수 초과 — 보상은 대기 그대로, 운영자 검토). 구현 결함이 보상 회수로 흘러가지 않게 한다.
- **대안**: 워커가 직접 확정·회수 트랜잭션을 돌린다(spec 09 9절의 의사코드 모양).
- **이유**: 워커는 코어 버전별로 여러 벌이 떠 있다(T-18). 정산 로직이 워커에 들어 있으면 규칙을 고칠 때마다 옛 버전 워커까지 다시 빌드해야 하고, 버전마다 정산이 달라질 수 있다. 재화를 건드리는 코드는 한 프로세스에만 둔다. 비교 대상을 `score`, `gold` 열로만 두면 귀환 가방 후보(가방 → 서고 → 거래 가능한 영웅 블록), 복수 보상, 해금이 검증 밖으로 빠져 위조 지급이 된다. 중간 지점이 구속력이 없으면 5웨이브 단위의 다시 하기를 막지 못한다.

## T-24 DB 접근은 Npgsql + Dapper + SQL 마이그레이션 [신규]

- **결정**: SQL을 직접 쓴다. 스키마는 `server/migrations/`의 번호 붙은 SQL 파일이고 `Wizard.Server.Data`의 실행기가 적용한다. 재화와 물건은 `ILedger`로만 바꾼다.
- **대안**: EF Core.
- **이유**: 명세가 요구하는 것이 `SELECT … FOR UPDATE`, 계정 번호 오름차순 잠금, `SKIP LOCKED` 대기열, 추가만 하는 원장, 서버 역할의 `UPDATE`/`DELETE` 회수다. 전부 SQL 한 줄 한 줄을 통제해야 하는 일이라 ORM이 만들어 주는 SQL은 방해다.

## T-25 `run.id`와 `duel_match.id`는 uuid [통합]

- **결정**: 서버가 UUID v7로 만든다. 기록 파일 머리말, 미제출 판 보관함, 내부 API의 멱등 키가 같은 16바이트를 쓴다.
- **대안**: spec 12의 `bigint` 자동 증가.
- **이유**: spec 09의 기록 파일 형식과 멱등 규칙이 16바이트 id를 전제로 한다. 순번 id는 판 수를 밖에 드러내고, 끊긴 채 끝난 판을 기기에 보관할 때 추측 가능한 키가 된다. v7은 시간순이라 색인에도 무리가 없다.

## T-26 `IBackend` / `IOnlineBackend` 분리, `Result<T>` [신규]

- **결정**: 두 프로필이 같이 가진 기능은 `IBackend`, 서버에만 있는 기능은 `IOnlineBackend : IBackend`에 둔다. `OfflineBackend`는 뒤의 것을 구현하지 않는다. 모든 호출은 `Task<Result<T>>`를 돌려주고 오류는 `ErrorCode`다.
- **대안**: 인터페이스 하나에 전부 넣고 오프라인은 `NotSupportedOffline`을 돌려준다. 실패를 예외로 던진다.
- **이유**: spec 08 8.3의 "지원 여부가 아니라 프로필 종류"를 타입으로 옮긴 것이다. 화면이 `Kind`를 보고 메뉴를 묶으면 오프라인에서 서버 기능을 부를 길이 컴파일 단계에서 없다. `OfflineBackend`에 통신과 결제 코드가 없다는 조건(spec 12 10.7)도 저절로 지켜진다. 접속 끊김은 예외적인 일이 아니라 늘 있는 일이라 값으로 다룬다.

## T-27 실수는 표시 계층과 서버의 레이팅에만 [신규]

- **결정**: `Core`, `Content`, `Sim`에는 실수형이 없다. `Rules`는 정수(만분율, 나노점)만 쓴다. `Fix64 → float` 변환은 코어 세 어셈블리 밖에서 `Wizard.ViewLogic.FixConvert`에만 있다(HUD 모델과 연출 한도가 엔진 없는 ViewLogic에 있기 때문이다. T-31). Unity 어셈블리는 `System.Numerics.Vector2`를 엔진 타입으로 옮기기만 한다. Glicko-2(결투 점수)는 `Wizard.Server.App`에서 `double`로 계산한다.
- **대안**: 코어에 `ToFloat()`를 두고 "틱 안에서는 쓰지 말 것"으로 약속한다.
- **이유**: 함수가 있으면 언젠가 쓰인다. 변환을 코어보다 위 계층(ViewLogic)에만 두면 코어에서 부를 수 없다(의존 방향이 거꾸로라 컴파일되지 않는다). 레이팅은 서버 한 곳에서만 계산하고 다른 기기와 맞출 필요가 없어서 실수가 문제가 되지 않는다.

## T-28 입력은 Input System 이벤트 시각으로 양자화 [신규]

- **결정**: Unity의 Input System 저수준 이벤트(`InputSystem.onEvent`)에서 물리 키와 이벤트 시각을 받는다. `InputQuantizer`가 시각을 `(틱, 오프셋 0~255)`로 바꾸고 정렬과 순번을 매긴다. 물리 키 → 논리 키 변환과 "논리 키 상태가 바뀔 때만 이벤트"는 `IInputSource` 구현이 맡는다.
- **대안**: `Update`에서 `Input.GetKeyDown`을 읽는다.
- **이유**: 프레임 단위로 읽으면 섬(0.07초 = 4.2틱) 판정에 프레임 간격(60Hz에서 16.7ms)이 그대로 오차로 들어간다(GDD 8 "프레임 단위로 읽지 않는다"). 물리 키 기준이라 한글 입력기의 영향을 받지 않는다. 빌드에서 확인한다(GDD 16의 1단계).

## T-29 에셋 없이 도는 표시 계층 [신규]

- **결정**: 표시 계층은 `IAssetCatalog`, `IFxPlayer`, `ISfxPlayer`와 이벤트 바인딩 표로 에셋을 가리킨다. 지금은 회색 도형 렌더러, 빈 재생기, 자리 표시 그림으로 돈다. 예고 구역의 모양과 채움은 도형으로 그린다(에셋이 필요 없다).
- **대안**: 에셋이 올 때까지 표시 코드를 미룬다.
- **이유**: 손맛 시제품(GDD 16의 1단계)이 회색 도형으로 시작한다. 식별자와 인터페이스가 먼저 있으면 에셋을 받는 날 바꾸는 것은 카탈로그와 바인딩 표뿐이다.

## T-30 결정론은 런타임 × 아키텍처 조합의 골든 테스트로 지킨다 [신규] (개정)

- **결정**: `tests/golden/`의 (씨앗, 입력 기록, 기대 해시)를 아래 조합이 각각 돌린다. 하나라도 다르면 실패다. 스냅숏 저장·복원 뒤의 해시, 같은 기록 두 번의 해시, 0 제수와 `MinValue` 경계의 산술 벡터도 같이 본다.

  | 런타임 | arm64 | x64 |
  |---|---|---|
  | dotnet(CoreCLR) | `dotnet test` | `dotnet test -a x64`(Rosetta 2, x64 런타임 설치 필요) |
  | Unity Mono | EditMode 테스트, macOS 플레이어 | macOS Intel 64-bit 플레이어를 Rosetta로 머리 없이 실행 |
  | Unity IL2CPP | macOS 플레이어(`mac-il2cpp` 모듈 필요) | macOS Intel 64-bit 플레이어를 Rosetta로 |

  서버는 arm64 맥미니이고 배포 대상 클라이언트는 x64가 많다. 0으로 나누기, `MinValue ÷ −1`, 스택 깊이처럼 아키텍처에 따라 갈리는 곳이 한 번도 비교되지 않으면, 처음 갈라지는 순간이 실제 플레이어의 보상 회수다. 실제로 배포하는 (OS, 아키텍처, 백엔드) 조합은 그 조합의 골든 통과가 배포 조건이다(T-39).
- **대안**: dotnet 테스트만 돌리고 Unity는 같은 소스이니 같다고 믿는다. arm64 한 아키텍처에서만 세 런타임을 돌린다(이 문서의 처음 결정).
- **지금 이 기기의 사실**: `mac-il2cpp` 모듈이 설치되어 있지 않다(Mono 변형만 있다). x64 dotnet 런타임도 없다. Rosetta 2는 있다. 모듈과 런타임 설치는 사용자가 한다(`PIPELINE.md` 4.1절). 골든 실행 스크립트는 필요한 것이 없으면 건너뛰지 않고 무엇이 없는지 적고 실패한다.
- **이유**: 소스는 같아도 컴파일러와 런타임이 다르다. 정수 연산은 같아야 하지만 "같아야 한다"를 확인하는 장치가 없으면 차이는 출시 뒤에 검증 불일치(무고한 플레이어의 보상 회수)로 나타난다.

## T-31 화면 로직은 엔진 없는 공유 패키지 `Wizard.ViewLogic`에 둔다 [신규]

- **결정**: 아홉 번째 공유 패키지 `src/Wizard.ViewLogic`을 둔다(`netstandard2.1`, C# 9, `noEngineReferences: true`). 여기에 있는 것: 화면 상태 기계와 뷰 모델(`ScreenStack` 포함), HUD 모델, 알림 라우터, 표시 이벤트 바인딩 표와 `UiEventKeys`, `PresentationEventRouter`, 연출 한도(번쩍·흔들림·입자 예산) 계산, 에셋 매니페스트 로더와 레지스트리, 표시 계약의 엔진 무관 타입(`IAssetCatalog`, `IFxPlayer`, `ISfxPlayer`, `FxRequest`, `AssetKey`), `FixConvert`. 의존은 `Core`, `Content`, `Text`, `Sim`, `Rules`, `Protocol`, `Netcode`, `Client`다. 코어 버전 해시에 들어가지 않고 실수형을 쓸 수 있다. `Wizard.Unity.UI`와 `Wizard.Unity.Presentation`은 이것을 그리고 입력을 넘기기만 하는 얇은 층이다.
- **대안**: (가) 화면 상태 기계와 뷰 모델을 `Wizard.Unity.UI` 안에 둔다(이 문서 묶음의 처음 구성). (나) `Wizard.Client`에 넣는다.
- **이유**: spec 13의 테스트 벡터 243개와 spec 14의 화면·에셋 쪽 벡터를 `dotnet test`로 돌리려는 것이다. (가)처럼 Unity 어셈블리 안에 두면 검증마다 편집기를 배치 모드로 띄워야 하고, 작업 트리마다 `Library`가 따로 생겨 병렬 작업이 무거워진다. 화면 규칙이 엔진 밖에 있으면 "뷰에 규칙이 없다"를 컴파일러가 지켜 준다(`noEngineReferences`라 `UnityEngine`을 쓰는 순간 컴파일이 깨진다). (나)는 통신·저장·세션을 맡는 `Client`에 화면 상태가 섞인다. `Client`는 머리 없는 클라이언트(`tools/Wizard.LoadBot`)도 참조하는 층이다.
- **따라오는 일**:
  - 표시 계약의 타입에 엔진 타입을 쓰지 못한다. 좌표는 `System.Numerics.Vector2`, 그림과 소리는 불투명 핸들(`SpriteRef`, `ClipRef`)이고 Unity 쪽 구현이 실제 자산을 쥔다(`CONTRACTS.md` 13절).
  - `Fix64 → float` 변환(`FixConvert`)의 자리가 `Wizard.Unity.Presentation`에서 여기로 옮겨졌다(T-27). Unity 어셈블리는 `UnityConvert`로 엔진 타입에 옮기기만 한다.
  - 표시용 난수는 여기서도 코어 난수기를 쓰지 않는다. 코어 금지 목록(실수형 등)의 분석기 진단은 이 어셈블리에 걸리지 않는다. 공유 코드의 문법 제한(C# 9)은 걸린다.
  - `ARCHITECTURE.md` 2절·3.1절(공유 어셈블리 9개)·3.2절(의존 그림)·10.5절(모듈), 테스트 프로젝트 `tests/Wizard.ViewLogic.Tests`. 서버 프로젝트는 이 어셈블리를 참조하지 않는다.

## T-32 화면 뷰는 uGUI로, 코드로만 만든다 [신규]

- **결정**: `unity/Assets/Game/UI`의 뷰는 uGUI 위젯을 **코드로** 만든다. 에셋을 들여오기 전까지 프리팹과 장면 편집을 쓰지 않는다. 장면 넷(`Boot`, `Menu`, `Battle`, `Plaza`)에는 시작 개체 하나만 두고 나머지는 코드가 만든다. 뷰는 `Wizard.ViewLogic`의 모델을 읽어 그리고 조작을 모델에 넘기기만 한다. 그림은 위젯의 겉(스킨)만 바꿔 꽂는다.
- **대안**: (가) 화면마다 프리팹을 만들고 장면에 배치한다. (나) UI Toolkit(UXML·USS).
- **이유**: 장면과 프리팹 파일은 병합 충돌이 나면 풀기 어렵다. 여러 작업 트리가 화면을 같이 만들기 때문에 충돌이 생기지 않는 형식이어야 한다. 에셋이 없는 지금은 코드로 만든 자리 표시 위젯이 가장 단순하고, 화면 하나가 폴더 하나(`UI/Screens/<화면>/`)라서 소유 경로로 자를 수 있다. (나)는 배치와 모양이 UXML·USS 자산 파일로 갈라져 같은 이득이 줄어든다. 저장소 뼈대의 `Wizard.Unity.UI` asmdef도 uGUI(`UnityEngine.UI`)를 참조한다.
- **따라오는 일**: 공용 위젯(판, 버튼, 탭, 목록, 칸, 글자, 선택 괄호)과 `ScreenHost`는 M4-UE-01이 만든다. 장면 파일, `ProjectSettings/`, `Packages/manifest.json`은 unity-engineer의 작업만 고친다(`PIPELINE.md` 4.5절). 에셋 반입 뒤에 프리팹을 쓸지는 아직 정하지 않았다. 그때 결정으로 남긴다. 그 전까지는 이 결정이 선다.

## T-33 뼈대를 먼저 만들고, 모듈은 부분 클래스의 훅과 모듈 등록으로 붙는다 [신규]

- **결정**: 함께 고칠 수밖에 없는 것을 M0~M1에서 한 번에 선언한다: 프로젝트 전부(실행 파일의 빈 `Program.cs` 포함), id 열거형 전부, `SimEventType` 전 구간, `ErrorCode`, 계약 인터페이스. `SimWorld`와 `ContentDb`는 부분 클래스이고, 틱 단계·상태 절·팩 절마다 부분 메서드 훅을 미리 선언해 둔다. 모듈은 자기 폴더의 파일(`SimWorld.<모듈>.cs`, `ContentDb.<절>.cs`)에서 훅을 구현하기만 한다. **훅은 (단계, 모듈)마다 따로 선언한다**(목록은 `CONTRACTS.md` 3.2절의 표). 서버의 엔드포인트·서비스·웹소켓 처리기와 DataBuild의 변환기는 모듈 인터페이스를 구현한 클래스를 어셈블리에서 찾아 등록한다.
- **대안**: (가) 모듈 작업이 필요할 때마다 열거형, 틱 파이프라인, `Program.cs`, 솔루션 파일을 고친다. (나) 코어의 모듈을 인터페이스 목록으로 실행 중에 등록한다. (다) 단계마다 훅 하나를 두고 여러 모듈이 같이 구현한다.
- **이유**: (가)는 여러 에이전트가 같은 파일을 동시에 고치게 한다. 뼈대가 먼저 있으면 뒤 작업은 새 파일만 더한다. (나)는 틱 안의 호출 순서가 등록 순서에 숨는다. 코어의 순서는 `Step` 한 곳에 글로 보여야 한다(`CONTRACTS.md` 3.2절의 0~12단계가 유일한 순서다). 부분 메서드는 구현이 없으면 호출이 컴파일에서 사라지므로, 빈 훅만 있는 상태로도 컴파일되고 아직 없는 모듈의 비용이 없다. (다)는 C#에서 되지 않는다. 부분 메서드는 구현이 하나뿐이어야 한다. 서버와 DataBuild는 등록 순서가 결과를 바꾸지 않는 곳이라 어셈블리에서 찾는 방식을 쓴다.
- **따라오는 일**:
  - `SimWorld.<모듈>.cs`와 `ContentDb.<절>.cs`는 모듈 폴더에 두되 네임스페이스가 `Wizard.Sim`, `Wizard.Content`다. `src/Wizard.Core/Errors/`(네임스페이스 `Wizard.Core`)와 함께 "네임스페이스 = 폴더"의 허용된 예외 셋이고 분석기가 이 세 꼴만 허용한다(M0-BT-02).
  - 훅을 더하거나 한 단계 안의 호출 순서를 바꾸는 것은 계약 변경이다(`PIPELINE.md` 3.7절).
  - 선언하는 작업: 프로젝트와 `Program.cs`는 M0-BT-01, 열거형은 M1-CONT-01, `ContentDb`의 접근자와 절 목차·DataBuild 뼈대는 M1-CONT-02, `SimEventType`과 Sim 계약 타입은 M1-SIM-01, 틱 파이프라인과 훅은 M1-SIM-02, `ErrorCode`는 M1-CORE-05, 서버의 모듈 등록은 M10-API-01(엔드포인트·서비스)과 M12-API-01(웹소켓 처리기).
  - 뒤 작업은 `Program.cs`를 `owned_paths`에 가진 경우에만 고친다. `PIPELINE.md` 4.4절의 2번.

## T-34 Unity `.meta`의 GUID는 경로에서 계산한다 [신규]

- **결정**: `tools/Wizard.MetaGen`이 `src/`와 `unity/Assets/` 아래의 폴더·파일마다 `.meta`가 없으면 만든다. GUID는 저장소 기준 상대 경로의 MD5다(입력 바이트의 정확한 꼴은 `CONTRACTS.md` 11.5절). 같은 경로면 어느 작업 트리에서나 같은 GUID가 나온다. 이미 있는 `.meta`는 덮어쓰지 않는다. `metagen --check`는 빠진 `.meta`, 주인 없는 `.meta`, 중복 GUID를 찾고, `src/`와 `unity/Assets/Game/`에서는 GUID가 계산값과 같은지도 본다. Unity를 띄우는 모든 스크립트는 먼저 `metagen`을 돌린다.
- **대안**: (가) Unity가 만든 `.meta`를 그대로 커밋한다. (나) `.meta`를 커밋하지 않는다.
- **이유**: 에이전트는 Unity를 열지 않고 `src/`에 파일을 더한다. (가)는 작업 트리마다 Unity가 제각각 무작위 GUID를 만들어, 병합할 때 같은 파일의 `.meta`가 충돌한다. (나)는 기기마다 GUID가 달라져 GUID로 가리키는 참조(장면의 스크립트, 설정 자산)가 기기마다 어긋난다. 경로에서 계산하면 누가 먼저 만들어도 바이트가 같다. `src/`와 `unity/Assets/Game/`에서 계산값과 대조하는 것은 Unity가 먼저 만든 무작위 GUID가 커밋되어 굳는 것을 막으려는 것이다. 그 밖의 곳(Unity가 프로젝트 틀로 만든 설정 자산 등)은 이미 있는 값을 그대로 둔다.
- **따라오는 일**: `metagen --check`는 병합 게이트에 있다. MetaGen은 확장자별 가져오기 블록을 Unity가 쓰는 형식 그대로 써서 Unity가 연 뒤에도 `.meta`가 바뀌지 않게 한다(M0-BT-04). `.meta`는 "MetaGen이 만든 것만" 더할 수 있는 공유 파일이다(`PIPELINE.md` 4.4절). 경로가 GUID를 정하므로 `src/`와 `unity/Assets/Game/`의 파일을 옮기면 GUID가 바뀐다. `ARCHITECTURE.md` 4.2절, `PIPELINE.md` 4.3·4.5절.

## T-35 이름 표는 병합되는 형식으로 고정한다 [신규]

- **결정**: `data/names/names.ko.json`은 열쇠 오름차순, 한 줄에 열쇠 하나, 마지막 줄은 표지 열쇠 `"~end": ""`다. git 병합은 union(`.gitattributes`)이고, 병합 뒤 `databuild names --fix`가 정렬과 중복을 본다. `databuild names --check`가 형식(정렬, 중복 없음, 한 줄에 열쇠 하나, 끝 표지)을 검사한다. 작업은 자기 영역의 열쇠만 더한다.
- **대안**: (가) 영역마다 이름 파일을 나눈다. (나) 형식을 정하지 않고 충돌은 병합 때 푼다.
- **이유**: 이름은 한 곳에만 둔다(DECISIONS 1.25). 그래서 이 파일은 하나뿐이고 여러 작업이 줄을 더한다. (가)는 "한 곳"을 깨고, 열쇠가 어느 파일에 있는지를 또 정해야 한다. 형식을 고정하면 덧붙이기끼리는 충돌하지 않는다. 끝 표지가 있으면 실제 항목의 줄이 모두 쉼표로 끝나서, 줄을 더하는 변경이 이웃 줄을 건드리지 않는다. union 병합은 양쪽 줄을 다 살리므로 순서가 흐트러지거나 같은 열쇠가 두 번 남을 수 있다. 그것을 `names --fix`와 `--check`가 잡는다.
- **따라오는 일**: 파일의 뼈대와 두 명령은 M1-CONT-02, `.gitattributes`의 union 줄은 M0-BT-01, 커밋 훅의 형식 검사는 `PIPELINE.md` 4.6절. 실행 때 읽는 것은 이 JSON이 아니라 DataBuild가 만든 이진 절 `names.ko.wznm`이다. 정렬은 서수 비교이고 표지 열쇠는 이진 절에 싣지 않는다(`CONTRACTS.md` 5절). `ARCHITECTURE.md` 8.3절, `PIPELINE.md` 3.8·4.4절.

## T-36 메일은 HTTPS API 방식의 `ApiMailSender` + 사업자 어댑터 [신규]

- **결정**: 실제 메일 발송은 외부 메일 사업자의 HTTPS API를 부르는 `ApiMailSender` 하나로 한다. 사업자마다 다른 부분(요청 만들기, 응답 해석, 배달·반송 사건 읽기)은 `IMailApiAdapter`로 떼고, 재시도·하루 한도·반송 처리·`mail_outbox` 상태 전이는 공통 코드에 둔다. 개발과 테스트는 `NullMailSender`와 가짜 어댑터로 돈다. **출시 구성은 `recovery_mail_enabled = true`다**(작업 M10-ACC-05, 게이트 M18-INT).
- **대안**: (가) 맥미니에서 SMTP 서버를 직접 돌린다. (나) 사업자의 SMTP 릴레이를 쓴다. (다) 메일을 붙이지 않고 출시한다.
- **이유**: DECISIONS 1.24(비밀번호를 잃으면 이메일로 복구)는 확정 사항이라 (다)는 될 수 없다. 가정 회선의 맥미니에서 직접 보내는 메일은 대부분 스팸으로 떨어진다(역방향 DNS와 발신 평판이 없다). API 방식은 배달·반송 사건을 받아 `mail_outbox`를 닫을 수 있고 포트 25가 필요 없다.
- **따라오는 일**: 사업자의 계정과 API 키 발급, 발신 도메인의 SPF·DKIM 설정은 사용자가 한다(`human_check`). 어느 사업자를 쓸지는 `OPEN-QUESTIONS.md`에 올린다. 어댑터가 얇아서 사업자가 정해지면 어댑터 하나만 더한다.

## T-37 외부 라이브러리 [신규]

- **결정**:

  | 쓰임 | 라이브러리 | 쓰는 곳 |
  |---|---|---|
  | Ed25519(상태 파일 서명·검증) | BouncyCastle.Cryptography 2.x | `Wizard.Client`(검증), `tools/Wizard.StatusSign`(서명) |
  | ChaCha20-Poly1305 | 클라이언트는 BouncyCastle.Cryptography, 서버는 런타임의 `ChaCha20Poly1305` | `Wizard.Client`, `Wizard.Server.Duel` |
  | Argon2id(64MiB, 3회, 병렬 1) | Konscious.Security.Cryptography.Argon2 | `Wizard.Server.App`. 동시에 도는 해시 수를 세마포어로 묶는다(해시 하나가 64MiB를 쓴다) |
  | JSON Schema 검사 | JsonSchema.Net | `tools/Wizard.DataBuild` |
  | JSON(클라이언트, 테스트 공유부) | Newtonsoft.Json 13.0.x(Unity 패키지 `com.unity.nuget.newtonsoft-json`과 같은 판) | `Wizard.Client`, `Wizard.TestKit.Shared` |
  | DB | Npgsql, Dapper | `Wizard.Server.Data` |
  | 분석기 | Microsoft.CodeAnalysis.CSharp 4.3.x(Unity의 Roslyn 4.3.1에 맞춘다) | `tools/Wizard.Analyzers` |

  정확한 판 번호는 M0-BT-01이 그때의 안정판으로 `Directory.Packages.props`에 고정한다. 코어 세 어셈블리와 `Rules`, `Protocol`, `Netcode`, `Text`에는 외부 의존이 없다. `ViewLogic`은 패키지를 직접 참조하지 않는다(`Client`를 참조할 뿐이다).
- **대안**: Ed25519와 AEAD를 직접 구현한다. Argon2를 네이티브 라이브러리로 쓴다.
- **이유**: 이 기기의 .NET 10에는 `ChaCha20Poly1305`는 있지만 Ed25519와 Argon2가 없다. 암호를 직접 구현하지 않는다. 네이티브 라이브러리는 배포물과 Unity 플러그인 구성을 늘린다. 한 벌(BouncyCastle)로 클라이언트와 도구를 맞추면 RFC 벡터 한 묶음으로 둘을 검증한다.
- **따라오는 일**: 패키지 참조 줄과 Unity 쪽 DLL(`unity/Assets/Plugins/`), asmdef의 `overrideReferences`·`precompiledReferences`, `com.wizard.client`의 `package.json` 의존은 M0-BT-01이 한꺼번에 넣는다. 뒤 작업이 csproj를 고칠 일이 없게 한다(`PIPELINE.md` 4.4절).

## T-38 테스트용 PostgreSQL은 Docker의 고정 이미지 [신규]

- **결정**: 서버 통합 테스트의 DB는 `tools/ci/pg-test.sh`가 Docker로 띄운다(`postgres:17`의 고정 태그, 작업 트리마다 빈 포트, 테스트마다 임시 데이터베이스). Docker가 돌고 있지 않으면 `ci.sh full`은 **건너뛰지 않고** 무엇이 없는지 적고 실패한다.
- **대안**: Homebrew로 `postgresql@17`을 깔고 사용자 영역에서 `initdb`한다. Testcontainers for .NET.
- **이유**: 이 기기에는 `psql`도 `pg_ctl`도 없지만 Docker는 돌고 있다. 설치 없이 지금 된다. 판 번호가 이미지 태그로 고정된다. "테스트 건너뛰기 금지"와 "깨끗한 복제본에서 full 통과"를 같이 지키려면 DB가 없을 때의 동작이 정해져 있어야 한다.
- **따라오는 일**: 배치용 PostgreSQL(맥미니에 직접 설치)은 여전히 사용자가 깐다. 테스트용과 배치용의 주 판 번호를 맞춘다.

## T-39 배포 대상마다 골든이 배포 조건. Windows IL2CPP는 Windows 기기에서만 [신규]

- **결정**: 배포하는 모든 (OS, 아키텍처, 백엔드) 조합은 그 조합에서 골든을 통과해야 배포할 수 있다. macOS(arm64, x64)는 이 기기에서 만든다. Windows는 두 길이 있고 **기본은 (가)** 다(사용자가 (나)로 바꿀 수 있다): (가) 이 맥에서 `windows-mono` 모듈로 Mono 백엔드 플레이어를 만들고 Windows 기기에서 골든 실행기를 돌린 결과를 게이트에 붙인다. (나) Windows 기기(또는 가상 머신)에 Unity와 Visual Studio C++ 도구를 두고 IL2CPP로 빌드해 그 자리에서 골든을 돌린다.
- **대안**: "Windows 모듈을 깔면 같은 스크립트로 만든다"(이 문서 묶음의 처음 서술).
- **이유**: 처음 서술은 틀렸다. 이 맥의 Unity가 제공하는 Windows 모듈은 `windows-mono`뿐이고 `windows-il2cpp`는 목록에 없다. Windows IL2CPP 플레이어는 Windows에서만 만들 수 있다. 어느 길이든 골든은 Windows 실기에서 돌려야 한다.
- **(가)를 기본으로 둔 이유**: (가)는 Windows 기기가 골든을 **돌리기만** 하면 된다(Unity와 C++ 도구를 깔 필요가 없다). 빌드 스크립트가 이 맥 한 곳에 남는다. 백엔드가 플랫폼마다 달라도(macOS는 IL2CPP, Windows는 Mono) 조합별 골든이 같으면 결과는 같다.
- **따라오는 일**: (나)로 바꿀지와 Windows 기기의 준비는 `OPEN-QUESTIONS.md`에 올린다. 골든 실행기는 결과를 파일(조합 이름, 골든별 해시, 통과 여부)로 내서 다른 기기의 결과를 게이트에 붙일 수 있게 한다.

## T-40 CI의 로직은 C# 도구, 작업 상태는 추적하지 않는 상태 파일 [신규]

- **결정**: (1) `tools/ci/`의 셸 스크립트는 `#!/bin/bash`와 bash 3.2 문법만 쓰는 얇은 껍데기이고, 로직(단계 실행, 시간 제한, 잠금, 변경 범위 판단)은 `tools/Wizard.Ci`(C#)에 둔다. (2) 작업의 상태(`in_progress`, `review`, `done`)는 `tasks.json`이 아니라 git이 추적하지 않는 `.claude/taskstate.json`에 두고 `taskgraph`가 파일 잠금을 잡고 고친다. `tasks.json`은 구조(정의)만 담고 tech-lead의 작업만 커밋한다. `tasks.json`의 `status`는 처음 분류(`todo`, `blocked-on-assets`)만 뜻한다. (3) 상태 파일을 잃으면 `taskgraph reconcile`이 main의 커밋 기록(`<type>(<작업 id>): …`)에서 `done`을 다시 만든다.
- **대안**: 전부 bash로 쓴다. 상태를 `tasks.json`에 둔 채 쓴다.
- **이유**: 이 맥의 `/bin/bash`는 3.2.57이라 연관 배열, `mapfile`, `${x,,}`가 없고 `timeout`과 `flock`도 없다. Unity 배치 실행이 멈췄을 때 끊을 시간 제한과, 여러 작업 트리가 같은 상태를 고칠 때의 잠금이 필요하다. 상태를 추적 파일에 두면 main 체크아웃에 커밋되지 않은 수정이 늘 남아 통합 담당의 옮기기·합치기, tech-lead의 작업 추가와 같은 파일에서 부딪친다.

## T-41 다시 만드는 파일과 절 꼬리표 해시 [신규]

- **결정**: (1) "다시 만드는 파일" 부류를 둔다: `src/*/Generated/*.g.cs`와 `tests/golden/**/expected*.json`. 어느 작업이든 `owned_paths`에 없어도, 그 파일이 **도구의 출력과 바이트가 같으면** 바꿀 수 있다(`taskgraph check-diff`가 다시 만들어 대조한다). 병합 때는 통합 담당이 main 위에서 다시 만든다. (2) 골든의 기대 해시를 다시 만든 작업은 `tests/golden/CHANGELOG.md`에 한 줄(작업 id, 어느 골든, 왜 바뀌는가)을 더하고 결과 보고에 적는다. 검토자는 "해시가 바뀔 이유가 있는 변경인가"를 본다. 통합 빌드가 모든 조합에서 다시 확인한다. (3) 상태 직렬화와 해시는 절마다 `(절 id, 내용)`으로 쓰고, 개체 풀·목록만으로 이루어진 절은 개수가 0이면 통째로 생략한다.
- **대안**: `SimVersion.g.cs`와 골든을 한 작업만 가진다(이 문서 묶음의 처음 구성). 버전 값은 병합 때만 다시 만든다.
- **이유**: 처음 구성에서는 코어의 `.cs`를 고치는 모든 작업이 `SimVersion.g.cs`를 갱신하면 소유 위반, 갱신하지 않으면 생성 파일 불일치로 반드시 실패한다. 상태 절을 더하는 작업은 옛 골든의 해시를 바꾸는데 그 폴더를 가진 작업이 따로 있었다. 절 생략 규칙은 풀 모듈을 더하는 것만으로는 그 모듈을 쓰지 않는 골든이 바뀌지 않게 해서 다시 만드는 일을 줄인다.
- **따라오는 일**: 골든을 다시 만드는 일이 쉬워진 만큼 "버그로 해시가 바뀐 것을 다시 만들어 덮는" 위험이 생긴다. 이유 기록과 검토 항목, 조합 전체의 재확인으로 막는다.

## T-42 코어의 금지 목록 확장 [신규]

- **결정**: `Core`/`Content`/`Sim`에서 다음을 더 막는다(분석기, 억제도 오류): (1) 가변 정적 필드(`const`와 불변 `static readonly`만. 정적 배열은 생성 표의 허용 목록에 있는 것만), (2) `[ThreadStatic]`, `unsafe`, `BitConverter`, `MemoryMarshal`, `Unsafe`, `Buffer.BlockCopy`, `Marshal`, (3) 상수가 아닌 제수의 원시 `/`·`%`, (4) `checked`. `Rules`에는 실수형 금지를, `Netcode`에는 시계(`DateTime`, `Stopwatch`)와 소켓 금지를 진단으로 건다. 분석기는 생성 코드(`*.g.cs`)도 검사한다(`ConfigureGeneratedCodeAnalysis`). `.editorconfig`의 `dotnet_diagnostic.WZ*.severity`, csproj의 `NoWarn`·`WarningsNotAsErrors`로 진단을 끄는 것도 CI가 거절한다.
- **대안**: `Thread`/`Task`만 막는다(처음 구성).
- **이유**: "`Step`에서 할당 금지"는 구현자를 정적 임시 버퍼로 민다. `wizard-duel`과 `wizard-verify`는 여러 세계를 여러 스레드에서 동시에 돌리므로 세계끼리 버퍼가 섞여 **서버에서만** 다른 결과가 난다. 단일 스레드 테스트와 골든으로는 잡히지 않는다. 구조체 메모리를 그대로 쓰는 직렬화는 패딩, `bool`의 표현, 엔디언에 기댄다.
- **따라오는 일**: "세계 16개를 16스레드에서 같은 기록으로 동시에 돌려 해시가 단일 실행과 같다"를 결정론 묶음에 둔다(M1-TE-01).

## T-43 솔루션은 `.sln`, 분석기는 `netstandard2.0`, Unity 쪽은 최상위 빌드 설정을 끊는다 [신규]

- **결정**: (1) 솔루션 파일은 `Wizard.sln`이다(`dotnet new sln --format sln`. .NET 10 SDK의 기본은 `.slnx`다). (2) `global.json`은 `rollForward: latestPatch`. (3) `tools/Wizard.Analyzers`만 `netstandard2.0`이다(컴파일러 확장의 요구. `net10.0`이면 RS1041 경고가 오류가 되고 IDE에서 올라오지 않는다). 분석기를 공유·서버 프로젝트에 거는 줄(`ProjectReference OutputItemType="Analyzer"`)은 M0-BT-01이 최상위 `Directory.Build.props`에 넣는다. (4) `unity/Directory.Build.props`에 빈 `<Project/>`를 두어 Unity가 IDE용으로 만드는 csproj가 최상위 설정을 물려받지 않게 하고, `unity/*.csproj`·`unity/*.sln`을 무시한다. 프로젝트 파일을 훑는 도구의 범위는 `src/`, `server/`, `tests/`, `tools/`다. (5) `.gitignore`의 Unity 규칙은 `/unity/Library/`처럼 위치를 고정한다(지금의 `[Bb]uild/`, `[Ll]ogs/`는 저장소 어디의 같은 이름 폴더든 무시한다). (6) 실행 파일 프로젝트의 빈 `Program.cs`는 M0-BT-01이 만든다(없으면 CS5001). (7) 가짜 결제 제공자는 `Wizard.Server.Tests`와 개발용 호스트 `Wizard.Server.Api.DevHost`만 참조한다. `Wizard.Server.Api`는 참조하지 않으므로 출시 배포물에 그 DLL이 없다.
- **대안**: `.slnx`. 분석기를 `net10.0`으로. `Api`가 구성에 따라 가짜 제공자를 등록한다.
- **이유**: 전부 "M0의 첫 빌드가 실제로 통과하는가"에 걸린 사실들이다.

## T-44 IL2CPP 코드 제거는 `link.xml`로 막고 플레이어에서 왕복 테스트를 돌린다 [신규]

- **결정**: `unity/Assets/link.xml`로 `Wizard.Protocol`, `Wizard.Client`, `BouncyCastle.Cryptography`, `Newtonsoft.Json`을 보존한다. Managed Stripping Level은 Minimal로 고정한다. 프로토콜 JSON 픽스처의 왕복 테스트와 AEAD의 RFC 벡터를 IL2CPP 플레이어의 머리 없는 실행에서도 돌린다(골든 실행기와 같은 틀).
- **대안**: DTO마다 `[Preserve]`를 붙인다.
- **이유**: `Wizard.Client`는 `noEngineReferences`라 `UnityEngine.Scripting.Preserve`를 쓸 수 없다. Newtonsoft는 리플렉션으로 DTO를 읽는다. Mono에서는 통과하고 IL2CPP 플레이어에서만 역직렬화가 null이나 예외로 깨지는 일은 출시 직전에야 드러난다.

## T-45 서버 주도의 왕복 측정, 결투 키의 epoch, 입력 검증 순서 [신규]

- **결정**: (1) 결투의 왕복 지연은 서버가 직접 잰다(서버가 보낸 시각의 에코와 붙들어 둔 시간). 입력 지연 D는 서버가 정해 `HelloAck`로만 내려 주고, `DuelSetup`(시뮬레이션의 입력)에는 넣지 않는다. 자동 매치의 회선 조건은 대기열에 넣기 전에 `wizard-duel`과의 UDP 탐침으로 서버가 확인한다. (2) 결투 키는 (경기, 사람, epoch)마다 새로 만들고 같은 키에서 패킷 번호는 되돌아가지 않는다. 재접속은 `duel.rejoin`으로 새 키를 받는다. 응답 주소는 태그 검증을 통과한 마지막 패킷의 출발 주소다. (3) 서버의 입력 검증은 `CONTRACTS.md` 10.6절의 순서다: 봉인된 틱의 이벤트는 그 이벤트만 늦음으로 버리고, 건너뛴 순번은 유실로 소비하고, 패킷 전체를 버리는 것은 한 패킷 안의 역행뿐이다. (4) 클라이언트는 입력 시계(멈추지 않는다)와 예측 틱(확정 + 8 이하)을 가른다. 한도에 걸리면 멈추고 기다리며, 전체 상태는 해시 불일치와 긴 확정 공백에만 청한다. (5) 버려진 내 입력 뒤에는 확정 키 상태와 물리 키 상태의 차이를 새 순번의 보정 이벤트로 낸다.
- **대안**: spec 10 14.8절의 의사코드 그대로(봉인된 틱의 이벤트와 순번 건너뜀은 패킷 버림). 클라이언트가 잰 왕복으로 D를 정한다. 한도 초과 때 전체 상태를 받는다.
- **이유**: (1) 클라이언트 값에 기대면 D, 이른 시각 검사, 대기열 자격이 모두 자기 보고가 된다. (2) 같은 (키, nonce)의 재사용은 키스트림을 드러낸다. (3) 의사코드대로면 늦은 이벤트 하나가 뒤따르는 최대 8개 패킷을 통째로 버리게 하고, 133ms를 넘는 연속 유실 뒤에는 빠진 순번이 영영 오지 않아 이후 모든 패킷이 순서 위반이 된다. 30개가 쌓이면 정직한 플레이어가 몰수패한다. online 4.11의 표("늦은 입력은 그 이벤트만 버린다")와도 어긋난다. (4) online 4.11-6과 spec 10 TV-N22가 "멈추고 기다린다"다. 회선이 튈 때마다 전체 상태를 받으면 해시 불일치 통계가 단순 지연으로 오염된다. (5) 뗌이 버려지면 캐릭터가 계속 걷거나 쏜다.
- **따라오는 일**: spec 10 14.8절의 표·의사코드와 TV-N15·TV-N16을 이 규칙으로 고쳐 적었다(`REVIEW-LOG.md`). NetSim에 연속 유실(버스트) 모형과 "오프셋 균등 분포 입력의 폐기율" 측정을 더한다(M2-NET-06).

## T-46 `EndlessSetup`의 정규 바이트는 `.wzrc` 머리말 전체이고, 코어·데이터 버전은 `EndlessSetup`의 칸이다 [신규]

- **문제**: `CONTRACTS.md` 3.2절·11.1절은 "`EndlessSetup`의 정규 바이트 = `.wzrc` 머리말 그대로"라 하고 8.3절은 그 안에 core_version과 data_version이 있다고 적는데, 3.2절의 `EndlessSetup`에는 그 두 값을 담을 칸이 없고 `WriteCanonical(ByteWriter w)`에는 받을 매개변수가 없었다. 어느 구현도 요구된 바이트를 낼 수 없었다(M1-SIM-01의 계약 변경 요청).
- **결정**: (1) 정규 바이트는 머리말 **전체**다(magic, format_version, core_version, data_version, run_id … unlocked_arts). `run.setup_canonical`, `RunStartTicket.setup`, 기록 파일의 머리말이 같은 바이트다. (2) `EndlessSetup`에 `string CoreVersion`(소문자 hex 32자)과 `int DataVersion`을 더한다. 판의 봉투 값이고 세계는 읽지 않는다(상태와 해시에 들어가지 않는다). 맞는 코어·팩으로 돌리는지는 호출자가 본다. (3) 한 설정에 바이트 열은 하나다. `TryReadCanonical`은 다시 쓰면 같은 바이트가 되는 입력만 받는다. `WriteCanonical`은 형식에 담기지 않는 설정에 `InvalidOperationException`을 던지고 아무것도 쓰지 않는다. (4) 기록 파일의 앞머리를 읽는 `TryReadCanonicalPrefix(bytes, out setup, out length)`를 더한다. (5) `InputRecordHeader`는 `{ FormatVersion, Setup }`이다. core_version·data_version을 따로 들지 않는다(뼈대에 있던 두 칸을 뺐다. 계약에 적힌 적이 없는 칸이고 쓰는 코드가 없었다). `InputRecordCodec.WriteHeader`는 `Setup.WriteCanonical`을 부른다.
- **대안**: (가) 정규 바이트를 run_id부터 unlocked_arts까지로 줄이고, 머리말을 "magic + format_version + core_version + data_version + 정규 바이트"로 다시 정의한다(버전은 `InputRecordHeader`에만). (나) `WriteCanonical(ByteWriter w, string coreVersion, uint dataVersion)`처럼 매개변수로 받는다. (다) `WriteCanonical`이 `SimVersion.CoreVersion` 상수를 쓴다.
- **이유**: (가)는 "정규 바이트 = 머리말 = `run.setup_canonical` = `setup`"이라는 등식을 깨서 3.2·8.3·11.1·12.1절과 ARCHITECTURE의 판 흐름, 그리고 그 등식을 수락 조건으로 가진 작업 여덟 개(M1-SIM-01, M1-SIM-07, M11-PRO-01, M11-RUN-01~03, M11-VER-01, M11-CLI-01)를 다 고쳐야 한다. 서버가 저장하는 바이트와 `EndlessSetup`이 읽는 바이트가 달라지면 "받은 바이트를 그대로 쓴다"가 "받은 바이트의 뒷부분을 쓴다"가 되어 잘못 자를 여지가 생긴다. (나)는 읽는 쪽에 값을 돌려줄 자리가 또 필요해 시그니처 둘이 같이 바뀌고, 버전을 설정과 따로 들고 다니다 어긋날 수 있다. (다)는 틀린다: `wizard-api`는 지금 버전의 코어 한 벌만 링크하지만 판의 코어 버전은 출발 요청의 값이다(T-18). 상수를 쓰면 서버가 자기 버전을 적는다. 칸을 더하는 것은 더하기만 하는 변경이고 이미 적힌 문장과 수락 조건을 하나도 바꾸지 않는다.
- **`DataVersion`이 `int`인 까닭**: 비교 상대인 `ContentPack.DataVersion`·`ContentDb.DataVersion`이 `int`다(2.3절). 파일의 칸은 `u32` 그대로이고, `int`를 넘는 값은 읽을 때 거절한다.
- **새 설정의 `CoreVersion`이 빈 문자열인 까닭**: 기본값을 `SimVersion.CoreVersion`으로 두면 서버가 요청의 값을 넣는 것을 잊어도 조용히 자기 버전이 적힌다. 비워 두면 `WriteCanonical`이 던진다.
- **따라오는 일**: (1) M1-SIM-07의 `InputRecordCodec`은 머리말을 직접 풀지 않고 `TryReadCanonicalPrefix`를 쓴다. (2) `SimWorld.CreateEndless`는 두 버전 칸을 읽지 않는다(M1-SIM-02). (3) M11-RUN-02는 `EndlessSetup.CoreVersion`에 요청의 `core_version`을 넣는다. (4) `DuelSetup.WriteCanonical`(WZDR 머리말)은 `DuelRoomOptions`의 칸이 아직 없어서 선언하지 않았다. 아래 "아직 정하지 않은 기술 항목"에 올렸다.

---

## 아직 정하지 않은 기술 항목

구현을 막지 않는다. 해당 단계에 닿았을 때 정한다.

| 항목 | 지금의 기본 | 닿는 단계 |
|---|---|---|
| Windows 빌드 | T-39. 기본은 이 맥에서 만든 Windows Mono 플레이어 + Windows 실기의 골든. IL2CPP로 바꾸려면 Windows 빌드 기기가 있어야 한다(`OPEN-QUESTIONS.md`) | 배포 빌드 |
| Argon2id 구현 라이브러리 | T-37에서 정했다 | — |
| 메일 사업자 | 구조는 T-36에서 정했다(HTTPS API + 어댑터, 작업 M10-ACC-05). 어느 사업자의 계정을 쓸지는 사용자가 정한다(`OPEN-QUESTIONS.md`). 개발은 `NullMailSender` | 서버 기반 |
| 로그 형식과 보관 | 구조화 JSON, 30일(spec 12 8.4). 라이브러리는 기본 `ILogger` | 서버 기반 |
| PostgreSQL, Caddy 설치 | 배치용은 맥미니에 아직 없다(사용자 설치). 테스트용 DB는 T-38 | 서버 기반 |
| 팩 내려받기 경로 | `wizard-api`가 정적 파일로 낸다 | 판 제출과 검증 |
| 에셋 반입 뒤 화면 뷰에 프리팹을 쓸지 | T-32. 반입 전까지는 코드로만 만든다. 반입 뒤의 방식은 정하지 않았다 | 에셋 반입(시각 작업) |
| WZDR 머리말(`DuelSetup`의 정규 바이트)의 칸 배치와 `DuelSetup.WriteCanonical`을 더하는 작업 | 계약은 "`DuelSetup`의 정규 바이트 + `input_delay u8 × 2`, magic `WZDR`"까지만 정했다(CONTRACTS 11.1절). `DuelRoomOptions`의 칸(spec 10 9.2절)이 코드에 아직 없어서 바이트를 정할 수 없다. 기본 방향은 T-46과 같다: 봉투 값(magic, format_version, core_version, data_version)을 앞에 두고 한 설정에 바이트 열 하나. `DuelSetup.cs`는 M1-SIM-01의 소유 폴더(`src/Wizard.Sim/Runtime/Contracts/`)에 있어서, 더하는 작업의 소유 경로를 `tasks.json`에서 정해야 한다 | M1-SIM-07의 WZDR 코덱 전(그 작업의 수락 조건 "WZDR 코덱과 `ReplayRunner.RunDuel`") |
| IL2CPP 골든 실행을 자동으로 돌리는 방법 | Unity 배치 모드로 플레이어를 빌드한 뒤(빌드 진입점은 `-quit`을 쓴다) 플레이어를 `-batchmode -nographics`로 실행. `mac-il2cpp` 모듈이 먼저 있어야 한다 | 손맛 시제품 뒤 |
