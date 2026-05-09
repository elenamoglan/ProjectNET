using VoidRunner.Models;

namespace VoidRunner.Strategies;

/// <summary>
/// Strategy pattern: each enemy type gets its own movement algorithm.
/// Demonstrates: interfaces, dependency injection via composition.
/// </summary>
public interface IMovementStrategy
{
    void SetTarget(Player player);
    void Move(Enemy enemy, float deltaTime);
}
