using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CAIME.Models;

namespace CAIME.Validators
{
    internal static class GroundTypesValidator
    {
        public static bool Validate(Project project)
        {
            var mapHexFile       = project.MapHexFile;
            var hexData          = mapHexFile.HexData;
            var width            = (int)mapHexFile.MapWidth;
            var height           = (int)mapHexFile.MapHeight;
            var landRegionsCount = mapHexFile.LandRegions.Count;
            var totalGroundTypes = mapHexFile.LandGroundTypes.Count + mapHexFile.SeaGroundTypes.Count;
            var failed           = 0;

            Parallel.For(0, (int)mapHexFile.Capacity, hexIndex =>
            {
                var hex = hexData[hexIndex];

                //No ground type set
                if (hex.GroundTypeIndex == Hex.INVALID_GROUND_TYPE_INDEX)
                {
                    LoggerViewModel.Log($"Ground types validation: Hex({hex.Q}, {hex.R}) has no ground type set.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Invalid ground type index (out of range)
                if (hex.GroundTypeIndex >= totalGroundTypes)
                {
                    LoggerViewModel.Log($"Ground types validation: Hex({hex.Q}, {hex.R}) has invalid ground type index {hex.GroundTypeIndex}. Valid range is 0-{totalGroundTypes - 1}.", LogLevel.Error);
                    Interlocked.Exchange(ref failed, 1);
                }

                var isSeaGround = mapHexFile.IsSeaGroundType(hex.GroundTypeIndex);

                //Sea ground type, land region
                if (isSeaGround && hex.RegionId < landRegionsCount)
                {
                    LoggerViewModel.Log($"Ground types validation: Hex({hex.Q}, {hex.R}) has a sea ground type, yet it is a land region.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Land ground type, sea region
                if (!isSeaGround && hex.RegionId >= landRegionsCount)
                {
                    LoggerViewModel.Log($"Ground types validation: Hex({hex.Q}, {hex.R}) has a land ground type, yet it is a sea region.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Ground type category contradicts the hex's own land/sea flag
                if (hex.GroundTypeIndex != Hex.INVALID_GROUND_TYPE_INDEX && isSeaGround != hex.IsSea)
                {
                    LoggerViewModel.Log($"Ground types validation: Hex({hex.Q}, {hex.R}) has a {(isSeaGround ? "sea" : "land")} ground type but the hex is flagged as {(hex.IsSea ? "sea" : "land")}.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Single neighbour pass (allocation-free) to classify land/sea neighbours
                var seaNbrCount   = 0;
                var landNbrCount  = 0;
                var inMapNbrCount = 0;
                for (ushort dir = 0; dir < HexGridUtility.NEIGHBOURS_COUNT; ++dir)
                {
                    var nbrIndex = NeighbourIndex(hex, dir, width, height);
                    if (nbrIndex == -1)
                    {
                        continue;
                    }

                    ++inMapNbrCount;
                    if (hexData[nbrIndex].IsSea)
                    {
                        ++seaNbrCount;
                    }
                    else
                    {
                        ++landNbrCount;
                    }
                }

                //Land ground type with more than 3 sea neighbours.
                //A coast (cliff/beach) hex should keep at least one non-coast land neighbour, otherwise
                //it is effectively stranded in the sea. (All Rome2/Attila/ToB maps pass this check.)
                if (!isSeaGround && seaNbrCount > 3)
                {
                    LoggerViewModel.Log($"Ground types validation: Hex({hex.Q}, {hex.R}) is land terrain bordering {seaNbrCount} sea hexes (a coast hex should keep at least one non-coast land neighbour).", LogLevel.Info);
                }

                //Lone land hex surrounded entirely by sea
                if (hex.IsLand && inMapNbrCount > 0 && landNbrCount == 0)
                {
                    LoggerViewModel.Log($"Ground types validation: Hex({hex.Q}, {hex.R}) is a lone land hex surrounded entirely by sea.", LogLevel.Warning);
                    Interlocked.Exchange(ref failed, 1);
                }

                //Lone sea hex surrounded entirely by land (single-hex lake; usually a mistake, but can be intentional)
                if (hex.IsSea && inMapNbrCount > 0 && seaNbrCount == 0)
                {
                    LoggerViewModel.Log($"Ground types validation: Hex({hex.Q}, {hex.R}) is a lone sea hex surrounded entirely by land.", LogLevel.Info);
                }
            });

            //Ground type database mismatch check
            if (project.Database != null && project.Database.CachedGroundTypes.Count > 0)
            {
                if (!MatchesDatabase(mapHexFile.LandGroundTypes, mapHexFile.SeaGroundTypes, project.Database.CachedGroundTypes))
                {
                    Interlocked.Exchange(ref failed, 1);
                }
            }

            return failed == 0;
        }

        /// <summary>
        /// Allocation-free neighbour index lookup. Mirrors <see cref="MapHexFile.GetNeighbour"/> but avoids
        /// allocating a Hex per call, which otherwise dominates the per-hex parallel loop on large maps.
        /// </summary>
        private static int NeighbourIndex(Hex hex, ushort dir, int width, int height)
        {
            var offset = Hex.Directions_FlatTop[hex.Q & 1, dir];
            int q = hex.Q + offset.Q;
            int r = hex.R + offset.R;

            if (q < 0 || r < 0 || q >= width || r >= height)
            {
                return -1;
            }

            return r * width + q;
        }

        internal static bool MatchesDatabase(IReadOnlyList<string> landGroundTypes, IReadOnlyList<string> seaGroundTypes, IReadOnlyList<DBGroundType> databaseGroundTypes)
        {
            var isSeaByKey = new Dictionary<string, bool>();
            foreach (var groundType in databaseGroundTypes)
            {
                if (groundType.Key != null && !isSeaByKey.ContainsKey(groundType.Key))
                {
                    isSeaByKey.Add(groundType.Key, groundType.IsSea);
                }
            }

            var landMatches = CategoryMatchesDatabase(landGroundTypes, isSea: false, isSeaByKey);
            var seaMatches  = CategoryMatchesDatabase(seaGroundTypes, isSea: true, isSeaByKey);

            return landMatches && seaMatches;
        }

        private static bool CategoryMatchesDatabase(IReadOnlyList<string> mapGroundTypes, bool isSea, IReadOnlyDictionary<string, bool> isSeaByKey)
        {
            var matches  = true;
            var category = isSea ? "sea" : "land";

            foreach (var groundType in mapGroundTypes)
            {
                if (!isSeaByKey.TryGetValue(groundType, out var isSeaInDatabase))
                {
                    LoggerViewModel.Log($"Ground types validation: The map's {category} ground type '{groundType}' is not in the database.", LogLevel.Error);
                    matches = false;
                }
                else if (isSeaInDatabase != isSea)
                {
                    LoggerViewModel.Log($"Ground types validation: Ground type '{groundType}' is a {category} ground type in the map but has is_sea={isSeaInDatabase} in the database.", LogLevel.Error);
                    matches = false;
                }
            }

            return matches;
        }
    }
}
