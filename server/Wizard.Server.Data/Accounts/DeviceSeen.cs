namespace Wizard.Server.Data.Accounts
{
    // PgDeviceStore.TouchAsync 의 결과. IsNew = 이 계정이 처음 보는 기기(2.6절 거래 보류의 사유)
    public readonly struct DeviceSeen
    {
        public DeviceSeen(long deviceId, bool isNew)
        {
            DeviceId = deviceId;
            IsNew = isNew;
        }

        public long DeviceId { get; }

        public bool IsNew { get; }
    }
}
