using System;
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

        private LayerOpacityPreview(Layer layer, int[] affectedHexes, float[] channelsAtZeroOpacity, float[] channelSlopes, bool[] isCoveredWithoutLayer)
        {
            Layer                       = layer;
            AffectedHexes               = affectedHexes;
            this.channelsAtZeroOpacity  = channelsAtZeroOpacity;
            this.channelSlopes          = channelSlopes;
            this.isCoveredWithoutLayer  = isCoveredWithoutLayer;
        }

        public static LayerOpacityPreview Prepare(IReadOnlyList<Layer> stack, Layer layer, int hexCount)
        {
            int layerIndex = IndexOf(stack, layer);
            if (layerIndex < 0 || layer.IsVisible == false || layer.Colours == null)
            {
                return new LayerOpacityPreview(layer, Array.Empty<int>(), Array.Empty<float>(), Array.Empty<float>(), Array.Empty<bool>());
            }

            int capacity                = CountColouredHexes(layer.Colours, hexCount);
            var affectedHexes           = new int[capacity];
            var channelsAtZeroOpacity   = new float[capacity * 3];
            var channelSlopes           = new float[capacity * 3];
            var isCoveredWithoutLayer   = new bool[capacity];
            int affectedCount           = 0;

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

                var below   = LayerCompositor.Blend(belowColours, belowOpacities, hexIndex);
                int channel = affectedCount * 3;

                SetChannel(above.Red,   above.Transmittance, colour         & 0xFF, below.Red,   below.Transmittance, channelsAtZeroOpacity, channelSlopes, channel);
                SetChannel(above.Green, above.Transmittance, (colour >> 8)  & 0xFF, below.Green, below.Transmittance, channelsAtZeroOpacity, channelSlopes, channel + 1);
                SetChannel(above.Blue,  above.Transmittance, (colour >> 16) & 0xFF, below.Blue,  below.Transmittance, channelsAtZeroOpacity, channelSlopes, channel + 2);

                affectedHexes[affectedCount]            = hexIndex;
                isCoveredWithoutLayer[affectedCount]    = above.IsCovered || below.IsCovered;
                ++affectedCount;
            }

            if (affectedCount < capacity)
            {
                Array.Resize(ref affectedHexes, affectedCount);
                Array.Resize(ref channelsAtZeroOpacity, affectedCount * 3);
                Array.Resize(ref channelSlopes, affectedCount * 3);
                Array.Resize(ref isCoveredWithoutLayer, affectedCount);
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

        private static void SetChannel(float aboveChannel, float aboveTransmittance, int layerChannel, float belowChannel, float belowTransmittance,
                                       float[] channelsAtZeroOpacity, float[] channelSlopes, int channel)
        {
            float belowOverBackground = belowChannel + belowTransmittance * LayerBlend.ViewportBackgroundChannel;

            channelsAtZeroOpacity[channel]  = aboveChannel + aboveTransmittance * belowOverBackground;
            channelSlopes[channel]          = aboveTransmittance * (layerChannel - belowOverBackground);
        }

        private static int CountColouredHexes(int[] colours, int hexCount)
        {
            int count = 0;
            for (int hexIndex = 0; hexIndex < hexCount; ++hexIndex)
            {
                if (colours[hexIndex] != ColourTable.Zero)
                {
                    ++count;
                }
            }

            return count;
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
