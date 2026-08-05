using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BistroBuilderMenuPreparation21FValidationResult
{
    private readonly List<string> correct = new List<string>();
    private readonly List<string> warnings = new List<string>();
    private readonly List<string> errors = new List<string>();

    public int CorrectCount => correct.Count;
    public int WarningCount => warnings.Count;
    public int ErrorCount => errors.Count;
    public void AddCorrect(string value) => correct.Add(value);
    public void AddWarning(string value) => warnings.Add(value);
    public void AddError(string value) => errors.Add(value);

    public string BuildReport()
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine(
            "BISTRO BUILDER - 2.1F PREPARACIÓN CONFIGURABLE"
        );
        builder.AppendLine("Correctos: " + CorrectCount);
        builder.AppendLine("Advertencias: " + WarningCount);
        builder.AppendLine("Errores: " + ErrorCount);
        Append(builder, "OK", correct);
        Append(builder, "ADVERTENCIA", warnings);
        Append(builder, "ERROR", errors);
        return builder.ToString().TrimEnd();
    }

    private static void Append(
        StringBuilder builder,
        string prefix,
        List<string> values
    )
    {
        for (int index = 0; index < values.Count; index++)
        {
            builder.Append("- ");
            builder.Append(prefix);
            builder.Append(": ");
            builder.AppendLine(values[index]);
        }
    }
}

/// <summary>
/// Validador no destructivo de contratos, escena y persistencia 2.1F.
/// </summary>
public static class BistroBuilderMenuPreparation21FValidator
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Validate 2.1F Preparation Settings";

    [MenuItem(MenuPath, false, 181)]
    private static void ValidateFromMenu()
    {
        BistroBuilderMenuPreparation21FValidationResult result =
            ValidateCurrentProject();
        string report = result.BuildReport();

        if (result.ErrorCount > 0)
        {
            Debug.LogError(report);
        }
        else if (result.WarningCount > 0)
        {
            Debug.LogWarning(report);
        }
        else
        {
            Debug.Log(report);
        }

        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    public static BistroBuilderMenuPreparation21FValidationResult
        ValidateCurrentProject()
    {
        BistroBuilderMenuPreparation21FValidationResult result =
            new BistroBuilderMenuPreparation21FValidationResult();
        BistroBuilderMenuEditor21EValidationResult prerequisite =
            BistroBuilderMenuEditor21EValidator.ValidateCurrentProject();

        if (prerequisite.ErrorCount > 0)
        {
            result.AddError("2.1E no sigue validado.");
        }
        else
        {
            result.AddCorrect("2.1E permanece válido como base transaccional.");
        }

        ValidateContracts(result);
        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded ||
            string.IsNullOrWhiteSpace(scene.path))
        {
            result.AddError("La escena activa no está cargada o guardada.");
            return result;
        }

        ValidateUnique<BistroBuilderSaveGameService>(
            scene,
            result,
            "servicio universal de guardado"
        );
        ValidateUnique<BistroBuilderMenuSaveSectionProvider>(
            scene,
            result,
            "proveedor menu.state"
        );
        ValidateUnique<BistroBuilderMenuStateV1ToV2Migration>(
            scene,
            result,
            "migración v1 -> v2"
        );
        ValidateUnique<BistroBuilderMenuStateV2ToV3Migration>(
            scene,
            result,
            "migración v2 -> v3"
        );
        ValidateUnique<BistroBuilderMenuEditorService>(
            scene,
            result,
            "servicio de editor"
        );
        ValidateUnique<BistroBuilderOrderLineExecutionService>(
            scene,
            result,
            "ejecución de líneas de cocina"
        );


        List<BistroBuilderSaveGameService> saveServices =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderSaveGameService
            >(scene);

        if (saveServices.Count == 1)
        {
            saveServices[0].RefreshExtensions();

            if (saveServices[0].ValidateConfiguration(out string saveError))
            {
                result.AddCorrect(
                    "La plataforma de guardado registra la cadena V1 -> V2 -> V3."
                );
            }
            else
            {
                result.AddError(
                    "La plataforma de guardado no acepta 2.1F: " + saveError
                );
            }
        }

        List<BistroBuilderMenuStateV2ToV3Migration> migrations =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderMenuStateV2ToV3Migration
            >(scene);

        if (migrations.Count == 1)
        {
            BistroBuilderMenuStateV2ToV3Migration migration = migrations[0];
            bool validMigration =
                migration.SectionId ==
                    BistroBuilderMenuSaveSectionProvider.StableSectionId &&
                migration.FromVersion == 2 &&
                migration.ToVersion == 3 &&
                migration.FromSerializerId ==
                    BistroBuilderJsonSaveSerializer.StableSerializerId &&
                migration.ToSerializerId ==
                    BistroBuilderJsonSaveSerializer.StableSerializerId;

            if (validMigration)
            {
                result.AddCorrect(
                    "La migración consecutiva menu.state V2 -> V3 es válida."
                );
            }
            else
            {
                result.AddError(
                    "La migración menu.state V2 -> V3 tiene un contrato inválido."
                );
            }
        }

        List<BistroBuilderMenuSaveSectionProvider> providers =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<
                BistroBuilderMenuSaveSectionProvider
            >(scene);

        if (providers.Count == 1)
        {
            bool correctVersion = providers[0].SectionVersion == 3;
            bool valid = providers[0].ValidateConfiguration(
                out string providerError
            );

            if (correctVersion && valid)
            {
                result.AddCorrect(
                    "menu.state v3 está configurado y operativo."
                );
            }
            else
            {
                result.AddError(
                    "menu.state v3 no es válido: " +
                    (!correctVersion
                        ? "versión incorrecta"
                        : providerError)
                );
            }
        }

        return result;
    }

    private static void ValidateContracts(
        BistroBuilderMenuPreparation21FValidationResult result
    )
    {
        bool ranges =
            BistroBuilderDishDefinition.MinimumPreparationDifficulty == 1 &&
            BistroBuilderDishDefinition.MaximumPreparationDifficulty == 10 &&
            BistroBuilderDishDefinition.MinimumPreparationSeconds == 1 &&
            BistroBuilderDishDefinition.MaximumPreparationSeconds == 86400;

        if (ranges)
        {
            result.AddCorrect(
                "Los rangos canónicos de dificultad y tiempo son estables."
            );
        }
        else
        {
            result.AddError("Los rangos canónicos de preparación son inválidos.");
        }

        MethodInfo difficulty = typeof(BistroBuilderMenuEditorService)
            .GetMethod(
                "TrySetPreparationDifficulty",
                new[] { typeof(string), typeof(int) }
            );
        MethodInfo time = typeof(BistroBuilderMenuEditorService)
            .GetMethod(
                "TrySetBasePreparationSeconds",
                new[] { typeof(string), typeof(int) }
            );
        MethodInfo resolver = typeof(BistroBuilderRestaurantMenuService)
            .GetMethod(
                "TryResolvePreparationSettings",
                BindingFlags.Instance | BindingFlags.Public
            );
        MethodInfo kitchen = typeof(BistroBuilderOrderLineExecutionService)
            .GetMethod(
                "TryResolveDishPreparationDurationSeconds",
                BindingFlags.Instance | BindingFlags.Public
            );

        if (difficulty != null && time != null)
        {
            result.AddCorrect(
                "El editor publica cambios transaccionales de dificultad y tiempo."
            );
        }
        else
        {
            result.AddError("Faltan contratos editables de preparación.");
        }

        if (resolver != null && kitchen != null)
        {
            result.AddCorrect(
                "La cocina consume la preparación efectiva de la carta."
            );
        }
        else
        {
            result.AddError("Falta la integración carta-cocina 2.1F.");
        }

        int currentSchemaVersion =
            BistroBuilderMenuSaveData.CurrentSchemaVersion;
        int stableSectionVersion =
            BistroBuilderMenuSaveSectionProvider.StableSectionVersion;

        if (currentSchemaVersion == 3 && stableSectionVersion == 3)
        {
            result.AddCorrect("El contrato persistente actual es menu.state v3.");
        }
        else
        {
            result.AddError(
                $"La persistencia publica versiones incompatibles: " +
                $"schema={currentSchemaVersion}, sección={stableSectionVersion}."
            );
        }
    }

    private static void ValidateUnique<T>(
        Scene scene,
        BistroBuilderMenuPreparation21FValidationResult result,
        string label
    ) where T : Component
    {
        List<T> components =
            BistroBuilderMenuEditor21EInstaller.FindSceneComponents<T>(scene);

        if (components.Count == 1)
        {
            result.AddCorrect("Existe un único " + label + ".");
        }
        else
        {
            result.AddError(
                "Debe existir un único " + label + "; hay " +
                components.Count + "."
            );
        }
    }
}
