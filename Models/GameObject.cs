using VoidRunner.Graphics;

namespace VoidRunner.Models;

/// <summary>
/// Abstract base for every entity drawn on the game canvas.
/// Demonstrates: abstract classes, properties, virtual methods.
/// </summary>
public abstract class GameObject
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; protected set; }
    public float Height { get; protected set; }
    public bool IsActive { get; set; } = true;

    protected GameObject(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public RectF Bounds => new(X, Y, Width, Height);

    public bool CollidesWith(GameObject other) =>
        IsActive && other.IsActive && Bounds.IntersectsWith(other.Bounds);

    public abstract void Update(float deltaTime);
    public abstract void Draw(IGameRenderer g);
}
