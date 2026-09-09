using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class BistroBuilderSpatialContractBinding
{
    public string key = string.Empty;
    public BistroBuilderSpatialContractDefinition contract;
}

/// <summary>
/// Vincula nuevas instancias colocables a BBSIS usando identidades y perfiles
/// funcionales ya existentes. No decide creación, seating ni navegación.
/// </summary>
[DisallowMultipleComponent]
public sealed class BistroBuilderSpatialRuntimeBinder : MonoBehaviour
{
    [SerializeField] private RestaurantPlaceableRegistry placeableRegistry;
    [SerializeField] private BistroBuilderSpatialInteractionService spatialService;
    [SerializeField] private List<BistroBuilderSpatialContractBinding> seatContracts =
        new List<BistroBuilderSpatialContractBinding>();
    [SerializeField] private List<BistroBuilderSpatialContractBinding> tableContracts =
        new List<BistroBuilderSpatialContractBinding>();
    [SerializeField] private BistroBuilderSpatialContractDefinition doorContract;

    private void Start()
    {
        ResolveDependencies();
        Subscribe();
        BindExistingPlaceables();
        BindExistingDoors();
        spatialService?.RebuildSubjects();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public bool ValidateConfiguration(out string error)
    {
        ResolveDependencies();
        if (spatialService == null)
        {
            error = "Spatial Runtime Binder necesita BBSIS.";
            return false;
        }
        if (seatContracts.Count == 0 || tableContracts.Count == 0)
        {
            error = "Spatial Runtime Binder necesita contratos de silla y mesa.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    public bool TryBindPlaceable(RestaurantPlaceableObject placeable)
    {
        if (placeable == null || !placeable.HasInstanceId) return false;
        BistroBuilderSpatialSubject existingSubject =
            placeable.GetComponent<BistroBuilderSpatialSubject>();
        string subjectId = existingSubject != null &&
                           !string.IsNullOrWhiteSpace(existingSubject.SubjectId)
            ? existingSubject.SubjectId
            : "spatial.placeable." + placeable.InstanceId;

        RestaurantSeat seat = placeable.GetComponent<RestaurantSeat>();
        if (seat != null && seat.UseProfile != null)
        {
            BistroBuilderSpatialContractDefinition contract =
                FindContract(seatContracts, seat.UseProfile.ProfileId);
            return contract != null && BistroBuilderSpatialBindingUtility.BindSeat(
                seat,
                contract,
                subjectId);
        }

        RestaurantTableSeatingConfiguration table =
            placeable.GetComponent<RestaurantTableSeatingConfiguration>();
        if (table != null && table.Definition != null)
        {
            BistroBuilderSpatialContractDefinition contract =
                FindContract(tableContracts, table.Definition.ConfigurationId);
            return contract != null && BistroBuilderSpatialBindingUtility.BindTable(
                table,
                contract,
                subjectId);
        }
        return false;
    }

    private void HandlePlaceableRegistered(RestaurantPlaceableObject placeable)
    {
        if (!TryBindPlaceable(placeable)) return;
        spatialService?.RebuildSubjects();
    }

    private void BindExistingPlaceables()
    {
        if (placeableRegistry == null) return;
        foreach (RestaurantPlaceableObject placeable in placeableRegistry.RegisteredPlaceables)
            TryBindPlaceable(placeable);
    }

    private void BindExistingDoors()
    {
        if (doorContract == null) return;
        BistroBuilderNavigableDoor[] doors = FindObjectsByType<BistroBuilderNavigableDoor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.InstanceID);
        for (int i = 0; i < doors.Length; i++)
        {
            BistroBuilderNavigableDoor door = doors[i];
            if (door == null) continue;
            BistroBuilderSpatialSubject existing =
                door.GetComponent<BistroBuilderSpatialSubject>();
            if (existing == null || string.IsNullOrWhiteSpace(existing.SubjectId))
                continue;
            BistroBuilderSpatialBindingUtility.BindDoor(
                door,
                doorContract,
                existing.SubjectId);
        }
    }

    private void ResolveDependencies()
    {
        if (placeableRegistry == null)
            placeableRegistry = FindFirstObjectByType<RestaurantPlaceableRegistry>();
        if (spatialService == null)
            spatialService = FindFirstObjectByType<BistroBuilderSpatialInteractionService>();
    }

    private void Subscribe()
    {
        if (placeableRegistry == null) return;
        placeableRegistry.PlaceableRegistered -= HandlePlaceableRegistered;
        placeableRegistry.PlaceableRegistered += HandlePlaceableRegistered;
    }

    private void Unsubscribe()
    {
        if (placeableRegistry != null)
            placeableRegistry.PlaceableRegistered -= HandlePlaceableRegistered;
    }

    private static BistroBuilderSpatialContractDefinition FindContract(
        List<BistroBuilderSpatialContractBinding> bindings,
        string key)
    {
        if (bindings == null || string.IsNullOrWhiteSpace(key)) return null;
        string normalized = key.Trim().ToLowerInvariant();
        for (int i = 0; i < bindings.Count; i++)
        {
            BistroBuilderSpatialContractBinding binding = bindings[i];
            if (binding == null || binding.contract == null) continue;
            if (string.Equals(binding.key, normalized, StringComparison.Ordinal))
                return binding.contract;
        }
        return null;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        IEnumerable<BistroBuilderSpatialContractBinding> seats,
        IEnumerable<BistroBuilderSpatialContractBinding> tables,
        BistroBuilderSpatialContractDefinition door)
    {
        seatContracts.Clear();
        tableContracts.Clear();
        if (seats != null)
        {
            foreach (BistroBuilderSpatialContractBinding binding in seats)
                if (binding != null && binding.contract != null)
                    seatContracts.Add(binding);
        }
        if (tables != null)
        {
            foreach (BistroBuilderSpatialContractBinding binding in tables)
                if (binding != null && binding.contract != null)
                    tableContracts.Add(binding);
        }
        doorContract = door;
    }
#endif
}
