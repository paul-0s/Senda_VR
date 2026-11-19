using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// Ventana que aparece al hacer clic en un objeto: muestra el objeto
    /// girando, su nombre y una descripción educativa.
    ///   PC: botones con el mouse, o E = guardar / Esc = cerrar.
    ///   VR: gatillo derecho = guardar / botón B o gatillo izquierdo = cerrar.
    /// </summary>
    public class VentanaObjeto : MonoBehaviour
    {
        public static VentanaObjeto Instancia { get; private set; }
        public static bool Abierta => Instancia != null && Instancia.abierta;

        [Header("Referencias")]
        public CanvasGroup grupo;
        public RectTransform panel;
        public RawImage vista;
        public TextMeshProUGUI titulo;
        public TextMeshProUGUI descripcion;
        public TextMeshProUGUI ayuda;
        public Button botonPrincipal;
        public TextMeshProUGUI textoBotonPrincipal;
        public Button botonCerrar;

        [Header("Vista del objeto")]
        public float velocidadGiro = 30f;
        public Color fondoVista = new Color(0.17f, 0.17f, 0.17f, 1f);

        bool abierta;
        float ignorarHasta;
        System.Action accionPrincipal;
        InputAction principal, cerrar;

        // Estudio de fotos: un lugar lejano con su propia cámara que dibuja el objeto en la ventana
        Transform estudio, giro;
        Camera camaraVista;
        RenderTexture texturaVista;
        GameObject clon;
        CanvasAdaptable adaptable;

        void Awake()
        {
            Instancia = this;
            adaptable = GetComponent<CanvasAdaptable>();

            principal = new InputAction("Principal", InputActionType.Button);
            principal.AddBinding("<Keyboard>/e");
            principal.AddBinding("<Keyboard>/enter");
            principal.AddBinding("<Gamepad>/buttonSouth");
            principal.AddBinding("<XRController>{RightHand}/{TriggerButton}");

            cerrar = new InputAction("Cerrar", InputActionType.Button);
            cerrar.AddBinding("<Keyboard>/escape");
            cerrar.AddBinding("<Keyboard>/q");
            cerrar.AddBinding("<Gamepad>/buttonEast");
            cerrar.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            cerrar.AddBinding("<XRController>{LeftHand}/{TriggerButton}");

            botonPrincipal.onClick.AddListener(() => { if (abierta && Time.unscaledTime > ignorarHasta) accionPrincipal?.Invoke(); });
            botonCerrar.onClick.AddListener(Cerrar);

            grupo.alpha = 0f;
            grupo.blocksRaycasts = false;
            grupo.interactable = false;
        }

        void OnEnable() { principal.Enable(); cerrar.Enable(); }
        void OnDisable() { principal.Disable(); cerrar.Disable(); }

        void OnDestroy()
        {
            if (Instancia == this) Instancia = null;
            if (abierta) EstadoJugador.CerrarVentana();
            if (texturaVista != null) texturaVista.Release();
            if (estudio != null) Destroy(estudio.gameObject);
        }

        // ------------------------------------------------------------ abrir

        public void Mostrar(ObjetoTomable objeto)
        {
            Abrir(objeto.gameObject, objeto.nombreVisible, objeto.descripcion);
            ConfigurarPrincipal("Guardar en la mochila", () => { objeto.Tomar(); Cerrar(); });
            ayuda.text = EstadoJugador.EnVR
                ? "Gatillo derecho: guardar   ·   Botón B: cerrar"
                : "E: guardar   ·   Esc: cerrar";
        }

        public void Mostrar(Computador pc)
        {
            var inv = Inventario.Instancia;
            bool completo = inv == null || inv.Completo;
            string texto;
            if (completo)
            {
                texto = "¡Tienes todo tu equipo!\n\nRuta disponible: <b>" + pc.nombreSendero + "</b>.\n" +
                        "Recuerda: avisa a alguien a dónde vas y a qué hora piensas volver.";
                ConfigurarPrincipal("Ir al sendero", () => IrAlSendero(pc));
            }
            else
            {
                texto = "Antes de salir al sendero, prepara tu equipo.\n\nTe falta: <b>" +
                        string.Join(", ", inv.Faltantes.Select(TextosObjetos.Nombre)) + "</b>.\n\n" +
                        "Busca estos objetos en el cuarto y haz clic en ellos.";
                ConfigurarPrincipal(null, null);
            }
            Abrir(pc.gameObject, pc.titulo, texto);
            ayuda.text = EstadoJugador.EnVR
                ? (completo ? "Gatillo derecho: ir al sendero   ·   " : "") + "Botón B: cerrar"
                : (completo ? "E: ir al sendero   ·   " : "") + "Esc: cerrar";
        }

        void ConfigurarPrincipal(string texto, System.Action accion)
        {
            accionPrincipal = accion;
            botonPrincipal.gameObject.SetActive(accion != null);
            if (texto != null) textoBotonPrincipal.text = texto;
        }

        void Abrir(GameObject origen, string nombre, string texto)
        {
            titulo.text = nombre;
            descripcion.text = texto;
            PrepararVista(origen);

            if (!abierta) EstadoJugador.AbrirVentana();
            abierta = true;
            ignorarHasta = Time.unscaledTime + 0.3f; // el mismo clic que abrió no debe "guardar"
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = !EstadoJugador.EnVR;

            if (adaptable != null) adaptable.PonerFrenteA(Camera.main);
            StopAllCoroutines();
            StartCoroutine(Animar(true));
        }

        public void Cerrar()
        {
            if (!abierta) return;
            abierta = false;
            StopAllCoroutines();
            StartCoroutine(Animar(false));
            EstadoJugador.CerrarVentana();
        }

        void IrAlSendero(Computador pc)
        {
            if (string.IsNullOrEmpty(pc.escenaSendero) || !Application.CanStreamedLevelBeLoaded(pc.escenaSendero))
            {
                descripcion.text = "La escena '" + pc.escenaSendero + "' todavía no está agregada en " +
                                   "File > Build Profiles > Scene List.";
                return;
            }
            Cerrar();
            SceneManager.LoadScene(pc.escenaSendero);
        }

        // ------------------------------------------------------------ loop

        void Update()
        {
            if (!abierta) return;
            if (giro != null) giro.Rotate(0f, velocidadGiro * Time.unscaledDeltaTime, 0f, Space.World);
            if (Time.unscaledTime < ignorarHasta) return;
            if (cerrar.WasPressedThisFrame()) Cerrar();
            else if (principal.WasPressedThisFrame() && accionPrincipal != null) accionPrincipal();
        }

        IEnumerator Animar(bool mostrar)
        {
            grupo.blocksRaycasts = mostrar;
            grupo.interactable = mostrar;
            if (mostrar && camaraVista != null) camaraVista.enabled = true;
            float desde = grupo.alpha, hasta = mostrar ? 1f : 0f;
            float t = 0f, dur = mostrar ? 0.25f : 0.18f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float k = SendaEstilo.EaseOutCubic(Mathf.Clamp01(t));
                grupo.alpha = Mathf.Lerp(desde, hasta, k);
                if (panel != null) panel.localScale = Vector3.one * Mathf.Lerp(mostrar ? 0.92f : 1f, mostrar ? 1f : 0.95f, k);
                yield return null;
            }
            if (!mostrar && clon != null) { Destroy(clon); clon = null; }
            if (!mostrar && camaraVista != null) camaraVista.enabled = false;
        }

        // ------------------------------------------------------------ vista 3D

        void CrearEstudio()
        {
            estudio = new GameObject("Senda_EstudioVista").transform;
            estudio.position = new Vector3(0f, -5000f, 0f);
            giro = new GameObject("Giro").transform;
            giro.SetParent(estudio, false);

            texturaVista = new RenderTexture(768, 768, 24) { name = "VistaObjeto_RT", antiAliasing = 4 };
            camaraVista = new GameObject("Camara vista").AddComponent<Camera>();
            camaraVista.transform.SetParent(estudio, false);
            camaraVista.transform.localPosition = new Vector3(0f, 0.35f, -2.2f);
            camaraVista.transform.LookAt(estudio.position);
            camaraVista.fieldOfView = 30f;
            camaraVista.nearClipPlane = 0.05f;
            camaraVista.farClipPlane = 10f;
            camaraVista.clearFlags = CameraClearFlags.SolidColor;
            camaraVista.backgroundColor = fondoVista;
            camaraVista.targetTexture = texturaVista;
            camaraVista.stereoTargetEye = StereoTargetEyeMask.None;
            camaraVista.enabled = false;

            // Luz puntual (solo ilumina el estudio, no el cuarto)
            var luz = new GameObject("Luz vista").AddComponent<Light>();
            luz.type = LightType.Point;
            luz.range = 6f;
            luz.intensity = 3f;
            luz.transform.SetParent(estudio, false);
            luz.transform.localPosition = new Vector3(-1.2f, 1.5f, -1.8f);

            vista.texture = texturaVista;
        }

        void PrepararVista(GameObject origen)
        {
            if (estudio == null) CrearEstudio();
            if (clon != null) Destroy(clon);
            giro.localRotation = Quaternion.identity;

            bool activo = origen.activeSelf;
            origen.SetActive(true);
            clon = Instantiate(origen, giro);
            origen.SetActive(activo);
            clon.name = "Vista_" + origen.name;

            // Solo queremos la apariencia: fuera scripts, colisiones y físicas
            foreach (var mb in clon.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(mb);
            foreach (var col in clon.GetComponentsInChildren<Collider>(true)) Destroy(col);
            foreach (var rb in clon.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);

            clon.transform.localRotation = origen.transform.rotation;
            clon.transform.localScale = origen.transform.lossyScale;
            clon.transform.localPosition = Vector3.zero;

            var renders = clon.GetComponentsInChildren<Renderer>(true);
            if (renders.Length == 0) return;
            var caja = renders[0].bounds;
            foreach (var r in renders) caja.Encapsulate(r.bounds);

            // Centrar y ajustar el tamaño para que quepa (~1 metro)
            float tam = Mathf.Max(caja.size.x, caja.size.y, caja.size.z);
            float factor = tam > 0.0001f ? 1f / tam : 1f;
            clon.transform.localScale *= factor;

            // recalcular la caja tras escalar y mover su centro al eje de giro
            caja = renders[0].bounds;
            foreach (var r in renders) caja.Encapsulate(r.bounds);
            clon.transform.position += giro.position - caja.center;
        }
    }
}
