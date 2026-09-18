using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BBPLFSVerticalSliceController))]
public sealed class BBPLFSVerticalSliceControllerEditor : Editor
{
    private string lastMessage = string.Empty;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("BBPLFS Vertical Slice", EditorStyles.boldLabel);

        BBPLFSVerticalSliceController controller = (BBPLFSVerticalSliceController)target;

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("1. Analyze selected room"))
            {
                controller.AnalyzeSelectedArea(out lastMessage);
            }

            if (GUILayout.Button("2. Generate dining candidates"))
            {
                controller.GenerateDiningRoom(out lastMessage);
            }

            IReadOnlyListDrawer(controller);

            if (GUILayout.Button("Accept previewed candidate"))
            {
                controller.AcceptPreviewedCandidate(out lastMessage);
            }

            if (GUILayout.Button("Cancel preview"))
            {
                controller.CancelPreview();
                lastMessage = "Preview cleared.";
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to run the vertical slice. Install/configure it from Bistro Builder > BBPLFS > Install Vertical Slice.", MessageType.Info);
        }

        if (!string.IsNullOrWhiteSpace(lastMessage))
        {
            EditorGUILayout.HelpBox(lastMessage, MessageType.None);
        }
    }
    private void IReadOnlyListDrawer(BBPLFSVerticalSliceController controller)
    {
        if (controller.Candidates == null || controller.Candidates.Count == 0)
        {
            EditorGUILayout.LabelField("Candidates", "0");
            return;
        }

        EditorGUILayout.LabelField("Candidates", controller.Candidates.Count.ToString());
        for (int index = 0; index < controller.Candidates.Count; index++)
        {
            BBPLFSLayoutCandidate candidate = controller.Candidates[index];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{index + 1}. {candidate.CandidateId} | {candidate.Capacity} seats | score {candidate.Score:0.00}");

            if (GUILayout.Button("Preview", GUILayout.Width(75f)))
            {
                controller.PreviewCandidate(index, out lastMessage);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}