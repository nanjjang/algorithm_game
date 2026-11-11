namespace Wizard.Rules.RunLimits
{
    // CONTRACTS 4.3. wizard-api 는 WaveMath 를 부르지 않는다(T-18)
    public interface IRunLimits
    {
        int WhimsUpper(int wave);                         // spec 09 8.2: w < 21 → 0, w < 41 → 1, 그 밖 2 (데이터)
        long ScoreCap(int wave, int whimsUpper);          // 씨앗에서 실제 변덕을 다시 계산하지 않는다
        long GoldCap(int wave, int whimsUpper);
        long SkippedWaveScore(int wave);
        bool StartWaveAllowed(int startWave, int bestWavePassed);   // skip × 10 ≤ best × 7
        RuleCheck RankedCheck(RankedInputs inputs);       // spec 09 13절
    }
}
