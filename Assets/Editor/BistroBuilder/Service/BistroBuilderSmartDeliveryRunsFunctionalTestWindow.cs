using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Prepara una prueba funcional real y reproducible de 367G1 sobre la escena
/// jugable. Todos los cambios se realizan exclusivamente durante Play Mode y
/// se descartan al salir de él.
/// </summary>
public sealed class BistroBuilderSmartDeliveryRunsFunctionalTestWindow :
    EditorWindow
{
    private const int TargetLineCount = 3;
    private const int TargetTableCount = 2;
    private const float SafetyTimeoutSeconds = 90f;

    private WaiterTaskCoordinator coordinator;
    private RestaurantServiceStateService serviceState;
    private BistroBuilderOrderCompositionProfile temporaryProfile;
    private string status = "Entra en Play Mode con el servicio cerrado.";
    private Vector2 scroll;
    private bool completionLogged;

    [MenuItem(
        "Tools/Bistro Builder/Service/" +
        "367G1 Functional Multi-Table Test",
        false,
        263
    )]
    private static void OpenWindow()
    {
        BistroBuilderSmartDeliveryRunsFunctionalTestWindow window =
            GetWindow<BistroBuilderSmartDeliveryRunsFunctionalTestWindow>();
        window.titleContent = new GUIContent("BB 367G1 Test");
        window.minSize = new Vector2(600f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshReferences();
    }

    private void Update()
    {
        if (Application.isPlaying)
            Repaint();
    }

    private void OnDisable()
    {
        // El perfil temporal debe seguir vivo mientras continúe Play Mode.
        // Unity lo elimina automáticamente al salir por HideAndDontSave.
        temporaryProfile = null;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 367G1 — Prueba funcional determinista",
            EditorStyles.boldLabel
        );
        EditorGUILayout.Space(4f);

        EditorGUILayout.HelpBox(
            "La prueba usa la escena real, una sola cocina y un único " +
            "camarero operativo. Genera grupos de 2 y 1 clientes y una " +
            "línea individual por cliente en el primer pase. El coordinador " +
            "retiene el reparto hasta reunir al menos 3 platos de 2 mesas. " +
            "Los cambios existen solo durante Play Mode.",
            MessageType.Info
        );

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode, mantén el servicio en Closed y vuelve " +
                "a esta ventana.",
                MessageType.Warning
            );

            if (GUILayout.Button("Actualizar referencias", GUILayout.Height(28f)))
                RefreshReferences();

            return;
        }

        if (coordinator == null || serviceState == null)
            RefreshReferences();

        if (coordinator == null || serviceState == null)
        {
            EditorGUILayout.HelpBox(
                "No se localizaron WaiterTaskCoordinator y " +
                "RestaurantServiceStateService.",
                MessageType.Error
            );
            return;
        }

        EditorGUILayout.LabelField(
            "Estado del servicio",
            serviceState.CurrentState.ToString()
        );
        EditorGUILayout.LabelField(
            "Ventana normal de consolidación",
            coordinator.DeliveryRunConsolidationSeconds.ToString("0.00") +
            " s reales"
        );

        EditorGUI.BeginDisabledGroup(!serviceState.IsClosed);
        if (GUILayout.Button(
                "Preparar y abrir prueba 3 platos / 2 mesas",
                GUILayout.Height(34f)
            ))
        {
            PrepareAndOpenTest();
        }
        EditorGUI.EndDisabledGroup();

        if (!serviceState.IsClosed &&
            !coordinator.IsFunctionalDiagnosticArmed &&
            coordinator.FunctionalDiagnosticRun == null)
        {
            EditorGUILayout.HelpBox(
                "La prueba debe prepararse antes de abrir el servicio.",
                MessageType.Warning
            );
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Seguimiento", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.LabelField(
            "Coordinador",
            string.IsNullOrWhiteSpace(coordinator.FunctionalDiagnosticStatus)
                ? "Sin diagnóstico armado."
                : coordinator.FunctionalDiagnosticStatus
        );

        DrawObservedRun();

        EditorGUILayout.Space(8f);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawInstructions();
        EditorGUILayout.EndScrollView();
    }

    private void PrepareAndOpenTest()
    {
        try
        {
            RefreshReferences();

            if (coordinator == null || serviceState == null)
                throw new InvalidOperationException(
                    "Faltan servicios obligatorios de la escena."
                );

            if (!serviceState.IsClosed)
                throw new InvalidOperationException(
                    "El servicio debe permanecer cerrado al preparar la prueba."
                );

            Waiter[] waiters = UnityEngine.Object.FindObjectsByType<Waiter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            RestaurantTable[] tables =
                UnityEngine.Object.FindObjectsByType<RestaurantTable>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            KitchenSystem[] kitchens =
                UnityEngine.Object.FindObjectsByType<KitchenSystem>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            if (waiters.Length == 0)
                throw new InvalidOperationException(
                    "La escena no contiene camareros."
                );

            if (tables.Length < TargetTableCount)
                throw new InvalidOperationException(
                    "La escena necesita al menos dos mesas operativas."
                );

            if (kitchens.Length != 1)
                throw new InvalidOperationException(
                    "La prueba determinista requiere una única cocina."
                );

            Array.Sort(waiters, CompareWaiters);
            Waiter primaryWaiter = waiters[0];

            RestrictRuntimeSystemsToWaiter(primaryWaiter);
            ConfigureDeterministicSpawner();
            ConfigureFirstCourseIndividualProfile();
            ConfigureCoordinator();

            for (int index = 1; index < waiters.Length; index++)
                coordinator.UnregisterWaiter(waiters[index]);

            coordinator.RegisterWaiter(primaryWaiter);

            if (!coordinator.TryArmFunctionalDeliveryDiagnostic(
                    TargetLineCount,
                    TargetTableCount,
                    SafetyTimeoutSeconds,
                    out string diagnosticError
                ))
            {
                throw new InvalidOperationException(diagnosticError);
            }

            if (!serviceState.TryOpenService())
                throw new InvalidOperationException(
                    "RestaurantServiceStateService rechazó la apertura."
                );

            completionLogged = false;
            status =
                "Prueba preparada y servicio abierto. Espera a que aparezca " +
                "una ronda con 3 platos y 2 mesas; la ventana se actualizará " +
                "automáticamente.";
        }
        catch (Exception exception)
        {
            status = "ERROR: " + exception.Message;
            Debug.LogException(exception);
        }
    }

    private void RestrictRuntimeSystemsToWaiter(Waiter primaryWaiter)
    {
        SetSingleWaiter(
            UnityEngine.Object.FindFirstObjectByType<WaiterAssignmentSystem>(),
            primaryWaiter
        );
        SetSingleWaiter(
            UnityEngine.Object.FindFirstObjectByType<BillAssignmentSystem>(),
            primaryWaiter
        );
        SetSingleWaiter(
            UnityEngine.Object.FindFirstObjectByType<
                TableCleaningAssignmentSystem
            >(),
            primaryWaiter
        );
        SetSingleWaiter(
            UnityEngine.Object.FindFirstObjectByType<
                FoodDeliveryAssignmentSystem
            >(),
            primaryWaiter
        );
    }

    private static void SetSingleWaiter(
        MonoBehaviour system,
        Waiter primaryWaiter
    )
    {
        if (system == null)
            return;

        SerializedObject serialized = new SerializedObject(system);
        SerializedProperty waitersProperty =
            serialized.FindProperty("waiters");

        if (waitersProperty == null || !waitersProperty.isArray)
            return;

        waitersProperty.arraySize = 1;
        waitersProperty.GetArrayElementAtIndex(0).objectReferenceValue =
            primaryWaiter;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void ConfigureDeterministicSpawner()
    {
        CustomerGroupSpawner spawner =
            UnityEngine.Object.FindFirstObjectByType<CustomerGroupSpawner>();

        if (spawner == null)
            throw new InvalidOperationException(
                "No se encontró CustomerGroupSpawner."
            );

        SerializedObject serialized = new SerializedObject(spawner);
        RequireProperty(serialized, "numberOfGroups").intValue = 2;
        RequireProperty(serialized, "firstSpawnDelay").floatValue = 0.1f;
        RequireProperty(serialized, "timeBetweenGroups").floatValue = 0.5f;
        RequireProperty(serialized, "minimumGroupSize").intValue = 1;
        RequireProperty(serialized, "maximumGroupSize").intValue = 2;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        if (!spawner.TryConfigureDiagnosticGroupSizes(
                new[] { 2, 1 },
                out string sequenceError
            ))
        {
            throw new InvalidOperationException(sequenceError);
        }
    }

    private void ConfigureFirstCourseIndividualProfile()
    {
        BistroBuilderOrderCompositionService composition =
            UnityEngine.Object.FindFirstObjectByType<
                BistroBuilderOrderCompositionService
            >();

        if (composition == null)
            throw new InvalidOperationException(
                "No se encontró BistroBuilderOrderCompositionService."
            );

        temporaryProfile = ScriptableObject.CreateInstance<
            BistroBuilderOrderCompositionProfile
        >();
        temporaryProfile.name = "__BB_367G1_FUNCTIONAL_PROFILE__";
        temporaryProfile.hideFlags = HideFlags.HideAndDontSave;

        SerializedObject profileSerialized =
            new SerializedObject(temporaryProfile);
        RequireProperty(profileSerialized, "coordinationPolicy").enumValueIndex =
            (int)BistroBuilderCourseCoordinationPolicy.PerTable;

        SerializedProperty rules = RequireProperty(
            profileSerialized,
            "rules"
        );
        rules.arraySize = 1;

        SerializedProperty rule = rules.GetArrayElementAtIndex(0);
        rule.FindPropertyRelative("enabled").boolValue = true;
        rule.FindPropertyRelative("courseIndex").intValue = 1;
        rule.FindPropertyRelative("compositionMode").enumValueIndex =
            (int)BistroBuilderOrderLineCompositionMode.IndividualPerCustomer;
        rule.FindPropertyRelative("menuDisplayOffset").intValue = 0;
        rule.FindPropertyRelative("sharedGroupSize").intValue = 2;
        profileSerialized.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject compositionSerialized =
            new SerializedObject(composition);
        RequireProperty(
            compositionSerialized,
            "compositionProfile"
        ).objectReferenceValue = temporaryProfile;
        compositionSerialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void ConfigureCoordinator()
    {
        SerializedObject serialized = new SerializedObject(coordinator);
        RequireProperty(serialized, "enableMultiTableDeliveryRuns")
            .boolValue = true;
        RequireProperty(serialized, "maxDeliveryRunSize").intValue = 3;
        RequireProperty(serialized, "restrictRunsToSameResponsibleWaiter")
            .boolValue = true;
        RequireProperty(serialized, "deliveryRunConsolidationSeconds")
            .floatValue = 0.8f;
        RequireProperty(serialized, "logDeliveryRuns").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private void DrawObservedRun()
    {
        BistroBuilderDeliveryRun run = coordinator.FunctionalDiagnosticRun;

        if (run == null)
        {
            EditorGUILayout.HelpBox(
                "Aún no se ha creado la ronda objetivo.",
                MessageType.Info
            );
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Ronda observada", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("RunId", run.RunId.ToString());
        EditorGUILayout.LabelField("Estado", run.State.ToString());
        EditorGUILayout.LabelField("Platos", run.Items.Count.ToString());
        EditorGUILayout.LabelField("Mesas", run.Stops.Count.ToString());
        EditorGUILayout.LabelField(
            "Pendientes",
            run.RemainingLineCount.ToString()
        );

        bool correctShape =
            run.Items.Count == TargetLineCount &&
            run.Stops.Count == TargetTableCount;
        bool completed =
            run.State == BistroBuilderDeliveryRunState.Completed &&
            run.RemainingLineCount == 0;

        if (correctShape && completed)
        {
            EditorGUILayout.HelpBox(
                "PRUEBA FUNCIONAL 367G1 SUPERADA: una recogida, 3 platos, " +
                "2 mesas y ronda completada sin regresar a cocina.",
                MessageType.Info
            );
            status = "Prueba funcional 367G1 superada.";

            if (!completionLogged)
            {
                completionLogged = true;
                Debug.Log(
                    "BISTRO BUILDER — PRUEBA FUNCIONAL 367G1 SUPERADA. " +
                    "Ronda " + run.RunId + ": " + run.Items.Count +
                    " platos, " + run.Stops.Count +
                    " mesas, 0 líneas pendientes y estado Completed.",
                    coordinator
                );
            }
        }
        else if (!correctShape)
        {
            EditorGUILayout.HelpBox(
                "La ronda observada no coincide con el objetivo 3/2.",
                MessageType.Error
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "La ronda 3/2 está en ejecución.",
                MessageType.Warning
            );
        }
    }

    private static void DrawInstructions()
    {
        EditorGUILayout.LabelField(
            "Secuencia esperada en Console",
            EditorStyles.boldLabel
        );
        EditorGUILayout.SelectableLabel(
            "367G1 diagnóstico: objetivo de 3 plato(s) y 2 mesa(s) " +
            "alcanzado. Se libera el reparto.\n" +
            "Ronda 367G1 ...: 3 plato(s), 2 mesa(s).\n" +
            "Camarero ... recoge la ronda ... con 3 plato(s).\n" +
            "... sirve dos líneas en la primera mesa.\n" +
            "... sirve una línea en la segunda mesa.\n" +
            "Ronda 367G1 ... completada ...",
            GUILayout.MinHeight(120f)
        );
    }

    private void RefreshReferences()
    {
        coordinator =
            UnityEngine.Object.FindFirstObjectByType<WaiterTaskCoordinator>();
        serviceState =
            UnityEngine.Object.FindFirstObjectByType<
                RestaurantServiceStateService
            >();
    }

    private static int CompareWaiters(Waiter left, Waiter right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;
        return left.WaiterId.CompareTo(right.WaiterId);
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string name
    )
    {
        SerializedProperty property = serialized.FindProperty(name);

        if (property == null)
            throw new InvalidOperationException(
                "No existe la propiedad serializada " + name + "."
            );

        return property;
    }
}
