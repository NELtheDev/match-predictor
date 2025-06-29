using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Domain.Models;

namespace MatchPredictor.Infrastructure.Services;

public class DataAnalyzerService : IDataAnalyzerService
{
    public IEnumerable<MatchData> BothTeamsScore(IEnumerable<MatchData> matches)
    {
        return matches.Where(m =>
        {
            var bttsScore = (m.OverTwoGoals + m.OverThreeGoals) / 2.0 + 
                            (Math.Abs(m.HomeWin - m.AwayWin) < PredictionThresholds.BalancedMatchDiff ? 0.05 : 0);
            
            return m.OverTwoGoals == 0 || m.OverThreeGoals == 0
                ? (Math.Abs(m.HomeWin - m.AwayWin) < 0.20 &&
                   m.OverTwoGoals > 0.68) ||
                  (Math.Abs(m.HomeWin - m.AwayWin) < 0.20 &&
                   m.OverThreeGoals > PredictionThresholds.Over3)
                : bttsScore >= PredictionThresholds.BTTSScoreThreshold;
        });
    }


    public IEnumerable<MatchData> OverTwoGoals(IEnumerable<MatchData> matches)
    {
        return matches.Where(m =>
        {
            var score = (m.OverTwoGoals >= PredictionThresholds.Over2 ? 0.6 : 0) +
                        (m.OverThreeGoals >= PredictionThresholds.Over3 ? 0.4 : 0);
            return m.OverTwoGoals == 0 || m.OverThreeGoals == 0
                ? m.OverTwoGoals > 0.65 || m.OverThreeGoals > 0.55
                : score >= 0.8;
        });
    }
    
    public IEnumerable<MatchData> Draw(IEnumerable<MatchData> matches)
    {
        return matches
            .Select(m => new
            {
                Match = m,
                Score = (m.Draw >= PredictionThresholds.DrawProb ? 0.5 : 0) +
                        (m.HomeWin <= PredictionThresholds.WinCap ? 0.25 : 0) +
                        (m.AwayWin <= PredictionThresholds.WinCap ? 0.25 : 0) +
                        (m.UnderTwoGoals >= PredictionThresholds.Under2 ? 0.1 : 0)
            })
            .Where(x => x.Score >= 0.85)
            .Select(x => x.Match);
    }
    
    public IEnumerable<MatchData> StraightWin(IEnumerable<MatchData> matches)
    {
        var results = new List<MatchData>();

        foreach (var m in matches)
        {
            // Home Win Case
            if (m.HomeWin >= PredictionThresholds.HomeWinStrong)
            {
                var score = m.HomeWin;

                if (m.OverTwoGoals >= PredictionThresholds.OverGoalsForWin)
                    score += 0.1;
                else if (m.UnderTwoGoals <= PredictionThresholds.UnderGoalsForControl)
                    score += 0.05;

                if (score >= 0.7)
                    results.Add(m);
            }

            // Away Win Case
            if (m.AwayWin >= PredictionThresholds.AwayWinStrong)
            {
                var score = m.AwayWin;

                if (m.OverTwoGoals >= PredictionThresholds.OverGoalsForWin)
                    score += 0.1;
                else if (m.UnderTwoGoals <= PredictionThresholds.UnderGoalsForControl)
                    score += 0.05;

                if (score >= 0.72)
                    results.Add(m);
            }
        }

        return results;
    }
}