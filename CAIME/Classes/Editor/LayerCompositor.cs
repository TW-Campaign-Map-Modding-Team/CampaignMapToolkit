using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace CAIME
{
    public static class LayerCompositor
    {
        public static void ComposeAll(IReadOnlyList<Layer> stack, int[] output)
        {
            ContributorsBetween(stack, 0, stack.Count, output.Length, out int[][] colours, out byte[] opacities);

            for (int hexIndex = 0; hexIndex < output.Length; ++hexIndex)
            {
                output[hexIndex] = Blend(colours, opacities, hexIndex).Result();
            }
        }

        public static int ComposeHex(IReadOnlyList<Layer> stack, int hexIndex)
        {
            var blend = LayerBlend.Start();
            for (int index = 0; index < stack.Count; ++index)
            {
                var layer = stack[index];
                if (IsContributing(layer) && blend.Add(layer.Colours[hexIndex], layer.Opacity))
                {
                    break;
                }
            }

            return blend.Result();
        }

        public static bool CanChangeHex(IReadOnlyList<Layer> stack, Layer layer, int hexIndex)
        {
            if (layer.Opacity == 0)
            {
                return false;
            }

            var above = LayerBlend.Start();
            for (int index = 0; index < stack.Count && stack[index] != layer; ++index)
            {
                var layerAbove = stack[index];
                if (IsContributing(layerAbove) && above.Add(layerAbove.Colours[hexIndex], layerAbove.Opacity))
                {
                    return false;
                }
            }

            return true;
        }

        internal static void ContributorsBetween(IReadOnlyList<Layer> stack, int start, int end, int hexCount, out int[][] colours, out byte[] opacities)
        {
            var contributors = new List<Layer>(end - start);
            for (int index = start; index < end; ++index)
            {
                if (IsContributing(stack[index]))
                {
                    Debug.Assert(stack[index].Colours.Length >= hexCount, $"{stack[index].Name} colours are shorter than the grid");
                    contributors.Add(stack[index]);
                }
            }

            colours   = new int[contributors.Count][];
            opacities = new byte[contributors.Count];
            for (int index = 0; index < contributors.Count; ++index)
            {
                colours[index]   = contributors[index].Colours;
                opacities[index] = contributors[index].Opacity;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static LayerBlend Blend(int[][] colours, byte[] opacities, int hexIndex)
        {
            var blend = LayerBlend.Start();
            for (int layer = 0; layer < colours.Length; ++layer)
            {
                if (blend.Add(colours[layer][hexIndex], opacities[layer]))
                {
                    break;
                }
            }

            return blend;
        }

        private static bool IsContributing(Layer layer)
        {
            return layer.IsVisible && layer.Opacity > 0 && layer.Colours != null;
        }
    }
}
