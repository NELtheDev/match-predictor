using Hangfire;
using MatchPredictor.Domain.Interfaces;
using MatchPredictor.Domain.Models;
using MatchPredictor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MatchPredictor.Application.Services;

public class AnalyzerService  : IAnalyzerService
{
    private readonly IDataAnalyzerService _dataAnalyzerService;
    private readonly IWebScraperService _webScraperService;
    private readonly ApplicationDbContext _dbContext;
    private readonly IExtractFromExcel _excelExtract;
    private readonly ILogger<AnalyzerService> _logger;
    
public AnalyzerService(
        IDataAnalyzerService dataAnalyzerService,
        IWebScraperService webScraperService,
        ApplicationDbContext dbContext,
        IExtractFromExcel excelExtract,
        ILogger<AnalyzerService> logger)
    {
        _dataAnalyzerService = dataAnalyzerService;
        _webScraperService = webScraperService;
        _dbContext = dbContext;
        _excelExtract = excelExtract;
        _logger = logger;
    }

    public async Task RunScraperAndAnalyzerAsync()
    {
        _logger.LogInformation("Starting scraping and analysis process...");

        try
        {
            await _webScraperService.ScrapeMatchDataAsync();
            _logger.LogInformation("Web scraping completed successfully.");
            
            var scraped = _excelExtract.ExtractMatchDatasetFromFile().ToList();
            _logger.LogInformation($"Extracted {scraped.Count} matches from Excel file.");

            var today = DateTime.UtcNow.Date.ToString("dd-MM-yyyy");
            var existing = await _dbContext.Predictions.Where(p => p.Date == today).ToListAsync();
            _dbContext.Predictions.RemoveRange(existing);
            await _dbContext.SaveChangesAsync();

            await SavePredictions("BothTeamsScore", _dataAnalyzerService.BothTeamsScore(scraped));
            await SavePredictions("Draw", _dataAnalyzerService.Draw(scraped));
            await SavePredictions("Over2.5Goals", _dataAnalyzerService.OverTwoGoals(scraped));
            await SavePredictions("StraightWin", _dataAnalyzerService.StraightWin(scraped));
            _logger.LogInformation("Predictions saved successfully.");

            await _dbContext.ScrapingLogs.AddAsync(new ScrapingLog
            {
                Timestamp = DateTime.UtcNow,
                Status = "Success",
                Message = "Scraping and prediction analysis completed successfully."
            });
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Scraping log saved successfully.");
        }
        catch (Exception ex)
        {
            var log = new ScrapingLog
            {
                Status = "Failed",
                Message = $"{ex.Message}"
            };
            _logger.LogError(ex, "An error occurred during scraping and analysis.");
            
            await _dbContext.ScrapingLogs.AddAsync(log);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Scraping log saved with error status.");
            throw; // Re-throw the exception to ensure Hangfire marks the job as failed
        }
    }
    
    public async Task CleanupOldPredictionsAsync()
    {
        var cutoff = DateTime.UtcNow.Date.AddDays(-2);

        var oldPredictions = (await _dbContext.Predictions.ToListAsync())
            .Where(p => DateTime.Parse(p.Date) < cutoff)
            .ToList();

        if (oldPredictions.Count > 0)
        {
            _dbContext.Predictions.RemoveRange(oldPredictions);
            await _dbContext.SaveChangesAsync();
        }
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
                    "BothTeamsScore" => 0,
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