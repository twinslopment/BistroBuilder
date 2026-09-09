using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderReputationBlock8ValidationResult
{
    private readonly List<string> lines = new List<string>();
    public int Passed { get; private set; }
    public int Errors { get; private set; }
    public void Check(bool condition, string ok, string fail)
    {
        if (condition) { Passed++; lines.Add("[OK] " + ok); }
        else { Errors++; lines.Add("[ERROR] " + fail); }
    }
    public string BuildReport() =>
        "=== BISTRO BUILDER — REPUTACIÓN BLOQUE 8 VALIDACIÓN ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

/// <summary>
/// 8G — Gate estructural acumulativo de todo el Bloque 8.
/// </summary>
public static class BistroBuilderReputationBlock8Validator
{
    [MenuItem("Tools/Bistro Builder/Reputation/8G - Validar bloque completo", false, 8192)]
    private static void ValidateFromMenu()
    {
        var result = ValidateCurrentScene();
        if (result.Errors == 0) Debug.Log(result.BuildReport());
        else Debug.LogError(result.BuildReport());
    }

    public static void ValidateFromCommandLine()
    {
        EditorSceneManager.OpenScene(BistroBuilderMarketing7APaths.MainScene, OpenSceneMode.Single);
        var result = ValidateCurrentScene();
        if (result.Errors > 0) throw new InvalidOperationException(result.BuildReport());
        Debug.Log(result.BuildReport());
    }

    public static BistroBuilderReputationBlock8ValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderReputationBlock8ValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "Escena principal activa y guardada.",
            "No existe una escena principal guardada.");

        GameObject[] hosts = FindNamedObjects(scene, "GameSystems");
        result.Check(hosts.Length == 1,
            "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");

        BistroBuilderReputationService[] reputation =
            FindSceneComponents<BistroBuilderReputationService>(scene);
        result.Check(IsUniqueOnHost(reputation, hosts),
            "ReputationService es autoridad única en GameSystems.",
            "ReputationService falta, está duplicado o mal ubicado.");
        if (reputation.Length == 1)
            result.Check(reputation[0].ValidateConfiguration(out _),
                "La reputación histórica valida su snapshot.",
                "El snapshot canónico de Reputación es inválido.");

        BistroBuilderCustomerExperienceTrackingService[] tracking =
            FindSceneComponents<BistroBuilderCustomerExperienceTrackingService>(scene);
        result.Check(IsUniqueOnHost(tracking, hosts),
            "Experience Tracking es único y vive en GameSystems.",
            "Experience Tracking falta, está duplicado o mal ubicado.");
        if (tracking.Length == 1)
            result.Check(tracking[0].ValidateConfiguration(out _),
                "Satisfacción, esperas, calidad y precio tienen dependencias reales.",
                "Experience Tracking no valida sus autoridades.");

        BistroBuilderReputationSaveSectionProvider[] stateProviders =
            FindSceneComponents<BistroBuilderReputationSaveSectionProvider>(scene);
        BistroBuilderReputationRuntimeSaveSectionProvider[] runtimeProviders =
            FindSceneComponents<BistroBuilderReputationRuntimeSaveSectionProvider>(scene);
        result.Check(IsUniqueOnHost(stateProviders, hosts),
            "reputation.state tiene proveedor único.",
            "reputation.state falta o está duplicado.");
        result.Check(IsUniqueOnHost(runtimeProviders, hosts),
            "reputation.runtime tiene proveedor único.",
            "reputation.runtime falta o está duplicado.");
        if (stateProviders.Length == 1)
            result.Check(stateProviders[0].ValidateConfiguration(out _),
                "reputation.state valida configuración.",
                "reputation.state tiene dependencias inválidas.");
        if (runtimeProviders.Length == 1)
            result.Check(runtimeProviders[0].ValidateConfiguration(out _) &&
                         runtimeProviders[0].ApplyOrder > 500,
                "reputation.runtime valida y se aplica después de service.runtime.",
                "reputation.runtime no respeta el orden de restauración.");

        BistroBuilderSaveGameService[] saves =
            FindSceneComponents<BistroBuilderSaveGameService>(scene);
        if (saves.Length == 1)
            saves[0].RefreshExtensions();
        result.Check(saves.Length == 1 &&
                     saves[0].HasProvider(BistroBuilderReputationSaveSectionProvider.StableSectionId) &&
                     saves[0].HasProvider(BistroBuilderReputationRuntimeSaveSectionProvider.StableSectionId),
            "SaveGame descubre estado histórico y visitas en curso.",
            "SaveGame no descubre ambas secciones de Reputación.");

        BistroBuilderGuestRelationsService[] relations =
            FindSceneComponents<BistroBuilderGuestRelationsService>(scene);
        result.Check(relations.Length == 1 && relations[0].ValidateConfiguration(out _),
            "GuestRelations conserva cohortes de clientes habituales.",
            "GuestRelations no valida junto a Reputación.");

        BistroBuilderMarketingDemandIntegrationService[] demand =
            FindSceneComponents<BistroBuilderMarketingDemandIntegrationService>(scene);
        result.Check(demand.Length == 1 && demand[0].ValidateConfiguration(out _),
            "Demanda integra Marketing, boca a boca y retornos sin duplicar autoridad.",
            "La integración de demanda no valida con Reputación.");

        BistroBuilderReputationPlayerFacade[] facades =
            FindSceneComponents<BistroBuilderReputationPlayerFacade>(scene);
        result.Check(IsUniqueOnHost(facades, hosts),
            "La fachada jugable de Reputación es única en GameSystems.",
            "La fachada de Reputación falta o está mal ubicada.");
        if (facades.Length == 1)
            result.Check(facades[0].ValidateConfiguration(out _),
                "La fachada de UI compone todos los datos canónicos.",
                "La fachada de UI tiene dependencias inválidas.");

        BistroBuilderReputationPlayerScreen[] screens =
            FindSceneComponents<BistroBuilderReputationPlayerScreen>(scene);
        result.Check(screens.Length == 1 && screens[0].ValidateConfiguration(out _),
            "Existe una única pantalla jugable de Reputación completamente cableada.",
            "La pantalla jugable de Reputación falta o está incompleta.");

        GameObject[] uiRoots = FindNamedObjects(scene, "BistroBuilderReputationUI");
        GameObject[] launchers = FindNamedObjects(scene, "OpenReputationButton");
        result.Check(uiRoots.Length == 1 && launchers.Length == 1,
            "La UI de Reputación y su botón de apertura son únicos.",
            "La UI de Reputación o su launcher falta/está duplicado.");

        result.Check(BistroBuilderReputationSnapshot.CurrentSchemaId == "reputation.state" &&
                     BistroBuilderReputationRuntimeSnapshot.CurrentSchemaId == "reputation.runtime",
            "Los contratos persistentes de 8E son estables.",
            "Los IDs persistentes de Reputación han cambiado.");

        result.Check(HasNoMarketingField(typeof(BistroBuilderReputationService)) &&
                     HasNoMarketingField(typeof(BistroBuilderCustomerExperienceTrackingService)) &&
                     HasNoMarketingField(typeof(BistroBuilderReputationPlayerFacade)),
            "La autoridad de Reputación y su medición permanecen independientes de Marketing.",
            "Reputación contiene una dependencia directa de tipos Marketing.");

        result.Check(HasField(typeof(BistroBuilderCustomerAcquisitionProfile), "discoverySourceId") &&
                     HasField(typeof(BistroBuilderCustomerAcquisitionTag), "discoverySourceId") &&
                     HasField(typeof(BistroBuilderCustomerAcquisitionTag), "returningVisit") &&
                     HasField(typeof(BistroBuilderCustomerAcquisitionTag), "guestRelationsReferenceId"),
            "Captación conserva descubrimiento e identidad de cliente habitual.",
            "El contrato runtime de captación pierde descubrimiento o habitualidad.");

        BistroBuilderReputation8AValidationResult foundation =
            BistroBuilderReputation8AValidator.ValidateCurrentScene();
        result.Check(foundation.Errors == 0,
            "La fundación 8A permanece verde.",
            "El cierre 8G ha roto la fundación 8A.");

        BistroBuilderMarketingPlayerUiValidationResult marketing =
            BistroBuilderMarketingPlayerUiValidator.ValidateCurrentScene();
        result.Check(marketing.Errors == 0,
            "El Bloque 7 Marketing permanece estructuralmente verde.",
            "Reputación ha introducido una regresión estructural en Marketing.");

        return result;
    }

    private static bool HasNoMarketingField(Type type)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                             BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < fields.Length; i++)
            if (fields[i].FieldType.Name.IndexOf("Marketing", StringComparison.Ordinal) >= 0)
                return false;
        return true;
    }

    private static bool HasField(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.Public |
                            BindingFlags.NonPublic) != null;

    private static bool IsUniqueOnHost<T>(T[] values, GameObject[] hosts)
        where T : Component =>
        values.Length == 1 && hosts.Length == 1 && values[0].gameObject == hosts[0];

    private static GameObject[] FindNamedObjects(Scene scene, string name)
    {
        var list = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null && string.Equals(transform.name, name, StringComparison.Ordinal))
                list.Add(transform.gameObject);
        return list.ToArray();
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T[] found = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < found.Length; i++) if (found[i] != null) list.Add(found[i]);
        }
        return list.ToArray();
    }
}
