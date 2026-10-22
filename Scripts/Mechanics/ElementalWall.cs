using Platformer.Core;
using Platformer.Model;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Pared elemental. Bloquea el paso y castiga al jugador que la toca,
    /// hasta que el jugador activa el elemento necesario.
    /// </summary>
    public class ElementalWall : MonoBehaviour
    {
        [Tooltip("Elemento que abre la pared.")]
        [SerializeField] private GameElement requiredElement = GameElement.Rayo;

        [Tooltip("Segundos entre cada golpe recibido por el jugador.")]
        [SerializeField] private float damageInterval = 1f;

        [Tooltip("Sonido reproducido cuando la pared se abre.")]
        [SerializeField] private AudioClip openAudio;

        [Tooltip("Sonido reproducido cuando la pared bloquea al jugador.")]
        [SerializeField] private AudioClip blockedAudio;

        [Tooltip("Mensaje mostrado al bloquearse el paso.")]
        [SerializeField] private string blockedMessage = "Necesitas el elemento RAYO (tecla F)";

        [Tooltip("Distancia a la que el jugador puede disolver la pared.")]
        [SerializeField] private float openDistance = 6f;

        BoxCollider2D blocker;
        BoxCollider2D trigger;
        SpriteRenderer[] bricks;
        bool opened;
        float nextDamageTime;

        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        /// <summary>
        /// Elemento que abre la pared.
        /// </summary>
        public GameElement RequiredElement => requiredElement;

        /// <summary>
        /// True cuando la pared ya fue abierta.
        /// </summary>
        public bool IsOpened => opened;

        void Awake()
        {
            foreach (BoxCollider2D collider in GetComponentsInChildren<BoxCollider2D>(true))
            {
                if (collider.isTrigger)
                {
                    if (trigger == null) trigger = collider;
                }
                else if (blocker == null)
                {
                    blocker = collider;
                }
            }

            bricks = GetComponentsInChildren<SpriteRenderer>(true);
        }

        void Update()
        {
            if (opened) return;
            if (!HasRequiredElement()) return;

            // La pared solo se disuelve cuando el jugador está cerca:
            // conseguir el elemento lejos no resuelve el reto por sí solo.
            if (PlayerIsClose())
                Open();
        }

        bool HasRequiredElement()
        {
            return requiredElement != GameElement.Ninguno && model.ActiveElement == requiredElement;
        }

        bool PlayerIsClose()
        {
            PlayerController player = model.player;
            if (player == null) return false;

            Vector3 wallPosition = transform.position;
            Vector3 playerPosition = player.transform.position;
            float dx = playerPosition.x - wallPosition.x;
            float dy = playerPosition.y - wallPosition.y;
            return (dx * dx + dy * dy) <= openDistance * openDistance;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            if (HasRequiredElement())
            {
                Open();
            }
            else
            {
                Damage(player);
            }
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (opened) return;

            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            if (HasRequiredElement())
            {
                Open();
            }
            else
            {
                Damage(player);
            }
        }

        void Damage(PlayerController player)
        {
            if (Time.time < nextDamageTime) return;

            Health health = player.Health;
            if (health == null || !health.IsAlive) return;

            nextDamageTime = Time.time + damageInterval;
            health.Decrement();
            model.ShowMessage(blockedMessage);

            if (blockedAudio != null)
                AudioSource.PlayClipAtPoint(blockedAudio, player.transform.position);
        }

        void Open()
        {
            if (opened) return;
            opened = true;

            if (blocker != null)
                blocker.enabled = false;

            foreach (SpriteRenderer brick in bricks)
            {
                if (brick != null)
                    brick.enabled = false;
            }

            if (openAudio != null)
                AudioSource.PlayClipAtPoint(openAudio, transform.position);

            model.ShowMessage("La pared se ha disuelto");
        }
    }
}
