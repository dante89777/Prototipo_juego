using Platformer.Core;
using Platformer.Mechanics;
using UnityEngine;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the health component on an enemy has a hitpoint value of  0.
    /// </summary>
    /// <typeparam name="EnemyDeath"></typeparam>
    public class EnemyDeath : Simulation.Event<EnemyDeath>
    {
        public EnemyController enemy;

        public override void Execute()
        {
            enemy._collider.enabled = false;
            enemy.control.enabled = false;

            var animator = enemy.GetComponent<Animator>();
            if (animator != null)
                animator.SetTrigger("death");

            if (enemy._audio && enemy.Ouch)
                enemy._audio.PlayOneShot(enemy.Ouch);
        }
    }
}
