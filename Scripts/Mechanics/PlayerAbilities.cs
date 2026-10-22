using Platformer.Core;
using Platformer.Model;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Gestiona las habilidades del jugador: elementos obtenidos, ataques
    /// desbloqueados y el cambio de elemento activo con la tecla F.
    /// Los datos viven en el modelo; este componente sólo traduce la entrada
    /// del teclado a cambios de estado.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAbilities : MonoBehaviour
    {
        [Tooltip("Tecla usada para alternar entre los elementos obtenidos.")]
        [SerializeField] private Key switchElementKey = Key.F;

        readonly PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        PlayerController player;

        /// <summary>
        /// Elemento equipado en este momento.
        /// </summary>
        public GameElement ActiveElement => model.ActiveElement;

        /// <summary>
        /// Tecla con la que se alterna de elemento.
        /// </summary>
        public Key SwitchElementKey => switchElementKey;

        void Awake()
        {
            player = GetComponent<PlayerController>();
        }

        void Update()
        {
            if (player == null || !player.ControlEnabled) return;
            if (Keyboard.current == null) return;

            if (Keyboard.current[switchElementKey].wasPressedThisFrame)
                SwitchElement();
        }

        /// <summary>
        /// Alterna entre los elementos obtenidos y avisa al jugador.
        /// </summary>
        public void SwitchElement()
        {
            if (!model.CanSwitchElement)
            {
                model.ShowMessage("Todavía no tienes dos elementos");
                return;
            }

            GameElement element = model.SwitchElement();
            model.ShowMessage($"Elemento activo: {NameOf(element)}");
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
