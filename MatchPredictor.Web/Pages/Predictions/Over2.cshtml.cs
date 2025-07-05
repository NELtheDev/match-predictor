using MatchPredictor.Domain.Models;
using MatchPredictor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MatchPredictor.Web.Pages.Predictions;

public class Over2 : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    public List<Prediction>? Matches { get; set; } = [];
    
    public Over2(ApplicationDbContext context, IMemoryCache cache)
    {
        _cache = cache;
        _context = context;
    }
    
    public async Task<IActionResult> OnGet()
    {
        var dateString = DateTime.UtcNow.Date.ToString("dd-MM-yyyy");
        var today = DateTime.UtcNow.Date;
        Matches = await _cache.GetOrCreateAsync($"over2_{today}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12);
            return await _context.Predictions
                .Where(p => p.Date == dateString && 
                            p.PredictionCategory == "Over2.5Goals")
                .OrderBy(p => p.Time)
                .ThenBy(p => p.HomeTeam)
                .ToListAsync();
        });
        
        Matches = Matches?
            .DistinctBy(p => new { p.League, p.HomeTeam, p.AwayTeam, p.Date, p.Time })
            .ToList();
        
        return Page();
    }
}