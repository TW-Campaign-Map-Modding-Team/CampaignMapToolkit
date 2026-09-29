using System;

namespace CAIME
{
    public static class OpacityPercent
    {
        public static byte ToOpacity(double percent)
        {
            return (byte)Math.Round(Math.Max(0.0, Math.Min(percent, 100.0)) * Layer.FullyOpaque / 100.0);
        }

        public static double FromOpacity(byte opacity)
        {
            return opacity * 100.0 / Layer.FullyOpaque;
        }
    }
}
