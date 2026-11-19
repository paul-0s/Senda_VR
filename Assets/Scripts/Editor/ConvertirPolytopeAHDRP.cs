using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Menú "Senda > Convertir Polytope a HDRP".
///
/// Los assets gratis de Polytope Studio traen shaders que solo funcionan en URP,
/// por eso en HDRP se ven morados. Esta herramienta cambia esos materiales al shader
/// estándar de HDRP (HDRP/Lit), conservando su textura y su color:
///  - hojas, pasto y flores: recorte de transparencia (Alpha Clipping) y doble cara;
///  - agua: transparente;
///  - terreno: HDRP/TerrainLit.
/// Se pierden el viento y el degradado automático de color de los shaders originales.
/// </summary>
public static class ConvertirPolytopeAHDRP
{
    const string Carpeta = "Assets/Polytope Studio";

    // Nombres de propiedades donde Polytope (y otros) guardan la textura principal
    static readonly string[] NombresTextura = { "_BaseTexture", "_BaseMap", "_BaseColorMap", "_MainTex", "_Albedo", "_Texture" };
    static readonly string[] PalabrasVegetacion = { "leaf", "leaves", "foliage", "grass", "poppy", "flower", "plant" };

    [MenuItem("Senda/Convertir Polytope a HDRP")]
    static void Convertir()
    {
        Shader lit = Shader.Find("HDRP/Lit");
        if (lit == null)
        {
            EditorUtility.DisplayDialog("Convertir Polytope",
                "No encontré el shader HDRP/Lit. Revisa que el proyecto esté usando HDRP.", "OK");
            return;
        }

        // 1. Buscar materiales que no funcionan en HDRP
        var aConvertir = new List<Material>();
        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { Carpeta }))
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (mat != null && NecesitaConversion(mat)) aConvertir.Add(mat);
        }

        if (aConvertir.Count == 0)
        {
            EditorUtility.DisplayDialog("Convertir Polytope", "No hay materiales morados en Polytope Studio. ¡Todo listo!", "OK");
            return;
        }

        string lista = string.Join("\n", aConvertir.ConvertAll(m => "• " + m.name));
        if (aConvertir.Count > 25) lista = $"({aConvertir.Count} materiales)";
        if (!EditorUtility.DisplayDialog("Convertir Polytope a HDRP",
                $"Voy a convertir {aConvertir.Count} materiales a HDRP/Lit:\n\n{lista}\n\n" +
                "Se pierden el viento y el degradado de color originales. Se puede deshacer con Ctrl+Z.",
                "Convertir", "Cancelar"))
            return;

        Undo.RecordObjects(aConvertir.ToArray(), "Convertir Polytope a HDRP");

        // 2. Convertir
        int hechos = 0;
        try
        {
            for (int i = 0; i < aConvertir.Count; i++)
            {
                Material mat = aConvertir[i];
                EditorUtility.DisplayProgressBar("Convertir Polytope a HDRP", mat.name, (float)i / aConvertir.Count);
                try
                {
                    ConvertirMaterial(mat, lit);
                    hechos++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"No pude convertir {mat.name}: {e.Message}", mat);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Convertir Polytope a HDRP: {hechos} de {aConvertir.Count} materiales convertidos.");
        EditorUtility.DisplayDialog("Convertir Polytope a HDRP", $"Listo: {hechos} materiales convertidos.", "OK");
    }

    static bool NecesitaConversion(Material mat)
    {
        Shader s = mat.shader;
        if (s == null) return true;
        string n = s.name;
        if (n.StartsWith("HDRP/") || n.StartsWith("Skybox/")) return false;
        return n == "Hidden/InternalErrorShader" || !s.isSupported
               || n.StartsWith("Polytope Studio") || n.StartsWith("Universal Render Pipeline");
    }

    // ------------------------------------------------------------------ Conversión

    static void ConvertirMaterial(Material mat, Shader lit)
    {
        string shaderOriginal = mat.shader != null ? mat.shader.name : "";
        string nombre = mat.name.ToLowerInvariant();

        // Leer las propiedades guardadas ANTES de cambiar el shader
        LeerPropiedades(mat, out var texturas, out var numeros, out var colores);

        // Terreno
        if (nombre.Contains("terrain") || shaderOriginal.ToLowerInvariant().Contains("terrain"))
        {
            Shader terrainLit = Shader.Find("HDRP/TerrainLit");
            if (terrainLit != null) mat.shader = terrainLit;
            EditorUtility.SetDirty(mat);
            return;
        }

        bool esAgua = nombre.Contains("water") || shaderOriginal.Contains("Water");
        bool esVegetacion = !esAgua && (ContieneAlguna(nombre, PalabrasVegetacion)
                            || shaderOriginal.Contains("Foliage") || shaderOriginal.Contains("Plants")
                            || shaderOriginal.Contains("Flowers"));

        Texture principal = BuscarTexturaPrincipal(texturas);
        Texture normal = BuscarTextura(texturas, "normal", "bump", "_nm");

        // Color: si el material usaba el teñido personalizado de Polytope, el color real
        // venía de TopColor/GroundColor y no de la textura.
        Color color = Color.white;
        bool tenido = mat.IsKeywordEnabled("_CUSTOMCOLORSTINTING_ON")
                      || (numeros.TryGetValue("_CUSTOMCOLORSTINTING", out float t) && t > 0.5f);
        if (tenido && colores.TryGetValue("_TopColor", out Color top) && colores.TryGetValue("_GroundColor", out Color ground))
        {
            float gradiente = numeros.TryGetValue("_Gradient", out float g) ? g : 1f;
            Color objetivo = Color.Lerp(ground, top, Mathf.Clamp01(1.25f * gradiente));
            Color promedio = principal != null ? ColorPromedio(principal) : Color.white;
            // HDRP multiplica textura × color, así que compensamos el color propio de la textura
            color = new Color(
                Mathf.Clamp01(objetivo.r / Mathf.Max(promedio.r, 0.05f)),
                Mathf.Clamp01(objetivo.g / Mathf.Max(promedio.g, 0.05f)),
                Mathf.Clamp01(objetivo.b / Mathf.Max(promedio.b, 0.05f)), 1f);
        }
        else if (principal == null)
        {
            if (colores.TryGetValue("_Color", out Color colorBase) || colores.TryGetValue("_BaseColor", out colorBase)) color = colorBase;
        }

        if (esAgua)
        {
            color = BuscarColor(colores, "water", "shallow", "deep") ?? new Color(0.15f, 0.45f, 0.5f, 1f);
            color.a = 0.6f;
        }

        // Cambiar al shader de HDRP
        mat.shader = lit;
        mat.SetTexture("_BaseColorMap", principal);
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", esAgua ? 0.92f : 0.2f);
        if (normal != null) mat.SetTexture("_NormalMap", normal);

        if (esVegetacion)
        {
            float corte = 0.5f;
            if (numeros.TryGetValue("_LeavesThickness", out float grosor)) corte = Mathf.Clamp(1f - grosor, 0.05f, 0.95f);
            else if (numeros.TryGetValue("_Cutoff", out float cutoff)) corte = cutoff;
            mat.SetFloat("_AlphaCutoffEnable", 1f);
            mat.SetFloat("_AlphaCutoff", corte);
            mat.SetFloat("_DoubleSidedEnable", 1f);
        }

        if (esAgua)
        {
            mat.SetFloat("_SurfaceType", 1f); // transparente
            mat.SetFloat("_BlendMode", 0f);   // alpha
        }

        ValidarMaterialHDRP(mat);
        EditorUtility.SetDirty(mat);
    }

    // HDRP necesita recalcular los keywords del material después de cambiar opciones.
    static void ValidarMaterialHDRP(Material mat)
    {
        Type hdMaterial = Type.GetType("UnityEngine.Rendering.HighDefinition.HDMaterial, Unity.RenderPipelines.HighDefinition.Runtime");
        MethodInfo validar = hdMaterial?.GetMethod("ValidateMaterial", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
        if (validar != null) validar.Invoke(null, new object[] { mat });
        else Debug.LogWarning($"No pude validar {mat.name}; ábrelo una vez en el Inspector para que HDRP lo actualice.", mat);
    }

    // ------------------------------------------------------------------ Utilidades

    static void LeerPropiedades(Material mat,
        out Dictionary<string, Texture> texturas,
        out Dictionary<string, float> numeros,
        out Dictionary<string, Color> colores)
    {
        texturas = new Dictionary<string, Texture>();
        numeros = new Dictionary<string, float>();
        colores = new Dictionary<string, Color>();

        var so = new SerializedObject(mat);
        SerializedProperty tex = so.FindProperty("m_SavedProperties.m_TexEnvs");
        for (int i = 0; tex != null && i < tex.arraySize; i++)
        {
            var el = tex.GetArrayElementAtIndex(i);
            var t = el.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            if (t != null) texturas[el.FindPropertyRelative("first").stringValue] = t;
        }
        SerializedProperty flo = so.FindProperty("m_SavedProperties.m_Floats");
        for (int i = 0; flo != null && i < flo.arraySize; i++)
        {
            var el = flo.GetArrayElementAtIndex(i);
            numeros[el.FindPropertyRelative("first").stringValue] = el.FindPropertyRelative("second").floatValue;
        }
        SerializedProperty col = so.FindProperty("m_SavedProperties.m_Colors");
        for (int i = 0; col != null && i < col.arraySize; i++)
        {
            var el = col.GetArrayElementAtIndex(i);
            colores[el.FindPropertyRelative("first").stringValue] = el.FindPropertyRelative("second").colorValue;
        }
    }

    static Texture BuscarTexturaPrincipal(Dictionary<string, Texture> texturas)
    {
        foreach (string n in NombresTextura)
            if (texturas.TryGetValue(n, out Texture t)) return t;

        // Si no, la primera textura que no sea un mapa auxiliar
        string[] auxiliares = { "normal", "bump", "noise", "mask", "decal", "detail", "wind", "projection", "_nm", "snow" };
        foreach (var par in texturas)
            if (!ContieneAlguna(par.Key.ToLowerInvariant(), auxiliares)) return par.Value;
        return null;
    }

    static Texture BuscarTextura(Dictionary<string, Texture> texturas, params string[] palabras)
    {
        foreach (var par in texturas)
            if (ContieneAlguna(par.Key.ToLowerInvariant(), palabras) || ContieneAlguna(par.Value.name.ToLowerInvariant(), palabras))
                return par.Value;
        return null;
    }

    static Color? BuscarColor(Dictionary<string, Color> colores, params string[] palabras)
    {
        foreach (var par in colores)
            if (ContieneAlguna(par.Key.ToLowerInvariant(), palabras)) return par.Value;
        return null;
    }

    static bool ContieneAlguna(string texto, string[] palabras)
    {
        foreach (string p in palabras) if (texto.Contains(p)) return true;
        return false;
    }

    // Color promedio de una textura (solo donde no es transparente), en espacio gamma.
    static Color ColorPromedio(Texture tex)
    {
        const int lado = 32;
        RenderTexture rt = RenderTexture.GetTemporary(lado, lado, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture anterior = RenderTexture.active;
        Texture2D lectura = null;
        try
        {
            Graphics.Blit(tex, rt);
            RenderTexture.active = rt;
            lectura = new Texture2D(lado, lado, TextureFormat.RGBA32, false, false);
            lectura.ReadPixels(new Rect(0, 0, lado, lado), 0, 0);
            lectura.Apply();

            Color suma = Color.clear;
            float peso = 0f;
            foreach (Color px in lectura.GetPixels())
            {
                suma += px * px.a;
                peso += px.a;
            }
            return peso > 0.001f ? suma / peso : Color.white;
        }
        finally
        {
            RenderTexture.active = anterior;
            RenderTexture.ReleaseTemporary(rt);
            if (lectura != null) Object.DestroyImmediate(lectura);
        }
    }
}
