namespace PulseCRM.Scoring.Algorithm;

public record ScoringInput(
    double Value,
    int StageOrder,
    int MaxStageOrder,
    int DaysInStage,
    int ActivityCount30d,
    double MaxTenantValue
);

public record ScoringResult(int Score, Dictionary<string, double> Factors);

public static class LeadScoringAlgorithm
{
    public static ScoringResult Calculate(ScoringInput input)
    {
        var stageFactor   = input.MaxStageOrder > 0 ? (double)input.StageOrder / input.MaxStageOrder : 0;
        var valueFactor   = input.MaxTenantValue > 0 ? Math.Log(1 + input.Value) / Math.Log(1 + input.MaxTenantValue) : 0;
        var engageFactor  = Math.Min(1.0, input.ActivityCount30d / 10.0);
        var recencyFactor = RecencyDecay(input.DaysInStage);

        var raw = 35 * stageFactor
                + 25 * valueFactor
                + 20 * engageFactor
                + 20 * recencyFactor;

        var score = (int)Math.Clamp(Math.Round(raw), 0, 100);

        var factors = new Dictionary<string, double>
        {
            ["stageProgress"]    = Math.Round(stageFactor * 35, 2),
            ["dealValue"]        = Math.Round(valueFactor * 25, 2),
            ["activityCount"]    = Math.Round(engageFactor * 20, 2),
            ["recency"]          = Math.Round(recencyFactor * 20, 2)
        };

        return new ScoringResult(score, factors);
    }

    private static double RecencyDecay(int daysInStage)
    {
        // Exponential decay — fresh deal scores higher
        return Math.Exp(-0.05 * daysInStage);
    }
}
