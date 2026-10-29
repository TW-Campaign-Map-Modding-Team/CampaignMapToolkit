using System;

namespace CAIME
{
    /// <summary>
    /// A campaign mask from map.hex: a name, which a campaigns row's mask column refers to, and a
    /// bitmap of the hexes the mask takes away. The map_data builder writes the regions once for
    /// each mask, and a campaign plays on its own mask's copy (the unnamed mask's when it has none).
    /// </summary>
    public sealed class CampaignMask
    {
        public string Name      { get; }

        /// <summary>The u32 map.hex stores after the name. Every vanilla map has 0; its meaning is unknown.</summary>
        public uint   Value     { get; }

        /// <summary>
        /// One bit a hex, in rows of <see cref="BytesPerRow"/> bytes, lowest bit first. Empty when no
        /// hex is masked.
        /// </summary>
        public byte[] Bitmap    { get; private set; }

        public CampaignMask(string name, uint value = 0, byte[] bitmap = null)
        {
            Name    = name;
            Value   = value;
            Bitmap  = bitmap ?? Array.Empty<byte>();
        }

        public static int BytesPerRow(uint width) => (int)((width + 7) / 8);

        public static int BitmapSize(uint width, uint height) => BytesPerRow(width) * (int)height;

        public bool IsMasked(int column, int row, uint width)
        {
            int index = row * BytesPerRow(width) + column / 8;
            return index < Bitmap.Length && (Bitmap[index] & (1 << (column % 8))) != 0;
        }

        public void SetMasked(int column, int row, uint width, uint height, bool masked)
        {
            if (Bitmap.Length != BitmapSize(width, height))
            {
                Bitmap = new byte[BitmapSize(width, height)];
            }

            int index = row * BytesPerRow(width) + column / 8;
            int bit   = 1 << (column % 8);
            Bitmap[index] = masked ? (byte)(Bitmap[index] | bit) : (byte)(Bitmap[index] & ~bit);
        }

        /// <summary>
        /// This mask on a resized grid, where hex <c>i</c> of the new grid takes its bit from hex
        /// <c>sourceHexes[i]</c> of the old one.
        /// </summary>
        public CampaignMask Resampled(int[] sourceHexes, uint oldWidth, uint newWidth, uint newHeight)
        {
            var resampled = new CampaignMask(Name, Value);
            if (Bitmap.Length == 0)
            {
                return resampled;
            }

            for (int hexIndex = 0; hexIndex < sourceHexes.Length; ++hexIndex)
            {
                int source = sourceHexes[hexIndex];
                if (IsMasked(source % (int)oldWidth, source / (int)oldWidth, oldWidth))
                {
                    resampled.SetMasked(hexIndex % (int)newWidth, hexIndex / (int)newWidth, newWidth, newHeight, true);
                }
            }

            return resampled;
        }
    }
}
