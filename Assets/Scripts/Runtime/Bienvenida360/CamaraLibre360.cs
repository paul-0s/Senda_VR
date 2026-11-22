using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace SendaVR
{
    /// <summary>
    /// Para probar el 360 sin visor: clic para capturar el mouse y mirar alrededor; Esc lo suelta.
    /// Con visor (VR) no hace nada: la cabeza mueve la cámara (TrackedPoseDriver en la cámara hija).
    /// Va en el objeto padre de la cámara.
    /// </summary>
    public class CamaraLibre360 : MonoBehaviour
    {
        public float sensibilidad = 0.12f;
        [Tooltip("Para pantallas con botones: se mira alrededor manteniendo el CLIC DERECHO y el mouse queda libre para hacer clic.")]
        public bool usarClicDerecho;
        float giroX, giroY;

        void Start()
        {
            // En pantallas con botones el mouse debe estar libre (pudo quedar capturado en la escena anterior)
            if (usarClicDerecho && !XRSettings.isDeviceActive)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        void Update()
        {
            if (XRSettings.isDeviceActive) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (usarClicDerecho)
            {
                if (!mouse.rightButton.isPressed) return;
                var dd = mouse.delta.ReadValue();
                giroY += dd.x * sensibilidad;
                giroX = Mathf.Clamp(giroX - dd.y * sensibilidad, -85f, 85f);
                transform.localRotation = Quaternion.Euler(giroX, giroY, 0f);
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Cursor.lockState != CursorLockMode.Locked) return;

            var d = mouse.delta.ReadValue();
            giroY += d.x * sensibilidad;
            giroX = Mathf.Clamp(giroX - d.y * sensibilidad, -85f, 85f);
            transform.localRotation = Quaternion.Euler(giroX, giroY, 0f);
        }
    }
}
