using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Controla qué pantalla se ve. Al dar Play muestra "pantallaInicial"
    /// y oculta las demás. Mientras el menú está abierto, "MenuAbierto" es true
    /// y el Jugador no captura el mouse ni se mueve.
    /// </summary>
    public class NavegadorPantallas : MonoBehaviour
    {
        public PantallaBase pantallaInicial;
        public PantallaBase[] pantallas;

        /// <summary>True mientras el menú de Senda.vr está visible.</summary>
        public static bool MenuAbierto { get; private set; }

        PantallaBase actual;

        void OnEnable() => MenuAbierto = true;
        void OnDisable() => MenuAbierto = false;

        void Start()
        {
            foreach (var p in pantallas)
            {
                if (p == null) continue;
                p.navegador = this;
                p.OcultarAlInstante();
            }
            if (pantallaInicial != null) IrA(pantallaInicial);
        }

        public void IrA(PantallaBase destino)
        {
            if (destino == null || destino == actual) return;
            if (actual != null) actual.Ocultar();
            actual = destino;
            destino.navegador = this;
            destino.transform.SetAsLastSibling();
            destino.Mostrar();
        }

        /// <summary>Oculta todo el menú y devuelve el control al Jugador.</summary>
        public void CerrarMenu()
        {
            gameObject.SetActive(false); // OnDisable deja MenuAbierto en false
        }
    }
}
