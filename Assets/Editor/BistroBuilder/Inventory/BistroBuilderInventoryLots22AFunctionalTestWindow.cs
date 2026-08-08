using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Prueba funcional aislada de 2.2A.
///
/// Crea un inventario temporal en Play Mode para demostrar recepción fechada,
/// FEFO, cambio de día, salida automática por caducidad y round-trip de una
/// reserva con lotes. No toca el inventario real de la partida.
/// </summary>
public sealed class BistroBuilderInventoryLots22AFunctionalTestWindow :
    EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/2.2A Internal Lots, Expiration and FEFO Functional Test";

    private Vector2 scroll;
    private string report =
        "Entra en Play Mode y pulsa Ejecutar prueba funcional 2.2A.";

    [MenuItem(MenuPath, false, 363)]
    private static void OpenWindow()
    {
        GetWindow<BistroBuilderInventoryLots22AFunctionalTestWindow>(
            "Prueba 2.2A"
        );
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "2.2A — Lotes, caducidad diaria y FEFO",
            EditorStyles.boldLabel
        );
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Ejecutar prueba funcional 2.2A", GUILayout.Height(32)))
            {
                RunFunctionalTest();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "La prueba requiere Play Mode. Trabaja sobre un inventario temporal aislado.",
                MessageType.Info
            );
        }

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
    }

    private void RunFunctionalTest()
    {
        var passed = new List<string>();
        var failed = new List<string>();
        GameObject root = null;

        void Check(bool condition, string message)
        {
            if (condition)
            {
                passed.Add(message);
            }
            else
            {
                failed.Add(message);
            }
        }

        try
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject gameSystems =
                BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(
                    scene
                );
            BistroBuilderInventoryService realInventory =
                gameSystems != null
                    ? gameSystems.GetComponent<BistroBuilderInventoryService>()
                    : null;
            BistroBuilderRecipeCatalogService recipes =
                gameSystems != null
                    ? gameSystems.GetComponent<BistroBuilderRecipeCatalogService>()
                    : null;

            if (realInventory == null || recipes == null ||
                realInventory.OpeningStockProfile == null)
            {
                throw new InvalidOperationException(
                    "No están disponibles inventario, recetas y stock inicial."
                );
            }

            long realRevisionBefore = realInventory.RuntimeRevision;

            root = new GameObject("BB_22A_Functional_Runtime");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            root.AddComponent<GameClock>();
            BistroBuilderGeneralGameStateService calendar =
                root.AddComponent<BistroBuilderGeneralGameStateService>();
            BistroBuilderInventoryService inventory =
                root.AddComponent<BistroBuilderInventoryService>();

            SetReference(inventory, "recipeCatalogService", recipes);
            SetReference(
                inventory,
                "openingStockProfile",
                realInventory.OpeningStockProfile
            );
            SetReference(inventory, "generalGameStateService", calendar);
            SetBoolean(inventory, "logInitialization", false);

            root.SetActive(true);
            string error = string.Empty;
            bool initialized = inventory.IsInitialized ||
                inventory.TryInitialize(out error);
            Check(
                initialized && inventory.ValidateRuntimeState(out error),
                "El inventario temporal arranca con lotes válidos."
            );

            List<BistroBuilderInventoryLotSnapshot> lots =
                new List<BistroBuilderInventoryLotSnapshot>();
            inventory.CopyLotSnapshotsTo(lots);
            List<BistroBuilderInventoryLotSnapshot> initialMerluza =
                FilterLots(lots, "ingredient_merluza", true);
            int oldExpiration = FindEarliestExpiration(initialMerluza);
            Check(
                oldExpiration > calendar.DayIndex,
                "La merluza inicial recibe fecha interna y caducidad futura."
            );

            int initialDay = calendar.DayIndex;
            bool advancedOneDay = SetCalendarDay(calendar, initialDay + 1);
            Check(
                advancedOneDay &&
                inventory.LastShelfLifeProcessedDayIndex == calendar.DayIndex,
                "El inventario procesa la caducidad al cambiar de día, no por horas."
            );

            bool received = inventory.TryAddStock(
                "operation_22a_functional_purchase",
                "supplier_22a_functional",
                "ingredient_merluza",
                5000L,
                BistroBuilderInventoryTransactionType.Purchase,
                "Recepción funcional 2.2A.",
                out error
            );
            inventory.CopyLotSnapshotsTo(lots);
            List<BistroBuilderInventoryLotSnapshot> merluzaLots =
                FilterLots(lots, "ingredient_merluza", true);
            int latestExpiration = FindLatestExpiration(merluzaLots);
            Check(
                received && latestExpiration > oldExpiration,
                "Una recepción posterior crea un lote nuevo con caducidad posterior."
            );

            BistroBuilderInventoryLotSnapshot earliestLot =
                FindEarliestAvailableLot(merluzaLots);
            long reserveQuantity = Math.Max(
                1L,
                Math.Min(earliestLot.AvailableCanonicalMilliUnits, 1000L)
            );
            bool reserved = inventory.TryCreateReservation(
                "operation_22a_functional_reserve_oldest",
                "reservation_22a_functional_oldest",
                "functional_22a",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_merluza",
                        reserveQuantity
                    )
                },
                out BistroBuilderInventoryReservationSnapshot reservation,
                out error
            );
            Check(
                reserved && reservation != null &&
                reservation.Lines.Count == 1 &&
                reservation.Lines[0].LotAllocations.Count > 0 &&
                reservation.Lines[0].LotAllocations[0].LotId ==
                    earliestLot.LotId,
                "FEFO asigna primero la existencia que caduca antes."
            );

            bool advancedToExpiry = SetCalendarDay(calendar, oldExpiration);
            inventory.CopyLotSnapshotsTo(lots);
            merluzaLots = FilterLots(lots, "ingredient_merluza", false);
            bool expiredFreeRemoved = true;
            bool reservedCommitmentPreserved = false;
            bool newerSurvives = false;
            for (int index = 0; index < merluzaLots.Count; index++)
            {
                BistroBuilderInventoryLotSnapshot lot = merluzaLots[index];
                if (lot.ExpirationDayIndex > 0 &&
                    lot.ExpirationDayIndex <= calendar.DayIndex &&
                    lot.AvailableCanonicalMilliUnits > 0L)
                {
                    expiredFreeRemoved = false;
                }
                if (lot.LotId == earliestLot.LotId &&
                    lot.ReservedCanonicalMilliUnits == reserveQuantity)
                {
                    reservedCommitmentPreserved = true;
                }
                if (lot.ExpirationDayIndex > calendar.DayIndex &&
                    lot.OnHandCanonicalMilliUnits > 0L)
                {
                    newerSurvives = true;
                }
            }
            Check(
                advancedToExpiry && expiredFreeRemoved &&
                reservedCommitmentPreserved,
                "Al cambiar de día se retira la cantidad libre caducada sin romper una reserva ya aceptada."
            );

            bool released = reserved && inventory.TryReleaseReservation(
                "operation_22a_functional_release_oldest",
                "reservation_22a_functional_oldest",
                "Liberación de reserva que ha alcanzado su caducidad.",
                out error
            );
            inventory.CopyLotSnapshotsTo(lots);
            merluzaLots = FilterLots(lots, "ingredient_merluza", false);
            bool releasedExpiredRemoved = released;
            for (int index = 0; index < merluzaLots.Count; index++)
            {
                BistroBuilderInventoryLotSnapshot lot = merluzaLots[index];
                if (lot.ExpirationDayIndex > 0 &&
                    lot.ExpirationDayIndex <= calendar.DayIndex &&
                    (lot.AvailableCanonicalMilliUnits > 0L ||
                     lot.ReservedCanonicalMilliUnits > 0L))
                {
                    releasedExpiredRemoved = false;
                    break;
                }
            }
            Check(
                releasedExpiredRemoved,
                "Al liberar una reserva ya caducada, su cantidad se retira inmediatamente y no vuelve al stock utilizable."
            );

            inventory.TryGetStockSnapshot(
                "ingredient_merluza",
                out BistroBuilderInventoryStockSnapshot merluzaStock
            );
            Check(
                merluzaStock.ExpiredCanonicalMilliUnits > 0L,
                "El balance acumula separadamente la cantidad retirada por caducidad."
            );

            var transactions =
                new List<BistroBuilderInventoryTransactionSnapshot>();
            inventory.CopyTransactionsTo(transactions);
            bool expirationRecorded = false;
            for (int index = 0; index < transactions.Count; index++)
            {
                if (transactions[index].IngredientId ==
                        "ingredient_merluza" &&
                    transactions[index].TransactionType ==
                        BistroBuilderInventoryTransactionType.Expiration)
                {
                    expirationRecorded = true;
                    break;
                }
            }
            Check(
                expirationRecorded,
                "La caducidad deja una salida específica en el libro de movimientos."
            );

            Check(
                newerSurvives,
                "FEFO/caducidad no elimina una recepción más nueva que aún es apta."
            );

            Check(
                merluzaStock.FreshnessState ==
                    BistroBuilderInventoryFreshnessState.NearExpiry &&
                merluzaStock.NextExpirationDayIndex > calendar.DayIndex,
                "El stock restante expone Próximo a caducar sin penalización de calidad."
            );

            long secondReserveQuantity = Math.Max(
                1L,
                Math.Min(
                    merluzaStock.AvailableCanonicalMilliUnits,
                    1000L
                )
            );
            bool secondReserved = inventory.TryCreateReservation(
                "operation_22a_functional_reserve_after_expiry",
                "reservation_22a_functional_after_expiry",
                "functional_22a",
                new List<BistroBuilderInventoryQuantityLine>
                {
                    new BistroBuilderInventoryQuantityLine(
                        "ingredient_merluza",
                        secondReserveQuantity
                    )
                },
                out BistroBuilderInventoryReservationSnapshot secondReservation,
                out error
            );
            bool avoidsExpiredLots = secondReserved && secondReservation != null;
            if (avoidsExpiredLots)
            {
                IReadOnlyList<BistroBuilderInventoryLotAllocationSnapshot>
                    allocations = secondReservation.Lines[0].LotAllocations;
                for (int index = 0; index < allocations.Count; index++)
                {
                    if (!inventory.TryGetLotSnapshot(
                            allocations[index].LotId,
                            out BistroBuilderInventoryLotSnapshot lot
                        ) ||
                        (lot.ExpirationDayIndex > 0 &&
                         lot.ExpirationDayIndex <= calendar.DayIndex))
                    {
                        avoidsExpiredLots = false;
                        break;
                    }
                }
            }
            Check(
                avoidsExpiredLots,
                "Las reservas posteriores nunca reutilizan lotes ya caducados."
            );

            BistroBuilderInventoryRuntimeSnapshot snapshot = null;
            bool captured = secondReserved &&
                inventory.TryCaptureRuntimeSnapshot(
                    out snapshot,
                    out error
                );
            string json = captured ? JsonUtility.ToJson(snapshot, false) : "";
            BistroBuilderInventoryRuntimeSnapshot roundTrip = captured
                ? JsonUtility.FromJson<BistroBuilderInventoryRuntimeSnapshot>(json)
                : null;
            Check(
                roundTrip != null && roundTrip.TryValidateBasic(out error) &&
                roundTrip.schemaVersion == 2 &&
                roundTrip.lots.Count == inventory.LotCount,
                "menu-independent inventory.canonical v2 conserva lotes en JSON."
            );

            bool restored = roundTrip != null &&
                inventory.TryReplaceFromRuntimeSnapshot(
                    roundTrip,
                    false,
                    out error
                ) &&
                inventory.TryGetReservationSnapshot(
                    "reservation_22a_functional_after_expiry",
                    out BistroBuilderInventoryReservationSnapshot restoredReservation
                ) &&
                restoredReservation.Lines[0].LotAllocations.Count > 0;
            Check(
                restored,
                "Carga restaura también la asignación FEFO de una reserva activa."
            );

            bool releasedAfterRestore = restored &&
                inventory.TryReleaseReservation(
                    "operation_22a_functional_release_after_restore",
                    "reservation_22a_functional_after_expiry",
                    "Cierre de prueba funcional.",
                    out error
                );
            Check(
                releasedAfterRestore,
                "Una reserva restaurada puede cerrarse sobre sus lotes originales."
            );

            inventory.CopyLotSnapshotsTo(lots);
            bool nonExpiringFound = false;
            for (int index = 0; index < lots.Count; index++)
            {
                if (lots[index].IngredientId == "ingredient_agua_cocina" &&
                    lots[index].ExpirationDayIndex == 0 &&
                    lots[index].OnHandCanonicalMilliUnits > 0L)
                {
                    nonExpiringFound = true;
                    break;
                }
            }
            Check(
                nonExpiringFound,
                "Los ingredientes sin vida útil configurada permanecen no caducables."
            );

            Check(
                inventory.ValidateRuntimeState(out error),
                "La auditoría final concilia lotes, reservas, balance y libro."
            );

            Check(
                realInventory.RuntimeRevision == realRevisionBefore,
                "La prueba no modifica el inventario real de la partida."
            );
        }
        catch (Exception exception)
        {
            failed.Add(
                "Excepción inesperada: " + exception.GetType().Name +
                " - " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            if (root != null)
            {
                Object.Destroy(root);
            }
        }

        var builder = new StringBuilder(8192);
        builder.AppendLine(
            failed.Count == 0
                ? "PRUEBA FUNCIONAL 2.2A SUPERADA"
                : "PRUEBA FUNCIONAL 2.2A CON FALLOS"
        );
        builder.AppendLine("Correctos: " + passed.Count);
        builder.AppendLine("Fallos: " + failed.Count);
        for (int index = 0; index < passed.Count; index++)
        {
            builder.AppendLine("- OK: " + passed[index]);
        }
        for (int index = 0; index < failed.Count; index++)
        {
            builder.AppendLine("- ERROR: " + failed[index]);
        }
        report = builder.ToString().TrimEnd();

        if (failed.Count == 0)
        {
            Debug.Log(report);
        }
        else
        {
            Debug.LogError(report);
        }
        Repaint();
    }

    private static List<BistroBuilderInventoryLotSnapshot> FilterLots(
        List<BistroBuilderInventoryLotSnapshot> source,
        string ingredientId,
        bool requireAvailable
    )
    {
        var result = new List<BistroBuilderInventoryLotSnapshot>();
        for (int index = 0; index < source.Count; index++)
        {
            if (source[index].IngredientId == ingredientId &&
                (!requireAvailable ||
                 source[index].AvailableCanonicalMilliUnits > 0L))
            {
                result.Add(source[index]);
            }
        }
        return result;
    }

    private static int FindEarliestExpiration(
        List<BistroBuilderInventoryLotSnapshot> lots
    )
    {
        int result = 0;
        for (int index = 0; index < lots.Count; index++)
        {
            int expiration = lots[index].ExpirationDayIndex;
            if (expiration > 0 && (result == 0 || expiration < result))
            {
                result = expiration;
            }
        }
        return result;
    }

    private static int FindLatestExpiration(
        List<BistroBuilderInventoryLotSnapshot> lots
    )
    {
        int result = 0;
        for (int index = 0; index < lots.Count; index++)
        {
            result = Math.Max(result, lots[index].ExpirationDayIndex);
        }
        return result;
    }

    private static BistroBuilderInventoryLotSnapshot FindEarliestAvailableLot(
        List<BistroBuilderInventoryLotSnapshot> lots
    )
    {
        BistroBuilderInventoryLotSnapshot best = default;
        bool found = false;
        for (int index = 0; index < lots.Count; index++)
        {
            BistroBuilderInventoryLotSnapshot lot = lots[index];
            if (lot.AvailableCanonicalMilliUnits <= 0L ||
                lot.ExpirationDayIndex <= 0)
            {
                continue;
            }

            if (!found || lot.ExpirationDayIndex < best.ExpirationDayIndex ||
                (lot.ExpirationDayIndex == best.ExpirationDayIndex &&
                 string.CompareOrdinal(lot.LotId, best.LotId) < 0))
            {
                best = lot;
                found = true;
            }
        }
        return best;
    }

    private static bool SetCalendarDay(
        BistroBuilderGeneralGameStateService calendar,
        int targetDayIndex
    )
    {
        if (calendar == null || targetDayIndex < 1)
        {
            return false;
        }

        int delta = targetDayIndex - calendar.DayIndex;
        DateTime current = new DateTime(
            calendar.CalendarYear,
            calendar.CalendarMonth,
            calendar.CalendarDay
        );
        DateTime target = current.AddDays(delta);
        return calendar.TrySetCalendar(
            targetDayIndex,
            target.Year,
            target.Month,
            target.Day
        );
    }

    private static void SetReference(
        UnityEngine.Object target,
        string propertyName,
        UnityEngine.Object value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        UnityEngine.Object target,
        string propertyName,
        bool value
    )
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException(
                target.GetType().Name + " no contiene " + propertyName + "."
            );
        }
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
