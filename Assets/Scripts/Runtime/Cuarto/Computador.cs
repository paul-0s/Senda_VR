using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// El computador del cuarto. Por ahora: si ya tienes todo el equipo,
    /// te permite ir al sendero; si no, te dice qué te falta.
    /// (Más adelante aquí va el menú de selección de sendero.)
    /// </summary>
    [DisallowMultipleComponent]
    public class Computador : MonoBehaviour
    {
        public string titulo = "Computador";
        [Tooltip("Escena del sendero. Debe estar en File > Build Profiles > Scene List.")]
        public string escenaSendero = "OutdoorsScene";
        public string nombreSendero = "Parque Natural Aguas de Ramón";
    }
}
