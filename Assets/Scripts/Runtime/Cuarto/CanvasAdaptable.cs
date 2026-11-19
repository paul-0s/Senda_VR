using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// En PC el canvas se dibuja encima de la pantalla (nítido y sin chocar con paredes).
    /// En VR se convierte en un panel en el mundo: pegado a la cabeza (HUD) o
    /// flotando frente al jugador (ventanas), porque en el visor no existe "la pantalla".
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public class CanvasAdaptable : MonoBehaviour
    {
        [Tooltip("Solo en VR: el canvas sigue a la cabeza (para la lista y la mira).")]
        public bool seguirCabeza;
        [Tooltip("Solo en VR: distancia a los ojos, en metros.")]
        public float distancia = 0.8f;
        [Tooltip("Solo en VR: metros por cada pixel del diseño (1920x1080).")]
        public float escalaVR = 0.0006f;

        [Tooltip("Objetos que solo tienen sentido en pantalla de PC (por ejemplo, el oscurecido de fondo).")]
        public GameObject[] soloPC;

        public bool EsMundo { get; private set; }

        void Start()
        {
            var canvas = GetComponent<Canvas>();
            if (!EstadoJugador.EnVR)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                return;
            }

            EsMundo = true;
            foreach (var go in soloPC) if (go != null) go.SetActive(false);
            var cam = Camera.main;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var rt = (RectTransform)transform;
            rt.sizeDelta = new Vector2(1920f, 1080f);
            if (seguirCabeza && cam != null)
            {
                transform.SetParent(cam.transform, false);
                rt.localPosition = new Vector3(0f, 0f, distancia);
                rt.localRotation = Quaternion.identity;
            }
            rt.localScale = Vector3.one * escalaVR;
        }

        /// <summary>Solo VR: pone el panel frente al jugador, a la altura de los ojos.</summary>
        public void PonerFrenteA(Camera cam)
        {
            if (!EsMundo || cam == null || seguirCabeza) return;
            var adelante = cam.transform.forward; adelante.y = 0f;
            if (adelante.sqrMagnitude < 0.001f) adelante = cam.transform.up;
            adelante.Normalize();
            transform.position = cam.transform.position + adelante * distancia;
            transform.rotation = Quaternion.LookRotation(adelante, Vector3.up);
        }
    }
}
