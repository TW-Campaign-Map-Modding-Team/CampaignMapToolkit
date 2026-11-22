using System;
using System.Collections.Generic;
using System.Linq;
using CAIME.Models;

namespace CAIME.Validators
{
    internal static class AttritionsValidator
    {
        public static bool Validate(Project project)
        {
            var isSuccess = true;
            var mapHexFile = project.MapHexFile;
            var attritionsCount = mapHexFile.Attritions.Count;

            if (!MatchesDatabase(mapHexFile.Attritions, project.Database.CachedAttritions))
            {
                isSuccess = false;
            }

            //Invalid attrition index (out of range)
            for (int hexIndex = 0; hexIndex < mapHexFile.Capacity; ++hexIndex)
            {
                var hex = mapHexFile.HexData[hexIndex];
                if (hex.AttritionIndex != Hex.INVALID_ATTRITION_INDEX && hex.AttritionIndex >= attritionsCount)
                {
                    LoggerViewModel.Log($"Attritions validation: Hex({hex.Q}, {hex.R}) has invalid attrition index {hex.AttritionIndex}. Valid range is 0-{attritionsCount - 1}.", LogLevel.Error);
                    isSuccess = false;
                }
            }

            return isSuccess;
        }

        internal static bool MatchesDatabase(IReadOnlyList<string> mapAttritions, IReadOnlyList<DBAttrition> databaseAttritions)
        {
            var databaseKeys = new HashSet<string>(databaseAttritions.Select(attrition => attrition.Key));
            var matches      = true;

            foreach (var attrition in mapAttritions)
            {
                if (!databaseKeys.Contains(attrition))
                {
                    LoggerViewModel.Log($"Attritions validation: The map's attrition '{attrition}' is not in the database.", LogLevel.Error);
                    matches = false;
                }
            }

            return matches;
        }
    }
}
