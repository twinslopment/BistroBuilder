using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.AssetStudioBB.Editor
{
    internal sealed class AssetStudioBBWindow : EditorWindow
    {
        private string manifestPath = string.Empty;
        private AssetStudioBBManifest manifest;
        private IReadOnlyList<AssetStudioBBValidationMessage> validation;
        private Vector2 scroll;
        private Texture2D previewTexture;

        [MenuItem("Tools/Configurador de Assets/Asset Studio BB")]
        private static void Open()
        {
            var window = GetWindow<AssetStudioBBWindow>();
            window.titleContent = new GUIContent("Asset Studio BB");
            window.minSize = new Vector2(590f, 520f);
            window.RefreshFromSelection();
            window.Show();
        }

        private void OnEnable() => RefreshFromSelection();

        private void OnSelectionChange()
        {
            RefreshFromSelection();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Asset Studio BB · Local 3.0", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Selecciona el manifiesto .bbstudio.json o cualquiera de sus FBX. " +
                "Este importador es independiente del Smart Assets antiguo y conserva materiales por roles.",
                MessageType.Info);

            if (manifest == null)
            {
                EditorGUILayout.HelpBox(
                    "No hay un asset de Asset Studio BB seleccionado.",
                    MessageType.Warning);
                if (GUILayout.Button("Actualizar desde selección"))
                    RefreshFromSelection();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (previewTexture != null)
            {
                var rect = GUILayoutUtility.GetRect(180f, 180f, GUILayout.Width(180f), GUILayout.Height(180f));
                EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("ID", manifest.assetId);
            EditorGUILayout.LabelField("Nombre", manifest.displayName);
            EditorGUILayout.LabelField("Familia", $"{manifest.family} / {manifest.subtype}");
            EditorGUILayout.LabelField("Calidad", manifest.quality);
            EditorGUILayout.LabelField(
                "Medidas",
                $"{manifest.dimensions.targetWidthM:F3} × " +
                $"{manifest.dimensions.targetDepthM:F3} × " +
                $"{manifest.dimensions.targetHeightM:F3} m");
            if (manifest.dimensions.seatHeightM > 0f)
                EditorGUILayout.LabelField("Altura asiento", $"{manifest.dimensions.seatHeightM:F3} m");
            if (manifest.dimensions.worktopHeightM > 0f)
                EditorGUILayout.LabelField("Altura trabajo", $"{manifest.dimensions.worktopHeightM:F3} m");
            EditorGUILayout.LabelField("Mallas fuente", manifest.geometry.meshCount.ToString());
            EditorGUILayout.LabelField("Triángulos fuente", manifest.geometry.triangleCountSource.ToString("N0"));
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Puerta de calidad Unity", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(150f));
            foreach (var message in validation)
            {
                var type = message.Severity == AssetStudioBBSeverity.Error
                    ? MessageType.Error
                    : message.Severity == AssetStudioBBSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(message.Text, type);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("Acabados", EditorStyles.boldLabel);
            foreach (var variant in manifest.variants)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(variant.displayName);
                EditorGUILayout.LabelField($"×{variant.priceMultiplier:F2}", GUILayout.Width(52f));
                EditorGUILayout.EndHorizontal();
            }

            using (new EditorGUI.DisabledScope(AssetStudioBBValidator.HasErrors(validation)))
            {
                if (GUILayout.Button("Generar o actualizar prefabs con LOD", GUILayout.Height(36f)))
                {
                    try
                    {
                        var set = AssetStudioBBVariantGenerator.Generate(manifestPath, manifest);
                        Selection.activeObject = set;
                        EditorGUIUtility.PingObject(set);
                        ShowNotification(new GUIContent("Variantes actualizadas"));
                    }
                    catch (System.Exception exception)
                    {
                        Debug.LogException(exception);
                        EditorUtility.DisplayDialog("Asset Studio BB", exception.Message, "Cerrar");
                    }
                }
            }

            EditorGUILayout.HelpBox(
                "El resultado visual queda en Generated/VisualVariants. Selecciona el prefab elegido " +
                "y pásalo después por la Fábrica Universal de artículos colocables.",
                MessageType.None);
        }

        private void RefreshFromSelection()
        {
            manifestPath = string.Empty;
            manifest = null;
            validation = new List<AssetStudioBBValidationMessage>();
            previewTexture = null;

            var selected = Selection.activeObject;
            if (selected == null)
                return;
            var selectedPath = AssetDatabase.GetAssetPath(selected);
            manifestPath = AssetStudioBBPaths.FindManifestFromSelection(selectedPath);
            if (!AssetStudioBBManifest.TryLoad(manifestPath, out manifest))
            {
                manifest = null;
                return;
            }
            validation = AssetStudioBBValidator.Validate(manifestPath, manifest);
            if (!string.IsNullOrWhiteSpace(manifest.preview))
            {
                var previewPath = AssetStudioBBPaths.AbsoluteAssetPath(manifestPath, manifest.preview);
                previewTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(previewPath);
            }
        }
    }
}
