using System;
using System.Collections.Generic;

/// <summary>
/// Puntero pequeño y reemplazable de forma atómica hacia una generación
/// completa de la partida.
/// </summary>
[Serializable]
public sealed class BistroBuilderSaveSlotPointer
{
    public int formatVersion = 1;
    public string generationId = string.Empty;
    public string manifestSha256 = string.Empty;
    public string updatedUtc = string.Empty;
}

/// <summary>
/// Manifiesto de una generación inmutable.
/// </summary>
[Serializable]
public sealed class BistroBuilderSaveManifest
{
    public int formatVersion = 1;
    public int slotIndex;
    public string slotDisplayName = string.Empty;
    public string generationId = string.Empty;
    public string createdUtc = string.Empty;
    public string applicationVersion = string.Empty;
    public string sceneName = string.Empty;
    public string metadataSerializerId = string.Empty;
    public long totalPayloadBytes;
    public List<BistroBuilderSaveSectionManifest> sections =
        new List<BistroBuilderSaveSectionManifest>();
}

/// <summary>
/// Metadatos, formato y checksum de una sección individual.
/// </summary>
[Serializable]
public sealed class BistroBuilderSaveSectionManifest
{
    public string sectionId = string.Empty;
    public int sectionVersion;
    public string serializerId = string.Empty;
    public string relativePath = string.Empty;
    public long byteCount;
    public string sha256 = string.Empty;
}

/// <summary>
/// Metadatos ligeros para construir el menú de partidas sin leer todas
/// las secciones ni reconstruir el mundo.
/// </summary>
public sealed class BistroBuilderSaveSlotSummary
{
    public int SlotIndex { get; }

    public string SlotDisplayName { get; }

    public string GenerationId { get; }

    public string CreatedUtc { get; }

    public string ApplicationVersion { get; }

    public string SceneName { get; }

    public long PayloadBytes { get; }

    public bool RecoveredFromFallback { get; }

    public BistroBuilderSaveSlotSummary(
        int slotIndex,
        string slotDisplayName,
        string generationId,
        string createdUtc,
        string applicationVersion,
        string sceneName,
        long payloadBytes,
        bool recoveredFromFallback
    )
    {
        SlotIndex = slotIndex;
        SlotDisplayName = slotDisplayName ?? string.Empty;
        GenerationId = generationId ?? string.Empty;
        CreatedUtc = createdUtc ?? string.Empty;
        ApplicationVersion = applicationVersion ?? string.Empty;
        SceneName = sceneName ?? string.Empty;
        PayloadBytes = payloadBytes;
        RecoveredFromFallback = recoveredFromFallback;
    }
}

/// <summary>
/// Sección capturada en el hilo principal antes de serializarla.
/// </summary>
public sealed class BistroBuilderCapturedSaveSection
{
    public IBistroBuilderSaveSectionProvider Provider { get; }

    public string SectionId { get; }

    public int SectionVersion { get; }

    public string SerializerId { get; }

    public object State { get; }

    public BistroBuilderCapturedSaveSection(
        IBistroBuilderSaveSectionProvider provider,
        object state
    )
    {
        Provider = provider ??
            throw new ArgumentNullException(nameof(provider));
        SectionId = provider.SectionId ?? string.Empty;
        SectionVersion = provider.SectionVersion;
        SerializerId = provider.SerializerId ?? string.Empty;
        State = state;
    }
}

/// <summary>
/// Contenido ya serializado listo para escribirse.
/// </summary>
public sealed class BistroBuilderSerializedSaveSection
{
    public string SectionId { get; }

    public int SectionVersion { get; }

    public string SerializerId { get; }

    public string FileExtension { get; }

    public byte[] Payload { get; }

    public BistroBuilderSerializedSaveSection(
        string sectionId,
        int sectionVersion,
        string serializerId,
        string fileExtension,
        byte[] payload
    )
    {
        SectionId = sectionId ?? string.Empty;
        SectionVersion = sectionVersion;
        SerializerId = serializerId ?? string.Empty;
        FileExtension = fileExtension ?? ".dat";
        Payload = payload;
    }
}

/// <summary>
/// Solicitud completa de escritura de una generación.
/// </summary>
public sealed class BistroBuilderStorageWriteRequest
{
    public int SlotIndex { get; }

    public string SlotDisplayName { get; }

    public string ApplicationVersion { get; }

    public string SceneName { get; }

    public IReadOnlyList<BistroBuilderSerializedSaveSection>
        Sections { get; }

    public BistroBuilderStorageWriteRequest(
        int slotIndex,
        string slotDisplayName,
        string applicationVersion,
        string sceneName,
        IReadOnlyList<BistroBuilderSerializedSaveSection> sections
    )
    {
        SlotIndex = slotIndex;
        SlotDisplayName = slotDisplayName ?? string.Empty;
        ApplicationVersion = applicationVersion ?? string.Empty;
        SceneName = sceneName ?? string.Empty;
        Sections = sections;
    }
}

/// <summary>
/// Sección verificada y leída desde disco.
/// </summary>
public sealed class BistroBuilderStoredSaveSection
{
    public string SectionId { get; }

    public int SectionVersion { get; }

    public string SerializerId { get; }

    public byte[] Payload { get; }

    public BistroBuilderStoredSaveSection(
        string sectionId,
        int sectionVersion,
        string serializerId,
        byte[] payload
    )
    {
        SectionId = sectionId ?? string.Empty;
        SectionVersion = sectionVersion;
        SerializerId = serializerId ?? string.Empty;
        Payload = payload;
    }
}

/// <summary>
/// Generación validada y lista para migrarse/deserializarse.
/// </summary>
public sealed class BistroBuilderStorageReadPackage
{
    public BistroBuilderSaveManifest Manifest { get; }

    public IReadOnlyList<BistroBuilderStoredSaveSection>
        Sections { get; }

    public bool RecoveredFromFallback { get; }

    public BistroBuilderStorageReadPackage(
        BistroBuilderSaveManifest manifest,
        IReadOnlyList<BistroBuilderStoredSaveSection> sections,
        bool recoveredFromFallback
    )
    {
        Manifest = manifest;
        Sections = sections;
        RecoveredFromFallback = recoveredFromFallback;
    }
}

/// <summary>
/// Estado deserializado asociado a su proveedor.
/// </summary>
public sealed class BistroBuilderLoadedSaveSection
{
    public IBistroBuilderSaveSectionProvider Provider { get; }

    public object State { get; }

    public BistroBuilderLoadedSaveSection(
        IBistroBuilderSaveSectionProvider provider,
        object state
    )
    {
        Provider = provider;
        State = state;
    }
}

public enum BistroBuilderSaveOperationKind
{
    None = 0,
    Save = 1,
    Load = 2,
    Delete = 3
}

public enum BistroBuilderSaveOperationPhase
{
    Idle = 0,
    Validating = 1,
    Capturing = 2,
    Serializing = 3,
    Writing = 4,
    Reading = 5,
    Migrating = 6,
    Deserializing = 7,
    PreparingWorld = 8,
    Applying = 9,
    Finalizing = 10,
    RollingBack = 11,
    Cleaning = 12,
    Completed = 13,
    Failed = 14
}

/// <summary>
/// Resultado público de una operación de guardado, carga o eliminación.
/// </summary>
public sealed class BistroBuilderSaveOperationResult
{
    public bool Succeeded { get; }

    public BistroBuilderSaveOperationKind OperationKind { get; }

    public int SlotIndex { get; }

    public string GenerationId { get; }

    public string Message { get; }

    public long PayloadBytes { get; }

    public double DurationMilliseconds { get; }

    public bool RecoveredFromFallback { get; }

    private BistroBuilderSaveOperationResult(
        bool succeeded,
        BistroBuilderSaveOperationKind operationKind,
        int slotIndex,
        string generationId,
        string message,
        long payloadBytes,
        double durationMilliseconds,
        bool recoveredFromFallback
    )
    {
        Succeeded = succeeded;
        OperationKind = operationKind;
        SlotIndex = slotIndex;
        GenerationId = generationId ?? string.Empty;
        Message = message ?? string.Empty;
        PayloadBytes = payloadBytes;
        DurationMilliseconds = durationMilliseconds;
        RecoveredFromFallback = recoveredFromFallback;
    }

    public static BistroBuilderSaveOperationResult Success(
        BistroBuilderSaveOperationKind operationKind,
        int slotIndex,
        string generationId,
        string message,
        long payloadBytes,
        double durationMilliseconds,
        bool recoveredFromFallback = false
    )
    {
        return new BistroBuilderSaveOperationResult(
            true,
            operationKind,
            slotIndex,
            generationId,
            message,
            payloadBytes,
            durationMilliseconds,
            recoveredFromFallback
        );
    }

    public static BistroBuilderSaveOperationResult Failure(
        BistroBuilderSaveOperationKind operationKind,
        int slotIndex,
        string message,
        double durationMilliseconds
    )
    {
        return new BistroBuilderSaveOperationResult(
            false,
            operationKind,
            slotIndex,
            string.Empty,
            message,
            0L,
            durationMilliseconds,
            false
        );
    }
}

/// <summary>
/// Resultado interno de escritura del almacenamiento.
/// </summary>
public sealed class BistroBuilderStorageWriteResult
{
    public bool Succeeded { get; }

    public string GenerationId { get; }

    public long PayloadBytes { get; }

    public string ErrorMessage { get; }

    private BistroBuilderStorageWriteResult(
        bool succeeded,
        string generationId,
        long payloadBytes,
        string errorMessage
    )
    {
        Succeeded = succeeded;
        GenerationId = generationId ?? string.Empty;
        PayloadBytes = payloadBytes;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public static BistroBuilderStorageWriteResult Success(
        string generationId,
        long payloadBytes
    )
    {
        return new BistroBuilderStorageWriteResult(
            true,
            generationId,
            payloadBytes,
            string.Empty
        );
    }

    public static BistroBuilderStorageWriteResult Failure(
        string errorMessage
    )
    {
        return new BistroBuilderStorageWriteResult(
            false,
            string.Empty,
            0L,
            errorMessage
        );
    }
}

/// <summary>
/// Resultado interno de lectura del almacenamiento.
/// </summary>
public sealed class BistroBuilderStorageReadResult
{
    public bool Succeeded { get; }

    public BistroBuilderStorageReadPackage Package { get; }

    public string ErrorMessage { get; }

    private BistroBuilderStorageReadResult(
        bool succeeded,
        BistroBuilderStorageReadPackage package,
        string errorMessage
    )
    {
        Succeeded = succeeded;
        Package = package;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    public static BistroBuilderStorageReadResult Success(
        BistroBuilderStorageReadPackage package
    )
    {
        return new BistroBuilderStorageReadResult(
            true,
            package,
            string.Empty
        );
    }

    public static BistroBuilderStorageReadResult Failure(
        string errorMessage
    )
    {
        return new BistroBuilderStorageReadResult(
            false,
            null,
            errorMessage
        );
    }
}
