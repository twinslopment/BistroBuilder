using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Autotest aislado del dominio 367B.
///
/// No modifica escenas, assets, carta real ni partidas guardadas.
/// </summary>
public static class BistroBuilderCanonicalOrderFoundationSelfTest
{
    private static int passed;
    private static int failed;
    private static readonly List<string> messages = new List<string>();

    [MenuItem(
        "Tools/Bistro Builder/Orders/" +
        "Run 367B Canonical Orders Self-Test",
        false,
        202
    )]
    public static void RunFromMenu()
    {
        passed = 0;
        failed = 0;
        messages.Clear();

        try
        {
            RunIdentityTests();
            RunFactoryTests();
            RunTransitionTests();
            RunSharedDishAndCourseTests();
            RunSnapshotTests();
            RunAtomicityTests();
        }
        catch (Exception exception)
        {
            Check(false, "Excepción no controlada: " + exception.Message);
            Debug.LogException(exception);
        }

        System.Text.StringBuilder report =
            new System.Text.StringBuilder();

        report.AppendLine("BISTRO BUILDER - AUTOTEST 367B");
        report.AppendLine("Pruebas superadas: " + passed);
        report.AppendLine("Pruebas fallidas: " + failed);

        for (int index = 0; index < messages.Count; index++)
        {
            report.AppendLine(messages[index]);
        }

        Debug.Log(report.ToString());

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            report.ToString(),
            "Aceptar"
        );
    }

    private static void RunIdentityTests()
    {
        Check(
            BistroBuilderOrderIdUtility.Normalize(" Order_TEST ") ==
            "order_test",
            "La identidad se normaliza."
        );
        Check(
            BistroBuilderOrderIdUtility.IsValid("order_001"),
            "OrderId válido aceptado."
        );
        Check(
            BistroBuilderOrderIdUtility.IsValid("customer.alpha-01"),
            "Referencia con punto y guion aceptada."
        );
        Check(
            !BistroBuilderOrderIdUtility.IsValid("AB"),
            "Identidad demasiado corta rechazada."
        );
        Check(
            !BistroBuilderOrderIdUtility.IsValid("cliente con espacios"),
            "Identidad con espacios rechazada."
        );
        Check(
            !BistroBuilderOrderIdUtility.IsValid("cliente/01"),
            "Identidad con separador frágil rechazada."
        );

        string orderIdA = BistroBuilderOrderIdUtility.NewOrderId();
        string orderIdB = BistroBuilderOrderIdUtility.NewOrderId();
        string lineIdA = BistroBuilderOrderIdUtility.NewLineId();
        string lineIdB = BistroBuilderOrderIdUtility.NewLineId();

        Check(orderIdA != orderIdB, "OrderId generado es único.");
        Check(lineIdA != lineIdB, "LineId generado es único.");
        Check(
            BistroBuilderOrderIdUtility.IsValid(orderIdA),
            "OrderId generado tiene formato estable."
        );
        Check(
            BistroBuilderOrderIdUtility.IsValid(lineIdA),
            "LineId generado tiene formato estable."
        );
    }

    private static void RunFactoryTests()
    {
        FakeDishResolver resolver = BuildResolver();
        BistroBuilderCanonicalOrderCreationRequest request =
            BuildIndividualRequest(3);

        bool created = BistroBuilderCanonicalOrderFactory.TryCreate(
            request,
            resolver,
            1,
            out BistroBuilderCanonicalOrder order,
            out BistroBuilderCanonicalOrderOperationResult result
        );

        Check(created, "La fábrica crea una comanda válida.");
        Check(result.Succeeded, "El resultado de creación es correcto.");
        Check(order != null, "La comanda creada existe.");
        Check(order.SequenceNumber == 1, "La secuencia se conserva.");
        Check(order.Lines.Count == 3, "Se crea una línea por cliente.");
        Check(
            order.State == BistroBuilderCanonicalOrderState.Draft,
            "La comanda comienza en Draft."
        );
        Check(
            order.TableReferenceId == "table_test_001",
            "La referencia de mesa se conserva."
        );
        Check(
            order.CustomerGroupReferenceId == "group_test_001",
            "La referencia de grupo se conserva."
        );
        Check(
            order.MealService ==
                BistroBuilderMealServiceAvailability.Lunch,
            "El servicio concreto se conserva."
        );
        Check(
            order.Lines[0].PrimaryCustomerId == "customer_test_001",
            "La primera línea conserva su cliente."
        );
        Check(
            order.Lines[1].DishId == "dish_merluza_plancha",
            "La segunda línea conserva su plato."
        );
        Check(
            order.Lines[2].PriceCentsAtOrder == 650,
            "El precio queda congelado al crear."
        );
        Check(
            order.CalculateTotalPriceCents() == 4350,
            "El total económico se calcula por líneas."
        );
        Check(order.TryValidate(out _), "La comanda creada se valida.");

        BistroBuilderCanonicalOrderCreationRequest empty =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                tableReferenceId = "table_test_001",
                customerGroupReferenceId = "group_test_001",
                mealService =
                    BistroBuilderMealServiceAvailability.Lunch
            };

        Check(
            !BistroBuilderCanonicalOrderFactory.TryCreate(
                empty,
                resolver,
                2,
                out _,
                out _
            ),
            "Una comanda sin líneas se rechaza."
        );

        BistroBuilderCanonicalOrderCreationRequest invalidTable =
            BuildIndividualRequest(1);
        invalidTable.tableReferenceId = "mesa con espacio";
        Check(
            !BistroBuilderCanonicalOrderFactory.TryCreate(
                invalidTable,
                resolver,
                2,
                out _,
                out _
            ),
            "Una referencia de mesa inválida se rechaza."
        );

        BistroBuilderCanonicalOrderCreationRequest unknownDish =
            BuildIndividualRequest(1);
        unknownDish.lines[0].dishId = "dish_inexistente";
        Check(
            !BistroBuilderCanonicalOrderFactory.TryCreate(
                unknownDish,
                resolver,
                2,
                out _,
                out BistroBuilderCanonicalOrderOperationResult unknownResult
            ),
            "Un plato inexistente se rechaza."
        );
        Check(
            unknownResult.FailureReason ==
                BistroBuilderCanonicalOrderFailureReason.DishUnavailable,
            "El fallo de plato se tipifica."
        );

        resolver.Block("dish_fabada_asturiana");
        BistroBuilderCanonicalOrderCreationRequest blocked =
            BuildIndividualRequest(1);
        Check(
            !BistroBuilderCanonicalOrderFactory.TryCreate(
                blocked,
                resolver,
                2,
                out _,
                out _
            ),
            "Un plato no pedible se rechaza."
        );
    }

    private static void RunTransitionTests()
    {
        BistroBuilderCanonicalOrder order = CreateOrder(2);
        string firstLine = order.Lines[0].LineId;
        string secondLine = order.Lines[1].LineId;

        Check(
            !order.TryTransitionLine(
                firstLine,
                BistroBuilderCanonicalOrderLineState.Preparing,
                "waiter_test_001",
                out _
            ),
            "No se puede saltar de Draft a Preparing."
        );
        Check(
            order.TryTransitionLine(
                firstLine,
                BistroBuilderCanonicalOrderLineState.Submitted,
                "waiter_test_001",
                out _
            ),
            "Draft pasa a Submitted."
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.Draft,
            "Una línea aún Draft mantiene el agregado Draft."
        );
        Check(
            order.TryTransitionLine(
                secondLine,
                BistroBuilderCanonicalOrderLineState.Submitted,
                "waiter_test_001",
                out _
            ),
            "La segunda línea pasa a Submitted."
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.Submitted,
            "Todas las líneas Submitted actualizan el agregado."
        );

        Advance(order, firstLine, BistroBuilderCanonicalOrderLineState.Queued);
        Advance(order, secondLine, BistroBuilderCanonicalOrderLineState.Queued);
        Check(
            order.State == BistroBuilderCanonicalOrderState.InProgress,
            "Queued produce estado agregado InProgress."
        );

        Advance(
            order,
            firstLine,
            BistroBuilderCanonicalOrderLineState.Preparing
        );
        Advance(
            order,
            secondLine,
            BistroBuilderCanonicalOrderLineState.Preparing
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.InProgress,
            "Preparing mantiene InProgress."
        );

        Advance(
            order,
            firstLine,
            BistroBuilderCanonicalOrderLineState.ReadyForPickup
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.InProgress,
            "Una línea lista no oculta otra en preparación."
        );
        Advance(
            order,
            secondLine,
            BistroBuilderCanonicalOrderLineState.ReadyForPickup
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.ReadyForPickup,
            "Todas las líneas listas producen ReadyForPickup."
        );

        Advance(
            order,
            firstLine,
            BistroBuilderCanonicalOrderLineState.AssignedForDelivery
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.InDelivery,
            "Una línea asignada produce InDelivery."
        );
        Advance(
            order,
            firstLine,
            BistroBuilderCanonicalOrderLineState.InTransit
        );
        Advance(
            order,
            firstLine,
            BistroBuilderCanonicalOrderLineState.Served
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.InProgress,
            "Un plato servido no completa otro aún en pase."
        );

        Advance(
            order,
            secondLine,
            BistroBuilderCanonicalOrderLineState.AssignedForDelivery
        );
        Advance(
            order,
            secondLine,
            BistroBuilderCanonicalOrderLineState.InTransit
        );
        Advance(
            order,
            secondLine,
            BistroBuilderCanonicalOrderLineState.Served
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.Served,
            "Todas las líneas servidas producen Served."
        );

        Advance(
            order,
            firstLine,
            BistroBuilderCanonicalOrderLineState.Consumed
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.Served,
            "Un cliente puede terminar antes que otro."
        );
        Advance(
            order,
            secondLine,
            BistroBuilderCanonicalOrderLineState.Consumed
        );
        Check(
            order.State == BistroBuilderCanonicalOrderState.Completed,
            "Todas las líneas consumidas completan la comanda."
        );
        Check(order.IsTerminal, "Completed es terminal.");
        Check(
            !order.TryTransitionLine(
                firstLine,
                BistroBuilderCanonicalOrderLineState.Served,
                "waiter_test_001",
                out _
            ),
            "Una comanda terminal no admite transiciones."
        );
        Check(
            order.CalculateTotalPriceCents() == 3700,
            "Consumir no altera el precio congelado."
        );

        BistroBuilderCanonicalOrder cancellable = CreateOrder(2);
        Check(
            cancellable.TryCancel("manager_test_001", out _),
            "Una comanda activa puede cancelarse."
        );
        Check(
            cancellable.State ==
                BistroBuilderCanonicalOrderState.Cancelled,
            "Cancelar todas las líneas produce Cancelled."
        );
        Check(
            cancellable.CalculateTotalPriceCents() == 0,
            "Las líneas canceladas no suman al total."
        );
        Check(
            !cancellable.TryCancel("manager_test_001", out _),
            "Una comanda cancelada no se cancela de nuevo."
        );
    }

    private static void RunSharedDishAndCourseTests()
    {
        FakeDishResolver resolver = BuildResolver();
        BistroBuilderCanonicalOrderCreationRequest request =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                tableReferenceId = "table_shared_001",
                customerGroupReferenceId = "group_shared_001",
                mealService = BistroBuilderMealServiceAvailability.Dinner
            };

        request.lines.Add(
            new BistroBuilderCanonicalOrderLineRequest(
                "dish_fabada_asturiana",
                string.Empty,
                new[]
                {
                    "customer_shared_001",
                    "customer_shared_002"
                },
                2
            )
        );
        request.lines.Add(
            new BistroBuilderCanonicalOrderLineRequest(
                "dish_tarta_queso",
                "customer_shared_001",
                new[] { "customer_shared_001" },
                3
            )
        );

        bool created = BistroBuilderCanonicalOrderFactory.TryCreate(
            request,
            resolver,
            5,
            out BistroBuilderCanonicalOrder order,
            out _
        );

        Check(created, "Se crea una comanda con plato compartido.");
        Check(order.Lines[0].IsShared, "La línea compartida se identifica.");
        Check(
            order.Lines[0].ConsumerCustomerIds.Count == 2,
            "El plato compartido conserva dos consumidores."
        );
        Check(
            string.IsNullOrEmpty(order.Lines[0].PrimaryCustomerId),
            "Un compartido puede no tener cliente principal."
        );
        Check(
            order.Lines[0].CourseIndex == 2,
            "El pase principal se conserva."
        );
        Check(
            order.Lines[1].CourseIndex == 3,
            "El pase de postre se conserva."
        );

        BistroBuilderCanonicalOrderCreationRequest duplicateConsumer =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                tableReferenceId = "table_shared_001",
                customerGroupReferenceId = "group_shared_001",
                mealService = BistroBuilderMealServiceAvailability.Dinner
            };
        duplicateConsumer.lines.Add(
            new BistroBuilderCanonicalOrderLineRequest(
                "dish_fabada_asturiana",
                "customer_shared_001",
                new[]
                {
                    "customer_shared_001",
                    "customer_shared_001"
                },
                1
            )
        );
        Check(
            !BistroBuilderCanonicalOrderFactory.TryCreate(
                duplicateConsumer,
                resolver,
                6,
                out _,
                out _
            ),
            "Un consumidor duplicado se rechaza."
        );

        BistroBuilderCanonicalOrderCreationRequest primaryOutside =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                tableReferenceId = "table_shared_001",
                customerGroupReferenceId = "group_shared_001",
                mealService = BistroBuilderMealServiceAvailability.Dinner
            };
        primaryOutside.lines.Add(
            new BistroBuilderCanonicalOrderLineRequest(
                "dish_fabada_asturiana",
                "customer_shared_999",
                new[] { "customer_shared_001" },
                1
            )
        );
        Check(
            !BistroBuilderCanonicalOrderFactory.TryCreate(
                primaryOutside,
                resolver,
                7,
                out _,
                out _
            ),
            "Un cliente principal ajeno a consumidores se rechaza."
        );
    }

    private static void RunSnapshotTests()
    {
        BistroBuilderCanonicalOrder first = CreateOrder(2);
        BistroBuilderCanonicalOrder second = CreateOrder(1, 2);
        List<BistroBuilderCanonicalOrder> source =
            new List<BistroBuilderCanonicalOrder> { first, second };

        BistroBuilderCanonicalOrderRuntimeSnapshot snapshot =
            new BistroBuilderCanonicalOrderRuntimeSnapshot(3, source);

        Check(snapshot.SchemaVersion == 1, "El snapshot está versionado.");
        Check(snapshot.NextSequenceNumber == 3, "La secuencia se captura.");
        Check(snapshot.Orders.Count == 2, "Se capturan dos comandas.");
        Check(snapshot.TryValidate(out _), "El snapshot se valida.");

        BistroBuilderCanonicalOrderRuntimeSnapshot clone =
            snapshot.Clone();
        Check(!ReferenceEquals(snapshot, clone), "El snapshot se clona.");
        Check(
            !ReferenceEquals(snapshot.Orders[0], clone.Orders[0]),
            "Las comandas del snapshot se clonan profundamente."
        );
        Check(
            !ReferenceEquals(
                snapshot.Orders[0].Lines[0],
                clone.Orders[0].Lines[0]
            ),
            "Las líneas del snapshot se clonan profundamente."
        );
        Check(
            snapshot.Orders[0].OrderId == clone.Orders[0].OrderId,
            "La identidad se conserva al clonar."
        );
        Check(
            snapshot.Orders[0].Lines[0].DishId ==
            clone.Orders[0].Lines[0].DishId,
            "El DishId se conserva al clonar."
        );

        BistroBuilderCanonicalOrder snapshotOrder = clone.Orders[0];
        string lineId = snapshotOrder.Lines[0].LineId;
        Check(
            snapshotOrder.TryTransitionLine(
                lineId,
                BistroBuilderCanonicalOrderLineState.Submitted,
                "snapshot_test_actor",
                out _
            ),
            "La copia puede mutar de forma independiente."
        );
        Check(
            snapshot.Orders[0].Lines[0].State ==
                BistroBuilderCanonicalOrderLineState.Draft,
            "Mutar la copia no altera el original."
        );
    }

    private static void RunAtomicityTests()
    {
        FakeDishResolver resolver = BuildResolver();
        BistroBuilderCanonicalOrderCreationRequest request =
            BuildIndividualRequest(3);
        request.lines[2].dishId = "dish_inexistente";

        bool created = BistroBuilderCanonicalOrderFactory.TryCreate(
            request,
            resolver,
            10,
            out BistroBuilderCanonicalOrder order,
            out BistroBuilderCanonicalOrderOperationResult result
        );

        Check(!created, "Una línea inválida cancela toda la creación.");
        Check(order == null, "No queda un agregado parcial.");
        Check(!result.Succeeded, "El resultado atómico informa del fallo.");

        BistroBuilderCanonicalOrder valid = CreateOrder(2);
        string lineId = valid.Lines[0].LineId;
        int revision = valid.Revision;
        BistroBuilderCanonicalOrderLineState state = valid.Lines[0].State;

        bool changed = valid.TryTransitionLine(
            lineId,
            BistroBuilderCanonicalOrderLineState.InTransit,
            "invalid_actor",
            out _
        );

        Check(!changed, "Una transición inválida no se aplica.");
        Check(valid.Revision == revision, "No aumenta la revisión al fallar.");
        Check(
            valid.Lines[0].State == state,
            "El estado permanece intacto al fallar."
        );

        Check(
            BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.AssignedForDelivery,
                BistroBuilderCanonicalOrderLineState.ReadyForPickup
            ),
            "Una asignación puede liberarse de nuevo al pase."
        );
        Check(
            BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.InTransit,
                BistroBuilderCanonicalOrderLineState.ReadyForPickup
            ),
            "Un transporte fallido puede devolver el plato al pase."
        );
        Check(
            !BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.Consumed,
                BistroBuilderCanonicalOrderLineState.Served
            ),
            "Consumed es inmutable."
        );
        Check(
            !BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.Cancelled,
                BistroBuilderCanonicalOrderLineState.Submitted
            ),
            "Cancelled es inmutable."
        );
        Check(
            !BistroBuilderCanonicalOrderTransitionPolicy.CanTransition(
                BistroBuilderCanonicalOrderLineState.Failed,
                BistroBuilderCanonicalOrderLineState.Queued
            ),
            "Failed es inmutable."
        );
    }

    private static BistroBuilderCanonicalOrder CreateOrder(
        int customerCount,
        long sequence = 1
    )
    {
        bool created = BistroBuilderCanonicalOrderFactory.TryCreate(
            BuildIndividualRequest(customerCount),
            BuildResolver(),
            sequence,
            out BistroBuilderCanonicalOrder order,
            out BistroBuilderCanonicalOrderOperationResult result
        );

        if (!created)
        {
            throw new InvalidOperationException(result.Message);
        }

        return order;
    }

    private static BistroBuilderCanonicalOrderCreationRequest
        BuildIndividualRequest(int customerCount)
    {
        string[] dishes =
        {
            "dish_fabada_asturiana",
            "dish_merluza_plancha",
            "dish_tarta_queso"
        };

        BistroBuilderCanonicalOrderCreationRequest request =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                tableReferenceId = "table_test_001",
                customerGroupReferenceId = "group_test_001",
                mealService = BistroBuilderMealServiceAvailability.Lunch
            };

        for (int index = 0; index < customerCount; index++)
        {
            string customerId =
                "customer_test_" + (index + 1).ToString("000");
            request.lines.Add(
                new BistroBuilderCanonicalOrderLineRequest(
                    dishes[index % dishes.Length],
                    customerId,
                    new[] { customerId },
                    1
                )
            );
        }

        return request;
    }

    private static FakeDishResolver BuildResolver()
    {
        FakeDishResolver resolver = new FakeDishResolver();
        resolver.Add("dish_fabada_asturiana", 1850, 0);
        resolver.Add("dish_merluza_plancha", 1850, 1);
        resolver.Add("dish_tarta_queso", 650, 2);
        return resolver;
    }

    private static void Advance(
        BistroBuilderCanonicalOrder order,
        string lineId,
        BistroBuilderCanonicalOrderLineState target
    )
    {
        if (!order.TryTransitionLine(
                lineId,
                target,
                "self_test_actor",
                out string error
            ))
        {
            throw new InvalidOperationException(error);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (condition)
        {
            passed++;
            messages.Add("- OK: " + message);
        }
        else
        {
            failed++;
            messages.Add("- FALLO: " + message);
        }
    }

    private sealed class FakeDishResolver :
        IBistroBuilderOrderDishResolver
    {
        private readonly Dictionary<string, BistroBuilderResolvedOrderDish>
            dishes =
                new Dictionary<string, BistroBuilderResolvedOrderDish>(
                    StringComparer.Ordinal
                );

        private readonly HashSet<string> blocked =
            new HashSet<string>(StringComparer.Ordinal);

        public void Add(string dishId, int priceCents, int displayOrder)
        {
            string normalized = BistroBuilderOrderIdUtility.Normalize(dishId);
            dishes[normalized] = new BistroBuilderResolvedOrderDish(
                normalized,
                priceCents,
                displayOrder
            );
        }

        public void Block(string dishId)
        {
            blocked.Add(BistroBuilderOrderIdUtility.Normalize(dishId));
        }

        public bool TryResolveOrderableDish(
            string dishId,
            BistroBuilderMealServiceAvailability mealService,
            out BistroBuilderResolvedOrderDish dish,
            out string rejectionReason
        )
        {
            string normalized = BistroBuilderOrderIdUtility.Normalize(dishId);

            if (blocked.Contains(normalized))
            {
                dish = default(BistroBuilderResolvedOrderDish);
                rejectionReason = "Plato bloqueado por el test.";
                return false;
            }

            if (!dishes.TryGetValue(normalized, out dish))
            {
                rejectionReason = "Plato inexistente.";
                return false;
            }

            rejectionReason = string.Empty;
            return true;
        }
    }
}
