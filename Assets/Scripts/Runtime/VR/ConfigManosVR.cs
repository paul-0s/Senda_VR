using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Ajustes de las manos VR. El archivo vive en Assets/Resources/ConfigManosVR.asset
    /// y lo crea solo el editor (menú Senda.vr/VR/Configurar manos VR).
    /// Puedes cambiar los valores mientras juegas en VR: como es un asset, los cambios se quedan guardados.
    /// </summary>
    [CreateAssetMenu(menuName = "Senda/Config Manos VR", fileName = "ConfigManosVR")]
    public class ConfigManosVR : ScriptableObject
    {
        [Tooltip("Apágalo para volver a ver los controles en vez de las manos.")]
        public bool usarManos = true;

        [Header("Modelos (del sample HandVisualizer de XR Hands)")]
        public GameObject modeloIzquierdo;
        public GameObject modeloDerecho;
        [Tooltip("Material de la piel. Si está vacío se crea uno HDRP/Lit en el momento.")]
        public Material material;
        [Tooltip("Esconde el modelo del control del Quest.")]
        public bool ocultarControl = true;

        [Header("Posición de la mano en el control (se ajusta para la DERECHA; la izquierda se refleja sola)")]
        [Tooltip("Distancia entre el centro del mango y la palma, en metros.")]
        public float separacionPalma = 0.035f;
        [Tooltip("Mover la mano (metros, en ejes del control: X derecha, Y arriba, Z adelante).")]
        public Vector3 ajustePosicion = Vector3.zero;
        [Tooltip("Girar la mano (grados).")]
        public Vector3 ajusteRotacion = Vector3.zero;
        public float escala = 1f;

        [Header("Dedos")]
        [Range(0f, 1f), Tooltip("Qué tan doblados quedan los dedos sin apretar nada.")]
        public float dedosRelajados = 0.12f;
        [Range(0f, 1f), Tooltip("Cuánto se dobla el índice cuando solo tocas el gatillo.")]
        public float indiceTocandoGatillo = 0.3f;
        [Tooltip("Qué tan rápido se mueven los dedos.")]
        public float velocidadDedos = 18f;
        [Range(0f, 1f), Tooltip("Cierre máximo cuando sostienes un objeto (para que no lo atraviese).")]
        public float cierreAlSostener = 0.7f;

        [Header("Escenas 360 (sin luces propias)")]
        [Tooltip("En escenas sin luces (Logo360, Inicio360, Bienvenida360) la mano se ilumina con luces suaves propias, para que no se queme de blanco.")]
        public bool iluminarEnEscenasSinLuz = true;
        [Tooltip("Luz principal (lux). Súbela si la mano se ve oscura, bájala si se ve muy blanca.")]
        public float luzPrincipal = 2f;
        [Tooltip("Luz de relleno desde abajo (lux), para que el lado oscuro no quede negro.")]
        public float luzRelleno = 0.7f;
    }
}
