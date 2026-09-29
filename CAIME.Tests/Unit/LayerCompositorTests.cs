using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CAIME.Tests.Unit
{
    [TestClass]
    public class LayerCompositorTests
    {
        private const int HexCount = 2000;
        private const int ViewportBackground = 0x1E;

        private static readonly int Red   = Utility.ToRgba(255, 0, 0);
        private static readonly int Blue  = Utility.ToRgba(0, 0, 255);
        private static readonly int White = Utility.ToRgba(255, 255, 255);
        private static readonly int Black = Utility.ToRgba(0, 0, 0);

        [TestMethod]
        public void AllLayersOpaque_ShowsTheFirstVisibleColour_BitForBit()
        {
            var random = new Random(1);
            var stack  = RandomStack(random, _ => Layer.FullyOpaque);

            var output = new int[HexCount];
            LayerCompositor.ComposeAll(stack, output);

            for (int hex = 0; hex < HexCount; ++hex)
            {
                var expected = stack.Where(layer => layer.IsVisible)
                                    .Select(layer => layer.Colours[hex])
                                    .FirstOrDefault(colour => colour != ColourTable.Zero);
                Assert.AreEqual(expected, output[hex], $"hex {hex}");
            }
        }

        [TestMethod]
        public void HalfOpaqueLayer_OverAnOpaqueLayer_BlendsEachChannel()
        {
            var stack = new[]
            {
                MakeLayer(visible: true, opacity: 128, Red),
                MakeLayer(visible: true, opacity: Layer.FullyOpaque, Blue),
            };

            var result = LayerCompositor.ComposeHex(stack, 0);

            AssertChannels(result, red: 255 * 128 / 255.0, green: 0, blue: 255 * (1 - 128 / 255.0));
        }

        [TestMethod]
        public void PartlyTransparentStack_WithNothingBelow_BlendsOverTheViewportBackground()
        {
            var stack = new[] { MakeLayer(visible: true, opacity: 128, White) };

            var result  = LayerCompositor.ComposeHex(stack, 0);
            var channel = 255 * 128 / 255.0 + ViewportBackground * (1 - 128 / 255.0);

            AssertChannels(result, channel, channel, channel);
        }

        [TestMethod]
        public void NoVisibleLayerColoursTheHex_ShowsNothing()
        {
            var stack = new[]
            {
                MakeLayer(visible: false, opacity: Layer.FullyOpaque, Red),
                MakeLayer(visible: true,  opacity: Layer.FullyOpaque, ColourTable.Zero),
            };

            Assert.AreEqual(ColourTable.Zero, LayerCompositor.ComposeHex(stack, 0));
        }

        [TestMethod]
        public void HiddenAndFullyTransparentLayers_ContributeNothing()
        {
            var stack = new[]
            {
                MakeLayer(visible: false, opacity: Layer.FullyOpaque, Red),
                MakeLayer(visible: true,  opacity: 0, Red),
                MakeLayer(visible: true,  opacity: Layer.FullyOpaque, Blue),
            };

            Assert.AreEqual(Blue, LayerCompositor.ComposeHex(stack, 0));
        }

        [TestMethod]
        public void OnlyAFullyTransparentLayerColoursTheHex_ShowsNothing()
        {
            var stack = new[] { MakeLayer(visible: true, opacity: 0, Red) };

            Assert.AreEqual(ColourTable.Zero, LayerCompositor.ComposeHex(stack, 0));
        }

        [TestMethod]
        public void ALayersOwnColourAlpha_IsIgnoredWhenBlending()
        {
            var halfAlphaRed = Utility.ToRgba(255, 0, 0, 128);
            var withOwnAlpha = LayerCompositor.ComposeHex(new[] { MakeLayer(true, 128, halfAlphaRed), MakeLayer(true, Layer.FullyOpaque, Black) }, 0);
            var opaqueRed    = LayerCompositor.ComposeHex(new[] { MakeLayer(true, 128, Red),          MakeLayer(true, Layer.FullyOpaque, Black) }, 0);

            Assert.AreEqual(opaqueRed, withOwnAlpha);
        }

        [TestMethod]
        public void BlendedColours_AreFullyOpaque()
        {
            var stack = new[] { MakeLayer(true, 64, Red), MakeLayer(true, 200, Blue) };

            Utility.RgbaDecompose(LayerCompositor.ComposeHex(stack, 0), out _, out _, out _, out byte alpha);

            Assert.AreEqual(255, alpha);
        }

        [TestMethod]
        public void ComposeHex_AgreesWithComposeAll_ForEveryHex()
        {
            var random = new Random(2);
            var stack  = RandomStack(random, r => (byte)(r.Next(3) == 0 ? Layer.FullyOpaque : r.Next(256)));

            var output = new int[HexCount];
            LayerCompositor.ComposeAll(stack, output);

            for (int hex = 0; hex < HexCount; ++hex)
            {
                Assert.AreEqual(output[hex], LayerCompositor.ComposeHex(stack, hex), $"hex {hex}");
            }
        }

        [TestMethod]
        public void ALayerWithoutColoursYet_IsSkipped()
        {
            var stack = new[] { new Layer(LayerType.Roads, isVisible: true), MakeLayer(true, Layer.FullyOpaque, Blue) };

            var output = new int[1];
            LayerCompositor.ComposeAll(stack, output);

            Assert.AreEqual(Blue, output[0]);
            Assert.AreEqual(Blue, LayerCompositor.ComposeHex(stack, 0));
        }

        private static List<Layer> RandomStack(Random random, Func<Random, byte> opacity)
        {
            var stack = new List<Layer>();
            for (int index = 0; index < 8; ++index)
            {
                var colours = new int[HexCount];
                for (int hex = 0; hex < HexCount; ++hex)
                {
                    colours[hex] = random.Next(3) == 0 ? ColourTable.Zero : random.Next(int.MinValue, int.MaxValue) | 1;
                }

                stack.Add(MakeLayer(visible: random.Next(4) != 0, opacity(random), colours));
            }

            return stack;
        }

        private static Layer MakeLayer(bool visible, byte opacity, params int[] colours)
        {
            var layer = new Layer(LayerType.Regions, isVisible: visible);
            layer.SetColours(colours);
            layer.Opacity = opacity;
            return layer;
        }

        private static void AssertChannels(int colour, double red, double green, double blue)
        {
            Utility.RgbaDecompose(colour, out byte r, out byte g, out byte b, out byte a);

            Assert.AreEqual(red,   r, 1.0, "red");
            Assert.AreEqual(green, g, 1.0, "green");
            Assert.AreEqual(blue,  b, 1.0, "blue");
            Assert.AreEqual(255,   a, "alpha");
        }
    }
}
