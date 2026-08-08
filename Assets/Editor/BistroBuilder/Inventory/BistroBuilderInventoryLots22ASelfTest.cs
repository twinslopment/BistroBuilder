using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Autotest 2.2A: contrato persistente v2, migración v1->v2, materialización
/// de lotes y asignación FEFO sin tocar el inventario real de la escena.
/// </summary>
public static class BistroBuilderInventoryLots22ASelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Inventory/Run 2.2A Internal Lots, Expiration and FEFO Self-Test";

    private sealed class TestResult
    {
        public readonly List<string> Passed = new List<string>();
        public readonly List<string> Failed = new List<string>();

        public void Check(bool condition, string message)
        {
            if (condition)
            {
                Passed.Add(message);
            }
            else
            {
                Failed.Add(message);
            }
        }

        public string BuildReport()
        {
            var builder = new StringBuilder(8192);
            builder.AppendLine("BISTRO BUILDER - AUTOTEST 2.2A");
            builder.AppendLine("Pruebas superadas: " + Passed.Count);
            builder.AppendLine("Pruebas fallidas: " + Failed.Count);
            for (int index = 0; index < Passed.Count; index++)
            {
                builder.AppendLine("- OK: " + Passed[index]);
            }
            for (int index = 0; index < Failed.Count; index++)
            {
                builder.AppendLine("- ERROR: " + Failed[index]);
            }
            return builder.ToString().TrimEnd();
        }
    }

    [MenuItem(MenuPath, false, 362)]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog(
                "Bistro Builder",
                "Sal de Play Mode antes de ejecutar el autotest 2.2A.",
                "Aceptar"
            );
            return;
        }

        var result = new TestResult();
        GameObject root = null;

        try
        {
            BistroBuilderInventoryLots22AValidationResult validation =
                BistroBuilderInventoryLots22AValidator
                    .ValidateCurrentProject();
            result.Check(
                validation.ErrorCount == 0,
                "La instalación 2.2A supera el validador estructural."
            );
            result.Check(
                validation.WarningCount == 0,
                "La instalación 2.2A no deja advertencias estructurales."
            );

            result.Check(
                BistroBuilderInventoryRuntimeSnapshot.CurrentSchemaVersion ==
                    2,
                "El snapshot autoritativo del inventario es v2."
            );
            result.Check(
                (int)BistroBuilderInventoryTransactionType.Expiration == 7 &&
                Enum.GetValues(
                    typeof(BistroBuilderInventoryFreshnessState)
                ).Length == 5,
                "Caducidad y los cinco estados de frescura forman parte del dominio."
            );

            root = new GameObject("BB_22A_SelfTest");
            root.hideFlags = HideFlags.HideAndDontSave;
            root.SetActive(false);

            BistroBuilderInventoryStateV1ToV2Migration migration =
                root.AddComponent<BistroBuilderInventoryStateV1ToV2Migration>();
            string legacyJson = BuildLegacyV1Json();
            bool migrated = migration.TryMigrate(
                Encoding.UTF8.GetBytes(legacyJson),
                out byte[] migratedPayload,
                out string error
            );
            result.Check(
                migrated && migratedPayload != null,
                "La migración v1->v2 transforma un inventario histórico válido."
            );

            BistroBuilderInventoryRuntimeSnapshot migratedSnapshot = null;
            if (migrated)
            {
                migratedSnapshot = JsonUtility.FromJson<
                    BistroBuilderInventoryRuntimeSnapshot
                >(Encoding.UTF8.GetString(migratedPayload));
            }
            result.Check(
                migratedSnapshot != null &&
                migratedSnapshot.TryValidateBasic(out error),
                "El payload migrado cumple el contrato inventory.canonical v2."
            );
            result.Check(
                migratedSnapshot != null &&
                migratedSnapshot.requiresLotMaterialization &&
                migratedSnapshot.lots.Count == 0 &&
                migratedSnapshot.stock.Count == 1 &&
                migratedSnapshot.stock[0].expiredCanonicalMilliUnits == 0L,
                "La migración conserva balances y difiere las fechas al calendario cargado."
            );

            Scene scene = SceneManager.GetActiveScene();
            GameObject gameSystems =
                BistroBuilderIngredientsRecipesEditorUtility.FindGameSystems(
                    scene
                );
            BistroBuilderInventoryService installedInventory =
                gameSystems != null
                    ? gameSystems.GetComponent<BistroBuilderInventoryService>()
                    : null;
            BistroBuilderRecipeCatalogService recipes =
                gameSystems != null
                    ? gameSystems.GetComponent<BistroBuilderRecipeCatalogService>()
                    : null;
            BistroBuilderGeneralGameStateService generalState =
                gameSystems != null
                    ? gameSystems.GetComponent<BistroBuilderGeneralGameStateService>()
                    : null;

            BistroBuilderInventoryService service =
                root.AddComponent<BistroBuilderInventoryService>();
            SetReference(service, "recipeCatalogService", recipes);
            SetReference(
                service,
                "openingStockProfile",
                installedInventory != null
                    ? installedInventory.OpeningStockProfile
                    : null
            );
            SetReference(service, "generalGameStateService", generalState);
            SetBoolean(service, "logInitialization", false);

            bool initialized = service.TryInitialize(out error);
            result.Check(
                initialized,
                "Un inventario aislado materializa lotes desde el stock inicial."
            );
            result.Check(
                initialized &&
                service.LotCount ==
                    service.OpeningStockProfile.LineCount,
                "Cada línea de apertura crea un lote interno trazable."
            );

            BistroBuilderInventoryRuntimeSnapshot snapshot = null;
            bool captured = initialized &&
                service.TryCaptureRuntimeSnapshot(out snapshot, out error);
            result.Check(
                captured && snapshot != null &&
                snapshot.lots.Count == service.LotCount &&
                snapshot.TryValidateBasic(out error),
                "El snapshot v2 conserva balances y lotes coherentes."
            );

            string oldLotId = string.Empty;
            long reserveQuantity = 0L;
            bool splitPrepared = captured &&
                TryPrepareFefoSplit(
                    snapshot,
                    generalState != null ? generalState.DayIndex : 1,
                    out oldLotId,
                    out reserveQuantity,
                    out error
                );
            result.Check(
                splitPrepared &&
                service.TryReplaceFromRuntimeSnapshot(
                    snapshot,
                    false,
                    out error
                ),
                "El runtime acepta dos lotes del mismo ingrediente con caducidad distinta."
            );

            BistroBuilderInventoryReservationSnapshot reservation = null;
            string reservationId = "reservation_22a_fefo";
            bool reserved = splitPrepared &&
                service.TryCreateReservation(
                    "operation_22a_fefo_reserve",
                    reservationId,
                    "selftest_22a",
                    new List<BistroBuilderInventoryQuantityLine>
                    {
                        new BistroBuilderInventoryQuantityLine(
                            "ingredient_merluza",
                            reserveQuantity
                        )
                    },
                    out reservation,
                    out error
                );
            result.Check(
                reserved && reservation != null &&
                reservation.Lines.Count == 1 &&
                reservation.Lines[0].LotAllocations.Count > 0 &&
                reservation.Lines[0].LotAllocations[0].LotId == oldLotId,
                "FEFO reserva primero el lote que caduca antes."
            );

            BistroBuilderInventoryRuntimeSnapshot activeSnapshot = null;
            bool activeCaptured = reserved &&
                service.TryCaptureRuntimeSnapshot(
                    out activeSnapshot,
                    out error
                );
            string json = activeCaptured
                ? JsonUtility.ToJson(activeSnapshot, false)
                : string.Empty;
            BistroBuilderInventoryRuntimeSnapshot roundTrip = activeCaptured
                ? JsonUtility.FromJson<BistroBuilderInventoryRuntimeSnapshot>(
                    json
                )
                : null;
            result.Check(
                roundTrip != null &&
                roundTrip.TryValidateBasic(out error) &&
                roundTrip.reservations.Count > 0 &&
                roundTrip.reservations[0].lines[0].lotAllocations.Count > 0,
                "El round-trip JSON conserva la asignación de reserva a lotes."
            );

            bool released = reserved &&
                service.TryReleaseReservation(
                    "operation_22a_fefo_release",
                    reservationId,
                    "Liberación de autotest.",
                    out error
                );
            result.Check(
                released,
                "Liberar una reserva devuelve exactamente sus cantidades a los lotes."
            );

            result.Check(
                service.ValidateRuntimeState(out error),
                "La auditoría final reconstruye balances, lotes, reservas y libro."
            );
        }
        catch (Exception exception)
        {
            result.Failed.Add(
                "Excepción inesperada: " + exception.GetType().Name +
                " - " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        string report = result.BuildReport();
        if (result.Failed.Count > 0)
        {
            Debug.LogError(report);
        }
        else
        {
            Debug.Log(report);
        }
        EditorUtility.DisplayDialog("Bistro Builder", report, "Aceptar");
    }

    private static bool TryPrepareFefoSplit(
        BistroBuilderInventoryRuntimeSnapshot snapshot,
        int currentDay,
        out string oldLotId,
        out long reserveQuantity,
        out string error
    )
    {
        oldLotId = "inventory_lot_90000001";
        reserveQuantity = 0L;
        error = string.Empty;
        if (snapshot == null)
        {
            error = "No existe snapshot para preparar FEFO.";
            return false;
        }

        BistroBuilderInventoryLotSaveRecord original = null;
        int originalIndex = -1;
        for (int index = 0; index < snapshot.lots.Count; index++)
        {
            BistroBuilderInventoryLotSaveRecord lot = snapshot.lots[index];
            if (lot != null && lot.ingredientId == "ingredient_merluza")
            {
                original = lot;
                originalIndex = index;
                break;
            }
        }

        if (original == null || original.onHandCanonicalMilliUnits < 2L ||
            original.reservedCanonicalMilliUnits != 0L)
        {
            error = "La merluza inicial no permite preparar el escenario FEFO.";
            return false;
        }

        long oldQuantity = Math.Max(
            1L,
            original.onHandCanonicalMilliUnits / 2L
        );
        long newQuantity =
            original.onHandCanonicalMilliUnits - oldQuantity;
        if (newQuantity <= 0L)
        {
            error = "No se pudo dividir la existencia de merluza.";
            return false;
        }

        snapshot.lots.RemoveAt(originalIndex);
        snapshot.lots.Add(
            new BistroBuilderInventoryLotSaveRecord
            {
                lotId = oldLotId,
                ingredientId = original.ingredientId,
                sourceId = "selftest_22a_old",
                receivedDayIndex = Math.Max(1, currentDay),
                expirationDayIndex = Math.Max(1, currentDay) + 2,
                onHandCanonicalMilliUnits = oldQuantity,
                reservedCanonicalMilliUnits = 0L,
                revision = original.revision
            }
        );
        snapshot.lots.Add(
            new BistroBuilderInventoryLotSaveRecord
            {
                lotId = "inventory_lot_90000002",
                ingredientId = original.ingredientId,
                sourceId = "selftest_22a_new",
                receivedDayIndex = Math.Max(1, currentDay),
                expirationDayIndex = Math.Max(1, currentDay) + 4,
                onHandCanonicalMilliUnits = newQuantity,
                reservedCanonicalMilliUnits = 0L,
                revision = original.revision
            }
        );
        snapshot.nextLotSequence = 90000003L;
        snapshot.lots.Sort(
            (left, right) => string.CompareOrdinal(
                left != null ? left.lotId : string.Empty,
                right != null ? right.lotId : string.Empty
            )
        );
        reserveQuantity = Math.Max(1L, Math.Min(oldQuantity, 1000L));
        return snapshot.TryValidateBasic(out error);
    }

    private static string BuildLegacyV1Json()
    {
        long ticks = DateTime.UtcNow.Ticks;
        return "{" +
            "\"schemaVersion\":1," +
            "\"nextTransactionSequence\":2," +
            "\"runtimeRevision\":1," +
            "\"stock\":[{" +
                "\"ingredientId\":\"ingredient_merluza\"," +
                "\"storageLocationId\":\"storage_refrigerated\"," +
                "\"onHandCanonicalMilliUnits\":1000," +
                "\"reservedCanonicalMilliUnits\":0," +
                "\"consumedCanonicalMilliUnits\":0," +
                "\"wastedCanonicalMilliUnits\":0," +
                "\"revision\":1}]," +
            "\"reservations\":[]," +
            "\"operations\":[]," +
            "\"ledger\":[{" +
                "\"sequence\":1," +
                "\"transactionId\":\"inventory_tx_00000001\"," +
                "\"operationId\":\"legacy_opening_operation\"," +
                "\"ingredientId\":\"ingredient_merluza\"," +
                "\"transactionType\":0," +
                "\"quantityCanonicalMilliUnits\":1000," +
                "\"onHandDeltaCanonicalMilliUnits\":1000," +
                "\"reservedDeltaCanonicalMilliUnits\":0," +
                "\"previousOnHandCanonicalMilliUnits\":0," +
                "\"newOnHandCanonicalMilliUnits\":1000," +
                "\"previousReservedCanonicalMilliUnits\":0," +
                "\"newReservedCanonicalMilliUnits\":0," +
                "\"sourceId\":\"legacy_opening\"," +
                "\"reason\":\"Migración de prueba\"," +
                "\"timestampUtcTicks\":" + ticks + "}]}";
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
