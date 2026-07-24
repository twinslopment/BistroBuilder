using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ventana técnica temporal para probar la carta runtime antes de construir
/// su UI final. Todas las acciones utilizan la API pública del servicio.
/// </summary>
public sealed class BistroBuilderMenuDebugWindow : EditorWindow
{
    private readonly List<BistroBuilderMenuItemRuntimeState> snapshot =
        new List<BistroBuilderMenuItemRuntimeState>(32);

    private BistroBuilderRestaurantMenuService menuService;
    private BistroBuilderDishCatalogService catalogService;
    private Vector2 scroll;
    private int selectedIndex;
    private int editedPriceCents;
    private BistroBuilderMealServiceAvailability editedAvailability;
    private string editedDishId = string.Empty;
    private string statusMessage = string.Empty;

    [MenuItem("Tools/Bistro Builder/Menu/Menu Debug", false, 130)]
    private static void Open()
    {
        GetWindow<BistroBuilderMenuDebugWindow>(
            "BistroBuilder Menu"
        );
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        ResolveServices();
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField(
            "BistroBuilder 367A — Carta runtime",
            EditorStyles.boldLabel
        );
        EditorGUILayout.Space(4f);

        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Entra en Play Mode para modificar la carta runtime.",
                MessageType.Info
            );

            if (GUILayout.Button("Localizar servicios"))
            {
                ResolveServices();
            }

            DrawStatus();
            return;
        }

        if (menuService == null || catalogService == null)
        {
            EditorGUILayout.HelpBox(
                "No se han localizado los servicios 367A en la escena.",
                MessageType.Error
            );

            if (GUILayout.Button("Reintentar"))
            {
                ResolveServices();
            }

            return;
        }

        RefreshSnapshot();

        EditorGUILayout.LabelField(
            "Platos canónicos",
            catalogService.DefinitionCount.ToString()
        );
        EditorGUILayout.LabelField(
            "Entradas en carta",
            menuService.ItemCount.ToString()
        );
        EditorGUILayout.LabelField(
            "Revisión runtime",
            menuService.Revision.ToString()
        );

        EditorGUILayout.Space(6f);

        if (GUILayout.Button("Restaurar carta desde catálogo"))
        {
            ApplyResult(menuService.ResetToCatalogDefaults());
            RefreshSnapshot();
        }

        if (snapshot.Count == 0)
        {
            EditorGUILayout.HelpBox(
                "La carta activa está vacía.",
                MessageType.Warning
            );
            DrawStatus();
            return;
        }

        selectedIndex = Mathf.Clamp(
            selectedIndex,
            0,
            snapshot.Count - 1
        );

        string[] labels = new string[snapshot.Count];

        for (int index = 0; index < snapshot.Count; index++)
        {
            BistroBuilderMenuItemRuntimeState item = snapshot[index];
            labels[index] = item.DisplayOrder + " — " + item.DishId;
        }

        int previousIndex = selectedIndex;
        selectedIndex = EditorGUILayout.Popup(
            "Plato",
            selectedIndex,
            labels
        );

        BistroBuilderMenuItemRuntimeState selected =
            snapshot[selectedIndex];

        if (selectedIndex != previousIndex)
        {
            LoadEditorValues(selected);
        }

        DrawSelectedItem(selected);
        DrawStatus();
    }

    private void DrawSelectedItem(
        BistroBuilderMenuItemRuntimeState selected
    )
    {
        if (catalogService.TryGetDefinition(
                selected.DishId,
                out BistroBuilderDishDefinition definition
            ))
        {
            EditorGUILayout.LabelField(
                "Nombre",
                definition.DisplayName
            );
            EditorGUILayout.LabelField(
                "Categoría",
                definition.Category.ToString()
            );
            EditorGUILayout.LabelField(
                "Pase",
                definition.Course.ToString()
            );
            EditorGUILayout.LabelField(
                "Estación futura",
                definition.RequiredStation.ToString()
            );
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Estado actual", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Precio",
            FormatPrice(selected.CurrentPriceCents)
        );
        EditorGUILayout.LabelField(
            "Servicios",
            selected.AvailableServices.ToString()
        );
        EditorGUILayout.LabelField(
            "Activo",
            selected.Enabled.ToString()
        );
        EditorGUILayout.LabelField(
            "Desbloqueado",
            selected.Unlocked.ToString()
        );
        EditorGUILayout.LabelField(
            "Agotado",
            selected.ManuallySoldOut.ToString()
        );
        EditorGUILayout.LabelField(
            "Plato firma",
            selected.SignatureDish.ToString()
        );

        EditorGUILayout.Space(6f);

        editedPriceCents = EditorGUILayout.IntField(
            "Precio en céntimos",
            editedPriceCents
        );
        editedAvailability =
            (BistroBuilderMealServiceAvailability)
            EditorGUILayout.EnumFlagsField(
                "Servicios",
                editedAvailability
            );

        if (GUILayout.Button("Aplicar precio y servicios"))
        {
            BistroBuilderMenuMutationResult price =
                menuService.TrySetPriceCents(
                    selected.DishId,
                    editedPriceCents
                );

            if (!price.Succeeded &&
                price.FailureReason !=
                    BistroBuilderMenuMutationFailureReason.NoChange)
            {
                ApplyResult(price);
                return;
            }

            ApplyResult(
                menuService.TrySetAvailability(
                    selected.DishId,
                    editedAvailability
                )
            );
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(selected.Enabled ? "Desactivar" : "Activar"))
        {
            ApplyResult(
                menuService.TrySetEnabled(
                    selected.DishId,
                    !selected.Enabled
                )
            );
        }

        if (GUILayout.Button(
                selected.ManuallySoldOut
                    ? "Marcar disponible"
                    : "Marcar agotado"
            ))
        {
            ApplyResult(
                menuService.TrySetManuallySoldOut(
                    selected.DishId,
                    !selected.ManuallySoldOut
                )
            );
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
                selected.SignatureDish
                    ? "Quitar firma"
                    : "Marcar firma"
            ))
        {
            ApplyResult(
                menuService.TrySetSignatureDish(
                    selected.DishId,
                    !selected.SignatureDish
                )
            );
        }

        if (GUILayout.Button(
                selected.Unlocked
                    ? "Bloquear"
                    : "Desbloquear"
            ))
        {
            ApplyResult(
                menuService.TrySetUnlocked(
                    selected.DishId,
                    !selected.Unlocked
                )
            );
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Subir"))
        {
            ApplyResult(
                menuService.TryMoveDish(
                    selected.DishId,
                    selected.DisplayOrder - 1
                )
            );
        }

        if (GUILayout.Button("Bajar"))
        {
            ApplyResult(
                menuService.TryMoveDish(
                    selected.DishId,
                    selected.DisplayOrder + 1
                )
            );
        }

        EditorGUILayout.EndHorizontal();
    }

    private void ResolveServices()
    {
        menuService = FindSceneComponent<
            BistroBuilderRestaurantMenuService
        >();
        catalogService = FindSceneComponent<
            BistroBuilderDishCatalogService
        >();
        statusMessage = menuService != null && catalogService != null
            ? "Servicios 367A localizados."
            : "No se encontraron todos los servicios 367A.";
        RefreshSnapshot();
        Repaint();
    }

    private void RefreshSnapshot()
    {
        snapshot.Clear();

        if (menuService == null)
        {
            statusMessage = "No se encontró BistroBuilderRestaurantMenuService.";
            return;
        }

        if (!menuService.TryGetSnapshot(snapshot, out string error))
        {
            if (!string.IsNullOrEmpty(error))
            {
                statusMessage = error;
            }

            return;
        }

        if (snapshot.Count > 0)
        {
            selectedIndex = Mathf.Clamp(
                selectedIndex,
                0,
                snapshot.Count - 1
            );

            BistroBuilderMenuItemRuntimeState selected =
                snapshot[selectedIndex];

            if (!string.Equals(
                    editedDishId,
                    selected.DishId,
                    System.StringComparison.Ordinal
                ))
            {
                LoadEditorValues(selected);
            }
        }
    }

    private void LoadEditorValues(
        BistroBuilderMenuItemRuntimeState item
    )
    {
        if (item == null)
        {
            return;
        }

        editedDishId = item.DishId;
        editedPriceCents = item.CurrentPriceCents;
        editedAvailability = item.AvailableServices;
    }

    private void ApplyResult(BistroBuilderMenuMutationResult result)
    {
        statusMessage = result.Message;
        editedDishId = string.Empty;
        RefreshSnapshot();
        Repaint();
    }

    private void DrawStatus()
    {
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(
                statusMessage,
                MessageType.None
            );
        }
    }

    private void HandlePlayModeStateChanged(
        PlayModeStateChange state
    )
    {
        if (state == PlayModeStateChange.EnteredPlayMode ||
            state == PlayModeStateChange.EnteredEditMode)
        {
            ResolveServices();
        }
    }

    private static T FindSceneComponent<T>() where T : Component
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();

        for (int index = 0; index < candidates.Length; index++)
        {
            T candidate = candidates[index];

            if (candidate != null &&
                candidate.gameObject.scene.IsValid() &&
                !EditorUtility.IsPersistent(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string FormatPrice(int cents)
    {
        return (cents / 100f).ToString("0.00") + " €";
    }
}
