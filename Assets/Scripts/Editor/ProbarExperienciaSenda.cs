using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SendaVR.EditorTools
{
    /// <summary>
    /// "Senda.vr > ▶ Probar experiencia completa" (tecla F5):
    /// guarda, y da Play desde la primera escena del recorrido (Logo360 si existe; si no, Menu):
    /// Logo 360 → Términos → Login → Bienvenida 360 → Cuarto → Sendero.
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

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AgregarABuild(); // ordena el recorrido (Logo360 primero si existe; si no, Menu)
            var primera = EditorBuildSettings.scenes.FirstOrDefault(s => s.enabled);
            var inicio = primera != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(primera.path) : null;
            if (inicio == null)
            {
                EditorUtility.DisplayDialog("Senda.vr", "No encontré la escena inicial del recorrido.", "OK");
                return;
            }
            EditorSceneManager.playModeStartScene = inicio;
            SessionState.SetBool(ClaveProbando, true);
            EditorApplication.isPlaying = true;
        }
    }
}
