using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Autoría Tool-First: deriva sólo semántica lógica segura desde contratos BBSIS.
/// No infiere gameplay incierto ni crea geometría espacial.
/// </summary>
public static class BistroBuilderInteractionAuthoringTool
{
    private const string DefinitionFolder =
        "Assets/Resources/BistroBuilder/Interaction/Targets";

    public static int LastAuthored { get; private set; }
    public static int LastSkipped { get; private set; }
    public static int LastFailed { get; private set; }
    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Bistro Builder/Interaction & Reservation v1/Autoría/AUTO + validar")]
    public static void AutoAuthorAndValidate()
    {
        EnsureFolder(DefinitionFolder);
        LastAuthored = LastSkipped = LastFailed = 0;
        var lines = new List<string>();
        BistroBuilderSpatialSubject[] subjects =
            UnityEngine.Object.FindObjectsByType<BistroBuilderSpatialSubject>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        Array.Sort(subjects, (a, b) => string.CompareOrdinal(
            a != null ? a.SubjectId : string.Empty,
            b != null ? b.SubjectId : string.Empty));
        for (int i = 0; i < subjects.Length; i++)
        {
            BistroBuilderSpatialSubject subject = subjects[i];
            if (subject == null || subject.Contract == null ||
                string.IsNullOrWhiteSpace(subject.SubjectId))
            {
                LastFailed++;
                continue;
            }
            if (!TryBuildSafeDefinition(subject.Contract, out var channels, out string reason))
            {
                LastSkipped++;
                lines.Add("SKIP " + subject.SubjectId + " - " + reason);
                continue;
            }

            string assetPath = DefinitionFolder + "/" +
                               Sanitize(subject.Contract.ContractId) + ".asset";
            BistroBuilderInteractionTargetDefinition definition =
                AssetDatabase.LoadAssetAtPath<BistroBuilderInteractionTargetDefinition>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<BistroBuilderInteractionTargetDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }
            definition.ConfigureForEditor(
                "interaction.definition." + subject.Contract.ContractId,
                null,
                channels);
            EditorUtility.SetDirty(definition);

            BistroBuilderInteractionTarget target =
                subject.GetComponent<BistroBuilderInteractionTarget>();
            if (target == null)
                target = Undo.AddComponent<BistroBuilderInteractionTarget>(subject.gameObject);
            target.ConfigureForEditor(
                "interaction.target." + subject.SubjectId,
                definition,
                subject);
            EditorUtility.SetDirty(target);
            LastAuthored++;
            lines.Add("AUTO " + subject.SubjectId + " -> " + definition.DefinitionId);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().IsValid())
        {
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }

        BistroBuilderInteractionService service =
            UnityEngine.Object.FindFirstObjectByType<BistroBuilderInteractionService>();
        service?.RebuildTargets();
        LastReport = "AUTO=" + LastAuthored + " SKIP=" + LastSkipped +
                     " FAIL=" + LastFailed + "\n" + string.Join("\n", lines);
        Debug.Log(LastReport);
        if (LastFailed > 0)
            throw new InvalidOperationException(LastReport);
        BistroBuilderInteractionV1Validator.Run();
    }

    private static bool TryBuildSafeDefinition(
        BistroBuilderSpatialContractDefinition contract,
        out List<BistroBuilderInteractionChannelDefinition> channels,
        out string reason)
    {
        channels = new List<BistroBuilderInteractionChannelDefinition>();
        reason = string.Empty;
        if (contract == null)
        {
            reason = "sin Spatial Contract";
            return false;
        }

        bool isDoor = string.Equals(contract.FamilyId, "architecture.door", StringComparison.Ordinal) ||
                      contract.HasTrait("door") || contract.HasTrait("dynamic.door");
        if (isDoor)
        {
            reason = "puerta de tránsito: sin reserva lógica automática";
            return false;
        }

        if (string.Equals(contract.FamilyId, "seating.table", StringComparison.Ordinal) ||
            contract.HasTrait("seating.table"))
            channels.Add(LogicalChannel(
                "party.assignment", 1, "table.assign",
                BistroBuilderInteractionHolderKind.Group));
        bool chair = string.Equals(contract.FamilyId, "seating.chair", StringComparison.Ordinal) ||
                     contract.HasTrait("seating.chair");
        for (int i = 0; i < contract.Ports.Count; i++)
        {
            BistroBuilderSpatialPortDefinition port = contract.Ports[i];
            if (port == null || string.IsNullOrWhiteSpace(port.portId)) continue;
            switch (port.kind)
            {
                case BistroBuilderSpatialPortKind.SeatBay when chair:
                    channels.Add(SpatialChannel(
                        "seat." + port.portId, port.portId, "seat.use",
                        BistroBuilderSpatialClaimKind.Seat,
                        BistroBuilderInteractionHolderKind.Actor));
                    break;
                case BistroBuilderSpatialPortKind.Work:
                    channels.Add(SpatialChannel(
                        "work." + port.portId, port.portId, "work.use",
                        BistroBuilderSpatialClaimKind.Work,
                        BistroBuilderInteractionHolderKind.Actor));
                    break;
                case BistroBuilderSpatialPortKind.Service:
                    channels.Add(SpatialChannel(
                        "service." + port.portId, port.portId, "service.use",
                        BistroBuilderSpatialClaimKind.Service,
                        BistroBuilderInteractionHolderKind.Actor));
                    break;
                case BistroBuilderSpatialPortKind.Transfer:
                    channels.Add(SpatialChannel(
                        "transfer." + port.portId, port.portId, "transfer.use",
                        BistroBuilderSpatialClaimKind.Transfer,
                        BistroBuilderInteractionHolderKind.Actor));
                    break;
            }
        }
        for (int i = 0; i < contract.WorkEdges.Count; i++)
        {
            BistroBuilderSpatialWorkEdgeDefinition edge = contract.WorkEdges[i];
            if (edge == null || string.IsNullOrWhiteSpace(edge.edgeId)) continue;
            channels.Add(new BistroBuilderInteractionChannelDefinition
            {
                channelId = "workedge." + edge.edgeId,
                capacity = 1,
                interactionIds = new List<string> { "work.use" },
                allowedHolderKinds = new List<BistroBuilderInteractionHolderKind>
                {
                    BistroBuilderInteractionHolderKind.Actor
                },
                spatialBinding = new BistroBuilderInteractionSpatialBindingDefinition
                {
                    bindingKind = BistroBuilderInteractionSpatialBindingKind.WorkEdge,
                    bindingId = edge.edgeId,
                    claimKind = BistroBuilderSpatialClaimKind.Work
                }
            });
        }

        if (channels.Count > 0) return true;
        reason = "sin semántica lógica auto-segura";
        return false;
    }

    private static BistroBuilderInteractionChannelDefinition LogicalChannel(
        string id,
        int capacity,
        string interaction,
        BistroBuilderInteractionHolderKind holder)
    {
        return new BistroBuilderInteractionChannelDefinition
        {
            channelId = id,
            capacity = Math.Max(1, capacity),
            interactionIds = new List<string> { interaction },
            allowedHolderKinds = new List<BistroBuilderInteractionHolderKind> { holder }
        };
    }

    private static BistroBuilderInteractionChannelDefinition SpatialChannel(
        string id,
        string portId,
        string interaction,
        BistroBuilderSpatialClaimKind claimKind,
        BistroBuilderInteractionHolderKind holder)
    {
        return new BistroBuilderInteractionChannelDefinition
        {
            channelId = id,
            capacity = 1,
            interactionIds = new List<string> { interaction },
            allowedHolderKinds = new List<BistroBuilderInteractionHolderKind> { holder },
            spatialBinding = new BistroBuilderInteractionSpatialBindingDefinition
            {
                bindingKind = BistroBuilderInteractionSpatialBindingKind.Port,
                bindingId = portId,
                claimKind = claimKind
            }
        };
    }
    private static void EnsureFolder(string path)
    {
        string normalized = path.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unnamed";
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
            if (invalid.Contains(chars[i]) || chars[i] == '.' || chars[i] == ':')
                chars[i] = '_';
        return new string(chars);
    }
}
