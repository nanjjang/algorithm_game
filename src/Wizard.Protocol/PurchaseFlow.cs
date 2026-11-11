namespace Wizard.Protocol
{
    // CONTRACTS 6.5. 서버의 IPaymentProvider 도 같은 열거형을 쓴다
    public enum PurchaseFlow : byte
    {
        ClientReceipt = 0,
        ServerOrder = 1,
        RedeemCode = 2,
    }
}
