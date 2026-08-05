using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest determinista y aislado de 2.1D.
/// No modifica la escena ni assets persistentes.
/// </summary>
public static class BistroBuilderSignatureDish21DSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Menu/Run 2.1D Signature Dishes Self-Test";

    private sealed class Report
    {
        public int Passed;
        public int Failed;
        public readonly List<string> Lines = new List<string>();

        public void Expect(bool condition, string message)
        {
            if (condition)
            {
                Passed++;
                Lines.Add("- OK: " + message);
            }
            else
            {
                Failed++;
                Lines.Add("- FALLO: " + message);
            }
        }

        public string Build()
        {
            return "BISTRO BUILDER - AUTOTEST 2.1D PLATOS FIRMA\n" +
                   "Pruebas superadas: " + Passed + "\n" +
                   "Pruebas fallidas: " + Failed + "\n" +
                   string.Join("\n", Lines);
        }
    }

    private sealed class FixedRandom :
        IBistroBuilderMenuSelectionRandomSource
    {
        private readonly ulong value;

        public FixedRandom(ulong value)
        {
            this.value = value;
        }

        public ulong NextUInt64()
        {
            return value;
        }
    }

    private sealed class FixedDishResolver :
        IBistroBuilderOrderDishResolver
    {
        private readonly BistroBuilderResolvedOrderDish dish;

        public FixedDishResolver(BistroBuilderResolvedOrderDish dish)
        {
            this.dish = dish;
        }

        public bool TryResolveOrderableDish(
            string dishId,
            BistroBuilderMealServiceAvailability mealService,
            out BistroBuilderResolvedOrderDish resolved,
            out string rejectionReason
        )
        {
            if (!string.Equals(
                    BistroBuilderOrderIdUtility.Normalize(dishId),
                    dish.DishId,
                    StringComparison.Ordinal
                ))
            {
                resolved = default(BistroBuilderResolvedOrderDish);
                rejectionReason = "DishId inesperado.";
                return false;
            }

            resolved = dish;
            rejectionReason = string.Empty;
            return true;
        }
    }

    [MenuItem(MenuPath, false, 162)]
    private static void Run()
    {
        Report report = new Report();
        BistroBuilderMenuCommercialPolicy policy =
            ScriptableObject.CreateInstance<
                BistroBuilderMenuCommercialPolicy
            >();
        ConfigurePolicy(policy, 3, 15000);

        try
        {
            RunContextTests(report);
            RunEvaluatorTests(report, policy);
            RunDistributionTests(report, policy);
            RunOrderSnapshotTests(report);
            RunTelemetryTests(report);
        }
        catch (Exception exception)
        {
            report.Expect(false, "Excepción no controlada: " + exception);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(policy);
        }

        string text = report.Build();

        if (report.Failed > 0)
        {
            Debug.LogError(text);
        }
        else
        {
            Debug.Log(text);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            "BISTRO BUILDER - AUTOTEST 2.1D PLATOS FIRMA\n" +
            "Pruebas superadas: " + report.Passed + "\n" +
            "Pruebas fallidas: " + report.Failed +
            "\nSelección ponderada, determinismo, snapshot histórico y " +
            "telemetría validados.",
            "Aceptar"
        );
    }

    private static void RunContextTests(Report report)
    {
        BistroBuilderMenuSelectionContext valid = CreateContext(0, 0);
        report.Expect(valid.TryValidate(out _), "El contexto válido se acepta.");

        BistroBuilderMenuSelectionContext invalidMeal =
            new BistroBuilderMenuSelectionContext(
                BistroBuilderMealServiceAvailability.All,
                BistroBuilderServiceMode.TableService,
                "customer_21d",
                1,
                0,
                0
            );
        report.Expect(
            !invalidMeal.TryValidate(out _),
            "Una franja no concreta se rechaza."
        );

        BistroBuilderMenuSelectionContext invalidMode =
            new BistroBuilderMenuSelectionContext(
                BistroBuilderMealServiceAvailability.Lunch,
                (BistroBuilderServiceMode)99,
                "customer_21d",
                1,
                0,
                0
            );
        report.Expect(
            !invalidMode.TryValidate(out _),
            "Una modalidad desconocida se rechaza."
        );

        BistroBuilderMenuSelectionContext invalidReference =
            new BistroBuilderMenuSelectionContext(
                BistroBuilderMealServiceAvailability.Lunch,
                BistroBuilderServiceMode.TableService,
                "?",
                1,
                0,
                0
            );
        report.Expect(
            !invalidReference.TryValidate(out _),
            "Una referencia inestable se rechaza."
        );

        BistroBuilderMenuSelectionContext invalidCourse =
            new BistroBuilderMenuSelectionContext(
                BistroBuilderMealServiceAvailability.Lunch,
                BistroBuilderServiceMode.TableService,
                "customer_21d",
                99,
                0,
                0
            );
        report.Expect(
            !invalidCourse.TryValidate(out _),
            "Un pase fuera de rango se rechaza."
        );

        report.Expect(
            valid.WithOrdinal(4, 2).SelectionOrdinal == 4 &&
            valid.WithOrdinal(4, 2).FallbackDisplayOffset == 2,
            "WithOrdinal conserva el contexto y cambia el ordinal."
        );
    }

    private static void RunEvaluatorTests(
        Report report,
        BistroBuilderMenuCommercialPolicy policy
    )
    {
        List<BistroBuilderMenuOfferItemSnapshot> plain =
            CreateCandidates(false, false, false, false);
        BistroBuilderMenuSelectionContext context = CreateContext(0, 0);

        bool selected = BistroBuilderMenuSelectionEvaluator.TrySelect(
            plain,
            policy,
            context,
            null,
            null,
            out BistroBuilderMenuSelectionResult first,
            out _,
            out _
        );
        report.Expect(selected, "Una oferta ordinaria puede seleccionarse.");
        report.Expect(first.DishId == "dish_21d_0", "El fallback conserva el primer plato.");
        report.Expect(!first.UsedWeightedSelection, "Pesos iguales no activan azar ponderado.");
        report.Expect(first.CandidateCount == 4, "No se duplican candidatos para ponderar.");
        report.Expect(first.TotalWeightBasisPoints == 40000L, "La suma base es exacta.");

        BistroBuilderMenuSelectionEvaluator.TrySelect(
            plain,
            policy,
            CreateContext(1, 1),
            null,
            null,
            out BistroBuilderMenuSelectionResult second,
            out _,
            out _
        );
        report.Expect(second.DishId == "dish_21d_1", "El offset histórico 1 se conserva.");

        BistroBuilderMenuSelectionEvaluator.TrySelect(
            plain,
            policy,
            CreateContext(2, 3),
            null,
            null,
            out BistroBuilderMenuSelectionResult fourth,
            out _,
            out _
        );
        report.Expect(fourth.DishId == "dish_21d_3", "El offset histórico 3 se conserva.");

        List<BistroBuilderMenuOfferItemSnapshot> allSignature =
            CreateCandidates(true, true, true, true);
        BistroBuilderMenuSelectionEvaluator.TrySelect(
            allSignature,
            policy,
            CreateContext(0, 2),
            null,
            null,
            out BistroBuilderMenuSelectionResult equalSignature,
            out _,
            out _
        );
        report.Expect(
            equalSignature.DishId == "dish_21d_2" &&
            !equalSignature.UsedWeightedSelection,
            "Todos firma conservan el orden porque sus pesos son iguales."
        );

        List<BistroBuilderMenuOfferItemSnapshot> weighted =
            CreateCandidates(true, false, false, false);
        selected = BistroBuilderMenuSelectionEvaluator.TrySelect(
            weighted,
            policy,
            context,
            null,
            new FixedRandom(0UL),
            out BistroBuilderMenuSelectionResult weightedFirst,
            out _,
            out _
        );
        report.Expect(selected, "La selección ponderada acepta una fuente inyectada.");
        report.Expect(weightedFirst.DishId == "dish_21d_0", "El extremo inferior elige el firma inicial.");
        report.Expect(weightedFirst.UsedWeightedSelection, "Pesos distintos activan ponderación.");
        report.Expect(weightedFirst.UsedInjectedRandomSource, "Se registra la fuente inyectada.");
        report.Expect(weightedFirst.EffectiveWeightBasisPoints == 15000L, "El peso firma se aplica exactamente.");
        report.Expect(weightedFirst.TotalWeightBasisPoints == 45000L, "La suma ponderada es exacta.");

        BistroBuilderMenuSelectionEvaluator.TrySelect(
            weighted,
            policy,
            context,
            null,
            new FixedRandom(15000UL),
            out BistroBuilderMenuSelectionResult weightedSecond,
            out _,
            out _
        );
        report.Expect(weightedSecond.DishId == "dish_21d_1", "El siguiente intervalo elige el primer no firma.");

        HashSet<string> excluded = new HashSet<string>(StringComparer.Ordinal)
        {
            "dish_21d_0"
        };
        BistroBuilderMenuSelectionEvaluator.TrySelect(
            weighted,
            policy,
            context,
            excluded,
            null,
            out BistroBuilderMenuSelectionResult excludedResult,
            out _,
            out _
        );
        report.Expect(excludedResult.DishId != "dish_21d_0", "Las exclusiones se respetan.");
        report.Expect(!excludedResult.UsedWeightedSelection, "Al excluir el firma vuelven pesos iguales.");

        List<BistroBuilderMenuOfferItemSnapshot> duplicate =
            CreateCandidates(false, false, false, false);
        duplicate.Add(duplicate[0]);
        report.Expect(
            !BistroBuilderMenuSelectionEvaluator.TrySelect(
                duplicate,
                policy,
                context,
                null,
                null,
                out _,
                out BistroBuilderMenuSelectionFailureReason duplicateReason,
                out _
            ) &&
            duplicateReason ==
                BistroBuilderMenuSelectionFailureReason.DuplicateDishId,
            "Un DishId duplicado se rechaza en lugar de aumentar su peso."
        );

        List<BistroBuilderMenuOfferItemSnapshot> unavailable =
            new List<BistroBuilderMenuOfferItemSnapshot>
            {
                CreateCandidate(0, false, false),
                CreateCandidate(1, false, true)
            };
        BistroBuilderMenuSelectionEvaluator.TrySelect(
            unavailable,
            policy,
            context,
            null,
            null,
            out BistroBuilderMenuSelectionResult onlyAvailable,
            out _,
            out _
        );
        report.Expect(onlyAvailable.DishId == "dish_21d_0", "Los no pedibles no entran en el sorteo.");

        List<BistroBuilderMenuOfferItemSnapshot> mismatched =
            CreateCandidates(false, false, false, false);
        mismatched[1] = CreateCandidate(
            1,
            false,
            false,
            BistroBuilderServiceMode.BarService
        );
        report.Expect(
            !BistroBuilderMenuSelectionEvaluator.TrySelect(
                mismatched,
                policy,
                context,
                null,
                null,
                out _,
                out BistroBuilderMenuSelectionFailureReason mismatchReason,
                out _
            ) && mismatchReason ==
                BistroBuilderMenuSelectionFailureReason.InvalidCandidates,
            "Una mezcla de modalidades se rechaza."
        );

        List<BistroBuilderMenuOfferItemSnapshot> mixedRestaurant =
            CreateCandidates(false, false, false, false);
        mixedRestaurant[2] = CreateCandidate(
            2,
            false,
            false,
            BistroBuilderServiceMode.TableService,
            "restaurant_other",
            1
        );
        report.Expect(
            !BistroBuilderMenuSelectionEvaluator.TrySelect(
                mixedRestaurant,
                policy,
                context,
                null,
                null,
                out _,
                out BistroBuilderMenuSelectionFailureReason restaurantReason,
                out _
            ) && restaurantReason ==
                BistroBuilderMenuSelectionFailureReason.InvalidCandidates,
            "Una mezcla de restaurantes se rechaza."
        );

        List<BistroBuilderMenuOfferItemSnapshot> mixedRevision =
            CreateCandidates(false, false, false, false);
        mixedRevision[2] = CreateCandidate(
            2,
            false,
            false,
            BistroBuilderServiceMode.TableService,
            "restaurant_main",
            2
        );
        report.Expect(
            !BistroBuilderMenuSelectionEvaluator.TrySelect(
                mixedRevision,
                policy,
                context,
                null,
                null,
                out _,
                out BistroBuilderMenuSelectionFailureReason revisionReason,
                out _
            ) && revisionReason ==
                BistroBuilderMenuSelectionFailureReason.InvalidCandidates,
            "Una mezcla de revisiones de oferta se rechaza."
        );

        List<BistroBuilderMenuOfferItemSnapshot> oversized =
            new List<BistroBuilderMenuOfferItemSnapshot>(
                policy.MaximumMenuItems + 1
            );

        for (int index = 0; index <= policy.MaximumMenuItems; index++)
        {
            oversized.Add(
                CreateCandidateWithDishId(
                    "dish_oversized_" + index,
                    index
                )
            );
        }

        report.Expect(
            !BistroBuilderMenuSelectionEvaluator.TrySelect(
                oversized,
                policy,
                context,
                null,
                null,
                out _,
                out BistroBuilderMenuSelectionFailureReason capacityReason,
                out _
            ) && capacityReason ==
                BistroBuilderMenuSelectionFailureReason.InvalidCandidates,
            "La selección respeta la capacidad máxima de la carta."
        );

        report.Expect(
            !BistroBuilderMenuSelectionEvaluator.TrySelect(
                null,
                policy,
                context,
                null,
                null,
                out _,
                out BistroBuilderMenuSelectionFailureReason nullReason,
                out _
            ) && nullReason ==
                BistroBuilderMenuSelectionFailureReason.InvalidCandidates,
            "Una colección nula se rechaza."
        );

        report.Expect(
            !BistroBuilderMenuSelectionEvaluator.TrySelect(
                new List<BistroBuilderMenuOfferItemSnapshot>(),
                policy,
                context,
                null,
                null,
                out _,
                out BistroBuilderMenuSelectionFailureReason emptyReason,
                out _
            ) && emptyReason ==
                BistroBuilderMenuSelectionFailureReason.NoOrderableCandidates,
            "Una colección vacía se rechaza."
        );

        BistroBuilderMenuSelectionEvaluator.TrySelect(
            weighted,
            policy,
            CreateContext(17, 0),
            null,
            null,
            out BistroBuilderMenuSelectionResult deterministicA,
            out _,
            out _
        );
        BistroBuilderMenuSelectionEvaluator.TrySelect(
            weighted,
            policy,
            CreateContext(17, 0),
            null,
            null,
            out BistroBuilderMenuSelectionResult deterministicB,
            out _,
            out _
        );
        report.Expect(
            deterministicA.DishId == deterministicB.DishId &&
            deterministicA.DeterministicSeed ==
                deterministicB.DeterministicSeed,
            "El mismo contexto y oferta producen la misma decisión."
        );

        report.Expect(
            deterministicA.DeterministicSeed != 0UL,
            "La semilla estable nunca se publica como no inicializada."
        );
    }

    private static void RunDistributionTests(
        Report report,
        BistroBuilderMenuCommercialPolicy policy
    )
    {
        List<BistroBuilderMenuOfferItemSnapshot> weighted =
            CreateCandidates(true, false, false, false);
        int signatureSelections = 0;
        int[] counts = new int[4];
        const int samples = 4096;

        for (int index = 0; index < samples; index++)
        {
            BistroBuilderMenuSelectionEvaluator.TrySelect(
                weighted,
                policy,
                CreateContext(index, 0),
                null,
                null,
                out BistroBuilderMenuSelectionResult result,
                out _,
                out _
            );

            int dishIndex = ParseDishIndex(result.DishId);
            counts[dishIndex]++;

            if (result.WasSignatureDishAtSelection)
            {
                signatureSelections++;
            }
        }

        int averageNonSignature =
            (counts[1] + counts[2] + counts[3]) / 3;
        report.Expect(signatureSelections > averageNonSignature, "El firma se elige más que cada plato ordinario medio.");
        report.Expect(signatureSelections > samples / 4, "El firma supera la cuota de pesos iguales.");
        report.Expect(counts[1] > 0 && counts[2] > 0 && counts[3] > 0, "La ponderación no elimina candidatos ordinarios.");
        report.Expect(signatureSelections < samples, "El peso firma no fuerza una elección absoluta.");

        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        for (int ordinal = 0; ordinal < 4; ordinal++)
        {
            BistroBuilderMenuSelectionEvaluator.TrySelect(
                weighted,
                policy,
                CreateContext(ordinal, 0),
                unique,
                null,
                out BistroBuilderMenuSelectionResult result,
                out _,
                out _
            );
            unique.Add(result.DishId);
        }
        report.Expect(unique.Count == 4, "La selección sin reemplazo puede cubrir cuatro platos distintos.");
    }

    private static void RunOrderSnapshotTests(Report report)
    {
        BistroBuilderResolvedOrderDish legacy =
            new BistroBuilderResolvedOrderDish("dish_legacy", 1200, 0);
        report.Expect(!legacy.SignatureDish, "El constructor legacy no inventa plato firma.");
        report.Expect(string.IsNullOrEmpty(legacy.RestaurantId), "El constructor legacy admite RestaurantId vacío.");
        report.Expect(legacy.MenuOfferRevision == 0, "El constructor legacy usa revisión cero compatible.");

        BistroBuilderResolvedOrderDish signature =
            new BistroBuilderResolvedOrderDish(
                "dish_signature",
                2500,
                2,
                true,
                "restaurant_main",
                17
            );
        report.Expect(signature.SignatureDish, "El resolvedor puede congelar plato firma.");
        report.Expect(signature.RestaurantId == "restaurant_main", "El RestaurantId histórico se normaliza.");
        report.Expect(signature.MenuOfferRevision == 17, "La revisión de oferta se conserva.");

        BistroBuilderCanonicalOrderCreationRequest request =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                tableReferenceId = "table_21d",
                customerGroupReferenceId = "group_21d",
                mealService = BistroBuilderMealServiceAvailability.Lunch,
                serviceMode = BistroBuilderServiceMode.TableService
            };
        request.lines.Add(
            new BistroBuilderCanonicalOrderLineRequest(
                "dish_signature",
                "customer_21d",
                new[] { "customer_21d" },
                1
            )
        );

        bool created = BistroBuilderCanonicalOrderFactory.TryCreate(
            request,
            new FixedDishResolver(signature),
            1L,
            out BistroBuilderCanonicalOrder order,
            out BistroBuilderCanonicalOrderOperationResult operation
        );
        report.Expect(created && operation.Succeeded, "La fábrica acepta el snapshot comercial 2.1D.");
        BistroBuilderCanonicalOrderLine line = order.Lines[0];
        report.Expect(line.WasSignatureDishAtOrder, "La línea recuerda que era plato firma.");
        report.Expect(line.RestaurantIdAtOrder == "restaurant_main", "La línea recuerda el restaurante.");
        report.Expect(line.MenuOfferRevisionAtOrder == 17, "La línea recuerda la revisión de oferta.");
        report.Expect(line.PriceCentsAtOrder == 2500, "El precio histórico sigue congelado.");
        report.Expect(line.TryValidate(out _), "La línea 2.1D es válida.");

        BistroBuilderCanonicalOrder clone = order.Clone();
        BistroBuilderCanonicalOrderLine clonedLine = clone.Lines[0];
        report.Expect(clonedLine.WasSignatureDishAtOrder, "El clon conserva plato firma.");
        report.Expect(clonedLine.RestaurantIdAtOrder == "restaurant_main", "El clon conserva restaurante.");
        report.Expect(clonedLine.MenuOfferRevisionAtOrder == 17, "El clon conserva revisión.");

        BistroBuilderResolvedOrderDish changedAfterOrder =
            new BistroBuilderResolvedOrderDish(
                "dish_signature",
                9999,
                2,
                false,
                "restaurant_other",
                99
            );
        report.Expect(
            changedAfterOrder.SignatureDish != line.WasSignatureDishAtOrder &&
            line.PriceCentsAtOrder == 2500,
            "Cambios posteriores no reescriben la línea existente."
        );
    }

    private static void RunTelemetryTests(Report report)
    {
        BistroBuilderResolvedOrderDish signature =
            new BistroBuilderResolvedOrderDish(
                "dish_signature",
                2500,
                0,
                true,
                "restaurant_main",
                4
            );
        BistroBuilderCanonicalOrder order = CreateOrder(signature);
        GameObject owner = new GameObject("BB_21D_Telemetry_Test");
        owner.hideFlags = HideFlags.HideAndDontSave;
        BistroBuilderSignatureDishTelemetryService telemetry =
            owner.AddComponent<BistroBuilderSignatureDishTelemetryService>();
        BistroBuilderSignatureDishTelemetryChangeType lastChangeType =
            BistroBuilderSignatureDishTelemetryChangeType.Reset;
        string lastSubjectId = string.Empty;
        telemetry.TelemetryChanged += change =>
        {
            lastChangeType = change.ChangeType;
            lastSubjectId = change.SubjectId;
        };

        try
        {
            report.Expect(
                telemetry.TryObserveOrderSnapshot(order, out _),
                "Telemetría puede observar una comanda válida."
            );
            report.Expect(
                lastChangeType ==
                    BistroBuilderSignatureDishTelemetryChangeType.OrderObserved &&
                lastSubjectId == order.Lines[0].LineId,
                "El evento de pedido publica tipo y LineId correctos."
            );
            report.Expect(telemetry.TotalOrderedLines == 1L, "Cuenta una línea pedida.");
            report.Expect(telemetry.SignatureOrderedLines == 1L, "Cuenta una línea firma pedida.");
            telemetry.TryObserveOrderSnapshot(order, out _);
            report.Expect(telemetry.TotalOrderedLines == 1L, "Observar dos veces no duplica pedidos.");

            report.Expect(
                TryAdvanceOrderToConsumed(order, out string transitionError),
                string.IsNullOrWhiteSpace(transitionError)
                    ? "La comanda alcanza Consumed por transiciones válidas."
                    : transitionError
            );
            telemetry.TryObserveOrderSnapshot(order, out _);
            report.Expect(
                lastChangeType ==
                    BistroBuilderSignatureDishTelemetryChangeType
                        .ConsumptionObserved &&
                lastSubjectId == order.Lines[0].LineId,
                "El evento de consumo no se confunde con un nuevo pedido."
            );
            report.Expect(telemetry.TotalConsumedLines == 1L, "Cuenta una línea consumida.");
            report.Expect(telemetry.SignatureConsumedLines == 1L, "Cuenta una venta firma consumida.");
            telemetry.TryObserveOrderSnapshot(order, out _);
            report.Expect(telemetry.TotalConsumedLines == 1L, "Observar dos veces no duplica consumos.");

            report.Expect(
                telemetry.TryCaptureRuntimeSnapshot(
                    out BistroBuilderSignatureDishTelemetrySnapshot snapshot,
                    out _
                ),
                "La telemetría captura un snapshot válido."
            );
            report.Expect(snapshot.SchemaVersion == 1, "El snapshot usa versión conocida.");
            report.Expect(snapshot.SignatureOrderedLines == 1L, "El snapshot conserva pedidos firma.");
            report.Expect(snapshot.SignatureConsumedLines == 1L, "El snapshot conserva consumos firma.");

            BistroBuilderSignatureDishTelemetrySnapshot withSelections =
                new BistroBuilderSignatureDishTelemetrySnapshot(
                    10L,
                    4L,
                    snapshot.TotalOrderedLines,
                    snapshot.SignatureOrderedLines,
                    snapshot.TotalConsumedLines,
                    snapshot.SignatureConsumedLines,
                    new List<string>(snapshot.ObservedOrderLineIds),
                    new List<string>(snapshot.ObservedConsumedLineIds)
                );
            report.Expect(withSelections.TryValidate(out _), "Un snapshot coherente se valida.");
            telemetry.ResetMetrics(false);
            report.Expect(telemetry.TotalOrderedLines == 0L, "Reset elimina contadores runtime.");
            report.Expect(
                telemetry.TryReplaceFromRuntimeSnapshot(
                    withSelections,
                    false,
                    out _
                ),
                "El snapshot se restaura atómicamente."
            );
            report.Expect(telemetry.TotalSelections == 10L, "La restauración conserva selecciones.");
            report.Expect(telemetry.SignatureSelections == 4L, "La restauración conserva selecciones firma.");
            report.Expect(telemetry.TotalOrderedLines == 1L, "La restauración conserva pedidos.");

            BistroBuilderSignatureDishTelemetrySnapshot invalid =
                new BistroBuilderSignatureDishTelemetrySnapshot(
                    1L,
                    2L,
                    0L,
                    0L,
                    0L,
                    0L,
                    null,
                    null
                );
            report.Expect(!invalid.TryValidate(out _), "Un snapshot con más firmas que selecciones se rechaza.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
        }
    }

    private static BistroBuilderCanonicalOrder CreateOrder(
        BistroBuilderResolvedOrderDish dish
    )
    {
        BistroBuilderCanonicalOrderCreationRequest request =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                tableReferenceId = "table_metrics",
                customerGroupReferenceId = "group_metrics",
                mealService = BistroBuilderMealServiceAvailability.Lunch,
                serviceMode = BistroBuilderServiceMode.TableService
            };
        request.lines.Add(
            new BistroBuilderCanonicalOrderLineRequest(
                dish.DishId,
                "customer_metrics",
                new[] { "customer_metrics" },
                1
            )
        );
        BistroBuilderCanonicalOrderFactory.TryCreate(
            request,
            new FixedDishResolver(dish),
            1L,
            out BistroBuilderCanonicalOrder order,
            out _
        );
        return order;
    }

    private static bool TryAdvanceOrderToConsumed(
        BistroBuilderCanonicalOrder order,
        out string error
    )
    {
        if (order == null || order.Lines.Count == 0)
        {
            error = "No existe una comanda válida para avanzar.";
            return false;
        }

        MethodInfo transition = typeof(BistroBuilderCanonicalOrder).GetMethod(
            "TryTransitionLine",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        if (transition == null)
        {
            error = "No se encontró la transición canónica de líneas.";
            return false;
        }

        string lineId = order.Lines[0].LineId;
        BistroBuilderCanonicalOrderLineState[] states =
        {
            BistroBuilderCanonicalOrderLineState.Submitted,
            BistroBuilderCanonicalOrderLineState.Queued,
            BistroBuilderCanonicalOrderLineState.Preparing,
            BistroBuilderCanonicalOrderLineState.ReadyForPickup,
            BistroBuilderCanonicalOrderLineState.AssignedForDelivery,
            BistroBuilderCanonicalOrderLineState.InTransit,
            BistroBuilderCanonicalOrderLineState.Served,
            BistroBuilderCanonicalOrderLineState.Consumed
        };

        for (int index = 0; index < states.Length; index++)
        {
            object[] arguments =
            {
                lineId,
                states[index],
                "autotest_21d",
                null
            };
            object invocation = transition.Invoke(order, arguments);

            if (!(invocation is bool succeeded) || !succeeded)
            {
                error = arguments[3] as string ??
                        "Una transición canónica de 2.1D falló.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static List<BistroBuilderMenuOfferItemSnapshot> CreateCandidates(
        bool signature0,
        bool signature1,
        bool signature2,
        bool signature3
    )
    {
        return new List<BistroBuilderMenuOfferItemSnapshot>
        {
            CreateCandidate(0, signature0, false),
            CreateCandidate(1, signature1, false),
            CreateCandidate(2, signature2, false),
            CreateCandidate(3, signature3, false)
        };
    }

    private static BistroBuilderMenuOfferItemSnapshot
        CreateCandidateWithDishId(string dishId, int displayOrder)
    {
        BistroBuilderDishAvailabilitySnapshot availability =
            new BistroBuilderDishAvailabilitySnapshot(
                dishId,
                BistroBuilderDishAvailabilityState.Available,
                100L,
                string.Empty,
                0L,
                0L,
                1,
                string.Empty
            );

        return new BistroBuilderMenuOfferItemSnapshot(
            "restaurant_main",
            dishId,
            dishId,
            "category_main_course",
            BistroBuilderDishCourse.Main,
            BistroBuilderKitchenStationType.HotKitchen,
            1000,
            displayOrder,
            false,
            BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.TableService,
            BistroBuilderDishServiceModeAvailability.All,
            availability,
            BistroBuilderMenuOfferBlockFlags.None,
            BistroBuilderMenuOfferRejectionReason.None,
            string.Empty,
            1
        );
    }

    private static BistroBuilderMenuOfferItemSnapshot CreateCandidate(
        int index,
        bool signature,
        bool unavailable,
        BistroBuilderServiceMode mode =
            BistroBuilderServiceMode.TableService,
        string restaurantId = "restaurant_main",
        int offerRevision = 1
    )
    {
        string dishId = "dish_21d_" + index;
        BistroBuilderDishAvailabilitySnapshot availability =
            new BistroBuilderDishAvailabilitySnapshot(
                dishId,
                unavailable
                    ? BistroBuilderDishAvailabilityState.OutOfStock
                    : BistroBuilderDishAvailabilityState.Available,
                unavailable ? 0L : 100L,
                string.Empty,
                0L,
                0L,
                1,
                unavailable ? "Agotado" : string.Empty
            );

        return new BistroBuilderMenuOfferItemSnapshot(
            restaurantId,
            dishId,
            "Plato " + index,
            "category_main_course",
            BistroBuilderDishCourse.Main,
            BistroBuilderKitchenStationType.HotKitchen,
            1000 + index,
            index,
            signature,
            BistroBuilderMealServiceAvailability.Lunch,
            mode,
            BistroBuilderDishServiceModeAvailability.All,
            availability,
            unavailable
                ? BistroBuilderMenuOfferBlockFlags.OutOfStock
                : BistroBuilderMenuOfferBlockFlags.None,
            unavailable
                ? BistroBuilderMenuOfferRejectionReason.OutOfStock
                : BistroBuilderMenuOfferRejectionReason.None,
            unavailable ? "Agotado" : string.Empty,
            offerRevision
        );
    }

    private static BistroBuilderMenuSelectionContext CreateContext(
        int ordinal,
        int fallbackOffset
    )
    {
        return new BistroBuilderMenuSelectionContext(
            BistroBuilderMealServiceAvailability.Lunch,
            BistroBuilderServiceMode.TableService,
            "customer_21d_" + ordinal,
            1,
            ordinal,
            fallbackOffset
        );
    }

    private static int ParseDishIndex(string dishId)
    {
        return int.Parse(dishId.Substring(dishId.Length - 1));
    }

    private static void ConfigurePolicy(
        BistroBuilderMenuCommercialPolicy policy,
        int maximumSignatureDishes,
        int signatureWeightBasisPoints
    )
    {
        SerializedObject serialized = new SerializedObject(policy);
        serialized.FindProperty("minimumPriceCents").intValue = 0;
        serialized.FindProperty("maximumPriceCents").intValue = 1000000;
        serialized.FindProperty("maximumMenuItems").intValue = 256;
        serialized.FindProperty("maximumSignatureDishes").intValue =
            maximumSignatureDishes;
        serialized.FindProperty("requireSignatureDishEnabled").boolValue =
            true;
        serialized.FindProperty("requireSignatureDishUnlocked").boolValue =
            true;
        serialized.FindProperty(
            "requireSignatureDishServiceAvailability"
        ).boolValue = true;
        serialized.FindProperty(
            "signatureSelectionWeightBasisPoints"
        ).intValue = signatureWeightBasisPoints;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
