using Platformer.Core;
using Platformer.Model;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Zona de daño. Resta vida al jugador mientras permanece dentro del
    /// área. Puede tener un elemento que la haga inofensiva.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class DamageZone : MonoBehaviour
    {
        [Tooltip("Segundos entre cada golpe recibido.")]
        [SerializeField] private float damageInterval = 1f;
        [Tooltip("Si se indica un elemento, el jugador no recibe daño con ese elemento activo.")]
        [SerializeField] private GameElement safeElement = GameElement.Ninguno;
        [Tooltip("Sonido reproducido al recibir daño.")]
        [SerializeField] private AudioClip hurtAudio;

        float nextDamageTime;

        void Awake()
        {
            var collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
                Damage(player);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player != null)
                Damage(player);
        }

        void Damage(PlayerController player)
        {
            if (Time.time < nextDamageTime) return;

            PlatformerModel model = Simulation.GetModel<PlatformerModel>();
            nextDamageTime = Time.time + damageInterval;

            if (safeElement != GameElement.Ninguno)
            {
                if (model.ActiveElement == safeElement)
                {
                    model.ShowMessage("El fuego no te afecta");
                    return;
                }

                // El jugador ya tiene el elemento correcto pero no lo tiene
                // equipado: se le avisa en vez de castigarle.
                if (model.HasElement(safeElement))
                {
                    model.ShowMessage($"Pulsa F para equipar {NameOf(safeElement)}");
                    return;
                }
            }

            Health health = player.Health;
            if (health == null || !health.IsAlive) return;

            health.Decrement();

            if (hurtAudio != null)
                AudioSource.PlayClipAtPoint(hurtAudio, player.transform.position);
        }

        static string NameOf(GameElement value)
        {
            switch (value)
            {
                case GameElement.Fuego: return "FUEGO";
                case GameElement.Rayo: return "RAYO";
                default: return "NINGUNO";
            }
        }
    }
}
