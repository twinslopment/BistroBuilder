using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Owns only scene-agent population. No employee records, salaries, shifts,
/// task queues or save section. 4D requests slots before binding; service.runtime
/// requests exact saved WaiterIds before restoring positions and orders.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Bistro Builder/Staff/Waiter Population")]
public sealed class BistroBuilderStaffWaiterPopulation : MonoBehaviour
{
    [SerializeField] private WaiterTaskCoordinator waiterTaskCoordinator;
    [SerializeField, Min(1f)] private float initialSpawnSpacing = 1.15f;

    private readonly HashSet<int> usedIds = new HashSet<int>();
    private readonly List<Waiter> allAgents = new List<Waiter>();
    private readonly List<Waiter> createdInTransaction = new List<Waiter>();
    private readonly List<Waiter> generatedToRetire = new List<Waiter>();

    public bool TryEnsureMinimumSlots(int requiredCount, out string error)
    {
        if (requiredCount < 0)
        {
            error = "El número de plazas operativas no es válido.";
            return false;
        }
        if (!CollectAgents(out error)) return false;
        if (allAgents.Count >= requiredCount) return true;

        Waiter source = FindSource();
        if (source == null)
        {
            error = "No hay un arquetipo Waiter completo con movimiento, comandas, reparto, cuenta y limpieza.";
            return false;
        }

        createdInTransaction.Clear();
        int maxId = MaxExistingId();
        int missing = requiredCount - allAgents.Count;
        for (int i = 0; i < missing; i++)
        {
            if (maxId == int.MaxValue)
            {
                error = "Se ha agotado el rango de WaiterId.";
                RollbackCreated();
                return false;
            }
            maxId++;
            if (!TryCreateAgent(source, maxId, allAgents.Count + i, out Waiter created, out error))
            {
                RollbackCreated();
                return false;
            }
            createdInTransaction.Add(created);
            usedIds.Add(maxId);
        }
        createdInTransaction.Clear();
        error = string.Empty;
        return true;
    }

    /// <summary>
    /// Called inside service.runtime Apply, before order assignment/positions.
    /// Uses saved operational identities, never guesses them from EmployeeIds.
    /// </summary>
    public bool TryReconcileSavedIds(
        IReadOnlyList<BistroBuilderWaiterRuntimeSaveRecord> records,
        out string error)
    {
        if (records == null)
        {
            error = "El guardado no contiene lista de agentes Waiter.";
            return false;
        }
        if (!CollectAgents(out error)) return false;
        var target = new HashSet<int>();
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] == null || records[i].waiterId < 1 ||
                !target.Add(records[i].waiterId))
            {
                error = "El guardado contiene WaiterId inválidos o repetidos.";
                return false;
            }
        }

        generatedToRetire.Clear();
        var missingIds = new List<int>();
        for (int i = 0; i < allAgents.Count; i++)
        {
            Waiter existing = allAgents[i];
            if (!target.Contains(existing.WaiterId) &&
                existing.GetComponent<BistroBuilderStaffGeneratedWaiter>() != null)
                generatedToRetire.Add(existing);
        }
        foreach (int id in target)
            if (!usedIds.Contains(id)) missingIds.Add(id);
        missingIds.Sort();

        if (missingIds.Count > 0)
        {
            Waiter source = FindSource();
            if (source == null)
            {
                error = "Falta el arquetipo operativo para reconstruir los WaiterId guardados.";
                return false;
            }
            createdInTransaction.Clear();
            for (int i = 0; i < missingIds.Count; i++)
            {
                if (!TryCreateAgent(source, missingIds[i], allAgents.Count + i,
                        out Waiter created, out error))
                {
                    RollbackCreated();
                    return false;
                }
                createdInTransaction.Add(created);
            }
            createdInTransaction.Clear();
        }

        // An active-service checkpoint may be loaded while the authored
        // prototype is hidden by the New Game setup. Saved agents must be
        // activated before service.runtime indexes their identities.
        for (int i = 0; i < allAgents.Count; i++)
        {
            Waiter restored = allAgents[i];
            if (restored != null && target.Contains(restored.WaiterId) &&
                !restored.gameObject.activeSelf)
                restored.gameObject.SetActive(true);
        }

        // Only our generated slots can be retired. An authored scene archetype
        // is never destroyed by loading an older or smaller roster.
        for (int i = 0; i < generatedToRetire.Count; i++)
        {
            Waiter extra = generatedToRetire[i];
            if (extra == null) continue;
            if (waiterTaskCoordinator != null)
                waiterTaskCoordinator.UnregisterWaiter(extra);
            extra.gameObject.SetActive(false);
            Destroy(extra.gameObject);
        }
        generatedToRetire.Clear();
        error = string.Empty;
        return true;
    }

    public void ApplyBoundVisibility(BistroBuilderStaffSessionSnapshot snapshot)
    {
        var assigned = new HashSet<int>();
        if (snapshot != null && snapshot.active && snapshot.bindings != null)
            for (int i = 0; i < snapshot.bindings.Count; i++)
                if (snapshot.bindings[i] != null)
                    assigned.Add(snapshot.bindings[i].waiterId);

        Waiter[] sceneAgents = FindObjectsByType<Waiter>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < sceneAgents.Length; i++)
        {
            Waiter waiter = sceneAgents[i];
            if (waiter == null) continue;
            BistroBuilderStaffWaiterVisualPresence visibility =
                waiter.GetComponent<BistroBuilderStaffWaiterVisualPresence>();
            if (visibility == null)
                visibility = waiter.gameObject.AddComponent<BistroBuilderStaffWaiterVisualPresence>();
            visibility.SetPresent(assigned.Contains(waiter.WaiterId));
        }
    }

    private bool CollectAgents(out string error)
    {
        if (waiterTaskCoordinator == null)
            TryGetComponent(out waiterTaskCoordinator);
        if (waiterTaskCoordinator == null)
        {
            error = "Falta el coordinador canónico de camareros.";
            return false;
        }

        allAgents.Clear();
        usedIds.Clear();
        Waiter[] current = FindObjectsByType<Waiter>(
            FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < current.Length; i++)
        {
            Waiter waiter = current[i];
            if (waiter == null || waiter.WaiterId < 1 ||
                !usedIds.Add(waiter.WaiterId))
            {
                error = "La escena ya contiene un WaiterId inválido o duplicado.";
                return false;
            }
            allAgents.Add(waiter);
        }
        error = string.Empty;
        return true;
    }

    private Waiter FindSource()
    {
        for (int i = 0; i < allAgents.Count; i++)
        {
            Waiter source = allAgents[i];
            if (source != null &&
                source.GetComponent<BistroBuilderStaffGeneratedWaiter>() == null &&
                source.GetComponent<WaiterMovementView>() != null &&
                source.GetComponent<WaiterTableServiceFlow>() != null &&
                source.GetComponent<FoodDeliveryServiceFlow>() != null &&
                source.GetComponent<BillServiceFlow>() != null &&
                source.GetComponent<TableCleaningServiceFlow>() != null)
                return source;
        }
        return null;
    }

    private int MaxExistingId()
    {
        int max = 0;
        foreach (int id in usedIds)
            if (id > max) max = id;
        return max;
    }

    private bool TryCreateAgent(Waiter source, int waiterId, int spawnOrdinal,
        out Waiter result, out string error)
    {
        result = null;
        error = string.Empty;
        if (source == null || source.GetComponent<BistroBuilderStaffGeneratedWaiter>() != null)
        {
            error = "Solo puede clonarse un arquetipo de escena no generado.";
            return false;
        }

        GameObject template = source.gameObject;
        bool active = template.activeSelf;
        GameObject clone = null;
        try
        {
            // Inactive cloning prevents duplicate IDs from ever reaching
            // OnEnable, navigation or WaiterTaskCoordinator before provisioning.
            template.SetActive(false);
            clone = Instantiate(template, template.transform.parent);
            clone.name = "StaffWaiter_" + waiterId;
            if (clone.GetComponent<BistroBuilderStaffGeneratedWaiter>() == null)
                clone.AddComponent<BistroBuilderStaffGeneratedWaiter>();
            Waiter next = clone.GetComponent<Waiter>();
            if (next == null || !next.TryConfigureGeneratedIdentity(waiterId) ||
                !next.TrySetStaffServiceEligibility(false))
            {
                error = "No se pudo configurar el WaiterId antes de activar el agente.";
                return false;
            }

            // Animation's persistent actor name must follow the freshly
            // provisioned WaiterId; cloning an explicit prototype actorId
            // would otherwise collide even though the WaiterIds are unique.
            BistroBuilderAnimationActorBinding actor =
                clone.GetComponent<BistroBuilderAnimationActorBinding>();
            if (actor != null)
                actor.ConfigureRuntime("waiter:" + waiterId,
                    actor.Driver, actor.RecipePlayer, actor.RigAdapter, actor.CarryPresenter);

            var templateVisibility =
                source.GetComponent<BistroBuilderStaffWaiterVisualPresence>();
            if (templateVisibility == null)
                templateVisibility = source.gameObject.AddComponent<BistroBuilderStaffWaiterVisualPresence>();
            var cloneVisibility =
                clone.GetComponent<BistroBuilderStaffWaiterVisualPresence>();
            if (cloneVisibility == null)
                cloneVisibility = clone.AddComponent<BistroBuilderStaffWaiterVisualPresence>();
            if (!cloneVisibility.TryAdoptBaseline(templateVisibility))
            {
                error = "El arquetipo visual del camarero no coincide con el clon.";
                return false;
            }

            Transform anchor = source.transform;
            Vector3 offset = anchor.right * (initialSpawnSpacing * (spawnOrdinal + 1));
            clone.transform.SetPositionAndRotation(anchor.position + offset, anchor.rotation);
            // Restore the scene archetype before activating the newly configured slot.
            template.SetActive(active);
            clone.SetActive(true);
            if (!clone.activeInHierarchy)
            {
                error = "El nuevo camarero depende de un padre inactivo; no puede ofrecer un agente funcional.";
                return false;
            }
            if (!waiterTaskCoordinator.RegisterWaiter(next))
            {
                error = "El coordinador de servicio rechazó el nuevo camarero.";
                return false;
            }
            result = next;
            return true;
        }
        catch (Exception exception)
        {
            error = "No se pudo crear el agente operativo: " + exception.Message;
            return false;
        }
        finally
        {
            template.SetActive(active);
            if (result == null && clone != null)
            {
                clone.SetActive(false);
                Destroy(clone);
            }
        }
    }

    private void RollbackCreated()
    {
        for (int i = 0; i < createdInTransaction.Count; i++)
        {
            Waiter waiter = createdInTransaction[i];
            if (waiter == null) continue;
            waiterTaskCoordinator?.UnregisterWaiter(waiter);
            waiter.gameObject.SetActive(false);
            Destroy(waiter.gameObject);
        }
        createdInTransaction.Clear();
    }
}
