using System;
using UnityEditor;
using UnityEngine;

public static class BistroBuilderEditPlaytestTariffInstaller
{
    public const string AssetPath =
        "Assets/Resources/BistroBuilder/Finance/BB_EditMode_PlaytestTariffs.asset";

    [MenuItem("Bistro Builder/Finance/Install Block 18 Playtest Tariffs")]
    public static void InstallFromMenu()
    {
        Install();
        Debug.Log("Block 18 playtest tariffs installed: " + AssetPath);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            Install();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    public static BistroBuilderEditFinanceTariffTable Install()
    {
        EnsureFolder("Assets/Resources/BistroBuilder/Finance");
        var table = AssetDatabase.LoadAssetAtPath<
            BistroBuilderEditFinanceTariffTable>(AssetPath);
        if (table == null)
        {
            table = ScriptableObject.CreateInstance<
                BistroBuilderEditFinanceTariffTable>();
            AssetDatabase.CreateAsset(table, AssetPath);
        }

        var serialized = new SerializedObject(table);
        serialized.FindProperty("sourceReference").stringValue =
            "Bistro Builder owner-authorized provisional playtest tariff set";
        serialized.FindProperty("tariffRevision").stringValue =
            "playtest-2026-09-08-r1";
        SerializedProperty rates = serialized.FindProperty("rates");
        rates.arraySize = 0;

        AddRate(rates, "wall.default",
            BistroBuilderEditFinanceRateUnit.Length, 1500L, 700L, -300L);
        AddRate(rates, "door",
            BistroBuilderEditFinanceRateUnit.Quantity, 12000L, 5000L, -2500L);
        AddRate(rates, "window",
            BistroBuilderEditFinanceRateUnit.Quantity, 9000L, 4000L, -1800L);
        AddRate(rates, "finish.floor.default",
            BistroBuilderEditFinanceRateUnit.Area, 2200L, 1100L, 0L);
        AddRate(rates, "finish.wall.default",
            BistroBuilderEditFinanceRateUnit.Area, 1800L, 900L, 0L);
        AddRate(rates, "zone.dining",
            BistroBuilderEditFinanceRateUnit.Area, 150L, 75L, 0L);
        AddRate(rates, "zone.kitchen",
            BistroBuilderEditFinanceRateUnit.Area, 180L, 90L, 0L);
        AddRate(rates, "zone.bar",
            BistroBuilderEditFinanceRateUnit.Area, 180L, 90L, 0L);
        AddRate(rates, "zone.terrace",
            BistroBuilderEditFinanceRateUnit.Area, 120L, 60L, 0L);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssetIfDirty(table);
        AssetDatabase.ImportAsset(AssetPath,
            ImportAssetOptions.ForceSynchronousImport);
        table = AssetDatabase.LoadAssetAtPath<
            BistroBuilderEditFinanceTariffTable>(AssetPath);
        string validationError = string.Empty;
        if (table == null || !table.ValidateConfiguration(out validationError))
            throw new InvalidOperationException(
                "Playtest tariff table is invalid: " + validationError);
        return table;
    }

    private static void AddRate(
        SerializedProperty rates,
        string definitionId,
        BistroBuilderEditFinanceRateUnit unit,
        long added,
        long modified,
        long removed)
    {
        int index = rates.arraySize;
        rates.InsertArrayElementAtIndex(index);
        SerializedProperty rate = rates.GetArrayElementAtIndex(index);
        rate.FindPropertyRelative("definitionId").stringValue = definitionId;
        rate.FindPropertyRelative("unit").intValue = (int)unit;
        rate.FindPropertyRelative("pricedChanges").intValue =
            (int)BistroBuilderEditFinancePricedChanges.All;
        rate.FindPropertyRelative("addedSignedCentsPerUnit").longValue = added;
        rate.FindPropertyRelative("modifiedSignedCentsPerUnit").longValue = modified;
        rate.FindPropertyRelative("removedSignedCentsPerUnit").longValue = removed;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string leaf = System.IO.Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(leaf))
            throw new InvalidOperationException("Invalid asset folder: " + path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
