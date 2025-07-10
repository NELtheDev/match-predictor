using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Domain.Models;

namespace MatchPredictor.Infrastructure.Services;

public class ProbabilityCalculator : IProbabilityCalculator
{
    public double CalculateBttsProbability(MatchData match)
    {
        var baseScore = (match.OverTwoGoals + match.OverThreeGoals) / 2.0;
        var balanceBonus = Math.Abs(match.HomeWin - match.AwayWin) < PredictionThresholds.BalancedMatchDiff ? 0.05 : 0;
        var winAndOverBonus = match is { HomeWin: > PredictionThresholds.WinCap, 
            AwayWin: > PredictionThresholds.WinCap} &&
            (match.OverTwoGoals > PredictionThresholds.Over2 || 
             match.OverThreeGoals > PredictionThresholds.Over3) ? 0.15 : 0;
        var winDrawBonus = match.HomeWin > match.Draw && 
                           match.AwayWin > match.Draw && 
                           match.OverThreeGoals > PredictionThresholds.Over3 ? 
            0.15 : 0;
        var winEqualBonus = Equals(match.HomeWin, match.AwayWin) && 
                            (match.OverTwoGoals > PredictionThresholds.Over2 || 
                             match.OverThreeGoals > PredictionThresholds.Over3) ? 0.15 : 0;
        
        return baseScore + balanceBonus + winAndOverBonus + winEqualBonus + winDrawBonus;
    }

    public double CalculateOverTwoGoalsProbability(MatchData match)
    {
        var over2Weight = match.OverTwoGoals >= PredictionThresholds.Over2 ? 0.25 : 0;
        var over3Weight = match.OverThreeGoals >= PredictionThresholds.Over3 ? 0.25 : 0;
        var sameHomeWeight = match is { HomeWin: > PredictionThresholds.BalancedMatchDiff, 
                                 AwayWin: > PredictionThresholds.BalancedMatchDiff, 
                                 OverTwoGoals: > PredictionThresholds.Over2 } || 
                             (match.HomeWin + match.Draw < match.AwayWin && match.OverTwoGoals > PredictionThresholds.Over2) || 
                             (match.AwayWin + match.Draw < match.HomeWin && match.OverThreeGoals > PredictionThresholds.Over3)
            ? 0.35 : 0;
        
        return over2Weight + over3Weight + sameHomeWeight;
    }
    
    public double CalculateDrawProbability(MatchData match)
    {
        var drawWeight = match.Draw >= PredictionThresholds.DrawProb ? 0.5 : 0;
        var homeWinWeight = match is { Draw: > 0.35, HomeWin: < 0.34, AwayWin: < 0.34, UnderTwoGoals: > PredictionThresholds.Over2 } ? 0.25 : 0;
        var awayWinWeight = match.HomeWin < 0.34 && match is { AwayWin: < 0.34, UnderThreeGoals: > 0.75 } ? 0.25 : 0;
        var under2Weight = match.UnderTwoGoals >= PredictionThresholds.Under2 ? 0.1 : 0;
        var highDrawWeight = match.Draw >= 0.36 ? 0.5 : 0;
        
        return drawWeight + homeWinWeight + awayWinWeight + under2Weight  + highDrawWeight;;
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