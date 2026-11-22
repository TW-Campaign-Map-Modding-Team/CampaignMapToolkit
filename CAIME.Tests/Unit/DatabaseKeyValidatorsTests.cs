using System.Collections.Generic;
using System.Linq;
using CAIME.Models;
using CAIME.Validators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class DatabaseKeyValidatorsTests
    {
        private static readonly string[] TddLandGroundTypes =
        {
            "brownlands", "dense_forest", "desert", "grassland", "hills", "hilly_light_forest", "jungle",
            "light_forest", "marsh", "mountain", "plains", "river", "swamp", "tundra", "wasteland",
        };

        private static readonly string[] TddSeaGroundTypes = { "sea_coast", "sea_lake", "sea_ocean" };

        private static List<DBGroundType> GroundTypes(IEnumerable<string> land, IEnumerable<string> sea)
        {
            return land.Select(key => new DBGroundType { Key = key, IsSea = false })
                .Concat(sea.Select(key => new DBGroundType { Key = key, IsSea = true }))
                .ToList();
        }

        [TestMethod]
        public void GroundTypes_DatabaseInAnotherOrder_Matches()
        {
            var vanillaThenModLand = TddLandGroundTypes.Where(key => key != "brownlands" && key != "wasteland")
                .Concat(new[] { "brownlands", "wasteland" });

            var database = GroundTypes(vanillaThenModLand, TddSeaGroundTypes);

            Assert.IsTrue(GroundTypesValidator.MatchesDatabase(TddLandGroundTypes, TddSeaGroundTypes, database));
        }

        [TestMethod]
        public void GroundTypes_DatabaseWithTypesTheMapDoesNotUse_Matches()
        {
            var database = GroundTypes(TddLandGroundTypes.Concat(new[] { "volcanic" }), TddSeaGroundTypes);

            Assert.IsTrue(GroundTypesValidator.MatchesDatabase(TddLandGroundTypes, TddSeaGroundTypes, database));
        }

        [TestMethod]
        public void GroundTypes_MapTypeMissingFromDatabase_DoesNotMatch()
        {
            var database = GroundTypes(TddLandGroundTypes.Where(key => key != "wasteland"), TddSeaGroundTypes);

            Assert.IsFalse(GroundTypesValidator.MatchesDatabase(TddLandGroundTypes, TddSeaGroundTypes, database));
        }

        [TestMethod]
        public void GroundTypes_MapTypeInTheOtherCategory_DoesNotMatch()
        {
            var database = GroundTypes(TddLandGroundTypes.Where(key => key != "river"), TddSeaGroundTypes.Concat(new[] { "river" }));

            Assert.IsFalse(GroundTypesValidator.MatchesDatabase(TddLandGroundTypes, TddSeaGroundTypes, database));
        }

        [TestMethod]
        public void Climates_DatabaseInAnotherOrderWithExtraClimates_Matches()
        {
            var map      = new[] { "ROME_Atlantic", "ROME_Med", "default" };
            var database = new[] { "default", "ROME_Desert", "ROME_Med", "ROME_Atlantic" }.Select(key => new DBClimate { Key = key }).ToList();

            Assert.IsTrue(ClimatesValidator.MatchesDatabase(map, database));
        }

        [TestMethod]
        public void Climates_MapClimateMissingFromDatabase_DoesNotMatch()
        {
            var map      = new[] { "ROME_Atlantic", "ROME_Med" };
            var database = new[] { "ROME_Atlantic" }.Select(key => new DBClimate { Key = key }).ToList();

            Assert.IsFalse(ClimatesValidator.MatchesDatabase(map, database));
        }

        [TestMethod]
        public void Attritions_DatabaseInAnotherOrderWithExtraAttritions_Matches()
        {
            var map      = new[] { "att_att_desert", "att_att_mountain" };
            var database = new[] { "att_att_snow_1", "att_att_mountain", "att_att_desert" }.Select(key => new DBAttrition { Key = key }).ToList();

            Assert.IsTrue(AttritionsValidator.MatchesDatabase(map, database));
        }

        [TestMethod]
        public void Attritions_MapAttritionMissingFromDatabase_DoesNotMatch()
        {
            var map      = new[] { "att_att_desert", "att_att_mountain" };
            var database = new[] { "att_att_desert" }.Select(key => new DBAttrition { Key = key }).ToList();

            Assert.IsFalse(AttritionsValidator.MatchesDatabase(map, database));
        }
    }
}
