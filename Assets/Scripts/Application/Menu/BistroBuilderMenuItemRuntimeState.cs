using System;
using UnityEngine;

/// <summary>
/// Estado modificable de un plato concreto dentro de la carta de una
/// partida. No contiene la definición completa: solo referencia DishId.
/// </summary>
[Serializable]
public sealed class BistroBuilderMenuItemRuntimeState
{
    [SerializeField]
    private string dishId = string.Empty;

    [SerializeField]
    private int currentPriceCents;

    [SerializeField]
    private bool unlocked = true;

    [SerializeField]
    private bool enabled = true;

    [SerializeField]
    private bool manuallySoldOut;

    [SerializeField]
    private bool signatureDish;

    [SerializeField]
    private BistroBuilderMealServiceAvailability availableServices =
        BistroBuilderMealServiceAvailability.All;

    [SerializeField]
    private int displayOrder;

    public string DishId => dishId;

    public int CurrentPriceCents => currentPriceCents;

    public bool Unlocked => unlocked;

    public bool Enabled => enabled;

    public bool ManuallySoldOut => manuallySoldOut;

    public bool SignatureDish => signatureDish;

    public BistroBuilderMealServiceAvailability AvailableServices =>
        availableServices;

    public int DisplayOrder => displayOrder;

    public BistroBuilderMenuItemRuntimeState()
    {
    }

    public BistroBuilderMenuItemRuntimeState(
        string dishId,
        int currentPriceCents,
        bool unlocked,
        bool enabled,
        bool manuallySoldOut,
        bool signatureDish,
        BistroBuilderMealServiceAvailability availableServices,
        int displayOrder
    )
    {
        this.dishId =
            BistroBuilderMenuIdUtility.NormalizeStableId(dishId);
        this.currentPriceCents = currentPriceCents;
        this.unlocked = unlocked;
        this.enabled = enabled;
        this.manuallySoldOut = manuallySoldOut;
        this.signatureDish = signatureDish;
        this.availableServices = availableServices;
        this.displayOrder = displayOrder;
    }

    public static BistroBuilderMenuItemRuntimeState FromDefinition(
        BistroBuilderDishDefinition definition,
        int displayOrder,
        bool enabled,
        bool unlocked
    )
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        return new BistroBuilderMenuItemRuntimeState(
            definition.DishId,
            definition.BasePriceCents,
            unlocked,
            enabled,
            false,
            false,
            definition.DefaultAvailability,
            displayOrder
        );
    }

    public BistroBuilderMenuItemRuntimeState Clone()
    {
        return new BistroBuilderMenuItemRuntimeState(
            dishId,
            currentPriceCents,
            unlocked,
            enabled,
            manuallySoldOut,
            signatureDish,
            availableServices,
            displayOrder
        );
    }

    public bool TryValidate(
        BistroBuilderDishCatalogService catalogService,
        out string error
    )
    {
        if (!BistroBuilderMenuIdUtility.IsValidStableId(dishId))
        {
            error = "La entrada de carta contiene un DishId inválido.";
            return false;
        }

        if (catalogService == null ||
            !catalogService.TryGetDefinition(dishId, out _))
        {
            error = "La carta referencia un plato inexistente: " + dishId + ".";
            return false;
        }

        if (currentPriceCents < 0 ||
            currentPriceCents > BistroBuilderDishDefinition.MaximumPriceCents)
        {
            error = "El precio actual de " + dishId +
                    " queda fuera del rango permitido.";
            return false;
        }

        if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                availableServices,
                true
            ))
        {
            error = "La disponibilidad de " + dishId + " es inválida.";
            return false;
        }

        if (displayOrder < 0)
        {
            error = "El orden de presentación de " + dishId +
                    " no puede ser negativo.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    internal void SetPriceCents(int value)
    {
        currentPriceCents = value;
    }

    internal void SetUnlocked(bool value)
    {
        unlocked = value;
    }

    internal void SetEnabled(bool value)
    {
        enabled = value;
    }

    internal void SetManuallySoldOut(bool value)
    {
        manuallySoldOut = value;
    }

    internal void SetSignatureDish(bool value)
    {
        signatureDish = value;
    }

    internal void SetAvailableServices(
        BistroBuilderMealServiceAvailability value
    )
    {
        availableServices = value;
    }

    internal void SetDisplayOrder(int value)
    {
        displayOrder = value;
    }
}
