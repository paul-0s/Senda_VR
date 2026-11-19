using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SendaVR.EditorTools
{
    /// <summary>
    /// "Senda.vr > ▶ Probar experiencia completa" (tecla F5):
    /// guarda, y da Play siempre desde la escena Menu, para recorrer todo de corrido:
    /// Splash → Términos → Login → Bienvenida → Cuarto → Sendero.
    /// Al detener el Play, el botón Play normal vuelve a funcionar como siempre (escena abierta).
    /// </summary>
    [InitializeOnLoad]
    public static partial class ConstructorMenuSenda
    {
        const string ClaveProbando = "SendaVR_ProbandoExperiencia";

        static ConstructorMenuSenda()
        {
            EditorApplication.playModeStateChanged += estado =>
            {
                if (estado == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(ClaveProbando, false))
                {
                    EditorSceneManager.playModeStartScene = null;
                    SessionState.SetBool(ClaveProbando, false);
                }
            };
        }

        [MenuItem("Senda.vr/▶ Probar experiencia completa _F5", priority = 0)]
        static void ProbarExperiencia()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false; // F5 otra vez = detener
                return;
            }

            var menu = AssetDatabase.LoadAssetAtPath<SceneAsset>(EscenaMenu);
            if (menu == null)
            {
                EditorUtility.DisplayDialog("Senda.vr", "No encontré la escena del menú en " + EscenaMenu + ".", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AgregarABuild(); // Menu, Cuarto y el sendero deben estar en la lista para poder cargarse
            EditorSceneManager.playModeStartScene = menu;
            SessionState.SetBool(ClaveProbando, true);
            EditorApplication.isPlaying = true;
        }
    }
}
