namespace Wizard.Server.Tests.Infra
{
    // 시험용 강화 결과 꾸러미(request.result 에 jsonb 로 저장된다)
    public sealed class ProbeEnhanceResult
    {
        public int Roll { get; set; }
        public long GoldAfter { get; set; }
        public string Outcome { get; set; } = "";
    }
}
