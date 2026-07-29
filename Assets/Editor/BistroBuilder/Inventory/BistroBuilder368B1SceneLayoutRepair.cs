using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Reparación acumulativa 368B1 para la escena provisional.
///
/// Corrige dos problemas detectados por los validadores runtime:
/// - Las sillas 368A heredan explícitamente el área de su mesa.
/// - Las cuatro mesas y sus sillas se redistribuyen en posiciones que
///   respetan huellas, espacios operativos y obstáculos fijos.
///
/// La selección de posiciones se calcula a partir de los datos reales de
/// mesa, silla, área y obstáculos. No depende de nombres visibles ni de
/// coordenadas hardcodeadas de la escena.
/// </summary>
public static class BistroBuilder368B1SceneLayoutRepair
{
    private const float CandidateStep = 0.50f;
    private const float CandidateBoundaryPadding = 0.35f;
    private const float ShapeTolerance = 0.015f;

    private sealed class TableGroup
    {
        public RestaurantTable Table;
        public RestaurantTableSeatingConfiguration Configuration;
        public RestaurantArea Area;
        public readonly Dictionary<int, BistroBuilder368AInstalledChair>
            ChairsBySlot = new Dictionary<int, BistroBuilder368AInstalledChair>();
    }

    private sealed class GroupPose
    {
        public Vector3 TablePosition;
        public Quaternion TableRotation;
        public readonly List<RestaurantPlacementShape> Shapes =
            new List<RestaurantPlacementShape>(12);
        public readonly Dictionary<int, ChairPose> ChairPoses =
            new Dictionary<int, ChairPose>();
    }

    private readonly struct ChairPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        public ChairPose(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    /// <summary>
    /// Repara la distribución provisional de mesas y sillas.
    /// Es idempotente: repetirla produce la misma distribución válida.
    /// </summary>
    public static void Repair(Scene scene)
    {
        List<TableGroup> groups = BuildGroups(scene, out RestaurantArea area);

        if (groups.Count == 0)
        {
            throw new InvalidOperationException(
                "No hay mesas operativas para reparar la distribución."
            );
        }

        AssignChairAreas(groups);

        if (!IsProvisionalPrototypeScene(scene, groups.Count))
        {
            if (!Validate(scene, out string existingLayoutError))
            {
                throw new InvalidOperationException(existingLayoutError);
            }

            return;
        }

        if (!TryGetAreaBounds(area, out Bounds bounds))
        {
            throw new InvalidOperationException(
                "No se pudieron calcular los límites del comedor."
            );
        }

        RestaurantPlacementObstacle[] obstacles =
            FindSceneObjects<RestaurantPlacementObstacle>(scene);
        Vector3 referenceObstaclePosition = ResolveReferenceObstaclePosition(
            obstacles,
            bounds.center
        );

        var selectedPoses = new List<GroupPose>(groups.Count);

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            TableGroup group = groups[groupIndex];
            List<Vector3> candidates = BuildCandidates(
                bounds,
                group.Table.transform.position.y,
                referenceObstaclePosition
            );

            GroupPose selected = null;

            for (int candidateIndex = 0;
                 candidateIndex < candidates.Count;
                 candidateIndex++)
            {
                GroupPose pose = BuildGroupPose(
                    group,
                    candidates[candidateIndex],
                    group.Table.transform.rotation
                );

                if (!IsPoseInsideArea(pose, area) ||
                    OverlapsBlockingObstacle(pose, obstacles) ||
                    OverlapsSelectedGroups(pose, selectedPoses))
                {
                    continue;
                }

                selected = pose;
                break;
            }

            if (selected == null)
            {
                throw new InvalidOperationException(
                    "No se encontró una distribución segura para " +
                    group.Table.name + "."
                );
            }

            selectedPoses.Add(selected);
        }

        for (int index = 0; index < groups.Count; index++)
        {
            ApplyPose(groups[index], selectedPoses[index]);
        }

        if (!Validate(scene, out string validationError))
        {
            throw new InvalidOperationException(validationError);
        }
    }

    /// <summary>
    /// Valida que las sillas tengan área y que los grupos mesa-sillas
    /// respeten el área, los obstáculos y el espacio operativo entre grupos.
    /// </summary>
    public static bool Validate(Scene scene, out string error)
    {
        error = string.Empty;
        List<TableGroup> groups;
        RestaurantArea area;

        try
        {
            groups = BuildGroups(scene, out area);
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        RestaurantPlacementObstacle[] obstacles =
            FindSceneObjects<RestaurantPlacementObstacle>(scene);
        var poses = new List<GroupPose>(groups.Count);

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            TableGroup group = groups[groupIndex];

            foreach (KeyValuePair<int, BistroBuilder368AInstalledChair> pair
                     in group.ChairsBySlot)
            {
                BistroBuilder368AInstalledChair marker = pair.Value;
                RestaurantAreaMember chairMember =
                    marker.GetComponent<RestaurantAreaMember>();

                if (chairMember == null ||
                    chairMember.AssignedArea == null ||
                    !ReferenceEquals(chairMember.AssignedArea, group.Area))
                {
                    error = marker.name +
                        " no hereda el área asignada de su mesa.";
                    return false;
                }
            }

            GroupPose pose = BuildGroupPose(
                group,
                group.Table.transform.position,
                group.Table.transform.rotation
            );

            if (!IsPoseInsideArea(pose, area))
            {
                error = group.Table.name +
                    " o alguna de sus sillas queda fuera del área válida.";
                return false;
            }

            if (OverlapsBlockingObstacle(pose, obstacles))
            {
                error = group.Table.name +
                    " o alguna de sus sillas invade un obstáculo fijo.";
                return false;
            }

            if (OverlapsSelectedGroups(pose, poses))
            {
                error = group.Table.name +
                    " no conserva espacio físico y operativo respecto a " +
                    "otro conjunto mesa-sillas.";
                return false;
            }

            poses.Add(pose);
        }

        return true;
    }

    private static void AssignChairAreas(List<TableGroup> groups)
    {
        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            TableGroup group = groups[groupIndex];

            foreach (KeyValuePair<int, BistroBuilder368AInstalledChair> pair
                     in group.ChairsBySlot)
            {
                BistroBuilder368AInstalledChair marker = pair.Value;
                RestaurantAreaMember member =
                    marker.GetComponent<RestaurantAreaMember>();

                if (member == null)
                {
                    throw new InvalidOperationException(
                        marker.name +
                        " no contiene RestaurantAreaMember."
                    );
                }

                Undo.RecordObject(
                    member,
                    "Asignar área de silla provisional 368B1"
                );
                member.SetArea(group.Area);
                EditorUtility.SetDirty(member);
            }
        }
    }

    private static bool IsProvisionalPrototypeScene(
        Scene scene,
        int tableCount
    )
    {
        if (tableCount != 4)
        {
            return false;
        }

        BistroBuilder367HInstalledFixture[] fixtures =
            FindSceneObjects<BistroBuilder367HInstalledFixture>(scene);
        bool hasBar = false;
        int fixedTableCount = 0;

        for (int index = 0; index < fixtures.Length; index++)
        {
            string fixtureId = fixtures[index].FixtureId;

            if (string.Equals(
                    fixtureId,
                    "fixture_367h_bar",
                    StringComparison.Ordinal
                ))
            {
                hasBar = true;
            }
            else if (fixtureId.StartsWith(
                         "fixture_367h_table_",
                         StringComparison.Ordinal
                     ))
            {
                fixedTableCount++;
            }
        }

        return hasBar && fixedTableCount >= 2;
    }

    private static List<TableGroup> BuildGroups(
        Scene scene,
        out RestaurantArea commonArea
    )
    {
        commonArea = null;
        RestaurantTable[] tables = FindSceneObjects<RestaurantTable>(scene);
        Array.Sort(tables, (first, second) =>
            first.TableId.CompareTo(second.TableId));

        BistroBuilder368AInstalledChair[] markers =
            FindSceneObjects<BistroBuilder368AInstalledChair>(scene);
        var markersByTable = new Dictionary<
            int,
            List<BistroBuilder368AInstalledChair>
        >();

        for (int index = 0; index < markers.Length; index++)
        {
            BistroBuilder368AInstalledChair marker = markers[index];

            if (!markersByTable.TryGetValue(
                    marker.TableId,
                    out List<BistroBuilder368AInstalledChair> list
                ))
            {
                list = new List<BistroBuilder368AInstalledChair>();
                markersByTable.Add(marker.TableId, list);
            }

            list.Add(marker);
        }

        var groups = new List<TableGroup>(tables.Length);

        for (int tableIndex = 0; tableIndex < tables.Length; tableIndex++)
        {
            RestaurantTable table = tables[tableIndex];
            RestaurantTableSeatingConfiguration configuration =
                table.GetComponent<RestaurantTableSeatingConfiguration>();
            RestaurantAreaMember tableMember =
                table.GetComponent<RestaurantAreaMember>();

            if (configuration == null ||
                tableMember == null ||
                tableMember.AssignedArea == null)
            {
                throw new InvalidOperationException(
                    table.name +
                    " no tiene configuración de asientos o área válida."
                );
            }

            if (commonArea == null)
            {
                commonArea = tableMember.AssignedArea;
            }
            else if (!ReferenceEquals(commonArea, tableMember.AssignedArea))
            {
                throw new InvalidOperationException(
                    "Las mesas provisionales no comparten una misma área."
                );
            }

            if (!markersByTable.TryGetValue(
                    table.TableId,
                    out List<BistroBuilder368AInstalledChair> tableMarkers
                ))
            {
                throw new InvalidOperationException(
                    table.name + " no tiene sillas 368A instaladas."
                );
            }

            var group = new TableGroup
            {
                Table = table,
                Configuration = configuration,
                Area = tableMember.AssignedArea
            };

            for (int markerIndex = 0;
                 markerIndex < tableMarkers.Count;
                 markerIndex++)
            {
                BistroBuilder368AInstalledChair marker =
                    tableMarkers[markerIndex];

                if (group.ChairsBySlot.ContainsKey(marker.SlotIndex))
                {
                    throw new InvalidOperationException(
                        table.name + " tiene dos sillas para la plaza " +
                        marker.SlotIndex + "."
                    );
                }

                group.ChairsBySlot.Add(marker.SlotIndex, marker);
            }

            var slots = new List<RestaurantTableSeatSlot>(8);
            int slotCount = configuration.WriteCurrentSlots(slots);

            if (slotCount != table.Capacity ||
                group.ChairsBySlot.Count != slotCount)
            {
                throw new InvalidOperationException(
                    table.name + " no tiene una silla por plaza."
                );
            }

            groups.Add(group);
        }

        return groups;
    }

    private static GroupPose BuildGroupPose(
        TableGroup group,
        Vector3 tablePosition,
        Quaternion tableRotation
    )
    {
        var pose = new GroupPose
        {
            TablePosition = tablePosition,
            TableRotation = tableRotation
        };

        RestaurantPlacementFootprint tableFootprint =
            group.Table.GetComponent<RestaurantPlacementFootprint>();

        if (tableFootprint == null)
        {
            throw new InvalidOperationException(
                group.Table.name + " no tiene huella de colocación."
            );
        }

        pose.Shapes.Add(
            tableFootprint.BuildShapeAtPose(tablePosition, tableRotation)
        );

        var slots = new List<RestaurantTableSeatSlot>(8);
        group.Configuration.WriteSlotsAtPose(
            tablePosition,
            tableRotation,
            slots
        );

        for (int slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            RestaurantTableSeatSlot slot = slots[slotIndex];

            if (!group.ChairsBySlot.TryGetValue(
                    slot.SlotIndex,
                    out BistroBuilder368AInstalledChair marker
                ))
            {
                throw new InvalidOperationException(
                    group.Table.name + " no tiene silla para la plaza " +
                    slot.SlotIndex + "."
                );
            }

            RestaurantSeat seat = marker.GetComponent<RestaurantSeat>();
            RestaurantPlacementFootprint chairFootprint =
                marker.GetComponent<RestaurantPlacementFootprint>();

            if (seat == null || chairFootprint == null)
            {
                throw new InvalidOperationException(
                    marker.name + " no contiene silla y huella válidas."
                );
            }

            Quaternion chairRotation =
                seat.CalculateRootRotationForFacingDirection(
                    slot.FacingDirection
                );
            Vector3 chairPosition =
                seat.CalculateRootPositionForAssociationAtPose(
                    slot.AssociationPosition,
                    chairRotation
                );

            pose.ChairPoses.Add(
                slot.SlotIndex,
                new ChairPose(chairPosition, chairRotation)
            );
            pose.Shapes.Add(
                chairFootprint.BuildShapeAtPose(
                    chairPosition,
                    chairRotation
                )
            );

            RestaurantOperationalClearanceSet clearances =
                marker.GetComponent<RestaurantOperationalClearanceSet>();

            if (clearances == null)
            {
                continue;
            }

            for (int clearanceIndex = 0;
                 clearanceIndex < clearances.ClearanceCount;
                 clearanceIndex++)
            {
                if (clearances.TryBuildShapeAtPose(
                        clearanceIndex,
                        chairPosition,
                        chairRotation,
                        out RestaurantPlacementShape clearanceShape,
                        out _
                    ))
                {
                    pose.Shapes.Add(clearanceShape);
                }
            }
        }

        return pose;
    }

    private static List<Vector3> BuildCandidates(
        Bounds bounds,
        float y,
        Vector3 referenceObstaclePosition
    )
    {
        var candidates = new List<Vector3>(512);

        for (float z = bounds.min.z + CandidateBoundaryPadding;
             z <= bounds.max.z - CandidateBoundaryPadding;
             z += CandidateStep)
        {
            for (float x = bounds.min.x + CandidateBoundaryPadding;
                 x <= bounds.max.x - CandidateBoundaryPadding;
                 x += CandidateStep)
            {
                candidates.Add(new Vector3(x, y, z));
            }
        }

        Vector3 center = bounds.center;
        candidates.Sort((first, second) =>
        {
            float firstScore = CandidateScore(
                first,
                referenceObstaclePosition,
                center,
                bounds
            );
            float secondScore = CandidateScore(
                second,
                referenceObstaclePosition,
                center,
                bounds
            );
            int scoreComparison = secondScore.CompareTo(firstScore);

            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            int zComparison = first.z.CompareTo(second.z);
            return zComparison != 0
                ? zComparison
                : first.x.CompareTo(second.x);
        });

        return candidates;
    }

    private static float CandidateScore(
        Vector3 candidate,
        Vector3 referenceObstacle,
        Vector3 center,
        Bounds bounds
    )
    {
        float obstacleDistance =
            (candidate - referenceObstacle).sqrMagnitude;
        float edgeDistance = Mathf.Min(
            candidate.x - bounds.min.x,
            bounds.max.x - candidate.x,
            candidate.z - bounds.min.z,
            bounds.max.z - candidate.z
        );
        Vector3 horizontalCenterOffset = candidate - center;
        horizontalCenterOffset.y = 0f;

        return obstacleDistance +
               edgeDistance * 3f -
               horizontalCenterOffset.sqrMagnitude * 0.05f;
    }

    private static Vector3 ResolveReferenceObstaclePosition(
        RestaurantPlacementObstacle[] obstacles,
        Vector3 fallback
    )
    {
        for (int index = 0; index < obstacles.Length; index++)
        {
            RestaurantPlacementObstacle obstacle = obstacles[index];

            if (obstacle != null &&
                string.Equals(
                    obstacle.ObstacleId,
                    "placement_obstacle_367h_bar",
                    StringComparison.Ordinal
                ))
            {
                return obstacle.WorldCenter;
            }
        }

        return fallback;
    }

    private static bool IsPoseInsideArea(
        GroupPose pose,
        RestaurantArea area
    )
    {
        for (int index = 0; index < pose.Shapes.Count; index++)
        {
            RestaurantPlacementShape shape = pose.Shapes[index];

            if (!area.ContainsPosition(shape.Center) ||
                !area.ContainsPosition(shape.GetCorner(1f, 1f)) ||
                !area.ContainsPosition(shape.GetCorner(-1f, 1f)) ||
                !area.ContainsPosition(shape.GetCorner(-1f, -1f)) ||
                !area.ContainsPosition(shape.GetCorner(1f, -1f)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool OverlapsBlockingObstacle(
        GroupPose pose,
        RestaurantPlacementObstacle[] obstacles
    )
    {
        for (int obstacleIndex = 0;
             obstacleIndex < obstacles.Length;
             obstacleIndex++)
        {
            RestaurantPlacementObstacle obstacle = obstacles[obstacleIndex];

            if (obstacle == null || !obstacle.IsBlocking)
            {
                continue;
            }

            RestaurantPlacementShape obstacleShape =
                new RestaurantPlacementShape(
                    obstacle.WorldCenter,
                    obstacle.WorldRightAxis,
                    obstacle.WorldForwardAxis,
                    obstacle.WorldSize * 0.5f,
                    obstacle.MinimumClearance
                );

            for (int shapeIndex = 0;
                 shapeIndex < pose.Shapes.Count;
                 shapeIndex++)
            {
                if (ShapesOverlap(
                        pose.Shapes[shapeIndex],
                        obstacleShape
                    ))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool OverlapsSelectedGroups(
        GroupPose candidate,
        List<GroupPose> selected
    )
    {
        for (int selectedIndex = 0;
             selectedIndex < selected.Count;
             selectedIndex++)
        {
            GroupPose other = selected[selectedIndex];

            for (int firstIndex = 0;
                 firstIndex < candidate.Shapes.Count;
                 firstIndex++)
            {
                for (int secondIndex = 0;
                     secondIndex < other.Shapes.Count;
                     secondIndex++)
                {
                    if (ShapesOverlap(
                            candidate.Shapes[firstIndex],
                            other.Shapes[secondIndex]
                        ))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool ShapesOverlap(
        RestaurantPlacementShape first,
        RestaurantPlacementShape second
    )
    {
        Vector3[] axes =
        {
            first.RightAxis,
            first.ForwardAxis,
            second.RightAxis,
            second.ForwardAxis
        };

        float extra = first.MinimumClearance +
                      second.MinimumClearance +
                      ShapeTolerance;

        for (int index = 0; index < axes.Length; index++)
        {
            Vector3 axis = axes[index];
            float centerDistance =
                RestaurantPlacementShape.ProjectCenterDistance(
                    first,
                    second,
                    axis
                );
            float projectedRadius =
                first.CalculateProjectionRadius(axis, extra * 0.5f) +
                second.CalculateProjectionRadius(axis, extra * 0.5f);

            if (centerDistance >= projectedRadius)
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyPose(TableGroup group, GroupPose pose)
    {
        Undo.RecordObject(
            group.Table.transform,
            "Redistribuir mesa provisional 368B1"
        );
        group.Table.transform.SetPositionAndRotation(
            pose.TablePosition,
            pose.TableRotation
        );
        EditorUtility.SetDirty(group.Table.transform);
        EditorUtility.SetDirty(group.Table);

        foreach (KeyValuePair<int, BistroBuilder368AInstalledChair> pair
                 in group.ChairsBySlot)
        {
            BistroBuilder368AInstalledChair marker = pair.Value;
            ChairPose chairPose = pose.ChairPoses[pair.Key];
            Undo.RecordObject(
                marker.transform,
                "Redistribuir silla provisional 368B1"
            );
            marker.transform.SetPositionAndRotation(
                chairPose.Position,
                chairPose.Rotation
            );

            RestaurantAreaMember member =
                marker.GetComponent<RestaurantAreaMember>();

            if (member == null)
            {
                throw new InvalidOperationException(
                    marker.name + " no contiene RestaurantAreaMember."
                );
            }

            Undo.RecordObject(
                member,
                "Asignar área de silla provisional 368B1"
            );
            member.SetArea(group.Area);
            EditorUtility.SetDirty(member);
            EditorUtility.SetDirty(marker.transform);
            EditorUtility.SetDirty(marker);
        }
    }

    private static bool TryGetAreaBounds(
        RestaurantArea area,
        out Bounds bounds
    )
    {
        bounds = default;
        bool initialized = false;

        if (area == null || area.BoundaryColliders == null)
        {
            return false;
        }

        for (int index = 0;
             index < area.BoundaryColliders.Count;
             index++)
        {
            Collider collider = area.BoundaryColliders[index];

            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (!initialized)
            {
                bounds = collider.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(collider.bounds);
            }
        }

        return initialized;
    }

    private static T[] FindSceneObjects<T>(Scene scene)
        where T : Component
    {
        var results = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            results.AddRange(
                roots[rootIndex].GetComponentsInChildren<T>(true)
            );
        }

        return results.ToArray();
    }
}
