using UnityEngine;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// Visual de los Toggle: sirve para la casilla "He leído..." y para el
    /// interruptor "Recuérdame" (perilla que se desliza).
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    public class InterruptorVisual : MonoBehaviour
    {
        [Header("Casilla: se muestra/oculta este objeto")]
        public GameObject marca;

        [Header("Interruptor deslizante (opcional)")]
        public Image fondo;
        public RectTransform perilla;
        public Color colorApagado = new Color(1f, 1f, 1f, 0.35f);
        public Color colorEncendido = new Color(0.945f, 0.494f, 0.027f, 1f);
        public float recorrido = 32f;

        Toggle toggle;

        void Awake()
        {
            toggle = GetComponent<Toggle>();
            toggle.transition = Selectable.Transition.None;
            toggle.onValueChanged.AddListener(_ => Refrescar(false));
        }

        void OnEnable()
        {
            if (toggle == null) toggle = GetComponent<Toggle>();
            Refrescar(true);
        }

        void Update()
        {
            if (perilla == null) return;
            float objetivo = toggle.isOn ? recorrido / 2f : -recorrido / 2f;
            var p = perilla.anchoredPosition;
            p.x = Mathf.Lerp(p.x, objetivo, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            perilla.anchoredPosition = p;
            if (fondo != null)
                fondo.color = Color.Lerp(fondo.color, toggle.isOn ? colorEncendido : colorApagado, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        }

        void Refrescar(bool instantaneo)
        {
            if (marca != null) marca.SetActive(toggle.isOn);
            if (instantaneo && perilla != null)
            {
                var p = perilla.anchoredPosition;
                p.x = toggle.isOn ? recorrido / 2f : -recorrido / 2f;
                perilla.anchoredPosition = p;
                if (fondo != null) fondo.color = toggle.isOn ? colorEncendido : colorApagado;
            }
        }
    }
}
