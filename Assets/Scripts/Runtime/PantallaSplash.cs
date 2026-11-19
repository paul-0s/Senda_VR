using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace SendaVR
{
    /// <summary>
    /// Pantalla de inicio: el logo aparece pequeño con un "pop", crece y sube
    /// mientras aparece el texto "Senda.vr" (variantes Default/Variant2 del
    /// componente "LOGO ANIMADO" en Figma). Luego pasa a la siguiente pantalla.
    /// </summary>
    public class PantallaSplash : PantallaBase
    {
        public RectTransform logo;
        public CanvasGroup logoGrupo;
        public RectTransform texto;
        public CanvasGroup textoGrupo;
        public PantallaBase siguiente;

        [Header("Video del logo (opcional)")]
        [Tooltip("Si hay video (logo_animacion.mp4), se reproduce en lugar de la animación hecha con código.")]
        public VideoPlayer videoLogo;
        public GameObject capaVideo;
        public float esperaTrasVideo = 0.5f;

        [Header("Tiempos (segundos)")]
        public float retrasoInicial = 0.3f;
        public float duracionAparecer = 0.8f;
        public float duracionCrecer = 0.9f;
        public float esperaFinal = 1.6f;

        [Header("Posiciones (px de la pantalla 1920x1080)")]
        public float escalaInicial = 0.59f;     // 137 / 233, tamaño de "Variant2"
        public float alturaFinalLogo = 110f;    // cuánto sube el logo
        public float alturaFinalTexto = -115f;  // dónde queda el texto

        protected override void AlMostrar()
        {
            StopAllCoroutines();
            StartCoroutine(Secuencia());
        }

        IEnumerator Secuencia()
        {
            if (videoLogo != null && videoLogo.clip != null)
            {
                yield return SecuenciaVideo();
                yield break;
            }
            if (capaVideo != null) capaVideo.SetActive(false);

            logo.anchoredPosition = Vector2.zero;
            logo.localScale = Vector3.zero;
            logoGrupo.alpha = 0f;
            textoGrupo.alpha = 0f;
            texto.anchoredPosition = new Vector2(0f, alturaFinalTexto - 30f);

            // Fundido de la propia pantalla
            yield return Animar(duracionFundido, t => grupo.alpha = t);
            yield return new WaitForSecondsRealtime(retrasoInicial);

            // 1) Pop del logo pequeño
            yield return Animar(duracionAparecer, t =>
            {
                logo.localScale = Vector3.one * Mathf.LerpUnclamped(0f, escalaInicial, t);
                logoGrupo.alpha = Mathf.Clamp01(t * 2f);
            }, SendaEstilo.EaseOutBack);

            yield return new WaitForSecondsRealtime(0.25f);

            // 2) Crece, sube y aparece el texto
            yield return Animar(duracionCrecer, t =>
            {
                logo.localScale = Vector3.one * Mathf.Lerp(escalaInicial, 1f, t);
                logo.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, alturaFinalLogo, t));
                textoGrupo.alpha = t;
                texto.anchoredPosition = new Vector2(0f, Mathf.Lerp(alturaFinalTexto - 30f, alturaFinalTexto, t));
            }, SendaEstilo.EaseInOutCubic);

            yield return new WaitForSecondsRealtime(esperaFinal);
            IrA(siguiente);
        }

        IEnumerator SecuenciaVideo()
        {
            logo.gameObject.SetActive(false);
            texto.gameObject.SetActive(false);
            if (capaVideo != null) capaVideo.SetActive(true);
            grupo.alpha = 0f;
            LimpiarTextura(videoLogo.targetTexture, Color.white);

            videoLogo.isLooping = false;
            videoLogo.Prepare();
            float limite = Time.unscaledTime + 5f;
            while (!videoLogo.isPrepared && Time.unscaledTime < limite) yield return null;

            bool termino = false;
            VideoPlayer.EventHandler alTerminar = _ => termino = true;
            videoLogo.loopPointReached += alTerminar;
            videoLogo.Play();
            yield return EsperarPrimerCuadro(videoLogo);
            yield return Animar(duracionFundido, t => grupo.alpha = t);

            limite = Time.unscaledTime + (float)videoLogo.clip.length + 3f;
            while (!termino && Time.unscaledTime < limite) yield return null;
            videoLogo.loopPointReached -= alTerminar;

            yield return new WaitForSecondsRealtime(esperaTrasVideo);
            IrA(siguiente);
        }
    }
}
