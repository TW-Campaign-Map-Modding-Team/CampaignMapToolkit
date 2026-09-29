using System;
using System.Runtime.CompilerServices;

namespace CAIME
{
    internal struct LayerBlend
    {
        public const float ViewportBackgroundChannel = 0x1E;
        public const float NegligibleTransmittance   = 0.5f / 255f;

        private float red;
        private float green;
        private float blue;
        private float transmittance;
        private bool isCovered;
        private bool hasExactColour;
        private int exactColour;

        public bool IsCovered       => isCovered;
        public float Transmittance  => transmittance;
        public float Red            => red;
        public float Green          => green;
        public float Blue           => blue;

        public static LayerBlend Start()
        {
            return new LayerBlend { transmittance = 1f };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Add(int colour, byte opacity)
        {
            if (colour == ColourTable.Zero)
            {
                return false;
            }

            if (isCovered == false && opacity == Layer.FullyOpaque)
            {
                exactColour     = colour;
                hasExactColour  = true;
                isCovered       = true;
                red             = colour         & 0xFF;
                green           = (colour >> 8)  & 0xFF;
                blue            = (colour >> 16) & 0xFF;
                transmittance   = 0f;
                return true;
            }

            float weight = transmittance * opacity * (1f / 255f);

            red           += weight * (colour         & 0xFF);
            green         += weight * ((colour >> 8)  & 0xFF);
            blue          += weight * ((colour >> 16) & 0xFF);
            transmittance -= weight;
            isCovered      = true;

            return transmittance < NegligibleTransmittance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Result()
        {
            if (isCovered == false)
            {
                return ColourTable.Zero;
            }

            if (hasExactColour)
            {
                return exactColour;
            }

            return ToRgba(red   + transmittance * ViewportBackgroundChannel,
                          green + transmittance * ViewportBackgroundChannel,
                          blue  + transmittance * ViewportBackgroundChannel);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToRgba(float red, float green, float blue)
        {
            return Utility.ToRgba(ToChannel(red), ToChannel(green), ToChannel(blue));
        }

        private static byte ToChannel(float value)
        {
            return (byte)Math.Max(0f, Math.Min(value + 0.5f, 255f));
        }
    }
}
