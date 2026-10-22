using Platformer.Core;
using Platformer.Model;
using Platformer.View;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Cofre del nivel. Al tocarlo se abre y otorga al jugador un elemento,
    /// el ataque asociado o el gancho, según la configuración.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class ChestPickup : MonoBehaviour
    {
        [Header("Recompensa")]
        [Tooltip("Elemento que entrega el cofre. Ninguno si no entrega ninguno.")]
        [SerializeField] private GameElement element = GameElement.Ninguno;
        [Tooltip("Si es true, el cofre también desbloquea el ataque del elemento.")]
        [SerializeField] private bool grantsAttack = true;
        [Tooltip("Si es true, el cofre desbloquea el gancho.")]
        [SerializeField] private bool grantsGrapple = false;

        [Header("Presentación")]
        [SerializeField] private AudioClip openAudio;
        [SerializeField] private Color glowColor = Color.white;
        [Tooltip("Nombre del animator que abre la tapa del cofre.")]
        [SerializeField] private string openParameter = "IsOpened";

        Collider2D chestCollider;
        Animator chestAnimator;
        bool opened;

        /// <summary>
        /// Elemento que entrega el cofre.
        /// </summary>
        public GameElement Element => element;

        /// <summary>
        /// True si el cofre ya fue abierto.
        /// </summary>
        public bool IsOpened => opened;

        void Awake()
        {
            chestCollider = GetComponent<Collider2D>();
            chestAnimator = GetComponentInChildren<Animator>();
            if (chestCollider != null)
                chestCollider.isTrigger = true;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (opened) return;

            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            opened = true;
            PlatformerModel model = Simulation.GetModel<PlatformerModel>();

            if (element != GameElement.Ninguno)
            {
                model.CollectElement(element);
                if (grantsAttack)
                    model.UnlockAttack(element);
            }

            if (grantsGrapple)
                model.UnlockGrapple();

            if (chestAnimator != null && !string.IsNullOrEmpty(openParameter))
                chestAnimator.SetBool(openParameter, true);

            if (openAudio != null)
                AudioSource.PlayClipAtPoint(openAudio, transform.position);

            CircleEffect.Spawn(transform.position + Vector3.up * 0.4f, 1.3f, glowColor);

            if (chestCollider != null)
                chestCollider.enabled = false;

            model.ShowMessage(BuildMessage());
        }

        string BuildMessage()
        {
            if (grantsGrapple && element == GameElement.Ninguno)
                return "Has obtenido el GANCHO";
            if (element != GameElement.Ninguno)
                return $"Has obtenido {NameOf(element)}";
            return "Has obtenido una recompensa";
        }

        static string NameOf(GameElement value)
        {
            switch (value)
            {
                case GameElement.Fuego: return "el elemento FUEGO";
                case GameElement.Rayo: return "el elemento RAYO";
                default: return "un nuevo poder";
            }
        }
    }
}
