using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Agrega el menú "Senda > Crear jugador en primera persona".
/// Arma el jugador completo en la escena abierta y mueve ahí la Main Camera existente.
/// Se puede deshacer con Ctrl+Z.
/// </summary>
public static class CrearJugador
{
    const float EyeHeight = 1.7f;

    [MenuItem("Senda/Crear jugador en primera persona")]
    static void Crear()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            EditorUtility.DisplayDialog("Crear jugador",
                "No encontré una cámara con el tag MainCamera en la escena.", "OK");
            return;
        }

        if (cam.GetComponentInParent<PlayerController>() != null)
        {
            EditorUtility.DisplayDialog("Crear jugador",
                "La Main Camera ya es parte de un jugador.", "OK");
            return;
        }

        Undo.SetCurrentGroupName("Crear jugador en primera persona");
        int undoGroup = Undo.GetCurrentGroup();

        // Ubicar al jugador donde está la cámara, apoyado en el suelo si hay uno debajo.
        Vector3 pos = cam.transform.position - Vector3.up * EyeHeight;
        if (Physics.Raycast(cam.transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 100f))
            pos = hit.point;

        // Jugador (cápsula de colisión)
        var player = new GameObject("Jugador");
        Undo.RegisterCreatedObjectUndo(player, "Crear jugador");
        player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f));

        var cc = Undo.AddComponent<CharacterController>(player);
        cc.height = 1.8f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.3f;
        cc.slopeLimit = 50f;

        // CameraOffset (altura de los ojos)
        var offset = new GameObject("CameraOffset");
        Undo.RegisterCreatedObjectUndo(offset, "Crear CameraOffset");
        Undo.SetTransformParent(offset.transform, player.transform, "Parent CameraOffset");
        offset.transform.localPosition = new Vector3(0f, EyeHeight, 0f);
        offset.transform.localRotation = Quaternion.identity;

        // Mover la Main Camera adentro
        Undo.SetTransformParent(cam.transform, offset.transform, "Mover Main Camera");
        Undo.RecordObject(cam.transform, "Resetear Main Camera");
        cam.transform.localPosition = Vector3.zero;
        cam.transform.localRotation = Quaternion.identity;
        Undo.RecordObject(cam, "Ajustar near plane");
        cam.nearClipPlane = 0.05f; // que no se corten las cosas al acercarse (importante en VR)

        // Script
        var pc = Undo.AddComponent<PlayerController>(player);
        pc.cameraOffset = offset.transform;
        pc.playerCamera = cam;

        Undo.CollapseUndoOperations(undoGroup);
        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(player.scene);
        Debug.Log("Jugador creado. Dale Play para probarlo con teclado y mouse.");
    }
}
