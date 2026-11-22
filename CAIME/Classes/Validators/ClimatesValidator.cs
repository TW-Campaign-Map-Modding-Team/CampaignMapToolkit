using System;
using System.Collections.Generic;
using System.Linq;
using CAIME.Models;

namespace CAIME.Validators
{
    internal static class ClimatesValidator
    {
        public static bool Validate(Project project)
        {
            var isSuccess = true;
            var mapHexFile = project.MapHexFile;
            var climatesCount = mapHexFile.Climates.Count;

            if (!MatchesDatabase(mapHexFile.Climates, project.Database.CachedClimates))
            {
                isSuccess = false;
            }

            //Invalid climate index (out of range)
            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                var hex = mapHexFile.HexData[hexIndex];
                if (hex.ClimateIndex == Hex.INVALID_CLIMATE_INDEX)
                {
                    LoggerViewModel.Log($"Climates validation: Hex({hex.Q}, {hex.R}) has no climate set.", LogLevel.Warning);
                    isSuccess = false;
                }
                else if (hex.ClimateIndex >= climatesCount)
                {
                    LoggerViewModel.Log($"Climates validation: Hex({hex.Q}, {hex.R}) has invalid climate index {hex.ClimateIndex}. Valid range is 0-{climatesCount - 1}.", LogLevel.Error);
                    isSuccess = false;
                }
            }

            return isSuccess;
        }

        private static bool MatchesDatabase(IReadOnlyList<string> mapClimates, IReadOnlyList<DBClimate> databaseClimates)
        {
            var databaseKeys = new HashSet<string>(databaseClimates.Select(climate => climate.Key));
            var matches      = true;

            foreach (var climate in mapClimates)
            {
                if (!databaseKeys.Contains(climate))
                {
                    LoggerViewModel.Log($"Climates validation: The map's climate '{climate}' is not in the database.", LogLevel.Error);
                    matches = false;
                }
            }

            return matches;
        }
    }
}
