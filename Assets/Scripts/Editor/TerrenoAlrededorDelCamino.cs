using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menú "Senda > Terreno alrededor del camino".
///
/// Toma el mesh del camino (sendaMap) y moldea un Terrain de Unity a su alrededor:
///  - debajo del camino, el terreno queda pegado justo bajo el mesh;
///  - a los lados hay una franja plana (la orilla del sendero);
///  - entre tramos del camino a distinta altura, el terreno forma una ladera suave
///    (se rellena como una tela estirada entre los tramos, sin muros);
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
    [SerializeField] float anchoLadera = 40f;
    [SerializeField] float anchoTransicion = 50f;
    [SerializeField] float alturaCerros = 15f;
    [SerializeField] float tamanoCerros = 150f;
    [SerializeField] float pendiente = 0.05f;
    [SerializeField] float pulido = 3f;
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
        anchoLadera = EditorGUILayout.Slider(new GUIContent("Ancho de ladera (m)",
            "Qué tan lejos del camino se mantiene su altura antes de empezar a bajar o subir. Más grande = laderas más largas y suaves (y sin pedestal al final del camino)."), anchoLadera, 0f, 150f);
        anchoTransicion = EditorGUILayout.Slider(new GUIContent("Transición (m)",
            "En cuántos metros pasa de plano a cerro. Más grande = más suave."), anchoTransicion, 1f, 200f);
        alturaCerros = EditorGUILayout.Slider(new GUIContent("Altura de cerros (m)",
            "Qué tan altos son los cerros generados con ruido."), alturaCerros, 0f, 150f);
        tamanoCerros = EditorGUILayout.Slider(new GUIContent("Tamaño de cerros (m)",
            "Ancho aproximado de cada cerro. Más grande = cerros más anchos y suaves."), tamanoCerros, 10f, 300f);
        pendiente = EditorGUILayout.Slider(new GUIContent("Pendiente",
            "Metros que sube el terreno por cada metro que te alejas del camino. Negativo = baja (como quebrada)."), pendiente, -0.5f, 0.8f);
        pulido = EditorGUILayout.Slider(new GUIContent("Pulido final (m)",
            "Suaviza todo el terreno al final (sin tocar el camino) para quitar bordes duros. 0 = sin pulir."), pulido, 0f, 15f);
        using (new EditorGUILayout.HorizontalScope())
        {
            semilla = EditorGUILayout.IntField(new GUIContent("Semilla", "Cambia el número para obtener otros cerros."), semilla);
            if (GUILayout.Button("Otra", GUILayout.Width(50))) semilla = Random.Range(1, 99999);
        }
        if (GUILayout.Button("Usar valores de cerro suave"))
        {
            anchoBorde = 2f; anchoLadera = 40f; anchoTransicion = 50f; alturaCerros = 15f;
            tamanoCerros = 150f; pendiente = 0.05f; pulido = 3f;
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

            // 5. Altura base, en dos capas:
            //    a) una "guía": la altura del camino más cercano, desenfocada (sin saltos);
            //    b) una "tela" estirada (ecuación de Laplace) que corrige la diferencia entre
            //       la guía y el camino real, dejando el camino exacto y sin muros entre tramos.
            //    La guía evita que el terreno caiga de golpe donde el camino queda aislado
            //    (por ejemplo, en la punta final), que formaba un pedestal.
            EditorUtility.DisplayProgressBar("Terreno del camino", "Calculando la altura general...", 0.45f);
            var fijo = new bool[n];
            var guia = new float[n];
            for (int i = 0; i < n; i++)
            {
                fijo[i] = caminoY[i] > float.NegativeInfinity;
                guia[i] = caminoY[fuente[i]] - hundir;
            }
            if (anchoLadera > 0f)
                Desenfocar(guia, res, Mathf.Clamp(Mathf.RoundToInt(anchoLadera / cx), 1, res / 2), 3);

            EditorUtility.DisplayProgressBar("Terreno del camino", "Estirando la ladera entre los tramos...", 0.5f);
            var residuo = new float[n];
            for (int i = 0; i < n; i++)
                residuo[i] = fijo[i] ? (caminoY[i] - hundir) - guia[i] : 0f;
            residuo = RellenarSuave(residuo, fijo, res);

            var baseY = new float[n];
            for (int i = 0; i < n; i++)
                baseY[i] = fijo[i] ? caminoY[i] - hundir : guia[i] + residuo[i];

            // 6. Sumar los cerros
            EditorUtility.DisplayProgressBar("Terreno del camino", "Levantando cerros...", 0.7f);
            var alturaMundo = new float[n];
            var distancia = new float[n];
            var rng = new System.Random(semilla);
            float ox = (float)rng.NextDouble() * 10000f;
            float oz = (float)rng.NextDouble() * 10000f;
            float frecuencia = 1f / Mathf.Max(1f, tamanoCerros);

            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int i = z * res + x;
                if (fijo[i])
                {
                    alturaMundo[i] = baseY[i];
                    distancia[i] = 0f;
                    continue;
                }
                int s = fuente[i];
                int sx = s % res, sz = s / res;
                float ddx = (x - sx) * cx, ddz = (z - sz) * cz;
                float d = Mathf.Sqrt(ddx * ddx + ddz * ddz);
                distancia[i] = d;

                float fuera = Mathf.Max(0f, d - anchoBorde);
                float t = Mathf.SmoothStep(0f, 1f, fuera / Mathf.Max(0.01f, anchoTransicion));
                float ruido = Fbm((pos.x + x * cx) * frecuencia + ox, (pos.z + z * cz) * frecuencia + oz);
                alturaMundo[i] = baseY[i] + t * (ruido * alturaCerros + fuera * pendiente);
            }

            // 7. Pulido final: suavizar todo menos el camino y su orilla
            if (pulido > 0f)
            {
                EditorUtility.DisplayProgressBar("Terreno del camino", "Puliendo...", 0.8f);
                var pulida = (float[])alturaMundo.Clone();
                Desenfocar(pulida, res, Mathf.Max(1, Mathf.RoundToInt(pulido / cx)), 3);
                for (int i = 0; i < n; i++)
                {
                    float w = Mathf.SmoothStep(0f, 1f, (distancia[i] - anchoBorde) / 5f);
                    alturaMundo[i] = Mathf.Lerp(alturaMundo[i], pulida[i], w);
                }
            }

            var alturas = new float[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                alturas[z, x] = Mathf.Clamp01((alturaMundo[z * res + x] - pos.y) / tam.y);
            data.SetHeights(0, 0, alturas);

            // 8. Pintar el camino
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

    // Ruido para los cerros: sobre todo formas grandes y redondas, con poco detalle chico. Devuelve 0..1.
    static float Fbm(float x, float z)
    {
        float suma = 0f, amp = 0.6f, f = 1f, total = 0f;
        for (int o = 0; o < 3; o++)
        {
            suma += amp * Mathf.PerlinNoise(x * f, z * f);
            total += amp;
            amp *= 0.35f;
            f *= 2f;
        }
        return suma / total;
    }

    // ------------------------------------------------------------------ Relleno suave (Laplace)

    // Rellena los puntos no fijos con la superficie más suave posible que pasa por los fijos,
    // como una tela estirada. Trabaja de grueso a fino para que sea rápido.
    static float[] RellenarSuave(float[] valores, bool[] fijo, int res)
    {
        // Pirámide de resoluciones: res, (res-1)/2+1, ... hasta ~33
        var resoluciones = new List<int> { res };
        var niveles = new List<float[]> { valores };
        var fijos = new List<bool[]> { fijo };
        while (resoluciones[resoluciones.Count - 1] > 33 && (resoluciones[resoluciones.Count - 1] - 1) % 2 == 0)
        {
            int r = resoluciones[resoluciones.Count - 1];
            int rc = (r - 1) / 2 + 1;
            float[] vf = niveles[niveles.Count - 1];
            bool[] ff = fijos[fijos.Count - 1];
            var vc = new float[rc * rc];
            var fc = new bool[rc * rc];
            for (int zc = 0; zc < rc; zc++)
            for (int xc = 0; xc < rc; xc++)
            {
                float suma = 0f; int cuenta = 0;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = xc * 2 + dx, z = zc * 2 + dz;
                    if (x < 0 || z < 0 || x >= r || z >= r) continue;
                    int i = z * r + x;
                    if (ff[i]) { suma += vf[i]; cuenta++; }
                }
                if (cuenta > 0) { fc[zc * rc + xc] = true; vc[zc * rc + xc] = suma / cuenta; }
            }
            resoluciones.Add(rc); niveles.Add(vc); fijos.Add(fc);
        }

        // Nivel más grueso: partir del promedio y relajar muchas veces
        int ultimo = resoluciones.Count - 1;
        float promedio = 0f; int nf = 0;
        for (int i = 0; i < niveles[ultimo].Length; i++) if (fijos[ultimo][i]) { promedio += niveles[ultimo][i]; nf++; }
        promedio = nf > 0 ? promedio / nf : 0f;
        float[] sol = new float[niveles[ultimo].Length];
        for (int i = 0; i < sol.Length; i++) sol[i] = fijos[ultimo][i] ? niveles[ultimo][i] : promedio;
        Relajar(sol, fijos[ultimo], resoluciones[ultimo], 600);

        // Subir de nivel: ampliar y relajar
        for (int l = ultimo - 1; l >= 0; l--)
        {
            int r = resoluciones[l], rc = resoluciones[l + 1];
            var fino = new float[r * r];
            for (int z = 0; z < r; z++)
            for (int x = 0; x < r; x++)
            {
                int i = z * r + x;
                if (fijos[l][i]) { fino[i] = niveles[l][i]; continue; }
                float gx = x * 0.5f, gz = z * 0.5f;
                int x0 = Mathf.Min((int)gx, rc - 2), z0 = Mathf.Min((int)gz, rc - 2);
                float tx = gx - x0, tz = gz - z0;
                float a = Mathf.Lerp(sol[z0 * rc + x0], sol[z0 * rc + x0 + 1], tx);
                float b = Mathf.Lerp(sol[(z0 + 1) * rc + x0], sol[(z0 + 1) * rc + x0 + 1], tx);
                fino[i] = Mathf.Lerp(a, b, tz);
            }
            sol = fino;
            Relajar(sol, fijos[l], r, l == 0 ? 40 : 80);
        }
        return sol;
    }

    // Gauss-Seidel con sobre-relajación: cada punto libre se acerca al promedio de sus 4 vecinos.
    static void Relajar(float[] v, bool[] fijo, int r, int iteraciones)
    {
        const float omega = 1.8f;
        for (int it = 0; it < iteraciones; it++)
        {
            for (int z = 0; z < r; z++)
            {
                int zArriba = z > 0 ? z - 1 : z, zAbajo = z < r - 1 ? z + 1 : z;
                for (int x = 0; x < r; x++)
                {
                    int i = z * r + x;
                    if (fijo[i]) continue;
                    int xIzq = x > 0 ? x - 1 : x, xDer = x < r - 1 ? x + 1 : x;
                    float prom = 0.25f * (v[z * r + xIzq] + v[z * r + xDer] + v[zArriba * r + x] + v[zAbajo * r + x]);
                    v[i] += omega * (prom - v[i]);
                }
            }
        }
    }

    // Desenfoque de caja (varias pasadas ≈ desenfoque gaussiano).
    static void Desenfocar(float[] a, int res, int radio, int pasadas)
    {
        var tmp = new float[a.Length];
        float inv = 1f / (2 * radio + 1);
        for (int p = 0; p < pasadas; p++)
        {
            // horizontal: a -> tmp
            for (int z = 0; z < res; z++)
            {
                int fila = z * res;
                float suma = 0f;
                for (int k = -radio; k <= radio; k++) suma += a[fila + Mathf.Clamp(k, 0, res - 1)];
                for (int x = 0; x < res; x++)
                {
                    tmp[fila + x] = suma * inv;
                    suma += a[fila + Mathf.Min(x + radio + 1, res - 1)] - a[fila + Mathf.Max(x - radio, 0)];
                }
            }
            // vertical: tmp -> a
            for (int x = 0; x < res; x++)
            {
                float suma = 0f;
                for (int k = -radio; k <= radio; k++) suma += tmp[Mathf.Clamp(k, 0, res - 1) * res + x];
                for (int z = 0; z < res; z++)
                {
                    a[z * res + x] = suma * inv;
                    suma += tmp[Mathf.Min(z + radio + 1, res - 1) * res + x] - tmp[Mathf.Max(z - radio, 0) * res + x];
                }
            }
        }
    }
}
