using System;
using System.Collections.Generic;

namespace BistroBuilder.LivingArchitecture.Domain
{
    [Serializable]
    public sealed class ArchitecturePersistenceState
    {
        public int schemaVersion = ArchitecturePersistence.CurrentSchemaVersion;
        public string buildingId = string.Empty;
        public List<ArchitectureLevelPersistenceData> levels = new List<ArchitectureLevelPersistenceData>();
    }

    [Serializable]
    public sealed class ArchitectureLevelPersistenceData
    {
        public string levelId = string.Empty;
        public double elevation;
        public List<ArchitectureVertexPersistenceData> vertices = new List<ArchitectureVertexPersistenceData>();
        public List<ArchitectureWallPersistenceData> walls = new List<ArchitectureWallPersistenceData>();
    }

    [Serializable]
    public sealed class ArchitectureVertexPersistenceData
    {
        public string vertexId = string.Empty;
        public double x;
        public double y;
    }

    [Serializable]
    public sealed class ArchitectureWallPersistenceData
    {
        public string wallId = string.Empty;
        public string startVertexId = string.Empty;
        public string endVertexId = string.Empty;
        public double thickness;
        public double height;
        public List<ArchitectureOpeningPersistenceData> openings = new List<ArchitectureOpeningPersistenceData>();
    }

    [Serializable]
    public sealed class ArchitectureOpeningPersistenceData
    {
        public string openingId = string.Empty;
        public string wallId = string.Empty;
        public double centerT;
        public double width;
        public double bottom;
        public double height;
    }

    /// <summary>
    /// Frontera versionada entre el dominio arquitectónico y el SaveGame universal.
    /// El DTO evita depender de la serialización de readonly structs y nunca persiste regiones/meshes derivados.
    /// </summary>
    public static class ArchitecturePersistence
    {
        public const int CurrentSchemaVersion = 1;

        public static ArchitecturePersistenceState Capture(ArchitectureBuilding building)
        {
            if (building == null) throw new ArgumentNullException(nameof(building));
            var state = new ArchitecturePersistenceState { buildingId = building.Id.Value ?? string.Empty };
            var levels = new List<ArchitectureLevel>(building.Levels ?? new List<ArchitectureLevel>());
            levels.Sort((a, b) => string.CompareOrdinal(a?.Id.Value, b?.Id.Value));
            foreach (var level in levels)
            {
                if (level == null) continue;
                var levelData = new ArchitectureLevelPersistenceData { levelId = level.Id.Value ?? string.Empty, elevation = level.Elevation };
                var vertices = new List<ArchitectureVertex>(level.Vertices ?? new List<ArchitectureVertex>());
                vertices.Sort((a, b) => string.CompareOrdinal(a?.Id.Value, b?.Id.Value));
                foreach (var vertex in vertices)
                {
                    if (vertex == null) continue;
                    levelData.vertices.Add(new ArchitectureVertexPersistenceData { vertexId = vertex.Id.Value ?? string.Empty, x = vertex.Position.X, y = vertex.Position.Y });
                }
                var walls = new List<ArchitectureWall>(level.Walls ?? new List<ArchitectureWall>());
                walls.Sort((a, b) => string.CompareOrdinal(a?.Id.Value, b?.Id.Value));
                foreach (var wall in walls)
                {
                    if (wall == null) continue;
                    var wallData = new ArchitectureWallPersistenceData
                    {
                        wallId = wall.Id.Value ?? string.Empty,
                        startVertexId = wall.StartVertexId.Value ?? string.Empty,
                        endVertexId = wall.EndVertexId.Value ?? string.Empty,
                        thickness = wall.Thickness,
                        height = wall.Height
                    };
                    var openings = new List<ArchitectureOpening>(wall.Openings ?? new List<ArchitectureOpening>());
                    openings.Sort((a, b) => string.CompareOrdinal(a?.Id.Value, b?.Id.Value));
                    foreach (var opening in openings)
                    {
                        if (opening == null) continue;
                        wallData.openings.Add(new ArchitectureOpeningPersistenceData
                        {
                            openingId = opening.Id.Value ?? string.Empty,
                            wallId = opening.WallId.Value ?? string.Empty,
                            centerT = opening.CenterT,
                            width = opening.Width,
                            bottom = opening.Bottom,
                            height = opening.Height
                        });
                    }
                    levelData.walls.Add(wallData);
                }
                state.levels.Add(levelData);
            }
            return state;
        }

        public static bool TryRestore(ArchitecturePersistenceState source, out ArchitectureBuilding building, out string error)
        {
            building = null;
            if (!TryMigrate(source, out var state, out error)) return false;
            try
            {
                var restored = new ArchitectureBuilding { Id = new BuildingId(state.buildingId) };
                foreach (var levelData in state.levels ?? new List<ArchitectureLevelPersistenceData>())
                {
                    if (levelData == null) continue;
                    var level = new ArchitectureLevel { Id = new LevelId(levelData.levelId), Elevation = levelData.elevation };
                    foreach (var vertexData in levelData.vertices ?? new List<ArchitectureVertexPersistenceData>())
                    {
                        if (vertexData == null) continue;
                        level.Vertices.Add(new ArchitectureVertex { Id = new VertexId(vertexData.vertexId), Position = new ArchitecturePoint(vertexData.x, vertexData.y) });
                    }
                    foreach (var wallData in levelData.walls ?? new List<ArchitectureWallPersistenceData>())
                    {
                        if (wallData == null) continue;
                        var wall = new ArchitectureWall
                        {
                            Id = new WallId(wallData.wallId),
                            StartVertexId = new VertexId(wallData.startVertexId),
                            EndVertexId = new VertexId(wallData.endVertexId),
                            Thickness = wallData.thickness,
                            Height = wallData.height
                        };
                        foreach (var openingData in wallData.openings ?? new List<ArchitectureOpeningPersistenceData>())
                        {
                            if (openingData == null) continue;
                            wall.Openings.Add(new ArchitectureOpening
                            {
                                Id = new OpeningId(openingData.openingId),
                                WallId = new WallId(openingData.wallId),
                                CenterT = openingData.centerT,
                                Width = openingData.width,
                                Bottom = openingData.bottom,
                                Height = openingData.height
                            });
                        }
                        level.Walls.Add(wall);
                    }
                    restored.Levels.Add(level);
                }
                var validation = ArchitectureValidator.Validate(new ArchitectureSnapshot { Building = restored });
                if (!validation.IsValid)
                {
                    var messages = new List<string>();
                    foreach (var issue in validation.Issues)
                    {
                        if (issue != null && issue.Severity == ArchitectureValidationSeverity.Error)
                            messages.Add(issue.Code + ":" + issue.Message);
                    }
                    error = "LA8_RESTORE_INVALID: " + string.Join(" | ", messages.ToArray());
                    return false;
                }
                building = restored;
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = "LA8_RESTORE_EXCEPTION: " + ex.Message;
                return false;
            }
        }

        public static bool TryMigrate(ArchitecturePersistenceState source, out ArchitecturePersistenceState migrated, out string error)
        {
            migrated = null;
            if (source == null) { error = "LA8_STATE_NULL"; return false; }
            if (source.schemaVersion < 0 || source.schemaVersion > CurrentSchemaVersion)
            {
                error = "LA8_SCHEMA_UNSUPPORTED_" + source.schemaVersion;
                return false;
            }
            migrated = CloneState(source);
            if (migrated.schemaVersion == 0)
            {
                foreach (var level in migrated.levels ?? new List<ArchitectureLevelPersistenceData>())
                foreach (var wall in level?.walls ?? new List<ArchitectureWallPersistenceData>())
                {
                    if (wall == null) continue;
                    if (wall.thickness <= ArchitectureGeometry.Epsilon) wall.thickness = 0.10d;
                    if (wall.height <= ArchitectureGeometry.Epsilon) wall.height = 3.0d;
                    foreach (var opening in wall.openings ?? new List<ArchitectureOpeningPersistenceData>())
                        if (opening != null && string.IsNullOrWhiteSpace(opening.wallId)) opening.wallId = wall.wallId;
                }
                migrated.schemaVersion = 1;
            }
            error = string.Empty;
            return true;
        }

        private static ArchitecturePersistenceState CloneState(ArchitecturePersistenceState source)
        {
            var clone = new ArchitecturePersistenceState { schemaVersion = source.schemaVersion, buildingId = source.buildingId ?? string.Empty };
            foreach (var level in source.levels ?? new List<ArchitectureLevelPersistenceData>())
            {
                if (level == null) continue;
                var lc = new ArchitectureLevelPersistenceData { levelId = level.levelId ?? string.Empty, elevation = level.elevation };
                foreach (var v in level.vertices ?? new List<ArchitectureVertexPersistenceData>()) if (v != null) lc.vertices.Add(new ArchitectureVertexPersistenceData { vertexId = v.vertexId ?? string.Empty, x = v.x, y = v.y });
                foreach (var w in level.walls ?? new List<ArchitectureWallPersistenceData>())
                {
                    if (w == null) continue;
                    var wc = new ArchitectureWallPersistenceData { wallId = w.wallId ?? string.Empty, startVertexId = w.startVertexId ?? string.Empty, endVertexId = w.endVertexId ?? string.Empty, thickness = w.thickness, height = w.height };
                    foreach (var o in w.openings ?? new List<ArchitectureOpeningPersistenceData>()) if (o != null) wc.openings.Add(new ArchitectureOpeningPersistenceData { openingId = o.openingId ?? string.Empty, wallId = o.wallId ?? string.Empty, centerT = o.centerT, width = o.width, bottom = o.bottom, height = o.height });
                    lc.walls.Add(wc);
                }
                clone.levels.Add(lc);
            }
            return clone;
        }
    }
}
