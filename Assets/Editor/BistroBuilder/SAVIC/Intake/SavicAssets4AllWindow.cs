using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BistroBuilder.Editor.Savic
{
    internal sealed class SavicAssets4AllWindow : EditorWindow
    {
        private string folder = "";
        private int binding;
        private SavicAssets4AllReceipt receipt;
        [MenuItem("Tools/Bistro Builder/SAVIC/Assets4ALL/Import Delivery")]
        private static void Open() => GetWindow<SavicAssets4AllWindow>("Assets4ALL → SAVIC");
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Entrega desde Assets4ALL", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Selecciona la carpeta de una revisión exportada desde Blender. Para corregir un artículo existente, vincúlalo en su primera entrega. Las siguientes revisiones lo actualizan automáticamente.", MessageType.Info);
            folder = EditorGUILayout.TextField("Carpeta", folder);
            if (GUILayout.Button("Seleccionar entrega…")) folder = EditorUtility.OpenFolderPanel("Entrega Assets4ALL", folder, "");
            var context = SavicEditorContext.Instance;
            var published = context.Manifests.GetAll().Where(m => m.status == "PUBLISHED").OrderBy(m => m.source.originalFileName).ToArray();
            var choices = new[] { "Crear artículo nuevo" }.Concat(published.Select(m => m.source.originalFileName + " · " + m.savicId)).ToArray();
            binding = Mathf.Clamp(binding, 0, choices.Length - 1);
            binding = EditorGUILayout.Popup("Primera vinculación", binding, choices);
            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(folder) || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Importar o actualizar"))
                    receipt = SavicAssets4AllService.Import(folder, context, binding == 0 ? null : published[binding - 1].savicId);
            if (receipt != null)
                EditorGUILayout.HelpBox(receipt.state + " · " + receipt.reason + "\n" + receipt.savicId + "\n" + receipt.canonicalContentId,
                    receipt.state == "NEEDS_REVIEW" ? MessageType.Warning : MessageType.Info);
            EditorGUILayout.HelpBox("También puedes exportar a ContentInbox/Deliveries. SAVIC detecta las entregas completas y guarda el resultado en SAVIC/Receipts/Assets4All.", MessageType.None);
        }
    }
}
