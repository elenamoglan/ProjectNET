namespace VoidRunner.Graphics;

public readonly struct RectF
{
    public float X { get; }
    public float Y { get; }
    public float Width { get; }
    public float Height { get; }

    public RectF(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool IntersectsWith(RectF other) =>
        X < other.X + other.Width
        && X + Width > other.X
        && Y < other.Y + other.Height
        && Y + Height > other.Y;
}
