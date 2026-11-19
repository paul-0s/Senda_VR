using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Lista de equipo para el sendero (la "mochila"). Se muestra en una
    /// pequeña lista en la esquina de la vista.
    /// </summary>
    public class Inventario : MonoBehaviour
    {
        public static Inventario Instancia { get; private set; }

        [Tooltip("Objetos que hay que reunir antes de ir al sendero.")]
        public List<TipoObjeto> requeridos = new List<TipoObjeto>
        {
            TipoObjeto.Mochila, TipoObjeto.Botella, TipoObjeto.Zapatillas, TipoObjeto.PaloTrekking
        };

        public TextMeshProUGUI textoLista;
        public TextMeshProUGUI textoAviso;
        public CanvasGroup grupoAviso;

        readonly HashSet<TipoObjeto> tomados = new HashSet<TipoObjeto>();
        float avisoHasta;

        public bool Completo => requeridos.All(tomados.Contains);
        public IEnumerable<TipoObjeto> Faltantes => requeridos.Where(r => !tomados.Contains(r));

        void Awake()
        {
            Instancia = this;
            Refrescar();
            if (grupoAviso != null) grupoAviso.alpha = 0f;
        }

        void OnDestroy() { if (Instancia == this) Instancia = null; }

        public bool Tiene(TipoObjeto t) => tomados.Contains(t);

        public void Agregar(TipoObjeto t)
        {
            tomados.Add(t);
            Refrescar();
            if (Completo)
                Avisar("¡Equipo completo! Ahora ve al computador para elegir tu sendero.", 6f);
            else
                Avisar(TextosObjetos.Nombre(t) + " guardado en la mochila.", 2.5f);
        }

        public void Avisar(string texto, float segundos)
        {
            if (textoAviso == null) return;
            textoAviso.text = texto;
            avisoHasta = Time.unscaledTime + segundos;
        }

        void Update()
        {
            if (grupoAviso == null) return;
            float objetivo = Time.unscaledTime < avisoHasta ? 1f : 0f;
            grupoAviso.alpha = Mathf.MoveTowards(grupoAviso.alpha, objetivo, Time.unscaledDeltaTime * 3f);
        }

        void Refrescar()
        {
            if (textoLista == null) return;
            var sb = new System.Text.StringBuilder("<b>Mochila  " + requeridos.Where(tomados.Contains).Count() + "/" + requeridos.Count + "</b>\n");
            foreach (var r in requeridos)
            {
                bool ok = tomados.Contains(r);
                // (se usan caracteres simples para que existan en la fuente Nunito)
                sb.Append(ok ? "<color=#F17E07>• " : "<color=#FFFFFF99>• ");
                sb.Append(TextosObjetos.Nombre(r));
                sb.Append(ok ? "  (listo)</color>" : "</color>");
                sb.Append('\n');
            }
            textoLista.text = sb.ToString();
        }
    }
}
