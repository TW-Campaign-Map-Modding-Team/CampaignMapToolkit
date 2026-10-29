using System.Collections.Generic;

namespace CAIME.Classes.Importers
{
    /// <summary>
    /// What a hex is to the map_data builder. The values are the codes every game's map_data.esf
    /// stores per region area.
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

    /// <summary>The three ways the games lay out map_data.esf, told apart by MAP_DATA's version.</summary>
    public enum MapDataLayout
    {
        /// <summary>MAP_DATA v0: Rome II, Attila and Thrones of Britannia.</summary>
        Attila      = 0,

        /// <summary>MAP_DATA v1: Warhammer, Warhammer II, Troy and Pharaoh.</summary>
        Pharaoh     = 1,

        /// <summary>MAP_DATA v2: Warhammer III and Three Kingdoms.</summary>
        Warhammer3  = 2,
    }

    public sealed class MapDataRegionInfo
    {
        public string       Key                 { get; set; }

        /// <summary>
        /// The builder's sea flag: whether the region's first area that is not impassable is sea or
        /// a bridge. A land region can have it, and a sea region can lack it.
        /// </summary>
        public bool         HasPassableSea      { get; set; }

        /// <summary>The region's map.hex colour, or <see cref="MapDataContents.UnassignedColour"/>.</summary>
        public uint         Colour              { get; set; }

        /// <summary>The settlement's town sprawl, as the builder fills it from the settlement.</summary>
        public List<int>    SprawlHexes         { get; } = new List<int>();

        public List<int>    PrimarySlotHexes    { get; } = new List<int>();
        public List<int>    PortSlotHexes       { get; } = new List<int>();

        /// <summary>The hexes of town slots 2 upwards (Attila and Pharaoh), from slot 2.</summary>
        public List<List<int>> OtherSlotHexes   { get; } = new List<List<int>>();
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

        public MapDataLayout            Layout                  { get; set; }

        /// <summary>MAP_FILE_DATA_VERSION, or null when the file has none (Attila's layout).</summary>
        public ushort?                  FileDataVersion         { get; set; }

        public uint                     Width                   { get; set; }
        public uint                     Height                  { get; set; }

        /// <summary>The theatre's name: the playable area's index.</summary>
        public string                   TheatreIndex            { get; set; }
        public float                    MaxX                    { get; set; }
        public float                    MaxY                    { get; set; }

        /// <summary>The campaign masks the regions are written for, the unnamed one first.</summary>
        public List<string>             MaskNames               { get; } = new List<string>();

        /// <summary>Land regions first, then sea regions, as map.hex lists them.</summary>
        public List<MapDataRegionInfo>  Regions                 { get; } = new List<MapDataRegionInfo>();

        /// <summary>map.hex's land ground types followed by its sea ground types.</summary>
        public List<string>             GroundTypes             { get; } = new List<string>();
        public List<string>             Climates                { get; } = new List<string>();
        public List<string>             Attritions              { get; } = new List<string>();

        /// <summary>Empty in the games without areas of interest.</summary>
        public List<string>             AreasOfInterest         { get; } = new List<string>();

        public int[]                    HexRegions              { get; set; }
        public MapDataAreaType[]        HexAreaTypes            { get; set; }
        public sbyte[]                  HexGroundTypes          { get; set; }
        public sbyte[]                  HexClimates             { get; set; }
        public sbyte[]                  HexAttritions           { get; set; }

        /// <summary>
        /// The area of interest byte as the builder copies it from map.hex: bits 3-8 of map.hex's
        /// bytes 8-9, so what it holds depends on the game (<see cref="MapHexFile.AreaOfInterestShift"/>).
        /// Zero in the games without areas of interest.
        /// </summary>
        public byte[]                   HexAreaOfInterestBits   { get; set; }
        public byte[]                   HexRoadMasks            { get; set; }
        public byte[]                   HexRiverMasks           { get; set; }

        public int HexCount => (int)(Width * Height);
    }
}
