using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

/// <summary>
/// Controlador en primera persona con movimiento estilo Half-Life 2 / Project Borealis
/// (motor Source): aceleración, fricción, control en el aire, bunny hop y agacharse.
/// Incluye pasos que dependen de la velocidad y de la superficie, y balanceo de cámara.
/// Funciona en PC y en VR.
///
/// PC:  WASD mover · Mouse mirar · Shift correr · Alt caminar lento ·
///      Ctrl o C agacharse · Espacio saltar · Esc soltar el mouse.
/// VR:  joystick izquierdo mover (clic = correr) · joystick derecho girar a saltos ·
///      botón A saltar · la cabeza mueve la cámara.
///
/// Las velocidades vienen de Project Borealis, pasadas de centímetros de Unreal a metros.
///
/// Jerarquía esperada (el menú "Senda > Crear jugador en primera persona" la arma sola):
///   Jugador            (CharacterController + este script)
///   └─ CameraOffset    (objeto vacío)
///      └─ Main Camera
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    // ======================================================================= Ajustes

    [Header("Referencias")]
    [Tooltip("Objeto vacío, hijo del jugador, que contiene la cámara.")]
    public Transform cameraOffset;
    [Tooltip("La cámara del jugador (hija de CameraOffset).")]
    public Camera playerCamera;

    [Header("Velocidades (m/s)")]
    [Tooltip("Velocidad manteniendo Alt (caminar lento).")]
    public float velocidadCaminar = 2.86f;
    [Tooltip("Velocidad normal, sin teclas extra.")]
    public float velocidadTrotar = 3.62f;
    [Tooltip("Velocidad manteniendo Shift.")]
    public float velocidadCorrer = 6.10f;
    [Tooltip("Velocidad agachado.")]
    public float velocidadAgachado = 1.21f;

    [Header("Física estilo Source")]
    [Tooltip("Qué tan rápido alcanzas la velocidad en el suelo (sv_accelerate).")]
    public float aceleracionSuelo = 10f;
    [Tooltip("Qué tan rápido cambias de dirección en el aire (sv_airaccelerate).")]
    public float aceleracionAire = 10f;
    [Tooltip("Tope de velocidad que se suma en el aire. Bajo = control de aire estilo Source (air strafing).")]
    public float topeVelocidadAire = 0.57f;
    [Tooltip("Cuánto frena el suelo (sv_friction).")]
    public float friccion = 4f;
    [Tooltip("Bajo esta velocidad el frenado es más firme, para detenerse rápido (sv_stopspeed).")]
    public float velocidadFrenado = 1.9f;
    [Tooltip("Fricción extra al estar al borde de una caída, para no resbalar por los bordes.")]
    public float multiplicadorFriccionBorde = 2f;
    [Tooltip("Gravedad en m/s². Half-Life 2 usa 11,43 (sv_gravity 600).")]
    public float gravedad = 11.43f;
    [Tooltip("Velocidad hacia arriba al saltar, en m/s.")]
    public float velocidadSalto = 3.05f;
    [Tooltip("Si está activo, mantener Espacio salta solo al tocar el suelo (bunny hop automático).")]
    public bool saltoAutomatico = false;
    [Tooltip("Segundos en el suelo antes de que empiece la fricción. Permite el bunny hop.")]
    public float ventanaFrenado = 0.015f;
    [Tooltip("Pendiente máxima por la que se puede caminar, en grados.")]
    public float pendienteMaxima = 45.6f;
    [Tooltip("Altura máxima de escalón que se sube sin saltar, en metros.")]
    public float alturaEscalon = 0.34f;

    [Header("Altura y agacharse")]
    public float alturaDePie = 1.8f;
    public float alturaAgachado = 1.0f;
    [Tooltip("Altura de los ojos de pie (modo PC). En VR la da el visor.")]
    public float alturaOjos = 1.7f;
    public float alturaOjosAgachado = 0.9f;
    [Tooltip("Segundos que demora en agacharse o pararse.")]
    public float tiempoAgacharse = 0.2f;

    [Header("Mirar con mouse (PC)")]
    public float sensibilidadMouse = 0.1f;
    [Tooltip("Cuánto se puede mirar hacia arriba/abajo, en grados.")]
    public float limiteMirada = 89f;

    [Header("Inclinación al moverse de lado (como Half-Life)")]
    [Tooltip("Grados que se inclina la cámara al moverte de lado. 0 = desactivado.")]
    public float anguloInclinacion = 1.5f;
    [Tooltip("Velocidad lateral (m/s) a la que se llega a la inclinación máxima.")]
    public float velocidadInclinacion = 3.8f;

    [Header("Balanceo de cámara al caminar")]
    public bool balanceoActivo = true;
    [Tooltip("Cuánto sube y baja la cámara con cada paso (m), a velocidad normal.")]
    public float balanceoVertical = 0.035f;
    [Tooltip("Cuánto se mueve la cámara de lado a lado (m).")]
    public float balanceoLateral = 0.02f;
    [Tooltip("Cuánto se ladea la cámara con cada paso (grados).")]
    public float balanceoGiro = 0.6f;
    [Tooltip("Qué tan rápido entra y sale el balanceo al empezar o dejar de caminar.")]
    public float suavizadoBalanceo = 8f;
    [Tooltip("Qué tan fuerte se hunde la cámara al aterrizar.")]
    public float golpeAterrizaje = 0.12f;
    [Tooltip("Balanceo en VR. Viene apagado porque mover la cámara que no controla tu cabeza marea mucho.")]
    public bool balanceoEnVR = false;

    [Header("Pasos")]
    [Tooltip("Metros por paso a velocidad de caminar. El ritmo de los pasos sale de aquí y de la velocidad.")]
    public float largoPasoCaminando = 1.1f;
    [Tooltip("Metros por paso corriendo (pasos más largos, pero más seguidos porque vas más rápido).")]
    public float largoPasoCorriendo = 1.85f;
    public float largoPasoAgachado = 0.6f;
    [Tooltip("Bajo esta velocidad no suenan pasos.")]
    public float velocidadMinimaPasos = 0.5f;
    [Tooltip("Sonidos por superficie. La primera es la de por defecto.")]
    public SuperficiePasos[] superficies = { new SuperficiePasos() };
    [Tooltip("Fuente de audio de los pies. Si está vacía se crea sola.")]
    public AudioSource fuenteAudio;

    [Header("VR")]
    [Tooltip("Grados que gira el jugador con cada empujón del joystick derecho.")]
    public float snapTurnAngle = 30f;

    [System.Serializable]
    public class SuperficiePasos
    {
        public string nombre = "Por defecto";
        [Tooltip("Suena con esta superficie si el suelo tiene uno de estos Physics Materials.")]
        public PhysicsMaterial[] materialesFisicos = new PhysicsMaterial[0];
        [Tooltip("Suena con esta superficie si la capa de terreno dominante bajo los pies es una de estas.")]
        public TerrainLayer[] capasTerreno = new TerrainLayer[0];
        public AudioClip[] pasos = new AudioClip[0];
        [Tooltip("Opcional. Si está vacío, al correr se usan los pasos normales más fuerte.")]
        public AudioClip[] pasosCorriendo = new AudioClip[0];
        public AudioClip[] saltos = new AudioClip[0];
        public AudioClip[] aterrizajes = new AudioClip[0];
        [Range(0f, 1f)] public float volumenCaminando = 0.5f;
        [Range(0f, 1f)] public float volumenCorriendo = 0.9f;
    }

    // ======================================================================= Estado

    CharacterController controller;
    InputAction moveAction, lookAction, sprintAction, walkAction, crouchAction, jumpAction, turnAction;
    TrackedPoseDriver poseDriver;

    bool vrActive;
    Vector3 velocidad;
    bool enSuelo, estabaEnSuelo;
    float tiempoEnSuelo;
    RaycastHit sueloHit;
    float friccionSuperficie = 1f;
    float bufferSalto;

    float agachado;          // 0 = de pie, 1 = agachado
    bool quiereAgacharse;

    float pitch;
    bool turnReady = true;

    // Balanceo y pasos
    float distanciaPasos;    // metros recorridos medidos en "pasos"
    int ultimoPaso;
    float intensidadBalanceo;
    Vector3 balanceoPos;
    float balanceoRoll;
    float inclinacion;
    float aterrizajeOffset, aterrizajeVel;
    AudioClip ultimoClip;

    float CosPendiente => Mathf.Cos(pendienteMaxima * Mathf.Deg2Rad);
    float VelocidadHorizontal => new Vector2(velocidad.x, velocidad.z).magnitude;

    // ======================================================================= Inicio

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (cameraOffset == null && playerCamera != null) cameraOffset = playerCamera.transform.parent;

        controller.height = alturaDePie;
        controller.center = new Vector3(0f, alturaDePie * 0.5f + controller.skinWidth, 0f);
        controller.slopeLimit = pendienteMaxima;
        controller.stepOffset = Mathf.Min(alturaEscalon, alturaAgachado * 0.5f);
        controller.minMoveDistance = 0f;

        CrearAcciones();
        CrearFuenteAudio();
    }

    void CrearAcciones()
    {
        moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddBinding("<Gamepad>/leftStick");
        moveAction.AddBinding("<XRController>{LeftHand}/{Primary2DAxis}");

        lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");

        sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        sprintAction.AddBinding("<Gamepad>/leftStickPress");
        sprintAction.AddBinding("<XRController>{LeftHand}/{Primary2DAxisClick}");

        walkAction = new InputAction("Walk", InputActionType.Button, "<Keyboard>/leftAlt");

        crouchAction = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
        crouchAction.AddBinding("<Keyboard>/c");
        crouchAction.AddBinding("<Gamepad>/buttonEast");

        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");
        jumpAction.AddBinding("<XRController>{RightHand}/{PrimaryButton}");

        turnAction = new InputAction("Turn", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}", expectedControlType: "Vector2");
    }

    void CrearFuenteAudio()
    {
        if (fuenteAudio != null) return;
        var pies = new GameObject("Pies");
        pies.transform.SetParent(transform, false);
        pies.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        fuenteAudio = pies.AddComponent<AudioSource>();
        fuenteAudio.playOnAwake = false;
        fuenteAudio.spatialBlend = 1f;
        fuenteAudio.minDistance = 1.5f;
        fuenteAudio.maxDistance = 20f;
    }

    IEnumerable<InputAction> Acciones()
    {
        yield return moveAction; yield return lookAction; yield return sprintAction; yield return walkAction;
        yield return crouchAction; yield return jumpAction; yield return turnAction;
    }

    void OnEnable() { foreach (var a in Acciones()) a.Enable(); }

    void OnDisable()
    {
        foreach (var a in Acciones()) a.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        vrActive = XRSettings.isDeviceActive;
        if (vrActive) SetupVR();
        else SetupPC();
    }

    void SetupPC()
    {
        if (poseDriver != null) poseDriver.enabled = false;
        if (cameraOffset != null) cameraOffset.localPosition = new Vector3(0f, alturaOjos, 0f);
        playerCamera.transform.localPosition = Vector3.zero;
        playerCamera.transform.localRotation = Quaternion.identity;
        LockCursor(true);
    }

    void SetupVR()
    {
        // En VR la altura la entrega el visor, medida desde el suelo real.
        if (cameraOffset != null) cameraOffset.localPosition = Vector3.zero;

        var subsystems = new List<XRInputSubsystem>();
        SubsystemManager.GetSubsystems(subsystems);
        foreach (var s in subsystems) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);

        poseDriver = playerCamera.GetComponent<TrackedPoseDriver>();
        if (poseDriver == null) poseDriver = playerCamera.gameObject.AddComponent<TrackedPoseDriver>();
        poseDriver.enabled = false;
        poseDriver.positionInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        poseDriver.rotationInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        poseDriver.trackingStateInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/trackingState", expectedControlType: "Integer"));
        poseDriver.enabled = true;

        LockCursor(false);
    }

    // ======================================================================= Cada frame

    void Update()
    {
        // Mientras el menú de Senda.vr (login, términos...) esté abierto, el jugador
        // no se mueve ni captura el mouse, para poder hacer clic en los botones.
        if (SendaVR.NavegadorPantallas.MenuAbierto)
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        if (vrActive)
        {
            SnapTurn();
        }
        else
        {
            HandleCursor();
            MouseLook();
        }

        ActualizarAgachado(dt);
        Moverse(dt);
        if (vrActive) FollowHeadWithCapsule();

        ActualizarPasosYBalanceo(dt);
        AplicarCamara(dt);
    }

    // ======================================================================= Mirar

    void MouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;
        Vector2 delta = lookAction.ReadValue<Vector2>() * sensibilidadMouse;
        transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -limiteMirada, limiteMirada);
    }

    void SnapTurn()
    {
        float x = turnAction.ReadValue<Vector2>().x;
        if (turnReady && Mathf.Abs(x) > 0.7f)
        {
            transform.RotateAround(playerCamera.transform.position, Vector3.up, Mathf.Sign(x) * snapTurnAngle);
            turnReady = false;
        }
        else if (Mathf.Abs(x) < 0.3f)
        {
            turnReady = true;
        }
    }

    // ======================================================================= Movimiento estilo Source

    void Moverse(float dt)
    {
        // --- Dirección deseada (hacia donde mira la cámara, sin subir ni bajar)
        Vector2 input = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        Vector3 adelante = vrActive ? playerCamera.transform.forward : transform.forward;
        adelante.y = 0f; adelante.Normalize();
        Vector3 derecha = Vector3.Cross(Vector3.up, adelante);
        Vector3 deseo = adelante * input.y + derecha * input.x;
        float magnitudDeseo = deseo.magnitude;
        Vector3 direccionDeseo = magnitudDeseo > 0.001f ? deseo / magnitudDeseo : Vector3.zero;
        float velocidadDeseo = VelocidadMaxima() * magnitudDeseo;

        // --- Salto (con un pequeño margen para no "perder" el salto si lo apretaste justo antes de tocar el suelo)
        bufferSalto -= dt;
        if (jumpAction.WasPressedThisFrame() || (saltoAutomatico && jumpAction.IsPressed())) bufferSalto = 0.1f;

        estabaEnSuelo = enSuelo;
        DetectarSuelo();

        if (enSuelo && !estabaEnSuelo) AlAterrizar(-velocidad.y);

        if (enSuelo && bufferSalto > 0f && agachado < 0.5f)
        {
            velocidad.y = velocidadSalto;
            enSuelo = false;
            bufferSalto = 0f;
            ReproducirSonido(s => s.saltos, VolumenSegunVelocidad());
        }

        if (enSuelo)
        {
            tiempoEnSuelo += dt;
            velocidad.y = 0f; // en el suelo se mantiene la velocidad horizontal (como en Source)

            // La fricción espera un instante al aterrizar: eso permite el bunny hop
            if (tiempoEnSuelo >= ventanaFrenado) AplicarFriccion(dt, direccionDeseo);
            Acelerar(direccionDeseo, velocidadDeseo, aceleracionSuelo * friccionSuperficie, dt);
        }
        else
        {
            tiempoEnSuelo = 0f;
            AcelerarAire(direccionDeseo, velocidadDeseo, dt);
            velocidad.y = Mathf.Max(velocidad.y - gravedad * dt, -60f);
        }

        // --- Mover el CharacterController
        Vector3 desplazamiento = velocidad * dt;
        if (enSuelo) desplazamiento.y -= alturaEscalon; // pegarse al suelo al bajar pendientes
        CollisionFlags choques = controller.Move(desplazamiento);

        if ((choques & CollisionFlags.Above) != 0 && velocidad.y > 0f) velocidad.y = 0f;
    }

    float VelocidadMaxima()
    {
        if (agachado > 0.5f) return velocidadAgachado;
        if (sprintAction.IsPressed()) return velocidadCorrer;
        if (walkAction.IsPressed()) return velocidadCaminar;
        return velocidadTrotar;
    }

    void DetectarSuelo()
    {
        // Si vamos subiendo rápido (recién saltamos), no estamos en el suelo
        if (velocidad.y > 1f) { enSuelo = false; return; }

        float radio = controller.radius * 0.95f;
        Vector3 centro = transform.TransformPoint(controller.center);
        Vector3 origen = centro + Vector3.down * (controller.height * 0.5f - controller.radius);
        float distancia = controller.skinWidth + (estabaEnSuelo ? 0.12f : 0.05f);

        enSuelo = false;
        friccionSuperficie = 1f;
        if (Physics.SphereCast(origen, radio, Vector3.down, out sueloHit, distancia, ~0, QueryTriggerInteraction.Ignore)
            && sueloHit.normal.y >= CosPendiente)
        {
            enSuelo = true;
            var mat = sueloHit.collider.sharedMaterial;
            if (mat != null) friccionSuperficie = Mathf.Min(1f, mat.dynamicFriction * 1.25f);
        }
    }

    // Fricción de Source: frena proporcional a la velocidad, con un mínimo (velocidadFrenado)
    void AplicarFriccion(float dt, Vector3 direccionDeseo)
    {
        Vector3 h = new Vector3(velocidad.x, 0f, velocidad.z);
        float rapidez = h.magnitude;
        if (rapidez < 0.01f) { velocidad.x = velocidad.z = 0f; return; }

        float f = friccion * friccionSuperficie;
        if (multiplicadorFriccionBorde != 1f && EstaAlBorde(direccionDeseo, h / rapidez)) f *= multiplicadorFriccionBorde;

        float control = Mathf.Max(rapidez, velocidadFrenado);
        float nueva = Mathf.Max(rapidez - control * f * dt, 0f);
        h *= nueva / rapidez;
        velocidad.x = h.x; velocidad.z = h.z;
    }

    bool EstaAlBorde(Vector3 direccionDeseo, Vector3 direccionVelocidad)
    {
        Vector3 dir = direccionDeseo != Vector3.zero ? direccionDeseo : direccionVelocidad;
        Vector3 origen = transform.position + dir * (controller.radius + 0.3f) + Vector3.up * 0.1f;
        return !Physics.Raycast(origen, Vector3.down, 0.75f, ~0, QueryTriggerInteraction.Ignore);
    }

    // Aceleración de Source: solo suma velocidad en la dirección deseada hasta llegar al máximo
    void Acelerar(Vector3 dir, float velocidadDeseo, float aceleracion, float dt)
    {
        if (dir == Vector3.zero) return;
        float actual = Vector3.Dot(velocidad, dir);
        float agregar = velocidadDeseo - actual;
        if (agregar <= 0f) return;
        float acel = Mathf.Min(aceleracion * velocidadDeseo * dt, agregar);
        velocidad += dir * acel;
    }

    // En el aire el tope es bajo, pero la aceleración es alta: eso permite el "air strafing"
    void AcelerarAire(Vector3 dir, float velocidadDeseo, float dt)
    {
        if (dir == Vector3.zero) return;
        float tope = Mathf.Min(velocidadDeseo, topeVelocidadAire);
        float actual = Vector3.Dot(velocidad, dir);
        float agregar = tope - actual;
        if (agregar <= 0f) return;
        float acel = Mathf.Min(aceleracionAire * velocidadDeseo * dt, agregar);
        velocidad += dir * acel;
    }

    // Al chocar con paredes, quitar la velocidad que va contra ellas (ClipVelocity de Source)
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y >= CosPendiente) return; // es suelo
        Vector3 n = hit.normal;
        if (enSuelo)
        {
            // caminando contra una pared o pendiente muy empinada: solo frenar lo horizontal
            n.y = 0f;
            if (n.sqrMagnitude < 0.0001f) return;
            n.Normalize();
        }
        float contra = Vector3.Dot(velocidad, n);
        if (contra < 0f) velocidad -= n * contra;
    }

    // ======================================================================= Agacharse

    void ActualizarAgachado(float dt)
    {
        quiereAgacharse = crouchAction.IsPressed();
        float objetivo = quiereAgacharse ? 1f : 0f;
        if (!quiereAgacharse && agachado > 0f && !PuedePararse()) objetivo = agachado; // algo encima: seguir agachado

        float paso = dt / Mathf.Max(0.01f, tiempoAgacharse);
        agachado = Mathf.MoveTowards(agachado, objetivo, paso);

        float altura = Mathf.Lerp(alturaDePie, alturaAgachado, agachado);
        controller.height = altura;
        Vector3 c = controller.center;
        c.y = altura * 0.5f + controller.skinWidth;
        controller.center = c;

        if (!vrActive && cameraOffset != null)
        {
            float t = Mathf.SmoothStep(0f, 1f, agachado);
            cameraOffset.localPosition = new Vector3(0f, Mathf.Lerp(alturaOjos, alturaOjosAgachado, t), 0f);
        }
    }

    bool PuedePararse()
    {
        float r = controller.radius * 0.95f;
        Vector3 baseP = transform.TransformPoint(new Vector3(controller.center.x, 0f, controller.center.z));
        Vector3 abajo = baseP + Vector3.up * (r + controller.skinWidth + 0.05f);
        Vector3 arriba = baseP + Vector3.up * (alturaDePie - r);
        foreach (var col in Physics.OverlapCapsule(abajo, arriba, r, ~0, QueryTriggerInteraction.Ignore))
            if (col != controller) return false;
        return true;
    }

    // En VR la cápsula sigue a la cabeza, para no atravesar paredes al caminar por la pieza.
    void FollowHeadWithCapsule()
    {
        Vector3 head = transform.InverseTransformPoint(playerCamera.transform.position);
        Vector3 c = controller.center;
        controller.center = new Vector3(head.x, c.y, head.z);
    }

    // ======================================================================= Pasos y balanceo

    void ActualizarPasosYBalanceo(float dt)
    {
        float rapidez = VelocidadHorizontal;
        bool caminando = enSuelo && rapidez >= velocidadMinimaPasos;

        // Largo del paso según velocidad: más rápido = pasos más largos y más seguidos
        float tCorrer = Mathf.InverseLerp(velocidadCaminar, velocidadCorrer, rapidez);
        float largo = agachado > 0.5f ? largoPasoAgachado : Mathf.Lerp(largoPasoCaminando, largoPasoCorriendo, tCorrer);

        if (caminando)
        {
            distanciaPasos += rapidez * dt / Mathf.Max(0.1f, largo);
            int paso = Mathf.FloorToInt(distanciaPasos);
            if (paso != ultimoPaso)
            {
                ultimoPaso = paso;
                float vol = VolumenSegunVelocidad();
                bool corriendo = tCorrer > 0.6f;
                ReproducirSonido(s => corriendo && s.pasosCorriendo.Length > 0 ? s.pasosCorriendo : s.pasos, vol);
            }
        }

        // Intensidad del balanceo: crece con la velocidad, se apaga al detenerse o en el aire
        float objetivo = caminando ? Mathf.Clamp(rapidez / velocidadTrotar, 0f, 1.6f) : 0f;
        intensidadBalanceo = Mathf.Lerp(intensidadBalanceo, objetivo, 1f - Mathf.Exp(-suavizadoBalanceo * dt));

        // Fase: cada paso es medio ciclo; la cámara toca fondo justo cuando suena el paso
        float fase = distanciaPasos * Mathf.PI;
        float factorAgachado = agachado > 0.5f ? 0.6f : 1f;
        float k = intensidadBalanceo * factorAgachado;
        balanceoPos = new Vector3(
            Mathf.Sin(fase) * balanceoLateral * k,
            -(1f + Mathf.Cos(2f * fase)) * 0.5f * balanceoVertical * k,
            0f);
        balanceoRoll = Mathf.Sin(fase) * balanceoGiro * k;

        // Inclinación al moverse de lado (cl_rollangle de Half-Life)
        float lateral = Vector3.Dot(velocidad, vrActive ? playerCamera.transform.right : transform.right);
        float objetivoInclinacion = anguloInclinacion <= 0f || velocidadInclinacion <= 0f ? 0f
            : -Mathf.Clamp(lateral / velocidadInclinacion, -1f, 1f) * anguloInclinacion;
        inclinacion = Mathf.Lerp(inclinacion, objetivoInclinacion, 1f - Mathf.Exp(-10f * dt));

        // Resorte del aterrizaje
        float aceleracion = -120f * aterrizajeOffset - 18f * aterrizajeVel;
        aterrizajeVel += aceleracion * dt;
        aterrizajeOffset += aterrizajeVel * dt;
    }

    void AlAterrizar(float velocidadCaida)
    {
        if (velocidadCaida < 1f) return;

        // Hundir la cámara según la fuerza de la caída
        aterrizajeVel -= Mathf.Min(velocidadCaida * golpeAterrizaje, 0.8f);

        // Volumen del aterrizaje según la caída (como Project Borealis)
        float vol = velocidadCaida > 8f ? 1f : velocidadCaida > 5f ? 0.85f : velocidadCaida > 2.5f ? 0.5f : 0.3f;
        if (!ReproducirSonido(s => s.aterrizajes, vol))
            ReproducirSonido(s => s.pasos, vol); // si no hay sonidos de aterrizaje, usar un paso
        distanciaPasos = Mathf.Floor(distanciaPasos); // el próximo paso parte desde aquí
        ultimoPaso = Mathf.FloorToInt(distanciaPasos);
    }

    float VolumenSegunVelocidad()
    {
        SuperficiePasos s = SuperficieActual();
        float t = Mathf.InverseLerp(velocidadCaminar, velocidadCorrer, VelocidadHorizontal);
        float vol = s != null ? Mathf.Lerp(s.volumenCaminando, s.volumenCorriendo, t) : 0.7f;
        if (agachado > 0.5f) vol *= 0.65f;
        return vol;
    }

    void AplicarCamara(float dt)
    {
        bool usarBalanceo = balanceoActivo && (!vrActive || balanceoEnVR);
        Vector3 offset = usarBalanceo ? balanceoPos + Vector3.up * aterrizajeOffset : Vector3.zero;

        if (!vrActive)
        {
            float roll = usarBalanceo ? inclinacion + balanceoRoll : 0f;
            playerCamera.transform.localPosition = offset;
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, roll);
        }
        else if (cameraOffset != null)
        {
            // En VR la cabeza manda: solo se mueve el contenedor, y solo si se activó a propósito
            cameraOffset.localPosition = offset;
        }
    }

    // ======================================================================= Sonido por superficie

    SuperficiePasos SuperficieActual()
    {
        if (superficies == null || superficies.Length == 0) return null;
        if (!Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
            return superficies[0];

        // Terreno: buscar la capa de textura dominante bajo los pies
        if (hit.collider is TerrainCollider)
        {
            var terreno = hit.collider.GetComponent<Terrain>();
            TerrainLayer capa = terreno != null ? CapaDominante(terreno, hit.point) : null;
            if (capa != null)
                foreach (var s in superficies)
                    if (s.capasTerreno != null && System.Array.IndexOf(s.capasTerreno, capa) >= 0) return s;
        }

        // Otros objetos: por Physics Material
        var mat = hit.collider.sharedMaterial;
        if (mat != null)
            foreach (var s in superficies)
                if (s.materialesFisicos != null && System.Array.IndexOf(s.materialesFisicos, mat) >= 0) return s;

        return superficies[0];
    }

    static TerrainLayer CapaDominante(Terrain terreno, Vector3 punto)
    {
        TerrainData td = terreno.terrainData;
        if (td == null || td.terrainLayers == null || td.terrainLayers.Length == 0) return null;
        Vector3 local = punto - terreno.transform.position;
        int x = Mathf.Clamp((int)(local.x / td.size.x * td.alphamapWidth), 0, td.alphamapWidth - 1);
        int z = Mathf.Clamp((int)(local.z / td.size.z * td.alphamapHeight), 0, td.alphamapHeight - 1);
        float[,,] mezcla = td.GetAlphamaps(x, z, 1, 1);
        int mejor = 0;
        for (int i = 1; i < mezcla.GetLength(2); i++)
            if (mezcla[0, 0, i] > mezcla[0, 0, mejor]) mejor = i;
        return mejor < td.terrainLayers.Length ? td.terrainLayers[mejor] : null;
    }

    // Reproduce un sonido al azar de la lista elegida, sin repetir el último. Devuelve si sonó algo.
    bool ReproducirSonido(System.Func<SuperficiePasos, AudioClip[]> elegir, float volumen)
    {
        if (fuenteAudio == null || volumen <= 0f) return false;
        SuperficiePasos s = SuperficieActual();
        AudioClip[] clips = s != null ? elegir(s) : null;
        if ((clips == null || clips.Length == 0) && superficies != null && superficies.Length > 0)
            clips = elegir(superficies[0]); // la superficie no tiene ese sonido: usar el de por defecto
        if (clips == null || clips.Length == 0) return false;

        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clips.Length > 1 && clip == ultimoClip) clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];
        if (clip == null) return false;
        ultimoClip = clip;

        fuenteAudio.pitch = Random.Range(0.92f, 1.08f);
        fuenteAudio.PlayOneShot(clip, volumen);
        return true;
    }

    // ======================================================================= Mouse

    void HandleCursor()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LockCursor(false);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) LockCursor(true);
    }

    void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
