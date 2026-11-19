using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Paleta y medidas de Senda.vr tomadas del archivo de Figma.
    /// Si cambias un color aquí, vuelve a ejecutar "Senda.vr > Construir menú".
    /// </summary>
    public static class SendaEstilo
    {
        // Botones
        public static readonly Color Naranja         = Hex("F17E07"); // Iniciar sesión / Crear cuenta
        public static readonly Color NaranjaIntenso  = Hex("E65E09"); // hover de Salir / Aceptar
        public static readonly Color Azul            = Hex("4D76DD"); // Crear cuenta / Cancelar
        public static readonly Color Gris            = Hex("838181"); // botón deshabilitado / Salir / puntos inactivos
        public static readonly Color GrisClaro       = Hex("D9D9D9"); // punto activo / casilla marcada
        public static readonly Color NaranjaLogo     = Hex("FF8345");

        // Interruptor "Recuérdame"
        public static readonly Color CremaInterruptor = Hex("EEDCCF");
        public static readonly Color NaranjaPerilla   = Hex("FF6803");

        // Paneles y texto
        public static readonly Color Panel           = new Color(32f / 255f, 32f / 255f, 32f / 255f, 0.92f); // #202020 al 92 %
        public static readonly Color Velo            = new Color(20f / 255f, 24f / 255f, 29f / 255f, 0.58f); // Rectangle 234/235
        public static readonly Color VeloInferior    = new Color(20f / 255f, 24f / 255f, 29f / 255f, 0.48f); // segunda capa en crear cuenta
        public static readonly Color Oscurecer       = Velo; // nombre antiguo, se mantiene por compatibilidad
        public static readonly Color Blanco          = Color.white;
        public static readonly Color TextoSuave      = new Color(1f, 1f, 1f, 0.59f);
        public static readonly Color TextoEtiqueta   = new Color(204f / 255f, 204f / 255f, 204f / 255f, 0.77f); // "Recuérdame:"
        public static readonly Color BordeCampo      = Color.white;
        public static readonly Color TextoOscuro     = Hex("2B2B2B");
        public static readonly Color Error           = Hex("FF8A65");

        // Tamaños de texto (px de Figma)
        public const float TamTitulo = 48f;
        public const float TamSubtitulo = 24f;
        public const float TamCampo = 24f;
        public const float TamBoton = 32f;

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var c);
            return c;
        }

        // Curvas de animación
        public static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float EaseInOutCubic(float t) =>
            t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
