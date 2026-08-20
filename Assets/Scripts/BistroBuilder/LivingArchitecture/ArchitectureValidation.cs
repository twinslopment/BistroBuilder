using System;
using System.Collections.Generic;
using System.Linq;

namespace BistroBuilder.LivingArchitecture.Domain
{
    public enum ArchitectureValidationSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    [Serializable]
    public sealed class ArchitectureValidationIssue
    {
        public ArchitectureValidationSeverity Severity;
        public string Code;
        public string EntityId;
        public string Message;
    }

    public sealed class ArchitectureValidationResult
    {
        public readonly List<ArchitectureValidationIssue> Issues = new List<ArchitectureValidationIssue>();
        public bool IsValid => Issues.All(x => x.Severity != ArchitectureValidationSeverity.Error);
        public int ErrorCount => Issues.Count(x => x.Severity == ArchitectureValidationSeverity.Error);
    }

    /// <summary>
    /// Gate canónico de invariantes LA1. No repara silenciosamente: describe y rechaza estados inválidos.
    /// </summary>
    public static class ArchitectureValidator
    {
        public static ArchitectureValidationResult Validate(ArchitectureSnapshot snapshot)
        {
            var result = new ArchitectureValidationResult();
            if (snapshot == null) { Error(result, "LA1_NULL_SNAPSHOT", null, "El snapshot no puede ser null."); return result; }
            if (snapshot.Building == null) { Error(result, "LA1_NULL_BUILDING", null, "El edificio no puede ser null."); return result; }
            if (!ArchitectureId.IsValid(snapshot.Building.Id.Value)) Error(result, "LA1_BUILDING_ID", snapshot.Building.Id.Value, "BuildingId inválido.");

            var levelIds = new HashSet<string>(StringComparer.Ordinal);
            var vertexIds = new HashSet<string>(StringComparer.Ordinal);
            var wallIds = new HashSet<string>(StringComparer.Ordinal);
            var openingIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var level in snapshot.Building.Levels ?? new List<ArchitectureLevel>())
            {
                if (level == null) { Error(result, "LA1_NULL_LEVEL", null, "La colección contiene un nivel null."); continue; }
                RequireUnique(result, levelIds, level.Id.Value, "LA1_LEVEL_ID", "LevelId inválido o duplicado.");

                var localVertices = new Dictionary<string, ArchitectureVertex>(StringComparer.Ordinal);
                foreach (var vertex in level.Vertices ?? new List<ArchitectureVertex>())
                {
                    if (vertex == null) { Error(result, "LA1_NULL_VERTEX", level.Id.Value, "La colección contiene un vértice null."); continue; }
                    RequireUnique(result, vertexIds, vertex.Id.Value, "LA1_VERTEX_ID", "VertexId inválido o duplicado.");
                    if (ArchitectureId.IsValid(vertex.Id.Value)) localVertices[vertex.Id.Value] = vertex;
                }

                foreach (var wall in level.Walls ?? new List<ArchitectureWall>())
                {
                    if (wall == null) { Error(result, "LA1_NULL_WALL", level.Id.Value, "La colección contiene una pared null."); continue; }
                    RequireUnique(result, wallIds, wall.Id.Value, "LA1_WALL_ID", "WallId inválido o duplicado.");
                    if (!localVertices.TryGetValue(wall.StartVertexId.Value ?? string.Empty, out var start)) Error(result, "LA1_WALL_START_MISSING", wall.Id.Value, "La pared referencia un vértice inicial inexistente en su nivel.");
                    if (!localVertices.TryGetValue(wall.EndVertexId.Value ?? string.Empty, out var end)) Error(result, "LA1_WALL_END_MISSING", wall.Id.Value, "La pared referencia un vértice final inexistente en su nivel.");
                    if (wall.StartVertexId.Equals(wall.EndVertexId)) Error(result, "LA1_WALL_SAME_VERTEX", wall.Id.Value, "Una pared no puede conectar un vértice consigo mismo.");
                    if (start != null && end != null && start.Position.DistanceTo(end.Position) <= ArchitectureGeometry.Epsilon) Error(result, "LA1_WALL_DEGENERATE", wall.Id.Value, "La longitud de pared debe superar el epsilon canónico.");
                    if (wall.Thickness <= ArchitectureGeometry.Epsilon) Error(result, "LA1_WALL_THICKNESS", wall.Id.Value, "El espesor debe ser positivo.");
                    if (wall.Height <= ArchitectureGeometry.Epsilon) Error(result, "LA1_WALL_HEIGHT", wall.Id.Value, "La altura debe ser positiva.");

                    var wallLength = start != null && end != null ? start.Position.DistanceTo(end.Position) : 0d;
                    foreach (var opening in wall.Openings ?? new List<ArchitectureOpening>())
                    {
                        if (opening == null) { Error(result, "LA1_NULL_OPENING", wall.Id.Value, "La colección contiene una apertura null."); continue; }
                        RequireUnique(result, openingIds, opening.Id.Value, "LA1_OPENING_ID", "OpeningId inválido o duplicado.");
                        if (!opening.WallId.Equals(wall.Id)) Error(result, "LA1_OPENING_OWNER", opening.Id.Value, "La apertura debe declarar la misma WallId que su pared contenedora.");
                        if (opening.CenterT < 0d || opening.CenterT > 1d) Error(result, "LA1_OPENING_T", opening.Id.Value, "CenterT debe estar en [0,1].");
                        if (opening.Width <= ArchitectureGeometry.Epsilon || opening.Height <= ArchitectureGeometry.Epsilon) Error(result, "LA1_OPENING_SIZE", opening.Id.Value, "La apertura debe tener ancho y alto positivos.");
                        if (opening.Bottom < 0d || opening.Bottom + opening.Height > wall.Height + ArchitectureGeometry.Epsilon) Error(result, "LA1_OPENING_VERTICAL_DOMAIN", opening.Id.Value, "La apertura excede el dominio vertical de la pared.");
                        if (wallLength > ArchitectureGeometry.Epsilon)
                        {
                            var halfParametricWidth = (opening.Width / wallLength) * 0.5d;
                            if (opening.CenterT - halfParametricWidth < -ArchitectureGeometry.Epsilon || opening.CenterT + halfParametricWidth > 1d + ArchitectureGeometry.Epsilon)
                                Error(result, "LA1_OPENING_HORIZONTAL_DOMAIN", opening.Id.Value, "La apertura excede la longitud útil de la pared.");
                        }
                    }
                }
            }

            return result;
        }

        private static void RequireUnique(ArchitectureValidationResult result, HashSet<string> set, string value, string code, string message)
        {
            if (!ArchitectureId.IsValid(value) || !set.Add(value)) Error(result, code, value, message);
        }

        private static void Error(ArchitectureValidationResult result, string code, string entityId, string message)
        {
            result.Issues.Add(new ArchitectureValidationIssue { Severity = ArchitectureValidationSeverity.Error, Code = code, EntityId = entityId, Message = message });
        }
    }
}
