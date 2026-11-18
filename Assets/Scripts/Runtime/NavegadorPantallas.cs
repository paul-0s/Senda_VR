using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Controla qué pantalla se ve. Al dar Play muestra "pantallaInicial"
    /// y oculta las demás.
    /// </summary>
    public class NavegadorPantallas : MonoBehaviour
    {
        public PantallaBase pantallaInicial;
        public PantallaBase[] pantallas;

        PantallaBase actual;

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
    }
}
