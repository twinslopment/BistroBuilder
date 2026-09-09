using System;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderFinance3AValidator
{
    [MenuItem("Tools/Bistro Builder/Finanzas/3A - Validar núcleo financiero", false, 3001)]
    public static void ValidateFromMenu()
    {
        bool ok = ValidateCurrentScene(out int passed, out int failed, out string report);
        Debug.Log(report);
        EditorUtility.DisplayDialog(
            "Bistro Builder — 3A",
            "Validación financiera: " + passed + " correctos, " + failed + " errores.",
            "Aceptar");

        if (!ok)
        {
            Debug.LogError("3A — La validación del núcleo financiero ha fallado.");
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
            report = builder.ToString();
            return false;
        }

        GameObject gameSystems = FindGameSystems(scene);
        Check(gameSystems != null,
            "Existe GameSystems canónico.", ref passed, ref failed, builder);
        if (gameSystems == null)
        {
            report = builder.ToString();
            return false;
        }

        BistroBuilderFinanceService finance =
            gameSystems.GetComponent<BistroBuilderFinanceService>();
        BistroBuilderFinanceSaveSectionProvider provider =
            gameSystems.GetComponent<BistroBuilderFinanceSaveSectionProvider>();
        BistroBuilderSaveGameService save =
            gameSystems.GetComponent<BistroBuilderSaveGameService>();

        Check(finance != null,
            "GameSystems contiene BistroBuilderFinanceService.", ref passed, ref failed, builder);
        Check(provider != null,
            "GameSystems contiene finance.runtime provider.", ref passed, ref failed, builder);
        Check(save != null,
            "GameSystems conserva BistroBuilderSaveGameService.", ref passed, ref failed, builder);

        BistroBuilderFinanceService[] financeServices =
            UnityEngine.Object.FindObjectsByType<BistroBuilderFinanceService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        Check(financeServices.Length == 1,
            "Existe una sola autoridad financiera en la escena.", ref passed, ref failed, builder);

        if (finance != null)
        {
            bool financeValid = finance.ValidateConfiguration(out string financeError);
            Check(financeValid,
                "Configuración de Finanzas válida" + FormatError(financeError) + ".",
                ref passed, ref failed, builder);

            BistroBuilderFinanceSnapshot initialSnapshot =
                BistroBuilderFinanceEngine.CreateInitialSnapshot(
                    finance.OpeningBalanceCents,
                    finance.CurrencyCode);
            bool snapshotValid = BistroBuilderFinanceEngine.TryValidateSnapshot(
                initialSnapshot,
                out string snapshotError);
            Check(snapshotValid,
                "Snapshot finance.runtime v1 inicial válido" + FormatError(snapshotError) + ".",
                ref passed, ref failed, builder);
        }

        if (provider != null)
        {
            Check(provider.ValidateConfiguration(out string providerError),
                "Proveedor de persistencia válido" + FormatError(providerError) + ".",
                ref passed, ref failed, builder);
        }

        if (save != null)
        {
            save.RefreshExtensions();
            Check(save.HasProvider(BistroBuilderFinanceSaveSectionProvider.StableSectionId),
                "SaveGameService registra finance.runtime.",
                ref passed, ref failed, builder);
            Check(save.ValidateConfiguration(out string saveError),
                "La plataforma universal de guardado sigue válida" + FormatError(saveError) + ".",
                ref passed, ref failed, builder);
        }

        builder.Insert(0,
            "3A — VALIDACIÓN NÚCLEO FINANCIERO\n" +
            "Correctos: " + passed + "  Errores: " + failed + "\n\n");
        report = builder.ToString();
        return failed == 0;
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
