using Newtonsoft.Json;
using VoidRunner.Models;

namespace VoidRunner.Services;

/// <summary>
/// Persists high scores to a local JSON file.
/// Demonstrates: async file I/O, generic lists, LINQ ordering.
/// </summary>
public sealed class ScoreService
{
    private const int    MaxEntries = 10;
    private readonly string _filePath;

    public ScoreService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VoidRunner");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "scores.json");
    }

    public async Task<List<HighScore>> LoadAsync()
    {
        if (!File.Exists(_filePath)) return [];

        try
        {
            string json = await File.ReadAllTextAsync(_filePath);
            return JsonConvert.DeserializeObject<List<HighScore>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task SaveAsync(HighScore entry)
    {
        var scores = await LoadAsync();
        scores.Add(entry);

        var trimmed = scores
            .OrderByDescending(s => s.Score)
            .Take(MaxEntries)
            .ToList();

        await File.WriteAllTextAsync(_filePath, JsonConvert.SerializeObject(trimmed, Formatting.Indented));
    }

    public async Task<bool> IsHighScore(int score)
    {
        var scores = await LoadAsync();
        return scores.Count < MaxEntries || scores.Any(s => score > s.Score);
    }
}
