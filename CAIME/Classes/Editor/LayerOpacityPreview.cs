using System.Collections.Generic;

namespace CAIME
{
    public class LayerOpacityPreview
    {
        private readonly float[] channelsAtZeroOpacity;
        private readonly float[] channelSlopes;
        private readonly bool[] isCoveredWithoutLayer;

        public Layer Layer { get; }
        public int[] AffectedHexes { get; }

        private LayerOpacityPreview(Layer layer, List<int> affectedHexes, List<float> channelsAtZeroOpacity, List<float> channelSlopes, List<bool> isCoveredWithoutLayer)
        {
            Layer                       = layer;
            AffectedHexes               = affectedHexes.ToArray();
            this.channelsAtZeroOpacity  = channelsAtZeroOpacity.ToArray();
            this.channelSlopes          = channelSlopes.ToArray();
            this.isCoveredWithoutLayer  = isCoveredWithoutLayer.ToArray();
        }

        public static LayerOpacityPreview Prepare(IReadOnlyList<Layer> stack, Layer layer, int hexCount)
        {
            var affectedHexes           = new List<int>();
            var channelsAtZeroOpacity   = new List<float>();
            var channelSlopes           = new List<float>();
            var isCoveredWithoutLayer   = new List<bool>();

            int layerIndex = IndexOf(stack, layer);
            if (layerIndex >= 0 && layer.IsVisible && layer.Colours != null)
            {
                LayerCompositor.ContributorsBetween(stack, 0, layerIndex, hexCount, out int[][] aboveColours, out byte[] aboveOpacities);
                LayerCompositor.ContributorsBetween(stack, layerIndex + 1, stack.Count, hexCount, out int[][] belowColours, out byte[] belowOpacities);

                for (int hexIndex = 0; hexIndex < hexCount; ++hexIndex)
                {
                    int colour = layer.Colours[hexIndex];
                    if (colour == ColourTable.Zero)
                    {
                        continue;
                    }

                    var above = LayerCompositor.Blend(aboveColours, aboveOpacities, hexIndex);
                    if (above.Transmittance < LayerBlend.NegligibleTransmittance)
                    {
                        continue;
                    }

                    var below = LayerCompositor.Blend(belowColours, belowOpacities, hexIndex);

                    AddChannel(above.Red,   above.Transmittance, colour         & 0xFF, below.Red,   below.Transmittance, channelsAtZeroOpacity, channelSlopes);
                    AddChannel(above.Green, above.Transmittance, (colour >> 8)  & 0xFF, below.Green, below.Transmittance, channelsAtZeroOpacity, channelSlopes);
                    AddChannel(above.Blue,  above.Transmittance, (colour >> 16) & 0xFF, below.Blue,  below.Transmittance, channelsAtZeroOpacity, channelSlopes);

                    affectedHexes.Add(hexIndex);
                    isCoveredWithoutLayer.Add(above.IsCovered || below.IsCovered);
                }
            }

            return new LayerOpacityPreview(layer, affectedHexes, channelsAtZeroOpacity, channelSlopes, isCoveredWithoutLayer);
        }

        public void Compose(byte opacity, int[] output)
        {
            float alpha = opacity * (1f / 255f);

            for (int index = 0; index < AffectedHexes.Length; ++index)
            {
                if (opacity == 0 && isCoveredWithoutLayer[index] == false)
                {
                    output[AffectedHexes[index]] = ColourTable.Zero;
                    continue;
                }

                int channel = index * 3;
                output[AffectedHexes[index]] = LayerBlend.ToRgba(channelsAtZeroOpacity[channel]     + alpha * channelSlopes[channel],
                                                                 channelsAtZeroOpacity[channel + 1] + alpha * channelSlopes[channel + 1],
                                                                 channelsAtZeroOpacity[channel + 2] + alpha * channelSlopes[channel + 2]);
            }
        }

        private static void AddChannel(float aboveChannel, float aboveTransmittance, int layerChannel, float belowChannel, float belowTransmittance,
                                       List<float> channelsAtZeroOpacity, List<float> channelSlopes)
        {
            float belowOverBackground = belowChannel + belowTransmittance * LayerBlend.ViewportBackgroundChannel;

            channelsAtZeroOpacity.Add(aboveChannel + aboveTransmittance * belowOverBackground);
            channelSlopes.Add(aboveTransmittance * (layerChannel - belowOverBackground));
        }

        private static int IndexOf(IReadOnlyList<Layer> stack, Layer layer)
        {
            for (int index = 0; index < stack.Count; ++index)
            {
                if (stack[index] == layer)
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
