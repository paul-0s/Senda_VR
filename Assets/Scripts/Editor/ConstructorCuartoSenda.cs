using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SendaVR.EditorTools
{
    /// <summary>
    /// Menú "Senda.vr > Cuarto": arma la escena del cuarto del estudiante
    /// a partir del modelo exportado desde Blender (FBX) en Assets/SendaVR/Cuarto.
    /// </summary>
    public static partial class ConstructorMenuSenda
    {
        const string CarpetaCuarto = Raiz + "/Cuarto";
        const string EscenaCuarto = Raiz + "/Cuarto.unity";
        const string EscenaMenu = Raiz + "/Menu.unity";
        const string NombreEscenaCuarto = "Cuarto";
        const float AlturaOjos = 1.5f;

        // Palabras para reconocer cada objeto por su nombre en Blender (sin tildes, en minúsculas)
        static readonly Dictionary<TipoObjeto, string[]> Claves = new Dictionary<TipoObjeto, string[]>
        {
            { TipoObjeto.Mochila,      new[] { "mochila", "backpack", "bagpack" } },
            { TipoObjeto.Botella,      new[] { "botella", "bottle", "hydroflask" } },
            { TipoObjeto.Zapatillas,   new[] { "zapatilla", "zapatillas", "boots", "boot", "shoe", "shoes", "bota", "botas" } },
            { TipoObjeto.PaloTrekking, new[] { "palotrekking", "palo", "bastones", "baston", "pole", "poles", "trekkingpole", "march" } },
            { TipoObjeto.Snacks,       new[] { "snack", "snacks", "almond", "almonds", "almendras" } },
        };
        static readonly string[] ClavesComputador = { "pantalla", "monitor", "computador", "computer", "pc", "screen" };

        // ================================================================ menú

        [MenuItem("Senda.vr/Cuarto/Construir cuarto", priority = 20)]
        static void ConstruirCuarto()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Senda.vr", "Detén el Play ▶ antes de construir el cuarto.", "OK");
                return;
            }

            var modelo = BuscarModeloCuarto();
            if (modelo == null)
            {
                EditorUtility.DisplayDialog("Senda.vr",
                    "No encontré el modelo del cuarto.\n\n" +
                    "1. En Blender: File > Export > FBX.\n" +
                    "2. Guárdalo dentro de la carpeta del proyecto:\n   " + CarpetaCuarto + "\n" +
                    "3. Vuelve a ejecutar Senda.vr > Cuarto > Construir cuarto.\n\n" +
                    "(Mira el LEEME para las opciones de exportación.)", "OK");
                return;
            }

            if (File.Exists(EscenaCuarto) &&
                !EditorUtility.DisplayDialog("Senda.vr",
                    "Ya existe la escena Cuarto. ¿Reconstruirla desde cero?\n(Los cambios hechos a mano en esa escena se pierden.)",
                    "Reconstruir", "Cancelar"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            PrepararImportacion(modelo);
            ConfigurarImportadores();
            fuente = BuscarFuente();

            var escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 1) El cuarto
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GetAssetPath(modelo));
            var cuarto = (GameObject)PrefabUtility.InstantiatePrefab(prefab, escena);
            cuarto.name = "Cuarto (" + prefab.name + ")";
            var caja = Limites(cuarto);

            // Aviso si algo vino con la escala mal (pasa con "Apply Transform" en modelos de Sketchfab)
            var diminutos = cuarto.transform.Cast<Transform>()
                .Where(h => h.GetComponentInChildren<Renderer>(true) != null)
                .Where(h => { var s = Limites(h.gameObject).size; return Mathf.Max(s.x, s.y, s.z) < 0.03f; })
                .Select(h => h.name).ToList();
            if (diminutos.Count > 0)
                Debug.LogWarning("[Senda.vr] Estos objetos quedaron diminutos (menos de 3 cm): " + string.Join(", ", diminutos) +
                                 ". Exporta desde Blender con 'Apply Transform' DESMARCADO y 'Apply Scalings: FBX Units Scale'.");

            // Luces que vienen de Blender: solo una luz direccional puede proyectar sombras
            foreach (var l in cuarto.GetComponentsInChildren<Light>(true))
                if (l.type == LightType.Directional) l.shadows = LightShadows.None;

            // 2) Objetos que se pueden tomar y computador
            var encontrados = new Dictionary<TipoObjeto, Transform>();
            foreach (var par in Claves)
            {
                var t = Buscar(cuarto.transform, par.Value);
                if (t == null) continue;
                encontrados[par.Key] = t;
                HacerTomable(t.gameObject, par.Key);
            }
            var pantalla = Buscar(cuarto.transform, ClavesComputador);
            if (pantalla != null) HacerComputador(pantalla.gameObject);

            // 3) Luz extra en el centro del cuarto (el modelo viene sin luces de Unity)
            var luz = new GameObject("Luz del cuarto").AddComponent<Light>();
            luz.type = LightType.Point;
            luz.transform.position = new Vector3(caja.center.x, caja.max.y - 0.4f, caja.center.z);
            luz.range = Mathf.Max(caja.size.x, caja.size.z) * 1.2f;
            luz.intensity = 2f;
            luz.shadows = LightShadows.Soft;

            // 4) Jugador
            var jugador = CrearJugadorEn(PuntoInicio(cuarto.transform, caja), out var cam);
            MirarHacia(jugador.transform, pantalla != null ? pantalla.GetComponent<Computador>() : null);

            // 5) Interfaz: lista de la mochila, mira y ventana de objeto
            CrearEventSystem(false);
            var inventario = CrearHud(cam, out var mira, out var etiqueta, out var grupoEtiqueta);
            CrearVentanaObjeto();
            var interaccion = cam.gameObject.AddComponent<InteraccionJugador>();
            interaccion.camara = cam;
            interaccion.mira = mira;
            interaccion.etiqueta = etiqueta;
            interaccion.grupoEtiqueta = grupoEtiqueta;

            // 6) Guardar, agregar a Build Profiles y conectar con el menú
            EditorSceneManager.SaveScene(escena, EscenaCuarto);
            AgregarABuild();
            bool conectado = ConectarMenu();

            Selection.activeGameObject = jugador;
            EditorGUIUtility.PingObject(jugador);

            var informe = new StringBuilder("Cuarto construido y guardado en " + EscenaCuarto + ".\n\n");
            foreach (TipoObjeto tipo in System.Enum.GetValues(typeof(TipoObjeto)))
            {
                bool requerido = tipo != TipoObjeto.Snacks;
                if (encontrados.TryGetValue(tipo, out var t))
                    informe.Append("✓ " + TextosObjetos.Nombre(tipo) + "  →  " + t.name + "\n");
                else if (requerido)
                    informe.Append("✗ " + TextosObjetos.Nombre(tipo) + "  (no lo encontré)\n");
            }
            informe.Append(pantalla != null ? "✓ Computador  →  " + pantalla.name + "\n" : "✗ Computador (no lo encontré)\n");
            if (encontrados.Count < 4 || pantalla == null)
                informe.Append("\nPara los que faltan: selecciónalos en la Hierarchy y usa\nSenda.vr > Cuarto > Marcar selección como...\n");
            informe.Append(conectado
                ? "\nEl menú ya lleva al cuarto después de la bienvenida."
                : "\nEscribe \"Cuarto\" en 'Escena Siguiente' de PantallaLogin (escena Menu).");
            informe.Append("\n\nRevisa que el Jugador haya quedado parado en el suelo y no dentro de un mueble; si no, muévelo y guarda (Ctrl + S).");
            EditorUtility.DisplayDialog("Senda.vr", informe.ToString(), "OK");
        }

        [MenuItem("Senda.vr/Cuarto/Marcar selección como.../Mochila", priority = 21)]
        static void MarcarMochila() => MarcarSeleccion(TipoObjeto.Mochila);
        [MenuItem("Senda.vr/Cuarto/Marcar selección como.../Botella de agua", priority = 22)]
        static void MarcarBotella() => MarcarSeleccion(TipoObjeto.Botella);
        [MenuItem("Senda.vr/Cuarto/Marcar selección como.../Zapatillas", priority = 23)]
        static void MarcarZapatillas() => MarcarSeleccion(TipoObjeto.Zapatillas);
        [MenuItem("Senda.vr/Cuarto/Marcar selección como.../Palo de trekking", priority = 24)]
        static void MarcarPalo() => MarcarSeleccion(TipoObjeto.PaloTrekking);
        [MenuItem("Senda.vr/Cuarto/Marcar selección como.../Snacks", priority = 25)]
        static void MarcarSnacks() => MarcarSeleccion(TipoObjeto.Snacks);

        [MenuItem("Senda.vr/Cuarto/Marcar selección como.../Computador", priority = 30)]
        static void MarcarComputador()
        {
            var go = Selection.activeGameObject;
            if (!ValidarSeleccion(go)) return;
            Undo.RegisterFullObjectHierarchyUndo(go, "Marcar computador");
            HacerComputador(go);
            EditorSceneManager.MarkSceneDirty(go.scene);
        }

        static void MarcarSeleccion(TipoObjeto tipo)
        {
            var go = Selection.activeGameObject;
            if (!ValidarSeleccion(go)) return;
            Undo.RegisterFullObjectHierarchyUndo(go, "Marcar " + tipo);
            HacerTomable(go, tipo);
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[Senda.vr] '" + go.name + "' ahora es: " + TextosObjetos.Nombre(tipo) + ". Puedes editar su texto en el Inspector (ObjetoTomable).");
        }

        static bool ValidarSeleccion(GameObject go)
        {
            if (go != null && go.scene.IsValid()) return true;
            EditorUtility.DisplayDialog("Senda.vr", "Primero selecciona el objeto en la Hierarchy (en la escena Cuarto).", "OK");
            return false;
        }

        // ================================================================ objetos

        static void HacerTomable(GameObject go, TipoObjeto tipo)
        {
            foreach (var otro in go.GetComponents<ObjetoTomable>()) Object.DestroyImmediate(otro);
            var obj = go.AddComponent<ObjetoTomable>();
            obj.tipo = tipo;
            obj.AplicarTextos();
            ColliderParaApuntar(go);
        }

        static void HacerComputador(GameObject go)
        {
            if (go.GetComponent<Computador>() == null) go.AddComponent<Computador>();
            ColliderParaApuntar(go);
        }

        /// <summary>Caja invisible (trigger) que cubre el objeto, para que sea fácil apuntarle.</summary>
        static void ColliderParaApuntar(GameObject go)
        {
            var caja = Limites(go);
            if (caja.size == Vector3.zero) return;
            var col = go.GetComponent<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            var t = go.transform;
            col.center = t.InverseTransformPoint(caja.center);
            var s = t.InverseTransformVector(caja.size);
            col.size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z)) * 1.05f;
        }

        static Bounds Limites(GameObject go)
        {
            var renders = go.GetComponentsInChildren<Renderer>(true);
            if (renders.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renders[0].bounds;
            foreach (var r in renders) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Busca el objeto cuyo nombre coincide, prefiriendo el más alto en la jerarquía.</summary>
        static Transform Buscar(Transform raiz, string[] claves)
        {
            Transform mejor = null;
            int mejorProfundidad = int.MaxValue;
            void Recorrer(Transform t, int prof)
            {
                if (t != raiz && Coincide(t.name, claves) && prof < mejorProfundidad)
                {
                    mejor = t; mejorProfundidad = prof;
                    return; // no seguir dentro: ya tenemos el objeto completo
                }
                foreach (Transform hijo in t) Recorrer(hijo, prof + 1);
            }
            Recorrer(raiz, 0);
            return mejor;
        }

        static bool Coincide(string nombre, string[] claves)
        {
            string n = Normalizar(nombre);
            var palabras = n.Split(new[] { ' ', '_', '-', '.', '(', ')' }, System.StringSplitOptions.RemoveEmptyEntries);
            string junto = string.Concat(palabras);
            foreach (var c in claves)
            {
                if (palabras.Contains(c)) return true;              // palabra exacta ("pc", "palo")
                if (c.Length >= 5 && junto.Contains(c)) return true; // dentro de otra ("hikingbackpack")
            }
            return false;
        }

        static string Normalizar(string s)
        {
            var d = s.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in d)
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
            // separar "PaloTrekking" -> "palo trekking" no hace falta: se compara también todo junto
            return sb.ToString();
        }

        // ================================================================ modelo

        static GameObject BuscarModeloCuarto()
        {
            if (!AssetDatabase.IsValidFolder(CarpetaCuarto))
            {
                AssetDatabase.CreateFolder(Raiz, "Cuarto");
                return null;
            }
            var modelos = AssetDatabase.FindAssets("t:Model", new[] { CarpetaCuarto })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderByDescending(r => { try { return new FileInfo(r).Length; } catch { return 0L; } })
                .ToList();
            return modelos.Count == 0 ? null : AssetDatabase.LoadAssetAtPath<GameObject>(modelos[0]);
        }

        static void PrepararImportacion(GameObject modelo)
        {
            string ruta = AssetDatabase.GetAssetPath(modelo);
            if (AssetImporter.GetAtPath(ruta) is ModelImporter mi && !mi.addCollider)
            {
                mi.addCollider = true; // paredes, piso y muebles sólidos
                mi.SaveAndReimport();
            }

            // Texturas: máximo 2048 (las originales son enormes) y mapas normales bien marcados
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaCuarto }))
            {
                var r = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(r) is TextureImporter ti)) continue;
                bool esNormal = Path.GetFileNameWithoutExtension(r).ToLowerInvariant().Contains("normal");
                bool cambio = false;
                if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; cambio = true; }
                if (esNormal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; cambio = true; }
                if (cambio) ti.SaveAndReimport();
            }
        }

        // ================================================================ jugador

        /// <summary>
        /// Busca un lugar en el piso del cuarto donde quepa el jugador (sin muebles encima),
        /// lo más cerca posible del centro. Los rayos parten a 1,5 m del suelo para no chocar con el techo.
        /// Si en Blender hay un objeto vacío llamado "Spawn" o "Inicio", se usa ese lugar.
        /// </summary>
        static Vector3 PuntoInicio(Transform cuarto, Bounds caja)
        {
            Physics.SyncTransforms();
            const QueryTriggerInteraction sinTriggers = QueryTriggerInteraction.Ignore;

            var marca = Buscar(cuarto, new[] { "spawn", "inicio", "jugadorinicio", "playerstart" });
            if (marca != null)
            {
                if (Physics.Raycast(marca.position + Vector3.up * 0.5f, Vector3.down, out var h, 5f, ~0, sinTriggers))
                    return h.point + Vector3.up * 0.02f;
                return marca.position;
            }

            // Piso aproximado: el punto más bajo que alcanzan rayos desde arriba del centro
            float alturaRayo = caja.min.y + 1.5f;
            var golpes = new List<Vector3>();
            const int n = 21;
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                {
                    float x = Mathf.Lerp(caja.min.x + 0.4f, caja.max.x - 0.4f, i / (n - 1f));
                    float z = Mathf.Lerp(caja.min.z + 0.4f, caja.max.z - 0.4f, j / (n - 1f));
                    if (Physics.Raycast(new Vector3(x, alturaRayo, z), Vector3.down, out var h, 3f, ~0, sinTriggers))
                        golpes.Add(h.point);
                }
            if (golpes.Count == 0) return new Vector3(caja.center.x, caja.min.y + 0.05f, caja.center.z);

            // el piso es la altura más repetida entre los golpes más bajos
            var ordenados = golpes.Select(g => g.y).OrderBy(y => y).ToList();
            float piso = ordenados[Mathf.Min(ordenados.Count - 1, ordenados.Count / 10)];

            var centro = new Vector2(caja.center.x, caja.center.z);
            var libres = golpes
                .Where(g => Mathf.Abs(g.y - piso) < 0.08f)
                .Where(g => !Physics.CheckCapsule(g + Vector3.up * 0.35f, g + Vector3.up * 1.4f, 0.32f, ~0, sinTriggers))
                .OrderBy(g => Vector2.Distance(new Vector2(g.x, g.z), centro))
                .ToList();
            var elegido = libres.Count > 0 ? libres[0] : golpes.OrderBy(g => Mathf.Abs(g.y - piso)).First();
            return elegido + Vector3.up * 0.02f;
        }

        [MenuItem("Senda.vr/Cuarto/Reubicar jugador en el piso", priority = 40)]
        static void ReubicarJugador()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var jugador = Object.FindAnyObjectByType<PlayerController>();
            var cuarto = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("Cuarto ("));
            if (jugador == null || cuarto == null)
            {
                EditorUtility.DisplayDialog("Senda.vr", "Abre la escena Cuarto (con su Jugador) primero.", "OK");
                return;
            }
            Undo.RecordObject(jugador.transform, "Reubicar jugador");
            jugador.transform.position = PuntoInicio(cuarto.transform, Limites(cuarto));
            MirarHacia(jugador.transform, Object.FindAnyObjectByType<Computador>());
            EditorSceneManager.MarkSceneDirty(jugador.gameObject.scene);
            Selection.activeGameObject = jugador.gameObject;
        }

        /// <summary>Gira al jugador (solo en horizontal) para que mire hacia el computador.</summary>
        static void MirarHacia(Transform jugador, Component destino)
        {
            if (destino == null) return;
            var dir = destino.transform.position - jugador.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f) jugador.rotation = Quaternion.LookRotation(dir);
        }

        static GameObject CrearJugadorEn(Vector3 pos, out Camera cam)
        {
            cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
                cam.gameObject.AddComponent<AudioListener>();
            }

            var jugador = new GameObject("Jugador");
            jugador.transform.position = pos;
            var cc = jugador.AddComponent<CharacterController>();
            // El cuarto mide 2,3 m de alto: jugador un poco más bajo (ojos a 1,5 m)
            cc.height = 1.6f; cc.radius = 0.28f; cc.center = new Vector3(0f, 0.8f, 0f);
            cc.stepOffset = 0.3f; cc.slopeLimit = 50f;

            var offset = new GameObject("CameraOffset").transform;
            offset.SetParent(jugador.transform, false);
            offset.localPosition = new Vector3(0f, AlturaOjos, 0f);

            cam.transform.SetParent(offset, false);
            cam.transform.localPosition = Vector3.zero;
            cam.transform.localRotation = Quaternion.identity;
            cam.nearClipPlane = 0.05f;

            var pc = jugador.AddComponent<PlayerController>();
            pc.eyeHeight = AlturaOjos;
            pc.cameraOffset = offset;
            pc.playerCamera = cam;
            return jugador;
        }

        // ================================================================ interfaz

        static Canvas CanvasNuevo(string nombre, int orden)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = orden;
            var esc = go.AddComponent<CanvasScaler>();
            esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            esc.referenceResolution = new Vector2(1920, 1080);
            esc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        static Inventario CrearHud(Camera cam, out Image mira, out TextMeshProUGUI etiqueta, out CanvasGroup grupoEtiqueta)
        {
            var canvas = CanvasNuevo("Cuarto_HUD", 5);
            var rt = (RectTransform)canvas.transform;
            var adapt = canvas.gameObject.AddComponent<CanvasAdaptable>();
            adapt.seguirCabeza = true;
            adapt.distancia = 0.8f;
            adapt.escalaVR = 0.00045f;

            // Lista de la mochila (arriba a la izquierda)
            var lista = Panel("Lista mochila", rt, 40, 40, 400, 230);
            var textoLista = Txt("Texto", lista, "Mochila 0/4", 26, SendaEstilo.Blanco, false, TextAlignmentOptions.TopLeft);
            Estirar(textoLista.rectTransform, 28, 20, 20, 22);
            textoLista.lineSpacing = 8;

            // Mira (centro)
            mira = Img("Mira", rt, Spr("circulo"), new Color(1f, 1f, 1f, 0.7f));
            Centro(mira.rectTransform, 0, 0, 14, 14);
            mira.raycastTarget = false;

            // Nombre del objeto apuntado
            var fondoEtiqueta = Img("Etiqueta", rt, Spr("pildora"), new Color(0f, 0f, 0f, 0.6f));
            Centro(fondoEtiqueta.rectTransform, 0, -80, 460, 92);
            fondoEtiqueta.raycastTarget = false;
            grupoEtiqueta = fondoEtiqueta.gameObject.AddComponent<CanvasGroup>();
            grupoEtiqueta.alpha = 0f;
            grupoEtiqueta.blocksRaycasts = false;
            etiqueta = Txt("Texto", fondoEtiqueta.rectTransform, "", 30, SendaEstilo.Blanco, false, TextAlignmentOptions.Center);
            Estirar(etiqueta.rectTransform, 20, 6, 20, 6);

            // Aviso (abajo)
            var aviso = Img("Aviso", rt, Spr("pildora"), new Color(SendaEstilo.Naranja.r, SendaEstilo.Naranja.g, SendaEstilo.Naranja.b, 0.95f));
            Centro(aviso.rectTransform, 0, -400, 1000, 80);
            aviso.raycastTarget = false;
            var grupoAviso = aviso.gameObject.AddComponent<CanvasGroup>();
            grupoAviso.blocksRaycasts = false;
            var textoAviso = Txt("Texto", aviso.rectTransform, "", 28, SendaEstilo.Blanco, true, TextAlignmentOptions.Center);
            Estirar(textoAviso.rectTransform, 30, 0, 30, 0);

            var inv = canvas.gameObject.AddComponent<Inventario>();
            inv.textoLista = textoLista;
            inv.textoAviso = textoAviso;
            inv.grupoAviso = grupoAviso;
            return inv;
        }

        static VentanaObjeto CrearVentanaObjeto()
        {
            var canvas = CanvasNuevo("Cuarto_Ventana", 20);
            var rt = (RectTransform)canvas.transform;
            var grupo = canvas.gameObject.AddComponent<CanvasGroup>();

            var oscuro = Img("Oscurecer", rt, null, new Color(0f, 0f, 0f, 0.45f));
            Estirar(oscuro.rectTransform);

            var adapt = canvas.gameObject.AddComponent<CanvasAdaptable>();
            adapt.seguirCabeza = false;
            adapt.distancia = 1.3f;
            adapt.escalaVR = 0.0007f;
            adapt.soloPC = new[] { oscuro.gameObject };

            // Panel 1100 x 620 en el centro (mismo estilo que el login)
            var panelImg = Img("Panel", rt, Spr("panel_redondeado"), SendaEstilo.Panel);
            var panel = panelImg.rectTransform;
            Centro(panel, 0, 0, 1100, 620);

            var marco = Img("Marco vista", panel, Spr("panel_redondeado"), new Color(1f, 1f, 1f, 0.06f));
            Figma(marco.rectTransform, 40, 50, 440, 440);
            var vista = Nuevo("Vista objeto", marco.rectTransform).gameObject.AddComponent<RawImage>();
            Estirar(vista.rectTransform, 10, 10, 10, 10);
            vista.raycastTarget = false;

            var titulo = Txt("Titulo", panel, "Objeto", 44, SendaEstilo.Blanco, true, TextAlignmentOptions.TopLeft);
            Figma(titulo.rectTransform, 520, 50, 540, 64);
            var linea = Img("Linea", panel, null, SendaEstilo.Naranja);
            Figma(linea.rectTransform, 522, 122, 80, 5);
            var desc = Txt("Descripcion", panel, "", 26, SendaEstilo.Blanco, false, TextAlignmentOptions.TopLeft);
            Figma(desc.rectTransform, 520, 150, 540, 320);
            desc.enableAutoSizing = true; desc.fontSizeMin = 18; desc.fontSizeMax = 26;
            desc.lineSpacing = 6;

            var principal = Boton("Boton guardar", panel, 520, 506, 330, 70, "Guardar en la mochila", SendaEstilo.Naranja, SendaEstilo.NaranjaIntenso, 26);
            var cerrar = Boton("Boton cerrar", panel, 866, 506, 194, 70, "Cerrar", SendaEstilo.Azul, SendaEstilo.Azul * 1.12f, 26);
            var ayuda = Txt("Ayuda", panel, "", 20, SendaEstilo.TextoSuave, false, TextAlignmentOptions.Center);
            Figma(ayuda.rectTransform, 40, 512, 440, 60);

            var v = canvas.gameObject.AddComponent<VentanaObjeto>();
            v.grupo = grupo;
            v.panel = panel;
            v.vista = vista;
            v.titulo = titulo;
            v.descripcion = desc;
            v.ayuda = ayuda;
            v.botonPrincipal = principal;
            v.textoBotonPrincipal = principal.GetComponentInChildren<TextMeshProUGUI>();
            v.botonCerrar = cerrar;
            grupo.alpha = 0f;
            grupo.blocksRaycasts = false;
            return v;
        }

        // ================================================================ build y menú

        /// <summary>
        /// Ordena las escenas del recorrido en Build Profiles. Si existen las escenas VR 360:
        /// Logo360 → Inicio360 → Bienvenida360 → Cuarto → sendero (y Menu de PC al final).
        /// Si no, el recorrido de PC: Menu → Cuarto → sendero.
        /// </summary>
        static void AgregarABuild()
        {
            var sendero = AssetDatabase.FindAssets("OutdoorsScene t:Scene").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
            bool recorrido360 = File.Exists(EscenaLogo360) && File.Exists(EscenaInicio360);
            var orden = recorrido360
                ? new[] { EscenaLogo360, EscenaInicio360, EscenaBienvenida360, EscenaCuarto, sendero, EscenaMenu }
                : new[] { EscenaMenu, EscenaCuarto, sendero };

            var lista = orden.Where(r => !string.IsNullOrEmpty(r) && File.Exists(r))
                             .Select(r => new EditorBuildSettingsScene(r, true)).ToList();
            // el resto de escenas que ya estaban en la lista se mantienen al final
            foreach (var s in EditorBuildSettings.scenes)
                if (File.Exists(s.path) && lista.All(x => x.path != s.path)) lista.Add(s);
            EditorBuildSettings.scenes = lista.ToArray();
        }

        /// <summary>Abre la escena Menu, escribe "Cuarto" en PantallaLogin > Escena Siguiente y la guarda.</summary>
        static bool ConectarMenu()
        {
            if (!File.Exists(EscenaMenu)) return false;
            var actual = SceneManager.GetActiveScene();
            var menu = EditorSceneManager.OpenScene(EscenaMenu, OpenSceneMode.Additive);
            bool ok = false;
            foreach (var raiz in menu.GetRootGameObjects())
                foreach (var login in raiz.GetComponentsInChildren<PantallaLogin>(true))
                {
                    login.escenaSiguiente = NombreEscenaCuarto;
                    EditorUtility.SetDirty(login);
                    ok = true;
                }
            if (ok) EditorSceneManager.SaveScene(menu);
            EditorSceneManager.CloseScene(menu, true);
            SceneManager.SetActiveScene(actual);
            return ok;
        }
    }
}
