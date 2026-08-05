using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Construye una petición canónica de comanda a partir de un perfil de datos.
///
/// No crea ni modifica comandas. Su salida se entrega a
/// BistroBuilderCanonicalOrderService, que conserva la autoridad transaccional.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Orders/Order Composition Service")]
public sealed class BistroBuilderOrderCompositionService : MonoBehaviour
{
    public const string RuntimeRevision = "367F";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderMenuOfferService offerService;

    [SerializeField]
    private BistroBuilderMenuSelectionService selectionService;

    [SerializeField]
    private BistroBuilderOrderCompositionProfile compositionProfile;

    [Header("Depuración")]

    [SerializeField]
    private bool logComposition;

    private readonly List<BistroBuilderMenuOfferItemSnapshot> offerBuffer =
        new List<BistroBuilderMenuOfferItemSnapshot>(32);

    private readonly List<string> orderableDishIds =
        new List<string>(32);

    private readonly List<string> normalizedCustomerIds =
        new List<string>(16);

    private readonly HashSet<string> uniqueCustomerIds =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly HashSet<string> coveredCustomerIds =
        new HashSet<string>(StringComparer.Ordinal);

    private readonly List<string> consumerBuffer =
        new List<string>(32);

    public BistroBuilderRestaurantMenuService MenuService => menuService;
    public BistroBuilderMenuOfferService OfferService => offerService;
    public BistroBuilderMenuSelectionService SelectionService =>
        selectionService;
    public BistroBuilderOrderCompositionProfile CompositionProfile =>
        compositionProfile;

    private void Awake()
    {
        ResolveDependencies();

        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService en el compositor.";
            return false;
        }

        if (!menuService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (offerService != null &&
            !ReferenceEquals(offerService.MenuService, menuService))
        {
            error = "El compositor no comparte la oferta canónica.";
            return false;
        }

        if (selectionService != null)
        {
            if (!selectionService.ValidateConfiguration(out error))
            {
                return false;
            }

            if (offerService == null ||
                !ReferenceEquals(
                    selectionService.OfferService,
                    offerService
                ))
            {
                error = "El compositor no comparte la selección 2.1D.";
                return false;
            }
        }

        if (compositionProfile == null)
        {
            error = "Falta el perfil de composición de comandas 367F.";
            return false;
        }

        return compositionProfile.TryValidate(out error);
    }

    public bool TryBuildCreationRequest(
        string externalReferenceId,
        string tableReferenceId,
        string customerGroupReferenceId,
        IList<string> customerIds,
        BistroBuilderMealServiceAvailability mealService,
        out BistroBuilderCanonicalOrderCreationRequest request,
        out string error
    )
    {
        request = null;

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                mealService,
                false
            ) ||
            mealService == BistroBuilderMealServiceAvailability.All)
        {
            error = "El compositor necesita un servicio de comida concreto.";
            return false;
        }

        if (!TryNormalizeCustomers(customerIds, out error))
        {
            return false;
        }

        if (!TryBuildOrderableDishList(mealService, out error))
        {
            return false;
        }

        BistroBuilderCanonicalOrderCreationRequest candidate =
            new BistroBuilderCanonicalOrderCreationRequest
            {
                externalReferenceId = externalReferenceId,
                tableReferenceId = tableReferenceId,
                customerGroupReferenceId = customerGroupReferenceId,
                mealService = mealService,
                serviceMode = BistroBuilderServiceMode.TableService
            };

        coveredCustomerIds.Clear();
        int generatedLineCount = 0;
        IReadOnlyList<BistroBuilderCourseCompositionRule> rules =
            compositionProfile.Rules;

        for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            BistroBuilderCourseCompositionRule rule = rules[ruleIndex];

            if (rule == null || !rule.Enabled)
            {
                continue;
            }

            switch (rule.CompositionMode)
            {
                case BistroBuilderOrderLineCompositionMode
                    .SharedAllCustomers:
                    consumerBuffer.Clear();
                    consumerBuffer.AddRange(normalizedCustomerIds);
                    if (!TryAddLine(
                            candidate,
                            rule,
                            0,
                            consumerBuffer,
                            out error
                        ))
                    {
                        return false;
                    }

                    generatedLineCount++;
                    break;

                case BistroBuilderOrderLineCompositionMode
                    .IndividualPerCustomer:
                    for (int customerIndex = 0;
                         customerIndex < normalizedCustomerIds.Count;
                         customerIndex++)
                    {
                        consumerBuffer.Clear();
                        consumerBuffer.Add(
                            normalizedCustomerIds[customerIndex]
                        );
                        if (!TryAddLine(
                                candidate,
                                rule,
                                customerIndex,
                                consumerBuffer,
                                out error
                            ))
                        {
                            return false;
                        }
                    }

                    generatedLineCount += normalizedCustomerIds.Count;
                    break;

                case BistroBuilderOrderLineCompositionMode.SharedGroups:
                    int groupSize = Math.Max(2, rule.SharedGroupSize);

                    int groupOrdinal = 0;

                    for (int start = 0;
                         start < normalizedCustomerIds.Count;
                         start += groupSize)
                    {
                        consumerBuffer.Clear();
                        int end = Math.Min(
                            normalizedCustomerIds.Count,
                            start + groupSize
                        );

                        for (int customerIndex = start;
                             customerIndex < end;
                             customerIndex++)
                        {
                            consumerBuffer.Add(
                                normalizedCustomerIds[customerIndex]
                            );
                        }

                        if (!TryAddLine(
                                candidate,
                                rule,
                                groupOrdinal,
                                consumerBuffer,
                                out error
                            ))
                        {
                            return false;
                        }

                        generatedLineCount++;
                        groupOrdinal++;
                    }
                    break;

                default:
                    error = "El perfil contiene un modo de composición desconocido.";
                    return false;
            }

            if (generatedLineCount >
                BistroBuilderCourseAndSharingPolicy.MaximumLinesPerOrder)
            {
                error = "La composición supera el máximo de líneas por comanda.";
                return false;
            }
        }

        if (candidate.lines.Count == 0)
        {
            error = "El perfil no generó ninguna línea de comanda.";
            return false;
        }

        if (coveredCustomerIds.Count != normalizedCustomerIds.Count)
        {
            error = "La composición no cubre a todos los clientes del grupo.";
            return false;
        }

        request = candidate;
        error = string.Empty;

        if (logComposition)
        {
            Debug.Log(
                "367F compuso " + request.lines.Count +
                " línea(s) para " + normalizedCustomerIds.Count +
                " cliente(s) y " + CountDistinctCourses(request.lines) +
                " pase(s).",
                this
            );
        }

        return true;
    }

    private bool TryAddLine(
        BistroBuilderCanonicalOrderCreationRequest request,
        BistroBuilderCourseCompositionRule rule,
        int selectionOrdinal,
        List<string> consumers,
        out string error
    )
    {
        string primaryCustomerId =
            consumers != null && consumers.Count > 0
                ? consumers[0]
                : string.Empty;
        string dishId;

        if (selectionService != null)
        {
            string selectionReferenceId =
                BistroBuilderOrderIdUtility.IsValid(primaryCustomerId)
                    ? primaryCustomerId
                    : request.customerGroupReferenceId;
            BistroBuilderMenuSelectionContext context =
                new BistroBuilderMenuSelectionContext(
                    request.mealService,
                    BistroBuilderServiceMode.TableService,
                    selectionReferenceId,
                    rule.CourseIndex,
                    selectionOrdinal,
                    rule.MenuDisplayOffset + selectionOrdinal
                );

            if (!selectionService.TrySelectFromCandidates(
                    context,
                    offerBuffer,
                    null,
                    out BistroBuilderMenuSelectionResult selection,
                    out error
                ))
            {
                return false;
            }

            dishId = selection.DishId;
        }
        else
        {
            int dishIndex = PositiveModulo(
                rule.MenuDisplayOffset + selectionOrdinal,
                orderableDishIds.Count
            );
            dishId = orderableDishIds[dishIndex];
        }

        request.lines.Add(
            new BistroBuilderCanonicalOrderLineRequest(
                dishId,
                primaryCustomerId,
                consumers,
                rule.CourseIndex
            )
        );

        for (int index = 0; index < consumers.Count; index++)
        {
            coveredCustomerIds.Add(consumers[index]);
        }

        error = string.Empty;
        return true;
    }

    private bool TryNormalizeCustomers(
        IList<string> source,
        out string error
    )
    {
        normalizedCustomerIds.Clear();
        uniqueCustomerIds.Clear();

        if (source == null || source.Count == 0)
        {
            error = "El compositor necesita al menos un CustomerId.";
            return false;
        }

        for (int index = 0; index < source.Count; index++)
        {
            string customerId = BistroBuilderOrderIdUtility.Normalize(
                source[index]
            );

            if (!BistroBuilderOrderIdUtility.IsValid(customerId))
            {
                error = "El compositor recibió un CustomerId inválido.";
                return false;
            }

            if (!uniqueCustomerIds.Add(customerId))
            {
                error = "El compositor recibió un CustomerId duplicado.";
                return false;
            }

            normalizedCustomerIds.Add(customerId);
        }

        normalizedCustomerIds.Sort(StringComparer.Ordinal);
        error = string.Empty;
        return true;
    }

    private bool TryBuildOrderableDishList(
        BistroBuilderMealServiceAvailability mealService,
        out string error
    )
    {
        offerBuffer.Clear();
        orderableDishIds.Clear();

        if (offerService != null)
        {
            if (!offerService.TryGetOffer(
                    mealService,
                    BistroBuilderServiceMode.TableService,
                    false,
                    offerBuffer,
                    out error
                ))
            {
                return false;
            }

            for (int index = 0; index < offerBuffer.Count; index++)
            {
                BistroBuilderMenuOfferItemSnapshot item = offerBuffer[index];

                if (item.IsOrderable)
                {
                    orderableDishIds.Add(item.DishId);
                }
            }
        }
        else
        {
            // Compatibilidad de los autotests aislados 367F. La escena
            // instalada por 2.1C siempre recibe offerService y el validador lo
            // exige; esta rama solo evalúa el estado persistente de carta y no
            // depende del inventario runtime de la escena.
            List<BistroBuilderMenuItemRuntimeState> legacyItems =
                new List<BistroBuilderMenuItemRuntimeState>();

            if (!menuService.TryGetSnapshot(legacyItems, out error))
            {
                return false;
            }

            for (int index = 0; index < legacyItems.Count; index++)
            {
                BistroBuilderMenuItemRuntimeState item = legacyItems[index];

                if (item != null &&
                    menuService.IsDishEligibleByMenuState(
                        item.DishId,
                        mealService,
                        out _
                    ) &&
                    menuService.CatalogService.TryGetDefinition(
                        item.DishId,
                        out BistroBuilderDishDefinition definition
                    ) &&
                    definition.IsAvailableForServiceMode(
                        BistroBuilderServiceMode.TableService
                    ))
                {
                    orderableDishIds.Add(item.DishId);
                }
            }
        }

        if (orderableDishIds.Count == 0)
        {
            error = "No existen platos pedibles para componer la comanda " +
                    "de mesa.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int CountDistinctCourses(
        IList<BistroBuilderCanonicalOrderLineRequest> lines
    )
    {
        HashSet<int> courses = new HashSet<int>();

        for (int index = 0; index < lines.Count; index++)
        {
            courses.Add(lines[index].courseIndex);
        }

        return courses.Count;
    }

    private static int PositiveModulo(int value, int modulus)
    {
        if (modulus <= 0)
        {
            return 0;
        }

        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    private void ResolveDependencies()
    {
        if (menuService == null)
        {
            TryGetComponent(out menuService);
        }

        if (offerService == null)
        {
            TryGetComponent(out offerService);
        }

        if (selectionService == null)
        {
            TryGetComponent(out selectionService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ResolveDependencies();
    }
#endif
}
