using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Autotest determinista de la plataforma de persistencia.
///
/// Prueba serialización, checksums, generaciones inmutables,
/// recuperación tras corrupción, borrado y configuración de escena.
/// Nunca escribe dentro de la carpeta real de partidas del jugador.
/// </summary>
public static class BistroBuilderPersistenceFoundationSelfTest
{
    private const string MenuPath =
        "Tools/Bistro Builder/Persistence/Run Save Foundation Self-Test";

    [MenuItem(MenuPath, false, 120)]
    private static void Run()
    {
        SaveSelfTestReport report = new SaveSelfTestReport();
        string temporaryRoot = Path.GetFullPath(
            Path.Combine(
                "Library",
                "BistroBuilderPersistenceSelfTest_" +
                Guid.NewGuid().ToString("N")
            )
        );

        try
        {
            RunStorageTests(temporaryRoot, report);
            RunSceneTests(report);
        }
        catch (Exception exception)
        {
            report.Fail(
                "Excepción no controlada: " + exception.Message
            );
            Debug.LogException(exception);
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, true);
                }
            }
            catch (Exception cleanupException)
            {
                report.Fail(
                    "No se pudo limpiar la carpeta temporal: " +
                    cleanupException.Message
                );
            }
        }

        string output = report.BuildReport();

        if (report.FailedCount > 0)
        {
            Debug.LogError(output);
        }
        else
        {
            Debug.Log(output);
        }

        EditorUtility.DisplayDialog(
            "Bistro Builder",
            output,
            "Aceptar"
        );
    }

    private static void RunStorageTests(
        string temporaryRoot,
        SaveSelfTestReport report
    )
    {
        BistroBuilderJsonSaveSerializer serializer =
            new BistroBuilderJsonSaveSerializer();

        report.Check(
            serializer.SerializerId == "unity-json-v1",
            "El serializador expone una identidad estable."
        );
        report.Check(
            serializer.FileExtension == ".json",
            "El serializador expone la extensión JSON."
        );

        PersistenceSelfTestData sample =
            new PersistenceSelfTestData
            {
                number = 42,
                text = "Bistro Builder"
            };

        byte[] serialized = serializer.Serialize(sample, true);
        PersistenceSelfTestData deserialized =
            (PersistenceSelfTestData)serializer.Deserialize(
                serialized,
                typeof(PersistenceSelfTestData)
            );

        report.Check(
            deserialized != null,
            "El JSON puede deserializarse."
        );
        report.Check(
            deserialized != null && deserialized.number == 42,
            "El JSON conserva valores numéricos."
        );
        report.Check(
            deserialized != null &&
            deserialized.text == "Bistro Builder",
            "El JSON conserva cadenas."
        );

        RestaurantStructureSaveData structureSample =
            new RestaurantStructureSaveData
            {
                sceneName = "Prototype_Restaurant"
            };
        structureSample.placeables.Add(
            new RestaurantPlaceableSaveRecord
            {
                instanceId = "chair_test",
                itemId = "chair_bistro_01",
                worldPosition =
                    new BistroBuilderSaveVector3(
                        new Vector3(1f, 0f, 2f)
                    ),
                worldRotation =
                    new BistroBuilderSaveQuaternion(
                        Quaternion.Euler(0f, 90f, 0f)
                    ),
                localScale =
                    new BistroBuilderSaveVector3(Vector3.one)
            }
        );
        structureSample.seatLinks.Add(
            new RestaurantSeatLinkSaveRecord
            {
                seatInstanceId = "chair_test",
                tableInstanceId = "table_test",
                slotIndex = 1
            }
        );

        RestaurantStructureSaveData structureRoundTrip =
            (RestaurantStructureSaveData)serializer.Deserialize(
                serializer.Serialize(structureSample, false),
                typeof(RestaurantStructureSaveData)
            );

        report.Check(
            structureRoundTrip != null &&
            structureRoundTrip.placeables.Count == 1,
            "El DTO estructural completa un round-trip JSON."
        );
        report.Check(
            structureRoundTrip != null &&
            structureRoundTrip.placeables[0].instanceId ==
                "chair_test" &&
            structureRoundTrip.placeables[0].itemId ==
                "chair_bistro_01",
            "El DTO conserva identidades de instancia y definición."
        );
        report.Check(
            structureRoundTrip != null &&
            structureRoundTrip.seatLinks.Count == 1 &&
            structureRoundTrip.seatLinks[0].slotIndex == 1,
            "El DTO conserva la relación mesa-plaza-silla."
        );
        report.Check(
            structureRoundTrip != null &&
            structureRoundTrip.placeables[0]
                .worldRotation.HasUsableMagnitude(),
            "El DTO conserva una rotación válida."
        );

        string firstHash =
            BistroBuilderFileSaveStorage.ComputeSha256(serialized);
        string repeatedHash =
            BistroBuilderFileSaveStorage.ComputeSha256(serialized);
        byte[] differentPayload = new byte[serialized.Length + 1];
        Buffer.BlockCopy(
            serialized,
            0,
            differentPayload,
            0,
            serialized.Length
        );
        differentPayload[differentPayload.Length - 1] = 1;
        string differentHash =
            BistroBuilderFileSaveStorage.ComputeSha256(
                differentPayload
            );

        report.Check(
            firstHash.Length == 64,
            "SHA-256 genera 64 caracteres hexadecimales."
        );
        report.Check(
            firstHash == repeatedHash,
            "El checksum es determinista."
        );
        report.Check(
            firstHash != differentHash,
            "El checksum detecta contenido diferente."
        );
        report.Check(
            BistroBuilderFileSaveStorage.BuildSlotFolderName(1) ==
                "slot_001",
            "Los nombres de slot son estables y ordenables."
        );

        BistroBuilderFileSaveStorage storage =
            new BistroBuilderFileSaveStorage(
                temporaryRoot,
                serializer,
                3,
                true
            );

        report.Check(
            Path.IsPathRooted(storage.RootPath),
            "La ruta de almacenamiento se normaliza a absoluta."
        );

        BistroBuilderStorageWriteResult unsafeIdWrite =
            storage.WriteGenerationAsync(
                    new BistroBuilderStorageWriteRequest(
                        2,
                        "ID insegura",
                        "test",
                        "Prototype_Restaurant",
                        new List<BistroBuilderSerializedSaveSection>
                        {
                            new BistroBuilderSerializedSaveSection(
                                "../escape",
                                1,
                                serializer.SerializerId,
                                serializer.FileExtension,
                                serialized
                            )
                        }
                    ),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            !unsafeIdWrite.Succeeded,
            "El storage rechaza identidades de sección inseguras."
        );

        BistroBuilderStorageWriteResult unsafeExtensionWrite =
            storage.WriteGenerationAsync(
                    new BistroBuilderStorageWriteRequest(
                        2,
                        "Extensión insegura",
                        "test",
                        "Prototype_Restaurant",
                        new List<BistroBuilderSerializedSaveSection>
                        {
                            new BistroBuilderSerializedSaveSection(
                                "test.section",
                                1,
                                serializer.SerializerId,
                                ".json/../escape",
                                serialized
                            )
                        }
                    ),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            !unsafeExtensionWrite.Succeeded,
            "El storage rechaza extensiones de archivo inseguras."
        );
        report.Check(
            !storage.SlotExists(2),
            "Una escritura inválida no confirma ningún slot."
        );

        bool retentionWritesSucceeded = true;

        for (int generationIndex = 0;
             generationIndex < 5;
             generationIndex++)
        {
            BistroBuilderStorageWriteResult retentionWrite =
                storage.WriteGenerationAsync(
                        new BistroBuilderStorageWriteRequest(
                            2,
                            "Retención",
                            "test",
                            "Prototype_Restaurant",
                            new List<
                                BistroBuilderSerializedSaveSection
                            >
                            {
                                new BistroBuilderSerializedSaveSection(
                                    "test.section",
                                    1,
                                    serializer.SerializerId,
                                    serializer.FileExtension,
                                    serializer.Serialize(
                                        new PersistenceSelfTestData
                                        {
                                            number = generationIndex,
                                            text = "retention"
                                        },
                                        false
                                    )
                                )
                            }
                        ),
                        CancellationToken.None
                    )
                    .GetAwaiter()
                    .GetResult();

            retentionWritesSucceeded &= retentionWrite.Succeeded;
        }

        report.Check(
            retentionWritesSucceeded,
            "Las generaciones sucesivas se escriben correctamente."
        );

        string retentionGenerationsPath = Path.Combine(
            storage.GetSlotPath(2),
            "generations"
        );
        int retainedGenerationCount =
            Directory.Exists(retentionGenerationsPath)
                ? Directory.GetDirectories(retentionGenerationsPath)
                    .Length
                : 0;

        report.Check(
            retainedGenerationCount <= 3,
            "La retención elimina generaciones antiguas no protegidas."
        );

        bool retentionSlotDeleted =
            storage.DeleteSlotAsync(
                    2,
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            retentionSlotDeleted && !storage.SlotExists(2),
            "El slot auxiliar de retención se elimina por completo."
        );

        BistroBuilderStorageWriteResult pointerRecoveryWrite =
            storage.WriteGenerationAsync(
                    new BistroBuilderStorageWriteRequest(
                        3,
                        "Recuperación de puntero",
                        "test",
                        "Prototype_Restaurant",
                        new List<BistroBuilderSerializedSaveSection>
                        {
                            new BistroBuilderSerializedSaveSection(
                                "test.section",
                                1,
                                serializer.SerializerId,
                                serializer.FileExtension,
                                serialized
                            )
                        }
                    ),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            pointerRecoveryWrite.Succeeded,
            "Se crea una generación para probar el puntero corrupto."
        );

        string pointerRecoveryPath = Path.Combine(
            storage.GetSlotPath(3),
            "current.json"
        );
        BistroBuilderSaveSlotPointer corruptedPointer =
            (BistroBuilderSaveSlotPointer)serializer.Deserialize(
                File.ReadAllBytes(pointerRecoveryPath),
                typeof(BistroBuilderSaveSlotPointer)
            );
        corruptedPointer.manifestSha256 = new string('0', 64);
        File.WriteAllBytes(
            pointerRecoveryPath,
            serializer.Serialize(corruptedPointer, true)
        );

        report.Check(
            File.Exists(pointerRecoveryPath),
            "El autotest ha alterado el checksum del puntero actual."
        );

        BistroBuilderStorageReadResult pointerRecoveredRead =
            storage.ReadLatestValidGenerationAsync(
                    3,
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            pointerRecoveredRead.Succeeded &&
            pointerRecoveredRead.Package.RecoveredFromFallback &&
            pointerRecoveredRead.Package.Manifest.generationId ==
                pointerRecoveryWrite.GenerationId,
            "El escaneo recupera la generación aunque el puntero " +
            "tenga un checksum corrupto."
        );

        bool pointerRecoverySlotDeleted =
            storage.DeleteSlotAsync(
                    3,
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            pointerRecoverySlotDeleted && !storage.SlotExists(3),
            "El slot auxiliar de recuperación se elimina por completo."
        );

        List<BistroBuilderSerializedSaveSection> sections =
            new List<BistroBuilderSerializedSaveSection>
            {
                new BistroBuilderSerializedSaveSection(
                    "test.section",
                    1,
                    serializer.SerializerId,
                    serializer.FileExtension,
                    serialized
                )
            };

        BistroBuilderStorageWriteResult firstWrite =
            storage.WriteGenerationAsync(
                    new BistroBuilderStorageWriteRequest(
                        1,
                        "Prueba 1",
                        "test",
                        "Prototype_Restaurant",
                        sections
                    ),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            firstWrite.Succeeded,
            "La primera generación se escribe correctamente."
        );
        report.Check(
            !string.IsNullOrWhiteSpace(firstWrite.GenerationId),
            "La generación recibe una identidad única."
        );
        report.Check(
            firstWrite.PayloadBytes > 0,
            "La escritura informa del payload real."
        );
        report.Check(
            storage.SlotExists(1),
            "El slot existe tras confirmar el puntero actual."
        );

        string slotPath = storage.GetSlotPath(1);
        string firstGenerationPath = Path.Combine(
            slotPath,
            "generations",
            firstWrite.GenerationId
        );

        report.Check(
            File.Exists(Path.Combine(slotPath, "current.json")),
            "Existe el puntero current.json."
        );
        report.Check(
            Directory.Exists(firstGenerationPath),
            "La generación confirmada es inmutable y completa."
        );
        report.Check(
            File.Exists(
                Path.Combine(firstGenerationPath, "manifest.json")
            ),
            "La generación contiene manifest.json."
        );
        report.Check(
            Directory.Exists(
                Path.Combine(firstGenerationPath, "sections")
            ),
            "La generación contiene secciones segmentadas."
        );

        BistroBuilderStorageReadResult firstRead =
            storage.ReadLatestValidGenerationAsync(
                    1,
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            firstRead.Succeeded && firstRead.Package != null,
            "La generación puede leerse y verificarse."
        );
        report.Check(
            firstRead.Succeeded &&
            firstRead.Package.Manifest.slotIndex == 1,
            "El manifiesto conserva el índice del slot."
        );
        report.Check(
            firstRead.Succeeded &&
            firstRead.Package.Manifest.slotDisplayName == "Prueba 1",
            "El manifiesto conserva el nombre visible."
        );
        report.Check(
            firstRead.Succeeded &&
            firstRead.Package.Sections.Count == 1,
            "La lectura recupera exactamente una sección."
        );
        report.Check(
            firstRead.Succeeded &&
            BytesEqual(
                firstRead.Package.Sections[0].Payload,
                serialized
            ),
            "La lectura conserva el contenido de la sección."
        );

        PersistenceSelfTestData secondSample =
            new PersistenceSelfTestData
            {
                number = 84,
                text = "Segunda generación"
            };
        byte[] secondSerialized =
            serializer.Serialize(secondSample, true);

        BistroBuilderStorageWriteResult secondWrite =
            storage.WriteGenerationAsync(
                    new BistroBuilderStorageWriteRequest(
                        1,
                        "Prueba 2",
                        "test",
                        "Prototype_Restaurant",
                        new List<BistroBuilderSerializedSaveSection>
                        {
                            new BistroBuilderSerializedSaveSection(
                                "test.section",
                                1,
                                serializer.SerializerId,
                                serializer.FileExtension,
                                secondSerialized
                            )
                        }
                    ),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            secondWrite.Succeeded,
            "Una segunda generación se escribe correctamente."
        );
        report.Check(
            firstWrite.GenerationId != secondWrite.GenerationId,
            "Cada generación tiene una identidad distinta."
        );
        report.Check(
            File.Exists(Path.Combine(slotPath, "previous.json")),
            "La sustitución atómica conserva previous.json."
        );

        BistroBuilderStorageReadResult secondRead =
            storage.ReadLatestValidGenerationAsync(
                    1,
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            secondRead.Succeeded &&
            secondRead.Package.Manifest.generationId ==
                secondWrite.GenerationId,
            "La lectura prioriza la generación actual."
        );

        IReadOnlyList<BistroBuilderSaveSlotSummary> summaries =
            storage.ReadAllSlotSummariesAsync(
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            summaries.Count == 1,
            "El índice ligero descubre exactamente un slot."
        );
        report.Check(
            summaries.Count == 1 &&
            summaries[0].SlotDisplayName == "Prueba 2",
            "El índice ligero recupera metadatos sin leer el mundo."
        );

        string secondGenerationPath = Path.Combine(
            slotPath,
            "generations",
            secondWrite.GenerationId
        );
        byte[] secondManifestPayload = File.ReadAllBytes(
            Path.Combine(secondGenerationPath, "manifest.json")
        );
        BistroBuilderSaveManifest secondManifest =
            (BistroBuilderSaveManifest)serializer.Deserialize(
                secondManifestPayload,
                typeof(BistroBuilderSaveManifest)
            );
        string secondSectionPath = Path.Combine(
            secondGenerationPath,
            secondManifest.sections[0]
                .relativePath
                .Replace('/', Path.DirectorySeparatorChar)
        );

        File.WriteAllText(secondSectionPath, "contenido corrupto");

        report.Check(
            File.ReadAllText(secondSectionPath) ==
                "contenido corrupto",
            "El autotest ha simulado una corrupción real."
        );

        BistroBuilderStorageReadResult recoveredRead =
            storage.ReadLatestValidGenerationAsync(
                    1,
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult();

        report.Check(
            recoveredRead.Succeeded,
            "La lectura se recupera tras corromper current."
        );
        report.Check(
            recoveredRead.Succeeded &&
            recoveredRead.Package.RecoveredFromFallback,
            "La recuperación queda marcada como fallback."
        );
        report.Check(
            recoveredRead.Succeeded &&
            recoveredRead.Package.Manifest.generationId ==
                firstWrite.GenerationId,
            "La recuperación utiliza la generación anterior válida."
        );

        bool deleted = storage.DeleteSlotAsync(
                1,
                CancellationToken.None
            )
            .GetAwaiter()
            .GetResult();

        report.Check(
            deleted,
            "El slot puede eliminarse de forma completa."
        );
        report.Check(
            !storage.SlotExists(1),
            "El slot deja de existir tras eliminarlo."
        );

        IReadOnlyList<BistroBuilderSaveSlotSummary>
            summariesAfterDelete =
                storage.ReadAllSlotSummariesAsync(
                        CancellationToken.None
                    )
                    .GetAwaiter()
                    .GetResult();

        report.Check(
            summariesAfterDelete.Count == 0,
            "El índice ligero queda vacío tras eliminar el slot."
        );
    }

    private static void RunSceneTests(SaveSelfTestReport report)
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject gameSystems =
            BistroBuilderPersistenceFoundationValidator
                .FindGameSystems(scene);

        BistroBuilderSaveGameService service = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderSaveGameService>()
            : null;
        BistroBuilderSaveDefinitionCatalog catalog = gameSystems != null
            ? gameSystems.GetComponent<BistroBuilderSaveDefinitionCatalog>()
            : null;
        RestaurantStructureSaveSectionProvider provider =
            gameSystems != null
                ? gameSystems.GetComponent<
                    RestaurantStructureSaveSectionProvider
                >()
                : null;
        BistroBuilderPlacementOperationSaveGuard guard =
            gameSystems != null
                ? gameSystems.GetComponent<
                    BistroBuilderPlacementOperationSaveGuard
                >()
                : null;
        BistroBuilderEditInteractionSaveParticipant participant =
            gameSystems != null
                ? gameSystems.GetComponent<
                    BistroBuilderEditInteractionSaveParticipant
                >()
                : null;

        if (service != null)
        {
            service.RefreshExtensions();
        }

        report.Check(
            service != null &&
            service.ValidateConfiguration(out _),
            "El orquestador de escena está configurado."
        );
        report.Check(
            service != null && service.RegisteredProviderCount >= 1,
            "El orquestador descubre al menos un proveedor."
        );
        report.Check(
            catalog != null && catalog.ValidateConfiguration(out _),
            "El catálogo de definiciones es válido."
        );
        report.Check(
            provider != null && provider.ValidateConfiguration(out _),
            "El proveedor estructural es válido."
        );
        report.Check(
            guard != null && guard.ValidateConfiguration(out _),
            "La regla de seguridad de colocación es válida."
        );
        report.Check(
            participant != null,
            "Existe el participante que bloquea la edición."
        );
        report.Check(
            participant != null &&
            participant.ValidateConfiguration(out _),
            "El bloqueo de interacción está configurado."
        );

        BistroBuilderPersistenceValidationResult validation =
            BistroBuilderPersistenceFoundationValidator
                .ValidateCurrentProject();

        report.Check(
            validation.ErrorCount == 0,
            "El validador integral termina con cero errores."
        );
    }

    private static bool BytesEqual(
        byte[] first,
        byte[] second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first == null ||
            second == null ||
            first.Length != second.Length)
        {
            return false;
        }

        for (int index = 0; index < first.Length; index++)
        {
            if (first[index] != second[index])
            {
                return false;
            }
        }

        return true;
    }

    [Serializable]
    private sealed class PersistenceSelfTestData
    {
        public int number;
        public string text = string.Empty;
    }

    private sealed class SaveSelfTestReport
    {
        private readonly List<string> passed = new List<string>();
        private readonly List<string> failed = new List<string>();

        public int PassedCount => passed.Count;
        public int FailedCount => failed.Count;

        public void Check(bool condition, string description)
        {
            if (condition)
            {
                passed.Add(description);
            }
            else
            {
                failed.Add(description);
            }
        }

        public void Fail(string description)
        {
            failed.Add(description);
        }

        public string BuildReport()
        {
            System.Text.StringBuilder builder =
                new System.Text.StringBuilder(2048);

            builder.AppendLine(
                "BISTRO BUILDER - AUTOTEST DE PERSISTENCIA"
            );
            builder.AppendLine(
                "Pruebas superadas: " + PassedCount
            );
            builder.AppendLine(
                "Pruebas fallidas: " + FailedCount
            );

            for (int index = 0; index < passed.Count; index++)
            {
                builder.AppendLine("- OK: " + passed[index]);
            }

            for (int index = 0; index < failed.Count; index++)
            {
                builder.AppendLine("- ERROR: " + failed[index]);
            }

            return builder.ToString();
        }
    }
}
