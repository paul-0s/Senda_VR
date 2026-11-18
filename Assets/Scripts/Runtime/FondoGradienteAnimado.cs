using UnityEngine;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// Fondo degradado que pasa suavemente entre las 3 variantes del
    /// componente "Home" de Figma, con un leve "respirar" de escala.
    /// </summary>
    public class FondoGradienteAnimado : MonoBehaviour
    {
        public Image capaAbajo;
        public Image capaArriba;
        public Sprite[] variantes;
        public float duracionTransicion = 3.5f;
        public float pausaEntreVariantes = 1.0f;
        public float escalaRespirar = 0.05f;

        int indice;
        float reloj;
        bool enTransicion;

        void OnEnable()
        {
            if (variantes == null || variantes.Length == 0) return;
            indice = 0;
            capaAbajo.sprite = variantes[0];
            capaArriba.sprite = variantes[Siguiente()];
            SetAlpha(capaArriba, 0f);
            reloj = 0f;
            enTransicion = false;
        }

        int Siguiente() => (indice + 1) % variantes.Length;

        void Update()
        {
            if (variantes == null || variantes.Length < 2) return;
            float dt = Time.unscaledDeltaTime;
            reloj += dt;

            if (!enTransicion && reloj >= pausaEntreVariantes) { enTransicion = true; reloj = 0f; }
            if (enTransicion)
            {
                float k = Mathf.Clamp01(reloj / duracionTransicion);
                SetAlpha(capaArriba, SendaEstilo.EaseInOutCubic(k));
                if (k >= 1f)
                {
                    indice = Siguiente();
                    capaAbajo.sprite = variantes[indice];
                    capaArriba.sprite = variantes[Siguiente()];
                    SetAlpha(capaArriba, 0f);
                    enTransicion = false;
                    reloj = 0f;
                }
            }

            float s = 1f + escalaRespirar * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 0.35f));
            transform.localScale = new Vector3(s, s, 1f);
        }

        static void SetAlpha(Image img, float a)
        {
            var c = img.color; c.a = a; img.color = c;
        }
    }
}
