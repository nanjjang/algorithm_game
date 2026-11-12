namespace Wizard.Server.Data
{
    // DB 역할 이름. 0001_core.sql 이 만든다. 서버 프로세스(wizard-api)는 이 역할의 권한으로 돈다:
    // 원장 표에는 SELECT·INSERT 만 있다(CONTRACTS 7.2절, spec 12 T-IDEM-09). 마이그레이션은 소유자 역할로 돈다.
    public static class DbRoles
    {
        public const string App = "wizard_app";
    }
}
