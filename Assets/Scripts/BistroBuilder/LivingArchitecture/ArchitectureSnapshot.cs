using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Snapshot canónico de Arquitectura Viva. Es la unidad de propuesta, commit, rollback y persistencia.
    /// </summary>
    [Serializable]
    public sealed class ArchitectureSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion = CurrentSchemaVersion;
        public ArchitectureBuilding Building = new ArchitectureBuilding();

        public ArchitectureSnapshot DeepClone()
        {
            return new ArchitectureSnapshot
            {
                SchemaVersion = SchemaVersion,
                Building = Building?.DeepClone()
            };
        }

        /// <summary>
        /// Fingerprint topológico determinista para gates, rollback y futuros round-trips de Save/Load.
        /// Se ordena por ID para no depender del orden de listas.
        /// </summary>
        public string ComputeFingerprint()
        {
            var sb = new StringBuilder(1024);
            sb.Append("schema=").Append(SchemaVersion).Append('|');
            sb.Append("building=").Append(Building?.Id.Value ?? string.Empty).Append('|');

            if (Building?.Levels != null)
            {
                foreach (var level in Building.Levels.Where(x => x != null).OrderBy(x => x.Id.Value, StringComparer.Ordinal))
                {
                    sb.Append("level=").Append(level.Id.Value).Append('@').Append(level.Elevation.ToString("R")).Append('|');
                    foreach (var vertex in level.Vertices.Where(x => x != null).OrderBy(x => x.Id.Value, StringComparer.Ordinal))
                    {
                        sb.Append("v=").Append(vertex.Id.Value).Append('@').Append(vertex.Position.X.ToString("R")).Append(',').Append(vertex.Position.Y.ToString("R")).Append('|');
                    }
                    foreach (var wall in level.Walls.Where(x => x != null).OrderBy(x => x.Id.Value, StringComparer.Ordinal))
                    {
                        sb.Append("w=").Append(wall.Id.Value).Append(':').Append(wall.StartVertexId.Value).Append('>').Append(wall.EndVertexId.Value)
                            .Append('@').Append(wall.Thickness.ToString("R")).Append(',').Append(wall.Height.ToString("R")).Append('|');
                        foreach (var opening in wall.Openings.Where(x => x != null).OrderBy(x => x.Id.Value, StringComparer.Ordinal))
                        {
                            sb.Append("o=").Append(opening.Id.Value).Append(':').Append(opening.WallId.Value).Append('@')
                                .Append(opening.CenterT.ToString("R")).Append(',').Append(opening.Width.ToString("R")).Append(',')
                                .Append(opening.Bottom.ToString("R")).Append(',').Append(opening.Height.ToString("R")).Append('|');
                        }
                    }
                }
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(bytes.Length * 2);
                foreach (var value in bytes) hex.Append(value.ToString("x2"));
                return hex.ToString();
            }
        }

        public ArchitectureLevel FindLevel(LevelId levelId) => Building?.Levels?.FirstOrDefault(x => x != null && x.Id.Equals(levelId));
        public ArchitectureVertex FindVertex(VertexId vertexId) => Building?.Levels?.SelectMany(x => x?.Vertices ?? new List<ArchitectureVertex>()).FirstOrDefault(x => x != null && x.Id.Equals(vertexId));
        public ArchitectureWall FindWall(WallId wallId) => Building?.Levels?.SelectMany(x => x?.Walls ?? new List<ArchitectureWall>()).FirstOrDefault(x => x != null && x.Id.Equals(wallId));
    }
}
