using Platformer.Core;
using Platformer.Model;
using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// This class exposes the the game model in the inspector, and ticks the
    /// simulation.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameController : MonoBehaviour
    {
        public static GameController Instance { get; private set; }

        //This model field is public and can be therefore be modified in the
        //inspector.
        //The reference actually comes from the InstanceRegister, and is shared
        //through the simulation and events. Unity will deserialize over this
        //shared reference when the scene loads, allowing the model to be
        //conveniently configured inside the inspector.
        public PlatformerModel model = Simulation.GetModel<PlatformerModel>();

        void Awake()
        {
            // Unity deserializa el modelo guardado en la escena en una
            // instancia propia. Se vuelcan sus valores sobre la instancia
            // compartida por la simulación para que todo el estado de la
            // partida viva en un único objeto.
            PlatformerModel shared = Simulation.GetModel<PlatformerModel>();
            shared.CopyConfigurationFrom(model);
            model = shared;
            model.ResetProgress();
        }

        void OnEnable()
        {
            Instance = this;
        }

        void OnDisable()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (Instance == this) Simulation.Tick();
        }
    }
}
