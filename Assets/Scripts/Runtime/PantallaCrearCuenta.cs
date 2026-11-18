using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>
    /// Crear cuenta en dos pasos (las dos pantallas "¡Rellena tus datos!" de Figma).
    /// Paso 1: correo, nombre, fecha, teléfono. Paso 2: contraseña y "Recuérdame".
    /// Prototipo: no guarda contraseñas, solo valida y vuelve al inicio de sesión.
    /// </summary>
    public class PantallaCrearCuenta : PantallaBase
    {
        public enum Paso { DatosPersonales, Contrasena }
        public Paso paso = Paso.DatosPersonales;

        public RectTransform panel;
        public Button botonVolver;
        public Button botonCancelar;
        public Button botonContinuar;
        public TextMeshProUGUI mensaje;

        [Header("Paso 1")]
        public TMP_InputField campoCorreo;
        public TMP_InputField campoNombre;
        public TMP_InputField campoFecha;
        public TMP_InputField campoTelefono;

        [Header("Paso 2")]
        public TMP_InputField campoContrasena;
        public TMP_InputField campoRepetir;
        public Toggle recuerdame;

        [Header("Navegación")]
        public PantallaBase pantallaAnterior;   // Paso 1 → Login, Paso 2 → Paso 1
        public PantallaBase pantallaSiguiente;  // Paso 1 → Paso 2
        public PantallaLogin login;
        public PantallaCrearCuenta paso1;       // solo para el paso 2 (leer el correo)

        Vector2 posPanel;
        bool posGuardada;

        protected override void Awake()
        {
            base.Awake();
            botonVolver.onClick.AddListener(() => IrA(pantallaAnterior));
            botonCancelar.onClick.AddListener(() => IrA(login));
            botonContinuar.onClick.AddListener(Continuar);
        }

        protected override void AlMostrar()
        {
            if (!posGuardada) { posPanel = panel.anchoredPosition; posGuardada = true; }
            if (mensaje != null) mensaje.text = "";
            StartCoroutine(Entrada());
        }

        IEnumerator Entrada()
        {
            var desde = posPanel + new Vector2(60f, 0f);
            yield return Animar(0.5f, t => panel.anchoredPosition = Vector2.LerpUnclamped(desde, posPanel, t), SendaEstilo.EaseOutCubic);
        }

        void Continuar()
        {
            if (paso == Paso.DatosPersonales)
            {
                if (Vacio(campoCorreo) || Vacio(campoNombre) || Vacio(campoFecha) || Vacio(campoTelefono))
                { Error("Completa todos los datos para continuar."); return; }
                if (!campoCorreo.text.Contains("@")) { Error("Revisa tu correo electrónico."); return; }
                IrA(pantallaSiguiente);
            }
            else
            {
                if (campoContrasena.text.Length < 6) { Error("La contraseña debe tener al menos 6 caracteres."); return; }
                if (campoContrasena.text != campoRepetir.text) { Error("Las contraseñas no coinciden."); return; }
                if (login != null)
                {
                    if (paso1 != null) login.PrecargarCorreo(paso1.campoCorreo.text.Trim());
                    login.MostrarMensaje("¡Cuenta creada! Ya puedes iniciar sesión.", false);
                }
                campoContrasena.text = campoRepetir.text = "";
                IrA(login);
            }
        }

        static bool Vacio(TMP_InputField c) => c == null || c.text.Trim().Length == 0;

        void Error(string texto)
        {
            if (mensaje != null) { mensaje.text = texto; mensaje.color = SendaEstilo.Error; }
            StartCoroutine(Sacudir());
        }

        IEnumerator Sacudir()
        {
            yield return Animar(0.4f, t =>
            {
                float x = Mathf.Sin(t * Mathf.PI * 6f) * 14f * (1f - t);
                panel.anchoredPosition = posPanel + new Vector2(x, 0f);
            });
        }
    }
}
