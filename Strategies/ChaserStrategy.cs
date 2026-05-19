using VoidRunner.Models;

namespace VoidRunner.Strategies;

// Moves directly toward the player at constant speed.

public sealed class ChaserStrategy : IMovementStrategy
{
    private Player? _target;

    public void SetTarget(Player player) => _target = player;

    public void Move(Enemy enemy, float deltaTime)
    {
        if (_target is null) return;

        float dx = (_target.X + _target.Width / 2f) - (enemy.X + enemy.Width / 2f);
        float dy = (_target.Y + _target.Height / 2f) - (enemy.Y + enemy.Height / 2f);
        float dist = MathF.Sqrt(dx * dx + dy * dy);

        if (dist < 1f) return;

        float nx = dx / dist;
        float ny = dy / dist;

        enemy.X += nx * enemy.BaseSpeed * deltaTime;
        enemy.Y += ny * enemy.BaseSpeed * deltaTime;
    }
}
