using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ejecuta dos pruebas funcionales reales de 367H sobre la escena jugable.
/// Todos los cambios se realizan exclusivamente durante Play Mode y se
/// descartan al salir de él.
/// </summary>
public sealed class BistroBuilderBarServiceFunctionalTestWindow : EditorWindow
{
    private enum FunctionalTestKind
    {
        None = 0,
        ExclusiveBarService = 1,
        WaitingAtBarToTable = 2
    }

    private RestaurantServiceStateService serviceState;
    private CustomerGroupSpawner spawner;
    private BistroBuilderBarServiceSystem barSystem;
    private BistroBuilderBarServiceRegistry barRegistry;
    private TableAssignmentSystem tableAssignment;
    private CustomerGroup trackedGroup;
    private RestaurantTable releasedTable;
    private FunctionalTestKind activeTest;
    private int baselineCompletedBar;
    private int baselineCompletedWaiting;
    private bool groupEverHadTable;
    private bool tableReleased;
    private bool completionLogged;
    private double startedAt;
    private string status =
        "Entra en Play Mode con el servicio cerrado y elige una prueba.";
    private Vector2 scroll;

    private const double TimeoutSeconds = 120d;

    [MenuItem(
        "Tools/Bistro Builder/Service/367H Functional Bar Tests",
        false,
        268
    )]
    private static void OpenWindow()
    {
        BistroBuilderBarServiceFunctionalTestWindow window =
            GetWindow<BistroBuilderBarServiceFunctionalTestWindow>();
        window.titleContent = new GUIContent("BB 367H Test");
        window.minSize = new Vector2(680f, 520f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshReferences();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        RefreshReferencesIfNeeded();
        ObserveActiveTest();
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 367H — Pruebas funcionales de barra",
            EditorStyles.boldLabel
        );
        EditorGUILayout.Space(4f);

        EditorGUILayout.HelpBox(
            "Cada prueba debe ejecutarse en una entrada distinta a Play Mode " +
            "y con el servicio en Closed. La herramienta configura un único " +
            "grupo, acelera solo los tiempos de barra y no guarda ningún " +
            "cambio en la escena.",
            MessageType.Info
        );

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode, no abras el servicio manualmente y vuelve " +
                "a esta ventana.",
                MessageType.Warning
            );

            if (GUILayout.Button("Actualizar referencias", GUILayout.Height(28f)))
            {
                RefreshReferences();
            }

            DrawInstructions();
            return;
        }

        RefreshReferencesIfNeeded();

        if (!HasRequiredReferences())
        {
            EditorGUILayout.HelpBox(
                "No se localizaron todos los sistemas 367H obligatorios.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.LabelField(
            "Estado del servicio",
            serviceState.CurrentState.ToString()
        );
        EditorGUILayout.LabelField(
            "Plazas de barra",
            barRegistry.FreeCapacity + "/" +
            barRegistry.RegisteredSpotCount + " libres"
        );
        EditorGUILayout.LabelField(
            "Sesiones activas",
            barSystem.ActiveSessionCount.ToString()
        );

        EditorGUI.BeginDisabledGroup(
            !serviceState.IsClosed || activeTest != FunctionalTestKind.None
        );

        if (GUILayout.Button(
                "Prueba A — Servicio exclusivo en barra",
                GUILayout.Height(34f)
            ))
        {
            PrepareExclusiveBarTest();
        }

        if (GUILayout.Button(
                "Prueba B — Espera en barra y transición a mesa",
                GUILayout.Height(34f)
            ))
        {
            PrepareWaitingTransitionTest();
        }

        EditorGUI.EndDisabledGroup();

        if (!serviceState.IsClosed && activeTest == FunctionalTestKind.None)
        {
            EditorGUILayout.HelpBox(
                "El servicio ya está abierto sin una prueba 367H armada. " +
                "Sal de Play Mode y vuelve a entrar.",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Seguimiento", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status, ResolveStatusMessageType());
        DrawTrackedGroup();
        DrawResult();

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawInstructions();
        EditorGUILayout.EndScrollView();
    }

    private void PrepareExclusiveBarTest()
    {
        try
        {
            PrepareCommon(BistroBuilderServiceMode.BarService, true);
            activeTest = FunctionalTestKind.ExclusiveBarService;
            status =
                "Prueba A abierta. El cliente debe ocupar barra, pedir, " +
                "recibir, consumir, pagar y salir sin mesa.";
        }
        catch (Exception exception)
        {
            FailPreparation(exception);
        }
    }

    private void PrepareWaitingTransitionTest()
    {
        try
        {
            PrepareCommon(BistroBuilderServiceMode.WaitingAtBar, false);

            RestaurantTable[] tables = FindObjectsByType<RestaurantTable>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.InstanceID
            );

            if (tables.Length < 4)
            {
                throw new InvalidOperationException(
                    "La escena 367H debe contener al menos cuatro mesas."
                );
            }

            Array.Sort(tables, CompareTables);
            releasedTable = tables[0];

            for (int index = 0; index < tables.Length; index++)
            {
                tables[index].SetState(TableState.WaitingForFood);
            }

            if (!serviceState.TryOpenService())
            {
                throw new InvalidOperationException(
                    "RestaurantServiceStateService rechazó la apertura."
                );
            }

            activeTest = FunctionalTestKind.WaitingAtBarToTable;
            status =
                "Prueba B abierta con todas las mesas bloqueadas. La " +
                "herramienta liberará una mesa después de que exista una " +
                "comanda real de barra.";
        }
        catch (Exception exception)
        {
            FailPreparation(exception);
        }
    }

    private void PrepareCommon(
        BistroBuilderServiceMode mode,
        bool openService
    )
    {
        RefreshReferences();

        if (!HasRequiredReferences())
        {
            throw new InvalidOperationException(
                "Faltan servicios obligatorios de la escena."
            );
        }

        if (!serviceState.IsClosed)
        {
            throw new InvalidOperationException(
                "El servicio debe permanecer cerrado al preparar la prueba."
            );
        }

        activeTest = FunctionalTestKind.None;
        trackedGroup = null;
        releasedTable = null;
        groupEverHadTable = false;
        tableReleased = false;
        completionLogged = false;
        baselineCompletedBar = barSystem.CompletedBarServiceCount;
        baselineCompletedWaiting = barSystem.CompletedWaitingAtBarCount;
        startedAt = EditorApplication.timeSinceStartup;

        SerializedObject spawnerSerialized = new SerializedObject(spawner);
        RequireProperty(spawnerSerialized, "numberOfGroups").intValue = 1;
        RequireProperty(spawnerSerialized, "firstSpawnDelay").floatValue = 0.1f;
        RequireProperty(spawnerSerialized, "timeBetweenGroups").floatValue = 0.2f;
        RequireProperty(spawnerSerialized, "minimumGroupSize").intValue = 1;
        RequireProperty(spawnerSerialized, "maximumGroupSize").intValue = 1;
        spawnerSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (!spawner.TryConfigureDiagnosticGroupSizes(
                new[] { 1 },
                out string sizeError
            ))
        {
            throw new InvalidOperationException(sizeError);
        }

        if (!spawner.TryConfigureDiagnosticServiceModes(
                new[] { mode },
                out string modeError
            ))
        {
            throw new InvalidOperationException(modeError);
        }

        // Reduce exclusivamente los tiempos de interacción de barra. La
        // producción y el reparto siguen usando los sistemas reales.
        SerializedObject barSerialized = new SerializedObject(barSystem);
        RequireProperty(barSerialized, "orderTakingDuration").floatValue = 0.25f;
        RequireProperty(barSerialized, "consumptionDuration").floatValue = 0.5f;
        RequireProperty(barSerialized, "billDeliveryDuration").floatValue = 0.25f;
        RequireProperty(barSerialized, "paymentDuration").floatValue = 0.35f;
        RequireProperty(barSerialized, "maximumItemsPerBarOrder").intValue = 1;
        RequireProperty(barSerialized, "logChanges").boolValue = true;
        barSerialized.ApplyModifiedPropertiesWithoutUndo();

        if (openService && !serviceState.TryOpenService())
        {
            throw new InvalidOperationException(
                "RestaurantServiceStateService rechazó la apertura."
            );
        }
    }

    private void ObserveActiveTest()
    {
        if (activeTest == FunctionalTestKind.None ||
            !HasRequiredReferences())
        {
            return;
        }

        if (EditorApplication.timeSinceStartup - startedAt > TimeoutSeconds)
        {
            status = "ERROR: la prueba superó el timeout de seguridad.";
            activeTest = FunctionalTestKind.None;
            return;
        }

        ResolveTrackedGroup();

        if (trackedGroup != null && trackedGroup.HasAssignedTable)
        {
            groupEverHadTable = true;
        }

        if (activeTest == FunctionalTestKind.ExclusiveBarService)
        {
            ObserveExclusiveBarTest();
        }
        else if (activeTest == FunctionalTestKind.WaitingAtBarToTable)
        {
            ObserveWaitingTransitionTest();
        }
    }

    private void ObserveExclusiveBarTest()
    {
        bool completed =
            barSystem.CompletedBarServiceCount > baselineCompletedBar &&
            barSystem.LastCompletedSession.ServiceMode ==
                BistroBuilderServiceMode.BarService;

        if (!completed)
        {
            return;
        }

        bool valid =
            !groupEverHadTable &&
            !barSystem.LastCompletedSession.ChargeTransferredToTableBill &&
            barSystem.LastCompletedSession.AmountCents > 0 &&
            barSystem.ActiveSessionCount == 0 &&
            barRegistry.FreeCapacity == barRegistry.RegisteredSpotCount;

        if (valid)
        {
            status =
                "SUPERADA A: servicio completo en barra, pago directo, " +
                "ninguna mesa proxy y plaza liberada.";
            LogCompletionOnce(status);
        }
        else
        {
            status =
                "ERROR A: la sesión terminó, pero alguna invariante de " +
                "destino, pago o liberación no se cumplió.";
        }

        activeTest = FunctionalTestKind.None;
    }

    private void ObserveWaitingTransitionTest()
    {
        if (trackedGroup == null)
        {
            return;
        }

        if (!tableReleased &&
            barSystem.TryGetSessionSnapshot(
                trackedGroup,
                out BistroBuilderBarSessionSnapshot snapshot
            ) &&
            snapshot.TotalLineCount > 0 &&
            snapshot.ChargeCents > 0 &&
            snapshot.Phase >= BistroBuilderBarSessionPhase.WaitingForItems)
        {
            releasedTable.SetState(TableState.Free);
            tableAssignment.RequestReevaluation();
            tableReleased = true;
            status =
                "La comanda de barra ya existe. Mesa " +
                releasedTable.TableId +
                " liberada; debe quedar reservada hasta cerrar barra.";
        }

        bool completed =
            tableReleased &&
            barSystem.CompletedWaitingAtBarCount > baselineCompletedWaiting &&
            barSystem.LastCompletedSession.ServiceMode ==
                BistroBuilderServiceMode.WaitingAtBar &&
            barSystem.LastCompletedSession.ChargeTransferredToTableBill;

        if (!completed)
        {
            return;
        }

        int transferred =
            barSystem.GetPendingTransferredChargeCents(trackedGroup);
        bool valid =
            trackedGroup.HasAssignedTable &&
            !trackedGroup.HasAssignedBarSpot &&
            trackedGroup.CurrentServiceMode ==
                BistroBuilderServiceMode.TableService &&
            transferred > 0 &&
            barRegistry.FreeCapacity == barRegistry.RegisteredSpotCount;

        if (valid)
        {
            status =
                "SUPERADA B: consumo independiente, cargo transferido una " +
                "vez, plaza liberada y grupo sentado en la mesa reservada.";
            LogCompletionOnce(status);
        }
        else
        {
            status =
                "ERROR B: la sesión cerró, pero no se completó correctamente " +
                "la transición barra→mesa.";
        }

        activeTest = FunctionalTestKind.None;
    }

    private void ResolveTrackedGroup()
    {
        if (trackedGroup != null)
        {
            return;
        }

        CustomerGroup[] groups = FindObjectsByType<CustomerGroup>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.InstanceID
        );

        for (int index = 0; index < groups.Length; index++)
        {
            CustomerGroup group = groups[index];

            if (group == null)
            {
                continue;
            }

            bool matches =
                activeTest == FunctionalTestKind.ExclusiveBarService
                    ? group.RequestedServiceMode ==
                        BistroBuilderServiceMode.BarService
                    : group.RequestedServiceMode ==
                        BistroBuilderServiceMode.WaitingAtBar;

            if (matches)
            {
                trackedGroup = group;
                break;
            }
        }
    }

    private void DrawTrackedGroup()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Grupo observado", EditorStyles.boldLabel);

        if (trackedGroup == null)
        {
            EditorGUILayout.LabelField("Estado", "Aún no generado.");
            return;
        }

        EditorGUILayout.LabelField("GroupId", trackedGroup.GroupId.ToString());
        EditorGUILayout.LabelField(
            "Modalidad solicitada",
            trackedGroup.RequestedServiceMode.ToString()
        );
        EditorGUILayout.LabelField(
            "Modalidad actual",
            trackedGroup.CurrentServiceMode.ToString()
        );
        EditorGUILayout.LabelField(
            "Estado",
            trackedGroup.CurrentState.ToString()
        );
        EditorGUILayout.LabelField(
            "Mesa",
            trackedGroup.HasAssignedTable
                ? trackedGroup.AssignedTable.TableId.ToString()
                : "Ninguna"
        );
        EditorGUILayout.LabelField(
            "Barra",
            trackedGroup.HasAssignedBarSpot
                ? trackedGroup.AssignedBarSpot.BarSpotId
                : "Ninguna"
        );

        if (barSystem.TryGetSessionSnapshot(
                trackedGroup,
                out BistroBuilderBarSessionSnapshot snapshot
            ))
        {
            EditorGUILayout.LabelField("Fase de barra", snapshot.Phase.ToString());
            EditorGUILayout.LabelField(
                "Líneas servidas",
                snapshot.ServedLineCount + "/" + snapshot.TotalLineCount
            );
            EditorGUILayout.LabelField(
                "Importe",
                FormatCents(snapshot.ChargeCents)
            );
        }
    }

    private void DrawResult()
    {
        if (status.StartsWith("SUPERADA", StringComparison.Ordinal))
        {
            EditorGUILayout.HelpBox(
                "PRUEBA FUNCIONAL 367H " + status,
                MessageType.Info
            );
        }
    }

    private static void DrawInstructions()
    {
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Procedimiento",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "1. Ejecuta primero la Prueba A y espera a que aparezca SUPERADA.\n" +
            "2. Sal de Play Mode y vuelve a entrar.\n" +
            "3. Ejecuta la Prueba B y espera a que aparezca SUPERADA.\n" +
            "4. No abras el servicio manualmente ni añadas clientes durante " +
            "las pruebas.",
            MessageType.None
        );
    }

    private MessageType ResolveStatusMessageType()
    {
        if (status.StartsWith("ERROR", StringComparison.Ordinal))
        {
            return MessageType.Error;
        }

        if (status.StartsWith("SUPERADA", StringComparison.Ordinal))
        {
            return MessageType.Info;
        }

        return MessageType.None;
    }

    private void RefreshReferencesIfNeeded()
    {
        if (serviceState == null || spawner == null || barSystem == null ||
            barRegistry == null || tableAssignment == null)
        {
            RefreshReferences();
        }
    }

    private void RefreshReferences()
    {
        serviceState = FindFirstObjectByType<RestaurantServiceStateService>();
        spawner = FindFirstObjectByType<CustomerGroupSpawner>();
        barSystem = FindFirstObjectByType<BistroBuilderBarServiceSystem>();
        barRegistry = FindFirstObjectByType<BistroBuilderBarServiceRegistry>();
        tableAssignment = FindFirstObjectByType<TableAssignmentSystem>();
    }

    private bool HasRequiredReferences()
    {
        return serviceState != null &&
               spawner != null &&
               barSystem != null &&
               barRegistry != null &&
               tableAssignment != null;
    }

    private void FailPreparation(Exception exception)
    {
        activeTest = FunctionalTestKind.None;
        status = "ERROR: " + exception.Message;
        Debug.LogException(exception);
    }

    private void LogCompletionOnce(string message)
    {
        if (completionLogged)
        {
            return;
        }

        completionLogged = true;
        Debug.Log("BISTRO BUILDER — PRUEBA FUNCIONAL 367H " + message);
    }

    private static int CompareTables(RestaurantTable left, RestaurantTable right)
    {
        if (ReferenceEquals(left, right))
        {
            return 0;
        }
        if (left == null)
        {
            return 1;
        }
        if (right == null)
        {
            return -1;
        }
        return left.TableId.CompareTo(right.TableId);
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string propertyName
    )
    {
        SerializedProperty property = serialized.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + propertyName + "."
            );
        }

        return property;
    }

    private static string FormatCents(int cents)
    {
        return (Mathf.Max(0, cents) / 100f).ToString("0.00") + " €";
    }
}
