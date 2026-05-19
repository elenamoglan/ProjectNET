using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json;

namespace VoidRunner.Services;

// Translates strings via the free MyMemory API (no key required).
// Falls back to a built-in dictionary on failure.

public interface ITranslationService
{
    Task<string> TranslateAsync(string text, string targetLang);
    string GetLanguageName(string code);
}

public sealed class TranslationService : ITranslationService, IDisposable
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
        DefaultRequestHeaders = { { "User-Agent", "VoidRunner/1.0" } }
    };

    // Built-in fallback dictionary
    // key: (text, langCode) -> translated string
    private static readonly Dictionary<(string, string), string> Fallback = new()
    {
        // Romanian
        { ("Play",         "ro"), "Joacă" },
        { ("High Scores",  "ro"), "Scoruri Mari" },
        { ("Quit",         "ro"), "Ieșire" },
        { ("Game Over",    "ro"), "Joc Terminat" },
        { ("Score",        "ro"), "Scor" },
        { ("Lives",        "ro"), "Vieți" },
        { ("Paused",       "ro"), "Pauză" },
        { ("Resume",       "ro"), "Continuă" },
        { ("Language",     "ro"), "Limbă" },
        { ("Survived",     "ro"), "Supraviețuit" },
        { ("Enter your name:", "ro"), "Introdu numele tău:" },
        { ("MenuSubtitle", "ro"), "Supraviețuiește cât poți · WASD / săgeți" },
        { ("High Score!", "ro"), "Scor maxim!" },
        { ("Play again?", "ro"), "Încă un joc?" },
        { ("(Y) Yes · (N) No", "ro"), "(Y) Da · (N) Nu" },
        {
            ("Enter confirm · Esc cancel", "ro"), "Intro — confirmă · Esc — renunță"
        },
        // Spanish
        { ("Play",         "es"), "Jugar" },
        { ("High Scores",  "es"), "Puntuaciones" },
        { ("Quit",         "es"), "Salir" },
        { ("Game Over",    "es"), "Fin del Juego" },
        { ("Score",        "es"), "Puntuación" },
        { ("Lives",        "es"), "Vidas" },
        { ("Paused",       "es"), "Pausado" },
        { ("Resume",       "es"), "Reanudar" },
        { ("Language",     "es"), "Idioma" },
        { ("Survived",     "es"), "Sobrevivido" },
        { ("Enter your name:", "es"), "Escribe tu nombre:" },
        { ("MenuSubtitle", "es"), "Aguanta lo que puedas · WASD o flechas" },
        { ("High Score!", "es"), "¡Récord!" },
        { ("Play again?", "es"), "¿Volver a jugar?" },
        { ("(Y) Yes · (N) No", "es"), "(Y) Sí · (N) No" },
        {
            ("Enter confirm · Esc cancel", "es"), "Intro — confirmar · Esc — cancelar"
        },
        // French
        { ("Play",         "fr"), "Jouer" },
        { ("High Scores",  "fr"), "Meilleurs Scores" },
        { ("Quit",         "fr"), "Quitter" },
        { ("Game Over",    "fr"), "Partie Terminée" },
        { ("Score",        "fr"), "Score" },
        { ("Lives",        "fr"), "Vies" },
        { ("Paused",       "fr"), "En Pause" },
        { ("Resume",       "fr"), "Reprendre" },
        { ("Language",     "fr"), "Langue" },
        { ("Survived",     "fr"), "Survécu" },
        { ("Enter your name:", "fr"), "Entrez votre nom :" },
        { ("MenuSubtitle", "fr"), "Survivez le plus longtemps possible · ZQSD ou flèches" },
        { ("High Score!", "fr"), "Meilleur score !" },
        { ("Play again?", "fr"), "Rejouer ?" },
        { ("(Y) Yes · (N) No", "fr"), "(Y) Oui · (N) Non" },
        {
            ("Enter confirm · Esc cancel", "fr"), "Entrée — confirmer · Échap — annuler"
        },
    };

    private readonly ConcurrentDictionary<string, string> _cache = new();

    public Task<string> TranslateAsync(string text, string targetLang)
    {
        if (targetLang == "en") return Task.FromResult(text);

        string cacheKey = $"{text}|{targetLang}";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return Task.FromResult(cached);

        if (Fallback.TryGetValue((text, targetLang), out var dictHit))
        {
            _cache[cacheKey] = dictHit;
            return Task.FromResult(dictHit);
        }

        return TranslateAsyncSlow(text, targetLang, cacheKey);
    }

    private async Task<string> TranslateAsyncSlow(string text, string targetLang, string cacheKey)
    {
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            string url = $"https://api.mymemory.translated.net/get" +
                         $"?q={Uri.EscapeDataString(text)}&langpair=en|{targetLang}";

            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            string? result = doc.RootElement
                .GetProperty("responseData")
                .GetProperty("translatedText")
                .GetString();

            if (!string.IsNullOrWhiteSpace(result) && result != text)
            {
                _cache[cacheKey] = result;
                return result;
            }
        }
        catch
        {
            // Fall through to dictionary / source text
        }

        string final = Fallback.TryGetValue((text, targetLang), out var fb) ? fb : text;
        _cache[cacheKey] = final;
        return final;
    }

    public string GetLanguageName(string code) => code switch
    {
        "en" => "English",
        "ro" => "Română",
        "es" => "Español",
        "fr" => "Français",
        _ => code.ToUpperInvariant()
    };

    public void Dispose() => Http.Dispose();
}
