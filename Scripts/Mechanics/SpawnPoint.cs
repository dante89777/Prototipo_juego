using UnityEngine;

namespace Platformer.Mechanics
{
    /// <summary>
    /// Punto de reaparición del jugador. El modelo de la partida lo referencia
    /// para devolver al jugador al inicio del nivel cuando muere.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        /// <summary>
        /// Posición de reaparición en coordenadas de mundo.
        /// </summary>
        public Vector3 Position => transform.position;

        /// <summary>
        /// Coloca el punto de reaparición en la posición indicada.
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
        }
    }
}
