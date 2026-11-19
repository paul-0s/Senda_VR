using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace SendaVR.EditorTools
{
    /// <summary>
    /// Construye en la escena abierta todas las pantallas de Senda.vr
    /// (splash, términos, inicio de sesión y crear cuenta) con las medidas
    /// de Figma (1920 x 1080). Menú: Senda.vr > ...
    /// </summary>
    public static class ConstructorMenuSenda
    {
        const string Raiz = "Assets/SendaVR";
        const string CarpetaSprites = Raiz + "/Sprites";
        const string CarpetaVideo = Raiz + "/Video";
        const string NombreCanvas = "SendaVR_UI";

        static TMP_FontAsset fuente;

        // ------------------------------------------------------------------ menú

        [MenuItem("Senda.vr/1. Preparar imágenes (ejecutar primero)", priority = 1)]
        public static void PrepararImagenes()
        {
            ConfigurarImportadores();
            EditorUtility.DisplayDialog("Senda.vr",
                "Imágenes listas.\n\nAhora usa:\nSenda.vr > 2. Construir menú (pantalla de PC)\no\nSenda.vr > 3. Construir menú (VR)", "OK");
        }

        [MenuItem("Senda.vr/2. Construir menú (pantalla de PC)", priority = 2)]
        public static void ConstruirEscritorio() => Construir(false);

        [MenuItem("Senda.vr/3. Construir menú (VR)", priority = 3)]
        public static void ConstruirVR() => Construir(true);

        // ------------------------------------------------------------- construir

        static void Construir(bool modoVR)
        {
            ConfigurarImportadores();
            fuente = BuscarFuente();

            var existente = GameObject.Find(NombreCanvas);
            if (existente != null)
            {
                if (!EditorUtility.DisplayDialog("Senda.vr",
                        "Ya existe '" + NombreCanvas + "' en la escena. ¿Reemplazarlo?", "Reemplazar", "Cancelar"))
                    return;
                Undo.DestroyObjectImmediate(existente);
            }

            // Canvas
            var canvasGO = new GameObject(NombreCanvas, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasGO.layer = LayerMask.NameToLayer("UI");
            var canvas = canvasGO.GetComponent<Canvas>();
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            var crt = canvasGO.GetComponent<RectTransform>();

            if (modoVR)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                crt.sizeDelta = new Vector2(1920, 1080);
                crt.localScale = Vector3.one * 0.001f; // 1920 px = 1,92 m
                var cam = Camera.main;
                if (cam != null)
                {
                    var adelante = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
                    if (adelante.sqrMagnitude < 0.01f) adelante = Vector3.forward;
                    crt.position = new Vector3(cam.transform.position.x, Mathf.Max(cam.transform.position.y, 1.5f), cam.transform.position.z) + adelante * 2.2f;
                    crt.rotation = Quaternion.LookRotation(adelante, Vector3.up);
                    canvas.worldCamera = cam;
                }
                else
                {
                    crt.position = new Vector3(0f, 1.6f, 2.2f);
                }
                scaler.dynamicPixelsPerUnit = 3f;
                if (!AgregarPorNombre(canvasGO, "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit"))
                {
                    canvasGO.AddComponent<GraphicRaycaster>();
                    Debug.LogWarning("[Senda.vr] No encontré XR Interaction Toolkit. Instálalo (Package Manager) para apuntar al menú con los controles de VR. Mientras tanto funciona con el mouse.");
                }
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            CrearEventSystem(modoVR);

            var fondoNegro = Img("Fondo negro", crt, null, Color.black);
            Estirar(fondoNegro.rectTransform);
            fondoNegro.raycastTarget = false;

            // Video del fondo (opcional)
            Texture texturaVideo = PrepararVideo(crt, out var controlVideo);

            var contenedor = Nuevo("Pantallas", crt);
            Estirar(contenedor);

            var splash = CrearSplash(contenedor);
            var terminos = CrearTerminos(contenedor);
            var login = CrearLogin(contenedor, texturaVideo);
            var paso1 = CrearCuentaPaso1(contenedor, texturaVideo);
            var paso2 = CrearCuentaPaso2(contenedor, texturaVideo);

            // Conexiones entre pantallas
            splash.siguiente = terminos;
            terminos.siguiente = login;
            login.pantallaCrearCuenta = paso1;
            login.videoFondo = controlVideo;
            paso1.pantallaAnterior = login; paso1.pantallaSiguiente = paso2; paso1.login = login;
            paso2.pantallaAnterior = paso1; paso2.login = login; paso2.paso1 = paso1;

            // En el editor solo se deja visible el splash (las demás se activan al dar Play)
            foreach (var otra in new PantallaBase[] { terminos, login, paso1, paso2 })
                otra.gameObject.SetActive(false);

            var nav = canvasGO.AddComponent<NavegadorPantallas>();
            nav.pantallaInicial = splash;
            nav.pantallas = new PantallaBase[] { splash, terminos, login, paso1, paso2 };

            Undo.RegisterCreatedObjectUndo(canvasGO, "Construir menú Senda.vr");
            Selection.activeGameObject = canvasGO;
            EditorSceneManager.MarkSceneDirty(canvasGO.scene);

            string faltantes = Faltantes();
            EditorUtility.DisplayDialog("Senda.vr",
                "Menú construido" + (modoVR ? " para VR." : " para pantalla de PC.") +
                "\n\nDale Play para probarlo." +
                (faltantes.Length > 0 ? "\n\nAún usas versiones provisorias de:\n" + faltantes + "\nExpórtalos desde Figma (ver la guía) y vuelve a construir." : ""),
                "OK");
        }

        // ---------------------------------------------------------------- splash

        static PantallaSplash CrearSplash(RectTransform padre)
        {
            var p = Pantalla<PantallaSplash>("01 Splash (logo animado)", padre);
            var rt = p.GetComponent<RectTransform>();

            var fondo = Img("Fondo", rt, Spr("fondo_splash"), Color.white);
            Estirar(fondo.rectTransform);

            // Logo
            Sprite logoFinal = Spr("logo_senda");
            Sprite logo = logoFinal != null ? logoFinal : Spr("logo_senda_PROVISORIO");
            var logoImg = Img("Logo", rt, logo, logoFinal != null ? Color.white : SendaEstilo.NaranjaLogo);
            logoImg.preserveAspect = true;
            Centro(logoImg.rectTransform, 0, 0, 233, 307);
            var logoGrupo = logoImg.gameObject.AddComponent<CanvasGroup>();

            var txtGO = Nuevo("Texto Senda.vr", rt);
            Centro(txtGO, 0, -115, 500, 80);
            var txt = Txt("Texto", txtGO, "Senda.vr", 52, SendaEstilo.TextoOscuro, true, TextAlignmentOptions.Center);
            Estirar(txt.rectTransform);
            var txtGrupo = txtGO.gameObject.AddComponent<CanvasGroup>();

            // Video del logo (si existe logo_animacion.mp4)
            var clipLogo = BuscarVideoLogo();
            if (clipLogo != null)
            {
                string rutaRT = CarpetaVideo + "/LogoVideo_RT.renderTexture";
                var rtex = AssetDatabase.LoadAssetAtPath<RenderTexture>(rutaRT);
                if (rtex == null)
                {
                    rtex = new RenderTexture((int)clipLogo.width, (int)clipLogo.height, 0) { name = "LogoVideo_RT" };
                    AssetDatabase.CreateAsset(rtex, rutaRT);
                }
                var raw = Nuevo("Video logo", rt).gameObject.AddComponent<RawImage>();
                raw.texture = rtex;
                raw.raycastTarget = false;
                Estirar(raw.rectTransform);
                var ajuste = raw.gameObject.AddComponent<AspectRatioFitter>();
                ajuste.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                ajuste.aspectRatio = clipLogo.height > 0 ? (float)clipLogo.width / clipLogo.height : 16f / 9f;
                p.videoLogo = CrearReproductor(raw.rectTransform, "Reproductor", clipLogo, rtex, false);
                p.capaVideo = raw.gameObject;
            }

            p.logo = logoImg.rectTransform;
            p.logoGrupo = logoGrupo;
            p.texto = txtGO;
            p.textoGrupo = txtGrupo;
            return p;
        }

        // -------------------------------------------------------------- términos

        static PantallaTerminos CrearTerminos(RectTransform padre)
        {
            var p = Pantalla<PantallaTerminos>("02 Terminos y condiciones", padre);
            var rt = p.GetComponent<RectTransform>();

            // Fondo degradado animado (3 variantes del componente "Home")
            var fondoGO = Nuevo("Fondo degradado animado", rt);
            Estirar(fondoGO);
            var abajo = Img("Capa abajo", fondoGO, Spr("fondo_gradiente_1"), Color.white); Estirar(abajo.rectTransform);
            var arriba = Img("Capa arriba", fondoGO, Spr("fondo_gradiente_2"), Color.white); Estirar(arriba.rectTransform);
            abajo.raycastTarget = arriba.raycastTarget = false;
            var anim = fondoGO.gameObject.AddComponent<FondoGradienteAnimado>();
            anim.capaAbajo = abajo; anim.capaArriba = arriba;
            anim.variantes = new[] { Spr("fondo_gradiente_1"), Spr("fondo_gradiente_2"), Spr("fondo_gradiente_3") }
                .Where(s => s != null).ToArray();

            // Panel (componente "terminos y condiciones": 1248 de ancho, esquina superior en y = 181)
            var panel = Panel("Panel terminos", rt, 336, 181, 1248, 730);
            var titulo = Txt("Titulo", panel, TextosSenda.TituloTerminos, 32, SendaEstilo.Blanco, true, TextAlignmentOptions.TopLeft);
            Figma(titulo.rectTransform, 79, 60, 1090, 46);
            var intro = Txt("Introduccion", panel, TextosSenda.IntroTerminos, 20, SendaEstilo.Blanco, true, TextAlignmentOptions.TopJustified);
            Figma(intro.rectTransform, 79, 126, 1090, 58);

            var zonaTexto = Nuevo("Seccion", panel);
            Figma(zonaTexto, 79, 206, 1090, 312);
            var grupoTexto = zonaTexto.gameObject.AddComponent<CanvasGroup>();
            var seccion = Txt("Texto seccion", zonaTexto, TextosSenda.SeccionesTerminos[0], 20, SendaEstilo.Blanco, false, TextAlignmentOptions.TopJustified);
            seccion.enableAutoSizing = true; seccion.fontSizeMin = 16; seccion.fontSizeMax = 20;
            Estirar(seccion.rectTransform);

            // Paginación: flechas circulares + puntos (centrados en el panel)
            int n = TextosSenda.SeccionesTerminos.Length;
            var puntos = new Image[n];
            const float tamPunto = 18f, pasoPunto = 31f, tamFlecha = 38f;
            float anchoPuntos = (n - 1) * pasoPunto + tamPunto;
            float inicioPuntos = 624f - anchoPuntos / 2f;
            for (int i = 0; i < n; i++)
            {
                var d = Img("Punto " + (i + 1), panel, Spr("circulo"), SendaEstilo.Gris);
                Figma(d.rectTransform, inicioPuntos + i * pasoPunto, 540, tamPunto, tamPunto);
                var b = d.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                puntos[i] = d;
            }
            var flecha = Spr("flecha_circulo") ?? Spr("icono_volver");
            var anterior = BotonFlecha("Anterior", panel, flecha, inicioPuntos - 25.5f - tamFlecha, 530, tamFlecha, false);
            var siguiente = BotonFlecha("Siguiente", panel, flecha, inicioPuntos + anchoPuntos + 25.5f, 530, tamFlecha, true);

            // Casilla de aceptación (solo en la última página)
            var fila = Nuevo("Casilla aceptar", panel);
            Figma(fila, 248, 595, 760, 40);
            var casilla = Casilla(fila, TextosSenda.TextoCasilla);

            // Botones Salir / Aceptar (dentro del panel, aparecen al marcar la casilla)
            var zonaBotones = Nuevo("Botones Salir y Aceptar", panel);
            Estirar(zonaBotones);
            var grupoBotones = zonaBotones.gameObject.AddComponent<CanvasGroup>();
            grupoBotones.alpha = 0f;
            grupoBotones.blocksRaycasts = false;
            var salir = Boton("Boton Salir", zonaBotones, 344, 680, 252, 63, "Salir", SendaEstilo.Gris, SendaEstilo.NaranjaIntenso, 23);
            var aceptar = Boton("Boton Aceptar terminos", zonaBotones, 652, 680, 252, 63, "Aceptar", SendaEstilo.Gris, SendaEstilo.NaranjaIntenso, 23);
            aceptar.GetComponent<BotonAnimado>().colorDeshabilitado = SendaEstilo.Gris;

            p.secciones = (string[])TextosSenda.SeccionesTerminos.Clone();
            p.textoSeccion = seccion;
            p.grupoTexto = grupoTexto;
            p.puntos = puntos;
            p.botonAnterior = anterior;
            p.botonSiguiente = siguiente;
            p.filaCasilla = fila.gameObject;
            p.casilla = casilla;
            p.botonAceptar = aceptar;
            p.botonSalir = salir;
            p.panel = panel;
            p.grupoBotones = grupoBotones;
            return p;
        }

        // ----------------------------------------------------------------- login

        static PantallaLogin CrearLogin(RectTransform padre, Texture video)
        {
            var p = Pantalla<PantallaLogin>("03 Inicio de sesion", padre);
            var rt = p.GetComponent<RectTransform>();

            FondoFoto(rt, video, null, "fondo_login", "fondo_login_provisorio");
            var osc = Img("Velo", rt, null, SendaEstilo.Velo);
            Estirar(osc.rectTransform);
            osc.raycastTarget = false;
            var oscGrupo = osc.gameObject.AddComponent<CanvasGroup>();

            var panel = Panel("Panel login", rt, 336, 171, 1248, 737);
            var panelGrupo = panel.gameObject.AddComponent<CanvasGroup>();

            TituloCentrado(panel, "¡Iniciemos!", "Por favor ingresa tu cuenta para continuar", 69, 115);

            var correo = Campo("Campo correo", panel, 225, 179, 806, 74, Spr("icono_correo"), "Correo electrónico", TMP_InputField.ContentType.EmailAddress);
            var clave = Campo("Campo contrasena", panel, 225, 282, 806, 74, Spr("icono_candado"), "Contraseña", TMP_InputField.ContentType.Password);

            var iniciar = Boton("Boton Iniciar sesion", panel, 232, 412, 384, 74, "Iniciar sesión", SendaEstilo.Naranja, SendaEstilo.NaranjaIntenso, SendaEstilo.TamBoton);
            var crear = Boton("Boton Crear cuenta", panel, 647, 412, 384, 74, "Crear cuenta", SendaEstilo.Azul, SendaEstilo.Azul * 1.12f, SendaEstilo.TamBoton);

            var mensaje = Txt("Mensaje", panel, "", 21, SendaEstilo.Blanco, false, TextAlignmentOptions.Center);
            Figma(mensaje.rectTransform, 0, 496, 1248, 40);

            var linea = Img("Linea divisoria", panel, null, new Color(1, 1, 1, 0.45f));
            Figma(linea.rectTransform, 226, 550, 803, 2);
            linea.raycastTarget = false;

            var sociales = new[]
            {
                BotonSocial("Boton Google", panel, "logo_google", "G", 337, 585, 70, 70),
                BotonSocial("Boton Oculus", panel, "logo_oculus", "oculus", 518, 597, 213, 46),
                BotonSocial("Boton Apple", panel, "logo_apple", "Apple", 842, 579, 57, 68),
            };

            p.panel = panel;
            p.panelGrupo = panelGrupo;
            p.oscurecer = oscGrupo;
            p.campoCorreo = correo;
            p.campoContrasena = clave;
            p.botonIniciar = iniciar;
            p.botonCrearCuenta = crear;
            p.botonesSociales = sociales;
            p.mensaje = mensaje;
            return p;
        }

        // ---------------------------------------------------------- crear cuenta

        static PantallaCrearCuenta CrearCuentaBase(string nombre, RectTransform padre, Texture video, out RectTransform panel)
        {
            var p = Pantalla<PantallaCrearCuenta>(nombre, padre);
            var rt = p.GetComponent<RectTransform>();
            FondoFoto(rt, video, "fondo_crear_cuenta", "fondo_login", "fondo_login_provisorio");
            var osc = Img("Velo", rt, null, SendaEstilo.Velo);
            Estirar(osc.rectTransform);
            osc.raycastTarget = false;
            // Segunda capa (Rectangle 234): más oscura hacia abajo
            var oscAbajo = Img("Velo inferior", rt, Spr("velo_degradado"), SendaEstilo.VeloInferior);
            Estirar(oscAbajo.rectTransform, 0, 0, 0, 250);
            oscAbajo.raycastTarget = false;

            panel = Panel("Panel crear cuenta", rt, 336, 159, 1248, 763);
            TituloCentrado(panel, "¡Rellena tus datos!", "Completa los datos para crear tu cuenta", 83, 142);

            var volver = BotonFlecha("Boton volver", panel, Spr("flecha_circulo") ?? Spr("icono_volver"), 35, 53, 46, false);
            var coloresVolver = volver.colors;
            coloresVolver.normalColor = SendaEstilo.GrisClaro;
            volver.colors = coloresVolver;

            p.panel = panel;
            p.botonVolver = volver;
            return p;
        }

        static PantallaCrearCuenta CrearCuentaPaso1(RectTransform padre, Texture video)
        {
            var p = CrearCuentaBase("04 Crear cuenta - datos", padre, video, out var panel);
            p.paso = PantallaCrearCuenta.Paso.DatosPersonales;

            p.campoCorreo = Campo("Campo correo", panel, 221, 191, 806, 74, Spr("icono_correo"), "Correo electrónico", TMP_InputField.ContentType.EmailAddress);
            p.campoNombre = Campo("Campo nombre", panel, 221, 294, 806, 74, Spr("icono_persona"), "Nombre completo", TMP_InputField.ContentType.Name);
            p.campoFecha = Campo("Campo fecha", panel, 221, 397, 806, 74, Spr("icono_calendario"), "Fecha de nacimiento (DD/MM/AAAA)", TMP_InputField.ContentType.Standard);
            p.campoTelefono = Campo("Campo telefono", panel, 221, 500, 806, 74, Spr("icono_telefono"), "Teléfono", TMP_InputField.ContentType.IntegerNumber);

            p.mensaje = Txt("Mensaje", panel, "", 20, SendaEstilo.Error, false, TextAlignmentOptions.Center);
            Figma(p.mensaje.rectTransform, 0, 579, 1248, 38);

            p.botonCancelar = Boton("Boton Cancelar", panel, 228, 622, 384, 74, "Cancelar", SendaEstilo.Azul, SendaEstilo.Azul * 1.12f, SendaEstilo.TamBoton);
            p.botonContinuar = Boton("Boton Crear cuenta", panel, 643, 622, 384, 74, "Crear cuenta", SendaEstilo.Naranja, SendaEstilo.NaranjaIntenso, SendaEstilo.TamBoton);
            return p;
        }

        static PantallaCrearCuenta CrearCuentaPaso2(RectTransform padre, Texture video)
        {
            var p = CrearCuentaBase("05 Crear cuenta - contrasena", padre, video, out var panel);
            p.paso = PantallaCrearCuenta.Paso.Contrasena;

            p.campoContrasena = Campo("Campo contrasena", panel, 221, 191, 806, 74, Spr("icono_candado"), "Contraseña", TMP_InputField.ContentType.Password, 60);
            p.campoRepetir = Campo("Campo repetir contrasena", panel, 221, 307, 806, 74, Spr("icono_candado"), "Repite tu contraseña", TMP_InputField.ContentType.Password, 60);
            BotonOjo(p.campoContrasena);
            BotonOjo(p.campoRepetir);

            var lbl = Txt("Recuerdame", panel, "Recuérdame:", 24, SendaEstilo.TextoEtiqueta, false, TextAlignmentOptions.Left);
            Figma(lbl.rectTransform, 221, 404, 300, 33);
            p.recuerdame = Interruptor(panel, 957, 407, 59, 27, false);

            p.mensaje = Txt("Mensaje", panel, "", 20, SendaEstilo.Error, false, TextAlignmentOptions.Center);
            Figma(p.mensaje.rectTransform, 0, 456, 1248, 40);

            p.botonContinuar = Boton("Boton Crear cuenta", panel, 226, 512, 797, 74, "Crear Cuenta", SendaEstilo.Naranja, SendaEstilo.NaranjaIntenso, SendaEstilo.TamBoton);
            p.botonCancelar = Boton("Boton Cancelar", panel, 226, 609, 797, 74, "Cancelar", SendaEstilo.Azul, SendaEstilo.Azul * 1.12f, SendaEstilo.TamBoton);
            return p;
        }

        // ============================================================ piezas UI

        static RectTransform Nuevo(string nombre, Transform padre)
        {
            var go = new GameObject(nombre, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            go.transform.SetParent(padre, false);
            return go.GetComponent<RectTransform>();
        }

        static T Pantalla<T>(string nombre, RectTransform padre) where T : PantallaBase
        {
            var rt = Nuevo(nombre, padre);
            Estirar(rt);
            rt.gameObject.AddComponent<CanvasGroup>();
            return rt.gameObject.AddComponent<T>();
        }

        /// <summary>Posición igual que en Figma: x, y desde la esquina superior izquierda del padre.</summary>
        static void Figma(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Centro(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void Estirar(RectTransform rt, float izq = 0, float abajo = 0, float der = 0, float arriba = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(izq, abajo);
            rt.offsetMax = new Vector2(-der, -arriba);
        }

        static Image Img(string nombre, Transform padre, Sprite sprite, Color color)
        {
            var img = Nuevo(nombre, padre).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            if (sprite != null && sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
            return img;
        }

        static TextMeshProUGUI Txt(string nombre, Transform padre, string texto, float tam, Color color, bool negrita, TextAlignmentOptions alin)
        {
            var t = Nuevo(nombre, padre).gameObject.AddComponent<TextMeshProUGUI>();
            if (fuente != null) t.font = fuente;
            t.text = texto;
            t.fontSize = tam;
            t.color = color;
            t.fontStyle = negrita ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = alin;
            t.raycastTarget = false;
            return t;
        }

        static RectTransform Panel(string nombre, RectTransform padre, float x, float y, float w, float h)
        {
            var img = Img(nombre, padre, Spr("panel_redondeado"), SendaEstilo.Panel);
            Figma(img.rectTransform, x, y, w, h);
            return img.rectTransform;
        }

        static void TituloCentrado(RectTransform panel, string titulo, string subtitulo, float yTitulo, float ySub)
        {
            var t = Txt("Titulo", panel, titulo, SendaEstilo.TamTitulo, SendaEstilo.Blanco, true, TextAlignmentOptions.Center);
            Figma(t.rectTransform, 0, yTitulo - 34, panel.sizeDelta.x, 68);
            var s = Txt("Subtitulo", panel, subtitulo, SendaEstilo.TamSubtitulo, SendaEstilo.Blanco, false, TextAlignmentOptions.Center);
            Figma(s.rectTransform, 0, ySub - 18, panel.sizeDelta.x, 36);
        }

        static Button Boton(string nombre, RectTransform padre, float x, float y, float w, float h, string texto, Color normal, Color hover, float tamTexto)
        {
            var img = Img(nombre, padre, Spr("pildora"), normal);
            Figma(img.rectTransform, x, y, w, h);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.transition = Selectable.Transition.None;
            var anim = img.gameObject.AddComponent<BotonAnimado>();
            anim.fondo = img;
            anim.colorNormal = normal;
            hover.a = 1f;
            anim.colorHover = hover;
            var t = Txt("Texto", img.rectTransform, texto, tamTexto, SendaEstilo.Blanco, true, TextAlignmentOptions.Center);
            Estirar(t.rectTransform, 12, 0, 12, 0);
            return b;
        }

        static Button BotonIcono(string nombre, RectTransform padre, Sprite icono, float x, float y, float tam, bool espejo)
        {
            var img = Img(nombre, padre, icono, Color.white);
            img.preserveAspect = true;
            Figma(img.rectTransform, x, y, tam, tam);
            if (espejo)
            {
                // girar 180° alrededor del centro para que la flecha apunte a la derecha
                img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                img.rectTransform.anchoredPosition = new Vector2(x + tam / 2f, -y - tam / 2f);
                img.rectTransform.localScale = new Vector3(-1, 1, 1);
            }
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colores = b.colors;
            colores.disabledColor = new Color(1, 1, 1, 0.25f);
            colores.highlightedColor = new Color(1f, 0.85f, 0.7f);
            b.colors = colores;
            return b;
        }

        /// <summary>Botón circular con flecha (Group 67 de Figma). El sprite apunta a la izquierda.</summary>
        static Button BotonFlecha(string nombre, RectTransform padre, Sprite sprite, float x, float y, float tam, bool haciaDerecha)
        {
            var img = Img(nombre, padre, sprite, Color.white);
            img.preserveAspect = true;
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0, 1);
            img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            img.rectTransform.anchoredPosition = new Vector2(x + tam / 2f, -y - tam / 2f);
            img.rectTransform.sizeDelta = new Vector2(tam, tam);
            if (haciaDerecha) img.rectTransform.localScale = new Vector3(-1, 1, 1);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var colores = b.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1f, 0.85f, 0.7f);
            colores.pressedColor = new Color(1f, 0.7f, 0.45f);
            colores.selectedColor = Color.white;
            colores.disabledColor = SendaEstilo.Gris;
            colores.fadeDuration = 0.12f;
            b.colors = colores;
            return b;
        }

        static Button BotonSocial(string nombre, RectTransform padre, string sprite, string textoAlternativo, float x, float y, float w, float h)
        {
            var spr = Spr(sprite);
            Image img;
            if (spr != null)
            {
                img = Img(nombre, padre, spr, Color.white);
                img.preserveAspect = true;
            }
            else
            {
                img = Img(nombre, padre, null, new Color(1, 1, 1, 0.001f)); // área de click invisible
                var t = Txt("Texto (provisorio)", img.rectTransform, textoAlternativo, 34, SendaEstilo.Blanco, true, TextAlignmentOptions.Center);
                t.enableAutoSizing = true; t.fontSizeMin = 14; t.fontSizeMax = 34;
                Estirar(t.rectTransform);
            }
            Figma(img.rectTransform, x, y, w, h);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var anim = img.gameObject.AddComponent<BotonAnimado>();
            anim.fondo = img;
            anim.colorNormal = anim.colorHover = anim.colorDeshabilitado = img.color;
            return b;
        }

        static TMP_InputField Campo(string nombre, RectTransform padre, float x, float y, float w, float h, Sprite icono,
                                    string placeholder, TMP_InputField.ContentType tipo, float espacioDerecha = 0)
        {
            var bg = Img(nombre, padre, Spr("pildora_borde"), SendaEstilo.BordeCampo);
            Figma(bg.rectTransform, x, y, w, h);

            if (icono != null)
            {
                var ic = Img("Icono", bg.rectTransform, icono, Color.white);
                ic.preserveAspect = true;
                ic.raycastTarget = false;
                Figma(ic.rectTransform, 29, (h - 32) / 2f, 32, 32);
            }
            var div = Img("Divisor", bg.rectTransform, null, new Color(1, 1, 1, 0.6f));
            div.raycastTarget = false;
            Figma(div.rectTransform, 82, (h - 43.5f) / 2f, 2, 43.5f);

            var area = Nuevo("Area de texto", bg.rectTransform);
            Estirar(area, 100, 6, 30 + espacioDerecha, 6);
            area.gameObject.AddComponent<RectMask2D>();

            var ph = Txt("Placeholder", area, placeholder, 24, SendaEstilo.TextoSuave, false, TextAlignmentOptions.MidlineLeft);
            Estirar(ph.rectTransform);
            ph.fontStyle = FontStyles.Normal;
            var txt = Txt("Texto", area, "", 24, SendaEstilo.Blanco, false, TextAlignmentOptions.MidlineLeft);
            Estirar(txt.rectTransform);

            var input = bg.gameObject.AddComponent<TMP_InputField>();
            input.targetGraphic = bg;
            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = tipo;
            input.asteriskChar = '•';
            input.customCaretColor = true;
            input.caretColor = SendaEstilo.Naranja;
            input.caretWidth = 3;
            input.selectionColor = new Color(0.95f, 0.5f, 0.03f, 0.4f);
            if (fuente != null) input.fontAsset = fuente;
            var colores = input.colors;
            colores.normalColor = Color.white;
            colores.highlightedColor = new Color(1f, 0.9f, 0.8f);
            colores.selectedColor = new Color(1f, 0.75f, 0.5f);
            input.colors = colores;
            return input;
        }

        static void BotonOjo(TMP_InputField campo)
        {
            var rt = campo.GetComponent<RectTransform>();
            var img = Img("Boton ojo", rt, Spr("icono_ojo_tachado"), new Color(1, 1, 1, 0.8f));
            img.preserveAspect = true;
            Figma(img.rectTransform, 748, (rt.sizeDelta.y - 30) / 2f, 30, 30);
            var b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var m = img.gameObject.AddComponent<MostrarContrasena>();
            m.campo = campo;
            m.icono = img;
            m.ojoAbierto = Spr("icono_ojo");
            m.ojoTachado = Spr("icono_ojo_tachado");
        }

        static Toggle Casilla(RectTransform fila, string texto)
        {
            var toggle = fila.gameObject.AddComponent<Toggle>();
            var caja = Img("Caja", fila, Spr("casilla_borde"), Color.white);
            Figma(caja.rectTransform, 0, 5, 30, 30);
            var marca = Img("Marcada", caja.rectTransform, Spr("casilla_relleno"), SendaEstilo.GrisClaro);
            Estirar(marca.rectTransform);
            marca.raycastTarget = false;
            var check = Img("Check", marca.rectTransform, Spr("icono_check"), SendaEstilo.TextoOscuro);
            Estirar(check.rectTransform, 5, 5, 5, 5);
            check.raycastTarget = false;
            var t = Txt("Texto", fila, texto, 21, SendaEstilo.Blanco, false, TextAlignmentOptions.MidlineLeft);
            t.raycastTarget = true; // para poder hacer click en el texto
            Figma(t.rectTransform, 46, 0, 720, 40);

            toggle.targetGraphic = caja;
            toggle.isOn = false;
            var vis = fila.gameObject.AddComponent<InterruptorVisual>();
            vis.marca = marca.gameObject;
            marca.gameObject.SetActive(false);
            return toggle;
        }

        static Toggle Interruptor(RectTransform padre, float x, float y, float w, float h, bool encendido)
        {
            var fondo = Img("Interruptor Recuerdame", padre, Spr("pildora"), encendido ? SendaEstilo.Naranja : SendaEstilo.CremaInterruptor);
            Figma(fondo.rectTransform, x, y, w, h);
            var perilla = Img("Perilla", fondo.rectTransform, Spr("circulo"), encendido ? Color.white : SendaEstilo.NaranjaPerilla);
            Centro(perilla.rectTransform, encendido ? 16 : -16, 0, h - 6, h - 6);
            perilla.raycastTarget = false;
            var toggle = fondo.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = fondo;
            toggle.isOn = encendido;
            var vis = fondo.gameObject.AddComponent<InterruptorVisual>();
            vis.fondo = fondo;
            vis.perilla = perilla.rectTransform;
            vis.recorrido = w - h;
            vis.colorApagado = SendaEstilo.CremaInterruptor;     // como en Figma (Component 2)
            vis.colorEncendido = SendaEstilo.Naranja;
            vis.perillaImagen = perilla;
            vis.perillaApagada = SendaEstilo.NaranjaPerilla;
            vis.perillaEncendida = Color.white;
            return toggle;
        }

        /// <summary>
        /// Fondo de pantalla: usa la foto "especifico" si existe; si no, el video;
        /// si no, la primera imagen de "alternativas" que encuentre.
        /// </summary>
        static void FondoFoto(RectTransform padre, Texture video, string especifico, params string[] alternativas)
        {
            Sprite s = especifico != null ? Spr(especifico) : null;
            if (s == null && video != null)
            {
                var raw = Nuevo("Fondo (video)", padre).gameObject.AddComponent<RawImage>();
                raw.texture = video;
                raw.raycastTarget = false;
                Estirar(raw.rectTransform);
                return;
            }
            for (int i = 0; s == null && i < alternativas.Length; i++) s = Spr(alternativas[i]);
            var img = Img("Fondo (imagen)", padre, s, Color.white);
            img.raycastTarget = false;
            Estirar(img.rectTransform);
        }

        // ============================================================ recursos

        static bool EsVideoLogo(VideoClip c) => c.name == "logo_animacion";

        static VideoClip BuscarVideoLogo()
        {
            if (!AssetDatabase.IsValidFolder(CarpetaVideo)) return null;
            return AssetDatabase.FindAssets("t:VideoClip", new[] { CarpetaVideo })
                .Select(g => AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(c => c != null && EsVideoLogo(c));
        }

        /// <summary>
        /// Busca los MP4 del menú en Assets/SendaVR/Video:
        ///  - "menu_entrada": se reproduce una vez y luego entra el panel.
        ///  - "menu_bucle": se repite en bucle detrás del panel.
        /// Si hay un solo video (con cualquier nombre), se usa como bucle.
        /// </summary>
        static Texture PrepararVideo(RectTransform canvas, out ControlVideoFondo control)
        {
            control = null;
            if (!AssetDatabase.IsValidFolder(CarpetaVideo)) return null;
            var clips = AssetDatabase.FindAssets("t:VideoClip", new[] { CarpetaVideo })
                .Select(g => AssetDatabase.LoadAssetAtPath<VideoClip>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c => c != null).ToList();
            if (clips.Count == 0) return null;

            clips = clips.Where(c => !EsVideoLogo(c)).ToList();
            if (clips.Count == 0) return null;
            VideoClip intro = clips.FirstOrDefault(c => c.name == "menu_entrada" || c.name == "fondo_login_intro");
            VideoClip bucle = clips.FirstOrDefault(c => c.name == "menu_bucle" || c.name == "fondo_login")
                              ?? clips.FirstOrDefault(c => c != intro);
            if (bucle == null) { bucle = intro; intro = null; } // solo existe la intro: se usa como bucle

            string rutaRT = CarpetaVideo + "/FondoVideo_RT.renderTexture";
            var rtex = AssetDatabase.LoadAssetAtPath<RenderTexture>(rutaRT);
            if (rtex == null)
            {
                rtex = new RenderTexture(1920, 1080, 0) { name = "FondoVideo_RT" };
                AssetDatabase.CreateAsset(rtex, rutaRT);
            }

            var go = Nuevo("Video fondo login", canvas);
            control = go.gameObject.AddComponent<ControlVideoFondo>();
            control.bucle = CrearReproductor(go, "Bucle (animacion-2)", bucle, rtex, true);
            if (intro != null) control.intro = CrearReproductor(go, "Intro (animacion-1)", intro, rtex, false);
            return rtex;
        }

        static VideoPlayer CrearReproductor(RectTransform padre, string nombre, VideoClip clip, RenderTexture rt, bool enBucle)
        {
            var vp = Nuevo(nombre, padre).gameObject.AddComponent<VideoPlayer>();
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.isLooping = enBucle;
            vp.playOnAwake = false;
            vp.waitForFirstFrame = true;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            return vp;
        }

        static Sprite Spr(string nombre)
        {
            foreach (var guid in AssetDatabase.FindAssets(nombre + " t:Texture2D", new[] { CarpetaSprites }))
            {
                var ruta = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(ruta) == nombre)
                    return AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
            }
            return null;
        }

        static string Faltantes()
        {
            string r = "";
            if (Spr("logo_senda") == null) r += "• Logo (logo_senda.png)\n";
            bool hayVideo = AssetDatabase.IsValidFolder(CarpetaVideo) && AssetDatabase.FindAssets("t:VideoClip", new[] { CarpetaVideo }).Length > 0;
            if (!hayVideo && Spr("fondo_login") == null) r += "• Videos del logo y del menú (.mp4)\n";
            if (Spr("fondo_crear_cuenta") == null) r += "• Fondo de crear cuenta (fondo_crear_cuenta.jpg)\n";
            if (Spr("logo_google") == null) r += "• Logos Oculus / Google / Apple\n";
            return r;
        }

        static void ConfigurarImportadores()
        {
            if (!AssetDatabase.IsValidFolder(CarpetaSprites)) return;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { CarpetaSprites }))
            {
                var ruta = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(ruta) as TextureImporter;
                if (imp == null) continue;
                string n = Path.GetFileNameWithoutExtension(ruta);

                Vector4 borde = Vector4.zero;
                float ppu = 100;
                switch (n)
                {
                    case "pildora":
                    case "pildora_borde": borde = new Vector4(73, 73, 73, 73); ppu = 200; break;
                    case "panel_redondeado": borde = new Vector4(44, 44, 44, 44); ppu = 150; break;
                    case "casilla_borde":
                    case "casilla_relleno": borde = new Vector4(18, 18, 18, 18); break;
                }

                bool cambio = imp.textureType != TextureImporterType.Sprite
                              || imp.spriteBorder != borde
                              || !Mathf.Approximately(imp.spritePixelsPerUnit, ppu)
                              || imp.mipmapEnabled;
                if (!cambio) continue;

                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.spriteBorder = borde;
                imp.spritePixelsPerUnit = ppu;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
        }

        static TMP_FontAsset BuscarFuente()
        {
            var guid = AssetDatabase.FindAssets("Nunito t:TMP_FontAsset").FirstOrDefault();
            if (guid == null)
            {
                Debug.Log("[Senda.vr] No encontré una fuente TMP 'Nunito'. Se usa la fuente por defecto. Mira la guía para crearla.");
                return null;
            }
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
        }

        static void CrearEventSystem(bool modoVR)
        {
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(go, "EventSystem");
            if (modoVR && AgregarPorNombre(go, "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit")) return;
            if (AgregarPorNombre(go, "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem")) return;
            go.AddComponent<StandaloneInputModule>();
        }

        static bool AgregarPorNombre(GameObject go, string tipoCompleto)
        {
            var tipo = Type.GetType(tipoCompleto);
            if (tipo == null) return false;
            go.AddComponent(tipo);
            return true;
        }
    }
}
