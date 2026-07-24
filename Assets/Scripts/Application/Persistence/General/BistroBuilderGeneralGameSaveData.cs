using System;
using System.Collections.Generic;

/// <summary>
/// Tipo de fotografía lógica contenida en una generación.
///
/// ClosedRestaurant puede restaurarse con la base actual.
/// ActiveService requiere además las secciones de runtime que persisten
/// clientes, comandas, cocina, personal y tareas en curso.
/// </summary>
public enum BistroBuilderSaveSnapshotMode
{
    ClosedRestaurant = 0,
    ActiveService = 1
}

/// <summary>
/// Estado general, pequeño y estable de una partida.
///
/// No contiene inventario, finanzas detalladas, personal ni entidades de
/// servicio. Esos sistemas tendrán secciones propias y referenciarán esta
/// identidad de partida.
/// </summary>
[Serializable]
public sealed class BistroBuilderGeneralGameSaveData
{
    public int schemaVersion = 1;

    public string gameId = string.Empty;
    public string restaurantName = string.Empty;
    public string createdUtc = string.Empty;
    public string capturedUtc = string.Empty;

    public int dayIndex = 1;
    public int calendarYear = 1;
    public int calendarMonth = 1;
    public int calendarDay = 1;

    public string progressionStageId = "new_restaurant";
    public int progressionLevel = 1;

    public int clockHour = 8;
    public int clockMinute;
    public float clockAccumulatedMinutes;
    public float clockSpeedMultiplier = 1f;
    public bool clockIsPaused;

    public int serviceState;
    public int snapshotMode;

    /// <summary>
    /// Identificador reservado para el futuro checkpoint de servicio.
    /// Permanecerá vacío en fotografías con el restaurante cerrado.
    /// </summary>
    public string activeServiceCheckpointId = string.Empty;

    /// <summary>
    /// Secciones que deben existir para restaurar una fotografía activa.
    /// Permite detectar de forma segura contenido incompleto o de una
    /// versión futura del juego.
    /// </summary>
    public List<string> requiredRuntimeSectionIds =
        new List<string>();
}
