using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Silk.NET.SDL;
using VoidRunner.Models;

namespace VoidRunner.Graphics;

public sealed unsafe class GlRenderer2D : IGameRenderer, IDisposable
{
    private readonly Sdl _sdl;
    private readonly Window* _window;
    private Renderer* _renderer;

    private int _vw;
    private int _vh;
    private bool _initialized;

    private readonly Dictionary<TextCacheKey, TexEntry> _textCache = new();
    private static FontFamily? _fontFamily;

    private readonly record struct TextCacheKey(string Text, float Size, bool Bold);
    private readonly record struct TexEntry(nint Id, float W, float H);

    public GlRenderer2D(Sdl sdl, Window* window)
    {
        _sdl = sdl;
        _window = window;
    }

    public int Width => _vw > 0 ? _vw : GameConstants.CanvasWidth;
    public int Height => _vh > 0 ? _vh : GameConstants.CanvasHeight;

    public void Init()
    {
        _renderer = _sdl.CreateRenderer(_window, -1,
            (uint)(RendererFlags.Accelerated | RendererFlags.Presentvsync));

        if (_renderer == null)
            throw new InvalidOperationException(
                $"SDL_CreateRenderer failed: {_sdl.GetErrorS()}");

        _sdl.SetHint("SDL_RENDER_SCALE_QUALITY", "1");
        _sdl.RenderSetLogicalSize(_renderer,
            GameConstants.CanvasWidth, GameConstants.CanvasHeight);

        PollDrawableSize();
        _initialized = true;
    }

    private void PollDrawableSize()
    {
        int w, h;
        _sdl.GetWindowSize(_window, &w, &h);
        if (w < 1) w = GameConstants.CanvasWidth;
        if (h < 1) h = GameConstants.CanvasHeight;
        _vw = w;
        _vh = h;
    }

    public void BeginFrame()
    {
        if (!_initialized) return;
        PollDrawableSize();
        // Match original glClearColor(0.024, 0.020, 0.058, 1)
        _sdl.SetRenderDrawColor(_renderer, 6, 5, 15, 255);
        _sdl.RenderClear(_renderer);
    }

    public void EndFrame()
    {
        if (!_initialized) return;
        _sdl.RenderPresent(_renderer);
    }

    public void FillRect(float x, float y, float w, float h, ColorRgba fill)
    {
        if (!_initialized) return;
        SetDrawColor(fill);
        var rect = new FRect { X = x, Y = y, W = w, H = h };
        _sdl.RenderFillRectF(_renderer, &rect);
    }

    public void FillEllipse(float x, float y, float ew, float eh, ColorRgba fill)
    {
        if (!_initialized) return;
        SetDrawColor(fill);
        float cx = x + ew * 0.5f;
        float cy = y + eh * 0.5f;
        float rx = ew * 0.5f;
        float ry = eh * 0.5f;
        // Scanline fill — one horizontal rect per pixel row
        for (float dy = -ry; dy <= ry; dy += 1f)
        {
            float dx = rx * MathF.Sqrt(Math.Max(0f, 1f - (dy * dy) / (ry * ry)));
            var line = new FRect { X = cx - dx, Y = cy + dy, W = dx * 2f, H = 1f };
            _sdl.RenderFillRectF(_renderer, &line);
        }
    }

    public void StrokeEllipse(float x, float y, float ew, float eh, ColorRgba stroke, float lineWidth)
    {
        if (!_initialized) return;
        SetDrawColor(stroke);
        float cx = x + ew * 0.5f;
        float cy = y + eh * 0.5f;
        float rx = ew * 0.5f;
        float ry = eh * 0.5f;
        const int seg = 36;
        for (int i = 0; i < seg; i++)
        {
            float t0 = i / (float)seg * MathF.Tau;
            float t1 = (i + 1) / (float)seg * MathF.Tau;
            _sdl.RenderDrawLineF(_renderer,
                cx + MathF.Cos(t0) * rx, cy + MathF.Sin(t0) * ry,
                cx + MathF.Cos(t1) * rx, cy + MathF.Sin(t1) * ry);
        }
    }

    public void FillPolygon(ReadOnlySpan<(float x, float y)> verts, ColorRgba fill)
    {
        if (!_initialized || verts.Length < 3) return;
        SetDrawColor(fill);
        // Triangle fan from first vertex
        for (int i = 1; i < verts.Length - 1; i++)
        {
            // Rasterise each triangle as scanlines
            FillTriangle(
                verts[0].x, verts[0].y,
                verts[i].x, verts[i].y,
                verts[i + 1].x, verts[i + 1].y);
        }
    }

    private void FillTriangle(float ax, float ay, float bx, float by, float cx, float cy)
    {
        // Sort vertices by Y
        if (ay > by) { (ax, ay, bx, by) = (bx, by, ax, ay); }
        if (ay > cy) { (ax, ay, cx, cy) = (cx, cy, ax, ay); }
        if (by > cy) { (bx, by, cx, cy) = (cx, cy, bx, by); }

        float totalH = cy - ay;
        if (totalH < 1f) return;

        for (float y = ay; y <= cy; y += 1f)
        {
            bool secondHalf = y > by;
            float segH = secondHalf ? cy - by : by - ay;
            if (segH < 0.001f) segH = 0.001f;
            float alpha = (y - ay) / totalH;
            float beta = secondHalf ? (y - by) / segH : (y - ay) / segH;
            float x1 = ax + (cx - ax) * alpha;
            float x2 = secondHalf ? bx + (cx - bx) * beta : ax + (bx - ax) * beta;
            if (x1 > x2) (x1, x2) = (x2, x1);
            var line = new FRect { X = x1, Y = y, W = x2 - x1 + 1f, H = 1f };
            _sdl.RenderFillRectF(_renderer, &line);
        }
    }

    public void StrokePolygon(ReadOnlySpan<(float x, float y)> verts, ColorRgba stroke, float lineWidth)
    {
        if (!_initialized || verts.Length < 2) return;
        SetDrawColor(stroke);
        int n = verts.Length;
        var pts = stackalloc FPoint[n + 1];
        for (int i = 0; i < n; i++)
            pts[i] = new FPoint { X = verts[i].x, Y = verts[i].y };
        pts[n] = pts[0]; // close the loop
        _sdl.RenderDrawLinesF(_renderer, pts, n + 1);
    }

    public void DrawString(string text, float x, float y, float fontSizeDip, bool bold, ColorRgba color)
    {
        if (!_initialized) return;
        TexEntry te = EnsureTextTexture(text, fontSizeDip, bold);
        DrawTextureInternal((Texture*)te.Id, x, y, te.W, te.H, color);
    }

    public (float w, float h) MeasureString(string text, float fontSizeDip, bool bold)
    {
        TexEntry te = EnsureTextTexture(text, fontSizeDip, bold);
        return (te.W, te.H);
    }

    private TexEntry EnsureTextTexture(string text, float size, bool bold)
    {
        var key = new TextCacheKey(text, size, bold);
        if (_textCache.TryGetValue(key, out var existing))
            return existing;

        FontFamily fam = _fontFamily ??= ResolveFontFamily();
        Font font = fam.CreateFont(size, bold ? FontStyle.Bold : FontStyle.Regular);
        TextOptions textOpts = new(font);
        FontRectangle bounds = TextMeasurer.MeasureBounds(text, textOpts);
        int tw = Math.Max(1, (int)MathF.Ceiling(bounds.Width) + 3);
        int th = Math.Max(1, (int)MathF.Ceiling(bounds.Height) + 3);

        using Image<Rgba32> img = new(tw, th, new Rgba32(0, 0, 0, 0));
        img.Mutate(ctx =>
            ctx.DrawText(text, font, SixLabors.ImageSharp.Color.White,
                         new PointF(-bounds.X, -bounds.Y)));

        byte[] raw = new byte[tw * th * 4];
        img.CopyPixelDataTo(raw);

        Texture* tex = CreateSdlTextureRgba(raw, tw, th);
        var entry = new TexEntry((nint)tex, tw, th);
        _textCache[key] = entry;
        return entry;
    }

    private static FontFamily ResolveFontFamily()
    {
        if (SystemFonts.TryGet("Segoe UI", out FontFamily ui)) return ui;
        foreach (FontFamily family in SystemFonts.Collection.Families) return family;
        throw new InvalidOperationException("No fonts available.");
    }

    public uint CreateTextureRgba(ReadOnlySpan<byte> rgba, int w, int h)
        => (uint)(nint)CreateSdlTextureRgba(rgba, w, h);

    private Texture* CreateSdlTextureRgba(ReadOnlySpan<byte> rgba, int w, int h)
    {
        Texture* tex = _sdl.CreateTexture(_renderer,
            (uint)PixelFormatEnum.Rgba32,
            (int)TextureAccess.Static, w, h);

        if (tex == null)
            throw new InvalidOperationException(
                $"SDL_CreateTexture failed: {_sdl.GetErrorS()}");

        _sdl.SetTextureBlendMode(tex, BlendMode.Blend);

        fixed (byte* p = rgba)
            _sdl.UpdateTexture(tex, null, p, w * 4);

        return tex;
    }

    public void DrawTexture(uint texture, float x, float y, float w, float h, ColorRgba tint)
        => DrawTextureInternal((Texture*)(nint)texture, x, y, w, h, tint);

    private void DrawTextureInternal(Texture* tex, float x, float y, float fw, float fh, ColorRgba tint)
    {
        if (!_initialized || tex == null) return;
        _sdl.SetTextureColorMod(tex, tint.R, tint.G, tint.B);
        _sdl.SetTextureAlphaMod(tex, tint.A);
        var dst = new FRect { X = x, Y = y, W = fw, H = fh };
        _sdl.RenderCopyF(_renderer, tex, null, &dst);
    }

    public void DeleteTexture(uint texture)
    {
        if (texture == 0) return;
        _sdl.DestroyTexture((Texture*)(nint)texture);
    }

    private void SetDrawColor(ColorRgba c)
    {
        _sdl.SetRenderDrawColor(_renderer, c.R, c.G, c.B, c.A);
        _sdl.SetRenderDrawBlendMode(_renderer, BlendMode.Blend);
    }

    public void Dispose()
    {
        if (!_initialized) return;
        foreach (var e in _textCache.Values)
            _sdl.DestroyTexture((Texture*)e.Id);
        _textCache.Clear();
        if (_renderer != null)
        {
            _sdl.DestroyRenderer(_renderer);
            _renderer = null;
        }
    }
}