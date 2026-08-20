using System;

namespace BistroBuilder.LivingArchitecture.Domain
{
    /// <summary>
    /// Base común para identidades arquitectónicas estables.
    /// Los IDs no dependen de nombres, índices, posiciones ni GameObjects.
    /// </summary>
    public static class ArchitectureId
    {
        public static string NewValue(string prefix)
        {
            return string.Concat(prefix, "_", Guid.NewGuid().ToString("N"));
        }

        public static bool IsValid(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }
    }

    [Serializable] public readonly struct BuildingId : IEquatable<BuildingId> { public readonly string Value; public BuildingId(string value) { Value = value; } public static BuildingId New() => new BuildingId(ArchitectureId.NewValue("bld")); public bool Equals(BuildingId other) => Value == other.Value; public override bool Equals(object obj) => obj is BuildingId other && Equals(other); public override int GetHashCode() => Value?.GetHashCode() ?? 0; public override string ToString() => Value ?? string.Empty; }
    [Serializable] public readonly struct LevelId : IEquatable<LevelId> { public readonly string Value; public LevelId(string value) { Value = value; } public static LevelId New() => new LevelId(ArchitectureId.NewValue("lvl")); public bool Equals(LevelId other) => Value == other.Value; public override bool Equals(object obj) => obj is LevelId other && Equals(other); public override int GetHashCode() => Value?.GetHashCode() ?? 0; public override string ToString() => Value ?? string.Empty; }
    [Serializable] public readonly struct VertexId : IEquatable<VertexId> { public readonly string Value; public VertexId(string value) { Value = value; } public static VertexId New() => new VertexId(ArchitectureId.NewValue("vtx")); public bool Equals(VertexId other) => Value == other.Value; public override bool Equals(object obj) => obj is VertexId other && Equals(other); public override int GetHashCode() => Value?.GetHashCode() ?? 0; public override string ToString() => Value ?? string.Empty; }
    [Serializable] public readonly struct WallId : IEquatable<WallId> { public readonly string Value; public WallId(string value) { Value = value; } public static WallId New() => new WallId(ArchitectureId.NewValue("wal")); public bool Equals(WallId other) => Value == other.Value; public override bool Equals(object obj) => obj is WallId other && Equals(other); public override int GetHashCode() => Value?.GetHashCode() ?? 0; public override string ToString() => Value ?? string.Empty; }
    [Serializable] public readonly struct OpeningId : IEquatable<OpeningId> { public readonly string Value; public OpeningId(string value) { Value = value; } public static OpeningId New() => new OpeningId(ArchitectureId.NewValue("opn")); public bool Equals(OpeningId other) => Value == other.Value; public override bool Equals(object obj) => obj is OpeningId other && Equals(other); public override int GetHashCode() => Value?.GetHashCode() ?? 0; public override string ToString() => Value ?? string.Empty; }
    [Serializable] public readonly struct RegionId : IEquatable<RegionId> { public readonly string Value; public RegionId(string value) { Value = value; } public static RegionId New() => new RegionId(ArchitectureId.NewValue("reg")); public bool Equals(RegionId other) => Value == other.Value; public override bool Equals(object obj) => obj is RegionId other && Equals(other); public override int GetHashCode() => Value?.GetHashCode() ?? 0; public override string ToString() => Value ?? string.Empty; }
    [Serializable] public readonly struct ArchitectureOperationId : IEquatable<ArchitectureOperationId> { public readonly string Value; public ArchitectureOperationId(string value) { Value = value; } public static ArchitectureOperationId New() => new ArchitectureOperationId(ArchitectureId.NewValue("aop")); public bool Equals(ArchitectureOperationId other) => Value == other.Value; public override bool Equals(object obj) => obj is ArchitectureOperationId other && Equals(other); public override int GetHashCode() => Value?.GetHashCode() ?? 0; public override string ToString() => Value ?? string.Empty; }
}
