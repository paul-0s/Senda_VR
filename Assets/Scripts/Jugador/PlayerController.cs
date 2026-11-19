using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using TrackedPoseDriver = UnityEngine.InputSystem.XR.TrackedPoseDriver;

/// <summary>
/// Controlador en primera persona que funciona en PC y en VR.
///
/// PC (sin visor):  WASD para caminar, mouse para mirar, Shift para correr,
///                  Espacio para saltar, Esc para soltar el mouse.
/// VR (con visor):  joystick izquierdo para caminar (clic para correr),
///                  joystick derecho para girar a saltos (snap turn),
///                  la cabeza mueve la cámara.
///
/// Jerarquía esperada (el menú "Senda > Crear jugador en primera persona" la arma sola):
///   Jugador            (CharacterController + este script)
///   └─ CameraOffset    (objeto vacío)
///      └─ Main Camera
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Objeto vacío, hijo del jugador, que contiene la cámara.")]
    public Transform cameraOffset;
    [Tooltip("La cámara del jugador (hija de CameraOffset).")]
    public Camera playerCamera;

    [Header("Movimiento")]
    [Tooltip("Velocidad al caminar, en metros por segundo.")]
    public float walkSpeed = 2.5f;
    [Tooltip("Velocidad al correr, en metros por segundo.")]
    public float sprintSpeed = 4.5f;
    public float gravity = -9.81f;
    [Tooltip("Altura del salto en metros (solo en PC; en VR saltar marea).")]
    public float jumpHeight = 1.0f;

    [Header("Modo PC (teclado y mouse)")]
    public float mouseSensitivity = 0.1f;
    [Tooltip("Cuánto se puede mirar hacia arriba/abajo, en grados.")]
    public float maxPitch = 85f;
    [Tooltip("Altura de los ojos en modo PC. En VR la da el visor.")]
    public float eyeHeight = 1.7f;

    [Header("Modo VR")]
    [Tooltip("Grados que gira el jugador con cada empujón del joystick derecho.")]
    public float snapTurnAngle = 30f;

    CharacterController controller;
    InputAction moveAction, lookAction, sprintAction, jumpAction, turnAction;
    TrackedPoseDriver poseDriver;

    bool vrActive;
    float pitch;
    float verticalVelocity;
    bool turnReady = true;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();
        if (cameraOffset == null && playerCamera != null) cameraOffset = playerCamera.transform.parent;

        // --- Acciones de input (Input System). Cada acción acepta teclado, gamepad y controles VR. ---
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

        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        jumpAction.AddBinding("<Gamepad>/buttonSouth");

        turnAction = new InputAction("Turn", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}", expectedControlType: "Vector2");
    }

    void OnEnable()
    {
        moveAction.Enable(); lookAction.Enable(); sprintAction.Enable();
        jumpAction.Enable(); turnAction.Enable();
    }

    void OnDisable()
    {
        moveAction.Disable(); lookAction.Disable(); sprintAction.Disable();
        jumpAction.Disable(); turnAction.Disable();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        // ¿Hay un visor VR conectado y funcionando?
        vrActive = XRSettings.isDeviceActive;

        if (vrActive) SetupVR();
        else SetupPC();
    }

    // ---------------------------------------------------------------- Configuración

    void SetupPC()
    {
        if (poseDriver != null) poseDriver.enabled = false;
        if (cameraOffset != null) cameraOffset.localPosition = new Vector3(0f, eyeHeight, 0f);
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

        // El TrackedPoseDriver mueve la cámara según la cabeza, con la menor latencia posible.
        poseDriver = playerCamera.GetComponent<TrackedPoseDriver>();
        if (poseDriver == null) poseDriver = playerCamera.gameObject.AddComponent<TrackedPoseDriver>();
        poseDriver.enabled = false;
        poseDriver.positionInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3"));
        poseDriver.rotationInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion"));
        poseDriver.trackingStateInput = new InputActionProperty(new InputAction(binding: "<XRHMD>/trackingState", expectedControlType: "Integer"));
        poseDriver.enabled = true;

        LockCursor(false);
    }

    // ---------------------------------------------------------------- Cada frame

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

        if (vrActive)
        {
            SnapTurn();
            FollowHeadWithCapsule();
        }
        else
        {
            HandleCursor();
            MouseLook();
        }

        Move();
    }

    void MouseLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 delta = lookAction.ReadValue<Vector2>() * mouseSensitivity;
        transform.Rotate(0f, delta.x, 0f);                       // girar el cuerpo
        pitch = Mathf.Clamp(pitch - delta.y, -maxPitch, maxPitch); // inclinar la cabeza
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void SnapTurn()
    {
        float x = turnAction.ReadValue<Vector2>().x;

        if (turnReady && Mathf.Abs(x) > 0.7f)
        {
            // Girar alrededor de la cabeza para que no se sienta como un desplazamiento.
            transform.RotateAround(playerCamera.transform.position, Vector3.up, Mathf.Sign(x) * snapTurnAngle);
            turnReady = false;
        }
        else if (Mathf.Abs(x) < 0.3f)
        {
            turnReady = true; // hay que soltar el joystick antes de volver a girar
        }
    }

    // En VR la persona puede caminar físicamente por su pieza;
    // la cápsula de colisión sigue la cabeza para no atravesar paredes.
    void FollowHeadWithCapsule()
    {
        Vector3 head = transform.InverseTransformPoint(playerCamera.transform.position);
        controller.center = new Vector3(head.x, controller.height * 0.5f + controller.skinWidth, head.z);
    }

    void Move()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();

        // Caminar hacia donde mira la cámara, pero sin subir ni bajar.
        Vector3 forward = playerCamera.transform.forward; forward.y = 0f; forward.Normalize();
        Vector3 right = playerCamera.transform.right; right.y = 0f; right.Normalize();
        Vector3 direction = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);

        float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;

        // Gravedad y salto
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (!vrActive && controller.isGrounded && jumpAction.WasPressedThisFrame())
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = direction * speed + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    // ---------------------------------------------------------------- Mouse

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
