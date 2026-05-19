using VoidRunner.Models;

namespace VoidRunner.Strategies;

// Strategy pattern: each enemy type gets its own movement algorithm.

public interface IMovementStrategy
{
    void SetTarget(Player player);
    void Move(Enemy enemy, float deltaTime);
}
