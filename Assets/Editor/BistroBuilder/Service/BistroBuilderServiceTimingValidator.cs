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

            if (!catalog.TryGetProfile(
                    BistroBuilderServiceTimingPhase.BillDelivery,
                    out BistroBuilderServiceTimingProfile bill))
            {
                errors++;
                if (logResult) Debug.LogError("Falta el perfil BillDelivery.");
            }
            else if (!Approximately(bill.TargetSeconds, 90f) ||
                     !Approximately(bill.AttentionSeconds, 120f) ||
                     !Approximately(bill.DelaySeconds, 210f) ||
                     !Approximately(bill.IncidentSeconds, 300f) ||
                     !Approximately(bill.CriticalSeconds, 420f))
            {
                errors++;
                if (logResult)
                    Debug.LogError("El perfil BillDelivery no coincide con el tuning provisional aprobado.");
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

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.001f;
    }
}
