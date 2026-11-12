using System.Threading.Tasks;

namespace Wizard.Server.Tests.Infra
{
    // 이 작업(M10-DB-01)의 코드가 쓰지만 다른 작업의 마이그레이션이 만드는 표의 시험용 대역.
    //   account   — M10-ACC-01(0010). IRowLocks.LockAccountsAsync 는 id 만 본다
    //   item_stack, wand — M10-INV-01(0020). 열은 spec 12 8.2절 DDL + CONTRACTS 12.1절 그대로
    // 그 표가 이미 있으면(진짜 마이그레이션이 들어온 뒤) 아무것도 하지 않는다. 진짜 표가 대역과 다르면 이 작업의 시험이 그것을 잡는다.
    // 대역에도 진짜 표와 같은 원칙으로 wizard_app 의 권한을 준다.
    public static class StandInTables
    {
        private const string Sql = @"
DO $$
BEGIN
  IF to_regclass('public.account') IS NULL THEN
    CREATE TABLE account (id bigint PRIMARY KEY);
    GRANT SELECT, UPDATE ON account TO wizard_app;
  END IF;
  IF to_regclass('public.item_stack') IS NULL THEN
    CREATE TABLE item_stack (
      account_id   bigint NOT NULL,
      item_code    text NOT NULL,
      qty          bigint NOT NULL DEFAULT 0 CHECK (qty >= 0),
      qty_pending  bigint NOT NULL DEFAULT 0 CHECK (qty_pending >= 0),
      qty_socketed bigint NOT NULL DEFAULT 0 CHECK (qty_socketed >= 0),
      qty_bound    bigint NOT NULL DEFAULT 0 CHECK (qty_bound >= 0),
      PRIMARY KEY (account_id, item_code)
    );
    GRANT SELECT, INSERT, UPDATE ON item_stack TO wizard_app;
  END IF;
  IF to_regclass('public.wand') IS NULL THEN
    CREATE TABLE wand (
      id                 bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
      owner_id           bigint NOT NULL,
      character          smallint NOT NULL CHECK (character BETWEEN 1 AND 5),
      stage              smallint NOT NULL CHECK (stage BETWEEN 0 AND 13),
      floor_peak         smallint NOT NULL CHECK (floor_peak BETWEEN 0 AND 13),
      record_peak        smallint NOT NULL CHECK (record_peak BETWEEN 0 AND 13),
      drop_count         integer NOT NULL DEFAULT 0,
      aura               integer NOT NULL DEFAULT 0,
      slots_extra        smallint NOT NULL DEFAULT 0 CHECK (slots_extra BETWEEN 0 AND 3),
      carve_bonus        integer NOT NULL DEFAULT 0,
      mutations          jsonb NOT NULL DEFAULT '[]',
      pending_offer      jsonb NULL,
      loadout            jsonb NOT NULL,
      skin_id            text NULL,
      show_scars         boolean NOT NULL DEFAULT false,
      origin             text NOT NULL CHECK (origin IN ('start','shop','trade')),
      owner_count        integer NOT NULL DEFAULT 1,
      trade_locked_until timestamptz NULL,
      lock_state         text NOT NULL DEFAULT 'none'
                         CHECK (lock_state IN ('none','expedition','duel','market','mail','trade')),
      pending_run_id     uuid NULL,
      debt_locked        boolean NOT NULL DEFAULT false,
      acquired_at        timestamptz NOT NULL DEFAULT now(),
      version            integer NOT NULL DEFAULT 0
    );
    GRANT SELECT, INSERT, UPDATE ON wand TO wizard_app;
  END IF;
END
$$;";

        public static Task EnsureAsync(PgTestDatabase db) => db.ExecAsync(Sql);
    }
}
