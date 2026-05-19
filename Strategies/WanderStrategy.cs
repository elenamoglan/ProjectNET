using VoidRunner.Models;

namespace VoidRunner.Strategies;

// Wanders randomly; occasionally locks onto the player briefly.

public sealed class WanderStrategy : IMovementStrategy
{
    private Player? _target;
    private float _angle = Random.Shared.NextSingle() * MathF.Tau;
    private float _turnTimer = 0f;
    private float _homingTimer = 0f;
    private bool _isHoming = false;

    public void SetTarget(Player player) => _target = player;

    public void Move(Enemy enemy, float deltaTime)
    {
        _turnTimer -= deltaTime;
        _homingTimer -= deltaTime;

        if (_homingTimer <= 0f)
        {
            _isHoming = !_isHoming;
            _homingTimer = _isHoming
                ? 1.0f + Random.Shared.NextSingle() * 1.0f   // home for 1–2 s
                : 2.0f + Random.Shared.NextSingle() * 2.0f;  // wander for 2–4 s
        }

        if (_isHoming && _target is not null)
        {
            float dx = _target.X - enemy.X;
            float dy = _target.Y - enemy.Y;
            _angle = MathF.Atan2(dy, dx);
        }
        else if (_turnTimer <= 0f)
        {
            _angle += (Random.Shared.NextSingle() - 0.5f) * MathF.PI;
            _turnTimer = 0.4f + Random.Shared.NextSingle() * 0.6f;
        }

        enemy.X += MathF.Cos(_angle) * enemy.BaseSpeed * deltaTime;
        enemy.Y += MathF.Sin(_angle) * enemy.BaseSpeed * deltaTime;

        // Bounce off walls
        if (enemy.X < 0)
        {
            enemy.X = 0;
            _angle = MathF.PI - _angle;
        }
        if (enemy.X > GameConstants.CanvasWidth - enemy.Width)
        {
            enemy.X = GameConstants.CanvasWidth - enemy.Width;
            _angle = MathF.PI - _angle;
        }
        if (enemy.Y < 0)
        {
            enemy.Y = 0;
            _angle = -_angle;
        }
        if (enemy.Y > GameConstants.CanvasHeight - enemy.Height)
        {
            enemy.Y = GameConstants.CanvasHeight - enemy.Height;
            _angle = -_angle;
        }
    }
}
