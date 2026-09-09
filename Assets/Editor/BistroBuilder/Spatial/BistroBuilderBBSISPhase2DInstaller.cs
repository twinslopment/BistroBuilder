using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instala BBSIS 2D para carritos, logistica y objetos moviles.
/// </summary>
public static class BistroBuilderBBSISPhase2DInstaller
{
    private const string ScenePath =
        "Assets/Scenes/Prototype_Restaurant.unity";
    private const string ContractPath =
        "Assets/Resources/BistroBuilder/Spatial/Contracts/" +
        "BB_SpatialContract_Logistics_Cart.asset";
    private const string ProfilePath =
        "Assets/Resources/BistroBuilder/Spatial/Profiles/" +
        "BB_MobilityProfile_Logistics_Cart.asset";

    [MenuItem("Bistro Builder/BBSIS/Fase 2D/Instalar")]
    public static void Install()
    {
        BistroBuilderBBSISPhase2CInstaller.Install();
        Scene scene = EditorSceneManager.OpenScene(
            ScenePath,
            OpenSceneMode.Single);
        if (!scene.IsValid() || !scene.isLoaded)
            throw new InvalidOperationException(
                "No pudo abrirse la escena BBSIS 2D.");

        EnsureFolders();
        BistroBuilderSpatialContractDefinition contract =
            EnsureContract();
        BistroBuilderMobilitySpatialProfileDefinition profile =
            EnsureProfile();

        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
            throw new InvalidOperationException(
                "BBSIS 2D necesita GameSystems.");
        BistroBuilderSpatialInteractionService spatial =
            systems.GetComponent<BistroBuilderSpatialInteractionService>();
        BistroBuilderSupplierDeliveryPresentationService presentations =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderSupplierDeliveryPresentationService>();
        if (spatial == null)
            throw new InvalidOperationException(
                "BBSIS 2D necesita la autoridad Spatial.");

        BistroBuilderMobilitySpatialCoordinator coordinator =
            GetOrAdd<BistroBuilderMobilitySpatialCoordinator>(systems);
        coordinator.ConfigureForEditor(
            spatial,
            presentations,
            contract,
            profile);

        MarkDirty(systems);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem(
        "Bistro Builder/BBSIS/Fase 2D/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderBBSISPhase2DValidator.Run();
        BistroBuilderBBSISPhase2DSelfTest.Run();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BBSIS FASE 2D - INSTALACION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
    private static BistroBuilderSpatialContractDefinition
        EnsureContract()
    {
        BistroBuilderSpatialContractDefinition contract =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderSpatialContractDefinition>(ContractPath);
        if (contract == null)
        {
            contract = ScriptableObject.CreateInstance<
                BistroBuilderSpatialContractDefinition>();
            AssetDatabase.CreateAsset(contract, ContractPath);
        }
        contract.ConfigureForEditor(
            "spatial.contract.logistics.cart",
            "logistics.cart",
            BistroBuilderAdaptiveSpatialProxyMode.Layered,
            new[]
            {
                "logistics.cart",
                "mobility.cart",
                "carry.envelope"
            });
        contract.ClearSemanticGeometryForEditor();
        contract.SetContractVersionForEditor(1);
        EditorUtility.SetDirty(contract);
        return contract;
    }

    private static BistroBuilderMobilitySpatialProfileDefinition
        EnsureProfile()
    {
        BistroBuilderMobilitySpatialProfileDefinition profile =
            AssetDatabase.LoadAssetAtPath<
                BistroBuilderMobilitySpatialProfileDefinition>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<
                BistroBuilderMobilitySpatialProfileDefinition>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        profile.ConfigureForEditor(
            "mobility.logistics.cart.standard",
            0.68f,
            0.92f,
            0.12f,
            0.18f,
            0.2f,
            0.28f,
            6);
        EditorUtility.SetDirty(profile);
        return profile;
    }
    private static void EnsureFolders()
    {
        const string root = "Assets/Resources/BistroBuilder/Spatial";
        if (!AssetDatabase.IsValidFolder(root + "/Profiles"))
            AssetDatabase.CreateFolder(root, "Profiles");
    }

    private static T GetOrAdd<T>(GameObject source)
        where T : Component
    {
        T component = source.GetComponent<T>();
        return component != null
            ? component
            : source.AddComponent<T>();
    }

    private static void MarkDirty(GameObject source)
    {
        Component[] components = source.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null)
                EditorUtility.SetDirty(components[i]);
        EditorUtility.SetDirty(source);
    }
}
