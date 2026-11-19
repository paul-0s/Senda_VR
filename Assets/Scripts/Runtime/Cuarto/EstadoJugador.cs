using UnityEngine;
using UnityEngine.XR;

namespace SendaVR
{
    /// <summary>
    /// Indica si el Jugador debe quedarse quieto y con el mouse libre
    /// (menú abierto o ventana de un objeto abierta).
    /// </summary>
    public static class EstadoJugador
    {
        static int ventanasAbiertas;

        public static bool Bloqueado => NavegadorPantallas.MenuAbierto || ventanasAbiertas > 0;
        public static bool EnVR => XRSettings.isDeviceActive;

        public static void AbrirVentana() => ventanasAbiertas++;

        public static void CerrarVentana()
        {
            ventanasAbiertas = Mathf.Max(0, ventanasAbiertas - 1);
            if (ventanasAbiertas == 0 && !NavegadorPantallas.MenuAbierto && !EnVR)
            {
                // Vuelve a capturar el mouse para seguir mirando
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reiniciar() => ventanasAbiertas = 0;
    }
}
