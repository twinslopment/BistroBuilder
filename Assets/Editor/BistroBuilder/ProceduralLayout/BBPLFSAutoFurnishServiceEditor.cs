using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BBPLFSAutoFurnishService))]
public sealed class BBPLFSAutoFurnishServiceEditor : Editor
{
    private string lastMessage = string.Empty;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("BBPLFS Auto Furnish V1", EditorStyles.boldLabel);
        BBPLFSAutoFurnishService service = (BBPLFSAutoFurnishService)target;

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Generar y validar propuestas"))
                service.Generate(out lastMessage);

            EditorGUILayout.LabelField("Propuestas válidas", service.Candidates.Count.ToString());
            for (int i = 0; i < service.Candidates.Count; i++)
            {
                BBPLFSRankedCandidate ranked = service.Candidates[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i + 1) + ". " + ranked.Candidate.CandidateId +
                    " | " + ranked.Candidate.Capacity + " | " + ranked.FinalScore.ToString("0.000"));
                if (GUILayout.Button("Preview", GUILayout.Width(72f))) service.Preview(i, out lastMessage);
                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button("Aceptar propuesta previsualizada")) service.Accept(out lastMessage);
            if (GUILayout.Button("Cancelar preview"))
            {
                service.CancelPreview();
                lastMessage = "Preview cancelado.";
            }
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Instala con Bistro Builder > BBPLFS > Install Advanced V1 y entra en Play Mode.", MessageType.Info);
        if (service.RejectionDiagnostics.Count > 0)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("Diagnósticos", EditorStyles.boldLabel);
            for (int i = 0; i < Mathf.Min(5, service.RejectionDiagnostics.Count); i++)
                EditorGUILayout.HelpBox(service.RejectionDiagnostics[i], MessageType.Warning);
        }
        if (!string.IsNullOrWhiteSpace(lastMessage)) EditorGUILayout.HelpBox(lastMessage, MessageType.None);
    }
}
