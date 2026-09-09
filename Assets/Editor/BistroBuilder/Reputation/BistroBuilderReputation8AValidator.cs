using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderReputation8AValidationResult
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
        "=== BISTRO BUILDER — REPUTACIÓN 8A VALIDACIÓN ===\n" +
        string.Join("\n", lines) + "\nResultado: " + Passed +
        " OK / " + Errors + " errores.";
}

public static class BistroBuilderReputation8AValidator
{
    [MenuItem("Tools/Bistro Builder/Reputation/8A - Validar", false, 8102)]
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

    public static BistroBuilderReputation8AValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderReputation8AValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        result.Check(scene.IsValid() && scene.isLoaded && !string.IsNullOrWhiteSpace(scene.path),
            "Escena principal activa y guardada.", "No existe una escena activa guardada.");

        GameObject[] hosts = FindNamedObjects(scene, "GameSystems");
        result.Check(hosts.Length == 1, "Existe un único GameSystems canónico.",
            "GameSystems falta o está duplicado.");

        BistroBuilderReputationService[] reputation = FindSceneComponents<BistroBuilderReputationService>(scene);
        result.Check(reputation.Length == 1 && hosts.Length == 1 && reputation[0].gameObject == hosts[0],
            "ReputationService es único y vive en GameSystems.",
            "ReputationService falta, está duplicado o mal ubicado.");
        if (reputation.Length == 1)
            result.Check(reputation[0].ValidateConfiguration(out _),
                "ReputationService valida su estado.", "ReputationService tiene estado inválido.");

        BistroBuilderReputationSaveSectionProvider[] providers =
            FindSceneComponents<BistroBuilderReputationSaveSectionProvider>(scene);
        result.Check(providers.Length == 1 && hosts.Length == 1 && providers[0].gameObject == hosts[0],
            "reputation.state tiene un único proveedor en GameSystems.",
            "El proveedor reputation.state falta, está duplicado o mal ubicado.");
        if (providers.Length == 1)
            result.Check(providers[0].ValidateConfiguration(out _),
                "El proveedor reputation.state valida configuración.",
                "El proveedor reputation.state tiene dependencias inválidas.");

        BistroBuilderSaveGameService[] saves = FindSceneComponents<BistroBuilderSaveGameService>(scene);
        if (saves.Length == 1)
            saves[0].RefreshExtensions();
        result.Check(saves.Length == 1 && saves[0].HasProvider(BistroBuilderReputationSaveSectionProvider.StableSectionId),
            "SaveGame descubre reputation.state.", "SaveGame no descubre reputation.state.");

        BistroBuilderGuestRelationsService[] relations = FindSceneComponents<BistroBuilderGuestRelationsService>(scene);
        result.Check(relations.Length == 1 && relations[0].ValidateConfiguration(out _),
            "GuestRelations queda centrado en cohortes y enlazado a Reputación.",
            "GuestRelations no valida tras separar la autoridad de reputación.");

        BistroBuilderMarketingGuestRelationsBridge[] bridge =
            FindSceneComponents<BistroBuilderMarketingGuestRelationsBridge>(scene);
        result.Check(bridge.Length == 1 && bridge[0].ValidateConfiguration(out _),
            "Marketing acredita la autoridad canónica de Reputación.",
            "El puente Marketing → Reputación no valida.");

        BistroBuilderMarketingDemandIntegrationService[] demand =
            FindSceneComponents<BistroBuilderMarketingDemandIntegrationService>(scene);
        result.Check(demand.Length == 1 && demand[0].ValidateConfiguration(out _),
            "La demanda consume Reputación sin perder cohortes recurrentes.",
            "La integración de demanda no valida con Reputación.");

        BistroBuilderMarketingPlayerFacade[] facade = FindSceneComponents<BistroBuilderMarketingPlayerFacade>(scene);
        result.Check(facade.Length == 1 && facade[0].ValidateConfiguration(out _),
            "La UI de Marketing lee Reputación canónica y cohortes por separado.",
            "La UI de Marketing no valida tras la separación.");

        result.Check(BistroBuilderReputationSnapshot.CurrentSchemaId == "reputation.state" &&
                     BistroBuilderReputationSnapshot.CurrentSchemaVersion == 1,
            "Contrato persistente reputation.state v1 estable.",
            "El contrato persistente de Reputación no es el esperado.");

        result.Check(HasNoMarketingField(typeof(BistroBuilderReputationService)),
            "ReputationService no depende de tipos Marketing.",
            "ReputationService contiene una dependencia directa de Marketing.");

        BistroBuilderMarketingPlayerUiValidationResult marketingUi =
            BistroBuilderMarketingPlayerUiValidator.ValidateCurrentScene();
        result.Check(marketingUi.Errors == 0,
            "La cadena estructural del Bloque 7 permanece verde.",
            "La fundación 8A ha roto una regresión estructural de Marketing.");
        return result;
    }

    private static bool HasNoMarketingField(System.Type type)
    {
        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                             BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < fields.Length; i++)
            if (fields[i].FieldType.Name.IndexOf("Marketing", System.StringComparison.Ordinal) >= 0)
                return false;
        return true;
    }

    private static GameObject[] FindNamedObjects(Scene scene, string name)
    {
        var list = new List<GameObject>();
        foreach (GameObject root in scene.GetRootGameObjects())
        foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            if (transform != null && transform.name == name) list.Add(transform.gameObject);
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
