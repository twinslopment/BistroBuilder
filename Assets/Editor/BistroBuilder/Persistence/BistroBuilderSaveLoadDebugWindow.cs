using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana provisional de pruebas para guardar y cargar sin depender
/// todavía del menú principal definitivo.
/// </summary>
public sealed class BistroBuilderSaveLoadDebugWindow : EditorWindow
{
    private const string MenuPath =
        "Tools/Bistro Builder/Persistence/Save & Load Debug";

    private int slotIndex = 1;
    private string slotDisplayName = "Partida de prueba";
    private Vector2 scroll;

    [MenuItem(MenuPath, false, 130)]
    private static void Open()
    {
        BistroBuilderSaveLoadDebugWindow window =
            GetWindow<BistroBuilderSaveLoadDebugWindow>();
        window.titleContent = new GUIContent("BB Save/Load");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update -= HandleEditorUpdate;
        EditorApplication.update += HandleEditorUpdate;
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
            "BistroBuilder 366 — Persistencia",
            EditorStyles.boldLabel
        );
        EditorGUILayout.HelpBox(
            "Esta ventana es solo para validar la base técnica. " +
            "El menú final de partidas se implementará en UI/UX.",
            MessageType.Info
        );

        slotIndex = EditorGUILayout.IntSlider(
            "Slot",
            slotIndex,
            1,
            999
        );
        slotDisplayName = EditorGUILayout.TextField(
            "Nombre",
            slotDisplayName
        );

        EditorGUILayout.Space(8f);

        BistroBuilderSaveGameService service = FindService();

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode para guardar o cargar una partida.",
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

    private void DrawServiceControls(
        BistroBuilderSaveGameService service
    )
    {
        bool busy = service.IsBusy;

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
        return UnityEngine.Object.FindFirstObjectByType<
            BistroBuilderSaveGameService
        >(
            FindObjectsInactive.Include
        );
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
