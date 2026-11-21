using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace SendaVR
{
    /// <summary>
    /// Bienvenida 360: el fondo animado envuelve al usuario y el logo y los textos
    /// aparecen al frente con las mismas animaciones del video de After Effects.
    /// Mientras tanto se carga el cuarto; al terminar, funde a negro y entra.
    /// Espacio (PC) o botón A (VR): saltar.
    /// </summary>
    public class Bienvenida360 : MonoBehaviour
    {
        [Header("Fondo 360")]
        public VideoPlayer video;
        public Renderer esfera;

        [Header("Logo")]
        public CanvasGroup logo;
        public RectTransform brillo;
        public float logoEntra = 0.9f, logoEntraDur = 0.3f;
        public float brilloInicio = 1.65f, brilloDur = 0.5f, brilloRecorrido = 300f;
        public float logoSale = 2.4f, logoSaleDur = 0.35f;

        [Header("Textos")]
        public TextoAnimadoLetras[] textos;

        [Header("Ubicación frente al usuario")]
        public RectTransform panelTextos;
        public Transform cabeza;
        public float distancia = 2.5f;

        [Header("Final")]
        public float duracion = 15f;
        public float fundidoFinal = 0.8f;
        [Tooltip("Escena que se carga al terminar. Debe estar en File > Build Profiles > Scene List.")]
        public string escenaSiguiente = "Cuarto";

        InputAction saltar;
        float t;
        bool terminando;

        void Awake()
        {
            saltar = new InputAction("Saltar", InputActionType.Button);
            saltar.AddBinding("<Keyboard>/space");
            saltar.AddBinding("<Gamepad>/buttonSouth");
            saltar.AddBinding("<XRController>{RightHand}/{PrimaryButton}");
            if (logo != null) logo.alpha = 0f;
            Aplicar(0f);
        }

        void OnEnable() => saltar.Enable();
        void OnDisable() => saltar.Disable();

        IEnumerator Start()
        {
            // Carga el cuarto en segundo plano
            AsyncOperation carga = null;
            if (!string.IsNullOrEmpty(escenaSiguiente) && Application.CanStreamedLevelBeLoaded(escenaSiguiente))
            {
                carga = SceneManager.LoadSceneAsync(escenaSiguiente);
                carga.allowSceneActivation = false;
            }
            else if (!string.IsNullOrEmpty(escenaSiguiente))
                Debug.LogWarning("[Senda.vr] La escena '" + escenaSiguiente + "' no está en Build Profiles > Scene List.");

            // Espera un cuadro para que el visor entregue la posición de la cabeza y ubica los textos al frente
            yield return null;
            UbicarFrente();

            if (video != null && video.clip != null)
            {
                video.isLooping = false;
                if (video.targetTexture != null) PantallaBase.LimpiarTextura(video.targetTexture, Color.black);
                video.Prepare();
                float limite = Time.unscaledTime + 6f;
                while (!video.isPrepared && Time.unscaledTime < limite) yield return null;
                video.Play();
                yield return PantallaBase.EsperarPrimerCuadro(video);
            }

            float inicioReloj = Time.unscaledTime;
            while (!terminando)
            {
                // El reloj sigue al video para que logo y textos calcen con el fondo
                t = video != null && video.isPlaying ? (float)video.time : Time.unscaledTime - inicioReloj;
                Aplicar(t);
                if (t >= duracion - 0.05f || (video != null && video.clip != null && !video.isPlaying && t > 1f)) break;
                if (t > 1f && saltar.WasPressedThisFrame()) break;
                yield return null;
            }
            terminando = true;

            // Fundido a negro
            var mat = esfera != null ? esfera.material : null;
            Color desde = mat != null ? mat.color : Color.white;
            float k = 0f;
            var grupoTextos = panelTextos != null ? panelTextos.GetComponent<CanvasGroup>() : null;
            float alfaTextos = grupoTextos != null ? grupoTextos.alpha : 1f;
            while (k < 1f)
            {
                k += Time.unscaledDeltaTime / fundidoFinal;
                if (mat != null) mat.color = Color.Lerp(desde, Color.black, k);
                if (grupoTextos != null) grupoTextos.alpha = Mathf.Lerp(alfaTextos, 0f, k);
                yield return null;
            }
            if (video != null) video.Stop();

            if (carga != null)
            {
                while (carga.progress < 0.9f) yield return null;
                carga.allowSceneActivation = true;
            }
        }

        void UbicarFrente()
        {
            if (panelTextos == null || cabeza == null) return;
            var adelante = cabeza.forward; adelante.y = 0f;
            if (adelante.sqrMagnitude < 0.001f) adelante = Vector3.forward;
            adelante.Normalize();
            panelTextos.position = cabeza.position + adelante * distancia;
            panelTextos.rotation = Quaternion.LookRotation(adelante, Vector3.up);
        }

        void Aplicar(float tiempo)
        {
            if (logo != null)
            {
                float entra = Mathf.Clamp01((tiempo - logoEntra) / logoEntraDur);
                float sale = 1f - Mathf.Clamp01((tiempo - logoSale) / logoSaleDur);
                logo.alpha = SendaEstilo.EaseOutCubic(entra) * sale;
                if (brillo != null)
                {
                    float b = Mathf.Clamp01((tiempo - brilloInicio) / brilloDur);
                    brillo.anchoredPosition = new Vector2(Mathf.Lerp(-brilloRecorrido, brilloRecorrido, SendaEstilo.EaseInOutCubic(b)), 0f);
                    brillo.gameObject.SetActive(b > 0f && b < 1f);
                }
            }
            if (textos != null)
                foreach (var tx in textos) if (tx != null) tx.Aplicar(tiempo);
        }
    }
}
