using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SendaVR
{
    /// <summary>Botón del ojo: muestra u oculta la contraseña.</summary>
    [RequireComponent(typeof(Button))]
    public class MostrarContrasena : MonoBehaviour
    {
        public TMP_InputField campo;
        public Image icono;
        public Sprite ojoAbierto;
        public Sprite ojoTachado;

        bool visible;

        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Alternar);
            Aplicar();
        }

        void Alternar()
        {
            visible = !visible;
            Aplicar();
        }

        void Aplicar()
        {
            if (campo == null) return;
            campo.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
            campo.ForceLabelUpdate();
            if (icono != null) icono.sprite = visible ? ojoAbierto : ojoTachado;
        }
    }
}
