//
// using MatchPredictor.Data;
// using MatchPredictor.Data.Models;
// using Analysis.Controller;
// using Data.Repository;
// using Microsoft.EntityFrameworkCore;
//
// namespace MatchPredictor.Web.Services;
//
// public class AnalyzerJobService
// {
//     private readonly ApplicationDbContext _dbContext;
//     private readonly IDataAnalyzer _dataAnalyzer;
//
//     public AnalyzerJobService(ApplicationDbContext dbContext, IDataAnalyzer dataAnalyzer)
//     {
//         _dbContext = dbContext;
//         _dataAnalyzer = dataAnalyzer;
//     }
//
//     public async Task RunScraperAndAnalyzer()
//     {
//         // Example: call your SeleniumWebScraper to scrape new data
//         Console.WriteLine("Running WebScraper...");
//         // TODO: Replace with your actual scraper call
//         // var scraper = new SeleniumWebScraper();
//         // await scraper.RunAsync();
//
//         Console.WriteLine("Running DataAnalyzer...");
//
//         // Clear today's predictions first
//         var today = DateTime.UtcNow.Date;
//         var existing = await _dbContext.Predictions.Where(p => p.MatchDate == today).ToListAsync();
//         _dbContext.Predictions.RemoveRange(existing);
//         await _dbContext.SaveChangesAsync();
//
//         // Now insert predictions per category
//         await InsertCategoryPredictions("BothTeamsScore", _dataAnalyzer.BothTeamsScore());
//         await InsertCategoryPredictions("Draw", _dataAnalyzer.Draw());
//         await InsertCategoryPredictions("Over2.5Goals", _dataAnalyzer.OverTwoGoals());
//         await InsertCategoryPredictions("StraightWin", _dataAnalyzer.StraightWin());
//
//         Console.WriteLine("Predictions saved.");
//     }
//
//     private async Task InsertCategoryPredictions(string category, IEnumerable<Shared.Models.MatchData> matches)
//     {
//         var today = DateTime.UtcNow.Date;
//         foreach (var match in matches)
//         {
//             var prediction = new Prediction
//             {
//                 MatchDate = today,
//                 HomeTeam = match.HomeTeam,
//                 AwayTeam = match.AwayTeam,
//                 PredictionCategory = category,
//                 PredictedOutcome = GetOutcomeForCategory(category, match),
//                 ConfidenceScore = match.OverTwoGoals, // Example → replace with correct score if needed
//                 CreatedAt = DateTime.UtcNow
//             };
//
//             _dbContext.Predictions.Add(prediction);
//         }
//
//         await _dbContext.SaveChangesAsync();
//     }
//
//     private static string GetOutcomeForCategory(string category, Shared.Models.MatchData match)
//     {
//         return category switch
//         {
//             "BothTeamsScore" => "BTTS",
//             "Draw" => "Draw",
//             "Over2.5Goals" => "Over 2.5",
//             "StraightWin" => match.HomeWin > match.AwayWin ? "Home Win" : "Away Win",
//             _ => "N/A"
//         };
//     }
// }
