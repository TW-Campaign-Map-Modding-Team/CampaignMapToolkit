using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using CAIME;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Unit tests for <see cref="BaselineTilemapExporter"/>'s bitmap stage - the part that lays the
    /// hex grid's tile indices out as the pixels of the tilemap image.
    ///
    /// <para>
    /// The public entry point opens a save dialog, so these drive the private static stage directly.
    /// </para>
    /// </summary>
    [TestClass]
    public class BaselineTilemapExporterTests
    {
        private static readonly Type ExporterType = typeof(BaselineTilemapExporter);

        private const byte River                = 2;
        private const byte Cliff                = 3;
        private const byte Beach                = 4;
        private const byte Land                 = 5;
        private const byte Sea                  = 6;
        private const byte RiverMouth           = 8;
        private const byte CliffEnd             = 9;
        private const byte RiverSource          = 10;
        private const byte MountainsCold        = 11;
        private const byte MountainsTemperate   = 12;
        private const byte MountainsSubtropical = 13;

        private const sbyte GrasslandGroundType = 0;
        private const sbyte MountainGroundType  = 1;
        private const sbyte AridClimate         = 0;
        private const sbyte ColdClimate         = 1;
        private const sbyte TemperateClimate    = 2;
        private const sbyte SubtropicalClimate  = 3;

        // Hex row 0 is the bottom of the map while an image's row 0 is the top, so the northern row
        // must come out on top. Odd columns sit half a hex north of even ones (their up-right and
        // down-right neighbours are rows R+1 and R), so their stagger must point up in the image;
        // flipping before doubling instead of after would point it down.
        [TestMethod]
        public void CreateBitmap_PutsNorthAtTheTopWithOddColumnsStaggeredNorth()
        {
            var hexGrid = new byte[]
            {
                1, 2,   // row 0, south
                3, 4,   // row 1, north
            };

            using (var bitmap = CreateBitmap(2, 2, hexGrid, new MapHexFile()))
            {
                CollectionAssert.AreEqual(
                    new byte[]
                    {
                        3, 3, 4, 4,
                        3, 3, 4, 4,
                        3, 3, 2, 2,
                        1, 1, 2, 2,
                        1, 1, 2, 2,
                    },
                    ReadPaletteIndices(bitmap),
                    "The tilemap must have north at the top with odd columns half a hex higher.");
            }
        }

        // 3K's map.hex already flags every land hex touching the sea as a beach or a cliff, so the
        // flag is trusted rather than re-derived from a sea neighbour as it is for other games.
        [TestMethod]
        public void CacheColours_ThreeKingdoms_TakesCliffsFromTheCliffFlag()
        {
            var map = ThreeKingdomsRow(
                hex => hex.IsSea = true,
                hex => { },                     // touches the sea but is not flagged
                hex => hex.IsCliff = true);     // flagged but does not touch the sea

            CollectionAssert.AreEqual(new[] { Sea, Land, Cliff }, TileIndices(map));
        }

        [TestMethod]
        public void CacheColours_ThreeKingdoms_EndsCliffsBesideABeach()
        {
            var map = ThreeKingdomsRow(
                hex => hex.IsBeach = true,
                hex => hex.IsCliff = true,
                hex => hex.IsCliff = true);

            CollectionAssert.AreEqual(new[] { Beach, CliffEnd, Cliff }, TileIndices(map));
        }

        // A 3K river mouth is the coast hex beside the sea and can itself be flagged a cliff, so it
        // is found by its sea neighbour, ahead of the cliff rule, rather than by being a beach.
        [TestMethod]
        public void CacheColours_ThreeKingdoms_FindsRiverMouthsByTheirSeaNeighbour()
        {
            var map = ThreeKingdomsRow(
                hex => hex.IsSea = true,
                hex => { hex.IsRiver = true; hex.IsCliff = true; },
                hex => hex.IsRiver = true,
                hex => hex.IsRiver = true,
                hex => { });

            CollectionAssert.AreEqual(new[] { Sea, RiverMouth, River, RiverSource, Land }, TileIndices(map));
        }

        [TestMethod]
        public void CacheColours_ThreeKingdoms_PaintsMountainsByClimate()
        {
            var map = ThreeKingdomsRow(
                Mountain(ColdClimate),
                Mountain(TemperateClimate),
                Mountain(SubtropicalClimate),
                Mountain(AridClimate),
                Mountain(Hex.INVALID_CLIMATE_INDEX),
                hex => { hex.GroundTypeIndex = GrasslandGroundType; hex.ClimateIndex = ColdClimate; });

            CollectionAssert.AreEqual(
                new[] { MountainsCold, MountainsTemperate, MountainsSubtropical, Land, Land, Land },
                TileIndices(map),
                "Only cold, temperate and subtropical have a mountain tile set; the rest is generic land.");
        }

        [TestMethod]
        public void CacheColours_OtherGames_PaintMountainsAsLand()
        {
            var map = ThreeKingdomsRow(Mountain(ColdClimate), Mountain(TemperateClimate));
            MapHexHarness.SetProperty(map, nameof(MapHexFile.GameName), "attila");

            CollectionAssert.AreEqual(new[] { Land, Land }, TileIndices(map));
        }

        private static Action<Hex> Mountain(sbyte climateIndex)
            => hex => { hex.GroundTypeIndex = MountainGroundType; hex.ClimateIndex = climateIndex; };

        // A single row of hexes, each the neighbour of the next.
        private static MapHexFile ThreeKingdomsRow(params Action<Hex>[] initHexes)
        {
            var map = MapHexHarness.BuildGrid((uint)initHexes.Length, 1, hex => initHexes[hex.Index](hex));

            MapHexHarness.SetProperty(map, nameof(MapHexFile.GameName),        "three_kingdoms");
            MapHexHarness.SetProperty(map, nameof(MapHexFile.LandGroundTypes), new List<string> { "grassland", "mountain" });
            MapHexHarness.SetProperty(map, nameof(MapHexFile.Climates),        new List<string> { "arid", "cold", "temperate", "subtropical" });

            return map;
        }

        private static byte[] TileIndices(MapHexFile map)
        {
            var exporter = new BaselineTilemapExporter();
            MapHexHarness.InvokePrivate(exporter, "CacheColours", map);

            return (byte[])MapHexHarness.GetPrivateField(exporter, "relevantIndices");
        }

        private static byte[] ReadPaletteIndices(Bitmap bitmap)
        {
            var bmpData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                                          ImageLockMode.ReadOnly, bitmap.PixelFormat);
            try
            {
                var indices = new byte[bitmap.Width * bitmap.Height];

                for (int row = 0; row < bitmap.Height; ++row)
                    Marshal.Copy(IntPtr.Add(bmpData.Scan0, row * bmpData.Stride), indices, row * bitmap.Width, bitmap.Width);

                return indices;
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }
        }

        private static Bitmap CreateBitmap(int hexMapWidth, int hexMapHeight, byte[] imageData, MapHexFile mapHexFile)
            => (Bitmap)Invoke("CreateBitmap", hexMapWidth, hexMapHeight, imageData, mapHexFile);

        private static object Invoke(string methodName, params object[] args)
        {
            var method = ExporterType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"BaselineTilemapExporter.{methodName} was not found.");

            return method.Invoke(null, args);
        }
    }
}
