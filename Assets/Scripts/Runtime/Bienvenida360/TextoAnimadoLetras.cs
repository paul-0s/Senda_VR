using TMPro;
using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Anima un texto como en el video de After Effects: las letras aparecen una por una
    /// de izquierda a derecha, cada una subiendo un poco mientras se hace visible,
    /// y al final todo el texto se desvanece.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class TextoAnimadoLetras : MonoBehaviour
    {
        [Header("Tiempos (segundos del video)")]
        public float inicio = 4.12f;
        [Tooltip("Cuánto tarda en revelarse el texto completo, de la primera a la última letra.")]
        public float duracionRevelado = 0.3f;
        [Tooltip("Cuánto tarda cada letra en aparecer.")]
        public float duracionLetra = 0.18f;
        public float inicioSalida = 5.3f;
        public float duracionSalida = 0.25f;

        [Header("Movimiento")]
        [Tooltip("Cuánto sube cada letra al aparecer (en píxeles del diseño 1920x1080).")]
        public float subida = 30f;

        TMP_Text tmp;

        void Awake() => tmp = GetComponent<TMP_Text>();

        public float Fin => inicioSalida + duracionSalida;

        public void Aplicar(float t)
        {
            if (tmp == null) tmp = GetComponent<TMP_Text>();
            bool visible = t >= inicio - 0.05f && t <= Fin + 0.05f;
            if (tmp.enabled != visible) tmp.enabled = visible;
            if (!visible) return;

            tmp.ForceMeshUpdate();
            var info = tmp.textInfo;
            int total = Mathf.Max(1, info.characterCount - 1);
            float paso = duracionRevelado / total;
            float salida = 1f - Mathf.Clamp01((t - inicioSalida) / Mathf.Max(0.01f, duracionSalida));

            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                float p = Mathf.Clamp01((t - inicio - i * paso) / Mathf.Max(0.01f, duracionLetra));
                float e = SendaEstilo.EaseOutCubic(p);
                byte alfa = (byte)Mathf.RoundToInt(255f * p * salida);
                var desplazamiento = new Vector3(0f, -(1f - e) * subida, 0f);

                var malla = info.meshInfo[c.materialReferenceIndex];
                for (int k = 0; k < 4; k++)
                {
                    malla.vertices[c.vertexIndex + k] += desplazamiento;
                    malla.colors32[c.vertexIndex + k].a = alfa;
                }
            }
            tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
