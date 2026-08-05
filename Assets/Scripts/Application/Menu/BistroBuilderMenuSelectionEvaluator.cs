using System;
using System.Collections.Generic;

/// <summary>
/// Evaluador puro de selección de platos 2.1D.
///
/// - No consulta inventario ni carta: recibe snapshots ya resueltos por 2.1C.
/// - No duplica candidatos para ponderarlos.
/// - No utiliza UnityEngine.Random.
/// - Conserva el orden histórico cuando todos los pesos son iguales.
/// </summary>
internal sealed class BistroBuilderMenuSelectionScratch
{
    public readonly List<BistroBuilderMenuOfferItemSnapshot> Candidates =
        new List<BistroBuilderMenuOfferItemSnapshot>(32);
    public readonly HashSet<string> UniqueDishIds =
        new HashSet<string>(StringComparer.Ordinal);

    public void Clear()
    {
        Candidates.Clear();
        UniqueDishIds.Clear();
    }
}

public static class BistroBuilderMenuSelectionEvaluator
{
    private sealed class CandidateComparer :
        IComparer<BistroBuilderMenuOfferItemSnapshot>
    {
        public static readonly CandidateComparer Instance =
            new CandidateComparer();

        public int Compare(
            BistroBuilderMenuOfferItemSnapshot first,
            BistroBuilderMenuOfferItemSnapshot second
        )
        {
            int orderComparison =
                first.DisplayOrder.CompareTo(second.DisplayOrder);

            return orderComparison != 0
                ? orderComparison
                : string.Compare(
                    first.DishId,
                    second.DishId,
                    StringComparison.Ordinal
                );
        }
    }

    public static bool TrySelect(
        IList<BistroBuilderMenuOfferItemSnapshot> source,
        BistroBuilderMenuCommercialPolicy policy,
        BistroBuilderMenuSelectionContext context,
        ISet<string> excludedDishIds,
        IBistroBuilderMenuSelectionRandomSource randomSource,
        out BistroBuilderMenuSelectionResult result,
        out BistroBuilderMenuSelectionFailureReason failureReason,
        out string error
    )
    {
        return TrySelectWithScratch(
            source,
            policy,
            context,
            excludedDishIds,
            randomSource,
            new BistroBuilderMenuSelectionScratch(),
            out result,
            out failureReason,
            out error
        );
    }

    internal static bool TrySelectWithScratch(
        IList<BistroBuilderMenuOfferItemSnapshot> source,
        BistroBuilderMenuCommercialPolicy policy,
        BistroBuilderMenuSelectionContext context,
        ISet<string> excludedDishIds,
        IBistroBuilderMenuSelectionRandomSource randomSource,
        BistroBuilderMenuSelectionScratch scratch,
        out BistroBuilderMenuSelectionResult result,
        out BistroBuilderMenuSelectionFailureReason failureReason,
        out string error
    )
    {
        result = default(BistroBuilderMenuSelectionResult);
        failureReason = BistroBuilderMenuSelectionFailureReason.None;

        if (scratch == null)
        {
            failureReason =
                BistroBuilderMenuSelectionFailureReason.InvalidCandidates;
            error = "Falta el buffer de selección reutilizable.";
            return false;
        }

        scratch.Clear();

        if (!context.TryValidate(out error))
        {
            failureReason =
                BistroBuilderMenuSelectionFailureReason.InvalidContext;
            return false;
        }

        if (policy != null)
        {
            if (!policy.TryValidate(out error))
            {
                failureReason =
                    BistroBuilderMenuSelectionFailureReason.InvalidPolicy;
                return false;
            }
        }

        if (source == null)
        {
            failureReason =
                BistroBuilderMenuSelectionFailureReason.InvalidCandidates;
            error = "La colección de candidatos es nula.";
            return false;
        }

        int maximumCandidates = policy != null
            ? policy.MaximumMenuItems
            : BistroBuilderMenuCommercialPolicy.DefaultMaximumMenuItems;

        if (source.Count > maximumCandidates)
        {
            failureReason =
                BistroBuilderMenuSelectionFailureReason.InvalidCandidates;
            error = "La selección supera la capacidad máxima de la carta.";
            return false;
        }

        List<BistroBuilderMenuOfferItemSnapshot> candidates =
            scratch.Candidates;
        HashSet<string> uniqueDishIds = scratch.UniqueDishIds;
        string restaurantId = string.Empty;
        int offerRevision = -1;

        for (int index = 0; index < source.Count; index++)
        {
            BistroBuilderMenuOfferItemSnapshot candidate = source[index];

            if (!candidate.IsOrderable)
            {
                continue;
            }

            if (candidate.MealService != context.MealService ||
                candidate.ServiceMode != context.ServiceMode)
            {
                failureReason =
                    BistroBuilderMenuSelectionFailureReason.InvalidCandidates;
                error = "Un candidato pertenece a otra franja o modalidad.";
                return false;
            }

            string dishId = BistroBuilderMenuIdUtility.NormalizeStableId(
                candidate.DishId
            );
            string candidateRestaurantId =
                BistroBuilderMenuIdUtility.NormalizeStableId(
                    candidate.RestaurantId
                );

            if (!BistroBuilderMenuIdUtility.IsValidStableId(dishId) ||
                !string.Equals(
                    dishId,
                    candidate.DishId,
                    StringComparison.Ordinal
                ))
            {
                failureReason =
                    BistroBuilderMenuSelectionFailureReason.InvalidCandidates;
                error = "Un candidato contiene un DishId inválido.";
                return false;
            }

            if (!BistroBuilderMenuIdUtility.IsValidStableId(
                    candidateRestaurantId
                ) ||
                !string.Equals(
                    candidateRestaurantId,
                    candidate.RestaurantId,
                    StringComparison.Ordinal
                ))
            {
                failureReason =
                    BistroBuilderMenuSelectionFailureReason.InvalidCandidates;
                error = "Un candidato contiene un RestaurantId inválido.";
                return false;
            }

            if (restaurantId.Length == 0)
            {
                restaurantId = candidateRestaurantId;
                offerRevision = candidate.OfferRevision;
            }
            else if (!string.Equals(
                         restaurantId,
                         candidateRestaurantId,
                         StringComparison.Ordinal
                     ) ||
                     offerRevision != candidate.OfferRevision)
            {
                failureReason =
                    BistroBuilderMenuSelectionFailureReason.InvalidCandidates;
                error = "Los candidatos mezclan restaurante o revisión de oferta.";
                return false;
            }

            if (!uniqueDishIds.Add(dishId))
            {
                failureReason =
                    BistroBuilderMenuSelectionFailureReason.DuplicateDishId;
                error = "La selección recibió el DishId duplicado " +
                        dishId + ".";
                return false;
            }

            if (excludedDishIds != null &&
                excludedDishIds.Contains(dishId))
            {
                continue;
            }

            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
        {
            failureReason =
                BistroBuilderMenuSelectionFailureReason
                    .NoOrderableCandidates;
            error = "No existen candidatos pedibles para esta selección.";
            return false;
        }

        candidates.Sort(CandidateComparer.Instance);

        int signatureWeight = policy != null
            ? policy.SignatureSelectionWeightBasisPoints
            : BistroBuilderMenuCommercialPolicy
                .DefaultSignatureSelectionWeightBasisPoints;
        int baseWeight = BistroBuilderMenuCommercialPolicy.BasisPointsPerUnit;
        long totalWeight = 0L;
        int firstWeight = -1;
        bool hasDifferentWeights = false;

        for (int index = 0; index < candidates.Count; index++)
        {
            int weight = candidates[index].SignatureDish
                ? signatureWeight
                : baseWeight;

            if (firstWeight < 0)
            {
                firstWeight = weight;
            }
            else if (firstWeight != weight)
            {
                hasDifferentWeights = true;
            }

            totalWeight += weight;
        }

        if (totalWeight <= 0L)
        {
            failureReason =
                BistroBuilderMenuSelectionFailureReason.InvalidPolicy;
            error = "La suma de pesos de selección no es positiva.";
            return false;
        }

        ulong deterministicSeed = BistroBuilderMenuSelectionSeedUtility.Compute(
            context,
            restaurantId,
            candidates,
            signatureWeight
        );
        bool usedInjectedRandom = randomSource != null;
        int selectedIndex;

        if (!hasDifferentWeights)
        {
            selectedIndex = PositiveModulo(
                context.FallbackDisplayOffset,
                candidates.Count
            );
        }
        else
        {
            ulong raw = randomSource != null
                ? randomSource.NextUInt64()
                : BistroBuilderMenuSelectionDeterministicRandom
                    .NextFromSeed(deterministicSeed);
            ulong target = raw % (ulong)totalWeight;
            ulong cumulative = 0UL;
            selectedIndex = candidates.Count - 1;

            for (int index = 0; index < candidates.Count; index++)
            {
                int weight = candidates[index].SignatureDish
                    ? signatureWeight
                    : baseWeight;
                cumulative += (ulong)weight;

                if (target < cumulative)
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        BistroBuilderMenuOfferItemSnapshot selected =
            candidates[selectedIndex];
        int selectedWeight = selected.SignatureDish
            ? signatureWeight
            : baseWeight;

        result = new BistroBuilderMenuSelectionResult(
            selected,
            candidates.Count,
            selectedWeight,
            totalWeight,
            hasDifferentWeights,
            usedInjectedRandom,
            deterministicSeed
        );
        failureReason = BistroBuilderMenuSelectionFailureReason.None;
        error = string.Empty;
        return true;
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
}
