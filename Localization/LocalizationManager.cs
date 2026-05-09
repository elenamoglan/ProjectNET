using VoidRunner.Services;

namespace VoidRunner.Localization;

/// <summary>
/// Manages the current UI language and caches translated strings.
/// Demonstrates: singleton-like service, async initialisation, events.
/// </summary>
public sealed class LocalizationManager
{
    private readonly ITranslationService          _translator;
    private readonly Dictionary<string, string>   _cache = new();
    private readonly SemaphoreSlim                  _loadLock = new(1, 1);
    private          string                         _lang  = "en";

    public event Action? LanguageChanged;

    public string CurrentLanguage => _lang;
    public static readonly string[] SupportedLanguages = ["en", "ro", "es", "fr"];

    public LocalizationManager(ITranslationService translator)
    {
        _translator = translator;
    }

    /// <summary>
    /// Switch language and pre-translate all known UI keys.
    /// </summary>
    public async Task SetLanguageAsync(string langCode)
    {
        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Allow first warm-up when _lang already matches but cache is still empty.
            if (langCode == _lang && _cache.Count > 0) return;
            _lang = langCode;
            _cache.Clear();

            // Pre-warm cache with common strings (sequential avoids unsafe Dictionary writes).
            string[] keys =
            [
                "Play", "High Scores", "Quit", "Game Over",
                "Score", "Lives", "Paused", "Resume", "Language", "Survived",
                "Enter your name:", "MenuSubtitle",
                "High Score!",
                "Play again?",
                "(Y) Yes · (N) No",
                "Enter confirm · Esc cancel",
            ];

            foreach (var k in keys)
            {
                if (k == "MenuSubtitle" && langCode == "en")
                {
                    _cache[k] = "Survive as long as you can · WASD / arrow keys";
                    continue;
                }

                var translated = await _translator.TranslateAsync(k, langCode).ConfigureAwait(false);
                _cache[k] = translated;
            }

            LanguageChanged?.Invoke();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    /// <summary>Returns the cached translation or the key itself as fallback.</summary>
    public string Get(string key) =>
        _cache.TryGetValue(key, out var val) ? val : key;

    public string GetLanguageName(string code) => _translator.GetLanguageName(code);
}
