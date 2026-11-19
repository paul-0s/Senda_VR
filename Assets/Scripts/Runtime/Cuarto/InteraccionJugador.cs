using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// Mira al centro de la vista (PC) o hacia donde miras con el visor (VR).
    /// Si apuntas a un objeto tomable o al computador, aparece su nombre;
    /// con clic / E (PC) o el gatillo derecho (VR) se abre su ventana.
    /// Va en la cámara del Jugador.
    /// </summary>
    public class InteraccionJugador : MonoBehaviour
    {
        public Camera camara;
        [Tooltip("Distancia máxima para alcanzar un objeto, en metros.")]
        public float alcance = 3f;

        [Header("Interfaz (la crea Senda.vr > Construir cuarto)")]
        public Image mira;
        public TextMeshProUGUI etiqueta;
        public CanvasGroup grupoEtiqueta;

        public Color colorMira = new Color(1f, 1f, 1f, 0.7f);
        public Color colorMiraActiva = new Color(0.945f, 0.494f, 0.027f, 1f);

        InputAction usar;
        Component apuntado;

        void Awake()
        {
            if (camara == null) camara = GetComponentInChildren<Camera>();
            if (camara == null) camara = Camera.main;
            usar = new InputAction("Usar", InputActionType.Button);
            usar.AddBinding("<Mouse>/leftButton");
            usar.AddBinding("<Keyboard>/e");
            usar.AddBinding("<Gamepad>/buttonSouth");
            usar.AddBinding("<XRController>{RightHand}/{TriggerButton}");
        }

        void OnEnable() => usar.Enable();
        void OnDisable() => usar.Disable();

        void Update()
        {
            if (camara == null) return;
            bool libre = !EstadoJugador.Bloqueado && !VentanaObjeto.Abierta;

            apuntado = libre ? Buscar() : null;
            ActualizarInterfaz(libre);

            if (apuntado == null || !usar.WasPressedThisFrame()) return;
            // En PC, el primer clic solo captura el mouse
            if (!EstadoJugador.EnVR && Cursor.lockState != CursorLockMode.Locked &&
                Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) return;

            var ventana = VentanaObjeto.Instancia;
            if (ventana == null) return;
            if (apuntado is ObjetoTomable obj) ventana.Mostrar(obj);
            else if (apuntado is Computador pc) ventana.Mostrar(pc);
        }

        Component Buscar()
        {
            var rayo = new Ray(camara.transform.position, camara.transform.forward);
            if (!Physics.Raycast(rayo, out var hit, alcance, ~0, QueryTriggerInteraction.Collide)) return null;
            var obj = hit.collider.GetComponentInParent<ObjetoTomable>();
            if (obj != null && !obj.Tomado) return obj;
            return hit.collider.GetComponentInParent<Computador>();
        }

        void ActualizarInterfaz(bool libre)
        {
            if (mira != null)
            {
                mira.enabled = libre;
                mira.color = apuntado != null ? colorMiraActiva : colorMira;
                float tam = apuntado != null ? 1.6f : 1f;
                mira.rectTransform.localScale = Vector3.Lerp(mira.rectTransform.localScale, Vector3.one * tam, Time.unscaledDeltaTime * 12f);
            }

            if (grupoEtiqueta != null)
            {
                float objetivo = apuntado != null ? 1f : 0f;
                grupoEtiqueta.alpha = Mathf.MoveTowards(grupoEtiqueta.alpha, objetivo, Time.unscaledDeltaTime * 6f);
            }

            if (etiqueta != null && apuntado != null)
            {
                string nombre = apuntado is ObjetoTomable o ? o.nombreVisible
                              : apuntado is Computador c ? c.titulo : "";
                string tecla = EstadoJugador.EnVR ? "Gatillo" : "Clic o E";
                etiqueta.text = "<b>" + nombre + "</b>\n<size=70%>" + tecla + " para ver</size>";
            }
        }
    }
}
