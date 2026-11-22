using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SendaVR.EditorTools
{
    /// <summary>
    /// "Senda.vr > Inicio VR (360) > Construir escena Inicio360":
    /// Términos, Inicio de sesión y Crear cuenta como paneles flotando frente al usuario,
    /// con el bosque en 360 alrededor. Recorrido: Logo360 → Inicio360 → Bienvenida360 → Cuarto.
    /// </summary>
    public static partial class ConstructorMenuSenda
    {
        const string EscenaInicio360 = Raiz + "/Inicio360.unity";
        const string NombreEscenaInicio360 = "Inicio360";
        const float BrilloBosque = 0.9f;   // entrada y bosque en bucle (antes 0,7: se veía oscuro)

        [MenuItem("Senda.vr/Inicio VR (360)/Construir escena Inicio360", priority = 10)]
        static void ConstruirInicio360()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Senda.vr", "Detén el Play ▶ antes de construir.", "OK");
                return;
            }
            var clip = BuscarVideo("menu_bosque_360");
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Senda.vr", "No encontré " + CarpetaVideo + "/menu_bosque_360.mp4.", "OK");
                return;
            }
            if (File.Exists(EscenaInicio360) &&
                !EditorUtility.DisplayDialog("Senda.vr", "Ya existe la escena Inicio360. ¿Reconstruirla?", "Reconstruir", "Cancelar"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigurarImportadores();
            fuente = BuscarFuente();
            var escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------------------------------------------------- cámara y bosque 360
            var cam = CrearCamara360();
            var mirar = cam.transform.parent.GetComponent<CamaraLibre360>();
            if (mirar != null) mirar.usarClicDerecho = false; // el mouse mueve la cabeza libremente (como en VR)
            // Punto de mira al centro: se apunta mirando y se presiona con clic (en VR se apaga solo)
            var puntero = cam.transform.parent.gameObject.AddComponent<PunteroMirada>();
            puntero.camara = cam;
            puntero.spritePunto = Spr("circulo");

            string rutaRT = CarpetaVideo + "/Inicio360_RT.renderTexture";
            var rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(rutaRT);
            if (rt == null)
            {
                rt = new RenderTexture((int)clip.width, (int)clip.height, 0) { name = "Inicio360_RT" };
                AssetDatabase.CreateAsset(rt, rutaRT);
            }
            // Fondo de Términos (degradado hecho en After Effects) y del Login (bosque)
            var clipTerminos = BuscarVideo("fondo_terminos_360");
            var clipEntrada = BuscarVideo("entrada_bosque_360");
            var esferaMR = CrearEsfera360(clipTerminos != null ? clipTerminos : clip, rt, CarpetaVideo + "/Inicio360_Mat.mat",
                                          cam.transform, clipTerminos != null ? 0.65f : 0.7f, out var vp);
            vp.isLooping = true;
            vp.playOnAwake = true;

            // ---------------------------------------------------------- paneles flotando
            var canvasGO = new GameObject(NombreCanvas, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var crt = (RectTransform)canvasGO.transform;
            crt.sizeDelta = new Vector2(1920, 1080);
            // Más cerca y más grande para poder leer: 1920 px = 2,3 m, a 1,3 m de los ojos
            // (el panel de Términos queda de ~1,5 m de ancho, ocupa ~60° de la vista)
            crt.localScale = Vector3.one * 0.0012f;
            crt.position = new Vector3(0f, 1.5f, 1.3f);
            canvasGO.GetComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            canvasGO.AddComponent<GraphicRaycaster>();  // mouse (PC)
            AgregarPorNombre(canvasGO, "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit"); // controles VR (si está XRI)
            var frente = canvasGO.AddComponent<PanelFrente>();
            frente.distancia = 1.3f;   // se ubica una sola vez al empezar y queda fijo
            CrearEventSystem(true);

            var contenedor = Nuevo("Pantallas", crt);
            Estirar(contenedor);
            var terminos = CrearTerminos(contenedor);
            var login = CrearLogin(contenedor, null);
            var paso1 = CrearCuentaPaso1(contenedor, null);
            var paso2 = CrearCuentaPaso2(contenedor, null);

            // Sin fondos planos: el fondo es el bosque 360
            string[] fondosPlanos = { "Fondo degradado animado", "Fondo (video)", "Fondo (imagen)", "Velo", "Velo inferior" };
            foreach (var pantalla in new Component[] { terminos, login, paso1, paso2 })
                foreach (Transform hijo in pantalla.transform)
                    if (fondosPlanos.Contains(hijo.name)) hijo.gameObject.SetActive(false);

            terminos.siguiente = login;
            login.pantallaCrearCuenta = paso1;
            login.pantallaCarga = null;
            login.videoFondo = null;
            login.escenaSiguiente = File.Exists(EscenaBienvenida360) ? "Bienvenida360"
                                  : File.Exists(EscenaCuarto) ? NombreEscenaCuarto : "";
            paso1.pantallaAnterior = login; paso1.pantallaSiguiente = paso2; paso1.login = login;
            paso2.pantallaAnterior = paso1; paso2.login = login; paso2.paso1 = paso1;
            foreach (var otra in new PantallaBase[] { login, paso1, paso2 }) otra.gameObject.SetActive(false);

            var nav = canvasGO.AddComponent<NavegadorPantallas>();
            nav.pantallaInicial = terminos;
            nav.pantallas = new PantallaBase[] { terminos, login, paso1, paso2 };

            // El fondo 360 cambia según la pantalla
            var fondos = esferaMR.gameObject.AddComponent<FondoSegunPantalla>();
            fondos.navegador = nav;
            fondos.reglas = new[]
            {
                new FondoSegunPantalla.Regla { pantalla = terminos, video = clipTerminos != null ? clipTerminos : clip, brillo = clipTerminos != null ? 0.65f : 0.7f },
                // Al aceptar los Términos: animación del bosque abriéndose (9 s) y luego el panel de inicio de sesión.
                // La entrada termina en el cuadro 198 del bosque en bucle → sigue en el 199 (7,96 s) sin corte.
                new FondoSegunPantalla.Regla { pantalla = login, video = clip, brillo = BrilloBosque,
                                               videoEntrada = clipEntrada, continuarEnSegundo = 199f / 25f },
                new FondoSegunPantalla.Regla { pantalla = paso1, video = clip, brillo = BrilloBosque },
                new FondoSegunPantalla.Regla { pantalla = paso2, video = clip, brillo = BrilloBosque },
            };

            EditorSceneManager.SaveScene(escena, EscenaInicio360);

            // Logo360 → Inicio360
            bool conectado = CambiarSiguienteDe(EscenaLogo360, NombreEscenaInicio360);
            AgregarABuild();

            EditorUtility.DisplayDialog("Senda.vr",
                "Escena Inicio360 lista.\n" +
                (clipTerminos != null ? "Fondo de Términos: fondo_terminos_360 · Fondo del Login: bosque.\n\n"
                                      : "(No encontré fondo_terminos_360.mp4: Términos usa el bosque.)\n\n") +
                (clipEntrada != null ? "Entre Términos y el inicio de sesión: animación de entrada al bosque (entrada_bosque_360).\n\n"
                                     : "(No encontré entrada_bosque_360.mp4: se pasa directo al inicio de sesión.)\n\n") +
                "Recorrido: Logo360 → Inicio360 (Términos, Login) → " + (string.IsNullOrEmpty(login.escenaSiguiente) ? "(nada)" : login.escenaSiguiente) +
                (login.escenaSiguiente == "Bienvenida360" ? " → Cuarto" : "") + ".\n" +
                (conectado ? "" : "\n(No encontré la escena Logo360: constrúyela para que el recorrido parta con el logo.)\n") +
                "\nPara probar: F5 (Probar experiencia completa) o Play en esta escena.\n" +
                "• PC: el mouse mueve la cabeza; apunta con el punto del centro y haz clic para presionar. Esc suelta el mouse.\n" +
                "• VR (Link): mira alrededor con el visor. Apuntar con los controles se agrega en el siguiente paso.", "OK");
        }

        /// <summary>Abre una escena, cambia "escenaSiguiente" de su Bienvenida360 y la guarda.</summary>
        static bool CambiarSiguienteDe(string rutaEscena, string siguiente)
        {
            if (!File.Exists(rutaEscena)) return false;
            var actual = SceneManager.GetActiveScene();
            var s = EditorSceneManager.OpenScene(rutaEscena, OpenSceneMode.Additive);
            bool ok = false;
            foreach (var raiz in s.GetRootGameObjects())
                foreach (var b in raiz.GetComponentsInChildren<Bienvenida360>(true))
                {
                    b.escenaSiguiente = siguiente;
                    if (b.sonido == null && rutaEscena == EscenaLogo360) AsegurarSonidoLogo(b); // viento del logo
                    EditorUtility.SetDirty(b);
                    ok = true;
                }
            if (ok) EditorSceneManager.SaveScene(s);
            EditorSceneManager.CloseScene(s, true);
            if (actual.IsValid()) SceneManager.SetActiveScene(actual);
            return ok;
        }
    }
}
