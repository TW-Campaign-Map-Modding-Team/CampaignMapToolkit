using System.Collections.Generic;
using System.IO;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Covers <see cref="MapHexFile"/>'s file layer: creating a map, writing it out and reading it
    /// back. Every generator test in this suite starts from a loaded map.hex, so a serialisation
    /// regression surfaces there as a confusing golden-master diff rather than a targeted failure.
    /// </summary>
    [TestClass]
    public class MapHexFileRoundTripTests
    {
        private string _dir;

        [TestInitialize]
        public void Setup()
        {
            MapHexHarness.ResetStaticMaskState();
            _dir = MapHexHarness.CreateTempDir("caime_maphex_roundtrip");
        }

        [TestCleanup]
        public void Cleanup()
        {
            MapHexHarness.DeleteTempDir(_dir);
        }

        // Pharaoh Dynasties is written as 0x14 and read back as FAKE_DYNASTIES_MINOR_VER, so it is
        // covered separately below.
        private static IEnumerable<object[]> SupportedVersions => new[]
        {
            new object[] { 0x0D, "rome2"          },
            new object[] { 0x0F, "attila"         },
            new object[] { 0x12, "warhammer2"     },
            new object[] { 0x13, "three_kingdoms" },
            new object[] { 0x14, "warhammer3"     },
        };

        // CreateMapHex and Load have independent per-version branches; a mismatch in any of them
        // desynchronises the reader and corrupts everything after it.
        [TestMethod]
        [DynamicData(nameof(SupportedVersions))]
        public void CreateMapHex_ThenLoad_RoundTripsHeaderAndDimensions(int minorVersion, string gameName)
        {
            MapHexFile.CreateMapHex(_dir, "map", gameName, "test_campaign", 8, 6, minorVersion);

            var reloaded = new MapHexFile();
            Assert.IsTrue(reloaded.Load(Path.Combine(_dir, "map.hex")),
                $"Load failed for a map CreateMapHex had just written at version 0x{minorVersion:X2}.");

            Assert.AreEqual(minorVersion, reloaded.MinorFileVersion, "Minor file version");
            Assert.AreEqual(gameName,     reloaded.GameName,        "Game name");
            Assert.AreEqual("test_campaign", reloaded.CampaignMapName, "Campaign map name");
            Assert.AreEqual(8u,  reloaded.MapWidth,  "Map width");
            Assert.AreEqual(6u,  reloaded.MapHeight, "Map height");
            Assert.AreEqual(48u, reloaded.Capacity,  "Capacity");
            Assert.AreEqual(48,  reloaded.HexData.Length, "Hex data length");
        }

        // The per-hex payload is 16 bytes of bit fields sharing bytes, so an off-by-one shift
        // corrupts a neighbouring field rather than its own.
        [TestMethod]
        public void SaveThenLoad_PreservesEveryPerHexField()
        {
            var map = CreateAndLoad(0x14, "warhammer3", width: 4, height: 4);

            map.LandRegions.AddRange(new[] { "region_one", "region_two" });
            map.SeaRegions.Add("sea_one");
            map.LandGroundTypes.AddRange(new[] { "grassland", "forest" });
            map.SeaGroundTypes.Add("ocean");
            map.Climates.Add("temperate");
            map.Attritions.Add("none");
            map.AreasOfInterest.Add("capital");

            var hex = map.HexData[5];
            hex.RegionId        = 1;
            hex.GroundTypeIndex = 1;
            hex.ClimateIndex    = 0;
            hex.AttritionIndex  = 0;
            hex.InterestIndex   = 0;
            hex.TownSlotIndex   = Hex.PORT_SLOT_INDEX;
            hex.IsTownSprawl    = true;
            hex.IsPassable      = false;
            hex.IsBeach         = true;
            hex.RestrictionLvl  = 5;

            // The connectivity flags are re-derived from the masks on load, so each needs a mask.
            hex.IsRoad          = true;
            hex.RoadEdgeMask    = 0b00101010;
            hex.IsRiver         = true;
            hex.RiverEdgeMask   = 0b00010101;
            hex.IsTradeRoute    = true;
            hex.TradeRouteMask  = 0b00110011;
            hex.IsBorder        = true;
            hex.RegionEdgeMask  = 0b00001111;

            map.HexData[6].IsSea = true;

            Assert.IsTrue(map.Save(_dir, "map"), "Save returned false.");

            var reloaded = Load("map");
            var loaded   = reloaded.HexData[5];

            Assert.AreEqual(1, loaded.RegionId,        "RegionId");
            Assert.AreEqual(1, loaded.GroundTypeIndex, "GroundTypeIndex");
            Assert.AreEqual(0, loaded.ClimateIndex,    "ClimateIndex");
            Assert.AreEqual(0, loaded.AttritionIndex,  "AttritionIndex");
            Assert.AreEqual(0, loaded.InterestIndex,   "InterestIndex");
            Assert.AreEqual(Hex.PORT_SLOT_INDEX, loaded.TownSlotIndex, "TownSlotIndex");
            Assert.IsTrue (loaded.IsTownSprawl, "IsTownSprawl");
            Assert.IsFalse(loaded.IsPassable,   "IsPassable");
            Assert.IsTrue (loaded.IsBeach,      "IsBeach");
            Assert.AreEqual(5, loaded.RestrictionLvl, "RestrictionLvl");

            Assert.AreEqual(0b00101010, loaded.RoadEdgeMask,   "RoadEdgeMask");
            Assert.AreEqual(0b00010101, loaded.RiverEdgeMask,  "RiverEdgeMask");
            Assert.AreEqual(0b00110011, loaded.TradeRouteMask, "TradeRouteMask");
            Assert.AreEqual(0b00001111, loaded.RegionEdgeMask, "RegionEdgeMask");

            Assert.IsTrue(loaded.IsRoad,       "IsRoad");
            Assert.IsTrue(loaded.IsRiver,      "IsRiver");
            Assert.IsTrue(loaded.IsTradeRoute, "IsTradeRoute");
            Assert.IsTrue(loaded.IsBorder,     "IsBorder");

            Assert.IsTrue (reloaded.HexData[6].IsSea,   "Neighbouring sea hex");
            Assert.IsFalse(reloaded.HexData[6].IsBeach, "Sea and beach share one bit pair");
        }

        // ReadHexData16 derives IsRoad/IsRiver/IsTradeRoute/IsBorder from "mask > 0" - they have no
        // bit of their own. This is what makes authored-mask preservation load-bearing.
        [TestMethod]
        public void SaveThenLoad_DropsConnectivityFlagsWithoutAMask()
        {
            var map = CreateAndLoad(0x14, "warhammer3", width: 4, height: 4);

            var hex = map.HexData[5];
            hex.IsRoad       = true;
            hex.IsRiver      = true;
            hex.IsTradeRoute = true;
            hex.IsBorder     = true;

            Assert.IsTrue(map.Save(_dir, "map"), "Save returned false.");

            var loaded = Load("map").HexData[5];

            Assert.IsFalse(loaded.IsRoad,       "IsRoad without a road edge mask");
            Assert.IsFalse(loaded.IsRiver,      "IsRiver without a river edge mask");
            Assert.IsFalse(loaded.IsTradeRoute, "IsTradeRoute without a trade route mask");
            Assert.IsFalse(loaded.IsBorder,     "IsBorder without a region edge mask");
        }

        // Restriction level shares byte 8 with the Area of Interest index (AoI bits 4+, restrictions
        // bits 0-2), so only levels 0-7 survive a round trip.
        [TestMethod]
        public void SaveThenLoad_PreservesRestrictionLevelsZeroToSeven_AlongsideAreaOfInterest()
        {
            var map = CreateAndLoad(0x14, "warhammer3", width: 4, height: 4);
            map.AreasOfInterest.AddRange(new[] { "aoi_zero", "aoi_one", "aoi_two" });

            for (byte level = 0; level <= 7; ++level)
            {
                map.HexData[level].RestrictionLvl = level;
                map.HexData[level].InterestIndex  = 2;
            }

            Assert.IsTrue(map.Save(_dir, "map"), "Save returned false.");

            var reloaded = Load("map");
            for (byte level = 0; level <= 7; ++level)
            {
                Assert.AreEqual(level, reloaded.HexData[level].RestrictionLvl,
                    $"Restriction level {level} did not round-trip.");
                Assert.AreEqual(2, reloaded.HexData[level].InterestIndex,
                    $"Restriction level {level} corrupted the Area of Interest index sharing its byte.");
            }
        }

        // CA's tools store the Area of Interest index + 1 in bits 4-10 of bytes 8-9 (bit 3 is not part
        // of it), and the Assembly Kit's map_data builder reads it from there. Index 16 is the first
        // that reaches byte 9.
        [TestMethod]
        public void Save_WritesAreaOfInterestFromBitFourIntoByteNine()
        {
            var map = CreateAndLoad(0x14, "warhammer3", width: 4, height: 4);
            for (int i = 0; i < 17; ++i)
                map.AreasOfInterest.Add($"aoi_{i}");

            map.HexData[0].InterestIndex  = 16;
            map.HexData[0].RestrictionLvl = 5;

            Assert.IsTrue(map.Save(_dir, "map"), "Save returned false.");

            // The hex records are followed by one 4-byte trailing block (count, size, blob) and the CRC.
            var bytes = File.ReadAllBytes(Path.Combine(_dir, "map.hex"));
            var hex0  = bytes.Length - 4 - 12 - 16 * 16;

            Assert.AreEqual(0b0001_0101, bytes[hex0 + 8], "Byte 8: index + 1 = 17 has low bits 0001 at bits 4-7, restriction 5 at bits 0-2.");
            Assert.AreEqual(0b0000_0001, bytes[hex0 + 9], "Byte 9: bit 4 of index + 1 = 17.");

            Assert.AreEqual(16, Load("map").HexData[0].InterestIndex, "InterestIndex");
        }

        // Rome II uses the 8-byte layout, which has no Area of Interest, trade route or region edge field.
        [TestMethod]
        public void SaveThenLoad_Rome2EightByteLayout_PreservesItsSupportedFields()
        {
            var map = CreateAndLoad(0x0D, "rome2", width: 4, height: 4);

            map.LandRegions.AddRange(new[] { "region_one", "region_two" });
            map.LandGroundTypes.AddRange(new[] { "grassland", "forest" });
            map.Climates.Add("temperate");
            map.Attritions.Add("none");

            var hex = map.HexData[3];
            hex.RegionId        = 1;
            hex.GroundTypeIndex = 1;
            hex.TownSlotIndex   = Hex.MAIN_SLOT_INDEX;
            hex.IsTownSprawl    = true;
            hex.IsPassable      = false;
            hex.IsRoad          = true;
            hex.RoadEdgeMask    = 0b00001001;

            Assert.IsTrue(map.Save(_dir, "map"), "Save returned false.");

            var loaded = Load("map").HexData[3];

            Assert.AreEqual(1, loaded.RegionId,        "RegionId");
            Assert.AreEqual(1, loaded.GroundTypeIndex, "GroundTypeIndex");
            Assert.AreEqual(Hex.MAIN_SLOT_INDEX, loaded.TownSlotIndex, "TownSlotIndex");
            Assert.IsTrue (loaded.IsTownSprawl, "IsTownSprawl");
            Assert.IsFalse(loaded.IsPassable,   "IsPassable");
            Assert.AreEqual(0b00001001, loaded.RoadEdgeMask, "RoadEdgeMask");
        }

        // Dynasties is written as 0x14 but held in memory as FAKE_DYNASTIES_MINOR_VER. Getting either
        // side of that translation wrong makes the file unreadable by the game.
        [TestMethod]
        public void DynastiesMap_IsHeldAsFakeMinorVersion_ButWrittenBackAsZero14()
        {
            MapHexFile.CreateMapHex(_dir, "map", "phar", "dynasties_campaign", 4, 4, 0x14);

            var map = Load("map");
            Assert.AreEqual(MapHexFile.FAKE_DYNASTIES_MINOR_VER, map.MinorFileVersion,
                "A 'phar' map written as 0x14 must be promoted to the in-app Dynasties version.");

            Assert.IsTrue(map.Save(_dir, "resaved"), "Save returned false.");

            using (var br = new BinaryReader(File.OpenRead(Path.Combine(_dir, "resaved.hex"))))
            {
                br.ReadInt32();
                Assert.AreEqual(0x14, br.ReadInt32(),
                    "The in-app Dynasties marker leaked into the file - the game would reject it.");
            }

            Assert.AreEqual(MapHexFile.FAKE_DYNASTIES_MINOR_VER, Load("resaved").MinorFileVersion,
                "The re-saved Dynasties map did not load back as a Dynasties map.");
        }

        // Dynasties stores one combined region array on disk while the app keeps land and sea
        // separate, so every RegionId is rewritten on save and restored on load via the remap table.
        [TestMethod]
        public void DynastiesMap_RoundTripsSeparateLandAndSeaRegionIndices()
        {
            MapHexFile.CreateMapHex(_dir, "map", "phar", "dynasties_campaign", 4, 4, 0x14);

            var map = Load("map");
            map.LandRegions.AddRange(new[] { "land_one", "land_two" });
            map.SeaRegions.AddRange(new[] { "sea_one" });

            map.HexData[0].RegionId = 0;
            map.HexData[1].RegionId = 1;
            map.HexData[2].RegionId = 2;
            map.HexData[2].IsSea    = true;

            Assert.IsTrue(map.Save(_dir, "map"), "Save returned false.");

            Assert.AreEqual(0, map.HexData[0].RegionId, "Save left hex 0 holding a combined index.");
            Assert.AreEqual(1, map.HexData[1].RegionId, "Save left hex 1 holding a combined index.");
            Assert.AreEqual(2, map.HexData[2].RegionId, "Save left hex 2 holding a combined index.");

            var reloaded = Load("map");
            CollectionAssert.AreEqual(new List<string> { "land_one", "land_two" }, reloaded.LandRegions, "Land regions");
            CollectionAssert.AreEqual(new List<string> { "sea_one" },              reloaded.SeaRegions,  "Sea regions");

            Assert.AreEqual(0, reloaded.HexData[0].RegionId, "hex 0 region index");
            Assert.AreEqual(1, reloaded.HexData[1].RegionId, "hex 1 region index");
            Assert.AreEqual(2, reloaded.HexData[2].RegionId, "hex 2 region index");
        }

        [TestMethod]
        public void Load_UnsupportedMinorVersion_ReturnsFalse()
        {
            MapHexFile.CreateMapHex(_dir, "map", "warhammer3", "test_campaign", 4, 4, 0x14);

            var path  = Path.Combine(_dir, "map.hex");
            var bytes = File.ReadAllBytes(path);
            bytes[4] = 0x7E;
            File.WriteAllBytes(path, bytes);

            Assert.IsFalse(new MapHexFile().Load(path), "An unsupported file version must not load.");
        }

        [TestMethod]
        public void Load_NullOrEmptyPath_ReturnsFalseWithoutThrowing()
        {
            Assert.IsFalse(new MapHexFile().Load(null), "A null path must be rejected.");
            Assert.IsFalse(new MapHexFile().Load(string.Empty), "An empty path must be rejected.");
        }

        // Save writes a .bin first and appends the CRC into the .hex; the intermediate must not survive.
        [TestMethod]
        public void Save_LeavesNoIntermediateBinFile_AndOverwritesInPlace()
        {
            var map = CreateAndLoad(0x14, "warhammer3", width: 4, height: 4);

            Assert.IsTrue(map.Save(_dir, "map"), "First save returned false.");
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "map.bin")), "A stray .bin was left by the first save.");

            map.HexData[0].IsSea = true;
            Assert.IsTrue(map.Save(_dir, "map"), "Overwriting save returned false.");
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "map.bin")), "A stray .bin was left by the overwriting save.");

            Assert.IsTrue(Load("map").HexData[0].IsSea, "The overwriting save did not take effect.");
        }

        // Auto-saves and backups write to a side file, so the project keeps its unsaved changes.
        [TestMethod]
        public void Save_ClearsDirtyFlagOnlyWhenAsked()
        {
            var map = CreateAndLoad(0x14, "warhammer3", width: 4, height: 4);

            map.SetDirty();
            Assert.IsTrue(map.IsDirty, "SetDirty did not mark the map dirty.");

            Assert.IsTrue(map.Save(_dir, "autosave", combinedRegionOrder: null, clearDirtyFlag: false));
            Assert.IsTrue(map.IsDirty, "An auto-save must leave unsaved changes flagged.");

            Assert.IsTrue(map.Save(_dir, "map"));
            Assert.IsFalse(map.IsDirty, "A normal save must clear the dirty flag.");
        }

        // A stale Index makes every later neighbour lookup and dirty-hex record point at the wrong cell.
        [TestMethod]
        public void ResizeMapHex_GrowingMap_ShiftsOriginalContentAndRestampsCoordinates()
        {
            var map = MapHexHarness.BuildGrid(3, 3);

            for (int i = 0; i < 9; ++i)
                map.HexData[i].GroundTypeIndex = (sbyte)i;

            Assert.IsTrue(map.ResizeMapHex(5, 5, newPadRight: 1, newPadLeft: 1, newPadTop: 1, newPadBottom: 1));

            Assert.AreEqual(5u,  map.MapWidth,  "Width");
            Assert.AreEqual(5u,  map.MapHeight, "Height");
            Assert.AreEqual(25u, map.Capacity,  "Capacity");
            Assert.AreEqual(25,  map.HexData.Length, "Hex data length");

            Assert.AreEqual(0, map.HexData[1 * 5 + 1].GroundTypeIndex, "Original hex 0 did not land at (1,1).");
            Assert.AreEqual(8, map.HexData[3 * 5 + 3].GroundTypeIndex, "Original hex 8 did not land at (3,3).");

            for (int row = 0; row < 5; ++row)
            {
                for (int col = 0; col < 5; ++col)
                {
                    var hex = map.HexData[row * 5 + col];
                    Assert.AreEqual(col,           hex.Q,     $"Q at ({col},{row})");
                    Assert.AreEqual(row,           hex.R,     $"R at ({col},{row})");
                    Assert.AreEqual(row * 5 + col, hex.Index, $"Index at ({col},{row})");
                }
            }
        }

        // Padding clamps to the nearest edge hex, so one source hex feeds several target cells.
        [TestMethod]
        public void ResizeMapHex_PaddedCells_AreIndependentClonesOfTheClampedEdgeHex()
        {
            var map = MapHexHarness.BuildGrid(2, 2);
            for (int i = 0; i < 4; ++i)
                map.HexData[i].GroundTypeIndex = (sbyte)(i + 1);

            map.ResizeMapHex(4, 4, newPadRight: 1, newPadLeft: 1, newPadTop: 1, newPadBottom: 1);

            var cornerPad = map.HexData[0 * 4 + 0];
            var original  = map.HexData[1 * 4 + 1];

            Assert.AreEqual(1, cornerPad.GroundTypeIndex, "Padding did not take the clamped edge hex's value.");
            Assert.AreEqual(1, original.GroundTypeIndex,  "The original hex lost its value.");
            Assert.AreNotSame(cornerPad, original, "Padded cells must not share a Hex instance with the original.");

            cornerPad.GroundTypeIndex = 99;
            Assert.AreEqual(1, original.GroundTypeIndex,
                "Editing a padded hex changed the hex it was cloned from - the instances are aliased.");
        }

        [TestMethod]
        public void ResizeMapHex_ShrinkingMap_KeepsTheRetainedRegion()
        {
            var map = MapHexHarness.BuildGrid(4, 4);
            for (int i = 0; i < 16; ++i)
                map.HexData[i].GroundTypeIndex = (sbyte)i;

            Assert.IsTrue(map.ResizeMapHex(2, 2, newPadRight: -1, newPadLeft: 0, newPadTop: -1, newPadBottom: 0));

            Assert.AreEqual(2u, map.MapWidth,  "Width");
            Assert.AreEqual(2u, map.MapHeight, "Height");
            Assert.AreEqual(4u, map.Capacity,  "Capacity");

            Assert.AreEqual(0, map.HexData[0].GroundTypeIndex, "Retained hex (0,0)");
            Assert.AreEqual(1, map.HexData[1].GroundTypeIndex, "Retained hex (1,0)");
            Assert.AreEqual(4, map.HexData[2].GroundTypeIndex, "Retained hex (0,1)");
            Assert.AreEqual(5, map.HexData[3].GroundTypeIndex, "Retained hex (1,1)");
        }

        [TestMethod]
        public void ResizeMapHex_ThenSave_WritesTheNewDimensions()
        {
            var map = CreateAndLoad(0x14, "warhammer3", width: 4, height: 4);

            map.ResizeMapHex(6, 5, newPadRight: 2, newPadLeft: 0, newPadTop: 1, newPadBottom: 0);
            Assert.IsTrue(map.Save(_dir, "resized"), "Save returned false.");

            var reloaded = Load("resized");
            Assert.AreEqual(6u,  reloaded.MapWidth,  "Width");
            Assert.AreEqual(5u,  reloaded.MapHeight, "Height");
            Assert.AreEqual(30u, reloaded.Capacity,  "Capacity");
            Assert.AreEqual(30,  reloaded.HexData.Length, "Hex data length");
        }

        private MapHexFile CreateAndLoad(int minorVersion, string gameName, uint width, uint height)
        {
            MapHexFile.CreateMapHex(_dir, "map", gameName, "test_campaign", width, height, minorVersion);
            return Load("map");
        }

        private MapHexFile Load(string fileName)
        {
            var map = new MapHexFile();
            Assert.IsTrue(map.Load(Path.Combine(_dir, fileName + ".hex")), $"Load failed for {fileName}.hex.");
            return map;
        }
    }
}
