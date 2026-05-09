namespace VoidRunner.Graphics;

public interface IGameRenderer
{
    int Width { get; }
    int Height { get; }

    void BeginFrame();
    void EndFrame();

    void FillRect(float x, float y, float w, float h, ColorRgba fill);
    void FillEllipse(float x, float y, float w, float h, ColorRgba fill);
    void StrokeEllipse(float x, float y, float w, float h, ColorRgba stroke, float lineWidth);
    void FillPolygon(ReadOnlySpan<(float x, float y)> verts, ColorRgba fill);
    void StrokePolygon(ReadOnlySpan<(float x, float y)> verts, ColorRgba stroke, float lineWidth);

    void DrawString(string text, float x, float y, float fontSizeDip, bool bold, ColorRgba color);
    (float w, float h) MeasureString(string text, float fontSizeDip, bool bold);

    /// <summary>Uploads RGBA bytes (width × height × 4), returns GL texture id.</summary>
    uint CreateTextureRgba(ReadOnlySpan<byte> rgba, int w, int h);

    void DrawTexture(uint texture, float x, float y, float w, float h, ColorRgba tint);
    void DeleteTexture(uint texture);
}
