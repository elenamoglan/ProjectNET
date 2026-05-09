using Silk.NET.Input;
using VoidRunner.Graphics;

namespace VoidRunner.Models;

/// <summary>
/// Player-controlled ship. Reacts to WASD / arrow keys.
/// Demonstrates: encapsulation, readonly collections, clamping, invincibility frames.
/// </summary>
public sealed class Player : GameObject
{
    public const float Speed = 260f;
    private const float InvincibilityDuration = 1.5f;

    private readonly HashSet<Key> _pressedKeys;
    private float _invincibilityTimer;
    private float _blinkAccum;

    public int Lives { get; set; } = 3;
    public bool IsInvincible => _invincibilityTimer > 0f;

    private static readonly ColorRgba ShipColor   = ColorRgba.FromArgb(255, 0, 200, 255);
    private static readonly ColorRgba ThrustColor = ColorRgba.FromArgb(160, 255, 130, 0);
    private static readonly ColorRgba OutlineCol  = ColorRgba.FromArgb(255, 255, 255, 255);

    public Player(float x, float y, HashSet<Key> pressedKeys)
        : base(x, y, 24, 28)
    {
        _pressedKeys = pressedKeys;
    }

    public override void Update(float deltaTime)
    {
        HandleMovement(deltaTime);
        TickInvincibility(deltaTime);
    }

    private void HandleMovement(float deltaTime)
    {
        float dx = 0f, dy = 0f;

        if (_pressedKeys.Contains(Key.W) || _pressedKeys.Contains(Key.Up)) dy -= 1f;
        if (_pressedKeys.Contains(Key.S) || _pressedKeys.Contains(Key.Down)) dy += 1f;
        if (_pressedKeys.Contains(Key.A) || _pressedKeys.Contains(Key.Left)) dx -= 1f;
        if (_pressedKeys.Contains(Key.D) || _pressedKeys.Contains(Key.Right)) dx += 1f;

        if (dx != 0f && dy != 0f)
        {
            dx *= 0.7071f;
            dy *= 0.7071f;
        }

        X = Math.Clamp(X + dx * Speed * deltaTime, 0f, GameConstants.CanvasWidth - Width);
        Y = Math.Clamp(Y + dy * Speed * deltaTime, 0f, GameConstants.CanvasHeight - Height);
    }

    private void TickInvincibility(float deltaTime)
    {
        if (_invincibilityTimer <= 0f) return;
        _invincibilityTimer -= deltaTime;
        _blinkAccum         += deltaTime;
    }

    public void TriggerInvincibility() => _invincibilityTimer = InvincibilityDuration;

    public void ResetToCenter()
    {
        X = (GameConstants.CanvasWidth - Width) / 2f;
        Y = (GameConstants.CanvasHeight - Height) / 2f;
        _invincibilityTimer = 0f;
        _blinkAccum = 0f;
    }

    public override void Draw(IGameRenderer g)
    {
        if (IsInvincible && (int)(_blinkAccum * 8) % 2 == 0) return;

        (float x, float y)[] ship =
        [
            (X + Width / 2f, Y),
            (X + Width, Y + Height * 0.65f),
            (X + Width / 2f, Y + Height),
            (X, Y + Height * 0.65f),
        ];

        g.FillPolygon(ship, ShipColor);
        g.StrokePolygon(ship, OutlineCol, 1.5f);

        g.FillEllipse(
            X + Width * 0.33f,
            Y + Height * 0.85f,
            Width * 0.34f,
            Height * 0.22f,
            ThrustColor);
    }
}
