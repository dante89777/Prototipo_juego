using Platformer.Core;
using Platformer.Mechanics;
using Platformer.Model;

namespace Platformer.Gameplay
{
    /// <summary>
    /// Fired when the player is spawned after dying.
    /// </summary>
    public class PlayerSpawn : Simulation.Event<PlayerSpawn>
    {
        PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        public override void Execute()
        {
            var player = model.player;
            player.Collider.enabled = true;
            player.ControlEnabled = false;
            if (player.AudioSource != null && player.RespawnAudio != null)
                player.AudioSource.PlayOneShot(player.RespawnAudio);
            if (player.Health != null)
                player.Health.SetMaxHP(player.Health.MaxHP);
            player.Teleport(model.spawnPoint.transform.position);
            player.JumpState = PlayerJumpState.Grounded;
            player.Animator.Rebind();
            player.Animator.Play("Idle", 0, 0f);
            model.virtualCamera.Follow = player.transform;
            model.virtualCamera.LookAt = player.transform;
            Simulation.Schedule<EnablePlayerInput>(2f);
        }
    }
}
