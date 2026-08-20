using System;
using System.Collections.Generic;

namespace BistroBuilder.LivingArchitecture.Domain
{
    [Serializable]
    public sealed class ArchitectureVertex
    {
        public VertexId Id;
        public ArchitecturePoint Position;

        public ArchitectureVertex DeepClone() => new ArchitectureVertex { Id = Id, Position = Position };
    }

    [Serializable]
    public sealed class ArchitectureOpening
    {
        public OpeningId Id;
        public WallId WallId;
        public double CenterT;
        public double Width;
        public double Bottom;
        public double Height;

        public ArchitectureOpening DeepClone() => new ArchitectureOpening { Id = Id, WallId = WallId, CenterT = CenterT, Width = Width, Bottom = Bottom, Height = Height };
    }

    [Serializable]
    public sealed class ArchitectureWall
    {
        public WallId Id;
        public VertexId StartVertexId;
        public VertexId EndVertexId;
        public double Thickness;
        public double Height;
        public List<ArchitectureOpening> Openings = new List<ArchitectureOpening>();

        public ArchitectureWall DeepClone()
        {
            var clone = new ArchitectureWall { Id = Id, StartVertexId = StartVertexId, EndVertexId = EndVertexId, Thickness = Thickness, Height = Height };
            foreach (var opening in Openings) clone.Openings.Add(opening?.DeepClone());
            return clone;
        }
    }

    [Serializable]
    public sealed class ArchitectureLevel
    {
        public LevelId Id;
        public double Elevation;
        public List<ArchitectureVertex> Vertices = new List<ArchitectureVertex>();
        public List<ArchitectureWall> Walls = new List<ArchitectureWall>();

        public ArchitectureLevel DeepClone()
        {
            var clone = new ArchitectureLevel { Id = Id, Elevation = Elevation };
            foreach (var vertex in Vertices) clone.Vertices.Add(vertex?.DeepClone());
            foreach (var wall in Walls) clone.Walls.Add(wall?.DeepClone());
            return clone;
        }
    }

    [Serializable]
    public sealed class ArchitectureBuilding
    {
        public BuildingId Id;
        public List<ArchitectureLevel> Levels = new List<ArchitectureLevel>();

        public ArchitectureBuilding DeepClone()
        {
            var clone = new ArchitectureBuilding { Id = Id };
            foreach (var level in Levels) clone.Levels.Add(level?.DeepClone());
            return clone;
        }
    }
}
