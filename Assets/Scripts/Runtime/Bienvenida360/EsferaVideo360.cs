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

        void Awake()
        {
            GetComponent<MeshFilter>().sharedMesh = CrearMalla();
            transform.rotation = Quaternion.Euler(0f, rotacion, 0f);
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
