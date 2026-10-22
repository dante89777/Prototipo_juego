using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;
using UnityEngine;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player has died.
    /// </summary>
    /// <typeparam name="PlayerDeath"></typeparam>
    public class PlayerDeath : Simulation.Event<PlayerDeath>
    {
        PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public override void Execute()
        {
            var player = model.player;
            if (player.Health.IsAlive)
            {
                player.Health.Die();
                model.virtualCamera.Follow = null;
                model.virtualCamera.LookAt = null;
                player.ControlEnabled = false;

                if (player.AudioSource != null && player.OuchAudio != null)
                    player.AudioSource.PlayOneShot(player.OuchAudio);
                player.Animator.SetBool("noBlood", false);
                player.Animator.SetTrigger("Death");
                Simulation.Schedule<PlayerSpawn>(2);
            }
        }
    }
}
