using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace SendaVR
{
    /// <summary>
    /// Cambia el video 360 del fondo según la pantalla del menú que se está mostrando
    /// (por ejemplo: degradado en Términos y bosque en Inicio de sesión), con un fundido suave.
    /// Una regla puede tener además un "video de entrada" que se ve una sola vez antes de esa pantalla
    /// (la animación del bosque que se abre) y que empalma sin corte con el video en bucle.
    /// Va en el objeto "Fondo 360" (junto a EsferaVideo360 y VideoPlayer).
    /// </summary>
    [RequireComponent(typeof(VideoPlayer), typeof(EsferaVideo360))]
    public class FondoSegunPantalla : MonoBehaviour
    {
        [System.Serializable]
        public class Regla
        {
            public PantallaBase pantalla;
            public VideoClip video;
            [Range(0.2f, 1.2f)] public float brillo = 0.7f;
            [Tooltip("Opcional: se reproduce una vez antes de mostrar la pantalla; el panel aparece al terminar.")]
            public VideoClip videoEntrada;
            [Tooltip("Segundo del video en bucle donde sigue la entrada (para que no se note el corte).")]
            public float continuarEnSegundo;
        }

        public NavegadorPantallas navegador;
        public Regla[] reglas;
        public float duracionFundido = 0.35f;

        VideoPlayer vp, vpEntrada;
        RenderTexture rtEntrada;
        EsferaVideo360 esfera;
        Renderer rend;
        Coroutine cambio;
        bool entradaVista;

        void Awake()
        {
            vp = GetComponent<VideoPlayer>();
            esfera = GetComponent<EsferaVideo360>();
            rend = GetComponent<Renderer>();
        }

        void OnEnable()
        {
            if (navegador == null) return;
            navegador.AlCambiarPantalla += AlCambiar;
            navegador.antesDeMostrar = AntesDeMostrar;
        }

        void OnDisable()
        {
            if (navegador == null) return;
            navegador.AlCambiarPantalla -= AlCambiar;
            if (navegador.antesDeMostrar == (System.Func<PantallaBase, IEnumerator>)AntesDeMostrar) navegador.antesDeMostrar = null;
        }

        void OnDestroy()
        {
            if (rtEntrada != null) { rtEntrada.Release(); Destroy(rtEntrada); }
        }

        Regla ReglaDe(PantallaBase p)
        {
            if (reglas == null) return null;
            foreach (var r in reglas) if (r != null && r.pantalla == p) return r;
            return null;
        }

        void AlCambiar(PantallaBase p)
        {
            var r = ReglaDe(p);
            if (r == null || r.video == null) return;
            if (vp.clip == r.video) { esfera.FijarBrillo(r.brillo); return; }
            if (cambio != null) StopCoroutine(cambio);
            cambio = StartCoroutine(Cambiar(r));
        }

        IEnumerator Fundir(Color desde, Color hasta)
        {
            var mat = rend.material;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / duracionFundido)
            {
                mat.color = Color.Lerp(desde, hasta, t);
                yield return null;
            }
            mat.color = hasta;
        }

        static Color Gris(float b) => new Color(b, b, b, 1f);

        IEnumerator Cambiar(Regla r)
        {
            yield return Fundir(rend.material.color, Color.black);
            vp.Stop();
            vp.clip = r.video;
            vp.isLooping = true;
            vp.Prepare();
            float limite = Time.unscaledTime + 5f;
            while (!vp.isPrepared && Time.unscaledTime < limite) yield return null;
            vp.Play();
            yield return PantallaBase.EsperarPrimerCuadro(vp);
            yield return Fundir(Color.black, Gris(r.brillo));
            esfera.FijarBrillo(r.brillo);
            cambio = null;
        }

        /// <summary>Lo llama el navegador entre ocultar una pantalla y mostrar la siguiente.</summary>
        IEnumerator AntesDeMostrar(PantallaBase destino)
        {
            var r = ReglaDe(destino);
            if (r == null || r.videoEntrada == null || r.video == null || entradaVista) yield break;
            entradaVista = true;
            if (cambio != null) { StopCoroutine(cambio); cambio = null; }

            var mat = rend.material;
            var rtPrincipal = vp.targetTexture;

            // Reproductor aparte para la entrada (así el bosque en bucle se prepara mientras tanto)
            if (vpEntrada == null)
            {
                var go = new GameObject("Video entrada");
                go.transform.SetParent(transform, false);
                vpEntrada = go.AddComponent<VideoPlayer>();
                vpEntrada.playOnAwake = false;
                vpEntrada.renderMode = VideoRenderMode.RenderTexture;
                vpEntrada.audioOutputMode = VideoAudioOutputMode.None;
                rtEntrada = new RenderTexture(rtPrincipal != null ? rtPrincipal.width : 4096,
                                              rtPrincipal != null ? rtPrincipal.height : 2048, 0) { name = "Entrada360_RT" };
                vpEntrada.targetTexture = rtEntrada;
            }
            vpEntrada.clip = r.videoEntrada;
            vpEntrada.isLooping = false;
            vpEntrada.Prepare();

            // Fundido a negro del fondo actual (Términos) mientras se prepara la entrada
            yield return Fundir(mat.color, Color.black);
            float limite = Time.unscaledTime + 5f;
            while (!vpEntrada.isPrepared && Time.unscaledTime < limite) yield return null;

            vpEntrada.Play();
            yield return PantallaBase.EsperarPrimerCuadro(vpEntrada);
            mat.mainTexture = rtEntrada;
            vp.Stop();

            // Mientras corre la entrada, se deja listo el bosque en bucle en el punto donde sigue
            vp.clip = r.video;
            vp.isLooping = true;
            vp.Prepare();
            StartCoroutine(Fundir(Color.black, Gris(r.brillo)));

            limite = Time.unscaledTime + 5f;
            while (!vp.isPrepared && Time.unscaledTime < limite) yield return null;

            // Se deja el bosque en pausa YA en el cuadro donde sigue (así al empalmar no hay que esperar a que busque)
            long inicio = Mathf.RoundToInt(r.continuarEnSegundo * (float)vp.frameRate);
            bool buscado = false;
            VideoPlayer.EventHandler alBuscar = _ => buscado = true;
            vp.seekCompleted += alBuscar;
            vp.Pause();
            vp.frame = inicio;
            limite = Time.unscaledTime + 3f;
            while (!buscado && Time.unscaledTime < limite) yield return null;
            vp.seekCompleted -= alBuscar;

            // Espera el último cuadro de la entrada
            long ultimo = (long)r.videoEntrada.frameCount - 1;
            limite = Time.unscaledTime + (float)r.videoEntrada.length + 3f;
            while (vpEntrada.isPlaying && vpEntrada.frame < ultimo && Time.unscaledTime < limite) yield return null;

            // Empalme en el mismo cuadro: se cambia la textura (ya muestra el cuadro siguiente) y se sigue reproduciendo
            vp.Play();
            limite = Time.unscaledTime + 0.25f;
            while (vp.frame <= inicio && Time.unscaledTime < limite) yield return null; // ya hay cuadro nuevo dibujado
            mat.mainTexture = rtPrincipal;
            vpEntrada.Stop();
            esfera.FijarBrillo(r.brillo);
        }
    }
}
