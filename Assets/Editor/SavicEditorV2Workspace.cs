using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    /// <summary>Convenience commands over the existing Editor V2 and SAVIC authorities.</summary>
    public static class SavicEditorV2Workspace
    {
        [MenuItem("Tools/Bistro Builder/Proyecto conjunto/Abrir Editor V2 y SAVIC")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Prototype_Restaurant.unity");
            SavicEditorWindow.Open();
        }

        // Only additional published content receives derivatives. The six R9
        // source photos already have this resolution and are kept byte-for-byte.
        public static void PrepareAdditionalPhotos()
        {
            var report = new StringBuilder("ADDITIONAL SAVIC PHOTOS — NATIVE GPU\n");
            foreach (var item in BistroBuilderCatalogThumbnailService.LoadAllDefinitions()
                .Where(x => x.Category == RestaurantPlaceableItemCategory.Seating && x.HasValidPrefab))
            {
                if (item.CatalogIcon != null && item.CatalogIcon.texture.width == 1024 &&
                    item.CatalogIcon.texture.height == 1024 && item.InspectorPreview == item.CatalogIcon) continue;
                var result = BistroBuilderCatalogThumbnailService.GenerateAndAssign(item, false, true, 1024, false);
                if (!result.Succeeded) throw new InvalidOperationException(item.ItemId + ": " + result.Message);
                report.AppendLine(item.ItemId + "|" + result.AssetPath);
            }
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs/EditorV2Savic");
            File.WriteAllText("Logs/EditorV2Savic/additional-photos.txt", report + "PASS\n");
            Debug.Log(report + "PASS");
        }

        public static void BuildPreview()
        {
            const string folder = "Builds/Windows/EditorV2_SAVIC_20261014";
            Directory.CreateDirectory(folder);
            var originalName = PlayerSettings.productName;
            var originalMode = PlayerSettings.fullScreenMode;
            BuildReport result;
            try
            {
                // The established R9 review bootstrap enters the native editor.
                PlayerSettings.productName = "BistroBuilder Editor V2 R9";
                PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
                result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/Scenes/Prototype_Restaurant.unity" },
                    locationPathName = folder + "/BistroBuilder_EditorV2_SAVIC.exe",
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
                });
            }
            finally
            {
                PlayerSettings.productName = originalName;
                PlayerSettings.fullScreenMode = originalMode;
            }
            var summary = result.summary;
            string report = "BUILD=" + summary.result + "\nERRORS=" + summary.totalErrors + "\nWARNINGS=" + summary.totalWarnings;
            File.WriteAllText(folder + "/BUILD_REPORT.txt", report);
            File.WriteAllText(folder + "/LEEME.txt", "EDITOR V2 R9 + CONTENIDO SAVIC\nAbre BistroBuilder_EditorV2_SAVIC.exe. F11 alterna ventana.\nLa recepción de nuevos assets funciona en Unity, con Play detenido.\nEste ejecutable muestra el catálogo publicado al compilar; necesita una compilación nueva para incorporar cambios posteriores.\n" + report);
            if (summary.result != BuildResult.Succeeded) throw new InvalidOperationException(report);
        }
    }
}
