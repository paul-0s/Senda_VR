using System.Collections;
using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Al empezar, ubica este objeto (por ejemplo el canvas del menú) frente a la cámara,
    /// a la altura de los ojos. Sirve en PC y en VR (donde la altura de la cabeza la da el visor).
    /// </summary>
    public class PanelFrente : MonoBehaviour
    {
        public float distancia = 2.2f;
        [Tooltip("Cuánto más abajo de los ojos queda el centro del panel (metros).")]
        public float bajarCentro = 0.1f;

        IEnumerator Start()
        {
            yield return null; // espera un cuadro para que el visor entregue la posición de la cabeza
            var cam = Camera.main;
            if (cam == null) yield break;
            var adelante = cam.transform.forward; adelante.y = 0f;
            if (adelante.sqrMagnitude < 0.001f) adelante = Vector3.forward;
            adelante.Normalize();
            transform.position = cam.transform.position + adelante * distancia + Vector3.down * bajarCentro;
            transform.rotation = Quaternion.LookRotation(adelante, Vector3.up);
        }
    }
}
