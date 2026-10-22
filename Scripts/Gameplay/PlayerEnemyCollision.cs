using Platformer.Core;
using Platformer.Mechanics;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player collides with an enemy. Ya no existe el "stomp":
    /// cualquier colisión con un enemigo hace daño al jugador.
    /// </summary>
    public class PlayerEnemyCollision : Simulation.Event<PlayerEnemyCollision>
    {
        public PlayerController player;
        public EnemyController enemy;

        public override void Execute()
        {
            if (player == null || enemy == null) return;

            var health = player.Health;
            if (health == null || !health.IsAlive) return;

            health.Decrement();
        }
    }
}
