using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BistroBuilderInteractionV1Validator
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/Interaction & Reservation v1/Validar")]
    public static void Run()
    {
        int ok = 0;
        int fail = 0;
        var report = new StringBuilder("BB INTERACTION & RESERVATION v1 - VALIDACION\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        BistroBuilderInteractionService[] services =
            UnityEngine.Object.FindObjectsByType<BistroBuilderInteractionService>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(services.Length == 1, "Una única autoridad lógica");
        BistroBuilderInteractionService service = services.Length == 1 ? services[0] : null;
        Check(service != null && service.ValidateConfiguration(out _),
            "Configuración runtime válida");
        Check(UnityEngine.Object.FindObjectsByType<BistroBuilderInteractionBbsisBridge>(
                  FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "Un único bridge BBSIS");
        Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderInteractionSaveSectionProvider>() != null,
            "Proveedor Save/Load instalado");
        Check(UnityEngine.Object.FindObjectsByType<BistroBuilderWaiterTaskClaimCoordinator>(
                  FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "Un único coordinador de Task Claims de camarero");
        Check(UnityEngine.Object.FindObjectsByType<BistroBuilderKitchenInteractionCoordinator>(
                  FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "Un único coordinador de capacidad lógica de cocina");
        Check(UnityEngine.Object.FindObjectsByType<BistroBuilderDishCustodyCoordinator>(
                  FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 1,
            "Un único coordinador de Custody de platos físicos");
        BistroBuilderOrderLineExecutionService lineExecution =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderOrderLineExecutionService>();
        Check(lineExecution != null && lineExecution.ValidateConfiguration(out _),
            "OrderLineExecution integrado con Custody física");
        Check(UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>() != null,
            "BBSIS disponible como autoridad espacial externa");

        BistroBuilderInteractionTarget[] targets =
            UnityEngine.Object.FindObjectsByType<BistroBuilderInteractionTarget>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        bool targetsValid = true;
        for (int i = 0; i < targets.Length; i++)
        {
            BistroBuilderInteractionTarget target = targets[i];
            if (target == null || string.IsNullOrWhiteSpace(target.TargetId) ||
                !ids.Add(target.TargetId) || !target.ValidateTarget(out _))
                targetsValid = false;
        }
        Check(targetsValid, "Targets con IDs estables y contratos válidos");

        Type[] interactionTypes =
        {
            typeof(BistroBuilderInteractionService),
            typeof(BistroBuilderInteractionTarget),
            typeof(BistroBuilderInteractionBbsisBridge)
        };
        bool noSpatialDuplication = true;
        for (int t = 0; t < interactionTypes.Length; t++)
        {
            FieldInfo[] fields = interactionTypes[t].GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for (int f = 0; f < fields.Length; f++)
            {
                Type fieldType = fields[f].FieldType;
                if (fieldType == typeof(BistroBuilderSpatialVolume) ||
                    fieldType == typeof(BistroBuilderSpatialLease) ||
                    fieldType == typeof(BistroBuilderSpatialClaimRequest))
                    noSpatialDuplication = false;
            }
        }
        Check(noSpatialDuplication,
            "Interaction no almacena geometría/Claims/Spatial Leases como estado propio");
        Check(service == null || service.ValidateRuntimeInvariants(out _),
            "Invariantes runtime válidas");

        LastPassed = ok;
        LastFailed = fail;
        report.AppendLine("Resultado: " + ok + " OK / " + fail + " fallos.");
        LastReport = report.ToString();
        Debug.Log(LastReport);
        if (fail > 0) throw new InvalidOperationException(LastReport);
    }

    public static void RunFromCommandLine()
    {
        try
        {
            EditorSceneManager.OpenScene(
                "Assets/Scenes/Prototype_Restaurant.unity",
                OpenSceneMode.Single);
            Run();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }
}
