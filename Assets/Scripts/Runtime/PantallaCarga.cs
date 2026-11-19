using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SendaVR
{
    /// <summary>
    /// Pantalla de carga y bienvenida: reproduce "bienvenida.mp4" mientras la
    /// escena siguiente (el cuarto) se carga en segundo plano. Cuando termina
    /// el video, funde a negro y entra al mundo 3D.
    /// Si no hay escena siguiente, cierra el menú y le da el control al Jugador.
    /// </summary>
    public class PantallaCarga : PantallaBase
    {
        public VideoPlayer video;
        public RawImage imagen;
        [Tooltip("Video del menú que se detiene para no gastar rendimiento mientras corre la bienvenida.")]
        public ControlVideoFondo videoFondo;

        [Tooltip("Escena a cargar (la llena PantallaLogin con su 'Escena Siguiente').")]
        public string escenaSiguiente = "";

        [Header("Tiempos (segundos)")]
        public float fundidoFinal = 0.8f;
        [Tooltip("Si el video no avisa que terminó, se continúa igual después de su duración + este margen.")]
        public float margenSeguridad = 3f;

        bool enCurso;

        protected override void AlMostrar()
        {
            if (enCurso) return;
            enCurso = true;
            StartCoroutine(Secuencia());
        }

        IEnumerator Secuencia()
        {
            if (imagen != null) imagen.color = Color.white;

            // 1) Empieza a cargar el cuarto en segundo plano (sin mostrarlo aún)
            AsyncOperation carga = null;
            if (!string.IsNullOrEmpty(escenaSiguiente))
            {
                if (Application.CanStreamedLevelBeLoaded(escenaSiguiente))
                {
                    carga = SceneManager.LoadSceneAsync(escenaSiguiente);
                    carga.allowSceneActivation = false;
                }
                else
                {
                    Debug.LogWarning("[Senda.vr] La escena '" + escenaSiguiente + "' no está en File > Build Profiles > Scene List. " +
                                     "Agrégala para poder entrar al cuarto.");
                }
            }

            // 2) Reproduce la bienvenida
            if (video != null && video.clip != null)
            {
                video.isLooping = false;
                PantallaBase.LimpiarTextura(video.targetTexture, Color.black);
                video.Prepare();
                float limite = Time.unscaledTime + 5f;
                while (!video.isPrepared && Time.unscaledTime < limite) yield return null;

                bool termino = false;
                VideoPlayer.EventHandler alTerminar = _ => termino = true;
                video.loopPointReached += alTerminar;
                video.Play();

                // ya no se ve el fondo del menú: se detiene para ahorrar rendimiento
                yield return new WaitForSecondsRealtime(duracionFundido);
                if (videoFondo != null) videoFondo.Detener();

                limite = Time.unscaledTime + (float)video.clip.length + margenSeguridad;
                while (!termino && Time.unscaledTime < limite) yield return null;
                video.loopPointReached -= alTerminar;
            }

            // 3) Fundido a negro
            if (imagen != null)
                yield return Animar(fundidoFinal, t => imagen.color = Color.Lerp(Color.white, Color.black, t));

            // 4) Entra al cuarto (o, si no hay escena, al Jugador de esta escena)
            if (carga != null)
            {
                while (carga.progress < 0.9f) yield return null;
                carga.allowSceneActivation = true;
            }
            else
            {
                enCurso = false;
                if (navegador != null) navegador.CerrarMenu();
            }
        }
    }
}
