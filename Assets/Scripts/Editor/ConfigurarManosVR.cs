using System;
using System.Reflection;
using SendaVR;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crea Assets/Resources/ConfigManosVR.asset (con los modelos de manos del sample de XR Hands)
/// y un material de piel HDRP. Se ejecuta solo al abrir el proyecto si falta;
/// también está en el menú Senda.vr/VR/Configurar manos VR.
/// </summary>
[InitializeOnLoad]
public static class ConfigurarManosVR
{
    const string Carpeta = "Assets/Resources";
    const string RutaConfig = Carpeta + "/ConfigManosVR.asset";
    const string RutaMaterial = Carpeta + "/ManoVR_Piel.mat";

    static ConfigurarManosVR()
    {
        EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<ConfigManosVR>(RutaConfig) == null)
                Configurar(false);
        };
    }

    [MenuItem("Senda.vr/VR/Configurar manos VR")]
    static void ConfigurarDesdeMenu()
    {
        var cfg = Configurar(true);
        if (cfg != null)
        {
            Selection.activeObject = cfg;
            EditorGUIUtility.PingObject(cfg);
        }
    }

    static ConfigManosVR Configurar(bool avisar)
    {
        var izquierda = BuscarModelo("LeftHand");
        var derecha = BuscarModelo("RightHand");
        if (izquierda == null || derecha == null)
        {
            if (avisar)
                EditorUtility.DisplayDialog("Manos VR",
                    "No encontré LeftHand.fbx / RightHand.fbx.\n\nInstala el paquete XR Hands e importa el sample HandVisualizer (Package Manager → XR Hands → Samples).",
                    "OK");
            return null;
        }

        if (!AssetDatabase.IsValidFolder(Carpeta))
            AssetDatabase.CreateFolder("Assets", "Resources");

        var cfg = AssetDatabase.LoadAssetAtPath<ConfigManosVR>(RutaConfig);
        if (cfg == null)
        {
            cfg = ScriptableObject.CreateInstance<ConfigManosVR>();
            AssetDatabase.CreateAsset(cfg, RutaConfig);
        }

        if (cfg.modeloIzquierdo == null) cfg.modeloIzquierdo = izquierda;
        if (cfg.modeloDerecho == null) cfg.modeloDerecho = derecha;
        if (cfg.material == null) cfg.material = CrearMaterial();

        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log("[Senda] Manos VR configuradas: " + RutaConfig, cfg);
        return cfg;
    }

    static GameObject BuscarModelo(string nombre)
    {
        foreach (var guid in AssetDatabase.FindAssets(nombre + " t:Model"))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            if (ruta.EndsWith("/" + nombre + ".fbx", StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<GameObject>(ruta);
        }
        return null;
    }

    static Material CrearMaterial()
    {
        var existente = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (existente != null) return existente;

        var shader = Shader.Find("HDRP/Lit");
        if (shader == null) return null;
        var mat = new Material(shader);
        mat.SetColor("_BaseColor", new Color(0.87f, 0.72f, 0.62f));
        mat.SetFloat("_Smoothness", 0.35f);
        AssetDatabase.CreateAsset(mat, RutaMaterial);

        Type hdMaterial = Type.GetType("UnityEngine.Rendering.HighDefinition.HDMaterial, Unity.RenderPipelines.HighDefinition.Runtime");
        MethodInfo validar = hdMaterial?.GetMethod("ValidateMaterial", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
        if (validar != null) validar.Invoke(null, new object[] { mat });

        EditorUtility.SetDirty(mat);
        return mat;
    }
}
