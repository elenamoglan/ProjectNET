namespace VoidRunner.Models;

// Record for a single high-score entry.

public record HighScore
{
    public string PlayerName { get; init; } = "Player";
    public int Score { get; init; }
    public TimeSpan Survived { get; init; }
    public DateTime PlayedAt { get; init; } = DateTime.UtcNow;

    public string FormattedTime => $"{(int)Survived.TotalMinutes}:{Survived.Seconds:D2}";
}
