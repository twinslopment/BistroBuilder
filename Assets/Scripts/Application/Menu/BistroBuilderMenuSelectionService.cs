using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Autoridad runtime de selección de platos 2.1D.
///
/// Obtiene la oferta exclusivamente desde 2.1C y aplica la ponderación de
/// platos firma definida por la política comercial. No convierte la elección
/// en disponibilidad, no persiste estado derivado y no usa aleatoriedad global.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Menu/Menu Selection Service")]
public sealed class BistroBuilderMenuSelectionService : MonoBehaviour
{
    public const string RuntimeRevision = "MENU-2.1D";

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderMenuOfferService offerService;

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [Header("Depuración")]

    [SerializeField]
    private bool logSelections;

    private readonly List<BistroBuilderMenuOfferItemSnapshot> offerBuffer =
        new List<BistroBuilderMenuOfferItemSnapshot>(32);
    private readonly List<BistroBuilderMenuSelectionResult> resultBuffer =
        new List<BistroBuilderMenuSelectionResult>(16);
    private readonly HashSet<string> exclusionBuffer =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly BistroBuilderMenuSelectionScratch selectionScratch =
        new BistroBuilderMenuSelectionScratch();

    // Protege los buffers reutilizables y la publicación de eventos frente a
    // llamadas reentrantes desde un suscriptor de SelectionCompleted. Unity
    // ejecuta esta autoridad en el hilo principal, pero un callback puede
    // intentar iniciar otra selección antes de que termine la actual.
    private bool operationInProgress;

    public event Action<BistroBuilderMenuSelectionCompletedEvent>
        SelectionCompleted;

    public BistroBuilderMenuOfferService OfferService => offerService;
    public BistroBuilderRestaurantMenuService MenuService => menuService;
    public long SelectionSequence { get; private set; }

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    private void Start()
    {
        if (!ValidateConfiguration(out string error))
        {
            Debug.LogError(error, this);
        }
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (offerService == null)
        {
            error = "Falta BistroBuilderMenuOfferService en selección 2.1D.";
            return false;
        }

        if (!offerService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService en selección.";
            return false;
        }

        if (!ReferenceEquals(offerService.MenuService, menuService))
        {
            error = "Selección y oferta no comparten la carta canónica.";
            return false;
        }

        if (menuService.CommercialPolicy == null)
        {
            error = "Falta la política comercial de selección.";
            return false;
        }

        return menuService.CommercialPolicy.TryValidate(out error);
    }

    public bool TrySelectFromCurrentOffer(
        BistroBuilderMenuSelectionContext context,
        ISet<string> excludedDishIds,
        out BistroBuilderMenuSelectionResult result,
        out string error
    )
    {
        result = default(BistroBuilderMenuSelectionResult);

        if (operationInProgress)
        {
            error = "La selección 2.1D no admite llamadas reentrantes.";
            return false;
        }

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        offerBuffer.Clear();

        if (!offerService.TryGetOffer(
                context.MealService,
                context.ServiceMode,
                false,
                offerBuffer,
                out error
            ))
        {
            return false;
        }

        return TrySelectFromCandidates(
            context,
            offerBuffer,
            excludedDishIds,
            null,
            out result,
            out error
        );
    }

    public bool TrySelectFromCandidates(
        BistroBuilderMenuSelectionContext context,
        IList<BistroBuilderMenuOfferItemSnapshot> candidates,
        ISet<string> excludedDishIds,
        out BistroBuilderMenuSelectionResult result,
        out string error
    )
    {
        return TrySelectFromCandidates(
            context,
            candidates,
            excludedDishIds,
            null,
            out result,
            out error
        );
    }

    public bool TrySelectFromCandidates(
        BistroBuilderMenuSelectionContext context,
        IList<BistroBuilderMenuOfferItemSnapshot> candidates,
        ISet<string> excludedDishIds,
        IBistroBuilderMenuSelectionRandomSource randomSource,
        out BistroBuilderMenuSelectionResult result,
        out string error
    )
    {
        result = default(BistroBuilderMenuSelectionResult);

        if (operationInProgress)
        {
            error = "La selección 2.1D no admite llamadas reentrantes.";
            return false;
        }

        operationInProgress = true;

        try
        {
            CacheDependenciesIfNeeded();

            if (menuService == null || menuService.CommercialPolicy == null)
            {
                error = "Falta la política comercial de selección.";
                return false;
            }

            if (!BistroBuilderMenuSelectionEvaluator.TrySelectWithScratch(
                    candidates,
                    menuService.CommercialPolicy,
                    context,
                    excludedDishIds,
                    randomSource,
                    selectionScratch,
                    out result,
                    out _,
                    out error
                ))
            {
                return false;
            }

            if (!IsCurrentOfferSnapshot(result.OfferItem, out error))
            {
                result = default(BistroBuilderMenuSelectionResult);
                return false;
            }

            PublishSelection(context, result);
            return true;
        }
        finally
        {
            operationInProgress = false;
        }
    }

    /// <summary>
    /// Selecciona varios platos sin reemplazo. La operación es atómica: el
    /// destino solo cambia cuando se han resuelto todos los elementos.
    /// </summary>
    public bool TrySelectDistinctFromCandidates(
        BistroBuilderMenuSelectionContext baseContext,
        IList<BistroBuilderMenuOfferItemSnapshot> candidates,
        int count,
        ISet<string> excludedDishIds,
        List<BistroBuilderMenuSelectionResult> destination,
        out string error
    )
    {
        if (destination == null)
        {
            error = "El destino de selecciones distintas es nulo.";
            return false;
        }

        if (count < 1)
        {
            error = "Debe solicitarse al menos una selección.";
            return false;
        }

        if (operationInProgress)
        {
            error = "La selección 2.1D no admite llamadas reentrantes.";
            return false;
        }

        operationInProgress = true;

        try
        {
            CacheDependenciesIfNeeded();

            if (menuService == null || menuService.CommercialPolicy == null)
            {
                error = "Falta la política comercial de selección.";
                return false;
            }

            resultBuffer.Clear();
            exclusionBuffer.Clear();

            if (excludedDishIds != null)
            {
                foreach (string dishId in excludedDishIds)
                {
                    string normalized =
                        BistroBuilderMenuIdUtility.NormalizeStableId(dishId);

                    if (BistroBuilderMenuIdUtility.IsValidStableId(normalized))
                    {
                        exclusionBuffer.Add(normalized);
                    }
                }
            }

            for (int index = 0; index < count; index++)
            {
                BistroBuilderMenuSelectionContext context =
                    baseContext.WithOrdinal(
                        baseContext.SelectionOrdinal + index,
                        baseContext.FallbackDisplayOffset
                    );

                if (!BistroBuilderMenuSelectionEvaluator.TrySelectWithScratch(
                        candidates,
                        menuService.CommercialPolicy,
                        context,
                        exclusionBuffer,
                        null,
                        selectionScratch,
                        out BistroBuilderMenuSelectionResult result,
                        out BistroBuilderMenuSelectionFailureReason reason,
                        out error
                    ))
                {
                    if (reason ==
                        BistroBuilderMenuSelectionFailureReason
                            .NoOrderableCandidates)
                    {
                        error = "No hay suficientes candidatos distintos para " +
                                "completar la selección.";
                    }

                    resultBuffer.Clear();
                    return false;
                }

                if (!IsCurrentOfferSnapshot(result.OfferItem, out error))
                {
                    resultBuffer.Clear();
                    return false;
                }

                resultBuffer.Add(result);
                exclusionBuffer.Add(result.DishId);
            }

            destination.Clear();
            destination.AddRange(resultBuffer);

            for (int index = 0; index < resultBuffer.Count; index++)
            {
                BistroBuilderMenuSelectionContext context =
                    baseContext.WithOrdinal(
                        baseContext.SelectionOrdinal + index,
                        baseContext.FallbackDisplayOffset
                    );
                PublishSelection(context, resultBuffer[index]);
            }

            error = string.Empty;
            return true;
        }
        finally
        {
            operationInProgress = false;
        }
    }

    private bool IsCurrentOfferSnapshot(
        BistroBuilderMenuOfferItemSnapshot item,
        out string error
    )
    {
        if (offerService == null)
        {
            error = "Falta la oferta canónica para validar la selección.";
            return false;
        }

        if (item.OfferRevision != offerService.Revision ||
            !string.Equals(
                item.RestaurantId,
                offerService.ActiveRestaurantId,
                StringComparison.Ordinal
            ))
        {
            error = "La selección recibió una oferta obsoleta o de otro " +
                    "restaurante.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public void ResetSelectionSequence()
    {
        SelectionSequence = 0L;
    }

    private void PublishSelection(
        BistroBuilderMenuSelectionContext context,
        BistroBuilderMenuSelectionResult result
    )
    {
        SelectionSequence++;

        BistroBuilderMenuSelectionCompletedEvent change =
            new BistroBuilderMenuSelectionCompletedEvent(
                context,
                result,
                SelectionSequence
            );
        SelectionCompleted?.Invoke(change);

        if (logSelections)
        {
            Debug.Log(
                "Selección 2.1D: " + result.DishId +
                (result.WasSignatureDishAtSelection
                    ? " [firma]"
                    : string.Empty) +
                ". Modalidad: " + context.ServiceMode +
                ". Candidatos: " + result.CandidateCount + ".",
                this
            );
        }
    }

    private void CacheDependenciesIfNeeded()
    {
        if (offerService == null)
        {
            TryGetComponent(out offerService);
        }

        if (menuService == null)
        {
            TryGetComponent(out menuService);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }
#endif
}
