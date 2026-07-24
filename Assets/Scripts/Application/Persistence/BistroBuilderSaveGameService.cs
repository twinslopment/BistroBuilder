using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

/// <summary>
/// Orquestador universal de guardado y carga de partidas.
///
/// El hilo principal solo captura y aplica estado de Unity. La
/// serialización, checksums y acceso a disco se ejecutan fuera del hilo
/// principal. Cada sistema aporta una sección independiente mediante
/// IBistroBuilderSaveSectionProvider.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu(
    "Bistro Builder/Persistence/Save Game Service"
)]
public sealed class BistroBuilderSaveGameService : MonoBehaviour
{
    [Header("Almacenamiento")]

    [Tooltip(
        "Ruta relativa dentro de Application.persistentDataPath."
    )]
    [SerializeField]
    private string saveRootFolderName = "BistroBuilder/Saves";

    [Tooltip(
        "Número mínimo de generaciones completas conservadas por slot."
    )]
    [SerializeField]
    [Min(2)]
    private int retainedGenerationsPerSlot = 3;

    [Tooltip(
        "Hace legibles los JSON de desarrollo. Puede desactivarse en " +
        "builds finales sin cambiar el formato lógico."
    )]
    [SerializeField]
    private bool prettyPrintJson = true;

    [Header("Rendimiento")]

    [Tooltip(
        "Número máximo de artículos que un proveedor procesa antes de " +
        "ceder un frame durante captura o carga."
    )]
    [SerializeField]
    [Min(1)]
    private int objectsPerFrame = 32;

    [Header("Depuración")]

    [SerializeField]
    private bool logOperations = true;

    private readonly List<IBistroBuilderSaveSectionProvider>
        providers =
            new List<IBistroBuilderSaveSectionProvider>(16);

    private readonly List<IBistroBuilderSaveSectionMigration>
        migrations =
            new List<IBistroBuilderSaveSectionMigration>(16);

    private readonly List<IBistroBuilderSaveOperationGuard>
        guards =
            new List<IBistroBuilderSaveOperationGuard>(8);

    private readonly List<IBistroBuilderSaveOperationParticipant>
        participants =
            new List<IBistroBuilderSaveOperationParticipant>(8);

    private readonly List<IBistroBuilderSaveOperationParticipant>
        activeParticipants =
            new List<IBistroBuilderSaveOperationParticipant>(8);

    private readonly Dictionary<string, IBistroBuilderSaveSerializer>
        serializers =
            new Dictionary<string, IBistroBuilderSaveSerializer>(
                StringComparer.Ordinal
            );

    private IBistroBuilderSaveSerializer metadataSerializer;
    private IBistroBuilderSaveStorage storage;
    private CancellationTokenSource operationCancellation;

    public event Action<BistroBuilderSaveOperationPhase, float, string>
        ProgressChanged;

    public event Action<BistroBuilderSaveOperationResult>
        OperationCompleted;

    public bool IsBusy { get; private set; }

    public BistroBuilderSaveOperationKind ActiveOperation { get; private set; }

    public BistroBuilderSaveOperationPhase CurrentPhase { get; private set; }

    public float CurrentProgress { get; private set; }

    public string CurrentStatusMessage { get; private set; }

    public BistroBuilderSaveOperationResult LastResult { get; private set; }

    public string SaveRootPath
    {
        get
        {
            return storage != null
                ? storage.RootPath
                : BuildSaveRootPath();
        }
    }

    public int RegisteredProviderCount => providers.Count;

    public int RegisteredSerializerCount => serializers.Count;

    /// <summary>
    /// Registra o sustituye un adaptador de serialización por identidad.
    /// </summary>
    public bool RegisterSerializer(
        IBistroBuilderSaveSerializer saveSerializer
    )
    {
        if (IsBusy ||
            saveSerializer == null ||
            !IsSafeStableId(saveSerializer.SerializerId) ||
            !IsSafeFileExtension(saveSerializer.FileExtension))
        {
            return false;
        }

        serializers[
            NormalizeSectionId(saveSerializer.SerializerId)
        ] = saveSerializer;

        return true;
    }

    /// <summary>
    /// Permite sustituir archivos locales por otro almacenamiento de
    /// plataforma sin modificar proveedores ni datos de juego.
    /// </summary>
    public bool TrySetStorage(
        IBistroBuilderSaveStorage replacement,
        out string error
    )
    {
        error = string.Empty;

        if (IsBusy)
        {
            error = "No puede cambiarse el almacenamiento durante " +
                    "una operación activa.";
            return false;
        }

        if (replacement == null)
        {
            error = "El almacenamiento indicado es nulo.";
            return false;
        }

        storage = replacement;
        return true;
    }

    public bool TryGetSerializer(
        string serializerId,
        out IBistroBuilderSaveSerializer saveSerializer
    )
    {
        saveSerializer = null;

        if (string.IsNullOrWhiteSpace(serializerId))
        {
            return false;
        }

        return serializers.TryGetValue(
            NormalizeSectionId(serializerId),
            out saveSerializer
        );
    }

    private void Awake()
    {
        InitializeInfrastructure();
        RefreshExtensions();
    }

    private void OnDisable()
    {
        operationCancellation?.Cancel();

        if (IsBusy)
        {
            EndActiveParticipants(false);
            IsBusy = false;
            ActiveOperation = BistroBuilderSaveOperationKind.None;
        }
    }

    private void OnDestroy()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <summary>
    /// Redescubre proveedores, migraciones y reglas una sola vez.
    /// El instalador llama también a este método tras reparar la escena.
    /// </summary>
    public void RefreshExtensions()
    {
        EnsureInfrastructure();
        providers.Clear();
        migrations.Clear();
        guards.Clear();
        participants.Clear();

        MonoBehaviour[] behaviours =
            GetComponents<MonoBehaviour>();

        for (int index = 0;
             index < behaviours.Length;
             index++)
        {
            MonoBehaviour behaviour = behaviours[index];

            if (behaviour == null ||
                ReferenceEquals(behaviour, this))
            {
                continue;
            }

            if (behaviour is IBistroBuilderSaveSectionProvider provider)
            {
                providers.Add(provider);
            }

            if (behaviour is IBistroBuilderSaveSectionMigration migration)
            {
                migrations.Add(migration);
            }

            if (behaviour is IBistroBuilderSaveOperationGuard guard)
            {
                guards.Add(guard);
            }

            if (behaviour is
                IBistroBuilderSaveOperationParticipant participant)
            {
                participants.Add(participant);
            }

            if (behaviour is IBistroBuilderSaveSerializer serializer)
            {
                RegisterSerializer(serializer);
            }
        }

        providers.Sort(CompareProviders);
        migrations.Sort(CompareMigrations);
        guards.Sort(CompareGuards);
        participants.Sort(CompareParticipants);
    }

    public bool ValidateConfiguration(out string error)
    {
        error = string.Empty;

        if (!TryBuildSaveRootPath(
                out _,
                out error
            ))
        {
            return false;
        }

        if (providers.Count == 0)
        {
            error = "No existe ningún proveedor de secciones de partida.";
            return false;
        }

        HashSet<string> sectionIds =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0;
             index < providers.Count;
             index++)
        {
            IBistroBuilderSaveSectionProvider provider = providers[index];

            if (provider == null ||
                !IsSafeStableId(provider.SectionId) ||
                provider.SectionVersion < 1 ||
                provider.StateType == null ||
                !IsSafeStableId(provider.SerializerId))
            {
                error = "Existe un proveedor de persistencia inválido.";
                return false;
            }

            string sectionId = NormalizeSectionId(provider.SectionId);

            if (!sectionIds.Add(sectionId))
            {
                error = "La sección " + sectionId + " está duplicada.";
                return false;
            }

            if (!TryGetSerializer(
                    provider.SerializerId,
                    out _
                ))
            {
                error = "La sección " + sectionId +
                        " solicita el serializador no registrado " +
                        provider.SerializerId + ".";
                return false;
            }
        }

        HashSet<string> migrationKeys =
            new HashSet<string>(StringComparer.Ordinal);

        for (int index = 0;
             index < migrations.Count;
             index++)
        {
            IBistroBuilderSaveSectionMigration migration = migrations[index];

            if (migration == null ||
                !IsSafeStableId(migration.SectionId) ||
                migration.FromVersion < 1 ||
                migration.ToVersion != migration.FromVersion + 1 ||
                !IsSafeStableId(migration.FromSerializerId) ||
                !IsSafeStableId(migration.ToSerializerId))
            {
                error = "Existe una migración inválida.";
                return false;
            }

            string key =
                NormalizeSectionId(migration.SectionId) +
                ":" +
                migration.FromVersion +
                ":" +
                NormalizeSectionId(
                    migration.FromSerializerId
                );

            if (!migrationKeys.Add(key))
            {
                error = "La migración " + key + " está duplicada.";
                return false;
            }

            if (!TryGetSerializer(
                    migration.ToSerializerId,
                    out _
                ))
            {
                error = "La migración " + key +
                        " termina en un serializador no registrado.";
                return false;
            }
        }

        return true;
    }

    public bool TrySaveSlot(
        int slotIndex,
        string slotDisplayName,
        out string rejectionMessage
    )
    {
        rejectionMessage = string.Empty;

        if (!TryBeginOperation(
                BistroBuilderSaveOperationKind.Save,
                slotIndex,
                true,
                out rejectionMessage
            ))
        {
            return false;
        }

        StartCoroutine(
            SaveRoutine(
                slotIndex,
                slotDisplayName ?? string.Empty
            )
        );

        return true;
    }

    public bool TryLoadSlot(
        int slotIndex,
        out string rejectionMessage
    )
    {
        rejectionMessage = string.Empty;

        if (!TryBeginOperation(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                false,
                out rejectionMessage
            ))
        {
            return false;
        }

        StartCoroutine(LoadRoutine(slotIndex));
        return true;
    }

    public bool TryDeleteSlot(
        int slotIndex,
        out string rejectionMessage
    )
    {
        rejectionMessage = string.Empty;

        if (!TryBeginOperation(
                BistroBuilderSaveOperationKind.Delete,
                slotIndex,
                false,
                out rejectionMessage,
                evaluateGuards: false
            ))
        {
            return false;
        }

        StartCoroutine(DeleteRoutine(slotIndex));
        return true;
    }

    public bool SlotExists(int slotIndex)
    {
        EnsureInfrastructure();
        return storage.SlotExists(slotIndex);
    }

    /// <summary>
    /// Obtiene metadatos ligeros para el futuro menú de partidas sin
    /// deserializar el estado completo del restaurante.
    /// </summary>
    public Task<IReadOnlyList<BistroBuilderSaveSlotSummary>>
        ReadSlotSummariesAsync(
            CancellationToken cancellationToken
        )
    {
        EnsureInfrastructure();
        return storage.ReadAllSlotSummariesAsync(cancellationToken);
    }

    public void CancelActiveOperation()
    {
        operationCancellation?.Cancel();
    }

    private IEnumerator SaveRoutine(
        int slotIndex,
        string slotDisplayName
    )
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CancellationToken token = operationCancellation.Token;

        CaptureBatchResult captureResult =
            new CaptureBatchResult();

        SetProgress(
            BistroBuilderSaveOperationPhase.Capturing,
            0.05f,
            "Capturando estado de la partida..."
        );

        yield return CaptureAllProviders(
            slotIndex,
            captureResult,
            0.05f,
            0.45f,
            token
        );

        if (captureResult.HasFailed)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Save,
                slotIndex,
                captureResult.ErrorMessage,
                stopwatch
            );
            yield break;
        }

        SetProgress(
            BistroBuilderSaveOperationPhase.Serializing,
            0.50f,
            "Serializando secciones..."
        );

        string applicationVersion = Application.version;
        string sceneName = SceneManager.GetActiveScene().name;

        Task<List<BistroBuilderSerializedSaveSection>>
            serializationTask =
                Task.Run(
                    () => SerializeCapturedSections(
                        captureResult.Sections,
                        token
                    ),
                    token
                );

        /*
         * Aunque se solicite cancelación, esperamos a que el trabajo en
         * segundo plano alcance un estado terminal. Así no queda una tarea
         * huérfana serializando mientras comienza otra operación.
         */
        while (!serializationTask.IsCompleted)
        {
            yield return null;
        }

        if (serializationTask.IsCanceled ||
            token.IsCancellationRequested)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Save,
                slotIndex,
                "El guardado fue cancelado.",
                stopwatch
            );
            yield break;
        }

        if (serializationTask.IsFaulted)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Save,
                slotIndex,
                FlattenTaskError(serializationTask.Exception),
                stopwatch
            );
            yield break;
        }

        BistroBuilderStorageWriteRequest request =
            new BistroBuilderStorageWriteRequest(
                slotIndex,
                slotDisplayName,
                applicationVersion,
                sceneName,
                serializationTask.Result
            );

        SetProgress(
            BistroBuilderSaveOperationPhase.Writing,
            0.65f,
            "Escribiendo generación segura..."
        );

        Task<BistroBuilderStorageWriteResult> writeTask =
            storage.WriteGenerationAsync(request, token);

        /*
         * El storage observa el CancellationToken y limpia su generación
         * temporal. Esperamos su terminación para impedir escrituras
         * concurrentes sobre el mismo slot tras cancelar.
         */
        while (!writeTask.IsCompleted)
        {
            yield return null;
        }

        if (writeTask.IsCanceled || token.IsCancellationRequested)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Save,
                slotIndex,
                "El guardado fue cancelado.",
                stopwatch
            );
            yield break;
        }

        if (writeTask.IsFaulted)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Save,
                slotIndex,
                FlattenTaskError(writeTask.Exception),
                stopwatch
            );
            yield break;
        }

        BistroBuilderStorageWriteResult storageResult =
            writeTask.Result;

        if (!storageResult.Succeeded)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Save,
                slotIndex,
                storageResult.ErrorMessage,
                stopwatch
            );
            yield break;
        }

        stopwatch.Stop();

        CompleteSuccess(
            BistroBuilderSaveOperationResult.Success(
                BistroBuilderSaveOperationKind.Save,
                slotIndex,
                storageResult.GenerationId,
                "Partida guardada correctamente.",
                storageResult.PayloadBytes,
                stopwatch.Elapsed.TotalMilliseconds
            )
        );
    }

    private IEnumerator LoadRoutine(int slotIndex)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CancellationToken token = operationCancellation.Token;

        SetProgress(
            BistroBuilderSaveOperationPhase.Reading,
            0.05f,
            "Leyendo y verificando la partida..."
        );

        Task<BistroBuilderStorageReadResult> readTask =
            storage.ReadLatestValidGenerationAsync(
                slotIndex,
                token
            );

        while (!readTask.IsCompleted)
        {
            yield return null;
        }

        if (readTask.IsCanceled || token.IsCancellationRequested)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                "La carga fue cancelada.",
                stopwatch
            );
            yield break;
        }

        if (readTask.IsFaulted)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                FlattenTaskError(readTask.Exception),
                stopwatch
            );
            yield break;
        }

        BistroBuilderStorageReadResult storageReadResult =
            readTask.Result;

        if (!storageReadResult.Succeeded ||
            storageReadResult.Package == null)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                storageReadResult.ErrorMessage,
                stopwatch
            );
            yield break;
        }

        SetProgress(
            BistroBuilderSaveOperationPhase.Migrating,
            0.20f,
            "Comprobando versiones y migraciones..."
        );

        if (!TryPrepareSectionsForDeserialization(
                storageReadResult.Package,
                out List<PreparedStoredSection> preparedSections,
                out string preparationError
            ))
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                preparationError,
                stopwatch
            );
            yield break;
        }

        SetProgress(
            BistroBuilderSaveOperationPhase.Deserializing,
            0.28f,
            "Deserializando secciones..."
        );

        Task<List<BistroBuilderLoadedSaveSection>> deserializeTask =
            Task.Run(
                () => DeserializePreparedSections(
                    preparedSections,
                    token
                ),
                token
            );

        while (!deserializeTask.IsCompleted)
        {
            yield return null;
        }

        if (deserializeTask.IsCanceled || token.IsCancellationRequested)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                "La carga fue cancelada.",
                stopwatch
            );
            yield break;
        }

        if (deserializeTask.IsFaulted)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                FlattenTaskError(deserializeTask.Exception),
                stopwatch
            );
            yield break;
        }

        List<BistroBuilderLoadedSaveSection> targetSections =
            deserializeTask.Result;

        if (!ValidateLoadedSections(
                targetSections,
                out string validationError
            ))
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                validationError,
                stopwatch
            );
            yield break;
        }

        /*
         * Antes de modificar el mundo se captura un snapshot en memoria.
         * Si cualquier proveedor falla, el snapshot se reaplica de forma
         * automática y el jugador no queda con una carga parcial.
         */
        CaptureBatchResult rollbackCapture =
            new CaptureBatchResult();

        SetProgress(
            BistroBuilderSaveOperationPhase.Capturing,
            0.38f,
            "Creando punto de restauración en memoria..."
        );

        yield return CaptureAllProviders(
            slotIndex,
            rollbackCapture,
            0.38f,
            0.48f,
            token
        );

        if (rollbackCapture.HasFailed)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                "No se pudo crear el punto de restauración: " +
                rollbackCapture.ErrorMessage,
                stopwatch
            );
            yield break;
        }

        List<BistroBuilderLoadedSaveSection> rollbackSections =
            ConvertCapturedToLoaded(rollbackCapture.Sections);

        BistroBuilderSaveLoadContext loadContext =
            new BistroBuilderSaveLoadContext(
                slotIndex,
                false,
                objectsPerFrame,
                () => token.IsCancellationRequested
            );

        SetProgress(
            BistroBuilderSaveOperationPhase.PreparingWorld,
            0.50f,
            "Preparando el mundo para la carga..."
        );

        yield return PrepareAllProviders(loadContext);

        if (!loadContext.HasFailed)
        {
            SetProgress(
                BistroBuilderSaveOperationPhase.Applying,
                0.62f,
                "Reconstruyendo la partida..."
            );

            yield return ApplyAllProviders(
                targetSections,
                loadContext,
                0.62f,
                0.90f
            );
        }

        if (!loadContext.HasFailed)
        {
            SetProgress(
                BistroBuilderSaveOperationPhase.Finalizing,
                0.93f,
                "Finalizando registros y relaciones..."
            );

            FinalizeAllProviders(loadContext);
        }

        if (loadContext.HasFailed)
        {
            string targetFailure = loadContext.ErrorMessage;

            BistroBuilderSaveLoadContext rollbackContext =
                new BistroBuilderSaveLoadContext(
                    slotIndex,
                    true,
                    objectsPerFrame
                );

            SetProgress(
                BistroBuilderSaveOperationPhase.RollingBack,
                0.94f,
                "Restaurando el estado anterior..."
            );

            yield return PrepareAllProviders(rollbackContext);

            if (!rollbackContext.HasFailed)
            {
                yield return ApplyAllProviders(
                    rollbackSections,
                    rollbackContext,
                    0.94f,
                    0.98f
                );
            }

            if (!rollbackContext.HasFailed)
            {
                FinalizeAllProviders(rollbackContext);
            }

            string finalError = rollbackContext.HasFailed
                ? targetFailure +
                  " Además, la restauración de seguridad falló: " +
                  rollbackContext.ErrorMessage
                : targetFailure +
                  " El estado anterior fue restaurado correctamente.";

            CompleteFailure(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                finalError,
                stopwatch
            );
            yield break;
        }

        stopwatch.Stop();

        CompleteSuccess(
            BistroBuilderSaveOperationResult.Success(
                BistroBuilderSaveOperationKind.Load,
                slotIndex,
                storageReadResult.Package.Manifest.generationId,
                storageReadResult.Package.RecoveredFromFallback
                    ? "Partida cargada desde una generación de respaldo."
                    : "Partida cargada correctamente.",
                storageReadResult.Package.Manifest.totalPayloadBytes,
                stopwatch.Elapsed.TotalMilliseconds,
                storageReadResult.Package.RecoveredFromFallback
            )
        );
    }

    private IEnumerator DeleteRoutine(int slotIndex)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        CancellationToken token = operationCancellation.Token;

        SetProgress(
            BistroBuilderSaveOperationPhase.Cleaning,
            0.25f,
            "Eliminando slot..."
        );

        Task<bool> task = storage.DeleteSlotAsync(
            slotIndex,
            token
        );

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsCanceled)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Delete,
                slotIndex,
                "La eliminación fue cancelada.",
                stopwatch
            );
            yield break;
        }

        if (task.IsFaulted || !task.Result)
        {
            CompleteFailure(
                BistroBuilderSaveOperationKind.Delete,
                slotIndex,
                task.IsFaulted
                    ? FlattenTaskError(task.Exception)
                    : "No se pudo eliminar el slot.",
                stopwatch
            );
            yield break;
        }

        stopwatch.Stop();

        CompleteSuccess(
            BistroBuilderSaveOperationResult.Success(
                BistroBuilderSaveOperationKind.Delete,
                slotIndex,
                string.Empty,
                "Slot eliminado correctamente.",
                0L,
                stopwatch.Elapsed.TotalMilliseconds
            )
        );
    }

    private IEnumerator CaptureAllProviders(
        int slotIndex,
        CaptureBatchResult batchResult,
        float progressStart,
        float progressEnd,
        CancellationToken token
    )
    {
        batchResult.Sections.Clear();
        batchResult.HasFailed = false;
        batchResult.ErrorMessage = string.Empty;

        for (int index = 0;
             index < providers.Count;
             index++)
        {
            IBistroBuilderSaveSectionProvider provider = providers[index];
            BistroBuilderSaveCaptureContext context =
                new BistroBuilderSaveCaptureContext(
                    slotIndex,
                    () => token.IsCancellationRequested
                );

            IEnumerator routine;

            try
            {
                routine = provider.CaptureState(context);
            }
            catch (Exception exception)
            {
                batchResult.Fail(
                    provider.SectionId +
                    ": " +
                    exception.Message
                );
                yield break;
            }

            ProviderRoutineResult routineResult =
                new ProviderRoutineResult();

            yield return RunProviderRoutineSafely(
                routine,
                routineResult
            );

            if (routineResult.Exception != null)
            {
                batchResult.Fail(
                    provider.SectionId +
                    ": " +
                    routineResult.Exception.Message
                );
                yield break;
            }

            if (context.HasFailed || context.State == null)
            {
                batchResult.Fail(
                    provider.SectionId +
                    ": " +
                    (context.HasFailed
                        ? context.ErrorMessage
                        : "El proveedor no devolvió estado.")
                );
                yield break;
            }

            if (!provider.StateType.IsInstanceOfType(context.State))
            {
                batchResult.Fail(
                    provider.SectionId +
                    ": el estado capturado no coincide con " +
                    provider.StateType.Name + "."
                );
                yield break;
            }

            if (!provider.ValidateState(
                    context.State,
                    out string capturedStateError
                ))
            {
                batchResult.Fail(
                    provider.SectionId +
                    ": " +
                    capturedStateError
                );
                yield break;
            }

            batchResult.Sections.Add(
                new BistroBuilderCapturedSaveSection(
                    provider,
                    context.State
                )
            );

            float normalized =
                providers.Count > 0
                    ? (index + 1f) / providers.Count
                    : 1f;

            SetProgress(
                BistroBuilderSaveOperationPhase.Capturing,
                Mathf.Lerp(
                    progressStart,
                    progressEnd,
                    normalized
                ),
                "Capturada sección " + provider.SectionId + "."
            );
        }
    }

    private IEnumerator PrepareAllProviders(
        BistroBuilderSaveLoadContext context
    )
    {
        for (int index = providers.Count - 1;
             index >= 0;
             index--)
        {
            IBistroBuilderSaveSectionProvider provider = providers[index];
            IEnumerator routine;

            try
            {
                routine = provider.PrepareForLoad(context);
            }
            catch (Exception exception)
            {
                context.Fail(
                    provider.SectionId +
                    ": " +
                    exception.Message
                );
                yield break;
            }

            ProviderRoutineResult routineResult =
                new ProviderRoutineResult();

            yield return RunProviderRoutineSafely(
                routine,
                routineResult
            );

            if (routineResult.Exception != null)
            {
                context.Fail(
                    provider.SectionId +
                    ": " +
                    routineResult.Exception.Message
                );
                yield break;
            }

            if (context.HasFailed)
            {
                yield break;
            }
        }
    }

    private IEnumerator ApplyAllProviders(
        IReadOnlyList<BistroBuilderLoadedSaveSection> loadedSections,
        BistroBuilderSaveLoadContext context,
        float progressStart,
        float progressEnd
    )
    {
        Dictionary<string, BistroBuilderLoadedSaveSection> byId =
            new Dictionary<string, BistroBuilderLoadedSaveSection>(
                StringComparer.Ordinal
            );

        for (int index = 0;
             index < loadedSections.Count;
             index++)
        {
            BistroBuilderLoadedSaveSection section =
                loadedSections[index];

            byId[NormalizeSectionId(section.Provider.SectionId)] =
                section;
        }

        for (int index = 0;
             index < providers.Count;
             index++)
        {
            IBistroBuilderSaveSectionProvider provider = providers[index];
            string sectionId = NormalizeSectionId(provider.SectionId);

            if (!byId.TryGetValue(
                    sectionId,
                    out BistroBuilderLoadedSaveSection section
                ))
            {
                if (provider.IsRequired)
                {
                    context.Fail(
                        "Falta la sección obligatoria " +
                        sectionId +
                        "."
                    );
                    yield break;
                }

                continue;
            }

            IEnumerator routine;

            try
            {
                routine = provider.ApplyState(
                    section.State,
                    context
                );
            }
            catch (Exception exception)
            {
                context.Fail(
                    sectionId +
                    ": " +
                    exception.Message
                );
                yield break;
            }

            ProviderRoutineResult routineResult =
                new ProviderRoutineResult();

            yield return RunProviderRoutineSafely(
                routine,
                routineResult
            );

            if (routineResult.Exception != null)
            {
                context.Fail(
                    sectionId +
                    ": " +
                    routineResult.Exception.Message
                );
                yield break;
            }

            if (context.HasFailed)
            {
                yield break;
            }

            float normalized =
                providers.Count > 0
                    ? (index + 1f) / providers.Count
                    : 1f;

            SetProgress(
                context.IsRollback
                    ? BistroBuilderSaveOperationPhase.RollingBack
                    : BistroBuilderSaveOperationPhase.Applying,
                Mathf.Lerp(
                    progressStart,
                    progressEnd,
                    normalized
                ),
                "Aplicada sección " + sectionId + "."
            );
        }
    }

    private void FinalizeAllProviders(
        BistroBuilderSaveLoadContext context
    )
    {
        for (int index = 0;
             index < providers.Count;
             index++)
        {
            try
            {
                providers[index].FinalizeLoad(context);
            }
            catch (Exception exception)
            {
                context.Fail(
                    providers[index].SectionId +
                    ": " +
                    exception.Message
                );
                return;
            }

            if (context.HasFailed)
            {
                return;
            }
        }
    }

    private IEnumerator RunProviderRoutineSafely(
        IEnumerator routine,
        ProviderRoutineResult result
    )
    {
        if (routine == null)
        {
            yield break;
        }

        while (true)
        {
            bool hasNext;
            object current = null;

            try
            {
                hasNext = routine.MoveNext();

                if (hasNext)
                {
                    current = routine.Current;
                }
            }
            catch (Exception exception)
            {
                result.Exception = exception;
                yield break;
            }

            if (!hasNext)
            {
                yield break;
            }

            yield return current;
        }
    }

    private List<BistroBuilderSerializedSaveSection>
        SerializeCapturedSections(
            IReadOnlyList<BistroBuilderCapturedSaveSection> captured,
            CancellationToken token
        )
    {
        List<BistroBuilderSerializedSaveSection> serialized =
            new List<BistroBuilderSerializedSaveSection>(
                captured.Count
            );

        for (int index = 0;
             index < captured.Count;
             index++)
        {
            token.ThrowIfCancellationRequested();

            BistroBuilderCapturedSaveSection section = captured[index];

            if (!TryGetSerializer(
                    section.SerializerId,
                    out IBistroBuilderSaveSerializer sectionSerializer
                ))
            {
                throw new InvalidOperationException(
                    "No está registrado el serializador " +
                    section.SerializerId + "."
                );
            }

            serialized.Add(
                new BistroBuilderSerializedSaveSection(
                    NormalizeSectionId(section.SectionId),
                    section.SectionVersion,
                    NormalizeSectionId(
                        sectionSerializer.SerializerId
                    ),
                    sectionSerializer.FileExtension,
                    sectionSerializer.Serialize(
                        section.State,
                        prettyPrintJson
                    )
                )
            );
        }

        return serialized;
    }

    private bool TryPrepareSectionsForDeserialization(
        BistroBuilderStorageReadPackage package,
        out List<PreparedStoredSection> prepared,
        out string error
    )
    {
        prepared = new List<PreparedStoredSection>();
        error = string.Empty;

        Dictionary<string, BistroBuilderStoredSaveSection> storedById =
            new Dictionary<string, BistroBuilderStoredSaveSection>(
                StringComparer.Ordinal
            );

        for (int index = 0;
             index < package.Sections.Count;
             index++)
        {
            BistroBuilderStoredSaveSection stored =
                package.Sections[index];
            string sectionId = NormalizeSectionId(stored.SectionId);

            if (storedById.ContainsKey(sectionId))
            {
                error = "La partida contiene la sección duplicada " +
                        sectionId +
                        ".";
                return false;
            }

            storedById.Add(sectionId, stored);
        }

        for (int index = 0;
             index < providers.Count;
             index++)
        {
            IBistroBuilderSaveSectionProvider provider = providers[index];
            string sectionId = NormalizeSectionId(provider.SectionId);

            if (!storedById.TryGetValue(
                    sectionId,
                    out BistroBuilderStoredSaveSection stored
                ))
            {
                if (provider.IsRequired)
                {
                    error = "Falta la sección obligatoria " +
                            sectionId +
                            ".";
                    return false;
                }

                continue;
            }

            int version = stored.SectionVersion;
            string serializerId = NormalizeSectionId(
                stored.SerializerId
            );
            byte[] payload = stored.Payload;

            if (version > provider.SectionVersion)
            {
                error = "La sección " +
                        sectionId +
                        " pertenece a una versión futura (" +
                        version +
                        ").";
                return false;
            }

            int migrationSafetyCounter = 0;

            while (version < provider.SectionVersion)
            {
                migrationSafetyCounter++;

                if (migrationSafetyCounter > 128)
                {
                    error = "Se detectó un ciclo de migraciones en " +
                            sectionId +
                            ".";
                    return false;
                }

                IBistroBuilderSaveSectionMigration migration =
                    FindMigration(
                        sectionId,
                        version,
                        serializerId
                    );

                if (migration == null)
                {
                    error = "No existe migración para " +
                            sectionId +
                            " desde la versión " +
                            version +
                            ".";
                    return false;
                }

                if (!migration.TryMigrate(
                        payload,
                        out byte[] migratedPayload,
                        out string migrationError
                    ))
                {
                    error = "La migración de " +
                            sectionId +
                            " ha fallado: " +
                            migrationError;
                    return false;
                }

                if (migratedPayload == null ||
                    migratedPayload.Length == 0)
                {
                    error = "La migración de " + sectionId +
                            " devolvió un payload vacío.";
                    return false;
                }

                payload = migratedPayload;
                serializerId = NormalizeSectionId(
                    migration.ToSerializerId
                );
                version = migration.ToVersion;
            }

            string expectedSerializerId = NormalizeSectionId(
                provider.SerializerId
            );

            if (!string.Equals(
                    serializerId,
                    expectedSerializerId,
                    StringComparison.Ordinal
                ))
            {
                error = "La sección " + sectionId +
                        " usa " + serializerId +
                        " pero el proveedor actual requiere " +
                        expectedSerializerId +
                        ". Falta una migración de formato.";
                return false;
            }

            if (!TryGetSerializer(serializerId, out _))
            {
                error = "No está registrado el serializador " +
                        serializerId + ".";
                return false;
            }

            prepared.Add(
                new PreparedStoredSection(
                    provider,
                    provider.StateType,
                    serializerId,
                    payload
                )
            );
        }

        return true;
    }

    private List<BistroBuilderLoadedSaveSection>
        DeserializePreparedSections(
            IReadOnlyList<PreparedStoredSection> prepared,
            CancellationToken token
        )
    {
        List<BistroBuilderLoadedSaveSection> loaded =
            new List<BistroBuilderLoadedSaveSection>(
                prepared.Count
            );

        for (int index = 0;
             index < prepared.Count;
             index++)
        {
            token.ThrowIfCancellationRequested();

            PreparedStoredSection section = prepared[index];

            if (!TryGetSerializer(
                    section.SerializerId,
                    out IBistroBuilderSaveSerializer sectionSerializer
                ))
            {
                throw new InvalidOperationException(
                    "No está registrado el serializador " +
                    section.SerializerId + "."
                );
            }

            loaded.Add(
                new BistroBuilderLoadedSaveSection(
                    section.Provider,
                    sectionSerializer.Deserialize(
                        section.Payload,
                        section.StateType
                    )
                )
            );
        }

        return loaded;
    }

    private bool ValidateLoadedSections(
        IReadOnlyList<BistroBuilderLoadedSaveSection> sections,
        out string error
    )
    {
        error = string.Empty;

        for (int index = 0;
             index < sections.Count;
             index++)
        {
            BistroBuilderLoadedSaveSection section = sections[index];

            if (!section.Provider.ValidateState(
                    section.State,
                    out string sectionError
                ))
            {
                error = section.Provider.SectionId +
                        ": " +
                        sectionError;
                return false;
            }
        }

        return true;
    }

    private List<BistroBuilderLoadedSaveSection>
        ConvertCapturedToLoaded(
            IReadOnlyList<BistroBuilderCapturedSaveSection> captured
        )
    {
        List<BistroBuilderLoadedSaveSection> loaded =
            new List<BistroBuilderLoadedSaveSection>(captured.Count);

        for (int index = 0;
             index < captured.Count;
             index++)
        {
            loaded.Add(
                new BistroBuilderLoadedSaveSection(
                    captured[index].Provider,
                    captured[index].State
                )
            );
        }

        return loaded;
    }

    private IBistroBuilderSaveSectionMigration FindMigration(
        string sectionId,
        int fromVersion,
        string fromSerializerId
    )
    {
        for (int index = 0;
             index < migrations.Count;
             index++)
        {
            IBistroBuilderSaveSectionMigration migration = migrations[index];

            if (migration.FromVersion == fromVersion &&
                string.Equals(
                    NormalizeSectionId(migration.SectionId),
                    sectionId,
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    NormalizeSectionId(
                        migration.FromSerializerId
                    ),
                    fromSerializerId,
                    StringComparison.Ordinal
                ))
            {
                return migration;
            }
        }

        return null;
    }

    private bool TryBeginOperation(
        BistroBuilderSaveOperationKind operationKind,
        int slotIndex,
        bool isSave,
        out string rejectionMessage,
        bool evaluateGuards = true
    )
    {
        rejectionMessage = string.Empty;

        if (slotIndex < 1 || slotIndex > 999)
        {
            rejectionMessage = "El slot debe estar entre 1 y 999.";
            return false;
        }

        if (IsBusy)
        {
            rejectionMessage =
                "Ya existe una operación de guardado o carga activa.";
            return false;
        }

        EnsureInfrastructure();
        RefreshExtensions();

        if (!ValidateConfiguration(out rejectionMessage))
        {
            return false;
        }

        if (evaluateGuards &&
            !EvaluateGuards(isSave, out rejectionMessage))
        {
            return false;
        }

        ActiveOperation = operationKind;

        if (!BeginParticipants(
                operationKind,
                out rejectionMessage
            ))
        {
            ActiveOperation = BistroBuilderSaveOperationKind.None;
            return false;
        }

        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();

        IsBusy = true;
        LastResult = null;

        SetProgress(
            BistroBuilderSaveOperationPhase.Validating,
            0f,
            "Validando operación..."
        );

        return true;
    }

    private bool BeginParticipants(
        BistroBuilderSaveOperationKind operationKind,
        out string rejectionMessage
    )
    {
        rejectionMessage = string.Empty;
        activeParticipants.Clear();

        for (int index = 0;
             index < participants.Count;
             index++)
        {
            IBistroBuilderSaveOperationParticipant participant =
                participants[index];
            bool began;

            try
            {
                began = participant.TryBeginSaveOperation(
                    operationKind,
                    out rejectionMessage
                );
            }
            catch (Exception exception)
            {
                rejectionMessage = exception.Message;
                began = false;
            }

            if (!began)
            {
                EndActiveParticipants(false);

                if (string.IsNullOrWhiteSpace(rejectionMessage))
                {
                    rejectionMessage =
                        "Un sistema no pudo preparar el snapshot.";
                }

                return false;
            }

            activeParticipants.Add(participant);
        }

        return true;
    }

    private void EndActiveParticipants(bool succeeded)
    {
        for (int index = activeParticipants.Count - 1;
             index >= 0;
             index--)
        {
            try
            {
                activeParticipants[index].EndSaveOperation(
                    ActiveOperation,
                    succeeded
                );
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        activeParticipants.Clear();
    }

    private bool EvaluateGuards(
        bool isSave,
        out string rejectionMessage
    )
    {
        rejectionMessage = string.Empty;

        for (int index = 0;
             index < guards.Count;
             index++)
        {
            bool allowed = isSave
                ? guards[index].CanSave(out rejectionMessage)
                : guards[index].CanLoad(out rejectionMessage);

            if (!allowed)
            {
                if (string.IsNullOrWhiteSpace(rejectionMessage))
                {
                    rejectionMessage =
                        "El estado actual no permite esta operación.";
                }

                return false;
            }
        }

        return true;
    }

    private void InitializeInfrastructure()
    {
        serializers.Clear();
        metadataSerializer = new BistroBuilderJsonSaveSerializer();
        RegisterSerializer(metadataSerializer);

        storage = new BistroBuilderFileSaveStorage(
            BuildSaveRootPath(),
            metadataSerializer,
            retainedGenerationsPerSlot,
            prettyPrintJson
        );

        CurrentStatusMessage = string.Empty;
        CurrentPhase = BistroBuilderSaveOperationPhase.Idle;
    }

    private void EnsureInfrastructure()
    {
        if (metadataSerializer == null || storage == null)
        {
            InitializeInfrastructure();
        }
    }

    private string BuildSaveRootPath()
    {
        if (!TryBuildSaveRootPath(
                out string path,
                out string error
            ))
        {
            throw new InvalidOperationException(error);
        }

        return path;
    }

    private bool TryBuildSaveRootPath(
        out string path,
        out string error
    )
    {
        path = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(saveRootFolderName) ||
            Path.IsPathRooted(saveRootFolderName))
        {
            error =
                "La carpeta de partidas debe ser una ruta relativa.";
            return false;
        }

        string normalizedRelativePath =
            saveRootFolderName
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .Trim(Path.DirectorySeparatorChar);
        string persistentRoot = Path.GetFullPath(
                Application.persistentDataPath
            )
            .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        string candidate = Path.GetFullPath(
            Path.Combine(persistentRoot, normalizedRelativePath)
        );

        StringComparison pathComparison =
            Path.DirectorySeparatorChar == '\\'
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        if (!candidate.StartsWith(
                persistentRoot,
                pathComparison
            ))
        {
            error =
                "La carpeta de partidas intenta salir de " +
                "Application.persistentDataPath.";
            return false;
        }

        path = candidate;
        return true;
    }

    private void SetProgress(
        BistroBuilderSaveOperationPhase phase,
        float progress,
        string message
    )
    {
        CurrentPhase = phase;
        CurrentProgress = Mathf.Clamp01(progress);
        CurrentStatusMessage = message ?? string.Empty;

        ProgressChanged?.Invoke(
            CurrentPhase,
            CurrentProgress,
            CurrentStatusMessage
        );
    }

    private void CompleteSuccess(
        BistroBuilderSaveOperationResult result
    )
    {
        LastResult = result;

        SetProgress(
            BistroBuilderSaveOperationPhase.Completed,
            1f,
            result.Message
        );

        FinishOperation(true);
        OperationCompleted?.Invoke(result);

        if (logOperations)
        {
            Debug.Log(
                result.OperationKind +
                " slot " +
                result.SlotIndex +
                " completado en " +
                result.DurationMilliseconds.ToString("F1") +
                " ms. Bytes: " +
                result.PayloadBytes +
                ".",
                this
            );
        }
    }

    private void CompleteFailure(
        BistroBuilderSaveOperationKind operationKind,
        int slotIndex,
        string message,
        Stopwatch stopwatch
    )
    {
        stopwatch.Stop();

        BistroBuilderSaveOperationResult result =
            BistroBuilderSaveOperationResult.Failure(
                operationKind,
                slotIndex,
                message,
                stopwatch.Elapsed.TotalMilliseconds
            );

        LastResult = result;

        SetProgress(
            BistroBuilderSaveOperationPhase.Failed,
            CurrentProgress,
            result.Message
        );

        FinishOperation(false);
        OperationCompleted?.Invoke(result);

        Debug.LogError(
            operationKind +
            " del slot " +
            slotIndex +
            " ha fallado: " +
            message,
            this
        );
    }

    private void FinishOperation(bool succeeded)
    {
        EndActiveParticipants(succeeded);
        IsBusy = false;
        ActiveOperation = BistroBuilderSaveOperationKind.None;

        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    private static string FlattenTaskError(
        AggregateException exception
    )
    {
        if (exception == null)
        {
            return "La operación en segundo plano ha fallado.";
        }

        AggregateException flattened = exception.Flatten();

        return flattened.InnerExceptions.Count > 0
            ? flattened.InnerExceptions[0].Message
            : flattened.Message;
    }

    private static string NormalizeSectionId(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }

    private static bool IsSafeStableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string trimmed = value.Trim();

        for (int index = 0; index < trimmed.Length; index++)
        {
            char character = trimmed[index];
            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= 'A' && character <= 'Z' ||
                character >= '0' && character <= '9' ||
                character == '.' ||
                character == '_' ||
                character == '-';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSafeFileExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return false;
        }

        string trimmed = extension.Trim();

        if (!trimmed.StartsWith(".", StringComparison.Ordinal) ||
            trimmed.Length < 2)
        {
            return false;
        }

        for (int index = 1; index < trimmed.Length; index++)
        {
            char character = trimmed[index];
            bool allowed =
                character >= 'a' && character <= 'z' ||
                character >= 'A' && character <= 'Z' ||
                character >= '0' && character <= '9' ||
                character == '_' ||
                character == '-';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    private static int CompareProviders(
        IBistroBuilderSaveSectionProvider first,
        IBistroBuilderSaveSectionProvider second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int orderComparison = first.LoadOrder.CompareTo(
            second.LoadOrder
        );

        return orderComparison != 0
            ? orderComparison
            : string.Compare(
                first.SectionId,
                second.SectionId,
                StringComparison.Ordinal
            );
    }

    private static int CompareMigrations(
        IBistroBuilderSaveSectionMigration first,
        IBistroBuilderSaveSectionMigration second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        int sectionComparison = string.Compare(
            first.SectionId,
            second.SectionId,
            StringComparison.Ordinal
        );

        if (sectionComparison != 0)
        {
            return sectionComparison;
        }

        int versionComparison =
            first.FromVersion.CompareTo(second.FromVersion);

        return versionComparison != 0
            ? versionComparison
            : string.Compare(
                first.FromSerializerId,
                second.FromSerializerId,
                StringComparison.Ordinal
            );
    }

    private static int CompareGuards(
        IBistroBuilderSaveOperationGuard first,
        IBistroBuilderSaveOperationGuard second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        return second.Priority.CompareTo(first.Priority);
    }

    private static int CompareParticipants(
        IBistroBuilderSaveOperationParticipant first,
        IBistroBuilderSaveOperationParticipant second
    )
    {
        if (ReferenceEquals(first, second))
        {
            return 0;
        }

        if (first == null)
        {
            return 1;
        }

        if (second == null)
        {
            return -1;
        }

        return second.Priority.CompareTo(first.Priority);
    }

    private sealed class CaptureBatchResult
    {
        public readonly List<BistroBuilderCapturedSaveSection>
            Sections =
                new List<BistroBuilderCapturedSaveSection>(16);

        public bool HasFailed;
        public string ErrorMessage = string.Empty;

        public void Fail(string errorMessage)
        {
            HasFailed = true;
            ErrorMessage = errorMessage ?? string.Empty;
        }
    }

    private sealed class ProviderRoutineResult
    {
        public Exception Exception;
    }

    private sealed class PreparedStoredSection
    {
        public IBistroBuilderSaveSectionProvider Provider { get; }

        public Type StateType { get; }

        public string SerializerId { get; }

        public byte[] Payload { get; }

        public PreparedStoredSection(
            IBistroBuilderSaveSectionProvider provider,
            Type stateType,
            string serializerId,
            byte[] payload
        )
        {
            Provider = provider;
            StateType = stateType ??
                throw new ArgumentNullException(nameof(stateType));
            SerializerId = serializerId ?? string.Empty;
            Payload = payload;
        }
    }
}
