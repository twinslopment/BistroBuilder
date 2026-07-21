using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.SmartAssets.Editor
{
    internal sealed class BistroBuilderSmartAssetWindow : EditorWindow
    {
        private GameObject model;
        private string modelPath = string.Empty;
        private BistroBuilderSmartAssetManifest manifest;
        private IReadOnlyList<SmartAssetMessage> messages;
        private Vector2 scroll;

        [MenuItem("Tools/BistroBuilder/Smart Assets")]
        private static void Open()
        {
            var window = GetWindow<BistroBuilderSmartAssetWindow>();
            window.titleContent = new GUIContent("Smart Assets");
            window.minSize = new Vector2(540f, 440f);
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
            EditorGUILayout.LabelField("BistroBuilder · Smart Asset Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Selecciona el FBX principal, no una malla interna ni una instancia de escena.", MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField("Modelo", model, typeof(GameObject), false);
                if (GUILayout.Button("Actualizar", GUILayout.Width(90f))) Refresh();
            }

            if (model == null)
            {
                EditorGUILayout.HelpBox("No hay un FBX gestionado seleccionado.", MessageType.Warning);
                return;
            }

            if (manifest == null)
            {
                EditorGUILayout.HelpBox("Falta el manifiesto .bbasset.json junto al FBX.", MessageType.Error);
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Ficha dimensional", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("ID", manifest.assetId);
            EditorGUILayout.LabelField("Nombre", manifest.displayName);
            EditorGUILayout.LabelField("Categoría", manifest.category);
            EditorGUILayout.LabelField("Tipo", manifest.assetType);
            EditorGUILayout.LabelField("Medidas", $"{manifest.dimensions.targetWidthM:F3} × {manifest.dimensions.targetDepthM:F3} × {manifest.dimensions.targetHeightM:F3} m");
            if (manifest.assetType == "CHAIR")
                EditorGUILayout.LabelField("Altura de asiento", $"{manifest.functionalMeasurements.seatHeightM:F3} m");
            EditorGUILayout.LabelField("Variantes", manifest.variants?.Length.ToString() ?? "0");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Validar", GUILayout.Height(28f))) ValidateNow();
                if (GUILayout.Button("Reimportar con contrato", GUILayout.Height(28f)))
                {
                    AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
                    Refresh();
                }
            }

            using (new EditorGUI.DisabledScope(
                manifest.variants == null || manifest.variants.Length == 0 || HasErrors()))
            {
                if (GUILayout.Button("Generar materiales y prefabs visuales de variantes", GUILayout.Height(32f)))
                {
                    var set = BistroBuilderSmartAssetVariantGenerator.Generate(modelPath, model, manifest);
                    Selection.activeObject = set;
                    EditorGUIUtility.PingObject(set);
                }
            }

            if (messages == null) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Puerta de calidad", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var message in messages)
            {
                var type = message.Severity == SmartAssetSeverity.Error
                    ? MessageType.Error
                    : message.Severity == SmartAssetSeverity.Warning
                        ? MessageType.Warning
                        : MessageType.Info;
                EditorGUILayout.HelpBox(message.Text, type);
            }
            EditorGUILayout.EndScrollView();
        }

        private void Refresh()
        {
            model = null;
            modelPath = string.Empty;
            manifest = null;
            messages = null;
            var selected = Selection.activeObject;
            if (selected == null) return;
            var path = AssetDatabase.GetAssetPath(selected);
            if (!BistroBuilderSmartAssetPaths.IsManagedModel(path)) return;
            model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            modelPath = path;
            BistroBuilderSmartAssetManifest.TryLoad(BistroBuilderSmartAssetPaths.ManifestPath(path), out manifest);
            if (model != null && manifest != null) messages = BistroBuilderSmartAssetValidator.Validate(model, manifest);
        }

        private void ValidateNow()
        {
            if (model != null && manifest != null)
                messages = BistroBuilderSmartAssetValidator.Validate(model, manifest);
            Repaint();
        }

        private bool HasErrors()
        {
            return messages != null && BistroBuilderSmartAssetValidator.HasErrors(messages);
        }
    }
}
