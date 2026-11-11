# 12. 서버 구성 · 계정/인증 · 운영 · 결제 뼈대 — 구현 명세서

게임: **엄청엄청 위대한 마법사 키우기**. 대상: Unity 2D URP 클라이언트(C#) + 맥미니 한 대의 C# 서버(ASP.NET Core, 자체 UDP 결투 프로세스, 검증 워커) + PostgreSQL.

## 0. 이 문서를 읽는 법

### 0.1 원본과 우선순위

| 순위 | 문서 | 이 명세가 가져온 곳 |
|---|---|---|
| 1 | `docs/DECISIONS.md` | 1.13, 1.14, 1.16, 1.17, 1.22, 1.24, 2.4, 2.6, 2.7 |
| 2 | `docs/online.md` | **7절 전체, 9절, 10절, 11절, 12절, 14절** (본 명세의 원본). 교차 확인: 0절, 1.1절, 1.2절, 3.1~3.5절, 4.9~4.12절, 5.3~5.6절, 6.2절, 6.5절, 8절 |
| 3 | `docs/GDD.md` | 16절(만드는 순서), 19절(온라인 요약), 확인 목록 62·75·76번 |
| 3 | `docs/ui-assets.md` | 4.3절(로그인·가입·복구 판), 4.5절(메인 메뉴의 "이메일 미확인" 꼬리표), 4.19절(설정 > 계정), 4.20절(닉네임 폭), 공지 판 |

DECISIONS와 다른 문서가 다르면 DECISIONS를 따른다. 이 명세 안에서 "원본"은 `online.md`를 말한다.

### 0.2 표시

- **[확정]**: DECISIONS의 [개발자] 항목, 또는 원본에서 [개발자]로 표시된 것.
- **[조율]**: DECISIONS 2절(공통 규격)이 정한 것, 또는 원본이 [조율]로 표시한 것.
- **[제안]**: 원본의 설계안(원본에서 표시가 없는 수치는 모두 [제안]이다. `online.md` 머리말). QA에서 조정하는 시작값이다(DECISIONS 1.17).
- **[임시]**: 원본이 정하지 않아 이 명세가 구현을 막지 않으려고 넣은 기본값. 16절 "미정"에 모두 다시 모았다.
- 출처 표기: `(online 7.3)` = `docs/online.md` 7.3절. `(DEC 1.24)` = `docs/DECISIONS.md` 1절 24번.

### 0.3 단위 환산 규칙

- 시뮬레이션 틱: **60틱/초**, 1틱 = 16.667ms (online 3.1). 시간 → 틱 환산은 `ticks = seconds × 60`. 서버 운영 시간(토큰 수명, 메일 한도 등)은 틱과 무관한 **벽시계(서버 시각, UTC 저장)** 로 다룬다. 틱 환산은 시뮬레이션이나 결투 프로세스와 닿는 값에만 적는다.
- 광장 동기화: 10틱/초(광장 1틱 = 100ms = 시뮬레이션 6틱) (online 5.1, 7.2).
- 고정소수점: **Q32.32 정수**(`long`, 1.0 = 2^32 = 4,294,967,296) (online 3.1). 칸 단위 값은 이 형식으로 둔다. 예: 0.5칸 = `0x0000_0000_8000_0000` = 2,147,483,648.
- 시각 저장: PostgreSQL `timestamptz`(UTC). "서버 시각"이 기준이다. 클라이언트 시계는 믿지 않는다 (online 7.8).
- 크기: KB = 1,000바이트, MB = 1,000,000바이트로 읽는다(원본은 구분하지 않음. Argon2 메모리 64MB만 64 MiB = 65,536 KiB로 둔다 [임시]).

### 0.4 이 문서가 정하지 않는 것

시각 자원(GUI 그림, 스프라이트, 이펙트 그림, 소리)은 구현하지 않는다. 대신 식별자와 이벤트 이름을 17절에 둔다. 결투 시뮬레이션 규칙, 광장 기능, 거래 규칙의 세부, 시즌 점수 식은 각각의 명세가 주인이다. 이 문서는 그것들이 쓰는 **표, 트랜잭션 규칙, 서버 프로세스**만 정한다.

일정·마감·범위 삭감은 다루지 않는다 (DEC 1.16). 11절은 의존 순서만 적는다.

---

## 1. 서버 구성

### 1.1 기술 선택 (online 7.1) [확정: 맥미니 한 대 / 제안: C 채택]

| 선택지 | 평가 | 결론 |
|---|---|---|
| A. Nakama를 맥미니에서 운영 | 서버 함수가 Go/TypeScript/Lua라 C# 결정론 코어를 못 부른다. 결투·검증을 별도 C# 프로세스로 빼면 서버 언어가 둘, 자체 지갑과 원장이 겹친다 | 버림 |
| B. 관리형 백엔드(PlayFab, UGS 등) | "맥미니에 직접 올린다"와 어긋난다 (DEC 1.13) | 버림 |
| **C. 전부 C# (ASP.NET Core + 자체 UDP 서버)** | 결정론 코어를 서버 안에서 바로 쓴다. 트랜잭션과 원장이 한 곳. 친구·채팅·매치·순위표는 직접 만든다 | **채택** |

- 서버 함수 언어 = **C# 하나**, DB = **PostgreSQL 하나**, 원장 = **하나**. (online 7.1) [제안]
- 스팀의 인증, 중계, 지갑, 친구 기능은 하나도 쓰지 않는다. (online 7, DEC 1.13, DEC 2.6) [확정]
- 범위를 줄이지 않는다: 친구·채팅·매치·순위표를 직접 만든다. (online 7.1, DEC 1.16) [확정]

### 1.2 프로세스와 구성 요소 (online 7.2) [제안]

```
[Unity 클라이언트]
  ├ Wizard.Sim        결정론 전투 코어(순수 C#, 유니티 무관)
  ├ Wizard.Rules      강화·정산·상점·슬롯 새기기 규칙(서버와 공유하는 C# 라이브러리, online 8.3)
  ├ IBackend          ServerBackend(HTTPS + 웹소켓) / OfflineBackend(기기 안 저장 파일)
  ├ IDuelTransport    UdpDuelTransport / WebSocketDuelTransport / LoopbackTransport
  └ IPurchaseFlow     출시 빌드에는 구현 등록 없음(9절)

[상태 파일]  맥미니 밖 정적 호스팅 1곳. 서명된 JSON(3.2절)

[맥미니 한 대]
  ├ Caddy            TLS 종료(인증서 자동 갱신), :443 → wizard-api, IP당 요청 제한
  ├ wizard-api       ASP.NET Core. 계정, 인벤토리, 강화, 거래, 장터, 정산, 시즌, 채팅, 친구,
  │                  매치메이커, 광장 인스턴스(웹소켓, 10틱), 결제 뼈대
  ├ wizard-duel      C#. UDP 포트 1개. 경기마다 Wizard.Sim을 60틱으로 돌린다
  ├ wizard-verify×2  C# 콘솔. verify_queue에서 판 기록을 꺼내 Wizard.Sim으로 재실행
  ├ PostgreSQL       DB 1개
  ├ (나가는 쪽) 메일  wizard-api → IMailSender → 외부 메일 사업자 HTTPS API(받는 포트 없음)
  ├ wizard-admin     관리 웹. 맥미니 안에서만 열림(밖에서는 SSH 터널)
  └ 백업 작업        launchd 주기 작업(5절)
```

| 요소 | 선택 | 이유 | 출처 |
|---|---|---|---|
| 서버 언어 | C# (.NET LTS, macOS arm64) | 코어와 같은 언어 | online 7.2 [제안] |
| DB | PostgreSQL | 거래·재화는 관계형 트랜잭션 | online 7.2 [제안] |
| API | HTTPS(JSON) + 웹소켓 | 요청·응답은 HTTPS. 광장·채팅·알림·매치는 웹소켓 | online 7.2 [제안] |
| 결투 | UDP 직접, 못 쓰면 웹소켓 | online 4.11 | online 7.2 [조율] |
| 프로세스 관리 | launchd(`KeepAlive`). 죽으면 자동 재시작 | 맥 기본 기능만 | online 7.2 [제안] |
| 이식성 | 맥 전용 기능 금지 | 고장 나면 다른 맥이나 리눅스 PC에 같은 프로세스·DB | online 7.2 [제안] |

**구현 식별자 [제안]**

| 식별자 | 종류 | 메모 |
|---|---|---|
| `Wizard.Sim` | 어셈블리 | 클라이언트·`wizard-duel`·`wizard-verify`가 같은 소스. 코어 버전 해시 `CoreVersionHash` 노출 |
| `Wizard.Rules` | 어셈블리 | 서버와 `OfflineBackend`가 같은 소스 (online 8.3) |
| `Wizard.Server.Api` | 프로젝트 | `wizard-api` |
| `Wizard.Server.Duel` | 프로젝트 | `wizard-duel` |
| `Wizard.Server.Verify` | 프로젝트 | `wizard-verify` |
| `Wizard.Server.Admin` | 프로젝트 | `wizard-admin` (바인딩 `127.0.0.1` 전용) |
| `Wizard.Server.Data` | 프로젝트 | DB 접근, 마이그레이션, 트랜잭션 도우미 |
| launchd 라벨 | `com.wizard.api`, `com.wizard.duel`, `com.wizard.verify.1`, `com.wizard.verify.2`, `com.wizard.admin`, `com.wizard.backup.wal`, `com.wizard.backup.daily`, `com.wizard.ddns` | [임시] |

### 1.3 서버 쪽 공통 규칙 (online 5.6, 7.3, 7.9)

1. `wizard-api`의 **모든 요청은 접근 토큰을 확인한 뒤에야 계정 번호를 얻는다. 클라이언트가 보낸 계정 번호는 믿지 않는다.** (online 7.3) [제안]
2. 재화와 물건을 건드리는 서버 함수는 전부 PostgreSQL 트랜잭션 하나(`BEGIN … COMMIT`) 안에서 돈다. 바꿀 행은 `SELECT … FOR UPDATE`로 먼저 잠근다. 대상: 강화, 거래, 장터, 정산, 우편 수령, 슬롯 새기기, 상점 구매, 결제 지급. (online 5.6) [제안]
3. 여러 계정 행을 잠글 때는 **계정 번호 오름차순**으로 잠근다(교착 방지). (online 5.6) [제안]
4. 모든 변경 요청에는 클라이언트가 만든 요청 번호(`request_id`)가 붙는다. `request(account_id, request_id)` 유일 제약, 처음 결과를 저장, 같은 번호가 다시 오면 저장된 결과를 돌려준다. 30일 보관. 판 정산은 `run_id`, 결제는 `order_id`가 같은 역할. (online 5.6, 7.9) [제안]
5. 원장은 하나: 재화는 `currency_ledger`, 물건은 `item_ledger`. 추가만 한다. 다른 지갑·저장소 기능 없음. (online 5.6, 7.9) [제안]
6. 수치(확률표, 비용 계수, 드랍 표, 리그 경계, 수수료)는 코드가 아니라 `config`와 **버전이 붙은 데이터 파일**에 둔다. 같은 데이터 파일이 클라이언트 빌드에도 들어가 오프라인 프로필이 쓴다. 전투 수치는 결정론 코어 버전에 묶인다. (online 7.9) [제안]
7. 비밀번호와 토큰은 로그에 쓰지 않는다. 이메일 주소는 로그에 평문으로 쓰지 않는다. (online 7.3, 7.10) [제안]

**멱등 처리 의사코드**

```
function HandleMutation(ctx, requestId, kind, body):
    accountId = AuthenticateAccessToken(ctx)          // 1.3-1
    BEGIN
      existing = SELECT result FROM request
                 WHERE account_id = accountId AND request_id = requestId
                 FOR UPDATE                            // 같은 번호 동시 2건 대비
      if existing != null:
          ROLLBACK; return existing.result             // 같은 결과 그대로
      INSERT INTO request(account_id, request_id, kind, result, at)
             VALUES (accountId, requestId, kind, '{"pending":true}', now())
             -- 유일 제약 충돌 시: 재시도 루프에서 SELECT로 돌아간다
      result = DoBusinessLogic(accountId, body)        // 행 잠금 + 원장 기록
      UPDATE request SET result = result WHERE ...
    COMMIT
    return result
```

- 경계: 비즈니스 로직이 "규칙상 거절"(잔액 부족 등)을 돌려도 그 거절 결과를 저장하고 커밋한다(같은 번호로 다시 보내면 같은 거절). DB 오류·직렬화 실패로 롤백되면 `request` 행도 남지 않아 다시 보낼 수 있다. [임시]
- `request_id` 형식: UUID v4 또는 v7(클라이언트 생성). [임시]

---

## 2. 계정과 인증 (online 7.3) [조율: 자체 계정 / 확정: 이메일로 복구]

### 2.1 가입 입력 규칙

| 필드 | 규칙 | 식별자 | 출처 |
|---|---|---|---|
| 아이디 | 영문 소문자·숫자 4~16자, 유일. 정규식 `^[a-z0-9]{4,16}$` | `login_id` | online 7.3 [제안] |
| 비밀번호 | 10자 이상. 문자 종류 강제 없음. 흔한 비밀번호 목록(상위 10만 개)에 있으면 거절. 아이디를 포함하면 거절 | `password` (평문은 저장·로그 금지) | online 7.3 [제안] |
| 닉네임 | 한글·영문·숫자 2~8자, 유일. 화면의 닉네임 칸 폭은 이 값으로 잡는다(한글 8자 = F-16으로 128px, `ui-assets.md` 4.20) | `nickname` | online 7.3 [제안], GDD 확인 62번 |
| 이메일 | **가입할 때 받는다** [확정 DEC 1.24]. 형식만 검사: `@`와 도메인 꼴, 254자 이하 | `email` | online 7.3 |
| 받지 않는 것 | 실명, 전화번호, 생년월일 | | online 7.3, 10 [제안] |

- 이메일은 **비밀번호 복구와 보안 알림에만** 쓴다. (online 7.3) [확정 + 제안]
- 닉네임·아이디에는 금칙어 필터, 운영자·NPC 사칭 금지어를 적용한다. 신고 시 운영자가 강제 변경할 수 있다. (online 10) [제안]

**검증 의사코드**

```
function ValidateSignup(loginId, password, nickname, email):
    if !Regex("^[a-z0-9]{4,16}$").Match(loginId)       -> ERR_LOGIN_ID_FORMAT
    if Exists(account.login_id == loginId)             -> ERR_LOGIN_ID_TAKEN
    if len(password) < 10                              -> ERR_PW_TOO_SHORT
    if len(password) > 128                             -> ERR_PW_TOO_LONG        // [임시]
    if password.ToLowerInvariant().Contains(loginId)   -> ERR_PW_CONTAINS_ID
    if CommonPasswords100k.Contains(password)          -> ERR_PW_COMMON          // 대소문자 그대로 비교 [임시]
    if !NicknameValid(nickname)                        -> ERR_NICK_FORMAT
    if NameFilter.Blocked(nickname) or NameFilter.Blocked(loginId) -> ERR_NAME_BLOCKED
    if Exists(account.nickname == nickname)            -> ERR_NICK_TAKEN
    if len(email) > 254 or !EmailShape(email)          -> ERR_EMAIL_FORMAT
    if Count(account.email_hmac == Hmac(Normalize(email))) >= 3 -> ERR_EMAIL_LIMIT
    if SignupCount(ip, last 24h) >= 3                  -> ERR_SIGNUP_LIMIT
    if SignupCount(device_hash, last 24h) >= 3         -> ERR_SIGNUP_LIMIT
    return OK

NicknameValid(n): 2 <= len(n) <= 8 and every char in [가-힣] ∪ [A-Za-z] ∪ [0-9]
                  // 한글은 완성형 음절만, 자모 낱자 불가 [임시]. 길이는 UTF-16 code unit이 아니라 문자 수
EmailShape(e):    정확히 하나의 '@', 앞부분 1자 이상, 뒷부분에 '.'이 있고 마지막 라벨 2자 이상 [임시]
Normalize(e):     Trim(e).ToLowerInvariant()                       (online 7.3 찾기용 값)
```

- 아이디 유일성: `login_id`는 소문자만 허용하므로 대소문자 문제가 없다. 닉네임 유일성은 대소문자 무시로 본다(`lower(nickname)` 유일 색인) [임시].
- 오류 코드는 가입 화면용이다. **로그인 실패 문구는 하나만** 쓴다(2.4절).

### 2.2 비밀번호 저장 (online 7.3) [제안]

| 항목 | 값 |
|---|---|
| 알고리즘 | **Argon2id** |
| 메모리 | 64MB (= 65,536 KiB [임시 해석]) |
| 반복 | 3 |
| 병렬 | 1 |
| 목표 시간 | 맥미니에서 0.1초 안팎이 되게 맞춘다(실측으로 메모리·반복 조정) |
| 소금 | 계정마다 16바이트 무작위(CSPRNG) |
| pepper | DB 밖 파일에 둔 서버 비밀값을 섞는다 |
| 출력 길이 | 32바이트 [임시] |
| 원문 | 어디에도 저장하지 않는다. 로그에도 남기지 않는다 |
| 설정 상향 | 설정을 올리면 **다음 로그인 때** 새 설정으로 다시 해시한다 |
| 동시 계산 | **동시에 4건까지** 계산하고 나머지는 줄을 세운다 |

**pepper 섞는 방법 [임시]**: Argon2id의 `secret`(키) 입력에 pepper를 넣는다. 라이브러리가 지원하지 않으면 `pre = HMAC-SHA256(pepper, password_utf8)`을 Argon2id의 비밀번호 입력으로 쓴다. 어느 쪽을 쓰는지 `pw_params.pepper_mode`에 적는다.

`pw_params` (jsonb) 스키마 [제안]:

```json
{ "alg": "argon2id", "v": 19, "m_kib": 65536, "t": 3, "p": 1,
  "salt_len": 16, "out_len": 32, "pepper_id": 1, "pepper_mode": "secret" }
```

**해시 대기열 의사코드**

```
static SemaphoreSlim HashGate = new(4)                // 동시 4건
async Task<bool> VerifyPassword(account, pw):
    await HashGate.WaitAsync(timeout: 10s)              // [임시] 넘으면 503 "잠시 후 다시"
    try:
        ok = Argon2id.Verify(account.pw_hash, pw, account.pw_params, Pepper[account.pw_params.pepper_id])
        if ok and account.pw_params != CurrentParams:
            account.pw_hash, account.pw_params = Argon2id.Hash(pw, NewSalt16(), CurrentParams)
        return ok
    finally: HashGate.Release()
```

- 없는 아이디로 로그인해도 **같은 비용의 가짜 해시 계산**을 한 번 해서 응답 시간을 맞춘다(아이디 존재를 알 수 없게. 2.4절 요구의 구현) [임시].

### 2.3 토큰과 세션

| 항목 | 값 | 출처 |
|---|---|---|
| 로그인 | TLS 위에서 아이디와 비밀번호를 보낸다. 서버가 접근 토큰과 갱신 토큰을 준다 | online 7.3 [제안] |
| 접근 토큰 | 무작위 32바이트. 수명 **30분**. 클라이언트는 메모리에만 둔다. 서버는 SHA-256 해시만 저장 | online 7.3 [제안] |
| 갱신 토큰 | 무작위 32바이트. 수명 **30일**. **쓸 때마다 새것으로 바뀐다**(회전). 기기의 보안 저장소(macOS 키체인, Windows 자격 증명 저장소)에 둔다 | online 7.3 [제안] |
| 재사용 탐지 | 이미 쓴 갱신 토큰이 다시 오면 탈취로 보고 **그 기기의 토큰을 전부 폐기** | online 7.3 [제안] |
| 동시 접속 | 계정당 **게임 접속 하나**. 새 접속이 기존 접속을 끊는다 | online 7.3, 6.6 [제안] |
| 비밀번호 변경 | 현재 비밀번호 확인 → 변경 → **모든 기기의 토큰 폐기** | online 7.3 [제안] |
| 결투 패킷 인증 | 경기마다 서버가 TLS 연결로 내려 준 키(ChaCha20-Poly1305, 패킷 번호로 재전송 차단) | online 7.3, 4.11 [조율] |

- 토큰 표현: 32바이트 → base64url(패딩 없음) 43자 [임시]. 서버 저장: `SHA-256(token_bytes)` 32바이트.
- 틱 환산: 접근 토큰 30분 = 108,000틱 (참고용, 시뮬레이션과 무관).

**세션 상태 기계 [제안 + 임시]**

```
상태: Active → (refresh 사용) → Rotated(이전 refresh_hash는 "사용됨"으로 기억)
      Active → Revoked   (로그아웃 / 비밀번호 변경 / 재설정 성공 / 재사용 탐지 / 운영 제재 / 계정 삭제)
      Active → Expired   (refresh_expires_at 지남)

갱신 요청(refresh_token R):
    h = SHA256(R)
    s = SELECT * FROM session WHERE refresh_hash = h FOR UPDATE
    if s == null:
        p = SELECT * FROM session_refresh_used WHERE refresh_hash = h       // 재사용 탐지용 기록 [제안]
        if p != null:
            UPDATE session SET revoked_at = now() WHERE family_id = p.family_id AND revoked_at IS NULL
            emit security.refresh_reuse_detected(p.account_id, p.device_id)
        return 401
    if s.revoked_at != null or s.refresh_expires_at <= now(): return 401
    INSERT session_refresh_used(refresh_hash = s.refresh_hash, family_id = s.family_id, account_id, device_id, at)
    s.access_hash  = SHA256(newA = Random32()); s.access_expires_at  = now() + 30min
    s.refresh_hash = SHA256(newR = Random32()); s.refresh_expires_at = now() + 30days
    return (newA, newR)
```

- `family_id`: 한 기기에서 한 번 로그인해 생긴 토큰 사슬. "그 기기의 토큰 전부"는 같은 `device_id`의 모든 활성 세션으로 넓혀 폐기한다(원문 "그 기기"에 맞춤) [임시].
- 갱신 토큰 수명 30일을 회전 때마다 새로 30일로 잡는다(슬라이딩) [임시].
- 동시 접속 하나: 웹소켓 연결 표 `live_connection(account_id)`를 메모리에 둔다. 새 연결이 인증되면 기존 연결에 `session.kicked`(사유: 다른 기기 접속)를 보내고 닫는다. 진행 중 결투가 있으면 결투 프로세스는 이탈 규칙(online 4.10)을 그대로 적용한다 [임시].

### 2.4 틀린 로그인 대기 (online 7.3) [제안]

| 규칙 | 값 |
|---|---|
| 계정당 | **5회 틀리면** 1초부터 두 배씩 늘어나는 대기, 최대 **15분** |
| IP당 | **분당 20회** |
| 오류 문구 | 아이디가 있는지 없는지 알 수 없게 **하나만** 쓴다 |

**알고리즘 [임시 해석]**: 마지막 성공 이후의 연속 실패 수 `n`.

```
WaitSeconds(n) = 0                         if n < 5
               = min(2^(n-5), 900)         if n >= 5      // 1, 2, 4, …, 512, 900, 900 …
로그인 시도 시:
    if IpAttempts(ip, last 60s) >= 20                  -> 429, 같은 문구
    n = ConsecutiveFailures(login_id)                    // login_attempt 표에서 계산
    lastFail = LastFailureAt(login_id)
    if n >= 5 and now() < lastFail + WaitSeconds(n):   -> 429 "잠시 후 다시 시도해 달라"(남은 초 표시)
    ok = VerifyPassword(...)                             // 없는 아이디도 가짜 해시
    INSERT login_attempt(login_id, ip, at=now(), ok)
    if !ok -> 401 "아이디 또는 비밀번호가 맞지 않다"
```

- 대기는 계정(아이디) 단위로 센다. 없는 아이디도 같은 방식으로 세고 같은 문구를 쓴다 [임시].
- 대기 중 맞는 비밀번호를 넣어도 대기가 끝날 때까지 거절한다 [임시].
- `login_attempt`는 30일 뒤 삭제한다 (online 7.9).

### 2.5 가입 남용과 기능별 플레이 조건 (online 7.3, 10, 4.9, 5.3, 5.5) [제안]

| 한도 | 값 |
|---|---|
| IP당 가입 | 하루 3계정 |
| 기기당 가입 | 하루 3계정 |
| 이메일 주소 하나에 | 계정 3개까지 |
| 채팅 | 새 계정은 무한 웨이브 10에 닿기 전까지 **주변 채널만** (online 5.3) |
| 거래(직접 거래, 장터 등록·구매, 선물) | 무한 웨이브 10 도달 + 누적 플레이 2시간 (online 5.5) |
| 자동 매치 | 무한 웨이브 10 도달 + 누적 플레이 2시간. 같은 기기에서 자동 매치를 쓸 수 있는 계정 3개까지 (online 4.9) |
| 같은 기기 계정 사이 거래 | 표시(탐지 대상) (online 10, 6.6) |

- "하루" = 지난 24시간 이동 창 [임시].
- 누적 플레이 2시간 = `account.play_seconds >= 7200`. `play_seconds`를 무엇으로 셀지(접속 시간 / 판 시간)는 원본이 정하지 않았다 → 16절 [임시: 서버 프로필로 접속해 있던 시간 중 자리 비움이 아닌 시간].

### 2.6 새 기기와 거래 보류 (online 7.3, 10) [제안]

다음 셋은 모두 **24시간 동안 거래와 장터 등록을 막는다**:

| 사유 | 시작 시각 | 출처 |
|---|---|---|
| 처음 보는 기기에서 로그인 | 그 로그인 시각 | online 7.3 |
| 이메일 주소 바꾸기 완료 | 교체 시각 | online 7.3 |
| 비밀번호 재설정 성공 | 재설정 시각 | online 7.3 |

구현 [제안]: `account.trade_hold_until = max(trade_hold_until, now() + 24h)`. 거래·장터 등록 서버 함수는 `now() < trade_hold_until`이면 `ERR_TRADE_HOLD`(남은 시간 포함)로 거절한다. 장터 **구매**도 막는지는 원문이 "거래와 장터 등록"이라 적었으므로 **구매는 막지 않는다** [임시. 16절].

- 기기 식별: 클라이언트가 설치 때 만든 무작위 기기 값(키체인 저장)을 보내고 서버는 `device_hash = HMAC-SHA256(device_key, 값)`만 저장한다 [임시]. 원문은 "기기 식별값의 해시"(online 10) [제안].

### 2.7 계정 삭제 (online 7.3, 7.9) [제안]

```
상태: Active ──(설정에서 요청, 비밀번호 확인 [임시])──▶ DeletionPending(requested_at)
      DeletionPending ──(7일 경과, 배치 작업)──▶ Deleted
      DeletionPending ──(본인이 로그인해 취소 [임시])──▶ Active
```

- 7일 뒤 **계정 행을 지우고 원장에는 익명 번호만 남긴다**. 이메일(암호문 `email_enc`와 찾기용 `email_hmac`)과 남은 확인·재설정 코드(`account_token`)를 그때 같이 지운다.
- 구현 [임시]: 원장(`currency_ledger`, `item_ledger`)의 `account_id`는 FK를 걸지 않은 숫자다. 계정 행이 지워져도 번호만 남으면 그것이 "익명 번호"다. 원장은 추가만 하는 표이므로 행을 고치지 않는다.
- 삭제 대기 중 거래·장터·결투 가능 여부는 원문에 없다 → [임시] 대기 중에는 거래·장터·선물 금지, 플레이는 가능.
- 삭제 확정 때 함께 지울 것 [임시]: `session`, `device`, `character_state`, `wand`(소유), `item_stack`, `item_instance`, `currency_balance`, `friend`, `block`, `mail`, `request`. 남길 것: 원장, `enhance_log`, `trade`, `market_sale`, `duel_match`(상대 기록), `audit_log`, `season_result`(역대 후계자 비석의 닉네임은 영구 — 16절).

### 2.8 메일 발송 (online 7.3) [확정: 이메일 복구 / 제안: 방식]

**방식 선택**

| 선택지 | 평가 | 결론 |
|---|---|---|
| A. 맥미니에서 직접 보낸다 | 가정용 IP는 차단 목록, 나가는 25번 포트 차단 흔함, 역방향 DNS 불가 → 스팸함/거절 | **쓰지 않는다** |
| **B. 외부 트랜잭션 메일 사업자 HTTPS API** | 나가는 443만. 도달률은 사업자. 발송 결과(반송, 거절)를 API로 조회. 하루 수십 통 규모 | **채택(기본)** |
| C. 외부 사업자 SMTP 릴레이(587, STARTTLS + 인증) | 표준이라 바꾸기 쉬움. 반송 정보가 거칠고 587이 막힌 회선에서 안 됨 | 예비 |
| D. 개인 메일 계정 SMTP | 하루 한도, 약관, 보낸 사람이 개인 주소 | 개발 중 시험용만 |

**인터페이스 [제안]**

```csharp
public enum MailKind { VerifyEmail, ResetPassword, FindLoginIds, NotifyPasswordChanged, NotifyEmailChanged }

public interface IMailSender {
    string Id { get; }                 // "api", "smtp-relay", "null"
    bool IsEnabled { get; }            // NullMailSender는 false
    Task<MailSendResult> SendAsync(string toAddress, MailKind kind, MailBody body, CancellationToken ct);
    Task<IReadOnlyList<MailDeliveryEvent>> PollDeliveryAsync(DateTime since); // 반송·거절 조회(API형만)
}
public sealed record MailSendResult(bool Accepted, bool Retryable, string? ProviderRef, string? Error);
```

- 구현 셋: `ApiMailSender`(B, 기본), `SmtpRelayMailSender`(C, 예비), `NullMailSender`(사업자 미설정). 어느 것을 쓸지는 `config`가 정한다(`mail.sender` = `api` | `smtp-relay` | `null`). `IPaymentProvider`와 같은 생각: 연결하지 않아도 나머지가 전부 돈다. (online 7.3)
- 보내는 주소: 7.4절(본 명세 3.1)에서 산 도메인의 주소(예: `no-reply@`). 도메인 DNS에 **SPF, DKIM, DMARC** 레코드(사업자가 주는 값). (online 7.3)
- 사업자 선택 기준: 국내 메일 서비스로의 도달, DKIM 서명, 발송 기록 조회, 월 수백 통 수준의 요금, 발송 기록 보관 기간을 짧게 둘 수 있는지. **어느 사업자를 쓸지는 정하지 않았다** (online 7.3, 14-1).
- 메일 형식: **한글 한 종류의 글자 메일**(그림, 추적 픽셀, 눌러서 들어가는 링크 없음). 본문에는 **코드, 만료 시각, "요청한 적이 없으면 무시하라"**만. 아이디와 닉네임은 적지 않는다. 예외: [아이디 찾기] 메일은 그 주소로 확인된 계정의 **아이디 목록만** 적는다(코드와 닉네임 없음). (online 7.3)
- 알림 메일(비밀번호가 바뀜, 이메일이 바뀜) 본문 [임시]: 바뀐 시각과 "본인이 아니면 즉시 비밀번호 재설정을 하라"만. 아이디·닉네임 없음.
- 메일을 쓰는 곳은 **셋뿐**: 가입 확인, 비밀번호 재설정(같은 화면의 [아이디 찾기] 포함), 보안 알림(비밀번호 변경, 이메일 변경). 공지·광고·시즌 소식에 쓰지 않는다. (online 7.3)
- **미확인 주소에는 확인 메일 말고는 아무것도 보내지 않는다.** (online 7.3) → 보안 알림도 확인된 주소에만 보낸다.

**발송 대기열(`mail_outbox`) 상태 기계 [제안 + 임시]**

```
Queued ──(SendAsync Accepted)──▶ Sent
Queued ──(실패, Retryable, tries < 4)──▶ Queued(next_try_at = now + 지연[tries])
Queued ──(실패 4회째 또는 !Retryable 또는 코드 만료)──▶ Dropped
(Failed 상태는 "마지막 시도가 실패, 다시 시도 대기"를 관리 화면에 보이기 위한 표시값)
지연표: 1회 실패 → 1분 뒤, 2회 → 5분 뒤, 3회 → 15분 뒤, 4회째 실패 → 버림   (online 7.3)
```

- 시도 횟수 해석 [임시]: 첫 시도 1회 + 재시도 3회 = 최대 4회.
- 확인 코드(수명 15분)·재설정 코드(30분)가 재시도 시점에 이미 만료되었거나 폐기되었으면 보내지 않고 `Dropped`로 둔다 [임시. 15절 충돌 C-03].
- 받는 주소와 본문은 `mail_outbox`에 넣지 않는다(보낼 때 `account`에서 읽는다). 30일 뒤 삭제. 하루 발송 상한과 실패율 감시에 쓴다. (online 7.9)
- 코드 원문은 DB에 해시만 있으므로 재시도 때 코드를 다시 만들 수 없다 → 재시도용 원문 코드는 **wizard-api 프로세스 메모리에만** 만료 시각까지 두고, 프로세스가 재시작되면 그 메일은 `Dropped`(사용자는 다시 요청) [임시. 15절 C-04].
- 발송 실패율이 오르면 운영자에게 알린다(본 명세 9.4). 임계값 [임시]: 최근 1시간 발송 10통 이상이면서 `Dropped` 비율 20% 이상.
- 서버 전체 하루 발송 상한: **500통**(`config: mail.daily_cap = 500`). 넘으면 보내지 않고 화면의 답은 같다. (online 7.3)

### 2.9 이메일 확인 (online 7.3) [제안]

| 항목 | 값 |
|---|---|
| 받는 때 | 가입 화면에서 아이디, 비밀번호, 닉네임과 함께. 형식만 검사 |
| 확인 코드 | 가입 직후 그 주소로 **6자리** 숫자. 게임 안의 확인 칸에 넣는다(웹 페이지 없음) |
| 수명 | **15분** |
| 틀림 | **5번 틀리면 폐기** |
| 다시 보내기 | **60초에 1회, 하루 5회** |
| 확인하지 않으면 | **로그인과 플레이는 전부 된다**. 확인되지 않은 주소로는 재설정 코드를 보내지 않으므로 비밀번호를 잃으면 되찾을 수 없다. 메인 메뉴와 설정 > 계정에 "이메일 미확인: 비밀번호를 잃으면 되찾을 수 없다"와 [확인 코드 보내기]를 계속 띄운다 |
| 저장 | 확인 여부 = `email_verified_at` 한 칸 |

- 6자리 코드: `000000`~`999999` 균등(CSPRNG), 앞자리 0 허용 [임시]. 저장은 해시만 [제안] → 6자리는 공간이 작아 단순 SHA-256은 오프라인 대입에 약하므로 `HMAC-SHA256(code_key, account_id || ':' || code)` [임시]로 둔다.
- 새 확인 코드를 보내면 같은 계정의 미사용 확인 코드는 폐기한다 [임시](재설정 코드 규칙과 맞춤).
- 틱 환산(참고): 15분 = 54,000틱, 60초 = 3,600틱.

```
VerifyEmailCode(accountId, code):
    t = SELECT * FROM account_token WHERE account_id=? AND kind='verify_email'
        AND used_at IS NULL AND expires_at > now() ORDER BY created_at DESC LIMIT 1 FOR UPDATE
    if t == null -> ERR_CODE_INVALID
    if !ConstantTimeEquals(t.token_hash, H(code)):
        t.attempts += 1
        if t.attempts >= 5: t.used_at = now()             // 폐기
        -> ERR_CODE_INVALID(remaining = max(0, 5 - t.attempts))
    t.used_at = now(); account.email_verified_at = now()
```

### 2.10 이메일 주소 바꾸기 (online 7.3) [제안]

```
1. 설정 > 계정 → 현재 비밀번호 확인 (Argon2id, 2.2)
2. 새 주소 형식 검사 + "주소 하나에 계정 3개" 검사(새 주소의 HMAC 기준)
3. account_token(kind='change_email', new_email_enc = Enc(새 주소)) 생성, 새 주소로 6자리 확인 코드
   (수명·틀림·다시 보내기 규칙은 2.9와 같다 [임시])
4. 코드가 맞으면 한 트랜잭션에서:
     old = Dec(account.email_enc); oldVerified = account.email_verified_at != null
     account.email_enc = token.new_email_enc; account.email_hmac = Hmac(Normalize(새 주소))
     account.email_verified_at = now()
     account.trade_hold_until = max(…, now() + 24h)
     if oldVerified: 옛 주소로 NotifyEmailChanged 한 통 (아래 주의)
     옛 주소는 그 자리에서 지운다
```

- 옛 주소 알림과 "옛 주소는 그 자리에서 지운다"가 부딪힌다(`mail_outbox`는 주소를 넣지 않음) → 해결 [임시]: 교체 트랜잭션 **커밋 직전**에 옛 주소 평문을 메모리에서 꺼내 알림 메일을 즉시 발송 시도하고, 재시도용 옛 주소는 프로세스 메모리에만 최대 21분(1+5+15분) 둔다. DB에는 남기지 않는다. 15절 C-02.
- 옛 주소가 미확인이었다면 알림을 보내지 않는다("미확인 주소에는 확인 메일 말고는 보내지 않는다").
- 메일이 꺼져 있으면 [확인 코드 보내기]와 [이메일 바꾸기] 단추가 꺼진다(`ui-assets.md` 4.19). 새 주소 확인 없이는 바꿀 수 없다 [임시].

### 2.11 비밀번호 재설정 (online 7.3) [확정: 이메일 복구 / 제안: 값]

| 항목 | 값 |
|---|---|
| 요청 | 로그인 화면 [비밀번호를 잊었다] → 아이디와 이메일. 서버는 **맞든 틀리든 같은 답을 같은 시간에**: "맞는 계정이 있으면 메일을 보냈다" |
| 보내는 조건 | 아이디가 있고, 넣은 이메일이 그 계정의 **확인된** 이메일과 같을 때만 |
| 재설정 코드 | 무작위 **12자**. 영문 대문자와 숫자, 헷갈리는 글자 **0·O·1·I 제외**(32자 알파벳, 정확히 60비트). **4자씩 끊어** 적는다. 서버는 SHA-256 해시만 |
| 한 번만 | 쓰면 바로 폐기. 새 코드를 요청하면 앞의 코드는 전부 폐기. **5번 틀리면 폐기** |
| 만료 | **30분** |
| 요청 한도 | 계정당 **60초에 1회, 1시간에 3회, 하루 5회**. IP당 **1시간에 10회**. 서버 전체 **하루 500통**(`config`). 한도를 넘어도 화면의 답은 같다 |
| 코드를 넣는 곳 | 게임 안 "비밀번호 다시 정하기" 화면: 아이디, 재설정 코드, 새 비밀번호(규칙은 가입과 같다) |
| 성공하면 | 비밀번호 변경 → **모든 기기의 접근·갱신 토큰 폐기** → "비밀번호가 바뀌었다" 알림 메일 → **24시간 거래·장터 등록 막음** |
| 운영자 | 비밀번호를 대신 정해 주지 않는다. 관리 화면은 "그 계정의 확인된 이메일로 재설정 메일 다시 보내기"뿐. 확인된 이메일이 없는 계정은 운영자도 되찾아 줄 수 없다 |

- 알파벳 [제안]: `ABCDEFGHJKLMNPQRSTUVWXYZ23456789` (26 − 2 + 10 − 2 = 32자). 각 글자 = 5비트, 12자 = 60비트.
- 표시 형식: `XXXX-XXXX-XXXX`. 입력 정규화 [임시]: 공백·하이픈 제거, 소문자 → 대문자. 정규화 뒤 알파벳 밖 글자가 있으면 틀림 1회로 센다.
- 해시: `SHA-256(정규화된 12자 ASCII)` (online 7.3). 60비트라 단순 SHA-256으로 충분하다고 본 원문을 따른다.
- 틀림 횟수는 **코드 단위**(그 계정의 활성 재설정 코드)로 센다. 아이디가 없거나 활성 코드가 없을 때도 같은 오류 문구 [임시].
- 같은 시간 응답 [임시]: 요청 처리 시간을 고정값(예: 400ms) 이상으로 맞추고, 메일 발송은 비동기(`mail_outbox`)로 뺀다.

**요청 처리 의사코드**

```
RequestPasswordReset(loginId, email, ip):
    start = now()
    defer SleepUntil(start + 400ms)                                    // [임시] 같은 시간
    if !features.recovery_mail_enabled: return SAME_RESPONSE            // 화면은 요청 칸 자체를 안 띄움(2.13)
    RecordResetRequest(ip, loginId)                                     // 한도 계산용 기록 [제안]
    if IpResetRequests(ip, 1h) > 10: return SAME_RESPONSE
    acct = FindByLoginId(loginId)
    if acct == null: return SAME_RESPONSE
    if acct.email_verified_at == null: return SAME_RESPONSE
    if Hmac(Normalize(email)) != acct.email_hmac: return SAME_RESPONSE
    if !ConstantTimeEquals(Normalize(Dec(acct.email_enc)), Normalize(email)): return SAME_RESPONSE
    if AccountMailRequests(acct, 60s) >= 1 or (acct, 1h) >= 3 or (acct, 24h) >= 5: return SAME_RESPONSE
    if ServerMailsToday() >= config.mail.daily_cap (500): return SAME_RESPONSE
    BEGIN
      UPDATE account_token SET used_at = now()
        WHERE account_id = acct.id AND kind='reset_password' AND used_at IS NULL    // 앞 코드 전부 폐기
      code = RandomCode12()
      INSERT account_token(kind='reset_password', token_hash=SHA256(code), attempts=0,
                           expires_at=now()+30min, created_ip=ip, ...)
      INSERT mail_outbox(kind='reset_password', account_id=acct.id, status='queued', tries=0)
    COMMIT
    return SAME_RESPONSE   // "맞는 계정이 있으면 메일을 보냈다. 30분 안에 코드를 넣어 달라"
```

- 계정당 한도의 "요청"은 **메일이 실제로 나간 요청**만 셀지, 조건이 맞지 않은 요청도 셀지 원문에 없다 → [임시] 아이디가 존재하는 모든 요청을 그 계정의 요청으로 센다(무차별 요청으로 남의 계정 한도를 소진시키는 것도 결과는 "메일 안 감"으로 같으므로 안전 쪽).

**재설정 실행 의사코드**

```
ConfirmPasswordReset(loginId, code, newPw):
    acct = FindByLoginId(loginId) FOR UPDATE ; if null -> ERR_RESET_INVALID
    t = active reset token of acct FOR UPDATE ; if null or t.expires_at <= now() -> ERR_RESET_INVALID
    if SHA256(Normalize(code)) != t.token_hash:
        t.attempts += 1; if t.attempts >= 5: t.used_at = now()
        -> ERR_RESET_INVALID
    ValidatePassword(newPw, loginId)  // 가입과 같은 규칙. 실패해도 코드는 소모하지 않는다 [임시]
    t.used_at = now()
    acct.pw_hash, acct.pw_params = Argon2id.Hash(newPw)
    UPDATE session SET revoked_at = now() WHERE account_id = acct.id AND revoked_at IS NULL
    acct.trade_hold_until = max(acct.trade_hold_until, now() + 24h)
    INSERT mail_outbox(kind='notify_password_changed', ...)
    INSERT audit_log(actor='system', action='password_reset', target=acct.id)
    -> OK   // 화면: "모든 기기에서 로그아웃되었다. 24시간 동안 거래와 장터 등록이 막힌다"
```

### 2.12 아이디 찾기 (online 7.3) [제안]

- 같은 화면의 [아이디 찾기]: 이메일만 넣는다 → 그 주소로 **확인된** 계정의 아이디 목록을 메일로 보낸다. 화면의 답은 언제나 같다.
- 한도는 **재설정 요청과 같이 센다.** 계정이 여럿일 수 있으므로 [임시]: 그 주소의 HMAC에 묶인 확인된 계정 **모두**의 계정당 카운터에 1을 더하고, 그 중 하나라도 한도를 넘으면 보내지 않는다. IP당 1시간 10회, 서버 하루 500통도 같이 센다.
- `mail_outbox.account_id`에는 목록의 첫 계정(가장 작은 id)을 적는다 [임시].
- 확인된 계정이 0개면 보내지 않는다(미확인 주소에는 확인 메일 말고 보내지 않음).

### 2.13 메일 사업자 미설정·정지 때 (online 7.3) [제안]

| 상황 | 동작 |
|---|---|
| 게임과 로그인 | **그대로 된다**. 메일이 필요한 것은 확인과 복구뿐. 다른 서버 함수는 `IMailSender`를 부르지 않는다 |
| 가입 | 된다. 이메일은 받아서 암호화 저장, "미확인"으로 둔다. 확인 메일은 메일이 켜진 뒤 설정 > 계정 [확인 코드 보내기]로 |
| 복구 | 안 된다. 서버는 **로그인 전에 읽는 기능 값**에 "복구 메일: 꺼짐"을 싣는다. 로그인 화면 [비밀번호를 잊었다]는 요청 칸 대신 "지금은 복구 메일을 보낼 수 없다. 공지를 확인해 달라" |
| 미뤄 보내기 | **하지 않는다**(만료 시간이 있는 코드라서) |
| 일시 실패 | 1분, 5분, 15분 뒤 재시도, 그래도 안 되면 버림. 실패율이 오르면 운영자 알림 |
| 맥미니 꺼짐 | 복구도 할 수 없다. 서버가 돌아온 뒤에 한다 |
| 오프라인 프로필 | 계정이 없으므로 이메일도 복구도 없다 |

**기능 값 API [제안]** — 로그인 전, 토큰 없이 읽는다.

```
GET /v1/features
200 {
  "protocol_version": 1,
  "recovery_mail_enabled": false,     // IMailSender.IsEnabled && config.mail.enabled
  "payments_enabled": false,          // 클라이언트는 이 값으로 화면을 정하지 않는다. 상점은 GetStoreAsync 결과만 본다(10.2)
  "signup_open": true,                // [임시] 점검 때 false
  "maintenance": false
}
```

`recovery_mail_enabled = false`로 판정하는 조건 [임시]: `mail.sender == "null"`, 또는 `mail.enabled == false`, 또는 최근 15분 동안 시도한 메일이 모두 `Retryable` 실패, 또는 오늘 발송이 하루 상한에 닿음.

### 2.14 저장하는 것 (이메일은 개인정보다) (online 7.3) [제안]

| 항목 | 방법 |
|---|---|
| 이메일 주소 | **AES-256-GCM**, 행마다 무작위 nonce(12바이트 [임시]). 저장 형식 [임시]: `key_id(1B) ‖ nonce(12B) ‖ ciphertext ‖ tag(16B)`. 키는 DB 밖 파일, pepper와 같은 방식으로 관리. DB 덤프·백업에는 암호문만 |
| 찾기용 값 | `HMAC-SHA256(email_lookup_key, Trim(email).ToLowerInvariant())`. 키는 암호 키와 **다른** 키. "아이디 찾기"와 "주소 하나에 계정 3개" 검사에 쓴다. 되돌릴 수 없다. 유일하지 않다(주소 하나에 3행까지) |
| 확인 여부 | `email_verified_at` 한 칸 |
| 코드 | 확인 코드와 재설정 코드는 해시만. 만료되거나 쓴 코드는 **24시간 뒤** 지운다 |
| 로그 | 평문 금지. 관리 화면은 가린 꼴(`a***@d***.com`)만. **전체 주소를 보는 동작은 `audit_log`에 남는다** |
| 메일 사업자 | 넘어가는 것은 받는 주소와 본문뿐. 사업자 쪽 발송 기록 보관 기간을 설정 가능한 가장 짧은 값으로 |
| 지우는 때 | 계정 삭제 확정(요청 7일 뒤): 암호문, 찾기용 값, 남은 코드 전부. 원장에는 처음부터 이메일이 없다. 주소를 바꾸면 옛 주소는 그 자리에서 지운다 |

**가린 꼴 규칙 [임시]**: `local[0] + "***@" + domainFirstLabel[0] + "***" + domain[첫 '.'부터 끝까지]`.
예: `alice@daum.net` → `a***@d***.net`, `bob@mail.example.co.kr` → `b***@m***.example.co.kr`.

**AAD [임시]**: GCM의 추가 인증 데이터에 `account_id`(8바이트 빅엔디언)를 넣어 암호문을 다른 행에 옮겨 붙이는 것을 막는다. 주소 변경 대기 중인 `account_token.new_email_enc`도 같은 키·같은 AAD.

### 2.15 인증 API 목록 [제안]

| 메서드·경로 | 인증 | 요약 |
|---|---|---|
| `GET /v1/features` | 없음 | 2.13 |
| `POST /v1/auth/signup` | 없음 | `{login_id, password, nickname, email, device_key}` → 토큰 + 확인 메일 대기열 |
| `POST /v1/auth/login` | 없음 | `{login_id, password, device_key, client_version, core_version_hash}` → `{access_token, refresh_token, access_expires_at, new_device:bool}` |
| `POST /v1/auth/refresh` | 갱신 토큰 | 회전 |
| `POST /v1/auth/logout` | 접근 토큰 | 이 기기 세션 폐기 |
| `POST /v1/auth/password/change` | 접근 토큰 | `{current, new}` → 모든 기기 토큰 폐기, 알림 메일 |
| `POST /v1/auth/email/send-code` | 접근 토큰 | 확인 코드(재)발송 |
| `POST /v1/auth/email/verify` | 접근 토큰 | `{code}` |
| `POST /v1/auth/email/change` | 접근 토큰 | `{current_password, new_email}` |
| `POST /v1/auth/email/change/confirm` | 접근 토큰 | `{code}` |
| `POST /v1/auth/recovery/request` | 없음 | `{login_id, email}` → 언제나 같은 답 |
| `POST /v1/auth/recovery/find-ids` | 없음 | `{email}` → 언제나 같은 답 |
| `POST /v1/auth/recovery/confirm` | 없음 | `{login_id, code, new_password}` |
| `POST /v1/account/delete` | 접근 토큰 | `{current_password}` → 7일 대기 |
| `POST /v1/account/delete/cancel` | 접근 토큰 | [임시] |
| `GET  /healthz` | 없음 | 외부 감시 서비스용 상태 주소(6.3). DB 연결까지 확인 |

모든 변경 요청(토큰이 있는 것)은 헤더 `Idempotency-Key: <request_id>`를 단다(1.3-4) [임시].

---

## 3. TLS, 상태 파일, 집 밖에 내놓는 방법 (online 7.4)

### 3.1 TLS [제안]

- 도메인 하나를 산다. 인증서는 **Caddy**가 자동으로 받고 갱신한다. DNS 확인 방식(DNS-01)을 쓰면 80번 포트를 열지 않아도 된다.
- **TLS 1.3만** 받는다. 클라이언트는 인증서를 정상 검증한다. **검증을 끄는 개발용 스위치는 출시 빌드에 넣지 않는다** (컴파일 조건 `#if WIZARD_DEV_INSECURE_TLS`는 출시 구성에서 정의 금지, CI가 출시 빌드에서 이 기호를 찾으면 실패 [임시]).
- **평문 HTTP는 받지 않는다**(80 포트 리스너 없음).
- Caddy에서 IP당 요청 수를 제한한다 [제안]. 시작값 [임시]: IP당 초당 20요청, 순간 40.

### 3.2 상태 파일 (online 7.4) [제안]

- 맥미니 밖의 정적 호스팅 한 곳(무료 정적 페이지면 충분)에 작은 JSON.
- 내용: API 주소, 결투 UDP 주소와 포트, 연결 경로 종류, 최소 클라이언트 버전, 새 빌드 내려받는 곳, 점검·장애 공지.
- 클라이언트는 **켤 때 이 파일부터** 읽는다. 서명 **Ed25519**, 공개 키는 클라이언트에 넣어 둔다. 정적 호스팅이 털려도 가짜 서버로 로그인 요청을 돌릴 수 없다.
- **맥미니가 꺼져 있어도 읽힌다** → "서버 점검 중, 21시쯤 다시 엽니다" 같은 공지. 주소·포트를 바꿔도 빌드를 다시 내지 않는다.
- 이 파일도 못 읽으면 **빌드에 든 기본 주소**로 시도, 그것도 안 되면 **오프라인 프로필을 권한다**.

**형식 [제안]**

```json
{
  "payload": "<base64url(UTF-8 JSON)>",
  "sig": "<base64url(Ed25519(payload_bytes))>",
  "key_id": 1
}
```

payload:

```json
{
  "schema": 1,
  "issued_at": "2026-11-11T12:00:00Z",
  "api_base": "https://api.example.com:443",
  "duel_udp": { "host": "duel.example.com", "port": 47000 },
  "route_kind": "port_forward",                 // "port_forward" | "tunnel" | "relay"
  "duel_udp_available": true,                    // tunnel이면 false → 자동 매치 불가(online 4.11)
  "min_client_version": "1.0.0",
  "protocol_version": 1,
  "download_url": "https://example.com/download",
  "notices": [
    { "id": "maint-2026-11-11", "kind": "maintenance", "starts_at": "...", "ends_at": "...",
      "text": "서버 점검 중, 21시쯤 다시 엽니다" }
  ]
}
```

- 서명 대상은 `payload` 문자열을 base64url 디코딩한 바이트 그대로 [임시](JSON 정규화 문제를 피한다).
- `key_id`로 키 교체를 대비한다. 클라이언트는 공개 키 2개까지 내장 [임시].
- 되돌림 공격 방지 [임시]: 클라이언트는 마지막으로 받아 들인 `issued_at`을 저장하고 그보다 이른 파일은 버린다(단, 처음 실행은 예외).
- 포트 기본값(UDP 47000 등)은 [임시]. 원문은 "UDP 1개"만 정했다.

**클라이언트 시작 절차 (상태 기계) [제안]**

```
Boot
 └▶ FetchStatusFile (timeout 5s [임시])
      ├ 성공 & 서명 OK ──────────────▶ ApplyStatus
      ├ 서명 실패 ──────────────────▶ 파일 무시 → UseBuiltInDefaults
      └ 네트워크 실패 ───────────────▶ UseBuiltInDefaults
ApplyStatus / UseBuiltInDefaults
 └▶ VersionCheck: client_version < min_client_version
      ├ 낮음 ─▶ ServerProfileBlocked("새 빌드 위치 안내" + download_url) ; 오프라인 프로필은 계속 된다
      └ 통과 ─▶ ProbeApi(GET /healthz, /v1/features)
                 ├ 성공 ─▶ ConnectAllowed (점검 공지가 있으면 함께 표시)
                 ├ 503 점검 ─▶ MaintenanceNotice(공지 문구) + [오프라인 프로필로 놀기] [다시 시도]
                 ├ 접속자 상한 ─▶ AdmissionQueue(내 순서, [오프라인 프로필로 하기])
                 └ 실패 ─▶ Unreachable(상태 파일의 공지 표시) + [오프라인 프로필로 놀기] [다시 시도]
```

- 오프라인 프로필 자동 테스트(online 8.3)에서는 상태 파일 읽기 시도만 예외로 허용되고, **실패해도 조용히 넘어가야 한다**.

### 3.3 노출 방식 세 가지 (online 7.4) [제안, 기본값은 14-6 확인 대상]

| 방식 | 구성 | 장점 | 단점 |
|---|---|---|---|
| **① 포트 포워딩 + 동적 DNS (기본)** | 공유기: TCP 443 → Caddy, UDP 1개 → wizard-duel. 맥미니는 내부 고정 IP. 맥미니의 작은 스크립트가 **1분마다** 공인 IP 확인, 바뀌면 DNS 레코드 수정(**TTL 60초**) | 무료, 지연 최소, UDP 그대로 | 집 공인 IP 노출. 공격 시 집 인터넷도 죽음. 공유 주소(CGNAT) 회선에서는 불가. 443이 막힌 회선이면 다른 포트를 쓰고 상태 파일에 적는다. IP가 바뀌는 순간 접속이 한 번 끊긴다 |
| ② 터널 서비스 | 맥미니가 밖으로 연결, 접속은 터널 사업자 주소로 | 포트를 열지 않음, 집 IP 숨김, 공유 주소 회선 가능 | 대개 HTTPS·웹소켓만. 결투가 웹소켓 경로 → **자동 매치를 열 수 없다**(online 4.11). 지연 증가 |
| ③ 작은 중계 서버 | 싼 가상 서버 1대를 문지기로, 맥미니와 WireGuard 터널. TCP·UDP 모두 넘김 | 집 IP 숨김, UDP 가능, 공유 주소 회선 가능, 공격은 문지기가 받음 | 월 몇 천 원. 지연 몇 ms 증가. 관리할 것 하나 증가. 게임 서버와 DB는 여전히 맥미니 |

- 기본은 ①. 클라이언트는 상태 파일의 주소만 따르므로 ①→③ 이전은 **상태 파일과 DNS만** 고친다.
- 어느 방식이든 공유기에는 위 포트만 연다. **관리 화면, DB 포트, SSH는 밖에 열지 않는다.** 밖에서 관리하려면 ③의 터널이나 개인용 VPN으로만.
- `wizard-duel`의 UDP 포트는 **유효한 경기 토큰이 없는 패킷을 암호 해제 전에 버린다**.
- 가정용 회선 약관이 서버 운영을 막는지는 개발자가 통신사에 확인한다 (online 7.4, 14-6).
- 서버 쪽 지터가 **10ms를 넘는 시간이 하루 1% 이상**이면 노출 방식을 바꾼다 (online 4.12-6) [제안]. 하루 1% = 864초 = 14분 24초.

**DDNS 스크립트 의사코드 [제안]**

```
every 60s:
    ip = QueryPublicIp()        // 실패하면 다음 주기
    if ip != lastIp:
        UpdateDnsRecord(name, ip, ttl=60)
        lastIp = ip; Log("ddns.updated")
```

**UDP 패킷 1차 거름 [제안]**: 패킷 앞머리에 평문 `match_token_id`(8바이트 [임시])와 패킷 번호. `match_token_id`가 진행 중 경기 표에 없으면 **복호화 시도 없이** 버린다. 있으면 그 경기 키로 ChaCha20-Poly1305 복호화·인증, 패킷 번호가 창보다 오래되었거나 이미 받은 번호면 버린다.

---

## 4. 맥미니 한 대의 용량 추정 (online 7.5) [제안]

실측 전 추정. 가정: 애플 실리콘 맥미니(성능 코어 4개 이상, 메모리 16GB 이상), 가정용 광회선 **올림 100Mbps 이상**. 실제 기종·회선으로 **봇 클라이언트 부하 시험**을 해서 이 표를 고친다.

| 부하 | 계산 | 값 |
|---|---|---|
| 결투 한 경기의 CPU | 60틱 × 틱당 0.2ms(두 사람, 탄 300개 이하) | 코어 하나의 약 1.2% (= 12ms/초) |
| 결투 한 사람의 회선 | 초당 60패킷 × 약 85바이트, 올림·내림 각각 | 약 40kbps (60 × 85 × 8 = 40,800bps) |
| 광장 한 사람의 회선 | 초당 10회 × (39명 × 약 10바이트 + 머리 60바이트) | 약 36kbps 내림 (10 × 450 × 8 = 36,000bps). 인스턴스 하나(40명) 1.5Mbps (40 × 36 = 1,440kbps) |
| 판 재검증 | 판 평균 25분 = 9만 틱. 화면 없이 실시간의 100배 | 판당 약 15초 (1,500초 ÷ 100). 접속자 한 명이 한 시간에 약 1판 |
| DB | 강화·거래·정산은 건당 행 몇 개 | 초당 수백 건까지 문제없다 |

| 동시 접속 | 광장 200 / 결투 60 / 판 중 40 (300명) | 광장 350 / 결투 100 / 판 중 50 (500명) |
|---|---|---|
| 올림 회선 | 약 10Mbps | 약 17Mbps |
| CPU | 결투 30경기 0.4코어 + 검증 1.3코어 + API·광장 0.5코어 | 결투 50경기 0.6코어 + 검증 2.1코어 + API·광장 1코어 |
| 판단 | 여유 있다 | 될 것으로 보나 검증 대기열이 저녁에 밀린다(거래 잠금 해제가 늦어질 뿐 플레이는 된다) |

- 검증 코어 계산: 접속자 N명 × 시간당 1판 × 15초 ÷ 3,600초 = N/240 코어. 300명 → 1.25 ≈ 1.3, 500명 → 2.08 ≈ 2.1.
- **목표: 동시 접속 300명까지 편하게, 500명까지는 부하 시험을 통과하면.** 등록 계정 수천 명 규모.
- 먼저 바닥나는 것은 CPU나 회선이 아니라 **한 대뿐이라는 점**. 맥미니, 공유기, 회선, 전기 중 하나만 멈춰도 온라인 전체가 멈춘다(6절).
- 500명을 넘으면: ① 검증 워커를 다른 기계로(DB의 대기열만 읽으면 되므로 가장 쉽다) → ② `wizard-duel`을 다른 기계로 → ③ DB를 뺀다. 프로세스를 처음부터 나눠 둔 이유.
- **접속자 수 상한**: `config: server.max_concurrent = 400`(시작값). 넘으면 **대기열 화면**을 보여 주고 오프라인 프로필을 권한다.

**입장 대기열 [제안 + 임시]**

```
OnWebSocketAuthenticated(account):
    if LiveCount() < config.server.max_concurrent: Admit(account)
    else: Enqueue(account) ; Send(queue.position, n)  // 5초마다 순서 갱신 [임시]
OnDisconnect(): if Queue.NotEmpty and LiveCount() < max: Admit(Queue.Dequeue())
```

- "접속자"는 웹소켓으로 들어와 있는 서버 프로필 계정 수 [임시]. 오프라인에서 판을 하다 결과만 제출하러 오는 HTTPS 요청(판 제출)은 상한에 세지 않고 대기열을 거치지 않는다 [임시].
- 대기열은 프로세스 메모리. 재시작 때 비워진다 [임시].

---

## 5. 백업 (online 7.6) [제안]

| 대상 | 방법 | 잃을 수 있는 양 (RPO) |
|---|---|---|
| DB(가장 중요) | PostgreSQL **WAL 보관**을 켜서 맥미니에 꽂은 **외장 SSD**에 **1분마다** 쌓는다 + **매일 새벽 전체 백업** | 내장 디스크가 죽어도 최대 1분 |
| 집 밖 사본 | 매일 새벽 전체 덤프를 **암호화**해 밖의 저장소 한 곳에(**30일분** 보관). WAL 조각도 **5분마다** 올린다 | 집에 불이 나도 최대 5분 |
| 입력 기록(다시 보기) | 외장 SSD에 저장. **시즌 종료 90일 뒤** 상위권 외에는 지운다 | 잃어도 재화에는 영향 없다 |
| 설정과 비밀값 | pepper, 이메일 암호 키, 찾기용 HMAC 키, 메일 사업자 API 키, 상태 파일 서명 키, 백업 암호 키는 맥미니 밖(개발자의 암호 관리자)에도 한 벌. **DB 백업과 같은 곳에 두지 않는다** | pepper를 잃으면 전원이 비밀번호를 다시 정해야 하는데, 이메일 암호 키까지 잃으면 재설정 메일을 보낼 주소도 읽을 수 없다. **둘을 같이 잃으면 계정을 되찾을 길이 없다** |
| 서버 프로그램 | 저장소. 배포한 버전의 태그를 남긴다 | |

- **분기마다 실제로 복원해 본다.** 다른 기계에 백업을 풀어 서버가 뜨는지, 원장 대조가 맞는지. 해 보지 않은 백업은 없는 것과 같다.
- 완전히 죽었을 때 복구 절차 문서: 새 기계에 .NET과 PostgreSQL 설치 → 가장 최근 백업 복원 → 프로세스 실행 → 공유기 포워딩 대상 변경. **목표 4시간**(RTO).
- 백업 작업이 실패하면 개발자에게 알림(9.4). **외장 SSD의 남은 용량도 감시**.

**설정 값 [제안 + 임시]**

| 항목 | 값 |
|---|---|
| `archive_mode` | `on` |
| `archive_timeout` | `60s` (1분마다 WAL 조각 강제 전환) |
| `archive_command` | 외장 SSD 경로로 복사(+ 체크섬) |
| 전체 백업 | `pg_basebackup` 매일 03:30 KST [임시] (launchd `StartCalendarInterval`) |
| 집 밖 덤프 | 매일 04:30 KST [임시], `pg_dump -Fc` → 암호화(AES-256-GCM 또는 age [임시]) → 업로드, 30일 넘은 것 삭제 |
| 집 밖 WAL | 5분마다 새 WAL 조각을 암호화해 업로드 (launchd `StartInterval = 300`) |
| 외장 SSD 경고 | 남은 용량 20% 미만 경고, 10% 미만 긴급 [임시] |
| 입력 기록 지우기 | 시즌 `ends_at + 90일`에 배치. 남기는 "상위권" = 각 순위표 상위 100위의 판, 후계자 후보 10명의 판, `report`/`sanction`에 걸린 판, 결투는 `duel_match.rated`인 것 중 시즌 리그별 상위 100위 계정의 경기 [임시. 16절] |

**비밀값 파일 [임시]**: `/usr/local/etc/wizard/secrets/` 아래 `pepper.<id>.key`, `email_enc.<id>.key`, `email_hmac.<id>.key`, `code_hmac.key`, `device_hmac.key`, `status_sign.<id>.ed25519`, `backup.<id>.key`, `mail_api.key`. 권한 0400, 소유자는 서버 실행 계정. 키 ID는 암호문·해시 매개변수에 같이 적어 교체를 가능하게 한다.

---

## 6. 맥미니가 꺼져 있을 때 (online 7.7) [제안]

### 6.1 상황별 동작

| 상황 | 게임의 동작 |
|---|---|
| 클라이언트를 켰는데 서버에 닿지 않음 | 상태 파일의 공지를 보여 준다. "오프라인 프로필로 놀기"와 "다시 시도". 오프라인 프로필은 무한 웨이브, 조립, 강화 화면, 연습이 전부 된다 |
| 서버 프로필로 판을 하던 중 | 판은 그대로 계속된다. 끝나면 결과를 기기에 보관, 서버가 돌아오면 제출. 보상은 받고, **순위에는 종료 후 10분 안에 낸 판만** 오른다 |
| 결투 중 | 양쪽이 다 끊기므로 **무효. 점수 변동 없음** |
| 광장·거래 화면 | 타이틀로 나가며 공지. 진행 중이던 거래는 커밋 전이면 없던 일(트랜잭션) |
| 강화 요청 직후 | 서버가 결과를 저장했으면 돌아온 뒤 결과 연출. 저장 전이었으면 재료 그대로. 요청 번호(멱등 키) 덕에 다시 보내도 두 번 강화되지 않는다 |
| 장터 등록 기간 | 서버가 꺼져 있던 시간만큼 **모든 등록의 만료를 늦춘다** |
| 주간 상점 한도, 일일 결투 보상 | 서버 시각 기준. 꺼져 있던 날의 몫은 돌려주지 않는다 |
| 비밀번호 복구 | 할 수 없다. 서버가 돌아온 뒤. 꺼지기 전에 보낸 재설정 코드는 만료(30분)가 서버 시각 기준이라 대개 다시 요청해야 한다 |
| 시즌 마감 | 마지막 24시간 안에 **2시간 넘게** 꺼져 있었으면 종료를 그만큼 늦춘다 (online 6.2와 같은 규칙) |
| 서버가 다시 켜짐 | launchd가 프로세스를 띄운다 → DB 복구 확인 → **원장 대조 한 번** → 접속 허용. 클라이언트는 **5초에서 시작해 2분까지** 늘어나는 간격으로 재시도(간격을 흩는다) |

### 6.2 정지 시간 기록과 보정 알고리즘 [제안 + 임시]

정지 시간을 재려면 "서버가 살아 있었다"는 기록이 필요하다. 원본 표에 없으므로 추가한다.

- `server_heartbeat(at timestamptz)` 한 행을 `wizard-api`가 **10초마다** 갱신 [임시].
- 시작 때 `downtime = now() − last_heartbeat`. `downtime > 30초`면 `server_downtime(id, from=last_heartbeat, until=now(), seconds)` 행을 추가 [임시].

```
OnServerStart():
    CheckDbRecovered()                                   // pg_is_in_recovery() = false, 마이그레이션 버전 일치
    last = SELECT at FROM server_heartbeat
    gap = now() - last
    BEGIN
      if gap > 30s:
          INSERT server_downtime(from=last, until=now())
          // 장터: 만료 연장
          UPDATE market_listing SET expires_at = expires_at + gap
              WHERE status = 'active' AND expires_at > last     // 꺼져 있는 동안 살아 있던 등록
          // 시즌: 마지막 24시간 안 정지 누계가 2시간 초과면 연장
          s = current season (status='active')
          if s != null:
              windowStart = s.ends_at - 24h
              down = SUM(overlap(server_downtime, [windowStart, s.ends_at]))
              if down > 2h and not s.extended_for(downtime ids):
                  s.ends_at += down ; s.settle_until += down     // [임시] 정산 기간도 민다
    COMMIT
    RunLedgerReconcile(quick)                            // 9.5의 대조, 불일치면 접속 허용 보류 + 알림
    OpenForConnections()
```

경계 조건:
- 시즌 연장 기준은 "2시간 **넘게**" → 정확히 2시간 00분 00초는 연장하지 않는다. 2시간 1초부터 그 전체 시간(예: 2시간 1초)을 늦춘다.
- 연장으로 늘어난 시간 안에 다시 꺼지면? → 연장된 `ends_at` 기준 마지막 24시간으로 다시 계산하되, 이미 반영한 정지 구간은 다시 세지 않는다 [임시].
- 장터: 등록의 만료가 정지 중에 지났든 아니든, 정지 시작 때 살아 있던 모든 등록에 `gap`을 더한다. 정지 전에 이미 만료된 등록은 손대지 않는다.
- 원정 14일 정리(online 3.2)와 `account_debt`, 거래 잠금 7일 등 다른 시간 규칙은 원문이 보정하라고 하지 않았으므로 **보정하지 않는다** [임시. 16절].

### 6.3 꺼지는 일을 줄이는 것 (online 7.7) [제안]

- **UPS**에 맥미니와 공유기를 같이 꽂는다. 정전이 **5분을 넘으면** UPS 신호로 DB를 정상 종료한다.
- 맥미니 설정: 잠자기 끔, 정전 뒤 자동 시작 켬, 자동 로그인 뒤 launchd가 서버를 띄움. **FileVault는 끈다**(재시동마다 사람이 암호를 넣어야 하므로). 대신 백업 파일을 암호화한다.
- macOS 업데이트는 자동으로 하지 않는다. 미리 상태 파일에 점검 시간을 올리고 사람이 한다.
- 맥미니 밖의 **무료 감시 서비스가 1분마다** API 상태 주소(`/healthz`)를 두드려 응답이 없으면 개발자 전화로 알린다.
- **계획 점검**: `config`의 점검 스위치(`server.maintenance = true`)로 새 접속을 막고, **진행 중인 결투가 끝나기를 5분 기다린 뒤** 내린다. (5분 = 결투 시뮬레이션 18,000틱)

**계획 점검 상태 기계 [제안 + 임시]**

```
Open ──(maintenance=true)──▶ Draining
   Draining: 새 웹소켓 접속·로그인·새 결투 매치·새 원정 출발 거절(503 + 공지)
             진행 중 결투는 계속, 진행 중 판 제출·정산은 받는다 [임시]
Draining ──(진행 중 결투 0 또는 5분 경과)──▶ ShuttingDown
   ShuttingDown: 남은 결투는 "서버 정지 = 무효, 점수 변동 없음"(6.1) [임시]
                 광장 연결에 공지 보내고 끊음 → 프로세스 정상 종료
```

### 6.4 클라이언트 재접속 간격 [제안 + 임시]

```
base = [5, 10, 20, 40, 80, 120] 초, 그 뒤 120초 반복   // 5초에서 2분까지 두 배씩
delay_k = base[min(k, 5)] × Uniform(0.8, 1.2)           // 흩기 ±20% [임시]
```

---

## 7. 서버가 권위를 갖는 범위 (online 7.8, 8) [제안 / 조율]

| 영역 | 권위 | 클라이언트가 하는 일 |
|---|---|---|
| 재화, 인벤토리, 지팡이 상태 | 서버 | 표시와 요청 |
| 강화 난수와 결과 | 서버 | 연출 |
| 거래, 장터, 우편 | 서버 (트랜잭션) | 화면 |
| 무한 웨이브 전투 | 클라이언트가 실행, 서버가 **모든 판을** 재실행으로 검증 | 시뮬레이션과 입력 기록 |
| 무한 웨이브 드랍 | 서버가 정산 때 뽑는다 | 드랍 기회만 기록 |
| 결투 전투와 승패 | **서버가 시뮬레이션** [조율 DEC 2.6] | 예측과 되감기 |
| 결투 입력의 시각과 순서 | 클라이언트가 적고 서버가 검증·확정 | 누른 시각 기록 |
| 결투 매치·점수·리그·IL | 서버 | 표시 |
| 광장 위치 | 서버 (속도·벽 검사) | 자기 이동 예측 |
| 채팅 필터·제한 | 서버 | 표시 |
| 시각 | 서버 시각 | 클라이언트 시계는 믿지 않는다 |

- 오프라인 프로필에서는 위의 모든 것을 기기가 한다. 그 결과는 서버로 가지 않는다. (online 7.8, 8.2) [조율 DEC 2.6]
- 서버 프로필의 물건을 기기에 내려받아 접속 없이 쓰는 기능은 없다. 예외: 서버 프로필로 **이미 시작한 판**은 끊겨도 끝까지 하고 나중에 제출한다. (online 8.2) [조율]
- `IBackend`에는 "기능 지원 여부"가 아니라 **프로필 종류**(`ProfileKind { Offline, Server }`)가 있다. (online 8.3) [조율]
- 코어 버전 해시가 다르면 순위 제출과 결투 입장을 막는다. (online 3.1) [제안]

**광장 이동 검사 [제안 + 임시]** (권위 범위의 구현 예): 광장 속도 5.0칸/초(online 5.1) → 광장 1틱(0.1초) 최대 0.5칸 = Q32.32 `2,147,483,648`. 서버는 클라이언트가 보낸 목표 위치까지의 거리가 `0.5칸 × (경과 광장 틱) × 1.1`(허용 오차 10% [임시])을 넘거나 벽을 지나면 서버 위치로 되돌린다(`plaza.position_corrected`).

---

## 8. 데이터 모델 (online 7.9, 9.3) [제안]

원본의 표·열은 **전부** 옮겼다. 원본에 없는 열·표는 `-- [임시]` 주석을 단다. 열거형은 PostgreSQL `enum` 대신 `text + CHECK`로 둔다(마이그레이션 쉬움) [임시]. 모든 시각은 `timestamptz`.

### 8.1 공통 열거값 [제안]

| 이름 | 값 (DB 문자열) | 원문 |
|---|---|---|
| `character` | `smallint` 1~5 (제자 1~5. 이름은 이름 표에서, DEC 1.25) | 5종 |
| `league` | `smallint` 0=`sprout`(새싹 ≤70), 1=`beast`(맹수 ≤155), 2=`heir`(후계, 제한 없음) | online 0, 4.8 |
| `token_kind` | `verify_email`, `reset_password`, `change_email` | 이메일 확인/비밀번호 재설정/이메일 변경 |
| `mail_kind` | `verify_email`, `reset_password`, `find_login_ids`, `notify_password_changed`, `notify_email_changed` | [임시] (원문 "kind"만) |
| `mail_status` | `queued`, `sent`, `failed`, `dropped` | 대기/보냄/실패/버림 |
| `wand_origin` | `start`, `shop`, `trade` | 시작/상점/거래 |
| `wand_lock` | `none`, `expedition`, `duel`, `market`, `mail`, `trade` | 없음/원정/결투/장터/우편/거래 |
| `item_origin` | `earned`, `reward`, `paid` | 획득/보상/유료 |
| `currency` | `gold`, `season_coin`, `starlight` | 골드/시즌 주화/별빛 |
| `bucket` | `earned`, `paid`, `free` | 획득/유료/무상 |
| `enhance_kind` | `enhance`, `carve` | 강화/새기기 |
| `run_kind` | `normal`, `weekly` | 일반/주간 도전 |
| `run_status` | `in_progress`, `first_pass`, `verified`, `invalid`, `closed` | 진행/1차 통과/검증됨/무효/정리됨 |
| `run_end_kind` | `hp_zero`, `abandon`, `expedition_close`, `auto_close_14d` | 체력 0/판 포기/원정 정리/14일 정리 |
| `debt_kind` | `gold`, `item` | 골드/물건 |
| `duel_mode` | `challenge`, `room`, `auto` | 도전/방/자동 |
| `sanction_kind` | `chat`, `trade`, `duel`, `login` | 채팅/거래/결투/접속 |
| `audit_actor` | `operator`, `system` | 운영자/시스템 |
| `order_status` | `created`, `pending_auth`, `authorized`, `granted`, `cancelled`, `refunded`, `revoked` | 10.3 |
| `purchase_flow` | `client_receipt`, `server_order`, `redeem_code` | 9.2 |
| `entitlement_kind` | `currency`, `item`, `feature` | 재화/물건/기능 |
| `chat_channel` | `nearby`, `plaza`, `trade`, `whisper`, `friend`, `duel_room`, `system` | online 5.3 |
| `report_reason` | `abuse`, `scam`, `rmt`, `cheat`, `other` | 욕설/사기/현금 거래/부정 프로그램/기타 |

### 8.2 DDL

```sql
-- ===== 계정과 인증 (online 7.3, 7.9) =====
CREATE TABLE account (
  id                  bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  login_id            text NOT NULL UNIQUE CHECK (login_id ~ '^[a-z0-9]{4,16}$'),
  pw_hash             bytea NOT NULL,
  pw_params           jsonb NOT NULL,                 -- 2.2
  email_enc           bytea NOT NULL,                 -- AES-256-GCM (2.14)
  email_hmac          bytea NOT NULL,                 -- 유일 아님. 주소 하나에 3행까지
  email_verified_at   timestamptz NULL,
  nickname            text NOT NULL,
  created_at          timestamptz NOT NULL DEFAULT now(),
  last_login          timestamptz NULL,
  flags               bigint NOT NULL DEFAULT 0,      -- 비트: 1=검토 표시, 2=검증 실패 이력, 4=결제 막음(9.5) … [임시]
  sanction_until      timestamptz NULL,               -- sanction 표의 캐시 [임시 해석]
  play_seconds        bigint NOT NULL DEFAULT 0 CHECK (play_seconds >= 0),
  league              smallint NOT NULL DEFAULT 0 CHECK (league BETWEEN 0 AND 2),  -- 시즌 안에서 올라가기만(4.8)
  league_season_id    bigint NULL,
  show_name_in_alerts boolean NOT NULL DEFAULT true,  -- 기본값 [임시]
  phantom_met         boolean NOT NULL DEFAULT false, -- false = "허상을 만난 적 없음" 켜짐
  revenge_done_at     timestamptz NULL,
  phantom_tries       integer NOT NULL DEFAULT 0,
  phantom_best_dent   integer NOT NULL DEFAULT 0,     -- 단위는 monsters.md 3.9 [임시 타입]
  revenge_runs        integer NOT NULL DEFAULT 0,
  -- [임시] 원문에 없는 열
  trade_hold_until    timestamptz NULL,               -- 새 기기/이메일 변경/재설정 24시간 (2.6)
  deletion_requested_at timestamptz NULL,             -- 2.7
  max_wave_reached    integer NOT NULL DEFAULT 0      -- 거래·자동 매치 조건(웨이브 10) 판정용 캐시
);
CREATE UNIQUE INDEX account_nickname_ci ON account (lower(nickname));   -- [임시]
CREATE INDEX account_email_hmac ON account (email_hmac);
-- 주소 하나에 3계정: 가입·변경 트랜잭션에서 SELECT count(*) … FOR UPDATE 로 검사(DB 제약 아님)

CREATE TABLE account_token (
  id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id    bigint NOT NULL REFERENCES account(id) ON DELETE CASCADE,
  kind          text NOT NULL CHECK (kind IN ('verify_email','reset_password','change_email')),
  token_hash    bytea NOT NULL,
  new_email_enc bytea NULL,                     -- change_email 전용
  attempts      smallint NOT NULL DEFAULT 0 CHECK (attempts BETWEEN 0 AND 5),
  expires_at    timestamptz NOT NULL,
  used_at       timestamptz NULL,
  created_ip    inet NULL,                      -- IP는 30일 뒤 지운다(online 10) [임시: NULL로 덮음]
  created_at    timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX account_token_active ON account_token (account_id, kind) WHERE used_at IS NULL;
-- 정리: used_at 또는 expires_at 이 24시간 지난 행 삭제

CREATE TABLE mail_outbox (
  id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  kind         text NOT NULL,                   -- mail_kind
  account_id   bigint NULL,                     -- 주소·본문은 넣지 않는다
  status       text NOT NULL CHECK (status IN ('queued','sent','failed','dropped')),
  tries        smallint NOT NULL DEFAULT 0,
  provider_ref text NULL,
  created_at   timestamptz NOT NULL DEFAULT now(),
  sent_at      timestamptz NULL,
  next_try_at  timestamptz NULL,                -- [임시]
  token_id     bigint NULL                      -- [임시] 어떤 코드의 메일인지(만료 시 버림 판단)
);
-- 30일 뒤 삭제. 하루 발송 수 = count(*) WHERE status='sent' AND sent_at >= 오늘 0시(KST [임시])

CREATE TABLE session (
  id                 bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id         bigint NOT NULL REFERENCES account(id) ON DELETE CASCADE,
  device_id          bigint NOT NULL,
  access_hash        bytea NOT NULL UNIQUE,
  refresh_hash       bytea NOT NULL UNIQUE,
  family_id          uuid NOT NULL,
  expires_at         timestamptz NOT NULL,      -- 원문 열 = 갱신 토큰 만료로 쓴다 [임시]
  access_expires_at  timestamptz NOT NULL,      -- [임시] 원문은 expires_at 하나(15절 C-06)
  revoked_at         timestamptz NULL
);
CREATE TABLE session_refresh_used (            -- [임시] 재사용 탐지
  refresh_hash bytea PRIMARY KEY, family_id uuid NOT NULL,
  account_id bigint NOT NULL, device_id bigint NOT NULL, at timestamptz NOT NULL
);  -- 30일 + 여유 뒤 삭제

CREATE TABLE device (
  id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id  bigint NOT NULL REFERENCES account(id) ON DELETE CASCADE,
  device_hash bytea NOT NULL,
  first_seen  timestamptz NOT NULL,
  last_seen   timestamptz NOT NULL,
  UNIQUE (account_id, device_hash)
);
CREATE INDEX device_by_hash ON device (device_hash);   -- 기기당 계정 수, 기기당 자동 매치 3계정

CREATE TABLE login_attempt (
  login_id text NOT NULL, ip inet NOT NULL, at timestamptz NOT NULL, ok boolean NOT NULL
);
CREATE INDEX login_attempt_by_id ON login_attempt (login_id, at DESC);
CREATE INDEX login_attempt_by_ip ON login_attempt (ip, at DESC);
-- 30일 뒤 삭제

CREATE TABLE signup_log (                      -- [임시] IP·기기당 하루 3계정 검사용
  ip inet NOT NULL, device_hash bytea NOT NULL, at timestamptz NOT NULL
);  -- 30일 뒤 삭제

CREATE TABLE recovery_request_log (            -- [임시] 재설정·아이디 찾기 한도(계정·IP) 검사용
  account_id bigint NULL, ip inet NOT NULL, kind text NOT NULL, at timestamptz NOT NULL
);  -- 30일 뒤 삭제

-- ===== 멱등 (online 5.6, 7.9) =====
CREATE TABLE request (
  account_id bigint NOT NULL,
  request_id uuid NOT NULL,
  kind       text NOT NULL,
  result     jsonb NOT NULL,
  at         timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (account_id, request_id)
);  -- 30일 보관

-- ===== 캐릭터·지팡이·물건 =====
CREATE TABLE character_state (
  account_id       bigint NOT NULL REFERENCES account(id) ON DELETE CASCADE,
  character        smallint NOT NULL CHECK (character BETWEEN 1 AND 5),
  equipped_wand_id bigint NOT NULL,             -- 든 지팡이
  unlocked_arts    jsonb NOT NULL,              -- 비기 해금(캐릭터별, DEC 2.8). 시작 3종
  best_score       bigint NOT NULL DEFAULT 0,
  best_wave        integer NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id, character)
);  -- 계정당 5행

CREATE TABLE wand (
  id                 bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  owner_id           bigint NOT NULL,
  character          smallint NOT NULL CHECK (character BETWEEN 1 AND 5),
  stage              smallint NOT NULL CHECK (stage BETWEEN 0 AND 13),
  floor_peak         smallint NOT NULL CHECK (floor_peak BETWEEN 0 AND 13),   -- 바닥 계산용
  record_peak        smallint NOT NULL CHECK (record_peak BETWEEN 0 AND 13),  -- 흔적용
  drop_count         integer NOT NULL DEFAULT 0,
  aura               integer NOT NULL DEFAULT 0,       -- 장인의 기운(단위는 강화 명세)
  slots_extra        smallint NOT NULL DEFAULT 0 CHECK (slots_extra BETWEEN 0 AND 3),
  carve_bonus        integer NOT NULL DEFAULT 0,       -- 슬롯 새기기 누적 확률(만분율 [임시])
  mutations          jsonb NOT NULL DEFAULT '[]',
  loadout            jsonb NOT NULL,                   -- 출발 조립
  skin_id            text NULL,
  show_scars         boolean NOT NULL DEFAULT false,   -- 흔적 공개, 기본 끔 [확정 DEC 1.15]
  origin             text NOT NULL CHECK (origin IN ('start','shop','trade')),
  owner_count        integer NOT NULL DEFAULT 1,
  trade_locked_until timestamptz NULL,
  lock_state         text NOT NULL DEFAULT 'none'
                     CHECK (lock_state IN ('none','expedition','duel','market','mail','trade')),
  pending_run_id     bigint NULL,
  version            integer NOT NULL DEFAULT 0       -- 낙관적 동시성 + 원장 참조
);
CREATE INDEX wand_owner_char_stage ON wand (owner_id, character, stage);
-- (owner_id, character)당 행 수 상한 없음, 1행 이상(DEC 1.22). "마지막 한 자루" 검사는
-- 계정 행을 FOR UPDATE 로 잠근 트랜잭션 안에서 count(*) 로 센다.

CREATE TABLE item_stack (
  account_id   bigint NOT NULL,
  item_code    text NOT NULL,
  qty          bigint NOT NULL DEFAULT 0 CHECK (qty >= 0),
  qty_pending  bigint NOT NULL DEFAULT 0 CHECK (qty_pending >= 0),
  qty_socketed bigint NOT NULL DEFAULT 0 CHECK (qty_socketed >= 0),
  qty_bound    bigint NOT NULL DEFAULT 0 CHECK (qty_bound >= 0),
  PRIMARY KEY (account_id, item_code)
);
-- qty_bound: 계정에 묶인 수량. 거래·장터·선물 불가, 쓸 때 먼저 빠진다(online 5.5)

CREATE TABLE item_instance (
  id        bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  owner_id  bigint NOT NULL,
  item_code text NOT NULL,
  origin    text NOT NULL CHECK (origin IN ('earned','reward','paid')),
  bound     boolean NOT NULL DEFAULT false,
  season_id bigint NULL,
  version   integer NOT NULL DEFAULT 0
);

-- ===== 재화와 원장 (online 5.6, 7.9, 9.1) =====
CREATE TABLE currency_balance (
  account_id     bigint NOT NULL,
  currency       text NOT NULL CHECK (currency IN ('gold','season_coin','starlight')),
  bucket         text NOT NULL CHECK (bucket IN ('earned','paid','free')),
  amount         bigint NOT NULL,
  amount_pending bigint NOT NULL DEFAULT 0 CHECK (amount_pending >= 0),
  PRIMARY KEY (account_id, currency, bucket),
  CHECK (amount >= 0 OR bucket = 'paid')       -- 유료 구분만 환불 때 음수 허용(9.5)
);

CREATE TABLE currency_ledger (                  -- 재화 원장은 이것 하나. 추가만
  id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id    bigint NOT NULL,                -- FK 없음: 계정 삭제 뒤 익명 번호로 남는다
  currency      text NOT NULL,
  bucket        text NOT NULL,
  delta         bigint NOT NULL,
  balance_after bigint NOT NULL,
  pending       boolean NOT NULL DEFAULT false,
  reason        text NOT NULL,
  ref_type      text NULL,
  ref_id        text NULL,
  request_id    uuid NULL,
  at            timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE item_ledger (                      -- 물건 원장은 이것 하나. 추가만
  id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id bigint NOT NULL,
  item_ref   text NOT NULL,                     -- 'stack:<item_code>' | 'wand:<id>' | 'inst:<id>' [임시]
  delta      bigint NOT NULL,
  pending    boolean NOT NULL DEFAULT false,
  reason     text NOT NULL,
  ref_type   text NULL,
  ref_id     text NULL,
  request_id uuid NULL,
  at         timestamptz NOT NULL DEFAULT now()
);
-- 추가만: 서버 DB 역할에 UPDATE/DELETE 권한을 주지 않는다(REVOKE) [임시]

CREATE TABLE enhance_log (
  id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  wand_id        bigint NOT NULL,
  account_id     bigint NOT NULL,
  kind           text NOT NULL CHECK (kind IN ('enhance','carve')),
  stage_before   smallint NOT NULL,
  stage_after    smallint NOT NULL,
  prob_breakdown jsonb NOT NULL,
  roll           bigint NOT NULL,               -- 뽑힌 값(정수 표현 [임시])
  outcome        text NOT NULL,
  sacrifices     jsonb NOT NULL DEFAULT '[]',
  charm_used     boolean NOT NULL DEFAULT false,
  at             timestamptz NOT NULL DEFAULT now()
);  -- 계속 보관(online 7.10)

-- ===== 무한 웨이브 (online 3.2, 7.9) =====
CREATE TABLE run (
  id             bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,   -- 정산 멱등 키
  account_id     bigint NOT NULL,
  character      smallint NOT NULL,
  wand_id        bigint NOT NULL,
  wand_snapshot  jsonb NOT NULL,
  seed           bigint NOT NULL,
  kind           text NOT NULL CHECK (kind IN ('normal','weekly')),
  start_wave     integer NOT NULL DEFAULT 1,
  phantom_first  boolean NOT NULL,              -- 출발 때 서버가 정해 적는다
  core_version   text NOT NULL,
  started_at     timestamptz NOT NULL,
  ended_at       timestamptz NULL,
  submitted_at   timestamptz NULL,
  wave           integer NULL,
  score          bigint NULL,
  gold           bigint NULL,
  drop_chances   jsonb NULL,
  loot           jsonb NULL,
  status         text NOT NULL CHECK (status IN ('in_progress','first_pass','verified','invalid','closed')),
  end_kind       text NULL CHECK (end_kind IN ('hp_zero','abandon','expedition_close','auto_close_14d')),
  ranked         boolean NOT NULL DEFAULT false,
  replay_key     text NULL
);
CREATE INDEX run_open_by_account ON run (account_id) WHERE status = 'in_progress';

CREATE TABLE run_checkpoint (
  run_id     bigint NOT NULL REFERENCES run(id),
  wave       integer NOT NULL,
  tick       bigint NOT NULL,
  state_hash bytea NOT NULL,
  score      bigint NOT NULL,
  wallet     bigint NOT NULL,
  replay_key text NOT NULL,
  at         timestamptz NOT NULL,
  PRIMARY KEY (run_id, wave)
);

CREATE TABLE account_debt (
  id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id bigint NOT NULL,
  kind       text NOT NULL CHECK (kind IN ('gold','item')),
  item_code  text NULL,
  qty        bigint NOT NULL CHECK (qty > 0),
  run_id     bigint NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  cleared_at timestamptz NULL
);

CREATE TABLE verify_queue (
  id       bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  run_id   bigint NOT NULL UNIQUE,
  priority smallint NOT NULL,                  -- 0 순위표 상위 → 1 개인 최고 → 2 표시 계정 → 3 나머지
  status   text NOT NULL,                      -- 'waiting','taken','done' [임시]
  result   jsonb NULL,
  worker   text NULL,
  at       timestamptz NOT NULL DEFAULT now()
);
-- 워커 꺼내기: SELECT … ORDER BY priority, at FOR UPDATE SKIP LOCKED LIMIT 1 [임시]

-- ===== 결투 (online 4.x, 7.9) =====
CREATE TABLE duel_match (
  id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  mode        text NOT NULL CHECK (mode IN ('challenge','room','auto')),
  league      smallint NULL,
  p1          bigint NOT NULL,
  p2          bigint NOT NULL,
  snapshots   jsonb NOT NULL,
  options     jsonb NOT NULL,
  result      text NULL,                       -- 'p1','p2','void','forfeit_p1','forfeit_p2' [임시]
  rounds      jsonb NULL,
  input_delay jsonb NOT NULL,                  -- 사람별 D(2~4틱)
  net_stats   jsonb NULL,
  replay_key  text NULL,                       -- 확정 입력 기록은 파일로
  rated       boolean NOT NULL,
  at          timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE duel_rating (
  account_id bigint NOT NULL, league smallint NOT NULL, season_id bigint NOT NULL,
  rating double precision NOT NULL, deviation double precision NOT NULL,
  volatility double precision NOT NULL, games integer NOT NULL DEFAULT 0,
  PRIMARY KEY (account_id, league, season_id)
);
CREATE TABLE il_history (
  account_id bigint NOT NULL, day date NOT NULL, holding_il integer NOT NULL,
  PRIMARY KEY (account_id, day)
);  -- 14일 최고값 계산

-- ===== 거래 (online 5.5, 5.6, 7.9) =====
CREATE TABLE trade (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  a_id bigint NOT NULL, b_id bigint NOT NULL,
  a_gives jsonb NOT NULL, b_gives jsonb NOT NULL,
  fee bigint NOT NULL, appraised_a bigint NOT NULL, appraised_b bigint NOT NULL,
  at timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE market_listing (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  seller_id bigint NOT NULL, item_ref text NOT NULL,
  qty bigint NOT NULL CHECK (qty >= 0), unit_price bigint NOT NULL CHECK (unit_price > 0),
  deposit bigint NOT NULL, expires_at timestamptz NOT NULL,
  status text NOT NULL                          -- 'active','sold','expired','cancelled' [임시]
);  -- 구매 때 이 행을 잠근다
CREATE TABLE market_sale (
  listing_id bigint NOT NULL, buyer_id bigint NOT NULL, qty bigint NOT NULL,
  price bigint NOT NULL, fee bigint NOT NULL, at timestamptz NOT NULL DEFAULT now()
);  -- 시세 계산
CREATE TABLE mail (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id bigint NOT NULL, payload jsonb NOT NULL, reason text NOT NULL,
  expires_at timestamptz NULL, claimed_at timestamptz NULL
);  -- 대금·보상·반환. 수령: UPDATE … WHERE id=? AND claimed_at IS NULL 이 1행일 때만 지급

-- ===== 사회 =====
CREATE TABLE friend ( account_id bigint NOT NULL, other_id bigint NOT NULL, state text NOT NULL,
                      at timestamptz NOT NULL, PRIMARY KEY (account_id, other_id) );
CREATE TABLE block  ( account_id bigint NOT NULL, other_id bigint NOT NULL, state text NOT NULL,
                      at timestamptz NOT NULL, PRIMARY KEY (account_id, other_id) );  -- 최대 200명(online 5.3)
CREATE TABLE plaza_instance ( id bigint PRIMARY KEY, started_at timestamptz NOT NULL,
                              population integer NOT NULL );   -- 메모리 상태의 그림자

-- ===== 시즌·순위 =====
CREATE TABLE season (
  id bigint PRIMARY KEY, starts_at timestamptz NOT NULL, ends_at timestamptz NOT NULL,
  settle_until timestamptz NOT NULL, status text NOT NULL
);
CREATE TABLE season_result (
  season_id bigint NOT NULL, account_id bigint NOT NULL, league smallint NOT NULL,
  s double precision, r double precision, q_e double precision, q_p double precision,
  e double precision, p double precision, total double precision, rank integer,
  rewards_granted boolean NOT NULL DEFAULT false,
  PRIMARY KEY (season_id, account_id)
);
CREATE TABLE leaderboard_entry (
  board text NOT NULL, season_id bigint NULL, account_id bigint NOT NULL,
  character smallint NULL, score bigint NOT NULL, wave integer NOT NULL,
  run_id bigint NOT NULL, at timestamptz NOT NULL
);  -- 자체 순위표

-- ===== 운영 =====
CREATE TABLE report (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, reporter_id bigint NOT NULL,
  target_id bigint NOT NULL, reason text NOT NULL, chat_excerpt jsonb NULL, ref text NULL,
  status text NOT NULL, handled_by text NULL, at timestamptz NOT NULL DEFAULT now()
);  -- 붙은 발췌는 처리 뒤 1년(online 10)
CREATE TABLE sanction (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, account_id bigint NOT NULL,
  kind text NOT NULL CHECK (kind IN ('chat','trade','duel','login')),
  reason text NOT NULL, "from" timestamptz NOT NULL, until timestamptz NULL,  -- NULL = 영구
  by text NOT NULL
);
CREATE TABLE chat_log (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, channel text NOT NULL,
  instance_id bigint NULL, account_id bigint NOT NULL, text text NOT NULL,
  filtered boolean NOT NULL, at timestamptz NOT NULL DEFAULT now()
);  -- 90일 보관 뒤 삭제
CREATE TABLE audit_log (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  actor text NOT NULL,                        -- 'operator:<name>' | 'system' [임시]
  action text NOT NULL, target text NOT NULL,
  before jsonb NULL, after jsonb NULL, at timestamptz NOT NULL DEFAULT now()
);  -- 운영자 행동 전부. 혼자 운영해도 남긴다(online 10)

CREATE TABLE config ( key text PRIMARY KEY, value jsonb NOT NULL, updated_at timestamptz NOT NULL );

-- [임시] 6.2 정지 시간
CREATE TABLE server_heartbeat ( id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1), at timestamptz NOT NULL );
CREATE TABLE server_downtime  ( id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                "from" timestamptz NOT NULL, until timestamptz NOT NULL,
                                applied_season_id bigint NULL );

-- ===== 결제 (online 9.3) — 출시 때는 비어 있다 =====
CREATE TABLE product (
  id text PRIMARY KEY, kind text NOT NULL, contents jsonb NOT NULL, price_tier text NOT NULL,
  flows text[] NOT NULL,                      -- 허용 흐름
  enabled boolean NOT NULL DEFAULT false, starts_at timestamptz NULL, ends_at timestamptz NULL
);  -- 상품 목록의 유일한 진실
CREATE TABLE purchase_order (
  id uuid PRIMARY KEY,                        -- 멱등 키
  account_id bigint NOT NULL, product_id text NOT NULL REFERENCES product(id),
  provider text NOT NULL, flow text NOT NULL CHECK (flow IN ('client_receipt','server_order','redeem_code')),
  status text NOT NULL CHECK (status IN ('created','pending_auth','authorized','granted','cancelled','refunded','revoked')),
  provider_ref text NULL UNIQUE,              -- 같은 결제 두 번 지급 방지
  price bigint NOT NULL, currency_code text NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(), authorized_at timestamptz NULL,
  granted_at timestamptz NULL, expires_at timestamptz NOT NULL      -- created_at + 30분
);
CREATE TABLE payment_proof (
  order_id uuid NOT NULL REFERENCES purchase_order(id), raw text NOT NULL,
  verified_at timestamptz NULL, verify_result jsonb NULL
);
CREATE TABLE entitlement (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, account_id bigint NOT NULL,
  kind text NOT NULL CHECK (kind IN ('currency','item','feature')), ref text NOT NULL,
  source text NOT NULL,                       -- 'order:<uuid>' | 'reward:<…>'
  granted_at timestamptz NOT NULL DEFAULT now(), revoked_at timestamptz NULL
);
CREATE TABLE refund_event (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, order_id uuid NOT NULL,
  provider_ref text NOT NULL UNIQUE,          -- 같은 알림 두 번 방지 [임시]
  at timestamptz NOT NULL, handled_at timestamptz NULL
);
CREATE TABLE redeem_code (
  code_hash bytea PRIMARY KEY, product_id text NOT NULL REFERENCES product(id),
  used_by bigint NULL, used_at timestamptz NULL, expires_at timestamptz NULL
);
```

### 8.3 `config` 키 [제안 + 임시]

| 키 | 시작값 | 출처 |
|---|---|---|
| `server.max_concurrent` | `400` | online 7.5 |
| `server.maintenance` | `false` | online 7.7 (점검 스위치) |
| `mail.sender` | `"null"` → 사업자 연결 뒤 `"api"` | online 7.3 |
| `mail.enabled` | `true` | [임시] |
| `mail.daily_cap` | `500` | online 7.3 |
| `payments_enabled` | `false` | online 9.2 |
| `chat.wordlist_version` | 정수 | online 7.9 (금칙어 버전) |
| `features.*` | 기능 스위치 | online 7.9 |
| `data.version.<표 이름>` | 버전이 붙은 데이터 파일(확률표, 비용 계수, 드랍 표, 리그 경계, 수수료) | online 7.9 |
| `auth.argon2` | `{"m_kib":65536,"t":3,"p":1}` | online 7.3 |
| `auth.hash_concurrency` | `4` | online 7.3 |

### 8.4 보관 기간 한눈에 [제안]

| 대상 | 기간 | 출처 |
|---|---|---|
| `account_token`(쓴·만료된 것) | 24시간 뒤 삭제 | online 7.3, 7.9 |
| `mail_outbox` | 30일 | online 7.9 |
| `login_attempt` | 30일 | online 7.9 |
| `request` | 30일 | online 5.6, 7.9 |
| 접속 IP | 30일 | online 10 |
| 구조화 로그 | 30일 | online 7.10 |
| `chat_log` | 90일 | online 7.9, 10 |
| 신고에 붙은 발췌 | 처리 뒤 1년 | online 10 |
| 입력 기록(다시 보기) | 시즌 종료 90일 뒤 상위권 외 삭제 | online 7.6 |
| 집 밖 전체 덤프 | 30일분 | online 7.6 |
| 원장, `enhance_log` | 계속 | online 7.10 |
| 계정(삭제 요청) | 7일 뒤 삭제 | online 7.3 |

---

## 9. 운영 (online 7.10) [제안]

### 9.1 관리 화면 (`wizard-admin`, 맥미니 안에서만, 밖에서는 SSH 터널)

- 계정 검색(아이디·닉네임), 인벤토리·원장 조회.
- 제재: 채팅/거래/결투/접속, 기간과 사유.
- 신고 대기열(붙은 채팅 포함).
- 판·결투 다시 보기.
- 아이템·골드 회수와 지급(**전부 `audit_log`**).
- 닉네임 강제 변경.
- 확인된 이메일로 재설정 메일 다시 보내기(운영자가 비밀번호를 대신 정하지 않는다. 이메일은 가린 꼴로만. 전체 주소 보기 = `audit_log`).
- 공지.

### 9.2 경제 지표

하루 골드 생성·소멸, 품목별 생성·소멸·거래량·중앙값, 단계별 지팡이 분포, 강화 시도와 **실제 성공률(표의 확률과 비교)**, 단계별 실제 도달 웨이브(online 2.3 가정 검증).

### 9.3 이상 탐지

원장 대조 불일치, 검증 실패, 감정가 대비 이상 거래, 짧은 시간 대량 획득, 승패 쏠림 쌍, 신고 누적, 결투 봉인 마감에 반복해서 걸리는 계정.

### 9.4 로그·감시·알림

| 항목 | 내용 |
|---|---|
| 로그 | 서버 함수 호출(누가, 무엇을, 결과), 접속 기록, 오류. **구조화 로그 30일**. 원장과 강화 기록은 계속 보관. 비밀번호와 토큰은 로그에 쓰지 않는다 |
| 감시 지표 | 접속자 수, 함수 지연, DB 연결, 검증 대기열 길이, 오류율, 디스크 남은 용량, 백업 성공 여부, 서버 쪽 지터, 메일 발송 실패율과 하루 발송 수 |
| 죽음 알림 | 밖의 감시 서비스(1분마다 `/healthz`) |

알림 임계값 [임시]:

| 지표 | 경고 | 긴급 |
|---|---|---|
| 검증 대기열 가장 오래된 항목 | 30분 | 3시간 |
| API p95 지연 | 300ms | 1초 |
| 5xx 비율(5분) | 1% | 5% |
| 외장 SSD 남은 용량 | 20% | 10% |
| 백업 작업 | 1회 실패 | 2회 연속 실패 |
| 서버 쪽 지터 > 10ms 누적 | 하루 0.5% | 하루 1% (노출 방식 변경 검토, online 4.12) |
| 메일 `dropped` 비율(1시간, 10통 이상) | 20% | 50% |
| 하루 발송 수 | 400 | 500(상한) |

### 9.5 야간 대조 (online 5.6 — 7.7 재시작 때도 한 번) [제안]

```
1. 재화: 각 (account_id, currency, bucket)에 대해 SUM(currency_ledger.delta) = currency_balance.amount
         (pending 행은 amount_pending과 따로 대조 [임시])
2. 겹쳐지는 물건: 품목별 생성 − 소멸 = 현재 수량(item_stack.qty + qty_pending + … 합)
3. 지팡이: 생성 − 소멸 = 현재 개체 수
4. 잠금: lock_state='expedition'인데 status='in_progress'인 run 이 없는 지팡이,
         lock_state='market'인데 active 등록이 없는 물건
불일치 → 운영자 알림(admin.reconcile_mismatch). 재시작 때의 대조는 불일치면 접속 허용을 보류한다 [임시]
```

### 9.6 사고 대응

복제가 발견되면 원장으로 해당 물건의 이동 경로를 따라가 회수한다. **전체 되돌리기는 마지막 수단.**

### 9.7 배포

- 서버와 클라이언트는 **프로토콜 버전**을 맞춘다. 상태 파일의 최소 버전보다 낮은 클라이언트는 서버 프로필에 못 들어가고 새 빌드 위치를 안내받는다(오프라인 프로필은 계속 된다).
- 코어 버전이 바뀌면 **진행 중인 판은 예전 버전 워커로 검증**한다(워커는 **최근 2개 버전**을 같이 둔다).
  - 구현 [임시]: `wizard-verify`는 `Wizard.Sim` 두 버전을 별도 `AssemblyLoadContext`로 싣고 `run.core_version`으로 고른다. 3개 이상 이전 버전의 판은 `invalid`가 아니라 운영자 검토(`verify.unsupported_core`)로 둔다 [임시].
- 빌드 배포: 직접 올려 내려받게 한다. **macOS 빌드는 서명과 공증, Windows 빌드는 서명**(경고 없이 열리게).

---

## 10. 결제를 나중에 연결하는 구조 (online 9) [확정: 지금 넣지 않음, 연결만 하면 되게, 없어도 완전히 동작 — DEC 1.14, 2.6]

목표: 결제를 붙이는 날 **게임 로직과 기존 표를 건드리지 않고**, 서버에 결제 제공자 구현 하나 + 상품 목록 데이터, 클라이언트에 결제창 구현 하나만 추가. 붙이지 않은 상태에서는 **흔적 없이** 완전히 동작.

### 10.1 재화 구분 (online 9.1)

| 재화 | 구분(bucket) | 얻는 법 | 거래 | 출시 시 |
|---|---|---|---|---|
| 골드 (`gold`) | 획득 (`earned`) | 플레이 | 가능 | 사용 |
| 시즌 주화 (`season_coin`) | 획득 (`earned`) | 플레이 | 불가 | 사용 |
| 별빛 [제안: 이름] (`starlight`) | **유료** (`paid`) | 결제 | 불가 | 표와 코드만, 잔액 항상 0 |
| 별빛 (`starlight`) | **무상** (`free`) | 이벤트·보상·교환 코드 | 불가 | 위와 같음 |

- 유료와 무상을 처음부터 나눠 적는다. "돈 주고 산 잔액이 얼마인가"에 답할 수 있어야 한다. **쓸 때는 무상부터** 빠진다.
- 모든 증감은 `currency_ledger`에 한 줄씩.
- 별빛으로 산 물건 = `item_instance.origin = 'paid'`, `bound = true`. 직접 거래·장터 서버 함수는 `bound`인 물건과 거래 불가 재화를 **거절**. 이 검사는 **결제가 없어도 지금부터** 돈다(후계자 전용 보상·복수 보상이 같은 검사).
- 별빛을 골드로 바꾸는 길은 만들지 않는다.
- 캐릭터와 전용 지팡이는 별빛으로 팔지 않는다. 골드 상점의 +0 새 지팡이(500골드)는 골드로만.
- 출시 시 유료 재화의 용도 기본값: 꾸미기와 편의에만 (online 14-7) [제안].

**별빛 차감 의사코드**

```
SpendStarlight(acct, n):
    free = balance(acct,'starlight','free') FOR UPDATE
    paid = balance(acct,'starlight','paid') FOR UPDATE
    if paid.amount < 0: -> ERR_STARLIGHT_BLOCKED      // 환불 음수 잔액(9.5)
    if free.amount + paid.amount < n: -> ERR_INSUFFICIENT
    useFree = min(free.amount, n); usePaid = n - useFree
    ledger(-useFree, 'free'); ledger(-usePaid, 'paid')
```

### 10.2 인터페이스 (online 9.2)

서버(`wizard-api` 안):

```csharp
public enum PurchaseFlow { ClientReceipt, ServerOrder, RedeemCode }

public interface IPaymentProvider {
    string Id { get; }                 // "none", "fake-receipt", "fake-order", 나중에 "pg-…", "store-…"
    PurchaseFlow Flow { get; }
    bool IsEnabled { get; }            // NullPaymentProvider는 항상 false
    Task<ProviderOrder> CreateAsync(PurchaseOrder order, Product product);     // ServerOrder형: 결제창 주소·토큰
    Task<VerifyResult> VerifyAsync(PurchaseOrder order, string? proof);        // 제공자 서버에 직접 확인
    bool TryParseWebhook(HttpRequest request, out ProviderEvent providerEvent); // 서명 검증 포함
    Task<IReadOnlyList<ProviderEvent>> PollAsync(DateTime since);              // 알림 놓쳤을 때
}
```

클라이언트:

```csharp
// IBackend (ServerBackend만 실제로 부른다)
Task<StoreView>    GetStoreAsync();                         // 결제가 꺼져 있으면 유료 상품 0개
Task<OrderTicket>  CreateOrderAsync(string productId);      // 주문 번호 + 흐름 + (있으면) 결제창 주소
Task<OrderResult>  ConfirmOrderAsync(string orderId, string? proof);

public interface IPurchaseFlow {
    PurchaseFlow Flow { get; }
    Task<string?> AuthorizeAsync(OrderTicket ticket);       // 영수증형: 영수증 / 주문형: 창 닫힐 때까지 기다린 뒤 null / 코드형: 입력 코드
}
```

규칙:
- 출시 서버에는 `NullPaymentProvider` **하나만** 등록. 모든 메서드가 "지원 안 함".
- `config.payments_enabled = false`면 주문 함수는 **제공자를 보기도 전에** 거절. 누가 직접 호출해도 같다. 알림(webhook) 주소도 항상 거절.
- 상품 목록의 진실은 서버 `product` 표 하나. 클라이언트 구현은 현지 가격 문자열만 덧붙인다.
- 출시 빌드에는 `IPurchaseFlow` 구현 등록 없음. 상점 화면은 `GetStoreAsync`의 유료 상품이 0개면 **별빛 탭과 잔액 표시를 아예 그리지 않는다**. "준비 중" 같은 빈 화면도 두지 않는다.
- **서버 스위치, 서버 제공자, 클라이언트 구현 셋 다** 있어야 구매가 된다.

보조 타입 [임시]:

```csharp
public sealed record ProviderOrder(string? ProviderRef, string? CheckoutUrl, string? CheckoutToken);
public sealed record VerifyResult(bool Ok, bool Final, string? ProviderRef, string? Error); // Final=false면 아직 결제 전
public sealed record ProviderEvent(string ProviderRef, ProviderEventKind Kind, DateTime At, string Raw);
public enum ProviderEventKind { Paid, Refunded, Cancelled }
public sealed record OrderTicket(Guid OrderId, PurchaseFlow Flow, string? CheckoutUrl);
public sealed record OrderResult(Guid OrderId, OrderStatus Status, IReadOnlyList<GrantLine> Granted);
```

### 10.3 주문 상태 기계 (online 9.2)

```
상태:  생성(created) ──▶ 승인 대기(pending_auth) ──▶ 승인됨(authorized) ──▶ 지급 완료(granted)
         │                    │                                          │
         └──▶ 취소(cancelled) ◀┘ (만료 30분)                               └──▶ 환불(refunded) ──▶ 회수됨(revoked)
```

| 전이 | 조건 |
|---|---|
| (없음) → `created` | `CreateOrder`: `payments_enabled` 참, 상품 `enabled`·기간 안, 흐름이 `product.flows`에 있음, 계정이 결제 막힘이 아님. `expires_at = now + 30분` |
| `created` → `pending_auth` | ServerOrder: `CreateAsync`가 결제창 주소를 돌려줌. ClientReceipt/RedeemCode: 티켓을 내려 준 즉시 [임시] |
| `created`/`pending_auth` → `cancelled` | `now ≥ expires_at`(30분) 또는 제공자 취소 알림 |
| `pending_auth` → `authorized` | `VerifyAsync` 성공(제공자에 **직접** 확인). 알림 또는 `ConfirmOrder` 또는 `PollAsync`가 계기 |
| `authorized` → `granted` | 지급 트랜잭션(아래). 같은 트랜잭션에서 바로 이어 간다 [임시] |
| `granted` → `refunded` | 환불 알림 또는 주기 조회 → `refund_event` 기록 |
| `refunded` → `revoked` | 권리 철회·회수 완료 |

- `authorized` 상태로 남는 경우: 지급 트랜잭션이 실패했을 때. 다음 접속 때 "승인됨"인 주문을 찾아 마저 지급한다 (online 9.3).
- 만료(30분) 뒤에 늦게 온 "결제 완료" 알림 [임시]: 주문이 `cancelled`라도 제공자 확인이 성공이면 `authorized`로 되살려 지급한다(돈을 받았으므로). 15절 C-08.

### 10.4 구매 흐름 세 가지 (online 9.3)

같은 주문 표와 같은 두 함수(`CreateOrder`, `ConfirmOrder`)로 모두 받는다.

| 흐름 | 쓰는 곳 | 순서 |
|---|---|---|
| **A. 클라이언트 영수증형** (`ClientReceipt`) | 앱 장터 방식 스토어 | ① CreateOrder → ② 클라이언트가 스토어 결제 → 영수증 → ③ ConfirmOrder(영수증) → ④ 서버가 스토어 서버에 영수증 직접 확인 → ⑤ 지급 |
| **B. 서버 주문형** (`ServerOrder`) | 웹 결제창, 결제 대행사(직접 배포에 맞다) | ① CreateOrder → 서버가 제공자에 결제 건 생성, 결제창 주소 받음 → ② 클라이언트가 결제창(브라우저) → ③ 이용자 승인 → ④ 제공자가 서버로 알림(또는 클라이언트가 ConfirmOrder) → ⑤ 서버가 제공자에 직접 확인 → ⑥ 지급 |
| **C. 교환 코드형** (`RedeemCode`) | 선물 코드, 행사 코드, 후원 보상 | ① CreateOrder → ② 코드 입력 → ③ ConfirmOrder(코드) → ④ 서버가 `redeem_code`에서 확인(한 번만 쓰이게 **행 잠금**) → ⑤ 지급. 바깥 제공자 불필요 |

**지급 트랜잭션 의사코드**

```
Grant(orderId):
  BEGIN
    o = SELECT * FROM purchase_order WHERE id = orderId FOR UPDATE
    if o.status == 'granted' or o.status in ('refunded','revoked'): ROLLBACK; return FirstResult(o)   // 같은 order_id 재확인
    require o.status == 'authorized'
    (provider_ref 유일 제약이 같은 결제의 두 번째 주문을 막는다)
    UPDATE purchase_order SET status='granted', granted_at=now()
    INSERT entitlement(kind, ref, source='order:'||o.id)
    for line in product.contents:
        if currency: currency_balance(starlight, paid) += n ; currency_ledger(+n, 'paid', reason='purchase', ref_type='order', ref_id=o.id)
        if item:     INSERT item_instance(origin='paid', bound=true) ; item_ledger(+1, reason='purchase')
  COMMIT
```

- 클라이언트의 "결제 성공"이라는 말은 어느 흐름에서도 믿지 않는다.
- B는 제공자의 알림을 맥미니가 받아야 한다. 443 HTTPS로 들어오므로 3.3절 어느 노출 방식에서도 된다. 꺼져 있던 동안의 알림은 켜진 뒤 `PollAsync`로 메운다.
- 결제창을 닫고 게임을 꺼 버려도 주문은 서버에 남는다. 다음 접속 때 "승인됨" 주문을 찾아 마저 지급.
- 교환 코드 해시 [임시]: `SHA-256(정규화된 코드)`. 코드 형식은 재설정 코드와 같은 32자 알파벳 16자 [임시].
- 게임 로직은 **"이 계정이 이 꾸미기를 갖고 있는가"만** 묻는다. 플레이로 얻었는지 샀는지는 `origin`과 `entitlement.source`에만 있고, 전투·강화·매치·리그 코드는 그 값을 읽지 않는다.

### 10.5 가짜 제공자 (online 9.4)

- `FakeReceiptProvider`(A형), `FakeOrderProvider`(B형, 가짜 결제창과 가짜 알림 포함), 코드형(C형은 그대로)을 **테스트 빌드**에 둔다.
- 10.7 점검표를 **세 흐름 모두** 통과시킨 뒤 `NullPaymentProvider`로 되돌려 출시한다. 연결하는 날 처음 돌려 보는 코드가 없게.
- 컴파일 분리 [임시]: 가짜 제공자는 `Wizard.Server.Payments.Fakes` 별도 어셈블리, 출시 구성의 DI 등록에서 제외. 출시 빌드가 이 어셈블리를 참조하면 CI 실패.

### 10.6 환불 (online 9.5)

```
OnRefund(evt):                          // 알림 또는 PollAsync
  BEGIN
    INSERT refund_event(order_id, provider_ref, at) ON CONFLICT (provider_ref) DO NOTHING
    if 이미 처리됨: COMMIT; return       // 같은 알림 두 번
    o = order FOR UPDATE ; o.status = 'refunded'
    entitlement.revoked_at = now()
    물건: 회수(귀속이라 남에게 넘어가 있지 않다) → item_ledger(-1, reason='refund')
    별빛: paid 구분에서 n 뺀다. 모자라면 음수 허용 → 0 이상이 될 때까지 별빛 사용 막음
          골드나 플레이 진행은 건드리지 않는다
    o.status = 'revoked' ; refund_event.handled_at = now()
    if CountRefunds(account, 최근 90일 [임시]) >= 2 [임시]: account.flags |= PAYMENT_BLOCKED ; 표시
    audit_log(actor='system', action='refund', …)
  COMMIT
```

- "같은 계정의 환불이 반복되면 결제를 막고 표시한다" — 반복 기준은 원문이 정하지 않음 → [임시] 90일 안 2회.
- 전 과정이 `audit_log`와 원장에 남는다.

### 10.7 결제 없이 완전히 동작하기 위한 조건 (online 9.6) — 출시 전 점검표

- [ ] 게임의 모든 물건과 기능이 골드, 시즌 주화, 플레이 보상만으로 얻어진다. 별빛으로만 얻는 것이 하나도 없다.
- [ ] 강화 확률, 비용, 부적, 강화석, 블록, 리그, 매치, 순위 계산 어디에도 별빛·유료 여부를 읽는 코드가 없다.
- [ ] 경제 균형(online 2절)이 별빛 유입 0을 전제로 맞춰져 있다.
- [ ] 유료 상품이 0개일 때 상점의 별빛 탭, 잔액, 구매 버튼, 관련 안내문이 화면에 전혀 나오지 않는다.
- [ ] `product`, `purchase_order`, `payment_proof`, `entitlement`, `refund_event`, `redeem_code` 표가 비어 있어도 모든 서버 함수가 정상 동작한다.
- [ ] 서버 스위치가 꺼져 있으면 주문 생성·확인 함수와 알림 주소는 항상 거절한다(누가 직접 호출해도).
- [ ] 거래 서버 함수의 `bound`·거래 불가 검사가 결제와 무관하게 지금 켜져 있고 테스트가 있다.
- [ ] 자동 테스트에 "별빛 잔액이 0이 아닌 계정", "유료 물건을 거래에 올리는 시도", "같은 영수증 두 번", "같은 알림 두 번", "결제창을 닫고 재접속", "쓴 코드 다시 쓰기", "환불로 음수 잔액"이 가짜 제공자로 들어 있다.
- [ ] **오프라인 프로필에서는 `GetStoreAsync`가 유료 상품 0개를 돌려주고 `CreateOrderAsync`가 호출되지 않는다.** `OfflineBackend`에는 결제 코드가 없다.
- [ ] **서버에 닿지 않는 상태에서 게임을 켜도** 결제 관련 화면, 오류, 대기가 하나도 없다.

"읽는 코드가 없다" 자동 검사 [임시]: Roslyn 분석기 또는 CI 스크립트로 `Wizard.Sim`, `Wizard.Rules`의 강화·매치·리그 네임스페이스가 `Starlight`, `Bucket.Paid`, `ItemOrigin.Paid`, `Entitlement.Source`를 참조하면 실패.

### 10.8 한국에서 결제를 켤 때 확인할 의무 (online 9.7) — 참고, 법률 자문 아님

켜기 전에 그 시점의 법령과 고시를 직접 확인하거나 전문가에게 확인받아야 한다.

| 주제 | 닿는 지점 | 확인할 것 |
|---|---|---|
| 확률형 아이템 정보 표시 | 강화가 확률형. 유료 재화·유료 재료가 강화에 쓰이면 "강화형" 확률형 아이템일 수 있다 | 의무 대상·범위, 표시 방법, 소규모 사업자 예외, 제재. 강화 화면의 확률 내역 표시와 `enhance_log`가 대비가 된다 |
| 등급분류 | 결제와 무관하게 국내 유통에 등급 필요. 플레이어 간 거래 + 유료 재화가 있으면 등급이 올라갈 수 있다 | 직접 배포의 등급분류 경로, 거래·유료 재화·확률형·채팅이 판정에 주는 영향, 내용 수정 신고 의무 |
| 게임 결과물의 환전 | 지팡이·재료 거래 → 게임 밖 현금 거래 가능성 | 환전·환전 알선 금지 규정과 사업자 조치 범위. 약관의 현금 거래 금지, 11절 탐지·제재 |
| 유료 재화가 거래로 흘러가는 경로 | 10.1에서 유료 재화·유료 물건 거래 불가 | 이 차단이 등급·사행성 판단에서 어떻게 평가되는지 |
| 전자상거래 | 결제, 청약 철회, 환불. 결제 대행사와 직접 계약 | 사업자 등록, 통신판매업 신고, 청약 철회 제한 고지, 거래 기록 보존 기간, 표시·광고 규정 |
| 미성년자 결제 | 나이를 받지 않는다 | 법정대리인 동의, 미성년자 결제 취소, 결제 한도 |
| 개인정보 | 지금도 아이디, 닉네임, **이메일**, 접속 IP, 기기 식별값 해시. 메일 사업자에 주소가 넘어감(처리 위탁일 수 있음). 결제를 붙이면 항목 증가 | 개인정보 처리방침과 수집·이용 동의 화면이 **결제와 무관하게 가입 화면에 필요한지**, 최소화, 만 14세 미만 동의, 메일 사업자 위탁 고지, 집에 둔 서버의 보관 책임 |
| 선불 재화 | 별빛은 미리 사 두는 재화 | 선불 충전 재화 규정, 서비스 종료 시 환급 기준 |

---

## 11. 부정 이용과 안전 (online 10) [제안]

| 주제 | 대응 | 구현 연결 |
|---|---|---|
| 봇(자동 사냥) | 판마다 입력 기록이 서버에 있다. 입력 간격 분포가 기계적인 계정, **하루 20시간 이상** 도는 계정, 같은 입력열이 반복되는 계정을 표시. 깊은 웨이브일수록 수입이 커서 얕은 웨이브 반복의 수익이 낮다. 귀환 가방 칸수와 영웅 블록 제한이 블록 양산의 상한 | `wizard-verify`가 판마다 통계 기록 → `account.flags` 검토 표시 (자동 제재 아님) |
| 광장 봇·도배 | 채널별 발언 간격, 반복 문장 차단, 새 계정 채팅 제한, 주소·연락처 모양 차단(online 5.3) | 채팅 명세 |
| 현금 거래 | 약관으로 금지. 탐지: 감정가와 동떨어진 거래, 한쪽만 주는 거래, 새 계정으로 몰리는 골드, 한 계정에서 여러 계정으로 퍼지는 물건. 억제: 수수료, 하루 골드 상한, 선물 조건, 장터 가격 범위, 지팡이 7일 재거래 잠금. 제재: 양쪽 계정의 해당 물건 회수와 거래 정지 | 9.3 이상 탐지 |
| 계정 탈취 | Argon2id, 틀린 로그인 대기, 갱신 토큰 재사용 탐지, 새 기기 24시간 거래 제한, 비밀번호 변경 시 전체 토큰 폐기. 이메일 복구로 메일 계정이 털리면 게임 계정도 넘어갈 수 있다: 재설정 코드는 한 번만 쓰이고 30분 뒤 만료, 재설정 뒤 24시간 거래·장터 등록 막힘, 비밀번호·이메일 변경 시 알림 메일 | 2절 |
| 부계정 양산 | 가입 제한(IP·기기당 하루 3개), 기능별 플레이 조건, 기기당 자동 매치 계정 3개, 같은 기기 계정 사이 거래 표시. 완전히 막을 수는 없다 | 2.5 |
| 메모리·속도 조작 | 서버 프로필의 능력치와 인벤토리는 서버에 있다. 무한 웨이브 결과는 전수 재실행 검증. 결투는 서버 시뮬레이션이라 클라이언트 상태를 고쳐도 자기 화면만 틀어진다 | 7절 |
| 결투 입력 조작 | 봉인 뒤에만 상대 입력 공개, 늦은 입력 폐기, 미래 시각 거절, 순서 검사. 마감에 반복해 걸리거나 이벤트가 자주 버려지는 계정을 표시 | 결투 명세 |
| 결투 이탈·고의 지연 | 이탈 패배, 대기열 금지(online 4.10). 되감기 한도를 반복해서 넘기게 만드는 쪽은 자기 입력이 버려져 손해 | 결투 명세 |
| 서버 공격 | 열어 둔 포트 최소화, 토큰 없는 UDP 패킷 폐기, IP당 요청 제한, 필요하면 중계 서버 뒤로 | 3절 |
| 채팅 폭력 | 필터, 신고, 자동 임시 차단, 차단 목록, 채널 끄기. 제재 단계: **경고 → 채팅 1일 → 7일 → 30일 → 영구**. 결투 중 자유 채팅 없음 | `sanction(kind='chat')` |
| 닉네임·아이디 | 금칙어 필터, 운영자·NPC 사칭 금지어, 신고 시 강제 변경 | 2.1 |
| 미성년자 | 모든 이용자를 미성년자일 수 있다고 보고 기본값: **귓속말 기본 "친구만"**, **채팅 필터는 끌 수 없다**. 현금 가치가 걸린 기능 없음(결제 미연결) | 설정 기본값 |
| 개인정보 최소화 | 받는 것: 아이디, 비밀번호 해시, 닉네임, 이메일(암호화, 복구·보안 알림 전용, 계정 삭제 때 삭제), 기기 식별값 해시, 접속 IP(30일 뒤 삭제). 받지 않는 것: 실명, 생년월일, 전화번호. 채팅 기록 90일, 신고 발췌는 처리 뒤 1년 | 8.4 |
| 운영자 권한 | 지급·회수·제재는 전부 `audit_log`. 혼자 운영하더라도 남긴다 | 9.1 |

**채팅 제재 단계 상태 기계 [제안 + 임시]**

```
level 0 (없음) → 위반 → level 1 경고 → 위반 → level 2 채팅 1일 → 위반 → level 3 7일 → 위반 → level 4 30일 → 위반 → level 5 영구
단계가 내려가는 조건(예: 180일 무위반 시 한 단계 낮춤)은 원문에 없다 → [임시] 내려가지 않는다
```

- 봇 "하루 20시간 이상": 서버 시각 기준 지난 24시간 이동 창에서 원정 판 시간(`ended_at − started_at` 합) ≥ 72,000초(= 4,320,000틱) [임시 해석].

---

## 12. 만드는 순서 (온라인 부분) (online 11, GDD 16) [제안]

일정이 아니라 **의존 순서**다. 범위는 줄이지 않는다 (DEC 1.16).

| 순서 | 단계 | 내용 | 앞 단계 의존 | 완료 확인 |
|---|---|---|---|---|
| 1 | 결정론 코어 | GDD의 손맛 시제품을 처음부터 `Wizard.Sim` 위에. 입력 이벤트 구조체와 순서 규칙(online 3.1), 입력 기록 저장·다시 돌리기, 상태 해시 | 없음 | "같은 기록을 두 번 돌리면 해시가 같다", "같은 오프셋의 방향과 K는 방향이 먼저" 자동 테스트 |
| 2 | `IBackend`, `Wizard.Rules`, `OfflineBackend` | 프로필, 서고, 지팡이, 정산, 드랍, **강화 화면과 슬롯 새기기**, 상점을 오프라인 프로필로 완성. **여기까지가 서버 없이 완전한 게임** | 1 | 네트워크를 끊고 처음 실행 → 판 → 정산 → 강화 → 다시 실행 통과. 서버 주소로 나가는 호출 0건(상태 파일 읽기만 예외, 실패해도 조용히) (online 8.3) |
| 3 | 서버 기반 | `wizard-api`, PostgreSQL, 계정(가입·로그인·토큰, 이메일 확인과 비밀번호 복구. `IMailSender`는 **`NullMailSender`로 먼저**, 메일 사업자는 나중), 원장, 멱등 키, 강화 서버 함수, `ServerBackend`. 맥미니에 올리고 TLS, 상태 파일, 백업 | 2 | 14절 T-AUTH, T-MAIL, T-TLS, T-BKP 테스트 |
| 4 | 무한 웨이브 제출과 검증 워커, 순위표 (+ 주간 도전, GDD 16-11) | | 1, 3 | |
| 5 | 광장 | 인스턴스, 이동, 시설, 채팅, 친구, 살펴보기 (+ 서버 전체 알림, GDD 16-12) | 3 | |
| 6 | 거래 | 직접 거래, 장터, 우편함, 야간 대조 | 3, 5 | 9.5 대조 |
| 7 | 결투 | 로컬 2인(`LoopbackTransport`, online 4.4 통과 기준) → `wizard-duel`(서버 시뮬레이션, 봉인, 검증) → 예측과 되감기 → 결투 방 → 자동 매치, IL과 리그, 점수 | 1, 3, 5 | |
| 8 | 시즌 | 후계 점수, 정산, 보상, 시즌 상점 | 4, 7 | |
| 9 | 운영 | 관리 화면, 이상 탐지, 부하 시험(4절), 복원 연습(5절), 꺼졌다 켜기 연습(6절) | 3~8 | 14절 T-CAP, T-DOWN |
| 10 | 결제 뼈대 점검 | 가짜 제공자로 10.7 목록을 **세 흐름 모두** 통과 → 빈 제공자로 되돌려 출시 | 3 (원장·멱등), 6 (`bound` 검사) | 14절 T-PAY |

- 메일 사업자 연결은 3단계 안의 하위 순서다: `NullMailSender` → 사업자 선정(16절 U-01) → `ApiMailSender` → DNS의 SPF/DKIM/DMARC → 도달 시험.
- 3단계 안의 하위 의존 [임시]: DB 스키마·마이그레이션 → 멱등·원장 도우미 → 계정·토큰 → 기능 값(`/v1/features`) → 강화 서버 함수 → `ServerBackend` → 맥미니 배치(launchd, Caddy, DDNS) → 상태 파일 서명 도구 → 백업 작업.

---

## 13. QA에서 먼저 볼 수치 (online 12) [제안]

원본 15개 항목을 그대로 옮기고 측정 방법을 붙인다. 이 명세의 영역(용량·운영)이 아닌 항목도 대시보드 지표로 수집할 대상이라 모두 둔다.

| # | 볼 것 | 기준값 / 목표 | 측정 데이터 [임시] |
|---|---|---|---|
| Q1 | 지팡이 단계별 실제 도달 웨이브와 웨이브 하나에 실제로 걸리는 시간(online 2.3 가정). 2.6의 시간 추정이 전부 여기에 서 있다. 판 길이는 기본 가정과 `monsters.md` 6.6의 속도가 **1.3~1.6배** 다르다 | 가정과의 차이 | `run`(wave, started_at, ended_at, wand_snapshot.stage) |
| Q2 | 시간당 골드와 +13까지의 실제 시간 | 목표 **65~90시간**(제물을 넣는 보통 플레이어) [확정 DEC 1.20]. 다르면 **비용 계수 100**부터 고친다 | `currency_ledger`(reason=run_settle), `enhance_log` |
| Q3 | 강화석과 부적의 수급, 장터 가격. 계산상 중급이 쓰는 양의 **25배**, 상급 **5배** 들어온다. 부적을 +10부터 쓰면 +13까지 시간이 **절반 가까이** 준다 | 의도와 맞는지 | `item_ledger`, `market_sale` |
| Q4 | 귀환 가방으로 들어오는 제물 블록이 **시도당 3개**를 대는지. 계정당 지팡이 자루 수 분포, 떠나보내기·+0 구입 횟수, **+4 이하**에서 떠나보낸 지팡이 비율(슬롯 낚시) | | `wand`, `item_ledger` |
| Q5 | 결투 체력(**100 × 칸**), 라운드 길이, 순간 피해 상한 **30%**, 가시밭이 나오는 라운드 비율 | | `duel_match.rounds` |
| Q6 | 결투 조준 보정 **10도**와 판정 반지름 **0.30**(Q32.32 = 1,288,490,189) | 마력탄 단독 적중률 **15% 이상**, 시간 종료 라운드 **20% 이하** | `duel_match.rounds` |
| Q7 | 결투의 스침·벌칙 **1.0초**(60틱) 창 | 초당 결정 획득량이 무한 웨이브보다 크지 않은지 | 결투·판 통계 |
| Q8 | 5×5 캐릭터 상성표(`world-characters.md` 5.4) | **45~55%** 밖의 짝 | `duel_match` |
| Q9 | 결투 강화 배율 `1 + 0.03 × 단계` | 같은 실력에서 +4와 +0의 승률 차이가 **55:45**를 넘지 않는지. 유리 지팡이 등 변이 조합별 허수아비 피해 | `duel_match`, 연습 기록 |
| Q10 | 리그 경계 **70 / 155** 분포 | 한 리그에 대기열이 마를 정도로 쏠리면 경계를 옮긴다 | `il_history`, 대기열 지표 |
| Q11 | 입력 지연 **2·3·4틱**에서 섬(**0.07초** = 4.2틱)과 만개(**0.18초** = 10.8틱) 성공률 | 통신 없는 판과 **같아야 한다**(달라지면 안 된다) | 봇 시험 + `duel_match.net_stats` |
| Q12 | 서버 왕복 **60 / 90 / 120ms**, 지터 **10 / 20 / 30ms**, 유실 **1 / 3%** 조합 | 버려지는 입력 비율과 되감기 깊이. online 4.12 표가 맞는지 | 망 에뮬레이터 + `net_stats` |
| Q13 | 왕복 **100ms** 상대와 붙을 때 "나간 것처럼 보였다가 취소되는 비기" 빈도 | 준비 **2틱**과 **3틱** 지연 적용이 충분한지 | 클라이언트 예측 취소 계수 |
| Q14 | 봇 클라이언트 **300 / 500명** 부하에서 맥미니의 CPU, 회선, 검증 대기열 길이, 서버 틱 지연 | 4절 표(300명 여유, 500명 통과) | 부하 시험 도구(봇 클라이언트) |
| Q15 | 후계 점수의 E와 P 분포 | **상위 20명**에서 한쪽 모드만으로 순위가 뒤집히는지 | `season_result` |

**부하 시험 봇 [제안 + 임시]**: `Wizard.Tools.LoadBot` 콘솔. 시나리오 `plaza_idle_walk`(광장 이동 + 5초마다 채팅 확률 10%), `duel_bot_pair`(봇 결투, 60패킷/초), `run_submit`(25분 분량 녹화 기록 제출), `login_storm`(로그인 폭주 — 해시 동시 4건 확인). 비율은 4절 표의 광장:결투:판 = 200:60:40 / 350:100:50.

---

## 14. 테스트 벡터 (given / when / then)

각 사례는 자동 테스트 하나다. ID 접두어: T-AUTH(계정), T-MAIL, T-TOK(토큰), T-RATE(한도), T-TLS(상태 파일·TLS), T-CAP(용량 검산), T-BKP(백업), T-DOWN(꺼짐), T-AUTHZ(권위), T-DB(데이터 모델), T-IDEM(멱등), T-PAY(결제), T-SAFE(안전), T-ORD(순서), T-OFF(오프라인).

### 14.1 가입 입력

| ID | given | when | then |
|---|---|---|---|
| T-AUTH-01 | 빈 DB | 아이디 `abcd`(4자) 가입 | 성공 |
| T-AUTH-02 | | 아이디 `abc`(3자) | `ERR_LOGIN_ID_FORMAT` |
| T-AUTH-03 | | 아이디 `abcdefghijklmnop`(16자) | 성공 |
| T-AUTH-04 | | 아이디 `abcdefghijklmnopq`(17자) | `ERR_LOGIN_ID_FORMAT` |
| T-AUTH-05 | | 아이디 `Abcd`(대문자) | `ERR_LOGIN_ID_FORMAT` |
| T-AUTH-06 | | 아이디 `ab_cd` | `ERR_LOGIN_ID_FORMAT` |
| T-AUTH-07 | `abcd` 존재 | 아이디 `abcd` | `ERR_LOGIN_ID_TAKEN` |
| T-AUTH-08 | | 비밀번호 9자 | `ERR_PW_TOO_SHORT` |
| T-AUTH-09 | | 비밀번호 10자, 흔한 목록에 없음, 아이디 미포함 | 성공 (문자 종류 강제 없음: `aaaaaaaaab` 같은 소문자만도 성공, 단 흔한 목록에 없을 때) |
| T-AUTH-10 | 흔한 목록에 `password123` | 비밀번호 `password123` | `ERR_PW_COMMON` |
| T-AUTH-11 | 아이디 `wizard77` | 비밀번호 `xxwizard77yy` | `ERR_PW_CONTAINS_ID` |
| T-AUTH-12 | | 닉네임 `가`(1자) | `ERR_NICK_FORMAT` |
| T-AUTH-13 | | 닉네임 `가나`(2자) | 성공 |
| T-AUTH-14 | | 닉네임 `가나다라마바사아`(8자) | 성공 |
| T-AUTH-15 | | 닉네임 9자 | `ERR_NICK_FORMAT` |
| T-AUTH-16 | | 닉네임 `ab12가나` | 성공 |
| T-AUTH-17 | | 닉네임 `ㄱㄴ`(자모) | `ERR_NICK_FORMAT` [임시] |
| T-AUTH-18 | | 닉네임 `가 나`(공백) | `ERR_NICK_FORMAT` |
| T-AUTH-19 | | 이메일 254자(형식 맞음) | 성공 |
| T-AUTH-20 | | 이메일 255자 | `ERR_EMAIL_FORMAT` |
| T-AUTH-21 | | 이메일 `abc` (`@` 없음) | `ERR_EMAIL_FORMAT` |
| T-AUTH-22 | `A@x.com`으로 3계정 | ` a@X.COM ` 으로 4번째 가입 | `ERR_EMAIL_LIMIT` (정규화 후 HMAC 같음) |
| T-AUTH-23 | 같은 주소 2계정 | 3번째 가입 | 성공 |
| T-AUTH-24 | 가입 성공 | DB 확인 | `email_enc`는 평문 주소를 바이트로 포함하지 않는다. `email_verified_at IS NULL`. 실명·전화·생일 열이 없다 |
| T-AUTH-25 | 가입 성공 | 로그(전체) 검색 | 비밀번호 평문, 이메일 평문이 0건 |

### 14.2 비밀번호 해시

| ID | given | when | then |
|---|---|---|---|
| T-AUTH-30 | 가입 | `pw_params` 확인 | `alg=argon2id, m_kib=65536, t=3, p=1, salt_len=16` |
| T-AUTH-31 | 두 계정이 같은 비밀번호 | `pw_hash` 비교 | 서로 다르다(소금 16바이트 무작위) |
| T-AUTH-32 | pepper 파일을 다른 값으로 교체 | 로그인 | 실패(pepper가 섞였음을 증명) |
| T-AUTH-33 | 계정이 `t=2`로 저장됨, 현재 설정 `t=3` | 올바른 비밀번호로 로그인 | 성공 후 `pw_params.t = 3`, `pw_hash` 바뀜 |
| T-AUTH-34 | 같은 상황 | **틀린** 비밀번호 | 다시 해시하지 않는다 |
| T-AUTH-35 | 로그인 10건 동시 | 해시 계산 동시 실행 수 측정 | 최대 4 |
| T-AUTH-36 | 기준 맥미니 | Argon2id 1회 | 약 0.1초(0.05~0.2초 허용 [임시]). 벗어나면 설정 조정 경고 |
| T-AUTH-37 | 없는 아이디 | 로그인 응답 시간 | 있는 아이디+틀린 비밀번호와 같은 분포(차이 평균 20ms 이내 [임시]) |

### 14.3 토큰과 세션

| ID | given | when | then |
|---|---|---|---|
| T-TOK-01 | 로그인 | 응답 | 접근·갱신 토큰 각각 32바이트(base64url 43자). DB에는 SHA-256 해시만, 평문 0건 |
| T-TOK-02 | 접근 토큰 발급 시각 t0 | t0 + 29분 59초에 API | 성공 |
| T-TOK-03 | | t0 + 30분 00초 | 401 |
| T-TOK-04 | 갱신 토큰 발급 t0 | t0 + 30일 − 1초 갱신 | 성공, 새 토큰 쌍, 이전 갱신 토큰 무효 |
| T-TOK-05 | | t0 + 30일 | 401 |
| T-TOK-06 | 갱신 토큰 R1 → 갱신해서 R2 받음 | R1을 다시 보냄 | 401, 그 기기의 모든 세션 `revoked_at` 설정, R2도 무효 |
| T-TOK-07 | 기기 A·B에 세션 | A에서 R 재사용 탐지 | A 세션만 폐기, B 세션 유지 |
| T-TOK-08 | 기기 A·B 로그인 | 비밀번호 변경(현재 비밀번호 맞음) | A·B 모든 세션 폐기 |
| T-TOK-09 | | 비밀번호 변경, 현재 비밀번호 틀림 | 거절, 세션 유지 |
| T-TOK-10 | 기기 A 웹소켓 접속 중 | 기기 B 같은 계정 접속 | A에 `session.kicked` 후 끊김, B 접속 |
| T-TOK-11 | 요청 바디에 `account_id: 999`(남의 번호) | 접근 토큰은 계정 5 | 서버는 계정 5로 처리(바디 번호 무시) |
| T-TOK-12 | 접근 토큰을 디스크에 저장하려는 클라이언트 코드 | 정적 검사 | 접근 토큰은 메모리에만(저장 API 호출 0건). 갱신 토큰은 키체인/자격 증명 저장소만 |

### 14.4 틀린 로그인

| ID | given | when | then |
|---|---|---|---|
| T-RATE-01 | 연속 실패 4회 | 5번째 시도 즉시 | 시도 허용(대기 없음) — 5회째가 틀리면 그때부터 대기 |
| T-RATE-02 | 연속 실패 5회 (마지막 t) | t + 0.9초 시도 | 429(대기) |
| T-RATE-03 | | t + 1.0초 시도 | 허용 |
| T-RATE-04 | 연속 실패 n | 대기 | n=5→1s, 6→2s, 7→4s, 8→8s, 9→16s, 10→32s, 11→64s, 12→128s, 13→256s, 14→512s, 15→900s, 20→900s |
| T-RATE-05 | 실패 7회 | 성공 로그인 | 연속 실패 수 0으로 |
| T-RATE-06 | 한 IP에서 60초 안 20회 시도 | 21번째 | 429 |
| T-RATE-07 | | 61초 뒤 | 허용 |
| T-RATE-08 | 없는 아이디 / 있는 아이디+틀린 비밀번호 | 응답 본문 비교 | 문구·상태 코드 동일 |
| T-RATE-09 | `login_attempt` 행이 31일 전 | 정리 작업 | 삭제 |

### 14.5 가입 남용

| ID | given | when | then |
|---|---|---|---|
| T-RATE-20 | 한 IP에서 24시간 안 3계정 | 4번째 | `ERR_SIGNUP_LIMIT` |
| T-RATE-21 | 첫 가입 후 24시간 1초 지남(3번째까지는 그 안) | 같은 IP 4번째 | 첫 가입이 창에서 빠져 성공 [임시 이동 창] |
| T-RATE-22 | 한 기기 3계정/24시간, IP는 다 다름 | 4번째 | `ERR_SIGNUP_LIMIT` |
| T-RATE-23 | 웨이브 9까지 간 새 계정 | 광장 채널 발언 | 거절(주변만) |
| T-RATE-24 | 웨이브 10 도달 | 광장 채널 발언 | 허용 |
| T-RATE-25 | 웨이브 10, `play_seconds = 7199` | 장터 등록 | 거절 |
| T-RATE-26 | 웨이브 10, `play_seconds = 7200` | 장터 등록 | 허용 |
| T-RATE-27 | 같은 기기에서 자동 매치를 쓴 계정 3개 | 4번째 계정 자동 매치 | 거절 |

### 14.6 이메일 확인

| ID | given | when | then |
|---|---|---|---|
| T-MAIL-01 | 가입 직후, 메일 켜짐 | outbox | `verify_email` 1건 대기, 코드 6자리 숫자 |
| T-MAIL-02 | 코드 발급 t0 | t0 + 14분 59초 맞는 코드 | `email_verified_at` 설정 |
| T-MAIL-03 | | t0 + 15분 | `ERR_CODE_INVALID` |
| T-MAIL-04 | 틀림 4회 | 5번째 틀림 | 코드 폐기, 맞는 코드도 이후 거절 |
| T-MAIL-05 | 틀림 4회 | 5번째에 맞는 코드 | 성공 |
| T-MAIL-06 | 다시 보내기 t | t + 59초 다시 보내기 | 거절(남은 1초) |
| T-MAIL-07 | | t + 60초 | 허용 |
| T-MAIL-08 | 오늘 다시 보내기 5회 | 6번째 | 거절 |
| T-MAIL-09 | 미확인 계정 | 로그인, 원정, 강화, 결투 | 전부 허용 |
| T-MAIL-10 | 미확인 계정 | 비밀번호 재설정 요청(맞는 아이디·주소) | 같은 화면 답, outbox 0건 |
| T-MAIL-11 | 미확인 계정 | 비밀번호 변경 | 알림 메일 0건(미확인 주소에는 확인 메일만) |
| T-MAIL-12 | 메일 본문 생성 | 확인 메일 | 코드, 만료 시각, "요청한 적이 없으면 무시하라"만. 아이디·닉네임·URL·`<img` 없음. 글자 메일(text/plain) |
| T-MAIL-13 | | 아이디 찾기 메일 | 아이디 목록만. 코드·닉네임 없음 |
| T-MAIL-14 | DB의 확인 코드 행 | `token_hash` | 원문 6자리가 아님(해시) |

### 14.7 이메일 바꾸기

| ID | given | when | then |
|---|---|---|---|
| T-MAIL-20 | 확인된 옛 주소 | 현재 비밀번호 틀림으로 변경 요청 | 거절 |
| T-MAIL-21 | | 맞는 비밀번호 + 새 주소 → 새 주소로 코드 → 맞음 | `email_enc`=새 주소, `email_verified_at` 설정, 옛 주소로 `notify_email_changed` 1통, `trade_hold_until = now+24h` |
| T-MAIL-22 | 교체 직후 | DB 전체·백업 덤프에서 옛 주소 HMAC 검색 | `account`에 없음 |
| T-MAIL-23 | 교체 후 23시간 59분 | 거래 신청 | `ERR_TRADE_HOLD` |
| T-MAIL-24 | 교체 후 24시간 | 거래 신청 | 허용 |
| T-MAIL-25 | 옛 주소 미확인 | 교체 | 옛 주소 알림 0통 |

### 14.8 비밀번호 재설정

| ID | given | when | then |
|---|---|---|---|
| T-MAIL-30 | | 재설정 코드 생성 10만 회 | 모두 12자, 글자는 `ABCDEFGHJKLMNPQRSTUVWXYZ23456789`만, `0 O 1 I` 0회. 각 글자 빈도 1/32 ± 통계 오차 |
| T-MAIL-31 | | 엔트로피 | log2(32^12) = 60비트 |
| T-MAIL-32 | 코드 `ABCD2345WXYZ` | 표시 | `ABCD-2345-WXYZ` |
| T-MAIL-33 | 코드 `ABCD2345WXYZ` | 입력 `abcd-2345 wxyz` | 맞음(정규화) [임시] |
| T-MAIL-34 | 확인된 이메일 계정 | (아이디 맞음, 이메일 맞음) / (아이디 맞음, 이메일 틀림) / (아이디 없음) / (미확인) | 네 경우 응답 본문·상태 코드 동일, 응답 시간 차이 ≤ 50ms [임시], 메일은 첫 경우만 |
| T-MAIL-35 | 이메일 대소문자·공백만 다름 | 요청 | 보낸다(정규화 비교) |
| T-MAIL-36 | 코드 발급 t0 | t0 + 29분 59초 사용 | 성공 |
| T-MAIL-37 | | t0 + 30분 | 거절 |
| T-MAIL-38 | 코드 C1 발급 → 60초 후 C2 발급 | C1 사용 | 거절(앞 코드 폐기) |
| T-MAIL-39 | 코드 C 사용 성공 | C 다시 사용 | 거절(한 번만) |
| T-MAIL-40 | 틀림 4회 | 5번째 틀림 → 이후 맞는 코드 | 거절(폐기) |
| T-MAIL-41 | 계정 요청 t | t + 59초 요청 | 메일 안 감, 답은 같음 |
| T-MAIL-42 | 계정 1시간 안 3회 메일 | 4번째(61분 간격 지키지 않음) | 메일 안 감 |
| T-MAIL-43 | 계정 24시간 안 5회 | 6번째 | 메일 안 감 |
| T-MAIL-44 | 한 IP 1시간 10회 | 11번째(다른 계정) | 메일 안 감, 답 같음 |
| T-MAIL-45 | 서버 오늘 500통 보냄 | 501번째 | 안 보냄, 답 같음, 경고 지표 |
| T-MAIL-46 | 재설정 성공 | 결과 | 비밀번호 변경, 모든 세션 폐기, `notify_password_changed` 1통, `trade_hold_until = now+24h`, `audit_log` 1행 |
| T-MAIL-47 | 재설정 성공 후 | 옛 비밀번호 로그인 | 실패 |
| T-MAIL-48 | 새 비밀번호가 9자 | 재설정 확정 | 거절, 코드는 소모되지 않음 [임시] |
| T-MAIL-49 | 확인된 계정 3개가 같은 주소 | 아이디 찾기 | 1통, 본문에 아이디 3개 |
| T-MAIL-50 | 같은 주소, 확인 2 + 미확인 1 | 아이디 찾기 | 본문에 확인된 2개만 |
| T-MAIL-51 | 확인된 계정 없음 | 아이디 찾기 | 0통, 답 같음 |
| T-MAIL-52 | 아이디 찾기 + 재설정 합계 | 계정 한도 1시간 3회 | 합산해서 센다 |
| T-MAIL-53 | 운영자 | 관리 화면에서 비밀번호 지정 기능 | 존재하지 않는다. "재설정 메일 다시 보내기"만, 확인된 이메일이 없으면 단추 꺼짐 |

### 14.9 메일 꺼짐·재시도

| ID | given | when | then |
|---|---|---|---|
| T-MAIL-60 | `mail.sender = null` | `/v1/features` | `recovery_mail_enabled = false` |
| T-MAIL-61 | 같음 | 가입 | 성공, 이메일 암호화 저장, 미확인, outbox에 메일 없음 또는 `dropped` [임시: 만들지 않음] |
| T-MAIL-62 | 같음 | 로그인, 원정, 강화, 거래, 결투 | 모두 정상(`IMailSender` 호출 0회) |
| T-MAIL-63 | 같음 | 재설정 요청 API 직접 호출 | 같은 답, 코드 생성 0, 나중에 켜도 보내지 않음 |
| T-MAIL-64 | 사업자가 일시 실패(Retryable) | 발송 | 1분 뒤, 5분 뒤, 15분 뒤 재시도, 4번째 실패면 `dropped`. 총 시도 4회 |
| T-MAIL-65 | 확인 코드(15분) 메일이 2회 실패 | 15분 재시도 시점(발급 21분 뒤) | 코드 만료 → 보내지 않고 `dropped` |
| T-MAIL-66 | Retryable 아님(주소 거절) | 발송 | 즉시 `dropped` |
| T-MAIL-67 | `mail_outbox` 행 | 열 확인 | 받는 주소·본문 열 없음 |
| T-MAIL-68 | outbox 행 31일 전 | 정리 | 삭제 |
| T-MAIL-69 | 사업자에 넘기는 요청 | 내용 | 받는 주소와 본문만(아이디·계정 번호 없음) |

### 14.10 저장·개인정보

| ID | given | when | then |
|---|---|---|---|
| T-DB-01 | 같은 주소로 두 계정 | `email_enc` 비교 | 다르다(행마다 nonce) |
| T-DB-02 | 계정 A의 `email_enc`를 계정 B 행에 복사 | 복호화 | 실패(AAD = account_id) [임시] |
| T-DB-03 | `alice@daum.net` | 가린 꼴 | `a***@d***.net` |
| T-DB-04 | `bob@mail.example.co.kr` | 가린 꼴 | `b***@m***.example.co.kr` [임시] |
| T-DB-05 | 운영자가 전체 주소 보기 | 결과 | `audit_log`에 `action='view_email'` 1행 |
| T-DB-06 | 쓴 코드, `used_at` 23시간 59분 전 | 정리 작업 | 남는다 |
| T-DB-07 | `used_at` 24시간 전 | 정리 | 삭제 |
| T-DB-08 | 계정 삭제 요청 t0 | t0 + 6일 23시간 | 계정 행 존재 |
| T-DB-09 | | t0 + 7일 | `account` 행 없음, `account_token` 없음, `currency_ledger`의 그 `account_id` 행은 남음(익명 번호), 이메일 암호문·HMAC 어디에도 없음 |
| T-DB-10 | `pg_dump` 결과 | 평문 이메일 검색 | 0건 |

### 14.11 멱등·트랜잭션

| ID | given | when | then |
|---|---|---|---|
| T-IDEM-01 | 강화 요청 `request_id = R` 성공 | 같은 R로 다시 | 첫 결과 그대로, `enhance_log` 1행, 재료 차감 1회 |
| T-IDEM-02 | 같은 R로 동시에 2요청 | | 하나만 실행, 둘 다 같은 결과 |
| T-IDEM-03 | 강화 트랜잭션 중 서버 강제 종료(커밋 전) | 재시작 후 같은 R | 새로 실행됨(재료 그대로였음) |
| T-IDEM-04 | 커밋 후 응답 전 종료 | 재시작 후 같은 R | 저장된 결과 반환, 결과 연출 재생(`enhance.result_recovered`) |
| T-IDEM-05 | `request` 행 31일 전 | 정리 | 삭제 |
| T-IDEM-06 | 계정 7과 3의 직접 거래 | 잠금 순서 | 3 → 7 (계정 번호 순) |
| T-IDEM-07 | 우편 `claimed_at IS NULL` | 수령 2회 동시 | 1회만 지급 |
| T-IDEM-08 | 장터 마지막 1개 | 두 사람 동시 구매 | 한 명만 성공 |
| T-IDEM-09 | `currency_ledger` | UPDATE/DELETE 시도(서버 DB 역할) | 권한 거부 [임시] |
| T-IDEM-10 | `item_stack.qty = 0` | `qty − 1` | CHECK 위반 → 트랜잭션 취소 |
| T-IDEM-11 | `currency_balance(gold, earned) = 0` | −1 | CHECK 위반 |
| T-IDEM-12 | `currency_balance(starlight, paid) = 0` | −5(환불) | 허용, −5 |

### 14.12 TLS·상태 파일

| ID | given | when | then |
|---|---|---|---|
| T-TLS-01 | Caddy | TLS 1.2 클라이언트 | 핸드셰이크 실패 |
| T-TLS-02 | | TLS 1.3 | 성공 |
| T-TLS-03 | | 평문 HTTP :80 | 연결 거부(리스너 없음) |
| T-TLS-04 | 출시 빌드 | 인증서 검증 끄기 기호 | 존재하지 않음(CI 검사) |
| T-TLS-05 | 자체 서명 인증서 서버 | 출시 클라이언트 접속 | 실패 |
| T-TLS-06 | 서명이 맞는 상태 파일 | 시작 | 그 주소 사용 |
| T-TLS-07 | payload 한 바이트 변조 | 시작 | 파일 무시 → 빌드 기본 주소 |
| T-TLS-08 | 다른 키로 서명 | 시작 | 무시 |
| T-TLS-09 | 상태 파일 못 읽음, 기본 주소 접속 가능 | 시작 | 기본 주소로 접속 |
| T-TLS-10 | 둘 다 실패 | 시작 | "오프라인 프로필로 놀기" + "다시 시도" |
| T-TLS-11 | 맥미니 꺼짐, 상태 파일에 점검 공지 | 시작 | 공지 문구 표시 |
| T-TLS-12 | `min_client_version = 1.2.0`, 클라이언트 1.1.9 | 시작 | 서버 프로필 막힘 + `download_url` 안내, 오프라인 프로필 가능 |
| T-TLS-13 | `issued_at`이 마지막으로 받은 것보다 이전 | 시작 | 무시 [임시] |
| T-TLS-14 | `route_kind = tunnel` | 자동 매치 메뉴 | 열 수 없음(웹소켓 경로) |
| T-TLS-15 | 경기 토큰 없는 UDP 패킷 1만 개 | wizard-duel | 복호화 호출 0회, 모두 버림 |
| T-TLS-16 | 이미 받은 패킷 번호 재전송 | | 버림 |
| T-TLS-17 | 공인 IP 변경 | DDNS 스크립트 | 60초 안에 레코드 갱신, TTL 60 |

### 14.13 용량 검산 (원본 7.5의 계산을 코드 상수로 고정하는 회귀 테스트)

| ID | given | when | then |
|---|---|---|---|
| T-CAP-01 | 60틱, 틱당 0.2ms | 경기당 CPU | 12ms/초 = 코어의 1.2% |
| T-CAP-02 | 60패킷/초 × 85바이트 | 한 방향 | 40,800bps ≈ 40kbps |
| T-CAP-03 | 10회/초 × (39 × 10 + 60) | 광장 1인 내림 | 36,000bps = 36kbps |
| T-CAP-04 | 40명 × 36kbps | 인스턴스 | 1,440kbps ≈ 1.5Mbps |
| T-CAP-05 | 25분 = 1,500초 | 틱 수 | 90,000틱 |
| T-CAP-06 | 90,000틱 ÷ (60 × 100) | 검증 시간 | 15초 |
| T-CAP-07 | 300명 × 1판/시간 × 15초 ÷ 3,600 | 검증 코어 | 1.25 ≈ 1.3 |
| T-CAP-08 | 500명 | 검증 코어 | 2.08 ≈ 2.1 |
| T-CAP-09 | 결투 60명 = 30경기 × 1.2% | 코어 | 0.36 ≈ 0.4 |
| T-CAP-10 | 결투 100명 = 50경기 | 코어 | 0.6 |
| T-CAP-11 | 300명 구성 | 올림 | 60 × 40kbps + 200 × 36kbps = 9.6Mbps ≈ 10Mbps |
| T-CAP-12 | 500명 구성 | 올림 | 100 × 40 + 350 × 36 = 16.6Mbps ≈ 17Mbps |
| T-CAP-13 | 300명 합계 CPU | | 0.4 + 1.3 + 0.5 = 2.2코어 (성능 코어 4개 이하) |
| T-CAP-14 | 500명 합계 CPU | | 0.6 + 2.1 + 1 = 3.7코어 |
| T-CAP-15 | 입력 기록 1시간 | 크기 | 약 150KB (online 3.2) |
| T-CAP-16 | `max_concurrent = 400`, 접속 400 | 401번째 | 대기열 화면 + [오프라인 프로필로 하기] |
| T-CAP-17 | 대기열 1명, 접속자 1명 나감 | | 대기열 첫 사람 입장 |
| T-CAP-18 | 봇 300명 부하 | 30분 | CPU·회선·검증 대기열·서버 틱 지연이 4절 "여유 있다" 범위 [임시: 서버 틱 지연 p99 < 2ms, 검증 대기 < 5분] |
| T-CAP-19 | 봇 500명 부하 | 30분 | 플레이 막힘 0, 검증 대기열만 밀림 허용 |

### 14.14 백업

| ID | given | when | then |
|---|---|---|---|
| T-BKP-01 | `archive_timeout` | 설정 확인 | 60초 |
| T-BKP-02 | 쓰기 지속 중 내장 디스크 손실 모의 | 외장 SSD의 기본 백업 + WAL로 복원 | 잃은 커밋 ≤ 마지막 1분 |
| T-BKP-03 | 집 밖 사본만으로 복원 | | 잃은 커밋 ≤ 5분 |
| T-BKP-04 | 집 밖 덤프 31개 | 정리 | 30일분만 남음 |
| T-BKP-05 | 집 밖 덤프 파일 | 평문 검사 | 암호문(헤더 외 식별 가능한 SQL 없음) |
| T-BKP-06 | 복원 연습(분기) | 다른 기계에 복원 → 서버 기동 → 원장 대조 | 대조 일치. 소요 시간 기록(목표 4시간) |
| T-BKP-07 | 백업 작업 실패 | | 개발자 알림 |
| T-BKP-08 | 외장 SSD 남은 용량 15% | | 경고 [임시 20%] |
| T-BKP-09 | 비밀값 목록 | 맥미니 밖 사본 점검표 | pepper, 이메일 암호 키, HMAC 키, 메일 API 키, 상태 파일 서명 키, 백업 암호 키 6종 모두 체크 |
| T-BKP-10 | 시즌 종료 + 90일 | 입력 기록 정리 | 상위권 외 삭제, 상위권은 남음 |

### 14.15 꺼짐 처리

| ID | given | when | then |
|---|---|---|---|
| T-DOWN-01 | 서버 프로필 판 진행 중 서버 정지 | 판 계속 → 종료 | 결과 기기 보관, 재접속 때 제출, 보상 받음 |
| T-DOWN-02 | 종료 t, 제출 t + 10분 | | 순위 반영 [임시: 10분 정각 포함] |
| T-DOWN-03 | 제출 t + 10분 1초 | | 보상만, 순위 미반영 |
| T-DOWN-04 | 결투 중 서버 정지 | 재시작 | 그 경기 `result='void'`, 점수 변동 0 |
| T-DOWN-05 | 직접 거래 커밋 전 정지 | 재시작 | 양쪽 소지품 변화 없음 |
| T-DOWN-06 | 장터 등록 `expires_at = T`, 서버가 T − 1시간부터 3시간 정지 | 재시작 | `expires_at = T + 3시간` |
| T-DOWN-07 | 정지 전에 이미 만료된 등록 | 재시작 | 변화 없음 |
| T-DOWN-08 | 일일 결투 보상, 서버가 하루 종일 꺼짐 | 다음 날 | 꺼진 날 몫 없음 |
| T-DOWN-09 | 시즌 `ends_at = E`, E − 10시간부터 2시간 정확히 정지 | 재시작 | 연장 없음 |
| T-DOWN-10 | 같은데 2시간 1초 정지 | | `ends_at = E + 2시간 1초` |
| T-DOWN-11 | 마지막 24시간 안에 1시간 + 1시간 30분 두 번 정지 | 두 번째 재시작 | 누계 2.5시간 > 2시간 → `ends_at += 2.5시간` [임시: 누계] |
| T-DOWN-12 | E − 25시간부터 3시간 정지(창 걸침) | | 창 안에 든 2시간만 셈 → 2시간이라 연장 없음 [임시] |
| T-DOWN-13 | 재설정 코드 발급 직후 서버 40분 정지 | 재시작 후 코드 사용 | 만료(서버 시각 기준 30분) |
| T-DOWN-14 | 재시작 | 순서 | DB 복구 확인 → 원장 대조 → 접속 허용. 대조 전 접속 시도는 503 |
| T-DOWN-15 | 대조 불일치 | 재시작 | 접속 보류 + 운영자 알림 [임시] |
| T-DOWN-16 | 클라이언트 재접속 | 간격 k=0..7 | 5, 10, 20, 40, 80, 120, 120, 120초 (각 ±20%) |
| T-DOWN-17 | 클라이언트 1,000개 동시 재접속 시뮬레이션 | 첫 5초 창 | 도착 시각이 4~6초에 퍼짐(한 순간 몰림 없음) |
| T-DOWN-18 | `maintenance = true`, 결투 2경기 진행 중 | | 새 접속 503, 결투는 계속 |
| T-DOWN-19 | 5분 안에 두 경기 끝남 | | 그때 종료 단계 |
| T-DOWN-20 | 5분 지나도 1경기 남음 | | 5분에 종료, 남은 경기 무효 [임시] |
| T-DOWN-21 | 정전 4분 59초 | UPS | DB 계속 |
| T-DOWN-22 | 정전 5분 초과 | UPS 신호 | DB 정상 종료 |
| T-DOWN-23 | 외부 감시가 `/healthz` 무응답 | 1분 주기 | 개발자 전화 알림 |
| T-DOWN-24 | 강화 요청 직후 정지, 서버가 커밋한 상태 | 재접속 | 결과 연출 재생, 재료 이미 차감, 두 번 강화 없음 |

### 14.16 권위·오프라인

| ID | given | when | then |
|---|---|---|---|
| T-AUTHZ-01 | 클라이언트가 강화 결과 `success`를 보냄 | 서버 | 무시, 서버 난수로 판정 |
| T-AUTHZ-02 | 클라이언트 시계 +1시간 | 장터 등록 만료 표시 | 서버 시각 기준 |
| T-AUTHZ-03 | 클라이언트가 지팡이 능력치를 보냄 | 판 출발 | 무시, 서버가 아는 값으로 `wand_snapshot` |
| T-AUTHZ-04 | 코어 버전 해시 불일치 | 순위 제출 / 결투 입장 | 거절 |
| T-AUTHZ-05 | 광장 위치가 0.1초에 1칸 이동 주장 | 서버 | 되돌림(최대 0.5칸 × 1.1) |
| T-OFF-01 | 네트워크 끊김 | 처음 실행 → 판 → 정산 → 강화 → 다시 실행 | 통과, 서버 주소로 나가는 호출 0건(상태 파일 시도 1건만, 실패 조용히) |
| T-OFF-02 | 오프라인 프로필 | `GetStoreAsync` | 유료 상품 0개, `CreateOrderAsync` 호출 0회 |
| T-OFF-03 | 오프라인 프로필 | 이메일·복구 메뉴 | 없음(설정 > 계정은 저장 파일 위치와 [저장 파일 되돌리기]만) |
| T-OFF-04 | 오프라인 저장 파일 | 서버 업로드 API | 존재하지 않음 |

### 14.17 결제 (online 9.6 목록 전부)

| ID | given | when | then |
|---|---|---|---|
| T-PAY-01 | `payments_enabled = false` | `CreateOrder` 직접 호출 | 거절, `IPaymentProvider` 호출 0회 |
| T-PAY-02 | 같음 | `ConfirmOrder` 직접 호출 | 거절 |
| T-PAY-03 | 같음 | 알림 주소 POST(서명 맞음) | 거절 |
| T-PAY-04 | 결제 표 6개가 모두 비어 있음 | 서버 전 함수 스모크 | 정상 |
| T-PAY-05 | 유료 상품 0개 | 상점 화면 | 별빛 탭·잔액·구매 버튼·안내문 0개, "준비 중" 없음 |
| T-PAY-06 | 서버 꺼짐 | 게임 시작 | 결제 관련 화면·오류·대기 0 |
| T-PAY-07 | 가짜 제공자, 별빛 잔액 ≠ 0인 계정 | 강화 확률·비용, 매치, 리그, 순위 계산 | 별빛 0 계정과 결과 동일 |
| T-PAY-08 | `origin='paid', bound=true` 꾸미기 | 직접 거래·장터 등록 | 거절 |
| T-PAY-09 | 후계자 전용 겉모습(`bound=true`, 결제 무관) | 거래 | 거절(같은 검사, 결제 꺼짐에서도) |
| T-PAY-10 | 시즌 주화·별빛 | 거래 | 거절(거래 불가 재화) |
| T-PAY-11 | FakeReceipt, 영수증 X로 지급 완료 | 다른 주문에 같은 영수증 X(같은 `provider_ref`) | 두 번째 지급 0(유일 제약) |
| T-PAY-12 | 같은 주문 `ConfirmOrder` 2회 | | 첫 결과 그대로, 지급 1회 |
| T-PAY-13 | FakeOrder, 같은 알림 2회 | | 지급 1회 |
| T-PAY-14 | FakeOrder, 승인 후 결제창 닫고 게임 종료(서버는 `authorized`) | 재접속 | 마저 지급, `granted` |
| T-PAY-15 | 주문 `created`, 30분 경과 | | `cancelled` |
| T-PAY-16 | 29분 59초에 승인 | | `authorized` → `granted` |
| T-PAY-17 | 교환 코드 사용 완료 | 같은 코드 다시 | 거절 |
| T-PAY-18 | 같은 코드 두 계정 동시 사용 | | 한 명만(행 잠금) |
| T-PAY-19 | 별빛 100(유료) 지급 후 80 사용(무상 0) | 100 환불 | 유료 잔액 −80, 별빛 사용 막힘, 골드·진행 불변 |
| T-PAY-20 | 유료 −80 상태 | 무상 별빛 50 받음 후 사용 시도 | 막힘(유료가 0 이상이 될 때까지) [임시: 무상 사용도 막음] |
| T-PAY-21 | 무상 30 + 유료 50 | 40 사용 | 무상 30 → 0, 유료 50 → 40 (무상부터) |
| T-PAY-22 | 유료 물건 환불 | | 회수, `item_ledger` −1, `entitlement.revoked_at` |
| T-PAY-23 | 같은 계정 90일 안 환불 2회 [임시] | | 결제 막힘 표시 |
| T-PAY-24 | 환불 처리 | | `audit_log`와 원장 기록 존재 |
| T-PAY-25 | 출시 구성 빌드 | DI 등록 | `NullPaymentProvider`만, `IPurchaseFlow` 구현 0개, Fakes 어셈블리 미참조 |
| T-PAY-26 | 별빛 → 골드 교환 API | | 존재하지 않음 |
| T-PAY-27 | 캐릭터·지팡이 상품 | `product` 등록 시도 | 검증 거절 [임시] |
| T-PAY-28 | 정적 분석 | 강화·매치·리그·순위 네임스페이스 | 별빛·유료 참조 0 |

### 14.18 안전

| ID | given | when | then |
|---|---|---|---|
| T-SAFE-01 | 채팅 위반 이력 0 | 위반 1~5회 | 경고 → 1일 → 7일 → 30일 → 영구 |
| T-SAFE-02 | 새 계정 기본 설정 | 귓속말 | "친구만" |
| T-SAFE-03 | | 채팅 필터 끄기 설정 | 존재하지 않음 |
| T-SAFE-04 | 운영자 지급·회수·제재 | | 각각 `audit_log` 1행 |
| T-SAFE-05 | 지난 24시간 판 시간 72,000초 | 봇 탐지 | 검토 표시(자동 제재 없음) |
| T-SAFE-06 | 접속 IP 31일 전 기록 | 정리 | 삭제(또는 NULL) |
| T-SAFE-07 | 채팅 91일 전 | 정리 | 삭제 |
| T-SAFE-08 | 신고 처리 후 1년 + 1일 | 정리 | 발췌 삭제 |
| T-SAFE-09 | 닉네임 `운영자` | 가입 | 사칭 금지어로 거절 |

### 14.19 만드는 순서

| ID | given | when | then |
|---|---|---|---|
| T-ORD-01 | 같은 입력 기록 | 두 번 재실행 | 상태 해시 동일 |
| T-ORD-02 | 같은 틱·같은 오프셋에 방향과 K | 처리 순서 | 방향 → K |
| T-ORD-03 | 3단계 완료 시점 | `IMailSender` 구현 | `NullMailSender`로 전체 계정 테스트 통과 |

---

## 15. 충돌

DECISIONS가 기준이다. "택함"은 이 명세가 따른 쪽이다.

| ID | 충돌 | 택함 | 근거 |
|---|---|---|---|
| C-01 | 이전 판의 "가입할 때 한 번 보여 주는 복구 코드(16자)"와 `account.recovery_hash` vs 이메일 복구 | **이메일 복구**. 복구 코드·`recovery_hash` 없음 | DEC 1.24 [개발자]. online 7.3·7.9, ui-assets 4.3(복구 코드 판 삭제)도 이미 반영 |
| C-02 | online 7.3 "주소를 바꾸면 옛 주소로 알림 한 통" vs "옛 주소는 그 자리에서 지운다" vs `mail_outbox`는 주소를 넣지 않는다(7.9) — 재시도할 주소가 남지 않는다 | 교체 커밋 직전 즉시 발송 시도, 재시도용 옛 주소는 프로세스 메모리에만 최대 21분 [임시] | 원문 세 문장을 모두 지키는 유일한 방법. DB에는 옛 주소가 남지 않는다 |
| C-03 | 메일 재시도 1·5·15분(누적 21분) vs 확인 코드 수명 15분 | 재시도 시점에 코드가 만료·폐기됐으면 보내지 않고 버린다 [임시] | 만료된 코드를 보내는 것은 의미가 없다. "요청을 받아 두었다가 나중에 보내지 않는다(만료가 있는 코드라서)"와 같은 취지(online 7.3) |
| C-04 | 코드는 해시만 저장(7.3) vs 실패한 메일을 1·5·15분 뒤 다시 보낸다(본문에 코드 원문 필요) | 원문 코드는 만료 시각까지 프로세스 메모리에만. 재시작되면 그 메일은 버림 [임시] | 해시만 저장 규칙을 깨지 않는다 |
| C-05 | `mail_outbox.account_id` 하나 vs [아이디 찾기] 메일은 여러 계정에 걸친다 | 가장 작은 계정 번호를 적는다 [임시] | 열 구조를 바꾸지 않음 |
| C-06 | `session`에 `expires_at` 하나 vs 접근 30분·갱신 30일 두 수명 | `expires_at` = 갱신 만료, `access_expires_at` 열 추가 [임시] | 두 수명을 모두 지키려면 필요 |
| C-07 | 신규 기기·이메일 변경·재설정의 24시간 거래 보류 vs 데이터 모델에 보관 열 없음 | `account.trade_hold_until` 추가 [임시] | 셋 다 "같은 제한"(online 7.3)이므로 한 열로 |
| C-08 | 주문 상태 그림: 만료(30분) 뒤 `cancelled` vs 제공자에서 실제로 결제 완료 알림이 늦게 옴 | 제공자 직접 확인이 성공이면 `authorized`로 되살려 지급 [임시] | "클라이언트 말은 믿지 않고 서버가 제공자에 직접 확인"(9.2)·"승인됨 주문은 마저 지급"(9.3)과 맞음. 돈을 받고 안 주는 일 방지 |
| C-09 | IP당·기기당 가입 한도, 재설정 IP 한도를 셀 기록 표가 데이터 모델에 없음 | `signup_log`, `recovery_request_log` 추가 [임시] | IP 30일 삭제(online 10)와 같이 30일 보관 |
| C-10 | 계정 삭제 시 "원장에는 익명 번호만" vs 원장은 추가만(행 수정 불가) | 원장 `account_id`에 FK를 걸지 않고 숫자를 그대로 둔다(그 번호가 익명 번호) [임시] | 원장 행을 고치지 않고 두 규칙을 다 지킨다 |
| C-11 | `account.sanction_until` vs `sanction` 표(종류별 기간) | `sanction` 표가 진실, `sanction_until`은 "접속" 제재의 캐시 [임시] | 종류가 4개라 열 하나로는 표현 불가 |
| C-12 | online 7.7 "결투 중 서버 정지 = 무효, 점수 변동 없음" vs online 4.10 "이탈 패배" | 서버 정지·점검 종료는 무효, 한쪽 연결만 끊기면 이탈 규칙 | 서버 쪽 원인은 이탈이 아니다(7.7이 상황을 명시) |
| C-13 | online 11절(10단계) vs GDD 16절(18단계, 10~17이 온라인) — GDD는 4단계에 "주간 도전", 5단계에 "서버 전체 알림"을 명시 | online 11 순서 + GDD가 덧붙인 항목을 같은 단계에 포함 | GDD 16절이 "10~17단계의 세부 순서는 online 11절과 같다"고 적음. 내용 차이 아님 |
| C-14 | online 14-1의 요청 한도 요약("계정당 1시간 3회·하루 5회") vs 7.3 본문("60초에 1회"까지) | 본문(60초 1회 포함) | 14절은 요약. GDD 확인 75번도 요약이라 같은 값 |
| C-15 | online 12-1 "판 길이가 1.3~1.6배 다르다" vs online 0절 "약 0.7배" | 충돌 아님(1/1.3 ≈ 0.77, 1/1.6 ≈ 0.63) — QA Q1에서 실측 | 기록만 |
| C-16 | 상태 파일 "443이 막힌 회선이면 다른 포트" vs Caddy 443 고정 서술 | 포트는 상태 파일의 `api_base`가 정한다 | online 7.4 본문이 이미 허용 |
| C-17 | online 7.9 `wand.lock_state`에 "결투" 값 vs 결투는 서버가 스냅숏을 쓰는 방식 | 값 유지(결투 중 거래 불가, online 5.5 "결투 중 거래 불가") | 두 문장이 같은 규칙 |
| C-18 | `email_hmac` "유일하지 않다(주소 하나에 3행까지)" vs DB 제약으로 3을 표현할 수 없음 | 가입·변경 트랜잭션에서 같은 HMAC 행을 `FOR UPDATE`로 세서 검사 | 동시 가입 경쟁은 HMAC 값으로 권고 잠금(`pg_advisory_xact_lock`) [임시] |

---

## 16. 미정 (개발자 확인 필요 + 원문이 정하지 않은 것)

### 16.1 원본 14절 "개발자 확인 필요" (기본값은 본문에 이미 적용)

| ID | 항목 | 현재 기본값 | 이 명세 영역 여부 |
|---|---|---|---|
| U-01 (14-1) | **메일 사업자와 복구 절차의 값** | 외부 트랜잭션 메일 사업자의 HTTPS API. **사업자 미정**. 확인 6자리·15분, 재설정 12자·30분·한 번만, 계정당 1시간 3회·하루 5회, 서버 하루 500통, 재설정 뒤 24시간 거래 잠금, 주소 하나에 계정 3개 | 이 명세. 사업자가 정해지기 전에는 `NullMailSender` [임시] |
| U-02 (14-2) | 시작 웨이브 선택 | 넣는다(넘긴 최고 웨이브의 70% 이하, 10 단위, 건너뛴 구간 점수 60%, 골드·드랍 없음) | 무한 웨이브 명세. 여기선 `run.start_wave` 열만 |
| U-03 (14-3) | 결투 조준 보정 최대 10도 | 넣는다. 빼면 판정 반지름 0.35 이상 | 결투 명세. QA Q6 |
| U-04 (14-4) | 결투에서 비기를 해금과 무관하게 전부 | 전부 쓴다 | 결투 명세 |
| U-05 (14-5) | **이메일 미확인 계정의 허용 범위** | 로그인·플레이·거래까지 전부, 복구만 안 됨. 대안: 확인해야 거래·장터가 열림(부계정·탈취 억제, 단 메일 정지 중 새 계정 거래 막힘). 개인정보 처리방침·수집 동의 화면 필요 여부는 법령 확인 | 이 명세. [임시] 기본값 유지. 대안을 고르면 2.6의 거래 보류 검사에 `email_verified_at IS NOT NULL` 조건 하나를 더한다(스위치 `config: trade.require_verified_email = false`) |
| U-06 (14-6) | **노출 방식** | ① 포트 포워딩 + 동적 DNS. 공인 IP 노출이 싫거나 공인 IP가 없으면 ③(월 몇 천 원). 가정용 회선 약관은 통신사 확인 | 이 명세. [임시] ① |
| U-07 (14-7) | 결제를 켠 뒤 유료 재화 용도 | 꾸미기와 편의에만 | 이 명세(10.1). [임시] 유지 |
| U-08 (14-8) | 낮은 리그 시즌 순위 보상 | 리그마다 따로, 재료에 0.4 / 0.7 곱함. 후계자와 2~10위는 후계 리그만 | 시즌 명세 |
| U-09 (14-9) | 강화석이 크게 남는 것 | 도전 1회 강화석 1개 유지, 남는 것은 장터·슬롯 새기기로 | 강화·경제 명세. QA Q3 |
| U-10 (14-10) | 지팡이 개수 제한 없음의 딸린 값 | +0 500골드, +0 장터 불가(직접 거래만), 장터 20건, +3 이하·변이·확장 없는 지팡이 일괄 떠나보내기, 감정가 10% 환급. 슬롯 낚시(첫 칸 약 28,000골드, 둘째 칸까지 약 15,000골드 쌈) 남김 | 강화·거래 명세. QA Q4 |
| U-11 (14-11) | 지팡이를 팔아도 시즌 중 리그 유지 | 올라가기만, 다음 시즌에 보유 IL로 재설정 | 리그 명세 |

### 16.2 원문이 정하지 않은 것 (이 명세의 [임시] 기본값)

| ID | 무엇 | [임시] 기본값 |
|---|---|---|
| U-20 | 틀린 로그인 대기의 정확한 식 | `n ≥ 5`일 때 `min(2^(n−5), 900)`초, 성공하면 초기화, 계정(아이디) 단위 |
| U-21 | "하루" 한도의 창 | 지난 24시간 이동 창. 서버 하루 발송 상한만 KST 0시 기준 |
| U-22 | 서버 시각의 하루·주 경계(주간 순위 월요일, 일일 결투 보상) | Asia/Seoul 00:00 |
| U-23 | 비밀번호 최대 길이 | 128자 |
| U-24 | 흔한 비밀번호 목록의 출처·비교 방식 | 공개 유출 목록 상위 10만, 대소문자 그대로 비교. 빌드 시 블룸 필터 또는 정렬 해시 목록으로 서버에 포함 |
| U-25 | 닉네임 글자 범위 세부 | 한글 완성형 음절만(자모 불가), 대소문자 무시 유일 |
| U-26 | 6자리 확인 코드 해시 방식 | HMAC-SHA256(code_key, account_id ‖ code) |
| U-27 | 같은 시간 응답 구현 | 최소 400ms 고정 + 메일은 비동기 |
| U-28 | 메일 재시도 시도 수 해석 | 첫 시도 + 재시도 3회 = 4회 |
| U-29 | 발송 실패율 알림 임계 | 1시간 10통 이상 중 `dropped` 20% 경고, 50% 긴급 |
| U-30 | 재설정 계정 한도에 "조건 불일치 요청"도 세는가 | 아이디가 존재하는 모든 요청을 센다 |
| U-31 | 아이디 찾기 한도를 어느 계정 카운터에 | 그 주소의 확인된 계정 모두에 1씩 |
| U-32 | 새 기기 판별 | 키체인에 둔 설치 무작위 값의 HMAC |
| U-33 | 24시간 보류가 장터 **구매**도 막는가 | 막지 않는다(원문 "거래와 장터 등록") |
| U-34 | 계정 삭제 취소, 삭제 대기 중 할 수 있는 것 | 로그인해 취소 가능. 대기 중 거래·장터·선물 금지 |
| U-35 | 삭제된 계정의 닉네임이 후계자 비석·역대 순위에 남는가 | 비석(영구, online 6.4)은 남긴다 — 개인정보 측면 법령 확인 필요 표시 |
| U-36 | `play_seconds` 세는 방법 | 서버 프로필 접속 중 자리 비움이 아닌 시간 |
| U-37 | 접속자 상한에 세는 대상 | 웹소켓 접속 중인 계정. 판 제출 HTTPS는 세지 않음 |
| U-38 | 입력 기록 90일 뒤 남기는 "상위권" | 각 순위표 상위 100위의 판, 후계자 후보 10명, 신고·제재가 걸린 판, 리그별 결투 상위 100위 계정의 경기 |
| U-39 | 정지 시간 측정 | `server_heartbeat` 10초 갱신, 30초 넘는 공백을 정지로 기록 |
| U-40 | 시즌 연장 "2시간 넘게"의 누계 여부 | 마지막 24시간 창 안 정지 구간의 합 |
| U-41 | 다른 시간 규칙(원정 14일 정리, 거래 잠금 7일, 우편 만료)의 정지 보정 | 보정하지 않는다 |
| U-42 | 계획 점검 5분 뒤 남은 결투 | 무효(점수 변동 없음) |
| U-43 | 재접속 흩기 | ±20% 균등 |
| U-44 | 상태 파일 되돌림 방지·키 교체 | `issued_at` 단조 증가, 공개 키 2개 내장 |
| U-45 | UDP 포트 번호, 패킷 앞머리 형식 | 47000, 평문 `match_token_id` 8바이트 + 패킷 번호 |
| U-46 | Caddy IP당 요청 제한 값 | 초당 20, 순간 40 |
| U-47 | 백업 시각, 덤프 암호 방식 | 03:30 기본 백업, 04:30 집 밖 덤프(KST), AES-256-GCM 또는 age |
| U-48 | 감시 알림 임계 | 9.4 표 |
| U-49 | 대조 불일치 시 재시작 동작 | 접속 허용 보류 + 알림(운영자가 판단해 강제 개방 스위치) |
| U-50 | 코어 3개 이상 이전 버전의 판 | 무효 처리하지 않고 운영자 검토 |
| U-51 | 환불 반복 기준 | 90일 안 2회 |
| U-52 | 유료 잔액 음수일 때 무상 별빛 사용 | 막는다("별빛 사용을 막는다"를 별빛 전체로 해석) |
| U-53 | 교환 코드 형식 | 32자 알파벳 16자, SHA-256 해시 저장 |
| U-54 | 채팅 제재 단계가 내려가는 조건 | 내려가지 않는다 |
| U-55 | 봇 "하루 20시간" 측정 | 지난 24시간 판 시간 합 ≥ 72,000초 |
| U-56 | 광장 이동 검사 허용 오차 | 10% |
| U-57 | 거래 보류·미확인 이메일 대안 스위치 | `config: trade.require_verified_email = false` (U-05) |
| U-58 | 개인정보 처리방침·수집 동의 화면 | 법령 확인 전까지 가입 판에 "이메일은 비밀번호 복구와 보안 알림에만 쓴다" 문구(ui-assets 4.3) 유지, 동의 체크 칸 자리를 데이터로만 정의(`signup.consent_required = false`) |

---

## 17. 시각/소리 연결점 (이벤트 이름만, 구현 안 함)

클라이언트 이벤트 버스에 아래 이름으로 발행한다. 그림·이펙트·소리는 나중에 이 이름에 꽂는다. 괄호는 관련 화면·부품 식별자(`ui-assets.md`).

**접속·서버 상태**
- `net.status_file.loaded`, `net.status_file.invalid_signature`, `net.status_file.unreachable`
- `net.server.connected`, `net.server.disconnected`, `net.server.maintenance` (I-F5 연결됨/끊김/점검 중)
- `net.server.reconnect_attempt`, `net.server.reconnected`
- `net.admission.queued`, `net.admission.position_changed`, `net.admission.admitted` (접속 대기 판)
- `net.client.version_too_old` (새 빌드 안내)
- `net.offline.suggested` ("오프라인 프로필로 놀기")
- `notice.maintenance.shown`, `notice.operator.shown` (운영 공지 판)
- `session.kicked` (다른 기기 접속)

**계정·인증**
- `auth.login.started`, `auth.login.succeeded`, `auth.login.failed`, `auth.login.throttled`
- `auth.signup.succeeded`, `auth.signup.field_invalid`
- `auth.new_device.trade_hold` (24시간 거래 보류 안내)
- `auth.password.changed`
- `auth.email.code_sent`, `auth.email.code_wrong`, `auth.email.code_expired`, `auth.email.verified`, `auth.email.resend_cooldown_tick` (K-36 남은 시간)
- `auth.email.unverified_badge` (K-46 "이메일 미확인")
- `auth.email.changed`
- `auth.recovery.mail_off` (복구 메일 꺼짐 안내 판)
- `auth.recovery.requested` (같은 답 표시)
- `auth.recovery.reset_succeeded` ("모든 기기에서 로그아웃되었다…")
- `auth.recovery.reset_failed`
- `auth.account.deletion_requested`, `auth.account.deletion_cancelled`
- `security.refresh_reuse_detected` (강제 로그아웃 안내)

**꺼짐·복구 연출**
- `run.submit.queued_offline` (기기에 보관), `run.submit.sent`, `run.submit.late_unranked` (10분 넘김)
- `duel.voided_by_server` (서버 정지로 무효)
- `trade.cancelled_by_disconnect`
- `enhance.result_recovered` (재접속 뒤 강화 결과 연출 재생)
- `market.listing.extended_by_downtime`
- `season.end_extended`

**운영·알림(서버 전체 시스템 채널 — 실제 연출은 effects/ui 명세)**
- `system.broadcast.enhance_10plus`, `system.broadcast.enhance_13`
- `system.broadcast.notice`

**결제 (출시 때 발행되지 않음. 유료 상품 0개면 화면 없음)**
- `store.paid_tab.visible` (유료 상품 ≥ 1일 때만)
- `purchase.order.created`, `purchase.checkout.opened`, `purchase.order.granted`, `purchase.order.cancelled`, `purchase.order.refunded`
- `purchase.redeem.succeeded`, `purchase.redeem.failed`
- `currency.starlight.blocked_negative`

**결투 네트워크 표시 (online 4.12 연결, 결투 명세와 공유)**
- `duel.net.margin_level` (초록 15ms 이상 / 노랑 / 호박색 5ms 미만 — 빨강 금지)
- `duel.net.input_dropped` (그 틱 작은 표시)
- `duel.net.recommend_wired` (대기열 "유선 연결 권장")

**관리 화면(그림 불필요, 알림 소리만 가능)**
- `admin.alert.backup_failed`, `admin.alert.disk_low`, `admin.alert.mail_failure_rate`, `admin.alert.verify_queue_backlog`, `admin.alert.reconcile_mismatch`, `admin.alert.server_jitter`
