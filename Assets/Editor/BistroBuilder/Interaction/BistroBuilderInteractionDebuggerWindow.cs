using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class BistroBuilderInteractionDebuggerWindow : EditorWindow
{
    private string holderId = string.Empty;
    private string resourceId = string.Empty;
    private Vector2 scroll;

    [MenuItem("Bistro Builder/Interaction & Reservation v1/Debugger")]
    private static void Open()
    {
        GetWindow<BistroBuilderInteractionDebuggerWindow>(
            "BB Interaction Debugger");
    }

    private void OnGUI()
    {
        BistroBuilderInteractionService service =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderInteractionService>();
        if (service == null)
        {
            EditorGUILayout.HelpBox("No hay Interaction Service activo.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("BB Interaction & Reservation v1", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Epoch", service.ArbitrationEpoch.ToString());
        EditorGUILayout.LabelField("Grants", service.ActiveGrantCount.ToString());
        holderId = EditorGUILayout.TextField("Holder ID", holderId);
        resourceId = EditorGUILayout.TextField("Resource ID", resourceId);
        if (GUILayout.Button("Refrescar")) Repaint();

        scroll = EditorGUILayout.BeginScrollView(scroll);
        if (!string.IsNullOrWhiteSpace(holderId))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Holder grants", EditorStyles.boldLabel);
            foreach (BistroBuilderInteractionGrantRecord grant in
                     service.GetGrantsForHolder(holderId))
                DrawGrant(grant);
        }
        if (!string.IsNullOrWhiteSpace(resourceId))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resource grants", EditorStyles.boldLabel);
            foreach (BistroBuilderInteractionGrantRecord grant in
                     service.GetGrantsForResource(resourceId))
                DrawGrant(grant);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Últimos eventos", EditorStyles.boldLabel);
        var trace = service.GetTraceSnapshot();
        int start = Math.Max(0, trace.Count - 30);
        for (int i = start; i < trace.Count; i++)
        {
            BistroBuilderInteractionTraceRecord row = trace[i];
            if (row == null) continue;
            EditorGUILayout.LabelField(
                row.epoch + " | " + row.action + " | " + row.subjectId +
                " | " + row.reason + " | " + row.detail,
                EditorStyles.miniLabel);
        }
        EditorGUILayout.EndScrollView();
    }
    private static void DrawGrant(BistroBuilderInteractionGrantRecord grant)
    {
        if (grant == null) return;
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField(
            grant.grantId + " @" + grant.generation,
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Kind / State", grant.kind + " / " + grant.state);
        EditorGUILayout.LabelField("Holder", grant.holderId);
        EditorGUILayout.LabelField("Resource", grant.resourceId);
        EditorGUILayout.LabelField("Interaction", grant.interactionId);
        EditorGUILayout.LabelField("Target", grant.targetId);
        EditorGUILayout.LabelField("Channel / Slot", grant.channelId + " / " + grant.slotId);
        EditorGUILayout.LabelField("Priority", grant.taskPriorityClass.ToString());
        EditorGUILayout.LabelField("Scope", grant.conflictKey);
        EditorGUILayout.LabelField("Parent", grant.parentGrantId);
        EditorGUILayout.LabelField("Spatial Lease", grant.spatialLeaseId);
        EditorGUILayout.LabelField("Last reason", grant.lastReason.ToString());
        EditorGUILayout.EndVertical();
    }
}
