namespace Wizard.Solo.UI
{
    /// <summary>정산 묶음 등록(00 문서 2.1·8절): Settlement(기록, 귀환 가방, 가져가는 것).</summary>
    public static partial class ScreenRegistry
    {
        static partial void RegisterSettlement(IScreenManager m, GameContext ctx)
        {
            // 판 끝 요약용 표시 통계 수집기: 부팅 때 세션에 한 번 붙고, 정산 화면이 그 판의 통계를 읽는다(시뮬레이션 상태를 더하지 않는다).
            var stats = new View.Stats.RunStatsCollector();
            var tuning = Screens.Settlement.RecapTuning.From(ctx != null ? ctx.Content : null);
            stats.PatternWindowTicks = SimConst.Ticks(tuning.BossPatternWindowSeconds);
            if (ctx != null) stats.Attach(ctx.Session);
            m.Register(ScreenId.Settlement, () => new Screens.Settlement.SettlementScreen(stats, tuning));
        }
    }
}
