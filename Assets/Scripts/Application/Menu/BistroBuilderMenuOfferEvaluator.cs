using System;

/// <summary>
/// Evaluador puro de oferta 2.1C.
///
/// Centraliza la prioridad de bloqueos para que mesa, barra, UI y comandas
/// no interpreten por separado activación, franja, modalidad o inventario.
/// </summary>
public static class BistroBuilderMenuOfferEvaluator
{
    public static bool TryEvaluate(
        string restaurantId,
        BistroBuilderMenuItemRuntimeState menuItem,
        BistroBuilderDishDefinition definition,
        BistroBuilderDishAvailabilitySnapshot availability,
        BistroBuilderMenuCommercialPolicy commercialPolicy,
        BistroBuilderMenuOfferContext context,
        int offerRevision,
        out BistroBuilderMenuOfferItemSnapshot snapshot,
        out string error
    )
    {
        snapshot = default(BistroBuilderMenuOfferItemSnapshot);

        if (!context.TryValidate(out error))
        {
            return false;
        }

        string normalizedRestaurantId =
            BistroBuilderMenuIdUtility.NormalizeStableId(restaurantId);

        if (!BistroBuilderMenuIdUtility.IsValidStableId(
                normalizedRestaurantId
            ))
        {
            error = "La oferta necesita un RestaurantId estable.";
            return false;
        }

        if (menuItem == null)
        {
            error = "No puede evaluarse una entrada de carta nula.";
            return false;
        }

        if (!menuItem.TryValidateStructure(out error))
        {
            return false;
        }

        if (definition != null &&
            !string.Equals(
                definition.DishId,
                menuItem.DishId,
                StringComparison.Ordinal
            ))
        {
            error = "La definición no corresponde al DishId de la carta.";
            return false;
        }

        if (!string.Equals(
                availability.DishId,
                menuItem.DishId,
                StringComparison.Ordinal
            ))
        {
            error = "La disponibilidad no corresponde al DishId de la carta.";
            return false;
        }

        if (offerRevision < 0)
        {
            error = "La revisión de oferta no puede ser negativa.";
            return false;
        }

        BistroBuilderMenuOfferBlockFlags flags =
            BistroBuilderMenuOfferBlockFlags.None;

        if (definition == null)
        {
            flags |= BistroBuilderMenuOfferBlockFlags.MissingDefinition;
        }

        if (!menuItem.Unlocked)
        {
            flags |= BistroBuilderMenuOfferBlockFlags.Locked;
        }

        if (!menuItem.Enabled)
        {
            flags |= BistroBuilderMenuOfferBlockFlags.Disabled;
        }

        if (menuItem.ManuallySoldOut)
        {
            flags |= BistroBuilderMenuOfferBlockFlags.ManuallySoldOut;
        }

        if ((menuItem.AvailableServices & context.MealService) == 0)
        {
            flags |=
                BistroBuilderMenuOfferBlockFlags.UnavailableForMealService;
        }

        if (definition != null &&
            !definition.IsAvailableForServiceMode(context.ServiceMode))
        {
            flags |=
                BistroBuilderMenuOfferBlockFlags.UnsupportedServiceMode;
        }

        if (!BistroBuilderMenuPolicyEvaluator.TryValidatePrice(
                menuItem.CurrentPriceCents,
                commercialPolicy,
                out _
            ))
        {
            flags |= BistroBuilderMenuOfferBlockFlags.InvalidPrice;
        }

        switch (availability.State)
        {
            case BistroBuilderDishAvailabilityState.InvalidRecipe:
                flags |= BistroBuilderMenuOfferBlockFlags.InvalidRecipe;
                break;

            case BistroBuilderDishAvailabilityState.OutOfStock:
                flags |= BistroBuilderMenuOfferBlockFlags.OutOfStock;
                break;

            case BistroBuilderDishAvailabilityState.Locked:
                flags |= BistroBuilderMenuOfferBlockFlags.Locked;
                break;

            case BistroBuilderDishAvailabilityState.Disabled:
                flags |= BistroBuilderMenuOfferBlockFlags.Disabled;
                break;

            case BistroBuilderDishAvailabilityState.ManuallyPaused:
                flags |= BistroBuilderMenuOfferBlockFlags.ManuallySoldOut;
                break;

            case BistroBuilderDishAvailabilityState.UnavailableForService:
                flags |= BistroBuilderMenuOfferBlockFlags
                    .UnavailableForMealService;
                break;

            case BistroBuilderDishAvailabilityState.Available:
            case BistroBuilderDishAvailabilityState.LowStock:
                break;

            default:
                flags |=
                    BistroBuilderMenuOfferBlockFlags.AvailabilityUnknown;
                break;
        }

        BistroBuilderMenuOfferRejectionReason primary =
            ResolvePrimaryReason(flags);
        string message = BuildMessage(primary, availability);

        snapshot = new BistroBuilderMenuOfferItemSnapshot(
            normalizedRestaurantId,
            menuItem.DishId,
            definition != null ? definition.DisplayName : menuItem.DishId,
            definition != null ? definition.CategoryId : string.Empty,
            definition != null
                ? definition.Course
                : BistroBuilderDishCourse.Unspecified,
            definition != null
                ? definition.RequiredStation
                : BistroBuilderKitchenStationType.None,
            menuItem.CurrentPriceCents,
            menuItem.DisplayOrder,
            menuItem.SignatureDish,
            context.MealService,
            context.ServiceMode,
            definition != null
                ? definition.AllowedServiceModes
                : BistroBuilderDishServiceModeAvailability.None,
            availability,
            flags,
            primary,
            message,
            offerRevision
        );

        error = string.Empty;
        return true;
    }

    public static BistroBuilderMenuOfferRejectionReason ResolvePrimaryReason(
        BistroBuilderMenuOfferBlockFlags flags
    )
    {
        if ((flags & BistroBuilderMenuOfferBlockFlags.MissingDefinition) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason.MissingDefinition;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags.Locked) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason.Locked;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags.Disabled) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason.Disabled;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags.ManuallySoldOut) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason.ManuallySoldOut;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags.InvalidPrice) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason.InvalidPrice;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags
                .UnavailableForMealService) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason
                .UnavailableForMealService;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags
                .UnsupportedServiceMode) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason
                .UnsupportedServiceMode;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags.InvalidRecipe) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason.InvalidRecipe;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags.OutOfStock) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason.OutOfStock;
        }

        if ((flags & BistroBuilderMenuOfferBlockFlags.AvailabilityUnknown) != 0)
        {
            return BistroBuilderMenuOfferRejectionReason
                .AvailabilityUnknown;
        }

        return BistroBuilderMenuOfferRejectionReason.None;
    }

    public static string BuildMessage(
        BistroBuilderMenuOfferRejectionReason reason,
        BistroBuilderDishAvailabilitySnapshot availability
    )
    {
        switch (reason)
        {
            case BistroBuilderMenuOfferRejectionReason.None:
                return availability.State ==
                    BistroBuilderDishAvailabilityState.LowStock
                        ? availability.Reason
                        : string.Empty;

            case BistroBuilderMenuOfferRejectionReason.MissingDefinition:
                return "No existe la definición canónica del plato.";

            case BistroBuilderMenuOfferRejectionReason.Locked:
                return "El plato todavía no está desbloqueado.";

            case BistroBuilderMenuOfferRejectionReason.Disabled:
                return "El plato está desactivado en la carta.";

            case BistroBuilderMenuOfferRejectionReason.ManuallySoldOut:
                return "El plato está marcado temporalmente como agotado.";

            case BistroBuilderMenuOfferRejectionReason.InvalidPrice:
                return "El precio del plato no cumple la política comercial.";

            case BistroBuilderMenuOfferRejectionReason
                .UnavailableForMealService:
                return "El plato no se ofrece en este servicio del día.";

            case BistroBuilderMenuOfferRejectionReason
                .UnsupportedServiceMode:
                return "El plato no está disponible en esta modalidad de " +
                       "servicio.";

            case BistroBuilderMenuOfferRejectionReason.InvalidRecipe:
                return string.IsNullOrWhiteSpace(availability.Reason)
                    ? "El plato no tiene una receta válida."
                    : availability.Reason;

            case BistroBuilderMenuOfferRejectionReason.OutOfStock:
                return string.IsNullOrWhiteSpace(availability.Reason)
                    ? "El plato está agotado por falta de existencias."
                    : availability.Reason;

            default:
                return "No se pudo determinar la disponibilidad del plato.";
        }
    }
}
