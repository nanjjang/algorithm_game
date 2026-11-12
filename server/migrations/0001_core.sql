-- 0001_core.sql — 서버 DB의 바탕: 멱등 request, 재화 잔액과 두 원장, config, audit_log, 정지 시간 기록.
-- 근거: spec 12 1.3절·8.2절·8.3절·6.2절, CONTRACTS 7.2절·12.1~12.3절, DECISIONS-TECH T-24.
--
-- 적용 규칙(MigrationRunner, server/Wizard.Server.Data/Core/MigrationRunner.cs):
--   - 소유자 역할로 한 트랜잭션 안에서 돈다. 이 파일에 BEGIN/COMMIT 을 쓰지 않는다.
--   - 적용된 뒤에는 고치지 않는다. 체크섬이 바뀌면 실행기가 거절한다. 바꿀 것은 새 번호의 파일로 더한다.
--
-- 권한(T-IDEM-09): 서버 프로세스는 소유자가 아니라 wizard_app 역할의 권한으로 돈다(로그인 역할을 wizard_app 의 구성원으로 두거나
-- UnitOfWork 가 SET LOCAL ROLE wizard_app 을 한다). 표마다 필요한 권한을 아래에서 명시적으로 준다. 기본 권한(ALTER DEFAULT PRIVILEGES)은
-- 쓰지 않는다: 뒤 마이그레이션이 GRANT 를 빠뜨리면 권한 거부로 바로 드러나고, REVOKE 를 빠뜨려 조용히 열리는 일이 없다.

-- ===== 서버 DB 역할 =====
-- 역할은 클러스터 전체의 것이다. 이미 있으면 그대로 둔다(같은 클러스터의 여러 DB, 동시에 도는 테스트 DB).
DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'wizard_app') THEN
    CREATE ROLE wizard_app NOLOGIN;
  END IF;
EXCEPTION WHEN duplicate_object OR unique_violation THEN
  NULL;   -- 다른 트랜잭션이 막 만들었다
END
$$;

-- ===== 멱등 (spec 12 1.3절, CONTRACTS 7.2절·12.1절) =====
-- IIdempotency.RunAsync 가 이 행을 먼저 INSERT … ON CONFLICT DO NOTHING 한다. result 가 NULL 인 행은 진행 중인 트랜잭션 안에만 있다.
CREATE TABLE request (
  account_id  bigint NOT NULL,
  request_id  uuid NOT NULL,
  kind        text NOT NULL,
  fingerprint bytea NOT NULL,                  -- 요청 본문 정규 JSON 의 SHA-256(kind, 경로 인자 포함)
  result      jsonb NULL,                      -- NULL = 진행 중
  at          timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (account_id, request_id)
);  -- 30일 보관(8.4절). 정리는 at 기준
CREATE INDEX request_at ON request (at);

-- ===== 재화와 원장 (spec 12 8.2절·10.1절) =====
-- amount 는 확정분, amount_pending 은 검증 대기분이다. 원장의 pending 줄은 amount_pending 을, 나머지 줄은 amount 를 바꾼다 [임시].
-- 대조(9.5절): 구분마다 SUM(delta WHERE NOT pending) = amount, SUM(delta WHERE pending) = amount_pending.
CREATE TABLE currency_balance (
  account_id     bigint NOT NULL,
  currency       text NOT NULL CHECK (currency IN ('gold','season_coin','starlight')),
  bucket         text NOT NULL CHECK (bucket IN ('earned','paid','free')),
  amount         bigint NOT NULL,
  amount_pending bigint NOT NULL DEFAULT 0 CHECK (amount_pending >= 0),
  PRIMARY KEY (account_id, currency, bucket),
  CHECK (amount >= 0 OR bucket = 'paid')       -- 유료 구분만 환불 때 음수 허용(10.6절, T-IDEM-11·12)
);

CREATE TABLE currency_ledger (                  -- 재화 원장은 이것 하나. 추가만
  id            bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id    bigint NOT NULL,                -- FK 없음: 계정 삭제 뒤 익명 번호로 남는다
  currency      text NOT NULL CHECK (currency IN ('gold','season_coin','starlight')),
  bucket        text NOT NULL CHECK (bucket IN ('earned','paid','free')),
  delta         bigint NOT NULL CHECK (delta <> 0),   -- [임시] 0 줄은 남기지 않는다
  balance_after bigint NOT NULL,                -- pending 줄이면 amount_pending, 아니면 amount 의 바뀐 뒤 값
  pending       boolean NOT NULL DEFAULT false,
  reason        text NOT NULL,                  -- LedgerReason 의 snake_case
  ref_type      text NULL,
  ref_id        text NULL,
  request_id    uuid NULL,
  at            timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX currency_ledger_account ON currency_ledger (account_id, currency, bucket);   -- [임시] 대조
CREATE INDEX currency_ledger_ref ON currency_ledger (ref_type, ref_id);                   -- [임시] 판 회수

CREATE TABLE item_ledger (                      -- 물건 원장은 이것 하나. 추가만
  id         bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  account_id bigint NOT NULL,
  item_ref   text NOT NULL CHECK (item_ref ~ '^(stack:.+|wand:[0-9]+|inst:[0-9]+)$'),   -- [임시] 8.2절의 세 꼴
  delta      bigint NOT NULL CHECK (delta <> 0),
  pending    boolean NOT NULL DEFAULT false,
  reason     text NOT NULL,
  ref_type   text NULL,
  ref_id     text NULL,
  request_id uuid NULL,
  at         timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX item_ledger_account ON item_ledger (account_id, item_ref);   -- [임시] 대조
CREATE INDEX item_ledger_ref ON item_ledger (ref_type, ref_id);           -- [임시] 판 회수

-- 추가만(12.3절): wizard_app 에는 UPDATE/DELETE 권한을 주지 않는다(아래 GRANT). 소유자가 실수로 고치는 것도 트리거가 막는다.
CREATE FUNCTION ledger_append_only() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  RAISE EXCEPTION 'ledger table % is append-only (%)', TG_TABLE_NAME, TG_OP USING ERRCODE = 'insufficient_privilege';
END
$$;
CREATE TRIGGER currency_ledger_no_change BEFORE UPDATE OR DELETE ON currency_ledger
  FOR EACH ROW EXECUTE FUNCTION ledger_append_only();
CREATE TRIGGER currency_ledger_no_truncate BEFORE TRUNCATE ON currency_ledger
  FOR EACH STATEMENT EXECUTE FUNCTION ledger_append_only();
CREATE TRIGGER item_ledger_no_change BEFORE UPDATE OR DELETE ON item_ledger
  FOR EACH ROW EXECUTE FUNCTION ledger_append_only();
CREATE TRIGGER item_ledger_no_truncate BEFORE TRUNCATE ON item_ledger
  FOR EACH STATEMENT EXECUTE FUNCTION ledger_append_only();

-- ===== 운영 (spec 12 8.2절·8.3절·6.2절) =====
CREATE TABLE config ( key text PRIMARY KEY, value jsonb NOT NULL, updated_at timestamptz NOT NULL );

CREATE TABLE audit_log (
  id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  actor text NOT NULL,                        -- 'operator:<name>' | 'system' [임시]
  action text NOT NULL, target text NOT NULL,
  before jsonb NULL, after jsonb NULL, at timestamptz NOT NULL DEFAULT now()
);  -- 운영자 행동 전부. 혼자 운영해도 남긴다(online 10)

-- [임시] 6.2 정지 시간
CREATE TABLE server_heartbeat ( id smallint PRIMARY KEY DEFAULT 1 CHECK (id = 1), at timestamptz NOT NULL );
CREATE TABLE server_downtime  ( id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                "from" timestamptz NOT NULL, until timestamptz NOT NULL,
                                applied_season_id bigint NULL );

-- ===== wizard_app 의 권한 =====
GRANT SELECT, INSERT, UPDATE, DELETE ON request TO wizard_app;        -- DELETE 는 30일 정리
GRANT SELECT, INSERT, UPDATE ON currency_balance TO wizard_app;       -- ILedger 만 쓴다. 잔액 행은 지우지 않는다
GRANT SELECT, INSERT ON currency_ledger, item_ledger TO wizard_app;   -- 추가만(T-IDEM-09)
GRANT SELECT ON config TO wizard_app;                                 -- 바꾸는 쪽은 관리 화면(그 작업이 권한을 정한다)
GRANT SELECT, INSERT ON audit_log TO wizard_app;                      -- 감사 기록도 추가만
GRANT SELECT, INSERT, UPDATE ON server_heartbeat, server_downtime TO wizard_app;
