-- 0010_accounts.sql — 계정과 인증의 표: account, account_token, mail_outbox, session, session_refresh_used, device,
-- login_attempt, signup_log, recovery_request_log, account_settings, sanction.
-- 근거: spec 12 2절(계정과 인증)·8.1절(열거값)·8.2절(DDL)·8.4절(보관 기간), CONTRACTS 12.1~12.2절.
--
-- 적용 규칙은 0001_core.sql 과 같다: 소유자 역할로 한 트랜잭션 안에서 돈다(BEGIN/COMMIT 을 쓰지 않는다). 적용된 뒤에는 고치지 않는다.
-- 열은 spec 12 8.2절 DDL 그대로다. 이 파일이 더한 것은 `-- [임시]` 를 단 CHECK 와 색인뿐이고 표의 모양(열, 형식, NULL 여부)은 바꾸지 않는다.
--
-- 개인정보(spec 12 2.14절): 이메일은 암호문(email_enc, new_email_enc: AES-256-GCM)과 찾기용 HMAC(email_hmac)만 둔다.
-- 비밀번호는 Argon2id 해시(pw_hash)와 매개변수(pw_params)만 둔다. 평문 이메일·비밀번호 열은 없다.
-- 토큰과 코드도 해시만 둔다(token_hash, access_hash, refresh_hash). 기기는 HMAC(device_hash)만 둔다(2.6절).

-- ===== 계정 (spec 12 8.2절, CONTRACTS 12.1절 play_seconds) =====
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
-- 주소 하나에 3계정: 가입·변경 트랜잭션에서 검사한다(DB 제약 아님). 동시 가입이 셈을 같이 지나치지 않도록
-- 저장소(Accounts/PgAccountStore)가 그 HMAC 의 트랜잭션 권고 잠금을 먼저 잡고 센다.
CREATE INDEX account_deletion_due ON account (deletion_requested_at) WHERE deletion_requested_at IS NOT NULL;   -- [임시] 7일 삭제 배치

-- ===== 확인·재설정·변경 코드 (spec 12 2.9~2.11절) =====
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

-- ===== 메일 발송 대기열 (spec 12 2.8절) =====
CREATE TABLE mail_outbox (
  id           bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  kind         text NOT NULL                    -- mail_kind (8.1절)
               CHECK (kind IN ('verify_email','reset_password','find_login_ids','notify_password_changed','notify_email_changed')),  -- [임시] CONTRACTS 12절: 열거값은 text + CHECK
  account_id   bigint NULL,                     -- 주소·본문은 넣지 않는다
  status       text NOT NULL CHECK (status IN ('queued','sent','failed','dropped')),
  tries        smallint NOT NULL DEFAULT 0,
  provider_ref text NULL,
  created_at   timestamptz NOT NULL DEFAULT now(),
  sent_at      timestamptz NULL,
  next_try_at  timestamptz NULL,                -- [임시]
  token_id     bigint NULL                      -- [임시] 어떤 코드의 메일인지(만료 시 버림 판단)
);
-- 30일 뒤 삭제. 하루 발송 수 = count(*) WHERE sent_at >= 오늘 0시(KST [임시]).
-- 상태는 8.1절의 넷뿐이다(DECISIONS-TECH T-56): 배달 확인은 sent 그대로, 사업자의 반송·거절 사건은 sent → dropped(sent_at 은 남는다).
-- 그래서 하루 발송 수는 status 가 아니라 sent_at 으로 센다(반송이 한도를 돌려주지 않는다. 반송이 없으면 spec 의 식과 같다).
CREATE INDEX mail_outbox_due ON mail_outbox (next_try_at) WHERE status IN ('queued','failed');   -- [임시] 발송 작업이 꺼낼 행
CREATE INDEX mail_outbox_sent_at ON mail_outbox (sent_at) WHERE sent_at IS NOT NULL;           -- [임시] 하루 상한
CREATE INDEX mail_outbox_provider_ref ON mail_outbox (provider_ref) WHERE provider_ref IS NOT NULL;   -- [임시] 반송 사건을 줄에 잇는다(T-56)
CREATE INDEX mail_outbox_bounced ON mail_outbox (account_id) WHERE status = 'dropped' AND sent_at IS NOT NULL;   -- [임시] 반송된 주소(T-56)
CREATE INDEX mail_outbox_created_at ON mail_outbox (created_at);                               -- [임시] 실패율 감시, 30일 정리

-- ===== 세션과 토큰 (spec 12 2.3절) =====
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
CREATE INDEX session_account_active ON session (account_id) WHERE revoked_at IS NULL;   -- [임시] 모든 기기 폐기
CREATE INDEX session_device_active ON session (device_id) WHERE revoked_at IS NULL;     -- [임시] 재사용 탐지 때 그 기기 전부 폐기
CREATE INDEX session_family ON session (family_id);                                      -- [임시]
CREATE INDEX session_expires_at ON session (expires_at);                                 -- [임시] 정리

CREATE TABLE session_refresh_used (            -- [임시] 재사용 탐지
  refresh_hash bytea PRIMARY KEY, family_id uuid NOT NULL,
  account_id bigint NOT NULL, device_id bigint NOT NULL, at timestamptz NOT NULL
);  -- 30일 + 여유 뒤 삭제
CREATE INDEX session_refresh_used_at ON session_refresh_used (at);   -- [임시] 정리

-- ===== 기기 (spec 12 2.5~2.6절) =====
CREATE TABLE device (
  id          bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id  bigint NOT NULL REFERENCES account(id) ON DELETE CASCADE,
  device_hash bytea NOT NULL,
  first_seen  timestamptz NOT NULL,
  last_seen   timestamptz NOT NULL,
  UNIQUE (account_id, device_hash)
);
CREATE INDEX device_by_hash ON device (device_hash);   -- 기기당 계정 수, 기기당 자동 매치 3계정

-- ===== 남용 방지 기록 (spec 12 2.4·2.5·2.11·2.12절) =====
CREATE TABLE login_attempt (
  login_id text NOT NULL, ip inet NOT NULL, at timestamptz NOT NULL, ok boolean NOT NULL
);
CREATE INDEX login_attempt_by_id ON login_attempt (login_id, at DESC);
CREATE INDEX login_attempt_by_ip ON login_attempt (ip, at DESC);
CREATE INDEX login_attempt_at ON login_attempt (at);   -- [임시] 30일 정리
-- 30일 뒤 삭제

CREATE TABLE signup_log (                      -- [임시] IP·기기당 하루 3계정 검사용
  ip inet NOT NULL, device_hash bytea NOT NULL, at timestamptz NOT NULL
);  -- 30일 뒤 삭제
CREATE INDEX signup_log_by_ip ON signup_log (ip, at DESC);                     -- [임시]
CREATE INDEX signup_log_by_device ON signup_log (device_hash, at DESC);        -- [임시]
CREATE INDEX signup_log_at ON signup_log (at);                                 -- [임시] 30일 정리

CREATE TABLE recovery_request_log (            -- [임시] 재설정·아이디 찾기 한도(계정·IP) 검사용
  account_id bigint NULL, ip inet NOT NULL,
  kind text NOT NULL CHECK (kind IN ('reset_password','find_login_ids')),   -- [임시] 2.11절 재설정, 2.12절 아이디 찾기
  at timestamptz NOT NULL
);  -- 30일 뒤 삭제
CREATE INDEX recovery_request_log_by_account ON recovery_request_log (account_id, at DESC) WHERE account_id IS NOT NULL;   -- [임시]
CREATE INDEX recovery_request_log_by_ip ON recovery_request_log (ip, at DESC);                                            -- [임시]
CREATE INDEX recovery_request_log_at ON recovery_request_log (at);                                                        -- [임시] 30일 정리

-- ===== 설정 동기화 (CONTRACTS 12.2절, spec 13 13절) =====
-- 설정과 키 배치는 두 프로필이 같이 쓰고 서버 프로필은 계정에도 동기화한다. updated_at 은 서버 시각이다(클라이언트 시각을 믿지 않는다).
CREATE TABLE account_settings (
  account_id bigint PRIMARY KEY REFERENCES account(id) ON DELETE CASCADE,
  settings   jsonb NOT NULL,
  updated_at timestamptz NOT NULL
);

-- ===== 제재 (spec 12 8.2절 운영) =====
CREATE TABLE sanction (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, account_id bigint NOT NULL,
  kind text NOT NULL CHECK (kind IN ('chat','trade','duel','login')),
  reason text NOT NULL, "from" timestamptz NOT NULL, until timestamptz NULL,  -- NULL = 영구
  by text NOT NULL
);
CREATE INDEX sanction_by_account ON sanction (account_id, kind);   -- [임시] 유효 제재 조회

-- ===== wizard_app 의 권한 (0001_core.sql 의 원칙: 표마다 필요한 것만 명시적으로 준다) =====
GRANT SELECT, INSERT, UPDATE, DELETE ON account TO wizard_app;               -- DELETE 는 삭제 요청 7일 뒤(2.7절)
GRANT USAGE ON SEQUENCE account_id_seq TO wizard_app;                         -- 가입은 번호를 먼저 받는다(이메일 암호문의 AAD = 계정 번호, 2.14절)
GRANT SELECT, INSERT, UPDATE, DELETE ON account_token TO wizard_app;         -- DELETE 는 24시간 정리
GRANT SELECT, INSERT, UPDATE, DELETE ON mail_outbox TO wizard_app;           -- DELETE 는 30일 정리
GRANT SELECT, INSERT, UPDATE, DELETE ON session TO wizard_app;               -- DELETE 는 만료 정리
GRANT SELECT, INSERT, DELETE ON session_refresh_used TO wizard_app;          -- 쓴 갱신 토큰 기록은 고치지 않는다
GRANT SELECT, INSERT, UPDATE, DELETE ON device TO wizard_app;                -- UPDATE 는 last_seen
GRANT SELECT, INSERT, DELETE ON login_attempt, signup_log TO wizard_app;     -- 기록은 고치지 않는다. DELETE 는 30일 정리
GRANT SELECT, INSERT, DELETE ON recovery_request_log TO wizard_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON account_settings TO wizard_app;
GRANT SELECT, INSERT, UPDATE ON sanction TO wizard_app;                      -- 제재 이력은 지우지 않는다(UPDATE 는 해제 시각)
