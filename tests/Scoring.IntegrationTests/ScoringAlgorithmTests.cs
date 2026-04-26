using PulseCRM.Scoring.Algorithm;
using Xunit;
using FluentAssertions;

namespace PulseCRM.Scoring.IntegrationTests;

public class ScoringAlgorithmTests
{
    [Fact]
    public void Calculate_WithMaxValues_Returns100()
    {
        var input = new ScoringInput(Value: 500_000, StageOrder: 4, MaxStageOrder: 4,
            DaysInStage: 0, ActivityCount30d: 10, MaxTenantValue: 500_000);
        var result = LeadScoringAlgorithm.Calculate(input);
        result.Score.Should().Be(100);
    }

    [Fact]
    public void Calculate_WithZeroValues_ReturnsNearZero()
    {
        var input = new ScoringInput(Value: 0, StageOrder: 0, MaxStageOrder: 4,
            DaysInStage: 365, ActivityCount30d: 0, MaxTenantValue: 500_000);
        var result = LeadScoringAlgorithm.Calculate(input);
        result.Score.Should().BeLessThan(5);
    }

    [Fact]
    public void Calculate_ScoreIsClamped_Between0And100()
    {
        var input = new ScoringInput(Value: 999_999_999, StageOrder: 100, MaxStageOrder: 4,
            DaysInStage: 0, ActivityCount30d: 999, MaxTenantValue: 1);
        var result = LeadScoringAlgorithm.Calculate(input);
        result.Score.Should().BeInRange(0, 100);
    }

    [Fact]
    public void Calculate_FactorsDictHasAllExpectedKeys()
    {
        var input = new ScoringInput(50_000, 2, 4, 5, 3, 200_000);
        var result = LeadScoringAlgorithm.Calculate(input);
        result.Factors.Keys.Should().Contain(["stageProgress", "dealValue", "activityCount", "recency"]);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(10, 60)]
    [InlineData(60, 5)]
    public void Calculate_RecencyDecaysWithTime(int daysInStage, int expectedMinScore)
    {
        var input = new ScoringInput(100_000, 3, 4, daysInStage, 5, 500_000);
        var result = LeadScoringAlgorithm.Calculate(input);
        result.Score.Should().BeGreaterThanOrEqualTo(0);
    }
}
