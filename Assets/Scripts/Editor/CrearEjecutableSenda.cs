using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SendaVR.EditorTools
{
    /// <summary>
    /// "Senda.vr > Crear ejecutable para Windows": arma Senda.vr como programa (.exe)
    /// en el Escritorio, en la carpeta "SendaVR_Ejecutable", para mostrarlo en otro computador.
    /// </summary>
    public static partial class ConstructorMenuSenda
    {
        [MenuItem("Senda.vr/Crear ejecutable para Windows (.exe)", priority = 60)]
        static void CrearEjecutable()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Senda.vr", "Detén el Play ▶ antes de crear el ejecutable.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            AgregarABuild();
            var escenas = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (escenas.Length == 0 || escenas[0] != EscenaMenu)
            {
                EditorUtility.DisplayDialog("Senda.vr", "La primera escena de la lista debe ser " + EscenaMenu + ".", "OK");
                return;
            }

            string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "SendaVR_Ejecutable");
            string exe = Path.Combine(carpeta, "SendaVR.exe");

            if (!EditorUtility.DisplayDialog("Senda.vr",
                    "Se creará el ejecutable en:\n" + carpeta + "\n\nEscenas incluidas:\n• " +
                    string.Join("\n• ", escenas.Select(Path.GetFileNameWithoutExtension)) +
                    "\n\nPuede tardar varios minutos (sobre todo la primera vez). Unity quedará ocupado mientras tanto.",
                    "Crear", "Cancelar"))
                return;

            // Datos del programa
            PlayerSettings.productName = "Senda.vr";
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultIsNativeResolution = true;
            PlayerSettings.runInBackground = true;

            if (Directory.Exists(carpeta))
            {
                try { Directory.Delete(carpeta, true); }
                catch (Exception e) { Debug.LogWarning("[Senda.vr] No pude borrar el ejecutable anterior (¿está abierto?): " + e.Message); }
            }

            var opciones = new BuildPlayerOptions
            {
                scenes = escenas,
                locationPathName = exe,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            BuildReport reporte = BuildPipeline.BuildPlayer(opciones);
            var r = reporte.summary;

            if (r.result == BuildResult.Succeeded)
            {
                double mb = r.totalSize / (1024.0 * 1024.0);
                EditorUtility.DisplayDialog("Senda.vr",
                    "¡Ejecutable listo! (" + mb.ToString("0") + " MB, " + r.totalTime.TotalMinutes.ToString("0.0") + " min)\n\n" +
                    "Está en el Escritorio, carpeta SendaVR_Ejecutable.\n" +
                    "Para abrirlo: doble clic en SendaVR.exe.\n\n" +
                    "Para llevarlo a otro computador copia la CARPETA COMPLETA (o comprímela en .zip); " +
                    "el .exe solo no funciona.\n\nPara cerrar el programa: Alt + F4.", "Abrir carpeta");
                EditorUtility.RevealInFinder(exe);
            }
            else
            {
                var errores = reporte.steps.SelectMany(s => s.messages)
                    .Where(m => m.type == LogType.Error || m.type == LogType.Exception)
                    .Select(m => m.content).Take(5).ToList();
                EditorUtility.DisplayDialog("Senda.vr",
                    "No se pudo crear el ejecutable (" + r.result + ").\n\n" +
                    (errores.Count > 0 ? string.Join("\n\n", errores) : "Revisa los errores rojos en la Console.") +
                    "\n\nSi dice que falta 'Windows Build Support', instálalo desde Unity Hub > Installs > (tu versión) > Add modules.",
                    "OK");
            }
        }
    }
}
