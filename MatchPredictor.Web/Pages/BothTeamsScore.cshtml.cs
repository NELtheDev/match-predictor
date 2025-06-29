using MatchPredictor.Domain.Models;
using MatchPredictor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MatchPredictor.Web.Pages;

public class BothTeamsScoreModel : PageModel
{
    private readonly ApplicationDbContext _context;
    public List<Prediction> Predictions { get; set; } = new();

    public BothTeamsScoreModel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task OnGet()
    {
        // var today = DateTime.UtcNow.Date;
        // Predictions = await _context.Predictions
        //     .Where(p => p.MatchDate == today && p.PredictionCategory == "BothTeamsScore")
        //     .OrderBy(p => p.HomeTeam)
        //     .ToListAsync();
    }
}
