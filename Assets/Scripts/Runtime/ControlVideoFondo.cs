using UnityEngine;
using UnityEngine.Video;

namespace SendaVR
{
    /// <summary>
    /// Fondo en video del inicio de sesión:
    /// "intro" (animacion-1) se reproduce una vez y luego "bucle" (animacion-2)
    /// se repite. Si no hay intro, solo se reproduce el bucle.
    /// </summary>
    public class ControlVideoFondo : MonoBehaviour
    {
        public VideoPlayer intro;
        public VideoPlayer bucle;

        public bool TieneIntro => intro != null && intro.clip != null;
        public bool IntroTerminada { get; private set; }

        bool iniciado;

        void Start()
        {
            if (bucle != null) bucle.Prepare();
            if (TieneIntro) intro.Prepare();
            else IntroTerminada = true;
        }

        /// <summary>La llama PantallaLogin la primera vez que aparece.</summary>
        public void IniciarSecuencia()
        {
            if (iniciado) return;
            iniciado = true;

            if (TieneIntro)
            {
                intro.loopPointReached += AlTerminarIntro;
                intro.Play();
            }
            else
            {
                IntroTerminada = true;
                if (bucle != null) bucle.Play();
            }
        }

        /// <summary>Detiene los videos del menú (los usa la pantalla de carga).</summary>
        public void Detener()
        {
            if (intro != null) { intro.loopPointReached -= AlTerminarIntro; intro.Stop(); }
            if (bucle != null) bucle.Stop();
        }

        void AlTerminarIntro(VideoPlayer vp)
        {
            intro.loopPointReached -= AlTerminarIntro;
            if (bucle != null) bucle.Play();
            intro.Stop();
            IntroTerminada = true;
        }
    }
}
