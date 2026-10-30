using System.Collections.Generic;

namespace CAIME.Classes.Importers
{
    /// <summary>
    /// What a hex is to the map_data builder. The values are the codes Warhammer III's
    /// map_data.esf stores per region area.
    /// </summary>
    public enum MapDataAreaType : byte
    {
        Land        = 0,
        Beach       = 1,
        Bridge      = 3,
        Sea         = 4,
        Cliff       = 6,
        Impassable  = 7,
        BridgeCliff = 8,
    }

    public sealed class MapDataRegionInfo
    {
        public string       Key                 { get; set; }

        /// <summary>
        /// The builder's sea flag. It is only set on sea regions that have passable sea, so a
        /// region without it can still be a sea region.
        /// </summary>
        public bool         HasPassableSea      { get; set; }

        /// <summary>The region's map.hex colour, or <see cref="MapDataContents.UnassignedColour"/>.</summary>
        public uint         Colour              { get; set; }

        public List<int>    PrimarySlotHexes    { get; } = new List<int>();
        public List<int>    PortSlotHexes       { get; } = new List<int>();
    }

    /// <summary>
    /// The contents of a map_data.esf that a map.hex can be rebuilt from, with every per-hex
    /// value in its own row-major array (index = row * width + column).
    /// </summary>
    public sealed class MapDataContents
    {
        /// <summary>
        /// The colour the builder writes for a region past the end of map.hex's colour array.
        /// </summary>
        public const uint UnassignedColour = 0xFFFFFF00;

        public const sbyte None = -1;

        public uint                     Width                   { get; set; }
        public uint                     Height                  { get; set; }

        /// <summary>Land regions first, then sea regions, as map.hex lists them.</summary>
        public List<MapDataRegionInfo>  Regions                 { get; } = new List<MapDataRegionInfo>();

        /// <summary>map.hex's land ground types followed by its sea ground types.</summary>
        public List<string>             GroundTypes             { get; } = new List<string>();
        public List<string>             Climates                { get; } = new List<string>();
        public List<string>             Attritions              { get; } = new List<string>();
        public List<string>             AreasOfInterest         { get; } = new List<string>();

        public int[]                    HexRegions              { get; set; }
        public MapDataAreaType[]        HexAreaTypes            { get; set; }
        public sbyte[]                  HexGroundTypes          { get; set; }
        public sbyte[]                  HexClimates             { get; set; }
        public sbyte[]                  HexAttritions           { get; set; }
        public sbyte[]                  HexAreasOfInterest      { get; set; }
        public byte[]                   HexRoadMasks            { get; set; }
        public byte[]                   HexRiverMasks           { get; set; }

        public int HexCount => (int)(Width * Height);
    }
}
