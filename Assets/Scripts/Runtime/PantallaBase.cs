using System;
using System.Collections;
using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Base de todas las pantallas: aparece y desaparece con un fundido.
    /// Cada pantalla es un hijo de "Pantallas" dentro del Canvas.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PantallaBase : MonoBehaviour
    {
        [Tooltip("Duración del fundido al entrar/salir (segundos).")]
        public float duracionFundido = 0.45f;

        [HideInInspector] public NavegadorPantallas navegador;

        protected CanvasGroup grupo;
        Coroutine fundido;

        protected virtual void Awake()
        {
            grupo = GetComponent<CanvasGroup>();
        }

        public void Mostrar()
        {
            gameObject.SetActive(true);
            if (grupo == null) grupo = GetComponent<CanvasGroup>();
            grupo.alpha = 0f;
            grupo.interactable = true;
            grupo.blocksRaycasts = true;
            if (fundido != null) StopCoroutine(fundido);
            fundido = StartCoroutine(Animar(duracionFundido, t => grupo.alpha = t, SendaEstilo.EaseOutCubic));
            AlMostrar();
        }

        public void Ocultar()
        {
            if (!gameObject.activeInHierarchy) return;
            grupo.blocksRaycasts = false; // (interactable se deja en true para que los botones no se vean grises)
            if (fundido != null) StopCoroutine(fundido);
            fundido = StartCoroutine(OcultarRutina());
        }

        IEnumerator OcultarRutina()
        {
            float inicio = grupo.alpha;
            yield return Animar(duracionFundido, t => grupo.alpha = Mathf.Lerp(inicio, 0f, t), SendaEstilo.EaseOutCubic);
            gameObject.SetActive(false);
        }

        public void OcultarAlInstante()
        {
            if (grupo == null) grupo = GetComponent<CanvasGroup>();
            grupo.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>Se llama cada vez que la pantalla aparece.</summary>
        protected virtual void AlMostrar() { }

        protected void IrA(PantallaBase destino)
        {
            if (navegador != null && destino != null) navegador.IrA(destino);
        }

        /// <summary>
        /// Borra una RenderTexture de video. Así no se ve por un instante el último
        /// cuadro que quedó guardado de la prueba anterior.
        /// </summary>
        public static void LimpiarTextura(RenderTexture rt, Color color)
        {
            if (rt == null) return;
            var anterior = RenderTexture.active;
            RenderTexture.active = rt;
            GL.Clear(true, true, color);
            RenderTexture.active = anterior;
        }

        /// <summary>Espera a que el video tenga su primer cuadro dibujado (máximo "limite" segundos).</summary>
        public static IEnumerator EsperarPrimerCuadro(UnityEngine.Video.VideoPlayer vp, float limite = 2f)
        {
            float fin = Time.unscaledTime + limite;
            while (vp != null && vp.frame < 1 && Time.unscaledTime < fin) yield return null;
        }

        /// <summary>Anima un valor de 0 a 1 durante "duracion" segundos.</summary>
        public static IEnumerator Animar(float duracion, Action<float> paso, Func<float, float> curva = null)
        {
            float t = 0f;
            while (t < duracion)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duracion);
                paso(curva != null ? curva(k) : k);
                yield return null;
            }
            paso(1f);
        }
    }
}
