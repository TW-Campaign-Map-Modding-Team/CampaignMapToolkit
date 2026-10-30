using System.Collections.Generic;

namespace CAIME.Rpfm
{
    /// <summary>
    /// Single source of truth for which database tables a given game requires: the RPFM workflow
    /// reads these, and <see cref="DatabaseViewModel"/> loads them.
    /// </summary>
    public static class DatabaseTableProvider
    {
        // Tables every supported game needs.
        private static readonly string[] CommonTables =
        {
            Constants.TABLE_CAMPAIGN_GROUND_TYPES,
            Constants.TABLE_CAMPAIGN_MAP_ATTRITIONS,
            // Unlike every other table here, its primary key ("index") is an autonumber assigned once
            // when a row is created - not a natural key - but that value is preserved verbatim through
            // export, so a mod pack's row still matches the game's row for the same map and the
            // primary-key merge in DatabaseTableConverter still applies correctly.
            Constants.TABLE_CAMPAIGN_MAP_PLAYABLE_AREAS,
            Constants.TABLE_CAMPAIGN_MAP_REGIONS,
            Constants.TABLE_CAMPAIGN_MAP_ROADS,
            Constants.TABLE_CAMPAIGNS,
            Constants.TABLE_CLIMATES,
            Constants.TABLE_REGIONS,
            Constants.TABLE_REGIONS_TO_PROVINCES,
        };

        /// <summary>
        /// The database tables required to load a project for <paramref name="game"/>.
        /// Mirrors exactly the set loaded by <see cref="DatabaseViewModel"/>.
        /// </summary>
        public static IReadOnlyList<string> GetRequiredTables(GameTemplate game)
        {
            var tables = new List<string>(CommonTables);

            if (game == GameTemplate.Three_Kingdoms || game == GameTemplate.Warhammer3)
            {
                tables.Add(Constants.TABLE_CAMPAIGN_MAP_AREAS_OF_INTEREST);
            }

            return tables;
        }
    }
}
