namespace MatchPredictor.Domain.Models;

public class MatchScore
{
    public int Id { get; set; }

    public string League { get; set; } = null!;
    public string HomeTeam { get; set; } = null!;
    public string AwayTeam { get; set; } = null!;
    public DateTime MatchTime { get; set; }

    public string Score { get; set; } = null!;
    public bool BTTSLabel { get; set; }
}