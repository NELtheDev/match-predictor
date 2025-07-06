using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Domain.Models;

namespace MatchPredictor.Infrastructure.Services;

public class ProbabilityCalculator : IProbabilityCalculator
{
    public double CalculateBttsProbability(MatchData match)
    {
        if (match.OverTwoGoals == 0 || match.OverThreeGoals == 0)
        {
            return Math.Abs(match.HomeWin - match.AwayWin) < PredictionThresholds.BalancedMatchDiff 
                ? (match.OverTwoGoals + match.OverThreeGoals) / 2.0 
                : 0;
        }

        var baseScore = (match.OverTwoGoals + match.OverThreeGoals) / 2.0;
        var balanceBonus = Math.Abs(match.HomeWin - match.AwayWin) < PredictionThresholds.BalancedMatchDiff ? 0.05 : 0;
        
        return baseScore + balanceBonus;
    }

    public double CalculateOverTwoGoalsProbability(MatchData match)
    {
        var over2Weight = match.OverTwoGoals >= PredictionThresholds.Over2 ? 0.6 : 0;
        var over3Weight = match.OverThreeGoals >= PredictionThresholds.Over3 ? 0.4 : 0;
        
        return match.OverTwoGoals == 0 || match.OverThreeGoals == 0 ?
            over2Weight + over3Weight + PredictionThresholds.OverGoalsForControl : over2Weight + over3Weight;
    }
    
    public double CalculateDrawProbability(MatchData match)
    {
        var drawWeight = match.Draw >= PredictionThresholds.DrawProb ? 0.5 : 0;
        var homeWinWeight = match.HomeWin <= PredictionThresholds.WinCap ? 0.25 : 0;
        var awayWinWeight = match.AwayWin <= PredictionThresholds.WinCap ? 0.25 : 0;
        var under2Weight = match.UnderTwoGoals >= PredictionThresholds.Under2 ? 0.1 : 0;
        
        return drawWeight + homeWinWeight + awayWinWeight + under2Weight;
    }
    
    public bool IsStrongHomeWin(MatchData match)
    {
        if (match.HomeWin < PredictionThresholds.HomeWinStrong)
            return false;

        var score = match.HomeWin;
        
        if (match.OverTwoGoals >= PredictionThresholds.OverGoalsForWin)
            score += 0.1;
        else if (match.UnderTwoGoals <= PredictionThresholds.UnderGoalsForControl)
            score += 0.05;

        return score >= 0.7;
    }
    
    public bool IsStrongAwayWin(MatchData match)
    {
        if (match.AwayWin < PredictionThresholds.AwayWinStrong)
            return false;

        var score = match.AwayWin;
        
        if (match.OverTwoGoals >= PredictionThresholds.OverGoalsForWin)
            score += 0.1;
        else if (match.UnderTwoGoals <= PredictionThresholds.UnderGoalsForControl)
            score += 0.05;

        return score >= 0.72;
    }
}