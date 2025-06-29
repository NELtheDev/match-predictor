using MatchPredictor.Domain.Models;
using MatchPredictor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatchPredictor.Web.Pages.Predictions;

public class BTTS : PageModel
{
    private readonly ApplicationDbContext _context;
    public List<Prediction> Matches { get; set; } = [];
    
    public BTTS(ApplicationDbContext context)
    {
        _context = context;
    }
    
    public async Task OnGet()
    {
        var dateString = DateTime.UtcNow.Date.ToString("dd-MM-yyyy");
        Matches = await _context.Predictions
            .Where(p => p.Date == dateString && 
                        p.PredictionCategory == "BothTeamsScore")
            .ToListAsync();
            
        Matches = Matches
            .DistinctBy(p => new { p.League, p.HomeTeam, p.AwayTeam, p.Date, p.Time })
            .OrderBy(p => p.Time)
            .ThenBy(p => p.HomeTeam)
            .ToList();
    }
}