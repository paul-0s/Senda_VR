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
        float giroX, giroY;

        void Update()
        {
            if (XRSettings.isDeviceActive) return;
            var mouse = Mouse.current;
            if (mouse == null) return;

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
