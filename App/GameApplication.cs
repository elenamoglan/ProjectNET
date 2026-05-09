using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using VoidRunner.Graphics;
using VoidRunner.Localization;
using VoidRunner.Models;
using VoidRunner.Services;

using System.Threading;

namespace VoidRunner.App;

internal enum AppPhase
{
    Menu,
    Game,
}

public sealed class GameApplication : IDisposable
{
    private static readonly Vector2D<int> WindowSz = new(GameConstants.CanvasWidth, GameConstants.CanvasHeight);

    private static readonly Key[] TrackedKeys =
    [
        Key.W, Key.A, Key.S, Key.D, Key.Up, Key.Down, Key.Left, Key.Right,
        Key.P, Key.Escape, Key.Enter, Key.Backspace, Key.Space, Key.Minus,
        Key.Y, Key.N, Key.ShiftLeft, Key.ShiftRight,
        Key.Number0, Key.Number1, Key.Number2, Key.Number3, Key.Number4,
        Key.Number5, Key.Number6, Key.Number7, Key.Number8, Key.Number9,
        Key.A, Key.B, Key.C, Key.E, Key.F, Key.G, Key.H, Key.I, Key.J, Key.K, Key.L, Key.M,
        Key.O, Key.Q, Key.R, Key.T, Key.U, Key.V, Key.X, Key.Z
    ];

    private readonly IWindow              _window;
    private readonly GlRenderer2D         _renderer;
    private readonly TranslationService   _translator = new();
    private readonly LocalizationManager  _locale;
    private readonly ScoreService         _scores = new();
    private readonly AudioService         _audio;

    private IInputContext? _input;
    private IKeyboard?     _keyboard;
    private IMouse?        _mouse;

    private AppPhase      _phase = AppPhase.Menu;
    private GameSession?  _session;

    private readonly HashSet<Key> _prevKeys = [];
    private readonly HashSet<Key> _currKeys = [];

    // Menu
    private readonly (float x, float y, float speed, float size)[] _menuStars;
    private readonly Random _rnd = new();
    private int _langIdx;
    private string _btnPlay   = "";
    private string _btnScores = "";
    private string _btnQuit   = "";
    private string _status    = "";
    private bool   _busyLang;
    private bool   _scoresOverlay;

    private readonly Dictionary<Key, bool> _typeKeyPrev = [];

    private const    float MenuBtnW = 220f;
    private const    float MenuBtnH = 52f;
    private static float MenuBtnX => (GameConstants.CanvasWidth - MenuBtnW) / 2f;

    private bool           _mouseLeftPrev;
    private string _scoresBody = "";

    /// <summary>Signalled from translator threads — consumed on <see cref="OnUpdate"/> (main loop).</summary>
    private int _menuTextsRefreshPending;

    public GameApplication()
    {
        SdlWindowing.Use();

        WindowOptions opts = WindowOptions.Default;
        opts.Title = "Void Runner";
        opts.Size = WindowSz;
        opts.IsVisible                = true;
        opts.ShouldSwapAutomatically  = true;
        opts.IsContextControlDisabled = false;
        opts.TransparentFramebuffer = false;
        opts.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core,
            ContextFlags.ForwardCompatible, new APIVersion(3, 3));

        _window = Window.Create(opts);
        _locale = new LocalizationManager(_translator);
        _locale.LanguageChanged += () =>
            Interlocked.Exchange(ref _menuTextsRefreshPending, 1);

        _menuStars = Enumerable.Range(0, 120).Select(_ => (
            x:     (float)_rnd.NextDouble() * GameConstants.CanvasWidth,
            y:     (float)_rnd.NextDouble() * GameConstants.CanvasHeight,
            speed: 0.3f + (float)_rnd.NextDouble() * 1.2f,
            size:  0.8f + (float)_rnd.NextDouble() * 2f)).ToArray();

        _window.Load    += OnLoad;
        _window.Update  += OnUpdate;
        _window.Render += OnRender;

        _renderer = new GlRenderer2D(_window);
        _audio    = new AudioService();

        RefreshMenuTextsSafe();
        _langIdx = 0;
    }

    public void Run()
    {
        _window.Run();
    }

    private void OnLoad()
    {
        _renderer.Init();
        _window.VSync = true;

        _input = _window.CreateInput();
        TryBindInputDevices();

        _ = WarmInitialLocaleAsync();
    }

    private async Task WarmInitialLocaleAsync()
    {
        try
        {
            await _locale.SetLanguageAsync("en").ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Exchange(ref _menuTextsRefreshPending, 1);
        }
    }

    private void TryBindInputDevices()
    {
        if (_input is null) return;

        _keyboard ??= _input.Keyboards.FirstOrDefault();
        _mouse    ??= _input.Mice.FirstOrDefault();
    }

    private void OnUpdate(double deltaSeconds)
    {
        float dt = (float)Math.Min(deltaSeconds, 0.05);

        if (Interlocked.Exchange(ref _menuTextsRefreshPending, 0) != 0)
            RefreshMenuTextsSafe();

        TryBindInputDevices();

        bool leftNow = _mouse?.IsButtonPressed(MouseButton.Left) ?? false;
        bool leftClickEdge = leftNow && !_mouseLeftPrev;
        _mouseLeftPrev = leftNow;

        if (_keyboard is not null)
            SyncKeyboardState();

        if (_phase == AppPhase.Menu)
        {
            UpdateMenu(dt, leftClickEdge);
            return;
        }

        if (_session is null) return;

        bool overlay = HandleGameOverlayInput();
        if (_session is null)
            return;

        if (!overlay &&
            !_session.GameOverBlocksPause() &&
            (Edge(Key.P) || Edge(Key.Escape)))
            _session.TogglePause();

        if (!_session.GameOverBlocksPause())
            FillMovementKeys(_session.MovementKeys);

        _session.Update(dt);
    }

    /// <summary>Returns true during name/play-again overlays (blocks pause and movement).</summary>
    private bool HandleGameOverlayInput()
    {
        if (_session is null) return false;

        if (_session.GoPhaseIsNameEntry())
        {
            if (Edge(Key.Enter))
            {
                _session.SubmitName();
                return true;
            }

            if (Edge(Key.Escape))
            {
                _session.CancelNameEntry();
                return true;
            }

            if (Edge(Key.Backspace))
                _session.NameBackspace();

            TryTypeChar(Key.A, 'a');
            TryTypeChar(Key.B, 'b');
            TryTypeChar(Key.C, 'c');
            TryTypeChar(Key.D, 'd');
            TryTypeChar(Key.E, 'e');
            TryTypeChar(Key.F, 'f');
            TryTypeChar(Key.G, 'g');
            TryTypeChar(Key.H, 'h');
            TryTypeChar(Key.I, 'i');
            TryTypeChar(Key.J, 'j');
            TryTypeChar(Key.K, 'k');
            TryTypeChar(Key.L, 'l');
            TryTypeChar(Key.M, 'm');
            TryTypeChar(Key.N, 'n');
            TryTypeChar(Key.O, 'o');
            TryTypeChar(Key.P, 'p');
            TryTypeChar(Key.Q, 'q');
            TryTypeChar(Key.R, 'r');
            TryTypeChar(Key.S, 's');
            TryTypeChar(Key.T, 't');
            TryTypeChar(Key.U, 'u');
            TryTypeChar(Key.V, 'v');
            TryTypeChar(Key.W, 'w');
            TryTypeChar(Key.X, 'x');
            TryTypeChar(Key.Y, 'y');
            TryTypeChar(Key.Z, 'z');
            TryTypeChar(Key.Number0, '0');
            TryTypeChar(Key.Number1, '1');
            TryTypeChar(Key.Number2, '2');
            TryTypeChar(Key.Number3, '3');
            TryTypeChar(Key.Number4, '4');
            TryTypeChar(Key.Number5, '5');
            TryTypeChar(Key.Number6, '6');
            TryTypeChar(Key.Number7, '7');
            TryTypeChar(Key.Number8, '8');
            TryTypeChar(Key.Number9, '9');
            TryTypeChar(Key.Space, ' ');
            TryTypeChar(Key.Minus, '-');
            return true;
        }

        if (!_session.GoPhaseIsAskAgain()) return false;

        if (Edge(Key.Y))
        {
            _audio.PlayMenuSelect();
            _session.PlayAgainYes();
            return true;
        }

        if (Edge(Key.N) || Edge(Key.Escape))
        {
            _audio.PlayMenuSelect();
            _session.PlayAgainNo();
            return true;
        }

        return true;
    }

    private void TryTypeChar(Key k, char chLow)
    {
        if (_session is null || !_session.GoPhaseIsNameEntry()) return;

        bool now = Down(k);
        _typeKeyPrev.TryGetValue(k, out bool was);
        if (now == was)
            return;

        _typeKeyPrev[k] = now;
        if (!now) return;

        bool shift = Down(Key.ShiftLeft) || Down(Key.ShiftRight);
        char ch = shift && char.IsLetter(chLow) ? char.ToUpperInvariant(chLow) : chLow;
        if (shift && chLow == '-')
            ch = '_';

        _session.NameChar(ch);
    }

    private void FillMovementKeys(HashSet<Key> dest)
    {
        dest.Clear();
        void H(Key k)
        {
            if (Down(k)) dest.Add(k);
        }

        H(Key.W);
        H(Key.A);
        H(Key.S);
        H(Key.D);
        H(Key.Up);
        H(Key.Down);
        H(Key.Left);
        H(Key.Right);
    }

    private void SyncKeyboardState()
    {
        _prevKeys.Clear();
        foreach (Key k in _currKeys) _prevKeys.Add(k);

        _currKeys.Clear();
        foreach (Key k in TrackedKeys)
        {
            if (_keyboard!.IsKeyPressed(k)) _currKeys.Add(k);
        }
    }

    private bool Down(Key k) => _currKeys.Contains(k);

    private bool Edge(Key k) => _currKeys.Contains(k) && !_prevKeys.Contains(k);

    private void StartGame()
    {
        _audio.PlayMenuSelect();
        _session = new GameSession(_locale, _scores, _audio, ReturnToMenu);
        _phase   = AppPhase.Game;
    }

    private void ReturnToMenu()
    {
        _phase   = AppPhase.Menu;
        _session = null;
    }

    private void UpdateMenu(float dt, bool leftClickEdge)
    {
        if (_busyLang) return;

        if (_scoresOverlay)
        {
            if (Edge(Key.Escape) || leftClickEdge)
                _scoresOverlay = false;

            return;
        }

        for (int i = 0; i < _menuStars.Length; i++)
        {
            var s = _menuStars[i];
            s.y += s.speed;
            if (s.y > GameConstants.CanvasHeight) s.y = 0;
            _menuStars[i] = s;
        }

        if (Edge(Key.Escape))
        {
            _window.Close();
            return;
        }

        if (Edge(Key.Left))
            _ = CycleLanguageAsync(-1);
        if (Edge(Key.Right))
            _ = CycleLanguageAsync(1);

        if (!leftClickEdge) return;

        var (mx, my) = MouseGame();
        if (Hit(mx, my, MenuBtnX, 200, MenuBtnW, MenuBtnH))
        {
            StartGame();
            return;
        }

        if (Hit(mx, my, MenuBtnX, 270, MenuBtnW, MenuBtnH))
        {
            _audio.PlayMenuSelect();
            _scoresOverlay = true;
            _scoresBody = "Loading…";
            _ = LoadScoresOverlayAsync();
            return;
        }

        if (Hit(mx, my, MenuBtnX, 340, MenuBtnW, MenuBtnH))
        {
            _audio.PlayMenuSelect();
            _window.Close();
        }
    }

    private async Task LoadScoresOverlayAsync()
    {
        var scores = await _scores.LoadAsync().ConfigureAwait(false);
        if (scores.Count == 0)
        {
            _scoresBody = "No scores yet.";
            return;
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("* TOP SCORES *");
        sb.AppendLine();
        for (int i = 0; i < scores.Count; i++)
        {
            HighScore sc = scores[i];
            sb.AppendLine($"  {i + 1,2}. {sc.PlayerName,-12} {sc.Score,6} pts   {sc.FormattedTime}");
        }

        _scoresBody = sb.ToString();
    }

    private void OnRender(double _)
    {
        _renderer.BeginFrame();

        if (_phase == AppPhase.Menu)
            DrawMenu(_renderer);
        else
            _session?.Draw(_renderer);

        _renderer.EndFrame();
    }

    private (float x, float y) MouseGame()
    {
        if (_mouse is null) return (0, 0);
        var p = _mouse.Position;
        float sx = GameConstants.CanvasWidth  / (float)Math.Max(1, _window.FramebufferSize.X);
        float sy = GameConstants.CanvasHeight / (float)Math.Max(1, _window.FramebufferSize.Y);
        return (p.X * sx, p.Y * sy);
    }

    private static bool Hit(float mx, float my, float x, float y, float w, float h) =>
        mx >= x && mx <= x + w && my >= y && my <= y + h;

    private void DrawMenu(IGameRenderer g)
    {
        foreach (var s in _menuStars)
        {
            int a = Math.Min((int)(80 + s.size * 50), 255);
            g.FillEllipse(s.x, s.y, s.size, s.size, ColorRgba.FromArgb((byte)a, 200, 220, 255));
        }

        var titleTxt = "VOID RUNNER";
        var titleSz = g.MeasureString(titleTxt, 44f, true);
        int tx = (int)((GameConstants.CanvasWidth - titleSz.w) / 2f);
        g.DrawString(titleTxt, tx, 80, 44f, true, ColorRgba.FromArgb(255, 100, 200, 255));
        g.DrawString(titleTxt, tx + 2, 82, 44f, true, ColorRgba.FromArgb(255, 180, 80, 220));

        string sub = _locale.Get("MenuSubtitle");
        var ss = g.MeasureString(sub, 12f, false);
        g.DrawString(sub, (GameConstants.CanvasWidth - ss.w) / 2f, 148, 12f, false,
            ColorRgba.FromArgb(255, 120, 160, 200));

        DrawButton(g, MenuBtnX, 200, _btnPlay);
        DrawButton(g, MenuBtnX, 270, _btnScores);
        DrawButton(g, MenuBtnX, 340, _btnQuit);

        string code = LocalizationManager.SupportedLanguages[_langIdx];
        string langLine = $"{_locale.Get("Language")}: {_locale.GetLanguageName(code)} — ← / →";
        var langSz = g.MeasureString(langLine, 10f, false);
        g.DrawString(langLine, (GameConstants.CanvasWidth - langSz.w) / 2f, 420, 10f, false,
            ColorRgba.FromArgb(255, 160, 180, 220));

        if (!string.IsNullOrEmpty(_status))
        {
            var st = g.MeasureString(_status, 10f, false);
            g.DrawString(_status, (GameConstants.CanvasWidth - st.w) / 2f, 444, 10f, false,
                ColorRgba.FromArgb(255, 100, 150, 255));
        }

        if (_scoresOverlay)
            DrawScoresOverlay(g);
    }

    private static void DrawButton(IGameRenderer g, float x, float y, string text)
    {
        g.FillRect(x, y, 220, 52, ColorRgba.FromArgb(255, 20, 26, 60));
        var ms = g.MeasureString(text, 12f, true);
        g.DrawString(text, x + (220 - ms.w) / 2f, y + (52 - ms.h) / 2f, 12f, true,
            ColorRgba.FromArgb(255, 180, 220, 255));
        g.StrokePolygon(
            [(x, y), (x + 220, y), (x + 220, y + 52), (x, y + 52)],
            ColorRgba.FromArgb(255, 60, 80, 140),
            1f);
    }

    private void DrawScoresOverlay(IGameRenderer g)
    {
        g.FillRect(0, 0, GameConstants.CanvasWidth, GameConstants.CanvasHeight,
            ColorRgba.FromArgb(200, 0, 0, 0));

        float y = 40f;
        foreach (ReadOnlySpan<char> line in _scoresBody.AsSpan().EnumerateLines())
        {
            string s = line.ToString();
            g.DrawString(s, 80, y, 13f, false, ColorRgba.FromArgb(255, 210, 220, 245));
            y += 20f;
        }

        var hintSz = g.MeasureString("Esc or click — close", 12f, false);
        g.DrawString("Esc or click — close", (GameConstants.CanvasWidth - hintSz.w) / 2f,
            GameConstants.CanvasHeight - 60, 12f, false, ColorRgba.FromArgb(255, 140, 160, 200));
    }

    private void RefreshMenuTextsSafe()
    {
        _btnPlay   = _locale.Get("Play");
        _btnScores = _locale.Get("High Scores");
        _btnQuit   = _locale.Get("Quit");
    }

    private async Task CycleLanguageAsync(int delta)
    {
        if (_busyLang) return;
        _busyLang = true;
        _status   = "…";

        try
        {
            int n = LocalizationManager.SupportedLanguages.Length;
            _langIdx = ((_langIdx + delta) % n + n) % n;
            string code = LocalizationManager.SupportedLanguages[_langIdx];
            await _locale.SetLanguageAsync(code).ConfigureAwait(false);
            _status = "";
        }
        catch
        {
            _status = "";
            Interlocked.Exchange(ref _menuTextsRefreshPending, 1);
        }
        finally
        {
            _busyLang = false;
        }
    }

    public void Dispose()
    {
        _renderer.Dispose();
        _audio.Dispose();
        _translator.Dispose();
        _window.Dispose();
        GC.SuppressFinalize(this);
    }
}
