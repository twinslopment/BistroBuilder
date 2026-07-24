using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana provisional para validar guardado, carga y estado general sin
/// depender todavía del menú principal definitivo.
/// </summary>
public sealed class BistroBuilderSaveLoadDebugWindow : EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Persistence/Save & Load Debug";

    private int slotIndex = 1;
    private string slotDisplayName = "Partida de prueba";
    private Vector2 scroll;

    private bool generalFieldsInitialized;
    private string restaurantName = "Mi restaurante";
    private int dayIndex = 1;
    private int calendarYear = 1;
    private int calendarMonth = 1;
    private int calendarDay = 1;
    private string progressionStageId = "new_restaurant";
    private int progressionLevel = 1;
    private int clockHour = 8;
    private int clockMinute;
    private float clockFraction;
    private float speedMultiplier = 1f;
    private bool clockPaused;

    [MenuItem(MenuPath, false, 130)]
    private static void Open()
    {
        BistroBuilderSaveLoadDebugWindow window =
            GetWindow<BistroBuilderSaveLoadDebugWindow>();
        window.titleContent = new GUIContent("BB Save/Load");
        window.minSize = new Vector2(440f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update -= HandleEditorUpdate;
        EditorApplication.update += HandleEditorUpdate;
        generalFieldsInitialized = false;
    }

    private void OnDisable()
    {
        EditorApplication.update -= HandleEditorUpdate;
    }

    private void HandleEditorUpdate()
    {
        Repaint();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField(
            "BistroBuilder 366B — Persistencia general",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Ventana temporal de validación. El menú final de partidas " +
            "se implementará en UI/UX.",
            MessageType.Info
        );

        slotIndex = EditorGUILayout.IntSlider(
            "Slot",
            slotIndex,
            1,
            999
        );
        slotDisplayName = EditorGUILayout.TextField(
            "Nombre del slot",
            slotDisplayName
        );

        EditorGUILayout.Space(8f);

        BistroBuilderSaveGameService service = FindService();
        BistroBuilderGeneralGameStateService generalState =
            FindGeneralState();
        GameClock gameClock = FindGameClock();
        RestaurantServiceStateService serviceState =
            FindServiceState();

        if (!EditorApplication.isPlaying)
        {
            generalFieldsInitialized = false;
            EditorGUILayout.HelpBox(
                "Entra en Play Mode para guardar, cargar o modificar " +
                "el estado de prueba.",
                MessageType.Warning
            );
        }
        else if (service == null)
        {
            EditorGUILayout.HelpBox(
                "No se encontró BistroBuilderSaveGameService.",
                MessageType.Error
            );
        }
        else
        {
            DrawGeneralStateControls(
                generalState,
                gameClock,
                serviceState
            );
            EditorGUILayout.Space(12f);
            DrawServiceControls(service);
        }

        EditorGUILayout.Space(12f);

        using (new EditorGUI.DisabledScope(service == null))
        {
            if (GUILayout.Button("Abrir carpeta de partidas"))
            {
                EditorUtility.RevealInFinder(
                    service != null
                        ? service.SaveRootPath
                        : Application.persistentDataPath
                );
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawGeneralStateControls(
        BistroBuilderGeneralGameStateService generalState,
        GameClock gameClock,
        RestaurantServiceStateService serviceState
    )
    {
        EditorGUILayout.LabelField(
            "Estado general de prueba",
            EditorStyles.boldLabel
        );

        if (generalState == null || gameClock == null || serviceState == null)
        {
            EditorGUILayout.HelpBox(
                "Falta el estado general, GameClock o el estado de servicio.",
                MessageType.Error
            );
            return;
        }

        if (!generalFieldsInitialized)
        {
            ReadGeneralFields(generalState, gameClock);
        }

        EditorGUILayout.LabelField("GameId", generalState.GameId);
        EditorGUILayout.LabelField(
            "Servicio",
            serviceState.CurrentState.ToString()
        );

        restaurantName = EditorGUILayout.TextField(
            "Restaurante",
            restaurantName
        );
        dayIndex = EditorGUILayout.IntField("Día de juego", dayIndex);

        using (new EditorGUILayout.HorizontalScope())
        {
            calendarYear = EditorGUILayout.IntField(
                "Año",
                calendarYear
            );
            calendarMonth = EditorGUILayout.IntField(
                "Mes",
                calendarMonth
            );
            calendarDay = EditorGUILayout.IntField(
                "Día",
                calendarDay
            );
        }

        progressionStageId = EditorGUILayout.TextField(
            "Etapa",
            progressionStageId
        );
        progressionLevel = EditorGUILayout.IntField(
            "Nivel",
            progressionLevel
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            clockHour = EditorGUILayout.IntField("Hora", clockHour);
            clockMinute = EditorGUILayout.IntField("Minuto", clockMinute);
        }

        clockFraction = EditorGUILayout.Slider(
            "Fracción de minuto",
            clockFraction,
            0f,
            0.999f
        );
        speedMultiplier = EditorGUILayout.FloatField(
            "Velocidad",
            speedMultiplier
        );
        clockPaused = EditorGUILayout.Toggle(
            "Pausa",
            clockPaused
        );

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Leer actual"))
            {
                ReadGeneralFields(generalState, gameClock);
            }

            if (GUILayout.Button("Aplicar prueba"))
            {
                ApplyGeneralFields(generalState, gameClock);
            }
        }
    }

    private void ReadGeneralFields(
        BistroBuilderGeneralGameStateService generalState,
        GameClock gameClock
    )
    {
        restaurantName = generalState.RestaurantName;
        dayIndex = generalState.DayIndex;
        calendarYear = generalState.CalendarYear;
        calendarMonth = generalState.CalendarMonth;
        calendarDay = generalState.CalendarDay;
        progressionStageId = generalState.ProgressionStageId;
        progressionLevel = generalState.ProgressionLevel;
        clockHour = gameClock.Hour;
        clockMinute = gameClock.Minute;
        clockFraction = gameClock.AccumulatedMinutes;
        speedMultiplier = gameClock.SpeedMultiplier;
        clockPaused = gameClock.IsPaused;
        generalFieldsInitialized = true;
    }

    private void ApplyGeneralFields(
        BistroBuilderGeneralGameStateService generalState,
        GameClock gameClock
    )
    {
        bool generalApplied =
            generalState.TrySetRestaurantName(restaurantName) &&
            generalState.TrySetCalendar(
                dayIndex,
                calendarYear,
                calendarMonth,
                calendarDay
            ) &&
            generalState.TrySetProgression(
                progressionStageId,
                progressionLevel
            );

        bool clockApplied = gameClock.TryRestoreState(
            clockHour,
            clockMinute,
            speedMultiplier,
            clockPaused,
            clockFraction,
            true
        );

        if (!generalApplied || !clockApplied)
        {
            ShowRejection(
                "Los datos introducidos no forman un estado válido."
            );
            return;
        }

        ReadGeneralFields(generalState, gameClock);
    }

    private void DrawServiceControls(
        BistroBuilderSaveGameService service
    )
    {
        bool busy = service.IsBusy;

        EditorGUILayout.LabelField(
            "Operación de persistencia",
            EditorStyles.boldLabel
        );
        EditorGUILayout.LabelField(
            "Estado",
            busy ? "Ocupado" : "Disponible"
        );
        EditorGUILayout.LabelField(
            "Fase",
            service.CurrentPhase.ToString()
        );
        EditorGUI.ProgressBar(
            EditorGUILayout.GetControlRect(false, 20f),
            service.CurrentProgress,
            service.CurrentStatusMessage
        );

        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(busy))
            {
                if (GUILayout.Button("Guardar"))
                {
                    if (!service.TrySaveSlot(
                            slotIndex,
                            slotDisplayName,
                            out string rejection
                        ))
                    {
                        ShowRejection(rejection);
                    }
                }

                if (GUILayout.Button("Cargar"))
                {
                    if (!service.SlotExists(slotIndex))
                    {
                        ShowRejection(
                            "El slot " + slotIndex + " no existe."
                        );
                    }
                    else if (!service.TryLoadSlot(
                                 slotIndex,
                                 out string rejection
                             ))
                    {
                        ShowRejection(rejection);
                    }
                }

                if (GUILayout.Button("Eliminar"))
                {
                    bool confirmed = EditorUtility.DisplayDialog(
                        "Bistro Builder",
                        "¿Eliminar completamente el slot " +
                        slotIndex + "?",
                        "Eliminar",
                        "Cancelar"
                    );

                    if (confirmed &&
                        !service.TryDeleteSlot(
                            slotIndex,
                            out string rejection
                        ))
                    {
                        ShowRejection(rejection);
                    }
                }
            }
        }

        using (new EditorGUI.DisabledScope(!busy))
        {
            if (GUILayout.Button("Cancelar operación"))
            {
                service.CancelActiveOperation();
            }
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField(
            "Slot existente",
            service.SlotExists(slotIndex) ? "Sí" : "No"
        );
        EditorGUILayout.LabelField(
            "Carpeta",
            service.SaveRootPath,
            EditorStyles.wordWrappedLabel
        );

        BistroBuilderSaveOperationResult result = service.LastResult;

        if (result == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            result.Message +
            "\nDuración: " +
            result.DurationMilliseconds.ToString("F1") +
            " ms" +
            "\nPayload: " + result.PayloadBytes + " bytes" +
            (result.RecoveredFromFallback
                ? "\nRecuperada desde respaldo."
                : string.Empty),
            result.Succeeded
                ? MessageType.Info
                : MessageType.Error
        );
    }

    private static BistroBuilderSaveGameService FindService()
    {
        return Object.FindFirstObjectByType<
            BistroBuilderSaveGameService
        >(FindObjectsInactive.Include);
    }

    private static BistroBuilderGeneralGameStateService FindGeneralState()
    {
        return Object.FindFirstObjectByType<
            BistroBuilderGeneralGameStateService
        >(FindObjectsInactive.Include);
    }

    private static GameClock FindGameClock()
    {
        return Object.FindFirstObjectByType<GameClock>(
            FindObjectsInactive.Include
        );
    }

    private static RestaurantServiceStateService FindServiceState()
    {
        return Object.FindFirstObjectByType<
            RestaurantServiceStateService
        >(FindObjectsInactive.Include);
    }

    private static void ShowRejection(string message)
    {
        EditorUtility.DisplayDialog(
            "Bistro Builder",
            string.IsNullOrWhiteSpace(message)
                ? "La operación no está disponible."
                : message,
            "Aceptar"
        );
    }
}
