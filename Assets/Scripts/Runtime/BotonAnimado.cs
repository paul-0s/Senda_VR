using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// Hover y click de los botones (equivale a las variantes Default/Variant2
    /// de los componentes BOTON de Figma). Funciona con mouse y con los
    /// controles de VR (XR Interaction Toolkit).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class BotonAnimado : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image fondo;
        public Color colorNormal = Color.white;
        public Color colorHover = Color.white;
        public Color colorDeshabilitado = new Color(0.51f, 0.51f, 0.51f, 1f);
        public float escalaHover = 1.04f;
        public float escalaPresionado = 0.97f;
        public float velocidad = 12f;

        Button boton;
        bool encima, presionado;

        void Awake()
        {
            boton = GetComponent<Button>();
            boton.transition = Selectable.Transition.None;
            if (fondo == null) fondo = GetComponent<Image>();
        }

        void OnDisable()
        {
            encima = presionado = false;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            bool activo = boton.IsInteractable();
            Color objetivo = !activo ? colorDeshabilitado : (encima ? colorHover : colorNormal);
            float escala = !activo ? 1f : (presionado ? escalaPresionado : (encima ? escalaHover : 1f));
            float k = 1f - Mathf.Exp(-velocidad * Time.unscaledDeltaTime);
            if (fondo != null) fondo.color = Color.Lerp(fondo.color, objetivo, k);
            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * escala, k);
        }

        public void OnPointerEnter(PointerEventData e) => encima = true;
        public void OnPointerExit(PointerEventData e) { encima = false; presionado = false; }
        public void OnPointerDown(PointerEventData e) => presionado = true;
        public void OnPointerUp(PointerEventData e) => presionado = false;
    }
}
