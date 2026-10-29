using System.Linq;
using CAIME.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    /// <summary>
    /// Editing campaign masks in the editor: adding a mask, painting one mask while another may be
    /// shown, and the masks layer's place in the layer stack.
    /// </summary>
    [TestClass]
    public class CampaignMaskEditingTests
    {
        private const uint WIDTH  = 10;
        private const uint HEIGHT = 4;

        private static MapHexFile Map()
        {
            var map = MapHexHarness.BuildGrid(WIDTH, HEIGHT);
            MapHexHarness.SetProperty(map, nameof(MapHexFile.ColoursLand), new ColoursContainer(new int[0]));
            MapHexHarness.SetProperty(map, nameof(MapHexFile.ColoursSea), new ColoursContainer(new int[0]));
            return map;
        }

        private static Hex HexAt(MapHexFile map, int column, int row) => map.HexData[row * (int)map.MapWidth + column];

        [TestMethod]
        public void CreateCampaignMask_AddsAMaskThatMasksNothing_AfterTheOthers()
        {
            var map  = Map();
            var mask = new MapHexEditor(map, null).CreateCampaignMask("lost_province");

            Assert.IsNotNull(mask);
            CollectionAssert.AreEqual(new[] { "", "lost_province" }, map.CampaignMasks.Select(m => m.Name).ToArray());
            Assert.AreEqual(0, mask.Bitmap.Length, "A new mask masks no hexes.");
            Assert.IsTrue(map.IsDirty);
        }

        [TestMethod]
        public void CreateCampaignMask_RefusesAnEmptyDuplicateOrNonAsciiName()
        {
            var map    = Map();
            var editor = new MapHexEditor(map, null);
            editor.CreateCampaignMask("taken");

            Assert.IsNull(editor.CreateCampaignMask(""), "empty: the unnamed mask is already there");
            Assert.IsNull(editor.CreateCampaignMask(null), "null");
            Assert.IsNull(editor.CreateCampaignMask("taken"), "duplicate");
            Assert.IsNull(editor.CreateCampaignMask("café"), "map.hex stores the names as ASCII");
            Assert.AreEqual(2, map.CampaignMasks.Count);
        }

        [TestMethod]
        public void MaskSwatch_MasksAndUnmasksOnlyItsOwnMask()
        {
            var map   = Map();
            var mine  = new CampaignMask("mine");
            var other = new CampaignMask("other");
            map.SetCampaignMasks(new[] { new CampaignMask(""), mine, other });
            var hex   = HexAt(map, 9, 2);

            new CampaignMaskSwatch(map, mine, isMasked: true).Apply(hex);
            Assert.IsTrue(mine.IsMasked(9, 2, WIDTH));
            Assert.IsFalse(other.IsMasked(9, 2, WIDTH));
            Assert.AreEqual(CampaignMask.BitmapSize(WIDTH, HEIGHT), mine.Bitmap.Length);

            new CampaignMaskSwatch(map, mine, isMasked: false).Apply(hex);
            Assert.IsFalse(mine.IsMasked(9, 2, WIDTH));
        }

        [TestMethod]
        public void MaskSwatch_MatchesAHexWhoseBitIsItsValue()
        {
            var map    = Map();
            var mask   = new CampaignMask("m");
            var masked = HexAt(map, 3, 1);
            mask.SetMasked(3, 1, WIDTH, HEIGHT, true);

            var maskSwatch   = new CampaignMaskSwatch(map, mask, isMasked: true);
            var unmaskSwatch = new CampaignMaskSwatch(map, mask, isMasked: false);

            Assert.IsTrue(maskSwatch.Matches(masked));
            Assert.IsFalse(unmaskSwatch.Matches(masked));
            Assert.IsTrue(unmaskSwatch.Matches(HexAt(map, 4, 1)));
        }

        [TestMethod]
        public void MaskLayer_ColoursAHexByTheShownMask_EvenAfterPaintingAnother()
        {
            var map    = Map();
            var shown  = new CampaignMask("shown");
            var hidden = new CampaignMask("hidden");
            var layer  = new CampaignMaskLayer();
            layer.ShowMask(map, shown);

            var hex     = HexAt(map, 5, 0);
            var painted = new CampaignMaskSwatch(map, hidden, isMasked: true);
            painted.Apply(hex);

            Assert.AreEqual(ColourTable.Zero, layer.ColourAfterApplying(painted, hex.Index), "Painting a hidden mask leaves the shown one as it was.");

            layer.MaskSwatch.Apply(hex);
            Assert.AreEqual(ColourTable.MaskedHex, layer.ColourAfterApplying(layer.MaskSwatch, hex.Index));
        }

        [TestMethod]
        public void Eraser_UnmasksTheShownMask()
        {
            var map   = Map();
            var mask  = new CampaignMask("m");
            var layer = new CampaignMaskLayer();
            layer.ShowMask(map, mask);

            var eraser = (CampaignMaskSwatch)EraserToolCommand.ClearSwatchFor(layer);

            Assert.AreSame(mask, eraser.Mask);
            Assert.IsFalse(eraser.IsMasked);
        }

        [TestMethod]
        public void Resize_KeepsTheSameMasks_SoTheLayerStillShowsOne()
        {
            var map  = Map();
            var mask = new CampaignMask("m");
            mask.SetMasked(2, 1, WIDTH, HEIGHT, true);
            map.SetCampaignMasks(new[] { mask });

            Assert.IsTrue(map.ResizeMapHex(WIDTH + 2, HEIGHT, 1, 1, 0, 0));

            Assert.AreSame(mask, map.CampaignMasks.Single());
            Assert.IsTrue(mask.IsMasked(3, 1, WIDTH + 2));
        }

        [TestMethod]
        public void MasksLayer_SitsDirectlyAboveImpassable_InGamesWhoseMapHexStoresMasks()
        {
            foreach (var game in new[] { GameTemplate.Warhammer, GameTemplate.Warhammer2, GameTemplate.Warhammer3, GameTemplate.Troy, GameTemplate.Pharaoh, GameTemplate.Pharaoh_Dynasties })
            {
                var layers = new LayersViewModel();
                layers.Initialise(game);

                var order = layers.GetLayers();
                Assert.AreEqual(order.IndexOf(LayerType.Impassable) - 1, order.IndexOf(LayerType.CampaignMasks), game.ToString());
                Assert.IsNotNull(layers.CampaignMaskLayer, game.ToString());
            }

            foreach (var game in new[] { GameTemplate.Rome2, GameTemplate.Attila, GameTemplate.Thrones_Of_Britannia, GameTemplate.Three_Kingdoms })
            {
                var layers = new LayersViewModel();
                layers.Initialise(game);

                Assert.IsFalse(layers.GetLayers().Contains(LayerType.CampaignMasks), game.ToString());
                Assert.IsNull(layers.CampaignMaskLayer, game.ToString());
            }
        }
    }
}
