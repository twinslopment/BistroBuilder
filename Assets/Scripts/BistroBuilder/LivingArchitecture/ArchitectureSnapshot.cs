using System;
using System.Collections.Generic;
using System.Globalization;
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
        /// Se ordena por ID y usa cultura invariante para no depender del equipo/idioma.
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
                    sb.Append("level=").Append(level.Id.Value).Append('@').Append(Format(level.Elevation)).Append('|');
                    foreach (var vertex in (level.Vertices ?? new List<ArchitectureVertex>()).Where(x => x != null).OrderBy(x => x.Id.Value, StringComparer.Ordinal))
                    {
                        sb.Append("v=").Append(vertex.Id.Value).Append('@').Append(Format(vertex.Position.X)).Append(',').Append(Format(vertex.Position.Y)).Append('|');
                    }
                    foreach (var wall in (level.Walls ?? new List<ArchitectureWall>()).Where(x => x != null).OrderBy(x => x.Id.Value, StringComparer.Ordinal))
                    {
                        sb.Append("w=").Append(wall.Id.Value).Append(':').Append(wall.StartVertexId.Value).Append('>').Append(wall.EndVertexId.Value)
                            .Append('@').Append(Format(wall.Thickness)).Append(',').Append(Format(wall.Height)).Append('|');
                        foreach (var opening in (wall.Openings ?? new List<ArchitectureOpening>()).Where(x => x != null).OrderBy(x => x.Id.Value, StringComparer.Ordinal))
                        {
                            sb.Append("o=").Append(opening.Id.Value).Append(':').Append(opening.WallId.Value).Append('@')
                                .Append(Format(opening.CenterT)).Append(',').Append(Format(opening.Width)).Append(',')
                                .Append(Format(opening.Bottom)).Append(',').Append(Format(opening.Height)).Append('|');
                        }
                    }
                }
            }

            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var hex = new StringBuilder(bytes.Length * 2);
                foreach (var value in bytes) hex.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        public ArchitectureLevel FindLevel(LevelId levelId) => Building?.Levels?.FirstOrDefault(x => x != null && x.Id.Equals(levelId));
        public ArchitectureVertex FindVertex(VertexId vertexId) => Building?.Levels?.SelectMany(x => x?.Vertices ?? new List<ArchitectureVertex>()).FirstOrDefault(x => x != null && x.Id.Equals(vertexId));
        public ArchitectureWall FindWall(WallId wallId) => Building?.Levels?.SelectMany(x => x?.Walls ?? new List<ArchitectureWall>()).FirstOrDefault(x => x != null && x.Id.Equals(wallId));

        private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    }
}
