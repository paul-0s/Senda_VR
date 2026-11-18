using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú "Senda > Terreno alrededor del camino".
///
/// Toma el mesh del camino (sendaMap) y moldea un Terrain de Unity a su alrededor:
///  - debajo del camino, el terreno queda pegado justo bajo el mesh;
///  - a los lados hay una franja plana (la orilla del sendero);
///  - más allá, el terreno sube con cerros generados con ruido.
/// Opcionalmente pinta el camino con una capa de textura del terreno.
///
/// También activa Read/Write y "Generate Colliders" en el modelo del camino,
/// para que el jugador pueda caminar encima.
/// </summary>
public class TerrenoAlrededorDelCamino : EditorWindow
{
    [SerializeField] Terrain terreno;
    [SerializeField] GameObject camino;

    [SerializeField] bool ajustarTerreno = true;
    [SerializeField] float margen = 150f;
    [SerializeField] float metrosPorCelda = 1f;

    [SerializeField] float hundir = 0.15f;
    [SerializeField] float anchoBorde = 2f;
    [SerializeField] float anchoTransicion = 25f;
    [SerializeField] float alturaCerros = 25f;
    [SerializeField] float tamanoCerros = 80f;
    [SerializeField] float pendiente = 0.1f;
    [SerializeField] int semilla = 1;

    [SerializeField] bool pintarCamino = true;
    [SerializeField] int capaCamino = 1;
    [SerializeField] float anchoPintura = 0.5f;

    Vector2 scroll;

    [MenuItem("Senda/Terreno alrededor del camino")]
    static void Abrir() => GetWindow<TerrenoAlrededorDelCamino>("Terreno del camino");

    void OnEnable()
    {
        if (terreno == null) terreno = Terrain.activeTerrain;
        if (camino == null) camino = GameObject.Find("sendaMap");
    }

    // ------------------------------------------------------------------ Ventana

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.HelpBox(
            "Moldea el terreno alrededor del camino: queda justo debajo del camino y sube en cerros hacia los lados.\n" +
            "Reemplaza la forma (y la pintura, si está activada) que tenga el terreno. Se puede deshacer con Ctrl+Z.",
            MessageType.Info);

        terreno = (Terrain)EditorGUILayout.ObjectField(new GUIContent("Terreno", "El Terrain que se va a moldear."), terreno, typeof(Terrain), true);
        camino = (GameObject)EditorGUILayout.ObjectField(new GUIContent("Camino", "El objeto con el mesh del sendero (sendaMap)."), camino, typeof(GameObject), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tamaño del terreno", EditorStyles.boldLabel);
        ajustarTerreno = EditorGUILayout.Toggle(new GUIContent("Ajustar al camino",
            "Mueve y cambia el tamaño del terreno para que cubra todo el camino más el margen."), ajustarTerreno);
        using (new EditorGUI.DisabledScope(!ajustarTerreno))
        {
            margen = EditorGUILayout.FloatField(new GUIContent("Margen (m)",
                "Cuántos metros de cerro hay alrededor del camino, hacia cada lado."), margen);
            metrosPorCelda = EditorGUILayout.Slider(new GUIContent("Detalle (m por punto)",
                "Distancia entre puntos del terreno. Más chico = más detalle, pero más pesado."), metrosPorCelda, 0.25f, 2f);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Forma de los cerros", EditorStyles.boldLabel);
        hundir = EditorGUILayout.Slider(new GUIContent("Hundir bajo el camino (m)",
            "Cuánto queda el terreno por debajo del mesh del camino, para que no lo tape."), hundir, 0f, 1f);
        anchoBorde = EditorGUILayout.Slider(new GUIContent("Orilla plana (m)",
            "Franja plana a cada lado del camino antes de que empiece a subir el cerro."), anchoBorde, 0f, 10f);
        anchoTransicion = EditorGUILayout.Slider(new GUIContent("Transición (m)",
            "En cuántos metros pasa de plano a cerro. Más grande = más suave."), anchoTransicion, 1f, 100f);
        alturaCerros = EditorGUILayout.Slider(new GUIContent("Altura de cerros (m)",
            "Qué tan altos son los cerros generados con ruido."), alturaCerros, 0f, 150f);
        tamanoCerros = EditorGUILayout.Slider(new GUIContent("Tamaño de cerros (m)",
            "Ancho aproximado de cada cerro. Más grande = cerros más anchos y suaves."), tamanoCerros, 10f, 300f);
        pendiente = EditorGUILayout.Slider(new GUIContent("Pendiente",
            "Metros que sube el terreno por cada metro que te alejas del camino. Negativo = baja (como quebrada)."), pendiente, -0.5f, 0.8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            semilla = EditorGUILayout.IntField(new GUIContent("Semilla", "Cambia el número para obtener otros cerros."), semilla);
            if (GUILayout.Button("Otra", GUILayout.Width(50))) semilla = Random.Range(1, 99999);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pintar el camino", EditorStyles.boldLabel);
        pintarCamino = EditorGUILayout.Toggle(new GUIContent("Pintar",
            "Pinta el camino con una capa de textura del terreno (necesita al menos 2 capas en Paint Texture)."), pintarCamino);
        using (new EditorGUI.DisabledScope(!pintarCamino))
        {
            capaCamino = EditorGUILayout.IntSlider(new GUIContent("Capa del camino",
                "Número de la capa (Terrain Layer) para el camino. La 0 es la primera."), capaCamino, 0, 7);
            anchoPintura = EditorGUILayout.Slider(new GUIContent("Ancho extra (m)",
                "Cuánto se extiende la textura del camino hacia los lados."), anchoPintura, 0f, 5f);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Generar terreno", GUILayout.Height(32))) Generar();

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ Generación

    void Generar()
    {
        if (terreno == null || camino == null)
        {
            ShowNotification(new GUIContent("Elige el terreno y el camino"));
            return;
        }

        var filtros = camino.GetComponentsInChildren<MeshFilter>();
        if (filtros.Length == 0)
        {
            ShowNotification(new GUIContent("El camino no tiene ningún mesh"));
            return;
        }

        try
        {
            EditorUtility.DisplayProgressBar("Terreno del camino", "Preparando el modelo...", 0f);
            PrepararModelo(filtros);

            // 1. Triángulos del camino en coordenadas de mundo
            var tris = new List<Vector3>();
            Bounds limites = default;
            bool primero = true;
            foreach (var mf in filtros)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Vector3[] v = mesh.vertices;
                int[] t = mesh.triangles;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                var w = new Vector3[v.Length];
                for (int i = 0; i < v.Length; i++)
                {
                    w[i] = m.MultiplyPoint3x4(v[i]);
                    if (primero) { limites = new Bounds(w[i], Vector3.zero); primero = false; }
                    else limites.Encapsulate(w[i]);
                }
                foreach (int k in t) tris.Add(w[k]);
            }
            if (tris.Count == 0)
            {
                ShowNotification(new GUIContent("No pude leer el mesh del camino"));
                return;
            }

            TerrainData data = terreno.terrainData;
            var deshacer = new List<Object> { data, terreno.transform };
            if (data.alphamapTextures != null) deshacer.AddRange(data.alphamapTextures);
            Undo.RegisterCompleteObjectUndo(deshacer.ToArray(), "Terreno alrededor del camino");

            // 2. Ajustar tamaño y posición del terreno
            if (ajustarTerreno)
            {
                float sx = limites.size.x + 2f * margen;
                float sz = limites.size.z + 2f * margen;
                float ladoMax = Mathf.Max(sx, sz);
                int resolucion = 513;
                while (resolucion < 2049 && ladoMax / (resolucion - 1) > metrosPorCelda)
                    resolucion = (resolucion - 1) * 2 + 1;

                float alejamiento = margen * 1.42f; // distancia máxima aprox. (diagonal)
                float minY = limites.min.y - hundir - 5f + Mathf.Min(0f, pendiente) * alejamiento;
                float maxY = limites.max.y + alturaCerros + Mathf.Max(0f, pendiente) * alejamiento + 10f;

                data.heightmapResolution = resolucion;
                data.size = new Vector3(sx, maxY - minY, sz);
                terreno.transform.position = new Vector3(limites.min.x - margen, minY, limites.min.z - margen);
            }

            int res = data.heightmapResolution;
            Vector3 pos = terreno.transform.position;
            Vector3 tam = data.size;
            float cx = tam.x / (res - 1);
            float cz = tam.z / (res - 1);
            int n = res * res;

            // 3. Dibujar el camino sobre la grilla del terreno (altura del camino en cada punto)
            EditorUtility.DisplayProgressBar("Terreno del camino", "Leyendo el camino...", 0.2f);
            var caminoY = new float[n];
            for (int i = 0; i < n; i++) caminoY[i] = float.NegativeInfinity;

            void Marcar(int x, int z, float y)
            {
                if (x < 0 || z < 0 || x >= res || z >= res) return;
                int i = z * res + x;
                if (y > caminoY[i]) caminoY[i] = y;
            }

            for (int k = 0; k < tris.Count; k += 3)
            {
                Vector3 p0 = tris[k], p1 = tris[k + 1], p2 = tris[k + 2];
                var a = new Vector2((p0.x - pos.x) / cx, (p0.z - pos.z) / cz);
                var b = new Vector2((p1.x - pos.x) / cx, (p1.z - pos.z) / cz);
                var c = new Vector2((p2.x - pos.x) / cx, (p2.z - pos.z) / cz);

                // Los vértices siempre cuentan (por si el camino es más angosto que la grilla)
                Marcar(Mathf.RoundToInt(a.x), Mathf.RoundToInt(a.y), p0.y);
                Marcar(Mathf.RoundToInt(b.x), Mathf.RoundToInt(b.y), p1.y);
                Marcar(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y), p2.y);

                float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
                if (Mathf.Abs(area) < 1e-6f) continue; // triángulo vertical o degenerado

                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x))));
                int x1 = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x))));
                int z0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y))));
                int z1 = Mathf.Min(res - 1, Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y))));

                for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    float w0 = ((b.x - x) * (c.y - z) - (c.x - x) * (b.y - z)) / area;
                    float w1 = ((c.x - x) * (a.y - z) - (a.x - x) * (c.y - z)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < -1e-4f || w1 < -1e-4f || w2 < -1e-4f) continue;
                    Marcar(x, z, w0 * p0.y + w1 * p1.y + w2 * p2.y);
                }
            }

            // 4. Para cada punto, buscar el punto del camino más cercano (búsqueda a lo ancho)
            EditorUtility.DisplayProgressBar("Terreno del camino", "Midiendo distancias al camino...", 0.4f);
            var fuente = new int[n];
            var cola = new int[n];
            int cabeza = 0, cuenta = 0;
            for (int i = 0; i < n; i++)
            {
                if (caminoY[i] > float.NegativeInfinity) { fuente[i] = i; cola[cuenta++] = i; }
                else fuente[i] = -1;
            }
            if (cuenta == 0)
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Terreno del camino",
                    "El camino queda fuera del terreno. Activa \"Ajustar al camino\" o mueve el terreno debajo del camino.", "OK");
                return;
            }
            while (cabeza < cuenta)
            {
                int i = cola[cabeza++];
                int x = i % res, z = i / res;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= res || nz >= res) continue;
                    int j = nz * res + nx;
                    if (fuente[j] != -1) continue;
                    fuente[j] = fuente[i];
                    cola[cuenta++] = j;
                }
            }

            // 5. Calcular alturas
            EditorUtility.DisplayProgressBar("Terreno del camino", "Levantando cerros...", 0.6f);
            var alturas = new float[res, res];
            var distancia = new float[n];
            var rng = new System.Random(semilla);
            float ox = (float)rng.NextDouble() * 10000f;
            float oz = (float)rng.NextDouble() * 10000f;
            float frecuencia = 1f / Mathf.Max(1f, tamanoCerros);

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int i = z * res + x;
                float h;
                if (caminoY[i] > float.NegativeInfinity)
                {
                    h = caminoY[i] - hundir;
                    distancia[i] = 0f;
                }
                else
                {
                    int s = fuente[i];
                    int sx = s % res, sz = s / res;
                    float ddx = (x - sx) * cx, ddz = (z - sz) * cz;
                    float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                    distancia[i] = d;

                    float fuera = Mathf.Max(0f, d - anchoBorde);
                    float t = Mathf.SmoothStep(0f, 1f, fuera / Mathf.Max(0.01f, anchoTransicion));
                    float ruido = Fbm((pos.x + x * cx) * frecuencia + ox, (pos.z + z * cz) * frecuencia + oz);
                    h = caminoY[s] - hundir + t * (ruido * alturaCerros + fuera * pendiente);
                }
                alturas[z, x] = Mathf.Clamp01((h - pos.y) / tam.y);
            }
            data.SetHeights(0, 0, alturas);

            // 6. Pintar el camino
            if (pintarCamino)
            {
                EditorUtility.DisplayProgressBar("Terreno del camino", "Pintando el camino...", 0.85f);
                PintarCamino(data, res, distancia);
            }

            terreno.Flush();
            EditorUtility.SetDirty(data);
            EditorUtility.SetDirty(terreno);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(terreno.gameObject.scene);
            ShowNotification(new GUIContent("¡Terreno listo!"));
            Debug.Log($"Terreno generado: {tam.x:0} x {tam.z:0} m, resolución {res}.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    void PintarCamino(TerrainData data, int res, float[] distancia)
    {
        TerrainLayer[] capas = data.terrainLayers;
        if (capas == null || capas.Length < 2 || capaCamino >= capas.Length)
        {
            Debug.LogWarning("Terreno del camino: para pintar el camino, agrega al menos 2 capas en " +
                             "Terrain > Paint Texture (una para el cerro y otra para el camino) y elige su número en \"Capa del camino\".");
            return;
        }

        int ar = data.alphamapResolution;
        int nCapas = capas.Length;
        int capaBase = capaCamino == 0 ? 1 : 0;
        var alfa = new float[ar, ar, nCapas];

        for (int az = 0; az < ar; az++)
        for (int ax = 0; ax < ar; ax++)
        {
            int hx = Mathf.RoundToInt(ax / (float)(ar - 1) * (res - 1));
            int hz = Mathf.RoundToInt(az / (float)(ar - 1) * (res - 1));
            float d = distancia[hz * res + hx];
            float w = 1f - Mathf.SmoothStep(0f, 1f, (d - anchoPintura) / 1.5f);
            alfa[az, ax, capaCamino] = w;
            alfa[az, ax, capaBase] += 1f - w;
        }
        data.SetAlphamaps(0, 0, alfa);
    }

    // Activa Read/Write (para leer el mesh) y Generate Colliders (para caminar encima).
    static void PrepararModelo(MeshFilter[] filtros)
    {
        var revisados = new HashSet<string>();
        foreach (var mf in filtros)
        {
            if (mf.sharedMesh == null) continue;
            string ruta = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (string.IsNullOrEmpty(ruta) || !revisados.Add(ruta)) continue;
            if (AssetImporter.GetAtPath(ruta) is ModelImporter imp && (!imp.isReadable || !imp.addCollider))
            {
                imp.isReadable = true;
                imp.addCollider = true;
                imp.SaveAndReimport();
            }
        }
    }

    // Ruido con varias capas de detalle (cerros grandes + lomitas chicas). Devuelve 0..1.
    static float Fbm(float x, float z)
    {
        float suma = 0f, amp = 0.5f, f = 1f, total = 0f;
        for (int o = 0; o < 4; o++)
        {
            suma += amp * Mathf.PerlinNoise(x * f, z * f);
            total += amp;
            amp *= 0.5f;
            f *= 2f;
        }
        return suma / total;
    }
}
