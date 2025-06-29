using MatchPredictor.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace MatchPredictor.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<MatchData> MatchDatas => Set<MatchData>();
    public DbSet<Prediction> Predictions => Set<Prediction>();
}