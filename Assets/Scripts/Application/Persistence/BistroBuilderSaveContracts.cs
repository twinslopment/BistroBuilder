using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Contrato que permite añadir una sección independiente a una partida.
///
/// Cada sistema futuro —personal, inventario, economía, carta o
/// progresión— implementará este contrato sin conocer el almacenamiento
/// físico. Cada sección puede elegir su propio serializador.
/// </summary>
public interface IBistroBuilderSaveSectionProvider
{
    string SectionId { get; }

    int SectionVersion { get; }

    int LoadOrder { get; }

    bool IsRequired { get; }

    Type StateType { get; }

    string SerializerId { get; }

    IEnumerator CaptureState(
        BistroBuilderSaveCaptureContext context
    );

    bool ValidateState(
        object state,
        out string error
    );

    IEnumerator PrepareForLoad(
        BistroBuilderSaveLoadContext context
    );

    IEnumerator ApplyState(
        object state,
        BistroBuilderSaveLoadContext context
    );

    void FinalizeLoad(
        BistroBuilderSaveLoadContext context
    );
}

/// <summary>
/// Migración pura entre dos versiones consecutivas de una sección.
///
/// También puede cambiar el serializador de la sección, por ejemplo de
/// JSON a un formato binario, sin modificar el sistema propietario.
/// </summary>
public interface IBistroBuilderSaveSectionMigration
{
    string SectionId { get; }

    int FromVersion { get; }

    int ToVersion { get; }

    string FromSerializerId { get; }

    string ToSerializerId { get; }

    bool TryMigrate(
        byte[] sourcePayload,
        out byte[] migratedPayload,
        out string error
    );
}

/// <summary>
/// Regla que puede impedir un guardado o una carga en un estado inseguro.
/// </summary>
public interface IBistroBuilderSaveOperationGuard
{
    int Priority { get; }

    bool CanSave(out string rejectionMessage);

    bool CanLoad(out string rejectionMessage);
}

/// <summary>
/// Participante opcional del cerrojo global de snapshot.
///
/// Permite que futuros sistemas pausen el reloj, congelen IA o cierren
/// escrituras mientras se captura una imagen consistente de la partida.
/// </summary>
public interface IBistroBuilderSaveOperationParticipant
{
    int Priority { get; }

    bool TryBeginSaveOperation(
        BistroBuilderSaveOperationKind operationKind,
        out string rejectionMessage
    );

    void EndSaveOperation(
        BistroBuilderSaveOperationKind operationKind,
        bool succeeded
    );
}

/// <summary>
/// Serializador binario intercambiable.
///
/// Incluso JSON se expresa como bytes UTF-8, lo que permite mezclar en
/// una misma partida secciones JSON, binarias u otros formatos.
/// </summary>
public interface IBistroBuilderSaveSerializer
{
    string SerializerId { get; }

    string FileExtension { get; }

    byte[] Serialize(
        object value,
        bool prettyPrint
    );

    object Deserialize(
        byte[] serializedValue,
        Type targetType
    );
}

/// <summary>
/// Almacenamiento intercambiable. La lógica de juego no depende de
/// archivos locales, Steam Cloud, consola ni un proveedor remoto.
/// </summary>
public interface IBistroBuilderSaveStorage
{
    string RootPath { get; }

    Task<BistroBuilderStorageWriteResult> WriteGenerationAsync(
        BistroBuilderStorageWriteRequest request,
        CancellationToken cancellationToken
    );

    Task<BistroBuilderStorageReadResult>
        ReadLatestValidGenerationAsync(
            int slotIndex,
            CancellationToken cancellationToken
        );

    Task<IReadOnlyList<BistroBuilderSaveSlotSummary>>
        ReadAllSlotSummariesAsync(
            CancellationToken cancellationToken
        );

    Task<bool> DeleteSlotAsync(
        int slotIndex,
        CancellationToken cancellationToken
    );

    bool SlotExists(int slotIndex);
}

/// <summary>
/// Contexto mutable de captura. Permite capturas incrementales mediante
/// corutinas y comunica el resultado al orquestador.
/// </summary>
public sealed class BistroBuilderSaveCaptureContext
{
    private readonly Func<bool> cancellationRequested;

    public int SlotIndex { get; }

    public bool IsCancellationRequested =>
        cancellationRequested != null && cancellationRequested();

    public object State { get; private set; }

    public bool HasFailed { get; private set; }

    public string ErrorMessage { get; private set; }

    public BistroBuilderSaveCaptureContext(
        int slotIndex,
        Func<bool> cancellationRequested = null
    )
    {
        SlotIndex = slotIndex;
        this.cancellationRequested = cancellationRequested;
        ErrorMessage = string.Empty;
    }

    public void Complete(object state)
    {
        if (HasFailed)
        {
            return;
        }

        State = state;
    }

    public void Fail(string errorMessage)
    {
        HasFailed = true;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "La captura de la sección ha fallado."
            : errorMessage;
    }
}

/// <summary>
/// Contexto compartido durante una carga o una restauración de seguridad.
/// </summary>
public sealed class BistroBuilderSaveLoadContext
{
    private readonly Func<bool> cancellationRequested;

    public int SlotIndex { get; }

    public bool IsCancellationRequested =>
        cancellationRequested != null && cancellationRequested();

    public bool IsRollback { get; }

    public int ObjectsPerFrame { get; }

    public bool HasFailed { get; private set; }

    public string ErrorMessage { get; private set; }

    public BistroBuilderSaveLoadContext(
        int slotIndex,
        bool isRollback,
        int objectsPerFrame,
        Func<bool> cancellationRequested = null
    )
    {
        SlotIndex = slotIndex;
        IsRollback = isRollback;
        ObjectsPerFrame = Math.Max(1, objectsPerFrame);
        this.cancellationRequested = cancellationRequested;
        ErrorMessage = string.Empty;
    }

    public void Fail(string errorMessage)
    {
        if (HasFailed)
        {
            return;
        }

        HasFailed = true;
        ErrorMessage = string.IsNullOrWhiteSpace(errorMessage)
            ? "La aplicación de la sección ha fallado."
            : errorMessage;
    }
}
