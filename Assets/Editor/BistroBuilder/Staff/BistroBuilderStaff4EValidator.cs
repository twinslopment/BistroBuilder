using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderStaff4EValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;
    public void Correct(string value) => correct.Add(value);
    public void Warning(string value) => warnings.Add(value);
    public void Error(string value) => errors.Add(value);

    public string BuildReport()
    {
        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER — 4E PERSONAL / VALIDACIÓN ===");
        Append(builder, "OK", correct);
        Append(builder, "AVISO", warnings);
        Append(builder, "ERROR", errors);
        builder.AppendLine();
        builder.AppendLine(
            "Correctos: " + CorrectCount +
            " · Advertencias: " + WarningCount +
            " · Errores: " + ErrorCount);
        return builder.ToString();
    }

    private static void Append(
        StringBuilder builder,
        string prefix,
        List<string> values)
    {
        for (int index = 0; index < values.Count; index++)
        {
            builder.AppendLine("[" + prefix + "] " + values[index]);
        }
    }
}

/// <summary>
/// Gate estructural 4E. Comprueba que Personal se integra como dos secciones
/// del Save universal y que su orden de fases es compatible con service.runtime.
/// </summary>
public static class BistroBuilderStaff4EValidator
{
    [MenuItem(
        "Tools/Bistro Builder/Personal/4E - Validar persistencia",
        false,
        3241)]
    private static void ValidateMenu()
    {
        BistroBuilderStaff4EValidationResult result = ValidateCurrentScene();
        Debug.Log(result.BuildReport());
        EditorUtility.DisplayDialog(
            "Bistro Builder — 4E Personal",
            "Correctos: " + result.CorrectCount +
            "\nAdvertencias: " + result.WarningCount +
            "\nErrores: " + result.ErrorCount,
            "Aceptar");
    }

    public static BistroBuilderStaff4EValidationResult ValidateCurrentScene()
    {
        var result = new BistroBuilderStaff4EValidationResult();
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            result.Error("No hay escena activa válida.");
            return result;
        }
        result.Correct("Escena activa válida.");

        GameObject gameSystems = FindUniqueGameSystems(scene, out int gsCount);
        if (gameSystems == null || gsCount != 1)
        {
            result.Error("Debe existir exactamente un GameSystems.");
            return result;
        }
        result.Correct("GameSystems canónico único.");

        BistroBuilderSaveGameService[] saveServices =
            FindSceneComponents<BistroBuilderSaveGameService>(scene);
        BistroBuilderStaffService[] staffServices =
            FindSceneComponents<BistroBuilderStaffService>(scene);
        BistroBuilderStaffSessionService[] sessionServices =
            FindSceneComponents<BistroBuilderStaffSessionService>(scene);
        BistroBuilderStaffStateSaveSectionProvider[] stateProviders =
            FindSceneComponents<BistroBuilderStaffStateSaveSectionProvider>(scene);
        BistroBuilderStaffSessionSaveSectionProvider[] sessionProviders =
            FindSceneComponents<BistroBuilderStaffSessionSaveSectionProvider>(scene);
        BistroBuilderActiveServiceSaveSectionProvider[] serviceProviders =
            FindSceneComponents<BistroBuilderActiveServiceSaveSectionProvider>(scene);

        if (saveServices.Length != 1 || staffServices.Length != 1 ||
            sessionServices.Length != 1 || stateProviders.Length != 1 ||
            sessionProviders.Length != 1 || serviceProviders.Length != 1)
        {
            result.Error(
                "4E necesita un único SaveGameService, StaffService, " +
                "StaffSessionService, dos providers de Personal y " +
                "un service.runtime existente.");
            return result;
        }

        BistroBuilderSaveGameService save = saveServices[0];
        BistroBuilderStaffStateSaveSectionProvider stateProvider =
            stateProviders[0];
        BistroBuilderStaffSessionSaveSectionProvider sessionProvider =
            sessionProviders[0];
        BistroBuilderActiveServiceSaveSectionProvider serviceProvider =
            serviceProviders[0];

        if (save.gameObject == gameSystems &&
            stateProvider.gameObject == gameSystems &&
            sessionProvider.gameObject == gameSystems)
        {
            result.Correct("Save y providers de Personal viven en GameSystems.");
        }
        else
        {
            result.Error("Los providers 4E deben vivir junto al SaveGameService.");
        }

        if (stateProvider.ValidateConfiguration(out string stateError))
        {
            result.Correct("Provider staff.state configurado correctamente.");
        }
        else
        {
            result.Error("staff.state inválido: " + stateError);
        }

        if (sessionProvider.ValidateConfiguration(out string sessionError))
        {
            result.Correct(
                "Provider staff.session.runtime configurado correctamente.");
        }
        else
        {
            result.Error("staff.session.runtime inválido: " + sessionError);
        }

        if (stateProvider.SectionId == BistroBuilderStaffSnapshot.CurrentSchemaId &&
            sessionProvider.SectionId ==
                BistroBuilderStaffSessionSnapshot.CurrentSchemaId &&
            !string.Equals(
                stateProvider.SectionId,
                sessionProvider.SectionId,
                StringComparison.Ordinal))
        {
            result.Correct("IDs de sección de Personal son estables y únicos.");
        }
        else
        {
            result.Error("Los IDs de sección 4E no coinciden con sus esquemas.");
        }

        if (stateProvider.SectionVersion ==
                BistroBuilderStaffSnapshot.CurrentSchemaVersion &&
            sessionProvider.SectionVersion ==
                BistroBuilderStaffSessionSnapshot.CurrentSchemaVersion)
        {
            result.Correct("Versiones de sección coinciden con los modelos V1.");
        }
        else
        {
            result.Error("Versión de provider y modelo de Personal divergen.");
        }

        if (stateProvider.SerializerId ==
                BistroBuilderJsonSaveSerializer.StableSerializerId &&
            sessionProvider.SerializerId ==
                BistroBuilderJsonSaveSerializer.StableSerializerId)
        {
            result.Correct("Personal reutiliza el serializador universal existente.");
        }
        else
        {
            result.Error("4E no debe introducir un serializador paralelo.");
        }

        if (!stateProvider.IsRequired && !sessionProvider.IsRequired)
        {
            result.Correct(
                "Secciones opcionales mantienen compatibilidad con saves pre-4E.");
        }
        else
        {
            result.Error("Las secciones V1 de Personal deben ser opcionales.");
        }

        if (stateProvider.PrepareOrder > serviceProvider.PrepareOrder &&
            sessionProvider.PrepareOrder > stateProvider.PrepareOrder)
        {
            result.Correct(
                "Prepare ordenado: service.runtime → staff.state → staff.session.");
        }
        else
        {
            result.Error("El orden Prepare 4E puede desmontar Personal demasiado pronto.");
        }

        if (stateProvider.ApplyOrder < sessionProvider.ApplyOrder &&
            sessionProvider.ApplyOrder < serviceProvider.ApplyOrder)
        {
            result.Correct(
                "Apply ordenado: staff.state → binding → service.runtime.");
        }
        else
        {
            result.Error("El orden Apply 4E no garantiza Employee antes de Waiter.");
        }

        if (sessionProvider.FinalizeOrder > serviceProvider.FinalizeOrder)
        {
            result.Correct(
                "Finalize de Personal ocurre después de reanudar service.runtime.");
        }
        else
        {
            result.Error("Personal no debe reanudarse antes de service.runtime.");
        }

        save.RefreshExtensions();
        if (save.HasProvider(stateProvider.SectionId) &&
            save.HasProvider(sessionProvider.SectionId))
        {
            result.Correct("SaveGameService descubre ambas secciones 4E.");
        }
        else
        {
            result.Error("SaveGameService no ha registrado ambas secciones de Personal.");
        }

        if (save.ValidateConfiguration(out string saveError))
        {
            result.Correct("SaveGameService sigue validando con 4E instalado.");
        }
        else
        {
            result.Error("SaveGameService inválido tras 4E: " + saveError);
        }

        int stateCount = CountSectionProviders(scene, stateProvider.SectionId);
        int sessionCount = CountSectionProviders(scene, sessionProvider.SectionId);
        if (stateCount == 1 && sessionCount == 1)
        {
            result.Correct("No existen providers duplicados para Personal.");
        }
        else
        {
            result.Error(
                "Secciones de Personal duplicadas: staff.state=" + stateCount +
                ", staff.session.runtime=" + sessionCount + ".");
        }

        if (Application.isPlaying)
        {
            BistroBuilderStaffSnapshot staffSnapshot =
                staffServices[0].CreateSnapshot();
            BistroBuilderStaffSessionSnapshot sessionSnapshot =
                sessionServices[0].CreateSessionSnapshot();
            bool staffOk = BistroBuilderStaffEngine.TryValidateSnapshot(
                staffSnapshot,
                staffServices[0].RoleCatalog,
                out string runtimeStaffError);
            bool sessionOk = staffOk &&
                BistroBuilderStaffSessionEngine.TryValidateSnapshot(
                    sessionSnapshot,
                    staffSnapshot,
                    out string runtimeSessionError);

            if (staffOk && sessionOk)
            {
                result.Correct("Snapshots runtime de Personal son persistibles.");
            }
            else
            {
                result.Error(
                    "Snapshot runtime no persistible: " + runtimeStaffError +
                    (sessionOk ? string.Empty : " " + runtimeSessionError));
            }
        }
        else
        {
            result.Correct("Round-trip runtime se comprobará en Play Mode/4G.");
        }

        return result;
    }

    private static int CountSectionProviders(Scene scene, string sectionId)
    {
        int count = 0;
        MonoBehaviour[] behaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < behaviours.Length; index++)
        {
            MonoBehaviour behaviour = behaviours[index];
            if (behaviour != null && behaviour.gameObject.scene == scene &&
                behaviour is IBistroBuilderSaveSectionProvider provider &&
                string.Equals(provider.SectionId, sectionId, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    private static GameObject FindUniqueGameSystems(Scene scene, out int count)
    {
        count = 0;
        GameObject found = null;
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            Transform[] transforms = roots[index].GetComponentsInChildren<Transform>(true);
            for (int child = 0; child < transforms.Length; child++)
            {
                Transform transform = transforms[child];
                if (transform != null &&
                    string.Equals(transform.name, "GameSystems", StringComparison.Ordinal))
                {
                    found = transform.gameObject;
                    count++;
                }
            }
        }
        return count == 1 ? found : null;
    }

    private static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var result = new List<T>();
        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                result.Add(all[index]);
            }
        }
        return result.ToArray();
    }
}
