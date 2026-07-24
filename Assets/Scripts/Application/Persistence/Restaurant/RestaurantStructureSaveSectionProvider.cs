using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Primera sección real de partida: reconstruye todos los artículos del
/// modo edición y verifica las relaciones mesa-plaza-silla.
///
/// Las asociaciones de grupos enlazados no se duplican en el archivo:
/// se reconstruyen mediante la topología confirmada y después se comparan
/// con la relación persistida para detectar cualquier divergencia.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Restaurant Structure Save Provider"
)]
public sealed class RestaurantStructureSaveSectionProvider :
    MonoBehaviour,
    IBistroBuilderSaveSectionProvider
{
    public const string StableSectionId = "restaurant.structure";
    public const int StableSectionVersion = 1;

    [Header("Dependencias")]

    [SerializeField]
    private RestaurantPlaceableRegistry placeableRegistry;

    [SerializeField]
    private RestaurantPlaceableLifecycleService lifecycleService;

    [SerializeField]
    private RestaurantPlacementValidationService validationService;

    [SerializeField]
    private RestaurantPlacementHistoryService historyService;

    [SerializeField]
    private RestaurantSeatingTopologyService seatingTopologyService;

    [SerializeField]
    private BistroBuilderSaveDefinitionCatalog definitionCatalog;

    [Header("Rendimiento")]

    [SerializeField]
    [Min(1)]
    private int captureObjectsPerFrame = 64;

    [Header("Depuración")]

    [SerializeField]
    private bool logLoadSummary = true;

    private readonly List<RestaurantPlaceableObject>
        placeableBuffer =
            new List<RestaurantPlaceableObject>(128);

    private readonly List<RestaurantPlaceableSaveRecord>
        loadOrderBuffer =
            new List<RestaurantPlaceableSaveRecord>(128);

    private readonly Dictionary<string, RestaurantPlaceableObject>
        loadedPlaceablesById =
            new Dictionary<string, RestaurantPlaceableObject>(
                StringComparer.Ordinal
            );

    public string SectionId => StableSectionId;

    public int SectionVersion => StableSectionVersion;

    public int LoadOrder => 100;

    public bool IsRequired => true;

    public Type StateType => typeof(RestaurantStructureSaveData);

    public string SerializerId =>
        BistroBuilderJsonSaveSerializer.StableSerializerId;

    private void Awake()
    {
        CacheDependenciesIfNeeded();
    }

    public bool ValidateConfiguration(out string error)
    {
        CacheDependenciesIfNeeded();

        if (placeableRegistry == null)
        {
            error = "Falta RestaurantPlaceableRegistry.";
            return false;
        }

        if (lifecycleService == null)
        {
            error = "Falta RestaurantPlaceableLifecycleService.";
            return false;
        }

        if (validationService == null)
        {
            error = "Falta RestaurantPlacementValidationService.";
            return false;
        }

        if (historyService == null)
        {
            error = "Falta RestaurantPlacementHistoryService.";
            return false;
        }

        if (seatingTopologyService == null)
        {
            error = "Falta RestaurantSeatingTopologyService.";
            return false;
        }

        if (definitionCatalog == null)
        {
            error = "Falta BistroBuilderSaveDefinitionCatalog.";
            return false;
        }

        if (!definitionCatalog.ValidateConfiguration(out error))
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

        RestaurantStructureSaveData data =
            new RestaurantStructureSaveData
            {
                sceneName = SceneManager.GetActiveScene().name
            };

        placeableBuffer.Clear();

        foreach (RestaurantPlaceableObject placeable
                 in placeableRegistry.RegisteredPlaceables)
        {
            if (placeable != null)
            {
                placeableBuffer.Add(placeable);
            }
        }

        placeableBuffer.Sort(ComparePlaceablesByInstanceId);
        data.placeables.Capacity = placeableBuffer.Count;
        data.seatLinks.Capacity = placeableBuffer.Count;

        int safeBatchSize = Mathf.Max(1, captureObjectsPerFrame);

        for (int index = 0;
             index < placeableBuffer.Count;
             index++)
        {
            if (context.IsCancellationRequested)
            {
                context.Fail("La captura fue cancelada.");
                yield break;
            }

            RestaurantPlaceableObject placeable =
                placeableBuffer[index];

            if (placeable.ItemDefinition == null ||
                string.IsNullOrWhiteSpace(
                    placeable.ItemDefinition.ItemId
                ) ||
                string.IsNullOrWhiteSpace(placeable.InstanceId))
            {
                context.Fail(
                    placeable.name +
                    " no tiene identidad o definición persistente."
                );
                yield break;
            }

            data.placeables.Add(
                new RestaurantPlaceableSaveRecord
                {
                    instanceId = NormalizeId(placeable.InstanceId),
                    itemId = NormalizeId(
                        placeable.ItemDefinition.ItemId
                    ),
                    worldPosition =
                        new BistroBuilderSaveVector3(
                            placeable.transform.position
                        ),
                    worldRotation =
                        new BistroBuilderSaveQuaternion(
                            placeable.transform.rotation
                        ),
                    localScale =
                        new BistroBuilderSaveVector3(
                            placeable.transform.localScale
                        )
                }
            );

            if (placeable.TryGetComponent(
                    out RestaurantSeat seat
                ))
            {
                if (!seat.IsAssociated ||
                    seat.AssociatedTable == null)
                {
                    context.Fail(
                        placeable.DisplayName +
                        " no está asociado a una plaza confirmada."
                    );
                    yield break;
                }

                RestaurantPlaceableObject tablePlaceable =
                    seat.AssociatedTable.GetComponent<
                        RestaurantPlaceableObject
                    >();

                if (tablePlaceable == null ||
                    string.IsNullOrWhiteSpace(
                        tablePlaceable.InstanceId
                    ))
                {
                    context.Fail(
                        placeable.DisplayName +
                        " está asociado a una mesa sin identidad."
                    );
                    yield break;
                }

                data.seatLinks.Add(
                    new RestaurantSeatLinkSaveRecord
                    {
                        seatInstanceId = NormalizeId(
                            placeable.InstanceId
                        ),
                        tableInstanceId = NormalizeId(
                            tablePlaceable.InstanceId
                        ),
                        slotIndex = seat.AssociatedSlotIndex
                    }
                );
            }

            if ((index + 1) % safeBatchSize == 0)
            {
                yield return null;
            }
        }

        data.seatLinks.Sort(CompareSeatLinks);
        context.Complete(data);
    }

    public bool ValidateState(
        object state,
        out string error
    )
    {
        error = string.Empty;

        if (!(state is RestaurantStructureSaveData data))
        {
            error = "El estado estructural tiene un tipo incorrecto.";
            return false;
        }

        if (!ValidateConfiguration(out error))
        {
            return false;
        }

        if (data.placeables == null || data.seatLinks == null)
        {
            error = "El estado estructural contiene listas nulas.";
            return false;
        }

        string activeSceneName = SceneManager.GetActiveScene().name;

        if (string.IsNullOrWhiteSpace(data.sceneName) ||
            !string.Equals(
                data.sceneName,
                activeSceneName,
                StringComparison.Ordinal
            ))
        {
            error = "La partida pertenece a la escena " +
                    data.sceneName +
                    " y la escena activa es " +
                    activeSceneName + ".";
            return false;
        }

        HashSet<string> instanceIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> seatIds =
            new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, int> tableCapacities =
            new Dictionary<string, int>(StringComparer.Ordinal);

        for (int index = 0;
             index < data.placeables.Count;
             index++)
        {
            RestaurantPlaceableSaveRecord record =
                data.placeables[index];

            if (record == null ||
                string.IsNullOrWhiteSpace(record.instanceId) ||
                string.IsNullOrWhiteSpace(record.itemId))
            {
                error = "Existe un artículo sin identidad persistente.";
                return false;
            }

            string instanceId = NormalizeId(record.instanceId);
            string itemId = NormalizeId(record.itemId);

            if (!instanceIds.Add(instanceId))
            {
                error = "La identidad " + instanceId + " está duplicada.";
                return false;
            }

            if (!definitionCatalog.TryGetDefinition(
                    itemId,
                    out RestaurantPlaceableItemDefinition definition
                ))
            {
                error = "No existe la definición " + itemId + ".";
                return false;
            }

            if (definition.Prefab == null)
            {
                error = definition.DisplayName +
                        " no tiene prefab de carga.";
                return false;
            }

            if (!record.worldPosition.IsFinite() ||
                !record.worldRotation.HasUsableMagnitude() ||
                !record.localScale.IsFinite())
            {
                error = instanceId +
                        " contiene una transformación no finita.";
                return false;
            }

            Vector3 scale = record.localScale.ToVector3();

            if (Mathf.Abs(scale.x) <= 0.000001f ||
                Mathf.Abs(scale.y) <= 0.000001f ||
                Mathf.Abs(scale.z) <= 0.000001f)
            {
                error = instanceId + " contiene una escala nula.";
                return false;
            }

            if (definition.Prefab.GetComponent<RestaurantSeat>() != null)
            {
                seatIds.Add(instanceId);
            }

            RestaurantTableSeatingConfiguration tableConfiguration =
                definition.Prefab.GetComponent<
                    RestaurantTableSeatingConfiguration
                >();

            if (tableConfiguration != null)
            {
                tableCapacities[instanceId] =
                    tableConfiguration.MaximumCustomers;
            }
        }

        HashSet<string> linkedSeatIds =
            new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> occupiedTableSlots =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0;
             index < data.seatLinks.Count;
             index++)
        {
            RestaurantSeatLinkSaveRecord link = data.seatLinks[index];

            if (link == null ||
                string.IsNullOrWhiteSpace(link.seatInstanceId) ||
                string.IsNullOrWhiteSpace(link.tableInstanceId) ||
                link.slotIndex < 0)
            {
                error = "Existe una relación de asiento inválida.";
                return false;
            }

            string seatId = NormalizeId(link.seatInstanceId);
            string tableId = NormalizeId(link.tableInstanceId);

            if (!seatIds.Contains(seatId))
            {
                error = seatId + " no es una silla persistida.";
                return false;
            }

            if (!tableCapacities.TryGetValue(
                    tableId,
                    out int tableCapacity
                ))
            {
                error = tableId +
                        " no es una mesa con plazas persistibles.";
                return false;
            }

            if (link.slotIndex >= tableCapacity)
            {
                error = "La plaza " + link.slotIndex +
                        " no existe en la mesa " + tableId + ".";
                return false;
            }

            string tableSlotKey =
                tableId + ":" + link.slotIndex;

            if (!occupiedTableSlots.Add(tableSlotKey))
            {
                error = "La plaza " + link.slotIndex +
                        " de " + tableId +
                        " está asignada a más de una silla.";
                return false;
            }

            if (!linkedSeatIds.Add(seatId))
            {
                error = seatId +
                        " tiene más de una asociación persistida.";
                return false;
            }
        }

        if (linkedSeatIds.Count != seatIds.Count)
        {
            error =
                "Todas las sillas deben conservar una relación " +
                "mesa-plaza persistente.";
            return false;
        }

        return true;
    }

    public IEnumerator PrepareForLoad(
        BistroBuilderSaveLoadContext context
    )
    {
        if (!ValidateConfiguration(out string configurationError))
        {
            context.Fail(configurationError);
            yield break;
        }

        historyService.ClearHistory();
        placeableBuffer.Clear();
        loadedPlaceablesById.Clear();

        foreach (RestaurantPlaceableObject placeable
                 in placeableRegistry.RegisteredPlaceables)
        {
            if (placeable != null)
            {
                placeableBuffer.Add(placeable);
            }
        }

        for (int index = 0;
             index < placeableBuffer.Count;
             index++)
        {
            if (context.IsCancellationRequested && !context.IsRollback)
            {
                context.Fail("La carga fue cancelada.");
                yield break;
            }

            RestaurantPlaceableObject placeable =
                placeableBuffer[index];

            if (placeable == null)
            {
                continue;
            }

            placeable.gameObject.SetActive(false);

            if (!lifecycleService.TryPermanentlyDestroyInstance(
                    placeable,
                    out RestaurantPlaceableLifecycleResult result
                ))
            {
                context.Fail(
                    "No se pudo retirar " +
                    placeable.name +
                    ": " +
                    result.Message
                );
                yield break;
            }

            if ((index + 1) % context.ObjectsPerFrame == 0)
            {
                yield return null;
            }
        }

        /*
         * Destroy se completa al final de frame. Esperar evita que los
         * colliders antiguos intervengan en la validación de la carga.
         */
        yield return null;
        Physics.SyncTransforms();

        seatingTopologyService.RebuildImmediately();
    }

    public IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context
    )
    {
        if (!(state is RestaurantStructureSaveData data))
        {
            context.Fail("El estado estructural no puede aplicarse.");
            yield break;
        }

        if (!ValidateState(data, out string validationError))
        {
            context.Fail(validationError);
            yield break;
        }

        loadOrderBuffer.Clear();
        loadOrderBuffer.AddRange(data.placeables);
        loadOrderBuffer.Sort(CompareRecordsForLoad);
        loadedPlaceablesById.Clear();

        for (int index = 0;
             index < loadOrderBuffer.Count;
             index++)
        {
            if (context.IsCancellationRequested && !context.IsRollback)
            {
                context.Fail("La carga fue cancelada.");
                yield break;
            }

            RestaurantPlaceableSaveRecord record =
                loadOrderBuffer[index];

            if (!definitionCatalog.TryGetDefinition(
                    record.itemId,
                    out RestaurantPlaceableItemDefinition definition
                ))
            {
                context.Fail(
                    "No existe la definición " +
                    record.itemId +
                    "."
                );
                yield break;
            }

            Vector3 position = record.worldPosition.ToVector3();
            Quaternion rotation =
                record.worldRotation.ToQuaternion();
            Vector3 scale = record.localScale.ToVector3();

            bool created =
                lifecycleService.TryCreateProvisionalInstance(
                    definition,
                    position,
                    rotation,
                    null,
                    out RestaurantPlaceableObject placeable,
                    out RestaurantPlaceableLifecycleResult
                        creationResult
                );

            if (!created || placeable == null)
            {
                context.Fail(
                    "No se pudo crear " +
                    record.itemId +
                    ": " +
                    creationResult.Message
                );
                yield break;
            }

            string restoredInstanceId =
                NormalizeId(record.instanceId);

            placeable.AssignInstanceId(restoredInstanceId);
            placeable.name = BuildLoadedInstanceName(
                definition,
                restoredInstanceId
            );

            placeable.transform.localScale = scale;
            placeable.transform.SetPositionAndRotation(
                position,
                rotation
            );

            if (!placeable.TryGetComponent(
                    out RestaurantAreaMember member
                ))
            {
                DestroyUnregisteredInstance(placeable);
                context.Fail(
                    placeable.name +
                    " no contiene RestaurantAreaMember."
                );
                yield break;
            }

            Physics.SyncTransforms();

            RestaurantPlacementValidationResult placementResult =
                validationService.ValidateCurrentPlacement(member);

            if (!placementResult.IsValid ||
                placementResult.CandidateArea == null)
            {
                string placementError = BuildPlacementLoadError(
                    placeable,
                    placementResult
                );
                DestroyUnregisteredInstance(placeable);
                context.Fail(placementError);
                yield break;
            }

            member.SetArea(placementResult.CandidateArea);

            RestaurantPlacementStateSnapshot stateSnapshot =
                RestaurantPlacementStateSnapshot.Capture(member);

            if (!lifecycleService.TryActivateInstance(
                    placeable,
                    stateSnapshot,
                    out RestaurantPlaceableLifecycleResult
                        activationResult
                ))
            {
                string displayName = placeable.DisplayName;
                DestroyUnregisteredInstance(placeable);
                context.Fail(
                    "No se pudo activar " +
                    displayName +
                    ": " +
                    activationResult.Message
                );
                yield break;
            }

            loadedPlaceablesById.Add(
                restoredInstanceId,
                placeable
            );

            if (!context.References.TryRegister(
                    BistroBuilderSaveReferenceDomains.RestaurantPlaceable,
                    restoredInstanceId,
                    placeable
                ))
            {
                context.Fail(
                    "La referencia persistente del colocable " +
                    restoredInstanceId + " está duplicada."
                );
                yield break;
            }

            if (placeable.TryGetComponent(
                    out RestaurantTable loadedTable
                ))
            {
                context.References.TryRegister(
                    BistroBuilderSaveReferenceDomains.RestaurantTable,
                    restoredInstanceId,
                    loadedTable
                );
            }

            if (placeable.TryGetComponent(
                    out RestaurantSeat loadedSeat
                ))
            {
                context.References.TryRegister(
                    BistroBuilderSaveReferenceDomains.RestaurantSeat,
                    restoredInstanceId,
                    loadedSeat
                );
            }

            if ((index + 1) % context.ObjectsPerFrame == 0)
            {
                yield return null;
            }
        }

        Physics.SyncTransforms();
        seatingTopologyService.RebuildImmediately();

        if (!ValidateRestoredSeatLinks(
                data,
                out string seatLinkError
            ))
        {
            context.Fail(seatLinkError);
            yield break;
        }
    }

    public void FinalizeLoad(
        BistroBuilderSaveLoadContext context
    )
    {
        if (context.HasFailed)
        {
            return;
        }

        Physics.SyncTransforms();
        seatingTopologyService.RebuildImmediately();
        historyService.ClearHistory();

        if (logLoadSummary)
        {
            Debug.Log(
                nameof(RestaurantStructureSaveSectionProvider) +
                " ha restaurado " +
                loadedPlaceablesById.Count +
                " artículo(s). Rollback: " +
                context.IsRollback +
                ".",
                this
            );
        }
    }

    private bool ValidateRestoredSeatLinks(
        RestaurantStructureSaveData data,
        out string error
    )
    {
        error = string.Empty;

        for (int index = 0;
             index < data.seatLinks.Count;
             index++)
        {
            RestaurantSeatLinkSaveRecord expected =
                data.seatLinks[index];
            string seatId = NormalizeId(expected.seatInstanceId);

            if (!loadedPlaceablesById.TryGetValue(
                    seatId,
                    out RestaurantPlaceableObject seatPlaceable
                ) ||
                seatPlaceable == null ||
                !seatPlaceable.TryGetComponent(
                    out RestaurantSeat seat
                ))
            {
                error = "No se pudo resolver la silla " + seatId + ".";
                return false;
            }

            if (!seat.IsAssociated || seat.AssociatedTable == null)
            {
                error = seatPlaceable.DisplayName +
                        " no recuperó su plaza.";
                return false;
            }

            RestaurantPlaceableObject tablePlaceable =
                seat.AssociatedTable.GetComponent<
                    RestaurantPlaceableObject
                >();

            string actualTableId = tablePlaceable != null
                ? NormalizeId(tablePlaceable.InstanceId)
                : string.Empty;

            if (!string.Equals(
                    actualTableId,
                    NormalizeId(expected.tableInstanceId),
                    StringComparison.Ordinal
                ) ||
                seat.AssociatedSlotIndex != expected.slotIndex)
            {
                error =
                    "La asociación de " +
                    seatPlaceable.DisplayName +
                    " no coincide con la partida guardada.";
                return false;
            }
        }

        return true;
    }

    private int CompareRecordsForLoad(
        RestaurantPlaceableSaveRecord first,
        RestaurantPlaceableSaveRecord second
    )
    {
        int firstWeight = ResolveLoadWeight(first);
        int secondWeight = ResolveLoadWeight(second);
        int weightComparison = firstWeight.CompareTo(secondWeight);

        return weightComparison != 0
            ? weightComparison
            : string.Compare(
                first.instanceId,
                second.instanceId,
                StringComparison.Ordinal
            );
    }

    private int ResolveLoadWeight(
        RestaurantPlaceableSaveRecord record
    )
    {
        if (record == null ||
            !definitionCatalog.TryGetDefinition(
                record.itemId,
                out RestaurantPlaceableItemDefinition definition
            ) ||
            definition.Prefab == null)
        {
            return 10;
        }

        if (definition.Prefab.GetComponent<RestaurantTable>() != null)
        {
            return 0;
        }

        if (definition.Prefab.GetComponent<RestaurantSeat>() != null)
        {
            return 2;
        }

        return 1;
    }

    private void DestroyUnregisteredInstance(
        RestaurantPlaceableObject placeable
    )
    {
        if (placeable == null)
        {
            return;
        }

        lifecycleService.TryPermanentlyDestroyInstance(
            placeable,
            out _
        );
    }

    private static string BuildPlacementLoadError(
        RestaurantPlaceableObject placeable,
        RestaurantPlacementValidationResult result
    )
    {
        string diagnostic =
            !string.IsNullOrWhiteSpace(result.UserMessage)
                ? result.UserMessage
                : !string.IsNullOrWhiteSpace(result.TechnicalMessage)
                    ? result.TechnicalMessage
                    : result.Status.ToString();

        return "La posición guardada de " +
               placeable.DisplayName +
               " ya no es válida: " +
               diagnostic +
               ".";
    }

    private static string BuildLoadedInstanceName(
        RestaurantPlaceableItemDefinition definition,
        string instanceId
    )
    {
        string safeName = definition != null
            ? definition.DisplayName
            : "Artículo";

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return safeName;
        }

        int suffixLength = Mathf.Min(8, instanceId.Length);

        return safeName + "_" +
               instanceId.Substring(0, suffixLength);
    }

    private static int ComparePlaceablesByInstanceId(
        RestaurantPlaceableObject first,
        RestaurantPlaceableObject second
    )
    {
        string firstId = first != null
            ? first.InstanceId
            : string.Empty;
        string secondId = second != null
            ? second.InstanceId
            : string.Empty;

        return string.Compare(
            firstId,
            secondId,
            StringComparison.Ordinal
        );
    }

    private static int CompareSeatLinks(
        RestaurantSeatLinkSaveRecord first,
        RestaurantSeatLinkSaveRecord second
    )
    {
        string firstId = first != null
            ? first.seatInstanceId
            : string.Empty;
        string secondId = second != null
            ? second.seatInstanceId
            : string.Empty;

        return string.Compare(
            firstId,
            secondId,
            StringComparison.Ordinal
        );
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private void CacheDependenciesIfNeeded()
    {
        if (placeableRegistry == null)
        {
            TryGetComponent(out placeableRegistry);
        }

        if (lifecycleService == null)
        {
            TryGetComponent(out lifecycleService);
        }

        if (validationService == null)
        {
            TryGetComponent(out validationService);
        }

        if (historyService == null)
        {
            TryGetComponent(out historyService);
        }

        if (seatingTopologyService == null)
        {
            TryGetComponent(out seatingTopologyService);
        }

        if (definitionCatalog == null)
        {
            TryGetComponent(out definitionCatalog);
        }
    }

#if UNITY_EDITOR
    private void Reset()
    {
        CacheDependenciesIfNeeded();
    }

    private void OnValidate()
    {
        CacheDependenciesIfNeeded();
        captureObjectsPerFrame = Mathf.Max(
            1,
            captureObjectsPerFrame
        );
    }
#endif
}
