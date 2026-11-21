using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SendaVR.EditorTools
{
    /// <summary>
    /// "Senda.vr > Bienvenida 360 > Construir escena": arma la escena Bienvenida360 con
    /// el fondo 360 (video en una esfera) y el logo y los textos animados al frente.
    /// </summary>
    public static partial class ConstructorMenuSenda
    {
        const string EscenaBienvenida360 = Raiz + "/Bienvenida360.unity";
        const string RutaLogoBlanco = Raiz + "/Sprites/Logo/logo_senda_blanco.png";
        const string RutaBrillo = Raiz + "/Sprites/UI/brillo_banda.png";
        const float EscalaTextos360 = 0.00156f; // 1920 px del diseño ≈ 3 m de ancho a 2,5 m (≈ 62°)

        struct TextoBienvenida
        {
            public string texto; public float inicio, revelado, salida, salidaDur;
            public TextoBienvenida(string t, float i, float r, float s, float sd) { texto = t; inicio = i; revelado = r; salida = s; salidaDur = sd; }
        }

        // Tiempos medidos cuadro a cuadro en Video_de_bienvenida.mp4 (After Effects)
        static readonly TextoBienvenida[] TextosBienvenida =
        {
            new TextoBienvenida("¡Hola!",                          4.12f, 0.25f,  5.28f, 0.30f),
            new TextoBienvenida("¡Bienvenido a Senda.vr! :)",      6.28f, 0.30f,  7.78f, 0.30f),
            new TextoBienvenida("Esto es aún un prototipo",        8.84f, 0.32f, 10.38f, 0.30f),
            new TextoBienvenida("Pero espero que lo disfrutes <3", 11.00f, 0.30f, 12.60f, 0.35f),
        };

        [MenuItem("Senda.vr/Bienvenida 360/Construir escena", priority = 15)]
        static void ConstruirBienvenida360()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Senda.vr", "Detén el Play ▶ antes de construir la bienvenida 360.", "OK");
                return;
            }
            var clip = BuscarVideo("bienvenida_fondo_360");
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Senda.vr", "No encontré el video " + CarpetaVideo + "/bienvenida_fondo_360.mp4.", "OK");
                return;
            }
            if (File.Exists(EscenaBienvenida360) &&
                !EditorUtility.DisplayDialog("Senda.vr", "Ya existe la escena Bienvenida360. ¿Reconstruirla?", "Reconstruir", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var spriteLogo = SpriteDe(RutaLogoBlanco, true);
            var spriteBrillo = SpriteDe(RutaBrillo, false);
            var fuenteArial = FuenteArial(out bool yaEsNegrita);

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------- cámara (PC y VR)
            var cabeza = new GameObject("Cabeza (mouse en PC)");
            cabeza.AddComponent<CamaraLibre360>();
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(cabeza.transform, false);
            camGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            camGO.AddComponent<AudioListener>();
            var tpd = camGO.AddComponent<TrackedPoseDriver>(); // en VR, la cabeza mueve la cámara
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.positionInput = new InputActionProperty(new InputAction("Posicion cabeza", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction("Rotacion cabeza", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));

            // ---------------------------------------------------------- fondo 360
            string rutaRT = CarpetaVideo + "/Bienvenida360_RT.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rutaRT);
            if (rt == null)
            {
                rt = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "Bienvenida360_RT" };
                AssetDatabase.CreateAsset(rt, rutaRT);
            }
            string rutaMat = CarpetaVideo + "/Bienvenida360_Mat.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            // "UI/Default" funciona igual con el render clásico de Unity y con URP, y permite teñir (fundido a negro)
            var shader = Shader.Find("UI/Default");
            if (mat == null)
            {
                mat = new Material(shader) { name = "Bienvenida360_Mat" };
                AssetDatabase.CreateAsset(mat, rutaMat);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.mainTexture = rt;
            mat.color = Color.white;
            mat.renderQueue = 1000; // fondo: se dibuja antes que el logo y los textos
            EditorUtility.SetDirty(mat);

            var fondo = new GameObject("Fondo 360");
            fondo.AddComponent<MeshFilter>();
            var mr = fondo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var esfera = fondo.AddComponent<EsferaVideo360>();
            esfera.seguir = camGO.transform;
            var vp = fondo.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.waitForFirstFrame = true;
            vp.skipOnDrop = true;
            vp.isLooping = false;

            // ---------------------------------------------------------- logo y textos al frente
            var canvasGO = new GameObject("Textos 360", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var crt = (RectTransform)canvasGO.transform;
            crt.sizeDelta = new Vector2(1920f, 1080f);
            crt.localScale = Vector3.one * EscalaTextos360;
            crt.position = new Vector3(0f, 1.6f, 2.5f);
            canvasGO.AddComponent<CanvasGroup>();

            var logoGO = new GameObject("Logo", typeof(RectTransform));
            logoGO.transform.SetParent(crt, false);
            var logoImg = logoGO.AddComponent<Image>();
            logoImg.sprite = spriteLogo;
            logoImg.preserveAspect = true;
            logoImg.raycastTarget = false;
            var lrt = (RectTransform)logoGO.transform;
            float altoLogo = 310f;
            float anchoLogo = spriteLogo != null ? altoLogo * spriteLogo.rect.width / spriteLogo.rect.height : 236f;
            lrt.sizeDelta = new Vector2(anchoLogo, altoLogo);
            lrt.anchoredPosition = Vector2.zero;
            logoGO.AddComponent<Mask>().showMaskGraphic = true;   // el brillo solo se ve dentro del logo
            var grupoLogo = logoGO.AddComponent<CanvasGroup>();
            grupoLogo.alpha = 0f;

            var brilloGO = new GameObject("Brillo", typeof(RectTransform));
            brilloGO.transform.SetParent(lrt, false);
            var brilloImg = brilloGO.AddComponent<Image>();
            brilloImg.sprite = spriteBrillo;
            brilloImg.color = new Color(1f, 0.96f, 0.72f, 0.9f);
            brilloImg.raycastTarget = false;
            var brt = (RectTransform)brilloGO.transform;
            brt.sizeDelta = new Vector2(110f, altoLogo * 1.8f);
            brt.localRotation = Quaternion.Euler(0f, 0f, -20f);
            brilloGO.SetActive(false);

            var animados = new TextoAnimadoLetras[TextosBienvenida.Length];
            for (int i = 0; i < TextosBienvenida.Length; i++)
            {
                var d = TextosBienvenida[i];
                var tGO = new GameObject("Texto " + (i + 1), typeof(RectTransform));
                tGO.transform.SetParent(crt, false);
                var tmp = tGO.AddComponent<TextMeshProUGUI>();
                tmp.text = d.texto;
                tmp.richText = false; // para que "<3" se vea tal cual
                if (fuenteArial != null) tmp.font = fuenteArial;
                tmp.fontSize = 78f;
                tmp.fontStyle = yaEsNegrita ? FontStyles.Normal : FontStyles.Bold;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;
                var r = (RectTransform)tGO.transform;
                r.sizeDelta = new Vector2(1800f, 160f);
                r.anchoredPosition = new Vector2(0f, -10f);
                var anim = tGO.AddComponent<TextoAnimadoLetras>();
                anim.inicio = d.inicio;
                anim.duracionRevelado = d.revelado;
                anim.inicioSalida = d.salida;
                anim.duracionSalida = d.salidaDur;
                animados[i] = anim;
            }

            // ---------------------------------------------------------- control
            var control = new GameObject("Bienvenida 360").AddComponent<Bienvenida360>();
            control.video = vp;
            control.esfera = mr;
            control.logo = grupoLogo;
            control.brillo = brt;
            control.brilloRecorrido = anchoLogo * 0.9f;
            control.textos = animados;
            control.panelTextos = crt;
            control.cabeza = camGO.transform;
            control.escenaSiguiente = File.Exists(EscenaCuarto) ? NombreEscenaCuarto : "";

            EditorSceneManager.SaveScene(escena, EscenaBienvenida360);
            AgregarABuild();
            AgregarEscenaABuild(EscenaBienvenida360, 1);

            EditorUtility.DisplayDialog("Senda.vr",
                "Bienvenida 360 lista (" + EscenaBienvenida360 + ").\n\n" +
                "Para probarla: dale Play con esta escena abierta.\n" +
                "• PC: clic para mirar alrededor con el mouse (Esc suelta el mouse).\n" +
                "• VR (Link): mira alrededor con el visor.\n" +
                "• Espacio / botón A: saltar.\n\n" +
                "Al terminar entra al Cuarto." +
                (fuenteArial == null || !fuenteArial.name.ToLowerInvariant().Contains("arial")
                    ? "\n\nNota: uso Liberation Sans (casi idéntica a Arial). Para Arial exacta, crea un Font Asset de Arial Bold con Window > TextMeshPro > Font Asset Creator y vuelve a construir."
                    : ""), "OK");
        }

        const string EscenaLogo360 = Raiz + "/Logo360.unity";

        /// <summary>
        /// Escena con el video 360 del logo hecho en After Effects (logo_360.mp4).
        /// Solo reproduce el video alrededor del usuario y luego pasa a la Bienvenida 360.
        /// </summary>
        [MenuItem("Senda.vr/Bienvenida 360/Construir escena Logo 360 (After Effects)", priority = 16)]
        static void ConstruirLogo360()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Senda.vr", "Detén el Play ▶ antes de construir.", "OK");
                return;
            }
            var clip = BuscarVideo("logo_360");
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Senda.vr", "No encontré " + CarpetaVideo + "/logo_360.mp4.", "OK");
                return;
            }
            if (File.Exists(EscenaLogo360) &&
                !EditorUtility.DisplayDialog("Senda.vr", "Ya existe la escena Logo360. ¿Reconstruirla?", "Reconstruir", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = CrearCamara360();

            string rutaRT = CarpetaVideo + "/Logo360_RT.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rutaRT);
            if (rt == null)
            {
                rt = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "Logo360_RT" };
                AssetDatabase.CreateAsset(rt, rutaRT);
            }
            var mr = CrearEsfera360(clip, rt, CarpetaVideo + "/Logo360_Mat.mat", cam.transform, 1f, out var vp);

            var control = new GameObject("Logo 360").AddComponent<Bienvenida360>();
            control.video = vp;
            control.esfera = mr;
            control.cabeza = cam.transform;
            control.duracion = (float)clip.length;
            control.fundidoFinal = 0.6f;
            control.escenaSiguiente = File.Exists(EscenaBienvenida360) ? "Bienvenida360"
                                    : File.Exists(EscenaCuarto) ? NombreEscenaCuarto : "";

            EditorSceneManager.SaveScene(escena, EscenaLogo360);
            AgregarABuild();
            AgregarEscenaABuild(EscenaLogo360, 1);
            EditorUtility.DisplayDialog("Senda.vr",
                "Escena Logo360 lista. Dale Play para verla (clic + mouse para mirar alrededor).\n" +
                "Al terminar pasa a: " + (string.IsNullOrEmpty(control.escenaSiguiente) ? "(nada)" : control.escenaSiguiente) + ".", "OK");
        }

        static Camera CrearCamara360()
        {
            var cabeza = new GameObject("Cabeza (mouse en PC)");
            cabeza.AddComponent<CamaraLibre360>();
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(cabeza.transform, false);
            camGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 200f;
            camGO.AddComponent<AudioListener>();
            var tpd = camGO.AddComponent<TrackedPoseDriver>();
            tpd.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tpd.positionInput = new InputActionProperty(new InputAction("Posicion cabeza", InputActionType.Value, "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
            tpd.rotationInput = new InputActionProperty(new InputAction("Rotacion cabeza", InputActionType.Value, "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
            return cam;
        }

        static MeshRenderer CrearEsfera360(UnityEngine.Video.VideoClip clip, RenderTexture rt, string rutaMat, Transform seguir, float brillo, out VideoPlayer vp)
        {
            var shader = Shader.Find("UI/Default");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(rutaMat);
            if (mat == null)
            {
                mat = new Material(shader) { name = Path.GetFileNameWithoutExtension(rutaMat) };
                AssetDatabase.CreateAsset(mat, rutaMat);
            }
            else if (mat.shader != shader) mat.shader = shader;
            mat.mainTexture = rt;
            mat.color = Color.white;
            mat.renderQueue = 1000;
            EditorUtility.SetDirty(mat);

            var fondo = new GameObject("Fondo 360");
            fondo.AddComponent<MeshFilter>();
            var mr = fondo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var esfera = fondo.AddComponent<EsferaVideo360>();
            esfera.seguir = seguir;
            esfera.brillo = brillo;
            vp = fondo.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.waitForFirstFrame = true;
            vp.skipOnDrop = true;
            vp.isLooping = false;
            return mr;
        }

        static void AgregarEscenaABuild(string ruta, int indice)
        {
            var lista = EditorBuildSettings.scenes.ToList();
            if (lista.Any(s => s.path == ruta)) return;
            lista.Insert(Mathf.Clamp(indice, 0, lista.Count), new EditorBuildSettingsScene(ruta, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }

        static Sprite SpriteDe(string ruta, bool mipmaps)
        {
            if (!(AssetImporter.GetAtPath(ruta) is TextureImporter ti)) return null;
            bool cambio = false;
            if (ti.textureType != TextureImporterType.Sprite) { ti.textureType = TextureImporterType.Sprite; cambio = true; }
            if (ti.spriteImportMode != SpriteImportMode.Single) { ti.spriteImportMode = SpriteImportMode.Single; cambio = true; }
            if (!ti.alphaIsTransparency) { ti.alphaIsTransparency = true; cambio = true; }
            if (ti.mipmapEnabled != mipmaps) { ti.mipmapEnabled = mipmaps; cambio = true; }
            if (cambio) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        }

        /// <summary>Busca un Font Asset de Arial; si no hay, Liberation Sans (misma forma y medidas que Arial).</summary>
        static TMP_FontAsset FuenteArial(out bool yaEsNegrita)
        {
            var todas = AssetDatabase.FindAssets("t:TMP_FontAsset")
                .Select(g => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(f => f != null).ToList();
            var arial = todas.FirstOrDefault(f => f.name.ToLowerInvariant().Contains("arial") && f.name.ToLowerInvariant().Contains("bold"))
                     ?? todas.FirstOrDefault(f => f.name.ToLowerInvariant().Contains("arial"))
                     ?? todas.FirstOrDefault(f => f.name.StartsWith("LiberationSans SDF"))
                     ?? TMP_Settings.defaultFontAsset;
            yaEsNegrita = arial != null && arial.name.ToLowerInvariant().Contains("bold");
            return arial;
        }
    }
}
