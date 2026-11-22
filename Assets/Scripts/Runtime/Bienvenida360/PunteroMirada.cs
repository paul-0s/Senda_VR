using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;

namespace SendaVR
{
    /// <summary>
    /// Modo "como en VR" para probar en PC: el mouse mueve la cabeza (sin mantener ningún botón)
    /// y un punto en el centro de la vista hace de puntero. Clic izquierdo = presionar lo que estás mirando.
    /// Esc suelta el mouse; clic en la ventana lo vuelve a capturar.
    /// Con visor VR conectado se apaga solo (ahí se usarán los rayos de los controles).
    /// Va en el objeto "Cabeza (mouse en PC)", junto a CamaraLibre360.
    /// </summary>
    public class PunteroMirada : MonoBehaviour
    {
        public Camera camara;
        public Sprite spritePunto;
        public Color colorNormal = new Color(1f, 1f, 1f, 0.85f);
        public Color colorEncima = new Color(1f, 0.55f, 0.1f, 1f);
        public float tamNormal = 10f, tamEncima = 22f;
        [TextArea] public string ayuda = "Mueve el mouse para mirar alrededor  ·  Apunta con el punto y haz clic para elegir  ·  Esc suelta el mouse";
        public float segundosAyuda = 7f;

        readonly List<RaycastResult> resultados = new List<RaycastResult>();
        PointerEventData ped;
        GameObject encima, presionado, clicPendiente;
        bool bloqueadoAntes;
        RectTransform punto;
        Image imgPunto;
        CanvasGroup grupoAyuda;
        float tiempoAyuda;

        // acciones del módulo de UI que se desactivan mientras el mouse está capturado (para no hacer doble clic)
        InputSystemUIInputModule moduloIS;
        InputActionReference guardPoint, guardClick;

        bool EnVR => XRSettings.isDeviceActive;

        void Start()
        {
            if (camara == null) camara = Camera.main;
            CrearInterfaz();
            if (!EnVR) Capturar(true);
        }

        void OnDisable() { Capturar(false); }

        void Capturar(bool si)
        {
            Cursor.lockState = si ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !si;
            ModuloUsaMouse(!si);
        }

        void ModuloUsaMouse(bool usar)
        {
            var es = EventSystem.current;
            if (es == null) return;
            if (moduloIS == null) moduloIS = es.GetComponent<InputSystemUIInputModule>();
            if (moduloIS != null)
            {
                if (!usar)
                {
                    if (moduloIS.point != null) { guardPoint = moduloIS.point; guardClick = moduloIS.leftClick; }
                    moduloIS.point = null;
                    moduloIS.leftClick = null;
                }
                else if (guardPoint != null)
                {
                    moduloIS.point = guardPoint;
                    moduloIS.leftClick = guardClick;
                }
            }
            // XR Interaction Toolkit (si está instalado): XRUIInputModule.enableMouseInput
            foreach (var m in es.GetComponents<BaseInputModule>())
            {
                var prop = m.GetType().GetProperty("enableMouseInput");
                if (prop != null && prop.CanWrite) prop.SetValue(m, usar);
            }
        }

        void Update()
        {
            if (EnVR)
            {
                if (punto != null) punto.gameObject.SetActive(false);
                return;
            }
            var mouse = Mouse.current;
            var teclado = Keyboard.current;

            if (teclado != null && teclado.escapeKey.wasPressedThisFrame) Capturar(false);
            else if (mouse != null && Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame) Capturar(true);

            bool activo = Cursor.lockState == CursorLockMode.Locked;
            punto.gameObject.SetActive(activo);

            // Ayuda que se desvanece
            if (grupoAyuda != null)
            {
                tiempoAyuda += Time.unscaledDeltaTime;
                grupoAyuda.alpha = Mathf.Clamp01((segundosAyuda - tiempoAyuda) / 1.5f);
            }

            if (!activo) { CambiarEncima(null); bloqueadoAntes = false; return; }

            var es = EventSystem.current;
            if (es == null || camara == null) return;
            if (ped == null) ped = new PointerEventData(es);
            ped.Reset();
            ped.position = new Vector2(camara.pixelWidth * 0.5f, camara.pixelHeight * 0.5f);
            ped.button = PointerEventData.InputButton.Left;

            resultados.Clear();
            es.RaycastAll(ped, resultados);
            RaycastResult golpe = default;
            foreach (var r in resultados) if (r.gameObject != null) { golpe = r; break; }
            ped.pointerCurrentRaycast = golpe;
            var objetivo = golpe.gameObject;

            CambiarEncima(objetivo);

            // Botón bajo el punto → el punto crece y se pone naranjo
            bool sobreAlgo = objetivo != null && ExecuteEvents.GetEventHandler<IPointerClickHandler>(objetivo) != null
                             || objetivo != null && ExecuteEvents.GetEventHandler<IPointerDownHandler>(objetivo) != null;
            float tam = Mathf.Lerp(punto.sizeDelta.x, sobreAlgo ? tamEncima : tamNormal, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            punto.sizeDelta = new Vector2(tam, tam);
            imgPunto.color = Color.Lerp(imgPunto.color, sobreAlgo ? colorEncima : colorNormal, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));

            // Solo cuenta el clic si el mouse ya estaba capturado (el clic que lo captura no presiona nada)
            if (mouse != null && bloqueadoAntes)
            {
                if (mouse.leftButton.wasPressedThisFrame) Presionar(es, objetivo, golpe);
                if (mouse.leftButton.wasReleasedThisFrame) Soltar(objetivo);
            }
            bloqueadoAntes = true;
        }

        void CambiarEncima(GameObject nuevo)
        {
            var antes = encima != null ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(encima) : null;
            var ahora = nuevo != null ? ExecuteEvents.GetEventHandler<IPointerEnterHandler>(nuevo) : null;
            encima = nuevo;
            if (antes == ahora) return;
            if (ped == null && EventSystem.current != null) ped = new PointerEventData(EventSystem.current);
            if (ped == null) return;
            if (antes != null)
            {
                ExecuteEvents.ExecuteHierarchy(antes, ped, ExecuteEvents.pointerExitHandler);
                ped.hovered.Remove(antes);
            }
            if (ahora != null)
            {
                ped.pointerEnter = ahora;
                ped.hovered.Add(ahora);
                ExecuteEvents.ExecuteHierarchy(ahora, ped, ExecuteEvents.pointerEnterHandler);
            }
        }

        void Presionar(EventSystem es, GameObject objetivo, RaycastResult golpe)
        {
            ped.eligibleForClick = true;
            ped.pressPosition = ped.position;
            ped.pointerPressRaycast = golpe;
            ped.clickCount = 1;
            ped.clickTime = Time.unscaledTime;

            if (objetivo == null) { es.SetSelectedGameObject(null); presionado = clicPendiente = null; return; }

            // deselecciona si se presiona otra cosa (igual que el módulo normal)
            var seleccionable = ExecuteEvents.GetEventHandler<ISelectHandler>(objetivo);
            if (seleccionable != es.currentSelectedGameObject) es.SetSelectedGameObject(null, ped);

            presionado = ExecuteEvents.ExecuteHierarchy(objetivo, ped, ExecuteEvents.pointerDownHandler);
            clicPendiente = ExecuteEvents.GetEventHandler<IPointerClickHandler>(objetivo);
            if (presionado == null) presionado = clicPendiente;
            ped.pointerPress = presionado;
            ped.rawPointerPress = objetivo;
        }

        void Soltar(GameObject objetivo)
        {
            if (presionado != null) ExecuteEvents.Execute(presionado, ped, ExecuteEvents.pointerUpHandler);
            var clicAhora = objetivo != null ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(objetivo) : null;
            if (clicPendiente != null && clicAhora == clicPendiente)
                ExecuteEvents.Execute(clicPendiente, ped, ExecuteEvents.pointerClickHandler);
            presionado = clicPendiente = null;
            ped.pointerPress = null;
            ped.rawPointerPress = null;
            ped.eligibleForClick = false;
        }

        void CrearInterfaz()
        {
            var go = new GameObject("Punto de mira (PC)", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 500;
            var cs = go.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920, 1080);
            cs.matchWidthOrHeight = 0.5f;

            var p = new GameObject("Punto", typeof(RectTransform), typeof(Image));
            p.transform.SetParent(go.transform, false);
            punto = (RectTransform)p.transform;
            punto.anchorMin = punto.anchorMax = punto.pivot = new Vector2(0.5f, 0.5f);
            punto.sizeDelta = new Vector2(tamNormal, tamNormal);
            imgPunto = p.GetComponent<Image>();
            imgPunto.sprite = spritePunto;
            imgPunto.color = colorNormal;
            imgPunto.raycastTarget = false;

            if (string.IsNullOrEmpty(ayuda) || EnVR) return;
            var a = new GameObject("Ayuda", typeof(RectTransform), typeof(CanvasGroup));
            a.transform.SetParent(go.transform, false);
            var art = (RectTransform)a.transform;
            art.anchorMin = art.anchorMax = art.pivot = new Vector2(0.5f, 0f);
            art.anchoredPosition = new Vector2(0f, 60f);
            art.sizeDelta = new Vector2(1400f, 56f);
            grupoAyuda = a.GetComponent<CanvasGroup>();
            grupoAyuda.blocksRaycasts = false;
            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondo.transform.SetParent(a.transform, false);
            var frt = (RectTransform)fondo.transform;
            frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one; frt.offsetMin = frt.offsetMax = Vector2.zero;
            var fi = fondo.GetComponent<Image>();
            fi.color = new Color(0f, 0f, 0f, 0.55f);
            fi.raycastTarget = false;
            var t = new GameObject("Texto", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            t.transform.SetParent(a.transform, false);
            var trt = (RectTransform)t.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
            var tmp = t.GetComponent<TMPro.TextMeshProUGUI>();
            tmp.text = ayuda;
            tmp.fontSize = 24;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }
    }
}
