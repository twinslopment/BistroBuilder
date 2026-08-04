using System;
using System.Collections.Generic;

/// <summary>
/// Evaluador puro de la política comercial de carta.
///
/// Centraliza límites y reglas para que el servicio activo, la persistencia
/// y la edición transaccional no diverjan.
/// </summary>
public static class BistroBuilderMenuPolicyEvaluator
{
    public static int GetMinimumPriceCents(
        BistroBuilderMenuCommercialPolicy policy
    )
    {
        return policy != null
            ? policy.MinimumPriceCents
            : BistroBuilderMenuCommercialPolicy.DefaultMinimumPriceCents;
    }

    public static int GetMaximumPriceCents(
        BistroBuilderMenuCommercialPolicy policy
    )
    {
        return policy != null
            ? policy.MaximumPriceCents
            : BistroBuilderDishDefinition.MaximumPriceCents;
    }

    public static int GetMaximumMenuItems(
        BistroBuilderMenuCommercialPolicy policy
    )
    {
        return policy != null
            ? policy.MaximumMenuItems
            : int.MaxValue;
    }

    public static int GetMaximumSignatureDishes(
        BistroBuilderMenuCommercialPolicy policy
    )
    {
        return policy != null
            ? policy.MaximumSignatureDishes
            : int.MaxValue;
    }

    public static bool TryValidatePrice(
        int priceCents,
        BistroBuilderMenuCommercialPolicy policy,
        out string error
    )
    {
        int minimum = GetMinimumPriceCents(policy);
        int maximum = GetMaximumPriceCents(policy);

        if (priceCents < minimum || priceCents > maximum)
        {
            error = "El precio debe estar entre " + minimum +
                    " y " + maximum + " céntimos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool TryValidateMenu(
        IList<BistroBuilderMenuItemRuntimeState> items,
        BistroBuilderMenuCommercialPolicy policy,
        out string error
    )
    {
        if (policy != null && !policy.TryValidate(out error))
        {
            return false;
        }

        if (items == null)
        {
            error = "La carta que debe validar la política es nula.";
            return false;
        }

        int maximumMenuItems = GetMaximumMenuItems(policy);

        if (items.Count > maximumMenuItems)
        {
            error = "La carta supera el máximo de " + maximumMenuItems +
                    " platos.";
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        int signatureCount = 0;

        for (int index = 0; index < items.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = items[index];

            if (item == null)
            {
                error = "La política recibió una entrada de carta nula.";
                return false;
            }

            if (!item.TryValidateStructure(out error))
            {
                return false;
            }

            if (!ids.Add(item.DishId))
            {
                error = "La política detectó el DishId duplicado " +
                        item.DishId + ".";
                return false;
            }

            if (!TryValidatePrice(
                    item.CurrentPriceCents,
                    policy,
                    out error
                ))
            {
                error = item.DishId + ": " + error;
                return false;
            }

            if (!item.SignatureDish)
            {
                continue;
            }

            signatureCount++;

            if (!TryValidateSignatureDishState(item, policy, out error))
            {
                return false;
            }
        }

        int maximumSignatureDishes = GetMaximumSignatureDishes(policy);

        if (signatureCount > maximumSignatureDishes)
        {
            error = "La carta supera el máximo de " +
                    maximumSignatureDishes + " platos firma.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool CanAddDish(
        int currentItemCount,
        BistroBuilderMenuCommercialPolicy policy,
        out string error
    )
    {
        int maximum = GetMaximumMenuItems(policy);

        if (currentItemCount < 0)
        {
            error = "El número actual de platos no puede ser negativo.";
            return false;
        }

        if (currentItemCount >= maximum)
        {
            error = "La carta ya contiene el máximo de " + maximum +
                    " platos.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool CanSetSignatureDish(
        IList<BistroBuilderMenuItemRuntimeState> items,
        BistroBuilderMenuItemRuntimeState target,
        bool value,
        BistroBuilderMenuCommercialPolicy policy,
        out BistroBuilderMenuMutationFailureReason failureReason,
        out string error
    )
    {
        if (target == null)
        {
            failureReason =
                BistroBuilderMenuMutationFailureReason.InvalidState;
            error = "No se puede modificar un plato firma nulo.";
            return false;
        }

        if (!value || target.SignatureDish)
        {
            failureReason = BistroBuilderMenuMutationFailureReason.None;
            error = string.Empty;
            return true;
        }

        if (!TryValidateSignatureDishRequirements(target, policy, out error))
        {
            failureReason =
                BistroBuilderMenuMutationFailureReason.PolicyViolation;
            return false;
        }

        int currentSignatureCount = 0;

        if (items != null)
        {
            for (int index = 0; index < items.Count; index++)
            {
                BistroBuilderMenuItemRuntimeState item = items[index];

                if (item != null && item.SignatureDish)
                {
                    currentSignatureCount++;
                }
            }
        }

        int maximum = GetMaximumSignatureDishes(policy);

        if (currentSignatureCount >= maximum)
        {
            failureReason =
                BistroBuilderMenuMutationFailureReason
                    .SignatureLimitReached;
            error = "Ya se ha alcanzado el máximo de " + maximum +
                    " platos firma.";
            return false;
        }

        failureReason = BistroBuilderMenuMutationFailureReason.None;
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Comprueba que una mutación de activación, desbloqueo o servicios no
    /// deje inválido un plato que ya está marcado como firma.
    /// </summary>
    public static bool CanApplySignatureDependentState(
        BistroBuilderMenuItemRuntimeState item,
        bool enabled,
        bool unlocked,
        BistroBuilderMealServiceAvailability availability,
        BistroBuilderMenuCommercialPolicy policy,
        out string error
    )
    {
        if (item == null)
        {
            error = "No se puede validar un plato nulo.";
            return false;
        }

        if (!item.SignatureDish || policy == null)
        {
            error = string.Empty;
            return true;
        }

        if (policy.RequireSignatureDishEnabled && !enabled)
        {
            error = "Un plato firma debe permanecer activo.";
            return false;
        }

        if (policy.RequireSignatureDishUnlocked && !unlocked)
        {
            error = "Un plato firma debe permanecer desbloqueado.";
            return false;
        }

        if (policy.RequireSignatureDishServiceAvailability &&
            availability == BistroBuilderMealServiceAvailability.None)
        {
            error = "Un plato firma debe ofrecerse en al menos un servicio.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateSignatureDishState(
        BistroBuilderMenuItemRuntimeState item,
        BistroBuilderMenuCommercialPolicy policy,
        out string error
    )
    {
        if (!TryValidateSignatureDishRequirements(item, policy, out error))
        {
            error = item.DishId + ": " + error;
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Valida el estado que tendría un plato al convertirse en firma.
    /// No consulta item.SignatureDish porque durante una mutación de alta el
    /// valor todavía es falso. Esto evita aceptar candidatos desactivados,
    /// bloqueados o sin servicios por un falso positivo del estado previo.
    /// </summary>
    private static bool TryValidateSignatureDishRequirements(
        BistroBuilderMenuItemRuntimeState item,
        BistroBuilderMenuCommercialPolicy policy,
        out string error
    )
    {
        if (item == null)
        {
            error = "No se puede validar un plato firma nulo.";
            return false;
        }

        if (policy == null)
        {
            error = string.Empty;
            return true;
        }

        if (policy.RequireSignatureDishEnabled && !item.Enabled)
        {
            error = "Un plato firma debe permanecer activo.";
            return false;
        }

        if (policy.RequireSignatureDishUnlocked && !item.Unlocked)
        {
            error = "Un plato firma debe permanecer desbloqueado.";
            return false;
        }

        if (policy.RequireSignatureDishServiceAvailability &&
            item.AvailableServices ==
                BistroBuilderMealServiceAvailability.None)
        {
            error = "Un plato firma debe ofrecerse en al menos un servicio.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
