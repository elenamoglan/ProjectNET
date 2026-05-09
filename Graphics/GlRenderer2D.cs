using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Silk.NET.OpenGL;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace VoidRunner.Graphics;

public sealed class GlRenderer2D : IGameRenderer, IDisposable
{
    private readonly IWindow _window;
    private GL _gl = null!;
    private uint _colorProgram;
    private uint _texProgram;
    private int _uMvpColor;
    private int _uMvpTex;
    private int _uTexColor;
    private int _uSampler;
    private uint _vao;
    private uint _colorVbo;
    private uint _texVbo;
    private Matrix4X4<float> _mvp;

    private int _vw;
    private int _vh;
    private bool _initialized;
    private readonly List<float> _colorVerts = [];

    private readonly Dictionary<TextCacheKey, TexEntry> _textCache = new();
    private static FontFamily? _fontFamily;

    private readonly record struct TextCacheKey(string Text, float Size, bool Bold);

    private readonly record struct TexEntry(uint Id, float W, float H);

    public GlRenderer2D(IWindow window)
    {
        _window = window;
        _vw = window.FramebufferSize.X;
        _vh = window.FramebufferSize.Y;

        window.FramebufferResize += sz =>
        {
            ApplyFramebufferDimensions(sz.X, sz.Y);
        };
    }

    public int Width => _vw;
    public int Height => _vh;

    public unsafe void Init()
    {
        _gl = GL.GetApi(_window);
        ApplyFramebufferDimensions(_window.FramebufferSize.X, _window.FramebufferSize.Y);
        if (_vw <= 1 || _vh <= 1)
            ApplyFramebufferDimensions(_window.Size.X, _window.Size.Y);
        _gl.Viewport(0, 0, (uint)Math.Max(1, _vw), (uint)Math.Max(1, _vh));
        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        const string vsColor = """
#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec4 aCol;
uniform mat4 uMVP;
out vec4 vCol;
void main(){ vCol=aCol; gl_Position=uMVP*vec4(aPos,0,1); }
""";
        const string fsColor = """
#version 330 core
in vec4 vCol;
out vec4 fragColor;
void main(){ fragColor=vCol; }
""";
        const string vsTex = """
#version 330 core
layout(location=0) in vec2 aPos;
layout(location=1) in vec2 aUv;
uniform mat4 uMVP;
out vec2 vUv;
void main(){ vUv=aUv; gl_Position=uMVP*vec4(aPos,0,1); }
""";
        const string fsTex = """
#version 330 core
in vec2 vUv;
uniform sampler2D uTex;
uniform vec4 uColor;
out vec4 fragColor;
void main(){ vec4 t=texture(uTex,vUv); fragColor=vec4(uColor.rgb*t.rgb, uColor.a*t.a); }
""";

        _colorProgram = CreateProgram(vsColor, fsColor);
        _texProgram = CreateProgram(vsTex, fsTex);
        _uMvpColor = _gl.GetUniformLocation(_colorProgram, "uMVP");
        _uMvpTex = _gl.GetUniformLocation(_texProgram, "uMVP");
        _uTexColor = _gl.GetUniformLocation(_texProgram, "uColor");
        _uSampler = _gl.GetUniformLocation(_texProgram, "uTex");

        _gl.GenVertexArrays(1, out _vao);
        _gl.GenBuffers(1, out _colorVbo);
        _gl.GenBuffers(1, out _texVbo);

        BindColorLayout();
        _initialized = true;
    }

    private void ApplyFramebufferDimensions(int fbWidth, int fbHeight)
    {
        int w = fbWidth > 0 ? fbWidth : _window.Size.X;
        int h = fbHeight > 0 ? fbHeight : _window.Size.Y;
        w = Math.Max(w, 1);
        h = Math.Max(h, 1);

        _vw = w;
        _vh = h;
        UpdateMvp();

        if (_gl is null) return;
        _gl.Viewport(0, 0, (uint)_vw, (uint)_vh);
    }

    private unsafe void BindColorLayout()
    {
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _colorVbo);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 24, null);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 24, (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);
    }

    private unsafe void BindTexLayout()
    {
        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _texVbo);
        _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 16, null);
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 16, (void*)(2 * sizeof(float)));
        _gl.EnableVertexAttribArray(1);
    }

    private void UpdateMvp()
    {
        _mvp = Matrix4X4.CreateOrthographicOffCenter(0f, _vw, 0f, _vh, -1f, 1f);
    }

    private float GlY(float gameY) => _vh - gameY;

    private unsafe void UniformMvp(int loc)
    {
        Matrix4X4<float> m = _mvp;
        _gl.UniformMatrix4(loc, 1, false, (float*)&m);
    }

    private void AddTri(float ax, float ay, Vector4 ca, float bx, float by, Vector4 cb, float cx, float cy, Vector4 cc)
    {
        void vert(float x, float y, Vector4 c)
        {
            _colorVerts.Add(x); _colorVerts.Add(y); _colorVerts.Add(c.X); _colorVerts.Add(c.Y); _colorVerts.Add(c.Z); _colorVerts.Add(c.W);
        }

        vert(ax, ay, ca);
        vert(bx, by, cb);
        vert(cx, cy, cc);
    }

    private static Vector4 V4(ColorRgba fill)
    {
        var v = fill.ToVector4();
        return new Vector4(v.X, v.Y, v.Z, v.W);
    }

    public void BeginFrame()
    {
        if (!_initialized) return;

        int fx = _window.FramebufferSize.X;
        int fy = _window.FramebufferSize.Y;

        if (fx < 1 || fy < 1)
            ApplyFramebufferDimensions(Math.Max(1, _window.Size.X), Math.Max(1, _window.Size.Y));
        else if (fx != _vw || fy != _vh)
            ApplyFramebufferDimensions(fx, fy);

        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.Viewport(0, 0, (uint)Math.Max(_vw, 1), (uint)Math.Max(_vh, 1));

        _gl.ClearColor(0.024f, 0.020f, 0.058f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _colorVerts.Clear();
    }

    public unsafe void EndFrame()
    {
        FlushColor();
    }

    private unsafe void FlushColor()
    {
        if (_colorVerts.Count == 0) return;

        BindColorLayout();

        _gl.UseProgram(_colorProgram);
        UniformMvp(_uMvpColor);

        Span<float> span = CollectionsMarshal.AsSpan(_colorVerts);
        fixed (float* ptr = span)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(span.Length * sizeof(float)), ptr, BufferUsageARB.StreamDraw);

        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)(span.Length / 6));
        _colorVerts.Clear();
    }

    public void FillRect(float x, float y, float w, float h, ColorRgba fill)
    {
        var c = V4(fill);
        float x0 = x, x1 = x + w;
        float gy0 = GlY(y), gy1 = GlY(y + h);
        float yTop = Math.Max(gy0, gy1);
        float yBot = Math.Min(gy0, gy1);
        AddTri(x0, yTop, c, x1, yTop, c, x1, yBot, c);
        AddTri(x0, yTop, c, x1, yBot, c, x0, yBot, c);
    }

    public void FillEllipse(float x, float y, float ew, float eh, ColorRgba fill)
    {
        var c = V4(fill);
        float cx = x + ew * 0.5f;
        float cy = y + eh * 0.5f;
        float rx = ew * 0.5f;
        float ry = eh * 0.5f;
        const int seg = 32;
        float gcx = cx;
        float gcyGl = GlY(cy);
        for (int i = 0; i < seg; i++)
        {
            float t0 = i / (float)seg * MathF.Tau;
            float t1 = (i + 1f) / (float)seg * MathF.Tau;
            float x0 = cx + MathF.Cos(t0) * rx;
            float y0g = cy + MathF.Sin(t0) * ry;
            float x1 = cx + MathF.Cos(t1) * rx;
            float y1g = cy + MathF.Sin(t1) * ry;
            AddTri(gcx, gcyGl, c, x0, GlY(y0g), c, x1, GlY(y1g), c);
        }
    }

    public unsafe void StrokeEllipse(float x, float y, float ew, float eh, ColorRgba stroke, float lineWidth)
    {
        FlushColor();
        _gl.LineWidth(lineWidth);
        var c = V4(stroke);
        float cx = x + ew * 0.5f;
        float cy = y + eh * 0.5f;
        float rx = ew * 0.5f;
        float ry = eh * 0.5f;
        const int seg = 36;

        BindColorLayout();
        _gl.UseProgram(_colorProgram);
        UniformMvp(_uMvpColor);

        Span<float> ring = stackalloc float[seg * 6];
        for (int i = 0; i < seg; i++)
        {
            float t = i / (float)seg * MathF.Tau;
            float gx = cx + MathF.Cos(t) * rx;
            float gyGame = cy + MathF.Sin(t) * ry;
            ring[i * 6 + 0] = gx;
            ring[i * 6 + 1] = GlY(gyGame);
            ring[i * 6 + 2] = c.X;
            ring[i * 6 + 3] = c.Y;
            ring[i * 6 + 4] = c.Z;
            ring[i * 6 + 5] = c.W;
        }

        fixed (float* p = ring)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(ring.Length * sizeof(float)), p, BufferUsageARB.StreamDraw);

        _gl.DrawArrays(PrimitiveType.LineLoop, 0, seg);
    }

    public void FillPolygon(ReadOnlySpan<(float x, float y)> verts, ColorRgba fill)
    {
        if (verts.Length < 3) return;
        var c = V4(fill);
        float gx0 = verts[0].x;
        float gy0Gl = GlY(verts[0].y);
        for (int i = 1; i < verts.Length - 1; i++)
        {
            float x1 = verts[i].x, y1 = verts[i].y;
            float x2 = verts[i + 1].x, y2 = verts[i + 1].y;
            AddTri(gx0, gy0Gl, c, x1, GlY(y1), c, x2, GlY(y2), c);
        }
    }

    public unsafe void StrokePolygon(ReadOnlySpan<(float x, float y)> verts, ColorRgba stroke, float lineWidth)
    {
        if (verts.Length < 2) return;
        FlushColor();
        _gl.LineWidth(lineWidth);
        var c = V4(stroke);
        int n = verts.Length;

        BindColorLayout();
        _gl.UseProgram(_colorProgram);
        UniformMvp(_uMvpColor);

        Span<float> line = stackalloc float[n * 6];
        for (int i = 0; i < n; i++)
        {
            line[i * 6 + 0] = verts[i].x;
            line[i * 6 + 1] = GlY(verts[i].y);
            line[i * 6 + 2] = c.X;
            line[i * 6 + 3] = c.Y;
            line[i * 6 + 4] = c.Z;
            line[i * 6 + 5] = c.W;
        }

        fixed (float* p = line)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(line.Length * sizeof(float)), p, BufferUsageARB.StreamDraw);

        _gl.DrawArrays(PrimitiveType.LineLoop, 0, (uint)n);
    }

    public void DrawString(string text, float x, float y, float fontSizeDip, bool bold, ColorRgba color)
    {
        TexEntry te = EnsureTextTexture(text, fontSizeDip, bold);
        DrawTextureInternal(te.Id, x, y, te.W, te.H, color);
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
        int tw = Math.Max(1, (int)Math.Ceiling(bounds.Width) + 3);
        int th = Math.Max(1, (int)Math.Ceiling(bounds.Height) + 3);

        using Image<Rgba32> img = new(tw, th, new Rgba32(0, 0, 0, 0));
        img.Mutate(ctx =>
            ctx.DrawText(text, font, SixLabors.ImageSharp.Color.White, new PointF(-bounds.X, -bounds.Y)));

        byte[] raw = new byte[tw * th * 4];
        img.CopyPixelDataTo(raw);
        uint tex = CreateTextureRgba(raw, tw, th);
        var entry = new TexEntry(tex, tw, th);
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
    {
        uint tex;
        _gl.GenTextures(1, out tex);
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        unsafe
        {
            fixed (byte* p = rgba)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba, (uint)w, (uint)h, 0, PixelFormat.Rgba,
                    PixelType.UnsignedByte, p);
        }

        return tex;
    }

    public void DrawTexture(uint texture, float x, float y, float w, float h, ColorRgba tint)
        => DrawTextureInternal(texture, x, y, w, h, tint);

    private unsafe void DrawTextureInternal(uint texture, float x, float y, float fw, float fh, ColorRgba tint)
    {
        FlushColor();

        float x0 = x, x1 = x + fw;
        float gy0 = GlY(y), gy1 = GlY(y + fh);
        float yTop = Math.Max(gy0, gy1);
        float yBot = Math.Min(gy0, gy1);

        Span<float> q =
        [
            x0, yTop, 0f, 0f,
            x1, yTop, 1f, 0f,
            x1, yBot, 1f, 1f,
            x0, yTop, 0f, 0f,
            x1, yBot, 1f, 1f,
            x0, yBot, 0f, 1f,
        ];

        BindTexLayout();

        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, texture);

        _gl.UseProgram(_texProgram);
        UniformMvp(_uMvpTex);

        var tv = tint.ToVector4();
        _gl.Uniform4(_uTexColor, tv.X, tv.Y, tv.Z, tv.W);
        _gl.Uniform1(_uSampler, 0);

        fixed (float* p = q)
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(q.Length * sizeof(float)), p, BufferUsageARB.StreamDraw);

        _gl.DrawArrays(PrimitiveType.Triangles, 0, 6);

        BindColorLayout();
    }

    public void DeleteTexture(uint texture)
    {
        if (texture == 0) return;
        uint t = texture;
        _gl.DeleteTextures(1, ref t);
    }

    private uint CreateProgram(string vsSrc, string fsSrc)
    {
        uint v = CompileShader(ShaderType.VertexShader, vsSrc);
        uint f = CompileShader(ShaderType.FragmentShader, fsSrc);
        uint p = _gl.CreateProgram();
        _gl.AttachShader(p, v);
        _gl.AttachShader(p, f);
        _gl.LinkProgram(p);
        _gl.GetProgram(p, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked != (int)GLEnum.True)
        {
            string log = _gl.GetProgramInfoLog(p);
            _gl.DeleteProgram(p);
            _gl.DeleteShader(v);
            _gl.DeleteShader(f);
            throw new InvalidOperationException($"GL program failed to link: {log}");
        }

        _gl.DeleteShader(v);
        _gl.DeleteShader(f);
        return p;
    }

    private uint CompileShader(ShaderType type, string src)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, src);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int ok);
        if (ok != (int)GLEnum.True)
        {
            string log = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new InvalidOperationException($"Shader {type} failed: {log}");
        }

        return shader;
    }

    public void Dispose()
    {
        if (!_initialized) return;

        try
        {
            foreach (var e in _textCache.Values)
            {
                uint id = e.Id;
                _gl.DeleteTextures(1, ref id);
            }

            _textCache.Clear();
            if (_colorVbo != 0) _gl.DeleteBuffers(1, ref _colorVbo);
            if (_texVbo != 0) _gl.DeleteBuffers(1, ref _texVbo);
            if (_vao != 0) _gl.DeleteVertexArrays(1, ref _vao);
            if (_colorProgram != 0) _gl.DeleteProgram(_colorProgram);
            if (_texProgram != 0) _gl.DeleteProgram(_texProgram);
        }
        catch (InvalidOperationException)
        {
        }
    }
}
