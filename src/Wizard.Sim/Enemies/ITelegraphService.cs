namespace Wizard.Sim.Enemies
{
    // CONTRACTS 3.6
    public interface ITelegraphService
    {
        int Request(in TelegraphRequest request);         // 요청 번호. 허가는 8단계에서(상한, R5)
        bool IsGranted(int requestId, out int zoneId);
        void Cancel(int zoneOrRequestId);
    }
}
