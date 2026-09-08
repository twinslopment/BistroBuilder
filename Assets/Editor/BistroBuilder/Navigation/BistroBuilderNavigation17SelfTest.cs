using System;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BistroBuilderNavigation17SelfTest
{
    public static int LastPassed { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/17 Navegacion/Ejecutar autotests")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity", OpenSceneMode.Single);
        int ok = 0;
        int fail = 0;
        StringBuilder report = new StringBuilder("BLOQUE 17 - AUTOTEST\n");
        void Check(bool condition, string label)
        {
            if (condition) { ok++; report.AppendLine("OK - " + label); }
            else { fail++; report.AppendLine("FAIL - " + label); }
        }

        BistroBuilderNavigationService navigation =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderNavigationService>();
        Check(navigation != null, "Servicio disponible");
        Check(navigation != null && navigation.ValidateConfiguration(out _),
            "Configuracion valida");

        if (navigation != null)
        {
            BistroBuilderSpatialInteractionService bbsis =
                UnityEngine.Object.FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
            Check(bbsis != null,
                "BBSIS disponible como autoridad unica de reservas espaciales");
            Check(typeof(BistroBuilderNavigationService).GetField(
                    "destinationReservations", BindingFlags.Instance | BindingFlags.NonPublic) == null,
                "Navigation no mantiene un sistema paralelo de reservas de destino");
            Check(navigation.ActiveDestinationReservationCount == 0,
                "Navigation observa cero leases de destino BBSIS al arrancar");
            Check(typeof(BistroBuilderNavigationService).GetMethod(
                    "TryReserveDestination", BindingFlags.Instance | BindingFlags.Public) != null,
                "Adaptador de destino conserva la frontera de delegacion a BBSIS");
            navigation.UpdateAgentPresence(
                "traffic:high", BistroBuilderNavigationAgentMask.Other,
                Vector3.zero, 0.3f, 40);
            navigation.UpdateAgentPresence(
                "traffic:low", BistroBuilderNavigationAgentMask.Other,
                new Vector3(0f, 0f, -0.5f), 0.3f, 20);            BistroBuilderNavigationLocalMoveDecision priorityDecision = navigation.SolveLocalVelocity(
                new BistroBuilderNavigationLocalMoveInput
                {
                    ownerId = "traffic:low", agentMask = BistroBuilderNavigationAgentMask.Other,
                    position = new Vector3(0f, 0f, -0.5f), currentVelocity = Vector3.zero,
                    preferredVelocity = Vector3.forward, radius = 0.3f, externalUrgency = 20
                });
            Check(priorityDecision.shouldYield && priorityDecision.yieldingTo == "traffic:high",
                "Urgencia externa estable cede prioridad sin bloquear avoidance lateral");

            navigation.RemoveAgentPresence("traffic:high");
            navigation.RemoveAgentPresence("traffic:low");
            Check(navigation.ActiveAgentCount == 0,
                "Presencia transitoria se limpia");

            GameObject envelopeObject = new GameObject("__BB17_TestEnvelope__");
            BistroBuilderDynamicCirculationEnvelope envelope =
                envelopeObject.AddComponent<BistroBuilderDynamicCirculationEnvelope>();
            envelope.ConfigureForEditor(Vector3.zero, new Vector2(1f, 1f),
                BistroBuilderNavigationAgentMask.All,
                BistroBuilderDynamicSpaceKind.TemporaryObstacle);
            int structuralRevisionBeforeEnvelope = navigation.Revision;
            int trafficEpochBeforeEnvelope = navigation.TrafficEpoch;
            navigation.RegisterDynamicEnvelope(envelope);
            envelope.SetActiveWindow("test:envelope", true);
            navigation.NotifyDynamicSpaceChanged();
            int revisionWithEnvelope = navigation.Revision;
            Check(!navigation.CanAdvance(
                "test:outsider", BistroBuilderNavigationAgentMask.Waiter,
                Vector3.zero, 0.2f, 40),
                "Obstaculo dinamico bloquea el paso");
            Check(navigation.CanAdvance(
                "test:envelope", BistroBuilderNavigationAgentMask.Waiter,
                Vector3.zero, 0.2f, 40),
                "Propietario puede ocupar su propio espacio temporal");
            envelope.SetActiveWindow("test:envelope", false);
            navigation.NotifyDynamicSpaceChanged();
            Check(navigation.Revision == structuralRevisionBeforeEnvelope,
                "Cambios dinamicos no invalidan la topologia estructural");
            Check(navigation.TrafficEpoch > trafficEpochBeforeEnvelope,
                "Cambios dinamicos actualizan TrafficEpoch");
            navigation.UnregisterDynamicEnvelope(envelope);
            UnityEngine.Object.DestroyImmediate(envelopeObject);
        }

        RestaurantArea[] areas = UnityEngine.Object.FindObjectsByType<RestaurantArea>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        RestaurantArea kitchen = areas.FirstOrDefault(area =>
            area != null && (area.AreaId ?? string.Empty).Contains("kitchen"));
        BistroBuilderNavigationAccessZone kitchenZone =
            kitchen != null ? kitchen.GetComponent<BistroBuilderNavigationAccessZone>() : null;
        Check(kitchenZone != null &&
              !kitchenZone.Allows(BistroBuilderNavigationAgentMask.Customer),
            "Politica impide atajo de clientes por cocina");
        Check(kitchenZone != null &&
              kitchenZone.Allows(BistroBuilderNavigationAgentMask.Waiter),
            "Politica permite paso de camareros por cocina");

        RestaurantSeat[] seats = UnityEngine.Object.FindObjectsByType<RestaurantSeat>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        Check(seats.Length > 0 && seats.All(seat =>
            seat.GetComponent<BistroBuilderSeatCirculationEnvelope>() != null),
            "Sillas participan en espacio dinamico");
        Check(typeof(BistroBuilderNavigableDoor).GetMethod("TrySetOpen") != null,
            "Puertas exponen apertura coordinada con navegacion");
        Check(typeof(BistroBuilderWaiterRoutingService).GetField(
            "navigationService", BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "IA de Camareros 13 delega en Navegacion 17");

        Check(typeof(CustomerMovementView).GetField(
            "navigationService", BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "Clientes delegan en Navegacion 17");
        Check(typeof(BistroBuilderSupplyDeliveryVisual).GetField(
            "navigationService", BindingFlags.Instance | BindingFlags.NonPublic) != null,
            "Repartidores delegan en Navegacion 17");
        Check(BistroBuilderNavigationRuntimeSnapshot.CurrentSchemaId ==
              "navigation.runtime",
            "Contrato de persistencia/reconstruccion estable");

        if (navigation != null)
        {
            BistroBuilderCirculationHealthReport health =
                navigation.EvaluateCirculationHealth();
            Check(health != null && health.checkedConnections > 0,
                "Salud de circulacion inspecciona conexiones reales");
            Check(health != null && health.blockingCount == 0,
                "Restaurante actual no tiene rutas criticas bloqueadas");
        }

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

