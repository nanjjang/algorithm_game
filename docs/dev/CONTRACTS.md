# 인터페이스 계약 — 엄청엄청 위대한 마법사 키우기

모듈 사이의 공개 인터페이스를 실제 C# 시그니처로 적는다. 구조와 흐름은 `ARCHITECTURE.md`, 결정의 이유는 `DECISIONS-TECH.md`에 있다. 규칙과 수치는 `docs/spec/`이 주인이고, 이 문서는 **타입, 시그니처, 형식, 순서**의 주인이다.

각 계약 머리에 `소유`(구현하는 모듈)와 `근거`(명세 절)를 적는다. "spec 01 4.3"은 `docs/spec/01-sim-core.md` 4.3절이다.

## 0. 공통 규약

### 0.1 코드 규약

- 공유 어셈블리 아홉(`Wizard.Core`~`Wizard.Client`, `Wizard.ViewLogic`)은 `netstandard2.1`, C# 9다. `record`, `init`, `record struct`, 파일 범위 네임스페이스, `Int128`, `System.Text.Json`을 쓰지 않는다(ARCHITECTURE 3.3절).
- 아래 코드 블록은 네임스페이스 블록을 생략하고 머리 주석에 네임스페이스를 적는다.
- `Wizard.Core`, `Wizard.Content`, `Wizard.Sim`에는 실수형이 없다. `Fix64`를 실수로 바꾸는 함수는 `Wizard.ViewLogic.FixConvert` 한 곳에만 둔다(13절). Unity 어셈블리는 그 결과(`System.Numerics.Vector2`, `float`)를 엔진 타입으로 옮기기만 한다.
- 코어 세 어셈블리에는 가변 정적 필드가 없다(`const`와 불변 `static readonly` 표만). 임시 버퍼는 세계나 호출자가 가진다. 서버는 여러 세계를 여러 스레드에서 동시에 돌린다.
- 바이트를 쓰고 읽는 길은 `ByteWriter`/`ByteReader`/`StateWriter`의 시프트 구현뿐이다. `BitConverter`, `MemoryMarshal`, `Unsafe`, `Buffer.BlockCopy`, `Marshal`, `unsafe`, `[ThreadStatic]`, `checked`를 코어 세 어셈블리에서 쓰지 않는다.
- 코어 세 어셈블리에서 상수가 아닌 제수의 원시 `/`와 `%`를 쓰지 않는다. `FixMath`와 `Wide`의 나눗셈 도우미만 쓴다(1.1절 계약).
- 코어 안의 컬렉션은 배열, `SortedIntList`, 고정 크기 풀만 쓴다. `Dictionary`는 조회 전용이고 순회하지 않는다.
- `Wizard.ViewLogic`은 엔진이 없는 아홉 번째 공유 어셈블리다(T-31). 실수형을 쓸 수 있고 코어 버전 해시에 들어가지 않는다. `UnityEngine` 타입을 쓰지 못하므로 좌표는 `System.Numerics.Vector2`다. 화면의 규칙과 상태 기계는 여기에 있고 Unity 쪽 뷰는 그리기와 입력 전달만 한다(13절).
- 계약의 선언은 뼈대 작업이 먼저 한다(T-33). `SimWorld`와 `ContentDb`는 부분 클래스이고 모듈은 자기 파일에서 훅을 구현한다(3.2절, 2.3절). 훅을 더하거나 호출 순서를 바꾸는 것은 계약 변경이다.
- 시간 단위: 틱 `uint`(60틱/초), 입력 시각 `long`(`틱 × 256 + 오프셋`, 1단위 = 1/15,360초), 서버 벽시계는 UTC `DateTime`(DB는 `timestamptz`).

### 0.2 오류 규약

| 층 | 규약 |
|---|---|
| `Wizard.Sim` | 게임 규칙상의 "안 됨"은 예외가 아니다. 무시하거나 결과 값으로 돌려준다(불발, 무효 입력). 상태가 깨졌을 때만 `SimInvariantException`(`Wizard.Core`)을 던지고 그 판은 중단한다. 제수 0과 `Step`의 전제 위반도 코드가 직접 검사해 같은 예외를 던진다(런타임의 검사에 기대지 않는다. 1.1절, 3.2절). 깨진 팩은 `ContentException` |
| `Wizard.Rules` | 순수 함수. 거절은 `RuleCheck`(`ErrorCode` 포함)로 돌려준다. 예외를 던지지 않는다 |
| `Wizard.Client` | 모든 비동기 호출은 `Task<Result<T>>`. 통신 실패도 `Result`의 오류로 온다(`ErrorCode.Network*`). 예외는 프로그래머 오류에만 |
| HTTP | 성공 200. 규칙상 거절 422 + 오류 본문(멱등 저장 대상). 형식 오류 400, 토큰 401, 제재·권한 403, 없음 404, 버전·멱등 충돌 409, 빌드가 낮음 426, 요청 제한 429(`Retry-After`), 점검 503 |
| 웹소켓 | 요청형 메시지의 거절은 같은 `c`(클라이언트 순번)를 단 `error` 메시지 |

```csharp
// namespace Wizard.Core
public enum ErrorCode : ushort {
    None = 0,
    // 1~99 통신·세션
    NetworkUnreachable = 1, NetworkTimeout = 2, Unauthorized = 3, TokenExpired = 4, Sanctioned = 5,
    Maintenance = 6, ServerFull = 7, ClientTooOld = 8, CoreVersionMismatch = 9, DataVersionMismatch = 10,
    RateLimited = 11, IdempotencyConflict = 12, VersionConflict = 13, BadRequest = 14, NotFound = 15,
    NotSupportedOffline = 16, KickedByNewLogin = 17,
    // 100~199 계정
    LoginFailed = 100, LoginThrottled = 101, LoginIdTaken = 102, NicknameTaken = 103, InvalidLoginId = 104,
    InvalidPassword = 105, InvalidNickname = 106, InvalidEmail = 107, EmailAccountLimit = 108,
    CodeInvalid = 109, CodeExpired = 110, MailUnavailable = 111, SignupClosed = 112, SignupLimit = 113,
    // 200~299 지팡이·강화·상점 (spec 08 13절)
    NotOwner = 200, MaxStage = 201, WandLocked = 202, MutationPending = 203, NoPendingMutation = 204,
    NotEnoughGold = 205, NotEnoughStone = 206, NotEnoughCharm = 207, CharmNotNeeded = 208,
    InvalidSacrifice = 209, LastProjectile = 210, NotEligible = 211, LastWand = 212,
    NameConfirmRequired = 213, TradeLocked = 214, PendingVerification = 215, HasDebt = 216,
    WeeklyLimit = 217, LibraryFull = 218, InvalidLoadout = 219, CoreEmpty = 220,
    // 300~399 판
    UnsettledRunFirst = 300, StartWaveNotAllowed = 301, RunNotFound = 302, RunAlreadySettled = 303,
    RunTokenInvalid = 304, RunRecordInvalid = 305, RunPrimaryCheckFailed = 306, WeeklyNotOpen = 307,
    // 400~499 거래·장터·우편·사회
    TradeNotOpen = 400, TradeRevisionStale = 401, ItemBound = 402, ItemNotTradable = 403,
    ListingGone = 404, PriceChanged = 405, GoldDailyCap = 406, Blocked = 407, TargetNotEligible = 408,
    MailAlreadyClaimed = 409, FriendLimit = 410, BlockLimit = 411,
    // 500~599 결투
    DuelNotEligible = 500, DuelQueueBanned = 501, DuelRttTooHigh = 502, DuelUdpRequired = 503,
    RoomFull = 504, RoomPasswordWrong = 505, IlCapExceeded = 506, ChallengeExpired = 507,
    // 600~699 결제
    PaymentsDisabled = 600, ProductUnavailable = 601, PaymentBlocked = 602, OrderExpired = 603,
    ReceiptInvalid = 604, CodeUsed = 605,
}
public readonly struct Error {
    public readonly ErrorCode Code;
    public readonly string? Detail;        // 개발용. 화면 문구는 Code로 이름 표에서 찾는다 ("error.<code>")
    public readonly int RetryAfterMs;      // 0이면 없음
    public Error(ErrorCode code, string? detail = null, int retryAfterMs = 0);
}
public readonly struct Result<T> {
    public bool Ok { get; }
    public T Value { get; }                // Ok일 때만 뜻이 있다
    public Error Error { get; }
    public static Result<T> Success(T value);
    public static Result<T> Fail(Error error);
    public static Result<T> Fail(ErrorCode code);
}
public readonly struct Unit { }            // 값이 없는 Result<Unit>
```

오류 코드는 **추가만** 하고 번호를 바꾸지 않는다. JSON에는 이름을 snake_case 문자열로 싣는다.

`ErrorCode`, `Error`, `Result<T>`, `Unit`은 `src/Wizard.Core/Errors/` 폴더에 두고 **코어 버전 해시에서 뺀다**(T-18 개정). `Wizard.Sim`과 `Wizard.Content`의 전투 쪽 코드는 이 타입들을 참조하지 않는다(분석기 진단). 거래·결제·결투 오류 코드를 더해도 검증 워커를 다시 빌드하지 않는다.

### 0.3 통합 표 — 명세마다 달랐던 표현의 정본

명세의 규칙과 수치는 그대로다. 아래는 표현만 하나로 정한 것이다. 구현은 오른쪽 칸을 쓴다.

| 주제 | 명세의 표현 | 정본 |
|---|---|---|
| 고정소수점 타입 | spec 01·05 `Fix64`, spec 02·14 `Fx`, spec 04 정수 U(1/6000칸) | `Wizard.Core.Fix64`. U 단위는 쓰지 않는다. 데이터에는 칸 단위 십진으로 적고 빌드 도구가 Raw로 바꾼다 |
| 벡터 | `FixVec2`, `FxVec2` | `FixVec2` |
| 십진 → Raw 반올림 | spec 01 0에서 먼 쪽, spec 02·14 짝수 쪽 | 가장 가까운 정수, 같으면 짝수 |
| 각도 | spec 01 1/65536바퀴 + 4096칸 보간, spec 02 센티도, spec 10 BAM32 | 센티도 정수 `Angle`(한 바퀴 36,000). sin/cos는 9,001칸 표 |
| 각도의 방향 | spec 02 반시계가 +, spec 10 시계가 +(y 아래 기준) | 0 = +x(오른쪽), **화면에서 반시계가 +**. `Dir8`과 같은 방향이다(N = 9000 = 화면 위). y는 아래가 +이므로 방향 벡터는 `(cos a, −sin a)` |
| 열 | spec 01 `HeatHu`(int, 열 1 = 120), spec 02 `Fx Heat` | `int` HU. 분열·거울상 배수는 천분율(1600, 2200, 1500) |
| 키 코드 | spec 01·09: Up 0, Left 1, Down 2, Right 3 / spec 10: Up 0, Down 1, Left 2, Right 3 | spec 01. `LogicalKey` 하나(`DuelKey` 없음). K = `Skill` |
| 정렬 키 분류 | spec 01·10: 자세 0, 방향 1, J 2, K 3, Pause 4 / spec 09: 방향 0, K 1, 그 밖 2 | spec 01. 슬롯을 더해 `(틱, 오프셋, 분류, 슬롯, 순번)` |
| `InputEvent` 필드 | `Tick`/`Down`, `EffectiveTick`/`Pressed` | `Tick`, `Offset`, `Key`, `Down`, `Seq` |
| 상태 해시 | spec 01 FNV-1a 64, spec 10 xxHash64(예시), spec 09 32바이트 | FNV-1a 64(8바이트). 기록 파일 무결성은 SHA-256 |
| 스냅숏 보관 수 | spec 01 9, spec 10 16 | 세계는 저장·복원만 한다. `DuelClientCore`는 확정 세계 1개 + 예측 세계 1개를 두고 스냅숏 버퍼 1개로 확정 → 예측을 복사한다(6.4절). 틱별 스냅숏 고리는 두지 않는다 |
| 1/√2의 Raw | spec 01 3,037,000,500(반올림), spec 11 3,037,000,499(버림) | `FixMath.InvSqrt2Raw = 3,037,000,500` 하나. spec 11의 499는 500으로 바꿔 읽는다. 광장 이동도 이 값을 쓴다 |
| 야영 중의 틱 | spec 01 16절 "야영 중 틱을 돌리지 않는다", spec 09 8.3·spec 07 11.4·TV-158 "야영도 코어 틱은 나간다" | **야영에서도 틱이 진행한다**(전리품 창, 야영 화면, 연습 포함). 틱을 멈추는 것은 일시정지뿐이다(3.1절). T-13 개정 |
| 결투 입력의 순서 위반과 늦음 | spec 10 14.8 의사코드·TV-N15·TV-N16(봉인된 틱의 이벤트와 순번 건너뜀은 패킷 버림), online 4.11 표·TV-N11(늦은 입력은 그 이벤트만 버림) | 10.6절의 검증 순서. 봉인된 틱의 이벤트는 **그 이벤트만** 늦음으로 버리고, 순번이 건너뛰면 사이 순번을 유실로 소비한다. 패킷 전체를 버리는 것은 한 패킷 안에서 순서가 역행할 때뿐이다. spec 10의 해당 줄은 이 규칙으로 고쳐 적었다 |
| 되감기 한도 초과 | online 4.11-6·spec 10 14.7·TV-N22(멈추고 기다림), dev 문서 일부(전체 상태 요청) | `Stalled`(예측 중지, 확정 대기). 전체 상태 요청은 해시 불일치와 확정 공백이 `FullStateGapTicks`를 넘을 때만(6.4절) |
| 입력 지연 D의 자리 | spec 10 `DuelPlayerSetup.InputDelayTicks` | 시뮬레이션은 D를 모른다. `DuelSetup`에 없다. 서버가 재서 `HelloAck`로 내려 주고 `DuelHostCore.SetInputDelay`와 WZDR 머리말의 별도 칸에 둔다(10.5절) |
| 1차 검사의 변덕 | spec 09 8.2 `whims_upper(w)`(최대치), 이 문서의 옛 `ScoreCap(wave, m1, m2)` | spec 09. `IRunLimits.ScoreCap(wave, whimsUpper)`. `wizard-api`는 `WaveMath`를 부르지 않는다(4.3절) |
| 체력 배율 | spec 07 6.1 `long HMicro(w)`(×1,000,000, 단계마다 내림, 포화), 이 문서의 옛 `Fix64 HpMultiplier` | spec 07. `WaveMath.HpMicro`(3.7절) |
| 시간대·리그 코드 | spec 14 3.9·3.15(1부터), 이 문서의 옛 열거형(0부터) | spec 14(T-16). `TimeOfDay` 1~4, `LeagueId` 1~3 |
| 변덕·틀의 문자열 id | spec 07의 이름, spec 14 `whim.*`·`frame.*` | spec 14. 열거형 이름은 2.1절, 문자열 id는 대응표 |
| 시설 id | spec 11 `fac_*`, spec 14 `facility.*` | spec 14. 대응표는 2.1절. `fac_nuru_chair`처럼 작업용 이름이 든 식별자는 코드와 데이터에 쓰지 않는다(DECISIONS 1.25) |
| uuid의 바이트 | 정하지 않음 | RFC 4122 네트워크 순서(문자열 hex 순서) 16바이트. `UuidBytes`만 쓴다(1.5절) |
| 순번을 매기는 곳 | spec 01 양자화기, spec 09 "읽을 때 1부터" | `InputRecorder`(무한 웨이브·연습)와 `DuelClientCore`(결투)만. 양자화기는 순번 없는 `QuantizedKey`를 낸다(3.1절) |
| `Fix64 → float` | 이 문서의 옛 `Wizard.Unity.Presentation.FixConvert` | `Wizard.ViewLogic.FixConvert`(13절) |
| 난수 흐름 | spec 01 고정 `StreamId` 1~6, spec 07 `Derive(seed, tag, wave, sub)` | spec 07 방식. 흐름 표는 1.3절 하나 |
| 발사 묶음 | spec 01 `ShotId`(내 발사)·`CastId`(스침 묶음), spec 02 `CastId`(내 발사) | `ShotId` = 플레이어 발사 1회. `CastId` = 스침을 한 번만 인정하는 묶음(적의 시전, 결투에서는 상대의 `ShotId`) |
| 모드 열거형 | `SimMode`, `GameMode` | `SimMode { Endless, Duel, Plaza, Practice }` |
| 틱 안 처리 순서 | spec 01 11단계, spec 05 9단계 | 3.2절의 0~12단계 |
| 야영 조작 기록 | spec 01·07 `CampCommand`, spec 09 `RunCommand` | `RunCommand` |
| 캐릭터 | `D1`, `Char1`, `char_1`, `disciple_1` | `CharacterId.Char1`~`Char5`(1~5), 문자열 `char.1`~`char.5` |
| 속성 | `Frost`, `frost`, `shadow` | `Element.Ice`, `Element.Dark`. 문자열 `element.ice`, `element.dark` |
| 블록·비기·보스·재료·패시브 id | 명세마다 다름 | spec 14 3절. 대응표는 2.1절 |
| 투사체의 트리 참조 | `CompiledSpell Tree`(참조) | `SpellHandle`(정수). 스냅숏이 값 복사가 되게 |
| `run.id` | spec 12 `bigint`, spec 09 `uuid` | `uuid`(v7, 서버 생성) |
| `run.status`·`kind` 저장 | spec 12 `text`, spec 09 `smallint` | `text + CHECK`(spec 12의 값) |
| 주문 상태 이름 | spec 08 `AwaitingApproval`/`Approved`, spec 12 `pending_auth`/`authorized` | spec 12 |
| `IDuelTransport.TryReceive` | `out ReadOnlySpan<byte>` | `int Receive(Span<byte>)`(6.3절) |
| 결투 상태의 `Dictionary` | spec 10 `RimeCounter` | 슬롯 순 고정 배열 |

## 1. `Wizard.Core`

### 1.1 `Fix64` — Q32.32

소유: `Wizard.Core`. 근거: spec 01 2.2, spec 02 2.2.

```csharp
// namespace Wizard.Core
public readonly struct Fix64 : IEquatable<Fix64>, IComparable<Fix64> {
    public readonly long Raw;                         // 값 = Raw / 2^32
    public const int FracBits = 32;
    public const long OneRaw = 1L << 32;

    public static readonly Fix64 Zero, One, Half, MinValue, MaxValue;

    public static Fix64 FromRaw(long raw);
    public static Fix64 FromInt(int value);
    public static Fix64 FromRatio(long numerator, long denominator);   // 가장 가까운 Raw, 같으면 짝수
    public static Fix64 FromPermille(int permille);                    // = FromRatio(permille, 1000)

    public int FloorToInt();                          // 음의 무한대 쪽
    public int CeilToInt();
    public int RoundToInt();                          // .5는 올림(양의 무한대 쪽)

    public static Fix64 operator +(Fix64 a, Fix64 b); // Raw 덧셈, 모든 빌드에서 unchecked
    public static Fix64 operator -(Fix64 a, Fix64 b);
    public static Fix64 operator -(Fix64 a);
    public static Fix64 operator *(Fix64 a, Fix64 b); // 128비트 곱 뒤 산술 오른쪽 시프트 32(내림)
    public static Fix64 operator /(Fix64 a, Fix64 b); // (a.Raw << 32) / b.Raw 를 128비트로, 0 쪽 버림. b == 0이면 SimInvariantException(직접 검사)
    public static Fix64 operator *(Fix64 a, int b);
    public static Fix64 operator /(Fix64 a, int b);   // 0 쪽 버림. b == 0이면 SimInvariantException(직접 검사)
    public static bool operator <(Fix64 a, Fix64 b);  // >, <=, >=, ==, != 도 같다

    public static Fix64 Abs(Fix64 a);
    public static Fix64 Min(Fix64 a, Fix64 b);
    public static Fix64 Max(Fix64 a, Fix64 b);
    public static Fix64 Clamp(Fix64 v, Fix64 lo, Fix64 hi);
    public static Fix64 MulPermille(Fix64 a, int permille);   // FixMath.MulDiv(a.Raw, permille, 1000). 128비트 곱, 0 쪽 버림
}

public readonly struct FixVec2 : IEquatable<FixVec2> {
    public readonly Fix64 X, Y;                       // +x 오른쪽, +y 아래(전장 좌표: (0,0) 왼쪽 위, (24,14) 오른쪽 아래)
    public FixVec2(Fix64 x, Fix64 y);
    public static FixVec2 operator +(FixVec2 a, FixVec2 b);
    public static FixVec2 operator -(FixVec2 a, FixVec2 b);
    public static FixVec2 operator *(FixVec2 a, Fix64 s);
    public static Fix64 Dot(FixVec2 a, FixVec2 b);
    public static Fix64 Cross(FixVec2 a, FixVec2 b);
    public Fix64 LengthSq();
    public Fix64 Length();                            // FixMath.Sqrt(LengthSq())
    public FixVec2 Normalized();                      // len = Length(). 길이 0이면 (0,0). 성분 = Wide.Div128By64(X.Raw << 32, len.Raw), 0 쪽 버림
    public static Fix64 DistanceSq(FixVec2 a, FixVec2 b);
    public static FixVec2 FromAngle(Angle a);         // (cos a, −sin a). y가 아래로 늘기 때문에 부호를 뒤집는다
}

public readonly struct Angle : IEquatable<Angle> {
    public readonly int Centi;                        // 0..35999. 0 = +x(오른쪽), 값이 커질수록 화면에서 반시계
    public const int Full = 36000;
    public static Angle FromCenti(int centi);         // 음수와 36000 이상을 접는다
    public static Angle FromDegrees(int degrees);
    public static Angle FromDir8(Dir8 dir);           // E 0, NE 4500, N 9000(화면 위 = (0, −1)), …
    public static Angle OfVector(FixVec2 v);          // FixMath.Atan2(−v.Y, v.X). FromAngle의 역
    public static Angle operator +(Angle a, int centi);
    public static int Delta(Angle from, Angle to);    // 부호 있는 최단 차이 [−18000, 18000)
    public static Angle RotateToward(Angle from, Angle to, int maxCenti);
}
public enum Dir8 : byte { E = 0, NE = 1, N = 2, NW = 3, W = 4, SW = 5, S = 6, SE = 7 }

public static class FixMath {
    public static Fix64 Sqrt(Fix64 v);                // 정수 제곱근(내림). v ≤ 0이면 0
    public static Fix64 Sin(Angle a);                 // 9,001칸 1/4바퀴 표 + 대칭. 보간 없음
    public static Fix64 Cos(Angle a);
    public static Angle Atan2(Fix64 y, Fix64 x);      // 수학의 atan2(y가 위로 느는 좌표). 아래 정의. (0,0)이면 0. 전장 벡터에는 Angle.OfVector를 쓴다
    public static long MulDiv(long a, long b, long c);// a × b / c 를 128비트로, 0 쪽 버림. c == 0이면 SimInvariantException
    public static Fix64 PowTable(PowBase which, int exponent);   // 1.15^n, 0.85^d, 0.92^d, 0.7^k — 미리 만든 표. 지수가 표 범위 밖이면 끝값으로 고정(음수 → 0번, 넘침 → 마지막)
    public const long InvSqrt2Raw = 3037000500L;      // 1/√2. 코어 전체에서 이 값 하나(0.3절)
    // 나눗셈 도우미. 코어 세 어셈블리에서 상수가 아닌 제수는 반드시 이것을 거친다(분석기).
    public static int DivInt(int a, int b);           // 0 쪽 버림. b == 0 → SimInvariantException. int.MinValue / −1 → int.MinValue(래핑)
    public static long DivLong(long a, long b);       // 같음. long.MinValue / −1 → long.MinValue
    public static int ModInt(int a, int b);           // 부호는 피제수를 따른다. b == 0 → SimInvariantException. MinValue % −1 → 0
    public static long ModLong(long a, long b);
    public static int SaturateToInt(long v);          // [int.MinValue, int.MaxValue]로 자른다(적 체력, 체력 배율 적용 피해 전용)
}
public enum PowBase : byte { Enhance115 = 0, Decay085 = 1, Decay092 = 2, Chain07 = 3 }
```

계약:

- 넘침: 덧셈·뺄셈·곱셈은 래핑한다(모든 빌드 동일). 값의 범위는 설계가 보장하고, `SIM_CHECKS` 기호가 정의된 테스트 빌드에서만 단언문으로 확인한다. 단언문은 결과를 바꾸지 않는다.
- `Atan2`: 팔분면으로 접은 뒤 `a ∈ [0, 4500]`에서 `sin(a)·x ≤ cos(a)·y`를 만족하는 가장 큰 `a`를 이분 탐색으로 찾는다(128비트 정수 비교). `a`와 `a+1` 가운데 외적의 절댓값이 작은 쪽을 고르고, 같으면 `a`. 그 뒤 원래 팔분면으로 편다. 실수 함수는 쓰지 않는다.
- 표(`SinTable.g.cs`, `PowTables.g.cs`)는 `tools/Wizard.TableGen`이 임의 정밀도 계산으로 만들고 커밋한다. 표 바이트의 SHA-256을 테스트가 확인한다.
- 속도 변환(spec 01 2.3): 틱당 이동량 `vRaw = (SpeedCenti × mulPermille × 2^32 + 3,000,000) / 6,000,000`(전부 `long`, 양수 전제, 반올림의 동률은 올림. spec 05 TV-UNIT-05의 3.76 → 269,151,284와 맞는다), 대각선 한 축은 `(vRaw × InvSqrt2Raw) >> 32`. 도우미 `FixMath`가 아니라 `Wizard.Sim.World.Movement`에 둔다.
- 나눗셈은 전 함수다. 세 런타임(CoreCLR, Mono, IL2CPP)과 두 아키텍처(arm64, x64)에서 0으로 나누기와 `MinValue ÷ −1`의 동작이 다르므로(예외, 0, 프로세스 종료) 런타임에 맡기지 않는다. (1) 제수 0은 코드가 먼저 검사해 `SimInvariantException`을 던진다. (2) `MinValue ÷ −1`은 `MinValue`(래핑), 나머지는 0이다. 도우미가 이 경우를 먼저 가른다. (3) `Wide.Div128By64`의 몫이 64비트를 넘으면 아래 64비트를 돌려준다. `checked` 문맥은 쓰지 않는다.
- 포화와 래핑의 구분: **적·보스의 체력 계산과 체력 배율을 적용한 피해**(spec 07 6.1~6.2)만 `SaturateToInt`로 포화시킨다. 그 밖의 `Fix64`·정수 연산은 래핑이다.
- 골든에 0 제수와 `MinValue` 경계 벡터를 둔다(`tests/golden/arith/`). 모든 런타임·아키텍처 조합에서 같은 값, 같은 예외 분류가 나와야 한다.

### 1.2 128비트 도우미

```csharp
// namespace Wizard.Core
public static class Wide {                            // netstandard2.1에 Int128과 Math.BigMul(long,long)이 없다
    public static void Mul64(long a, long b, out long hi, out ulong lo);      // 부호 있는 64×64 → 128
    public static void UMul64(ulong a, ulong b, out ulong hi, out ulong lo);
    public static long ShiftRight32(long hi, ulong lo);                       // 128비트 산술 시프트 뒤 아래 64비트
    public static long Div128By64(long hi, ulong lo, long divisor);           // 0 쪽 버림. divisor == 0 → SimInvariantException. 몫이 64비트를 넘으면 아래 64비트
    public static int Compare128(long aHi, ulong aLo, long bHi, ulong bLo);
}
```

`Wide`와 `FixMath`의 나눗셈 도우미 안에서만 원시 `/`와 `%`를 쓴다(제수 검사를 마친 뒤). 구현은 시프트와 뺄셈 또는 검사된 원시 연산 가운데 무엇이든 되지만 결과는 위 정의와 같아야 하고, 경계 벡터로 확인한다.

### 1.3 난수

소유: `Wizard.Core`. 근거: spec 01 3.3, spec 07 14.2.

```csharp
// namespace Wizard.Core
public struct Pcg32 {                                 // PCG32 XSH-RR. 가변 구조체: 두 필드가 상태 해시와 스냅숏에 들어간다
    public ulong State;
    public ulong Inc;
    public Pcg32(ulong seed, ulong seq);              // inc = (seq << 1) | 1; state = 0; Next(); state += seed; Next();
    public uint NextU32();
    public int NextInt(int exclusiveMax);             // 거부법: threshold = (uint)(-exclusiveMax) % exclusiveMax. exclusiveMax < 1이면 SimInvariantException(직접 검사)
    public int NextRange(int minInclusive, int maxInclusive);
    public bool Chance(int numerator, int denominator);       // NextInt(denominator) < numerator
    public Fix64 NextFix01();                         // [0,1): Raw = NextU32()
}

public enum RngStream : ushort {
    WaveComp  = 1,    // 틀, 종, 엘리트 종, 개수, 묶음 나누기            (호출 때 파생, 상태 없음)
    WaveSpawn = 2,    // 스폰 위치                                      (웨이브 시작 때 파생, 세계가 보관)
    Modifier  = 3,    // 변덕 선택. 서버가 상한 계산 때 이것만 돌린다     (호출 때 파생)
    Loot      = 4,    // 전리품 카드. sub = 다시 뽑기 횟수, 빌린 블록은 0xFFFF   (호출 때 파생)
    HpShard   = 5,    // 체력 조각                                      (세계가 보관)
    Event     = 6,    // 별똥 결정 등 돌발 사건                          (세계가 보관)
    EnemyAi   = 7,    // 적 AI의 무작위 선택                             (세계가 보관)
    Mutation  = 8,    // 불안정 핵 5%, 뒤틀린 총열 흔들림                 (세계가 보관)
    Weather   = 9,    // 규칙에 영향 없는 연출 사건(다시 보기에서 같게)    (세계가 보관)
    ArtUnlock = 10,   // 비기 해금 후보                                  (호출 때 파생)
    BossAi    = 11,   // 보스 패턴 선택                                  (세계가 보관)
    DuelWild  = 12,   // 결투 야생 난입                                  (세계가 보관, wave = 라운드 번호)
}

public static class RngStreams {
    public static ulong SplitMix64(ulong x);
    // Pcg32(SplitMix64(seed ^ ((ulong)stream << 48) ^ ((ulong)(uint)wave << 16) ^ (ushort)sub), seq: (ulong)stream)
    public static Pcg32 Derive(ulong runSeed, RngStream stream, int wave, int sub = 0);
}
```

- "세계가 보관"인 흐름은 웨이브(결투는 라운드) 시작 때 `Derive`로 다시 만든다. 앞 웨이브에서 난수를 얼마나 썼는지가 뒤 웨이브에 번지지 않는다.
- 강화, 드랍 추첨은 판 씨앗을 쓰지 않는다. `Wizard.Rules.IRandomSource`(4.1절)를 쓴다.
- 표시 계층은 `Pcg32`를 쓰지 않는다.

### 1.4 상태 해시와 정규 직렬화

소유: `Wizard.Core`. 근거: spec 01 3.4.

```csharp
// namespace Wizard.Core
public ref struct StateWriter {                       // 두 가지 모드: 해시만(버퍼 없음) / 바이트로 쓰기(상태 전체 전송)
    public static StateWriter ForHash();
    public static StateWriter ForBytes(ByteWriter target);
    public void U8(byte v);  public void Bool(bool v);            // bool = 1바이트 0/1
    public void I32(int v);  public void U32(uint v);             // 리틀 엔디언 고정 폭
    public void I64(long v); public void U64(ulong v);
    public void Fix(Fix64 v); public void Vec(FixVec2 v); public void Ang(Angle v);
    public void Count(int n);                                      // 컬렉션 앞에 개수(i32)
    public ulong Hash { get; }                                     // FNV-1a 64: offset 0xcbf29ce484222325, prime 0x100000001b3
}
public ref struct StateReader { /* 같은 순서로 읽는다. ForBytes로 쓴 것만 */ }

public sealed class ByteWriter {                      // 패킷과 파일용. 재사용 가능한 버퍼
    public void U8(byte v); public void U16(ushort v); public void U32(uint v); public void U64(ulong v);
    public void I16(short v); public void I32(int v); public void I64(long v);
    public void VarUInt(ulong v);                     // LEB128
    public void Bytes(ReadOnlySpan<byte> v);
    public ReadOnlySpan<byte> Written { get; }
    public void Reset();
}
public ref struct ByteReader {
    public ByteReader(ReadOnlySpan<byte> data);
    public bool TryU8(out byte v); /* … 같은 종류. 모자라면 false, 예외 없음 */
    public bool TryVarUInt(out ulong v);
    public int Remaining { get; }
}
```

정규 직렬화 규칙: 필드 순서는 코드에 고정, 개체는 id 오름차순, 선택 값은 존재 바이트 뒤에 값, 표시 전용 값은 넣지 않는다, 패딩 없음.

### 1.5 결정론 자료구조와 식별자

```csharp
// namespace Wizard.Core
public static class DetSort {
    // 비교자는 구조체 제네릭 매개변수로 받는다. 인터페이스로 받으면 구조체 비교자가 박싱되어 Step의 할당 0과 부딪친다.
    public static void Stable<T, TComparer>(T[] items, int start, int count, TComparer comparer, T[] scratch)
        where TComparer : struct, IComparer<T>;                                               // 병합 정렬. 안정. scratch는 호출자가 가진 임시 버퍼
    public static void InsertionStable<T, TComparer>(Span<T> items, TComparer comparer)
        where TComparer : struct, IComparer<T>;
}
public sealed class SortedIntList {                   // 오름차순 유지, 중복 없음
    public int Count { get; }
    public int this[int index] { get; }
    public bool Add(int value);
    public bool Contains(int value);
    public bool Remove(int value);
    public void Clear();
    public void CopyFrom(SortedIntList other);        // 스냅숏용 값 복사
}
public sealed class RingBuffer<T> where T : struct {  // 고정 용량. C# 9에는 인라인 고정 배열이 없어 내부가 배열 참조다. 그래서 구조체가 아니라 클래스다
    public RingBuffer(int capacity);
    public int Count { get; } public int Capacity { get; }
    public ref readonly T this[int index] { get; }    // 0 = 가장 오래된 것
    public void Push(in T value);                     // 가득 차면 가장 오래된 것을 덮는다
    public void Clear();
    public void CopyFrom(RingBuffer<T> other);        // 스냅숏용 깊은 복사. 대입으로 복사하지 않는다
}
public static class UuidBytes {                       // uuid ↔ 16바이트. RFC 4122 네트워크 순서(문자열 hex 순서)
    public static void Write(Guid value, Span<byte> destination16);   // Guid.ToByteArray()의 혼합 순서를 쓰지 않는다
    public static Guid Read(ReadOnlySpan<byte> source16);
}

public readonly struct EntityId : IEquatable<EntityId>, IComparable<EntityId> {
    public readonly int Value;                        // 세계 하나에 카운터 하나. 생성 순서대로 1씩. 0 = 없음
    public static readonly EntityId None;
}
public readonly struct PlayerSlot : IEquatable<PlayerSlot> {
    public readonly byte Value;                       // 0..3. 무한 웨이브는 0 하나, 결투는 0(왼쪽)·1(오른쪽)
    public static readonly PlayerSlot None;           // 255
}
public readonly struct ContentId : IEquatable<ContentId> {   // "block.fireball" 같은 문자열 id (spec 14 1.1)
    public readonly string Value;
    public static bool TryParse(string s, out ContentId id); // ^[a-z][a-z0-9_]*(\.[a-z0-9][a-z0-9_]*)+$, 64자 이하
}
```

## 2. `Wizard.Content`

소유: `Wizard.Content`. 근거: spec 14 1~3절과 각 명세의 데이터 절.

### 2.1 id 열거형과 대응표

코어는 정수 열거형만 쓴다. 저장, 통신, DB, 데이터 파일은 문자열 id를 쓴다. 정수 값은 추가만 하고 바꾸지 않는다.

```csharp
// namespace Wizard.Content
public enum CharacterId : byte { Baseline = 0, Char1 = 1, Char2 = 2, Char3 = 3, Char4 = 4, Char5 = 5 }
public enum Element : byte { None = 0, Fire = 1, Lightning = 2, Arcane = 3, Ice = 4, Dark = 5 }
public enum BlockId : byte {
    None = 0, MagicBolt = 1, Fireball = 2, ChainLightning = 3, Boomerang = 4, IceSpike = 5, ShadowOrb = 6,
    SplitTwo = 7, SplitThree = 8, OnHitTrigger = 9,
    WallBounce = 10, DoubleSize = 11, Pierce = 12, Frost = 13, Homing = 14, TripleHeat = 15, Corrosion = 16 }
public enum BlockKind : byte { Projectile = 1, Control = 2, Modifier = 3 }
public enum Rarity : byte { Common = 1, Rare = 2, Hero = 3 }
public enum ArtId : byte {
    None = 0, Burst = 1, Recoil = 2, Fan = 3, Wings = 4, Afterimage = 5, Spear = 6, Vent = 7, Octastar = 8,
    ThunderSpear = 9, Bloom = 10, Kiln = 11, BoltDash = 12, CopyCircle = 13, IceScreen = 14, ShadowHand = 15 }
[Flags] public enum ArtMask : ushort { None = 0 /* 비트 번호 = (int)ArtId */ }
public enum PassiveId : byte { None = 0, Overflow = 1, LightningRod = 2, Underline = 3, ThinIce = 4, ShadowContract = 5 }
public enum WandTraitId : byte { None = 0, HeatAt80 = 1, ThreeMutations = 2, DepthDecay = 3, KeepCrystal = 4, OnCredit = 5 }
public enum MutationId : byte {
    None = 0, TwistedBarrel = 1, UnstableCore = 2, HypersensitiveCircuit = 3, Gluttony = 4, MirrorImage = 5, GlassWand = 6 }
public enum EnemyTypeId : ushort {
    None = 0, SlimeMoss = 1, WolfBramble = 2, GolemMenhir = 3, ShamanCap = 4, BugLantern = 5, HogNeedle = 6,
    WeaselPot = 7, BatDusk = 8, WispStray = 9, VineThorn = 10, BoarTusk = 11, ToadDrum = 12,
    SporeSeedling = 101 }                              // 엘리트는 종이 아니라 EnemyState.IsElite (표시 id는 elite.<키>, 코드 200 + 종)
public enum BossId : ushort {
    None = 0, KilnBoar = 301, MirrorPhantom = 302, SporeQueen = 303, StormOwl = 304, FrostGoat = 305, NightSerpent = 306 }
[Flags] public enum EnemyTag : ushort { None = 0, Elite = 1, Heavy = 2, Boss = 4, Flying = 8, Burrowed = 16, Rooted = 32 }
public enum TimeOfDay : byte { None = 0, Day = 1, Dusk = 2, Night = 3, BossWave = 4 }       // spec 14 3.9의 코드(tod.day … tod.boss)
public enum FrameId : byte { Swarm = 1, Hunt = 2, Battery = 3, Ambush = 4, Mixed = 5 }
public enum ModifierId : byte {
    None = 0, Rush = 1, ThickHide = 2, BigHerd = 3, ChiefMarch = 4, LastBreath = 5, SlowBarrage = 6, LongNight = 7, NarrowField = 8 }
public enum LeagueId : byte { None = 0, Sprout = 1, Beast = 2, Heir = 3 }                    // spec 14 3.15의 코드
public enum StoneTier : byte { Low = 0, Mid = 1, High = 2 }
public enum ProfileKind : byte { Offline = 0, Server = 1 }
public enum CurrencyCode : byte { Gold = 0, SeasonCoin = 1, Starlight = 2 }
public enum CurrencyBucket : byte { Earned = 0, Paid = 1, Free = 2 }
public enum WandOrigin : byte { Start = 0, Shop = 1, Trade = 2 }
public enum WandLockState : byte { None = 0, Expedition = 1, Duel = 2, Market = 3, Mail = 4, Trade = 5 }

// spec 14 3.9~3.15의 나머지 표. 코드는 그 표의 코드(없으면 표의 순서, 1부터). T-33에 따라 M1-CONT-01이 전부 선언한다.
public enum WeatherEventId : byte {                   // spec 14 3.9 "날씨와 돌발 사건". 결투에는 넣지 않는다
    None = 0, Shower = 1, Fog = 2, StarCrystal = 3, MasterAdvice = 4, DiscardedItem = 5 }
public enum ItemCode : ushort {                       // spec 14 3.10. 문자열 id가 정본(item_stack.item_code). 블록은 BlockId를 쓴다
    None = 0, StoneLow = 1, StoneMid = 2, StoneHigh = 3, CharmSafeguard = 4,
    MaterialBundle = 10, CrowBundle = 11, SeasonFabric = 12, WandBox = 13, LibraryExpansion = 14, Rename = 15,
    HealthShard = 20 }                                // 판 안에서만. 프로필에 남지 않는다
public enum FacilityId : byte {                       // spec 14 3.13 표의 순서
    None = 0, BigCampfire = 1, StallStreet = 2, PeddlerCart = 3, DuelBoard = 4, ArenaPath = 5,
    HeirMonument = 6, MasterChair = 7, TrainingDummy = 8, CrowMailbox = 9, FieldGate = 10 }
public enum EmoteId : byte {                          // spec 14 3.15
    None = 0, Greet = 1, Clap = 2, Taunt = 3, Bow = 4, Sit = 5, Dance = 6, RaiseWand = 7, Despair = 8 }
public enum ChatChannel : byte {                      // spec 14 3.15. 일곱 개. 서버 전체 채널은 없다(DECISIONS 2.7)
    None = 0, Nearby = 1, Plaza = 2, Trade = 3, Whisper = 4, Friends = 5, DuelRoom = 6, System = 7 }
public enum TitleKind : byte { Stage = 1, Record = 2, Revenge = 3, Season = 4, Feat = 5 }     // 칭호 하나하나는 데이터의 문자열 id(title.*)다
public enum CosmeticSlot : byte { None = 0, Head = 1, Cloak = 2, Footprints = 3, Seat = 4 }   // spec 14 3.14 치장 칸
public enum CosmeticCategory : byte { None = 0 /* 1~12: spec 14 3.14 "옷장 분류 12"의 나열 순서(cosmetic_cat.*) */ }
public enum CosmeticSource : byte { Record = 1, Season = 2, Trade = 3, GoldShop = 4, PaidShop = 5 }
public enum PresenceState : byte {                    // spec 14 3.15 머리 위·목록 상태(I-F4)
    Offline = 0, Online = 1, Expedition = 2, Duel = 3, Trade = 4, Idle = 5, DuelWait = 6, Spectate = 7 }

public static class ContentIds {                      // 열거형 ↔ 문자열 id. 표는 팩에서 읽지 않고 코드에 고정한다
    public static string ToId(BlockId v);  public static bool TryParse(string id, out BlockId v);
    public static string ToId(ArtId v);    public static bool TryParse(string id, out ArtId v);
    /* CharacterId, Element, MutationId, EnemyTypeId, BossId, PassiveId, … 같은 모양 */
    public static string ResolveAlias(string id);     // "frost" → "ice", "master" → "npc.master" 등 (spec 14 1.2, 3.1)
}
```

명세의 옛 이름 → 정본(구현할 때 명세를 읽으며 바꿔 읽는다):

| 종류 | 명세의 이름 | 정본 열거형 / 문자열 id |
|---|---|---|
| 블록 | `GreatFireball`, `great_fireball`, `giant_fireball` | `Fireball` / `block.fireball` |
| 블록 | `Split2`, `split_2` / `Split3`, `split_3` | `SplitTwo` / `block.split_two`, `SplitThree` / `block.split_three` |
| 블록 | `Pierce2`, `pierce_2` | `Pierce` / `block.pierce` |
| 블록 | `Overheat3`, `overheat_x3` | `TripleHeat` / `block.triple_heat` |
| 블록 | `Erosion`, `erosion`(잠식) | `Corrosion` / `block.corrosion` |
| 비기 | `ManaRelease`, `RecoilShot`, `FanSpread`, `TwinWing` | `Burst`, `Recoil`, `Fan`, `Wings` / `art.burst`, `art.recoil`, `art.fan`, `art.wings` |
| 비기 | `AfterimageStep`, `PiercingLance`, `VentBurst`, `EightfoldSpread` | `Afterimage`, `Spear`, `Vent`, `Octastar` |
| 비기 | `ThunderLance`, `FullBloom` | `ThunderSpear`, `Bloom` |
| 비기 | `Transcribe`, `art.transcribe` / `art.thunder_dash` | `CopyCircle` / `art.copy_circle`, `BoltDash` / `art.bolt_dash` |
| 패시브 | `passive.boil_over`, `passive.shadow_pact` | `passive.overflow`, `passive.shadow_contract` |
| 보스 | `boss.boar_kiln`, `boss.master_phantom`, `boss.mirror_master`, `boss.star_serpent` | `boss.kiln_boar`, `boss.mirror_phantom`, `boss.night_serpent` |
| 재료 | `stone_low`/`stone_mid`/`stone_high`, `charm_guard` | `mat.stone_low`/`mat.stone_mid`/`mat.stone_high`, `charm.safeguard` |
| 속성 | `Frost`, `frost`, `shadow` | `Element.Ice`, `Element.Dark` |
| 변덕 | `Rush`, `ThickHide`, `BigHerd`, `ChiefMarch`, `LastBreath`, `SlowBarrage`, `LongNight`, `NarrowField`(열거형 이름) | 문자열 id는 spec 14: `whim.crowding`, `whim.thick_hide`, `whim.big_pack`, `whim.chief_parade`, `whim.last_breath`, `whim.slow_barrage`, `whim.long_night`, `whim.narrow_field`(코드 1~8) |
| 틀 | `Hunt` | `frame.hunting_party`. 나머지는 `frame.swarm`, `frame.battery`, `frame.ambush`, `frame.mixed` |
| 시설 | `fac_bonfire` | `FacilityId.BigCampfire` / `facility.big_campfire` |
| 시설 | `fac_stall_street`, `fac_peddler_cart`, `fac_duel_board` | `facility.stall_street`, `facility.peddler_cart`, `facility.duel_board` |
| 시설 | `fac_stone_path` | `FacilityId.ArenaPath` / `facility.arena_path` |
| 시설 | `fac_heir_monument` | `facility.heir_monument` |
| 시설 | `fac_nuru_chair` | `FacilityId.MasterChair` / `facility.master_chair`(작업용 이름을 식별자에 쓰지 않는다) |
| 시설 | `fac_practice_dummy` | `FacilityId.TrainingDummy` / `facility.training_dummy` |
| 시설 | `fac_crow_mailbox` | `facility.crow_mailbox` |
| 시설 | `fac_wilds_gate` | `FacilityId.FieldGate` / `facility.field_gate` |

작업용 이름의 로마자 표기(`dalgu`, `chorong`, `galpi`, `seonge`, `geumeum`, `nuru`, `handeot`, `dolmadang`, `cheotmodak`)는 `src/`, `server/`, `data/`의 식별자·열쇠·파일 이름에 쓰지 않는다. 예외는 그림 매니페스트가 이미 정한 폴더 열쇠를 적는 `data/presentation/asset_aliases.json`과 `art/` 아래 경로뿐이다. CI의 `names-scan.sh`가 검사한다.

비기 번호는 spec 03의 0~14가 아니라 위 열거형의 1~15다. 비기 해금 저장은 문자열 id 배열이다.

### 2.2 정의 타입

정의 타입은 팩에서 읽은 뒤 바뀌지 않는다. 필드의 값과 범위는 각 명세의 표가 주인이다. 여기서는 타입과 단위만 고정한다.

```csharp
// namespace Wizard.Content
public sealed class CharacterDef {                    // spec 01 5.1, spec 04 2.2
    public CharacterId Id; public Element Element;
    public int MaxHpCells;                            // 칸. 체력 점수 = 칸 × 100
    public int WalkSpeedCenti, StanceSpeedCenti;      // 센티 칸/초 (4.7 → 470)
    public Fix64 HitRadius, BodyRadius, GrazeRadius;
    public int InvulnTicks;
    public int CrystalCap, ComboPerCrystal, CrystalLossOnHit;
    public int BaseLinkSlots, MaxMutations;
    public int CoolHuPerTick, CoolDelayTicks, CritStartHu, LockTicks;
    public int GrazeBuffTicks, GrazeBuffPermille;
    public PassiveId Passive; public WandTraitId WandTrait; public ArtId UniqueArt;
    public DuelCharacterOverride Duel;                // spec 04 8절, spec 10 7절
}
public sealed class BlockDef {                        // spec 02 4.2
    public BlockId Id; public BlockKind Kind; public Rarity Rarity; public Element Element;
    public int HeatHu;                                // 투사체·변형이 더하는 열
    public int HeatMulPermille;                       // 분열만 1600 / 2200. 그 밖 1000
    public int SplitCount;                            // 분열만 2 / 3
    public Fix64 BaseDamage; public int IntervalTicks;
    public Fix64 SpeedPerTick; public bool IsInstant; public Fix64 Range; public Fix64 HitRadius;
    public int DamageBonusPermille;                   // 변형: 200, 100
    public ProjectileFlags Flags; public ModifierEffect Effect;
    public BlockParams Params;                        // 폭발 반경, 전이 횟수, 감속 %, 지속 틱 등
}
public sealed class ArtDef { /* spec 03 12.1: Id, Kind, Owner, Paths, IsJust, Difficulty, CostKind, Cost, MinCost,
                               CreditAllowed, FallbackParent, Unlock, Effect, Duel */ }
public sealed class MutationDef { /* spec 04 11절 */ }
public sealed class EnemyDef { /* spec 05 2.2~2.3: 수치, AttackDef[], AiKind, AiParams, EliteDef */ }
public sealed class BossDef { /* spec 06 1절: 단계, 패턴 표, 격노 */ }
public sealed class WaveCurveDef { /* spec 07 2.1 */ }
public sealed class FrameDef { } public sealed class ModifierDef { } public sealed class TimeOfDayDef { }
public sealed class ScoringDef { /* spec 07 12절 */ }
public sealed class ArenaDef { public Fix64 Width, Height; public FixVec2[] Pillars; public FixVec2[] SpawnPoints; }
public sealed class DuelRuleSet { /* spec 10 2절·17.2절. 각도는 센티도, 반지름은 Fix64. 전투 규칙만(코어 버전 해시 안) */ }
// namespace Wizard.Content.Rules — 넷코드 값. 시뮬레이션 결과를 바꾸지 않으므로 해시 밖(data/rules/net.json, 섹션 34)이다.
// 결투 규칙 데이터(M14)보다 먼저, 넷코드 뼈대(M2)가 쓴다. 코드에 60/90 같은 값을 박지 않는다.
public sealed class NetRuleSet {
    public InputDelayStep[] InputDelayTable;          // 왕복 경계 ms → D. 처음 값: ≤60 → 2, ≤90 → 3, 그 밖 4 (online 4.12)
    public int RollbackLimitTicks;                    // 8
    public int DisconnectSilenceTicks, DisconnectGraceTicks;      // 끊김 판정까지의 무패킷 틱, 유예(spec 10의 20초)
    public int FullStateGapTicks, CatchupTicksPerAdvance;
    public int RttHoldCapMs, QueueRttLimitMs;         // QueueRttLimitMs = 120
    public int OrderViolationForfeit;                 // 30
    public int SpectatorDelayTicks, SpectatorKeyframeTicks;       // 120, 60
}   // 명세에 없는 처음 값은 [총괄 제안]이고 M2-NET-06의 측정으로 고친다
public sealed class SimLimitsDef { public uint RunMaxTicks; /* 판당 틱 상한. 재실행의 ReplayLimits */ }
public sealed class EnhanceTable { /* spec 08 4절: BaseProb[13], 비용 계수, 상한, 기운, 제물 점수 */ }
public sealed class EconomyTable { /* spec 08 6~7절: 상점 값, 수수료율, 감정가 표, 주간 한도 */ }
public sealed class DropTable { /* spec 07 2.7, 13절 */ }
public sealed class LeagueTable { /* spec 10 10~11절: IL 식의 계수, 리그 경계, 제한 */ }
public sealed class SeasonTable { /* spec 09 16절 */ }
```

### 2.3 팩과 조회

```csharp
// namespace Wizard.Content
public sealed class ContentPack {
    public int SchemaVersion { get; }
    public int DataVersion { get; }                   // 판마다 기록하는 데이터 판 번호
    public string CoreCompat { get; }                 // 이 팩이 맞는 코어 버전(SimVersion.CoreVersion)
    public ReadOnlyMemory<byte> Sha256 { get; }
    public static ContentPack Load(ReadOnlySpan<byte> bytes);     // 형식·해시가 틀리면 ContentException
}
public sealed class ContentDb {                       // 불변. 판과 규칙 함수에 주입한다
    public static ContentDb Build(ContentPack pack);
    public int DataVersion { get; }
    public CharacterDef Character(CharacterId id);
    public BlockDef Block(BlockId id);
    public ArtDef Art(ArtId id);
    public MutationDef Mutation(MutationId id);
    public EnemyDef Enemy(EnemyTypeId id);
    public BossDef Boss(BossId id);
    public WaveCurveDef WaveCurve { get; }
    public IReadOnlyList<FrameDef> Frames { get; }
    public IReadOnlyList<ModifierDef> Modifiers { get; }
    public ScoringDef Scoring { get; }
    public ArenaDef Arena(SimMode mode);
    public DuelRuleSet DuelRules { get; }
    public EnhanceTable Enhance { get; }
    public EconomyTable Economy { get; }
    public DropTable Drops { get; }
    public LeagueTable Leagues { get; }
    public SeasonTable Season { get; }
    // 전투 쪽(코어 버전 해시 안). 절이 팩에 없으면 ContentException
    public TrieDef Tries(CharacterId id);             // 캐릭터별 커맨드 트라이(spec 03 5절)
    public EliteDef Elite(EnemyTypeId baseType);
    public StarfallDef Starfall { get; }
    public IReadOnlyList<WeatherEventDef> WeatherEvents { get; }
    public BossRotationDef BossRotation { get; }      // 보스 순번(spec 06 2.1, spec 14 7.3)
    public TimeOfDayDef TimeOfDay(TimeOfDay tod);
    public ElementDef ElementRules { get; }           // 약점·저항 배율
    public SimLimitsDef SimLimits { get; }            // 판당 틱 상한 등 재실행이 읽는 한도(3.8절)
    // 규칙 쪽(해시 밖. src/Wizard.Content/Rules/). Wizard.Sim은 부르지 못한다(분석기)
    public ShopCatalog Shop { get; }
    public BagTable Bag { get; }
    public RunLimitsTable RunLimits { get; }          // ScoreCap·GoldCap 계수, whims_upper(spec 09 8.2)
    public IReadOnlyList<TitleDef> Titles { get; }
    public IReadOnlyList<CosmeticDef> Cosmetics { get; }
    public PlazaDef Plaza { get; }                    // 시설 자리, 채널 상수, 광장 하루 길이
    public NetRuleSet Net { get; }                    // 넷코드 값(입력 지연 표, 되감기 한도, 끊김 판정 …)
    public ItemDef Item(ItemCode code);               // 거래 가능 여부, 아이콘 열쇠
}
public enum SimMode : byte { Endless = 0, Duel = 1, Plaza = 2, Practice = 3 }
```

`ContentDb`는 부분 클래스다(T-33). 절 하나에 `ContentDb.<절>.cs` 하나이고, 위 접근자는 M1-CONT-02가 전부 선언한다(그 절을 읽는 코드가 아직 없으면 `ContentException`을 던지는 몸통으로). 뒤의 데이터 작업은 자기 절의 파일만 채운다.

**전투 쪽과 규칙 쪽의 구분**(T-18 개정). `data/sim/`의 절에 대응하는 정의와 절 읽기는 `src/Wizard.Content/{Defs, Pack/Sections}/`에 있고 코어 버전 해시에 들어간다. `data/rules/`의 절(enhance, economy, shop, drops, bag, leagues, il, fees, season, titles, cosmetics, plaza, limits, items, net, chat_filter)에 대응하는 것은 `src/Wizard.Content/Rules/{Defs, Sections}/`(네임스페이스 `Wizard.Content.Rules`)에 있고 해시에서 빠진다. `Wizard.Sim`이 `Wizard.Content.Rules`를 참조하면 컴파일 오류다. 재실행 결과를 바꾸는 값은 `data/sim/`에만 둘 수 있다. 판 안의 드랍 **기회** 기록에 쓰는 표가 필요하면 그 부분은 `data/sim/`의 절로 둔다.

팩 파일 형식은 11.2절.

## 3. `Wizard.Sim`

### 3.1 입력

소유: `Sim.Input`. 근거: spec 01 4절, spec 09 3.2~3.4, spec 10 14.2.

```csharp
// namespace Wizard.Sim.Input
public enum LogicalKey : byte {                       // 4비트
    Up = 0, Left = 1, Down = 2, Right = 3, Fire = 4, Skill = 5, Stance = 6, Reverse = 7, Pause = 8 }

public readonly struct InputEvent {
    public readonly uint Tick;                        // 유효 틱(결투는 누른 틱 + D)
    public readonly byte Offset;                      // 0..255, 1/256틱
    public readonly LogicalKey Key;
    public readonly bool Down;
    public readonly uint Seq;                         // 플레이어별 순번. 1부터 1씩. 정렬의 동률 해소에만 쓴다. 세계 상태와 해시에 넣지 않는다
    public InputEvent(uint tick, byte offset, LogicalKey key, bool down, uint seq);
    public long Time => (long)Tick * 256 + Offset;    // 입력 시각
    public byte PackKey() => (byte)((int)Key | (Down ? 0x10 : 0));     // 패킷·파일의 1바이트
}
public readonly struct QuantizedKey {                 // 순번이 아직 없는 키 이벤트. InputQuantizer가 낸다(6.5절)
    public readonly uint Tick; public readonly byte Offset; public readonly LogicalKey Key; public readonly bool Down;
}
public struct SeqCounter {                            // 플레이어 한 명의 순번 공간. InputRecorder와 DuelClientCore만 가진다
    public uint Last;                                 // 마지막으로 매긴 순번. 0이면 아직 없음
    public uint Next();                               // ++Last
}
[Flags] public enum KeyMask : ushort { None = 0 /* 비트 번호 = (int)LogicalKey */ }
public readonly struct PlayerInput {
    public readonly PlayerSlot Slot;
    public readonly InputEvent Event;
}
public static class InputOrder {
    public static int KeyClass(LogicalKey key);       // Stance·Reverse 0, 방향 1, Fire 2, Skill 3, Pause 4
    public static int Compare(in PlayerInput a, in PlayerInput b);    // (Tick, Offset, KeyClass, Slot, Seq)
    public static void Sort(Span<PlayerInput> inputs);                // DetSort
    public const int MaxEventsPerTickPerPlayer = 8;
}

public enum RunCommandOp : byte {
    PickCard = 1,        // A = 카드 번호(0~2)
    RerollCards = 2,
    DiscardBlock = 3,    // A = 야생 블록 인스턴스 번호
    SetSlot = 4,         // A = 슬롯 번호(0 = 코어), B = 블록 인스턴스 번호(0이면 비움)
    SwapSlots = 5,       // A, B = 슬롯 번호
    Heal = 6,
    UnlockArt = 7,       // A = (int)ArtId
    Depart = 8,
    AbandonRun = 9,
    CloseCampScreen = 10, // CampScreen → FireIdle (spec 07 11.4의 "닫기")
    EnterPractice = 11,   // CampScreen → Practice
    ExitPractice = 12,    // Practice → CampScreen
    // 200~255: 디버그 전용. SimConfig.AllowDebugOps가 꺼져 있으면 무효 명령이다(서버 프로필의 판과 재실행은 언제나 꺼짐)
    DebugSkipWave = 200, DebugInvulnerable = 201, DebugGrant = 202,      // A = 종류(골드·결정·블록), B = 양 또는 id
    DebugSetStage = 203,  // A = 단계. 이 판의 세계 안에서만 실효 단계를 바꾼다. 프로필의 지팡이는 바뀌지 않는다(DECISIONS 1.7)
}
public readonly struct RunCommand {                   // 키로 적을 수 없는 판 안의 조작
    public readonly uint Tick;                        // 이 틱의 키 이벤트보다 먼저 적용
    public readonly uint Seq;                         // 키 이벤트와 같은 순번 공간
    public readonly PlayerSlot Slot;
    public readonly RunCommandOp Op;
    public readonly int A, B;
}
```

규칙:

- **순번을 매기는 곳은 둘뿐이다**: `InputRecorder`(무한 웨이브·연습)와 `DuelClientCore`(결투). `InputQuantizer`는 순번 없는 `QuantizedKey`를 낸다. 한 틱 안에서는 명령이 먼저, 그다음 키를 `(Offset, KeyClass)` 순으로 번호를 매긴다. 한 틱의 아홉 번째 키부터는 **번호를 매기기 전에** 버린다(결투는 서버도 버린다). 그래서 기록 파일의 레코드 순서 = 순번 순서이고, 실행 중의 순번과 재실행 때 다시 매긴 순번이 같다.
- `Seq`는 정렬의 동률 해소와 결투의 확정 대조에만 쓴다. 세계 상태(보관된 K, 판정기 버퍼 등)와 해시에 넣지 않는다. 상태가 "어느 입력이었나"를 기억해야 하면 입력 시각(`Time`)을 쓴다.
- 눌린 키의 누름, 안 눌린 키의 뗌은 `KeyState`가 무시한다.
- **틱을 멈추는 것은 일시정지뿐이다**(T-13 개정, 0.3절). 전리품 창과 야영 화면이 떠 있어도 틱은 진행하고, `RunCommand`는 발생한 틱 번호를 달고 그 틱의 0단계에서 적용된다. 화면은 세계 상태(`ISimView.Camp`)를 그대로 읽는다. 일시정지는 틱을 멈추고 실제 시간을 `PauseMark`로 기록한다(순위 판의 10분 한도. 결투에는 일시정지가 없다).
- 창이 떠 있는 동안 클라이언트는 키 이벤트를 세계에 넣지 않고(화면이 쓴다), 창이 닫히면 "논리 키 상태의 차이"를 이벤트로 만든다(spec 01 3.6의 일시정지 해제와 같은 규칙). 코어도 야영 하위 상태에 따라 키를 무시한다(3.2절 표). 조작된 기록이 창 중에 키를 넣어도 결과가 바뀌지 않는다.
- 무효한 `RunCommand`(야영이 아닌데 `Heal`, 하위 상태가 허용하지 않는 명령, `AllowDebugOps`가 꺼졌는데 디버그 op)는 적용하지 않고 디버그 이벤트만 낸다. 클라이언트의 `RunSession`은 `SimWorld.CanApply`로 확인한 명령만 기록하므로 정직한 기록에는 무효 명령이 없다. **재실행에서 무효 명령이 하나라도 나오면 `ReplayStatus.RecordCorrupt`다**(3.8절).

### 3.2 세계, 틱, 해시, 스냅숏

소유: `Sim.Runtime`. 근거: spec 01 3절·12절·13절.

```csharp
// namespace Wizard.Sim
public static class SimVersion {
    public const string CoreVersion = "…";            // Generated/SimVersion.g.cs. 재실행에 영향을 주는 소스(T-18의 대상 목록) SHA-256의 앞 16바이트(hex 32자)
    public const byte FactsFormat = 1;                // 이 코어가 쓰는 RunResultFacts 형식 판(3.7절)
    public const int TickRate = 60;
}

public sealed class SimConfig {                       // 모드 스위치. spec 01 12.1의 표
    public SimMode Mode;
    public bool HitRadiusOverrideOn; public Fix64 HitRadiusOverride;
    public int InvulnTicksOverride;                   // −1이면 캐릭터 값
    public int BurstCapPermille, BurstWindowTicks;
    public int GrazeCooldownTicks; public bool GrazeFullReward;
    public int PenaltyWindowTicks, SkillPrepTicks, StatusApplyDelayTicks;
    public int AimAssistCenti;
    public int PlayerProjectileCap, EnemyProjectileCap; public bool ProjectileCapPerPlayer;
    public bool FireEnabled, SkillEnabled, StarfallEnabled, UnstableCoreSelfBlowup, AllArtsUnlocked;
    public bool AllowDebugOps;                        // RunCommandOp 200~255를 받을지. ForEndless·ForDuel·ForPlaza는 false. 오프라인 개발 빌드의 디버그 메뉴와 연습만 켠다
    public static SimConfig ForEndless(ContentDb db);
    public static SimConfig ForDuel(ContentDb db, DuelRoomOptions options);
    public static SimConfig ForPlaza(ContentDb db);
    public static SimConfig ForPractice(ContentDb db, SimConfig basedOn);
}

public sealed class WandSnapshot {                    // 서버가 아는 지팡이 상태. run.wand_snapshot, duel_match.snapshots
    public CharacterId Character; public int Stage; public int SlotsExtra;
    public MutationId[] Mutations;                    // 받은 순서
    public BlockId Core; public BlockId[] Links;      // 길이 = 기본 연결 칸 + SlotsExtra
}
public sealed class EndlessSetup {
    public Guid RunId; public ulong Seed; public RunKind Kind;
    public int StartWave;                             // 1, 11, 21, …
    public bool PhantomFirst;
    public WandSnapshot Wand; public ArtMask UnlockedArts;
    public bool SeasonTagOn;                          // 시즌 주화·한정 드랍 기회를 기록할지
    // 판의 봉투 값(T-46). 이 판을 돌릴 코어와 팩이다. 세계는 읽지 않는다(상태와 해시에 들어가지 않는다).
    // 맞는 코어·팩으로 돌리는지는 호출자가 본다(클라이언트는 출발 때, 워커는 대기열 조건과 팩 저장소에서).
    // 서버는 지금 링크한 코어의 상수가 아니라 출발 요청의 core_version을 넣는다(T-18). 그래서 상수가 아니라 칸이다.
    public string CoreVersion;                        // hex 32자(소문자). SimVersion.CoreVersion과 같은 꼴. 새 설정은 빈 문자열이라 채우기 전에는 쓰지 못한다
    public int DataVersion;                           // ContentDb.DataVersion. 0 이상
    public const ushort CanonicalFormatVersion = 1;   // .wzrc의 format_version
    // 정규 바이트 = .wzrc 머리말 그대로(11.1절): magic부터 unlocked_arts까지. 서버 프로필에서는 서버가 StartRun 때 만들어
    // run.setup_canonical에 저장하고 RunStartTicket.setup으로 내려 준다. 클라이언트는 그 바이트를 읽어 쓰고 다시 조립하지 않는다.
    // WriteCanonical은 w의 끝에 덧붙인다. 칸이 형식에 담기지 않으면(null, 소문자 hex 32자가 아닌 CoreVersion, 음수, u8·u16을 넘는 값,
    // 255개를 넘는 배열, RunKind에 없는 Kind) 아무것도 쓰지 않고 InvalidOperationException을 던진다(설정을 만든 코드의 결함. 틱 밖에서만 부른다).
    public void WriteCanonical(ByteWriter w);
    // 신뢰할 수 없는 바이트를 읽는다. 예외 없음. 실패하면 setup은 null이다([NotNullWhen(true)] out EndlessSetup?).
    // true인 입력은 WriteCanonical이 다시 쓰면 같은 바이트가 된다(한 설정에 바이트 열 하나): 다른 magic·format_version, RunKind에 없는 kind,
    // 0·1이 아닌 참거짓, int를 넘는 data_version, 모자라거나 남는 길이는 false다.
    // 캐릭터·변이·블록 id가 그 팩에 있는지, 시작 웨이브가 규칙에 맞는지는 보지 않는다(팩과 규칙을 모른다).
    public static bool TryReadCanonical(ReadOnlySpan<byte> bytes, out EndlessSetup setup);       // bytes 전체가 정규 바이트. 뒤에 남으면 false
    public static bool TryReadCanonicalPrefix(ReadOnlySpan<byte> bytes, out EndlessSetup setup, out int length);
                                                      // 기록 파일의 앞머리에서 읽는다. length = 머리말의 길이. InputRecordCodec.TryRead가 쓴다
}
public sealed class DuelSetup {
    public Guid MatchId; public ulong Seed; public DuelRoomOptions Options;
    public DuelPlayerSetup[] Players;                 // 길이 2. 리그 제한을 적용한 뒤의 값
    public void WriteCanonical(ByteWriter w);         // WZDR 머리말(11.1절). DuelRoomOptions의 칸과 WZDR 머리말의 칸 배치가 정해진 뒤에 더한다.
                                                      // 그때까지 코드에 선언이 없다(T-46의 따라오는 일)
}
public sealed class DuelPlayerSetup {                 // 입력 지연 D는 여기에 없다. 시뮬레이션은 D를 모른다(0.3절, 10.5절)
    public PlayerSlot Slot; public WandSnapshot AppliedWand; public int ActualStage;
    public int MaxHpPoints; public ArtMask UnlockedArts;
}
public enum RunKind : byte { Normal = 0, Weekly = 1 }

public sealed class SimWorld {
    public static SimWorld CreateEndless(ContentDb db, SimConfig config, EndlessSetup setup);
    public static SimWorld CreateDuel(ContentDb db, SimConfig config, DuelSetup setup);
    public static SimWorld CreatePractice(ContentDb db, SimConfig config, PracticeSetup setup);

    public uint Tick { get; }                         // 다음에 처리할 틱
    public SimMode Mode { get; }
    public int PlayerCount { get; }
    public bool IsOver { get; }

    // 틱 하나를 진행한다. inputs는 Tick == this.Tick인 것만, InputOrder 순으로, 플레이어당 8개 이하여야 한다.
    // commands는 Tick == this.Tick, Seq 오름차순이어야 한다. 이 전제는 모든 빌드에서 검사하고 어기면 SimInvariantException이다
    // (이벤트가 16개 이하라 비용이 없다). 신뢰할 수 없는 기록은 InputRecordCodec.TryRead가 먼저 거른다(3.8절).
    public void Step(ReadOnlySpan<PlayerInput> inputs, ReadOnlySpan<RunCommand> commands, ISimEventSink sink);
    public bool CanApply(in RunCommand command);      // 지금 상태에서 이 명령이 유효한가. 상태를 바꾸지 않는다. RunSession이 기록 전에 부른다

    public ulong ComputeStateHash();                  // 정규 직렬화의 FNV-1a 64
    public void WriteState(ByteWriter target);        // 상태 전체(재접속, 해시 불일치 복구, 관전 시작)
    public void ReadState(ReadOnlySpan<byte> state);

    public SimSnapshot CreateSnapshotBuffer();        // 이 세계에 맞는 크기의 빈 스냅숏
    public void SaveSnapshot(SimSnapshot target);     // 값 복사. 할당 없음
    public void LoadSnapshot(SimSnapshot source);

    public ISimView View { get; }                     // 읽기 전용
    public RunOutcome GetOutcome();                   // IsOver 뒤(무한 웨이브). 3.7절
    public DuelOutcome GetDuelOutcome();              // IsOver 뒤(결투)
}
public sealed class SimSnapshot { public uint Tick { get; } /* 내용은 불투명 */ }
```

`Step`의 처리 순서(유일한 순서. 같은 단계 안에서는 개체 id 오름차순, 플레이어는 슬롯 순):

```
 0. RunCommand 적용(순번 순). 야영·전리품 단계에서만 뜻이 있다
 1. 기한 처리: 잠금 풀림, 지연 효과 큐(결투의 3틱 지연) 중 이번 틱에 닿은 것 적용
 2. 입력 이벤트 적용(InputOrder 순): KeyState → 자세 → 시야 → 판정기(방향, K) → 일시정지
 3. 시야의 대각 유지 창 만료
 4. 플레이어 이동과 벽
 5. 비기 발동: 이번 틱에 판정된 K, 준비가 끝난 비기(결투 2틱), 보관된 K
 6. J 발사: 과열 판정 → ISpellExecutor.Execute → 열 반영
 7. 웨이브 디렉터(묶음 방출, 스폰) → 적·보스 AI: 상태 전이, 이동 의도, 예고 요청
 8. 예고 대기 줄: 동시 예고 상한과 R5를 통과한 요청을 구역으로 만든다
 9. 이동과 충돌: 적 이동 → 탄 이동(플레이어 탄, 적 탄 순) → 플레이어 탄↔적 → 적 탄·돌진·접촉↔플레이어
    → 판정 틱이 된 예고 구역 → 피격 판정 뒤 스침 판정 → 줍기(별똥, 체력 조각)
10. 피해 확정과 사망 처리(유폭 예약, 드랍 기회, 처치 점수·골드, 탐식), 상태 효과·지대 만료
11. 틱 끝 갱신: 냉각, 콤보 끊김 타이머, 별똥, 웨이브·야영·라운드 상태 기계(클리어, 점수 확정, 가시밭 고리)
12. Tick += 1
```

계약:

- 세계는 스레드 하나에서만 부른다. 세계끼리는 상태를 공유하지 않는다(서버는 여러 세계를 여러 스레드에서 동시에 돌린다). 코어에는 가변 정적 필드가 없으므로 임시 버퍼도 세계가 가진다.
- 모든 상태는 값 형식 배열, `SortedIntList`, 고정 크기 풀에 있다. 불변인 `ContentDb`만 참조로 가리킨다. 컴파일된 주문은 `SpellTable`의 정수 핸들로 가리킨다.
- `LoadSnapshot(s)` 뒤에 같은 입력으로 `Step`을 부르면 저장하지 않고 진행한 것과 같은 해시가 나와야 한다.
- 해시를 계산하는 시점: 무한 웨이브는 5웨이브마다의 중간 지점과 판 끝(호출자가 부른다), 결투는 30틱마다. 디버그 빌드는 매 틱 남길 수 있다.

**`SpellTable`은 세계 상태다.**

- 항목 = 원천 `(core, links[], WandCombatProfile의 정규 바이트)`. `CompiledSpell` 객체는 원천에서 다시 만들 수 있는 캐시일 뿐이고 상태가 아니다.
- 핸들 = 추가 순서의 색인(0부터). 같은 원천(바이트가 같다)은 새 항목을 만들지 않고 이미 있는 핸들을 돌려준다(앞에서부터 선형으로 찾는다. `Dictionary`를 순회하지 않는다).
- 표는 추가만 한다. 스냅숏에는 표의 **길이**가 들어가고, `LoadSnapshot`은 길이를 그 값으로 줄인다(되감은 뒤 다시 돌리면 같은 순서로 다시 채워진다). `WriteState`와 상태 해시에는 길이와 모든 항목의 원천이 들어간다. `ReadState`는 원천에서 다시 컴파일해 복원한다. 받은 쪽의 모든 핸들이 실행 가능해야 한다(재접속, 관전 시작, 해시 불일치 복구).
- 새 핸들이 생기는 곳은 세계 생성과 `Step` 0단계(`Depart`로 조립이 확정될 때)뿐이다. 사본 마법진과 허상의 복사는 이미 있는 핸들을 쓴다. 5~10단계에서는 컴파일하지 않는다. 0단계의 컴파일은 `Step` 할당 0의 유일한 예외다(결투에는 0단계 명령이 없으므로 결투의 `Step`은 언제나 할당 0이다).

**상태 절과 해시.**

- 세계 상태는 절로 나뉘고, 절마다 `(절 id u16, 내용)`으로 직렬화한다. 절 id는 아래 훅 표의 모듈 번호다.
- 개체 풀과 목록만으로 이루어진 절(투사체, 적, 예고, 지대, 상태 효과 등)은 **개수가 0이면 절 id까지 통째로 생략한다**. 스칼라 상태를 가진 절(플레이어, 과열, 콤보, 웨이브·야영, 결투 라운드, 난수 흐름, `SpellTable`)은 언제나 쓴다. 이 규칙은 해시와 `WriteState`에 똑같이 적용된다. 그래서 풀 모듈을 새로 더하는 것만으로는 그 모듈을 쓰지 않는 옛 골든의 해시가 바뀌지 않는다.
- 판정기 상태(`ICommandParser`), 보관된 K, 지연 효과 큐도 다른 상태와 똑같이 스냅숏·`WriteState`·해시에 들어간다(3.5절).

**부분 클래스와 훅**(T-33). `SimWorld`는 부분 클래스다. 모듈은 자기 폴더에 `SimWorld.<모듈>.cs`를 두고(네임스페이스는 `Wizard.Sim`. "네임스페이스 = 폴더"의 허용된 예외다) 훅을 구현한다. C#의 부분 메서드는 구현이 하나뿐이어야 하므로 훅은 **(단계, 모듈)마다 따로** 선언한다. 선언은 M1-SIM-02가 아래 표대로 전부 해 두고, 호출 순서는 `Step` 한 곳에 고정한다.

| 모듈(절 id) | 폴더 | 훅 (`partial void`) |
|---|---|---|
| Player(1) | `Player/` | `Stage02_Player`, `Stage03_Player`, `Stage04_Player`, `Stage10_Player` |
| Heat(2) | `Heat/` | `Stage01_Heat`, `Stage06_Heat`, `Stage11_Heat` |
| Graze(3) | `Graze/` | `Stage09_Graze` |
| Combo(4) | `Combo/` | `Stage10_Combo`, `Stage11_Combo` |
| Spell(5), SpellTable(6) | `Spell/` | `Stage00_Spell`, `Stage06_Spell`, `Stage09_Spell`, `Stage10_Spell` |
| Arts(7) | `Arts/` | `Stage02_Arts`, `Stage05_Arts` |
| Characters(8) | `Characters/` | `Stage01_Characters`, `Stage06_Characters`, `Stage10_Characters` |
| Enemies(9), Telegraph(10) | `Enemies/` | `Stage07_Enemies`, `Stage08_Telegraph`, `Stage09_Enemies`, `Stage10_Enemies` |
| Bosses(11) | `Bosses/` | `Stage07_Bosses`, `Stage10_Bosses` |
| Waves(12), Camp(13), Outcome(14) | `Waves/` | `Stage00_Camp`, `Stage07_Waves`, `Stage10_Waves`, `Stage11_Waves` |
| Duel(15) | `Duel/` | `Stage01_Duel`, `Stage09_Duel`, `Stage11_Duel` |
| Rng(16) | `Runtime/Core/` | (훅 없음. 웨이브·라운드 시작 때 다시 파생) |
| Debug(17) | `Runtime/Debug/` | `Stage00_Debug` |

모듈마다 상태 훅 넷도 같은 방식으로 선언한다: `Snapshot_Save_<모듈>`, `Snapshot_Load_<모듈>`, `State_Write_<모듈>(ref StateWriter)`, `State_Read_<모듈>(ref StateReader)`. 해시는 `State_Write`를 해시 모드로 부른 것이다(직렬화와 해시가 갈라질 수 없다). 한 단계 안의 모듈 호출 순서는 위 표의 위에서 아래 순이다. 훅을 더하거나 순서를 바꾸는 것은 계약 변경이다.

**야영 하위 상태**(`ISimView.Camp.Sub`. 정본 상태이고 해시에 들어간다. spec 07 11.4).

| `CampSub` | 받는 키 | 받는 명령 | 나가는 길 |
|---|---|---|---|
| `LootPick` | 없음(전부 무시) | `PickCard`, `RerollCards`, `DiscardBlock`(넘칠 때), `AbandonRun` | 고를 수만큼 확정되면 `FireIdle` |
| `FireIdle` | 이동, 자세, J(모닥불 옆 = `CampScreen`, 출발 표식 = `DepartHold`) | `AbandonRun` | 키 |
| `CampScreen` | 없음 | `SetSlot`, `SwapSlots`, `DiscardBlock`, `Heal`, `UnlockArt`, `EnterPractice`, `CloseCampScreen`, `Depart`, `AbandonRun` | `CloseCampScreen` → `FireIdle`, `Depart` → 다음 웨이브 |
| `Practice` | 전부(허수아비 연습. 무엇이 판에 남는지는 spec 07·13의 연습 규칙) | `ExitPractice`, `AbandonRun` | `ExitPractice` → `CampScreen` |
| `DepartHold` | J 유지(60틱), 떼면 `FireIdle` | `AbandonRun` | 60틱 → 다음 웨이브 |

- 표에 없는 명령은 무효다(3.1절). `LootPick`이 미결인 채 다음 웨이브로 가는 전이는 없다. 카드를 고르지 않은 기록은 그 자리에서 판이 끝날 때까지 머문다(`EndedLate` 또는 기록 끝).
- 키로 일어나는 전이(`FireIdle` → `CampScreen`, `DepartHold`)는 입력 이벤트로 기록되고, 화면 조작으로 일어나는 전이는 위 명령으로 기록된다. 기록되지 않는 전이는 없다.

### 3.3 표시 이벤트와 읽기 전용 뷰

소유: `Sim.Events`. 근거: 각 명세의 "시각/소리 연결점" 절, spec 01 17절, spec 10 23절.

```csharp
// namespace Wizard.Sim.Events
public struct SimEvent {                              // 고정 크기, 참조 없음
    public SimEventType Type;
    public PlayerSlot Slot;                           // 관련 플레이어. 없으면 None
    public uint Tick;
    public EntityId Source, Target;
    public int A, B, C;                               // 종류별 인자(아래 표와 SimEventDocs)
    public long X, Y;                                 // 위치(Fix64 Raw). 없으면 0
    public long V;                                    // 큰 값(피해 Raw 등)
}
public interface ISimEventSink { void Emit(in SimEvent e); }

public enum SimEventType : ushort {
    // 100~199 입력·이동·자세 (spec 01 17.2)
    StanceEntered = 100, StanceExited = 101, ReverseFlipped = 102, FacingChanged = 103, CommandReset = 104,
    // 200~299 과열
    ShotFired = 200, CritShotFired = 201, CritHit = 202, HeatEnteredImminent = 203, HeatEnteredCritical = 204,
    HeatEnteredBrink = 205, HeatLockEntered = 206, FireDryWhileLocked = 207, HeatUnlocked = 208,
    HeatExemptArmed = 209, HeatExemptConsumed = 210, KilnAbsorbed = 211, BoiloverTriggered = 212,
    UnstableCoreExploded = 213,
    // 300~399 스침·콤보·결정·체력
    GrazeAwarded = 300, GrazeCosmeticOnly = 301, GrazeBuffStarted = 302, GrazeBuffEnded = 303,
    LightningRodStrike = 304, ComboChanged = 310, ComboReset = 311, CrystalGained = 320, CrystalsFull = 321,
    CrystalGainBlocked = 322, CrystalsSpent = 323, CrystalsShattered = 324, CrystalProtected = 325,
    CrackChanged = 326, DebtCreated = 327, DebtRepaid = 328, CrystalsConverted = 329,
    StarfallTelegraph = 340, StarfallLanded = 341, StarfallBlink = 342, StarfallPicked = 343, StarfallExpired = 344,
    PlayerHit = 360, InvulnEnded = 361, PlayerDowned = 362, PlayerRose = 363, HitstopRequest = 370,
    // 400~499 주문·투사체 (spec 02 19절)
    SpellNodeExecuted = 480,   // A = 실행 트리의 마디 번호(SpellPreview의 마디 번호와 같다), B = ShotId. HUD의 지팡이 회로 점등(GDD 13)
    // 500~599 비기·판정기 (spec 03 21절, spec 04 18절)
    // 600~699 적·예고·꼬리표 (spec 05 14절)
    // 700~799 보스·첫 만남·복수 (spec 06 17절)
    // 800~899 웨이브·야영·전리품·점수 (spec 07 20절)
    // 900~999 결투 (spec 10 23절)
    // 1000~ 디버그
}
public static class SimEventDocs {
    public static string BindingKey(SimEventType type);            // "heat.lock_entered" 꼴. event_bindings.json의 열쇠
}

public interface ISimView {                           // 표시 계층이 매 프레임 읽는다. 코어 상태를 바꾸지 못한다
    uint Tick { get; }
    int PlayerCount { get; }
    PlayerView Player(PlayerSlot slot);
    ReadOnlySpan<EnemyView> Enemies { get; }
    ReadOnlySpan<ProjectileView> Projectiles { get; }
    ReadOnlySpan<TelegraphView> Telegraphs { get; }   // 시작 틱과 길이를 준다. 표시는 이것으로만 채움을 계산한다
    ReadOnlySpan<HazardView> Hazards { get; }
    WaveView Wave { get; }
    CampView Camp { get; }
    DuelView Duel { get; }
}
public readonly struct PlayerView {                   // spec 01 17.1의 표
    public readonly EntityId Id; public readonly CharacterId Character;
    public readonly FixVec2 Pos; public readonly Dir8 Facing; public readonly StanceMode Stance;
    public readonly int HpPoints, MaxHpPoints; public readonly bool Invulnerable;
    public readonly int HeatHu, CritStartHu; public readonly HeatRingState HeatRing; public readonly bool ExemptArmed;
    public readonly int LockRemainTicks, LockTotalTicks;
    public readonly int Combo, ComboIdleTicks; public readonly bool ComboTimerPaused;
    public readonly int Crystals, CrystalCap, Debt, Crack;
    public readonly bool GrazeBuffActive;
    public readonly ArtPreview Preview;               // "지금 K = …" (spec 03 11절)
    public readonly RecentInputs Recent;              // 최근 입력 5개
    public readonly KeyMask Keys;                     // 지금의 논리 키 상태(결투의 키 상태 맞추기, 6.4절)
}
public readonly struct ArtPreview {                   // spec 03 11절. 벡터 B08·B13·B14·B15·B15b·B37
    public readonly ArtId JustArt; public readonly int JustCost;       // 섬 자리의 기술. 없으면 None
    public readonly ArtId NormalArt; public readonly int NormalCost;   // 일반 간격의 기술. 둘 다 있으면 HUD가 두 줄로 그린다(B15)
    public readonly int CreditCount;                  // 외상으로 낼 결정 수(그믐, B13). 0이면 없음
    public readonly int FizzleInputIndex;             // 불발이면 몇 번째 입력이 늦었는가(1부터). 0이면 불발 아님(B37)
    public readonly int FizzleLateByUnits;            // 늦은 양(입력 시각 단위)
}
public readonly struct RecentInputs {                 // 최대 5개. 0번이 가장 최근
    public readonly int Count;
    public CmdDir Dir(int index); public bool Chained(int index);      // chained = 앞 입력과 이어짐(간격 안)
}
```

`CampView`는 `Sub`(`CampSub`), 방금 넘긴 웨이브, 이 야영의 회복 횟수, 전리품 카드, 보관함, 해금 차례를 준다. 화면은 이것을 그대로 읽고 따로 상태를 두지 않는다.

- 400번대 이후의 이벤트는 각 명세의 연결점 절에 있는 이름을 그 구간에 그대로 옮긴다. 열거형 이름은 PascalCase, 데이터 열쇠는 `구역.snake_case`다(예: `TelegraphStart` ↔ `telegraph.start`).
- 번호는 추가만 한다. 다시 보기 파일은 이벤트를 담지 않으므로(입력만 담는다) 번호 변경이 기록을 깨지는 않지만, 바인딩 표와 맞추려고 고정한다.
- 결투에서 예측 중인 틱의 이벤트를 어떻게 다루는지는 6.4절(`PresentationEvent`).

### 3.4 주문 컴파일러

소유: `Sim.Spell`. 근거: spec 02 5~11절.

```csharp
// namespace Wizard.Sim.Spell
public readonly struct WandCombatProfile {
    public readonly CharacterId Character; public readonly int BaseLinks, ExtraSlots;
    public readonly PowBase DepthDecay;               // Decay085 또는 Decay092
    public readonly bool UnderlinePassive; public readonly MutationSet Mutations;
}
public readonly struct SpellHandle { public readonly int Value; }

public sealed class CompiledSpell {                   // 불변
    public bool IsValid; public int FireIntervalTicks;
    public SpellScope[] Scopes;                       // 길이 1~4
    public int HeatPerShotHu;                         // 거울상 +50% 전의 값
    public int InstancesTotal, InstancesTotalMirrored, MaxDepthNormal, MaxDepthMirrored;
    public Fix64 StandardT;                           // spec 02 14.2
    public CompileDiagnostic[] Diagnostics;
    public ulong Hash;
}
public interface ISpellCompiler {
    CompiledSpell Compile(BlockId core, ReadOnlySpan<BlockId> links, in WandCombatProfile profile);
    SpellPreview BuildPreview(CompiledSpell spell);   // 조립 화면용(spec 02 14.4)
    string DebugPrint(CompiledSpell spell);           // 글자 출력(spec 02 14.5)
}

public enum Faction : byte { Player = 0, Enemy = 1 }
public enum SourceKind : byte { Fire = 0, ArtBurst = 1, ArtOther = 2, CopyCircle = 3, EnemyCopy = 4 }
public readonly struct ExecuteOptions {
    public readonly EntityId Owner;                   // 발사한 개체(플레이어 또는 허상)
    public readonly bool IsCrit, GlassCritBonus;
    public readonly int ShotId;                       // 발사 1회의 묶음(콤보 +3 상한, 결투 스침 묶음)
    public readonly SourceKind Source;
    public readonly int EnhanceStage;                 // 실행 순간의 실효 단계
    public readonly Fix64 CharacterMul;
    public readonly int ProjectileBudget;             // 비기 160 상한의 남은 몫. 제한 없으면 int.MaxValue
    public readonly EntityId AimTarget;               // 결투 조준 보정 대상. 없으면 None
}
public readonly struct ExecuteResult {
    public readonly int HeatToAddHu; public readonly int Spawned, Omitted; public readonly bool SelfDetonated;
}
public interface ISpellExecutor {
    // GDD 4절의 여섯 인자 + 옵션. J 발사, 비기, 사본 마법진, 허상의 복사가 모두 이것을 부른다.
    ExecuteResult Execute(SpellHandle tree, FixVec2 position, Angle direction, Fix64 damageMul,
                          bool heatExempt, Faction faction, in ExecuteOptions options);
}
```

- `SpellHandle`은 세계의 `SpellTable` 색인이다. 표는 세계 상태이고 스냅숏·`WriteState`·해시에 들어간다(3.2절). 실행기는 실행한 마디마다 `SpellNodeExecuted`를 낸다(HUD의 회로 점등).
- 변이 난수(`RngStream.Mutation`)와 모드는 실행기가 세계에서 읽는다. 옵션으로 넘기지 않는다.
- 컴파일은 순수 함수다(같은 입력이면 같은 `Hash`). 조립 화면은 세계 없이 `ISpellCompiler`만 써서 미리보기를 만든다.
- 진단 코드와 이름 표 열쇠(`warn.core_empty` 등)는 spec 02 6.3절.

### 3.5 커맨드 판정기

소유: `Sim.Arts`. 근거: spec 03 5~11절.

```csharp
// namespace Wizard.Sim.Arts
public enum CmdDir : byte { Forward = 0, Back = 1, Left = 2, Right = 3 }

public sealed class CommandTrie {                     // 캐릭터별. 불변
    public static CommandTrie Build(CharacterId character, ContentDb db);    // 아호-코라식 실패 링크, DFA 전이 표
    public int NodeCount { get; }
    public ref readonly TrieNode Node(int id);
    public int Next(int node, CmdDir dir);            // 입력당 O(1)
}
public interface ICrystalWallet {                     // PlayerState가 구현한다
    int Crystals { get; set; } int Cap { get; } int Debt { get; set; } bool HasCredit { get; }
}
public enum CommandResetReason : byte { StanceEnter, StanceExit, Reverse, Pause, WaveStart, RoundStart, Fired, Fizzled, Timeout }

public interface ICommandParser {
    void Reset(CommandResetReason reason);
    void OnStanceChanged(bool inStance, long time);
    void OnDirection(CmdDir dir, long time);                               // 자세 밖이면 무시
    bool OnSkillPress(long time, ICrystalWallet wallet, in UnlockSet unlocks, out ArtDecision decision);
    bool TryTakeBuffered(long tickEndTime, out ArtDecision decision);      // 재사용 대기 끝에 나가는 보관된 K
    CommandPreview Preview(long now, ICrystalWallet wallet, in UnlockSet unlocks);   // HUD "지금 K ="
    void WriteState(ref StateWriter w);                                    // 커서, 방향 시각 8개, 대기 끝, 보관 결과
    void CopyFrom(ICommandParser other);                                   // 스냅숏
}
```

- 시각은 전부 입력 시각(`long`)이다. 간격 비교는 정수다(0.07초 = 1,075).
- 판정기 상태(커서, 방향 시각 버퍼, 재사용 대기, 보관된 K)는 다른 상태와 똑같이 스냅숏·`WriteState`·해시에 들어가고, 되감기 때 복원해 다시 돌린다. 내 입력이 모두 제때 확정되면 커서와 판정 시각은 다시 돌려도 같다(spec 10 14.11의 뜻은 이것이다). 그러나 비용과 대체 기술의 결정(`ArtDecision`)은 지갑 상태를 읽으므로 상대의 적중이 늦게 확정되면 달라질 수 있고, 내 이벤트가 서버에서 버려지면 판정기의 입력 열 자체가 바뀐다. 판정기 상태를 복원 대상에서 빼면 서버와 갈린다.
- 판정기는 순번(`Seq`)을 상태에 넣지 않는다. 입력 시각만 넣는다(3.1절).
- `ArtDecision`의 필드는 spec 03 7.4절 그대로이고 `ArtId`만 2.1절의 정본을 쓴다.

### 3.6 적과 보스

소유: `Sim.Enemies`, `Sim.Bosses`. 근거: spec 05 2~3절, spec 06 1~2절.

```csharp
// namespace Wizard.Sim.Enemies
public struct EnemyState { /* spec 05 2.4. Def 참조 대신 EnemyTypeId, AI 변수는 고정 길이 int 배열 */ }
public readonly struct EnemyContext {                 // 두뇌가 읽을 수 있는 것
    public readonly uint Tick; public readonly int Wave;
    public readonly ISimView World;
    public readonly IArenaQuery Arena;                // 벽, 기둥, 설치 벽, 레이캐스트
    public readonly ITelegraphService Telegraphs;
}
public struct EnemyIntent {                           // 두뇌가 쓰는 것. 세계가 9단계에서 적용한다
    public FixVec2 MoveDelta; public bool WantsAttack; public int AttackIndex; public FixVec2 AimPoint;
}
public interface IEnemyBrain {                        // 종마다 하나. 상태가 없다(상태는 EnemyState에)
    AiKind Kind { get; }
    void OnSpawn(ref EnemyState self, in EnemyContext ctx, ref Pcg32 rng);
    void Think(ref EnemyState self, in EnemyContext ctx, ref Pcg32 rng, ref EnemyIntent intent);
    void OnTelegraphGranted(ref EnemyState self, in EnemyContext ctx, int zoneId);
    void OnStrike(ref EnemyState self, in EnemyContext ctx, IEnemyActions actions);     // 탄 생성, 돌진 시작 등
    void OnInterrupted(ref EnemyState self, in EnemyContext ctx);
    void OnDeath(ref EnemyState self, in EnemyContext ctx, IEnemyActions actions);      // 유폭, 갈라짐
}
public interface ITelegraphService {
    int Request(in TelegraphRequest request);         // 요청 번호. 허가는 8단계에서(상한, R5)
    bool IsGranted(int requestId, out int zoneId);
    void Cancel(int zoneOrRequestId);
}
public interface ISafePathChecker {                   // R5. 정수만 쓴다(spec 05 10절)
    bool HasSafePath(FixVec2 playerPos, CharacterId character, ReadOnlySpan<TelegraphZone> zones,
                     ReadOnlySpan<LingeringHazard> hazards, in TelegraphRequest candidate, uint now);
}

// namespace Wizard.Sim.Bosses
public interface IBossBrain {
    BossId Id { get; }
    void OnIntro(ref BossState self, in EnemyContext ctx);
    void Think(ref BossState self, in EnemyContext ctx, ref Pcg32 rng, IBossActions actions);
    void OnPhaseChanged(ref BossState self, int phase, in EnemyContext ctx, IBossActions actions);
    void OnDeath(ref BossState self, in EnemyContext ctx, IBossActions actions);
}
```

- 두뇌는 `EnemyBrainRegistry`(코드에 고정된 표)가 `AiKind`로 찾는다. 리플렉션을 쓰지 않는다.
- 두뇌는 세계를 직접 고치지 않는다. `EnemyIntent`, `ITelegraphService`, `IEnemyActions`로만 요청한다.
- 허상의 복사 발사는 `IBossActions`를 거쳐 `ISpellExecutor.Execute(…, Faction.Enemy, …)`를 부른다.

### 3.7 웨이브, 점수, 판의 결과

소유: `Sim.Waves`. 근거: spec 07 4~14절, spec 09 3.7.

```csharp
// namespace Wizard.Sim.Waves
public sealed class WavePlan {                        // (씨앗, 웨이브, 직전 틀)에서 정해지는 것
    public int Wave; public TimeOfDay Tod; public FrameId Frame;
    public ModifierId Modifier1, Modifier2;
    public BossId Boss; public bool PhantomFirstEncounter;
    public SpawnBundle[] Bundles; public int EliteCount; public int Budget;
}
public interface IWaveDirector {
    WavePlan Plan(ulong seed, int wave, FrameId previousFrame, bool phantomFirst);    // 순수 함수
    void Begin(WavePlan plan);
    void Tick(uint tick, IEnemySpawner spawner, ref Pcg32 spawnRng);                  // 묶음 방출, 지체 방지
    bool AllReleased { get; }
}
public static class WaveMath {                        // 코어 안에서만 쓴다. wizard-api의 판 처리 경로는 부르지 않는다(T-18 개정)
    public static TimeOfDay TodOf(int wave);
    public static int DayOf(int wave);
    public static BossId BossForWave(int wave, bool phantomFirst, ContentDb db);
    public static void ModifiersFor(ulong seed, int wave, ContentDb db, out ModifierId m1, out ModifierId m2);
    // spec 07 6.1~6.2의 정수 식 그대로. Q32.32 배율로는 단계별 내림을 재현할 수 없다(17.3절 TV-022·TV-023).
    public static long HpMicro(int wave, ContentDb db);                               // H(w) × 1,000,000. 단계마다 ×106/100 내림
    public static int EnemyHp(int baseHp, int wave, bool elite, bool thickHide, ContentDb db);   // Wide 128비트 곱 → 나눗셈 → [1, int.MaxValue] 포화
    public static int BossHp(int baseHp, int wave, ContentDb db);                     // baseHp × HMicro(w) / HMicro(10), 포화
    public static int ScaledDamage(int baseDamage, int wave, ContentDb db);           // "체력 배율 적용" 피해. 같은 HMicro, 포화
    public static int Budget(int wave, ContentDb db);
}

public sealed class RunOutcome {                      // 판이 끝났을 때의 결과. 서버 재실행 결과와 비교한다
    public RunEndKind EndKind; public uint EndTick; public ulong FinalStateHash;
    public int StartWave, ReachedWave, ClearedWave;
    public long Score; public WaveResult[] Waves;
    public GoldBreakdown Gold; public DropChance[] DropChances;
    public WildBlock[] WildBlocksFinal;               // 귀환 가방 후보
    public PhantomResult Phantom;
    public ArtId[] ArtsUnlockedThisRun;
    public RunCheckpoint[] Checkpoints;               // 5웨이브 지점: (wave, tick, state_hash)
    public bool DebugUsed;                            // 디버그 op이 한 번이라도 적용됐는가
    public void WriteFacts(ByteWriter w);             // RunResultFacts의 정규 바이트(이 코어의 FactsFormat으로)
}
public readonly struct RunCheckpoint { public readonly int Wave; public readonly uint Tick; public readonly ulong StateHash; }
public enum RunEndKind : byte { None = 0, HpZero = 1, Abandon = 2 }
```

**`RunResultFacts` — 판의 결과를 코어 버전과 무관하게 실어 나르는 정규 바이트.**

```
facts_format u8 (=1)
end_kind u8   end_tick u32   final_state_hash u64
start_wave u16   reached_wave u16   cleared_wave u16
score i64
wave_count u16, 웨이브마다: WaveResult의 필드(spec 09 3.7절의 순서, 전부 고정 폭 정수)
gold: GoldBreakdown의 필드(고정 폭)
drop_chance_count u16, 항목마다: DropChance의 필드
wild_block_count u16, 항목마다: 인스턴스 번호 u16, block u8, 빌림 u8         // wild_blocks_final
phantom: PhantomResult의 필드(first_encounter, downed, dent_permille, revenge_kill …)
arts_unlocked u16(ArtMask)
checkpoint_count u8, 항목마다: wave u16, tick u32, state_hash u64
flags u8 (비트 0 = debug_used)
```

- 쓰는 쪽은 코어다(`RunOutcome.WriteFacts`). 클라이언트는 판이 끝나면 이 바이트를 그대로 올리고, 검증 워커는 재실행 결과를 같은 함수로 쓴다. 둘 다 그 판의 코어 버전이 쓰므로 정직한 판은 **바이트가 같다**.
- 읽는 쪽은 `Wizard.Rules.RunResultFacts.TryRead(bytes, out facts)`다(4.3절). 지금 돌고 있는 워커 버전들의 `facts_format`을 모두 읽을 수 있어야 한다. 형식 판은 `run.facts_format`에 저장한다.
- **불변식: 정산에 쓰이는 모든 필드는 이 바이트 안에 있다.** 정산의 입력은 `RunResultFacts`와 `bag_selection`(서버가 `wild_blocks_final`의 부분집합인지 검사한다)과 서버가 아는 맥락뿐이다. 이 바이트 밖의 클라이언트 값(화면용 요약, 통계)은 정산과 순위에 쓰지 않는다. 새 정산 입력이 생기면 `facts_format`을 올린다.
- 다이제스트 = 이 바이트의 SHA-256. 2차 검증의 일치 판정은 바이트 동등이다(8.3절, 12.4절).

- `WaveResult`, `GoldBreakdown`, `DropChance`, `PhantomResult`의 필드는 spec 09 3.7절.
- 점수는 정수로만 계산한다(spec 01 D15). 처치 점수, 빠른 처리, 무피격, 스침, 변덕 배율의 내림 순서는 spec 07 12.2절.
- 가방에 담을 블록은 판이 끝난 뒤 정산 화면에서 고른다. 그 선택은 코어 밖(`RunSummary.bag_selection`)이다.

### 3.8 재실행

소유: `Sim.Runtime`. 근거: spec 09 9절.

```csharp
// namespace Wizard.Sim
public sealed class ReplayResult {
    public ReplayStatus Status;
    public RunOutcome Outcome;                        // Completed일 때만
    public uint TicksSimulated;
    public string? FaultDetail;                       // SimFault일 때 예외의 종류와 틱(운영자용)
}
public enum ReplayStatus : byte {
    Completed = 0,
    RecordCorrupt = 1,   // 구조 오류, 꼬리표·해시 불일치, 무효 명령, 중간 지점 레코드와 실제 해시의 불일치
    EndedEarly = 2,      // 기록의 end_tick 전에 판이 끝났다
    EndedLate = 3,       // end_tick에 닿았는데 판이 끝나지 않았다
    SimFault = 4,        // 코어가 SimInvariantException을 던졌다(구현 결함일 수 있다)
    Timeout = 5,         // 호출자가 준 시간·틱 한도를 넘었다
    PackMissing = 6,     // 그 판의 data_version 팩을 찾지 못했다(호출자가 채운다)
}
public static class ReplayRunner {
    // 머리말과 기록으로 처음부터 다시 돌린다. upToWave > 0이면 그 웨이브의 중간 지점까지만(원정 정리).
    // header는 **호출자가 믿는 원천**에서 온 것이어야 한다. 서버는 run.setup_canonical로만 만든다(파일의 머리말을 쓰지 않는다).
    // 예외를 밖으로 던지지 않는다. SimInvariantException은 SimFault로 돌려준다.
    public static ReplayResult Run(ContentDb db, InputRecordHeader header, ReadOnlySpan<byte> recordBody,
                                   ReplayLimits limits, int upToWave = 0);
    public static DuelReplayResult RunDuel(ContentDb db, ReadOnlySpan<byte> duelRecord, ReplayLimits limits);   // WZDR. 호스트의 확정 기록, 다시 보기
}
public readonly struct ReplayLimits { public readonly uint MaxTicks; public readonly long MaxStepCount; }       // 값은 SimLimitsDef(data/sim/limits)
public sealed class InputRecordHeader {               // .wzrc 머리말을 읽은 것. 머리말의 바이트 = Setup.WriteCanonical의 바이트(T-46)
    public ushort FormatVersion;                      // = EndlessSetup.CanonicalFormatVersion
    public EndlessSetup Setup;                        // core_version·data_version도 여기에 있다(Setup.CoreVersion, Setup.DataVersion). 따로 두지 않는다
}
public static class InputRecordCodec {                // 신뢰할 수 없는 바이트를 읽는 유일한 문
    // 구조를 전부 확인한 뒤에만 true. 실패는 전부 RecordCorrupt다.
    public static bool TryRead(ReadOnlySpan<byte> file, out InputRecordHeader header, out InputRecordBody body, out RecordError error);
    public static void WriteHeader(ByteWriter w, InputRecordHeader header);        // = header.Setup.WriteCanonical(w). 머리말을 쓰는 코드는 한 곳뿐이다
}
public sealed class InputRecorder {                   // 판을 하는 쪽이 쓴다. 순번은 여기서만 매긴다(3.1절)
    public InputRecorder(InputRecordHeader header);
    // 이 틱의 명령과 키에 순번을 매기고(명령 먼저, 키는 (Offset, KeyClass) 순, 9번째 키부터는 번호 없이 버림),
    // 기록하고, 세계에 넣을 열을 돌려준다.
    public void Append(uint tick, PlayerSlot slot, ReadOnlySpan<RunCommandDraft> commands, ReadOnlySpan<QuantizedKey> keys,
                       Span<RunCommand> commandsOut, out int commandCount, Span<PlayerInput> inputsOut, out int inputCount);
    public void MarkPause(uint tick, uint wallMs);
    public void MarkCheckpoint(int wave, uint tick, ulong stateHash);
    public ReadOnlyMemory<byte> Snapshot();           // 지금까지의 파일 바이트(중간 지점 올리기)
    public ReadOnlyMemory<byte> Finish(uint endTick, ulong finalStateHash);
}
```

검증 워커, 다시 보기, 골든 테스트가 같은 `ReplayRunner`를 쓴다. 파일 형식은 11.1절. `RunCommandDraft`는 순번이 없는 `RunCommand`(op, a, b)다.

**신뢰할 수 없는 기록을 거르는 규칙.** 서버가 받는 `.wzrc`는 공격자의 입력이다.

`InputRecordCodec.TryRead`가 `RecordCorrupt`로 돌려주는 것:

- magic, `format_version`, 길이가 맞지 않는다. 꼬리말 뒤에 바이트가 남는다. `record_sha256`이 다르다
- Δtick 누적이 `uint`를 넘거나 `end_tick`을 넘는다. 레코드의 틱이 역행한다(Δtick은 음수가 없으므로 넘침으로만 생긴다)
- 한 틱에 키 이벤트가 9개 이상이다. 한 틱 안에서 키의 `(offset, KeyClass)`가 역행한다. 명령이 그 틱의 키 뒤에 온다
- 키 코드가 `LogicalKey` 범위 밖이다. op이 `RunCommandOp`에 없다. 모르는 꼬리표
- `end_tick`이 `ReplayLimits.MaxTicks`를 넘는다

`ReplayRunner.Run`이 재실행 중에 `RecordCorrupt`로 돌려주는 것: 무효 명령(3.1절), `0x04 Checkpoint` 레코드의 `(wave, tick, state_hash)`가 재실행의 값과 다름.

**판정의 분류**(서버. 8.3절, 12.4절). 구현 결함이 보상 회수로 흘러가지 않게 가른다.

| 재실행 결과 | 판정 | 처리 |
|---|---|---|
| `Completed`이고 facts 바이트가 같고, 서버가 받아 둔 중간 지점이 전부 같다 | `match` | 대기 해제, 순위 확정 |
| `Completed`인데 facts나 중간 지점이 다르다. `RecordCorrupt`, `EndedEarly`, `EndedLate` | `mismatch` | 회수, 갚을 것, 기록 삭제, 계정 표시(spec 09 9절) |
| `SimFault`, `Timeout`, `PackMissing`, `attempts` 상한 초과, 워커 비정상 종료 | `unverifiable` | 보상은 대기 그대로 둔다. 회수하지 않는다. 운영자 검토 표시(`account_flag`가 아니라 `verify_queue.status`와 관리 화면의 목록) |

`unverifiable`은 플레이어의 잘못으로 보지 않는다. 운영자가 원인을 고친 뒤 다시 대기열에 넣는다.

### 3.9 결투의 코어 쪽

소유: `Sim.Duel`. 근거: spec 10 2~8절·16절, spec 01 12절.

```csharp
// namespace Wizard.Sim.Duel
public sealed class DuelRoomOptions { /* spec 10 9.2: 라운드 수, 길이, 체력 배율, 야생 난입, 가시밭 끔 … */ }
public enum DuelPhase : byte { Countdown = 0, Live = 1, Overtime = 2, RoundResult = 3, Intermission = 4, MatchOver = 5 }
public enum DuelWinner : byte { None = 0, P1 = 1, P2 = 2, Draw = 3 }
public enum DuelEndReason : byte { Ko = 0, Timeout = 1 }       // 이탈·기권·몰수·무효는 코어 밖(호스트)
public sealed class DuelOutcome {
    public DuelWinner Winner; public DuelRoundResult[] Rounds; public uint EndTick; public ulong FinalStateHash;
}
```

라운드의 시작과 끝, 카운트다운, 연장, 가시밭 고리는 입력에서 결정되는 코어 상태다. 서버가 따로 알리지 않아도 양쪽이 같은 틱에 같은 단계에 있다.

## 4. `Wizard.Rules`

### 4.1 난수원

```csharp
// namespace Wizard.Rules
public interface IRandomSource {                      // 판 씨앗과 무관한 난수. 서버 = CSPRNG, 오프라인 = 기기 난수, 테스트 = 고정 열
    int NextInt(int exclusiveMax);
    ulong NextU64();
}
public readonly struct RuleCheck {
    public readonly bool Ok; public readonly ErrorCode Code;
    public static RuleCheck Pass; public static RuleCheck Fail(ErrorCode code);
}
```

### 4.2 강화

소유: `Rules.Enhance`. 근거: spec 08 3~5절.

```csharp
// namespace Wizard.Rules.Enhance
public sealed class WandState { /* spec 08 2.1의 필드. 규칙 함수의 입력이자 출력 */ }
public readonly struct SacrificeItem { public readonly BlockId Block; public readonly int Qty; }
public sealed class EnhanceRequest { public IReadOnlyList<SacrificeItem> Sacrifices; public bool UseCharm; }
public sealed class InventoryView {                   // 검사에 필요한 잔액과 수량만
    public long Gold, GoldPending;
    public Func<string, StackView> Stack;             // item_code → (qty, pending, socketed, bound)
}
public enum EnhanceOutcome : byte { GreatSuccess, Success, FailDrop, FailCharmBlocked, FailNoDrop, MutationOffered }
public enum MutationDecision : byte { Accept, Decline }

public sealed class EnhanceResolution {               // 순수 계산의 결과. 저장은 호출자가 한다
    public EnhanceOutcome Outcome; public WandState WandAfter;
    public ProbBreakdown Breakdown; public int RollBp, SuccessBp, GreatBp, MutationEndBp;
    public EnhanceCost Cost; public IReadOnlyList<ItemDelta> Consumed;      // 귀속 → 일반 → 대기 순으로 뺀 내역
    public bool CharmConsumed, SlotExpanded, RecordPeakUpdated, ScarAdded, UsedPendingAssets;
    public MutationOffer? Offer;                      // MutationOffered일 때
    public int MilestoneStage;                        // 0이면 없음
    public NoticeKind Notice;                         // None / Plus10to12 / Plus13
}
public interface IEnhanceRules {
    RuleCheck Validate(WandState wand, EnhanceRequest request, InventoryView inventory);
    ProbBreakdown Probability(WandState wand, IReadOnlyList<SacrificeItem> sacrifices);
    EnhanceCost CostFor(int stage);
    bool DropPossible(WandState wand);
    EnhanceResolution Resolve(WandState wand, EnhanceRequest request, InventoryView inventory,
                              int rollBp, IRandomSource mutationPick);       // rollBp ∈ [0, 10000)
    EnhanceResolution ResolveMutation(WandState wand, MutationDecision decision);
    RuleCheck ValidateRemoveMutation(WandState wand, MutationId mutation, InventoryView inventory);
    CarveResolution ResolveCarve(WandState wand, InventoryView inventory, int rollBp);
    EnhanceExpectation Expect(WandState wand, ExpectationAssumptions assumptions);     // 마르코프 연쇄(spec 08 5절)
}
```

`Resolve`는 난수를 인자로 받는다. 서버와 오프라인의 차이는 `rollBp`를 누가 뽑느냐뿐이다.

### 4.3 경제, 정산, 상한, 리그

소유: `Rules.Economy`, `Rules.Settlement`, `Rules.RunLimits`, `Rules.League`. 근거: spec 08 6~7절, spec 07 13절, spec 09 7~8절·13절, spec 10 10~11절, spec 11 6절.

```csharp
// namespace Wizard.Rules.Economy
public interface IEconomyRules {
    ShopQuote QuoteShop(string sku, int qty, ShopWeekState week);                    // 값, 주간 한도
    long AppraiseWand(WandState wand);                                               // 감정가(spec 08 6.6)
    long AppraiseItem(string itemCode, long marketMedianOrZero);
    TradeFee DirectTradeFee(TradeSide a, TradeSide b);                               // 올림, 양쪽 반씩(spec 11 7절)
    MarketFee MarketFees(string itemRef, long unitPrice, long qty);                  // 보증금, 수수료
    long GiftFee(TradeSide given);
    ReleaseRefund ReleaseWands(IReadOnlyList<WandState> wands);                      // 떠나보내기 환급(내림)
    int LibraryCapacity(int expansions);
    RuleCheck CanTrade(TradeEligibility who);                                        // 웨이브 10, 보류, 갚을 것 등
}

// namespace Wizard.Rules.Settlement
public sealed class SettlementContext {
    public ProfileKind Profile; public bool SeasonTagOn; public bool PhantomMetBefore, RevengeDoneBefore;
    public int WeeklyRunIndex;                        // 주간 도전 넷째 판부터 가방 0
    public bool Tidy;                                 // 원정 정리(가방 자동, 순위 미반영)
    public IReadOnlyList<int> BagSelection;
}
public sealed class SettlementResult {
    public IReadOnlyList<Grant> Grants;               // 재화·물건 지급 목록(귀속 여부 포함)
    public IReadOnlyList<BagEntry> Bag; public int BagCapacity, HeroLimit;
    public ProfileProgress Progress;                  // 최고 웨이브·점수, 비기 해금, 첫 만남·복수 표시
}
public sealed class RunResultFacts {                  // 3.7절의 정규 바이트를 읽은 것. Sim 타입에 기대지 않는다(정수와 id만)
    public byte FactsFormat; /* 3.7절 바이트의 필드 그대로 */
    public static bool TryRead(ReadOnlySpan<byte> bytes, out RunResultFacts facts);   // 지금 지원하는 모든 facts_format
    public static bool BagSelectionValid(RunResultFacts facts, IReadOnlyList<int> bagSelection);   // wild_blocks_final의 부분집합이고 중복이 없다
}
public interface ISettlementRules {
    // 입력은 facts와 맥락뿐이다. RunOutcome(Sim 타입)을 받지 않는다. 그래서 wizard-api는 판의 코어 버전과 무관하게 정산한다.
    SettlementResult Settle(RunResultFacts facts, SettlementContext context, IRandomSource rng);
    int BagCapacity(int clearedWave); int BagHeroLimit(int clearedWave);
}

// namespace Wizard.Rules.RunLimits
public interface IRunLimits {
    int WhimsUpper(int wave);                         // spec 09 8.2: w < 21 → 0, w < 41 → 1, 그 밖 2 (데이터)
    long ScoreCap(int wave, int whimsUpper);          // 씨앗에서 실제 변덕을 다시 계산하지 않는다
    long GoldCap(int wave, int whimsUpper);
    long SkippedWaveScore(int wave);
    bool StartWaveAllowed(int startWave, int bestWavePassed);                        // skip × 10 ≤ best × 7
    RuleCheck RankedCheck(RankedInputs inputs);                                      // spec 09 13절
}

// namespace Wizard.Rules.League
public interface ILeagueRules {
    int WandIl(WandState wand); int EquippedIl(WandState wand); int HoldingIl(IReadOnlyList<WandState> wands);
    LeagueId LeagueOf(int holdingIl14dMax);
    WandSnapshot ApplyLimits(WandSnapshot wand, LeagueLimits limits);                // 넘치는 단계·영웅·확장·변이를 깎아 적용
    LeagueLimits LimitsFor(LeagueId league);
}
```

규칙 함수는 DB, 시계, 파일을 모른다. 필요한 것은 인자로 받는다. 서버의 유스케이스와 `OfflineBackend`가 같은 함수를 부르고 저장만 각자 한다.

- `OfflineBackend`도 같은 길을 탄다: `world.GetOutcome().WriteFacts` → `RunResultFacts.TryRead` → `Settle`.
- `Wizard.Rules`가 `Wizard.Sim`을 참조하는 곳은 리그 제한(`WandSnapshot`)뿐이다. 정산, 1차 검사, 순위 조건은 Sim의 코드와 Sim의 정규 형식에 기대지 않는다.
- `SettlementContext.SeasonTagOn`이 켜져 있으면 판의 시즌 주화 기회(spec 07 13.6절)를 지급 목록에 넣는다. 오프라인 프로필과 시즌이 꺼진 판에서는 주화와 한정 드랍이 나오지 않는다(spec 07 TV-264).
- `IEconomyRules.CanTrade`의 `TradeEligibility`는 `max_wave_reached`, `play_seconds`, 보류, 갚을 것을 받는다. 거절 사유는 `NeedWave10`, `NeedPlay2h` 등 spec 11 6절의 값이다.
- 지팡이는 그 캐릭터만 쓴다(DECISIONS 2.4). `Rules.Profile.CanUseWand(character, wand)`가 다르면 `NotEligible`이다. 출발, 든 지팡이 설정, 결투 입장이 모두 이 함수를 부른다.

## 5. `Wizard.Text`

소유: `Wizard.Text`. 근거: spec 14 2절.

```csharp
// namespace Wizard.Text
public sealed class NameTable {
    public string Locale { get; } public int Version { get; }
    // 이름 표의 이진 절(names.<locale>.wznm). DataBuild가 data/names/names.ko.json을 검사해 만든다.
    // 형식: magic "WZNM", version u16, locale, count u32, (열쇠 UTF-8, 값 UTF-8)…, sha256. ByteReader로 읽는다.
    // 공유 코드에 JSON 파서를 들이지 않는다(Wizard.Text는 Core에만 의존한다).
    public static NameTable Load(ReadOnlySpan<byte> pack);
    public bool TryGet(string nameKey, out string value);      // 별칭을 푼 뒤 찾는다
}
public interface ITextResolver {
    string Name(string nameKey);                               // 없으면 열쇠 그대로
    string Format(string templateKey, IReadOnlyDictionary<string, string>? args = null);   // {char.5}{이/가} …
    string ResolveTemplate(string template, IReadOnlyDictionary<string, string>? args);
}
public static class TextMetrics {
    public static int WidthPx(string s, int fontPx);           // 한글 = 글자 크기, 숫자·영문·기호 = 절반
    public static string Ellipsize(string s, int maxPx, int fontPx);
    public static string FormatThousands(long v);              // 48,200
}
```

화면에 찍는 모든 문자열은 `ITextResolver`를 거친다. 코드에 한글 문구를 직접 적지 않는다.

**원본 파일의 형식**(`data/names/names.ko.json`, T-35). 여러 작업이 줄을 더하는 유일한 파일이라 병합되는 꼴로 고정한다.

```json
{
  "char.1": "제자 1",
  "char.2": "제자 2",
  "~end": ""
}
```

- 한 줄에 열쇠 하나. 열쇠는 서수(ordinal) 비교 오름차순이다(문화권에 기대는 정렬을 쓰지 않는다. 기기마다 결과가 같아야 한다).
- 마지막 항목 줄(닫는 중괄호 바로 앞)은 표지 열쇠 `"~end": ""`다. 열쇠는 ASCII이고(spec 14 1.1·2.2절의 id 문법) `~`로 시작하지 않으므로 서수 비교에서 표지가 언제나 끝에 온다. 실제 항목의 줄이 모두 쉼표로 끝나므로 줄을 더하는 변경이 이웃 줄을 건드리지 않는다. 표지는 이름이 아니다. DataBuild는 표지를 이진 절에 싣지 않는다.
- git 병합은 union이다(`.gitattributes`). 병합 뒤 `databuild names --fix`가 정렬을 맞추고 중복을 본다. `databuild names --check`는 정렬, 중복 없음, 한 줄에 열쇠 하나, 끝 표지를 검사한다(M1-CONT-02).
- 작업은 자기 영역의 열쇠만 더한다. 남의 줄을 고치거나 지우지 않는다(PIPELINE 4.4절).

## 6. `Wizard.Client`

### 6.1 `IBackend`

소유: `Client.Backend`. 근거: spec 08 8.3절, spec 13 1.2절, spec 12 7절.

화면과 게임 로직은 이것만 부른다. 두 구현(`OfflineBackend`, `ServerBackend`)은 있는 기능에 대해 같은 모양으로 답한다. 화면은 "지원하는가"를 묻지 않고 `Kind`로 메뉴 구성을 정한다.

```csharp
// namespace Wizard.Client
public interface IBackend : IDisposable {
    ProfileKind Kind { get; }
    BackendFeatures Features { get; }                 // 서버: /v1/features. 오프라인: 고정값
    IProfileService Profile { get; }
    IWandService Wands { get; }
    IEnhanceService Enhance { get; }
    IShopService Shop { get; }
    IRunService Runs { get; }
    IRecordService Records { get; }
    IStoreService Store { get; }
    ISettingsService Settings { get; }
}
public interface IOnlineBackend : IBackend {          // Kind == Server일 때만 존재한다. OfflineBackend는 구현하지 않는다
    ConnectionState Connection { get; }
    event Action<ConnectionState> ConnectionChanged;
    event Action<ServerNotice> NoticeReceived;        // +10 이상 성공, +13, 운영 공지
    IAccountService Account { get; }
    IPlazaService Plaza { get; }
    IChatService Chat { get; }
    ISocialService Social { get; }
    ITradeService Trade { get; }
    IMarketService Market { get; }
    IMailService Mail { get; }
    IDuelService Duel { get; }
    IBoardService Boards { get; }
    ISeasonService Season { get; }
}

public interface IProfileService {
    Task<Result<ProfileView>> GetAsync(CancellationToken ct = default);
    Task<Result<ProfileView>> SetEquippedWandAsync(Guid requestId, CharacterId character, long wandId, CancellationToken ct = default);
    Task<Result<ProfileView>> SetTitleAsync(Guid requestId, string? titleId, CancellationToken ct = default);
    Task<Result<ProfileView>> SetCosmeticsAsync(Guid requestId, CharacterId character, CosmeticLoadout loadout, CancellationToken ct = default);
}
public interface IWandService {
    Task<Result<WandView>> SetLoadoutAsync(Guid requestId, long wandId, LoadoutDto loadout, CancellationToken ct = default);
    Task<Result<WandView>> ClearLoadoutAsync(Guid requestId, long wandId, CancellationToken ct = default);
    Task<Result<WandView>> SetScarVisibilityAsync(Guid requestId, long wandId, bool show, CancellationToken ct = default);
    Task<Result<WandView>> SetSkinAsync(Guid requestId, long wandId, string? skinId, CancellationToken ct = default);
    Task<Result<ReleaseResult>> ReleaseAsync(Guid requestId, IReadOnlyList<long> wandIds, string? typedName, CancellationToken ct = default);
}
public interface IEnhanceService {
    Task<Result<EnhanceResultDto>> EnhanceAsync(Guid requestId, long wandId, IReadOnlyList<SacrificeDto> sacrifices, bool useCharm, CancellationToken ct = default);
    Task<Result<EnhanceResultDto>> ResolveMutationAsync(Guid requestId, long wandId, Guid attemptId, MutationDecision decision, CancellationToken ct = default);
    Task<Result<WandView>> RemoveMutationAsync(Guid requestId, long wandId, MutationId mutation, CancellationToken ct = default);
    Task<Result<CarveResultDto>> CarveSlotAsync(Guid requestId, long wandId, CancellationToken ct = default);
}
public interface IShopService {
    Task<Result<ShopView>> GetAsync(CancellationToken ct = default);
    Task<Result<BuyResult>> BuyAsync(Guid requestId, string sku, int qty, CancellationToken ct = default);
}
public interface IRunService {
    Task<Result<RunStartTicket>> StartAsync(Guid requestId, RunStartRequest request, CancellationToken ct = default);
    Task<Result<Unit>> SubmitCheckpointAsync(Guid runId, RunCheckpointDto checkpoint, ReadOnlyMemory<byte> recordSoFar, CancellationToken ct = default);
    Task<Result<RunSettlementView>> FinishAsync(Guid runId, RunSummaryDto summary, ReadOnlyMemory<byte> record, CancellationToken ct = default);
    Task<Result<RunSettlementView>> TidyAsync(Guid runId, CancellationToken ct = default);          // 원정 정리
    Task<Result<IReadOnlyList<RunStatusView>>> ListOpenAsync(CancellationToken ct = default);        // 미정산 판
    Task<Result<RunStatusView>> GetAsync(Guid runId, CancellationToken ct = default);
    Task<Result<WeeklyInfo>> GetWeeklyAsync(CancellationToken ct = default);                         // 오프라인: 게임에 든 씨앗 목록에서
}
public interface IRecordService {                     // 오프라인: 기기 안 개인 기록. 서버: 내 기록(순위표는 IBoardService)
    Task<Result<RecordsView>> GetAsync(CancellationToken ct = default);
    Task<Result<RevengeTrackerView>> GetRevengeTrackerAsync(CancellationToken ct = default);
}
public interface IStoreService {                      // 유료 상점. OfflineBackend는 언제나 상품 0개를 돌려주고 나머지는 불리지 않는다
    Task<Result<StoreView>> GetAsync(CancellationToken ct = default);
    Task<Result<OrderTicket>> CreateOrderAsync(string productId, CancellationToken ct = default);
    Task<Result<OrderResult>> ConfirmOrderAsync(Guid orderId, string? proof, CancellationToken ct = default);
}
public interface ISettingsService {                   // 두 프로필이 같이 쓰는 유일한 것. 저장은 기기, 서버 프로필은 계정에도 동기화
    SettingsData Current { get; }
    Task<Result<Unit>> SaveAsync(SettingsData settings, CancellationToken ct = default);
}
```

온라인 전용 서비스(메서드는 8~9절의 엔드포인트·메시지와 1:1로 대응한다):

```csharp
public interface IAccountService {
    Task<Result<Unit>> SendEmailCodeAsync(CancellationToken ct = default);
    Task<Result<Unit>> VerifyEmailAsync(string code, CancellationToken ct = default);
    Task<Result<Unit>> ChangePasswordAsync(string current, string next, CancellationToken ct = default);
    Task<Result<Unit>> RequestEmailChangeAsync(string currentPassword, string newEmail, CancellationToken ct = default);
    Task<Result<Unit>> ConfirmEmailChangeAsync(string code, CancellationToken ct = default);
    Task<Result<Unit>> RequestDeletionAsync(string currentPassword, CancellationToken ct = default);
    Task<Result<Unit>> CancelDeletionAsync(CancellationToken ct = default);
    Task<Result<ProfileView>> ChangeNicknameAsync(Guid requestId, string nickname, CancellationToken ct = default);   // 골드 상점의 item.rename. 값은 Rules.Economy
    Task<Result<Unit>> LogoutAsync(CancellationToken ct = default);
}
public interface IPlazaService {
    Task<Result<PlazaWelcome>> JoinAsync(PlazaJoinRequest request, CancellationToken ct = default);
    void Leave();
    void SendInput(PlazaInput input);                 // 10틱마다. 응답 없음
    void SendEmote(string emoteId); void SetActivity(PlazaActivity activity);
    Task<Result<Unit>> InteractAsync(PlazaTarget target, CancellationToken ct = default);
    event Action<PlazaSnapshot> SnapshotReceived;
    event Action<PlazaMemberEvent> MemberChanged;
}
public interface IChatService {
    Task<Result<Unit>> SendAsync(ChatSend message, CancellationToken ct = default);
    Task<Result<Unit>> SaveSettingsAsync(ChatSettings settings, CancellationToken ct = default);
    event Action<ChatMessage> MessageReceived;
}
public interface ISocialService {
    Task<Result<FriendListView>> GetFriendsAsync(CancellationToken ct = default);
    Task<Result<Unit>> RequestFriendAsync(Guid requestId, long accountId, CancellationToken ct = default);
    Task<Result<Unit>> RespondAsync(Guid incomingRequestId, bool accept, CancellationToken ct = default);   // 친구·거래·도전 공용
    Task<Result<Unit>> RemoveFriendAsync(Guid requestId, long accountId, CancellationToken ct = default);
    Task<Result<IReadOnlyList<RecentMetView>>> GetRecentAsync(CancellationToken ct = default);
    Task<Result<Unit>> BlockAsync(Guid requestId, long accountId, bool block, CancellationToken ct = default);
    Task<Result<InspectView>> InspectAsync(long accountId, CancellationToken ct = default);
    Task<Result<long>> ReportAsync(Guid requestId, ReportRequest report, CancellationToken ct = default);
    event Action<IncomingRequest> RequestReceived; event Action<FriendPresence> PresenceChanged;
}
public interface ITradeService {                      // 세션형. 상태는 서버가 trade.state로 내려 준다
    Task<Result<Unit>> RequestAsync(long targetAccountId, CancellationToken ct = default);
    void SetSlot(long tradeId, int slotIndex, TradeSlot? item); void SetGold(long tradeId, long gold);
    void SetLocked(long tradeId, bool locked);
    Task<Result<TradeResult>> ConfirmAsync(Guid requestId, long tradeId, int revision, bool lossWarningAck, CancellationToken ct = default);
    void Cancel(long tradeId);
    event Action<TradeState> StateChanged;
}
public interface IMarketService {
    Task<Result<MarketPage>> SearchAsync(MarketQuery query, CancellationToken ct = default);
    Task<Result<PriceStat>> GetPriceAsync(string itemKey, CancellationToken ct = default);
    Task<Result<ListingReceipt>> ListAsync(Guid requestId, MarketListRequest request, CancellationToken ct = default);
    Task<Result<PurchaseReceipt>> BuyAsync(Guid requestId, long listingId, long qty, long expectedUnitPrice, CancellationToken ct = default);
    Task<Result<Unit>> CancelAsync(Guid requestId, long listingId, CancellationToken ct = default);
}
public interface IMailService {
    Task<Result<MailPage>> ListAsync(int page, CancellationToken ct = default);
    Task<Result<ClaimResult>> ClaimAsync(Guid requestId, long mailId, CancellationToken ct = default);
    Task<Result<ClaimResult>> ClaimAllAsync(Guid requestId, CancellationToken ct = default);
    Task<Result<Unit>> SendNoteAsync(Guid requestId, NoteSend note, CancellationToken ct = default);
    Task<Result<GiftReceipt>> SendGiftAsync(Guid requestId, GiftSend gift, CancellationToken ct = default);
}
public interface IDuelService {
    Task<Result<DuelStatusView>> GetStatusAsync(CancellationToken ct = default);          // 리그, IL, 점수, 대기열 조건
    // 대기열에 들어가기 전에 서버가 회선을 잰다(10.5절의 탐침). 답의 QueueTicket에 탐침 토큰이 있고,
    // 탐침이 끝나 서버가 받아들이면 QueueChanged(state = queued), 못 넘으면 QueueChanged(state = rejected, code)가 온다.
    Task<Result<QueueTicket>> EnqueueAsync(Guid requestId, DuelEntrySelection selection, CancellationToken ct = default); void Dequeue();
    void AcceptMatch(Guid matchId);                                                       // duel.match_accept. accept_by_ms 안에
    void SelectPrep(Guid matchId, DuelEntrySelection selection, bool locked);             // duel.prep_select. 도전·방에서 고르기
    Task<Result<IReadOnlyList<RoomSummary>>> ListRoomsAsync(CancellationToken ct = default);
    Task<Result<RoomState>> CreateRoomAsync(Guid requestId, DuelRoomOptions options, CancellationToken ct = default);
    Task<Result<RoomState>> JoinRoomAsync(long roomId, string? password, bool spectate, CancellationToken ct = default);
    void SetRoomOptions(long roomId, DuelRoomOptions options);                            // duel.room_options. 방장만
    void SetReady(long roomId, bool ready, DuelEntrySelection selection); void LeaveRoom(long roomId);
    Task<Result<Unit>> ChallengeAsync(long targetAccountId, bool applyLeagueRules, CancellationToken ct = default);
    void Forfeit(Guid matchId);                                                           // duel.forfeit
    Task<Result<Unit>> RejoinAsync(Guid matchId, CancellationToken ct = default);         // duel.rejoin. 답은 MatchStarting으로 다시 온다(새 키, epoch+1)
    void StartSpectate(long roomId); void StopSpectate(long roomId);                      // duel.spectate_start / stop
    Task<Result<Unit>> PromoteLeagueAsync(Guid requestId, LeagueId target, CancellationToken ct = default);
    Task<Result<DuelResultView>> GetResultAsync(Guid matchId, CancellationToken ct = default);
    event Action<DuelMatchFound> MatchFound;          // 상대, accept_by_ms
    event Action<DuelPrepState> PrepChanged;          // 양쪽의 고른 상태와 남은 시간
    event Action<DuelMatchStart> MatchStarting;       // 설정, 키, epoch, 주소, (재접속이면) 이어 갈 틱·순번·키 상태. DuelSession이 받아 쓴다
    event Action<RoomState> RoomChanged; event Action<QueueStatus> QueueChanged;
}
public sealed class DuelEntrySelection {              // 결투에 들고 들어갈 것. 서버가 지팡이를 잠그고 리그 제한을 적용한다
    public int Character { get; set; }                // 1..5
    public long WandId { get; set; }                  // 그 캐릭터의 지팡이여야 한다(아니면 NotEligible). 조립은 그 지팡이에 저장된 것
}
public interface IBoardService {
    Task<Result<BoardPage>> GetAsync(BoardQuery query, CancellationToken ct = default);
    Task<Result<BoardPage>> GetAroundMeAsync(BoardQuery query, CancellationToken ct = default);
    Task<Result<ReplayFile>> GetReplayAsync(Guid runId, CancellationToken ct = default);
}
public interface ISeasonService {
    Task<Result<SeasonView>> GetCurrentAsync(CancellationToken ct = default);
    Task<Result<HeirEstimate>> GetHeirEstimateAsync(CancellationToken ct = default);
    Task<Result<SeasonResultView>> GetMyResultAsync(long seasonId, CancellationToken ct = default);
    Task<Result<SeasonShopView>> GetShopAsync(CancellationToken ct = default);                                  // 시즌 상점(시즌 주화로 산다)
    Task<Result<BuyResult>> BuyAsync(Guid requestId, string sku, int qty, CancellationToken ct = default);
    Task<Result<ClaimResult>> ClaimRewardAsync(Guid requestId, long seasonId, CancellationToken ct = default);  // 흔들의자의 "시즌 보상 받기". 보상은 우편으로 가 있고 이 호출은 그 우편을 받는다
}
```

만들기:

```csharp
public interface IBackendFactory {
    Task<Result<IBackend>> OpenOfflineAsync(CancellationToken ct = default);            // 없으면 새 프로필을 만든다
    IAuthClient Auth { get; }
}
public interface IAuthClient {                        // 로그인 전. 성공하면 IOnlineBackend를 준다
    Task<Result<BootStatus>> BootAsync(CancellationToken ct = default);                 // 상태 파일 → 버전 → /healthz, /v1/features
    Task<Result<IOnlineBackend>> SignupAsync(SignupRequest request, CancellationToken ct = default);
    Task<Result<IOnlineBackend>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<Result<IOnlineBackend>> ResumeAsync(CancellationToken ct = default);           // 저장된 갱신 토큰으로
    Task<Result<Unit>> RequestRecoveryAsync(string loginId, string email, CancellationToken ct = default);
    Task<Result<Unit>> FindIdsAsync(string email, CancellationToken ct = default);
    Task<Result<Unit>> ConfirmRecoveryAsync(string loginId, string code, string newPassword, CancellationToken ct = default);
}
```

계약:

- 변경 요청의 `requestId`는 화면이 동작 하나에 한 번 만든다. 다시 보낼 때 같은 값을 쓴다. 오프라인도 같은 값을 받아 직전 결과를 돌려준다.
- `OfflineBackend`는 통신 코드를 포함하지 않는다. 결제 코드도 없다(`Store.GetAsync`는 빈 목록).
- 서버 프로필로 이미 시작한 판은 접속이 끊겨도 끝까지 한다. `ServerBackend.Runs.FinishAsync`가 실패하면 미제출 판 보관함에 넣고 다시 접속했을 때 보낸다.
- 뷰 타입(`ProfileView`, `WandView`, `EnhanceResultDto` 등)은 `Wizard.Protocol.Http`의 DTO 그대로다(8절).
- **두 프로필은 섞이지 않는다**(DECISIONS 2.6). `ServerBackend`는 `profiles/offline/` 아래를 읽지도 쓰지도 않는다. 어느 엔드포인트도 오프라인 저장 파일이나 오프라인에서 한 판의 기록을 받지 않는다. 오프라인의 해금·강화·골드를 서버로 옮기는 호출은 없다(spec 03 B27, spec 08 TV-PF-06, spec 12 T-OFF-04).
- `OfflineBackend`의 골드 상점은 `item.rename`을 내지 않는다(오프라인 프로필에는 서버 닉네임이 없다) [임시, OPEN-QUESTIONS].
- `IDuelService`, `ISeasonService`의 메서드는 8.3·8.4절의 엔드포인트와 9절의 메시지에 하나씩 대응한다. 대응표는 9절 끝에 있다.

### 6.2 저장, 전송 추상화

```csharp
// namespace Wizard.Client.Save
public interface ISaveStore {                         // 임시 파일에 쓰고 이름을 바꾼다. 직전 3세대 보관
    Task<Result<SaveLoad>> LoadAsync(string name, CancellationToken ct = default);     // 깨졌으면 직전 세대 + Restored 표시
    Task<Result<Unit>> SaveAsync(string name, ReadOnlyMemory<byte> utf8Json, CancellationToken ct = default);
}
// namespace Wizard.Client.Net
public interface IHttpTransport {
    Task<HttpReply> SendAsync(HttpCall call, CancellationToken ct);
}
public interface IRealtimeChannel {                   // 웹소켓 하나
    Task<Result<Unit>> ConnectAsync(Uri uri, string accessToken, CancellationToken ct);
    void SendText(string json); void SendBinary(ReadOnlySpan<byte> frame);
    event Action<string> TextReceived; event Action<ReadOnlyMemory<byte>> BinaryReceived;
    event Action<ErrorCode> Closed;
}
// namespace Wizard.Protocol
public interface IJsonCodec {
    string Serialize<T>(T value);
    bool TryDeserialize<T>(string json, out T value);
}
public interface IAeadCipher {                        // ChaCha20-Poly1305 (RFC 8439)
    void Seal(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
              ReadOnlySpan<byte> plaintext, Span<byte> ciphertextAndTag);
    bool Open(ReadOnlySpan<byte> key, ReadOnlySpan<byte> nonce12, ReadOnlySpan<byte> aad,
              ReadOnlySpan<byte> ciphertextAndTag, Span<byte> plaintext);
}
```

### 6.3 `IDuelTransport`

소유: `Client.Duel`. 근거: spec 10 14.10절.

```csharp
// namespace Wizard.Client.Duel
public enum DuelTransportKind : byte { Udp = 0, WebSocket = 1, Loopback = 2 }
public interface IDuelTransport : IDisposable {
    DuelTransportKind Kind { get; }
    void Send(ReadOnlySpan<byte> packet);             // 10절의 패킷 하나. 막지 않는다
    int Receive(Span<byte> buffer);                   // 받은 패킷 길이. 없으면 0. 막지 않는다
}
// 구현: UdpDuelTransport, WebSocketDuelTransport, LoopbackTransport(기기 안의 DuelHostCore에 직접 붙는다)
```

자동 매치는 `Kind == Udp`일 때만 들어갈 수 있다.

### 6.4 넷코드 코어(`Wizard.Netcode`)

소유: `Wizard.Netcode`. 근거: spec 10 14절.

```csharp
// namespace Wizard.Netcode.Duel
public sealed class DuelHostCore {                    // 소켓도 시계도 모른다. 서버 프로세스와 Loopback이 같이 쓴다
    public DuelHostCore(ContentDb db, DuelSetup setup, DuelHostKeys keys, IAeadCipher cipher);
    // receiveTimeUs는 그 패킷이 **기기에 닿은 시각**이다(wizard-duel은 커널 수신 타임스탬프를 쓴다). 큐에서 꺼낸 시각이 아니다.
    // 서버의 GC와 루프 지연이 늦음 판정에 섞이지 않게 한다. fromAddress는 불투명한 주소 표지(응답 주소 추적용).
    public void OnPacket(ReadOnlySpan<byte> packet, long receiveTimeUs, ulong fromAddress);
    public void Advance(long serverTimeUs);           // 봉인 → 정렬 → SimWorld.Step → 확정. 호출자는 수신 버퍼를 **다 비운 뒤에** 부른다
    public int PollOutgoing(PlayerSlot to, Span<byte> buffer, out ulong toAddress);    // 보낼 패킷. 없으면 0. 주소 = 태그 검증을 통과한 마지막 패킷의 출발 주소
    public void SetInputDelay(PlayerSlot slot, int delayTicks);    // 서버가 잰 왕복으로 정한 D. 이른 시각 검사에 쓴다. 시뮬레이션에는 들어가지 않는다
    public int MeasuredRttMs(PlayerSlot slot);                     // 서버가 직접 쌓은 표본(최근 16회)의 최솟값. 표본이 없으면 −1
    public void Rekey(PlayerSlot slot, DuelPeerKey key);           // 재접속: 새 token_id, 새 키, epoch + 1(10.1절)
    public DuelResume ResumeInfo(PlayerSlot slot);                 // 마지막 확정 틱, 다음 순번, 서버가 아는 키 상태
    public bool TryWriteFullState(ByteWriter target, out uint tick);
    public bool TryWriteSpectatorStart(ByteWriter target, out uint tick);   // 120틱 전 시점의 상태(호스트가 60틱마다 보관한다). 관전 시작용
    public int PollSpectatorChunk(Span<byte> buffer);              // 120틱 늦은 확정 입력
    public DuelHostStatus Status { get; }             // 진행, 끝(결과), 몰수(누가), 끊김 타이머
    public DuelNetStats StatsFor(PlayerSlot slot);
    public ReadOnlyMemory<byte> ConfirmedInputRecord();            // WZDR. ReplayRunner.RunDuel로 돌리면 호스트의 최종 해시가 나온다
}
public sealed class DuelClientCore {
    public DuelClientCore(ContentDb db, DuelSetup setup, PlayerSlot me, DuelClientKeys keys, IAeadCipher cipher, DuelResume? resume = null);
    public void SetInputDelay(int delayTicks);                     // HelloAck로 받은 값
    // 순번은 여기서 매긴다(3.1절). keys의 유효 틱 = 누른 순간의 입력 시계 틱 + D. 부르면 곧바로 PollOutgoing에 패킷이 생긴다.
    public void OnLocalInput(ReadOnlySpan<QuantizedKey> keys, KeyMask physicalKeysNow);
    public void OnPacket(ReadOnlySpan<byte> packet, long clientTimeUs);
    public void OnFullState(uint tick, ReadOnlySpan<byte> state);
    public DuelAdvance Advance(uint inputClockTick);  // 예측을 입력 시계까지 끌고 간다. 아래 "시계 둘"
    public int PollOutgoing(Span<byte> buffer);
    public ISimView View { get; }                     // 예측 세계
    public ulong ConfirmedHash(uint tick);            // 확정 세계의 해시(30틱마다 서버 값과 비교)
    public int DrainEvents(Span<PresentationEvent> buffer);
    public int MarginMsEstimate { get; }              // HUD의 마감 여유
    public bool NeedsFullState { get; }               // 해시 불일치 또는 확정 공백 > full_state_gap_ticks일 때만
}
public readonly struct DuelAdvance { public readonly bool Stalled; public readonly int TicksSimulated, RollbackDepth; public readonly uint PredictedTick, ConfirmedTick; }
public enum EventPhase : byte { Predicted = 0, Confirmed = 1, Retracted = 2 }
public struct PresentationEvent { public SimEvent Event; public EventPhase Phase; public ushort Ordinal; }   // Ordinal = 그 틱에서 같은 (Type, Slot)의 몇 번째인가

public static class ClockSync {
    public static ClockOffset Estimate(ReadOnlySpan<ClockSample> samples);     // 왕복이 가장 짧은 표본 기준
    public static int ChooseInputDelay(int rttMs, NetRuleSet rules);           // 경계와 D 값은 데이터(InputDelayTable). 코드에 60/90을 박지 않는다
    public static int TickLengthAdjustPpm(long errorUs);                       // ±5,000ppm 안. 입력 시계의 드리프트 보정에만 쓴다
}

// namespace Wizard.Netcode.Plaza
public sealed class PlazaHostCore { /* 인스턴스 하나. 입력 적용, 속도·벽 검사, 스냅숏 만들기 */ }
public sealed class PlazaClientCore { /* 자기 이동 예측, ack 뒤 재적용, 남의 위치 보간용 버퍼 */ }
```

표시 이벤트의 단계:

- 무한 웨이브와 연습은 모든 이벤트가 곧바로 `Confirmed`다.
- 결투는 예측 세계의 이벤트를 `Predicted`로 낸다. 그 틱을 다시 돌렸을 때 **열쇠 `(Tick, Type, Slot, Ordinal)`** 이 같은 이벤트가 없으면 `Retracted`를 낸다. 확정 세계가 그 틱을 지나면 확정 세계의 이벤트를 `Confirmed`로 낸다(같은 열쇠의 `Predicted`가 이미 나갔으면 표시 계층은 이어서 그린다).
- 열쇠에 `EntityId`를 넣지 않는다. 되감기 뒤에 생성 순서가 바뀌면 개체 번호가 달라져서, 예측 하나가 틀렸을 때 같은 틱 뒤의 이벤트가 전부 번복·재예측으로 깜박이기 때문이다. `Source`와 `Target`은 표시용 인자일 뿐이다.
- `Confirmed`는 확정 세계에서만 나오므로 같은 이벤트가 두 번 확정되지 않는다.
- 소리와 피격 연출은 `Confirmed`에서만 낸다. 비기 발동 문양은 `Predicted`에서 그리고 `Retracted`면 0.1초 동안 흐려지게 한다(spec 10 21절 C1).

**세계 둘.** `DuelClientCore`는 확정 세계 1개와 예측 세계 1개를 둔다. 확정 세계는 서버가 확정한 입력만으로 나아간다. 해시는 확정 세계에서 계산한다. 예측 세계는 확정 세계의 스냅숏에서 출발해 (내 입력 + 상대의 "직전 키 상태 유지")로 입력 시계까지 나아간다. 새 확정이 예측과 달랐으면 확정 세계를 예측 세계에 복사하고(`SaveSnapshot` → `LoadSnapshot`, 스냅숏 버퍼 1개) 다시 돌린다.

**시계 둘.**

| 시계 | 무엇인가 | 멈추는가 |
|---|---|---|
| 입력 시계 | 서버와 맞춘 경기 시계. 키 이벤트의 유효 틱(= 누른 틱 + D)과 패킷의 `sealed_through_tick`의 기준 | **멈추지 않는다.** 드리프트는 틱 길이 ±0.5%로 맞춘다 |
| 예측 틱 | 예측 세계가 가 있는 틱 | `마지막 확정 틱 + 8`을 넘지 못한다(되감기 한도) |

- 예측 틱이 한도에 걸리면 `Advance`는 `Stalled`를 돌려주고 예측을 멈춘다. 확정을 기다린다. **전체 상태를 청하지 않는다.** 그동안에도 입력 시계는 가고, 키 이벤트는 입력 시계의 틱으로 찍혀 곧바로 나간다.
- 멈춤이 풀리면 한 번의 `Advance`에서 여러 틱을 돌려 입력 시계를 따라잡는다(한 번에 `CatchupTicksPerAdvance`까지. 데이터 값). ±0.5% 보정으로 따라잡지 않는다.
- `NeedsFullState`가 서는 때는 둘뿐이다: 30틱 해시가 서버와 다를 때, 서버가 보내 주는 확정의 첫 틱이 내 확정 틱 + 1보다 커서 공백이 생겼고 그 공백이 `FullStateGapTicks`(데이터 값)를 넘을 때. "한 경기 해시 불일치 3회 초과" 통계에는 앞의 것만 센다.

**버려진 내 입력과 키 상태 맞추기.** 서버가 내 이벤트를 버리면(늦음, 유실, 이른 시각, 수량) 서버의 키 상태와 내 물리 키 상태가 어긋난 채 남는다(뗌이 버려지면 계속 걷거나 쏜다).

- 확정 열이 정본이다. 내 이벤트 가운데 유효 틱이 확정 틱 이하인데 확정 열에 없는 것은 버려진 것이다(`ServerTick`의 dropped 목록은 이유를 알려 줄 뿐이다).
- 버려진 것을 알게 되면 `DuelClientCore`는 `(확정 세계에서 본 내 KeyMask) ⊕ (지금의 물리 KeyMask)`의 차이를 **새 순번**의 보정 이벤트로 낸다. 보정 이벤트의 시각은 만드는 순간의 입력 시계 시각(유효 틱 = 그 틱 + D)이고, 직전에 낸 내 이벤트의 시각보다 앞서지 않게 올린다(한 패킷 안의 순서를 지킨다).
- 버려진 원래 시각을 되살리지 않는다. 방향 누름의 보정은 판정기에 **새 시각의 입력**으로 들어간다. 다른 틱으로 옮기지 않는다는 규칙(online 4.11)은 서버가 지킨다. 클라이언트는 새 입력을 낼 뿐이다.
- 보정 이벤트도 버려질 수 있다. 그러면 다음 확정에서 다시 맞춘다. 물리 키 상태는 `OnLocalInput`의 `physicalKeysNow`와 `IInputSource.CurrentKeys`로 받는다.

**결과의 확정.** 경기 결과는 `match_id`당 처음 확정된 것이 이긴다. `wizard-api`가 생존 신호 끊김으로 무효(`void`) 처리한 경기에 뒤늦게 결과 보고가 오면 저장된 `void`를 그대로 돌려준다(점수와 보상은 바뀌지 않는다). `wizard-duel`은 보고가 받아들여질 때까지 다시 보낸다(7.3절).

### 6.5 입력원과 결제창

```csharp
// namespace Wizard.Client
public readonly struct RawKeyEvent {                  // 유니티 쪽이 만든다. 실수 시각은 여기까지만
    public readonly LogicalKey Key; public readonly bool Down; public readonly double TimeSeconds;
}
public interface IInputSource {
    int Drain(Span<RawKeyEvent> buffer);              // 논리 키 상태가 바뀔 때만 이벤트가 생긴다
    KeyMask CurrentKeys { get; }                      // 지금의 논리 키 상태(물리 키에서 온 것). 결투의 키 상태 맞추기(6.4절)
    void SetBindings(KeyBindings bindings);
    void SetDuelPolling(bool on);                     // 결투 중에는 입력 폴링을 프레임과 분리한다(아래)
}
public readonly struct InputClock {                   // 실수 시각 ↔ 틱. 무한 웨이브는 SimClock, 결투는 서버와 맞춘 입력 시계
    public readonly double EpochSeconds; public readonly double TickSeconds;     // TickSeconds는 1/60 ± 0.5%
}
public sealed class InputQuantizer {
    // 틱과 무관하게 부른다. 프레임마다 Drain 직후에. 틱 = floor((이벤트의 하드웨어 시각 − epoch) / tickSeconds) + inputDelayTicks,
    // 오프셋 = 틱 안의 위치 × 256(내림). 순번은 매기지 않는다. 같은 (틱, 오프셋)은 KeyClass 순으로 놓는다.
    public int Quantize(ReadOnlySpan<RawKeyEvent> raw, in InputClock clock, int inputDelayTicks, Span<QuantizedKey> output);
}
public sealed class TickInputQueue {                  // 세계에 넣을 때 쓴다. 양자화된 키를 틱별로 꺼낸다
    public void Enqueue(ReadOnlySpan<QuantizedKey> keys);
    public int TakeForTick(uint tick, Span<QuantizedKey> output);   // 유효 틱이 tick인 것. (Offset, KeyClass) 순. 9번째부터는 버린다
}
public interface IPurchaseFlow {                      // 출시 빌드에는 구현을 등록하지 않는다(spec 12 10.2)
    PurchaseFlow Flow { get; }
    Task<string?> AuthorizeAsync(OrderTicket ticket); // 영수증형: 영수증 / 주문형: 창이 닫힐 때까지 기다린 뒤 null / 코드형: 입력한 코드
}
// namespace Wizard.Protocol  (서버의 IPaymentProvider도 같은 열거형을 쓴다)
public enum PurchaseFlow : byte { ClientReceipt = 0, ServerOrder = 1, RedeemCode = 2 }
```

**입력이 나가는 순서와 마감 여유.** 결투에서 키 이벤트를 틱 진행에 묶어 보내면(틱 t의 키가 t가 끝난 뒤 첫 프레임에 나가면) 여유에서 프레임 한 장이 통째로 빠진다. 그래서:

1. 프레임마다 `Drain` → `Quantize`(틱 = 하드웨어 시각의 틱, 유효 틱 = +D) → `DuelClientCore.OnLocalInput` → **곧바로 송신** → 그다음에 틱 루프(ARCHITECTURE 6.1절).
2. `Wizard.Unity.Input`은 결투 중에 입력 폴링을 프레임과 분리한다(`InputSystem.pollingFrequency`를 올리거나 전용 큐에서 꺼내 프레임 사이에도 `OnLocalInput`과 송신을 부른다). 프레임 대기가 여유에 들어가지 않게 한다.
3. 마감 여유의 식: `(D + 1 − f)틱 − 편도 − 전송 대기`. `f`는 누른 순간이 틱 안에서 차지하는 위치(0~1)다. online 4.12 표의 "20ms 이상"은 `f = 0`일 때의 값이고, 최악(`f → 1`)은 한 틱만큼 작다.
4. D의 표(`InputDelayTable`)는 `NetRuleSet`의 데이터다. NetSim에서 "오프셋을 균등 분포로 준 입력의 폐기율이 왕복 60/90/120ms, 지터 15ms 이하에서 0"을 재고(M2-NET-06), 못 넘으면 표를 수치 조정 작업으로 올린다.

서버 스위치(`payments_enabled`), 서버 제공자, 클라이언트의 `IPurchaseFlow` 구현 셋이 모두 있어야 구매가 된다. 상점 화면은 `Store.GetAsync`의 유료 상품이 0개면 별빛 탭과 잔액을 그리지 않는다.

## 7. 서버 쪽 인터페이스

### 7.1 결제와 메일

소유: `Wizard.Server.App`. 근거: spec 12 10.2절(결제), 2.8절(메일).

```csharp
// namespace Wizard.Server.App.Payments
public interface IPaymentProvider {
    string Id { get; }                                // "none", "fake-receipt", "fake-order", 나중에 "pg-…"
    PurchaseFlow Flow { get; }
    bool IsEnabled { get; }                           // NullPaymentProvider는 항상 false
    Task<ProviderOrder> CreateAsync(PurchaseOrder order, Product product);
    Task<VerifyResult> VerifyAsync(PurchaseOrder order, string? proof);        // 제공자 서버에 직접 확인
    bool TryParseWebhook(HttpRequest request, out ProviderEvent providerEvent);
    Task<IReadOnlyList<ProviderEvent>> PollAsync(DateTime since);
}
public sealed record ProviderOrder(string? ProviderRef, string? CheckoutUrl, string? CheckoutToken);
public sealed record VerifyResult(bool Ok, bool Final, string? ProviderRef, string? Error);
public sealed record ProviderEvent(string ProviderRef, ProviderEventKind Kind, DateTime At, string Raw);
public enum ProviderEventKind { Paid, Refunded, Cancelled }
public enum OrderStatus { Created, PendingAuth, Authorized, Granted, Cancelled, Refunded, Revoked }

// namespace Wizard.Server.App.Mail
public enum MailKind { VerifyEmail, ResetPassword, FindLoginIds, NotifyPasswordChanged, NotifyEmailChanged }
public interface IMailSender {
    string Id { get; }                                // "api", "smtp-relay", "null"
    bool IsEnabled { get; }                           // NullMailSender는 false
    Task<MailSendResult> SendAsync(string toAddress, MailKind kind, MailBody body, CancellationToken ct);
    Task<IReadOnlyList<MailDeliveryEvent>> PollDeliveryAsync(DateTime since);
}
public sealed record MailSendResult(bool Accepted, bool Retryable, string? ProviderRef, string? Error);
```

- 출시 서버에는 `NullPaymentProvider` 하나만 등록한다. `config.payments_enabled = false`면 주문 함수와 알림 주소는 제공자를 보기 전에 거절한다.
- 개발과 테스트는 `NullMailSender`로 돈다. 가입과 로그인은 그대로 되고 복구만 꺼진다.
- **출시 구성에는 실제 구현 `ApiMailSender`(외부 메일 사업자의 HTTPS API, T-36)를 등록한다.** 비밀번호를 잃으면 이메일로 복구한다는 확정 사항(DECISIONS 1.24)은 이 구현이 있어야 이행된다. `ApiMailSender`는 사업자별 어댑터(`IMailApiAdapter`: 요청 만들기, 응답 해석, 배달·반송 사건 읽기)를 받는다. 발송 재시도, 하루 한도, 반송 처리, `mail_outbox` 상태 전이(queued → sent → delivered / bounced / failed)는 어댑터와 무관한 공통 코드다. API 키는 `config`가 아니라 비밀값 저장소(환경)에서 읽는다.

### 7.2 트랜잭션, 원장, 멱등

소유: `Wizard.Server.Data`. 근거: spec 12 1.3절.

```csharp
// namespace Wizard.Server.Data
public interface IClock { DateTime UtcNow { get; } }
public interface IUnitOfWork : IAsyncDisposable {     // PostgreSQL 트랜잭션 하나
    NpgsqlConnection Connection { get; } NpgsqlTransaction Transaction { get; }
    Task CommitAsync(CancellationToken ct);
}
public interface IUnitOfWorkFactory { Task<IUnitOfWork> BeginAsync(CancellationToken ct); }

public interface IIdempotency {
    // 한 트랜잭션 안에서: request 행을 먼저 INSERT … ON CONFLICT DO NOTHING 한다(spec 12 1.3절. 조회를 먼저 하지 않는다).
    //  - 1행이 들어갔다 → body를 돌리고 결과를 그 행에 저장하고 COMMIT.
    //  - 0행이다(이미 있다) → 그 행을 FOR UPDATE로 잡는다(앞선 트랜잭션이 끝날 때까지 기다린다).
    //      저장된 지문이 bodyFingerprint와 다르면 IdempotencyConflict. 결과가 있으면 그것을 돌려준다(body를 부르지 않는다).
    // bodyFingerprint = 요청 본문 정규 JSON의 SHA-256(kind, 경로 인자 포함).
    Task<IdempotentResult<TResult>> RunAsync<TResult>(long accountId, Guid requestId, string kind, ReadOnlyMemory<byte> bodyFingerprint,
                                                      Func<IUnitOfWork, Task<TResult>> body, CancellationToken ct);
}
public readonly struct IdempotentResult<T> { public readonly bool Conflict; public readonly bool Replayed; public readonly T Value; }
public readonly struct LedgerRef {                    // 원장 한 줄의 원인. currency_ledger·item_ledger의 reason, ref_type, ref_id, request_id
    public readonly LedgerReason Reason;              // Enhance, ShopBuy, RunSettle, RunRevoke, Trade, MarketSale, MarketFee, Mail, DuelReward, SeasonReward, SeasonCoinConvert, NicknameChange, Admin, Payment, Refund …(추가만)
    public readonly string RefType; public readonly string RefId;   // "run" + uuid, "trade" + id …
    public readonly Guid? RequestId;
}
public enum ItemOrigin : byte { Earned = 0, Shop = 1, Trade = 2, Season = 3, Revenge = 4, Admin = 5, Paid = 6 }   // item_instance.origin. Paid는 강화·매치·리그 코드가 읽지 않는다
public sealed class WandRow { /* 12.2절 wand 표의 열 그대로. LockWandAsync가 FOR UPDATE로 읽은 한 줄 */ }
public interface ILedger {                            // 재화와 물건의 유일한 변경 통로. 잔액 행을 잠그고 원장에 한 줄을 더한다
    Task<long> AddCurrencyAsync(IUnitOfWork uow, long accountId, CurrencyCode currency, CurrencyBucket bucket,
                                long delta, bool pending, LedgerRef reference);
    Task AddItemAsync(IUnitOfWork uow, long accountId, string itemRef, long delta, bool pending, LedgerRef reference);
    Task SpendStarlightAsync(IUnitOfWork uow, long accountId, long amount, LedgerRef reference);   // 무상부터
}
public interface IRowLocks {                          // 여러 계정을 잠글 때는 계정 번호 오름차순
    Task LockAccountsAsync(IUnitOfWork uow, params long[] accountIds);
    Task<WandRow> LockWandAsync(IUnitOfWork uow, long wandId);
}
```

규칙: 유스케이스는 `ILedger`를 거치지 않고 잔액·수량 표를 고치지 않는다. 원장 표에는 서버 DB 역할의 `UPDATE`/`DELETE` 권한이 없다.

### 7.3 `wizard-api` ↔ `wizard-duel`

소유: `Wizard.Protocol.Internal`(DTO), `Server.Api`와 `Server.Duel`(양 끝). 유닉스 도메인 소켓 위의 HTTP.

| 방향 | 경로 | 본문 | 뜻 |
|---|---|---|---|
| api → duel | `POST /internal/matches` | `DuelMatchTicket { match_id, setup: DuelSetupDto, token_ids[2], keys[2], spectate }` | 경기 생성. 같은 `match_id`면 같은 답 |
| api → duel | `DELETE /internal/matches/{match_id}` | | 시작 전 취소 |
| api → duel | `GET /internal/stats` | | 동시 경기 수, 틱 시간 |
| duel → api | `POST /internal/duel-results` | `DuelMatchReport { match_id, result, reason, rounds, net_stats, input_delay, replay_path }` | 결과 보고. `match_id`로 멱등. **처음 확정된 결과가 이긴다.** 이미 `void`로 닫힌 경기면 저장된 `void`를 답으로 돌려준다. `wizard-duel`은 2xx를 받을 때까지 다시 보낸다 |
| duel → api | `POST /internal/heartbeat` | `{ started_at, matches }` | 2초마다. 끊기면 진행 중 경기는 무효 처리 |
| api → duel | `POST /internal/matches/{match_id}/rekey` | `{ slot, epoch, token_id, key }` | 재접속. 그 사람의 새 키와 토큰(10.1절). 답 `DuelResume { confirmed_tick, next_seq, key_state }` |
| api → duel | `POST /internal/probes` | `{ probe_id, account_id, token_id, key, expires_at }` | 대기열 진입 전 회선 탐침을 연다(10.5절) |
| duel → api | `POST /internal/probe-results` | `{ probe_id, account_id, samples, rtt_min_ms, transport }` | `wizard-duel`이 직접 잰 왕복. `wizard-api`가 `DuelRttTooHigh`(502)·`DuelUdpRequired`(503)를 판정한다 |
| duel → api | `POST /internal/matches/{match_id}/pre-start` | `{ rtt_ms[2], input_delay[2] }` | 시계 맞추기 뒤 서버가 잰 값. 한도를 넘는 쪽이 있으면 `wizard-api`가 시작 전에 취소한다(`void`, 상대에게 불이익 없음) |

유닉스 소켓 경로는 짧은 고정 뿌리에 둔다: 배치는 `/tmp/wz/duel.sock`, 테스트는 `/tmp/wz/<작업 트리 경로의 해시 8자>-<목적>.sock`. macOS의 `sun_path`는 104바이트라서 작업 트리의 `artifacts/` 아래에 두면 bind가 실패한다. 경로가 100자를 넘으면 `Server.Duel`과 `Server.Api`가 시작할 때 오류로 끝낸다.

## 8. HTTP API

소유: `Wizard.Protocol.Http`(DTO, 경로 상수), `Wizard.Server.Api`(구현), `Wizard.Client.ServerBackend`(호출). 근거: spec 12 2.15절, spec 09 18절, spec 11 13.3절, spec 08 8.3절, spec 10 9절.

공통: 접두사 `/v1`. 본문은 JSON(snake_case). 인증이 필요한 요청은 `Authorization: Bearer`. "멱등" 칸이 `키`인 요청은 `Idempotency-Key` 머리말이 필수다. 오류 본문은 `{ "error": { "code": "not_enough_gold", "detail": "…", "retry_after_ms": 0 } }`.

### 8.1 시작과 계정

| 메서드·경로 | 인증 | 멱등 | 요청 → 응답 |
|---|---|---|---|
| `GET /healthz` | 없음 | | → 200 / 503 |
| `GET /v1/features` | 없음 | | → `FeaturesResponse { protocol_version, core_version, data_version, recovery_mail_enabled, payments_enabled, signup_open, maintenance }` |
| `GET /v1/content/manifest` | 없음 | | → `{ data_version, pack_sha256, pack_url, core_compat }` |
| `POST /v1/auth/signup` | 없음 | | `{ login_id, password, nickname, email, device_key }` → `AuthTokens` |
| `POST /v1/auth/login` | 없음 | | `{ login_id, password, device_key, client_version, core_version }` → `AuthTokens { access_token, refresh_token, access_expires_at, new_device }` |
| `POST /v1/auth/refresh` | 갱신 토큰 | | → `AuthTokens`(회전) |
| `POST /v1/auth/logout` | 접근 | | |
| `POST /v1/auth/password/change` | 접근 | | `{ current, new }` |
| `POST /v1/auth/email/send-code` · `verify` · `change` · `change/confirm` | 접근 | | spec 12 2.15절 |
| `POST /v1/auth/recovery/request` · `find-ids` · `confirm` | 없음 | | 답은 언제나 같다 |
| `POST /v1/account/delete` · `delete/cancel` | 접근 | | |
| `POST /v1/account/nickname` | 접근 | 키 | `{ nickname }` → `ProfileView`. 값은 `Rules.Economy`의 `item.rename`. `NicknameTaken`, `InvalidNickname`, `NotEnoughGold` |
| `GET /v1/settings` · `PUT /v1/settings` | 접근 | | `SettingsData`(키 배치 포함) |

### 8.2 프로필, 지팡이, 강화, 상점

| 메서드·경로 | 멱등 | 요청 → 응답 |
|---|---|---|
| `GET /v1/profile` | | → `ProfileView` |
| `PUT /v1/characters/{n}/equipped-wand` | 키 | `{ wand_id }` → `ProfileView` |
| `PUT /v1/characters/{n}/cosmetics` · `PUT /v1/profile/title` | 키 | → `ProfileView` |
| `PUT /v1/wands/{id}/loadout` | 키 | `LoadoutDto` → `WandView` |
| `POST /v1/wands/{id}/loadout/clear` | 키 | → `WandView` |
| `PUT /v1/wands/{id}/scars` · `PUT /v1/wands/{id}/skin` | 키 | → `WandView` |
| `POST /v1/wands/release` | 키 | `{ wand_ids, typed_name }` → `ReleaseResult` |
| `POST /v1/wands/{id}/enhance` | 키 | `{ sacrifices: [{block, qty}], use_charm }` → `EnhanceResultDto` |
| `POST /v1/wands/{id}/mutation/resolve` | 키 | `{ attempt_id, decision }` → `EnhanceResultDto` |
| `POST /v1/wands/{id}/mutation/remove` | 키 | `{ mutation }` → `WandView` |
| `POST /v1/wands/{id}/carve` | 키 | → `CarveResultDto` |
| `GET /v1/shop` | | → `ShopView` |
| `POST /v1/shop/buy` | 키 | `{ sku, qty }` → `BuyResult` |
| `GET /v1/store` | | → `StoreView`(결제가 꺼져 있으면 유료 상품 0개) |
| `POST /v1/store/orders` | | `{ product_id }` → `OrderTicket` |
| `POST /v1/store/orders/{order_id}/confirm` | (order_id) | `{ proof }` → `OrderResult` |
| `POST /v1/payments/webhook/{provider}` | | 제공자 알림. 스위치가 꺼져 있으면 언제나 거절 |

핵심 DTO:

```csharp
// namespace Wizard.Protocol.Http
public sealed class ProfileView {
    public long AccountId { get; set; } public string Nickname { get; set; } = "";
    public bool EmailVerified { get; set; }
    public List<CharacterView> Characters { get; set; } = new List<CharacterView>();   // 5명
    public List<WandView> Wands { get; set; } = new List<WandView>();
    public List<BalanceView> Balances { get; set; } = new List<BalanceView>();          // currency, bucket, amount, amount_pending
    public List<StackView> Items { get; set; } = new List<StackView>();                 // item_code, qty, qty_pending, qty_socketed, qty_bound
    public List<InstanceView> Instances { get; set; } = new List<InstanceView>();       // 개체 꾸미기
    public List<DebtView> Debts { get; set; } = new List<DebtView>();
    public int LibraryCapacity { get; set; }
    public LeagueId League { get; set; } public int HoldingIl { get; set; }
    public bool PhantomMet { get; set; } public DateTime? RevengeDoneAt { get; set; }
    public string? TitleId { get; set; }
    public DateTime? TradeHoldUntil { get; set; }
    public long PlaySeconds { get; set; } public int MaxWaveReached { get; set; }
    public bool TradeOpen { get; set; }                                                 // 거래·장터가 열렸는가(spec 11 6절)
    public List<string> TradeMissing { get; set; } = new List<string>();                // need_wave10, need_play2h, hold, debt …
}
public sealed class CharacterView {
    public int Character { get; set; }                // 1..5
    public long EquippedWandId { get; set; }
    public List<string> UnlockedArts { get; set; } = new List<string>();    // 공용 비기 id. 시작 3종은 싣지 않아도 참
    public long BestScore { get; set; } public int BestWave { get; set; }
}
public sealed class WandView {
    public long Id { get; set; } public int Character { get; set; }
    public int Stage { get; set; } public int FloorPeak { get; set; } public int RecordPeak { get; set; }
    public int DropCount { get; set; } public int Aura { get; set; }
    public int SlotsExtra { get; set; } public int CarveBonus { get; set; }
    public List<string> Mutations { get; set; } = new List<string>();       // 받은 순서
    public MutationOfferDto? PendingOffer { get; set; }
    public LoadoutDto Loadout { get; set; } = new LoadoutDto();
    public string? SkinId { get; set; } public bool ShowScars { get; set; }
    public string Origin { get; set; } = "start"; public int OwnerCount { get; set; }
    public DateTime? TradeLockedUntil { get; set; } public string LockState { get; set; } = "none";
    public bool PendingLocked { get; set; } public bool DebtLocked { get; set; }
    public DateTime AcquiredAt { get; set; } public int Version { get; set; }
    public int Il { get; set; }
}
public sealed class LoadoutDto {
    public string? Core { get; set; }                                       // "block.magic_bolt" 또는 null
    public List<string?> Links { get; set; } = new List<string?>();         // 기본 연결 칸 + 확장 칸
}
public sealed class EnhanceResultDto {                                      // spec 08 4.11절의 필드
    public Guid AttemptId { get; set; } public EnhanceOutcome Outcome { get; set; }
    public int StageBefore { get; set; } public int StageAfter { get; set; }
    public bool RecordPeakUpdated { get; set; } public bool SlotExpanded { get; set; } public bool SlotAlreadyFull { get; set; }
    public bool CharmConsumed { get; set; } public int AuraBefore { get; set; } public int AuraAfter { get; set; }
    public string? MutationCandidate { get; set; } public string? MutationPushedOut { get; set; }
    public EnhanceFallbackDto? Fallback { get; set; }
    public ProbBreakdownDto Breakdown { get; set; } = new ProbBreakdownDto();
    public int RollBp { get; set; } public int SuccessBp { get; set; } public int GreatBp { get; set; } public int MutationEndBp { get; set; }
    public EnhanceCostDto Cost { get; set; } = new EnhanceCostDto();
    public MilestoneDto? Milestone { get; set; } public string? ServerNotice { get; set; } public bool ScarAdded { get; set; }
    public WandView Wand { get; set; } = new WandView();
    public List<BalanceView> Balances { get; set; } = new List<BalanceView>();
    public List<StackView> Items { get; set; } = new List<StackView>();
}
```

### 8.3 판, 순위, 시즌

| 메서드·경로 | 멱등 | 요청 → 응답 |
|---|---|---|
| `POST /v1/runs` | 키 | `RunStartRequest { character, wand_id, kind, start_wave, core_version, data_version }` → `RunStartTicket { run_id, run_token, setup(base64url: EndlessSetup의 정규 바이트 = .wzrc 머리말), seed, kind, start_wave, phantom_first, wand_snapshot, unlocked_arts, season_tag_on, ranked_eligible_start, week_id, weekly_run_index }`. `wand.character ≠ character`면 `NotEligible` |
| `POST /v1/runs/{run_id}/checkpoints` | (run_id, wave) | multipart: `RunCheckpointDto { wave, tick, state_hash, score, wallet }` + 기록 파일 |
| `POST /v1/runs/{run_id}/finish` | run_id | multipart: `RunSummaryDto { facts(RunResultFacts 바이트), bag_selection[], client_stats }` + 기록 파일 → `RunSettlementView { status, ranked, grants, bag, pending, progress }` |
| `POST /v1/runs/{run_id}/tidy` | run_id | → `RunSettlementView` |
| `GET /v1/runs/{run_id}` · `GET /v1/runs?status=in_progress` | | → `RunStatusView` |
| `GET /v1/weekly` | | → `WeeklyInfo { week_id, starts_at, ends_at, my_runs, open }` |
| `GET /v1/boards/{board}?season=&week=&league=&character=&page=` | | → `BoardPage` |
| `GET /v1/boards/{board}/me` | | → `BoardPage`(내 주변) |
| `GET /v1/replays/{run_id}` | | → 기록 파일(상위 100위. 주간 도전은 주가 끝난 뒤) |
| `GET /v1/records` · `GET /v1/records/revenge` | | → `RecordsView`, `RevengeTrackerView` |
| `GET /v1/seasons/current` · `…/heir-estimate` · `GET /v1/seasons/{id}/result/me` | | spec 09 18절 |
| `GET /v1/seasons/current/shop` | | → `SeasonShopView { items[{ sku, price_coin, limit, bought }], coin_balance }` |
| `POST /v1/seasons/current/shop/buy` | 키 | `{ sku, qty }` → `BuyResult`. 시즌 주화(`CurrencyCode.SeasonCoin`)로 낸다 |
| `POST /v1/seasons/{id}/rewards/claim` | 키 | → `ClaimResult`. 시즌 보상은 정산 때 우편으로 가 있다. 이 호출은 그 시즌의 보상 우편을 한꺼번에 받는다(없으면 빈 결과) |

`seed`와 `state_hash`는 부호 없는 64비트라서 JSON에는 16자리 hex 문자열로 싣는다. `run_token`은 base64url이다.

**출발의 정본은 서버가 가진다.** `StartRun`은 `EndlessSetup`의 정규 바이트(seed, kind, start_wave, phantom_first, season_tag_on, wand_snapshot, unlocked_arts, core_version, data_version)를 만들어 `run.setup_canonical`에 저장하고 같은 바이트를 `setup`으로 내려 준다. `core_version`과 `data_version`은 `RunStartRequest`의 값이고 `EndlessSetup.CoreVersion`·`DataVersion`에 실려 `WriteCanonical`이 쓴다(3.2절, T-46). 클라이언트는 `EndlessSetup.TryReadCanonical`로 읽고, 받은 바이트를 그대로 기록 파일의 머리말로 쓴다. 출발 뒤에 계정의 해금이나 지팡이가 바뀌어도(그 판의 정산이 해금을 더하거나, 다른 판이 먼저 정산되어도) 재실행의 시작 조건은 이 바이트다.

**제출에서 확정까지의 순서**(spec 09 7~9절. 12.4절의 열을 쓴다).

1. `finish`를 받으면 **한 트랜잭션에서**: `run` 행 `FOR UPDATE` → 토큰 소모(`token_used_at`) → 1차 검사 → 그 결과 기록 → 정산 → `verify_queue` 등록 → COMMIT. 토큰 소모와 정산이 다른 트랜잭션이면 그 사이에 죽은 뒤의 재제출이 "토큰 이미 사용"으로 막혀 판이 정산되지 못한다. 멱등 키는 `run_id`이고, 같은 `run_id`의 재제출은 저장된 결과를 돌려준다.
2. 1차 검사: 기록 파일을 `InputRecordCodec.TryRead`로 읽는다 → 파일의 머리말 바이트가 `run.setup_canonical`과 **같은지 대조**한다(다르면 `RunRecordInvalid`. 머리말의 `unlocked_arts` 한 비트만 바꿔도 거절된다) → 디버그 op이 하나라도 있거나 facts의 `debug_used`가 켜져 있으면 `RunRecordInvalid` → `RunResultFacts.TryRead` → 가속·일시정지 검사 → 웨이브별 점수·골드 상한(`IRunLimits.ScoreCap(w, WhimsUpper(w))`) → `bag_selection` 검사.
3. facts 바이트를 `run.claimed_facts`에, SHA-256을 `run.claimed_digest`에 저장한다. 정산은 이 facts로만 한다.
4. 검증 워커(그 판의 코어 버전): `run.setup_canonical`로 머리말을 만들고(파일의 머리말은 재실행 입력으로 쓰지 않는다) `ReplayRunner.Run` → `ReplayStatus`와 재실행 facts의 바이트·다이제스트를 `verify_queue.result`에 적는다. 워커는 `run`의 주장 값을 읽지 않고 비교하지 않는다.
5. `wizard-api`의 백그라운드 작업: 3.8절의 표대로 `match` / `mismatch` / `unverifiable`을 정한다. `match`의 조건은 (a) 재실행 facts 바이트 = `claimed_facts`, (b) `run_checkpoint`의 모든 줄 `(wave, tick, state_hash)`가 재실행 facts의 중간 지점과 같다. 받아 둔 중간 지점이 구속력을 가지므로 5웨이브 단위의 다시 하기가 걸린다.
6. `wizard-api`는 이 경로에서 `Wizard.Sim`의 코드(`WaveMath`, `SimWorld`)를 부르지 않는다. 그래서 코어 N으로 출발한 판을 서버가 N+1로 바뀐 뒤에 받아도 같은 결과가 난다.

### 8.4 사회, 거래, 결투

| 메서드·경로 | 멱등 | 요청 → 응답 |
|---|---|---|
| `GET /v1/friends` · `GET /v1/friends/recent` | | |
| `POST /v1/friends/requests` · `DELETE /v1/friends/{account_id}` | 키 | |
| `PUT /v1/blocks/{account_id}` · `DELETE /v1/blocks/{account_id}` · `GET /v1/blocks` | 키 | |
| `GET /v1/accounts/{account_id}/inspect` | | → `InspectView` |
| `POST /v1/reports` | 키 | `{ target, reason, note, ref }` → `{ report_id }` |
| `PUT /v1/chat/settings` | | `ChatSettings` |
| `GET /v1/market/listings?category=&q=&sort=&page=` | | → `MarketPage` |
| `GET /v1/market/prices/{item_key}` | | → `PriceStat` |
| `POST /v1/market/listings` | 키 | `{ item, qty, unit_price }` → `ListingReceipt` |
| `POST /v1/market/listings/{id}/buy` | 키 | `{ qty, expected_unit_price }` → `PurchaseReceipt` |
| `POST /v1/market/listings/{id}/cancel` | 키 | |
| `GET /v1/mail?page=` · `POST /v1/mail/{id}/claim` · `POST /v1/mail/claim-all` | 키(수령) | |
| `POST /v1/notes` · `POST /v1/gifts` | 키 | |
| `GET /v1/duel/status` | | → `DuelStatusView { league, holding_il, equipped_il, rating, deviation, queue_eligible, missing[], banned_until }`. `missing`에는 `need_wave10`, `need_play2h`, `device_account_limit`(기기당 계정 3개) 등이 온다(spec 10 9.5, TV-M19·M20) |
| `POST /v1/duel/queue` | 키 | `{ selection: DuelEntrySelection }` → `QueueTicket { probe: { probe_id, token_id, key, udp{host, port}, expires_at_ms } }`. 고른 지팡이의 캐릭터가 다르면 `NotEligible`. 대기열에는 **탐침이 끝난 뒤** 서버가 넣는다(10.5절) |
| `DELETE /v1/duel/queue` | 키 | |
| `GET /v1/duel/rooms` · `POST /v1/duel/rooms` · `POST /v1/duel/rooms/{id}/join` | 키(만들기) | |
| `POST /v1/league/promote` | 키 | `{ target }` |
| `GET /v1/duel/matches/{match_id}` · `GET /v1/duel/replays/{match_id}` | | |

직접 거래의 창 조작, 방의 준비 상태, 도전 신청은 세션형이라 웹소켓 메시지로 한다(9절). 최종 확정(`trade.confirm`)만 `request_id`를 싣는다.

## 9. 웹소켓 메시지

소유: `Wizard.Protocol.Realtime`. 근거: spec 11 13.1~13.2절, spec 10 9·14·16절, spec 13 1.6절.

연결: `wss://<api>/v1/ws`, 머리말에 접근 토큰. 계정당 연결 하나. 새 연결이 오면 옛 연결은 `system.kicked`를 받고 끊긴다.

글자 프레임 봉투:

```json
{ "t": "chat.send", "c": 17, "d": { "channel": "plaza", "text": "…" } }      // 클라 → 서버. c = 클라이언트 순번(응답 짝짓기)
{ "t": "chat.msg",  "s": 9021, "d": { … } }                                  // 서버 → 클라. s = 서버 순번
{ "t": "error",     "c": 17, "d": { "code": "rate_limited", "retry_after_ms": 800 } }
```

이진 프레임: 첫 바이트가 종류다. `0x01 plaza.snapshot`, `0x02 plaza.input`, `0x10 duel.full_state`, `0x11 duel.spectate`.

| 방향 | `t` | 본문 필드 |
|---|---|---|
| S→C | `system.hello` | server_time_ms, session_id, resume_ok |
| S→C | `system.kicked` | reason(new_login, sanction, maintenance) |
| 양쪽 | `system.ping` / `system.pong` | t_ms |
| C→S | `plaza.join` | instance_id?, reason(auto, manual, follow), follow_account_id? |
| C→S | `plaza.leave` · `plaza.emote` · `plaza.activity` · `plaza.interact` | emote_id / activity / target |
| C→S | (이진 0x02) `plaza.input` | seq u16, 최근 3틱의 이동 방향(각 1바이트: Dir8 또는 0xFF) |
| S→C | `plaza.welcome` | instance_id, tick, server_time_ms, members[OverheadView], self_slot |
| S→C | (이진 0x01) `plaza.snapshot` | tick u32, ack_seq u16, n u8, 항목 n개 { slot u8, x u16, y u16(1/64칸), facing_activity u8, emote u8 } |
| S→C | `plaza.member_joined` · `plaza.member_left` · `plaza.member_changed` · `plaza.position_corrected` | |
| C→S | `chat.send` | channel, text, target?, item_links? |
| S→C | `chat.msg` · `chat.reject` | spec 11 13.1절 |
| S→C | `notice.server` | kind(plus10to12, plus13, bundle, ops), wand_type, element, stage, nickname?, count?, text_key? |
| S→C | `request.note` | request_id, kind(friend, trade, challenge), from, expires_at_ms |
| C→S | `request.respond` | request_id, accept |
| S→C | `friend.presence` | account_id, presence |
| C→S | `trade.request` · `trade.set_slot` · `trade.set_gold` · `trade.lock` · `trade.unlock` · `trade.cancel` | spec 11 13.2절 |
| C→S | `trade.confirm` | trade_id, revision, loss_warning_ack, request_id |
| S→C | `trade.opened` · `trade.state` · `trade.result` | spec 11 13.2절 |
| S→C | `mail.new` · `run.verified` · `run.invalidated` · `profile.changed` | 다시 읽어야 할 것을 알린다(본문은 id만) |
| C→S | `duel.challenge` | target_account_id, apply_league_rules |
| C→S | `duel.room_ready` | room_id, ready, selection(DuelEntrySelection) |
| C→S | `duel.room_leave` · `duel.room_options` · `duel.forfeit` | room_id / room_id, options / match_id |
| S→C | `duel.queue_status` | state(probing, queued, rejected, matched), code?(duel_rtt_too_high, duel_udp_required …), waited_ms, range |
| S→C | `duel.room_state` | room_id, options, members, ready[], spectators |
| S→C | `duel.match_found` | match_id, mode, opponent(OverheadView), accept_by_ms |
| C→S | `duel.match_accept` | match_id. `accept_by_ms` 안에 양쪽이 보내야 한다. 안 보낸 쪽은 대기열에서 빠진다 |
| C→S | `duel.prep_select` | match_id, selection(DuelEntrySelection), locked. 도전과 방의 "캐릭터·지팡이·조립 고르기"(spec 10 9.3). 자동 매치는 대기열에 넣을 때 고른 것으로 고정이다 |
| S→C | `duel.prep_state` | match_id, me{selection, locked}, opponent{character, locked}, deadline_ms. 양쪽이 잠그면 서버가 지팡이를 잠그고(`WandLockState.Duel`) 리그 제한을 적용해 `duel.setup`을 낸다 |
| S→C | `duel.setup` | match_id, setup(DuelSetupDto), my_slot, epoch, token_id, key(base64url 32바이트), udp{host, port}, ws_path, transport_hint, resume?{confirmed_tick, next_seq, key_state} |
| C→S | `duel.rejoin` | match_id. 클라이언트를 다시 켰거나 웹소켓이 새로 붙었을 때. 답은 다시 발급한 `duel.setup`(새 token_id, 새 키, epoch + 1, resume)과 `duel.full_state` |
| S→C | `duel.opponent_disconnected` · `duel.opponent_reconnected` | grace_ms |
| S→C | `duel.result` | match_id, result, reason, rounds, rating_delta?, rewards |
| C→S | `duel.full_state_request` | match_id, have_tick |
| S→C | (이진 0x10) `duel.full_state` | tick u32, len u32, 상태 바이트(`SimWorld.WriteState`) |
| C→S | `duel.spectate_start` · `duel.spectate_stop` | room_id |
| S→C | (이진 0x11) `duel.spectate` | from_tick u32, 확정 입력 묶음(10.3절의 confirmed와 같은 부호화) |

경기의 수명주기(매치 성립, 설정, 결과, 무효, 상대 끊김)는 UDP가 아니라 이 연결로 온다. UDP는 틱 단위 자료만 싣는다.

`IDuelService` ↔ 엔드포인트·메시지 대응:

| 메서드 | 통로 |
|---|---|
| `GetStatusAsync`, `EnqueueAsync`, `Dequeue`, `ListRoomsAsync`, `CreateRoomAsync`, `JoinRoomAsync`, `PromoteLeagueAsync`, `GetResultAsync` | 8.4절의 HTTP |
| `AcceptMatch` / `SelectPrep` / `SetRoomOptions` / `SetReady` / `LeaveRoom` | `duel.match_accept` / `duel.prep_select` / `duel.room_options` / `duel.room_ready` / `duel.room_leave` |
| `ChallengeAsync` / `Forfeit` / `RejoinAsync` / `StartSpectate` / `StopSpectate` | `duel.challenge` / `duel.forfeit` / `duel.rejoin` / `duel.spectate_start` / `duel.spectate_stop` |
| `MatchFound` / `PrepChanged` / `MatchStarting` / `RoomChanged` / `QueueChanged` | `duel.match_found` / `duel.prep_state` / `duel.setup` / `duel.room_state` / `duel.queue_status` |

도전을 받는 쪽은 `request.respond`로 수락하고, 그 뒤 양쪽이 `duel.prep_select`로 고른다.

## 10. 결투 UDP 패킷

소유: `Wizard.Protocol.Duel`(코덱), `Wizard.Netcode.Duel`(쓰는 쪽). 근거: spec 10 14.3~14.10절.

모든 수는 리틀 엔디언이다. 한 패킷은 1,200바이트를 넘지 않는다.

### 10.1 겉 형식

```
오프셋  크기  필드
0       1     ver_type      윗 4비트 = 프로토콜 판(1), 아랫 4비트 = 종류
1       4     token_id      u32. 경기와 보내는 사람을 가리킨다(서버가 duel.setup으로 준 값)
5       8     packet_number u64. 보내는 쪽마다 1부터 1씩
13      n     ciphertext    본문(10.2~10.4)
13+n    16    tag           Poly1305
```

- 암호: ChaCha20-Poly1305. 키는 **(경기, 사람, epoch)마다** 새로 만든 32바이트다(`duel.setup`으로 받는다). 두 사람의 키는 다르다.
- nonce(12바이트) = `방향(1바이트: 0 = 클라→서버, 1 = 서버→클라)` ‖ `0x00 0x00 0x00` ‖ `packet_number`(8바이트 LE).
- **같은 키에서 `packet_number`는 절대 되돌아가지 않는다.** 같은 (키, nonce)를 두 번 쓰면 키스트림이 드러나고 위조가 가능해진다. 클라이언트가 다시 켜져 번호를 잃었으면 같은 키로 1부터 다시 세지 않고 `duel.rejoin`으로 새 키(epoch + 1, 새 `token_id`)를 받는다. 서버 쪽 번호는 `DuelHostCore`가 경기 내내 들고 있고, 재발급 때 그 사람에게 보내는 방향의 번호도 새 키에서 1부터 다시 센다. 옛 `token_id`의 패킷은 재발급 즉시 버린다.
- AAD = 앞의 13바이트.
- 재전송 차단: 받는 쪽은 방향별로 가장 큰 번호와 64칸 비트맵 창을 둔다. 창보다 오래됐거나 이미 받은 번호는 버린다.
- `token_id`를 모르거나 태그가 틀리면 열어 보지 않고 버린다. 응답하지 않는다.
- 응답 주소: 서버는 그 `token_id`로 **태그 검증을 통과한 마지막 패킷의 출발 주소**로 보낸다. NAT가 포트를 바꿔도 다음 정상 패킷부터 따라간다. 검증에 실패한 패킷은 주소를 바꾸지 못한다.
- 끊김 판정: 그 사람의 검증된 패킷이 `DisconnectSilenceTicks`(`NetRuleSet`의 데이터 값) 동안 없으면 끊긴 것으로 본다. 그 순간 서버는 그 사람의 눌린 키를 전부 뗀 것으로 만든다(spec 10 3.7). 이 **서버가 만든 뗌 이벤트는 그 사람의 순번 공간을 소비하고**(마지막 순번 + 1부터) 확정 열과 WZDR에 보통 이벤트와 똑같이 실린다. 돌아온 클라이언트는 `resume.next_seq`부터 번호를 매기고 `resume.key_state`와 물리 키 상태의 차이를 보정 이벤트로 낸다(6.4절). 유예(spec 10의 20초) 안에 돌아오지 않으면 몰수다.

종류: `1 ClientInput`, `2 ServerTick`, `3 SyncRequest`, `4 SyncReply`, `5 Hello`, `6 HelloAck`, `7 Bye`.

### 10.2 `ClientInput` (클라 → 서버, 매 틱 1개 + 키 이벤트가 생기면 즉시 1개)

```
u32   sealed_through_tick     이 틱까지의 내 입력은 이것이 전부다
u32   ack_confirmed_tick      내가 받은 마지막 확정 틱
u32   client_send_time_ms     경기 시계 기준
u32   echo_server_time_ms     내가 마지막으로 받은 서버 패킷의 server_time_ms (없으면 0)
u16   echo_hold_ms            그 패킷을 받은 뒤 이 패킷을 보낼 때까지 든 시간. 서버의 왕복 표본 = 도착 시각 − echo_server_time − hold
u32   base_seq                이 패킷의 첫 이벤트 바로 앞 순번. 이벤트가 없으면 지금까지 매긴 마지막 순번
u8    event_count             0..64. 유효 틱이 sealed_through_tick−7 이상인 내 이벤트 전부(아직 봉인하지 않은 다음 틱의 것도 싣는다)
이벤트[0]:  u32 tick, u8 offset, u8 key_down(아랫 4비트 = 키, 0x10 = 누름), u32 seq
이벤트[i>0]: varuint Δtick(≥0), u8 offset, u8 key_down, varuint Δseq(≥1)
```

이벤트는 순번 순이고, 정직한 클라이언트에서는 그것이 `(tick, offset, 키 분류)` 순과 같다. 크기는 입력이 없을 때 52바이트, 보통 70~100바이트다.

- `sealed_through_tick`은 입력 시계의 지금 틱 `n`에 대해 `n + D − 1`이다(새 키는 유효 틱 `n + D` 이상을 받으므로 그 앞은 다 정해졌다).
- "키 이벤트가 생기면 즉시 1개": 방금 생긴 이벤트(유효 틱 `n + D`, 아직 봉인 전)를 실어 바로 보낸다. 다음 틱 패킷을 기다리지 않는다.
- `base_seq`는 이벤트가 하나도 없는 패킷으로도 서버가 순번을 맞추게 한다(8틱 넘게 패킷이 끊긴 뒤의 복구, 10.6절).

### 10.3 `ServerTick` (서버 → 클라)

```
u32   server_time_ms
u32   echo_client_send_time_ms
i16   margin_ms_estimate       봉인 마감까지 남았던 시간의 추정(HUD)
u8    flags                    1 = 해시 있음, 2 = 버린 이벤트 있음
u32   confirmed_from_tick      아직 ack 받지 못한 첫 확정 틱
u8    confirmed_count          0..32
틱마다: u8 n0, u8 n1, 이어서 슬롯 0의 이벤트 n0개, 슬롯 1의 이벤트 n1개
        이벤트: u8 offset, u8 key_down, varuint seq   (틱은 순서로 안다)
[flags&1]  u32 hash_tick, u64 state_hash
[flags&2]  u8 dropped_count, 항목: u32 seq, u8 reason(1 늦음, 2 이른 시각, 3 수량, 4 순서(패킷 거부), 5 유실(창 밖으로 밀려남)), u16 run(같은 이유로 이어진 순번의 수 − 1)
```

서버는 B의 틱 T 입력을 A의 틱 T가 봉인된 뒤에만 A에게 싣는다(미리 보기 막기). 서버가 만든 뗌 이벤트(끊김, 10.1절)도 그 사람의 확정 이벤트로 실린다.

### 10.4 시계와 인사

```
SyncRequest : u64 t0_us(클라 단조 시계), u32 echo_server_seq(직전 SyncReply의 server_seq. 처음이면 0), u32 echo_hold_us
SyncReply   : u64 t0_us(그대로), u64 server_time_us, u32 server_seq(서버가 매긴 번호. 보낸 시각을 서버가 기억한다)
Hello       : u8 slot, u32 client_build
HelloAck    : u32 start_tick, u64 match_epoch_server_us, u8 input_delay_self, u8 input_delay_opponent, u16 rtt_ms_measured
Bye         : u8 reason
```

경기 전 `SyncRequest`/`SyncReply`를 16회 주고받는다. 클라이언트는 왕복이 가장 짧은 표본으로 시계 오프셋을 정한다. 경기 중에는 `ServerTick`의 시각 칸으로 1초마다 다시 잰다.

### 10.5 왕복 측정, 입력 지연 D, 대기열 탐침

왕복과 D는 서버가 잰다. 클라이언트가 알려 준 왕복을 믿지 않는다.

- 서버의 표본: `SyncRequest`의 `echo_server_seq`·`echo_hold_us`와 `ClientInput`의 `echo_server_time_ms`·`echo_hold_ms`로 `왕복 = 도착 시각 − 내가 보낸 시각 − hold`를 직접 계산한다. `hold`가 `RttHoldCapMs`(데이터 값)를 넘는 표본은 버린다(붙들어 두어 왕복을 작게 보이려는 값). 최근 16개의 최솟값이 그 사람의 왕복이고, 그 절반이 이른 시각 검사의 편도 추정이다(spec 10 14.8).
- D는 서버가 자기 표본으로 `ClockSync.ChooseInputDelay(rtt, NetRuleSet)`를 불러 정하고 `HelloAck`로만 내려 준다. `Hello`에는 왕복 칸이 없다. `DuelSetup`에도 D가 없다(시뮬레이션은 D를 모른다). 호스트는 `SetInputDelay`로 받고, 다시 보기 파일(WZDR)은 머리말의 별도 칸에 적는다.
- 경기 시작 때 서버가 잰 왕복이 한도(자동 매치 120ms)를 넘으면 시작 전에 취소한다(`void`. 상대에게 불이익이 없고 넘은 쪽도 이탈로 세지 않는다).

**대기열에 들어가기 전의 탐침.** 자동 매치의 조건 "왕복 120ms 이하, UDP 가능"(`DuelRttTooHigh` 502, `DuelUdpRequired` 503)을 서버가 확인한다.

1. `POST /v1/duel/queue` → `wizard-api`가 `wizard-duel`에 탐침을 연다(`POST /internal/probes`) → 답으로 탐침 토큰과 키를 준다.
2. 클라이언트는 그 토큰으로 `wizard-duel`과 UDP `SyncRequest`/`SyncReply`를 16회 한다(탐침 토큰은 Sync 두 종류만 받는다).
3. `wizard-duel`이 자기가 잰 왕복을 `POST /internal/probe-results`로 알린다. UDP 패킷이 하나도 닿지 않고 만료되면 `transport = none`이다.
4. `wizard-api`가 판정해 대기열에 넣거나(`duel.queue_status { state: queued }`) 거절한다(`state: rejected, code`). 웹소켓 대체 경로로만 닿는 클라이언트는 503이다.

### 10.6 서버의 입력 검증 순서 (정본)

spec 10 14.8절의 표를 순서가 있는 절차로 고정한다. 봉인된 틱의 이벤트 하나, 또는 8틱(133ms)을 넘는 연속 유실 하나가 뒤따르는 패킷들을 통째로 버리게 하면 정직한 플레이어가 몰수패한다. 그래서 **패킷 단위 거부는 한 패킷 안의 역행에만** 쓴다.

```
OnClientInput(p, pkt, receiveTime):                       // AEAD와 재전송 창을 통과한 패킷
  // (0) 패킷 구조: 한 패킷 안에서 seq가 늘지 않거나 (tick, offset)이 역행하면 조작이다
  if not StrictlyIncreasingSeq(pkt.events) or not NonDecreasingTime(pkt.events) or pkt.base_seq >= first(pkt.events).seq:
      p.orderViolations += 1; notify(reason 4); return   // 이 패킷만 버린다. 봉인도 올리지 않는다
  // (1) 순번 맞추기: 창 밖으로 밀려난 이벤트는 영영 오지 않는다. 유실로 소비한다
  if pkt.base_seq > p.lastSeq:
      MarkLost(p.lastSeq + 1 .. pkt.base_seq); p.lastSeq = pkt.base_seq
  for e in pkt.events:
      if e.seq <= p.lastSeq: continue                     // 겹쳐 보낸 것. 조용히 건너뛴다
      if e.seq > p.lastSeq + 1: MarkLost(p.lastSeq + 1 .. e.seq − 1)
      p.lastSeq = e.seq                                    // 아래에서 무엇이 되든 이 순번은 소비됐다
      if Sealed(p, e.tick) or receiveTime >= TickStart(e.tick + 1):   drop(e, 1 늦음); continue
      if PressTime(e, p.D) > ToInputTime(receiveTime − p.oneWay) + 256: drop(e, 2 이른 시각); continue
      if e.down == p.keyDown[e.key]: continue             // 키 상태 모순. 무시(알리지 않는다)
      if Count(p, e.tick) >= 8: drop(e, 3 수량); continue
      Accept(p, e)
  // (2) 봉인
  p.sealedThrough = max(p.sealedThrough, pkt.sealed_through_tick)
```

- 버린 이벤트와 유실로 소비한 순번은 `ServerTick`의 dropped 목록으로 본인에게 알린다. 통계(`net_stats`)에 넣는다.
- 몰수는 `orderViolations`만 센다(spec 10의 [임시] 30개 초과). 늦음과 유실은 몇 개가 쌓여도 몰수가 아니다.
- 같은 패킷 열과 같은 수신 시각 열이면 결과가 같다(`DuelHostCore`는 시계를 모른다).
- 봉인은 `Advance`에서도 일어난다: 서버 시각이 `T + 1틱`에 닿으면 T는 "변화 없음"으로 닫힌다.
- 검사 대상 사례: 200ms 동안 모든 패킷이 유실되는 중에 키를 누르고 뗀다 → 몰수 0, 유실로 통지, 그 뒤 입력은 정상으로 받는다. 늦은 이벤트 하나 → 그 이벤트만 버려지고 같은 패킷의 다른 이벤트와 뒤따르는 패킷은 받는다(M2-NET-04, M2-NET-06).

### 10.7 웹소켓 대체 경로

`/v1/duel/ws?token_id=…`에 붙어 위와 **같은 바이트**를 이진 프레임 하나에 패킷 하나씩 싣는다. 암호화도 그대로 한다(코드 경로를 하나로 유지). 이 경로로는 자동 매치에 들어갈 수 없다.

## 11. 파일 형식

### 11.1 입력 기록 `.wzrc`

소유: `Wizard.Sim.Input`(`InputRecordHeader`, `InputRecordCodec`). 기록 형식은 코어의 것이다. 검증 워커와 `ReplayRunner`가 `Wizard.Protocol` 없이 읽는다. 근거: spec 09 3.5절, spec 01 3.6절.

```
머리말:
  magic "WZRC"(4)  format_version u16(=1)
  core_version [16]   data_version u32
  run_id [16]   seed u64   kind u8   start_wave u16   phantom_first u8   season_tag_on u8
  wand_snapshot: character u8, stage u8, slots_extra u8, mutation_count u8, mutations[u8…],
                 core u8, link_count u8, links[u8…]
  unlocked_arts u16
본문: 꼬리표가 붙은 레코드의 열
  0x01 Input     : varuint Δtick, u8 offset, u8 key_down            (순번은 싣지 않는다. 읽을 때 1부터 매긴다)
  0x02 Command   : varuint Δtick, u8 op, i32 a, i32 b               (순번 공간을 같이 쓴다)
  0x03 Pause     : varuint Δtick, u32 wall_ms
  0x04 Checkpoint: u16 wave, u32 tick, u64 state_hash
꼬리말:
  0xFF  end_tick u32   final_state_hash u64   flags u8(비트 0 = debug_used)   record_sha256 [32] (머리말 ‖ 본문)
```

- `run_id [16]`을 비롯한 모든 uuid는 RFC 4122 네트워크 순서(문자열 hex 순서) 16바이트다. `UuidBytes.Write`/`Read`만 쓴다(1.5절). `Guid.ToByteArray()`는 앞 세 필드가 리틀 엔디언인 혼합 순서라서 쓰지 않는다. `tests/fixtures/uuid/`에 알려진 uuid의 바이트 픽스처를 둔다.
- 레코드의 순서가 곧 순번의 순서다. 한 틱 안에서는 `0x02 Command`가 먼저, 그다음 `0x01 Input`이 `(offset, KeyClass)` 순으로 온다. 읽는 쪽은 레코드 순서대로 1부터 번호를 매긴다. 쓰는 쪽(`InputRecorder`)이 실행 중에 매긴 번호와 같다(3.1절).
- 머리말의 바이트는 `EndlessSetup`의 정규 바이트와 같다(magic부터 unlocked_arts까지 전부. T-46). 쓰는 코드는 `EndlessSetup.WriteCanonical` 하나, 읽는 코드는 `TryReadCanonical`·`TryReadCanonicalPrefix` 하나다(3.2절). 서버 프로필에서는 서버가 내려 준 `setup` 바이트를 그대로 쓴다(8.3절).
- 머리말의 칸: `core_version [16]`은 `EndlessSetup.CoreVersion`(소문자 hex 32자)이 나타내는 16바이트를 글자 순서대로, `data_version u32`는 `EndlessSetup.DataVersion`(0 이상의 `int`), `kind u8`은 `RunKind`의 번호, `phantom_first`와 `season_tag_on`은 0 또는 1, `character`·`mutations`·`core`·`links`는 id의 번호(2.1절), `stage`·`slots_extra`는 `WandSnapshot`의 값, `unlocked_arts u16`은 `ArtMask`다. 수는 리틀 엔디언이다(1.4절). `phantom_first`가 `season_tag_on`보다 먼저다(클래스의 칸 순서와 다르다).
- 한 설정에 바이트 열은 하나다. 읽는 쪽은 다시 쓰면 같은 바이트가 되는 입력만 받는다(다른 `format_version`, `RunKind`에 없는 `kind`, 0·1이 아닌 참거짓, `int`를 넘는 `data_version`, 모자란 길이는 거절). 서버의 1차 검사가 바이트로 대조하므로(8.3절) 같은 설정의 다른 표기가 있으면 안 된다.
- 디버그 op(200~255)이 한 번이라도 적용된 판은 꼬리말의 `debug_used`가 켜진다. 서버는 이 플래그와 무관하게 본문에 디버그 op이 있으면 거절한다.

결투의 확정 입력 기록은 같은 틀에 슬롯 바이트를 더한 `0x05 DuelInput`(varuint Δtick, u8 slot, u8 offset, u8 key_down, varuint seq)을 쓰고, 머리말 대신 `DuelSetup`의 정규 바이트와 그 뒤의 `input_delay u8 × 2`(시뮬레이션 밖의 칸)를 싣는다(`magic "WZDR"`). `DuelSetup`의 정규 바이트의 칸 배치는 아직 적지 않았다. `DuelRoomOptions`의 칸이 정해질 때 이 절에 적는다(`DECISIONS-TECH.md`의 "아직 정하지 않은 기술 항목"). 서버가 만든 뗌 이벤트도 `0x05`로 실린다. 이것도 `Wizard.Sim`의 코덱이고 `ReplayRunner.RunDuel`이 읽는다.

### 11.2 콘텐츠 팩 `.wzpack`

소유: `Wizard.Content`(읽기), `tools/Wizard.DataBuild`(쓰기).

```
magic "WZPK"(4)  schema u16  data_version u32  core_compat [16]
section_count u16, 목차: { section_id u16, offset u32, length u32 } …
섹션들(정규 순서, id 오름차순으로 직렬화)
sha256 [32] (앞의 모든 바이트)
```

섹션 id는 고정이고 추가만 한다. 모르는 섹션은 건너뛴다. 모든 수는 이미 정수다(Raw, 틱, HU, 센티도, 천분율).

| 구간 | 섹션 |
|---|---|
| 1~19 전투(`data/sim/`, 코어 버전 해시 안) | 1 characters, 2 blocks, 3 arts, 4 mutations, 5 enemies, 6 bosses, 7 waves, 8 frames, 9 modifiers, 10 tod, 11 scoring, 12 arena, 13 duel_rules, 14 tries, 15 starfall, 16 weather_events, 17 boss_rotation, 18 elites, 19 elements |
| 20~39 규칙(`data/rules/`, 해시 밖) | 20 enhance, 21 economy, 22 drops, 23 leagues, 24 season, 25 titles, 26 cosmetics, 27 plaza, 28 shop, 29 items, 30 bag, 31 run_limits, 32 il, 33 fees, 34 net, 35 chat_filter |
| 40~ 전투(이어서) | 40 sim_limits(판당 틱 상한 등) |

이름 표는 팩의 섹션이 아니라 별도 파일 `names.<locale>.wznm`이다(5절). DataBuild가 팩과 같이 만들고 `StreamingAssets/content/`에 같이 놓는다.

### 11.3 오프라인 저장 파일

소유: `Wizard.Client.Save`. 근거: spec 08 8.5절.

`<persistentDataPath>/profiles/offline/profile.json`과 `.1`, `.2`, `.3`. UTF-8 JSON이고 마지막 줄에 본문의 SHA-256을 적는다(깨짐 감지용이며 변조 방지용이 아니다). 최상위 필드는 spec 08 8.5절과 같고 id만 정본을 쓴다(`"character": 1`, `"core": "block.magic_bolt"`, `"mat.stone_low"`). 미제출 서버 판은 `profiles/server/<account_id>/pending_runs/<run_id>.{json,wzrc}`에 둔다.

### 11.4 상태 파일

소유: `Wizard.Protocol.Boot`. 근거: spec 12 3.2절. `{ payload(base64url JSON), sig(Ed25519), key_id }`. payload의 필드는 spec 12 3.2절 그대로다. 클라이언트는 공개 키 2개를 내장하고, 마지막으로 받아 들인 `issued_at`보다 이른 파일은 버린다.

### 11.5 Unity `.meta`

소유: `tools/Wizard.MetaGen`(M0-BT-04). 근거: T-34.

- `src/`와 `unity/Assets/` 아래의 폴더·파일마다 `.meta`가 하나 있다. Unity가 아니라 `metagen`이 만들고 커밋한다.
- GUID = MD5(대상의 저장소 기준 상대 경로). 입력은 `.meta`를 뗀 대상 경로의 UTF-8 바이트이고, 구분자는 `/`, 앞에 `/`가 없고, 대소문자는 그대로다(예: `src/Wizard.Sim/Heat/HeatState.cs`). 출력은 hex 32자(소문자)다. 같은 경로면 어느 작업 트리, 어느 기기에서나 같은 GUID다.
- 이미 있는 `.meta`는 덮어쓰지 않는다. `metagen --check`는 빠진 `.meta`, 주인 없는 `.meta`, 중복 GUID를 찾고, `src/`와 `unity/Assets/Game/`에서는 GUID가 위 계산값과 같은지도 본다.
- 가져오기 블록(`MonoImporter`, `AssemblyDefinitionImporter`, `PackageManifestImporter`, `PluginImporter`, `folderAsset`)은 Unity가 쓰는 형식 그대로 쓴다. Unity가 연 뒤에도 바이트가 바뀌지 않아야 한다.

## 12. DB 스키마

소유: `Wizard.Server.Data`(`server/migrations/`). 근거: spec 12 8절이 바탕이고, 다른 명세가 더한 표와 이 문서의 조정을 합친다. 모든 시각은 `timestamptz`, 열거값은 `text + CHECK`.

### 12.1 spec 12 8.2절 DDL에서 바꾸는 것

| 대상 | 바꿈 | 이유 |
|---|---|---|
| `run.id` | `uuid PRIMARY KEY`(서버가 v7로 생성) | 기록 파일 머리말과 멱등 키가 16바이트(spec 09) |
| `run_checkpoint.run_id`, `verify_queue.run_id`, `account_debt.run_id`, `leaderboard_entry.run_id`, `wand.pending_run_id` | `uuid` | 위와 같음 |
| `run` | `token_hash`, `token_used_at`, `summary_hash`, `season_id`, `week_id`, `weekly_run_index`, `ranked_eligible_start`, `pause_total_ms`, `invalid_reason`, `last_contact_at`, `data_version`, `first_result jsonb` 추가 | spec 09 3.8절 |
| `run` | `setup_canonical bytea NOT NULL`(StartRun이 만든 `EndlessSetup`의 정규 바이트. unlocked_arts와 season_tag_on이 이 안에 있다), `facts_format smallint`, `claimed_facts bytea`, `claimed_digest bytea`(SHA-256) 추가 | 재실행의 시작 조건과 비교 대상을 서버가 가진다(8.3절). `summary_hash`는 중복 제출 판별용일 뿐이다 |
| `verify_queue` | `status`의 값에 `unverifiable` 추가. `result jsonb`의 모양 = `{ replay_status, facts_digest, ticks, fault_detail }`, 재실행 facts 바이트는 `result_facts bytea` 열 | 워커는 결과만 적는다. 비교는 `wizard-api`가 한다 |
| `account` | `play_seconds bigint NOT NULL DEFAULT 0 CHECK (play_seconds >= 0)` | spec 12 8.2절의 DDL에 있다. 12.2절 목록에서 빠져 있었다 |
| `request` | `fingerprint bytea`(요청 본문 지문), `result`는 NULL 허용(진행 중) | 7.2절 |
| `season_shop_purchase` (새 표) | account_id, season_id, sku, qty, at | 시즌 상점의 구매 한도 |
| `run_checkpoint.state_hash`, `run`의 최종 해시 | `bytea` 8바이트 | FNV-1a 64 |
| `verify_queue` | `core_version text`, `attempts smallint` 추가. 워커는 `WHERE core_version = <자기 버전>`으로 꺼낸다 | 버전별 워커 |
| `wand` | `pending_offer jsonb NULL`, `acquired_at timestamptz`, `debt_locked boolean` 추가 | spec 08 2.1절 |
| `duel_match.id` | `uuid`(경기 토큰, 내부 API의 멱등 키) | 7.3절 |
| `duel_match` | `reason text` 추가. `result`의 값은 `p1`, `p2`, `draw`, `void` | spec 10 17.1절 |

### 12.2 표 목록

| 묶음 | 표 | 핵심 열 | 근거 |
|---|---|---|---|
| 계정 | `account` | id, login_id, pw_hash, pw_params, email_enc, email_hmac, email_verified_at, nickname, flags, league, league_season_id, phantom_met, revenge_done_at, phantom_tries, phantom_best_dent, revenge_runs, trade_hold_until, max_wave_reached, play_seconds, show_name_in_alerts, deletion_requested_at | spec 12 |
| | `account_token`, `session`, `session_refresh_used`, `device`, `login_attempt`, `signup_log`, `recovery_request_log`, `mail_outbox` | spec 12 8.2절 그대로 | spec 12 |
| | `account_settings` | account_id, settings jsonb, updated_at | 설정 동기화(spec 13 13절) |
| | `account_chat_settings` | account_id, muted_channels, mute_all, whisper_policy, show_alerts | spec 11 4절 |
| | `account_flag` | id, account_id, flag, ref_type, ref_id, detail, at, reviewed_by, reviewed_at | spec 09 3.8절 |
| 멱등 | `request` | (account_id, request_id) PK, kind, result jsonb, at | spec 12 |
| 소유물 | `character_state` | (account_id, character) PK, equipped_wand_id, unlocked_arts jsonb, best_score, best_wave | spec 12 |
| | `wand` | id, owner_id, character, stage, floor_peak, record_peak, drop_count, aura, slots_extra, carve_bonus, mutations, pending_offer, loadout, skin_id, show_scars, origin, owner_count, trade_locked_until, lock_state, pending_run_id, debt_locked, acquired_at, version | spec 12, 08 |
| | `wand_pending_lock` | wand_id, run_id, created_at, cleared_at | spec 09 |
| | `item_stack` | (account_id, item_code) PK, qty, qty_pending, qty_socketed, qty_bound | spec 12 |
| | `item_instance` | id, owner_id, item_code, origin, bound, season_id, version | spec 12 |
| | `library_state`, `shop_purchase` | account_id, expansions / account_id, sku, qty, week_start, at | spec 08 8.7절 |
| 재화 | `currency_balance` | (account_id, currency, bucket) PK, amount, amount_pending | spec 12 |
| | `currency_ledger`, `item_ledger` | 추가만. delta, pending, reason, ref_type, ref_id, request_id, at | spec 12 |
| | `account_debt` | id, account_id, kind, item_code, qty, run_id, created_at, cleared_at | spec 12 |
| 강화 | `enhance_log` | id, wand_id, account_id, kind, stage_before, stage_after, prob_breakdown, roll, outcome, sacrifices, charm_used, at | spec 12 |
| 판 | `run` | id uuid, account_id, character, wand_id, wand_snapshot, seed, kind, start_wave, phantom_first, core_version, data_version, started_at, ended_at, submitted_at, wave, score, gold, drop_chances, loot, status, end_kind, ranked, replay_key + 12.1절의 열 | spec 12, 09 |
| | `run_checkpoint` | (run_id, wave) PK, tick, state_hash, score, wallet, replay_key, at | spec 12 |
| | `run_grant` | run_id, account_id, asset, qty_granted, qty_pending_left, state | spec 09 |
| | `verify_queue` | id, run_id UNIQUE, priority, status, result, worker, core_version, attempts, at | spec 12, 09 |
| | `weekly_challenge` | week_id PK, seed, starts_at, ends_at, settled_at | spec 09 |
| 순위·시즌 | `leaderboard_entry` | board, season_id, week_id, account_id, character, score, wave, run_id, start_wave, wand_stage, loadout, verified, at | spec 12, 09 |
| | `season`, `season_result` | spec 12 8.2절 + number, extended_seconds. 점수 열은 `bigint`(나노점) | spec 09 16절 |
| | `heir_tombstone` | season_id, account_id, nickname_at_time, character, wand_stage, total | spec 09 |
| 결투 | `duel_match` | id uuid, mode, league, p1, p2, snapshots, options, result, reason, rounds, input_delay, net_stats, replay_key, rated, at | spec 12, 10 |
| | `duel_rating` | (account_id, league, season_id) PK, rating, deviation, volatility, games | spec 12 |
| | `il_history`, `account_il`, `wand_il` | 14일 최고값과 캐시 | spec 10 17.1절 |
| | `account_season_league` | account_id, season_id, league, voluntary_used, promoted_at | spec 10 |
| | `duel_daily`, `duel_pair_count`, `duel_queue_ban` | 하루 보상·이탈, 같은 상대 판 수, 대기열 금지 | spec 10 |
| 거래 | `trade` | id, a_id, b_id, a_gives, b_gives, fee, appraised_a, appraised_b, at | spec 12 |
| | `market_listing`, `market_sale`, `market_price_stat` | 등록, 판매, 7일 중앙값 | spec 12, 11 |
| | `mail` | id, account_id, payload, reason, expires_at, claimed_at | spec 12 |
| 사회 | `friend`, `block`, `report`, `chat_log`, `plaza_instance` | spec 12 8.2절 그대로 | spec 12 |
| 운영 | `sanction`, `audit_log`, `config`, `server_heartbeat`, `server_downtime` | spec 12 8.2절 그대로 | spec 12 |
| 결제 | `product`, `purchase_order`, `payment_proof`, `entitlement`, `refund_event`, `redeem_code` | 출시 때 비어 있다 | spec 12 |

### 12.3 불변 규칙

- 재화·물건을 건드리는 유스케이스는 트랜잭션 하나에서 돈다. 바꿀 행은 먼저 `SELECT … FOR UPDATE`. 여러 계정은 번호 오름차순.
- 겹치는 물건과 잔액은 `CHECK (qty >= 0)`. 유료 구분만 환불 때 음수를 허용한다.
- 원장은 추가만 한다. 야간 대조가 "원장 합 = 잔액", "생성 − 소멸 = 현재 수량"을 확인한다.
- 지팡이의 "마지막 한 자루" 검사는 계정 행을 잠근 트랜잭션 안에서 센다.
- 결투 프로세스와 검증 워커는 재화·물건 표에 쓰지 않는다.

### 12.4 표마다의 셈과 확정 규칙

- **`play_seconds`**(거래·장터·자동 매치의 "누적 플레이 2시간", spec 11 6절, spec 12 U-36 [임시]): 서버 프로필로 접속해 있던 시간 가운데 자리 비움이 아닌 시간이다. 웹소켓 허브(M12-API-01)가 세고 60초마다, 그리고 연결이 끊길 때 더한다. 클라이언트가 보낸 값으로 더하지 않는다. 거래와 자동 매치는 허브보다 뒤에 만들어지므로 그 전에 HTTP 활동으로 어림하는 길은 두지 않는다.
- **비기 해금**(DECISIONS 2.8): 판의 facts에 실린 새 해금은 정산 트랜잭션 안에서 `character_state.unlocked_arts`에 더한다. 같은 `run_id`를 다시 제출해도 한 번만 반영된다. 해금도 `run_grant`에 `asset = art.<id>`로 적는다. 검증이 `mismatch`면 그 판의 해금을 되돌린다 [임시, OPEN-QUESTIONS]. 이미 그 해금을 가지고 출발한 뒤의 판은 그대로 유효하다(그 판의 `setup_canonical`에 들어 있으므로 재실행이 같다).
- **시즌 주화**: 얻는 길은 판의 드랍(spec 07 13.6절, `season_tag_on`일 때 정산에서)과 자동 매치(승리 6, 패배 3, 하루 첫 10판. spec 10 TV-M14·M15)다. 쓰는 곳은 시즌 상점이다. 시즌이 끝나면 남은 주화를 개당 10골드로 바꾼다(spec 09 T211). 이 환전은 시즌 정산 작업 안에서 계정당 한 번만 일어나고 원장에 `SeasonCoinConvert`로 남는다.
- **지팡이는 그 캐릭터만**(DECISIONS 2.4): `StartRun`, 든 지팡이 설정, 결투 입장(대기열, 방 준비, 도전 수락)에서 `wand.character ≠ 요청한 character`면 `NotEligible`이다.

## 13. 표시 계층 계약

소유: 엔진과 무관한 타입·열쇠·바인딩 표·연출 한도는 `Wizard.ViewLogic`(T-31), 텍스처와 재생기를 쥔 구현은 `Wizard.Unity.Presentation`. 근거: spec 14 5.7절, 각 명세의 연결점 절. 에셋이 없는 지금은 빈 구현과 도형으로 돈다. 아래 타입에는 `UnityEngine` 타입이 없다(`System.Numerics.Vector2`를 쓴다).

화면 쪽의 경계도 같다. 화면의 상태 기계와 뷰 모델, HUD 모델은 `Wizard.ViewLogic`에 있고(spec 13), `unity/Assets/Game/UI`의 뷰는 코드로 만든 uGUI 위젯으로 모델을 그리고 조작을 모델에 넘기기만 한다. 프리팹과 장면 편집을 쓰지 않는다(T-32). 뷰에는 규칙이 없다.

```csharp
// namespace Wizard.ViewLogic.Presentation   (SpriteRef·ClipRef는 불투명 핸들. Unity 쪽 구현이 실제 그림을 쥔다)
public enum AssetPack { Ui, Character, Wand, Monster, Fx, World, Background }
public readonly struct AssetKey {
    public readonly AssetPack Pack; public readonly string Id; public readonly string? Variant; public readonly string? State;
}
public interface IAssetCatalog {
    bool TryGetSprite(AssetKey key, int frame, out SpriteRef sprite);
    bool TryGetClip(AssetKey key, out ClipRef clip);
    AssetKey Placeholder(AssetKey missing);           // 회청 바둑판 + id 글자. 위험색·금색·연두·청록을 쓰지 않는다
}
public readonly struct FxRequest {
    public readonly string FxId;                      // event_bindings.json이 정한다 ("fx_lock_steam" 등)
    public readonly System.Numerics.Vector2 WorldPos; public readonly float AngleDeg;
    public readonly Element Element; public readonly int A, B;
    public readonly EventPhase Phase;                 // Retracted면 0.1초 흐려지며 사라진다
}
public interface IFxPlayer { void Play(in FxRequest request); void StopAll(); }
public interface ISfxPlayer { void Play(string sfxId, int pitchStep = 0, float volumeDb = 0f); void SetBus(SfxBus bus, float volume); }
public interface IPresentationEventHandler { void Handle(in PresentationEvent e, ISimView view); }

// namespace Wizard.ViewLogic
public static class FixConvert {                      // 고정소수점 → 실수는 코어 세 어셈블리 밖에서 **여기에만** 있다
    public static float ToFloat(Fix64 v);
    public static System.Numerics.Vector2 ToWorld(FixVec2 cellPos);   // 칸 → 표시 좌표(1칸 = 1유닛, y 뒤집기)
    public static float ToDegrees(Angle a);
}
// namespace Wizard.Unity.Presentation
public static class UnityConvert {                    // 엔진 타입으로 옮기기만 한다. Fix64를 직접 만지지 않는다
    public static UnityEngine.Vector2 ToUnity(System.Numerics.Vector2 v);
}

// namespace Wizard.ViewLogic.Presentation  — 코어 밖에서 생기는 표시 사건(화면, 통신, 광장, 알림, 거래, 시즌)
public static class UiEventKeys {                     // 문자열 열쇠의 카탈로그. 열쇠는 추가만 한다
    // 구역: "ui.*"(화면 조작, 강화 의식), "net.*"(연결, 대기열, 회선), "duel.*"(매치·점수·입력 버려짐·여유 띠),
    //       "plaza.*", "notice.*"(서버 전체 알림), "trade.*", "season.*", "run.*"(제출·검증 상태)
    public const string DuelMatchFound = "duel.match_found";      // 아래 다섯은 spec 10 23절의 예
    public const string DuelQueueEnter = "duel.queue_enter";
    public const string DuelRatingChanged = "duel.rating_changed";
    public const string DuelInputDropped = "duel.input_dropped";
    public const string DuelMarginBandChanged = "duel.margin_band_changed";
    /* … spec 09 22절, spec 10 23절(코어 밖의 것), spec 11 17절, spec 12 17절, spec 13 21절의 이름 전부. M9-PRS-01이 채운다 */
    public static IReadOnlyList<string> All { get; }
}
public readonly struct UiEventArgs { public readonly int A, B; public readonly string? Id; public readonly Element Element; }
public sealed class PresentationEventRouter {
    public void Handle(in PresentationEvent e, ISimView view);        // 코어 이벤트: SimEventDocs.BindingKey로 바인딩을 찾는다
    public void RaiseUi(string uiEventKey, in UiEventArgs args);      // 코어 밖 사건: 같은 바인딩 표에서 찾는다
}
```

`data/presentation/event_bindings.json`의 한 줄:

```json
{ "event": "heat.lock_entered", "fx": ["fx_lock_x", "fx_lock_steam"], "sfx": ["sfx_c07"],
  "shake": "none", "hitstop_ms": 0, "confirmed_only": true }
```

- `event`는 `SimEventDocs.BindingKey`가 돌려주는 열쇠이거나 `UiEventKeys`의 열쇠다. 두 카탈로그에 같은 열쇠가 있으면 테스트가 실패한다(결투처럼 코어 이벤트와 코어 밖 사건이 같은 구역 이름을 쓸 수 있으므로 열쇠 단위로 겹침을 막는다). `fx`, `sfx`의 식별자는 `effects.md` 20절과 19절의 이름을 쓴다.
- **빠짐 검사**: `SimEventType`의 모든 값과 `UiEventKeys.All`의 모든 열쇠는 바인딩 표에 줄이 있거나, 같은 폴더의 `event_bindings.none.json`(의도적으로 연출이 없는 사건의 목록)에 있어야 한다. 각 명세의 연결점 절(spec 01 17, 02 19, 03 21, 04 18, 05 14, 06 17, 07 20, 08, 09 22, 10 23, 11 17, 12 17, 13 21)에 나오는 사건 이름은 두 카탈로그 가운데 하나에 있어야 한다. 테스트가 확인한다(M9-PRS-01). 에셋을 나중에 꽂을 식별자가 빠지지 않게 하는 장치다.
- 대응이 없는 이벤트는 조용히 지나간다. 개발 빌드는 한 번 기록한다.
- 흔들림, 번쩍, 그림 정지의 세기와 줄이기 설정은 표시 계층이 집행한다(spec 13 14절). 코어는 `HitstopRequest`와 사건만 알린다.
