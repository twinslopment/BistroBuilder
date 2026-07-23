using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest aislado de estándares, generación de plazas y bloqueo de
/// una silla que excede la capacidad fija de una mesa.
///
/// Utiliza objetos HideAndDontSave y no modifica la escena.
/// </summary>
public static class
    BistroBuilderSeatingFoundationSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Seating/" +
        "Run Seating Foundation Self-Test";

    private const string StandardsAssetPath =
        "Assets/Data/Restaurant/Seating/" +
        "RestaurantSeatingStandards.asset";

    [MenuItem(MenuPath, false, 102)]
    private static void Run()
    {
        List<string> passed = new List<string>();
        List<string> failed = new List<string>();

        TestStandards(passed, failed);
        TestConfigurationGeneration(passed, failed);
        TestImmutableCapacityAndSpacing(passed, failed);
        TestFixedCapacityRule(passed, failed);
        TestInteractiveSnapping(passed, failed);
        TestOperationalClearance(passed, failed);

        StringBuilder report = new StringBuilder();

        report.AppendLine(
            "BISTRO BUILDER - AUTOTEST DE ASIENTOS"
        );

        report.AppendLine(
            "Pruebas superadas: " +
            passed.Count
        );

        report.AppendLine(
            "Pruebas fallidas: " +
            failed.Count
        );

        for (int index = 0;
             index < passed.Count;
             index++)
        {
            report.AppendLine(
                "- OK: " +
                passed[index]
            );
        }

        for (int index = 0;
             index < failed.Count;
             index++)
        {
            report.AppendLine(
                "- FALLO: " +
                failed[index]
            );
        }

        if (failed.Count > 0)
        {
            Debug.LogError(report.ToString());
        }
        else
        {
            Debug.Log(report.ToString());
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            "Pruebas superadas: " +
            passed.Count +
            "\nPruebas fallidas: " +
            failed.Count,
            "Aceptar"
        );
    }

    private static void TestStandards(
        List<string> passed,
        List<string> failed
    )
    {
        RestaurantSeatingStandardsDefinition standards =
            AssetDatabase.LoadAssetAtPath<
                RestaurantSeatingStandardsDefinition
            >(StandardsAssetPath);

        Assert(
            standards != null,
            "Existe RestaurantSeatingStandards.",
            passed,
            failed
        );

        if (standards == null)
        {
            return;
        }

        int[] rectangular =
        {
            2,
            4,
            6
        };

        for (int index = 0;
             index < rectangular.Length;
             index++)
        {
            Assert(
                standards.SupportsRectangularCapacity(
                    rectangular[index]
                ),
                "Capacidad rectangular " +
                rectangular[index] +
                " admitida.",
                passed,
                failed
            );
        }

        AssertRoundStandard(
            standards,
            4,
            1.00f,
            true,
            passed,
            failed
        );

        AssertRoundStandard(
            standards,
            6,
            1.20f,
            true,
            passed,
            failed
        );

        AssertRoundStandard(
            standards,
            8,
            1.50f,
            true,
            passed,
            failed
        );

        bool hasTen =
            standards.TryGetRoundTableStandard(
                10,
                out RestaurantRoundTableStandard ten
            );

        Assert(
            hasTen &&
            !ten.DiameterIsApproved,
            "La mesa redonda de 10 está soportada sin inventar " +
            "un diámetro todavía no aprobado.",
            passed,
            failed
        );
    }

    private static void TestConfigurationGeneration(
        List<string> passed,
        List<string> failed
    )
    {
        GameObject root =
            new GameObject(
                "BB_SeatingConfigurationSelfTest"
            );

        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);

        List<RestaurantTableSeatingConfigurationDefinition>
            definitions =
                new List<
                    RestaurantTableSeatingConfigurationDefinition
                >();

        try
        {
            TestRectangularConfiguration(
                root.transform,
                2,
                1,
                1,
                new Vector2(1.00f, 0.80f),
                definitions,
                passed,
                failed
            );

            TestRectangularConfiguration(
                root.transform,
                4,
                2,
                2,
                new Vector2(1.40f, 0.80f),
                definitions,
                passed,
                failed
            );

            TestRectangularConfiguration(
                root.transform,
                6,
                3,
                3,
                new Vector2(2.00f, 0.80f),
                definitions,
                passed,
                failed
            );

            TestNonUniformScaledConfiguration(
                root.transform,
                definitions,
                passed,
                failed
            );

            TestRoundConfiguration(
                root.transform,
                4,
                1.00f,
                definitions,
                passed,
                failed
            );

            TestRoundConfiguration(
                root.transform,
                6,
                1.20f,
                definitions,
                passed,
                failed
            );

            TestRoundConfiguration(
                root.transform,
                8,
                1.50f,
                definitions,
                passed,
                failed
            );
        }
        catch (Exception exception)
        {
            failed.Add(
                "La generación de plazas lanzó " +
                exception.GetType().Name +
                ": " +
                exception.Message
            );
        }
        finally
        {
            for (int index = 0;
                 index < definitions.Count;
                 index++)
            {
                if (definitions[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        definitions[index]
                    );
                }
            }

            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestRectangularConfiguration(
        Transform parent,
        int capacity,
        int positiveZ,
        int negativeZ,
        Vector2 footprintSize,
        List<RestaurantTableSeatingConfigurationDefinition>
            definitions,
        List<string> passed,
        List<string> failed
    )
    {
        RestaurantTableSeatingConfigurationDefinition definition =
            CreateRectangularDefinition(
                capacity,
                positiveZ,
                negativeZ,
                0,
                0
            );

        definitions.Add(definition);

        GameObject tableObject =
            CreateTemporaryObject(
                "RectangularTable_" + capacity,
                parent
            );

        RestaurantTable table =
            tableObject.AddComponent<RestaurantTable>();

        RestaurantPlacementFootprint footprint =
            tableObject.AddComponent<
                RestaurantPlacementFootprint
            >();

        RestaurantTableSeatingConfiguration configuration =
            tableObject.AddComponent<
                RestaurantTableSeatingConfiguration
            >();

        ConfigureTable(
            table,
            footprint,
            configuration,
            definition,
            capacity,
            footprintSize
        );

        List<RestaurantTableSeatSlot> slots =
            new List<RestaurantTableSeatSlot>(capacity);

        configuration.WriteCurrentSlots(slots);

        Assert(
            configuration.ValidateConfiguration(out _) &&
            slots.Count == capacity,
            "La mesa rectangular de " +
            capacity +
            " genera exactamente " +
            capacity +
            " plazas válidas.",
            passed,
            failed
        );
    }

    private static void TestNonUniformScaledConfiguration(
        Transform parent,
        List<RestaurantTableSeatingConfigurationDefinition>
            definitions,
        List<string> passed,
        List<string> failed
    )
    {
        RestaurantTableSeatingConfigurationDefinition definition =
            CreateRectangularDefinition(
                4,
                2,
                2,
                0,
                0
            );

        definitions.Add(definition);

        GameObject tableObject =
            CreateTemporaryObject(
                "ScaledRectangularTable_4",
                parent
            );

        tableObject.transform.localScale =
            new Vector3(2f, 1f, 1f);

        RestaurantTable table =
            tableObject.AddComponent<RestaurantTable>();

        RestaurantPlacementFootprint footprint =
            tableObject.AddComponent<
                RestaurantPlacementFootprint
            >();

        RestaurantTableSeatingConfiguration configuration =
            tableObject.AddComponent<
                RestaurantTableSeatingConfiguration
            >();

        ConfigureTable(
            table,
            footprint,
            configuration,
            definition,
            4,
            new Vector2(0.70f, 0.80f)
        );

        List<RestaurantTableSeatSlot> slots =
            new List<RestaurantTableSeatSlot>(4);

        configuration.WriteCurrentSlots(slots);

        configuration.ResolveWorldDimensionsAtPose(
            tableObject.transform.position,
            tableObject.transform.rotation,
            out float width,
            out float depth
        );

        Assert(
            configuration.ValidateConfiguration(out _) &&
            slots.Count == 4 &&
            Mathf.Approximately(width, 1.40f) &&
            Mathf.Approximately(depth, 0.80f),
            "La configuración conserva metros mundiales con " +
            "escala no uniforme.",
            passed,
            failed
        );
    }

    private static void TestRoundConfiguration(
        Transform parent,
        int capacity,
        float diameter,
        List<RestaurantTableSeatingConfigurationDefinition>
            definitions,
        List<string> passed,
        List<string> failed
    )
    {
        RestaurantTableSeatingConfigurationDefinition definition =
            CreateRoundDefinition(
                capacity,
                diameter
            );

        definitions.Add(definition);

        GameObject tableObject =
            CreateTemporaryObject(
                "RoundTable_" + capacity,
                parent
            );

        RestaurantTable table =
            tableObject.AddComponent<RestaurantTable>();

        RestaurantPlacementFootprint footprint =
            tableObject.AddComponent<
                RestaurantPlacementFootprint
            >();

        RestaurantTableSeatingConfiguration configuration =
            tableObject.AddComponent<
                RestaurantTableSeatingConfiguration
            >();

        ConfigureTable(
            table,
            footprint,
            configuration,
            definition,
            capacity,
            new Vector2(diameter, diameter)
        );

        List<RestaurantTableSeatSlot> slots =
            new List<RestaurantTableSeatSlot>(capacity);

        configuration.WriteCurrentSlots(slots);

        Assert(
            configuration.ValidateConfiguration(out _) &&
            slots.Count == capacity,
            "La mesa redonda de " +
            capacity +
            " y diámetro " +
            diameter.ToString("0.00") +
            " m genera exactamente " +
            capacity +
            " plazas válidas.",
            passed,
            failed
        );
    }

    private static void TestImmutableCapacityAndSpacing(
        List<string> passed,
        List<string> failed
    )
    {
        GameObject root =
            new GameObject(
                "BB_ImmutableCapacitySelfTest"
            );

        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);

        RestaurantTableSeatingConfigurationDefinition definition =
            null;

        try
        {
            definition =
                CreateRectangularDefinition(
                    4,
                    2,
                    2,
                    0,
                    0
                );

            GameObject tableObject =
                CreateTemporaryObject(
                    "ImmutableTable4",
                    root.transform
                );

            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();

            RestaurantPlacementFootprint footprint =
                tableObject.AddComponent<
                    RestaurantPlacementFootprint
                >();

            RestaurantTableSeatingConfiguration configuration =
                tableObject.AddComponent<
                    RestaurantTableSeatingConfiguration
                >();

            ConfigureTable(
                table,
                footprint,
                configuration,
                definition,
                5,
                new Vector2(1.40f, 0.80f)
            );

            bool mismatchedCapacityIsValid =
                configuration.ValidateConfiguration(
                    out string capacityError
                );

            Assert(
                !mismatchedCapacityIsValid &&
                capacityError.Contains(
                    "configuración fija admite 4"
                ),
                "Una mesa configurada para 4 no puede convertirse " +
                "en una mesa de 5.",
                passed,
                failed
            );

            ConfigureTable(
                table,
                footprint,
                configuration,
                definition,
                4,
                new Vector2(0.90f, 0.80f)
            );

            bool insufficientSpacingIsValid =
                configuration.ValidateConfiguration(
                    out string spacingError
                );

            Assert(
                !insufficientSpacingIsValid &&
                spacingError.Contains(
                    "m por cliente"
                ),
                "La capacidad se rechaza cuando no existe " +
                "separación suficiente por comensal.",
                passed,
                failed
            );
        }
        catch (Exception exception)
        {
            failed.Add(
                "El autotest de capacidad inmutable lanzó " +
                exception.GetType().Name +
                ": " +
                exception.Message
            );
        }
        finally
        {
            if (definition != null)
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }

            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestFixedCapacityRule(
        List<string> passed,
        List<string> failed
    )
    {
        GameObject testRoot =
            new GameObject(
                "BB_SeatingSelfTest_Root"
            );

        testRoot.hideFlags = HideFlags.HideAndDontSave;
        testRoot.SetActive(false);

        RestaurantSeatUseProfileDefinition profile = null;
        RestaurantTableSeatingConfigurationDefinition definition = null;

        try
        {
            RestaurantSeatRegistry seatRegistry =
                testRoot.AddComponent<RestaurantSeatRegistry>();

            RestaurantTableRegistry tableRegistry =
                testRoot.AddComponent<RestaurantTableRegistry>();

            RestaurantSeatingPlacementConstraintRule rule =
                testRoot.AddComponent<
                    RestaurantSeatingPlacementConstraintRule
                >();

            SerializedObject serializedTableRegistry =
                new SerializedObject(tableRegistry);

            serializedTableRegistry
                .FindProperty("discoverSceneTablesOnStart")
                .boolValue = false;

            serializedTableRegistry
                .ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedRule =
                new SerializedObject(rule);

            serializedRule
                .FindProperty("seatRegistry")
                .objectReferenceValue = seatRegistry;

            serializedRule
                .FindProperty("tableRegistry")
                .objectReferenceValue = tableRegistry;

            serializedRule.ApplyModifiedPropertiesWithoutUndo();

            profile =
                ScriptableObject.CreateInstance<
                    RestaurantSeatUseProfileDefinition
                >();

            definition =
                CreateRectangularDefinition(
                    4,
                    2,
                    2,
                    0,
                    0
                );

            GameObject tableObject =
                CreateTemporaryObject(
                    "TestTable4",
                    testRoot.transform
                );

            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();

            RestaurantPlacementFootprint tableFootprint =
                tableObject.AddComponent<
                    RestaurantPlacementFootprint
                >();

            RestaurantTableSeatingConfiguration configuration =
                tableObject.AddComponent<
                    RestaurantTableSeatingConfiguration
                >();

            ConfigureTable(
                table,
                tableFootprint,
                configuration,
                definition,
                4,
                new Vector2(1.40f, 0.80f)
            );

            Assert(
                configuration.ValidateConfiguration(
                    out _
                ),
                "La mesa de 4 valida su separación por comensal.",
                passed,
                failed
            );

            List<RestaurantTableSeatSlot> slots =
                new List<RestaurantTableSeatSlot>(4);

            configuration.WriteCurrentSlots(slots);

            Assert(
                slots.Count == 4,
                "La mesa de 4 genera exactamente 4 plazas.",
                passed,
                failed
            );

            tableRegistry.RegisterTable(table);

            List<RestaurantSeat> confirmedSeats =
                new List<RestaurantSeat>(4);

            for (int index = 0;
                 index < slots.Count;
                 index++)
            {
                RestaurantSeat seat =
                    CreateSeatAtSlot(
                        "ConfirmedSeat_" + index,
                        testRoot.transform,
                        profile,
                        slots[index]
                    );

                confirmedSeats.Add(seat);
                seatRegistry.RegisterSeat(seat);
            }

            RestaurantSeat candidate =
                CreateSeatAtSlot(
                    "CandidateFifthSeat",
                    testRoot.transform,
                    profile,
                    slots[0]
                );

            RestaurantAreaMember candidateMember =
                candidate.gameObject.AddComponent<
                    RestaurantAreaMember
                >();

            RestaurantPlacementFootprint candidateFootprint =
                candidate.gameObject.AddComponent<
                    RestaurantPlacementFootprint
                >();

            RestaurantPlacementConstraintContext fullContext =
                new RestaurantPlacementConstraintContext(
                    candidateMember,
                    candidate.transform.position,
                    candidate.transform.rotation,
                    null,
                    candidateFootprint,
                    null,
                    null
                );

            RestaurantPlacementConstraintEvaluation fullResult =
                rule.Evaluate(fullContext);

            Assert(
                !fullResult.IsValid &&
                string.Equals(
                    fullResult.RuleId,
                    "seating_table_capacity_exceeded",
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    fullResult.UserMessage,
                    "Esta mesa admite un máximo de 4 clientes.",
                    StringComparison.Ordinal
                ),
                "La quinta silla queda bloqueada con el mensaje " +
                "de capacidad máxima.",
                passed,
                failed
            );

            candidate.transform.SetPositionAndRotation(
                new Vector3(0.80f, 0f, 0f),
                Quaternion.LookRotation(
                    Vector3.left,
                    Vector3.up
                )
            );

            RestaurantPlacementConstraintContext perimeterContext =
                new RestaurantPlacementConstraintContext(
                    candidateMember,
                    candidate.transform.position,
                    candidate.transform.rotation,
                    null,
                    candidateFootprint,
                    null,
                    null
                );

            RestaurantPlacementConstraintEvaluation perimeterResult =
                rule.Evaluate(perimeterContext);

            Assert(
                !perimeterResult.IsValid &&
                string.Equals(
                    perimeterResult.RuleId,
                    "seating_table_capacity_exceeded",
                    StringComparison.Ordinal
                ),
                "Una quinta silla alrededor de un lado sin plazas " +
                "también queda bloqueada por capacidad máxima.",
                passed,
                failed
            );

            candidate.transform.SetPositionAndRotation(
                slots[0].AssociationPosition,
                Quaternion.LookRotation(
                    slots[0].FacingDirection,
                    Vector3.up
                )
            );

            seatRegistry.UnregisterSeat(
                confirmedSeats[0]
            );

            RestaurantPlacementConstraintContext occupiedContext =
                new RestaurantPlacementConstraintContext(
                    candidateMember,
                    slots[1].AssociationPosition,
                    Quaternion.LookRotation(
                        slots[1].FacingDirection,
                        Vector3.up
                    ),
                    null,
                    candidateFootprint,
                    null,
                    null
                );

            RestaurantPlacementConstraintEvaluation occupiedResult =
                rule.Evaluate(occupiedContext);

            Assert(
                !occupiedResult.IsValid &&
                string.Equals(
                    occupiedResult.RuleId,
                    "seating_slot_occupied",
                    StringComparison.Ordinal
                ) &&
                occupiedResult.ShouldOverrideGenericConflicts,
                "Una plaza ocupada prevalece sobre el mensaje " +
                "genérico de solapamiento.",
                passed,
                failed
            );

            RestaurantPlacementConstraintEvaluation freeResult =
                rule.Evaluate(fullContext);

            Assert(
                freeResult.IsValid,
                "La misma plaza es válida cuando vuelve a quedar libre.",
                passed,
                failed
            );
        }
        catch (Exception exception)
        {
            failed.Add(
                "El sandbox de capacidad lanzó " +
                exception.GetType().Name +
                ": " +
                exception.Message
            );
        }
        finally
        {
            if (profile != null)
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }

            if (definition != null)
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }

            UnityEngine.Object.DestroyImmediate(testRoot);
        }
    }

    /// <summary>
    /// Comprueba el flujo que faltaba en 365: captura desde una pose
    /// imperfecta, orientación automática, histéresis y liberación.
    /// </summary>
    private static void TestInteractiveSnapping(
        List<string> passed,
        List<string> failed
    )
    {
        GameObject root =
            new GameObject(
                "BB_InteractiveSeatingSnapSelfTest"
            );

        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);

        RestaurantSeatUseProfileDefinition profile = null;
        RestaurantTableSeatingConfigurationDefinition definition = null;

        try
        {
            RestaurantSeatRegistry seatRegistry =
                root.AddComponent<RestaurantSeatRegistry>();

            RestaurantTableRegistry tableRegistry =
                root.AddComponent<RestaurantTableRegistry>();

            RestaurantSeatingSnapProvider provider =
                root.AddComponent<RestaurantSeatingSnapProvider>();

            RestaurantPlacementSnapService snapService =
                root.AddComponent<RestaurantPlacementSnapService>();

            SerializedObject serializedTableRegistry =
                new SerializedObject(tableRegistry);

            serializedTableRegistry
                .FindProperty("discoverSceneTablesOnStart")
                .boolValue = false;

            serializedTableRegistry
                .ApplyModifiedPropertiesWithoutUndo();

            SerializedObject serializedProvider =
                new SerializedObject(provider);

            serializedProvider
                .FindProperty("tableRegistry")
                .objectReferenceValue = tableRegistry;

            serializedProvider
                .FindProperty("seatRegistry")
                .objectReferenceValue = seatRegistry;

            serializedProvider
                .FindProperty("captureRadius")
                .floatValue = 0.65f;

            serializedProvider
                .FindProperty("releaseRadius")
                .floatValue = 0.85f;

            serializedProvider.ApplyModifiedPropertiesWithoutUndo();

            snapService.RefreshProviders();

            profile =
                ScriptableObject.CreateInstance<
                    RestaurantSeatUseProfileDefinition
                >();

            definition =
                CreateRectangularDefinition(
                    2,
                    1,
                    1,
                    0,
                    0
                );

            GameObject tableObject =
                CreateTemporaryObject(
                    "SnapTestTable",
                    root.transform
                );

            RestaurantTable table =
                tableObject.AddComponent<RestaurantTable>();

            RestaurantPlacementFootprint footprint =
                tableObject.AddComponent<
                    RestaurantPlacementFootprint
                >();

            RestaurantTableSeatingConfiguration configuration =
                tableObject.AddComponent<
                    RestaurantTableSeatingConfiguration
                >();

            ConfigureTable(
                table,
                footprint,
                configuration,
                definition,
                2,
                new Vector2(1.00f, 0.80f)
            );

            tableRegistry.RegisterTable(table);

            List<RestaurantTableSeatSlot> slots =
                new List<RestaurantTableSeatSlot>(2);

            configuration.WriteCurrentSlots(slots);

            RestaurantSeat candidate =
                CreateSeatAtSlot(
                    "InteractiveSnapCandidate",
                    root.transform,
                    profile,
                    slots[0]
                );

            candidate.AssociationPoint.localPosition =
                new Vector3(0f, 0f, 0.20f);

            RestaurantAreaMember member =
                candidate.gameObject.AddComponent<
                    RestaurantAreaMember
                >();

            Quaternion wrongRotation =
                Quaternion.Euler(0f, 90f, 0f);

            Vector3 impreciseAssociationPosition =
                slots[0].AssociationPosition +
                Vector3.right * 0.45f;

            Vector3 impreciseRootPosition =
                candidate
                    .CalculateRootPositionForAssociationAtPose(
                        impreciseAssociationPosition,
                        wrongRotation
                    );

            snapService.BeginSession(member);

            bool snapped =
                snapService.TryResolveSnap(
                    member,
                    impreciseRootPosition,
                    wrongRotation,
                    out RestaurantPlacementSnapResult snapResult
                );

            Assert(
                snapped &&
                snapResult.IsSnapped,
                "Una silla se captura desde una posición imperfecta " +
                "del cursor.",
                passed,
                failed
            );

            Vector3 resolvedAssociationPosition =
                candidate.CalculateAssociationPositionAtPose(
                    snapResult.RootPosition,
                    snapResult.RootRotation
                );

            Assert(
                Vector3.Distance(
                    resolvedAssociationPosition,
                    slots[0].AssociationPosition
                ) <= 0.001f,
                "El snapping coloca AssociationPoint exactamente en " +
                "la plaza.",
                passed,
                failed
            );

            Assert(
                Vector3.Angle(
                    candidate.CalculateFacingDirectionAtPose(
                        snapResult.RootRotation
                    ),
                    slots[0].FacingDirection
                ) <= 0.1f,
                "El snapping orienta automáticamente la silla hacia " +
                "la mesa.",
                passed,
                failed
            );

            Vector3 hysteresisAssociationPosition =
                slots[0].AssociationPosition +
                Vector3.right * 0.75f;

            Vector3 hysteresisRootPosition =
                candidate
                    .CalculateRootPositionForAssociationAtPose(
                        hysteresisAssociationPosition,
                        snapResult.RootRotation
                    );

            bool retained =
                snapService.TryResolveSnap(
                    member,
                    hysteresisRootPosition,
                    snapResult.RootRotation,
                    out RestaurantPlacementSnapResult retainedResult
                );

            Assert(
                retained &&
                retainedResult.TargetKey == snapResult.TargetKey,
                "La histéresis mantiene estable la plaza capturada.",
                passed,
                failed
            );

            Vector3 releasedAssociationPosition =
                slots[0].AssociationPosition +
                Vector3.right * 0.95f;

            Vector3 releasedRootPosition =
                candidate
                    .CalculateRootPositionForAssociationAtPose(
                        releasedAssociationPosition,
                        snapResult.RootRotation
                    );

            bool stillSnapped =
                snapService.TryResolveSnap(
                    member,
                    releasedRootPosition,
                    snapResult.RootRotation,
                    out _
                );

            Assert(
                !stillSnapped,
                "La plaza se libera al superar el radio de salida.",
                passed,
                failed
            );

            RestaurantSeat occupiedSeat =
                CreateSeatAtSlot(
                    "OccupiedSnapSlot",
                    root.transform,
                    profile,
                    slots[0]
                );

            occupiedSeat.ApplyTopology(
                configuration,
                slots[0].SlotIndex,
                RestaurantSeatTopologyStatus.Associated,
                "Plaza ocupada para el autotest."
            );

            seatRegistry.RegisterSeat(occupiedSeat);

            snapService.EndSession();
            snapService.BeginSession(member);

            bool occupiedCaptured =
                snapService.TryResolveSnap(
                    member,
                    impreciseRootPosition,
                    wrongRotation,
                    out RestaurantPlacementSnapResult occupiedResult
                );

            Assert(
                occupiedCaptured &&
                occupiedResult.HintState ==
                    RestaurantPlacementSnapHintState.Occupied,
                "Una plaza ocupada sigue atrayendo la previsualización " +
                "para mostrar un rechazo preciso.",
                passed,
                failed
            );
        }
        catch (Exception exception)
        {
            failed.Add(
                "El autotest de snapping interactivo lanzó " +
                exception.GetType().Name +
                ": " +
                exception.Message
            );
        }
        finally
        {
            if (profile != null)
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }

            if (definition != null)
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }

            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void TestOperationalClearance(
        List<string> passed,
        List<string> failed
    )
    {
        GameObject root =
            new GameObject(
                "BB_OperationalClearanceSelfTest"
            );

        root.hideFlags = HideFlags.HideAndDontSave;
        root.SetActive(false);

        try
        {
            RestaurantAreaMemberRegistry memberRegistry =
                root.AddComponent<RestaurantAreaMemberRegistry>();

            RestaurantPlacementRegistry placementRegistry =
                root.AddComponent<RestaurantPlacementRegistry>();

            SerializedObject serializedPlacementRegistry =
                new SerializedObject(placementRegistry);

            serializedPlacementRegistry
                .FindProperty("memberRegistry")
                .objectReferenceValue = memberRegistry;

            serializedPlacementRegistry
                .FindProperty("discoverRegisteredMembersOnStart")
                .boolValue = false;

            serializedPlacementRegistry
                .ApplyModifiedPropertiesWithoutUndo();

            RestaurantOperationalClearanceConstraintRule rule =
                root.AddComponent<
                    RestaurantOperationalClearanceConstraintRule
                >();

            GameObject candidateObject =
                CreateTemporaryObject(
                    "ClearanceCandidate",
                    root.transform
                );

            RestaurantAreaMember candidateMember =
                candidateObject.AddComponent<RestaurantAreaMember>();

            RestaurantPlacementFootprint candidateFootprint =
                candidateObject.AddComponent<
                    RestaurantPlacementFootprint
                >();

            ConfigureFootprint(
                candidateFootprint,
                new Vector2(0.50f, 0.50f)
            );

            RestaurantOperationalClearanceSet clearanceSet =
                candidateObject.AddComponent<
                    RestaurantOperationalClearanceSet
                >();

            ConfigureClearanceSet(
                clearanceSet,
                new Vector3(0f, 0f, -0.65f),
                new Vector2(0.60f, 0.80f)
            );

            GameObject blockerObject =
                CreateTemporaryObject(
                    "ClearanceBlocker",
                    root.transform
                );

            blockerObject.transform.position =
                new Vector3(0f, 0f, -0.65f);

            RestaurantAreaMember blockerMember =
                blockerObject.AddComponent<RestaurantAreaMember>();

            RestaurantPlacementFootprint blockerFootprint =
                blockerObject.AddComponent<
                    RestaurantPlacementFootprint
                >();

            ConfigureFootprint(
                blockerFootprint,
                new Vector2(0.20f, 0.20f)
            );

            memberRegistry.RegisterMember(blockerMember);
            placementRegistry.RegisterFootprint(blockerFootprint);

            RestaurantPlacementConstraintContext blockedContext =
                new RestaurantPlacementConstraintContext(
                    candidateMember,
                    candidateObject.transform.position,
                    candidateObject.transform.rotation,
                    null,
                    candidateFootprint,
                    placementRegistry,
                    null
                );

            RestaurantPlacementConstraintEvaluation blockedResult =
                rule.Evaluate(blockedContext);

            Assert(
                !blockedResult.IsValid &&
                string.Equals(
                    blockedResult.RuleId,
                    "operational_clearance_candidate",
                    StringComparison.Ordinal
                ),
                "Un mueble dentro del recorrido operativo bloquea " +
                "la colocación.",
                passed,
                failed
            );

            blockerObject.transform.position =
                new Vector3(0f, 0f, -2f);

            RestaurantPlacementConstraintEvaluation clearResult =
                rule.Evaluate(blockedContext);

            Assert(
                clearResult.IsValid,
                "La colocación vuelve a ser válida al liberar el " +
                "recorrido operativo.",
                passed,
                failed
            );

            SerializedObject serializedCandidateClearance =
                new SerializedObject(clearanceSet);

            serializedCandidateClearance
                .FindProperty("requiresClearanceForOwner")
                .boolValue = false;

            serializedCandidateClearance
                .ApplyModifiedPropertiesWithoutUndo();

            RestaurantOperationalClearanceSet blockerClearance =
                blockerObject.AddComponent<
                    RestaurantOperationalClearanceSet
                >();

            ConfigureClearanceSet(
                blockerClearance,
                new Vector3(0f, 0f, 2f),
                new Vector2(0.80f, 0.80f)
            );

            RestaurantPlacementConstraintEvaluation reverseResult =
                rule.Evaluate(blockedContext);

            Assert(
                !reverseResult.IsValid &&
                string.Equals(
                    reverseResult.RuleId,
                    "operational_clearance_existing",
                    StringComparison.Ordinal
                ),
                "Un objeto tampoco puede invadir el espacio " +
                "operativo reservado por otro elemento.",
                passed,
                failed
            );
        }
        catch (Exception exception)
        {
            failed.Add(
                "El autotest de espacio operativo lanzó " +
                exception.GetType().Name +
                ": " +
                exception.Message
            );
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureFootprint(
        RestaurantPlacementFootprint footprint,
        Vector2 size
    )
    {
        SerializedObject serialized =
            new SerializedObject(footprint);

        serialized.FindProperty("localCenter").vector3Value =
            Vector3.zero;

        serialized.FindProperty("size").vector2Value =
            size;

        serialized.FindProperty("blocksOtherPlacements").boolValue =
            true;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureClearanceSet(
        RestaurantOperationalClearanceSet clearanceSet,
        Vector3 localCenter,
        Vector2 size
    )
    {
        SerializedObject serialized =
            new SerializedObject(clearanceSet);

        serialized.FindProperty("blocksOtherPlacements").boolValue =
            true;

        serialized.FindProperty("requiresClearanceForOwner").boolValue =
            true;

        SerializedProperty clearances =
            serialized.FindProperty("clearances");

        clearances.arraySize = 1;

        SerializedProperty clearance =
            clearances.GetArrayElementAtIndex(0);

        clearance.FindPropertyRelative("clearanceId").stringValue =
            "self_test_clearance";

        clearance.FindPropertyRelative("localCenter").vector3Value =
            localCenter;

        clearance.FindPropertyRelative("size").vector2Value =
            size;

        clearance.FindPropertyRelative("blockedUserMessage").stringValue =
            "El espacio operativo está bloqueado.";

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RestaurantTableSeatingConfigurationDefinition
        CreateRectangularDefinition(
            int capacity,
            int positiveZ,
            int negativeZ,
            int positiveX,
            int negativeX
        )
    {
        RestaurantTableSeatingConfigurationDefinition definition =
            ScriptableObject.CreateInstance<
                RestaurantTableSeatingConfigurationDefinition
            >();

        SerializedObject serialized =
            new SerializedObject(definition);

        serialized.FindProperty("configurationId").stringValue =
            "self_test_table_" +
            capacity;

        serialized.FindProperty("displayName").stringValue =
            "Mesa de prueba de " +
            capacity;

        serialized.FindProperty("maximumCustomers").intValue =
            capacity;

        serialized.FindProperty("shape").enumValueIndex =
            (int)RestaurantTableSeatingShape.Rectangular;

        serialized
            .FindProperty("usePlacementFootprintDimensions")
            .boolValue = true;

        serialized.FindProperty("positiveZSeats").intValue =
            positiveZ;

        serialized.FindProperty("negativeZSeats").intValue =
            negativeZ;

        serialized.FindProperty("positiveXSeats").intValue =
            positiveX;

        serialized.FindProperty("negativeXSeats").intValue =
            negativeX;

        serialized.FindProperty("sideEndInset").floatValue =
            0.10f;

        serialized
            .FindProperty("minimumSpacePerCustomer")
            .floatValue = 0.55f;

        serialized
            .FindProperty("parkedGapFromTableEdge")
            .floatValue = 0.10f;

        serialized.ApplyModifiedPropertiesWithoutUndo();

        return definition;
    }

    private static RestaurantTableSeatingConfigurationDefinition
        CreateRoundDefinition(
            int capacity,
            float diameter
        )
    {
        RestaurantTableSeatingConfigurationDefinition definition =
            ScriptableObject.CreateInstance<
                RestaurantTableSeatingConfigurationDefinition
            >();

        SerializedObject serialized =
            new SerializedObject(definition);

        serialized.FindProperty("configurationId").stringValue =
            "self_test_round_" +
            capacity;

        serialized.FindProperty("displayName").stringValue =
            "Mesa redonda de prueba de " +
            capacity;

        serialized.FindProperty("maximumCustomers").intValue =
            capacity;

        serialized.FindProperty("shape").enumValueIndex =
            (int)RestaurantTableSeatingShape.Round;

        serialized
            .FindProperty("usePlacementFootprintDimensions")
            .boolValue = true;

        serialized.FindProperty("manualRoundDiameter").floatValue =
            diameter;

        serialized
            .FindProperty("minimumSpacePerCustomer")
            .floatValue = 0.55f;

        serialized
            .FindProperty("parkedGapFromTableEdge")
            .floatValue = 0.10f;

        serialized.ApplyModifiedPropertiesWithoutUndo();

        return definition;
    }

    private static void ConfigureTable(
        RestaurantTable table,
        RestaurantPlacementFootprint footprint,
        RestaurantTableSeatingConfiguration configuration,
        RestaurantTableSeatingConfigurationDefinition definition,
        int capacity,
        Vector2 footprintSize
    )
    {
        SerializedObject serializedTable =
            new SerializedObject(table);

        serializedTable.FindProperty("tableId").intValue = 9001;
        serializedTable.FindProperty("capacity").intValue = capacity;
        serializedTable.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedFootprint =
            new SerializedObject(footprint);

        serializedFootprint.FindProperty("localCenter").vector3Value =
            Vector3.zero;

        serializedFootprint.FindProperty("size").vector2Value =
            footprintSize;

        serializedFootprint.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedConfiguration =
            new SerializedObject(configuration);

        serializedConfiguration.FindProperty("table")
            .objectReferenceValue = table;

        serializedConfiguration.FindProperty("placementFootprint")
            .objectReferenceValue = footprint;

        serializedConfiguration.FindProperty("definition")
            .objectReferenceValue = definition;

        serializedConfiguration.ApplyModifiedPropertiesWithoutUndo();
    }

    private static RestaurantSeat CreateSeatAtSlot(
        string objectName,
        Transform parent,
        RestaurantSeatUseProfileDefinition profile,
        RestaurantTableSeatSlot slot
    )
    {
        GameObject seatObject =
            CreateTemporaryObject(
                objectName,
                parent
            );

        seatObject.transform.SetPositionAndRotation(
            slot.AssociationPosition,
            Quaternion.LookRotation(
                slot.FacingDirection,
                Vector3.up
            )
        );

        RestaurantSeat seat =
            seatObject.AddComponent<RestaurantSeat>();

        Transform associationPoint =
            CreateTemporaryChild(
                seatObject.transform,
                "AssociationPoint"
            );

        Transform motionRoot =
            CreateTemporaryChild(
                seatObject.transform,
                "OperationalMotionRoot"
            );

        Transform seatPoint =
            CreateTemporaryChild(
                motionRoot,
                "SeatPoint"
            );

        Transform approachPoint =
            CreateTemporaryChild(
                seatObject.transform,
                "CustomerApproachPoint"
            );

        SerializedObject serializedSeat =
            new SerializedObject(seat);

        serializedSeat.FindProperty("useProfile")
            .objectReferenceValue = profile;

        serializedSeat.FindProperty("facingAxis").enumValueIndex =
            (int)RestaurantSeatFacingAxis.PositiveZ;

        serializedSeat.FindProperty("associationPoint")
            .objectReferenceValue = associationPoint;

        serializedSeat.FindProperty("operationalMotionRoot")
            .objectReferenceValue = motionRoot;

        serializedSeat.FindProperty("seatPoint")
            .objectReferenceValue = seatPoint;

        serializedSeat.FindProperty("customerApproachPoint")
            .objectReferenceValue = approachPoint;

        serializedSeat.ApplyModifiedPropertiesWithoutUndo();

        return seat;
    }

    private static GameObject CreateTemporaryObject(
        string objectName,
        Transform parent
    )
    {
        GameObject created = new GameObject(objectName);
        created.hideFlags = HideFlags.HideAndDontSave;
        created.transform.SetParent(parent, false);
        return created;
    }

    private static Transform CreateTemporaryChild(
        Transform parent,
        string childName
    )
    {
        GameObject child =
            CreateTemporaryObject(
                childName,
                parent
            );

        return child.transform;
    }

    private static void AssertRoundStandard(
        RestaurantSeatingStandardsDefinition standards,
        int capacity,
        float expectedDiameter,
        bool expectedApproved,
        List<string> passed,
        List<string> failed
    )
    {
        bool found =
            standards.TryGetRoundTableStandard(
                capacity,
                out RestaurantRoundTableStandard standard
            );

        Assert(
            found &&
            standard.DiameterIsApproved == expectedApproved &&
            Mathf.Approximately(
                standard.DiameterMetres,
                expectedDiameter
            ),
            "Mesa redonda de " +
            capacity +
            " = " +
            expectedDiameter.ToString("0.00") +
            " m.",
            passed,
            failed
        );
    }

    private static void Assert(
        bool condition,
        string message,
        List<string> passed,
        List<string> failed
    )
    {
        if (condition)
        {
            passed.Add(message);
        }
        else
        {
            failed.Add(message);
        }
    }
}
