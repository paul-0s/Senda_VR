using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// Inicio de sesión: primero se ve solo el fondo animado ("animacion-1")
    /// y luego aparece el panel ("animacion-2" + formulario).
    /// Es un prototipo: no hay servidor, solo valida que los campos tengan datos.
    /// </summary>
    public class PantallaLogin : PantallaBase
    {
        public RectTransform panel;
        public CanvasGroup panelGrupo;
        public CanvasGroup oscurecer;
        public TMP_InputField campoCorreo;
        public TMP_InputField campoContrasena;
        public Button botonIniciar;
        public Button botonCrearCuenta;
        public Button[] botonesSociales;
        public TextMeshProUGUI mensaje;
        public PantallaBase pantallaCrearCuenta;
        [Tooltip("Opcional: video de fondo. Si tiene intro (animacion-1), el panel aparece cuando termina.")]
        public ControlVideoFondo videoFondo;
        [Tooltip("Segundos máximos de espera a que termine la intro del video.")]
        public float esperaMaximaIntro = 15f;

        [Header("Animación de entrada")]
        public float retrasoPrimeraVez = 1.2f;
        public float retrasoAlVolver = 0.15f;

        [Header("Después de iniciar sesión")]
        [Tooltip("Nombre de la escena del cuarto (Hub). Debe estar en File > Build Profiles > Scene List.")]
        public string escenaSiguiente = "";
        [Tooltip("Pantalla de carga y bienvenida (video). Si está vacía, se carga la escena directo.")]
        public PantallaCarga pantallaCarga;

        bool yaSeMostro;
        Vector2 posPanel;
        bool posGuardada;

        protected override void Awake()
        {
            base.Awake();
            botonIniciar.onClick.AddListener(Iniciar);
            botonCrearCuenta.onClick.AddListener(() => IrA(pantallaCrearCuenta));
            foreach (var b in botonesSociales)
                if (b != null) b.onClick.AddListener(() => Entrar("Inicio de sesión simulado."));
        }

        protected override void AlMostrar()
        {
            if (!posGuardada) { posPanel = panel.anchoredPosition; posGuardada = true; }
            bool esperarIntro = !yaSeMostro && videoFondo != null && videoFondo.TieneIntro;
            if (videoFondo != null) videoFondo.IniciarSecuencia();
            StartCoroutine(Entrada(yaSeMostro ? retrasoAlVolver : retrasoPrimeraVez, esperarIntro));
            yaSeMostro = true;
        }

        IEnumerator Entrada(float retraso, bool esperarIntro)
        {
            panelGrupo.alpha = 0f;
            panelGrupo.blocksRaycasts = false;
            if (oscurecer != null) oscurecer.alpha = 0f;
            panel.anchoredPosition = posPanel + new Vector2(0f, -50f);
            if (esperarIntro)
            {
                // animacion-1: se ve solo el video; el panel entra cuando termina
                float limite = Time.unscaledTime + esperaMaximaIntro;
                while (!videoFondo.IntroTerminada && Time.unscaledTime < limite) yield return null;
                retraso = 0.2f;
            }
            yield return new WaitForSecondsRealtime(retraso);
            yield return Animar(0.7f, t =>
            {
                panelGrupo.alpha = t;
                if (oscurecer != null) oscurecer.alpha = t;
                panel.anchoredPosition = Vector2.LerpUnclamped(posPanel + new Vector2(0f, -50f), posPanel, t);
            }, SendaEstilo.EaseOutCubic);
            panelGrupo.blocksRaycasts = true;
        }

        public void MostrarMensaje(string texto, bool esError)
        {
            if (mensaje == null) return;
            mensaje.text = texto;
            mensaje.color = esError ? SendaEstilo.Error : SendaEstilo.Blanco;
        }

        public void PrecargarCorreo(string correo)
        {
            if (campoCorreo != null) campoCorreo.text = correo;
        }

        void Iniciar()
        {
            string correo = campoCorreo.text.Trim();
            if (correo.Length == 0 || campoContrasena.text.Length == 0)
            {
                MostrarMensaje("Completa tu correo y contraseña.", true);
                StartCoroutine(Sacudir());
                return;
            }
            if (!correo.Contains("@"))
            {
                MostrarMensaje("Revisa tu correo electrónico.", true);
                StartCoroutine(Sacudir());
                return;
            }
            Entrar("¡Bienvenido a Senda.vr!");
        }

        /// <summary>Entra a la experiencia: pantalla de carga/bienvenida y luego el cuarto.</summary>
        public void Entrar(string texto)
        {
            MostrarMensaje(texto, false);
            if (pantallaCarga != null)
            {
                pantallaCarga.escenaSiguiente = escenaSiguiente;
                IrA(pantallaCarga);
            }
            else if (!string.IsNullOrEmpty(escenaSiguiente))
                SceneManager.LoadScene(escenaSiguiente);
            else
            {
                // Sin escena siguiente: se cierra el menú y el Jugador de esta escena toma el control.
                Debug.Log("[Senda.vr] Sesión iniciada. Para cargar otra escena, escribe su nombre en 'Escena Siguiente' de PantallaLogin.");
                if (navegador != null) navegador.CerrarMenu();
            }
        }

        IEnumerator Sacudir()
        {
            yield return Animar(0.4f, t =>
            {
                float x = Mathf.Sin(t * Mathf.PI * 6f) * 14f * (1f - t);
                panel.anchoredPosition = posPanel + new Vector2(x, 0f);
            });
        }
    }
}
