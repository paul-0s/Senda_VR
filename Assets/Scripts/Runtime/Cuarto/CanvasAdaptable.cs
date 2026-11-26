using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// En PC el canvas se dibuja encima de la pantalla (nítido y sin chocar con paredes).
    /// En VR se convierte en un panel en el mundo: pegado a la cabeza (HUD) o
    /// flotando frente al jugador (ventanas), porque en el visor no existe "la pantalla".
    /// En VR además se dibuja encima de todo, para que las paredes y muebles no lo tapen.
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

        [Tooltip("Solo en VR: dibujar el panel encima de todo, para que las paredes y muebles no lo tapen.")]
        public bool dibujarEncima = true;

        // Materiales de UI copiados con la prueba de profundidad apagada (uno por material original)
        static readonly Dictionary<Material, Material> materialesEncima = new Dictionary<Material, Material>();
        readonly HashSet<Graphic> procesados = new HashSet<Graphic>();
        int cuadrosParaRevisar;

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

            if (dibujarEncima)
            {
                canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 100);
                AplicarDibujarEncima();
            }
        }

        void LateUpdate()
        {
            // Los elementos que se crean después (por ejemplo, filas nuevas de la lista) también van encima
            if (!EsMundo || !dibujarEncima) return;
            if (--cuadrosParaRevisar > 0) return;
            cuadrosParaRevisar = 30;
            AplicarDibujarEncima();
        }

        /// <summary>Hace que todos los textos e imágenes del canvas ignoren la profundidad (se ven a través de paredes).</summary>
        void AplicarDibujarEncima()
        {
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                if (g == null || procesados.Contains(g)) continue;
                procesados.Add(g);

                if (g is TMP_Text texto)
                {
                    var m = texto.fontMaterial; // copia propia de este texto
                    if (m != null) m.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                    continue;
                }

                var original = g.material; // si no tiene, Unity entrega el material de UI por defecto
                if (original == null) continue;
                if (!materialesEncima.TryGetValue(original, out var encima) || encima == null)
                {
                    encima = new Material(original) { name = original.name + " (encima)" };
                    encima.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
                    materialesEncima[original] = encima;
                }
                g.material = encima;
            }
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
