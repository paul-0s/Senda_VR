using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRHMD = UnityEngine.InputSystem.XR.XRHMD;

namespace SendaVR
{
    /// <summary>
    /// Ubica este objeto (por ejemplo el canvas del menú) frente a la cámara, a la altura de los ojos.
    /// Sirve en PC y en VR:
    ///  - En PC lo ubica al empezar.
    ///  - En VR espera a que el visor entregue la posición real de la cabeza (con Air Link / Quest Link
    ///    puede tardar 1-2 segundos después del Play) y recién ahí lo pone frente a ti.
    ///  - En VR, el botón B o Y de los controles lo vuelve a traer frente a ti cuando quieras.
    /// </summary>
    public class PanelFrente : MonoBehaviour
    {
        public float distancia = 2.2f;
        [Tooltip("Cuánto más abajo de los ojos queda el centro del panel (metros).")]
        public float bajarCentro = 0.1f;
        [Tooltip("En VR, el botón B o Y de los controles vuelve a poner el panel frente a ti.")]
        public bool recentrarConBoton = true;

        InputAction recentrar;
        bool ubicadoEnVR;
        bool sueloPedido;
        int cuadrosRastreando;
        static readonly List<XRInputSubsystem> entradasXR = new List<XRInputSubsystem>();
        static readonly List<XRDisplaySubsystem> pantallasXR = new List<XRDisplaySubsystem>();

        void OnEnable()
        {
            recentrar = new InputAction("Recentrar menu", InputActionType.Button);
            recentrar.AddBinding("<XRController>{RightHand}/{SecondaryButton}");
            recentrar.AddBinding("<XRController>{LeftHand}/{SecondaryButton}");
            recentrar.Enable();
        }

        void OnDisable()
        {
            if (recentrar == null) return;
            recentrar.Disable();
            recentrar.Dispose();
            recentrar = null;
        }

        IEnumerator Start()
        {
            yield return null; // un cuadro, para que la cámara esté lista
            Ubicar();          // en PC queda listo; en VR es una posición provisoria
        }

        void Update()
        {
            // En VR: esperar a que la cabeza esté rastreada de verdad y recién ahí ubicar el panel
            if (!ubicadoEnVR && CabezaRastreada())
            {
                if (!sueloPedido)
                {
                    UsarSueloComoReferencia(); // la altura de los ojos medida desde el piso real
                    sueloPedido = true;
                    cuadrosRastreando = 0;
                }
                cuadrosRastreando++;
                if (cuadrosRastreando >= 5) // unos cuadros para que la pose se asiente
                {
                    Ubicar();
                    ubicadoEnVR = true;
                }
            }

            if (recentrarConBoton && recentrar != null && recentrar.WasPressedThisFrame())
                Ubicar();
        }

        /// <summary>Pone el panel frente a la cámara, mirando hacia ella.</summary>
        public void Ubicar()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var adelante = cam.transform.forward; adelante.y = 0f;
            if (adelante.sqrMagnitude < 0.001f) adelante = Vector3.forward;
            adelante.Normalize();
            transform.position = cam.transform.position + adelante * distancia + Vector3.down * bajarCentro;
            transform.rotation = Quaternion.LookRotation(adelante, Vector3.up);
        }

        static bool CabezaRastreada()
        {
            bool visor = XRSettings.isDeviceActive;
            if (!visor)
            {
                SubsystemManager.GetSubsystems(pantallasXR);
                foreach (var d in pantallasXR) if (d.running) { visor = true; break; }
            }
            if (!visor) return false;

            var hmd = InputSystem.GetDevice<XRHMD>();
            return hmd != null && hmd.isTracked.isPressed;
        }

        static void UsarSueloComoReferencia()
        {
            SubsystemManager.GetSubsystems(entradasXR);
            foreach (var s in entradasXR) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor);
        }
    }
}
