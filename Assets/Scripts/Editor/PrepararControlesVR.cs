using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;

namespace SendaVR.EditorTools
{
    /// <summary>
    /// "Senda.vr > VR > Agregar controles VR a esta escena".
    ///
    /// Toma los controles del XR Origin de los Starter Assets de XR Interaction Toolkit
    /// (modelo del control con botones animados + rayo para apuntar y agarrar + toque con el dedo)
    /// y los pone dentro del CameraOffset del Jugador (o de la cámara en las escenas 360).
    /// Además deja la escena lista para apuntar a los menús con los controles:
    ///   - Input Action Manager con las acciones de XRI,
    ///   - XR Interaction Manager,
    ///   - EventSystem con XR UI Input Module (el mouse sigue funcionando en PC),
    ///   - Tracked Device Graphic Raycaster en los Canvas.
    /// En PC los controles se esconden solos (MostrarSoloEnVR).
    /// Se puede deshacer con Ctrl+Z.
    /// </summary>
    public static class PrepararControlesVR
    {
        const string NombreIzquierdo = "Control Izquierdo";
        const string NombreDerecho = "Control Derecho";

        // Tipos de XR Interaction Toolkit por nombre, para que el proyecto compile aunque alguien no lo tenga instalado
        const string TipoXRUIInputModule = "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit";
        const string TipoRaycasterVR = "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit";
        const string TipoInputActionManager = "UnityEngine.XR.Interaction.Toolkit.Inputs.InputActionManager, Unity.XR.Interaction.Toolkit";
        const string TipoInteractionManager = "UnityEngine.XR.Interaction.Toolkit.XRInteractionManager, Unity.XR.Interaction.Toolkit";

        [MenuItem("Senda.vr/VR/Agregar controles VR a esta escena", priority = 50)]
        static void Agregar()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog("Controles VR", "Detén el Play primero.", "OK");
                return;
            }

            // 1. Prefab del XR Origin de los Starter Assets
            string rutaPrefab = AssetDatabase.FindAssets("t:Prefab XR Origin")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.Contains("Starter Assets") && p.EndsWith("XR Origin (XR Rig).prefab"));
            Type tIAM = Type.GetType(TipoInputActionManager);
            if (rutaPrefab == null || tIAM == null)
            {
                EditorUtility.DisplayDialog("Controles VR",
                    "No encontré XR Interaction Toolkit con sus Starter Assets.\n\n" +
                    "Window > Package Manager > XR Interaction Toolkit > Samples > Starter Assets > Import.", "OK");
                return;
            }

            // 2. Dónde van los controles: el CameraOffset del Jugador, o el padre de la cámara
            Transform origen = BuscarOrigen();
            if (origen == null)
            {
                EditorUtility.DisplayDialog("Controles VR", "No encontré un Jugador ni una Main Camera en esta escena.", "OK");
                return;
            }
            var existentes = new[] { origen.Find(NombreIzquierdo), origen.Find(NombreDerecho) }
                .Where(t => t != null).Select(t => t.gameObject).ToList();
            bool yaTiene = existentes.Count > 0;

            Undo.SetCurrentGroupName("Agregar controles VR");
            int grupo = Undo.GetCurrentGroup();

            var controles = yaTiene ? existentes : CrearControles(rutaPrefab, origen, grupo);
            if (controles == null) return;

            // Sin XR Origin (usamos nuestro propio Jugador): apagar la estabilización del rayo,
            // que lo necesita y si no lo encuentra tira un error rojo en la Console
            foreach (var c in controles) QuitarDependenciaDeXROrigin(c);

            ContinuarPreparacion(origen, controles, tIAM, grupo, yaTiene);
        }

        static List<GameObject> CrearControles(string rutaPrefab, Transform origen, int grupo)
        {
            // 3. Instanciar el XR Origin, sacarle los controles y borrar el resto
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rutaPrefab);
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab, origen.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(rig, "Crear XR Origin temporal");
            PrefabUtility.UnpackPrefabInstance(rig, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var controles = new List<GameObject>();
            string[] mover =
            {
                "Left Controller", "Right Controller",
                "Left Controller Stabilized Attach", "Right Controller Stabilized Attach",
                "Left Controller Teleport Stabilized Origin", "Right Controller Teleport Stabilized Origin",
            };
            foreach (string nombre in mover)
            {
                Transform t = BuscarHijo(rig.transform, nombre);
                if (t == null) continue;
                Undo.SetTransformParent(t, origen, "Mover control");
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
                if (nombre == "Left Controller") { t.name = NombreIzquierdo; controles.Add(t.gameObject); }
                if (nombre == "Right Controller") { t.name = NombreDerecho; controles.Add(t.gameObject); }
            }
            Undo.DestroyObjectImmediate(rig);

            if (controles.Count == 0)
            {
                Undo.CollapseUndoOperations(grupo);
                Undo.PerformUndo();
                EditorUtility.DisplayDialog("Controles VR", "El XR Origin no tenía 'Left Controller' / 'Right Controller'. No cambié nada.", "OK");
                return null;
            }

            foreach (var c in controles)
            {
                // Sin teletransporte: el movimiento lo hace nuestro PlayerController con el joystick
                foreach (Transform hijo in c.GetComponentsInChildren<Transform>(true))
                    if (hijo != c.transform && hijo.name.Contains("Teleport"))
                    {
                        Undo.RecordObject(hijo.gameObject, "Desactivar teletransporte");
                        hijo.gameObject.SetActive(false);
                    }

                // Este script de los samples cambia a modo teletransporte con el joystick: no lo usamos
                foreach (var mb in c.GetComponents<MonoBehaviour>())
                    if (mb != null && mb.GetType().Name == "ControllerInputActionManager")
                        Undo.DestroyObjectImmediate(mb);
            }
            return controles;
        }

        static void ContinuarPreparacion(Transform origen, List<GameObject> controles, Type tIAM, int grupo, bool yaTiene)
        {
            // 4. Input Action Manager con las acciones de XRI (sin esto los controles no se mueven)
            var accionesXRI = AssetDatabase.FindAssets("XRI Default Input Actions")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<InputActionAsset>)
                .FirstOrDefault(a => a != null);
            Component iam = origen.GetComponent(tIAM);
            if (iam == null) iam = Undo.AddComponent(origen.gameObject, tIAM);
            if (accionesXRI != null)
            {
                var prop = tIAM.GetProperty("actionAssets");
                var lista = prop?.GetValue(iam) as IList ?? new List<InputActionAsset>();
                if (!lista.Contains(accionesXRI)) lista.Add(accionesXRI);
                prop?.SetValue(iam, lista);
                EditorUtility.SetDirty(iam);
            }

            // 5. XR Interaction Manager
            Type tManager = Type.GetType(TipoInteractionManager);
            if (tManager != null && Object.FindAnyObjectByType(tManager) == null)
            {
                var go = new GameObject("XR Interaction Manager");
                SceneManager_Mover(go, origen.gameObject.scene);
                Undo.RegisterCreatedObjectUndo(go, "Crear XR Interaction Manager");
                Undo.AddComponent(go, tManager);
            }

            // 6. EventSystem con XR UI Input Module (apuntar a los menús con los controles; el mouse sigue funcionando)
            PrepararEventSystem(origen.gameObject.scene);

            // 7. Canvas: que los rayos de los controles puedan tocar los botones
            Type tRaycaster = Type.GetType(TipoRaycasterVR);
            if (tRaycaster != null)
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (canvas.isRootCanvas && canvas.gameObject.scene == origen.gameObject.scene && canvas.GetComponent(tRaycaster) == null)
                        Undo.AddComponent(canvas.gameObject, tRaycaster);

            // 8. Esconder los controles en PC
            var mostrar = origen.GetComponent<MostrarSoloEnVR>();
            if (mostrar == null) mostrar = Undo.AddComponent<MostrarSoloEnVR>(origen.gameObject);
            Undo.RecordObject(mostrar, "Controles solo en VR");
            mostrar.objetos = (mostrar.objetos ?? new GameObject[0]).Concat(controles).Distinct().ToArray();
            EditorUtility.SetDirty(mostrar);

            // 9. Menús 360: que los paneles se pongan frente a ti cuando el visor ya sabe dónde está tu cabeza
            int paneles = AgregarPanelFrente(origen.gameObject.scene);

            Undo.CollapseUndoOperations(grupo);
            EditorSceneManager.MarkSceneDirty(origen.gameObject.scene);
            Selection.activeTransform = origen;
            Debug.Log($"[Senda] {(yaTiene ? "Controles VR actualizados" : "Controles VR agregados")} en '{origen.name}'" +
                      (paneles > 0 ? $"; {paneles} panel(es) ahora se ubican frente a ti en VR" : "") +
                      ". Guarda la escena (Ctrl+S) y prueba con el visor.");
        }

        /// <summary>
        /// En las escenas de menú (sin Jugador), agrega PanelFrente a los Canvas en World Space que no lo tienen,
        /// respetando la distancia y la altura a la que el equipo los dejó respecto de la cámara.
        /// </summary>
        static int AgregarPanelFrente(UnityEngine.SceneManagement.Scene escena)
        {
            if (Object.FindAnyObjectByType<PlayerController>() != null) return 0; // en el sendero y el cuarto no se mueve nada
            Camera cam = Camera.main;
            if (cam == null) return 0;

            int n = 0;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.gameObject.scene != escena) continue;
                if (canvas.renderMode != RenderMode.WorldSpace) continue;
                if (canvas.transform.IsChildOf(cam.transform)) continue;
                if (canvas.GetComponent<PanelFrente>() != null) continue;

                Vector3 d = canvas.transform.position - cam.transform.position;
                var pf = Undo.AddComponent<PanelFrente>(canvas.gameObject);
                pf.distancia = Mathf.Max(0.5f, new Vector2(d.x, d.z).magnitude);
                pf.bajarCentro = -d.y;
                EditorUtility.SetDirty(pf);
                n++;
            }
            return n;
        }

        /// <summary>Apaga lo que en los controles de XRI depende de un XR Origin (no lo usamos).</summary>
        static void QuitarDependenciaDeXROrigin(GameObject control)
        {
            foreach (var mb in control.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                foreach (string nombre in new[] { "enableStabilization", "useDistanceBasedVelocityScaling" })
                {
                    var prop = mb.GetType().GetProperty(nombre);
                    if (prop == null || prop.PropertyType != typeof(bool) || !prop.CanWrite) continue;
                    if (!(bool)prop.GetValue(mb)) continue;
                    Undo.RecordObject(mb, "Quitar dependencia de XR Origin");
                    prop.SetValue(mb, false);
                    EditorUtility.SetDirty(mb);
                }
            }
        }

        static Transform BuscarOrigen()
        {
            var pc = Object.FindAnyObjectByType<PlayerController>();
            if (pc != null && pc.cameraOffset != null) return pc.cameraOffset;

            Camera cam = Camera.main;
            if (cam == null) return null;
            if (cam.transform.parent != null) return cam.transform.parent;

            // Escenas 360: la cámara está suelta. Le creamos un padre en el origen (no cambia su posición).
            var origen = new GameObject("Origen VR").transform;
            SceneManager_Mover(origen.gameObject, cam.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(origen.gameObject, "Crear Origen VR");
            Undo.SetTransformParent(cam.transform, origen, "Mover cámara a Origen VR");
            return origen;
        }

        static void PrepararEventSystem(UnityEngine.SceneManagement.Scene escena)
        {
            Type tModulo = Type.GetType(TipoXRUIInputModule);
            if (tModulo == null) return;

            var es = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(e => e.gameObject.scene == escena);
            if (es == null)
            {
                var go = new GameObject("EventSystem");
                SceneManager_Mover(go, escena);
                Undo.RegisterCreatedObjectUndo(go, "Crear EventSystem");
                es = Undo.AddComponent<EventSystem>(go);
            }
            if (es.GetComponent(tModulo) != null) return;

            foreach (var modulo in es.GetComponents<BaseInputModule>())
                Undo.DestroyObjectImmediate(modulo);
            Undo.AddComponent(es.gameObject, tModulo);
        }

        static void SceneManager_Mover(GameObject go, UnityEngine.SceneManagement.Scene escena)
        {
            if (escena.IsValid() && go.scene != escena)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, escena);
        }

        static Transform BuscarHijo(Transform raiz, string nombre)
        {
            foreach (var t in raiz.GetComponentsInChildren<Transform>(true))
                if (t.name == nombre) return t;
            return null;
        }
    }
}
