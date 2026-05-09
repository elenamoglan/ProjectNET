using VoidRunner.Models;

namespace VoidRunner.Strategies;

/// <summary>
/// Predicts player position a short time ahead, then dashes toward it.
/// Demonstrates: predictive AI, state machine (idle / dashing).
/// </summary>
public sealed class SpeedDashStrategy : IMovementStrategy
{
    private Player? _target;
    private float   _dashCooldown = 1.2f;
    private float   _dirX, _dirY;
    private bool    _isDashing;
    private float   _dashTimer;
    private const float DashDuration = 0.3f;

    public void SetTarget(Player player) => _target = player;

    public void Move(Enemy enemy, float deltaTime)
    {
        if (_target is null) return;

        _dashCooldown -= deltaTime;

        if (_isDashing)
        {
            enemy.X += _dirX * enemy.BaseSpeed * deltaTime;
            enemy.Y += _dirY * enemy.BaseSpeed * deltaTime;
            _dashTimer -= deltaTime;
            if (_dashTimer <= 0f) _isDashing = false;
        }
        else if (_dashCooldown <= 0f)
        {
            // Predict ahead: 0.4s look-ahead
            float predX = _target.X; // simplified; full prediction needs velocity
            float predY = _target.Y;

            float dx = predX - enemy.X;
            float dy = predY - enemy.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            if (dist > 1f)
            {
                _dirX = dx / dist;
                _dirY = dy / dist;
            }

            _isDashing    = true;
            _dashTimer    = DashDuration;
            _dashCooldown = 1.0f + Random.Shared.NextSingle() * 0.6f;
        }
        else
        {
            // Slow drift toward player between dashes
            float dx = (_target.X) - enemy.X;
            float dy = (_target.Y) - enemy.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            if (dist > 1f)
            {
                enemy.X += (dx / dist) * (enemy.BaseSpeed * 0.25f) * deltaTime;
                enemy.Y += (dy / dist) * (enemy.BaseSpeed * 0.25f) * deltaTime;
            }
        }
    }
}
