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
/// Orden opcional por fase. Permite aplicar datos generales pronto y
/// restaurar el estado operativo al final, después de reconstruir todas
/// las entidades de un futuro servicio activo.
/// </summary>
public interface IBistroBuilderSaveSectionPhaseOrdering
{
    int PrepareOrder { get; }

    int ApplyOrder { get; }

    int FinalizeOrder { get; }
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

    /// <summary>
    /// Datos temporales compartidos por todas las secciones de una misma
    /// captura. Permite usar un único checkpointId en game.general,
    /// clientes, comandas y cocina.
    /// </summary>
    public BistroBuilderSaveOperationBag SharedData { get; }

    public bool HasFailed { get; private set; }

    public string ErrorMessage { get; private set; }

    public BistroBuilderSaveCaptureContext(
        int slotIndex,
        Func<bool> cancellationRequested = null,
        BistroBuilderSaveOperationBag sharedData = null
    )
    {
        SlotIndex = slotIndex;
        this.cancellationRequested = cancellationRequested;
        SharedData = sharedData ?? new BistroBuilderSaveOperationBag();
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

    /// <summary>
    /// Registro compartido de entidades reconstruidas. Las secciones de
    /// clientes, comandas o personal podrán resolver por ID mesas,
    /// asientos y otros objetos creados por secciones anteriores.
    /// </summary>
    public BistroBuilderSaveReferenceRegistry References { get; }

    /// <summary>
    /// Bolsa de datos temporal compartida durante una única carga.
    /// Nunca se persiste ni sobrevive a la operación.
    /// </summary>
    public BistroBuilderSaveOperationBag SharedData { get; }

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
        References = new BistroBuilderSaveReferenceRegistry();
        SharedData = new BistroBuilderSaveOperationBag();
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

/// <summary>
/// Dominios estables para referencias cruzadas entre secciones.
/// </summary>
public static class BistroBuilderSaveReferenceDomains
{
    public const string GameState = "game.state";
    public const string GameClock = "game.clock";
    public const string ServiceState = "service.state";
    public const string RestaurantPlaceable = "restaurant.placeable";
    public const string RestaurantTable = "restaurant.table";
    public const string RestaurantSeat = "restaurant.seat";
}

/// <summary>
/// Registro de referencias reconstruidas, indexadas por dominio e ID
/// persistente. No serializa objetos Unity: solo sirve durante la carga.
/// </summary>
public sealed class BistroBuilderSaveReferenceRegistry
{
    private readonly Dictionary<string, object> references =
        new Dictionary<string, object>(StringComparer.Ordinal);

    public int Count => references.Count;

    public bool TryRegister(
        string domain,
        string persistentId,
        object value,
        bool replaceExisting = false
    )
    {
        if (value == null ||
            !TryBuildKey(domain, persistentId, out string key))
        {
            return false;
        }

        if (references.ContainsKey(key) && !replaceExisting)
        {
            return false;
        }

        references[key] = value;
        return true;
    }

    public bool TryResolve<T>(
        string domain,
        string persistentId,
        out T value
    ) where T : class
    {
        value = null;

        if (!TryBuildKey(domain, persistentId, out string key) ||
            !references.TryGetValue(key, out object stored))
        {
            return false;
        }

        value = stored as T;
        return value != null;
    }

    public bool Contains(string domain, string persistentId)
    {
        return TryBuildKey(domain, persistentId, out string key) &&
               references.ContainsKey(key);
    }

    public void Clear()
    {
        references.Clear();
    }

    private static bool TryBuildKey(
        string domain,
        string persistentId,
        out string key
    )
    {
        key = string.Empty;

        if (string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(persistentId))
        {
            return false;
        }

        key = domain.Trim().ToLowerInvariant() + "|" +
              persistentId.Trim().ToLowerInvariant();
        return true;
    }
}

/// <summary>
/// Datos temporales compartidos entre proveedores durante una carga.
/// </summary>
public sealed class BistroBuilderSaveOperationBag
{
    private readonly Dictionary<string, object> values =
        new Dictionary<string, object>(StringComparer.Ordinal);

    public int Count => values.Count;

    public void Set(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "La clave compartida no puede estar vacía.",
                nameof(key)
            );
        }

        values[key.Trim().ToLowerInvariant()] = value;
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(key) ||
            !values.TryGetValue(
                key.Trim().ToLowerInvariant(),
                out object stored
            ) ||
            !(stored is T typed))
        {
            return false;
        }

        value = typed;
        return true;
    }

    public void Clear()
    {
        values.Clear();
    }
}

