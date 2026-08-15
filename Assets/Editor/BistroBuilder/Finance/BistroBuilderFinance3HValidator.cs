using System;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3HValidator
{
    public static bool ValidateCurrentScene(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        var builder = new StringBuilder();
        builder.AppendLine("=== BISTRO BUILDER 3H — VALIDACIÓN ===");

        Scene scene = SceneManager.GetActiveScene();
        Check(
            scene.IsValid() && scene.isLoaded &&
            !string.IsNullOrWhiteSpace(scene.path),
            "Escena activa cargada y guardada",
            ref passed,
            ref failed,
            builder);

        GameObject gameSystems = FindGameSystems(scene);
        Check(
            gameSystems != null,
            "GameSystems presente",
            ref passed,
            ref failed,
            builder);

        BistroBuilderFinancialHistoryService[] histories =
            FindSceneComponents<BistroBuilderFinancialHistoryService>(scene);
        Check(
            histories.Length == 1,
            "Existe una única autoridad de lectura 3H",
            ref passed,
            ref failed,
            builder);

        BistroBuilderFinancialHistoryService history =
            histories.Length == 1 ? histories[0] : null;
        Check(
            history != null && gameSystems != null &&
            history.gameObject == gameSystems,
            "3H está instalado en GameSystems",
            ref passed,
            ref failed,
            builder);

        Check(
            history != null && history.enabled,
            "3H está habilitado",
            ref passed,
            ref failed,
            builder);

        BistroBuilderFinancialResultsService[] results =
            FindSceneComponents<BistroBuilderFinancialResultsService>(scene);
        Check(
            results.Length == 1,
            "3G conserva una única fachada de resultados",
            ref passed,
            ref failed,
            builder);

        BistroBuilderGeneralGameStateService[] generals =
            FindSceneComponents<BistroBuilderGeneralGameStateService>(scene);
        Check(
            generals.Length == 1,
            "Calendario general conserva una única autoridad",
            ref passed,
            ref failed,
            builder);

        Check(
            history != null && results.Length == 1 &&
            ReferenceEquals(history.FinancialResultsService, results[0]),
            "3H referencia la fachada canónica 3G",
            ref passed,
            ref failed,
            builder);

        Check(
            history != null && generals.Length == 1 &&
            ReferenceEquals(history.GeneralGameStateService, generals[0]),
            "3H comparte el calendario canónico",
            ref passed,
            ref failed,
            builder);

        string historyError = string.Empty;
        Check(
            history != null && history.ValidateConfiguration(out historyError),
            "Configuración 3H válida" + Suffix(historyError),
            ref passed,
            ref failed,
            builder);

        string resultsError = string.Empty;
        Check(
            results.Length == 1 &&
            results[0].ValidateConfiguration(out resultsError),
            "Configuración 3G sigue válida" + Suffix(resultsError),
            ref passed,
            ref failed,
            builder);

        Check(
            !HasTypeNamed("BistroBuilderFinancialHistorySaveSectionProvider") &&
            !HasTypeNamed("BistroBuilderFinancialHistorySnapshot"),
            "3H no introduce persistencia ni snapshot sombra",
            ref passed,
            ref failed,
            builder);

        builder.AppendLine();
        builder.AppendLine(
            "Resultado: " + passed + " OK / " + failed + " errores");
        report = builder.ToString();
        return failed == 0;
    }

    private static string Suffix(string error)
    {
        return string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : " — " + error;
    }

    private static bool HasTypeNamed(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int assemblyIndex = 0;
             assemblyIndex < assemblies.Length;
             assemblyIndex++)
        {
            Type[] types;
            try
            {
                types = assemblies[assemblyIndex].GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                types = exception.Types;
            }

            if (types == null)
            {
                continue;
            }

            for (int typeIndex = 0; typeIndex < types.Length; typeIndex++)
            {
                Type type = types[typeIndex];
                if (type != null &&
                    string.Equals(type.Name, typeName, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        if (!scene.IsValid())
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index] != null &&
                string.Equals(
                    roots[index].name,
                    "GameSystems",
                    StringComparison.Ordinal))
            {
                return roots[index];
            }
        }
        return null;
    }

    private static T[] FindSceneComponents<T>(Scene scene)
        where T : Component
    {
        T[] all = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        var list = new System.Collections.Generic.List<T>();

        for (int index = 0; index < all.Length; index++)
        {
            if (all[index] != null && all[index].gameObject.scene == scene)
            {
                list.Add(all[index]);
            }
        }

        return list.ToArray();
    }

    private static void Check(
        bool condition,
        string label,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + label);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + label);
        }
    }
}
