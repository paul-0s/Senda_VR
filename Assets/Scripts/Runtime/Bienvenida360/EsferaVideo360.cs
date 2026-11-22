using UnityEngine;

namespace SendaVR
{
    /// <summary>
    /// Esfera gigante vista desde adentro donde se proyecta un video 360 (equirectangular).
    /// El centro del video queda al frente (+Z). La esfera sigue a la cámara para que
    /// el fondo se sienta "infinito", como un cielo.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class EsferaVideo360 : MonoBehaviour
    {
        public float radio = 50f;
        public int segmentosLatitud = 64;
        public int segmentosLongitud = 128;
        [Tooltip("Gira el video alrededor del jugador (grados), por si el frente no queda donde quieres.")]
        public float rotacion = 0f;
        public Transform seguir;
        [Tooltip("Brillo del fondo 360 (1 = video original). Bájalo si se ve muy claro o \"quemado\".")]
        [Range(0.2f, 1.2f)] public float brillo = 0.65f;

        void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = CrearMalla();
            transform.rotation = Quaternion.Euler(0f, rotacion, 0f);
            AsegurarShader();
            AplicarBrillo();
        }

        /// <summary>Cambia el brillo desde otro script (por ejemplo, al cambiar de video).</summary>
        public void FijarBrillo(float nuevo)
        {
            brillo = nuevo;
            AplicarBrillo();
        }

        /// <summary>Tiñe el material para bajar el brillo del video (se puede cambiar en Play desde el Inspector).</summary>
        void AplicarBrillo()
        {
            var r = GetComponent<MeshRenderer>();
            r.material.color = new Color(brillo, brillo, brillo, 1f);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying && GetComponent<MeshRenderer>() != null) AplicarBrillo();
        }
#endif

        /// <summary>
        /// Si el material usa un shader que este proyecto no puede dibujar (se vería rosado),
        /// lo cambia por "UI/Default", que funciona con el render clásico y con URP.
        /// </summary>
        void AsegurarShader()
        {
            var r = GetComponent<MeshRenderer>();
            var m = r.sharedMaterial;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            bool esURP = pipeline != null && pipeline.GetType().Name.Contains("Universal");
            bool malo = m == null || m.shader == null || !m.shader.isSupported ||
                        (!esURP && m.shader.name.StartsWith("Universal Render Pipeline")); // p. ej. en HDRP
            if (!malo) return;

            var ui = Shader.Find("UI/Default");
            if (ui == null) return;
            var nuevo = new Material(ui) { name = "Bienvenida360_Mat (UI)", renderQueue = 1000 };
            // la textura es la del reproductor de video de este mismo objeto
            var vp = GetComponent<UnityEngine.Video.VideoPlayer>();
            if (vp != null && vp.targetTexture != null) nuevo.mainTexture = vp.targetTexture;
            else if (m != null) nuevo.mainTexture = m.mainTexture;
            r.material = nuevo;
            Debug.Log("[Senda.vr] La esfera 360 usaba un shader no compatible ('" + (m != null ? m.shader.name : "ninguno") +
                      "'); se cambió a UI/Default.");
        }

        void LateUpdate()
        {
            if (seguir != null) transform.position = seguir.position;
        }

        Mesh CrearMalla()
        {
            int nLat = segmentosLatitud, nLon = segmentosLongitud;
            var vertices = new Vector3[(nLat + 1) * (nLon + 1)];
            var uvs = new Vector2[vertices.Length];
            for (int i = 0; i <= nLat; i++)
            {
                float v = (float)i / nLat;                       // 0 = abajo, 1 = arriba
                float lat = (v - 0.5f) * Mathf.PI;
                for (int j = 0; j <= nLon; j++)
                {
                    float u = (float)j / nLon;                   // 0.5 = frente (+Z)
                    float lon = (u - 0.5f) * 2f * Mathf.PI;
                    int k = i * (nLon + 1) + j;
                    vertices[k] = new Vector3(Mathf.Sin(lon) * Mathf.Cos(lat), Mathf.Sin(lat), Mathf.Cos(lon) * Mathf.Cos(lat)) * radio;
                    uvs[k] = new Vector2(u, v);
                }
            }
            var tris = new int[nLat * nLon * 6];
            int t = 0;
            for (int i = 0; i < nLat; i++)
                for (int j = 0; j < nLon; j++)
                {
                    int a = i * (nLon + 1) + j, b = a + nLon + 1;
                    // sentido horario visto desde ADENTRO (Unity dibuja las caras en sentido horario)
                    tris[t++] = a; tris[t++] = b; tris[t++] = a + 1;
                    tris[t++] = a + 1; tris[t++] = b; tris[t++] = b + 1;
                }
            var m = new Mesh { name = "Esfera360" };
            m.vertices = vertices;
            m.uv = uvs;
            var blancos = new Color32[vertices.Length];
            for (int i = 0; i < blancos.Length; i++) blancos[i] = new Color32(255, 255, 255, 255);
            m.colors32 = blancos; // el shader UI/Default multiplica por el color de cada vértice
            m.triangles = tris;
            m.RecalculateNormals();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * radio * 2f);
            return m;
        }
    }
}
