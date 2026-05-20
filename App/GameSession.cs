using VoidRunner.Graphics;
using VoidRunner.Localization;
using VoidRunner.Models;
using VoidRunner.Services;

namespace VoidRunner.App;

internal enum GameOverPhase
{
    None,
    WaitAfterDeath,
    CheckingHighScore,
    NameEntry,
    Saving,
    AskPlayAgain,
}

internal sealed class GameSession
{
    private readonly LocalizationManager _locale;
    private readonly ScoreService _scoreService;
    private readonly AudioService _audio;
    private readonly HashSet<KeyCode> _keys = [];
    private readonly Action _exitToMenu;

    private Player _player = null!;
    private readonly List<Enemy> _enemies = [];

    private bool _paused;
    private bool _gameOver;

    private float _elapsed;
    private int _score;
    private float _spawnTimer;
    private float _spawnInterval = 2.5f;
    private float _speedMult = 1.0f;
    private int _wave;

    private readonly (float x, float y, float speed, float size)[] _stars;
    private float _waveAlpha;

    private GameOverPhase _goPhase = GameOverPhase.None;
    private float _goTimer;
    private Task<bool>? _highTask;
    private bool _isHighScore;
    private Task? _persistTask;

    internal string NameBuffer { get; private set; } = "";

    private static readonly Random Rng = new();

    public GameSession(LocalizationManager locale, ScoreService scoreService, AudioService audio, Action exitToMenu)
    {
        _locale = locale;
        _scoreService = scoreService;
        _audio = audio;
        _exitToMenu = exitToMenu;

        _stars = Enumerable.Range(0, 100)
            .Select(_ => (
                x: (float)Rng.NextDouble() * GameConstants.CanvasWidth,
                y: (float)Rng.NextDouble() * GameConstants.CanvasHeight,
                speed: 0.5f + (float)Rng.NextDouble() * 1.5f,
                size: 0.8f + (float)Rng.NextDouble() * 1.8f))
            .ToArray();

        InitGame();
    }

    private void InitGame()
    {
        _keys.Clear();

        _player = new Player(
            (GameConstants.CanvasWidth - 24) / 2f,
            (GameConstants.CanvasHeight - 28) / 2f,
            _keys);

        _enemies.Clear();
        _elapsed = 0f;
        _score = 0;
        _spawnTimer = 0f;
        _spawnInterval = 2.5f;
        _speedMult = 1.0f;
        _wave = 0;
        _gameOver = false;
        _paused = false;

        _goPhase = GameOverPhase.None;
        _goTimer = 0f;
        _highTask = null;
        _persistTask = null;
        NameBuffer = "";
    }

    public void Update(float dt)
    {
        dt = Math.Min(dt, 0.05f);

        if (_gameOver)
        {
            TickGameOver(dt);
            return;
        }

        if (!_paused)
        {
            UpdateStars(dt);
            UpdateLiveGame(dt);
        }
    }

    private void TickGameOver(float dt)
    {
        switch (_goPhase)
        {
            case GameOverPhase.WaitAfterDeath:
                _goTimer -= dt;
                if (_goTimer <= 0f)
                {
                    _highTask = _scoreService.IsHighScore(_score);
                    _goPhase = GameOverPhase.CheckingHighScore;
                }

                break;
        }

        if (_goPhase == GameOverPhase.CheckingHighScore && _highTask is { IsCompleted: true })
        {
            bool fault = _highTask.IsFaulted;
            _isHighScore = !fault && _highTask.Result;

            if (_isHighScore)
            {
                NameBuffer = "Player";
                _goPhase = GameOverPhase.NameEntry;
            }
            else
            {
                StartSave("Player");
            }

            _highTask = null;
        }

        if (_goPhase != GameOverPhase.Saving || _persistTask?.IsCompleted != true) return;

        _persistTask = null;
        _goPhase = GameOverPhase.AskPlayAgain;
    }

    private void StartSave(string playerName)
    {
        _goPhase = GameOverPhase.Saving;
        string nameFinal = playerName.Trim();
        if (nameFinal.Length == 0) nameFinal = "Player";

        string captured = nameFinal;
        _persistTask = Persist(captured);

        Task Persist(string nm) =>
            _scoreService.SaveAsync(new HighScore
            {
                PlayerName = nm,
                Score = _score,
                Survived = TimeSpan.FromSeconds(_elapsed)
            });
    }

    public void TogglePause()
    {
        if (_gameOver) return;
        _paused = !_paused;
        if (_paused) _audio.PlayPause();
    }

    public void SubmitName()
    {
        if (_goPhase != GameOverPhase.NameEntry) return;
        StartSave(NameBuffer.Length == 0 ? "Player" : NameBuffer);
    }

    public void CancelNameEntry()
    {
        if (_goPhase != GameOverPhase.NameEntry) return;
        StartSave("Player");
    }

    public void NameBackspace()
    {
        if (_goPhase != GameOverPhase.NameEntry || NameBuffer.Length == 0) return;
        NameBuffer = NameBuffer[..^1];
    }

    public void NameChar(char ch)
    {
        if (_goPhase != GameOverPhase.NameEntry || NameBuffer.Length >= 24) return;
        if (char.IsLetterOrDigit(ch) || ch is ' ' or '-' or '_') NameBuffer += ch;
    }

    public void PlayAgainYes()
    {
        if (_goPhase != GameOverPhase.AskPlayAgain) return;
        InitGame();
    }

    public void PlayAgainNo()
    {
        if (_goPhase != GameOverPhase.AskPlayAgain) return;
        _exitToMenu();
    }

    internal void NotifyPlayerDeathFinal()
    {
        if (_goPhase != GameOverPhase.None) return;
        _gameOver = true;
        _paused = false;
        _audio.PlayGameOver();
        _goPhase = GameOverPhase.WaitAfterDeath;
        _goTimer = 0.8f;
    }

    private void UpdateStars(float dt)
    {
        for (int i = 0; i < _stars.Length; i++)
        {
            var s = _stars[i];
            s.y += s.speed;
            if (s.y > GameConstants.CanvasHeight) s.y = 0;
            _stars[i] = s;
        }
    }

    private void UpdateLiveGame(float dt)
    {
        _elapsed += dt;
        _spawnTimer += dt;
        // AI-generated
        _waveAlpha = Math.Max(0f, _waveAlpha - dt * 0.8f);

        int newWave = (int)(_elapsed / 15f);
        if (newWave > _wave)
        {
            _wave = newWave;
            _speedMult = 1.0f + _wave * 0.18f;
            _spawnInterval = Math.Max(0.6f, 2.5f - _wave * 0.22f);
            _waveAlpha = 2.5f;
        }
        // end AI-generated

        _player.Update(dt);

        if (_spawnTimer >= _spawnInterval)
        {
            _spawnTimer = 0f;
            SpawnEnemy();
        }

        foreach (var enemy in _enemies) enemy.Update(dt);

        _score = (int)(_elapsed * 10 * (1 + _wave * 0.15));

        if (!_player.IsInvincible)
        {
            var hit = _enemies.FirstOrDefault(en => en.CollidesWith(_player));
            if (hit is not null)
            {
                _player.Lives--;
                _audio.PlayHit();
                _player.TriggerInvincibility();

                if (_player.Lives <= 0)
                    NotifyPlayerDeathFinal();
            }
        }

        _enemies.RemoveAll(en =>
            en.X < -60 || en.X > GameConstants.CanvasWidth + 60 ||
            en.Y < -60 || en.Y > GameConstants.CanvasHeight + 60);
    }

    private void SpawnEnemy()
    {
        EnemyType type = _wave switch
        {
            0 => EnemyType.Chaser,
            1 => Rng.Next(2) == 0 ? EnemyType.Chaser : EnemyType.Speeder,
            _ => (EnemyType)Rng.Next(3)
        };

        var (x, y) = RandomEdgePosition(type);
        var enemy = Enemy.Create(type, x, y, _speedMult);
        enemy.SetTarget(_player);
        _enemies.Add(enemy);

        // AI-generated
        if (_wave > 0 && _wave % 3 == 0 && Rng.Next(3) == 0)
        {
            var (x2, y2) = RandomEdgePosition(EnemyType.Chaser);
            var bonus = Enemy.Create(EnemyType.Chaser, x2, y2, _speedMult);
            bonus.SetTarget(_player);
            _enemies.Add(bonus);
        }
        // end AI-generated
    }

    private static (float x, float y) RandomEdgePosition(EnemyType type)
    {
        float margin = type == EnemyType.Wanderer ? 40 : 20;
        int edge = Rng.Next(4);

        return edge switch
        {
            0 => ((float)Rng.NextDouble() * GameConstants.CanvasWidth, -margin),
            1 => ((float)Rng.NextDouble() * GameConstants.CanvasWidth,
                GameConstants.CanvasHeight + margin),
            2 => (-margin, (float)Rng.NextDouble() * GameConstants.CanvasHeight),
            _ => (GameConstants.CanvasWidth + margin,
                (float)Rng.NextDouble() * GameConstants.CanvasHeight)
        };
    }

    public void Draw(IGameRenderer g)
    {
        foreach (var s in _stars)
        {
            int a = Math.Min((int)(60 + s.size * 40), 255);
            g.FillEllipse(s.x, s.y, s.size, s.size, ColorRgba.FromArgb((byte)a, 180, 200, 255));
        }

        foreach (var enemy in _enemies) enemy.Draw(g);
        _player.Draw(g);

        DrawHUD(g);

        if (_paused && !_gameOver)
            DrawOverlay(g, _locale.Get("Paused"), "P / ESC to resume");

        if (_waveAlpha > 0 && !_gameOver)
            DrawWaveBanner(g);

        DrawGameOverUI(g);
    }

    private void DrawGameOverUI(IGameRenderer g)
    {
        if (!_gameOver || _goPhase == GameOverPhase.None) return;

        if (_goPhase is GameOverPhase.WaitAfterDeath
            or GameOverPhase.CheckingHighScore
            or GameOverPhase.Saving)
            DrawOverlay(g, _locale.Get("Game Over"), "");

        if (_goPhase == GameOverPhase.NameEntry)
        {
            DrawOverlay(g, _locale.Get("High Score!"),
                $"{_locale.Get("Enter your name:")}\n\n{NameBuffer}_\n\n{_locale.Get("Enter confirm · Esc cancel")}",
                subtitleSize: 14f, titleSubtitleGap: 40f);
            return;
        }

        if (_goPhase != GameOverPhase.AskPlayAgain) return;

        string again = $"{_locale.Get("Play again?")}\n{_locale.Get("(Y) Yes · (N) No")}";
        DrawOverlay(g, _locale.Get("Game Over"),
            $"{_locale.Get("Score")}: {_score}\n" +
            $"{_locale.Get("Survived")}: {TimeSpan.FromSeconds(_elapsed):mm\\:ss}\n\n{again}",
            subtitleSize: 18f, titleSubtitleGap: 58f);
    }

    private void DrawHUD(IGameRenderer g)
    {
        const int topY = 12;

        for (int i = 0; i < _player.Lives; i++)
            g.FillEllipse(16 + i * 26, topY, 18, 18, ColorRgba.FromArgb(255, 0, 200, 255));

        string scoreText = $"{_locale.Get("Score")}: {_score}";
        g.DrawString(scoreText, GameConstants.CanvasWidth / 2f - 60, topY, 13f, true,
            ColorRgba.FromArgb(255, 180, 220, 255));

        string timeText = TimeSpan.FromSeconds(_elapsed).ToString(@"mm\:ss");
        g.DrawString(timeText, GameConstants.CanvasWidth - 90, topY, 13f, true,
            ColorRgba.FromArgb(255, 140, 180, 230));

        if (_wave > 0)
        {
            string waveText = $"Wave {_wave}";
            g.DrawString(waveText, GameConstants.CanvasWidth - 80, GameConstants.CanvasHeight - 30, 13f, true,
                ColorRgba.FromArgb(255, 100, 160, 100));
        }

        if (_elapsed < 5f)
        {
            int alpha = (int)(255 * (1f - _elapsed / 5f));
            string hint = "WASD / Arrow keys to move   P to pause";
            var sf = g.MeasureString(hint, 13f, true);
            g.DrawString(hint, (GameConstants.CanvasWidth - sf.w) / 2f, GameConstants.CanvasHeight - 30, 13f, true,
                ColorRgba.FromArgb((byte)Math.Clamp(alpha, 0, 255), 120, 150, 190));
        }
    }

    private void DrawOverlay(IGameRenderer g, string title, string subtitle,
        float subtitleSize = 13f, float titleSubtitleGap = 28f)
    {
        g.FillRect(0, 0, GameConstants.CanvasWidth, GameConstants.CanvasHeight,
            ColorRgba.FromArgb(150, 0, 0, 0));

        var ts = g.MeasureString(title, 32f, true);
        bool hasSub = !string.IsNullOrEmpty(subtitle);
        (float w, float h) ss = (0f, 0f);
        if (hasSub)
            ss = g.MeasureString(subtitle, subtitleSize, false);

        float gap = hasSub ? titleSubtitleGap : 0f;
        float totalH = ts.h + gap + ss.h;
        float top = (GameConstants.CanvasHeight - totalH) / 2f;

        g.DrawString(title,
            (GameConstants.CanvasWidth - ts.w) / 2f,
            top,
            32f,
            true,
            ColorRgba.FromArgb(255, 180, 220, 255));

        if (!hasSub) return;

        g.DrawString(subtitle,
            (GameConstants.CanvasWidth - ss.w) / 2f,
            top + ts.h + gap,
            subtitleSize,
            false,
            ColorRgba.FromArgb(255, 150, 180, 220));
    }

    private void DrawWaveBanner(IGameRenderer g)
    {
        int alpha = (int)(Math.Min(_waveAlpha, 1f) * 220);
        string txt = _wave == 1 ? "Wave 1 – Watch out!" : $"Wave {_wave}  – Danger increased!";
        var sz = g.MeasureString(txt, 20f, true);

        g.FillRect(0, 80, GameConstants.CanvasWidth, sz.h + 16,
            ColorRgba.FromArgb((byte)Math.Clamp(alpha / 2, 0, 255), 40, 20, 80));

        g.DrawString(txt,
            (GameConstants.CanvasWidth - sz.w) / 2f,
            88,
            20f,
            true,
            ColorRgba.FromArgb((byte)Math.Clamp(alpha, 0, 255), 255, 160, 60));
    }

    public bool GoPhaseIsNameEntry() => _gameOver && _goPhase == GameOverPhase.NameEntry;

    public bool GoPhaseIsAskAgain() => _gameOver && _goPhase == GameOverPhase.AskPlayAgain;

    public bool GameOverBlocksPause() => _gameOver;

    public HashSet<KeyCode> MovementKeys => _keys;
}
