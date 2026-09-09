using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Autotest puro de nombres, estados y lectura legible de 10G.</summary>
public static class BistroBuilderAdvancedCustomers10GSelfTest
{
    [MenuItem("Tools/Bistro Builder/Customers/10G - Autotest", false, 10062)]
    private static void RunFromMenu()
    {
        bool ok = Run(out _, out _, out string report);
        if (ok) Debug.Log(report); else Debug.LogError(report);
    }

    public static void RunFromCommandLine()
    {
        if (!Run(out _, out _, out string report))
            throw new InvalidOperationException(report);
        Debug.Log(report);
    }

    public static bool Run(out int passed, out int failed, out string report)
    {
        int p = 0; int f = 0;
        var lines = new List<string>();
        void Check(bool condition, string text)
        {
            if (condition) { p++; lines.Add("[OK] " + text); }
            else { f++; lines.Add("[FAIL] " + text); }
        }
        var acquisition = BistroBuilderCustomerAcquisitionProfile.CreateBaseline();
        acquisition.segmentId = "workers";
        acquisition.sourceSystemId = "customers.10g.test";
        acquisition.sourceReferenceId = "fixture";
        var seed = BistroBuilderAdvancedCustomers10ASeed.BuildComplete();
        bool firstOk = BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
            500, 3, 4, acquisition, seed, out var first, out _);
        bool secondOk = BistroBuilderAdvancedCustomerProfileEngine.TryBuildGroupProfile(
            500, 3, 4, acquisition, seed, out var second, out _);
        Check(firstOk && secondOk && first.members.Count == 3,
            "10G recibe tres identidades individuales desde el perfil 10A.");
        Check(!string.IsNullOrWhiteSpace(first.members[0].displayName),
            "Cada cliente materializado dispone de un nombre legible.");
        Check(first.members[0].displayName == second.members[0].displayName,
            "El nombre individual es determinista para una misma identidad.");
        Check(first.members[0].displayName != first.members[1].displayName ||
              first.members[0].customerId != first.members[1].customerId,
            "Los miembros del grupo mantienen identidades individuales diferenciadas.");

        Check(BistroBuilderAdvancedCustomerInspectionService.FormatServiceState(
                CustomerGroupState.WaitingForWaiter) ==
              "Esperando a que le tomen nota",
            "WaitingForWaiter se presenta en lenguaje natural.");
        Check(BistroBuilderAdvancedCustomerInspectionService.FormatServiceState(
                CustomerGroupState.WaitingForFood).Contains("Pedido realizado"),
            "WaitingForFood informa de que el pedido ya está hecho.");
        Check(BistroBuilderAdvancedCustomerInspectionService.FormatServiceState(
                CustomerGroupState.WaitingForBill) == "Esperando la cuenta",
            "WaitingForBill se presenta como espera de cuenta.");
        Check(BistroBuilderAdvancedCustomerInspectionService.ResolvePatienceBand(8200) ==
              BistroBuilderCustomerPatienceBand.High &&
              BistroBuilderAdvancedCustomerInspectionService.ResolvePatienceBand(5200) ==
              BistroBuilderCustomerPatienceBand.Medium &&
              BistroBuilderAdvancedCustomerInspectionService.ResolvePatienceBand(2500) ==
              BistroBuilderCustomerPatienceBand.Low &&
              BistroBuilderAdvancedCustomerInspectionService.ResolvePatienceBand(500) ==
              BistroBuilderCustomerPatienceBand.Exhausted,
            "La paciencia visible distingue alta, media, baja y al límite.");
        Check(BistroBuilderAdvancedCustomerInspectionService.FormatMood(
                BistroBuilderCustomerBehaviorMood.Critical) == "Muy molesto",
            "El estado conductual crítico usa una etiqueta comprensible.");
        Check(BistroBuilderAdvancedCustomerInspectionService.FormatSatisfaction(9000) ==
              "Muy satisfecho" &&
              BistroBuilderAdvancedCustomerInspectionService.FormatSatisfaction(2000) ==
              "Muy descontento",
            "La satisfacción se resume sin exponer métricas técnicas.");

        passed = p; failed = f;
        report = "=== BISTRO BUILDER — 10G / FICHA INDIVIDUAL ===\n" +
            string.Join("\n", lines) + "\nResultado: " + p +
            " OK / " + f + " fallos.";
        return f == 0;
    }
}
