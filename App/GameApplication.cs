using System.Diagnostics;
using Silk.NET.SDL;
using VoidRunner.Graphics;
using VoidRunner.Localization;
using VoidRunner.Models;
using VoidRunner.Services;

namespace VoidRunner.App;

internal enum AppPhase
{
    Menu,
    Game,
}

public sealed class GameApplication : IDisposable
{
    private static readonly KeyCode[] TrackedKeys =
    [
        KeyCode.W, KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.Up, KeyCode.Down, KeyCode.Left, KeyCode.Right,
        KeyCode.P, KeyCode.Escape, KeyCode.Return, KeyCode.Backspace, KeyCode.Space, KeyCode.Minus,
        KeyCode.Y, KeyCode.N, KeyCode.LShift, KeyCode.RShift,
        KeyCode.Zero, KeyCode.One, KeyCode.Two, KeyCode.Three, KeyCode.Four,
        KeyCode.Five, KeyCode.Six, KeyCode.Seven, KeyCode.Eight, KeyCode.Nine,
        KeyCode.B, KeyCode.C, KeyCode.E, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.I, KeyCode.J,
        KeyCode.K, KeyCode.L, KeyCode.M, KeyCode.O, KeyCode.Q, KeyCode.R, KeyCode.T, KeyCode.U, KeyCode.V,
        KeyCode.X, KeyCode.Z
    ];

    private readonly Sdl _sdl;
    private readonly SdlContext _sdlContext;
    private unsafe Window* _window;

    private readonly GlRenderer2D _renderer;
    private readonly TranslationService _translator = new();
    private readonly LocalizationManager _locale;
    private readonly ScoreService _scores = new();
    private readonly AudioService _audio;

    private AppPhase _phase = AppPhase.Menu;
    private GameSession? _session;

    private readonly HashSet<KeyCode> _prevKeys = [];
    private readonly HashSet<KeyCode> _currKeys = [];
    private readonly Dictionary<KeyCode, bool> _typeKeyPrev = [];

    private readonly (float x, float y, float speed, float size)[] _menuStars;
    private readonly Random _rnd = new();
    private int _langIdx;
    private string _btnPlay = "";
    private string _btnScores = "";
    private string _btnQuit = "";
    private string _status = "";
    private bool _busyLang;
    private bool _scoresOverlay;
    private string _scoresBody = "";

    private const float MenuBtnW = 220f;
    private const float MenuBtnH = 52f;
    private static float MenuBtnX => (GameConstants.CanvasWidth - MenuBtnW) / 2f;

    private byte _mousePrimaryPrev;
    private int _mouseX;
    private int _mouseY;
    private int _drawableW = GameConstants.CanvasWidth;
    private int _drawableH = GameConstants.CanvasHeight;

    private int _menuTextsRefreshPending;
    private bool _requestQuit;

    public GameApplication()
    {
        _sdlContext = new SdlContext();
        _sdl = new Sdl(_sdlContext);

        _locale = new LocalizationManager(_translator);
        _locale.LanguageChanged += () => Interlocked.Exchange(ref _menuTextsRefreshPending, 1);

        _menuStars = Enumerable.Range(0, 120).Select(_ => (
            x: (float)_rnd.NextDouble() * GameConstants.CanvasWidth,
            y: (float)_rnd.NextDouble() * GameConstants.CanvasHeight,
            speed: 0.3f + (float)_rnd.NextDouble() * 1.2f,
            size: 0.8f + (float)_rnd.NextDouble() * 2f)).ToArray();

        CreateWindowAndGl();

        unsafe
        {
            _renderer = new GlRenderer2D(_sdl, _window);
        }

        _renderer.Init();
        _audio = new AudioService();

        RefreshMenuTextsSafe();
        _langIdx = 0;

        _ = WarmInitialLocaleAsync();
    }

    private unsafe void CreateWindowAndGl()
    {
        int init = _sdl.Init(Sdl.InitVideo | Sdl.InitAudio | Sdl.InitEvents | Sdl.InitTimer |
                             Sdl.InitGamecontroller | Sdl.InitJoystick);
        if (init < 0)
            throw new InvalidOperationException("Failed to initialize SDL.");

        uint flags = (uint)WindowFlags.Resizable | (uint)WindowFlags.AllowHighdpi;
        _window = (Window*)_sdl.CreateWindow(
            "Void Runner",
            Sdl.WindowposUndefined,
            Sdl.WindowposUndefined,
            GameConstants.CanvasWidth,
            GameConstants.CanvasHeight,
            flags);

        if (_window == null)
        {
            var winErr = _sdl.GetErrorAsException();
            if (winErr != null) throw winErr;
            throw new InvalidOperationException("Failed to create SDL window.");
        }

        int dw, dh;
        _sdl.GetWindowSize(_window, &dw, &dh);
        _drawableW = dw;
        _drawableH = dh;
    }

    // AI-generated
    public void Run()
    {
        bool quit = false;
        var timer = Stopwatch.StartNew();
        Span<byte> mouseButtonStates = stackalloc byte[(int)MouseButton.Count];
        var ev = new Event();

        while (!quit)
        {
            while (_sdl.PollEvent(ref ev) != 0)
            {
                if (ev.Type == (uint)EventType.Quit)
                {
                    quit = true;
                    break;
                }

                switch (ev.Type)
                {
                    case (uint)EventType.Windowevent when ev.Window.Event == (byte)WindowEventID.Close:
                        quit = true;
                        break;

                    case (uint)EventType.Mousemotion:
                        _mouseX = ev.Motion.X;
                        _mouseY = ev.Motion.Y;
                        break;

                    case (uint)EventType.Fingerdown:
                        mouseButtonStates[(byte)MouseButton.Primary] = 1;
                        break;

                    case (uint)EventType.Mousebuttondown:
                        mouseButtonStates[ev.Button.Button] = 1;
                        break;

                    case (uint)EventType.Fingerup:
                        mouseButtonStates[(byte)MouseButton.Primary] = 0;
                        break;

                    case (uint)EventType.Mousebuttonup:
                        mouseButtonStates[ev.Button.Button] = 0;
                        break;
                }
            }

            if (quit) break;

            ReadOnlySpan<byte> keyboardState;
            unsafe
            {
                keyboardState = new(_sdl.GetKeyboardState(null), (int)KeyCode.Count);
            }

            float dt = (float)Math.Min(timer.Elapsed.TotalSeconds, 0.05);
            timer.Restart();

            unsafe
            {
                int dw, dh;
                _sdl.GetWindowSize(_window, &dw, &dh);
                _drawableW = dw;
                _drawableH = dh;
            }

            if (Interlocked.Exchange(ref _menuTextsRefreshPending, 0) != 0)
                RefreshMenuTextsSafe();

            SyncKeyboardState(keyboardState);

            bool leftNow = mouseButtonStates[(byte)MouseButton.Primary] != 0;
            bool leftClickEdge = leftNow && _mousePrimaryPrev == 0;
            _mousePrimaryPrev = leftNow ? (byte)1 : (byte)0;

            Update(dt, leftClickEdge);
            Render();


            if (_requestQuit)
                quit = true;
        }
    }
    // end AI-generated

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

    private void SyncKeyboardState(ReadOnlySpan<byte> keyboardState)
    {
        _prevKeys.Clear();
        foreach (KeyCode k in _currKeys) _prevKeys.Add(k);

        _currKeys.Clear();
        foreach (KeyCode k in TrackedKeys)
        {
            if (keyboardState[(byte)k] != 0)
                _currKeys.Add(k);
        }
    }

    private bool Down(KeyCode k) => _currKeys.Contains(k);

    private bool Edge(KeyCode k) => _currKeys.Contains(k) && !_prevKeys.Contains(k);

    private void Update(float dt, bool leftClickEdge)
    {
        if (_phase == AppPhase.Menu)
        {
            UpdateMenu(dt, leftClickEdge);
            return;
        }

        if (_session is null) return;

        bool overlay = HandleGameOverlayInput();
        if (_session is null) return;

        if (!overlay &&
            !_session.GameOverBlocksPause() &&
            (Edge(KeyCode.P) || Edge(KeyCode.Escape)))
            _session.TogglePause();

        if (!_session.GameOverBlocksPause())
            FillMovementKeys(_session.MovementKeys);

        _session.Update(dt);
    }

    private bool HandleGameOverlayInput()
    {
        if (_session is null) return false;

        if (_session.GoPhaseIsNameEntry())
        {
            if (Edge(KeyCode.Return))
            {
                _session.SubmitName();
                return true;
            }

            if (Edge(KeyCode.Escape))
            {
                _session.CancelNameEntry();
                return true;
            }

            if (Edge(KeyCode.Backspace))
                _session.NameBackspace();

            TryTypeChar(KeyCode.A, 'a');
            TryTypeChar(KeyCode.B, 'b');
            TryTypeChar(KeyCode.C, 'c');
            TryTypeChar(KeyCode.D, 'd');
            TryTypeChar(KeyCode.E, 'e');
            TryTypeChar(KeyCode.F, 'f');
            TryTypeChar(KeyCode.G, 'g');
            TryTypeChar(KeyCode.H, 'h');
            TryTypeChar(KeyCode.I, 'i');
            TryTypeChar(KeyCode.J, 'j');
            TryTypeChar(KeyCode.K, 'k');
            TryTypeChar(KeyCode.L, 'l');
            TryTypeChar(KeyCode.M, 'm');
            TryTypeChar(KeyCode.N, 'n');
            TryTypeChar(KeyCode.O, 'o');
            TryTypeChar(KeyCode.P, 'p');
            TryTypeChar(KeyCode.Q, 'q');
            TryTypeChar(KeyCode.R, 'r');
            TryTypeChar(KeyCode.S, 's');
            TryTypeChar(KeyCode.T, 't');
            TryTypeChar(KeyCode.U, 'u');
            TryTypeChar(KeyCode.V, 'v');
            TryTypeChar(KeyCode.W, 'w');
            TryTypeChar(KeyCode.X, 'x');
            TryTypeChar(KeyCode.Y, 'y');
            TryTypeChar(KeyCode.Z, 'z');
            TryTypeChar(KeyCode.Zero, '0');
            TryTypeChar(KeyCode.One, '1');
            TryTypeChar(KeyCode.Two, '2');
            TryTypeChar(KeyCode.Three, '3');
            TryTypeChar(KeyCode.Four, '4');
            TryTypeChar(KeyCode.Five, '5');
            TryTypeChar(KeyCode.Six, '6');
            TryTypeChar(KeyCode.Seven, '7');
            TryTypeChar(KeyCode.Eight, '8');
            TryTypeChar(KeyCode.Nine, '9');
            TryTypeChar(KeyCode.Space, ' ');
            TryTypeChar(KeyCode.Minus, '-');
            return true;
        }

        if (!_session.GoPhaseIsAskAgain()) return false;

        if (Edge(KeyCode.Y))
        {
            _audio.PlayMenuSelect();
            _session.PlayAgainYes();
            return true;
        }

        if (Edge(KeyCode.N) || Edge(KeyCode.Escape))
        {
            _audio.PlayMenuSelect();
            _session.PlayAgainNo();
            return true;
        }

        return true;
    }


    // AI-generated
    private void TryTypeChar(KeyCode k, char chLow)
    {
        if (_session is null || !_session.GoPhaseIsNameEntry()) return;

        bool now = Down(k);
        _typeKeyPrev.TryGetValue(k, out bool was);
        if (now == was) return;

        _typeKeyPrev[k] = now;
        if (!now) return;

        bool shift = Down(KeyCode.LShift) || Down(KeyCode.RShift);
        char ch = shift && char.IsLetter(chLow) ? char.ToUpperInvariant(chLow) : chLow;
        if (shift && chLow == '-')
            ch = '_';

        _session.NameChar(ch);
    }
    // end AI-generated

    private void FillMovementKeys(HashSet<KeyCode> dest)
    {
        dest.Clear();
        void H(KeyCode k)
        {
            if (Down(k)) dest.Add(k);
        }

        H(KeyCode.W);
        H(KeyCode.A);
        H(KeyCode.S);
        H(KeyCode.D);
        H(KeyCode.Up);
        H(KeyCode.Down);
        H(KeyCode.Left);
        H(KeyCode.Right);
    }

    private void StartGame()
    {
        _audio.PlayMenuSelect();
        _session = new GameSession(_locale, _scores, _audio, ReturnToMenu);
        _phase = AppPhase.Game;
    }

    private void ReturnToMenu()
    {
        _phase = AppPhase.Menu;
        _session = null;
    }

    private void UpdateMenu(float dt, bool leftClickEdge)
    {
        if (_busyLang) return;

        if (_scoresOverlay)
        {
            if (Edge(KeyCode.Escape) || leftClickEdge)
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

        if (Edge(KeyCode.Escape))
        {
            _requestQuit = true;
            return;
        }

        if (Edge(KeyCode.Left))
            _ = CycleLanguageAsync(-1);
        if (Edge(KeyCode.Right))
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
            _requestQuit = true;
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

    private void Render()
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
        float sx = GameConstants.CanvasWidth / (float)Math.Max(1, _drawableW);
        float sy = GameConstants.CanvasHeight / (float)Math.Max(1, _drawableH);
        return (_mouseX * sx, _mouseY * sy);
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
        _btnPlay = _locale.Get("Play");
        _btnScores = _locale.Get("High Scores");
        _btnQuit = _locale.Get("Quit");
    }

    private async Task CycleLanguageAsync(int delta)
    {
        if (_busyLang) return;
        _busyLang = true;
        _status = "…";

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

        unsafe
        {
            if (_window != null)
            {
                _sdl.DestroyWindow(_window);
                _window = null;
            }
        }

        _sdl.Quit();
        _audio.Dispose();
        _translator.Dispose();
        _sdlContext.Dispose();
        GC.SuppressFinalize(this);
    }
}