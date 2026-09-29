using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class LayerOpacityPreviewTests
    {
        private const int HexCount = 3000;
        private const int AdjustedLayer = 3;

        [TestMethod]
        public void Compose_AgreesWithAFullRedraw_AtEveryOpacity()
        {
            foreach (var seed in new[] { 1, 2, 3 })
            {
                var stack = RandomStack(new Random(seed));
                var layer = stack[AdjustedLayer];
                layer.SetVisible(true);
                layer.Opacity = 128;

                var output = new int[HexCount];
                LayerCompositor.ComposeAll(stack, output);
                var preview = LayerOpacityPreview.Prepare(stack, layer, HexCount);
                var affected = new HashSet<int>(preview.AffectedHexes);

                foreach (byte opacity in new byte[] { 0, 1, 64, 200, 254, Layer.FullyOpaque, 128 })
                {
                    layer.Opacity = opacity;
                    preview.Compose(opacity, output);

                    var expected = new int[HexCount];
                    LayerCompositor.ComposeAll(stack, expected);

                    for (int hex = 0; hex < HexCount; ++hex)
                    {
                        var where = $"seed {seed}, opacity {opacity}, hex {hex}";
                        if (affected.Contains(hex))
                        {
                            AssertSameColour(expected[hex], output[hex], where);
                        }
                        else
                        {
                            Assert.AreEqual(expected[hex], output[hex], where);
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void HiddenLayer_AffectsNoHexes()
        {
            var stack = RandomStack(new Random(4));
            stack[AdjustedLayer].SetVisible(false);

            var preview = LayerOpacityPreview.Prepare(stack, stack[AdjustedLayer], HexCount);

            Assert.AreEqual(0, preview.AffectedHexes.Length);
        }

        [TestMethod]
        public void HexesTheLayerDoesNotColour_OrThatAreHiddenByAnOpaqueLayerAbove_AreNotAffected()
        {
            var top    = MakeLayer(Layer.FullyOpaque, 0, 0, Utility.ToRgba(1, 2, 3));
            var middle = MakeLayer(128, Utility.ToRgba(9, 9, 9), ColourTable.Zero, Utility.ToRgba(9, 9, 9));
            var stack  = new List<Layer> { top, middle };

            var preview = LayerOpacityPreview.Prepare(stack, middle, 3);

            CollectionAssert.AreEqual(new[] { 0 }, preview.AffectedHexes);
        }

        private static void AssertSameColour(int expected, int actual, string where)
        {
            if (expected == ColourTable.Zero || actual == ColourTable.Zero)
            {
                Assert.AreEqual(expected, actual, where);
                return;
            }

            Utility.RgbaDecompose(expected, out byte er, out byte eg, out byte eb, out _);
            Utility.RgbaDecompose(actual,   out byte ar, out byte ag, out byte ab, out _);

            Assert.AreEqual(er, ar, 1.0, $"red, {where}");
            Assert.AreEqual(eg, ag, 1.0, $"green, {where}");
            Assert.AreEqual(eb, ab, 1.0, $"blue, {where}");
        }

        private static List<Layer> RandomStack(Random random)
        {
            return Enumerable.Range(0, 8).Select(_ =>
            {
                var colours = Enumerable.Range(0, HexCount)
                                        .Select(__ => random.Next(3) == 0 ? ColourTable.Zero : random.Next(int.MinValue, int.MaxValue) | 1)
                                        .ToArray();
                var layer = MakeLayer((byte)(random.Next(3) == 0 ? Layer.FullyOpaque : random.Next(256)), colours);
                layer.SetVisible(random.Next(4) != 0);
                return layer;
            }).ToList();
        }

        private static Layer MakeLayer(byte opacity, params int[] colours)
        {
            var layer = new Layer(LayerType.Regions, isVisible: true);
            layer.SetColours(colours);
            layer.Opacity = opacity;
            return layer;
        }
    }
}
