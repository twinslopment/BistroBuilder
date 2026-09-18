using System;

namespace BistroBuilder.FurnitureFinishes
{
    public enum FurnitureSurfaceFamily
    {
        Unknown = 0,
        Wood = 1,
        Fabric = 2,
        Leather = 3,
        Metal = 4,
        Stone = 5,
        Glass = 6,
        Paint = 7,
        Plastic = 8,
        Ceramic = 9,
        Other = 10
    }

    [Flags]
    public enum FurnitureFinishChannel
    {
        None = 0,
        BaseColorMap = 1 << 0,
        NormalMap = 1 << 1,
        MaskMap = 1 << 2,
        OcclusionMap = 1 << 3
    }
}