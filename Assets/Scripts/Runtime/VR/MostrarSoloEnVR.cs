using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace SendaVR
{
    /// <summary>
    /// Muestra los objetos de la lista solo cuando hay un visor VR activo
    /// (por ejemplo, los controles con sus rayos). En PC los esconde.
    /// Revisa en cada frame, porque con Air Link / Quest Link el visor puede
    /// activarse un momento después de dar Play.
    /// Va en un objeto que siempre esté activo (por ejemplo CameraOffset).
    /// </summary>
    public class MostrarSoloEnVR : MonoBehaviour
    {
        [Tooltip("Objetos que solo se ven en VR.")]
        public GameObject[] objetos;

        bool inicializado;
        bool estabaEnVR;
        static readonly List<XRDisplaySubsystem> pantallas = new List<XRDisplaySubsystem>();

        /// <summary>¿Hay un visor VR conectado y mostrando imagen?</summary>
        public static bool VisorActivo()
        {
            if (XRSettings.isDeviceActive) return true;
            SubsystemManager.GetSubsystems(pantallas);
            foreach (var d in pantallas) if (d.running) return true;
            return false;
        }

        void Update()
        {
            bool vr = VisorActivo();
            if (inicializado && vr == estabaEnVR) return;
            inicializado = true;
            estabaEnVR = vr;
            if (objetos == null) return;
            foreach (var o in objetos) if (o != null) o.SetActive(vr);
        }
    }
}
