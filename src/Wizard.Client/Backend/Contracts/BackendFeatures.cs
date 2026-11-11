namespace Wizard.Client
{
    // CONTRACTS 6.1. 서버: /v1/features. 오프라인: 고정값. 화면은 "지원하는가"를 묻지 않고 Kind 로 메뉴를 정한다
    public sealed class BackendFeatures
    {
        public bool RecoveryMailEnabled;
        public bool PaymentsEnabled;
        public bool SignupOpen;
        public bool Maintenance;
    }
}
