namespace EsfLibrary
{
    /// <summary>
    /// The string hashes of the Assembly Kit's 32-bit CALibs, which order its hash maps of strings
    /// (see <see cref="CaHashMapOrder"/>). Characters are signed bytes.
    /// </summary>
    public static class CaStringHash
    {
        /// <summary>Rome II's: the even-indexed characters summed into the low 16 bits, the odd-indexed ones into the high 16.</summary>
        public static uint RomeII(string value)
        {
            ushort even = 0, odd = 0;
            for (int i = 0; i < value.Length; ++i)
            {
                if ((i & 1) == 0)
                {
                    even += (ushort)(sbyte)value[i];
                }
                else
                {
                    odd += (ushort)(sbyte)value[i];
                }
            }

            return ((uint)odd << 16) | even;
        }

        /// <summary>Attila's: from 0xF73A51C9, each character xored in from the last, shifted by 0, 8, 16 and 24 bits in turn.</summary>
        public static uint Attila(string value)
        {
            uint hash   = 0xF73A51C9;
            int shift   = 0;
            for (int i = value.Length - 1; i >= 0; --i)
            {
                hash   ^= (uint)(sbyte)value[i] << shift;
                shift   = (shift + 8) & 31;
            }

            return hash;
        }
    }
}
