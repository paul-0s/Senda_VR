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

        /// <summary>Se avisa cada vez que cambia la pantalla visible (lo usa, por ejemplo, el fondo 360).</summary>
        public event System.Action<PantallaBase> AlCambiarPantalla;
        public PantallaBase Actual => actual;

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

        /// <summary>
        /// Opcional: algo que debe pasar entre ocultar la pantalla actual y mostrar la siguiente
        /// (por ejemplo, el video 360 de entrada al inicio de sesión).
        /// </summary>
        public System.Func<PantallaBase, System.Collections.IEnumerator> antesDeMostrar;

        bool enTransicion;

        public void IrA(PantallaBase destino)
        {
            if (destino == null || destino == actual || enTransicion) return;
            if (antesDeMostrar != null && actual != null) StartCoroutine(IrConTransicion(destino));
            else Cambiar(destino);
        }

        System.Collections.IEnumerator IrConTransicion(PantallaBase destino)
        {
            enTransicion = true;
            actual.Ocultar();
            yield return antesDeMostrar(destino);
            enTransicion = false;
            actual = null;
            Cambiar(destino);
        }

        void Cambiar(PantallaBase destino)
        {
            if (actual != null) actual.Ocultar();
            actual = destino;
            destino.navegador = this;
            destino.transform.SetAsLastSibling();
            destino.Mostrar();
            AlCambiarPantalla?.Invoke(destino);
        }

        /// <summary>Oculta todo el menú y devuelve el control al Jugador.</summary>
        public void CerrarMenu()
        {
            gameObject.SetActive(false); // OnDisable deja MenuAbierto en false
        }
    }
}
