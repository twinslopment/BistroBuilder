using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.SmartAssets.Editor
{
    internal sealed class BistroBuilderSmartAssetWindow
        : EditorWindow
    {
        private GameObject model;
        private string modelPath = string.Empty;
        private BistroBuilderSmartAssetManifest manifest;
        private IReadOnlyList<SmartAssetMessage> messages;
        private Vector2 validationScroll;
        private Vector2 variantScroll;

        [MenuItem(
            "Tools/Configurador de Assets/Smart Assets")]
        private static void Open()
        {
            var window =
                GetWindow<
                    BistroBuilderSmartAssetWindow>();

            window.titleContent =
                new GUIContent(
                    "Configurador de Assets");

            window.minSize =
                new Vector2(580f, 520f);

            window.Refresh();
            window.Show();
        }

        private void OnSelectionChange()
        {
            Refresh();
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawSelectedModel();

            if (model == null)
            {
                EditorGUILayout.HelpBox(
                    "Selecciona el FBX principal dentro de " +
                    "Assets/Art/Blender/Placeables/.",
                    MessageType.Warning);

                return;
            }

            if (manifest == null)
            {
                EditorGUILayout.HelpBox(
                    "Falta el manifiesto .bbasset.json " +
                    "junto al FBX.",
                    MessageType.Error);

                return;
            }

            DrawDimensionalBrief();
            DrawConfiguredVariants();
            DrawActions();
            DrawValidation();
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    "BistroBuilder · Configurador de Assets",
                    EditorStyles.boldLabel);

                GUILayout.FlexibleSpace();

                EditorGUILayout.LabelField(
                    $"v{BistroBuilderSmartAssetsVersion.Current}",
                    EditorStyles.miniLabel,
                    GUILayout.Width(48f));
            }

            EditorGUILayout.HelpBox(
                "Las medidas y variantes se definen en Blender. " +
                "Unity valida la ficha y genera materiales y " +
                "prefabs visuales sin duplicar la geometría.",
                MessageType.Info);
        }

        private void DrawSelectedModel()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(
                        "Modelo",
                        model,
                        typeof(GameObject),
                        false);
                }

                if (GUILayout.Button(
                        "Actualizar",
                        GUILayout.Width(90f)))
                {
                    Refresh();
                }
            }
        }

        private void DrawDimensionalBrief()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Ficha dimensional",
                EditorStyles.boldLabel);

            EditorGUILayout.LabelField(
                "ID",
                manifest.assetId);

            EditorGUILayout.LabelField(
                "Nombre",
                manifest.displayName);

            EditorGUILayout.LabelField(
                "Categoría",
                manifest.category);

            EditorGUILayout.LabelField(
                "Tipo",
                manifest.assetType);

            EditorGUILayout.LabelField(
                "Medidas",
                $"{manifest.dimensions.targetWidthM:F3} × " +
                $"{manifest.dimensions.targetDepthM:F3} × " +
                $"{manifest.dimensions.targetHeightM:F3} m");

            if (manifest.assetType == "CHAIR")
            {
                EditorGUILayout.LabelField(
                    "Altura de asiento",
                    $"{manifest.functionalMeasurements.seatHeightM:F3} m");
            }
        }

        private void DrawConfiguredVariants()
        {
            var variants =
                manifest.variants
                ?? Array.Empty<
                    BistroBuilderSmartAssetManifest
                        .VariantData>();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                $"Variantes configuradas ({variants.Length})",
                EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Puedes añadir, eliminar, renombrar y elegir " +
                "los colores en Blender. Los acabados " +
                "recomendados son solo presets opcionales.",
                MessageType.None);

            if (variants.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No hay acabados definidos.",
                    MessageType.Warning);

                return;
            }

            variantScroll =
                EditorGUILayout.BeginScrollView(
                    variantScroll,
                    GUILayout.MinHeight(90f),
                    GUILayout.MaxHeight(180f));

            foreach (var variant in variants)
            {
                if (variant == null)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.ColorField(
                            GUIContent.none,
                            variant.GetBaseColor(),
                            false,
                            false,
                            false,
                            GUILayout.Width(44f));
                    }

                    EditorGUILayout.LabelField(
                        string.IsNullOrWhiteSpace(
                            variant.displayName)
                            ? variant.id
                            : variant.displayName,
                        GUILayout.MinWidth(150f));

                    EditorGUILayout.LabelField(
                        variant.id,
                        EditorStyles.miniLabel,
                        GUILayout.MinWidth(130f));

                    EditorGUILayout.LabelField(
                        $"×{Mathf.Max(0.01f, variant.priceMultiplier):F2}",
                        GUILayout.Width(48f));
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawActions()
        {
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(
                        "Validar",
                        GUILayout.Height(30f)))
                {
                    ValidateNow();
                }

                if (GUILayout.Button(
                        "Reimportar con contrato",
                        GUILayout.Height(30f)))
                {
                    AssetDatabase.ImportAsset(
                        modelPath,
                        ImportAssetOptions.ForceUpdate);

                    Refresh();
                }
            }

            var hasVariants =
                manifest.variants != null
                && manifest.variants.Length > 0;

            using (new EditorGUI.DisabledScope(
                !hasVariants || HasErrors()))
            {
                if (GUILayout.Button(
                        "Generar o actualizar materiales " +
                        "y prefabs visuales",
                        GUILayout.Height(34f)))
                {
                    try
                    {
                        var set =
                            BistroBuilderSmartAssetVariantGenerator
                                .Generate(
                                    modelPath,
                                    model,
                                    manifest);

                        Selection.activeObject =
                            set;

                        EditorGUIUtility.PingObject(
                            set);

                        Debug.Log(
                            $"[Smart Assets] " +
                            $"{manifest.assetId}: " +
                            $"{manifest.variants.Length} " +
                            "variante(s) generada(s) o " +
                            "actualizada(s).",
                            set);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }

            using (new EditorGUI.DisabledScope(
                string.IsNullOrWhiteSpace(modelPath)))
            {
                if (GUILayout.Button(
                        "Mostrar carpeta generada",
                        GUILayout.Height(24f)))
                {
                    var generatedRoot =
                        BistroBuilderSmartAssetPaths
                            .GeneratedRoot(modelPath);

                    var folder =
                        AssetDatabase.LoadAssetAtPath<
                            DefaultAsset>(
                                generatedRoot);

                    if (folder != null)
                    {
                        Selection.activeObject =
                            folder;

                        EditorGUIUtility.PingObject(
                            folder);
                    }
                }
            }
        }

        private void DrawValidation()
        {
            if (messages == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Puerta de calidad",
                EditorStyles.boldLabel);

            validationScroll =
                EditorGUILayout.BeginScrollView(
                    validationScroll);

            foreach (var message in messages)
            {
                var type =
                    message.Severity ==
                    SmartAssetSeverity.Error
                        ? MessageType.Error
                        : message.Severity ==
                          SmartAssetSeverity.Warning
                            ? MessageType.Warning
                            : MessageType.Info;

                EditorGUILayout.HelpBox(
                    message.Text,
                    type);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            model =
                null;

            modelPath =
                string.Empty;

            manifest =
                null;

            messages =
                null;

            var selected =
                Selection.activeObject;

            if (selected == null)
            {
                return;
            }

            var selectedPath =
                AssetDatabase.GetAssetPath(selected);

            if (!BistroBuilderSmartAssetPaths.IsManagedModel(
                    selectedPath))
            {
                return;
            }

            model =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    selectedPath);

            modelPath =
                selectedPath;

            BistroBuilderSmartAssetManifest.TryLoad(
                BistroBuilderSmartAssetPaths
                    .ManifestPath(selectedPath),
                out manifest);

            if (model != null && manifest != null)
            {
                messages =
                    BistroBuilderSmartAssetValidator.Validate(
                        model,
                        manifest);
            }
        }

        private void ValidateNow()
        {
            if (model != null && manifest != null)
            {
                messages =
                    BistroBuilderSmartAssetValidator.Validate(
                        model,
                        manifest);
            }

            Repaint();
        }

        private bool HasErrors()
        {
            return messages != null
                && BistroBuilderSmartAssetValidator
                    .HasErrors(messages);
        }
    }
}
