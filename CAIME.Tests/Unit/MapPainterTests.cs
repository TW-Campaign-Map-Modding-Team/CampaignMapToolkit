using System.Linq;
using CAIME;
using CAIME.Painters;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class MapPainterTests
    {
        private const uint WIDTH  = 5;
        private const uint HEIGHT = 5;

        [TestInitialize]
        public void Setup() => MapHexHarness.ResetStaticMaskState();

        [TestCleanup]
        public void Cleanup() => MapHexHarness.ResetStaticMaskState();

        [TestMethod]
        public void BrushArea_SizeOne_IsJustTheCentre()
        {
            var map  = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var area = HexShapes.BrushArea(map.HexData[12], 1, (int)WIDTH, (int)HEIGHT);

            CollectionAssert.AreEqual(new[] { 12 }, area.ToArray());
        }

        [TestMethod]
        public void BrushArea_SizeTwo_AddsTheSixNeighbours()
        {
            var map  = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var area = HexShapes.BrushArea(map.HexData[12], 2, (int)WIDTH, (int)HEIGHT);

            var expected = Enumerable.Range(0, HexGridUtility.NEIGHBOURS_COUNT)
                                     .Select(dir => map.GetNeighbourIndex(map.HexData[12], (ushort)dir))
                                     .Concat(new[] { 12 });

            CollectionAssert.AreEquivalent(expected.ToArray(), area.ToArray());
        }

        [TestMethod]
        public void BrushArea_AtACorner_IsClippedToTheMap()
        {
            var map  = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var area = HexShapes.BrushArea(map.HexData[0], 3, (int)WIDTH, (int)HEIGHT);

            Assert.IsTrue(area.All(index => index >= 0 && index < map.Capacity), "Every hex must be on the map.");
            Assert.IsTrue(area.Count < 19, "A size 3 brush at a corner must lose the hexes off the map.");
            CollectionAssert.Contains(area, 0);
        }

        [TestMethod]
        public void PaintBrush_AppliesTheSwatchToTheWholeAreaAndMarksTheMapDirty()
        {
            var map     = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var painter = new MapPainter(map);

            var painted = painter.PaintBrush(map.HexData[12], 2, new GroundSwatch("forest", 1, 0));

            Assert.AreEqual(7, painted.Count);
            Assert.IsTrue(painted.All(index => map.HexData[index].GroundTypeIndex == 1));
            Assert.AreEqual(Hex.INVALID_GROUND_TYPE_INDEX, map.HexData[0].GroundTypeIndex, "Hexes outside the brush stay untouched.");
            Assert.IsTrue((bool)MapHexHarness.GetPrivateField(map, "isDirty"));
        }

        [TestMethod]
        public void PaintBrush_WithABeach_SkipsHexesThatAreNotCoastalLand()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            map.HexData[5].IsSea = true;

            var painted = new MapPainter(map).PaintBrush(map.HexData[0], 1, new BeachSwatch(true));
            var inland  = new MapPainter(map).PaintBrush(map.HexData[22], 1, new BeachSwatch(true));

            CollectionAssert.AreEqual(new[] { 0 }, painted.ToArray());
            Assert.AreEqual(0, inland.Count);
            Assert.IsFalse(map.HexData[22].IsBeach);
        }

        [TestMethod]
        public void PaintLine_PaintsBothEndsAndEveryHexBetween()
        {
            var map     = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            var painted = new MapPainter(map).PaintLine(map.HexData[0], map.HexData[20], new RoadSwatch(true));

            CollectionAssert.AreEquivalent(new[] { 0, 5, 10, 15, 20 }, painted.ToArray());
            Assert.IsTrue(painted.All(index => map.HexData[index].IsRoad));
        }

        [TestMethod]
        public void PaintLine_PaintsTheEndsFirst_AsTheLineToolDoes()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);

            new MapPainter(map).PaintLine(map.HexData[0], map.HexData[20], new RoadSwatch(true));

            Assert.AreEqual(1, map.HexData[0].RoadPaintSeq,  "Start");
            Assert.AreEqual(2, map.HexData[20].RoadPaintSeq, "Finish");
            Assert.AreEqual(3, map.HexData[5].RoadPaintSeq,  "First hex in between");
        }

        [TestMethod]
        public void FloodFill_SpreadsOnlyThroughHexesWithTheSameSourceValue()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT, hex => hex.RegionId = hex.Q < 2 ? 0 : 1);
            HexShapes.TryGetFloodFillMatch(LayerType.Regions, out var sameRegion);

            var painted = new MapPainter(map).FloodFill(map.HexData[0], new ClimateSwatch("temperate", 0, 0), sameRegion);

            var westOfTheBoundary = map.HexData.Where(hex => hex.Q < 2).Select(hex => hex.Index);
            CollectionAssert.AreEquivalent(westOfTheBoundary.ToArray(), painted.ToArray());
            Assert.IsTrue(map.HexData.Where(hex => hex.Q >= 2).All(hex => hex.ClimateIndex == Hex.INVALID_CLIMATE_INDEX));
        }

        [TestMethod]
        public void TryGetFloodFillMatch_IsOnlyAvailableForLayersTheFloodFillToolAccepts()
        {
            var accepted = CliNames.AllLayers.Where(layer => HexShapes.TryGetFloodFillMatch(layer, out _)).ToArray();

            CollectionAssert.AreEquivalent(
                new[]
                {
                    LayerType.GroundTypes, LayerType.Attritions, LayerType.Climates, LayerType.Regions,
                    LayerType.Impassable, LayerType.Roads, LayerType.TradeRoutes, LayerType.Restrictions,
                },
                accepted);
        }
    }
}
