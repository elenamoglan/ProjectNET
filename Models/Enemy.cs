using VoidRunner.Graphics;
using VoidRunner.Strategies;

namespace VoidRunner.Models;

public enum EnemyType { Chaser, Speeder, Wanderer }

/// <summary>
/// Enemy entity. Uses a Strategy pattern so movement logic is swappable.
/// Demonstrates: composition over inheritance, constructor injection.
/// </summary>
public sealed class Enemy : GameObject
{
    public EnemyType Type     { get; }
    public float     BaseSpeed { get; }

    private readonly IMovementStrategy _movement;
    private readonly ColorRgba        _color;
    private readonly float            _radius;

    public static Enemy Create(EnemyType type, float x, float y, float speedMultiplier)
    {
        var (size, speed, color, strategy) = type switch
        {
            EnemyType.Chaser   => (20f, 90f  * speedMultiplier, ColorRgba.FromArgb(255, 220, 60, 60),   (IMovementStrategy)new ChaserStrategy()),
            EnemyType.Speeder  => (14f, 170f * speedMultiplier, ColorRgba.FromArgb(255, 255, 200, 40),   new SpeedDashStrategy()),
            EnemyType.Wanderer => (26f, 65f  * speedMultiplier, ColorRgba.FromArgb(255, 100, 80, 200),    new WanderStrategy()),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        return new Enemy(type, x, y, size, speed, color, strategy);
    }

    private Enemy(EnemyType type, float x, float y, float size,
        float baseSpeed, ColorRgba color, IMovementStrategy movement)
        : base(x, y, size, size)
    {
        Type      = type;
        BaseSpeed = baseSpeed;
        _color    = color;
        _radius   = size / 2f;
        _movement = movement;
    }

    public override void Update(float deltaTime) =>
        _movement.Move(this, deltaTime);

    public void SetTarget(Player player) =>
        _movement.SetTarget(player);

    public override void Draw(IGameRenderer g)
    {
        float cx = X + _radius, cy = Y + _radius;

        g.FillEllipse(X - 4, Y - 4, Width + 8, Height + 8, _color.WithAlpha(50));
        g.FillEllipse(X, Y, Width, Height, _color);
        g.StrokeEllipse(X, Y, Width, Height, ColorRgba.FromArgb(200, 255, 255, 255), 1f);

        string symbol = Type switch
        {
            EnemyType.Chaser   => "\u25B2",
            EnemyType.Speeder  => "\u25BA",
            EnemyType.Wanderer => "\u25C6",
            _ => "?"
        };

        var sz = g.MeasureString(symbol, 7f, true);
        g.DrawString(symbol, cx - sz.w / 2f, cy - sz.h / 2f, 7f, true, ColorRgba.FromArgb(255, 255, 255, 255));
    }
}
