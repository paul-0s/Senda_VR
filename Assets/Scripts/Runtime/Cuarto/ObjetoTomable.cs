using UnityEngine;

namespace SendaVR
{
    public enum TipoObjeto { Mochila, Botella, Zapatillas, PaloTrekking, Snacks }

    /// <summary>
    /// Objeto del cuarto que se puede tomar. Al hacer clic se abre una ventana
    /// con el objeto girando y su descripción; al guardarlo, desaparece del
    /// cuarto y se marca en la lista de la mochila.
    /// </summary>
    [DisallowMultipleComponent]
    public class ObjetoTomable : MonoBehaviour
    {
        public TipoObjeto tipo;
        public string nombreVisible;
        [TextArea(3, 8)] public string descripcion;

        public bool Tomado { get; private set; }

        /// <summary>Al agregar el script (o con clic derecho > Reset) se llenan los textos según el tipo.</summary>
        void Reset() => AplicarTextos();

        public void AplicarTextos()
        {
            nombreVisible = TextosObjetos.Nombre(tipo);
            descripcion = TextosObjetos.Descripcion(tipo);
        }

        public void Tomar()
        {
            if (Tomado) return;
            Tomado = true;
            if (Inventario.Instancia != null) Inventario.Instancia.Agregar(tipo);
            gameObject.SetActive(false);
        }
    }

    /// <summary>Textos educativos de cada objeto. Puedes cambiarlos en el Inspector de cada objeto.</summary>
    public static class TextosObjetos
    {
        public static string Nombre(TipoObjeto t)
        {
            switch (t)
            {
                case TipoObjeto.Mochila: return "Mochila";
                case TipoObjeto.Botella: return "Botella de agua";
                case TipoObjeto.Zapatillas: return "Zapatillas de trekking";
                case TipoObjeto.PaloTrekking: return "Palo de trekking";
                default: return "Snacks";
            }
        }

        public static string Descripcion(TipoObjeto t)
        {
            switch (t)
            {
                case TipoObjeto.Mochila:
                    return "Aquí llevarás todo lo necesario para el sendero: agua, comida, abrigo y botiquín. " +
                           "Ajusta bien las correas de hombros y cintura para que el peso descanse en tus caderas y no en tu espalda.";
                case TipoObjeto.Botella:
                    return "En la precordillera el aire es seco y el sol es intenso, incluso en invierno. " +
                           "Lleva al menos 2 litros de agua y toma pequeños sorbos cada 15 a 20 minutos, aunque no sientas sed.";
                case TipoObjeto.Zapatillas:
                    return "Tienen una suela con buen agarre para no resbalar en tierra suelta o piedras, y protegen tus tobillos. " +
                           "Úsalas algunos días antes de la salida para ablandarlas y evitar ampollas.";
                case TipoObjeto.PaloTrekking:
                    return "Este palo te ayudará al momento de escalar y te facilitará subir pendientes inclinadas. " +
                           "Además, reduce el impacto en tus rodillas al bajar y te da equilibrio al cruzar esteros.";
                default:
                    return "Frutos secos, barras o fruta te dan energía rápida durante la caminata. " +
                           "Come algo pequeño en cada descanso y guarda tus envoltorios: en el cerro no se deja basura.";
            }
        }
    }
}
