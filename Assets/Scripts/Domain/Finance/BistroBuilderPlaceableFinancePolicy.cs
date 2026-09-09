using System;

public readonly struct BistroBuilderPlaceableDisposalPreview
{
    public RestaurantPlaceableDisposalMode Mode { get; }
    public long AcquisitionCostCents { get; }
    public long ResaleCents { get; }
    public long RemovalCostCents { get; }
    public long NetCashCents { get; }

    public bool HasFinancialEffect =>
        ResaleCents > 0L || RemovalCostCents > 0L;

    public BistroBuilderPlaceableDisposalPreview(
        RestaurantPlaceableDisposalMode mode,
        long acquisitionCostCents,
        long resaleCents,
        long removalCostCents)
    {
        Mode = mode;
        AcquisitionCostCents = acquisitionCostCents;
        ResaleCents = resaleCents;
        RemovalCostCents = removalCostCents;
        NetCashCents = checked(resaleCents - removalCostCents);
    }
}

/// <summary>
/// Reglas puras de compra, reventa y demolición de colocables.
/// No conoce Unity runtime, ledger ni servicios de edición.
/// </summary>
public static class BistroBuilderPlaceableFinancePolicy
{
    public const int DefaultResaleBasisPoints = 5000;
    public const int DefaultDemolitionBasisPoints = 1500;

    public static long ResolvePurchaseCents(
        RestaurantPlaceableItemDefinition definition)
    {
        return definition != null
            ? definition.PurchasePriceCents
            : 0L;
    }

    public static string ResolvePurchaseCategory(
        RestaurantPlaceableItemDefinition definition)
    {
        if (definition == null)
        {
            return "investment.improvement";
        }

        switch (definition.Category)
        {
            case RestaurantPlaceableItemCategory.Furniture:
            case RestaurantPlaceableItemCategory.Seating:
            case RestaurantPlaceableItemCategory.Lighting:
            case RestaurantPlaceableItemCategory.Decoration:
                return "investment.furniture";

            case RestaurantPlaceableItemCategory.KitchenEquipment:
            case RestaurantPlaceableItemCategory.ServiceEquipment:
                return "investment.equipment";

            case RestaurantPlaceableItemCategory.Structural:
                return "investment.renovation";

            default:
                return "investment.improvement";
        }
    }

    public static bool TryBuildDisposalPreview(
        RestaurantPlaceableItemDefinition definition,
        long acquisitionCostCents,
        out BistroBuilderPlaceableDisposalPreview preview,
        out string error)
    {
        preview = default;
        error = string.Empty;

        if (definition == null)
        {
            error = "El colocable no tiene definición económica.";
            return false;
        }

        if (acquisitionCostCents < 0L)
        {
            error = "El coste de adquisición no puede ser negativo.";
            return false;
        }

        RestaurantPlaceableDisposalMode mode =
            ResolveEffectiveDisposalMode(definition);
        long resaleCents = 0L;
        long removalCents = 0L;

        try
        {
            if (mode == RestaurantPlaceableDisposalMode.Resale ||
                mode == RestaurantPlaceableDisposalMode.ResaleWithRemovalCost)
            {
                int basisPoints = definition.ResaleBasisPoints > 0
                    ? definition.ResaleBasisPoints
                    : DefaultResaleBasisPoints;
                resaleCents = RoundBasisPoints(
                    acquisitionCostCents,
                    basisPoints);
            }

            if (mode == RestaurantPlaceableDisposalMode.Demolition)
            {
                if (definition.RemovalCostCents > 0L)
                {
                    removalCents = definition.RemovalCostCents;
                }
                else
                {
                    int basisPoints = definition.DemolitionBasisPoints > 0
                        ? definition.DemolitionBasisPoints
                        : DefaultDemolitionBasisPoints;
                    removalCents = RoundBasisPoints(
                        acquisitionCostCents,
                        basisPoints);
                }
            }
            else if (mode ==
                     RestaurantPlaceableDisposalMode.ResaleWithRemovalCost)
            {
                removalCents = definition.RemovalCostCents;
            }

            preview = new BistroBuilderPlaceableDisposalPreview(
                mode,
                acquisitionCostCents,
                resaleCents,
                removalCents);
            return true;
        }
        catch (OverflowException)
        {
            error = "La valoración económica del colocable queda fuera de rango.";
            return false;
        }
    }

    public static RestaurantPlaceableDisposalMode
        ResolveEffectiveDisposalMode(
            RestaurantPlaceableItemDefinition definition)
    {
        if (definition == null)
        {
            return RestaurantPlaceableDisposalMode.None;
        }

        if (definition.DisposalMode != RestaurantPlaceableDisposalMode.Automatic)
        {
            return definition.DisposalMode;
        }

        return definition.Category == RestaurantPlaceableItemCategory.Structural
            ? RestaurantPlaceableDisposalMode.Demolition
            : RestaurantPlaceableDisposalMode.Resale;
    }

    private static long RoundBasisPoints(long cents, int basisPoints)
    {
        if (cents <= 0L || basisPoints <= 0)
        {
            return 0L;
        }

        long numerator = checked(cents * basisPoints);
        return checked((numerator + 5000L) / 10000L);
    }
}
