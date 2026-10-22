using Platformer.Core;
using Platformer.Mechanics;
using static Platformer.Core.Simulation;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player health reaches 0. This usually would result in a 
    /// PlayerDeath event.
    /// </summary>
    /// <typeparam name="HealthIsZero"></typeparam>
    public class HealthIsZero : Simulation.Event<HealthIsZero>
    {
        public Health health;

        public override void Execute()
        {
            var enemy = health.GetComponent<EnemyController>();
            if (enemy != null)
            {
                var ev = Schedule<EnemyDeath>();
                ev.enemy = enemy;
                return;
            }

            if (health.GetComponent<PlayerController>() != null)
                Schedule<PlayerDeath>();
        }
    }
}