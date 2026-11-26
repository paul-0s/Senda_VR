using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace SendaVR
{
    // =====================================================================================
    //  Interacción del Cuarto en VR (en PC no cambia nada: todo esto solo se activa con visor)
    //
    //  - Los objetos tomables (mochila, botella, zapatillas, palo) se AGARRAN con la mano
    //    (botón de agarre / grip), tocándolos o apuntándolos con el rayo (vienen a tu mano).
    //    Mientras lo tienes en la mano aparece su descripción.
    //    Para guardarlo, llévalo DETRÁS DE TU CABEZA (por sobre el hombro, como a la mochila)
    //    y suéltalo: se guarda en la mochila igual que con el botón "Guardar" en PC.
    //  - El computador se usa APUNTANDO con el rayo del control y apretando el GATILLO:
    //    se abre la misma ventana que en PC (ir al sendero o ver qué te falta).
    //  - En VR se apaga la "mira" que apuntaba con la cabeza.
    //
    //  No hay que agregar nada a la escena: se prepara solo al cargar cualquier escena
    //  que tenga ObjetoTomable o Computador.
    // =====================================================================================

    /// <summary>Arranca solo y, en cada escena, prepara los objetos del cuarto para VR.</summary>
    public class ModoCuartoVR : MonoBehaviour
    {
        static ModoCuartoVR instancia;
        readonly List<InteraccionJugador> miras = new List<InteraccionJugador>();
        bool vrAntes;
        bool primera = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Arrancar()
        {
            if (instancia != null) return;
            var go = new GameObject("Senda_ModoCuartoVR") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            instancia = go.AddComponent<ModoCuartoVR>();
            go.AddComponent<ManosVR>();
            SceneManager.sceneLoaded += (_, __) => instancia.PrepararEscena();
            instancia.PrepararEscena();
        }

        void PrepararEscena()
        {
            foreach (var o in FindObjectsByType<ObjetoTomable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (o.GetComponent<AgarrableVR>() == null) o.gameObject.AddComponent<AgarrableVR>();
            foreach (var c in FindObjectsByType<Computador>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.GetComponent<ComputadorVR>() == null) c.gameObject.AddComponent<ComputadorVR>();

            miras.Clear();
            miras.AddRange(FindObjectsByType<InteraccionJugador>(FindObjectsInactive.Include, FindObjectsSortMode.None));
            primera = true;
        }

        void Update()
        {
            bool vr = MostrarSoloEnVR.VisorActivo();
            if (!primera && vr == vrAntes) return;
            primera = false;
            vrAntes = vr;

            // En VR no se apunta con la cabeza: se usan las manos y el rayo de los controles
            foreach (var m in miras)
            {
                if (m == null) continue;
                m.enabled = !vr;
                if (vr)
                {
                    if (m.mira != null) m.mira.enabled = false;
                    if (m.grupoEtiqueta != null) m.grupoEtiqueta.alpha = 0f;
                }
            }
        }

        /// <summary>Vibración en un control (busca el componente de vibración en el control y sus hijos).</summary>
        public static void VibrarControl(Transform control, float fuerza, float segundos)
        {
            if (control == null) return;
            foreach (var c in control.GetComponentsInChildren<Component>())
                if (IntentarVibrar(c, fuerza, segundos)) return;
        }

        static bool IntentarVibrar(Component c, float fuerza, float segundos)
        {
            if (c == null) return false;
            var m = c.GetType().GetMethod("SendHapticImpulse", BindingFlags.Public | BindingFlags.Instance,
                                          null, new[] { typeof(float), typeof(float) }, null);
            if (m == null) return false;
            m.Invoke(c, new object[] { fuerza, segundos });
            return true;
        }

        /// <summary>Vibración en el control que tiene el objeto (si el control lo permite).</summary>
        public static void Vibrar(Component interactor, float fuerza, float segundos)
        {
            if (interactor == null) return;
            foreach (var c in interactor.GetComponentsInParent<Component>())
            {
                if (c == null) continue;
                var m = c.GetType().GetMethod("SendHapticImpulse", BindingFlags.Public | BindingFlags.Instance,
                                              null, new[] { typeof(float), typeof(float) }, null);
                if (m == null) continue;
                m.Invoke(c, new object[] { fuerza, segundos });
                return;
            }
        }
    }

    /// <summary>
    /// Se agrega solo a cada ObjetoTomable. En VR lo vuelve agarrable con la mano (lo mueven las ManosVR);
    /// al soltarlo detrás de la cabeza se guarda en la mochila.
    /// </summary>
    [DisallowMultipleComponent]
    public class AgarrableVR : MonoBehaviour
    {
        [Tooltip("Qué tan lejos de la cabeza cuenta como 'en la mochila' (metros).")]
        public float alcanceMochila = 0.7f;

        public static readonly List<AgarrableVR> Todos = new List<AgarrableVR>();

        public ObjetoTomable Objeto { get; private set; }
        public bool Preparado { get; private set; }
        public bool Sostenido { get; private set; }
        public Rigidbody Cuerpo { get; private set; }
        /// <summary>Manos cuyo rayo (o mano) está tocando este objeto ahora.</summary>
        public readonly HashSet<InteractorHandedness> apuntadoPor = new HashSet<InteractorHandedness>();

        XRSimpleInteractable apuntable;
        Collider[] colisores;
        CharacterController jugador;
        bool enZona;
        bool eraCinematico;

        void Awake() => Objeto = GetComponent<ObjetoTomable>();
        void OnEnable() => Todos.Add(this);
        void OnDisable() { Todos.Remove(this); apuntadoPor.Clear(); }

        void Update()
        {
            bool vr = MostrarSoloEnVR.VisorActivo();
            if (vr && !Preparado) Preparar();
            if (apuntable != null && apuntable.enabled != vr) apuntable.enabled = vr;
        }

        void Preparar()
        {
            Preparado = true;

            // Colisión sólida: para tocarlo, apuntarle con el rayo y que no atraviese el piso.
            // (En PC el cuarto usa una caja "trigger" solo para apuntar; en VR la volvemos sólida.)
            var todos = GetComponentsInChildren<Collider>(true);
            if (todos.Length == 0)
            {
                var caja = gameObject.AddComponent<BoxCollider>();
                AjustarCaja(caja);
            }
            foreach (var c in GetComponentsInChildren<Collider>(true))
                if (c != null && c.isTrigger) c.isTrigger = false;
            colisores = PrepararColisores(gameObject);

            // Física para soltarlo y lanzarlo. Si no tenía, parte quieto donde está
            // (no se cae del estante) y recién después de tomarlo cae con gravedad.
            Cuerpo = GetComponent<Rigidbody>();
            if (Cuerpo == null)
            {
                Cuerpo = gameObject.AddComponent<Rigidbody>();
                Cuerpo.isKinematic = true;
            }
            Cuerpo.interpolation = RigidbodyInterpolation.None; // lo movemos nosotros mientras está en la mano
            Cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Solo para saber si el rayo o la mano lo están tocando (el movimiento lo hacen las ManosVR)
            apuntable = gameObject.AddComponent<XRSimpleInteractable>();
            if (colisores.Length > 0) UsarSoloEstosColisores(apuntable, colisores);
            apuntable.hoverEntered.AddListener(a => apuntadoPor.Add(a.interactorObject.handedness));
            apuntable.hoverExited.AddListener(a => apuntadoPor.Remove(a.interactorObject.handedness));

            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) jugador = pc.GetComponent<CharacterController>();
        }

        /// <summary>Lo llama la mano al tomarlo.</summary>
        public void AlTomar(Transform mano)
        {
            Sostenido = true;
            enZona = false;
            eraCinematico = Cuerpo.isKinematic;
            Cuerpo.isKinematic = true;
            IgnorarJugador(true); // que no empuje al jugador al llevarlo hacia la espalda
            TarjetaInfoVR.Mostrar(Objeto.nombreVisible, Objeto.descripcion,
                                  "Llévalo detrás de tu cabeza para guardarlo en la mochila", transform);
        }

        /// <summary>Lo llama la mano mientras lo sostiene. Devuelve true si acaba de entrar o salir de la zona de la mochila.</summary>
        public bool RevisarZona(out bool dentro)
        {
            dentro = EnZonaMochila();
            if (dentro == enZona) return false;
            enZona = dentro;
            TarjetaInfoVR.Pie(dentro ? "<color=#F17E07>Suéltalo para guardarlo en la mochila</color>"
                                     : "Llévalo detrás de tu cabeza para guardarlo en la mochila");
            return true;
        }

        /// <summary>Lo llama la mano al soltarlo. Devuelve true si se guardó en la mochila.</summary>
        public bool AlSoltar(Vector3 velocidad, Vector3 velocidadAngular)
        {
            Sostenido = false;
            IgnorarJugador(false);
            TarjetaInfoVR.Ocultar(transform);

            if (EnZonaMochila())
            {
                Objeto.Tomar(); // igual que "Guardar" en PC: lo marca en la lista y lo saca del cuarto
                return true;
            }

            // Soltado en otro lado: cae con gravedad y conserva el impulso de la mano (se puede lanzar)
            Cuerpo.isKinematic = false;
            Cuerpo.linearVelocity = velocidad;
            Cuerpo.angularVelocity = velocidadAngular;
            return false;
        }

        public Bounds Limites()
        {
            if (colisores != null && colisores.Length > 0 && colisores[0] != null)
            {
                var b = colisores[0].bounds;
                foreach (var c in colisores) if (c != null) b.Encapsulate(c.bounds);
                return b;
            }
            return new Bounds(transform.position, Vector3.one * 0.1f);
        }

        bool EnZonaMochila()
        {
            var cam = Camera.main;
            if (cam == null) return false;
            Vector3 d = Limites().center - cam.transform.position;
            Vector3 adelante = cam.transform.forward; adelante.y = 0f;
            if (adelante.sqrMagnitude < 0.0001f) return false;
            adelante.Normalize();
            float atras = -Vector3.Dot(new Vector3(d.x, 0f, d.z), adelante);
            // Detrás de la cabeza, por sobre el hombro o justo encima de la cabeza (no frente a la cara)
            return atras > -0.05f && d.magnitude < alcanceMochila && d.y > -0.75f && d.y < 0.6f;
        }

        void IgnorarJugador(bool ignorar)
        {
            if (jugador == null || colisores == null) return;
            foreach (var c in colisores) if (c != null) Physics.IgnoreCollision(c, jugador, ignorar);
        }

        void AjustarCaja(BoxCollider caja)
        {
            var rends = GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            caja.center = transform.InverseTransformPoint(b.center);
            Vector3 s = transform.lossyScale;
            caja.size = new Vector3(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                                    b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                                    b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
        }

        /// <summary>
        /// El modelo del cuarto se importa con "Generate Colliders": cada objeto trae una colisión con la forma
        /// exacta de la malla (cóncava). Unity no puede mover esas con física, y XR Interaction Toolkit
        /// tampoco puede medir distancias con ellas. Si el objeto tiene otra colisión (la caja), se apaga la de malla;
        /// si no, la malla se vuelve "convexa" (una envoltura simple que sí funciona con física).
        /// </summary>
        public static Collider[] PrepararColisores(GameObject go)
        {
            var todos = go.GetComponentsInChildren<Collider>(true);
            bool hayOtro = todos.Any(c => !(c is MeshCollider));
            foreach (var mc in todos.OfType<MeshCollider>())
            {
                if (hayOtro) mc.enabled = false;
                else mc.convex = true;
            }
            return todos.Where(c => c != null && c.enabled).ToArray();
        }

        /// <summary>Hace que el interactuable use solo estas colisiones (no las de malla apagadas).</summary>
        public static void UsarSoloEstosColisores(XRBaseInteractable interactuable, Collider[] usar)
        {
            interactuable.enabled = false; // para registrarlo de nuevo con la lista correcta
            interactuable.colliders.Clear();
            interactuable.colliders.AddRange(usar);
            interactuable.enabled = true;
        }
    }

    /// <summary>
    /// Las dos manos en VR. Con el botón de agarre (grip) toman el objeto que la mano o su rayo
    /// estén tocando; el objeto sigue a la mano hasta que sueltas el botón.
    /// La crea sola ModoCuartoVR.
    /// </summary>
    public class ManosVR : MonoBehaviour
    {
        class Mano
        {
            public InteractorHandedness lado;
            public string nombreControl;
            public Transform t;
            public InputAction agarre;
            public AgarrableVR objeto;
            public Vector3 offPos;
            public Quaternion offRot;
            public float atraer;          // 0..1 mientras el objeto viaja hacia la mano
            public Vector3 desdePos;
            public Quaternion desdeRot;
            public Vector3 posAnterior;
            public Quaternion rotAnterior;
            public Vector3 velocidad, velocidadAngular;
        }

        readonly Mano[] manos =
        {
            new Mano { lado = InteractorHandedness.Left, nombreControl = "Control Izquierdo" },
            new Mano { lado = InteractorHandedness.Right, nombreControl = "Control Derecho" },
        };

        const float DistanciaCerca = 0.25f;  // más cerca que esto se toma "desde donde lo tocas"
        const float TiempoAtraer = 0.18f;    // lo que tarda en llegar a la mano si lo tomas con el rayo

        void Awake()
        {
            manos[0].agarre = new InputAction("Agarrar izquierda", InputActionType.Button, "<XRController>{LeftHand}/{GripButton}");
            manos[1].agarre = new InputAction("Agarrar derecha", InputActionType.Button, "<XRController>{RightHand}/{GripButton}");
        }

        void OnEnable() { foreach (var m in manos) m.agarre.Enable(); }
        void OnDisable() { foreach (var m in manos) { m.agarre.Disable(); if (m.objeto != null) Soltar(m); } }

        void Update()
        {
            if (!MostrarSoloEnVR.VisorActivo()) { foreach (var m in manos) if (m.objeto != null) Soltar(m); return; }

            foreach (var m in manos)
            {
                if (m.t == null) m.t = BuscarControl(m.nombreControl);
                if (m.t == null) continue;

                if (m.agarre.WasPressedThisFrame() && m.objeto == null) Tomar(m);
                else if (m.agarre.WasReleasedThisFrame() && m.objeto != null) Soltar(m);
            }
        }

        // Después de que los controles se movieron este cuadro
        void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            foreach (var m in manos)
            {
                if (m.t == null) continue;

                // Velocidad de la mano (para lanzar)
                Vector3 v = (m.t.position - m.posAnterior) / dt;
                Quaternion dq = m.t.rotation * Quaternion.Inverse(m.rotAnterior);
                dq.ToAngleAxis(out float ang, out Vector3 eje);
                if (ang > 180f) ang -= 360f;
                Vector3 w = (float.IsInfinity(eje.x) || float.IsNaN(eje.x)) ? Vector3.zero : eje * (ang * Mathf.Deg2Rad / dt);
                m.velocidad = Vector3.Lerp(m.velocidad, v, 0.5f);
                m.velocidadAngular = Vector3.Lerp(m.velocidadAngular, w, 0.5f);
                m.posAnterior = m.t.position;
                m.rotAnterior = m.t.rotation;

                if (m.objeto == null) continue;
                if (m.objeto.Objeto != null && m.objeto.Objeto.Tomado) { m.objeto = null; continue; }

                // Seguir a la mano (si viene desde lejos, primero viaja hasta ella)
                Vector3 destinoPos = m.t.TransformPoint(m.offPos);
                Quaternion destinoRot = m.t.rotation * m.offRot;
                var obj = m.objeto.transform;
                if (m.atraer < 1f)
                {
                    m.atraer = Mathf.Min(1f, m.atraer + dt / TiempoAtraer);
                    float k = SendaEstilo.EaseOutCubic(m.atraer);
                    obj.SetPositionAndRotation(Vector3.Lerp(m.desdePos, destinoPos, k), Quaternion.Slerp(m.desdeRot, destinoRot, k));
                }
                else obj.SetPositionAndRotation(destinoPos, destinoRot);

                if (m.objeto.RevisarZona(out bool dentro))
                    ModoCuartoVR.VibrarControl(m.t, dentro ? 0.5f : 0.15f, dentro ? 0.12f : 0.05f);
            }
        }

        void Tomar(Mano m)
        {
            var candidato = Elegir(m);
            if (candidato == null) return;
            foreach (var otra in manos) if (otra.objeto == candidato) Soltar(otra); // pasarlo de una mano a otra

            var obj = candidato.transform;
            Vector3 centro = candidato.Limites().center;
            bool cerca = Vector3.Distance(centro, m.t.position) <= DistanciaCerca;

            m.objeto = candidato;
            candidato.AlTomar(m.t);

            if (cerca)
            {
                // Se toma desde donde lo tocas
                m.offPos = m.t.InverseTransformPoint(obj.position);
                m.offRot = Quaternion.Inverse(m.t.rotation) * obj.rotation;
                m.atraer = 1f;
            }
            else
            {
                // Con el rayo: viaja a la mano y queda centrado en ella, con su rotación actual
                m.offRot = Quaternion.Inverse(m.t.rotation) * obj.rotation;
                Vector3 desplazamientoCentro = obj.position - centro; // el pivote no siempre está al centro
                m.offPos = m.t.InverseTransformPoint(m.t.position + m.t.forward * 0.06f + desplazamientoCentro);
                m.desdePos = obj.position;
                m.desdeRot = obj.rotation;
                m.atraer = 0f;
            }
            ModoCuartoVR.VibrarControl(m.t, 0.25f, 0.06f);
        }

        void Soltar(Mano m)
        {
            var obj = m.objeto;
            m.objeto = null;
            if (obj == null) return;
            bool guardado = obj.AlSoltar(m.velocidad, m.velocidadAngular);
            if (m.t != null) ModoCuartoVR.VibrarControl(m.t, guardado ? 0.7f : 0.1f, guardado ? 0.15f : 0.04f);
        }

        AgarrableVR Elegir(Mano m)
        {
            AgarrableVR mejor = null;
            float mejorDist = float.MaxValue;
            foreach (var a in AgarrableVR.Todos)
            {
                if (a == null || !a.Preparado || a.Objeto == null || a.Objeto.Tomado) continue;
                float d = Vector3.Distance(a.Limites().ClosestPoint(m.t.position), m.t.position);
                bool tocado = a.apuntadoPor.Contains(m.lado) || d <= 0.08f;
                if (!tocado || d >= mejorDist) continue;
                mejor = a;
                mejorDist = d;
            }
            return mejor;
        }

        static Transform BuscarControl(string nombre)
        {
            foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (t.name == nombre) return t;
            return null;
        }
    }

    /// <summary>
    /// Se agrega solo al Computador. En VR: apuntas con el rayo del control y aprietas el gatillo
    /// (o el agarre) para abrir la misma ventana que en PC.
    /// </summary>
    [DisallowMultipleComponent]
    public class ComputadorVR : MonoBehaviour
    {
        Computador pc;
        XRSimpleInteractable interactuable;
        readonly HashSet<Object> apuntando = new HashSet<Object>();
        InputAction gatillo;
        bool mostrandoTarjeta;

        void Awake()
        {
            pc = GetComponent<Computador>();
            gatillo = new InputAction("Usar computador", InputActionType.Button);
            gatillo.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            gatillo.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
        }

        void OnEnable() => gatillo.Enable();
        void OnDisable() => gatillo.Disable();

        void Update()
        {
            bool vr = MostrarSoloEnVR.VisorActivo();
            if (vr && interactuable == null) Preparar();
            if (interactuable != null && interactuable.enabled != vr) interactuable.enabled = vr;
            if (!vr) return;

            apuntando.RemoveWhere(o => o == null);
            bool libre = !EstadoJugador.Bloqueado && !VentanaObjeto.Abierta;
            bool apuntado = libre && apuntando.Count > 0;

            if (apuntado && !mostrandoTarjeta)
            {
                TarjetaInfoVR.Mostrar(pc.titulo, "", "Gatillo para usar", transform);
                mostrandoTarjeta = true;
            }
            else if (!apuntado && mostrandoTarjeta)
            {
                TarjetaInfoVR.Ocultar(transform);
                mostrandoTarjeta = false;
            }

            if (apuntado && gatillo.WasPressedThisFrame()) Usar();
        }

        void Preparar()
        {
            if (GetComponentsInChildren<Collider>(true).Length == 0)
            {
                var caja = gameObject.AddComponent<BoxCollider>();
                var rends = GetComponentsInChildren<Renderer>(true);
                if (rends.Length > 0)
                {
                    var b = rends[0].bounds;
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    caja.center = transform.InverseTransformPoint(b.center);
                    Vector3 s = transform.lossyScale;
                    caja.size = new Vector3(b.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
                                            b.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
                                            b.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
                }
            }
            // El rayo de los controles no detecta cajas "trigger": la caja para apuntar se vuelve sólida
            foreach (var c in GetComponentsInChildren<Collider>(true)) if (c.isTrigger) c.isTrigger = false;
            var colisoresPc = AgarrableVR.PrepararColisores(gameObject);
            interactuable = gameObject.AddComponent<XRSimpleInteractable>();
            if (colisoresPc.Length > 0) AgarrableVR.UsarSoloEstosColisores(interactuable, colisoresPc);
            interactuable.hoverEntered.AddListener(a => apuntando.Add(a.interactorObject as Object));
            interactuable.hoverExited.AddListener(a => apuntando.Remove(a.interactorObject as Object));
            interactuable.selectEntered.AddListener(a => { if (!EstadoJugador.Bloqueado && !VentanaObjeto.Abierta) Usar(); });
        }

        void Usar()
        {
            if (VentanaObjeto.Instancia == null) return;
            TarjetaInfoVR.Ocultar(transform);
            mostrandoTarjeta = false;
            VentanaObjeto.Instancia.Mostrar(pc); // la misma ventana que en PC
        }
    }

    /// <summary>
    /// Tarjeta pequeña que flota sobre el objeto que tienes en la mano (o al que apuntas):
    /// nombre, descripción y una ayuda. Se dibuja encima de todo para que no la tapen las paredes.
    /// </summary>
    public class TarjetaInfoVR : MonoBehaviour
    {
        static TarjetaInfoVR instancia;
        TextMeshProUGUI titulo, cuerpo, pie;
        RectTransform panel;
        CanvasGroup grupo;
        Transform siguiendo;
        float alfaObjetivo;

        public static void Mostrar(string tituloTexto, string cuerpoTexto, string pieTexto, Transform objeto)
        {
            var t = Obtener();
            t.siguiendo = objeto;
            t.titulo.text = tituloTexto;
            t.cuerpo.text = cuerpoTexto;
            t.cuerpo.gameObject.SetActive(!string.IsNullOrEmpty(cuerpoTexto));
            t.pie.text = pieTexto;
            t.alfaObjetivo = 1f;
            t.Posicionar(true);
        }

        public static void Pie(string pieTexto)
        {
            if (instancia != null) instancia.pie.text = pieTexto;
        }

        /// <summary>Oculta la tarjeta, solo si está mostrando ese objeto.</summary>
        public static void Ocultar(Transform objeto)
        {
            if (instancia == null || instancia.siguiendo != objeto) return;
            instancia.alfaObjetivo = 0f;
        }

        static TarjetaInfoVR Obtener()
        {
            if (instancia != null) return instancia;

            var go = new GameObject("Senda_TarjetaInfoVR", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 200;
            instancia = go.AddComponent<TarjetaInfoVR>();
            instancia.grupo = go.AddComponent<CanvasGroup>();
            instancia.grupo.alpha = 0f;
            instancia.grupo.blocksRaycasts = false;
            instancia.grupo.interactable = false;

            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(620f, 360f);
            rt.localScale = Vector3.one * 0.0007f; // ~43 cm de ancho

            // Fondo
            var fondo = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            fondo.transform.SetParent(go.transform, false);
            instancia.panel = (RectTransform)fondo.transform;
            Estirar(instancia.panel);
            var img = fondo.GetComponent<Image>();
            img.color = new Color(0.06f, 0.07f, 0.08f, 0.82f);
            var layout = fondo.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 28, 22, 22);
            layout.spacing = 10f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            var ajuste = fondo.AddComponent<ContentSizeFitter>();
            ajuste.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_FontAsset fuente = null;
            var algunTexto = FindAnyObjectByType<TextMeshProUGUI>();
            if (algunTexto != null) fuente = algunTexto.font;

            instancia.titulo = CrearTexto(fondo.transform, fuente, 40f, FontStyles.Bold, Color.white);
            instancia.cuerpo = CrearTexto(fondo.transform, fuente, 25f, FontStyles.Normal, new Color(1f, 1f, 1f, 0.88f));
            instancia.pie = CrearTexto(fondo.transform, fuente, 23f, FontStyles.Italic, new Color(1f, 1f, 1f, 0.7f));

            // Encima de todo (que no la tapen paredes ni el mismo objeto)
            var matFondo = new Material(img.material);
            matFondo.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
            img.material = matFondo;
            foreach (var t in new[] { instancia.titulo, instancia.cuerpo, instancia.pie })
                t.fontMaterial.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);

            return instancia;
        }

        static TextMeshProUGUI CrearTexto(Transform padre, TMP_FontAsset fuente, float tam, FontStyles estilo, Color color)
        {
            var go = new GameObject("Texto", typeof(RectTransform));
            go.transform.SetParent(padre, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (fuente != null) t.font = fuente;
            t.fontSize = tam;
            t.fontStyle = estilo;
            t.color = color;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            return t;
        }

        static void Estirar(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        void LateUpdate()
        {
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, alfaObjetivo, Time.unscaledDeltaTime * 6f);
            if (grupo.alpha <= 0f && alfaObjetivo <= 0f) { siguiendo = null; return; }
            Posicionar(false);
        }

        void Posicionar(bool inmediato)
        {
            var cam = Camera.main;
            if (cam == null || siguiendo == null || !siguiendo.gameObject.activeInHierarchy) { alfaObjetivo = 0f; return; }

            // Arriba del objeto, un poco hacia la cámara
            Bounds b = new Bounds(siguiendo.position, Vector3.zero);
            foreach (var r in siguiendo.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
            Vector3 haciaCam = (cam.transform.position - b.center); haciaCam.y = 0f;
            haciaCam = haciaCam.sqrMagnitude > 0.0001f ? haciaCam.normalized : Vector3.zero;
            Vector3 destino = new Vector3(b.center.x, b.max.y + 0.06f, b.center.z) + haciaCam * 0.12f;

            transform.position = inmediato ? destino : Vector3.Lerp(transform.position, destino, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            Vector3 mirar = transform.position - cam.transform.position;
            if (mirar.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(mirar, Vector3.up);
        }
    }
}
