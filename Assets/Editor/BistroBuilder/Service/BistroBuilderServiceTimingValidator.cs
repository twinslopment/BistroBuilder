using UnityEditor;
using UnityEngine;

public static class BistroBuilderServiceTimingValidator
{
    [MenuItem("Bistro Builder/Servicio/Timing contextual/Validar")]
    public static void ValidateFromMenu()
    {
        Validate(true);
    }

    public static bool Validate(bool logResult)
    {
        int errors = 0;

        BistroBuilderServiceTimingCatalog catalog =
            AssetDatabase.LoadAssetAtPath<BistroBuilderServiceTimingCatalog>(
                BistroBuilderServiceTimingInstaller.AssetPath
            );

        if (catalog == null)
        {
            errors++;
            if (logResult) Debug.LogError("No existe BB_ServiceTimingCatalog.");
        }
        else
        {
            if (!catalog.Validate(out string catalogError))
            {
                errors++;
                if (logResult) Debug.LogError(catalogError);
            }

            if (!ValidateFixedProfile(
                    catalog,
                    BistroBuilderServiceTimingPhase.BillDelivery,
                    90f,
                    120f,
                    210f,
                    300f,
                    420f,
                    1500,
                    2500,
                    "BillDelivery",
                    logResult
                ))
            {
                errors++;
            }

            if (!ValidateFixedProfile(
                    catalog,
                    BistroBuilderServiceTimingPhase.TakeOrder,
                    10f,
                    20f,
                    35f,
                    50f,
                    70f,
                    1500,
                    2500,
                    "TakeOrder",
                    logResult
                ))
            {
                errors++;
            }

            BistroBuilderFoodTimingPolicy food =
                catalog.FoodTimingPolicy;
            if (food == null ||
                !Approximately(food.MinimumExpectedSeconds, 4f) ||
                !Approximately(food.AttentionMultiplier, 1.15f) ||
                !Approximately(food.AttentionOffsetSeconds, 0f) ||
                !Approximately(food.DelayMultiplier, 1.35f) ||
                !Approximately(food.DelayOffsetSeconds, 4f) ||
                !Approximately(food.IncidentMultiplier, 2f) ||
                !Approximately(food.IncidentOffsetSeconds, 0f) ||
                !Approximately(food.CriticalMultiplier, 3f) ||
                !Approximately(food.CriticalOffsetSeconds, 30f) ||
                food.ExplanationPenaltyMitigationBasisPoints != 1500 ||
                food.ApologyPenaltyMitigationBasisPoints != 2500)
            {
                errors++;
                if (logResult)
                {
                    Debug.LogError(
                        "La política dinámica FoodDelivery no coincide con el tuning provisional."
                    );
                }
            }

            if (catalog.RecoverableServiceIncidentPenaltyBasisPoints !=
                    1000 ||
                catalog.RecoverableServiceIncidentApologyRecoveryBasisPoints !=
                    500)
            {
                errors++;
                if (logResult)
                {
                    Debug.LogError(
                        "El tuning de incidencias explícitas no coincide con el provisional."
                    );
                }
            }
        }

        WaiterTaskCoordinator[] coordinators =
            Object.FindObjectsByType<WaiterTaskCoordinator>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        BistroBuilderTableContextActionService[] contexts =
            Object.FindObjectsByType<BistroBuilderTableContextActionService>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (coordinators.Length != 1)
        {
            errors++;
            if (logResult)
                Debug.LogError(
                    "Debe existir exactamente un WaiterTaskCoordinator. Encontrados: " +
                    coordinators.Length
                );
        }

        if (contexts.Length != 1)
        {
            errors++;
            if (logResult)
                Debug.LogError(
                    "Debe existir exactamente un TableContextActionService. Encontrados: " +
                    contexts.Length
                );
        }
        else if (!contexts[0].ValidateConfiguration(out string contextError))
        {
            errors++;
            if (logResult) Debug.LogError(contextError);
        }

        BillAssignmentSystem[] legacy =
            Object.FindObjectsByType<BillAssignmentSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        for (int index = 0; index < legacy.Length; index++)
        {
            if (legacy[index] != null && legacy[index].enabled)
            {
                errors++;
                if (logResult)
                    Debug.LogError(
                        "BillAssignmentSystem legacy debe permanecer desactivado; " +
                        "WaiterTaskCoordinator es la autoridad de cuentas."
                    );
            }
        }

        if (logResult)
            Debug.Log(
                errors == 0
                    ? "SERVICE TIMING VALIDATOR: PASS"
                    : "SERVICE TIMING VALIDATOR: FAIL (" + errors + ")"
            );

        return errors == 0;
    }

    private static bool ValidateFixedProfile(
        BistroBuilderServiceTimingCatalog catalog,
        BistroBuilderServiceTimingPhase phase,
        float target,
        float attention,
        float delay,
        float incident,
        float critical,
        int explanation,
        int apology,
        string label,
        bool logResult)
    {
        if (!catalog.TryGetProfile(
                phase,
                out BistroBuilderServiceTimingProfile profile
            ) ||
            profile == null)
        {
            if (logResult)
                Debug.LogError("Falta el perfil " + label + ".");
            return false;
        }

        bool valid =
            Approximately(profile.TargetSeconds, target) &&
            Approximately(profile.AttentionSeconds, attention) &&
            Approximately(profile.DelaySeconds, delay) &&
            Approximately(profile.IncidentSeconds, incident) &&
            Approximately(profile.CriticalSeconds, critical) &&
            profile.ExplanationPenaltyMitigationBasisPoints ==
                explanation &&
            profile.ApologyPenaltyMitigationBasisPoints == apology;

        if (!valid && logResult)
        {
            Debug.LogError(
                "El perfil " + label +
                " no coincide con el tuning provisional."
            );
        }

        return valid;
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.001f;
    }
}
