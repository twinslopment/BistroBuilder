using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3CValidator
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3C - Validar compras a proveedores", false, 3021)]
    public static void ValidateFromMenu()
    {
        bool ok = ValidateCurrentScene(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3C",
            "Validación de compras: " + passed + " correctos, " + failed + " errores.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError("3C — La validación de compras a proveedores ha fallado.");
        }
    }

    public static bool ValidateCurrentScene(
        out int passed,
        out int failed,
        out string report)
    {
        passed = 0;
        failed = 0;
        StringBuilder builder = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();

        Check(scene.IsValid() && scene.isLoaded,
            "Escena activa válida.", ref passed, ref failed, builder);
        if (!scene.IsValid() || !scene.isLoaded)
        {
            report = BuildReport(passed, failed, builder);
            return false;
        }

        bool finance3BValid = BistroBuilderFinance3BValidator.ValidateCurrentScene(
            out _, out int finance3BErrors, out _);
        Check(finance3BValid && finance3BErrors == 0,
            "3A/3B permanecen íntegros y válidos.",
            ref passed, ref failed, builder);

        GameObject gameSystems = FindGameSystems(scene);
        Check(gameSystems != null,
            "Existe GameSystems canónico.", ref passed, ref failed, builder);
        if (gameSystems == null)
        {
            report = BuildReport(passed, failed, builder);
            return false;
        }

        BistroBuilderSupplierPurchaseFinanceBridge[] bridges =
            UnityEngine.Object.FindObjectsByType<BistroBuilderSupplierPurchaseFinanceBridge>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Check(bridges.Length == 1,
            "Existe un único bridge financiero de proveedores 3C.",
            ref passed, ref failed, builder);

        BistroBuilderSupplierPurchaseFinanceBridge bridge =
            bridges.Length == 1 ? bridges[0] : null;
        Check(bridge != null && bridge.gameObject == gameSystems,
            "El bridge 3C pertenece a GameSystems.",
            ref passed, ref failed, builder);

        BistroBuilderFinanceService finance =
            gameSystems.GetComponent<BistroBuilderFinanceService>();
        BistroBuilderGeneralGameStateService generalState =
            GetReference(bridge, "generalGameStateService") as
                BistroBuilderGeneralGameStateService;
        GameClock gameClock = GetReference(bridge, "gameClock") as GameClock;
        BistroBuilderSaveGameService save =
            GetReference(bridge, "saveGameService") as BistroBuilderSaveGameService;

        Check(bridge != null &&
              finance != null &&
              ReferenceEquals(GetReference(bridge, "financeService"), finance) &&
              generalState != null && gameClock != null && save != null,
            "3C referencia solo las autoridades canónicas de caja, tiempo y guardado.",
            ref passed, ref failed, builder);

        Check(save != null &&
              save.GetComponent<BistroBuilderFinanceSaveSectionProvider>() != null &&
              save.GetComponent<BistroBuilderSupplierIntegratedSaveSectionProvider>() != null,
            "Persisten finance.runtime y supplier.integrated.runtime sin estado paralelo 3C.",
            ref passed, ref failed, builder);

        bool providersReady = false;
        if (save != null)
        {
            save.RefreshExtensions();
            providersReady =
                save.HasProvider(BistroBuilderFinanceSaveSectionProvider.StableSectionId) &&
                save.HasProvider(BistroBuilderSupplierIntegratedSaveSectionProvider.StableSectionId);
        }
        Check(providersReady,
            "SaveGameService registra las dos autoridades que 3C reconcilia.",
            ref passed, ref failed, builder);

        string bridgeError = string.Empty;
        Check(bridge != null && bridge.ValidateConfiguration(out bridgeError),
            "Configuración de 3C válida" + FormatError(bridgeError) + ".",
            ref passed, ref failed, builder);

        report = BuildReport(passed, failed, builder);
        return failed == 0;
    }

    private static UnityEngine.Object GetReference(
        UnityEngine.Object target,
        string fieldName)
    {
        if (target == null)
        {
            return null;
        }

        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static GameObject FindGameSystems(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            GameObject root = roots[index];
            if (root != null &&
                string.Equals(root.name, "GameSystems", StringComparison.Ordinal))
            {
                return root;
            }
        }
        return null;
    }

    private static string BuildReport(int passed, int failed, StringBuilder builder)
    {
        builder.Insert(0,
            "3C — VALIDACIÓN COMPRAS A PROVEEDORES\n" +
            "Correctos: " + passed + "  Errores: " + failed + "\n\n");
        return builder.ToString();
    }

    private static void Check(
        bool condition,
        string message,
        ref int passed,
        ref int failed,
        StringBuilder builder)
    {
        if (condition)
        {
            passed++;
            builder.AppendLine("[OK] " + message);
        }
        else
        {
            failed++;
            builder.AppendLine("[ERROR] " + message);
        }
    }

    private static string FormatError(string error)
    {
        return string.IsNullOrWhiteSpace(error) ? string.Empty : ": " + error;
    }
}
