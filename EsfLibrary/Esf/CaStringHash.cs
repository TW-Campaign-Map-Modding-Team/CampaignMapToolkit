namespace EsfLibrary
{
    /// <summary>
    /// The string hashes of the Assembly Kit's CALibs, which order its hash maps of strings (see
    /// <see cref="CaHashMap"/>). They take ASCII strings, the only ones <see cref="EsfWriter"/>
    /// writes; the builders hash chars as signed bytes.
    /// </summary>
    public static class CaStringHash
    {
        private const uint MURMUR_SEED = 0x4A545EED;

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

        /// <summary>
        /// The 64-bit CALibs' <c>CA::murmur_hash</c>: MurmurHash3's 32-bit x86 variant, seeded with
        /// 0x4A545EED.
        /// </summary>
        public static uint Murmur(string value)
        {
            const uint C1 = 0xCC9E2D51;
            const uint C2 = 0x1B873593;

            uint hash       = MURMUR_SEED;
            int blockEnd    = value.Length & ~3;

            for (int i = 0; i < blockEnd; i += 4)
            {
                uint block = (uint)(value[i] | value[i + 1] << 8 | value[i + 2] << 16 | value[i + 3] << 24);
                hash ^= RotateLeft(block * C1, 15) * C2;
                hash  = RotateLeft(hash, 13) * 5 + 0xE6546B64;
            }

            uint tail = 0;
            switch (value.Length & 3)
            {
                case 3: tail ^= (uint)value[blockEnd + 2] << 16; goto case 2;
                case 2: tail ^= (uint)value[blockEnd + 1] << 8;  goto case 1;
                case 1: tail ^= value[blockEnd];
                        hash ^= RotateLeft(tail * C1, 15) * C2;
                        break;
            }

            hash ^= (uint)value.Length;
            hash ^= hash >> 16;
            hash *= 0x85EBCA6B;
            hash ^= hash >> 13;
            hash *= 0xC2B2AE35;
            return hash ^ (hash >> 16);
        }

        private static uint RotateLeft(uint value, int bits)
        {
            return (value << bits) | (value >> (32 - bits));
        }
    }
}
