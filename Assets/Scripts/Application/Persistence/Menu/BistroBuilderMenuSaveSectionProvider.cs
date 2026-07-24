using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Proveedor versionado de la sección menu.state.
///
/// Se integra en la plataforma 366 sin conocer archivos ni rutas. La carta
/// se captura y restaura de forma atómica mediante DishId estables.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Persistence/Menu Save Provider")]
public sealed class BistroBuilderMenuSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider,
    IBistroBuilderSaveSectionPhaseOrdering
{
    public const string StableSectionId = "menu.state";
    public const int StableSectionVersion = 1;

    [Header("Dependencias")]

    [SerializeField]
    private BistroBuilderSaveGameService saveGameService;

    [SerializeField]
    private BistroBuilderRestaurantMenuService menuService;

    [SerializeField]
    private BistroBuilderDishCatalogService catalogService;

    [Header("Rendimiento")]

    [SerializeField]
    [Min(1)]
    private int captureItemsPerFrame = 64;

    [Header("Depuración")]

    [SerializeField]
    private bool logLoadSummary = true;

    private readonly List<BistroBuilderMenuItemRuntimeState> captureBuffer =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    public string SectionId => StableSectionId;

    public int SectionVersion => StableSectionVersion;

    public int LoadOrder => 20;

    // Se mantiene opcional para poder abrir partidas 366/366B anteriores.
    public bool IsRequired => false;

    public Type StateType => typeof(BistroBuilderMenuSaveData);

    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    public int PrepareOrder => 20;

    public int ApplyOrder => 20;

    public int FinalizeOrder => 20;

    public BistroBuilderRestaurantMenuService MenuService => menuService;

    public BistroBuilderDishCatalogService CatalogService => catalogService;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (saveGameService == null)
        {
            error = "Falta BistroBuilderSaveGameService.";
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService.";
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        if (menuService == null)
        {
            error = "Falta BistroBuilderRestaurantMenuService.";
            return false;
        }

        if (!menuService.ValidateConfiguration(out error))
        {
            return false;
        }

        error = string.Empty;
        return true;
    }

    public IEnumerator CaptureState(
        BistroBuilderSaveCaptureContext context
    )
    {
        if (!ValidateConfiguration(out string configurationError))
        {
            context.Fail(configurationError);
            yield break;
        }

        if (!menuService.TryGetSnapshot(
                captureBuffer,
                out string snapshotError
            ))
        {
            context.Fail(snapshotError);
            yield break;
        }

        BistroBuilderMenuSaveData data = new BistroBuilderMenuSaveData();
        int batchSize = Mathf.Max(1, captureItemsPerFrame);

        for (int index = 0; index < captureBuffer.Count; index++)
        {
            if (context.IsCancellationRequested)
            {
                context.Fail("La captura de menu.state fue cancelada.");
                yield break;
            }

            BistroBuilderMenuItemRuntimeState item = captureBuffer[index];

            data.items.Add(
                new BistroBuilderMenuItemSaveData
                {
                    dishId = item.DishId,
                    currentPriceCents = item.CurrentPriceCents,
                    unlocked = item.Unlocked,
                    enabled = item.Enabled,
                    manuallySoldOut = item.ManuallySoldOut,
                    signatureDish = item.SignatureDish,
                    availableServices = (int)item.AvailableServices,
                    displayOrder = item.DisplayOrder
                }
            );

            if ((index + 1) % batchSize == 0)
            {
                yield return null;
            }
        }

        context.Complete(data);
    }

    public bool ValidateState(object state, out string error)
    {
        if (!(state is BistroBuilderMenuSaveData data))
        {
            error = "menu.state no tiene el tipo esperado.";
            return false;
        }

        if (data.schemaVersion != StableSectionVersion)
        {
            error = "La versión interna de menu.state no coincide.";
            return false;
        }

        if (data.items == null)
        {
            error = "La lista persistente de carta es nula.";
            return false;
        }

        if (catalogService == null)
        {
            error = "Falta BistroBuilderDishCatalogService para validar menu.state.";
            return false;
        }

        if (!catalogService.ValidateConfiguration(out error))
        {
            return false;
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0; index < data.items.Count; index++)
        {
            BistroBuilderMenuItemSaveData item = data.items[index];

            if (item == null)
            {
                error = "menu.state contiene una entrada nula.";
                return false;
            }

            if (!BistroBuilderMenuIdUtility.IsValidStableId(item.dishId))
            {
                error = "menu.state contiene un DishId inválido.";
                return false;
            }

            if (!ids.Add(item.dishId))
            {
                error = "menu.state contiene el DishId duplicado " +
                        item.dishId + ".";
                return false;
            }

            if (!catalogService.TryGetDefinition(item.dishId, out _))
            {
                error = "menu.state referencia el plato inexistente " +
                        item.dishId + ".";
                return false;
            }

            if (item.currentPriceCents < 0 ||
                item.currentPriceCents >
                    BistroBuilderDishDefinition.MaximumPriceCents)
            {
                error = "menu.state contiene un precio inválido para " +
                        item.dishId + ".";
                return false;
            }

            BistroBuilderMealServiceAvailability availability =
                (BistroBuilderMealServiceAvailability)item.availableServices;

            if (!BistroBuilderMenuIdUtility.IsValidServiceMask(
                    availability,
                    true
                ))
            {
                error = "menu.state contiene servicios inválidos para " +
                        item.dishId + ".";
                return false;
            }

            if (item.displayOrder < 0)
            {
                error = "menu.state contiene un orden negativo para " +
                        item.dishId + ".";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public IEnumerator PrepareForLoad(
        BistroBuilderSaveLoadContext context
    )
    {
        if (!ValidateConfiguration(out string error))
        {
            context.Fail(error);
        }

        yield break;
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context
    )
    {
        if (!ValidateState(state, out string validationError))
        {
            context.Fail(validationError);
            yield break;
        }

        BistroBuilderMenuSaveData data =
            (BistroBuilderMenuSaveData)state;

        List<BistroBuilderMenuItemRuntimeState> replacement =
            new List<BistroBuilderMenuItemRuntimeState>(data.items.Count);

        int batchSize = Mathf.Max(1, context.ObjectsPerFrame);

        for (int index = 0; index < data.items.Count; index++)
        {
            if (context.IsCancellationRequested)
            {
                context.Fail("La aplicación de menu.state fue cancelada.");
                yield break;
            }

            BistroBuilderMenuItemSaveData item = data.items[index];

            replacement.Add(
                new BistroBuilderMenuItemRuntimeState(
                    item.dishId,
                    item.currentPriceCents,
                    item.unlocked,
                    item.enabled,
                    item.manuallySoldOut,
                    item.signatureDish,
                    (BistroBuilderMealServiceAvailability)
                        item.availableServices,
                    item.displayOrder
                )
            );

            if ((index + 1) % batchSize == 0)
            {
                yield return null;
            }
        }

        if (!menuService.TryReplaceAll(
                replacement,
                true,
                out string applyError
            ))
        {
            context.Fail(applyError);
        }
    }

    public void FinalizeLoad(BistroBuilderSaveLoadContext context)
    {
        if (context.HasFailed || !logLoadSummary)
        {
            return;
        }

        Debug.Log(
            "menu.state restaurada con " + menuService.ItemCount +
            " plato(s) y revisión " + menuService.Revision + ".",
            this
        );
    }

    private void CacheDependenciesIfNeeded()
    {
        if (saveGameService == null)
        {
            TryGetComponent(out saveGameService);
        }

        if (catalogService == null)
        {
            TryGetComponent(out catalogService);
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
