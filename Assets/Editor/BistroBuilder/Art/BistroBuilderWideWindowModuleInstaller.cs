using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BistroBuilderWideWindowModuleInstaller
{
    private const string AssetName = "BB_WindowModule_3000x2500_B";
    private const string Root = "Assets/Art/Blender/Architecture/Windows/" + AssetName;
    private const string FbxPath = Root + "/Models/" + AssetName + ".fbx";
    private const string MaterialsPath = Root + "/Materials";
    private const string PrefabsPath = Root + "/Prefabs";
    private const string FrameMaterialPath = MaterialsPath + "/BB_MAT_WindowFrame_Dark.mat";
    private const string GlassMaterialPath = MaterialsPath + "/BB_MAT_Glass_Clear.mat";
    private const string PrefabPath = PrefabsPath + "/" + AssetName + ".prefab";

    [MenuItem("Tools/Bistro Builder/Art/Install Window Module 3000x2500 B")]
    public static void Install()
    {
        Directory.CreateDirectory(MaterialsPath);
        Directory.CreateDirectory(PrefabsPath);
        ConfigureModelImporter();
        var frameMaterial = CreateFrameMaterial();
        var glassMaterial = CreateGlassMaterial();
        BuildPrefab(frameMaterial, glassMaterial);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Validate();
    }

    public static void InstallFromCommandLine()
    {
        Install();
    }

    private static void ConfigureModelImporter()
    {
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        if (!(AssetImporter.GetAtPath(FbxPath) is ModelImporter importer))
            throw new InvalidOperationException("No se pudo cargar ModelImporter: " + FbxPath);
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.isReadable = false;
        importer.globalScale = 1f;
        importer.useFileScale = true;
        importer.SaveAndReimport();
    }

    private static Material GetOrCreateMaterial(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            throw new InvalidOperationException("No se encontro Universal Render Pipeline/Lit.");
        material = new Material(shader) { enableInstancing = true };
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Material CreateFrameMaterial()
    {
        var material = GetOrCreateMaterial(FrameMaterialPath);
        material.name = "BB_MAT_WindowFrame_Dark";
        SetColor(material, new Color(0.025f, 0.03f, 0.035f, 1f));
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.75f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.78f);
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = -1;
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material CreateGlassMaterial()
    {
        var material = GetOrCreateMaterial(GlassMaterialPath);
        material.name = "BB_MAT_Glass_Clear";
        SetColor(material, new Color(0.78f, 0.88f, 0.92f, 0.18f));
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.955f);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildPrefab(Material frameMaterial, Material glassMaterial)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
        if (model == null)
            throw new InvalidOperationException("No se pudo importar el FBX.");
        var instance = UnityEngine.Object.Instantiate(model);
        instance.name = AssetName;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;

        var frame = FindChild(instance.transform, "Frame");
        var glass = FindChild(instance.transform, "Glass");
        if (frame == null || glass == null)
            throw new InvalidOperationException("El FBX debe contener Frame y Glass.");
        var frameRenderer = frame.GetComponent<MeshRenderer>();
        var glassRenderer = glass.GetComponent<MeshRenderer>();
        if (frameRenderer == null || glassRenderer == null)
            throw new InvalidOperationException("Frame/Glass sin MeshRenderer.");
        frameRenderer.sharedMaterial = frameMaterial;
        glassRenderer.sharedMaterial = glassMaterial;
        glassRenderer.shadowCastingMode = ShadowCastingMode.Off;
        glassRenderer.receiveShadows = false;
        glassRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;

        var collider = instance.GetComponent<BoxCollider>();
        if (collider == null) collider = instance.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 1.25f, 0f);
        collider.size = new Vector3(3.00f, 2.50f, 0.07f);

        PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
        UnityEngine.Object.DestroyImmediate(instance);
    }

    private static Transform FindChild(Transform root, string name)
    {
        foreach (var child in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(child.name, name, StringComparison.Ordinal))
                return child;
        return null;
    }

    private static void SetColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
    }

    private static void Validate()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var glassMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);
        if (prefab == null || glassMaterial == null)
            throw new InvalidOperationException("Prefab/material de cristal no generado.");
        var glass = FindChild(prefab.transform, "Glass");
        var frame = FindChild(prefab.transform, "Frame");
        var collider = prefab.GetComponent<BoxCollider>();
        if (glass == null || frame == null || collider == null)
            throw new InvalidOperationException("Estructura de prefab incompleta.");
        var glassRenderer = glass.GetComponent<MeshRenderer>();
        if (glassRenderer == null || glassRenderer.sharedMaterial != glassMaterial)
            throw new InvalidOperationException("Glass no usa BB_MAT_Glass_Clear.");
        if (glassMaterial.renderQueue != (int)RenderQueue.Transparent)
            throw new InvalidOperationException("Material de cristal no es transparente.");
        if (glassMaterial.HasProperty("_Cull") && glassMaterial.GetFloat("_Cull") != (float)CullMode.Off)
            throw new InvalidOperationException("Cristal no configurado a doble cara.");
        if (Vector3.Distance(collider.size, new Vector3(3.00f, 2.50f, 0.07f)) > 0.001f)
            throw new InvalidOperationException("Collider fuera de medida.");
        var alpha = glassMaterial.HasProperty("_BaseColor") ? glassMaterial.GetColor("_BaseColor").a : 0f;
        if (Mathf.Abs(alpha - 0.18f) > 0.001f)
            throw new InvalidOperationException("Alpha de cristal inesperado: " + alpha);
        Debug.Log("BB_WINDOW_UNITY_PASS|ASSET=" + AssetName + "|SIZE=3.00x2.50x0.07|GLASS_ALPHA=0.18|DOUBLE_SIDED=1");
    }
}

