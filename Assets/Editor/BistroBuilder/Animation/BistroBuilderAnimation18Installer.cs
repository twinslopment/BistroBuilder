using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Instalador idempotente del vertical slice de Animación e Interacciones.
/// No instala clips ni cambia la autoridad de navegación/gameplay.
/// </summary>
public static class BistroBuilderAnimation18Installer
{
    private const string ScenePath = "Assets/Scenes/Prototype_Restaurant.unity";
    private const string DataFolder = "Assets/Data/Animation";
    private const string FamilyFolder = DataFolder + "/Families";
    private const string CatalogPath = DataFolder + "/BistroBuilderMotionCatalog.asset";

    [MenuItem("Bistro Builder/18 Animacion e interacciones/Instalar fundacion")]
    public static void Install()
    {
        EnsureFolders();
        BistroBuilderMotionCatalog catalog = EnsureMotionCatalog();
        EnsureFamilyProfiles();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject systems = GameObject.Find("GameSystems");
        if (systems == null)
            throw new InvalidOperationException("Falta GameSystems en Prototype_Restaurant.");

        BistroBuilderInteractionPresentationService service =
            EnsureComponent<BistroBuilderInteractionPresentationService>(systems);
        service.ConfigureForEditor(catalog);
        EditorUtility.SetDirty(service);

        InstallSeatDescriptors();
        InstallCharacterPresentersWherePossible();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Bistro Builder/18 Animacion e interacciones/Instalar y validar")]
    public static void InstallAndValidate()
    {
        Install();
        BistroBuilderAnimation18Validator.Run();
        BistroBuilderAnimation18SelfTest.Run();
    }

    public static void InstallFromCommandLine()
    {
        try
        {
            InstallAndValidate();
            Debug.Log("BLOQUE 18 - FUNDACION ANIMACION PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Data", "Animation");
        EnsureFolder(DataFolder, "Families");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, child);
    }

    private static BistroBuilderMotionCatalog EnsureMotionCatalog()
    {
        BistroBuilderMotionCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderMotionCatalog>(CatalogPath);
        if (catalog != null) return catalog;

        // Si una versión previa quedó serializada sin MonoScript válido,
        // se reemplaza sólo este asset canónico de BB18.
        if (File.Exists(Path.GetFullPath(CatalogPath)))
            AssetDatabase.DeleteAsset(CatalogPath);

        catalog = ScriptableObject.CreateInstance<BistroBuilderMotionCatalog>();
        catalog.ConfigureForEditor(new List<BistroBuilderMotionProfile>());
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    private static void EnsureFamilyProfiles()
    {
        EnsureFamily(
            "seat.standard",
            BistroBuilderInteractionFamily.Seat,
            new List<BistroBuilderInteractionOperation>
            {
                BistroBuilderInteractionOperation.Sit,
                BistroBuilderInteractionOperation.Stand
            },
            "seat.standard",
            "seat.generic",
            true);

        EnsureFamily(
            "portal.standard",
            BistroBuilderInteractionFamily.Portal,
            new List<BistroBuilderInteractionOperation>
            {
                BistroBuilderInteractionOperation.Open,
                BistroBuilderInteractionOperation.Close
            },
            string.Empty,
            string.Empty,
            false);

        EnsureFamily(
            "transfer.standard",
            BistroBuilderInteractionFamily.Transfer,
            new List<BistroBuilderInteractionOperation>
            {
                BistroBuilderInteractionOperation.Pickup,
                BistroBuilderInteractionOperation.Place
            },
            "transfer.table.1h",
            "transfer.generic",
            true);
    }

    private static void EnsureFamily(
        string id,
        BistroBuilderInteractionFamily family,
        List<BistroBuilderInteractionOperation> operations,
        string primaryMotion,
        string fallbackMotion,
        bool commit)
    {
        string safeName = id.Replace('.', '_');
        string path = FamilyFolder + "/" + safeName + ".asset";
        BistroBuilderInteractionFamilyProfile profile =
            AssetDatabase.LoadAssetAtPath<BistroBuilderInteractionFamilyProfile>(path);
        if (profile == null)
        {
            if (File.Exists(Path.GetFullPath(path)))
                AssetDatabase.DeleteAsset(path);
            profile = ScriptableObject.CreateInstance<BistroBuilderInteractionFamilyProfile>();
            AssetDatabase.CreateAsset(profile, path);
        }

        profile.ConfigureForEditor(id, family, operations, primaryMotion, fallbackMotion, commit);
        EditorUtility.SetDirty(profile);
    }

    private static void InstallSeatDescriptors()
    {
        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (RestaurantSeat seat in seats)
        {
            if (seat == null || seat.CustomerApproachPoint == null) continue;
            BistroBuilderAssetInteractionDescriptor descriptor =
                EnsureComponent<BistroBuilderAssetInteractionDescriptor>(seat.gameObject);

            BistroBuilderAnimationInteractionSlotDefinition slot = new BistroBuilderAnimationInteractionSlotDefinition();
            slot.ConfigureForEditor(
                "seat.default",
                BistroBuilderInteractionFamily.Seat,
                seat.CustomerApproachPoint,
                seat.SeatPoint,
                seat.CustomerApproachPoint,
                null,
                seat);

            descriptor.ConfigureForEditor(
                new List<BistroBuilderAnimationInteractionSlotDefinition> { slot });
            EditorUtility.SetDirty(descriptor);
        }
    }

    private static void InstallCharacterPresentersWherePossible()
    {
        Animator[] animators = UnityEngine.Object.FindObjectsByType<Animator>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Animator animator in animators)
        {
            if (animator == null || animator.gameObject.scene != SceneManager.GetActiveScene())
                continue;

            GameObject root = animator.transform.root.gameObject;
            // La navegación conserva siempre la autoridad posicional.
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);

            BistroBuilderCharacterAnimationDriver driver =
                EnsureComponent<BistroBuilderCharacterAnimationDriver>(root);
            driver.ConfigureForEditor(animator, EnsureMotionCatalog());
            EnsureComponent<BistroBuilderLocomotionAnimationPresenter>(root);
            EditorUtility.SetDirty(root);
        }
    }

    private static T EnsureComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
            component = Undo.AddComponent<T>(target);
        return component;
    }
}
