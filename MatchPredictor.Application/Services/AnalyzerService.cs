using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Domain.Models;
using MatchPredictor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MatchPredictor.Application.Services;

public class AnalyzerService
{
    private readonly IDataAnalyzerService _dataAnalyzerService;
    private readonly IWebScraperService _webScraperService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IExtractFromExcel _excelExtract;
    
public AnalyzerService(
        IDataAnalyzerService dataAnalyzerService,
        IWebScraperService webScraperService,
        ApplicationDbContext dbContext,
        IExtractFromExcel excelExtract)
    {
        _dataAnalyzerService = dataAnalyzerService;
        _webScraperService = webScraperService;
        _dbContext = dbContext;
        _excelExtract = excelExtract;
    }
    public async Task RunScraperAndAnalyzerAsync()
    {
        await _webScraperService.ScrapeMatchDataAsync();
        var scraped = _excelExtract.ExtractMatchDatasetFromFile().ToList();

        var today = DateTime.UtcNow.Date.ToString("dd-MM-yyyy");
        var existing = await _dbContext.Predictions.Where(p => p.Date == today).ToListAsync();
        _dbContext.Predictions.RemoveRange(existing);
        await _dbContext.SaveChangesAsync();

        await SavePredictions("BothTeamsScore", _dataAnalyzerService.BothTeamsScore(scraped));
        await SavePredictions("Draw", _dataAnalyzerService.Draw(scraped));
        await SavePredictions("Over2.5Goals", _dataAnalyzerService.OverTwoGoals(scraped));
        await SavePredictions("StraightWin", _dataAnalyzerService.StraightWin(scraped));
    }

    private async Task SavePredictions(string category, IEnumerable<MatchData> matches)
    {
        foreach (var match in matches)
        {
            var prediction = new Prediction
            {
                HomeTeam = match.HomeTeam,
                AwayTeam = match.AwayTeam,
                League = match.League,
                PredictionCategory = category,
                PredictedOutcome = category switch
                {
                    "BothTeamsScore" => "BTTS",
                    "Draw" => "Draw",
                    "Over2.5Goals" => "Over 2.5",
                    "StraightWin" => match.HomeWin > match.AwayWin ? "Home Win" : "Away Win",
                    _ => "Unknown"
                },
                ConfidenceScore = category switch
                {
                    "BothTeamsScore" => null,
                    "Draw" => (decimal?)match.Draw,
                    "Over2.5Goals" => (decimal?)match.OverTwoGoals,
                    "StraightWin" => (decimal?)(match.HomeWin > match.AwayWin ? match.HomeWin : match.AwayWin),
                    _ => null
                },
                Date = match.Date,
                Time = match.Time,
            };

            var exists = await _dbContext.Predictions.AnyAsync(p =>
                p.HomeTeam == prediction.HomeTeam &&
                p.AwayTeam == prediction.AwayTeam &&
                p.League == prediction.League &&
                p.Date == prediction.Date &&
                p.Time == prediction.Time &&
                p.PredictedOutcome == prediction.PredictedOutcome &&
                p.PredictionCategory == prediction.PredictionCategory);

            if (!exists)
            {
                _dbContext.Predictions.Add(prediction);
            }
        }

        await _dbContext.SaveChangesAsync();
    }

}