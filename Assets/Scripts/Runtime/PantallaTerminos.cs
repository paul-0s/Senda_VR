using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// Términos y condiciones paginados (variantes 1 a 6 del componente
    /// "terminos y condiciones"). El botón de aceptar se activa solo en la
    /// última página y con la casilla marcada.
    /// </summary>
    public class PantallaTerminos : PantallaBase
    {
        [TextArea(4, 10)] public string[] secciones = TextosSenda.SeccionesTerminos;

        public TextMeshProUGUI textoSeccion;
        public CanvasGroup grupoTexto;
        public Image[] puntos;
        public Button botonAnterior;
        public Button botonSiguiente;
        public GameObject filaCasilla;
        public Toggle casilla;
        public Button botonAceptar;
        public Button botonSalir;
        public RectTransform panel;
        public PantallaBase siguiente;

        public Color colorPuntoActivo = Color.white;
        public Color colorPuntoInactivo = new Color(1f, 1f, 1f, 0.3f);

        int pagina;
        Coroutine cambio;

        protected override void Awake()
        {
            base.Awake();
            botonAnterior.onClick.AddListener(() => IrAPagina(pagina - 1));
            botonSiguiente.onClick.AddListener(() => IrAPagina(pagina + 1));
            for (int i = 0; i < puntos.Length; i++)
            {
                int indice = i;
                var b = puntos[i].GetComponent<Button>();
                if (b != null) b.onClick.AddListener(() => IrAPagina(indice));
            }
            casilla.onValueChanged.AddListener(_ => Refrescar());
            botonAceptar.onClick.AddListener(() => IrA(siguiente));
            botonSalir.onClick.AddListener(Salir);
        }

        protected override void AlMostrar()
        {
            pagina = 0;
            casilla.isOn = false;
            textoSeccion.text = secciones[0];
            grupoTexto.alpha = 1f;
            Refrescar();
            StartCoroutine(EntradaPanel());
        }

        IEnumerator EntradaPanel()
        {
            var final = panel.anchoredPosition;
            var inicio = final + new Vector2(0f, -40f);
            yield return Animar(0.6f, t => panel.anchoredPosition = Vector2.LerpUnclamped(inicio, final, t), SendaEstilo.EaseOutCubic);
        }

        void IrAPagina(int nueva)
        {
            nueva = Mathf.Clamp(nueva, 0, secciones.Length - 1);
            if (nueva == pagina) return;
            pagina = nueva;
            if (cambio != null) StopCoroutine(cambio);
            cambio = StartCoroutine(CambiarTexto(secciones[pagina]));
            Refrescar();
        }

        IEnumerator CambiarTexto(string nuevo)
        {
            float a = grupoTexto.alpha;
            yield return Animar(0.15f, t => grupoTexto.alpha = Mathf.Lerp(a, 0f, t));
            textoSeccion.text = nuevo;
            yield return Animar(0.25f, t => grupoTexto.alpha = t, SendaEstilo.EaseOutCubic);
        }

        void Refrescar()
        {
            bool ultima = pagina == secciones.Length - 1;
            for (int i = 0; i < puntos.Length; i++)
            {
                puntos[i].gameObject.SetActive(i < secciones.Length);
                puntos[i].color = i == pagina ? colorPuntoActivo : colorPuntoInactivo;
            }
            botonAnterior.interactable = pagina > 0;
            botonSiguiente.interactable = !ultima;
            filaCasilla.SetActive(ultima);
            botonAceptar.interactable = ultima && casilla.isOn;
        }

        void Salir()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
