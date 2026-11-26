using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SendaVR
{
    /// <summary>
    /// Pone un modelo de mano en cada control VR ("Control Izquierdo" / "Control Derecho")
    /// y esconde el modelo del control. Funciona solo, en todas las escenas, y solo en VR.
    /// En PC no hace nada.
    /// </summary>
    public class ManosModeloVR : MonoBehaviour
    {
        static ManosModeloVR instancia;
        ConfigManosVR config;
        float proximaRevision;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            if (instancia != null) return;
            var cfg = Resources.Load<ConfigManosVR>("ConfigManosVR");
            if (cfg == null)
            {
                Debug.Log("[Senda] Manos VR: no encontré Resources/ConfigManosVR. Usa el menú Senda.vr/VR/Configurar manos VR.");
                return;
            }
            var go = new GameObject("ManosModeloVR");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<ManosModeloVR>();
            instancia.config = cfg;
        }

        void Update()
        {
            if (Time.unscaledTime < proximaRevision) return;
            proximaRevision = Time.unscaledTime + 0.5f;
            if (config == null || !config.usarManos) return;
            if (!MostrarSoloEnVR.VisorActivo()) return;
            Revisar("Control Izquierdo", false);
            Revisar("Control Derecho", true);
            RevisarLuz();
        }

        // ------------------------------------------------------------------ Escenas sin luces

        // Las escenas 360 no tienen luces: solo la esfera de video (que no usa luz) y el cielo.
        // La exposición automática se ajusta a la esfera y la mano, que sí recibe la luz física
        // del cielo, queda quemada de blanco. Aquí apagamos la luz del cielo sobre lo iluminado
        // (solo afecta a la mano: la esfera y los menús no usan luz) y ponemos dos luces suaves.
        GameObject luces;
        Light luzPrincipal, luzRelleno;

        void RevisarLuz()
        {
            if (luces != null)
            {
                if (!config.iluminarEnEscenasSinLuz) { Destroy(luces); luces = null; return; }
                luzPrincipal.intensity = config.luzPrincipal;
                luzRelleno.intensity = config.luzRelleno;
                return;
            }
            if (!config.iluminarEnEscenasSinLuz || EscenaTieneLuces()) return;

            luces = new GameObject("Luces de las manos (escena sin luz)");
            var volumen = luces.AddComponent<Volume>();
            volumen.isGlobal = true;
            volumen.priority = 1000f;
            var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
            var indirecta = perfil.Add<IndirectLightingController>(true);
            indirecta.indirectDiffuseLightingMultiplier.Override(0f);
            indirecta.reflectionLightingMultiplier.Override(0f);
            volumen.sharedProfile = perfil;

            luzPrincipal = CrearLuz("Luz principal", new Vector3(50f, -30f, 0f), config.luzPrincipal);
            luzRelleno = CrearLuz("Luz relleno", new Vector3(-40f, 150f, 0f), config.luzRelleno);
        }

        Light CrearLuz(string nombre, Vector3 angulos, float intensidad)
        {
            var go = new GameObject(nombre);
            go.transform.SetParent(luces.transform, false);
            go.transform.rotation = Quaternion.Euler(angulos);
            var luz = go.AddComponent<Light>();
            if (go.GetComponent<HDAdditionalLightData>() == null) go.AddComponent<HDAdditionalLightData>();
            luz.type = LightType.Directional;
            luz.shadows = LightShadows.None;
            luz.color = new Color(1f, 0.97f, 0.92f);
            luz.intensity = intensidad;
            return luz;
        }

        bool EscenaTieneLuces()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (l.enabled && l.intensity > 0f && (luces == null || !l.transform.IsChildOf(luces.transform)))
                    return true;
            return false;
        }

        void Revisar(string nombre, bool derecha)
        {
            var go = GameObject.Find(nombre);
            if (go == null) return;
            var control = go.transform;
            if (control.GetComponentInChildren<ManoVR>(true) != null) return;
            var modelo = derecha ? config.modeloDerecho : config.modeloIzquierdo;
            if (modelo == null) return;

            var mano = Instantiate(modelo, control, false);
            mano.name = derecha ? "Mano Derecha" : "Mano Izquierda";
            mano.AddComponent<ManoVR>().Iniciar(config, derecha, control);
        }
    }

    /// <summary>
    /// Una mano: se acomoda en el control y dobla los dedos según los botones.
    /// Grip → medio, anular y meñique. Gatillo → índice. Tocar stick / A / B → pulgar.
    /// </summary>
    public class ManoVR : MonoBehaviour
    {
        class Articulacion
        {
            public Transform t;
            public Quaternion reposo;
            public Vector3 eje;      // en el espacio del padre
            public float angulo;     // grados con el dedo cerrado del todo
        }

        class Dedo
        {
            public readonly List<Articulacion> partes = new List<Articulacion>();
            public float actual;
        }

        ConfigManosVR cfg;
        bool derecha;
        Transform control;
        Dedo pulgar, indice, medio, anular, menique;
        readonly List<Dedo> todos = new List<Dedo>();

        Quaternion rotacionBase = Quaternion.identity;
        Vector3 escalaOriginal = Vector3.one;
        Vector3 palmaLocal;            // palma en el espacio de la mano (con escala)
        bool listo;

        InputAction accionGrip, accionGatillo, accionToqueGatillo, accionToquePulgar;
        readonly List<GameObject> visualesControl = new List<GameObject>();

        public void Iniciar(ConfigManosVR config, bool esDerecha, Transform ctrl)
        {
            cfg = config;
            derecha = esDerecha;
            control = ctrl;

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            escalaOriginal = transform.localScale;

            if (!PrepararHuesos())
            {
                Debug.LogWarning($"[Senda] Manos VR: el modelo {name} no tiene los huesos esperados (L_Wrist, L_IndexProximal...).", this);
                return;
            }
            PrepararMaterial();
            CrearAcciones();
            if (cfg.ocultarControl) OcultarControl();
            listo = true;
            Acomodar();
            AplicarDedos(true);
        }

        // ------------------------------------------------------------------ Huesos

        bool PrepararHuesos()
        {
            string p = derecha ? "R_" : "L_";
            var huesos = new Dictionary<string, Transform>();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (!huesos.ContainsKey(t.name)) huesos.Add(t.name, t);

            Transform H(string n) => huesos.TryGetValue(p + n, out var hueso) ? hueso : null;

            var muneca = H("Wrist");
            var palma = H("Palm");
            var indiceBase = H("IndexProximal");
            var medioBase = H("MiddleProximal");
            var meniqueBase = H("LittleProximal");
            var pulgarBase = H("ThumbMetacarpal");
            if (muneca == null || indiceBase == null || medioBase == null || meniqueBase == null || pulgarBase == null)
                return false;

            // Ejes de la mano, en el espacio de este objeto (la raíz del modelo).
            Vector3 P(Transform x) => transform.InverseTransformPoint(x.position);
            Vector3 haciaDedos = (P(medioBase) - P(muneca)).normalized;
            Vector3 haciaPulgar = (P(indiceBase) - P(meniqueBase)).normalized;
            Vector3 cruz = Vector3.Cross(haciaDedos, haciaPulgar).normalized;
            Vector3 haciaPalma = derecha ? cruz : -cruz;

            palmaLocal = Vector3.Scale(transform.localScale,
                palma != null ? P(palma) : (P(muneca) + P(medioBase)) * 0.5f);

            // Base: la línea de nudillos (meñique → índice) va hacia adelante del control
            // y la palma mira hacia el mango (hacia adentro).
            Quaternion modelo = Quaternion.LookRotation(haciaPulgar, -haciaPalma);
            Vector3 palmaObjetivo = derecha ? Vector3.left : Vector3.right;
            Quaternion objetivo = Quaternion.LookRotation(Vector3.forward, -palmaObjetivo);
            rotacionBase = objetivo * Quaternion.Inverse(modelo);

            // Dedos
            indice = CrearDedo(huesos, p, "Index", haciaPalma, 0f);
            medio = CrearDedo(huesos, p, "Middle", haciaPalma, 0f);
            anular = CrearDedo(huesos, p, "Ring", haciaPalma, 3f);
            menique = CrearDedo(huesos, p, "Little", haciaPalma, 6f);

            // Pulgar: se dobla hacia la palma y hacia el índice.
            pulgar = new Dedo();
            string[] partesPulgar = { "ThumbMetacarpal", "ThumbProximal", "ThumbDistal", "ThumbTip" };
            float[] angulosPulgar = { 12f, 28f, 35f };
            for (int i = 0; i < 3; i++)
            {
                var t = H(partesPulgar[i]);
                var siguiente = H(partesPulgar[i + 1]);
                if (t == null || siguiente == null) continue;
                Vector3 hacia = (haciaPalma + (P(indiceBase) - P(t)).normalized * 0.7f).normalized;
                AgregarArticulacion(pulgar, t, siguiente, hacia, angulosPulgar[i]);
            }

            todos.Clear();
            foreach (var d in new[] { pulgar, indice, medio, anular, menique })
                if (d != null) todos.Add(d);
            return true;
        }

        Dedo CrearDedo(Dictionary<string, Transform> huesos, string p, string nombre, Vector3 haciaPalma, float anguloMetacarpo)
        {
            string[] partes = { "Metacarpal", "Proximal", "Intermediate", "Distal", "Tip" };
            float[] angulos = { anguloMetacarpo, 72f, 95f, 55f };
            var dedo = new Dedo();
            for (int i = 0; i < 4; i++)
            {
                huesos.TryGetValue(p + nombre + partes[i], out var t);
                huesos.TryGetValue(p + nombre + partes[i + 1], out var siguiente);
                if (t == null || siguiente == null || angulos[i] <= 0f) continue;
                AgregarArticulacion(dedo, t, siguiente, haciaPalma, angulos[i]);
            }
            return dedo;
        }

        void AgregarArticulacion(Dedo dedo, Transform t, Transform siguiente, Vector3 haciaLocal, float angulo)
        {
            if (t.parent == null) return;
            Vector3 hueso = (transform.InverseTransformPoint(siguiente.position) - transform.InverseTransformPoint(t.position)).normalized;
            Vector3 ejeLocal = Vector3.Cross(hueso, haciaLocal);
            if (ejeLocal.sqrMagnitude < 1e-6f) return;
            Vector3 ejeMundo = transform.TransformDirection(ejeLocal.normalized);
            dedo.partes.Add(new Articulacion
            {
                t = t,
                reposo = t.localRotation,
                eje = t.parent.InverseTransformDirection(ejeMundo).normalized,
                angulo = angulo
            });
        }

        // ------------------------------------------------------------------ Aspecto

        void PrepararMaterial()
        {
            Material mat = cfg.material;
            if (mat == null)
            {
                var shader = Shader.Find("HDRP/Lit");
                if (shader != null)
                {
                    mat = new Material(shader) { name = "Mano (runtime)" };
                    mat.SetColor("_BaseColor", new Color(0.87f, 0.72f, 0.62f));
                    mat.SetFloat("_Smoothness", 0.35f);
                }
            }

            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (mat != null)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                    r.sharedMaterials = mats;
                }
                r.shadowCastingMode = ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer s) s.updateWhenOffscreen = true;
            }
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        void OcultarControl()
        {
            foreach (var t in control.GetComponentsInChildren<Transform>(true))
            {
                if (t == control || t.IsChildOf(transform)) continue;
                bool esModelo = t.name.StartsWith("XR Controller") || t.name == "UniversalController"
                                || t.name.EndsWith("Controller Visual");
                if (esModelo && t.gameObject.activeSelf)
                {
                    t.gameObject.SetActive(false);
                    visualesControl.Add(t.gameObject);
                }
            }
        }

        // ------------------------------------------------------------------ Botones

        void CrearAcciones()
        {
            string h = derecha ? "{RightHand}" : "{LeftHand}";

            accionGrip = new InputAction("Mano grip", InputActionType.Value, $"<XRController>{h}/{{Grip}}", expectedControlType: "Axis");
            accionGatillo = new InputAction("Mano gatillo", InputActionType.Value, $"<XRController>{h}/{{Trigger}}", expectedControlType: "Axis");

            accionToqueGatillo = new InputAction("Mano toca gatillo", InputActionType.Button);
            accionToqueGatillo.AddBinding($"<XRController>{h}/triggerTouched");

            accionToquePulgar = new InputAction("Mano toca pulgar", InputActionType.Button);
            accionToquePulgar.AddBinding($"<XRController>{h}/thumbstickTouched");
            accionToquePulgar.AddBinding($"<XRController>{h}/{{Primary2DAxisTouch}}");
            accionToquePulgar.AddBinding($"<XRController>{h}/primaryTouched");
            accionToquePulgar.AddBinding($"<XRController>{h}/secondaryTouched");
            accionToquePulgar.AddBinding($"<XRController>{h}/thumbrestTouched");

            accionGrip.Enable();
            accionGatillo.Enable();
            accionToqueGatillo.Enable();
            accionToquePulgar.Enable();
        }

        void OnEnable()
        {
            accionGrip?.Enable();
            accionGatillo?.Enable();
            accionToqueGatillo?.Enable();
            accionToquePulgar?.Enable();
        }

        void OnDisable()
        {
            accionGrip?.Disable();
            accionGatillo?.Disable();
            accionToqueGatillo?.Disable();
            accionToquePulgar?.Disable();
        }

        void OnDestroy()
        {
            accionGrip?.Dispose();
            accionGatillo?.Dispose();
            accionToqueGatillo?.Dispose();
            accionToquePulgar?.Dispose();
            foreach (var v in visualesControl) if (v != null) v.SetActive(true);
        }

        // ------------------------------------------------------------------ Cada frame

        void LateUpdate()
        {
            if (!listo) return;

            if (!cfg.usarManos)
            {
                Destroy(gameObject);   // vuelve a mostrar el control
                return;
            }

            Acomodar();
            AplicarDedos(false);
        }

        void Acomodar()
        {
            Vector3 ajusteRot = cfg.ajusteRotacion;
            Vector3 ajustePos = cfg.ajustePosicion;
            if (!derecha)
            {
                ajusteRot = new Vector3(ajusteRot.x, -ajusteRot.y, -ajusteRot.z);
                ajustePos = new Vector3(-ajustePos.x, ajustePos.y, ajustePos.z);
            }

            float escala = Mathf.Max(0.01f, cfg.escala);
            transform.localScale = escalaOriginal * escala;

            Quaternion rot = Quaternion.Euler(ajusteRot) * rotacionBase;
            Vector3 palmaObjetivo = (derecha ? Vector3.right : Vector3.left) * cfg.separacionPalma + ajustePos;
            transform.localRotation = rot;
            transform.localPosition = palmaObjetivo - rot * (palmaLocal * escala);
        }

        void AplicarDedos(bool deInmediato)
        {
            float relajado = cfg.dedosRelajados;
            float grip = accionGrip != null ? accionGrip.ReadValue<float>() : 0f;
            float gatillo = accionGatillo != null ? accionGatillo.ReadValue<float>() : 0f;
            bool tocaGatillo = accionToqueGatillo != null && accionToqueGatillo.IsPressed();
            bool tocaPulgar = accionToquePulgar != null && accionToquePulgar.IsPressed();

            float cierre = Mathf.Lerp(relajado, 1f, grip);
            float objetivoIndice = Mathf.Max(tocaGatillo ? cfg.indiceTocandoGatillo : relajado, Mathf.Lerp(relajado, 1f, gatillo));
            float objetivoPulgar = tocaPulgar ? 1f : relajado;

            if (Sosteniendo())
            {
                float tope = cfg.cierreAlSostener;
                cierre = Mathf.Max(cierre, tope * 0.8f);
                cierre = Mathf.Min(cierre, tope);
                objetivoIndice = Mathf.Clamp(objetivoIndice, tope * 0.7f, tope);
                objetivoPulgar = Mathf.Max(objetivoPulgar, 0.6f);
            }

            float paso = deInmediato ? 1f : 1f - Mathf.Exp(-cfg.velocidadDedos * Time.deltaTime);
            Mover(pulgar, objetivoPulgar, paso);
            Mover(indice, objetivoIndice, paso);
            Mover(medio, cierre, paso);
            Mover(anular, cierre, paso);
            Mover(menique, cierre, paso);
        }

        static void Mover(Dedo dedo, float objetivo, float paso)
        {
            if (dedo == null) return;
            dedo.actual = Mathf.Lerp(dedo.actual, objetivo, paso);
            foreach (var a in dedo.partes)
                a.t.localRotation = Quaternion.AngleAxis(a.angulo * dedo.actual, a.eje) * a.reposo;
        }

        bool Sosteniendo()
        {
            var lista = AgarrableVR.Todos;
            for (int i = 0; i < lista.Count; i++)
            {
                var a = lista[i];
                if (a != null && a.Sostenido && (a.transform.position - control.position).sqrMagnitude < 0.35f * 0.35f)
                    return true;
            }
            return false;
        }
    }
}
