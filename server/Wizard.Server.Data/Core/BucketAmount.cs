namespace Wizard.Server.Data
{
    // currency_balance 의 (bucket, amount) 한 줄(Dapper 대응용)
    internal sealed class BucketAmount
    {
        public string Bucket { get; set; } = "";
        public long Amount { get; set; }
    }
}
