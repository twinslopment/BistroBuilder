using System;
using System.Collections.Generic;

/// <summary>
/// Motivo tipado por el que no pudo resolverse una elección de plato.
/// </summary>
public enum BistroBuilderMenuSelectionFailureReason
{
    None = 0,
    InvalidContext = 1,
    InvalidCandidates = 2,
    NoOrderableCandidates = 3,
    DuplicateDishId = 4,
    InvalidPolicy = 5
}

/// <summary>
/// Contexto estable de una decisión de cliente.
///
/// No contiene tiempo real ni estado aleatorio global. SelectionReferenceId,
/// CourseIndex y SelectionOrdinal forman una identidad reproducible para que
/// guardar, cargar o repetir un autotest produzca la misma decisión mientras
/// la oferta no cambie.
/// </summary>
public readonly struct BistroBuilderMenuSelectionContext
{
    public BistroBuilderMealServiceAvailability MealService { get; }
    public BistroBuilderServiceMode ServiceMode { get; }
    public string SelectionReferenceId { get; }
    public int CourseIndex { get; }
    public int SelectionOrdinal { get; }
    public int FallbackDisplayOffset { get; }

    public BistroBuilderMenuSelectionContext(
        BistroBuilderMealServiceAvailability mealService,
        BistroBuilderServiceMode serviceMode,
        string selectionReferenceId,
        int courseIndex,
        int selectionOrdinal,
        int fallbackDisplayOffset
    )
    {
        MealService = mealService;
        ServiceMode = serviceMode;
        SelectionReferenceId = BistroBuilderOrderIdUtility.Normalize(
            selectionReferenceId
        );
        CourseIndex = courseIndex;
        SelectionOrdinal = selectionOrdinal;
        FallbackDisplayOffset = fallbackDisplayOffset;
    }

    public bool TryValidate(out string error)
    {
        if (!BistroBuilderMenuOfferContext.IsConcreteMealService(
                MealService
            ))
        {
            error = "La selección necesita desayuno, comida o cena.";
            return false;
        }

        if (!BistroBuilderServiceModeUtility.IsDefined(ServiceMode))
        {
            error = "La selección contiene una modalidad desconocida.";
            return false;
        }

        if (!BistroBuilderOrderIdUtility.IsValid(SelectionReferenceId))
        {
            error = "La selección necesita una referencia estable válida.";
            return false;
        }

        if (!BistroBuilderCourseAndSharingPolicy.IsValidCourseIndex(
                CourseIndex
            ))
        {
            error = "La selección contiene un pase inválido.";
            return false;
        }

        if (SelectionOrdinal < 0 || SelectionOrdinal > 1000000)
        {
            error = "El ordinal de selección queda fuera de rango.";
            return false;
        }

        if (FallbackDisplayOffset < 0 || FallbackDisplayOffset > 1000000)
        {
            error = "El desplazamiento de compatibilidad queda fuera de rango.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public BistroBuilderMenuSelectionContext WithOrdinal(
        int selectionOrdinal,
        int fallbackDisplayOffset
    )
    {
        return new BistroBuilderMenuSelectionContext(
            MealService,
            ServiceMode,
            SelectionReferenceId,
            CourseIndex,
            selectionOrdinal,
            fallbackDisplayOffset
        );
    }
}

/// <summary>
/// Fuente inyectable de enteros aleatorios de 64 bits.
/// El runtime canónico no utiliza UnityEngine.Random.
/// </summary>
public interface IBistroBuilderMenuSelectionRandomSource
{
    ulong NextUInt64();
}

/// <summary>
/// Puerto genérico para que sistemas externos ajusten la preferencia de un
/// plato sin convertirse en autoridad de carta, oferta ni selección.
/// 100 puntos básicos equivalen a +1 % sobre el peso base del candidato.
/// </summary>
public interface IBistroBuilderMenuSelectionWeightProvider
{
    string WeightProviderId { get; }

    bool TryGetWeightAdjustmentBasisPoints(
        BistroBuilderMenuSelectionContext context,
        string dishId,
        out int adjustmentBasisPoints,
        out string error
    );
}

/// <summary>
/// Generador SplitMix64 pequeño, determinista y sin asignaciones por muestra.
/// Se utiliza únicamente para decisiones de carta; no sustituye a otros
/// generadores del juego.
/// </summary>
public sealed class BistroBuilderMenuSelectionDeterministicRandom :
    IBistroBuilderMenuSelectionRandomSource
{
    private ulong state;

    public BistroBuilderMenuSelectionDeterministicRandom(ulong seed)
    {
        state = seed;
    }

    public ulong NextUInt64()
    {
        return NextFromState(ref state);
    }

    public static ulong NextFromSeed(ulong seed)
    {
        return NextFromState(ref seed);
    }

    private static ulong NextFromState(ref ulong currentState)
    {
        unchecked
        {
            currentState += 0x9E3779B97F4A7C15UL;
            ulong value = currentState;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}

/// <summary>
/// Resultado inmutable de una elección ponderada.
/// </summary>
public readonly struct BistroBuilderMenuSelectionResult
{
    public BistroBuilderMenuOfferItemSnapshot OfferItem { get; }
    public int CandidateCount { get; }
    public long EffectiveWeightBasisPoints { get; }
    public long TotalWeightBasisPoints { get; }
    public bool UsedWeightedSelection { get; }
    public bool UsedInjectedRandomSource { get; }
    public ulong DeterministicSeed { get; }

    public string DishId => OfferItem.DishId;
    public bool WasSignatureDishAtSelection => OfferItem.SignatureDish;

    public BistroBuilderMenuSelectionResult(
        BistroBuilderMenuOfferItemSnapshot offerItem,
        int candidateCount,
        long effectiveWeightBasisPoints,
        long totalWeightBasisPoints,
        bool usedWeightedSelection,
        bool usedInjectedRandomSource,
        ulong deterministicSeed
    )
    {
        OfferItem = offerItem;
        CandidateCount = Math.Max(0, candidateCount);
        EffectiveWeightBasisPoints = Math.Max(
            0L,
            effectiveWeightBasisPoints
        );
        TotalWeightBasisPoints = Math.Max(0L, totalWeightBasisPoints);
        UsedWeightedSelection = usedWeightedSelection;
        UsedInjectedRandomSource = usedInjectedRandomSource;
        DeterministicSeed = deterministicSeed;
    }
}

/// <summary>
/// Evento publicado después de una selección completa y válida.
/// </summary>
public readonly struct BistroBuilderMenuSelectionCompletedEvent
{
    public BistroBuilderMenuSelectionContext Context { get; }
    public BistroBuilderMenuSelectionResult Result { get; }
    public long SelectionSequence { get; }

    public BistroBuilderMenuSelectionCompletedEvent(
        BistroBuilderMenuSelectionContext context,
        BistroBuilderMenuSelectionResult result,
        long selectionSequence
    )
    {
        Context = context;
        Result = result;
        SelectionSequence = Math.Max(0L, selectionSequence);
    }
}

/// <summary>
/// Utilidades estables de semilla. No usa GetHashCode porque su resultado no
/// constituye un contrato persistente entre plataformas o versiones.
/// </summary>
public static class BistroBuilderMenuSelectionSeedUtility
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Compute(
        BistroBuilderMenuSelectionContext context,
        string restaurantId,
        IList<BistroBuilderMenuOfferItemSnapshot> orderedCandidates,
        int signatureWeightBasisPoints
    )
    {
        ulong hash = OffsetBasis;
        AddString(ref hash, restaurantId);
        AddInt(ref hash, (int)context.MealService);
        AddInt(ref hash, (int)context.ServiceMode);
        AddString(ref hash, context.SelectionReferenceId);
        AddInt(ref hash, context.CourseIndex);
        AddInt(ref hash, context.SelectionOrdinal);
        AddInt(ref hash, context.FallbackDisplayOffset);
        AddInt(ref hash, signatureWeightBasisPoints);

        if (orderedCandidates != null)
        {
            AddInt(ref hash, orderedCandidates.Count);

            for (int index = 0; index < orderedCandidates.Count; index++)
            {
                BistroBuilderMenuOfferItemSnapshot item =
                    orderedCandidates[index];
                AddString(ref hash, item.DishId);
                AddInt(ref hash, item.DisplayOrder);
                AddInt(ref hash, item.SignatureDish ? 1 : 0);
            }
        }

        // SplitMix64 admite cero, pero separar esa semilla facilita la lectura
        // de diagnósticos y evita confundirla con un valor no inicializado.
        return hash != 0UL ? hash : 0xD1B54A32D192ED03UL;
    }

    private static void AddString(ref ulong hash, string value)
    {
        string normalized = value ?? string.Empty;

        unchecked
        {
            for (int index = 0; index < normalized.Length; index++)
            {
                char character = normalized[index];
                hash ^= (byte)(character & 0xFF);
                hash *= Prime;
                hash ^= (byte)(character >> 8);
                hash *= Prime;
            }

            hash ^= 0xFF;
            hash *= Prime;
        }
    }

    private static void AddInt(ref ulong hash, int value)
    {
        unchecked
        {
            uint raw = (uint)value;

            for (int shift = 0; shift < 32; shift += 8)
            {
                hash ^= (byte)(raw >> shift);
                hash *= Prime;
            }
        }
    }
}
