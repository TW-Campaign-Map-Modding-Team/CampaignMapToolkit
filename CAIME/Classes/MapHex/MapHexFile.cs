using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using Force.Crc32;

namespace CAIME
{
    public enum HexType : byte
    {
        Land = 0,
        Sea,
        Impassable,
        Beach,
        SettlementLand,
        BridgeCliff,
        River,
        SettlementBeach,
        SettlementSea,
        SettlementRiver
    }

    public class LayerChangedEventArgs : RoutedEventArgs
    {
        public LayerType LayerType { get; private set; }

        public LayerChangedEventArgs(LayerType layerType)
        {
            LayerType = layerType;
        }
    }

    public delegate void LayerChangedEventHandler(object sender, LayerChangedEventArgs e);

    public class MapHexFile
    {
        private readonly static DateTime    UNIX_BASE               = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        private const int                   BASE_LAND_COLOURS_COUNT = 290;
        private const int                   BASE_SEA_COLOURS_COUNT  = 289;

        // In-app minor version for Pharaoh Dynasties maps. On disk they are 0x14 like Warhammer 3,
        // but they store regions in a single combined array preceded by a region type array, and
        // have no Areas of Interest. Vanilla Pharaoh maps are plain 0x12 and need no marker.
        public const int                    FAKE_DYNASTIES_MINOR_VER = 0xFF;

        // On-disk minor version a Pharaoh Dynasties map is written with.
        private const int                   DYNASTIES_MINOR_VER      = 0x14;

        private bool isDirty;
        public bool IsDirty
        {
            get
            {
                return isDirty || ColoursLand.IsDirty || ColoursSea.IsDirty;
            }
        }

        // Edge masks (road/river/trade-route/region) are authored data: when a map is loaded
        // they are read verbatim from the .hex (ReadHexData16) and cannot always be reproduced
        // exactly by recomputation (e.g. road/river triangle chords are paint-order dependent).
        // The masks are therefore preserved unless the user actually edits the layer:
        //  - a "...NeedRecalculation" flag requests a FULL rebuild (set by the layer importer);
        //  - a "...DirtyHexes" set requests an INCREMENTAL rebuild of just the changed hexes and
        //    their neighbours (populated by the swatches), so authored masks elsewhere survive.
        // Static because swatches are shared, MapHexFile-less singletons and the editor is
        // single-document. All reset on Load / new map.
        public static bool RoadMasksNeedRecalculation   { get; set; }
        public static bool RiverMasksNeedRecalculation  { get; set; }
        public static bool RegionMasksNeedRecalculation { get; set; }
        public static bool TradeMasksNeedRecalculation  { get; set; }

        public static readonly HashSet<int> RoadDirtyHexes   = new HashSet<int>();
        public static readonly HashSet<int> RiverDirtyHexes  = new HashSet<int>();
        public static readonly HashSet<int> RegionDirtyHexes = new HashSet<int>();
        public static readonly HashSet<int> TradeDirtyHexes  = new HashSet<int>();

        // Monotonic counters stamped onto a hex (Hex.RoadPaintSeq/RiverPaintSeq) when it is
        // painted, so triangle resolution follows paint order. Reset on Load/new map.
        public static int RoadPaintCounter  { get; set; }
        public static int RiverPaintCounter { get; set; }

        private static void ResetMaskEditState()
        {
            RoadMasksNeedRecalculation   = false;
            RiverMasksNeedRecalculation  = false;
            RegionMasksNeedRecalculation = false;
            TradeMasksNeedRecalculation  = false;
            RoadDirtyHexes.Clear();
            RiverDirtyHexes.Clear();
            RegionDirtyHexes.Clear();
            TradeDirtyHexes.Clear();
            RoadPaintCounter  = 0;
            RiverPaintCounter = 0;
        }

        public readonly static int          MAX_HEX_COUNT           = 731520;

        public int                          MajorFileVersion        { get; private set; }
        public int                          MinorFileVersion        { get; private set; }

        public string                       GameName                { get; private set; }
        public string                       CampaignMapName         { get; private set; }

        public ColoursContainer             ColoursLand             { get; private set; }
        public ColoursContainer             ColoursSea              { get; private set; }

        public uint                         MapWidth                { get; private set; }
        public uint                         MapHeight               { get; private set; }
        public uint                         Capacity                { get; private set; }

        public Hex[]                        HexData                 { get; private set; }

        public List<string>                 LandRegions             { get; private set; }
        public List<string>                 SeaRegions              { get; private set; }
        public List<string>                 LandGroundTypes         { get; private set; }
        public List<string>                 SeaGroundTypes          { get; private set; }
        public List<string>                 Climates                { get; private set; }
        public List<string>                 Attritions              { get; private set; }
        public List<string>                 AreasOfInterest         { get; private set; }

        private Dictionary<int, int>        RegionIndexRemapTable;

        public List<KeyValuePair<string, uint>> UnknownEntries { get; private set; } = DefaultUnknownEntries();

        private int                         unknownTrailingBlockCount = 1;

        private static List<KeyValuePair<string, uint>> DefaultUnknownEntries()
        {
            return new List<KeyValuePair<string, uint>> { new KeyValuePair<string, uint>("", 0) };
        }

        public LayerChangedEventHandler     OnLayerChanged;

        public MapHexFile()
        {
            isDirty                         = false;
            MajorFileVersion                = 0;
            MinorFileVersion                = 0;
        }

        private void AppendChecksum(string mapBinPath, string mapHexPath)
        {
            var inputData   = File.ReadAllBytes(mapBinPath);
            var outputData  = new byte[inputData.Length + 4];

            Array.Copy(inputData, 0, outputData, 0, inputData.Length);
            Crc32Algorithm.ComputeAndWriteToEnd(outputData);

            // Write to a temp file and swap it in, so a failure mid-write cannot
            // destroy an existing .hex.
            var tmpPath = mapHexPath + ".tmp";
            File.WriteAllBytes(tmpPath, outputData);

            if (File.Exists(mapHexPath))
            {
                File.Replace(tmpPath, mapHexPath, null);
            }
            else
            {
                File.Move(tmpPath, mapHexPath);
            }

            File.Delete(mapBinPath);
        }

        private bool IsSupportedFileVersion(int minorVersion)
        {
            // 0x0D (13) - Rome II
            // 0x0F (15) - Rome II, Attila, Thrones of Britannia
            // 0x12 (18) - WH1, WH2, Troy
            // 0x13 (19) - Three Kingdoms
            // 0x14 (20) - WH3, Pharaoh (in map.hex file)
            // 0xFF (255) - Pharaoh (in the app). Fake minor version used for Pharaoh maps that differ from WH3 maps by storing regions in a combined array and by storing region type array in front of it. Also doesn't have Areas of Interest.

            return minorVersion == 0x0D || minorVersion == 0x0F || minorVersion == 0x12 || minorVersion == 0x13 || minorVersion == 0x14 || minorVersion == FAKE_DYNASTIES_MINOR_VER;
        }

        public bool Load(string mapHexPath)
        {
            if (mapHexPath == null || mapHexPath.Length == 0)
            {
                return false;
            }

            // Authored masks are about to be read verbatim - preserve them until the user
            // actually edits the relevant layer.
            ResetMaskEditState();

            using (var br = new BinaryReader(new FileStream(mapHexPath, FileMode.Open, FileAccess.Read)))
            {
                this.ReadHeader(br);

                if (this.IsSupportedFileVersion(MinorFileVersion) == false)
                {
                    LoggerViewModel.Log($"Unsupported or unknown file version: {MinorFileVersion}", LogLevel.ErrorMessageBox);
                    return false;
                }

                if (MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                {
                    var regionTypes     = this.ReadIntArray(br);
                    var combinedRegions = this.ReadStringArray(br);

                    if (regionTypes.Length != combinedRegions.Length)
                    {
                        LoggerViewModel.Log($"Mismatch between region types and combined regions: {regionTypes.Length} != {combinedRegions.Length}", LogLevel.ErrorMessageBox);
                        return false;
                    }

                    LandRegions         = this.GetRegionsFromCombinedList(HexType.Land, regionTypes, combinedRegions);
                    SeaRegions          = this.GetRegionsFromCombinedList(HexType.Sea, regionTypes, combinedRegions);

                    CreateRegionIndexRemapTable(combinedRegions);
                }
                else
                {
                    LandRegions         = new List<string>(this.ReadStringArray(br));
                    SeaRegions          = new List<string>(this.ReadStringArray(br));
                }

                LandGroundTypes         = new List<string>(this.ReadStringArray(br));
                SeaGroundTypes          = new List<string>(this.ReadStringArray(br));
                Climates                = new List<string>(this.ReadStringArray(br));
                Attritions              = new List<string>(this.ReadStringArray(br));

                if (MinorFileVersion == 0x13 || MinorFileVersion == 0x14)
                {
                    AreasOfInterest     = new List<string>(this.ReadStringArray(br));
                }

                if (MinorFileVersion == 0x12 || MinorFileVersion == 0x14 || MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                {
                    this.ReadUnknownEntries(br);
                }

                var landColours         = this.ReadIntArray(br);
                var seaColours          = this.ReadIntArray(br);

                ColoursLand             = new ColoursContainer(landColours);
                ColoursSea              = new ColoursContainer(seaColours);

                this.ReadHexData(br, MinorFileVersion);

                if (MinorFileVersion == 0x12 || MinorFileVersion == 0x14 || MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                {
                    this.ReadUnknownTrailingBlocks(br);
                }
            }

            this.UpdateBridgeCliffs();

            return true;
        }

        public bool Save(string filePath, string fileName, IReadOnlyList<string> combinedRegionOrder = null, bool clearDirtyFlag = true)
        {
            this.UpdateHexTypes();

            // Keep the authored masks that were loaded from the source .hex; only regenerate
            // (fully on import, incrementally on paint) the layers the user actually edited.
            this.EnsureRoadEdgeMasks();
            this.EnsureRiverEdgeMasks();
            this.EnsureRegionEdgeMasks();
            this.EnsureTradeRouteEdgeMasks();

            var mapBinPath = $"{filePath}\\{fileName}.bin";
            var mapHexPath = $"{filePath}\\{fileName}.hex";

            // Region indices are temporarily rewritten into their on-disk (combined) form below.
            // If anything throws in between, the in-memory model must not be left holding them.
            bool holdingCombinedRegionIndices = false;

            try
            {
                using (var bw = new BinaryWriter(new FileStream(mapBinPath, FileMode.Create, FileAccess.Write)))
                {
                    this.WriteHeader(bw, MajorFileVersion, MinorFileVersion, GameName, CampaignMapName);

                    if (MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                    {
                        this.RebuildRegionIndexRemapTable(combinedRegionOrder);
                        this.WriteCombinedStringArray(bw, LandRegions, SeaRegions);
                        this.SetCombinedRegionIndices();
                        holdingCombinedRegionIndices = true;
                    }
                    else
                    {
                        this.WriteStringArray(bw, LandRegions);
                        this.WriteStringArray(bw, SeaRegions);
                    }

                    this.WriteStringArray(bw, LandGroundTypes);
                    this.WriteStringArray(bw, SeaGroundTypes);
                    this.WriteStringArray(bw, Climates);
                    this.WriteStringArray(bw, Attritions);

                    if (MinorFileVersion == 0x13 || MinorFileVersion == 0x14)
                    {
                        this.WriteStringArray(bw, AreasOfInterest);
                    }

                    if (MinorFileVersion == 0x12 || MinorFileVersion == 0x14 || MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                    {
                        this.WriteUnknownEntries(bw);
                    }

                    this.WriteIntArray(bw, ColoursLand.Colours);
                    this.WriteIntArray(bw, ColoursSea.Colours);
                    this.WriteHexData(bw, MinorFileVersion, MapWidth, MapHeight, HexData);

                    if (MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                    {
                        this.RestoreSeparateRegionIndices();
                        holdingCombinedRegionIndices = false;
                    }

                    if (MinorFileVersion == 0x12 || MinorFileVersion == 0x14 || MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                    {
                        this.WriteUnknownTrailingBlocks(bw, MapWidth, MapHeight);
                    }
                }

                this.AppendChecksum(mapBinPath, mapHexPath);
            }
            finally
            {
                if (holdingCombinedRegionIndices)
                {
                    this.RestoreSeparateRegionIndices();
                }

                // Don't leave a stray .bin behind if writing or the checksum swap failed.
                if (File.Exists(mapBinPath))
                {
                    File.Delete(mapBinPath);
                }
            }

            if (clearDirtyFlag)
            {
                isDirty = false;
                ColoursLand.SetDirty(false);
                ColoursSea.SetDirty(false);
            }

            LoggerViewModel.Log($"Saved {fileName}.hex to {filePath}", LogLevel.Info);
            return true;
        }

        public bool ResizeMapHex(uint newWidth, uint newHeight, int newPadRight, int newPadLeft, int newPadTop, int newPadBottom)
        {
            var resizedHexData = CreateEmptyHexData(newWidth, newHeight);

            for (int row = 0; row < newHeight; row++)
            {
                for (int col = 0; col < newWidth; col++)
                {
                    // Calculate the corresponding original array coordinates
                    int originalCol = col - newPadLeft;
                    int originalRow = row - newPadBottom;

                    if (originalCol < 0 || originalCol >= this.MapWidth || originalRow < 0 || originalRow >= this.MapHeight)
                    {
                        // If outside the original array, use the closest existing hex
                        if (originalCol < 0)
                        {
                            originalCol = 0;
                        }
                        else
                        if (originalCol >= this.MapWidth)
                        {
                            originalCol = (int)this.MapWidth - 1;
                        }

                        if (originalRow < 0)
                        {
                            originalRow = 0;
                        }
                        else
                        if (originalRow >= this.MapHeight)
                        {
                            originalRow = (int)this.MapHeight - 1;
                        }
                    }

                    // Copy the value from the original array. Clone the hex: when padding clamps
                    // to an edge hex, the same source hex is used for several target cells, and
                    // sharing one mutable instance between them would alias/corrupt the grid.
                    var hex = this.HexData[originalRow * this.MapWidth + originalCol].Clone();
                    hex.Q = col;
                    hex.R = row;
                    hex.Index = (int)(row * newWidth + col);
                    resizedHexData[row * newWidth + col] = hex;
                }
            }

            this.MapWidth = newWidth;
            this.MapHeight = newHeight;
            this.Capacity = newWidth * newHeight;
            this.HexData = resizedHexData;

            return true;
        }

        #region Hex data update

        public void UpdateHexTypes()
        {
            this.UpdateLandSea();
            this.UpdateCliffs();
            this.UpdateBridgeCliffs();

            for (int hexIndex = 0; hexIndex < Capacity; ++hexIndex)
            {
                var hex = HexData[hexIndex];
                hex.UpdateHexType();
            }
        }

        public void UpdateLandSea()
        {
            for (int hexIndex = 0; hexIndex < Capacity; ++hexIndex)
            {
                var hex = HexData[hexIndex];
                if (hex.RegionId == Hex.INVALID_REGION_INDEX)
                    continue;

                hex.IsSea = this.IsSeaGroundType(hex.GroundTypeIndex);
            }

            isDirty = true;
        }

        public void UpdateCliffs()
        {
            for (int hexIndex = 0; hexIndex < Capacity; ++hexIndex)
            {
                var hex = HexData[hexIndex];

                hex.IsCliff = false;

                if (hex.IsBeach) // || hex.IsImpassable)
                    continue;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                        continue;

                    var neighbour = HexData[nbrIndex];
                    if (hex.IsLand && neighbour.IsSea)
                    {
                        hex.IsCliff = true;
                        break;
                    }
                }
            }

            isDirty = true;
        }

        private void UpdateBridgeCliffs()
        {
            for (int hexIndex = 0; hexIndex < Capacity; ++hexIndex)
            {
                var hex = HexData[hexIndex];
                hex.IsBridgeCliff = false;

                if (hex.IsSea)
                    continue; // Only land hexes can be Bridge Cliffs

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                        continue;

                    var neighbour = HexData[nbrIndex];
                    if (neighbour.IsSea && neighbour.IsBridge)
                    {
                        hex.IsBridgeCliff = true;
                        break;
                    }
                }
            }
        }

        // Rome II, Attila, and Thrones of Britannia (file format 0x0D/0x0F) store through-town
        // roads as disconnected stubs in their real shipped map data: a town's interior road
        // hexes only connect to each other when the town's road network has a single entrance.
        // Verified against real game data - Attila's main_attila_map: single-exit interior
        // edges are 11/11 connected, multi-exit ones are 0/3. Newer formats (WH1/WH2/Troy/3K/
        // WH3/Pharaoh) keep interior roads fully connected like anywhere else on the map -
        // Warhammer 3's wh3_main_combi_map_5: multi-exit interior edges are 1524/1583 (96%)
        // connected - so this only applies to the older format.
        private bool UsesDisconnectedTownInteriorRoads => MinorFileVersion == 0x0D || MinorFileVersion == 0x0F;

        /// <summary>
        /// Deadend answers for town-interior road components, valid for the duration of one mask
        /// pass. The answer is a property of the whole component, but the callers ask once per
        /// (road hex, direction), so it is cached for every member the search walks.
        /// </summary>
        private Dictionary<int, bool> _townRoadDeadendCache;

        private bool IsTownRoadDeadend(int startHexIndex)
        {
            var startHex = HexData[startHexIndex];
            if (startHex.IsRoad == false || startHex.TownSlotIndex != Hex.MAIN_SLOT_INDEX)
            {
                return false;
            }

            if (_townRoadDeadendCache != null && _townRoadDeadendCache.TryGetValue(startHexIndex, out bool cached))
            {
                return cached;
            }

            var endpoints = new HashSet<int>();
            var component = new List<int>();
            var visited = new HashSet<int>();
            var queue = new Queue<int>();

            queue.Enqueue(startHexIndex);
            visited.Add(startHexIndex);

            while (queue.Count > 0)
            {
                var hexIndex = queue.Dequeue();
                var hex = HexData[hexIndex];

                if (hex.TownSlotIndex != Hex.MAIN_SLOT_INDEX)
                {
                    endpoints.Add(hexIndex);
                    continue;
                }

                component.Add(hexIndex);

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    if (visited.Contains(nbrIndex))
                    {
                        continue;
                    }

                    visited.Add(nbrIndex);

                    var nbr = HexData[nbrIndex];
                    if (nbr.IsRoad)
                    {
                        queue.Enqueue(nbrIndex);
                    }
                }
            }

            bool isDeadend = endpoints.Count == 1;

            if (_townRoadDeadendCache != null)
            {
                foreach (var hexIndex in component)
                {
                    _townRoadDeadendCache[hexIndex] = isDeadend;
                }
            }

            return isDeadend;
        }

        public void CalculateRoadEdgeMasks()
        {
            // The road network does not change during the pass, so deadend answers can be cached.
            _townRoadDeadendCache = new Dictionary<int, bool>();
            try
            {
                CalculateRoadEdgeMasksCore();
            }
            finally
            {
                _townRoadDeadendCache = null;
            }
        }

        private void CalculateRoadEdgeMasksCore()
        {
            // Zero every mask up-front so triangle prevention can read the masks that
            // accumulate as hexes are processed.
            for (int i = 0; i < Capacity; ++i)
            {
                HexData[i].RoadEdgeMask = 0;
            }

            // Process road hexes in paint order (RoadPaintSeq): loaded/unpainted hexes
            // (seq 0) first, then painted hexes in the order they were painted. The
            // earlier-painted hex of a triangle becomes the apex and the chord the later
            // paint would have used to close the loop is dropped - this is the paint-order
            // dependent triangle resolution the original game data exhibits.
            foreach (int hexIndex in GetMaskProcessingOrder(isRoad: true))
            {
                var hex = HexData[hexIndex];

                var bordersBridge = false;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    var nbr = HexData[nbrIndex];
                    if (nbr.IsRoad)
                    {
                        if (UsesDisconnectedTownInteriorRoads && hex.TownSlotIndex == Hex.MAIN_SLOT_INDEX && nbr.TownSlotIndex == Hex.MAIN_SLOT_INDEX)
                        {
                            if (IsTownRoadDeadend(hexIndex) == false)
                            {
                                continue;
                            }
                        }

                        // Skip edges that would close a triangle (3 mutually-connected hexes).
                        if (WouldCloseTriangle(hexIndex, nbrIndex, dir, isRoad: true))
                        {
                            continue;
                        }

                        hex.RoadEdgeMask |= (byte)(1 << dir);
                        nbr.RoadEdgeMask |= (byte)(1 << HexGridUtility.InverseDir(dir));
                    }
                    else
                    if (nbr.IsBridge)
                    {
                        bordersBridge = true;
                    }
                }

                // For deadends that are connected by a bridge
                if (bordersBridge && HexGridUtility.GetNumBitsSetInEdgeMask(hex.RoadEdgeMask) == 1)
                {
                    var dir = (ushort)HexGridUtility.GetDirFromEdgeMask(hex.RoadEdgeMask);
                    var invDir = HexGridUtility.InverseDir(dir);
                    hex.RoadEdgeMask |= (byte)(1 << invDir);
                }
            }

            isDirty = true;
        }

        public void CalculateRiverEdgeMasks()
        {
            // Zero every mask up-front so triangle prevention can read the masks that
            // accumulate as hexes are processed.
            for (int i = 0; i < Capacity; ++i)
            {
                HexData[i].RiverEdgeMask = 0;
            }

            // Process river hexes in paint order (see CalculateRoadEdgeMasks for the rationale).
            foreach (int hexIndex in GetMaskProcessingOrder(isRoad: false))
            {
                var hex = HexData[hexIndex];

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    var nbr = HexData[nbrIndex];
                    if (nbr.IsRiver)
                    {
                        // Skip edges that would close a triangle (3 mutually-connected hexes).
                        if (WouldCloseTriangle(hexIndex, nbrIndex, dir, isRoad: false))
                        {
                            continue;
                        }

                        hex.RiverEdgeMask |= (byte)(1 << dir);
                        nbr.RiverEdgeMask |= (byte)(1 << HexGridUtility.InverseDir(dir));
                    }
                }
            }

            isDirty = true;
        }

        /// <summary>
        /// Returns road/river hex indices ordered by paint sequence (loaded/unpainted hexes
        /// first, then painted hexes in paint order), so triangle resolution follows the
        /// order in which the roads/rivers were painted.
        /// </summary>
        private List<int> GetMaskProcessingOrder(bool isRoad)
        {
            var hexes = new List<int>();
            for (int i = 0; i < Capacity; ++i)
            {
                if (isRoad ? HexData[i].IsRoad : HexData[i].IsRiver)
                {
                    hexes.Add(i);
                }
            }

            hexes.Sort((a, b) =>
            {
                int seqA = isRoad ? HexData[a].RoadPaintSeq : HexData[a].RiverPaintSeq;
                int seqB = isRoad ? HexData[b].RoadPaintSeq : HexData[b].RiverPaintSeq;
                return seqA != seqB ? seqA.CompareTo(seqB) : a.CompareTo(b);
            });

            return hexes;
        }

        /// <summary>
        /// True if connecting <paramref name="hexIndex"/> to <paramref name="nbrIndex"/> would
        /// close a triangle, i.e. they already share a connected common neighbour. Used to drop
        /// exactly one chord per triangle; processing in paint order decides which one.
        /// </summary>
        private bool WouldCloseTriangle(int hexIndex, int nbrIndex, ushort dir, bool isRoad)
        {
            var hex = HexData[hexIndex];

            // The two hexes that share the hex<->nbr edge sit at dir-1 and dir+1 from hex.
            var flankDirs = new ushort[] { (ushort)((dir + 5) % 6), (ushort)((dir + 1) % 6) };
            foreach (var commonDir in flankDirs)
            {
                int commonIndex = GetNeighbourIndex(hex, commonDir);
                if (commonIndex == -1)
                {
                    continue;
                }

                var common = HexData[commonIndex];
                var commonMask = isRoad ? common.RoadEdgeMask : common.RiverEdgeMask;
                var hexMask = isRoad ? hex.RoadEdgeMask : hex.RiverEdgeMask;

                // hex must already be connected to the common hex...
                if ((hexMask & (1 << commonDir)) == 0)
                {
                    continue;
                }

                // ...and the common hex already connected to nbr - that closes the triangle.
                for (ushort d = 0; d < HexGridUtility.NEIGHBOURS_COUNT; ++d)
                {
                    if ((commonMask & (1 << d)) == 0)
                    {
                        continue;
                    }
                    if (GetNeighbourIndex(common, d) == nbrIndex)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void CalculateRegionEdgeMasks()
        {
            for (int hexIndex = 0; hexIndex < Capacity; ++hexIndex)
            {
                var hex = HexData[hexIndex];
                hex.RegionEdgeMask = 0; //Zero the mask first

                if (hex.IsBorder == false)
                    continue;

                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                        continue;

                    var nbr = HexData[nbrIndex];
                    if (nbr.IsBorder && hex.RegionId != nbr.RegionId)
                    {
                        hex.RegionEdgeMask |= (byte)(1 << dir);
                    }
                }
            }

            isDirty = true;
        }

        public void CalculateTradeRouteEdgeMasks()
        {
            for (int hexIndex = 0; hexIndex < Capacity; ++hexIndex)
            {
                var hex = HexData[hexIndex];
                hex.TradeRouteMask = 0; ; //Zero the mask first

                if (hex.IsTradeRoute == false)
                    continue;

                byte mask = 0;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                        continue;

                    var nbr = HexData[nbrIndex];
                    if (nbr.IsTradeRoute)
                        mask |= (byte)(1 << dir);
                }

                hex.TradeRouteMask = mask;
            }

            isDirty = true;
        }

        #endregion

        #region Edge mask refresh (preserve authored masks unless edited)

        public void EnsureRoadEdgeMasks()
        {
            if (RoadMasksNeedRecalculation)
            {
                CalculateRoadEdgeMasks();
                RoadMasksNeedRecalculation = false;
                RoadDirtyHexes.Clear();
            }
            else if (RoadDirtyHexes.Count > 0)
            {
                UpdateConnectivityMasksIncremental(RoadDirtyHexes, isRoad: true);
                RoadDirtyHexes.Clear();
            }
        }

        public void EnsureRiverEdgeMasks()
        {
            if (RiverMasksNeedRecalculation)
            {
                CalculateRiverEdgeMasks();
                RiverMasksNeedRecalculation = false;
                RiverDirtyHexes.Clear();
            }
            else if (RiverDirtyHexes.Count > 0)
            {
                UpdateConnectivityMasksIncremental(RiverDirtyHexes, isRoad: false);
                RiverDirtyHexes.Clear();
            }
        }

        public void EnsureRegionEdgeMasks()
        {
            if (RegionMasksNeedRecalculation)
            {
                CalculateRegionEdgeMasks();
                RegionMasksNeedRecalculation = false;
                RegionDirtyHexes.Clear();
            }
            else if (RegionDirtyHexes.Count > 0)
            {
                UpdateRegionMasksIncremental(RegionDirtyHexes);
                RegionDirtyHexes.Clear();
            }
        }

        public void EnsureTradeRouteEdgeMasks()
        {
            if (TradeMasksNeedRecalculation)
            {
                CalculateTradeRouteEdgeMasks();
                TradeMasksNeedRecalculation = false;
                TradeDirtyHexes.Clear();
            }
            else if (TradeDirtyHexes.Count > 0)
            {
                UpdateTradeRouteMasksIncremental(TradeDirtyHexes);
                TradeDirtyHexes.Clear();
            }
        }

        private HashSet<int> CollectAffected(IEnumerable<int> dirtyHexes)
        {
            // The masks that can change are those of the edited hexes plus their neighbours.
            var affected = new HashSet<int>();
            foreach (var h in dirtyHexes)
            {
                if (h < 0 || h >= Capacity)
                {
                    continue;
                }

                affected.Add(h);
                var hex = HexData[h];
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex != -1)
                    {
                        affected.Add(nbrIndex);
                    }
                }
            }
            return affected;
        }

        /// <summary>
        /// Rebuilds road/river masks for the edited hexes and their neighbours only, leaving
        /// authored masks elsewhere untouched. Triangle resolution still follows paint order.
        /// </summary>
        private void UpdateConnectivityMasksIncremental(IEnumerable<int> dirtyHexes, bool isRoad)
        {
            // The road network does not change during the pass, so deadend answers can be cached.
            _townRoadDeadendCache = isRoad ? new Dictionary<int, bool>() : null;
            try
            {
                UpdateConnectivityMasksIncrementalCore(dirtyHexes, isRoad);
            }
            finally
            {
                _townRoadDeadendCache = null;
            }
        }

        private void UpdateConnectivityMasksIncrementalCore(IEnumerable<int> dirtyHexes, bool isRoad)
        {
            var affected = CollectAffected(dirtyHexes);

            // 1. Clear the affected hexes' masks and the reciprocal bits pointing back at them
            //    (so edges removed by an erase, including on the region boundary, disappear).
            foreach (var h in affected)
            {
                var hex = HexData[h];
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    var invBit = (byte)(1 << HexGridUtility.InverseDir(dir));
                    if (isRoad)
                    {
                        HexData[nbrIndex].RoadEdgeMask &= (byte)~invBit;
                    }
                    else
                    {
                        HexData[nbrIndex].RiverEdgeMask &= (byte)~invBit;
                    }
                }

                if (isRoad)
                {
                    hex.RoadEdgeMask = 0;
                }
                else
                {
                    hex.RiverEdgeMask = 0;
                }
            }

            // 2. Re-derive the affected member hexes in paint order.
            var order = new List<int>();
            foreach (var h in affected)
            {
                if (isRoad ? HexData[h].IsRoad : HexData[h].IsRiver)
                {
                    order.Add(h);
                }
            }
            order.Sort((a, b) =>
            {
                int seqA = isRoad ? HexData[a].RoadPaintSeq : HexData[a].RiverPaintSeq;
                int seqB = isRoad ? HexData[b].RoadPaintSeq : HexData[b].RiverPaintSeq;
                return seqA != seqB ? seqA.CompareTo(seqB) : a.CompareTo(b);
            });

            foreach (var hexIndex in order)
            {
                var hex = HexData[hexIndex];
                var bordersBridge = false;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    int nbrIndex = GetNeighbourIndex(hex, dir);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    var nbr = HexData[nbrIndex];
                    if (isRoad ? nbr.IsRoad : nbr.IsRiver)
                    {
                        if (isRoad && UsesDisconnectedTownInteriorRoads && hex.TownSlotIndex == Hex.MAIN_SLOT_INDEX && nbr.TownSlotIndex == Hex.MAIN_SLOT_INDEX)
                        {
                            if (IsTownRoadDeadend(hexIndex) == false)
                            {
                                continue;
                            }
                        }

                        if (WouldCloseTriangle(hexIndex, nbrIndex, dir, isRoad))
                        {
                            continue;
                        }

                        if (isRoad)
                        {
                            hex.RoadEdgeMask |= (byte)(1 << dir);
                            nbr.RoadEdgeMask |= (byte)(1 << HexGridUtility.InverseDir(dir));
                        }
                        else
                        {
                            hex.RiverEdgeMask |= (byte)(1 << dir);
                            nbr.RiverEdgeMask |= (byte)(1 << HexGridUtility.InverseDir(dir));
                        }
                    }
                    else
                    if (isRoad && nbr.IsBridge)
                    {
                        bordersBridge = true;
                    }
                }

                if (isRoad && bordersBridge && HexGridUtility.GetNumBitsSetInEdgeMask(hex.RoadEdgeMask) == 1)
                {
                    var dir = (ushort)HexGridUtility.GetDirFromEdgeMask(hex.RoadEdgeMask);
                    var invDir = HexGridUtility.InverseDir(dir);
                    hex.RoadEdgeMask |= (byte)(1 << invDir);
                }
            }

            isDirty = true;
        }

        /// <summary>
        /// Region edge masks are independent per hex (a border edge towards a different region),
        /// so each affected hex is simply re-derived from its current neighbours.
        /// </summary>
        private void UpdateRegionMasksIncremental(IEnumerable<int> dirtyHexes)
        {
            foreach (var h in CollectAffected(dirtyHexes))
            {
                var hex = HexData[h];
                byte mask = 0;
                if (hex.IsBorder)
                {
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        int nbrIndex = GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1)
                        {
                            continue;
                        }

                        var nbr = HexData[nbrIndex];
                        if (nbr.IsBorder && hex.RegionId != nbr.RegionId)
                        {
                            mask |= (byte)(1 << dir);
                        }
                    }
                }
                hex.RegionEdgeMask = mask;
            }

            isDirty = true;
        }

        /// <summary>
        /// Trade-route masks are independent per hex (an edge towards an adjacent trade route),
        /// so each affected hex is simply re-derived from its current neighbours.
        /// </summary>
        private void UpdateTradeRouteMasksIncremental(IEnumerable<int> dirtyHexes)
        {
            foreach (var h in CollectAffected(dirtyHexes))
            {
                var hex = HexData[h];
                byte mask = 0;
                if (hex.IsTradeRoute)
                {
                    for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                    {
                        int nbrIndex = GetNeighbourIndex(hex, dir);
                        if (nbrIndex == -1)
                        {
                            continue;
                        }

                        if (HexData[nbrIndex].IsTradeRoute)
                        {
                            mask |= (byte)(1 << dir);
                        }
                    }
                }
                hex.TradeRouteMask = mask;
            }

            isDirty = true;
        }

        #endregion

        #region Static methods

        private static Hex[] CreateEmptyHexData(uint width, uint height)
        {
            var hexData = new Hex[width * height];

            for (int row = 0; row < height; ++row)
            {
                for (int col = 0; col < width; ++col)
                {
                    var index = (int)(row * width + col);
                    var hex = new Hex(col, row, index);
                    hexData[index] = hex;
                }
            }

            return hexData;
        }

        public static MapHexFile CreateMapHex(string filePath, string fileName, string gameName, string campaignMapName, uint width, uint height, int version)
        {
            var file                    = new MapHexFile();

            ResetMaskEditState();

            var mapBinPath              = $"{filePath}/{fileName}.bin";
            var mapHexPath              = $"{filePath}/{fileName}.hex";

            using (var bw = new BinaryWriter(new FileStream(mapBinPath, FileMode.Create, FileAccess.Write)))
            {
                file.LandRegions        = new List<string>();
                file.SeaRegions         = new List<string>();
                file.LandGroundTypes    = new List<string>();
                file.SeaGroundTypes     = new List<string>();
                file.Climates           = new List<string>();
                file.Attritions         = new List<string>();

                file.MajorFileVersion   = 0;
                file.MinorFileVersion   = version;
                file.GameName           = gameName;
                file.CampaignMapName    = campaignMapName;

                if (file.GameName == "phar" && file.MinorFileVersion == DYNASTIES_MINOR_VER)
                {
                    file.MinorFileVersion = FAKE_DYNASTIES_MINOR_VER;
                }

                file.WriteHeader(bw, 0, file.MinorFileVersion, file.GameName, file.CampaignMapName);

                file.WriteStringArray(bw, file.LandRegions);
                file.WriteStringArray(bw, file.SeaRegions);
                file.WriteStringArray(bw, file.LandGroundTypes);
                file.WriteStringArray(bw, file.SeaGroundTypes);
                file.WriteStringArray(bw, file.Climates);
                file.WriteStringArray(bw, file.Attritions);

                if (file.MinorFileVersion == 0x13 || file.MinorFileVersion == 0x14)
                {
                    file.AreasOfInterest = new List<string>();
                    file.WriteStringArray(bw, file.AreasOfInterest);
                }

                if (file.MinorFileVersion == 0x12 || file.MinorFileVersion == 0x14 || file.MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                {
                    file.WriteUnknownEntries(bw);
                }

                file.ColoursLand        = new ColoursContainer(GenerateLandColours());
                file.ColoursSea         = new ColoursContainer(GenerateSeaColours());

                file.WriteIntArray(bw, file.ColoursLand.Colours);
                file.WriteIntArray(bw, file.ColoursSea.Colours);
                
                file.MapWidth           = width;
                file.MapHeight          = height;
                file.Capacity           = width * height;
                file.HexData            = CreateEmptyHexData(width, height);

                file.WriteHexData(bw, file.MinorFileVersion, file.MapWidth, file.MapHeight, file.HexData);

                if (file.MinorFileVersion == 0x12 || file.MinorFileVersion == 0x14 || file.MinorFileVersion == FAKE_DYNASTIES_MINOR_VER)
                {
                    file.WriteUnknownTrailingBlocks(bw, file.MapWidth, file.MapHeight);
                }
            }

            file.AppendChecksum(mapBinPath, mapHexPath);
            file.UpdateHexTypes();

            return file;
        }

        /// <summary>
        /// One generator for all region colours. A fresh Random() per call is seeded from
        /// Environment.TickCount on .NET Framework, so calls within the same ~15 ms tick return
        /// identical colours - which made CreateNewRegion's retry loop regenerate the same
        /// rejected colour until the tick advanced.
        /// </summary>
        private static readonly Random ColourRandom = new Random();

        public static int[] GenerateLandColours(int count = BASE_LAND_COLOURS_COUNT)
        {
            var _random = ColourRandom;
            var colours = new int[count];

            for (int i = 0; i < count; ++i)
            {
                byte r = (byte)_random.Next(0, 255);
                byte g = (byte)_random.Next(0, 255);
                byte b = (byte)_random.Next(0, (r + g) / 2); //Prevents blue from being too prominant

                colours[i] = Utility.ToRgba(r, g, b);
            }

            return colours;
        }

        public static int[] GenerateSeaColours(int count = BASE_SEA_COLOURS_COUNT)
        {
            var _random = ColourRandom;
            var colours = new int[count];

            for (int i = 0; i < count; ++i)
            {
                byte b = (byte)_random.Next(128, 255);
                byte g = (byte)_random.Next(0, (int)(b * 0.9));
                byte r = (byte)_random.Next(0, (int)(b * 0.9)-g);

                colours[i] = Utility.ToRgba(r, g, b);
            }

            return colours;
        }

        #endregion

        #region Helpers
        public string GetRegionName(int regionIndex)
        {
            if (regionIndex < 0 || regionIndex >= LandRegions.Count + SeaRegions.Count)
            {
                return null;
            }

            if (regionIndex < LandRegions.Count)
            {
                return LandRegions[regionIndex];
            }
            else
            {
                return SeaRegions[regionIndex - LandRegions.Count];
            }
        }

        public string GetGroundTypeName(int groundTypeIndex)
        {
            if (groundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX)
            {
                return null;
            }

            if (groundTypeIndex < LandGroundTypes.Count)
            {
                return LandGroundTypes[groundTypeIndex];
            }
            else
            {
                return SeaGroundTypes[groundTypeIndex - LandGroundTypes.Count];
            }
        }

        public string GetAttritionName(int attritionIndex)
        {
            if (attritionIndex == Hex.INVALID_ATTRITION_INDEX)
            {
                return null;
            }

            return Attritions[attritionIndex];
        }

        public string GetClimateName(int climateIndex)
        {
            if (climateIndex == Hex.INVALID_CLIMATE_INDEX)
            {
                return null;
            }

            return Climates[climateIndex];
        }
        public string GetAreaOfInterestName(int areaOfInterestIndex)
        {
            if (areaOfInterestIndex == Hex.INVALID_AREA_OF_INT_INDEX)
            {
                return null;
            }

            return AreasOfInterest[areaOfInterestIndex];
        }

        private void CreateRegionIndexRemapTable(string[] combinedRegions)
        {
            if (RegionIndexRemapTable != null)
            {
                LoggerViewModel.Log("MapHexFile.CreateIndexRemapTable RegionIndexRemapTable is not null.", LogLevel.Warning);
            }

            RegionIndexRemapTable = new Dictionary<int, int>();

            for (int i = 0; i < combinedRegions.Length; ++i)
            {
                var region = combinedRegions[i];

                int remappedIndex = LandRegions.IndexOf(region);
                if (remappedIndex == -1)
                {
                    int seaIndex = SeaRegions.IndexOf(region);
                    if (seaIndex == -1)
                    {
                        // A -1 sea lookup plus LandRegions.Count would produce a valid-looking
                        // wrong index; map missing regions to INVALID instead.
                        LoggerViewModel.Log($"MapHexFile.CreateIndexRemapTable Region '{region}' is in neither the land nor the sea region list.", LogLevel.Warning);
                        remappedIndex = Hex.INVALID_REGION_INDEX;
                    }
                    else
                    {
                        remappedIndex = seaIndex + LandRegions.Count;
                    }
                }

                RegionIndexRemapTable[i] = remappedIndex;
            }

            LoggerViewModel.Log("MapHexFile.CreateIndexRemapTable Successfuly created a region index remap table.", LogLevel.Info);
        }

        private void SetCombinedRegionIndices()
        {
            var reverseRegionIndexRemapTable = new Dictionary<int, int>();

            foreach (var kvp in RegionIndexRemapTable)
            {
                reverseRegionIndexRemapTable[kvp.Value] = kvp.Key;
            }

            RemapRegionIndices(reverseRegionIndexRemapTable);
        }

        private void RestoreSeparateRegionIndices()
        {
            RemapRegionIndices(RegionIndexRemapTable);
        }

        /// <summary>
        /// Rewrites every hex's region index through <paramref name="remapTable"/>. A hex referencing
        /// an index the table does not cover is left unassigned rather than throwing, and reported
        /// once for the whole map.
        /// </summary>
        private void RemapRegionIndices(Dictionary<int, int> remapTable)
        {
            int unmapped = 0;

            foreach (var hex in HexData)
            {
                if (hex.RegionId == Hex.INVALID_REGION_INDEX)
                {
                    continue;
                }

                if (remapTable.TryGetValue(hex.RegionId, out int remappedIndex))
                {
                    hex.RegionId = remappedIndex;
                }
                else
                {
                    ++unmapped;
                    hex.RegionId = Hex.INVALID_REGION_INDEX;
                }
            }

            if (unmapped > 0)
            {
                LoggerViewModel.Log($"MapHexFile: {unmapped} hex(es) reference a region index outside the region list; they have been left unassigned.", LogLevel.Warning);
            }
        }

        public bool VerifyRegionIndices(Dictionary<string, int> expectedCombinedIds)
        {
            if (MinorFileVersion != FAKE_DYNASTIES_MINOR_VER || RegionIndexRemapTable == null)
            {
                return true;
            }

            var inverseRemap = new Dictionary<int, int>(RegionIndexRemapTable.Count);
            foreach (var kvp in RegionIndexRemapTable)
            {
                inverseRemap[kvp.Value] = kvp.Key;
            }

            bool isValid = true;
            int totalRegions = LandRegions.Count + SeaRegions.Count;
            for (int appIdx = 0; appIdx < totalRegions; ++appIdx)
            {
                var regionKey = GetRegionName(appIdx);
                if (!expectedCombinedIds.TryGetValue(regionKey, out int expectedIdx))
                {
                    LoggerViewModel.Log($"Region verification: '{regionKey}' not found in database.", LogLevel.Warning);
                    isValid = false;
                    continue;
                }

                if (!inverseRemap.TryGetValue(appIdx, out int hexIdx) || hexIdx != expectedIdx)
                {
                    var hexIdxStr = inverseRemap.TryGetValue(appIdx, out int h) ? (h + 1).ToString() : "N/A";
                    LoggerViewModel.Log($"Region verification: '{regionKey}' <index> mismatch — database: {expectedIdx + 1}, hex file: {hexIdxStr}.", LogLevel.Warning);
                    isValid = false;
                }
            }

            return isValid;
        }
        #endregion

        #region Hex utility

        public Hex GetHex(int hexIndex)
        {
            return HexData[hexIndex];
        }

        public int GetHexIndex(Hex hex)
        {
            return HexGridUtility.IndexFromCoords(hex.R, hex.Q, (int)MapWidth);
        }

        public Hex GetNeighbour(Hex hex, ushort direction)
        {
            int nbrIndex = GetNeighbourIndex(hex, direction);
            return nbrIndex == -1 ? null : HexData[nbrIndex];
        }

        public Hex GetNeighbour(int hexIndex, ushort direction)
        {
            return GetNeighbour(HexData[hexIndex], direction);
        }

        public int GetNeighbourIndex(int hexIndex, ushort direction)
        {
            return GetNeighbourIndex(HexData[hexIndex], direction);
        }

        /// <summary>
        /// Index of the neighbour in <paramref name="direction"/>, or -1 when it falls off the map.
        /// Allocation-free: the whole editor and every validator walks neighbours through here.
        /// </summary>
        public int GetNeighbourIndex(Hex hex, ushort direction)
        {
            return HexGridUtility.GetNeighbourIndexFast(hex, direction, (int)MapWidth, (int)MapHeight);
        }

        #endregion

        #region Write helpers

        private void WriteAsciiString(BinaryWriter bw, string str)
        {
            bw.Write(str.Length);
            bw.Write(Encoding.ASCII.GetBytes(str));
        }

        private void WriteHeader(BinaryWriter bw, int majorVersion, int minorVersion, string gameName, string campaignMapName)
        {
            if (minorVersion == FAKE_DYNASTIES_MINOR_VER)
            {
                minorVersion = DYNASTIES_MINOR_VER;
            }

            bw.Write(majorVersion);
            bw.Write(minorVersion);

            if (minorVersion >= 0x12)
            {
                var timestamp = (ulong)DateTime.Now.Subtract(UNIX_BASE).TotalSeconds;
                bw.Write(timestamp);
            }

            this.WriteAsciiString(bw, gameName);
            this.WriteAsciiString(bw, campaignMapName);
        }

        private void WriteStringArray(BinaryWriter bw, List<string> array)
        {
            bw.Write(array.Count);
            foreach (var item in array)
            {
                bw.Write(item.Length);
                bw.Write(Encoding.ASCII.GetBytes(item));
            }
        }

        private void RebuildRegionIndexRemapTable(IReadOnlyList<string> combinedRegionOrder)
        {
            // Build a name→appIndex lookup for the current land+sea lists
            int totalCount   = LandRegions.Count + SeaRegions.Count;
            var appIdxByName = new Dictionary<string, int>(totalCount);

            for (int i = 0; i < LandRegions.Count; i++)
            {
                appIdxByName[LandRegions[i]] = i;
            }

            for (int i = 0; i < SeaRegions.Count; i++)
            {
                appIdxByName[SeaRegions[i]] = LandRegions.Count + i;
            }

            var newTable        = new Dictionary<int, int>(totalCount);
            var assignedAppIdxs = new HashSet<int>();
            int nextCombIdx     = 0;

            // Walk the canonical DB order. Regions removed from the map are skipped.
            if (combinedRegionOrder != null)
            {
                foreach (var name in combinedRegionOrder)
                {
                    if (appIdxByName.TryGetValue(name, out int appIdx))
                    {
                        newTable[nextCombIdx++] = appIdx;
                        assignedAppIdxs.Add(appIdx);
                    }
                }
            }

            // Append any regions not covered by the provided order (e.g. newly added ones)
            for (int appIdx = 0; appIdx < totalCount; appIdx++)
            {
                if (!assignedAppIdxs.Contains(appIdx))
                {
                    newTable[nextCombIdx++] = appIdx;
                }
            }

            RegionIndexRemapTable = newTable;
        }

        private void WriteCombinedStringArray(BinaryWriter bw, List<string> landArray, List<string> seaArray)
        {
            // Order is determined by RegionIndexRemapTable, which was rebuilt to preserve
            // the original on-disk order (with any new regions appended at the end).
            int totalCount    = landArray.Count + seaArray.Count;
            var combinedArray = new List<string>(totalCount);
            var typeArray     = new int[totalCount];

            for (int i = 0; i < totalCount; i++)
            {
                int appIdx     = RegionIndexRemapTable[i];
                string name    = GetRegionName(appIdx);
                combinedArray.Add(name);
                typeArray[i]   = (appIdx < landArray.Count) ? 1 : 2;
            }

            this.WriteIntArray(bw, typeArray);
            this.WriteStringArray(bw, combinedArray);
        }

        private void WriteHexData8(BinaryWriter bw, uint capacity, Hex[] hexData)
        {
            var hexDataBytes = new byte[capacity * 8];

            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                var hex                         = hexData[hexIndex];

                var regionIndex                 = Bits.Pack(hex.RegionId + 1, 13, "Region index");
                var groundTypeIndex             = Bits.Pack(hex.GroundTypeIndex + 1, 7, "Ground type index");
                var attritionIndex              = Bits.Pack(hex.AttritionIndex + 1, 5, "Attrition index");
                var climateIndex                = Bits.Pack(hex.ClimateIndex + 1, 7, "Climate index");
                
                var cliffBeachSeaBits           = hex.IsSea ? 0b00000001 : hex.IsBeach ? 0b00000010 : hex.IsCliff ? 0b00000011 : 0;
                var regionIndexLowerBits        = (byte)(regionIndex << 3);
                var regionIndexUpperBits        = (byte)(regionIndex >> 5);
                var isNogoBit                   = (byte)((hex.IsImpassable ? 1 : 0) << 3);
                var townSlotIndexBit            = (byte)(Bits.Pack(hex.TownSlotIndex + 1, 4, "Town slot index") << 4);
                var roadEdgeMask                = (byte)((hex.RoadEdgeMask) << 1);
                var isBridgeBit                 = (byte)((hex.IsBridge ? 1 : 0) << 7);
                var isTownSprawlBit             = (byte)(hex.IsTownSprawl ? 1 : 0);
                var riverEdgeMask               = (byte)(hex.RiverEdgeMask);
                var tradeRouteMaskLower         = (byte)(hex.TradeRouteMask << 6);
                var tradeRouteMaskUpper         = (byte)(hex.TradeRouteMask >> 2);
                var groundTypeLowerBits         = (byte)(groundTypeIndex << 4);
                var groundTypeUpperBits         = (byte)((groundTypeIndex >> 4) & 0b00000111);
                var attritionBits               = (byte)(attritionIndex << 3);
                var climateBits                 = (byte)(climateIndex << 1);

                hexDataBytes[hexIndex * 8 + 0]  = (byte)(regionIndexLowerBits | cliffBeachSeaBits);
                hexDataBytes[hexIndex * 8 + 1]  = regionIndexUpperBits;
                hexDataBytes[hexIndex * 8 + 2]  = (byte)(townSlotIndexBit | isNogoBit);
                hexDataBytes[hexIndex * 8 + 3]  = (byte)(isBridgeBit | roadEdgeMask | isTownSprawlBit);
                hexDataBytes[hexIndex * 8 + 4]  = (byte)(tradeRouteMaskLower | riverEdgeMask);
                hexDataBytes[hexIndex * 8 + 5]  = (byte)(groundTypeLowerBits | tradeRouteMaskUpper);
                hexDataBytes[hexIndex * 8 + 6]  = (byte)(groundTypeUpperBits | attritionBits);
                hexDataBytes[hexIndex * 8 + 7]  = climateBits;
            }

            bw.Write(hexDataBytes);
        }

        private void WriteHexData16(BinaryWriter bw, uint capacity, Hex[] hexData)
        {
            var hexDataBytes = new byte[capacity * 16];

            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                var hex                             = hexData[hexIndex];

                var regionIndex                     = Bits.Pack(hex.RegionId + 1, 13, "Region index");
                var groundTypeIndex                 = Bits.Pack(hex.GroundTypeIndex + 1, 7, "Ground type index");
                var attritionIndex                  = Bits.Pack(hex.AttritionIndex + 1, 5, "Attrition index");
                var climateIndex                    = Bits.Pack(hex.ClimateIndex + 1, 7, "Climate index");
                var areaOfInterestIndex             = hex.InterestIndex + 1;

                var cliffBeachSeaBits               = hex.IsSea ? 0b00000001 : hex.IsBeach ? 0b00000010 : hex.IsCliff ? 0b00000011 : 0;
                var regionIndexLowerBits            = (byte)(regionIndex << 3);
                var regionIndexUpperBits            = (byte)(regionIndex >> 5);
                var isNogoBit                       = (byte)((hex.IsImpassable ? 1 : 0) << 3);
                var townSlotIndexBit                = (byte)(Bits.Pack(hex.TownSlotIndex + 1, 4, "Town slot index") << 4);
                var roadEdgeMask                    = (byte)((hex.RoadEdgeMask) << 1);
                var isBridgeBit                     = (byte)((hex.IsBridge ? 1 : 0) << 7);
                var isTownSprawlBit                 = (byte)(hex.IsTownSprawl ? 1 : 0);
                var riverEdgeMask                   = (byte)(hex.RiverEdgeMask);
                var tradeRouteMaskLower             = (byte)(hex.TradeRouteMask << 6);
                var tradeRouteMaskUpper             = (byte)(hex.TradeRouteMask >> 2);
                var groundTypeLowerBits             = (byte)(groundTypeIndex << 4);
                var groundTypeUpperBits             = (byte)((groundTypeIndex >> 4) & 0b00000111);
                var attritionBits                   = (byte)(attritionIndex << 3);
                var climateBits                     = (byte)(climateIndex << 1);
                var areaOfInterestBits              = (byte)(areaOfInterestIndex << 3);
                var restrictionBits                 = (byte)(hex.RestrictionLvl);
                var regionHexEdgeMask               = (byte)(hex.RegionEdgeMask << 2);

                hexDataBytes[hexIndex * 16 + 0]     = (byte)(regionIndexLowerBits | cliffBeachSeaBits);
                hexDataBytes[hexIndex * 16 + 1]     = regionIndexUpperBits;
                hexDataBytes[hexIndex * 16 + 2]     = (byte)(townSlotIndexBit | isNogoBit);
                hexDataBytes[hexIndex * 16 + 3]     = (byte)(isBridgeBit | roadEdgeMask | isTownSprawlBit);
                hexDataBytes[hexIndex * 16 + 4]     = (byte)(tradeRouteMaskLower | riverEdgeMask);
                hexDataBytes[hexIndex * 16 + 5]     = (byte)(groundTypeLowerBits | tradeRouteMaskUpper);
                hexDataBytes[hexIndex * 16 + 6]     = (byte)(groundTypeUpperBits | attritionBits);
                hexDataBytes[hexIndex * 16 + 7]     = climateBits;
                hexDataBytes[hexIndex * 16 + 8]     = (byte)(areaOfInterestBits | restrictionBits);
                hexDataBytes[hexIndex * 16 + 9]     = 0; // Reserved
                hexDataBytes[hexIndex * 16 + 10]    = 0; // Reserved
                hexDataBytes[hexIndex * 16 + 11]    = 0; // Reserved
                hexDataBytes[hexIndex * 16 + 12]    = 0; // Reserved
                hexDataBytes[hexIndex * 16 + 13]    = 0; // Reserved
                hexDataBytes[hexIndex * 16 + 14]    = 0; // Reserved
                hexDataBytes[hexIndex * 16 + 15]    = regionHexEdgeMask;
            }

            bw.Write(hexDataBytes);
        }

        private void WriteHexData(BinaryWriter bw, int minorVersion, uint width, uint height, Hex[] hexData)
        {
            Capacity = width * height;

            bw.Write(width);
            bw.Write(height);

            if (minorVersion == 0x0D)
            {
                this.WriteHexData8(bw, Capacity, hexData);
            }
            else
            {
                this.WriteHexData16(bw, Capacity, hexData);
            }
        }

        private void WriteIntArray(BinaryWriter bw, int[] array)
        {
            bw.Write(array.Length);
            for (int i = 0; i < array.Length; ++i)
            {
                bw.Write(array[i]);
            }
        }

        #endregion

        #region Read helpers

        private void ReadHeader(BinaryReader br)
        {
            MajorFileVersion        = br.ReadInt32();
            MinorFileVersion        = br.ReadInt32();

            if (MinorFileVersion >= 0x12)
            {
                br.ReadUInt64();    // Skip timestamp
            }

            GameName                = this.ReadAsciiString(br);
            CampaignMapName         = this.ReadAsciiString(br);

            // "phar" covers both Pharaoh and Pharaoh Dynasties. Only Dynasties uses the combined
            // region layout, and it is the one written as 0x14; vanilla Pharaoh is a plain 0x12 map.
            if (GameName == "phar" && MinorFileVersion == DYNASTIES_MINOR_VER)
            {
                MinorFileVersion = FAKE_DYNASTIES_MINOR_VER;
            }
        }

        private string ReadAsciiString(BinaryReader br)
        {
            var length = br.ReadInt32();
            return Encoding.ASCII.GetString(br.ReadBytes(length));
        }

        private void ReadHexData8(BinaryReader br, uint capacity)
        {
            byte byteValueOffset1;
            byte byteValueOffset2;
            byte byteValueOffset3;
            byte byteValueOffset4;
            byte byteValueOffset5;
            byte byteValueOffset6;
            byte byteValueOffset7;
            byte byteValueOffset8;

            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                HexGridUtility.CoordsFromIndex(hexIndex, (int)MapWidth, out int row, out int col);

                HexData[hexIndex]   = new Hex(col, row, hexIndex);
                var hex             = HexData[hexIndex];

                byteValueOffset1    = br.ReadByte();
                byteValueOffset2    = br.ReadByte();
                byteValueOffset3    = br.ReadByte();
                byteValueOffset4    = br.ReadByte();
                byteValueOffset5    = br.ReadByte();
                byteValueOffset6    = br.ReadByte();
                byteValueOffset7    = br.ReadByte();
                byteValueOffset8    = br.ReadByte();

                hex.IsSea           = (byteValueOffset1 & 0b00000011) == 0b00000001;
                hex.IsBeach         = (byteValueOffset1 & 0b00000011) == 0b00000010;
                hex.IsCliff         = (byteValueOffset1 & 0b00000011) == 0b00000011;
                hex.RegionId        = ((byteValueOffset2 << 5) | (byteValueOffset1 >> 3)) - 1;
                hex.IsPassable      = (byteValueOffset3 & 0b00001000) != 0b00001000;
                hex.TownSlotIndex   = (sbyte)((byteValueOffset3 >> 4) - 1);
                hex.IsTownSprawl    = (byteValueOffset4 & 0b00000001) == 0b00000001;
                hex.IsBridge        = (byteValueOffset4 & 0b10000000) == 0b10000000;
                hex.RoadEdgeMask    = (byte)((byteValueOffset4 & 0b01111110) >> 1);
                hex.IsRoad          = hex.RoadEdgeMask > 0;
                hex.RiverEdgeMask   = (byte)(byteValueOffset5 & 0b00111111);
                hex.IsRiver         = hex.RiverEdgeMask > 0;
                hex.TradeRouteMask  = (byte)(((byteValueOffset6 & 0b00001111) << 2) | ((byteValueOffset5 & 0b11000000) >> 6));
                hex.IsTradeRoute    = hex.TradeRouteMask > 0;
                hex.GroundTypeIndex = (sbyte)((((byteValueOffset7 & 0b00000111) << 4) | (byteValueOffset6 >> 4)) - 1);
                hex.AttritionIndex  = (sbyte)((byteValueOffset7 >> 3) - 1);
                hex.ClimateIndex    = (sbyte)((byteValueOffset8 >> 1) - 1);
            }
        }

        private void ReadHexData16(BinaryReader br, uint capacity)
        {
            byte byteValueOffset1;
            byte byteValueOffset2;
            byte byteValueOffset3;
            byte byteValueOffset4;
            byte byteValueOffset5;
            byte byteValueOffset6;
            byte byteValueOffset7;
            byte byteValueOffset8;
            byte byteValueOffset9;
            byte byteValueOffset16;

            // Hoisted out of the per-hex loop - comparing the string once instead of
            // once per hex (~731k times on the largest maps).
            bool remapPharaohRegions = GameName == "phar";

            for (int hexIndex = 0; hexIndex < capacity; ++hexIndex)
            {
                HexGridUtility.CoordsFromIndex(hexIndex, (int)MapWidth, out int row, out int col);

                HexData[hexIndex]   = new Hex(col, row, hexIndex);
                var hex             = HexData[hexIndex];

                byteValueOffset1    = br.ReadByte();
                byteValueOffset2    = br.ReadByte();
                byteValueOffset3    = br.ReadByte();
                byteValueOffset4    = br.ReadByte();
                byteValueOffset5    = br.ReadByte();
                byteValueOffset6    = br.ReadByte();
                byteValueOffset7    = br.ReadByte();
                byteValueOffset8    = br.ReadByte();
                byteValueOffset9    = br.ReadByte();
                br.ReadByte();      // Reserved
                br.ReadByte();      // Reserved
                br.ReadByte();      // Reserved
                br.ReadByte();      // Reserved
                br.ReadByte();      // Reserved
                br.ReadByte();      // Reserved
                byteValueOffset16   = br.ReadByte();

                hex.IsSea           = (byteValueOffset1 & 0b00000011) == 0b00000001;
                hex.IsBeach         = (byteValueOffset1 & 0b00000011) == 0b00000010;
                hex.IsCliff         = (byteValueOffset1 & 0b00000011) == 0b00000011;
                hex.RegionId        = ((byteValueOffset2 << 5) | (byteValueOffset1 >> 3)) - 1;
                hex.IsPassable      = (byteValueOffset3 & 0b00001000) != 0b00001000;
                hex.TownSlotIndex   = (sbyte)((byteValueOffset3 >> 4) - 1);
                hex.IsTownSprawl    = (byteValueOffset4 & 0b00000001) == 0b00000001;
                hex.IsBridge        = (byteValueOffset4 & 0b10000000) == 0b10000000;
                hex.RoadEdgeMask    = (byte)((byteValueOffset4 & 0b01111110) >> 1);
                hex.IsRoad          = hex.RoadEdgeMask > 0;
                hex.RiverEdgeMask   = (byte)(byteValueOffset5 & 0b00111111);
                hex.IsRiver         = hex.RiverEdgeMask > 0;
                hex.TradeRouteMask  = (byte)(((byteValueOffset6 & 0b00001111) << 2) | ((byteValueOffset5 & 0b11000000) >> 6));
                hex.IsTradeRoute    = hex.TradeRouteMask > 0;
                hex.GroundTypeIndex = (sbyte)((((byteValueOffset7 & 0b00000111) << 4) | (byteValueOffset6 >> 4)) - 1);
                hex.AttritionIndex  = (sbyte)((byteValueOffset7 >> 3) - 1);
                hex.ClimateIndex    = (sbyte)((byteValueOffset8 >> 1) - 1);
                hex.InterestIndex   = (sbyte)((byteValueOffset9 >> 3) - 1);
                hex.RestrictionLvl  = (byte)(byteValueOffset9 & 0b00000111);
                hex.RegionEdgeMask  = (byte)(byteValueOffset16 >> 2);
                hex.IsBorder        = hex.RegionEdgeMask > 0;

                if (remapPharaohRegions && hex.RegionId >= 0)
                {
                    // A .hex referencing a region beyond the combined list must not abort the load.
                    hex.RegionId = RegionIndexRemapTable.TryGetValue(hex.RegionId, out int remappedIndex)
                                 ? remappedIndex
                                 : Hex.INVALID_REGION_INDEX;
                }
            }
        }

        private void ReadHexData(BinaryReader br, int minorVersion)
        {
            MapWidth    = br.ReadUInt32();
            MapHeight   = br.ReadUInt32();

            Capacity    = MapWidth * MapHeight;
            HexData     = new Hex[Capacity];

            if (minorVersion == 0x0D)
            {
                this.ReadHexData8(br, Capacity);
            }
            else
            {
                this.ReadHexData16(br, Capacity);
            }
        }

        private void ReadUnknownEntries(BinaryReader br)
        {
            int count       = br.ReadInt32();
            UnknownEntries  = new List<KeyValuePair<string, uint>>(count);

            for (int i = 0; i < count; ++i)
            {
                var name    = this.ReadAsciiString(br);
                var value   = br.ReadUInt32();
                UnknownEntries.Add(new KeyValuePair<string, uint>(name, value));
            }
        }

        private void ReadUnknownTrailingBlocks(BinaryReader br)
        {
            unknownTrailingBlockCount = br.ReadInt32();

            for (int i = 0; i < unknownTrailingBlockCount; ++i)
            {
                var size = br.ReadInt32();
                br.ReadBytes(size);
            }
        }

        private void WriteUnknownEntries(BinaryWriter bw)
        {
            bw.Write(UnknownEntries.Count);

            foreach (var entry in UnknownEntries)
            {
                this.WriteAsciiString(bw, entry.Key);
                bw.Write(entry.Value);
            }
        }

        private void WriteUnknownTrailingBlocks(BinaryWriter bw, uint width, uint height)
        {
            var blobSize = (int)(((width + 7) / 8) * height);

            bw.Write(unknownTrailingBlockCount);

            for (int i = 0; i < unknownTrailingBlockCount; ++i)
            {
                bw.Write(blobSize);
                bw.Write(new byte[blobSize]);
            }
        }

        private int[] ReadIntArray(BinaryReader br)
        {
            int arraySize = br.ReadInt32();
            var arrayData = new int[arraySize];

            for (int i = 0; i < arraySize; ++i)
            {
                arrayData[i] = br.ReadInt32();
            }

            return arrayData;
        }

        private string[] ReadStringArray(BinaryReader br)
        {
            int stringLen;
            int arraySize = br.ReadInt32();

            var array = new string[arraySize];
            for (int i = 0; i < arraySize; ++i)
            {
                stringLen = br.ReadInt32();
                array[i] = Encoding.ASCII.GetString(br.ReadBytes(stringLen));
            }

            return array;
        }

        private List<string> GetRegionsFromCombinedList(HexType type, int[] regionTypes, string[] combinedRegions)
        {
            var regions = new List<string>();

            for (int i = 0; i < regionTypes.Length; ++i)
            {
                if (regionTypes[i] == (int)type + 1)
                {
                    regions.Add(combinedRegions[i]);
                }
            }

            return regions;
        }

        #endregion

        public void SetDirty()
        {
            isDirty = true;
        }

        public void NotifyLayerChanged(LayerType layer)
        {
            OnLayerChanged?.Invoke(this, new LayerChangedEventArgs(layer));
        }

        public int GetColour(bool isSea, int colourIndex)
        {
            if (isSea)
            {
                if (colourIndex < 0 || colourIndex >= ColoursSea.Colours.Length)
                {
                    return ColourTable.Zero;
                }

                return ColoursSea.Colours[colourIndex];
            }
            else
            {
                if (colourIndex < 0 || colourIndex >= ColoursLand.Colours.Length)
                {
                    return ColourTable.Zero;
                }

                return ColoursLand.Colours[colourIndex];
            }
        }

        public bool IsSeaGroundType(sbyte groundTypeIndex)
        {
            if (groundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX)
            {
                return false;
            }

            return groundTypeIndex >= LandGroundTypes.Count;
        }

        /// <summary>
        /// A hex is eligible to be painted as a beach if it's land - by either its ground type or
        /// its region type, since the two aren't enforced to match - and has at least one sea
        /// neighbour, again by either definition.
        /// </summary>
        public bool IsEligibleForBeach(Hex hex)
        {
            var isLand = hex.IsLand || IsSeaGroundType(hex.GroundTypeIndex) == false;
            if (isLand == false)
            {
                return false;
            }

            for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
            {
                var nbrIndex = GetNeighbourIndex(hex, dir);
                if (nbrIndex == -1)
                {
                    continue;
                }

                var nbr = HexData[nbrIndex];
                if (nbr.IsSea || IsSeaGroundType(nbr.GroundTypeIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public void RenameMap(string newName)
        {
            CampaignMapName = newName;
            SetDirty();
        }
    }
}
