using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Safe preflight for the waiter population / kitchen-presence V1 bridge.
/// Does not touch saved scenes or invent operational tasks/characters.
/// </summary>
public static class BistroBuilderStaffPresenceV1SelfTest
{
    [MenuItem("Tools/Bistro Builder/Personal/V1 - Verificar agentes de sala y cocina", false, 3256)]
    public static void Run()
    {
        int passed = 0, failed = 0;
        void Check(bool condition, string label)
        {
            if (condition) passed++;
            else
            {
                failed++;
                Debug.LogError("[STAFF PRESENCE V1] FAIL: " + label);
            }
        }

        const string root = "Assets/Scripts/Application/Staff/Presence/";
        string population = Read(root + "BistroBuilderStaffWaiterPopulation.cs");
        string session = Read("Assets/Scripts/Application/Staff/BistroBuilderStaffSessionService.cs");
        string save = Read("Assets/Scripts/Application/Persistence/Service/BistroBuilderActiveServiceSaveSectionProvider.cs");
        string bridge = Read("Assets/Scripts/Application/Staff/Scheduling/BistroBuilderStaffScheduleSessionBridge.cs");
        string kitchen = Read("Assets/Scripts/Application/Kitchen/Advanced/BistroBuilderAdvancedKitchenService.cs");
        string opening = Read("Assets/Scripts/Application/Opening/BistroBuilderNewGameOpeningService.cs");
        string cook = Read(root + "BistroBuilderStaffCookPresence.cs");

        Check(population.Contains("TryEnsureMinimumSlots(") &&
            population.Contains("TryReconcileSavedIds(") &&
            population.Contains("TryConfigureGeneratedIdentity("),
            "La población usa identidades operativas únicas, no EmployeeId como WaiterId.");
        Check(population.Contains("WaiterTableServiceFlow") &&
            population.Contains("FoodDeliveryServiceFlow") &&
            population.Contains("BillServiceFlow") &&
            population.Contains("TableCleaningServiceFlow") &&
            population.Contains("RegisterWaiter(next)"),
            "El nuevo camarero es un agente funcional completo, no decoración.");
        Check(session.Contains("TryEnsureMinimumSlots(employeeBuffer.Count") &&
            session.Contains("ApplyBoundVisibility(sessionState)"),
            "4D obtiene suficientes slots antes del binding y oculta los no asignados.");
        Check(save.IndexOf("TryReconcileSavedIds(pendingData.waiters", StringComparison.Ordinal) >= 0 &&
            save.IndexOf("TryReconcileSavedIds(pendingData.waiters", StringComparison.Ordinal) <
            save.IndexOf("BuildWaiterIndexAndRestoreTransforms(pendingData", StringComparison.Ordinal),
            "service.runtime reconstruye IDs antes de cargar posiciones/comandas.");
        Check(bridge.Contains("TryResolveScheduledWaiterIds(") &&
            bridge.Contains("BistroBuilderStaffOperationalAdapterIds.WaiterAgent"),
            "Los turnos de cocina nunca se convierten en WaiterId.");
        Check(kitchen.Contains("scheduledCookEmployeeIds.Contains(cook.employeeId)") &&
            kitchen.Contains("GetComponent<BistroBuilderStaffCookPresence>()"),
            "Cocina selecciona cocineros programados y activa presentación separada.");
        Check(opening.Contains("TryScheduleInitialCook("),
            "La partida nueva incluye turno auténtico para el cocinero.");
        Check(cook.Contains("kitchenArea.ContainsPosition(") &&
            cook.Contains("cookVisualPrefab") &&
            cook.Contains("Resources.Load<GameObject>(") &&
            !cook.Contains("CreatePrimitive("),
            "Avatar de cocinero: solo en una zona Cocina verificada, sin muñecos genéricos.");

        GameObject temporary = null;
        try
        {
            temporary = new GameObject("__StaffPresenceIdentityTest__");
            Waiter waiter = temporary.AddComponent<Waiter>();
            temporary.AddComponent<BistroBuilderStaffGeneratedWaiter>();
            temporary.SetActive(false);
            Check(waiter.TryConfigureGeneratedIdentity(9173) && waiter.WaiterId == 9173,
                "Se permite asignar identidad únicamente al clon gestionado e inactivo.");
            temporary.SetActive(true);
            Check(!waiter.TryConfigureGeneratedIdentity(9174) && waiter.WaiterId == 9173,
                "Un agente activo no puede cambiar de WaiterId.");
        }
        finally
        {
            if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
        }

        // An author-approved character asset is required for the kitchen.
        // This is deliberately a warning, not an invented PASS.
        bool hasCookPrefab =
            Resources.Load<GameObject>("BistroBuilder/Characters/CookPresence") != null;
        if (!hasCookPrefab)
            Debug.LogWarning(
                "[STAFF PRESENCE V1] Pendiente: prefab 3D de cocinero en " +
                "Resources/BistroBuilder/Characters/CookPresence.prefab " +
                "(o asignarlo al adaptador de presencia de cocina).");

        string report = "[STAFF PRESENCE V1] STATIC " + passed + " PASS / " +
            failed + " FAIL · cook art " + (hasCookPrefab ? "PRESENT" : "PENDING") +
            ". No constituye Play Mode ni Save/Load PASS.";
        if (failed == 0) Debug.Log(report);
        else Debug.LogError(report);
    }

    private static string Read(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }
}
